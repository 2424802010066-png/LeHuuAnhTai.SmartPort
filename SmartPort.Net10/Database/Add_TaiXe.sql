-- =====================================================================
-- HO SO TAI XE — doc tu QR the CCCD gan chip / OCR bang lai
--
-- Muc tieu: rut ngan thoi gian dang ky tai cong. Hien tai bao ve chep tay
-- ho ten, so CCCD tu the giay -> cham, sai chinh ta, va khong tra cuu lai
-- duoc. The CCCD gan chip co san MA QR chua day du thong tin, quet mot cai
-- la xong — chinh xac 100%%, khong phu thuoc chat luong anh nhu OCR.
--
-- Lan sau xe do vao, he thong nhan ra tai xe ngay, khong phai quet lai.
-- =====================================================================

IF OBJECT_ID('TaiXe', 'U') IS NULL
CREATE TABLE TaiXe (
    MaTX        INT IDENTITY(1,1) PRIMARY KEY,
    SoGiayTo    NVARCHAR(20)   NOT NULL,
    LoaiGiayTo  NVARCHAR(10)   NULL,          -- CCCD / GPLX
    HoTen       NVARCHAR(100)  NULL,
    NgaySinh    DATE           NULL,
    GioiTinh    NVARCHAR(10)   NULL,
    DiaChi      NVARCHAR(300)  NULL,
    NgayCapCCCD DATE           NULL,
    SoGPLX      NVARCHAR(20)   NULL,
    HangGPLX    NVARCHAR(10)   NULL,
    HanGPLX     DATE           NULL,
    BienSo      NVARCHAR(20)   NULL,
    DienThoai   NVARCHAR(20)   NULL,
    AnhGiayTo   NVARCHAR(300)  NULL,
    GhiChu      NVARCHAR(300)  NULL,
    NgayTao     DATETIME       NOT NULL DEFAULT GETDATE(),
    LanVaoCuoi  DATETIME       NULL,
    SoLanVao    INT            NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TaiXe_SoGiayTo' AND object_id = OBJECT_ID('TaiXe'))
    CREATE UNIQUE INDEX UX_TaiXe_SoGiayTo ON TaiXe (SoGiayTo);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaiXe_BienSo' AND object_id = OBJECT_ID('TaiXe'))
    CREATE INDEX IX_TaiXe_BienSo ON TaiXe (BienSo);

-- Gan luot qua cong voi tai xe da xac thuc giay to
IF COL_LENGTH('XeRaVao', 'MaTX') IS NULL
    ALTER TABLE XeRaVao ADD MaTX INT NULL;
