import { Suspense, lazy } from 'react'
import { Link, Navigate, Route, Routes, useLocation, useParams } from 'react-router-dom'
import BoCuc from './components/BoCuc'
import BoCucIn from './components/BoCucIn'
import DangTai from './components/DangTai'
import YeuCauDangNhap from './components/YeuCauDangNhap'
import { NHAN_VIEN, TUYEN } from './dieuHuong/tuyen'
import BoCucCongKhai from './khu/cong-khai/BoCucCongKhai'

// Trang 404 cua website cong khai (giu dia chi cu /Home/KhongTimThay cua ban MVC)
const KhongTimThayCongKhai = lazy(() => import('./khu/cong-khai/KhongTimThay'))

/**
 * Dinh tuyen phia CLIENT (React Router): doi trang KHONG tai lai toan bo trang,
 * chi goi API lay du lieu moi — khac MVC la moi lan bam link server render lai HTML.
 * Danh sach man hinh khong viet tay o day ma doc tu so dang ky src/dieuHuong/tuyen.js.
 */
function ChuyenHuong({ den }) {
  // Giu nguyen ?tham so khi chuyen tu dia chi cu (vd /container?so=MSCU... -> /admin/containers/tracuu?so=...)
  const { search } = useLocation()
  return <Navigate to={den + search} replace />
}

function PhanTu({ tuyen }) {
  if (tuyen.chuyenDen) return <ChuyenHuong den={tuyen.chuyenDen} />
  const Trang = tuyen.trang
  const noiDung = <Trang {...tuyen.props} />
  // Khu admin da kiem tra dang nhap o route cha; o day chi chan vai tro hep hon (vd chi Admin)
  if (!tuyen.vaiTro) return noiDung
  return <YeuCauDangNhap vaiTro={tuyen.vaiTro}>{noiDung}</YeuCauDangNhap>
}

/** /Admin/Containers/Index (dia chi MVC cu) -> /admin/containers */
function BoIndex() {
  const { khu } = useParams()
  return <Navigate to={'/admin/' + khu} replace />
}

function KhongCoManHinh() {
  return (
    <div className="text-center py-5">
      <div className="display-6 text-muted mb-2">
        <i className="bi bi-signpost-2" />
      </div>
      <h2 className="h5">Không có màn hình ở địa chỉ này</h2>
      <p className="text-muted">Địa chỉ có thể đã đổi, hoặc chức năng này chưa mở cho tài khoản của bạn.</p>
      <Link className="btn btn-primary btn-sm" to="/admin">
        Về tổng quan
      </Link>
    </div>
  )
}

const theoBoCuc = (boCuc) => TUYEN.filter((t) => t.boCuc === boCuc)

export default function App() {
  const quanTri = theoBoCuc('quan-tri')
  return (
    <Suspense fallback={<DangTai chu="Đang mở màn hình…" />}>
      <Routes>
        {/* Trang dung rieng, khong khung (dang nhap, chuyen huong dia chi cu...) */}
        {theoBoCuc('trong').map((t) => (
          <Route key={t.duong} path={t.duong} element={<PhanTu tuyen={t} />} />
        ))}

        {/* Trang in (lenh giao nhan, phieu can, hoa don...): khong menu, co nut In */}
        <Route element={<BoCucIn />}>
          {theoBoCuc('in').map((t) => (
            <Route key={t.duong} path={t.duong} element={<PhanTu tuyen={t} />} />
          ))}
        </Route>

        {/* Khu dieu hanh: bat buoc dang nhap + vai tro Admin/NhanVien */}
        <Route path="/admin" element={<YeuCauDangNhap vaiTro={NHAN_VIEN} />}>
          <Route element={<BoCuc />}>
            {quanTri.map((t) =>
              t.duong === '/admin' ? (
                <Route key={t.duong} index element={<PhanTu tuyen={{ ...t, vaiTro: null }} />} />
              ) : (
                <Route key={t.duong} path={t.duong.slice('/admin/'.length)} element={<PhanTu tuyen={t.vaiTro === NHAN_VIEN ? { ...t, vaiTro: null } : t} />} />
              ),
            )}
            <Route path=":khu/index" element={<BoIndex />} />
            <Route path="*" element={<KhongCoManHinh />} />
          </Route>
        </Route>

        {/* Website cong khai + khu khach hang */}
        <Route element={<BoCucCongKhai />}>
          {theoBoCuc('cong-khai').map((t) => (
            <Route key={t.duong} path={t.duong} element={<PhanTu tuyen={t} />} />
          ))}
          <Route path="*" element={<KhongTimThayCongKhai />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
