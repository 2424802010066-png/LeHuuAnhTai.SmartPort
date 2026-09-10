/* ============================================================
   CONG THONG MINH ALPR — LUU ANH XE QUA CONG
   Chay tren CA local lan hosting (idempotent, chay lai vo tu).
   ============================================================ */

-- Cot duong dan anh chup xe tai cong (~/Uploads/gate/xxx.jpg)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.XeRaVao') AND name = 'AnhXe'
)
BEGIN
    ALTER TABLE dbo.XeRaVao ADD AnhXe VARCHAR(300) NULL;
    PRINT 'Da them cot XeRaVao.AnhXe.';
END
ELSE PRINT 'Cot AnhXe da co.';
GO

-- Cot luu nguyen van chuoi OCR doc duoc (phuc vu doi chieu/khieu nai)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.XeRaVao') AND name = 'OcrTho'
)
BEGIN
    ALTER TABLE dbo.XeRaVao ADD OcrTho NVARCHAR(120) NULL;
    PRINT 'Da them cot XeRaVao.OcrTho.';
END
ELSE PRINT 'Cot OcrTho da co.';
GO

-- Lo lenh RIENG (sau GO) — neu khong, SQL Server bien dich ca lo mot luot
-- va bao "Invalid column name 'AnhXe'" vi cot chua ton tai luc bien dich.
SELECT SoXe = COUNT(*), SoCoAnh = SUM(CASE WHEN AnhXe IS NULL THEN 0 ELSE 1 END) FROM dbo.XeRaVao;
GO
