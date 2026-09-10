-- ============================================================
-- MODULE ALPR — CONG THONG MINH: nhan dien bien so xe ra/vao cang
-- (YOLO26 phat hien xe + Tesseract LSTM doc bien so, luu nhat ky)
-- Script idempotent: chay nhieu lan khong loi.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'XeRaVao')
BEGIN
    CREATE TABLE dbo.XeRaVao (
        MaXV      INT IDENTITY(1,1) PRIMARY KEY,
        BienSo    NVARCHAR(20)  NOT NULL,
        Cong      NVARCHAR(20)  NOT NULL DEFAULT N'A1',
        HuongDi   NVARCHAR(10)  NOT NULL DEFAULT N'Vào',   -- Vào / Ra
        LoaiXe    NVARCHAR(30)  NULL,                       -- Xe tải / Đầu kéo / Ô tô...
        DoTinCay  DECIMAL(5,2)  NULL,                       -- % tin cay OCR
        Nguon     NVARCHAR(20)  NULL,                       -- AnhMau / TaiAnh / Webcam
        ThoiGian  DATETIME NOT NULL DEFAULT GETDATE(),
        GhiChu    NVARCHAR(200) NULL
    );
    CREATE INDEX IX_XeRaVao_ThoiGian ON dbo.XeRaVao(ThoiGian DESC);
    CREATE INDEX IX_XeRaVao_BienSo  ON dbo.XeRaVao(BienSo);
    PRINT N'Da tao bang XeRaVao.';
END
ELSE
    PRINT N'Bang XeRaVao da ton tai - bo qua.';

-- ================= SEED DU LIEU MAU (chi khi bang rong) =================
IF NOT EXISTS (SELECT 1 FROM dbo.XeRaVao)
BEGIN
    DECLARE @i INT = 0, @ngay INT, @gio INT, @phut INT;
    DECLARE @bien NVARCHAR(20), @huong NVARCHAR(10), @loai NVARCHAR(30), @cong NVARCHAR(20);

    -- 60 luot xe trong 3 ngay gan nhat, gio cao diem 7-10h & 14-17h
    WHILE @i < 60
    BEGIN
        SET @ngay = @i % 3;
        SET @gio  = CASE WHEN @i % 2 = 0 THEN 7 + (@i % 4) ELSE 13 + (@i % 5) END;
        SET @phut = (@i * 37) % 60;
        SET @bien = CASE @i % 10
            WHEN 0 THEN N'15C-274.84' WHEN 1 THEN N'29C-468.28' WHEN 2 THEN N'51D-123.45'
            WHEN 3 THEN N'15C-009.97' WHEN 4 THEN N'43A-555.19' WHEN 5 THEN N'89C-121.72'
            WHEN 6 THEN N'61H-777.88' WHEN 7 THEN N'15C-188.47' WHEN 8 THEN N'50H-246.80'
            ELSE      N'34F-902.11' END;
        SET @huong = CASE WHEN @i % 3 = 2 THEN N'Ra' ELSE N'Vào' END;
        SET @loai  = CASE @i % 4 WHEN 0 THEN N'Đầu kéo container' WHEN 1 THEN N'Xe tải'
                                 WHEN 2 THEN N'Xe tải' ELSE N'Đầu kéo container' END;
        SET @cong  = CASE WHEN @i % 5 = 4 THEN N'A2' ELSE N'A1' END;

        INSERT INTO dbo.XeRaVao (BienSo, Cong, HuongDi, LoaiXe, DoTinCay, Nguon, ThoiGian)
        VALUES (@bien, @cong, @huong, @loai,
                82 + (@i % 16),
                CASE WHEN @i % 4 = 0 THEN N'Webcam' ELSE N'AnhMau' END,
                DATEADD(MINUTE, @phut, DATEADD(HOUR, @gio, CAST(DATEADD(DAY, -@ngay, CAST(GETDATE() AS date)) AS datetime))));
        SET @i = @i + 1;
    END
    PRINT N'Da seed 60 luot xe mau.';
END

SELECT COUNT(*) AS TongLuotXe FROM dbo.XeRaVao;
