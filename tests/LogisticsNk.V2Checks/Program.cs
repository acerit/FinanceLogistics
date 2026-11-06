using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.LogisticsNk;

var parser = new NkFileParser();
var policy = new NkParsePolicy("EXPLICIT_RANGE",new(2026,1,1),new(2026,9,1),true);
var jsonOptions=new JsonSerializerOptions { WriteIndented=true };
string Arg(string name) {var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:throw new ArgumentException("Missing "+name);}
if(args.Contains("--inspect"))
{
    try
    {
        var source=Arg("--source");var p=NkSourceProfiles.V2.Single(x=>x.SourceCode==source);
        var path=Path.GetFullPath(Arg("--file"));
        var first=DateOnly.ParseExact(Arg("--start"),"yyyy-MM-dd",CultureInfo.InvariantCulture);
        var end=DateOnly.ParseExact(Arg("--end-exclusive"),"yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(end<=first)throw new ArgumentException("end-exclusive must be after start");
        using var file=File.OpenRead(path);var hash=Convert.ToHexString(SHA256.HashData(file));file.Position=0;
        var result=parser.Parse(file,Path.GetFileName(path),source,policy with {Start=first,EndExclusive=end},profileCode:p.Code);
        var summary=new {Mode="INSPECTION_NOT_APPROVAL",Source=source,Profile=p.Code,Sheet=p.Sheet,File=Path.GetFileName(path),Sha256=hash,
            CoverageStart=first,CoverageEndExclusive=end,Raw=result.DataRows,Typed=result.Rows.Count(x=>x.Line is not null),
            ERROR=result.Errors,WARNING=result.Warnings,SuggestedBatchStatus=result.Errors==0?"READY":"INVALID",
            CacheFreshnessVerified=false,Groups=result.Issues.GroupBy(x=>new{x.Severity,x.Code}).Select(g=>new{g.Key.Severity,g.Key.Code,Count=g.Count()}).ToArray()};
        Console.WriteLine(JsonSerializer.Serialize(summary,jsonOptions));
        foreach(var issue in result.Issues.Where(x=>x.Severity=="ERROR").Take(50))Console.WriteLine($"{issue.Code}: {issue.Message}");
        if(args.Contains("--out-dir"))
        {
            var folder=Path.GetFullPath(Arg("--out-dir"));Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,source+"_"+hash[..12]+"_summary.json"),JsonSerializer.Serialize(summary,jsonOptions));
            File.WriteAllText(Path.Combine(folder,source+"_"+hash[..12]+"_issues.json"),JsonSerializer.Serialize(result.Issues,jsonOptions));
        }
        Console.WriteLine("Inspection only; no SQL writes, file edits, formula evaluation or approval. Exit 2 means source data has ERROR.");
        return result.Errors==0?0:2;
    }
    catch(Exception e) when(e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or FormatException)
    { Console.Error.WriteLine("Inspection could not start: "+e.Message);return 1; }
}

var checks=0;
void Check(bool ok,string name) {if(!ok)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
ParsedNkFile Parse(NkSourceProfile p,Dictionary<int,Dictionary<int,Cell>> rows,NkParsePolicy? pol=null,string[]? merges=null,bool external=false,bool date1904=false,string ext=".xlsx")
{using var s=Fixture.Write(p,rows,merges,external,date1904);return parser.Parse(s,"test"+ext,p.SourceCode,pol??policy,profileCode:p.Code);}
string Template(NkSourceProfile p)=>JsonSerializer.Serialize(new{profile=p.Code,schemaVersion=p.SchemaVersion,sheet=p.Sheet,headerStart=1,headerEnd=p.HeaderRows.Length,dataStart=p.DataStart,formulaPolicy=p.FormulaPolicy});
string Frozen(NkSourceProfile p,bool cached)=>JsonSerializer.Serialize(new{sourceCode=p.SourceCode,scopeCode=p.ScopeCode,profile=p.Code,coverageProperty=p.CoverageProperty,dateTextCentury=2000,
    templateDefinition=JsonSerializer.Deserialize<JsonElement>(Template(p)),allowCachedAmountFormulas=cached,formulaPolicy=p.FormulaPolicy,
    periodMode="EXPLICIT_RANGE",startDate="2026-01-01",endDateExclusive="2026-09-01",maxRows=10000});

foreach(var p in NkSourceProfiles.V2)
{
    var tag=p.SourceCode+" ";var r=Fixture.Rows(p);var parsed=Parse(p,r);
    Check(parsed.DataRows==2&&parsed.Errors==0&&parsed.Rows.Count(x=>x.Line is not null)==2,tag+"V2 exact layout and two rows");
    Check((string?)parsed.Rows[0].Line!["InvoiceNumber"]=="000123",tag+"text identifier keeps leading zeros");
    Check(parsed.Issues.Any(x=>x.Code=="DUPLICATE_ROW"),tag+"duplicate kept and reported");
    r[p.DataStart][19]=new("12.549999999999999","n","E2*F2");parsed=Parse(p,r);
    Check(parsed.Errors==0&&(decimal?)parsed.Rows[0].Line!["OriginalAmount"]==12.549999999999999m&&parsed.Issues.Any(x=>x.Code=="FORMULA_CACHED_VALUE"),tag+"numeric formula cache read exactly, not recalculated");
    Check(parsed.Rows[0].Cells.Single(x=>x.Ordinal==19).Formula=="=E2*F2"&&parsed.Rows[0].Cells.Single(x=>x.Ordinal==19).CachedValue=="12.549999999999999",tag+"formula and cache remain raw");
    r=Fixture.Rows(p);r[p.DataStart][19]=new("8","n","E2*F2","shared","0",$"S{p.DataStart}:S{p.DataStart+1}");r[p.DataStart+1][19]=new("12.55","n","","shared","0");parsed=Parse(p,r);
    Check(parsed.Errors==0&&(decimal?)parsed.Rows[1].Line!["OriginalAmount"]==12.55m,tag+"shared follower uses its own cache");
    Check(parsed.Rows[1].Cells.Single(x=>x.Ordinal==19).FormulaOrigin=="S"+p.DataStart,tag+"shared follower traces master");
    r[p.DataStart][19]=new("8","n","E2*F2","shared","0","S"+p.DataStart);r[p.DataStart+1][19]=new("8","n");
    Check(Parse(p,r).Errors==0,tag+"single-cell shared group supported");
    r=Fixture.Rows(p);r[p.DataStart][19]=new("8","n","","shared","99");Check(Parse(p,r).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),tag+"orphan shared formula blocked");
    r=Fixture.Rows(p);r[p.DataStart][19]=new(null,"n","E2*F2");Check(Parse(p,r).Issues.Any(x=>x.Code=="FORMULA_CACHE_MISSING"),tag+"missing formula cache blocked");
    r[p.DataStart][19]=new("","n","E2*F2");Check(Parse(p,r).Errors>0,tag+"empty numeric formula cache blocked");
    r=Fixture.Rows(p);r[p.DataStart][22]=new("Acer_Product","str","XLOOKUP(A1,B:B,C:C)");Check(Parse(p,r).Issues.Any(x=>x.Code=="INVALID_DATE"),tag+"formula text in date column blocked");
    r=Fixture.Rows(p);r[p.DataStart][41]=new("#REF!","e","A1");parsed=Parse(p,r);Check(parsed.Errors==1&&parsed.DataRows==2&&parsed.Rows[0].Line is null,tag+"Excel error keeps raw row and blocks typed row");
    r=Fixture.Rows(p);r[p.DataStart][14]=new("000123","str","\"000123\"");Check(Parse(p,r).Errors==0,tag+"literal formula matches cache");
    r[p.DataStart][14]=new("wrong","str","\"000123\"");Check(Parse(p,r).Issues.Any(x=>x.Code=="FORMULA_CACHE_MISMATCH"),tag+"literal formula mismatch blocked");
    r=Fixture.Rows(p);r[p.DataStart][49]=new("","str","IF(A1=1,\"\",B1)");parsed=Parse(p,r);Check(parsed.Errors==0&&parsed.Rows[0].Line!["CocNumber"] is null,tag+"empty text formula cache is null");
    r=Fixture.Rows(p);parsed=Parse(p,r,external:true);Check(parsed.Errors==0&&parsed.Issues.Count(x=>x.Code=="EXTERNAL_LINKS_NOT_REFRESHED")==1,tag+"external parts never refreshed and warning visible");
    r=Fixture.Rows(p);r[p.DataStart][19]=new("8","n","SUM(A1:A2)","array");Check(Parse(p,r).Issues.Any(x=>x.Code=="FORMULA_UNSUPPORTED"),tag+"array formula structure blocked");
    r=Fixture.Rows(p);r[p.DataStart][31]=new("13-may-26");parsed=Parse(p,r);Check(parsed.Errors==0&&(DateOnly?)parsed.Rows[0].Line!["DeclarationDate"]==new DateOnly(2026,5,13),tag+"English month and explicit century");
    r[p.DataStart][31]=new("13-may");Check(Parse(p,r).Issues.Any(x=>x.Code=="INVALID_DATE"),tag+"missing year not guessed");
    r=Fixture.Rows(p);r[1][1]=new("wrong");Check(Parse(p,r).Issues.Any(x=>x.Code=="HEADER_MISMATCH"),tag+"wrong header blocks");
    r=Fixture.Rows(p);r[p.DataStart][57]=new("unexpected");Check(Parse(p,r).Issues.Any(x=>x.Code=="UNMAPPED_CELL"),tag+"extra data column retained and blocked");
    Check(Parse(p,Fixture.Rows(p),ext:".xls").Errors>0,tag+"renamed XLS extension rejected");
    Check(Parse(p,Fixture.Rows(p),policy with{MaxRows=1}).Issues.Any(x=>x.Code=="ROW_LIMIT"),tag+"truncation cannot pass");
    r=Fixture.Rows(p);foreach(var k in r.Keys.Where(k=>k>=p.DataStart).ToArray())r.Remove(k);Check(Parse(p,r).Issues.Any(x=>x.Code=="EMPTY_DATA_BATCH"),tag+"empty snapshot blocked");
    Check(Parse(p,Fixture.Rows(p),policy with{Start=new(2026,3,1)}).Issues.Count(x=>x.Code=="WRONG_COVERAGE")==2,tag+"range checked without filtering");
    Check(Parse(p,Fixture.Rows(p),policy with{AllowCachedAmountFormulas=false}).Errors>0,tag+"V2 requires explicit cached-value policy");
    Check(NkSourcePolicy.Supports(NkSourceProfiles.Get(p.SourceCode),Template(p)),tag+"source accepts its V2 template");
    var frozen=Frozen(p,true);Check(NkSourcePolicy.ProfileFromFrozen(p.SourceCode,frozen).Code==p.Code&&NkSourcePolicy.Frozen(frozen,p).AllowCachedAmountFormulas,tag+"frozen V2 policy retains profile");
    try{NkSourcePolicy.Frozen(Frozen(p,false),p);Check(false,tag+"wrong frozen flag");}catch(InvalidOperationException){Check(true,tag+"wrong frozen flag blocked");}
}
var a=NkSourceProfiles.V2[0];var rows=Fixture.Rows(a);
Check(Parse(a,rows,merges:["W1:W2","A1:A2"]).Errors==0,"Known vertical header merges accepted");
rows[2].Remove(23);Check(Parse(a,rows,merges:["W1:W2"]).Errors==0,"Excel-cleared covered header allowed");
Check(Parse(a,rows,merges:["C1:C2"]).Errors>0,"Merge cannot hide Trx and Date labels");
Check(Parse(a,rows,merges:["A1:B1"]).Errors>0,"Horizontal header merge rejected");
Check(Parse(a,rows,merges:["A3:A4"]).Errors>0,"Data merge rejected, no fill down");
Check(!NkSourcePolicy.Supports(NkSourceProfiles.Get("NK_1000"),Template(NkSourceProfiles.V2[1])),"Cross-site template rejected");

if(args.Contains("--write-samples"))
{
    var folder=Path.GetFullPath(Arg("--write-samples"));Directory.CreateDirectory(folder);
    foreach(var p in NkSourceProfiles.V2)
    {
        var r=Fixture.Rows(p);foreach(var k in r.Keys.Where(k=>k>=p.DataStart)){r[k][5]=new("2","n");r[k][6]=new("5","n");r[k][19]=new("10","n",$"E{k}*F{k}");}
        using(var stream=Fixture.Write(p,r,p.SiteCode=="1000"?["W1:W2"]:null,true))using(var f=File.Create(Path.Combine(folder,p.SourceCode+"_V2_01_valid.xlsx")))stream.CopyTo(f);
        r[p.DataStart][3]=new("31/02/2026");
        using(var stream=Fixture.Write(p,r,p.SiteCode=="1000"?["W1:W2"]:null,true))using(var f=File.Create(Path.Combine(folder,p.SourceCode+"_V2_02_bad_date.xlsx")))stream.CopyTo(f);
    }
}
Console.WriteLine($"Completed {checks} V2 checks. SQL/AD/site authorization/worker recovery still require Windows and SQL Server.");
return 0;
