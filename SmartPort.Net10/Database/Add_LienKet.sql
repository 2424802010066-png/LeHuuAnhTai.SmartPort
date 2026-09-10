-- =====================================================================
-- LIEN KET DU LIEU TOAN HE THONG (dong bo)
-- Van de: bang XeRaVao chi luu BIEN SO — khong biet xe do cho container
-- nao, theo lenh nao. Hau qua: khong dung duoc "mot dong thoi gian" cho
-- container nhu phan mem depot quoc te (ContainerHub/ContPark) va khong
-- doi chieu duoc khi khach khieu nai.
-- Sau migration nay: moi luot xe qua cong deu gan duoc voi container +
-- lenh giao nhan -> Ho so container 360 do lay duoc du lieu that.
-- =====================================================================

IF COL_LENGTH('XeRaVao', 'SoCont') IS NULL
    ALTER TABLE XeRaVao ADD SoCont NVARCHAR(11) NULL;

IF COL_LENGTH('XeRaVao', 'MaLenh') IS NULL
    ALTER TABLE XeRaVao ADD MaLenh INT NULL;

-- Tra cuu theo so container phai nhanh (man hinh ho so goi lien tuc)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XeRaVao_SoCont' AND object_id = OBJECT_ID('XeRaVao'))
    CREATE INDEX IX_XeRaVao_SoCont ON XeRaVao (SoCont) WHERE SoCont IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XeRaVao_BienSo' AND object_id = OBJECT_ID('XeRaVao'))
    CREATE INDEX IX_XeRaVao_BienSo ON XeRaVao (BienSo);
