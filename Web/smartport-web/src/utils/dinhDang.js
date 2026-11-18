const boSo = new Intl.NumberFormat('vi-VN')

export function dinhDangSo(v, le = 0) {
  if (v == null || Number.isNaN(Number(v))) return '—'
  return new Intl.NumberFormat('vi-VN', { minimumFractionDigits: le, maximumFractionDigits: le }).format(v)
}

export const dinhDangTien = (v) => (v == null ? '—' : boSo.format(Math.round(v)) + ' đ')

export function tienGon(v) {
  if (v == null) return '—'
  const a = Math.abs(v)
  if (a >= 1e9) return dinhDangSo(v / 1e9, 1) + ' tỷ'
  if (a >= 1e6) return dinhDangSo(v / 1e6, 1) + ' tr'
  return boSo.format(Math.round(v))
}

export const dinhDangNgay = (v) => (v ? new Date(v).toLocaleDateString('vi-VN') : '—')

/** "19:17 17/09" — gon cho bang danh sach */
export function gioNgayNgan(v) {
  if (!v) return '—'
  const d = new Date(v)
  const hai = (n) => String(n).padStart(2, '0')
  return `${hai(d.getHours())}:${hai(d.getMinutes())} ${hai(d.getDate())}/${hai(d.getMonth() + 1)}`
}

export const dinhDangNgayGio = (v) =>
  v
    ? new Date(v).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' })
    : '—'

const hai = (n) => String(n).padStart(2, '0')

/** Gia tri cho <input type="date">: "2026-09-17" (theo gio may, khong lech mui gio nhu toISOString) */
export function ngayInput(v) {
  if (!v) return ''
  const d = new Date(v)
  return Number.isNaN(d.getTime()) ? '' : `${d.getFullYear()}-${hai(d.getMonth() + 1)}-${hai(d.getDate())}`
}

/** Gia tri cho <input type="datetime-local">: "2026-09-17T08:30" */
export function ngayGioInput(v) {
  if (!v) return ''
  const d = new Date(v)
  return Number.isNaN(d.getTime()) ? '' : `${ngayInput(d)}T${hai(d.getHours())}:${hai(d.getMinutes())}`
}

/** "3 phút trước", "2 giờ trước", "hôm qua" — cho cot thoi gian gan day */
export function thoiGianTruoc(v) {
  if (!v) return '—'
  const giay = Math.round((Date.now() - new Date(v).getTime()) / 1000)
  if (giay < 45) return 'vừa xong'
  if (giay < 3600) return Math.round(giay / 60) + ' phút trước'
  if (giay < 86400) return Math.round(giay / 3600) + ' giờ trước'
  if (giay < 172800) return 'hôm qua'
  return dinhDangNgay(v)
}
