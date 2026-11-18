import { lazy } from 'react'

// Khu CONTAINER & BAI: danh sach container, ho so 360°, phieu EIR (ban in), form them/sua (AI goi y o bai),
// so do bai, giam sat container lanh. Dia chi giu nguyen ban MVC (/Admin/Containers/HoSo/5 -> /admin/containers/hoso/:id).
// (/admin/containers/tracuu da dang ky o khu tong-quan — dung chung man hinh pages/TraCuuContainer.)
export default [
  { duong: '/admin/containers', trang: lazy(() => import('./DanhSachContainer')), ten: 'Quản lý container' },
  { duong: '/admin/containers/hoso/:id', trang: lazy(() => import('./HoSoContainer')), ten: 'Hồ sơ container 360°' },
  { duong: '/admin/containers/eir/:id', trang: lazy(() => import('./PhieuEir')), ten: 'Phiếu giao nhận EIR', boCuc: 'in' },
  { duong: '/admin/containers/form/:id?', trang: lazy(() => import('./FormContainer')), ten: 'Thêm / sửa container' },
  { duong: '/admin/containers/yardmap', trang: lazy(() => import('./SoDoBai')), ten: 'Sơ đồ bãi container' },
  { duong: '/admin/reefer', trang: lazy(() => import('./ContainerLanh')), ten: 'Giám sát container lạnh' },
]
