// =====================================================================
// PHAN LOAI LOI GOI API — dung chung cho client.js va hop thu di ngoai tuyen.
// Thuan JS (khong dung window / import.meta) -> kiem thu duoc bang Node (kiem-thu/ngoai-tuyen.mjs).
//
// Vi sao phai phan loai: tram cong chi duoc XEP HANG GUI SAU khi yeu cau CHUA TOI duoc may chu
// (mat mang, qua gio, gateway bao dich vu chet 502/503/504). Loi 4xx nghia la may chu DA NGHE va TU CHOI
// (vd "KHONG CHO RA CONG: eDO het han") -> phai hien DO cho bao ve, tuyet doi khong xep hang roi "cho qua".
// =====================================================================

export class LoiApi extends Error {
  /**
   * @param {number} trangThai ma HTTP; 0 = chua toi may chu (mat mang / qua gio)
   * @param {string} thongBao thong bao tieng Viet hien cho nguoi dung
   * @param {any} chiTiet than loi JSON may chu tra (neu co)
   * @param {{ quaGio?: boolean }} tuyChon quaGio = het thoi gian cho (khong phai bi tu choi)
   */
  constructor(trangThai, thongBao, chiTiet, { quaGio = false } = {}) {
    super(thongBao)
    this.name = 'LoiApi'
    this.trangThai = trangThai
    this.chiTiet = chiTiet
    this.quaGio = quaGio
  }
}

/**
 * 502/503/504 -> gui lai sau (KHONG BAO GIO la loi nghiep vu):
 *   - gateway tra JSON { thongBao, dichVu, nguon: 'gateway', loi }: dich vu phia sau chet / qua gio -> yeu cau chua toi dich vu
 *   - 503 KHONG co nguon 'gateway' (chinh dich vu tra, vd con luot loi tam thoi) -> cung gui lai sau
 */
export const MA_MAY_CHU_TAM_THOI = [502, 503, 504]

const laSo = (x) => typeof x === 'number' && Number.isFinite(x)

/** Loi MANG: chua toi may chu (0), qua gio (0 + quaGio) hoac 502/503/504 -> DUOC xep hang gui lai */
export function laLoiMang(e) {
  if (!e || !laSo(e.trangThai)) return false
  return e.trangThai === 0 || MA_MAY_CHU_TAM_THOI.includes(e.trangThai)
}

/** Loi do GATEWAY tra (dich vu phia sau khong tra loi) — chi de hien thong bao ro hon */
export const laLoiTuGateway = (e) => laLoiMang(e) && e?.chiTiet?.nguon === 'gateway'

/** Loi NGHIEP VU: may chu da xu ly va tu choi (4xx, tru 401 het phien) -> hien do, KHONG xep hang */
export function laLoiNghiepVu(e) {
  return !!e && laSo(e.trangThai) && e.trangThai >= 400 && e.trangThai < 500 && e.trangThai !== 401
}

/**
 * 'mang' | 'qua-gio' | 'may-chu-tam' (502-504) | 'het-phien' (401) | 'nghiep-vu' (4xx) | 'may-chu' (500...) | 'huy' | 'khac'
 */
export function phanLoaiLoi(e) {
  if (!e) return 'khac'
  if (e.name === 'AbortError') return 'huy'
  if (!laSo(e.trangThai)) return 'khac'
  if (e.trangThai === 0) return e.quaGio ? 'qua-gio' : 'mang'
  if (MA_MAY_CHU_TAM_THOI.includes(e.trangThai)) return 'may-chu-tam'
  if (e.trangThai === 401) return 'het-phien'
  if (e.trangThai >= 400 && e.trangThai < 500) return 'nghiep-vu'
  return 'may-chu'
}
