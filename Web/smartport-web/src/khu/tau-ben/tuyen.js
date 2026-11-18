import { lazy } from 'react'

// Khu TAU & CAU BEN: chuyen tau, tau bien, hang tau, ben cang, Gantt, AI phan bo ben
// Trang danh sach va dia chi form cu (/form/:id?) dung CUNG mot component: form la hop thoai mo san,
// doi qua lai giua hai dia chi khong dung lai trang (xem useFormTheoDiaChi trong chung.js).
const ChuyenTau = lazy(() => import('./ChuyenTau'))
const TauBien = lazy(() => import('./TauBien'))
const BenCang = lazy(() => import('./BenCang'))

export default [
  { duong: '/admin/shippinglines', trang: lazy(() => import('./HangTau')), ten: 'Hãng tàu đối tác' },
  { duong: '/admin/shippinglines/form/:id?', chuyenDen: '/admin/shippinglines' },

  { duong: '/admin/vesselcalls', trang: ChuyenTau, ten: 'Điều độ chuyến tàu' },
  { duong: '/admin/vesselcalls/form/:id?', trang: ChuyenTau, ten: 'Điều độ chuyến tàu' },

  { duong: '/admin/vessels', trang: TauBien, ten: 'Quản lý tàu biển' },
  { duong: '/admin/vessels/form/:id?', trang: TauBien, ten: 'Quản lý tàu biển' },

  { duong: '/admin/berths', trang: BenCang, ten: 'Quản lý bến cảng' },
  { duong: '/admin/berths/form/:id?', trang: BenCang, ten: 'Quản lý bến cảng' },
  { duong: '/admin/berths/gantt', trang: lazy(() => import('./GanttBen')), ten: 'Gantt cầu bến' },

  { duong: '/admin/aitools/berth', trang: lazy(() => import('./AiPhanBen')), ten: 'AI gợi ý phân bổ bến' },
]
