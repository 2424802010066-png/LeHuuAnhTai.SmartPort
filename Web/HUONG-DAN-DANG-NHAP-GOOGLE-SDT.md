# Bật đăng nhập Google, gửi mã qua Gmail thật và SMS thật

Mặc định hệ thống chạy **chế độ demo** (không cần tài khoản gì):

| Thứ | Chế độ demo | Chạy thật cần |
|---|---|---|
| Email (mã quên mật khẩu, thông báo) | lưu file `.eml` ở `DichVu/SmartPort.DichVu.DinhDanh/App_Data/Outbox/` | Mật khẩu ứng dụng Gmail (mục A) |
| SMS (mã OTP số điện thoại) | lưu file `.txt` ở `DichVu/SmartPort.DichVu.DinhDanh/App_Data/OutboxSms/` + máy phát triển hiện luôn mã trên màn hình | Tài khoản Twilio (mục C) |
| Nút "Tiếp tục với Google" | mờ, có ghi chú "chưa cấu hình" | OAuth Client ID của Google (mục B) |

Mọi khóa bí mật điền vào **một file duy nhất**: `Web/MicroServices/.bimat/bi-mat.json` (đã nằm trong `.gitignore` —
**không bao giờ** đẩy lên GitHub). Điền xong thì chạy lại hệ thống (bấm đúp `CHAY_SMARTPORT.bat`).

> Tài khoản `admin` đã gắn sẵn email **lehuuanhtai2020tkh@gmail.com** (CSDL `SmartPortMS_DinhDanh`).

---

## A. Gửi mã thật về Gmail (≈ 5 phút)

1. Bật **Xác minh 2 bước** cho Gmail: <https://myaccount.google.com/signinoptions/twosv>
2. Tạo **Mật khẩu ứng dụng**: <https://myaccount.google.com/apppasswords> → đặt tên `SmartPort` → Google hiện 16 ký tự.
3. Mở `Web/MicroServices/.bimat/bi-mat.json`, mục `"smtp"` → dán 16 ký tự vào `"pass"` (bỏ dấu cách). Các ô khác đã điền sẵn
   (`smtp.gmail.com`, cổng 587, gửi từ Gmail của bạn).
4. Chạy lại hệ thống → trang **Quên mật khẩu** → nhập `admin` → mã 6 số về hộp thư Gmail.

> Mật khẩu ứng dụng **không phải** mật khẩu Gmail; thu hồi được bất cứ lúc nào ở trang trên.

## B. Đăng nhập bằng Google (≈ 10 phút)

1. Vào <https://console.cloud.google.com/> → tạo project, ví dụ `SmartPort`.
2. **Google Auth Platform** (hoặc *APIs & Services → OAuth consent screen*):
   - *Branding*: tên ứng dụng `SmartPort`, email hỗ trợ = Gmail của bạn.
   - *Audience*: loại **External**; khi còn ở chế độ **Testing** thì thêm `lehuuanhtai2020tkh@gmail.com` (và Gmail của thành
     viên nhóm) vào **Test users** — chỉ những người này đăng nhập được.
3. **Clients → Create client** (hoặc *Credentials → Create credentials → OAuth client ID*):
   - Application type: **Web application**
   - Authorized JavaScript origins: `http://localhost:5100`
   - Authorized redirect URIs: `http://localhost:5100/api/auth/google/goi-lai`  ← phải **giống hệt**
4. Chép **Client ID** và **Client secret** vào `bi-mat.json`, mục `"google"` → `clientId`, `clientSecret`.
5. Chạy lại hệ thống → trang đăng nhập → **Tiếp tục với Google** → chọn Gmail → vào thẳng tài khoản `admin`
   (email Google đã xác minh trùng email tài khoản → tự liên kết). Gmail chưa có tài khoản → màn hình "Hoàn tất đăng ký".

Khi đưa lên mạng (somee…): thêm redirect URI `https://<tên-miền>/api/auth/google/goi-lai` ở Google, đặt
`Google:ClientId`, `Google:ClientSecret`, `Google:DiaChiGoiLai` trong cấu hình máy chủ, và chạy `Database/Add_DangNhapNgoai.sql`
trên CSDL hosting.

## C. SMS thật qua Twilio (tùy chọn)

1. Đăng ký dùng thử: <https://www.twilio.com/try-twilio> (có sẵn tiền dùng thử).
2. **Verify** số điện thoại của bạn trong Twilio — tài khoản dùng thử **chỉ gửi được tới số đã xác minh**.
3. Lấy một số Twilio (Get a phone number) và chép **Account SID**, **Auth Token** ở trang Console.
4. Trong `bi-mat.json`, mục `"sms"`: `"nhaCungCap": "twilio"`, điền `twilioAccountSid`, `twilioAuthToken`, `twilioFrom`
   (số Twilio dạng `+1…`, hoặc Messaging Service SID `MG…`).
5. Chạy lại hệ thống → **Dùng số điện thoại** → mã về điện thoại thật.

> Ở Việt Nam, gửi OTP thương mại thường dùng **SMS brandname** (eSMS, SpeedSMS, VNPT…) — chỉ cần thêm một nhánh trong
> `SmartPort.Core/AppServices/SmsService.cs`, phần còn lại giữ nguyên.

---

## Để trả lời vấn đáp: an toàn ở đâu?

| Mối nguy | Cách chặn |
|---|---|
| Kẻ gian giả trang đăng nhập Google / đánh cắp `code` | Luồng *authorization code* + **PKCE S256**; đổi `code` lấy token ở **máy chủ** bằng `client_secret` (không xuống trình duyệt) |
| Giả mạo yêu cầu (CSRF) | **state** ngẫu nhiên lưu trong cookie HttpOnly, SameSite=Lax, ký HMAC, chỉ gửi về đường dẫn callback, hết hạn 10 phút |
| Phát lại id_token cũ | **nonce** gắn với phiên; kiểm **chữ ký RS256** bằng khóa công khai Google (JWKS), `iss`, `aud` = Client ID, hạn dùng |
| Đổi email Google để chiếm tài khoản | Định danh bằng **`sub`** (mã bất biến của Google); chỉ tự liên kết khi `email_verified = true` |
| Dò mã OTP | Mã 6 số, **5 phút**, sai **5 lần** là hủy (giữ lượt nguyên tử bằng 1 câu SQL), dùng **1 lần**; chỉ lưu **HMAC** của mã |
| Spam SMS tốn tiền | Mỗi số cách nhau ≥ 60 giây, ≤ 5 mã/giờ; mỗi IP ≤ 20 mã/giờ; giới hạn 10 lần/phút/IP cho mọi API đăng nhập |
| Chiếm SIM để vào tài khoản quản trị | Tài khoản **nội bộ** không tự liên kết theo số; phải đăng nhập mật khẩu rồi tự bật trong **Hồ sơ** |
| Lộ token trên thanh địa chỉ / nhật ký máy chủ | Kết quả Google trả về sau dấu **`#`** (fragment — trình duyệt không gửi lên máy chủ), trang đích xóa ngay khỏi lịch sử |
| Chuyển hướng ra trang lạ (open redirect) | `quayVe` chỉ nhận đường dẫn nội bộ bắt đầu bằng `/` |

Đăng ký mới (mật khẩu / Google / số điện thoại) ở chế độ microservices là một **saga 2 bước**: Định danh tạo tài khoản +
gửi lệnh `kinh-doanh.khach-hang-can-tao` (Outbox, cùng giao dịch) → Kinh doanh tạo doanh nghiệp → phát
`kinh-doanh.khach-hang-da-tao` → Định danh gắn `MaKH`. Kiểm thử: nhóm `dang-nhap-ngoai` (30 kiểm tra, không cần mạng) và
nhóm `microservices` (luồng thật qua gateway).
