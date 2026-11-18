import { lazy } from 'react'

// Khu AI THI GIAC: Camera AI (YOLO26), SafePose AI (uoc luong tu the), Camera IP hien truong.
// Dia chi giu NGUYEN dia chi ban MVC (/Admin/Vision, /Admin/Vision/History...) de link cu van mo dung.
// The Q-Learning cua khu nay duoc gan them vao man hinh AI Lab (src/pages/AiLab.jsx).
export default [
  { duong: '/admin/vision', trang: lazy(() => import('./CameraAi')), ten: 'Camera AI — YOLO26' },
  { duong: '/admin/vision/history', trang: lazy(() => import('./NhatKyNhanDien')), ten: 'Nhật ký nhận diện AI' },
  { duong: '/admin/vision/analytics', trang: lazy(() => import('./ThongKeLuuLuong')), ten: 'Thống kê lưu lượng cổng' },
  { duong: '/admin/vision/pose', trang: lazy(() => import('./SafePose')), ten: 'SafePose AI — an toàn lao động' },
  { duong: '/admin/camera', trang: lazy(() => import('./CameraIp')), ten: 'Camera IP hiện trường' },
]
