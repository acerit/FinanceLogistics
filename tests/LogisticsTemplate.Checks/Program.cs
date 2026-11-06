using System.Reflection;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Controllers;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Application.LogisticsTrade;
using FinanceLogistics.Infrastructure.LogisticsTrade;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

var count=0;
void Check(bool yes,string name){if(!yes)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
foreach(var p in TradeSourceProfiles.All) {
 var source=new SourceAccess(p.SourceCode,p.ScopeCode,AccessRules.Logistics,[p.SiteCode],true);
 var service=Proxy.Make<ITradeSourceImportService>((m,a)=>m.Name=="TemplatesAsync"
  ? Task.FromResult<IReadOnlyList<PoTemplateDto>>([new("7",960001,p.Code,p.Headers)])
  : throw new NotSupportedException(m.Name));
 var factory=Proxy.Make<ITradeImportFactory>((m,a)=>m.Name=="For"?service:throw new NotSupportedException(m.Name));
 var store=Proxy.Make<IIdentityStore>((m,a)=>m.Name=="SourcesAsync"?Task.FromResult(new[]{source}):throw new NotSupportedException(m.Name));
 TradeImportsController Controller(string role,string site){
  var c=new TradeImportsController(factory,store);
  var http=new DefaultHttpContext();
  http.Items[CurrentUser.ItemKey]=new UserProfile("1","synthetic",null,"Synthetic",[role],[site],role==AccessRules.SystemAdmin?[]:["sources.read"]);
  var route=new RouteData();route.Values["sourceCode"]=p.SourceCode;
  c.ControllerContext=new ControllerContext{HttpContext=http,RouteData=route};return c;
 }
 foreach(var role in new[]{AccessRules.Finance,AccessRules.Logistics}) {
  var c=Controller(role,p.SiteCode);var result=(FileContentResult)await c.BlankTemplate(7,default);
  Check(result.FileDownloadName==$"{p.SourceCode}_template_960001.xlsx",p.SourceCode+": authorized named template "+role);
  var parsed=new TradeFileParser().Parse(new MemoryStream(result.FileContents),result.FileDownloadName,p.SourceCode,new(p.IsVerbatim?"AS_OF_SNAPSHOT":"EXPLICIT_RANGE",new(2026,1,1),new(2027,1,1)));
  Check(parsed.Errors==1&&parsed.Issues.Single(i=>i.Severity=="ERROR").Code=="EMPTY_DATA_BATCH",p.SourceCode+": downloaded headers accepted; blank is not data");
 }
 try{await Controller(AccessRules.Logistics,p.SiteCode=="1000"?"6000":"1000").BlankTemplate(7,default);Check(false,"other site");}catch(IdentityFault e){Check(e.Status==403,p.SourceCode+": other-site denied");}
 try{await Controller(AccessRules.SystemAdmin,p.SiteCode).BlankTemplate(7,default);Check(false,"admin only");}catch(IdentityFault e){Check(e.Status==403,p.SourceCode+": admin-only denied");}
 try{await Controller(AccessRules.Finance,p.SiteCode).BlankTemplate(999,default);Check(false,"unknown template");}catch(PoImportFault e){Check(e.Code=="TRADE_TEMPLATE_NOT_FOUND",p.SourceCode+": unknown/unapproved template denied");}
}
Console.WriteLine($"Completed {count} controller/template checks. In-process checks do not test HTTP middleware, SQL, AD or IIS.");
public class Proxy:DispatchProxy {
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 protected override object? Invoke(MethodInfo? m,object?[]? a)=>Handler(m!,a);
 public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class {var p=Create<T,Proxy>();((Proxy)(object)p).Handler=handler;return p;}
}
