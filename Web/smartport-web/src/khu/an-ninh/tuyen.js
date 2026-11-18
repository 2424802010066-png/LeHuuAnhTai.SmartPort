import { lazy } from 'react'

// Khu AN NINH: phong an ninh (truc ban) · tram camera AI · kho media AI.
// Dia chi giu nguyen ban MVC (/Admin/Security -> /admin/security) de link trong CSDL,
// link do nghiep vu sinh ra va bookmark cua truc ban van mo dung man hinh.
// (/admin/security/tally — Kiem dem AI — thuoc khu kiem-dem, khong dang ky o day.)
export default [
  { duong: '/admin/security', trang: lazy(() => import('./PhongAnNinh')), ten: 'Phòng an ninh — cảnh báo AI' },
  { duong: '/admin/security/monitor', trang: lazy(() => import('./TramCamera')), ten: 'Trạm camera an ninh AI' },
  { duong: '/admin/security/media', trang: lazy(() => import('./KhoMedia')), ten: 'Kho ảnh & video AI' },
  { duong: '/admin/an-toan-ai', trang: lazy(() => import('./CanhBaoNguyHiem')), ten: 'Cảnh báo nguy hiểm AI — an toàn lao động cảng' },
  // Dia chi cu trong mot so link/tai lieu -> man hinh phong an ninh
  { duong: '/admin/security/index', chuyenDen: '/admin/security' },
]
