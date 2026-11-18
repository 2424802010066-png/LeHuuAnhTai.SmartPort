-- ============================================================
-- DANG NHAP / DANG KY BANG GOOGLE VA SO DIEN THOAI (OTP SMS)
--   * LienKetDangNhap: tai khoan SmartPort <-> danh tinh ngoai (Google "sub" / so dien thoai E.164).
--     UNIQUE (NhaCungCap, MaNgoai): 1 tai khoan Google / 1 so dien thoai chi vao duoc DUNG 1 tai khoan.
--   * MaXacThucSdt: ma OTP 6 so gui SMS — KHONG luu ma goc, chi luu HMAC-SHA256 (khoa bi mat o server).
-- Chay tren may (SmartPortDB / SmartPortMS_DinhDanh) va tren hosting (somee: dan vao o "Run scripts").
-- Script idempotent: chay nhieu lan khong loi, KHONG xoa du lieu.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LienKetDangNhap')
BEGIN
    CREATE TABLE dbo.LienKetDangNhap (
        MaLK        INT IDENTITY(1,1) PRIMARY KEY,
        MaTK        INT NOT NULL,
        NhaCungCap  VARCHAR(20) NOT NULL,               -- google | sdt
        MaNgoai     NVARCHAR(100) NOT NULL,             -- google: "sub" ; sdt: +84...
        Email       NVARCHAR(100) NULL,
        TenHienThi  NVARCHAR(100) NULL,
        TaoLuc      DATETIME2 NOT NULL,
        DungLanCuoi DATETIME2 NULL,
        CONSTRAINT UQ_LienKetDangNhap_NhaCungCap_MaNgoai UNIQUE (NhaCungCap, MaNgoai),
        CONSTRAINT FK_LienKetDangNhap_TaiKhoan
            FOREIGN KEY (MaTK) REFERENCES dbo.TaiKhoan(MaTK) ON DELETE CASCADE
    );
    CREATE INDEX IX_LienKetDangNhap_MaTK ON dbo.LienKetDangNhap(MaTK);
    PRINT N'Da tao bang LienKetDangNhap.';
END
ELSE
    PRINT N'Bang LienKetDangNhap da ton tai - bo qua.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaXacThucSdt')
BEGIN
    CREATE TABLE dbo.MaXacThucSdt (
        MaYeuCau    INT IDENTITY(1,1) PRIMARY KEY,
        SoDienThoai VARCHAR(20) NOT NULL,               -- E.164
        MucDich     VARCHAR(20) NOT NULL,               -- dang-nhap | lien-ket
        MaTK        INT NULL,                           -- lien-ket: tai khoan dang dang nhap
        MaHash      CHAR(64) NOT NULL,                  -- HMAC-SHA256 (hex) cua ma 6 so
        TaoLuc      DATETIME2 NOT NULL,
        HetHanLuc   DATETIME2 NOT NULL,
        SoLanSai    INT NOT NULL CONSTRAINT DF_MaXacThucSdt_SoLanSai DEFAULT 0,
        DaDungLuc   DATETIME2 NULL,
        HuyLuc      DATETIME2 NULL,
        DiaChiIP    NVARCHAR(45) NULL
    );
    CREATE INDEX IX_MaXacThucSdt_Sdt_TaoLuc ON dbo.MaXacThucSdt(SoDienThoai, TaoLuc DESC);
    CREATE INDEX IX_MaXacThucSdt_IP_TaoLuc ON dbo.MaXacThucSdt(DiaChiIP, TaoLuc DESC);
    PRINT N'Da tao bang MaXacThucSdt.';
END
ELSE
    PRINT N'Bang MaXacThucSdt da ton tai - bo qua.';
GO

SELECT (SELECT COUNT(*) FROM dbo.LienKetDangNhap) AS SoLienKet, (SELECT COUNT(*) FROM dbo.MaXacThucSdt) AS SoMaOtp;
