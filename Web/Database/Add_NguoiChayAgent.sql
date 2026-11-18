-- ============================================================
-- NHAT KY AGENT: ghi lai AI DA KHOI DONG luot chay
--
-- VI SAO CAN: bang NhatKyAgent ton tai de tra loi "sao tu nhien co
-- phieu nay". No dang tra loi duoc "may lam hay nguoi lam" (cot TuDong)
-- nhung KHONG tra loi duoc "nguoi nao bam". Khi mot phieu thu bi khach
-- khieu nai, cau hoi dau tien cua chu depot la "ai cho no chay?" —
-- thieu cot nay thi nhat ky mat mot nua gia tri doi chung.
--
-- Gia tri: ten dang nhap ('admin'), hoac 'nen' khi bo hen gio tu chay,
-- hoac 'api:<ten ung dung>' khi goi qua /api/v1/agent/chay.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'NhatKyAgent' AND COLUMN_NAME = 'NguoiChay')
    ALTER TABLE NhatKyAgent ADD NguoiChay nvarchar(100) NULL;
