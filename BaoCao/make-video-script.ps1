# ============================================================
# KICH BAN QUAY VIDEO DEMO — SmartPort (Word COM)
# ============================================================
$ErrorActionPreference = 'Stop'
$outDocx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\KichBan_Video_Demo_SmartPort.docx'
$outPdf  = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\KichBan_Video_Demo_SmartPort.pdf'

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$doc = $word.Documents.Add()
$sel = $word.Selection

$ps = $doc.PageSetup
$ps.PageWidth = $word.CentimetersToPoints(21)
$ps.PageHeight = $word.CentimetersToPoints(29.7)
$ps.TopMargin = $word.CentimetersToPoints(2)
$ps.BottomMargin = $word.CentimetersToPoints(2)
$ps.LeftMargin = $word.CentimetersToPoints(2.5)
$ps.RightMargin = $word.CentimetersToPoints(2)

$navy = 87*65536 + 47*256 + 10
$cam  = 45*65536 + 125*256 + 229
$xanhDam = 17 + 39*256 + 68*65536
$xanhNhat = 219 + 229*256 + 241*65536

$normal = $doc.Styles(-1)
$normal.Font.Name = 'Times New Roman'; $normal.Font.Size = 13
$normal.ParagraphFormat.LineSpacingRule = 1
$normal.ParagraphFormat.SpaceAfter = 6
$normal.ParagraphFormat.Alignment = 3

$h1 = $doc.Styles(-2); $h1.Font.Name = 'Times New Roman'; $h1.Font.Size = 15; $h1.Font.Bold = $true; $h1.Font.Color = $navy; $h1.ParagraphFormat.SpaceBefore = 12; $h1.ParagraphFormat.SpaceAfter = 6
$h2 = $doc.Styles(-3); $h2.Font.Name = 'Times New Roman'; $h2.Font.Size = 13; $h2.Font.Bold = $true; $h2.Font.Color = $navy; $h2.ParagraphFormat.SpaceBefore = 8; $h2.ParagraphFormat.SpaceAfter = 3

function P($t, $align = 3, $bold = $false, $italic = $false, $size = 13, $color = 0) {
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = $align
    $sel.Font.Bold = [int]$bold; $sel.Font.Italic = [int]$italic; $sel.Font.Size = $size; $sel.Font.Color = $color
    $sel.TypeText($t); $sel.TypeParagraph()
    $sel.Font.Bold = 0; $sel.Font.Italic = 0; $sel.Font.Size = 13; $sel.Font.Color = 0
}
function H1($t) { $sel.Style = $doc.Styles(-2); $sel.TypeText($t); $sel.TypeParagraph(); $sel.Style = $doc.Styles(-1) }
function H2($t) { $sel.Style = $doc.Styles(-3); $sel.TypeText($t); $sel.TypeParagraph(); $sel.Style = $doc.Styles(-1) }
function PB() { $sel.InsertBreak(7) }

function Bang($headers, $rows, $caption) {
    $cols = $headers.Count
    $tbl = $doc.Tables.Add($sel.Range, $rows.Count + 1, $cols)
    $tbl.Borders.Enable = 1
    $tbl.Range.Font.Size = 11
    $tbl.Range.ParagraphFormat.SpaceAfter = 2
    for ($c = 0; $c -lt $cols; $c++) {
        $cell = $tbl.Cell(1, $c + 1)
        $cell.Range.Text = $headers[$c]
        $cell.Range.Font.Bold = $true
        $cell.Range.Font.Color = 16777215
        $cell.Shading.BackgroundPatternColor = $navy
    }
    for ($r = 0; $r -lt $rows.Count; $r++) {
        for ($c = 0; $c -lt $cols; $c++) { $tbl.Cell($r + 2, $c + 1).Range.Text = [string]$rows[$r][$c] }
    }
    $end = $tbl.Range.End
    $sel.SetRange($end, $end)
    $sel.TypeParagraph()
    if ($caption) {
        $sel.Font.Size = 11; $sel.Font.Italic = $true; $sel.Font.Bold = $true
        $sel.ParagraphFormat.Alignment = 1
        $sel.TypeText($caption); $sel.TypeParagraph()
        $sel.Font.Size = 13; $sel.Font.Italic = $false; $sel.Font.Bold = $false
        $sel.ParagraphFormat.Alignment = 3
    }
}

# Mot canh (scene) trong video: thoi diem, URL/thao tac, loi thuyet minh
function Canh($num, $time, $title, $url, $action, $narration) {
    $sel.Font.Bold = $true; $sel.Font.Color = $navy; $sel.Font.Size = 13
    $sel.TypeText("CẢNH $num  [$time]  —  $title"); $sel.TypeParagraph()
    $sel.Font.Bold = $false; $sel.Font.Color = 0; $sel.Font.Size = 13
    if ($url) {
        $sel.Font.Bold = $true; $sel.TypeText("URL / Vị trí: "); $sel.Font.Bold = $false; $sel.Font.Color = 46 + 111*256 + 17*65536
        $sel.TypeText($url); $sel.TypeParagraph(); $sel.Font.Color = 0
    }
    $sel.Font.Bold = $true; $sel.TypeText("Thao tác quay: "); $sel.Font.Bold = $false
    $sel.TypeText($action); $sel.TypeParagraph()
    $sel.Font.Bold = $true; $sel.TypeText("Lời thuyết minh (đọc khi quay hoặc lồng tiếng sau): "); $sel.Font.Bold = $false
    $sel.Font.Italic = $true; $sel.Font.Color = $xanhDam
    $sel.TypeText('"' + $narration + '"'); $sel.TypeParagraph()
    $sel.Font.Italic = $false; $sel.Font.Color = 0
    $sel.TypeParagraph()
}

# ============================================================
# TIEU DE
# ============================================================
P 'KỊCH BẢN QUAY VIDEO DEMO SẢN PHẨM' 1 $true $false 18 $navy
P 'Website quản lý cảng trong ngành Logistics — Hệ thống SmartPort' 1 $true $true 14 $cam
P 'SV: Lê Hữu Anh Tài — 2424802010066  ·  GVHD: Ths. Nguyễn Thành Trung' 1 $false $false 12
P ''
P 'Tài liệu này hướng dẫn quay một video demo khoảng 6–8 phút, dùng công cụ QUAY MÀN HÌNH MIỄN PHÍ CÓ SẴN TRONG WINDOWS — Xbox Game Bar — không cần cài thêm phần mềm. Video gồm 12 cảnh, mỗi cảnh có: URL cần mở, thao tác cụ thể, và lời thuyết minh gợi ý.' 3 $false $true 12
PB

# ============================================================
# PHAN A: CHUAN BI
# ============================================================
H1 'A. CHUẨN BỊ TRƯỚC KHI QUAY (làm 1 lần)'
H2 '1. Mở Xbox Game Bar (quay màn hình có sẵn trong Windows 10/11 — miễn phí, không cần cài đặt)'
P '• Nhấn tổ hợp phím Windows + G để mở Xbox Game Bar.'
P '• Nếu lần đầu dùng, Windows hỏi "Bạn có muốn mở Xbox Game Bar không?" → chọn Có (Yes).'
P '• Trong bảng hiện ra, tìm ô Capture (Chụp) — có biểu tượng camera. Nếu không thấy, nhấn Windows + Alt + R để BẮT ĐẦU quay trực tiếp (sẽ hiện chấm đỏ nhỏ ở góc màn hình).'
P '• Quay lại: nhấn Windows + Alt + R để DỪNG quay. File video (.mp4) tự động lưu tại thư mục: This PC → Videos → Captures.'
P '• Mẹo: trước khi quay, mở sẵn trình duyệt Edge/Chrome full màn hình để video rõ nét, chữ dễ đọc.'
H2 '2. Chuẩn bị hệ thống'
P '• Mở Visual Studio → mở SmartPort.sln → nhấn F5 để chạy website (đợi trình duyệt tự mở).'
P '• Đăng nhập sẵn 2 tab trình duyệt: 1 tab bằng tài khoản admin (xem khu quản trị + AI), 1 tab ẩn danh chưa đăng nhập (xem trang công khai).'
P '• Chuẩn bị sẵn 1 ảnh có xe tải/người (để demo Camera AI YOLO) và có thể dùng ngay ảnh mẫu tích hợp sẵn cho OCR — không cần chuẩn bị.'
P '• Tắt thông báo (Windows Focus Assist) để tránh pop-up làm phiền lúc quay.'
H2 '3. Cấu trúc video (tổng ~7 phút)'
Bang @('Cảnh', 'Nội dung', 'Thời lượng') @(
    @('1', 'Mở đầu — giới thiệu đề tài', '20s'),
    @('2', 'Trang chủ — tổng quan giao diện', '35s'),
    @('3', 'Tra cứu container + OCR AI (Deep Learning #2)', '40s'),
    @('4', 'Chatbot PortBot (gõ + giọng nói)', '30s'),
    @('5', 'Ước tính chi phí AI', '25s'),
    @('6', 'Đăng ký/đăng nhập + đặt dịch vụ ePort', '40s'),
    @('7', 'Hóa đơn 3 kênh: Excel, PDF, Email', '35s'),
    @('8', 'Dashboard quản trị', '25s'),
    @('9', 'AI phân bổ bến tàu', '35s'),
    @('10', 'Camera AI — YOLOv10 (Deep Learning #1) ★ ĐIỂM NHẤN', '60s'),
    @('11', 'AI Lab — huấn luyện mạng nơ-ron (Deep Learning #3) ★ ĐIỂM NHẤN', '55s'),
    @('12', 'Dark mode + kết thúc', '20s')
) 'Bảng phân bổ thời lượng 12 cảnh quay'
PB

# ============================================================
# PHAN B: KICH BAN TUNG CANH
# ============================================================
H1 'B. KỊCH BẢN CHI TIẾT TỪNG CẢNH QUAY'

Canh 1 '0:00–0:20' 'Mở đầu' 'http://localhost:52080 (hoặc cổng Visual Studio đang chạy)' `
    'Quay màn hình Desktop 2 giây, sau đó mở trình duyệt vào trang chủ.' `
    'Xin chào thầy cô, em là Lê Hữu Anh Tài. Đây là video demo đồ án Website quản lý cảng trong ngành Logistics — hệ thống SmartPort, tích hợp 10 công nghệ AI với 3 mô hình Deep Learning chạy thật trên trình duyệt.'

Canh 2 '0:20–0:55' 'Trang chủ' '/' `
    'Cuộn chậm từ trên xuống: ticker trực tiếp → hero slider → thống kê → lưới 10 AI → dịch vụ → lịch tàu → thư viện ảnh → footer.' `
    'Trang chủ hiển thị dữ liệu chuyến tàu thời gian thực, thống kê lấy trực tiếp từ cơ sở dữ liệu, và nổi bật là lưới 10 công nghệ AI đang vận hành cùng hệ thống.'

Canh 3 '0:55–1:35' 'Tra cứu container + OCR' '/Tracking' `
    'Nhập số MSCU2201417 vào ô tra cứu, bấm Tra cứu — cho xem kết quả. Sau đó kéo xuống khung OCR, bấm nút "Chạy thử với ảnh container mẫu" — chờ AI đọc xong, quay cận cảnh kết quả nhận diện.' `
    'Đây là mô hình Deep Learning thứ hai: mạng nơ-ron LSTM đọc ký tự từ ảnh container và tự động điền số vào ô tra cứu, độ chính xác đạt 93% trong thử nghiệm của em.'

Canh 4 '1:35–2:05' 'Chatbot PortBot' 'Mọi trang — biểu tượng chat góc phải' `
    'Bấm biểu tượng chat, gõ "lịch tàu tuần này" — cho xem câu trả lời. Bấm nút micro, nói to "giá bốc xếp container" — cho xem chatbot nghe và trả lời.' `
    'PortBot trả lời bằng dữ liệu thật lấy trực tiếp từ cơ sở dữ liệu, và hỗ trợ cả hỏi bằng giọng nói tiếng Việt.'

Canh 5 '2:05–2:30' 'Ước tính chi phí AI' '/Cost' `
    'Chọn loại container 40HC, nhập số lượng 5, tick vài dịch vụ kèm, bấm "AI tính chi phí ngay" — cho xem bảng chi tiết, biểu đồ và nhận xét AI.' `
    'Công cụ này tính chi phí chi tiết theo biểu giá thật và tự sinh nhận xét thông minh giúp khách hàng tiết kiệm chi phí.'

Canh 6 '2:30–3:10' 'Đăng ký & đặt dịch vụ ePort' '/Account/Register rồi /Booking/Create' `
    'Lướt nhanh qua form đăng ký (không cần điền hết). Sau đó đăng nhập tài khoản taile, vào Đặt dịch vụ, chọn 2–3 dịch vụ với số lượng, cho xem tổng tiền tự động cập nhật, bấm Gửi yêu cầu.' `
    'Khách hàng doanh nghiệp đăng ký tài khoản để đặt dịch vụ cảng trực tuyến, hệ thống tự tính tiền ngay khi nhập số lượng.'

Canh 7 '3:10–3:45' 'Hóa đơn 3 kênh' '/Booking/My rồi vào chi tiết booking' `
    'Vào một booking đã duyệt, bấm lần lượt 3 nút: Tải hóa đơn Excel (cho xem file mở ra), Hóa đơn PDF (cho xem bản in đẹp), và Gửi email (cho xem thông báo đã gửi).' `
    'Hệ thống phát hành hóa đơn theo ba kênh: Excel chuẩn kế toán tự đọc số tiền thành chữ, bản in PDF, và gửi email tới khách hàng.'

Canh 8 '3:45–4:10' 'Dashboard quản trị' '/Admin (đăng nhập admin)' `
    'Đăng nhập admin, cho xem tổng quan dashboard: 6 thẻ chỉ số, panel cảnh báo AI, biểu đồ dự báo.' `
    'Khu quản trị có dashboard tổng hợp chỉ số thời gian thực và một trợ lý AI tự động cảnh báo các vấn đề vận hành.'

Canh 9 '4:10–4:45' 'AI phân bổ bến' '/Admin/AiTools/Berth' `
    'Cho xem danh sách chuyến tàu chưa có bến, AI gợi ý 2–3 bến kèm điểm số và lý do, bấm "Phân bến này" để áp dụng — cho xem thông báo thành công.' `
    'AI chấm điểm từng bến dựa trên ràng buộc an toàn hàng hải thực tế, và dự đoán thời gian giải phóng tàu, áp dụng chỉ bằng một cú nhấp.'

Canh 10 '4:45–5:45' 'Camera AI — YOLOv10 ★ ĐIỂM NHẤN' '/Admin/Vision' `
    'Đợi mô hình tải xong (badge chuyển xanh). Bấm ảnh mẫu "Xe đầu kéo trên đường" — quay cận cảnh khung nhận diện + % tin cậy hiện ra. QUAN TRỌNG: nếu có webcam, bấm "Bật webcam AI" và giơ điện thoại/cốc nước trước camera để AI nhận diện REAL-TIME — đây là cảnh ấn tượng nhất video.' `
    'Đây là mô hình Deep Learning quan trọng nhất: YOLOv10, thế hệ mới nhất của họ mô hình YOLO, chạy suy luận hoàn toàn trên trình duyệt bằng ONNX Runtime, không cần máy chủ GPU. Kết quả nhận diện đạt độ tin cậy 87%, tốc độ dưới 300 mili giây mỗi khung hình.'

Canh 11 '5:45–6:40' 'AI Lab — huấn luyện mạng nơ-ron ★ ĐIỂM NHẤN' '/Admin/AiLab' `
    'Kéo thanh epoch lên khoảng 100–150, bấm "Bắt đầu huấn luyện" — quay cận cảnh đường loss đang giảm dần theo thời gian thực, và biểu đồ so sánh dự báo AI với hồi quy tuyến tính khi huấn luyện xong.' `
    'Đây là mô hình Deep Learning thứ ba: một mạng nơ-ron nhân tạo được huấn luyện trực tiếp ngay trên trình duyệt bằng TensorFlow.js để dự báo sản lượng cảng, quý thầy cô có thể thấy đường mất mát giảm dần theo từng epoch ngay trước mắt.'

Canh 12 '6:40–7:00' 'Dark mode & kết thúc' 'Bất kỳ trang nào' `
    'Bấm nút chuyển giao diện Tối ở góc phải, cho xem toàn trang đổi màu mượt mà. Kết thúc bằng cảnh trang chủ.' `
    'Website hỗ trợ chế độ giao diện Tối đồng bộ toàn hệ thống. Trên đây là toàn bộ demo sản phẩm của em, em xin cảm ơn quý thầy cô đã theo dõi!'

PB

# ============================================================
# PHAN C: HAU KY
# ============================================================
H1 'C. HẬU KỲ — GHÉP VÀ NỘP VIDEO'
H2 '1. Tìm file video vừa quay'
P 'Mở File Explorer → This PC → Videos → Captures. File có tên dạng "Microsoft Edge 2026-07-19 ...mp4".'
H2 '2. Cắt/ghép (tuỳ chọn, dùng ứng dụng Clipchamp có sẵn trong Windows 11 — miễn phí)'
P '• Nếu quay nhiều đoạn nhỏ theo từng cảnh: mở ứng dụng Clipchamp (gõ "Clipchamp" vào ô tìm kiếm Windows) → Tạo video mới → kéo thả các đoạn theo đúng thứ tự 12 cảnh ở trên → Xuất video (Export) chất lượng 1080p.'
P '• Nếu quay liền một mạch theo kịch bản: chỉ cần cắt bỏ đầu/cuối thừa, không cần ghép.'
H2 '3. Đặt tên file theo chuẩn nộp bài'
P 'Video_Demo_SmartPort_LeHuuAnhTai_2424802010066.mp4' 3 $true
H2 '4. Nén và tải lên Google Drive'
P '• Nếu file video dưới 100MB, có thể nộp trực tiếp không cần nén.'
P '• Nếu cần nén: chuột phải vào file video → Send to → Compressed (zipped) folder.'
P '• Vào drive.google.com → đăng nhập Gmail của em → New → File upload → chọn video (hoặc file zip).'
P '• Sau khi tải lên xong, chuột phải vào file → Share (Chia sẻ) → đổi quyền "Restricted" thành "Anyone with the link" (Bất kỳ ai có đường liên kết) → Copy link → dán vào bài nộp.'
P ''
P 'Chúc bạn quay video demo thành công và đạt điểm A+!' 1 $true $true 13 $cam

# ---------- Luu ----------
if (Test-Path $outDocx) { [IO.File]::Delete($outDocx) }
$doc.SaveAs2($outDocx, 16)
$doc.ExportAsFixedFormat($outPdf, 17)
$pages = $doc.ComputeStatistics(2)
$doc.Close($false)
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
"XONG: $outDocx — $pages trang"
