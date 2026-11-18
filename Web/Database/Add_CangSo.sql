-- ============================================================
-- CANG SO: eDO, to khai hai quan (mo phong VASSCM), lich hen gio vao cong (TAS), doi chieu container.
-- Ban GOP (1 CSDL — somee / may): chay script nay 1 lan. Ban MICROSERVICES: dung_csdl_dich_vu.py tu tao
-- trong CSDL cua dich vu so huu. Idempotent: chay nhieu lan khong loi, KHONG xoa du lieu.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LenhGiaoHangDienTu')
BEGIN
    CREATE TABLE dbo.LenhGiaoHangDienTu (
        MaEDO         INT IDENTITY(1,1) PRIMARY KEY,
        SoEDO         NVARCHAR(30)  NOT NULL,
        SoVanDon      NVARCHAR(30)  NULL,
        SoCont        NVARCHAR(11)  NOT NULL,
        MaHangTau     INT NULL,
        MaKH          INT NULL,
        TenNguoiNhan  NVARCHAR(150) NULL,
        HanLenh       DATETIME2 NOT NULL,
        TrangThai     NVARCHAR(20)  NOT NULL,
        MaXacThuc     NVARCHAR(64)  NULL,
        NgayPhatHanh  DATETIME2 NOT NULL,
        NguoiPhatHanh NVARCHAR(100) NULL,
        NgaySuDung    DATETIME2 NULL,
        MaXV          INT NULL,
        GhiChu        NVARCHAR(300) NULL
    );
    CREATE UNIQUE INDEX UX_LenhGiaoHangDienTu_SoEDO ON dbo.LenhGiaoHangDienTu(SoEDO);
    CREATE INDEX IX_LenhGiaoHangDienTu_SoCont ON dbo.LenhGiaoHangDienTu(SoCont, TrangThai);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ToKhaiHaiQuan')
BEGIN
    CREATE TABLE dbo.ToKhaiHaiQuan (
        MaToKhai       INT IDENTITY(1,1) PRIMARY KEY,
        SoToKhai       NVARCHAR(20)  NOT NULL,
        LoaiHinh       NVARCHAR(10)  NOT NULL,
        HuongHang      NVARCHAR(10)  NOT NULL,
        SoCont         NVARCHAR(11)  NOT NULL,
        MaKH           INT NULL,
        TenHang        NVARCHAR(200) NULL,
        MaHS           NVARCHAR(10)  NULL,
        TriGia         DECIMAL(18, 2) NULL,
        PhanLuong      NVARCHAR(10)  NULL,
        LyDoPhanLuong  NVARCHAR(300) NULL,
        TrangThai      NVARCHAR(40)  NOT NULL,
        ChiCuc         NVARCHAR(100) NULL,
        NgayKhai       DATETIME2 NOT NULL,
        NgayThongQuan  DATETIME2 NULL,
        NgayQuaGiamSat DATETIME2 NULL,
        NguoiCapNhat   NVARCHAR(100) NULL,
        GhiChu         NVARCHAR(300) NULL
    );
    CREATE UNIQUE INDEX UX_ToKhaiHaiQuan_SoToKhai_SoCont ON dbo.ToKhaiHaiQuan(SoToKhai, SoCont);
    CREATE INDEX IX_ToKhaiHaiQuan_SoCont ON dbo.ToKhaiHaiQuan(SoCont, TrangThai);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LichHenXe')
BEGIN
    CREATE TABLE dbo.LichHenXe (
        MaLich     INT IDENTITY(1,1) PRIMARY KEY,
        MaLenh     INT NULL,
        SoLenh     NVARCHAR(30) NULL,
        BienSo     NVARCHAR(15) NOT NULL,
        SoCont     NVARCHAR(11) NULL,
        LoaiLenh   NVARCHAR(20) NULL,
        HenTuLuc   DATETIME2 NOT NULL,
        HenDenLuc  DATETIME2 NOT NULL,
        TrangThai  NVARCHAR(20) NOT NULL,
        DenLuc     DATETIME2 NULL,
        MaXV       INT NULL,
        SoPhutLech INT NULL,
        NguonDat   NVARCHAR(30) NULL,
        NguoiDat   NVARCHAR(100) NULL,
        MaKH       INT NULL,
        NgayDat    DATETIME2 NOT NULL
    );
    CREATE INDEX IX_LichHenXe_BienSo_Hen ON dbo.LichHenXe(BienSo, HenTuLuc);
    CREATE INDEX IX_LichHenXe_TrangThai_Hen ON dbo.LichHenXe(TrangThai, HenDenLuc);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KhungGioCong')
BEGIN
    CREATE TABLE dbo.KhungGioCong (
        MaKhung INT IDENTITY(1,1) PRIMARY KEY,
        Ngay    DATE NOT NULL,
        Gio     INT  NOT NULL,
        SucChua INT  NOT NULL,
        GhiChu  NVARCHAR(200) NULL
    );
    CREATE UNIQUE INDEX UX_KhungGioCong_Ngay_Gio ON dbo.KhungGioCong(Ngay, Gio);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DoiChieuContainer')
BEGIN
    CREATE TABLE dbo.DoiChieuContainer (
        MaDoiChieu    INT IDENTITY(1,1) PRIMARY KEY,
        MaXV          INT NULL,
        MaLenh        INT NULL,
        BienSo        NVARCHAR(15) NULL,
        HuongDi       NVARCHAR(10) NULL,
        SoContLenh    NVARCHAR(11) NULL,
        SoContDocDuoc NVARCHAR(11) NULL,
        HopLeIso      BIT NOT NULL DEFAULT 0,
        DoTinCay      FLOAT NULL,
        DiemGiongNhau FLOAT NULL,
        KetQua        NVARCHAR(20) NOT NULL,
        AnhCont       NVARCHAR(300) NULL,
        DacTrung      NVARCHAR(MAX) NULL,
        ThoiGian      DATETIME2 NOT NULL,
        GhiChu        NVARCHAR(300) NULL
    );
    CREATE INDEX IX_DoiChieuContainer_ThoiGian ON dbo.DoiChieuContainer(ThoiGian DESC);
END
PRINT N'Cang so: da co du 5 bang (LenhGiaoHangDienTu, ToKhaiHaiQuan, LichHenXe, KhungGioCong, DoiChieuContainer).';
