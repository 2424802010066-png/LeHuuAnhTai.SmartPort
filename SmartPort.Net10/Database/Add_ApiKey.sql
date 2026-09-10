-- =====================================================================
-- API TICH HOP — khoa truy cap cho phan mem BEN NGOAI (ke toan, WMS, TMS)
--
-- Ly do: bao cao Logistics VN 2024 chi ra "thieu co che lien ket" giua
-- kho bai va van tai la mot trong nhung nguyen nhan day chi phi len; doi
-- thu GTEL OTS quang cao manh kha nang noi WMS/TMS/HRM. Khach da xai MISA
-- hay phan mem van tai roi thi ho khong bo di — ho can he thong minh NOI
-- duoc vao cai dang co. Khong co API thi mat luon nhom khach do.
--
-- MaKH khac NULL = khoa CHI thay du lieu cua dung khach hang do.
-- =====================================================================

IF OBJECT_ID('ApiKey', 'U') IS NULL
CREATE TABLE ApiKey (
    MaKey        INT IDENTITY(1,1) PRIMARY KEY,
    TenUngDung   NVARCHAR(100) NOT NULL,
    Khoa         NVARCHAR(64)  NOT NULL,
    MaKH         INT           NULL,
    Quyen        NVARCHAR(10)  NULL,          -- Doc / Ghi
    KichHoat     BIT           NOT NULL DEFAULT 1,
    GhiChu       NVARCHAR(300) NULL,
    NgayTao      DATETIME      NOT NULL DEFAULT GETDATE(),
    LanGoiCuoi   DATETIME      NULL,
    SoLanGoi     INT           NOT NULL DEFAULT 0
);

-- Tra khoa phai nhanh: moi request API deu tra bang nay
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ApiKey_Khoa' AND object_id = OBJECT_ID('ApiKey'))
    CREATE UNIQUE INDEX UX_ApiKey_Khoa ON ApiKey (Khoa);
