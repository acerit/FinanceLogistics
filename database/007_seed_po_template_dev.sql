/* DEV exercise only. Not evidence of Finance approval of a production source template.
   Run once (safe to rerun without replacing data) after 004/005/006 in FinanceLogistics_Dev.
   This adds a template; it DOES NOT create/replace the current batch. */
SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'ingest.ImportBatch',N'U') IS NULL THROW 51000,'Apply 004 first.',1;
DECLARE @Upn nvarchar(320)=N'liem.phung@hsnservice.com'; -- change only if using another provisioned DEV Finance account
DECLARE @Actor bigint;
DECLARE @Count int;
SELECT @Count=COUNT(*),@Actor=MIN(u.UserId)
FROM sec.AppUser u JOIN sec.UserRole r ON r.UserId=u.UserId
WHERE u.Upn=@Upn AND u.IsActive=1 AND r.RoleCode='FINANCE';
IF @Count<>1 THROW 51000,'Expected exactly one active FINANCE account with this UPN. Do not hardcode UserId.',1;
DECLARE @Definition nvarchar(max)=N'{"profile":"PO_FLAT_ISO_V1_DEV","purpose":"TECHNICAL_EXERCISE_ONLY_NOT_FINANCE_SIGNOFF","headerRow":1,"excelSheet":"PO_UPDATED","excelSheetCount":1,"csvEncoding":"UTF-8","csvDelimiter":",","textDateFormat":"yyyy-MM-dd","decimalSeparator":".","ingestionMode":"SNAPSHOT","periodFilter":"Trx date belongs to selected month","requiredCells":["Receipt Num","Trx date","PO Num","Receipt QTY","PO Currency"],"xlsxFormulas":"REJECT","xlsFormulas":"CACHED_VALUES_WITH_WARNING","parserOwnsRules":true}';
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM cfg.TemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE SourceCode='PO_UPDATED' AND VersionNo=900001)
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM cfg.TemplateVersion WHERE SourceCode='PO_UPDATED' AND VersionNo=900001
                      AND DefinitionJson=@Definition AND Status='APPROVED')
            THROW 51000,'Version 900001 exists with different definition/status. Review; do not overwrite.',1;
    END
    ELSE
    BEGIN
        INSERT cfg.TemplateVersion(SourceCode,VersionNo,Status,DefinitionJson,ApprovedByUserId,ApprovedAtUtc)
        VALUES('PO_UPDATED',900001,'APPROVED',@Definition,@Actor,SYSUTCDATETIME());
        INSERT ops.AuditEvent(ActorKind,ActionCode,EntityType,EntityId,CorrelationId,AfterJson,Reason)
        VALUES('SYSTEM','DEV_PO_TEMPLATE_SEEDED','TemplateVersion',CONVERT(nvarchar(200),SCOPE_IDENTITY()),NEWID(),
               @Definition,N'Technical exercise seed. SQL APPROVED enables DEV workflow, not business sign-off.');
    END;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
SELECT TemplateVersionId,SourceCode,VersionNo,Status,DefinitionJson FROM cfg.TemplateVersion
WHERE SourceCode='PO_UPDATED' AND VersionNo=900001;
