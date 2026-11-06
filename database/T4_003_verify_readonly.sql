USE [FinanceLogistics_Dev];
SET NOCOUNT ON;
SELECT p.PeriodCode,p.Status,p.DataRevision,r.RequirementId,r.SourceCode,r.ScopeCode,r.ActiveBatchId,CONVERT(varchar(18),CONVERT(varbinary(8),r.RowVersion),1) AS RequirementRowVersion,r.PolicySnapshotJson
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId WHERE r.SourceCode IN('OTHER_IMPORT_6000','OTHER_EXPORT_6000','XK_1000','XK_6000','PIN_TEST_1000');
SELECT b.BatchId,b.RequirementId,b.SourceCode,b.Status,b.TotalRows,b.ErrorCount,b.WarningCount,j.JobId,j.Status AS JobStatus,j.LeaseOwner,j.LeaseUntilUtc,j.AttemptCount,f.OriginalName,CONVERT(varchar(64),f.Sha256,2) AS Sha256,
 (SELECT COUNT(*) FROM ingest.RawRow r WHERE r.BatchId=b.BatchId) AS RawRows,
 (SELECT COUNT(*) FROM src.OtherTradeLine l WHERE l.BatchId=b.BatchId)+(SELECT COUNT(*) FROM src.ExportLogisticsLine l WHERE l.BatchId=b.BatchId)+(SELECT COUNT(*) FROM src.PinTestLine l WHERE l.BatchId=b.BatchId) AS TypedRows
FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
LEFT JOIN ops.ProcessingJob j ON j.JobType='TRADE_VALIDATE' AND j.IdempotencyKey=CONCAT('trade-validate:',b.BatchId)
WHERE b.SourceCode IN('OTHER_IMPORT_6000','OTHER_EXPORT_6000','XK_1000','XK_6000','PIN_TEST_1000') ORDER BY b.BatchId DESC;
