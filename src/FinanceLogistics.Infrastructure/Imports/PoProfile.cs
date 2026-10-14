using System.Globalization;
using System.Text.Json;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

// This explicit DEV profile is deliberately different from an approved production mapping.
// Add a NEW profile/version when the Finance source layout changes; do not silently alter this one.
public static class PoProfile
{
    public const string Code = "PO_FLAT_ISO_V1_DEV";
    public const string FinanceCode = "PO_FINANCE_XLSX_V1";
    public const string SelectedMonth = "SELECTED_MONTH";
    public const string YearToPeriod = "YEAR_TO_PERIOD";
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxRows = 10_000;
    public static readonly string[] Headers = ["Souce", "Receipt Num", "Trx date", "PO Num",
        "Receipt QTY", "PO Unit Price", "PO Line", "PO Currency", "PO Rate Date", "PO Rate",
        "Receipt Rate", "Receipt Rate Date", "Purchase Functional Amount", "Invoice No.",
        "Invoice Functional Amount", "Invoice Amount", "Line Description"];
    public static readonly string[] Properties = ["SourceCodeText", "ReceiptNumber", "TransactionDate",
        "PoNumber", "ReceiptQuantity", "PoUnitPrice", "PoLineReference", "PoCurrency", "PoRateDate",
        "PoRate", "ReceiptRate", "ReceiptRateDate", "PurchaseFunctionalAmount", "InvoiceNumber",
        "InvoiceFunctionalAmount", "InvoiceAmount", "LineDescription"];
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Id(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static bool IsKnown(string? profile) => profile is Code or FinanceCode;
    public static string ProfileCode(string definition)
    {
        using var document = JsonDocument.Parse(definition);
        return document.RootElement.GetProperty("profile").GetString() ?? "";
    }
    public static bool Supports(string definition)
    {
        try { return IsKnown(ProfileCode(definition)); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return false; }
    }
    // A template describes layout; the requirement owns the reporting-period policy.
    // Existing DEV requirements keep their strict-month behavior. Finance must be configured explicitly.
    public static string PeriodMode(string profile, string policy)
    {
        if (profile == Code) return SelectedMonth;
        using var document = JsonDocument.Parse(policy);
        if (profile == FinanceCode && document.RootElement.TryGetProperty("financePoPeriodMode", out var value)
            && value.GetString() is string mode && (mode is SelectedMonth or YearToPeriod)) return mode;
        throw new PoImportFault(409, "PO_PERIOD_POLICY_REQUIRED",
            "Kỳ này chưa cấu hình cách nhận PO Finance theo tháng hay lũy kế. Chọn kỳ DEV đã cấu hình hoặc liên hệ quản trị.");
    }
    public static (long Bytes, int Rows) Limits(string policy)
    {
        using var document = JsonDocument.Parse(policy);
        var root = document.RootElement;
        if (!root.TryGetProperty("ingestionMode", out var mode) || mode.GetString() != "SNAPSHOT")
            throw new PoImportFault(409, "UNSUPPORTED_INGESTION_MODE", "Mốc này chỉ hỗ trợ SNAPSHOT.");
        var bytes = root.TryGetProperty("maxBytes", out var b) && b.TryGetInt64(out var bv) ? bv : MaxBytes;
        var rows = root.TryGetProperty("maxRows", out var r) && r.TryGetInt32(out var rv) ? rv : MaxRows;
        if (bytes <= 0 || rows <= 0) throw new PoImportFault(409, "INVALID_POLICY", "Giới hạn kỳ nộp không hợp lệ.");
        return (Math.Min(bytes, MaxBytes), Math.Min(rows, MaxRows));
    }
}
