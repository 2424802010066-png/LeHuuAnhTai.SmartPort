-- ============================================================
-- XU LY SU CO (UC22 -> UC28) — bang SuCo CHUNG cho moi vai tro — script idempotent, CHI DDL
--   UC22 Bao cao su co (moi nguoi dung) · UC23 An ninh, an toan · UC24 Hang hoa, thiet bi (tam giu container)
--   UC25 Giao dich thanh toan loi · UC26 Phan anh, khieu nai · UC27 Su co he thong · UC28 Thong ke su co
-- Quy tac nghiep vu:
--   QT14  su co NguyHiem phai duoc tiep nhan trong 5 phut, he thong bao dong toan cang ngay khi ghi
--   QT15  container dang TamGiuContainer = 1 (su co chua Dong / BaoGia) khong duoc giao ra cong
--   QT16  giao dich da tru tien ma lenh chua hieu luc phai doi soat trong ngay, khong kich hoat duoc thi hoan tien
--   QT17  su co KHONG xoa duoc; chi dong khi da ghi ket qua (CHECK CK_SuCo_KetQua). Phan anh cua khach tra loi trong 24 gio
-- Chay lai bao nhieu lan cung duoc (khong xoa, khong ghi du lieu). Du lieu mau: Seed_SuCo_Mau.sql (rieng).
-- Che do microservices: dung_csdl_dich_vu.py tu chay script nay cho CSDL SmartPortMS_AnNinh (bang SuCo thuoc dich vu an-ninh);
-- khoa ngoai chi tao o CSDL gop (khoa ngoai cheo dich vu khong ton tai trong microservices).
-- ============================================================

IF OBJECT_ID(N'dbo.SuCo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SuCo (
        MaSuCo           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SuCo PRIMARY KEY,
        Loai             NVARCHAR(30)   NOT NULL,     -- AnNinhAnToan / HangHoaThietBi / ThanhToan / DichVu / HeThong
        MucDo            NVARCHAR(20)   NOT NULL,     -- Thap / TrungBinh / NguyHiem
        TieuDe           NVARCHAR(150)  NOT NULL,
        MoTa             NVARCHAR(1000) NOT NULL,
        ViTri            NVARCHAR(100)  NULL,
        SoContainer      NVARCHAR(15)   NULL,
        MaLenh           INT            NULL,         -- LenhGiaoNhan.MaLenh (lenh giao nhan lien quan)
        AnhBase64        NVARCHAR(MAX)  NULL,         -- anh hien truong data:image/...;base64 (<= ~2 MB)
        MaTKBao          INT            NOT NULL,     -- nguoi bao (TaiKhoan)
        MaTKXuLy         INT            NULL,         -- nguoi tiep nhan / xu ly (TaiKhoan)
        TrangThai        NVARCHAR(20)   NOT NULL CONSTRAINT DF_SuCo_TrangThai DEFAULT N'Moi',
        TamGiuContainer  BIT            NOT NULL CONSTRAINT DF_SuCo_TamGiu DEFAULT 0,
        KetQua           NVARCHAR(1000) NULL,
        ThoiDiemBao      DATETIME2      NOT NULL CONSTRAINT DF_SuCo_ThoiDiemBao DEFAULT SYSDATETIME(),
        ThoiDiemTiepNhan DATETIME2      NULL,
        ThoiDiemDong     DATETIME2      NULL,
        MaSCAnNinh       INT            NULL,         -- SuCoAnNinh.MaSC khi chuyen tu canh bao camera AI
        CONSTRAINT CK_SuCo_Loai CHECK (Loai IN (N'AnNinhAnToan', N'HangHoaThietBi', N'ThanhToan', N'DichVu', N'HeThong')),
        CONSTRAINT CK_SuCo_MucDo CHECK (MucDo IN (N'Thap', N'TrungBinh', N'NguyHiem')),
        CONSTRAINT CK_SuCo_TrangThai CHECK (TrangThai IN (N'Moi', N'DangXuLy', N'DaXuLy', N'Dong', N'BaoGia')),
        CONSTRAINT CK_SuCo_ThoiDiemDong CHECK (ThoiDiemDong IS NULL OR ThoiDiemDong >= ThoiDiemBao),
        CONSTRAINT CK_SuCo_ThoiDiemTiepNhan CHECK (ThoiDiemTiepNhan IS NULL OR ThoiDiemTiepNhan >= ThoiDiemBao),
        -- QT17: da xu ly / da dong thi BAT BUOC co ket qua
        CONSTRAINT CK_SuCo_KetQua CHECK (TrangThai NOT IN (N'DaXuLy', N'Dong') OR KetQua IS NOT NULL)
    );
    PRINT N'Da tao bang SuCo.';
END
ELSE PRINT N'SuCo da ton tai.';
GO

-- Chi muc: danh sach loc theo trang thai + loai; QT15 tim nhanh theo so container; "phan anh cua toi"; thong ke theo ngay
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuCo_TrangThai_Loai' AND object_id = OBJECT_ID(N'dbo.SuCo'))
    CREATE INDEX IX_SuCo_TrangThai_Loai ON dbo.SuCo (TrangThai, Loai) INCLUDE (MucDo, ThoiDiemBao);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuCo_SoContainer' AND object_id = OBJECT_ID(N'dbo.SuCo'))
    CREATE INDEX IX_SuCo_SoContainer ON dbo.SuCo (SoContainer) INCLUDE (TamGiuContainer, TrangThai);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuCo_MaTKBao' AND object_id = OBJECT_ID(N'dbo.SuCo'))
    CREATE INDEX IX_SuCo_MaTKBao ON dbo.SuCo (MaTKBao, ThoiDiemBao DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuCo_ThoiDiemBao' AND object_id = OBJECT_ID(N'dbo.SuCo'))
    CREATE INDEX IX_SuCo_ThoiDiemBao ON dbo.SuCo (ThoiDiemBao DESC);
GO

-- Khoa ngoai: chi o CSDL gop (SmartPortDB). CSDL microservices SmartPortMS_* khong co khoa ngoai cheo dich vu.
IF DB_NAME() NOT LIKE N'SmartPortMS[_]%'
BEGIN
    IF OBJECT_ID(N'dbo.TaiKhoan', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.FK_SuCo_TaiKhoan_Bao', N'F') IS NULL
        ALTER TABLE dbo.SuCo ADD CONSTRAINT FK_SuCo_TaiKhoan_Bao FOREIGN KEY (MaTKBao) REFERENCES dbo.TaiKhoan (MaTK);
    IF OBJECT_ID(N'dbo.TaiKhoan', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.FK_SuCo_TaiKhoan_XuLy', N'F') IS NULL
        ALTER TABLE dbo.SuCo ADD CONSTRAINT FK_SuCo_TaiKhoan_XuLy FOREIGN KEY (MaTKXuLy) REFERENCES dbo.TaiKhoan (MaTK);
    IF OBJECT_ID(N'dbo.LenhGiaoNhan', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.FK_SuCo_LenhGiaoNhan', N'F') IS NULL
        ALTER TABLE dbo.SuCo ADD CONSTRAINT FK_SuCo_LenhGiaoNhan FOREIGN KEY (MaLenh) REFERENCES dbo.LenhGiaoNhan (MaLenh);
    IF OBJECT_ID(N'dbo.SuCoAnNinh', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.FK_SuCo_SuCoAnNinh', N'F') IS NULL
        ALTER TABLE dbo.SuCo ADD CONSTRAINT FK_SuCo_SuCoAnNinh FOREIGN KEY (MaSCAnNinh) REFERENCES dbo.SuCoAnNinh (MaSC);
END
GO

SELECT COUNT(*) AS SoSuCo FROM dbo.SuCo;
