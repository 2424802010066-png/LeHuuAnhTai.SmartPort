-- =====================================================================
-- XUNG DEM XE — nhan tin hieu tu THIET BI DEM THAT
--
-- Truoc day so dem thuc te phai go tay cuoi ca. Bang nay cho phep cam bien
-- vong tu duoi mat duong (hoac barie co bo dem) POST thang len API moi khi
-- co mot xe di qua. Cuoi ca he thong tu cong xung lai, khong ai go so nua
-- -> doi soat moi thuc su doc lap voi con nguoi.
--
-- MaXungThietBi: ma do CHINH thiet bi sinh, dung de CHONG GUI TRUNG khi
-- mang chap chon va thiet bi gui lai. Khong co no thi mot xe thanh hai xe.
-- =====================================================================

IF OBJECT_ID('XungDemXe', 'U') IS NULL
CREATE TABLE XungDemXe (
    MaXung        INT IDENTITY(1,1) PRIMARY KEY,
    Cong          NVARCHAR(20)  NOT NULL,
    HuongDi       NVARCHAR(10)  NULL,          -- Vào / Ra / null neu thiet bi khong phan biet
    ThoiGian      DATETIME      NOT NULL,
    ThietBi       NVARCHAR(50)  NULL,
    MaXungThietBi NVARCHAR(50)  NULL,
    DaChot        BIT           NOT NULL DEFAULT 0,
    MaDem         INT           NULL,          -- ky doi soat da gom xung nay
    NgayNhan      DATETIME      NOT NULL DEFAULT GETDATE()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XungDemXe_Ky' AND object_id = OBJECT_ID('XungDemXe'))
    CREATE INDEX IX_XungDemXe_Ky ON XungDemXe (Cong, ThoiGian, DaChot);

-- Chong gui trung: cung thiet bi + cung ma xung thi chi nhan mot lan
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_XungDemXe_MaXung' AND object_id = OBJECT_ID('XungDemXe'))
    CREATE UNIQUE INDEX UX_XungDemXe_MaXung ON XungDemXe (ThietBi, MaXungThietBi)
    WHERE MaXungThietBi IS NOT NULL AND ThietBi IS NOT NULL;
