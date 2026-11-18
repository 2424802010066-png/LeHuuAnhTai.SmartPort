# ============================================================
# TAO BANG PHAN CONG CONG VIEC — SmartPort (chay bang Word COM)
# ============================================================
$ErrorActionPreference = 'Stop'
$outDocx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\BangPhanCongCongViec_SmartPort.docx'
$outPdf  = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\BangPhanCongCongViec_SmartPort.pdf'

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
$ps.LeftMargin = $word.CentimetersToPoints(2.5)
$ps.RightMargin = $word.CentimetersToPoints(2)

$normal = $doc.Styles(-1)
$normal.Font.Name = 'Times New Roman'
$normal.Font.Size = 13
$normal.ParagraphFormat.LineSpacingRule = 1
$normal.ParagraphFormat.SpaceAfter = 6
$normal.ParagraphFormat.Alignment = 3

$navy = 87*65536 + 47*256 + 10
$xanhNhat = 219 + 229*256 + 241*65536
$xamNhat = 230 + 230*256 + 230*65536

$h1 = $doc.Styles(-2); $h1.Font.Name = 'Times New Roman'; $h1.Font.Size = 16; $h1.Font.Bold = $true; $h1.Font.Color = $navy; $h1.ParagraphFormat.SpaceBefore = 12; $h1.ParagraphFormat.SpaceAfter = 8

function P($t, $align = 3, $bold = $false, $size = 13, $italic = $false) {
    $sel.Style = $doc.Styles(-1)
    $sel.ParagraphFormat.Alignment = $align
    $sel.Font.Bold = [int]$bold
    $sel.Font.Italic = [int]$italic
    $sel.Font.Size = $size
    $sel.TypeText($t)
    $sel.TypeParagraph()
    $sel.Font.Bold = 0; $sel.Font.Italic = 0; $sel.Font.Size = 13
}
function H1($t) { $sel.Style = $doc.Styles(-2); $sel.TypeText($t); $sel.TypeParagraph(); $sel.Style = $doc.Styles(-1) }
function Bang($headers, $rows, $caption, $colWidths) {
    $cols = $headers.Count
    $tbl = $doc.Tables.Add($sel.Range, $rows.Count + 1, $cols)
    $tbl.Borders.Enable = 1
    $tbl.Range.Font.Size = 12
    $tbl.Range.ParagraphFormat.LineSpacingRule = 0
    $tbl.Range.ParagraphFormat.SpaceAfter = 2
    $tbl.Range.ParagraphFormat.Alignment = 3
    if ($colWidths) {
        for ($c = 0; $c -lt $cols; $c++) { $tbl.Columns.Item($c+1).Width = $word.CentimetersToPoints($colWidths[$c]) }
    }
    for ($c = 0; $c -lt $cols; $c++) {
        $cell = $tbl.Cell(1, $c + 1)
        $cell.Range.Text = $headers[$c]
        $cell.Range.Font.Bold = $true
        $cell.Range.Font.Color = 16777215
        $cell.Range.ParagraphFormat.Alignment = 1
        $cell.Shading.BackgroundPatternColor = $navy
        $cell.VerticalAlignment = 1
    }
    for ($r = 0; $r -lt $rows.Count; $r++) {
        for ($c = 0; $c -lt $cols; $c++) {
            $cell = $tbl.Cell($r + 2, $c + 1)
            $cell.Range.Text = [string]$rows[$r][$c]
            $cell.VerticalAlignment = 1
            if ($c -eq 0) { $cell.Range.ParagraphFormat.Alignment = 1 }
        }
    }
    $end = $tbl.Range.End
    $sel.SetRange($end, $end)
    $sel.TypeParagraph()
    if ($caption) {
        $sel.Font.Size = 11; $sel.Font.Italic = $true; $sel.Font.Bold = $true
        $sel.ParagraphFormat.Alignment = 1
        $sel.TypeText($caption)
        $sel.TypeParagraph()
        $sel.Font.Size = 13; $sel.Font.Italic = $false; $sel.Font.Bold = $false
        $sel.ParagraphFormat.Alignment = 3
    }
}

# ============================================================
# TIEU DE
# ============================================================
$sel.ParagraphFormat.SpaceAfter = 2
P 'TRƯỜNG ĐẠI HỌC THỦ DẦU MỘT' 1 $true 15
P 'VIỆN CÔNG NGHỆ SỐ' 1 $true 14
P '——————————' 1 $false 12
$sel.ParagraphFormat.SpaceAfter = 6
P '' 1
P 'BẢNG PHÂN CÔNG CÔNG VIỆC' 1 $true 22
P '(Đồ án kết thúc học phần — môn Lập trình Web)' 1 $false 13 $true
P '' 1
$sel.Font.Color = 255
$sel.Font.Italic = $true
P 'Đề tài: Xây dựng Website quản lý cảng trong ngành Logistics' 1 $true 15 $true
P '— Hệ thống SmartPort —' 1 $true 15 $true
$sel.Font.Italic = $false
$sel.Font.Color = 0
P '' 1

# ---------- Bang thong tin chung ----------
$info = @(
    @('Môn học:', 'Lập trình Web'),
    @('Giáo viên hướng dẫn:', 'Ths. Nguyễn Thành Trung'),
    @('Nhóm thực hiện:', '02 thành viên'),
    @('Ngày lập bảng:', '19/07/2026')
)
$tbl = $doc.Tables.Add($sel.Range, 4, 2)
$tbl.Borders.Enable = 1
$tbl.Rows.Alignment = 1
$tbl.Columns.Item(1).Width = $word.CentimetersToPoints(5.5)
$tbl.Columns.Item(2).Width = $word.CentimetersToPoints(9.5)
for ($r = 0; $r -lt 4; $r++) {
    $c1 = $tbl.Cell($r + 1, 1); $c2 = $tbl.Cell($r + 1, 2)
    $c1.Range.Text = $info[$r][0]; $c2.Range.Text = $info[$r][1]
    $c1.Range.Font.Bold = $true; $c1.Range.Font.Size = 13
    $c2.Range.Font.Bold = $true; $c2.Range.Font.Size = 13
    $c1.Shading.BackgroundPatternColor = $xanhNhat
    $c1.VerticalAlignment = 1; $c2.VerticalAlignment = 1
}
$endT = $tbl.Range.End
$sel.SetRange($endT, $endT)
$sel.TypeParagraph()
P '' 1

# ============================================================
# 1. DANH SACH THANH VIEN
# ============================================================
H1 '1. DANH SÁCH THÀNH VIÊN NHÓM'
Bang @('STT','Họ và tên','MSSV','Vai trò trong nhóm') @(
    @('1','Lê Hữu Anh Tài','2424802010066','Nhóm trưởng — trực tiếp thực hiện toàn bộ đồ án'),
    @('2','Đặng Anh Kiệt','(chưa cập nhật)','Thành viên — không tham gia thực hiện')
) '' @(1.5,4,3,6.5)
P ''

# ============================================================
# 2. BANG PHAN CONG CONG VIEC CHI TIET
# ============================================================
H1 '2. BẢNG PHÂN CÔNG CÔNG VIỆC CHI TIẾT'
Bang @('STT','Nội dung công việc','Người thực hiện','Kết quả / Sản phẩm') @(
    @('1','Khảo sát yêu cầu đề tài, lập kế hoạch và phân chia công việc','Lê Hữu Anh Tài','Kế hoạch thực hiện đồ án'),
    @('2','Thiết kế cơ sở dữ liệu SmartPortDB (14 bảng, quan hệ, ràng buộc khóa)','Lê Hữu Anh Tài','Script tạo CSDL SQL Server'),
    @('3','Xây dựng kiến trúc hệ thống MVC5 + EF6, phân quyền 3 vai trò (Admin/Nhân viên/Khách hàng)','Lê Hữu Anh Tài','Hệ thống Areas, AppAuthorizeAttribute'),
    @('4','Thiết kế giao diện (UI/UX), hiệu ứng chuyển động, chế độ Dark/Light mode, Responsive','Lê Hữu Anh Tài','44 giao diện (Views) hoàn chỉnh'),
    @('5','Lập trình 24 Controller, Model và xử lý nghiệp vụ backend','Lê Hữu Anh Tài','24 Controller hoạt động ổn định'),
    @('6','Xây dựng cổng ePort: đăng ký, đặt dịch vụ, tra cứu container cho khách hàng','Lê Hữu Anh Tài','Module ePort hoàn chỉnh'),
    @('7','Xây dựng khu quản trị Admin: quản lý tàu, bến, container, hãng tàu, dịch vụ, tin tức, thống kê','Lê Hữu Anh Tài','Module Admin hoàn chỉnh'),
    @('8','Xuất hóa đơn Excel (EPPlus) và gửi email tự động cho khách hàng','Lê Hữu Anh Tài','Chức năng xuất hóa đơn + gửi mail'),
    @('9','Tích hợp AI Chatbot tư vấn (Gemini API kèm chế độ rule-based dự phòng)','Lê Hữu Anh Tài','Chatbot hoạt động 2 chế độ'),
    @('10','Tích hợp Deep Learning — nhận diện vật thể qua camera bằng YOLOv10 (ONNX Runtime Web)','Lê Hữu Anh Tài','Camera AI nhận diện thời gian thực'),
    @('11','Tích hợp Deep Learning — nhận dạng ký tự số container bằng OCR (Tesseract.js)','Lê Hữu Anh Tài','Chức năng quét số container'),
    @('12','Tích hợp Deep Learning — huấn luyện mô hình dự báo sản lượng bằng TensorFlow.js','Lê Hữu Anh Tài','AI Lab huấn luyện trực tiếp trên trình duyệt'),
    @('13','Kiểm thử toàn hệ thống (26 đường dẫn, các luồng nghiệp vụ chính)','Lê Hữu Anh Tài','Báo cáo kiểm thử — đạt 26/26'),
    @('14','Viết báo cáo đồ án, thiết kế slide thuyết trình, kịch bản thuyết trình và video demo','Lê Hữu Anh Tài','Bộ hồ sơ báo cáo hoàn chỉnh'),
    @('15','Đóng gói mã nguồn, cấu hình và triển khai hosting sản phẩm','Lê Hữu Anh Tài','Mã nguồn đóng gói + website triển khai')
) 'Bảng 2.1. Phân công công việc chi tiết theo từng đầu việc' @(1.2,7.3,3.5,3)
P ''
P 'Ghi chú: Trong suốt quá trình thực hiện đồ án, thành viên Đặng Anh Kiệt không tham gia thực hiện bất kỳ nội dung công việc nào nêu trên (không phân tích, không lập trình, không kiểm thử, không tham gia viết báo cáo).' 3 $false 12 $true

# ============================================================
# 3. BANG TONG HOP TY LE DONG GOP
# ============================================================
H1 '3. BẢNG TỔNG HỢP TỶ LỆ ĐÓNG GÓP'
Bang @('STT','Họ và tên','Tỷ lệ đóng góp','Ghi chú') @(
    @('1','Lê Hữu Anh Tài','100%','Trực tiếp thực hiện toàn bộ 15/15 đầu việc'),
    @('2','Đặng Anh Kiệt','0%','Không tham gia thực hiện đầu việc nào')
) 'Bảng 3.1. Tổng hợp tỷ lệ đóng góp của từng thành viên' @(1.5,4,3,6.5)
P ''

# ============================================================
# 4. XAC NHAN
# ============================================================
H1 '4. XÁC NHẬN CỦA NHÓM'
P 'Bảng phân công công việc trên được lập dựa trên tình hình thực tế quá trình thực hiện đồ án. Đề nghị giảng viên hướng dẫn xem xét tỷ lệ đóng góp thực tế nêu trên khi đánh giá điểm số của từng thành viên trong nhóm.'
P ''
P ''
$sel.Font.Bold = $true
$tblSig = $doc.Tables.Add($sel.Range, 2, 2)
$tblSig.Borders.Enable = 0
$tblSig.Columns.Item(1).Width = $word.CentimetersToPoints(8)
$tblSig.Columns.Item(2).Width = $word.CentimetersToPoints(8)
$tblSig.Cell(1,1).Range.Text = 'Thành viên'
$tblSig.Cell(1,2).Range.Text = 'Nhóm trưởng'
$tblSig.Cell(1,1).Range.ParagraphFormat.Alignment = 1
$tblSig.Cell(1,2).Range.ParagraphFormat.Alignment = 1
$tblSig.Cell(2,1).Range.Text = '(Đặng Anh Kiệt)'
$tblSig.Cell(2,2).Range.Text = 'Lê Hữu Anh Tài'
$tblSig.Cell(2,1).Range.Font.Italic = $true
$tblSig.Cell(2,1).Range.Font.Bold = $false
$tblSig.Cell(2,2).Range.Font.Bold = $true
$tblSig.Cell(2,1).Range.ParagraphFormat.Alignment = 1
$tblSig.Cell(2,2).Range.ParagraphFormat.Alignment = 1
$tblSig.Rows.Item(2).Height = $word.CentimetersToPoints(2.2)
$tblSig.Rows.Item(2).HeightRule = 1
$sel.Font.Bold = $false
$endS = $tblSig.Range.End
$sel.SetRange($endS, $endS)
$sel.TypeParagraph()
P ''
P 'TP. Hồ Chí Minh, ngày 19 tháng 07 năm 2026' 1 $false 13 $true

# ---------- Luu ----------
if (Test-Path $outDocx) { [IO.File]::Delete($outDocx) }
$doc.SaveAs2($outDocx, 16)
$doc.ExportAsFixedFormat($outPdf, 17)
$pages = $doc.ComputeStatistics(2)
$doc.Close($false)
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
"XONG: $outDocx — $pages trang"
