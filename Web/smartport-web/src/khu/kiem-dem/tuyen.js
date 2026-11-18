// Man hinh cua khu nay dang ky tai day (xem src/dieuHuong/tuyen.js).
// Dia chi GIU NGUYEN dia chi ban MVC (viet thuong): link trong menu, ma QR da in
// va link do nghiep vu sinh ra van mo dung man hinh.
import { lazy } from 'react'

export default [
  { duong: '/admin/security/tally', trang: lazy(() => import('./KiemDemAi')), ten: 'Kiểm đếm AI' },
  { duong: '/admin/driver', trang: lazy(() => import('./GiamSatTaiXe')), ten: 'Giám sát tài xế' },
]
