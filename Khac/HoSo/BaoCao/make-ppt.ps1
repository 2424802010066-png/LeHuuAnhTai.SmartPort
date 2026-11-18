# ============================================================
# TAO SLIDE THUYET TRINH — SmartPort (PowerPoint COM)
# Dong bo theo cau truc moi 5 Chuong (I-V) cua BaoCao_DoAn_SmartPort
# Palette: Navy #0A2F57 chu dao · Ice #CADCFC · Cam #FF7D2D · Tim DL #8B5CF6
# ============================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$imgDir = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\img-doc'
$outPptx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\Slide_ThuyetTrinh_SmartPort.pptx'

function C($r, $g, $b) { return $r + $g * 256 + $b * 65536 }
$NAVY  = C 10 47 87
$NAVYD = C 7 31 58
$ICE   = C 202 220 252
$CAM   = C 255 125 45
$TIM   = C 139 92 246
$WHITE = C 255 255 255
$MUTED = C 91 107 124
$LIGHT = C 244 247 251

$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(0)   # khong cua so
$pres.PageSetup.SlideWidth = 960
$pres.PageSetup.SlideHeight = 540
$script:idx = 0

function NewSlide($bg) {
    $script:idx++
    $s = $pres.Slides.Add($script:idx, 12)   # ppLayoutBlank
    $s.FollowMasterBackground = 0
    $s.Background.Fill.Solid() | Out-Null
    $s.Background.Fill.ForeColor.RGB = $bg
    return $s
}
function T($s, $text, $x, $y, $w, $h, $size, $bold, $color, $align) {
    $tb = $s.Shapes.AddTextbox(1, [single]$x, [single]$y, [single]$w, [single]$h)
    $tr = $tb.TextFrame.TextRange
    $tr.Text = [string]$text
    $tr.Font.Name = 'Calibri'
    $tr.Font.Size = [single]$size
    $tr.Font.Bold = [int]$bold
    $tr.Font.Color.RGB = [int]$color
    $tr.ParagraphFormat.Alignment = [int]$align
    $tb.TextFrame.WordWrap = -1
    $tb.TextFrame.MarginLeft = 0; $tb.TextFrame.MarginRight = 0
    $tb.TextFrame.MarginTop = 0; $tb.TextFrame.MarginBottom = 0
    return $tb
}
function Chip($s, $text, $x, $y, $w, $h, $fill, $tcolor, $size, $bold = $true) {
    $sh = $s.Shapes.AddShape(5, [single]$x, [single]$y, [single]$w, [single]$h)   # rounded rect
    $sh.Fill.ForeColor.RGB = [int]$fill
    $sh.Line.Visible = 0
    try { $sh.Adjustments.Item(1) = 0.5 } catch { }
    $tr = $sh.TextFrame.TextRange
    $tr.Text = [string]$text
    $tr.Font.Name = 'Calibri'; $tr.Font.Size = [single]$size; $tr.Font.Bold = [int]$bold
    $tr.Font.Color.RGB = [int]$tcolor
    $tr.ParagraphFormat.Alignment = 2
    $sh.TextFrame.MarginLeft = 4; $sh.TextFrame.MarginRight = 4
    $sh.TextFrame.MarginTop = 2; $sh.TextFrame.MarginBottom = 2
    return $sh
}
function Card($s, $x, $y, $w, $h, $fill) {
    $sh = $s.Shapes.AddShape(5, [single]$x, [single]$y, [single]$w, [single]$h)
    $sh.Fill.ForeColor.RGB = [int]$fill
    $sh.Line.Visible = 0
    try { $sh.Adjustments.Item(1) = 0.06 } catch { }
    return $sh
}
function ImgS($s, $file, $x, $y, $w, $maxH = 400, $ext = 'jpg') {
    $path = Join-Path $imgDir "$file.$ext"
    $im = [System.Drawing.Image]::FromFile($path)
    $ratio = $im.Height / $im.Width
    $im.Dispose()
    $h = [math]::Round($w * $ratio)
    if ($h -gt $maxH) { $h = $maxH; $w = [math]::Round($h / $ratio) }
    $pic = $s.Shapes.AddPicture($path, 0, -1, [single]$x, [single]$y, [single]$w, [single]$h)
    $pic.Shadow.Visible = -1
    $pic.Shadow.OffsetX = 4; $pic.Shadow.OffsetY = 5
    $pic.Shadow.ForeColor.RGB = (C 150 165 185)
    return $pic
}
function ImgFit($s, $file, $x, $y, $maxW, $maxH, $ext = 'jpg') {
    $path = Join-Path $imgDir "$file.$ext"
    $im = [System.Drawing.Image]::FromFile($path)
    $ratio = $im.Width / $im.Height
    $im.Dispose()
    $w = $maxW; $h = [math]::Round($w / $ratio)
    if ($h -gt $maxH) { $h = $maxH; $w = [math]::Round($h * $ratio) }
    $cx = [math]::Round($x + ($maxW - $w) / 2)
    $cy = [math]::Round($y + ($maxH - $h) / 2)
    $pic = $s.Shapes.AddPicture($path, 0, -1, [single]$cx, [single]$cy, [single]$w, [single]$h)
    return $pic
}
function Stat($s, $num, $label, $x, $y, $w, $color) {
    T $s $num $x $y $w 52 40 $true $color 2 | Out-Null
    T $s $label $x ($y + 48) $w 36 12.5 $false $MUTED 2 | Out-Null
}
function TitleBar($s, $kicker, $title) {
    T $s $kicker 50 26 860 20 13 $true $CAM 1 | Out-Null
    T $s $title 50 46 860 46 30 $true $NAVY 1 | Out-Null
}

# ============ SLIDE 1: BIA (navy) ============
$s = NewSlide $NAVYD
$g = $s.Shapes.AddShape(1, 0, 0, 960, 540); $g.Fill.ForeColor.RGB = $NAVYD; $g.Line.Visible = 0; $g.ZOrder(1) | Out-Null
ImgS $s '01-home-p1' 540 92 390 356 | Out-Null
Chip $s '10 CÔNG NGHỆ AI  ·  3 MÔ HÌNH DEEP LEARNING CHẠY THẬT' 50 84 380 30 $CAM $WHITE 12 | Out-Null
T $s 'TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT · VIỆN CHUYỂN ĐỔI SỐ' 50 118 460 18 11.5 $true $CAM 1 | Out-Null
T $s 'BÁO CÁO ĐỒ ÁN MÔN LẬP TRÌNH WEB' 50 138 460 22 13.5 $false $ICE 1 | Out-Null
T $s 'SMARTPORT' 50 162 470 60 52 $true $WHITE 1 | Out-Null
T $s 'Website quản lý cảng trong ngành Logistics' 50 224 470 30 19 $true $ICE 1 | Out-Null
T $s ("ASP.NET MVC 5  ·  Entity Framework 6  ·  SQL Server`nYOLOv10  ·  OCR LSTM  ·  TensorFlow.js") 50 266 470 44 13 $false $ICE 1 | Out-Null
T $s ("Sinh viên thực hiện:  LÊ HỮU ANH TÀI") 50 356 470 24 16 $true $WHITE 1 | Out-Null
T $s ("MSSV: 2424802010066      GVHD: ThS. Nguyễn Thành Trung") 50 382 470 22 13 $false $ICE 1 | Out-Null
T $s 'TP. Hồ Chí Minh — Tháng 7/2026' 50 470 470 20 12 $false $MUTED 1 | Out-Null

# ============ SLIDE 2: NOI DUNG ============
$s = NewSlide $WHITE
TitleBar $s 'AGENDA' 'Nội dung trình bày — theo 5 chương báo cáo'
$muc = @('Chương I — Tổng quan vấn đề nghiên cứu', 'Chương II — Cơ sở lý thuyết & công nghệ', 'Chương III — Phân tích & thiết kế hệ thống', 'Chương IV — Kết quả thực nghiệm (giao diện)', 'Chương V — Tổng kết & hướng phát triển', 'Sơ đồ UML: Use Case · Activity · Class · CSDL', '3 mô hình Deep Learning chạy thật', 'Hỏi đáp (Q&A)')
for ($i = 0; $i -lt 8; $i++) {
    $col = $i % 2; $row = [math]::Floor($i / 2)
    $x = 70 + $col * 430; $y = 130 + $row * 92
    Chip $s ([string]($i + 1)) $x $y 40 40 $CAM $WHITE 18 | Out-Null
    T $s $muc[$i] ($x + 56) ($y + 7) 350 30 15.5 $true $NAVY 1 | Out-Null
}

# ============ SLIDE 3: CHUONG I - GIOI THIEU ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG I' 'Tổng quan vấn đề nghiên cứu'
T $s ("Bối cảnh:  cảng biển xử lý phần lớn hàng hóa XNK nhưng nhiều cảng vẫn vận hành thủ công — khách phải đến tận nơi tra cứu, điều độ phân bến bằng kinh nghiệm, số liệu phân tán.`n`nGiải pháp SmartPort:  số hóa toàn bộ quy trình cảng trên một website duy nhất và tiên phong đưa Deep Learning chạy ngay trên trình duyệt (không cần GPU server).") 50 108 500 220 15 $false (C 18 38 58) 1 | Out-Null
Card $s 50 330 500 170 $LIGHT | Out-Null
T $s 'Mục tiêu & phạm vi nghiên cứu' 74 348 460 24 16 $true $NAVY 1 | Out-Null
T $s ("1  Cổng công khai: tra cứu, lịch tàu, giá, ước tính phí AI`n2  ePort: đặt dịch vụ online + hóa đơn Excel/PDF/Email`n3  Quản trị: điều độ tàu — container — bãi — bến — báo cáo`n4  Tích hợp 10 công nghệ AI (3 Deep Learning)") 74 376 460 110 13.5 $false (C 18 38 58) 1 | Out-Null
Stat $s '90%' 'hàng hóa XNK qua cảng biển' 596 150 160 $CAM
Stat $s '14-16%' 'tăng trưởng logistics/năm' 772 150 160 $CAM
Stat $s '3' 'nhóm người dùng' 596 280 160 $NAVY
Stat $s '5' 'chương báo cáo' 772 280 160 $NAVY
Stat $s '10' 'công nghệ AI tích hợp' 596 410 160 $TIM
Stat $s '3' 'mô hình Deep Learning' 772 410 160 $TIM

# ============ SLIDE 4: CHUONG II - CONG NGHE NEN TANG ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG II' 'Cơ sở lý thuyết & công nghệ nền tảng'
$tech = @(
    @('ASP.NET MVC 5', 'Kiến trúc Model–View–Controller, Areas tách khu quản trị, phân quyền 3 vai trò'),
    @('Entity Framework 6 + SQL Server', 'ORM ánh xạ 14 bảng SQL Server, truy vấn LINQ, chống SQL Injection'),
    @('Bootstrap 5 + CSS riêng', 'Bộ nhận diện navy–cam, dark mode, hiệu ứng thuần CSS/JS, không phụ thuộc thư viện ngoài')
)
for ($i = 0; $i -lt 3; $i++) {
    $y = 116 + $i * 128
    Card $s 50 $y 860 112 $LIGHT | Out-Null
    Chip $s ([string]($i + 1)) 66 ($y + 16) 34 34 $NAVY $WHITE 15 | Out-Null
    T $s $tech[$i][0] 112 ($y + 14) 500 24 17 $true $NAVY 1 | Out-Null
    T $s $tech[$i][1] 112 ($y + 44) 770 44 13 $false $MUTED 1 | Out-Null
}

# ============ SLIDE 5: CHUONG II - SO DO KIEN TRUC (diagram) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG II · 2.3' 'Sơ đồ kiến trúc hệ thống SmartPort'
ImgFit $s 'd1-kientruc' 40 100 880 410 'png' | Out-Null
Chip $s '3 tầng: Trình duyệt (Client) → Ứng dụng (ASP.NET MVC 5) → Dữ liệu (SQL Server)' 130 500 700 30 $NAVY $WHITE 12.5 | Out-Null

# ============ SLIDE 6: CHUONG II - AI & DEEP LEARNING NEN TANG ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG II · 2.7' 'Nền tảng AI và Deep Learning ứng dụng'
$ai4 = @(
    @('YOLO (You Only Look Once)', 'CNN một giai đoạn, dự đoán khung bao + lớp đồng thời. Dùng YOLOv10n NMS-free, chạy qua ONNX Runtime Web'),
    @('OCR — mạng LSTM', 'Tesseract.js quét tuần tự dải ảnh, dự đoán ký tự — thay thế hoàn toàn so khớp mẫu cổ điển'),
    @('MLP — TensorFlow.js', 'Mạng nơ-ron truyền thẳng 3→16→8→1, tối ưu Adam, huấn luyện ngay trên trình duyệt'),
    @('Chatbot — kiến trúc RAG', 'Nhận diện ý định + truy vấn CSDL thật, tùy chọn sinh ngôn ngữ tự nhiên qua Google Gemini')
)
for ($i = 0; $i -lt 4; $i++) {
    $col = $i % 2; $row = [math]::Floor($i / 2)
    $x = 50 + $col * 440; $y = 118 + $row * 172
    Card $s $x $y 420 156 $LIGHT | Out-Null
    Chip $s ([string]($i + 1)) ($x + 16) ($y + 16) 34 34 $TIM $WHITE 15 | Out-Null
    T $s $ai4[$i][0] ($x + 62) ($y + 14) 340 24 15.5 $true $NAVY 1 | Out-Null
    T $s $ai4[$i][1] ($x + 62) ($y + 44) 340 100 12.5 $false $MUTED 1 | Out-Null
}

# ============ SLIDE 7: CHUONG III - USE CASE (diagram) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG III · 3.1.2' 'Sơ đồ Use Case — 3 tác nhân, 14 chức năng'
ImgFit $s 'd2-usecase' 40 100 880 410 'png' | Out-Null
Chip $s 'Khách hàng · Nhân viên · Quản trị viên — theo đúng 3 vai trò phân quyền hệ thống' 190 500 580 30 $NAVY $WHITE 12.5 | Out-Null

# ============ SLIDE 8: CHUONG III - ACTIVITY (diagram) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG III · 3.1.3' 'Sơ đồ Activity — quy trình đặt & duyệt dịch vụ ePort'
ImgFit $s 'd3-activity' 130 96 700 420 'png' | Out-Null

# ============ SLIDE 9: CHUONG III - CLASS DIAGRAM (diagram) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG III · 3.1.4' 'Sơ đồ Class Diagram — các lớp Model chính'
ImgFit $s 'd4-class' 40 100 880 410 'png' | Out-Null
Chip $s 'Entity Framework Code First — lớp Model tương ứng 1–1 với bảng CSDL' 220 500 520 30 $NAVY $WHITE 12.5 | Out-Null

# ============ SLIDE 10: CHUONG III - MO HINH VAT LY (diagram) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG III · 3.2.1' 'Mô hình vật lý cơ sở dữ liệu — 14 bảng'
ImgFit $s 'd5-er' 30 96 900 415 'png' | Out-Null

# ============ SLIDE 11: TRANG CHU ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Trang chủ — chuẩn cổng thông tin cảng hiện đại'
ImgS $s '01-home-p1' 470 108 440 392 | Out-Null
T $s ("•  Ticker TRỰC TIẾP chạy dữ liệu chuyến tàu thật`n`n•  Hero slider 4 ảnh cảng thật (Ken Burns) + 12 hạt sáng bay`n`n•  Thống kê thời gian thực từ CSDL`n`n•  Lưới 10 công nghệ AI · quy trình 4 bước`n`n•  Parallax số liệu · thư viện ảnh · đối tác · FAQ`n`n•  Chatbot PortBot thường trực mọi trang") 50 120 400 330 14.5 $false (C 18 38 58) 1 | Out-Null
Chip $s 'Hiệu ứng: reveal cuộn trang · tilt 3D theo chuột · dark mode' 50 462 400 34 $NAVY $WHITE 12 | Out-Null

# ============ SLIDE 12: TRAI NGHIEM ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Dark mode & bộ nhận diện thương hiệu riêng'
ImgS $s '02-home-dark' 50 118 430 350 | Out-Null
ImgS $s '01-home-p5' 500 118 410 350 | Out-Null
T $s 'Chế độ Tối — tự nhận theo hệ điều hành, ghi nhớ lựa chọn' 50 480 430 22 12.5 $true $MUTED 2 | Out-Null
T $s 'Lịch tàu sắp cập cảng + thư viện ảnh Ken Burns' 500 480 410 22 12.5 $true $MUTED 2 | Out-Null

# ============ SLIDE 13: TRA CUU + OCR ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1 — DEEP LEARNING #2' 'Tra cứu container & OCR đọc số từ ảnh'
ImgS $s '04-tracking' 50 118 425 330 | Out-Null
ImgS $s '05-ocr-ai' 495 118 425 330 | Out-Null
Chip $s 'Timeline hành trình + phí lưu bãi tự tính' 50 458 425 30 $LIGHT $NAVY 12 | Out-Null
Chip $s 'Mạng LSTM đọc MSCU2201417 — độ tin cậy 93%' 495 458 425 30 $TIM $WHITE 12 | Out-Null

# ============ SLIDE 14: TRANG CONG KHAI KHAC ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Lịch tàu · Dịch vụ & Giá · Tin tức'
ImgS $s '06-schedule' 70 116 230 360 | Out-Null
ImgS $s '07-services' 370 116 230 360 | Out-Null
ImgS $s '09-news' 670 116 230 360 | Out-Null
T $s 'Lịch tàu ETA/ATA · ETD/ATD, lọc theo hãng tàu' 40 486 290 34 11.5 $false $MUTED 2 | Out-Null
T $s 'Bảng giá 12 dịch vụ theo 5 nhóm' 340 486 290 34 11.5 $false $MUTED 2 | Out-Null
T $s 'Tin tức ảnh thật, phân trang, lượt xem' 640 486 290 34 11.5 $false $MUTED 2 | Out-Null

# ============ SLIDE 15: CHATBOT ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'PortBot — chatbot dữ liệu thật + giọng nói tiếng Việt'
ImgS $s '03-chatbot' 480 108 430 392 | Out-Null
T $s ("•  Nhận diện ý định tiếng Việt (có/không dấu)`n`n•  Trích mã container/booking bằng regex`n`n•  Trả lời bằng DỮ LIỆU THẬT: lịch tàu, giá, phí lưu bãi`n`n•  Kiến trúc RAG với Google Gemini (tùy chọn)`n`n•  Nói câu hỏi qua micro (Web Speech API vi-VN)`n`n•  Bot đọc to câu trả lời khi bật loa") 50 120 400 320 14.5 $false (C 18 38 58) 1 | Out-Null
Chip $s 'Hoạt động 24/7 trên mọi trang của website' 50 452 400 34 $CAM $WHITE 13 | Out-Null

# ============ SLIDE 16: UOC TINH PHI ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Công cụ AI ước tính chi phí dịch vụ cảng'
ImgS $s '08-cost' 440 108 470 392 | Out-Null
T $s ("Khách khai báo lô hàng:`nloại cont · số lượng · ngày lưu bãi · dịch vụ kèm`n`nAI tính toán:`n•  Chi tiết từng khoản theo biểu giá thật`n•  Biểu đồ cơ cấu chi phí`n•  Nhận xét thông minh: cảnh báo phí lưu bãi, ưu đãi sản lượng, gợi ý tiết kiệm") 50 120 370 300 14 $false (C 18 38 58) 1 | Out-Null
Chip $s 'Miễn phí — không cần đăng nhập' 50 430 370 34 $NAVY $WHITE 13 | Out-Null

# ============ SLIDE 17: EPORT ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'ePort — đặt dịch vụ cảng trực tuyến'
ImgS $s '15-booking-create' 400 108 510 392 | Out-Null
T $s ("•  Chọn dịch vụ + số lượng, hệ thống tính tiền tức thời`n`n•  Mã booking tự sinh (BK260009)`n`n•  Quy trình đúng sơ đồ Activity đã thiết kế:`n   Chờ duyệt → Đã duyệt → Đang thực hiện → Hoàn thành`n`n•  Theo dõi & hủy trực tuyến`n`n•  Kiểm thử: tổng tiền khớp tuyệt đối 1.590.000đ") 50 122 330 340 13.5 $false (C 18 38 58) 1 | Out-Null

# ============ SLIDE 18: THEO DOI BOOKING + HO SO ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Theo dõi booking & hồ sơ cá nhân'
ImgS $s '16-booking-my' 50 118 425 350 | Out-Null
ImgS $s '15b-profile' 495 118 425 350 | Out-Null
Chip $s 'Danh sách booking, trạng thái, hủy khi chờ duyệt' 50 476 425 30 $LIGHT $NAVY 12 | Out-Null
Chip $s 'Cập nhật thông tin & đổi mật khẩu (SHA-256)' 495 476 425 30 $LIGHT $NAVY 12 | Out-Null

# ============ SLIDE 19: HOA DON ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.1' 'Hóa đơn 3 kênh: Excel · PDF · Email'
ImgS $s '18-invoice-print' 460 108 450 392 | Out-Null
Card $s 50 120 380 110 $LIGHT | Out-Null
T $s 'Excel (.xlsx — EPPlus)' 74 134 330 22 15 $true $NAVY 1 | Out-Null
T $s 'Khung hóa đơn kế toán VN, thuế GTGT 8%, tự đọc số tiền thành chữ' 74 160 336 56 12.5 $false $MUTED 1 | Out-Null
Card $s 50 244 380 110 $LIGHT | Out-Null
T $s 'PDF (bản in chuyên nghiệp)' 74 258 330 22 15 $true $NAVY 1 | Out-Null
T $s 'Logo thương hiệu, bảng kê chi tiết, khối chữ ký 3 bên — In/Lưu PDF' 74 284 336 56 12.5 $false $MUTED 1 | Out-Null
Card $s 50 368 380 110 $LIGHT | Out-Null
T $s 'Email (SMTP + chế độ demo)' 74 382 330 22 15 $true $NAVY 1 | Out-Null
T $s 'Gửi hóa đơn đính kèm tới khách; chưa có SMTP thì sinh file .eml demo' 74 408 336 56 12.5 $false $MUTED 1 | Out-Null

# ============ SLIDE 20: DASHBOARD ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Dashboard quản trị — chỉ số, cảnh báo AI, biểu đồ'
ImgS $s '19-dashboard' 320 106 590 400 | Out-Null
T $s ("•  6 thẻ chỉ số thời gian thực`n`n•  Trợ lý AI cảnh báo vận hành (5 loại cảnh báo)`n`n•  Biểu đồ TEU + dự báo AI`n`n•  Cơ cấu container, doanh thu 6 tháng") 50 122 250 300 13 $false (C 18 38 58) 1 | Out-Null

# ============ SLIDE 21: DIEU DO + AI BEN ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Điều độ chuyến tàu & AI phân bổ bến'
ImgS $s '21-berth-ai' 400 108 510 392 | Out-Null
T $s ("Ràng buộc an toàn hàng hải:`n•  Chiều dài bến ≥ LOA + 15m dây buộc`n•  Độ sâu ≥ mớn nước + 0,5m`n•  Không trùng lịch ±6h`n`nAI chấm điểm từng bến, giải thích lý do, dự đoán giờ giải phóng tàu (moves ÷ cẩu × 28) — áp dụng 1 chạm.`n`nĐúng giải thuật đã trình bày ở mục 3.3.5 báo cáo") 50 122 330 340 13 $false (C 18 38 58) 1 | Out-Null

# ============ SLIDE 22: CONTAINER + SO DO BAI ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Quản lý container & sơ đồ bãi thông minh'
ImgS $s '24-containers' 50 118 425 340 | Out-Null
ImgS $s '26-yardmap' 495 118 425 340 | Out-Null
Chip $s 'Tìm kiếm, lọc, gate-out; nút AI gợi ý vị trí hạ bãi' 50 466 425 30 $LIGHT $NAVY 12 | Out-Null
Chip $s 'Sơ đồ bãi 6 khu — màu theo chiều cao chồng, ❄ cont lạnh' 495 466 425 30 $TIM $WHITE 11.5 | Out-Null

# ============ SLIDE 23: YOLO (diem nhan) ============
$s = NewSlide $NAVYD
T $s 'CHƯƠNG IV · DEEP LEARNING #1' 50 30 500 20 13 $true $TIM 1 | Out-Null
T $s 'Camera AI — YOLOv10 nhận diện đối tượng' 50 52 860 40 30 $true $WHITE 1 | Out-Null
ImgS $s '22-vision-yolo' 430 104 480 396 | Out-Null
T $s ("•  YOLOv10n — thế hệ YOLO mới nhất (NMS-free, 2024)`n`n•  Chạy NGAY TRÊN TRÌNH DUYỆT bằng ONNX Runtime (WebAssembly)`n`n•  Letterbox 640×640 → CNN → 300 dự đoán → vẽ khung + nhãn tiếng Việt`n`n•  Nguồn: ảnh tải lên · ảnh mẫu · webcam real-time`n`n•  Cảnh báo điều phối: ≥3 xe tải → mở thêm làn cổng") 50 110 360 270 13 $false $ICE 1 | Out-Null
Stat $s '87%' 'độ tin cậy nhận diện xe tải' 50 400 170 $CAM
Stat $s '286ms' 'mỗi khung hình (CPU)' 235 400 170 $CAM

# ============ SLIDE 24: AI LAB (diem nhan) ============
$s = NewSlide $NAVYD
T $s 'CHƯƠNG IV · DEEP LEARNING #3' 50 30 500 20 13 $true $TIM 1 | Out-Null
T $s 'AI Lab — tự huấn luyện mạng nơ-ron trên trình duyệt' 50 52 880 40 30 $true $WHITE 1 | Out-Null
ImgS $s '23-ailab-train' 430 104 480 396 | Out-Null
T $s ("•  MLP 3 → 16 → 8 → 1 (ReLU) — TensorFlow.js`n`n•  Cửa sổ trượt 3 tháng dự đoán tháng kế tiếp`n`n•  Tối ưu Adam · loss MSE · chuẩn hóa Min-Max`n`n•  Chỉnh siêu tham số: epochs, learning rate, nơ-ron ẩn`n`n•  Đường loss giảm THEO THỜI GIAN THỰC (thang log)`n`n•  So sánh trực quan với hồi quy tuyến tính") 50 110 360 280 13 $false $ICE 1 | Out-Null
Stat $s '9,7s' '200 epochs trên trình duyệt' 50 408 170 $CAM
Stat $s '±5.897' 'MAE (TEU) trên tập huấn luyện' 235 408 170 $CAM

# ============ SLIDE 25: BEN LIVE + BOOKING ADMIN ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Bến cảng LIVE & xử lý booking phía quản trị'
ImgS $s '27-berths' 50 118 425 340 | Out-Null
ImgS $s '28-bookings-admin' 495 118 425 340 | Out-Null
Chip $s 'Sơ đồ cầu bến LIVE — tàu đang chiếm bến hiển thị động' 50 468 425 30 $LIGHT $NAVY 12 | Out-Null
Chip $s 'Quy trình duyệt booking + phát hành hóa đơn' 495 468 425 30 $LIGHT $NAVY 12 | Out-Null

# ============ SLIDE 26: QUAN TRI DANH MUC (BIEU MAU) ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Quản trị danh mục — biểu mẫu thêm/sửa đầy đủ'
ImgS $s '34b-news-form' 70 116 230 360 | Out-Null
ImgS $s '33b-vessel-form' 370 116 230 360 | Out-Null
ImgS $s '36b-service-form' 670 116 230 360 | Out-Null
T $s 'Soạn thảo tin tức' 40 486 290 30 12 $false $MUTED 2 | Out-Null
T $s 'Hồ sơ tàu (IMO, LOA, mớn nước)' 340 486 290 30 12 $false $MUTED 2 | Out-Null
T $s 'Dịch vụ & đơn giá' 640 486 290 30 12 $false $MUTED 2 | Out-Null

# ============ SLIDE 27: THU VIEN ANH + BAO CAO ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.2' 'Thư viện ảnh & Báo cáo thống kê'
ImgS $s '29-gallery' 50 118 425 350 | Out-Null
ImgS $s '30-reports' 495 118 425 350 | Out-Null
Chip $s '21 vị trí ảnh — dán URL hoặc tải ảnh riêng' 50 476 425 30 $LIGHT $NAVY 12 | Out-Null
Chip $s 'Sản lượng TEU & doanh thu, biểu đồ, xuất Excel' 495 476 425 30 $LIGHT $NAVY 12 | Out-Null

# ============ SLIDE 28: KET QUA KIEM THU ============
$s = NewSlide $WHITE
TitleBar $s 'CHƯƠNG IV · 4.3' 'Kết quả kiểm thử & số liệu hệ thống'
Stat $s '26/26' 'trang HTTP 200 OK' 60 140 200 (C 15 138 77)
Stat $s '12/12' 'test case nghiệp vụ Đạt' 280 140 200 (C 15 138 77)
Stat $s '93%' 'độ tin cậy OCR container' 500 140 200 $TIM
Stat $s '87%' 'YOLO nhận diện xe tải' 720 140 200 $TIM
Stat $s '44' 'giao diện Razor View' 60 270 200 $NAVY
Stat $s '24' 'controller' 280 270 200 $NAVY
Stat $s '14' 'bảng CSDL' 500 270 200 $NAVY
Stat $s '11' 'tài liệu tham khảo' 720 270 200 $NAVY
Card $s 60 390 840 108 $LIGHT | Out-Null
T $s 'Các luồng đã kiểm chứng đầu-cuối' 84 404 700 22 15 $true $NAVY 1 | Out-Null
T $s 'Đặt dịch vụ ePort (BK260009 — 1.590.000đ khớp tuyệt đối) · Hóa đơn Excel/PDF/Email (.eml demo) · AI phân bổ bến áp dụng 1 chạm · OCR tự điền ô tra cứu · phân quyền chặn đúng vai trò · dark mode đồng bộ toàn trang' 84 430 800 60 12.5 $false $MUTED 1 | Out-Null

# ============ SLIDE 29: CHUONG V - KET LUAN (navy) ============
$s = NewSlide $NAVYD
T $s 'CHƯƠNG V' 50 30 400 20 13 $true $CAM 1 | Out-Null
T $s 'Tổng kết & hướng phát triển' 50 52 860 40 32 $true $WHITE 1 | Out-Null
Card $s 50 116 430 384 $NAVY | Out-Null
T $s 'Kết quả đạt được' 74 134 340 26 18 $true $CAM 1 | Out-Null
T $s ("•  Hệ thống quản lý cảng hoàn chỉnh: 24 controller, 44 giao diện, 14 bảng CSDL`n`n•  ePort + hóa đơn 3 kênh Excel/PDF/Email`n`n•  Đầy đủ sơ đồ UML: Use Case · Activity · Class · Mô hình vật lý CSDL`n`n•  Hệ sinh thái 10 công nghệ AI — 3 mô hình Deep Learning suy luận/huấn luyện ngay trên trình duyệt (edge inference)") 74 168 386 300 13 $false $ICE 1 | Out-Null
Card $s 500 116 410 384 $NAVY | Out-Null
T $s 'Hướng phát triển' 524 134 340 26 18 $true $CAM 1 | Out-Null
T $s ("•  Fine-tune YOLO trên dữ liệu container/xe đầu kéo Việt Nam`n`n•  Dự báo bằng LSTM/GRU khi đủ dữ liệu dài hạn`n`n•  SignalR thông báo thời gian thực · thanh toán online · chữ ký số hóa đơn`n`n•  Triển khai cloud + CI/CD · ứng dụng di động cho tài xế") 524 168 362 300 13.5 $false $ICE 1 | Out-Null

# ============ SLIDE 30: CAM ON (navy) ============
$s = NewSlide $NAVYD
Chip $s '⚓  SMARTPORT' 330 130 300 52 $CAM $WHITE 22 | Out-Null
T $s 'EM XIN CHÂN THÀNH CẢM ƠN' 80 220 800 50 38 $true $WHITE 2 | Out-Null
T $s 'Quý thầy cô và các bạn đã lắng nghe!' 80 276 800 30 18 $false $ICE 2 | Out-Null
T $s 'Rất mong nhận được câu hỏi và góp ý  —  Q&A' 80 330 800 26 15 $true $CAM 2 | Out-Null
T $s ("Lê Hữu Anh Tài  ·  MSSV 2424802010066`nlehuuanhtai2020tkh@gmail.com  ·  0387 562 579  ·  facebook.com/anh.tai.129573") 80 420 800 50 13 $false $MUTED 2 | Out-Null

# ---------- Luu ----------
if (Test-Path $outPptx) { [IO.File]::Delete($outPptx) }
$pres.SaveAs($outPptx, 24)
$soSlide = $pres.Slides.Count
$pres.Close()
$pp.Quit()
"XONG: $outPptx — $soSlide slide"
