using System.IO.Compression;
using System.Text;
using System.Xml;
using FinanceLogistics.Infrastructure.LogisticsTrade;
namespace FinanceLogistics.Api.Assets;

// Generates only the selected source/site-specific, versioned blank workbook.
// No Finance/Logistics transaction data are embedded in the template.
internal static class RealV3BlankTemplate
{
    private static void Put(ZipArchive zip,string name,Action<XmlWriter> write)
    {
        var entry=zip.CreateEntry(name,CompressionLevel.Fastest);
        using var stream=entry.Open();
        using var x=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false});
        x.WriteStartDocument();write(x);x.WriteEndDocument();
    }
    private static string Label(int column){var s="";for(var n=column;n>0;n=(n-1)/26)s=(char)('A'+(n-1)%26)+s;return s;}
    public static byte[] Build(TradeSourceProfile profile)
    {
        if(profile.SchemaVersion!=3)throw new InvalidOperationException("Requires approved RealInput V3 profile.");
        const string s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string r="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string pr="http://schemas.openxmlformats.org/package/2006/relationships";
        const string ct="http://schemas.openxmlformats.org/package/2006/content-types";
        using var mem=new MemoryStream();
        using(var zip=new ZipArchive(mem,ZipArchiveMode.Create,true))
        {
            Put(zip,"[Content_Types].xml",x=>{
                x.WriteStartElement("Types",ct);
                x.WriteStartElement("Default",ct);x.WriteAttributeString("Extension","rels");x.WriteAttributeString("ContentType","application/vnd.openxmlformats-package.relationships+xml");x.WriteEndElement();
                x.WriteStartElement("Default",ct);x.WriteAttributeString("Extension","xml");x.WriteAttributeString("ContentType","application/xml");x.WriteEndElement();
                x.WriteStartElement("Override",ct);x.WriteAttributeString("PartName","/xl/workbook.xml");x.WriteAttributeString("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");x.WriteEndElement();
                x.WriteStartElement("Override",ct);x.WriteAttributeString("PartName","/xl/worksheets/sheet1.xml");x.WriteAttributeString("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");x.WriteEndElement();
                x.WriteEndElement();});
            Put(zip,"_rels/.rels",x=>{
                x.WriteStartElement("Relationships",pr);
                x.WriteStartElement("Relationship",pr);x.WriteAttributeString("Id","rId1");x.WriteAttributeString("Type",r+"/officeDocument");x.WriteAttributeString("Target","/xl/workbook.xml");x.WriteEndElement();x.WriteEndElement();});
            Put(zip,"xl/workbook.xml",x=>{
                x.WriteStartElement("workbook",s);x.WriteAttributeString("xmlns","r",null,r);
                x.WriteStartElement("sheets",s);x.WriteStartElement("sheet",s);x.WriteAttributeString("name",profile.Sheet);x.WriteAttributeString("sheetId","1");x.WriteAttributeString("r","id",r,"rId1");x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();});
            Put(zip,"xl/_rels/workbook.xml.rels",x=>{
                x.WriteStartElement("Relationships",pr);x.WriteStartElement("Relationship",pr);
                x.WriteAttributeString("Id","rId1");x.WriteAttributeString("Type",r+"/worksheet");x.WriteAttributeString("Target","/xl/worksheets/sheet1.xml");x.WriteEndElement();x.WriteEndElement();});
            Put(zip,"xl/worksheets/sheet1.xml",x=>{
                x.WriteStartElement("worksheet",s);x.WriteStartElement("sheetData",s);
                x.WriteStartElement("row",s);x.WriteAttributeString("r",profile.HeaderRow.ToString());
                foreach(var field in profile.Fields.OrderBy(v=>v.Column))
                {
                    x.WriteStartElement("c",s);x.WriteAttributeString("r",Label(field.Column)+profile.HeaderRow);x.WriteAttributeString("t","inlineStr");
                    x.WriteStartElement("is",s);x.WriteStartElement("t",s);x.WriteAttributeString("xml","space","http://www.w3.org/XML/1998/namespace","preserve");x.WriteString(field.Header);
                    x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();
                }
                x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();});
        }
        return mem.ToArray();
    }
}
