/* ============================================================
   THEM BANG NHAN DIEN AI — bien Camera AI thanh module nghiep vu
   Luu lich su nhan dien de thong ke luu luong, canh bao un tac,
   xuat bao cao cho doanh nghiep.
   Chay 1 lan tren SmartPortDB (ca local va hosting). An toan re-run.
   ============================================================ */

IF OBJECT_ID(N'NhanDienAI', N'U') IS NULL
BEGIN
    CREATE TABLE NhanDienAI (
        MaND INT IDENTITY(1,1) PRIMARY KEY,
        ThoiGian DATETIME NOT NULL DEFAULT GETDATE(),
        Nguon NVARCHAR(20) NOT NULL DEFAULT N'AnhMau',   -- Webcam / TaiAnh / AnhMau
        TenModel VARCHAR(30) NULL,                        -- YOLO26n / YOLO26s / YOLO26m
        ViTriCamera NVARCHAR(50) NULL,                    -- Cong chinh / Bai A / Cau ben...
        TongDoiTuong INT NOT NULL DEFAULT 0,
        SoXeTai INT NOT NULL DEFAULT 0,                   -- truck + bus (xe tai / dau keo)
        SoOto INT NOT NULL DEFAULT 0,                     -- car
        SoNguoi INT NOT NULL DEFAULT 0,                   -- person
        SoTauThuyen INT NOT NULL DEFAULT 0,               -- boat
        DoTinCayTB DECIMAL(5,2) NULL,                     -- % trung binh
        ThoiGianXuLy INT NULL,                            -- ms
        MucCanhBao NVARCHAR(20) NULL DEFAULT N'Bình thường', -- Binh thuong / Chu y / Un tac
        GhiChu NVARCHAR(300) NULL
    );
    CREATE INDEX IX_NhanDienAI_ThoiGian ON NhanDienAI(ThoiGian);
    PRINT N'>> Da tao bang NhanDienAI';
END
ELSE
    PRINT N'>> Bang NhanDienAI da ton tai — bo qua';
GO

IF OBJECT_ID(N'NhanDienChiTiet', N'U') IS NULL
BEGIN
    CREATE TABLE NhanDienChiTiet (
        MaCT INT IDENTITY(1,1) PRIMARY KEY,
        MaND INT NOT NULL REFERENCES NhanDienAI(MaND) ON DELETE CASCADE,
        Nhan NVARCHAR(50) NOT NULL,        -- nhan tieng Viet hien thi
        NhanGoc VARCHAR(50) NULL,          -- nhan goc COCO
        SoLuong INT NOT NULL DEFAULT 1,
        DoTinCay DECIMAL(5,2) NULL
    );
    PRINT N'>> Da tao bang NhanDienChiTiet';
END
ELSE
    PRINT N'>> Bang NhanDienChiTiet da ton tai — bo qua';
GO

/* ---- Du lieu mau 7 ngay gan nhat de bieu do co so lieu ngay ---- */
IF NOT EXISTS (SELECT 1 FROM NhanDienAI)
BEGIN
    DECLARE @i INT = 0, @d DATETIME, @xt INT, @ot INT, @ng INT, @tt INT, @tong INT, @muc NVARCHAR(20), @ma INT;
    DECLARE @gio INT, @ngay INT;

    WHILE @i < 160
    BEGIN
        -- rai deu 7 ngay gan nhat, tap trung gio hanh chinh
        SET @ngay = @i % 7;
        SET @gio = 6 + (ABS(CHECKSUM(NEWID())) % 12);          -- 6h - 17h
        SET @d = DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 60,
                 DATEADD(HOUR, @gio, CAST(CAST(DATEADD(DAY, -@ngay, GETDATE()) AS DATE) AS DATETIME)));

        -- cao diem 8-10h va 14-16h thi nhieu xe hon
        IF (@gio BETWEEN 8 AND 10) OR (@gio BETWEEN 14 AND 16)
            SET @xt = 2 + ABS(CHECKSUM(NEWID())) % 5;          -- 2-6 xe tai
        ELSE
            SET @xt = ABS(CHECKSUM(NEWID())) % 3;              -- 0-2 xe tai

        SET @ot = ABS(CHECKSUM(NEWID())) % 4;
        SET @ng = ABS(CHECKSUM(NEWID())) % 6;
        SET @tt = CASE WHEN ABS(CHECKSUM(NEWID())) % 5 = 0 THEN 1 ELSE 0 END;
        SET @tong = @xt + @ot + @ng + @tt;

        SET @muc = CASE WHEN @xt >= 5 THEN N'Ùn tắc'
                        WHEN @xt >= 3 THEN N'Chú ý'
                        ELSE N'Bình thường' END;

        INSERT INTO NhanDienAI (ThoiGian, Nguon, TenModel, ViTriCamera, TongDoiTuong,
                                SoXeTai, SoOto, SoNguoi, SoTauThuyen, DoTinCayTB, ThoiGianXuLy, MucCanhBao)
        VALUES (@d,
                CASE ABS(CHECKSUM(NEWID())) % 3 WHEN 0 THEN N'Webcam' WHEN 1 THEN N'TaiAnh' ELSE N'AnhMau' END,
                CASE ABS(CHECKSUM(NEWID())) % 3 WHEN 0 THEN 'YOLO26n' WHEN 1 THEN 'YOLO26s' ELSE 'YOLO26m' END,
                CASE ABS(CHECKSUM(NEWID())) % 3 WHEN 0 THEN N'Cổng chính' WHEN 1 THEN N'Cổng phụ' ELSE N'Bãi container' END,
                @tong, @xt, @ot, @ng, @tt,
                70 + (ABS(CHECKSUM(NEWID())) % 26),            -- 70-95%
                180 + (ABS(CHECKSUM(NEWID())) % 420),          -- 180-600ms
                @muc);

        SET @ma = SCOPE_IDENTITY();

        IF @xt > 0 INSERT INTO NhanDienChiTiet (MaND, Nhan, NhanGoc, SoLuong, DoTinCay)
                   VALUES (@ma, N'Xe tải/đầu kéo', 'truck', @xt, 75 + (ABS(CHECKSUM(NEWID())) % 21));
        IF @ot > 0 INSERT INTO NhanDienChiTiet (MaND, Nhan, NhanGoc, SoLuong, DoTinCay)
                   VALUES (@ma, N'Ô tô', 'car', @ot, 70 + (ABS(CHECKSUM(NEWID())) % 26));
        IF @ng > 0 INSERT INTO NhanDienChiTiet (MaND, Nhan, NhanGoc, SoLuong, DoTinCay)
                   VALUES (@ma, N'Người', 'person', @ng, 72 + (ABS(CHECKSUM(NEWID())) % 24));
        IF @tt > 0 INSERT INTO NhanDienChiTiet (MaND, Nhan, NhanGoc, SoLuong, DoTinCay)
                   VALUES (@ma, N'Tàu/thuyền', 'boat', @tt, 68 + (ABS(CHECKSUM(NEWID())) % 25));

        SET @i = @i + 1;
    END
    PRINT N'>> Da tao 160 ban ghi nhan dien mau (7 ngay)';
END
ELSE
    PRINT N'>> Bang NhanDienAI da co du lieu — bo qua seed';
GO

SELECT MucCanhBao, COUNT(*) AS SoLan, SUM(SoXeTai) AS TongXeTai
FROM NhanDienAI GROUP BY MucCanhBao;
GO
