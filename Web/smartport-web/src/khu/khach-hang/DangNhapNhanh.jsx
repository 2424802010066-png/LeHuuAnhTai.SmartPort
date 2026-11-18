import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { API_URL, goiApi, taoQuery } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import HoanTatDangKy from './HoanTatDangKy'
import { noiVeSauDangNhap } from './noiVe'

const hai = (n) => String(n).padStart(2, '0')
const dongHo = (giay) => `${hai(Math.floor(giay / 60))}:${hai(giay % 60)}`

/** Logo "G" 4 mau cua Google (SVG noi tuyen — khong tai tu mang) */
export function LogoGoogle({ co = 18 }) {
  return (
    <svg width={co} height={co} viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
    </svg>
  )
}

/**
 * DANG NHAP / DANG KY NHANH (nhu cac app that): "Tiep tuc voi Google" va "Dung so dien thoai" (ma OTP 6 so qua SMS).
 * Tai khoan da co -> dang nhap ngay; chua co -> hoan tat dang ky (ten cong ty) roi dang nhap.
 * API: /api/auth/google, /api/auth/sdt/gui-ma, /api/auth/sdt/xac-nhan, /api/auth/ngoai/hoan-tat (DangNhapNgoaiController).
 */
export default function DangNhapNhanh({ quayVe, cheDo = 'dang-nhap' }) {
  const { nhanPhien } = useAuth()
  const dieuHuong = useNavigate()
  const [ch, setCh] = useState(null)
  const [moSdt, setMoSdt] = useState(false)
  const [buoc, setBuoc] = useState(1)
  const [so, setSo] = useState('')
  const [ma, setMa] = useState('')
  const [gui, setGui] = useState(null) // ket qua gui ma: { soDienThoai, cheDoDemo, maDemo, hetHanLuc, guiLaiLuc }
  const [ve, setVe] = useState(null)
  const [dangGui, setDangGui] = useState(false)
  const [loi, setLoi] = useState('')
  const [bayGio, setBayGio] = useState(() => Date.now())
  const oMa = useRef(null)

  useEffect(() => {
    let con = true
    goiApi('/api/auth/ngoai/cau-hinh', { coToken: false })
      .then((d) => con && setCh(d))
      .catch(() => con && setCh({ google: false, loi: true }))
    return () => {
      con = false
    }
  }, [])

  useEffect(() => {
    if (buoc !== 2) return undefined
    const hen = setInterval(() => setBayGio(Date.now()), 1000)
    return () => clearInterval(hen)
  }, [buoc])

  function xong(phien) {
    nhanPhien(phien)
    dieuHuong(noiVeSauDangNhap(phien.taiKhoan?.vaiTro, quayVe), { replace: true })
  }

  async function guiMa(e) {
    e?.preventDefault()
    setLoi('')
    setDangGui(true)
    try {
      const kq = await goiApi('/api/auth/sdt/gui-ma', { method: 'POST', coToken: false, body: { soDienThoai: so.trim() } })
      setGui({ ...kq, hetHanLuc: Date.now() + kq.hieuLucGiay * 1000, guiLaiLuc: Date.now() + kq.guiLaiSauGiay * 1000 })
      setMa('')
      setBuoc(2)
      setBayGio(Date.now())
      setTimeout(() => oMa.current?.focus(), 50)
    } catch (err) {
      setLoi(err.message)
    } finally {
      setDangGui(false)
    }
  }

  async function xacNhan(e) {
    e.preventDefault()
    setLoi('')
    setDangGui(true)
    try {
      const kq = await goiApi('/api/auth/sdt/xac-nhan', { method: 'POST', coToken: false, body: { soDienThoai: so.trim(), ma } })
      if (kq.canHoanTat) {
        setVe(kq)
        setBuoc(3)
      } else xong(kq.phien)
    } catch (err) {
      setLoi(err.message)
      setMa('')
      oMa.current?.focus()
    } finally {
      setDangGui(false)
    }
  }

  const conLai = gui ? Math.max(0, Math.round((gui.hetHanLuc - bayGio) / 1000)) : 0
  const choGuiLai = gui ? Math.max(0, Math.round((gui.guiLaiLuc - bayGio) / 1000)) : 0
  const urlGoogle = API_URL + '/api/auth/google' + taoQuery({ quayVe })

  return (
    <div className="kh-nhanh">
      {ch?.google ? (
        <a className="btn kh-nut-google w-100" href={urlGoogle}>
          <LogoGoogle />
          {cheDo === 'dang-ky' ? 'Đăng ký với Google' : 'Tiếp tục với Google'}
        </a>
      ) : (
        <button type="button" className="btn kh-nut-google w-100" disabled title="Quản trị viên chưa cấu hình Google:ClientId / ClientSecret">
          <LogoGoogle />
          {cheDo === 'dang-ky' ? 'Đăng ký với Google' : 'Tiếp tục với Google'}
        </button>
      )}
      {ch && !ch.google && (
        <div className="small text-body-secondary text-center mt-1">
          <i className="bi bi-info-circle me-1" />
          Máy chủ chưa bật đăng nhập Google (cần OAuth Client ID).
        </div>
      )}

      <button
        type="button"
        className={'btn kh-nut-sdt w-100 mt-2' + (moSdt ? ' active' : '')}
        onClick={() => {
          setMoSdt((v) => !v)
          setLoi('')
        }}
        aria-expanded={moSdt}
      >
        <i className="bi bi-phone" />
        {cheDo === 'dang-ky' ? 'Đăng ký bằng số điện thoại' : 'Dùng số điện thoại'}
      </button>

      {moSdt && (
        <div className="kh-khung-sdt mt-2">
          {loi && (
            <div className="alert alert-danger py-2 small" role="alert">
              {loi}
            </div>
          )}

          {buoc === 1 && (
            <form onSubmit={guiMa}>
              <label className="form-label small fw-semibold mb-1" htmlFor="sdt-so">
                Số điện thoại
              </label>
              <div className="input-group mb-2">
                <span className="input-group-text">🇻🇳 +84</span>
                <input
                  id="sdt-so"
                  className="form-control"
                  value={so}
                  onChange={(e) => setSo(e.target.value)}
                  inputMode="tel"
                  autoComplete="tel"
                  placeholder="0901 234 567"
                  maxLength={20}
                  required
                  autoFocus
                />
              </div>
              <button className="btn btn-primary w-100" disabled={dangGui || !so.trim()}>
                {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-chat-dots me-2" />}
                Gửi mã xác nhận
              </button>
              <div className="small text-body-secondary mt-2">Mã 6 số gửi qua SMS, hiệu lực 5 phút. Số mới sẽ được tạo tài khoản khách hàng.</div>
            </form>
          )}

          {buoc === 2 && gui && (
            <form onSubmit={xacNhan}>
              <div className="small mb-2">
                Đã gửi mã tới <b>{gui.soDienThoai}</b>{' '}
                <button
                  type="button"
                  className="btn btn-link btn-sm p-0 align-baseline"
                  onClick={() => {
                    setBuoc(1)
                    setLoi('')
                  }}
                >
                  Đổi số
                </button>
              </div>
              {gui.cheDoDemo && (
                <div className="alert alert-warning py-2 small">
                  <i className="bi bi-cone-striped me-1" />
                  <b>Chế độ demo</b> — máy chủ chưa gắn nhà mạng SMS nên tin nhắn được lưu ở <code>App_Data/OutboxSms</code>
                  {gui.maDemo && (
                    <>
                      . Mã thử: <b className="kh-ma-demo">{gui.maDemo}</b>
                    </>
                  )}
                </div>
              )}
              <div className="d-flex justify-content-between align-items-baseline">
                <label className="form-label small fw-semibold mb-1" htmlFor="sdt-ma">
                  Mã xác nhận
                </label>
                <span className={'small ' + (conLai === 0 ? 'text-danger fw-semibold' : 'text-body-secondary')}>
                  {conLai === 0 ? 'Mã đã hết hạn' : 'Hết hạn sau ' + dongHo(conLai)}
                </span>
              </div>
              <input
                ref={oMa}
                id="sdt-ma"
                className="form-control kh-o-ma mb-2"
                value={ma}
                onChange={(e) => setMa(e.target.value.replace(/\D/g, '').slice(0, 6))}
                inputMode="numeric"
                autoComplete="one-time-code"
                placeholder="••••••"
                maxLength={6}
              />
              <button className="btn btn-primary w-100" disabled={dangGui || ma.length !== 6 || conLai === 0}>
                {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-shield-check me-2" />}
                Xác nhận
              </button>
              <div className="text-center mt-2">
                <button type="button" className="btn btn-link btn-sm" disabled={choGuiLai > 0 || dangGui} onClick={guiMa}>
                  <i className="bi bi-arrow-repeat me-1" />
                  {choGuiLai > 0 ? 'Gửi lại mã sau ' + choGuiLai + ' giây' : 'Gửi lại mã'}
                </button>
              </div>
            </form>
          )}

          {buoc === 3 && ve && <HoanTatDangKy ve={ve.ve} kenh="sdt" soDienThoai={ve.soDienThoai} khiXong={xong} />}
        </div>
      )}
    </div>
  )
}
