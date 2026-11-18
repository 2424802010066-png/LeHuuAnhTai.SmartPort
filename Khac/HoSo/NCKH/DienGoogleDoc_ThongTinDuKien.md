# ĐIỀN VÀO GOOGLE DOC CỦA THẦY — THÔNG TIN DỰ KIẾN

> Bản này để **copy–paste thẳng** vào file Google Doc thầy Luân gửi
> (docs.google.com/document/d/1cHsrbwkRjKYqLhGlrXWat0gTHYFgfo9K).
> Giữ ngắn gọn — thầy chỉ cần nắm hướng nghiên cứu để trao đổi.

---

**Họ và tên:** Lê Hữu Anh Tài — **MSSV:** 2424802010066

**Lớp / Ngành:** …………… — Viện Công nghệ số

**Điện thoại / Email:** 0387 562 579 — lehuuanhtai2020tkh@gmail.com

**Giảng viên hướng dẫn mong muốn:** TS. Huỳnh Nguyễn Thành Luân

**Loại đề tài đăng ký:** Loại C (5 triệu)

---

**Tên đề tài dự kiến:**

Nghiên cứu mô hình học sâu triển khai tại biên cho dự báo sản lượng container và tự động hóa cổng cảng — thực nghiệm trên dữ liệu cảng biển Việt Nam

*(Edge-deployable deep learning for container throughput forecasting and automated port gate operations: experiments on Vietnamese seaport data)*

---

**Hướng nghiên cứu (tóm tắt):**

Đề tài giải quyết hai bài toán lõi của cảng cạn (ICD) và depot container vừa và nhỏ:

1. **Dự báo sản lượng container ngắn hạn** bằng mô hình không gian trạng thái chọn lọc (Selective SSM / Mamba), có bổ sung đặc trưng **lịch Âm — Tết Nguyên đán**. Đây là đặc thù bản địa mà các bộ dữ liệu chuẩn quốc tế không có, và các mô hình mùa vụ chu kỳ cố định 12 tháng không mô tả được.

2. **Nhận dạng tự động tại cổng** (biển số xe và mã container theo ISO 6346) bằng pipeline hai tầng chạy **hoàn toàn trên trình duyệt** (ONNX Runtime Web / WebAssembly) — không cần máy chủ GPU, dữ liệu ảnh không rời khỏi thiết bị.

Điểm chung của hai bài toán: hướng tới nhóm doanh nghiệp logistics vừa và nhỏ ở Bình Dương — nhóm **không đủ nguồn lực triển khai AI tập trung** như các tập đoàn khai thác cảng lớn.

---

**Nền tảng đã có (không bắt đầu từ con số không):**

- Hệ thống **SmartPort** do em tự xây dựng, đang chạy: ASP.NET Core .NET 10 + React, kiến trúc modular monolith hướng sự kiện, 277 endpoint, 40 bảng dữ liệu; đã tích hợp sẵn YOLO26n, CCT-S v2, Tesseract LSTM chạy trên trình duyệt. → dùng làm **bệ thử nghiệm có sẵn**.
- **Bộ dữ liệu 66 tháng** (01/2021 – 06/2026) sản lượng container thông qua cảng biển Việt Nam, thu thập từ Cục Hàng hải và Đường thủy Việt Nam, đã làm sạch và kiểm tra nhất quán.
- **Một vòng thực nghiệm đối chứng đã chạy xong** (20 hạt giống, tách tập theo thời gian 36/12/18 tháng):

| Mô hình | MAE (nghìn TEU) | MAPE (%) |
|---|---:|---:|
| Naive (tháng trước) | 302,9 | 10,65 |
| AR(3) | 286,6 | 9,64 |
| MLP (3 độ trễ) — tốt nhất hiện tại | 259,8 ± 29,6 | 9,06 ± 1,04 |

Điểm yếu đã phát hiện: khi **dự báo cuộn 18 tháng**, MAPE của MLP vọt lên **20,35%**. Thêm đặc trưng Tết thô sơ kéo về 14,69% — chứng tỏ tín hiệu Tết là thật nhưng chưa được mã hóa đúng cách. **Đây chính là khoảng trống đề tài muốn lấp.**

---

**Mục tiêu định lượng:**

- Giảm **≥ 15% MAE** so với mô hình tốt nhất hiện có (MLP, MAE = 259,8).
- Giảm **≥ 25% MAPE** ở kịch bản dự báo cuộn nhiều bước (hiện 20,35%).
- Nhận dạng tại cổng: chính xác cấp ký tự **≥ 95%**, độ trễ **≤ 1,5 giây/ảnh** trên máy không GPU rời.

**Sản phẩm dự kiến:** 01 bài báo (định hướng hội nghị FDSE — theo mô hình bài FDSE 2024 của thầy) + bộ dữ liệu + mô-đun tích hợp trên SmartPort + báo cáo tổng kết.

**Thời gian thực hiện:** 12 tháng (10/2026 – 9/2027).

---

**Câu hỏi muốn xin ý kiến thầy:**

1. Hướng "SSM chọn lọc + đặc trưng lịch Âm" có đủ tính mới để nhắm hội nghị quốc tế không, hay nên siết lại phạm vi vào một bài toán duy nhất?
2. Với đề tài loại C, thầy thấy nên ưu tiên chiều sâu mô hình dự báo, hay giữ cả hai bài toán để sản phẩm hệ thống trọn vẹn hơn?
3. Em có nên tìm một ICD/depot ở Bình Dương để xin chụp ảnh thực địa không, hay dùng dữ liệu mô phỏng + ảnh công khai là đủ cho phạm vi loại C?
