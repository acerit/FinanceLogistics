using System.Security.Cryptography;
using System.Text.Json;
using FinanceLogistics.Infrastructure.Imports;
using FinanceLogistics.Infrastructure.LogisticsTrade;
using FinanceLogistics.Application.Imports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
// Real service/SQL tests. HTTP authorization, AD/SAML and process-crash recovery are separate.
var evidence=new List<object>();string source="preflight";long? batch=null;
void Check(bool ok,string name){if(!ok)throw new Exception("Assertion failed: "+name);Console.WriteLine("PASS: "+source+" / "+name);evidence.Add(new{source,check=name,batchId=batch,status="PASS"});}
string Arg(string key){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:throw new ArgumentException("Missing "+key);}
var report=Path.GetFullPath(args.Contains("--report")?Arg("--report"):"trade-sql-results.json");
try{
 if(!args.Contains("--allow-dev-writes"))throw new ArgumentException("Requires --allow-dev-writes: test creates versions and changes active batches of seven DEV requirements.");
 var config=new ConfigurationBuilder().SetBasePath(Path.GetFullPath(Arg("--api-dir"))).AddJsonFile("appsettings.json",true).AddJsonFile("appsettings.Development.json",true).AddUserSecrets("finance-logistics-identity-development").AddEnvironmentVariables().Build();
 var conn=config.GetConnectionString("FinanceLogistics")??throw new Exception("Missing DEV connection.");
 if(new SqlConnectionStringBuilder(conn).InitialCatalog!="FinanceLogistics_Dev")throw new Exception("Only FinanceLogistics_Dev is permitted.");
 var db=new PoDatabase(conn);var files=new LocalPoFileStorage(config["PoImport:StorageRoot"]??throw new Exception("Missing private storage configuration."));var factory=new TradeImportFactory(db,files);
 async Task<List<Dictionary<string,object?>>> Read(string sql,params(string,object)[] ps){await using var c=await db.OpenAsync(default);await using var cmd=new SqlCommand(sql,c);foreach(var(k,v)in ps)cmd.Parameters.AddWithValue(k,v);await using var rd=await cmd.ExecuteReaderAsync();var rows=new List<Dictionary<string,object?>>();while(await rd.ReadAsync()){var r=new Dictionary<string,object?>();for(var i=0;i<rd.FieldCount;i++)r[rd.GetName(i)]=rd.IsDBNull(i)?null:rd.GetValue(i);rows.Add(r);}return rows;}
 async Task<long> Count(string sql,params(string,object)[] ps)=>Convert.ToInt64((await Read(sql,ps)).Single().Values.Single());
 if(Convert.ToString((await Read("SELECT DB_NAME() AS Db")).Single()["Db"])!="FinanceLogistics_Dev")throw new Exception("Unexpected actual database.");
 if(await Count("SELECT COUNT(*) FROM ops.ProcessingJob WHERE JobType='TRADE_VALIDATE' AND Status IN('QUEUED','RUNNING')")!=0)throw new Exception("Finish existing Trade jobs and stop background Workers first; no existing jobs changed.");
 var users=await Read("SELECT u.UserId FROM sec.AppUser u WHERE u.Upn=@upn AND u.IsActive=1 AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE')",("@upn",Arg("--actor-upn")));
 if(users.Count!=1)throw new Exception("UPN must match one active FINANCE user; no permissions granted here.");var actor=(long)users[0]["UserId"]!;
 var reqs=await Read("SELECT r.RequirementId,r.SourceCode,r.PeriodId,r.PolicySnapshotJson FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId WHERE p.PeriodCode='2026-10' AND p.Status='OPEN' AND r.SourceCode IN('OTHER_IMPORT_6000','OTHER_EXPORT_6000','OTHER_IMPORT_1000','OTHER_EXPORT_1000','XK_1000','XK_6000','PIN_TEST_1000')");
 if(reqs.Count!=TradeSourceProfiles.All.Length||reqs.Any(r=>{using var d=JsonDocument.Parse((string)r["PolicySnapshotJson"]!);return d.RootElement.GetProperty("purpose").GetString()!="DEV_TRADE_LAYOUT_VALIDATION"||d.RootElement.GetProperty("businessPolicySignedOff").GetBoolean();}))throw new Exception("T4_002 and T4_005 DEV requirements required. Business requirements are not permitted.");
 var templates=await Read("SELECT SourceCode,TemplateVersionId FROM cfg.TemplateVersion WHERE VersionNo=960001 AND Status='APPROVED'");
 foreach(var p in TradeSourceProfiles.All)if(templates.Count(r=>(string)r["SourceCode"]! ==p.SourceCode)!=1)throw new Exception("Missing/ambiguous template: "+p.SourceCode);
 SqlTradeJobProcessor Processor()=>new(db,files,new TradeFileParser(),factory,NullLogger<SqlTradeJobProcessor>.Instance);
 async Task Process(long id){for(var i=0;i<10;i++){await Processor().TryProcessNextAsync(default);if(await Count("SELECT COUNT(*) FROM ingest.ImportBatch WHERE BatchId=@id AND Status IN('READY','INVALID')",("@id",id))==1)return;}throw new Exception("Worker did not finish; inspect job. Do not alter statuses.");}
 async Task Fault(string code,Func<Task> action){try{await action();}catch(PoImportFault e){Check(e.Code==code,"reject "+code);return;}throw new Exception("Expected "+code);}
 foreach(var p in TradeSourceProfiles.All){
  source=p.SourceCode;var svc=factory.For(source);var req=reqs.Single(r=>(string)r["SourceCode"]! ==source);var rid=(long)req["RequirementId"]!;var period=(int)req["PeriodId"]!;var template=(long)templates.Single(r=>(string)r["SourceCode"]! ==source)["TemplateVersionId"]!;
  using var fixture=Fixture.Write(p,Fixture.Rows(p));var bytes=fixture.ToArray();var name=source+"_synthetic_"+Guid.NewGuid().ToString("N")+".xlsx";
  async Task<UploadPoResult> Upload(Guid key,byte[] data){using var s=new MemoryStream(data);return await svc.UploadAsync(new(rid,template,key,actor,Guid.NewGuid(),name),s,default);}
  var oldActive=(await Read("SELECT ActiveBatchId FROM ingest.SubmissionRequirement WHERE RequirementId=@r",("@r",rid)))[0]["ActiveBatchId"];
  var others=await Read("SELECT RequirementId,ActiveBatchId FROM ingest.SubmissionRequirement WHERE RequirementId<>@r",("@r",rid));
  var key=Guid.NewGuid();var upload=await Task.WhenAll(Upload(key,bytes),Upload(key,bytes));var id=long.Parse(upload[0].BatchId);batch=id;
  Check(upload[0].BatchId==upload[1].BatchId&&upload.Count(x=>x.AlreadyReceived)==1,"concurrent upload creates one batch");
  Check(await Count("SELECT COUNT(*) FROM ops.ProcessingJob WHERE JobType='TRADE_VALIDATE' AND IdempotencyKey=CONCAT('trade-validate:',@id)",("@id",id))==1,"one durable job");
  Check((await svc.GetBatchAsync(id,default)).Status=="RECEIVED"&&Equals(oldActive,(await Read("SELECT ActiveBatchId FROM ingest.SubmissionRequirement WHERE RequirementId=@r",("@r",rid)))[0]["ActiveBatchId"]),"upload remains queued; active unchanged");
  await Process(id);var ready=await svc.GetBatchAsync(id,default);Check(ready.Status=="READY"&&ready.ErrorCount==0&&ready.TotalRows==2,"worker validates two rows");
  Check(await Count($"SELECT COUNT(*) FROM {p.SqlTable} WHERE BatchId=@id",("@id",id))==2&&await Count("SELECT COUNT(*) FROM ingest.RawRow WHERE BatchId=@id",("@id",id))==2,"raw/typed persisted once");
  var rv=Convert.FromBase64String(ready.RequirementRowVersion);await Fault("WARNINGS_NOT_ACCEPTED",async()=>{await svc.ConfirmAtomicallyAsync(new(id,actor,rv,false,Guid.NewGuid()),default);});
  var rev=await Count("SELECT DataRevision FROM cfg.ReportingPeriod WHERE PeriodId=@p",("@p",period));var cmd=new ConfirmPoCommand(id,actor,rv,true,Guid.NewGuid());var confirms=await Task.WhenAll(svc.ConfirmAtomicallyAsync(cmd,default),svc.ConfirmAtomicallyAsync(cmd,default));
  Check(confirms.Count(x=>x.AlreadyCurrent)==1&&await Count("SELECT DataRevision FROM cfg.ReportingPeriod WHERE PeriodId=@p",("@p",period))==rev+1,"confirm retry increments revision once");
  Check(await Count("SELECT COUNT(*) FROM ops.AuditEvent WHERE ActionCode='TRADE_CONFIRMED' AND EntityId=CONVERT(nvarchar(200),@id)",("@id",id))==1,"one confirm audit");
  var changed=bytes.ToArray();changed[^1]^=1;await Fault("UPLOAD_KEY_REUSED",async()=>{await Upload(key,changed);});
  var badRows=Fixture.Rows(p);if(p.IsVerbatim)badRows[1][1]=new Cell("BAD HEADER");else badRows[2][p.Fields.First(x=>x.Kind==TradeFieldKind.Date).Column]=new Cell("31/02/2026");using var badStream=Fixture.Write(p,badRows);var invalid=long.Parse((await Upload(Guid.NewGuid(),badStream.ToArray())).BatchId);await Process(invalid);var invalidPreview=await svc.GetBatchAsync(invalid,default);
  Check(invalidPreview.Status=="INVALID"&&invalidPreview.JobStatus=="SUCCEEDED","invalid data has completed validation job");await Fault("BATCH_NOT_READY",async()=>{await svc.ConfirmAtomicallyAsync(new(invalid,actor,Convert.FromBase64String(invalidPreview.RequirementRowVersion),true,Guid.NewGuid()),default);});
  var a=long.Parse((await Upload(Guid.NewGuid(),bytes)).BatchId);var b=long.Parse((await Upload(Guid.NewGuid(),bytes)).BatchId);await Process(a);await Process(b);var pa=await svc.GetBatchAsync(a,default);var pb=await svc.GetBatchAsync(b,default);
  await svc.ConfirmAtomicallyAsync(new(a,actor,Convert.FromBase64String(pa.RequirementRowVersion),true,Guid.NewGuid()),default);await Fault("REQUIREMENT_CHANGED",async()=>{await svc.ConfirmAtomicallyAsync(new(b,actor,Convert.FromBase64String(pb.RequirementRowVersion),true,Guid.NewGuid()),default);});await Fault("BATCH_SUPERSEDED",async()=>{await svc.ConfirmAtomicallyAsync(cmd,default);});
  var fresh=await svc.GetBatchAsync(b,default);await svc.ConfirmAtomicallyAsync(new(b,actor,Convert.FromBase64String(fresh.RequirementRowVersion),true,Guid.NewGuid()),default);Check(!(await svc.GetBatchAsync(id,default)).IsCurrent&&(await svc.GetBatchAsync(id,default)).Status=="COMMITTED","old version retained");
  var original=await svc.OpenOriginalAsync(id,default);await using(original.Content){var storedHash=await SHA256.HashDataAsync(original.Content);Check(SHA256.HashData(bytes).SequenceEqual(storedHash),"original bytes preserved");}
  var othersAfter=await Read("SELECT RequirementId,ActiveBatchId FROM ingest.SubmissionRequirement WHERE RequirementId<>@r",("@r",rid));Check(others.All(x=>othersAfter.Any(y=>Equals(x["RequirementId"],y["RequirementId"])&&Equals(x["ActiveBatchId"],y["ActiveBatchId"]))),"other sources unaffected");
  await Fault("TRADE_BATCH_NOT_FOUND",async()=>{await factory.For(p.SiteCode=="1000"?"XK_6000":"XK_1000").GetBatchAsync(id,default);});
 }
 evidence.Add(new{source="integration",check="HTTP/AD/SAML/browser/crashed Worker",status="NOT_RUN"});return 0;
}catch(Exception e){Console.Error.WriteLine("FAIL: "+e.Message);evidence.Add(new{source,check="runner",batchId=batch,status="FAIL",error=e.Message});return 1;}
finally{Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{runAtUtc=DateTimeOffset.UtcNow,results=evidence},new JsonSerializerOptions{WriteIndented=true}));}
