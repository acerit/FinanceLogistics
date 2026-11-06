using FinanceLogistics.Application.Imports;
namespace FinanceLogistics.Infrastructure.LogisticsTrade;
public enum TradeFieldKind { Text, Identifier, Currency, Number, Date, Boolean, Verbatim }
public sealed record TradeField(string Property,string Header,int Column,TradeFieldKind Kind,int MaxLength,bool Required,bool OptionalHeader=false);
public sealed record TradeSourceProfile(string SourceCode,string Code,string SiteCode,string Sheet,string TableName,string CoverageProperty,bool IsVerbatim,TradeField[] Fields) {
 public int SchemaVersion {get; init;}=1;
 public int HeaderRow {get; init;}=1;
 public bool UsesCachedValues=>true;
 public bool RaPaymentOutputOnly=>SchemaVersion==2&&SourceCode.StartsWith("XK_",StringComparison.Ordinal);
 public string FormulaPolicy=>IsVerbatim?"PRESERVE_SOURCE":"CACHED_VALUE_ONLY";
 public string ScopeCode=>"SITE_"+SiteCode; public int DataStart=>HeaderRow+1; public int Width=>100;
 public string[][] HeaderRows=>[Fields.Select(x=>x.Header).ToArray()];
 public string SqlTable=>"src.["+TableName+"]"; public string[] Headers=>Fields.Select(x=>x.Header).ToArray();
}
public static class TradeSourceProfiles {
 public const int MaxRows=10000; public const long MaxBytes=10*1024*1024;
 public static TradeSourceProfile Get(string source)=>All.SingleOrDefault(p=>p.SourceCode==source)??throw new PoImportFault(404,"TRADE_SOURCE_NOT_SUPPORTED","Nguồn chưa có mẫu được triển khai.");
 public static TradeSourceProfile Get(string source,string code)=>All.Concat(XkRealV2).Concat(RealInputsV3).Concat(HeaderOneV4).SingleOrDefault(p=>p.SourceCode==source&&p.Code==code)??throw new PoImportFault(409,"TRADE_PROFILE_UNSUPPORTED","Profile không thuộc nguồn đã chọn.");
 private static readonly TradeSourceProfile[] Original=[
new("OTHER_IMPORT_6000","OTHER_IMPORT_6000_WORKBOOK_V1","6000","báo cáo OTHER NHẬP khẩu 2026","OtherTradeLine","DeclarationDate",false,[
 new("SourceCodeText","Souce",1,TradeFieldKind.Text,4000,false,false),
 new("TransactionType","TYPE OF TRANSACTION",2,TradeFieldKind.Text,4000,false,false),
 new("VendorName","Vendor Name",3,TradeFieldKind.Text,4000,false,false),
 new("PoNumber","PO Num",4,TradeFieldKind.Identifier,500,false,false),
 new("PoDate","PO date",5,TradeFieldKind.Date,500,false,false),
 new("InvoiceNumber","Invoice No.",6,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceDate","Invoice date",7,TradeFieldKind.Date,500,false,false),
 new("ReceiptNumber","Receipt Num",8,TradeFieldKind.Identifier,500,false,false),
 new("ReceiptDate","Receipt date",9,TradeFieldKind.Date,500,false,false),
 new("Quantity","Receipt QTY",10,TradeFieldKind.Number,500,false,false),
 new("DeclarationUnitPrice","Unit Price in declaration form",11,TradeFieldKind.Number,500,false,false),
 new("TotalAmount","Total amount",12,TradeFieldKind.Number,500,false,false),
 new("Currency","Currency",13,TradeFieldKind.Currency,500,false,false),
 new("DeclarationRate","Exchange rate ( declaration form)",14,TradeFieldKind.Number,500,false,false),
 new("VndAmount"," VND amount convert ",15,TradeFieldKind.Number,500,false,false),
 new("PartNumber"," Part No ",16,TradeFieldKind.Identifier,500,false,false),
 new("LineDescription","Line Description",17,TradeFieldKind.Text,4000,false,false),
 new("AwbNumber","AWB",18,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationNumber","Declaration No",19,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationDate","Declaration  date",20,TradeFieldKind.Date,500,true,false),
 new("DeclarationType","Declaration type",21,TradeFieldKind.Identifier,500,false,false),
 new("Freight","Cước vận chuyển)",22,TradeFieldKind.Number,500,false,false),
 new("ImportTax","Thuế NK",23,TradeFieldKind.Number,500,false,false),
 new("TaxableAmount","Trị giá tính thuế",24,TradeFieldKind.Number,500,false,false),
 new("TaxRate","Thuế suất",25,TradeFieldKind.Number,500,false,false),
 new("VatAmount","Tiền thuế GTGT",26,TradeFieldKind.Number,500,false,false),
 new("TaxPaymentNumber","Chứng từ thanh toán tiền thuế",27,TradeFieldKind.Identifier,500,false,false),
 new("TaxPaymentDate","Ngày thanh toán tiền thuế",28,TradeFieldKind.Date,500,false,false),
 new("SourceUserCreate","User create",29,TradeFieldKind.Text,4000,false,false),
]),
new("OTHER_EXPORT_6000","OTHER_EXPORT_6000_WORKBOOK_V1","6000","báo cáo OTHER XUẤT khẩu 2026","OtherTradeLine","DeclarationDate",false,[
 new("SourceCodeText","Souce",1,TradeFieldKind.Text,4000,false,false),
 new("TransactionType","TYPE OF TRANSACTION",2,TradeFieldKind.Text,4000,false,false),
 new("VendorName","Vendor Name",3,TradeFieldKind.Text,4000,false,false),
 new("PoNumber","PO Num",4,TradeFieldKind.Identifier,500,false,false),
 new("PoDate","PO date",5,TradeFieldKind.Date,500,false,false),
 new("InvoiceNumber","Invoice No.",6,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceDate","Invoice date",7,TradeFieldKind.Date,500,false,false),
 new("ReceiptNumber","Receipt Num",8,TradeFieldKind.Identifier,500,false,false),
 new("ReceiptDate","Receipt date",9,TradeFieldKind.Date,500,false,false),
 new("Quantity","Export QTY",10,TradeFieldKind.Number,500,false,false),
 new("DeclarationUnitPrice","Unit Price in declaration form",11,TradeFieldKind.Number,500,false,false),
 new("TotalAmount","Total amount",12,TradeFieldKind.Number,500,false,false),
 new("Currency","Currency",13,TradeFieldKind.Currency,500,false,false),
 new("DeclarationRate","Exchange rate ( declaration form)",14,TradeFieldKind.Number,500,false,false),
 new("VndAmount"," VND amount convert ",15,TradeFieldKind.Number,500,false,false),
 new("PartNumber"," Part No ",16,TradeFieldKind.Identifier,500,false,false),
 new("LineDescription","Line Description",17,TradeFieldKind.Text,4000,false,false),
 new("AwbNumber","AWB",18,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationNumber","Declaration No",19,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationDate","Declaration  date",20,TradeFieldKind.Date,500,true,false),
 new("DeclarationType","Declaration type",21,TradeFieldKind.Identifier,500,false,false),
 new("Freight"," Cước vận chuyển) ",22,TradeFieldKind.Number,500,false,false),
 new("ImportTax"," Thuế NK ",23,TradeFieldKind.Number,500,false,false),
 new("TaxableAmount"," Trị giá tính thuế ",24,TradeFieldKind.Number,500,false,false),
 new("TaxRate"," Thuế suất ",25,TradeFieldKind.Number,500,false,false),
 new("VatAmount"," Tiền thuế GTGT ",26,TradeFieldKind.Number,500,false,false),
 new("TaxPaymentNumber","Chứng từ thanh toán tiền thuế",27,TradeFieldKind.Identifier,500,false,false),
 new("TaxPaymentDate","Ngày thanh toán tiền thuế",28,TradeFieldKind.Date,500,false,false),
 new("SourceUserCreate","User create",29,TradeFieldKind.Text,4000,false,false),
]),
new("XK_6000","XK_6000_WORKBOOK_V1","6000","báo cáo XUẤT khẩu 2026","ExportLogisticsLine","IssuedDate",false,[
 new("SequenceReference","STT",1,TradeFieldKind.Identifier,500,false,false),
 new("CustomerCode","Mã khách hàng",2,TradeFieldKind.Identifier,500,false,false),
 new("OrderReference","Mã đơn hàng",3,TradeFieldKind.Identifier,500,false,false),
 new("CustomerName","Tên khách hàng",4,TradeFieldKind.Text,4000,false,false),
 new("Address","Địa chỉ",5,TradeFieldKind.Text,4000,false,false),
 new("TaxNumber","Mã số thuế",6,TradeFieldKind.Identifier,500,false,false),
 new("CreatedDate","Ngày tạo lập",7,TradeFieldKind.Date,500,false,false),
 new("IssuedDate","Ngày phát hành",8,TradeFieldKind.Date,500,true,false),
 new("InvoiceSeries","Ký hiệu",9,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceNumber","Số hóa đơn",10,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceStatus","Trạng thái",11,TradeFieldKind.Text,4000,false,false),
 new("PaymentMethod","Hình thức thanh toán",12,TradeFieldKind.Text,4000,false,false),
 new("ProductDescription","Tên sản phẩm",13,TradeFieldKind.Text,4000,false,false),
 new("UnitOfMeasure","Đơn vị tính",14,TradeFieldKind.Text,4000,false,false),
 new("Quantity"," Số lượng ",15,TradeFieldKind.Number,500,false,false),
 new("UnitPrice"," Đơn giá ",16,TradeFieldKind.Number,500,false,false),
 new("Amount"," Thành tiền ",17,TradeFieldKind.Number,500,false,false),
 new("Discount"," Chiết khấu ",18,TradeFieldKind.Number,500,false,false),
 new("VatRate","Thuế GTGT",19,TradeFieldKind.Text,4000,false,false),
 new("VatAmount","Tiền thuế",20,TradeFieldKind.Number,500,false,false),
 new("TotalAmount"," Tổng cộng ",21,TradeFieldKind.Number,500,false,false),
 new("NoCharge","Không tính tiền",22,TradeFieldKind.Boolean,500,false,false),
 new("TransactionType","Type",23,TradeFieldKind.Text,4000,false,false),
 new("InvoiceRate","Tỷ giá hóa đơn",24,TradeFieldKind.Number,500,false,false),
 new("Note","note",25,TradeFieldKind.Text,4000,false,false),
 new("MonthText","Month",26,TradeFieldKind.Text,4000,false,false),
 new("ContractNumber","hợp đồng",27,TradeFieldKind.Identifier,500,false,false),
 new("ContractDate","ngày hợp đồng",28,TradeFieldKind.Date,500,false,false),
 new("DeclarationNumber","tờ khai",29,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationDate","ngày tờ khai",30,TradeFieldKind.Date,500,false,false),
 new("DeclarationRate","tỷ giá tờ khai",31,TradeFieldKind.Number,500,false,false),
 new("BankPaymentReference","số chứng từ thanh toán ngân hàng",32,TradeFieldKind.Identifier,500,false,false),
 new("OffsetReference","offset",33,TradeFieldKind.Identifier,500,false,false),
 new("PaymentDate","ngày  thanh toán",34,TradeFieldKind.Date,500,false,false),
 new("AwbNumber","AWB",35,TradeFieldKind.Identifier,500,false,true),
]),
new("XK_1000","XK_1000_WORKBOOK_V1","1000","báo cáo XUẤT khẩu 2026","ExportLogisticsLine","IssuedDate",false,[
 new("SequenceReference","STT",1,TradeFieldKind.Identifier,500,false,false),
 new("CustomerCode","Mã khách hàng",2,TradeFieldKind.Identifier,500,false,false),
 new("OrderReference","Mã đơn hàng",3,TradeFieldKind.Identifier,500,false,false),
 new("CustomerName","Tên khách hàng",4,TradeFieldKind.Text,4000,false,false),
 new("Address","Địa chỉ",5,TradeFieldKind.Text,4000,false,false),
 new("TaxNumber","Mã số thuế",6,TradeFieldKind.Identifier,500,false,false),
 new("CreatedDate","Ngày tạo lập",7,TradeFieldKind.Date,500,false,false),
 new("IssuedDate","Ngày phát hành",8,TradeFieldKind.Date,500,true,false),
 new("InvoiceSeries","Ký hiệu",9,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceNumber","Số hóa đơn",10,TradeFieldKind.Identifier,500,false,false),
 new("InvoiceStatus","Trạng thái",11,TradeFieldKind.Text,4000,false,false),
 new("PaymentMethod","Hình thức thanh toán",12,TradeFieldKind.Text,4000,false,false),
 new("ProductDescription","Tên sản phẩm",13,TradeFieldKind.Text,4000,false,false),
 new("UnitOfMeasure","Đơn vị tính",14,TradeFieldKind.Text,4000,false,false),
 new("Quantity"," Số lượng ",15,TradeFieldKind.Number,500,false,false),
 new("UnitPrice"," Đơn giá ",16,TradeFieldKind.Number,500,false,false),
 new("Amount"," Thành tiền ",17,TradeFieldKind.Number,500,false,false),
 new("Discount"," Chiết khấu ",18,TradeFieldKind.Number,500,false,false),
 new("VatRate","Thuế GTGT",19,TradeFieldKind.Text,4000,false,false),
 new("VatAmount","Tiền thuế",20,TradeFieldKind.Number,500,false,false),
 new("TotalAmount"," Tổng cộng ",21,TradeFieldKind.Number,500,false,false),
 new("NoCharge","Không tính tiền",22,TradeFieldKind.Boolean,500,false,false),
 new("TransactionType","Type",23,TradeFieldKind.Text,4000,false,false),
 new("InvoiceRate","Tỷ giá hóa đơn",24,TradeFieldKind.Number,500,false,false),
 new("Note","note",25,TradeFieldKind.Text,4000,false,false),
 new("MonthText","Month",26,TradeFieldKind.Text,4000,false,false),
 new("ContractNumber","hợp đồng",27,TradeFieldKind.Identifier,500,false,false),
 new("ContractDate","ngày hợp đồng",28,TradeFieldKind.Date,500,false,false),
 new("DeclarationNumber","tờ khai",29,TradeFieldKind.Identifier,500,false,false),
 new("DeclarationDate","ngày tờ khai",30,TradeFieldKind.Date,500,false,false),
 new("DeclarationRate","tỷ giá tờ khai",31,TradeFieldKind.Number,500,false,false),
 new("BankPaymentReference","số chứng từ thanh toán ngân hàng",32,TradeFieldKind.Identifier,500,false,false),
 new("OffsetReference","offset",33,TradeFieldKind.Identifier,500,false,false),
 new("PaymentDate","ngày  thanh toán",34,TradeFieldKind.Date,500,false,false),
 new("AwbNumber","AWB",35,TradeFieldKind.Identifier,500,false,true),
]),
new("PIN_TEST_1000","PIN_TEST_1000_WORKBOOK_V1","1000","Pin test_1000","PinTestLine","NONE",true,[
 new("Pack","PACK",1,TradeFieldKind.Verbatim,32767,false,false),
 new("Cell","CELL",2,TradeFieldKind.Verbatim,32767,false,false),
 new("Manufacturer","MANUFACTURER",3,TradeFieldKind.Verbatim,32767,false,false),
 new("AuthorizationNumber","Giấy UQ số",4,TradeFieldKind.Verbatim,32767,false,false),
 new("AuthorizationDateRaw","Ngày",5,TradeFieldKind.Verbatim,32767,false,false),
 new("ExpiryDateRaw","Hết hạn ngày",6,TradeFieldKind.Verbatim,32767,false,false),
 new("SourceUserCreate","UserCreate\n",7,TradeFieldKind.Verbatim,32767,false,false),
]),
];
public static readonly TradeSourceProfile[] All=[..Original,
 Original.Single(p=>p.SourceCode=="OTHER_IMPORT_6000") with {SourceCode="OTHER_IMPORT_1000",Code="OTHER_IMPORT_1000_WORKBOOK_V1",SiteCode="1000"},
 Original.Single(p=>p.SourceCode=="OTHER_EXPORT_6000") with {SourceCode="OTHER_EXPORT_1000",Code="OTHER_EXPORT_1000_WORKBOOK_V1",SiteCode="1000"}
];
// Additional approved-by-source template profiles; All intentionally remains
// one base descriptor per source for authorization, previews and typed-table queries.
// V1 frozen batches remain readable. The actual XK input layout has a decorative
// first row, real headers in row 2, and an AWB/BL column before contract fields.
public static readonly TradeSourceProfile[] XkRealV2=[
    RealXkProfile(Original.Single(p=>p.SourceCode=="XK_1000")),
    RealXkProfile(Original.Single(p=>p.SourceCode=="XK_6000"))
];
private static TradeSourceProfile RealXkProfile(TradeSourceProfile old)
{
    var offsets=new Dictionary<string,int>(StringComparer.Ordinal) {
        ["AwbNumber"]=27,["ContractNumber"]=28,["ContractDate"]=29,
        ["DeclarationNumber"]=30,["DeclarationDate"]=31,["DeclarationRate"]=32,
        ["BankPaymentReference"]=33,["OffsetReference"]=34,["PaymentDate"]=35
    };
    var fields=old.Fields.Select(f=> f.Property=="AwbNumber"
        ? f with { Header="AWB/BL No\nSố chứng từ vận chuyển", Column=27, OptionalHeader=false }
        : offsets.TryGetValue(f.Property,out var col) ? f with { Column=col } : f)
        .OrderBy(f=>f.Column).ToArray();
    return old with { Code=old.SourceCode+"_WORKBOOK_V2",Sheet="2026 XK",
        HeaderRow=2,SchemaVersion=2,Fields=fields };
}

    // Actual input contracts. No report-only columns are demanded from Logistics.
    // V1 and XK V2 remain available for historical batches.
    public static readonly TradeSourceProfile[] RealInputsV3 = [
        Choose(All.Single(p=>p.SourceCode=="OTHER_IMPORT_1000"), "OTHER_IMPORT_1000_REAL_INPUT_V3", "OTHER-NHẬP", 3, new[] { ("SourceCodeText","Souce"),("TransactionType","TYPE OF TRANSACTION"),("VendorName","Vendor Name"),("PoNumber","PO Num"),("PoDate","PO date"),("InvoiceNumber","Invoice No."),("InvoiceDate","Invoice date"),("ReceiptNumber","Receipt Num"),("ReceiptDate","Receipt date"),("Quantity","Receipt QTY"),("DeclarationUnitPrice","Unit Price in declaration form"),("TotalAmount","Total amount"),("Currency","Currency"),("DeclarationRate","Exchange rate ( declaration form)"),("VndAmount","VND amount convert"),("PartNumber","Part No"),("LineDescription","Line Description"),("AwbNumber","AWB"),("DeclarationNumber","Declaration No"),("DeclarationDate","Declaration  date"),("DeclarationType","Declaration type"),("Freight","Cước vận chuyển)"),("ImportTax","Thuế NK"),("TaxableAmount","Trị giá tính thuế"),("TaxRate","Thuế suất"),("VatAmount","Tiền thuế GTGT"),("SourceUserCreate","User create") }),
        Choose(All.Single(p=>p.SourceCode=="OTHER_EXPORT_1000"), "OTHER_EXPORT_1000_REAL_INPUT_V3", "OTHER-XUẤT", 3, new[] { ("SourceCodeText","Souce"),("TransactionType","TYPE OF TRANSACTION"),("VendorName","Vendor Name"),("Quantity","Export QTY"),("DeclarationUnitPrice","Unit Price in declaration form"),("TotalAmount","Total amount"),("Currency","Currency"),("DeclarationRate","Exchange rate ( declaration form)"),("VndAmount","VND amount convert"),("PartNumber","Part No"),("LineDescription","Line Description"),("AwbNumber","AWB"),("DeclarationNumber","Declaration No"),("DeclarationDate","Declaration  date"),("DeclarationType","Declaration type"),("Freight","Cước vận chuyển)"),("ImportTax","Thuế NK"),("TaxableAmount","Trị giá tính thuế"),("TaxRate","Thuế suất"),("VatAmount","Tiền thuế GTGT"),("SourceUserCreate","User create") }),
        Choose(All.Single(p=>p.SourceCode=="OTHER_IMPORT_6000"), "OTHER_IMPORT_6000_REAL_INPUT_V3", "OTHER-NHẬP", 3, new[] { ("SourceCodeText","Souce"),("TransactionType","TYPE OF TRANSACTION"),("VendorName","Vendor Name"),("PoNumber","PO Num"),("PoDate","PO date"),("InvoiceNumber","Invoice No."),("InvoiceDate","Invoice date"),("ReceiptNumber","Receipt Num"),("ReceiptDate","Receipt date"),("Quantity","Receipt QTY"),("DeclarationUnitPrice","Unit Price in declaration form"),("TotalAmount","Total amount"),("Currency","Currency"),("DeclarationRate","Exchange rate ( declaration form)"),("VndAmount","VND amount convert"),("PartNumber","Part No"),("LineDescription","Line Description"),("AwbNumber","AWB"),("DeclarationNumber","Declaration No"),("DeclarationDate","Declaration  date"),("DeclarationType","Declaration type"),("Freight","Cước vận chuyển)"),("ImportTax","Thuế NK"),("TaxableAmount","Trị giá tính thuế"),("TaxRate","Thuế suất"),("VatAmount","Tiền thuế GTGT"),("SourceUserCreate","User create") }),
        Choose(All.Single(p=>p.SourceCode=="OTHER_EXPORT_6000"), "OTHER_EXPORT_6000_REAL_INPUT_V3", "OTHER-XUẤT", 3, new[] { ("SourceCodeText","Souce"),("TransactionType","TYPE OF TRANSACTION"),("VendorName","Vendor Name"),("Quantity","Export QTY"),("DeclarationUnitPrice","Unit Price in declaration form"),("TotalAmount","Total amount"),("Currency","Currency"),("DeclarationRate","Exchange rate ( declaration form)"),("VndAmount","VND amount convert"),("PartNumber","Part No"),("LineDescription","Line Description"),("AwbNumber","AWB"),("DeclarationNumber","Declaration No"),("DeclarationDate","Declaration  date"),("DeclarationType","Declaration type"),("Freight","Cước vận chuyển)"),("ImportTax","Thuế NK"),("TaxableAmount","Trị giá tính thuế"),("TaxRate","Thuế suất"),("VatAmount","Tiền thuế GTGT"),("SourceUserCreate","User create") }),
        Choose(All.Single(p=>p.SourceCode=="XK_1000"), "XK_1000_REAL_INPUT_V3", "2026 XK", 2, new[] { ("SequenceReference","STT"),("CustomerCode","Mã khách hàng"),("OrderReference","Mã đơn hàng"),("CustomerName","Tên khách hàng"),("Address","Địa chỉ"),("TaxNumber","Mã số thuế"),("CreatedDate","Ngày tạo lập"),("IssuedDate","Ngày phát hành"),("InvoiceSeries","Ký hiệu"),("InvoiceNumber","Số hóa đơn"),("InvoiceStatus","Trạng thái"),("PaymentMethod","Hình thức thanh toán"),("ProductDescription","Tên sản phẩm"),("UnitOfMeasure","Đơn vị tính"),("Quantity","Số lượng"),("UnitPrice","Đơn giá"),("Amount","Thành tiền"),("Discount","Chiết khấu"),("VatRate","Thuế GTGT"),("VatAmount","Tiền thuế"),("TotalAmount","Tổng cộng"),("NoCharge","Không tính tiền"),("TransactionType","Type"),("InvoiceRate","Tỷ giá hóa đơn"),("Note","note"),("MonthText","Month"),("AwbNumber","AWB/BL No\nSố chứng từ vận chuyển"),("ContractNumber","hợp đồng"),("ContractDate","ngày hợp đồng"),("DeclarationNumber","tờ khai"),("DeclarationDate","ngày tờ khai"),("DeclarationRate","tỷ giá tờ khai") }),
        Choose(All.Single(p=>p.SourceCode=="XK_6000"), "XK_6000_REAL_INPUT_V3", "2026 XK", 2, new[] { ("SequenceReference","STT"),("CustomerCode","Mã khách hàng"),("OrderReference","Mã đơn hàng"),("CustomerName","Tên khách hàng"),("Address","Địa chỉ"),("TaxNumber","Mã số thuế"),("CreatedDate","Ngày tạo lập"),("IssuedDate","Ngày phát hành"),("InvoiceSeries","Ký hiệu"),("InvoiceNumber","Số hóa đơn"),("InvoiceStatus","Trạng thái"),("PaymentMethod","Hình thức thanh toán"),("ProductDescription","Tên sản phẩm"),("UnitOfMeasure","Đơn vị tính"),("Quantity","Số lượng"),("UnitPrice","Đơn giá"),("Amount","Thành tiền"),("Discount","Chiết khấu"),("VatRate","Thuế GTGT"),("VatAmount","Tiền thuế"),("TotalAmount","Tổng cộng"),("NoCharge","Không tính tiền"),("TransactionType","Type"),("InvoiceRate","Tỷ giá hóa đơn"),("Note","note"),("MonthText","Month"),("AwbNumber","AWB/BL No\nSố chứng từ vận chuyển"),("ContractNumber","hợp đồng"),("ContractDate","ngày hợp đồng"),("DeclarationNumber","tờ khai"),("DeclarationDate","ngày tờ khai"),("DeclarationRate","tỷ giá tờ khai") }),
        Choose(All.Single(p=>p.SourceCode=="PIN_TEST_1000"), "PIN_TEST_1000_REAL_INPUT_V3", "8-Pin test", 1, new[] { ("Pack","PACK"),("Cell","CELL"),("Manufacturer","MANUFACTURER"),("AuthorizationNumber","Giấy UQ số"),("AuthorizationDateRaw","Ngày"),("ExpiryDateRaw","Hết hạn ngày"),("SourceUserCreate","UserCreate\n") }),
    ];
    // New uploads: single exact header in Excel row 1; historical V1-V3 remain readable.
    public static readonly TradeSourceProfile[] HeaderOneV4 = RealInputsV3
        .Select(p=>p with {
            Code=p.SourceCode+"_HEADER1_V4", HeaderRow=1, SchemaVersion=4,
            // In new Pin files the displayed user header is literally UserCreate,
            // without the trailing line break kept by the historical V3 template.
            Fields=p.SourceCode=="PIN_TEST_1000"
                ? p.Fields.Select(f=>f.Property=="SourceUserCreate" ? f with { Header="UserCreate" } : f).ToArray()
                : p.Fields
        })
        .ToArray();
    private static TradeSourceProfile Choose(TradeSourceProfile basis,string code,string sheet,int row,
       (string Property,string Header)[] layout)
    {
        var allowed=basis.Fields.ToDictionary(f=>f.Property,StringComparer.Ordinal);
        var fields=layout.Select((entry,i)=>allowed[entry.Property] with {
            Header=entry.Header, Column=i+1, OptionalHeader=false
        }).ToArray();
        if(fields.Select(f=>f.Property).Distinct(StringComparer.Ordinal).Count()!=fields.Length)
           throw new InvalidOperationException("Duplicate field in compiled profile.");
        return basis with {Code=code,Sheet=sheet,HeaderRow=row,SchemaVersion=3,Fields=fields};
    }
}
