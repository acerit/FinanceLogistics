using System.IO.Compression;
using System.Text;
using System.Xml;

namespace FinanceLogistics.Api.Assets;

// A single-sheet XLSX. Each new template has its exact headers in row 1;
// Excel users enter business values starting at row 2. No source data embedded.
internal static class HeaderRowOneXlsx
{
    private static void Put(ZipArchive zip,string name,Action<XmlWriter> content)
    {
        var entry=zip.CreateEntry(name,CompressionLevel.Optimal);
        using var stream=entry.Open();
        using var writer=XmlWriter.Create(stream,new XmlWriterSettings {Encoding=new UTF8Encoding(false),CloseOutput=false});
        writer.WriteStartDocument();content(writer);writer.WriteEndDocument();
    }
    private static string Label(int col) {var s="";for(var n=col;n>0;n=(n-1)/26)s=(char)('A'+(n-1)%26)+s;return s;}
    internal static byte[] Build(string sheet,IEnumerable<(int Column,string Header)> headerColumns)
    {
        var fields=headerColumns.OrderBy(x=>x.Column).ToArray();
        if(fields.Length==0||fields.Length>100||fields.Select((f,i)=>f.Column==i+1).Any(ok=>!ok))
            throw new InvalidOperationException("Template column mapping must be contiguous.");
        const string m="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string r="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string pr="http://schemas.openxmlformats.org/package/2006/relationships";
        const string ct="http://schemas.openxmlformats.org/package/2006/content-types";
        using var ms=new MemoryStream();
        using(var zip=new ZipArchive(ms,ZipArchiveMode.Create,true))
        {
            Put(zip,"[Content_Types].xml",w=>{
                w.WriteStartElement("Types",ct);
                w.WriteStartElement("Default",ct);w.WriteAttributeString("Extension","rels");w.WriteAttributeString("ContentType","application/vnd.openxmlformats-package.relationships+xml");w.WriteEndElement();
                w.WriteStartElement("Default",ct);w.WriteAttributeString("Extension","xml");w.WriteAttributeString("ContentType","application/xml");w.WriteEndElement();
                w.WriteStartElement("Override",ct);w.WriteAttributeString("PartName","/xl/workbook.xml");w.WriteAttributeString("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");w.WriteEndElement();
                w.WriteStartElement("Override",ct);w.WriteAttributeString("PartName","/xl/worksheets/sheet1.xml");w.WriteAttributeString("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");w.WriteEndElement();
                w.WriteEndElement();});
            Put(zip,"_rels/.rels",w=>{
                w.WriteStartElement("Relationships",pr);
                w.WriteStartElement("Relationship",pr);w.WriteAttributeString("Id","rId1");w.WriteAttributeString("Type",r+"/officeDocument");w.WriteAttributeString("Target","/xl/workbook.xml");w.WriteEndElement();w.WriteEndElement();});
            Put(zip,"xl/workbook.xml",w=>{
                w.WriteStartElement("workbook",m);w.WriteAttributeString("xmlns","r",null,r);
                w.WriteStartElement("sheets",m);w.WriteStartElement("sheet",m);
                w.WriteAttributeString("name",sheet);w.WriteAttributeString("sheetId","1");w.WriteAttributeString("r","id",r,"rId1");
                w.WriteEndElement();w.WriteEndElement();w.WriteEndElement();});
            Put(zip,"xl/_rels/workbook.xml.rels",w=>{
                w.WriteStartElement("Relationships",pr);w.WriteStartElement("Relationship",pr);w.WriteAttributeString("Id","rId1");
                w.WriteAttributeString("Type",r+"/worksheet");w.WriteAttributeString("Target","/xl/worksheets/sheet1.xml");
                w.WriteEndElement();w.WriteEndElement();});
            Put(zip,"xl/worksheets/sheet1.xml",w=>{
                w.WriteStartElement("worksheet",m);w.WriteStartElement("sheetData",m);
                w.WriteStartElement("row",m);w.WriteAttributeString("r","1");
                foreach(var field in fields)
                {
                    w.WriteStartElement("c",m);w.WriteAttributeString("r",Label(field.Column)+"1");w.WriteAttributeString("t","inlineStr");
                    w.WriteStartElement("is",m);w.WriteStartElement("t",m);
                    w.WriteAttributeString("xml","space","http://www.w3.org/XML/1998/namespace","preserve");
                    w.WriteString(field.Header);
                    w.WriteEndElement();w.WriteEndElement();w.WriteEndElement();
                }
                w.WriteEndElement();w.WriteEndElement();w.WriteEndElement();});
        }
        return ms.ToArray();
    }
}
