-- =====================================================================
-- DEM XE DOI CHIEU — CHONG GIAN LAN TAI CONG
--
-- Van de that o cang/depot: bao ve co the cho xe qua ma KHONG bam ghi
-- nhan tren he thong, roi thu tien bo tui. Phan mem khong bao gio biet,
-- vi no chi thay nhung gi duoc nhap vao no.
--
-- Cach phat hien: dat mot bo dem doc lap (cam bien vong tu duoi mat duong,
-- hoac dem tay theo ca). Cuoi ca doi chieu SO DEM voi SO LUOT HE THONG.
-- Chenh lech = so xe qua cong ma khong qua he thong.
--
-- Cot MoBarieTay ghi lai cac lan mo barie thu cong — day la thu hai ma
-- thanh tra hoi den.
-- =====================================================================

IF OBJECT_ID('DemXeCong', 'U') IS NULL
CREATE TABLE DemXeCong (
    MaDem       INT IDENTITY(1,1) PRIMARY KEY,
    Cong        NVARCHAR(20)   NOT NULL,
    Ca          NVARCHAR(20)   NULL,           -- Ca 1 / Ca 2 / Ca 3
    TuLuc       DATETIME       NOT NULL,
    DenLuc      DATETIME       NOT NULL,
    SoDemThucTe INT            NOT NULL DEFAULT 0,   -- tu cam bien / dem tay
    SoHeThong   INT            NOT NULL DEFAULT 0,   -- he thong tu dem, khong sua duoc
    ChenhLech   INT            NOT NULL DEFAULT 0,
    MoBarieTay  INT            NOT NULL DEFAULT 0,
    Nguon       NVARCHAR(20)   NULL,           -- CamBien / ThuCong
    NguoiNhap   NVARCHAR(50)   NULL,
    GhiChu      NVARCHAR(300)  NULL,
    DaGiaiTrinh BIT            NOT NULL DEFAULT 0,
    GiaiTrinh   NVARCHAR(500)  NULL,
    NgayTao     DATETIME       NOT NULL DEFAULT GETDATE()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DemXeCong_Ky' AND object_id = OBJECT_ID('DemXeCong'))
    CREATE INDEX IX_DemXeCong_Ky ON DemXeCong (Cong, TuLuc);
