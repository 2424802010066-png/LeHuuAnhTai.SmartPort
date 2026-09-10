# KHAO SAT CAC CANG VIET NAM (26/08/2026)

### BAO CAO SGN
# BÁO CÁO NGHIÊN CỨU ĐỐI THỦ: CỤM TÂN CẢNG SÀI GÒN (SNP)

Phạm vi: Cát Lái, Cái Mép (TCIT/TCTT/TCCT), Tân Cảng Hiệp Phước, các ICD vệ tinh. Trọng tâm: tính năng số NGOÀI ePort/eDO đã biết, app tài xế, xếp hàng/gọi xe, cảng xanh, giải pháp 2024–2026.

---

## 1. TỔNG QUAN HỆ SINH THÁI SỐ eSNP (áp dụng toàn cụm)

Vận hành bởi: Tổng công ty Tân Cảng Sài Gòn (SNP) — doanh nghiệp quân đội, nhà khai thác cảng container số 1 VN (~50% thị phần toàn quốc, ~90% khu vực TP.HCM).

SNP đã triển khai **hơn 30 phần mềm** trong hệ sinh thái eSNP — đây là "trần tham chiếu" cao nhất ở VN mà SmartPort nên soi vào:

| Hệ thống | Chức năng | SmartPort đã có? |
|---|---|---|
| **TOPX / TOPOVN** | TOS lõi điều hành khai thác (TOPOVN do SNP tự phát triển thay TOPX của RBS Úc) | Một phần (TOS + yard map) |
| **ePort + eDO + eBooking** | Cảng điện tử, lệnh giao hàng điện tử, booking hạ rỗng nối thẳng hãng tàu | ĐÃ CÓ |
| **Autogate** | Cổng thông minh OCR biển số ~99%, đối chiếu tự động | ĐÃ CÓ (ALPR 3 tầng) |
| **OTM (GPS tracking)** | Giám sát hành trình xe/sà lan realtime toàn chuỗi | CHƯA — SmartPort mới có DMS tài xế, chưa track GPS realtime |
| **eLogs** | Quản lý vận tải (TMS) cho đội xe | CHƯA đầy đủ |
| **eWMS** | Quản lý kho điện tử (kể cả kho ngoại quan, CFS, kho lạnh) | CHƯA |
| **ECM** | Quản lý container rỗng liên depot | CHƯA |
| **CHB** | Phần mềm khai báo hải quan tích hợp | CHƯA |
| **CRM + eOffice** | Quan hệ khách hàng + văn phòng điện tử nội bộ | CHƯA |
| **BI trên Big Data** | Báo cáo điều hành thông minh cho ban lãnh đạo | Một phần (báo cáo MISA) |
| **Trợ lý ảo Pi (AI/NLP)** | Chatbot phản hồi 100% yêu cầu trên website, Fanpage, **Zalo**; xử lý ~90% yêu cầu khách | ĐÃ CÓ chatbot (nhưng chưa đa kênh Zalo) |
| **AI an ninh cảng** | Phân tích camera an ninh bằng AI | ĐÃ CÓ |
| **VR360** | Tham quan cảng ảo | CHƯA (nice-to-have) |

Kết quả công bố giai đoạn 2021–2025: **tiết kiệm ~600 tỷ đồng**, doanh thu tăng bình quân 7,5%/năm, thủ tục từ 3 ngày rút còn vài phút, tiết kiệm 30.000–50.000 tờ giấy/ngày.

---

## 2. TÂN CẢNG CÁT LÁI (TP.HCM) — cảng container lớn nhất VN

**Tính năng số đáng chú ý (ngoài ePort/eDO):**

1. **Check-in online BẮT BUỘC trước 2 giờ**: từ 27/3/2023, Cát Lái chỉ tiếp nhận xe container đã hoàn tất check-in online trước khi đến cổng ít nhất 2 tiếng — qua web ePort hoặc app SNP ePort Mobile. Đây chính là cơ chế "xếp hàng ảo/điều tiết lưu lượng": tài xế biết trước lô hàng đủ điều kiện hay chưa, thiếu thủ tục gì thì bổ sung từ xa. Kết quả: thời gian xe chờ tại cổng giảm từ **13 phút xuống 6 phút**.
2. **App SNP ePort Mobile (ra mắt 15/8/2022)** — app cảng điện tử đầu tiên VN, các tính năng cho tài xế vượt trội hơn bản web:
   - Check-in online + **phiếu Check-in điện tử** kiểm tra điều kiện giao nhận container trước khi vào cảng;
   - **OCR đọc CMND/CCCD** khi đăng ký tài khoản check-in tài xế;
   - **Tích hợp Google Maps ước tính giờ đến cảng (ETA)**;
   - Chatbot FPT.AI hỗ trợ ngay trong app; thông báo đẩy (chính sách, thậm chí chúc mừng sinh nhật tài xế);
   - Xác thực eDO, thanh toán, khai báo, khai đơn vị vận tải, đăng ký tàu xuất, **đăng ký cắt seal**, khai VGM, tải hóa đơn điện tử.
3. **Cổng C tự động (Autogate hàng lẻ) — go-live 10/5/2024** do Kho Vận Tân Cảng vận hành: OCR biển số chính xác ~99%; từ 5/2024 **bắt buộc đăng ký trước thông tin phương tiện + tài xế trên eWMS** (mục Quản lý tài xế / Quản lý phương tiện) mới được qua cổng. → Tức là Autogate đã phủ cả xe hàng rời <15 tấn, không chỉ xe container.
4. **Kết nối trực tiếp CSDL hải quan** (VASSCM): thông quan tự động, ePort tự đối chiếu trạng thái tờ khai — SmartPort chưa có lớp kết nối cơ quan nhà nước này.

**Cảng xanh Cát Lái** (đạt Green Port Award APSN 2017 — đầu tiên VN):
- Điện mặt trời áp mái: 24.500m² tại Cát Lái (toàn hệ thống SNP 432.500m², ~80 MW/năm), kế hoạch lắp thêm 20.000m² năm 2025;
- **Trạm điện bờ (shore power) 2 MW cho tàu, vận hành 2025** — điểm rất đáng chú ý;
- Chuyển đổi thiết bị diesel → điện: 5 đầu kéo điện + 2 xe buýt điện dự kiến 2026; hoán cải cẩu sang điện/nhiên liệu sạch;
- Giảm phát thải gián tiếp nhờ số hóa (bớt xe chờ nổ máy, bớt giấy tờ).

---

## 3. CỤM CÁI MÉP: TCIT / TCTT / TCCT (Bà Rịa – Vũng Tàu)

- **TCIT** (liên doanh SNP + MOL, Wan Hai, Hanjin/KLine): đạt **Green Port Award APSN 2020** (cảng thứ 2 VN sau Cát Lái). Đã triển khai **e-Port cho 100% khách hạ container rỗng**; đón dịch vụ EC3 của liên minh Premier Alliance (ONE–HMM–Yang Ming) từ 2/2025. Dùng eRTG (RTG điện) trong danh mục cảng xanh.
- **TCTT**: đang thí điểm mô hình đáng chú ý nhất khu vực — **"cảng mở" (open port)**: kết nối cầu bến TCTT + CMIT liền kề thành **bến chung 1.200m đón 3 tàu mẹ cùng lúc**, xe chở hàng chạy qua **cổng kết nối nội bộ giữa 2 cảng không cần thủ tục hải quan lặp lại** — được Bộ GTVT, Cục Hàng hải, Tổng cục Hải quan ủng hộ; định hướng tiến tới free trade zone gắn cụm Cái Mép – Thị Vải. → Về phần mềm, điều này đòi hỏi liên thông dữ liệu lệnh/container GIỮA CÁC CẢNG — SmartPort chưa có khái niệm multi-terminal liên thông.
- SNP đã cam kết **hoàn thiện ePort đồng bộ cho Cái Mép, Hiệp Phước và Lạch Huyện (HICT)** — chuẩn hóa một nền tảng cho mọi terminal.
- Đặc thù Cái Mép: luồng hàng **trung chuyển sà lan** từ TP.HCM/ĐBSCL — thủ tục chuyển cửa khẩu bằng sà lan khai trên VNACCS, tra cứu tên tàu trên ePort để hạ đúng cảng. SmartPort chưa có module quản lý sà lan (barge management).

---

## 4. TÂN CẢNG HIỆP PHƯỚC (TCHP) + ICD VỆ TINH

- **TCHP**: nằm trong lộ trình phủ ePort toàn hệ thống; vai trò cảng vệ tinh gom hàng cho Cái Mép với cam kết SLA số hóa: 100% hàng nhập đích ICD Tân Cảng Nhơn Trạch được chuyển trong vòng **36 giờ** sau khi tàu cập TCHP.
- **ICD Tân Cảng Long Bình** (Đồng Nai, 105ha): kho ngoại quan, CFS, kho lạnh, trung tâm phân phối — vận hành trên **eWMS**; 353.000m² điện mặt trời áp mái (lớn nhất hệ thống).
- **ICD Tân Cảng Sóng Thần** (Bình Dương): kho 5.000–50.000m², depot rỗng, 55.000m² điện mặt trời; dịch vụ "cảng đích" (port destination) — thông quan tại ICD thay vì tại cảng biển.
- Mô hình đáng học: ICD vệ tinh + cảng biển dùng **chung một tài khoản ePort/ECM**, điều chuyển rỗng liên depot tự động — SmartPort mới quản lý 1 cảng đơn lẻ.

---

## 5. GIẢI PHÁP MỚI 2024–2026 & HƯỚNG NGHIÊN CỨU CỦA SNP

1. **Cổng C Autogate hàng lẻ** (5/2024) — mở rộng tự động hóa ra ngoài container.
2. **Trạm điện bờ 2 MW Cát Lái** (2025) + đầu kéo điện (2026) — cảng xanh giai đoạn 2.
3. **IoT giám sát nhiệt độ container lạnh** — đề án đang nghiên cứu chính thức của SNP. → SmartPort có thể đi trước: hồ sơ container 360 đã có, thêm dashboard reefer monitoring là điểm cộng lớn.
4. **Tự động hóa từng khâu dây chuyền sản xuất cảng** (đang nghiên cứu, tham chiếu cảng bán tự động TC-HICT Lạch Huyện).
5. **"Cảng mở" Cái Mép + định hướng free trade zone** — liên thông thủ tục đa cảng.
6. **Trợ lý ảo Pi phủ đa kênh** (web/Fanpage/**Zalo**) — kênh Zalo rất hợp bối cảnh VN.
7. Super App: SNP tuyên bố lộ trình gom mọi tiện ích vào **một Super Mobile App eSNP** duy nhất.

---

## 6. GỢI Ý CHO SMARTPORT (khoảng trống so với SNP)

**SNP có mà SmartPort CHƯA có — nên cân nhắc bổ sung:**
1. **Check-in online bắt buộc trước N giờ + phiếu check-in điện tử** (điều tiết lưu lượng cổng — SmartPort mới có check-in tại xe, chưa có "cửa sổ 2 giờ" và chặn xe chưa đủ điều kiện từ xa);
2. **OCR CCCD khi đăng ký tài xế** + ETA bằng bản đồ (DMS tài xế của SmartPort có thể nâng cấp 2 ý này ngay);
3. **GPS tracking realtime (kiểu OTM)** cho xe/sà lan sau khi rời cổng;
4. **Kết nối hải quan điện tử (VASSCM/VNACCS)** — dù ở mức mô phỏng, đây là điểm "ăn tiền" khi demo;
5. **Quản lý container rỗng liên depot (ECM) + eWMS kho CFS/ngoại quan**;
6. **IoT reefer monitoring** — SNP mới nghiên cứu, SmartPort làm trước được;
7. **Chatbot đa kênh Zalo** + thông báo đẩy cho tài xế;
8. **Module cảng xanh**: dashboard đo CO2 tiết kiệm nhờ eDO/check-in (SNP dùng con số "13 phút → 6 phút", "30–50 nghìn tờ giấy/ngày" làm truyền thông — SmartPort nên có KPI tương tự tự sinh từ dữ liệu);
9. **Shore power / điện mặt trời**: thêm phân hệ giám sát năng lượng vào báo cáo BI;
10. **Đa terminal + liên thông "cảng mở"**: kiến trúc cho phép 2 cảng chia sẻ lệnh giao nhận.

**SmartPort đã có mà ngang/hơn SNP công bố:** VietQR thanh toán tức thời, AI gợi ý vị trí bãi (SNP chưa công bố tính năng tương đương), K-Means/AI Lab, kiểm đếm AI, phí lưu bãi tự động theo hồ sơ container 360, AI Agent.

---

## NGUỒN CHÍNH
- [SNP — Ra mắt app SNP ePort Mobile](https://saigonnewport.com.vn/tin-tuc/pdf/hoat-dong-kinh-doanh/chinh-thuc-ra-mat-ung-dung-di-dong-snp-eport-cua-tan-cang-sai-gon.html) · [Thanh Niên](https://thanhnien.vn/tan-cang-sai-gon-ra-mat-ung-dung-di-dong-snp-eport-1851488821.htm) · [VPA](https://www.vpa.org.vn/vn/chinh-thuc-ra-mat-ung-dung-di-dong-snp-eport-cua-tan-cang-sai-gon/)
- [VnExpress — Xe container check-in online trước khi vào Cát Lái](https://vnexpress.net/xe-container-check-in-online-truoc-khi-vao-cang-cat-lai-4583039.html) · [Phiếu Check-in ePort](https://eport.saigonnewport.com.vn/CheckIn)
- [tthqsaigon — Bắt buộc đăng ký phương tiện/tài xế qua Cổng C từ 5/2024](https://tthqsaigon.net/dang-ky-thong-tin-phuong-tien-tai-xe-qua-cong-c/) · [SNP — Vận hành Autogate Cổng C](https://saigonnewport.com.vn/tin-tuc/pdf/hoat-dong-kinh-doanh/cong-ty-kho-van-tan-cang-van-hanh-thanh-cong-he-thong-cong-tu-dong-tai-cong-c-cang-tan-cang-cat-lai.html)
- [Báo Hải Quân VN — CCHC & chuyển đổi số 2021–2025 tiết kiệm ~600 tỷ](https://baohaiquanvietnam.vn/tin-tuc/tan-cang-sai-gon-cong-tac-cai-cach-hanh-chinh-va-chuyen-doi-so-giai-doan-2021-2025-da-tiet-kiem-gan-600-ty-dong) · [Tiên phong số hóa khai thác cảng](https://baohaiquanvietnam.vn/tin-tuc/tan-cang-sai-gon-tien-phong-ve-so-hoa-trong-quan-ly-khai-thac-cang-bien)
- [SNP — Hệ sinh thái logistics xanh](https://saigonnewport.com.vn/en/article/maritime-news/saigon-newport-creating-the-green-logistics-ecosystem-bridging-global-sustainability.html) · [Báo Xây Dựng — Mô hình cảng xanh](https://baoxaydung.vn/mo-hinh-cang-xanh-phat-trien-kinh-te-gan-voi-bao-ve-moi-truong-192251028170413946.htm)
- [TCIT — e-Port 100% khách hạ rỗng](https://www.tcit.com.vn/vi/news/thong-bao-trien-khai-e-port-doi-voi-100-khach-hang-ha-container-rong-tai-tcit.html) · [TCIT Green Port 2020 (APSN)](https://tcit.com.vn/en/article/operation-news/tan-cang-cai-mep-international-terminal-tcit-awarded-green-port-2020-by-the-apec-ports-service-network-apsn-council.html) · [TCIT & Premier Alliance 2025](https://saigonnewport.com.vn/en/article/operation-news/tan-cang-cai-mep-international-terminal-tcit-a-trusted-destination-for-the-premier-alliance.html)
- [Saigon Times — Kết nối TCTT–CMIT (cảng mở)](https://thesaigontimes.vn/ket-noi-hai-cang-bien-lien-ke-tai-cai-mep-thi-vai/) · [logistics.gov.vn — Hoàn thiện ePort cho Cái Mép, Hiệp Phước, HICT](https://logistics.gov.vn/dich-vu-logistics/dich-vu-khac/tan-cang-sai-gon-se-hoan-thien-eport-cho-cac-cang-cai-mep-hiep-phuoc-va-lach-huyen-hict)
- [SNP — Hướng đến hệ sinh thái eSNP](https://saigonnewport.com.vn/tin-tuc/tin-tuc-chuyen-nganh/tcsg-day-manh-chuyen-doi-so-huong-den-he-sinh-thai-esnp.html) · [Tiền Phong — Hành trình chuyển đổi số toàn diện](https://tienphong.vn/tan-cang-sai-gon-hanh-trinh-chuyen-doi-so-toan-dien-post1550460.tpo)
- [ICD Tân Cảng Long Bình](http://icdlongbinh.com/) · [ICD Tân Cảng Sóng Thần](https://www.icdsongthan.com.vn/) · [TCHP](https://saigonnewport.com.vn/en/port-company/tan-cang-hiep-phuoc-terminal-tchp)

### BAO CAO HP
# BÁO CÁO NGHIÊN CỨU ĐỐI THỦ — CỤM CẢNG HẢI PHÒNG (phục vụ định hướng SmartPort)

Phạm vi: Cảng Hải Phòng (Tân Vũ, Đình Vũ, Hoàng Diệu/Chùa Vẽ), Lạch Huyện (TC-HICT bến 1-2, bến 3-4 TIL, bến 5-6 Hateco HHIT), Nam Đình Vũ (Gemadept), VIP Green Port (Viconship). Trọng tâm: smart gate, xếp hàng/đặt lịch xe điện tử, ePort riêng, tính năng số độc đáo.

---

## 1. CẢNG HẢI PHÒNG — PHP (thuộc VIMC) — Chi nhánh Tân Vũ, Đình Vũ, Hoàng Diệu-Chùa Vẽ

**Vận hành bởi:** CTCP Cảng Hải Phòng (Tổng công ty Hàng hải VN - VIMC nắm chi phối).

**Tính năng số đáng chú ý:**
- **Smart Gate tại Tân Vũ** — cảng ĐẦU TIÊN khu vực có cổng giao nhận tự động: thử nghiệm cuối 2022, chính thức 01/03/2023, BẮT BUỘC 100% khách hàng từ 01/05/2023, áp dụng cho 10 làn xe, phủ đủ nghiệp vụ (giao xuất thẳng, giao hàng nhập, giao/hạ rỗng, hạ hàng xuất).
- **Con số then chốt: thời gian làm thủ tục tại cổng giảm từ 3-5 phút/xe xuống còn 10-25 GIÂY/xe** (hệ thống tự nhận dạng mã container OCR, biển số đầu kéo + rơ-moóc, chụp và lưu ảnh bề mặt container).
- Tích hợp chuỗi: **PL-TOS + ePort + app Container Checker** (kiểm tra tự động) + cảm biến điều khiển **barrier tự động**; thông tin giao nhận đẩy qua **bảng LED tại cổng + app Container Driver** trên điện thoại tài xế (phiếu vị trí Position tự động).
- **eEIR (phiếu giao nhận điện tử) gửi tự động qua app** cho tài xế → giao nhận "không giấy tờ, không tiếp xúc".
- **ePort Cảng Hải Phòng** (haiphongport.com.vn/vi/ep/eport): đăng ký dịch vụ trực tuyến, tra cứu eDO, tra cứu EBK/hóa đơn điện tử, **thanh toán online qua VNPAY, ví điện tử và tài khoản ngân hàng cá nhân** — ePort đã thành chuẩn vận hành bắt buộc tại các bến của PHP.
- **HaiphongPort App** (mobile, triển khai 21/11/2025): rút hàng, đóng hàng, phân xe vận chuyển, tra cứu dịch vụ container.
- Đang **nhân rộng Smart Gate sang Hoàng Diệu-Chùa Vẽ** (chạy thử 30 ngày từ 01/10/2025) — lộ trình phủ toàn bộ chi nhánh.
- Lưu ý: có một "Cảng Đình Vũ" khác (CTCP Đầu tư & Phát triển Cảng Đình Vũ - DVP) có ePort riêng eport.dinhvuport.com.vn: eDO/eBooking, thanh toán chuyển khoản QR.

---

## 2. TC-HICT — LẠCH HUYỆN BẾN 1-2

**Vận hành bởi:** liên doanh Tân Cảng Sài Gòn (SNP) 51% + MOL Nhật 17,5% + Wan Hai Đài Loan 16,5% + Itochu Nhật 15%. Cảng nước sâu đầu tiên miền Bắc, đón tàu ~14.000 TEU.

**Tính năng số đáng chú ý:**
- **ePort riêng** (eport.hict.net.vn): tra cứu container, tra cứu tàu/chuyến, **thanh toán dịch vụ online, tải + chuyển đổi hóa đơn điện tử**, đăng ký giao nhận container; kèm eDO với hãng tàu.
- **AI AutoGate (HICT SmartHub)**: từ **15/06/2025 áp dụng 100% Cổng Tự Động** cho tác nghiệp hạ bãi chờ xuất — tài xế bắt buộc dùng cổng tự động. Tham khảo mô hình AutoGate (đơn vị trong hệ VIMC đạt 25-30 giây/xe so với 3-4 phút trước đây).
- **Hệ app riêng cho hệ sinh thái vận tải** (phát triển bởi SmartHub Logistics Technology):
  - **App "HICT Lái Xe"**: tài xế nhận lệnh điều xe qua app trước khi đến cảng, bấm "bắt đầu" để kích hoạt quy trình qua cổng.
  - **App + website "HICT Điều Xe"** (dieuxe.hict.net.vn, ra mắt 10/02/2025): công cụ ĐIỀU PHỐI cho doanh nghiệp vận tải — quản lý lệnh điều xe, tăng năng suất đội xe. Đây là điểm khác biệt: cảng cấp luôn công cụ dispatch cho nhà xe.
  - **Chuẩn hóa mã định danh tài xế "số BAT 5 chữ số"** — điều kiện bắt buộc để qua cổng tự động.

---

## 3. BẾN 3-4 LẠCH HUYỆN — TIL CẢNG HẢI PHÒNG

**Vận hành bởi:** Công ty TNHH TIL Cảng Hải Phòng (thành lập 20/12/2024): PHP 51% + Terminal Investment Limited (TIL — nhà khai thác cảng của hãng tàu MSC) 49%.

**Điểm đáng chú ý:**
- Đón tàu thử nghiệm đầu tiên của MSC (M/V Greenville, 93.787 DWT, ~2.043 TEU) ngày 05/04/2025; công suất thiết kế 1,1 triệu TEU/năm, đón tàu mẹ 8.000-12.000 TEU.
- **Thiết bị xếp dỡ 100% chạy điện** (định hướng cảng xanh): 3 cẩu STS thế hệ mới Mitsui E&S (Nhật) tầm với 24 hàng container (~65m), nâng 46m — phục vụ tàu container lớn nhất thế giới.
- Là cửa ngõ kết nối trực tiếp mạng lưới toàn cầu của MSC/TIL — lợi thế "được hãng tàu chống lưng" chứ không thuần công nghệ phần mềm; dùng nền tảng số của PHP (ePort/Smart Gate) mở rộng.

---

## 4. HHIT — HATECO (BẾN 5-6 LẠCH HUYỆN) — "cảng thông minh đầu tiên của Việt Nam"

**Vận hành bởi:** Tập đoàn Hateco. Khánh thành 04/2025; 73 ha, cầu bến 900m, đón đồng thời 2 tàu đến 200.000 DWT (≥18.000 TEU), dài tối đa 400m — cảng nước sâu hiện đại nhất VN.

**Tính năng số đáng chú ý (đối thủ đáng gờm nhất về công nghệ):**
- **TOS thương mại quốc tế Navis N4** (chuẩn toàn cầu, không tự viết).
- **Cổng tự động hoàn toàn "không dừng" (non-stop gate) OCR + RFID**, kết hợp **kiosk tự phục vụ** cho tài xế; **thời gian xử lý DƯỚI 30 GIÂY/xe**; 100% lệnh điện tử.
- **TAS (Truck Appointment System) — ứng dụng ĐẶT LỊCH HẸN XE ĐẦU TIÊN TẠI VIỆT NAM**, vận hành chính thức 16/06/2025 sau 3 tháng thử nghiệm: tài xế đặt slot giờ trước khi đến cảng → điều tiết luồng xe, giảm ùn tắc, giảm phát thải. Đây chính là "hệ thống xếp hàng điện tử cho xe" mà đề bài yêu cầu tìm.
- Cẩu **bán tự động**: STS tầm với 24 hàng + **e-RTG chạy điện**; **mạng 5G** riêng trong cảng; camera giám sát 24/7.

---

## 5. NAM ĐÌNH VŨ — GEMADEPT

**Vận hành bởi:** CTCP Gemadept (GMD). Đã khai thác Giai đoạn 3 → tổng công suất trên 2 triệu TEU/năm, 7 cầu bến/1,5km, tàu đến 48.000 DWT — cảng sông lớn nhất miền Bắc. Định hướng "Smarter & Greener".

**Tính năng số đáng chú ý:**
- **SmartPort (trùng tên với sản phẩm của mình!)** — ứng dụng cảng thông minh Gemadept triển khai từ 12/05/2021 trên toàn hệ thống cảng Gemadept: đăng ký lệnh online, **thanh toán online, chứng từ điện tử, eDO, E-Gate, TRA CỨU DỮ LIỆU ĐA CẢNG TÍCH HỢP trong 1 nền tảng** (điểm mạnh chuỗi nhiều cảng Bắc-Nam), app riêng cho đội xe đầu kéo VÀ **SÀ LAN** (giao tiếp tự động đa chiều). Con số marketing: 1 lệnh giao dịch từ 3-4 giờ xuống còn ~10 phút.
- **SmartGate dùng AI** (đối tác chuyển đổi số CEH - VSL), áp dụng tại Nam Đình Vũ từ 01/12/2023: AI tự nhận diện **số container, biển số xe, biển số mooc, seal hãng tàu, seal hải quan, GPLX, CCCD tài xế và cả phiếu cân** — phạm vi nhận dạng giấy tờ rộng hơn ALPR thông thường.
- Thiết bị mới chạy điện, tiết kiệm năng lượng — gắn câu chuyện ESG/cảng xanh vào thương hiệu.

---

## 6. VIP GREEN PORT — VICONSHIP (VSC)

**Vận hành bởi:** CTCP Cảng Xanh VIP (thuộc hệ sinh thái Viconship: Green Port, VIP Green Port, và Nam Hải Đình Vũ mua lại từ Gemadept). 2 cầu bến 400m, tàu đến 42.000 DWT (~2.500 TEU), khai thác 24/7.

**Tính năng số đáng chú ý:**
- **ePort riêng** (eport.vipgreenport.com.vn): đăng ký giao nhận online; **thanh toán trực tuyến qua cổng VNPAY** chuyển thẳng về tài khoản Vietcombank của cảng; khách in phiếu hoặc **chỉ cần chụp màn hình mang đến cảng**.
- Phần mềm quản lý cập nhật **thời gian thực** cho mọi hoạt động cảng; **kết nối EDI với hãng tàu bất kỳ lúc nào**; phủ wifi toàn cảng; camera cổng/bãi/cầu tàu liên thông cơ quan quản lý.
- Định vị thương hiệu "cảng xanh" từ 2015. Mức độ số hóa nhìn chung THẤP hơn nhóm PHP/HICT/HHIT/Gemadept — chưa thấy công bố smart gate tự động hay TAS.

---

## 7. BẢNG SO SÁNH NHANH (trục tính năng số)

| Tính năng | PHP Tân Vũ | TC-HICT | HHIT (Hateco) | Nam Đình Vũ (GMD) | VIP Green |
|---|---|---|---|---|---|
| Smart gate tự động | Có, 10-25s, bắt buộc, 10 làn | Có, 100% từ 6/2025 | Non-stop OCR+RFID, <30s + kiosk | SmartGate AI từ 12/2023 | Chưa |
| Đặt lịch hẹn xe (TAS) | Chưa | Chưa (điều tiết qua app điều xe) | **CÓ — đầu tiên VN** | Chưa | Chưa |
| ePort riêng | Có + VNPAY/ví | Có + thanh toán, hóa đơn ĐT | 100% lệnh điện tử qua TAS | SmartPort đa cảng | Có + VNPAY |
| App tài xế | Container Driver + eEIR | HICT Lái Xe + mã BAT | Qua TAS/kiosk | App xe + sà lan | Chưa |
| App/công cụ cho NHÀ XE (điều phối) | Phân xe trong HaiphongPort App | **HICT Điều Xe (web+app)** | — | Trong SmartPort | — |
| TOS | PL-TOS (nội) | TOS quốc tế | **Navis N4** | TOS + CEH | PM nội bộ |
| Hạ tầng nổi bật | Bảng LED cổng | SmartHub | **5G riêng, e-RTG, cẩu bán tự động** | Thiết bị điện, GĐ3 2tr TEU | ESG/xanh |

---

## 8. GỢI Ý CHO SMARTPORT — THỨ HỌ CÓ MÀ MÌNH CHƯA CÓ (hoặc chưa làm rõ)

1. **TAS — đặt lịch hẹn/xếp hàng điện tử cho xe theo khung giờ (time-slot booking)**: HHIT đã làm "đầu tiên tại VN" và quảng bá rất mạnh. SmartPort có ePort booking + check-in tài xế nhưng CHƯA có điều tiết slot giờ vào cổng + phân bổ luồng xe chống ùn tắc → nên là module ưu tiên số 1 (dễ gắn với dữ liệu ALPR + DMS sẵn có, thêm thuật toán phân bổ slot/dự báo tải cổng).
2. **KPI thời gian qua cổng công khai (10-25 giây PHP, <30 giây HHIT)**: SmartPort có ALPR 3 tầng + đối chiếu lệnh nhưng chưa "đóng gói" thành cam kết non-stop gate với đồng hồ đo thời gian thực → nên thêm dashboard đo giây/xe và chế độ "cổng không dừng".
3. **Kiosk tự phục vụ tại cổng** (HHIT): phương án dự phòng khi app lỗi/tài xế mới.
4. **eEIR đẩy tự động qua app tài xế + bảng LED phiếu vị trí tại cổng** (PHP): SmartPort đã có EIR trong hồ sơ container 360 nhưng chưa có kênh đẩy tự động cho tài xế + màn hình LED hướng dẫn làn/vị trí.
5. **App điều xe cho doanh nghiệp vận tải** (HICT Điều Xe): SmartPort có DMS tài xế — nên mở rộng thành cổng dispatch cho NHÀ XE (giao lệnh, theo dõi đội xe) thay vì chỉ tài xế cá nhân; kèm **chuẩn hóa mã định danh tài xế** kiểu số BAT.
6. **Tra cứu/giao dịch ĐA CẢNG một nền tảng + hỗ trợ sà lan** (Gemadept SmartPort): nếu SmartPort định bán cho nhiều cảng thì kiến trúc multi-tenant tra cứu chéo là điểm cộng thương mại; module sà lan (barge) hiện chưa cảng nội địa nào làm sâu.
7. **Thanh toán đa kênh**: các cảng đều dùng VNPAY/ví điện tử bên cạnh chuyển khoản — SmartPort mới có VietQR, nên thêm cổng VNPAY/ví + tải & chuyển đổi hóa đơn điện tử ngay trên ePort (HICT có).
8. **Kết nối EDI chuẩn hãng tàu** (VIP Green, HICT): trao đổi eDO/manifest trực tiếp với hãng tàu — nâng tầm kho chứng từ số hiện có.
9. **Câu chuyện xanh gắn với số** (bến 3-4, HHIT, GMD): báo cáo giảm phát thải nhờ giảm thời gian chờ cổng (tính từ dữ liệu TAS/ALPR) — SmartPort có sẵn dữ liệu để làm báo cáo ESG tự động, chưa cảng nào làm phần mềm hóa mảng này.
10. **Điểm SmartPort đang NHỈNH hơn mặt bằng Hải Phòng**: AI gợi ý vị trí bãi, kiểm đếm AI, camera an ninh AI, AI Agent/chatbot, K-Means/AI Lab, phí lưu bãi tự động, báo cáo MISA — chưa thấy cảng nào ở Hải Phòng công bố tương đương → nên giữ làm điểm khác biệt khi chào hàng.

---

## NGUỒN THAM KHẢO CHÍNH
- Smart Gate Tân Vũ (10-25 giây, 10 làn, bắt buộc 01/05/2023): haiphongport.com.vn (mục Smart Gate, tin sản xuất kinh doanh); cangvuhaiphong.gov.vn; kaizen.vimc.co; thuehaiquan.tapchikinhtetaichinh.vn
- ePort PHP + HaiphongPort App: haiphongport.com.vn/vi/ep/eport
- TC-HICT: hict.net.vn; eport.hict.net.vn; app HICT Lái Xe / HICT Điều Xe (App Store, Google Play, dieuxe.hict.net.vn)
- AutoGate 25-30 giây (mô hình VIMC/Đà Nẵng mà HICT tham khảo): danangport.com; irtech.com.vn
- HHIT: hhit.com.vn (giới thiệu cổng tự động; tin "Cổng tự động + TAS đầu tiên tại Việt Nam", <30 giây, 16/06/2025); hatecogroup.vn; thanhnien.vn
- Bến 3-4 Lạch Huyện: nhandan.vn; vietnamplus.vn; baohaiphong.vn; nhadautu.vn
- Nam Đình Vũ: gemadept.com.vn; ndv.gemadept.com.vn (thông báo SmartGate 01/12/2023 + HDSD SmartGate); ictvietnam.vn
- VIP Green Port: vipgreenport.com.vn; eport.vipgreenport.com.vn (EportUserGuide.pdf); thanhphohaiphong.gov.vn; greenport.com.vn; eport.dinhvuport.com.vn

### BAO CAO CM
# BÁO CÁO NGHIÊN CỨU CỤM CẢNG CÁI MÉP – THỊ VẢI (BÀ RỊA – VŨNG TÀU)
*Phục vụ ban nghiên cứu SmartPort — trọng tâm: tính năng số / dịch vụ trực tuyến / công nghệ mà SmartPort CHƯA có*

## 0. Bối cảnh cụm cảng
- Cụm cảng nước sâu lớn nhất Việt Nam, duy nhất đón được tàu mẹ ~200.000+ DWT (tương đương 20.000+ TEU) đi thẳng Âu – Mỹ.
- Được World Bank & S&P Global xếp **hạng 7 thế giới về chỉ số hiệu quả cảng container CPPI** (công bố 6/2024) — nhờ năng suất giải phóng tàu mẹ rất cao.
- Đang được định hướng phát triển thành **cảng trung chuyển quốc tế xanh + thông minh**; ~80% hàng hóa trung chuyển giữa Cái Mép và TP.HCM đi bằng **sà lan** (giảm tải đường bộ, giảm phát thải).

---

## 1. CMIT — Cảng Quốc tế Cái Mép
- **Vận hành bởi:** Liên doanh APM Terminals (Maersk, 49%) + VIMC/Vinalines (36%) + Cảng Sài Gòn (15%). Thành lập 2007.
- **Quy mô:** Bến duy nhất VN đón tàu tới 214.121 DWT; đạt mốc 2 triệu TEU/năm.
- **Công nghệ / dịch vụ số đáng chú ý:**
  - **TOS Navis N4** (chuẩn quốc tế của APM Terminals) — kế hoạch bến, kế hoạch tàu, phân bổ cẩu bờ (crane split) cho tàu mẹ.
  - **Customer Access Portal (cap.cmit.com.vn):** tra cứu container theo số cont/booking/B/L, lịch tàu công khai, thanh toán online + hóa đơn điện tử.
  - **Hiển thị công khai tình trạng giao thông tại cổng cảng, cập nhật mỗi 3 phút** (camera/gate traffic) — khách chủ động chọn giờ đưa xe vào.
  - Kỷ lục năng suất: **233 container/giờ/tàu** với chỉ 6 cẩu bờ khi làm tàu Maersk Emden (~7.000 TEU trong ~20 giờ, 6/2023) → có hệ thống đo/phân tích năng suất bến–cẩu theo thời gian thực.
  - **Liên kết khai thác bến với TCIT liền kề** (dùng chung cầu bến, có cơ chế hải quan riêng cho chuyển container giữa 2 cảng).

## 2. TCIT — Cảng Quốc tế Tân Cảng – Cái Mép
- **Vận hành bởi:** Liên doanh Tổng công ty Tân Cảng Sài Gòn (SNP) + Mitsui O.S.K. Lines + Wan Hai + Hanjin Logistics. Hoạt động từ 1/2011. Vị thế **số 1 về sản lượng trong cụm**.
- **Công nghệ / dịch vụ số đáng chú ý:**
  - **ePort (hệ sinh thái ePort của Tân Cảng Sài Gòn):** đăng ký giao nhận container online, **eDO đã kết nối với hơn 30 hãng tàu/đại lý**, thanh toán phí qua thẻ ATM ngân hàng nội địa, tra cứu container/tàu/danh sách hàng XNK. Bắt buộc **100% khách hạ container rỗng qua ePort**.
  - **Đăng ký kiểm hóa, soi chiếu hải quan ngay trên ePort** — số hóa cả nghiệp vụ với cơ quan kiểm tra chuyên ngành (điểm rất đáng học).
  - TOS họ CATOS/TOPX của SNP; kỷ lục **238,08 container/giờ/tàu**, sản lượng 1 chuyến tàu mẹ 15.615 TEU.
  - **Giải thưởng Cảng Xanh (Green Port) của APEC APSN năm 2020** — cảng thứ 2 VN đạt.
  - **Tuyến trung chuyển container quốc tế từ Campuchia** — có quy trình/hồ sơ số cho hàng transshipment (TCTT năm 2024 làm gần 79.000 TEU hàng trung chuyển quốc tế).

## 3. Gemalink — Cảng Gemalink (Gemadept – CMA CGM)
- **Vận hành bởi:** Liên doanh Gemadept (VN) + Terminal Link/CMA CGM (Pháp). Khai trương 1/2021, cảng nước sâu lớn nhất cụm.
- **Công nghệ / dịch vụ số đáng chú ý:**
  - **Ứng dụng cảng thông minh "SMARTPORT" (smartport.gemadept.com.vn, từ 4/2022):** lệnh giao/hạ cont hàng & rỗng, lệnh đóng/rút hàng, lệnh dịch vụ — tất cả kèm **thanh toán online**; kết hợp **eDO + TOS + AutoGate số hóa 100% quy trình giao nhận, giảm ~30% thời gian chờ của xe**, giảm phát thải CO2 gián tiếp.
  - **Thiết kế smart port từ đầu:** dữ liệu cẩu (6 STS + 18 RTG) **truyền trực tiếp về trung tâm điều hành khai thác**, kiểm soát từng vị trí container trên bãi — tức có tầng **IoT/telemetry thiết bị** thật.
  - **ESG/cảng xanh:** cẩu STS chạy điện 100%, xe đầu kéo Terberg mới, **điện mặt trời 1,15 MWp (~3,8 MWh/ngày)**.
  - 5/2025: liên doanh Gemadept – CMA CGM vận hành **đội sà lan 100% chạy điện đầu tiên tại VN** (tuyến 180 km, giảm ~778 tấn CO2/năm).

## 4. SSIT — SP-SSA International Terminal
- **Vận hành bởi:** Liên doanh Cảng Sài Gòn + VIMC + SSA Marine (Seattle, Mỹ). Bến trên sông Thị Vải, đón tàu tới 160.000 DWT.
- **Công nghệ / dịch vụ số đáng chú ý:**
  - **ePort SSIT chính thức từ 22/01/2024:** tra cứu container/tàu, làm lệnh và **thanh toán online phí dịch vụ cảng** (có tài liệu HDSD công khai). Là cảng "chuyển đổi muộn" — cho thấy chuẩn ePort đã thành mặt bằng chung bắt buộc của cả cụm.
  - Cổng tra cứu lịch tàu công khai; mã cảng thống nhất trên các hệ thống ePort/VNACCS (SIT = SSIT = VNSPT).

## 5. TCTT — Tân Cảng – Cái Mép Thị Vải (cảng ODA, hạ lưu) & khu Cái Mép Hạ
- **TCTT:** Công ty TNHH MTV thuộc Tân Cảng Sài Gòn, khai thác bến ODA Nhật (JICA) 600m, 110.000 DWT, 600.000 TEU/năm; **486 ổ cắm container lạnh** (reefer), kho CFS 6.400 m2; dùng chung hệ ePort/eDO của SNP; 2024 làm ~79.000 TEU hàng trung chuyển quốc tế. Cảng Tân Cảng – Cái Mép (TCCT/QTM) định hướng "cảng hiện đại, xanh, thông minh" ứng dụng tự động hóa & quản trị dữ liệu.
- **Dự án Trung tâm logistics Cái Mép Hạ:** ~1.763 ha, vốn ~19.200 tỷ đồng — định hướng siêu cảng trung chuyển + trung tâm logistics sau cảng tầm khu vực.

## 6. Cơ chế/công nghệ cấp CỤM đáng chú ý
- **Cơ chế "cảng mở" (open port):** Hải quan đang xây dựng quy chế cho phép **chuyển container giữa các bến trong cụm không cần mở tờ khai vận chuyển độc lập + kẹp chì từng cont**; hiện đã có cơ chế riêng cho cặp bến liên kết TCIT–CMIT. Đây là bài toán "inter-terminal transfer" có quy trình số riêng.
- **EDI chuẩn hãng tàu quốc tế:** các cảng trong cụm trao đổi EDIFACT (BAPLIE, COPRAR, CODECO, COARRI...) trực tiếp với hãng tàu mẹ; TOS đa dạng: Navis N4, CATOS, TOPX, VTOS, PTOS.
- **Xanh hóa vận tải thủy:** 80% hàng đi sà lan; đội sà lan điện; các cảng đạt tiêu chí cảng xanh; chi phí vận hành cụm giảm từ ~200 tỷ còn ~66 tỷ đồng/năm nhờ chuyển đổi số + xanh.

---

## 7. GỢI Ý CHO SMARTPORT — thứ các cảng Cái Mép CÓ mà SmartPort CHƯA có
| # | Tính năng/công nghệ | Học từ | Ghi chú so với SmartPort hiện tại |
|---|---|---|---|
| 1 | **Module kế hoạch tàu & bến (vessel/berth planning, crane split)** + dashboard năng suất container/giờ/tàu | CMIT, TCIT (Navis N4, CATOS) | SmartPort mới mạnh mảng cổng-bãi-chứng từ, chưa có nghiệp vụ khai thác tàu mẹ |
| 2 | **EDI chuẩn EDIFACT với hãng tàu** (BAPLIE/COPRAR/CODECO/COARRI) và **eDO kết nối 30+ hãng tàu** | TCIT | eDO của SmartPort đang nội bộ; chưa có tầng kết nối liên hãng tàu quy mô lớn |
| 3 | **Đăng ký kiểm hóa/soi chiếu hải quan ngay trên ePort** (tích hợp VNACCS/kiểm tra chuyên ngành) | TCIT | SmartPort chưa có phân hệ nghiệp vụ hải quan |
| 4 | **Quy trình "cảng mở"/chuyển bãi liên cảng (inter-terminal transfer)** có giám sát hải quan số | Cụm CM-TV, cặp TCIT–CMIT | Tính năng mới hoàn toàn với SmartPort |
| 5 | **Quản lý sà lan/thủy nội địa (barge booking, kế hoạch cầu sà lan)** | Toàn cụm (80% hàng đi sà lan), sà lan điện Gemadept–CMA CGM | SmartPort chỉ có DMS cho tài xế đường bộ |
| 6 | **IoT/telemetry thiết bị:** dữ liệu cẩu STS/RTG truyền thẳng về trung tâm điều hành | Gemalink | SmartPort có camera AI nhưng chưa có dữ liệu máy móc thời gian thực |
| 7 | **Module ESG/cảng xanh:** đo điện năng, điện mặt trời, tính phát thải CO2 tiết kiệm được | Gemalink, TCIT (giải Green Port APEC) | Có thể thành báo cáo ESG tự động — xu hướng bắt buộc |
| 8 | **Công khai tình trạng cổng cảng thời gian thực (cập nhật ~3 phút) cho khách hàng** | CMIT (CAP) | SmartPort có ALPR/camera nhưng chưa "mở" dữ liệu cổng ra portal khách |
| 9 | **Giám sát container lạnh (reefer monitoring)** — cảnh báo nhiệt độ, quản lý ổ cắm | TCTT (486 ổ reefer) | Chưa có trong danh mục SmartPort |
| 10 | **Quản lý hàng trung chuyển quốc tế (transshipment)** — quy trình riêng, thống kê TEU trung chuyển | TCIT/TCTT (tuyến Campuchia) | Chưa có trong danh mục SmartPort |
| 11 | Thanh toán bổ sung **thẻ ATM nội địa/cổng ngân hàng** bên cạnh VietQR + hóa đơn điện tử tự động | TCIT, CMIT | Mở rộng phương thức thanh toán hiện có |

**Nhận định nhanh:** mặt bằng chung của cụm Cái Mép là *ePort + eDO + thanh toán online* — SmartPort đã ngang hoặc vượt ở tầng cổng/bãi/AI. Khoảng trống lớn nhất của SmartPort so với cụm này nằm ở **(a) nghiệp vụ khai thác tàu mẹ & EDI hãng tàu, (b) tích hợp hải quan, (c) sà lan/trung chuyển liên cảng, (d) IoT thiết bị + ESG**.

## Nguồn chính
- CMIT: [APM Terminals – Cai Mep](https://www.apmterminals.com/en/cai-mep/about/our-terminal), [Customer Access Portal](https://cap.cmit.com.vn/), [CMIT phá kỷ lục xếp dỡ](https://www.cmit.com.vn/vi/News/cai-mep-international-terminal-(cmit-breaks-the-volume-handling-record-for-a-single-vessel-call), [CMIT 2 triệu TEU – SGGP](https://www.sggp.org.vn/cang-quoc-te-cai-mep-lap-ky-luc-san-luong-2-trieu-teu-post827533.html)
- TCIT: [TCIT – About](https://tcit.com.vn/en/about-us.html), [Triển khai kiểm hóa/soi chiếu trên ePort](https://tcit.com.vn/tin-tuc/thong-bao/thong-bao-v-v-trien-khai-he-thong-dang-ky-kiem-hoa-soi-chieu-tren-eport.html), [100% hạ rỗng qua ePort](https://www.tcit.com.vn/vi/news/thong-bao-trien-khai-e-port-doi-voi-100-khach-hang-ha-container-rong-tai-tcit.html), [Hành trình 15 năm TCIT](https://diendandoanhnghiep.vn/cang-quoc-te-tan-cang-cai-mep-tcit-hanh-trinh-15-nam-khang-dinh-tam-voc-cang-nuoc-sau-hang-dau-viet-nam-10146327.html), [SNP hoàn thiện ePort cho Cái Mép](https://logistics.gov.vn/dich-vu-logistics/dich-vu-khac/tan-cang-sai-gon-se-hoan-thien-eport-cho-cac-cang-cai-mep-hiep-phuoc-va-lach-huyen-hict)
- Gemalink: [Gemalink triển khai SMARTPORT](https://www.gemadept.com.vn/gemalink-chinh-thuc-trien-khai-ung-dung-cang-thong-minh-smartport/), [ESG tại Gemalink](https://www.gemadept.com.vn/giai-bai-toan-esg-trong-quan-ly-khai-thac-cang-nhin-tu-cang-gemalink-dau-an-hla-connect-ky-10/), [Gemalink hoạt động – VTV](https://vtv.vn/xa-hoi/sieu-cang-gemalink-chinh-thuc-di-vao-hoat-dong-20210120083706182.htm)
- SSIT: [SSIT dùng ePort từ 22/01/2024](https://www.ssit.com.vn/tin-tuc/ssit-trien-khai-cong-eport-chinh-thuc-tu-ngay-22-thang-01-nam-2024.html), [HDSD ePort SSIT](https://www.ssit.com.vn/assets/uploads/myfiles/files/Procedures/SSIT_ePort/SSIT_HDSD_ePort.pdf), [VPA – SSIT](https://www.vpa.org.vn/sp-ssa-international-terminal-ssit/)
- TCTT/Cái Mép Hạ: [SNP – TCTT](https://saigonnewport.com.vn/en/port-company/tan-cang-cai-mep-thi-vai-terminal-tctt), [Wikipedia – Cảng QT Tân Cảng Cái Mép](https://vi.wikipedia.org/wiki/C%E1%BA%A3ng_Qu%E1%BB%91c_t%E1%BA%BF_T%C3%A2n_C%E1%BA%A3ng_-_C%C3%A1i_M%C3%A9p), [Cái Mép Hạ – MOIT](https://moit.gov.vn/tin-tuc/phat-trien-cong-nghiep/can-khan-truong-bien-khu-vuc-cang-cai-mep-ha-tro-thanh-trung-tam-logistics-lon-cua-khu-vuc-va-the-gioi.html)
- Cấp cụm: [Chính sách "cảng mở" CM-TV](https://logistics.gov.vn/chinh-sach/trong-nuoc/chinh-sach-cang-mo-tang-hang-hoa-qua-cang-cai-mep-thi-vai), [Phát triển CM-TV xanh, thông minh – VOV](http://vov.vn/kinh-te/phat-trien-cum-cang-nuoc-sau-cai-mep-thi-vai-theo-huong-xanh-thong-minh-post1312627.vov), [CM-TV điểm sáng cảng xanh – VTC](https://vtcnews.vn/cum-cang-cai-mep-thi-vai-diem-sang-cang-bien-xanh-trong-khu-vuc-ar985720.html), [Chuyển đổi số tăng năng suất giải phóng tàu – Báo BR-VT](https://www.baobariavungtau.com.vn/kinh-te/202405/cang-bien-day-manh-chuyen-doi-so-tang-nang-suat-giai-phong-tau-1009322/)

### BAO CAO MT
# BÁO CÁO NGHIÊN CỨU: CÁC CẢNG MIỀN TRUNG + KHÁC (Đà Nẵng, Quy Nhơn, Chân Mây, Dung Quất, Cửa Lò, Nghi Sơn, Cần Thơ, Long An)

Phạm vi: điều tra tính năng số / dịch vụ trực tuyến / công nghệ của từng cảng, đối chiếu với bộ tính năng SmartPort hiện có để tìm khoảng trống.

---

## 1. CẢNG ĐÀ NẴNG (Tiên Sa) — chuẩn mực số hóa của miền Trung

- **Vận hành:** CTCP Cảng Đà Nẵng (thành viên VIMC), khai thác khu bến Tiên Sa.
- **Tính năng số nổi bật (đã kiểm chứng):**
  - **DNP ePort** (eport.danangport.com, triển khai cuối 2020) — cảng điện tử ĐẦU TIÊN của miền Trung: eDO (lệnh giao nhận điện tử kết nối TẤT CẢ hãng tàu), eBooking, thanh toán điện tử không tiền mặt, **e-Customs (kết nối hải quan điện tử)**, hóa đơn điện tử. Mức số hóa ~90%, >95% khách hàng dùng online.
  - **AutoGate — cổng container tự động đầu tiên tại Việt Nam** (10/2021): nhận diện mã container bằng thuật toán ACCR + AI, nhận diện biển số, PLC điều khiển barrier, đọc cân điện tử; thời gian qua cổng từ ~10 phút xuống **~1 phút/xe**.
  - **RPA (Robotic Process Automation)** xử lý lệnh tự động ở nhiều khâu back-office — điểm rất ít cảng VN có.
  - **Trạm nhiên liệu tự động**, Data Center riêng + cáp quang phủ toàn cảng, trung tâm điều hành sản xuất.
  - Định hướng cảng xanh — cảng thông minh; ePort + AutoGate đạt giải Nhất Sáng tạo kỹ thuật Đà Nẵng, giải Ba VIFOTEC; được Tân Cảng Cái Mép, Cam Ranh, Quy Nhơn, Hải Phòng đến học tập mô hình.
- **SmartPort chưa có:** RPA xử lý lệnh, kết nối e-Customs một cửa, trạm nhiên liệu tự động, lộ trình "cảng xanh" có KPI.

## 2. CẢNG LIÊN CHIỂU (Đà Nẵng) — đối thủ tương lai cỡ 2 tỷ USD

- **Trạng thái:** đã khởi công bến container 4/2026 (chọn nhà đầu tư qua đấu thầu 12/2025); tổng đầu tư ~45.268 tỷ đồng (~2 tỷ USD), 8 bến / 2.750m, tàu 18.000 TEU, công suất thiết kế 5,7 triệu TEU/năm, 3 giai đoạn 2026–2036.
- **Công nghệ cam kết:** định vị "cảng xanh, cảng thông minh" ngay từ thiết kế — **IoT + AI trong quản lý, tự động hóa khai thác, năng lượng sạch, thiết bị xanh**.
- **Ý nghĩa với SmartPort:** đây là cảng "sinh ra đã số" — chuẩn tham chiếu tính năng tương lai (tự động hóa bến bãi, điện hóa thiết bị).

## 3. CẢNG QUY NHƠN (Bình Định) — ePort trên nền SmartTOS

- **Vận hành:** CTCP Cảng Quy Nhơn (QNP, thành viên VIMC).
- **Tính năng số nổi bật:**
  - **ePort Quy Nhơn** (eport.quynhonport.vn, chạy từ 28/11/2021, "Powered by **Intecom SmartTOS**"): tra cứu container, tra cứu hóa đơn điện tử, đăng ký làm hàng, thanh toán điện tử; 100% khách hàng sử dụng.
  - **Điểm rất đáng chú ý mà SmartPort CHƯA có:** khách hàng theo dõi **vị trí tàu và hàng hóa real-time 24/7**, và **xem trực tiếp camera hiện trường xếp dỡ hàng CỦA MÌNH** từ xa — biến camera thành dịch vụ khách hàng chứ không chỉ an ninh nội bộ.
  - Chiến lược Smart Port đang tuyển nhân sự triển khai: **nâng cấp TOS, ePort, ứng dụng AI và Big Data**.
  - eDO với hãng tàu: CMA CGM áp dụng EDO tại Quy Nhơn từ 15/8/2024.
- **SmartPort chưa có:** cổng camera "xem hàng của tôi" cho khách, hiển thị vị trí tàu real-time (AIS) trên ePort.

## 4. CẢNG CHÂN MÂY (Huế) — mạnh về tàu du lịch, số hóa còn mỏng

- **Vận hành:** CTCP Cảng Chân Mây (cổ phần hóa 2015).
- **Đặc điểm:** cảng loại I, 3 bến, tàu hàng 70.000 DWT, **tàu du lịch đến 225.000 GT** — điểm đến thường xuyên của Royal Caribbean, Princess, Costa, Silversea; 1 trong 46 cảng được Hiệp hội Du thuyền châu Á chọn. Mới làm container từ cuối 2022.
- **Số hóa:** mới ở mức "đang triển khai hệ thống cảng thông minh" (tối ưu thủ tục, giảm thời gian thông quan) — CHƯA thấy ePort/eDO công khai. Website chỉ có giới thiệu dịch vụ + tra cứu cơ bản.
- **Bài học cho SmartPort:** phân khúc **quản lý tàu khách/cruise (lịch tàu, hành khách, dịch vụ bờ)** là module chưa cảng phần mềm nội nào làm tốt; Chân Mây là khách hàng tiềm năng vì đang "đói" giải pháp số.

## 5. CẢNG DUNG QUẤT (Quảng Ngãi) — cụm nhiều nhà khai thác, số hóa phân mảnh

- **Vận hành:** cụm bến chia cho nhiều đơn vị: **PTSC Quảng Ngãi** (2 bến), **Gemadept Dung Quất** (1 bến), **Hào Hưng Quảng Ngãi** (4 bến), **Hòa Phát Dung Quất** (bến tổng hợp - container). Cảng loại I, nước sâu 21m không phụ thuộc thủy triều, quy hoạch ~90 triệu tấn/năm đến 2030.
- **Số hóa:** không có ePort dùng chung toàn cụm; từng nhà khai thác tự quản (Gemadept có năng lực công nghệ cấp tập đoàn — SmartGate/app cảng ở phía Bắc — nhưng chưa thấy công bố riêng cho Dung Quất). Đây là mô hình "một khu bến – nhiều operator" mà **một nền tảng ePort đa tenant** như SmartPort có thể chào bán.

## 6. CẢNG CỬA LÒ (Nghệ An) — đã có Smart TOS của Intecom

- **Vận hành:** CTCP Cảng Nghệ Tĩnh (thành viên VIMC), quản lý khai thác Cửa Lò + Bến Thủy.
- **Số hóa:** dùng **Intecom Smart TOS** quản lý điều hành khai thác hỗn hợp (container + tổng hợp): định vị DGPS theo dõi vị trí container/phương tiện real-time, lập kế hoạch xếp dỡ tự động, tính cước "một cửa"; có tra cứu hóa đơn điện tử. Chưa thấy ePort/eDO đầy đủ cho khách hàng.
- **Chính sách:** Nghệ An trợ giá hãng tàu container + doanh nghiệp XNK qua Cửa Lò, kèm chủ trương điện tử hóa thủ tục hành chính khu kinh tế.

## 7. CẢNG NGHI SƠN (Thanh Hóa) — hạ tầng mạnh, số hóa yếu nhất nhóm

- **Vận hành:** nhiều đơn vị (PTSC Thanh Hóa, cảng quốc tế Nghi Sơn, Long Sơn...); hãng tàu container: CMA CGM (quốc tế, 1 chuyến/tuần), VIMC Shipping, Hải An (nội địa).
- **Hiện trạng:** 6 bến/1.397m, tàu tổng hợp 70.000 DWT; nhưng bị đánh giá "thiếu hạ tầng logistics, đói tàu container". Cạnh tranh chủ yếu bằng **trợ giá của tỉnh** (NQ 248/2022: 500 triệu đồng/chuyến tàu container quốc tế, 2–3 triệu đồng/container nội địa), gần 40.000 tỷ vốn đầu tư hạ tầng.
- **Số hóa:** KHÔNG tìm thấy ePort/eDO riêng — khoảng trống thị trường rõ rệt cho giải pháp như SmartPort.

## 8. CẢNG CẦN THƠ (ĐBSCL) — mới ở giai đoạn "nghiên cứu triển khai"

- **Vận hành:** CTCP Cảng Cần Thơ (thành viên VIMC) — khu bến Hoàng Diệu + Cái Cui; bên cạnh đó có **Tân Cảng Cái Cui** (cảng thứ 17 của Tổng công ty Tân Cảng Sài Gòn, hưởng hệ sinh thái ePort SNP).
- **Số hóa:** Cảng Cần Thơ mới "**đang nghiên cứu triển khai** Smart Gate, e-Port, EDI/EDO" theo định hướng VIMC — tức chậm hơn Đà Nẵng/Quy Nhơn 3–4 năm. Điểm mới 2026: thử nghiệm tuyến container trực tiếp **Nam Ninh (Trung Quốc) – Cần Thơ**.
- **Ý nghĩa:** cảng VIMC hạng trung ở ĐBSCL là tập khách hàng đúng "khẩu vị" của SmartPort (cần trọn gói ePort + gate + TOS nhẹ, chi phí thấp).

## 9. CẢNG QUỐC TẾ LONG AN — hệ sinh thái số + cảng xanh đáng học nhất

- **Vận hành:** Đồng Tâm Group (Long An International Port), Chủ tịch kiêm TGĐ Võ Quốc Huy.
- **Tính năng số/công nghệ nổi bật:**
  - **Hệ sinh thái số xuyên suốt: SAP S/4HANA (ERP) + TOS (CATOS) + MOST + E-Port + WMS** — kết nối từ cầu bến, kho bãi, vận tải đến khách hàng. Dùng bộ giải pháp CyberLogitec (chuẩn quốc tế) thay vì phần mềm nội.
  - **Cảng xanh:** pin mặt trời, đèn LED, **cẩu điện 100%**, lộ trình **điện bờ (shore power) năm 2030**.
  - **Doanh nghiệp cảng Việt Nam ĐẦU TIÊN nhận giải "Cảng xanh thông minh châu Á – Thái Bình Dương"** (7/2026); giành quyền đăng cai **ASEAN Ports & Logistics 2027**.
- **SmartPort chưa có:** tích hợp ERP sâu (SmartPort mới dừng ở báo cáo MISA), WMS kho hàng, module giám sát năng lượng/phát thải phục vụ chứng nhận cảng xanh.

---

## BẢNG SO SÁNH NHANH MỨC ĐỘ SỐ HÓA

| Cảng | ePort/eDO | Cổng tự động | TOS | Điểm khác biệt |
|---|---|---|---|---|
| Đà Nẵng | Có, ~90% số hóa, e-Customs | AutoGate đầu tiên VN, ~1 phút/xe | Có + RPA | Chuẩn mực miền Trung |
| Quy Nhơn | Có (Intecom SmartTOS) | Đang đầu tư | SmartTOS | Camera xem hàng + vị trí tàu real-time cho khách |
| Long An | E-Port | Có | CATOS + SAP + WMS | Giải cảng xanh thông minh APAC đầu tiên VN |
| Chân Mây | Chưa | Chưa | Đang làm | Chuyên tàu du lịch 225.000 GT |
| Dung Quất | Chưa (phân mảnh nhiều operator) | Chưa | Riêng lẻ | Nước sâu 21m |
| Cửa Lò | Một phần | Chưa | Intecom Smart TOS | Trợ giá tỉnh |
| Nghi Sơn | Chưa | Chưa | Yếu | Trợ giá 500tr/chuyến, hạ tầng tỷ đô |
| Cần Thơ | Đang nghiên cứu | Đang nghiên cứu | Cũ | Tuyến TQ trực tiếp; TC Cái Cui dùng ePort SNP |
| Liên Chiểu | (tương lai) | Tự động hóa từ thiết kế | IoT+AI | 5,7 triệu TEU, 2 tỷ USD |

## GỢI Ý TÍNH NĂNG SMARTPORT NÊN BỔ SUNG (đối thủ đã có, SmartPort chưa có)

1. **Cổng khách hàng xem camera làm hàng của chính mình + vị trí tàu real-time (AIS)** — Quy Nhơn đã làm, rất "ăn điểm" trải nghiệm; SmartPort đã có camera AI an ninh, chỉ cần thêm lớp phân quyền cho khách xem theo lô hàng.
2. **Kết nối e-Customs (hải quan điện tử một cửa)** — Đà Nẵng đã có; SmartPort mới dừng ở chứng từ nội bộ.
3. **RPA xử lý lệnh back-office** — Đà Nẵng dùng robot phần mềm tự động hóa nghiệp vụ văn phòng, khác với AI nghiệp vụ hiện trường mà SmartPort đang có.
4. **Module cảng xanh/ESG:** đo điện năng, nhiên liệu, phát thải CO2 theo chuyến/thiết bị, dashboard phục vụ xét giải cảng xanh (Long An chứng minh giải thưởng xanh = lợi thế thương mại thật).
5. **Tích hợp ERP + WMS** (chuẩn Long An: SAP + CATOS + WMS) — SmartPort nên có API/connector ERP thay vì chỉ xuất báo cáo MISA.
6. **eBooking tàu (vessel booking/berth window) + dự báo lịch tàu** — Đà Nẵng có hệ thống dự báo, điều độ tàu.
7. **Kiến trúc đa tenant "một khu bến – nhiều operator"** để chào các cụm cảng phân mảnh (Dung Quất, Nghi Sơn).
8. **Module cảng khách/cruise** (lịch tàu khách, dịch vụ hành khách) — khoảng trống Chân Mây.
9. **Thị trường mục tiêu dễ thắng:** Nghi Sơn, Cửa Lò, Chân Mây, Cần Thơ — hạ tầng có, chính sách trợ giá có, nhưng chưa có ePort/autogate; đúng phân khúc SmartPort đang mạnh (eDO+PIN, ALPR 3 tầng, trạm cân VGM, hồ sơ container 360).

## NGUỒN

- [Bộ GTVT — Sáng kiến chuyển đổi số tại Cảng Đà Nẵng](https://mt.gov.vn/cntt/tin-tuc/855/85603/nhieu-sang-kien--giai-phap-sang-tao-trong-%E2%80%9Cchuyen-doi-so%E2%80%9D-tai-cang-da-nang-.aspx)
- [Danang Port — Số hóa cơ sở hạ tầng](https://danangport.com/can-canh-cang-bien-hien-dai-bac-nhat-viet-nam-nho-so-hoa-co-so-ha-tang/?lang=en)
- [DNP ePort](https://eport.danangport.com/) · [Danang Port — AutoGate](https://danangport.com/autogate-la-giai-phap-chuyen-doi-so-da-gop-phan-dua-cang-da-nang-tro-thanh-cang-bien-truc-tuyen-thong-minh-va-hien-dat-nhat-mien-trung/?lang=en) · [Báo Đà Nẵng — Chuyển đổi số ở Cảng Đà Nẵng](https://www.baodanang.vn/kinhte/202501/chuyen-doi-so-o-cang-da-nang-3998928/)
- [IRTECH — Giải pháp ePort/AutoGate](https://irtech.com.vn/giai-phap-cang-dien-tu-eport/)
- [Quy Nhơn Port — Giới thiệu ePort](https://www.quynhonport.vn/tin-tuc/gioi-thieu-he-thong-cang-dien-tu-eport-cua-cang-quy-nhon-511.html) · [ePort Quy Nhơn (Intecom SmartTOS)](https://eport.quynhonport.vn/) · [Quy Nhơn Port áp dụng thành công ePort](https://www.quynhonport.vn/tin-tuc/cang-quy-nhon-ap-dung-thanh-cong-mo-hinh-cang-bien-eport-chat-luong-dich-vu-tot-hon-558.html)
- [Danang.gov.vn — Chọn nhà đầu tư cảng Liên Chiểu Q1/2026](https://danang.gov.vn/vi/w/hoan-thanh-thu-tuc-lua-chon-nha-dau-tu-va-khoi-dong-du-an-cang-container-lien-chieu-trong-quy-i-nam-2026) · [VnEconomy — Khởi công cảng Liên Chiểu](https://vneconomy.vn/khoi-cong-cang-lien-chieu-cu-hich-moi-cho-logistics-mien-trung.htm)
- [Tạp chí Công Thương — Cảng Chân Mây](https://tapchicongthuong.vn/cang-chan-may--mot-trung-tam-moi-trong-chuoi-gia-tri-logistics-mien-trung-va-khu-vuc-141922.htm) · [Chan May Port](https://www.chanmayport.com.vn/)
- [Gemadept — Cảng Dung Quất](https://www.gemadept.com.vn/cang-dung-quat-433/index.html) · [PTSC Quảng Ngãi](https://ptscquangngai.com.vn/en/facilities/dung-quat-port.html)
- [Nghệ Tĩnh Port — Công nghệ thông tin](https://nghetinhport.vn/nguon-luc-khai-thac/cong-nghe-thong-tin/) · [Intecom Smart TOS](https://intecom.vn/phan-mem-khai-thac-cang-toan-dien-smart-tos.html) · [VLR — Nghệ An hỗ trợ tàu container Cửa Lò](https://vlr.vn/nghe-an-ban-hanh-chinh-sach-ho-tro-tau-container-tai-cang-cua-lo-17511.html)
- [VnEconomy — Nghi Sơn thiếu hạ tầng logistics](https://vneconomy.vn/thieu-ha-tang-logistics-cang-nghi-son-doi-tau-container-quoc-te.htm) · [Báo Thanh Hóa — Hấp dẫn hãng tàu đến Nghi Sơn](https://baothanhhoa.vn/hap-dan-hang-tau-den-voi-cang-bien-nghi-son-211666.htm)
- [Cảng Cần Thơ — Kết nối logistics ĐBSCL](http://www.canthoport.com.vn/news/cang-can-tho-ket-noi-phat-trien-cac-chuoi-dich-vu-logistics-khu-vuc-dbscl-mien-dong-nam-bo-va-vuong-quoc-campuchia) · [VIMC — Tuyến Nam Ninh–Cần Thơ](https://vimc.co/thu-nghiem-tuyen-van-tai-container-nam-ninh-can-tho-mo-them-huong-ket-noi-logistics-cho-dbscl/) · [SNP — Tân Cảng Cái Cui](https://saigonnewport.com.vn/en/port-company/tan-cang-cai-cui-terminal-tccc)
- [Thời báo Ngân hàng — Long An Port nhận giải quốc tế, đăng cai ASEAN Ports 2027](https://thoibaonganhang.vn/cang-quoc-te-long-an-don-giai-thuong-quoc-te-huong-toi-dang-cai-asean-ports-logistics-2027-184696.html) · [VietnamPlus — DN Việt đầu tiên nhận giải cảng xanh thông minh APAC](https://baomoi.com/doanh-nghiep-viet-dau-tien-duoc-vinh-danh-cang-xanh-thong-minh-chau-a-tbd-c55579724.epi) · [Long An Port — VILOG 2025](https://longanport.com/vi/cang-quoc-te-long-an-dong-hanh-cung-vilog-2025-khang-dinh-manh-me-ve-cam-ket-phat-trien-xanh-va-ben-vung/)

### BAO CAO CN
# BÁO CÁO: CÔNG NGHỆ NỔI BẬT TOÀN NGÀNH CẢNG BIỂN VIỆT NAM 2024–2026
(Phục vụ định hướng roadmap SmartPort — TOS + AI cho cảng container VN)

## 1. Port Community System (PCS) quốc gia — CHƯA có, nhưng đang thành hình

- **Hiện trạng:** Việt Nam CHƯA có PCS quốc gia đúng nghĩa (kiểu Portbase Hà Lan / TradeNet Singapore). Cái gần nhất đang chạy là **Cơ chế Một cửa Quốc gia (NSW)** cho thủ tục tàu vào/rời cảng + **VNACCS/VCIS** cho thông quan, do ngành Hải quan vận hành; Cục Hải quan đang triển khai dự án "Mở rộng Cổng thông tin một cửa quốc gia và kết nối Một cửa ASEAN (giai đoạn 3)".
- Bộ GTVT (nay là Bộ Xây dựng) có Đề án "Tăng cường chuyển đổi số giai đoạn 2024–2025" và bài toán **Trung tâm tích hợp dữ liệu chuyên ngành hàng hải** (đăng trên dx.gov.vn) — nền móng cho PCS quốc gia tương lai.
- Giới chuyên môn (VLR, Vinalines) liên tục kêu gọi PCS vì hiện 1 lệnh giao container qua ~11 điểm dừng mất 6–8 giờ. Quy hoạch 2025–2030 định hướng các cụm Lạch Huyện, Cái Mép – Thị Vải, Cần Giờ áp dụng PCS + 5G.
- **Ý nghĩa cho SmartPort:** chưa cảng nào có "PCS mini". SmartPort có thể đi trước bằng **lớp API mở/EDI connector** (kết nối hãng tàu, forwarder, hải quan VASSCM, depot) — đóng vai trò PCS cấp cảng, sẵn sàng ghép vào PCS quốc gia sau này.

## 2. Quy định "cảng xanh" của Cục Hàng hải — có lộ trình BẮT BUỘC

- **Khung pháp lý:** QĐ 2027/QĐ-BGTVT (29/10/2020) phê duyệt Đề án phát triển cảng xanh; **QĐ 710/QĐ-CHHVN (02/6/2021)** ban hành kế hoạch thực hiện; Cục Hàng hải đã ban hành **Tiêu chuẩn cơ sở TCCS 02:2022/CHHVN "Tiêu chí cảng xanh"** với 6 nhóm tiêu chí để cảng tự đánh giá và công bố.
- **Lộ trình:** 2020–2025 thí điểm; 2025–2030 xây tiêu chuẩn kỹ thuật quốc gia + áp dụng tự nguyện; **sau 2030 BẮT BUỘC** áp dụng tiêu chí cảng xanh trong quy hoạch, đầu tư và khai thác cảng biển.
- Xu hướng thực địa: chuyển RTG dầu → **eRTG chạy điện lưới**, điện bờ cho tàu, xe điện nội bộ, đo phát thải CO2.
- **Gap của SmartPort:** chưa có **module Cảng xanh/ESG** — dashboard đo điện/nhiên liệu/CO2 theo TEU, checklist tự đánh giá theo 6 nhóm tiêu chí TCCS 02:2022, báo cáo phát thải. Đây là tính năng "đón đầu quy định" rất đáng làm (demo tốt khi vấn đáp/thuyết trình).

## 3. Hóa đơn điện tử & số hóa chứng từ

- **NĐ 70/2025/NĐ-CP** (sửa NĐ 123/2020, hiệu lực **01/6/2025**): siết hóa đơn điện tử, hóa đơn khởi tạo từ máy tính tiền kết nối dữ liệu cơ quan thuế cho bán hàng/dịch vụ trực tiếp. Cảng biển xuất hóa đơn dịch vụ nâng hạ, lưu bãi... đều phải theo chuẩn này (các nhà cung cấp phổ biến: MISA meInvoice, Fast e-Invoice, VNPT...).
- **eDO thành chuẩn mực 2025:** Maersk, ONE, HMM, COSCO, MSC, CMA CGM... đồng loạt chuyển lệnh giao hàng điện tử; MSC/CMA CGM áp dụng eDO tại Hiệp Phước từ 19/08/2025. Các cảng đã nhận eDO: Cát Lái, SP-ITC, cụm Cái Mép, một phần Hải Phòng.
- **VASSCM** (hệ thống quản lý, giám sát hải quan tự động tại cảng/kho/bãi): đã phủ rộng Hải Phòng, TP.HCM (SPCT...) — cảng kết nối trực tiếp dữ liệu getIn/getOut với hải quan, bỏ giấy tờ thủ công.
- **Gap của SmartPort:** đã có eDO+PIN, kho chứng từ số, báo cáo MISA — nhưng nên bổ sung: (a) **kết nối phát hành hóa đơn điện tử chuẩn NĐ 70/2025** (tích hợp meInvoice API thay vì chỉ "báo cáo MISA"), (b) **mô-đun mô phỏng kết nối VASSCM/hải quan** (gửi danh sách container đủ điều kiện qua khỏi khu vực giám sát).

## 4. Camera AI / OCR cổng (Smart Gate) — đã thành "mặt bằng chung"

- **Cảng Hải Phòng** triển khai Smart Gate giao nhận cổng tự động; giải pháp **Smartgate AI** (được ictvietnam ghi nhận) xử lý toàn bộ nghiệp vụ cổng ~1 giây/xe: nhận diện biển số đầu kéo + rơ-moóc, OCR số container, chụp ảnh tình trạng vỏ, đẩy phiếu giao nhận điện tử qua app tài xế.
- **IRTECH AutoGate**, giải pháp nội địa tương tự, đang chào bán rộng.
- **HHIT (Hateco Lạch Huyện)** — khánh thành 4/2025, tự nhận là "cảng thông minh đầu tiên VN": **cổng tự động 100% từ 16/6/2025**, cầu tàu bán tự động.
- **So với SmartPort:** ALPR 3 tầng + check-in tài xế + đối chiếu lệnh tự động của SmartPort đã ngang mặt bằng; điểm có thể thêm: **chụp/AI đánh giá tình trạng vỏ container tại cổng (damage detection)** gắn vào hồ sơ 360 + EIR.

## 5. Tự động hóa cẩu RTG/STS + 5G riêng — mũi nhọn 2024–2026

- **HHIT Lạch Huyện:** 10 STS tầm với 24 hàng + **36 e-RTG chạy điện**, vận hành bán tự động — chuẩn mới của cảng nước sâu VN.
- **Tân Cảng Gantry** (thuộc SNP) tự sản xuất RTG trong nước, 2024 bàn giao 3 RTG cho Cát Lái/Hiệp Phước; **Vinalift** chào hệ điều khiển AI cho RTG; Konecranes bán "Smart Features" điều khiển từ xa.
- **5G dùng riêng (5G PMN):** Viettel triển khai 5G tại khu cảng **Tân Vũ, Đình Vũ (Hải Phòng)** — ứng dụng điều khiển cẩu RTG/STS từ xa (độ trễ ~1ms), giám sát vị trí container, drone an ninh. Đây là hạ tầng nền cho automation giai đoạn tới.
- **Phước An Port** (Đồng Nai, mở 2024–2025) cũng quảng bá mô hình tự động hóa & smart port từ đầu.
- **Gap của SmartPort:** thuần phần mềm, chưa có lớp **ECS/định vị thiết bị**: có thể thêm module "giám sát thiết bị" (trạng thái cẩu, số moves/giờ, mô phỏng điều độ cẩu) và ghi chú kiến trúc sẵn sàng 5G/IoT.

## 6. Điện mặt trời & năng lượng tái tạo tại cảng

- **Gemadept**: điện mặt trời áp mái tại ga hàng hóa SCSC và kho lạnh Mekong Logistics (4,8 MWp, Cần Thơ); ESG roadmap 2021–2025 hướng Net Zero; **Gemadept Dung Quất đạt giải Cảng xanh châu Á GPAS 2024** của APSN (APEC).
- Toàn ngành vào "đại phẫu xanh": áp mái nhà kho, eRTG, xe điện — gắn trực tiếp với tiêu chí cảng xanh mục 2.
- **Gợi ý SmartPort:** thêm widget "năng lượng tái tạo" vào dashboard ESG (sản lượng kWh mặt trời, % điện sạch) — dữ liệu giả lập cũng đủ gây ấn tượng.

## 7. Blockchain / vận đơn điện tử eB/L

- Các hãng tàu hoạt động tại VN đã dùng eB/L: Maersk, MSC, Hapag-Lloyd, CMA CGM, ONE (hợp tác **WaveBL**), Yang Ming, Evergreen. Nền tảng phổ biến: Bolero, essDocs, CargoX, WaveBL, edoxOnline (TradeLens của Maersk-IBM đã dừng từ 2023 — lưu ý khi thuyết trình).
- Phía VN mới ở mức nghiên cứu điều kiện pháp lý (Tạp chí Ngân hàng phân tích điều kiện áp dụng eB/L tại VN); chưa cảng nội địa nào phát hành/tiếp nhận eB/L trực tiếp — cảng chủ yếu chạm đến eDO chứ chưa đến eB/L.
- **Gợi ý SmartPort:** một module demo "xác thực chứng từ bằng hash/chuỗi khối" (lưu hash eDO/EIR lên sổ cái) là điểm cộng học thuật, chi phí thấp.

## 8. Giải thưởng chuyển đổi số ngành cảng (benchmark để "đo" SmartPort)

- **Top Công nghiệp 4.0 Việt Nam (I4.0 Awards):** Tân Cảng Sài Gòn được vinh danh 2023; **VIMC được vinh danh 2025** hạng mục đổi mới sáng tạo/chuyển đổi số (hệ sinh thái "VIMC One System", Cảng Sài Gòn chuyển đổi số toàn diện).
- **GPAS – Green Port Award System (APSN/APEC):** Gemadept Dung Quất 2024; trước đó Tân Cảng Cát Lái từng đạt.
- **Thương hiệu Quốc gia 2024:** Tân Cảng Sài Gòn lần thứ 8 liên tiếp — duy nhất ngành cảng-logistics.
- **Giải Sáng tạo TP.HCM:** SNP đạt giải Nhì (số hóa quản lý khai thác cảng, ePort).
- Ngoài ra còn **Vietnam Digital Awards (VDA)** do Hội Truyền thông số tổ chức hằng năm — kênh phù hợp nếu muốn "định vị" một sản phẩm như SmartPort.

## 9. TỔNG KẾT: SmartPort đang thiếu gì so với mặt bằng công nghệ ngành 2024–2026

| # | Khoảng trống | Căn cứ ngành | Độ ưu tiên |
|---|---|---|---|
| 1 | Module Cảng xanh/ESG (CO2/TEU, 6 nhóm tiêu chí TCCS 02:2022/CHHVN, điện mặt trời) | Bắt buộc sau 2030, cả ngành đang chạy | CAO |
| 2 | Hóa đơn điện tử chuẩn NĐ 70/2025 kết nối thuế (tích hợp meInvoice/Fast) | Hiệu lực 01/6/2025 | CAO |
| 3 | Lớp API mở/EDI "PCS mini" + kết nối VASSCM hải quan | PCS quốc gia đang thành hình, VASSCM đã phủ | CAO |
| 4 | AI phát hiện hư hỏng vỏ container tại cổng, gắn EIR | Smartgate AI, HHIT đã làm | TRUNG |
| 5 | Giám sát thiết bị cẩu (moves/giờ, trạng thái, điều độ) + ghi chú sẵn sàng 5G/IoT | HHIT bán tự động, Viettel 5G PMN Tân Vũ | TRUNG |
| 6 | Demo eB/L / xác thực chứng từ bằng blockchain hash | ONE-WaveBL, Maersk, MSC đã dùng eB/L | THẤP (điểm cộng) |
| 7 | Digital twin bãi cảng (VN dự kiến thí điểm hành lang Cái Mép–Cát Lái 2026–2027) | 11 nhóm công nghệ chiến lược quốc gia | THẤP |

## Nguồn tham khảo chính

- [MOIT — Tiêu chí "cảng xanh" bắt buộc áp dụng từ năm 2030](https://moit.gov.vn/phat-trien-ben-vung/tieu-chi-cang-xanh-bat-buoc-ap-dung-tu-nam-2030.html) · [Bộ GTVT — Từ sau 2030 bắt buộc "xanh hóa" cảng biển](https://mt.gov.vn/vn/tin-tuc/70096/tu-nam-2030-bat-buoc-thuc-hien-%E2%80%9Cxanh-hoa%E2%80%9D-cang-bien-viet-nam-.aspx) · [QĐ 710/QĐ-CHHVN 2021 — Kế hoạch thực hiện Đề án cảng xanh](https://luatvietnam.vn/hang-hai/quyet-dinh-710-qd-chhvn-cua-cuc-hang-hai-viet-nam-ban-hanh-ke-hoach-thuc-hien-de-an-phat-trien-cang-xanh-tai-viet-nam-357425-d1.html)
- [VLR — Vai trò Port Community System trong chuyển đổi số](https://vlr.vn/enhancing-port-efficiency-the-role-of-port-community-systems-in-digital-transformation-18072.html) · [dx.gov.vn — Trung tâm tích hợp dữ liệu chuyên ngành hàng hải](https://dx.gov.vn/bai-toan/xay-dung-trung-tam-tich-hop-du-lieu-chuyen-nganh-hang-hai-36.htm) · [Vimawa — Đề án tăng cường CĐS Bộ GTVT 2024–2025](https://vimawa.gov.vn/en/node/14704)
- [MISA — Nghị định 70/2025/NĐ-CP về hóa đơn, chứng từ từ 01/6/2025](https://asp.misa.vn/tt/kien-thuc/nghi-dinh-70-2025-nd-cp/) · [Finlogistics — eDO và quy trình 2025](https://www.finlogistics.vn/edo-la-gi/) · [Eimskip — eDO khác D/O giấy, các cảng áp dụng](https://eimskip.vn/lenh-giao-hang-dien-tu-edo)
- [Cảng vụ Hải Phòng — Cảng Hải Phòng triển khai Smart Gate](https://cangvuhaiphong.gov.vn/cang-hai-phong-trien-khai-he-thong-giao-nhan-cong-tu-dong-smart-gate/) · [ictvietnam — Thành công của Smartgate AI tại cảng biển VN](https://ictvietnam.vn/tu-thanh-cong-cua-smartgate-ai-tai-cang-bien-viet-nam-nghi-ve-luc-luong-lao-dong-moi-68682.html) · [IRTECH AutoGate](https://test.irtech.com.vn/autogate/)
- [Báo Chính phủ — Khánh thành cảng HHIT hiện đại nhất VN](https://baochinhphu.vn/khanh-thanh-cang-container-quoc-te-hateco-hai-phong-hien-dai-nhat-viet-nam-102250405135626956.htm) · [HHIT — tiếp nhận siêu cẩu STS](https://hhit.com.vn/vn/cang-container-quoc-te-hateco-hai-phong-hhit-tiep-nhan-sieu-cau-sts.html) · [VnEconomy — Tân Cảng Gantry sản xuất RTG](https://vneconomy.vn/tan-cang-gantry-khang-dinh-vi-the-tien-phong-trong-nganh-san-xuat-cau-rtg-tai-viet-nam.htm) · [Vinalift — điều khiển AI cho cẩu RTG](https://vinalift.vn/he-thong-dieu-khien-ai-cho-cau-rtg/) · [Phước An Port — Automation & Smart Port](https://phuocanport.com/en/location-and-infrastructure/automation-smart-port/)
- [VJST — 5G mở cơ hội cho cảng biển, nhà máy VN](https://vjst.vn/ung-dung-5g-mo-ra-nhieu-co-hoi-moi-cho-cang-bien-nha-may-o-viet-nam-67933.html) · [VCCI — 5G PMN ứng dụng thực tế](https://vccinews.vn/news/52291/mang-rieng-5g-pmn-%E2%80%93-nhung-ung-dung-tu-thuc-te.html)
- [Gemadept — Tiên phong chuyển đổi xanh](https://www.gemadept.com.vn/gemadept-tien-phong-chuyen-doi-xanh-kien-tao-tuong-lai-ben-vung/) · [Gemadept Dung Quất — giải Cảng xanh châu Á GPAS 2024](https://dqp.gemadept.com.vn/cang-quoc-te-gemadept-dung-quat-vinh-du-nhan-giai-thuong-cang-xanh-chau-a-gpas-2024-8695/index.html)
- [Tạp chí Ngân hàng — Điều kiện áp dụng eB/L tại VN](https://tapchinganhang.gov.vn/van-don-duong-bien-dien-tu-dieu-kien-ap-dung-tai-viet-nam-11775.html) · [ONE hợp tác WaveBL triển khai eBL](https://www.nitoda.com/n/hang-tau-one-hop-tac-voi-wave-bl-de-trien-khai-van-don-dien-tu-ebl-11782) · [Vinalines — IoT/AI/Blockchain trong vận tải biển](https://vinalines.net/ung-dung-cong-nghe-so-trong-van-tai-bien/)
- [VIMC — vinh danh Top Công nghiệp 4.0 VN 2025](https://vimc.co/en/vimc-ghi-dau-tai-top-cong-nghiep-4-0-viet-nam-2025/) · [Saigon Port — hệ sinh thái VIMC One System](https://saigonport.vn/cang-sai-gon-chuyen-doi-so-toan-dien-huong-toi-he-sinh-thai-so-vimc-one-system/) · [Báo Hải quân — SNP tiên phong số hóa khai thác cảng](https://baohaiquanvietnam.vn/tin-tuc/tan-cang-sai-gon-tien-phong-ve-so-hoa-trong-quan-ly-khai-thac-cang-bien) · [Hải quan Online — SNP giải Nhì Sáng tạo TP.HCM](https://haiquanonline.com.vn/tan-cang-sai-gon-dat-giai-nhi-giai-thuong-sang-tao-tp-ho-chi-minh-197486.html) · [SNP — Thương hiệu Quốc gia lần 8](https://saigonnewport.com.vn/tin-tuc/hoat-dong-kinh-doanh/tong-cong-ty-tan-cang-sai-gon-lan-thu-8-lien-tiep-dat-thuong-hieu-quoc-gia.html)
- [Thời báo Ngân hàng — Hải Phòng mở rộng VASSCM](https://thoibaonganhang.vn/hai-phong-mo-rong-he-thong-vasscm-tai-cang-bien-82764.html) · [B-Company — Smart port development in Vietnam](https://b-company.jp/smart-port-development-in-vietnam-a-strategic-investment-story/) · [CEH — hành trình cảng biển số VN (VTOS)](https://cehsoft.com/cau-chuyen-du-lieu-so-va-hanh-trinh-phat-trien-cang-bien-so-viet-nam/)