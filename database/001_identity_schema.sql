/* Identity V1.1. Apply ONLY to the separate FinanceLogistics DEVELOPMENT database.
   Supports an empty database or the delivered V1 draft schema. Never run against ELeave.
   Guarded creates do not repair an incompatible existing table. SQL Server execution still required.
   No automatic EF schema creation is used. */
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF SCHEMA_ID('sec') IS NULL EXEC('CREATE SCHEMA sec');
IF SCHEMA_ID('cfg') IS NULL EXEC('CREATE SCHEMA cfg');
GO

IF OBJECT_ID(N'cfg.Site', N'U') IS NULL
BEGIN
CREATE TABLE cfg.Site (
    SiteCode nvarchar(20) NOT NULL CONSTRAINT PK_Site PRIMARY KEY,
    SiteName nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Site_Active DEFAULT(1)
);
END;
GO

IF OBJECT_ID(N'sec.AppUser', N'U') IS NULL
BEGIN
CREATE TABLE sec.AppUser (
    UserId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
    WindowsSid nvarchar(184) NOT NULL CONSTRAINT UQ_AppUser_Sid UNIQUE,
    Upn nvarchar(320) NULL,
    DisplayName nvarchar(200) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_AppUser_Active DEFAULT(1),
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AppUser_Created DEFAULT(SYSUTCDATETIME()),
    RowVersion rowversion NOT NULL
    -- AD identifies the user by SID. No password column.
);
END;
GO

IF OBJECT_ID(N'sec.AppRole', N'U') IS NULL
BEGIN
CREATE TABLE sec.AppRole (
    RoleCode varchar(30) NOT NULL CONSTRAINT PK_AppRole PRIMARY KEY,
    RoleName nvarchar(120) NOT NULL
);
END;
GO

IF OBJECT_ID(N'sec.UserRole', N'U') IS NULL
BEGIN
CREATE TABLE sec.UserRole (
    UserId bigint NOT NULL,
    RoleCode varchar(30) NOT NULL,
    GrantedByUserId bigint NOT NULL,
    GrantedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_UserRole_Granted DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT PK_UserRole PRIMARY KEY(UserId,RoleCode),
    CONSTRAINT FK_UserRole_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_UserRole_Role FOREIGN KEY(RoleCode) REFERENCES sec.AppRole(RoleCode),
    CONSTRAINT FK_UserRole_GrantedBy FOREIGN KEY(GrantedByUserId) REFERENCES sec.AppUser(UserId)
);
END;
GO

IF OBJECT_ID(N'sec.UserSite', N'U') IS NULL
BEGIN
CREATE TABLE sec.UserSite (
    UserId bigint NOT NULL,
    SiteCode nvarchar(20) NOT NULL,
    CONSTRAINT PK_UserSite PRIMARY KEY(UserId,SiteCode),
    CONSTRAINT FK_UserSite_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_UserSite_Site FOREIGN KEY(SiteCode) REFERENCES cfg.Site(SiteCode)
);
END;
GO

IF OBJECT_ID(N'cfg.DataScope', N'U') IS NULL
BEGIN
CREATE TABLE cfg.DataScope (
    ScopeCode varchar(40) NOT NULL CONSTRAINT PK_DataScope PRIMARY KEY,
    ScopeName nvarchar(200) NOT NULL
);
END;
GO

IF OBJECT_ID(N'cfg.ScopeSite', N'U') IS NULL
BEGIN
CREATE TABLE cfg.ScopeSite (
    ScopeCode varchar(40) NOT NULL,
    SiteCode nvarchar(20) NOT NULL,
    CONSTRAINT PK_ScopeSite PRIMARY KEY(ScopeCode,SiteCode),
    CONSTRAINT FK_ScopeSite_Scope FOREIGN KEY(ScopeCode) REFERENCES cfg.DataScope(ScopeCode),
    CONSTRAINT FK_ScopeSite_Site FOREIGN KEY(SiteCode) REFERENCES cfg.Site(SiteCode)
);
END;
GO

IF OBJECT_ID(N'cfg.SourceType', N'U') IS NULL
BEGIN
CREATE TABLE cfg.SourceType (
    SourceCode varchar(40) NOT NULL CONSTRAINT PK_SourceType PRIMARY KEY,
    SourceName nvarchar(200) NOT NULL,
    OwnerRoleCode varchar(30) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_SourceType_Active DEFAULT(1),
    CONSTRAINT FK_SourceType_Role FOREIGN KEY(OwnerRoleCode) REFERENCES sec.AppRole(RoleCode)
);
END;
GO

IF OBJECT_ID(N'cfg.SourceScope', N'U') IS NULL
BEGIN
CREATE TABLE cfg.SourceScope (
    SourceCode varchar(40) NOT NULL,
    ScopeCode varchar(40) NOT NULL,
    CONSTRAINT PK_SourceScope PRIMARY KEY(SourceCode,ScopeCode),
    CONSTRAINT FK_SourceScope_Source FOREIGN KEY(SourceCode) REFERENCES cfg.SourceType(SourceCode),
    CONSTRAINT FK_SourceScope_Scope FOREIGN KEY(ScopeCode) REFERENCES cfg.DataScope(ScopeCode)
);
END;
GO

IF OBJECT_ID(N'sec.DirectoryAccount',N'U') IS NULL
CREATE TABLE sec.DirectoryAccount (
    UserId bigint NOT NULL CONSTRAINT PK_DirectoryAccount PRIMARY KEY,
    DirectoryKey varchar(40) NOT NULL,
    SamAccountName nvarchar(256) NOT NULL,
    CONSTRAINT UQ_DirectoryAccount UNIQUE(DirectoryKey,SamAccountName),
    CONSTRAINT FK_DirectoryAccount_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId)
);
GO
IF OBJECT_ID(N'sec.LoginAlias',N'U') IS NULL
CREATE TABLE sec.LoginAlias (
    NormalizedAlias nvarchar(320) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT PK_LoginAlias PRIMARY KEY,
    UserId bigint NOT NULL,
    CONSTRAINT FK_LoginAlias_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId)
);
GO
IF OBJECT_ID(N'sec.AuthSession',N'U') IS NULL
CREATE TABLE sec.AuthSession (
    SessionId uniqueidentifier NOT NULL CONSTRAINT PK_AuthSession PRIMARY KEY,
    UserId bigint NOT NULL,
    SecretHash binary(32) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL,
    ExpiresAtUtc datetime2(3) NOT NULL,
    RevokedAtUtc datetime2(3) NULL,
    CONSTRAINT FK_AuthSession_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT CK_AuthSession_Expiry CHECK(ExpiresAtUtc > CreatedAtUtc)
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'sec.AuthSession') AND name='IX_AuthSession_UserExpiry')
CREATE INDEX IX_AuthSession_UserExpiry ON sec.AuthSession(UserId,ExpiresAtUtc);
GO
IF OBJECT_ID(N'sec.IdentityAudit',N'U') IS NULL
CREATE TABLE sec.IdentityAudit (
    EventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IdentityAudit PRIMARY KEY,
    UserId bigint NULL,
    ActorUserId bigint NULL,
    EventCode varchar(40) NOT NULL,
    OccurredAtUtc datetime2(3) NOT NULL,
    CorrelationId nvarchar(64) NOT NULL,
    CONSTRAINT FK_IdentityAudit_User FOREIGN KEY(UserId) REFERENCES sec.AppUser(UserId),
    CONSTRAINT FK_IdentityAudit_Actor FOREIGN KEY(ActorUserId) REFERENCES sec.AppUser(UserId)
);
GO
