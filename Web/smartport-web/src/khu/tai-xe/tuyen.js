// Man hinh cua khu nay dang ky tai day (xem src/dieuHuong/tuyen.js).
// Khu TAI XE (quet CCCD / GPLX) + DOI SOAT CONG — giu dia chi ban MVC: Admin/TaiXe, Admin/TaiXe/Quet, Admin/DoiSoatCong.
// Dia chi cu dang /admin/taixe/index, /admin/doisoatcong/index da duoc App.jsx chuyen ve trang danh sach.
import { lazy } from 'react'

export default [
  { duong: '/admin/taixe', trang: lazy(() => import('./DanhSachTaiXe')), ten: 'Hồ sơ tài xế' },
  { duong: '/admin/taixe/quet', trang: lazy(() => import('./QuetGiayTo')), ten: 'Quét giấy tờ tài xế' },
  { duong: '/admin/doisoatcong', trang: lazy(() => import('./DoiSoatCong')), ten: 'Đối soát cổng' },
]
