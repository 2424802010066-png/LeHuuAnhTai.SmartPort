# ============================================================
# TAO BAN THUYET MINH DU AN KHOI NGHIEP — SmartPort (Word COM)
# Cau truc theo mau cuoc thi y tuong khoi nghiep sinh vien
# ============================================================
$ErrorActionPreference = 'Stop'
$outDir = "C:\Users\ASUS\Downloads\SmartPort\KhoiNghiep"
$imgDir = "C:\Users\ASUS\Downloads\SmartPort\anh-tinhnang"
$outDocx = Join-Path $outDir "ThuyetMinh_KhoiNghiep_SmartPort.docx"
$outPdf  = Join-Path $outDir "ThuyetMinh_KhoiNghiep_SmartPort.pdf"

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Add()

# ---- Thiet lap trang chuan ----
$doc.PageSetup.TopMargin = $word.CentimetersToPoints(2.0)
$doc.PageSetup.BottomMargin = $word.CentimetersToPoints(2.0)
$doc.PageSetup.LeftMargin = $word.CentimetersToPoints(3.0)
$doc.PageSetup.RightMargin = $word.CentimetersToPoints(2.0)

$sel = $word.Selection

function Set-Body {
    $sel.Font.Name = 'Times New Roman'; $sel.Font.Size = 13
    $sel.Font.Bold = $false; $sel.Font.Italic = $false
    $sel.Font.Color = 0
    $sel.ParagraphFormat.Alignment = 3          # justify
    $sel.ParagraphFormat.LineSpacingRule = 5    # 1.5 line... (5 = wdLineSpace1pt5)
    $sel.ParagraphFormat.SpaceAfter = 6
    $sel.ParagraphFormat.LeftIndent = 0
}
function P($text) { Set-Body; $sel.TypeText($text); $sel.TypeParagraph() }
function PB($text) { Set-Body; $sel.Font.Bold = $true; $sel.TypeText($text); $sel.Font.Bold = $false; $sel.TypeParagraph() }
function Bullet($text) {
    Set-Body
    $sel.ParagraphFormat.LeftIndent = $word.CentimetersToPoints(0.7)
    $sel.TypeText([char]0x2022 + ' ' + $text)
    $sel.TypeParagraph()
    $sel.ParagraphFormat.LeftIndent = 0
}
function H1($num, $text) {
    $sel.ParagraphFormat.Alignment = 0
    $sel.Font.Name = 'Times New Roman'; $sel.Font.Size = 15; $sel.Font.Bold = $true
    $sel.Font.Color = 0x572F0A   # BGR cua #0A2F57 (navy)
    $sel.ParagraphFormat.SpaceBefore = 14; $sel.ParagraphFormat.SpaceAfter = 8
    $sel.TypeText("$num. $text")
    $sel.TypeParagraph()
    $sel.ParagraphFormat.SpaceBefore = 0
    Set-Body
}
function H2($text) {
    $sel.ParagraphFormat.Alignment = 0
    $sel.Font.Name = 'Times New Roman'; $sel.Font.Size = 13; $sel.Font.Bold = $true
    $sel.Font.Color = 0x8B4F0D
    $sel.ParagraphFormat.SpaceBefore = 8; $sel.ParagraphFormat.SpaceAfter = 6
    $sel.TypeText($text)
    $sel.TypeParagraph()
    $sel.ParagraphFormat.SpaceBefore = 0
    Set-Body
}
function AddImage($file, $widthCm, $caption) {
    $path = Join-Path $imgDir $file
    if (-not (Test-Path $path)) { return }
    $sel.ParagraphFormat.Alignment = 1
    $shape = $sel.InlineShapes.AddPicture($path, $false, $true)
    $ratio = $shape.Height / $shape.Width
    $shape.Width = $word.CentimetersToPoints($widthCm)
    $shape.Height = $shape.Width * $ratio
    $sel.TypeParagraph()
    $sel.Font.Size = 11; $sel.Font.Italic = $true; $sel.Font.Bold = $false
    $sel.ParagraphFormat.Alignment = 1
    $sel.TypeText($caption)
    $sel.TypeParagraph()
    Set-Body
}
function AddTable($headers, $rows, $widths) {
    $tbl = $doc.Tables.Add($sel.Range, $rows.Count + 1, $headers.Count)
    $tbl.Borders.Enable = $true
    $tbl.Range.Font.Name = 'Times New Roman'; $tbl.Range.Font.Size = 12
    for ($c = 0; $c -lt $headers.Count; $c++) {
        $cell = $tbl.Cell(1, $c + 1)
        $cell.Range.Text = $headers[$c]
        $cell.Range.Font.Bold = $true
        $cell.Range.Font.Color = 0xFFFFFF
        $cell.Shading.BackgroundPatternColor = 0x572F0A
    }
    for ($r = 0; $r -lt $rows.Count; $r++) {
        for ($c = 0; $c -lt $headers.Count; $c++) {
            $tbl.Cell($r + 2, $c + 1).Range.Text = $rows[$r][$c]
        }
    }
    if ($widths) {
        for ($c = 0; $c -lt $widths.Count; $c++) {
            $tbl.Columns.Item($c + 1).Width = $word.CentimetersToPoints($widths[$c])
        }
    }
    $end = $tbl.Range.End
    $sel.SetRange($end, $end)
    $sel.TypeParagraph()
    Set-Body
}

# ================== TRANG BIA ==================
$sel.ParagraphFormat.Alignment = 1
$sel.Font.Name = 'Times New Roman'; $sel.Font.Size = 14; $sel.Font.Bold = $true
$sel.TypeText('CUỘC THI Ý TƯỞNG KHỞI NGHIỆP'); $sel.TypeParagraph()
$sel.TypeParagraph(); $sel.TypeParagraph(); $sel.TypeParagraph()
$sel.Font.Size = 30; $sel.Font.Color = 0x572F0A
$sel.TypeText('SMARTPORT'); $sel.TypeParagraph()
$sel.Font.Size = 17; $sel.Font.Color = 0x8B4F0D
$sel.TypeText('Nền tảng quản lý cảng và depot logistics'); $sel.TypeParagraph()
$sel.TypeText('tích hợp trí tuệ nhân tạo "AI-ngay-trên-trình-duyệt"'); $sel.TypeParagraph()
$sel.TypeParagraph(); $sel.TypeParagraph()
$sel.Font.Size = 13; $sel.Font.Color = 0; $sel.Font.Bold = $false
$sel.TypeText('Lĩnh vực: Công nghệ thông tin – Logistics – Chuyển đổi số'); $sel.TypeParagraph()
$sel.TypeParagraph(); $sel.TypeParagraph()
$sel.Font.Bold = $true
$sel.TypeText('Tác giả / Trưởng dự án: Lê Hữu Anh Tài'); $sel.TypeParagraph()
$sel.Font.Bold = $false
$sel.TypeText('MSSV: 2424802010066'); $sel.TypeParagraph()
$sel.TypeText('Email: lehuuanhtai2020tkh@gmail.com'); $sel.TypeParagraph()
$sel.TypeParagraph(); $sel.TypeParagraph(); $sel.TypeParagraph()
$sel.TypeText('Năm 2026'); $sel.TypeParagraph()
$sel.InsertBreak(7)   # page break

# ================== 1. TOM TAT ==================
H1 '1' 'TÓM TẮT Ý TƯỞNG (EXECUTIVE SUMMARY)'
P 'SmartPort là nền tảng phần mềm dạng dịch vụ (SaaS) giúp các cảng biển quy mô vừa và nhỏ, cảng cạn (ICD), depot và bãi container tại Việt Nam chuyển đổi số toàn diện nghiệp vụ khai thác: điều độ tàu – quản lý bãi container – cổng ra vào – đặt dịch vụ và thanh toán trực tuyến – báo cáo điều hành.'
P 'Điểm khác biệt cốt lõi của SmartPort là hệ sinh thái hơn 10 công nghệ AI được thiết kế để chạy ngay trên trình duyệt của người dùng (client-side AI): camera nhận diện phương tiện YOLO26 tăng tốc WebGPU, nhận diện biển số xe tại cổng (ALPR), đăng nhập bằng khuôn mặt có chống giả mạo, AI Agent điều phối tự động, dự báo lưu lượng bằng mô hình không gian trạng thái (kiến trúc lấy cảm hứng từ Mamba), cùng các mô hình học máy kinh điển (K-Means, cây quyết định, Q-Learning). Nhờ đó, chi phí hạ tầng gần như bằng không so với các hệ thống AI truyền thống cần máy chủ GPU — đây chính là chìa khóa để đưa công nghệ cấp cảng quốc tế xuống mức giá mà một depot tư nhân Việt Nam chi trả được.'
P 'Sản phẩm không dừng ở ý tưởng: phiên bản hoàn chỉnh đã được xây dựng, triển khai thực tế trên hosting, tích hợp thanh toán VietQR/NAPAS 247 và kết nối kế toán MISA. Dự án hướng tới mô hình thuê bao theo tháng với ba gói dịch vụ, mục tiêu 80 khách hàng sau 3 năm.'

# ================== 2. BOI CANH & VAN DE ==================
H1 '2' 'BỐI CẢNH VÀ VẤN ĐỀ'
H2 '2.1. Nỗi đau của thị trường'
Bullet 'Chi phí logistics của Việt Nam ước tính chiếm khoảng 16–18% GDP — cao hơn đáng kể mức trung bình thế giới (khoảng 10–11%), trong đó một phần lớn đến từ vận hành thủ công, chờ đợi tại cổng cảng và giấy tờ trùng lặp (nguồn tham khảo: Báo cáo Logistics Việt Nam, Bộ Công Thương).'
Bullet 'Việt Nam có 34 cảng biển với gần 300 bến cảng cùng hàng trăm ICD, depot và bãi container tư nhân; phần lớn đơn vị vừa và nhỏ vẫn quản lý bằng sổ giấy, Excel và bộ đàm — dữ liệu phân mảnh, thất thoát doanh thu phí lưu bãi, ùn tắc cổng vào giờ cao điểm.'
Bullet 'Các hệ thống quản lý cảng (TOS) quốc tế như Navis N4 có chi phí triển khai hàng trăm nghìn tới hàng triệu USD, yêu cầu hạ tầng máy chủ lớn — hoàn toàn ngoài tầm với của nhóm khách hàng này.'
Bullet 'Các giải pháp AI camera (nhận diện biển số, giám sát) hiện bán rời, giá cao, không tích hợp với nghiệp vụ đặt dịch vụ – thanh toán – kế toán, khiến doanh nghiệp phải vận hành 3–4 phần mềm cùng lúc.'
H2 '2.2. Cơ hội'
Bullet 'Chuyển đổi số ngành logistics và cảng biển là ưu tiên trong các chương trình chuyển đổi số quốc gia; xu hướng "cảng xanh, cảng thông minh" đang được thúc đẩy mạnh.'
Bullet 'Công nghệ trình duyệt 2024–2026 (WebGPU, ONNX Runtime Web, WebAssembly) lần đầu tiên cho phép chạy mô hình deep learning thị giác máy tính ngay trên máy người dùng — mở ra cách làm "AI không cần server GPU" mà 2–3 năm trước là bất khả thi.'

# ================== 3. GIAI PHAP ==================
H1 '3' 'GIẢI PHÁP — SẢN PHẨM SMARTPORT'
P 'SmartPort là một nền tảng web duy nhất bao trọn vòng nghiệp vụ của một cảng/depot, chia thành ba lớp giá trị:'
H2 '3.1. Lớp nghiệp vụ lõi (thay thế Excel và sổ giấy)'
Bullet 'Điều độ chuyến tàu, quản lý bến cảng, hãng tàu, tàu biển; AI gợi ý phân bổ bến theo ràng buộc chiều dài – mớn nước.'
Bullet 'Quản lý container và sơ đồ bãi trực quan; theo dõi container lạnh có cảnh báo nhiệt độ.'
Bullet 'Khách hàng đặt dịch vụ trực tuyến (booking), quy trình duyệt – thực hiện – hoàn thành, hóa đơn Excel/PDF, gửi email.'
Bullet 'Thanh toán trực tuyến VietQR/NAPAS 247 sinh mã QR động theo từng giao dịch; đối soát cho kế toán; xuất bảng kê chứng từ đúng mẫu nhập của phần mềm kế toán MISA.'
H2 '3.2. Lớp AI khai thác (điểm khác biệt cạnh tranh)'
AddTable @('Công nghệ AI', 'Ứng dụng thực tế tại cảng') @(
    @('YOLO26 (CNN, 2026) + WebGPU', 'Camera đếm xe tải, người, tàu thuyền; cảnh báo ùn tắc cổng theo thời gian thực'),
    @('ALPR (YOLO26 + OCR LSTM)', 'Đọc biển số xe tại cổng, ghi nhật ký ra/vào tự động như trạm thu phí'),
    @('ArcFace + MiniFASNet', 'Đăng nhập bằng khuôn mặt, chống giả mạo ảnh/màn hình (liveness 2 tầng)'),
    @('AI Agent điều phối', 'Tự quét toàn hệ thống, xếp hạng việc cần làm kèm chuỗi suy luận giải thích'),
    @('Mamba-style SSM + MLP + hồi quy', 'Dự báo lưu lượng xe và sản lượng để bố trí nhân lực, mở làn cổng'),
    @('K-Means, cây quyết định, Z-score', 'Phân khúc khách hàng, dự đoán rủi ro hủy đơn, phát hiện bất thường sản lượng'),
    @('Active Learning', 'Người dùng phản hồi đúng/sai, hệ thống tự hiệu chỉnh ngưỡng nhận diện theo thời gian'),
    @('NLP - RAG chatbot', 'Trợ lý ảo trả lời thủ tục, tra cứu container/booking bằng tiếng Việt')
) @(6.5, 9.5)
H2 '3.3. Lớp trải nghiệm'
Bullet 'Giao diện hiện đại, hỗ trợ giao diện tối/sáng, thẻ chỉ số trực quan, hiệu ứng chuyên nghiệp.'
Bullet 'Toàn bộ AI demo được trực tiếp trong trình duyệt — khách hàng dùng thử ngay không cần cài đặt.'
AddImage '17-cong-thong-minh-alpr.png' 14.5 'Hình 1. Cổng thông minh ALPR — AI đọc biển số xe và nhật ký ra/vào (ảnh chụp sản phẩm thật)'
AddImage '03-thong-ke-luu-luong.png' 14.5 'Hình 2. Thống kê lưu lượng cổng cảng từ dữ liệu camera AI'

# ================== 4. DIEM KHAC BIET ==================
H1 '4' 'ĐIỂM KHÁC BIỆT VÀ LỢI THẾ CẠNH TRANH'
AddTable @('Tiêu chí', 'TOS quốc tế (Navis...)', 'Phần mềm nội địa hiện có', 'SmartPort') @(
    @('Chi phí triển khai', 'Hàng trăm nghìn USD', 'Vài chục – trăm triệu', 'Từ vài triệu đồng/tháng (SaaS)'),
    @('AI thị giác (camera, biển số)', 'Module riêng, đắt', 'Hầu như không có', 'Tích hợp sẵn, chạy trên trình duyệt'),
    @('Hạ tầng máy chủ GPU', 'Bắt buộc', 'Không có AI', 'KHÔNG cần (client-side AI)'),
    @('Thanh toán VietQR + MISA', 'Không', 'Một phần', 'Tích hợp sẵn'),
    @('Khả năng tự cải thiện', 'Không', 'Không', 'Active Learning từ phản hồi'),
    @('Thời gian triển khai', '6–18 tháng', '1–3 tháng', '1–2 tuần')
) @(4.2, 4.2, 3.8, 4.3)
P 'Rào cản sao chép: tổ hợp 10+ mô hình AI đã được tinh chỉnh chạy ổn định trên trình duyệt (WebGPU, cache mô hình, chuỗi dự phòng đa tầng) cùng vòng lặp dữ liệu Active Learning — càng nhiều khách dùng, hệ thống nhận diện càng chính xác, tạo hiệu ứng tuyết lăn về chất lượng mà đối thủ đi sau khó bắt kịp.'

# ================== 5. THI TRUONG ==================
H1 '5' 'PHÂN TÍCH THỊ TRƯỜNG VÀ KHÁCH HÀNG MỤC TIÊU'
H2 '5.1. Quy mô thị trường'
Bullet 'TAM (thị trường tổng thể): thị trường dịch vụ logistics Việt Nam ước tính trên 40 tỷ USD/năm, tăng trưởng 14–16%/năm (nguồn tham khảo: Hiệp hội Doanh nghiệp dịch vụ logistics Việt Nam — VLA).'
Bullet 'SAM (thị trường có thể phục vụ): phần mềm quản lý cho khoảng 300 bến cảng, 100+ ICD/cảng cạn và hàng trăm depot, bãi container, kho CFS tư nhân trên cả nước.'
Bullet 'SOM (mục tiêu 3 năm): 80 khách hàng trả phí — tương đương khoảng 10–15% nhóm depot/ICD cỡ vừa đang chuyển đổi số.'
H2 '5.2. Chân dung khách hàng mục tiêu'
Bullet 'Depot/bãi container tư nhân 2–15 ha: đau đầu nhất về thất thoát phí lưu bãi và ùn tắc cổng; ngân sách CNTT hạn chế — là khách hàng "đất lành" giai đoạn đầu.'
Bullet 'ICD/cảng cạn và cảng sông quy mô vừa: cần điều độ, camera an ninh, báo cáo cho hãng tàu.'
Bullet 'Doanh nghiệp khai thác kho CFS, bãi xe container: dùng module cổng ALPR + thanh toán như một giải pháp thu phí tự động.'

# ================== 6. DOI THU ==================
H1 '6' 'ĐỐI THỦ CẠNH TRANH'
Bullet 'Nhóm TOS quốc tế (Navis N4, CATOS...): mạnh và toàn diện nhưng chi phí và thời gian triển khai vượt xa khả năng nhóm khách mục tiêu — không cạnh tranh trực tiếp phân khúc này.'
Bullet 'Nhóm phần mềm quản lý kho/bãi nội địa: giá phải chăng nhưng thiếu hoàn toàn lớp AI thị giác và trải nghiệm hiện đại; SmartPort thắng bằng AI tích hợp và chi phí hạ tầng bằng không.'
Bullet 'Nhóm giải pháp camera AI độc lập (nhận diện biển số bán rời): chỉ giải quyết một khâu, không có nghiệp vụ — SmartPort thay thế bằng một nền tảng duy nhất.'
Bullet 'Rủi ro đối thủ lớn gia nhập: lợi thế của SmartPort là tốc độ, giá, độ am hiểu nghiệp vụ nội địa (VietQR, MISA, biển số Việt Nam) và dữ liệu Active Learning tích lũy theo thời gian.'

# ================== 7. MO HINH KINH DOANH ==================
H1 '7' 'MÔ HÌNH KINH DOANH'
P 'Mô hình SaaS thuê bao theo tháng, cộng phí triển khai ban đầu và dịch vụ tùy biến:'
AddTable @('Gói', 'Giá dự kiến', 'Đối tượng', 'Nội dung chính') @(
    @('BASIC', '2,9 triệu đ/tháng', 'Depot, bãi xe nhỏ', 'Nghiệp vụ lõi + cổng ALPR + thanh toán QR'),
    @('PRO', '6,9 triệu đ/tháng', 'ICD, cảng sông vừa', 'Toàn bộ Basic + camera AI + dự báo + AI Agent + MISA'),
    @('ENTERPRISE', '14,9 triệu đ/tháng', 'Cảng biển nhỏ, chuỗi depot', 'Toàn bộ Pro + tùy biến, đào tạo, hỗ trợ ưu tiên, API')
) @(3.2, 3.8, 4.0, 5.0)
Bullet 'Phí triển khai – đào tạo ban đầu: 15–30 triệu đồng/khách (một lần).'
Bullet 'Doanh thu phụ: tùy biến báo cáo, tích hợp cân điện tử/barrier, cho thuê camera đi kèm.'
Bullet 'Chi phí biên thấp nhờ AI chạy phía người dùng: một máy chủ web thông thường phục vụ được hàng chục khách hàng.'

# ================== 8. TAI CHINH ==================
H1 '8' 'KẾ HOẠCH TÀI CHÍNH DỰ KIẾN 3 NĂM'
P 'Các số liệu dưới đây là ước tính thận trọng dựa trên giá gói trung bình 5 triệu đồng/tháng/khách và tỷ lệ rời bỏ 10%/năm:'
AddTable @('Chỉ tiêu', 'Năm 1', 'Năm 2', 'Năm 3') @(
    @('Khách hàng trả phí (lũy kế)', '10', '35', '80'),
    @('Doanh thu thuê bao', '~400 triệu', '~1,8 tỷ', '~4,4 tỷ'),
    @('Doanh thu triển khai + khác', '~180 triệu', '~500 triệu', '~1,0 tỷ'),
    @('Tổng doanh thu', '~580 triệu', '~2,3 tỷ', '~5,4 tỷ'),
    @('Chi phí (nhân sự, hạ tầng, bán hàng)', '~700 triệu', '~1,6 tỷ', '~3,2 tỷ'),
    @('Lợi nhuận trước thuế', '−120 triệu', '~700 triệu', '~2,2 tỷ')
) @(6.0, 3.3, 3.3, 3.4)
P 'Điểm hòa vốn dự kiến trong nửa cuối Năm 2 khi đạt khoảng 25–28 khách hàng trả phí.'

# ================== 9. LO TRINH ==================
H1 '9' 'LỘ TRÌNH TRIỂN KHAI'
AddTable @('Giai đoạn', 'Thời gian', 'Mục tiêu chính') @(
    @('Đã hoàn thành', 'Hiện tại', 'Sản phẩm hoàn chỉnh chạy thật trên hosting: nghiệp vụ lõi + 10+ tính năng AI + thanh toán VietQR + kết nối MISA'),
    @('Giai đoạn 1', 'Tháng 1–3', 'Pháp lý doanh nghiệp; mua bản quyền thương mại mô hình AI; thí điểm miễn phí tại 2–3 depot; hoàn thiện theo phản hồi'),
    @('Giai đoạn 2', 'Tháng 4–9', 'Ra mắt thương mại; đạt 10 khách trả phí; tuyển 2 lập trình viên + 1 kinh doanh; ứng dụng di động cho tài xế'),
    @('Giai đoạn 3', 'Tháng 10–24', 'Mở rộng 35+ khách; tích hợp cân điện tử, barrier tự động; chuẩn hóa API kết nối hãng tàu; gọi vốn vòng hạt giống'),
    @('Tầm nhìn', 'Năm 3+', 'Nền tảng số 1 cho cảng/depot vừa và nhỏ Việt Nam; mở rộng Đông Nam Á')
) @(3.2, 2.8, 10.0)

# ================== 10. DOI NGU ==================
H1 '10' 'ĐỘI NGŨ'
Bullet 'Lê Hữu Anh Tài — Sáng lập, phụ trách sản phẩm và công nghệ: trực tiếp xây dựng toàn bộ nền tảng (full-stack ASP.NET, cơ sở dữ liệu, 10+ mô hình AI tích hợp từ thị giác máy tính tới học tăng cường); am hiểu cả nghiệp vụ cảng lẫn công nghệ AI hiện đại nhất (YOLO26, WebGPU, Mamba, Active Learning).'
Bullet 'Kế hoạch bổ sung đồng sáng lập: 01 thành viên kinh doanh am hiểu ngành logistics (ưu tiên có quan hệ với depot/hãng tàu) và 01 thành viên vận hành – chăm sóc khách hàng.'
Bullet 'Cố vấn mong muốn: giảng viên hướng dẫn chuyên môn CNTT và chuyên gia khai thác cảng.'

# ================== 11. RUI RO ==================
H1 '11' 'RỦI RO CHÍNH VÀ PHƯƠNG ÁN ỨNG PHÓ'
AddTable @('Rủi ro', 'Ứng phó') @(
    @('Bản quyền mô hình AI (YOLO26 theo giấy phép AGPL-3.0 khi thương mại hóa)', 'Dự trù kinh phí mua Ultralytics Enterprise License, hoặc chuyển sang mô hình giấy phép Apache 2.0 (RT-DETR) — kiến trúc hệ thống đã thiết kế cho phép thay model không đổi code'),
    @('Khách hàng ngại thay đổi thói quen', 'Thí điểm miễn phí 2–3 tháng, giao diện tiếng Việt đơn giản, đào tạo tận nơi, song song dữ liệu cũ'),
    @('Đối thủ lớn gia nhập phân khúc', 'Đi nhanh chiếm nhóm depot vừa và nhỏ; khóa chân bằng dữ liệu Active Learning và tích hợp sâu VietQR/MISA'),
    @('Phụ thuộc một người phát triển', 'Tuyển sớm 2 lập trình viên; chuẩn hóa tài liệu mã nguồn (đã có sẵn bộ tài liệu kỹ thuật chi tiết)'),
    @('Bảo mật dữ liệu khách hàng', 'Ảnh khuôn mặt không lưu gốc (chỉ vector đặc trưng); phân quyền chặt; lộ trình đạt chứng nhận an toàn thông tin')
) @(7.5, 8.5)

# ================== 12. KEU GOI ==================
H1 '12' 'KÊU GỌI HỖ TRỢ / ĐẦU TƯ'
P 'Dự án kêu gọi 300 triệu đồng cho 6 tháng tăng tốc, phân bổ dự kiến:'
Bullet '40% — Nhân sự: tuyển 2 lập trình viên bán thời gian và 1 nhân sự kinh doanh.'
Bullet '25% — Pháp lý và bản quyền: thành lập doanh nghiệp, giấy phép thương mại mô hình AI.'
Bullet '20% — Thị trường: thí điểm tại 3 depot (thiết bị camera demo, đi lại, đào tạo).'
Bullet '15% — Hạ tầng và dự phòng: máy chủ, tên miền, chứng chỉ bảo mật.'
P 'Đổi lại, nhà đầu tư/đơn vị hỗ trợ nhận được quyền ưu tiên đàm phán cổ phần vòng hạt giống và quyền dùng thử trọn đời gói Enterprise.'

# ================== PHU LUC A: KICH BAN PITCH ==================
$sel.InsertBreak(7)
H1 'PHỤ LỤC A' 'KỊCH BẢN THUYẾT TRÌNH 5 PHÚT'
H2 'Phút 0:00 – 0:40 | Mở đầu bằng nỗi đau'
P '"Thưa Ban giám khảo, mỗi ngày tại hàng trăm depot container trên cả nước, xe tải xếp hàng dài trước cổng chỉ để một nhân viên ghi biển số bằng tay vào sổ. Phí lưu bãi thất thoát vì không ai nhớ container nằm đó bao nhiêu ngày. Còn phần mềm quản lý cảng chuẩn quốc tế thì có giá... hàng triệu đô. Em xin giới thiệu SmartPort — đưa công nghệ AI của cảng quốc tế xuống mức giá của một depot Việt Nam."'
H2 'Phút 0:40 – 1:30 | Giải pháp và bí quyết công nghệ'
P '"SmartPort là một website duy nhất quản trọn nghiệp vụ: điều độ tàu, bãi container, đặt dịch vụ, thanh toán QR. Nhưng bí quyết nằm ở chỗ: toàn bộ AI — camera đếm xe, đọc biển số, nhận diện khuôn mặt — chạy NGAY TRÊN TRÌNH DUYỆT bằng công nghệ WebGPU mới nhất, không cần máy chủ GPU đắt tiền. Chi phí hạ tầng gần như bằng 0 — đó là lý do chúng em bán được giá SaaS chỉ từ 2,9 triệu đồng/tháng."'
H2 'Phút 1:30 – 3:00 | Demo trực tiếp (mở sẵn website)'
Bullet 'Demo 1 (30 giây): Cổng thông minh — bấm ảnh xe tải, AI đọc biển số "15C-274.84", lưu nhật ký, danh sách xe cập nhật.'
Bullet 'Demo 2 (30 giây): Đăng nhập bằng khuôn mặt — quét mặt thật, tick xanh, vào thẳng hệ thống; giơ ảnh trên điện thoại — hệ thống chặn "giả mạo".'
Bullet 'Demo 3 (30 giây): Thống kê lưu lượng + khuyến nghị của AI Agent: "mở thêm làn cổng khung 15–16h" — đây là tiền thật khách tiết kiệm được.'
H2 'Phút 3:00 – 4:00 | Thị trường và mô hình kinh doanh'
P '"Thị trường logistics Việt Nam hơn 40 tỷ đô, riêng nhóm khách chúng em nhắm tới có hàng trăm depot và ICD chưa số hóa. Mô hình thuê bao 3 gói từ 2,9 tới 14,9 triệu/tháng. Với chỉ 80 khách sau 3 năm — tức hơn 10% nhóm mục tiêu — doanh thu đạt trên 5 tỷ đồng, hòa vốn từ năm thứ hai."'
H2 'Phút 4:00 – 5:00 | Traction, đội ngũ và lời kêu gọi'
P '"Khác với một ý tưởng trên giấy, sản phẩm của em đã chạy thật: đã triển khai hosting, tích hợp VietQR, kết nối kế toán MISA, và toàn bộ những gì Ban giám khảo vừa xem là demo trực tiếp, không dàn dựng. Em kêu gọi 300 triệu đồng cho 6 tháng: hoàn thiện pháp lý, bản quyền AI thương mại và thí điểm tại 3 depot. SmartPort — cảng thông minh trong tầm tay mọi doanh nghiệp Việt. Em cảm ơn Ban giám khảo!"'

# ================== PHU LUC B: HOI DAP ==================
H1 'PHỤ LỤC B' 'DỰ KIẾN CÂU HỎI CỦA BAN GIÁM KHẢO VÀ GỢI Ý TRẢ LỜI'
AddTable @('Câu hỏi', 'Gợi ý trả lời') @(
    @('AI chạy trên trình duyệt thì độ chính xác có kém không?', 'Mô hình YOLO26 bản chạy trình duyệt đạt mAP 40–55 tùy cỡ — đủ cho bài toán đếm xe/đọc biển ở cự ly cổng; hệ thống có Active Learning tự hiệu chỉnh theo phản hồi nên chính xác tăng dần theo thời gian sử dụng.'),
    @('Tại sao đối thủ lớn không làm điều tương tự?', 'Họ bị ràng buộc kiến trúc máy chủ GPU và mô hình giá cao; phân khúc depot nhỏ biên lợi nhuận mỏng không hấp dẫn họ, nhưng là đại dương xanh cho startup chi phí thấp.'),
    @('Doanh thu năm 1 dựa trên cơ sở nào?', '10 khách × gói trung bình ~5 triệu/tháng × trung bình 8 tháng sử dụng ≈ 400 triệu, cộng phí triển khai; đây là kịch bản thận trọng, đã trừ hao thời gian thuyết phục khách đầu tiên.'),
    @('Vấn đề bản quyền mô hình AI xử lý thế nào?', 'Hiện dùng cho nghiên cứu là hợp lệ; khi thương mại sẽ mua Ultralytics Enterprise License (đã dự trù trong 25% vốn kêu gọi) hoặc chuyển sang RT-DETR giấy phép Apache 2.0 — kiến trúc cho phép thay model không đổi mã nguồn.'),
    @('Dữ liệu khuôn mặt có rủi ro quyền riêng tư?', 'Hệ thống KHÔNG lưu ảnh gốc, chỉ lưu vector đặc trưng 512 số không thể dựng ngược thành ảnh; tính năng là tùy chọn, luôn có mật khẩu song song.'),
    @('Một mình bạn có vận hành nổi không?', 'Đó chính là lý do 40% vốn kêu gọi dành cho tuyển đội ngũ; toàn bộ hệ thống đã có tài liệu kỹ thuật chi tiết để bàn giao nhanh cho thành viên mới.'),
    @('Điều gì chứng minh khách sẽ trả tiền?', 'Một depot thất thoát chỉ 5% phí lưu bãi mỗi tháng đã lớn hơn phí thuê bao; giai đoạn thí điểm miễn phí 2–3 tháng sẽ đo đếm chính xác con số này làm bằng chứng bán hàng.'),
    @('Sản phẩm khác gì một đồ án sinh viên?', 'Đồ án dừng ở demo; SmartPort đã có đủ lớp thương mại: thanh toán VietQR thật, kết nối MISA, phân quyền, nhật ký an ninh, và lộ trình pháp lý — bản thân buổi demo hôm nay chạy trên hosting công khai.')
) @(5.5, 10.5)

# ================== PHU LUC C: HINH ANH ==================
H1 'PHỤ LỤC C' 'MỘT SỐ HÌNH ẢNH SẢN PHẨM THỰC TẾ'
AddImage '13-login-nut-face.png' 14 'Hình C1. Trang đăng nhập với Face ID'
AddImage '08-ai-agent.png' 14 'Hình C2. AI Agent điều phối — kế hoạch hành động kèm chuỗi suy luận'
AddImage '10-thanh-toan-qr.png' 14 'Hình C3. Thanh toán VietQR động chuẩn NAPAS 247'
AddImage '21-q-learning-xe-nang.png' 14 'Hình C4. Mô-đun Q-Learning: xe nâng tự học tìm đường tối ưu'
AddImage '20-cay-quyet-dinh.png' 14 'Hình C5. Cây quyết định dự đoán rủi ro hủy booking'

# ---- Danh so trang ----
$doc.Sections.Item(1).Footers.Item(1).PageNumbers.Add(1) | Out-Null

# ---- Luu ----
$doc.SaveAs2($outDocx, 16)              # wdFormatXMLDocument
$doc.ExportAsFixedFormat($outPdf, 17)   # wdExportFormatPDF
$pages = $doc.ComputeStatistics(2)      # wdStatisticPages
$doc.Close($false)
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
Write-Output "DA TAO: $outDocx ($pages trang) + PDF"
