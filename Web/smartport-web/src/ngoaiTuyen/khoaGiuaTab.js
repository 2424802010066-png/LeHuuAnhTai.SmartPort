// =====================================================================
// KHOA GIUA CAC TAB — bao dam MOT luong dong bo duy nhat tren may tram (nhieu tab cung mo SmartPort).
//   - Co navigator.locks (HTTPS / localhost): Web Locks, ifAvailable -> tab khac dang giu thi bo qua ngay.
//   - Khong co (http://<IP-LAN>: Web Locks chi co trong secure context): "hop dong thue" (lease) trong localStorage
//     { chu, nonce, het } — ghi, cho chut, doc lai: chi tab ma ban ghi con la cua minh moi duoc chay; gia han dinh ky,
//     tab chet thi lease tu het han sau thoiHanMs.
// Du 2 tab lo cung gui, may chu van chong trung theo maThaoTac -> khoa chi de bot yeu cau thua, khong phai chot an toan duy nhat.
// =====================================================================
import { taoMaThaoTac } from './maThaoTac.js'

const ngu = (ms, henGio) => new Promise((ok) => henGio(ok, ms))

export function taoKhoaGiuaTab({
  locks = null,
  luuTru = null,
  dongHo = Date.now,
  maTab = taoMaThaoTac(),
  thoiHanMs = 20000,
  choXacNhanMs = 60,
  henGio = setTimeout,
  henLap = setInterval,
  huyLap = clearInterval,
} = {}) {
  /** Chay fn neu khong tab nao dang giu khoa `ten`. Tra { daChay, ketQua } */
  async function chayNeuRanh(ten, fn) {
    if (locks && typeof locks.request === 'function') {
      return locks.request(ten, { ifAvailable: true }, async (khoa) => {
        if (!khoa) return { daChay: false }
        return { daChay: true, ketQua: await fn() }
      })
    }
    if (!luuTru) return { daChay: true, ketQua: await fn() } // khong co cach phoi hop: chay (may chu van chong trung)

    const k = 'sp-khoa:' + ten
    const doc = () => {
      try {
        return JSON.parse(luuTru.getItem(k))
      } catch {
        return null
      }
    }
    const hienTai = doc()
    if (hienTai && hienTai.het > dongHo() && hienTai.chu !== maTab) return { daChay: false }

    const cua = { chu: maTab, nonce: taoMaThaoTac(), het: dongHo() + thoiHanMs }
    try {
      luuTru.setItem(k, JSON.stringify(cua))
    } catch {
      return { daChay: true, ketQua: await fn() }
    }
    await ngu(choXacNhanMs, henGio) // 2 tab cung ghi gan nhu dong thoi: tab ghi sau cung thang, tab kia doc lai thay khong phai minh
    const lai = doc()
    if (!lai || lai.nonce !== cua.nonce) return { daChay: false }

    const giaHan = henLap(
      () => {
        const c = doc()
        if (c && c.nonce === cua.nonce) {
          try {
            luuTru.setItem(k, JSON.stringify({ ...c, het: dongHo() + thoiHanMs }))
          } catch {
            /* bo qua: het han thi tab khac tiep quan, may chu van chong trung */
          }
        }
      },
      Math.max(1000, Math.floor(thoiHanMs / 3)),
    )
    try {
      return { daChay: true, ketQua: await fn() }
    } finally {
      huyLap(giaHan)
      const c = doc()
      if (c && c.nonce === cua.nonce) {
        try {
          luuTru.removeItem(k)
        } catch {
          /* bo qua */
        }
      }
    }
  }

  return { chayNeuRanh, maTab, coWebLocks: !!(locks && typeof locks.request === 'function') }
}
