-- Tham so hang cho ao (dich vu lich-hen). Chay lai an toan.
IF OBJECT_ID('dbo.CauHinhHangCho', 'U') IS NULL
CREATE TABLE dbo.CauHinhHangCho (
    MaCauHinh   INT IDENTITY(1,1) PRIMARY KEY,
    SoLanMo     INT NOT NULL,
    PhutMoiXe   INT NOT NULL,
    PhutAnToan  INT NOT NULL,
    TienBoiHoan DECIMAL(18,2) NOT NULL
);
