import { Link, Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

/**
 * Bao ve route: chua dang nhap -> ve trang dang nhap (nho dia chi de quay lai);
 * sai vai tro -> bao khong co quyen.
 * Dung lam route cha (render <Outlet/>) hoac boc truc tiep: <YeuCauDangNhap vaiTro={['Admin']}>...</YeuCauDangNhap>
 */
export default function YeuCauDangNhap({ vaiTro, children }) {
  const { taiKhoan, dangXuat } = useAuth()
  const viTri = useLocation()

  if (!taiKhoan) return <Navigate to="/account/login" replace state={{ quayVe: viTri.pathname + viTri.search }} />

  if (vaiTro && !vaiTro.includes(taiKhoan.vaiTro)) {
    const laNhanVien = taiKhoan.vaiTro === 'Admin' || taiKhoan.vaiTro === 'NhanVien'
    return (
      <div className="container py-5" style={{ maxWidth: 560 }}>
        <div className="alert alert-warning">
          <h2 className="h5 alert-heading">
            <i className="bi bi-shield-lock me-2" />
            Tài khoản không có quyền mở chức năng này
          </h2>
          <p className="mb-3">
            Tài khoản <b>{taiKhoan.tenDangNhap}</b> có vai trò <b>{taiKhoan.vaiTro}</b>.
            {vaiTro.length === 1 && vaiTro[0] === 'Admin' ? ' Chức năng này chỉ dành cho quản trị viên.' : ''}
          </p>
          <Link className="btn btn-sm btn-primary me-2" to={laNhanVien ? '/admin' : '/'}>
            {laNhanVien ? 'Về tổng quan' : 'Về trang chủ'}
          </Link>
          <button className="btn btn-sm btn-outline-secondary" onClick={dangXuat}>
            Đăng xuất
          </button>
        </div>
      </div>
    )
  }
  return children ?? <Outlet />
}
