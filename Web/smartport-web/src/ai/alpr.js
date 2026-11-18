/* ============================================================
   SMARTGATE ALPR — nhan dien BIEN SO XE tai cong cang
   (chuyen tu Scripts/alpr.js cua ban MVC sang module ES cho React;
   thuat toan giu NGUYEN de ket qua hai giao dien giong nhau)
   ------------------------------------------------------------
   Pipeline AI 3 tang, chay 100% tren trinh duyet:
     1. YOLO26n (ONNX Runtime)    : phat hien XE trong khung hinh
     2. CCT-S v2 (fast-plate-ocr) : model CHUYEN DOC BIEN SO
     3. Tesseract.js (LSTM OCR)   : engine doc lap de DOI CHIEU + du phong
   Regex bien so VN loc & chuan hoa (15C-274.84 ...). Hai engine doc
   RIENG RE — trung nhau moi bao "2 engine khop".
   ============================================================ */
import { AI_MODELS, AI_VENDOR, taiScript, taoWorkerTesseract } from './taiThuVien'

const INPUT = 640
// Nhom lop phuong tien trong COCO: 2=car 5=bus 7=truck
const VEHICLE_CLS = { 2: 'Ô tô', 5: 'Xe khách', 7: 'Đầu kéo container' }
const BIEN_ALPHABET = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_'
const BIEN_SLOTS = 10
const BIEN_W = 128
const BIEN_H = 64

/* ---------------- 1. Khoi dong 3 engine (1 lan cho ca phien lam viec) ---------------- */
let huaKhoiDong = null

export function khoiDongAlpr() {
  if (!huaKhoiDong) {
    huaKhoiDong = (async () => {
      const e = { yolo: null, cct: null, worker: null }
      const pOrt = taiScript(AI_VENDOR + 'ort/ort.min.js').then(() => {
        window.ort.env.wasm.wasmPaths = AI_VENDOR + 'ort/'
      })
      const moHinh = (ten) => pOrt.then(() => window.ort.InferenceSession.create(AI_MODELS + ten, { executionProviders: ['wasm'] }))
      // Cho TAT CA engine "an dinh" roi moi ket luan: 1 engine hong khong duoc
      // lam hong ca he thong — chi can CCT hoac Tesseract la doc duoc bien.
      await Promise.allSettled([
        moHinh('yolo26n.onnx').then((s) => (e.yolo = s)),
        moHinh('cct_s_v2_global.onnx').then((s) => (e.cct = s)),
        taoWorkerTesseract({ tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-', preserve_interword_spaces: '0' }).then(
          (w) => (e.worker = w),
        ),
      ])
      if (!e.worker && !e.cct) {
        huaKhoiDong = null
        throw new Error('Không tải được engine đọc biển số (CCT-S v2 và Tesseract đều lỗi).')
      }
      return e
    })()
  }
  return huaKhoiDong
}

/* ---------------- 1b. CCT-S v2 — model chuyen doc bien so ----------------
   Vao: anh RGB 128x64 (uint8). Ra: 10 slot x 37 ky tu '0-9A-Z_' kem xac suat. */
async function docBienCCT(e, srcCanvas, rect, gheHang) {
  if (!e.cct) return null
  try {
    const c = document.createElement('canvas')
    c.width = BIEN_W
    c.height = BIEN_H
    const cx = c.getContext('2d')
    const pad = 0.05
    const sx = Math.max(0, rect.x - rect.w * pad)
    const sy = Math.max(0, rect.y - rect.h * pad)
    const sw = Math.min(srcCanvas.width - sx, rect.w * (1 + 2 * pad))
    const sh = Math.min(srcCanvas.height - sy, rect.h * (1 + 2 * pad))
    if (sw < 8 || sh < 8) return null
    if (gheHang) {
      // Bien 2 hang: cat doi tren/duoi roi ghep thanh 1 hang ngang
      cx.drawImage(srcCanvas, sx, sy, sw, sh / 2, 0, 0, BIEN_W / 2, BIEN_H)
      cx.drawImage(srcCanvas, sx, sy + sh / 2, sw, sh / 2, BIEN_W / 2, 0, BIEN_W / 2, BIEN_H)
    } else {
      cx.drawImage(srcCanvas, sx, sy, sw, sh, 0, 0, BIEN_W, BIEN_H)
    }
    const d = cx.getImageData(0, 0, BIEN_W, BIEN_H).data
    const arr = new Uint8Array(BIEN_H * BIEN_W * 3)
    for (let i = 0, p = 0; i < d.length; i += 4) {
      arr[p++] = d[i]
      arr[p++] = d[i + 1]
      arr[p++] = d[i + 2]
    }
    const out = await e.cct.run({ [e.cct.inputNames[0]]: new window.ort.Tensor('uint8', arr, [1, BIEN_H, BIEN_W, 3]) })
    // v2 co 2 dau ra (chu bien + quoc gia) -> chon tensor 10x37
    let o = null
    for (const ten of e.cct.outputNames) {
      const t = out[ten]
      if (t?.dims && t.dims.reduce((a, b) => a * b, 1) === BIEN_SLOTS * BIEN_ALPHABET.length) {
        o = t
        break
      }
    }
    if (!o) return null
    let text = ''
    let sum = 0
    let dem = 0
    let minC = 1
    for (let s = 0; s < BIEN_SLOTS; s++) {
      let best = -1
      let bi = 0
      for (let a = 0; a < BIEN_ALPHABET.length; a++) {
        const v = Number(o.data[s * BIEN_ALPHABET.length + a])
        if (v > best) {
          best = v
          bi = a
        }
      }
      const ch = BIEN_ALPHABET[bi]
      if (ch !== '_') {
        text += ch
        sum += best
        dem++
        if (best < minC) minC = best
      }
    }
    return dem ? { text, conf: sum / dem, minConf: minC } : null
  } catch {
    return null
  }
}

/** Thu model chuyen dung tren TAT CA ung vien vung bien, lay ban tot nhat */
async function docBienChuyen(e, srcCanvas, ungVien, debug) {
  if (!e.cct || !ungVien.length) return null
  const thu = []
  ungVien.forEach((uv) => {
    thu.push({ rect: uv, ghe: false })
    if (uv.haiHang) thu.push({ rect: uv, ghe: true })
  })
  let best = null
  for (const t of thu) {
    const r = await docBienCCT(e, srcCanvas, t.rect, t.ghe)
    debug.lanThu.push({ ten: 'cct/' + t.rect.mau + (t.ghe ? '/ghep-hang' : ''), raw: r ? r.text : '', conf: r ? Math.round(r.conf * 100) : 0 })
    if (r && r.text.length >= 5 && r.text.length <= 10) {
      r.plate = timBienSo(r.text, t.ghe || !!t.rect.haiHang)
      const diem = r.conf + (r.plate ? 0.15 : 0) + (t.rect.mau === 'vang' ? 0.02 : 0)
      if (!best || diem > best.diem) {
        Object.assign(r, { diem, rect: t.rect, ghe: t.ghe })
        best = r
      }
    }
  }
  return best
}

/* ---------------- 2. YOLO: tim bbox XE lon nhat ---------------- */
async function detectVehicle(e, imgEl, w, h) {
  if (!e.yolo) return null
  const cv = document.createElement('canvas')
  const ctx = cv.getContext('2d')
  cv.width = INPUT
  cv.height = INPUT
  // Letterbox: giu ti le, dem xam 114 nhu luc huan luyen YOLO
  const sc = Math.min(INPUT / w, INPUT / h)
  const nw = Math.round(w * sc)
  const nh = Math.round(h * sc)
  const dx = (INPUT - nw) / 2
  const dy = (INPUT - nh) / 2
  ctx.fillStyle = '#727272'
  ctx.fillRect(0, 0, INPUT, INPUT)
  ctx.drawImage(imgEl, dx, dy, nw, nh)

  const d = ctx.getImageData(0, 0, INPUT, INPUT).data
  const arr = new Float32Array(3 * INPUT * INPUT)
  for (let i = 0, p = 0; i < d.length; i += 4, p++) {
    arr[p] = d[i] / 255
    arr[p + INPUT * INPUT] = d[i + 1] / 255
    arr[p + 2 * INPUT * INPUT] = d[i + 2] / 255
  }
  const out = await e.yolo.run({ [e.yolo.inputNames[0]]: new window.ort.Tensor('float32', arr, [1, 3, INPUT, INPUT]) })
  const o = out[e.yolo.outputNames[0]]
  let best = null
  // Output [1,300,6]: x1 y1 x2 y2 score cls (YOLO26 end-to-end, khong can NMS)
  for (let k = 0; k < o.dims[1]; k++) {
    const b = k * 6
    const score = o.data[b + 4]
    const cls = Math.round(o.data[b + 5])
    if (score < 0.35 || !VEHICLE_CLS[cls]) continue
    const x1 = (o.data[b] - dx) / sc
    const y1 = (o.data[b + 1] - dy) / sc
    const x2 = (o.data[b + 2] - dx) / sc
    const y2 = (o.data[b + 3] - dy) / sc
    const area = (x2 - x1) * (y2 - y1)
    if (!best || area > best.area)
      best = { x1: Math.max(0, x1), y1: Math.max(0, y1), x2: Math.min(w, x2), y2: Math.min(h, y2), area, score, loai: VEHICLE_CLS[cls] }
  }
  return best
}

/* ---------------- 3. Chuan hoa bien so Viet Nam ---------------- */
// "15C-274.84", "29C-468.28", "51D-12345"
const RE_PLATE = /(\d{2})[\s\-.]?([A-Z]{1,2})[\s\-.]?(\d{3})[\s.]?(\d{2})|(\d{2})[\s\-.]?([A-Z]{1,2})[\s\-.]?(\d{4,5})/

function khop(t) {
  const m = t.match(RE_PLATE)
  if (!m) return null
  if (m[1]) return m[1] + m[2] + '-' + m[3] + '.' + m[4]
  const so = m[7]
  if (so.length === 5) return m[5] + m[6] + '-' + so.substring(0, 3) + '.' + so.substring(3)
  return m[5] + m[6] + '-' + so
}

/* OCR hay nham chu <-> so vi hinh dang giong nhau. Bien so VN co cau truc CO DINH
   (2 so + 1-2 chu + 4-5 so) nen thu cac hoan doi pho bien de "cuu" lan doc gan dung. */
const THAY_SO = { O: '0', Q: '0', D: '0', I: '1', L: '1', J: '1', Z: '2', S: '5', B: '8', G: '6', T: '7', A: '4' }
const THAY_CHU = { 0: 'D', 1: 'L', 8: 'B', 5: 'S', 6: 'G', 2: 'Z' }

/* Sua nham 0 <-> O theo NGU CANH: "0" ngay sau chu cai va khong co so theo sau -> chu "O" */
export function suaOSo(s) {
  const a = s.split('')
  for (let i = 0; i < a.length; i++) {
    const truoc = a[i - 1] || ''
    const sau = a[i + 1] || ''
    if (a[i] === '0' && /[A-Z]/.test(truoc) && !/[0-9]/.test(sau)) a[i] = 'O'
    else if (a[i] === 'O' && /[0-9]/.test(truoc) && /[0-9]/.test(sau)) a[i] = '0'
  }
  return a.join('')
}

/* Bien KHONG theo chuan VN (xe nuoc ngoai, xe noi bo cang...): lay "tu" giong bien so nhat */
export function timBienTuDo(rawText) {
  const t = (rawText || '').toUpperCase().replace(/[^A-Z0-9.-]/g, ' ')
  let best = null
  let bestDiem = 0
  t.split(/\s+/)
    .filter((s) => s.length > 0)
    .forEach((s) => {
      const sach = s.replace(/[^A-Z0-9]/g, '')
      if (sach.length < 4 || sach.length > 10) return
      const soChuSo = (sach.match(/[0-9]/g) || []).length
      const soChu = (sach.match(/[A-Z]/g) || []).length
      if (soChuSo < 2) return
      const diem = soChuSo * 2 + (soChu > 0 ? 12 : 0) + (sach.length >= 6 && sach.length <= 9 ? 15 : 0)
      if (diem > bestDiem) {
        bestDiem = diem
        best = s
      }
    })
  return best ? suaOSo(best) : null
}

/* BIEN 2 DONG XE MAY: 2 so tinh + seri CHU+SO (L1, B5, X1) + 5 so -> "59-L1 540.63" (9 ky tu).
   Truoc day regex chi cho seri la CHU nen "60B523822" bi doc thanh "60B-523.82" (mat 1 ky tu).
   Chi ap dung khi: vung bien la 2 DONG (bien o to / xe tai 1 dong khong bao gio co seri chu+so), du DUNG 9 ky tu
   va ky tu thu 4 VON LA SO (khong ep) — de "51LD 123.45" (seri 2 chu) va bien 8 ky tu "51C-638.72" (xe tai 2 dong)
   van theo cach doc cu, uu tien xe tai cho cong depot. */
function khopXeMay(t) {
  const tu = [t.replace(/[^A-Z0-9]/g, '')].concat(t.split(/\s+/).map((x) => x.replace(/[^A-Z0-9]/g, '')))
  for (const s of tu) {
    if (s.length !== 9 || !/[0-9]/.test(s[3])) continue
    let out = ''
    for (let j = 0; j < 9; j++) out += j === 2 ? THAY_CHU[s[j]] || s[j] : THAY_SO[s[j]] || s[j]
    if (/^\d{2}[A-Z]\d{6}$/.test(out)) return out.slice(0, 4) + '-' + out.slice(4, 7) + '.' + out.slice(7)
  }
  return null
}

/** haiDong = vung bien 2 dong (bien vuong) — bat dang seri chu+so cua xe may truoc */
export function timBienSo(rawText, haiDong = false) {
  const t = (rawText || '').toUpperCase().replace(/[^A-Z0-9.\-\s]/g, ' ')
  if (haiDong) {
    const xm = khopXeMay(t)
    if (xm) return xm
  }
  let kq = khop(t)
  if (kq) return kq

  // Thu 1: ep TAT CA ky tu de nham thanh SO roi khop lai
  kq = khop(t.replace(/[OQDILJZSBGTA]/g, (c) => THAY_SO[c] || c))
  if (kq) return kq

  // Thu 2: quet tung "tu" — sua theo dung VI TRI (so-chu-so)
  for (const tu of t.split(/\s+/)) {
    const s = tu.replace(/[^A-Z0-9]/g, '')
    if (s.length < 6 || s.length > 9) continue
    let out = ''
    for (let j = 0; j < s.length; j++) {
      const c = s[j]
      if (j < 2) out += THAY_SO[c] || c // 2 ky tu dau = MA TINH (so)
      else if (j === 2) out += THAY_CHU[c] || c // ky tu thu 3 = SERI (chu)
      else if (j === 3 && /[A-Z]/.test(c) && /[0-9]/.test(s[j + 1] || '')) out += c
      else out += THAY_SO[c] || c
    }
    kq = khop(out)
    if (kq) return kq
  }
  return null
}

/* ---- TU DINH VI VUNG BIEN SO (nen TRANG va nen VANG) ----
   Bien vang = xe kinh doanh van tai (dau keo container) — loai xe vao cang nhieu nhat.
   Quet luoi 16px, gom cum lien ke (flood fill), cham diem theo hinh dang bien so. */
function timVungBien(srcCanvas) {
  const w = srcCanvas.width
  const h = srcCanvas.height
  if (w < 64 || h < 64) return []
  const CELL = 16
  const gw = Math.floor(w / CELL)
  const gh = Math.floor(h / CELL)
  if (gw < 3 || gh < 3) return []
  const img = srcCanvas.getContext('2d').getImageData(0, 0, w, h).data

  const luoi = { trang: [], vang: [] }
  for (let gy = 0; gy < gh; gy++) {
    luoi.trang.push(new Array(gw).fill(0))
    luoi.vang.push(new Array(gw).fill(0))
    for (let gx = 0; gx < gw; gx++) {
      let soTrang = 0
      let soVang = 0
      let tong = 0
      for (let y = gy * CELL; y < (gy + 1) * CELL; y += 4) {
        for (let x = gx * CELL; x < (gx + 1) * CELL; x += 4) {
          const i = (y * w + x) * 4
          const r = img[i]
          const g = img[i + 1]
          const b = img[i + 2]
          const mx = Math.max(r, g, b)
          const mn = Math.min(r, g, b)
          if (mn > 140 && mx - mn < 60) soTrang++
          // VANG: do & luc cao, lam thap ro ret (hue ~40–60 do)
          else if (r > 120 && g > 95 && b < g - 35 && r - b > 55 && Math.abs(r - g) < 85) soVang++
          tong++
        }
      }
      luoi.trang[gy][gx] = soTrang / tong > 0.38 ? 1 : 0
      luoi.vang[gy][gx] = soVang / tong > 0.34 ? 1 : 0
    }
  }

  const ungVien = []
  for (const mau of ['vang', 'trang']) {
    const grid = luoi[mau]
    const seen = {}
    for (let sy = 0; sy < gh; sy++) {
      for (let sx = 0; sx < gw; sx++) {
        if (!grid[sy][sx] || seen[sy + ',' + sx]) continue
        const stack = [[sx, sy]]
        let minX = sx
        let maxX = sx
        let minY = sy
        let maxY = sy
        let size = 0
        while (stack.length) {
          const [px, py] = stack.pop()
          const key = py + ',' + px
          if (seen[key]) continue
          seen[key] = 1
          if (px < 0 || px >= gw || py < 0 || py >= gh || !grid[py][px]) continue
          size++
          minX = Math.min(minX, px)
          maxX = Math.max(maxX, px)
          minY = Math.min(minY, py)
          maxY = Math.max(maxY, py)
          stack.push([px + 1, py], [px - 1, py], [px, py + 1], [px, py - 1])
        }
        const cw = maxX - minX + 1
        const ch = maxY - minY + 1
        const ar = cw / ch
        if (size < 3 || size > gw * gh * 0.3) continue
        const motHang = ar >= 1.8 && ar <= 7.5
        const haiHang = ar >= 0.95 && ar < 1.8 // bien vuong 2 hang cua xe tai
        if (!motHang && !haiHang) continue
        ungVien.push({
          diem: size + (mau === 'vang' ? 60 : 0) + (motHang ? 20 : 0),
          size,
          mau,
          haiHang,
          // Pad rong tay de KHONG cat mat ky tu manh o mep (vd so "1")
          x: Math.max(0, minX * CELL - 14),
          y: Math.max(0, minY * CELL - 10),
          w: Math.min(w, cw * CELL + 28),
          h: Math.min(h, ch * CELL + 20),
        })
      }
    }
  }
  ungVien.sort((a, b) => b.diem - a.diem)
  return ungVien.slice(0, 3)
}

/* ---- TIEN XU LY OCR: grayscale + keo gian tuong phan + lam net (+ nhi phan Otsu) ---- */
function chuanBiOCR(srcCanvas, rect, scale, nhiPhan) {
  const big = document.createElement('canvas')
  const sw = rect ? rect.w : srcCanvas.width
  const sh = rect ? rect.h : srcCanvas.height
  const sx = rect ? rect.x : 0
  const sy = rect ? rect.y : 0
  big.width = Math.round(sw * scale)
  big.height = Math.round(sh * scale)
  const bctx = big.getContext('2d')
  bctx.imageSmoothingEnabled = true
  bctx.drawImage(srcCanvas, sx, sy, sw, sh, 0, 0, big.width, big.height)
  const im = bctx.getImageData(0, 0, big.width, big.height)
  const d = im.data

  // Keo gian tuong phan theo percentile 5–95
  const hist = new Array(256).fill(0)
  for (let i = 0; i < d.length; i += 4) {
    const gr = Math.round(0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2])
    d[i] = gr
    hist[gr]++
  }
  const total = d.length / 4
  let lo = 0
  let hi = 255
  let acc = 0
  for (let t = 0; t < 256; t++) {
    acc += hist[t]
    if (acc >= total * 0.05) {
      lo = t
      break
    }
  }
  acc = 0
  for (let t = 255; t >= 0; t--) {
    acc += hist[t]
    if (acc >= total * 0.05) {
      hi = t
      break
    }
  }
  const range = Math.max(20, hi - lo)
  for (let j = 0; j < d.length; j += 4) {
    const v = Math.max(0, Math.min(255, Math.round(((d[j] - lo) * 255) / range)))
    d[j] = d[j + 1] = d[j + 2] = v
  }

  // Lam net (unsharp mask nhe) — tach ky tu dinh nhau sau khi phong to
  if (big.width * big.height < 4000000) {
    const goc = new Uint8ClampedArray(d.length)
    goc.set(d)
    const W = big.width
    const H = big.height
    for (let y = 1; y < H - 1; y++) {
      for (let x = 1; x < W - 1; x++) {
        const p0 = (y * W + x) * 4
        const xungQuanh = goc[p0 - 4] + goc[p0 + 4] + goc[(y - 1) * W * 4 + x * 4] + goc[(y + 1) * W * 4 + x * 4]
        const net = 5 * goc[p0] - xungQuanh
        const vv = Math.max(0, Math.min(255, Math.round(0.45 * net + 0.55 * goc[p0])))
        d[p0] = d[p0 + 1] = d[p0 + 2] = vv
      }
    }
  }

  if (nhiPhan) {
    // OTSU: thu moi nguong 0..255, chon nguong lam phuong sai GIUA 2 lop (chu/nen) lon nhat
    const h2 = new Array(256).fill(0)
    let n2 = 0
    for (let k = 0; k < d.length; k += 4) {
      h2[d[k]]++
      n2++
    }
    let tong = 0
    for (let t = 0; t < 256; t++) tong += t * h2[t]
    let tongB = 0
    let wB = 0
    let maxVar = -1
    let nguong = 128
    for (let t = 0; t < 256; t++) {
      wB += h2[t]
      if (wB === 0) continue
      const wF = n2 - wB
      if (wF === 0) break
      tongB += t * h2[t]
      const mB = tongB / wB
      const mF = (tong - tongB) / wF
      const giua = wB * wF * (mB - mF) * (mB - mF)
      if (giua > maxVar) {
        maxVar = giua
        nguong = t
      }
    }
    for (let m = 0; m < d.length; m += 4) {
      const bw = d[m] > nguong ? 255 : 0
      d[m] = d[m + 1] = d[m + 2] = bw
    }
  }
  bctx.putImageData(im, 0, 0)
  return big
}

/* Rut TUNG KY TU kem do tin cay tu cay ket qua Tesseract */
function layKyTu(data) {
  const ds = []
  ;(data.blocks || []).forEach((b) =>
    (b.paragraphs || []).forEach((p) =>
      (p.lines || []).forEach((l) => (l.words || []).forEach((w) => (w.symbols || []).forEach((s) => ds.push({ t: s.text, c: s.confidence || 0 })))),
    ),
  )
  return ds
}

/* LOC KY TU RAC o 2 dau (vien bien, oc vit) — ky tu rac luon co do tin cay thap */
function locKyTuRac(ds) {
  let a = ds.slice()
  while (a.length > 4 && a[0].c < 55) a.shift()
  while (a.length > 4 && a[a.length - 1].c < 55) a.pop()
  a = a.filter((s) => s.c >= 32)
  const txt = a
    .map((s) => s.t)
    .join('')
    .trim()
  const tb = a.length ? a.reduce((t, s) => t + s.c, 0) / a.length : 0
  return { text: txt, conf: tb, soKyTu: a.length }
}

/** Doc 1 vung anh voi 1 che do PSM cua Tesseract */
async function docVung(e, srcCanvas, rect, psm, scale, nhiPhan) {
  if (!e.worker) return { plate: null, conf: 0, raw: '', rect }
  try {
    const cv = chuanBiOCR(srcCanvas, rect, scale, nhiPhan)
    await e.worker.setParameters({ tessedit_pageseg_mode: psm })
    const r = await e.worker.recognize(cv.toDataURL('image/png'), {}, { text: true, blocks: true })
    const text = (r.data.text || '').trim()
    let conf = r.data.confidence || 0
    const kyTu = layKyTu(r.data)
    let sach = null
    if (kyTu.length >= 4) {
      sach = locKyTuRac(kyTu)
      if (sach.text && sach.conf > 0) conf = sach.conf
    }
    const thoRaw = sach && sach.text ? sach.text : text
    const haiDong = !!(rect && rect.haiHang)
    const plate = timBienSo(thoRaw, haiDong) || timBienSo(text, haiDong)
    for (const line of r.data.lines || []) {
      if (timBienSo(line.text)) {
        conf = line.confidence
        break
      }
    }
    return { plate, conf: Math.round(conf * 10) / 10, raw: thoRaw, rawGoc: text, rect }
  } catch {
    return { plate: null, conf: 0, raw: '', rect }
  }
}

/** Doc bien so trong vung xe: CCT-S v2 truoc, Tesseract doi chieu / du phong */
async function ocrRegion(e, srcCanvas) {
  const ungVien = timVungBien(srcCanvas)
  const debug = { srcW: srcCanvas.width, srcH: srcCanvas.height, soUngVien: ungVien.length, ungVien, lanThu: [] }
  window.__alprDebug = debug

  const heSoPhong = (uv) => Math.max(2, Math.min(6, Math.round(150 / Math.max(12, uv.h))))
  const buoc = []
  ungVien.forEach((uv) => {
    const sc = heSoPhong(uv)
    buoc.push({ rect: uv, psm: uv.haiHang ? '6' : '7', scale: sc, ten: uv.mau + (uv.haiHang ? '/2hang' : '/1hang') })
    buoc.push({ rect: uv, psm: uv.haiHang ? '7' : '6', scale: sc, ten: uv.mau + '/doi-che-do' })
    buoc.push({ rect: uv, psm: '8', scale: sc, ten: uv.mau + '/mot-tu' })
  })
  buoc.push({ rect: null, psm: '6', scale: 2, ten: 'toan-vung-xe' })

  function chamDiem(r, coRect) {
    const bien = timBienTuDo(r.raw)
    if (!bien) return { diem: coRect ? 1 : 0, bien: '' }
    const sach = bien.replace(/[^A-Z0-9]/g, '')
    const soChuSo = (sach.match(/[0-9]/g) || []).length
    const soChu = (sach.match(/[A-Z]/g) || []).length
    let diem = 0
    if (coRect) diem += 100
    if (sach.length >= 6 && sach.length <= 9) diem += 40
    if (soChuSo >= 3) diem += 25
    if (soChu >= 1 && soChu <= 3) diem += 20
    diem += Math.min(30, (r.conf || 0) * 0.3)
    return { diem, bien }
  }

  async function chuoiTesseract() {
    let tot = null
    let totDiem = -1
    for (const b of buoc) {
      const r = await docVung(e, srcCanvas, b.rect, b.psm, b.scale, false)
      debug.lanThu.push({ ten: b.ten, psm: b.psm, raw: r.raw, plate: r.plate })
      if (!r.plate) {
        const cd = chamDiem(r, !!b.rect)
        if (cd.diem > totDiem) {
          totDiem = cd.diem
          r.bienTuDo = cd.bien
          r.mau = b.rect ? b.rect.mau : null
          tot = r
        }
        continue
      }
      r.mau = b.rect ? b.rect.mau : null
      // DOI CHIEU: doc lai chinh vung do bang anh NHI PHAN Otsu
      const r2 = await docVung(e, srcCanvas, b.rect, b.psm, b.scale, true)
      debug.lanThu.push({ ten: b.ten + '/nhi-phan', psm: b.psm, raw: r2.raw, plate: r2.plate })
      if (r2.plate && r2.plate === r.plate) {
        r.dongThuan = true
        r.conf = Math.max(r.conf, r2.conf, 90)
      } else if (r2.plate && r2.conf > r.conf + 8) {
        return { ...r2, mau: r.mau, dongThuan: false, khac: r.plate }
      } else {
        r.dongThuan = false
        if (r2.plate) r.khac = r2.plate
      }
      return r
    }
    if (!tot) return { plate: null, conf: 0, raw: '', rect: null }
    // Bien khong chuan VN: van doi chieu 2 cach doc tren vung bien tot nhat
    if (tot.rect && tot.bienTuDo) {
      const uv = tot.rect
      const r2 = await docVung(e, srcCanvas, uv, uv.haiHang ? '6' : '7', heSoPhong(uv), true)
      const b2 = timBienTuDo(r2.raw)
      debug.lanThu.push({ ten: 'xac-nhan/nhi-phan', psm: '7', raw: r2.raw, plate: b2 })
      if (b2 && b2 === tot.bienTuDo) tot.dongThuan = true
      else if (b2 && r2.conf > tot.conf + 8) Object.assign(tot, { khac: tot.bienTuDo, bienTuDo: b2, conf: r2.conf, dongThuan: false })
      else if (b2) Object.assign(tot, { khac: b2, dongThuan: false })
    }
    return tot
  }

  function ketQuaTuCCT(cct, tessR) {
    const r = { plate: cct.plate, conf: Math.round(cct.conf * 100), raw: cct.text, rect: cct.rect, mau: cct.rect.mau, engine: 'cct' }
    if (tessR?.plate && tessR.plate === cct.plate) {
      r.dongThuan = true
      r.conf = Math.max(r.conf, 95)
    } else if (tessR?.plate && (tessR.conf || 0) >= 60) {
      r.dongThuan = false
      r.khac = tessR.plate
    } else {
      r.dongThuan = false
    }
    return r
  }

  // TANG 1: model chuyen doc bien so
  const cct = await docBienChuyen(e, srcCanvas, ungVien, debug)
  debug.cct = cct ? { text: cct.text, conf: Math.round(cct.conf * 100), plate: cct.plate, ghe: !!cct.ghe } : null

  if (cct && cct.plate && cct.conf >= 0.88) {
    const tessR = await docVung(e, srcCanvas, cct.rect, cct.rect.haiHang ? '6' : '7', heSoPhong(cct.rect), false)
    debug.lanThu.push({ ten: 'tess/doi-chieu-cct', raw: tessR.raw, plate: tessR.plate })
    return ketQuaTuCCT(cct, tessR)
  }

  // Model yeu / khong doc duoc -> chay day du chuoi Tesseract roi HOP NHAT
  const tessKq = await chuoiTesseract()
  if (!cct) return tessKq
  if (tessKq.plate && cct.plate) {
    if (tessKq.plate === cct.plate) {
      return { ...tessKq, dongThuan: true, conf: Math.max(tessKq.conf, Math.round(cct.conf * 100), 92), engine: 'cct+tess' }
    }
    if (cct.conf >= 0.75) return ketQuaTuCCT(cct, tessKq)
    return { ...tessKq, khac: cct.plate, dongThuan: false }
  }
  if (cct.plate && cct.conf >= 0.6) return ketQuaTuCCT(cct, tessKq)
  if (!tessKq.plate && cct.text && cct.conf >= 0.55) {
    if (!tessKq.bienTuDo || cct.conf >= 0.75) tessKq.bienTuDo = suaOSo(cct.text)
    if (!tessKq.rect) {
      tessKq.rect = cct.rect
      tessKq.mau = cct.rect.mau
    }
    if (Math.round(cct.conf * 100) > (tessKq.conf || 0)) tessKq.conf = Math.round(cct.conf * 100)
  }
  return tessKq
}

/* ---- SOI NGOAI QUAN: tim vet mop meo / rach tren than xe (heuristic muc do) ---- */
function soiNgoaiQuan(cv, veh, plateRect, rx, ry) {
  try {
    const ctx = cv.getContext('2d')
    // Vung THAN XE: bo 8% mep, bo 30% duoi (banh xe), bo 18% tren (kinh/cabin)
    const bx = veh.x1 + (veh.x2 - veh.x1) * 0.08
    const by = veh.y1 + (veh.y2 - veh.y1) * 0.18
    const bw = (veh.x2 - veh.x1) * 0.84
    const bh = (veh.y2 - veh.y1) * 0.52
    if (bw < 80 || bh < 60) return null
    const img = ctx.getImageData(Math.round(bx), Math.round(by), Math.round(bw), Math.round(bh)).data
    const G = 14
    const cw = Math.floor(bw / G)
    const ch = Math.floor(bh / G)
    if (cw < 4 || ch < 4) return null

    const L = []
    for (let gy = 0; gy < G; gy++) {
      L.push([])
      for (let gx = 0; gx < G; gx++) {
        let sum = 0
        let n = 0
        for (let y = gy * ch; y < (gy + 1) * ch; y += 3) {
          for (let x = gx * cw; x < (gx + 1) * cw; x += 3) {
            const i = (Math.round(y) * Math.round(bw) + Math.round(x)) * 4
            sum += 0.299 * img[i] + 0.587 * img[i + 1] + 0.114 * img[i + 2]
            n++
          }
        }
        L[gy].push(sum / (n || 1))
      }
    }
    const flat = L.flat().sort((p, q) => p - q)
    const med = flat[Math.floor(flat.length / 2)]
    const toi = (yy, xx) => L[yy][xx] < med * 0.58

    let demHong = 0
    let mnX = G
    let mxX = -1
    let mnY = G
    let mxY = -1
    for (let yy = 0; yy < G; yy++) {
      for (let xx = 0; xx < G; xx++) {
        if (!toi(yy, xx)) continue
        const ke = (xx > 0 && toi(yy, xx - 1)) || (xx < G - 1 && toi(yy, xx + 1)) || (yy > 0 && toi(yy - 1, xx)) || (yy < G - 1 && toi(yy + 1, xx))
        if (!ke) continue
        if (plateRect) {
          const ox = bx + xx * cw
          const oy = by + yy * ch
          const px1 = rx + plateRect.x
          const py1 = ry + plateRect.y
          if (ox + cw > px1 && ox < px1 + plateRect.w && oy + ch > py1 && oy < py1 + plateRect.h) continue
        }
        demHong++
        mnX = Math.min(mnX, xx)
        mxX = Math.max(mxX, xx)
        mnY = Math.min(mnY, yy)
        mxY = Math.max(mxY, yy)
      }
    }
    const pct = Math.round((demHong * 100) / (G * G))
    // 2..18% dien tich = vet hong cuc bo; >18% = ca mang toi (kinh/bong) -> bo qua
    if (demHong < 2 || pct > 18) return null
    return { pct, x: bx + mnX * cw, y: by + mnY * ch, w: (mxX - mnX + 1) * cw, h: (mxY - mnY + 1) * ch }
  } catch {
    return null
  }
}

function anhVungBien(crop, rect) {
  if (!rect) return crop.toDataURL('image/jpeg', 0.8)
  const pc = document.createElement('canvas')
  const ph = Math.min(4, Math.max(2, Math.round(140 / Math.max(1, rect.h))))
  pc.width = rect.w * ph
  pc.height = rect.h * ph
  const c2 = pc.getContext('2d')
  c2.imageSmoothingEnabled = true
  c2.drawImage(crop, rect.x, rect.y, rect.w, rect.h, 0, 0, pc.width, pc.height)
  return pc.toDataURL('image/jpeg', 0.9)
}

/**
 * Phan tich 1 khung hinh: ve ket qua len canvas hien thi va tra ve du lieu cho giao dien.
 * @param {HTMLImageElement} imgEl anh xe
 * @param {HTMLCanvasElement} cv canvas hien thi
 * @param {(buoc: string) => void} baoBuoc bao tien do tung tang AI
 */
export async function phanTichAnhXe(imgEl, cv, baoBuoc = () => {}) {
  const e = await khoiDongAlpr()
  const w = imgEl.naturalWidth || imgEl.width
  const h = imgEl.naturalHeight || imgEl.height
  const ctx = cv.getContext('2d')
  cv.width = w
  cv.height = h
  ctx.drawImage(imgEl, 0, 0, w, h)

  baoBuoc('YOLO26 đang tìm phương tiện…')
  const veh = await detectVehicle(e, imgEl, w, h)
  let rx = 0
  let ry = 0
  let rw = w
  let rh = h
  let loai = 'Phương tiện'
  const netVe = Math.max(3, w / 300)
  if (veh) {
    loai = veh.loai
    rx = Math.max(0, veh.x1 - 8)
    ry = Math.max(0, veh.y1 - 8)
    rw = Math.min(w - rx, veh.x2 - veh.x1 + 16)
    rh = Math.min(h - ry, veh.y2 - veh.y1 + 16)
    ctx.lineWidth = netVe
    ctx.strokeStyle = '#22d3ee'
    ctx.strokeRect(veh.x1, veh.y1, veh.x2 - veh.x1, veh.y2 - veh.y1)
    const fs = Math.max(14, Math.round(w / 50))
    ctx.font = `700 ${fs}px "Segoe UI", sans-serif`
    ctx.fillStyle = '#22d3ee'
    ctx.fillRect(veh.x1 - 1, veh.y1 - fs - 8, ctx.measureText(loai).width + 12, fs + 8)
    ctx.fillStyle = '#04222e'
    ctx.fillText(loai, veh.x1 + 5, veh.y1 - 6)
  }

  baoBuoc(e.cct ? 'CCT-S v2 (AI chuyên biển số) đang đọc…' : 'Tesseract LSTM đang đọc biển số…')
  const crop = document.createElement('canvas')
  crop.width = rw
  crop.height = rh
  crop.getContext('2d').drawImage(imgEl, rx, ry, rw, rh, 0, 0, rw, rh)
  const r = await ocrRegion(e, crop)

  if (r.rect) {
    ctx.strokeStyle = '#f5c542'
    ctx.lineWidth = netVe
    ctx.strokeRect(rx + r.rect.x, ry + r.rect.y, r.rect.w, r.rect.h)
  }

  // Soi ngoai quan chi khi doc duoc bien chuan (giong ban MVC)
  let hongHoc = null
  if (r.plate) {
    const vungSoi = veh || { x1: w * 0.05, y1: h * 0.05, x2: w * 0.95, y2: h * 0.95 }
    hongHoc = soiNgoaiQuan(cv, vungSoi, r.rect, rx, ry)
    if (hongHoc) {
      ctx.setLineDash([9, 6])
      ctx.strokeStyle = '#ff1744'
      ctx.lineWidth = Math.max(3, w / 280)
      ctx.strokeRect(hongHoc.x, hongHoc.y, hongHoc.w, hongHoc.h)
      ctx.setLineDash([])
      const nhan = '⚠ MÓP MÉO/HƯ HẠI ~' + hongHoc.pct + '%'
      ctx.font = `700 ${Math.max(13, Math.round(w / 55))}px "Segoe UI", sans-serif`
      ctx.fillStyle = '#ff1744'
      ctx.fillRect(hongHoc.x - 1, Math.max(0, hongHoc.y - 24), ctx.measureText(nhan).width + 10, 22)
      ctx.fillStyle = '#fff'
      ctx.fillText(nhan, hongHoc.x + 4, Math.max(16, hongHoc.y - 7))
    }
  }

  const goiY = r.plate || (r.bienTuDo || r.raw || '').toUpperCase().replace(/[^A-Z0-9.-]/g, '').substring(0, 15)
  return {
    bienSo: goiY,
    chuanVN: !!r.plate,
    doTinCay: r.conf > 0 ? Math.round(r.conf) : null,
    loaiXe: loai,
    coXe: !!veh,
    dongThuan: !!r.dongThuan,
    khac: r.khac || null,
    engine: r.engine || 'tess',
    mauBien: r.rect ? r.mau : null,
    ocrTho: r.raw || '',
    hongHoc,
    anhVungBien: anhVungBien(crop, r.rect),
    // Anh bang chung chup SAU khi da ve khung xe + bien + vet hong
    anhXe: cv.toDataURL('image/jpeg', 0.85),
  }
}

export async function tenEngineDaTai() {
  const e = await khoiDongAlpr()
  return { yolo: !!e.yolo, cct: !!e.cct, tesseract: !!e.worker }
}
