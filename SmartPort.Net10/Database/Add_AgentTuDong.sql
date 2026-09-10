-- =====================================================================
-- AGENT TU DONG — tu PERCEIVE→REASON→PLAN len den ACT
--
-- Agent cu (AgentService) chi DE XUAT: moi muc la mot duong dan de nguoi
-- bam. Do la tro ly canh bao, khong phai tac tu. Ban ban nay cho agent
-- THUC SU LAM viec tren he thong cua chinh no: lap phieu thu, sinh ban
-- tin EDI, mo su co...
--
-- BA NGUYEN TAC BAT BUOC (vi agent dung toi TIEN):
--  1. Quyet dinh bang LUAT, khong bang mo hinh ngon ngu — tien khong duoc
--     phep "co ve dung". LLM neu co chi dung de VIET NOI DUNG.
--  2. Moi hanh dong ghi lai LY DO + HOAN TAC duoc.
--  3. Moi ky nang co 3 che do: Tat / Xin phep / Tu lam. Mac dinh XIN PHEP.
-- =====================================================================

IF OBJECT_ID('CauHinhAgent', 'U') IS NULL
CREATE TABLE CauHinhAgent (
    MaCH       INT IDENTITY(1,1) PRIMARY KEY,
    KyNang     NVARCHAR(40)  NOT NULL,
    TenHienThi NVARCHAR(150) NULL,
    MoTa       NVARCHAR(400) NULL,
    CheDo      NVARCHAR(20)  NOT NULL DEFAULT N'Xin phép',  -- Tắt / Xin phép / Tự làm
    Nguong     INT           NOT NULL DEFAULT 0,            -- ngưỡng ngày/số lượng tuỳ kỹ năng
    ToiDaMoiLan INT          NOT NULL DEFAULT 20,           -- chặn agent lỗi tạo hàng trăm bản ghi
    LanChayCuoi DATETIME     NULL,
    NgayTao    DATETIME      NOT NULL DEFAULT GETDATE()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CauHinhAgent_KyNang' AND object_id = OBJECT_ID('CauHinhAgent'))
    CREATE UNIQUE INDEX UX_CauHinhAgent_KyNang ON CauHinhAgent (KyNang);

IF OBJECT_ID('NhatKyAgent', 'U') IS NULL
CREATE TABLE NhatKyAgent (
    MaNK        INT IDENTITY(1,1) PRIMARY KEY,
    Luc         DATETIME      NOT NULL DEFAULT GETDATE(),
    KyNang      NVARCHAR(40)  NOT NULL,
    DoiTuong    NVARCHAR(100) NULL,        -- số cont / biển số / số phiếu...
    LyDo        NVARCHAR(600) NULL,        -- chuỗi suy luận, hiện thẳng cho người đọc
    HanhDong    NVARCHAR(400) NULL,        -- agent đã làm gì
    KetQua      NVARCHAR(400) NULL,
    -- Chờ duyệt / Đã làm / Đã hủy / Lỗi
    TrangThai   NVARCHAR(20)  NOT NULL DEFAULT N'Chờ duyệt',
    TuDong      BIT           NOT NULL DEFAULT 0,   -- agent tự làm hay người bấm duyệt
    BangThamChieu NVARCHAR(40) NULL,               -- bảng + khoá để HOÀN TÁC
    MaThamChieu INT           NULL,
    DaHoanTac   BIT           NOT NULL DEFAULT 0,
    NguoiXuLy   NVARCHAR(50)  NULL,
    LucXuLy     DATETIME      NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhatKyAgent_Luc' AND object_id = OBJECT_ID('NhatKyAgent'))
    CREATE INDEX IX_NhatKyAgent_Luc ON NhatKyAgent (Luc DESC, KyNang);

-- Khai bao 7 ky nang. Mac dinh XIN PHEP — khong ai muon phan mem moi cai
-- xong da tu dong lap phieu doi tien khach ngay dem dau tien.
IF NOT EXISTS (SELECT 1 FROM CauHinhAgent)
BEGIN
    INSERT INTO CauHinhAgent (KyNang, TenHienThi, MoTa, CheDo, Nguong, ToiDaMoiLan) VALUES
    ('ChotPhiLuuBai', N'Chốt phí lưu bãi quá hạn',
     N'Container đã qua ngày miễn phí mà chưa ai lập phiếu → agent tự lập phiếu thu.', N'Xin phép', 0, 20),
    ('SinhCodeco', N'Sinh CODECO báo hãng tàu',
     N'Hãng tàu có từ N lượt container vào/ra chưa báo cáo → agent tự sinh bản tin EDI.', N'Xin phép', 5, 5),
    ('NhacNoQuaHan', N'Nhắc nợ phiếu quá hạn',
     N'Phiếu thu treo quá N ngày chưa thu được → agent soạn nội dung nhắc khách.', N'Xin phép', 30, 20),
    ('CanhBaoTheXe', N'Cảnh báo thẻ xe sắp hết',
     N'Thẻ tháng sắp hết hạn hoặc thẻ trả trước sắp cạn tiền → nhắc thu ngân gọi chủ xe.', N'Xin phép', 7, 20),
    ('GiucDuyetEor', N'Giục hãng tàu duyệt EOR',
     N'Phiếu sửa chữa vỏ chờ duyệt quá N ngày → agent nhắc gửi lại hãng tàu.', N'Xin phép', 7, 20),
    ('CanhBaoTonLau', N'Cảnh báo container tồn lâu',
     N'Container nằm bãi quá N ngày → đề nghị gọi khách lấy hàng ra.', N'Xin phép', 30, 20),
    ('ChanGplxHetHan', N'Phát hiện tài xế hết hạn GPLX',
     N'Tài xế có bằng lái đã hết hạn mà vẫn qua cổng → agent mở sự cố an ninh.', N'Xin phép', 0, 20);
END
