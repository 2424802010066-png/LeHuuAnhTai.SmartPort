-- ============================================================
-- CHUC NANG QUEN MAT KHAU — bang luu yeu cau dat lai mat khau
-- bang ma xac nhan 6 so gui qua email.
--   * KHONG luu ma goc: chi luu HMAC-SHA256 cua ma (khoa bi mat nam o server)
--     -> lo CSDL cung khong doc ra ma.
--   * Moi yeu cau: het han sau 15 phut, sai toi da 5 lan, dung 1 lan.
-- Chay tren may (SmartPortDB) va tren hosting (somee: dan vao o "Run scripts").
-- Script idempotent: chay nhieu lan khong loi, KHONG xoa du lieu.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DatLaiMatKhau')
BEGIN
    CREATE TABLE dbo.DatLaiMatKhau (
        MaYeuCau   INT IDENTITY(1,1) PRIMARY KEY,
        MaTK       INT NOT NULL,
        MaHash     CHAR(64) NOT NULL,                 -- HMAC-SHA256 (hex) cua ma 6 so
        TaoLuc     DATETIME2 NOT NULL,
        HetHanLuc  DATETIME2 NOT NULL,
        SoLanSai   INT NOT NULL CONSTRAINT DF_DatLaiMatKhau_SoLanSai DEFAULT 0,
        DaDungLuc  DATETIME2 NULL,                    -- da dat lai mat khau thanh cong
        HuyLuc     DATETIME2 NULL,                    -- bi huy (co ma moi hon / sai qua 5 lan)
        DiaChiIP   NVARCHAR(45) NULL,
        TrinhDuyet NVARCHAR(200) NULL,
        CONSTRAINT FK_DatLaiMatKhau_TaiKhoan
            FOREIGN KEY (MaTK) REFERENCES dbo.TaiKhoan(MaTK) ON DELETE CASCADE
    );
    CREATE INDEX IX_DatLaiMatKhau_MaTK_TaoLuc ON dbo.DatLaiMatKhau(MaTK, TaoLuc DESC);
    PRINT N'Da tao bang DatLaiMatKhau.';
END
ELSE
    PRINT N'Bang DatLaiMatKhau da ton tai - bo qua.';

SELECT COUNT(*) AS SoYeuCauDatLaiMatKhau FROM dbo.DatLaiMatKhau;
