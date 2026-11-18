// Man hinh cua khu nay dang ky tai day (xem src/dieuHuong/tuyen.js).
// KHU TIEN ICH CONG KHAI — tin tuc, dich vu & bang gia, lich tau, uoc tinh chi phi,
// tra cuu container, bang tinh trang cong, cong tai xe, trang ung dung di dong (PWA).
// Dia chi giu nguyen ban MVC (viet thuong): /News -> /news, /News/Details/5 -> /news/details/5...
// nen ma QR da in, link trong CSDL va bookmark cua khach van mo dung trang.
// Cac trang deu nam trong khung website cong khai (BoCucCongKhai) — khong dat boCuc rieng.
import { lazy } from 'react'

export default [
  { duong: '/news', trang: lazy(() => import('./TinTuc')), ten: 'Tin tức' },
  { duong: '/news/details/:id', trang: lazy(() => import('./ChiTietTinTuc')), ten: 'Chi tiết tin' },
  { duong: '/services', trang: lazy(() => import('./DichVu')), ten: 'Dịch vụ & bảng giá' },
  { duong: '/schedule', trang: lazy(() => import('./LichTau')), ten: 'Lịch tàu' },
  { duong: '/cost', trang: lazy(() => import('./UocTinhChiPhi')), ten: 'Ước tính chi phí' },
  { duong: '/tracking', trang: lazy(() => import('./TraCuuContainer')), ten: 'Tra cứu container' },
  { duong: '/cong', trang: lazy(() => import('./TinhTrangCong')), ten: 'Tình trạng cổng' },
  { duong: '/taixe', trang: lazy(() => import('./CongTaiXe')), ten: 'Cổng tài xế' },
  { duong: '/mobile', trang: lazy(() => import('./AppDiDong')), ten: 'Ứng dụng di động' },

  // Dia chi cu cua ban MVC (dang .../Index) va loi tat tieng Viet -> chuyen ve trang moi
  { duong: '/news/index', chuyenDen: '/news' },
  { duong: '/services/index', chuyenDen: '/services' },
  { duong: '/schedule/index', chuyenDen: '/schedule' },
  { duong: '/cost/index', chuyenDen: '/cost' },
  { duong: '/tracking/index', chuyenDen: '/tracking' },
  { duong: '/cong/index', chuyenDen: '/cong' },
  { duong: '/taixe/index', chuyenDen: '/taixe' },
  { duong: '/mobile/index', chuyenDen: '/mobile' },
  { duong: '/tra-cuu', chuyenDen: '/tracking' },
]
