-- =====================================================================
-- EDI HANG TAU (UN/EDIFACT)
--
-- Depot khong so huu vo container — no giu ho cho hang tau. Nen moi lan
-- container vao/ra bai, hang tau BAT BUOC phai biet. Hien nay o Viet Nam
-- phan lon depot nho van bao cao bang Excel gui email: nguoi go tay, sai
-- so, hang tau doi soat khong khop -> tru tien.
--
-- CODECO = ban tin bao cao vao/ra depot (depot -> hang tau)
-- COPARN = ban tin hang tau bao truoc (hang tau -> depot), dung de tao lenh
--
-- Bang nay luu ca NOI DUNG ban tin de in/gui lai va truy vet khi tranh chap.
-- =====================================================================

IF OBJECT_ID('BanTinEDI', 'U') IS NULL
CREATE TABLE BanTinEDI (
    MaBT         INT IDENTITY(1,1) PRIMARY KEY,
    LoaiBT       NVARCHAR(10)   NOT NULL,       -- CODECO / COPARN / COARRI
    Huong        NVARCHAR(10)   NOT NULL,       -- Gui / Nhan
    MaHangTau    INT            NULL,
    SoThamChieu  NVARCHAR(30)   NULL,           -- so tham chieu trao doi (UNB/UNZ)
    SoChungTu    NVARCHAR(30)   NULL,           -- so chung tu trong BGM
    TuNgay       DATETIME       NULL,
    DenNgay      DATETIME       NULL,
    SoLuongCont  INT            NOT NULL DEFAULT 0,
    NoiDung      NVARCHAR(MAX)  NULL,
    TrangThai    NVARCHAR(20)   NULL,           -- Chờ gửi / Đã gửi / Đã nhận / Đã xử lý / Lỗi
    GhiChu       NVARCHAR(500)  NULL,
    NgayTao      DATETIME       NOT NULL DEFAULT GETDATE(),
    NguoiTao     NVARCHAR(50)   NULL,
    NgayGui      DATETIME       NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BanTinEDI_Loai' AND object_id = OBJECT_ID('BanTinEDI'))
    CREATE INDEX IX_BanTinEDI_Loai ON BanTinEDI (LoaiBT, Huong, NgayTao);

-- Ma SCAC cua hang tau da co san o cot HangTau.MaSCAC — EDI dung ma do lam
-- dinh danh ben nhan, nen khong can them cot moi.

-- Danh dau luot xe DA nam trong ban tin CODECO nao -> khong bao cao trung
IF COL_LENGTH('XeRaVao', 'MaBT') IS NULL
    ALTER TABLE XeRaVao ADD MaBT INT NULL;
