import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import './styles.css'
import { AuthProvider } from './auth/AuthContext'
import { ThongBaoNhanhProvider } from './components/ui/ThongBaoNhanh'
import { XacNhanProvider } from './components/ui/XacNhan'
import App from './App'
import { theoDoiCapNhat } from './ngoaiTuyen/capNhatPwa'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <ThongBaoNhanhProvider>
          <XacNhanProvider>
            <App />
          </XacNhanProvider>
        </ThongBaoNhanhProvider>
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)

// PWA: chi dang ky o ban build that — khi dev bang Vite, service worker se cache nham ma nguon cu.
// sw.js cai san VO UNG DUNG (index.html + moi chunk) -> tram cong mat Wi-Fi van mo lai duoc man chot cong.
// Chi co tren HTTPS / localhost: http://<IP-LAN> khong co service worker (xem thanh trang thai mang).
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker
      .register('/sw.js', { updateViaCache: 'none' })
      .then((reg) => {
        // Ban moi KHONG tu chiem quyen: thanh trang thai hien "Tai lai de cap nhat" (src/ngoaiTuyen/capNhatPwa.js)
        theoDoiCapNhat(reg)
        // Tab tram mo ca ngay: 30 phut hoi ban moi 1 lan (mat mang thi bo qua)
        setInterval(() => reg.update().catch(() => {}), 30 * 60 * 1000)
      })
      .catch(() => {
        /* trinh duyet cu hoac nguoi dung chan: web van chay binh thuong */
      })
  })
}

// Xin trinh duyet KHONG tu xoa du lieu trang (hop thu di luot xe, mo hinh AI) khi o dia day.
// Chi hoi khi chua duoc cap (Firefox co the hien hop hoi 1 lan); HTTP qua LAN khong co API nay -> bo qua.
if (navigator.storage?.persisted && navigator.storage?.persist) {
  navigator.storage
    .persisted()
    .then((da) => (da ? true : navigator.storage.persist()))
    .catch(() => {})
}
