import { lazy } from 'react'

// Khu XU LY SU CO (UC22 -> UC28) — bang SuCo, API /api/su-co:
//   /bao-su-co    khach hang bao su co, phan anh (UC22, UC26) + xem phan anh cua toi
//   /admin/su-co  nhan vien: tiep nhan, tam giu container, ghi ket qua, dong, bao gia, thong ke (UC23–UC28)
export default [
  { duong: '/admin/su-co', trang: lazy(() => import('./QuanLySuCo')), ten: 'Quản lý sự cố' },
  { duong: '/bao-su-co', trang: lazy(() => import('./BaoSuCoKhach')), ten: 'Báo sự cố, phản ánh', vaiTro: ['KhachHang'] },
]
