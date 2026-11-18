-- Ke hoach xep do tau (dich vu tau-ben). Chay lai an toan.
IF OBJECT_ID('dbo.KeHoachXepDo', 'U') IS NULL
CREATE TABLE dbo.KeHoachXepDo (
    MaKeHoach   INT IDENTITY(1,1) PRIMARY KEY,
    MaChuyen    INT NOT NULL,
    LapLuc      DATETIME2 NOT NULL,
    NguoiLap    NVARCHAR(100) NULL,
    SoMove      INT NOT NULL,
    GioDuKien   FLOAT NOT NULL,
    NoiDungJson NVARCHAR(MAX) NOT NULL
);
