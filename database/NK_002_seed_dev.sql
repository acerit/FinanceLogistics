/* DEV only. No production business-policy approval implied.
   Period 2026-09 is submission month. The supplied NK samples contain Trx Dates Jan-Aug.
   EXPLICIT_RANGE is used for layout testing. Operational YTD/monthly policy is approved separately.
   Never edit an existing template/frozen batch to make a test pass. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @OwnerUpn nvarchar(320)=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN';
DECLARE @OwnerId bigint,@PeriodId int,@Req bigint,@Version int=950001;
DECLARE @Start date='20260901';
DECLARE @PeriodCode char(7)=CONVERT(char(7),@Start,126);
IF @OwnerUpn=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN' THROW 51000,'Set OwnerUpn to an existing tested active FINANCE user. Do not guess UserId.',1;
IF OBJECT_ID(N'src.LogisticsNkLine') IS NULL THROW 51000,'Run NK_001 first.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsNk.Seed.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Seed lock unavailable.',1;
IF (SELECT COUNT(*) FROM sec.AppUser u WITH(UPDLOCK,HOLDLOCK) WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='FINANCE'))<>1
 THROW 51000,'Owner UPN must match exactly one active FINANCE user. No roles are granted here.',1;
SELECT @OwnerId=u.UserId FROM sec.AppUser u WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='FINANCE');
SELECT @PeriodId=PeriodId FROM cfg.ReportingPeriod WITH(UPDLOCK,HOLDLOCK) WHERE PeriodCode=@PeriodCode;
IF @PeriodId IS NULL
BEGIN
 INSERT cfg.ReportingPeriod(PeriodCode,StartDate,EndDateExclusive,Status) VALUES(@PeriodCode,@Start,DATEADD(month,1,@Start),'OPEN');
 SET @PeriodId=CONVERT(int,SCOPE_IDENTITY());
END
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.ReportingPeriod WHERE PeriodId=@PeriodId AND Status='OPEN' AND StartDate=@Start AND EndDateExclusive=DATEADD(month,1,@Start))
 THROW 51000,'Period differs or is not OPEN. Use a separately agreed DEV period; never reopen automatically.',1;
DECLARE @definition nvarchar(max),@policy nvarchar(max);

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='NK_1000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_1000')
 THROW 51000,'NK_1000 catalog missing.',1;
SET @definition=N'{"profile":"NK_1000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"1004 purchase 2026 - AGBS1(2026","headerStart":1,"headerEnd":2,"dataStart":3}';
SET @policy=N'{"purpose":"DEV_NK_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"TransactionDate","coverageStart":"2026-01-01","coverageEndExclusive":"2026-09-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='NK_1000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_1000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 950001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('NK_1000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='NK_1000' AND ScopeCode='SITE_1000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'NK_1000','SITE_1000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing NK_1000 requirement differs; no automatic changes.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='NK_6000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_6000')
 THROW 51000,'NK_6000 catalog missing.',1;
SET @definition=N'{"profile":"NK_6000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"báo cáo NHẬP khẩu 2026","headerStart":1,"headerEnd":1,"dataStart":2}';
SET @policy=N'{"purpose":"DEV_NK_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"TransactionDate","coverageStart":"2026-01-01","coverageEndExclusive":"2026-09-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='NK_6000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_6000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 950001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('NK_6000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='NK_6000' AND ScopeCode='SITE_6000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'NK_6000','SITE_6000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing NK_6000 requirement differs; no automatic changes.',1;
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT p.PeriodCode,r.RequirementId,r.SourceCode,r.ScopeCode,r.ActiveBatchId,r.PolicySnapshotJson
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
WHERE p.PeriodId=@PeriodId AND r.SourceCode IN('NK_1000','NK_6000');
