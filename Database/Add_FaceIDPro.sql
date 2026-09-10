/* ============================================================
   FACE ID PRO — bo sung cot chat luong mau khuon mat (idempotent)
   Chay tren CA local lan hosting.
   ============================================================ */

IF OBJECT_ID('dbo.KhuonMat', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.KhuonMat (
        MaKM       INT IDENTITY(1,1) PRIMARY KEY,
        MaTK       INT NOT NULL,
        VectorJson NVARCHAR(MAX) NOT NULL,
        GocChup    NVARCHAR(20) NULL,
        NgayTao    DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_KhuonMat_TaiKhoan FOREIGN KEY (MaTK) REFERENCES dbo.TaiKhoan(MaTK) ON DELETE CASCADE
    );
    PRINT 'Da tao bang KhuonMat.';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KhuonMat') AND name = 'ChatLuong')
BEGIN
    ALTER TABLE dbo.KhuonMat ADD ChatLuong DECIMAL(5,1) NULL;
    PRINT 'Da them cot ChatLuong.';
END
ELSE PRINT 'Cot ChatLuong da co.';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KhuonMat') AND name = 'DetScore')
BEGIN
    ALTER TABLE dbo.KhuonMat ADD DetScore DECIMAL(6,3) NULL;
    PRINT 'Da them cot DetScore.';
END
ELSE PRINT 'Cot DetScore da co.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KhuonMat_MaTK' AND object_id = OBJECT_ID('dbo.KhuonMat'))
    CREATE INDEX IX_KhuonMat_MaTK ON dbo.KhuonMat (MaTK);

SELECT SoMauKhuonMat = COUNT(*), SoTaiKhoanDaDangKy = COUNT(DISTINCT MaTK) FROM dbo.KhuonMat;
