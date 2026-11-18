# smartport-web

Giao diện React (SPA) của SmartPort, gọi `SmartPort.Api`. Kiến trúc đầy đủ: [../KIEN-TRUC-FE-BE.md](../KIEN-TRUC-FE-BE.md).

```bash
npm install
npm run dev      # http://localhost:5173 (cần SmartPort.Api chạy ở http://localhost:5200)
npm run build    # ra thư mục dist/ (kèm dist/ai: mô hình và thư viện AI cần dùng)
```

Biến môi trường (tạo file `.env.local` nếu cần đổi):

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `VITE_API_URL` | `http://localhost:5200` | Địa chỉ Web API |
| `VITE_MVC_URL` | `http://localhost:5123` | Bản MVC cho các màn hình chưa chuyển |

## Cấu trúc

```
src/
  api/        client.js (gắn token, đổi lỗi HTTP thành thông báo tiếng Việt) · useApi.js
  auth/       AuthContext.jsx (đăng nhập, tự đăng xuất khi token hết hạn hoặc API trả 401)
  realtime/   SuKienContext.jsx (nhận sự kiện Server-Sent Events, tự nối lại)
  ai/         alpr.js (YOLO26 + CCT-S v2 + Tesseract) · mlp.js (TensorFlow.js) · ocrContainer.js (ISO 6346)
  components/ bố cục, thẻ chỉ số, bảng tin sự kiện
  pages/      DangNhap · TongQuan · CongThongMinh · AiLab · TraCuuContainer
```

Mô hình và thư viện AI **không** chép vào repo: `vite.config.js` phục vụ thẳng từ
`../SmartPort.Net10/wwwroot` ở đường dẫn `/ai/vendor`, `/ai/models`, `/ai/mau`; khi build chỉ chép đúng các file cần.
