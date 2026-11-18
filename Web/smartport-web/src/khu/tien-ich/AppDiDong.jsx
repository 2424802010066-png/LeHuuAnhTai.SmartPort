import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { DauTrang } from './chung'

/**
 * TRANG UNG DUNG DI DONG (PWA) — thay Views/Mobile/Index.cshtml, dia chi /mobile.
 * Day la start_url trong manifest.webmanifest: mo ra khi nguoi dung cai SmartPort len
 * man hinh chinh dien thoai. Luoi chuc nang doi theo vai tro dang dang nhap.
 *
 * TAM THOI o day cung lo viec dang ky service worker va gan the <link rel="manifest">:
 * dung ra hai viec nay thuoc main.jsx / index.html (dung chung) — xem bao cao cuoi.
 */

/** Gan the manifest vao <head> neu trang chua co (index.html dung chung chua khai bao) */
function ganManifest() {
  if (document.querySelector('link[rel="manifest"]')) return
  const l = document.createElement('link')
  l.rel = 'manifest'
  l.href = '/manifest.webmanifest'
  document.head.appendChild(l)
}

function O({ den, icon, ten, mo, noiBat, ngoai }) {
  const noiDung = (
    <>
      <div className="ti-o-app-icon" aria-hidden="true">
        <i className={'bi ' + icon} />
      </div>
      <div className="ti-o-app-ten">{ten}</div>
      <div className="ti-o-app-mo">{mo}</div>
    </>
  )
  const lop = 'ti-o-app' + (noiBat ? ' noi-bat' : '')
  return (
    <div className="col-6 col-md-4 col-lg-3">
      {ngoai ? (
        <a className={lop} href={den}>
          {noiDung}
        </a>
      ) : (
        <Link className={lop} to={den}>
          {noiDung}
        </Link>
      )}
    </div>
  )
}

function Nhom({ ten, children }) {
  return (
    <>
      <div className="small fw-bold text-body-secondary text-uppercase mt-4 mb-2" style={{ letterSpacing: '.06em' }}>
        {ten}
      </div>
      <div className="row g-3">{children}</div>
    </>
  )
}

export default function AppDiDong() {
  const { taiKhoan } = useAuth()
  const vaiTro = taiKhoan?.vaiTro || ''
  const laKhach = vaiTro === 'KhachHang'
  const laNoiBo = vaiTro === 'Admin' || vaiTro === 'NhanVien'
  const [suKienCai, setSuKienCai] = useState(null)
  const [goiYIos, setGoiYIos] = useState(false)

  useEffect(() => {
    ganManifest()
    // Service worker chi chay tren HTTPS hoac localhost; loi thi bo qua (trang van dung binh thuong)
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('/sw.js').catch(() => {})

    const khiCaiDuoc = (e) => {
      e.preventDefault()
      setSuKienCai(e)
    }
    window.addEventListener('beforeinstallprompt', khiCaiDuoc)

    // iOS khong co beforeinstallprompt -> chi ra cho nguoi dung bam "Chia sẻ → Thêm vào MH chính"
    const laIos = /iphone|ipad|ipod/i.test(navigator.userAgent)
    const dangStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
    setGoiYIos(laIos && !dangStandalone)

    return () => window.removeEventListener('beforeinstallprompt', khiCaiDuoc)
  }, [])

  async function cai() {
    if (!suKienCai) return
    suKienCai.prompt()
    setSuKienCai(null)
  }

  return (
    <>
      <DauTrang
        icon="bi-phone"
        tieuDe="Ứng dụng SmartPort"
        mucHienTai="Ứng dụng di động"
        phu={taiKhoan ? `Xin chào ${taiKhoan.hoTen || taiKhoan.tenDangNhap} · ${vaiTro}` : 'Tài xế check-in lệnh · chủ hàng theo dõi container · điều độ giám sát mọi nơi.'}
      />
      <div className="container pb-5">
        {suKienCai && (
          <button type="button" className="btn btn-warning btn-lg w-100 fw-bold mb-3" onClick={cai}>
            <i className="bi bi-download me-2" />
            Cài SmartPort lên màn hình chính
          </button>
        )}
        {goiYIos && (
          <div className="alert alert-light border small">
            <i className="bi bi-apple me-1" />
            Trên iPhone: bấm nút <b>Chia sẻ</b> (ô vuông có mũi tên) → <b>Thêm vào MH chính</b> để cài app.
          </div>
        )}

        <Nhom ten="Dành cho tài xế & nhà xe">
          <O den="/taixe" icon="bi-truck" ten="Check-in tài xế" mo="Nhập số lệnh + PIN — tới cổng được ưu tiên" noiBat />
          <O den="/cong" icon="bi-stoplights" ten="Tình trạng cổng" mo="Mật độ realtime — xem trước khi cho xe chạy" />
        </Nhom>

        <Nhom ten="Dành cho chủ hàng">
          {laKhach ? (
            <>
              <O den="/hangcuatoi" icon="bi-box-seam" ten="Hàng & lệnh của tôi" mo="Vị trí cont, nhiệt độ lạnh, ảnh chụp" noiBat />
              <O den="/booking/create" icon="bi-bell" ten="Đặt dịch vụ" mo="Nâng hạ, lưu bãi, đóng rút…" />
              <O den="/booking/my" icon="bi-receipt" ten="Booking của tôi" mo="Theo dõi đơn + hóa đơn" />
              <O den="/payment/history" icon="bi-credit-card" ten="Thanh toán" mo="QR VietQR, lịch sử giao dịch" />
            </>
          ) : (
            <>
              <O den="/tracking" icon="bi-search" ten="Tra cứu container" mo="Vị trí bãi, phí lưu, nhiệt độ reefer" />
              <O den="/account/login" icon="bi-person-circle" ten="Đăng nhập" mo="Chủ hàng xem hàng & lệnh của mình" />
            </>
          )}
          <O den="/schedule" icon="bi-calendar2-week" ten="Lịch tàu" mo="ETA/ETD các chuyến sắp cập" />
          <O den="/cost" icon="bi-calculator" ten="Ước tính phí" mo="Báo giá dịch vụ tức thì" />
        </Nhom>

        {laNoiBo && (
          <Nhom ten="Điều hành cảng (nội bộ)">
            <O den="/admin" icon="bi-speedometer2" ten="Trang quản trị" mo="Tổng quan điều hành" noiBat />
            <O den="/admin/gate" icon="bi-camera-video" ten="Cổng ALPR" mo="Nhận diện biển số" />
            <O den="/admin/security" icon="bi-shield-check" ten="An ninh AI" mo="Sự cố realtime" />
            <O den="/admin/reefer" icon="bi-snow" ten="Cont lạnh" mo="Nhiệt độ + báo động" />
            <O den="/admin/berths/gantt" icon="bi-bar-chart-steps" ten="Gantt cầu bến" mo="Lịch tàu chiếm bến" />
            <O den="/admin/dashboard/giatriai" icon="bi-robot" ten="Giá trị AI" mo="ROI đo được" />
          </Nhom>
        )}

        <Nhom ten="Tin tức & dịch vụ">
          <O den="/news" icon="bi-newspaper" ten="Tin tức cảng" mo="Thông báo khai thác, sự kiện" />
          <O den="/services" icon="bi-tools" ten="Dịch vụ & bảng giá" mo="Biểu giá đang áp dụng" />
        </Nhom>
      </div>
    </>
  )
}
