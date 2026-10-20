/* READ ONLY. Run before/after tests. No secrets, no financial detail values. */
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
SELECT p.PeriodCode,p.Status,p.DataRevision,r.RequirementId,r.SourceCode,r.ActiveBatchId,
 CONVERT(varchar(18),r.RowVersion,1) AS RequirementRowVersion
FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
WHERE r.SourceCode IN('PO_UPDATED','AP_REPORT','SUPPLIER_PAYMENT_REGISTER','EX_VND','EX_USD') ORDER BY p.PeriodCode,r.SourceCode;
SELECT b.BatchId,b.SourceCode,b.VersionNo,b.Status,b.TotalRows,b.ErrorCount,b.WarningCount,b.ClientUploadId,
 j.JobId,j.Status AS JobStatus,j.AttemptCount,j.LeaseOwner,j.LeaseUntilUtc,j.LastErrorCode,
 (SELECT COUNT(*) FROM ingest.RawRow rr WHERE rr.BatchId=b.BatchId) AS RawRows,
 (SELECT COUNT(*) FROM src.ApReportLine x WHERE x.BatchId=b.BatchId)+
 (SELECT COUNT(*) FROM src.SupplierPaymentLine x WHERE x.BatchId=b.BatchId)+
 (SELECT COUNT(*) FROM src.ExchangeUsdLine x WHERE x.BatchId=b.BatchId)+
 (SELECT COUNT(*) FROM src.ExchangeVndLine x WHERE x.BatchId=b.BatchId) AS TypedRows
FROM ingest.ImportBatch b LEFT JOIN ops.ProcessingJob j ON j.IdempotencyKey=CONCAT('finance-validate:',b.BatchId) AND j.JobType='FINANCE_VALIDATE'
WHERE b.SourceCode IN('AP_REPORT','SUPPLIER_PAYMENT_REGISTER','EX_VND','EX_USD') ORDER BY b.BatchId DESC;
SELECT b.BatchId,i.Severity,i.IssueCode,COUNT(*) AS Issues FROM ingest.ImportIssue i JOIN ingest.ImportBatch b ON b.BatchId=i.BatchId
WHERE b.SourceCode IN('AP_REPORT','SUPPLIER_PAYMENT_REGISTER','EX_VND','EX_USD') GROUP BY b.BatchId,i.Severity,i.IssueCode ORDER BY b.BatchId DESC,i.Severity,i.IssueCode;
SELECT TOP(100) ActionCode,EntityId,OccurredAtUtc,CorrelationId FROM ops.AuditEvent WHERE ActionCode LIKE 'FINANCE%' ORDER BY AuditId DESC;
