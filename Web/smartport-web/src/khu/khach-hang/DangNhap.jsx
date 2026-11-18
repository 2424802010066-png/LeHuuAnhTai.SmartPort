import { useEffect, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { noiBo, noiVeSauDangNhap } from './noiVe'
import DangNhapNhanh from './DangNhapNhanh'
import QuetKhuonMat from './QuetKhuonMat'
import './khach-hang.css'

const KHOA_NHO_TEN = 'sp-user'

/** Dang nhap bang mat khau hoac khuon mat (thay Account/Login + modal Face ID cua ban MVC). */
export default function DangNhap() {
  const { taiKhoan, dangNhap, nhanPhien } = useAuth()
  const [thamSo] = useSearchParams()
  const viTri = useLocation()
  const dieuHuong = useNavigate()

  const [ten, setTen] = useState('')
  const [matKhau, setMatKhau] = useState('')
  const [hienMk, setHienMk] = useState(false)
  const [nhoTen, setNhoTen] = useState(false)
  const [dangGui, setDangGui] = useState(false)
  const [loi, setLoi] = useState('')
  const [moQuet, setMoQuet] = useState(false)
  const [matSanSang, setMatSanSang] = useState(null) // null = chua biet

  // Dia chi quay lai: ?returnUrl= (link cu / API sinh ra) hoac state do YeuCauDangNhap gui sang
  const quayVe = noiBo(thamSo.get('returnUrl')) || noiBo(viTri.state?.quayVe)
  // Vua dat lai mat khau (trang Quen mat khau chuyen sang): dien san ten dang nhap + bao thanh cong
  const tenSauDatLai = viTri.state?.ten
  const thongBaoThanhCong = viTri.state?.thongBao

  useEffect(() => {
    try {
      const daNho = localStorage.getItem(KHOA_NHO_TEN)
      if (daNho) setNhoTen(true)
      if (tenSauDatLai) setTen(tenSauDatLai)
      else if (daNho) setTen(daNho)
    } catch {
      /* trinh duyet chan localStorage: bo qua */
    }
  }, [tenSauDatLai])

  // Dich vu AI tat thi khoa nut khuon mat ngay tu dau (khong de nguoi dung bam vao roi doi)
  useEffect(() => {
    let con = true
    goiApi('/api/tai-khoan-ca-nhan/khuon-mat/dich-vu', { coToken: false })
      .then((d) => con && setMatSanSang(!!d.sanSang))
      .catch(() => con && setMatSanSang(false))
    return () => {
      con = false
    }
  }, [])

  if (taiKhoan) return <Navigate to={noiVeSauDangNhap(taiKhoan.vaiTro, quayVe)} replace />

  function nhoTenDangNhap(giaTri) {
    try {
      if (nhoTen) localStorage.setItem(KHOA_NHO_TEN, giaTri)
      else localStorage.removeItem(KHOA_NHO_TEN)
    } catch {
      /* bo qua */
    }
  }

  async function gui(e) {
    e.preventDefault()
    setLoi('')
    setDangGui(true)
    try {
      const kq = await dangNhap(ten.trim(), matKhau)
      nhoTenDangNhap(ten.trim())
      dieuHuong(noiVeSauDangNhap(kq.taiKhoan?.vaiTro, quayVe), { replace: true })
    } catch (err) {
      setLoi(err.message)
    } finally {
      setDangGui(false)
    }
  }

  function xongQuetMat(phien) {
    setMoQuet(false)
    nhanPhien(phien)
    dieuHuong(noiVeSauDangNhap(phien.taiKhoan?.vaiTro, quayVe), { replace: true })
  }

  return (
    <div className="kh-trang-auth">
      <div className="kh-khung">
        <section className="kh-gioi-thieu">
          <div className="d-flex align-items-center gap-3">
            <div className="kh-logo">
              <i className="bi bi-box-seam-fill" />
            </div>
            <div>
              <div className="kh-ten-he-thong">SmartPort</div>
              <div className="kh-phu-de">CẢNG QUỐC TẾ · LOGISTICS</div>
            </div>
          </div>
          <h2 className="kh-tieu-de-lon">
            Điều hành cảng container
            <br />
            trên <span>một màn hình</span>
          </h2>
          <div className="kh-so-lieu">
            <div>
              <b>24/7</b>
              <span>giám sát cổng – bãi</span>
            </div>
            <div>
              <b>3 phút</b>
              <span>xe qua cổng</span>
            </div>
            <div>
              <b>10+</b>
              <span>mô hình AI</span>
            </div>
          </div>
        </section>

        <section className="kh-bieu-mau">
          <span className="kh-nhan-an-toan">
            <i className="bi bi-shield-lock-fill" />
            Kết nối an toàn
          </span>
          <h1 className="h4 fw-bold mt-2 mb-1">Đăng nhập</h1>
          <p className="text-body-secondary small mb-3">Tài khoản nội bộ hoặc tài khoản khách hàng.</p>

          <DangNhapNhanh quayVe={quayVe} />
          <div className="kh-gach-ngang">hoặc dùng tên đăng nhập</div>

          <form onSubmit={gui}>
            {thongBaoThanhCong && !loi && (
              <div className="alert alert-success py-2 small" role="status">
                <i className="bi bi-check-circle-fill me-1" />
                {thongBaoThanhCong}
              </div>
            )}
            {loi && (
              <div className="alert alert-danger py-2 small" role="alert">
                {loi}
              </div>
            )}

            <label className="form-label small fw-semibold mb-1" htmlFor="kh-ten">
              Tên đăng nhập
            </label>
            <div className="kh-o-nhap mb-3">
              <i className="bi bi-person kh-icon" />
              <input
                id="kh-ten"
                className="form-control"
                value={ten}
                onChange={(e) => setTen(e.target.value)}
                autoComplete="username"
                autoFocus
                required
              />
            </div>

            <div className="d-flex justify-content-between align-items-baseline">
              <label className="form-label small fw-semibold mb-1" htmlFor="kh-mk">
                Mật khẩu
              </label>
              <Link className="small" to="/account/quen-mat-khau" state={{ ten: ten.trim() }}>
                Quên mật khẩu?
              </Link>
            </div>
            <div className="kh-o-nhap mb-3">
              <i className="bi bi-lock kh-icon" />
              <input
                id="kh-mk"
                type={hienMk ? 'text' : 'password'}
                className="form-control"
                value={matKhau}
                onChange={(e) => setMatKhau(e.target.value)}
                autoComplete="current-password"
                required
              />
              <button
                type="button"
                className="kh-mat"
                onClick={() => setHienMk((v) => !v)}
                title={hienMk ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                aria-label={hienMk ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                tabIndex={-1}
              >
                <i className={'bi ' + (hienMk ? 'bi-eye-slash' : 'bi-eye')} />
              </button>
            </div>

            <div className="form-check mb-3">
              <input className="form-check-input" type="checkbox" id="kh-nho" checked={nhoTen} onChange={(e) => setNhoTen(e.target.checked)} />
              <label className="form-check-label small" htmlFor="kh-nho">
                Ghi nhớ tên đăng nhập trên máy này
              </label>
            </div>

            <button className="btn btn-primary w-100 py-2" disabled={dangGui}>
              {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-box-arrow-in-right me-2" />}
              Đăng nhập
            </button>
          </form>

          <div className="kh-gach-ngang">hoặc</div>

          <button type="button" className="btn kh-nut-mat" onClick={() => setMoQuet(true)} disabled={matSanSang === false}>
            <i className="bi bi-person-bounding-box" />
            Đăng nhập bằng khuôn mặt
          </button>
          <div className="small text-body-secondary text-center mt-2">
            {matSanSang === false ? (
              <>
                <i className="bi bi-exclamation-triangle me-1" />
                Dịch vụ AI khuôn mặt chưa chạy — hãy đăng nhập bằng mật khẩu.
              </>
            ) : (
              <>
                Lần đầu dùng? Đăng nhập bằng mật khẩu rồi vào <b>Hồ sơ</b> → <b>Cài đặt khuôn mặt</b>.
              </>
            )}
          </div>

          <div className="text-center small mt-4">
            Chưa có tài khoản? <Link to="/account/register">Đăng ký khách hàng</Link>
          </div>
          <div className="text-center small mt-2">
            <Link to="/tracking">Tra cứu container không cần đăng nhập</Link>
          </div>
        </section>
      </div>

      <QuetKhuonMat mo={moQuet} dong={() => setMoQuet(false)} khiXong={xongQuetMat} />
    </div>
  )
}
