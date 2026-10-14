/* DEV only, reviewed real layout but NOT Finance sign-off of cumulative business policy.
   New profile 900002; preserve 900001 and all old batches. Create a separate July 2026 requirement.
   July is the source sample's latest Trx month, NOT today's month inferred from filename.
   YEAR_TO_PERIOD is an explicit DEV demonstration choice. Agree the production rule with Finance.
   Run the entire file. Reruns validate definitions; conflicting rows are never overwritten. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PoUpdateLine')
    AND name IN(N'PoUnitPrice',N'PurchaseFunctionalAmount',N'InvoiceAmount') AND precision=38 AND scale=18)<>3
    THROW 51000,'Run 009 first.',1;
DECLARE @OwnerUpn nvarchar(320)=N'liem.phung@hsnservice.com'; -- Same Finance login you already tested; edit if needed.
DECLARE @StartDate date='20260701';
DECLARE @PeriodMode varchar(30)='YEAR_TO_PERIOD'; -- For this multi-month DEV sample only.
DECLARE @Version int=900002;
DECLARE @PeriodCode char(7)=CONVERT(char(7),@StartDate,126);
DECLARE @OwnerId bigint,@PeriodId int,@RequirementId bigint;
DECLARE @Definition nvarchar(max)=N'{"profile":"PO_FINANCE_XLSX_V1","purpose":"DEV_REAL_FILE_VALIDATION_NOT_BUSINESS_SIGNOFF","excelSheet":"PO update","headerRows":[1,2],"firstDataRow":3,"columnCount":17,"currencyHeaderAlias":"PO Curr","numericPolicy":"PRESERVE_XML_VALUE_NO_ROUNDING","formulaPolicy":"INVOICE_LITERAL_MATCH_CACHE_OR_SAME_ROW_AMOUNT_MULTIPLICATION_CACHE_WITH_WARNING","parserOwnsRules":true}';
DECLARE @Policy nvarchar(max)=CONCAT(N'{"purpose":"DEV_FINANCE_PO_LAYOUT_CHECK","ingestionMode":"SNAPSHOT","maxBytes":10485760,"maxRows":10000,"financePoPeriodMode":"',@PeriodMode,N'","requireExplicitWarningsAcceptance":true,"businessPolicySignedOff":false,"remindersEnabled":false}');
IF @PeriodMode NOT IN('SELECTED_MONTH','YEAR_TO_PERIOD') OR DAY(@StartDate)<>1
    THROW 51000,'Check explicit period mode and first day of month.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF (SELECT COUNT(*) FROM sec.AppUser u WITH(UPDLOCK,HOLDLOCK) WHERE u.Upn=@OwnerUpn AND u.IsActive=1
        AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE')
        AND EXISTS(SELECT 1 FROM sec.DirectoryAccount d WHERE d.UserId=u.UserId))<>1
        THROW 51000,'OwnerUpn must match one active AD-linked FINANCE account. Do not hardcode UserId.',1;
    SELECT @OwnerId=u.UserId FROM sec.AppUser u WHERE u.Upn=@OwnerUpn AND u.IsActive=1
        AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE')
        AND EXISTS(SELECT 1 FROM sec.DirectoryAccount d WHERE d.UserId=u.UserId);
    IF NOT EXISTS(SELECT 1 FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
        WHERE s.SourceCode='PO_UPDATED' AND s.IsActive=1 AND s.OwnerRoleCode='FINANCE' AND sc.ScopeCode='FINANCE_SHARED')
        THROW 51000,'PO_UPDATED / FINANCE_SHARED catalog is missing or disabled.',1;
    IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='PO_UPDATED' AND VersionNo=@Version)
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='PO_UPDATED' AND VersionNo=@Version
            AND Status='APPROVED' AND DefinitionJson=@Definition)
            THROW 51000,'Template 900002 already differs. Review; do not edit an existing version.',1;
    END
    ELSE
    BEGIN
        INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
        VALUES('PO_UPDATED',@Version,'APPROVED',@Definition,@OwnerId,SYSUTCDATETIME());
        INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
        VALUES('SYSTEM','DEV_FINANCE_PO_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),@Definition,
            N'DEV validation of real Finance layout. APPROVED enables testing, not business acceptance.');
    END;
    SELECT @PeriodId=PeriodId FROM cfg.ReportingPeriod WITH(UPDLOCK,HOLDLOCK) WHERE PeriodCode=@PeriodCode;
    IF @PeriodId IS NULL
    BEGIN
        INSERT cfg.ReportingPeriod(PeriodCode,StartDate,EndDateExclusive,Status)
        VALUES(@PeriodCode,@StartDate,DATEADD(month,1,@StartDate),'OPEN');
        SET @PeriodId=CONVERT(int,SCOPE_IDENTITY());
    END
    ELSE IF NOT EXISTS(SELECT 1 FROM cfg.ReportingPeriod WHERE PeriodId=@PeriodId AND Status='OPEN'
        AND StartDate=@StartDate AND EndDateExclusive=DATEADD(month,1,@StartDate))
        THROW 51000,'Existing July period differs or is closed. This seed does not reopen periods.',1;
    SELECT @RequirementId=RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
    WHERE PeriodId=@PeriodId AND SourceCode='PO_UPDATED' AND ScopeCode='FINANCE_SHARED';
    IF @RequirementId IS NULL
    BEGIN
        INSERT ingest.SubmissionRequirement(PeriodId,SourceCode,ScopeCode,SubmissionRuleId,ResponsibleUserId,
            DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
        VALUES(@PeriodId,'PO_UPDATED','FINANCE_SHARED',NULL,@OwnerId,NULL,'Asia/Ho_Chi_Minh',1,@Policy);
        SET @RequirementId=CONVERT(bigint,SCOPE_IDENTITY());
        INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
        VALUES('SYSTEM','DEV_FINANCE_PO_REQUIREMENT_SEEDED','SubmissionRequirement',CONVERT(nvarchar(200),@RequirementId),NEWID(),@Policy,
            N'DEV full-sample snapshot, no deadline/email and no production sign-off.');
    END
    ELSE IF NOT EXISTS(SELECT 1 FROM ingest.SubmissionRequirement WHERE RequirementId=@RequirementId
        AND PolicySnapshotJson=@Policy AND ResponsibleUserId=@OwnerId AND SubmissionRuleId IS NULL
        AND DueAtUtc IS NULL AND TimeZoneId='Asia/Ho_Chi_Minh' AND IsRequired=1)
        THROW 51000,'Existing July requirement differs. Do not replace its policy or active batch to pass the demo.',1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
SELECT TemplateVersionId,SourceCode,VersionNo,Status,DefinitionJson FROM cfg.TemplateVersion
WHERE SourceCode='PO_UPDATED' AND VersionNo=@Version;
SELECT p.PeriodCode,p.Status,r.RequirementId,r.ActiveBatchId,r.PolicySnapshotJson
FROM cfg.ReportingPeriod p JOIN ingest.SubmissionRequirement r ON r.PeriodId=p.PeriodId WHERE r.RequirementId=@RequirementId;
