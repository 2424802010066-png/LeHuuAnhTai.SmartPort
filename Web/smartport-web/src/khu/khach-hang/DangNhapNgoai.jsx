import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import HoanTatDangKy from './HoanTatDangKy'
import { LogoGoogle } from './DangNhapNhanh'
import { noiBo, noiVeSauDangNhap } from './noiVe'
import './khach-hang.css'

/** base64url -> chuoi UTF-8 */
function giaiB64(s) {
  const b = atob(s.replace(/-/g, '+').replace(/_/g, '/') + '==='.slice((s.length + 3) % 4))
  return new TextDecoder().decode(Uint8Array.from(b, (c) => c.charCodeAt(0)))
}

/** Doc ket qua may chu gui sau dau # (fragment: khong bao gio gui len may chu / vao nhat ky) — chi 1 lan */
function docKetQua() {
  const p = new URLSearchParams(window.location.hash.replace(/^#/, ''))
  const kq = Object.fromEntries(p.entries())
  if (kq.phien) {
    try {
      kq.phien = JSON.parse(giaiB64(kq.phien))
    } catch {
      return { loi: 'Phiên đăng nhập trả về bị hỏng — vui lòng thử lại.' }
    }
  }
  return kq
}

/**
 * Trang dich sau "Dang nhap voi Google" (/api/auth/google/goi-lai chuyen ve day):
 *   #phien=... -> luu phien, vao trang can den | #ve=... -> hoan tat dang ky | #loi=... -> bao loi.
 */
export default function DangNhapNgoai() {
  const { nhanPhien } = useAuth()
  const dieuHuong = useNavigate()
  const [kq] = useState(docKetQua)

  useEffect(() => {
    // Xoa phan sau dau # khoi thanh dia chi + lich su (token khong nam lai tren man hinh / lich su trinh duyet)
    if (window.location.hash) window.history.replaceState(null, '', window.location.pathname)
    if (kq.phien?.token) {
      nhanPhien(kq.phien)
      dieuHuong(noiVeSauDangNhap(kq.phien.taiKhoan?.vaiTro, noiBo(kq.quayVe)), { replace: true })
    }
  }, [kq, nhanPhien, dieuHuong])

  if (!kq.phien && !kq.ve && !kq.loi) return <Navigate to="/account/login" replace />

  function xong(phien) {
    nhanPhien(phien)
    dieuHuong(noiVeSauDangNhap(phien.taiKhoan?.vaiTro, noiBo(kq.quayVe)), { replace: true })
  }

  return (
    <div className="kh-trang-auth">
      <div className="kh-khung kh-hep">
        <section className="kh-bieu-mau">
          <div className="d-flex align-items-center gap-2 mb-2">
            <LogoGoogle co={22} />
            <span className="kh-nhan-an-toan">
              <i className="bi bi-shield-lock-fill" />
              Đăng nhập an toàn với Google
            </span>
          </div>

          {kq.phien && (
            <div className="text-center py-4">
              <span className="spinner-border text-primary" />
              <div className="small text-body-secondary mt-2">Đang vào SmartPort…</div>
            </div>
          )}

          {kq.ve && (
            <>
              <h1 className="h4 fw-bold mt-2 mb-3">Hoàn tất đăng ký</h1>
              <HoanTatDangKy ve={kq.ve} kenh={kq.kenh || 'google'} hoTenGoiY={kq.hoTen} email={kq.email} khiXong={xong} />
            </>
          )}

          {kq.loi && (
            <>
              <h1 className="h4 fw-bold mt-2 mb-3">Chưa đăng nhập được</h1>
              <div className="alert alert-danger small" role="alert">
                <i className="bi bi-exclamation-octagon me-1" />
                {kq.loi}
              </div>
              <Link className="btn btn-primary w-100" to="/account/login">
                <i className="bi bi-arrow-left me-1" />
                Về trang đăng nhập
              </Link>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
