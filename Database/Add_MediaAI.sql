-- ============================================================
-- KHO LUU TRU MEDIA AI — noi tap trung MOI anh/video do cac
-- module AI sinh ra (kiem dem, an ninh, ALPR...). Idempotent.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MediaAI')
BEGIN
    CREATE TABLE dbo.MediaAI (
        MaMD     INT IDENTITY(1,1) PRIMARY KEY,
        Loai     NVARCHAR(10)  NOT NULL,       -- Anh / Video
        Nguon    NVARCHAR(30)  NOT NULL,       -- KiemDem / AnNinh / ALPR / CameraAI
        MoTa     NVARCHAR(250) NULL,
        DuongDan NVARCHAR(300) NOT NULL,       -- ~/Uploads/media/xxx
        ThoiGian DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_MediaAI_ThoiGian ON dbo.MediaAI(ThoiGian DESC);
    PRINT N'Da tao bang MediaAI.';
END
ELSE PRINT N'MediaAI da ton tai.';

SELECT COUNT(*) AS TongMedia FROM dbo.MediaAI;
