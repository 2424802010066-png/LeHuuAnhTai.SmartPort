import { lazy } from 'react'

// Khu DIEU PHOI: hang cho ao ngoai cong, hang nguy hiem IMDG, thiet bi bai, ke hoach xep do tau.
export default [
  { duong: '/admin/hang-cho-ao', trang: lazy(() => import('./HangChoAo')), ten: 'Hàng chờ ảo ngoài cổng' },
  { duong: '/admin/hang-nguy-hiem', trang: lazy(() => import('./HangNguyHiem')), ten: 'Hàng nguy hiểm IMDG' },
  { duong: '/admin/thiet-bi-bai', trang: lazy(() => import('./ThietBiBai')), ten: 'Điều phối thiết bị bãi' },
  { duong: '/admin/ke-hoach-xep-do', trang: lazy(() => import('./KeHoachXepDo')), ten: 'Kế hoạch xếp dỡ tàu' },
]
