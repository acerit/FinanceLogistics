/* DEV only. These coverage dates are technical test choices, not approved business policy. Pin snapshot never filters certificate dates. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @OwnerUpn nvarchar(320)=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN';
DECLARE @OwnerId bigint,@PeriodId int,@Req bigint,@Version int=960001;
DECLARE @Start date='20261001';
DECLARE @PeriodCode char(7)=CONVERT(char(7),@Start,126);
IF @OwnerUpn=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN' THROW 51000,'Set OwnerUpn to an existing tested active FINANCE user. Do not guess UserId.',1;
IF OBJECT_ID(N'src.PinTestLine') IS NULL THROW 51000,'Run T4_001 first.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsTrade.Seed.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
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

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='OTHER_IMPORT_6000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_6000')
 THROW 51000,'OTHER_IMPORT_6000 catalog missing.',1;
SET @definition=N'{"profile":"OTHER_IMPORT_6000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"báo cáo OTHER NHẬP khẩu 2026","headerPolicy":"EXACT_NAMES","formulaPolicy":"CACHED_VALUE_ONLY"}';
SET @policy=N'{"purpose":"DEV_TRADE_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"DeclarationDate","coverageStart":"2026-01-01","coverageEndExclusive":"2027-01-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='OTHER_IMPORT_6000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='OTHER_IMPORT_6000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 960001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('OTHER_IMPORT_6000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='OTHER_IMPORT_6000' AND ScopeCode='SITE_6000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'OTHER_IMPORT_6000','SITE_6000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing OTHER_IMPORT_6000 requirement differs; no automatic changes.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='OTHER_EXPORT_6000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_6000')
 THROW 51000,'OTHER_EXPORT_6000 catalog missing.',1;
SET @definition=N'{"profile":"OTHER_EXPORT_6000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"báo cáo OTHER XUẤT khẩu 2026","headerPolicy":"EXACT_NAMES","formulaPolicy":"CACHED_VALUE_ONLY"}';
SET @policy=N'{"purpose":"DEV_TRADE_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"DeclarationDate","coverageStart":"2026-01-01","coverageEndExclusive":"2027-01-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='OTHER_EXPORT_6000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='OTHER_EXPORT_6000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 960001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('OTHER_EXPORT_6000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='OTHER_EXPORT_6000' AND ScopeCode='SITE_6000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'OTHER_EXPORT_6000','SITE_6000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing OTHER_EXPORT_6000 requirement differs; no automatic changes.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='XK_6000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_6000')
 THROW 51000,'XK_6000 catalog missing.',1;
SET @definition=N'{"profile":"XK_6000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"báo cáo XUẤT khẩu 2026","headerPolicy":"EXACT_NAMES","formulaPolicy":"CACHED_VALUE_ONLY"}';
SET @policy=N'{"purpose":"DEV_TRADE_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"IssuedDate","coverageStart":"2026-01-01","coverageEndExclusive":"2027-01-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='XK_6000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='XK_6000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 960001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('XK_6000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='XK_6000' AND ScopeCode='SITE_6000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'XK_6000','SITE_6000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing XK_6000 requirement differs; no automatic changes.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='XK_1000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_1000')
 THROW 51000,'XK_1000 catalog missing.',1;
SET @definition=N'{"profile":"XK_1000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"báo cáo XUẤT khẩu 2026","headerPolicy":"EXACT_NAMES","formulaPolicy":"CACHED_VALUE_ONLY"}';
SET @policy=N'{"purpose":"DEV_TRADE_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"EXPLICIT_RANGE","coverageProperty":"IssuedDate","coverageStart":"2026-01-01","coverageEndExclusive":"2027-01-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='XK_1000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='XK_1000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 960001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('XK_1000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='XK_1000' AND ScopeCode='SITE_1000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'XK_1000','SITE_1000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing XK_1000 requirement differs; no automatic changes.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode WHERE s.SourceCode='PIN_TEST_1000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_1000')
 THROW 51000,'PIN_TEST_1000 catalog missing.',1;
SET @definition=N'{"profile":"PIN_TEST_1000_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"Pin test_1000","headerPolicy":"EXACT_NAMES","formulaPolicy":"PRESERVE_SOURCE"}';
SET @policy=N'{"purpose":"DEV_TRADE_LAYOUT_VALIDATION","ingestionMode":"SNAPSHOT","logisticsPeriodMode":"AS_OF_SNAPSHOT","coverageProperty":"NONE","coverageStart":"2026-01-01","coverageEndExclusive":"2027-01-01","dateTextCentury":2000,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='PIN_TEST_1000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='PIN_TEST_1000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template version 960001 differs. Do not overwrite it.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('PIN_TEST_1000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only');
END;
SET @Req=NULL;
SELECT @Req=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='PIN_TEST_1000' AND ScopeCode='SITE_1000';
IF @Req IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'PIN_TEST_1000','SITE_1000',@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','TRADE_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV range only; no business-policy signoff');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@Req AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing PIN_TEST_1000 requirement differs; no automatic changes.',1;

COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT r.RequirementId,r.SourceCode,r.ScopeCode,r.ActiveBatchId,r.PolicySnapshotJson FROM ingest.SubmissionRequirement r WHERE r.PeriodId=@PeriodId;
