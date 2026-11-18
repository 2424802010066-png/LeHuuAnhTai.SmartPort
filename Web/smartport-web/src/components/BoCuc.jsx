import { Suspense, useEffect, useMemo, useState } from 'react'
import { Link, Outlet, matchPath, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { NHOM_MENU } from '../dieuHuong/menu'
import { DUONG_CO_MAN_HINH, TUYEN } from '../dieuHuong/tuyen'
import { SuKienProvider, useSuKien } from '../realtime/SuKienContext'
import DangTai from './DangTai'
import BaoDongToanCuc from './khung/BaoDongToanCuc'
import ChuongThongBao from './khung/ChuongThongBao'
import NutGiaoDien from './khung/NutGiaoDien'
import TimToanCuc from './khung/TimToanCuc'
import TroLyNoi from './TroLy/TroLyNoi'
import ThanhTrangThaiMang from './ThanhTrangThaiMang'

const TRANG_THAI_KET_NOI = {
  'da-noi': { lop: 'text-success', chu: 'Trực tiếp', goiY: 'Đang nhận sự kiện realtime từ máy chủ' },
  'dang-noi': { lop: 'text-secondary', chu: 'Đang nối…', goiY: 'Đang mở luồng sự kiện' },
  'mat-ket-noi': { lop: 'text-danger', chu: 'Mất kết nối', goiY: 'Tự thử nối lại sau vài giây' },
}

const KHOA_NHOM_MO = 'sp-menu-nhom-mo'
const TUYEN_QUAN_TRI = TUYEN.filter((t) => t.boCuc === 'quan-tri' && t.trang)

function DenTrucTiep() {
  const { trangThai } = useSuKien()
  const tt = TRANG_THAI_KET_NOI[trangThai]
  return (
    <span className={'sp-den-truc-tiep me-3 small ' + tt.lop} title={tt.goiY}>
      <i className="bi bi-circle-fill" />
      <span className="d-none d-xl-inline">{tt.chu}</span>
    </span>
  )
}

const khongDau = (s) =>
  (s || '')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()

/** Muc menu dang mo = muc co dia chi dai nhat la tien to cua dia chi hien tai (/admin/containers/hoso/5 -> Container) */
function timMucDangMo(nhom, duongHienTai) {
  const p = duongHienTai.toLowerCase().replace(/\/+$/, '') || '/'
  let tot = null
  for (const n of nhom)
    for (const m of n.muc)
      if ((p === m.den || p.startsWith(m.den + '/')) && (!tot || m.den.length > tot.muc.den.length)) tot = { nhom: n.ma, muc: m }
  return tot
}

function docNhomMo() {
  try {
    return JSON.parse(localStorage.getItem(KHOA_NHOM_MO)) || {}
  } catch {
    return {}
  }
}

export default function BoCuc() {
  const { phien, taiKhoan, dangXuat } = useAuth()
  const [moMenuDiDong, setMoMenuDiDong] = useState(false)
  const [locMenu, setLocMenu] = useState('')
  const [nhomMo, setNhomMo] = useState(docNhomMo)
  const [anCanhBaoMk, setAnCanhBaoMk] = useState(false)
  const viTri = useLocation()

  // Chi hien muc da co man hinh + dung vai tro
  const nhomHien = useMemo(
    () =>
      NHOM_MENU.map((n) => ({
        ...n,
        muc: n.muc.filter((m) => DUONG_CO_MAN_HINH.has(m.den) && (!m.vaiTro || m.vaiTro.includes(taiKhoan.vaiTro))),
      })).filter((n) => n.muc.length),
    [taiKhoan.vaiTro],
  )

  const dangMo = timMucDangMo(nhomHien, viTri.pathname)
  const tuyenHienTai = TUYEN_QUAN_TRI.find((t) => matchPath({ path: t.duong, end: true }, viTri.pathname.toLowerCase()))
  const tieuDe = tuyenHienTai?.ten || dangMo?.muc.ten || 'SmartPort'

  useEffect(() => {
    document.title = tieuDe + ' · SmartPort'
  }, [tieuDe])

  // Doi man hinh: dong menu tren dien thoai
  useEffect(() => {
    setMoMenuDiDong(false)
  }, [viTri.pathname])

  const doiNhom = (ma, mo) => {
    setNhomMo((cu) => {
      const moi = { ...cu, [ma]: mo }
      try {
        localStorage.setItem(KHOA_NHOM_MO, JSON.stringify(moi))
      } catch {
        /* bo qua */
      }
      return moi
    })
  }

  const tuLoc = khongDau(locMenu.trim())
  const nhomTheoLoc = tuLoc
    ? nhomHien.map((n) => ({ ...n, muc: n.muc.filter((m) => khongDau((m.so || '') + ' ' + m.ten + ' ' + (m.nhan || '')).includes(tuLoc)) })).filter((n) => n.muc.length)
    : nhomHien

  return (
    <SuKienProvider>
      <div className="sp-khung">
        <aside className={'sp-menu' + (moMenuDiDong ? ' mo' : '')}>
          <Link to="/admin" className="sp-logo text-decoration-none">
            <i className="bi bi-box-seam-fill" />
            SmartPort
            <span>điều hành</span>
          </Link>
          <div className="px-3 pb-2">
            <input
              type="search"
              className="form-control form-control-sm sp-loc-menu"
              placeholder="Lọc chức năng…"
              aria-label="Lọc menu chức năng"
              value={locMenu}
              onChange={(e) => setLocMenu(e.target.value)}
            />
          </div>
          <nav className="sp-menu-cuon" aria-label="Chức năng">
            {nhomTheoLoc.map((n) => {
              const moNhom = !n.ten || !!tuLoc || (nhomMo[n.ma] ?? true)
              return (
                <div key={n.ma} className="sp-nhom-menu">
                  {n.ten && (
                    <button type="button" className="sp-nhom-menu-dau" aria-expanded={moNhom} onClick={() => doiNhom(n.ma, !moNhom)}>
                      <i className={'bi ' + n.icon} />
                      <span className="flex-grow-1 text-start">{n.ten}</span>
                      <i className={'bi sp-mui-ten ' + (moNhom ? 'bi-chevron-up' : 'bi-chevron-down')} />
                    </button>
                  )}
                  {moNhom &&
                    n.muc.map((m) => (
                      <Link key={m.den} to={m.den} className={'nav-link' + (dangMo?.muc === m ? ' active' : '')} aria-current={dangMo?.muc === m ? 'page' : undefined}>
                        <i className={'bi ' + m.icon} />
                        <span className="flex-grow-1 text-truncate">
                          {m.so && <span className="sp-so-menu">{m.so}.</span>}
                          {m.ten}
                        </span>
                        {m.nhan && <span className="sp-nhan-menu">{m.nhan}</span>}
                      </Link>
                    ))}
                </div>
              )
            })}
            {!nhomTheoLoc.length && <div className="px-3 small text-secondary">Không có chức năng “{locMenu}”.</div>}
          </nav>
          <div className="sp-menu-duoi">
            <Link className="nav-link" to="/" target="_blank">
              <i className="bi bi-box-arrow-up-right" />
              Xem website
            </Link>
            <div className="sp-kien-truc">React · ASP.NET Core Web API · JWT · SSE</div>
          </div>
        </aside>
        {moMenuDiDong && <div className="sp-man-che" onClick={() => setMoMenuDiDong(false)} />}

        <div className="sp-noi-dung">
          <header className="sp-dau">
            <button className="btn btn-sm btn-light d-lg-none me-2" onClick={() => setMoMenuDiDong(true)} aria-label="Mở menu">
              <i className="bi bi-list" />
            </button>
            <h1 className="h5 mb-0 me-3 text-truncate sp-tieu-de-trang">
              {dangMo?.muc.so && matchPath({ path: dangMo.muc.den, end: true }, viTri.pathname.toLowerCase()) && <span className="sp-so-menu">{dangMo.muc.so}.</span>}
              {tieuDe}
            </h1>
            <div className="flex-grow-1 d-none d-md-block">
              <TimToanCuc />
            </div>
            <div className="flex-grow-1 d-md-none" />
            {/* Mang toi may chu + luot xe cho gui cua tram (nhip /suc-khoe, khong dung navigator.onLine) */}
            <ThanhTrangThaiMang />
            <DenTrucTiep />
            <ChuongThongBao />
            <NutGiaoDien />
            <div className="text-end me-2 lh-sm d-none d-sm-block">
              <div className="fw-semibold small">{taiKhoan.hoTen}</div>
              <div className="text-body-secondary" style={{ fontSize: '.75rem' }}>
                {taiKhoan.vaiTro === 'Admin' ? 'Quản trị viên' : 'Nhân viên khai thác'}
              </div>
            </div>
            <button className="btn btn-sm btn-outline-secondary" onClick={dangXuat} title="Đăng xuất">
              <i className="bi bi-box-arrow-right" />
            </button>
          </header>
          <main className="sp-trang">
            {phien?.matKhauMacDinh && !anCanhBaoMk && (
              <div className="alert alert-warning d-flex align-items-center gap-2 py-2">
                <i className="bi bi-shield-exclamation fs-5" />
                <div className="small flex-grow-1">
                  <b>Bạn đang dùng mật khẩu mặc định.</b> Hệ thống vận hành thật yêu cầu mật khẩu riêng —{' '}
                  <Link className="fw-bold" to="/account/profile">
                    đổi mật khẩu ngay
                  </Link>
                  .
                </div>
                <button type="button" className="btn-close" aria-label="Ẩn" onClick={() => setAnCanhBaoMk(true)} />
              </div>
            )}
            {/* Suspense dat TRONG bo cuc: doi man hinh chi hien vong xoay o vung noi dung, menu giu nguyen */}
            <Suspense fallback={<DangTai chu="Đang mở màn hình…" />}>
              <Outlet />
              <BaoDongToanCuc />
            </Suspense>
          </main>
        </div>
        <TroLyNoi />
      </div>
    </SuKienProvider>
  )
}
