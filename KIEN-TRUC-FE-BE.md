# SmartPort — Modular Monolith hướng sự kiện, tách Front-end / Back-end

> Cập nhật 19/09/2026. Kiến trúc: **Event-Driven Architecture + Modular Monolith** (phương án cho đội ngũ nhỏ),
> giao diện React tách hẳn khỏi Web API. **Đã bỏ mô hình MVC**: toàn bộ màn hình chuyển sang React + Web API;
> bản MVC cũ dời vào `Khac/_Cu_KhongDung/SmartPort.Net10` (chỉ để tham khảo, không chạy, không còn trong solution).
> Khi deploy chỉ còn **một ứng dụng**: SmartPort.Api phục vụ cả API lẫn giao diện đã build.
> **Bố cục thư mục gốc:** `Web/` — toàn bộ mã nguồn + script cần để chạy website (9 project .NET, `smartport-web`,
> `tests`, `Database`, `chay_web.bat`). `Khac/` — mọi thứ không liên quan tới việc chạy web (hồ sơ/báo cáo,
> app mobile, bản lưu cũ). Xem mục 3.

## 1. Sơ đồ tổng thể

```
 Trình duyệt ──────────────────────────────────────────────────────────────────────┐
 │ smartport-web (React SPA — 1 bản build, 121 địa chỉ màn hình)                    │
 │  Khu điều hành /admin · Website công khai / · Khu khách hàng · Trang in          │
 │  AI chạy ngay trên trình duyệt: YOLO26 · Pose · CCT-S v2 · Tesseract · TF.js     │
 └──────┬──────────────────────────────────▲──────────────────────────────────────────┘
        │ REST/JSON + JWT                  │ Server-Sent Events (realtime)
        ▼                                  │
 ┌──────────────────────────────────────────────────────────┐
 │ SmartPort.Api (Web API + phục vụ bản build React)        │
 │  69 controller xếp theo 17 khu nghiệp vụ · 277 endpoint  │
 │  JWT · CORS · Swagger · giới hạn tần suất                │
 │  Bộ phát sự kiện (nền) · Đuôi Outbox → SSE · Agent nền   │
 └──────┬───────────────────────────────────────────────────┘
        │ gọi module
        ▼
 ┌───────────────────────────────────────────────────────────────────────────────────────┐
 │ MODULAR MONOLITH — mỗi module là 1 project, KHÔNG tham chiếu nhau                      │
 │  Modules.Cong ──phát──► cong.xe-da-qua-cong                                           │
 │  Modules.Bai  ◄─nghe── cong.*          ──phát──► bai.container-da-vao-bai / da-roi-bai │
 │  Modules.TaiChinh ◄─nghe── cong.*, bai.container-da-roi-bai                            │
 │                                         ──phát──► tai-chinh.phi-cong-da-xu-ly /        │
 │                                                    tai-chinh.phi-luu-bai-da-chot       │
 │  Modules.ThongBao ◄─đọc── mọi sự kiện (Outbox) → dịch câu tiếng Việt → realtime       │
 │  Contracts (sự kiện) · BuildingBlocks (Outbox, bộ phát) · Core (EF Core, nghiệp vụ cũ) │
 └──────┬────────────────────────────────────────────────────────────────────────────────┘
        ▼ EF Core — dữ liệu nghiệp vụ và sự kiện ghi CÙNG một giao dịch
   SQL Server SmartPortDB: 40 bảng nghiệp vụ + SuKienOutbox (hộp thư đi) + SuKienDaXuLy (đã xử lý)
```

## 2. Các project

| Project | Vai trò | Sở hữu / quy tắc |
|---|---|---|
| `SmartPort.BuildingBlocks` | Hạ tầng sự kiện: `SuKienTichHop`, Outbox, bộ phát, chống xử lý trùng | Không chứa nghiệp vụ |
| `SmartPort.Contracts` | "Ngôn ngữ chung": chỉ các lớp sự kiện | Chỉ phụ thuộc BuildingBlocks |
| `SmartPort.Modules.Cong` | Lượt xe qua cổng, lệnh giao nhận tại cổng, dock, ảnh xe | Phát `cong.*` |
| `SmartPort.Modules.Bai` | Trạng thái container vào/rời bãi | Nghe `cong.*`, phát `bai.*` |
| `SmartPort.Modules.TaiChinh` | Phí cổng, phiếu thu phí lưu bãi, sổ chứng từ | Nghe `cong.*`, `bai.*`, phát `tai-chinh.*` |
| `SmartPort.Modules.ThongBao` | Đọc Outbox, dịch sự kiện, đẩy realtime | Không sở hữu dữ liệu nghiệp vụ |
| `SmartPort.Core` | Nhân dùng chung: EF Core (40 bảng), nghiệp vụ chưa tách module | Tách dần theo cùng cách (strangler) |
| `SmartPort.Api` | Web API cho React, đồng thời phục vụ bản build React (1 ứng dụng khi deploy) | Controller mỏng, xếp theo khu: `Controllers/<Khu>/` |
| `smartport-web` | Giao diện React | Chỉ nói chuyện với API; mỗi khu một thư mục `src/khu/<khu>/` tự đăng ký màn hình |
| `tests/SmartPort.KiemThu` | Kiểm thử kiến trúc + tích hợp | Chạy được khi không có mạng |

## 3. Chạy trên máy

> Kể từ 19/09/2026: toàn bộ mã nguồn chạy web nằm trong **`Web/`** (mọi lệnh dưới đây chạy từ gốc repo,
> trỏ vào `Web/...`). Tài liệu/tài sản không liên quan tới chạy web đã dời sang **`Khac/`**
> (`Khac/HoSo`, `Khac/SmartPort.Mobile`, `Khac/_Cu_KhongDung`, `Khac/_SaoLuu`, `Khac/NopThi_...`).
> Cách nhanh nhất: chạy `Web\chay_web.bat`.

```bash
# 0) Lần đầu (và trên somee TRƯỚC khi deploy): tạo bảng sự kiện — script chỉ thêm, chạy lại an toàn
sqlcmd -S .\MSSQLSERVER01 -E -I -d SmartPortDB -i Web/Database/Add_SuKienOutbox.sql

# 1) Back-end (lần đầu: đặt khóa ký JWT, KHÔNG ghi vào appsettings.json)
dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên từ 32 ký tự trở lên>" --project Web/SmartPort.Api
dotnet run --project Web/SmartPort.Api          # http://localhost:5200/swagger

# 2) Front-end (khi lập trình)
npm install --prefix Web/smartport-web          # lần đầu
npm run dev --prefix Web/smartport-web          # http://localhost:5173 — Vite chuyển tiếp /api, ảnh, mô hình AI sang cổng 5200

# 2b) Bản chạy thật: build React ghi thẳng vào Web/SmartPort.Api/wwwroot -> mở http://localhost:5200 là ra giao diện
npm run build --prefix Web/smartport-web

# 3) Kiểm thử
dotnet run --project Web/tests/SmartPort.KiemThu -- kien-truc
#    tích hợp: CHỈ trên bản sao CSDL có tên kết thúc _KiemThu (chương trình từ chối chạy trên CSDL khác)
#    (PowerShell)
$env:SMARTPORT_KIEMTHU_DB = "Data Source=.\MSSQLSERVER01;Initial Catalog=SmartPortDB_KiemThu;Integrated Security=True;TrustServerCertificate=True"
dotnet run --project Web/tests/SmartPort.KiemThu -- tich-hop
```

- Chạy nhiều bản API song song vẫn an toàn: bộ phát sự kiện không lấy trùng. Tắt bộ phát: `"SuKien": { "ChayBoPhat": false }`;
  tắt agent chạy nền: `"Agent": { "ChayNen": false }`.
- Website công khai (không cần đăng nhập): `http://localhost:5200/` — tra cứu container `/tracking`, lịch tàu `/schedule`,
  báo giá `/cost`, cổng tài xế `/taixe`. Khu điều hành: `/admin`. Tài liệu API: `/swagger`.

## 4. API và màn hình theo khu nghiệp vụ

Toàn bộ **277 endpoint** (69 controller) có tài liệu tự sinh tại `/swagger`. Controller xếp theo khu, mỗi khu
tương ứng một thư mục màn hình React — nhờ vậy nhiều người sửa song song mà không giẫm chân nhau:

| Khu | API `Controllers/…` | Màn hình `src/khu/…` | Nội dung chính |
|---|---|---|---|
| Cổng & lệnh giao nhận | `CongVaLenh` | `cong` | Cổng ALPR, hồ sơ xe 360°, nhật ký cổng, trạm cân VGM, lệnh giao nhận (in QR), kho chứng từ |
| Tài xế & đối soát | `TaiXeDoiSoat` | `tai-xe` | Hồ sơ tài xế, quét CCCD/GPLX, đối soát số xe qua cổng |
| Container & bãi | `BaiContainer` | `container` | Danh sách container, hồ sơ 360°, EIR, sơ đồ bãi, container lạnh |
| EDI & depot | `EdiDepot` | `edi-depot` | CODECO/COARRI/COPARN, tồn rỗng theo hãng, phiếu sửa chữa vỏ EOR |
| Tàu & cầu bến | `TauBen` | `tau-ben` | Chuyến tàu, tàu biển, hãng tàu, bến, Gantt cầu bến, AI phân bổ bến |
| Thu tiền | `ThuTien` | `tai-chinh` | Phí lưu bãi & công nợ, thu phí cổng, thẻ xe, đối soát thanh toán, báo cáo |
| Kinh doanh | `KinhDoanh` | `kinh-doanh` | Đơn đặt dịch vụ, khách hàng, bảng giá, tin tức, thư viện ảnh, hộp thư liên hệ |
| AI thị giác | `ThiGiacAi` | `ai-thi-giac` | Camera AI (YOLO), SafePose, camera IP, Q-learning trong AI Lab |
| An ninh | `AnNinh` | `an-ninh` | Phòng an ninh, trạm camera AI, kho media AI |
| Kiểm đếm & giám sát tài xế | `KiemDemDms` | `kiem-dem` | Kiểm đếm AI, phát hiện buồn ngủ / mất tập trung |
| Điều hành thông minh | `DieuHanh` | `dieu-hanh` | Agent tự động, AI Agent, học máy, quy trình, cảng xanh, giá trị AI |
| Hệ thống | `HeThong` | `he-thong` | Sức khỏe hệ thống, nhật ký, tài khoản, khóa API, trợ lý nghiệp vụ |
| Website công khai | `CongKhai` | `cong-khai` | Trang chủ, giới thiệu, liên hệ, PortBot |
| Tiện ích công khai | `TienIchCongKhai` | `tien-ich` | Tin tức, dịch vụ, lịch tàu, báo giá, tra cứu container, tình trạng cổng, cổng tài xế, PWA |
| Tài khoản | `TaiKhoanKhach` | `khach-hang` | Đăng nhập (mật khẩu + khuôn mặt), đăng ký, hồ sơ, đổi mật khẩu |
| Dịch vụ khách hàng | `DichVuKhach` | `dich-vu-khach` | Đặt dịch vụ, thanh toán VietQR, hàng của tôi |
| Tích hợp đối tác | `TichHopDoiTac` | *(không có giao diện)* | `api/v1` cho phần mềm đối tác (khóa API), `api/mobile` cho app di động |

Dùng chung (ngoài khu): `auth`, `tong-quan`, `cong`, `container`, `su-kien` (SSE), `tro-ly`, `ai-lab`, `he-thong`.

| Phương thức | Đường dẫn tiêu biểu | Quyền | Việc |
|---|---|---|---|
| POST | `/api/auth/dang-nhap` | công khai, 10 lần/phút/IP | Đăng nhập → token JWT (8 giờ) |
| POST | `/api/cong/luot-xe` | Admin, NhanVien | Ghi lượt xe → **201 ngay**; bãi và tài chính xử lý **bất đồng bộ** qua sự kiện |
| GET | `/api/su-kien` | Admin, NhanVien | Luồng realtime (text/event-stream), hỗ trợ `Last-Event-ID` |
| GET | `/api/container/{soCont}` | công khai | Tra cứu + phí lưu bãi; nhân viên có thêm dòng thời gian |
| GET | `/api/v1/ping` | khóa API | Kiểm tra kết nối cho phần mềm đối tác |

Kết quả `POST /api/cong/luot-xe`: `maXV`, `dock`, `soCont`, `maSuKien`, `choBai` (Bãi sẽ phản hồi),
`choPhiCong` (Tài chính sẽ tính phí). Các sự kiện tiếp theo mang cùng `maXV`.

## 5. Luồng xe ra nhận container (ví dụ đã chạy thật trên giao diện)

```
POST /api/cong/luot-xe  (51C-638.72, Ra, lệnh LGN-DEMO-753)
 └─ [Cổng]  1 giao dịch: ghi XeRaVao + lệnh "Hoàn tất" + dòng Outbox cong.xe-da-qua-cong → 201 Created (trả về ngay)
      │ bộ phát nhận sự kiện (UPDLOCK + READPAST), mỗi bộ xử lý một giao dịch riêng
      ├─ [Bãi]       container → "Đã giao khách" + Outbox bai.container-da-roi-bai
      │                 └─ [Tài chính] lập phiếu PLB-2609-0097 · 3.420.000đ + sổ chứng từ + Outbox tai-chinh.phi-luu-bai-da-chot
      └─ [Tài chính] thu phí cổng 50.000đ tiền mặt + Outbox tai-chinh.phi-cong-da-xu-ly
 Màn hình Cổng: "Tiến trình xử lý qua sự kiện" tick từng bước, đủ 4 bước sau khoảng 2 giây;
 Tổng quan ở tab/máy khác nhận đủ 4 dòng — không F5.
```

## 6. Các cơ chế của hướng sự kiện

| Vấn đề | Cách xử lý trong SmartPort |
|---|---|
| Lưu dữ liệu xong nhưng mất sự kiện (hoặc ngược lại) khi máy chủ tắt giữa chừng | **Transactional Outbox**: sự kiện là một dòng trong `SuKienOutbox`, ghi cùng giao dịch với dữ liệu |
| Sự kiện bị giao lại (mạng chập chờn, tiến trình chết sau khi xử lý) | **Inbox** `SuKienDaXuLy` khóa chính (MaSuKien, BoXuLy) ghi cùng giao dịch với tác động → xử lý **đúng một lần**; thêm lớp chặn thứ 2 trong bộ xử lý phí |
| Nhiều tiến trình cùng phát (nhiều bản API, agent nền) | `UPDLOCK, READPAST` + "giữ chỗ" 60 giây: không lấy trùng; tiến trình chết thì hết hạn giữ chỗ, bên kia nhận lại |
| Bộ xử lý lỗi | Ghi lỗi, thử lại giãn dần 2, 4, 8… giây; quá 5 lần → trạng thái **Lỗi** (không kẹt vòng lặp), hiện trên `/api/su-kien/trang-thai` |
| Sự kiện do tiến trình khác ghi (agent nền, bản API thứ hai) | Module Thông báo **đọc Outbox** 0,7 giây/lần (có nhìn lùi 50 Id vì Id có thể commit không theo thứ tự) |
| Trình duyệt chậm / mất kết nối | Mỗi kết nối 1 hàng đợi giới hạn 200; nối lại gửi `Last-Event-ID` để nhận bù tối đa 100 sự kiện |
| Chậm bao lâu thì dữ liệu nhất quán? | **Nhất quán sau** (eventual consistency) ≈ 0,5–1 giây; màn hình cổng hiện từng bước, quá 10 giây báo chậm |

Kèm theo, sửa 2 lỗi tranh chấp có thật trong nghiệp vụ cũ (bắt buộc khi nhiều tiến trình cùng xử lý):
- **Phiếu thu lưu bãi trùng** (agent chạy nền và cổng chốt cùng lúc → PLB-2609-0009/0010): khóa ứng dụng SQL
  `sp_getapplock` biến "kiểm tra đã có phiếu + cấp số + thêm" thành một bước.
- **Chuỗi băm chứng từ rẽ nhánh** khi hai nơi ghi cùng lúc: khóa `SmartPort.ChungTu` khi lấy hash chứng từ trước.

## 7. Vì sao Modular Monolith, chưa tách Microservices

| Tiêu chí | Microservices | Modular Monolith + sự kiện (đang dùng) |
|---|---|---|
| Nhân lực | Nhiều nhóm, mỗi nhóm một service | Phù hợp nhóm nhỏ / 1 người |
| Dữ liệu liên kết chặt (container ↔ chuyến ↔ lượt xe ↔ phí ↔ chứng từ băm chuỗi) | Mỗi service một CSDL → mất phép nối bảng, cần saga | Một CSDL; module sở hữu bảng của mình, nói chuyện qua sự kiện |
| Hạ tầng | Message broker, gateway, giám sát phân tán | 1 web app + 1 SQL Server (chạy được trên somee) |
| Ranh giới module | Mạng tách hẳn | Project riêng + **kiểm thử kiến trúc** chặn module tham chiếu nhau |
| Đường nâng cấp | — | Bộ phát chuyển sự kiện Outbox sang RabbitMQ/Kafka thay vì gọi module trong tiến trình; module nào tải cao (Cổng) tách thành service trước |

## 8. Bỏ MVC: chuyển toàn bộ màn hình sang React + Web API (18/09/2026)

Thầy yêu cầu không dùng mô hình MVC nữa. Toàn bộ 51 controller MVC (35 khu quản trị + 16 trang công khai,
~14.000 dòng Razor và 17 file JavaScript) đã được viết lại thành Web API + React:

| Việc | Cách làm |
|---|---|
| Chia việc | Cắt hệ thống thành **17 khu nghiệp vụ**, mỗi khu sở hữu một thư mục API và một thư mục màn hình → làm song song, không đụng file của nhau |
| Giữ đường dẫn cũ | Địa chỉ React **giữ nguyên địa chỉ của bản MVC**, chỉ viết thường (`/Admin/Containers/HoSo/5` → `/admin/containers/hoso/5`, `/Tracking` → `/tracking`). React Router không phân biệt hoa thường nên link do nghiệp vụ sinh ra, link đã lưu trong CSDL và mã QR đã in vẫn mở đúng màn hình |
| Menu | Khai báo một chỗ (`src/dieuHuong/menu.js`), chia 8 nhóm, có ô lọc; màn hình nào đăng ký xong thì tự hiện |
| Dùng chung | Bộ giao diện `src/components/ui` (bảng, phân trang, hộp thoại, xác nhận, thông báo nhanh, nhãn trạng thái) + hook `useApi`, `useBoLoc`, `useThaoTac`, `useBieuMau` |
| Tài nguyên | Ảnh tải lên, mô hình AI, `App_Data` chuyển sang `SmartPort.Api`; cấu hình gộp vào `SmartPort.Api/appsettings*.json`; agent chạy nền chuyển vào API |
| Kiểm tra từng khu | Mỗi khu build riêng (bản sao project), chạy thử API thật trên `SmartPortDB_KiemThu` bằng token tự ký, và build riêng phần giao diện của khu |

Quy mô sau khi chuyển: **69 controller / 277 endpoint**, **112 trang React / 121 địa chỉ màn hình**,
~19.800 dòng C# ở API và ~42.600 dòng JS/JSX/CSS ở giao diện. Không có endpoint hay địa chỉ màn hình nào trùng nhau.

Ba lỗi đáng chú ý phát hiện khi chuyển (đã sửa): hai controller khai báo `IFormFile` trực tiếp làm Swagger không
sinh được tài liệu → **API không khởi động**; bản MVC cho bấm "gate out" nhiều lần làm sai ngày ra bãi; nút
"Đã sửa" của phiếu EOR trong bản MVC không bao giờ chạy được vì điều kiện ngược nhau giữa view và controller.

## 9. Kiểm thử đã chạy

| Nhóm | Kết quả |
|---|---|
| Kiến trúc: module không tham chiếu nhau (csproj + assembly), Contracts/BuildingBlocks đúng phụ thuộc, bộ xử lý là internal, tên sự kiện đúng module, **không còn view Razor trong solution**, controller chỉ trả dữ liệu, **chỉ module Cổng được tạo lượt xe** | 32/32 đạt |
| Tích hợp trên bản sao `SmartPortDB_KiemThu`: Outbox cùng giao dịch (hủy thì mất cả hai), 5 luồng cổng (vào/ra, có/không lệnh, lệnh ngược hướng, nhập bù), giao lại không xử lý trùng, **2 bộ phát song song** 15 lượt xe không thu trùng, **8 luồng chốt phiếu cùng lúc ra đúng 1 phiếu**, 12 lần ghi chứng từ đồng thời không rẽ nhánh, thử lại → lỗi vĩnh viễn sau 3 lần, dịch bản tin, chuỗi chứng từ nguyên vẹn | 38/38 đạt |
| Kiểm chứng bài kiểm thử có tác dụng: tạm gỡ khóa phiếu lưu bãi → bài chốt đồng thời **báo lỗi ngay** (trùng số phiếu) → trả khóa lại | đạt |
| Giao diện (API nối bản sao): đọc biển vàng 51C-638.72 → khớp lệnh → Lưu → tiến trình 4 bước xong sau ≈2 giây, tab Tổng quan nhận đủ 4 sự kiện | đạt |
| Hồi quy trên CSDL thật sau khi chạy script Outbox: API chỉ đọc 22/22, luồng sự kiện 7/7 | đạt |
| Build toàn bộ: BuildingBlocks, Contracts, Core, 4 module, Api, tests, `npm run build` | 0 lỗi, 0 cảnh báo |
| Quét toàn bộ API sau khi ráp 17 khu: gọi thử mọi endpoint GET bằng token quản trị | 117 endpoint trả 200, **0 lỗi máy chủ**; các mã 4xx còn lại đều đúng ý đồ (thiếu tham số, sai vai trò, id không tồn tại) |
| Mở trình duyệt kiểm tra bản gộp (API phục vụ luôn giao diện): trang chủ công khai, tổng quan, container, phí lưu bãi, phòng an ninh, agent, Gantt cầu bến, EDI, hồ sơ xe 360°, tra cứu container | Hiển thị đúng dữ liệu thật, **không có lỗi JavaScript nào trong console** |

## 10. Câu hỏi vấn đáp thường gặp

**Kiến trúc hệ thống là gì?** Modular Monolith hướng sự kiện: một ứng dụng triển khai, bên trong chia module
(Cổng, Bãi, Tài chính, Thông báo) không gọi thẳng nhau mà phát/nghe sự kiện qua Outbox; giao diện React tách
khỏi Web API.

**Sự kiện nằm ở đâu trong code?** Định nghĩa: `SmartPort.Contracts`. Phát: `hopThuDi.Them(...)` trong
`Modules.Cong/NoiBo/DichVuCong.cs`. Nghe: các lớp `IXuLySuKien<T>` trong `Modules.Bai/NoiBo`,
`Modules.TaiChinh/NoiBo`. Phát đi: `BuildingBlocks/Outbox/BoPhatSuKien.cs`. Tới giao diện:
`Modules.ThongBao` → `Api/Controllers/SuKienController.cs` → `smartport-web/src/realtime/SuKienContext.jsx`.

**Outbox là gì, sao không gửi sự kiện ngay sau SaveChanges?** Gửi ngay thì nếu máy chủ tắt đúng giữa "đã lưu"
và "đã gửi", sự kiện mất vĩnh viễn: container không bao giờ được cập nhật, khách không bị tính phí. Outbox ghi sự
kiện vào CSDL **trong cùng giao dịch** nên không thể mất; bộ phát gửi sau, gửi lại được.

**Giao lại nhiều lần có thu phí khách nhiều lần không?** Không. Mỗi (sự kiện, bộ xử lý) chỉ được ghi một dòng
`SuKienDaXuLy` — ghi cùng giao dịch với tiền. Lần giao lại bị khóa chính chặn. Đã kiểm thử bằng cách đánh dấu lại
toàn bộ sự kiện của một lượt xe là "chưa xử lý".

**Bất đồng bộ thì màn hình cổng biết kết quả thế nào?** API trả ngay `maXV`, `choBai`, `choPhiCong`; các sự kiện
sau mang cùng `maXV` về qua SSE, màn hình tick từng bước. Quá 10 giây chưa có thì báo chậm.

**Bỏ MVC thì khác gì trước?** Trước đây máy chủ dựng sẵn HTML (Razor) rồi gửi về; mỗi lần bấm là tải lại cả
trang, và giao diện dính chặt vào máy chủ nên app di động hay phần mềm đối tác phải làm đường riêng. Nay máy chủ
chỉ trả **dữ liệu JSON**, giao diện React tự vẽ và chỉ tải phần thay đổi; cùng một API đó phục vụ luôn app di động
(`api/mobile`) và đối tác (`api/v1`). Khi deploy vẫn chỉ một ứng dụng vì API phục vụ luôn bản build React.

**Vì sao giữ nguyên địa chỉ cũ của bản MVC?** Vì địa chỉ đã nằm trong dữ liệu: liên kết lưu trong sổ chứng từ, câu
trả lời của trợ lý, cảnh báo của agent, và mã QR đã in trên lệnh giao nhận. Giữ nguyên đường dẫn (chỉ viết thường)
thì những thứ đó vẫn mở đúng màn hình, không phải đi sửa dữ liệu cũ.

**Vì sao không Microservices?** Xem mục 7: nhóm 1 người, dữ liệu liên kết chặt, hạ tầng somee. Nhưng ranh giới
module đã được **kiểm thử tự động bảo vệ**, nên tách service sau này chỉ là đổi đường truyền sự kiện.

**Module có thật sự độc lập không hay chỉ là thư mục?** Mỗi module là project riêng, phần cài đặt `internal`;
bài kiểm thử kiến trúc đọc `.csproj` và assembly đã build — module nào tham chiếu module khác là kiểm thử báo lỗi.

## 11. Giới hạn hiện tại

- Mới tách luồng Cổng → Bãi → Tài chính thành module; các nghiệp vụ khác vẫn nằm trong `SmartPort.Core` (tách dần).
- Module vẫn dùng chung `AppDb` (một DbContext) — ranh giới dữ liệu theo quy ước "module nào sở hữu bảng nào",
  chưa tách schema.
- Chưa có refresh token; HTTPS khi chạy thử trên máy chưa bật.
- Phần AI chạy trong trình duyệt (camera, kiểm đếm, giám sát tài xế, quét giấy tờ) đã chuyển đủ thuật toán và
  tham số, nhưng chỉ thử được bằng ảnh/video mẫu — muốn chắc phải cắm camera thật chạy thử.
- Một số ảnh minh họa của website công khai vẫn trỏ ra Unsplash như bản cũ; muốn chạy offline hoàn toàn thì đổi
  khóa ảnh trong bảng `CauHinhAnh` sang `~/Content/img/...`.

## 12. Đưa lên hosting (somee)

```bash
# 1) TRƯỚC KHI DEPLOY: chạy script tạo bảng sự kiện trên CSDL somee (chỉ thêm bảng, chạy lại an toàn)
#    Web/Database/Add_SuKienOutbox.sql   ← CHƯA chạy trên somee

# 2) Build giao diện (ghi vào Web/SmartPort.Api/wwwroot) rồi publish đúng 1 project
npm run build --prefix Web/smartport-web
dotnet publish Web/SmartPort.Api -c Release -o <thư mục publish>
```

- Chuỗi kết nối thật của somee nằm ở bản sao `Khac/_Cu_KhongDung/SmartPort.Net10/appsettings.json.bak`
  (bản trong repo đã che mật khẩu) — điền vào `Web/SmartPort.Api/appsettings.json` trên máy trước khi publish,
  không đẩy mật khẩu lên GitHub.
- Đặt khóa ký JWT trên hosting bằng biến môi trường `Jwt__Key` (32 ký tự trở lên), không ghi vào appsettings.
- `wwwroot` chứa cả ảnh tải lên và mô hình AI (~216 MB): lần đầu đẩy lên hosting sẽ lâu; các lần sau chỉ cần
  đẩy `index.html` + thư mục `assets` nếu chỉ sửa giao diện.
