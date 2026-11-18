-- Khai bao hang nguy hiem IMDG (dich vu bai). Chay lai an toan. Dong mau gan vao container dang co vi tri hop le.
IF OBJECT_ID('dbo.KhaiBaoNguyHiem', 'U') IS NULL
CREATE TABLE dbo.KhaiBaoNguyHiem (
    MaKhaiBao   INT IDENTITY(1,1) PRIMARY KEY,
    SoCont      NVARCHAR(11) NOT NULL,
    SoUN        NVARCHAR(4) NULL,
    TenHang     NVARCHAR(200) NOT NULL,
    LopIMDG     NVARCHAR(4) NOT NULL,
    NhomDongGoi NVARCHAR(3) NULL,
    ONhiemBien  BIT NOT NULL DEFAULT 0,
    NguoiKhai   NVARCHAR(100) NULL,
    KhaiLuc     DATETIME2 NOT NULL,
    ConHieuLuc  BIT NOT NULL DEFAULT 1
);
