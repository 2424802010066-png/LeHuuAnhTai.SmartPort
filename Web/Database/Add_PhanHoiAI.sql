-- ============================================================
-- VONG LAP TU HOC (ACTIVE LEARNING) — phan hoi cua con nguoi
-- ve ket qua nhan dien AI. He thong dung du lieu nay de TU DIEU
-- CHINH nguong tin cay theo tung lop doi tuong (data flywheel).
-- Script idempotent.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PhanHoiAI')
BEGIN
    CREATE TABLE dbo.PhanHoiAI (
        MaPH     INT IDENTITY(1,1) PRIMARY KEY,
        Nhan     NVARCHAR(50) NOT NULL,        -- nhan goc COCO: truck / person / car / boat...
        DungSai  BIT NOT NULL,                 -- 1 = AI nhan dung, 0 = AI nhan sai
        DoTinCay DECIMAL(5,2) NULL,            -- do tin cay cua lan nhan dien do
        ThoiGian DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_PhanHoiAI_Nhan ON dbo.PhanHoiAI(Nhan);
    PRINT N'Da tao bang PhanHoiAI.';
END
ELSE
    PRINT N'Bang PhanHoiAI da ton tai - bo qua.';

-- Seed phan hoi mau (chi khi rong) — de demo co ngay du lieu tu hoc:
-- truck chinh xac cao, person hay nham -> he thong se tu NANG nguong person
IF NOT EXISTS (SELECT 1 FROM dbo.PhanHoiAI)
BEGIN
    INSERT INTO dbo.PhanHoiAI (Nhan, DungSai, DoTinCay, ThoiGian) VALUES
    (N'truck', 1, 88.2, DATEADD(DAY,-2,GETDATE())), (N'truck', 1, 91.5, DATEADD(DAY,-2,GETDATE())),
    (N'truck', 1, 84.1, DATEADD(DAY,-1,GETDATE())), (N'truck', 1, 89.9, DATEADD(DAY,-1,GETDATE())),
    (N'truck', 1, 93.0, DATEADD(DAY,-1,GETDATE())), (N'truck', 0, 41.7, DATEADD(DAY,-1,GETDATE())),
    (N'truck', 1, 87.4, GETDATE()),                 (N'truck', 1, 90.2, GETDATE()),
    (N'person', 1, 78.5, DATEADD(DAY,-2,GETDATE())),(N'person', 0, 38.9, DATEADD(DAY,-2,GETDATE())),
    (N'person', 1, 82.3, DATEADD(DAY,-1,GETDATE())),(N'person', 0, 42.1, DATEADD(DAY,-1,GETDATE())),
    (N'person', 1, 75.6, DATEADD(DAY,-1,GETDATE())),(N'person', 0, 36.4, GETDATE()),
    (N'person', 1, 80.0, GETDATE()),                (N'person', 1, 77.2, GETDATE()),
    (N'car', 1, 85.3, DATEADD(DAY,-1,GETDATE())),   (N'car', 1, 88.8, DATEADD(DAY,-1,GETDATE())),
    (N'car', 1, 82.6, GETDATE()),                   (N'car', 1, 90.1, GETDATE()),
    (N'boat', 1, 74.9, DATEADD(DAY,-1,GETDATE())),  (N'boat', 0, 45.2, GETDATE()),
    (N'boat', 1, 79.3, GETDATE()),                  (N'bus', 1, 86.7, GETDATE());
    PRINT N'Da seed 24 phan hoi mau.';
END

SELECT COUNT(*) AS TongPhanHoi FROM dbo.PhanHoiAI;
