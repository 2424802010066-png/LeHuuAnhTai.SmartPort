import { lazy } from 'react'

// Khu DIEU HANH THONG MINH: agent tu hanh dong, AI agent dieu phoi, hoc may,
// quy trinh van hanh, cang xanh ESG, gia tri AI (ROI).
// Dia chi GIU NGUYEN ban MVC, viet thuong (link trong CSDL / ma QR / agent sinh ra van mo dung).

export default [
  { duong: '/admin/agent', trang: lazy(() => import('./AgentTuDong')), ten: 'Agent tự động' },
  { duong: '/admin/aitools/agent', trang: lazy(() => import('./AgentDieuPhoi')), ten: 'AI Agent điều phối cảng' },
  { duong: '/admin/aitools/ml', trang: lazy(() => import('./HocMay')), ten: 'Học máy' },
  { duong: '/admin/dashboard/quytrinh', trang: lazy(() => import('./QuyTrinhVanHanh')), ten: 'Quy trình vận hành' },
  { duong: '/admin/dashboard/cangxanh', trang: lazy(() => import('./CangXanh')), ten: 'Cảng xanh · ESG' },
  { duong: '/admin/dashboard/giatriai', trang: lazy(() => import('./GiaTriAi')), ten: 'Giá trị AI · ROI' },
]
