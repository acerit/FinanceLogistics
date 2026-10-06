namespace FinanceLogistics.Domain.Imports;

public sealed class PoUpdateLine
{
    public long SourceLineId { get; set; }
    public long BatchId { get; set; }
    public long RawRowId { get; set; }
    public string SourceCode { get; set; } = "PO_UPDATED";
    public string? SourceCodeText { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public string? PoNumber { get; set; }
    public decimal? ReceiptQuantity { get; set; }
    public decimal? PoUnitPrice { get; set; }
    public string? PoLineReference { get; set; }
    public string? PoCurrency { get; set; }
    public DateOnly? PoRateDate { get; set; }
    public decimal? PoRate { get; set; }
    public decimal? ReceiptRate { get; set; }
    public DateOnly? ReceiptRateDate { get; set; }
    public decimal? PurchaseFunctionalAmount { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal? InvoiceFunctionalAmount { get; set; }
    public decimal? InvoiceAmount { get; set; }
    public string? LineDescription { get; set; }
}
