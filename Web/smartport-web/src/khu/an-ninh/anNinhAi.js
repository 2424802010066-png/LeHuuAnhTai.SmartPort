/* ============================================================
   TRAM CAMERA AN NINH AI — dong co chay 100% TREN TRINH DUYET
   (chuyen tu wwwroot/Scripts/security.js cua ban MVC sang module ES;
    thuat toan giu NGUYEN de ket qua hai giao dien giong nhau)

   1. XAC NHAN CONTAINER 2 BUOC (OCR ISO 6346: 4 chu + 7 so)
   2. GIAM SAT NGUY HIEM tren tung nhip quet:
      - LUA      : luoi 8x8, o "chay" >= 8% diem nong, phai co CUM >= 2 o
                   ke nhau + con NHAP NHAY giua cac khung (chong ao do, den xe)
      - NGUOI NGA: 4 dieu kien dong thoi + xac nhan 3 luot lien tiep
      - VAT TINH : doi tuong dung im qua nhieu khung (ghep IoU) trong khi
                   xung quanh van chuyen dong
   3. AM THANH canh bao bang Web Audio (khong can file mp3)
   ============================================================ */
import { AI_MODELS, AI_VENDOR, taiScript, taoWorkerTesseract } from '../../ai/taiThuVien'

const INPUT = 640
const THU_MUC_ORT = AI_VENDOR + 'ort/'
const TEN_BO_DEM = 'sp-models-v1'

/** Lop COCO duoc theo doi tai cang */
export const LOP_THEO_DOI = { 0: 'Người', 2: 'Ô tô', 5: 'Xe khách', 7: 'Xe tải', 28: 'Kiện hàng' }

/* ================== AM THANH (Web Audio — khong can file mp3) ================== */
let actx = null

function am() {
  if (!actx) actx = new (window.AudioContext || window.webkitAudioContext)()
  if (actx.state === 'suspended') actx.resume()
  return actx
}

/** Chuong "ting-ting" khi xac nhan DUNG */
export function chuong() {
  try {
    const a = am()
    const t = a.currentTime
    for (const [tanSo, tre] of [
      [880, 0],
      [1318.5, 0.16],
    ]) {
      const o = a.createOscillator()
      const g = a.createGain()
      o.type = 'sine'
      o.frequency.value = tanSo
      g.gain.setValueAtTime(0.0001, t + tre)
      g.gain.exponentialRampToValueAtTime(0.4, t + tre + 0.02)
      g.gain.exponentialRampToValueAtTime(0.0001, t + tre + 0.9)
      o.connect(g)
      g.connect(a.destination)
      o.start(t + tre)
      o.stop(t + tre + 1)
    }
  } catch {
    /* trinh duyet chan am thanh khi chua co tuong tac — bo qua */
  }
}

/** Chuong bao co su co MOI ve phong an ninh (3 not) */
export function chuongBao() {
  try {
    const a = am()
    const t = a.currentTime
    for (const [tanSo, tre] of [
      [988, 0],
      [784, 0.22],
      [988, 0.44],
    ]) {
      const o = a.createOscillator()
      const g = a.createGain()
      o.type = 'sine'
      o.frequency.value = tanSo
      g.gain.setValueAtTime(0.35, t + tre)
      g.gain.exponentialRampToValueAtTime(0.0001, t + tre + 0.5)
      o.connect(g)
      g.connect(a.destination)
      o.start(t + tre)
      o.stop(t + tre + 0.55)
    }
  } catch {
    /* bo qua */
  }
}

let nutCoi = null

/** Coi hu len-xuong khi NGUY HIEM (giay = tu tat sau bao nhieu giay) */
export function coiBatDau(giay) {
  coiDung()
  try {
    const a = am()
    const t = a.currentTime
    const o = a.createOscillator()
    const g = a.createGain()
    const lfo = a.createOscillator()
    const lg = a.createGain()
    o.type = 'sawtooth'
    o.frequency.value = 650
    lfo.type = 'sine'
    lfo.frequency.value = 3 // dao dong 3Hz
    lg.gain.value = 220 // bien do +-220Hz
    lfo.connect(lg)
    lg.connect(o.frequency)
    g.gain.value = 0.25
    o.connect(g)
    g.connect(a.destination)
    o.start(t)
    lfo.start(t)
    const hen = giay ? setTimeout(coiDung, giay * 1000) : null
    nutCoi = { o, lfo, hen }
  } catch {
    nutCoi = null
  }
}

export function coiDung() {
  if (!nutCoi) return
  clearTimeout(nutCoi.hen)
  try {
    nutCoi.o.stop()
    nutCoi.lfo.stop()
  } catch {
    /* da dung roi */
  }
  nutCoi = null
}

export const coiDangKeu = () => !!nutCoi

/** Beep canh bao ngan (OCR doi chieu SAI) */
export function beepCanhBao() {
  try {
    const a = am()
    const t = a.currentTime
    for (let i = 0; i < 3; i++) {
      const o = a.createOscillator()
      const g = a.createGain()
      o.type = 'square'
      o.frequency.value = 320
      g.gain.setValueAtTime(0.3, t + i * 0.25)
      g.gain.exponentialRampToValueAtTime(0.0001, t + i * 0.25 + 0.18)
      o.connect(g)
      g.connect(a.destination)
      o.start(t + i * 0.25)
      o.stop(t + i * 0.25 + 0.2)
    }
  } catch {
    /* bo qua */
  }
}

/* ================== NAP ENGINE (YOLO26 + Tesseract) ================== */
function taiMoHinh(url) {
  // Lan dau tai qua mang roi luu bo dem; cac lan sau nap tuc thi tu dia
  if (!('caches' in window)) return fetch(url).then((r) => r.arrayBuffer())
  return caches
    .open(TEN_BO_DEM)
    .then((c) =>
      c.match(url).then((hit) => {
        if (hit) return hit.arrayBuffer()
        return fetch(url).then((r) => {
          if (!r.ok) throw new Error('HTTP ' + r.status)
          return c.put(url, r.clone()).then(() => r.arrayBuffer())
        })
      }),
    )
    .catch(() => fetch(url).then((r) => r.arrayBuffer()))
}

let huaEngine = null

/**
 * Nap ONNX Runtime + YOLO26n + Tesseract (1 lan cho ca phien lam viec).
 * Tra ve { ort, yolo, worker }. Mot engine hong khong lam hong ca man hinh:
 * con YOLO thi van giam sat duoc, con Tesseract thi van doc duoc ma container.
 */
export function khoiDongAnNinh() {
  if (!huaEngine) {
    huaEngine = (async () => {
      const e = { ort: null, yolo: null, worker: null }
      const pYolo = (async () => {
        if (!window.ort?.InferenceSession) {
          const coGpu = !!navigator.gpu
          if (coGpu) {
            try {
              await taiScript(THU_MUC_ORT + 'ort.webgpu.min.js')
            } catch {
              /* khong nap duoc ban WebGPU -> dung ban CPU */
            }
          }
          if (!window.ort?.InferenceSession) await taiScript(THU_MUC_ORT + 'ort.min.js')
        }
        const ort = window.ort
        ort.env.wasm.wasmPaths = THU_MUC_ORT
        e.ort = ort
        const buf = await taiMoHinh(AI_MODELS + 'yolo26n.onnx')
        const nhaCungCap = navigator.gpu ? ['webgpu', 'wasm'] : ['wasm']
        e.yolo = await ort.InferenceSession.create(buf, { executionProviders: nhaCungCap }).catch(() =>
          ort.InferenceSession.create(buf, { executionProviders: ['wasm'] }),
        )
      })()
      const pOcr = taoWorkerTesseract({ tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ' }).then((w) => {
        e.worker = w
      })
      await Promise.allSettled([pYolo, pOcr])
      if (!e.yolo && !e.worker) {
        huaEngine = null
        throw new Error('Không tải được engine AI (YOLO26 và Tesseract đều lỗi) — kiểm tra mạng rồi tải lại trang.')
      }
      return e
    })()
  }
  return huaEngine
}

/* ================== 1. XAC NHAN CONTAINER 2 BUOC ================== */
const RE_CONT = /([A-Z]{4})\s*(\d{6})\s*(\d)/

/** Doc ma container ISO 6346 tren khung hinh hien tai; null neu chua doc duoc */
export async function docMaContainer(engine, canvas) {
  if (!engine?.worker) throw new Error('Engine OCR chưa sẵn sàng.')
  const r = await engine.worker.recognize(canvas.toDataURL('image/png'))
  const m = (r.data.text || '').toUpperCase().match(RE_CONT)
  return m ? m[1] + m[2] + m[3] : null
}

/* ================== 2. GIAM SAT NGUY HIEM ================== */
function iou(a, b) {
  const x1 = Math.max(a.x1, b.x1)
  const y1 = Math.max(a.y1, b.y1)
  const x2 = Math.min(a.x2, b.x2)
  const y2 = Math.min(a.y2, b.y2)
  const giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
  const hop = (a.x2 - a.x1) * (a.y2 - a.y1) + (b.x2 - b.x1) * (b.y2 - b.y1) - giao
  return hop > 0 ? giao / hop : 0
}

/* NGUOI NGA — cac dieu kien DONG THOI (chong bao oan nguoi NGOI gan webcam):
   1. Khung nam ngang RO RET: w/h > 1.45 (nguoi ngoi gan cam la 1.1-1.4)
   2. Vi tri THAP: tam khung o nua duoi khung hinh
   3. Khong choan man hinh: dien tich < 55%
   4. Be ngang < 55% khung hinh
   (con dieu kien thu 5 — xac nhan >= 3 luot quet lien tiep — nam trong batNhip) */
export function dauHieuNga(b, W, H) {
  const bw = b.x2 - b.x1
  const bh = Math.max(1, b.y2 - b.y1)
  const ar = bw / bh
  const yTam = (b.y1 + b.y2) / 2 / Math.max(1, H)
  const dienTich = (bw * bh) / Math.max(1, W * H)
  return ar > 1.45 && yTam > 0.52 && dienTich < 0.55 && bw / Math.max(1, W) < 0.55
}

/**
 * Bo giam sat cho MOT phien mo man hinh: giu bo dem nga, danh sach khung truoc
 * va ti le lua cua khung truoc. Roi trang thi bo luon (khong con trang thai treo).
 */
export function taoBoGiamSat(engine) {
  let tiLeLuaTruoc = null
  let khungTruoc = []
  let ngaDem = 0

  /** Phat hien LUA theo luoi 8x8 — khoanh DUNG vung chay */
  function quetLua(canvas) {
    const ctx = canvas.getContext('2d')
    const w = canvas.width
    const h = canvas.height
    if (!w || !h) return { tiLe: 0, nhapNhay: 0, khung: null, cum: 0 }
    const img = ctx.getImageData(0, 0, w, h).data
    const G = 8
    const cw = Math.floor(w / G)
    const ch = Math.floor(h / G)
    const o = []
    for (let gy = 0; gy < G; gy++) {
      o.push(new Array(G).fill(0))
      for (let gx = 0; gx < G; gx++) {
        let nong = 0
        let tong = 0
        for (let y = gy * ch; y < (gy + 1) * ch; y += 6) {
          for (let x = gx * cw; x < (gx + 1) * cw; x += 6) {
            const i = (y * w + x) * 4
            const r = img[i]
            const g = img[i + 1]
            const b = img[i + 2]
            if (r > 200 && g > 50 && g < 190 && b < 90 && r - b > 120) nong++
            tong++
          }
        }
        o[gy][gx] = tong ? nong / tong : 0
      }
    }
    // O "chay" = >= 8% diem nong; chi tinh o chay co HANG XOM chay (cum >= 2)
    let minX = G
    let maxX = -1
    let minY = G
    let maxY = -1
    let cum = 0
    let tongTiLe = 0
    for (let yy = 0; yy < G; yy++) {
      for (let xx = 0; xx < G; xx++) {
        tongTiLe += o[yy][xx]
        if (o[yy][xx] < 0.08) continue
        const keCanh =
          (xx > 0 && o[yy][xx - 1] >= 0.08) ||
          (xx < G - 1 && o[yy][xx + 1] >= 0.08) ||
          (yy > 0 && o[yy - 1][xx] >= 0.08) ||
          (yy < G - 1 && o[yy + 1][xx] >= 0.08)
        if (!keCanh) continue
        cum++
        minX = Math.min(minX, xx)
        maxX = Math.max(maxX, xx)
        minY = Math.min(minY, yy)
        maxY = Math.max(maxY, yy)
      }
    }
    const tiLe = tongTiLe / (G * G)
    const nhapNhay = tiLeLuaTruoc === null ? 0 : Math.abs(tiLe - tiLeLuaTruoc)
    tiLeLuaTruoc = tiLe
    const khung = cum >= 2 ? { x: minX * cw, y: minY * ch, w: (maxX - minX + 1) * cw, h: (maxY - minY + 1) * ch } : null
    return { tiLe, nhapNhay, khung, cum }
  }

  /** YOLO26n: phat hien doi tuong tren khung hinh (thu nho ve 640x640, padding xam) */
  async function phatHien(canvas, canvasPhu) {
    const ort = engine.ort
    const w = canvas.width
    const h = canvas.height
    const cv = canvasPhu
    const ctx = cv.getContext('2d')
    cv.width = INPUT
    cv.height = INPUT
    const sc = Math.min(INPUT / w, INPUT / h)
    const nw = Math.round(w * sc)
    const nh = Math.round(h * sc)
    const dx = (INPUT - nw) / 2
    const dy = (INPUT - nh) / 2
    ctx.fillStyle = '#727272'
    ctx.fillRect(0, 0, INPUT, INPUT)
    ctx.drawImage(canvas, dx, dy, nw, nh)
    const d = ctx.getImageData(0, 0, INPUT, INPUT).data
    const arr = new Float32Array(3 * INPUT * INPUT)
    for (let i = 0, p = 0; i < d.length; i += 4, p++) {
      arr[p] = d[i] / 255
      arr[p + INPUT * INPUT] = d[i + 1] / 255
      arr[p + 2 * INPUT * INPUT] = d[i + 2] / 255
    }
    const feeds = {}
    feeds[engine.yolo.inputNames[0]] = new ort.Tensor('float32', arr, [1, 3, INPUT, INPUT])
    const out = await engine.yolo.run(feeds)
    const o = out[engine.yolo.outputNames[0]]
    const hop = []
    for (let k = 0; k < o.dims[1]; k++) {
      const b = k * 6
      const diem = o.data[b + 4]
      const lop = Math.round(o.data[b + 5])
      if (diem < 0.4) continue
      hop.push({
        x1: (o.data[b] - dx) / sc,
        y1: (o.data[b + 1] - dy) / sc,
        x2: (o.data[b + 2] - dx) / sc,
        y2: (o.data[b + 3] - dy) / sc,
        diem,
        lop,
      })
    }
    return hop
  }

  /**
   * Mot nhip quet: tim lua + doi tuong, VE khung len canvas va tra ve
   * { canhBaos: [{loai, mucDo, moTa}], meta } cho man hinh xu ly tiep.
   */
  async function batNhip(canvas, canvasPhu) {
    const lua = quetLua(canvas)
    const hop = engine.yolo ? await phatHien(canvas, canvasPhu) : []
    const ctx = canvas.getContext('2d')
    const w = canvas.width
    const h = canvas.height
    ctx.lineWidth = Math.max(2.5, w / 320)
    ctx.font = '600 ' + Math.max(13, Math.round(w / 55)) + 'px "Segoe UI", sans-serif'

    const canhBaos = []
    // LUA: can CUM >= 2 o chay ke nhau + nhap nhay giua cac khung
    if (lua.khung && lua.nhapNhay > 0.004)
      canhBaos.push({
        loai: 'Lua',
        mucDo: 'Nguy hiểm',
        moTa: 'Phát hiện cụm lửa/nhiệt (' + lua.cum + ' vùng cháy liền kề, có nhấp nháy)',
      })

    /* PASS 1 — danh gia theo KHUNG HINH + ghep doi tuong voi khung truoc:
       · NGA: chi can MOT nguoi co dau hieu nga la bo dem tang; nguoi khac dang DUNG
         trong cung khung KHONG duoc triet tieu bo dem. Kep tran 6 de het nga thi ha nhanh.
       · VAT TINH: bo dem di theo TUNG VAT (ghep IoU khung truoc), khong theo lop. */
    let hopNga = null
    let dangDi = 0
    for (const b of hop) {
      if (!LOP_THEO_DOI[b.lop]) continue
      if (b.lop === 0 && !hopNga && dauHieuNga(b, w, h)) hopNga = b
      const cu = khungTruoc.find((p) => p.lop === b.lop && iou(p, b) > 0.85)
      b.dungIm = cu ? (cu.dungIm || 0) + 1 : 0
      if (!cu) dangDi++
    }
    if (hopNga) ngaDem = Math.min(6, ngaDem + 1)
    else if (ngaDem > 0) ngaDem--

    for (const b of hop) {
      if (!LOP_THEO_DOI[b.lop]) continue
      let mau = '#22d3ee'
      let nhan = LOP_THEO_DOI[b.lop]

      // NGUOI NGA: cac dieu kien hinh hoc + xac nhan nhieu luot quet
      if (b === hopNga) {
        if (ngaDem >= 3) {
          mau = '#ef4444'
          nhan = 'NGƯỜI NGÃ!'
          canhBaos.push({
            loai: 'NguoiNga',
            mucDo: 'Nguy hiểm',
            moTa: 'Người nằm/ngã bất động trong khung hình (xác nhận ' + ngaDem + ' lượt quét)',
          })
        } else {
          mau = '#f59e0b'
          nhan = 'Nghi ngã · xác nhận ' + ngaDem + '/3'
        }
      }

      // VAT TINH: bo dem theo tung vat
      if (b.dungIm >= 6 && dangDi > 0 && b.lop !== 0) {
        mau = '#f59e0b'
        nhan += ' · ĐỨNG IM'
        canhBaos.push({
          loai: 'VatTinh',
          mucDo: 'Chú ý',
          moTa: LOP_THEO_DOI[b.lop] + ' đứng im bất thường giữa khu vực lưu thông',
        })
        b.dungIm = 0
      }

      ctx.strokeStyle = mau
      ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1)
      ctx.fillStyle = mau
      const tw = ctx.measureText(nhan).width + 10
      ctx.fillRect(b.x1 - 1, b.y1 - 22, tw, 20)
      ctx.fillStyle = '#08131f'
      ctx.fillText(nhan, b.x1 + 4, b.y1 - 6)
    }
    khungTruoc = hop

    // Khung DO khoanh dung vung lua da dinh vi
    if (lua.khung) {
      ctx.strokeStyle = '#ef4444'
      ctx.lineWidth = Math.max(4, w / 200)
      ctx.strokeRect(lua.khung.x, lua.khung.y, lua.khung.w, lua.khung.h)
      ctx.fillStyle = 'rgba(239,68,68,.85)'
      ctx.fillRect(lua.khung.x, Math.max(0, lua.khung.y - 26), 210, 24)
      ctx.fillStyle = '#fff'
      ctx.fillText('VÙNG LỬA / NHIỆT', lua.khung.x + 6, Math.max(16, lua.khung.y - 8))
    }

    return {
      canhBaos,
      meta: hop.length + ' đối tượng · lửa ' + (lua.tiLe * 100).toFixed(1) + '% · ' + new Date().toLocaleTimeString('vi-VN'),
    }
  }

  return { batNhip }
}

/* ================== 3. VIDEO BANG CHUNG (MediaRecorder xoay vong) ==================
   WebM chi PHAT DUOC khi blob bat dau bang chunk header (init segment). Ban cu dung
   ring buffer shift() -> sau ~14s chunk header bi day ra ngoai, video mo khong len.
   Ban nay: cu ~16s thi stop + start lai recorder — MOI doan luon tu chua header. */
export function taoBoGhiHinh(stream) {
  let rec = null
  let doan = []
  let doi = 0
  let dangCho = false

  function batDau(the) {
    if (!stream || the !== doi) return
    try {
      rec = new MediaRecorder(stream, { mimeType: 'video/webm' })
    } catch {
      rec = null
      return
    }
    doan = []
    rec.ondataavailable = (e) => {
      if (e.data && e.data.size) {
        doan.push(e.data)
        // Du ~16s thi xoay vong doan moi (tru khi dang dong goi bang chung)
        if (doan.length >= 8 && !dangCho) {
          try {
            rec.stop()
          } catch {
            /* recorder da dung */
          }
        }
      }
    }
    rec.onstop = () => {
      if (the === doi && stream) batDau(the)
    }
    try {
      rec.start(2000)
    } catch {
      rec = null
    }
  }

  if (window.MediaRecorder && stream) batDau(doi)

  return {
    /** Ghi them ~2,2 giay roi dong goi doan video bang chung; null neu khong co gi de gui */
    async dongGoi() {
      if (!rec) return null
      dangCho = true
      await new Promise((ok) => setTimeout(ok, 2200))
      const blob = new Blob(doan.slice(), { type: 'video/webm' })
      dangCho = false
      return blob.size < 5000 ? null : blob
    },
    dung() {
      doi++ // chan onstop tu khoi dong lai
      if (rec && rec.state !== 'inactive') {
        try {
          rec.stop()
        } catch {
          /* da dung */
        }
      }
      rec = null
      doan = []
    },
  }
}
