using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.FinanceSources;

var parser=new FinanceFileParser();var checks=0;
var policy=new FinanceParsePolicy("EXPLICIT_RANGE",new(2026,1,1),new(2027,1,1),true);
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
ParsedFinanceFile Parse(FinanceSourceProfile profile,Dictionary<int,Dictionary<int,Cell>> rows,FinanceParsePolicy? p=null,string extension=".xlsx")
{using var stream=Fixture.Write(profile,rows);return parser.Parse(stream,"fixture"+extension,profile.SourceCode,p??policy);}
foreach(var profile in FinanceSourceProfiles.All)
{
 var rows=Fixture.Rows(profile);var parsed=Parse(profile,rows);
 Check(parsed.Errors==0&&parsed.DataRows==2&&parsed.Rows.Count(r=>r.Line is not null)==2,$"{profile.SourceCode} valid layout -> 2 typed");
 var row=parsed.Rows.First(x=>x.Kind=="DATA");var identifier=profile.Fields.First(x=>x.Kind==FinanceFieldKind.Identifier);
 Check((string?)row.Line![identifier.Property]=="000123",$"{profile.SourceCode} preserves leading zeros");
 var date=profile.Fields.First(x=>x.Property==profile.CoverageProperty);
 rows[profile.DataStart][date.Column]=new("31/02/2026");parsed=Parse(profile,rows);
 Check(parsed.Issues.Any(x=>x.Code=="INVALID_DATE"&&x.RecordNumber==profile.DataStart),$"{profile.SourceCode} invalid date locates row/column");
 Check(parsed.Rows.First(x=>x.Kind=="DATA").Line is null,$"{profile.SourceCode} invalid row stays raw, not typed");
 rows=Fixture.Rows(profile);rows[profile.HeaderStart][profile.Fields[0].HeaderColumn]=new("Wrong header");
 Check(Parse(profile,rows).Issues.Any(x=>x.Code=="HEADER_MISMATCH"),$"{profile.SourceCode} wrong header rejected");
 rows=Fixture.Rows(profile);Check(Parse(profile,rows,extension:".xls").Errors>0,$"{profile.SourceCode} legacy extension not silently accepted");
 rows=Fixture.Rows(profile);parsed=Parse(profile,rows,policy with{Start=new(2026,3,1)});
 Check(parsed.Issues.Any(x=>x.Code=="WRONG_COVERAGE"),$"{profile.SourceCode} date coverage enforced, not dropped");
}
var ap=FinanceSourceProfiles.Get("AP_REPORT");var a=Fixture.Rows(ap);
a[ap.DataStart][7]=new("000077","str","\"000077\"");
var good=Parse(ap,a);Check(good.Errors==0&&(string?)good.Rows[0].Line!["InvoiceNumber"]=="000077","AP literal formula cache matched");
a[ap.DataStart][7]=new("WRONG","str","\"000077\"");Check(Parse(ap,a).Issues.Any(x=>x.Code=="FORMULA_CACHE_MISMATCH"),"AP mismatched literal cache rejected");
a[ap.DataStart][7]=new(null,"str","\"000077\"");Check(Parse(ap,a).Issues.Any(x=>x.Code=="FORMULA_CACHE_MISSING"),"AP missing cache rejected");
a=Fixture.Rows(ap);a[ap.DataStart][11]=new(null);good=Parse(ap,a);Check(good.Rows[0].Line!["WithheldAmount"] is null,"AP blank withheld stays null not zero");
a[ap.DataStart][8]=new("-100.12","n");good=Parse(ap,a);Check(good.Errors==0&&good.Issues.Any(x=>x.Code=="NEGATIVE_VALUE")&&(decimal?)good.Rows[0].Line!["InvoiceGrossAmount"]==-100.12m,"AP credit amount retained with warning");
a=Fixture.Rows(ap);a[ap.DataStart+1]=new(a[ap.DataStart]);good=Parse(ap,a);Check(good.DataRows==2&&good.Issues.Any(x=>x.Code=="DUPLICATE_ROW"),"Duplicate content retained with warning, no dedupe");
a=Fixture.Rows(ap);a[ap.DataStart][7]=new("000077","str","\"000077\"","shared","0",$"G{ap.DataStart}:G{ap.DataStart+1}");a[ap.DataStart+1][7]=new("000077","str","","shared","0");
good=Parse(ap,a);Check(good.Errors==0&&(string?)good.Rows[1].Line!["InvoiceNumber"]=="000077"&&good.Rows[1].Cells.Single(x=>x.Ordinal==7).FormulaOrigin==$"G{ap.DataStart}","Shared literal resolved only in original same-column range");
a[ap.DataStart+1][7]=new("000077","str","","shared","99");Check(Parse(ap,a).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),"Unknown shared group rejected");
a=Fixture.Rows(ap);a[ap.DataStart][3]=new("1234567890123456","n");Check(Parse(ap,a).Issues.Any(x=>x.Code=="IDENTIFIER_PRECISION"),"Numeric identifier beyond Excel 15 digits rejected");
Check(FinanceFileParser.TryExactDecimal("0.30000000000000004",out var exact)&&exact==0.30000000000000004m,"Decimal reads XML lexeme without double rounding");
Check(!FinanceFileParser.TryExactDecimal("0.1234567890123456789",out _),"Decimal scale overflow blocked");
Check(!FinanceFileParser.TryExactDecimal("99999999999999999999.999999999999999999",out _),".NET decimal rounding blocked by exact comparison");
Check(!FinanceFileParser.TryExactDecimal("1,234.56",out _),"Ambiguous grouped number rejected");
Check(FinanceFileParser.TryExactDecimal("1.25E-5",out exact)&&exact==0.0000125m,"Scientific numeric lexeme expanded exactly");
var supplier=FinanceSourceProfiles.Get("SUPPLIER_PAYMENT_REGISTER");var s=Fixture.Rows(supplier);good=Parse(supplier,s);
Check(good.Rows.Count==4&&good.DataRows==2&&good.Rows.Count(x=>x.Kind=="SUBTOTAL")==1&&good.Rows.Count(x=>x.Kind=="FOOTER")==1,"Supplier subtotal/footer raw preserved, no typed duplication");
Check((string?)good.Rows[0].Line!["PayeeName"]=="TEST PayeeName","Supplier Payee header Z reads data Y");
s[supplier.DataStart][26]=new("unexpected moved Payee");Check(Parse(supplier,s).Issues.Any(x=>x.Severity=="ERROR"&&x.Code=="UNMAPPED_CELL"),"Supplier changed merged-column layout blocks confirmation");
s=Fixture.Rows(supplier);s[supplier.DataStart][24]=new("Y");good=Parse(supplier,s);Check(good.Errors==0&&good.DataRows==2&&good.Issues.Any(x=>x.Code=="VOID_PAYMENT"),"Supplier void Y retained and flagged");
s=Fixture.Rows(supplier);s.Remove(supplier.DataStart+3);Check(Parse(supplier,s).Issues.Any(x=>x.Code=="FOOTER_MISSING"),"Truncated supplier missing End of Report blocked");
s=Fixture.Rows(supplier);s[supplier.DataStart+4]=new(s[supplier.DataStart]);Check(Parse(supplier,s).Issues.Any(x=>x.Code=="DATA_AFTER_FOOTER"),"Supplier data after footer not silently dropped");
var usd=FinanceSourceProfiles.Get("EX_USD");var u=Fixture.Rows(usd);u[usd.DataStart][3]=new("37029.5","n","40398-3368.5");good=Parse(usd,u);
Check(good.Errors==0&&(decimal?)good.Rows[0].Line!["InvoiceAmount"]==37029.5m&&good.Issues.Any(x=>x.Code=="FORMULA_CACHED_VALUE"),"USD amount cache allowed explicitly with warning");
Check(Parse(usd,u,policy with{AllowCachedAmountFormulas=false}).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),"USD cached formula policy false rejects it");
u[usd.DataStart][3]=new("123","n","[external.xlsx]Sheet1!A1");Check(Parse(usd,u).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),"External formula blocked, no evaluation");
u=Fixture.Rows(usd);u[usd.DataStart][4]=new(null);u[usd.DataStart][5]=new(null);good=Parse(usd,u);Check(good.Errors==0&&good.Issues.Any(x=>x.Code=="LINK_DATA_MISSING"),"USD optional PO/declaration missing does not fabricate values");
u=Fixture.Rows(usd);u[usd.DataStart][11]=new("Unmapped note");good=Parse(usd,u);Check(good.Errors==0&&good.Rows[0].Cells.Any(x=>x.Ordinal==11)&&good.Issues.Any(x=>x.Code=="UNMAPPED_CELL"),"USD extra K note preserved, warning required");
var vnd=FinanceSourceProfiles.Get("EX_VND");var v=Fixture.Rows(vnd);v[vnd.DataStart][5]=new("0000714/0000715");v[vnd.DataStart][8]=new("107919707931");good=Parse(vnd,v);
Check(good.Errors==0&&(string?)good.Rows[0].Line!["InvoiceReference"]=="0000714/0000715"&&good.Issues.Any(x=>x.Code=="REFERENCE_NEEDS_REVIEW"),"VND slash reference preserved, not split");
Check(good.Rows[0].Cells.Any(x=>x.Ordinal==8)&&good.Issues.Any(x=>x.Code=="UNMAPPED_CELL"),"VND extra H cell preserved");
v=Fixture.Rows(vnd);v[vnd.DataStart][2]=new("46028","n");good=Parse(vnd,v);Check(good.Errors==0&&good.Rows[0].Line!["BankDate"] is DateOnly,"Excel date serial decoded by typed column");
v[vnd.DataStart][2]=new("60","n");Check(Parse(vnd,v).Issues.Any(x=>x.Code=="INVALID_DATE"),"Excel fictitious 1900-02-29 rejected");
try{FinanceSourcePolicy.Resolve("{\"ingestionMode\":\"SNAPSHOT\"}",new(2026,9,1),new(2026,10,1));Check(false,"Missing policy");}catch(PoImportFault e){Check(e.Code=="FINANCE_PERIOD_POLICY_REQUIRED","Missing period policy fails clearly");}
try{FinanceSourceProfiles.Get("AP_REPORT]; DROP TABLE sec.AppUser");Check(false,"Unknown source");}catch(PoImportFault e){Check(e.Status==404,"Source identifiers are strictly whitelisted");}
var sampleIndex=Array.IndexOf(args,"--write-samples");
if(sampleIndex>=0)
{
 var folder=Path.GetFullPath(args[sampleIndex+1]);Directory.CreateDirectory(folder);
 foreach(var p in FinanceSourceProfiles.All)
 {
  var rows=Fixture.Rows(p);using(var stream=Fixture.Write(p,rows))using(var file=File.Create(Path.Combine(folder,p.SourceCode+"_01_valid.xlsx")))stream.CopyTo(file);
  var date=p.Fields.Single(f=>f.Property==p.CoverageProperty);rows[p.DataStart][date.Column]=new("31/02/2026");
  using(var stream=Fixture.Write(p,rows))using(var file=File.Create(Path.Combine(folder,p.SourceCode+"_02_bad_date.xlsx")))stream.CopyTo(file);
 }
 Console.WriteLine("Synthetic samples written (no real financial data).");
}
var realIndex=Array.IndexOf(args,"--real-dir");
if(realIndex>=0)
{
 var folder=Path.GetFullPath(args[realIndex+1]);
 var specs=new[]{
  ("AP_REPORT","XXAPR017___Invoice_Detail_Repo_280926.xlsx","105f4fcb3350627967a3e7bc4de60c73d7d302a45b532c84806affbc2c7cbcff",2450,2450),
  ("SUPPLIER_PAYMENT_REGISTER","XXAPR015___Supplier_Payment_Re_280926.xlsx","05ba457960031b67c7c9ee9710c41ffbfc94711133befe1d5b3a5ff03403fe5c",3422,3506),
  ("EX_USD","EX_USD(1).xlsx","74f3079b1167d49396d5523e0afe9ce03432970a0633448e305a5877fb603268",538,538),
  ("EX_VND","EX_VND(1).xlsx","68ea0f95ba9f4b16adf9fac6725fc9f0298e2aa0f3767d5fe2a9016cbd45f9e4",2268,2268)
 };
 foreach(var(source,file,sha,typed,rawCount) in specs)
 {
  using var fileStream=File.OpenRead(Path.Combine(folder,file));Check(Convert.ToHexString(SHA256.HashData(fileStream)).Equals(sha,StringComparison.OrdinalIgnoreCase),source+" real SHA256");
  fileStream.Position=0;var parsed=parser.Parse(fileStream,file,source,policy);
  if(parsed.Errors>0)foreach(var issue in parsed.Issues.Where(x=>x.Severity=="ERROR").Take(8))Console.WriteLine($"ERROR {issue.Code} row={issue.RecordNumber} column={issue.Column}: {issue.Message}");
  Check(parsed.Errors==0&&parsed.DataRows==typed&&parsed.Rows.Count(x=>x.Line is not null)==typed&&parsed.Rows.Count==rawCount,source+" real row/raw/typed counts");
  if(source=="AP_REPORT")Check(parsed.Rows.Count(x=>x.Line?["InvoiceGrossAmount"] is decimal m&&m<0)==49,"AP 49 negative rows retained");
  if(source=="SUPPLIER_PAYMENT_REGISTER")Check(parsed.Rows.Count(x=>x.Line?.GetValueOrDefault("VoidStatus") is "Y")==8&&parsed.Rows.Count(x=>x.Kind=="SUBTOTAL")==83,"Supplier 8 void and 83 subtotal retained");
  if(source=="EX_USD")
  {Check((decimal?)parsed.Rows.Single(x=>x.RecordNumber==20).Line!["InvoiceAmount"]==355000m&&(decimal?)parsed.Rows.Single(x=>x.RecordNumber==202).Line!["InvoiceAmount"]==37029.5m,"USD C20/C202 cached values exact");
   Check(parsed.Rows.Single(x=>x.RecordNumber==173).Cells.Any(x=>x.CellReference=="K173"&&x.Text is not null),"USD real K173 extra note kept");}
  if(source=="EX_VND")Check(parsed.Rows.Single(x=>x.RecordNumber==398).Cells.Any(x=>x.CellReference=="H398"&&x.Text=="107919707931"),"VND real H398 extra value kept");
  Console.WriteLine($"PROFILE {source}: data={parsed.DataRows}, raw={parsed.Rows.Count}, errors={parsed.Errors}, warnings={parsed.Warnings}");
 }
}
Console.WriteLine($"Completed {checks} checks. SQL/AD/Windows/concurrency integration is a separate required gate.");
record Cell(string? Value,string Type="inlineStr",string? Formula=null,string? FormulaKind=null,string? SharedId=null,string? Range=null);
static class Fixture
{
 static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
 public static Dictionary<int,Dictionary<int,Cell>> Rows(FinanceSourceProfile p)
 {
  var rows=new Dictionary<int,Dictionary<int,Cell>>{{p.HeaderStart,p.Fields.ToDictionary(x=>x.HeaderColumn,x=>new Cell(x.Header))}};
  for(var i=0;i<2;i++)rows[p.DataStart+i]=p.Fields.ToDictionary(x=>x.Column,x=>x.Kind switch {
   FinanceFieldKind.Number=>new Cell("100.25","n"),FinanceFieldKind.Date=>new Cell("12/02/2026"),FinanceFieldKind.Currency=>new Cell("USD"),
   FinanceFieldKind.Identifier=>new Cell("000123"),_=>new Cell(x.Property=="VoidStatus"?"N":x.Property=="InvoiceStatus"?"Validated":"TEST "+x.Property)});
  if(p.SourceCode=="SUPPLIER_PAYMENT_REGISTER")
  {rows[p.DataStart+2]=new(){{17,new("Subtotal :")},{18,new("200.5","n")},{19,new("200.5","n")},{20,new("200.5","n")}};rows[p.DataStart+3]=new(){{1,new("End of Report")}};}
  return rows;
 }
 static string Label(int c){var t="";for(;c>0;c=(c-1)/26)t=(char)('A'+(c-1)%26)+t;return t;}
 public static MemoryStream Write(FinanceSourceProfile p,Dictionary<int,Dictionary<int,Cell>> rows)
 {
  var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
  {
   void Add(string path,string text){using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(text);}
   Add("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
   Add("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
   Add("xl/workbook.xml",$"<workbook xmlns=\"{S}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{p.Sheet}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
   Add("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
   var sheet=new XElement(S+"worksheet",new XElement(S+"sheetData",rows.OrderBy(x=>x.Key).Select(row=>new XElement(S+"row",new XAttribute("r",row.Key),row.Value.OrderBy(x=>x.Key).Select(pair=>{
    var c=pair.Value;var cell=new XElement(S+"c",new XAttribute("r",Label(pair.Key)+row.Key),new XAttribute("t",c.Type));
    if(c.Formula is not null){var f=new XElement(S+"f",c.Formula);if(c.FormulaKind is not null)f.Add(new XAttribute("t",c.FormulaKind));if(c.SharedId is not null)f.Add(new XAttribute("si",c.SharedId));if(c.Range is not null)f.Add(new XAttribute("ref",c.Range));cell.Add(f);}
    if(c.Value is not null)cell.Add(c.Type=="inlineStr"?new XElement(S+"is",new XElement(S+"t",c.Value)):new XElement(S+"v",c.Value));return cell;
   })))));
   Add("xl/worksheets/sheet1.xml",sheet.ToString(SaveOptions.DisableFormatting));
  }stream.Position=0;return stream;
 }
}
