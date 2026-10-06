USE master;
GO

IF DB_ID(N'FinanceLogistics_Dev') IS NULL
BEGIN
    CREATE DATABASE FinanceLogistics_Dev;
END;
GO

USE FinanceLogistics_Dev;
GO

SELECT
    @@SERVERNAME AS SqlServerName,
    DB_NAME() AS CurrentDatabase,
    ORIGINAL_LOGIN() AS ConnectedLogin;