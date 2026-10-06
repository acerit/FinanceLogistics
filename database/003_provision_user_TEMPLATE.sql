/* Review and replace the placeholders. Run with an authorized DBA/developer account.
   Obtain SID, UPN and SAM from trusted AD administration tools, never from browser input.
   First user in an empty DB must be SYSTEM_ADMIN; subsequent users need a provisioned admin as grantor.
   Existing V1 AppUser may be linked only if it has no DirectoryAccount yet.
   This script grants the selected role/site; it does not silently replace existing access.
*/
SET XACT_ABORT ON;
DECLARE @WindowsSid nvarchar(184)=N'REPLACE_WITH_AD_SID';
DECLARE @Upn nvarchar(320)=N'REPLACE_WITH_UPN';
DECLARE @Sam nvarchar(256)=N'REPLACE_WITH_SAM';
DECLARE @DirectoryKey varchar(40)='CORPORATE';
DECLARE @DisplayName nvarchar(200)=N'REPLACE_WITH_DISPLAY_NAME';
DECLARE @Role varchar(30)='LOGISTICS'; -- FINANCE / LOGISTICS / SYSTEM_ADMIN
DECLARE @Site nvarchar(20)=N'1000'; -- Required for Logistics; ignored for other roles.
DECLARE @GrantedBySid nvarchar(184)=N'REPLACE_WITH_EXISTING_ADMIN_SID';
DECLARE @DomainAlias nvarchar(320)=NULL; -- Optional exact DOMAIN\sam alias.

IF @WindowsSid NOT LIKE N'S-1-5-21-%' OR @Sam LIKE N'REPLACE%' OR @Upn LIKE N'REPLACE%' OR @DisplayName LIKE N'REPLACE%'
    THROW 51000,'Replace every required account placeholder using trusted AD data.',1;
IF @Role NOT IN('FINANCE','LOGISTICS','SYSTEM_ADMIN')
    THROW 51000,'Unknown application role.',1;
IF @Role='LOGISTICS' AND NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode=@Site AND IsActive=1)
    THROW 51000,'Choose an active Logistics site.',1;

BEGIN TRANSACTION;
DECLARE @UserId bigint, @GrantorId bigint;
SELECT @UserId=UserId FROM sec.AppUser WITH(UPDLOCK,HOLDLOCK) WHERE WindowsSid=@WindowsSid;
IF EXISTS(SELECT 1 FROM sec.DirectoryAccount WHERE UserId=@UserId)
    THROW 51000,'Account is already linked; use a reviewed access-change script instead.',1;

IF NOT EXISTS(SELECT 1 FROM sec.AppUser WITH(UPDLOCK,HOLDLOCK))
BEGIN
    IF @Role<>'SYSTEM_ADMIN' THROW 51000,'Bootstrap the first SYSTEM_ADMIN account before business users.',1;
    INSERT sec.AppUser(WindowsSid,Upn,DisplayName) VALUES(@WindowsSid,@Upn,@DisplayName);
    SET @UserId=SCOPE_IDENTITY(); SET @GrantorId=@UserId;
END
ELSE
BEGIN
    SELECT @GrantorId=u.UserId FROM sec.AppUser u
    JOIN sec.UserRole r ON r.UserId=u.UserId AND r.RoleCode='SYSTEM_ADMIN'
    WHERE u.WindowsSid=@GrantedBySid AND u.IsActive=1;
    IF @GrantorId IS NULL THROW 51000,'An active SYSTEM_ADMIN grantor is required.',1;
    IF @UserId IS NULL
    BEGIN
        INSERT sec.AppUser(WindowsSid,Upn,DisplayName) VALUES(@WindowsSid,@Upn,@DisplayName);
        SET @UserId=SCOPE_IDENTITY();
    END;
END;
INSERT sec.DirectoryAccount(UserId,DirectoryKey,SamAccountName) VALUES(@UserId,@DirectoryKey,@Sam);
/* Global alias uniqueness prevents ambiguous short usernames across domains.
   If bare SAM conflicts, omit that alias and require UPN / DOMAIN\sam. */
INSERT sec.LoginAlias(NormalizedAlias,UserId)
SELECT DISTINCT UPPER(LTRIM(RTRIM(a))),@UserId
FROM (VALUES(@Sam),(@Upn),(@DomainAlias)) v(a) WHERE a IS NOT NULL AND LTRIM(RTRIM(a))<>N'';
IF NOT EXISTS(SELECT 1 FROM sec.UserRole WHERE UserId=@UserId AND RoleCode=@Role)
    INSERT sec.UserRole(UserId,RoleCode,GrantedByUserId) VALUES(@UserId,@Role,@GrantorId);
IF @Role='LOGISTICS' AND NOT EXISTS(SELECT 1 FROM sec.UserSite WHERE UserId=@UserId AND SiteCode=@Site)
    INSERT sec.UserSite(UserId,SiteCode) VALUES(@UserId,@Site);
INSERT sec.IdentityAudit(UserId,ActorUserId,EventCode,OccurredAtUtc,CorrelationId)
VALUES(@UserId,@GrantorId,'USER_PROVISIONED',SYSUTCDATETIME(),CONVERT(nvarchar(36),NEWID()));
COMMIT;
SELECT @UserId AS UserId,@Role AS GrantedRole;
