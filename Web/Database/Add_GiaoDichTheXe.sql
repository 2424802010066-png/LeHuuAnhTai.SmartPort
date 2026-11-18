-- =====================================================================
-- SO CAI THE XE (GiaoDichTheXe) — CSDL TAI CHINH (SmartPortMS_TaiChinh; ban gop: SmartPortDB)
--
-- Moi lan NAP tien (+) / TRU phi cong (-) vao the tra truoc deu ghi THEM mot dong, khong bao gio sua / xoa:
--   - doi soat: so du hien tai = so du sau cua dong cuoi; tung dong: SoDuSau = SoDuSau dong truoc + SoTien
--   - CHONG TRU 2 LAN: UNIQUE (MaXV, Loai) — su kien "xe da qua cong" den lai (mat mang, phat lai, 2 ban dich vu)
--     thi lan sau vi pham khoa duy nhat -> giao dich bi huy -> so du KHONG bi tru them.
-- Bang CUC BO cua dich vu Tai chinh (khong sao chep sang dich vu khac) -> khong dung cot bang TheXe dang sao chep.
-- Ung dung ghi bang SQL tho (khong co DbSet trong AppDb); bang chua co thi ung dung chay nhu cu (suy giam em).
--
-- IDEMPOTENT: chay lai bao nhieu lan cung duoc, khong xoa bang / CSDL nao. Cot thoi gian datetime2.
-- =====================================================================

-- Chi muc co loc (WHERE) va trigger can 2 tuy chon nay; sqlcmd mac dinh tat QUOTED_IDENTIFIER
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.GiaoDichTheXe', N'U') IS NULL
CREATE TABLE dbo.GiaoDichTheXe (
    MaGD      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GiaoDichTheXe PRIMARY KEY,
    MaThe     INT            NOT NULL,                 -- the xe (khong dat khoa ngoai: xoa the van giu lich su tien)
    BienSo    NVARCHAR(20)   NULL,
    Loai      VARCHAR(10)    NOT NULL,                 -- NAP | TRU
    SoTien    DECIMAL(18,2)  NOT NULL,                 -- NAP > 0, TRU < 0
    SoDuSau   DECIMAL(18,2)  NOT NULL,                 -- so du NGAY SAU giao dich (doc trong cung giao dich)
    MaXV      INT            NULL,                     -- luot xe bi tru phi (NAP: NULL)
    GhiChu    NVARCHAR(300)  NULL,
    NguoiTao  NVARCHAR(100)  NULL,
    LucTao    DATETIME2(3)   NOT NULL CONSTRAINT DF_GiaoDichTheXe_LucTao DEFAULT SYSDATETIME(),
    CONSTRAINT CK_GiaoDichTheXe_Loai CHECK (Loai IN ('NAP', 'TRU')),
    CONSTRAINT CK_GiaoDichTheXe_Dau CHECK ((Loai = 'NAP' AND SoTien > 0) OR (Loai = 'TRU' AND SoTien < 0))
);
GO

-- CHONG TRU 2 LAN cho cung mot luot xe (NAP khong co MaXV nen nam ngoai chi muc)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GiaoDichTheXe_MaXV_Loai' AND object_id = OBJECT_ID(N'dbo.GiaoDichTheXe'))
    CREATE UNIQUE INDEX UX_GiaoDichTheXe_MaXV_Loai ON dbo.GiaoDichTheXe (MaXV, Loai) WHERE MaXV IS NOT NULL;
GO

-- Xem so cai tung the theo thu tu thoi gian
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GiaoDichTheXe_MaThe' AND object_id = OBJECT_ID(N'dbo.GiaoDichTheXe'))
    CREATE INDEX IX_GiaoDichTheXe_MaThe ON dbo.GiaoDichTheXe (MaThe, MaGD) INCLUDE (Loai, SoTien, SoDuSau, MaXV, LucTao);
GO

-- CHI GHI THEM: chan moi lenh UPDATE / DELETE (sai thi ghi but toan nguoc, khong sua dong cu)
IF OBJECT_ID(N'dbo.TR_GiaoDichTheXe_ChiGhiThem', N'TR') IS NULL
    EXEC (N'CREATE TRIGGER dbo.TR_GiaoDichTheXe_ChiGhiThem ON dbo.GiaoDichTheXe
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N''GiaoDichTheXe là sổ cái chỉ-ghi-thêm: không được sửa hoặc xóa dòng đã ghi.'', 1;
END');
GO
