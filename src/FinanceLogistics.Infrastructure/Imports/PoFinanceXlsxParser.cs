using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

// Restricted reader for the observed Finance layout, not a general Excel/formula engine.
// XML number lexemes are retained instead of taking an intermediate double conversion.
public sealed partial class PoFileParser
{
    private sealed record ExcelNumericCell(string Lexeme);
    private sealed record SharedLiteral(string Formula, int Column, int FirstRow, int LastRow, string Origin);
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static XDocument ReadXml(ZipArchive zip, string path)
    {
        using var stream = (zip.GetEntry(path) ?? throw new InvalidDataException("Missing workbook part.")).Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 50L * 1024 * 1024 });
        return XDocument.Load(xml);
    }

    private static string PartPath(string target)
    {
        // Resolve OPC absolute paths and workbook-relative paths, never a filesystem/remote URL.
        if (string.IsNullOrWhiteSpace(target) || target.Contains('\\') || target.Contains(':')
            || target.Contains('?') || target.Contains('#') || target.Contains('%'))
            throw new InvalidDataException("Unsupported workbook relationship.");
        var segments = new List<string>();
        foreach (var part in (target.StartsWith('/') ? target[1..] : "xl/" + target).Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (segments.Count == 0) throw new InvalidDataException(); segments.RemoveAt(segments.Count - 1); }
            else segments.Add(part);
        }
        var result = string.Join('/', segments);
        if (!result.StartsWith("xl/", StringComparison.Ordinal)) throw new InvalidDataException();
        return result;
    }

    private static string ColumnLabel(int index)
    {
        var text = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) text = (char)('A' + (n - 1) % 26) + text;
        return text;
    }

    private static int ColumnIndex(string reference, int row)
    {
        var match = Regex.Match(reference, "^([A-Z]{1,3})([1-9][0-9]{0,6})$");
        if (!match.Success || int.Parse(match.Groups[2].Value, Invariant) != row || row > 1_048_576)
            throw new InvalidDataException("Invalid cell coordinates.");
        var column = 0;
        foreach (var ch in match.Groups[1].Value) column = column * 26 + ch - 'A' + 1;
        if (column > 100) throw new InvalidDataException("Too many columns.");
        return column - 1;
    }

    private static string RichText(XElement? element) => element is null ? "" :
        string.Concat(element.Descendants(S + "t").Where(x => !x.Ancestors(S + "rPh").Any()).Select(x => x.Value));

    private static string? SourceText(XElement cell, string[] shared)
    {
        var value = cell.Element(S + "v")?.Value;
        return (string?)cell.Attribute("t") switch {
            "inlineStr" => RichText(cell.Element(S + "is")),
            "s" => int.TryParse(value, NumberStyles.None, Invariant, out var i) && i >= 0 && i < shared.Length
                ? shared[i] : throw new InvalidDataException("Invalid shared-string reference."),
            _ => value
        };
    }

    private static string FinanceHeaderKey(string text) => HeaderKey(text) is "PO CURR" ? "PO CURRENCY" : HeaderKey(text);

    // Decimal conversion without rounding through double or decimal.TryParse exponent underflow.
    private static string ExpandExcelNumber(string text)
    {
        var m = Regex.Match(text, "^([+-]?)([0-9]+)(?:\\.([0-9]+))?(?:[Ee]([+-]?[0-9]{1,3}))?$");
        if (!m.Success) return text;
        var exponent = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, Invariant) : 0;
        if (Math.Abs(exponent) > 100) return "NUMBER_OUT_OF_RANGE";
        var digits = m.Groups[2].Value + m.Groups[3].Value;
        var scale = m.Groups[3].Value.Length - exponent;
        var number = scale <= 0 ? digits + new string('0', -scale)
            : scale >= digits.Length ? "0." + new string('0', scale - digits.Length) + digits
            : digits.Insert(digits.Length - scale, ".");
        return m.Groups[1].Value + number;
    }

    private static bool AllowedAmountFormula(string formula, int record, int[] map)
    {
        var factors = formula.Replace("$", "", StringComparison.Ordinal).Split('*').Select(x => x.Trim()).ToArray();
        var price = ColumnLabel(map[5]) + record.ToString(Invariant);
        var rate = ColumnLabel(map[10]) + record.ToString(Invariant);
        var qty = ColumnLabel(map[4]) + record.ToString(Invariant);
        return factors.Length is 2 or 3 && factors.Distinct(StringComparer.Ordinal).Count() == factors.Length
            && factors.Contains(price) && factors.Contains(rate) && (factors.Length == 2 || factors.Contains(qty));
    }

    private static void ParseFinanceExcel(Stream stream, ParsedPoFile result, DateOnly start, DateOnly end,
        int maxRows, string periodMode, CancellationToken ct)
    {
        if (!stream.CanSeek || stream.Length < 8) throw new InvalidDataException("Invalid XLSX stream.");
        var signature = new byte[4]; stream.Position = 0; stream.ReadExactly(signature); stream.Position = 0;
        if (!signature.SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
        { Error(result, "FILE_EXTENSION_MISMATCH", "Mẫu Finance cần XLSX thật, không phải XLS/CSV đổi tên hoặc file được mã hóa."); return; }
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > 50L * 1024 * 1024
            || zip.Entries.Select(x => x.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count)
        { Error(result, "EXCEL_EXPANSION_LIMIT", "Excel vượt giới hạn giải nén hoặc có mục trùng tên."); return; }
        var book = ReadXml(zip, "xl/workbook.xml");
        var sheets = book.Root?.Element(S + "sheets")?.Elements(S + "sheet").ToArray() ?? [];
        if (sheets.Length != 1 || (string?)sheets[0].Attribute("name") != "PO update")
        { Error(result, "SHEET_LAYOUT", "PO Finance V1 cần đúng một sheet tên PO update."); return; }
        var relations = ReadXml(zip, "xl/_rels/workbook.xml.rels").Root?.Elements(P + "Relationship").ToArray() ?? [];
        var relationId = (string?)sheets[0].Attribute(R + "id");
        var targets = relations.Where(x => (string?)x.Attribute("Id") == relationId).ToArray();
        if (targets.Length != 1 || (string?)targets[0].Attribute("TargetMode") == "External"
            || (string?)targets[0].Attribute("Type") != R.NamespaceName + "/worksheet")
            throw new InvalidDataException("Invalid sheet relationship.");
        var sheet = ReadXml(zip, PartPath((string?)targets[0].Attribute("Target") ?? ""));
        var stringTargets = relations.Where(x => (string?)x.Attribute("Type") == R.NamespaceName + "/sharedStrings").ToArray();
        if (stringTargets.Length > 1 || stringTargets.Any(x => (string?)x.Attribute("TargetMode") == "External"))
            throw new InvalidDataException("Invalid shared strings.");
        var shared = stringTargets.Length == 0 ? Array.Empty<string>() :
            ReadXml(zip, PartPath((string?)stringTargets[0].Attribute("Target") ?? "")).Root?.Elements(S + "si").Select(RichText).ToArray() ?? [];
        var epoch = (string?)book.Root?.Element(S + "workbookPr")?.Attribute("date1904");
        if (epoch is not (null or "0" or "1" or "false" or "true")) throw new InvalidDataException("Unknown Excel date system.");
        var date1904 = epoch is "1" or "true";
        var merges = sheet.Root?.Element(S + "mergeCells")?.Elements(S + "mergeCell")
            .Select(x => (string?)x.Attribute("ref") ?? "").ToArray() ?? [];
        var expectedMerges = Enumerable.Range(0, 17).Select(x => $"{ColumnLabel(x)}1:{ColumnLabel(x)}2").ToHashSet();
        if (merges.Length != 17 || !expectedMerges.SetEquals(merges))
        { Error(result, "HEADER_LAYOUT", "Cần 17 tiêu đề gộp dọc hàng 1–2 (A1:A2 đến Q1:Q2); dữ liệu bắt đầu hàng 3."); return; }
        var xmlRows = sheet.Root?.Element(S + "sheetData")?.Elements(S + "row").ToArray() ?? [];
        var rows = new SortedDictionary<int, Dictionary<int, XElement>>();
        foreach (var row in xmlRows)
        {
            ct.ThrowIfCancellationRequested();
            if (!int.TryParse((string?)row.Attribute("r"), NumberStyles.None, Invariant, out var record)
                || record < 1 || record > 1_048_576 || rows.ContainsKey(record)) throw new InvalidDataException("Invalid/repeated row.");
            var cells = new Dictionary<int, XElement>();
            foreach (var cell in row.Elements(S + "c"))
                if (!cells.TryAdd(ColumnIndex((string?)cell.Attribute("r") ?? "", record), cell))
                    throw new InvalidDataException("Repeated cell.");
            rows.Add(record, cells);
        }
        if (!rows.TryGetValue(1, out var heading)) { Error(result, "HEADER_LAYOUT", "Thiếu hàng tiêu đề 1."); return; }
        var headers = Enumerable.Range(0, 17).Select(i => heading.TryGetValue(i, out var c) ? SourceText(c, shared) ?? "" : "").ToArray();
        if (heading.Any(x => x.Value.Element(S + "f") is not null || x.Key >= 17 && !string.IsNullOrWhiteSpace(SourceText(x.Value, shared)))
            || rows.TryGetValue(2, out var second) && second.Values.Any(c => c.Element(S + "f") is not null || !string.IsNullOrWhiteSpace(SourceText(c, shared))))
        { Error(result, "HEADER_LAYOUT", "Tiêu đề phải là giá trị ở hàng 1; hàng 2 không chứa dữ liệu hoặc công thức."); return; }
        var keys = headers.Select(FinanceHeaderKey).ToArray();
        var expectedKeys = PoProfile.Headers.Select(HeaderKey).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != 17 || expectedKeys.Any(x => !keys.Contains(x)))
        { Error(result, "HEADER_MISMATCH", "PO Finance cần đủ 17 tiêu đề; PO Curr được nhận là PO Currency. Không tự đoán cột khác."); return; }
        var map = expectedKeys.Select(x => Array.IndexOf(keys, x)).ToArray();
        // Excel compresses repeated literal invoice formulas as shared groups. Expanding a literal
        // needs no reference translation or formula engine. Other shared formulas remain unsupported.
        var sharedLiterals = new Dictionary<string, SharedLiteral>(StringComparer.Ordinal);
        foreach (var (rowNumber, sourceCells) in rows)
        foreach (var (column, cell) in sourceCells)
        {
            var f = cell.Element(S + "f");
            if (f is null || (string?)f.Attribute("t") != "shared" || string.IsNullOrEmpty(f.Value)) continue;
            var id = (string?)f.Attribute("si");
            var range = Regex.Match((string?)f.Attribute("ref") ?? "", "^([A-Z]+)([1-9][0-9]*):([A-Z]+)([1-9][0-9]*)$");
            if (id is null || !Regex.IsMatch(id, "^[0-9]{1,9}$") || !range.Success || column != map[13]
                || f.Attributes().Any(x => x.Name.LocalName is not ("t" or "si" or "ref"))
                || !Regex.IsMatch(f.Value, "^\"((?:[^\"]|\"\")*)\"$")
                || range.Groups[1].Value != ColumnLabel(column) || range.Groups[3].Value != ColumnLabel(column)
                || !int.TryParse(range.Groups[2].Value, out var first) || !int.TryParse(range.Groups[4].Value, out var last)
                || first != rowNumber || first < 3 || last < first || last > 1_048_576) continue;
            if (!sharedLiterals.TryAdd(id, new(f.Value, column, first, last, ColumnLabel(column) + rowNumber.ToString(Invariant))))
                throw new InvalidDataException("Repeated shared formula definition.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var businessKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        var characters = 0; var literals = 0;
        foreach (var (record, sourceCells) in rows.Where(x => x.Key >= 3))
        {
            ct.ThrowIfCancellationRequested();
            if (sourceCells.Values.All(c => c.Element(S + "f") is null && string.IsNullOrWhiteSpace(SourceText(c, shared)))) continue;
            var count = Math.Max(17, sourceCells.Keys.DefaultIfEmpty(16).Max() + 1);
            var raw = new PoCellDto[count]; var values = new object?[count];
            var firstIssue = result.Issues.Count;
            for (var i = 0; i < count; i++)
            {
                var address = ColumnLabel(i) + record.ToString(Invariant);
                var header = i < 17 ? headers[i] : $"Extra {i + 1}";
                sourceCells.TryGetValue(i, out var cell);
                var formulaElement = cell?.Element(S + "f");
                var formula = formulaElement?.Value;
                var formulaKind = (string?)formulaElement?.Attribute("t");
                var sharedId = (string?)formulaElement?.Attribute("si");
                var formulaOrigin = formula is null ? null : address;
                var supportedShared = formulaKind == "shared" && sharedId is not null
                    && sharedLiterals.TryGetValue(sharedId, out var group)
                    && group.Column == i && record >= group.FirstRow && record <= group.LastRow
                    && !formulaElement!.Attributes().Any(x => x.Name.LocalName is not ("t" or "si" or "ref"))
                    && (record == group.FirstRow || formula == "" && formulaElement!.Attribute("ref") is null);
                if (supportedShared)
                {
                    var literalGroup = sharedLiterals[sharedId!];
                    formula = literalGroup.Formula; formulaOrigin = literalGroup.Origin;
                }
                var text = cell is null ? null : SourceText(cell, shared);
                var type = (string?)cell?.Attribute("t") ?? "n";
                raw[i] = new(i + 1, header, text, formula is not null ? "Formula" : cell is null || text is null ? "Empty" : type,
                    address, formula is null ? null : "=" + formula, formula is null ? null : cell?.Element(S + "v")?.Value,
                    formulaOrigin, formula is null ? null : formulaKind ?? "normal", sharedId);
                void Issue(string severity, string code, string message) => result.Issues.Add(new(severity, code, header, $"{address}: {message}", record, "PO update"));
                if (type == "e") { Issue("ERROR", "EXCEL_CELL_ERROR", "Ô Excel đang lỗi; cần sửa ở nguồn."); continue; }
                if (formula is not null)
                {
                    if (formula.Length > 8192 || formulaElement!.Attributes().Any() && !supportedShared)
                    { Issue("ERROR", "FORMULA_UNSUPPORTED", "Chỉ shared formula dạng chuỗi hóa đơn có nhóm gốc hợp lệ được hỗ trợ; array/công thức khác hoặc quá dài bị chặn."); continue; }
                    if (cell?.Element(S + "v") is null || string.IsNullOrEmpty(text))
                    { Issue("ERROR", "FORMULA_CACHE_MISSING", "Công thức thiếu kết quả lưu; tính và lưu lại file bằng Excel rồi nộp bản mới."); continue; }
                    var literal = Regex.Match(formula, "^\"((?:[^\"]|\"\")*)\"$");
                    if (i == map[13] && literal.Success)
                    {
                        var decoded = literal.Groups[1].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
                        if (type != "str" || decoded != text)
                        { Issue("ERROR", "FORMULA_CACHE_MISMATCH", "Kết quả lưu khác chuỗi trong công thức hóa đơn."); continue; }
                        values[i] = decoded; literals++; continue;
                    }
                    if (i != map[12] || type != "n" || !AllowedAmountFormula(formula, record, map))
                    { Issue("ERROR", "FORMULA_UNSUPPORTED", "V1 chỉ nhận chuỗi hóa đơn và phép nhân đơn giá × tỷ giá (có thể × số lượng) cùng dòng ở cột Purchase Functional Amount."); continue; }
                    Issue("WARNING", "FORMULA_CACHED_VALUE", "Dùng kết quả Excel đã lưu, không tính lại công thức. Finance cần đối chiếu trước khi xác nhận.");
                }
                if (text is null) continue;
                values[i] = type switch {
                    "n" => new ExcelNumericCell(text),
                    "s" or "str" or "inlineStr" => text,
                    "d" when DateTime.TryParse(text, Invariant, DateTimeStyles.RoundtripKind, out var date) => date,
                    _ => null
                };
                if (values[i] is null) Issue("ERROR", "CELL_TYPE_UNSUPPORTED", "Kiểu ô chưa được hỗ trợ; không tự chuyển Boolean thành số.");
            }
            // Formatting-only cells to the right are harmless; populated extra columns must block confirmation.
            while (raw.Length > 17 && raw[^1].Text is null && raw[^1].Formula is null)
            { Array.Resize(ref raw, raw.Length - 1); Array.Resize(ref values, values.Length - 1); }
            if (!WithinLimits(result, raw.Select(x => (x.Text ?? "") + (x.Formula ?? "")).ToArray(), maxRows, ref characters)) break;
            AddRow(result, values, headers, map, "PO update", record, start, end, seen, raw, true, date1904, periodMode);
            // Formula/type errors predate AddRow's field validation, so invalidate the typed row explicitly.
            if (result.Issues.Skip(firstIssue).Any(x => x.Severity == "ERROR")) result.Rows[^1] = result.Rows[^1] with { Line = null };
            var line = result.Rows[^1].Line;
            if (line is not null)
            {
                var key = PoProfile.Serialize(new[] { line.SourceCodeText, line.ReceiptNumber, line.PoNumber, line.PoLineReference });
                if (businessKeys.TryGetValue(key, out var previous))
                    result.Issues.Add(new("WARNING", "RECEIPT_KEY_REPEATED", null,
                        $"Souce + Receipt Num + PO Num + PO Line trùng dòng {previous}. Giữ cả hai; đây chưa phải khóa nghiệp vụ đã chốt.", record, "PO update"));
                else businessKeys.Add(key, record);
            }
        }
        if (literals > 0) result.Issues.Add(new("INFO", "LITERAL_INVOICE_FORMULAS", null,
            $"Đã đọc {literals} công thức chuỗi hóa đơn; giữ số 0 đầu và công thức gốc trong raw."));
        if (periodMode == PoProfile.YearToPeriod && result.Rows.Count > 0)
            result.Issues.Add(new("WARNING", "YEAR_TO_PERIOD_SNAPSHOT", null,
                $"Kỳ này nhận snapshot từ 01/01/{start.Year} đến trước {end:yyyy-MM-dd}. Xác nhận thay toàn bộ bản PO của kỳ; dữ liệu nhiều tháng không được cộng dồn giữa các phiên bản."));
    }
}
