# MÔ HÌNH VẬT LÝ "CẢNG THÔNG MINH SMARTPORT"
### Danh sách linh kiện + hướng dẫn mua — Lê Hữu Anh Tài (2424802010066), TDMU

> **Nguyên tắc thiết kế:** mô hình KHÔNG chạy kịch bản giả. Mỗi cụm phần cứng đều bắn dữ liệu
> thật vào hệ thống web SmartPort đã xây (`/Admin/Gate/Save`, `/Admin/Reefer/GhiNhan`…),
> và điện thoại mở app PWA thấy đúng dữ liệu đó. Giám khảo bấm vào đâu cũng khớp.
>
> ⚠️ **Giá bên dưới là giá tham khảo thị trường VN** (Hshop, Nshop, Điện Tử Tương Lai, Shopee).
> Giá linh kiện dao động theo thời điểm — bạn kiểm tra lại trước khi chốt đơn.

---

## A. PHƯƠNG ÁN 1 — BẢN TỐI GIẢN (đủ để thi, ~900k – 1,2 triệu)

Làm được 3 màn ăn tiền nhất: **cổng ALPR mở barie**, **giám sát container lạnh**, **báo động sự cố**.

| # | Linh kiện | SL | Giá lẻ (đ) | Thành tiền | Dùng cho phân hệ nào |
|---|-----------|----|-----------|-----------|----------------------|
| 1 | **ESP32 DevKit V1** (38 chân, CP2102/CH340) | 1 | 95.000 – 135.000 | ~120.000 | Bộ não, WiFi đẩy dữ liệu về web |
| 2 | **ESP32-CAM AI-Thinker** (kèm OV2640) | 1 | 120.000 – 165.000 | ~140.000 | Camera cổng → ảnh cho AI đọc biển số |
| 3 | **Module nạp USB-TTL CH340** (nạp cho ESP32-CAM) | 1 | 25.000 – 45.000 | ~35.000 | Bắt buộc để nạp code ESP32-CAM |
| 4 | **Servo SG90** (hoặc MG90S bánh răng kim loại bền hơn) | 2 | 30.000 / 55.000 | ~70.000 | Barie cổng vào + cổng ra |
| 5 | **Cảm biến siêu âm HC-SR04** | 2 | 22.000 – 32.000 | ~55.000 | Phát hiện xe tới cổng vào/ra |
| 6 | **Cảm biến nhiệt DS18B20 chống nước** | 2 | 28.000 – 42.000 | ~70.000 | Giám sát container lạnh (Reefer) |
| 7 | **Màn OLED 0.96" I2C** | 1 | 45.000 – 70.000 | ~55.000 | Hiện mật độ cổng / nhiệt độ |
| 8 | **Còi buzzer 5V (loại có mạch)** | 1 | 8.000 – 15.000 | ~12.000 | Báo động sự cố |
| 9 | **Nguồn adapter 5V 3A** (jack DC 5.5mm) | 1 | 65.000 – 95.000 | ~80.000 | Cấp điện toàn mô hình |
| 10 | **Đế jack DC + module hạ áp LM2596** | 1 | 20.000 – 35.000 | ~30.000 | Chia nguồn an toàn |
| 11 | **Breadboard 830 lỗ + bộ dây Dupont 3 loại** | 1 | 70.000 – 110.000 | ~90.000 | Đấu nối không cần hàn |
| 12 | **Điện trở 1kΩ + 2kΩ** (chia áp Echo), tụ 100µF | 1 bộ | 10.000 – 20.000 | ~15.000 | Bảo vệ chân 3.3V của ESP32 |
| 13 | **Foam board / tấm formex A2 5mm** | 2 | 30.000 – 45.000 | ~70.000 | Làm nền cảng, nhà cổng |
| 14 | **Xe tải container đồ chơi mini (1:87 – 1:64)** | 2 | 35.000 – 90.000 | ~120.000 | Đạo cụ chạy kịch bản |
| 15 | **Keo nến + súng bắn keo, dao rọc, sơn xịt** | 1 bộ | 80.000 – 130.000 | ~100.000 | Thi công mô hình |
| | | | **TỔNG** | **≈ 990.000 đ** | |

---

## B. PHƯƠNG ÁN 2 — BẢN ĐẦY ĐỦ (ấn tượng hơn, thêm ~800k → tổng ~1,8 – 2,2 triệu)

Thêm **trạm cân điện tử**, **LED bãi container**, **thẻ tài xế RFID**, **quạt reefer**.

| # | Linh kiện thêm | SL | Giá lẻ (đ) | Thành tiền | Dùng cho |
|---|----------------|----|-----------|-----------|----------|
| 16 | **Loadcell 5kg + module HX711** | 1 | 65.000 – 95.000 | ~80.000 | Trạm cân → phiếu cân VGM |
| 17 | **Màn OLED 0.96" I2C thứ 2** (địa chỉ 0x3D) | 1 | 45.000 – 70.000 | ~55.000 | Hiện khối lượng tại trạm cân |
| 18 | **LED RGB WS2812B dải 1m/60 LED** | 1 | 65.000 – 100.000 | ~80.000 | Trạng thái ô bãi container |
| 19 | **Module RFID RC522 + 2 thẻ** | 1 | 38.000 – 60.000 | ~50.000 | Thẻ tài xế thay mã PIN |
| 20 | **Quạt 5V 30×30mm + module relay 5V 1 kênh** | 1 | 35.000 – 55.000 | ~45.000 | Mô phỏng máy lạnh reefer |
| 21 | **Servo MG90S cho cẩu bờ + ròng rọc nhỏ** | 1 | 55.000 – 75.000 | ~65.000 | Cẩu hạ container |
| 22 | **Container mini nhựa/in 3D (bộ 12–20 cái)** | 1 bộ | 90.000 – 200.000 | ~150.000 | Bãi container nhiều màu |
| 23 | **Mô hình tàu container** (mua sẵn hoặc tự cắt formex) | 1 | 80.000 – 250.000 | ~150.000 | Cầu bến |
| 24 | **Đèn LED trắng 3mm + điện trở** (đèn cảng) | 20 | 500 – 1.000/cái | ~20.000 | Trang trí ban đêm |
| 25 | **Mica trong/xanh làm mặt nước 40×30cm** | 1 | 50.000 – 90.000 | ~70.000 | Mặt biển |
| 26 | **Cỏ nhân tạo, sỏi mini, decal vạch kẻ đường** | 1 bộ | 60.000 – 120.000 | ~90.000 | Hoàn thiện cảnh quan |
| | | | **TỔNG THÊM** | **≈ 855.000 đ** | |

---

## C. MUA Ở ĐÂU

| Nơi bán | Ưu điểm | Ghi chú |
|---------|---------|---------|
| **Hshop.vn** | Hàng chuẩn, có datasheet, ship toàn quốc | Đắt hơn Shopee ~10–20% nhưng ít lỗi |
| **Nshop.com.vn** | Giá tốt, đủ combo Arduino/ESP32 | Có sẵn combo "học ESP32" tiện |
| **Điện Tử Tương Lai (dientutuonglai.com)** | Nhiều cảm biến lạ | |
| **Thegioiic.com** | Linh kiện rời, điện trở/tụ | |
| **Shopee** — shop *Điện tử DAT*, *MakerLab*, *IoT Store* | Rẻ nhất, freeship | Đọc kỹ đánh giá, tránh ESP32-CAM hàng dựng |
| **Chợ Nhật Tảo (Q.10, TP.HCM)** | Mua tận tay, về liền | Nếu bạn xuống Sài Gòn |
| **Tiệm điện tử gần TDMU / chợ Thủ Dầu Một** | Mua gấp keo, dây, foam | Đồ thủ công mua ở đây rẻ |

**Mẹo tiết kiệm:** tìm combo *"ESP32 starter kit"* (~250–400k) đã gồm ESP32 + breadboard + dây + LED + servo + siêu âm — rẻ hơn mua lẻ khoảng 30%.

---

## D. SƠ ĐỒ CHÂN NỐI (ESP32 DevKit V1)

| Chân ESP32 | Nối tới | Ghi chú |
|-----------|---------|---------|
| **D5 (GPIO5)** | Servo barie — dây tín hiệu (cam/vàng) | Nguồn servo lấy từ 5V ngoài |
| **D18 / D19** | HC-SR04: Trig / Echo | Echo qua cầu chia áp 1kΩ+2kΩ |
| **D4 (GPIO4)** | DS18B20 — chân DATA | Cần điện trở kéo lên 4.7kΩ giữa DATA và 3.3V |
| **D21 / D22** | OLED: SDA / SCL | Chuẩn I2C, dùng chung cho 2 OLED (khác địa chỉ) |
| **D32 / D33** | HX711: DT / SCK | |
| **D13 (GPIO13)** | LED WS2812B — chân Din | Nên có điện trở 330Ω nối tiếp |
| **D25 (GPIO25)** | Còi buzzer (+) | |
| **D26 (GPIO26)** | Relay điều khiển quạt — chân IN | |
| **GND** | GND chung của TẤT CẢ thiết bị + nguồn ngoài | **Bắt buộc nối chung** |

**3 điều dễ cháy board — nhớ kỹ:**
1. **GND phải chung** giữa nguồn ngoài và ESP32, nếu không tín hiệu chập chờn.
2. **Không lấy 5V của ESP32 để nuôi servo/LED** — dòng lớn làm sụt áp, reset board liên tục.
3. **ESP32 chạy 3.3V**, chân Echo của HC-SR04 xuất 5V → phải chia áp.

---

## E. LỘ TRÌNH LẮP (5 buổi, mỗi buổi ~3 tiếng)

| Buổi | Việc làm | Kết quả |
|------|----------|---------|
| **1** | Cắt nền foam 80×60cm, vẽ phân khu, sơn nền, dán mặt nước | Có "sa bàn" trống |
| **2** | Cắm ESP32 lên breadboard, nạp code WiFi, test đèn nháy + OLED | ESP32 lên mạng, in chữ ra OLED |
| **3** | Lắp cổng: siêu âm + servo barie + ESP32-CAM lên trụ | Đưa tay vào → barie mở |
| **4** | Lắp reefer (DS18B20 + quạt), còi, LED bãi | Rút cảm biến → còi kêu |
| **5** | Nối ESP32 vào web SmartPort qua WiFi, chạy thử kịch bản 90 giây | Demo hoàn chỉnh |

**Buổi 6 (nếu làm bản đầy đủ):** lắp trạm cân loadcell + hiệu chuẩn (đặt vật đã biết khối lượng để tính hệ số).

---

## F. NỐI MÔ HÌNH VÀO HỆ THỐNG WEB (phần quan trọng nhất)

Mô hình **không cần server riêng** — nó gọi thẳng API của SmartPort:

```
ESP32  ──HTTP POST──►  http://<IP-máy-tính>:5210/Admin/Gate/Save
                       {"bienSo":"51C-638.72","huong":"Vào","loaiXe":"Đầu kéo"}

ESP32  ──HTTP POST──►  /Admin/Reefer/GhiNhan   (nhiệt độ đo được)

Web    ──HTTP GET ──►  http://<IP-ESP32>/mo-barie   (lệnh hợp lệ → mở barie)
```

**Lưu ý khi demo:** phát WiFi từ điện thoại (hotspot), cho **laptop + ESP32 + ESP32-CAM + điện thoại** vào chung mạng đó. Không phụ thuộc WiFi hội trường → không sợ mất mạng giữa lúc thi.

**Về AI đọc biển số:** mô hình AI (YOLO26 + CCT-S) chạy **trên trình duyệt của laptop**, không chạy trên ESP32 (ESP32 quá yếu). ESP32-CAM chỉ đóng vai trò *camera IP*. Đây là cách các hệ thống thật cũng làm — camera ở hiện trường, AI ở máy chủ/máy trạm. Nếu ESP32-CAM cho ảnh mờ (rất hay gặp), **dùng webcam laptop chĩa vào mô hình** làm phương án dự phòng — vẫn đúng luồng nghiệp vụ.

---

## G. CHI PHÍ TỔNG HỢP

| Phương án | Chi phí | Phù hợp |
|-----------|---------|---------|
| **Tối giản** | ~990.000 đ | Đủ thi vòng trường, demo 3 tính năng mạnh nhất |
| **Đầy đủ** | ~1.845.000 đ | Thi vòng tỉnh/quốc gia, quay video quảng bá |
| **Sang** (thêm in 3D container + đèn LED cảnh quan + hộp mica trưng bày) | ~2.500.000 – 3.000.000 đ | Trưng bày triển lãm, gọi vốn |

---

## H. LỜI KHUYÊN THỰC TẾ

1. **Mua dư 1 con ESP32 và 1 servo** — cháy giữa chừng là chuyện thường, đêm trước ngày thi không mua kịp.
2. **Đừng hàn chết** — dùng breadboard + dây Dupont để sửa nhanh tại chỗ thi.
3. **Dán nhãn từng dây** (băng keo giấy ghi "servo", "siêu âm"…) — tháo lắp vận chuyển không bị rối.
4. **Quay sẵn video demo 90 giây** phòng trường hợp phần cứng chết tại hội trường.
5. **Chuẩn bị pin dự phòng 5V** (sạc dự phòng điện thoại) — hội trường có thể không có ổ điện gần bàn.
6. **Tập kịch bản 5–7 lần**, canh đúng 90 giây. Vừa thao tác vừa nói: *"Đây là dữ liệu thật đang chạy vào hệ thống, mời thầy/cô mở điện thoại kiểm tra"* — rồi đưa điện thoại cho giám khảo xem app PWA.
