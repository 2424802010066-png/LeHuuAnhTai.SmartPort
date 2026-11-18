import { useState } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { goiApi, LoiApi } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { Truong } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import DangNhapNhanh from './DangNhapNhanh'
import DoManhMatKhau from './DoManhMatKhau'
import { noiBo, noiVeSauDangNhap } from './noiVe'
import './khach-hang.css'

const RONG = {
  tenDangNhap: '',
  matKhau: '',
  xacNhanMatKhau: '',
  hoTen: '',
  email: '',
  soDienThoai: '',
  tenCongTy: '',
  maSoThue: '',
  diaChi: '',
}

/** {"TenDangNhap":["..."]} cua API -> {tenDangNhap:"..."} de gan vao dung o nhap */
function loiTheoO(chiTiet) {
  const ra = {}
  for (const [khoa, ds] of Object.entries(chiTiet?.errors || {})) {
    ra[khoa.charAt(0).toLowerCase() + khoa.slice(1)] = Array.isArray(ds) ? ds[0] : String(ds)
  }
  return ra
}

/** Dang ky tai khoan khach hang (thay Account/Register ban MVC) — dang ky xong dang nhap luon. */
export default function DangKy() {
  const { taiKhoan, nhanPhien } = useAuth()
  const { gt, doi } = useBieuMau(RONG)
  const [loiO, setLoiO] = useState({})
  const [loiChung, setLoiChung] = useState('')
  const [dangGui, setDangGui] = useState(false)
  const [thamSo] = useSearchParams()
  const dieuHuong = useNavigate()
  const quayVe = noiBo(thamSo.get('returnUrl'))

  if (taiKhoan) return <Navigate to={noiVeSauDangNhap(taiKhoan.vaiTro, quayVe)} replace />

  async function gui(e) {
    e.preventDefault()
    setLoiO({})
    setLoiChung('')
    setDangGui(true)
    try {
      const kq = await goiApi('/api/tai-khoan-ca-nhan/dang-ky', { method: 'POST', body: gt, coToken: false })
      nhanPhien(kq)
      dieuHuong(noiVeSauDangNhap(kq.taiKhoan?.vaiTro, quayVe), { replace: true })
    } catch (err) {
      const theoO = err instanceof LoiApi ? loiTheoO(err.chiTiet) : {}
      setLoiO(theoO)
      if (!Object.keys(theoO).length) setLoiChung(err.message)
    } finally {
      setDangGui(false)
    }
  }

  return (
    <div className="kh-trang-auth">
      <div className="kh-khung kh-rong">
        <section className="kh-bieu-mau">
          <div className="d-flex align-items-center gap-3 mb-3">
            <div className="kh-logo">
              <i className="bi bi-person-plus-fill" />
            </div>
            <div>
              <h1 className="h4 fw-bold mb-0">Đăng ký tài khoản khách hàng</h1>
              <div className="text-body-secondary small">Đặt dịch vụ, theo dõi container và hóa đơn của doanh nghiệp bạn.</div>
            </div>
          </div>

          <div className="kh-dang-ky-nhanh">
            <DangNhapNhanh quayVe={quayVe} cheDo="dang-ky" />
          </div>
          <div className="kh-gach-ngang">hoặc điền đầy đủ thông tin</div>

          <form onSubmit={gui}>
            {loiChung && (
              <div className="alert alert-danger py-2 small" role="alert">
                {loiChung}
              </div>
            )}

            <h2 className="h6 fw-bold text-primary-emphasis mt-2 mb-2">
              <i className="bi bi-person-badge me-2" />
              Thông tin đăng nhập
            </h2>
            <div className="row g-2">
              <Truong nhan="Tên đăng nhập" batBuoc lop="col-md-4 mb-2" loi={loiO.tenDangNhap} goiY="Chữ, số và dấu gạch dưới">
                <input className="form-control" value={gt.tenDangNhap} onChange={doi('tenDangNhap')} autoComplete="username" autoFocus />
              </Truong>
              <Truong
                nhan="Mật khẩu"
                batBuoc
                lop="col-md-4 mb-2"
                loi={loiO.matKhau}
                goiY={<DoManhMatKhau matKhau={gt.matKhau} ten={gt.tenDangNhap} gon />}
              >
                <input type="password" className="form-control" value={gt.matKhau} onChange={doi('matKhau')} autoComplete="new-password" maxLength={50} />
              </Truong>
              <Truong nhan="Xác nhận mật khẩu" batBuoc lop="col-md-4 mb-2" loi={loiO.xacNhanMatKhau}>
                <input
                  type="password"
                  className="form-control"
                  value={gt.xacNhanMatKhau}
                  onChange={doi('xacNhanMatKhau')}
                  autoComplete="new-password"
                />
              </Truong>
            </div>

            <h2 className="h6 fw-bold text-primary-emphasis mt-3 mb-2">
              <i className="bi bi-buildings me-2" />
              Thông tin doanh nghiệp
            </h2>
            <div className="row g-2">
              <Truong nhan="Tên công ty" batBuoc lop="col-md-8 mb-2" loi={loiO.tenCongTy}>
                <input className="form-control" value={gt.tenCongTy} onChange={doi('tenCongTy')} placeholder="Công ty TNHH …" />
              </Truong>
              <Truong nhan="Mã số thuế" lop="col-md-4 mb-2" loi={loiO.maSoThue}>
                <input className="form-control" value={gt.maSoThue} onChange={doi('maSoThue')} />
              </Truong>
              <Truong nhan="Địa chỉ" lop="col-12 mb-2" loi={loiO.diaChi}>
                <input className="form-control" value={gt.diaChi} onChange={doi('diaChi')} />
              </Truong>
              <Truong nhan="Người liên hệ" batBuoc lop="col-md-4 mb-2" loi={loiO.hoTen}>
                <input className="form-control" value={gt.hoTen} onChange={doi('hoTen')} autoComplete="name" />
              </Truong>
              <Truong nhan="Email" batBuoc lop="col-md-4 mb-2" loi={loiO.email}>
                <input type="email" className="form-control" value={gt.email} onChange={doi('email')} autoComplete="email" />
              </Truong>
              <Truong nhan="Số điện thoại" lop="col-md-4 mb-2" loi={loiO.soDienThoai}>
                <input className="form-control" value={gt.soDienThoai} onChange={doi('soDienThoai')} autoComplete="tel" />
              </Truong>
            </div>

            <div className="d-flex flex-wrap align-items-center gap-3 mt-3">
              <button className="btn btn-primary px-4" disabled={dangGui}>
                {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-check2-circle me-2" />}
                Đăng ký
              </button>
              <span className="small text-body-secondary">
                Đã có tài khoản? <Link to="/account/login">Đăng nhập</Link>
              </span>
            </div>
          </form>
        </section>
      </div>
    </div>
  )
}
