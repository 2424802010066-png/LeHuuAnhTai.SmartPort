// Mau + ten lop vung ma container (bo phat hien YOLO26 5 lop) — dung chung trang AI may chu (anh) va AI video
export const MAU_LOP = { ma_ngang: '#ef4444', ma_doc: '#f97316', so_huu: '#22c55e', seri: '#06b6d4', ma_loai: '#d946ef' }
export const TEN_LOP = { ma_ngang: 'mã ngang', ma_doc: 'mã dọc', so_huu: 'chủ sở hữu', seri: 'số seri', ma_loai: 'mã loại' }
export const MAU_BIEN_CHAC = '#22c55e'
// dinh dang video dich vu AI doc duoc (OpenCV + FFmpeg) — the input file + nhan dien tep video theo ten khi trinh duyet khong biet MIME
export const NHAN_VIDEO = 'video/*,.mp4,.m4v,.mov,.avi,.mkv,.webm,.3gp'
export const laVideo = (tep) => !!tep && ((tep.type || '').startsWith('video/') || /\.(mp4|m4v|mov|avi|mkv|webm|3gp)$/i.test(tep.name || ''))
export const MAU_BIEN_NGHI = '#eab308'

/** Ve 1 khung + nhan len canvas 2D (toa do da doi sang pixel canvas) */
export function veKhung(g, [x1, y1, x2, y2], mau, nhan, coChu = 14) {
  g.lineWidth = Math.max(2, coChu / 5)
  g.strokeStyle = mau
  g.strokeRect(x1, y1, x2 - x1, y2 - y1)
  if (!nhan) return
  g.font = `600 ${coChu}px system-ui, sans-serif`
  const w = g.measureText(nhan).width + 8
  const h = coChu + 6
  const y = y1 >= h ? y1 - h : y2
  g.fillStyle = mau
  g.fillRect(x1, y, w, h)
  g.fillStyle = '#fff'
  g.fillText(nhan, x1 + 4, y + coChu)
}
