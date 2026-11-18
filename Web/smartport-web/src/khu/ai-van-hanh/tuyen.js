import { lazy } from 'react'

// Khu AI VAN HANH: 4 mo-dun hoc may giai bai toan that cua cang can / depot, tinh tren du lieu van hanh:
//   du bao ngay roi bai + giam dao chuyen · du bao un tac cong + so lan can mo ·
//   canh bao som container lanh · phat hien luot xe bat thuong.
// So lieu tu GET /api/ai-van-hanh/* (SmartPort.Core/AppServices/AiVanHanh — thuat toan cai thuan C#).

export default [
  { duong: '/admin/ai', trang: lazy(() => import('./TrungTamAi')), ten: 'Trung tâm AI vận hành' },
  { duong: '/admin/ai/bai', trang: lazy(() => import('./AiBai')), ten: 'AI bãi · dự báo rời bãi & giảm đảo chuyển' },
  { duong: '/admin/ai/cong', trang: lazy(() => import('./AiCong')), ten: 'AI cổng · dự báo ùn tắc & số làn cần mở' },
  { duong: '/admin/ai/cont-lanh', trang: lazy(() => import('./AiContLanh')), ten: 'AI container lạnh · cảnh báo sớm' },
  { duong: '/admin/ai/gian-lan', trang: lazy(() => import('./AiGianLan')), ten: 'AI phát hiện lượt xe bất thường' },
]
