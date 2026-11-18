-- =====================================================================
-- PHIEN BAN BAN SAO — bang CUC BO moi dich vu (khong sao chep)
--
-- BanSaoPhienBan : moi dong ban sao (bang + khoa chinh) nho "phien ban" da ap = gio tao + Id dong Outbox cua
--                  dich vu so huu (header RabbitMQ outbox-luc / outbox-id). Tin "du-lieu.*" CU HON (vd tin loi
--                  di vong qua hang doi thu lai, toi SAU ban cap nhat moi hon) bi bo qua -> khong ghi de du lieu
--                  moi bang du lieu cu (lenh / eDO o Cong lui trang thai). Xoa dong -> giu phien ban lam "bia mo"
--                  de ban "sua" cu khong lam dong da xoa song lai.
-- Dung boi SmartPort.BuildingBlocks/SaoChep/PhienBanBanSao.cs. Bang chua co -> ap ban sao nhu cu (khong loi).
-- Dong cu hon thoi gian giu inbox (mac dinh 30 ngay) do tien trinh nen DonDepBangCucBo xoa.
--
-- Script CHI THEM bang moi, chay lai nhieu lan van an toan, KHONG DROP, KHONG sua/xoa du lieu cu.
-- Chay tren: moi CSDL SmartPortMS_* (dung_csdl_dich_vu.py tu chay). Ban gop (1 CSDL) khong can.
-- =====================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.BanSaoPhienBan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BanSaoPhienBan (
        Bang         NVARCHAR(100)   NOT NULL,              -- ten bang ban sao, vd LenhGiaoNhan
        Khoa         NVARCHAR(350)   NOT NULL,              -- khoa chinh dang chuan, vd MaLenh=12 (dai qua -> bam); 100+350 ky tu = 900 byte (gioi han khoa chinh)
        PhienBanLuc  DATETIME2(3)    NOT NULL,              -- gio tao dong Outbox o dich vu so huu (UTC)
        OutboxId     BIGINT          NOT NULL,              -- Id dong Outbox (so sanh khi cung gio)
        DichVuNguon  NVARCHAR(50)    NULL,
        CapNhatLuc   DATETIME2(3)    NOT NULL CONSTRAINT DF_BanSaoPhienBan_CapNhatLuc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_BanSaoPhienBan PRIMARY KEY (Bang, Khoa)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BanSaoPhienBan_CapNhat' AND object_id = OBJECT_ID(N'dbo.BanSaoPhienBan'))
    CREATE INDEX IX_BanSaoPhienBan_CapNhat ON dbo.BanSaoPhienBan (CapNhatLuc);
GO
