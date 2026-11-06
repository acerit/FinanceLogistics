using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.LogisticsNk;

var parser=new NkFileParser();var checks=0;
var policy=new NkParsePolicy("EXPLICIT_RANGE",new(2026,1,1),new(2027,1,1));
void Check(bool yes,string message){if(!yes)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);checks++;}
ParsedNkFile Parse(NkSourceProfile p,Dictionary<int,Dictionary<int,Cell>> rows,NkParsePolicy? cfg=null,string extension=".xlsx")
{using var s=Fixture.Write(p,rows);return parser.Parse(s,"sample"+extension,p.SourceCode,cfg??policy);}
foreach(var p in NkSourceProfiles.All)
{
 var rows=Fixture.Rows(p);var good=Parse(p,rows);
 Check(good.Errors==0&&good.DataRows==2&&good.Rows.All(x=>x.Line is not null),p.SourceCode+" layout and two rows");
 Check((string?)good.Rows[0].Line!["InvoiceNumber"]=="000123",p.SourceCode+" text identifiers preserve zero");
 Check(good.Issues.Any(x=>x.Code=="DUPLICATE_ROW")&&good.Rows.Count==2,p.SourceCode+" duplicate rows kept");
 rows[p.DataStart][3]=new("31/02/2026");var bad=Parse(p,rows);
 Check(bad.Issues.Any(x=>x.Code=="INVALID_DATE"&&x.RecordNumber==p.DataStart)&&bad.Rows.Count==2&&bad.Rows[0].Line is null,p.SourceCode+" error row retained as raw");
 rows=Fixture.Rows(p);rows[p.DataStart][22]=new("1125-Acer_Desktop_Gaming");
 Check(Parse(p,rows).Issues.Any(x=>x.Code=="INVALID_DATE"&&x.Message.StartsWith("V")),p.SourceCode+" product line in invoice date rejected");
 rows=Fixture.Rows(p);rows[p.DataStart][50]=new("10-九月");
 Check(Parse(p,rows).Issues.Any(x=>x.Code=="INVALID_DATE"&&x.Message.StartsWith("AX")),p.SourceCode+" yearless date is not guessed");
 rows=Fixture.Rows(p);rows[p.DataStart][41]=new("#REF!","e");
 bad=Parse(p,rows);Check(bad.Issues.Any(x=>x.Code=="EXCEL_CELL_ERROR")&&bad.Rows[0].Cells.Any(x=>x.CellReference=="AO"+p.DataStart&&x.Text=="#REF!"),p.SourceCode+" Excel error traced");
 rows=Fixture.Rows(p);rows[1][1]=new("Unexpected Header");Check(Parse(p,rows).Issues.Any(x=>x.Code=="HEADER_MISMATCH"),p.SourceCode+" header mismatch blocks");
 rows=Fixture.Rows(p);Check(Parse(p,rows,extension:".xls").Errors>0,p.SourceCode+" renamed xls rejected");
 rows[p.DataStart][30]=new("107883000000","n");good=Parse(p,rows);
 Check(good.Errors==0&&good.Issues.Any(x=>x.Code=="CUSTOMS_IDENTIFIER_REVIEW")&&(string?)good.Rows[0].Line!["FinalDeclarationNumber"]=="107883000000",p.SourceCode+" suspicious declaration preserved");
 rows[p.DataStart][30]=new("1234567890123456","n");Check(Parse(p,rows).Issues.Any(x=>x.Code=="IDENTIFIER_PRECISION"),p.SourceCode+" numeric identifier >15 digits blocked");
 rows=Fixture.Rows(p);rows[p.DataStart][6]=new("2.5099999999999998","n");good=Parse(p,rows);
 Check((decimal?)good.Rows[0].Line!["PoUnitPrice"]==2.5099999999999998m,p.SourceCode+" decimal lexeme exact, no double rounding");
 rows[p.DataStart][6]=new("-2.5","n");good=Parse(p,rows);Check(good.Errors==0&&good.Issues.Any(x=>x.Code=="NEGATIVE_VALUE"),p.SourceCode+" negative retained");
 rows=Fixture.Rows(p);rows[p.DataStart][45]=new("-");rows[p.DataStart][19]=new(null);good=Parse(p,rows);
 Check(good.Errors==0&&good.Rows[0].Line!["EecDate"] is null&&good.Rows[0].Line!["OriginalAmount"] is null,p.SourceCode+" blank/dash is null, never zero");
 rows=Fixture.Rows(p);rows[p.DataStart][7]=new("5000","n");good=Parse(p,rows);
 Check(good.Errors==0&&p.ScopeCode=="SITE_"+p.SiteCode&&(string?)good.Rows[0].Line!["PoLineReference"]=="5000",p.SourceCode+" PO Line is not a Logistics site");
 rows=Fixture.Rows(p);Check(Parse(p,rows,policy with{Start=new(2026,3,1)}).Issues.Count(x=>x.Code=="WRONG_COVERAGE")==2,p.SourceCode+" out-of-range data not silently filtered");
 Check(Parse(p,rows,policy with{MaxRows=1}).Issues.Any(x=>x.Code=="ROW_LIMIT"),p.SourceCode+" truncation cannot become READY");
 rows=Fixture.Rows(p);foreach(var n in rows.Keys.Where(x=>x>=p.DataStart).ToArray())rows.Remove(n);
 Check(Parse(p,rows).Issues.Any(x=>x.Code=="EMPTY_DATA_BATCH"),p.SourceCode+" empty file cannot erase source");
 rows=Fixture.Rows(p);rows[p.DataStart][57]=new("new field");Check(Parse(p,rows).Issues.Any(x=>x.Code=="UNMAPPED_CELL"),p.SourceCode+" extra column retains raw and blocks");
 rows=Fixture.Rows(p);rows[p.DataStart][6]=new("100","n","1+99");Check(Parse(p,rows).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),p.SourceCode+" amount formula is not run");
 rows=Fixture.Rows(p);rows[p.DataStart][14]=new("000123","str","\"000123\"");good=Parse(p,rows);
 Check(good.Errors==0&&good.Rows[0].Cells.Any(x=>x.Formula=="=\"000123\""&&x.CachedValue=="000123"),p.SourceCode+" literal formula and cache preserved");
 rows[p.DataStart][14]=new("wrong","str","\"000123\"");Check(Parse(p,rows).Errors>0,p.SourceCode+" mismatched formula cache blocked");
 try {NkSourcePolicy.Resolve("{\"ingestionMode\":\"SNAPSHOT\"}",new(2026,9,1),new(2026,10,1),p);Check(false,"Missing policy");}
 catch(PoImportFault e){Check(e.Code=="NK_PERIOD_POLICY_REQUIRED",p.SourceCode+" unknown policy blocks upload");}
 var cfg="{\"ingestionMode\":\"SNAPSHOT\",\"logisticsPeriodMode\":\"YEAR_TO_PERIOD\",\"coverageProperty\":\"TransactionDate\",\"dateTextCentury\":2000}";
 var range=NkSourcePolicy.Resolve(cfg,new(2026,9,1),new(2026,10,1),p);
 Check(range.Start==new DateOnly(2026,1,1)&&range.EndExclusive==new DateOnly(2026,10,1),p.SourceCode+" YTD separates submission and coverage");
}
Check(NkFileParser.TryDate("18-一月-26","s",false,out var d,out _)&&d==new DateOnly(2026,1,18),"Chinese short year has explicit century");
Check(NkFileParser.TryDate("17-09-2026","s",false,out d,out _)&&d==new DateOnly(2026,9,17),"Day-month-year dash format");
Check(!NkFileParser.TryDate("60","n",false,out _,out _),"Fictitious Excel leap day blocked");
Check(NkFileParser.TryDate("0","n",true,out d,out _)&&d==new DateOnly(1904,1,1),"1904 epoch supported");
Check(!NkFileParser.TryExactDecimal("0.1234567890123456789",out _),"SQL scale overflow rejected without rounding");
try{NkSourceProfiles.Get("NK_1000;DROP TABLE src.LogisticsNkLine");Check(false,"unknown source");}catch(PoImportFault e){Check(e.Status==404,"source/profile strictly whitelisted");}
var sampleIndex=Array.IndexOf(args,"--write-samples");
if(sampleIndex>=0)
{
 var folder=Path.GetFullPath(args[sampleIndex+1]);Directory.CreateDirectory(folder);
 foreach(var p in NkSourceProfiles.All)
 {
  var rows=Fixture.Rows(p);
  using(var s=Fixture.Write(p,rows))using(var f=File.Create(Path.Combine(folder,p.SourceCode+"_01_valid.xlsx")))s.CopyTo(f);
  rows[p.DataStart][3]=new("31/02/2026");
  using(var s=Fixture.Write(p,rows))using(var f=File.Create(Path.Combine(folder,p.SourceCode+"_02_bad_date.xlsx")))s.CopyTo(f);
 }
}
var real=Array.IndexOf(args,"--real-dir");
if(real>=0)
{
 var folder=Path.GetFullPath(args[real+1]);
 foreach(var (source,name,sha,count) in new[]{
  ("NK_1000","báo cáo NHẬP khẩu 2026_1000.xlsx","e421fda6581f2ad6735094c1ab644955ffaec9e39b367ca89d86a18f62528d41",155),
  ("NK_6000","báo cáo NHẬP khẩu 2026_6000.xlsx","03c68ec2043213669d2d80e3b53eb65ed6fd1e0581c4d4f8353d2c4b18246174",1418)})
 {
  using var stream=File.OpenRead(Path.Combine(folder,name));Check(Convert.ToHexString(SHA256.HashData(stream)).Equals(sha,StringComparison.OrdinalIgnoreCase),source+" exact real-file SHA256");
  stream.Position=0;var parsed=parser.Parse(stream,name,source,policy);
  Check(parsed.DataRows==count&&parsed.Rows.Count==count,source+" every real data row retained");
  string[] errorCells=source=="NK_1000"?["AO142","AP142"]:["AG1202","AH1202"];
  Check(errorCells.All(address=>parsed.Issues.Any(x=>x.Code=="EXCEL_CELL_ERROR"&&x.Message.StartsWith(address+":"))),source+" actual Excel errors blocked");
  Check(parsed.Errors>0,source+" original file INVALID is expected, not a failed parser test");
  if(source=="NK_1000")Check(parsed.Issues.Any(x=>x.Code=="INVALID_DATE"&&x.Message.StartsWith("V"))&&parsed.Issues.Any(x=>x.Code=="CUSTOMS_IDENTIFIER_REVIEW"),"NK1000 misaligned dates and suspected declaration loss surfaced");
  if(source=="NK_6000")Check(parsed.Rows.Where(x=>x.Line is not null).All(x=>x.Line!["EecNumber"] is null),"NK6000 EEC absence not invented");
  Console.WriteLine($"REAL {source}: raw={parsed.Rows.Count}; typed={parsed.Rows.Count(x=>x.Line is not null)}; ERROR={parsed.Errors}; WARNING={parsed.Warnings}");
  Console.WriteLine(JsonSerializer.Serialize(parsed.Issues.GroupBy(x=>new{x.Severity,x.Code}).Select(g=>new{g.Key.Severity,g.Key.Code,Count=g.Count()})));
  var reportIndex=Array.IndexOf(args,"--diagnostics-dir");
  if(reportIndex>=0)
  {
   var reportPath=Path.GetFullPath(args[reportIndex+1]);Directory.CreateDirectory(reportPath);
   File.WriteAllText(Path.Combine(reportPath,source+"_issues.json"),JsonSerializer.Serialize(parsed.Issues,new JsonSerializerOptions{WriteIndented=true}));
  }
 }
}
Console.WriteLine($"Completed {checks} checks. SQL/AD/site authorization/worker recovery still require Windows and SQL Server.");

record Cell(string? Value,string Type="inlineStr",string? Formula=null,string? FormulaKind=null,string? SharedId=null,string? Range=null);
static class Fixture
{
 static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
 public static Dictionary<int,Dictionary<int,Cell>> Rows(NkSourceProfile p)
 {
  var rows=new Dictionary<int,Dictionary<int,Cell>>();
  for(var i=0;i<p.HeaderRows.Length;i++)rows[i+1]=p.HeaderRows[i].Select((h,c)=>(h,c)).Where(x=>x.h!="").ToDictionary(x=>x.c+1,x=>new Cell(x.h));
  for(var i=0;i<2;i++)rows[p.DataStart+i]=p.Fields.ToDictionary(x=>x.Column,x=>x.Kind switch {
    NkFieldKind.Number=>new Cell("100.25","n"),NkFieldKind.Date=>new Cell("12/02/2026"),NkFieldKind.Currency=>new Cell("USD"),
    NkFieldKind.Identifier=>new Cell("000123"),_=>new Cell("SYNTHETIC "+x.Property)});
  return rows;
 }
 static string Label(int c){var t="";for(;c>0;c=(c-1)/26)t=(char)('A'+(c-1)%26)+t;return t;}
 public static MemoryStream Write(NkSourceProfile p,Dictionary<int,Dictionary<int,Cell>> rows)
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
