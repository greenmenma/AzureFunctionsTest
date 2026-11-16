-- SQL トリガー検証用のデータベースとテーブルを作成する
IF DB_ID('SampleDb') IS NULL
    CREATE DATABASE SampleDb;
GO

USE SampleDb;
GO

-- データベース単位で変更の追跡を有効化（SQL トリガーの前提条件）
IF NOT EXISTS (SELECT 1 FROM sys.change_tracking_databases WHERE database_id = DB_ID())
    ALTER DATABASE SampleDb SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
GO

IF OBJECT_ID('dbo.Items') IS NULL
BEGIN
    CREATE TABLE dbo.Items (
        Id       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Category NVARCHAR(100)    NOT NULL,
        Text     NVARCHAR(MAX)    NULL
    );
    -- テーブル単位でも変更の追跡を有効化
    ALTER TABLE dbo.Items ENABLE CHANGE_TRACKING;
END
GO

PRINT N'SampleDb の初期化が完了しました';
GO
