// Dung chung trong khu EDI hang tau + Depot (chi cac trang trong src/khu/edi-depot dung)

/** Lop mau nhan loai ban tin (edi-depot.css) */
export const LOP_LOAI_BAN_TIN = { CODECO: 'ed-loai-codeco', COARRI: 'ed-loai-coarri', COPARN: 'ed-loai-coparn' }

/**
 * Trang thai ban tin hien tren man hinh — giong ban MVC: Đã gửi / Đã xử lý / Lỗi,
 * con lai (null, "Chờ gửi"...) deu hien "Chờ gửi".
 */
export function trangThaiBanTin(tt) {
  return tt === 'Đã gửi' || tt === 'Đã xử lý' || tt === 'Lỗi' ? tt : 'Chờ gửi'
}

/** Mau NhanTrangThai cho ban tin EDI */
export const MAU_TRANG_THAI_EDI = { 'Đã gửi': 'ok', 'Đã xử lý': 'info', 'Lỗi': 'do', 'Chờ gửi': 'warn' }

/** Mau NhanTrangThai cho EOR — giong ban MVC: duyet xanh la, da sua xanh duong, tu choi xam, cho duyet vang */
export const MAU_TRANG_THAI_EOR = { 'Chờ duyệt': 'warn', 'Đã duyệt': 'ok', 'Đã sửa': 'info', 'Từ chối': 'off' }

/** Loai lenh giao nhan tao tu COPARN (thu tu giong o chon ban MVC) */
export const LOAI_LENH_COPARN = [
  { giaTri: 'LayRong', nhan: 'Lấy container rỗng' },
  { giaTri: 'TraRong', nhan: 'Trả container rỗng' },
  { giaTri: 'GiaoCont', nhan: 'Giao container (khách nhận hàng)' },
  { giaTri: 'HaCont', nhan: 'Hạ container vào bãi' },
]

const hai = (n) => String(n).padStart(2, '0')

/** "17/09" */
export function ngayThang(v) {
  if (!v) return '—'
  const d = new Date(v)
  return `${hai(d.getDate())}/${hai(d.getMonth() + 1)}`
}

/** Ky bao cao "10/09 → 17/09/2026" (cung ngay thi chi hien 1 ngay) */
export function kyBaoCao(tu, den) {
  if (!tu) return '—'
  const a = new Date(tu)
  const b = den ? new Date(den) : a
  const ngayDu = (d) => `${hai(d.getDate())}/${hai(d.getMonth() + 1)}/${d.getFullYear()}`
  return a.toDateString() === b.toDateString() ? ngayDu(b) : `${ngayThang(a)} → ${ngayDu(b)}`
}

/** Tach danh sach so container go tay (phay, cham phay, khoang trang, xuong dong) — dem truoc khi gui */
export function tachSoCont(chu) {
  return [...new Set((chu || '').split(/[\s,;]+/).map((x) => x.trim().toUpperCase()).filter(Boolean))]
}

/**
 * Sao chep chu vao clipboard. Trang mo qua http (khong phai localhost) thi navigator.clipboard
 * khong co -> dung o nhap tam + execCommand. Tra ve true neu chep duoc.
 */
export async function saoChep(chu) {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(chu)
      return true
    }
  } catch {
    /* thu cach cu ben duoi */
  }
  const o = document.createElement('textarea')
  o.value = chu
  o.setAttribute('readonly', '')
  o.style.position = 'fixed'
  o.style.top = '-1000px'
  document.body.appendChild(o)
  o.select()
  let duoc = false
  try {
    duoc = document.execCommand('copy')
  } catch {
    duoc = false
  }
  o.remove()
  return duoc
}
