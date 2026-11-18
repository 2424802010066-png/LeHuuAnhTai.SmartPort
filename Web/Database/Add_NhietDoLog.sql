-- Bang nhat ky nhiet do container lanh (reefer monitoring)
-- Chay tren CA local (.\MSSQLSERVER01) lan hosting (SmartPortDB.mssql.somee.com)
IF OBJECT_ID('dbo.NhietDoLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NhietDoLog (
        MaLog    INT IDENTITY(1,1) PRIMARY KEY,
        MaCont   INT NOT NULL,
        NhietDo  DECIMAL(5,1) NOT NULL,
        Nguon    NVARCHAR(20) NULL,      -- IoT / ThuCong
        ThoiGian DATETIME NOT NULL
    );
END
GO
-- UNIQUE + IGNORE_DUP_KEY: 2 request sinh so lieu IoT cung luc -> dong trung bi nuot im lang,
-- khong loi, khong nhan doi du lieu (chong race khong can khoa)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NhietDoLog' AND object_id = OBJECT_ID('dbo.NhietDoLog'))
    CREATE UNIQUE INDEX UX_NhietDoLog ON dbo.NhietDoLog (MaCont, ThoiGian, Nguon) WITH (IGNORE_DUP_KEY = ON);
GO
