using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
using FinanceLogistics.Infrastructure.LogisticsNk;
namespace FinanceLogistics.Infrastructure.LogisticsTrade;
public sealed record TradeParsePolicy(string PeriodMode,DateOnly Start,DateOnly EndExclusive,bool AllowCachedAmountFormulas=true,int MaxRows=10000);
public sealed record ParsedTradeRow(int RecordNumber,string SheetName,PoCellDto[] Cells,string Kind,Dictionary<string,object?>? Line);
public sealed class ParsedTradeFile {
 public List<ParsedTradeRow> Rows{get;}=[]; public List<ParsedPoIssue> Issues{get;}=[];
 public int DataRows=>Rows.Count(x=>x.Kind=="DATA");public int Errors=>Issues.Count(x=>x.Severity=="ERROR");public int Warnings=>Issues.Count(x=>x.Severity=="WARNING");
}
public sealed class TradeFileParser {
 public ParsedTradeFile Parse(Stream stream,string fileName,string sourceCode,TradeParsePolicy policy,CancellationToken ct=default,string? profileCode=null){
  var p=profileCode is null?TradeSourceProfiles.Get(sourceCode):TradeSourceProfiles.Get(sourceCode,profileCode);var result=new ParsedTradeFile();
  try{
   if(Path.GetExtension(fileName).ToLowerInvariant()!=".xlsx"||!stream.CanSeek||stream.Length>TradeSourceProfiles.MaxBytes||policy.MaxRows is <1 or >TradeSourceProfiles.MaxRows||policy.EndExclusive<=policy.Start||policy.EndExclusive.DayNumber-policy.Start.DayNumber>3660||!policy.AllowCachedAmountFormulas||p.IsVerbatim&&policy.PeriodMode!="AS_OF_SNAPSHOT"||!p.IsVerbatim&&policy.PeriodMode is not("SELECTED_MONTH" or "YEAR_TO_PERIOD" or "EXPLICIT_RANGE"))throw new InvalidDataException();
   var book=TradeXlsxReader.Read(stream,p,ct);var columns=Headers(book,p,result);var headerFailed=result.Errors!=0;
   if(book.HasExternalLinks)result.Issues.Add(new("WARNING","EXTERNAL_LINKS_NOT_REFRESHED",null,"Giữ cache nguồn; không tải liên kết ngoài hoặc xác nhận cache đã cập nhật."));
   if(p.SourceCode.StartsWith("XK_",StringComparison.Ordinal)&&!columns.ContainsKey("AwbNumber"))result.Issues.Add(new("WARNING","AWB_COLUMN_ABSENT","AWB","Mẫu này chưa có AWB; cần đối chiếu bản mới Logistics đã bổ sung."));
   var seen=new Dictionary<string,int>(StringComparer.Ordinal);
   foreach(var (record,cells) in book.Rows.Where(x=>x.Key>=p.DataStart)){
    ct.ThrowIfCancellationRequested();
    var blank=p.IsVerbatim?cells.Values.All(c=>string.IsNullOrEmpty(c.Value)&&c.Raw.Formula is null):cells.Values.All(c=>string.IsNullOrWhiteSpace(c.Value)&&c.Raw.Formula is null);
    if(blank)continue;
    if(result.Rows.Count>=policy.MaxRows){result.Issues.Add(new("ERROR","ROW_LIMIT",null,"Vượt giới hạn dòng; không xác nhận dữ liệu đọc dở."));break;}
    var raw=cells.OrderBy(x=>x.Key).Select(x=>x.Value.Raw).ToArray();
    if(headerFailed){result.Rows.Add(new(record,p.Sheet,raw,"DATA",null));continue;}
    // In the real 2026 XK layout payment fields at AG:AI are RA/Finance output.
    // A stray formula-only row in these fields is NOT a sale/invoice line.
    // Keep it in raw history, but do not create a typed trade transaction.
    if(p.RaPaymentOutputOnly)
    {
      var outputCols=p.Fields.Where(f=>f.Property is "BankPaymentReference" or "OffsetReference" or "PaymentDate")
          .Select(f=>columns.GetValueOrDefault(f.Property)).Where(c=>c>0).ToHashSet();
      var active=cells.Where(x=>!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null).ToArray();
      if(active.Length>0&&active.All(x=>outputCols.Contains(x.Key)))
      {
        result.Rows.Add(new(record,p.Sheet,raw,"OUTPUT_ONLY",null));
        result.Issues.Add(new("WARNING","RA_OUTPUT_ONLY_ROW",null,
          "Dòng chỉ chứa cột RA/Finance output, giữ raw nhưng không tạo giao dịch xuất khẩu.",record,p.Sheet));
        continue;
      }
    }
    var populated=cells.Where(x=>!string.IsNullOrWhiteSpace(x.Value.Value)||x.Value.Raw.Formula is not null).ToArray();
    // Preserve purely decorative numeric-dash rows from the source. They are not transactions.
    if(!p.IsVerbatim&&populated.Length>0&&populated.All(x=>x.Value.Raw.Formula is null&&x.Value.Value?.Trim()=="-"&&p.Fields.Any(f=>f.Kind==TradeFieldKind.Number&&columns.GetValueOrDefault(f.Property)==x.Key))){result.Rows.Add(new(record,p.Sheet,raw,"PLACEHOLDER",null));result.Issues.Add(new("WARNING","TEMPLATE_PLACEHOLDER_ROW",null,"Dòng chỉ có '-' ở cột số; giữ raw, không tạo giao dịch chuẩn hóa.",record,p.Sheet));continue;}
    // Keep old Excel template footer rows with only literal zero placeholders in
    // money totals. Preserve raw cells but never create artificial transactions.
    if(p.SchemaVersion>=3 && p.SourceCode.StartsWith("OTHER_IMPORT_",StringComparison.Ordinal)
       && populated.Length>0 && populated.All(x=>x.Value.Raw.Formula is null
          && x.Value.Value?.Trim()=="0" &&
          (x.Key==columns.GetValueOrDefault("TotalAmount") || x.Key==columns.GetValueOrDefault("VndAmount"))))
    {
        result.Rows.Add(new(record,p.Sheet,raw,"PLACEHOLDER",null));
        result.Issues.Add(new("WARNING","TEMPLATE_TRAILING_ZERO_ROW",null,"Dòng mẫu chỉ có số 0, giữ raw và không tạo giao dịch.",record,p.Sheet));
        continue;
    }
    var before=result.Errors;var values=new Dictionary<string,object?>(StringComparer.Ordinal);
    void Issue(string severity,string code,TradeField? f,string message)=>result.Issues.Add(new(severity,code,f?.Header,message,record,p.Sheet));
    var mapped=columns.Values.ToHashSet();foreach(var extra in cells.Where(x=>!mapped.Contains(x.Key)&&(!string.IsNullOrEmpty(x.Value.Value)||x.Value.Raw.Formula is not null)))Issue("ERROR","UNMAPPED_CELL",null,$"{extra.Value.Raw.CellReference}: Có dữ liệu dưới cột chưa được khai báo; giữ raw và chặn xác nhận.");
    foreach(var f in p.Fields){
     if(!columns.TryGetValue(f.Property,out var column)){values[f.Property]=null;continue;}cells.TryGetValue(column,out var c);var address=TradeXlsxReader.Label(column)+record;
     void Error(string code,string message)=>Issue("ERROR",code,f,address+": "+message);
     void Warn(string code,string message)=>Issue("WARNING",code,f,address+": "+message);
     var text=c?.Value;var type=c?.Type;
     if(p.RaPaymentOutputOnly && (f.Property is "BankPaymentReference" or "OffsetReference" or "PaymentDate"))
     {
       values[f.Property]=null;
       if(!string.IsNullOrWhiteSpace(text)||c?.Raw.Formula is not null)
         Warn("RA_OUTPUT_NOT_AUTHORITATIVE","Trường thanh toán dành cho Finance RA; giữ raw, không dùng giá trị Logistics làm dữ liệu chuẩn hóa.");
       continue;
     }
     if(f.Kind==TradeFieldKind.Verbatim){values[f.Property]=text;if(type=="e")Warn("SOURCE_EXCEL_ERROR_PRESERVED","Giữ lỗi Excel nguồn; chưa diễn giải thành kết quả nghiệp vụ.");if(c?.Raw.Formula is not null)Warn("SOURCE_FORMULA_PRESERVED","Giữ công thức/cache trong raw; không tính lại.");continue;}
     if(type=="e"){Error("EXCEL_CELL_ERROR","Ô Excel có lỗi; cần sửa từ chứng từ nguồn.");values[f.Property]=null;continue;}
     if(c?.Raw.Formula is not null){var literal=c.FormulaSupported?TradeXlsxReader.Literal(c.EffectiveFormula??""):null;
      if(!c.FormulaSupported||string.IsNullOrWhiteSpace(c.EffectiveFormula)){Error("FORMULA_UNSUPPORTED","Cấu trúc công thức không hỗ trợ.");values[f.Property]=null;continue;}
      if(c.Raw.CachedValue is null||(f.Kind is TradeFieldKind.Number or TradeFieldKind.Date)&&string.IsNullOrWhiteSpace(text)){Error("FORMULA_CACHE_MISSING","Thiếu kết quả đã lưu; không tự tính lại.");values[f.Property]=null;continue;}
      if(literal is not null&&(type!="str"||literal!=text)){Error("FORMULA_CACHE_MISMATCH","Công thức chuỗi không khớp cache.");values[f.Property]=null;continue;}
      Warn("FORMULA_CACHED_VALUE","Đọc cache nguồn; chưa xác minh độ mới.");
     }
     if(string.IsNullOrWhiteSpace(text)||text.Trim()=="-"&&f.Kind!=TradeFieldKind.Text){values[f.Property]=null;if(f.Required)Error("REQUIRED","Thiếu ngày dùng kiểm tra phạm vi theo policy.");continue;}
     if(type is not("n" or "s" or "str" or "inlineStr" or "d" or "b")||type=="b"&&f.Kind!=TradeFieldKind.Boolean){Error("CELL_TYPE_UNSUPPORTED","Kiểu ô không phù hợp.");values[f.Property]=null;continue;}
     switch(f.Kind){
      case TradeFieldKind.Number:if(!NkFileParser.TryExactDecimal(text,out var n)){Error("DECIMAL_PRECISION","Không tự làm tròn hoặc bỏ dấu phân cách.");values[f.Property]=null;}else values[f.Property]=n;break;
      case TradeFieldKind.Date:if(!NkFileParser.TryDateForProfile(text,type!,book.Date1904,true,out var date,out var time)){Error("INVALID_DATE","Cần ngày hợp lệ có năm; không đoán năm thiếu.");values[f.Property]=null;}else{values[f.Property]=date;if(time)Warn("DATE_TIME_PART","Giữ phần ngày chuẩn; raw giữ giá trị có giờ.");}break;
      case TradeFieldKind.Boolean:if(text=="1"||text.Equals("true",StringComparison.OrdinalIgnoreCase))values[f.Property]=true;else if(text=="0"||text.Equals("false",StringComparison.OrdinalIgnoreCase))values[f.Property]=false;else{Error("INVALID_BOOLEAN","Cần TRUE/FALSE hoặc 1/0.");values[f.Property]=null;}break;
      default:var value=text.Trim();if(f.Kind==TradeFieldKind.Identifier&&type=="n"){value=NkFileParser.Expand(text);Warn("NUMERIC_IDENTIFIER","Mã nguồn lưu Number; không tự thêm 0 hoặc phục hồi chữ số mất.");if(!Regex.IsMatch(value,@"^[0-9]+(?:\.0+)?$")||value.Split('.')[0].TrimStart('0').Length>15)Error("IDENTIFIER_PRECISION","Mã không nguyên hoặc vượt 15 chữ số; cần Text chính xác.");if(value.Contains('.'))value=value.TrimEnd('0').TrimEnd('.');}
       if(f.Kind==TradeFieldKind.Currency){value=value.ToUpperInvariant();if(!Regex.IsMatch(value,"^[A-Z]{3}$"))Error("CURRENCY_FORMAT","Cần mã tiền tệ ba chữ cái.");}if(value.Length>f.MaxLength)Error("TEXT_TOO_LONG","Vượt giới hạn; không cắt dữ liệu.");values[f.Property]=value;break;
     }
    }
    if(!p.IsVerbatim&&values.GetValueOrDefault(p.CoverageProperty) is DateOnly day&&(day<policy.Start||day>=policy.EndExclusive)){var f=p.Fields.Single(x=>x.Property==p.CoverageProperty);Issue("ERROR","WRONG_COVERAGE",f,$"{TradeXlsxReader.Label(columns[f.Property])}{record}: Ngoài phạm vi đã cấu hình; không lọc bỏ dòng.");}
    if(values.Values.OfType<decimal>().Any(x=>x<0))Issue("WARNING","NEGATIVE_VALUE",null,"Giữ số âm; chưa áp quy tắc báo cáo.");
    var signature=PoProfile.Serialize(p.Fields.Select(f=>values.GetValueOrDefault(f.Property)).ToArray());if(seen.TryGetValue(signature,out var previous))Issue("WARNING","DUPLICATE_ROW",null,$"Giống dòng {previous}; vẫn giữ cả hai.");else seen.Add(signature,record);
    result.Rows.Add(new(record,p.Sheet,raw,"DATA",result.Errors==before?values:null));
   }
  }catch(OperationCanceledException){throw;}catch(Exception e)when(e is InvalidDataException or XmlException or FormatException or OverflowException or ArgumentOutOfRangeException){result.Issues.Add(new("ERROR","FILE_LAYOUT_INVALID",null,"File không đúng XLSX/profile: kiểm tra sheet, header, ô gộp, macro, mã hóa và giới hạn file."));}
  if(result.DataRows==0&&result.Errors==0)result.Issues.Add(new("ERROR","EMPTY_DATA_BATCH",null,"File rỗng không thay nguồn; Không phát sinh là quy trình riêng."));return result;
 }
 private static Dictionary<string,int> Headers(TradeWorkbook book,TradeSourceProfile p,ParsedTradeFile result){
  var map=new Dictionary<string,int>(StringComparer.Ordinal);book.Rows.TryGetValue(p.HeaderRow,out var headers);
  foreach(var (column,cell)in headers??[]){if(string.IsNullOrWhiteSpace(cell.Value)&&cell.Raw.Formula is null)continue;var key=TradeXlsxReader.Key(cell.Value??"");var f=p.Fields.SingleOrDefault(x=>TradeXlsxReader.Key(x.Header)==key||p.SourceCode.StartsWith("XK_",StringComparison.Ordinal)&&x.Property=="AwbNumber"&&key=="AWB/BL");
   if(cell.Type=="e"||cell.Raw.Formula is not null||f is null){result.Issues.Add(new("ERROR","UNKNOWN_HEADER",cell.Value,$"{TradeXlsxReader.Label(column)}{p.HeaderRow}: Header chưa khai báo hoặc là công thức/lỗi."));continue;}if(!map.TryAdd(f.Property,column))result.Issues.Add(new("ERROR","DUPLICATE_HEADER",f.Header,$"{TradeXlsxReader.Label(column)}{p.HeaderRow}: Tiêu đề trùng; không chọn cột đầu tiên."));}
  foreach(var f in p.Fields.Where(x=>!x.OptionalHeader&&!map.ContainsKey(x.Property)))result.Issues.Add(new("ERROR","HEADER_MISMATCH",f.Header,"Thiếu tiêu đề; không tự đoán cột."));return map;
 }
}
