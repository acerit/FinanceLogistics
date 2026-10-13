using System.Text;
using FinanceLogistics.Infrastructure.Imports;

if (args.Length != 1) throw new ArgumentException("Usage: dotnet run --project tests/PoImport.Checks -- <absolute samples directory>");
var folder = Path.GetFullPath(args[0]);
var parser = new PoFileParser();
var start = new DateOnly(2026, 10, 1); var end = start.AddMonths(1); var checks = 0;
void Check(string name, bool condition)
{ if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
ParsedPoFile Read(string name, int maxRows = PoProfile.MaxRows)
{ using var file = File.OpenRead(Path.Combine(folder, name)); return parser.Parse(file, name, start, end, maxRows); }
var good = Read("PO_01_valid_v1.csv");
Check("Valid CSV: all rows typed and no issues", good.Rows.Count == 2 && good.Errors == 0 && good.Warnings == 0 && good.Rows.All(x => x.Line is not null));
Check("Leading zero identifier survives", good.Rows[0].Line!.ReceiptNumber == "000123");
Check("Quantity, price and amount are decimal values", good.Rows[0].Line!.ReceiptQuantity == 10m && good.Rows[0].Line!.PoUnitPrice == 12.5m && good.Rows[0].Line!.InvoiceAmount == 125m);
var corrected = Read("PO_02_corrected_v2.csv");
Check("Corrected source has independent values", corrected.Rows[0].Line!.LineDescription != good.Rows[0].Line!.LineDescription && good.Rows[0].Line!.ReceiptNumber == "000123");
foreach (var (file, code) in new[] {
    ("PO_03_invalid_date.csv","INVALID_DATE"), ("PO_04_missing_header.csv","HEADER_COUNT"),
    ("PO_05_empty.csv","EMPTY_DATA_BATCH"), ("PO_08_wrong_period.csv","WRONG_PERIOD"),
    ("PO_10_decimal_scale.csv","DECIMAL_PRECISION"), ("PO_11_blank_quantity.csv","REQUIRED"),
    ("PO_15_formula.xlsx","FORMULA_NOT_ALLOWED") })
{
    var result = Read(file); Check(file + " produces " + code, result.Errors > 0 && result.Issues.Any(x => x.Code == code));
}
var missing = Read("PO_06_warning_missing_invoice.csv");
Check("Blank invoice is null, never literal N/A/zero", missing.Errors == 0 && missing.Warnings > 0 && missing.Rows[0].Line!.InvoiceNumber is null);
var negative = Read("PO_07_warning_negative.csv");
Check("Negative amounts survive with warning", negative.Errors == 0 && negative.Issues.Any(x => x.Code == "NEGATIVE_VALUE") && negative.Rows[0].Line!.InvoiceAmount == -125m);
var repeated = Read("PO_09_repeated_row.csv");
Check("Repeated row warns but is not silently removed", repeated.Rows.Count == 2 && repeated.Errors == 0 && repeated.Issues.Any(x => x.Code == "REPEATED_ROW"));
var reordered = Read("PO_12_reordered_headers.csv");
Check("Columns are mapped by name, not ordinal", reordered.Errors == 0 && reordered.Rows[0].Line!.ReceiptNumber == "000123" && reordered.Rows[0].Line!.PoNumber == "PO-0001");
var multiline = Read("PO_13_quoted_multiline.csv");
Check("CSV quoting preserves comma and newline", multiline.Errors == 0 && multiline.Rows.Count == 1 && multiline.Rows[0].Line!.LineDescription!.Contains('\n'));
var excel = Read("PO_14_valid.xlsx");
Check("Excel types normalize to CSV values", excel.Errors == 0 && excel.Rows[0].Line!.TransactionDate == good.Rows[0].Line!.TransactionDate && excel.Rows[0].Line!.PoUnitPrice == 12.5m);
var numeric = Read("PO_16_numeric_identifier.xlsx");
Check("Numeric Excel identifiers need acknowledgement", numeric.Errors == 0 && numeric.Issues.Any(x => x.Code == "NUMERIC_IDENTIFIER"));
using (var disguised = File.OpenRead(Path.Combine(folder, "PO_14_valid.xlsx")))
    Check("Renamed XLSX cannot bypass ZIP/formula checks as XLS", parser.Parse(disguised, "renamed.xls", start, end).Issues.Any(x => x.Code == "FILE_EXTENSION_MISMATCH"));
Check("Row limit is an ERROR, not a usable partial snapshot", Read("PO_01_valid_v1.csv", 1).Issues.Any(x => x.Code == "ROW_LIMIT" && x.Severity == "ERROR"));
var invalid = Read("PO_03_invalid_date.csv");
Check("Cell error traces back to input row and column", invalid.Issues.Any(x => x.RecordNumber == 2 && x.Column == "Trx date" && x.SheetName == "CSV"));
using (var data = new MemoryStream(Encoding.UTF8.GetBytes(string.Join(',', PoProfile.Headers) + "\nPO,0001,2026-10-02,PO-1,0,0,1,USD,,,,,,INV,0,0,Zero is valid\n")))
{
    var zeros = parser.Parse(data, "zeros.csv", start, end);
    Check("Zero is different from missing", zeros.Errors == 0 && zeros.Rows[0].Line!.ReceiptQuantity == 0m);
}
Console.WriteLine($"{checks} parser checks passed. SQL transactions, Worker leases, API permissions and AD still need Windows/SQL integration checks.");
