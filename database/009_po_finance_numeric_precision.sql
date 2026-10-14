/* Additive DEV migration. Stop API + Worker; back up FinanceLogistics_Dev first.
   Three 28,8 columns become 38,18: the same 20 integer digits and more fractional digits.
   No data deletion, rounding, new table, period change or active-batch change. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'src.PoUpdateLine',N'U') IS NULL THROW 51000,'Existing PO schema is required.',1;
DECLARE @Columns TABLE(Name sysname PRIMARY KEY);
INSERT @Columns VALUES(N'PoUnitPrice'),(N'PurchaseFunctionalAmount'),(N'InvoiceAmount');
IF (SELECT COUNT(*) FROM sys.columns c JOIN @Columns n ON n.Name=c.name
    WHERE c.object_id=OBJECT_ID(N'src.PoUpdateLine') AND TYPE_NAME(c.system_type_id) IN(N'decimal',N'numeric')
    AND c.is_nullable=1 AND ((c.precision=28 AND c.scale=8) OR (c.precision=38 AND c.scale=18)))<>3
    THROW 51000,'Unexpected column definitions. Review actual schema; this migration will not guess.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PoUpdateLine') AND name=N'PoUnitPrice' AND scale=8)
        ALTER TABLE src.PoUpdateLine ALTER COLUMN PoUnitPrice decimal(38,18) NULL;
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PoUpdateLine') AND name=N'PurchaseFunctionalAmount' AND scale=8)
        ALTER TABLE src.PoUpdateLine ALTER COLUMN PurchaseFunctionalAmount decimal(38,18) NULL;
    IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PoUpdateLine') AND name=N'InvoiceAmount' AND scale=8)
        ALTER TABLE src.PoUpdateLine ALTER COLUMN InvoiceAmount decimal(38,18) NULL;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
SELECT c.name,TYPE_NAME(c.system_type_id) AS SqlType,c.precision,c.scale,c.is_nullable
FROM sys.columns c JOIN @Columns n ON n.Name=c.name WHERE c.object_id=OBJECT_ID(N'src.PoUpdateLine');
