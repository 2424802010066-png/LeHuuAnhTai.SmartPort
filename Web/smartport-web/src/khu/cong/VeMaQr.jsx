import { useMemo } from 'react'
import { taoMaQr } from './maQr'

/**
 * Ve ma QR bang <svg> (mot the <path> duy nhat -> in ra sac net, khong vo hat).
 * Dung cho ban in lenh giao nhan: tai xe quet bang dien thoai la ra trang check-in.
 */
export default function VeMaQr({ chu, canh = 110, vien = 2, mucSuaLoi = 'M', moTa = 'Mã QR' }) {
  const duong = useMemo(() => {
    if (!chu) return null
    try {
      const { co, o } = taoMaQr(chu, mucSuaLoi)
      const net = []
      for (let y = 0; y < co; y++) for (let x = 0; x < co; x++) if (o[y][x]) net.push(`M${x + vien} ${y + vien}h1v1h-1z`)
      return { co: co + vien * 2, d: net.join('') }
    } catch {
      // Noi dung qua dai: ban in van ra, chi khong co o QR
      return null
    }
  }, [chu, vien, mucSuaLoi])

  if (!duong) return null
  return (
    <svg
      width={canh}
      height={canh}
      viewBox={`0 0 ${duong.co} ${duong.co}`}
      shapeRendering="crispEdges"
      role="img"
      aria-label={moTa}
      style={{ background: '#fff', flex: '0 0 auto' }}
    >
      <title>{moTa}</title>
      <rect width={duong.co} height={duong.co} fill="#fff" />
      <path d={duong.d} fill="#000" />
    </svg>
  )
}
