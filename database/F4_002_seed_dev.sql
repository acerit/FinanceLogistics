/* DEV-only explicit profiles. APPROVED here permits test imports, not Finance business sign-off.
   September is a test submission period. Each source retains its own Jan-Jun/Jan-Aug/Jan-Jul coverage.
   This seed never changes PO/SAML, never reopens periods, never changes an active pointer. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @OwnerUpn nvarchar(320)=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN'; -- EDIT this one value. Never guess UserId=1.
DECLARE @Start date='20260901'; -- DEV test submission month, not a claim all files contain September data.
DECLARE @Version int=940001;
DECLARE @OwnerId bigint,@PeriodId int,@RequirementId bigint;
DECLARE @PeriodCode char(7)=CONVERT(char(7),@Start,126);
IF @OwnerUpn=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN' THROW 51000,'Set OwnerUpn to your already tested active FINANCE account.',1;
IF OBJECT_ID(N'src.ApReportLine') IS NULL OR OBJECT_ID(N'src.SupplierPaymentLine') IS NULL
 OR OBJECT_ID(N'src.ExchangeUsdLine') IS NULL OR OBJECT_ID(N'src.ExchangeVndLine') IS NULL THROW 51000,'Run F4_001 first.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'Finance4.Seed.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Seed lock unavailable.',1;
IF (SELECT COUNT(*) FROM sec.AppUser u WITH(UPDLOCK,HOLDLOCK) WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE'))<>1
 THROW 51000,'OwnerUpn must match exactly one active FINANCE account. No role is granted by this script.',1;
SELECT @OwnerId=u.UserId FROM sec.AppUser u WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE');
SELECT @PeriodId=PeriodId FROM cfg.ReportingPeriod WITH(UPDLOCK,HOLDLOCK) WHERE PeriodCode=@PeriodCode;
IF @PeriodId IS NULL
BEGIN
 INSERT cfg.ReportingPeriod(PeriodCode,StartDate,EndDateExclusive,Status) VALUES(@PeriodCode,@Start,DATEADD(month,1,@Start),'OPEN');
 SET @PeriodId=CONVERT(int,SCOPE_IDENTITY());
END
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.ReportingPeriod WHERE PeriodId=@PeriodId AND Status='OPEN' AND StartDate=@Start AND EndDateExclusive=DATEADD(month,1,@Start))
 THROW 51000,'Existing DEV period differs or is not OPEN; no automatic reopen.',1;
DECLARE @definition nvarchar(max),@policy nvarchar(max);

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='AP_REPORT' AND s.OwnerRoleCode='FINANCE' AND s.IsActive=1 AND sc.ScopeCode='FINANCE_SHARED')
 THROW 51000,'Catalog AP_REPORT/FINANCE_SHARED missing or inactive. Review catalog first.',1;
SET @definition=N'{"profile":"AP_ORACLE_XXAPR017_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_FINANCE_SIGNOFF","sheet":"AP report","headerStart":1,"headerEnd":3,"dataStart":4}'; SET @policy=N'{"purpose":"DEV_FINANCE4_SOURCE_VALIDATION","ingestionMode":"SNAPSHOT","financePeriodMode":"EXPLICIT_RANGE","coverageStart":"2026-01-01","coverageEndExclusive":"2026-07-01","allowCachedAmountFormulas":false,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='AP_REPORT' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='AP_REPORT' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 940001 differs for AP_REPORT; never edit an existing version to pass a test.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('AP_REPORT',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only.');
END;
SET @RequirementId=NULL;
SELECT @RequirementId=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='AP_REPORT' AND ScopeCode='FINANCE_SHARED';
IF @RequirementId IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,SubmissionRuleId,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'AP_REPORT','FINANCE_SHARED',NULL,@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV explicit source coverage; no reminders or business sign-off.');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@RequirementId AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing AP_REPORT requirement differs; seed does not overwrite it.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='SUPPLIER_PAYMENT_REGISTER' AND s.OwnerRoleCode='FINANCE' AND s.IsActive=1 AND sc.ScopeCode='FINANCE_SHARED')
 THROW 51000,'Catalog SUPPLIER_PAYMENT_REGISTER/FINANCE_SHARED missing or inactive. Review catalog first.',1;
SET @definition=N'{"profile":"PAYMENT_ORACLE_XXAPR015_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_FINANCE_SIGNOFF","sheet":"in","headerStart":15,"headerEnd":17,"dataStart":19}'; SET @policy=N'{"purpose":"DEV_FINANCE4_SOURCE_VALIDATION","ingestionMode":"SNAPSHOT","financePeriodMode":"EXPLICIT_RANGE","coverageStart":"2026-01-01","coverageEndExclusive":"2026-09-01","allowCachedAmountFormulas":false,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='SUPPLIER_PAYMENT_REGISTER' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='SUPPLIER_PAYMENT_REGISTER' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 940001 differs for SUPPLIER_PAYMENT_REGISTER; never edit an existing version to pass a test.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('SUPPLIER_PAYMENT_REGISTER',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only.');
END;
SET @RequirementId=NULL;
SELECT @RequirementId=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='SUPPLIER_PAYMENT_REGISTER' AND ScopeCode='FINANCE_SHARED';
IF @RequirementId IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,SubmissionRuleId,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'SUPPLIER_PAYMENT_REGISTER','FINANCE_SHARED',NULL,@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV explicit source coverage; no reminders or business sign-off.');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@RequirementId AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing SUPPLIER_PAYMENT_REGISTER requirement differs; seed does not overwrite it.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='EX_USD' AND s.OwnerRoleCode='FINANCE' AND s.IsActive=1 AND sc.ScopeCode='FINANCE_SHARED')
 THROW 51000,'Catalog EX_USD/FINANCE_SHARED missing or inactive. Review catalog first.',1;
SET @definition=N'{"profile":"EX_USD_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_FINANCE_SIGNOFF","sheet":"2026 USD","headerStart":2,"headerEnd":2,"dataStart":3}'; SET @policy=N'{"purpose":"DEV_FINANCE4_SOURCE_VALIDATION","ingestionMode":"SNAPSHOT","financePeriodMode":"EXPLICIT_RANGE","coverageStart":"2026-01-01","coverageEndExclusive":"2026-08-01","allowCachedAmountFormulas":true,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='EX_USD' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='EX_USD' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 940001 differs for EX_USD; never edit an existing version to pass a test.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('EX_USD',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only.');
END;
SET @RequirementId=NULL;
SELECT @RequirementId=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='EX_USD' AND ScopeCode='FINANCE_SHARED';
IF @RequirementId IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,SubmissionRuleId,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'EX_USD','FINANCE_SHARED',NULL,@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV explicit source coverage; no reminders or business sign-off.');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@RequirementId AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing EX_USD requirement differs; seed does not overwrite it.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='EX_VND' AND s.OwnerRoleCode='FINANCE' AND s.IsActive=1 AND sc.ScopeCode='FINANCE_SHARED')
 THROW 51000,'Catalog EX_VND/FINANCE_SHARED missing or inactive. Review catalog first.',1;
SET @definition=N'{"profile":"EX_VND_WORKBOOK_V1","schemaVersion":1,"purpose":"DEV_LAYOUT_CHECK_NOT_FINANCE_SIGNOFF","sheet":"2026 VND","headerStart":1,"headerEnd":1,"dataStart":2}'; SET @policy=N'{"purpose":"DEV_FINANCE4_SOURCE_VALIDATION","ingestionMode":"SNAPSHOT","financePeriodMode":"EXPLICIT_RANGE","coverageStart":"2026-01-01","coverageEndExclusive":"2026-08-01","allowCachedAmountFormulas":false,"maxBytes":10485760,"maxRows":10000,"businessPolicySignedOff":false,"remindersEnabled":false}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='EX_VND' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='EX_VND' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 940001 differs for EX_VND; never edit an existing version to pass a test.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('EX_VND',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV technical validation only.');
END;
SET @RequirementId=NULL;
SELECT @RequirementId=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
 WHERE PeriodId=@PeriodId AND SourceCode='EX_VND' AND ScopeCode='FINANCE_SHARED';
IF @RequirementId IS NULL
BEGIN
 INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,SubmissionRuleId,ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
 VALUES(@PeriodId,'EX_VND','FINANCE_SHARED',NULL,@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@policy);
 INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES('SYSTEM','FINANCE4_DEV_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@policy,N'DEV explicit source coverage; no reminders or business sign-off.');
END
ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@RequirementId AND PolicySnapshotJson=@policy
 AND ResponsibleUserId=@OwnerId AND DueAtUtc IS NULL AND SubmissionRuleId IS NULL AND IsRequired=1)
 THROW 51000,'Existing EX_VND requirement differs; seed does not overwrite it.',1;
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT SourceCode,TemplateVersionId,VersionNo,Status FROM cfg.TemplateVersion WHERE VersionNo=@Version;
SELECT p.PeriodCode,r.RequirementId,r.SourceCode,r.ActiveBatchId,r.PolicySnapshotJson
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
WHERE p.PeriodId=@PeriodId AND r.SourceCode IN('AP_REPORT','SUPPLIER_PAYMENT_REGISTER','EX_VND','EX_USD');
