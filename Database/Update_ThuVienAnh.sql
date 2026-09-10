/* Cap nhat v3: bang CauHinhAnh - thu vien anh quan ly duoc tu Admin */
USE SmartPortDB;
GO
IF OBJECT_ID('CauHinhAnh') IS NULL
BEGIN
    CREATE TABLE CauHinhAnh (
        Khoa VARCHAR(30) NOT NULL PRIMARY KEY,
        Url VARCHAR(500) NOT NULL,
        MoTa NVARCHAR(100) NULL,
        ThuTu INT NOT NULL DEFAULT 0
    );
END
GO
-- Seed / cap nhat gia tri mac dinh
MERGE CauHinhAnh AS t
USING (VALUES
    ('hero',   'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1800&auto=format&fit=crop', N'Nền hero + slide 1 (tàu container từ trên cao)', 1),
    ('slide2', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1800&auto=format&fit=crop', N'Hero slide 2 (cẩu STS làm hàng)', 2),
    ('slide3', 'https://images.unsplash.com/photo-1502920514313-52581002a659?q=80&w=1800&auto=format&fit=crop', N'Hero slide 3 (tàu trên biển)', 3),
    ('slide4', 'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1800&auto=format&fit=crop', N'Hero slide 4 (cầu bến hoàng hôn)', 4),
    ('head',   'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1600&auto=format&fit=crop', N'Banner các trang con', 5),
    ('about',  'https://images.unsplash.com/photo-1493946740644-2d8a1f1a6aff?q=80&w=1200&auto=format&fit=crop', N'Trang giới thiệu', 6),
    ('g1', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 1 (cẩu bờ)', 11),
    ('g2', 'https://images.unsplash.com/photo-1516216628859-9bccecab13ca?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 2 (cảng đêm)', 12),
    ('g3', 'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 3 (aerial)', 13),
    ('g4', 'https://images.unsplash.com/photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 4 (bãi cont)', 14),
    ('g5', 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 5 (tàu biển)', 15),
    ('g6', 'https://images.unsplash.com/photo-1519452635265-7b1fbfd1e4e0?q=80&w=1200&auto=format&fit=crop', N'Tin tức mẫu 6 (boong tàu)', 16),
    ('gal1', 'https://images.unsplash.com/photo-1494412574643-ff11b0a5c1c3?q=80&w=1200&auto=format&fit=crop', N'Thư viện 1 — tàu mẹ cập cảng', 21),
    ('gal2', 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop', N'Thư viện 2 — cẩu STS', 22),
    ('gal3', 'https://images.unsplash.com/photo-1524055988636-436cfa46e59e?q=80&w=1200&auto=format&fit=crop', N'Thư viện 3 — bãi container', 23),
    ('gal4', 'https://images.unsplash.com/photo-1553413077-190dd305871c?q=80&w=1200&auto=format&fit=crop', N'Thư viện 4 — kho CFS', 24),
    ('gal5', 'https://images.unsplash.com/photo-1519003722824-194d4455a60c?q=80&w=1200&auto=format&fit=crop', N'Thư viện 5 — đội xe', 25),
    ('gal6', 'https://images.unsplash.com/photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop', N'Thư viện 6 — bãi container AI', 26),
    ('biz1', 'https://images.unsplash.com/photo-1600880292203-757bb62b4baf?q=80&w=800&auto=format&fit=crop', N'Doanh nghiệp 1 (họp đối tác)', 31),
    ('biz2', 'https://images.unsplash.com/photo-1521791136064-7986c2920216?q=80&w=800&auto=format&fit=crop', N'Doanh nghiệp 2 (bắt tay hợp tác)', 32),
    ('cta',  'https://images.unsplash.com/photo-1502920514313-52581002a659?q=80&w=1600&auto=format&fit=crop', N'Nền băng CTA cuối trang', 33)
) AS s(Khoa, Url, MoTa, ThuTu)
ON t.Khoa = s.Khoa
WHEN NOT MATCHED THEN INSERT (Khoa, Url, MoTa, ThuTu) VALUES (s.Khoa, s.Url, s.MoTa, s.ThuTu);
GO
SELECT COUNT(*) AS SoAnh FROM CauHinhAnh;
