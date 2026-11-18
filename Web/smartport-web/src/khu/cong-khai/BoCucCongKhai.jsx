import { Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { useAuth } from '../../auth/AuthContext'
import DangTai from '../../components/DangTai'
import NutGiaoDien from '../../components/khung/NutGiaoDien'
import { useThaoTac } from '../../hooks/useThaoTac'
import LogoSmartPort from './LogoSmartPort'
import PortBot from './PortBot'
import { useHienDan, useTienDoCuon } from './hieuUng'
import './cong-khai.css'

/** Menu chinh — giu nguyen dia chi cua ban MVC de link cu / ma QR da in van mo dung trang */
const MENU = [
  { duong: '/', ten: 'Trang chủ', dungHan: true },
  { duong: '/home/about', ten: 'Giới thiệu' },
  { duong: '/schedule', ten: 'Lịch tàu' },
  { duong: '/tracking', ten: 'Tra cứu container' },
  { duong: '/services', ten: 'Dịch vụ & Giá' },
  { duong: '/cost', ten: 'Ước tính phí', ai: true },
  { duong: '/news', ten: 'Tin tức' },
  { duong: '/home/contact', ten: 'Liên hệ' },
]

const TIEN_ICH = [
  { duong: '/cong', ten: 'Tình trạng cổng (trực tiếp)', icon: 'bi-broadcast' },
  { duong: '/taixe', ten: 'Cổng tài xế · check-in', icon: 'bi-person-badge' },
  { duong: '/mobile', ten: 'Ứng dụng di động', icon: 'bi-phone' },
]

const KHACH = [
  { duong: '/hangcuatoi', ten: 'Hàng & lệnh của tôi', icon: 'bi-boxes' },
  { duong: '/booking', ten: 'Đặt dịch vụ', icon: 'bi-plus-circle' },
  { duong: '/booking/my', ten: 'Booking của tôi', icon: 'bi-receipt' },
  { duong: '/payment/history', ten: 'Lịch sử thanh toán', icon: 'bi-clock-history' },
]

/**
 * Khung website cong khai (thay Views/Shared/_Layout.cshtml cua ban MVC):
 * dau trang + menu theo vai tro, o dang ky ban tin, chan trang, PortBot va nut Zalo.
 */
export default function BoCucCongKhai() {
  const { taiKhoan, dangXuat } = useAuth()
  const { pathname } = useLocation()
  const dieuHuong = useNavigate()
  const { duLieu: cang } = useApi('/api/cong-khai/thong-tin-lien-he', { coToken: false })

  const [menuDt, setMenuDt] = useState(false)
  const [nhomMo, setNhomMo] = useState('')
  const [chatMo, setChatMo] = useState(false)
  const tienDoRef = useRef(null)
  const dauRef = useRef(null)
  useTienDoCuon(tienDoRef)
  // Khung cung co khoi .ck-hien (o dang ky ban tin) -> bat hien dan tren MOI trang cong khai
  useHienDan([pathname])

  // Doi trang: dong menu dien thoai / menu thu xuong va cuon len dau
  useEffect(() => {
    setMenuDt(false)
    setNhomMo('')
    window.scrollTo(0, 0)
  }, [pathname])

  // Bam ra ngoai thi dong menu thu xuong
  useEffect(() => {
    if (!nhomMo) return undefined
    const dong = (e) => {
      if (!dauRef.current?.contains(e.target)) setNhomMo('')
    }
    document.addEventListener('mousedown', dong)
    return () => document.removeEventListener('mousedown', dong)
  }, [nhomMo])

  const laNoiBo = taiKhoan?.vaiTro === 'Admin' || taiKhoan?.vaiTro === 'NhanVien'
  const laKhach = taiKhoan?.vaiTro === 'KhachHang'
  const zalo = cang?.zaloPhone

  const thoat = () => {
    dangXuat()
    dieuHuong('/')
  }

  const doiNhom = (ten) => setNhomMo((cu) => (cu === ten ? '' : ten))

  return (
    <div className={'ck-web' + (chatMo ? ' ck-chat-mo' : '')}>
      <div className="ck-tien-do" ref={tienDoRef} />

      <header className="ck-dau" ref={dauRef}>
        <div className="container">
          <div className="d-flex align-items-center gap-3 py-2">
            <Link to="/" className="ck-hieu">
              <LogoSmartPort />
              <span>
                SmartPort
                <small>Cảng quốc tế · AI Logistics</small>
              </span>
            </Link>

            <nav className="ck-menu ms-auto ck-an-nho" aria-label="Menu website">
              {MENU.map((m) => (
                <NavLink key={m.duong} to={m.duong} end={m.dungHan} className={({ isActive }) => (isActive ? 'ck-dang' : '')}>
                  {m.ten}
                  {m.ai && <span className="ck-nhan-ai">AI</span>}
                </NavLink>
              ))}
              <div className="ck-nhom">
                <button type="button" className="ck-nut-nhom" onClick={() => doiNhom('tienIch')} aria-expanded={nhomMo === 'tienIch'}>
                  Tiện ích <i className="bi bi-chevron-down small" />
                </button>
                {nhomMo === 'tienIch' && (
                  <div className="ck-nhom-hop">
                    {TIEN_ICH.map((t) => (
                      <Link key={t.duong} to={t.duong}>
                        <i className={'bi ' + t.icon} />
                        {t.ten}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            </nav>

            <div className="d-flex align-items-center gap-2 ms-auto ms-xl-0">
              <NutGiaoDien />

              {!taiKhoan && (
                <>
                  <Link className="ck-nut-vao d-none d-sm-inline-block" to="/account/register">
                    <i className="bi bi-person-plus me-1" />
                    Đăng ký
                  </Link>
                  <Link className="ck-nut-chinh" to="/account/login">
                    <i className="bi bi-box-arrow-in-right me-1" />
                    Đăng nhập
                  </Link>
                </>
              )}

              {laNoiBo && (
                <Link className="ck-nut-chinh d-none d-sm-inline-block" to="/admin">
                  <i className="bi bi-speedometer2 me-1" />
                  Trang quản trị
                </Link>
              )}

              {taiKhoan && (
                <div className="ck-nhom">
                  <button type="button" className="ck-nut-vao" onClick={() => doiNhom('taiKhoan')} aria-expanded={nhomMo === 'taiKhoan'}>
                    <i className="bi bi-person-circle me-1" />
                    <span className="d-none d-md-inline">{taiKhoan.hoTen || taiKhoan.tenDangNhap}</span>
                    <i className="bi bi-chevron-down small ms-1" />
                  </button>
                  {nhomMo === 'taiKhoan' && (
                    <div className="ck-nhom-hop">
                      {laNoiBo && (
                        <>
                          <Link to="/admin">
                            <i className="bi bi-speedometer2" />
                            Trang quản trị
                          </Link>
                          <hr />
                        </>
                      )}
                      {laKhach &&
                        KHACH.map((k) => (
                          <Link key={k.duong} to={k.duong}>
                            <i className={'bi ' + k.icon} />
                            {k.ten}
                          </Link>
                        ))}
                      {laKhach && <hr />}
                      <Link to="/account/profile">
                        <i className="bi bi-person-gear" />
                        Hồ sơ cá nhân
                      </Link>
                      <button type="button" className="ck-nut-nhom text-danger w-100 justify-content-start" onClick={thoat}>
                        <i className="bi bi-box-arrow-right" />
                        Đăng xuất
                      </button>
                    </div>
                  )}
                </div>
              )}

              <button
                type="button"
                className="ck-nut-tron d-xl-none"
                onClick={() => setMenuDt((v) => !v)}
                aria-expanded={menuDt}
                aria-label="Mở menu website"
              >
                <i className={'bi ' + (menuDt ? 'bi-x-lg' : 'bi-list')} />
              </button>
            </div>
          </div>

          <div className={'ck-menu-dt d-xl-none' + (menuDt ? ' ck-mo' : '')}>
            {MENU.map((m) => (
              <Link key={m.duong} to={m.duong}>
                {m.ten}
                {m.ai && <span className="ck-nhan-ai">AI</span>}
              </Link>
            ))}
            <div className="ck-nhom-ten">Tiện ích</div>
            {TIEN_ICH.map((t) => (
              <Link key={t.duong} to={t.duong}>
                <i className={'bi ' + t.icon} />
                {t.ten}
              </Link>
            ))}
            {laKhach && (
              <>
                <div className="ck-nhom-ten">Tài khoản doanh nghiệp</div>
                {KHACH.map((k) => (
                  <Link key={k.duong} to={k.duong}>
                    <i className={'bi ' + k.icon} />
                    {k.ten}
                  </Link>
                ))}
              </>
            )}
            {laNoiBo && (
              <>
                <div className="ck-nhom-ten">Nội bộ</div>
                <Link to="/admin">
                  <i className="bi bi-speedometer2" />
                  Trang quản trị
                </Link>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="ck-than">
        <Suspense fallback={<DangTai chu="Đang mở trang…" />}>
          <Outlet />
        </Suspense>
        <OBanTin />
      </main>

      <ChanTrang cang={cang} />

      <PortBot khiDoiTrangThai={setChatMo} />
      {zalo && (
        <a className="ck-zalo" href={'https://zalo.me/' + zalo} target="_blank" rel="noopener noreferrer" title={'Chat Zalo ' + zalo}>
          Zalo
        </a>
      )}
    </div>
  )
}

/** O dang ky nhan ban tin (thay form Subscribe cua _Layout.cshtml) */
function OBanTin() {
  const [email, setEmail] = useState('')
  const { chay, dangChay } = useThaoTac()

  const gui = async (e) => {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/cong-khai/dang-ky-ban-tin', { method: 'POST', body: { email }, coToken: false }))
    if (kq) setEmail('')
  }

  return (
    <div className="container">
      <div className="ck-ban-tin ck-hien mt-4">
        <div className="row align-items-center g-3">
          <div className="col-lg-6">
            <h2 className="h5 fw-bold mb-1">
              <i className="bi bi-envelope-heart me-2" />
              Nhận bản tin lịch tàu &amp; ưu đãi
            </h2>
            <p className="mb-0 small">Lịch tàu tuần, thay đổi biểu phí và ưu đãi dịch vụ — gửi mỗi sáng thứ 2.</p>
          </div>
          <div className="col-lg-6">
            <form className="d-flex gap-2" onSubmit={gui}>
              <input
                type="email"
                className="form-control"
                placeholder="Email doanh nghiệp của bạn..."
                value={email}
                maxLength={100}
                required
                onChange={(e) => setEmail(e.target.value)}
                aria-label="Email nhận bản tin"
              />
              <button className="ck-nut-chinh px-4 flex-shrink-0" type="submit" disabled={dangChay}>
                {dangChay ? 'Đang gửi…' : 'Đăng ký'}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}

function ChanTrang({ cang }) {
  const nam = cang?.nam ?? new Date().getFullYear()
  const zalo = cang?.zaloPhone
  const lienKet = useMemo(
    () => [
      { duong: '/home/about', ten: 'Giới thiệu cảng' },
      { duong: '/schedule', ten: 'Lịch tàu' },
      { duong: '/news', ten: 'Tin tức' },
      { duong: '/home/contact', ten: 'Liên hệ' },
    ],
    [],
  )
  const dichVu = useMemo(
    () => [
      { duong: '/services', ten: 'Bốc xếp · lưu bãi · kho CFS' },
      { duong: '/cost', ten: 'Ước tính chi phí AI' },
      { duong: '/taixe', ten: 'Cổng tài xế · check-in' },
      { duong: '/cong', ten: 'Tình trạng cổng (trực tiếp)' },
      { duong: '/mobile', ten: 'Ứng dụng di động' },
      { duong: '/tracking', ten: 'Tra cứu container' },
    ],
    [],
  )

  return (
    <footer className="ck-chan">
      <div className="container">
        <div className="row g-4">
          <div className="col-lg-4">
            <div className="d-flex align-items-center gap-2 mb-3">
              <LogoSmartPort canh={34} />
              <span className="fs-5 fw-bold text-white">SmartPort</span>
            </div>
            <p className="small">
              Cảng container quốc tế ứng dụng AI trong quản lý khai thác — kết nối hàng hóa Việt Nam với hơn 50 tuyến dịch vụ toàn cầu.
            </p>
            <div className="d-flex gap-3 fs-4 mb-3">
              {cang?.facebookNhom && (
                <a href={cang.facebookNhom} target="_blank" rel="noopener noreferrer" title="Cộng đồng Logistics & XNK" aria-label="Facebook">
                  <i className="bi bi-facebook" />
                </a>
              )}
              {cang?.youtube && (
                <a href={cang.youtube} target="_blank" rel="noopener noreferrer" title="Vietnam Logistics Review" aria-label="YouTube">
                  <i className="bi bi-youtube" />
                </a>
              )}
              <Link to="/home/contact" title="Liên hệ" aria-label="Liên hệ">
                <i className="bi bi-envelope" />
              </Link>
            </div>
            <div className="small d-flex flex-wrap gap-3">
              {cang?.facebookCaNhan && (
                <a href={cang.facebookCaNhan} target="_blank" rel="noopener noreferrer">
                  <i className="bi bi-facebook me-1" />
                  {cang.nguoiQuanLy}
                </a>
              )}
              {zalo && (
                <a href={'https://zalo.me/' + zalo} target="_blank" rel="noopener noreferrer">
                  <span className="fw-bold" style={{ color: '#4da3ff' }}>
                    Z
                  </span>{' '}
                  Zalo {zalo}
                </a>
              )}
            </div>
          </div>

          <div className="col-lg-2 col-6">
            <h4>Liên kết</h4>
            <ul>
              {lienKet.map((l) => (
                <li key={l.duong}>
                  <Link to={l.duong}>{l.ten}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="col-lg-3 col-6">
            <h4>Dịch vụ</h4>
            <ul>
              {dichVu.map((d) => (
                <li key={d.duong}>
                  <Link to={d.duong}>{d.ten}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="col-lg-3">
            <h4>Liên hệ</h4>
            <ul>
              <li className="d-flex gap-2">
                <span className="ck-ico-tron">
                  <i className="bi bi-geo-alt" />
                </span>
                <span>{cang?.diaChi ?? 'Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM'}</span>
              </li>
              <li className="d-flex gap-2">
                <span className="ck-ico-tron">
                  <i className="bi bi-telephone-fill" />
                </span>
                <span>
                  Hotline:{' '}
                  <a className="fw-bold text-white" href={'tel:' + (cang?.hotline ?? '')}>
                    {cang?.hotline}
                  </a>
                  <br />
                  <small style={{ color: '#7f9cbd' }}>Quản lý: {cang?.nguoiQuanLy}</small>
                </span>
              </li>
              <li className="d-flex gap-2">
                <span className="ck-ico-tron">
                  <i className="bi bi-envelope" />
                </span>
                <span style={{ wordBreak: 'break-all' }}>
                  <a href={'mailto:' + (cang?.email ?? '')}>{cang?.email}</a>
                </span>
              </li>
            </ul>
          </div>
        </div>

        <div className="ck-chan-duoi d-flex flex-wrap justify-content-between gap-2">
          <span>© {nam} Cảng Quốc tế SmartPort.</span>
          <span style={{ opacity: 0.65 }}>Hình ảnh: Unsplash (giấy phép miễn phí)</span>
          <span>
            {cang?.trinhDien ? 'Đồ án môn Lập trình Web — Lê Hữu Anh Tài (2424802010066)' : 'SmartPort — Nền tảng AI quản lý cảng & depot · v1.0'}
          </span>
        </div>
      </div>
    </footer>
  )
}
