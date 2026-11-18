import { lazy } from 'react'

// Moi vai tro deu duoc xem ho so / cai dat khuon mat cua CHINH MINH
// (dat vaiTro de App.jsx boc YeuCauDangNhap: chua dang nhap thi ve trang dang nhap va nho duong quay lai)
const DA_DANG_NHAP = ['Admin', 'NhanVien', 'KhachHang']

// Khu khach hang — tai khoan ca nhan: dang nhap (mat khau + khuon mat), dang ky, quen mat khau,
// ho so, doi mat khau, cai dat khuon mat. Dia chi giu nguyen nhu ban MVC (/Account/...).
export default [
  { duong: '/account/login', trang: lazy(() => import('./DangNhap')), ten: 'Đăng nhập', boCuc: 'trong' },
  { duong: '/account/logout', trang: lazy(() => import('./DangXuat')), ten: 'Đăng xuất', boCuc: 'trong' },
  { duong: '/account/register', trang: lazy(() => import('./DangKy')), ten: 'Đăng ký khách hàng', boCuc: 'trong' },
  { duong: '/account/quen-mat-khau', trang: lazy(() => import('./QuenMatKhau')), ten: 'Quên mật khẩu', boCuc: 'trong' },
  { duong: '/account/dang-nhap-ngoai', trang: lazy(() => import('./DangNhapNgoai')), ten: 'Đăng nhập bằng Google', boCuc: 'trong' },
  { duong: '/account/profile', trang: lazy(() => import('./HoSo')), ten: 'Hồ sơ cá nhân', vaiTro: DA_DANG_NHAP },
  { duong: '/account/facesetup', trang: lazy(() => import('./CaiDatKhuonMat')), ten: 'Cài đặt khuôn mặt', vaiTro: DA_DANG_NHAP },

  // Dia chi cu con nam trong link / bookmark
  { duong: '/dang-nhap', chuyenDen: '/account/login', boCuc: 'trong' },
  { duong: '/account/forgotpassword', chuyenDen: '/account/quen-mat-khau', boCuc: 'trong' },
  { duong: '/quen-mat-khau', chuyenDen: '/account/quen-mat-khau', boCuc: 'trong' },
  { duong: '/account', chuyenDen: '/account/profile', boCuc: 'trong' },
]
