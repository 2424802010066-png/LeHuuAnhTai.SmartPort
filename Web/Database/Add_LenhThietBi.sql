-- Lenh cong viec thiet bi bai (dich vu bai). Chay lai an toan.
IF OBJECT_ID('dbo.LenhThietBi', 'U') IS NULL
CREATE TABLE dbo.LenhThietBi (
    MaLenhTB   INT IDENTITY(1,1) PRIMARY KEY,
    SoCont     NVARCHAR(11) NOT NULL,
    LoaiViec   NVARCHAR(20) NOT NULL,
    TuViTri    NVARCHAR(12) NULL,
    DenViTri   NVARCHAR(12) NULL,
    UuTien     INT NOT NULL DEFAULT 1,
    MaThietBi  INT NULL,
    TrangThai  NVARCHAR(20) NOT NULL,
    TaoLuc     DATETIME2 NOT NULL,
    NhanLuc    DATETIME2 NULL,
    XongLuc    DATETIME2 NULL,
    QuangDuongRongM FLOAT NULL
);
