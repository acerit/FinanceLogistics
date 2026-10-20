using FinanceLogistics.Application.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;
public enum FinanceFieldKind { Text, Identifier, Currency, Number, Date }
public sealed record FinanceField(string Property, string Header, int Column, FinanceFieldKind Kind, int MaxLength, bool Required, int HeaderColumn);
public sealed record FinanceSourceProfile(string SourceCode, string Code, string Sheet, int HeaderStart, int HeaderEnd, int DataStart, int Width, string Table, string CoverageProperty, FinanceField[] Fields)
{
    public string[] Headers => Fields.Select(x => x.Header).ToArray();
    public string SqlTable => "src.[" + Table + "]";
}
public static class FinanceSourceProfiles
{
    public const int MaxRows = 10000;
    public const long MaxBytes = 10 * 1024 * 1024;
    public static FinanceSourceProfile Get(string source) => All.SingleOrDefault(x => x.SourceCode == source)
        ?? throw new PoImportFault(404, "FINANCE_SOURCE_NOT_FOUND", "Nguồn Finance không được hỗ trợ ở màn hình này.");
    public static readonly FinanceSourceProfile[] All = [
        new("AP_REPORT", "AP_ORACLE_XXAPR017_V1", "AP report", 1, 3, 4, 30, "ApReportLine", "GlDate", [
            new("OperatingUnit", "Operating Unit", 1, FinanceFieldKind.Text, 200, true, 1),
            new("TradingPartner", "Trading Partner", 2, FinanceFieldKind.Text, 300, false, 2),
            new("SupplierNumber", "Supplier Num", 3, FinanceFieldKind.Identifier, 120, true, 3),
            new("SupplierSiteName", "Supplier Site Name", 4, FinanceFieldKind.Identifier, 200, false, 4),
            new("PayeeName", "Payee Name", 5, FinanceFieldKind.Text, 300, false, 5),
            new("InvoiceType", "Invoice Type", 6, FinanceFieldKind.Text, 80, false, 6),
            new("InvoiceNumber", "Invoice Num", 7, FinanceFieldKind.Identifier, 200, true, 7),
            new("InvoiceGrossAmount", "Invoice Amount (Gross)", 8, FinanceFieldKind.Number, 0, true, 8),
            new("FunctionalAmount", "Functional Amount", 9, FinanceFieldKind.Number, 0, false, 9),
            new("HeaderTaxAmount", "Tax Amount (Header)", 10, FinanceFieldKind.Number, 0, false, 10),
            new("WithheldAmount", "Withheld Amount", 11, FinanceFieldKind.Number, 0, false, 11),
            new("PrepaidAmount", "Prepaid Amount", 12, FinanceFieldKind.Number, 0, false, 12),
            new("ExchangeRate", "Exchange Rate", 13, FinanceFieldKind.Number, 0, false, 13),
            new("LiabilityAccount", "Liability Account", 14, FinanceFieldKind.Identifier, 250, false, 14),
            new("InvoiceDescription", "Invoice Description", 15, FinanceFieldKind.Text, 2000, false, 15),
            new("PayGroup", "Pay Group", 16, FinanceFieldKind.Text, 120, false, 16),
            new("BatchName", "Batch Name", 17, FinanceFieldKind.Identifier, 200, false, 17),
            new("DocumentSequence", "Document Sequence", 18, FinanceFieldKind.Identifier, 120, false, 18),
            new("GlDate", "GL Date", 19, FinanceFieldKind.Date, 0, true, 19),
            new("InvoiceDate", "Invoice Date", 20, FinanceFieldKind.Date, 0, false, 20),
            new("TermsDate", "Terms Date", 21, FinanceFieldKind.Date, 0, false, 21),
            new("PaymentTerms", "Terms", 22, FinanceFieldKind.Text, 250, false, 22),
            new("DueDate", "Due Date", 23, FinanceFieldKind.Date, 0, false, 23),
            new("InvoiceCurrency", "Invoice Curr", 24, FinanceFieldKind.Currency, 10, true, 24),
            new("PaymentCurrency", "Payment Curr", 25, FinanceFieldKind.Currency, 10, false, 25),
            new("RemainingAmount", "Invoice Remaining Amount", 26, FinanceFieldKind.Number, 0, false, 26),
            new("InvoiceHold", "Invoice hold", 27, FinanceFieldKind.Text, 120, false, 27),
            new("HoldName", "Hold Name", 28, FinanceFieldKind.Text, 200, false, 28),
            new("HoldReason", "Hold Reason", 29, FinanceFieldKind.Text, 2000, false, 29),
            new("InvoiceStatus", "Invoice Status", 30, FinanceFieldKind.Text, 120, false, 30),
        ]),
        new("SUPPLIER_PAYMENT_REGISTER", "PAYMENT_ORACLE_XXAPR015_V1", "in", 15, 17, 19, 26, "SupplierPaymentLine", "PaymentDate", [
            new("AccountName", "Account Name", 1, FinanceFieldKind.Text, 200, false, 1),
            new("SupplierNumber", "Supplier Number", 2, FinanceFieldKind.Identifier, 120, true, 2),
            new("SupplierName", "Supplier Name", 4, FinanceFieldKind.Text, 300, false, 4),
            new("SupplierSite", "Supplier Site", 5, FinanceFieldKind.Identifier, 200, false, 5),
            new("AlternateSiteName", "Alternate Site Name", 8, FinanceFieldKind.Text, 300, false, 8),
            new("PaymentDate", "Payment Date", 9, FinanceFieldKind.Date, 0, true, 9),
            new("PaymentCurrency", "Payment Curr.", 10, FinanceFieldKind.Currency, 10, true, 10),
            new("PaymentMethod", "Payment Method", 11, FinanceFieldKind.Text, 120, false, 11),
            new("InvoiceNumber", "Invoice Number", 12, FinanceFieldKind.Identifier, 200, true, 12),
            new("VoucherNumber", "Voucher Number", 13, FinanceFieldKind.Identifier, 120, false, 13),
            new("InvoiceDate", "Invoice Date", 14, FinanceFieldKind.Date, 0, false, 14),
            new("GlDate", "GL Date", 15, FinanceFieldKind.Date, 0, false, 15),
            new("InvoiceAmount", "Invoice Amount", 17, FinanceFieldKind.Number, 0, false, 17),
            new("PaymentAmount", "Payment Amount", 18, FinanceFieldKind.Number, 0, true, 18),
            new("FunctionalAmount", "Functional Amount", 19, FinanceFieldKind.Number, 0, false, 19),
            new("DiscountAmount", "Discount Amount", 20, FinanceFieldKind.Number, 0, false, 20),
            new("PaymentDocumentNumber", "Payment doc.Num", 21, FinanceFieldKind.Identifier, 120, true, 21),
            new("PaymentVoucherNumber", "Payment VoucherNumber", 22, FinanceFieldKind.Identifier, 120, false, 22),
            new("Ppr", "PPR", 23, FinanceFieldKind.Text, 300, false, 23),
            new("VoidStatus", "Void Status", 24, FinanceFieldKind.Text, 40, false, 24),
            new("PayeeName", "Payee Name", 25, FinanceFieldKind.Text, 300, false, 26),
        ]),
        new("EX_USD", "EX_USD_WORKBOOK_V1", "2026 USD", 2, 2, 3, 10, "ExchangeUsdLine", "BankDate", [
            new("InvoiceNumber", "Invoice No.", 1, FinanceFieldKind.Identifier, 200, true, 1),
            new("InvoiceDate", "Invoice Date", 2, FinanceFieldKind.Date, 0, false, 2),
            new("InvoiceAmount", "Invoice Amount", 3, FinanceFieldKind.Number, 0, true, 3),
            new("PoNumber", "PO Number", 4, FinanceFieldKind.Identifier, 300, false, 4),
            new("CustomsDeclaration", "Customs Declaration", 5, FinanceFieldKind.Identifier, 300, false, 5),
            new("CustomsDeclarationDate", "Date Customs Declaration", 6, FinanceFieldKind.Date, 0, false, 6),
            new("BankReference", "Bank", 7, FinanceFieldKind.Identifier, 200, false, 7),
            new("BankDate", "Date", 8, FinanceFieldKind.Date, 0, true, 8),
            new("SupplierName", "Supplier", 9, FinanceFieldKind.Text, 300, false, 9),
            new("AfsPaymentReference", "Chứng từ thanh toán AFS", 10, FinanceFieldKind.Identifier, 200, false, 10),
        ]),
        new("EX_VND", "EX_VND_WORKBOOK_V1", "2026 VND", 1, 1, 2, 7, "ExchangeVndLine", "BankDate", [
            new("BankDocument", "Chứng từ ngân hàng", 1, FinanceFieldKind.Identifier, 300, false, 1),
            new("BankDate", "Ngày ngân hàng", 2, FinanceFieldKind.Date, 0, true, 2),
            new("Description", "Nội dung", 3, FinanceFieldKind.Text, 4000, false, 3),
            new("PaymentAmount", "Số tiền thanh toán", 4, FinanceFieldKind.Number, 0, true, 4),
            new("InvoiceReference", "Hóa đơn", 5, FinanceFieldKind.Identifier, 500, false, 5),
            new("AfsPaymentReference", "Chứng từ thanh toán AFS", 6, FinanceFieldKind.Identifier, 200, false, 6),
            new("BankPaymentReference", "Chứng từ thanh toán ngân hàng", 7, FinanceFieldKind.Identifier, 300, false, 7),
        ]),
    ];
}
