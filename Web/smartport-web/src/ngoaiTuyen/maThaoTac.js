// =====================================================================
// MA THAO TAC (khoa chong gui lap, hop dong H1) + MA THIET BI
// - crypto.randomUUID CHI co trong "secure context" (HTTPS / localhost). Tram cong mo bang http://<IP-LAN>:5100
//   thi khong co -> tu dung UUID v4 tu crypto.getRandomValues (co o MOI ngu canh, ke ca HTTP).
// - Math.random chi la duong lui cuoi cung cho trinh duyet qua cu (khong co Web Crypto).
// =====================================================================

function hex(b) {
  return (b + 0x100).toString(16).slice(1)
}

/** UUID v4 tu 16 byte ngau nhien (RFC 4122: byte 6 = 0100xxxx, byte 8 = 10xxxxxx) */
export function uuidTuByte(bytes) {
  const b = Array.from(bytes)
  b[6] = (b[6] & 0x0f) | 0x40
  b[8] = (b[8] & 0x3f) | 0x80
  const h = b.map(hex)
  return h.slice(0, 4).join('') + '-' + h.slice(4, 6).join('') + '-' + h.slice(6, 8).join('') + '-' + h.slice(8, 10).join('') + '-' + h.slice(10).join('')
}

/**
 * Sinh maThaoTac (≤ 64 ky tu, hop dong H1). Truyen `c` de kiem thu (gia lap moi truong khong co randomUUID).
 */
export function taoMaThaoTac(c = globalThis.crypto) {
  if (c && typeof c.randomUUID === 'function') {
    try {
      return c.randomUUID()
    } catch {
      /* mot so trinh duyet nem loi ngoai secure context -> xuong duoi */
    }
  }
  if (c && typeof c.getRandomValues === 'function') return uuidTuByte(c.getRandomValues(new Uint8Array(16)))
  const b = new Uint8Array(16)
  for (let i = 0; i < 16; i++) b[i] = Math.floor(Math.random() * 256)
  return uuidTuByte(b)
}

const KHOA_MA_THIET_BI = 'sp-ma-thiet-bi'

/** Ma thiet bi on dinh cho moi trinh duyet (luu localStorage); khong doc duoc thi sinh tam cho phien nay */
export function layMaThietBi(luuTru) {
  try {
    const co = luuTru?.getItem(KHOA_MA_THIET_BI)
    if (co) return co
    const moi = 'tram-cong-' + taoMaThaoTac().replace(/-/g, '').slice(0, 8)
    luuTru?.setItem(KHOA_MA_THIET_BI, moi)
    return moi
  } catch {
    return 'tram-cong-' + taoMaThaoTac().replace(/-/g, '').slice(0, 8)
  }
}
