using System.IO.Compression;
using System.Xml.Linq;
using FinanceLogistics.Infrastructure.LogisticsTrade;
record Cell(string? Value,string Type="inlineStr",string? Formula=null,string? FormulaKind=null,string? SharedId=null,string? Range=null);
static class Fixture
{
 static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
 public static Dictionary<int,Dictionary<int,Cell>> Rows(TradeSourceProfile p)
 {
  var rows=new Dictionary<int,Dictionary<int,Cell>>();
  for(var i=0;i<p.HeaderRows.Length;i++)rows[i+1]=p.HeaderRows[i].Select((h,c)=>(h,c)).Where(x=>x.h!="").ToDictionary(x=>x.c+1,x=>new Cell(x.h));
  for(var i=0;i<2;i++)rows[p.DataStart+i]=p.Fields.ToDictionary(x=>x.Column,x=>x.Kind switch {
    TradeFieldKind.Number=>new Cell("100.25","n"),TradeFieldKind.Date=>new Cell("12/02/2026"),TradeFieldKind.Currency=>new Cell("USD"),
    TradeFieldKind.Boolean=>new Cell("0","b"),TradeFieldKind.Identifier=>new Cell("000123"),_=>new Cell("SYNTHETIC "+x.Property)});
  return rows;
 }
 static string Label(int c){var t="";for(;c>0;c=(c-1)/26)t=(char)('A'+(c-1)%26)+t;return t;}
 public static MemoryStream Write(TradeSourceProfile p,Dictionary<int,Dictionary<int,Cell>> rows,string[]? merges=null,bool external=false,bool date1904=false)
 {
  var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
  {
   void Add(string path,string text){using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(text);}
   Add("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
   Add("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
   Add("xl/workbook.xml",$"<workbook xmlns=\"{S}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><workbookPr date1904=\"{(date1904?1:0)}\"/><sheets><sheet name=\"{p.Sheet}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
   Add("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
   var sheet=new XElement(S+"worksheet",new XElement(S+"sheetData",rows.OrderBy(x=>x.Key).Select(row=>new XElement(S+"row",new XAttribute("r",row.Key),row.Value.OrderBy(x=>x.Key).Select(pair=>{
    var c=pair.Value;var cell=new XElement(S+"c",new XAttribute("r",Label(pair.Key)+row.Key),new XAttribute("t",c.Type));
    if(c.Formula is not null){var f=new XElement(S+"f",c.Formula);if(c.FormulaKind is not null)f.Add(new XAttribute("t",c.FormulaKind));if(c.SharedId is not null)f.Add(new XAttribute("si",c.SharedId));if(c.Range is not null)f.Add(new XAttribute("ref",c.Range));cell.Add(f);}
    if(c.Value is not null)cell.Add(c.Type=="inlineStr"?new XElement(S+"is",new XElement(S+"t",c.Value)):new XElement(S+"v",c.Value));return cell;
   })))));
   if(merges is not null)sheet.Add(new XElement(S+"mergeCells",merges.Select(m=>new XElement(S+"mergeCell",new XAttribute("ref",m)))));
   if(external)Add("xl/externalLinks/externalLink1.xml","<externalLink xmlns=\""+S+"\"/>");
   Add("xl/worksheets/sheet1.xml",sheet.ToString(SaveOptions.DisableFormatting));
  }stream.Position=0;return stream;
 }
}
