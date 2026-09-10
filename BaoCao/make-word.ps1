# ============================================================
# TAO BAO CAO DO AN WORD — SmartPort (chay bang Word COM)
# Theo dung mau bao cao cua giao vien (Mau bao cao Lap trinh WEB.docx)
# ============================================================
$ErrorActionPreference = 'Stop'
$imgDir = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\img-doc'
$outDocx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\_new_BaoCao_DoAn_SmartPort.docx'
$outPdf  = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\_new_BaoCao_DoAn_SmartPort.pdf'

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$doc = $word.Documents.Add()
$sel = $word.Selection

# ---------- Thiet lap trang & style ----------
$ps = $doc.PageSetup
$ps.PageWidth = $word.CentimetersToPoints(21)
$ps.PageHeight = $word.CentimetersToPoints(29.7)
$ps.TopMargin = $word.CentimetersToPoints(2)
$ps.BottomMargin = $word.CentimetersToPoints(2)
$ps.LeftMargin = $word.CentimetersToPoints(3)
$ps.RightMargin = $word.CentimetersToPoints(2)

$normal = $doc.Styles(-1)          # wdStyleNormal
$normal.Font.Name = 'Times New Roman'
$normal.Font.Size = 13
$normal.ParagraphFormat.LineSpacingRule = 1   # 1.5 line
$normal.ParagraphFormat.SpaceAfter = 6
$normal.ParagraphFormat.Alignment = 3         # justify

$navy = 87*65536 + 47*256 + 10                # #0A2F57 -> BGR
$cam  = 45*65536 + 125*256 + 255
$xanhNhat = 219 + 229*256 + 241*65536

$h1 = $doc.Styles(-2); $h1.Font.Name = 'Times New Roman'; $h1.Font.Size = 16; $h1.Font.Bold = $true; $h1.Font.Color = $navy; $h1.ParagraphFormat.SpaceBefore = 12; $h1.ParagraphFormat.SpaceAfter = 10
$h2 = $doc.Styles(-3); $h2.Font.Name = 'Times New Roman'; $h2.Font.Size = 14; $h2.Font.Bold = $true; $h2.Font.Color = $navy; $h2.ParagraphFormat.SpaceBefore = 10
$h3 = $doc.Styles(-4); $h3.Font.Name = 'Times New Roman'; $h3.Font.Size = 13; $h3.Font.Bold = $true; $h3.Font.Italic = $true; $h3.Font.Color = $navy

# ---------- Bien theo doi so chuong / hinh / bang (de lam Danh muc Bang, Danh muc Hinh) ----------
$script:chapNo = 0
$script:figNo = 0
$script:tblNo = 0
$script:figList = @()
$script:tblList = @()

# ---------- Ham tien ich ----------
function P($t, $align = 3, $bold = $false, $size = 13, $italic = $false) {
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = $align
    if ($align -eq 3 -or $align -eq 0) { $sel.ParagraphFormat.FirstLineIndent = $word.CentimetersToPoints(1) } else { $sel.ParagraphFormat.FirstLineIndent = 0 }
    $sel.Font.Bold = [int]$bold
    $sel.Font.Italic = [int]$italic
    $sel.Font.Size = $size
    $sel.TypeText($t)
    $sel.TypeParagraph()
    $sel.Font.Bold = 0; $sel.Font.Italic = 0; $sel.Font.Size = 13
}
function H1($t) {
    $sel.Style = $doc.Styles(-2)
    $sel.ParagraphFormat.Alignment = 1
    $sel.ParagraphFormat.FirstLineIndent = 0
    $sel.TypeText($t); $sel.TypeParagraph()
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = 3
}
function H2($t) {
    $sel.Style = $doc.Styles(-3)
    $sel.ParagraphFormat.Alignment = 0
    $sel.ParagraphFormat.FirstLineIndent = 0
    $sel.TypeText($t); $sel.TypeParagraph()
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = 3
}
function H3($t) {
    $sel.Style = $doc.Styles(-4)
    $sel.ParagraphFormat.Alignment = 0
    $sel.ParagraphFormat.FirstLineIndent = 0
    $sel.TypeText($t); $sel.TypeParagraph()
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = 3
}
function PB() { $sel.InsertBreak(7) }

function Img($file, $cap, $wCm = 15.5, $ext = 'jpg') {
    $path = Join-Path $imgDir "$file.$ext"
    if (-not (Test-Path $path)) { return }
    $script:figNo++
    $numLabel = if ($script:chapNo -eq 0) { "$script:figNo" } else { "$script:chapNo.$script:figNo" }
    $sel.ParagraphFormat.Alignment = 1
    $sel.ParagraphFormat.FirstLineIndent = 0
    $pic = $sel.InlineShapes.AddPicture($path)
    $pic.LockAspectRatio = -1
    $maxH = $word.CentimetersToPoints(19)
    $pic.Width = $word.CentimetersToPoints($wCm)
    if ($pic.Height -gt $maxH) { $pic.Height = $maxH }
    $sel.TypeParagraph()
    $bm = "Fig$($script:chapNo)_$($script:figNo)"
    $doc.Bookmarks.Add($bm, $sel.Range) | Out-Null
    $sel.Font.Size = 11; $sel.Font.Italic = $true; $sel.Font.Bold = $true
    $sel.ParagraphFormat.Alignment = 1
    $sel.TypeText("Hình $numLabel. $cap")
    $sel.TypeParagraph()
    $script:figList += ,@($numLabel, $cap, $bm)
    $sel.Font.Size = 13; $sel.Font.Italic = $false; $sel.Font.Bold = $false
    $sel.ParagraphFormat.Alignment = 3
}
function Bang($headers, $rows, $capBase, $colWidths) {
    $script:tblNo++
    $numLabel = if ($script:chapNo -eq 0) { "$script:tblNo" } else { "$script:chapNo.$script:tblNo" }
    $cols = $headers.Count
    $tbl = $doc.Tables.Add($sel.Range, $rows.Count + 1, $cols)
    $tbl.Borders.Enable = 1
    $tbl.Range.Font.Size = 11
    $tbl.Range.ParagraphFormat.LineSpacingRule = 0
    $tbl.Range.ParagraphFormat.SpaceAfter = 2
    $tbl.Range.ParagraphFormat.FirstLineIndent = 0
    if ($colWidths) {
        for ($c = 0; $c -lt $cols; $c++) { $tbl.Columns.Item($c+1).Width = $word.CentimetersToPoints($colWidths[$c]) }
    }
    for ($c = 0; $c -lt $cols; $c++) {
        $cell = $tbl.Cell(1, $c + 1)
        $cell.Range.Text = $headers[$c]
        $cell.Range.Font.Bold = $true
        $cell.Range.Font.Color = 16777215
        $cell.Shading.BackgroundPatternColor = $navy
    }
    for ($r = 0; $r -lt $rows.Count; $r++) {
        for ($c = 0; $c -lt $cols; $c++) {
            $tbl.Cell($r + 2, $c + 1).Range.Text = [string]$rows[$r][$c]
        }
    }
    $end = $tbl.Range.End
    $sel.SetRange($end, $end)
    $sel.TypeParagraph()
    $bm = "Tbl$($script:chapNo)_$($script:tblNo)"
    $doc.Bookmarks.Add($bm, $sel.Range) | Out-Null
    $sel.Font.Size = 11; $sel.Font.Italic = $true; $sel.Font.Bold = $true
    $sel.ParagraphFormat.Alignment = 1
    $sel.TypeText("Bảng $numLabel. $capBase")
    $sel.TypeParagraph()
    $script:tblList += ,@($numLabel, $capBase, $bm)
    $sel.Font.Size = 13; $sel.Font.Italic = $false; $sel.Font.Bold = $false
    $sel.ParagraphFormat.Alignment = 3
}

# ============================================================
# TRANG BIA — theo dung Mau bao cao Lap trinh WEB cua giao vien
# ============================================================
$logoPath = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\img-doc\logo-tdmu.png'
$GVHD = 'ThS. Nguyễn Thành Trung'

$sel.ParagraphFormat.SpaceBefore = 10; $sel.ParagraphFormat.SpaceAfter = 2
P 'TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT' 1 $true 15
P 'VIỆN CHUYỂN ĐỔI SỐ' 1 $true 14
P '*****' 1 $true 13
$sel.ParagraphFormat.SpaceAfter = 6

$sel.ParagraphFormat.Alignment = 1
$logo = $sel.InlineShapes.AddPicture($logoPath)
$logo.LockAspectRatio = -1
$logo.Width = $word.CentimetersToPoints(7.2)
$sel.TypeParagraph()
P '' 1 $false 8

P 'ĐỒ ÁN MÔN HỌC' 1 $true 22
P 'LẬP TRÌNH WEB' 1 $true 18
P '' 1 $false 10

$sel.Font.Color = $navy
P 'XÂY DỰNG WEBSITE QUẢN LÝ CẢNG' 1 $true 18
P 'TRONG NGÀNH LOGISTICS' 1 $true 18
P '(HỆ THỐNG SMARTPORT)' 1 $true 15
$sel.Font.Color = 0
P '' 1

$sel.ParagraphFormat.Alignment = 0
$sel.ParagraphFormat.FirstLineIndent = 0
P 'Thành viên thực hiện:' 0 $true 13
P '        Họ và tên: Lê Hữu Anh Tài, MSSV: 2424802010066' 0 $false 13
P '        Họ và tên: Đặng Anh Kiệt, MSSV: (chưa cập nhật)' 0 $false 13
P '' 0
P "GVHD: $GVHD" 0 $true 13
P '' 0
P '' 0
P '' 0
P 'Thành phố Hồ Chí Minh, tháng 07/2026' 0 $true 13

$sel.InsertBreak(2)   # wdSectionBreakNextPage: bia thanh Section 1 rieng
$sel.ParagraphFormat.SpaceBefore = 0
$sel.ParagraphFormat.Alignment = 3

# ============================================================
# LOI CAM ON + NHAN XET
# ============================================================
H1 'LỜI CẢM ƠN'
P 'Trước tiên, em xin gửi lời cảm ơn chân thành đến quý thầy cô Viện Chuyển đổi số đã tận tình giảng dạy, truyền đạt những kiến thức nền tảng và chuyên sâu về lập trình web trong suốt học phần. Đặc biệt, em xin cảm ơn thầy Nguyễn Thành Trung — giảng viên hướng dẫn — đã định hướng đề tài, góp ý về kiến trúc hệ thống cũng như động viên em mạnh dạn ứng dụng các công nghệ trí tuệ nhân tạo hiện đại vào đồ án.'
P 'Trong quá trình thực hiện đề tài "Xây dựng Website quản lý cảng trong ngành Logistics", em đã có cơ hội vận dụng tổng hợp kiến thức về mô hình MVC, cơ sở dữ liệu, giao diện người dùng và bước đầu tiếp cận lĩnh vực học sâu (Deep Learning). Do thời gian và kinh nghiệm còn hạn chế, đồ án khó tránh khỏi thiếu sót; em rất mong nhận được sự góp ý của quý thầy cô để hệ thống ngày càng hoàn thiện hơn.'
P 'Em xin chân thành cảm ơn!' 3 $false 13 $true
$sel.ParagraphFormat.Alignment = 2
$sel.ParagraphFormat.FirstLineIndent = 0
P 'Sinh viên thực hiện' 2 $true
P 'Lê Hữu Anh Tài' 2 $true
PB
H1 'NHẬN XÉT CỦA GIẢNG VIÊN'
for ($i = 0; $i -lt 12; $i++) { P ('.' * 110) 3 }
P ''
P 'Điểm số: ................              Chữ ký giảng viên: ................' 3 $true
PB

# ============================================================
# MO DAU
# ============================================================
H1 'MỞ ĐẦU'
P 'Báo cáo này trình bày toàn bộ quá trình thực hiện đồ án môn học Lập trình Web với đề tài "Xây dựng Website quản lý cảng trong ngành Logistics", do sinh viên Lê Hữu Anh Tài thực hiện dưới sự hướng dẫn của ThS. Nguyễn Thành Trung.'
P 'Trong bối cảnh chuyển đổi số ngành logistics — cảng biển đang diễn ra mạnh mẽ tại Việt Nam, đồ án hướng đến xây dựng một hệ thống quản lý cảng trực tuyến hoàn chỉnh — SmartPort — tích hợp các công nghệ hiện đại như ASP.NET MVC 5, Entity Framework 6, đồng thời tiên phong tích hợp các mô hình Deep Learning suy luận ngay trên trình duyệt (edge inference) nhằm nâng cao tính tự động hóa và trải nghiệm người dùng.'
P 'Báo cáo được tổ chức thành 5 chương: Chương I giới thiệu tổng quan vấn đề nghiên cứu; Chương II trình bày cơ sở lý thuyết và các công nghệ nền tảng được sử dụng; Chương III phân tích và thiết kế hệ thống, bao gồm yêu cầu chức năng, các sơ đồ UML (Use Case, Activity, Class Diagram), thiết kế cơ sở dữ liệu và giải thuật AI; Chương IV trình bày kết quả thực nghiệm với đầy đủ giao diện hệ thống đã triển khai; Chương V tổng kết những kết quả đạt được và đề xuất hướng phát triển.'
P 'Em xin chân thành cảm ơn quý thầy cô đã dành thời gian xem xét và đánh giá báo cáo này.'
PB

# ============================================================
# MUC LUC
# ============================================================
H1 'MỤC LỤC'
$doc.TablesOfContents.Add($sel.Range, $true, 1, 3) | Out-Null
$sel.EndKey(6) | Out-Null
PB

# ============================================================
# DANH MUC BANG (placeholder — dien noi dung o cuoi script)
# ============================================================
H1 'DANH MỤC BẢNG'
$doc.Bookmarks.Add('PH_Bang', $sel.Range) | Out-Null
PB

# ============================================================
# DANH MUC HINH (placeholder — dien noi dung o cuoi script)
# ============================================================
H1 'DANH MỤC HÌNH'
$doc.Bookmarks.Add('PH_Hinh', $sel.Range) | Out-Null
PB

# ============================================================
# DANH MUC TU VIET TAT
# ============================================================
H1 'DANH MỤC TỪ VIẾT TẮT'
Bang @('Từ viết tắt', 'Diễn giải') @(
    @('MVC', 'Model – View – Controller'),
    @('EF', 'Entity Framework'),
    @('ORM', 'Object-Relational Mapping'),
    @('CSDL', 'Cơ sở dữ liệu'),
    @('AI', 'Artificial Intelligence — Trí tuệ nhân tạo'),
    @('CNN', 'Convolutional Neural Network — Mạng nơ-ron tích chập'),
    @('MLP', 'Multi-Layer Perceptron — Mạng nơ-ron truyền thẳng nhiều lớp'),
    @('LSTM', 'Long Short-Term Memory — Mạng nơ-ron hồi quy dài-ngắn hạn'),
    @('OCR', 'Optical Character Recognition — Nhận dạng ký tự quang học'),
    @('RAG', 'Retrieval-Augmented Generation'),
    @('API', 'Application Programming Interface'),
    @('UI/UX', 'User Interface / User Experience'),
    @('UML', 'Unified Modeling Language'),
    @('TEU', 'Twenty-foot Equivalent Unit — đơn vị container 20 feet'),
    @('IMO', 'International Maritime Organization'),
    @('ETA/ETD', 'Estimated Time of Arrival/Departure — giờ dự kiến cập/rời bến'),
    @('ATA/ATD', 'Actual Time of Arrival/Departure — giờ thực tế cập/rời bến')
) 'Bảng các từ viết tắt sử dụng trong báo cáo' @(3.5,11)
PB

# ============================================================
# CHUONG I: TONG QUAN VAN DE NGHIEN CUU
# ============================================================
$script:chapNo = 1; $script:figNo = 0; $script:tblNo = 0
H1 'CHƯƠNG I. TỔNG QUAN VẤN ĐỀ NGHIÊN CỨU'
H2 '1.1. Giới thiệu đề tài'
P 'Logistics là một trong những ngành kinh tế mũi nhọn của Việt Nam với tốc độ tăng trưởng 14–16%/năm, trong đó hệ thống cảng biển đóng vai trò cửa ngõ giao thương, xử lý hơn 90% khối lượng hàng hóa xuất nhập khẩu. Tuy nhiên, tại nhiều cảng vừa và nhỏ, quy trình khai thác vẫn phụ thuộc giấy tờ thủ công: khách hàng phải đến trực tiếp để tra cứu container, đăng ký dịch vụ; bộ phận điều độ phân bổ bến tàu dựa trên kinh nghiệm; số liệu khai thác phân tán khiến lãnh đạo khó ra quyết định kịp thời.'
P 'Song song đó, làn sóng chuyển đổi số và trí tuệ nhân tạo (AI) đang thay đổi căn bản cách vận hành cảng biển trên thế giới: camera AI giám sát cổng, mô hình học sâu đọc mã container tự động, thuật toán tối ưu phân bổ cầu bến. Xuất phát từ thực tiễn trên, em lựa chọn đề tài "Xây dựng Website quản lý cảng trong ngành Logistics — hệ thống SmartPort" nhằm xây dựng một hệ thống quản lý cảng trực tuyến hoàn chỉnh, đồng thời tiên phong tích hợp các mô hình Deep Learning chạy trực tiếp trên trình duyệt.'
H2 '1.2. Mục tiêu và phạm vi nghiên cứu'
P 'Đề tài hướng đến bốn nhóm mục tiêu chính: (1) Xây dựng cổng thông tin công khai cho khách hàng: tra cứu container theo thời gian thực, lịch tàu, bảng giá, tin tức, ước tính chi phí; (2) Xây dựng cổng dịch vụ trực tuyến ePort: khách hàng đăng ký tài khoản, đặt dịch vụ cảng, theo dõi trạng thái, nhận hóa đơn qua Excel/PDF/Email; (3) Xây dựng hệ thống quản trị khai thác đầy đủ cho cảng: điều độ chuyến tàu, quản lý container — bãi — bến, duyệt booking, báo cáo thống kê; (4) Nghiên cứu và tích hợp 10 công nghệ AI, trong đó có 3 mô hình Deep Learning thực thụ (YOLOv10 nhận diện đối tượng, OCR mạng LSTM đọc số container, mạng nơ-ron MLP dự báo sản lượng) cùng chatbot, trợ lý giọng nói tiếng Việt.'
P 'Phạm vi nghiệp vụ giới hạn trong các quy trình lõi của một cảng container: quản lý chuyến tàu (vessel call), container, bãi (yard), cầu bến (berth), dịch vụ cảng và đơn đặt dịch vụ. Đối tượng sử dụng gồm ba nhóm: khách vãng lai (tra cứu công khai), khách hàng doanh nghiệp (sử dụng ePort) và nhân sự cảng (quản trị viên, nhân viên khai thác). Hệ thống được triển khai dưới dạng ứng dụng web chạy trên máy chủ IIS, dữ liệu lưu trữ tập trung tại SQL Server.'
H2 '1.3. Phương pháp nghiên cứu'
P 'Đề tài áp dụng quy trình phát triển lặp (iterative): khảo sát website các cảng thực tế (Tân Cảng Sài Gòn — saigonnewport.com.vn) [9] → phân tích yêu cầu → thiết kế cơ sở dữ liệu và kiến trúc MVC → lập trình từng phân hệ → kiểm thử → tinh chỉnh giao diện và bổ sung AI. Các mô hình Deep Learning được lựa chọn theo tiêu chí: đã được cộng đồng kiểm chứng, có thể suy luận (inference) ngay trên trình duyệt bằng ONNX Runtime Web/TensorFlow.js, không yêu cầu máy chủ GPU — phù hợp điều kiện triển khai thực tế của đồ án.'
H2 '1.4. Bố cục báo cáo'
P 'Báo cáo gồm 5 chương: Chương I giới thiệu tổng quan vấn đề nghiên cứu; Chương II trình bày cơ sở lý thuyết và các công nghệ nền tảng; Chương III phân tích và thiết kế hệ thống (yêu cầu chức năng, sơ đồ UML, cơ sở dữ liệu, giải thuật AI); Chương IV trình bày kết quả thực nghiệm với đầy đủ giao diện hệ thống và kết quả kiểm thử; Chương V tổng kết kết quả đạt được và hướng phát triển.'
PB

# ============================================================
# CHUONG II: TIM HIEU VE CO SO LY THUYET
# ============================================================
$script:chapNo = 2; $script:figNo = 0; $script:tblNo = 0
H1 'CHƯƠNG II. TÌM HIỂU VỀ CƠ SỞ LÝ THUYẾT'
H2 '2.1. Tổng quan về ASP.Net'
H3 '2.1.1. Giới thiệu về ASP.Net'
P 'ASP.NET là nền tảng phát triển ứng dụng web do Microsoft phát triển, chạy trên .NET Framework [1]. ASP.NET cho phép xây dựng website động, dịch vụ web (Web API) và ứng dụng thời gian thực, hỗ trợ nhiều ngôn ngữ lập trình (C#, VB.NET) và tích hợp chặt chẽ với hệ sinh thái Visual Studio, Internet Information Services (IIS) và SQL Server. Đồ án SmartPort sử dụng ASP.NET MVC 5 trên nền .NET Framework 4.8 — phiên bản ổn định, phù hợp với hạ tầng Windows Server phổ biến tại các doanh nghiệp vừa và nhỏ ở Việt Nam.'
H3 '2.1.2. Tìm hiểu về mô hình lập trình'
P 'ASP.NET cung cấp ba mô hình lập trình chính: (1) Web Forms — mô hình hướng sự kiện (event-driven) kế thừa từ WinForms, phát triển nhanh nhưng khó kiểm soát mã HTML sinh ra; (2) MVC (Model – View – Controller) — tách biệt rõ dữ liệu, giao diện và luồng điều khiển, kiểm soát hoàn toàn HTML/CSS/JS, dễ kiểm thử; (3) Web API — chuyên xây dựng dịch vụ RESTful trả về JSON/XML. Đồ án lựa chọn mô hình MVC vì tính linh hoạt trong thiết kế giao diện hiện đại, khả năng tổ chức mã nguồn theo từng phân hệ nghiệp vụ (Areas) và sự phổ biến trong đào tạo, tuyển dụng lập trình viên .NET tại Việt Nam.'
H2 '2.2. Giới thiệu về mô hình lập trình Web bằng ASP.Net MVC'
P 'ASP.NET MVC 5 là framework phát triển web của Microsoft, tổ chức ứng dụng theo mẫu kiến trúc Model–View–Controller: Model đại diện dữ liệu và nghiệp vụ; View chịu trách nhiệm hiển thị (Razor Engine); Controller tiếp nhận yêu cầu HTTP, điều phối xử lý và trả kết quả [1]. Kiến trúc này tách biệt rõ ràng ba mối quan tâm, giúp mã nguồn dễ bảo trì, dễ kiểm thử và phù hợp với các hệ thống nghiệp vụ nhiều phân hệ như quản lý cảng. Hệ thống SmartPort sử dụng cơ chế Areas để tách khu quản trị (/Admin) khỏi cổng thông tin công khai, cùng bộ lọc phân quyền tùy biến (AppAuthorizeAttribute) kiểm soát ba vai trò người dùng.'
H2 '2.3. Hoạt động các thành phần chính trong ASP.NET MVC'
P 'Khi người dùng gửi yêu cầu HTTP, bộ định tuyến (Routing) của ASP.NET MVC ánh xạ URL đến Controller và Action tương ứng theo mẫu {controller}/{action}/{id}; Controller gọi các lớp Model và AppServices để xử lý nghiệp vụ, truy vấn dữ liệu thông qua Entity Framework; kết quả được đóng gói vào Model/ViewModel và truyền cho View (Razor .cshtml) để sinh mã HTML trả về trình duyệt. Hệ thống SmartPort mở rộng luồng xử lý chuẩn này bằng lớp AppServices đóng gói các giải thuật AI (Chatbot, Forecast, Berth, Yard, Alert, Cost) và lớp Bảo mật/Phân quyền kiểm soát truy cập theo vai trò, như minh họa ở Hình 2.1.'
Img 'd1-kientruc' 'Sơ đồ kiến trúc hệ thống SmartPort (3 tầng Client — Application — Data)' 16.5 'png'
H2 '2.4. Ưu điểm và nhược điểm của ASP.Net'
P 'Ưu điểm: (1) Hiệu năng cao nhờ cơ chế biên dịch (compiled) thay vì thông dịch từng dòng; (2) Bảo mật tốt — tích hợp sẵn cơ chế chống giả mạo yêu cầu (AntiForgeryToken chống CSRF), mã hóa cấu hình, xác thực Windows/Forms; (3) Hệ sinh thái phong phú — kho thư viện NuGet, tích hợp chặt chẽ với SQL Server và Visual Studio; (4) Khả năng mở rộng tốt nhờ cơ chế Areas, Filter, Dependency Injection — phù hợp hệ thống nhiều phân hệ như SmartPort (công khai/ePort/quản trị). Nhược điểm: (1) Phụ thuộc hệ điều hành Windows và IIS, chi phí hạ tầng cao hơn so với một số nền tảng mã nguồn mở thuần túy; (2) Đường cong học tập ban đầu dốc hơn các framework kịch bản do phải nắm vòng đời ứng dụng và Razor Engine; (3) Cộng đồng, tài nguyên học tập tiếng Việt còn hạn chế hơn so với các công nghệ mã nguồn mở phổ biến khác.'
H2 '2.5. Entity Framework 6 và SQL Server'
P 'Entity Framework 6 (EF6) là công nghệ ORM (Object–Relational Mapping) ánh xạ các bảng SQL Server thành lớp thực thể C#, cho phép truy vấn bằng LINQ thay vì viết SQL thuần, tự động tham số hóa để chống SQL Injection [2]. Đồ án sử dụng hướng tiếp cận Code First ánh xạ vào cơ sở dữ liệu có sẵn (Database.SetInitializer(null)) nhằm kiểm soát chặt lược đồ. SQL Server đóng vai trò hệ quản trị CSDL tập trung với 14 bảng dữ liệu, ràng buộc khóa ngoại đầy đủ và dữ liệu mẫu sử dụng ngày tương đối (GETDATE) giúp hệ thống demo luôn "sống".'
H2 '2.6. Công nghệ giao diện'
P 'Giao diện xây dựng trên Bootstrap 5 [7] kết hợp CSS tùy biến theo bộ nhận diện riêng (xanh navy đại dương — cam container), font chữ Be Vietnam Pro hỗ trợ tiếng Việt. Các hiệu ứng nâng cao được lập trình thuần CSS/JavaScript: slider ảnh thật crossfade kết hợp hiệu ứng Ken Burns, ticker dữ liệu chuyến tàu thời gian thực, reveal khi cuộn trang bằng IntersectionObserver, nghiêng 3D theo con trỏ (perspective transform), chế độ Tối–Sáng đồng bộ toàn hệ thống qua CSS Custom Properties, và biểu đồ Chart.js. Cách tiếp cận này bảo đảm hiệu năng cao mà không phụ thuộc thư viện hiệu ứng bên thứ ba.'
H2 '2.7. Nền tảng AI và Deep Learning ứng dụng trong đề tài'
H3 '2.7.1. Mạng nơ-ron tích chập và họ mô hình YOLO'
P 'YOLO (You Only Look Once) là họ mô hình phát hiện đối tượng (object detection) một giai đoạn: ảnh đầu vào đi qua mạng nơ-ron tích chập (CNN) duy nhất và trực tiếp dự đoán đồng thời tọa độ khung bao (bounding box) cùng xác suất lớp [3]. Đồ án sử dụng YOLOv10n — thế hệ mới nhất với thiết kế NMS-free: mô hình huấn luyện với cơ chế gán nhãn kép (dual assignments) nên khi suy luận không cần bước hậu xử lý Non-Maximum Suppression, giảm độ trễ đáng kể. Mô hình được xuất sang định dạng ONNX và thực thi ngay trên trình duyệt bằng ONNX Runtime Web [6] (WebAssembly), đầu vào chuẩn hóa 640×640 theo kỹ thuật letterbox, đầu ra là tối đa 300 phát hiện dạng (x1, y1, x2, y2, độ tin cậy, mã lớp) trên 80 lớp đối tượng COCO.'
H3 '2.7.2. Nhận dạng ký tự quang học (OCR) bằng mạng LSTM'
P 'Tesseract 4+ sử dụng mạng nơ-ron hồi quy LSTM (Long Short-Term Memory) quét tuần tự các dải ảnh văn bản để dự đoán chuỗi ký tự — cách tiếp cận Deep Learning thay thế hoàn toàn phương pháp so khớp mẫu cổ điển [4]. Đồ án tích hợp Tesseract.js (bản WebAssembly) kèm quy trình tiền xử lý ảnh tự xây dựng: phóng to, chuyển thang xám, tăng tương phản; kết quả văn bản sau đó được đối chiếu biểu thức chính quy [A-Z]{4}[0-9]{7} theo chuẩn mã container ISO 6346 để trích xuất chính xác số container.'
H3 '2.7.3. Mạng nơ-ron truyền thẳng (MLP) cho dự báo chuỗi thời gian'
P 'Bài toán dự báo sản lượng TEU được mô hình hóa theo cửa sổ trượt: dùng sản lượng 3 tháng liên tiếp làm đầu vào để dự đoán tháng kế tiếp. Mạng MLP kiến trúc 3 → 16 → 8 → 1 (hàm kích hoạt ReLU) được huấn luyện bằng thuật toán tối ưu Adam với hàm mất mát MSE ngay trên trình duyệt nhờ TensorFlow.js [5]; dữ liệu được chuẩn hóa Min-Max về [0, 1] trước khi huấn luyện và giải chuẩn hóa khi dự báo. Việc huấn luyện trực quan (đường loss giảm theo từng epoch, tùy chỉnh siêu tham số) biến hệ thống thành một "phòng thí nghiệm AI" phục vụ cả mục đích minh họa học thuật.'
H3 '2.7.4. Chatbot theo kiến trúc RAG'
P 'Chatbot PortBot vận hành theo hai tầng: tầng nhận diện ý định (intent detection) chuẩn hóa tiếng Việt không dấu, trích mã container/booking bằng biểu thức chính quy rồi truy vấn dữ liệu thật từ CSDL; tầng sinh ngôn ngữ (tùy chọn) gửi câu hỏi kèm ngữ cảnh dữ liệu đến mô hình ngôn ngữ lớn Google Gemini theo kiến trúc RAG (Retrieval-Augmented Generation) — bảo đảm câu trả lời tự nhiên nhưng luôn bám sát dữ liệu cảng, không bịa đặt. Hệ thống còn tích hợp Web Speech API cho phép hỏi bằng giọng nói tiếng Việt và nghe bot đọc câu trả lời.'
H2 '2.8. Khảo sát hiện trạng'
P 'Trước khi thiết kế, em khảo sát website Tổng công ty Tân Cảng Sài Gòn (saigonnewport.com.vn) [9] — đơn vị khai thác cảng container lớn nhất Việt Nam. Kết quả cho thấy cấu trúc đặc trưng của một cổng thông tin cảng chuyên nghiệp gồm: banner giới thiệu năng lực, khối tiện ích tra cứu (ePort), tin tức chuyên ngành, số liệu nổi bật, đối tác hãng tàu và liên kết cộng đồng. SmartPort kế thừa cấu trúc này, đồng thời bổ sung các điểm khác biệt: ticker chuyến tàu thời gian thực, công cụ ước tính chi phí AI và các phân hệ Deep Learning mà các website cảng hiện tại chưa cung cấp công khai.'
PB

# ============================================================
# CHUONG III: PHAN TICH VA THIET KE HE THONG
# ============================================================
$script:chapNo = 3; $script:figNo = 0; $script:tblNo = 0
H1 'CHƯƠNG III. PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG'
H2 '3.1. Phân tích thiết kế các chức năng ứng dụng'
H3 '3.1.1. Mô tả chức năng ứng dụng'
Bang @('Nhóm người dùng', 'Chức năng chính') @(
    @('Khách vãng lai', 'Xem trang chủ, giới thiệu, tin tức; tra cứu container (kèm OCR quét ảnh); xem lịch tàu, bảng giá; ước tính chi phí bằng AI; chat với PortBot (gõ phím hoặc giọng nói); gửi liên hệ; đăng ký tài khoản'),
    @('Khách hàng (ePort)', 'Đăng nhập; đặt dịch vụ cảng trực tuyến (tự tính tiền); theo dõi/hủy booking; tải hóa đơn Excel; xem/in hóa đơn PDF; nhận hóa đơn qua email; quản lý hồ sơ, đổi mật khẩu'),
    @('Nhân viên khai thác', 'Dashboard thống kê; điều độ chuyến tàu (cập bến/làm hàng/rời bến); AI phân bổ bến; Camera AI YOLO; AI Lab; quản lý container + AI gợi ý vị trí + sơ đồ bãi; quản lý tàu, hãng tàu, bến; duyệt booking; xuất hóa đơn; quản lý tin tức, thư viện ảnh, liên hệ; báo cáo + xuất Excel'),
    @('Quản trị viên', 'Toàn bộ quyền nhân viên + quản lý tài khoản (tạo, phân vai trò, khóa/mở, đặt lại mật khẩu)')
) 'Yêu cầu chức năng theo nhóm người dùng' @(4.5,11)
P 'Bên cạnh yêu cầu chức năng, hệ thống còn bảo đảm các yêu cầu phi chức năng: (1) An toàn — mật khẩu băm SHA-256, chống CSRF bằng AntiForgeryToken trên mọi form POST, chống SQL Injection nhờ Entity Framework, kiểm soát truy cập theo vai trò tại từng controller; (2) Hiệu năng — các mô hình AI suy luận phía trình duyệt không tạo tải máy chủ, tài nguyên tĩnh đánh phiên bản chống cache cũ; (3) Khả dụng — giao diện responsive, hỗ trợ chế độ Tối–Sáng, có phương án dự phòng khi mất kết nối (gradient thay ảnh, engine SSD thay YOLO); (4) Tiếng Việt toàn diện — bảng mã UTF-8 từ CSDL đến giao diện.'
H3 '3.1.2. Sơ đồ Use Case'
P 'Sơ đồ Use Case (biểu đồ trường hợp sử dụng) theo chuẩn UML [11] mô tả trực quan các chức năng mà từng nhóm tác nhân (actor) có thể thực hiện trong hệ thống. SmartPort có 3 tác nhân chính — Khách hàng, Nhân viên và Quản trị viên — tương ứng ba vai trò phân quyền của hệ thống, như thể hiện ở Hình 3.1.'
Img 'd2-usecase' 'Sơ đồ Use Case hệ thống SmartPort' 16.5 'png'
H3 '3.1.3. Sơ đồ Activity'
P 'Sơ đồ Activity (biểu đồ hoạt động) theo chuẩn UML [11] mô tả luồng xử lý nghiệp vụ theo trình tự thời gian, bao gồm các điểm quyết định rẽ nhánh. Hình 3.2 minh họa quy trình đặt và duyệt dịch vụ tại cổng ePort — một trong những luồng nghiệp vụ trung tâm của hệ thống, từ khi khách hàng xác nhận đặt dịch vụ đến khi nhân viên duyệt/từ chối và hệ thống tự động xuất hóa đơn.'
Img 'd3-activity' 'Sơ đồ Activity — quy trình đặt và duyệt dịch vụ tại cổng ePort' 15.5 'png'
H3 '3.1.4. Sơ đồ Class Diagram'
P 'Sơ đồ lớp (Class Diagram) theo chuẩn UML [11] mô tả cấu trúc tĩnh của hệ thống: các lớp đối tượng (tương ứng các lớp Model trong Entity Framework Code First), thuộc tính và quan hệ liên kết (association) kèm bội số (multiplicity) giữa chúng. Vì đồ án sử dụng Entity Framework Code First ánh xạ trực tiếp vào cơ sở dữ liệu có sẵn nên các lớp Model gần như tương ứng một-một với các bảng dữ liệu, như thể hiện ở Hình 3.3.'
Img 'd4-class' 'Sơ đồ Class Diagram — các lớp Model chính và quan hệ liên kết' 16.5 'png'
H3 '3.1.5. Kiến trúc phân quyền và bảo mật'
P 'Bảo mật và phân quyền được thiết kế theo mô hình kiểm soát truy cập dựa trên vai trò (Role-Based Access Control): mỗi tài khoản gắn với một trong ba vai trò (VaiTro) — Admin, NhanVien, KhachHang. Thuộc tính AppAuthorizeAttribute — kế thừa từ AuthorizeAttribute chuẩn của ASP.NET MVC — được gắn ở cấp Controller hoặc Action để kiểm tra vai trò lưu trong Session trước khi cho phép truy cập; người dùng không đủ quyền bị chuyển hướng đến trang từ chối truy cập. Toàn bộ khu vực quản trị được tổ chức trong MVC Area riêng (/Admin) nhằm tách biệt vật lý khỏi cổng thông tin công khai. Mật khẩu được băm một chiều bằng SHA-256; mọi form POST đều được bảo vệ bằng AntiForgeryToken chống tấn công giả mạo yêu cầu liên trang (CSRF).'
H2 '3.2. Thiết kế cơ sở dữ liệu'
H3 '3.2.1. Mô hình vật lý'
P 'Mô hình vật lý (Physical Model) mô tả chi tiết cấu trúc lưu trữ thực tế trên hệ quản trị CSDL SQL Server: tên bảng, tên cột, kiểu dữ liệu cụ thể, khóa chính (PK), khóa ngoại (FK) và các ràng buộc duy nhất (UNIQUE). Cơ sở dữ liệu SmartPortDB gồm 14 bảng, trong đó 10 bảng có quan hệ khóa ngoại chặt chẽ mô tả nghiệp vụ lõi (tàu — bến — chuyến tàu — container — khách hàng — dịch vụ) và 4 bảng độc lập phục vụ chức năng hỗ trợ (tin tức, liên hệ, thống kê sản lượng, cấu hình ảnh), như trình bày ở Hình 3.4.'
Img 'd5-er' 'Mô hình vật lý cơ sở dữ liệu SmartPortDB (14 bảng)' 16.5 'png'
H3 '3.2.2. Danh sách đối tượng'
P 'Bảng 3.2 liệt kê 14 bảng dữ liệu cùng vai trò nghiệp vụ tương ứng:'
Bang @('STT', 'Bảng', 'Vai trò') @(
    @('1', 'TaiKhoan', 'Tài khoản đăng nhập, vai trò (Admin/NhanVien/KhachHang), mật khẩu băm SHA-256'),
    @('2', 'KhachHang', 'Hồ sơ doanh nghiệp khách hàng (tên, MST, địa chỉ, liên hệ)'),
    @('3', 'HangTau', 'Danh mục hãng tàu (Maersk, MSC, CMA CGM...) kèm mã SCAC'),
    @('4', 'Tau', 'Hồ sơ tàu: IMO, LOA, mớn nước, sức chứa TEU — đầu vào cho AI phân bổ bến'),
    @('5', 'Ben', 'Cầu bến: chiều dài, độ sâu, số cẩu bờ, trạng thái khai thác'),
    @('6', 'ChuyenTau', 'Chuyến tàu (vessel call): ETA/ETD/ATA/ATD, bến, sản lượng cont nhập/xuất, trạng thái'),
    @('7', 'Container', 'Container: số ISO 6346, loại, chủ hàng, chuyến nhập/xuất, vị trí bãi (Khu-Bay-Hàng-Tầng), nhiệt độ cont lạnh'),
    @('8', 'DichVu', 'Biểu giá dịch vụ cảng theo nhóm (bốc xếp, lưu bãi, vận chuyển...)'),
    @('9', 'DatDichVu', 'Đơn đặt dịch vụ (booking) của khách: số booking, trạng thái duyệt, tổng tiền'),
    @('10', 'DatDichVuChiTiet', 'Dòng chi tiết dịch vụ trong từng booking'),
    @('11', 'TinTuc', 'Bài viết tin tức, lượt xem, cờ nổi bật'),
    @('12', 'LienHe', 'Hộp thư liên hệ từ website'),
    @('13', 'SanLuongThang', 'Chuỗi thời gian sản lượng TEU + doanh thu 18 tháng — dữ liệu huấn luyện AI'),
    @('14', 'CauHinhAnh', 'Thư viện 21 vị trí ảnh của website, quản lý động từ trang Admin')
) 'Danh sách các bảng dữ liệu' @(1.5,4,9.5)
H3 '3.2.3. Mô tả chi tiết các bảng'
P 'Các quan hệ khóa ngoại chính giữa các bảng: HangTau (1—n) Tau (1—n) ChuyenTau; Ben (1—n) ChuyenTau; ChuyenTau (1—n) Container theo hai vai trò chuyến nhập/chuyến xuất; KhachHang (1—n) Container, (1—n) DatDichVu (1—n) DatDichVuChiTiet (n—1) DichVu; KhachHang (1—n) TaiKhoan. Chi tiết từng cột, kiểu dữ liệu và ràng buộc được thể hiện đầy đủ trong mô hình vật lý ở Hình 3.4.'
H3 '3.2.4. Ràng buộc toàn vẹn và bảo mật dữ liệu'
P 'Toàn vẹn dữ liệu được bảo đảm ở nhiều lớp: ràng buộc khóa ngoại (FOREIGN KEY) ngăn dữ liệu tham chiếu treo; ràng buộc UNIQUE trên các trường nghiệp vụ nhạy cảm (TenDangNhap, SoCont, MaSoBooking); quy tắc ON DELETE CASCADE áp dụng cho quan hệ DatDichVu — DatDichVuChiTiet để tự động dọn dẹp dòng chi tiết khi xóa đơn đặt dịch vụ gốc. Ở tầng ứng dụng, Entity Framework tự động tham số hóa mọi câu truy vấn LINQ, loại bỏ hoàn toàn nguy cơ SQL Injection [2].'
H2 '3.3. Thiết kế giải thuật AI'
H3 '3.3.1. Tổng quan các giải thuật AI'
P 'Bảng 3.3 tổng hợp 10 công nghệ AI được thiết kế và tích hợp trong hệ thống, phân theo loại kỹ thuật:'
Bang @('#', 'Công nghệ', 'Loại', 'Mô hình / Thuật toán') @(
    @('1', 'Camera AI nhận diện đối tượng', 'Deep Learning', 'YOLOv10n (CNN, NMS-free) — ONNX Runtime Web'),
    @('2', 'OCR đọc số container', 'Deep Learning', 'Mạng LSTM (Tesseract.js) + tiền xử lý ảnh + regex ISO 6346'),
    @('3', 'AI Lab dự báo sản lượng', 'Deep Learning', 'MLP 3–16–8–1, tối ưu Adam, huấn luyện trên trình duyệt (TensorFlow.js)'),
    @('4', 'Chatbot PortBot', 'NLP / RAG', 'Nhận diện ý định + truy vấn CSDL + Google Gemini (tùy chọn)'),
    @('5', 'Trợ lý giọng nói tiếng Việt', 'Speech AI', 'Web Speech API: nhận dạng + tổng hợp giọng nói vi-VN'),
    @('6', 'AI phân bổ bến tàu', 'Tối ưu hóa', 'Chấm điểm đa tiêu chí + kiểm tra xung đột lịch + dự đoán giờ làm hàng'),
    @('7', 'AI gợi ý vị trí hạ bãi', 'Heuristic', 'Phân khu theo đặc tính container + cân bằng chiều cao chồng'),
    @('8', 'Trợ lý cảnh báo vận hành', 'Rule-based AI', 'Quét dữ liệu đa bảng, xếp hạng mức độ ưu tiên'),
    @('9', 'Dự báo nhanh trên Dashboard', 'Thống kê', 'Hồi quy tuyến tính bình phương tối thiểu'),
    @('10', 'AI ước tính chi phí', 'Hệ chuyên gia', 'Tính phí theo biểu giá + sinh nhận xét tiết kiệm thông minh')
) 'Hệ sinh thái 10 công nghệ AI của SmartPort' @(1,5,3,6.5)
H3 '3.3.2. Giải thuật Camera AI — YOLOv10'
P 'Quy trình suy luận được thiết kế theo các bước: ảnh nguồn (tải lên/ảnh mẫu/webcam) → letterbox về 640×640 giữ tỷ lệ → chuẩn hóa [0,1] định dạng CHW → mạng YOLOv10n suy luận qua ONNX Runtime (WebAssembly) → giải mã 300 dự đoán (x1, y1, x2, y2, độ tin cậy, lớp) → quy đổi tọa độ về ảnh gốc → vẽ khung, nhãn tiếng Việt, đếm số lượng theo lớp và sinh cảnh báo điều phối (ví dụ: từ 3 xe tải trở lên đề xuất mở thêm làn cổng). Hệ thống tự động chuyển sang engine dự phòng SSD MobileNet (TensorFlow.js) nếu không tải được mô hình chính, bảo đảm tính sẵn sàng.'
H3 '3.3.3. Giải thuật OCR đọc số container'
P 'Pipeline xử lý gồm bốn bước: (1) tiền xử lý ảnh — phóng to tối đa 2,2 lần, chuyển thang xám, tăng tương phản 1,35 lần nhằm cải thiện độ tương phản ký tự trên vỏ container; (2) mạng LSTM quét chuỗi ảnh dự đoán ký tự (giới hạn bảng chữ A–Z, 0–9); (3) hậu xử lý — loại ký tự nhiễu, so khớp mẫu biểu thức chính quy [A-Z]{4}[0-9]{7} theo chuẩn ISO 6346; (4) tự động điền mã vào ô tra cứu kèm nút "Tra cứu ngay" để người dùng xác nhận nhanh.'
H3 '3.3.4. Giải thuật AI Lab — mạng nơ-ron dự báo'
P 'Người dùng tùy chỉnh ba siêu tham số huấn luyện (epochs 50–500, learning rate 0,001–0,05, số nơ-ron ẩn 8/16/32) rồi khởi động: 15 mẫu cửa sổ trượt được sinh từ chuỗi 18 tháng dữ liệu TEU, đường mất mát MSE hiển thị theo thời gian thực trên thang logarit trong quá trình huấn luyện. Sau khi huấn luyện xong, hệ thống dự báo 6 tháng kế tiếp theo cơ chế tự hồi quy (autoregressive — dùng chính giá trị dự báo làm đầu vào cho bước kế tiếp) và so sánh trực quan với đường hồi quy tuyến tính làm mốc đối chiếu.'
H3 '3.3.5. Giải thuật AI phân bổ bến và gợi ý vị trí hạ bãi'
P 'AI phân bổ bến áp dụng phương pháp chấm điểm đa tiêu chí trên từng bến còn trống: kiểm tra ràng buộc an toàn hàng hải (chiều dài bến ≥ LOA tàu + 15m khoảng cách dây buộc; độ sâu bến ≥ mớn nước tàu + 0,5m dự phòng), loại trừ các bến đang có lịch trùng trong khung ±6 giờ, xếp hạng các bến đạt điều kiện và dự đoán thời gian giải phóng tàu theo công thức: thời gian làm hàng ước tính = số lượt cẩu ÷ (số cầu bến × 28 lượt/giờ/cẩu). AI gợi ý vị trí hạ bãi sử dụng giải thuật heuristic phân khu theo đặc tính container (khu R dành cho container lạnh, khu X cho hàng nguy hiểm, khu A/B/C cho hàng thường, khu E cho container rỗng), kết hợp nguyên tắc cân bằng tải — luôn ưu tiên vị trí có chiều cao chồng (stack) thấp nhất trong khu phù hợp để tối ưu thao tác nâng hạ về sau.'
PB

# ============================================================
# CHUONG IV: KET QUA THUC NGHIEM
# ============================================================
$script:chapNo = 4; $script:figNo = 0; $script:tblNo = 0
H1 'CHƯƠNG IV. KẾT QUẢ THỰC NGHIỆM'
P 'Chương này trình bày đầy đủ kết quả triển khai theo từng phân hệ, minh họa bằng hình chụp thực tế từ hệ thống đang vận hành, chia thành hai khu vực: giao diện người dùng (User) và giao diện quản trị (Admin).'
H2 '4.1. Giao diện User'
H3 '4.1.1. Giao diện trang chủ'
P 'Trang chủ được thiết kế theo chuẩn cổng thông tin cảng hiện đại, mở đầu bằng ticker "TRỰC TIẾP" chạy dữ liệu chuyến tàu thật và khối hero với slider 4 ảnh cảng thật (tự chuyển cảnh kèm hiệu ứng Ken Burns), huy hiệu "10 công nghệ AI", ô tra cứu container nhanh và 12 hạt sáng bay tạo chiều sâu.'
Img '01-home-p1' 'Trang chủ — ticker trực tiếp, hero slider ảnh thật và tra cứu nhanh'
Img '01-home-p2' 'Trang chủ — thống kê thời gian thực và lưới 10 công nghệ AI'
Img '01-home-p3' 'Trang chủ — 10 thẻ công nghệ AI (3 thẻ Deep Learning) và dịch vụ cảng'
Img '01-home-p4' 'Trang chủ — dịch vụ, quy trình 4 bước và băng parallax số liệu'
Img '01-home-p5' 'Trang chủ — lịch tàu sắp cập cảng và thư viện ảnh Ken Burns'
Img '01-home-p6' 'Trang chủ — đối tác hãng tàu, đánh giá khách hàng và giải thưởng'
Img '01-home-p7' 'Trang chủ — tin tức, cộng đồng, FAQ, bản tin và chân trang sóng biển'
P 'Website hỗ trợ chế độ giao diện Tối (Dark mode) đồng bộ toàn hệ thống, tự nhận theo hệ điều hành và ghi nhớ lựa chọn người dùng:'
Img '02-home-dark' 'Trang chủ ở chế độ giao diện Tối (Dark mode)'
H3 '4.1.2. Giao diện Chatbot PortBot và trợ lý giọng nói'
P 'PortBot thường trực ở góc màn hình mọi trang, trả lời bằng dữ liệu thật từ CSDL (tra container, lịch tàu, bảng giá, booking); hỗ trợ nói câu hỏi bằng tiếng Việt qua nút micro và đọc to câu trả lời qua nút loa.'
Img '03-chatbot' 'Chatbot PortBot đang trả lời lịch tàu bằng dữ liệu thời gian thực'
H3 '4.1.3. Giao diện tra cứu container và OCR Deep Learning'
P 'Người dùng nhập số container để xem trạng thái, vị trí bãi, hành trình (timeline) và phí lưu bãi ước tính. Đặc biệt, khối "Quét số container bằng AI" cho phép tải ảnh vỏ container — mạng LSTM đọc ký tự, nhận diện mã hợp lệ theo chuẩn ISO 6346 và tự điền vào ô tra cứu theo giải thuật đã trình bày ở mục 3.3.3.'
Img '04-tracking' 'Kết quả tra cứu container MSCU2201417 với timeline hành trình'
Img '05-ocr-ai' 'OCR Deep Learning đọc số container từ ảnh và điền tự động'
P 'Kết quả thực nghiệm: trên ảnh container mẫu, hệ thống đọc chính xác mã MSCU2201417 (11/11 ký tự) với độ tin cậy 93%, chứng minh tính khả thi của việc chạy mô hình OCR Deep Learning hoàn toàn phía trình duyệt.'
H3 '4.1.4. Giao diện lịch tàu cập cảng'
Img '06-schedule' 'Trang lịch tàu với bộ lọc trạng thái/hãng tàu, hiển thị ETA/ATA — ETD/ATD'
H3 '4.1.5. Giao diện dịch vụ và bảng giá'
Img '07-services' 'Trang dịch vụ & bảng giá theo 5 nhóm dịch vụ'
H3 '4.1.6. Giao diện công cụ AI ước tính chi phí'
P 'Khách hàng khai báo lô hàng (loại container, số lượng, số ngày lưu bãi, dịch vụ kèm) — hệ thống tính chi tiết từng khoản theo biểu giá thật, vẽ biểu đồ cơ cấu chi phí và sinh các nhận xét thông minh gợi ý tiết kiệm.'
Img '08-cost' 'Công cụ AI ước tính chi phí dịch vụ cảng'
H3 '4.1.7. Giao diện tin tức'
Img '09-news' 'Danh sách tin tức với ảnh thật và phân trang'
Img '10-news-detail' 'Trang chi tiết bài viết kèm tin liên quan'
H3 '4.1.8. Giao diện giới thiệu và liên hệ'
Img '11-about' 'Trang giới thiệu: ảnh thực tế, timeline phát triển 2015–2026, năng lực cầu bến'
Img '12-contact' 'Trang liên hệ: biểu mẫu, thông tin quản lý, cộng đồng Facebook/Zalo/YouTube'
H3 '4.1.9. Giao diện đăng ký và đăng nhập'
P 'Hệ thống xác thực bằng phiên làm việc (Session), mật khẩu băm SHA-256; đăng ký tài khoản khách hàng tự động tạo hồ sơ doanh nghiệp liên kết.'
Img '13-login' 'Trang đăng nhập với các tài khoản demo'
Img '14-register' 'Trang đăng ký tài khoản khách hàng doanh nghiệp'
P 'Sau khi đăng nhập, người dùng có trang hồ sơ cá nhân để cập nhật thông tin liên hệ và đổi mật khẩu.'
Img '15b-profile' 'Trang hồ sơ cá nhân: cập nhật thông tin và đổi mật khẩu'
H3 '4.1.10. Giao diện cổng dịch vụ trực tuyến ePort'
P 'Khách hàng chọn dịch vụ và số lượng — hệ thống tính tiền tức thời phía giao diện và kiểm tra lại phía máy chủ; booking được cấp mã tự động (BKyyxxxx) và theo dõi qua các trạng thái: Chờ duyệt → Đã duyệt → Đang thực hiện → Hoàn thành, đúng theo sơ đồ Activity đã thiết kế ở Hình 3.2.'
Img '15-booking-create' 'ePort — đặt dịch vụ trực tuyến với bảng tính tiền tự động'
Img '16-booking-my' 'ePort — danh sách booking của khách hàng'
Img '17-booking-details' 'ePort — chi tiết booking với các nút hóa đơn Excel/PDF/Email'
H3 '4.1.11. Giao diện hóa đơn ba kênh: Excel, PDF, Email'
P 'Từ mỗi booking, hệ thống phát hành hóa đơn theo ba kênh: (1) File Excel .xlsx chuẩn (thư viện EPPlus [8]) với khung hóa đơn kế toán Việt Nam, tự động đọc số tiền thành chữ; (2) Bản in PDF thiết kế chuyên nghiệp kèm logo; (3) Gửi email đính kèm hóa đơn tới khách hàng (hỗ trợ chế độ demo lưu .eml khi chưa cấu hình SMTP).'
Img '18-invoice-print' 'Hóa đơn PDF với logo, bảng kê, thuế GTGT và số tiền bằng chữ'
PB

H2 '4.2. Giao diện Admin'
H3 '4.2.1. Giao diện Dashboard quản trị'
P 'Dashboard tổng hợp 6 thẻ chỉ số thời gian thực, panel Trợ lý AI cảnh báo vận hành, biểu đồ sản lượng kèm dự báo AI, cơ cấu container và doanh thu.'
Img '19-dashboard' 'Dashboard quản trị: chỉ số, cảnh báo AI, biểu đồ và dự báo'
H3 '4.2.2. Giao diện điều độ chuyến tàu và AI phân bổ bến'
P 'Bộ phận điều độ quản lý vòng đời chuyến tàu bằng các nút chuyển trạng thái nhanh (tự ghi ATA/ATD). Trang AI phân bổ bến áp dụng đúng giải thuật đã trình bày ở mục 3.3.5: chấm điểm từng bến theo ràng buộc an toàn hàng hải và dự đoán thời gian giải phóng tàu.'
Img '20-vesselcalls' 'Trang điều độ chuyến tàu với nút chuyển trạng thái nhanh'
Img '20b-vesselcall-form' 'Biểu mẫu thêm/sửa chuyến tàu (ETA/ETD/ATA/ATD, bến, sản lượng cont)'
Img '21-berth-ai' 'AI phân bổ bến: chấm điểm, giải thích lý do và dự đoán giờ làm hàng'
H3 '4.2.3. Giao diện quản lý container, AI vị trí hạ bãi và sơ đồ bãi'
Img '24-containers' 'Danh sách container với tìm kiếm, lọc trạng thái và gate-out'
Img '25-container-form' 'Biểu mẫu container với nút AI gợi ý vị trí hạ bãi'
Img '26-yardmap' 'Sơ đồ bãi container trực quan: 6 khu, màu theo chiều cao chồng'
H3 '4.2.4. Giao diện Camera AI giám sát cổng cảng (YOLOv10)'
P 'Camera AI áp dụng đúng quy trình suy luận đã thiết kế ở mục 3.3.2. Kết quả thực nghiệm: nhận diện xe đầu kéo đạt độ tin cậy 87%, thời gian suy luận 286ms/khung hình trên CPU phổ thông (không cần GPU máy chủ); với ảnh đa đối tượng, hệ thống nhận diện đồng thời 8 đối tượng (người, laptop, cốc, ghế...) trong 765ms.'
Img '22-vision-yolo' 'YOLOv10 nhận diện xe tải với độ tin cậy 87% kèm cảnh báo điều phối'
H3 '4.2.5. Giao diện AI Lab — huấn luyện mô hình dự báo'
P 'AI Lab áp dụng đúng kiến trúc mạng nơ-ron đã thiết kế ở mục 3.3.4. Kết quả thực nghiệm: 200 epochs hoàn tất trong 9,7 giây ngay trên trình duyệt (không cần GPU), sai số tuyệt đối trung bình (MAE) đạt ±5.897 TEU trên chuỗi 18 tháng dữ liệu, dự báo sản lượng tháng kế tiếp đạt 51.926 TEU.'
Img '23-ailab-train' 'AI Lab đang huấn luyện mạng nơ-ron: epoch, loss và kiến trúc 3–16–8–1'
H3 '4.2.6. Giao diện bến cảng với sơ đồ cầu bến trực quan'
Img '27-berths' 'Sơ đồ cầu bến LIVE: tàu đang chiếm bến hiển thị động'
Img '27b-berth-form' 'Biểu mẫu thêm/sửa bến cảng (chiều dài, độ sâu, số cẩu bờ)'
H3 '4.2.7. Giao diện xử lý booking phía quản trị'
Img '28b-bookings-list' 'Danh sách đơn đặt dịch vụ với bộ lọc trạng thái'
Img '28-bookings-admin' 'Chi tiết booking phía quản trị với quy trình duyệt và phát hành hóa đơn'
H3 '4.2.8. Giao diện thư viện ảnh website'
P 'Toàn bộ 21 vị trí ảnh của website được quản lý động: dán URL mới hoặc tải ảnh riêng từ máy — áp dụng tức thời không cần sửa mã nguồn.'
Img '29-gallery' 'Trang quản lý thư viện ảnh với xem trước và tải ảnh lên'
H3 '4.2.9. Giao diện báo cáo thống kê và xuất Excel'
Img '30-reports' 'Báo cáo sản lượng — doanh thu theo năm, biểu đồ kết hợp, xuất Excel'
H3 '4.2.10. Giao diện quản trị danh mục và tài khoản'
P 'Mỗi danh mục (tàu, hãng tàu, khách hàng, dịch vụ, tin tức, tài khoản) đều có trang danh sách kèm biểu mẫu thêm/sửa đầy đủ, có kiểm tra ràng buộc dữ liệu.'
Img '31-accounts' 'Quản lý tài khoản: phân vai trò, khóa/mở, đặt lại mật khẩu'
Img '31b-account-form' 'Biểu mẫu tạo/sửa tài khoản và phân quyền'
Img '32-customers' 'Quản lý hồ sơ khách hàng doanh nghiệp'
Img '32b-customer-form' 'Biểu mẫu thêm/sửa khách hàng (tên công ty, MST, liên hệ)'
Img '33-vessels' 'Quản lý hồ sơ tàu biển (IMO, LOA, mớn nước, TEU)'
Img '33b-vessel-form' 'Biểu mẫu thêm/sửa tàu biển'
Img '37-shippinglines' 'Quản lý hãng tàu đối tác'
Img '37b-line-form' 'Biểu mẫu thêm/sửa hãng tàu (tên, mã SCAC, quốc gia)'
Img '36-services-admin' 'Quản lý biểu giá dịch vụ'
Img '36b-service-form' 'Biểu mẫu thêm/sửa dịch vụ và đơn giá'
Img '34-news-admin' 'Quản lý tin tức'
Img '34b-news-form' 'Trình soạn thảo bài viết tin tức'
Img '35-contacts' 'Hộp thư liên hệ từ website'
Img '38-admin-dark' 'Khu quản trị ở chế độ giao diện Tối'
PB

H2 '4.3. Kết quả kiểm thử hệ thống'
P 'Hệ thống được kiểm thử theo phương pháp hộp đen trên toàn bộ luồng nghiệp vụ; đồng thời kiểm thử tự động bằng cách quét HTTP toàn bộ đường dẫn (26/26 trang trả về mã 200 OK).'
Bang @('#', 'Trường hợp kiểm thử', 'Kết quả mong đợi', 'Kết quả') @(
    @('1', 'Tra cứu container MSCU2201417', 'Hiển thị trạng thái, vị trí, timeline, phí lưu bãi', 'Đạt'),
    @('2', 'OCR ảnh container mẫu', 'Đọc đúng mã, tự điền ô tra cứu', 'Đạt (93%)'),
    @('3', 'Chatbot hỏi lịch tàu/giá/booking', 'Trả lời đúng bằng dữ liệu thật', 'Đạt'),
    @('4', 'Đăng ký + đăng nhập khách hàng', 'Tạo tài khoản, phiên đăng nhập đúng vai trò', 'Đạt'),
    @('5', 'Đặt dịch vụ ePort', 'Sinh mã BK260009, tổng tiền khớp 1.590.000đ', 'Đạt'),
    @('6', 'Xuất hóa đơn Excel', 'File .xlsx chuẩn, đúng số tiền bằng chữ', 'Đạt'),
    @('7', 'Gửi hóa đơn email (demo)', 'Sinh file .eml kèm đính kèm trong Outbox', 'Đạt'),
    @('8', 'AI phân bổ bến', 'Chỉ gợi ý bến đủ điều kiện an toàn, áp dụng 1 chạm', 'Đạt'),
    @('9', 'AI gợi ý vị trí hạ bãi cont lạnh', 'Đề xuất khu R kèm 3 lý do', 'Đạt (R-01-03-1)'),
    @('10', 'Phân quyền: khách vào /Admin', 'Chặn truy cập, hiện trang từ chối', 'Đạt'),
    @('11', 'Dark mode', 'Đồng bộ toàn trang, ghi nhớ lựa chọn', 'Đạt'),
    @('12', 'Quét toàn bộ 26 đường dẫn', 'HTTP 200 OK toàn bộ', 'Đạt 26/26')
) 'Các trường hợp kiểm thử tiêu biểu' @(1,5.5,5.5,3)
P 'Bảng 4.2 tổng hợp riêng kết quả thực nghiệm của ba mô hình Deep Learning và chatbot, đối chiếu với chỉ tiêu đo lường cụ thể:'
Bang @('Mô hình', 'Chỉ số đo', 'Kết quả thực nghiệm') @(
    @('YOLOv10n', 'Độ tin cậy nhận diện xe tải / tốc độ', '87% / 286ms mỗi khung hình (CPU, WebAssembly)'),
    @('YOLOv10n', 'Ảnh đa đối tượng', '8 đối tượng nhận diện đồng thời trong 765ms'),
    @('OCR LSTM', 'Đọc mã container mẫu', 'Chính xác 11/11 ký tự (MSCU2201417), độ tin cậy 93%'),
    @('MLP TensorFlow.js', 'Thời gian huấn luyện 200 epochs', '9,7 giây trên trình duyệt (không cần GPU)'),
    @('MLP TensorFlow.js', 'Sai số huấn luyện (MAE)', '±5.897 TEU trên chuỗi 18 tháng'),
    @('PortBot', 'Độ phủ nghiệp vụ', 'Trả lời đúng 6 nhóm ý định bằng dữ liệu thật từ CSDL')
) 'Tổng hợp kết quả thực nghiệm các mô hình AI' @(4,4,7.5)
PB

# ============================================================
# CHUONG V: TONG KET
# ============================================================
$script:chapNo = 5; $script:figNo = 0; $script:tblNo = 0
H1 'CHƯƠNG V. TỔNG KẾT'
H2 '5.1. Kết quả đạt được'
P 'Đồ án đã hoàn thành trọn vẹn các mục tiêu đặt ra: xây dựng hệ thống quản lý cảng SmartPort hoàn chỉnh trên nền ASP.NET MVC 5 — Entity Framework 6 — SQL Server với 24 controller, 44 giao diện, 14 bảng dữ liệu; cổng ePort cho phép khách hàng đặt dịch vụ và nhận hóa đơn qua ba kênh Excel/PDF/Email; khu quản trị bao phủ toàn bộ nghiệp vụ khai thác cảng. Điểm nổi bật của đề tài là hệ sinh thái 10 công nghệ AI, trong đó 3 mô hình Deep Learning (YOLOv10, OCR LSTM, MLP TensorFlow.js) được suy luận và huấn luyện ngay trên trình duyệt — hướng tiếp cận "edge inference" hiện đại, đã kiểm chứng bằng số liệu thực nghiệm cụ thể trình bày ở Chương IV.'
P 'Bên cạnh kết quả đạt được, đồ án còn một số hạn chế do phạm vi học phần: các mô hình Deep Learning dùng trọng số huấn luyện sẵn trên tập dữ liệu tổng quát (COCO, văn bản Latin) chưa được tinh chỉnh (fine-tune) trên dữ liệu cảng thực tế của Việt Nam; mô hình dự báo mới học từ 18 điểm dữ liệu nên sai số còn đáng kể; hệ thống chưa có thông báo đẩy thời gian thực và chưa triển khai lên máy chủ công cộng.'
H2 '5.2. Hướng phát triển'
P 'Trong tương lai, đề tài có thể phát triển theo các hướng: (1) tinh chỉnh YOLO trên bộ dữ liệu container/xe đầu kéo Việt Nam và huấn luyện mô hình đọc số container chuyên dụng; (2) bổ sung dự báo bằng mạng LSTM/GRU khi tích lũy đủ dữ liệu dài hạn; (3) tích hợp thông báo thời gian thực (SignalR), thanh toán trực tuyến và chữ ký số hóa đơn; (4) triển khai điện toán đám mây kèm CI/CD; (5) mở rộng ứng dụng di động cho tài xế và giao nhận tại cổng cảng.'
P ''
H1 'TÀI LIỆU THAM KHẢO'
P '[1] Microsoft, "ASP.NET MVC 5 Documentation", learn.microsoft.com/aspnet/mvc.' 3
P '[2] Microsoft, "Entity Framework 6 Documentation", learn.microsoft.com/ef/ef6.' 3
P '[3] A. Wang và cộng sự, "YOLOv10: Real-Time End-to-End Object Detection", arXiv:2405.14458, 2024.' 3
P '[4] R. Smith, "An Overview of the Tesseract OCR Engine", Proc. ICDAR, và dự án Tesseract.js (github.com/naptha/tesseract.js).' 3
P '[5] Google, "TensorFlow.js — Machine Learning for JavaScript Developers", tensorflow.org/js.' 3
P '[6] Microsoft, "ONNX Runtime Web", onnxruntime.ai.' 3
P '[7] Bootstrap Team, "Bootstrap 5.3 Documentation", getbootstrap.com.' 3
P '[8] EPPlus Software, "EPPlus — Excel Spreadsheets for .NET", epplussoftware.com.' 3
P '[9] Tổng công ty Tân Cảng Sài Gòn, saigonnewport.com.vn (website khảo sát).' 3
P '[10] Unsplash, "License — Free to use", unsplash.com/license (nguồn hình ảnh minh họa).' 3
P '[11] Object Management Group (OMG), "Unified Modeling Language (UML) Specification", omg.org/spec/UML.' 3

# ---------- Dien noi dung DANH MUC BANG va DANH MUC HINH (quay lai vi tri placeholder) ----------
$rngBang = $doc.Bookmarks('PH_Bang').Range
$sel.SetRange($rngBang.Start, $rngBang.Start)
foreach ($t in $script:tblList) {
    $sel.ParagraphFormat.Alignment = 0
    $sel.ParagraphFormat.FirstLineIndent = 0
    $sel.ParagraphFormat.TabStops.Add($word.CentimetersToPoints(15.5), 2, 1) | Out-Null
    $sel.Font.Size = 13; $sel.Font.Bold = $false; $sel.Font.Italic = $false
    $sel.TypeText("Bảng $($t[0]). $($t[1])")
    $sel.TypeText([char]9)
    $sel.Fields.Add($sel.Range, -1, "PAGEREF $($t[2]) \h", $true) | Out-Null
    $sel.TypeParagraph()
}

$rngHinh = $doc.Bookmarks('PH_Hinh').Range
$sel.SetRange($rngHinh.Start, $rngHinh.Start)
foreach ($f in $script:figList) {
    $sel.ParagraphFormat.Alignment = 0
    $sel.ParagraphFormat.FirstLineIndent = 0
    $sel.ParagraphFormat.TabStops.Add($word.CentimetersToPoints(15.5), 2, 1) | Out-Null
    $sel.Font.Size = 13; $sel.Font.Bold = $false; $sel.Font.Italic = $false
    $sel.TypeText("Hình $($f[0]). $($f[1])")
    $sel.TypeText([char]9)
    $sel.Fields.Add($sel.Range, -1, "PAGEREF $($f[2]) \h", $true) | Out-Null
    $sel.TypeParagraph()
}
$sel.EndKey(6) | Out-Null

# ---------- Khung vien trang bia (Page Border that — chi Section 1) ----------
$sec1 = $doc.Sections.Item(1)
$bd = $sec1.Borders
$bd.Enable = $true
$bd.OutsideLineStyle = 7      # wdLineStyleDouble (viền đôi)
$bd.OutsideLineWidth = 12     # wdLineWidth150pt
$bd.OutsideColor = $navy
$bd.DistanceFrom = 1          # wdBorderDistanceFromPageEdge
$bd.DistanceFromTop = 24; $bd.DistanceFromBottom = 24
$bd.DistanceFromLeft = 24; $bd.DistanceFromRight = 24
$bd.AlwaysInFront = $true
$bd.SurroundHeader = $true; $bd.SurroundFooter = $true
for ($si = 2; $si -le $doc.Sections.Count; $si++) { $doc.Sections.Item($si).Borders.Enable = $false }
$sec1.PageSetup.DifferentFirstPageHeaderFooter = $true

# ---------- Danh so trang + cap nhat muc luc/truong ----------
$doc.Sections.Item($doc.Sections.Count).Footers.Item(1).PageNumbers.Add(1) | Out-Null
$doc.Fields.Update() | Out-Null
$doc.TablesOfContents.Item(1).Update()

# ---------- Luu ----------
if (Test-Path $outDocx) { [IO.File]::Delete($outDocx) }
$doc.SaveAs2($outDocx, 16)
$doc.ExportAsFixedFormat($outPdf, 17)
$pages = $doc.ComputeStatistics(2)
$doc.Close($false)
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
"XONG: $outDocx — $pages trang — $($script:figList.Count) hinh — $($script:tblList.Count) bang"
