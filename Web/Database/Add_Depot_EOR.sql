-- ============================================================
-- NGHIEP VU DEPOT CONTAINER RONG
--  1) Container gan HANG TAU  -> ton rong tach theo hang
--  2) Bang PhieuSuaChua (EOR - Estimate of Repair): kiem vo, bao gia
--     sua gui hang tau duyet. Day la nghiep vu song con cua depot rong.
-- Chay tren CA local (.\MSSQLSERVER01) lan hosting (somee)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Container') AND name = 'MaHangTau')
    ALTER TABLE dbo.Container ADD MaHangTau INT NULL;
GO

IF OBJECT_ID('dbo.PhieuSuaChua', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PhieuSuaChua (
        MaEOR         INT IDENTITY(1,1) PRIMARY KEY,
        SoEOR         NVARCHAR(30)  NULL,      -- EOR-yyMM-00001
        MaCont        INT           NULL,
        SoCont        NVARCHAR(11)  NULL,
        MaHangTau     INT           NULL,
        ViTriHuHong   NVARCHAR(50)  NULL,      -- Vach truoc / Vach ben / Cua / San / Noc
        MaHuHong      NVARCHAR(20)  NULL,      -- ma theo bang cua hang tau (DT/BR/CU...)
        MoTa          NVARCHAR(300) NULL,
        GioCong       DECIMAL(6,2)  NULL,
        ChiPhiVatTu   DECIMAL(18,2) NULL,
        ChiPhiUocTinh DECIMAL(18,2) NULL,
        TrangThai     NVARCHAR(20)  NULL,      -- Chờ duyệt / Đã duyệt / Từ chối / Đã sửa
        AnhHuHong     NVARCHAR(300) NULL,
        NguoiLap      NVARCHAR(50)  NULL,
        NgayLap       DATETIME      NOT NULL,
        NgayDuyet     DATETIME      NULL,
        GhiChu        NVARCHAR(300) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PhieuSuaChua_Cont' AND object_id = OBJECT_ID('dbo.PhieuSuaChua'))
    CREATE INDEX IX_PhieuSuaChua_Cont ON dbo.PhieuSuaChua (SoCont, NgayLap DESC);
GO
