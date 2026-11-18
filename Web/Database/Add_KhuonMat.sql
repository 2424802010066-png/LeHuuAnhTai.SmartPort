-- ============================================================
-- MODULE FACE LOGIN — bang luu vector khuon mat (ArcFace 512D)
-- Vector duoc luu dang JSON text trong NVARCHAR(MAX);
-- viec so khop cosine do ASP.NET (FaceApiService) thuc hien.
-- Script idempotent: chay nhieu lan khong loi.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KhuonMat')
BEGIN
    CREATE TABLE dbo.KhuonMat (
        MaKM      INT IDENTITY(1,1) PRIMARY KEY,
        MaTK      INT NOT NULL,
        VectorJson NVARCHAR(MAX) NOT NULL,      -- JSON mang 512 so thuc
        GocChup   NVARCHAR(20) NULL,            -- Thang / Trai / Phai
        NgayTao   DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_KhuonMat_TaiKhoan
            FOREIGN KEY (MaTK) REFERENCES dbo.TaiKhoan(MaTK) ON DELETE CASCADE
    );
    CREATE INDEX IX_KhuonMat_MaTK ON dbo.KhuonMat(MaTK);
    PRINT N'Da tao bang KhuonMat.';
END
ELSE
    PRINT N'Bang KhuonMat da ton tai - bo qua.';

SELECT COUNT(*) AS SoKhuonMatDaDangKy FROM dbo.KhuonMat;
