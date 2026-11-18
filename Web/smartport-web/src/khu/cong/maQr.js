// =====================================================================
// SINH MA QR (ISO/IEC 18004) ngay tren trinh duyet — dung cho ban in lenh giao nhan.
// Ban MVC khong co thu vien SINH QR (Scripts/vendor/qr/jsQR.js chi DOC QR), khong cai npm moi
// -> tu cai dat thuat toan chuan: che do byte (UTF-8), phien ban 1..40, sua loi Reed-Solomon,
// chon mat na theo diem phat. Tra ve ma tran o vuong; ve bang <svg> trong MaQr.jsx.
// =====================================================================

// Muc sua loi: thu tu trong bang = L, M, Q, H; ma 2 bit ghi vao vung dinh dang
const MUC = { L: { i: 0, bit: 1 }, M: { i: 1, bit: 0 }, Q: { i: 2, bit: 3 }, H: { i: 3, bit: 2 } }

// So byte sua loi moi khoi, theo muc (hang) va phien ban (cot, cot 0 bo trong)
const ECC_MOI_KHOI = [
  [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
  [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
  [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
  [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
]

// So khoi sua loi
const SO_KHOI = [
  [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
  [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
  [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
  [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
]

const bit = (x, i) => ((x >>> i) & 1) !== 0

/** So o du lieu (khong tinh o chuc nang) cua mot phien ban */
function soORaw(ver) {
  let kq = (16 * ver + 128) * ver + 64
  if (ver >= 2) {
    const soCanh = Math.floor(ver / 7) + 2
    kq -= (25 * soCanh - 10) * soCanh - 55
    if (ver >= 7) kq -= 36
  }
  return kq
}

const soByteDuLieu = (ver, m) => Math.floor(soORaw(ver) / 8) - ECC_MOI_KHOI[m][ver] * SO_KHOI[m][ver]

/* ---------- Truong Galois GF(2^8) + Reed-Solomon ---------- */
function nhanGF(x, y) {
  let z = 0
  for (let i = 7; i >= 0; i--) {
    z = (z << 1) ^ ((z >>> 7) * 0x11d)
    z ^= ((y >>> i) & 1) * x
  }
  return z
}

function daThucSinh(bac) {
  const kq = new Array(bac).fill(0)
  kq[bac - 1] = 1
  let goc = 1
  for (let i = 0; i < bac; i++) {
    for (let j = 0; j < kq.length; j++) {
      kq[j] = nhanGF(kq[j], goc)
      if (j + 1 < kq.length) kq[j] ^= kq[j + 1]
    }
    goc = nhanGF(goc, 0x02)
  }
  return kq
}

function phanDu(duLieu, sinh) {
  const kq = sinh.map(() => 0)
  for (const b of duLieu) {
    const heSo = b ^ kq.shift()
    kq.push(0)
    sinh.forEach((h, i) => {
      kq[i] ^= nhanGF(h, heSo)
    })
  }
  return kq
}

/* ---------- Ma tran ---------- */
function taoMaTran(ver, m, byteMa) {
  const co = ver * 4 + 17
  const o = Array.from({ length: co }, () => new Array(co).fill(false))
  const chucNang = Array.from({ length: co }, () => new Array(co).fill(false))
  const dat = (x, y, den) => {
    o[y][x] = den
    chucNang[y][x] = true
  }

  // Van thoi gian
  for (let i = 0; i < co; i++) {
    dat(6, i, i % 2 === 0)
    dat(i, 6, i % 2 === 0)
  }
  // 3 mat dinh vi
  const matDinhVi = (cx, cy) => {
    for (let dy = -4; dy <= 4; dy++)
      for (let dx = -4; dx <= 4; dx++) {
        const d = Math.max(Math.abs(dx), Math.abs(dy))
        const x = cx + dx
        const y = cy + dy
        if (x >= 0 && x < co && y >= 0 && y < co) dat(x, y, d !== 2 && d !== 4)
      }
  }
  matDinhVi(3, 3)
  matDinhVi(co - 4, 3)
  matDinhVi(3, co - 4)
  // Mau can chinh
  if (ver > 1) {
    const soCanh = Math.floor(ver / 7) + 2
    const buoc = Math.floor((ver * 8 + soCanh * 3 + 5) / (soCanh * 4 - 4)) * 2
    const viTri = [6]
    for (let p = co - 7; viTri.length < soCanh; p -= buoc) viTri.splice(1, 0, p)
    for (let i = 0; i < soCanh; i++)
      for (let j = 0; j < soCanh; j++) {
        if ((i === 0 && j === 0) || (i === 0 && j === soCanh - 1) || (i === soCanh - 1 && j === 0)) continue
        for (let dy = -2; dy <= 2; dy++)
          for (let dx = -2; dx <= 2; dx++) dat(viTri[i] + dx, viTri[j] + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1)
      }
  }

  const veDinhDang = (mask) => {
    const duLieu = (MUC_BIT[m] << 3) | mask
    let du = duLieu
    for (let i = 0; i < 10; i++) du = (du << 1) ^ ((du >>> 9) * 0x537)
    const b = ((duLieu << 10) | du) ^ 0x5412
    for (let i = 0; i <= 5; i++) dat(8, i, bit(b, i))
    dat(8, 7, bit(b, 6))
    dat(8, 8, bit(b, 7))
    dat(7, 8, bit(b, 8))
    for (let i = 9; i < 15; i++) dat(14 - i, 8, bit(b, i))
    for (let i = 0; i < 8; i++) dat(co - 1 - i, 8, bit(b, i))
    for (let i = 8; i < 15; i++) dat(8, co - 15 + i, bit(b, i))
    dat(8, co - 8, true)
  }
  veDinhDang(0) // giu cho vung dinh dang truoc khi dat du lieu

  if (ver >= 7) {
    let du = ver
    for (let i = 0; i < 12; i++) du = (du << 1) ^ ((du >>> 11) * 0x1f25)
    const b = (ver << 12) | du
    for (let i = 0; i < 18; i++) {
      const a = co - 11 + (i % 3)
      const c = Math.floor(i / 3)
      dat(a, c, bit(b, i))
      dat(c, a, bit(b, i))
    }
  }

  // Dat byte theo duong zic-zac tu goc duoi phai
  let k = 0
  for (let phai = co - 1; phai >= 1; phai -= 2) {
    if (phai === 6) phai = 5
    for (let doc = 0; doc < co; doc++)
      for (let j = 0; j < 2; j++) {
        const x = phai - j
        const len = ((phai + 1) & 2) === 0
        const y = len ? co - 1 - doc : doc
        if (!chucNang[y][x] && k < byteMa.length * 8) {
          o[y][x] = bit(byteMa[k >>> 3], 7 - (k & 7))
          k++
        }
      }
  }

  const apMatNa = (mask) => {
    for (let y = 0; y < co; y++)
      for (let x = 0; x < co; x++) {
        let dao
        switch (mask) {
          case 0: dao = (x + y) % 2 === 0; break
          case 1: dao = y % 2 === 0; break
          case 2: dao = x % 3 === 0; break
          case 3: dao = (x + y) % 3 === 0; break
          case 4: dao = (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0; break
          case 5: dao = ((x * y) % 2) + ((x * y) % 3) === 0; break
          case 6: dao = (((x * y) % 2) + ((x * y) % 3)) % 2 === 0; break
          default: dao = (((x + y) % 2) + ((x * y) % 3)) % 2 === 0
        }
        if (!chucNang[y][x] && dao) o[y][x] = !o[y][x]
      }
  }

  // Chon mat na co diem phat thap nhat (de may quet doc on dinh)
  let tot = 0
  let diemTot = Infinity
  for (let mask = 0; mask < 8; mask++) {
    apMatNa(mask)
    veDinhDang(mask)
    const diem = diemPhat(o, co)
    if (diem < diemTot) {
      diemTot = diem
      tot = mask
    }
    apMatNa(mask) // XOR lan 2 = tra ve nhu cu
  }
  apMatNa(tot)
  veDinhDang(tot)
  return o
}

const MUC_BIT = [MUC.L.bit, MUC.M.bit, MUC.Q.bit, MUC.H.bit]

function diemPhat(o, co) {
  let kq = 0
  const themLichSu = (doDai, ls) => {
    if (ls[0] === 0) doDai += co
    ls.pop()
    ls.unshift(doDai)
  }
  const demMau = (ls) => {
    const n = ls[1]
    const loi = n > 0 && ls[2] === n && ls[3] === n * 3 && ls[4] === n && ls[5] === n
    return (loi && ls[0] >= n * 4 && ls[6] >= n ? 1 : 0) + (loi && ls[6] >= n * 4 && ls[0] >= n ? 1 : 0)
  }
  const ketThuc = (mauChay, doDai, ls) => {
    if (mauChay) {
      themLichSu(doDai, ls)
      doDai = 0
    }
    themLichSu(doDai + co, ls)
    return demMau(ls)
  }
  const quet = (lay) => {
    for (let a = 0; a < co; a++) {
      let mauChay = false
      let doDai = 0
      const ls = [0, 0, 0, 0, 0, 0, 0]
      for (let b = 0; b < co; b++) {
        if (lay(a, b) === mauChay) {
          doDai++
          if (doDai === 5) kq += 3
          else if (doDai > 5) kq++
        } else {
          themLichSu(doDai, ls)
          if (!mauChay) kq += demMau(ls) * 40
          mauChay = lay(a, b)
          doDai = 1
        }
      }
      kq += ketThuc(mauChay, doDai, ls) * 40
    }
  }
  quet((y, x) => o[y][x])
  quet((x, y) => o[y][x])
  let den = 0
  for (let y = 0; y < co; y++)
    for (let x = 0; x < co; x++) {
      if (o[y][x]) den++
      if (y < co - 1 && x < co - 1) {
        const c = o[y][x]
        if (c === o[y][x + 1] && c === o[y + 1][x] && c === o[y + 1][x + 1]) kq += 3
      }
    }
  const tong = co * co
  kq += (Math.ceil(Math.abs(den * 20 - tong * 10) / tong) - 1) * 10
  return kq
}

/**
 * Ma hoa chuoi thanh ma QR.
 *   const { co, o } = taoMaQr('https://.../taixe?soLenh=EG-2609-00001', 'M')
 *   o[y][x] === true: o den
 * Nem loi neu chuoi qua dai (> ~2300 byte o muc M).
 */
export function taoMaQr(chu, mucSuaLoi = 'M') {
  const m = (MUC[mucSuaLoi] || MUC.M).i
  const byteChu = Array.from(new TextEncoder().encode(String(chu ?? '')))

  let ver = 1
  for (; ver <= 40; ver++) {
    const bitDem = ver < 10 ? 8 : 16
    if (4 + bitDem + byteChu.length * 8 <= soByteDuLieu(ver, m) * 8) break
  }
  if (ver > 40) throw new Error('Nội dung quá dài để tạo mã QR.')

  // Chuoi bit: che do byte (0100) + so ky tu + du lieu + ket thuc + dem
  const bits = []
  const them = (gt, n) => {
    for (let i = n - 1; i >= 0; i--) bits.push((gt >>> i) & 1)
  }
  them(0b0100, 4)
  them(byteChu.length, ver < 10 ? 8 : 16)
  byteChu.forEach((b) => them(b, 8))
  const sucChua = soByteDuLieu(ver, m) * 8
  them(0, Math.min(4, sucChua - bits.length))
  them(0, (8 - (bits.length % 8)) % 8)
  for (let dem = 0xec; bits.length < sucChua; dem ^= 0xec ^ 0x11) them(dem, 8)
  const duLieu = []
  for (let i = 0; i < bits.length; i += 8) duLieu.push(bits.slice(i, i + 8).reduce((a, b) => (a << 1) | b, 0))

  // Chia khoi, tinh ma sua loi tung khoi, xen ke byte cac khoi
  const soKhoi = SO_KHOI[m][ver]
  const eccKhoi = ECC_MOI_KHOI[m][ver]
  const tongByte = Math.floor(soORaw(ver) / 8)
  const soKhoiNgan = soKhoi - (tongByte % soKhoi)
  const doDaiNgan = Math.floor(tongByte / soKhoi)
  const sinh = daThucSinh(eccKhoi)
  const khoi = []
  for (let i = 0, k = 0; i < soKhoi; i++) {
    const phan = duLieu.slice(k, k + doDaiNgan - eccKhoi + (i < soKhoiNgan ? 0 : 1))
    k += phan.length
    const ecc = phanDu(phan, sinh)
    if (i < soKhoiNgan) phan.push(0)
    khoi.push(phan.concat(ecc))
  }
  const byteMa = []
  for (let i = 0; i < khoi[0].length; i++)
    khoi.forEach((kh, j) => {
      if (i !== doDaiNgan - eccKhoi || j >= soKhoiNgan) byteMa.push(kh[i])
    })

  return { phienBan: ver, co: ver * 4 + 17, o: taoMaTran(ver, m, byteMa) }
}
