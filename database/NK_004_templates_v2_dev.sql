/* DEV only. Adds immutable V2 templates; no table/requirement/active changes. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'src.LogisticsNkLine') IS NULL THROW 51000,'Run NK_001 first.',1;
DECLARE @OwnerUpn nvarchar(320)=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN';
DECLARE @OwnerId bigint,@Version int=950002,@definition nvarchar(max);
IF @OwnerUpn=N'REPLACE_WITH_YOUR_TESTED_FINANCE_UPN' THROW 51000,'Set OwnerUpn to an active tested FINANCE user.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsNk.Seed.V2',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Seed lock unavailable.',1;
IF (SELECT COUNT(*) FROM sec.AppUser u WITH(UPDLOCK,HOLDLOCK) WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='FINANCE'))<>1
 THROW 51000,'Owner UPN must match exactly one active FINANCE user. No roles are granted.',1;
SELECT @OwnerId=u.UserId FROM sec.AppUser u WHERE u.Upn=@OwnerUpn AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='FINANCE');

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='NK_1000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_1000')
 THROW 51000,'NK_1000 catalog missing. Run NK_001/NK_002.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_1000' AND VersionNo=950001)
 THROW 51000,'NK_1000 V1 template missing. Run NK_002 first.',1;
SET @definition=N'{"profile":"NK_1000_WORKBOOK_V2","schemaVersion":2,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"2026","headerStart":1,"headerEnd":2,"dataStart":3,"formulaPolicy":"CACHED_VALUE_ONLY"}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='NK_1000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_1000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 950002 differs. Do not overwrite an existing version.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('NK_1000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV V2 parser validation; cached values not business-approved');
END;
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT TemplateVersionId,SourceCode,VersionNo,Status,DefinitionJson
FROM cfg.TemplateVersion WHERE SourceCode IN('NK_1000','NK_6000') AND VersionNo IN(950001,950002)
ORDER BY SourceCode,VersionNo;

IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
 WHERE s.SourceCode='NK_6000' AND s.IsActive=1 AND s.OwnerRoleCode='LOGISTICS' AND sc.ScopeCode='SITE_6000')
 THROW 51000,'NK_6000 catalog missing. Run NK_001/NK_002.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_6000' AND VersionNo=950001)
 THROW 51000,'NK_6000 V1 template missing. Run NK_002 first.',1;
SET @definition=N'{"profile":"NK_6000_WORKBOOK_V2","schemaVersion":2,"purpose":"DEV_LAYOUT_CHECK_NOT_BUSINESS_SIGNOFF","sheet":"2026 - NK","headerStart":1,"headerEnd":1,"dataStart":2,"formulaPolicy":"CACHED_VALUE_ONLY"}';
IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='NK_6000' AND VersionNo=@Version)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='NK_6000' AND VersionNo=@Version AND Status='APPROVED' AND DefinitionJson=@definition)
  THROW 51000,'Template 950002 differs. Do not overwrite an existing version.',1;
END
ELSE
BEGIN
 INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
 VALUES('NK_6000',@Version,'APPROVED',@definition,@OwnerId,SYSUTCDATETIME());
 INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
 VALUES(@OwnerId,'USER','NK_DEV_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@definition,N'DEV V2 parser validation; cached values not business-approved');
END;
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT TemplateVersionId,SourceCode,VersionNo,Status,DefinitionJson
FROM cfg.TemplateVersion WHERE SourceCode IN('NK_1000','NK_6000') AND VersionNo IN(950001,950002)
ORDER BY SourceCode,VersionNo;
