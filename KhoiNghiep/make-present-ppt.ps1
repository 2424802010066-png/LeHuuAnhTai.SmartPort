# ============================================================
# BAI THUYET TRINH KHOI NGHIEP DAY DU — SmartPort (18 slide 16:9)
# Ban mo rong tu pitch deck: them agenda, 5 slide demo module,
# kien truc cong nghe — theo mach ke chuyen cua buoi thi.
# ============================================================
$ErrorActionPreference = 'Stop'
$outDir = "C:\Users\ASUS\Downloads\SmartPort\KhoiNghiep"
$imgDir = "C:\Users\ASUS\Downloads\SmartPort\anh-tinhnang"
$outPptx = Join-Path $outDir "ThuyetTrinh_KhoiNghiep_SmartPort.pptx"
$outPdf  = Join-Path $outDir "ThuyetTrinh_KhoiNghiep_SmartPort.pdf"

$NAVY   = 0x572F0A
$NAVY2  = 0x7A4A15
$ORANGE = 0x2D7DFF
$WHITE  = 0xFFFFFF
$DARKTXT= 0x33221A
$GRAY   = 0x8A7A6E

$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(0)
$pres.PageSetup.SlideWidth = 960
$pres.PageSetup.SlideHeight = 540
$W = 960; $H = 540

function NewSlide($dark) {
    $s = $pres.Slides.Add($pres.Slides.Count + 1, 12)
    $bg = $s.Shapes.AddShape(1, 0, 0, $W, $H)
    $bg.Line.Visible = 0
    if ($dark) { $bg.Fill.ForeColor.RGB = $NAVY } else { $bg.Fill.ForeColor.RGB = 0xF7F3EF }
    return $s
}
function TB($s, $x, $y, $w, $h, $text, $size, $color, $bold, $align) {
    $tb = $s.Shapes.AddTextbox(1, $x, $y, $w, $h)
    $tf = $tb.TextFrame
    $tf.WordWrap = -1
    $r = $tf.TextRange
    $r.Text = [string]$text
    $r.Font.Name = 'Segoe UI'
    $r.Font.Size = $size
    $r.Font.Bold = $(if ($bold) { -1 } else { 0 })
    $r.Font.Color.RGB = $color
    $r.ParagraphFormat.Alignment = $align
    return $tb
}
function Chip($s, $x, $y, $w, $h, $text, $fill, $txtColor, $size) {
    $sh = $s.Shapes.AddShape(5, $x, $y, $w, $h)
    $sh.Fill.ForeColor.RGB = $fill
    $sh.Line.Visible = 0
    $tr = $sh.TextFrame.TextRange
    $tr.Text = [string]$text
    $tr.Font.Name = 'Segoe UI'
    $tr.Font.Size = $size
    $tr.Font.Bold = -1
    $tr.Font.Color.RGB = $txtColor
    return $sh
}
function Pic($s, $file, $x, $y, $w) {
    $path = Join-Path $imgDir $file
    if (-not (Test-Path $path)) { return $null }
    $p = $s.Shapes.AddPicture($path, 0, -1, $x, $y)
    $ratio = $p.Height / $p.Width
    $p.Width = $w
    $p.Height = $w * $ratio
    $p.Line.Visible = -1
    $p.Line.ForeColor.RGB = $NAVY
    $p.Line.Weight = 1.5
    $p.Shadow.Type = 25
    return $p
}
function Footer($s, $num) {
    TB $s 870 512 80 22 ("SmartPort | " + $num) 10 $GRAY $false 2 | Out-Null
}
function TitleBar($s, $text) {
    TB $s 50 30 860 46 $text 28 $NAVY $true 1 | Out-Null
    $ln = $s.Shapes.AddShape(1, 52, 78, 120, 4)
    $ln.Fill.ForeColor.RGB = $ORANGE; $ln.Line.Visible = 0
}
# Slide demo: anh lon trai + tieu de + 3 bullet phai
function DemoSlide($num, $badge, $title, $img, $bullets, $note) {
    $s = NewSlide $false
    Chip $s 50 28 150 36 $badge $ORANGE $WHITE 14 | Out-Null
    TB $s 215 28 695 40 $title 24 $NAVY $true 1 | Out-Null
    Pic $s $img 50 92 545 | Out-Null
    $y = 110
    foreach ($b in $bullets) {
        Chip $s 620 $y 26 26 ([string][char]0x2713) $NAVY $WHITE 12 | Out-Null
        TB $s 656 ($y - 2) 260 84 $b 14 $DARKTXT $false 1 | Out-Null
        $y += 96
    }
    Chip $s 620 ($y + 4) 296 74 $note $NAVY2 $WHITE 12 | Out-Null
    Footer $s $num
    return $s
}

# ============ 1. COVER ============
$s = NewSlide $true
$bar = $s.Shapes.AddShape(1, 0, 356, $W, 6); $bar.Fill.ForeColor.RGB = $ORANGE; $bar.Line.Visible = 0
TB $s 80 105 800 40 'BÀI THUYẾT TRÌNH DỰ ÁN KHỞI NGHIỆP' 16 $ORANGE $true 2 | Out-Null
TB $s 80 150 800 90 'SMARTPORT' 66 $WHITE $true 2 | Out-Null
TB $s 80 248 800 70 ('Nền tảng quản lý cảng và depot logistics' + [char]13 + 'tích hợp AI chạy ngay trên trình duyệt') 22 $WHITE $false 2 | Out-Null
TB $s 80 385 800 30 'Lê Hữu Anh Tài  ·  MSSV 2424802010066' 16 $WHITE $false 2 | Out-Null
TB $s 80 415 800 26 'lehuuanhtai2020tkh@gmail.com  ·  2026' 13 $GRAY $false 2 | Out-Null

# ============ 2. AGENDA ============
$s = NewSlide $false
TitleBar $s 'NỘI DUNG TRÌNH BÀY'
$ag = @(
    @('01', 'Vấn đề & cơ hội thị trường'),
    @('02', 'Giải pháp SmartPort — demo 5 module chính'),
    @('03', 'Công nghệ AI & bí quyết chi phí thấp'),
    @('04', 'Thị trường & mô hình kinh doanh'),
    @('05', 'Tài chính · lộ trình · đội ngũ'),
    @('06', 'Kêu gọi hỗ trợ đầu tư')
)
$y = 105
foreach ($a in $ag) {
    Chip $s 70 $y 64 52 $a[0] $(if ([int]$a[0] % 2 -eq 0) { $ORANGE } else { $NAVY }) $WHITE 18 | Out-Null
    TB $s 155 ($y + 8) 700 40 $a[1] 18 $DARKTXT $true 1 | Out-Null
    $y += 68
}
Footer $s 2

# ============ 3. VAN DE ============
$s = NewSlide $false
TitleBar $s 'VẤN ĐỀ: Cảng nhỏ, nỗi đau lớn'
$items = @(
    @('16–18% GDP', 'chi phí logistics Việt Nam — gấp rưỡi trung bình thế giới (tham khảo: Bộ Công Thương)'),
    @('Sổ giấy + Excel', 'công cụ của phần lớn depot: thất thoát phí lưu bãi, ùn tắc cổng, dữ liệu phân mảnh'),
    @('Hàng triệu USD', 'giá phần mềm cảng quốc tế (TOS) — ngoài tầm với doanh nghiệp vừa và nhỏ'),
    @('3–4 phần mềm rời', 'camera, kế toán, booking... không kết nối được với nhau')
)
$y = 100
foreach ($it in $items) {
    Chip $s 50 $y 190 74 $it[0] $NAVY $WHITE 17 | Out-Null
    TB $s 260 ($y + 8) 650 64 $it[1] 15 $DARKTXT $false 1 | Out-Null
    $y += 92
}
Footer $s 3

# ============ 4. CO HOI / INSIGHT ============
$s = NewSlide $true
TB $s 50 40 860 44 'KHOẢNH KHẮC CÔNG NGHỆ 2026' 28 $ORANGE $true 2 | Out-Null
TB $s 90 110 780 100 ('Lần đầu tiên, trình duyệt web chạy được mô hình AI thị giác' + [char]13 + 'nhờ WebGPU + WebAssembly + ONNX Runtime') 22 $WHITE $true 2 | Out-Null
$ins = @(
    @('TRƯỚC ĐÂY', ('AI camera cần máy chủ GPU' + [char]13 + 'hàng trăm triệu đồng'), $NAVY2),
    @('BÂY GIỜ', ('AI chạy trên chính máy tính' + [char]13 + 'của nhân viên cổng — 0 đồng GPU'), $ORANGE)
)
$x = 150
foreach ($i in $ins) {
    $box = $s.Shapes.AddShape(5, $x, 250, 310, 150)
    $box.Fill.ForeColor.RGB = $(if ($i[2] -eq $ORANGE) { $ORANGE } else { $NAVY2 })
    $box.Line.ForeColor.RGB = $WHITE; $box.Line.Weight = 1.2
    TB $s $x 268 310 34 $i[0] 17 $WHITE $true 2 | Out-Null
    TB $s ($x + 14) 308 282 84 $i[1] 14 $WHITE $false 2 | Out-Null
    $x += 350
}
TB $s 80 440 800 50 'SmartPort là nền tảng quản lý cảng Việt Nam đầu tiên khai thác triệt để lợi thế này.' 17 $WHITE $true 2 | Out-Null
Footer $s 4

# ============ 5. GIAI PHAP TONG QUAN ============
$s = NewSlide $false
TitleBar $s 'GIẢI PHÁP: 1 website — trọn nghiệp vụ + AI'
$cols = @(
    @('NGHIỆP VỤ LÕI', 'Điều độ tàu · bến · bãi container · booking trực tuyến · hóa đơn · phân quyền', $NAVY),
    @('LỚP AI KHAI THÁC', 'Camera YOLO26 · biển số ALPR · Face ID · AI Agent · dự báo · tự học', $ORANGE),
    @('THANH TOÁN & KẾ TOÁN', 'VietQR/NAPAS 247 động · đối soát · xuất chứng từ chuẩn MISA', $NAVY2)
)
$x = 50
foreach ($c in $cols) {
    $box = $s.Shapes.AddShape(5, $x, 110, 275, 290)
    $box.Fill.ForeColor.RGB = $WHITE; $box.Line.ForeColor.RGB = $c[2]; $box.Line.Weight = 2.5
    Chip $s ($x + 20) 130 235 44 $c[0] $c[2] $WHITE 15 | Out-Null
    TB $s ($x + 18) 190 240 190 $c[1] 15 $DARKTXT $false 1 | Out-Null
    $x += 293
}
TB $s 50 425 860 60 'Khách hàng chỉ cần trình duyệt web — không cài đặt, không máy chủ AI, triển khai 1–2 tuần.' 17 $NAVY $true 2 | Out-Null
Footer $s 5

# ============ 6-10. DEMO 5 MODULE ============
DemoSlide 6 'DEMO 1' 'Cổng thông minh — AI đọc biển số (ALPR)' '17-cong-thong-minh-alpr.png' @(
    'YOLO26 phát hiện xe, OCR đọc biển số Việt Nam, chuẩn hóa tự động',
    'Nhật ký ra/vào như trạm thu phí: cổng, hướng, độ tin cậy, nguồn',
    'Cảnh báo xe vào 2 lần chưa ra — chống gian lận'
) 'Thay thế 1 nhân viên ghi sổ mỗi ca trực; dữ liệu an ninh tra cứu tức thì' | Out-Null

DemoSlide 7 'DEMO 2' 'Camera AI + thống kê lưu lượng (BI)' '03-thong-ke-luu-luong.png' @(
    'Đếm xe tải, người, tàu thuyền theo thời gian thực (WebGPU tăng tốc)',
    'Tự tìm khung giờ cao điểm, cảnh báo ùn tắc từ 5 xe/khung hình',
    'Khuyến nghị tự sinh: "mở thêm làn cổng 15–16h" + xuất Excel'
) 'Giúp giám đốc khai thác quyết định bằng SỐ LIỆU thay vì cảm tính' | Out-Null

DemoSlide 8 'DEMO 3' 'Đăng nhập khuôn mặt — bảo mật 2 tầng' '14-modal-quet-khuon-mat.png' @(
    'ArcFace 512 chiều — chính xác cấp thương mại, không lưu ảnh gốc',
    'Chống giả mạo MiniFASNet: giơ ảnh/màn hình là bị chặn',
    'Vòng quét radar + phản hồi trực quan — trải nghiệm như iPhone'
) 'An toàn hơn mật khẩu dùng chung — vấn đề nhức nhối tại các cảng' | Out-Null

DemoSlide 9 'DEMO 4' 'Thanh toán VietQR + kế toán MISA' '10-thanh-toan-qr.png' @(
    'Mã QR động chuẩn NAPAS 247: số tiền + nội dung tự điền sẵn',
    'Kế toán đối soát 1 màn hình; biên lai điện tử tự động',
    'Xuất bảng kê đúng mẫu nhập MISA — định khoản Nợ 131/Có 5113'
) 'Khép kín dòng tiền: từ booking tới sổ sách kế toán không gõ lại số nào' | Out-Null

DemoSlide 10 'DEMO 5' 'AI Agent điều phối + dự báo Mamba-SSM' '08-ai-agent.png' @(
    'Agent quét 7 nguồn dữ liệu, xếp hạng việc cần làm 0–100 điểm',
    'Mỗi đề xuất kèm CHUỖI SUY LUẬN giải thích — không hộp đen',
    'Dự báo lưu lượng 6 giờ tới bằng mô hình cảm hứng Mamba (2023)'
) 'Như một trợ lý điều độ cấp cao trực 24/7 không lương' | Out-Null

# ============ 11. BAN DO AI ============
$s = NewSlide $false
TitleBar $s '12 CÔNG NGHỆ AI — phủ kín bản đồ trí tuệ nhân tạo'
$chips = @(
    'YOLO26 (2026) + WebGPU', 'ALPR biển số VN', 'ArcFace Face ID', 'MiniFASNet chống giả mạo',
    'AI Agent điều phối', 'Mamba-SSM dự báo', 'LSTM OCR container', 'MLP dự báo sản lượng',
    'K-Means phân khúc KH', 'Cây quyết định rủi ro', 'Q-Learning xe nâng', 'NLP-RAG chatbot Việt'
)
$x = 50; $y = 100
for ($i = 0; $i -lt $chips.Count; $i++) {
    $fill = $(if ($i % 3 -eq 1) { $ORANGE } else { $NAVY })
    Chip $s $x $y 275 50 $chips[$i] $fill $WHITE 13 | Out-Null
    $x += 293
    if ($x -gt 700) { $x = 50; $y += 62 }
}
TB $s 50 370 860 100 ('Từ Machine Learning cổ điển tới Deep Learning hiện đại nhất — mỗi mô hình giải một bài toán' + [char]13 + 'nghiệp vụ THẬT, demo được ngay tại chỗ, không phải danh sách trang trí.') 16 $NAVY $true 2 | Out-Null
Footer $s 11

# ============ 12. KIEN TRUC & BI QUYET ============
$s = NewSlide $true
TB $s 50 34 860 44 'BÍ QUYẾT: AI CHẠY PHÍA NGƯỜI DÙNG' 28 $ORANGE $true 2 | Out-Null
$arch = @(
    @('Trình duyệt nhân viên', ('YOLO26 · ArcFace · OCR · WebGPU' + [char]13 + '(AI nặng chạy tại đây — miễn phí)'), $ORANGE),
    @('Máy chủ web thường', ('ASP.NET + SQL Server' + [char]13 + '(chỉ lo nghiệp vụ, rẻ, dễ thuê)'), $NAVY2),
    @('Dịch vụ ngoài', ('VietQR · MISA · hosting' + [char]13 + '(chuẩn Việt Nam có sẵn)'), $NAVY2)
)
$x = 65
foreach ($a in $arch) {
    $box = $s.Shapes.AddShape(5, $x, 130, 260, 160)
    $box.Fill.ForeColor.RGB = [int]$a[2]; $box.Line.ForeColor.RGB = $WHITE; $box.Line.Weight = 1.5
    TB $s $x 148 260 34 $a[0] 16 $WHITE $true 2 | Out-Null
    TB $s ($x + 12) 190 236 90 $a[1] 13 $WHITE $false 2 | Out-Null
    $x += 285
}
$kq = @(
    'Chi phí máy chủ GPU: 0 đồng',
    '1 máy chủ web phục vụ hàng chục khách',
    'Dữ liệu hình ảnh KHÔNG rời máy khách — riêng tư tuyệt đối',
    'Giá bán rẻ hơn 10 lần giải pháp truyền thống'
)
$y = 330
foreach ($k in $kq) {
    Chip $s 240 $y 480 38 $k $NAVY2 $WHITE 14 | Out-Null
    $y += 48
}
Footer $s 12

# ============ 13. THI TRUONG ============
$s = NewSlide $false
TitleBar $s 'THỊ TRƯỜNG'
$rings = @(
    @('TAM', 'Logistics Việt Nam: 40+ tỷ USD/năm, tăng 14–16%/năm', 300, $NAVY),
    @('SAM', '~300 bến cảng + 100+ ICD + hàng trăm depot/bãi tư nhân', 235, $NAVY2),
    @('SOM 3 năm', '80 khách hàng trả phí (~10–15% nhóm depot chuyển đổi số)', 170, $ORANGE)
)
$cy = 305
foreach ($r in $rings) {
    $d = $r[2]
    $c = $s.Shapes.AddShape(9, (250 - $d / 2), ($cy - $d / 2), $d, $d)
    $c.Fill.ForeColor.RGB = $r[3]; $c.Fill.Transparency = 0.15; $c.Line.ForeColor.RGB = $WHITE; $c.Line.Weight = 2
}
TB $s 208 177 90 30 'TAM' 16 $WHITE $true 2 | Out-Null
TB $s 208 243 90 30 'SAM' 15 $WHITE $true 2 | Out-Null
TB $s 195 290 116 30 'SOM' 15 $WHITE $true 2 | Out-Null
$y = 120
foreach ($r in $rings) {
    Chip $s 480 $y 130 40 $r[0] $r[3] $WHITE 14 | Out-Null
    TB $s 625 ($y + 2) 290 62 $r[1] 14 $DARKTXT $false 1 | Out-Null
    $y += 108
}
TB $s 480 445 430 60 'Khách hàng đầu tiên: depot container tư nhân 2–15 ha — đau nhất, quyết nhanh nhất.' 14 $NAVY $true 1 | Out-Null
Footer $s 13

# ============ 14. MO HINH KINH DOANH ============
$s = NewSlide $false
TitleBar $s 'MÔ HÌNH KINH DOANH — SaaS thuê bao'
$goi = @(
    @('BASIC', '2,9 triệu/tháng', 'Depot, bãi xe nhỏ', 'Nghiệp vụ lõi + cổng ALPR + thanh toán QR', $NAVY),
    @('PRO', '6,9 triệu/tháng', 'ICD, cảng sông vừa', '+ Camera AI, dự báo, AI Agent, MISA', $ORANGE),
    @('ENTERPRISE', '14,9 triệu/tháng', 'Cảng nhỏ, chuỗi depot', '+ Tùy biến, API, hỗ trợ ưu tiên', $NAVY2)
)
$x = 50
foreach ($g in $goi) {
    $box = $s.Shapes.AddShape(5, $x, 100, 275, 285)
    $box.Fill.ForeColor.RGB = $WHITE; $box.Line.ForeColor.RGB = $g[4]; $box.Line.Weight = 2.5
    Chip $s ($x + 20) 117 235 42 $g[0] $g[4] $WHITE 16 | Out-Null
    TB $s ($x + 18) 170 240 34 $g[1] 21 $g[4] $true 2 | Out-Null
    TB $s ($x + 18) 210 240 30 $g[2] 13 $GRAY $false 2 | Out-Null
    TB $s ($x + 18) 247 240 125 $g[3] 14 $DARKTXT $false 1 | Out-Null
    $x += 293
}
TB $s 50 410 860 70 ('+ Phí triển khai 15–30 triệu/khách (một lần) · tùy biến · thiết bị camera đi kèm' + [char]13 + 'Chi phí biên cực thấp nhờ AI chạy máy khách.') 15 $DARKTXT $false 2 | Out-Null
Footer $s 14

# ============ 15. TAI CHINH ============
$s = NewSlide $false
TitleBar $s 'TÀI CHÍNH DỰ KIẾN 3 NĂM (thận trọng)'
$fin = @(
    @('Khách hàng', '10', '35', '80'),
    @('Tổng doanh thu', '0,58 tỷ', '2,3 tỷ', '5,4 tỷ'),
    @('Chi phí', '0,70 tỷ', '1,6 tỷ', '3,2 tỷ'),
    @('Lợi nhuận', '-0,12 tỷ', '+0,7 tỷ', '+2,2 tỷ')
)
$colX2 = @(50, 330, 520, 710); $colW2 = @(270, 180, 180, 200)
$hd = @('Chỉ tiêu', 'Năm 1', 'Năm 2', 'Năm 3')
for ($c = 0; $c -lt 4; $c++) { Chip $s $colX2[$c] 100 $colW2[$c] 42 $hd[$c] $NAVY $WHITE 15 | Out-Null }
$y = 155
foreach ($r in $fin) {
    for ($c = 0; $c -lt 4; $c++) {
        TB $s ($colX2[$c] + 8) $y ($colW2[$c] - 16) 40 $r[$c] 16 $(if ($r[0] -eq 'Lợi nhuận' -and $c -gt 0) { $(if ($r[$c].StartsWith('-')) { 0x2626DC } else { 0x3D8015 }) } else { $DARKTXT }) $(($c -eq 0)) 1 | Out-Null
    }
    $ln = $s.Shapes.AddShape(1, 50, ($y + 40), 860, 1); $ln.Fill.ForeColor.RGB = 0xD8CFC8; $ln.Line.Visible = 0
    $y += 52
}
Chip $s 50 395 860 58 'Hòa vốn giữa Năm 2 (~25–28 khách). Giả định: gói TB 5 triệu/tháng, rời bỏ 10%/năm.' $ORANGE $WHITE 16 | Out-Null
Footer $s 15

# ============ 16. LO TRINH + DOI NGU ============
$s = NewSlide $false
TitleBar $s 'LỘ TRÌNH & ĐỘI NGŨ'
$road = @(
    @('HIỆN TẠI', 'Sản phẩm hoàn chỉnh chạy thật trên hosting'),
    @('T1–3', 'Pháp lý + bản quyền AI + thí điểm 3 depot'),
    @('T4–9', 'Thương mại hóa: 10 khách trả phí đầu tiên'),
    @('T10–24', '35+ khách · barrier/cân điện tử · gọi vốn hạt giống')
)
$x = 50
foreach ($r in $road) {
    Chip $s $x 100 200 46 $r[0] $NAVY $WHITE 14 | Out-Null
    TB $s ($x + 4) 155 200 105 $r[1] 13 $DARKTXT $false 1 | Out-Null
    if ($x -lt 700) {
        $ar = $s.Shapes.AddShape(33, ($x + 205), 113, 18, 20)
        $ar.Fill.ForeColor.RGB = $ORANGE; $ar.Line.Visible = 0
    }
    $x += 228
}
$ln2 = $s.Shapes.AddShape(1, 50, 280, 860, 2); $ln2.Fill.ForeColor.RGB = 0xD8CFC8; $ln2.Line.Visible = 0
TB $s 50 298 860 34 'ĐỘI NGŨ' 20 $NAVY $true 1 | Out-Null
TB $s 50 340 860 140 ('Lê Hữu Anh Tài — Founder, Sản phẩm & Công nghệ: tự tay xây toàn bộ nền tảng (full-stack + 12 mô hình AI).' + [char]13 + 'Đang tìm 2 đồng sáng lập: Kinh doanh logistics (quan hệ depot/hãng tàu) và Vận hành – CSKH.' + [char]13 + 'Cố vấn: giảng viên CNTT + chuyên gia khai thác cảng.') 15 $DARKTXT $false 1 | Out-Null
Footer $s 16

# ============ 17. ASK ============
$s = NewSlide $true
TB $s 50 55 860 46 'KÊU GỌI' 30 $ORANGE $true 2 | Out-Null
TB $s 50 110 860 80 '300 TRIỆU ĐỒNG · 6 THÁNG TĂNG TỐC' 40 $WHITE $true 2 | Out-Null
$asks = @(
    @('40%', 'Nhân sự: 2 dev + 1 kinh doanh'),
    @('25%', 'Pháp lý + bản quyền AI thương mại'),
    @('20%', 'Thí điểm 3 depot + thiết bị demo'),
    @('15%', 'Hạ tầng + dự phòng')
)
$x = 70
foreach ($a in $asks) {
    $box = $s.Shapes.AddShape(5, $x, 225, 195, 150)
    $box.Fill.ForeColor.RGB = $NAVY2; $box.Line.ForeColor.RGB = $ORANGE; $box.Line.Weight = 1.5
    TB $s $x 247 195 44 $a[0] 30 $ORANGE $true 2 | Out-Null
    TB $s ($x + 10) 295 175 70 $a[1] 13 $WHITE $false 2 | Out-Null
    $x += 212
}
TB $s 50 425 860 60 'Đổi lại: quyền ưu tiên đàm phán cổ phần vòng hạt giống + gói Enterprise trọn đời.' 16 $WHITE $false 2 | Out-Null
Footer $s 17

# ============ 18. CAM ON ============
$s = NewSlide $true
$bar2 = $s.Shapes.AddShape(1, 0, 330, $W, 6); $bar2.Fill.ForeColor.RGB = $ORANGE; $bar2.Line.Visible = 0
TB $s 80 140 800 70 'SMARTPORT' 54 $WHITE $true 2 | Out-Null
TB $s 80 225 800 44 'Cảng thông minh trong tầm tay mọi doanh nghiệp Việt' 22 $ORANGE $true 2 | Out-Null
TB $s 80 368 800 34 'EM XIN CẢM ƠN QUÝ BAN GIÁM KHẢO!' 20 $WHITE $true 2 | Out-Null
TB $s 80 412 800 30 'Demo trực tiếp: smartport-anhtai.somee.com  ·  lehuuanhtai2020tkh@gmail.com' 14 $GRAY $false 2 | Out-Null

# ---- Luu ----
$pres.SaveAs($outPptx, 24)
$pres.SaveAs($outPdf, 32)
$count = $pres.Slides.Count
$pres.Slides.Item(2).Export((Join-Path $outDir "qa-tt2.png"), "PNG", 960, 540)
$pres.Slides.Item(6).Export((Join-Path $outDir "qa-tt6.png"), "PNG", 960, 540)
$pres.Slides.Item(12).Export((Join-Path $outDir "qa-tt12.png"), "PNG", 960, 540)
$pres.Close()
$pp.Quit()
Write-Output "DA TAO BAI THUYET TRINH: $count slide + PDF"
