using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;

namespace FinanceLogistics.Infrastructure.FinanceSources;
public sealed record FinanceParsePolicy(string PeriodMode, DateOnly Start, DateOnly EndExclusive, bool AllowCachedAmountFormulas, int MaxRows=10000);
public sealed record ParsedFinanceRow(int RecordNumber,string SheetName,PoCellDto[] Cells,string Kind,Dictionary<string,object?>? Line);
public sealed class ParsedFinanceFile
{
    public List<ParsedFinanceRow> Rows {get;}=[];
    public List<ParsedPoIssue> Issues {get;}=[];
    public int DataRows=>Rows.Count(x=>x.Kind=="DATA");
    public int Errors=>Issues.Count(x=>x.Severity=="ERROR");
    public int Warnings=>Issues.Count(x=>x.Severity=="WARNING");
}
public sealed class FinanceFileParser
{
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public ParsedFinanceFile Parse(Stream stream,string fileName,string sourceCode,FinanceParsePolicy policy,CancellationToken ct=default)
    {
        var p=FinanceSourceProfiles.Get(sourceCode);var result=new ParsedFinanceFile();
        try
        {
            if(Path.GetExtension(fileName).ToLowerInvariant()!=".xlsx")throw new InvalidDataException("Profile requires native XLSX.");
            if(policy.MaxRows<1||policy.MaxRows>FinanceSourceProfiles.MaxRows||policy.EndExclusive<=policy.Start
                ||policy.PeriodMode is not ("SELECTED_MONTH" or "YEAR_TO_PERIOD" or "EXPLICIT_RANGE"))throw new InvalidDataException("Invalid parsing policy.");
            var book=FinanceXlsxReader.Read(stream,p,ct);
            if(!CheckHeaders(book,p,result))return result;
            var seen=new Dictionary<string,int>(StringComparer.Ordinal);var ended=false;var literals=0;var subtotals=0;
            foreach(var (record,cells) in book.Rows.Where(x=>x.Key>=p.DataStart))
            {
                ct.ThrowIfCancellationRequested();
                if(cells.Values.All(x=>string.IsNullOrWhiteSpace(x.Value)&&x.Raw.Formula is null))continue;
                var raw=cells.OrderBy(x=>x.Key).Select(x=>x.Value.Raw).ToArray();
                string? At(int col)=>cells.TryGetValue(col,out var x)?x.Value?.Trim():null;
                void Issue(string severity,string code,FinanceField? field,string message)=>result.Issues.Add(new(severity,code,field?.Header,message,record,p.Sheet));
                // Only this exact Oracle subtotal/footer structure is skipped as a transaction.
                if(p.SourceCode=="SUPPLIER_PAYMENT_REGISTER"&&At(17)=="Subtotal :"
                    &&cells.All(x=> x.Key is 17 or 18 or 19 or 20 || string.IsNullOrWhiteSpace(x.Value.Value)&&x.Value.Raw.Formula is null)
                    &&new[]{18,19,20}.All(c=>cells.TryGetValue(c,out var v)&&v.Raw.Formula is null&&v.Type=="n"&&TryExactDecimal(v.Value,out _)))
                {
                    result.Rows.Add(new(record,p.Sheet,raw,"SUBTOTAL",null));subtotals++;
                    if(ended)Issue("ERROR","DATA_AFTER_FOOTER",null,"Có dòng sau End of Report.");
                    continue;
                }
                if(p.SourceCode=="SUPPLIER_PAYMENT_REGISTER"&&At(1)=="End of Report"
                    &&cells.All(x=>x.Key==1 || string.IsNullOrWhiteSpace(x.Value.Value)&&x.Value.Raw.Formula is null)
                    &&cells[1].Raw.Formula is null)
                {
                    result.Rows.Add(new(record,p.Sheet,raw,"FOOTER",null));
                    if(ended)Issue("ERROR","DUPLICATE_FOOTER",null,"End of Report bị lặp.");ended=true;continue;
                }
                if(result.DataRows>=policy.MaxRows){result.Issues.Add(new("ERROR","ROW_LIMIT",null,"Vượt giới hạn dòng; không được xác nhận kết quả đang đọc dở."));break;}
                var errorsBefore=result.Errors;var values=new Dictionary<string,object?>(StringComparer.Ordinal);
                if(ended)Issue("ERROR","DATA_AFTER_FOOTER",null,"Có dữ liệu sau End of Report; không tự bỏ dòng.");
                foreach(var extra in cells.Where(x=>!p.Fields.Any(f=>f.Column==x.Key)&&(!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null)))
                {
                    var severity=extra.Key>p.Width?"WARNING":"ERROR";
                    Issue(severity,"UNMAPPED_CELL",null,$"{extra.Value.Raw.CellReference}: Ô ngoài cột đã định nghĩa được giữ trong dữ liệu gốc. Kiểm tra trước khi xác nhận.");
                    if(extra.Value.Type=="e"||extra.Value.Raw.Formula is not null)Issue("ERROR","UNMAPPED_FORMULA_OR_ERROR",null,$"{extra.Value.Raw.CellReference}: Không chấp nhận công thức/ô lỗi ở cột chưa định nghĩa.");
                }
                foreach(var f in p.Fields)
                {
                    cells.TryGetValue(f.Column,out var cell);var address=FinanceXlsxReader.Label(f.Column)+record;
                    void E(string code,string message)=>Issue("ERROR",code,f,address+": "+message);
                    void W(string code,string message)=>Issue("WARNING",code,f,address+": "+message);
                    var text=cell?.Value;var type=cell?.Type;
                    if(type=="e"){E("EXCEL_CELL_ERROR","Ô Excel đang có lỗi.");values[f.Property]=null;continue;}
                    if(cell?.Raw.Formula is not null)
                    {
                        if(!cell.FormulaSupported){E("FORMULA_UNSUPPORTED","Công thức shared/array chưa được hỗ trợ hoặc nhóm gốc không hợp lệ.");values[f.Property]=null;continue;}
                        if(cell.Raw.CachedValue is null||string.IsNullOrEmpty(text)){E("FORMULA_CACHE_MISSING","Công thức thiếu kết quả lưu; tính và lưu lại trong Excel rồi nộp bản mới.");values[f.Property]=null;continue;}
                        var literal=FinanceXlsxReader.Literal(cell.EffectiveFormula??"");
                        if(literal is not null&&f.Kind is FinanceFieldKind.Text or FinanceFieldKind.Identifier or FinanceFieldKind.Currency)
                        {
                            if(type!="str"||literal!=text){E("FORMULA_CACHE_MISMATCH","Kết quả lưu khác chuỗi gốc trong công thức.");values[f.Property]=null;continue;}
                            text=literal;literals++;
                        }
                        else if(p.SourceCode=="EX_USD"&&f.Property=="InvoiceAmount"&&type=="n"&&policy.AllowCachedAmountFormulas
                            &&Regex.IsMatch(cell.EffectiveFormula??"",@"^\s*(?:[0-9]+(?:\.[0-9]+)?|\$?[A-Z]{1,3}\$?[1-9][0-9]*)(?:\s*[+*/\-]\s*(?:[0-9]+(?:\.[0-9]+)?|\$?[A-Z]{1,3}\$?[1-9][0-9]*))*\s*$"))
                            W("FORMULA_CACHED_VALUE","Dùng kết quả Excel đã lưu, không tính lại công thức. Finance phải đối chiếu trước khi xác nhận.");
                        else {E("FORMULA_UNSUPPORTED","Công thức này chưa được mẫu/chính sách cho phép.");values[f.Property]=null;continue;}
                    }
                    if(string.IsNullOrWhiteSpace(text))
                    {values[f.Property]=null;if(f.Required)E("REQUIRED","Ô bắt buộc đang trống. Không tự điền 0.");continue;}
                    if(type is not ("n" or "s" or "str" or "inlineStr" or "d"))
                    {values[f.Property]=null;E("CELL_TYPE_UNSUPPORTED","Kiểu ô chưa được hỗ trợ, không tự đổi Boolean thành số.");continue;}
                    switch(f.Kind)
                    {
                        case FinanceFieldKind.Number:
                            if(!TryExactDecimal(text,out var number)){E("DECIMAL_PRECISION","Cần số chính xác, tối đa 20 chữ số nguyên/18 số lẻ và nằm trong .NET decimal. Không tự làm tròn, không dấu phân cách hàng nghìn.");values[f.Property]=null;}
                            else values[f.Property]=number;
                            break;
                        case FinanceFieldKind.Date:
                            if(!TryDate(text,type!,book.Date1904,out var date,out var hasTime)){E("INVALID_DATE","Ngày phải là ô Date/serial Excel, yyyy-MM-dd hoặc dd/MM/yyyy. Không đoán MM/dd/yyyy.");values[f.Property]=null;}
                            else {values[f.Property]=date;if(hasTime)W("DATE_TIME_PART","Nguồn có giờ; cột dữ liệu chuẩn giữ phần ngày.");}
                            break;
                        default:
                            var normalized=text.Trim();
                            if(f.Kind==FinanceFieldKind.Identifier&&type=="n")
                            {
                                normalized=Expand(text);
                                W("NUMERIC_IDENTIFIER","Mã nguồn được lưu kiểu Number; kiểm tra số 0 đầu. Hệ thống không tự thêm số 0.");
                                if(!Regex.IsMatch(normalized,@"^[0-9]+(?:\.0+)?$")||normalized.Split('.')[0].TrimStart('0').Length>15)
                                    E("IDENTIFIER_PRECISION","Mã Number quá 15 chữ số/không nguyên; cần xuất dạng Text từ nguồn.");
                                normalized=normalized.Contains('.')?normalized.TrimEnd('0').TrimEnd('.'):normalized;
                            }
                            if(f.Kind==FinanceFieldKind.Currency){normalized=normalized.ToUpperInvariant();if(!Regex.IsMatch(normalized,"^[A-Z]{3}$"))E("CURRENCY_FORMAT","Tiền tệ cần 3 chữ cái.");}
                            if(normalized.Length>f.MaxLength)E("TEXT_TOO_LONG",$"Tối đa {f.MaxLength} ký tự; không tự cắt dữ liệu.");
                            values[f.Property]=normalized;break;
                    }
                }
                if(values.GetValueOrDefault(p.CoverageProperty) is DateOnly day&&(day<policy.Start||day>=policy.EndExclusive))
                    Issue("ERROR","WRONG_COVERAGE",p.Fields.Single(x=>x.Property==p.CoverageProperty),$"Ngày phải từ {policy.Start:yyyy-MM-dd} đến trước {policy.EndExclusive:yyyy-MM-dd}; không tự bỏ dòng ngoài phạm vi.");
                if(values.Values.OfType<decimal>().Any(x=>x<0))Issue("WARNING","NEGATIVE_VALUE",null,"Có số âm; giữ nguyên để Finance xác nhận điều chỉnh/hoàn trả/bù trừ.");
                if(p.SourceCode=="SUPPLIER_PAYMENT_REGISTER"&&values.GetValueOrDefault("VoidStatus") is string status)
                {if(status=="Y")Issue("WARNING","VOID_PAYMENT",p.Fields.Single(x=>x.Property=="VoidStatus"),"Thanh toán có Void Status=Y; giữ dòng, chưa tính vào báo cáo cho đến khi chốt quy tắc void.");else if(status!="N")Issue("WARNING","UNKNOWN_VOID_STATUS",null,"Trạng thái void khác N/Y cần Finance kiểm tra.");}
                if(p.SourceCode=="AP_REPORT"&&values.GetValueOrDefault("InvoiceStatus") is string invStatus&&invStatus!="Validated")
                    Issue("WARNING","INVOICE_STATUS_REVIEW",p.Fields.Last(),"Hóa đơn chưa ở trạng thái Validated; giữ dữ liệu, kiểm tra quy tắc báo cáo.");
                if(p.SourceCode=="EX_USD"&&(values.GetValueOrDefault("PoNumber") is null||values.GetValueOrDefault("CustomsDeclaration") is null))
                    Issue("WARNING","LINK_DATA_MISSING",null,"Thiếu PO hoặc tờ khai; giữ trống, chưa coi là đối chiếu xong.");
                var references=p.SourceCode=="EX_USD"?new[]{"PoNumber","CustomsDeclaration"}:p.SourceCode=="EX_VND"?new[]{"InvoiceReference"}:Array.Empty<string>();
                if(references.Any(x=>values.GetValueOrDefault(x) is string v&&v.IndexOfAny([',','/',';'])>=0))
                    Issue("WARNING","REFERENCE_NEEDS_REVIEW",null,"Tham chiếu chứa dấu phân cách; giữ nguyên chuỗi, không tự tách hóa đơn/PO.");
                var signature=PoProfile.Serialize(p.Fields.Select(f=>values.GetValueOrDefault(f.Property)).ToArray());
                if(seen.TryGetValue(signature,out var previous))Issue("WARNING","DUPLICATE_ROW",null,$"Nội dung chuẩn hóa giống dòng {previous}; vẫn giữ cả hai dòng, không tự khử trùng.");else seen[signature]=record;
                result.Rows.Add(new(record,p.Sheet,raw,"DATA",result.Errors==errorsBefore?values:null));
            }
            if(p.SourceCode=="SUPPLIER_PAYMENT_REGISTER"&&!ended)result.Issues.Add(new("ERROR","FOOTER_MISSING",null,"Thiếu End of Report; kiểm tra file Oracle bị cắt dở."));
            if(subtotals>0)result.Issues.Add(new("INFO","CONTROL_ROWS_PRESERVED",null,$"Giữ {subtotals} dòng Subtotal và footer trong raw; không ghi chúng vào bảng giao dịch."));
            if(literals>0)result.Issues.Add(new("INFO","LITERAL_FORMULAS",null,$"Đã đối chiếu {literals} ô công thức chuỗi với giá trị cache; giữ công thức và vị trí gốc."));
        }
        catch(OperationCanceledException){throw;}
        catch(Exception e) when(e is InvalidDataException or XmlException or FormatException or OverflowException or ArgumentOutOfRangeException)
        {result.Issues.Add(new("ERROR","FILE_LAYOUT_INVALID",null,"Không đọc được đúng mẫu XLSX đã cấu hình. Kiểm tra sheet, cấu trúc file, mã hóa/mật khẩu và giới hạn dữ liệu."));}
        if(result.DataRows==0&&result.Errors==0)result.Issues.Add(new("ERROR","EMPTY_DATA_BATCH",null,"File không có giao dịch; không được thay bản đang dùng bằng file rỗng."));
        return result;
    }
    private static bool CheckHeaders(FinanceWorkbook b,FinanceSourceProfile p,ParsedFinanceFile result)
    {
        foreach(var f in p.Fields)
        {
            var texts=new List<string>();
            for(var r=p.HeaderStart;r<=p.HeaderEnd;r++)if(b.Rows.TryGetValue(r,out var row)&&row.TryGetValue(f.HeaderColumn,out var c))
            {if(c.Raw.Formula is not null){result.Issues.Add(new("ERROR","HEADER_FORMULA",f.Header,"Tiêu đề không được dùng công thức."));return false;}if(!string.IsNullOrWhiteSpace(c.Value))texts.Add(c.Value);}
            if(FinanceXlsxReader.Key(string.Join(" ",texts))!=FinanceXlsxReader.Key(f.Header))result.Issues.Add(new("ERROR","HEADER_MISMATCH",f.Header,$"Cần tiêu đề {f.Header} ở cột {FinanceXlsxReader.Label(f.HeaderColumn)}, hàng {p.HeaderStart}–{p.HeaderEnd}."));
        }
        foreach(var (r,row) in b.Rows.Where(x=>x.Key>=p.HeaderStart&&x.Key<=p.HeaderEnd))
            if(row.Any(x=>!p.Fields.Any(f=>f.HeaderColumn==x.Key)&&(!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null)))
                result.Issues.Add(new("ERROR","EXTRA_HEADER",null,$"Hàng {r}: có tiêu đề ngoài mẫu. Không tự đoán cột."));
        return result.Errors==0;
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
    private static bool TryDate(string text,string type,bool date1904,out DateOnly date,out bool hasTime)
    {
        date=default;hasTime=false;
        if(type=="n")
        {
            if(!decimal.TryParse(text,NumberStyles.Float,Inv,out var serial)||serial<0||serial>2_900_000||!date1904&&serial>=60&&serial<61)return false;
            var day=(int)decimal.Truncate(serial);hasTime=serial!=day;
            date=date1904?new DateOnly(1904,1,1).AddDays(day):new DateOnly(1899,12,31).AddDays(day>=61?day-1:day);return true;
        }
        if(type=="d"&&DateTime.TryParse(text,Inv,DateTimeStyles.RoundtripKind,out var dt)){date=DateOnly.FromDateTime(dt);hasTime=dt.TimeOfDay!=TimeSpan.Zero;return true;}
        return DateOnly.TryParseExact(text.Trim(),["yyyy-MM-dd","dd/MM/yyyy"],Inv,DateTimeStyles.None,out date);
    }
}
