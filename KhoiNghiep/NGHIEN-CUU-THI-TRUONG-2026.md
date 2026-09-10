# SmartPort — Nghiên cứu thị trường & định vị cạnh tranh
*Tra cứu ngày 04/09/2026. Mọi số liệu đều kèm nguồn để dùng được khi thuyết trình.*

---

## 1. Ngành logistics Việt Nam đang đau ở đâu

| Số liệu | Nguồn |
|---|---|
| **90,5%** doanh nghiệp dịch vụ logistics mới đạt mức số hóa 1–2 (có website / cho tải biểu mẫu). Chỉ **1,9%** có khả năng phân tích dự báo, **0,4%** đạt mức tự động | Báo cáo Logistics Việt Nam 2024 — Bộ Công Thương, tr.122; dẫn lại tại quanlynhanuoc.vn (30/12/2025) |
| **53%** doanh nghiệp gặp khó vì **chi phí đầu tư công nghệ logistics hiện đại quá cao** | nt., tr.100 |
| **62%** doanh nghiệp bị ảnh hưởng bởi **thiếu nhân lực chất lượng cao** trong quản lý chuỗi cung ứng | nt. |
| *"Thiếu cơ chế liên kết hiệu quả giữa các phương thức vận tải với hệ thống kho bãi"* → đội chi phí | tapchinganhang.gov.vn (09/03/2026) |
| *"Khó khăn hiện nay không chỉ nằm ở công nghệ, mà còn ở chất lượng dữ liệu, **tính kết nối của hệ thống**, năng lực nhân lực…"* — PGS.TS. Nguyễn Thanh Chương, Chủ tịch VALOMA | VALOMA LogTech Forum 2026 (09/05/2026), logistics.gov.vn |
| Ngành tăng trưởng **14–16%/năm**; hơn **30.000 doanh nghiệp**, **phần lớn quy mô nhỏ** | nt. + lyluanchinhtri.vn (08/10/2025) |
| **42,9%** doanh nghiệp coi chi phí cước vận tải tăng đột biến là thách thức lớn nhất | daibieunhandan.vn (19/03/2026) |
| Áp lực **ESG, truy xuất carbon** buộc doanh nghiệp logistics phải đo phát thải | baochinhphu.vn (09/05/2026) |
| Chiến lược phát triển dịch vụ logistics 2025–2035 (QĐ **2229/QĐ-TTg** ngày 09/10/2025) nhấn mạnh *"hạ tầng logistics đồng bộ, hiện đại… hạ tầng số"* | Thủ tướng Chính phủ |

### Ba câu chốt dùng khi pitch

1. **Thị trường đủ lớn và đang tăng**: 30.000+ doanh nghiệp, tăng 14–16%/năm.
2. **Nỗi đau đã được Nhà nước gọi tên**: *thiếu cơ chế liên kết* + *90,5% mới số hóa mức 1–2*.
3. **Rào cản là GIÁ, không phải nhu cầu**: 53% muốn nhưng không mua nổi phần mềm hiện có
   → đây chính là khe hở của SmartPort.

---

## 2. Đối thủ và khoảng cách

### 2.1 GTEL OTS — PSafe-ACS (Việt Nam, đối thủ trực tiếp nhất)
Giải pháp kiểm soát vào ra cho cảng / ICD / kho logistics. *(ots.vn)*

| Tính năng | GTEL OTS | SmartPort |
|---|:--:|:--:|
| ALPR biển số (>95%, <1s) | ✅ | ✅ |
| Nhận diện số hiệu container | ✅ | ✅ |
| Phân loại xe / container bằng AI | ✅ | ✅ |
| Cổng đăng ký từ xa + duyệt | ✅ | ✅ (cổng khách + TAS) |
| App tài xế, QR check-in | ✅ | ✅ (PWA + app MAUI) |
| Nhật ký chống gian lận | ✅ | ✅ |
| Dự phòng khi mất mạng | ✅ | ✅ (hàng đợi offline) |
| **API nối WMS / TMS / kế toán** | ✅ | ✅ **(04/09/2026)** |
| **Thu phí ngay tại cổng** (lượt / tháng / trả trước) | ✅ | ✅ **(04/09/2026)** |
| **Quét CCCD / OCR bằng lái để đăng ký nhanh** | ✅ | ✅ **(04/09/2026)** — dùng **mã QR** thẻ chip, chính xác hơn OCR |
| **Đếm xe qua cổng đối chiếu chống gian lận** | ✅ | ✅ **(04/09/2026)** |
| **Một dòng thời gian cho container** | ❌ (thiên về kiểm soát cổng) | ✅ **lợi thế** |
| **Cảnh báo bằng lái hết hạn** | — | ✅ **lợi thế** |
| Giá | thiết bị + triển khai, thường trăm triệu | thấp, chạy trên camera có sẵn |

**Đến 04/09/2026, SmartPort đã phủ hết bảng tính năng của GTEL OTS**, cộng thêm hai thứ họ không có:
dòng thời gian container và cảnh báo bằng lái hết hạn.

### 2.2 ContainerHub / ContPark / DepotSystems (quốc tế, depot container rỗng)

| Tính năng | Quốc tế | SmartPort |
|---|:--:|:--:|
| Gate in/out gắn với tồn bãi | ✅ | ✅ **(vừa bổ sung)** |
| Gate – bãi – M&R **dùng chung một dòng thời gian** | ✅ | ✅ **(vừa bổ sung)** |
| M&R / EOR | ✅ | ✅ |
| Cổng thông tin cho hãng tàu / khách | ✅ | ✅ |
| **EDI hãng tàu (CODECO / COPARN)** | ✅ | ✅ **(04/09/2026)** — EDIFACT D:95B thật, có bản dịch tiếng Việt để kiểm tra |
| **EDI COARRI (dỡ/xếp tàu)** | ✅ | ✅ **(05/09/2026)** — có đoạn TDT mô tả tàu + số IMO |
| **Storage clock → hóa đơn lưu bãi tự động** | ✅ | ✅ **(04/09/2026)** |
| Giá tham chiếu | **199 – 999 USD/tháng** (~5 – 25 triệu đ) | định giá dưới mức này |

---

## 3. Định vị SmartPort

> **"Phần mềm điều hành depot/ICD cho doanh nghiệp vừa và nhỏ Việt Nam —
> chạy trên camera sẵn có, giá bằng 1/5 giải pháp ngoại, và mọi dữ liệu
> nối liền nhau trên một dòng thời gian."**

Ba điểm tựa, mỗi điểm đều gắn thẳng vào một số liệu ở mục 1:

| Điểm tựa | Trả lời cho nỗi đau nào |
|---|---|
| **Giá thấp, dùng camera có sẵn, không bắt mua thiết bị mới** | 53% kêu chi phí công nghệ quá cao |
| **Liên kết & đồng bộ toàn hệ thống + API mở** | "thiếu cơ chế liên kết" — Ngân hàng Nhà nước & VALOMA |
| **Dễ dùng: màn hình bảo vệ 2 nút, tìm kiếm Ctrl+K, không cần đào tạo dài** | 62% thiếu nhân lực chất lượng cao |
| **AI không chỉ nhìn — AI tự làm việc, có nhật ký và nút hoàn tác** | Định vị AI của cả ngành đang dừng ở nhận diện (câu chuyện 2020) |

---

### 3.1 Điểm yếu đã tự chỉ ra — và đã vá

Khi soi lại SmartPort cạnh các startup logistics đang gọi vốn năm 2026, điểm yếu
lớn nhất **không nằm ở tính năng mà ở định vị**:

> **"AI của bạn chủ yếu là nhận diện — đọc biển số, đếm container. Đó là câu
> chuyện của 2020. Quỹ 2026 đang tài trợ cho agent TỰ LÀM VIỆC."**

Đây là phản biện đúng, và nó đắt: nhận diện đã thành hàng hoá phổ thông, ai cũng
gắn được. Cái chưa phổ thông là **để máy tự đụng vào nghiệp vụ** — mà không ai
dám, vì máy làm sai thì mất tiền thật.

**SmartPort trả lời bằng Agent tự hành động** (ngày 05/09/2026): 7 kỹ năng, mỗi
kỹ năng **gọi thẳng nghiệp vụ của hệ thống** chứ không dừng ở gợi ý.

| Kỹ năng | Agent tự làm gì (hành động thật) |
|---|---|
| Chốt phí lưu bãi | **Lập phiếu thu** có số, có mã QR — không phải "đề xuất lập phiếu" |
| Sinh CODECO | **Tạo bản tin EDI** báo hãng tàu container đã vào/ra bãi |
| Nhắc nợ quá hạn | Dựng danh sách phiếu quá hạn kèm số tiền để đòi |
| Cảnh báo thẻ xe | Bắt thẻ sắp/đã hết hạn **trước khi xe tới cổng** |
| Giục duyệt EOR | Không để phiếu sửa chữa nằm chết trong hàng đợi |
| Cảnh báo tồn lâu | Chỉ ra vốn đang chết ngoài bãi |
| Chặn bằng lái hết hạn | **Tự mở sự cố an ninh** cho tài xế không đủ điều kiện |

**Ba thứ khiến chủ depot dám bật chế độ "Tự làm"** — thiếu một cái là không ai bật:

1. **Lý do đọc được.** Mỗi việc ghi rõ *vì sao*, không phải "AI quyết định vậy".
2. **Hoàn tác được.** Nhật ký lưu bảng + khóa của bản ghi đã tạo; bấm một nút là gỡ.
   Riêng phiếu thu thì **chuyển sang "Hủy" chứ không xóa** — chứng từ đã đánh số
   phải còn dấu vết, kế toán không chấp nhận biến mất không lý do.
3. **Ba chế độ cho từng kỹ năng:** Tắt · Xin phép · Tự làm (mặc định *Xin phép*),
   cộng trần `tối đa mỗi lần` để agent lỗi không thể tạo hàng trăm bản ghi.

**Quyết định bằng luật, không bằng mô hình ngôn ngữ — đây là lựa chọn có chủ ý.**
Agent này đụng tới tiền của khách và báo cáo gửi hãng tàu; mỗi đồng phải giải
thích được trước kiểm toán. Một mô hình sinh văn bản không đưa ra được bảo đảm đó.

**Câu chốt mới khi pitch:**

> *"Phần mềm khác báo cho anh biết có 22 container đang nằm bãi.
> Phần mềm này **tự lập 19 phiếu thu**, ghi rõ vì sao lập từng phiếu,
> và nếu anh không đồng ý thì bấm hoàn tác. Anh chọn cho nó xin phép
> hay để nó tự làm."*

---

## 4. Việc còn phải làm (xếp theo mức sinh tiền)

### ✅ Đã hoàn tất trong ngày 04/09/2026

| # | Việc | Kết quả |
|---|---|---|
| 1 | **Liên kết & đồng bộ toàn hệ thống** | Hồ sơ container 360°, hồ sơ xe, tìm kiếm Ctrl+K, đồng bộ trạng thái khi qua cổng |
| 2 | **API tích hợp mở** | `/api/v1/` — 7 endpoint, khóa phân quyền theo khách hàng, tài liệu in được |
| 3 | **Đồng hồ lưu bãi → phiếu thu** | Bảng kê công nợ, chốt kỳ, phiếu thu song ngữ + QR VietQR, tự chốt khi gate-out |
| 4 | **EDI hãng tàu** | CODECO (sinh) + COPARN (nhận → tự lập lệnh), UN/EDIFACT D:95B thật |
| 5 | **Thu phí tại cổng** | Biểu giá sửa được · thẻ tháng · thẻ trả trước trừ dần · bảng giao ca in ký nhận |
| 6 | **Đếm xe chống gian lận** | Đối soát bộ đếm độc lập, lệch > 5 tự mở sự cố, ước tính tiền thất thoát, buộc giải trình |
| 7 | **Quét CCCD / OCR bằng lái** | Mã QR thẻ chip (chính xác tuyệt đối) + OCR bằng lái, cảnh báo bằng hết hạn |

**SOP: 64 → 87 chức năng.** Kiểm chứng bằng **242 test tự động**, đối chiếu số dòng/số tiền trong CSDL.

### ✅ Hoàn tất nốt ngày 05/09/2026

| # | Việc | Kết quả |
|---|---|---|
| 8 | **EDI COARRI** | Báo cáo dỡ/xếp container theo chuyến tàu — có đoạn `TDT` mô tả tàu + số IMO, `LOC+11`/`LOC+9` phân biệt cảng dỡ/cảng xếp |
| 9 | **API cho thiết bị đếm xe thật** | `POST /api/v1/dem-xe/xung` nhận tín hiệu cảm biến vòng từ (gửi lô tối đa 500, **chống gửi trùng** bằng mã xung của thiết bị); `POST /api/v1/dem-xe/chot` tự cộng xung — **không ai gõ tay số đếm nữa** |
| 10 | **Backport toàn bộ sang MVC5** | Đưa bản MVC5 lên ngang bản .NET 10 — sau đó **dừng nuôi bản MVC5** (05/09/2026) để chỉ còn một codebase: mỗi tính năng chỉ phải làm một lần |
| 11 | **Agent TỰ HÀNH ĐỘNG** | 7 kỹ năng gọi thẳng nghiệp vụ · 3 chế độ Tắt/Xin phép/Tự làm · nhật ký có lý do + người chạy · hoàn tác từng việc · chạy nền 30 phút (bản .NET 10) hoặc `POST /api/v1/agent/chay` cho bộ hẹn giờ ngoài (bản MVC5) |

**SOP: 64 → 99 chức năng.** Kiểm chứng bằng **425 test tự động** trên cả hai bản (325 test bản .NET 10 + 100 test bản MVC5; riêng Agent: 39 + 58, khẳng định trên số dòng và số tiền trong CSDL chứ không trên chuỗi màn hình).

### Còn lại (không phải việc code)

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Mua và lắp cảm biến đếm xe | Phần mềm đã sẵn sàng nhận tín hiệu — chỉ cần thiết bị POST vào API |
| 1b | Đặt bộ hẹn giờ gọi `/api/v1/agent/chay` trên bản somee | MVC5 không có tiến trình chạy nền bền — app pool bị recycle khi vắng khách. Dùng Windows Task Scheduler hoặc cron của hosting, gõ 30 phút/lần bằng khóa API **quyền Ghi** |
| 2 | Đổi mật khẩu SQL trên somee | Đang lộ trong Web.config |
| 3 | Đăng ký pháp nhân để xuất hóa đơn | Thủ tục |

### Câu chốt bán hàng mới có được (dùng khi pitch)

> *"Anh đang có 22 container nằm ngoài bãi. Phần mềm nói ngay: **139 triệu đồng tiền lưu bãi**
> đang treo, trong đó **126 triệu chưa ai lập phiếu**. Bấm một nút là ra 19 phiếu thu có mã QR,
> gửi khách quét là chuyển tiền."*

Đó là con số **phần mềm tự sinh từ dữ liệu cổng**, không phải bảng Excel ai đó gõ tay.

**Ngoài code (phần bạn phải tự làm):** đăng ký pháp nhân để xuất hóa đơn ·
đi thăm depot thật để lấy số đo trước/sau · gắn camera thật đo độ chính xác ALPR ·
gỡ ràng buộc giấy phép AGPL của Ultralytics · đổi mật khẩu SQL trên somee.
