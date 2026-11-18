-- =====================================================================
-- THU PHI TAI CONG
--
-- Doi thu trong nuoc (GTEL OTS) quang cao manh muc nay: phi luot, phi
-- thang, tru dan tu tai khoan tra truoc, tinh theo loai xe. Khong co thi
-- khach so sanh la thua ngay.
--
-- Hai bang:
--   BieuPhiCong — bang gia theo loai xe (sua duoc, khong hardcode)
--   TheXe       — the/tai khoan cua nha xe: thang (khong tinh luot) hoac
--                 tra truoc (moi luot tru dan). Het han/het tien -> tinh luot.
-- Cot them vao XeRaVao ghi lai SO TIEN DA THU cua chinh luot do, de sau nay
-- doi soat khong phai tinh lai theo bieu gia hien tai (bieu gia co the doi).
-- =====================================================================

IF OBJECT_ID('BieuPhiCong', 'U') IS NULL
CREATE TABLE BieuPhiCong (
    MaBP       INT IDENTITY(1,1) PRIMARY KEY,
    LoaiXe     NVARCHAR(40)   NOT NULL,
    PhiLuot    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    PhiThang   DECIMAL(18,2)  NOT NULL DEFAULT 0,
    GhiChu     NVARCHAR(200)  NULL,
    KichHoat   BIT            NOT NULL DEFAULT 1,
    NgayTao    DATETIME       NOT NULL DEFAULT GETDATE()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BieuPhiCong_LoaiXe' AND object_id = OBJECT_ID('BieuPhiCong'))
    CREATE UNIQUE INDEX UX_BieuPhiCong_LoaiXe ON BieuPhiCong (LoaiXe);

IF OBJECT_ID('TheXe', 'U') IS NULL
CREATE TABLE TheXe (
    MaThe      INT IDENTITY(1,1) PRIMARY KEY,
    BienSo     NVARCHAR(20)   NOT NULL,
    TenChuXe   NVARCHAR(150)  NULL,
    DienThoai  NVARCHAR(20)   NULL,
    MaKH       INT            NULL,
    LoaiThe    NVARCHAR(20)   NOT NULL,       -- Tháng / Trả trước
    SoDu       DECIMAL(18,2)  NOT NULL DEFAULT 0,
    HanDen     DATETIME       NULL,
    KichHoat   BIT            NOT NULL DEFAULT 1,
    GhiChu     NVARCHAR(300)  NULL,
    NgayTao    DATETIME       NOT NULL DEFAULT GETDATE()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TheXe_BienSo' AND object_id = OBJECT_ID('TheXe'))
    CREATE UNIQUE INDEX UX_TheXe_BienSo ON TheXe (BienSo);

-- Ghi so tien da thu ngay tren luot qua cong: doi soat ve sau khong phai
-- tinh lai theo bieu gia hien tai (bieu gia thay doi theo thoi gian).
IF COL_LENGTH('XeRaVao', 'PhiCong') IS NULL
    ALTER TABLE XeRaVao ADD PhiCong DECIMAL(18,2) NULL;

IF COL_LENGTH('XeRaVao', 'HinhThucThu') IS NULL
    ALTER TABLE XeRaVao ADD HinhThucThu NVARCHAR(30) NULL;

-- Bieu gia khoi tao (chi them khi bang con trong — khong de len gia da sua)
IF NOT EXISTS (SELECT 1 FROM BieuPhiCong)
BEGIN
    INSERT INTO BieuPhiCong (LoaiXe, PhiLuot, PhiThang, GhiChu) VALUES
        (N'Đầu kéo container', 50000,  900000, N'Xe đầu kéo chở container'),
        (N'Xe tải lớn',        30000,  600000, N'Xe tải trên 3,5 tấn'),
        (N'Xe tải nhỏ',        15000,  350000, N'Xe tải dưới 3,5 tấn'),
        (N'Xe con',            10000,  250000, N'Xe con, xe bán tải'),
        (N'Khác',              20000,  400000, N'Loại xe chưa phân loại');
END
