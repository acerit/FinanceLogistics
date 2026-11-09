using FinanceLogistics.Infrastructure.FinanceSources;
using System.Globalization;

if (args.Length==0) { Console.Error.WriteLine("Usage: dotnet run --project tests/FinanceRa.Checks -- <file.xlsx> [expected-data-rows]"); return 1; }
var file=args[0];
var expected=args.Length>1?int.Parse(args[1],CultureInfo.InvariantCulture):2;
using var stream=File.OpenRead(file);
var policy=new FinanceParsePolicy("EXPLICIT_RANGE",new DateOnly(2026,1,1),new DateOnly(2026,8,1),false);
var parsed=new FinanceFileParser().Parse(stream,Path.GetFileName(file),"RECEIPT_APPLICATION",policy);
Console.WriteLine($"RA source: raw={parsed.Rows.Count} data={parsed.DataRows} error={parsed.Errors} warning={parsed.Warnings}");
foreach(var g in parsed.Issues.GroupBy(x=>(x.Severity,x.Code))) Console.WriteLine($"{g.Key}: {g.Count()}");
if(parsed.Errors!=0 || parsed.DataRows!=expected) { Console.Error.WriteLine("RA PARSER NOT APPROVED; inspect errors, no SQL writes."); return 2; }
var rows=parsed.Rows.Where(x=>x.Kind=="DATA"&&x.Line is not null).Select(x=>x.Line!).ToArray();
if(rows.Length!=expected || rows.Any(x=>x.GetValueOrDefault("ReceiptNumber") is not string || x.GetValueOrDefault("ApplyTo") is not string || x.GetValueOrDefault("AmountApplied") is not decimal))
{ Console.Error.WriteLine("RA key/amount mapping mismatch"); return 2; }
Console.WriteLine("PASS: RA header row28, business keys and precise amount parsed.");
return 0;
