-- ============================================================
-- MODULE AN NINH AI + KIEM DEM — script idempotent
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SuCoAnNinh')
BEGIN
    CREATE TABLE dbo.SuCoAnNinh (
        MaSC        INT IDENTITY(1,1) PRIMARY KEY,
        Loai        NVARCHAR(30)  NOT NULL,      -- Lua / NguoiNga / VatTinh / OCRSai / Khac
        MucDo       NVARCHAR(20)  NOT NULL,      -- Nguy hiem / Chu y
        ViTri       NVARCHAR(50)  NOT NULL,
        MoTa        NVARCHAR(300) NULL,
        AnhBase64   NVARCHAR(MAX) NULL,          -- anh bang chung (jpeg thu nho)
        VideoPath   NVARCHAR(300) NULL,          -- duong dan video webm da luu
        TrangThai   NVARCHAR(20)  NOT NULL DEFAULT N'Mới',
        ThoiGian    DATETIME NOT NULL DEFAULT GETDATE(),
        NguoiXuLy   NVARCHAR(100) NULL,
        ThoiGianXuLy DATETIME NULL
    );
    CREATE INDEX IX_SuCo_ThoiGian ON dbo.SuCoAnNinh(ThoiGian DESC);
    PRINT N'Da tao bang SuCoAnNinh.';
END
ELSE PRINT N'SuCoAnNinh da ton tai.';

IF NOT EXISTS (SELECT 1 FROM dbo.SuCoAnNinh)
BEGIN
    INSERT INTO dbo.SuCoAnNinh (Loai, MucDo, ViTri, MoTa, TrangThai, ThoiGian, NguoiXuLy, ThoiGianXuLy) VALUES
    (N'Lua',     N'Nguy hiểm', N'Kho CFS',       N'Phát hiện vùng lửa/nhiệt bất thường gần cửa kho', N'Đã xử lý', DATEADD(DAY,-1,GETDATE()), N'Đội an ninh ca 2', DATEADD(DAY,-1,DATEADD(MINUTE,12,GETDATE()))),
    (N'NguoiNga',N'Nguy hiểm', N'Bãi container', N'Công nhân nằm bất động trong khu xếp dỡ',         N'Đã xử lý', DATEADD(HOUR,-20,GETDATE()), N'Y tế + an ninh', DATEADD(HOUR,-19,GETDATE())),
    (N'VatTinh', N'Chú ý',     N'Cổng chính',    N'Xe tải đứng im bất thường 5 phút giữa làn lưu thông', N'Mới', DATEADD(HOUR,-2,GETDATE()), NULL, NULL);
    PRINT N'Da seed 3 su co mau.';
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KiemDem')
BEGIN
    CREATE TABLE dbo.KiemDem (
        MaKD       INT IDENTITY(1,1) PRIMARY KEY,
        TenLo      NVARCHAR(100) NOT NULL,
        LopDem     NVARCHAR(30)  NULL,           -- lop YOLO dung de dem
        DuKien     INT NULL,
        DemDuoc    INT NULL,
        SoGiong    INT NULL,                     -- phan loai PHAI (giong mau)
        SoKhac     INT NULL,                     -- phan loai TRAI (khac mau)
        ChieuCao   NVARCHAR(30) NULL,
        TrongLuong NVARCHAR(30) NULL,
        GhiChu     NVARCHAR(200) NULL,
        ThoiGian   DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT N'Da tao bang KiemDem.';
END
ELSE PRINT N'KiemDem da ton tai.';

IF NOT EXISTS (SELECT 1 FROM dbo.KiemDem)
BEGIN
    INSERT INTO dbo.KiemDem (TenLo, LopDem, DuKien, DemDuoc, SoGiong, SoKhac, ChieuCao, TrongLuong, GhiChu, ThoiGian) VALUES
    (N'Lô nước suối LAVI 500ml', N'bottle', 24, 24, 22, 2, N'21 cm', N'0,5 kg/chai', N'2 chai khác mẫu chuyển làn trái', DATEADD(DAY,-1,GETDATE())),
    (N'Lô vali hành lý ký gửi',  N'suitcase', 12, 11, 11, 0, N'55 cm', N'8-12 kg', N'Thiếu 1 kiện so với khai báo', DATEADD(HOUR,-5,GETDATE()));
    PRINT N'Da seed 2 phien kiem dem.';
END

SELECT (SELECT COUNT(*) FROM dbo.SuCoAnNinh) AS SuCo, (SELECT COUNT(*) FROM dbo.KiemDem) AS KiemDem;
