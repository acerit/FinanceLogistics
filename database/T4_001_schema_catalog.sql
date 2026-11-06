/* DEV only; additive tables. No changes to PO/Finance/Identity or existing active versions. */
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'ingest.ImportBatch') IS NULL OR OBJECT_ID(N'ingest.RawRow') IS NULL THROW 51000,'Tested import foundation required.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsTrade.Schema.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Migration lock unavailable.',1;
IF OBJECT_ID(N'src.OtherTradeLine',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.OtherTradeLine') AND minor_id=0 AND name=N'LogisticsTradeSchema' AND CONVERT(nvarchar(100),value)=N'1') THROW 51000,'OtherTradeLine already exists outside this patch; compare DDL, do not drop.',1;
IF OBJECT_ID(N'src.OtherTradeLine',N'U') IS NULL
BEGIN
 CREATE TABLE src.OtherTradeLine(
 SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OtherTradeLine PRIMARY KEY,
 BatchId bigint NOT NULL,RawRowId bigint NOT NULL,SourceCode varchar(40) NOT NULL,
 [SourceCodeText] nvarchar(4000) NULL,
 [TransactionType] nvarchar(4000) NULL,
 [VendorName] nvarchar(4000) NULL,
 [PoNumber] nvarchar(500) NULL,
 [PoDate] date NULL,
 [InvoiceNumber] nvarchar(500) NULL,
 [InvoiceDate] date NULL,
 [ReceiptNumber] nvarchar(500) NULL,
 [ReceiptDate] date NULL,
 [Quantity] decimal(38,18) NULL,
 [DeclarationUnitPrice] decimal(38,18) NULL,
 [TotalAmount] decimal(38,18) NULL,
 [Currency] nvarchar(500) NULL,
 [DeclarationRate] decimal(38,18) NULL,
 [VndAmount] decimal(38,18) NULL,
 [PartNumber] nvarchar(500) NULL,
 [LineDescription] nvarchar(4000) NULL,
 [AwbNumber] nvarchar(500) NULL,
 [DeclarationNumber] nvarchar(500) NULL,
 [DeclarationDate] date NULL,
 [DeclarationType] nvarchar(500) NULL,
 [Freight] decimal(38,18) NULL,
 [ImportTax] decimal(38,18) NULL,
 [TaxableAmount] decimal(38,18) NULL,
 [TaxRate] decimal(38,18) NULL,
 [VatAmount] decimal(38,18) NULL,
 [TaxPaymentNumber] nvarchar(500) NULL,
 [TaxPaymentDate] date NULL,
 [SourceUserCreate] nvarchar(4000) NULL,
 CONSTRAINT UQ_OtherTradeLine_Raw UNIQUE(RawRowId),
 CONSTRAINT CK_OtherTradeLine_Source CHECK(SourceCode IN('OTHER_IMPORT_6000','OTHER_EXPORT_6000')),
 CONSTRAINT FK_OtherTradeLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
 CONSTRAINT FK_OtherTradeLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId));
 CREATE INDEX IX_OtherTradeLine_Batch ON src.OtherTradeLine(BatchId,SourceLineId);
 EXEC sys.sp_addextendedproperty @name=N'LogisticsTradeSchema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'OtherTradeLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine'))<>33 THROW 51000,'Column count mismatch: OtherTradeLine.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'SourceCodeText' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: OtherTradeLine.SourceCodeText.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TransactionType' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: OtherTradeLine.TransactionType.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'VendorName' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: OtherTradeLine.VendorName.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'PoNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.PoNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'PoDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: OtherTradeLine.PoDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'InvoiceNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.InvoiceNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'InvoiceDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: OtherTradeLine.InvoiceDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'ReceiptNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.ReceiptNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'ReceiptDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: OtherTradeLine.ReceiptDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'Quantity' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.Quantity.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'DeclarationUnitPrice' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.DeclarationUnitPrice.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TotalAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.TotalAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'Currency' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.Currency.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'DeclarationRate' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.DeclarationRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'VndAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.VndAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'PartNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.PartNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'LineDescription' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: OtherTradeLine.LineDescription.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'AwbNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.AwbNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'DeclarationNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.DeclarationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'DeclarationDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: OtherTradeLine.DeclarationDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'DeclarationType' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.DeclarationType.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'Freight' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.Freight.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'ImportTax' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.ImportTax.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TaxableAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.TaxableAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TaxRate' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.TaxRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'VatAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: OtherTradeLine.VatAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TaxPaymentNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: OtherTradeLine.TaxPaymentNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'TaxPaymentDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: OtherTradeLine.TaxPaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'SourceUserCreate' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: OtherTradeLine.SourceUserCreate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'FK_OtherTradeLine_Batch' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: OtherTradeLine.Batch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.OtherTradeLine') AND name=N'FK_OtherTradeLine_Raw' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: OtherTradeLine.Raw.',1;
IF OBJECT_ID(N'src.ExportLogisticsLine',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.ExportLogisticsLine') AND minor_id=0 AND name=N'LogisticsTradeSchema' AND CONVERT(nvarchar(100),value)=N'1') THROW 51000,'ExportLogisticsLine already exists outside this patch; compare DDL, do not drop.',1;
IF OBJECT_ID(N'src.ExportLogisticsLine',N'U') IS NULL
BEGIN
 CREATE TABLE src.ExportLogisticsLine(
 SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExportLogisticsLine PRIMARY KEY,
 BatchId bigint NOT NULL,RawRowId bigint NOT NULL,SourceCode varchar(40) NOT NULL,
 [SequenceReference] nvarchar(500) NULL,
 [CustomerCode] nvarchar(500) NULL,
 [OrderReference] nvarchar(500) NULL,
 [CustomerName] nvarchar(4000) NULL,
 [Address] nvarchar(4000) NULL,
 [TaxNumber] nvarchar(500) NULL,
 [CreatedDate] date NULL,
 [IssuedDate] date NULL,
 [InvoiceSeries] nvarchar(500) NULL,
 [InvoiceNumber] nvarchar(500) NULL,
 [InvoiceStatus] nvarchar(4000) NULL,
 [PaymentMethod] nvarchar(4000) NULL,
 [ProductDescription] nvarchar(4000) NULL,
 [UnitOfMeasure] nvarchar(4000) NULL,
 [Quantity] decimal(38,18) NULL,
 [UnitPrice] decimal(38,18) NULL,
 [Amount] decimal(38,18) NULL,
 [Discount] decimal(38,18) NULL,
 [VatRate] nvarchar(4000) NULL,
 [VatAmount] decimal(38,18) NULL,
 [TotalAmount] decimal(38,18) NULL,
 [NoCharge] bit NULL,
 [TransactionType] nvarchar(4000) NULL,
 [InvoiceRate] decimal(38,18) NULL,
 [Note] nvarchar(4000) NULL,
 [MonthText] nvarchar(4000) NULL,
 [ContractNumber] nvarchar(500) NULL,
 [ContractDate] date NULL,
 [DeclarationNumber] nvarchar(500) NULL,
 [DeclarationDate] date NULL,
 [DeclarationRate] decimal(38,18) NULL,
 [BankPaymentReference] nvarchar(500) NULL,
 [OffsetReference] nvarchar(500) NULL,
 [PaymentDate] date NULL,
 [AwbNumber] nvarchar(500) NULL,
 CONSTRAINT UQ_ExportLogisticsLine_Raw UNIQUE(RawRowId),
 CONSTRAINT CK_ExportLogisticsLine_Source CHECK(SourceCode IN('XK_6000','XK_1000')),
 CONSTRAINT FK_ExportLogisticsLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
 CONSTRAINT FK_ExportLogisticsLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId));
 CREATE INDEX IX_ExportLogisticsLine_Batch ON src.ExportLogisticsLine(BatchId,SourceLineId);
 EXEC sys.sp_addextendedproperty @name=N'LogisticsTradeSchema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'ExportLogisticsLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine'))<>39 THROW 51000,'Column count mismatch: ExportLogisticsLine.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'SequenceReference' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.SequenceReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'CustomerCode' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.CustomerCode.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'OrderReference' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.OrderReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'CustomerName' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.CustomerName.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'Address' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.Address.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'TaxNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.TaxNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'CreatedDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.CreatedDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'IssuedDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.IssuedDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'InvoiceSeries' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.InvoiceSeries.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'InvoiceNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.InvoiceNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'InvoiceStatus' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.InvoiceStatus.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'PaymentMethod' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.PaymentMethod.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'ProductDescription' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.ProductDescription.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'UnitOfMeasure' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.UnitOfMeasure.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'Quantity' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.Quantity.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'UnitPrice' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.UnitPrice.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'Amount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.Amount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'Discount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.Discount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'VatRate' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.VatRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'VatAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.VatAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'TotalAmount' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.TotalAmount.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'NoCharge' AND TYPE_NAME(user_type_id)='bit' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.NoCharge.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'TransactionType' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.TransactionType.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'InvoiceRate' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.InvoiceRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'Note' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.Note.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'MonthText' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=8000) THROW 51000,'Column mismatch: ExportLogisticsLine.MonthText.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'ContractNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.ContractNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'ContractDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.ContractDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'DeclarationNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.DeclarationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'DeclarationDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.DeclarationDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'DeclarationRate' AND TYPE_NAME(user_type_id)='decimal' AND is_nullable=1 AND precision=38 AND scale=18) THROW 51000,'Column mismatch: ExportLogisticsLine.DeclarationRate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'BankPaymentReference' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.BankPaymentReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'OffsetReference' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.OffsetReference.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'PaymentDate' AND TYPE_NAME(user_type_id)='date' AND is_nullable=1) THROW 51000,'Column mismatch: ExportLogisticsLine.PaymentDate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'AwbNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=1000) THROW 51000,'Column mismatch: ExportLogisticsLine.AwbNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'FK_ExportLogisticsLine_Batch' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: ExportLogisticsLine.Batch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.ExportLogisticsLine') AND name=N'FK_ExportLogisticsLine_Raw' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: ExportLogisticsLine.Raw.',1;
IF OBJECT_ID(N'src.PinTestLine',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.PinTestLine') AND minor_id=0 AND name=N'LogisticsTradeSchema' AND CONVERT(nvarchar(100),value)=N'1') THROW 51000,'PinTestLine already exists outside this patch; compare DDL, do not drop.',1;
IF OBJECT_ID(N'src.PinTestLine',N'U') IS NULL
BEGIN
 CREATE TABLE src.PinTestLine(
 SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PinTestLine PRIMARY KEY,
 BatchId bigint NOT NULL,RawRowId bigint NOT NULL,SourceCode varchar(40) NOT NULL,
 [Pack] nvarchar(max) NULL,
 [Cell] nvarchar(max) NULL,
 [Manufacturer] nvarchar(max) NULL,
 [AuthorizationNumber] nvarchar(max) NULL,
 [AuthorizationDateRaw] nvarchar(max) NULL,
 [ExpiryDateRaw] nvarchar(max) NULL,
 [SourceUserCreate] nvarchar(max) NULL,
 CONSTRAINT UQ_PinTestLine_Raw UNIQUE(RawRowId),
 CONSTRAINT CK_PinTestLine_Source CHECK(SourceCode IN('PIN_TEST_1000')),
 CONSTRAINT FK_PinTestLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
 CONSTRAINT FK_PinTestLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId));
 CREATE INDEX IX_PinTestLine_Batch ON src.PinTestLine(BatchId,SourceLineId);
 EXEC sys.sp_addextendedproperty @name=N'LogisticsTradeSchema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'PinTestLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine'))<>11 THROW 51000,'Column count mismatch: PinTestLine.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'Pack' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.Pack.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'Cell' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.Cell.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'Manufacturer' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.Manufacturer.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'AuthorizationNumber' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.AuthorizationNumber.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'AuthorizationDateRaw' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.AuthorizationDateRaw.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'ExpiryDateRaw' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.ExpiryDateRaw.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'SourceUserCreate' AND TYPE_NAME(user_type_id)='nvarchar' AND is_nullable=1 AND max_length=-1) THROW 51000,'Column mismatch: PinTestLine.SourceUserCreate.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'FK_PinTestLine_Batch' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: PinTestLine.Batch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'src.PinTestLine') AND name=N'FK_PinTestLine_Raw' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Missing/disabled/untrusted FK: PinTestLine.Raw.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'6000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode=N'6000') OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode<>N'6000') THROW 51000,'Review SITE_6000; this migration grants no user access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_IMPORT_6000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('OTHER_IMPORT_6000',N'OTHER_IMPORT_6000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_IMPORT_6000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing OTHER_IMPORT_6000 differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_IMPORT_6000' AND ScopeCode<>'SITE_6000') THROW 51000,'Unexpected OTHER_IMPORT_6000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_IMPORT_6000' AND ScopeCode='SITE_6000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('OTHER_IMPORT_6000','SITE_6000');
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'6000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode=N'6000') OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode<>N'6000') THROW 51000,'Review SITE_6000; this migration grants no user access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_EXPORT_6000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('OTHER_EXPORT_6000',N'OTHER_EXPORT_6000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_EXPORT_6000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing OTHER_EXPORT_6000 differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_EXPORT_6000' AND ScopeCode<>'SITE_6000') THROW 51000,'Unexpected OTHER_EXPORT_6000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_EXPORT_6000' AND ScopeCode='SITE_6000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('OTHER_EXPORT_6000','SITE_6000');
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'6000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode=N'6000') OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_6000' AND SiteCode<>N'6000') THROW 51000,'Review SITE_6000; this migration grants no user access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='XK_6000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('XK_6000',N'XK_6000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='XK_6000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing XK_6000 differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='XK_6000' AND ScopeCode<>'SITE_6000') THROW 51000,'Unexpected XK_6000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='XK_6000' AND ScopeCode='SITE_6000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('XK_6000','SITE_6000');
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'1000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode=N'1000') OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode<>N'1000') THROW 51000,'Review SITE_1000; this migration grants no user access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='XK_1000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('XK_1000',N'XK_1000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='XK_1000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing XK_1000 differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='XK_1000' AND ScopeCode<>'SITE_1000') THROW 51000,'Unexpected XK_1000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='XK_1000' AND ScopeCode='SITE_1000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('XK_1000','SITE_1000');
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=N'1000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode=N'1000') OR EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode<>N'1000') THROW 51000,'Review SITE_1000; this migration grants no user access.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='PIN_TEST_1000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('PIN_TEST_1000',N'PIN_TEST_1000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='PIN_TEST_1000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing PIN_TEST_1000 differs; no automatic enable or owner change.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='PIN_TEST_1000' AND ScopeCode<>'SITE_1000') THROW 51000,'Unexpected PIN_TEST_1000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='PIN_TEST_1000' AND ScopeCode='SITE_1000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('PIN_TEST_1000','SITE_1000');
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
