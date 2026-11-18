import { lazy } from 'react'

// Khu EDI HANG TAU + DEPOT: ban tin CODECO/COARRI/COPARN, ton rong theo hang tau, phieu sua chua vo EOR
// Dia chi giu nguyen ban MVC (Admin/Edi, Admin/Depot) — link trong so chung tu, tro ly, dong thoi gian container van mo dung.
export default [
  { duong: '/admin/edi', trang: lazy(() => import('./Edi')), ten: 'EDI hãng tàu' },
  { duong: '/admin/edi/xem/:id', trang: lazy(() => import('./XemBanTin')), ten: 'Bản tin EDI' },
  // Nhan + xem truoc + tao lenh trong cung mot man hinh (ban MVC tach 2 view)
  { duong: '/admin/edi/nhancoparn', trang: lazy(() => import('./NhanCoparn')), ten: 'Nhận COPARN từ hãng tàu' },
  { duong: '/admin/edi/xemtruoccoparn', chuyenDen: '/admin/edi/nhancoparn' },
  { duong: '/admin/depot', chuyenDen: '/admin/depot/tonrong' },
  { duong: '/admin/depot/tonrong', trang: lazy(() => import('./TonRong')), ten: 'Tồn rỗng theo hãng tàu' },
  { duong: '/admin/depot/eor', trang: lazy(() => import('./Eor')), ten: 'Phiếu sửa chữa vỏ (EOR)' },
  { duong: '/admin/depot/ineor/:id', trang: lazy(() => import('./InEor')), ten: 'In phiếu EOR', boCuc: 'in' },
]
