/* DEV seed for the next milestone: period + required PO submission.
   Apply 004 first. Run this whole file in SSMS, not a selected fragment.
   No file, import batch, typed PO data or approved template is created.
   Edit @StartDate and @OwnerUpn deliberately; no hard-coded UserId.
   Due date below is a TEST example, not the company's agreed deadline.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() <> N'FinanceLogistics_Dev'
    THROW 51000, 'Select FinanceLogistics_Dev before running this DEV seed.', 1;
IF OBJECT_ID(N'ingest.SubmissionRequirement', N'U') IS NULL
    THROW 51000, 'Run 004_po_import_schema.sql first.', 1;

DECLARE @StartDate date = '20261001';
DECLARE @PeriodCode char(7) = CONVERT(char(7), @StartDate, 126);
DECLARE @OwnerUpn nvarchar(320) = N'liem.phung@hsnservice.com';
DECLARE @OwnerId bigint;
DECLARE @PeriodId int;
DECLARE @RequirementId bigint;
-- Example only: 17:00 Viet Nam, day 5 of next month = 10:00 UTC.
DECLARE @DueAtUtc datetime2(3) =
    DATEADD(hour, 10, CAST(DATEADD(day, 4, DATEADD(month, 1, @StartDate)) AS datetime2(3)));
DECLARE @Policy nvarchar(max) = N'{
  "purpose":"DEV_PO_PERIODS_EXERCISE",
  "ingestionMode":"SNAPSHOT",
  "maxBytes":10485760,
  "maxRows":10000,
  "requireExplicitWarningsAcceptance":true,
  "deadlineIsIllustrative":true,
  "remindersEnabled":false
}';

IF DAY(@StartDate) <> 1
    THROW 51000, 'StartDate must be the first day of the month.', 1;
IF CHARINDEX(N'<', @OwnerUpn) > 0 OR CHARINDEX(N'>', @OwnerUpn) > 0
    THROW 51000, 'Remove placeholder angle brackets from OwnerUpn.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Exactly one active Finance user who is linked to AD must match.
    IF (SELECT COUNT_BIG(*) FROM sec.AppUser u WITH(UPDLOCK,HOLDLOCK)
        WHERE u.Upn = @OwnerUpn AND u.IsActive = 1
          AND EXISTS(SELECT 1 FROM sec.DirectoryAccount d WHERE d.UserId = u.UserId)
          AND EXISTS(SELECT 1 FROM sec.UserRole r
                     WHERE r.UserId = u.UserId AND r.RoleCode = 'FINANCE')) <> 1
        THROW 51000, 'OwnerUpn must identify exactly one active, AD-linked FINANCE user.', 1;

    SET @OwnerId = (
        SELECT u.UserId FROM sec.AppUser u
        WHERE u.Upn = @OwnerUpn AND u.IsActive = 1
          AND EXISTS(SELECT 1 FROM sec.DirectoryAccount d WHERE d.UserId = u.UserId)
          AND EXISTS(SELECT 1 FROM sec.UserRole r
                     WHERE r.UserId = u.UserId AND r.RoleCode = 'FINANCE')
    );

    IF NOT EXISTS (
        SELECT 1 FROM cfg.SourceType s
        JOIN cfg.SourceScope ss ON ss.SourceCode = s.SourceCode
        WHERE s.SourceCode = 'PO_UPDATED' AND ss.ScopeCode = 'FINANCE_SHARED'
          AND s.OwnerRoleCode = 'FINANCE' AND s.IsActive = 1
    )
        THROW 51000, 'Active PO_UPDATED / FINANCE_SHARED catalog is missing.', 1;

    SET @PeriodId = (
        SELECT PeriodId FROM cfg.ReportingPeriod WITH(UPDLOCK,HOLDLOCK)
        WHERE PeriodCode = @PeriodCode
    );
    IF @PeriodId IS NULL
    BEGIN
        INSERT cfg.ReportingPeriod(PeriodCode,StartDate,EndDateExclusive,Status)
        VALUES(@PeriodCode,@StartDate,DATEADD(month,1,@StartDate),'OPEN');
        SET @PeriodId = CONVERT(int, SCOPE_IDENTITY());
    END
    ELSE IF NOT EXISTS (
        SELECT 1 FROM cfg.ReportingPeriod
        WHERE PeriodId = @PeriodId AND StartDate = @StartDate
          AND EndDateExclusive = DATEADD(month,1,@StartDate) AND Status = 'OPEN'
    )
        THROW 51000, 'Existing period is different or not OPEN. This seed does not reopen periods.', 1;

    SET @RequirementId = (
        SELECT RequirementId FROM ingest.SubmissionRequirement WITH(UPDLOCK,HOLDLOCK)
        WHERE PeriodId = @PeriodId AND SourceCode = 'PO_UPDATED' AND ScopeCode = 'FINANCE_SHARED'
    );
    IF @RequirementId IS NULL
    BEGIN
        INSERT ingest.SubmissionRequirement(
            PeriodId,SourceCode,ScopeCode,SubmissionRuleId,
            ResponsibleUserId,DueAtUtc,TimeZoneId,IsRequired,PolicySnapshotJson)
        VALUES(
            @PeriodId,'PO_UPDATED','FINANCE_SHARED',NULL,
            @OwnerId,@DueAtUtc,'Asia/Ho_Chi_Minh',1,@Policy);
        SET @RequirementId = CONVERT(bigint, SCOPE_IDENTITY());
        INSERT ops.AuditEvent(
            ActorUserId,ActorKind,ActionCode,EntityType,EntityId,
            CorrelationId,Reason)
        VALUES(
            NULL,'SYSTEM','DEV_PO_REQUIREMENT_SEEDED',
            'SubmissionRequirement',CONVERT(nvarchar(200),@RequirementId),
            NEWID(),N'DEV exercise; deadline is illustrative; no Finance approval or email schedule.');
    END
    ELSE IF NOT EXISTS (
        SELECT 1 FROM ingest.SubmissionRequirement
        WHERE RequirementId = @RequirementId
          AND ResponsibleUserId = @OwnerId AND DueAtUtc = @DueAtUtc
          AND TimeZoneId = 'Asia/Ho_Chi_Minh' AND IsRequired = 1
          AND SubmissionRuleId IS NULL
          AND PolicySnapshotJson = @Policy
    )
        THROW 51000, 'Existing requirement differs. Inspect it; this seed never overwrites existing policy.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    p.PeriodId,p.PeriodCode,p.StartDate,p.EndDateExclusive,p.Status AS PeriodStatus,
    r.RequirementId,r.SourceCode,r.ScopeCode,r.DueAtUtc,r.TimeZoneId,
    r.ResponsibleUserId,u.Upn,r.ActiveBatchId
FROM cfg.ReportingPeriod p
JOIN ingest.SubmissionRequirement r ON r.PeriodId = p.PeriodId
LEFT JOIN sec.AppUser u ON u.UserId = r.ResponsibleUserId
WHERE r.RequirementId = @RequirementId;
