-- =====================================================================
-- PHIEU LUU BAI (storage clock -> hoa don)
--
-- Truoc day phi luu bai chi la CON SO HIEN THI tren man hinh ho so — xem
-- xong roi thoi, khong ai thu duoc dong nao. Depot song bang tien luu bai,
-- nen thieu buoc "chot thanh phieu" la phan mem khong dung duoc de kinh doanh.
--
-- Bang nay CHOT LAI so tien tai mot thoi diem: chot roi thi du hom sau co
-- sua ngay vao/ra, so tien tren phieu KHONG doi — day la yeu cau bat buoc
-- cua ke toan (chung tu phai bat bien).
-- =====================================================================

IF OBJECT_ID('PhieuLuuBai', 'U') IS NULL
CREATE TABLE PhieuLuuBai (
    MaPhieu      INT IDENTITY(1,1) PRIMARY KEY,
    SoPhieu      NVARCHAR(30)  NOT NULL,
    MaCont       INT           NULL,
    SoCont       NVARCHAR(11)  NULL,
    MaKH         INT           NULL,
    MaHangTau    INT           NULL,

    TuNgay       DATETIME      NOT NULL,
    DenNgay      DATETIME      NOT NULL,
    TongNgay     INT           NOT NULL DEFAULT 0,
    NgayMienPhi  INT           NOT NULL DEFAULT 0,
    NgayTinhPhi  INT           NOT NULL DEFAULT 0,

    DonGiaNgay   DECIMAL(18,2) NOT NULL DEFAULT 0,
    PhiLuuBai    DECIMAL(18,2) NOT NULL DEFAULT 0,
    DonGiaDien   DECIMAL(18,2) NOT NULL DEFAULT 0,
    SoNgayDien   INT           NOT NULL DEFAULT 0,
    PhiDien      DECIMAL(18,2) NOT NULL DEFAULT 0,
    TongTien     DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- Chờ thu / Đã thu / Hủy
    TrangThai    NVARCHAR(20)  NULL,
    NgayLap      DATETIME      NOT NULL DEFAULT GETDATE(),
    NguoiLap     NVARCHAR(50)  NULL,
    NgayThu      DATETIME      NULL,
    HinhThucThu  NVARCHAR(30)  NULL,
    GhiChu       NVARCHAR(300) NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PhieuLuuBai_SoPhieu' AND object_id = OBJECT_ID('PhieuLuuBai'))
    CREATE UNIQUE INDEX UX_PhieuLuuBai_SoPhieu ON PhieuLuuBai (SoPhieu);

-- Tra "cont nay da chot phieu chua" chay moi lan mo bang ke -> can index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PhieuLuuBai_Cont' AND object_id = OBJECT_ID('PhieuLuuBai'))
    CREATE INDEX IX_PhieuLuuBai_Cont ON PhieuLuuBai (MaCont, TrangThai);
