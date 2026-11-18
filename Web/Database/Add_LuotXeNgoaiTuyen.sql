-- =====================================================================
-- LUOT XE NGOAI TUYEN — bang CUC BO cua dich vu Cong (CSDL SmartPortMS_Cong; ban gop: SmartPortDB / somee)
--
-- Tram cong mat Wi-Fi van cho xe qua, ghi tam trong may roi dong bo len (POST /api/cong/dong-bo). Bang nay
-- giu cho moi thao tac:
--   MaThaoTac (khoa chinh) : khoa chong gui lap do TRAM sinh (uuid) / header Idempotency-Key cua /api/cong/luot-xe.
--                            Gui lai (mat phan hoi, 2 tab, 2 lo song song) -> tra dung ket qua cu, KHONG ghi luot thu 2,
--                            KHONG thu phi 2 lan. Hai yeu cau cung khoa dong thoi: yeu cau sau cho khoa dong cua
--                            chi muc roi nhan loi trung khoa 2627 -> doc ket qua cu (khong co cua so "kiem tra roi ghi").
--   LucXayRa / LucGuiTuMay : gio theo dong ho MAY TRAM (chua bu lech); DoLechGiay = gio may chu nhan - gio tram gui.
--   ThoiGianGhi            : gio da ghi vao XeRaVao = LucXayRa + do lech (gio xe qua cong THAT).
--   TrangThai              : da-luu | can-doi-soat (+ LyDoDoiSoat) — luot ghi tre vi pham tuan thu / xung dot.
--
-- KHONG doi cot XeRaVao (bang dang sao chep sang cac dich vu khac -> doi cot phai doi moi CSDL ban sao).
-- Script CHI THEM bang / chi muc, chay lai nhieu lan van an toan, KHONG DROP, KHONG sua / xoa du lieu cu.
-- Cot thoi gian dung datetime2 (bai hoc loi lam tron datetime <-> datetime2 cua phieu luu bai).
-- Chua chay script nay: dich vu van chay nhu cu (ghi log 1 lan), chi mat chong gui lap theo ma thao tac.
-- =====================================================================

-- Chi muc co loc (WHERE) bat buoc 2 tuy chon nay; sqlcmd mac dinh tat QUOTED_IDENTIFIER
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.LuotXeNgoaiTuyen', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LuotXeNgoaiTuyen (
        MaThaoTac       NVARCHAR(64)   NOT NULL CONSTRAINT PK_LuotXeNgoaiTuyen PRIMARY KEY,
        MaXV            INT            NULL,                 -- luot xe da ghi (XeRaVao.MaXV)
        TrangThai       NVARCHAR(20)   NOT NULL,             -- da-luu | can-doi-soat
        LyDoDoiSoat     NVARCHAR(500)  NULL,
        Nguon           NVARCHAR(20)   NULL,                 -- NGOAI-TUYEN | OFFLINE | nguon luot thuong
        MaThietBi       NVARCHAR(64)   NULL,                 -- vd tram-cong-A1
        BienSo          NVARCHAR(20)   NOT NULL,
        HuongDi         NVARCHAR(10)   NOT NULL,             -- Vao / Ra (co dau, nhu XeRaVao.HuongDi)
        Cong            NVARCHAR(20)   NULL,
        LucXayRa        DATETIME2(3)   NULL,                 -- gio xay ra theo may tram (chua bu lech)
        LucGuiTuMay     DATETIME2(3)   NULL,                 -- gio may tram gui lo
        DoLechGiay      INT            NULL,                 -- may chu nhan - tram gui (giay)
        ThoiGianGhi     DATETIME2(3)   NOT NULL,             -- gio ghi vao XeRaVao (da bu lech)
        NhanLuc         DATETIME2(3)   NOT NULL CONSTRAINT DF_LuotXeNgoaiTuyen_NhanLuc DEFAULT SYSDATETIME(),
        TienMatTaiCong  DECIMAL(18, 2) NULL,                 -- tien mat bao ve da thu tai cong (phieu tay)
        SoPhieuThu      NVARCHAR(30)   NULL,
        SoContTram      NVARCHAR(20)   NULL,                 -- so container tram ghi (doi chieu voi lenh)
        MaLenhTram      INT            NULL,                 -- ma lenh tram gui len
        NguoiThaoTac    NVARCHAR(100)  NULL,                 -- nguoi bam ghi tai tram
        NguoiDongBo     NVARCHAR(100)  NULL,                 -- phien dang nhap luc dong bo
        KetQuaJson      NVARCHAR(MAX)  NULL,                 -- ket qua lan dau, tra lai khi gui lap
        DaDoiSoat       BIT            NOT NULL CONSTRAINT DF_LuotXeNgoaiTuyen_DaDoiSoat DEFAULT 0,
        DoiSoatLuc      DATETIME2(3)   NULL,
        NguoiDoiSoat    NVARCHAR(100)  NULL,
        GhiChuDoiSoat   NVARCHAR(500)  NULL
    );
END
GO

-- Danh sach "can doi soat" chua xu ly (man hinh dieu do / an ninh)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LuotXeNgoaiTuyen_CanDoiSoat' AND object_id = OBJECT_ID(N'dbo.LuotXeNgoaiTuyen'))
    CREATE INDEX IX_LuotXeNgoaiTuyen_CanDoiSoat ON dbo.LuotXeNgoaiTuyen (NhanLuc)
        INCLUDE (MaXV, BienSo, HuongDi, LyDoDoiSoat)
        WHERE TrangThai = N'can-doi-soat' AND DaDoiSoat = 0;
GO

-- Tra nguoc tu luot xe -> thao tac ngoai tuyen (doi soat, kiem toan)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LuotXeNgoaiTuyen_MaXV' AND object_id = OBJECT_ID(N'dbo.LuotXeNgoaiTuyen'))
    CREATE INDEX IX_LuotXeNgoaiTuyen_MaXV ON dbo.LuotXeNgoaiTuyen (MaXV) WHERE MaXV IS NOT NULL;
GO
