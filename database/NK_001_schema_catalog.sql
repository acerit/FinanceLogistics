/* NK V1: additive DEV migration. No updates to PO/Finance/Identity, no data backfill. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'ingest.ImportBatch') IS NULL OR OBJECT_ID(N'ingest.RawRow') IS NULL
 OR COL_LENGTH(N'ingest.ImportBatch',N'ClientUploadId') IS NULL
 THROW 51000,'The tested import foundation is required.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsNk.Schema.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Migration lock unavailable.',1;
IF OBJECT_ID(N'src.LogisticsNkLine',N'U') IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.LogisticsNkLine')
 AND minor_id=0 AND name=N'LogisticsNkSchema' AND CONVERT(nvarchar(100),value)=N'1')
 THROW 51000,'LogisticsNkLine exists outside this patch. Compare DDL; do not drop or overwrite it.',1;
IF OBJECT_ID(N'src.LogisticsNkLine',N'U') IS NULL
BEGIN
 CREATE TABLE src.LogisticsNkLine(
 SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogisticsNkLine PRIMARY KEY,
 BatchId bigint NOT NULL, RawRowId bigint NOT NULL, SourceCode varchar(40) NOT NULL,
 [SourceCodeText] nvarchar(100) NULL,
 [ReceiptNumber] nvarchar(500) NULL,
 [TransactionDate] date NULL,
 [PoNumber] nvarchar(500) NULL,
 [ReceiptQuantity] decimal(38,18) NULL,
 [PoUnitPrice] decimal(38,18) NULL,
 [PoLineReference] nvarchar(500) NULL,
 [PoCurrency] nvarchar(10) NULL,
 [PoRateDate] date NULL,
 [PoRate] decimal(38,18) NULL,
 [ReceiptRate] decimal(38,18) NULL,
 [ReceiptRateDate] date NULL,
 [PurchaseFunctionalAmount] decimal(38,18) NULL,
 [InvoiceNumber] nvarchar(500) NULL,
 [InvoiceFunctionalAmount] decimal(38,18) NULL,
 [InvoiceAmount] decimal(38,18) NULL,
 [LineDescription] nvarchar(4000) NULL,
 [PartNumber] nvarchar(500) NULL,
 [OriginalAmount] decimal(38,18) NULL,
 [BookingRate] decimal(38,18) NULL,
 [VndConvert] decimal(38,18) NULL,
 [InvoiceDate] date NULL,
 [VendorName] nvarchar(500) NULL,
 [ProductLine] nvarchar(500) NULL,
 [PaymentNumber] nvarchar(500) NULL,
 [OffsetReference] nvarchar(500) NULL,
 [PaymentDate] date NULL,
 [AwbBlNumber] nvarchar(500) NULL,
 [DeclarationNumber] nvarchar(500) NULL,
 [FinalDeclarationNumber] nvarchar(500) NULL,
 [DeclarationDate] date NULL,
 [DeclarationType] nvarchar(500) NULL,
 [ContractNumber] nvarchar(500) NULL,
 [ContractDate] date NULL,
 [Freight] decimal(38,18) NULL,
 [ImportTax] decimal(38,18) NULL,
 [TaxableAmount] decimal(38,18) NULL,
 [DeclarationRate] decimal(38,18) NULL,
 [VatRate] decimal(38,18) NULL,
 [VatAmount] decimal(38,18) NULL,
 [TaxPaymentNumber] nvarchar(500) NULL,
 [TaxPaymentDate] date NULL,
 [Rmn] nvarchar(500) NULL,
 [EecNumber] nvarchar(500) NULL,
 [EecDate] date NULL,
 [EecInvoiceNumber] nvarchar(500) NULL,
 [EecInvoiceDate] date NULL,
 [EecPaymentDate] date NULL,
 [CocNumber] nvarchar(500) NULL,
 [CocDate] date NULL,
 [CocExpiryDate] date NULL,
 [CocInvoiceNumber] nvarchar(500) NULL,
 [CocInvoiceDate] date NULL,
 [CocPaymentDate] date NULL,
 [ConformityDeclarationNumber] nvarchar(500) NULL,
 [ConformityDeclarationDate] date NULL,
 CONSTRAINT UQ_LogisticsNkLine_Raw UNIQUE(RawRowId),
 CONSTRAINT CK_LogisticsNkLine_Source CHECK(SourceCode IN('NK_1000','NK_6000')),
 CONSTRAINT FK_LogisticsNkLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
 CONSTRAINT FK_LogisticsNkLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId)
 );
 CREATE INDEX IX_LogisticsNkLine_Batch ON src.LogisticsNkLine(BatchId,SourceLineId);
 EXEC sys.sp_addextendedproperty @name=N'LogisticsNkSchema',@value=N'1',
  @level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'LogisticsNkLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine'))<>60
 THROW 51000,'Unexpected LogisticsNkLine column count; stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'SourceCodeText' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=200)
 THROW 51000,'Column mismatch: SourceCodeText.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ReceiptNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: ReceiptNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'TransactionDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: TransactionDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: PoNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ReceiptQuantity' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: ReceiptQuantity.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoUnitPrice' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: PoUnitPrice.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoLineReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: PoLineReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoCurrency' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=20)
 THROW 51000,'Column mismatch: PoCurrency.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoRateDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: PoRateDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PoRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: PoRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ReceiptRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: ReceiptRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ReceiptRateDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: ReceiptRateDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PurchaseFunctionalAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: PurchaseFunctionalAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'InvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: InvoiceNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'InvoiceFunctionalAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: InvoiceFunctionalAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'InvoiceAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: InvoiceAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'LineDescription' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=8000)
 THROW 51000,'Column mismatch: LineDescription.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PartNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: PartNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'OriginalAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: OriginalAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'BookingRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: BookingRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'VndConvert' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: VndConvert.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'InvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: InvoiceDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'VendorName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: VendorName.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ProductLine' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: ProductLine.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PaymentNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: PaymentNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'OffsetReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: OffsetReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'PaymentDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: PaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'AwbBlNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: AwbBlNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'DeclarationNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: DeclarationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'FinalDeclarationNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: FinalDeclarationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'DeclarationDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: DeclarationDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'DeclarationType' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: DeclarationType.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ContractNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: ContractNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ContractDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: ContractDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'Freight' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: Freight.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ImportTax' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: ImportTax.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'TaxableAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: TaxableAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'DeclarationRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: DeclarationRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'VatRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: VatRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'VatAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Column mismatch: VatAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'TaxPaymentNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: TaxPaymentNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'TaxPaymentDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: TaxPaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'Rmn' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: Rmn.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'EecNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: EecNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'EecDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: EecDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'EecInvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: EecInvoiceNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'EecInvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: EecInvoiceDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'EecPaymentDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: EecPaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: CocNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: CocDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocExpiryDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: CocExpiryDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocInvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: CocInvoiceNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocInvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: CocInvoiceDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'CocPaymentDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: CocPaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ConformityDeclarationNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Column mismatch: ConformityDeclarationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine') AND name=N'ConformityDeclarationDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Column mismatch: ConformityDeclarationDate.',1;

IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'1000' AND IsActive=1)
 OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode=N'1000')
 OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode<>N'1000')
 THROW 51000,'Review existing SITE_1000 configuration; this migration does not grant site access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='NK_1000')
 INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('NK_1000',N'Nhập khẩu site 1000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='NK_1000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1)
 THROW 51000,'Existing NK_1000 source differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='NK_1000' AND ScopeCode<>'SITE_1000')
 THROW 51000,'Unexpected NK_1000 scope mapping.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='NK_1000' AND ScopeCode='SITE_1000')
 INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('NK_1000','SITE_1000');

IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'6000' AND IsActive=1)
 OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode=N'6000')
 OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode<>N'6000')
 THROW 51000,'Review existing SITE_6000 configuration; this migration does not grant site access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='NK_6000')
 INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('NK_6000',N'Nhập khẩu site 6000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='NK_6000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1)
 THROW 51000,'Existing NK_6000 source differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='NK_6000' AND ScopeCode<>'SITE_6000')
 THROW 51000,'Unexpected NK_6000 scope mapping.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='NK_6000' AND ScopeCode='SITE_6000')
 INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('NK_6000','SITE_6000');
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT SourceCode,SourceName,OwnerRoleCode,IsActive FROM cfg.SourceType WHERE SourceCode IN('NK_1000','NK_6000');
SELECT COUNT(*) AS TypedColumnCount FROM sys.columns WHERE object_id=OBJECT_ID(N'src.LogisticsNkLine');
