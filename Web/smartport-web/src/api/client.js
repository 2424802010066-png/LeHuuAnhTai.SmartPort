// =====================================================================
// LOP GOI API: moi man hinh React chi goi du lieu qua day.
// Token JWT nhan khi dang nhap duoc gan vao header Authorization cua MOI request.
// Dia chi API mac dinh la TUONG DOI ("/api/..."): khi deploy, API phuc vu luon giao dien
// (cung mot ten mien); khi dev, Vite chuyen tiep /api sang SmartPort.Api (vite.config.js).
//
// CHAY KHI MAT MANG (tram cong):
//   - Moi yeu cau co THOI GIAN CHO (timeout): Wi-Fi chap chon / may chu ngu / tab thu 7 cho ket noi SSE khong
//     lam man hinh treo mai — het gio -> LoiApi(0, quaGio) = loi MANG.
//   - Phan biet loi MANG (chua toi may chu, qua gio, 502/503/504 -> duoc xep hang gui lai) voi loi NGHIEP VU
//     (4xx: may chu da tu choi -> hien do, KHONG xep hang): laLoiMang / laLoiNghiepVu / phanLoaiLoi.
//   - Phat su kien 'smartport:ket-noi' {ok} de nhip suc khoe biet ngay khi vua mat / co lai ket noi.
// =====================================================================
import { goiChongGuiLap, taoBoKhoaGuiLap } from '../ngoaiTuyen/khoaGuiLap.js'
import { LoiApi, laLoiMang, laLoiNghiepVu, phanLoaiLoi } from '../ngoaiTuyen/phanLoaiLoi.js'

export { LoiApi, laLoiMang, laLoiNghiepVu, phanLoaiLoi }
// Bieu mau tao du lieu: const [khoaGui] = useState(taoBoKhoaGuiLap); goiApi(duong, { method: 'POST', body, chongGuiLap: khoaGui })
export { taoBoKhoaGuiLap }

export const API_URL = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

const KHOA_PHIEN = 'smartport.phien'

export function docPhien() {
  try {
    return JSON.parse(localStorage.getItem(KHOA_PHIEN))
  } catch {
    return null
  }
}

export function luuPhien(phien) {
  try {
    localStorage.setItem(KHOA_PHIEN, JSON.stringify(phien))
  } catch {
    /* trinh duyet chan localStorage: van dung duoc trong phien hien tai */
  }
}

export function xoaPhien() {
  try {
    localStorage.removeItem(KHOA_PHIEN)
  } catch {
    /* bo qua */
  }
}

function thongBaoLoi(trangThai, duLieu) {
  if (duLieu?.thongBao) return duLieu.thongBao
  if (duLieu?.errors) {
    const dau = Object.values(duLieu.errors)[0]
    if (Array.isArray(dau) && dau[0]) return dau[0]
  }
  if (trangThai === 401) return 'Phiên đăng nhập đã hết hạn — vui lòng đăng nhập lại.'
  if (trangThai === 403) return 'Tài khoản không có quyền dùng chức năng này.'
  if (trangThai === 404) return 'Không tìm thấy dữ liệu.'
  if (trangThai === 413) return 'File quá lớn — tối đa 200 MB.'
  if (trangThai === 429) return 'Thử quá nhiều lần — đợi 1 phút rồi thử lại.'
  if (trangThai === 502 || trangThai === 503 || trangThai === 504) return 'Dịch vụ máy chủ tạm thời không trả lời (' + trangThai + ').'
  return 'Máy chủ báo lỗi ' + trangThai + '.'
}

// Thoi gian cho mac dinh: lon hon timeout tuyen cua gateway (30 s) -> binh thuong gateway tra 504 truoc;
// chi chan truong hop treo han (may chu ngu, het 6 ket noi HTTP/1.1...). Man hinh can nhanh (chot cong) tu truyen timeoutMs.
const CHO_MAC_DINH_MS = 45000
const CHO_CHAY_LAU_MS = 5 * 60000 // AI / tro ly / toi uu: co the chay vai phut
const CHO_TAI_LEN_MS = 10 * 60000 // tai video / file len
const DUONG_CHAY_LAU = /^\/api\/(ai|ai-[a-z-]+|tro-ly|phan-ben-ai|camera-ai|cong-khai\/chatbot|du-bao|toi-uu|mo-phong)(\/|\?|$)/i

function chonThoiGianCho(duong, body, timeoutMs) {
  if (timeoutMs !== undefined) return timeoutMs // null / 0 = khong gioi han
  if (body instanceof FormData || body instanceof Blob) return CHO_TAI_LEN_MS
  if (DUONG_CHAY_LAU.test(duong)) return CHO_CHAY_LAU_MS
  return CHO_MAC_DINH_MS
}

let ketNoiCuoi = null
/** Bao nhip suc khoe: ok=false moi lan (de kiem lai ngay), ok=true chi khi vua doi trang thai */
function baoKetNoi(ok) {
  if (typeof window === 'undefined' || typeof CustomEvent === 'undefined') return
  if (ok && ketNoiCuoi === true) return
  ketNoiCuoi = ok
  try {
    window.dispatchEvent(new CustomEvent('smartport:ket-noi', { detail: { ok } }))
  } catch {
    /* bo qua */
  }
}

/**
 * Tao chuoi truy van tu object, bo qua gia tri rong:
 *   taoQuery({ q: 'MSCU', trang: 2, loc: '' }) -> "?q=MSCU&trang=2"
 */
export function taoQuery(thamSo = {}) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(thamSo)) {
    if (v === undefined || v === null || v === '') continue
    if (Array.isArray(v)) v.forEach((x) => p.append(k, x))
    else p.append(k, v instanceof Date ? v.toISOString() : String(v))
  }
  const s = p.toString()
  return s ? '?' + s : ''
}

function taoHeader(coToken, body) {
  const headers = { Accept: 'application/json' }
  const phien = docPhien()
  if (coToken && phien?.token) headers.Authorization = `Bearer ${phien.token}`
  // FormData (tai file len): de trinh duyet tu dat Content-Type kem boundary
  // Blob/File (tai THANG 1 tep, vd video cho AI): than = noi dung tep, Content-Type = loai tep
  if (body instanceof Blob) headers['Content-Type'] = body.type || 'application/octet-stream'
  else if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'
  return { headers, phien }
}

const guiNguyen = (body) => body instanceof FormData || body instanceof Blob

/**
 * Gui yeu cau -> { phanHoi, xong }. Bo hen gio van chay toi khi doc xong than (goi xong()).
 * signal cua ben goi: huy chu dong -> nem AbortError nhu cu. Het timeoutMs -> LoiApi(0, quaGio).
 */
async function guiYeuCau(duong, { method = 'GET', body, coToken = true, signal, headers: themHeader, timeoutMs, choPhep304 = false } = {}) {
  const { headers, phien } = taoHeader(coToken, body)
  Object.assign(headers, themHeader)
  const han = chonThoiGianCho(duong, body, timeoutMs)
  const dieuKhien = new AbortController()
  let quaGio = false
  let hen = null
  if (han > 0)
    hen = setTimeout(() => {
      quaGio = true
      dieuKhien.abort()
    }, han)
  const noiHuy = () => dieuKhien.abort()
  if (signal) {
    if (signal.aborted) dieuKhien.abort()
    else signal.addEventListener('abort', noiHuy, { once: true })
  }
  const xong = () => {
    if (hen) clearTimeout(hen)
    hen = null
    signal?.removeEventListener('abort', noiHuy)
  }

  let phanHoi
  try {
    phanHoi = await fetch(API_URL + duong, {
      method,
      headers,
      signal: dieuKhien.signal,
      body: body === undefined ? undefined : guiNguyen(body) ? body : JSON.stringify(body),
    })
  } catch (e) {
    xong()
    if (signal?.aborted) throw e?.name === 'AbortError' ? e : new DOMException('Đã hủy', 'AbortError')
    baoKetNoi(false)
    if (quaGio)
      throw new LoiApi(0, 'Máy chủ SmartPort không trả lời sau ' + Math.round(han / 1000) + ' giây (mạng chập chờn hoặc máy chủ đang bận).', null, {
        quaGio: true,
      })
    throw new LoiApi(0, 'Không kết nối được máy chủ SmartPort. Kiểm tra mạng hoặc chạy SmartPort.Api rồi thử lại.')
  }
  baoKetNoi(true) // co phan hoi HTTP (ke ca 4xx/5xx) = mang toi duoc may chu

  if (!phanHoi.ok && !(choPhep304 && phanHoi.status === 304)) {
    const laJson = (phanHoi.headers.get('content-type') || '').includes('json')
    const duLieu = laJson ? await phanHoi.json().catch(() => null) : null
    xong()
    // Token het han / tai khoan bi khoa / mat khau da doi: bao cho AuthContext dang xuat.
    // Chi khi token vua gui VAN LA phien hien tai: yeu cau cu mang token truoc luc doi mat khau (da duoc thay
    // bang phien moi) bi 401 thi bo qua, khong dang xuat nham phien moi.
    if (phanHoi.status === 401 && coToken && phien?.token && docPhien()?.token === phien.token)
      window.dispatchEvent(new Event('smartport:het-phien'))
    throw new LoiApi(phanHoi.status, thongBaoLoi(phanHoi.status, duLieu), duLieu)
  }
  xong.nguoiGoiHuy = () => !!signal?.aborted
  return { phanHoi, xong }
}

/** Doc than phan hoi; dut ket noi giua chung (Wi-Fi rot) / het gio -> LoiApi(0) de ben goi xep hang */
async function docThan(phanHoi, xong, cach) {
  try {
    return await cach(phanHoi)
  } catch (e) {
    if (xong.nguoiGoiHuy?.()) throw e // ben goi tu huy: giu AbortError nhu cu
    if (e?.name === 'AbortError' || e instanceof TypeError) {
      baoKetNoi(false)
      throw new LoiApi(0, 'Mất kết nối khi đang nhận dữ liệu từ máy chủ.')
    }
    throw e
  } finally {
    xong()
  }
}

const docJsonHoacChu = (phanHoi) => {
  if (phanHoi.status === 204 || phanHoi.status === 304) return null
  const laJson = (phanHoi.headers.get('content-type') || '').includes('json')
  return laJson ? phanHoi.json() : phanHoi.text()
}

/**
 * Goi SmartPort.Api. Tra ve du lieu JSON (hoac null voi 204 No Content).
 * body la object -> gui JSON; body la FormData -> gui multipart (tai file); body la File/Blob -> gui nguyen tep.
 * Loi HTTP duoc nem ra thanh LoiApi co thong bao tieng Viet de hien cho nguoi dung.
 * chongGuiLap: boKhoa (taoBoKhoaGuiLap) -> gui header Idempotency-Key theo noi dung, tu thu lai 1 lan khi loi mang.
 */
export async function goiApi(duong, tuyChon = {}) {
  if (tuyChon.chongGuiLap) {
    // Gui kem Idempotency-Key cua bieu mau (xem ngoaiTuyen/khoaGuiLap.js): bam lai / tu thu lai cung noi dung = cung khoa,
    // may chu [ChongGuiLap] tra ket qua cu thay vi tao / nap lan 2; 409 = dang xu ly -> bao cho
    const { chongGuiLap, ...tc } = tuyChon
    return goiChongGuiLap(goiApi, duong, tc, { boKhoa: chongGuiLap })
  }
  const { phanHoi, xong } = await guiYeuCau(duong, tuyChon)
  return docThan(phanHoi, xong, docJsonHoacChu)
}

/**
 * Nhu goiApi nhung tra them ma trang thai + ETag + gio may chu: { trangThai, duLieu, etag, gioMayChu }.
 * choPhep304: true -> 304 Not Modified (If-None-Match) khong bi coi la loi (ban sao ngoai tuyen).
 */
export async function goiApiDayDu(duong, tuyChon = {}) {
  const { phanHoi, xong } = await guiYeuCau(duong, tuyChon)
  const duLieu = await docThan(phanHoi, xong, docJsonHoacChu)
  return { trangThai: phanHoi.status, duLieu, etag: phanHoi.headers.get('etag'), gioMayChu: phanHoi.headers.get('x-gio-may-chu') }
}

/**
 * Tai file tu API co kem token (Excel, PDF, EDI...) roi luu xuong may.
 * Khong dung the <a href> duoc vi link thuong khong gui header Authorization.
 */
export async function taiFile(duong, { tenMacDinh = 'tai-ve', method = 'GET', body, timeoutMs = CHO_CHAY_LAU_MS } = {}) {
  const { phanHoi, xong } = await guiYeuCau(duong, { method, body, timeoutMs })
  const blob = await docThan(phanHoi, xong, (p) => p.blob())
  const cd = phanHoi.headers.get('content-disposition') || ''
  const m = cd.match(/filename\*=UTF-8''([^;]+)/i) || cd.match(/filename="?([^";]+)"?/i)
  const ten = m ? decodeURIComponent(m[1]) : tenMacDinh
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = ten
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10000)
  return ten
}

/**
 * Lay file CAN DANG NHAP (anh camera IP, anh bang chung...) duoi dang Blob.
 * The <img src> khong gui duoc header Authorization -> tai bang fetch roi tao URL tam:
 *   const url = URL.createObjectURL(await taiBlob('/api/camera/3/anh'))  // nho URL.revokeObjectURL(url) khi bo
 */
export async function taiBlob(duong, { method = 'GET', body, signal, timeoutMs } = {}) {
  const { phanHoi, xong } = await guiYeuCau(duong, { method, body, signal, timeoutMs })
  return docThan(phanHoi, xong, (p) => p.blob())
}

/** Anh / file tinh do API phuc vu ("~/Uploads/..." hoac "/Uploads/...") -> dia chi dung duoc tren trang */
export function duongDanAnh(p) {
  if (!p) return null
  if (p.startsWith('http') || p.startsWith('data:') || p.startsWith('blob:')) return p
  const tuongDoi = p.startsWith('~/') ? p.slice(1) : p.startsWith('/') ? p : '/' + p
  return API_URL + tuongDoi
}
