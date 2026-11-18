import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { goiApi, LoiApi } from '../../api/client'
import DoManhMatKhau from './DoManhMatKhau'
import { danhGiaMatKhau } from './matKhau'
import './khach-hang.css'

// sessionStorage: tai lai trang o buoc 2 van giu dung buoc va dong ho — KHONG luu ma xac nhan / mat khau
const KHOA_LUU = 'sp-quen-mat-khau'
const BUOC = ['Tài khoản', 'Mã & mật khẩu mới', 'Hoàn tất']

function docTrangThai() {
  try {
    const tt = JSON.parse(sessionStorage.getItem(KHOA_LUU))
    // Chi khoi phuc khi con trong thoi han ma (qua han thi bat dau lai tu buoc 1)
    return tt && tt.buoc === 2 && tt.hetHanLuc > Date.now() ? tt : null
  } catch {
    return null
  }
}

function luuTrangThai(tt) {
  try {
    if (tt) sessionStorage.setItem(KHOA_LUU, JSON.stringify(tt))
    else sessionStorage.removeItem(KHOA_LUU)
  } catch {
    /* trinh duyet chan sessionStorage: van dung duoc, chi mat buoc khi tai lai trang */
  }
}

const hai = (n) => String(n).padStart(2, '0')
const dongHo = (giay) => `${hai(Math.floor(giay / 60))}:${hai(giay % 60)}`

/**
 * QUEN MAT KHAU (moi vai tro): 1) nhap ten dang nhap / email -> nhan ma 6 so qua email
 * 2) nhap ma + mat khau moi -> 3) xong, quay lai dang nhap (dien san ten dang nhap).
 * API: POST /api/auth/quen-mat-khau, POST /api/auth/dat-lai-mat-khau (QuenMatKhauController).
 */
export default function QuenMatKhau() {
  const viTri = useLocation()
  const dieuHuong = useNavigate()
  const [daLuu] = useState(docTrangThai)

  const [buoc, setBuoc] = useState(daLuu ? 2 : 1)
  const [dinhDanh, setDinhDanh] = useState(daLuu?.dinhDanh || viTri.state?.ten || '')
  const [hetHanLuc, setHetHanLuc] = useState(daLuu?.hetHanLuc || 0)
  const [guiLaiLuc, setGuiLaiLuc] = useState(daLuu?.guiLaiLuc || 0)
  const [thongBao, setThongBao] = useState(daLuu?.thongBao || '')
  const [ma, setMa] = useState('')
  const [matKhau, setMatKhau] = useState('')
  const [xacNhan, setXacNhan] = useState('')
  const [hienMk, setHienMk] = useState(false)
  const [dangGui, setDangGui] = useState(false)
  const [loi, setLoi] = useState('')
  const [loiMk, setLoiMk] = useState([])
  const [tenXong, setTenXong] = useState('')
  const [bayGio, setBayGio] = useState(() => Date.now())
  const oMa = useRef(null)
  const laEmail = dinhDanh.includes('@')

  // Dong ho dem nguoc (ma het han, duoc gui lai) — chi chay o buoc 2
  useEffect(() => {
    if (buoc !== 2) return undefined
    const hen = setInterval(() => setBayGio(Date.now()), 1000)
    return () => clearInterval(hen)
  }, [buoc])

  useEffect(() => {
    luuTrangThai(buoc === 2 ? { buoc, dinhDanh, hetHanLuc, guiLaiLuc, thongBao } : null)
  }, [buoc, dinhDanh, hetHanLuc, guiLaiLuc, thongBao])

  async function guiMa(e) {
    e?.preventDefault()
    setLoi('')
    setLoiMk([])
    const s = dinhDanh.trim()
    if (!s) {
      setLoi('Vui lòng nhập tên đăng nhập hoặc email.')
      return
    }
    setDangGui(true)
    try {
      const kq = await goiApi('/api/auth/quen-mat-khau', { method: 'POST', body: { tenHoacEmail: s }, coToken: false })
      const t = Date.now()
      setThongBao(kq.thongBao)
      setHetHanLuc(t + kq.hieuLucPhut * 60000)
      setGuiLaiLuc(t + kq.guiLaiSauGiay * 1000)
      setBayGio(t)
      setMa('')
      setBuoc(2)
      setTimeout(() => oMa.current?.focus(), 50)
    } catch (err) {
      setLoi(err.message)
    } finally {
      setDangGui(false)
    }
  }

  async function datLai(e) {
    e.preventDefault()
    setLoi('')
    setLoiMk([])
    if (!/^\d{6}$/.test(ma)) {
      setLoi('Mã xác nhận gồm đúng 6 chữ số.')
      return
    }
    if (!danhGiaMatKhau(matKhau, laEmail ? '' : dinhDanh).hopLe) {
      setLoi('Mật khẩu mới chưa đạt các điều kiện bên dưới.')
      return
    }
    if (matKhau !== xacNhan) {
      setLoi('Mật khẩu nhập lại không khớp.')
      return
    }
    setDangGui(true)
    try {
      const kq = await goiApi('/api/auth/dat-lai-mat-khau', {
        method: 'POST',
        body: { tenHoacEmail: dinhDanh.trim(), maXacNhan: ma, matKhauMoi: matKhau, xacNhanMatKhau: xacNhan },
        coToken: false,
      })
      setTenXong(kq.duLieu?.tenDangNhap || '')
      setMa('')
      setMatKhau('')
      setXacNhan('')
      setBuoc(3)
    } catch (err) {
      setLoi(err.message)
      if (err instanceof LoiApi && Array.isArray(err.chiTiet?.loiMatKhau)) setLoiMk(err.chiTiet.loiMatKhau)
    } finally {
      setDangGui(false)
    }
  }

  function doiTaiKhoan() {
    setBuoc(1)
    setLoi('')
    setLoiMk([])
    setMa('')
  }

  function sangDangNhap() {
    dieuHuong('/account/login', {
      replace: true,
      state: { ten: tenXong, thongBao: 'Đặt lại mật khẩu thành công — hãy đăng nhập bằng mật khẩu mới.' },
    })
  }

  const conLai = Math.max(0, Math.round((hetHanLuc - bayGio) / 1000))
  const choGuiLai = Math.max(0, Math.round((guiLaiLuc - bayGio) / 1000))
  const hetHan = buoc === 2 && conLai === 0

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
            Lấy lại tài khoản
            <br />
            <span>an toàn trong 2 bước</span>
          </h2>
          <ul className="kh-an-toan">
            <li>
              <i className="bi bi-envelope-check" />
              <span>Mã 6 số gửi tới email đã đăng ký, hiệu lực 15 phút</span>
            </li>
            <li>
              <i className="bi bi-shield-lock" />
              <span>Mã chỉ dùng một lần — nhập sai 5 lần tự hủy</span>
            </li>
            <li>
              <i className="bi bi-phone" />
              <span>Đổi xong, mọi thiết bị đang đăng nhập bị đăng xuất</span>
            </li>
          </ul>
        </section>

        <section className="kh-bieu-mau">
          <span className="kh-nhan-an-toan">
            <i className="bi bi-shield-lock-fill" />
            Khôi phục mật khẩu
          </span>
          <h1 className="h4 fw-bold mt-2 mb-1">Quên mật khẩu</h1>

          <ol className="kh-buoc" aria-label="Các bước khôi phục mật khẩu">
            {BUOC.map((t, i) => (
              <li key={t} className={i + 1 < buoc ? 'xong' : i + 1 === buoc ? 'dang' : ''} aria-current={i + 1 === buoc ? 'step' : undefined}>
                <span>{i + 1 < buoc ? <i className="bi bi-check-lg" /> : i + 1}</span>
                {t}
              </li>
            ))}
          </ol>

          {loi && (
            <div className="alert alert-danger py-2 small" role="alert">
              {loi}
              {loiMk.length > 1 && (
                <ul className="mb-0 mt-1 ps-3">
                  {loiMk.slice(1).map((l) => (
                    <li key={l}>{l}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {buoc === 1 && (
            <form onSubmit={guiMa} noValidate>
              <p className="text-body-secondary small mb-3">
                Nhập tên đăng nhập hoặc email của tài khoản. SmartPort sẽ gửi <b>mã xác nhận 6 số</b> tới email đã đăng ký.
              </p>
              <label className="form-label small fw-semibold mb-1" htmlFor="qmk-ten">
                Tên đăng nhập hoặc email
              </label>
              <div className="kh-o-nhap mb-3">
                <i className="bi bi-person kh-icon" />
                <input
                  id="qmk-ten"
                  className="form-control"
                  value={dinhDanh}
                  onChange={(e) => setDinhDanh(e.target.value)}
                  autoComplete="username"
                  maxLength={100}
                  autoFocus
                  required
                />
              </div>
              <button className="btn btn-primary w-100 py-2" disabled={dangGui}>
                {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-send me-2" />}
                Gửi mã xác nhận
              </button>
              <div className="small text-body-secondary mt-3">
                <i className="bi bi-info-circle me-1" />
                Tài khoản chưa có email? Liên hệ quản trị viên SmartPort để được cấp lại mật khẩu.
              </div>
            </form>
          )}

          {buoc === 2 && (
            <form onSubmit={datLai} noValidate>
              <div className="alert alert-info py-2 small">
                <i className="bi bi-envelope-paper me-1" />
                {thongBao}
              </div>

              {/* Cho trinh quan ly mat khau biet mat khau moi thuoc tai khoan nao */}
              <input type="text" className="d-none" name="username" autoComplete="username" value={dinhDanh} readOnly tabIndex={-1} />

              <div className="d-flex justify-content-between align-items-baseline mb-1">
                <label className="form-label small fw-semibold mb-0" htmlFor="qmk-ma">
                  Mã xác nhận
                </label>
                <span className={'small ' + (hetHan ? 'text-danger fw-semibold' : 'text-body-secondary')}>
                  {hetHan ? 'Mã đã hết hạn' : 'Hết hạn sau ' + dongHo(conLai)}
                </span>
              </div>
              <input
                ref={oMa}
                id="qmk-ma"
                className="form-control kh-o-ma mb-1"
                value={ma}
                onChange={(e) => setMa(e.target.value.replace(/\D/g, '').slice(0, 6))}
                inputMode="numeric"
                autoComplete="one-time-code"
                placeholder="••••••"
                maxLength={6}
                aria-describedby="qmk-ma-goi-y"
              />
              <div id="qmk-ma-goi-y" className="small text-body-secondary mb-3">
                Mã gửi cho <b>{dinhDanh.trim()}</b> · mỗi mã nhập sai tối đa 5 lần.
              </div>

              <label className="form-label small fw-semibold mb-1" htmlFor="qmk-mk">
                Mật khẩu mới
              </label>
              <div className="kh-o-nhap">
                <i className="bi bi-lock kh-icon" />
                <input
                  id="qmk-mk"
                  type={hienMk ? 'text' : 'password'}
                  className="form-control"
                  value={matKhau}
                  onChange={(e) => setMatKhau(e.target.value)}
                  autoComplete="new-password"
                  maxLength={50}
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
              <DoManhMatKhau matKhau={matKhau} ten={laEmail ? '' : dinhDanh} />

              <label className="form-label small fw-semibold mb-1 mt-3" htmlFor="qmk-xn">
                Nhập lại mật khẩu mới
              </label>
              <div className="kh-o-nhap mb-1">
                <i className="bi bi-lock-fill kh-icon" />
                <input
                  id="qmk-xn"
                  type={hienMk ? 'text' : 'password'}
                  className="form-control"
                  value={xacNhan}
                  onChange={(e) => setXacNhan(e.target.value)}
                  autoComplete="new-password"
                  maxLength={50}
                />
              </div>
              <div className="small mb-3" style={{ minHeight: '1.3em' }}>
                {xacNhan &&
                  (xacNhan === matKhau ? (
                    <span className="kh-chu-la">
                      <i className="bi bi-check-circle-fill me-1" />
                      Khớp
                    </span>
                  ) : (
                    <span className="kh-chu-do">
                      <i className="bi bi-x-circle-fill me-1" />
                      Chưa khớp
                    </span>
                  ))}
              </div>

              <button className="btn btn-primary w-100 py-2" disabled={dangGui || hetHan}>
                {dangGui ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-key me-2" />}
                Đặt lại mật khẩu
              </button>

              <div className="d-flex justify-content-between small mt-3">
                <button type="button" className="btn btn-link btn-sm p-0" onClick={() => guiMa()} disabled={dangGui || choGuiLai > 0}>
                  <i className="bi bi-arrow-repeat me-1" />
                  {choGuiLai > 0 ? `Gửi lại mã sau ${choGuiLai} giây` : 'Gửi lại mã'}
                </button>
                <button type="button" className="btn btn-link btn-sm p-0" onClick={doiTaiKhoan}>
                  Nhập tài khoản khác
                </button>
              </div>
            </form>
          )}

          {buoc === 3 && (
            <div className="text-center py-2">
              <div className="kh-xong" aria-hidden="true">
                <i className="bi bi-check-lg" />
              </div>
              <h2 className="h5 fw-bold mt-3">Đã đặt lại mật khẩu</h2>
              <p className="small text-body-secondary">
                Mọi thiết bị đang đăng nhập tài khoản{tenXong ? <b> {tenXong}</b> : null} đã bị đăng xuất. SmartPort cũng gửi email
                thông báo tới bạn. Hãy đăng nhập lại bằng mật khẩu mới.
              </p>
              <button type="button" className="btn btn-primary w-100 py-2" onClick={sangDangNhap}>
                <i className="bi bi-box-arrow-in-right me-2" />
                Đăng nhập ngay
              </button>
            </div>
          )}

          {buoc !== 3 && (
            <>
              <div className="text-center small mt-4">
                <Link to="/account/login">
                  <i className="bi bi-arrow-left me-1" />
                  Quay lại đăng nhập
                </Link>
              </div>
              <div className="text-center small mt-2 text-body-secondary">
                Đã cài khuôn mặt? Có thể <Link to="/account/login">đăng nhập bằng khuôn mặt</Link> rồi đổi mật khẩu trong <b>Hồ sơ</b>.
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  )
}
