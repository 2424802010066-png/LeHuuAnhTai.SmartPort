-- =====================================================================
-- DON DU LIEU CU VE MOT TRUC (chay MOT LAN sau khi cai ban co lien ket)
--
-- Cac luot xe ghi TRUOC khi co cot SoCont deu dang mo coi. Doan nay noi
-- lai bang cach doi chieu voi lenh giao nhan: cung bien so, va thoi diem
-- qua cong nam trong khung gio hen (co noi long 6 gio hai dau vi xe hay
-- den som/muon).
--
-- AN TOAN: CHI cap nhat khi co DUNG MOT lenh ung vien. Co tu hai lenh tro
-- len thi de nguyen — tha thieu du lieu con hon gan sai container cho mot
-- luot xe roi ke toan xuat hoa don nham nguoi.
-- =====================================================================

UPDATE x
SET x.SoCont = k.SoCont,
    x.MaLenh = k.MaLenh
FROM XeRaVao x
CROSS APPLY (
    SELECT TOP 2 l.MaLenh, l.SoCont
    FROM LenhGiaoNhan l
    WHERE l.BienSoXe = x.BienSo
      AND l.SoCont IS NOT NULL
      AND x.ThoiGian >= DATEADD(hour, -6, l.HenTuLuc)
      AND x.ThoiGian <= DATEADD(hour,  6, l.HenDenLuc)
) k
WHERE x.SoCont IS NULL
  AND (SELECT COUNT(*)
       FROM LenhGiaoNhan l2
       WHERE l2.BienSoXe = x.BienSo
         AND l2.SoCont IS NOT NULL
         AND x.ThoiGian >= DATEADD(hour, -6, l2.HenTuLuc)
         AND x.ThoiGian <= DATEADD(hour,  6, l2.HenDenLuc)) = 1;
