// =====================================================================
// KHOA CHONG GUI LAP CHO BIEU MAU (header Idempotency-Key) — may chu co bo loc [ChongGuiLap] tren cac POST/PUT tao du lieu
// (tao lenh giao nhan, phat hanh eDO, khai hai quan, dat lich hen, cap / sua / nap the xe). Khong gui khoa thi bo loc vo dung:
// Wi-Fi rot sau khi may chu DA ghi -> nguoi dung bam lai -> 2 lenh / nap tien 2 lan.
//
// Quy tac (moi bieu mau giu 1 boKhoa, vd useState(taoBoKhoaGuiLap)):
//   - Khoa gan voi NOI DUNG gui (phuong thuc + duong dan + than): bam gui -> lay khoa cua noi dung do (chua co thi sinh moi).
//   - Bam lai / tu thu lai do loi mang voi CUNG noi dung -> dung LAI khoa (may chu tra ket qua cu + Idempotent-Replayed).
//   - Nguoi dung sua noi dung -> noi dung khac -> khoa MOI (may chu tra 422 neu cung khoa ma than khac — khong bao gio xay ra).
//   - Thanh cong -> bo khoa: lan gui sau (ke ca y het, vd 2 lich hen giong nhau co chu dich) dung khoa moi.
//   - 409 = may chu dang xu ly yeu cau truoc cung khoa -> cho 1 nhip roi hoi lai 1 lan; van 409 thi bao "dang xu ly, cho".
//   - 422 = khoa khong con dung duoc -> bo khoa (lan bam sau sinh khoa moi).
// Thuan JS (khong import.meta / window) -> kiem thu bang node kiem-thu/ngoai-tuyen.mjs.
// =====================================================================
import { LoiApi, laLoiMang } from './phanLoaiLoi.js'
import { taoMaThaoTac } from './maThaoTac.js'

export const TEN_HEADER_CHONG_GUI_LAP = 'Idempotency-Key'

const dauVet = (method, duong, body) => JSON.stringify([String(method || 'GET').toUpperCase(), String(duong || ''), body ?? null])

/**
 * Bo nho khoa cua 1 bieu mau: noi dung -> khoa, cho cac lan gui CHUA duoc xac nhan (toi da `toiDa` noi dung gan nhat:
 * sua A -> B roi quay lai A van dung khoa cua A neu A chua chac da toi may chu).
 */
export function taoBoKhoaGuiLap({ taoMa = taoMaThaoTac, toiDa = 20 } = {}) {
  const theoNoiDung = new Map()
  return {
    lay(method, duong, body) {
      const dv = dauVet(method, duong, body)
      let k = theoNoiDung.get(dv)
      if (!k) {
        k = taoMa()
        theoNoiDung.set(dv, k)
        while (theoNoiDung.size > toiDa) theoNoiDung.delete(theoNoiDung.keys().next().value)
      }
      return k
    },
    /** Da xac nhan (thanh cong) hoac khoa hong (422): bo, lan sau sinh khoa moi */
    bo(method, duong, body) {
      theoNoiDung.delete(dauVet(method, duong, body))
    },
    datLai() {
      theoNoiDung.clear()
    },
    get soKhoa() {
      return theoNoiDung.size
    },
  }
}

const nguMacDinh = (ms) => new Promise((ok) => setTimeout(ok, ms))

/**
 * Goi API ghi du lieu KEM Idempotency-Key cua boKhoa. goi = goiApi (chen vao de kiem thu).
 * Tu thu lai 1 lan (cung khoa) khi loi mang tuc thi / 502-504 / 409; qua gio (da cho ca chuc giay) thi khong tu thu,
 * bao nguoi dung bam lai (van cung khoa -> khong tao trung).
 */
export async function goiChongGuiLap(goi, duong, tuyChon = {}, { boKhoa, soLanThuLai = 1, choThuLaiMs = 1500, ngu = nguMacDinh } = {}) {
  const method = tuyChon.method || 'POST'
  if (!boKhoa) return goi(duong, tuyChon)
  const khoa = boKhoa.lay(method, duong, tuyChon.body)
  const tc = { ...tuyChon, method, headers: { ...(tuyChon.headers || {}), [TEN_HEADER_CHONG_GUI_LAP]: khoa } }
  for (let lan = 0; ; lan++) {
    try {
      const kq = await goi(duong, tc)
      boKhoa.bo(method, duong, tuyChon.body)
      return kq
    } catch (e) {
      if (e?.name === 'AbortError') throw e
      const dangXuLy = e?.trangThai === 409
      const thuDuoc = dangXuLy || (laLoiMang(e) && !e?.quaGio)
      if (thuDuoc && lan < soLanThuLai) {
        await ngu(dangXuLy ? Math.max(choThuLaiMs, 2000) : choThuLaiMs)
        continue
      }
      if (e?.trangThai === 422) boKhoa.bo(method, duong, tuyChon.body)
      if (dangXuLy)
        throw new LoiApi(409, 'Yêu cầu trước (cùng nội dung) máy chủ vẫn đang xử lý — đợi vài giây rồi bấm lại, sẽ KHÔNG tạo trùng.', e?.chiTiet)
      if (laLoiMang(e))
        throw new LoiApi(e.trangThai, (e.message || 'Không kết nối được máy chủ.') + ' Bấm lại để gửi tiếp — không tạo trùng.', e?.chiTiet, { quaGio: !!e?.quaGio })
      throw e
    }
  }
}
