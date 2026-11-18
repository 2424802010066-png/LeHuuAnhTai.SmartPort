// =====================================================================
// TRAM CONG OFFLINE-FIRST — noi cac khoi thuan JS voi trinh duyet (1 ban duy nhat moi tab).
//   kho (IndexedDB) -> hopThu (hop thu di) -> boDongBo (POST /api/cong/dong-bo, khoa giua tab)
//   banSao (GET /api/cong/goi-ngoai-tuyen, ETag) · tram (nhip /suc-khoe + dieu phoi) · ghiLuotXeTaiTram / doiChieuLenhTaiTram
// Man hinh chi goi cac ham o day; logic nam o cac file thuan (kiem thu bang node kiem-thu/ngoai-tuyen.mjs).
// =====================================================================
import { API_URL, docPhien, goiApi, goiApiDayDu, laLoiMang } from '../api/client'
import { doiChieuBanSao, taoBanSao } from './banSao.js'
import { taoBoDongBo } from './dongBo.js'
import { chuyenHangDoiCu } from './hangDoiCu.js'
import { ganVuot, ghiLuotXe, taoHopThuDi, taoMucLuotXe } from './hopThuDi.js'
import { taoKhoTuDong } from './khoCucBo.js'
import { taoKhoaGiuaTab } from './khoaGiuaTab.js'
import { layMaThietBi } from './maThaoTac.js'
import { taoDichVuNgoaiTuyen } from './dichVuNgoaiTuyen.js'

function layLuuTru() {
  try {
    return typeof localStorage !== 'undefined' ? localStorage : null
  } catch {
    return null // trinh duyet chan hoan toan bo nho trang
  }
}
const luuTru = layLuuTru()

const phienConHan = () => {
  const p = docPhien()
  return !!(p?.token && new Date(p.hetHanUtc) > new Date())
}

/**
 * Nhip suc khoe: GET /suc-khoe (khong can dang nhap, khong qua service worker), cho toi da 3 giay.
 * Gateway LUON tra 200 "tốt" khi chinh no song -> co phan hoi = co mang toi may chu; dich vu dang ngung nam o
 * dichVuNgung (vd ["cong"]) -> congNgung: van "co mang" nhung tram xep hang thang thay vi doi 502/504.
 */
async function kiemSucKhoe() {
  const dk = new AbortController()
  const hen = setTimeout(() => dk.abort(), 3000)
  try {
    const r = await fetch(API_URL + '/suc-khoe', { cache: 'no-store', signal: dk.signal, headers: { Accept: 'application/json' } })
    const j = r.ok && (r.headers.get('content-type') || '').includes('json') ? await r.json().catch(() => null) : null
    const ngung = Array.isArray(j?.dichVuNgung) ? j.dichVuNgung : []
    const congNgung = ngung.some((x) => String(x?.ten ?? x).toLowerCase() === 'cong')
    return { coMang: true, mayChuOn: r.ok, congNgung }
  } catch {
    return { coMang: false }
  } finally {
    clearTimeout(hen)
  }
}

let kenh = null
function moKenh() {
  try {
    kenh = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('sp-ngoai-tuyen') : null
  } catch {
    kenh = null
  }
  return kenh
}

const kho = taoKhoTuDong({ idb: typeof indexedDB !== 'undefined' ? indexedDB : null, luuTru, khiDoiLoai: (loai) => tram?.datLoaiKho(loai) })

export const hopThu = taoHopThuDi({ kho })
export const banSao = taoBanSao({ kho, goiDayDu: goiApiDayDu })
export const maThietBi = layMaThietBi(luuTru)
const khoa = taoKhoaGiuaTab({ locks: typeof navigator !== 'undefined' ? navigator.locks : null, luuTru })
const boDongBo = taoBoDongBo({ hopThu, goi: goiApi, khoa, maThietBi })

export const tram = taoDichVuNgoaiTuyen({
  hopThu,
  boDongBo,
  banSao,
  kiemSucKhoe,
  coPhien: phienConHan,
  layMaPhien: () => docPhien()?.token || null,
  // Hang doi CU (localStorage 'sp_chotcong_hangdoi') -> hop thu di: khoa tat dinh giong may chu, trong khoa giua tab,
  // xoa tung muc sau khi da an toan (xem hangDoiCu.js)
  khiBatDau: () => chuyenHangDoiCu({ luuTru, hopThu, khoa }),
  phatThayDoi: () => {
    try {
      kenh?.postMessage({ loai: 'hop-thu-doi' })
    } catch {
      /* kenh da dong */
    }
  },
  ngheMoiTruong: ({ kiemNgay, coPhanHoi, tabKhacDoi }) => {
    const khiHien = () => {
      if (document.visibilityState === 'visible') {
        kiemNgay()
        tabKhacDoi()
      }
    }
    const khiKetNoi = (e) => (e.detail?.ok ? coPhanHoi() : kiemNgay())
    // 'online' chi la GOI Y kiem lai ngay (khong tin gia tri navigator.onLine)
    window.addEventListener('online', kiemNgay)
    window.addEventListener('smartport:ket-noi', khiKetNoi)
    document.addEventListener('visibilitychange', khiHien)
    const k = moKenh()
    if (k) k.onmessage = () => tabKhacDoi()
    return () => {
      window.removeEventListener('online', kiemNgay)
      window.removeEventListener('smartport:ket-noi', khiKetNoi)
      document.removeEventListener('visibilitychange', khiHien)
      if (k) {
        k.onmessage = null
        k.close()
      }
      if (kenh === k) kenh = null
    }
  },
})

/**
 * Doi chieu lenh cho 1 xe: co mang -> hoi may chu (nhanh, timeout ngan); mat mang -> ban sao (ghi ro gio).
 * Tra { nguon: 'may-chu'|'ban-sao'|'khong-kiem-duoc', lenh, chan, canhBao, phiTamTinh, banSaoLuc, banSaoCu }
 *   lenh (may chu): { maLenh, soLenh, loai, soCont, taiXe, hen, dungGio, daCheckIn }
 *   lenh (ban sao): { maLenh, soLenh, loaiLenh, bienSo, soCont, trangThai, hetHan }
 *   (ban sao) biCat / khongDayDu: goi bi cat ma khong thay lenh -> man hinh KHONG duoc bao "khong co lenh"
 */
export async function doiChieuLenhTaiTram(bienSo, { huongDi, cong = 'A1', loaiXe, timeoutMs = 4000 } = {}) {
  const tt = tram.layTrangThai()
  if (tt.coMang !== false && !tt.congNgung) {
    try {
      const lenh = await goiApi('/api/cong/doi-chieu-lenh?bienSo=' + encodeURIComponent(bienSo), { timeoutMs })
      return { nguon: 'may-chu', lenh: lenh || null, chan: null, canhBao: [] }
    } catch (e) {
      // 4xx / 500: may chu tra loi duoc -> KHONG phai mat mang, khong dung ban sao thay may chu
      if (!laLoiMang(e)) return { nguon: 'may-chu', lenh: null, chan: null, canhBao: [], loi: e }
    }
  }
  const bs = await banSao.lay(cong).catch(() => null)
  if (!bs) return { nguon: 'khong-kiem-duoc', lenh: null, chan: null, canhBao: [] }
  return { nguon: 'ban-sao', ...banSao.doiChieu(bs, { bienSo, huongDi, loaiXe }), banSaoLuc: bs.xacNhanLuc, banSaoCu: banSao.laCu(bs) }
}

/** Kiem tuan thu tu ban sao khi phai xep hang (lenh may chu da biet hoac chua) -> ly do chan | null */
async function kiemBanSaoTruocXepHang(muc, lenhBiet) {
  const bs = await banSao.lay(muc.cong).catch(() => null)
  if (!bs) return null
  return doiChieuBanSao(bs, { bienSo: muc.bienSo, huongDi: muc.huongDi, loaiXe: muc.loaiXe, lenhBiet, bayGio: Date.now() }).chan
}

/**
 * Ghi luot xe tai tram (xem ghiLuotXe trong hopThuDi.js).
 * vuotBanSao = true: bao ve da xac nhan "VAN CHO QUA" -> khong kiem ban sao nua; muc mang vuotBanSao + lyDoVuot
 *   (may chu LUON gan can doi soat kem ly do).
 * mucCu: gui lai DUNG muc da tao (cung maThaoTac) — nut "Thu lai" / "Van cho qua" sau 'chua-xac-nhan'.
 * xepHangLuon: KHONG gui truc tiep, xep hang ngay (nut "Van cho qua" khi may chu khong tra loi kip).
 *
 * XE RA khi tram tin la TOI DUOC may chu (nhip suc khoe co mang, Cong khong ngung; coMang null = dang kiem lan dau cung
 * tinh la toi duoc): canXacNhan -> qua gio / loi mang / 502-504 / 409 / 401 tra 'chua-xac-nhan' (DO, chua cho ra) thay vi
 * tu xep hang. XE VAO, hoac nhip DA bao mat mang / Cong ngung: giu hanh vi cu (xep hang + doi chieu ban sao).
 */
export async function ghiLuotXeTaiTram(
  thongTin,
  { phu = null, lenhBiet = null, vuotBanSao = false, lyDoVuot = null, mucCu = null, xepHangLuon = false, timeoutMs = 10000 } = {},
) {
  let muc = mucCu ? { ...mucCu } : taoMucLuotXe(thongTin)
  if (vuotBanSao) muc = ganVuot(muc, lyDoVuot || muc.lyDoVuot)
  const tt = tram.layTrangThai()
  // mat mang HOAC dich vu Cong dang ngung -> ghi tam ngay, khong bat bao ve doi 502/504
  const toiDuoc = tt.coMang !== false && !tt.congNgung
  const guiTrucTiep = toiDuoc && !xepHangLuon
  const kq = await ghiLuotXe({
    hopThu,
    goi: goiApi,
    muc,
    phu,
    guiTrucTiep,
    canXacNhan: guiTrucTiep && muc.huongDi === 'Ra',
    timeoutMs,
    truocKhiXepHang: vuotBanSao ? null : (m) => kiemBanSaoTruocXepHang(m, lenhBiet),
  })
  if (kq.ketQua !== 'da-ghi' && kq.ketQua !== 'chua-xac-nhan') tram.daThayDoi()
  return { ...kq, muc: kq.muc || muc } // 422 -> ghiLuotXe da doi sang khoa moi
}

/** Dang xuat (AuthContext): xoa ban sao lenh / eDO / the xe tren may; GIU hop thu di (luot xe da qua that) */
export function xoaDuLieuNhayCamKhiDangXuat() {
  return tram.xoaBanSao().catch(() => {})
}
