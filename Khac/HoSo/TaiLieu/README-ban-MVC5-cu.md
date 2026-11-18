# 🚢 SmartPort — Website Quản lý Cảng trong ngành Logistics

**Đồ án môn Lập trình Web** — Sinh viên: **Lê Hữu Anh Tài** (MSSV: 2424802010066)

Website quản lý cảng container ứng dụng **ASP.NET MVC 5 + Entity Framework 6 + SQL Server**, giao diện Bootstrap 5 tiếng Việt, tích hợp **3 tính năng AI**.

---

## 1. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET MVC 5 (.NET Framework 4.8), C# |
| ORM | Entity Framework 6.4 (Code First mapping vào CSDL có sẵn) |
| CSDL | SQL Server — database `SmartPortDB` (13 bảng) |
| Frontend | Bootstrap 5, Bootstrap Icons, Chart.js 4, font Be Vietnam Pro |
| AI | Chatbot PortBot (NLP intent + tra dữ liệu thật + tích hợp Google Gemini), dự báo sản lượng (hồi quy tuyến tính), gợi ý phân bổ bến (thuật toán chấm điểm) |
| Bảo mật | Hash mật khẩu SHA-256, phân quyền 3 vai trò qua Session, Anti-Forgery Token |

## 2. Cách chạy project

### Bước 1 — Tạo cơ sở dữ liệu
Mở **SQL Server Management Studio** (hoặc sqlcmd), chạy file:
```
Database\SmartPort_DB.sql
```
Script tự tạo database `SmartPortDB` + toàn bộ dữ liệu mẫu (tàu, chuyến, container, booking, tin tức...). Dữ liệu lịch tàu dùng ngày **tương đối với ngày hiện tại** nên demo lúc nào cũng "sống".

> Lưu ý: connection string mặc định trỏ tới instance `.\MSSQLSERVER01` (Windows Authentication). Nếu máy bạn dùng instance khác, sửa trong `SmartPort\Web.config` → `<connectionStrings>`.

### Bước 2 — Mở và chạy bằng Visual Studio
1. Mở file **`SmartPort.sln`** bằng Visual Studio 2022/2026.
2. Chuột phải project → **Restore NuGet Packages** (VS thường tự làm khi build).
3. Nhấn **F5** (hoặc Ctrl+F5) để chạy bằng IIS Express.

Hoặc chạy bằng dòng lệnh không cần mở VS:
```
MSBuild SmartPort\SmartPort.csproj /t:Restore,Build
"C:\Program Files\IIS Express\iisexpress.exe" /path:"<đường dẫn>\SmartPort\SmartPort" /port:52080
```
rồi mở http://localhost:52080

## Thông tin liên hệ hiển thị trên website
- **Hotline: 0387 562 579 — Quản lý: Lê Hữu Anh Tài** · Email: **lehuuanhtai2020tkh@gmail.com**
- Facebook cá nhân + Zalo cấu hình tại `Web.config` (`FacebookCaNhan`, `ZaloPhone`)

## 3. Tài khoản demo (mật khẩu chung: `123456`)

| Tài khoản | Vai trò | Quyền |
|---|---|---|
| `admin` | Quản trị viên | Toàn quyền, gồm quản lý tài khoản |
| `dieudo` | Nhân viên khai thác | Toàn bộ nghiệp vụ trừ quản lý tài khoản |
| `taile` | Khách hàng (Cty XNK Đại Phát) | Đặt dịch vụ online, theo dõi booking |
| `thuha` | Khách hàng (Thủy sản Cửu Long) | Như trên |

## 4. Chức năng chính

### Trang công khai (khách truy cập)
- **Trang chủ**: hero minh họa cảng, thống kê thời gian thực, dịch vụ, lịch tàu, tin tức
- **Tra cứu container**: nhập số cont → trạng thái, vị trí bãi, hành trình (timeline), ước tính phí lưu bãi (thử: `MSCU2201417`, `MSCU2299876` — cont lạnh, `MSCU2210555` — chờ xuất)
- **Lịch tàu**: ETA/ETD/ATA/ATD, lọc theo trạng thái & hãng tàu
- **Dịch vụ & bảng giá**: 12 dịch vụ theo 5 nhóm
- **Tin tức**: danh sách + chi tiết, đếm lượt xem
- **Liên hệ**: gửi yêu cầu vào hộp thư quản trị
- **Đăng ký / đăng nhập** tài khoản khách hàng
- **ePort — Đặt dịch vụ online** (khách hàng): chọn dịch vụ + số lượng, tự tính tiền, theo dõi trạng thái, hủy khi chờ duyệt

### Khu quản trị `/Admin` (Admin + Nhân viên)
- **Dashboard**: 6 thẻ chỉ số, 3 biểu đồ Chart.js, bảng booking & chuyến tàu mới
- **Điều độ chuyến tàu**: CRUD chuyến, nút chuyển trạng thái nhanh (Cập bến → Làm hàng → Rời bến, tự ghi ATA/ATD), kiểm tra bến đủ chiều dài/độ sâu khi gán
- **Container**: CRUD, tìm kiếm/lọc, nút **Gate out** giao hàng
- **Tàu biển / Hãng tàu / Bến cảng**: CRUD đầy đủ, chặn xóa khi có dữ liệu liên quan
- **Đơn đặt dịch vụ**: duyệt / từ chối / bắt đầu / hoàn thành
- **Khách hàng, Tin tức, Hộp thư liên hệ**: quản lý đầy đủ
- **Tài khoản** (chỉ Admin): tạo, phân vai trò, khóa/mở, reset mật khẩu
- **Báo cáo**: sản lượng TEU & doanh thu theo tháng/năm, biểu đồ kết hợp, in báo cáo

### 🤖 6 trợ lý AI (điểm nhấn đề tài)
1. **Chatbot PortBot** (mọi trang, góc phải dưới): nhận diện ý định tiếng Việt (có/không dấu), trích mã container/booking bằng regex, **truy vấn dữ liệu thật** từ CSDL trả lời: tra container (kèm phí lưu bãi, điện lạnh), lịch tàu, bảng giá, trạng thái booking, thông tin cảng. Nếu điền `GeminiApiKey` trong `Web.config`, câu trả lời được **Google Gemini** diễn đạt tự nhiên hơn (kiến trúc RAG đơn giản) — không có key vẫn chạy đầy đủ.
2. **AI dự báo sản lượng** (Dashboard): hồi quy tuyến tính (least squares) trên 18 tháng dữ liệu → dự báo 3 tháng tới, vẽ đường dự báo nét đứt trên biểu đồ, tính % tăng trưởng.
3. **AI gợi ý phân bổ bến + dự đoán giờ giải phóng tàu** (`/Admin/AiTools/Berth`): lọc bến đủ điều kiện (chiều dài ≥ LOA + 15m dây buộc, độ sâu ≥ mớn nước + 0.5m an toàn, không trùng lịch ±6h), **chấm điểm** ưu tiên bến "vừa vặn", đồng thời **dự đoán thời gian làm hàng** = tổng moves / (số cẩu × 28 moves/h) — bấm 1 nút áp dụng.
4. **AI gợi ý vị trí hạ bãi** (nút ✨AI trong form Container): chọn KHU theo đặc tính cont (lạnh→R có điện, rỗng→E, chờ xuất→X sát cầu, nhập 20'→A, nhập 40'→B), rồi chọn stack thấp nhất còn chỗ để cân bằng tải, hạn chế đảo chuyển. Kèm **Sơ đồ bãi trực quan** (`/Admin/Containers/YardMap`).
5. **AI trợ lý cảnh báo vận hành** (Dashboard): quét dữ liệu theo bộ luật nghiệp vụ — tàu đến trong 24h chưa có bến, cont lạnh thiếu nhiệt độ, hàng tồn bãi >15 ngày, booking quá 48h chưa duyệt, dự báo % tải cẩu bờ 7 ngày tới... xếp theo mức độ ưu tiên kèm nút xử lý.
6. **AI ước tính chi phí** (menu "Ước tính phí" — công khai): khách khai báo lô hàng → tính chi tiết từng khoản theo biểu giá thật trong CSDL, vẽ biểu đồ cơ cấu chi phí, sinh **nhận xét thông minh** (cảnh báo phí lưu bãi, ưu đãi sản lượng, gợi ý tiết kiệm).

### 🧾 Xuất hóa đơn & báo cáo Excel (.xlsx thật — EPPlus)
- **Hóa đơn dịch vụ cảng** cho từng booking: khung hóa đơn kế toán VN (đơn vị phát hành, khách hàng, bảng dịch vụ có kẻ khung, cộng tiền + VAT 8% + tổng, **tự động đọc số tiền thành chữ**, khối chữ ký 3 bên)
  - Admin: nút "Xuất hóa đơn Excel" trong trang xử lý booking
  - Khách hàng: nút "Tải hóa đơn Excel" trong booking của mình (khi đã duyệt trở lên)
- **Báo cáo năm** (`Admin → Báo cáo → Xuất Excel`): file 2 sheet — Sản lượng TEU & doanh thu theo tháng + Doanh thu dịch vụ ePort
- Thư viện: **EPPlus 4.5.3** (NuGet, tự restore khi build)

### 🧠 DEEP LEARNING #2 — OCR đọc số container (v6)
- Trang **Tra cứu container** có khung "Quét số container bằng AI": tải/chụp ảnh vỏ container → mạng **LSTM (Tesseract.js)** đọc ký tự ngay trên trình duyệt → nhận diện mã hợp lệ (4 chữ + 7 số) → tự điền ô tra cứu
- Tiền xử lý ảnh tự động: phóng to, grayscale, tăng tương phản; nút "chạy thử với ảnh mẫu" để demo không cần ảnh thật
- Đã test thật: đọc đúng **MSCU2201417 với độ tin cậy 93%**

### 🧠 DEEP LEARNING #3 — AI Lab: tự huấn luyện mạng nơ-ron (v6)
- Trang **Admin → AI Lab (NEURAL)**: huấn luyện mô hình **MLP** (TensorFlow.js) dự báo TEU **ngay trên trình duyệt**: cửa sổ trượt 3 tháng → dự đoán tháng sau, tối ưu **Adam**, loss MSE
- Chỉnh **siêu tham số**: epochs (50–500), learning rate, số nơ-ron lớp ẩn — xem **đường loss giảm real-time** (thang log), sơ đồ kiến trúc mạng, so sánh dự báo **Neural Network vs Hồi quy tuyến tính** trên biểu đồ + bảng
- Đã test thật: 200 epochs / 9.7 giây, MAE ±5.897 TEU

### 🎤 Trợ lý giọng nói tiếng Việt (v6)
- PortBot thêm nút **mic** (nói câu hỏi bằng tiếng Việt — Web Speech API) và nút **loa** (bot đọc to câu trả lời) — rảnh tay khi đang lái xe vào cảng

### 🧠 DEEP LEARNING #1 — Camera AI YOLOv10 (v5)
- Trang **Admin → Camera AI (YOLO)**: mô hình **YOLOv10n** (kiến trúc YOLO thế hệ mới nhất, NMS-free, công bố 2024) chạy **ngay trên trình duyệt** bằng ONNX Runtime Web — không cần GPU/server AI
- Nhận diện **80 loại đối tượng** (xe tải, ô tô, người, tàu thuyền...): vẽ khung + nhãn tiếng Việt + % tin cậy, **đếm số lượng theo loại**, đo tốc độ ms/khung hình, chỉnh ngưỡng tin cậy bằng slider
- 3 nguồn ảnh: **upload từ máy**, **4 ảnh mẫu**, hoặc **webcam real-time** (phân tích liên tục)
- **Cảnh báo điều phối tự sinh**: ≥3 xe tải → gợi ý mở thêm làn cổng; đông người → nhắc an toàn lao động; phát hiện tàu thuyền...
- Engine dự phòng tự động: SSD MobileNet (TensorFlow.js) nếu YOLO không tải được
- Đã test thực tế: ảnh xe đầu kéo → "Xe tải/đầu kéo ×1" (286ms); ảnh cuộc họp → 8 đối tượng (2 người, laptop, cốc...)

### 📱 Kết nối chủ dự án (v5)
- **Facebook cá nhân** (facebook.com/anh.tai.129573) + **nút Zalo nổi** góc trái màn hình — cấu hình tại `Web.config` (`FacebookCaNhan`, `ZaloPhone` — **thay số Zalo thật của bạn vào đây**)
- Trang chủ thêm: **băng parallax số liệu** (ảnh thật đứng yên khi cuộn — hiệu ứng chiều sâu) + **dải phim 10 ảnh thật** chạy ngang vô tận

### 🎨 Nhận diện thương hiệu & trải nghiệm (v4)
- **Logo SmartPort thiết kế riêng** (SVG độc quyền: cẩu STS + container cam + sóng biển + tín hiệu AI nhấp nháy) — dùng thống nhất ở navbar, footer, admin, favicon, hóa đơn
- **Chế độ Tối – Sáng** 🌙☀️: nút chuyển trên navbar (cả public + admin), ghi nhớ lựa chọn, phủ toàn bộ giao diện
- **Hero slider 4 ảnh thật tự chạy** (crossfade + Ken Burns, chấm điều hướng)
- **Đánh giá khách hàng** (5★), **băng giải thưởng/chứng nhận** chạy ngang, **FAQ accordion**, **băng đăng ký bản tin**, **timeline chặng đường phát triển** (trang giới thiệu)
- Thanh tiến trình cuộn trang + nút lên đầu trang

### 🧾 Hóa đơn 3 kênh: Excel · PDF · Email
- **Excel (.xlsx)**: như mô tả trên (EPPlus)
- **PDF**: nút "Hóa đơn PDF" mở bản in thiết kế chuyên nghiệp (logo, bảng kê, bằng chữ, chữ ký) → bấm In / Lưu thành PDF
- **Email**: nút gửi hóa đơn (đính kèm Excel) tới email khách hàng — chưa cấu hình SMTP thì chạy **chế độ demo** (file .eml lưu ở `App_Data/Outbox`, mở bằng Outlook); điền `SmtpHost/SmtpUser/SmtpPass` trong Web.config (VD Gmail App Password) là gửi thật

### 🖼️ Trang quản lý thư viện ảnh (Admin → Thư viện ảnh)
- 21 vị trí ảnh của website (hero slider, banner, tin tức, thư viện...) quản lý qua bảng `CauHinhAnh`
- Dán URL mới **hoặc upload ảnh riêng từ máy** (≤5MB, lưu `/Content/img/uploads`) — áp dụng ngay không cần sửa code

### 📷 Hình ảnh thật
- Toàn bộ ảnh nền hero, banner trang con, thumbnail tin tức, thư viện "Khoảnh khắc tại cảng" (6 ảnh **Ken Burns** — tự phóng to/lia chậm), ảnh trang giới thiệu đều là **ảnh cảng biển thật** từ Unsplash (giấy phép miễn phí, nhúng qua CDN như Bootstrap)
- Quản lý tập trung tại `Helpers/Photos.cs` — muốn thay ảnh riêng chỉ sửa 1 file này (có lớp gradient dự phòng khi mất mạng)

### ✨ Hiệu ứng & trực quan sinh động
- **Ticker "TRỰC TIẾP"** chạy ngang đầu trang: tình hình tàu đang làm hàng/sắp cập lấy từ dữ liệu thật
- **Hero hoạt hình SVG**: tàu bập bềnh trên sóng, cẩu STS có xe con chạy + nâng hạ container, mây trôi, chim bay, radar quét, mặt trời tỏa sáng
- **Reveal khi cuộn trang** (IntersectionObserver), đếm số động, marquee đối tác hãng tàu
- **Gallery 4 hoạt cảnh động**: cẩu STS làm hàng, tàu vào luồng (hải đăng nhấp nháy), bãi container + cẩu RTG di chuyển, xe đầu kéo chạy vào cổng
- **Sơ đồ cầu bến LIVE** (Admin/Bến cảng): tàu SVG bập bềnh trên bến đang chiếm, trạng thái trống/bảo trì
- **Sơ đồ bãi container** dạng lưới màu theo chiều cao chồng cont, tooltip danh sách cont, thanh % lấp đầy động
- Khu **Cộng đồng Logistics**: liên kết Group Facebook & kênh YouTube Vietnam Logistics Review (cả footer)

## 5. Cấu hình AI Gemini (tùy chọn)
1. Lấy API key miễn phí tại https://aistudio.google.com
2. Mở `SmartPort\Web.config`, điền vào:
```xml
<add key="GeminiApiKey" value="AIza..." />
```
3. Chạy lại web — chatbot sẽ gắn nhãn "✦ Trả lời bởi Gemini AI".

## 6. Cấu trúc thư mục
```
SmartPort/
├── SmartPort.sln
├── README.md
├── Database/SmartPort_DB.sql        ← script tạo CSDL + seed
└── SmartPort/                       ← project MVC
    ├── App_Start/RouteConfig.cs
    ├── AppServices/                 ← 3 module AI
    │   ├── ChatbotService.cs
    │   ├── ForecastService.cs
    │   └── BerthService.cs
    ├── Areas/Admin/                 ← khu quản trị (14 controllers)
    ├── Controllers/                 ← 8 controllers công khai
    ├── Helpers/                     ← phân quyền, hash, định dạng
    ├── Models/                      ← 13 entities + AppDb + ViewModels
    ├── Views/                       ← giao diện công khai
    ├── Content/site.css, admin.css
    └── Scripts/chatbot.js, site.js
```

## 7. CSDL — 13 bảng
`TaiKhoan, KhachHang, HangTau, Tau, Ben, ChuyenTau, Container, DichVu, DatDichVu, DatDichVuChiTiet, TinTuc, LienHe, SanLuongThang`
