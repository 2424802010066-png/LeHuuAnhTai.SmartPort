-- Thiet bi nang ha trong bai (dich vu bai). Chay lai an toan.
IF OBJECT_ID('dbo.ThietBiBai', 'U') IS NULL
CREATE TABLE dbo.ThietBiBai (
    MaThietBi  INT IDENTITY(1,1) PRIMARY KEY,
    SoHieu     NVARCHAR(20) NOT NULL,
    Loai       NVARCHAR(5) NOT NULL,
    KhuHienTai NVARCHAR(2) NULL,
    BayHienTai INT NOT NULL DEFAULT 1,
    TrangThai  NVARCHAR(20) NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM dbo.ThietBiBai)
INSERT dbo.ThietBiBai (SoHieu, Loai, KhuHienTai, BayHienTai, TrangThai) VALUES
 (N'RTG-01','RTG','A',1,N'Sẵn sàng'), (N'RTG-02','RTG','B',3,N'Sẵn sàng'), (N'RTG-03','RTG','C',2,N'Sẵn sàng'),
 (N'RTG-04','RTG','X',4,N'Sẵn sàng'), (N'RS-01','RS','R',1,N'Sẵn sàng'), (N'RS-02','RS','X',1,N'Sẵn sàng'),
 (N'FL-01','FL','E',1,N'Sẵn sàng'), (N'FL-02','FL','E',2,N'Bảo trì');
