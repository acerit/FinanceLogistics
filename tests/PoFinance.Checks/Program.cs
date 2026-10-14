using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;

var checks = 0;
void Check(string name, bool ok) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
var parser = new PoFileParser();
var start = new DateOnly(2026, 7, 1); var end = start.AddMonths(1);
XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
XElement Text(string address, string value) => new(ns + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value)));
XElement Number(string address, string value) => new(ns + "c", new XAttribute("r", address), new XElement(ns + "v", value));
XElement Formula(string address, string formula, string? cached, string? type = null) => new(ns + "c", new XAttribute("r", address),
    type is null ? null : new XAttribute("t", type), new XElement(ns + "f", formula), cached is null ? null : new XElement(ns + "v", cached));
string Serial(int year, int month, int day) => (new DateTime(year, month, day) - new DateTime(1899, 12, 30)).TotalDays.ToString(CultureInfo.InvariantCulture);
XDocument Sheet()
{
    var headers = PoProfile.Headers.ToArray(); headers[7] = "PO Curr";
    var row = new XElement(ns + "row", new XAttribute("r", 3),
        Text("A3", "TEST"),Text("B3", "000123"),Number("C3", Serial(2026,7,2)),Text("D3", "PO-1"),Number("E3", "1"),
        Number("F3", "12.5"),Text("G3", "1000"),Text("H3", "USD"),Number("I3", "46029"),Number("J3", "25000"),
        Number("K3", "25000"),Number("L3", Serial(2026,7,2)),Formula("M3", "K3*F3", "312500"),
        Formula("N3", "\"00000322\"", "00000322", "str"),Number("O3", "312500"),Number("P3", "12.5"),Text("Q3", "Synthetic test only"));
    return new(new XElement(ns + "worksheet", new XElement(ns + "sheetData",
        new XElement(ns + "row", new XAttribute("r", 1),headers.Select((x,i) => Text(((char)('A'+i))+"1", x))),
        new XElement(ns + "row", new XAttribute("r", 2)),row),
        new XElement(ns + "mergeCells",Enumerable.Range(0,17).Select(i => new XElement(ns + "mergeCell",new XAttribute("ref",$"{(char)('A'+i)}1:{(char)('A'+i)}2"))))));
}
XElement Cell(XDocument doc, string address) => doc.Descendants(ns + "c").Single(x => (string?)x.Attribute("r") == address);
MemoryStream Workbook(Action<XDocument>? edit = null, bool date1904 = false, string sheetName = "PO update")
{
    var doc = Sheet(); edit?.Invoke(doc); var stream = new MemoryStream();
    using (var zip = new ZipArchive(stream,ZipArchiveMode.Create,true))
    {
        void Add(string path,string content) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false));writer.Write(content); }
        Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Add("_rels/.rels",new XDocument(new XElement(pkg+"Relationships",new XElement(pkg+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",rel.NamespaceName+"/officeDocument"),new XAttribute("Target","xl/workbook.xml")))).ToString());
        Add("xl/workbook.xml",new XDocument(new XElement(ns+"workbook",new XElement(ns+"workbookPr",new XAttribute("date1904",date1904 ? "1":"0")),new XElement(ns+"sheets",new XElement(ns+"sheet",new XAttribute("name",sheetName),new XAttribute("sheetId",1),new XAttribute(rel+"id","rId1"))))).ToString());
        Add("xl/_rels/workbook.xml.rels",new XDocument(new XElement(pkg+"Relationships",new XElement(pkg+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",rel.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet1.xml")))).ToString());
        Add("xl/worksheets/sheet1.xml",doc.ToString());
    }
    stream.Position=0;return stream;
}
ParsedPoFile Read(Action<XDocument>? edit=null,string mode=PoProfile.SelectedMonth,bool date1904=false,string sheetName="PO update")
{ using var file=Workbook(edit,date1904,sheetName);return parser.Parse(file,"sample.xlsx",start,end,profile:PoProfile.FinanceCode,periodMode:mode); }
bool Has(ParsedPoFile file,string code,string severity="ERROR")=>file.Issues.Any(x=>x.Code==code&&x.Severity==severity);
var good=Read();
Check("Finance headers, merged rows and data row 3",good.Errors==0&&good.Rows.Count==1&&good.Rows[0].RecordNumber==3);
Check("PO Curr becomes canonical currency",good.Rows[0].Line!.PoCurrency=="USD");
Check("Literal invoice formula preserves leading zeros",good.Rows[0].Line!.InvoiceNumber=="00000322");
Check("Serial date in General format is interpreted only as a date field",good.Rows[0].Line!.PoRateDate==new DateOnly(2026,1,7));
Check("Numeric amount formula requires acknowledgement",Has(good,"FORMULA_CACHED_VALUE","WARNING"));
Check("Raw provenance retains formula and exact cache",good.Rows[0].Cells[13].Formula=="=\"00000322\""&&good.Rows[0].Cells[13].CachedValue=="00000322"&&good.Rows[0].Cells[13].CellReference=="N3");
Check("Formula cache missing is an error",Has(Read(d=>Cell(d,"M3").Element(ns+"v")!.Remove()),"FORMULA_CACHE_MISSING"));
Check("Empty numeric formula cache is an error",Has(Read(d=>Cell(d,"M3").Element(ns+"v")!.Value=""),"FORMULA_CACHE_MISSING"));
Check("Literal/cache disagreement blocks typed line",Read(d=>Cell(d,"N3").Element(ns+"v")!.Value="322") is var mismatch && Has(mismatch,"FORMULA_CACHE_MISMATCH")&&mismatch.Rows[0].Line is null);
Check("Formula in an unapproved column is rejected",Has(Read(d=>Cell(d,"P3").ReplaceWith(Formula("P3","F3","12.5"))),"FORMULA_UNSUPPORTED"));
Check("External formula cannot use a cached result",Has(Read(d=>Cell(d,"M3").Element(ns+"f")!.Value="'[other.xlsx]S'!A1"),"FORMULA_UNSUPPORTED"));
Check("Shared formula is not guessed",Has(Read(d=>Cell(d,"M3").Element(ns+"f")!.SetAttributeValue("t","shared")),"FORMULA_UNSUPPORTED"));
Check("Excel error retains cell coordinates",Read(d=>Cell(d,"M3").ReplaceWith(new XElement(ns+"c",new XAttribute("r","M3"),new XAttribute("t","e"),new XElement(ns+"v","#VALUE!")))) is var excelError && excelError.Issues.Any(x=>x.Code=="EXCEL_CELL_ERROR"&&x.RecordNumber==3&&x.Column=="Purchase Functional Amount"));
Check("Wrong Finance sheet rejected",Has(Read(sheetName:"PO_UPDATED"),"SHEET_LAYOUT"));
Check("Incorrect header geometry rejected",Has(Read(d=>d.Descendants(ns+"mergeCell").First().Remove()),"HEADER_LAYOUT"));
Check("Unknown header rejected",Has(Read(d=>Cell(d,"H1").ReplaceWith(Text("H1","Unexpected"))),"HEADER_MISMATCH"));
Check("Data in header row 2 cannot be skipped",Has(Read(d=>d.Descendants(ns+"row").Single(x=>(string?)x.Attribute("r")=="2").Add(Text("A2","Actual data"))),"HEADER_LAYOUT"));
Check("Extra data column blocks confirmation",Has(Read(d=>d.Descendants(ns+"row").Last().Add(Text("R3","Extra"))),"ROW_COLUMN_COUNT"));
Check("Monthly mode still rejects earlier month",Has(Read(d=>Cell(d,"C3").Element(ns+"v")!.Value=Serial(2026,1,10)),"WRONG_PERIOD"));
var cumulative=Read(d=>Cell(d,"C3").Element(ns+"v")!.Value=Serial(2026,1,10),PoProfile.YearToPeriod);
Check("Explicit YTD retains earlier months with warning",cumulative.Errors==0&&cumulative.Rows.Count==1&&Has(cumulative,"YEAR_TO_PERIOD_SNAPSHOT","WARNING"));
Check("YTD rejects future month",Has(Read(d=>Cell(d,"C3").Element(ns+"v")!.Value=Serial(2026,8,1),PoProfile.YearToPeriod),"WRONG_PERIOD"));
Check("YTD rejects previous year",Has(Read(d=>Cell(d,"C3").Element(ns+"v")!.Value=Serial(2025,12,31),PoProfile.YearToPeriod),"WRONG_PERIOD"));
var precision=Read(d=> { Cell(d,"M3").Element(ns+"v")!.Value="17477532.800000001";Cell(d,"F3").Element(ns+"v")!.Value="0.019999999999999997";Cell(d,"P3").Element(ns+"v")!.Value="39608.639999999999"; });
Check("Exact Excel decimal values survive without rounding",precision.Errors==0&&precision.Rows[0].Line!.PurchaseFunctionalAmount==17477532.800000001m&&precision.Rows[0].Line!.PoUnitPrice==0.019999999999999997m&&precision.Rows[0].Line!.InvoiceAmount==39608.639999999999m);
Check("Too-small scientific number cannot silently become zero",Has(Read(d=>Cell(d,"F3").Element(ns+"v")!.Value="1E-30"),"DECIMAL_PRECISION"));
Check("Scientific notation is converted exactly within capacity",Read(d=>Cell(d,"F3").Element(ns+"v")!.Value="1.25E-2").Rows[0].Line!.PoUnitPrice==0.0125m);
var epoch1904Result=Read(d=>{Cell(d,"C3").Element(ns+"v")!.Value=(new DateTime(2026,7,2)-new DateTime(1904,1,1)).TotalDays.ToString(CultureInfo.InvariantCulture);Cell(d,"I3").Element(ns+"v")!.Value="0";},date1904:true);
Check("1904 workbook epoch is honored",epoch1904Result.Errors==0&&epoch1904Result.Rows[0].Line!.TransactionDate==new DateOnly(2026,7,2)&&epoch1904Result.Rows[0].Line!.PoRateDate==new DateOnly(1904,1,1));
Check("Excel fictional 1900 leap day rejected",Has(Read(d=>Cell(d,"I3").Element(ns+"v")!.Value="60"),"INVALID_EXCEL_DATE"));
var nullable=Read(d=>{Cell(d,"O3").Remove();Cell(d,"P3").Remove();});
Check("Missing invoice amounts remain null with warning",nullable.Errors==0&&nullable.Rows[0].Line!.InvoiceAmount is null&&Has(nullable,"INVOICE_AMOUNT_MISSING","WARNING"));
var repeated=Read(d=> { var row=new XElement(d.Descendants(ns+"row").Last());row.SetAttributeValue("r",4);foreach(var c in row.Elements(ns+"c")) { c.SetAttributeValue("r",c.Attribute("r")!.Value[..^1]+"4"); var f=c.Element(ns+"f"); if(f?.Value=="K3*F3")f.Value="K4*F4"; }d.Descendants(ns+"sheetData").Single().Add(row);});
Check("Repeated receipt keys are retained, never deduplicated",repeated.Errors==0&&repeated.Rows.Count==2&&Has(repeated,"RECEIPT_KEY_REPEATED","WARNING"));
var sharedLiteral=Read(d=> {
    var first=d.Descendants(ns+"row").Last();
    var second=new XElement(first);second.SetAttributeValue("r",4);
    foreach(var c in second.Elements(ns+"c")) { c.SetAttributeValue("r",c.Attribute("r")!.Value[..^1]+"4"); if(c.Element(ns+"f")?.Value=="K3*F3")c.Element(ns+"f")!.Value="K4*F4"; }
    var anchor=Cell(d,"N3").Element(ns+"f")!;anchor.SetAttributeValue("t","shared");anchor.SetAttributeValue("si","0");anchor.SetAttributeValue("ref","N3:N4");
    var follower=second.Elements(ns+"c").Single(x=>(string?)x.Attribute("r")=="N4").Element(ns+"f")!;
    follower.Value="";follower.SetAttributeValue("t","shared");follower.SetAttributeValue("si","0");
    d.Descendants(ns+"sheetData").Single().Add(second);
});
Check("Shared literal invoices resolve to their anchor without evaluating Excel",sharedLiteral.Errors==0&&sharedLiteral.Rows.Count==2&&sharedLiteral.Rows[1].Line!.InvoiceNumber=="00000322"&&sharedLiteral.Rows[1].Cells[13].FormulaOrigin=="N3"&&sharedLiteral.Rows[1].Cells[13].SharedFormulaId=="0");
Check("Unresolved shared invoice formula is blocked",Has(Read(d=> {var f=Cell(d,"N3").Element(ns+"f")!;f.Value="";f.SetAttributeValue("t","shared");f.SetAttributeValue("si","99");}),"FORMULA_UNSUPPORTED"));
using(var dev=Workbook()) Check("Old DEV profile continues to reject formulas",parser.Parse(dev,"sample.xlsx",start,end).Issues.Any(x=>x.Code=="FORMULA_NOT_ALLOWED"));
using(var financeCsv=new MemoryStream(Encoding.UTF8.GetBytes("a,b"))) Check("Finance profile cannot accept a renamed CSV",parser.Parse(financeCsv,"sample.csv",start,end,profile:PoProfile.FinanceCode).Issues.Any(x=>x.Code=="PROFILE_REQUIRES_XLSX"));
try { PoProfile.PeriodMode(PoProfile.FinanceCode,"{\"ingestionMode\":\"SNAPSHOT\"}");Check("Unconfigured Finance requirement fails closed",false); }
catch(PoImportFault error){Check("Unconfigured Finance requirement fails closed",error.Code=="PO_PERIOD_POLICY_REQUIRED");}
Check("Old requirement defaults to monthly DEV behavior",PoProfile.PeriodMode(PoProfile.Code,"{}") == PoProfile.SelectedMonth);
var oldRaw=System.Text.Json.JsonSerializer.Deserialize<PoCellDto>("{\"ordinal\":1,\"header\":\"Souce\",\"text\":\"PO\",\"cellType\":\"String\"}",PoProfile.Json)!;
Check("Old RawRow JSON remains readable",oldRaw.Text=="PO"&&oldRaw.Formula is null&&oldRaw.CellReference is null);
if(args.Length>0)
{
    if(args.Length!=2||args[0]!="--real-file") throw new ArgumentException("Optional: --real-file <absolute PO_update(1).xlsx path>");
    using var input=File.OpenRead(args[1]);
    Check("Real file matches reviewed version",Convert.ToHexString(SHA256.HashData(input)).Equals("33ddf4afd274b37fbd476261ce84b45e76887edfd9c7b68d32ecf72a056c00f8",StringComparison.OrdinalIgnoreCase));
    input.Position=0;
    var real=parser.Parse(input,"PO_update.xlsx",start,end,profile:PoProfile.FinanceCode,periodMode:PoProfile.YearToPeriod);
    foreach(var group in real.Issues.GroupBy(x=>new {x.Severity,x.Code})) Console.WriteLine($"{group.Key.Severity} {group.Key.Code}: {group.Count()}");
    Check("Real PO: 1371 raw and typed rows; zero errors",real.Errors==0&&real.Rows.Count==1371&&real.Rows.All(x=>x.Line is not null));
    Check("Real PO begins at row 3 and ends at 1373",real.Rows[0].RecordNumber==3&&real.Rows[^1].RecordNumber==1373);
    Check("Real PO has 653 literal plus six numeric formulas",real.Rows.Sum(x=>x.Cells.Count(c=>c.Formula is not null))==659&&real.Issues.Count(x=>x.Code=="FORMULA_CACHED_VALUE")==6);
    Check("Real I118 serial maps to 2026-01-07",real.Rows.Single(x=>x.RecordNumber==118).Line!.PoRateDate==new DateOnly(2026,1,7));
    Check("Real M241 exact XML cache is preserved",real.Rows.Single(x=>x.RecordNumber==241).Line!.PurchaseFunctionalAmount==17477532.800000001m);
    Check("Real M839 exact XML cache is preserved",real.Rows.Single(x=>x.RecordNumber==839).Line!.PurchaseFunctionalAmount==34812176275.200005m);
    Check("Real duplicate-key rows survive",new[]{462,560,1044,1045}.All(n=>real.Rows.Any(x=>x.RecordNumber==n&&x.Line is not null)));
    Check("Real four missing invoice amounts remain null",new[]{458,560,1044,1045}.All(n=>real.Rows.Single(x=>x.RecordNumber==n).Line!.InvoiceAmount is null));
}
Console.WriteLine($"{checks} checks passed. SQL/API/Worker integration must also be checked on Windows/SQL Server. No business sign-off is implied.");
