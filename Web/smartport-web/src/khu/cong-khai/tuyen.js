import { lazy } from 'react'

// =====================================================================
// WEBSITE CONG KHAI: trang chu, gioi thieu, lien he, trang loi.
// Giu nguyen dia chi cua ban MVC (/Home/About -> /home/about) de link cu,
// bookmark va ma QR da in van mo dung trang.
// Khung website (dau trang, chan trang, PortBot) nam o BoCucCongKhai.jsx.
// =====================================================================
export default [
  { duong: '/', trang: lazy(() => import('./TrangChu')), ten: 'Trang chủ' },
  { duong: '/home', chuyenDen: '/' },
  { duong: '/home/index', chuyenDen: '/' },
  { duong: '/home/about', trang: lazy(() => import('./GioiThieu')), ten: 'Giới thiệu' },
  { duong: '/home/contact', trang: lazy(() => import('./LienHe')), ten: 'Liên hệ' },
  { duong: '/home/loi', trang: lazy(() => import('./TrangLoi')), ten: 'Có lỗi xảy ra' },
  { duong: '/home/khongtimthay', trang: lazy(() => import('./KhongTimThay')), ten: 'Không tìm thấy trang' },
]
