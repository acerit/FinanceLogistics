/* Read-only checks. Rows/Batch counts are expected to be zero before uploading. */
IF DB_NAME() <> N'FinanceLogistics_Dev'
    THROW 51000, 'Select FinanceLogistics_Dev.', 1;

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;

SELECT expected.TableName,
       CASE WHEN OBJECT_ID(expected.TableName,N'U') IS NULL THEN 'MISSING' ELSE 'OK' END AS Result
FROM (VALUES
 (N'cfg.TemplateVersion'),(N'cfg.SubmissionRule'),(N'cfg.ReportingPeriod'),
 (N'ingest.SubmissionRequirement'),(N'ingest.StoredFile'),(N'ingest.ImportBatch'),
 (N'ingest.RawRow'),(N'ingest.ImportIssue'),
 (N'ops.ProcessingJob'),(N'ops.AuditEvent'),(N'src.PoUpdateLine')
) expected(TableName);

SELECT p.PeriodId,p.PeriodCode,p.Status AS PeriodStatus,
       p.StartDate,p.EndDateExclusive,
       r.RequirementId,r.SourceCode,r.ScopeCode,r.DueAtUtc,r.TimeZoneId,
       r.IsRequired,r.ActiveBatchId,u.Upn AS ResponsibleUpn,
       JSON_VALUE(r.PolicySnapshotJson,'$.ingestionMode') AS IngestionMode,
       JSON_VALUE(r.PolicySnapshotJson,'$.purpose') AS PolicyPurpose
FROM cfg.ReportingPeriod p
JOIN ingest.SubmissionRequirement r ON r.PeriodId=p.PeriodId
LEFT JOIN sec.AppUser u ON u.UserId=r.ResponsibleUserId
WHERE r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED'
ORDER BY p.StartDate DESC;

SELECT
 (SELECT COUNT_BIG(*) FROM ingest.ImportBatch WHERE SourceCode='PO_UPDATED') AS PoBatchCount,
 (SELECT COUNT_BIG(*) FROM src.PoUpdateLine) AS PoLineCount;
