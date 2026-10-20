using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.FinanceSources;

internal sealed record FinanceCell(PoCellDto Raw, string? EffectiveFormula, bool FormulaSupported, string? Value, string Type);
internal sealed record FinanceWorkbook(bool Date1904, SortedDictionary<int, Dictionary<int, FinanceCell>> Rows);

// Reads XLSX values and formula caches only. Never runs Excel, macros, external links or formulas.
internal static class FinanceXlsxReader
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private sealed record LiteralGroup(string Formula, int Column, int First, int Last, string Origin);
    public static string Label(int column) { var text=""; for(var n=column;n>0;n=(n-1)/26)text=(char)('A'+(n-1)%26)+text; return text; }
    public static string Key(string value) => Regex.Replace(value.Trim().TrimStart('\uFEFF'), @"\s+", " ").ToUpperInvariant();
    internal static string? Literal(string formula) { var m=Regex.Match(formula,"^\"((?:[^\"]|\"\")*)\"$"); return m.Success?m.Groups[1].Value.Replace("\"\"","\""):null; }
    private static XDocument Xml(ZipArchive zip,string path)
    {
        using var input=(zip.GetEntry(path)??throw new InvalidDataException("Missing XLSX part.")).Open();
        using var xml=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=50L*1024*1024});
        return XDocument.Load(xml);
    }
    private static string Part(string target)
    {
        if(string.IsNullOrWhiteSpace(target)||target.IndexOfAny(['\\',':','?','#','%'])>=0)throw new InvalidDataException("Invalid relationship.");
        var parts=new List<string>();
        foreach(var p in (target.StartsWith('/')?target[1..]:"xl/"+target).Split('/'))
        { if(p is "" or ".")continue; if(p==".."){if(parts.Count==0)throw new InvalidDataException();parts.RemoveAt(parts.Count-1);}else parts.Add(p); }
        var result=string.Join('/',parts);if(!result.StartsWith("xl/",StringComparison.Ordinal))throw new InvalidDataException();return result;
    }
    private static string Rich(XElement? e)=>e is null?"":string.Concat(e.Descendants(S+"t").Where(x=>!x.Ancestors(S+"rPh").Any()).Select(x=>x.Value));
    private static string? Value(XElement e,string[] strings)
    {
        var v=e.Element(S+"v")?.Value;
        return (string?)e.Attribute("t") switch {
            "inlineStr"=>Rich(e.Element(S+"is")),
            "s"=>int.TryParse(v,NumberStyles.None,CultureInfo.InvariantCulture,out var i)&&i>=0&&i<strings.Length?strings[i]:throw new InvalidDataException("Bad shared string."),
            _=>v };
    }
    private static int Column(string address,int row)
    {
        var m=Regex.Match(address,@"^([A-Z]{1,3})([1-9][0-9]{0,6})$");
        if(!m.Success||!int.TryParse(m.Groups[2].Value,out var r)||r!=row)throw new InvalidDataException("Invalid cell coordinate.");
        var col=0;foreach(var ch in m.Groups[1].Value)col=col*26+ch-'A'+1;
        if(col>100)throw new InvalidDataException("Column limit 100.");return col;
    }
    public static FinanceWorkbook Read(Stream input,FinanceSourceProfile profile,CancellationToken ct)
    {
        if(!input.CanSeek||input.Length<8)throw new InvalidDataException("Invalid XLSX stream.");
        input.Position=0;var signature=new byte[4];input.ReadExactly(signature);input.Position=0;
        if(!signature.SequenceEqual(new byte[]{0x50,0x4B,3,4}))throw new InvalidDataException("XLSX signature mismatch; no renamed/encrypted files.");
        using var zip=new ZipArchive(input,ZipArchiveMode.Read,true);
        if(zip.Entries.Count>2000||zip.Entries.Sum(x=>x.Length)>50L*1024*1024||zip.Entries.Select(x=>x.FullName).Distinct().Count()!=zip.Entries.Count)
            throw new InvalidDataException("XLSX expansion/duplicate-part limit.");
        var book=Xml(zip,"xl/workbook.xml");var sheets=book.Root?.Element(S+"sheets")?.Elements(S+"sheet").ToArray()??[];
        if(sheets.Length!=1||(string?)sheets[0].Attribute("name")!=profile.Sheet)throw new InvalidDataException($"Expected one sheet named {profile.Sheet}.");
        var links=Xml(zip,"xl/_rels/workbook.xml.rels").Root?.Elements(P+"Relationship").ToArray()??[];
        var selected=links.Where(x=>(string?)x.Attribute("Id")== (string?)sheets[0].Attribute(R+"id")).ToArray();
        if(selected.Length!=1||(string?)selected[0].Attribute("TargetMode")=="External"||(string?)selected[0].Attribute("Type")!=R.NamespaceName+"/worksheet")throw new InvalidDataException();
        var sheet=Xml(zip,Part((string?)selected[0].Attribute("Target")??""));
        var shared=links.Where(x=>(string?)x.Attribute("Type")==R.NamespaceName+"/sharedStrings").ToArray();
        if(shared.Length>1||shared.Any(x=>(string?)x.Attribute("TargetMode")=="External"))throw new InvalidDataException();
        var strings=shared.Length==0?[]:Xml(zip,Part((string?)shared[0].Attribute("Target")??"")).Root?.Elements(S+"si").Select(Rich).ToArray()??[];
        var epoch=(string?)book.Root?.Element(S+"workbookPr")?.Attribute("date1904");
        if(epoch is not (null or "0" or "1" or "false" or "true"))throw new InvalidDataException();
        var xmlRows=new SortedDictionary<int,Dictionary<int,XElement>>();var cellsCount=0;
        foreach(var row in sheet.Root?.Element(S+"sheetData")?.Elements(S+"row")??[])
        {
            ct.ThrowIfCancellationRequested();
            if(!int.TryParse((string?)row.Attribute("r"),out var r)||r<1||r>1048576||xmlRows.ContainsKey(r))throw new InvalidDataException("Invalid/repeated row.");
            var cells=new Dictionary<int,XElement>();
            foreach(var cell in row.Elements(S+"c"))
            { if(!cells.TryAdd(Column((string?)cell.Attribute("r")??"",r),cell)||++cellsCount>1_000_000)throw new InvalidDataException("Repeated cell/cell limit."); }
            xmlRows.Add(r,cells);
            if(xmlRows.Count>30_000)throw new InvalidDataException("Physical row limit 30000.");
        }
        var groups=new Dictionary<string,LiteralGroup>(StringComparer.Ordinal);
        foreach(var (r,cells) in xmlRows)foreach(var (col,cell) in cells)
        {
            var f=cell.Element(S+"f");if(f is null||(string?)f.Attribute("t")!="shared"||string.IsNullOrEmpty(f.Value))continue;
            var id=(string?)f.Attribute("si");var range=Regex.Match((string?)f.Attribute("ref")??"",@"^([A-Z]+)([1-9][0-9]*):([A-Z]+)([1-9][0-9]*)$");
            if(id is null||!Regex.IsMatch(id,@"^[0-9]{1,9}$")||!range.Success||Literal(f.Value) is null||f.Attributes().Any(x=>x.Name.LocalName is not ("t" or "si" or "ref"))
                ||range.Groups[1].Value!=Label(col)||range.Groups[3].Value!=Label(col)||!int.TryParse(range.Groups[2].Value,out var first)||!int.TryParse(range.Groups[4].Value,out var last)
                ||first!=r||last<first||last>1048576)continue;
            if(!groups.TryAdd(id,new(f.Value,col,first,last,Label(col)+r)))throw new InvalidDataException("Repeated shared formula group.");
        }
        var rows=new SortedDictionary<int,Dictionary<int,FinanceCell>>();long chars=0;
        foreach(var (r,source) in xmlRows)
        {
            ct.ThrowIfCancellationRequested();var cells=new Dictionary<int,FinanceCell>();
            foreach(var (col,cell) in source)
            {
                var value=Value(cell,strings);var f=cell.Element(S+"f");var rawFormula=f?.Value;var formula=rawFormula;var kind=(string?)f?.Attribute("t");var id=(string?)f?.Attribute("si");
                var address=Label(col)+r;var origin=f is null?null:address;var supported=f is null||!f.HasAttributes;
                if(kind=="shared"&&id is not null&&groups.TryGetValue(id,out var g)&&g.Column==col&&r>=g.First&&r<=g.Last
                    && !f!.Attributes().Any(x=>x.Name.LocalName is not ("t" or "si" or "ref"))
                    &&(r==g.First||rawFormula==""&&f.Attribute("ref")==null))
                {formula=g.Formula;origin=g.Origin;supported=true;}
                chars+=(value?.Length??0)+(formula?.Length??0);
                if(chars>8_000_000||(value?.Length??0)>32767||(formula?.Length??0)>8192)throw new InvalidDataException("Cell/text limit.");
                var type=(string?)cell.Attribute("t")??"n";
                var header=profile.Fields.FirstOrDefault(x=>x.Column==col)?.Header??$"Extra {Label(col)}";
                cells[col]=new(new(col,header,value,f is not null?"Formula":value is null?"Empty":type,address,
                    f is null?null:"="+rawFormula,f is null?null:cell.Element(S+"v")?.Value,origin,f is null?null:kind??"normal",id),formula,supported,value,type);
            }
            rows.Add(r,cells);
        }
        return new(epoch is "1" or "true",rows);
    }
}
