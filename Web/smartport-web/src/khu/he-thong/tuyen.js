import { lazy } from 'react'

// Khu HE THONG: van hanh, nhat ky hoat dong, tai khoan, tich hop API cho doi tac.
// Dia chi giu nguyen ban MVC cu (/Admin/System, /Admin/Accounts, /Admin/TichHop) de link
// da luu trong CSDL, link tro ly sinh ra va bookmark cu van mo dung man hinh.
const QuanLyTaiKhoan = lazy(() => import('./QuanLyTaiKhoan'))

export default [
  { duong: '/admin/system', trang: lazy(() => import('./VanHanhHeThong')), ten: 'Vận hành hệ thống' },
  { duong: '/admin/system/nhatky', trang: lazy(() => import('./NhatKyHoatDong')), ten: 'Nhật ký hoạt động' },

  // Form tai khoan la hop thoai mo san tren chinh trang danh sach (xem QuanLyTaiKhoan.jsx)
  { duong: '/admin/accounts', trang: QuanLyTaiKhoan, ten: 'Quản lý tài khoản', vaiTro: ['Admin'] },
  { duong: '/admin/accounts/form/:id?', trang: QuanLyTaiKhoan, ten: 'Quản lý tài khoản', vaiTro: ['Admin'] },

  { duong: '/admin/tichhop', trang: lazy(() => import('./TichHop')), ten: 'Tích hợp · API', vaiTro: ['Admin'] },
  { duong: '/admin/tichhop/tailieu', trang: lazy(() => import('./TaiLieuTichHop')), ten: 'Tài liệu API tích hợp', vaiTro: ['Admin'] },
]
