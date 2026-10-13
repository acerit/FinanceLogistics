/* READ ONLY. Select the period used by your samples. Run at each checkpoint. */
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @PeriodCode char(7)='2026-10';
SELECT p.PeriodCode,p.Status,p.DataRevision,r.RequirementId,r.ActiveBatchId,
       b.VersionNo AS ActiveVersion,b.Status AS ActiveStatus,
       (SELECT COUNT(*) FROM src.PoUpdateLine l WHERE l.BatchId=r.ActiveBatchId) AS ActivePoRows
FROM cfg.ReportingPeriod p JOIN ingest.SubmissionRequirement r ON r.PeriodId=p.PeriodId
LEFT JOIN ingest.ImportBatch b ON b.BatchId=r.ActiveBatchId
WHERE p.PeriodCode=@PeriodCode AND r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED';

SELECT b.BatchId,b.VersionNo,b.Status,b.TotalRows,b.ErrorCount,b.WarningCount,b.ClientUploadId,
       f.OriginalName,f.StorageKey,f.SizeBytes,CONVERT(varchar(64),f.Sha256,2) AS SHA256,
       j.Status AS JobStatus,j.AttemptCount,j.LeaseUntilUtc,j.LastErrorCode,
       (SELECT COUNT(*) FROM ingest.RawRow rr WHERE rr.BatchId=b.BatchId) AS StoredRawRows,
       (SELECT COUNT(*) FROM src.PoUpdateLine l WHERE l.BatchId=b.BatchId) AS StoredTypedRows
FROM ingest.ImportBatch b JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
JOIN ingest.StoredFile f ON f.FileId=b.FileId
LEFT JOIN ops.ProcessingJob j ON j.IdempotencyKey=CONCAT('po-validate:',b.BatchId)
WHERE p.PeriodCode=@PeriodCode AND r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED'
ORDER BY b.VersionNo;

SELECT b.VersionNo,i.Severity,rr.SheetName,rr.RecordNumber,i.ColumnName,i.IssueCode,i.Message
FROM ingest.ImportIssue i JOIN ingest.ImportBatch b ON b.BatchId=i.BatchId
JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
LEFT JOIN ingest.RawRow rr ON rr.RawRowId=i.RawRowId
WHERE p.PeriodCode=@PeriodCode AND r.SourceCode='PO_UPDATED' ORDER BY b.VersionNo,i.IssueId;

-- Invariant: this query must return ZERO rows.
SELECT r.RequirementId,r.ActiveBatchId,b.Status,b.ErrorCount,b.TotalRows
FROM ingest.SubmissionRequirement r JOIN ingest.ImportBatch b ON b.BatchId=r.ActiveBatchId
WHERE r.SourceCode='PO_UPDATED' AND
 (b.RequirementId<>r.RequirementId OR b.Status<>'COMMITTED' OR b.ErrorCount<>0 OR b.TotalRows<=0);

-- Application reports must select the active batch, not all historical rows.
SELECT p.PeriodCode,b.VersionNo,l.*
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
JOIN ingest.ImportBatch b ON b.BatchId=r.ActiveBatchId AND b.Status='COMMITTED'
JOIN src.PoUpdateLine l ON l.BatchId=b.BatchId
WHERE p.PeriodCode=@PeriodCode AND r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED';
