import { lazy } from 'react'

// Khu CONG · LENH GIAO NHAN · TRAM CAN · KHO CHUNG TU.
// Dia chi giu nguyen ban MVC (/Admin/Gate/HoSoXe -> /admin/gate/hosoxe) de link trong CSDL,
// ma QR da in va link do nghiep vu sinh ra (LienKetService, tro ly, agent) van mo dung man hinh.
// (/admin/gate — Cong thong minh ALPR — dang ky o khu tong-quan, dung chung pages/CongThongMinh.)
export default [
  { duong: '/admin/gate/hosoxe', trang: lazy(() => import('./HoSoXe')), ten: 'Hồ sơ xe · tài xế 360°' },
  { duong: '/admin/gate/history', trang: lazy(() => import('./NhatKyCong')), ten: 'Nhật ký xe ra/vào cổng' },
  { duong: '/admin/gate/phieucan', trang: lazy(() => import('./TramCan')), ten: 'Trạm cân xe · VGM' },
  { duong: '/admin/gate/inphieucan/:id', trang: lazy(() => import('./InPhieuCan')), ten: 'In phiếu cân', boCuc: 'in' },
  // Man hinh bao ve va phieu tay khong dung khung dieu hanh: bao ve dung o chot cong, chi can chu to / to giay
  { duong: '/admin/gate/baove', trang: lazy(() => import('./ChotBaoVe')), ten: 'Chốt cổng (bảo vệ)', boCuc: 'trong' },
  { duong: '/admin/gate/phieutay', trang: lazy(() => import('./PhieuTay')), ten: 'Phiếu ra vào cổng ghi tay', boCuc: 'in' },
  // Dia chi cu "can" trong mot so link/tai lieu -> tram can
  { duong: '/admin/gate/can', chuyenDen: '/admin/gate/phieucan' },

  { duong: '/admin/gatepass', trang: lazy(() => import('./LenhGiaoNhan')), ten: 'Lệnh giao nhận điện tử' },
  { duong: '/admin/gatepass/form/:id?', trang: lazy(() => import('./FormLenh')), ten: 'Tạo / sửa lệnh giao nhận' },
  { duong: '/admin/gatepass/in/:id', trang: lazy(() => import('./InLenh')), ten: 'In lệnh giao nhận', boCuc: 'in' },

  { duong: '/admin/docs', trang: lazy(() => import('./KhoChungTu')), ten: 'Kho chứng từ số' },
]
