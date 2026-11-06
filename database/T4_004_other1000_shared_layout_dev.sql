SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>N'FinanceLogistics_Dev' THROW 51000,'Select FinanceLogistics_Dev.',1;
IF OBJECT_ID(N'src.OtherTradeLine') IS NULL THROW 51000,'Install T4_001 first.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=N'LogisticsTrade.OtherSites.V2',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51000,'Schema lock unavailable.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.Site WHERE SiteCode='1000' AND IsActive=1) OR NOT EXISTS(SELECT 1 FROM cfg.DataScope WHERE ScopeCode='SITE_1000') OR (SELECT COUNT(*) FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000')<>1 OR NOT EXISTS(SELECT 1 FROM cfg.ScopeSite WHERE ScopeCode='SITE_1000' AND SiteCode='1000')
 THROW 51000,'Expected separate, active SITE_1000 configuration.',1;
IF EXISTS(SELECT 1 FROM src.OtherTradeLine WHERE SourceCode NOT IN('OTHER_IMPORT_1000','OTHER_EXPORT_1000','OTHER_IMPORT_6000','OTHER_EXPORT_6000')) THROW 51000,'Unexpected existing OTHER source.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'src.OtherTradeLine') AND name='CK_OtherTradeLine_Source') THROW 51000,'Expected named source constraint.',1;
ALTER TABLE src.OtherTradeLine DROP CONSTRAINT CK_OtherTradeLine_Source;
ALTER TABLE src.OtherTradeLine WITH CHECK ADD CONSTRAINT CK_OtherTradeLine_Source CHECK(SourceCode IN('OTHER_IMPORT_1000','OTHER_EXPORT_1000','OTHER_IMPORT_6000','OTHER_EXPORT_6000'));
ALTER TABLE src.OtherTradeLine CHECK CONSTRAINT CK_OtherTradeLine_Source;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_IMPORT_1000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('OTHER_IMPORT_1000',N'OTHER_IMPORT_1000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_IMPORT_1000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing OTHER_IMPORT_1000 differs.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_IMPORT_1000' AND ScopeCode<>'SITE_1000') THROW 51000,'Unexpected OTHER_IMPORT_1000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_IMPORT_1000' AND ScopeCode='SITE_1000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('OTHER_IMPORT_1000','SITE_1000');
IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_EXPORT_1000') INSERT cfg.SourceType(SourceCode,SourceName,OwnerRoleCode,IsActive) VALUES('OTHER_EXPORT_1000',N'OTHER_EXPORT_1000','LOGISTICS',1);
ELSE IF NOT EXISTS(SELECT 1 FROM cfg.SourceType WHERE SourceCode='OTHER_EXPORT_1000' AND OwnerRoleCode='LOGISTICS' AND IsActive=1) THROW 51000,'Existing OTHER_EXPORT_1000 differs.',1;
IF EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_EXPORT_1000' AND ScopeCode<>'SITE_1000') THROW 51000,'Unexpected OTHER_EXPORT_1000 scope.',1;
IF NOT EXISTS(SELECT 1 FROM cfg.SourceScope WHERE SourceCode='OTHER_EXPORT_1000' AND ScopeCode='SITE_1000') INSERT cfg.SourceScope(SourceCode,ScopeCode) VALUES('OTHER_EXPORT_1000','SITE_1000');
COMMIT;
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
