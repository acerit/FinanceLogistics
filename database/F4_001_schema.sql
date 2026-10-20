/* Finance4 V1. New typed tables only. No changes to PO, Identity, SAML or active versions.
   DEV first. Rerun is safe; incompatible pre-existing proposed tables cause a clear stop. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Run against FinanceLogistics_Dev first.',1;
IF OBJECT_ID(N'ingest.ImportBatch') IS NULL OR OBJECT_ID(N'ingest.RawRow') IS NULL OR OBJECT_ID(N'ops.ProcessingJob') IS NULL
    THROW 51000,'Install/verify the existing PO foundation first.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @schemaLock int;
EXEC @schemaLock=sys.sp_getapplock @Resource=N'Finance4.Schema.V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @schemaLock<0 THROW 51000,'Could not acquire schema lock.',1;
IF OBJECT_ID(N'src.ApReportLine',N'U') IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.ApReportLine') AND minor_id=0 AND name=N'Finance4Schema' AND CONVERT(nvarchar(100),value)=N'1')
    THROW 51000,'src.ApReportLine already exists outside Finance4 V1. Compare its DDL; do not drop/rename or overwrite it.',1;
IF OBJECT_ID(N'src.SupplierPaymentLine',N'U') IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.SupplierPaymentLine') AND minor_id=0 AND name=N'Finance4Schema' AND CONVERT(nvarchar(100),value)=N'1')
    THROW 51000,'src.SupplierPaymentLine already exists outside Finance4 V1. Compare its DDL; do not drop/rename or overwrite it.',1;
IF OBJECT_ID(N'src.ExchangeUsdLine',N'U') IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.ExchangeUsdLine') AND minor_id=0 AND name=N'Finance4Schema' AND CONVERT(nvarchar(100),value)=N'1')
    THROW 51000,'src.ExchangeUsdLine already exists outside Finance4 V1. Compare its DDL; do not drop/rename or overwrite it.',1;
IF OBJECT_ID(N'src.ExchangeVndLine',N'U') IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'src.ExchangeVndLine') AND minor_id=0 AND name=N'Finance4Schema' AND CONVERT(nvarchar(100),value)=N'1')
    THROW 51000,'src.ExchangeVndLine already exists outside Finance4 V1. Compare its DDL; do not drop/rename or overwrite it.',1;
IF OBJECT_ID(N'src.ApReportLine',N'U') IS NULL
BEGIN
    CREATE TABLE src.ApReportLine(
        SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F4_ApReportLine PRIMARY KEY,
        BatchId bigint NOT NULL,RawRowId bigint NOT NULL,
        SourceCode varchar(40) NOT NULL,
        [OperatingUnit] nvarchar(200) NULL,
        [TradingPartner] nvarchar(300) NULL,
        [SupplierNumber] nvarchar(120) NULL,
        [SupplierSiteName] nvarchar(200) NULL,
        [PayeeName] nvarchar(300) NULL,
        [InvoiceType] nvarchar(80) NULL,
        [InvoiceNumber] nvarchar(200) NULL,
        [InvoiceGrossAmount] decimal(38,18) NULL,
        [FunctionalAmount] decimal(38,18) NULL,
        [HeaderTaxAmount] decimal(38,18) NULL,
        [WithheldAmount] decimal(38,18) NULL,
        [PrepaidAmount] decimal(38,18) NULL,
        [ExchangeRate] decimal(38,18) NULL,
        [LiabilityAccount] nvarchar(250) NULL,
        [InvoiceDescription] nvarchar(2000) NULL,
        [PayGroup] nvarchar(120) NULL,
        [BatchName] nvarchar(200) NULL,
        [DocumentSequence] nvarchar(120) NULL,
        [GlDate] date NULL,
        [InvoiceDate] date NULL,
        [TermsDate] date NULL,
        [PaymentTerms] nvarchar(250) NULL,
        [DueDate] date NULL,
        [InvoiceCurrency] nvarchar(10) NULL,
        [PaymentCurrency] nvarchar(10) NULL,
        [RemainingAmount] decimal(38,18) NULL,
        [InvoiceHold] nvarchar(120) NULL,
        [HoldName] nvarchar(200) NULL,
        [HoldReason] nvarchar(2000) NULL,
        [InvoiceStatus] nvarchar(120) NULL,
        CONSTRAINT UQ_F4_ApReportLine_Raw UNIQUE(RawRowId),
        CONSTRAINT CK_F4_ApReportLine_Source CHECK(SourceCode='AP_REPORT'),
        CONSTRAINT FK_F4_ApReportLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
        CONSTRAINT FK_F4_ApReportLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId)
    );
    CREATE INDEX IX_F4_ApReportLine_Batch ON src.ApReportLine(BatchId,[GlDate],SourceLineId);
    EXEC sys.sp_addextendedproperty @name=N'Finance4Schema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'ApReportLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine'))<>34
 THROW 51000,'Unexpected column count in src.ApReportLine. Compare schema before continuing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'OperatingUnit' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ApReportLine.OperatingUnit. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'TradingPartner' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ApReportLine.TradingPartner. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'SupplierNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.ApReportLine.SupplierNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'SupplierSiteName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ApReportLine.SupplierSiteName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'PayeeName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ApReportLine.PayeeName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceType' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=160)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceType. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceGrossAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceGrossAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'FunctionalAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.FunctionalAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'HeaderTaxAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.HeaderTaxAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'WithheldAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.WithheldAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'PrepaidAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.PrepaidAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'ExchangeRate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.ExchangeRate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'LiabilityAccount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=500)
 THROW 51000,'Unexpected column src.ApReportLine.LiabilityAccount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceDescription' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=4000)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceDescription. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'PayGroup' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.ApReportLine.PayGroup. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'BatchName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ApReportLine.BatchName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'DocumentSequence' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.ApReportLine.DocumentSequence. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'GlDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ApReportLine.GlDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'TermsDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ApReportLine.TermsDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'PaymentTerms' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=500)
 THROW 51000,'Unexpected column src.ApReportLine.PaymentTerms. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'DueDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ApReportLine.DueDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceCurrency' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=20)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceCurrency. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'PaymentCurrency' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=20)
 THROW 51000,'Unexpected column src.ApReportLine.PaymentCurrency. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'RemainingAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ApReportLine.RemainingAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceHold' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceHold. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'HoldName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ApReportLine.HoldName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'HoldReason' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=4000)
 THROW 51000,'Unexpected column src.ApReportLine.HoldReason. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ApReportLine') AND name=N'InvoiceStatus' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.ApReportLine.InvoiceStatus. Stop and compare DDL.',1;
IF OBJECT_ID(N'src.SupplierPaymentLine',N'U') IS NULL
BEGIN
    CREATE TABLE src.SupplierPaymentLine(
        SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F4_SupplierPaymentLine PRIMARY KEY,
        BatchId bigint NOT NULL,RawRowId bigint NOT NULL,
        SourceCode varchar(40) NOT NULL,
        [AccountName] nvarchar(200) NULL,
        [SupplierNumber] nvarchar(120) NULL,
        [SupplierName] nvarchar(300) NULL,
        [SupplierSite] nvarchar(200) NULL,
        [AlternateSiteName] nvarchar(300) NULL,
        [PaymentDate] date NULL,
        [PaymentCurrency] nvarchar(10) NULL,
        [PaymentMethod] nvarchar(120) NULL,
        [InvoiceNumber] nvarchar(200) NULL,
        [VoucherNumber] nvarchar(120) NULL,
        [InvoiceDate] date NULL,
        [GlDate] date NULL,
        [InvoiceAmount] decimal(38,18) NULL,
        [PaymentAmount] decimal(38,18) NULL,
        [FunctionalAmount] decimal(38,18) NULL,
        [DiscountAmount] decimal(38,18) NULL,
        [PaymentDocumentNumber] nvarchar(120) NULL,
        [PaymentVoucherNumber] nvarchar(120) NULL,
        [Ppr] nvarchar(300) NULL,
        [VoidStatus] nvarchar(40) NULL,
        [PayeeName] nvarchar(300) NULL,
        CONSTRAINT UQ_F4_SupplierPaymentLine_Raw UNIQUE(RawRowId),
        CONSTRAINT CK_F4_SupplierPaymentLine_Source CHECK(SourceCode='SUPPLIER_PAYMENT_REGISTER'),
        CONSTRAINT FK_F4_SupplierPaymentLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
        CONSTRAINT FK_F4_SupplierPaymentLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId)
    );
    CREATE INDEX IX_F4_SupplierPaymentLine_Batch ON src.SupplierPaymentLine(BatchId,[PaymentDate],SourceLineId);
    EXEC sys.sp_addextendedproperty @name=N'Finance4Schema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'SupplierPaymentLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine'))<>25
 THROW 51000,'Unexpected column count in src.SupplierPaymentLine. Compare schema before continuing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'AccountName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.AccountName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'SupplierNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.SupplierNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'SupplierName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.SupplierName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'SupplierSite' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.SupplierSite. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'AlternateSiteName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.AlternateSiteName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentCurrency' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=20)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentCurrency. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentMethod' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentMethod. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'InvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.InvoiceNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'VoucherNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.VoucherNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'InvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.SupplierPaymentLine.InvoiceDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'GlDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.SupplierPaymentLine.GlDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'InvoiceAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.InvoiceAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'FunctionalAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.FunctionalAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'DiscountAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.DiscountAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentDocumentNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentDocumentNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PaymentVoucherNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=240)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PaymentVoucherNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'Ppr' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.Ppr. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'VoidStatus' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=80)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.VoidStatus. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.SupplierPaymentLine') AND name=N'PayeeName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.SupplierPaymentLine.PayeeName. Stop and compare DDL.',1;
IF OBJECT_ID(N'src.ExchangeUsdLine',N'U') IS NULL
BEGIN
    CREATE TABLE src.ExchangeUsdLine(
        SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F4_ExchangeUsdLine PRIMARY KEY,
        BatchId bigint NOT NULL,RawRowId bigint NOT NULL,
        SourceCode varchar(40) NOT NULL,
        [InvoiceNumber] nvarchar(200) NULL,
        [InvoiceDate] date NULL,
        [InvoiceAmount] decimal(38,18) NULL,
        [PoNumber] nvarchar(300) NULL,
        [CustomsDeclaration] nvarchar(300) NULL,
        [CustomsDeclarationDate] date NULL,
        [BankReference] nvarchar(200) NULL,
        [BankDate] date NULL,
        [SupplierName] nvarchar(300) NULL,
        [AfsPaymentReference] nvarchar(200) NULL,
        CONSTRAINT UQ_F4_ExchangeUsdLine_Raw UNIQUE(RawRowId),
        CONSTRAINT CK_F4_ExchangeUsdLine_Source CHECK(SourceCode='EX_USD'),
        CONSTRAINT FK_F4_ExchangeUsdLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
        CONSTRAINT FK_F4_ExchangeUsdLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId)
    );
    CREATE INDEX IX_F4_ExchangeUsdLine_Batch ON src.ExchangeUsdLine(BatchId,[BankDate],SourceLineId);
    EXEC sys.sp_addextendedproperty @name=N'Finance4Schema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'ExchangeUsdLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine'))<>14
 THROW 51000,'Unexpected column count in src.ExchangeUsdLine. Compare schema before continuing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'InvoiceNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.InvoiceNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'InvoiceDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ExchangeUsdLine.InvoiceDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'InvoiceAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.InvoiceAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'PoNumber' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.PoNumber. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'CustomsDeclaration' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.CustomsDeclaration. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'CustomsDeclarationDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ExchangeUsdLine.CustomsDeclarationDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'BankReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.BankReference. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'BankDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ExchangeUsdLine.BankDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'SupplierName' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.SupplierName. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeUsdLine') AND name=N'AfsPaymentReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ExchangeUsdLine.AfsPaymentReference. Stop and compare DDL.',1;
IF OBJECT_ID(N'src.ExchangeVndLine',N'U') IS NULL
BEGIN
    CREATE TABLE src.ExchangeVndLine(
        SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F4_ExchangeVndLine PRIMARY KEY,
        BatchId bigint NOT NULL,RawRowId bigint NOT NULL,
        SourceCode varchar(40) NOT NULL,
        [BankDocument] nvarchar(300) NULL,
        [BankDate] date NULL,
        [Description] nvarchar(4000) NULL,
        [PaymentAmount] decimal(38,18) NULL,
        [InvoiceReference] nvarchar(500) NULL,
        [AfsPaymentReference] nvarchar(200) NULL,
        [BankPaymentReference] nvarchar(300) NULL,
        CONSTRAINT UQ_F4_ExchangeVndLine_Raw UNIQUE(RawRowId),
        CONSTRAINT CK_F4_ExchangeVndLine_Source CHECK(SourceCode='EX_VND'),
        CONSTRAINT FK_F4_ExchangeVndLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
        CONSTRAINT FK_F4_ExchangeVndLine_Raw FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId)
    );
    CREATE INDEX IX_F4_ExchangeVndLine_Batch ON src.ExchangeVndLine(BatchId,[BankDate],SourceLineId);
    EXEC sys.sp_addextendedproperty @name=N'Finance4Schema',@value=N'1',@level0type=N'SCHEMA',@level0name=N'src',@level1type=N'TABLE',@level1name=N'ExchangeVndLine';
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine'))<>11
 THROW 51000,'Unexpected column count in src.ExchangeVndLine. Compare schema before continuing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'BankDocument' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ExchangeVndLine.BankDocument. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'BankDate' AND is_nullable=1 AND TYPE_NAME(user_type_id)='date')
 THROW 51000,'Unexpected column src.ExchangeVndLine.BankDate. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'Description' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=8000)
 THROW 51000,'Unexpected column src.ExchangeVndLine.Description. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'PaymentAmount' AND is_nullable=1 AND TYPE_NAME(user_type_id)='decimal' AND precision=38 AND scale=18)
 THROW 51000,'Unexpected column src.ExchangeVndLine.PaymentAmount. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'InvoiceReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=1000)
 THROW 51000,'Unexpected column src.ExchangeVndLine.InvoiceReference. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'AfsPaymentReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=400)
 THROW 51000,'Unexpected column src.ExchangeVndLine.AfsPaymentReference. Stop and compare DDL.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'src.ExchangeVndLine') AND name=N'BankPaymentReference' AND is_nullable=1 AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=600)
 THROW 51000,'Unexpected column src.ExchangeVndLine.BankPaymentReference. Stop and compare DDL.',1;
COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
SELECT s.name AS SchemaName,t.name AS TableName,c.name AS ColumnName,TYPE_NAME(c.user_type_id) AS DataType,c.max_length,c.precision,c.scale
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id
WHERE s.name='src' AND t.name IN('ApReportLine','SupplierPaymentLine','ExchangeUsdLine','ExchangeVndLine') ORDER BY t.name,c.column_id;
