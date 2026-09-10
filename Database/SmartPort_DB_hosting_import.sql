/* ============================================================
   SMARTPORT - BAN DA CAT SAN DE CHAY TREN HOSTING (somee.com)
   Khong co dong DROP/CREATE DATABASE vi host da tao san database roi.
   Mo file nay, bam Ctrl+A (chon het) roi Ctrl+C (copy), dan vao o
   "Run scripts" tren somee.com roi bam Execute/Run.
   ============================================================ */

/* ================= BANG DANH MUC ================= */
CREATE TABLE KhachHang (
    MaKH INT IDENTITY(1,1) PRIMARY KEY,
    TenCongTy NVARCHAR(150) NOT NULL,
    MaSoThue VARCHAR(20) NULL,
    DiaChi NVARCHAR(200) NULL,
    NguoiLienHe NVARCHAR(100) NULL,
    Email VARCHAR(100) NULL,
    SoDienThoai VARCHAR(20) NULL,
    NgayTao DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE TaiKhoan (
    MaTK INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap VARCHAR(50) NOT NULL UNIQUE,
    MatKhauHash VARCHAR(64) NOT NULL,
    HoTen NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) NULL,
    SoDienThoai VARCHAR(20) NULL,
    VaiTro VARCHAR(20) NOT NULL DEFAULT 'KhachHang', -- Admin / NhanVien / KhachHang
    MaKH INT NULL REFERENCES KhachHang(MaKH),
    KichHoat BIT NOT NULL DEFAULT 1,
    NgayTao DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE HangTau (
    MaHangTau INT IDENTITY(1,1) PRIMARY KEY,
    TenHangTau NVARCHAR(100) NOT NULL,
    MaSCAC VARCHAR(10) NULL,
    QuocGia NVARCHAR(50) NULL,
    Website VARCHAR(100) NULL
);

CREATE TABLE Tau (
    MaTau INT IDENTITY(1,1) PRIMARY KEY,
    TenTau NVARCHAR(100) NOT NULL,
    SoIMO VARCHAR(15) NULL,
    MaHangTau INT NULL REFERENCES HangTau(MaHangTau),
    LoaiTau NVARCHAR(50) NULL,          -- Container / Hang roi / Tong hop
    QuocTich NVARCHAR(50) NULL,
    ChieuDai DECIMAL(6,1) NULL,          -- LOA (m)
    MonNuoc DECIMAL(4,1) NULL,           -- draft (m)
    SucChuaTEU INT NULL
);

CREATE TABLE Ben (
    MaBen INT IDENTITY(1,1) PRIMARY KEY,
    TenBen NVARCHAR(50) NOT NULL,
    ChieuDai DECIMAL(6,1) NOT NULL,      -- m
    DoSau DECIMAL(4,1) NOT NULL,         -- m
    SoCau INT NOT NULL DEFAULT 2,        -- so cau bo STS
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Hoạt động',  -- Hoat dong / Bao tri
    MoTa NVARCHAR(200) NULL
);

CREATE TABLE ChuyenTau (
    MaChuyen INT IDENTITY(1,1) PRIMARY KEY,
    MaChuyenHieu VARCHAR(20) NOT NULL,
    MaTau INT NOT NULL REFERENCES Tau(MaTau),
    MaBen INT NULL REFERENCES Ben(MaBen),
    ETA DATETIME NOT NULL,
    ETD DATETIME NOT NULL,
    ATA DATETIME NULL,
    ATD DATETIME NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Dự kiến',
    -- Du kien / Da cap ben / Dang lam hang / Da roi ben / Huy
    ContNhap INT NOT NULL DEFAULT 0,
    ContXuat INT NOT NULL DEFAULT 0,
    GhiChu NVARCHAR(200) NULL
);

CREATE TABLE Container (
    MaCont INT IDENTITY(1,1) PRIMARY KEY,
    SoCont VARCHAR(11) NOT NULL UNIQUE,
    LoaiCont VARCHAR(10) NOT NULL,       -- 20GP/40GP/40HC/20RF/40RF/45HC
    TinhTrang VARCHAR(10) NOT NULL DEFAULT 'Full',   -- Full / Empty
    TrongLuong DECIMAL(8,2) NULL,        -- tan
    HangHoa NVARCHAR(200) NULL,
    MaKH INT NULL REFERENCES KhachHang(MaKH),
    MaChuyenNhap INT NULL REFERENCES ChuyenTau(MaChuyen),
    MaChuyenXuat INT NULL REFERENCES ChuyenTau(MaChuyen),
    ViTriBai VARCHAR(20) NULL,           -- Block-Bay-Row-Tier
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Trong bãi',
    -- Trong bai / Cho xuat tau / Da xuat tau / Da giao khach
    NgayVaoBai DATETIME NULL,
    NgayRaBai DATETIME NULL,
    NhietDo DECIMAL(4,1) NULL            -- reefer
);

CREATE TABLE DichVu (
    MaDV INT IDENTITY(1,1) PRIMARY KEY,
    TenDV NVARCHAR(100) NOT NULL,
    NhomDV NVARCHAR(50) NULL,
    DonViTinh NVARCHAR(30) NULL,
    DonGia DECIMAL(12,0) NOT NULL DEFAULT 0,
    MoTa NVARCHAR(300) NULL,
    KichHoat BIT NOT NULL DEFAULT 1
);

CREATE TABLE DatDichVu (
    MaBooking INT IDENTITY(1,1) PRIMARY KEY,
    MaSoBooking VARCHAR(15) NOT NULL UNIQUE,
    MaKH INT NOT NULL REFERENCES KhachHang(MaKH),
    NgayTao DATETIME NOT NULL DEFAULT GETDATE(),
    NgayCanThucHien DATETIME NULL,
    TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Chờ duyệt',
    -- Cho duyet / Da duyet / Dang thuc hien / Hoan thanh / Da huy
    GhiChu NVARCHAR(300) NULL,
    TongTien DECIMAL(14,0) NOT NULL DEFAULT 0
);

CREATE TABLE DatDichVuChiTiet (
    MaCT INT IDENTITY(1,1) PRIMARY KEY,
    MaBooking INT NOT NULL REFERENCES DatDichVu(MaBooking) ON DELETE CASCADE,
    MaDV INT NOT NULL REFERENCES DichVu(MaDV),
    SoLuong INT NOT NULL DEFAULT 1,
    DonGia DECIMAL(12,0) NOT NULL DEFAULT 0,
    ThanhTien DECIMAL(14,0) NOT NULL DEFAULT 0,
    GhiChu NVARCHAR(200) NULL
);

CREATE TABLE TinTuc (
    MaTin INT IDENTITY(1,1) PRIMARY KEY,
    TieuDe NVARCHAR(200) NOT NULL,
    TomTat NVARCHAR(400) NULL,
    NoiDung NVARCHAR(MAX) NULL,
    AnhMau VARCHAR(30) NULL,             -- ma gradient hien thi thumbnail
    NgayDang DATETIME NOT NULL DEFAULT GETDATE(),
    LuotXem INT NOT NULL DEFAULT 0,
    NoiBat BIT NOT NULL DEFAULT 0
);

CREATE TABLE LienHe (
    MaLH INT IDENTITY(1,1) PRIMARY KEY,
    HoTen NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) NULL,
    SoDienThoai VARCHAR(20) NULL,
    ChuDe NVARCHAR(150) NULL,
    NoiDung NVARCHAR(1000) NULL,
    NgayGui DATETIME NOT NULL DEFAULT GETDATE(),
    DaXuLy BIT NOT NULL DEFAULT 0
);

CREATE TABLE SanLuongThang (
    MaSL INT IDENTITY(1,1) PRIMARY KEY,
    Nam INT NOT NULL,
    Thang INT NOT NULL,
    TEU INT NOT NULL DEFAULT 0,
    DoanhThu DECIMAL(15,0) NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SanLuong UNIQUE(Nam, Thang)
);

CREATE TABLE CauHinhAnh (
    Khoa VARCHAR(30) NOT NULL PRIMARY KEY,
    Url VARCHAR(500) NOT NULL,
    MoTa NVARCHAR(100) NULL,
    ThuTu INT NOT NULL DEFAULT 0
);
GO

/* ================= DU LIEU MAU ================= */
SET NOCOUNT ON;

-- Khach hang
INSERT INTO KhachHang (TenCongTy, MaSoThue, DiaChi, NguoiLienHe, Email, SoDienThoai) VALUES
(N'Công ty TNHH XNK Đại Phát',        '0312456789', N'125 Nguyễn Tất Thành, Q.4, TP.HCM',      N'Lê Hữu Anh Tài',    'contact@daiphat.vn',   '0283940123'),
(N'Công ty CP Logistics Miền Nam',    '0309887766', N'88 Trường Sa, Q.Bình Thạnh, TP.HCM',     N'Trần Quốc Bảo',     'info@southlog.vn',     '0283777888'),
(N'Công ty TNHH Thủy sản Cửu Long',   '1801234567', N'KCN Trà Nóc, Cần Thơ',                   N'Nguyễn Thu Hà',     'sales@culongseafood.vn','0292383456'),
(N'Công ty CP Dệt may Thắng Lợi',     '0301112223', N'2 Trường Chinh, Q.Tân Phú, TP.HCM',      N'Phạm Minh Châu',    'export@vigatexco.vn',  '0283812345'),
(N'Công ty TNHH Điện tử VinaTech',    '0315556667', N'KCX Tân Thuận, Q.7, TP.HCM',             N'Đỗ Hoàng Long',     'purchase@vinatech.vn', '0283770999'),
(N'Công ty CP Nông sản Tây Nguyên',   '6001239876', N'12 Lê Duẩn, TP. Buôn Ma Thuột, Đắk Lắk', N'Hồ Thị Mai',        'trade@taynguyenagri.vn','0262385678');

-- Tai khoan (mat khau mac dinh: 123456, hash = SHA256('tendangnhap|123456'))
INSERT INTO TaiKhoan (TenDangNhap, MatKhauHash, HoTen, Email, SoDienThoai, VaiTro, MaKH) VALUES
('admin',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256','admin|123456'),2),  N'Quản trị hệ thống',  'admin@smartport.vn',  '0901234567', 'Admin',     NULL),
('dieudo', CONVERT(VARCHAR(64), HASHBYTES('SHA2_256','dieudo|123456'),2), N'Phạm Văn Điều',      'dieudo@smartport.vn', '0912345678', 'NhanVien',  NULL),
('taile',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256','taile|123456'),2),  N'Lê Hữu Anh Tài',     'tai.le@daiphat.vn',   '0987654321', 'KhachHang', 1),
('thuha',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256','thuha|123456'),2),  N'Nguyễn Thu Hà',      'ha.nguyen@culong.vn', '0977123456', 'KhachHang', 3);

-- Hang tau
INSERT INTO HangTau (TenHangTau, MaSCAC, QuocGia, Website) VALUES
(N'Maersk Line',   'MAEU', N'Đan Mạch',  'www.maersk.com'),
(N'MSC',           'MSCU', N'Thụy Sĩ',   'www.msc.com'),
(N'CMA CGM',       'CMDU', N'Pháp',      'www.cma-cgm.com'),
(N'Evergreen',     'EGLV', N'Đài Loan',  'www.evergreen-line.com'),
(N'Ocean Network Express', 'ONEY', N'Nhật Bản', 'www.one-line.com'),
(N'Hapag-Lloyd',   'HLCU', N'Đức',       'www.hapag-lloyd.com'),
(N'VIMC Lines',    'VIMC', N'Việt Nam',  'www.vimc.co');

-- Tau
INSERT INTO Tau (TenTau, SoIMO, MaHangTau, LoaiTau, QuocTich, ChieuDai, MonNuoc, SucChuaTEU) VALUES
(N'MAERSK SAIGON',    '9303534', 1, N'Container', N'Singapore', 292.0, 12.5, 8400),
(N'MSC MIRELLA',      '9760213', 2, N'Container', N'Panama',    299.9, 13.5, 9400),
(N'CMA CGM MEKONG',   '9674555', 3, N'Container', N'Malta',     269.0, 12.0, 6900),
(N'EVER LUCID',       '9629081', 4, N'Container', N'Đài Loan',  235.0, 10.2, 4200),
(N'ONE HANOI',        '9741234', 5, N'Container', N'Nhật Bản',  210.0, 10.5, 3500),
(N'COLOMBO EXPRESS',  '9295244', 6, N'Container', N'Đức',       300.0, 13.0, 8750),
(N'VIMC PIONEER',     '9155987', 7, N'Hàng rời',  N'Việt Nam',  158.0,  8.2, NULL),
(N'MAERSK EDINBURGH', '9456769', 1, N'Container', N'Đan Mạch',  300.0, 13.2, 8600),
(N'MSC BAMBOO',       '9776171', 2, N'Container', N'Panama',    330.0, 15.0, 14000),
(N'VIMC GLORY',       '9312456', 7, N'Tổng hợp',  N'Việt Nam',  155.0,  7.8, 900);

-- Ben (cau cang)
INSERT INTO Ben (TenBen, ChieuDai, DoSau, SoCau, TrangThai, MoTa) VALUES
(N'Bến B1', 250.0, 11.0, 2, N'Hoạt động', N'Bến container feeder, gần cổng chính'),
(N'Bến B2', 300.0, 14.2, 3, N'Hoạt động', N'Bến container quốc tế'),
(N'Bến B3', 350.0, 15.5, 4, N'Hoạt động', N'Bến nước sâu - tàu mẹ trên 10.000 TEU'),
(N'Bến B4', 200.0,  9.5, 2, N'Hoạt động', N'Bến hàng rời, hàng tổng hợp'),
(N'Bến B5', 320.0, 14.0, 3, N'Hoạt động', N'Bến container quốc tế mở rộng'),
(N'Bến B6', 180.0,  8.5, 1, N'Bảo trì',   N'Đang nâng cấp cẩu bờ, dự kiến hoạt động lại cuối năm');
GO

-- Chuyen tau (dung ngay tuong doi voi GETDATE de demo luon "song")
DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);
INSERT INTO ChuyenTau (MaChuyenHieu, MaTau, MaBen, ETA, ETD, ATA, ATD, TrangThai, ContNhap, ContXuat, GhiChu) VALUES
-- da roi ben
('MAEU-2606S', 1, 3, DATEADD(HOUR,8,  DATEADD(DAY,-12,@d)), DATEADD(HOUR,20, DATEADD(DAY,-11,@d)), DATEADD(HOUR,9,  DATEADD(DAY,-12,@d)), DATEADD(HOUR,22, DATEADD(DAY,-11,@d)), N'Đã rời bến', 620, 580, NULL),
('MSCU-2606N', 2, 2, DATEADD(HOUR,7,  DATEADD(DAY,-9,@d)),  DATEADD(HOUR,19, DATEADD(DAY,-8,@d)),  DATEADD(HOUR,7,  DATEADD(DAY,-9,@d)),  DATEADD(HOUR,21, DATEADD(DAY,-8,@d)),  N'Đã rời bến', 410, 455, NULL),
('EGLV-2607W', 4, 1, DATEADD(HOUR,9,  DATEADD(DAY,-6,@d)),  DATEADD(HOUR,21, DATEADD(DAY,-5,@d)),  DATEADD(HOUR,10, DATEADD(DAY,-6,@d)),  DATEADD(HOUR,23, DATEADD(DAY,-5,@d)),  N'Đã rời bến', 350, 300, NULL),
-- huy
('ONEY-2607X', 5, NULL, DATEADD(HOUR,8, DATEADD(DAY,-3,@d)), DATEADD(HOUR,20, DATEADD(DAY,-2,@d)), NULL, NULL, N'Hủy', 0, 0, N'Hãng tàu đổi lịch trình do thời tiết'),
-- dang lam hang / da cap ben
('CMDU-2607E', 3, 3, DATEADD(HOUR,6,  DATEADD(DAY,-1,@d)),  DATEADD(HOUR,18, DATEADD(DAY,1,@d)),  DATEADD(HOUR,7,  DATEADD(DAY,-1,@d)),  NULL, N'Đang làm hàng', 540, 610, NULL),
('VIMC-2607A', 7, 4, DATEADD(HOUR,5,  @d),                  DATEADD(HOUR,12, DATEADD(DAY,1,@d)),  DATEADD(HOUR,5,  @d),                  NULL, N'Đang làm hàng', 210, 180, N'Hàng rời: 12.000 tấn phân bón'),
('MAEU-2607S', 8, 5, DATEADD(HOUR,13, @d),                  DATEADD(HOUR,6,  DATEADD(DAY,2,@d)),  DATEADD(HOUR,13, @d),                  NULL, N'Đã cập bến', 480, 520, NULL),
-- du kien - da gan ben
('MSCU-2608N', 2, 2, DATEADD(HOUR,7,  DATEADD(DAY,2,@d)),   DATEADD(HOUR,19, DATEADD(DAY,3,@d)),  NULL, NULL, N'Dự kiến', 430, 470, NULL),
('EGLV-2608W', 4, 1, DATEADD(HOUR,9,  DATEADD(DAY,3,@d)),   DATEADD(HOUR,21, DATEADD(DAY,4,@d)),  NULL, NULL, N'Dự kiến', 360, 320, NULL),
-- du kien - CHUA gan ben (demo AI goi y)
('ONEY-2608X', 5, NULL, DATEADD(HOUR,8, DATEADD(DAY,4,@d)), DATEADD(HOUR,20, DATEADD(DAY,5,@d)),  NULL, NULL, N'Dự kiến', 300, 280, NULL),
('HLCU-2608B', 6, NULL, DATEADD(HOUR,6, DATEADD(DAY,5,@d)), DATEADD(HOUR,18, DATEADD(DAY,6,@d)),  NULL, NULL, N'Dự kiến', 520, 540, NULL),
('MAEU-2608S', 1, NULL, DATEADD(HOUR,7, DATEADD(DAY,6,@d)), DATEADD(HOUR,22, DATEADD(DAY,7,@d)),  NULL, NULL, N'Dự kiến', 600, 590, NULL),
('VIMC-2608A', 10, NULL, DATEADD(HOUR,5, DATEADD(DAY,7,@d)), DATEADD(HOUR,23, DATEADD(DAY,7,@d)), NULL, NULL, N'Dự kiến', 150, 140, N'Hàng tổng hợp'),
('MSCU-2609N', 9, NULL, DATEADD(HOUR,4, DATEADD(DAY,8,@d)), DATEADD(HOUR,20, DATEADD(DAY,9,@d)),  NULL, NULL, N'Dự kiến', 700, 720, N'Tàu mẹ 14.000 TEU - cần bến nước sâu');
GO

-- Container
DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);
INSERT INTO Container (SoCont, LoaiCont, TinhTrang, TrongLuong, HangHoa, MaKH, MaChuyenNhap, MaChuyenXuat, ViTriBai, TrangThai, NgayVaoBai, NgayRaBai, NhietDo) VALUES
-- trong bai (hang nhap tu cac chuyen dang lam hang)
('MSCU2201417', '40HC', 'Full', 24.50, N'Linh kiện điện tử',        5, 5, NULL, 'A-02-03-2', N'Trong bãi', DATEADD(HOUR,10, DATEADD(DAY,-1,@d)), NULL, NULL),
('CMDU5507821', '40GP', 'Full', 22.10, N'Máy móc thiết bị',         2, 5, NULL, 'A-02-04-1', N'Trong bãi', DATEADD(HOUR,11, DATEADD(DAY,-1,@d)), NULL, NULL),
('CMDU5508900', '20GP', 'Full', 18.30, N'Nguyên liệu nhựa',         1, 5, NULL, 'B-01-02-3', N'Trong bãi', DATEADD(HOUR,12, DATEADD(DAY,-1,@d)), NULL, NULL),
('MAEU3305891', '40HC', 'Full', 25.80, N'Vải cuộn',                 4, 7, NULL, 'A-03-01-1', N'Trong bãi', DATEADD(HOUR,15, @d), NULL, NULL),
('MAEU3306014', '20GP', 'Full', 17.60, N'Phụ liệu may mặc',         4, 7, NULL, 'B-01-05-2', N'Trong bãi', DATEADD(HOUR,16, @d), NULL, NULL),
('MSCU2299876', '20RF', 'Full', 19.20, N'Tôm đông lạnh xuất khẩu',  3, NULL, NULL, 'R-01-01-1', N'Trong bãi', DATEADD(DAY,-4,@d), NULL, -18.0),
('ONEY6612345', '40RF', 'Full', 26.40, N'Cá tra phi lê đông lạnh',  3, NULL, NULL, 'R-01-02-1', N'Trong bãi', DATEADD(DAY,-6,@d), NULL, -20.0),
('EGLV9901234', '40GP', 'Full', 21.75, N'Cà phê nhân',              6, 3, NULL, 'C-02-01-2', N'Trong bãi', DATEADD(DAY,-5,@d), NULL, NULL),
('EGLV9902678', '20GP', 'Full', 16.90, N'Hồ tiêu đen',              6, 3, NULL, 'C-02-02-1', N'Trong bãi', DATEADD(DAY,-5,@d), NULL, NULL),
('HLCU7701123', '40HC', 'Full', 23.30, N'Đồ gỗ nội thất',           2, NULL, NULL, 'C-01-03-3', N'Trong bãi', DATEADD(DAY,-21,@d), NULL, NULL),
('MAEU3300456', '20GP', 'Empty', 2.20, NULL,                        NULL, 1, NULL, 'E-01-01-1', N'Trong bãi', DATEADD(DAY,-11,@d), NULL, NULL),
('MSCU2205000', '40HC', 'Empty', 3.80, NULL,                        NULL, 2, NULL, 'E-01-02-2', N'Trong bãi', DATEADD(DAY,-8,@d), NULL, NULL),
-- cho xuat tau (da ha bai cho len tau)
('MSCU2210555', '40HC', 'Full', 27.10, N'Gạo thơm xuất khẩu',       6, NULL, 8, 'X-01-01-1', N'Chờ xuất tàu', DATEADD(DAY,-2,@d), NULL, NULL),
('MSCU2210777', '40HC', 'Full', 26.85, N'Gạo thơm xuất khẩu',       6, NULL, 8, 'X-01-02-1', N'Chờ xuất tàu', DATEADD(DAY,-2,@d), NULL, NULL),
('EGLV9905432', '20GP', 'Full', 19.40, N'Hàng may mặc',             4, NULL, 9, 'X-02-01-2', N'Chờ xuất tàu', DATEADD(DAY,-1,@d), NULL, NULL),
('MSCU2233001', '20RF', 'Full', 18.75, N'Nghêu đông lạnh',          3, NULL, 8, 'R-02-01-1', N'Chờ xuất tàu', DATEADD(DAY,-3,@d), NULL, -18.0),
('CMDU5511222', '45HC', 'Full', 28.20, N'Thiết bị gia dụng',        5, NULL, 9, 'X-02-03-1', N'Chờ xuất tàu', DATEADD(DAY,-1,@d), NULL, NULL),
-- da xuat tau (di theo chuyen da roi ben)
('MAEU3311999', '40GP', 'Full', 24.00, N'Cao su tự nhiên',          6, NULL, 1, NULL, N'Đã xuất tàu', DATEADD(DAY,-14,@d), DATEADD(DAY,-11,@d), NULL),
('MSCU2244888', '20GP', 'Full', 17.25, N'Hạt điều',                 6, NULL, 2, NULL, N'Đã xuất tàu', DATEADD(DAY,-10,@d), DATEADD(DAY,-8,@d), NULL),
('EGLV9908111', '40HC', 'Full', 25.10, N'Giày dép xuất khẩu',       4, NULL, 3, NULL, N'Đã xuất tàu', DATEADD(DAY,-7,@d), DATEADD(DAY,-5,@d), NULL),
('MAEU3312777', '20GP', 'Full', 18.00, N'Gốm sứ mỹ nghệ',           1, NULL, 1, NULL, N'Đã xuất tàu', DATEADD(DAY,-13,@d), DATEADD(DAY,-11,@d), NULL),
-- da giao khach (gate out)
('MSCU2266333', '40HC', 'Full', 23.90, N'Nguyên liệu sản xuất',     5, 2, NULL, NULL, N'Đã giao khách', DATEADD(DAY,-8,@d), DATEADD(DAY,-6,@d), NULL),
('MAEU3320123', '20GP', 'Full', 16.55, N'Hóa chất công nghiệp',     2, 1, NULL, NULL, N'Đã giao khách', DATEADD(DAY,-11,@d), DATEADD(DAY,-9,@d), NULL),
('EGLV9910666', '40GP', 'Full', 22.35, N'Thép cuộn',                2, 3, NULL, NULL, N'Đã giao khách', DATEADD(DAY,-5,@d), DATEADD(DAY,-3,@d), NULL),
('ONEY6620999', '20GP', 'Full', 15.80, N'Bột mì',                   1, 2, NULL, NULL, N'Đã giao khách', DATEADD(DAY,-8,@d), DATEADD(DAY,-7,@d), NULL),
-- them hang trong bai
('HLCU7705888', '20GP', 'Full', 17.10, N'Săm lốp ô tô',             2, NULL, NULL, 'B-02-01-1', N'Trong bãi', DATEADD(DAY,-15,@d), NULL, NULL),
('ONEY6633444', '40HC', 'Full', 24.65, N'Màn hình LCD',             5, NULL, 11, 'X-03-01-1', N'Chờ xuất tàu', DATEADD(DAY,-1,@d), NULL, NULL),
('VNLU4400111', '20GP', 'Full', 18.90, N'Phân bón NPK',             6, 6, NULL, 'B-03-02-1', N'Trong bãi', DATEADD(HOUR,9,@d), NULL, NULL),
('VNLU4400222', '20GP', 'Full', 19.05, N'Phân bón NPK',             6, 6, NULL, 'B-03-02-2', N'Trong bãi', DATEADD(HOUR,9,@d), NULL, NULL),
('MSCU2277456', '40RF', 'Full', 25.95, N'Xoài cát đông lạnh',       3, NULL, 8, 'R-02-02-1', N'Chờ xuất tàu', DATEADD(DAY,-2,@d), NULL, -22.0);
GO

-- Dich vu
INSERT INTO DichVu (TenDV, NhomDV, DonViTinh, DonGia, MoTa) VALUES
(N'Bốc xếp container 20''',            N'Bốc xếp',      N'Cont',       450000,  N'Nâng/hạ container 20 feet giữa tàu - bãi hoặc bãi - xe'),
(N'Bốc xếp container 40''',            N'Bốc xếp',      N'Cont',       680000,  N'Nâng/hạ container 40 feet giữa tàu - bãi hoặc bãi - xe'),
(N'Lưu bãi container 20''',            N'Lưu bãi',      N'Cont/ngày',  60000,   N'Phí lưu bãi container 20 feet, miễn phí 5 ngày đầu'),
(N'Lưu bãi container 40''',            N'Lưu bãi',      N'Cont/ngày',  95000,   N'Phí lưu bãi container 40 feet, miễn phí 5 ngày đầu'),
(N'Đóng / Rút hàng container',         N'Kho bãi',      N'Cont',       900000,  N'Đóng hàng vào hoặc rút hàng khỏi container tại kho CFS'),
(N'Vệ sinh container',                 N'Kho bãi',      N'Cont',       150000,  N'Vệ sinh, quét dọn, khử mùi container sau khi rút hàng'),
(N'Cân container',                     N'Khác',         N'Lượt',       120000,  N'Cân xác định VGM theo quy định SOLAS'),
(N'Cắm điện container lạnh',           N'Lưu bãi',      N'Cont/ngày',  250000,  N'Cấp điện, giám sát nhiệt độ container lạnh 24/7'),
(N'Vận chuyển container nội thành',    N'Vận chuyển',   N'Chuyến',     1200000, N'Vận chuyển container trong bán kính 30km'),
(N'Vận chuyển container liên tỉnh',    N'Vận chuyển',   N'Chuyến',     2800000, N'Vận chuyển container tuyến TP.HCM - các tỉnh lân cận'),
(N'Xếp dỡ hàng rời',                   N'Bốc xếp',      N'Tấn',        180000,  N'Xếp dỡ hàng rời: nông sản, phân bón, thức ăn chăn nuôi...'),
(N'Giám định - kiểm đếm hàng hóa',     N'Khác',         N'Lô',         500000,  N'Dịch vụ giám định số lượng, tình trạng hàng hóa');
GO

-- Dat dich vu (booking)
DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);
INSERT INTO DatDichVu (MaSoBooking, MaKH, NgayTao, NgayCanThucHien, TrangThai, GhiChu, TongTien) VALUES
('BK260001', 1, DATEADD(DAY,-25,@d), DATEADD(DAY,-20,@d), N'Hoàn thành',     N'Bốc xếp lô hàng gốm sứ',                6000000),
('BK260002', 2, DATEADD(DAY,-15,@d), DATEADD(DAY,-12,@d), N'Hoàn thành',     N'Kèm vận chuyển về kho Bình Thạnh',      7840000),
('BK260003', 3, DATEADD(DAY,-3,@d),  DATEADD(DAY,2,@d),   N'Đang thực hiện', N'Hàng lạnh -18°C, giám sát liên tục',    3300000),
('BK260004', 1, DATEADD(DAY,-2,@d),  DATEADD(DAY,1,@d),   N'Đã duyệt',       N'Rút hàng tại kho CFS số 2',             4500000),
('BK260005', 4, DATEADD(DAY,-1,@d),  DATEADD(DAY,3,@d),   N'Chờ duyệt',      N'Cần cân VGM trước khi hạ bãi',          6840000),
('BK260006', 2, @d,                  DATEADD(DAY,4,@d),   N'Chờ duyệt',      N'Lô phân bón 500 tấn từ tàu VIMC PIONEER', 90000000),
('BK260007', 5, DATEADD(DAY,-6,@d),  DATEADD(DAY,-4,@d),  N'Đã hủy',         N'Khách hàng đổi kế hoạch vận chuyển',    1200000),
('BK260008', 1, DATEADD(DAY,-8,@d),  DATEADD(DAY,-6,@d),  N'Hoàn thành',     N'Bốc xếp 15 cont 40'' hàng xuất',        10200000);

INSERT INTO DatDichVuChiTiet (MaBooking, MaDV, SoLuong, DonGia, ThanhTien, GhiChu) VALUES
(1, 1, 10, 450000, 4500000, N'10 cont 20'' hàng gốm sứ'),
(1, 6, 10, 150000, 1500000, NULL),
(2, 2, 8, 680000, 5440000, N'8 cont 40HC'),
(2, 9, 2, 1200000, 2400000, N'2 chuyến về kho Bình Thạnh'),
(3, 8, 6, 250000, 1500000, N'MSCU2299876, ONEY6612345...'),
(3, 3, 30, 60000, 1800000, N'Lưu bãi 6 cont x 5 ngày'),
(4, 5, 5, 900000, 4500000, N'Rút 5 cont hàng vải'),
(5, 1, 12, 450000, 5400000, NULL),
(5, 7, 12, 120000, 1440000, N'Cân VGM 12 cont'),
(6, 11, 500, 180000, 90000000, N'500 tấn phân bón NPK'),
(7, 9, 1, 1200000, 1200000, NULL),
(8, 2, 15, 680000, 10200000, N'15 cont 40'' giày dép');
GO

-- Tin tuc
INSERT INTO TinTuc (TieuDe, TomTat, NoiDung, AnhMau, NgayDang, LuotXem, NoiBat) VALUES
(N'SmartPort đưa vào vận hành 2 cẩu STS thế hệ mới tại bến B5',
 N'Hai cẩu bờ STS hiện đại với tầm với 22 hàng container vừa được lắp đặt hoàn tất, nâng năng suất giải phóng tàu của SmartPort lên hơn 30%.',
 N'<p>Sáng nay, Cảng Quốc tế SmartPort đã chính thức đưa vào vận hành 2 cẩu bờ STS (Ship-To-Shore) thế hệ mới tại bến B5. Đây là loại cẩu có tầm với 22 hàng container, sức nâng 65 tấn, có khả năng phục vụ các tàu mẹ kích cỡ trên 14.000 TEU.</p><p>Với tổng mức đầu tư gần 800 tỷ đồng, hai thiết bị mới giúp năng suất giải phóng tàu tăng hơn 30%, giảm đáng kể thời gian tàu nằm bến. Đại diện cảng cho biết đây là bước đi quan trọng trong chiến lược đưa SmartPort trở thành cảng trung chuyển hàng đầu khu vực.</p><p>Trong quý tới, cảng sẽ tiếp tục lắp đặt thêm 4 cẩu RTG chạy điện tại bãi container, hướng tới mục tiêu "cảng xanh" giảm 40% phát thải CO2 vào năm 2028.</p>',
 'g1', DATEADD(DAY,-2,GETDATE()), 358, 1),
(N'Ứng dụng AI vào điều độ tàu: giảm 25% thời gian chờ cầu bến',
 N'Hệ thống trí tuệ nhân tạo của SmartPort tự động gợi ý phân bổ bến tối ưu dựa trên chiều dài tàu, mớn nước và lịch khai thác.',
 N'<p>SmartPort vừa triển khai mô-đun AI hỗ trợ điều độ tàu trên hệ thống quản lý cảng. Thuật toán phân tích đồng thời các yếu tố: chiều dài tàu (LOA), mớn nước, độ sâu trước bến, số cẩu khả dụng và lịch cập cảng để đề xuất phương án phân bổ bến tối ưu nhất.</p><p>Sau 3 tháng chạy thử nghiệm, thời gian tàu chờ cầu bến trung bình giảm 25%, tỷ lệ khai thác cầu bến tăng từ 68% lên 81%. Bên cạnh đó, chatbot PortBot cũng được ra mắt trên website giúp khách hàng tra cứu container, lịch tàu và biểu phí dịch vụ 24/7.</p><p>Đây là một phần trong lộ trình chuyển đổi số toàn diện của cảng giai đoạn 2025-2030.</p>',
 'g2', DATEADD(DAY,-5,GETDATE()), 542, 1),
(N'Sản lượng container 6 tháng đầu năm tăng trưởng 9,2%',
 N'Tổng sản lượng thông qua SmartPort đạt hơn 300.000 TEU trong nửa đầu năm, tăng 9,2% so với cùng kỳ.',
 N'<p>Theo báo cáo khai thác, sản lượng container thông qua Cảng Quốc tế SmartPort trong 6 tháng đầu năm đạt 300.500 TEU, tăng 9,2% so với cùng kỳ năm trước. Hàng xuất chủ lực gồm: dệt may, giày dép, nông thủy sản; hàng nhập chủ yếu là nguyên phụ liệu sản xuất, máy móc thiết bị và linh kiện điện tử.</p><p>Doanh thu khai thác cảng ước đạt 560 tỷ đồng, hoàn thành 52% kế hoạch năm. Số chuyến tàu cập cảng đạt 214 chuyến, trong đó có 38 chuyến tàu mẹ tuyến đi thẳng châu Âu và Bắc Mỹ.</p>',
 'g3', DATEADD(DAY,-9,GETDATE()), 289, 1),
(N'Ra mắt cổng dịch vụ trực tuyến ePort: đặt dịch vụ cảng chỉ với 3 bước',
 N'Khách hàng có thể đăng ký bốc xếp, lưu bãi, vận chuyển... ngay trên website và theo dõi trạng thái xử lý theo thời gian thực.',
 N'<p>Cổng dịch vụ trực tuyến ePort của SmartPort chính thức hoạt động từ hôm nay. Doanh nghiệp chỉ cần đăng ký tài khoản, chọn dịch vụ và số lượng, hệ thống sẽ tự động tính phí và gửi yêu cầu tới bộ phận khai thác.</p><p>Toàn bộ trạng thái xử lý (chờ duyệt, đã duyệt, đang thực hiện, hoàn thành) đều được cập nhật trực tuyến, giúp khách hàng chủ động kế hoạch nhận/giao hàng. Trong tháng đầu ra mắt, cảng miễn phí dịch vụ cân VGM cho 100 booking đầu tiên đặt qua ePort.</p>',
 'g4', DATEADD(DAY,-14,GETDATE()), 471, 0),
(N'Diễn tập ứng phó sự cố tràn dầu và PCCC quy mô lớn tại cảng',
 N'Hơn 200 cán bộ nhân viên cùng các lực lượng chức năng tham gia diễn tập tình huống khẩn cấp tại khu bến B3-B4.',
 N'<p>Nhằm nâng cao năng lực ứng phó sự cố, SmartPort phối hợp với Cảnh sát PCCC và Cảng vụ Hàng hải tổ chức diễn tập quy mô lớn với tình huống giả định: tàu chở hàng rời va chạm gây tràn dầu kèm cháy tại khu vực bến B4.</p><p>Buổi diễn tập huy động 2 tàu lai, 4 xe chữa cháy, hệ thống phao quây thấm dầu và hơn 200 người tham gia. Kết quả được đánh giá đạt yêu cầu về thời gian phản ứng và quy trình phối hợp giữa các đơn vị.</p>',
 'g5', DATEADD(DAY,-20,GETDATE()), 174, 0),
(N'SmartPort ký kết hợp tác chiến lược với 3 hãng tàu quốc tế',
 N'Các tuyến dịch vụ mới kết nối trực tiếp với Singapore, Thượng Hải và Busan sẽ được mở trong quý tới.',
 N'<p>Trong khuôn khổ hội nghị khách hàng thường niên, SmartPort đã ký biên bản ghi nhớ hợp tác với 3 hãng tàu lớn về việc mở thêm tuyến dịch vụ định tuyến cố định qua cảng. Theo đó, từ quý sau sẽ có thêm 3 tuyến feeder kết nối trực tiếp Singapore, Thượng Hải và Busan với tần suất 2 chuyến/tuần mỗi tuyến.</p><p>Việc mở tuyến mới dự kiến bổ sung khoảng 8.000 TEU/tháng, đồng thời rút ngắn 2-3 ngày thời gian vận chuyển hàng xuất khẩu của doanh nghiệp trong vùng.</p>',
 'g6', DATEADD(DAY,-27,GETDATE()), 226, 0);
GO

-- Lien he
INSERT INTO LienHe (HoTen, Email, SoDienThoai, ChuDe, NoiDung, NgayGui, DaXuLy) VALUES
(N'Trương Văn Nam', 'nam.truong@gmail.com', '0909111222', N'Hỏi về thủ tục lấy hàng', N'Công ty tôi có 2 container hàng nhập trên tàu CMA CGM MEKONG, xin hỏi thủ tục và chứng từ cần thiết để lấy hàng ra khỏi cảng?', DATEADD(DAY,-1,GETDATE()), 0),
(N'Lý Thu Trang', 'trang.ly@vinafood.vn', '0918333444', N'Báo giá lưu bãi dài hạn', N'Chúng tôi cần lưu bãi khoảng 50 container 40 feet trong 2 tháng, mong cảng gửi báo giá ưu đãi cho số lượng lớn.', DATEADD(DAY,-3,GETDATE()), 1),
(N'Hoàng Minh Đức', 'duc.hoang@outlook.com', '0977888999', N'Hợp tác vận chuyển', N'Công ty vận tải chúng tôi muốn trở thành đối tác vận chuyển container đường bộ của cảng, xin thông tin đầu mối phụ trách.', DATEADD(DAY,-5,GETDATE()), 0);
GO

-- San luong thang (12/2025 -> 6/2026) phuc vu bieu do + AI du bao
INSERT INTO SanLuongThang (Nam, Thang, TEU, DoanhThu) VALUES
(2025, 1,  38120, 70522000000),
(2025, 2,  39450, 72982500000),
(2025, 3,  41200, 76220000000),
(2025, 4,  40100, 74185000000),
(2025, 5,  42800, 79180000000),
(2025, 6,  43500, 80475000000),
(2025, 7,  44120, 81622000000),
(2025, 8,  45900, 84915000000),
(2025, 9,  44700, 82695000000),
(2025, 10, 46800, 86580000000),
(2025, 11, 48200, 89170000000),
(2025, 12, 50100, 92685000000),
(2026, 1,  47200, 87320000000),
(2026, 2,  44800, 82880000000),
(2026, 3,  49600, 91760000000),
(2026, 4,  51200, 94720000000),
(2026, 5,  52800, 97680000000),
(2026, 6,  54100, 100085000000);
GO

-- Thu vien anh (quan ly tu Admin > Thu vien anh)
INSERT INTO CauHinhAnh (Khoa, Url, MoTa, ThuTu) VALUES
('hero',   'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1800&auto=format&fit=crop', N'Nền hero + slide 1 (tàu container từ trên cao)', 1),
('slide2', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1800&auto=format&fit=crop', N'Hero slide 2 (cẩu STS làm hàng)', 2),
('slide3', 'https://images.unsplash.com/photo-1502920514313-52581002a659?q=80&w=1800&auto=format&fit=crop', N'Hero slide 3 (tàu trên biển)', 3),
('slide4', 'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1800&auto=format&fit=crop', N'Hero slide 4 (cầu bến hoàng hôn)', 4),
('head',   'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1600&auto=format&fit=crop', N'Banner các trang con', 5),
('about',  'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1200&auto=format&fit=crop', N'Trang giới thiệu', 6),
('g1', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 1 (cẩu bờ)', 11),
('g2', 'https://images.unsplash.com/photo-1516216628859-9bccecab13ca?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 2 (cảng đêm)', 12),
('g3', 'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 3 (aerial)', 13),
('g4', 'https://images.unsplash.com/photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 4 (bãi cont)', 14),
('g5', 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 5 (tàu biển)', 15),
('g6', 'https://images.unsplash.com/photo-1519452635265-7b1fbfd1e4e0?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 6 (boong tàu)', 16),
('gal1', 'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1200&auto=format&fit=crop', N'Thư viện 1 — tàu mẹ cập cảng', 21),
('gal2', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop', N'Thư viện 2 — cẩu STS', 22),
('gal3', 'https://images.unsplash.com/photo-1524055988636-436cfa46e59e?q=80&w=1200&auto=format&fit=crop', N'Thư viện 3 — bãi container', 23),
('gal4', 'https://images.unsplash.com/photo-1553413077-190dd305871c?q=80&w=1200&auto=format&fit=crop', N'Thư viện 4 — kho CFS', 24),
('gal5', 'https://images.unsplash.com/photo-1519003722824-194d4455a60c?q=80&w=1200&auto=format&fit=crop', N'Thư viện 5 — đội xe', 25),
('gal6', 'https://images.unsplash.com/photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop', N'Thư viện 6 — bãi container AI', 26),
('biz1', 'https://images.unsplash.com/photo-1600880292203-757bb62b4baf?q=80&w=800&auto=format&fit=crop', N'Doanh nghiệp 1 (họp đối tác)', 31),
('biz2', 'https://images.unsplash.com/photo-1521791136064-7986c2920216?q=80&w=800&auto=format&fit=crop', N'Doanh nghiệp 2 (bắt tay hợp tác)', 32),
('cta',  'https://images.unsplash.com/photo-1502920514313-52581002a659?q=80&w=1600&auto=format&fit=crop', N'Nền băng CTA cuối trang', 33);
GO

PRINT N'=== Import du lieu SmartPortDB thanh cong! ===';
SELECT 'KhachHang' AS Bang, COUNT(*) AS SoDong FROM KhachHang
UNION ALL SELECT 'TaiKhoan', COUNT(*) FROM TaiKhoan
UNION ALL SELECT 'HangTau', COUNT(*) FROM HangTau
UNION ALL SELECT 'Tau', COUNT(*) FROM Tau
UNION ALL SELECT 'Ben', COUNT(*) FROM Ben
UNION ALL SELECT 'ChuyenTau', COUNT(*) FROM ChuyenTau
UNION ALL SELECT 'Container', COUNT(*) FROM Container
UNION ALL SELECT 'DichVu', COUNT(*) FROM DichVu
UNION ALL SELECT 'DatDichVu', COUNT(*) FROM DatDichVu
UNION ALL SELECT 'DatDichVuChiTiet', COUNT(*) FROM DatDichVuChiTiet
UNION ALL SELECT 'TinTuc', COUNT(*) FROM TinTuc
UNION ALL SELECT 'LienHe', COUNT(*) FROM LienHe
UNION ALL SELECT 'SanLuongThang', COUNT(*) FROM SanLuongThang;
GO
