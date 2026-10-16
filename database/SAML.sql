USE [FinanceLogistics_Dev];
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'sec.AppUser', N'U') IS NULL
       OR OBJECT_ID(N'sec.AuthSession', N'U') IS NULL
    BEGIN
        THROW 51000, N'Khong tim thay schema Identity nen tang.', 1;
    END;

    IF OBJECT_ID(N'sec.ExternalIdentity', N'U') IS NOT NULL
       OR OBJECT_ID(N'sec.SamlLoginRequest', N'U') IS NOT NULL
       OR OBJECT_ID(N'sec.SamlReplayProof', N'U') IS NOT NULL
       OR COL_LENGTH(N'sec.AuthSession', N'AuthenticationMethod') IS NOT NULL
       OR COL_LENGTH(N'sec.AuthSession', N'ExternalIdentityId') IS NOT NULL
    BEGIN
        THROW 51001,
            N'Schema SAML da ton tai mot phan hoac toan bo. Kiem tra migration truoc khi chay lai.',
            1;
    END;

    CREATE TABLE sec.ExternalIdentity
    (
        ExternalIdentityId bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ExternalIdentity PRIMARY KEY,

        ProviderKey varchar(40) NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        ObjectId uniqueidentifier NOT NULL,

        UserId bigint NOT NULL,
        IsActive bit NOT NULL
            CONSTRAINT DF_ExternalIdentity_IsActive DEFAULT (1),

        CreatedAtUtc datetime2(3) NOT NULL,
        CreatedByUserId bigint NOT NULL,

        CONSTRAINT UQ_ExternalIdentity_Subject
            UNIQUE (ProviderKey, TenantId, ObjectId),

        CONSTRAINT FK_ExternalIdentity_User
            FOREIGN KEY (UserId)
            REFERENCES sec.AppUser(UserId),

        CONSTRAINT FK_ExternalIdentity_Creator
            FOREIGN KEY (CreatedByUserId)
            REFERENCES sec.AppUser(UserId)
    );

    CREATE TABLE sec.SamlLoginRequest
    (
        RequestId varchar(128)
            COLLATE Latin1_General_100_BIN2 NOT NULL
            CONSTRAINT PK_SamlLoginRequest PRIMARY KEY,

        BrowserSecretHash binary(32) NOT NULL,

        CreatedAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        ConsumedAtUtc datetime2(3) NULL,

        PreviousSessionId uniqueidentifier NULL,

        CONSTRAINT CK_SamlLoginRequest_Expiry
            CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE INDEX IX_SamlLoginRequest_ExpiresAtUtc
        ON sec.SamlLoginRequest(ExpiresAtUtc);

    CREATE TABLE sec.SamlReplayProof
    (
        ProofHash binary(32) NOT NULL
            CONSTRAINT PK_SamlReplayProof PRIMARY KEY,

        ExpiresAtUtc datetime2(3) NOT NULL
    );

    CREATE INDEX IX_SamlReplayProof_ExpiresAtUtc
        ON sec.SamlReplayProof(ExpiresAtUtc);

    ALTER TABLE sec.AuthSession ADD
        AuthenticationMethod varchar(8) NOT NULL
            CONSTRAINT DF_AuthSession_AuthenticationMethod
            DEFAULT ('LDAP'),

        ExternalIdentityId bigint NULL,

        CONSTRAINT FK_AuthSession_ExternalIdentity
            FOREIGN KEY (ExternalIdentityId)
            REFERENCES sec.ExternalIdentity(ExternalIdentityId),

        CONSTRAINT CK_AuthSession_AuthenticationMethod CHECK
        (
            (AuthenticationMethod = 'LDAP'
                AND ExternalIdentityId IS NULL)
            OR
            (AuthenticationMethod = 'SAML'
                AND ExternalIdentityId IS NOT NULL)
        );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;