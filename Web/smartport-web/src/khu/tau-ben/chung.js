import { useCallback, useEffect, useRef } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { dinhDangSo } from '../../utils/dinhDang'

// =====================================================================
// DUNG CHUNG TRONG KHU TAU & CAU BEN
// =====================================================================

export const TRANG_THAI_CHUYEN = ['Dự kiến', 'Đã cập bến', 'Đang làm hàng', 'Đã rời bến', 'Hủy']
export const TRANG_THAI_BEN = ['Hoạt động', 'Bảo trì']
export const LOAI_TAU = ['Container', 'Hàng rời', 'Tổng hợp', 'Tàu dầu']

/** So gon kieu Viet (toi da 1 so le): 294 -> "294", 12.5 -> "12,5", null -> "—" */
export const soGon = (v) => (v == null || v === '' ? '—' : dinhDangSo(v, Number.isInteger(Number(v)) ? 0 : 1))

/** So met (chieu dai, do sau, mon nuoc) */
export const soMet = soGon

// So sanh theo phan muoi met de tranh sai so dau phay dong (12.3 + 0.5 != 12.8)
const muoi = (v) => Math.round(Number(v) * 10)

/**
 * Canh bao som tren form khi xep tau vao ben — cung quy tac voi API:
 * ben dai >= LOA + 15 m day buoc, sau >= mon nuoc + 0,5 m an toan.
 */
export function loiXepBen(ben, tau, dayBuoc = 15, chanAnToan = 0.5) {
  const loi = []
  if (!ben || !tau) return loi
  if (tau.chieuDai != null && muoi(ben.chieuDai) < muoi(tau.chieuDai) + muoi(dayBuoc))
    loi.push(`${ben.tenBen} ngắn hơn yêu cầu: cần ≥ ${soMet(Number(tau.chieuDai) + Number(dayBuoc))} m (LOA + ${soMet(dayBuoc)} m dây buộc).`)
  if (tau.monNuoc != null && muoi(ben.doSau) < muoi(tau.monNuoc) + muoi(chanAnToan))
    loi.push(`${ben.tenBen} không đủ sâu: cần ≥ ${soMet(Number(tau.monNuoc) + Number(chanAnToan))} m (mớn nước + ${soMet(chanAnToan)} m).`)
  return loi
}

/**
 * Form them/sua dang HOP THOAI nhung van GIU dia chi cua ban MVC:
 *   /admin/vessels            -> danh sach, hop thoai dong
 *   /admin/vessels/form       -> mo san hop thoai them moi       (dangSua = 0)
 *   /admin/vessels/form/5     -> mo san hop thoai sua tau #5      (dangSua = 5)
 *   /admin/vessels/form?id=5  -> link cu dang tham so cung mo dung
 * Hai tuyen dung CUNG mot component lazy nen doi qua lai khong dung lai trang (danh sach khong tai lai).
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

  // Tham chieu moi nhat cho ham dong (goi trong effect / promise ma khong phai khai bao lai)
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
    // Mo tu danh sach -> quay lai muc lich su truoc (Back); vao thang bang link -> thay bang danh sach
    if (vt.state?.tuDanhSach) dh(-1)
    else dh(duongGoc + tk, { replace: true })
  }, [duongGoc])

  return { dangSua, moForm, dongForm }
}
