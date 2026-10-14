/* READ ONLY. Set the BatchId shown on the page. This checks real PO sample evidence, not business approval. */
SET NOCOUNT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
DECLARE @BatchId bigint=NULL; -- REPLACE with the actual Finance batch ID; never assume 1.
IF @BatchId IS NULL THROW 51000,'Set @BatchId from the upload screen first.',1;
IF NOT EXISTS(SELECT 1 FROM ingest.ImportBatch WHERE BatchId=@BatchId AND SourceCode='PO_UPDATED'
    AND JSON_VALUE(ParsingOptionsJson,'$.profile')='PO_FINANCE_XLSX_V1')
    THROW 51000,'Batch is not a Finance PO profile batch.',1;
SELECT b.BatchId,b.VersionNo,b.Status,b.TotalRows,b.ErrorCount,b.WarningCount,b.ParsingOptionsJson,
    f.OriginalName,CONVERT(varchar(64),f.Sha256,2) AS Sha256,r.ActiveBatchId,p.DataRevision,j.Status AS JobStatus,
    j.AttemptCount,j.LastErrorCode
FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
LEFT JOIN ops.ProcessingJob j ON j.JobType='PO_VALIDATE' AND j.IdempotencyKey=CONCAT('po-validate:',b.BatchId)
WHERE b.BatchId=@BatchId;
SELECT N'Counts and source coverage' AS [CheckPoint],
    (SELECT COUNT(*) FROM ingest.RawRow WHERE BatchId=@BatchId) AS RawRows,
    COUNT(*) AS TypedRows,MIN(TransactionDate) AS MinTrxDate,MAX(TransactionDate) AS MaxTrxDate
FROM src.PoUpdateLine WHERE BatchId=@BatchId;
SELECT Severity,IssueCode,COUNT(*) AS IssueCount FROM ingest.ImportIssue WHERE BatchId=@BatchId
GROUP BY Severity,IssueCode ORDER BY Severity,IssueCode;
SELECT rr.RecordNumber,l.PoRateDate,l.PoUnitPrice,l.PurchaseFunctionalAmount,l.InvoiceNumber,l.InvoiceAmount,l.InvoiceFunctionalAmount
FROM src.PoUpdateLine l JOIN ingest.RawRow rr ON rr.RawRowId=l.RawRowId
WHERE l.BatchId=@BatchId AND rr.RecordNumber IN(5,118,241,458,560,839,1044,1045)
ORDER BY rr.RecordNumber;
SELECT rr.RecordNumber,c.[key] AS ZeroBasedCellIndex,JSON_VALUE(c.value,'$.cellReference') AS CellReference,
    JSON_VALUE(c.value,'$.text') AS RawText,JSON_VALUE(c.value,'$.formula') AS Formula,
    JSON_VALUE(c.value,'$.cachedValue') AS CachedValue
FROM ingest.RawRow rr CROSS APPLY OPENJSON(rr.CellsJson) c
WHERE rr.BatchId=@BatchId AND rr.RecordNumber IN(3,118,241,839)
    AND JSON_VALUE(c.value,'$.ordinal') IN('9','13','14') ORDER BY rr.RecordNumber,CONVERT(int,c.[key]);
SELECT b.BatchId,b.VersionNo,b.Status,CASE WHEN b.BatchId=r.ActiveBatchId THEN 1 ELSE 0 END AS IsCurrent,
    (SELECT COUNT(*) FROM src.PoUpdateLine l WHERE l.BatchId=b.BatchId) AS TypedRows
FROM ingest.ImportBatch b JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
WHERE b.RequirementId=(SELECT RequirementId FROM ingest.ImportBatch WHERE BatchId=@BatchId) ORDER BY b.VersionNo;
