import { useState } from 'react'
import { goiApi } from '../../api/client'

/**
 * HOAN TAT DANG KY sau khi xac minh danh tinh bang Google / so dien thoai (tai khoan chua co): them ten cong ty
 * (khach hang SmartPort la doanh nghiep / ca nhan thue dich vu cang). Ve ky HMAC 15 phut tu may chu mang danh tinh
 * da xac minh — trinh duyet khong sua duoc email Google / so dien thoai.
 * API: POST /api/auth/ngoai/hoan-tat -> phien dang nhap.
 */
export default function HoanTatDangKy({ ve, kenh, hoTenGoiY, email, soDienThoai, khiXong }) {
  const laGoogle = kenh === 'google'
  const [f, setF] = useState({
    hoTen: hoTenGoiY || '',
    tenCongTy: '',
    email: '',
    soDienThoai: '',
    maSoThue: '',
    diaChi: '',
  })
  const [dangGui, setDangGui] = useState(false)
  const [loi, setLoi] = useState('')
  const doi = (k) => (e) => setF({ ...f, [k]: e.target.value })
  const rong = (s) => (s.trim() ? s.trim() : null)

  async function gui(e) {
    e.preventDefault()
    setLoi('')
    setDangGui(true)
    try {
      const phien = await goiApi('/api/auth/ngoai/hoan-tat', {
        method: 'POST',
        coToken: false,
        body: {
          ve,
          hoTen: f.hoTen.trim(),
          tenCongTy: f.tenCongTy.trim(),
          email: laGoogle ? null : rong(f.email),
          soDienThoai: laGoogle ? rong(f.soDienThoai) : null,
          maSoThue: rong(f.maSoThue),
          diaChi: rong(f.diaChi),
        },
      })
      khiXong(phien)
    } catch (err) {
      setLoi(err.message)
    } finally {
      setDangGui(false)
    }
  }

  return (
    <form onSubmit={gui}>
      <div className="alert alert-success py-2 small">
        <i className={'bi me-1 ' + (laGoogle ? 'bi-google' : 'bi-phone')} />
        {laGoogle ? (
          <>
            Đã xác minh tài khoản Google <b>{email}</b>.
          </>
        ) : (
          <>
            Đã xác minh số điện thoại <b>{soDienThoai}</b>.
          </>
        )}{' '}
        Thêm vài thông tin để tạo tài khoản khách hàng.
      </div>
      {loi && (
        <div className="alert alert-danger py-2 small" role="alert">
          {loi}
        </div>
      )}

      <label className="form-label small fw-semibold mb-1" htmlFor="ht-ten">
        Họ tên <span className="text-danger">*</span>
      </label>
      <input id="ht-ten" className="form-control mb-2" value={f.hoTen} onChange={doi('hoTen')} maxLength={100} required autoFocus autoComplete="name" />

      <label className="form-label small fw-semibold mb-1" htmlFor="ht-cty">
        Tên công ty / cá nhân thuê dịch vụ <span className="text-danger">*</span>
      </label>
      <input
        id="ht-cty"
        className="form-control mb-2"
        value={f.tenCongTy}
        onChange={doi('tenCongTy')}
        maxLength={150}
        required
        placeholder="vd: Công ty TNHH Vận tải ABC"
        autoComplete="organization"
      />

      {laGoogle ? (
        <>
          <label className="form-label small fw-semibold mb-1" htmlFor="ht-sdt">
            Số điện thoại <span className="text-body-secondary fw-normal">(không bắt buộc)</span>
          </label>
          <input id="ht-sdt" className="form-control mb-2" value={f.soDienThoai} onChange={doi('soDienThoai')} maxLength={20} inputMode="tel" autoComplete="tel" />
        </>
      ) : (
        <>
          <label className="form-label small fw-semibold mb-1" htmlFor="ht-email">
            Email <span className="text-body-secondary fw-normal">(nên có — để nhận hóa đơn và lấy lại mật khẩu)</span>
          </label>
          <input id="ht-email" type="email" className="form-control mb-2" value={f.email} onChange={doi('email')} maxLength={100} autoComplete="email" />
        </>
      )}

      <div className="row g-2 mb-3">
        <div className="col-5">
          <label className="form-label small fw-semibold mb-1" htmlFor="ht-mst">
            Mã số thuế
          </label>
          <input id="ht-mst" className="form-control" value={f.maSoThue} onChange={doi('maSoThue')} maxLength={20} />
        </div>
        <div className="col-7">
          <label className="form-label small fw-semibold mb-1" htmlFor="ht-dc">
            Địa chỉ
          </label>
          <input id="ht-dc" className="form-control" value={f.diaChi} onChange={doi('diaChi')} maxLength={200} autoComplete="street-address" />
        </div>
      </div>

      <button className="btn btn-primary w-100 py-2" disabled={dangGui}>
        {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-person-check me-2" />}
        Tạo tài khoản và đăng nhập
      </button>
      <div className="small text-body-secondary mt-2">
        Tạo tài khoản là bạn đồng ý để SmartPort lưu thông tin trên nhằm cung cấp dịch vụ cảng. Bạn đăng nhập lại bằng{' '}
        {laGoogle ? 'Google' : 'số điện thoại (mã OTP)'}; muốn dùng mật khẩu thì vào “Quên mật khẩu” bằng email.
      </div>
    </form>
  )
}
