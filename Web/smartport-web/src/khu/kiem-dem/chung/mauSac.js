/* ============================================================
   PHAN TICH MAU SAC tren canvas — phan AI "co dien" chay kem YOLO
   (chuyen tu tally.js: hsvHist/histSim/timThungCarton/timVatMau/soiLoiThung
    va driver.js: timThungVaGoc). Giu NGUYEN nguong va cong thuc de ket qua
    giong het ban MVC.
   ------------------------------------------------------------
   Vi sao khong dung RGB tho: cung mot chai nuoc dung duoi den vang va trong
   bong toi cho gia tri RGB rat khac nhau. Histogram HSV (bo kenh V - do sang)
   chi giu "chat mau" nen phan loai on dinh hon nhieu.
   ============================================================ */

/** Goc mau HSV cua 1 diem anh (tra {hue 0..360, sat 0..1, max 0..1}) */
function hsv(r, g, b) {
  const mx = Math.max(r, g, b)
  const mn = Math.min(r, g, b)
  const d = mx - mn
  let hue = 0
  if (d > 0) {
    if (mx === r) hue = 60 * (((g - b) / d) % 6)
    else if (mx === g) hue = 60 * ((b - r) / d + 2)
    else hue = 60 * ((r - g) / d + 4)
    if (hue < 0) hue += 360
  }
  return { hue, sat: mx > 0 ? d / mx : 0, max: mx }
}

/**
 * Dac trung HISTOGRAM HSV (8 bin Hue x 3 bin Sat = 24 chieu, BO kenh V)
 * lay o vung loi 60% giua khung de tranh nen.
 */
export function hsvHist(canvas, hop) {
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const w = Math.max(2, Math.round(hop.x2 - hop.x1))
  const h = Math.max(2, Math.round(hop.y2 - hop.y1))
  const mx = Math.round(hop.x1 + w * 0.2)
  const my = Math.round(hop.y1 + h * 0.2)
  const mw = Math.max(2, Math.round(w * 0.6))
  const mh = Math.max(2, Math.round(h * 0.6))
  const img = ctx.getImageData(mx, my, mw, mh).data
  const hist = new Array(24).fill(0)
  let n = 0
  for (let i = 0; i < img.length; i += 8) {
    const { hue, sat } = hsv(img[i] / 255, img[i + 1] / 255, img[i + 2] / 255)
    const hBin = Math.min(7, Math.floor(hue / 45))
    const sBin = sat < 0.2 ? 0 : sat < 0.55 ? 1 : 2
    hist[hBin * 3 + sBin]++
    n++
  }
  for (let k = 0; k < 24; k++) hist[k] /= n || 1
  return hist
}

/** Do tuong dong cosine giua 2 histogram [0..1] */
export function histSim(a, b) {
  let dot = 0
  let na = 0
  let nb = 0
  for (let i = 0; i < a.length; i++) {
    dot += a[i] * b[i]
    na += a[i] * a[i]
    nb += b[i] * b[i]
  }
  return dot / (Math.sqrt(na) * Math.sqrt(nb) + 1e-9)
}

/** Ti le giao tren hop (dung cho ghep vet theo doi va gop cum trung) */
export function iou(a, b) {
  const x1 = Math.max(a.x1, b.x1)
  const y1 = Math.max(a.y1, b.y1)
  const x2 = Math.min(a.x2, b.x2)
  const y2 = Math.min(a.y2, b.y2)
  const giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
  const sa = (a.x2 - a.x1) * (a.y2 - a.y1)
  const sb = (b.x2 - b.x1) * (b.y2 - b.y1)
  return giao / (sa + sb - giao + 1e-9)
}

/**
 * Chia canvas thanh luoi o vuong, danh dau o nao "dat" theo ham loc mau.
 * @returns {{grid:number[][], gw:number, gh:number, o:number}}
 */
function luoiMau(canvas, o, buoc, loc, tiLe) {
  const w = canvas.width
  const h = canvas.height
  const gw = Math.floor(w / o)
  const gh = Math.floor(h / o)
  if (gw < 1 || gh < 1) return { grid: [], gw: 0, gh: 0, o }
  const img = canvas.getContext('2d', { willReadFrequently: true }).getImageData(0, 0, w, h).data
  const grid = []
  for (let gy = 0; gy < gh; gy++) {
    grid.push(new Array(gw).fill(0))
    for (let gx = 0; gx < gw; gx++) {
      let hit = 0
      let tot = 0
      for (let y = gy * o; y < (gy + 1) * o; y += buoc) {
        for (let x = gx * o; x < (gx + 1) * o; x += buoc) {
          const i = (y * w + x) * 4
          if (loc(hsv(img[i] / 255, img[i + 1] / 255, img[i + 2] / 255))) hit++
          tot++
        }
      }
      grid[gy][gx] = tot && hit / tot > tiLe ? 1 : 0
    }
  }
  return { grid, gw, gh, o }
}

/** Gom cac o ke nhau thanh tung CUM roi (flood fill) — moi cum la 1 vat */
function gomCum(grid, gw, gh) {
  const daXet = new Set()
  const cums = []
  for (let sy = 0; sy < gh; sy++) {
    for (let sx = 0; sx < gw; sx++) {
      if (!grid[sy][sx] || daXet.has(sy + ',' + sx)) continue
      const ngan = [[sx, sy]]
      const oTrong = []
      while (ngan.length) {
        const p = ngan.pop()
        const khoa = p[1] + ',' + p[0]
        if (daXet.has(khoa)) continue
        daXet.add(khoa)
        if (p[0] < 0 || p[0] >= gw || p[1] < 0 || p[1] >= gh || !grid[p[1]][p[0]]) continue
        oTrong.push(p)
        ngan.push([p[0] + 1, p[1]], [p[0] - 1, p[1]], [p[0], p[1] + 1], [p[0], p[1] - 1])
      }
      if (oTrong.length) cums.push(oTrong)
    }
  }
  return cums
}

function bao(oTrong, o) {
  let mnX = Infinity
  let mxX = -Infinity
  let mnY = Infinity
  let mxY = -Infinity
  for (const c of oTrong) {
    mnX = Math.min(mnX, c[0])
    mxX = Math.max(mxX, c[0])
    mnY = Math.min(mnY, c[1])
    mxY = Math.max(mxY, c[1])
  }
  return { x1: mnX * o, y1: mnY * o, x2: (mxX + 1) * o, y2: (mxY + 1) * o }
}

/** Mau carton: cam-nau 15..50 do, bao hoa vua, khong qua toi/sang */
const laCarton = (c) => c.hue >= 15 && c.hue <= 50 && c.sat >= 0.15 && c.sat <= 0.8 && c.max >= 0.25 && c.max <= 0.92

/** Fallback cho QC: tim vung THUNG CARTON theo mau (khi YOLO khong bat duoc) */
export function timThungCarton(canvas) {
  const { grid, gw, gh, o } = luoiMau(canvas, 20, 5, laCarton, 0.5)
  if (!gw) return []
  let nau = 0
  for (let y = 0; y < gh; y++) for (let x = 0; x < gw; x++) nau += grid[y][x]
  if (nau < gw * gh * 0.04) return [] // khong du vung carton
  return gomCum(grid, gw, gh)
    .filter((oTrong) => oTrong.length >= gw * gh * 0.025) // cum du lon (>= 2,5% anh) moi la 1 thung
    .map((oTrong) => ({ ...bao(oTrong, o), size: oTrong.length, score: 0.9, duBi: true }))
}

/**
 * Fallback khi YOLO khong bat duoc vat tren bang chuyen: tim MOI vat co mau
 * bao hoa (khac han nen bang chuyen xam) — moi cum la 1 vat.
 */
export function timVatMau(canvas) {
  const { grid, gw, gh, o } = luoiMau(canvas, 16, 4, (c) => c.sat >= 0.3 && c.max >= 0.18 && c.max <= 0.97, 0.45)
  if (gw < 4 || gh < 4) return []
  // BAC CAU DOC: tem nhan/vach sang nam ngang hay "cat" vat thanh 2 cum ->
  // o trong co o mau phia TREN va DUOI (cach <= 2) thi lap day
  for (let by = 1; by < gh - 1; by++) {
    for (let bx = 0; bx < gw; bx++) {
      if (grid[by][bx]) continue
      const tren = grid[by - 1][bx] || (by >= 2 && grid[by - 2][bx])
      const duoi = grid[by + 1][bx] || (by + 2 < gh && grid[by + 2][bx])
      if (tren && duoi) grid[by][bx] = 1
    }
  }
  const vats = gomCum(grid, gw, gh)
    // vat du lon (>= 0,5% khung) va khong choan ca khung (<= 30%)
    .filter((oTrong) => oTrong.length >= gw * gh * 0.005 && oTrong.length <= gw * gh * 0.3)
    .map((oTrong) => ({ ...bao(oTrong, o), score: 0.85, duBi: true }))
  // GOP cum chong lan nhau (manh vo cua cung 1 vat) de khong dem trung
  for (let a = 0; a < vats.length; a++) {
    for (let b = a + 1; b < vats.length; b++) {
      const A = vats[a]
      const B = vats[b]
      const ix = Math.max(0, Math.min(A.x2, B.x2) - Math.max(A.x1, B.x1))
      const iy = Math.max(0, Math.min(A.y2, B.y2) - Math.max(A.y1, B.y1))
      const giao = ix * iy
      const nho = Math.min((A.x2 - A.x1) * (A.y2 - A.y1), (B.x2 - B.x1) * (B.y2 - B.y1))
      if (nho > 0 && giao / nho > 0.45) {
        A.x1 = Math.min(A.x1, B.x1)
        A.y1 = Math.min(A.y1, B.y1)
        A.x2 = Math.max(A.x2, B.x2)
        A.y2 = Math.max(A.y2, B.y2)
        vats.splice(b, 1)
        b--
      }
    }
  }
  return vats
}

/**
 * Soi be mat 1 thung hang: luoi 12x12 trong thung, o LOI = toi hon han muc sang
 * trung vi cua chinh thung do (vet uot/tham mau/hu hai).
 * @returns {{pct:number, rect:{x:number,y:number,w:number,h:number}|null}}
 */
export function soiLoiThung(canvas, hop) {
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const bw = Math.round(hop.x2 - hop.x1)
  const bh = Math.round(hop.y2 - hop.y1)
  if (bw < 40 || bh < 40) return { pct: 0, rect: null }
  const mx = Math.round(hop.x1 + bw * 0.04) // vung loi 92% (tranh vien/nen)
  const my = Math.round(hop.y1 + bh * 0.04)
  const mw = Math.round(bw * 0.92)
  const mh = Math.round(bh * 0.92)
  const img = ctx.getImageData(mx, my, mw, mh).data
  const G = 12
  const cw = Math.floor(mw / G)
  const ch = Math.floor(mh / G)
  if (cw < 3 || ch < 3) return { pct: 0, rect: null }
  const L = []
  for (let gy = 0; gy < G; gy++) {
    L.push([])
    for (let gx = 0; gx < G; gx++) {
      let sum = 0
      let n = 0
      for (let y = gy * ch; y < (gy + 1) * ch; y += 3) {
        for (let x = gx * cw; x < (gx + 1) * cw; x += 3) {
          const i = (y * mw + x) * 4
          sum += 0.299 * img[i] + 0.587 * img[i + 1] + 0.114 * img[i + 2]
          n++
        }
      }
      L[gy][gx] = sum / (n || 1)
    }
  }
  const phang = []
  for (let a = 0; a < G; a++) for (let b = 0; b < G; b++) phang.push(L[a][b])
  phang.sort((p, q) => p - q)
  const giua = phang[Math.floor(phang.length / 2)]
  const NGUONG = giua * 0.68 // O LOI: toi hon 32% so voi trung vi
  let minX = G
  let maxX = -1
  let minY = G
  let maxY = -1
  let demLoi = 0
  for (let yy = 0; yy < G; yy++) {
    for (let xx = 0; xx < G; xx++) {
      if (L[yy][xx] >= NGUONG) continue
      const ke =
        (xx > 0 && L[yy][xx - 1] < NGUONG) ||
        (xx < G - 1 && L[yy][xx + 1] < NGUONG) ||
        (yy > 0 && L[yy - 1][xx] < NGUONG) ||
        (yy < G - 1 && L[yy + 1][xx] < NGUONG)
      if (!ke) continue // can cum >= 2 o ke — loai nhieu le
      demLoi++
      minX = Math.min(minX, xx)
      maxX = Math.max(maxX, xx)
      minY = Math.min(minY, yy)
      maxY = Math.max(maxY, yy)
    }
  }
  return {
    pct: Math.round((demLoi * 100) / (G * G)),
    rect:
      demLoi >= 2
        ? { x: mx + minX * cw, y: my + minY * ch, w: (maxX - minX + 1) * cw, h: (maxY - minY + 1) * ch }
        : null,
  }
}

/**
 * Khoang hang: tim cum mau CARTON roi tinh goc truc chinh (PCA) cua tung cum.
 * nghieng = do lech khoi phuong ngang HOAC doc (thung dung / thung nam deu OK).
 */
export function timThungVaGoc(canvas) {
  const { grid, gw, gh, o } = luoiMau(
    canvas,
    14,
    4,
    (c) => c.hue >= 15 && c.hue <= 52 && c.sat >= 0.18 && c.sat <= 0.85 && c.max >= 0.22 && c.max <= 0.95,
    0.45,
  )
  if (gw < 4 || gh < 4) return []
  const thungs = []
  for (const oTrong of gomCum(grid, gw, gh)) {
    if (oTrong.length < gw * gh * 0.012) continue // cum qua nho
    if (oTrong.length > gw * gh * 0.38) continue // cum qua lon = NEN (san/tuong), khong phai kien hang
    let mx = 0
    let my = 0
    for (const c of oTrong) {
      mx += c[0]
      my += c[1]
    }
    mx /= oTrong.length
    my /= oTrong.length
    let cxx = 0
    let cyy = 0
    let cxy = 0
    for (const c of oTrong) {
      const dx = c[0] - mx
      const dy = c[1] - my
      cxx += dx * dx
      cyy += dy * dy
      cxy += dx * dy
    }
    const goc = 0.5 * Math.atan2(2 * cxy, cxx - cyy) * (180 / Math.PI) // -90..90
    thungs.push({
      ...bao(oTrong, o),
      goc: Math.round(Math.abs(goc)),
      nghieng: Math.round(Math.min(Math.abs(goc), 90 - Math.abs(goc))),
      size: oTrong.length,
    })
  }
  return thungs
}
