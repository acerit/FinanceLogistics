using FinanceLogistics.Infrastructure.LogisticsNk;
using FinanceLogistics.Infrastructure.LogisticsTrade;
using System.Text.Json;

if(args.Length!=3){Console.Error.WriteLine("Usage: dotnet run --project tests/LogisticsRealInput.Probe -- SOURCE FILE PROFILE");return 1;}
var source=args[0];var path=args[1];var profile=args[2];
if(!File.Exists(path)){Console.Error.WriteLine("File not found: "+path);return 1;}
await using var file=File.OpenRead(path);
var start=new DateOnly(2026,1,1);var end=new DateOnly(2027,1,1);
if(source.StartsWith("NK_",StringComparison.Ordinal)){
  var result=new NkFileParser().Parse(file,Path.GetFileName(path),source,new NkParsePolicy("EXPLICIT_RANGE",start,end,true),default,profile);
  Console.WriteLine(JsonSerializer.Serialize(new{source,profile,rows=result.DataRows,typed=result.Rows.Count(r=>r.Line is not null),errors=result.Errors,warnings=result.Warnings,issues=result.Issues.GroupBy(x=>new{x.Severity,x.Code}).Select(x=>new{x.Key.Severity,x.Key.Code,count=x.Count()})},new JsonSerializerOptions{WriteIndented=true}));
  return result.Errors==0?0:2;
}
var p=TradeSourceProfiles.Get(source,profile);
var mode=p.IsVerbatim?"AS_OF_SNAPSHOT":"EXPLICIT_RANGE";
var trade=new TradeFileParser().Parse(file,Path.GetFileName(path),source,new TradeParsePolicy(mode,start,end),default,profile);
Console.WriteLine(JsonSerializer.Serialize(new{source,profile,rows=trade.DataRows,typed=trade.Rows.Count(r=>r.Line is not null),errors=trade.Errors,warnings=trade.Warnings,issues=trade.Issues.GroupBy(x=>new{x.Severity,x.Code}).Select(x=>new{x.Key.Severity,x.Key.Code,count=x.Count()})},new JsonSerializerOptions{WriteIndented=true}));
return trade.Errors==0?0:2;
