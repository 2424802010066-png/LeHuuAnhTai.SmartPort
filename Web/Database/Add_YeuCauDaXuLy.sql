-- =====================================================================
-- CHONG GUI LAP (Idempotency-Key) — bang CUC BO moi dich vu (khong sao chep)
--
-- YeuCauDaXuLy : moi thao tac ghi (POST/PUT/...) mang header Idempotency-Key duoc "giu cho" bang 1 dong
--                (khoa chinh = SHA-256 cua khoa + phuong thuc + duong dan + nguoi dung). Wifi chap chon,
--                nguoi dung bam lai -> API tra NGUYEN ket qua cu (ThanTraLoi), khong ghi lan 2.
--                2 yeu cau cung khoa cung luc: khoa chinh chi cho 1 dong -> yeu cau sau cho / 409.
--                BamThan (15/10/2026): SHA-256 cua THAN yeu cau lan dau. Cung khoa ma than khac -> 422 (khong im lang
--                tra ket qua cua thao tac kia). NULL = dong cu truoc khi co cot -> khong so.
-- Dung boi thuoc tinh [ChongGuiLap] (SmartPort.NenTang/HaTang/ChongGuiLap.cs). Bang chua co -> API van
-- chay binh thuong (ghi log 1 lan), nen chay script nay luc nao cung duoc.
-- Dong het han (HetHanLuc, mac dinh 72 gio) do tien trinh nen DonDepBangCucBo xoa.
--
-- Script CHI THEM bang / cot moi (nullable), chay lai nhieu lan van an toan, KHONG DROP, KHONG sua/xoa du lieu cu.
-- Chay tren: moi CSDL SmartPortMS_* (dung_csdl_dich_vu.py tu chay) + CSDL ban gop (SmartPortDB / somee) neu muon bat.
-- =====================================================================

-- Chi muc co loc / cot tinh toan bat buoc 2 tuy chon nay; sqlcmd mac dinh tat QUOTED_IDENTIFIER
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.YeuCauDaXuLy', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauDaXuLy (
        KhoaBam      CHAR(64)        NOT NULL CONSTRAINT PK_YeuCauDaXuLy PRIMARY KEY,  -- SHA-256 hex
        Khoa         NVARCHAR(64)    NOT NULL,              -- Idempotency-Key goc (tra cuu)
        PhuongThuc   VARCHAR(10)     NOT NULL,              -- POST / PUT / PATCH / DELETE
        DuongDan     NVARCHAR(400)   NOT NULL,
        NguoiDung    NVARCHAR(100)   NULL,                  -- tk:<MaTK> | api:<bam khoa API> | an-danh
        TrangThai    TINYINT         NOT NULL CONSTRAINT DF_YeuCauDaXuLy_TrangThai DEFAULT 0,   -- 0 dang xu ly, 1 xong
        MaHttp       INT             NULL,
        KieuNoiDung  NVARCHAR(200)   NULL,                  -- Content-Type tra loi
        ViTri        NVARCHAR(400)   NULL,                  -- header Location (201 Created)
        ThanTraLoi   VARBINARY(MAX)  NULL,                  -- than tra loi nguyen byte (toi da ~256 KB)
        ThanQuaLon   BIT             NOT NULL CONSTRAINT DF_YeuCauDaXuLy_ThanQuaLon DEFAULT 0,
        BatDauLuc    DATETIME2(3)    NOT NULL CONSTRAINT DF_YeuCauDaXuLy_BatDauLuc DEFAULT SYSUTCDATETIME(),
        GiuDen       DATETIME2(3)    NOT NULL,              -- qua gio nay ma chua xong = yeu cau truoc da chet -> cho chay lai
        XongLuc      DATETIME2(3)    NULL,
        HetHanLuc    DATETIME2(3)    NOT NULL,              -- het han luu ket qua -> don dep
        BamThan      CHAR(64)        NULL                   -- SHA-256 hex cua than yeu cau (NULL = khong so)
    );
END
GO

-- Cot moi cho bang DA CO (CSDL tao truoc 15/10/2026): them nullable, khong dong cham du lieu cu
IF OBJECT_ID(N'dbo.YeuCauDaXuLy', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.YeuCauDaXuLy', N'BamThan') IS NULL
    ALTER TABLE dbo.YeuCauDaXuLy ADD BamThan CHAR(64) NULL;
GO

-- Don dep tim dong het han
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_YeuCauDaXuLy_HetHan' AND object_id = OBJECT_ID(N'dbo.YeuCauDaXuLy'))
    CREATE INDEX IX_YeuCauDaXuLy_HetHan ON dbo.YeuCauDaXuLy (HetHanLuc);
GO
