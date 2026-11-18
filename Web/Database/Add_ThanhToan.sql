/* ============================================================
   THEM BANG THANH TOAN — module thanh toan SmartPort
   Chay 1 lan tren CSDL SmartPortDB (ca may local va hosting).
   An toan: co kiem tra ton tai truoc khi tao.
   ============================================================ */

IF OBJECT_ID(N'ThanhToan', N'U') IS NULL
BEGIN
    CREATE TABLE ThanhToan (
        MaTT INT IDENTITY(1,1) PRIMARY KEY,
        MaBooking INT NOT NULL REFERENCES DatDichVu(MaBooking) ON DELETE CASCADE,
        PhuongThuc NVARCHAR(30) NOT NULL,          -- ChuyenKhoan / TheNganHang / ViDienTu / TienMat
        SoTien DECIMAL(14,0) NOT NULL DEFAULT 0,
        NoiDungCK VARCHAR(50) NULL,                -- noi dung chuyen khoan (SP + so booking)
        TrangThai NVARCHAR(30) NOT NULL DEFAULT N'Chờ thanh toán',
        -- Cho thanh toan / Da thanh toan / Da huy / Qua han
        MaGiaoDich VARCHAR(50) NULL,
        NgayTao DATETIME NOT NULL DEFAULT GETDATE(),
        NgayThanhToan DATETIME NULL,
        GhiChu NVARCHAR(300) NULL
    );
    PRINT N'>> Da tao bang ThanhToan';
END
ELSE
    PRINT N'>> Bang ThanhToan da ton tai — bo qua';
GO

/* ---- Du lieu mau: gan giao dich cho cac booking da hoan thanh ---- */
IF NOT EXISTS (SELECT 1 FROM ThanhToan)
BEGIN
    DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);

    INSERT INTO ThanhToan (MaBooking, PhuongThuc, SoTien, NoiDungCK, TrangThai, MaGiaoDich, NgayTao, NgayThanhToan, GhiChu)
    SELECT b.MaBooking,
           CASE (b.MaBooking % 4)
                WHEN 0 THEN N'ChuyenKhoan'
                WHEN 1 THEN N'TheNganHang'
                WHEN 2 THEN N'ViDienTu'
                ELSE N'TienMat' END,
           b.TongTien,
           'SP' + b.MaSoBooking,
           CASE WHEN b.TrangThai = N'Hoàn thành' THEN N'Đã thanh toán'
                WHEN b.TrangThai = N'Đã hủy' THEN N'Đã hủy'
                ELSE N'Chờ thanh toán' END,
           CASE WHEN b.TrangThai = N'Hoàn thành'
                THEN 'TT' + RIGHT('00000' + CAST(b.MaBooking AS VARCHAR(5)), 5) + '001'
                ELSE NULL END,
           b.NgayTao,
           CASE WHEN b.TrangThai = N'Hoàn thành' THEN DATEADD(HOUR, 3, b.NgayTao) ELSE NULL END,
           NULL
    FROM DatDichVu b;

    PRINT N'>> Da tao du lieu thanh toan mau';
END
ELSE
    PRINT N'>> Bang ThanhToan da co du lieu — bo qua seed';
GO

SELECT TrangThai, COUNT(*) AS SoGiaoDich, SUM(SoTien) AS TongTien
FROM ThanhToan GROUP BY TrangThai;
GO
