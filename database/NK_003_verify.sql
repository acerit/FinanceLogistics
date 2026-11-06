/* Read-only evidence. Set BatchId to inspect one upload; NULL lists the latest batches. */
SET NOCOUNT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @BatchId bigint=NULL;
SELECT p.PeriodCode,p.Status AS PeriodStatus,p.DataRevision,r.RequirementId,r.SourceCode,r.ScopeCode,r.ActiveBatchId,
 CONVERT(varchar(18),CONVERT(binary(8),r.RowVersion),1) AS RequirementRowVersionHex,r.PolicySnapshotJson
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
WHERE r.SourceCode IN('NK_1000','NK_6000','PO_UPDATED','AP_REPORT','SUPPLIER_PAYMENT_REGISTER','EX_VND','EX_USD')
ORDER BY p.PeriodCode,r.SourceCode;
SELECT TOP(30) b.BatchId,b.SourceCode,b.RequirementId,b.VersionNo,b.Status,b.TotalRows,b.ErrorCount,b.WarningCount,
 j.JobId,j.Status AS JobStatus,j.AttemptCount,j.LeaseOwner,j.LeaseUntilUtc,j.LastErrorCode,
 (SELECT COUNT(*) FROM ingest.RawRow rr WHERE rr.BatchId=b.BatchId) AS RawRows,
 (SELECT COUNT(*) FROM src.LogisticsNkLine l WHERE l.BatchId=b.BatchId) AS TypedRows,
 b.ClientUploadId,b.ParsingOptionsJson,f.OriginalName,CONVERT(varchar(64),f.Sha256,2) AS Sha256
FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
LEFT JOIN ops.ProcessingJob j ON j.JobType='NK_VALIDATE' AND j.IdempotencyKey=CONCAT('nk-validate:',b.BatchId)
WHERE b.SourceCode IN('NK_1000','NK_6000') AND (@BatchId IS NULL OR b.BatchId=@BatchId) ORDER BY b.BatchId DESC;
SELECT i.BatchId,i.Severity,i.IssueCode,COUNT(*) AS Events
FROM ingest.ImportIssue i JOIN ingest.ImportBatch b ON b.BatchId=i.BatchId
WHERE b.SourceCode IN('NK_1000','NK_6000') AND (@BatchId IS NULL OR b.BatchId=@BatchId)
GROUP BY i.BatchId,i.Severity,i.IssueCode ORDER BY i.BatchId DESC,i.Severity,i.IssueCode;
SELECT TOP(100) i.BatchId,rr.SheetName,rr.RecordNumber,i.ColumnName,i.Severity,i.IssueCode,i.Message
FROM ingest.ImportIssue i LEFT JOIN ingest.RawRow rr ON rr.RawRowId=i.RawRowId
JOIN ingest.ImportBatch b ON b.BatchId=i.BatchId
WHERE b.SourceCode IN('NK_1000','NK_6000') AND i.Severity='ERROR' AND (@BatchId IS NULL OR b.BatchId=@BatchId)
ORDER BY i.BatchId DESC,rr.RecordNumber,i.IssueId;
SELECT TOP(100) a.* FROM ops.AuditEvent a WHERE a.ActionCode LIKE 'NK[_]%'
AND (@BatchId IS NULL OR (a.EntityType='ImportBatch' AND a.EntityId=CONVERT(nvarchar(200),@BatchId)))
ORDER BY a.AuditId DESC;
SELECT s.SiteCode,COUNT(DISTINCT u.UserId) AS ActiveLogisticsUsersWithoutFinanceRole
FROM cfg.Site s LEFT JOIN sec.UserSite us ON us.SiteCode=s.SiteCode
LEFT JOIN sec.AppUser u ON u.UserId=us.UserId AND u.IsActive=1
 AND EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='LOGISTICS')
 AND NOT EXISTS(SELECT 1 FROM sec.UserRole r WHERE r.UserId=u.UserId AND r.RoleCode='FINANCE')
WHERE s.SiteCode IN('1000','6000') GROUP BY s.SiteCode;
