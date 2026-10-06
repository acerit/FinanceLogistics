/* M1 teaching migration, 2026-10-05. Intended database: FinanceLogistics_Dev.
   Run AFTER Identity 001/002; only on a DB without import tables.
   This script is NOT a migration of an already-installed full V1/custom schema.
   No application SQL/AD runtime test has been performed in the authoring environment.
   No source file or user data is deleted. Review in SSMS before execution. */
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev before running this teaching migration.',1;
IF OBJECT_ID(N'sec.AppUser',N'U') IS NULL OR OBJECT_ID(N'cfg.SourceScope',N'U') IS NULL
    THROW 51000,'Run Identity 001 and 002 first.',1;
IF OBJECT_ID(N'cfg.TemplateVersion',N'U') IS NOT NULL THROW 51000,'cfg.TemplateVersion already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'cfg.SubmissionRule',N'U') IS NOT NULL THROW 51000,'cfg.SubmissionRule already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'cfg.ReportingPeriod',N'U') IS NOT NULL THROW 51000,'cfg.ReportingPeriod already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ingest.SubmissionRequirement',N'U') IS NOT NULL THROW 51000,'ingest.SubmissionRequirement already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ingest.StoredFile',N'U') IS NOT NULL THROW 51000,'ingest.StoredFile already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ingest.ImportBatch',N'U') IS NOT NULL THROW 51000,'ingest.ImportBatch already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ingest.RawRow',N'U') IS NOT NULL THROW 51000,'ingest.RawRow already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ingest.ImportIssue',N'U') IS NOT NULL THROW 51000,'ingest.ImportIssue already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ops.ProcessingJob',N'U') IS NOT NULL THROW 51000,'ops.ProcessingJob already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'ops.AuditEvent',N'U') IS NOT NULL THROW 51000,'ops.AuditEvent already exists. Review the actual schema; do not rerun this installation.',1;
IF OBJECT_ID(N'src.PoUpdateLine',N'U') IS NOT NULL THROW 51000,'src.PoUpdateLine already exists. Review the actual schema; do not rerun this installation.',1;
BEGIN TRY
BEGIN TRANSACTION;
IF SCHEMA_ID(N'ingest') IS NULL EXEC(N'CREATE SCHEMA ingest AUTHORIZATION dbo');
IF SCHEMA_ID(N'src') IS NULL EXEC(N'CREATE SCHEMA src AUTHORIZATION dbo');
IF SCHEMA_ID(N'ops') IS NULL EXEC(N'CREATE SCHEMA ops AUTHORIZATION dbo');
CREATE TABLE cfg.TemplateVersion (
    TemplateVersionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TemplateVersion PRIMARY KEY,
    SourceCode varchar(40) NOT NULL,
    VersionNo int NOT NULL,
    Status varchar(15) NOT NULL CONSTRAINT DF_TemplateVersion_Status DEFAULT('DRAFT'),
    DefinitionJson nvarchar(max) NOT NULL,
    ApprovedByUserId bigint NULL,
    ApprovedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_TemplateVersion_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT UQ_TemplateVersion UNIQUE(SourceCode,VersionNo),
    CONSTRAINT UQ_TemplateVersion_Source UNIQUE(TemplateVersionId,SourceCode),
    CONSTRAINT FK_TemplateVersion_Source FOREIGN KEY(SourceCode) REFERENCES cfg.SourceType(SourceCode),
    CONSTRAINT FK_TemplateVersion_Approver FOREIGN KEY(ApprovedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_TemplateVersion_Version CHECK(VersionNo>0),
    CONSTRAINT CK_TemplateVersion_Json CHECK(ISJSON(DefinitionJson)=1),
    CONSTRAINT CK_TemplateVersion_Status CHECK(Status IN('DRAFT','APPROVED','RETIRED')),
    CONSTRAINT CK_TemplateVersion_Approval CHECK(Status<>'APPROVED' OR (ApprovedByUserId IS NOT NULL AND ApprovedAtUtc IS NOT NULL))
);

CREATE TABLE cfg.SubmissionRule (
    SubmissionRuleId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubmissionRule PRIMARY KEY,
    SourceCode varchar(40) NOT NULL,
    ScopeCode varchar(40) NOT NULL,
    VersionNo int NOT NULL,
    Status varchar(15) NOT NULL CONSTRAINT DF_SubmissionRule_Status DEFAULT('DRAFT'),
    IngestionMode varchar(15) NOT NULL CONSTRAINT DF_SubmissionRule_Mode DEFAULT('UNDECIDED'),
    ScheduleJson nvarchar(max) NOT NULL,
    PolicyJson nvarchar(max) NOT NULL,
    ResponsibleUserId bigint NULL,
    BackupUserId bigint NULL,
    ApprovedByUserId bigint NULL,
    ApprovedAtUtc datetime2(3) NULL,
    CONSTRAINT UQ_SubmissionRule UNIQUE(SourceCode,ScopeCode,VersionNo),
    CONSTRAINT UQ_SubmissionRule_Scope UNIQUE(SubmissionRuleId,SourceCode,ScopeCode),
    CONSTRAINT FK_SubmissionRule_SourceScope FOREIGN KEY(SourceCode,ScopeCode) REFERENCES cfg.SourceScope(SourceCode,ScopeCode),
    CONSTRAINT FK_SubmissionRule_Owner FOREIGN KEY(ResponsibleUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_SubmissionRule_Backup FOREIGN KEY(BackupUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_SubmissionRule_Approver FOREIGN KEY(ApprovedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_SubmissionRule_Version CHECK(VersionNo>0),
    CONSTRAINT CK_SubmissionRule_Json CHECK(ISJSON(ScheduleJson)=1 AND ISJSON(PolicyJson)=1),
    CONSTRAINT CK_SubmissionRule_Mode CHECK(IngestionMode IN('UNDECIDED','SNAPSHOT','DELTA')),
    CONSTRAINT CK_SubmissionRule_Status CHECK(Status IN('DRAFT','APPROVED','RETIRED')),
    CONSTRAINT CK_SubmissionRule_Approval CHECK(Status<>'APPROVED' OR (IngestionMode<>'UNDECIDED' AND ResponsibleUserId IS NOT NULL AND ApprovedByUserId IS NOT NULL AND ApprovedAtUtc IS NOT NULL))
    -- DELTA is reserved. The V1 commit service must reject DELTA until implemented.
);

CREATE TABLE cfg.ReportingPeriod (
    PeriodId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportingPeriod PRIMARY KEY,
    PeriodCode char(7) NOT NULL CONSTRAINT UQ_ReportingPeriod_Code UNIQUE,
    StartDate date NOT NULL,
    EndDateExclusive date NOT NULL,
    Status varchar(12) NOT NULL CONSTRAINT DF_ReportingPeriod_Status DEFAULT('OPEN'),
    DataRevision bigint NOT NULL CONSTRAINT DF_ReportingPeriod_Revision DEFAULT(0),
    ClosedByUserId bigint NULL,
    ClosedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_ReportingPeriod_ClosedBy FOREIGN KEY(ClosedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_ReportingPeriod_Dates CHECK(DAY(StartDate)=1 AND EndDateExclusive=DATEADD(month,1,StartDate) AND PeriodCode=CONVERT(char(7),StartDate,126)),
    CONSTRAINT CK_ReportingPeriod_Status CHECK(Status IN('OPEN','REVIEW','CLOSED')),
    CONSTRAINT CK_ReportingPeriod_Revision CHECK(DataRevision>=0),
    CONSTRAINT CK_ReportingPeriod_Close CHECK(Status<>'CLOSED' OR (ClosedByUserId IS NOT NULL AND ClosedAtUtc IS NOT NULL))
);

CREATE TABLE ingest.SubmissionRequirement (
    RequirementId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubmissionRequirement PRIMARY KEY,
    PeriodId int NOT NULL,
    SourceCode varchar(40) NOT NULL,
    ScopeCode varchar(40) NOT NULL,
    SubmissionRuleId bigint NULL,
    ResponsibleUserId bigint NULL,
    BackupUserId bigint NULL,
    DueAtUtc datetime2(3) NULL,
    TimeZoneId varchar(80) NOT NULL CONSTRAINT DF_Requirement_Timezone DEFAULT('Asia/Ho_Chi_Minh'),
    IsRequired bit NOT NULL CONSTRAINT DF_Requirement_Required DEFAULT(1),
    PolicySnapshotJson nvarchar(max) NOT NULL,
    ActiveBatchId bigint NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Requirement UNIQUE(PeriodId,SourceCode,ScopeCode),
    CONSTRAINT UQ_Requirement_Source UNIQUE(RequirementId,SourceCode),
    CONSTRAINT FK_Requirement_Period FOREIGN KEY(PeriodId) REFERENCES cfg.ReportingPeriod(PeriodId),
    CONSTRAINT FK_Requirement_SourceScope FOREIGN KEY(SourceCode,ScopeCode) REFERENCES cfg.SourceScope(SourceCode,ScopeCode),
    CONSTRAINT FK_Requirement_RuleScope FOREIGN KEY(SubmissionRuleId,SourceCode,ScopeCode) REFERENCES cfg.SubmissionRule(SubmissionRuleId,SourceCode,ScopeCode),
    CONSTRAINT FK_Requirement_Owner FOREIGN KEY(ResponsibleUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_Requirement_Backup FOREIGN KEY(BackupUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_Requirement_Json CHECK(ISJSON(PolicySnapshotJson)=1)
);

CREATE TABLE ingest.StoredFile (
    FileId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StoredFile PRIMARY KEY,
    StorageKey nvarchar(450) NOT NULL CONSTRAINT UQ_StoredFile_StorageKey UNIQUE,
    OriginalName nvarchar(260) NOT NULL,
    MediaType varchar(120) NOT NULL,
    SizeBytes bigint NOT NULL,
    Sha256 binary(32) NOT NULL,
    Purpose varchar(20) NOT NULL,
    CreatedByUserId bigint NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_StoredFile_Created DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_StoredFile_User FOREIGN KEY(CreatedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_StoredFile_Size CHECK(SizeBytes>=0),
    CONSTRAINT CK_StoredFile_Purpose CHECK(Purpose IN('INPUT','REPORT','ERROR_EXPORT'))
    -- Store file bytes in protected storage; authorize every download through API.
);
CREATE INDEX IX_StoredFile_Hash ON ingest.StoredFile(Sha256);

CREATE TABLE ingest.ImportBatch (
    BatchId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportBatch PRIMARY KEY,
    RequirementId bigint NOT NULL,
    SourceCode varchar(40) NOT NULL,
    TemplateVersionId bigint NULL,
    FileId bigint NULL,
    VersionNo int NOT NULL,
    Kind varchar(15) NOT NULL,
    Status varchar(15) NOT NULL,
    IngestionMode varchar(15) NOT NULL CONSTRAINT DF_ImportBatch_Mode DEFAULT('UNDECIDED'),
    NoActivityReason nvarchar(2000) NULL,
    SourceAsOfUtc datetime2(3) NULL,
    ParsingOptionsJson nvarchar(max) NOT NULL,
    TotalRows int NOT NULL CONSTRAINT DF_ImportBatch_Total DEFAULT(0),
    ErrorCount int NOT NULL CONSTRAINT DF_ImportBatch_Errors DEFAULT(0),
    WarningCount int NOT NULL CONSTRAINT DF_ImportBatch_Warnings DEFAULT(0),
    UploadedByUserId bigint NOT NULL,
    UploadedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ImportBatch_Uploaded DEFAULT(SYSUTCDATETIME()),
    CommittedByUserId bigint NULL,
    CommittedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_ImportBatch_Version UNIQUE(RequirementId,VersionNo),
    CONSTRAINT UQ_ImportBatch_Requirement UNIQUE(BatchId,RequirementId),
    CONSTRAINT UQ_ImportBatch_Source UNIQUE(BatchId,SourceCode),
    CONSTRAINT FK_ImportBatch_Requirement FOREIGN KEY(RequirementId,SourceCode) REFERENCES ingest.SubmissionRequirement(RequirementId,SourceCode),
    CONSTRAINT FK_ImportBatch_Template FOREIGN KEY(TemplateVersionId,SourceCode) REFERENCES cfg.TemplateVersion(TemplateVersionId,SourceCode),
    CONSTRAINT FK_ImportBatch_File FOREIGN KEY(FileId) REFERENCES ingest.StoredFile(FileId),
    CONSTRAINT FK_ImportBatch_Uploader FOREIGN KEY(UploadedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_ImportBatch_Committer FOREIGN KEY(CommittedByUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_ImportBatch_Version CHECK(VersionNo>0),
    CONSTRAINT CK_ImportBatch_Kind CHECK(Kind IN('DATA','NO_ACTIVITY')),
    CONSTRAINT CK_ImportBatch_Content CHECK((Kind='DATA' AND FileId IS NOT NULL AND TemplateVersionId IS NOT NULL) OR (Kind='NO_ACTIVITY' AND FileId IS NULL AND NoActivityReason IS NOT NULL AND LEN(LTRIM(RTRIM(NoActivityReason)))>0)),
    CONSTRAINT CK_ImportBatch_Status CHECK(Status IN('RECEIVED','VALIDATING','READY','INVALID','COMMITTED','DISCARDED')),
    CONSTRAINT CK_ImportBatch_Mode CHECK(IngestionMode IN('UNDECIDED','SNAPSHOT','DELTA')),
    CONSTRAINT CK_ImportBatch_Json CHECK(ISJSON(ParsingOptionsJson)=1),
    CONSTRAINT CK_ImportBatch_Counts CHECK(TotalRows>=0 AND ErrorCount>=0 AND WarningCount>=0),
    CONSTRAINT CK_ImportBatch_Commit CHECK(Status<>'COMMITTED' OR (CommittedByUserId IS NOT NULL AND CommittedAtUtc IS NOT NULL AND ErrorCount=0 AND IngestionMode='SNAPSHOT'))
);
ALTER TABLE ingest.SubmissionRequirement ADD CONSTRAINT FK_Requirement_ActiveBatch
    FOREIGN KEY(ActiveBatchId,RequirementId) REFERENCES ingest.ImportBatch(BatchId,RequirementId);
CREATE INDEX IX_ImportBatch_Queue ON ingest.ImportBatch(Status,UploadedAtUtc) INCLUDE(RequirementId,SourceCode);

CREATE TABLE ingest.RawRow (
    RawRowId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RawRow PRIMARY KEY,
    BatchId bigint NOT NULL,
    SheetName nvarchar(128) NOT NULL,
    RecordNumber int NOT NULL,
    CellsJson nvarchar(max) NOT NULL,
    CONSTRAINT UQ_RawRow_Position UNIQUE(BatchId,SheetName,RecordNumber),
    CONSTRAINT UQ_RawRow_Batch UNIQUE(RawRowId,BatchId),
    CONSTRAINT FK_RawRow_Batch FOREIGN KEY(BatchId) REFERENCES ingest.ImportBatch(BatchId),
    CONSTRAINT CK_RawRow_Number CHECK(RecordNumber>0),
    CONSTRAINT CK_RawRow_Json CHECK(ISJSON(CellsJson)=1)
    -- Store an ordered array of cells: ordinal, original header, text, cell type,
    -- optional Excel formula and cached value. Do not collapse duplicate headers.
);

CREATE TABLE ingest.ImportIssue (
    IssueId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportIssue PRIMARY KEY,
    BatchId bigint NOT NULL,
    RawRowId bigint NULL,
    Severity varchar(10) NOT NULL,
    IssueCode varchar(80) NOT NULL,
    ColumnName nvarchar(500) NULL,
    Message nvarchar(2000) NOT NULL,
    CONSTRAINT FK_ImportIssue_Batch FOREIGN KEY(BatchId) REFERENCES ingest.ImportBatch(BatchId),
    CONSTRAINT FK_ImportIssue_Row FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId),
    CONSTRAINT CK_ImportIssue_Severity CHECK(Severity IN('ERROR','WARNING','INFO'))
);
CREATE INDEX IX_ImportIssue_Batch ON ingest.ImportIssue(BatchId,Severity);



CREATE TABLE ops.ProcessingJob (
    JobId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProcessingJob PRIMARY KEY,
    JobType varchar(30) NOT NULL,
    IdempotencyKey nvarchar(200) NOT NULL CONSTRAINT UQ_ProcessingJob_Idempotency UNIQUE,
    PayloadJson nvarchar(max) NOT NULL,
    Status varchar(15) NOT NULL,
    AttemptCount int NOT NULL CONSTRAINT DF_ProcessingJob_Attempts DEFAULT(0),
    MaxAttempts int NOT NULL CONSTRAINT DF_ProcessingJob_MaxAttempts DEFAULT(3),
    AvailableAtUtc datetime2(3) NOT NULL,
    LeaseOwner nvarchar(200) NULL,
    LeaseUntilUtc datetime2(3) NULL,
    LastErrorCode varchar(80) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ProcessingJob_Created DEFAULT(SYSUTCDATETIME()),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_ProcessingJob_Status CHECK(Status IN('QUEUED','RUNNING','SUCCEEDED','FAILED','CANCELLED')),
    CONSTRAINT CK_ProcessingJob_Json CHECK(ISJSON(PayloadJson)=1),
    CONSTRAINT CK_ProcessingJob_Attempts CHECK(AttemptCount>=0 AND MaxAttempts>0)
);
CREATE INDEX IX_ProcessingJob_Queue ON ops.ProcessingJob(Status,AvailableAtUtc);

CREATE TABLE ops.AuditEvent (
    AuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditEvent PRIMARY KEY,
    ActorUserId bigint NULL,
    ActorKind varchar(10) NOT NULL,
    ActionCode varchar(80) NOT NULL,
    EntityType varchar(80) NOT NULL,
    EntityId nvarchar(200) NOT NULL,
    CorrelationId uniqueidentifier NOT NULL,
    BeforeJson nvarchar(max) NULL,
    AfterJson nvarchar(max) NULL,
    Reason nvarchar(2000) NULL,
    OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AuditEvent_Occurred DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_AuditEvent_Actor FOREIGN KEY(ActorUserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_AuditEvent_ActorKind CHECK(ActorKind IN('USER','SYSTEM')),
    CONSTRAINT CK_AuditEvent_Actor CHECK(ActorKind<>'USER' OR ActorUserId IS NOT NULL),
    CONSTRAINT CK_AuditEvent_Json CHECK((BeforeJson IS NULL OR ISJSON(BeforeJson)=1) AND (AfterJson IS NULL OR ISJSON(AfterJson)=1))
    -- Grant application append-only access; no UPDATE/DELETE for the app identity.
    -- Redact secrets and avoid copying complete financial rows into audit metadata.
);
CREATE INDEX IX_AuditEvent_Entity ON ops.AuditEvent(EntityType,EntityId,OccurredAtUtc DESC);

-- Typed Finance tables are defined in 002_finance_tables.sql.

CREATE TABLE src.PoUpdateLine (
    SourceLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PoUpdateLine PRIMARY KEY,
    BatchId bigint NOT NULL,
    RawRowId bigint NOT NULL,
    SourceCode varchar(40) NOT NULL CONSTRAINT DF_PoUpdateLine_Source DEFAULT('PO_UPDATED'),
    SourceCodeText nvarchar(100) NULL, -- Souce
    ReceiptNumber nvarchar(120) NULL, -- Receipt Num
    TransactionDate date NULL, -- Trx date
    PoNumber nvarchar(120) NULL, -- PO Num
    ReceiptQuantity decimal(28,6) NULL, -- Receipt QTY
    PoUnitPrice decimal(28,8) NULL, -- PO Unit Price
    PoLineReference nvarchar(50) NULL, -- PO Line
    PoCurrency nvarchar(10) NULL, -- PO Currency
    PoRateDate date NULL, -- PO Rate Date
    PoRate decimal(28,10) NULL, -- PO Rate
    ReceiptRate decimal(28,10) NULL, -- Receipt Rate
    ReceiptRateDate date NULL, -- Receipt Rate Date
    PurchaseFunctionalAmount decimal(28,8) NULL, -- Purchase Functional Amount
    InvoiceNumber nvarchar(200) NULL, -- Invoice No.
    InvoiceFunctionalAmount decimal(28,8) NULL, -- Invoice Functional Amount
    InvoiceAmount decimal(28,8) NULL, -- Invoice Amount
    LineDescription nvarchar(2000) NULL, -- Line Description
    CONSTRAINT UQ_PoUpdateLine_RawRow UNIQUE(RawRowId),
    CONSTRAINT FK_PoUpdateLine_Batch FOREIGN KEY(BatchId,SourceCode) REFERENCES ingest.ImportBatch(BatchId,SourceCode),
    CONSTRAINT FK_PoUpdateLine_RawRow FOREIGN KEY(RawRowId,BatchId) REFERENCES ingest.RawRow(RawRowId,BatchId),
    CONSTRAINT CK_PoUpdateLine_Source CHECK(SourceCode='PO_UPDATED')
);
CREATE INDEX IX_PoUpdateLine_Batch ON src.PoUpdateLine(BatchId);
/* API requires these fields for new uploads. Nullable permits deliberate fixture setup.
   Fingerprint binds request id to file hash + template + source-as-of + parsing options. */
ALTER TABLE ingest.ImportBatch ADD ClientUploadId uniqueidentifier NULL, UploadFingerprint binary(32) NULL;
CREATE UNIQUE INDEX UX_ImportBatch_ClientRequest ON ingest.ImportBatch(RequirementId,ClientUploadId)
    WHERE ClientUploadId IS NOT NULL;

COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
SELECT DB_NAME() AS DatabaseName, N'M1 import schema created' AS Result;
