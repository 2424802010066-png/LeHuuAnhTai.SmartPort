import { lazy } from 'react'

// Khu KINH DOANH: don dat dich vu (booking), khach hang, dich vu & bang gia,
// tin tuc, thu vien anh website, hop thu lien he — phia quan tri.
// Trang danh sach va dia chi form cu (/form/:id?) dung CUNG mot component:
// form la hop thoai mo san, doi qua lai giua hai dia chi khong tai lai danh sach
// (xem useFormTheoDiaChi trong chung.js).
const KhachHang = lazy(() => import('./KhachHang'))
const DichVuGia = lazy(() => import('./DichVuGia'))
const TinTuc = lazy(() => import('./TinTuc'))

export default [
  { duong: '/admin/bookings', trang: lazy(() => import('./DonDatDichVu')), ten: 'Đơn đặt dịch vụ' },
  { duong: '/admin/bookings/details/:id', trang: lazy(() => import('./ChiTietDon')), ten: 'Xử lý booking' },
  { duong: '/admin/bookings/invoiceprint/:id', trang: lazy(() => import('./HoaDonIn')), ten: 'Hóa đơn dịch vụ', boCuc: 'in' },

  { duong: '/admin/customers', trang: KhachHang, ten: 'Quản lý khách hàng' },
  { duong: '/admin/customers/form/:id?', trang: KhachHang, ten: 'Quản lý khách hàng' },
  { duong: '/admin/customers/details/:id', trang: lazy(() => import('./HoSoKhachHang')), ten: 'Hồ sơ khách hàng' },

  { duong: '/admin/services', trang: DichVuGia, ten: 'Dịch vụ & bảng giá' },
  { duong: '/admin/services/form/:id?', trang: DichVuGia, ten: 'Dịch vụ & bảng giá' },

  { duong: '/admin/news', trang: TinTuc, ten: 'Quản lý tin tức' },
  { duong: '/admin/news/form/:id?', trang: TinTuc, ten: 'Quản lý tin tức' },

  { duong: '/admin/gallery', trang: lazy(() => import('./AnhWebsite')), ten: 'Thư viện ảnh website' },
  { duong: '/admin/contacts', trang: lazy(() => import('./HopThuLienHe')), ten: 'Hộp thư liên hệ' },
]
