-- =====================================================================
-- DU LIEU DEMO CO LIEN KET — de man hinh "Ho so container 360" va
-- "Ho so xe" co gi ma xem khi di thuyet trinh / demo cho khach.
--
-- Tao mot cau chuyen HOAN CHINH cho 1 container:
--   lenh ha bai -> xe vao cong -> can xe -> luu bai -> lenh giao ->
--   xe ra cong -> phieu sua chua vo -> chung tu
-- va vai lenh le te cho cac cont khac de danh sach khong trong tron.
--
-- AN TOAN: chi THEM, khong xoa gi. Moi ban ghi deu co GhiChu 'DEMO-LK'
-- de go bo sach se bang Xoa_DemoLienKet.sql khi ban giao that.
-- =====================================================================

DECLARE @cont NVARCHAR(11), @maCont INT, @maKH INT, @xe NVARCHAR(15) = '51C-638.72';
SELECT TOP 1 @maCont = MaCont, @cont = SoCont, @maKH = MaKH
FROM Container WHERE MaKH IS NOT NULL ORDER BY MaCont DESC;

IF @cont IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LenhGiaoNhan WHERE GhiChu LIKE '%DEMO-LK%')
BEGIN
    DECLARE @t0 DATETIME = DATEADD(day, -9, GETDATE());   -- luc ha bai
    DECLARE @t1 DATETIME = DATEADD(hour, -3, GETDATE());  -- luc giao khach

    -- 1) Lenh ha container vao bai
    INSERT INTO LenhGiaoNhan (SoLenh, LoaiLenh, MaKH, SoCont, BienSoXe, TenTaiXe, SdtTaiXe,
                              HenTuLuc, HenDenLuc, MaPin, TrangThai, GhiChu, NgayTao, VaoCongLuc, HoanTatLuc, DaCheckIn)
    VALUES ('LGN-DEMO-001', 'HaCont', @maKH, @cont, @xe, N'Nguyễn Văn Bảy', '0913456789',
            DATEADD(hour,-1,@t0), DATEADD(hour,3,@t0), '204815', N'Hoàn tất', 'DEMO-LK',
            DATEADD(day,-10,GETDATE()), @t0, DATEADD(minute,40,@t0), 1);
    DECLARE @l1 INT = SCOPE_IDENTITY();

    -- 2) Xe vao cong cho container do (co gan SoCont + MaLenh -> day la mat xich)
    INSERT INTO XeRaVao (BienSo, Cong, HuongDi, LoaiXe, DoTinCay, Nguon, ThoiGian, GhiChu, ViTriDo, SoCont, MaLenh)
    VALUES (@xe, 'A1', N'Vào', N'Đầu kéo container', 96.4, 'ALPR', @t0, N'Khớp lệnh LGN-DEMO-001 · DEMO-LK', 'D3', @cont, @l1);

    -- 3) Can xe vao/ra -> ra khoi luong hang (VGM)
    INSERT INTO PhieuCan (SoPhieu, MaLenh, BienSoXe, SoCont, CanLan1, CanLan2, Luc1, Luc2, NguoiCan, GhiChu)
    VALUES ('PC-DEMO-001', @l1, @xe, @cont, 32450, 12180,
            DATEADD(minute,6,@t0), DATEADD(minute,34,@t0), N'Trạm cân số 1', 'DEMO-LK');

    -- 4) Xe ra cong (xe khong, cont da nam lai bai)
    INSERT INTO XeRaVao (BienSo, Cong, HuongDi, LoaiXe, DoTinCay, Nguon, ThoiGian, GhiChu, SoCont, MaLenh)
    VALUES (@xe, 'A1', N'Ra', N'Đầu kéo container', 94.8, 'ALPR', DATEADD(minute,42,@t0), N'DEMO-LK', @cont, @l1);

    -- 5) Lenh giao container cho khach — xe khac den lay
    INSERT INTO LenhGiaoNhan (SoLenh, LoaiLenh, MaKH, SoCont, BienSoXe, TenTaiXe, SdtTaiXe,
                              HenTuLuc, HenDenLuc, MaPin, TrangThai, GhiChu, NgayTao, VaoCongLuc, DaCheckIn)
    VALUES ('LGN-DEMO-002', 'GiaoCont', @maKH, @cont, '61H-777.88', N'Trần Minh Đức', '0987654321',
            DATEADD(hour,-4,GETDATE()), DATEADD(hour,4,GETDATE()), '771302', N'Đã vào cổng', 'DEMO-LK',
            DATEADD(day,-1,GETDATE()), @t1, 1);
    DECLARE @l2 INT = SCOPE_IDENTITY();

    INSERT INTO XeRaVao (BienSo, Cong, HuongDi, LoaiXe, DoTinCay, Nguon, ThoiGian, GhiChu, ViTriDo, SoCont, MaLenh)
    VALUES ('61H-777.88', 'A2', N'Vào', N'Đầu kéo container', 91.2, 'ALPR', @t1, N'Khớp lệnh LGN-DEMO-002 · DEMO-LK', 'D1', @cont, @l2);

    -- 6) Phieu sua chua vo — phat hien mop vach ben trai khi kiem tra
    IF OBJECT_ID('PhieuSuaChua', 'U') IS NOT NULL
    INSERT INTO PhieuSuaChua (SoEOR, MaCont, SoCont, MaHangTau, ViTriHuHong, MaHuHong, MoTa,
                              GioCong, ChiPhiVatTu, ChiPhiUocTinh, TrangThai, NguoiLap, NgayLap, GhiChu)
    SELECT 'EOR-DEMO-01', @maCont, @cont, (SELECT TOP 1 MaHangTau FROM HangTau ORDER BY MaHangTau),
           N'Vách bên trái', 'DT', N'Móp lõm khoảng 30cm, không thủng',
           2.5, 180000, 555000, N'Chờ duyệt', N'Xưởng sửa chữa', DATEADD(day,-2,GETDATE()), 'DEMO-LK';

    -- 7) Vai lenh le te cho cac container khac
    INSERT INTO LenhGiaoNhan (SoLenh, LoaiLenh, MaKH, SoCont, BienSoXe, TenTaiXe, SdtTaiXe,
                              HenTuLuc, HenDenLuc, MaPin, TrangThai, GhiChu, NgayTao, DaCheckIn)
    SELECT TOP 3
        'LGN-DEMO-' + RIGHT('00' + CAST(100 + ROW_NUMBER() OVER (ORDER BY c.MaCont) AS NVARCHAR(5)), 3),
        'LayRong', c.MaKH, c.SoCont,
        CASE ROW_NUMBER() OVER (ORDER BY c.MaCont) WHEN 1 THEN '50H-246.80' WHEN 2 THEN '15C-188.47' ELSE '43A-555.19' END,
        N'Tài xế hợp đồng', '0900000000',
        DATEADD(hour, 1, GETDATE()), DATEADD(hour, 6, GETDATE()),
        RIGHT(CAST(ABS(CHECKSUM(NEWID())) AS NVARCHAR(10)) + '000000', 6),
        N'Chờ xe', 'DEMO-LK', GETDATE(), 0
    FROM Container c
    WHERE c.MaCont <> @maCont AND c.TrangThai = N'Trong bãi'
    ORDER BY c.MaCont DESC;
END
