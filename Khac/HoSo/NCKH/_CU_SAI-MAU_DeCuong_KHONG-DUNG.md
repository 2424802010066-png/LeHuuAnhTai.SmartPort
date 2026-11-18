# THUYẾT MINH ĐỀ TÀI NGHIÊN CỨU KHOA HỌC SINH VIÊN
### Năm học 2026 – 2027 · Viện Công nghệ số, Trường Đại học Thủ Dầu Một

---

## PHẦN A. THÔNG TIN CHUNG

**1. Tên đề tài**

**Tiếng Việt:** Nghiên cứu mô hình học sâu triển khai tại biên cho dự báo sản lượng container và tự động hóa cổng cảng — thực nghiệm trên dữ liệu cảng biển Việt Nam

**Tiếng Anh:** Edge-deployable deep learning for container throughput forecasting and automated port gate operations: experiments on Vietnamese seaport data

**2. Loại hình đề tài:** Loại C (kinh phí hỗ trợ tối đa 5.000.000 đồng)

**3. Loại hình nghiên cứu:** Nghiên cứu ứng dụng

**4. Lĩnh vực nghiên cứu:** Khoa học máy tính — Trí tuệ nhân tạo / Hệ thống thông tin ứng dụng trong logistics

**5. Thời gian thực hiện:** 12 tháng (từ tháng 10/2026 đến tháng 9/2027)

**6. Chủ nhiệm đề tài**

| Mục | Nội dung |
|---|---|
| Họ và tên | Lê Hữu Anh Tài |
| MSSV | 2424802010066 |
| Lớp / Ngành | (điền lớp) — Viện Công nghệ số |
| Điện thoại | 0387 562 579 |
| Email | lehuuanhtai2020tkh@gmail.com |

**7. Thành viên tham gia**

| TT | Họ và tên | MSSV | Nội dung phụ trách |
|---|---|---|---|
| 1 | Lê Hữu Anh Tài | 2424802010066 | Chủ nhiệm — toàn bộ nội dung 1→5 |
| 2 | *(để trống — bổ sung nếu có)* | | Hỗ trợ thu thập & gán nhãn dữ liệu |

> *Ghi chú: đề tài loại C có thể thực hiện độc lập. Nếu không mời thêm thành viên, xóa dòng số 2.*

**8. Giảng viên hướng dẫn**

| Mục | Nội dung |
|---|---|
| Họ và tên | TS. Huỳnh Nguyễn Thành Luân |
| Đơn vị | Viện Công nghệ số, Trường Đại học Thủ Dầu Một |
| Email | luanhnt@tdmu.edu.vn |

**9. Đơn vị chủ trì:** Viện Công nghệ số, Trường Đại học Thủ Dầu Một

---

## PHẦN B. NỘI DUNG NGHIÊN CỨU

## 1. TÍNH CẤP THIẾT CỦA ĐỀ TÀI

### 1.1. Bối cảnh thực tiễn

Container hóa là trụ cột của chuỗi cung ứng Việt Nam. Theo số liệu thống kê của Cục Hàng hải và Đường thủy Việt Nam mà nhóm nghiên cứu đã thu thập và làm sạch (66 tháng, từ 01/2021 đến 06/2026), sản lượng container thông qua hệ thống cảng biển cả nước năm 2024 đạt **30.571 nghìn TEU**, duy trì đà tăng trưởng hai chữ số ở nhiều thời điểm. Riêng vùng kinh tế trọng điểm phía Nam — nơi đặt Trường Đại học Thủ Dầu Một — là địa bàn tập trung dày đặc các cảng cạn (ICD) và depot container phục vụ các khu công nghiệp Bình Dương, Đồng Nai.

Áp lực lớn nhất của các điểm thông quan này không nằm ở cầu bến mà nằm ở **cổng ra vào**. Kinh nghiệm của Tân cảng Cát Lái cho thấy khi áp dụng check-in điện tử bắt buộc trước 2 giờ, thời gian xe chờ tại cổng giảm từ **13 phút xuống còn 6 phút** — một cải thiện có giá trị kinh tế và môi trường rõ rệt, bởi mỗi phút xe đầu kéo nổ máy chờ đều là nhiên liệu và phát thải. Tuy nhiên, các giải pháp tạo ra kết quả đó (hệ sinh thái hơn 30 phần mềm, cổng tự động OCR, hạ tầng máy chủ AI chuyên dụng) đòi hỏi nguồn lực chỉ những tập đoàn khai thác cảng lớn mới đáp ứng được.

### 1.2. Khoảng trống cần giải quyết

Phần lớn ICD, depot và kho bãi vừa và nhỏ tại Bình Dương và lân cận **không có đủ điều kiện triển khai AI theo mô hình tập trung**: chi phí máy chủ GPU, chi phí đường truyền tải ảnh camera liên tục lên đám mây, và đặc biệt là thiếu nhân lực vận hành hạ tầng AI. Điều này tạo ra một khoảng cách công nghệ ngày càng lớn giữa nhóm khai thác cảng lớn và phần còn lại của thị trường.

Đồng thời, qua khảo sát tài liệu, nhóm nhận thấy hai khoảng trống học thuật cụ thể:

**(a) Dự báo sản lượng container Việt Nam chưa được mô hình hóa đúng với đặc thù mùa vụ bản địa.** Chuỗi sản lượng container Việt Nam chịu chi phối mạnh bởi **Tết Nguyên đán** — một hiệu ứng mùa vụ theo lịch Âm, trượt giữa tháng 1 và tháng 2 dương lịch qua các năm. Các mô hình mùa vụ chu kỳ cố định 12 tháng không nắm bắt được hiện tượng này. Trong thực nghiệm sơ bộ của nhóm (mục 5), mô hình mạng nơ-ron nhiều lớp (MLP) chỉ cải thiện khoảng **14,2% MAE** so với dự báo ngây thơ (naive), và đặc biệt **suy giảm nghiêm trọng khi dự báo nhiều bước**: sai số MAPE tăng từ 9,06% (dự báo 1 bước) lên **20,35%** (dự báo cuộn 18 tháng). Với công tác lập kế hoạch bãi và nhân lực — vốn cần tầm nhìn 3–6 tháng — mức sai số này là không dùng được.

**(b) Học sâu tại biên (edge/on-device) cho nghiệp vụ cảng chưa được đánh giá định lượng.** Các nghiên cứu về nhận dạng biển số và mã container hiện nay hầu hết giả định có GPU máy chủ. Trong khi đó, hạ tầng suy luận ngay trên trình duyệt (ONNX Runtime Web, WebAssembly, WebGPU) đã đủ chín để chạy các mô hình nhẹ, mang lại ba lợi ích phù hợp với bối cảnh doanh nghiệp vừa và nhỏ: **không cần máy chủ AI**, **dữ liệu ảnh không rời khỏi thiết bị** (bảo mật), và **vẫn hoạt động khi mất kết nối**. Tuy vậy, chưa có công bố nào đánh giá độ chính xác — độ trễ của hướng tiếp cận này trên bài toán cổng cảng với điều kiện ảnh thực địa Việt Nam (biển số hai hàng, bụi, ngược sáng, mưa).

### 1.3. Lợi thế triển khai của nhóm

Đề tài **không bắt đầu từ con số không**. Chủ nhiệm đề tài đã xây dựng và đang vận hành hệ thống **SmartPort** — nền tảng điều hành cảng cạn (ASP.NET Core .NET 10 + React, kiến trúc modular monolith hướng sự kiện, 277 endpoint, 40 bảng dữ liệu nghiệp vụ) đã tích hợp sẵn các mô-đun AI chạy trên trình duyệt (YOLO26n, CCT-S v2, Tesseract LSTM, TensorFlow.js). Hệ thống này đóng vai trò **bệ thử nghiệm (testbed) có sẵn**, cho phép đề tài tập trung toàn bộ nguồn lực vào phần nghiên cứu khoa học thay vì phải xây dựng lại hạ tầng phần mềm. Nhóm cũng đã hoàn tất bộ dữ liệu sản lượng 66 tháng và một vòng thực nghiệm đối chứng có kiểm soát (20 hạt giống ngẫu nhiên, tách tập huấn luyện/kiểm định/kiểm tra) — làm cơ sở đối sánh (baseline) đáng tin cậy cho các mô hình sẽ đề xuất.

---

## 2. TỔNG QUAN TÌNH HÌNH NGHIÊN CỨU

### 2.1. Ngoài nước

**Dự báo chuỗi thời gian bằng mô hình không gian trạng thái chọn lọc.** Năm 2023, Gu và Dao giới thiệu **Mamba** [1] — kiến trúc không gian trạng thái (State Space Model) với cơ chế *chọn lọc*: các tham số của phương trình trạng thái được điều biến theo từng đầu vào, cho phép mô hình "nhớ" hoặc "quên" có chủ đích. Mamba đạt chất lượng ngang hoặc vượt Transformer nhưng độ phức tạp chỉ **tuyến tính O(n)** thay vì bậc hai, nên rất phù hợp với thiết bị hạn chế tài nguyên. Hướng này nhanh chóng được mở rộng sang dự báo chuỗi thời gian và giao thông: **MambaTS** [2] cải tiến cơ chế quét cho dự báo dài hạn; **ST-Mamba** [3] và **ST-MambaSync** [4] áp dụng cho dự báo luồng giao thông, cho thấy khả năng nắm bắt phụ thuộc dài hạn mà không bị hiện tượng làm trơn quá mức. Pessoa và cộng sự [5] bổ sung định lượng độ bất định cho dự báo bằng Mamba, ghi nhận sai số trung bình khoảng 8% trên các bộ chuẩn.

**Dự báo và tối ưu vận hành cổng cảng.** Sun và cộng sự [6] chứng minh hệ thống đặt lịch xe (truck appointment system) giảm đáng kể thời gian quay vòng xe. Li và cộng sự [7] đề xuất mô hình học sâu tổ hợp dự báo lượng xe đến cảng theo khung thời gian linh hoạt từ 1 đến 24 giờ. Min và cộng sự [8] sử dụng học chuyển giao (transfer learning) để dự báo thời gian quay vòng xe tại các cảng **thiếu dữ liệu** — một tình huống rất gần với bối cảnh ICD Việt Nam. Chhetri và cộng sự [9] mô hình hóa thời gian lưu bãi container bằng các thuật toán học máy.

**Nhận dạng tự động tại cổng.** Hướng nghiên cứu container code recognition hiện chuyển dịch sang kiến trúc hai tầng phát hiện + đọc chữ: kết hợp YOLO với TrOCR cho mã container theo chuẩn ISO 6346 [10]; hoặc tích hợp khử nhiễu sâu để giám sát phương tiện trong cảng thông minh [11]. Về mô hình đọc ký tự nhẹ, **Compact Convolutional Transformer** [12] cho phép Transformer hoạt động hiệu quả trên tập dữ liệu nhỏ — nền tảng của mô hình CCT-S v2 chuyên đọc biển số.

**Suy luận học sâu tại trình duyệt.** Nghiên cứu năm 2024 về suy luận trong trình duyệt trên thiết bị biên [13] chỉ ra suy luận tại chỗ giúp phản hồi nhanh hơn và bảo vệ riêng tư tốt hơn nhờ loại bỏ vòng truyền lên đám mây; các tiến bộ về WebAssembly và WebGPU [14] tiếp tục thu hẹp khoảng cách hiệu năng so với suy luận phía máy chủ.

### 2.2. Trong nước

Tại Việt Nam, chuyển đổi số cảng biển đang diễn ra mạnh nhưng **chủ yếu ở dạng triển khai công nghiệp, ít công bố khoa học**. Tổng công ty Tân cảng Sài Gòn đã vận hành hệ sinh thái eSNP với hơn 30 phần mềm, cổng tự động nhận dạng biển số độ chính xác khoảng 99%, trợ lý ảo xử lý phần lớn yêu cầu khách hàng; giai đoạn 2021–2025 công bố tiết kiệm gần 600 tỷ đồng. Tuy nhiên đây là các hệ thống thương mại đóng, không công bố phương pháp, tập dữ liệu hay chỉ số đánh giá — nên không thể tái lập hoặc kế thừa trong nghiên cứu.

Về công bố học thuật, nhóm nghiên cứu Trường Đại học Thủ Dầu Một đã có kinh nghiệm đưa đề tài ứng dụng AI ra hội nghị quốc tế: Nguyen, Huynh và Vo [15] công bố nghiên cứu phân loại cảm xúc phản hồi khách hàng bằng BERT tại hội nghị **FDSE 2024** (Springer CCIS, quyển 2309). Đây là minh chứng cho thấy mô hình "đề tài NCKH sinh viên → bài báo hội nghị quốc tế có chỉ mục" là con đường khả thi tại Viện Công nghệ số, và cũng là định hướng công bố của đề tài này.

### 2.3. Kết luận rút ra và định vị của đề tài

Tổng hợp lại, đề tài xác định ba điểm định vị:

1. **Về mô hình dự báo:** các kiến trúc SSM chọn lọc đã chứng minh hiệu quả trên dữ liệu giao thông quốc tế, nhưng **chưa từng được áp dụng và đánh giá cho chuỗi sản lượng container Việt Nam** — chuỗi có đặc thù mùa vụ Tết Âm lịch mà các bộ dữ liệu chuẩn quốc tế không có.
2. **Về triển khai:** các nghiên cứu nhận dạng tại cổng đều giả định hạ tầng GPU máy chủ; **chưa có đánh giá định lượng độ chính xác — độ trễ của pipeline chạy hoàn toàn tại biên** trong điều kiện ảnh thực địa Việt Nam.
3. **Về tính tái lập:** trong nước chưa có bộ dữ liệu mở nào cho bài toán này; đề tài sẽ đóng góp bộ dữ liệu được mô tả minh bạch kèm quy tắc làm sạch.

---

## 3. MỤC TIÊU CỦA ĐỀ TÀI

### 3.1. Mục tiêu tổng quát

Nghiên cứu, đề xuất và đánh giá thực nghiệm một bộ mô hình học sâu **nhẹ, triển khai được tại biên** nhằm giải quyết hai bài toán lõi của cảng cạn Việt Nam — dự báo sản lượng container ngắn hạn và nhận dạng tự động phương tiện/container tại cổng — từ đó chứng minh rằng doanh nghiệp logistics vừa và nhỏ có thể ứng dụng AI hiệu quả mà **không cần đầu tư hạ tầng máy chủ AI chuyên dụng**.

### 3.2. Mục tiêu cụ thể

| TT | Mục tiêu cụ thể | Tiêu chí đo lường |
|---|---|---|
| MT1 | Xây dựng và công bố bộ dữ liệu phục vụ nghiên cứu | (a) Chuỗi sản lượng container Việt Nam ≥ 66 tháng, có kiểm tra nhất quán; (b) Bộ ảnh cổng thực địa ≥ 1.000 ảnh đã gán nhãn biển số/mã container |
| MT2 | Đề xuất mô hình dự báo sản lượng dựa trên SSM chọn lọc kết hợp đặc trưng lịch Âm (Tết) | Giảm **≥ 15% MAE** so với mô hình tốt nhất hiện có của nhóm (MLP, MAE = 259,8 nghìn TEU); và giảm **≥ 25% MAPE** ở kịch bản dự báo cuộn nhiều bước (hiện 20,35%) |
| MT3 | Xây dựng pipeline nhận dạng hai tầng chạy hoàn toàn trên trình duyệt | Độ chính xác cấp ký tự **≥ 95%**; độ trễ **≤ 1,5 giây/ảnh** trên máy tính văn phòng phổ thông không GPU rời |
| MT4 | Tích hợp vào hệ thống SmartPort và đánh giá hiệu quả vận hành | Đo được thời gian xử lý một lượt xe qua cổng trước/sau khi áp dụng, trên kịch bản mô phỏng có dữ liệu thật |
| MT5 | Công bố kết quả khoa học | Tối thiểu **01 bài báo** gửi hội nghị/tạp chí chuyên ngành (định hướng FDSE hoặc hội thảo quốc gia về CNTT) |

---

## 4. ĐỐI TƯỢNG VÀ PHẠM VI NGHIÊN CỨU

### 4.1. Đối tượng nghiên cứu

- Các mô hình học sâu cho chuỗi thời gian: mô hình không gian trạng thái chọn lọc (Selective SSM/Mamba), mạng hồi tiếp (LSTM/GRU), Transformer và các mô hình đối chứng thống kê.
- Các mô hình thị giác máy tính nhẹ cho phát hiện đối tượng và nhận dạng ký tự quang học trong điều kiện triển khai tại biên.
- Quy trình nghiệp vụ cổng cảng cạn: tiếp nhận xe, đối chiếu lệnh giao nhận, ghi nhận container ra/vào bãi.

### 4.2. Phạm vi nghiên cứu

**Phạm vi nội dung:** Đề tài **giới hạn ở hai bài toán** đã nêu (dự báo sản lượng và nhận dạng tại cổng). Các bài toán khác của khai thác cảng — phân bổ cầu bến tối ưu, lập kế hoạch xếp dỡ tàu, điều độ cẩu — **không thuộc phạm vi** đề tài này.

**Phạm vi dữ liệu:**
- Dữ liệu dự báo: chuỗi sản lượng container thông qua cảng biển Việt Nam theo tháng, kỳ 01/2021 – 06/2026 (66 tháng), nguồn Cục Hàng hải và Đường thủy Việt Nam [16]; mở rộng cập nhật trong thời gian thực hiện đề tài.
- Dữ liệu thị giác: ảnh chụp thực địa tại cổng ICD/depot khu vực Bình Dương, bổ sung ảnh từ hệ thống SmartPort đang vận hành.

**Phạm vi triển khai:** Mô hình phải chạy được ở chế độ suy luận trên trình duyệt web của máy tính văn phòng phổ thông (CPU, không GPU rời). Giai đoạn **huấn luyện** vẫn được thực hiện ngoại tuyến trên máy tính cá nhân hoặc dịch vụ đám mây thuê ngắn hạn — đây là ranh giới cần nêu rõ để tránh hiểu nhầm.

**Phạm vi thời gian:** 12 tháng, từ 10/2026 đến 9/2027.

---

## 5. KẾT QUẢ NGHIÊN CỨU SƠ BỘ CỦA NHÓM

Để chứng minh tính khả thi, nhóm đã thực hiện một vòng thực nghiệm đối chứng trên bộ dữ liệu 66 tháng. Thiết kế thực nghiệm: chia tập theo thời gian (36 tháng huấn luyện / 12 tháng kiểm định / 18 tháng kiểm tra), lặp lại với **20 hạt giống ngẫu nhiên** và báo cáo trung bình ± độ lệch chuẩn, đánh giá ở hai kịch bản (dự báo 1 bước và dự báo cuộn 18 bước).

**Bảng 1. Kết quả đối chứng trên tập kiểm tra (18 tháng), đơn vị MAE/RMSE: nghìn TEU**

| Mô hình | MAE | RMSE | MAPE (%) |
|---|---:|---:|---:|
| Naive — lấy giá trị tháng trước | 302,9 | 354,1 | 10,65 |
| Naive theo mùa — cùng kỳ năm trước | 335,3 | 388,0 | 11,47 |
| Hồi quy tuyến tính theo thời gian | 321,5 | 384,8 | 10,56 |
| Hồi quy tuyến tính trên 3 độ trễ — AR(3) | 286,6 | 331,8 | 9,64 |
| **MLP (3 độ trễ)** — tốt nhất hiện tại | **259,8 ± 29,6** | **331,2** | **9,06 ± 1,04** |
| MLP (3 độ trễ + trễ 12 + đặc trưng Tết) | 270,7 ± 71,3 | 329,4 | 9,33 ± 2,48 |

**Bảng 2. Kịch bản dự báo cuộn 18 tháng — điểm yếu cần khắc phục**

| Mô hình | MAE | RMSE | MAPE (%) |
|---|---:|---:|---:|
| MLP (3 độ trễ) | 606,1 | 782,7 | **20,35** |
| MLP (3 độ trễ + trễ 12 + đặc trưng Tết) | 432,0 | 506,9 | **14,69** |

**Ba kết luận rút ra, định hình hướng nghiên cứu của đề tài:**

1. **Mức cải thiện hiện tại còn khiêm tốn.** Mô hình học sâu tốt nhất (MLP) chỉ giảm 14,2% MAE so với dự báo naive — chưa đủ thuyết phục để đưa vào quyết định vận hành. Cần kiến trúc chuyên cho chuỗi thời gian thay vì mạng nơ-ron tổng quát.
2. **Dự báo nhiều bước là điểm gãy.** MAPE tăng gấp hơn hai lần khi chuyển từ dự báo 1 bước sang dự báo cuộn. Đây chính là bài toán mà cơ chế trạng thái ẩn của SSM được kỳ vọng xử lý tốt hơn nhờ duy trì bộ nhớ dài hạn theo cách có chọn lọc.
3. **Đặc trưng Tết có giá trị nhưng chưa được khai thác đúng.** Việc thêm đặc trưng Tết làm **xấu đi** dự báo 1 bước (MAE 259,8 → 270,7) nhưng **cải thiện mạnh** dự báo cuộn (MAPE 20,35% → 14,69%). Điều này cho thấy tín hiệu mùa vụ Âm lịch là thật và đáng giá, nhưng cách mã hóa nhị phân thô sơ hiện tại chưa phù hợp — cần thiết kế đặc trưng lịch Âm tinh vi hơn (khoảng cách ngày tới Tết, tỷ lệ ngày làm việc trong tháng, hiệu ứng trước/sau Tết bất đối xứng). **Đây là đóng góp khoa học mang tính bản địa rõ nét nhất của đề tài.**

---

## 6. CÁCH TIẾP CẬN VÀ PHƯƠNG PHÁP NGHIÊN CỨU

### 6.1. Cách tiếp cận

Đề tài tiếp cận theo hướng **nghiên cứu thực nghiệm có đối chứng, gắn với hệ thống thật**. Thay vì xây dựng mô hình trên dữ liệu mô phỏng, đề tài sử dụng dữ liệu thống kê chính thức của cơ quan quản lý nhà nước và dữ liệu vận hành từ hệ thống SmartPort, rồi đưa mô hình quay ngược trở lại hệ thống để đo hiệu quả thực tế. Nguyên tắc xuyên suốt là **mọi cải tiến phải được chứng minh bằng chỉ số định lượng so với đường cơ sở công bố công khai**.

### 6.2. Các phương pháp nghiên cứu cụ thể

**(1) Phương pháp nghiên cứu tài liệu.** Tổng hợp và phân tích hệ thống các công bố quốc tế giai đoạn 2022–2026 về SSM/Mamba, dự báo vận hành cảng, nhận dạng tại cổng và suy luận tại biên; khảo sát các hệ thống đang vận hành trong nước để xác định khoảng trống.

**(2) Phương pháp thu thập và xử lý dữ liệu.** Thu thập báo cáo thống kê tháng từ Cục Hàng hải và Đường thủy Việt Nam; xử lý theo bộ quy tắc làm sạch đã thiết lập (lấy hiệu lũy kế thay vì số ước thực hiện; ưu tiên bản công bố muộn nhất đã hiệu chỉnh; không trộn hai lần công bố khi lấy hiệu; kiểm tra nhất quán tổng xuất–nhập–nội địa). Với dữ liệu ảnh: chụp thực địa có sự đồng ý của đơn vị, gán nhãn thủ công theo chuẩn ISO 6346 cho mã container và quy cách biển số Việt Nam.

**(3) Phương pháp mô hình hóa và thực nghiệm đối chứng.** Xây dựng mô hình đề xuất và so sánh với đầy đủ các đường cơ sở từ đơn giản đến phức tạp (naive, naive theo mùa, ARIMA/SARIMA, hồi quy tuyến tính, AR, MLP, LSTM, GRU, Transformer). Quy trình chuẩn: tách tập theo thời gian (không xáo trộn ngẫu nhiên để tránh rò rỉ dữ liệu tương lai), lặp nhiều hạt giống, báo cáo trung bình kèm độ lệch chuẩn, đánh giá bằng MAE, RMSE, MAPE ở cả kịch bản một bước và nhiều bước.

**(4) Phương pháp nghiên cứu loại trừ (ablation study).** Tách riêng đóng góp của từng thành phần đề xuất (cơ chế chọn lọc, đặc trưng lịch Âm, độ dài cửa sổ trượt) để xác định thành phần nào thực sự tạo ra cải thiện — tránh kết luận sai khi cải thiện chỉ đến từ việc tăng số tham số.

**(5) Phương pháp đo hiệu năng triển khai.** Đo độ trễ suy luận, dung lượng mô hình, mức tiêu thụ bộ nhớ trên cấu hình máy tính văn phòng phổ thông; lặp lại phép đo nhiều lần và loại bỏ lần chạy khởi động để bảo đảm tính ổn định của số liệu.

**(6) Phương pháp thực nghiệm hệ thống.** Tích hợp mô hình vào SmartPort, xây dựng kịch bản mô phỏng luồng xe qua cổng dựa trên log vận hành thật, đo thời gian xử lý trung bình và tỷ lệ phải can thiệp thủ công trước/sau khi áp dụng.

### 6.3. Mô hình đề xuất (dự kiến)

**Mô hình dự báo — SSM chọn lọc có nhận biết lịch Âm:**

Mô hình kế thừa phương trình trạng thái của kiến trúc Mamba, trong đó trạng thái ẩn được cập nhật theo:

```
h(t) = Ā(Δt) · h(t−1) + B̄(Δt) · x(t)
y(t) = C · h(t) + D · x(t)
```

với bước chọn lọc `Δt = softplus(W_Δ · [x(t), c(t)] + b_Δ)` được điều biến **không chỉ bởi giá trị chuỗi `x(t)` mà còn bởi vector đặc trưng lịch `c(t)`**. Vector `c(t)` là đóng góp bản địa của đề tài, dự kiến gồm: khoảng cách ngày từ giữa tháng đến mùng 1 Tết Âm lịch, tỷ lệ ngày làm việc thực tế trong tháng, và cờ phân biệt tháng trước Tết (tích trữ, đẩy hàng) với tháng sau Tết (đình trệ). Cách thiết kế này cho phép mô hình **tự học khi nào cần "quên" lịch sử** (ví dụ tháng Tết là dị thường, không nên dùng để suy ra tháng kế) — điều mà mô hình mùa vụ chu kỳ cố định không làm được.

**Pipeline nhận dạng tại cổng — hai tầng, đa engine đối chiếu:**

```
Ảnh cổng → [Tầng 1] YOLO26n (ONNX Runtime Web)  → khoanh vùng xe / biển số / mặt container
                            ↓
         → [Tầng 2] CCT-S v2  ─┐
                               ├→ đối chiếu chéo → kết quả + độ tin cậy
         → [Tầng 2'] Tesseract ┘     (hai engine độc lập; trùng khớp mới tự động chấp nhận)
                            ↓
              Hậu xử lý: biểu thức chính quy biển số VN + kiểm tra số kiểm tra ISO 6346
```

Điểm nhấn thiết kế là **cơ chế đối chiếu chéo hai engine độc lập**: chỉ khi hai engine cho cùng kết quả thì hệ thống mới tự động thông qua, ngược lại chuyển cho nhân viên xác nhận. Cách làm này đánh đổi một phần tỷ lệ tự động hóa để lấy **độ tin cậy** — phù hợp với nghiệp vụ cổng cảng, nơi một mã container đọc sai gây hậu quả dây chuyền lên bãi và hóa đơn. Đề tài sẽ đo định lượng sự đánh đổi này (tỷ lệ tự động hóa so với tỷ lệ sai sót còn lại).

---

## 7. NỘI DUNG NGHIÊN CỨU VÀ TIẾN ĐỘ THỰC HIỆN

### 7.1. Các nội dung nghiên cứu

**Nội dung 1 — Tổng quan và xây dựng khung lý thuyết.** Hệ thống hóa các công bố về SSM/Mamba, dự báo vận hành cảng, nhận dạng tại cổng và suy luận tại biên; xác lập bộ chỉ số đánh giá và các đường cơ sở đối chứng.

**Nội dung 2 — Xây dựng bộ dữ liệu.** Hoàn thiện và mở rộng chuỗi sản lượng container (cập nhật tới thời điểm thực hiện); thu thập, gán nhãn và kiểm định chất lượng bộ ảnh cổng thực địa; biên soạn tài liệu mô tả dữ liệu và quy tắc làm sạch phục vụ tái lập.

**Nội dung 3 — Nghiên cứu mô hình dự báo sản lượng.** Cài đặt mô hình SSM chọn lọc có nhận biết lịch Âm; thiết kế và kiểm chứng bộ đặc trưng lịch; thực nghiệm đối chứng đầy đủ; nghiên cứu loại trừ để phân tách đóng góp từng thành phần.

**Nội dung 4 — Nghiên cứu pipeline nhận dạng tại biên.** Tinh chỉnh mô hình phát hiện và đọc ký tự trên dữ liệu thực địa Việt Nam; xây dựng cơ chế đối chiếu chéo và hậu xử lý theo chuẩn; đo độ chính xác và độ trễ; phân tích các trường hợp thất bại điển hình (ngược sáng, bụi, biển số hai hàng, vỏ container han gỉ).

**Nội dung 5 — Tích hợp hệ thống, đánh giá hiệu quả và công bố.** Đưa hai mô hình vào hệ thống SmartPort; xây dựng kịch bản mô phỏng và đo KPI vận hành; viết bài báo khoa học; hoàn thiện báo cáo tổng kết và nghiệm thu.

### 7.2. Tiến độ thực hiện

| TT | Nội dung công việc | Thời gian | Sản phẩm của giai đoạn |
|---|---|---|---|
| 1 | Tổng quan tài liệu, chuẩn hóa khung đánh giá | 10/2026 – 11/2026 | Báo cáo tổng quan; danh mục đường cơ sở |
| 2 | Xây dựng và kiểm định bộ dữ liệu | 11/2026 – 01/2027 | Bộ dữ liệu + tài liệu mô tả |
| 3 | Mô hình dự báo: cài đặt, thực nghiệm, ablation | 01/2027 – 04/2027 | Bảng kết quả đối chứng; mã nguồn thực nghiệm |
| 4 | Pipeline nhận dạng tại biên: tinh chỉnh, đo đạc | 03/2027 – 06/2027 | Mô hình đã tinh chỉnh; báo cáo độ chính xác – độ trễ |
| 5 | Tích hợp SmartPort, đo KPI vận hành | 06/2027 – 07/2027 | Bản tích hợp chạy được; số liệu trước/sau |
| 6 | Viết bài báo, gửi hội nghị | 07/2027 – 08/2027 | Bản thảo bài báo đã gửi |
| 7 | Hoàn thiện báo cáo tổng kết, nghiệm thu | 08/2027 – 09/2027 | Báo cáo tổng kết; hồ sơ nghiệm thu |

> *Lưu ý tiến độ: các giai đoạn 3–4 và 4–5 được thiết kế gối đầu một tháng để dự phòng rủi ro chậm ở khâu thu thập dữ liệu thực địa — khâu phụ thuộc vào lịch làm việc của doanh nghiệp.*

---

## 8. SẢN PHẨM CỦA ĐỀ TÀI

### 8.1. Sản phẩm khoa học

| TT | Tên sản phẩm | Yêu cầu chất lượng | Số lượng |
|---|---|---|---|
| 1 | Bài báo khoa học | Hội nghị/tạp chí chuyên ngành CNTT; định hướng hội nghị quốc tế có chỉ mục (FDSE) hoặc hội thảo quốc gia | 01 |
| 2 | Báo cáo tổng kết đề tài | Theo mẫu quy định của Trường | 01 |

### 8.2. Sản phẩm đào tạo và ứng dụng

| TT | Tên sản phẩm | Yêu cầu chất lượng | Số lượng |
|---|---|---|---|
| 3 | Bộ dữ liệu nghiên cứu | Chuỗi sản lượng ≥ 66 tháng có kiểm tra nhất quán; bộ ảnh cổng ≥ 1.000 ảnh đã gán nhãn; kèm tài liệu mô tả cho phép tái lập | 01 bộ |
| 4 | Mô hình dự báo sản lượng | Đạt MT2: giảm ≥ 15% MAE so với đường cơ sở tốt nhất; kèm mã nguồn thực nghiệm | 01 |
| 5 | Pipeline nhận dạng tại biên | Đạt MT3: chính xác cấp ký tự ≥ 95%, độ trễ ≤ 1,5 giây/ảnh trên máy không GPU rời | 01 |
| 6 | Mô-đun tích hợp trên hệ thống SmartPort | Chạy được, có hướng dẫn sử dụng, có số liệu đo KPI trước/sau | 01 |
| 7 | Góp phần đào tạo | Hỗ trợ chủ nhiệm đề tài hoàn thành khóa luận/đồ án tốt nghiệp cùng hướng | 01 |

### 8.3. Địa chỉ ứng dụng dự kiến

- Các cảng cạn (ICD), depot container và doanh nghiệp kho vận vừa và nhỏ trên địa bàn tỉnh Bình Dương và vùng kinh tế trọng điểm phía Nam.
- Viện Công nghệ số, Trường Đại học Thủ Dầu Một: làm học liệu minh họa cho các học phần Trí tuệ nhân tạo, Học máy, Hệ thống thông tin.

---

## 9. HIỆU QUẢ DỰ KIẾN

**Về khoa học và công nghệ.** Đề tài đóng góp: (i) mô hình dự báo chuỗi thời gian có tích hợp đặc trưng lịch Âm — hướng tiếp cận có giá trị cho mọi chuỗi kinh tế Việt Nam chịu ảnh hưởng Tết, không chỉ riêng ngành cảng; (ii) bằng chứng định lượng đầu tiên về khả năng và giới hạn của suy luận học sâu tại biên cho nghiệp vụ cổng cảng trong điều kiện Việt Nam; (iii) bộ dữ liệu mô tả minh bạch, cho phép các nghiên cứu sau đối chứng.

**Về kinh tế – xã hội.** Giảm thời gian xe chờ tại cổng đồng nghĩa giảm nhiên liệu tiêu hao và khí thải từ xe đầu kéo nổ máy chờ. Quan trọng hơn, do mô hình chạy trên thiết bị sẵn có, chi phí triển khai gần như chỉ còn chi phí phần mềm — **hạ thấp rào cản công nghệ cho nhóm doanh nghiệp vừa và nhỏ**, vốn là phần đông của thị trường logistics Bình Dương. Việc dữ liệu ảnh không rời khỏi thiết bị cũng giải quyết được e ngại về bảo mật thông tin khách hàng — một rào cản tâm lý thực tế khi doanh nghiệp cân nhắc ứng dụng AI.

**Về đào tạo.** Đề tài rèn luyện cho sinh viên năng lực nghiên cứu bài bản: đọc và tổng hợp tài liệu quốc tế, thiết kế thực nghiệm có đối chứng, viết và công bố kết quả khoa học. Kết quả đề tài là nền tảng trực tiếp cho khóa luận tốt nghiệp và có thể tiếp tục phát triển ở bậc học cao hơn.

---

## 10. DỰ TOÁN KINH PHÍ

**Tổng kinh phí đề nghị hỗ trợ: 5.000.000 đồng** (Năm triệu đồng chẵn) — đề tài loại C.

| TT | Nội dung chi | Diễn giải | Thành tiền (đồng) |
|---|---|---|---:|
| 1 | Chi công lao động chuyên môn | Thù lao thực hiện các nội dung nghiên cứu 1–5 | 2.000.000 |
| 2 | Chi thu thập và xử lý số liệu | Khảo sát thực địa tại ICD/depot (đi lại, liên hệ đơn vị); công gán nhãn bộ ảnh ≥ 1.000 ảnh | 1.200.000 |
| 3 | Chi thuê dịch vụ kỹ thuật | Thuê tài nguyên tính toán đám mây phục vụ huấn luyện mô hình (theo giờ, ngắn hạn); tên miền và lưu trữ bản demo | 800.000 |
| 4 | Chi công bố khoa học | Lệ phí gửi bài, in ấn poster/tài liệu trình bày hội nghị | 500.000 |
| 5 | Chi văn phòng phẩm, in ấn tài liệu | In đề cương, báo cáo tiến độ, báo cáo tổng kết, đóng quyển nghiệm thu | 500.000 |
| | **TỔNG CỘNG** | | **5.000.000** |

---

## 11. TÀI LIỆU THAM KHẢO

[1] A. Gu and T. Dao, "Mamba: Linear-Time Sequence Modeling with Selective State Spaces," *arXiv preprint arXiv:2312.00752*, 2023.

[2] X. Cai et al., "MambaTS: Improved Selective State Space Models for Long-term Time Series Forecasting," in *Advances in Neural Information Processing Systems (NeurIPS)*, 2024.

[3] Z. Shao et al., "ST-Mamba: Spatial-Temporal Selective State Space Model for Traffic Flow Prediction," *arXiv preprint arXiv:2404.13257*, 2024.

[4] Z. Shao et al., "ST-MambaSync: Complement the power of Mamba and Transformer for time series forecasting," *Information Fusion*, 2025.

[5] P. Pessoa et al., "Mamba time series forecasting with uncertainty quantification," *arXiv preprint arXiv:2503.10873*, 2025.

[6] S. Sun et al., "Reducing external container trucks' turnaround time in ports," *Computers & Industrial Engineering*, 2022.

[7] N. Li et al., "Prediction of delivery truck arrivals at container terminals," *Maritime Economics & Logistics*, vol. 26, no. 4, 2024. doi: 10.1057/s41278-024-00304-1

[8] Y. Min et al., "Freight Truck Turnaround Time Prediction at Container Ports Using Transfer Learning," *Journal of Marine Science and Engineering*, vol. 14, no. 8, art. 727, 2026.

[9] P. Chhetri et al., "Container dwell time predictive modelling: an application of machine learning," *Maritime Policy & Management*, 2025. doi: 10.1080/03088839.2025.2501010

[10] "Automated Container Code Recognition with YOLOv7 and TrOCR," *Preprints.org*, 202508.0232, 2025.

[11] "Advanced vehicle monitoring in smart port utilizing deep denoising techniques," *Ain Shams Engineering Journal*, 2025.

[12] A. Hassani et al., "Escaping the Big Data Paradigm with Compact Transformers," *arXiv preprint arXiv:2104.05704*, 2021.

[13] "Empowering In-Browser Deep Learning Inference on Edge Devices," *arXiv preprint*, 2024.

[14] Chrome Developers, "WebAssembly and WebGPU enhancements for faster Web AI," 2024. [Trực tuyến]. Địa chỉ: https://developer.chrome.com

[15] V. Nguyen, L. N. T. Huynh, and T. Vo, "Optimizing Customer Feedback Analysis with BERT-Based Sentiment Classification: A Case Study of Toyota Dong Sai Gon," in *Future Data and Security Engineering (FDSE 2024)*, CCIS vol. 2309, Springer, 2024, pp. 269–281. doi: 10.1007/978-981-96-0434-0_19

[16] Cục Hàng hải và Đường thủy Việt Nam, "Thống kê sản lượng hàng hóa thông qua cảng biển," 2021–2026. [Trực tuyến]. Địa chỉ: https://vimawa.gov.vn/vi/thong-ke

---

|  |  |
|---|---|
| **Xác nhận của đơn vị** | **Bình Dương, ngày ... tháng ... năm 2026** |
| | |
| **Giảng viên hướng dẫn** | **Chủ nhiệm đề tài** |
| *(Ký, ghi rõ họ tên)* | *(Ký, ghi rõ họ tên)* |
| | |
| | |
| TS. Huỳnh Nguyễn Thành Luân | Lê Hữu Anh Tài |
