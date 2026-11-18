// =====================================================================
// CHUYEN HANG DOI CU (localStorage 'sp_chotcong_hangdoi' cua ChotBaoVe MVC / React cu) -> HOP THU DI.
// Muc cu: { bienSo, huongDi, cong, luc: new Date().toISOString() } — client cu gui qua POST /api/cong/dong-bo-lo
// KHONG kem khoa: may chu tu suy ra khoa (CongController.KhoaSuyRa, C#):
//   "lo-" + Convert.ToHexString(SHA256(UTF8(bienSo + "|" + huong + "|" + cong.Trim().ToUpperInvariant() + "|" + luc.UtcTicks)))
//          .Substring(0, 40).ToLowerInvariant()
//   bienSo = Trim().ToUpperInvariant(); huong = "Ra" neu dung "Ra", con lai "Vào"; UtcTicks = ms * 10000 + 621355968000000000.
// Truoc day moi tab tu chuyen, ngoai khoa giua tab, gan maThaoTac NGAU NHIEN, chi xoa localStorage khi chay het:
//   2 tab mo cung luc -> moi luot xe vao hop thu di 2 lan voi 2 khoa khac nhau -> 2 luot RA + 2 lan thu phi cong;
//   lo cu da toi may chu (dong-bo-lo) ma mat phan hoi -> gui lai bang khoa moi -> ghi kep.
// Nay: khoa TAT DINH giong het may chu (muc da tung gui qua dong-bo-lo -> /dong-bo tra "trung"), chay trong khoa giua tab,
// them-neu-chua-co, xoa TUNG muc cu ngay sau khi muc do da nam an toan trong hop thu di.
// =====================================================================
import { taoMucLuotXe } from './hopThuDi.js'

export const KHOA_HANG_DOI_CU = 'sp_chotcong_hangdoi'
const TICKS_EPOCH = 621355968000000000n // DateTime(1970-01-01).Ticks

// ---------------------------------------------------------------- SHA-256
// crypto.subtle CHI co trong secure context (HTTPS / localhost); tram cong mo bang http://<IP-LAN>:5100 thi khong co
// -> du phong SHA-256 thuan JS (FIPS 180-4), kiem doi chieu voi node:crypto trong kiem-thu/ngoai-tuyen.mjs.
const K = new Uint32Array([
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
  0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
  0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
  0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
])

/** SHA-256 thuan JS: Uint8Array -> Uint8Array(32) */
export function sha256ThuanJs(du) {
  const bitLen = du.length * 8
  const dai = (((du.length + 9 + 63) >> 6) << 6) >>> 0
  const m = new Uint8Array(dai)
  m.set(du)
  m[du.length] = 0x80
  const dv = new DataView(m.buffer)
  dv.setUint32(dai - 8, Math.floor(bitLen / 0x100000000))
  dv.setUint32(dai - 4, bitLen >>> 0)
  const h = new Uint32Array([0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19])
  const w = new Uint32Array(64)
  const r = (x, n) => (x >>> n) | (x << (32 - n))
  for (let o = 0; o < dai; o += 64) {
    for (let i = 0; i < 16; i++) w[i] = dv.getUint32(o + i * 4)
    for (let i = 16; i < 64; i++) {
      const s0 = r(w[i - 15], 7) ^ r(w[i - 15], 18) ^ (w[i - 15] >>> 3)
      const s1 = r(w[i - 2], 17) ^ r(w[i - 2], 19) ^ (w[i - 2] >>> 10)
      w[i] = (w[i - 16] + s0 + w[i - 7] + s1) >>> 0
    }
    let [a, b, c, d, e, f, g, hh] = h
    for (let i = 0; i < 64; i++) {
      const t1 = (hh + (r(e, 6) ^ r(e, 11) ^ r(e, 25)) + ((e & f) ^ (~e & g)) + K[i] + w[i]) >>> 0
      const t2 = ((r(a, 2) ^ r(a, 13) ^ r(a, 22)) + ((a & b) ^ (a & c) ^ (b & c))) >>> 0
      hh = g
      g = f
      f = e
      e = (d + t1) >>> 0
      d = c
      c = b
      b = a
      a = (t1 + t2) >>> 0
    }
    h[0] += a
    h[1] += b
    h[2] += c
    h[3] += d
    h[4] += e
    h[5] += f
    h[6] += g
    h[7] += hh
  }
  const ra = new Uint8Array(32)
  const rv = new DataView(ra.buffer)
  for (let i = 0; i < 8; i++) rv.setUint32(i * 4, h[i])
  return ra
}

const hex = (bytes) => Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')

/** SHA-256 (hex thuong) cua chuoi UTF-8: Web Crypto neu co, khong thi thuan JS. `subtle` chen vao de kiem thu. */
export async function bamSha256Hex(chuoi, { subtle = globalThis.crypto?.subtle } = {}) {
  const du = new TextEncoder().encode(String(chuoi))
  if (subtle && typeof subtle.digest === 'function') {
    try {
      return hex(new Uint8Array(await subtle.digest('SHA-256', du)))
    } catch {
      /* mot so trinh duyet nem loi ngoai secure context -> thuan JS */
    }
  }
  return hex(sha256ThuanJs(du))
}

// ---------------------------------------------------------------- khoa suy ra (giong may chu)

/** Chuoi nguon dung nhu C# KhoaSuyRa; null neu khong co gio hop le (may chu cung khong suy ra duoc) */
export function nguonKhoaSuyRa({ bienSo, huongDi, cong, luc }) {
  // 2 client cu deu luu luc = new Date().toISOString() ("...T07:41:10.123Z") -> System.Text.Json doc ra DateTime Kind=Utc,
  // UtcTicks = dung so ms nay * 10000 (toISOString luon du 3 chu so ms, khong lech lam tron).
  const ms = typeof luc === 'string' ? Date.parse(luc) : typeof luc === 'number' ? luc : NaN
  if (!Number.isFinite(ms)) return null
  const bs = String(bienSo ?? '')
    .trim()
    .toUpperCase()
  const huong = huongDi === 'Ra' ? 'Ra' : 'Vào' // "Vào" dang NFC, dung byte UTF-8 nhu chuoi C#
  const ticks = BigInt(Math.trunc(ms)) * 10000n + TICKS_EPOCH // vuot 2^53: phai dung BigInt
  return bs + '|' + huong + '|' + String(cong ?? '').trim().toUpperCase() + '|' + ticks.toString()
}

/**
 * Khoa chong gui lap TAT DINH cho 1 muc hang doi cu: 'lo-' + 40 ky tu hex (giong het may chu).
 * Muc khong co gio (khong the xay ra voi 2 client cu, ca hai luu toISOString) -> van tat dinh theo noi dung
 * ('lo-x' + bam JSON) de 2 tab chuyen cung muc khong ra 2 khoa.
 */
export async function khoaSuyRaLo(x, tuyChon) {
  const nguon = nguonKhoaSuyRa(x || {})
  if (nguon) return 'lo-' + (await bamSha256Hex(nguon, tuyChon)).slice(0, 40)
  return 'lo-x' + (await bamSha256Hex(JSON.stringify(x ?? null), tuyChon)).slice(0, 40)
}

// ---------------------------------------------------------------- chuyen

function docHangDoi(luuTru) {
  try {
    const v = JSON.parse(luuTru.getItem(KHOA_HANG_DOI_CU) || '[]')
    return Array.isArray(v) ? v : null
  } catch {
    return null // JSON hong: KHONG dong vao (khong xoa du lieu nguoi dung chua doc duoc)
  }
}

/** Bo DUNG 1 muc (so khop noi dung) khoi hang doi cu — doc lai moi lan: tab client cu (neu con mo) them muc moi khong bi mat */
function boMuc(luuTru, x) {
  const ds = docHangDoi(luuTru)
  if (!ds) return
  const dau = JSON.stringify(x)
  const i = ds.findIndex((y) => JSON.stringify(y) === dau)
  if (i < 0) return
  ds.splice(i, 1)
  if (ds.length) luuTru.setItem(KHOA_HANG_DOI_CU, JSON.stringify(ds))
  else luuTru.removeItem(KHOA_HANG_DOI_CU)
}

/**
 * Chuyen hang doi cu sang hop thu di (goi o khiBatDau cua tram, MOI tab — khoa giua tab bao dam 1 tab lam).
 * Tra { daChay, soChuyen, soDaCo } — daChay=false: tab khac dang chuyen (hoac khong co gi de chuyen).
 * Ghi hop thu di loi (day bo nho...) -> nem ra; cac muc chua chuyen VAN nam trong localStorage, lan mo sau chuyen tiep.
 */
export async function chuyenHangDoiCu({ luuTru, hopThu, khoa, tenKhoa = 'sp-chuyen-hang-doi-cu', bam = undefined }) {
  if (!luuTru) return { daChay: false, soChuyen: 0, soDaCo: 0 }
  const truoc = docHangDoi(luuTru)
  if (!truoc || truoc.length === 0) return { daChay: false, soChuyen: 0, soDaCo: 0 }
  const lam = async () => {
    let soChuyen = 0
    let soDaCo = 0
    for (const x of docHangDoi(luuTru) || []) {
      // doc lai TRONG khoa: tab truoc co the vua chuyen xong
      if (!x?.bienSo) {
        boMuc(luuTru, x) // muc rong / hong: client cu cung bo qua
        continue
      }
      const ma = await khoaSuyRaLo(x, bam)
      const gio = Date.parse(x.luc)
      const muc = taoMucLuotXe(
        { bienSo: x.bienSo, huongDi: x.huongDi, cong: x.cong, ghiChu: 'Chuyển từ hàng đợi cũ của máy trạm' },
        { dongHo: () => (Number.isFinite(gio) ? gio : Date.now()), taoMa: () => ma },
      )
      if (await hopThu.themNeuChuaCo(muc)) soChuyen++
      else soDaCo++
      boMuc(luuTru, x) // chi xoa SAU KHI muc da nam an toan trong hop thu di
    }
    return { soChuyen, soDaCo }
  }
  const r = khoa ? await khoa.chayNeuRanh(tenKhoa, lam) : { daChay: true, ketQua: await lam() }
  return r.daChay ? { daChay: true, ...r.ketQua } : { daChay: false, soChuyen: 0, soDaCo: 0 }
}
