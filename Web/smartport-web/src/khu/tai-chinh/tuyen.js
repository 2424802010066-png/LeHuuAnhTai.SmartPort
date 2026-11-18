import { lazy } from 'react'

// Khu THU TIEN: phi luu bai & cong no, thu phi cong, doi soat thanh toan, bao cao thong ke.
// Dia chi giu nguyen ban MVC (Admin/LuuBai, Admin/PhiCong, Admin/Payments, Admin/Reports) — link trong
// so chung tu, tro ly, ma QR da in va LienKetService van mo dung man hinh.
export default [
  { duong: '/admin/luubai', trang: lazy(() => import('./BangKeLuuBai')), ten: 'Phí lưu bãi · công nợ' },
  { duong: '/admin/luubai/danhsach', trang: lazy(() => import('./SoPhieuLuuBai')), ten: 'Sổ phiếu thu phí lưu bãi' },
  { duong: '/admin/luubai/phieu/:id', trang: lazy(() => import('./PhieuThuLuuBai')), ten: 'Phiếu thu phí lưu bãi', boCuc: 'in' },

  { duong: '/admin/phicong', trang: lazy(() => import('./PhiCong')), ten: 'Thu phí cổng' },
  { duong: '/admin/phicong/thexe', trang: lazy(() => import('./TheXe')), ten: 'Thẻ xe qua cổng' },
  { duong: '/admin/phicong/tongket', trang: lazy(() => import('./TongKetCa')), ten: 'Tổng kết ca thu phí cổng', boCuc: 'in' },

  { duong: '/admin/payments', trang: lazy(() => import('./DoiSoatThanhToan')), ten: 'Đối soát thanh toán' },
  { duong: '/admin/payments/details/:id', trang: lazy(() => import('./ChiTietThanhToan')), ten: 'Chi tiết giao dịch' },

  { duong: '/admin/reports', trang: lazy(() => import('./BaoCaoThongKe')), ten: 'Báo cáo thống kê' },
]
