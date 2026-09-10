# ============================================================
# KICH BAN THUYET TRINH — SmartPort (Word COM)
# Dong bo theo Slide_ThuyetTrinh_SmartPort.pptx moi (30 slide, 5 Chuong)
# ============================================================
$ErrorActionPreference = 'Stop'
$outDocx = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\KichBan_ThuyetTrinh_SmartPort.docx'
$outPdf  = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\KichBan_ThuyetTrinh_SmartPort.pdf'

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
$xam  = 124 + 107*256 + 91*65536

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

# Khoi 1 slide: tieu de + thoi luong + loi noi + thao tac
function Slide($num, $title, $time, $speech, $note) {
    H2 ("● SLIDE $num — $title   [" + $time + "]")
    $sel.Font.Italic = $true; $sel.Font.Color = $cam
    $sel.ParagraphFormat.Alignment = 3
    $sel.TypeText("Lời nói:"); $sel.TypeParagraph()
    $sel.Font.Italic = $false; $sel.Font.Color = 0
    foreach ($line in $speech) { P ('“' + $line + '”') 3 $false $false 13 (17 + 39*256 + 68*65536) }
    if ($note) {
        $sel.Font.Bold = $true; $sel.TypeText("Thao tác/Ghi chú: "); $sel.Font.Bold = $false
        $sel.TypeText($note); $sel.TypeParagraph()
    }
}

# ============================================================
# TIEU DE
# ============================================================
P 'KỊCH BẢN THUYẾT TRÌNH ĐỒ ÁN' 1 $true $false 18 $navy
P 'Website quản lý cảng trong ngành Logistics — Hệ thống SmartPort' 1 $true $true 14 $cam
P 'Môn: Lập trình Web  ·  SV: Lê Hữu Anh Tài — 2424802010066  ·  GVHD: ThS. Nguyễn Thành Trung' 1 $false $false 12
P '' 1
P 'Tổng thời lượng: ~16–18 phút (30 slide, đúng theo 5 chương của báo cáo) + 3–5 phút hỏi đáp. Tài liệu này gồm: (1) lời dẫn nhập, (2) kịch bản nói cho từng slide, (3) phần Hỏi–Đáp có câu trả lời mẫu, (4) mẹo trình bày. Phần trong ngoặc kép “…” là lời có thể nói gần như nguyên văn.' 3 $false $true 12
P ''

# ============================================================
# MO DAU
# ============================================================
H1 'A. LỜI DẪN NHẬP (trước Slide 1 — đứng nghiêm, nhìn hội đồng)'
$sel.Font.Color = (17 + 39*256 + 68*65536)
P '“Em kính chào quý thầy cô trong hội đồng. Em là Lê Hữu Anh Tài, mã số sinh viên 2424802010066. Hôm nay em xin phép được trình bày báo cáo đồ án môn Lập trình Web, với đề tài: Xây dựng Website quản lý cảng trong ngành Logistics — hệ thống SmartPort.”'
P '“Báo cáo của em gồm 5 chương, có đầy đủ sơ đồ UML — Use Case, Activity, Class Diagram và mô hình vật lý cơ sở dữ liệu — cùng hệ sinh thái 10 công nghệ AI, trong đó có 3 mô hình Deep Learning chạy thật ngay trên trình duyệt. Bài trình bày của em kéo dài khoảng 17 phút. Em xin phép bắt đầu ạ.”'
$sel.Font.Color = 0
PB

# ============================================================
# KICH BAN TUNG SLIDE
# ============================================================
H1 'B. KỊCH BẢN CHI TIẾT TỪNG SLIDE'

Slide 1 'Trang bìa' '~20 giây' @(
    'Đề tài của em là hệ thống SmartPort — một website quản lý cảng container được xây dựng trên nền ASP.NET MVC 5, Entity Framework 6 và SQL Server.',
    'Điểm khác biệt lớn nhất mà em muốn nhấn mạnh ngay từ đầu: hệ thống tích hợp 10 công nghệ AI, trong đó có 3 mô hình Deep Learning là YOLOv10, OCR mạng LSTM, và mạng nơ-ron TensorFlow.js — tất cả đều chạy thật, không phải mô phỏng.'
) 'Chỉ tay vào huy hiệu “10 công nghệ AI · 3 mô hình Deep Learning” và ảnh trang chủ bên phải.'

Slide 2 'Nội dung trình bày' '~20 giây' @(
    'Bài trình bày của em bám sát đúng 5 chương của báo cáo: Chương 1 tổng quan vấn đề nghiên cứu; Chương 2 cơ sở lý thuyết và công nghệ; Chương 3 phân tích thiết kế với đầy đủ sơ đồ UML; Chương 4 kết quả thực nghiệm — tức là toàn bộ giao diện hệ thống; và Chương 5 tổng kết, hướng phát triển.'
) 'Lướt nhanh, không đọc hết từng mục.'

Slide 3 'Chương I — Tổng quan vấn đề nghiên cứu' '~45 giây' @(
    'Về bối cảnh: cảng biển xử lý tới 90% khối lượng hàng hóa xuất nhập khẩu, nhưng nhiều cảng vừa và nhỏ vẫn vận hành thủ công — khách hàng phải đến tận nơi để tra cứu container, bộ phận điều độ phân bổ bến dựa vào kinh nghiệm, số liệu thì phân tán.',
    'Giải pháp của em là số hóa toàn bộ quy trình cảng trên một website duy nhất, và tiên phong đưa Deep Learning chạy ngay trên trình duyệt mà không cần máy chủ GPU.',
    'Hệ thống hướng tới 4 mục tiêu: cổng công khai để tra cứu; cổng ePort để đặt dịch vụ trực tuyến; khu quản trị đầy đủ nghiệp vụ; và tích hợp 10 công nghệ AI.'
) 'Nhấn vào các con số lớn bên phải (90%, 3 nhóm người dùng, 10 AI).'

Slide 4 'Chương II — Cơ sở lý thuyết & công nghệ nền tảng' '~35 giây' @(
    'Về công nghệ: phần lõi hệ thống dùng ASP.NET MVC 5 theo mô hình Model–View–Controller, với cơ chế Areas để tách riêng khu quản trị và phân quyền 3 vai trò.',
    'Tầng dữ liệu dùng Entity Framework 6 ánh xạ 14 bảng SQL Server, truy vấn bằng LINQ và tự động chống SQL Injection. Giao diện dùng Bootstrap 5 kết hợp CSS tự viết cho bộ nhận diện riêng và chế độ tối–sáng.'
) $null

Slide 5 'Chương II — Sơ đồ kiến trúc hệ thống' '~40 giây' @(
    'Đây là sơ đồ kiến trúc tổng thể, chia làm 3 tầng. Tầng trên cùng là trình duyệt — nơi chạy giao diện người dùng và đặc biệt là các mô hình AI suy luận ngay tại đây, không cần gửi dữ liệu lên máy chủ.',
    'Tầng giữa là ứng dụng ASP.NET MVC 5 gồm Controller, Model, View và lớp AppServices đóng gói các giải thuật AI, cùng lớp bảo mật phân quyền. Tầng dưới cùng là SQL Server lưu trữ toàn bộ dữ liệu. Em cũng tách riêng khối dịch vụ bên ngoài gồm Gemini API cho chatbot và máy chủ email gửi hóa đơn.'
) 'Đây là sơ đồ do em tự vẽ theo yêu cầu của thầy — nói chậm, chỉ tay theo từng tầng từ trên xuống.'

Slide 6 'Chương II — Nền tảng AI và Deep Learning' '~40 giây' @(
    'Về mặt lý thuyết, YOLO là họ mô hình phát hiện đối tượng một giai đoạn, dùng mạng nơ-ron tích chập để vừa xác định vị trí vừa phân loại đối tượng cùng lúc.',
    'OCR hiện đại dùng mạng LSTM để quét tuần tự và đọc ký tự, thay thế hoàn toàn cách so khớp mẫu truyền thống. Mạng MLP là kiến trúc nơ-ron truyền thẳng cơ bản, phù hợp cho bài toán dự báo chuỗi thời gian. Và chatbot hiện đại thường theo kiến trúc RAG — kết hợp truy xuất dữ liệu thật với khả năng sinh ngôn ngữ tự nhiên của mô hình lớn.'
) $null

Slide 7 'Chương III — Sơ đồ Use Case' '~40 giây' @(
    'Đây là sơ đồ Use Case theo chuẩn UML, mô tả những gì mỗi loại người dùng có thể làm trong hệ thống. Em có 3 tác nhân: khách hàng ở bên trái với 7 chức năng như đăng nhập, tra cứu container bằng OCR, đặt dịch vụ; nhân viên và quản trị viên ở bên phải với các chức năng vận hành và quản lý.',
    'Nổi bật nhất là hai use case màu cam bên phải: giám sát Camera AI và huấn luyện mô hình dự báo — chính là hai mô hình Deep Learning em sẽ demo ở phần sau.'
) $null

Slide 8 'Chương III — Sơ đồ Activity' '~40 giây' @(
    'Đây là sơ đồ Activity, mô tả một quy trình nghiệp vụ cụ thể theo trình tự thời gian — quy trình đặt và duyệt dịch vụ tại cổng ePort.',
    'Bắt đầu từ khi khách hàng đăng nhập, chọn dịch vụ, xác nhận đặt — hệ thống tự động tạo đơn ở trạng thái chờ duyệt. Sau đó nhân viên xem xét và ra quyết định tại điểm rẽ nhánh màu cam: nếu duyệt, hệ thống tự động xuất hóa đơn Excel và gửi email; nếu từ chối, hệ thống gửi thông báo cho khách. Đây chính là luồng nghiệp vụ trung tâm mà em đã lập trình thành công.'
) $null

Slide 9 'Chương III — Sơ đồ Class Diagram' '~35 giây' @(
    'Đây là sơ đồ lớp, mô tả cấu trúc tĩnh của hệ thống ở góc độ lập trình hướng đối tượng — các lớp Model, thuộc tính, và quan hệ liên kết giữa chúng kèm bội số một–nhiều.',
    'Vì em dùng Entity Framework Code First ánh xạ trực tiếp vào cơ sở dữ liệu có sẵn, nên các lớp Model gần như tương ứng một-một với các bảng dữ liệu ở sơ đồ tiếp theo.'
) $null

Slide 10 'Chương III — Mô hình vật lý cơ sở dữ liệu' '~35 giây' @(
    'Cuối cùng là mô hình vật lý cơ sở dữ liệu — chi tiết đến từng tên bảng, tên cột, kiểu dữ liệu, khóa chính và khóa ngoại.',
    'Cơ sở dữ liệu SmartPortDB có 14 bảng, trong đó 10 bảng có quan hệ khóa ngoại chặt chẽ mô tả nghiệp vụ lõi từ tàu, bến, container đến khách hàng, dịch vụ; và 4 bảng độc lập bên dưới phục vụ các chức năng hỗ trợ như tin tức, liên hệ, thống kê.'
) 'Kết thúc phần sơ đồ UML — chuyển giọng: "Sau đây em xin demo hệ thống đã triển khai."'

Slide 11 'Trang chủ' '~30 giây' @(
    'Bước sang Chương 4 — kết quả thực nghiệm. Trang chủ được thiết kế theo chuẩn cổng thông tin cảng hiện đại: mở đầu bằng ticker chạy dữ liệu chuyến tàu thật theo thời gian thực, khối hero với slider 4 ảnh cảng thật, và các số liệu thống kê lấy trực tiếp từ cơ sở dữ liệu.'
) $null

Slide 12 'Dark mode & thương hiệu' '~20 giây' @(
    'Website hỗ trợ chế độ giao diện Tối, tự nhận theo hệ điều hành và ghi nhớ lựa chọn của người dùng, đồng bộ trên toàn hệ thống. Em cũng tự thiết kế logo thương hiệu riêng.'
) $null

Slide 13 'Tra cứu container & OCR (Deep Learning #2)' '~35 giây' @(
    'Chức năng tra cứu cho phép nhập số container để xem trạng thái, vị trí bãi, hành trình dạng timeline và phí lưu bãi tự tính.',
    'Đặc biệt bên phải là mô hình Deep Learning thứ hai: người dùng chụp ảnh vỏ container, mạng LSTM sẽ đọc ký tự và tự nhận ra mã hợp lệ. Em đã thử nghiệm và mô hình đọc chính xác mã MSCU2201417 với độ tin cậy 93%.'
) 'Nếu demo trực tiếp: mở trang Tra cứu, bấm “chạy thử ảnh mẫu” cho hội đồng xem AI đọc số.'

Slide 14 'Lịch tàu · Dịch vụ · Tin tức' '~20 giây' @(
    'Ngoài ra cổng công khai còn có trang lịch tàu hiển thị đầy đủ thời gian dự kiến và thực tế; trang bảng giá 12 dịch vụ theo 5 nhóm; và trang tin tức có phân trang, hình ảnh thật.'
) $null

Slide 15 'Chatbot PortBot & giọng nói' '~35 giây' @(
    'PortBot là trợ lý ảo thường trực trên mọi trang. Nó nhận diện ý định tiếng Việt, trích mã container hoặc booking bằng biểu thức chính quy, rồi trả lời bằng dữ liệu thật lấy từ cơ sở dữ liệu — đúng kiến trúc RAG em vừa trình bày ở Chương 2.',
    'Ngoài ra em tích hợp trợ lý giọng nói: người dùng nói câu hỏi bằng tiếng Việt và nghe bot đọc câu trả lời.'
) 'Nếu demo: bấm biểu tượng chat, gõ hoặc nói “lịch tàu tuần này”.'

Slide 16 'Ước tính chi phí bằng AI' '~25 giây' @(
    'Đây là công cụ ước tính chi phí: khách hàng khai báo lô hàng, hệ thống tính chi tiết từng khoản theo biểu giá thật, vẽ biểu đồ cơ cấu chi phí và đưa ra nhận xét thông minh. Công cụ này miễn phí, không cần đăng nhập.'
) $null

Slide 17 'ePort — đặt dịch vụ trực tuyến' '~30 giây' @(
    'Cổng ePort dành cho khách hàng: chọn dịch vụ và số lượng, hệ thống tính tiền tức thời. Mỗi booking được cấp mã tự động và theo dõi qua các trạng thái đúng như sơ đồ Activity em đã trình bày — từ chờ duyệt đến hoàn thành.',
    'Em đã kiểm thử: tạo booking BK260009 với tổng tiền khớp tuyệt đối 1.590.000 đồng.'
) $null

Slide 18 'Theo dõi booking & hồ sơ' '~20 giây' @(
    'Khách hàng có trang riêng để theo dõi danh sách booking, hủy khi còn ở trạng thái chờ duyệt, và trang hồ sơ để cập nhật thông tin, đổi mật khẩu.'
) $null

Slide 19 'Hóa đơn 3 kênh: Excel · PDF · Email' '~30 giây' @(
    'Từ mỗi booking, hệ thống phát hành hóa đơn theo ba kênh: file Excel chuẩn kế toán Việt Nam tự động đọc số tiền thành chữ; bản in PDF chuyên nghiệp kèm logo; và gửi email đính kèm hóa đơn tới khách hàng.'
) $null

Slide 20 'Dashboard quản trị' '~25 giây' @(
    'Chuyển sang khu quản trị. Dashboard tổng hợp 6 thẻ chỉ số theo thời gian thực, một panel trợ lý AI cảnh báo vận hành, biểu đồ sản lượng kèm dự báo AI.'
) $null

Slide 21 'Điều độ chuyến tàu & AI phân bổ bến' '~40 giây' @(
    'Bộ phận điều độ quản lý vòng đời chuyến tàu bằng các nút chuyển trạng thái nhanh, tự động ghi giờ thực tế.',
    'Trang AI phân bổ bến chấm điểm từng bến dựa trên các ràng buộc an toàn hàng hải: chiều dài bến phải lớn hơn chiều dài tàu cộng 15 mét dây buộc, độ sâu lớn hơn mớn nước cộng nửa mét, không trùng lịch. AI còn dự đoán thời gian giải phóng tàu — đúng giải thuật em đã mô tả chi tiết ở Chương 3.'
) $null

Slide 22 'Container & sơ đồ bãi thông minh' '~25 giây' @(
    'Trang quản lý container có tìm kiếm, lọc, và nút AI gợi ý vị trí hạ bãi tối ưu. Bên phải là sơ đồ bãi trực quan chia 6 khu, tô màu theo chiều cao chồng container, đánh dấu container lạnh.'
) $null

Slide 23 'Camera AI — YOLOv10 (Deep Learning #1)' '~55 giây' @(
    'Đây là mô hình Deep Learning quan trọng nhất — Camera AI dùng YOLOv10, thế hệ YOLO mới nhất năm 2024 với thiết kế NMS-free.',
    'Quy trình: ảnh đầu vào chuẩn hóa về 640×640, đưa qua mạng nơ-ron tích chập, cho ra tối đa 300 dự đoán, rồi vẽ khung kèm nhãn tiếng Việt. Hệ thống còn tự sinh cảnh báo điều phối, ví dụ phát hiện từ 3 xe tải trở lên sẽ đề xuất mở thêm làn cổng.',
    'Kết quả thực nghiệm: nhận diện xe tải đạt độ tin cậy 87%, tốc độ 286 mili-giây mỗi khung hình, ngay trên CPU thông thường.'
) 'ĐÂY LÀ ĐIỂM NHẤN. Nếu có mạng: bật webcam AI, giơ điện thoại/cốc nước cho hội đồng thấy AI nhận diện real-time — sẽ rất ấn tượng.'

Slide 24 'AI Lab — huấn luyện mạng nơ-ron (Deep Learning #3)' '~50 giây' @(
    'Mô hình Deep Learning thứ ba là AI Lab — nơi em tự huấn luyện mạng nơ-ron ngay trên trình duyệt bằng TensorFlow.js, dùng mạng MLP kiến trúc 3–16–8–1, học chuỗi 18 tháng sản lượng bằng cửa sổ trượt.',
    'Người dùng có thể chỉnh siêu tham số rồi xem đường mất mát giảm theo thời gian thực. Thực nghiệm: 200 epoch hoàn tất trong 9,7 giây, sai số trung bình khoảng 5.900 TEU.'
) 'Nếu demo: kéo epochs, bấm “Bắt đầu huấn luyện”, cho hội đồng thấy đường loss giảm trực tiếp.'

Slide 25 'Bến LIVE & xử lý booking phía quản trị' '~25 giây' @(
    'Sơ đồ cầu bến LIVE hiển thị động những bến đang có tàu chiếm. Bên phải là màn hình xử lý booking phía quản trị, nơi nhân viên duyệt đơn và phát hành hóa đơn — đúng quy trình ở sơ đồ Activity.'
) $null

Slide 26 'Quản trị danh mục — biểu mẫu' '~20 giây' @(
    'Mỗi danh mục như tàu, hãng tàu, khách hàng, dịch vụ, tin tức, tài khoản đều có trang danh sách kèm biểu mẫu thêm và sửa đầy đủ, có kiểm tra ràng buộc dữ liệu.'
) $null

Slide 27 'Thư viện ảnh & báo cáo thống kê' '~20 giây' @(
    'Toàn bộ 21 vị trí ảnh của website được quản lý động. Trang báo cáo thống kê sản lượng và doanh thu theo tháng, có biểu đồ và cho phép xuất Excel.'
) $null

Slide 28 'Chương IV — Kết quả kiểm thử' '~30 giây' @(
    'Về kiểm thử: em đã quét tự động toàn bộ 26 đường dẫn, tất cả trả về mã 200 OK; kiểm thử 12 trường hợp nghiệp vụ đều đạt.',
    'Một vài con số minh chứng: OCR đọc container 93%, YOLO nhận diện 87%, hệ thống có 44 giao diện, 24 controller, 14 bảng dữ liệu.'
) 'Nói dứt khoát các con số để tạo độ tin cậy.'

Slide 29 'Chương V — Tổng kết & hướng phát triển' '~40 giây' @(
    'Tóm lại, đồ án đã hoàn thành một hệ thống quản lý cảng đầy đủ với cổng ePort, hóa đơn ba kênh, đầy đủ sơ đồ UML theo đúng yêu cầu, và điểm nhấn là hệ sinh thái 10 công nghệ AI với 3 mô hình Deep Learning chạy thật — tất cả đều được kiểm chứng bằng số liệu.',
    'Về hướng phát triển, em dự định tinh chỉnh mô hình YOLO trên dữ liệu container Việt Nam, bổ sung dự báo bằng LSTM khi có đủ dữ liệu, và triển khai lên đám mây.'
) $null

Slide 30 'Cảm ơn & Hỏi đáp' '~15 giây' @(
    'Trên đây là toàn bộ phần trình bày của em. Em xin chân thành cảm ơn quý thầy cô đã lắng nghe, và em rất mong nhận được các câu hỏi cũng như góp ý ạ.'
) 'Đứng thẳng, mỉm cười, sẵn sàng cho Q&A.'
PB

# ============================================================
# HOI DAP
# ============================================================
H1 'C. PHẦN HỎI – ĐÁP (chuẩn bị sẵn — rất quan trọng để đạt A+)'
P 'Nguyên tắc trả lời: bình tĩnh, nghe hết câu hỏi, nhắc lại ý chính rồi trả lời gọn. Nếu không chắc, hãy trung thực và nêu hướng khắc phục — thầy cô đánh giá cao sự trung thực hơn là trả lời liều.' 3 $false $true

function QA($q, $a) {
    $sel.Font.Bold = $true; $sel.Font.Color = $navy
    $sel.ParagraphFormat.Alignment = 3
    $sel.TypeText("Hỏi: "); $sel.Font.Color = 0
    $sel.TypeText($q); $sel.TypeParagraph()
    $sel.Font.Bold = $false
    $sel.Font.Bold = $true; $sel.TypeText("Đáp: "); $sel.Font.Bold = $false
    $sel.TypeText($a); $sel.TypeParagraph()
    $sel.TypeParagraph()
}

QA 'Vì sao chọn vẽ Use Case, Activity, Class Diagram mà không phải Sequence hay Component Diagram?' 'Dạ em chọn 3 sơ đồ này vì chúng bao phủ đủ 3 góc nhìn quan trọng nhất và đúng theo yêu cầu trong mẫu báo cáo của thầy: Use Case cho góc nhìn chức năng — ai làm gì; Activity cho góc nhìn quy trình — luồng xử lý theo thời gian; và Class Diagram cho góc nhìn cấu trúc tĩnh — các lớp và quan hệ. Đây cũng là 3 loại sơ đồ UML được dùng phổ biến nhất khi phân tích thiết kế hướng đối tượng. Em có thể bổ sung Sequence Diagram nếu thầy cô yêu cầu thêm.'

QA 'Mô hình vật lý cơ sở dữ liệu khác gì mô hình logic?' 'Dạ mô hình logic mô tả các thực thể và quan hệ ở mức khái niệm, chưa gắn với công nghệ cụ thể. Mô hình vật lý mà em trình bày là bước triển khai cụ thể trên SQL Server: có kiểu dữ liệu chính xác như INT, NVARCHAR, DECIMAL, có khai báo khóa chính khóa ngoại rõ ràng — đây chính là bản thiết kế em dùng để viết script tạo cơ sở dữ liệu thật.'

QA 'Các mô hình AI này em có tự huấn luyện không hay dùng có sẵn?' 'Dạ em xin trả lời trung thực: mô hình YOLOv10 và OCR em dùng trọng số đã huấn luyện sẵn trên tập dữ liệu tổng quát (COCO và văn bản Latin), rồi tích hợp và xây dựng quy trình xử lý xung quanh. Riêng mạng nơ-ron dự báo sản lượng ở AI Lab thì em tự huấn luyện hoàn toàn ngay trên trình duyệt, thầy cô có thể thấy quá trình huấn luyện trực tiếp. Việc dùng mô hình pre-trained cho object detection và OCR là cách làm chuẩn trong công nghiệp vì huấn luyện lại từ đầu cần tập dữ liệu rất lớn.'

QA 'Deep Learning chạy trên trình duyệt là như thế nào, có thật không?' 'Dạ có thật ạ. Mô hình được xuất sang định dạng ONNX hoặc TensorFlow.js, sau đó thư viện ONNX Runtime Web sẽ tải mô hình về và chạy suy luận bằng WebAssembly ngay trong trình duyệt của người dùng. Nghĩa là mọi tính toán diễn ra trên máy khách, máy chủ không cần GPU.'

QA 'AI phân bổ bến có phải là trí tuệ nhân tạo thật không?' 'Dạ em phân loại rõ: đây là AI theo hướng thuật toán tối ưu và hệ luật (rule-based / heuristic), không phải mạng nơ-ron. Nó chấm điểm đa tiêu chí dựa trên các ràng buộc an toàn hàng hải thực tế. Trong 10 công nghệ AI của em, có loại là Deep Learning, có loại là tối ưu hóa, có loại là hệ chuyên gia — em đã ghi rõ phân loại trong báo cáo ở mục 3.3.1.'

QA 'Hệ thống chống SQL Injection và bảo mật như thế nào?' 'Dạ, em dùng Entity Framework 6 nên mọi truy vấn đều được tham số hóa tự động, chống SQL Injection. Mật khẩu được băm bằng SHA-256. Mọi biểu mẫu POST đều có AntiForgeryToken chống CSRF. Và mỗi controller trong khu quản trị đều kiểm tra vai trò người dùng trước khi cho truy cập — đúng mô hình phân quyền em trình bày ở mục 3.1.5.'

QA 'Vì sao không huấn luyện YOLO trên dữ liệu cảng Việt Nam?' 'Dạ đây đúng là một hạn chế em đã nêu trong phần kết luận. Để tinh chỉnh (fine-tune) YOLO cần một tập dữ liệu ảnh container và xe đầu kéo được gán nhãn thủ công, số lượng lớn — vượt quá phạm vi và thời gian của học phần. Em đã đề xuất đây là hướng phát triển tiếp theo.'

QA 'Nếu mất kết nối Internet thì các chức năng AI có chạy được không?' 'Dạ, các mô hình AI cần tải một lần từ CDN nên lần đầu cần mạng; sau khi tải, phần suy luận chạy cục bộ. Nếu không tải được YOLO, hệ thống tự chuyển sang mô hình SSD MobileNet dự phòng. Các nghiệp vụ cốt lõi như tra cứu, đặt dịch vụ vẫn hoạt động bình thường vì chạy phía máy chủ.'

QA 'Chatbot có phải AI không, RAG là gì?' 'Dạ chatbot của em hoạt động hai tầng. Tầng một là nhận diện ý định và truy vấn dữ liệu thật, luôn chạy được. Tầng hai là tùy chọn kết nối mô hình ngôn ngữ lớn Gemini theo kiến trúc RAG — viết tắt của Retrieval-Augmented Generation, nghĩa là truy xuất dữ liệu rồi mới sinh câu trả lời, giúp câu trả lời tự nhiên nhưng luôn dựa trên dữ liệu thật, tránh việc AI bịa thông tin.'

QA 'Điểm khác biệt của SmartPort so với website cảng thật như Tân Cảng Sài Gòn?' 'Dạ, em có khảo sát website Tân Cảng Sài Gòn và kế thừa cấu trúc cổng thông tin chuyên nghiệp của họ. Điểm khác biệt em bổ sung là: ticker chuyến tàu thời gian thực, công cụ ước tính chi phí bằng AI, và đặc biệt là 3 mô hình Deep Learning chạy công khai ngay trên web.'

QA 'Đề tài còn hạn chế gì?' 'Dạ em xin nêu thẳng thắn: các mô hình dùng trọng số tổng quát chưa tinh chỉnh trên dữ liệu cảng; mô hình dự báo mới học từ 18 điểm dữ liệu nên sai số còn đáng kể; và hệ thống chưa có thông báo đẩy thời gian thực, chưa triển khai lên máy chủ công cộng. Đây đều là những hướng em sẽ hoàn thiện tiếp.'
PB

# ============================================================
# MEO TRINH BAY
# ============================================================
H1 'D. MẸO TRÌNH BÀY ĐỂ ĐẠT ĐIỂM CAO'
P '1. Luyện nói trước 2–3 lần, bấm giờ để chắc chắn trong 16–18 phút. Không đọc nguyên văn slide — hãy nói bằng lời của mình.'
P '2. Mở sẵn website (nhấn F5) và đăng nhập tài khoản admin TRƯỚC khi thuyết trình, để demo mượt. Chuẩn bị sẵn 1 ảnh container và 1 ảnh có xe/người để demo OCR và YOLO.'
P '3. Ở 4 slide sơ đồ UML (Slide 5, 7, 8, 9, 10), hãy dùng con trỏ chuột chỉ trực tiếp vào từng khối trên sơ đồ khi nói — đây là điểm mới thầy đặc biệt yêu cầu, đừng lướt qua nhanh.'
P '4. Dành nhiều thời gian và năng lượng nhất cho 2 slide Deep Learning nền navy-tím (Slide 23 YOLO, Slide 24 AI Lab) — đây là phần ăn điểm. Nếu máy có mạng và webcam, demo trực tiếp YOLO real-time sẽ gây ấn tượng mạnh nhất.'
P '5. Nói to, rõ, chậm ở các slide kỹ thuật (Slide 4, 5, 9, 10, 23, 24). Nhìn vào hội đồng, không nhìn chằm chằm màn hình.'
P '6. Khi bị hỏi khó, đừng hoảng: nghe hết câu hỏi → hít thở → nhắc lại ý → trả lời gọn. Nếu không biết, nói “Dạ phần này em chưa nghiên cứu sâu, em xin ghi nhận và tìm hiểu thêm ạ.” Sự trung thực được đánh giá cao.'
P '7. Nhấn mạnh các con số thực nghiệm (93%, 87%, 286ms, 9,7 giây, 26/26 trang OK) — chúng chứng minh em đã làm thật và kiểm thử thật.'
P '8. Kết thúc dứt khoát, cảm ơn hội đồng, giữ thái độ tự tin và cầu thị trong suốt phần hỏi đáp.'
P ''
P 'Chúc bạn bảo vệ thành công và đạt điểm A+!' 1 $true $true 13 $cam

# ---------- Luu ----------
if (Test-Path $outDocx) { [IO.File]::Delete($outDocx) }
$doc.SaveAs2($outDocx, 16)
$doc.ExportAsFixedFormat($outPdf, 17)
$pages = $doc.ComputeStatistics(2)
$doc.Close($false)
$word.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
"XONG: $outDocx — $pages trang"
