# 🚢 SmartPort — Hệ thống quản lý và điều hành cảng container thông minh

**Đề tài:** Phân tích và thiết kế hướng đối tượng Hệ thống Quản lý và Điều hành Cảng biển Thông minh (SmartPort)
**Học phần:** KTPM013 — Thực hành Phân tích, thiết kế hướng đối tượng · GVHD: ThS. Trần Hữu Duật
**Sinh viên:** Lê Hữu Anh Tài — MSSV 2424802010066

| | |
|---|---|
| Mã nguồn | https://github.com/2424802010066-png/LeHuuAnhTai.SmartPort |
| Sản phẩm chạy thật | https://diane-dating-alexander-originally.trycloudflare.com |

> Bản chạy thật được đưa ra Internet bằng **Cloudflare Tunnel** từ máy chủ của sinh viên (ASP.NET Core chế độ
> Production + SQL Server). Link tạm này đổi mỗi khi khởi động lại tunnel; link mới nhất luôn được cập nhật tại đây.

---

## 1. Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Back-end | ASP.NET Core Web API (.NET 10), C#, Entity Framework Core, JWT, Swagger, giới hạn tần suất gọi API |
| Front-end | React 19 + Vite, React Router, Bootstrap 5, Chart.js |
| CSDL | SQL Server — `SmartPortDB`: 40 bảng nghiệp vụ + 2 bảng sự kiện (`SuKienOutbox`, `SuKienDaXuLy`) |
| Kiến trúc | Modular Monolith hướng sự kiện (Outbox + Server-Sent Events cho thông báo tức thời). Cùng mã nguồn chạy được kiểu **gộp 1 tiến trình** (bản đưa lên hosting) hoặc **13 microservice** sau API Gateway + RabbitMQ |
| AI | Chạy ngay trên trình duyệt bằng ONNX Runtime Web / TensorFlow.js: YOLO26 nhận diện xe – container – người, ước lượng tư thế, CCT-S v2, Tesseract đọc số container; chatbot PortBot tra dữ liệu thật |

Tài liệu kiến trúc chi tiết: [KIEN-TRUC-FE-BE.md](KIEN-TRUC-FE-BE.md) và [Web/KIEN-TRUC-MICROSERVICES.md](Web/KIEN-TRUC-MICROSERVICES.md).

## 2. Cấu trúc thư mục

```
SmartPort/
├── Web/                              ← toàn bộ mã nguồn để chạy website
│   ├── SmartPort.Api/                ← Web API + phục vụ giao diện React đã build (1 ứng dụng khi deploy)
│   ├── SmartPort.Core/               ← EF Core (AppDb, các lớp thực thể), nghiệp vụ dùng chung
│   ├── SmartPort.Modules.*/          ← module Cổng, Bãi, Tài chính, Thông báo, Cảng số
│   ├── SmartPort.NenTang/            ← khởi động web dùng chung: CSDL, JWT, CORS, Swagger…
│   ├── SmartPort.Contracts/          ← các lớp sự kiện dùng chung giữa module
│   ├── SmartPort.BuildingBlocks/     ← Outbox, bộ phát sự kiện, chống xử lý trùng
│   ├── DichVu/                       ← controller xếp theo 13 dịch vụ nghiệp vụ
│   ├── SmartPort.Gateway/            ← API Gateway (chế độ microservices)
│   ├── smartport-web/                ← giao diện React + Vite
│   ├── SmartPort.AI/                 ← dịch vụ AI phụ (Python)
│   ├── Database/                     ← script SQL
│   └── tests/                        ← kiểm thử kiến trúc + tích hợp
└── Khac/                             ← hồ sơ, báo cáo, ảnh, app di động (không cần để chạy web)
```

## 3. Chạy trên máy

Cần: .NET SDK 10, Node.js 20 trở lên, SQL Server có sẵn CSDL `SmartPortDB`.

1. **CSDL:** chuỗi kết nối khi chạy trên máy nằm ở `Web/SmartPort.Api/appsettings.Development.json`
   (mặc định `.\MSSQLSERVER01`, đăng nhập Windows). Bản sao lưu CSDL đầy đủ không đưa lên repo công khai
   vì có tài khoản người dùng và dữ liệu khuôn mặt; các script tạo bảng nằm ở `Web/Database/`:
   - CSDL mới: chạy `SmartPort_DB.sql` trước (**script này XÓA rồi tạo lại `SmartPortDB`**, chỉ dùng khi cài lần đầu).
   - Sau đó chạy **tất cả** các tệp `Add_*.sql` theo thứ tự tên (đủ 61 bảng). Mỗi tệp tự kiểm tra bảng đã có hay chưa,
     chạy lại nhiều lần không mất dữ liệu; thiếu tệp nào thì trang dùng bảng đó báo lỗi 500.
     ```powershell
     Get-ChildItem Web/Database/Add_*.sql | Sort-Object Name | ForEach-Object { sqlcmd -S .\MSSQLSERVER01 -E -d SmartPortDB -i $_.FullName }
     ```
   - `Seed_*.sql` là dữ liệu mẫu, tùy chọn (ví dụ `Seed_SuCo_Mau.sql` cho trang quản lý sự cố).
2. **Khóa ký JWT** (lần đầu, không ghi vào file cấu hình):
   ```bash
   dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên từ 32 ký tự trở lên>" --project Web/SmartPort.Api
   ```
3. **Build giao diện** (ghi thẳng vào `Web/SmartPort.Api/wwwroot`):
   ```bash
   npm install --prefix Web/smartport-web
   npm run build --prefix Web/smartport-web
   ```
4. **Chạy:**
   ```bash
   dotnet run --project Web/SmartPort.Api
   ```
   Mở http://localhost:5200 (website), http://localhost:5200/admin (khu điều hành), http://localhost:5200/swagger (tài liệu API).
   Trên Windows có thể bấm đúp `Web/chay_web.bat`.

## 4. Chức năng chính

- **Website công khai:** trang chủ số liệu thời gian thực, tra cứu container (thử `MSCU2201417`), lịch tàu, dịch vụ – bảng giá, ước tính chi phí, tin tức, liên hệ, chatbot PortBot.
- **Khu khách hàng:** đăng ký lệnh giao nhận, theo dõi hàng của tôi, thanh toán bằng mã QR, hóa đơn.
- **Khu điều hành `/admin`:** bảng điều khiển, điều độ chuyến tàu – cầu bến, sơ đồ bãi container, cổng tự động (camera AI đọc biển số, số container), kiểm đếm, an ninh, tài chính – phí cổng – phí lưu bãi, báo cáo, quản lý tài khoản và phân quyền.
- **Xử lý sự cố:** mọi người dùng báo sự cố, phản ánh; nhân viên tiếp nhận, tạm giữ container (cổng chặn không cho ra), ghi kết quả, đóng, thống kê; sự cố nguy hiểm phát báo động toàn cảng; hoàn tiền giao dịch lỗi.
- **Thông báo tức thời** qua Server-Sent Events: xe qua cổng → bãi cập nhật container → tài chính tính phí, cùng được ghi trong một giao dịch nhờ Outbox.

Tài khoản dùng thử trên bản chạy thật được gửi kèm bài nộp, không ghi công khai ở đây.

README của bản ASP.NET MVC 5 trước đây được lưu tại [Khac/HoSo/TaiLieu/README-ban-MVC5-cu.md](Khac/HoSo/TaiLieu/README-ban-MVC5-cu.md).
