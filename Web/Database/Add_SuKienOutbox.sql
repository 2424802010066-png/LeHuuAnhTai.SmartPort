-- =====================================================================
-- KIEN TRUC HUONG SU KIEN — TRANSACTIONAL OUTBOX
--
-- SuKienOutbox : "hop thu di". Module ghi du lieu nghiep vu VA dong su kien vao day trong
--                CUNG mot giao dich -> khong bao gio mat su kien (hoac co su kien ma khong
--                co du lieu) khi may chu tat giua chung.
-- SuKienDaXuLy : "so da xu ly" (inbox). Khoa chinh (MaSuKien, BoXuLy): su kien bi giao lai
--                thi bo xu ly khong lam lan thu hai -> khong thu phi / lap phieu trung.
--
-- Script CHI THEM bang moi, chay lai nhieu lan van an toan, KHONG sua/xoa du lieu cu.
-- Chay tren: CSDL local (.\MSSQLSERVER01 / SmartPortDB) va CSDL deploy (somee) TRUOC khi
-- trien khai ban co module su kien.
-- =====================================================================

-- Chi muc co loc (WHERE) bat buoc 2 tuy chon nay; sqlcmd mac dinh tat QUOTED_IDENTIFIER
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.SuKienOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SuKienOutbox (
        Id           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SuKienOutbox PRIMARY KEY,
        MaSuKien     UNIQUEIDENTIFIER NOT NULL,
        Loai         NVARCHAR(100)  NOT NULL,              -- vd cong.xe-da-qua-cong
        Module       NVARCHAR(30)   NOT NULL,              -- module phat: Cổng / Bãi / Tài chính
        NoiDung      NVARCHAR(MAX)  NOT NULL,              -- JSON su kien
        MaTuongQuan  NVARCHAR(60)   NULL,                  -- gom su kien cung nghiep vu, vd xe:221
        NgayTao      DATETIME2(3)   NOT NULL CONSTRAINT DF_SuKienOutbox_NgayTao DEFAULT SYSUTCDATETIME(),
        TrangThai    TINYINT        NOT NULL CONSTRAINT DF_SuKienOutbox_TrangThai DEFAULT 0,  -- 0 cho, 1 xong, 2 loi
        SoLanThu     INT            NOT NULL CONSTRAINT DF_SuKienOutbox_SoLanThu DEFAULT 0,
        ThuLaiLuc    DATETIME2(3)   NOT NULL CONSTRAINT DF_SuKienOutbox_ThuLaiLuc DEFAULT SYSUTCDATETIME(),
        KhoaDen      DATETIME2(3)   NULL,                  -- het han "giu cho" cua bo phat dang xu ly
        DangXuLyBoi  NVARCHAR(100)  NULL,
        XuLyLuc      DATETIME2(3)   NULL,
        LoiCuoi      NVARCHAR(1000) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SuKienOutbox_MaSuKien' AND object_id = OBJECT_ID(N'dbo.SuKienOutbox'))
    CREATE UNIQUE INDEX UX_SuKienOutbox_MaSuKien ON dbo.SuKienOutbox (MaSuKien);
GO

-- Bo phat su kien tim "dang cho, den gio thu" lien tuc: chi muc loc rieng cac dong chua xong
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuKienOutbox_DangCho' AND object_id = OBJECT_ID(N'dbo.SuKienOutbox'))
    CREATE INDEX IX_SuKienOutbox_DangCho ON dbo.SuKienOutbox (ThuLaiLuc, Id) INCLUDE (KhoaDen) WHERE TrangThai = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SuKienOutbox_MaTuongQuan' AND object_id = OBJECT_ID(N'dbo.SuKienOutbox'))
    CREATE INDEX IX_SuKienOutbox_MaTuongQuan ON dbo.SuKienOutbox (MaTuongQuan) WHERE MaTuongQuan IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.SuKienDaXuLy', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SuKienDaXuLy (
        MaSuKien  UNIQUEIDENTIFIER NOT NULL,
        BoXuLy    NVARCHAR(150)    NOT NULL,               -- ten lop bo xu ly
        XuLyLuc   DATETIME2(3)     NOT NULL CONSTRAINT DF_SuKienDaXuLy_XuLyLuc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SuKienDaXuLy PRIMARY KEY (MaSuKien, BoXuLy)
    );
END
GO
