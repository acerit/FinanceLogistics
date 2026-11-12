using FinanceLogistics.Application.Imports;
namespace FinanceLogistics.Infrastructure.LogisticsNk;
public enum NkFieldKind { Text, Identifier, Currency, Number, Date }
public sealed record NkField(string Property,string Header,int Column,NkFieldKind Kind,int MaxLength,bool Required);
public sealed record NkSourceProfile(string SourceCode,string Code,string SiteCode,string Sheet,string[][] HeaderRows,NkField[] Fields)
{
    public int SchemaVersion { get; init; } = 1;
    public bool UsesCachedValues => SchemaVersion >= 2;
    public string FormulaPolicy => UsesCachedValues ? "CACHED_VALUE_ONLY" : "LITERAL_ONLY";
    public string ScopeCode => "SITE_"+SiteCode;
    public int DataStart => HeaderRows.Length+1;
    public int Width => HeaderRows[0].Length;
    public string CoverageProperty => "TransactionDate";
    public string SqlTable => "src.[LogisticsNkLine]";
    public string[] Headers => Fields.Select(x=>x.Header).ToArray();
}
public static class NkSourceProfiles
{
    public const int MaxRows=10000;
    public const long MaxBytes=10*1024*1024;
    public static NkSourceProfile Get(string source) => All.SingleOrDefault(p=>p.SourceCode==source)
        ?? throw new PoImportFault(404,"NK_SOURCE_NOT_FOUND","Nguồn nhập khẩu này chưa được hỗ trợ.");
    public static NkSourceProfile Get(string source, string profileCode) => All.Concat(V2).Concat(RealInputsV3).Concat(HeaderOneV4).Concat(HeaderOneV5)
        .SingleOrDefault(p=>p.SourceCode==source && p.Code==profileCode)
        ?? throw new PoImportFault(409,"NK_PROFILE_UNSUPPORTED","Profile không thuộc nguồn nhập khẩu đã chọn.");
    public static readonly NkSourceProfile[] All = [
        new("NK_1000","NK_1000_WORKBOOK_V1","1000","1004 purchase 2026 - AGBS1(2026",[
            ["Souce","Receipt Num","Trx","PO Num","Receipt QTY"," PO Unit Price ","PO Line","PO Curr","PO Rate Date","PO Rate","Receipt Rate","Receipt Rate Date"," Purchase Functional Amount ","Invoice No."," Invoice Functional Amount "," Invoice Amount ","Line Description"," Part No ","Original amount","Booking rate","VND convert","Invoice date","Vendor Name","PL","Payment No","OFFSET","Payment date","AWB/BL No","Declaration No","Final declaration No","Declaration  date","Declaration type","Contract No","Contract Date"," Freight ","Imported tax"," Taxable amount "," Declaration rate ","VAT rate"," VAT amount ","Tax payment No","Tax payment Date","RMN","Energy Efficiency Certificate (EEC)","Energy Efficiency Certificate Date (EEC date)","EEC Invoice No","EEC invoice Date","EEC Payment date","Certificate of Conformity to Technical Regulations No (COC)","COC date","Expired date","COC Invoice No","COC invoice date","COC Payment date","Declaration of Conformity to Technical Regulations","Declaration of COC date"],
            ["","","Date","","","","","","","","","","","","","",""," Part No ","","","","Invoice date","Vendor Name","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","",""],
        ],[
            new("SourceCodeText","Souce",1,NkFieldKind.Text,100,false),
            new("ReceiptNumber","Receipt Num",2,NkFieldKind.Identifier,500,false),
            new("TransactionDate","Trx Date",3,NkFieldKind.Date,0,true),
            new("PoNumber","PO Num",4,NkFieldKind.Identifier,500,false),
            new("ReceiptQuantity","Receipt QTY",5,NkFieldKind.Number,0,false),
            new("PoUnitPrice"," PO Unit Price ",6,NkFieldKind.Number,0,false),
            new("PoLineReference","PO Line",7,NkFieldKind.Identifier,500,false),
            new("PoCurrency","PO Curr",8,NkFieldKind.Currency,10,false),
            new("PoRateDate","PO Rate Date",9,NkFieldKind.Date,0,false),
            new("PoRate","PO Rate",10,NkFieldKind.Number,0,false),
            new("ReceiptRate","Receipt Rate",11,NkFieldKind.Number,0,false),
            new("ReceiptRateDate","Receipt Rate Date",12,NkFieldKind.Date,0,false),
            new("PurchaseFunctionalAmount"," Purchase Functional Amount ",13,NkFieldKind.Number,0,false),
            new("InvoiceNumber","Invoice No.",14,NkFieldKind.Identifier,500,false),
            new("InvoiceFunctionalAmount"," Invoice Functional Amount ",15,NkFieldKind.Number,0,false),
            new("InvoiceAmount"," Invoice Amount ",16,NkFieldKind.Number,0,false),
            new("LineDescription","Line Description",17,NkFieldKind.Text,4000,false),
            new("PartNumber"," Part No ",18,NkFieldKind.Identifier,500,false),
            new("OriginalAmount","Original amount",19,NkFieldKind.Number,0,false),
            new("BookingRate","Booking rate",20,NkFieldKind.Number,0,false),
            new("VndConvert","VND convert",21,NkFieldKind.Number,0,false),
            new("InvoiceDate","Invoice date",22,NkFieldKind.Date,0,false),
            new("VendorName","Vendor Name",23,NkFieldKind.Text,500,false),
            new("ProductLine","PL",24,NkFieldKind.Identifier,500,false),
            new("PaymentNumber","Payment No",25,NkFieldKind.Identifier,500,false),
            new("OffsetReference","OFFSET",26,NkFieldKind.Identifier,500,false),
            new("PaymentDate","Payment date",27,NkFieldKind.Date,0,false),
            new("AwbBlNumber","AWB/BL No",28,NkFieldKind.Identifier,500,false),
            new("DeclarationNumber","Declaration No",29,NkFieldKind.Identifier,500,false),
            new("FinalDeclarationNumber","Final declaration No",30,NkFieldKind.Identifier,500,false),
            new("DeclarationDate","Declaration  date",31,NkFieldKind.Date,0,false),
            new("DeclarationType","Declaration type",32,NkFieldKind.Identifier,500,false),
            new("ContractNumber","Contract No",33,NkFieldKind.Identifier,500,false),
            new("ContractDate","Contract Date",34,NkFieldKind.Date,0,false),
            new("Freight"," Freight ",35,NkFieldKind.Number,0,false),
            new("ImportTax","Imported tax",36,NkFieldKind.Number,0,false),
            new("TaxableAmount"," Taxable amount ",37,NkFieldKind.Number,0,false),
            new("DeclarationRate"," Declaration rate ",38,NkFieldKind.Number,0,false),
            new("VatRate","VAT rate",39,NkFieldKind.Number,0,false),
            new("VatAmount"," VAT amount ",40,NkFieldKind.Number,0,false),
            new("TaxPaymentNumber","Tax payment No",41,NkFieldKind.Identifier,500,false),
            new("TaxPaymentDate","Tax payment Date",42,NkFieldKind.Date,0,false),
            new("Rmn","RMN",43,NkFieldKind.Identifier,500,false),
            new("EecNumber","Energy Efficiency Certificate (EEC)",44,NkFieldKind.Identifier,500,false),
            new("EecDate","Energy Efficiency Certificate Date (EEC date)",45,NkFieldKind.Date,0,false),
            new("EecInvoiceNumber","EEC Invoice No",46,NkFieldKind.Identifier,500,false),
            new("EecInvoiceDate","EEC invoice Date",47,NkFieldKind.Date,0,false),
            new("EecPaymentDate","EEC Payment date",48,NkFieldKind.Date,0,false),
            new("CocNumber","Certificate of Conformity to Technical Regulations No (COC)",49,NkFieldKind.Identifier,500,false),
            new("CocDate","COC date",50,NkFieldKind.Date,0,false),
            new("CocExpiryDate","Expired date",51,NkFieldKind.Date,0,false),
            new("CocInvoiceNumber","COC Invoice No",52,NkFieldKind.Identifier,500,false),
            new("CocInvoiceDate","COC invoice date",53,NkFieldKind.Date,0,false),
            new("CocPaymentDate","COC Payment date",54,NkFieldKind.Date,0,false),
            new("ConformityDeclarationNumber","Declaration of Conformity to Technical Regulations",55,NkFieldKind.Identifier,500,false),
            new("ConformityDeclarationDate","Declaration of COC date",56,NkFieldKind.Date,0,false),
        ]),
        new("NK_6000","NK_6000_WORKBOOK_V1","6000","báo cáo NHẬP khẩu 2026",[
            ["Souce","Receipt Num","Trx Date","PO Num"," Receipt QTY "," PO Unit Price ","PO Line","PO Curr","PO Rate Date","PO Rate","Receipt Rate","Receipt Rate Date"," Purchase Functional Amount ","Invoice No."," Invoice Functional Amount "," Invoice Amount ","Line Description"," Part No\nMã hàng "," Original amount\nTrị giá mua nguyên tệ ","Booking rate\nTỷ giá ghi sổ","VND convert\nChuyển đổi VND","Invoice date\nNgày hóa đơn","Vendor Name\nTên người bán","PL\nMã ngành hàng","Payment No\nChứng từ thanh toán hàng hóa ","OFFSET\nChứng từ cấn trừ","Payment date\nNgày thanh toán","AWB/BL No\nSố chứng từ vận chuyển","Declaration No\nSố tờ khai HQ","Final declaration No\nSố tờ khai HQ đã thông quan","Declaration  date\nNgày tờ khai HQ","Declaration type\nLoại TK","Contract No\nSố hợp đồng","Contract Date\nNgày hợp đồng"," Freight\nPhí vận chuyển "," Imported tax\nThuế nhập khẩu "," Taxable amount\nTrị giá tính thuế ","Declaration rate\nTỷ giá TK","VAT rate\nThuế suất GTGT"," VAT amount\nSố tiền thuế GTGT ","Tax payment No\nSố chứng từ thanh toán tiền thuế","Tax payment Date\nNgày thanh toán tiền thuế","RMN\nMã RMN","Energy Efficiency Certificate (EEC)\nChứng nhận HSNL","Energy Efficiency Certificate Date (EEC date)\nNgày cấp HSNL","EEC Invoice No\nHóa đơn HSNL","EEC invoice Date\nNgày hóa đơn HSNL","EEC Payment date\nNgày thanh toán hóa đơn HSNL","Certificate of Conformity to Technical Regulations No (COC)\nChứng nhận HCHQ","COC date\nNgày chứng nhận HCHQ","Expired date\nNgày hết hạn chứng nhận HCHQ","COC Invoice No\nHóa đơn HCHQ","COC invoice date\nNgày hóa đơn HCHQ","COC Payment date\nNgày thanh toán hóa đơn HCHQ","Declaration of Conformity to Technical Regulations\nSố công bố HCHQ","Declaration of COC date\nNgày công bố HCHQ"],
        ],[
            new("SourceCodeText","Souce",1,NkFieldKind.Text,100,false),
            new("ReceiptNumber","Receipt Num",2,NkFieldKind.Identifier,500,false),
            new("TransactionDate","Trx Date",3,NkFieldKind.Date,0,true),
            new("PoNumber","PO Num",4,NkFieldKind.Identifier,500,false),
            new("ReceiptQuantity"," Receipt QTY ",5,NkFieldKind.Number,0,false),
            new("PoUnitPrice"," PO Unit Price ",6,NkFieldKind.Number,0,false),
            new("PoLineReference","PO Line",7,NkFieldKind.Identifier,500,false),
            new("PoCurrency","PO Curr",8,NkFieldKind.Currency,10,false),
            new("PoRateDate","PO Rate Date",9,NkFieldKind.Date,0,false),
            new("PoRate","PO Rate",10,NkFieldKind.Number,0,false),
            new("ReceiptRate","Receipt Rate",11,NkFieldKind.Number,0,false),
            new("ReceiptRateDate","Receipt Rate Date",12,NkFieldKind.Date,0,false),
            new("PurchaseFunctionalAmount"," Purchase Functional Amount ",13,NkFieldKind.Number,0,false),
            new("InvoiceNumber","Invoice No.",14,NkFieldKind.Identifier,500,false),
            new("InvoiceFunctionalAmount"," Invoice Functional Amount ",15,NkFieldKind.Number,0,false),
            new("InvoiceAmount"," Invoice Amount ",16,NkFieldKind.Number,0,false),
            new("LineDescription","Line Description",17,NkFieldKind.Text,4000,false),
            new("PartNumber"," Part No\nMã hàng ",18,NkFieldKind.Identifier,500,false),
            new("OriginalAmount"," Original amount\nTrị giá mua nguyên tệ ",19,NkFieldKind.Number,0,false),
            new("BookingRate","Booking rate\nTỷ giá ghi sổ",20,NkFieldKind.Number,0,false),
            new("VndConvert","VND convert\nChuyển đổi VND",21,NkFieldKind.Number,0,false),
            new("InvoiceDate","Invoice date\nNgày hóa đơn",22,NkFieldKind.Date,0,false),
            new("VendorName","Vendor Name\nTên người bán",23,NkFieldKind.Text,500,false),
            new("ProductLine","PL\nMã ngành hàng",24,NkFieldKind.Identifier,500,false),
            new("PaymentNumber","Payment No\nChứng từ thanh toán hàng hóa ",25,NkFieldKind.Identifier,500,false),
            new("OffsetReference","OFFSET\nChứng từ cấn trừ",26,NkFieldKind.Identifier,500,false),
            new("PaymentDate","Payment date\nNgày thanh toán",27,NkFieldKind.Date,0,false),
            new("AwbBlNumber","AWB/BL No\nSố chứng từ vận chuyển",28,NkFieldKind.Identifier,500,false),
            new("DeclarationNumber","Declaration No\nSố tờ khai HQ",29,NkFieldKind.Identifier,500,false),
            new("FinalDeclarationNumber","Final declaration No\nSố tờ khai HQ đã thông quan",30,NkFieldKind.Identifier,500,false),
            new("DeclarationDate","Declaration  date\nNgày tờ khai HQ",31,NkFieldKind.Date,0,false),
            new("DeclarationType","Declaration type\nLoại TK",32,NkFieldKind.Identifier,500,false),
            new("ContractNumber","Contract No\nSố hợp đồng",33,NkFieldKind.Identifier,500,false),
            new("ContractDate","Contract Date\nNgày hợp đồng",34,NkFieldKind.Date,0,false),
            new("Freight"," Freight\nPhí vận chuyển ",35,NkFieldKind.Number,0,false),
            new("ImportTax"," Imported tax\nThuế nhập khẩu ",36,NkFieldKind.Number,0,false),
            new("TaxableAmount"," Taxable amount\nTrị giá tính thuế ",37,NkFieldKind.Number,0,false),
            new("DeclarationRate","Declaration rate\nTỷ giá TK",38,NkFieldKind.Number,0,false),
            new("VatRate","VAT rate\nThuế suất GTGT",39,NkFieldKind.Number,0,false),
            new("VatAmount"," VAT amount\nSố tiền thuế GTGT ",40,NkFieldKind.Number,0,false),
            new("TaxPaymentNumber","Tax payment No\nSố chứng từ thanh toán tiền thuế",41,NkFieldKind.Identifier,500,false),
            new("TaxPaymentDate","Tax payment Date\nNgày thanh toán tiền thuế",42,NkFieldKind.Date,0,false),
            new("Rmn","RMN\nMã RMN",43,NkFieldKind.Identifier,500,false),
            new("EecNumber","Energy Efficiency Certificate (EEC)\nChứng nhận HSNL",44,NkFieldKind.Identifier,500,false),
            new("EecDate","Energy Efficiency Certificate Date (EEC date)\nNgày cấp HSNL",45,NkFieldKind.Date,0,false),
            new("EecInvoiceNumber","EEC Invoice No\nHóa đơn HSNL",46,NkFieldKind.Identifier,500,false),
            new("EecInvoiceDate","EEC invoice Date\nNgày hóa đơn HSNL",47,NkFieldKind.Date,0,false),
            new("EecPaymentDate","EEC Payment date\nNgày thanh toán hóa đơn HSNL",48,NkFieldKind.Date,0,false),
            new("CocNumber","Certificate of Conformity to Technical Regulations No (COC)\nChứng nhận HCHQ",49,NkFieldKind.Identifier,500,false),
            new("CocDate","COC date\nNgày chứng nhận HCHQ",50,NkFieldKind.Date,0,false),
            new("CocExpiryDate","Expired date\nNgày hết hạn chứng nhận HCHQ",51,NkFieldKind.Date,0,false),
            new("CocInvoiceNumber","COC Invoice No\nHóa đơn HCHQ",52,NkFieldKind.Identifier,500,false),
            new("CocInvoiceDate","COC invoice date\nNgày hóa đơn HCHQ",53,NkFieldKind.Date,0,false),
            new("CocPaymentDate","COC Payment date\nNgày thanh toán hóa đơn HCHQ",54,NkFieldKind.Date,0,false),
            new("ConformityDeclarationNumber","Declaration of Conformity to Technical Regulations\nSố công bố HCHQ",55,NkFieldKind.Identifier,500,false),
            new("ConformityDeclarationDate","Declaration of COC date\nNgày công bố HCHQ",56,NkFieldKind.Date,0,false),
        ]),
    ];
    public static readonly NkSourceProfile[] V2 = [
        All[0] with { Code="NK_1000_WORKBOOK_V2", Sheet="2026", SchemaVersion=2 },
        All[1] with { Code="NK_6000_WORKBOOK_V2", Sheet="2026 - NK", SchemaVersion=2 }
    ];


    private static readonly string[] Headers6000 = ["Souce","Receipt Num","Trx Date","PO Num","Receipt QTY","PO Unit Price","PO Line","PO Curr","PO Rate Date","PO Rate","Receipt Rate","Receipt Rate Date","Purchase Functional Amount","Invoice No.","Invoice Functional Amount","Invoice Amount","Line Description","AWB/BL No\nSố chứng từ vận chuyển","Declaration No\nSố tờ khai HQ","Final declaration No\nSố tờ khai HQ đã thông quan","Declaration  date\nNgày tờ khai HQ","Declaration type\nLoại TK","Contract No\nSố hợp đồng","Contract Date\nNgày hợp đồng","Freight\nPhí vận chuyển","Imported tax\nThuế nhập khẩu","Taxable amount\nTrị giá tính thuế","Declaration rate\nTỷ giá TK","VAT rate\nThuế suất GTGT","VAT amount\nSố tiền thuế GTGT"];

    // Real input layouts verified against workbook specimens 05-Nov-2026.
    // Versioned independently from V1/V2 so historical import batches are untouched.
    public static readonly NkSourceProfile[] RealInputsV3 = [
        All[0] with { Code="NK_1000_REAL_INPUT_V3", Sheet="Import_2026_1000", SchemaVersion=3,
            HeaderRows=[
                new string[] { "Souce","Receipt Num","Trx Date","PO Num","Receipt QTY","PO Unit Price","PO Line","PO Curr","PO Rate Date","PO Rate","Receipt Rate","Receipt Rate Date","Purchase Functional Amount","Invoice No.","Invoice Functional Amount","Invoice Amount","Line Description","Part No","Original amount","Booking rate","VND convert","Invoice date","Vendor Name","PL","Payment No","OFFSET","Payment date","AWB/BL No","Declaration No","Final declaration No","Declaration  date","Declaration type","Contract No","Contract Date","Freight","Imported tax","Taxable amount","Declaration rate","VAT rate","VAT amount","Tax payment No","Tax payment Date","RMN","Energy Efficiency Certificate (EEC)","Energy Efficiency Certificate Date (EEC date)","EEC Invoice No","EEC invoice Date","EEC Payment date","Certificate of Conformity to Technical Regulations No (COC)","COC date","Expired date","COC Invoice No","COC invoice date","COC Payment date","Declaration of Conformity to Technical Regulations","Declaration of COC date" },
                new string[] { "","","","","","","","","","","","","","","","","","Part No","","","","Invoice date","Vendor Name","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","","" }
            ] },
        All[1] with { Code="NK_6000_REAL_INPUT_V3", Sheet="2026 - NK", SchemaVersion=3,
            HeaderRows=[new string[] { "Souce","Receipt Num","Trx Date","PO Num","Receipt QTY","PO Unit Price","PO Line","PO Curr","PO Rate Date","PO Rate","Receipt Rate","Receipt Rate Date","Purchase Functional Amount","Invoice No.","Invoice Functional Amount","Invoice Amount","Line Description","AWB/BL No\nSố chứng từ vận chuyển","Declaration No\nSố tờ khai HQ","Final declaration No\nSố tờ khai HQ đã thông quan","Declaration  date\nNgày tờ khai HQ","Declaration type\nLoại TK","Contract No\nSố hợp đồng","Contract Date\nNgày hợp đồng","Freight\nPhí vận chuyển","Imported tax\nThuế nhập khẩu","Taxable amount\nTrị giá tính thuế","Declaration rate\nTỷ giá TK","VAT rate\nThuế suất GTGT","VAT amount\nSố tiền thuế GTGT" }],
            Fields=All[1].Fields.Where(f=>f.Column<=17 || f.Column is >=28 and <=40)
                .Select(f=> {
                    var col=f.Column<=17 ? f.Column : f.Column-10;
                    return f with {Column=col, Header=Headers6000[col-1]};
                }).ToArray() }
    ];
    // All 9 source contracts: header starts at A1 and data at row 2.
    // Do not modify the frozen, previously approved V1/V2/V3 descriptions.
    public static readonly NkSourceProfile[] HeaderOneV4 = RealInputsV3
        .Select(p=>p with {
            Code=p.SourceCode+"_HEADER1_V4", SchemaVersion=4,
            HeaderRows=[p.HeaderRows[0].ToArray()],
            Fields=p.Fields.OrderBy(f=>f.Column)
                .Select((f,i)=>f with { Column=i+1,Header=p.HeaderRows[0][i] })
                .ToArray()
        }).ToArray();

    // NK_1000 input variant approved for 44 source fields (10 legacy middle
    // columns and 2 tax-payment columns removed). No re-use of the old V4
    // name: existing batch profiles and their frozen headers remain unchanged.
    // Site 6000 deliberately continues to use its separate 30-column V4.
    public static readonly NkSourceProfile[] HeaderOneV5 = [BuildNk1000Header44V5()];

    private static NkSourceProfile BuildNk1000Header44V5()
    {
        var prior = HeaderOneV4.Single(p => p.SourceCode == "NK_1000");
        var fields = prior.Fields
            .Where(f => f.Column <= 17 || f.Column is >= 28 and <= 40 || f.Column is >= 43 and <= 56)
            .Select((f, i) => f with { Column = i + 1 })
            .ToArray();
        if (fields.Length != 44 || fields[17].Property != "AwbBlNumber" || fields[30].Property != "Rmn")
            throw new InvalidOperationException("NK_1000 V5 input contract must contain 44 mapped columns.");
        return prior with
        {
            Code = "NK_1000_HEADER1_44_V5",
            SchemaVersion = 5,
            HeaderRows = [fields.Select(f => f.Header).ToArray()],
            Fields = fields
        };
    }

}
