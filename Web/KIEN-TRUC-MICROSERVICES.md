# SmartPort — Kiến trúc Microservices hướng sự kiện (tài liệu ôn vấn đáp)

> Bản tóm tắt để trình bày / trả lời vấn đáp. Mọi con số ở đây là **đo thật trên máy phát triển** (ngày 07/10/2026),
> dữ liệu nghiệp vụ là **dữ liệu demo** (seed + mô phỏng), không phải dữ liệu vận hành của một cảng thật.

## 1. Trả lời nhanh: hệ thống đã là microservices chưa?

| | Trước | Bây giờ |
|---|---|---|
| Kiểu kiến trúc | *Modular monolith*: 1 tiến trình ASP.NET Core, 1 CSDL `SmartPortDB` | **Microservices**: 13 dịch vụ .NET + 1 dịch vụ AI Python, mỗi dịch vụ 1 tiến trình, 1 cổng, **1 CSDL riêng** |
| Giao tiếp giữa module | Gọi hàm trực tiếp trong cùng tiến trình | **Bất đồng bộ qua sự kiện** (RabbitMQ) + HTTP qua **API Gateway** |
| Lối vào cho trình duyệt | `SmartPort.Api` | **API Gateway (YARP)** cổng 5100 — một địa chỉ duy nhất |
| Triển khai somee (free) | 1 tiến trình | **Vẫn chạy chế độ gộp** từ CÙNG mã nguồn (`SmartPort.Api` link controller của mọi dịch vụ) |

Lý do giữ chế độ gộp: hosting miễn phí chỉ cho 1 ứng dụng — cùng một mã nguồn chạy được cả 2 kiểu, bật/tắt bằng cấu hình
(`Bus:CheDo = NoiBo | RabbitMQ`, `SoHuuDuLieu.Bat`).

## 2. Sơ đồ tổng thể

```
 Trình duyệt / app di động
          │  HTTPS + JWT
          ▼
 ┌──────────────────────────┐        định tuyến theo dich-vu.json (/api/cong/* → cong, /api/edo/* → edo, ...)
 │  API Gateway (YARP) 5100 │──────────────────────────────────────────────────────────────┐
 └──────────────────────────┘                                                              │
     │        │        │        │        │        │        │        │        │             │
  dinh-danh kinh-doanh tau-ben  bai     cong   tai-chinh an-ninh tich-hop van-hanh ... edo hai-quan lich-hen
   5101     5102     5103    5104    5105    5106     5107    5108     5109     5112  5113    5111
     │        │        │        │        │        │        │        │        │             │
  SmartPortMS_DinhDanh … (mỗi dịch vụ 1 CSDL SQL Server riêng)                              │
     │        │        │        │        │        │        │        │        │             │
     └────────┴────────┴────────┴───┬────┴────────┴────────┴────────┴────────┴─────────────┘
                                    ▼
                RabbitMQ — exchange TOPIC "smartport.su-kien"
                (mỗi dịch vụ 1 hàng đợi bền smartport.<tên> + hàng đợi thử lại + hàng đợi lỗi smartport.loi)
                                    │
                         thong-bao 5110 ──SSE──► giao diện realtime

 cong 5105 ──HTTP nội bộ──► ai-thi-giac 8100 (Python, GPU): đọc biển số, đọc mã container
```

## 3. Danh mục dịch vụ (`Web/dich-vu.json`)

| Dịch vụ | Cổng | CSDL riêng | Bảng **sở hữu** (chỉ nó được ghi) | Việc |
|---|---|---|---|---|
| dinh-danh | 5101 | SmartPortMS_DinhDanh | TaiKhoan, ApiKey, KhuonMat, DatLaiMatKhau | Đăng nhập JWT, quên mật khẩu, khuôn mặt, quản trị |
| kinh-doanh | 5102 | SmartPortMS_KinhDoanh | KhachHang, DichVu, DatDichVu, ThanhToan, TinTuc… | Khách hàng, đặt dịch vụ, thanh toán, trang công khai |
| tau-ben | 5103 | SmartPortMS_TauBen | HangTau, Tau, ChuyenTau, Ben | Tàu, chuyến, bến, phân bến AI |
| bai | 5104 | SmartPortMS_Bai | Container, NhietDoLog, PhieuSuaChua | Container, sơ đồ bãi, container lạnh IoT, depot |
| cong | 5105 | SmartPortMS_Cong | XeRaVao, LenhGiaoNhan, ChungTu, TaiXe, PhieuCan… | Lượt xe qua cổng, lệnh giao nhận, trạm cân, **chống tráo container** |
| tai-chinh | 5106 | SmartPortMS_TaiChinh | BieuPhiCong, PhieuLuuBai, TheXe | Phí cổng, phí lưu bãi, báo cáo |
| an-ninh | 5107 | SmartPortMS_AnNinh | SuCoAnNinh, MediaAI, NhanDienAI, **DoiChieuContainer**… | Sự cố, camera, kho media AI |
| tich-hop | 5108 | SmartPortMS_TichHop | BanTinEDI | EDI CODECO/COARRI với hãng tàu |
| van-hanh | 5109 | SmartPortMS_VanHanh | CauHinhAgent, NhatKyAgent | AI vận hành, agent tự động, trợ lý |
| thong-bao | 5110 | SmartPortMS_ThongBao | — | Nghe MỌI sự kiện, đẩy realtime (SSE) |
| **lich-hen** | 5111 | SmartPortMS_LichHen | KhungGioCong, LichHenXe | **Đặt lịch hẹn giờ vào cổng (TAS)** |
| **edo** | 5112 | SmartPortMS_Edo | LenhGiaoHangDienTu | **Lệnh giao hàng điện tử (eDO)** |
| **hai-quan** | 5113 | SmartPortMS_HaiQuan | ToKhaiHaiQuan | **Theo dõi thông quan (mô phỏng VASSCM)** |
| ai-thi-giac | 8100 | — (không trạng thái) | — | Python + GPU: YOLO26, RF-DETR, CRNN |

## 4. Các mẫu thiết kế — và **tại sao**

### 4.1 Database-per-service + bảng sở hữu / bản sao
* Mỗi bảng có **đúng 1 dịch vụ chủ** (cột `soHuu`). Dịch vụ khác cần đọc thì giữ **bản sao chỉ-đọc** trong CSDL của mình.
* Bản sao được cập nhật bằng sự kiện `du-lieu.<bảng>` (*event-carried state transfer*): EF Core interceptor
  `BatThayDoiDuLieu` bắt mọi dòng thêm/sửa/xóa của bảng sở hữu và ghi sự kiện vào Outbox **trong cùng giao dịch**.
* Ghi nhầm vào bảng của dịch vụ khác bị **chặn ngay** (`ChanGhiBangKhac` — chặn cả EF lẫn SQL thô).
* **Tại sao không gọi API sang dịch vụ khác để đọc?** Mỗi lần đọc phải gọi mạng → chậm, và dịch vụ kia sập thì mình sập theo
  (*temporal coupling*). Bản sao cục bộ: đọc nhanh, dịch vụ kia tắt vẫn chạy; đổi lại chấp nhận **nhất quán cuối cùng**
  (trễ vài trăm ms).
* Cần **thêm dòng** vào bảng của dịch vụ khác (vd. Cổng cần lập phiếu lưu bãi của Tài chính) → gửi **lệnh** (sự kiện
  `tai-chinh.phieu-luu-bai-can-lap`, hoặc `yeu-cau-them.<bảng>`), dịch vụ chủ tự ghi. Không ai ghi hộ CSDL người khác.

### 4.2 Transactional Outbox
* Vấn đề *dual-write*: lưu CSDL xong rồi mới gửi RabbitMQ → mất điện giữa 2 bước thì dữ liệu có mà sự kiện mất (hoặc ngược lại).
* Giải: sự kiện được ghi vào bảng **Outbox trong cùng giao dịch** với dữ liệu nghiệp vụ → hoặc cả hai, hoặc không gì cả.
  Bộ chuyển tiếp (`BoChuyenTiepRabbit`) đọc Outbox, đẩy lên exchange với **publisher confirm**, xác nhận rồi mới đánh dấu đã gửi.
* Gửi **tuần tự theo thứ tự ghi** → bản sao không bao giờ áp bản cũ đè bản mới.

### 4.3 Inbox (chống xử lý trùng) + thử lại có độ trễ + hàng đợi lỗi
* RabbitMQ giao **ít nhất 1 lần** → có thể giao lại. Mỗi dịch vụ lưu mã sự kiện đã xử lý (bảng `SuKienDaXuLy`) → nhận lại thì bỏ qua
  (**idempotent**).
* Xử lý xong mới `ack`. Lỗi → chuyển sang hàng đợi `smartport.<tên>.thu-lai` (TTL 3 giây) rồi quay lại; quá **6 lần** → vào
  **dead-letter** `smartport.loi` để người xem xét, không chặn các tin khác.

### 4.4 Choreography (không có "nhạc trưởng")
Mỗi dịch vụ tự phản ứng với sự kiện nó quan tâm. Ví dụ **xe kéo container ra cổng**:

```
cong   : ghi lượt xe ra ─► cong.xe-da-qua-cong
           ├─► bai       : cập nhật vị trí container ─► bai.container-da-roi-bai
           │                   ├─► tai-chinh : chốt phí lưu bãi ─► tai-chinh.phi-luu-bai-da-chot
           │                   ├─► edo       : eDO "Đã sử dụng" ─► edo.lenh-giao-hang-da-su-dung
           │                   └─► hai-quan  : tờ khai "Đã qua khu vực giám sát"
           ├─► tai-chinh : thu phí cổng ─► tai-chinh.phi-cong-da-xu-ly ─► cong : ghi phí vào lượt xe
           ├─► lich-hen  : (với lượt VÀO) đối chiếu giờ hẹn đúng / sớm / trễ ─► lich-hen.xe-da-den
           └─► thong-bao : đẩy realtime lên màn hình điều hành
```
Thêm chức năng mới = thêm 1 dịch vụ nghe sự kiện, **không sửa** dịch vụ Cổng (nguyên lý mở/đóng).

### 4.5 API Gateway (YARP)
Một địa chỉ cho trình duyệt (khỏi CORS, khỏi lộ 14 cổng). Bảng định tuyến sinh từ `dich-vu.json`; `/dich-vu/<tên>/swagger`
xem tài liệu riêng từng dịch vụ. JWT ký bằng **một khóa chung** → dịch vụ nào cũng tự kiểm token, không cần gọi lại dịch vụ Định danh.

### 4.6 Giám sát
Mỗi dịch vụ có `/suc-khoe` (CSDL, RabbitMQ, Outbox, số bảng sở hữu/bản sao). Trang **Giám sát microservices**
(`/admin/microservices`) gom tất cả + hàng đợi RabbitMQ, tự làm mới 5 giây.

### 4.7 Chạy khi mất Wi-Fi / mất Internet (14–15/10) — "phần đáng tiền nhất"

Phải tách **3 tình huống khác nhau** (giám khảo hay gộp chung):

| Tình huống | Hệ thống làm gì | Ở đâu trong mã |
|---|---|---|
| **Mất Internet** (mạng nội bộ cảng còn) | Toàn bộ hệ thống chạy trên **máy chủ đặt tại cảng**, không cần cloud: RabbitMQ dựng từ gói offline (`docker load` từ `MicroServices/goi/*.tar`, `pull_policy: never`, ảnh ghim `4.3.6`), AI chạy cục bộ (`YOLO_OFFLINE`, `HF_HUB_OFFLINE`), thư viện JS tải về máy, mã **VietQR tự sinh tại chỗ** (chuẩn EMVCo/NAPAS 247 + CRC16-CCITT) thay vì gọi `img.vietqr.io`. Chỉ tạm mất: email / SMS thật (xếp hàng `HangDoiEmailBen`, có mạng gửi bù), đăng nhập Google | `chay_microservices.ps1`, `PaymentService.TaoChuoiVietQr`, `MaQrVietQr.jsx` |
| **Mất Wi-Fi ở trạm cổng** (máy trạm không tới máy chủ) | Trang chốt cổng là **PWA**: service worker giữ sẵn vỏ ứng dụng → tải lại / khởi động lại máy vẫn mở được. Mỗi lần bấm ghi **xuống IndexedDB trước** (hộp thư đi), có `maThaoTac` (UUID) + `lucXayRa` (giờ bấm thật). Đối chiếu lệnh / eDO / thẻ xe bằng **bản sao** tải trước (`GET /api/cong/goi-ngoai-tuyen`, ETag → 304), ghi rõ "bản sao lúc HH:mm". Có mạng lại → tự gửi lô ≤ 50 qua `POST /api/cong/dong-bo` | `public/sw.js`, `src/ngoaiTuyen/*`, `ChotBaoVe.jsx`, `CongController` |
| **Mất điện / máy chủ hỏng hẳn** | Nút **IN PHIẾU TAY** → ghi tay → nhập bù sau (không kẹt xe). Sao lưu CSDL hằng ngày (`sao_luu.ps1`, giữ 7 bản) + `phuc_hoi.ps1`; máy chủ không ngủ (gateway gọi `SetThreadExecutionState`); giám sát tự chạy lại dịch vụ chết | `MicroServices/*.ps1` |

**Máy chủ nhận lượt ghi trễ — không tin mù quáng máy trạm:**
* **Idempotent** theo `maThaoTac` (bảng cục bộ `LuotXeNgoaiTuyen`, khóa duy nhất): gửi lại / gửi đồng thời → `trung` + đúng `maXV` cũ, không bao giờ 2 lượt.
  `POST /api/cong/luot-xe` nhận thêm header `Idempotency-Key` (dùng chung khóa đó) → bảo vệ bấm 2 lần khi Wi-Fi chập chờn.
* **Sửa lệch đồng hồ** máy trạm: lệch = giờ máy chủ lúc nhận − `lucGuiTuMay`; giờ lượt = `lucXayRa` + lệch; tương lai → bây giờ; cũ hơn 72 giờ → vẫn ghi nhưng **cần đối soát**.
* **Vẫn kiểm tuân thủ** (eDO, hải quan, lệnh hủy / hoàn tất / quá hạn, biển khác lệnh). Xe đã đi qua thật nên **vẫn ghi lượt**, nhưng gắn `can-doi-soat` + lý do
  và phát sự kiện `cong.luot-xe-can-doi-soat` → bảng tin "Cần đối soát" (`GET /api/cong/can-doi-soat`, `POST .../{maThaoTac}/da-xu-ly`).
  Client tự gắn `nguon = "NGOAI-TUYEN"` trên `/luot-xe` thường vẫn bị chặn — không lách được.
* **Phí không bao giờ bị bỏ âm thầm** (trước đây lượt nhập bù `OFFLINE` được miễn phí → thất thu): có tiền mặt tại cổng + số phiếu → ghi "Tiền mặt (phiếu tay)",
  không trừ thẻ lần 2; không có → thu thẻ / công nợ như lượt thường, kèm cảnh báo đối soát. AI gian lận coi nguồn `NGOAI-TUYEN` là thao tác tay (điểm rủi ro).

**Vì sao trạm phải vào bằng HTTPS trong mạng nội bộ?** Service worker + IndexedDB bền chỉ chạy trong *secure context*: `localhost` thì được,
nhưng máy trạm mở `http://192.168.x.x` thì **không** → mất khả năng chạy offline. `tao_chung_chi.ps1` tạo **CA nội bộ** + chứng chỉ máy chủ
(SAN đủ tên máy và mọi IP), gateway nghe thêm `https://0.0.0.0:5443` (`SMARTPORT.bat chay-lan`); HTTP/2 tự bật → hết giới hạn 6 kết nối SSE
của HTTP/1.1. Cổng HTTP 5100 vẫn **chỉ nghe localhost**; 13 dịch vụ, AI, RabbitMQ không mở ra mạng.

**Vì sao không dùng `navigator.onLine`?** Nó chỉ cho biết card mạng còn nối Wi-Fi, không cho biết **máy chủ có tới được không** (Wi-Fi còn mà switch
hỏng → vẫn báo "online"). Trạm tự gọi `/suc-khoe` có giãn cách (backoff); chỉ xếp hàng khi **lỗi mạng / quá giờ / 502–504**; máy chủ đã trả lời
4xx (vd. "KHÔNG CHO RA CỔNG: chưa có eDO") thì hiện ĐỎ và **không xếp hàng** — không biến lỗi tuân thủ thành "mất mạng". Nhiều tab cùng mở
→ khóa giữa tab (`navigator.locks` / dự phòng localStorage) để chỉ 1 tab đồng bộ.

**Cập nhật giao diện an toàn:** bản build mới **không tự chiếm quyền** giữa ca (không `skipWaiting`); thanh trạng thái hiện "⬆ có bản mới", bảo vệ bấm
"Tải lại để cập nhật". Trong lúc bản mới còn chờ, mất mạng thì vẫn phục vụ trọn vỏ cũ (không bao giờ "nửa cũ nửa mới" → trang trắng).

**Diễn tập trên trình duyệt thật (15/10, Chromium qua Playwright, hệ thống thật):** đang mở trang chốt cổng → **cắt mạng** → tải lại trang (vẫn mở
được, bộ đếm VÀO 11 / RA 11 + 6 lượt gần nhất vẫn hiện) → bấm XE VÀO `51C13136` lúc 01:02:23 → "🟡 ĐÃ GHI TẠM · 1 chờ gửi"; CSDL máy chủ chưa có lượt
→ **bật mạng** → sau **2 giây** tự gửi; CSDL: `XeRaVao` có lượt với **đúng giờ bấm 01:02:23** (không phải giờ đồng bộ 01:02:52), `Nguon = NGOAI-TUYEN`,
`LuotXeNgoaiTuyen.TrangThai = da-luu`, dịch vụ Bãi nhận được qua bản sao, sự kiện Outbox đã gửi.
Sửa sau diễn tập: tải lại trang lúc mất mạng thì bộ đếm VÀO / RA về 0 → nay nhớ số đếm + 6 lượt gần nhất **của hôm nay** trong máy và làm mới
từ máy chủ ngay khi tới được máy chủ.

### 4.8 Backend bền — chịu được từng thành phần chết (14–15/10)

| Sự cố | Trước | Nay | Kiểm chứng |
|---|---|---|---|
| RabbitMQ chết | Khởi động hỏng cả hệ thống | `/suc-khoe` = **`suy-giam` (HTTP 200)**: nghiệp vụ vẫn chạy, sự kiện nằm chờ trong Outbox; giám sát tự `docker start` | Diễn tập A + D bên dưới |
| Một dịch vụ chết | Gateway trả trang trắng; phải bật tay | Gateway kiểm sức khỏe chủ động + **lỗi JSON tiếng Việt** `{thongBao, dichVu, nguon:"gateway"}` 502/503/504; `giam_sat.ps1` 15 s/vòng, **chạy lại đúng dịch vụ đó** (tối đa 3 lần / 5 phút, hỏng liên tục → "CẦN NGƯỜI XỬ LÝ") | Diễn tập B |
| Yêu cầu treo | Chờ vô hạn | Timeout theo tuyến (mặc định 30 s, đồng bộ cổng 180 s, AI video 10 phút, SSE không cắt) — `dich-vu.json` `gateway.tuyenDacBiet` | nhóm `do-ben` |
| Bấm 2 lần / mạng gửi lại | Tạo 2 lệnh / 2 eDO | `[ChongGuiLap]` + header `Idempotency-Key`, bảng cục bộ `YeuCauDaXuLy` (khóa băm theo **người dùng** + đường dẫn): gửi lại → đúng kết quả cũ; đang xử lý → 409. Gắn trên tạo lệnh cổng, phát hành eDO, khai hải quan, đặt lịch hẹn, cấp / sửa thẻ | 8 yêu cầu cùng khóa đồng thời → **đúng 1** chạy |
| Trừ thẻ trả trước | Đọc số dư rồi ghi đè → 2 xe cùng lúc mất tiền / âm | **Một câu SQL nguyên tử** `UPDATE ... SET SoDu = SoDu - @gia WHERE SoDu >= @gia`; 0 dòng → thu tiền mặt / công nợ. Sổ cái **chỉ-ghi-thêm** `GiaoDichTheXe` (trigger chặn UPDATE/DELETE, UNIQUE (MaXV, Loai) → 1 lượt xe trừ 1 lần) | 40 lần trừ + 10 lần nạp song song: số dư đúng từng đồng; xe đến 8 lần cùng lúc → trừ 1 lần |
| Cấp lệnh / eDO song song | Cấp trùng số | Giao dịch + khóa ứng dụng (`sp_getapplock`) | rà mã nguồn (nhóm `tien-bao-mat`) |
| Sự kiện tới không theo thứ tự | Bản sao **cũ** ghi đè bản **mới** | Bảng cục bộ `BanSaoPhienBan`: chỉ áp bản có phiên bản mới hơn | nhóm `do-ben` |
| Sự kiện xử lý lỗi mãi | Nằm chết trong hàng đợi lỗi | Admin xem / **phát lại**: `GET/POST /api/he-thong/su-kien-loi[/phat-lai]` | nhóm `do-ben` |
| Máy chủ mất CSDL / dựng lại | "Phục hồi giả" từ dữ liệu mẫu (âm thầm mất dữ liệu thật) | `dung_csdl_dich_vu.py`: CSDL có rồi → chỉ chạy script bảng cục bộ idempotent; thiếu 1 phần → **dừng, mã 2**, chỉ dựng mới khi `--tao-moi`; `sao_luu.ps1` / `phuc_hoi.ps1` (hỏi xác nhận, sao lưu an toàn trước khi đè) | — |
| Laptop ngủ giữa ca | Hệ thống "chết" | Gateway giữ máy thức (`SetThreadExecutionState`), nhả khi tắt | `/suc-khoe` gateway `giuMayThuc: true` |
| Mã OTP demo | Lộ `maDemo` ở môi trường Development | Chỉ trả khi chạy `SMARTPORT.bat chay-kiem-thu` **và** yêu cầu từ chính máy chủ (loopback); `chay` / `chay-lan` luôn tắt | nhóm `tien-bao-mat` |

**Nguyên tắc khi thêm dữ liệu mới:** không đổi cột bảng nghiệp vụ đang **sao chép** giữa các dịch vụ (đổi cột = phải đổi lược đồ mọi CSDL bản sao
→ dễ gãy). Dữ liệu mới (khóa chống gửi lặp, giờ gốc, đối soát, sổ cái) để ở **bảng cục bộ** tạo bằng script idempotent `Database/Add_<Bảng>.sql`
(`IF OBJECT_ID(...) IS NULL CREATE TABLE`, không DROP). Bản gộp deploy somee dùng chung mã: thiếu bảng mới → ghi log 1 lần và chạy như cũ.
Deploy bản gộp thì chạy tay `Add_YeuCauDaXuLy.sql`, `Add_LuotXeNgoaiTuyen.sql`, `Add_GiaoDichTheXe.sql` trên CSDL somee.

**Lỗi thật do diễn tập tìm ra (test đơn vị không bắt được):** `giam_sat.ps1` không bao giờ tự bật lại RabbitMQ — vòng kiểm báo
"String không có ContainsKey". Nguyên nhân: PowerShell **không phân biệt hoa thường** và dùng **phạm vi động** — trong hàm `KiemRabbitMQ` biến cục bộ
`$trangThai` (chuỗi "running") che mất bảng trạng thái chung `$TrangThai`, nên hàm con `LayTrangThai` gọi `.ContainsKey` trên chuỗi. Sửa: đổi tên biến +
dùng `$script:TrangThai` tường minh; quét cây cú pháp (AST) cả 7 script PowerShell — không còn chỗ nào bị che kiểu này.

## 5. Danh mục sự kiện (routing key = tên sự kiện; tiền tố = dịch vụ phát)

| Sự kiện | Phát bởi | Ai nghe |
|---|---|---|
| `cong.xe-da-qua-cong` | cong | bai, tai-chinh, lich-hen, thong-bao |
| `cong.lenh-giao-nhan-da-tao` / `-da-huy` | cong | lich-hen (tự tạo / hủy lịch hẹn) |
| `cong.container-da-doi-chieu` | cong | an-ninh (lưu, lệch → sự cố "Trao container") |
| `an-ninh.su-co-da-bao` | an-ninh (Outbox cùng giao dịch ghi sự cố) | thong-bao → bảng **báo động toàn cục** đỏ / vàng trên mọi màn hình |
| `cong.api-key-da-duoc-dung` | cong | dinh-danh |
| `bai.container-da-vao-bai` / `-da-roi-bai` | bai | tai-chinh, edo, hai-quan |
| `tai-chinh.phi-cong-da-xu-ly` | tai-chinh | cong |
| `tai-chinh.phieu-luu-bai-can-lap` (lệnh) | bai | tai-chinh |
| `edo.lenh-giao-hang-da-phat-hanh` / `-su-dung` / `-huy` | edo | thong-bao (+ bản sao ở cong) |
| `hai-quan.to-khai-da-khai-bao` / `-thong-quan`, `hai-quan.container-bi-tam-dung` | hai-quan | thong-bao (+ bản sao ở cong) |
| `lich-hen.lich-hen-da-dat` / `xe-da-den` / `xe-khong-den` | lich-hen | thong-bao |
| `tich-hop.yeu-cau-sinh-codeco` (lệnh), `tich-hop.luot-xe-da-vao-ban-tin-edi` | van-hanh / tich-hop | tich-hop / cong |
| `van-hanh.yeu-cau-chay-agent` (lệnh) | nhiều | van-hanh |
| `du-lieu.<bảng>` | dịch vụ chủ của bảng | mọi dịch vụ giữ bản sao bảng đó |

## 6. Bốn chức năng mới — vấn đề thật của cảng và cách giải

| Chức năng | Vấn đề ở cảng | Cách làm trong SmartPort |
|---|---|---|
| **Lịch hẹn giờ vào cổng (TAS)** | Xe container dồn cùng giờ → kẹt cổng, kẹt đường dẫn vào cảng | Khung giờ 6h–22h, sức chứa mặc định 6 xe/khung; đặt / hủy; khi xe vào tự đối chiếu **đúng giờ (±15 phút) / sớm / trễ**; quá khung 60 phút chưa đến → **không đến**; gợi ý khung vắng; lệnh giao nhận mới tự sinh lịch hẹn (qua sự kiện) |
| **eDO** | Lệnh giao hàng giấy phải đi lấy ở hãng tàu, dễ làm giả | Hãng tàu phát hành eDO **ký HMAC-SHA256**; mã QR `EDO|Số eDO|Số cont|Mã xác thực` quét tại cổng kiểm được **không cần mạng tới hãng tàu**; hết hạn / hủy / tự "Đã sử dụng" khi container rời bãi |
| **Thông quan hải quan** | Container chưa thông quan bị lấy ra; theo dõi trạng thái tờ khai thủ công | **Mô phỏng** VNACCS/VASSCM: điểm rủi ro → luồng **Xanh (<30) tự thông quan / Vàng (30–59) kiểm hồ sơ / Đỏ (≥60) kiểm thực tế**; tạm dừng; xác nhận "qua khu vực giám sát" khi container ra |
| **Chống tráo container** | Kéo nhầm / tráo container khi ra cổng | AI đọc số container trên ảnh → so với lệnh: **Khớp / Nghi vấn** (lệch 1 ký tự và sai số kiểm tra ISO 6346 → khả năng đọc nhầm) **/ Không khớp** (→ sự cố an ninh, giữ xe) / **Không đọc được** (kiểm tay) |

Cổng ra áp **tuân thủ** theo cấu hình `AppSettings:KiemTraEdo`, `KiemTraHaiQuan` = `tat | canh-bao | chan`
(dịch vụ cong dùng `chan`: thiếu eDO hợp lệ hoặc tờ khai chưa thông quan → **không cho ra**).

## 7. AI thị giác — số đo trên tập TEST giữ riêng

> Tập test **không dùng để huấn luyện**, tham số được chọn trên tập **val**, test chỉ đo 1 lần.

| Bài toán (tập test) | Hệ cũ (trình duyệt, 01/10) | Mô hình 1 (YOLO26 / CRNN) | **Bản cuối** | Cách làm bản cuối |
|---|---|---|---|---|
| Mã container — **nguyên ảnh camera** (350 ảnh) | 14,6% | 96,0% | **98,0%** | YOLO26 tìm vùng mã → CRNN → giải mã ràng buộc ISO 6346 → **gộp chứng cứ** nhiều vùng |
| Mã container — vùng mã cắt sẵn (350) | 31,1% | 97,1% | **98,0%** | như trên |
| Biển số — **nguyên ảnh camera** (752 ảnh) | 48,3% | 87,5% | **91,2%** (ô tô 94,8% · xe máy 89,5%) | YOLO26 tìm biển → **YOLO26 + RF-DETR** đọc ký tự → gộp + quy hoạch động theo mẫu biển VN |
| Biển số — ảnh biển cắt sẵn (752) | 39,5% | 91,2% | **92,7%** | như trên |

* **Đo lại qua dịch vụ thật** (gateway + JWT, gửi đủ 752 + 350 ảnh test): kết quả **khớp 100%** với đánh giá offline;
  thời gian trung vị qua gateway: biển số 184 ms, container 248 ms (GPU RTX 5060 Laptop).
* Mã container nguyên ảnh: 343 đúng · 5 sai nhưng vẫn qua ISO · 2 không đủ tin cậy → kiểm tay. Trong 7 ca sai có **2 ca nghi
  nhãn sai**: ảnh ghi rõ `STXU 203562 0` nhưng nhãn là `STTU2045620`; nhãn `MRKK7981930` không hợp lệ (ký tự thứ 4 phải
  là U/J/Z). Không sửa nhãn — con số 98,0% là tính nghiêm.
* Biển số: lỗi còn lại chủ yếu là nhầm 1 ký tự (5↔9, H↔M↔N) và **biển seri "LD"** (vd. `50LD04411` đọc thành `50D04411`)
  vì mẫu `DDLL…` hiếm trong dữ liệu huấn luyện (14/1.024 biển).
* Huấn luyện (mAP50 trên tập test riêng của bộ phát hiện): vùng mã container YOLO26 **0,994**; ký tự biển RF-DETR **0,981**
  (F1 0,977); ký tự biển YOLO26 0,976; tìm biển YOLO26 0,995 (val).
* Chưa dùng: **DINOv3, SAM 3.1** (mô hình có điều khoản của Meta — cần chủ tài khoản đăng nhập Hugging Face và đồng ý);
  **Depth Anything 3** đã tải nhưng không gắn vào vì dự án chưa có dữ liệu có nhãn độ sâu để đo lợi ích.
* **Chưa đạt 100%** và không hứa 100%: ảnh thật có mờ, lóa, che khuất, sơn bong. Thiết kế bù bằng **từ chối khi không chắc**
  (container) và **đối chiếu với lệnh** (chống tráo) thay vì đoán.
* Script tái lập: `Web/SmartPort.AI/cai_tien_container.py`, `cai_tien_bien_so.py` (chỉnh trên val, đo test 1 lần),
  `danh_gia_container.py`, `danh_gia_bien_so.py`; kết quả trong `Web/SmartPort.AI/runs/danh_gia/`.

Ý tưởng chính (để trả lời vấn đáp):
* **Mã container = 4 chữ chủ sở hữu (chữ thứ 4 là U/J/Z) + 6 số seri + 1 số kiểm tra** (ISO 6346). Giải mã CTC **có ràng buộc**:
  chỉ chấp nhận chuỗi đúng cấu trúc **và** đúng số kiểm tra → sai 1 ký tự gần như luôn bị phát hiện.
  *Điểm yếu đã biết:* tổng mod 11 = 10 được quy về số kiểm tra 0 → vài mã sai vẫn "qua" kiểm tra.
* **Gộp chứng cứ**: đọc mọi vùng mã (ngang, dọc, 2 khối) × 3 mức lề, tính **xác suất CTC chính xác** của từng ứng viên trên mọi
  lần đọc, cộng có trọng số theo độ tin cậy bộ phát hiện → vùng mờ vẫn đóng góp, vùng sai không kéo kết quả xuống.
* **Ưu tiên nhẹ** mã chủ sở hữu đã biết (cảng thật: sổ đăng ký BIC / danh sách hãng tàu khách hàng).
* Không đủ tin cậy → **không trả mã** → cổng chuyển kiểm tay (thà hỏi người còn hơn báo động tráo nhầm).
* Biển số: gộp ô ký tự của **YOLO26 + RF-DETR**, rồi **quy hoạch động** chọn chuỗi khớp mẫu biển số Việt Nam
  (DD L [L|D] DDD–DDDDD, biển 2 dòng: dòng trên 4 ký tự) → tự bỏ ô thừa (ốc vít, viền), sửa nhầm chữ/số theo vị trí.
* **Ngưỡng từ chối biển số (08/10)**: quy hoạch động *luôn* ép ra một chuỗi đúng mẫu VN — kể cả khi biển quá nhỏ hay là biển
  lạ (biển đỏ quân đội `TN-24-59`, biển Trung Quốc) → từng đọc ra `12J5552` "hợp lệ" nhưng sai. Nay tính **độ tin cậy** = trung
  bình nhân (xác suất có ký tự × xác suất đúng lớp) của các ký tự giữ lại; dưới **0,638** (chọn trên VAL: giữ mọi biển đọc đúng
  của VAL) thì **không trả biển**, chỉ trả `bienSoDoan` + "chuyển kiểm tay". Đo TEST 1 lần: đúng 91,2% → **91,1%** (mất 1 ảnh),
  đọc sai mà vẫn cho qua 8,8% → **7,3%**, chuyển kiểm tay 1,6% (`chon_nguong_tin_cay_bien_so.py`).

### 7.1 Nhận diện trên VIDEO (08/10)

Trang `/admin/ai-may-chu` → tab **Video**: chọn video là AI chạy luôn (mp4 H.264/H.265, mov, avi, mkv, webm · ≤ 200 MB · ≤ 10 phút).

```
trình duyệt ──POST /api/ai-thi-giac/video?loai=ca-hai&fps=3 (thân = tệp video)──► gateway ──► ai-thi-giac
            ◄── 202 {maViec} ──  (xử lý NỀN, không giữ kết nối — mẫu "Asynchronous Request-Reply")
            ──GET /api/ai-thi-giac/video/{maViec}?tu=n (mỗi 0,7 s)──► tiến độ + đối tượng + kết quả khung MỚI từ khung n
            ──DELETE /api/ai-thi-giac/video/{maViec}──► dừng
```
1. **Lấy mẫu khung** bằng OpenCV + FFmpeg (mặc định 3 khung/giây): xe qua cổng mất vài giây, đọc cả 30 khung/giây chỉ tốn GPU.
2. **Đọc từng khung** bằng đúng bộ đọc ảnh (biển số + mã container) — giữ khóa GPU *từng khung* nên yêu cầu đọc ảnh vẫn chen vào được.
3. **Gom theo đối tượng** (theo vết đơn giản): khung gần nhau về thời gian + chuỗi sai khác ≤ 2 ký tự (Levenshtein) hoặc khung biển
   trùng chỗ (IoU ≥ 0,3) → cùng một xe / container.
4. **Bỏ phiếu theo thời gian**: mỗi khung một phiếu, trọng số = độ tin cậy → khung mờ đọc lệch 1 ký tự bị các khung rõ "bầu lại".
   Đối tượng "chắc chắn" khi có ít nhất 1 khung đọc chắc và chuỗi thắng ≥ 1/2 phiếu; chỉ thấy ở 1 khung, không chắc → bỏ.
5. Container trong video dùng ngưỡng chặt hơn ảnh lẻ: **độ tin cậy ≥ 0,421** (chọn trên VAL, giữ 99% mã đúng) — video có nhiều khung
   đường trống / đầu kéo dễ "nặn" ra mã đúng ISO với tin cậy ≈ 0 (đã gặp `HENU6869046` 0,000 trên video thử).

Kết quả chạy thật qua gateway (GPU RTX 5060 Laptop):

| Video | Kết quả |
|---|---|
| Video thử 29 giây dựng từ **ảnh thật có đáp án** (1 container tập TEST, 2 biển số tập TEST, 2 container + 1 xe biển đỏ quân đội từ ảnh người dùng gửi; camera tiến lại + nhòe chuyển động + nén JPEG) | **5/5 đối tượng đúng, 0 báo nhầm**; 87 khung, ~300 ms/khung, xử lý 27 giây. Bỏ phiếu sửa được các khung đọc lệch (`57F31173`, `59L204773`, `53E214773` bị bầu lại) |
| Video thật (tin VTC1, xe đầu kéo rơi xuống nước, 17 giây, H.264 528×356) | Không báo đối tượng nào — video chỉ có biển Trung Quốc bị che, không có mã container đọc được (đúng: không đoán bừa) |

*Nói thật:* video thử dựng từ ảnh tĩnh nên dễ hơn camera thật (không có xe chạy nhanh, mưa đêm); đây là **kiểm thử chức năng**, chưa
phải số đo độ chính xác video. Hạn chế: mỗi khung lấy 1 biển rõ nhất + 1 mã container → hợp camera cổng (mỗi lượt 1 xe); video nhiều xe
cùng lúc cần bộ theo vết nhiều đối tượng (ByteTrack/DeepSORT). Mã nguồn: `Web/SmartPort.AI/nhan_dien_video.py`.

### 7.2 Cảnh báo nguy hiểm — an toàn lao động trong cảng (08–09/10)

Trang `/admin/an-toan-ai` (menu *AI & an ninh → Cảnh báo nguy hiểm AI*): nguồn hình là **video / webcam / camera IP**; trình duyệt gửi
từng khung (≤ 960 px, JPEG) → `POST /api/ai-thi-giac/an-toan/khung` (gateway, JWT, mỗi camera một **phiên** có trạng thái) → dịch vụ
AI Python (`SmartPort.AI/an_toan_cang.py`). Rủi ro mức **NGUY HIỂM** / thiếu bảo hộ được **tự báo** sang dịch vụ An ninh
(`POST /api/an-ninh/su-co`) → sự kiện `an-ninh.su-co-da-bao` qua **Outbox** → Thông báo → **bảng báo động đỏ trên mọi màn hình**
(kèm chuông), bấm vào mở phòng an ninh.

```
khung camera ─► YOLO26s (COCO: người, ô tô, xe tải, xe buýt, xe máy) ─► ByteTrack (mã đối tượng ổn định)
             ─► đổi sang MÉT trên mặt đất ─► Kalman (vị trí + vận tốc) ─► máy rủi ro ─► chống báo động giả ─► sự kiện
             └► cắt TỪNG NGƯỜI, phóng 320 px ─► YOLO26s đồ bảo hộ (Construction-PPE) ─► luật mũ / áo ─► bỏ phiếu 15 khung
```

| Rủi ro | Cách tính | Mức |
|---|---|---|
| **Sắp va chạm xe – người / xe – xe** | Xe = **đoạn thẳng** mép dưới khung trên mặt đất (xe đầu kéo 16 m nhìn ngang không bị coi là hình tròn 8 m). Mô phỏng chuyển động **tương đối** 3 s tới (bước 0,1 s) → **TTC** = lúc khoảng cách ≤ nửa bề dày 2 vật | NGUY HIỂM TTC ≤ 1,5 s · CẢNH BÁO ≤ 3 s, lề 1 m (ngưỡng hệ thống FCW ô tô) |
| **Xe sắp va chạm vật cản cố định** | Vùng "vật cản" vẽ quanh **chân** dãy container xếp / chân cẩu / tường (YOLO COCO không nhận các vật này) → đổi sang mét → mô phỏng đoạn mép xe đi vào đa giác; chỉ khi xe đang **tiến lại** | như trên |
| Người quá gần xe đang chạy | khoảng cách tới mép xe | < 1,5 m / < 3 m |
| Người vào vùng cấm (dưới cần trục, bãi xếp) · người trên làn xe khi có xe tới | điểm chân trong đa giác | NGUY HIỂM / CẢNH BÁO |
| Quá tốc độ (toàn bãi 15 km/h hoặc theo vùng) | vận tốc Kalman | > 1,5 × giới hạn: NGUY HIỂM |
| Người ngã / nằm bất động | khung chuyển sang nằm (rộng/cao > 1,25) ≥ 1,5 s và gần như đứng yên | NGUY HIỂM |
| Không đội mũ bảo hộ · không mặc áo phản quang | 2 tầng + luật chọn trên VAL (bên dưới) | CẢNH BÁO |

**Đổi sang mét.**
* *Đã hiệu chỉnh*: 4 điểm ảnh ↔ 4 điểm thật → **homography**.
* *Chưa hiệu chỉnh*: **tự ước tỉ lệ** theo mô hình camera lỗ kim — vật cao h đứng cách Z cao f·h/Z pixel. Mỗi người / xe cho 1 mẫu
  s = cao khung / cao thật của loại (người 1,7 m, xe tải 3,2 m…); khớp s(y) = a·y + b (bình phương tối thiểu, loại ngoại lai)
  → Z = f/s(y), X = (x − W/2)/s(y). Sai góc nhìn (f) làm khoảng cách **và** vận tốc lệch cùng tỉ lệ → **TTC theo chiều sâu
  không đổi** (có kiểm thử).
* Mọi vật cùng một độ sâu (không ước được độ dốc a): giữ mô hình cũ, hoặc dùng chiều cao cột camera khai báo.

**Kalman bền vững** (sửa sau khi chạy video):
* Nhiễu đo = độ lệch mặt đất khi chân lệch 2 px (người) / 3 px (xe) → vật xa được tin ít hơn.
* Mép dưới bị vật gần hơn che → nhiễu × 3.
* Phép đo lệch > 4σ (Mahalanobis — ô tô chạy ngang che xe tải, hộp gộp 2 xe) bị **bỏ qua, chỉ dự đoán** → vết không mất vận tốc.
  Trước đây vết bị khởi tạo lại → cảnh báo trễ ~1 s.
* Vận tốc > 72 km/h trong bãi = đo sai, không dùng.
* Bước thời gian Kalman là **dt thật** (trước đây cắt ≤ 1 s → xe bị che 2 s hiện lại bị tính vận tốc gấp đôi, báo quá tốc độ giả);
  mất dấu > 3 s thì khởi tạo lại vết.
* Bộ phát hiện chạy ngưỡng **0,1**, vết chỉ được *tạo* từ hộp ≥ 0,3: hộp điểm thấp (bị che một phần) vẫn được ByteTrack ghép vào
  vết cũ nhưng không sinh vết mới, không làm mẫu tự ước tỉ lệ. Bộ đệm mất vết đặt đúng **3 giây theo fps thật**
  (supervision tính theo khung × frame_rate/30 — bản cũ chỉ giữ ~0,9 s).

**Chống báo động giả (mọi ngưỡng tính theo GIÂY, không theo số khung):**
* Rủi ro phải có trong ≥ 75% khung của cửa sổ **0,4 s** (fps thật ước từ dấu thời gian khung) — máy chủ bận 2,5 khung/giây không bị
  chậm thêm hàng giây, máy chủ 30–60 khung/giây không bị "xe ma" 0,1 s qua mặt; vận tốc chỉ được tin sau **≥ 0,5 s** theo vết.
* Mỗi (loại, cặp đối tượng) nghỉ 4 s mới báo lại; đã báo NGUY HIỂM thì không báo thêm mức thấp hơn cùng cặp.
* Va chạm chỉ tính khi khoảng cách **đang giảm** (≥ 0,3 m/s): 2 xe chạy song song, xe lướt dọc dãy cont không bị báo; người ngồi trên
  xe máy / xe đạp đang chạy không bị coi là "người sát xe"; người sát xe đang chạy luôn được báo ở mức cao nhất (NGUY HIỂM).
* Người ngã: xét **số giây nằm liên tục** (≥ 1,35 s), đúng cả khi máy chủ chỉ 1 khung/giây hay 60 khung/giây.
* Cấu hình sai (4 điểm hiệu chỉnh trùng / thẳng hàng / bấm chéo, vùng vật cản vẽ trên chân trời) **không làm sập phiên**: máy bỏ phần
  sai, chạy tiếp bằng tự ước tỉ lệ và trả `canhBaoCauHinh` → giao diện hiện "Cấu hình chưa dùng được".

**Đồ bảo hộ — 3 bước cải tiến, mỗi bước có số đo** (`danh_gia_bao_ho_khoang_cach.py`; mức **từng người** trên Construction-PPE;
VAL 235 / TEST 234 người có nhãn — vùng đầu / thân xét **theo lớp** y hệt máy cảnh báo (mũ: tâm ở −10…40% chiều cao người,
áo: 15…85%; bản đo trước dùng 1 vùng chung nên bỏ sót áo nằm ở 15–40%, đã đo lại); mô phỏng camera 1280×720: thu nhỏ để người cao 200 / 120 / 80 px, khung **không nén** và
**nén JPEG 70** như luồng camera IP; luật + ngưỡng chọn trên VAL gộp mọi điều kiện, TEST đo 1 lần):

1. **Luật dựa trên lớp mạnh**: "thiếu mũ" = điểm *helmet* thấp **và** có dấu hiệu *no_helmet* (lớp helmet AP50 0,93; lớp no_helmet chỉ 0,23).
2. **Suy luận 2 tầng**: chạy cả khung thì YOLO thu 1280 → 640, người 80 px chỉ còn đầu ~10 px. Nay **cắt từng người** (lề 20%
   ngang, 15% trên) **phóng 320 px** rồi mới tìm mũ / áo — cùng hàm `cat_nguoi` cho máy cảnh báo và bộ đánh giá.
3. **Mô hình bền suy giảm** `bao_ho_ben`: nén JPEG xoá tín hiệu "đầu trần" của đầu nhỏ (điểm 0,21 → 0,03 trên video thử). YOLO26s
   huấn luyện lại (cùng công thức) trên Construction-PPE + **1 bản suy giảm mỗi ảnh**: thu nhỏ để người cao 40–160 px, 30% làm mờ,
   nén JPEG chất lượng 25–85 ngay ở độ phân giải nhỏ, phóng lại (`tao_du_lieu_suy_giam.py`). Test giữ nguyên ảnh gốc.

| F1 phát hiện **thiếu mũ** (TEST) | Gốc | 200 px | 120 px | 80 px | 200 px nén | 120 px nén | 80 px nén |
|---|---|---|---|---|---|---|---|
| Hệ cũ: cả khung, mô hình gốc | 0,89 | 0,87 | 0,69 | 0,22 | 0,58 | 0,18 | **0,05** |
| + cắt người 320 px | 0,91 | 0,91 | 0,93 | 0,84 | 0,70 | 0,51 | 0,45 |
| **+ mô hình bền suy giảm (đang dùng)** | **0,91** | **0,94** | **0,93** | **0,91** | **0,91** | **0,90** | **0,85** |

| F1 phát hiện **thiếu áo phản quang** (TEST) | Gốc | 200 px | 120 px | 80 px | 200 px nén | 120 px nén | 80 px nén |
|---|---|---|---|---|---|---|---|
| Hệ cũ: cả khung, mô hình gốc | 0,89 | 0,86 | 0,76 | 0,55 | 0,64 | 0,34 | **0,04** |
| **Đang dùng** (cắt người + mô hình bền, luật áo 0,40 / 0,05) | **0,90** | **0,94** | **0,92** | **0,90** | **0,92** | **0,84** | **0,87** |

* Mô hình đang dùng: thiếu mũ precision 0,92–0,95, recall 0,78–0,95; thiếu áo precision 0,90–0,98, recall 0,79–0,91 → **hầu như
  không báo nhầm**. Bỏ sót được bù bằng **bỏ phiếu 15 khung** (≥ 6 quan sát, ≥ 60% cùng chiều).
* Chọn theo VAL: F1 trung bình thiếu mũ 0,881 → **0,969**, thiếu áo 0,845 → **0,896**. Mô hình bền chạy *cả khung* lại kém hơn →
  hai cải tiến phải đi cùng nhau. `danh_gia_bao_ho_nguoi.py --tu-kiem` kiểm vùng của bộ đánh giá khớp máy cảnh báo (10/10).
* mAP50 phát hiện (11 lớp, TEST gốc): 0,558 → 0,542 — gần như giữ nguyên; lớp helmet 0,925 → 0,931, vest 0,883 → 0,911.

**Tốc độ (RTX 5060 Laptop, khung 1280×720, 2 người + 2 xe):** **36 ms/khung** ở máy chủ, **64 ms** tính cả vòng qua gateway
(~15 khung/giây). Trước khi sửa là 418 ms; có 2 nguyên nhân:
1. **`ThreadingHTTPServer` tạo luồng mới cho mỗi yêu cầu.** PyTorch giữ handle cuBLAS / cuDNN theo luồng → mỗi khung khởi tạo
   lại (đo: 45 ms cùng luồng, 117 ms mỗi khung một luồng). Sửa: máy cảnh báo chạy trên **1 luồng cố định**.
2. **Windows 11 "power throttling / EcoQoS".** Dịch vụ chạy nền, cửa sổ ẩn → bị xếp lên lõi tiết kiệm điện của CPU lai.
   Sửa: tự gọi `SetProcessInformation(ProcessPowerThrottling)` lúc khởi động. Áp cho cả tiến trình AI, nên các API biển số,
   container cũng nhanh hơn số ghi ở mục 7 (chưa đo lại).

**Kiểm thử:**

| Bộ kiểm thử | Kiểm gì | Kết quả |
|---|---|---|
| `kiem_thu_an_toan.py` (camera ảo lỗ kim, không cần GPU) | Homography sai < 1 cm; tự ước tỉ lệ sai Z < 3%; vận tốc sai < 10%; TTC khớp thời gian thật ± 0,4 s; khai sai góc nhìn vẫn báo đúng lúc; xe đầu kéo dài đi ngang không báo nhầm; 2 xe cắt ngang; xe đỗ yên; vùng cấm, làn xe, giới hạn tốc độ; người ngã; mũ / áo; **máy chủ chỉ 2,5 khung/giây vẫn báo ≥ 2 s trước**; **hộp xe nhảy 5 m 0,3 s vẫn giữ vận tốc**; không báo trùng mức; **vật cản**: lao tới / chạy song song / vẽ nhầm trên trời; mọi xe cùng độ sâu; + **29 kịch bản hồi quy** của đợt rà soát (che 1–2 s không vọt tốc độ, khung thưa 1 khung / 2 s, ngã ở 1 khung/s và 60 khung/s, người lái xe máy, 2 xe song song, vật cản khi đã hiệu chỉnh, hiệu chỉnh suy biến, vùng tốc độ chồng nhau, cảnh tĩnh nhiễu không ra "xe chạy"…) | **62/62** |
| `kiem_thu_dich_vu_an_toan.py` (bộ phát hiện giả, không GPU) | Phiên hết hạn tạo lại **giữ cấu hình** + `canCauHinh`; mỗi tài khoản ≤ 16 camera, hết chỗ → 429 rõ ràng (không đẩy camera người khác); ảnh khai kích thước khổng lồ / bom nén → 400; cấu hình quá nhiều vùng → 400; JWT sai iss / aud; **token bị thu hồi** (khóa tài khoản, đổi mật khẩu) bị từ chối (hỏi Định danh, đệm 60 s) | **79/79** |
| `smartport-web/kiem-thu/canh-bao-nguy-hiem.mjs` (Node) | Logic giao diện: phiên bản cấu hình gửi đúng khi đổi giữa chừng, gộp + gửi tuần tự báo sự cố (không spam), khóa chống trùng, kiểm 4 điểm hiệu chỉnh, dọn webcam / hẹn giờ, CSS cũ của Trạm camera không bị ghi đè | **69/69** |
| `kiem_thu_an_toan_video.py` (qua gateway + JWT, như trình duyệt) | Video 12 s dựng từ **ảnh thật có đáp án** (2 công nhân tập TEST Construction-PPE: 1 đủ bảo hộ, 1 đầu trần; xe tải từ ảnh người dùng; ô tô từ bộ biển số VN), phối cảnh camera cao 6 m, nén VP8 + JPEG: báo va chạm ≥ 2 s trước, NGUY HIỂM ≥ 1 s trước, vào vùng cấm ≤ 1 s, quá tốc độ **đúng ô tô**, **đầu trần bị báo thiếu mũ**, **người đủ bảo hộ không bị báo**, không báo vô lý | **7/7** |
| C# `microservices` (20 kiểm tra cảnh báo nguy hiểm) | xem mục 8 | **144/144** |

**Rà soát độc lập (09/10).** Sau khi mọi kiểm thử đã đạt, 5 người rà (engine AI, dịch vụ Python, .NET, giao diện, phương pháp đánh
giá + file bat) đọc code tìm lỗi mà kiểm thử **không phủ tới**; mỗi lỗi được 1 người khác cố **bác bỏ** bằng code / chạy thử.
43 lỗi báo → **39 lỗi thật** (4 bị bác). Ví dụ: máy chủ ≤ 1,25 khung/s thì không bao giờ báo người ngã; xe bị che 1–2 s hiện lại bị
tính 23–40 km/h; người sát xe đang chạy lại bị hạ xuống "cảnh báo"; vùng vật cản bị bỏ qua khi đã hiệu chỉnh 4 điểm; tắt "tự báo"
giữa chừng không có tác dụng; rò rỉ webcam; phòng an ninh hiện loại sự cố mới thành "Sự cố khác"; mã sự kiện realtime đánh lại từ 1
sau khi khởi động lại; file bat báo "TẤT CẢ ĐẠT" cả khi một phần bị bỏ qua. **Đã sửa cả 39**, mỗi nhóm có người kiểm lại độc lập
(tự sửa thêm 15 hồi quy do chính bản sửa gây ra) và kiểm thử hồi quy cho từng lỗi.

*Nói thật:*
* Video kiểm thử là **dựng từ ảnh thật** (dán theo đúng phối cảnh), chưa phải camera cảng thật có xe chạy, mưa, đêm, ngược sáng.
* Đánh giá đồ bảo hộ là trên Construction-PPE (công trường, gần giống cảng) với mô phỏng khoảng cách + nén, chưa có tập ảnh cảng
  Việt Nam gán nhãn.
* Chưa hiệu chỉnh 4 điểm thì vận tốc lệch ~15–50% (giả định chiều cao trung bình người / xe + góc nhìn khai báo); **thời điểm
  báo va chạm vẫn đúng**, vì sai số tỉ lệ triệt tiêu trong TTC theo chiều sâu.
* Người cao < 80 px không đo bảo hộ (ngoài vùng đã kiểm chứng).
* `supervision.ByteTrack` báo sẽ bỏ ở bản 0.31 → cần ghim phiên bản hoặc đổi API khi nâng cấp.
* YOLO COCO không nhận xe nâng, reach stacker, cẩu RTG → hiện được coi như xe tải / ô tô nếu nhận ra, hoặc vẽ thành vùng vật cản.
  Bộ phát hiện **thiết bị cảng chuyên dụng** là hướng nâng cấp tiếp (cần bộ dữ liệu có nhãn).

## 8. Chạy và kiểm thử

**Một file điều khiển tất cả: `Web/SMARTPORT.bat`** (bấm đúp → menu; hoặc `SMARTPORT.bat <lệnh>`):

| Menu | Lệnh | Làm gì |
|---|---|---|
| 1 | `chay` | Bật Docker Desktop, kiểm SQL Server, chạy RabbitMQ + 13 dịch vụ .NET + AI Python + gateway, mở trình duyệt |
| 2 | `build` | Build lại mã nguồn rồi chạy |
| 3 | `tat` | Tắt 15 tiến trình + RabbitMQ |
| 4 | `tinh-trang` | Gọi `/suc-khoe` từng dịch vụ (đọc `dich-vu.json`) → bảng tình trạng |
| 5 / 6 | `mo` / `an-toan` | Mở giao diện / trang Cảnh báo nguy hiểm AI |
| 7 | `kiem-thu` | Build bộ kiểm thử → 9 nhóm C# + engine AI an toàn (62) + vùng bảo hộ (10) + dịch vụ AI (79) + giao diện cảnh báo (69) + trạm ngoại tuyến (156, Node) + video đầu-cuối qua gateway — **chạy menu 15 trước**; log ở `MicroServices/nhat-ky/kiem-thu-*.txt`; mã thoát 1 nếu có lỗi, **3 nếu có phần bị bỏ qua** (hệ thống chưa chạy) — không bao giờ báo "đạt" suông |
| 8 | `kiem-thu-nhanh` | 5 nhóm C# không cần hệ thống chạy + kịch bản AI an toàn |
| 9 | `danh-gia-ai` | Đánh giá lại đồ bảo hộ trên VAL/TEST (mục 7.2) |
| 10 | `chay-lan` | Như `chay` + gateway nghe thêm **HTTPS 5443** cho máy trạm trong mạng nội bộ (cần menu 14 trước) — mục 4.7 |
| 11 | `giam-sat` | Cửa sổ giám sát: 15 s/vòng, tự chạy lại dịch vụ / RabbitMQ / Docker chết (tối đa 3 lần / 5 phút) |
| 12 | `sao-luu [thư-mục]` | Sao lưu 13 CSDL `SmartPortMS_*` + ảnh tải lên + bí mật, giữ 7 bản (đưa thư mục → chép thêm ra USB) |
| 13 | `phuc-hoi [bản]` | Phục hồi từ bản sao lưu (hỏi xác nhận, phải tắt hệ thống, sao lưu an toàn trước khi đè) |
| 14 | `tao-chung-chi` | Tạo CA nội bộ + chứng chỉ máy chủ — **chỉ ghi tệp**, in lệnh firewall / certutil để bạn tự chạy |
| 15 | `chay-kiem-thu` | Chạy hệ thống cho bộ kiểm thử (OTP demo trả mã cho chính máy chủ); kiểm thử xong chạy lại `chay` |

Diễn tập sự cố trên hệ thống đang chạy: `python MicroServices/dien_tap_su_co.py [--chi A,B,C,D]` (A RabbitMQ chết, B dịch vụ Cổng chết,
C bấm 2 lần, D giám sát tự bật lại RabbitMQ; mã thoát 0 = đạt hết).

**Lệnh quản trị BẠN tự chạy** (PowerShell "Run as administrator" — script không bao giờ tự sửa cài đặt Windows):
```powershell
# 1) Mở cổng HTTPS cho máy trạm, CHỈ mạng Private (mạng cảng phải là Private: Get-NetConnectionProfile)
New-NetFirewallRule -DisplayName 'SmartPort HTTPS 5443' -Direction Inbound -Protocol TCP -LocalPort 5443 -Action Allow -Profile Private
# 2) Mỗi máy trạm: tin CA nội bộ (chép Web\MicroServices\.bimat\chung-chi\ca.cer sang)
certutil -addstore -f Root ca.cer
# 3) Tự chạy SmartPort + giám sát khi đăng nhập Windows: in lệnh Task Scheduler rồi tự dán
pwsh Web/MicroServices/giam_sat.ps1 -InLenhLich
# 4) Máy chủ là laptop: gập màn hình không ngủ khi cắm điện
powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0; powercfg /setactive SCHEME_CURRENT
```

Lối tắt Desktop cũ **SmartPort - Chay / Tat** (`CHAY_SMARTPORT.bat`, `TAT_SMARTPORT.bat`) vẫn dùng được.

Toàn bộ nằm trong **một thư mục** `Downloads/SmartPort/Web` (dịch vụ AI + mô hình ở `Web/SmartPort.AI`, venv và mô hình
không lên git).

```powershell
pwsh Web/MicroServices/chay_microservices.ps1            # RabbitMQ (Docker) + CSDL từng dịch vụ + build + 15 tiến trình
pwsh Web/MicroServices/chay_microservices.ps1 -KhongBuild -Chi cong,ai-thi-giac   # chỉ chạy lại vài dịch vụ
pwsh Web/MicroServices/dung_microservices.ps1            # tắt (thêm -TatRabbitMQ để tắt luôn RabbitMQ)
```
Giao diện: http://localhost:5100 · RabbitMQ: http://localhost:15672 · Swagger: http://localhost:5100/dich-vu/<tên>/swagger

Kiểm thử tự động (`dotnet run --project Web/tests/SmartPort.KiemThu -- <nhóm>`), lần chạy 08/10/2026:

| Nhóm | Kiểm gì | Kết quả |
|---|---|---|
| `kien-truc` | Ranh giới module, chỉ dịch vụ chủ được ghi bảng, tên sự kiện đúng tiền tố dịch vụ, thời điểm chứng từ tròn giây | **71/71** |
| `ai` | Thuật toán AI vận hành (so với giá trị tính tay) | **40/40** |
| `mat-khau` | Quy tắc mật khẩu, mã quên mật khẩu, dấu phiên | **27/27** |
| `giay-to` | Bóc thông tin bằng lái từ văn bản OCR | **12/12** |
| `dang-nhap-ngoai` | Chuẩn hóa số điện thoại, vé ký HMAC (hết hạn / sai mục đích / bị sửa), PKCE chuẩn RFC 7636, id_token Google (chữ ký giả, iss / aud sai, hết hạn, nonce, email_verified) | **30/30** |
| `microservices` (09/10) | Thêm 20 kiểm tra **cảnh báo nguy hiểm** (+3 ở nhóm khác): 401 khi chưa đăng nhập, mô hình đã nạp, sai mã phiên → 400, 41 khung camera qua gateway → theo vết + vận tốc, dự báo va chạm ≥ 1 s trước, vào vùng cấm ≤ 1 s, quá tốc độ đúng ô tô, không báo vô lý, báo sự cố → sự kiện `an-ninh.su-co-da-bao` tới bảng tin, chống trùng 30 s **theo danh tính nguy hiểm** (`khoaTrung`: 2 nguy hiểm khác nhau cùng chỗ đều được ghi, gửi lại cùng khóa thì không, nguyên tử khi gửi song song), tên loại sự cố mới hiện đúng ở phòng an ninh | **144/144** |
| `microservices` (08/10) | Trên hệ **đang chạy**: 15 tiến trình khỏe, sao chép dữ liệu qua RabbitMQ, đăng nhập qua gateway, 24 API, luồng eDO + hải quan (luồng Đỏ) + ra cổng, chống tráo (cả **ảnh thật** qua dịch vụ AI), TAS, **đăng ký (saga)**, **OTP số điện thoại** (gửi lại sớm bị chặn, sai mã, hoàn tất đăng ký, tự liên kết), Google chưa cấu hình, không tin lỗi / kẹt, **AI video** (401 khi chưa đăng nhập, tệp hỏng → 400, 202 + hỏi tiến độ, đủ 5 đối tượng đúng đoạn, không báo nhầm, dừng giữa chừng, 404) | **121/121** |

**Sau đợt nâng cấp mất mạng + backend bền (15/10/2026)** — chạy `SMARTPORT.bat chay-kiem-thu` rồi `kiem-thu`:

| Nhóm | Kiểm gì | Kết quả |
|---|---|---|
| `kien-truc`, `ai`, `mat-khau`, `giay-to`, `dang-nhap-ngoai` | như trên (+ bảng cục bộ mới thuộc đúng dịch vụ) | **72 · 40 · 27 · 12 · 30** — đạt hết |
| `microservices` | chạy lại toàn bộ trên hệ đang chạy sau nâng cấp | **144/144** |
| `ngoai-tuyen` (mới) | `dong-bo` idempotent (gửi 3 lần, 2 lô đồng thời), sửa lệch giờ, quá 72 giờ, tuân thủ vẫn kiểm → cần đối soát + sự kiện cùng giao dịch, `dong-bo-lo` cũ, `Idempotency-Key` trên `luot-xe` (201 / 200 phát lại / 409 / 422), ETag → 304; sau rà soát lần 2: vượt bản sao, tiền mặt bất thường / lệch giá, xe ra không khớp lệnh, đối soát 4 mắt | **69/69** |
| `do-ben` (mới) | băm khóa, bộ lọc `[ChongGuiLap]` (+ so thân yêu cầu, gia hạn giữ chỗ, giữ chỗ khi lưu kết quả lỗi), lỗi tạm / vĩnh viễn, sức khỏe 3 mức + bản tóm tắt cho người ngoài, phát lại hàng đợi lỗi có chặn tuổi, chống bản sao cũ, gateway (lỗi JSON, HTTPS, 3 lần lỗi liên tiếp mới đánh ngừng); + kho SQL thật | **91/91** (+ CSDL **100/100**) |
| `tien-bao-mat` (mới) | VietQR EMVCo + CRC16, OTP demo không lộ, rà mã chống lỗi cũ, nạp + xe ra song song qua gateway; + trừ / nạp thẻ đồng thời, công nợ khi ghi trễ, thẻ tháng theo ngày xe qua trên CSDL | **28/28** (+ CSDL **30/30**) |
| `tich-hop` (`SmartPortDB_BaoCao`, đã sao lưu trước) | Cổng → Bãi → Tài chính; lượt nhập bù nay **tính phí** (không thẻ → Công nợ chờ thu) | **62/62** |
| Python AI + Node | AI an toàn 62, vùng bảo hộ 10, dịch vụ AI 79, giao diện cảnh báo 69, **trạm ngoại tuyến 262**, video đầu-cuối 7 | đạt hết |
| `dien_tap_su_co.py` (hệ thật) | A RabbitMQ chết · B Cổng chết · C bấm 2 lần · D giám sát bật lại RabbitMQ | **23/23** (chạy lại sau rà soát lần 2) |
| Trình duyệt thật (Playwright) | trạm cắt mạng → tải lại → ghi xe → có mạng tự gửi (mục 4.7); HTTPS 5443: TLS 1.3 + HTTP/2, chứng chỉ hợp lệ qua CA nội bộ trên `localhost` và IP LAN | đạt |

**Rà soát độc lập lần 2 (15/10/2026)** — một agent chỉ đọc mã, đối kháng, sau khi test đã xanh: 23 mục, kiểm chứng lại từng mục,
1 báo nhầm (hàng đợi email đã bật lúc khởi động), còn lại đã sửa:

| Nhóm | Lỗ hổng tìm ra | Đã sửa |
|---|---|---|
| Tiền | Lượt ghi trễ không kèm tiền mặt, xe không thẻ → ghi "Tiền mặt" = **tiền ảo** trong quỹ ca đã giao; nhập bù kiểu cũ trừ thẻ dù bảo vệ có thể đã thu phiếu tay; thẻ tháng xét theo ngày XỬ LÝ; lỗi thoáng qua tắt sổ cái thẻ 2 phút; "chưa có bản sao" xếp là lỗi vĩnh viễn → phí rơi vào hàng đợi lỗi | Hình thức **"Công nợ (chờ thu)"** (không vào tiền mặt phải nộp, có dòng riêng ở bảng giao ca); nhập bù kiểu cũ không trừ thẻ; xét hạn thẻ theo ngày xe qua; truy vấn bảng lỗi → ném lỗi (không nhớ "chưa có"); `ChuaCoBanSaoException` = lỗi tạm thời |
| Tuân thủ cổng | Máy chủ chậm 8 s → trạm xếp hàng, xe RA chở container đi qua đường ghi trễ (chỉ đối soát); bản sao thiếu eDO chỉ cảnh báo; biển số cấm máy chủ không kiểm; lùi giờ "vừa kịp hạn" eDO / lệnh không bị gắn cờ; xe RA mang container không khớp lệnh ghi `da-luu`; tiền mặt khai bất kỳ số nào | Trạm: XE RA mà máy chủ không trả lời kịp khi đang có mạng → **ĐỎ "CHƯA XÁC NHẬN"** (thử lại cùng khóa / "vẫn cho qua" có `vuotBanSao` → luôn đối soát); chế độ `chan` thiếu eDO / tờ khai trong bản sao → chặn mặc định; máy chủ kiểm biển cấm; kiểm tuân thủ thêm theo giờ NHẬN (hợp lệ theo giờ khai mà hết hạn lúc nhận → đối soát); xe RA mang container không khớp lệnh → đối soát; tiền mặt lệch biểu giá / thiếu phiếu / chiều vào / bất thường (> 5 triệu, không ghi quỹ) → đối soát |
| Quy trình | Bất kỳ nhân viên nào (kể cả người đã cho xe qua) đóng được "cần đối soát" | Chỉ **Admin**, và **không tự đối soát** lượt mình ghi / đồng bộ (nguyên tắc 4 mắt) |
| Nền tảng | `[ChongGuiLap]` không ràng thân yêu cầu; action chạy lâu / lưu kết quả lỗi → có thể chạy 2 lần; `/suc-khoe` lộ lỗi SQL, pid, bảng cho người ngoài; gateway đánh "ngừng" sau 2 lần chậm; phát lại Outbox không chặn tuổi | Băm thân (cột `BamThan`), cùng khóa khác thân → 422; gia hạn giữ chỗ định kỳ, lưu lỗi thì giữ chỗ + lưu nền; người ngoài chỉ nhận bản tóm tắt; thống kê Outbox nền (≤ 1,5 s, `NOLOCK`), ngưỡng 3 lần lỗi liên tiếp; chặn sự kiện cũ hơn hạn dọn inbox |
| Trạm ngoại tuyến | Chuyển hàng đợi cũ ở mỗi tab với khóa ngẫu nhiên → lượt + phí kép; Wi-Fi yếu kẹt mãi lô cũ nhất; giao diện không gửi Idempotency-Key; đăng xuất giữ bản sao nhạy cảm, 401/403 đánh lỗi cả lô | Khóa tất định giống máy chủ, chạy trong khóa giữa tab, xóa từng mục; thời gian chờ theo dung lượng + thu nhỏ lô; `khoaGuiLap.js` cho biểu mẫu (eDO, hải quan, lịch hẹn, lệnh, thẻ xe); đăng xuất xóa bản sao, giữ hộp thư đi, chờ đăng nhập lại |
| Khác | Gói ngoại tuyến cắt mất lệnh cần nhất; Take(200) trước khi lọc biển; tên ảnh trùng mili-giây; danh sách phục hồi đẩy bản lỗi lên đầu | Ưu tiên lệnh trong khung giờ + cờ `biCat`; lọc biển trong SQL; thêm Guid vào tên ảnh; sắp bản thường mới nhất trước |

Bài học vấn đáp: **test xanh chưa phải là xong** — cả hai vòng rà soát đều tìm ra lỗi thật mà test không bắt (vì test chỉ kiểm điều người viết đã
nghĩ tới). Mỗi lỗi sửa xong đều có test mới chứng minh.

Ngoài ra: chế độ gộp (bản somee) khởi động tốt, 256 đường dẫn API; trang `/admin/microservices`, `/admin/edo`,
`/admin/hai-quan`, `/admin/lich-hen`, `/admin/doi-chieu-container`, `/admin/ai-may-chu` đã mở thử qua gateway.
Nhóm `tich-hop` (luồng Cổng → Bãi → Tài chính trên CSDL thật, chạy trên `SmartPortDB_BaoCao`): **59/59** (09/10).

**Sửa lỗi sổ chứng từ "gãy chuỗi oan" (08/10):** hash chỉ dùng thời điểm tới **giây**, nhưng cột `NgayLap` kiểu SQL `datetime` làm tròn
1/300 giây → chứng từ lập lúc `14:31:46,999` được lưu `14:31:47,000`, tính lại hash không khớp và trang kiểm tra báo "bị phá" dù không ai
sửa (~0,2% chứng từ). Sửa: `ChungTuHelper.DenGiay` cắt về tròn giây trước khi tính hash + lưu. Chứng từ cũ bị dính được sửa bằng
`MicroServices/sua_chuoi_chung_tu_lam_tron.py` — **chỉ** khi hash lưu sẵn khớp chính xác lúc lùi 1 giây (bằng chứng mật mã là lỗi làm
tròn, không phải bị sửa); không đổi hash nào. Đã sửa 1 chứng từ (#272) trong `SmartPortMS_Cong`; `SmartPortDB`, `SmartPortDB_BaoCao`,
`SmartPortDB_KiemThu` kiểm ra nguyên vẹn.

**Sửa 3 lỗi THẤT THU phí lưu bãi (08/10)** — `LuuBaiService` (dùng chung cho bản gộp và dịch vụ Tài chính) + module Bãi:

| Lỗi | Hậu quả | Cách sửa |
|---|---|---|
| Container "Đã cấp rỗng" (lấy rỗng) quay lại bãi vẫn giữ **ngày vào cũ** | Phí đang chạy cộng cả những ngày container ở ngoài depot; lúc giao đi trả lại phiếu lượt trước → lượt sau không thu đồng nào | `DongBoContainerKhiXeQuaCong`: đặt lại `NgayVaoBai` khi container **không đang trong bãi** (`DangTrongBai`: đã vào, chưa ra, "Trong bãi"/"Chờ xuất tàu") |
| **Phiếu chốt giữa kỳ** (agent / nút Chốt / chốt kỳ) rồi container ở thêm | Gate-out thấy "đã có phiếu của kỳ" → trả phiếu cũ, các ngày sau không thu (thật: `MSCU2277456`, thiếu 19 ngày) | `TinhPhanChuaChot`: lập **phiếu bổ sung** = phí cả lượt − các phiếu đã lập; miễn 5 ngày chỉ tính 1 lần; đã chốt phủ hết thì trả phiếu cũ (vẫn chống chốt trùng) |
| So `TuNgay == NgayVaoBai` giữa cột SQL `datetime` (làm tròn 1/300 s) và tham số `datetime2(7)` của EF Core | Không thấy phiếu đã có → **8 luồng chốt cùng lúc ra 8 phiếu** (lỗi "chập chờn": chỉ đạt khi mili-giây chia hết cho 10) | `PhieuCuaLuot`: so `TuNgay ≥ NgayVaoBai − 1 giây` |

Kiểm chứng: nhóm `tich-hop` trên `SmartPortDB_BaoCao` (đã sao lưu trước) **55/55**; chạy lại với logic cũ thì **12/55 lỗi** (vd. container chốt giữa
kỳ: cũ thu 570.000đ, đúng phải 2.375.000đ). Bảng kê lưu bãi thêm cột "tiền chưa chốt" + nút **Bổ sung**; chốt kỳ lập cả phiếu bổ sung.

**Cổng — 2 quy tắc thêm khi rà sơ đồ UML (08–09/10)** (`DichVuCong.GhiLuotXeAsync`):
* Xe mang biển **khác** biển đã gán trên lệnh → vẫn ghi lượt nhưng **cảnh báo** "kiểm tra giấy tờ tài xế" (không chặn: AI có thể đọc
  sai 1 ký tự; lệnh tạo từ COPARN còn mang biển giả "CHUA-GAN"). So sau khi bỏ dấu: `51C-123.45` = `51C12345`.
* Lệnh đã **"Hoàn tất"** không được gắn lại cho lượt xe mới (trước đây dùng lại được → lấy container lần 2 bằng lệnh cũ) — lượt ghi không kèm lệnh + cảnh báo.
* Trợ lý AI đổi container sang "Đã ra bãi" (chuỗi không có trong danh mục) → nay dùng trạng thái chuẩn **"Đã giao khách"**, cảnh báo theo phần
  phí **chưa chốt** của lượt.

Kiểm thử tích hợp sau đợt này: **59/59**; toàn bộ 7 nhóm **359/359**.

**Đăng nhập Google / số điện thoại:** xem `Web/HUONG-DAN-DANG-NHAP-GOOGLE-SDT.md` (cách bật Gmail thật, Google OAuth,
Twilio + bảng an toàn để vấn đáp). Đăng ký mới là **saga**: Định danh tạo tài khoản + lệnh `kinh-doanh.khach-hang-can-tao`
→ Kinh doanh tạo doanh nghiệp → `kinh-doanh.khach-hang-da-tao` → Định danh gắn `MaKH`.

## 9. Câu hỏi vấn đáp hay gặp

**Vì sao không dùng giao dịch phân tán (2PC)?** Khóa tài nguyên trên nhiều CSDL, một bên chậm là cả hệ thống chờ, RabbitMQ không
tham gia 2PC. Dùng Outbox + sự kiện + xử lý idempotent → **nhất quán cuối cùng** nhưng không mất dữ liệu.

**RabbitMQ chết thì sao?** Dịch vụ vẫn nhận yêu cầu và ghi CSDL bình thường; sự kiện nằm chờ trong Outbox, RabbitMQ sống lại thì
bộ chuyển tiếp gửi tiếp. Không mất sự kiện.

**Một dịch vụ chết thì sao?** Tin nhắn cho nó nằm trong hàng đợi bền; khởi động lại thì xử lý tiếp từ chỗ dừng. Dịch vụ khác vẫn chạy
nhờ dữ liệu bản sao cục bộ.

**Làm sao không xử lý trùng?** Mỗi sự kiện có `Ma` (GUID); bảng `SuKienDaXuLy` ghi trong cùng giao dịch với việc xử lý.

**Nhất quán cuối cùng có nguy hiểm không?** Bản sao trễ cỡ trăm mili-giây. Chỗ cần chắc chắn (vd. cho xe ra) thì kiểm tra trên dữ
liệu chính của dịch vụ sở hữu quyết định đó (Cổng sở hữu lượt xe & lệnh).

**Mất Wi-Fi ở cổng thì xe có bị kẹt không?** Không. Trang chốt cổng là PWA: mở lại được khi mất mạng, bấm ghi là lưu vào IndexedDB của máy trạm
("🟡 ĐÃ GHI TẠM"), đối chiếu lệnh bằng bản sao tải trước; có mạng lại tự gửi bù, không trùng (mỗi thao tác 1 mã UUID). Hỏng hẳn → phiếu tay.

**Bảo vệ có lợi dụng "mất mạng" để cho xe ra không cần eDO / sửa giờ không?** Máy chủ **vẫn kiểm tuân thủ** khi nhận lượt ghi trễ: vi phạm thì vẫn
ghi (xe đã qua thật) nhưng gắn **cần đối soát** + sự kiện lên bảng tin; giờ được sửa theo độ lệch đồng hồ máy trạm, cũ hơn 72 giờ → đối soát;
phí vẫn thu; AI gian lận tính nguồn `NGOAI-TUYEN` là thao tác tay. Gửi `nguon = NGOAI-TUYEN` qua API thường vẫn bị chặn.

**Sao không dùng Background Sync của trình duyệt?** Chỉ Chrome có; Safari / Firefox không. Trạm cổng luôn mở trang nên tự đồng bộ theo nhịp
kiểm sức khỏe là đủ và chạy được mọi trình duyệt.

**Hai xe ra cùng lúc trừ cùng một thẻ thì sao?** Trừ bằng 1 câu SQL nguyên tử có điều kiện `SoDu >= gia` — không đọc-rồi-ghi; sổ cái chỉ-ghi-thêm
UNIQUE theo lượt xe nên 1 lượt chỉ trừ 1 lần (thử 40 lần trừ + 10 lần nạp song song: đúng từng đồng).

**Tại sao Python cho AI mà không gọi thẳng trong .NET?** Hệ sinh thái mô hình (Ultralytics, RF-DETR, PyTorch) ở Python; tách
thành dịch vụ riêng → GPU dùng chung, cập nhật mô hình không phải build lại .NET, dịch vụ khác gọi qua HTTP + JWT.

## 10. Hạn chế (nói thật khi được hỏi)

* Các CSDL tách riêng nhưng **chung một SQL Server** trên máy phát triển; RabbitMQ 1 nút (chưa cluster).
* Chưa có Kubernetes / service discovery — địa chỉ dịch vụ lấy từ `dich-vu.json`.
* Hải quan là **mô phỏng** (chưa kết nối VNACCS/VASSCM thật); dữ liệu nghiệp vụ là demo.
* Dịch vụ AI 1 GPU, suy luận tuần tự (khóa GPU) — tải lớn cần thêm bản sao dịch vụ AI sau gateway. Việc xử lý video giữ trong RAM
  của dịch vụ AI (30 phút) — khởi động lại dịch vụ là mất việc đang chạy (cần hàng đợi bền nếu làm thật).
* Nhận diện **chưa đạt 100%** (xem mục 7); ca không chắc chắn được chuyển kiểm tay thay vì đoán.
* **Một máy chủ tại cảng = điểm hỏng duy nhất**: đã có sao lưu / phục hồi, giám sát, phiếu tay — nhưng chưa chạy song song 2 máy (HA); làm thật cần
  UPS + máy dự phòng nạp bản sao lưu.
* Trạm mất mạng **chưa có ô nhập tiền mặt + số phiếu** (API đã nhận `tienMatTaiCong`, `soPhieuThu`) → hiện thu thẻ / công nợ khi đồng bộ, phiếu tay
  nhập bù. Màn camera AI `/admin/gate` và báo sự cố an ninh chưa dùng hộp thư đi ngoại tuyến (mới có trang chốt bảo vệ); chưa Background Sync.
* Gửi hóa đơn email (`SendInvoice`) vẫn gửi SMTP ngay trong yêu cầu (chưa qua hàng đợi); máy demo chạy `ASPNETCORE_ENVIRONMENT=Development`.
* Cảnh báo nguy hiểm (mục 7.2) mới kiểm trên video **dựng** từ ảnh thật + bộ dữ liệu công trường — cần quay thử camera cảng thật
  (đêm, mưa, ngược sáng) và gán nhãn vài trăm khung trước khi dùng vận hành.
