import { useCallback, useEffect, useRef } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'

// =====================================================================
// DUNG CHUNG TRONG KHU KINH DOANH (don dat dich vu, khach hang, dich vu & gia,
// tin tuc, thu vien anh, hop thu lien he)
// =====================================================================

export const API_KD = '/api/kinh-doanh'
export const API_DON = API_KD + '/don-dat-dich-vu'
export const API_KHACH = API_KD + '/khach-hang'
export const API_DICH_VU = API_KD + '/dich-vu'
export const API_TIN = API_KD + '/tin-tuc'
export const API_ANH = API_KD + '/anh-website'
export const API_LIEN_HE = API_KD + '/lien-he'

/** 5 trang thai booking cua ban MVC (Areas/Admin/Controllers/BookingsController.ValidStatus) */
export const TRANG_THAI_DON = ['Chờ duyệt', 'Đã duyệt', 'Đang thực hiện', 'Hoàn thành', 'Đã hủy']

/** Nhom dich vu trong form MVC Services/Form.cshtml */
export const NHOM_DICH_VU = ['Bốc xếp', 'Lưu bãi', 'Kho bãi', 'Vận chuyển', 'Khác']

/** Khoa anh bia bai viet (bang CauHinhAnh) — form MVC News/Form.cshtml */
export const ANH_BIA_TIN = ['g1', 'g2', 'g3', 'g4', 'g5', 'g6']

export const duongChiTietDon = (id) => '/admin/bookings/details/' + id
export const duongInHoaDon = (id) => '/admin/bookings/invoiceprint/' + id
export const duongHoSoKhach = (id) => '/admin/customers/details/' + id

/**
 * Nut chuyen trang thai booking — dung cac nut cua man hinh MVC Admin/Bookings/Details:
 * Cho duyet -> Duyet / Tu choi; Da duyet -> Bat dau thuc hien; Dang thuc hien -> Hoan thanh.
 */
export const NUT_TRANG_THAI = {
  'Đã duyệt': { nhan: 'Duyệt booking', icon: 'bi-check2-circle', mau: 'primary' },
  'Đã hủy': { nhan: 'Từ chối', icon: 'bi-x-circle', mau: 'outline-danger', hoi: 'Từ chối booking này? Các khoản chờ thanh toán của booking sẽ bị hủy theo.' },
  'Đang thực hiện': { nhan: 'Bắt đầu thực hiện', icon: 'bi-play-circle', mau: 'primary' },
  'Hoàn thành': { nhan: 'Hoàn thành', icon: 'bi-flag-fill', mau: 'success' },
}

/** Bo dau tieng Viet de loc tren trinh duyet (danh muc nho) */
export const khongDau = (s) =>
  (s || '')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd')
    .toLowerCase()

const THE_AN_TOAN = new Set(['P', 'BR', 'STRONG', 'B', 'EM', 'I', 'U', 'UL', 'OL', 'LI', 'H2', 'H3', 'H4', 'BLOCKQUOTE', 'SPAN', 'DIV', 'HR', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD'])

/**
 * Loc HTML bai viet truoc khi xem truoc: chi giu the trinh bay, bo script/style/iframe
 * va MOI thuoc tinh (ke ca on*, href, style) — noi dung do bien tap vien go, khong duoc chay ma.
 */
export function locHtmlAnToan(html) {
  if (!html) return ''
  const tai = new DOMParser().parseFromString('<div id="goc">' + html + '</div>', 'text/html')
  const goc = tai.getElementById('goc')
  const duyet = (nut) => {
    for (const con of [...nut.children]) {
      if (!THE_AN_TOAN.has(con.tagName)) {
        // The la: giu lai chu ben trong, bo the
        const chu = tai.createTextNode(con.textContent || '')
        con.replaceWith(chu)
        continue
      }
      for (const ten of [...con.getAttributeNames()]) con.removeAttribute(ten)
      duyet(con)
    }
  }
  duyet(goc)
  return goc.innerHTML
}

/**
 * Form them/sua dang HOP THOAI nhung van GIU dia chi cua ban MVC:
 *   /admin/customers          -> danh sach, hop thoai dong
 *   /admin/customers/form     -> mo san hop thoai them moi        (dangSua = 0)
 *   /admin/customers/form/5   -> mo san hop thoai sua khach #5    (dangSua = 5)
 *   /admin/customers/form?id=5 -> link cu dang tham so cung mo dung
 * Hai tuyen dung CUNG mot component lazy nen doi qua lai khong dung lai trang.
 * Bam Back cua trinh duyet = dong hop thoai.
 */
export function useFormTheoDiaChi(duongGoc) {
  const viTri = useLocation()
  const dieuHuong = useNavigate()
  const { id } = useParams()

  const duong = viTri.pathname.toLowerCase().replace(/\/+$/, '')
  const laForm = duong === duongGoc + '/form' || duong.startsWith(duongGoc + '/form/')
  const thamSo = new URLSearchParams(viTri.search)
  const so = Number.parseInt(id ?? thamSo.get('id'), 10)
  const dangSua = laForm ? (so > 0 ? so : 0) : null

  // Giu bo loc (?q=...&trang=2) khi mo / dong form, bo tham so id cua link cu
  thamSo.delete('id')
  const timKiem = thamSo.toString() ? '?' + thamSo.toString() : ''

  const moiNhat = useRef({ viTri, dieuHuong, timKiem })
  useEffect(() => {
    moiNhat.current = { viTri, dieuHuong, timKiem }
  })

  const moForm = useCallback(
    (ma) => {
      const { dieuHuong: dh, timKiem: tk } = moiNhat.current
      dh(duongGoc + '/form' + (ma ? '/' + ma : '') + tk, { state: { tuDanhSach: true } })
    },
    [duongGoc],
  )

  const dongForm = useCallback(() => {
    const { viTri: vt, dieuHuong: dh, timKiem: tk } = moiNhat.current
    if (vt.state?.tuDanhSach) dh(-1)
    else dh(duongGoc + tk, { replace: true })
  }, [duongGoc])

  return { dangSua, moForm, dongForm }
}
