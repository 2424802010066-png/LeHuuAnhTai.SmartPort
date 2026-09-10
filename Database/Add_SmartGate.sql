/* ============================================================
   SMART GATE 2.0 — dieu phoi Dock tu dong (dong bo kieu YMS)
   Chay tren CA local lan hosting (idempotent).
   LUU Y: phai co GO tach lo — khong thi SQL Server bien dich ca
   lo mot luot va bao "Invalid column name".
   ============================================================ */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.XeRaVao') AND name = 'ViTriDo'
)
BEGIN
    ALTER TABLE dbo.XeRaVao ADD ViTriDo VARCHAR(10) NULL;
    PRINT 'Da them cot XeRaVao.ViTriDo (Dock dieu phoi).';
END
ELSE PRINT 'Cot ViTriDo da co.';
GO

SELECT SoXe = COUNT(*), CoDock = SUM(CASE WHEN ViTriDo IS NULL THEN 0 ELSE 1 END) FROM dbo.XeRaVao;
GO
