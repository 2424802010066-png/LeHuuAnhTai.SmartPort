-- ============================================================
-- DU LIEU MAU CHO XU LY SU CO (bang SuCo — chay Add_SuCo.sql truoc). Idempotent: moi dong kiem theo TieuDe, chay lai khong chen trung.
-- Nguoi bao lay tu tai khoan SAN CO theo vai tro (uu tien admin / dieudo / taile / thuha), KHONG doi mat khau ai.
-- Thoi diem tinh lui tu luc chay script de man hinh luon "moi".
-- ============================================================
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SuCo', N'U') IS NULL
BEGIN
    RAISERROR(N'Chua co bang SuCo — chay Database/Add_SuCo.sql truoc.', 16, 1);
    RETURN;
END

DECLARE @admin INT = (SELECT TOP 1 MaTK FROM dbo.TaiKhoan WHERE VaiTro = N'Admin' AND KichHoat = 1 AND TenDangNhap NOT LIKE N'kiemthu%' ORDER BY MaTK);
DECLARE @nv INT = COALESCE(
    (SELECT MaTK FROM dbo.TaiKhoan WHERE TenDangNhap = N'dieudo' AND VaiTro = N'NhanVien'),
    (SELECT TOP 1 MaTK FROM dbo.TaiKhoan WHERE VaiTro = N'NhanVien' AND KichHoat = 1 AND TenDangNhap NOT LIKE N'kiemthu%' ORDER BY MaTK),
    @admin);
DECLARE @kh1 INT = COALESCE(
    (SELECT MaTK FROM dbo.TaiKhoan WHERE TenDangNhap = N'taile' AND VaiTro = N'KhachHang'),
    (SELECT TOP 1 MaTK FROM dbo.TaiKhoan WHERE VaiTro = N'KhachHang' AND MaKH IS NOT NULL AND TenDangNhap NOT LIKE N'kiemthu%' ORDER BY MaTK));
DECLARE @kh2 INT = COALESCE(
    (SELECT MaTK FROM dbo.TaiKhoan WHERE TenDangNhap = N'thuha' AND VaiTro = N'KhachHang'),
    (SELECT TOP 1 MaTK FROM dbo.TaiKhoan WHERE VaiTro = N'KhachHang' AND MaKH IS NOT NULL AND TenDangNhap NOT LIKE N'kiemthu%' ORDER BY MaTK DESC));

IF @admin IS NULL OR @nv IS NULL OR @kh1 IS NULL OR @kh2 IS NULL
BEGIN
    PRINT N'Thieu tai khoan Admin / NhanVien / KhachHang — bo qua du lieu mau su co.';
    RETURN;
END

DECLARE @maKH1 INT = (SELECT MaKH FROM dbo.TaiKhoan WHERE MaTK = @kh1);
DECLARE @maKH2 INT = (SELECT MaKH FROM dbo.TaiKhoan WHERE MaTK = @kh2);
-- Booking da thanh toan cua khach 1 (cho phan anh hoa don); container trong bai + lenh cua khach 2
DECLARE @booking NVARCHAR(30) = COALESCE(
    (SELECT TOP 1 MaSoBooking FROM dbo.DatDichVu WHERE MaKH = @maKH1 AND TrangThai = N'Hoàn thành' ORDER BY MaBooking DESC),
    (SELECT TOP 1 MaSoBooking FROM dbo.DatDichVu WHERE MaKH = @maKH1 ORDER BY MaBooking DESC), N'BK260008');
DECLARE @contHu NVARCHAR(15) = COALESCE(
    (SELECT TOP 1 SoCont FROM dbo.Container WHERE MaKH = @maKH2 AND SoCont = N'MSCU2299876'),
    (SELECT TOP 1 c.SoCont FROM dbo.Container c WHERE c.MaKH = @maKH2 AND c.TrangThai = N'Trong bãi'
        AND NOT EXISTS (SELECT 1 FROM dbo.LenhGiaoNhan l WHERE l.SoCont = c.SoCont AND l.TrangThai IN (N'Chờ xe', N'Đã vào cổng'))
        ORDER BY c.MaCont));
DECLARE @lenh INT = COALESCE(
    (SELECT TOP 1 MaLenh FROM dbo.LenhGiaoNhan WHERE MaKH = @maKH2 AND SoLenh = N'LGN-DEMO-002'),
    (SELECT TOP 1 MaLenh FROM dbo.LenhGiaoNhan WHERE MaKH = @maKH2 ORDER BY MaLenh DESC));
DECLARE @contLenh NVARCHAR(15) = (SELECT SoCont FROM dbo.LenhGiaoNhan WHERE MaLenh = @lenh);
DECLARE @scLua INT = (SELECT TOP 1 MaSC FROM dbo.SuCoAnNinh WHERE Loai = N'Lua' ORDER BY MaSC);
DECLARE @bayGio DATETIME2 = SYSDATETIME();
DECLARE @t DATETIME2;
DECLARE @so INT = 0;

-- 1) Hang hoa, thiet bi — DA DONG co ket qua
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Cẩu RTG-03 rò rỉ dầu thủy lực tại block B')
BEGIN
    SET @t = DATEADD(HOUR, -74, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, MaTKBao, MaTKXuLy, TrangThai, KetQua, ThoiDiemBao, ThoiDiemTiepNhan, ThoiDiemDong)
    VALUES (N'HangHoaThietBi', N'TrungBinh', N'Cẩu RTG-03 rò rỉ dầu thủy lực tại block B',
            N'Tài xế cẩu phát hiện vệt dầu dưới chân cẩu RTG-03 khi nâng container 40'' ở hàng B-07; đồng hồ áp suất dao động. Đã dừng cẩu, khoanh vùng 10 m.',
            N'Bãi container – Block B', @nv, @nv, N'Dong',
            N'Đội kỹ thuật thay ống dầu thủy lực cụm nâng, thử tải 40 tấn đạt; vệ sinh vết dầu bằng vật liệu thấm. RTG-03 hoạt động lại sau 3 giờ 40 phút.',
            @t, DATEADD(MINUTE, 12, @t), DATEADD(MINUTE, 236, @t));
    SET @so += 1;
END

-- 2) Hang hoa — container moP vach, DANG TAM GIU (QT15: cong khong giao ra)
IF @contHu IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Container ' + @contHu + N' móp vách hông, rách seal')
BEGIN
    SET @t = DATEADD(MINUTE, -310, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, SoContainer, MaTKBao, MaTKXuLy, TrangThai, TamGiuContainer, ThoiDiemBao, ThoiDiemTiepNhan)
    VALUES (N'HangHoaThietBi', N'TrungBinh', N'Container ' + @contHu + N' móp vách hông, rách seal',
            N'Kiểm đếm AI và nhân viên hiện trường ghi nhận vách hông trái móp sâu khoảng 15 cm, seal số hiệu bị rách. Đã chụp ảnh 4 mặt, chờ giám định và báo chủ hàng.',
            N'Bãi container – Block C, hàng C-03', @contHu, @nv, @nv, N'DangXuLy', 1, @t, DATEADD(MINUTE, 4, @t));
    SET @so += 1;
END

-- 3) An ninh — camera AI bao chay tai Kho CFS, ket luan BAO GIA
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Camera AI báo cháy tại Kho CFS')
BEGIN
    SET @t = DATEADD(HOUR, -50, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, MaTKBao, MaTKXuLy, TrangThai, KetQua, ThoiDiemBao, ThoiDiemTiepNhan, ThoiDiemDong, MaSCAnNinh)
    VALUES (N'AnNinhAnToan', N'NguyHiem', N'Camera AI báo cháy tại Kho CFS',
            N'Camera CFS-02 phát hiện vùng sáng/nhiệt bất thường gần cửa kho số 2, độ tin cậy 71%. Hệ thống tự chuyển cảnh báo AI thành sự cố và phát báo động toàn cảng.',
            N'Kho CFS – cửa số 2', @admin, @nv, N'BaoGia',
            N'Báo giả: đèn pha xe nâng chiếu thẳng vào camera CFS-02 tạo vùng sáng giống ngọn lửa; đội an ninh kiểm tra tại chỗ sau 3 phút, không có cháy. Đã chỉnh góc camera.',
            @t, DATEADD(MINUTE, 3, @t), DATEADD(MINUTE, 16, @t), @scLua);
    SET @so += 1;
END

-- 4) Thanh toan — khach khieu nai hoa don tinh trung phi, DANG XU LY
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Hóa đơn booking ' + @booking + N' tính trùng phí nâng hạ')
BEGIN
    SET @t = DATEADD(MINUTE, -380, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, MaTKBao, MaTKXuLy, TrangThai, ThoiDiemBao, ThoiDiemTiepNhan)
    VALUES (N'ThanhToan', N'TrungBinh', N'Hóa đơn booking ' + @booking + N' tính trùng phí nâng hạ',
            N'Hóa đơn điện tử của booking ' + @booking + N' có 2 dòng "Phí nâng hạ container 40''" cùng ngày, tổng tiền dư 850.000 đ so với báo giá. Đề nghị cảng kiểm tra và xuất hóa đơn điều chỉnh.',
            @kh1, @nv, N'DangXuLy', @t, DATEADD(MINUTE, 25, @t));
    SET @so += 1;
END

-- 5) Dich vu — khach phan anh xe cho cong lau, MOI (chua tra loi, han 24 gio)
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Xe chờ ở cổng hơn 2 giờ dù đã đặt lịch hẹn')
BEGIN
    SET @t = DATEADD(MINUTE, -95, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, SoContainer, MaLenh, MaTKBao, TrangThai, ThoiDiemBao)
    VALUES (N'DichVu', N'Thap', N'Xe chờ ở cổng hơn 2 giờ dù đã đặt lịch hẹn',
            N'Xe đến đúng khung giờ hẹn TAS nhưng phải xếp hàng ngoài cổng hơn 2 giờ mới được vào lấy hàng. Đề nghị cảng cho biết nguyên nhân và cách ưu tiên xe đã đặt lịch.',
            N'Cổng chính', @contLenh, @lenh, @kh2, N'Moi', @t);
    SET @so += 1;
END

-- 6) He thong — mat ket noi camera ALPR, DA XU LY (cho dong)
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Mất kết nối camera ALPR cổng phụ')
BEGIN
    SET @t = DATEADD(HOUR, -26, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, MaTKBao, MaTKXuLy, TrangThai, KetQua, ThoiDiemBao, ThoiDiemTiepNhan)
    VALUES (N'HeThong', N'TrungBinh', N'Mất kết nối camera ALPR cổng phụ',
            N'Màn hình cổng phụ báo mất luồng camera ALPR làn 2; trạm cổng tự chuyển sang chế độ ngoại tuyến, nhân viên nhập biển số tay.',
            N'Cổng phụ – làn 2', @nv, @nv, N'DaXuLy',
            N'Khởi động lại switch PoE tủ mạng cổng phụ, camera lên lại sau 18 phút. Trạm đã đồng bộ 14 lượt ghi trễ, 0 lượt cần đối soát.',
            @t, DATEADD(MINUTE, 6, @t));
    SET @so += 1;
END

-- 7) An toan — nguoi di bo vao lan xe nang, NGUY HIEM dang xu ly (tiep nhan sau 2 phut — dat QT14)
IF NOT EXISTS (SELECT 1 FROM dbo.SuCo WHERE TieuDe = N'Người đi bộ vào làn xe nâng khu B')
BEGIN
    SET @t = DATEADD(MINUTE, -42, @bayGio);
    INSERT INTO dbo.SuCo (Loai, MucDo, TieuDe, MoTa, ViTri, MaTKBao, MaTKXuLy, TrangThai, ThoiDiemBao, ThoiDiemTiepNhan)
    VALUES (N'AnNinhAnToan', N'NguyHiem', N'Người đi bộ vào làn xe nâng khu B',
            N'Công nhân không mặc áo phản quang đi tắt qua làn xe nâng reach stacker ở khu B trong lúc xe đang chạy lùi; tài xế phanh kịp. Cần nhắc nhở và rà soát rào chắn.',
            N'Bãi container – Block B', @nv, @nv, N'DangXuLy', @t, DATEADD(MINUTE, 2, @t));
    SET @so += 1;
END

PRINT N'Da them ' + CAST(@so AS NVARCHAR(10)) + N' su co mau.';
SELECT MaSuCo, Loai, MucDo, TrangThai, TamGiuContainer, LEFT(TieuDe, 60) AS TieuDe FROM dbo.SuCo ORDER BY MaSuCo;
