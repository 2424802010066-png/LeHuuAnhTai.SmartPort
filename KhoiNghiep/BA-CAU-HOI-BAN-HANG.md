# BẢN TRẢ LỜI 3 CÂU HỎI — SmartPort (bản 2, sau phản biện)

> Bản này bỏ hết chỗ bị bắt bẻ đúng: bỏ cách chào "59 chức năng", bỏ câu "chi phí AI gần bằng 0", bỏ neo giá vào lương, bỏ cam kết "phản hồi 30 phút", bỏ mọi con số mượn của Cát Lái / Hải Phòng. Giữ lại đúng những gì chứng minh được bằng động tác trước mặt khách.

## VIỆC PHẢI XONG TRƯỚC KHI ĐƯỢC GỌI LÀ "ĐI CHÀO HÀNG"

Chưa xong 4 việc này thì mọi buổi gặp chỉ được gọi là **khảo sát**, không được gọi là chào hàng, và tuyệt đối không đưa bảng giá.

| # | Việc | Vì sao chặn | Thời gian |
|---|---|---|---|
| 1 | Xin 2–3 giờ **video cổng depot thật** (ban ngày / ban đêm / trời mưa), chạy ALPR, đếm tỷ lệ đọc đúng theo từng điều kiện | Bốn thứ đang tựa lên giả định "camera sẵn có đủ đọc biển": lời chào "cắm vào camera anh đang có", định vị giá, tờ A4 hai tuần, cam kết KPI. Chưa đo mà đã in bảng giá là làm ngược | 1 buổi chiều |
| 2 | **Đọc được camera IP/RTSP** của đầu ghi Hikvision/Dahua, không bắt khách mua webcam | Depot nào cũng đã có camera. Bắt vứt đi mua lại là tự chặn đường. "Cần phần mềm bắc cầu" = chưa làm được | 1–2 tuần |
| 3 | **Pháp nhân xuất được hóa đơn** (hộ kinh doanh hoặc công ty TNHH) | Chủ hoặc kế toán hỏi trong 10 phút đầu: "công ty em tên gì, xuất hóa đơn được không". Trả lời "em đang làm" là mất phiên đó và mất luôn lời giới thiệu | Trước buổi gặp đầu tiên |
| 4 | **Chốt đường giấy phép mô hình AI** — mua Ultralytics Enterprise, hoặc đổi sang RT-DETR / YOLOX / DAMO-YOLO rồi huấn luyện lại và đo lại trên biển số Việt Nam | YOLO đang là AGPL-3.0. Kiến trúc client-side làm chuyện này **nặng hơn** chứ không nhẹ hơn: mô hình và mã AI được phân phối xuống trình duyệt người dùng qua mạng — chạm đúng điều khoản mạng của AGPL. Hôm nay khách có gật cũng chưa ký được | Vài tuần, phải nằm trong lịch |

---

# CÂU 1 — SẢN PHẨM MANG LẠI GIÁ TRỊ GÌ, CHO AI, ĐO BẰNG GÌ?

## 1.1. Câu chốt một dòng (bản mới)

> "SmartPort là **quyển sổ tự ghi** của cảng anh: mỗi lượt xe, mỗi container, mỗi lần cãi nhau với khách đều có giờ, có tên người, có ảnh — và nó **tự sinh ra đúng những file mà hãng tàu đang bắt anh gõ tay mỗi ngày**."

Ba chữ phải nhớ khi bị hỏi hơn cái đầu ghi camera ở chỗ nào: **lệnh — chứng từ — bằng chứng**. AI chỉ là cách ghi cho nhanh, không phải thứ đem bán.

## 1.2. Trước hết phải thú nhận và sửa: sản phẩm đang hình dạng CẢNG BIỂN, khách nhắm là DEPOT RỖNG

Đây là lỗ hổng nặng nhất, phải nói ra ngay đầu buổi chứ không giấu.

| Việc depot rỗng làm mỗi ngày | SmartPort hôm nay |
|---|---|
| Kiểm vỏ theo chuẩn hãng tàu, lập **EOR** (báo giá sửa) gửi hãng chờ duyệt, quản lý **M&R** | **CHƯA CÓ.** Mới có soi móp méo bằng AI + lưu ảnh mốc giờ |
| Tồn rỗng tách theo **hãng tàu – size/type – tình trạng A/B/C** | **CHƯA CÓ** (sơ đồ bãi hiện tổng quát, không tách theo hãng) |
| Lệnh cấp rỗng (booking release) từ hãng tàu | **CHƯA CÓ** |
| **Báo cáo gate-in/gate-out về hãng tàu** (chuẩn ngành là EDI CODECO) | **CHƯA CÓ.** Chính tài liệu khảo sát tự ghi EDI hãng tàu là khoảng trống lớn nhất, ECM liên depot là "CHƯA" |
| Phí nâng/hạ, phí vệ sinh vỏ | Có khung phí, phải cấu hình lại |
| Cổng: đọc biển, đối chiếu lệnh + PIN, ảnh vỏ, EIR, phí lưu bãi tự động, hồ sơ container | **ĐÃ CÓ VÀ MẠNH** |
| Tàu, cầu bến, Gantt 9 ngày, VGM 2 lượt, kiểm đếm nghìn kiện, reefer | Có, nhưng **depot rỗng không dùng** — cắt khỏi bài chào |

**Hai đường đi, phải chọn dứt khoát, không đi cả hai:**

- **Đường A — vẫn nhắm depot rỗng:** coi 4 việc trên là **sản phẩm bàn giao của kỳ thí điểm**, lấy đúng mẫu EOR và mẫu báo cáo tồn của khách làm đề bài. Làm cái rẻ nhất trước: **xuất đúng file Excel/PDF mà nhân viên đang gõ tay gửi hãng tàu** — đúng cột, đúng tên trường, đúng mã. Vài ngày công, và nó biến SmartPort từ "thêm một thứ phải nhập" thành "thứ giúp bớt nhập". EDI CODECO thật chỉ làm khi có một hãng cụ thể yêu cầu bằng văn bản.
- **Đường B — đổi khách sang chỗ sản phẩm đã khớp sẵn:** **cổng nhà máy / kho hàng trong KCN, ICD nhỏ, bãi xe container** — cùng bài toán cổng (biển số + lệnh + PIN + cân + ảnh bằng chứng), số lượng gấp nhiều lần depot, không cần M&R, không cần EDI hãng tàu.

Trước khi chào tiếp: **xin đứng ở một depot hai ngày, không lấy tiền, cầm sổ ghi từng bước**. Buổi sau mở đầu bằng "em đã đứng ở depot của anh Ba hai ngày, em thấy…" thì mới có người nghe.

## 1.3. Bỏ cách chào "59 chức năng" — thay bằng "10 màn hình anh mở mỗi ngày"

Nói "59 chức năng" thì chủ depot nghĩ hai điều: *cái này chắc mắc lắm*, và *rộng mà không sâu*. Mang một tờ A4 duy nhất, đúng nghiệp vụ của họ:

1. Màn hình cổng (đọc biển → khớp lệnh → ảnh vỏ → mở barrier)
2. Tạo lệnh giao nhận: số lệnh + mã PIN
3. Danh sách xe đang trong bãi / đã ra
4. Sơ đồ bãi và vị trí hạ
5. Hồ sơ container 360 (tra lại 3 tháng trước)
6. In EIR song ngữ
7. Phí lưu bãi tự động theo biểu giá
8. Kho ảnh bằng chứng (cổng, kiểm vỏ)
9. Báo cáo ngày / báo cáo gửi hãng tàu
10. Cổng tra cứu cho khách của anh

## 1.4. Ba lý do mua — đã đổi trục từ "tiết kiệm" sang "tiền vô"

| # | Lý do | Cách nói khi ngồi với chủ |
|---|---|---|
| 1 | **Thu đủ tiền lưu bãi** | "Tháng rồi anh có container nào quá hạn mà quên tính, hoặc nể khách nên không tính không? Bao nhiêu ngày? Phần mềm tính đủ, không quên, không nể — chỗ đó là **tiền vô**, không phải tiền tiết kiệm trên giấy." |
| 2 | **Ảnh mốc giờ để đòi tiền và để khỏi phải đền** | "Vỏ móp lúc vào hay lúc ra — có ảnh thì anh đòi được; không ảnh thì anh nuốt. Vụ gần nhất anh mất bao nhiêu?" |
| 3 | **Tự sinh file hãng tàu đang bắt gõ tay** | "Cái em bán không phải cái mắt đọc biển số. Cái em bán là quyển sổ tự ghi, và cái máy in ra đúng thứ hãng tàu đòi." |

Ba câu này đều **hỏi khách tự điền số của họ**, không có một con số nào do mình bịa.

## 1.5. Trang "Giá trị AI · ROI" phải đập ra ba khối, cấm cộng lại thành một con số

| Khối | Đơn vị | Nội dung | Cấm |
|---|---|---|---|
| A. **Sự thật** | lượt, vụ, tấm | AI đã đọc bao nhiêu biển số, chặn bao nhiêu lệnh sai, lưu bao nhiêu ảnh | — |
| B. **Tiền vô** | đồng | Phí lưu bãi hệ thống tính được mà trước đây dễ sót | Chỉ tính khi đối chiếu được với sổ cũ |
| C. **Giờ công** | giờ | Để nguyên đơn vị giờ, kèm dòng chữ thật: *"chỉ thành tiền nếu anh dùng số giờ này chạy thêm xe mà không thêm người"* | **Không nhân ra tiền** |
| D. **Rủi ro chặn được** | số vụ | Kèm ô để chủ tự điền "vụ tranh chấp gần nhất của anh là bao nhiêu tiền" | **Không quy ra tiền hộ khách** |

**Phải xóa khỏi công thức:** dòng "7 phút chờ cổng (Cát Lái 13→6 phút)" — mượn số của cảng nghìn xe/ngày áp cho depot 100 xe/ngày là thổi phồng có hệ thống; và định mức mặc định "2 phút/xe ghi tay" — anh Tuấn ở cổng ghi 15 giây. Mọi định mức để **trống hoặc bằng 0** cho tới khi bấm giờ tại chính cảng đó, và ghi nguồn ngay trên màn hình: *"đo tại cổng ngày 12–26/9, n = 340 lượt"*.

## 1.6. Không nói "tiết kiệm X triệu" — nói NGƯỠNG HÒA VỐN, và dám cộng tổng

Mang theo một tờ **tổng chi phí năm đầu**, có báo giá thật, không ước lượng. Chủ doanh nghiệp coi thường người bán không dám cộng tổng.

| Khoản | Đơn giá | Số lượng | Thành tiền |
|---|---|---|---|
| Thuê phần mềm × 12 tháng | … | | |
| Máy trạm cổng (theo số làn) | … | | |
| Camera / xoay góc / thêm đèn (theo kịch bản A–B–C ở Câu 2) | … | | |
| UPS + công kéo mạng dây | … | | |
| Phí triển khai một lần | … | | |
| Ngày công anh em đi đào tạo | … | | |
| **TỔNG NĂM ĐẦU** | | | **Y** |

Rồi nói đúng một câu: *"Chi phí năm đầu là Y. Anh hòa vốn nếu năm nay thu lại được **N ngày phí lưu bãi bị sót**, hoặc đòi được **một vụ sửa vỏ** nhờ có ảnh. Anh tự thấy con số N đó có dễ đạt không."*

## 1.7. Thứ tuyệt đối không hứa

1. **Độ chính xác và tốc độ** — không nói 99%, không nói "10–25 giây/xe". Đó là số công bố của Autogate Cát Lái và Smart Gate Hải Phòng. Cách nói đúng: *"em chưa đo ở cổng ngoài trời, cho em hai tuần đo tại cổng anh rồi lấy số của anh."*
2. **Số tiền tiết kiệm cụ thể** — chỉ đưa công thức và để khách tự điền định mức, lương thực tế của họ.
3. **Các lớp kết nối ra ngoài** — chưa có: EDI/CODECO hãng tàu, eDO trực tiếp từ hãng, hải quan VASSCM/VNACCS, hóa đơn điện tử hợp lệ theo NĐ 70/2025 (mới dừng ở xuất Excel/MISA), GPS sau khi rời cổng, WMS kho ngoại quan, ECM liên depot, sà lan. **Không viết "tích hợp hãng tàu" vào hợp đồng đầu tiên** khi chưa có — đó là điều khoản dễ chết nhất vì phụ thuộc bên thứ ba mình không có quan hệ.
4. **"Blockchain"** ở đây là hash nối chuỗi trong CSDL để phát hiện sửa đổi — **không phải** vận đơn điện tử eB/L có giá trị pháp lý.
5. **Năng lực vận hành** — một người: không 24/7, không SLA uptime, không có mặt trong 2 giờ.
6. **Dữ liệu cá nhân** (mục mới, trước đây thiếu hẳn): ảnh biển số, ảnh xe, ảnh khuôn mặt nhân viên, video tài xế đều là dữ liệu cá nhân theo Nghị định 13/2023/NĐ-CP (phạm vi nghĩa vụ cụ thể **cần kiểm chứng** với người có chuyên môn). **DMS giám sát tài xế: không đưa vào hợp đồng đầu. Đăng nhập khuôn mặt: mặc định TẮT.** Kèm 2 trang hồ sơ: thu gì, để làm gì, giữ bao lâu rồi tự xóa, ai được xem, và biển thông báo có camera ghi hình tại cổng.

## 1.8. Và một chỗ phải đổi hẳn giọng

**Buổi gặp đầu tuyệt đối không bán bằng góc "kiểm soát nội bộ, chống nhân viên gian lận".** Ba người ở cổng thường là người nhà chủ. Đi vào cổng nhà người ta rồi nói bóng gió người nhà họ ăn cắp là hỏng.

Bán cho **chủ**: bằng chứng khi cãi nhau với khách và với hãng tàu, và tiền lưu bãi thu đủ.
Bán cho **nhân viên**: máy gánh việc chép sổ, hết phải ghi tay ban đêm.
Nhật ký audit cứ để nó chạy âm thầm — chủ tự biết dùng.

---

# CÂU 2 — DOANH NGHIỆP TRIỂN KHAI VÀ VẬN HÀNH THẾ NÀO?

## 2.1. Mô hình triển khai — mặc định đề xuất HYBRID

| Mô hình | Ai nên dùng | Ưu | Nhược nói thẳng |
|---|---|---|---|
| **Cloud (SaaS)** | Muốn chạy nhanh, không có phòng máy | 1 ngày có link, không tốn hạ tầng ban đầu | Mất mạng là làn cổng đứng → bắt buộc router 4G dự phòng |
| **On-premise** | Có dữ liệu nhạy cảm, có hợp đồng bảo mật với hãng tàu | Cắt internet vẫn chạy phần lõi vì AI và thư viện đều nằm local. Không cần GPU | Khách tự lo máy chủ, tự lo backup |
| **Hybrid (khuyến nghị)** | Đa số depot | Lõi + CSDL + kho ảnh **trong cảng**; chỉ đẩy ra cloud 3 thứ đối ngoại: bảng tình trạng cổng công khai, cổng khách hàng, cổng tài xế. Sao lưu đêm ra cloud | Cần một đường internet ổn định cho phần đối ngoại |

**Câu bán hàng phải sửa lại cho đúng — đây là chỗ bị bắt bẻ nặng nhất:**

- ~~"Chi phí AI gần bằng 0"~~ → **SAI KHI NÓI VỚI KHÁCH.** Chi phí AI không biến mất, nó chuyển từ nhà cung cấp sang máy của khách.
- **Nói đúng:** *"AI chạy ngay trên máy tại cổng anh — **không có phí thuê server AI hằng tháng, không phải thay máy chủ GPU mỗi 3 năm, mất mạng vẫn đọc được biển, dữ liệu không ra khỏi cảng của anh**. Đổi lại, anh phải có một máy trạm đủ khỏe ở mỗi làn — em ghi thẳng vào bảng giá, không giấu trong phụ lục."*
- Đặt cạnh **báo giá thật của một hệ AI camera chạy server GPU** để khách tự so. So sánh có thật thuyết phục hơn chữ "bằng 0".

## 2.2. Khách cần chuẩn bị gì — và ba kịch bản phí, báo trước ngay buổi đầu

| Hạng mục | Yêu cầu tối thiểu | Ghi chú thật |
|---|---|---|
| **Máy trạm cổng** (mỗi làn 1 máy) | CPU i5 gen 12+ / Ryzen 5 5600+, RAM 16GB, SSD 256GB, Win 10/11, Chrome/Edge mới | AI đọc biển chạy **ở đây**. Máy văn phòng i3/4GB: mở được trang nghiệp vụ, **không giao ALPR**. Cấu hình này vào **phụ lục hợp đồng như điều kiện nghiệm thu**, không nói miệng. Phải benchmark thật trên đúng máy khách định dùng, ghi FPS đo được |
| **Camera cổng** | Camera **chuyên dụng ngoài trời có vỏ chống nước**, góc chếch hẹp, tốc độ màn trập cao, chống ngược sáng/lóa đèn pha | Webcam USB **chỉ dùng cho 2 tuần chạy thử trong nhà bảo vệ** — dây USB thường chỉ đi được ~5m và webcam văn phòng không chống nước. Camera lắp để "xem lại khi có chuyện" đặt góc bao quát, **không chắc đọc được biển** — phải test trước |
| **Máy chủ** (bản on-prem) | 4–8 nhân, RAM 16GB, SSD 500GB + ổ dữ liệu 2TB cho ảnh/video | Ảnh lưu ra thư mục đĩa, không nhét vào CSDL → SQL Server Express miễn phí vẫn đủ vài năm cho depot nhỏ. **Không cần card đồ họa** |
| **Mạng & điện** | Mạng **dây** từ cabin cổng về phòng máy (không wifi cho làn cổng), UPS ≥30 phút cho máy chủ + switch + máy trạm | Thêm SIM 4G dự phòng nếu chạy cloud |
| **Nhân sự** | 1 đầu mối CNTT (biết cài Windows, cắm mạng, backup — không cần lập trình viên) + **1 lãnh đạo có quyền ra lệnh đổi cách làm** | Không có người thứ hai thì **đừng ký**. Tên người này ghi vào hợp đồng |
| **Dữ liệu nền** (gửi Excel trước) | Khách + MST, biểu giá và biểu phí lưu bãi (kể cả số ngày miễn), sơ đồ bãi, xe/tài xế thường xuyên, mẫu EIR đang dùng, tài khoản ngân hàng cho VietQR | Thiếu mục nào vẫn nhập tay sau, không chặn go-live |

**Ba kịch bản phí triển khai — nói ngay buổi đầu, chủ depot ghét nhất là bị đội giá giữa chừng:**

| Kịch bản | Tình trạng camera | Phí |
|---|---|---|
| **A** | Camera sẵn có đủ dùng, chỉ cần cấu hình | X |
| **B** | Phải xoay góc / thêm đèn hắt | X + |
| **C** | Phải gắn thêm 1 camera chuyên đọc biển và/hoặc 1 máy trạm mới | X + + |

**Bước sàng khách cực rẻ trước khi chạy tới:** nhắn Zalo xin họ chụp giùm **một tấm ảnh cổng lúc 8h tối**. Tấm ảnh đó quyết định có nên đi hay không.

## 2.3. Lộ trình — hai giai đoạn CÓ TÊN KHÁC NHAU

Trước đây nói "chạy thử 2 tuần không tốn đồng nào" ở chỗ này rồi "thí điểm phải có trả phí" ở chỗ kia — hai câu đá nhau, và khách sẽ nhớ đúng câu có lợi cho họ.

### GIAI ĐOẠN ĐO — 2 tuần, miễn phí, hệ thống chỉ ĐỌC, không đụng quy trình
Cắm vào camera đang có, chạy song song, không thay đổi gì ở cổng. Đầu ra là **một tờ A4 khách được giữ dù có mua hay không**:
- Mỗi ngày bao nhiêu lượt xe, khung giờ nào ùn
- Trung bình bao nhiêu giây mỗi xe (bấm giờ thật, có n)
- Bao nhiêu lượt biển số AI đọc sai so với sổ — **tách riêng ban ngày / ban đêm / trời mưa**
- Bao nhiêu xe vào mà không khớp lệnh
- Bao nhiêu ngày lưu bãi chưa thu

### GIAI ĐOẠN CHẠY THẬT — có trả phí, giảm 50% ba tháng đầu

| Mốc | Việc | Kết quả nghiệm thu |
|---|---|---|
| **Tuần 1** | Dựng hệ thống, nạp dữ liệu nền, 3 vai trò, VietQR | Khách đăng nhập thấy **đúng tên cảng mình, đúng bảng giá mình, đúng sơ đồ bãi mình** |
| **Tuần 2** | Lắp/chỉnh camera, hiệu chỉnh ngày và đêm, 50–100 lượt xe thật đối chiếu tay | Có **tỷ lệ đọc đúng đo được trên xe thật của cảng đó**, tách ngày/đêm |
| **Tuần 3** | Đào tạo 3 buổi × 90 phút, tờ hướng dẫn 1 trang dán tại cabin, chạy song song sổ tay | Bảng đối chiếu sổ tay vs phần mềm. **Đề nghị chủ trả phụ cấp cho anh em cổng tuần này, hoặc rút xuống 3 ngày một ca** — đừng bắt người ta làm gấp đôi không công rồi trách người ta phá |
| **Tuần 4–5** | **Cắt sang chạy thật theo LÀN và theo KHUNG GIỜ VẮNG**, không cắt cả cổng trong một ngày. Một làn, giờ vắng, hai tuần; đạt thì mở rộng | Một làn chạy trọn vẹn từ xe vào tới xe ra trên phần mềm |
| **Tuần 6–8** | Bật thêm từng nhóm, mỗi tuần một nhóm: kho chứng từ → an ninh AI → cổng khách hàng / cổng tài xế → báo cáo, xuất Excel/MISA, file gửi hãng tàu | Biên bản nghiệm thu kèm số hệ thống tự đếm |

## 2.4. Nghiệm thu phải có TIỀN gắn vào — đây là chỗ mua được lòng tin nhanh nhất

Viết đúng những dòng này vào hợp đồng:

| Tiêu chí | Cách đo | Không đạt thì sao |
|---|---|---|
| Tỷ lệ đọc đúng biển số **trên tập ảnh cổng của chính depot đó**, tách ngày/đêm | Đối chiếu với sổ ghi tay, n ≥ 300 lượt | **Không thu tiền, tháo ra, trả cổng về nguyên trạng** |
| Thời gian xử lý một xe **không dài hơn cách ghi sổ hiện tại** | Bấm giờ, so với số đo ở Giai đoạn Đo | Như trên |
| **Người của khách tự cài lại được hệ thống và tự phục hồi được bản sao lưu**, mình ngồi bên cạnh không đụng tay | Diễn tập trước mặt chủ | Chưa coi là nghiệm thu xong |
| **Rút dây mạng ngay tại chỗ, xe vẫn qua được cổng** | Diễn tập trước mặt chủ trong ngày nghiệm thu | Như trên |

KPI cam kết là **tỷ lệ đọc đúng** — thứ mình kiểm soát và đo lại được. **Không** cam kết "giảm bao nhiêu giây mỗi xe" — cái đó phụ thuộc quy trình và thái độ nhân viên của họ.

## 2.5. Hỗ trợ và rủi ro "đội ngũ một người" — cam kết thứ giữ được, bỏ thứ không giữ được

**Bỏ hẳn "phản hồi trong 30 phút giờ hành chính"** — giờ hành chính là giờ đang ngồi trong lớp. Khách biết điều đó trước cả mình.

Thay bằng:

| Cam kết | Nội dung |
|---|---|
| **Cổng không bao giờ đứng** | Có **chế độ dự phòng thủ công**: một nút trong phần mềm, in phiếu tối giản, ghi tay số lệnh + PIN, nhập bù sau, mọi dòng nhập bù đều có dấu vết audit. **Diễn tập trước mặt chủ ngày nghiệm thu.** Ghi thẳng vào hợp đồng: *"Hệ thống hỏng thì anh vẫn cho xe qua cổng như cũ, không kẹt một xe nào."* |
| **Người thứ hai có tên và số điện thoại** | Không dùng "ký gửi mã nguồn" làm lời trấn an — mã nguồn nằm trong két không tự chạy. Thuê một tiệm CNTT ở Bình Dương tham gia **ngay từ kỳ thí điểm**, đưa hợp đồng của họ vào phụ lục để khách cầm được số |
| **Công khai lịch bận** | Tuần nào thi, tháng nào bận — nói trước và nói cách xử lý. Giấu lịch học rồi biến mất giữa kỳ mới là thứ giết uy tín |
| **Điều khoản thoát** | Bản on-prem chạy độc lập; bàn giao toàn bộ CSDL ở định dạng đọc được (Excel/SQL backup) trong N ngày nếu ngừng dịch vụ; đề xuất sẵn phương án **sau thí điểm khách mua đứt, phí tháng chuyển thành phí bảo trì** |
| **Trách nhiệm bản quyền** | Điều khoản: bên cung cấp chịu **toàn bộ** trách nhiệm bản quyền phần mềm và mô hình AI, bồi thường nếu khách bị đòi. Khách sẽ đọc đúng dòng đó |
| Kênh | Zalo nhóm riêng mỗi khách (có cả trưởng ca, không chỉ sếp) + điện thoại + AnyDesk. Cảng Việt Nam sống trên Zalo, đừng bắt mở ticket |
| Sao lưu | CSDL tự động hằng đêm, giữ 30 bản; kho ảnh sao lưu hằng tuần ra ổ riêng; **mỗi quý diễn tập phục hồi một lần có biên bản** — backup chưa từng thử phục hồi coi như không có |
| Ranh giới | Bảo trì phần cứng (camera, máy trạm, UPS) thuộc khách, bảo hành với nơi bán. Hỗ trợ chọn mua và chỉnh góc miễn phí. Ghi rõ để sau khỏi cãi khi cháy một con webcam |

## 2.6. Rủi ro vận hành và cách chặn

| Rủi ro | Cách chặn |
|---|---|
| **Mất điện, mất mạng** | UPS ≥30 phút; on-prem thì LAN vẫn sống; cloud thì bắt buộc 4G dự phòng; luôn có đường lùi giấy (số lệnh + PIN in ra, tài xế cầm theo) |
| **Camera mờ, ngược sáng, mưa, ban đêm** | Đo ở Giai đoạn Đo trước khi ký; 3 tầng nhận dạng; **luồng nghiệp vụ không phụ thuộc hoàn toàn vào AI** — đọc không ra thì gõ tay biển số hoặc số lệnh + PIN, xe vẫn qua trong vài giây, ảnh vẫn lưu |
| **Nhân viên quay về sổ tay** | Tên một lãnh đạo trong hợp đồng; chạy song song rồi cắt theo làn; cabin chỉ 1 màn hình 1 nút; đo thành tích bằng "số xe xử lý mỗi ca" để họ thấy mình làm được nhiều hơn, không phải đang bị rình |
| **Máy trạm yếu / trình duyệt cũ** | Cấu hình vào phụ lục hợp đồng; test ngay trên máy khách định dùng ở Giai đoạn Đo; khóa máy cổng chỉ dùng Chrome/Edge mới; lần đầu để tải xong ~33MB mô hình rồi mới bàn giao |
| **Tranh chấp dữ liệu / phụ thuộc nhà cung cấp nhỏ** | Audit trail + ảnh mốc giờ; chứng từ niêm phong hash; dữ liệu nằm trong máy khách, xuất Excel/MISA bất cứ lúc nào; điều khoản thoát ở trên |

---

# CÂU 3 — AI DÙNG, AI TRẢ TIỀN, BÁN THẾ NÀO, THU TIỀN THẾ NÀO?

## 3.1. Người dùng thật

| Ai | Dùng phần nào | Tần suất | Điều kiện sống còn |
|---|---|---|---|
| **Bảo vệ / nhân viên cổng** (2–3 người, 2–3 ca) | Đúng **một** màn hình cổng | Mỗi lượt xe vào và ra | Chữ to, ít nút, bấm được bằng tay bẩn, chạy được khi mạng chập chờn. Thiết kế như trang quản trị là họ không dùng |
| **Giao nhận / thương vụ** (1–3) | Tạo lệnh + PIN, in EIR, kho chứng từ, hồ sơ container, phí lưu bãi | Vài chục lần/ngày, dồn 7–10h và 15–18h | Là người đang chịu trận khi khách gọi hỏi "hàng tôi đâu" — họ sẽ nói giúp mình |
| **Điều độ / trưởng ca** (thường 1, hay là người nhà chủ) | Sơ đồ bãi, tồn container, slot giờ vào cổng | 10–20 lần/ngày | **Người dùng sâu nhất — đồng minh số 1** |
| **Kế toán** (1–2) | Đối soát VietQR, phí lưu bãi, xuất Excel/MISA | 15–30 phút/ngày, nặng 3–5 ngày cuối tháng | Bản xuất **không khớp mẫu họ đang làm** thì họ nhập tay song song — và đó là lúc dự án bắt đầu chết |
| **Tài xế** (người ngoài, của nhà xe) | Cổng tài xế: tra lệnh bằng số lệnh + PIN, check-in trước, xem tình trạng cổng | 1–3 lần/chuyến | **Không bắt đăng ký tài khoản, không bắt cài app.** Đông người dùng nhất nhưng không trả một đồng — giá trị của họ là chứng minh hệ thống chạy thật ngoài đời |
| **Giám đốc / chủ** | Dashboard, báo cáo sản lượng, cảnh báo đẩy về điện thoại | 2–5 phút/ngày, buổi tối | Người dùng ít nhất lại là người quyết chi tiền → mọi thứ họ cần phải nằm ở **màn hình đầu tiên** |

## 3.2. Người trả tiền

**Giám đốc / chủ doanh nghiệp, ký trực tiếp, không qua phòng ban nào.** Công ty TNHH tư nhân hoặc công ty gia đình 8–25 người, không có phòng CNTT, không có dòng ngân sách tên "chuyển đổi số", mọi khoản chi lặp lại trên ~5 triệu/tháng đều phải chủ gật. Chủ 35–55 tuổi, xuất thân nhà xe hoặc giao nhận, tự đọc hợp đồng, quyết trong một buổi cà phê.

Hệ quả: **không có quy trình mua sắm nào để bám** — được hay không nằm ở chỗ chủ có tin mình không. Phải gặp **tại depot**, gửi email gần như chắc chắn chìm. Trình bày theo **tiền**, không theo kiến trúc hệ thống.

**Loại khỏi danh sách khách đầu tiên:** depot thuộc SNP / Gemadept / VIMC hoặc thuộc hãng tàu nước ngoài (ICD Tân Cảng Sóng Thần, Long Bình đã chạy eWMS/ePort của tập đoàn) — người ký ở ban CNTT tập đoàn, có đấu thầu, chu kỳ 6–18 tháng. Không phải trận đánh cho một người.

## 3.3. Người ảnh hưởng — và một người GÁC CỬA trước đây bị bỏ sót

| Ai | Vai trò thật | Cách xử |
|---|---|---|
| **Trưởng phòng khai thác / điều độ** (hay là con, cháu, em rể chủ) | Đồng minh số 1 | Cho dùng trước mọi người; cho tự đặt biểu giá, tự đặt tên block bãi; **để chính họ trình bày kết quả 2 tuần trước chủ**, không phải mình |
| **HÃNG TÀU** | **Vừa mở cửa vừa gác cửa** — trước đây chỉ xếp vào "áp lực giúp mình bán", một chiều và quá lạc quan. Depot rỗng **không sở hữu container**, nó là nhà thầu dịch vụ của hãng tàu. Hãng quy định mẫu EIR, bảng mã hư hỏng, định dạng báo cáo tồn | **Bước 0 khi khảo sát, hỏi trước cả câu về nỗi đau:** (1) Hãng nào chỉ định depot này hạ rỗng? (2) Đang gửi EIR và báo cáo tồn bằng cách nào — Excel, Word, gõ tay vào cổng của hãng? (3) Có hãng nào bắt file định dạng cố định không? Ba câu này quyết định deal làm được hay không |
| **Kế toán trưởng** | Không quyết mua nhưng giết được hợp đồng bằng 3 câu: có hóa đơn VAT không, có vào chi phí hợp lý được không, số có khớp sổ không | Pháp nhân + mẫu hợp đồng + bản xuất **đúng định dạng file họ đang dùng**, chuẩn bị trước khi gặp |
| **Nhà thầu camera / IT đang phục vụ depot** | Vừa là kênh phân phối rẻ nhất vừa là **kẻ thay thế trực tiếp** phần ALPR | Kéo về làm đối tác lắp đặt, và **chia hoa hồng theo thuê bao hằng tháng chứ không chỉ phí lắp một lần** — chia phí lắp thì ông ta vẫn có lý do bán đứt cái NVR |

## 3.4. Người cản trở

| Ai | Lý do thật (không phải lý do họ nói) | Cách xử |
|---|---|---|
| **Bảo vệ / nhân viên ghi sổ** | Sợ mất việc; mất khoản bồi dưỡng của tài xế để được vào trước, châm chước lỗi vỏ. Họ không phản đối ra mặt — hệ thống chỉ "tự nhiên hay lỗi", rồi 3 tháng sau quay về sổ tay | Định vị AI là **ghi hộ**, người gác cổng vẫn bấm nút xác nhận cuối và **tên họ hiện trên phiếu**; xin chủ cam kết trước cả tổ không giảm người 6 tháng đầu; đo bằng "số xe xử lý mỗi ca" |
| **Surveyor / kiểm vỏ** | Module soi móp méo đụng vào quyền phán xét mức hư hại — mà mức hư hại ra tiền M&R và ra chuyện ai đền. Và họ bác **đúng**: AI không phân biệt được móp trong ngưỡng cho phép với móp phải sửa theo chuẩn hãng tàu | **AI chỉ chụp, đánh dấu vị trí nghi ngờ, lưu ảnh mốc giờ. Người ký duyệt, có quyền sửa kết quả AI.** Hãng tàu là bên quyết chuẩn hư hại — nói rõ điều này thì lập luận mới đứng vững. Bán bằng góc "ảnh mốc giờ bảo vệ anh khi khách cãi", không bao giờ bằng góc "AI thay anh" |
| **Tài xế / nhà xe** | Slot giờ ép mất linh hoạt; **DMS camera cabin bị coi là giám sát cá nhân** — thực tế họ dán băng keo che camera, và nghề lái container đang thiếu người nên chủ nhà xe không dám ép | **Không đưa DMS vào hợp đồng đầu.** Bán slot bằng lợi ích của chính tài xế: "đặt giờ thì tới là vào, khỏi nằm ngoài đường 2 tiếng". DMS để dành bán riêng cho **chủ nhà xe** — người trả bảo hiểm và gánh hậu quả tai nạn |
| **Chính ông chủ** (chỗ không ai nói ra) | Bản ghi tự động 100% lượt xe + audit + hash tạo ra bản ghi **không sửa được** về toàn bộ sản lượng thật. Phí nâng hạ, vệ sinh vỏ, châm chước lưu bãi cho khách quen không phải khoản nào cũng vào sổ | Buổi đầu bán bằng góc "bằng chứng khi cãi với khách và hãng tàu", không phải "minh bạch nội bộ". Cho chủ **tự chọn mức chi tiết của báo cáo**. Nhấn mạnh bản cài tại chỗ: *"dữ liệu nằm trong máy của anh, em không giữ, em không xem."* Chuỗi khối chống sửa là điểm bán tuyệt vời với **khách của depot**, là điểm gây sợ với **chủ depot** — phải nói đúng đối tượng |

## 3.5. Đối thủ thật — không phải Navis N4, không phải CATOS

Toàn bộ nghiên cứu đối thủ đang là SNP, Gemadept, Hateco, Cái Mép — tức nghiên cứu rất công phu **đúng cái chỗ tuyên bố không bán**. Đối thủ thật ở phân khúc đã chọn có ba, và cả ba nguy hơn Navis:

| Đầu việc của depot | Excel + Zalo | ANPR có sẵn trong đầu ghi Hikvision | SmartPort |
|---|---|---|---|
| Chi phí | **0đ** | Vài triệu, một lần | Thuê tháng + máy trạm |
| Nhân viên đã quen | **Đã thuộc** | — | Phải học |
| Không bao giờ hỏng | **Đúng** | Gần đúng | Phụ thuộc máy/mạng |
| Đọc biển số, mở barrier | Không | **Có** | Có |
| Biết xe này có lệnh hay không | Có (tra tay) | **Không** | Có, tự động |
| Đúng container nào, mã PIN tài xế | Có (tra tay) | **Không** | Có |
| In EIR | Gõ tay | Không | Tự sinh |
| Ảnh vỏ trước/sau có mốc giờ | Không | Có video, phải lục | Gắn thẳng vào hồ sơ container |
| Phí lưu bãi tự tính từ ngày thứ N | Nhớ thì tính | Không | Tự động theo biểu giá |
| Tra lại container 3 tháng trước trong 10 giây | Rất khó | Không | Có |
| File gửi hãng tàu | Gõ tay | Không | Tự sinh (Đường A) |
| Khách của anh tự tra online | Không | Không | Có |

Để cột Excel thắng ở mấy ô — thành thật thì bảng mới đáng tin, và chủ nhìn ra ngay khoảng trống ở nửa dưới.

**Câu phải thuộc lòng khi bị kéo xuống so giá với cái đầu ghi 5 triệu:**
> "Đầu ghi đọc biển rồi mở barrier — hết. Nó không biết xe này có lệnh hay không, lệnh đúng container nào, PIN tài xế đúng chưa. Nó không in EIR. Nó không tự tính phí lưu bãi từ ngày thứ sáu. Nó không dựng được hồ sơ container để tháng sau khách cãi thì anh mở ra chỉ mặt. **Cái em bán không phải cái mắt đọc biển, cái em bán là quyển sổ tự ghi.**"

Và chuẩn bị sẵn cho đòn Excel, vì đòn đó chắc chắn tới:
> "Excel không sai — nó sai lúc khách gọi hỏi container ba tháng trước, hoặc lúc hãng tàu bắt đền vỏ móp mà mình không có tấm ảnh nào."

## 3.6. Khách hàng đầu tiên

**Depot container rỗng tư nhân độc lập, 3–8 ha, ~80–250 lượt xe/ngày, trục Sóng Thần – Bình Chuẩn – Tân Uyên – Bàu Bàng hoặc Biên Hòa – Nhơn Trạch – Long Thành, cách chỗ mình ở dưới 30 km.**

**Tiêu chí số 1, đứng trên tất cả:** chỗ nào có **quan hệ cá nhân dẫn thẳng tới chủ**. Khách đầu tiên không phải khách tốt nhất, mà là khách chịu cho mình vào cổng đặt cái laptop chạy thử hai tuần.

Dấu hiệu nhận biết (đi ngang cổng là thấy): còn sổ ghi tay ở cổng; 7–9h sáng có hàng xe nằm chờ ngoài đường; không có website, chỉ có Zalo, lệnh gửi bằng ảnh chụp; đã có camera nhưng chỉ mở xem lại khi có chuyện; chính chủ hoặc vợ con chủ đang phải nghe điện thoại "container tôi đâu"; **có ít nhất một hãng tàu/forwarder đang đòi ảnh tình trạng vỏ hoặc đòi tra cứu online**.

Tránh: thuộc tập đoàn hoặc hãng tàu; đã mua phần mềm depot của bên khác (thành trận thay thế, phải chứng minh di trú dữ liệu, quá sức một người); trên 500 lượt/ngày (họ đòi SLA và điều khoản phạt); không quen ai bên trong.

**Mục tiêu của deal đầu tiên không phải tiền** — là ba thứ: (1) một tên doanh nghiệp được phép nhắc, (2) số liệu thật trước–sau, (3) một câu trích dẫn của chủ để mang đi chào khách thứ hai. Ghi cả ba vào hợp đồng để đổi lấy việc giảm sâu phí triển khai.

## 3.7. Cách tiếp cận

1. **Đi bằng người giới thiệu, không đi bằng email.** TDMU nằm giữa vùng depot đông nhất phía Nam — hỏi thầy cô có học viên tại chức ngành logistics, hỏi hội cựu sinh viên, hỏi bạn bè xem ba mẹ ai làm ở Sóng Thần/VSIP/Nam Tân Uyên. Xin một câu giới thiệu và một cuộc gặp 20 phút **tại depot**.
2. **Đi cửa sau qua tài xế.** 5–7h sáng ra quán cà phê võng quanh cổng depot. Đổi vài ly cà phê lấy danh sách 5–10 depot cụ thể kèm tên chủ, nỗi đau cụ thể, và con số xe/ngày thật.
3. **Chào một thứ đo được trong hai tuần, không chào 59 chức năng.** "Cho em cắm vào camera cổng anh đang có, chạy đọc thôi, không đụng quy trình, hai tuần, không tốn đồng nào. Sau đó em đưa anh một tờ A4." Tờ giấy đó bán hàng thay mình.
4. **Dùng cuộc thi khởi nghiệp làm giấy bảo chứng thay cho việc chưa có khách** — và xin thẳng ban giám khảo/nhà tài trợ một lời giới thiệu tới một depot. Đó mới là giải thưởng thật. Mang bản cứng có dấu đi gặp chủ, thế hệ này tin giấy hơn tin slide.
5. **Kênh 0đ đúng tệp:** video ngắn màn hình cổng chạy thật (không phải dữ liệu giả) đăng nhóm Facebook logistics/vận tải container, lập Zalo OA. Nhóm khách này sống trên Zalo, không sống trên LinkedIn.

## 3.8. Mô hình doanh thu

**Chia gói theo LƯU LƯỢNG CỔNG, không chia theo chức năng.** Chia theo chức năng thì chủ ngồi cắt module xuống gói rẻ, mua bản què rồi kết luận "phần mềm không ăn thua". "Bao nhiêu xe một ngày" là ngôn ngữ của họ.

| Gói | Quy mô | Giá đề xuất (**cần kiểm chứng**) |
|---|---|---|
| 1 | Dưới 100 lượt xe/ngày | 2,9 tr/tháng |
| 2 | 100–300 lượt/ngày | 6,9 tr/tháng |
| 3 | Trên 300, hoặc nhiều bãi/nhiều điểm | 14,9 tr/tháng |

**Cơ chế đo phải minh bạch, nếu không khách sẽ nghi "phần mềm mày đếm, mày muốn đếm sao chả được":** bộ đếm hiện công khai trên dashboard; chốt gói theo **trung bình 3 tháng**; chỉ nâng gói khi vượt ngưỡng **2 tháng liên tiếp** và phải báo trước; **không bao giờ tự động nâng**.

Kèm theo:
- **Phí triển khai một lần thu riêng**, theo 3 kịch bản A/B/C ở Câu 2. Nhóm này tin "có bỏ tiền ra người ta mới làm tới nơi"; triển khai miễn phí bị coi là hàng thử. Với 2–3 khách đầu được giảm sâu, nhưng **phải đổi lấy** quyền dùng tên tham chiếu, quyền công bố số liệu trước–sau, và một buổi cho khách tiềm năng tới xem tận nơi — ghi vào hợp đồng.
- **Ký năm, trả trước quý hoặc năm, giảm 10–15%.**
- **Thí điểm có trả phí:** giảm 50% ba tháng đầu, kèm KPI **tỷ lệ đọc đúng trên tập ảnh cổng của chính depot đó**; không đạt thì hoàn tiền phần thuê bao.
- **Add-on bán riêng, không nhồi vào gói:** module reefer (chỉ depot có bãi lạnh); **DMS bán cho chủ nhà xe theo xe/tháng, không bán cho depot**.
- **Câu chốt mới, thay câu "chưa tới một phần ba lương một người"** (câu cũ tự mâu thuẫn với cam kết 6 tháng không giảm người):
  > "Sáu tháng đầu em không hứa anh bớt người. Em hứa **cùng ngần ấy người anh xử được nhiều xe hơn, và anh đòi được tiền lưu bãi đủ hơn** — chỗ đó là tiền vô, không phải tiền tiết kiệm trên giấy."

**Phải nhìn thẳng vào trần doanh thu** (giám khảo quỹ sẽ hỏi): bộ lọc khách hiện tại tự ăn thị trường của chính mình — nhóm bị loại (thuộc tập đoàn, trên 500 xe/ngày, đã mua phần mềm) chính xác là nhóm **có ngân sách**. Với triển khai 2–4 tuần/khách bằng một người, **trần phục vụ thực tế là 6–10 khách/năm**, và đó là năm không nghỉ, không ốm, không thi. Vậy nên:
- **Tự đặt trần: tối đa 2 khách triển khai song song.** Nhận khách thứ 3 khi chưa có người là cách nhanh nhất hỏng cả ba trong một khu mà ai cũng biết nhau.
- **Viết rõ đường mở rộng, với điều kiện chuyển hướng cụ thể — "sau khách thứ 5 hoặc sau tháng thứ 9":** (a) cổng nhà máy / kho hàng trong KCN (cùng bài toán cổng, số lượng gấp nhiều lần depot); (b) ICD nhỏ và kho ngoại quan sau khi đã có case study; (c) bán riêng **module cổng** như một sản phẩm độc lập.

---

# NHỮNG GIẢ ĐỊNH CÒN PHẢI KIỂM CHỨNG

| # | Giả định chưa chắc | Nếu sai thì sập cái gì | Cách kiểm chứng | Chi phí |
|---|---|---|---|---|
| 1 | **Camera IP giám sát sẵn có của depot đủ để đọc biển số** | Lời chào "cắm vào camera anh đang có", định vị giá, tờ A4 hai tuần, cam kết KPI — bốn thứ cùng lúc. Nếu chỉ đạt 60–75% thì phải thêm camera ANPR chuyên dụng, phí triển khai đội lên vài chục triệu | Xin 2–3 giờ video cổng ngày/đêm/mưa, chạy model, đếm | **Một buổi chiều — giả định rẻ nhất mà chưa làm** |
| 2 | **Đọc được luồng RTSP từ đầu ghi Hikvision/Dahua** | Bắt khách vứt camera đi mua lại = tự chặn đường bán | Làm thật, test trên đầu ghi thật | 1–2 tuần code |
| 3 | **Tỷ lệ đọc đúng ban đêm / trời mưa / biển bẩn / biển cong** | KPI hợp đồng, tiêu chí nghiệm thu | Đo tại chính cổng khách, tách điều kiện, n ≥ 300 | 2 tuần |
| 4 | **Máy trạm đời cũ chạy nổi WASM/WebGPU tới mức nào** | Cấu hình tối thiểu ghi trong phụ lục hợp đồng | Benchmark trên đúng máy khách định dùng, ghi FPS đo được | 1 buổi |
| 5 | **Giá Ultralytics Enterprise License / hoặc độ chính xác sau khi đổi sang RT-DETR-YOLOX-DAMO** | Nếu vài nghìn USD/năm thì nó ăn đứt biên của 3–4 khách đầu — phải biết **trước khi in bảng giá** | Hỏi thẳng Ultralytics; nếu đổi mô hình thì huấn luyện lại + đo lại trên biển số VN | Chưa rõ, phải hỏi |
| 6 | **Giấy phép của các trọng số khác đang nhúng** — đặc biệt phần nhận diện khuôn mặt (nhiều bản InsightFace giới hạn phi thương mại) | Quyền bán | Rà từng model trong `wwwroot/Content/models` | 1 ngày |
| 7 | **Phạm vi nghĩa vụ theo NĐ 13/2023 với ảnh biển số, ảnh xe, khuôn mặt nhân viên, video tài xế** | Rủi ro pháp lý đang bị đẩy sang khách — người đứng tên doanh nghiệp | Hỏi người có chuyên môn; soạn 2 trang hồ sơ dữ liệu cá nhân | 1 buổi tư vấn |
| 8 | **Mức giá 2,9 / 6,9 / 14,9 triệu có đúng túi tiền không** | Toàn bộ mô hình doanh thu | Hỏi thẳng 5–7 chủ depot: "anh đang trả bao nhiêu mỗi tháng cho phần mềm / camera / giám sát hành trình?" | Vài buổi cà phê |
| 9 | **Số depot lọt hết tiêu chí trong bán kính 30 km** — hiện **chưa có mẫu số nào**, đây là lỗi chết người trong pitch | Doanh thu trần, tính khả thi của cả kế hoạch | Bốn nguồn miễn phí: danh sách depot được hãng tàu chỉ định hạ rỗng; hội viên hiệp hội logistics BD–ĐN (danh sách có công khai không — **cần kiểm chứng**); đếm tay trên Google Maps dọc đúng các trục; tra đăng ký kinh doanh ngành kho bãi. Ra 3 con số phễu: tổng → độc lập → lọt tiêu chí | 2–3 ngày |
| 10 | **Yêu cầu cụ thể của từng hãng tàu về EIR, bảng mã hư hỏng, định dạng báo cáo tồn** | Quyết định deal làm được hay không (nếu không khớp, depot phải nhập kép → dự án chết) | Bước 0 khi khảo sát, hỏi thẳng depot và xin mẫu file họ đang gửi | Miễn phí, hỏi là ra |
| 11 | **Tên tổ chức và lịch sinh hoạt của hội logistics / VCCI Bình Dương** | Một kênh tiếp cận | Gọi hỏi trước khi đi | 1 cuộc gọi |
| 12 | **Tìm được người thứ hai / tiệm CNTT ở Bình Dương chịu nhận bàn giao hệ .NET 10 + ONNX** | Toàn bộ lời trấn an "không bị bỏ rơi". Escrow mã nguồn của một sinh viên là hình thức nếu không có người này | Đi gặp 3–5 tiệm, đưa hợp đồng thật, trả tiền thật ngay từ kỳ thí điểm | Vài triệu/tháng |
| 13 | **Định mức thời gian thật ở cổng** (giây/xe ghi tay, số cuộc gọi hỏi hàng, số ngày lưu bãi bị sót) | Toàn bộ trang ROI và toàn bộ lập luận hòa vốn | Bấm giờ trong 2 tuần Giai đoạn Đo, ghi nguồn ngay trên màn hình | Nằm trong 2 tuần đo |