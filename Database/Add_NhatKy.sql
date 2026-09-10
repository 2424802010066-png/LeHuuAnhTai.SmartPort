/* ============================================================
   NANG CAP FULL STACK — chay tren CA local lan hosting (idempotent)
   1. Bang NhatKy (audit log)
   2. Noi cot MatKhauHash VARCHAR(64) -> VARCHAR(200) cho PBKDF2
   3. Index tang toc cac truy van hay dung
   ============================================================ */

-- 1. Bang nhat ky hoat dong
IF OBJECT_ID('dbo.NhatKy', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NhatKy (
        MaNK      INT IDENTITY(1,1) PRIMARY KEY,
        Loai      VARCHAR(20)   NOT NULL,        -- DangNhap / ThaoTac / BaoMat / Loi
        TaiKhoan  NVARCHAR(50)  NULL,
        NoiDung   NVARCHAR(500) NOT NULL,
        DiaChiIP  VARCHAR(45)   NULL,
        ThoiGian  DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Da tao bang NhatKy.';
END
ELSE PRINT 'Bang NhatKy da co.';

-- 2. Noi cot mat khau cho hash PBKDF2 (~90 ky tu)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.TaiKhoan') AND name = 'MatKhauHash' AND max_length < 200
)
BEGIN
    ALTER TABLE dbo.TaiKhoan ALTER COLUMN MatKhauHash VARCHAR(200) NOT NULL;
    PRINT 'Da noi MatKhauHash -> VARCHAR(200).';
END
ELSE PRINT 'MatKhauHash da du rong.';

-- 3. Index tang toc (bo qua neu da co)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhatKy_ThoiGian' AND object_id = OBJECT_ID('dbo.NhatKy'))
    CREATE INDEX IX_NhatKy_ThoiGian ON dbo.NhatKy (ThoiGian DESC) INCLUDE (Loai);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhatKy_Loai' AND object_id = OBJECT_ID('dbo.NhatKy'))
    CREATE INDEX IX_NhatKy_Loai ON dbo.NhatKy (Loai, ThoiGian DESC);
IF OBJECT_ID('dbo.SuCoAnNinh', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SuCo_TrangThai' AND object_id = OBJECT_ID('dbo.SuCoAnNinh'))
    CREATE INDEX IX_SuCo_TrangThai ON dbo.SuCoAnNinh (TrangThai, ThoiGian DESC);
IF OBJECT_ID('dbo.NhanDienAI', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhanDien_ThoiGian' AND object_id = OBJECT_ID('dbo.NhanDienAI'))
    CREATE INDEX IX_NhanDien_ThoiGian ON dbo.NhanDienAI (ThoiGian DESC);
IF OBJECT_ID('dbo.XeRaVao', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XeRaVao_ThoiGian' AND object_id = OBJECT_ID('dbo.XeRaVao'))
    CREATE INDEX IX_XeRaVao_ThoiGian ON dbo.XeRaVao (ThoiGian DESC);
IF OBJECT_ID('dbo.DatDichVu', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DatDichVu_TrangThai' AND object_id = OBJECT_ID('dbo.DatDichVu'))
    CREATE INDEX IX_DatDichVu_TrangThai ON dbo.DatDichVu (TrangThai);
IF OBJECT_ID('dbo.ThanhToan', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ThanhToan_TrangThai' AND object_id = OBJECT_ID('dbo.ThanhToan'))
    CREATE INDEX IX_ThanhToan_TrangThai ON dbo.ThanhToan (TrangThai);
IF OBJECT_ID('dbo.MediaAI', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MediaAI_ThoiGian' AND object_id = OBJECT_ID('dbo.MediaAI'))
    CREATE INDEX IX_MediaAI_ThoiGian ON dbo.MediaAI (ThoiGian DESC);
PRINT 'Index OK.';

SELECT SoNhatKy = COUNT(*) FROM dbo.NhatKy;
