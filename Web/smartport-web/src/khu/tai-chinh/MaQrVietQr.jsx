import { useMemo } from 'react'
import { taoMaQr } from '../cong/maQr'

// Vung trang quanh ma (quiet zone) 4 o theo ISO/IEC 18004 — app ngan hang quet tren man hinh / giay in de bat hon
const VIEN = 4

/**
 * MA QR VIETQR ve TAI CHO tu chuoi EMVCo / NAPAS 247 do may chu tu sinh (qrNoiDung, da co CRC):
 * khong goi img.vietqr.io -> trang thanh toan / phieu thu van hien QR khi MAT INTERNET (dien thoai khach co 4G
 * van chuyen khoan duoc), va khong day so tien + noi dung chuyen khoan len may chu ben thu ba.
 * qrUrl (anh img.vietqr.io) chi la DU PHONG cho may chu cu chua tra qrNoiDung.
 */
export default function MaQrVietQr({ noiDung, url, canh = 240, moTa = 'Mã QR VietQR', className, style }) {
  const hinh = useMemo(() => {
    if (!noiDung) return null
    try {
      const { co, o } = taoMaQr(noiDung, 'M')
      const net = []
      for (let y = 0; y < co; y++) for (let x = 0; x < co; x++) if (o[y][x]) net.push(`M${x + VIEN} ${y + VIEN}h1v1h-1z`)
      return { co: co + VIEN * 2, d: net.join('') }
    } catch {
      return null // chuoi hong / qua dai: roi xuong anh du phong
    }
  }, [noiDung])

  if (hinh) {
    return (
      <svg
        className={className}
        width={canh}
        height={canh}
        viewBox={`0 0 ${hinh.co} ${hinh.co}`}
        shapeRendering="crispEdges"
        role="img"
        aria-label={moTa}
        style={{ display: 'block', height: 'auto', background: '#fff', ...style }}
      >
        <title>{moTa}</title>
        <rect width={hinh.co} height={hinh.co} fill="#fff" />
        <path d={hinh.d} fill="#000" />
      </svg>
    )
  }
  if (url) return <img className={className} src={url} alt={moTa} style={style} />
  return null
}
