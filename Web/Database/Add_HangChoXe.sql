-- Hang cho ao ngoai cong (dich vu lich-hen). Chay lai an toan.
IF OBJECT_ID('dbo.HangChoXe', 'U') IS NULL
CREATE TABLE dbo.HangChoXe (
    MaHangCho   INT IDENTITY(1,1) PRIMARY KEY,
    MaLenh      INT NULL,
    SoLenh      NVARCHAR(20) NULL,
    BienSo      NVARCHAR(15) NOT NULL,
    SoCont      NVARCHAR(11) NULL,
    LoaiLenh    NVARCHAR(20) NULL,
    HenTuLuc    DATETIME2 NULL,
    HenDenLuc   DATETIME2 NULL,
    VaoHangLuc  DATETIME2 NOT NULL,
    GoiLuc      DATETIME2 NULL,
    VaoCongLuc  DATETIME2 NULL,
    TrangThai   NVARCHAR(20) NOT NULL,
    CamKetPhut  INT NOT NULL DEFAULT 0,
    SoPhutCho   INT NULL,
    ChenNgang   BIT NOT NULL DEFAULT 0,
    TienBoiHoan DECIMAL(18,2) NOT NULL DEFAULT 0,
    MaTheoDoi   NVARCHAR(32) NOT NULL,
    LaDienTap   BIT NOT NULL DEFAULT 0
);
