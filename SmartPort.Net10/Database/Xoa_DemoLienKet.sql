-- Go sach du lieu demo do Seed_DemoLienKet.sql tao ra (moc: GhiChu chua 'DEMO-LK').
-- Chay truoc khi ban giao he thong that cho khach.
DELETE FROM XeRaVao      WHERE GhiChu LIKE '%DEMO-LK%';
DELETE FROM PhieuCan     WHERE GhiChu LIKE '%DEMO-LK%';
DELETE FROM PhieuSuaChua WHERE GhiChu LIKE '%DEMO-LK%';
DELETE FROM LenhGiaoNhan WHERE GhiChu LIKE '%DEMO-LK%';
