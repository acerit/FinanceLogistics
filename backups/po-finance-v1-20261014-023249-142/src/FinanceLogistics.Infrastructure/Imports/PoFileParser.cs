using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ExcelDataReader;
using Microsoft.VisualBasic.FileIO;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Domain.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

public sealed record ParsedPoRow(int RecordNumber, string SheetName, PoCellDto[] Cells, PoUpdateLine? Line);
public sealed record ParsedPoIssue(string Severity, string Code, string? Column, string Message,
    int? RecordNumber = null, string? SheetName = null);
public sealed class ParsedPoFile
{
    public List<ParsedPoRow> Rows { get; } = [];
    public List<ParsedPoIssue> Issues { get; } = [];
    public int Errors => Issues.Count(x => x.Severity == "ERROR");
    public int Warnings => Issues.Count(x => x.Severity == "WARNING");
}

// No Excel engine is invoked; formulas are not recalculated. See the DEV profile in README.
public sealed class PoFileParser
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const int MaxRawCharacters = 4_000_000;
    private static string HeaderKey(string value)
    {
        var key = Regex.Replace(value.Trim().TrimStart('\uFEFF'), "\\s+", " ").ToUpperInvariant();
        return key switch { "SOURCE" => "SOUCE", "INVOICE NO" => "INVOICE NO.", _ => key };
    }

    public ParsedPoFile Parse(Stream stream, string originalName, DateOnly start, DateOnly end,
        int maxRows = PoProfile.MaxRows, CancellationToken ct = default)
    {
        var result = new ParsedPoFile();
        try
        {
            var extension = Path.GetExtension(originalName).ToLowerInvariant();
            if (extension == ".csv") ParseCsv(stream, result, start, end, maxRows, ct);
            else if (extension is ".xlsx" or ".xls") ParseExcel(stream, extension, result, start, end, maxRows, ct);
            else Error(result, "FILE_TYPE_UNSUPPORTED", "Chỉ nhận .csv, .xlsx hoặc .xls đúng định dạng.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is DecoderFallbackException or MalformedLineException
            or InvalidDataException or XmlException or FormatException or ArgumentException
            || ex.GetType().Namespace?.StartsWith("ExcelDataReader", StringComparison.Ordinal) == true)
        {
            Error(result, "FILE_UNREADABLE", "Không đọc được file. Kiểm tra định dạng thực, UTF-8, file khóa/mật khẩu và header.");
        }
        catch (IOException) { throw; } // Worker retries genuine storage failures.
        if (result.Rows.Count == 0 && result.Errors == 0)
            Error(result, "EMPTY_DATA_BATCH", "File chỉ có tiêu đề hoặc không có dòng dữ liệu; không thể xóa bản hiện hành bằng file rỗng.");
        return result;
    }

    private static void ParseCsv(Stream stream, ParsedPoFile result, DateOnly start, DateOnly end, int maxRows, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false,
            bufferSize: 8192, leaveOpen: true);
        using var csv = new TextFieldParser(reader) { TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        csv.SetDelimiters(",");
        if (csv.EndOfData) return;
        var headers = csv.ReadFields() ?? [];
        var map = MapHeaders(headers, result);
        if (map is null) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var record = 1; var characters = 0;
        while (!csv.EndOfData)
        {
            ct.ThrowIfCancellationRequested();
            record++;
            var fields = csv.ReadFields() ?? [];
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (!WithinLimits(result, fields, maxRows, ref characters)) break;
            AddRow(result, fields.Cast<object?>().ToArray(), headers, map, "CSV", record, start, end, seen);
        }
    }

    private static void ParseExcel(Stream stream, string extension, ParsedPoFile result,
        DateOnly start, DateOnly end, int maxRows, CancellationToken ct)
    {
        if (!stream.CanSeek) throw new ArgumentException("Excel input must be seekable.");
        if (stream.Length < 8) throw new InvalidDataException("Truncated workbook.");
        var signature = new byte[8]; stream.ReadExactly(signature); stream.Position = 0;
        var isZip = signature[0] == 0x50 && signature[1] == 0x4B && signature[2] == 0x03 && signature[3] == 0x04;
        var isOle = signature.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
        if ((extension == ".xlsx" && !isZip) || (extension == ".xls" && !isOle))
        { Error(result, "FILE_EXTENSION_MISMATCH", "Nội dung không khớp đuôi file. XLS cần file Excel 97–2003 thực; đổi tên không phải chuyển định dạng."); return; }
        if (extension == ".xlsx")
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > 50L * 1024 * 1024)
            { Error(result, "EXCEL_EXPANSION_LIMIT", "Nội dung Excel vượt giới hạn giải nén 50 MiB/2000 mục."); return; }
            foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                && x.FullName.EndsWith(".xml", StringComparison.Ordinal)))
            {
                using var input = entry.Open();
                using var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null, MaxCharactersInDocument = 50L * 1024 * 1024 });
                while (xml.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "f")
                    { Error(result, "FORMULA_NOT_ALLOWED", "Mẫu DEV nhận giá trị. Hãy tạo bản sao Paste values; giữ file gốc ở nghiệp vụ."); return; }
                }
            }
        }
        else
            result.Issues.Add(new("WARNING", "XLS_CACHED_VALUES", null,
                "XLS cũ được đọc theo giá trị lưu trong file; bộ đọc không tính lại hay xác minh công thức. Finance phải kiểm tra giá trị trước khi xác nhận."));
        stream.Position = 0;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var excel = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
        if (excel.ResultsCount != 1 || excel.Name != "PO_UPDATED")
        { Error(result, "SHEET_LAYOUT", "Mẫu DEV cần đúng một sheet tên PO_UPDATED, header ở hàng 1."); return; }
        if (!excel.Read()) return;
        var headerValues = Values(excel);
        var headers = headerValues.Select(CellText).Select(x => x ?? "").ToArray();
        var map = MapHeaders(headers, result);
        if (map is null) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var record = 1; var characters = 0;
        while (excel.Read())
        {
            ct.ThrowIfCancellationRequested(); record++;
            var values = Values(excel);
            if (values.All(x => string.IsNullOrWhiteSpace(CellText(x)))) continue;
            if (!WithinLimits(result, values.Select(x => CellText(x) ?? "").ToArray(), maxRows, ref characters)) break;
            // Excel may omit trailing empty fields; the approved header defines their positions.
            if (values.Length < headers.Length) Array.Resize(ref values, headers.Length);
            AddRow(result, values, headers, map, excel.Name, record, start, end, seen);
        }
    }

    private static object?[] Values(IExcelDataReader reader)
    {
        var values = new object?[reader.FieldCount];
        for (var i = 0; i < values.Length; i++) values[i] = reader.GetValue(i);
        var count = values.Length;
        while (count > 0 && string.IsNullOrWhiteSpace(CellText(values[count - 1]))) count--;
        return values[..count];
    }
    private static bool WithinLimits(ParsedPoFile result, string[] values, int maxRows, ref int characters)
    {
        if (result.Rows.Count >= Math.Min(maxRows, PoProfile.MaxRows))
        { Error(result, "ROW_LIMIT", "Vượt giới hạn số dòng; kết quả đang hiển thị chỉ là phần đã đọc và không được xác nhận."); return false; }
        if (values.Length > 100 || values.Any(x => x.Length > 32767))
        { Error(result, "CELL_LIMIT", "Dòng có quá nhiều cột hoặc ô quá dài."); return false; }
        characters += values.Sum(x => x.Length);
        if (characters > MaxRawCharacters)
        { Error(result, "TEXT_LIMIT", "Dữ liệu vượt giới hạn 4 triệu ký tự của mốc DEV."); return false; }
        return true;
    }
    private static int[]? MapHeaders(string[] headers, ParsedPoFile result)
    {
        if (headers.Length != 17)
        { Error(result, "HEADER_COUNT", $"Cần đúng 17 cột, đọc được {headers.Length}. Header phải ở hàng đầu tiên."); return null; }
        var keys = headers.Select(HeaderKey).ToArray();
        var expected = PoProfile.Headers.Select(HeaderKey).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != 17)
        { Error(result, "HEADER_DUPLICATED", "Tiêu đề cột bị trùng sau khi chuẩn hóa."); return null; }
        var missing = expected.Where(x => !keys.Contains(x, StringComparer.Ordinal)).ToArray();
        if (missing.Length != 0)
        { Error(result, "HEADER_MISMATCH", "Thiếu/sai tiêu đề: " + string.Join(", ", missing)); return null; }
        return expected.Select(x => Array.IndexOf(keys, x)).ToArray();
    }
    private static string? CellText(object? value) => value switch
    {
        null or DBNull => null,
        DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss", Invariant),
        double d => d.ToString("0.############################", Invariant),
        float f => f.ToString("0.############################", Invariant),
        IFormattable formattable => formattable.ToString(null, Invariant),
        _ => value.ToString()
    };

    private static void AddRow(ParsedPoFile result, object?[] values, string[] headers, int[] map,
        string sheet, int record, DateOnly start, DateOnly end, HashSet<string> seen)
    {
        var before = result.Issues.Count;
        void Issue(string severity, string code, int? index, string message) =>
            result.Issues.Add(new(severity, code, index is null ? null : PoProfile.Headers[index.Value], message, record, sheet));
        var cells = values.Select((value, index) => new PoCellDto(index + 1,
            index < headers.Length ? headers[index] : $"Extra {index + 1}", CellText(value),
            value?.GetType().Name ?? "Empty")).ToArray();
        if (values.Length != 17)
        {
            Issue("ERROR", "ROW_COLUMN_COUNT", null, $"Dòng có {values.Length} ô thay vì 17.");
            result.Rows.Add(new(record, sheet, cells, null)); return;
        }
        object? Value(int index) => values[map[index]];
        string? Text(int index, int maxLength, bool required = false, bool identifier = false)
        {
            var value = Value(index);
            var text = CellText(value)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (required) Issue("ERROR", "REQUIRED", index, "Ô bắt buộc đang trống.");
                return null;
            }
            if (text.Length > maxLength) Issue("ERROR", "TEXT_TOO_LONG", index, $"Tối đa {maxLength} ký tự.");
            if (identifier && value is not string)
            {
                Issue("WARNING", "NUMERIC_IDENTIFIER", index, "Mã được lưu dưới dạng số trong Excel; cần kiểm tra số 0 đầu và độ chính xác. Nên xuất mã dạng Text.");
                if (Regex.Replace(text, "[^0-9]", "").Length > 15)
                    Issue("ERROR", "IDENTIFIER_PRECISION", index, "Mã số Excel dài hơn 15 chữ số có thể đã mất chính xác. Cần xuất lại từ nguồn dạng Text.");
            }
            return text;
        }
        DateOnly? Date(int index, bool required = false)
        {
            var value = Value(index);
            if (value is DateTime date)
            {
                if (date.TimeOfDay != TimeSpan.Zero) Issue("WARNING", "DATE_TIME_PART", index, "Chỉ lấy phần ngày; nguồn có thêm giờ.");
                return DateOnly.FromDateTime(date);
            }
            var text = Text(index, 40, required);
            if (text is null) return null;
            if (DateOnly.TryParseExact(text, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var parsed)) return parsed;
            Issue("ERROR", "INVALID_DATE", index, "Ngày dạng text phải là yyyy-MM-dd; Excel có thể dùng ô Date thực.");
            return null;
        }
        decimal? Number(int index, int scale, bool required = false)
        {
            var text = Text(index, 100, required);
            if (text is null) return null;
            if (!Regex.IsMatch(text, "^[+-]?[0-9]+(?:\\.[0-9]+)?$"))
            { Issue("ERROR", "INVALID_NUMBER", index, "Dùng dấu chấm thập phân, không dấu phân cách hàng nghìn, không N/A hay ký hiệu tiền."); return null; }
            var parts = text.TrimStart('+', '-').Split('.');
            var digits = parts[0].TrimStart('0').Length;
            var fraction = parts.Length == 2 ? parts[1].TrimEnd('0').Length : 0;
            if (digits > 28 - scale || fraction > scale || !decimal.TryParse(text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant, out var number))
            { Issue("ERROR", "DECIMAL_PRECISION", index, $"Vượt decimal(28,{scale}); hệ thống không tự làm tròn."); return null; }
            return number;
        }
        var line = new PoUpdateLine
        {
            SourceCodeText = Text(0, 100), ReceiptNumber = Text(1, 120, true, true),
            TransactionDate = Date(2, true), PoNumber = Text(3, 120, true, true),
            ReceiptQuantity = Number(4, 6, true), PoUnitPrice = Number(5, 8),
            PoLineReference = Text(6, 50, identifier: true), PoCurrency = Text(7, 10, true)?.ToUpperInvariant(),
            PoRateDate = Date(8), PoRate = Number(9, 10), ReceiptRate = Number(10, 10),
            ReceiptRateDate = Date(11), PurchaseFunctionalAmount = Number(12, 8),
            InvoiceNumber = Text(13, 200, identifier: true), InvoiceFunctionalAmount = Number(14, 8),
            InvoiceAmount = Number(15, 8), LineDescription = Text(16, 2000)
        };
        if (line.TransactionDate is { } trx && (trx < start || trx >= end))
            Issue("ERROR", "WRONG_PERIOD", 2, "Trx date nằm ngoài kỳ đã chọn. Mẫu DEV dùng dữ liệu riêng từng tháng.");
        if (line.PoCurrency is { } currency && !Regex.IsMatch(currency, "^[A-Z]{3}$"))
            Issue("ERROR", "CURRENCY_FORMAT", 7, "Tiền tệ gồm 3 chữ cái, ví dụ VND hoặc USD.");
        if (line.InvoiceNumber is null) Issue("WARNING", "INVOICE_MISSING", 13, "Chưa có số hóa đơn; giữ trống để bổ sung sau.");
        if (new[] { line.ReceiptQuantity, line.PoUnitPrice, line.PurchaseFunctionalAmount,
                line.InvoiceFunctionalAmount, line.InvoiceAmount }.Any(x => x < 0))
            Issue("WARNING", "NEGATIVE_VALUE", null, "Có số âm; cần xác nhận đây là hoàn trả/điều chỉnh hợp lệ.");
        if (!seen.Add(PoProfile.Serialize(cells.Select(x => x.Text))))
            Issue("WARNING", "REPEATED_ROW", null, "Dòng giống hệt một dòng trước đó; hệ thống giữ cả hai, không tự xóa.");
        result.Rows.Add(new(record, sheet, cells, result.Issues.Skip(before).Any(x => x.Severity == "ERROR") ? null : line));
    }
    private static void Error(ParsedPoFile result, string code, string message) =>
        result.Issues.Add(new("ERROR", code, null, message));
}
