import { lazy } from 'react'

// Khu CANG SO & TUAN THU: lich hen cong (TAS), lenh giao hang dien tu (eDO), thong quan hai quan (VASSCM),
// chong trao container (AI doi chieu), giam sat kien truc microservices.
export default [
  { duong: '/admin/lich-hen', trang: lazy(() => import('./LichHenCong')), ten: 'Lịch hẹn giờ vào cổng (TAS)' },
  { duong: '/admin/edo', trang: lazy(() => import('./Edo')), ten: 'Lệnh giao hàng điện tử (eDO)' },
  { duong: '/admin/hai-quan', trang: lazy(() => import('./HaiQuan')), ten: 'Thông quan hải quan (VASSCM)' },
  { duong: '/admin/doi-chieu-container', trang: lazy(() => import('./DoiChieuContainer')), ten: 'Chống tráo container' },
  { duong: '/admin/ai-may-chu', trang: lazy(() => import('./AiMayChu')), ten: 'AI thị giác máy chủ (YOLO26 · RF-DETR · CRNN)' },
  { duong: '/admin/microservices', trang: lazy(() => import('./GiamSatMicroservices')), ten: 'Giám sát microservices', vaiTro: ['Admin'] },
]
