-- ============================================================
-- BANG CAMERA IP (trang /admin/camera — camera hien truong RTSP/HTTP)
-- Bang nay co trong CSDL demo nhung chua co script tao rieng; trich tu dinh nghia
-- bang trong CSDL demo (SMO). Script idempotent: chay nhieu lan khong loi, KHONG xoa du lieu.
-- ============================================================

IF OBJECT_ID(N'dbo.CameraIP', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CameraIP (
        MaCam         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TenCam        NVARCHAR(60)  NOT NULL,
        ViTri         NVARCHAR(60)  NULL,
        Hang          NVARCHAR(20)  NULL,
        DiaChi        NVARCHAR(120) NOT NULL,
        TenDangNhap   NVARCHAR(60)  NULL,
        MatKhau       NVARCHAR(120) NULL,
        Kenh          INT           NULL,
        DuongDanRieng NVARCHAR(200) NULL,
        KichHoat      BIT           NOT NULL CONSTRAINT DF_CameraIP_KichHoat DEFAULT 1,
        LanThuCuoi    DATETIME      NULL,
        KetQuaThu     NVARCHAR(200) NULL,
        NgayTao       DATETIME      NOT NULL CONSTRAINT DF_CameraIP_NgayTao DEFAULT GETDATE()
    );
    PRINT N'Da tao bang CameraIP.';
END
ELSE
    PRINT N'Bang CameraIP da ton tai - bo qua.';
