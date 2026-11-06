using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.LogisticsNk;

public sealed record NkParsePolicy(string PeriodMode,DateOnly Start,DateOnly EndExclusive,bool AllowCachedAmountFormulas=false,int MaxRows=10000);
public sealed record ParsedNkRow(int RecordNumber,string SheetName,PoCellDto[] Cells,string Kind,Dictionary<string,object?>? Line);
public sealed class ParsedNkFile
{
    public List<ParsedNkRow> Rows {get;}=[];
    public List<ParsedPoIssue> Issues {get;}=[];
    public int DataRows=>Rows.Count;
    public int Errors=>Issues.Count(x=>x.Severity=="ERROR");
    public int Warnings=>Issues.Count(x=>x.Severity=="WARNING");
}
public sealed class NkFileParser
{
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public ParsedNkFile Parse(Stream stream,string fileName,string sourceCode,NkParsePolicy policy,CancellationToken ct=default,string? profileCode=null)
    {
        var p=profileCode is null?NkSourceProfiles.Get(sourceCode):NkSourceProfiles.Get(sourceCode,profileCode);var result=new ParsedNkFile();
        try
        {
            if(Path.GetExtension(fileName).ToLowerInvariant()!=".xlsx")throw new InvalidDataException("Native XLSX required.");
            if(!stream.CanSeek||stream.Length>NkSourceProfiles.MaxBytes||policy.MaxRows<1||policy.MaxRows>NkSourceProfiles.MaxRows
                ||policy.EndExclusive<=policy.Start||policy.EndExclusive.DayNumber-policy.Start.DayNumber>3660||policy.AllowCachedAmountFormulas!=p.UsesCachedValues
                ||policy.PeriodMode is not ("SELECTED_MONTH" or "YEAR_TO_PERIOD" or "EXPLICIT_RANGE"))throw new InvalidDataException("Invalid NK policy.");
            var book=NkXlsxReader.Read(stream,p,ct);
            if(!CheckHeaders(book,p,result))return result;
            if(book.HasExternalLinks)result.Issues.Add(new("WARNING","EXTERNAL_LINKS_NOT_REFRESHED",null,"Workbook có liên kết ngoài. Hệ thống chỉ đọc cache trong file, không tải liên kết hay xác nhận cache đã cập nhật."));
            if(book.HeaderMergeCount>0)result.Issues.Add(new("INFO","HEADER_MERGES_SUPPORTED",null,$"Đọc {book.HeaderMergeCount} vùng gộp dọc trong tiêu đề đã biết; không gộp hoặc điền xuống dữ liệu."));
            var seen=new Dictionary<string,int>(StringComparer.Ordinal);var placeholders=0;
            foreach(var (record,cells) in book.Rows.Where(x=>x.Key>=p.DataStart))
            {
                ct.ThrowIfCancellationRequested();
                if(cells.Values.All(x=>string.IsNullOrWhiteSpace(x.Value)&&x.Raw.Formula is null))continue;
                if(result.Rows.Count>=policy.MaxRows){result.Issues.Add(new("ERROR","ROW_LIMIT",null,"Vượt giới hạn dòng; không được xác nhận file đọc dở."));break;}
                var before=result.Errors;var values=new Dictionary<string,object?>(StringComparer.Ordinal);
                void Issue(string level,string code,NkField? f,string text)=>result.Issues.Add(new(level,code,f?.Header,text,record,p.Sheet));
                foreach(var x in cells.Where(x=>x.Key>p.Width&&(!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null)))
                    Issue("ERROR","UNMAPPED_CELL",null,$"{x.Value.Raw.CellReference}: Có dữ liệu ngoài số cột của profile; giữ raw, cần phiên bản mẫu được xác nhận.");
                foreach(var f in p.Fields)
                {
                    cells.TryGetValue(f.Column,out var c);var address=NkXlsxReader.Label(f.Column)+record;
                    void E(string code,string message)=>Issue("ERROR",code,f,address+": "+message);
                    void W(string code,string message)=>Issue("WARNING",code,f,address+": "+message);
                    var text=c?.Value;var type=c?.Type;
                    if(type=="e"){E("EXCEL_CELL_ERROR","Ô Excel có lỗi; sửa tại nguồn và nộp bản mới.");values[f.Property]=null;continue;}
                    if(c?.Raw.Formula is not null)
                    {
                        // V2 reads the saved cache; it never evaluates or refreshes formulas/links.
                        var literal=c.FormulaSupported?NkXlsxReader.Literal(c.EffectiveFormula??""):null;
                        if(p.UsesCachedValues)
                        {
                            if(!c.FormulaSupported||string.IsNullOrWhiteSpace(c.EffectiveFormula))
                            {E("FORMULA_UNSUPPORTED","Cấu trúc công thức không được hỗ trợ hoặc thiếu shared-formula master.");values[f.Property]=null;continue;}
                            if(c.Raw.CachedValue is null || (f.Kind is NkFieldKind.Number or NkFieldKind.Date)&&string.IsNullOrWhiteSpace(text))
                            {E("FORMULA_CACHE_MISSING","Thiếu kết quả công thức đã lưu. Logistics cần kiểm tra/tính lại và lưu file từ nguồn.");values[f.Property]=null;continue;}
                            if(literal is not null&&(type!="str"||literal!=text))
                            {E("FORMULA_CACHE_MISMATCH","Công thức chuỗi nguyên văn không khớp cache.");values[f.Property]=null;continue;}
                            W("FORMULA_CACHED_VALUE","Đọc kết quả công thức đã lưu, không tính lại; cần chủ nguồn kiểm tra độ mới của cache.");
                        }
                        else if(literal is null||f.Kind is NkFieldKind.Number or NkFieldKind.Date||type!="str"||literal!=text||c.Raw.CachedValue is null)
                        {E("FORMULA_UNSUPPORTED","Mẫu NK V1 không tính công thức; chỉ chấp nhận công thức chuỗi nguyên văn có cache khớp.");values[f.Property]=null;continue;}
                    }
                    if(string.IsNullOrWhiteSpace(text))
                    {values[f.Property]=null;if(f.Required)E("REQUIRED","Thiếu ngày giao dịch dùng để kiểm tra phạm vi.");continue;}
                    if(text.Trim()=="-"&&f.Kind is not NkFieldKind.Text)
                    {values[f.Property]=null;placeholders++;if(f.Required)E("REQUIRED","Dấu '-' không thay cho ngày bắt buộc.");continue;}
                    if(type is not ("n" or "s" or "str" or "inlineStr" or "d"))
                    {values[f.Property]=null;E("CELL_TYPE_UNSUPPORTED","Kiểu ô không thuộc mẫu; không tự đổi Boolean thành số.");continue;}
                    switch(f.Kind)
                    {
                        case NkFieldKind.Number:
                            if(!TryExactDecimal(text,out var n)){E("DECIMAL_PRECISION","Số phải giữ được chính xác trong decimal, tối đa 20 số nguyên/18 số lẻ; không tự làm tròn hoặc bỏ dấu phân cách.");values[f.Property]=null;}
                            else values[f.Property]=n;
                            break;
                        case NkFieldKind.Date:
                            if(!TryDateForProfile(text,type!,book.Date1904,p.UsesCachedValues,out var date,out var time)){E("INVALID_DATE","Cần ngày Excel, yyyy-MM-dd, d/M/yyyy, d-M-yyyy hoặc d-tháng tiếng Hoa-yy/yyyy. Năm hai chữ số dùng 2000–2099; không đoán năm bị thiếu.");values[f.Property]=null;}
                            else {values[f.Property]=date;if(time)W("DATE_TIME_PART","Dữ liệu chuẩn giữ phần ngày; raw giữ giá trị có giờ.");}
                            break;
                        default:
                            var normalized=text.Trim();
                            if(f.Kind==NkFieldKind.Identifier&&type=="n")
                            {
                                normalized=Expand(text);
                                W("NUMERIC_IDENTIFIER","Mã được lưu Number; giữ giá trị nguồn, không tự thêm số 0 đầu hoặc phục hồi chữ số đã mất.");
                                if(!Regex.IsMatch(normalized,@"^[0-9]+(?:\.0+)?$")||normalized.Split('.')[0].TrimStart('0').Length>15)
                                    E("IDENTIFIER_PRECISION","Mã số không nguyên hoặc dài quá 15 chữ số; xuất lại dạng Text từ nguồn.");
                                if(normalized.Contains('.'))normalized=normalized.TrimEnd('0').TrimEnd('.');
                                if(f.Property is "DeclarationNumber" or "FinalDeclarationNumber" && normalized.Length==12&&normalized.EndsWith("00000",StringComparison.Ordinal))
                                    W("CUSTOMS_IDENTIFIER_REVIEW","Mã tờ khai 12 chữ số kết thúc bằng 00000; đối chiếu chứng từ. Đây là dấu hiệu cần rà soát, chưa kết luận sai.");
                            }
                            if(f.Kind==NkFieldKind.Currency){normalized=normalized.ToUpperInvariant();if(!Regex.IsMatch(normalized,"^[A-Z]{3}$"))E("CURRENCY_FORMAT","Cần mã tiền tệ ba chữ cái.");}
                            if(normalized.Length>f.MaxLength)E("TEXT_TOO_LONG",$"Tối đa {f.MaxLength} ký tự; không tự cắt nội dung.");
                            values[f.Property]=normalized;break;
                    }
                }
                if(values.GetValueOrDefault(p.CoverageProperty) is DateOnly day&&(day<policy.Start||day>=policy.EndExclusive))
                    Issue("ERROR","WRONG_COVERAGE",p.Fields[2],$"C{record}: Trx Date nằm ngoài [{policy.Start:yyyy-MM-dd}, {policy.EndExclusive:yyyy-MM-dd}); giữ raw, không tự lọc bỏ dòng.");
                if(values.Values.OfType<decimal>().Any(x=>x<0))Issue("WARNING","NEGATIVE_VALUE",null,"Có số âm; giữ nguyên, chưa áp quy tắc báo cáo.");
                if(values.GetValueOrDefault("DeclarationNumber") is null&&values.GetValueOrDefault("FinalDeclarationNumber") is null)
                {
                    var declaration=p.Fields.First(f=>f.Property=="DeclarationNumber");
                    var final=p.Fields.First(f=>f.Property=="FinalDeclarationNumber");
                    Issue("WARNING","DECLARATION_MISSING",declaration,
                        $"{NkXlsxReader.Label(declaration.Column)}{record}/{NkXlsxReader.Label(final.Column)}{record}: Chưa có mã tờ khai; giữ dòng để bổ sung bằng snapshot mới.");
                }
                var sig=PoProfile.Serialize(p.Fields.Select(f=>values.GetValueOrDefault(f.Property)).ToArray());
                if(seen.TryGetValue(sig,out var previous))Issue("WARNING","DUPLICATE_ROW",null,$"Dữ liệu chuẩn giống dòng {previous}; vẫn giữ cả hai, không tự khử trùng.");else seen.Add(sig,record);
                result.Rows.Add(new(record,p.Sheet,cells.OrderBy(x=>x.Key).Select(x=>x.Value.Raw).ToArray(),"DATA",result.Errors==before?values:null));
            }
            if(placeholders>0)result.Issues.Add(new("INFO","PLACEHOLDER_AS_EMPTY",null,$"{placeholders} dấu '-' ở cột mã/ngày/số được biểu diễn null; raw giữ nguyên. Không chuyển thành số 0."));
        }
        catch(OperationCanceledException){throw;}
        catch(Exception e) when(e is InvalidDataException or XmlException or FormatException or OverflowException or ArgumentOutOfRangeException)
        {result.Issues.Add(new("ERROR","FILE_LAYOUT_INVALID",null,"File không đúng XLSX/profile NK đã chọn: kiểm tra sheet, tiêu đề, ô gộp, macro, mã hóa và giới hạn dung lượng."));}
        if(result.DataRows==0&&result.Errors==0)result.Issues.Add(new("ERROR","EMPTY_DATA_BATCH",null,"File rỗng không được thay nguồn. Quy trình Không phát sinh triển khai riêng."));
        return result;
    }
    private static bool CheckHeaders(NkWorkbook book,NkSourceProfile p,ParsedNkFile result)
    {
        for(var i=0;i<p.HeaderRows.Length;i++)
        {
            book.Rows.TryGetValue(i+1,out var row);
            for(var col=1;col<=p.Width;col++)
            {
                NkCell? cell=null;row?.TryGetValue(col,out cell);
                var expected=p.HeaderRows[i][col-1];
                if(book.CoveredHeaderCells.Contains(NkXlsxReader.Label(col)+(i+1)) && string.IsNullOrWhiteSpace(cell?.Value) && cell?.Raw.Formula is null && cell?.Type!="e")continue;
                if(cell?.Raw.Formula is not null||cell?.Type=="e"||NkXlsxReader.Key(cell?.Value??"")!=NkXlsxReader.Key(expected))
                    result.Issues.Add(new("ERROR","HEADER_MISMATCH",p.Fields[col-1].Header,$"{NkXlsxReader.Label(col)}{i+1}: Cần tiêu đề '{expected}'; không tự dịch cột."));
            }
            if(row?.Any(x=>x.Key>p.Width&&(!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null))==true)
                result.Issues.Add(new("ERROR","EXTRA_HEADER",null,$"Hàng {i+1}: Có tiêu đề ngoài số cột của profile."));
        }
        return result.Errors==0;
    }
    public static bool TryDateForProfile(string text,string type,bool date1904,bool v2,out DateOnly date,out bool hasTime)
    {
        if(TryDate(text,type,date1904,out date,out hasTime))return true;
        if(!v2 || type is not ("s" or "str" or "inlineStr"))return false;
        var m=Regex.Match(text.Trim(),@"^([0-9]{1,2})-([A-Za-z]{3})-([0-9]{2}|[0-9]{4})$");
        if(!m.Success)return false;
        var y=m.Groups[3].Value;if(y.Length==2)y="20"+y;
        return DateOnly.TryParseExact(m.Groups[1].Value+"-"+m.Groups[2].Value+"-"+y,"d-MMM-yyyy",Inv,DateTimeStyles.None,out date);
    }
    public static bool TryDate(string text,string type,bool date1904,out DateOnly date,out bool hasTime)
    {
        date=default;hasTime=false;
        if(type=="n")
        {
            if(!decimal.TryParse(text,NumberStyles.Float,Inv,out var serial)||serial<0||serial>2_900_000||!date1904&&serial>=60&&serial<61)return false;
            var days=(int)decimal.Truncate(serial);hasTime=serial!=days;
            try {date=date1904?new DateOnly(1904,1,1).AddDays(days):new DateOnly(1899,12,31).AddDays(days>=61?days-1:days);return true;}
            catch(ArgumentOutOfRangeException){return false;}
        }
        text=text.Trim();
        if(DateOnly.TryParseExact(text,["yyyy-MM-dd","d/M/yyyy","d-M-yyyy"],Inv,DateTimeStyles.None,out date))return true;
        if(type=="d"&&DateTime.TryParseExact(text,["yyyy-MM-dd'T'HH:mm:ss","yyyy-MM-dd'T'HH:mm:ss.FFFFFFF","yyyy-MM-dd'T'HH:mm:ssK","yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],Inv,DateTimeStyles.RoundtripKind,out var dt))
        {date=DateOnly.FromDateTime(dt);hasTime=dt.TimeOfDay!=TimeSpan.Zero;return true;}
        var m=Regex.Match(text,@"^([0-9]{1,2})-(一|二|三|四|五|六|七|八|九|十|十一|十二)月-([0-9]{2}|[0-9]{4})$");
        if(!m.Success)return false;
        string[] months=["一","二","三","四","五","六","七","八","九","十","十一","十二"];
        var y=int.Parse(m.Groups[3].Value,Inv);if(m.Groups[3].Length==2)y+=2000;
        try {date=new(y,Array.IndexOf(months,m.Groups[2].Value)+1,int.Parse(m.Groups[1].Value,Inv));return true;}
        catch(ArgumentOutOfRangeException){return false;}
    }

    internal static string Expand(string value)
    {
        var m=Regex.Match(value.Trim(),@"^([+-]?)([0-9]+)(?:\.([0-9]+))?(?:[Ee]([+-]?[0-9]{1,3}))?$");
        if(!m.Success)return value;var exponent=m.Groups[4].Success?int.Parse(m.Groups[4].Value,Inv):0;
        if(Math.Abs(exponent)>100)return "NUMBER_OUT_OF_RANGE";
        var digits=m.Groups[2].Value+m.Groups[3].Value;var scale=m.Groups[3].Length-exponent;
        return m.Groups[1].Value+(scale<=0?digits+new string('0',-scale):scale>=digits.Length?"0."+new string('0',scale-digits.Length)+digits:digits.Insert(digits.Length-scale,"."));
    }
    private static (BigInteger,int)? Exact(string value)
    {
        var m=Regex.Match(value,@"^([+-]?)([0-9]+)(?:\.([0-9]+))?$");if(!m.Success)return null;
        var digits=m.Groups[2].Value+m.Groups[3].Value;var n=BigInteger.Parse(digits,Inv);if(m.Groups[1].Value=="-")n=-n;return(n,m.Groups[3].Length);
    }
    public static bool TryExactDecimal(string? value,out decimal number)
    {
        number=0;if(value is null||value.Length>150)return false;var text=Expand(value);var parts=text.TrimStart('+','-').Split('.');
        if(parts[0].TrimStart('0').Length>20||parts.Length>2||parts.Length==2&&parts[1].TrimEnd('0').Length>18)return false;
        var exact=Exact(text);if(exact is null||!decimal.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,Inv,out number))return false;
        var actual=Exact(number.ToString(Inv))!.Value;
        return exact.Value.Item1*BigInteger.Pow(10,actual.Item2)==actual.Item1*BigInteger.Pow(10,exact.Value.Item2);
    }
}
