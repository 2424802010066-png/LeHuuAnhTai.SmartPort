# ============================================================
# TAO PITCH DECK KHOI NGHIEP — SmartPort (PowerPoint COM, 16:9)
# ============================================================
$ErrorActionPreference = 'Stop'
$outDir = "C:\Users\ASUS\Downloads\SmartPort\KhoiNghiep"
$imgDir = "C:\Users\ASUS\Downloads\SmartPort\anh-tinhnang"
$outPptx = Join-Path $outDir "PitchDeck_SmartPort.pptx"
$outPdf  = Join-Path $outDir "PitchDeck_SmartPort.pdf"

# Mau (BGR int cho COM)
$NAVY   = 0x572F0A     # #0A2F57
$NAVY2  = 0x7A4A15     # dam hon chut cho khoi
$ORANGE = 0x2D7DFF     # #FF7D2D
$WHITE  = 0xFFFFFF
$DARKTXT= 0x33221A
$GRAY   = 0x8A7A6E

$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(0)   # msoFalse = khong mo cua so
$pres.PageSetup.SlideWidth = 960
$pres.PageSetup.SlideHeight = 540
$W = 960; $H = 540

function NewSlide($dark) {
    $s = $pres.Slides.Add($pres.Slides.Count + 1, 12)   # ppLayoutBlank
    $bg = $s.Shapes.AddShape(1, 0, 0, $W, $H)
    $bg.Line.Visible = 0
    if ($dark) { $bg.Fill.ForeColor.RGB = $NAVY } else { $bg.Fill.ForeColor.RGB = 0xF7F3EF }
    $bg.ZOrder(1)   # msoSendToBack? 1 = msoBringToFront... dung: se them truoc moi thu khac nen ok
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
    $r.ParagraphFormat.Alignment = $align   # 1=left 2=center
    return $tb
}
function Chip($s, $x, $y, $w, $h, $text, $fill, $txtColor, $size) {
    $sh = $s.Shapes.AddShape(5, $x, $y, $w, $h)   # rounded rect
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
    TB $s 880 512 70 22 ("SmartPort | " + $num) 10 $GRAY $false 2 | Out-Null
}

# ============ SLIDE 1: COVER ============
$s = NewSlide $true
$bar = $s.Shapes.AddShape(1, 0, 356, $W, 6); $bar.Fill.ForeColor.RGB = $ORANGE; $bar.Line.Visible = 0
TB $s 80 120 800 40 'CUỘC THI Ý TƯỞNG KHỞI NGHIỆP' 16 $ORANGE $true 2 | Out-Null
TB $s 80 165 800 90 'SMARTPORT' 66 $WHITE $true 2 | Out-Null
TB $s 80 262 800 70 ('Nền tảng quản lý cảng và depot logistics' + [char]13 + 'tích hợp AI chạy ngay trên trình duyệt') 22 $WHITE $false 2 | Out-Null
TB $s 80 385 800 30 'Lê Hữu Anh Tài  ·  MSSV 2424802010066' 16 $WHITE $false 2 | Out-Null
TB $s 80 415 800 26 'lehuuanhtai2020tkh@gmail.com  ·  2026' 13 $GRAY $false 2 | Out-Null

# ============ SLIDE 2: VAN DE ============
$s = NewSlide $false
TB $s 50 34 860 46 'VẤN ĐỀ: Cảng nhỏ, nỗi đau lớn' 30 $NAVY $true 1 | Out-Null
$items = @(
    @('16–18% GDP', 'là chi phí logistics của Việt Nam — cao gấp rưỡi trung bình thế giới (tham khảo: Bộ Công Thương)'),
    @('Sổ giấy + Excel', 'là công cụ quản lý của phần lớn depot/bãi container: thất thoát phí lưu bãi, ùn tắc cổng, dữ liệu phân mảnh'),
    @('Hàng triệu USD', 'là giá phần mềm cảng quốc tế (TOS) — ngoài tầm với tuyệt đối của doanh nghiệp vừa và nhỏ'),
    @('3–4 phần mềm rời', 'camera biển số, kế toán, booking, báo cáo... không nói chuyện được với nhau')
)
$y = 100
foreach ($it in $items) {
    Chip $s 50 $y 190 74 $it[0] $NAVY $WHITE 17 | Out-Null
    TB $s 260 ($y + 6) 650 66 $it[1] 15 $DARKTXT $false 1 | Out-Null
    $y += 92
}
Footer $s 2

# ============ SLIDE 3: GIAI PHAP ============
$s = NewSlide $false
TB $s 50 34 860 46 'GIẢI PHÁP: 1 website — trọn nghiệp vụ + AI' 30 $NAVY $true 1 | Out-Null
$cols = @(
    @('NGHIỆP VỤ LÕI', 'Điều độ tàu · bến · bãi container · booking trực tuyến · hóa đơn · phân quyền', $NAVY),
    @('LỚP AI KHAI THÁC', 'Camera YOLO26 · biển số ALPR · Face ID · AI Agent · dự báo Mamba-SSM · tự học', $ORANGE),
    @('THANH TOÁN & KẾ TOÁN', 'VietQR/NAPAS 247 động · đối soát · xuất chứng từ chuẩn MISA', $NAVY2)
)
$x = 50
foreach ($c in $cols) {
    $box = $s.Shapes.AddShape(5, $x, 110, 275, 300)
    $box.Fill.ForeColor.RGB = $WHITE; $box.Line.ForeColor.RGB = $c[2]; $box.Line.Weight = 2.5
    Chip $s ($x + 20) 130 235 44 $c[0] $c[2] $WHITE 15 | Out-Null
    TB $s ($x + 18) 190 240 200 $c[1] 15 $DARKTXT $false 1 | Out-Null
    $x += 293
}
TB $s 50 435 860 60 'Khách hàng chỉ cần trình duyệt web — không cài đặt, không máy chủ AI, triển khai 1–2 tuần.' 17 $NAVY $true 2 | Out-Null
Footer $s 3

# ============ SLIDE 4: DEMO SAN PHAM ============
$s = NewSlide $true
TB $s 50 30 860 42 'SẢN PHẨM THẬT — KHÔNG PHẢI Ý TƯỞNG TRÊN GIẤY' 26 $WHITE $true 1 | Out-Null
Pic $s '17-cong-thong-minh-alpr.png' 70 92 500 | Out-Null
Pic $s '13-login-nut-face.png' 600 92 300 | Out-Null
Pic $s '18-mamba-ssm-du-bao.png' 600 300 300 | Out-Null
TB $s 70 460 500 50 'Cổng thông minh ALPR: AI đọc biển số xe, nhật ký ra/vào tự động' 13 $WHITE $false 1 | Out-Null
TB $s 600 500 300 30 'Face ID + Dự báo lưu lượng' 13 $WHITE $false 1 | Out-Null
Footer $s 4

# ============ SLIDE 5: CONG NGHE AI ============
$s = NewSlide $false
TB $s 50 34 860 46 '10+ CÔNG NGHỆ AI — phủ kín bản đồ trí tuệ nhân tạo' 28 $NAVY $true 1 | Out-Null
$chips = @(
    'YOLO26 (2026) + WebGPU', 'ALPR biển số VN', 'ArcFace Face ID', 'Chống giả mạo MiniFASNet',
    'AI Agent điều phối', 'Mamba-SSM dự báo', 'LSTM OCR container', 'MLP dự báo sản lượng',
    'K-Means phân khúc KH', 'Cây quyết định rủi ro', 'Q-Learning xe nâng', 'NLP-RAG chatbot Việt'
)
$x = 50; $y = 105
for ($i = 0; $i -lt $chips.Count; $i++) {
    $fill = $(if ($i % 3 -eq 1) { $ORANGE } else { $NAVY })
    Chip $s $x $y 275 52 $chips[$i] $fill $WHITE 13 | Out-Null
    $x += 293
    if ($x -gt 700) { $x = 50; $y += 66 }
}
TB $s 50 390 860 90 ('Bí quyết: toàn bộ AI chạy CLIENT-SIDE trên trình duyệt (WebGPU + ONNX Runtime + WebAssembly)' + [char]13 + '=> chi phí máy chủ GPU = 0 đồng => giá bán chỉ bằng 1/10 giải pháp truyền thống.') 17 $NAVY $true 2 | Out-Null
Footer $s 5

# ============ SLIDE 6: THI TRUONG ============
$s = NewSlide $false
TB $s 50 34 860 46 'THỊ TRƯỜNG' 30 $NAVY $true 1 | Out-Null
$rings = @(
    @('TAM', 'Logistics Việt Nam: 40+ tỷ USD/năm, tăng 14–16%/năm', 300, $NAVY),
    @('SAM', '~300 bến cảng + 100+ ICD + hàng trăm depot/bãi tư nhân', 235, $NAVY2),
    @('SOM 3 năm', '80 khách hàng trả phí (~10–15% nhóm depot đang chuyển đổi số)', 170, $ORANGE)
)
$cy = 300
foreach ($r in $rings) {
    $d = $r[2]
    $c = $s.Shapes.AddShape(9, (250 - $d / 2), ($cy - $d / 2), $d, $d)
    $c.Fill.ForeColor.RGB = $r[3]; $c.Fill.Transparency = 0.15; $c.Line.ForeColor.RGB = $WHITE; $c.Line.Weight = 2
}
TB $s 208 172 90 30 'TAM' 16 $WHITE $true 2 | Out-Null
TB $s 208 238 90 30 'SAM' 15 $WHITE $true 2 | Out-Null
TB $s 195 285 116 30 'SOM' 15 $WHITE $true 2 | Out-Null
$y = 120
foreach ($r in $rings) {
    Chip $s 480 $y 120 40 $r[0] $r[3] $WHITE 14 | Out-Null
    TB $s 615 ($y + 2) 300 60 $r[1] 14 $DARKTXT $false 1 | Out-Null
    $y += 105
}
TB $s 480 440 430 60 'Khách hàng "đất lành" đầu tiên: depot container tư nhân 2–15 ha — đau nhất, quyết định nhanh nhất.' 14 $NAVY $true 1 | Out-Null
Footer $s 6

# ============ SLIDE 7: MO HINH KINH DOANH ============
$s = NewSlide $false
TB $s 50 34 860 46 'MÔ HÌNH KINH DOANH — SaaS thuê bao' 30 $NAVY $true 1 | Out-Null
$goi = @(
    @('BASIC', '2,9 triệu/tháng', 'Depot, bãi xe nhỏ', 'Nghiệp vụ lõi + cổng ALPR + thanh toán QR', $NAVY),
    @('PRO', '6,9 triệu/tháng', 'ICD, cảng sông vừa', '+ Camera AI, dự báo, AI Agent, MISA', $ORANGE),
    @('ENTERPRISE', '14,9 triệu/tháng', 'Cảng nhỏ, chuỗi depot', '+ Tùy biến, API, hỗ trợ ưu tiên', $NAVY2)
)
$x = 50
foreach ($g in $goi) {
    $box = $s.Shapes.AddShape(5, $x, 105, 275, 290)
    $box.Fill.ForeColor.RGB = $WHITE; $box.Line.ForeColor.RGB = $g[4]; $box.Line.Weight = 2.5
    Chip $s ($x + 20) 122 235 42 $g[0] $g[4] $WHITE 16 | Out-Null
    TB $s ($x + 18) 175 240 34 $g[1] 21 $g[4] $true 2 | Out-Null
    TB $s ($x + 18) 215 240 30 $g[2] 13 $GRAY $false 2 | Out-Null
    TB $s ($x + 18) 252 240 130 $g[3] 14 $DARKTXT $false 1 | Out-Null
    $x += 293
}
TB $s 50 420 860 70 ('+ Phí triển khai 15–30 triệu/khách (một lần) · dịch vụ tùy biến · thiết bị camera đi kèm' + [char]13 + 'Chi phí biên cực thấp: AI chạy máy khách => 1 máy chủ web phục vụ hàng chục khách hàng.') 15 $DARKTXT $false 2 | Out-Null
Footer $s 7

# ============ SLIDE 8: CANH TRANH ============
$s = NewSlide $false
TB $s 50 34 860 46 'VÌ SAO SMARTPORT THẮNG?' 30 $NAVY $true 1 | Out-Null
$rows = @(
    @('Chi phí triển khai', 'Trăm nghìn USD', 'Chục–trăm triệu', 'Từ 2,9 triệu/tháng'),
    @('AI camera + biển số', 'Module đắt', 'Không có', 'Tích hợp sẵn'),
    @('Máy chủ GPU', 'Bắt buộc', '—', 'KHÔNG cần'),
    @('VietQR + MISA', 'Không', 'Một phần', 'Có sẵn'),
    @('Tự cải thiện (Active Learning)', 'Không', 'Không', 'Có'),
    @('Thời gian triển khai', '6–18 tháng', '1–3 tháng', '1–2 tuần')
)
$headers = @('Tiêu chí', 'TOS quốc tế', 'PM nội địa', 'SmartPort')
$colX = @(50, 330, 540, 730); $colW = @(270, 200, 180, 180)
for ($c = 0; $c -lt 4; $c++) {
    Chip $s $colX[$c] 100 $colW[$c] 40 $headers[$c] $(if ($c -eq 3) { $ORANGE } else { $NAVY }) $WHITE 14 | Out-Null
}
$y = 148
foreach ($r in $rows) {
    for ($c = 0; $c -lt 4; $c++) {
        $bold = ($c -eq 3)
        $color = $(if ($c -eq 3) { $NAVY } else { $DARKTXT })
        TB $s ($colX[$c] + 6) $y ($colW[$c] - 12) 46 $r[$c] 13 $color $bold 1 | Out-Null
    }
    $ln = $s.Shapes.AddShape(1, 50, ($y + 44), 860, 1); $ln.Fill.ForeColor.RGB = 0xD8CFC8; $ln.Line.Visible = 0
    $y += 52
}
Footer $s 8

# ============ SLIDE 9: TRACTION ============
$s = NewSlide $true
TB $s 50 34 860 44 'TRACTION — NHỮNG GÌ ĐÃ LÀM ĐƯỢC' 28 $WHITE $true 1 | Out-Null
$done = @(
    'Sản phẩm HOÀN CHỈNH đang chạy thật trên hosting công khai',
    'Thanh toán VietQR/NAPAS 247 sinh mã động — đã test giao dịch thật',
    'Kết nối kế toán MISA (xuất chứng từ đúng mẫu nhập)',
    '10+ mô hình AI hoạt động: từ thị giác máy tính tới học tăng cường',
    'Bộ tài liệu kỹ thuật + báo cáo chi tiết sẵn sàng chuyển giao'
)
$y = 100
foreach ($d in $done) {
    Chip $s 50 $y 34 34 ([char]0x2713) $ORANGE $WHITE 16 | Out-Null
    TB $s 100 ($y + 2) 480 40 $d 15 $WHITE $false 1 | Out-Null
    $y += 62
}
Pic $s '10-thanh-toan-qr.png' 610 100 300 | Out-Null
Pic $s '08-ai-agent.png' 610 315 300 | Out-Null
Footer $s 9

# ============ SLIDE 10: TAI CHINH ============
$s = NewSlide $false
TB $s 50 34 860 46 'TÀI CHÍNH DỰ KIẾN 3 NĂM (thận trọng)' 28 $NAVY $true 1 | Out-Null
$fin = @(
    @('Khách hàng', '10', '35', '80'),
    @('Tổng doanh thu', '0,58 tỷ', '2,3 tỷ', '5,4 tỷ'),
    @('Chi phí', '0,70 tỷ', '1,6 tỷ', '3,2 tỷ'),
    @('Lợi nhuận', '-0,12 tỷ', '+0,7 tỷ', '+2,2 tỷ')
)
$colX2 = @(50, 330, 520, 710); $colW2 = @(270, 180, 180, 200)
$hd = @('Chỉ tiêu', 'Năm 1', 'Năm 2', 'Năm 3')
for ($c = 0; $c -lt 4; $c++) { Chip $s $colX2[$c] 105 $colW2[$c] 42 $hd[$c] $NAVY $WHITE 15 | Out-Null }
$y = 160
foreach ($r in $fin) {
    for ($c = 0; $c -lt 4; $c++) {
        TB $s ($colX2[$c] + 8) $y ($colW2[$c] - 16) 40 $r[$c] 16 $(if ($r[0] -eq 'Lợi nhuận' -and $c -gt 0) { $(if ($r[$c].StartsWith('-')) { 0x2626DC } else { 0x3D8015 }) } else { $DARKTXT }) $(($c -eq 0)) 1 | Out-Null
    }
    $ln = $s.Shapes.AddShape(1, 50, ($y + 40), 860, 1); $ln.Fill.ForeColor.RGB = 0xD8CFC8; $ln.Line.Visible = 0
    $y += 52
}
Chip $s 50 400 860 60 'Hòa vốn giữa Năm 2 (~25–28 khách). Giả định: gói TB 5 triệu/tháng, rời bỏ 10%/năm.' $ORANGE $WHITE 16 | Out-Null
Footer $s 10

# ============ SLIDE 11: LO TRINH + DOI NGU ============
$s = NewSlide $false
TB $s 50 34 860 46 'LỘ TRÌNH & ĐỘI NGŨ' 30 $NAVY $true 1 | Out-Null
$road = @(
    @('HIỆN TẠI', 'Sản phẩm hoàn chỉnh chạy thật'),
    @('T1–3', 'Pháp lý + bản quyền AI + thí điểm 3 depot'),
    @('T4–9', 'Thương mại hóa: 10 khách trả phí đầu tiên'),
    @('T10–24', '35+ khách · tích hợp barrier/cân · gọi vốn hạt giống')
)
$x = 50
foreach ($r in $road) {
    Chip $s $x 105 200 46 $r[0] $NAVY $WHITE 14 | Out-Null
    TB $s ($x + 4) 160 200 110 $r[1] 13 $DARKTXT $false 1 | Out-Null
    if ($x -lt 700) {
        $ar = $s.Shapes.AddShape(33, ($x + 205), 118, 18, 20)   # right arrow
        $ar.Fill.ForeColor.RGB = $ORANGE; $ar.Line.Visible = 0
    }
    $x += 228
}
$ln2 = $s.Shapes.AddShape(1, 50, 290, 860, 2); $ln2.Fill.ForeColor.RGB = 0xD8CFC8; $ln2.Line.Visible = 0
TB $s 50 310 860 34 'ĐỘI NGŨ' 20 $NAVY $true 1 | Out-Null
TB $s 50 350 860 130 ('Lê Hữu Anh Tài — Founder, Sản phẩm & Công nghệ: tự tay xây toàn bộ nền tảng (full-stack + 10 mô hình AI).' + [char]13 + 'Đang tìm 2 đồng sáng lập: Kinh doanh logistics (có quan hệ depot/hãng tàu) và Vận hành – CSKH.' + [char]13 + 'Cố vấn: giảng viên CNTT + chuyên gia khai thác cảng.') 15 $DARKTXT $false 1 | Out-Null
Footer $s 11

# ============ SLIDE 12: ASK ============
$s = NewSlide $true
TB $s 50 60 860 46 'KÊU GỌI' 30 $ORANGE $true 2 | Out-Null
TB $s 50 115 860 80 '300 TRIỆU ĐỒNG · 6 THÁNG TĂNG TỐC' 40 $WHITE $true 2 | Out-Null
$asks = @(
    @('40%', 'Nhân sự: 2 dev + 1 kinh doanh'),
    @('25%', 'Pháp lý + bản quyền AI thương mại'),
    @('20%', 'Thí điểm 3 depot + thiết bị demo'),
    @('15%', 'Hạ tầng + dự phòng')
)
$x = 70
foreach ($a in $asks) {
    $box = $s.Shapes.AddShape(5, $x, 230, 195, 150)
    $box.Fill.ForeColor.RGB = $NAVY2; $box.Line.ForeColor.RGB = $ORANGE; $box.Line.Weight = 1.5
    TB $s $x 252 195 44 $a[0] 30 $ORANGE $true 2 | Out-Null
    TB $s ($x + 10) 300 175 70 $a[1] 13 $WHITE $false 2 | Out-Null
    $x += 212
}
TB $s 50 430 860 60 'Đổi lại: quyền ưu tiên đàm phán cổ phần vòng hạt giống + gói Enterprise trọn đời.' 16 $WHITE $false 2 | Out-Null
Footer $s 12

# ============ SLIDE 13: CAM ON ============
$s = NewSlide $true
$bar2 = $s.Shapes.AddShape(1, 0, 330, $W, 6); $bar2.Fill.ForeColor.RGB = $ORANGE; $bar2.Line.Visible = 0
TB $s 80 150 800 70 'SMARTPORT' 54 $WHITE $true 2 | Out-Null
TB $s 80 235 800 44 'Cảng thông minh trong tầm tay mọi doanh nghiệp Việt' 22 $ORANGE $true 2 | Out-Null
TB $s 80 370 800 34 'EM XIN CẢM ƠN BAN GIÁM KHẢO!' 20 $WHITE $true 2 | Out-Null
TB $s 80 415 800 30 'Demo trực tiếp: smartport-anhtai.somee.com  ·  lehuuanhtai2020tkh@gmail.com' 14 $GRAY $false 2 | Out-Null

# ---- Luu ----
$pres.SaveAs($outPptx, 24)                 # ppSaveAsOpenXMLPresentation
$pres.SaveAs($outPdf, 32)                  # ppSaveAsPDF
$count = $pres.Slides.Count
# Xuat 3 slide QA
$pres.Slides.Item(1).Export((Join-Path $outDir "qa-slide1.png"), "PNG", 960, 540)
$pres.Slides.Item(4).Export((Join-Path $outDir "qa-slide4.png"), "PNG", 960, 540)
$pres.Slides.Item(7).Export((Join-Path $outDir "qa-slide7.png"), "PNG", 960, 540)
$pres.Close()
$pp.Quit()
Write-Output "DA TAO PITCH DECK: $count slide + PDF"
