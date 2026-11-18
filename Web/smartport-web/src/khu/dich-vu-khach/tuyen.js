import { lazy } from 'react'

// Khu DICH VU KHACH: dat dich vu (booking), cong thanh toan VietQR, hang & lenh cua toi.
// Dia chi GIU NGUYEN cua ban MVC (viet thuong) de link cu / ma QR da in van mo dung man hinh.
const KHACH = ['KhachHang']

export default [
  // ---- Dat dich vu (MVC: BookingController) ----
  { duong: '/booking', chuyenDen: '/booking/my' },
  { duong: '/booking/index', chuyenDen: '/booking/my' },
  { duong: '/booking/my', trang: lazy(() => import('./DonCuaToi')), ten: 'Booking của tôi', vaiTro: KHACH },
  { duong: '/booking/create', trang: lazy(() => import('./DatDichVu')), ten: 'Đặt dịch vụ cảng', vaiTro: KHACH },
  { duong: '/booking/details/:id', trang: lazy(() => import('./ChiTietDon')), ten: 'Chi tiết booking', vaiTro: KHACH },
  { duong: '/booking/invoiceprint/:id', trang: lazy(() => import('./HoaDonInKhach')), ten: 'Hóa đơn dịch vụ', boCuc: 'in', vaiTro: KHACH },
  { duong: '/booking/invoice/:id', trang: lazy(() => import('./TaiHoaDon')), ten: 'Tải hóa đơn', vaiTro: KHACH },

  // ---- Cong thanh toan (MVC: PaymentController) ----
  // MVC nhan Index(int id): /Payment?id=5 va /Payment/Index/5 -> giu ca hai
  { duong: '/payment', trang: lazy(() => import('./ChonPhuongThuc')), ten: 'Thanh toán trực tuyến', vaiTro: KHACH },
  { duong: '/payment/index/:id', trang: lazy(() => import('./ChonPhuongThuc')), ten: 'Thanh toán trực tuyến', vaiTro: KHACH },
  { duong: '/payment/pay/:id', trang: lazy(() => import('./TrangThanhToan')), ten: 'Hoàn tất thanh toán', vaiTro: KHACH },
  { duong: '/payment/receipt/:id', trang: lazy(() => import('./BienLai')), ten: 'Biên lai thanh toán', vaiTro: KHACH },
  { duong: '/payment/history', trang: lazy(() => import('./LichSuThanhToan')), ten: 'Lịch sử thanh toán', vaiTro: KHACH },

  // ---- Hang & lenh cua toi (MVC: HangCuaToiController) ----
  { duong: '/hangcuatoi', trang: lazy(() => import('./HangCuaToi')), ten: 'Hàng & lệnh của tôi', vaiTro: KHACH },
  { duong: '/hangcuatoi/index', chuyenDen: '/hangcuatoi' },
]
