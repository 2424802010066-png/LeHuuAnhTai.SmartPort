import { lazy } from 'react'

// Man hinh da co tu dot dau (src/pages) + cac dia chi cu cua ban MVC tro ve dung cho
export default [
  { duong: '/admin', trang: lazy(() => import('../../pages/TongQuan')), ten: 'Tổng quan' },
  { duong: '/admin/dashboard', chuyenDen: '/admin' },
  { duong: '/admin/gate', trang: lazy(() => import('../../pages/CongThongMinh')), ten: 'Cổng thông minh' },
  { duong: '/admin/ailab', trang: lazy(() => import('../../pages/AiLab')), ten: 'AI Lab dự báo sản lượng' },
  { duong: '/admin/containers/tracuu', trang: lazy(() => import('../../pages/TraCuuContainer')), ten: 'Tra cứu container' },

  // Dia chi cua giao dien React dot dau (truoc khi gom ve /admin).
  // Khong chuyen huong /cong: dia chi do la bang tinh trang cong CONG KHAI cua ban MVC (khu cong-khai).
  { duong: '/ai-lab', chuyenDen: '/admin/ailab', boCuc: 'trong' },
  { duong: '/container', chuyenDen: '/admin/containers/tracuu', boCuc: 'trong' },
]
