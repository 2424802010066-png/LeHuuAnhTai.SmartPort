/* ============================================================
   YOLO26n DUNG CHUNG cho Kiem dem AI va Giam sat tai xe
   (chuyen tu init/detect cua tally.js va initYolo/yoloDetect cua driver.js)
   ------------------------------------------------------------
   - ONNX Runtime Web: DUNG LAI ban da nap tren trang (alpr.js cung nap ort.min.js).
     Nap them ban ORT thu hai se ghi de window.ort -> tensor cua ban nay dua vao
     session cua ban kia bi tu choi -> hong AI o man hinh khac cua ung dung 1 trang.
   - Mo hinh 10 MB tai 1 lan, luu Cache Storage 'sp-models-v1' (dung chung ten voi ban MVC)
   - Chup khung hinh NGAY khi goi, roi xep hang: moi luc chi 1 lan suy luan tren session
     (camera tai xe va camera khoang hang goi xen ke nhau)
   ============================================================ */
import { AI_MODELS, AI_VENDOR, taiScript } from '../../../ai/taiThuVien'

export const KICH_THUOC_VAO = 640
const NGUONG_DIEM = 0.35
const TEN_BO_NHO_MO_HINH = 'sp-models-v1'

let huaKhoiDong = null
let daSanSang = false
let hangDoi = Promise.resolve()
let canvasLam = null

async function napOrt() {
  if (!window.ort) await taiScript(AI_VENDOR + 'ort/ort.min.js')
  const ort = window.ort
  ort.env.wasm.wasmPaths = AI_VENDOR + 'ort/'
  return ort
}

async function taiQuaMang(url) {
  const r = await fetch(url)
  if (!r.ok) throw new Error('HTTP ' + r.status + ' khi tải mô hình')
  return r
}

/** Tai mo hinh qua Cache Storage — KHONG luu phan hoi loi (luu nham thi trang khong bao gio tai lai duoc) */
async function taiMoHinh(url) {
  let kho = null
  try {
    kho = 'caches' in window ? await caches.open(TEN_BO_NHO_MO_HINH) : null
  } catch {
    kho = null // trinh duyet chan Cache Storage (che do rieng tu, http thuong)
  }
  if (!kho) return (await taiQuaMang(url)).arrayBuffer()
  const daCo = await kho.match(url)
  if (daCo) return daCo.arrayBuffer()
  const r = await taiQuaMang(url)
  await kho.put(url, r.clone()).catch(() => {})
  return r.arrayBuffer()
}

/** Nap ORT + YOLO26n (1 lan cho ca phien lam viec). Loi thi lan goi sau thu lai. */
export function khoiDongYolo() {
  if (!huaKhoiDong) {
    huaKhoiDong = (async () => {
      const ort = await napOrt()
      const moHinh = await taiMoHinh(AI_MODELS + 'yolo26n.onnx')
      const session = await ort.InferenceSession.create(moHinh, { executionProviders: ['wasm'] })
      daSanSang = true
      return { ort, session }
    })().catch((e) => {
      huaKhoiDong = null
      throw e
    })
  }
  return huaKhoiDong
}

/** YOLO da nap xong chua (dung trong vong lap camera — khong cho doi) */
export const yoloSanSang = () => daSanSang

/** Letterbox anh vao o vuong 640 (dem xam 114 nhu luc huan luyen) -> tensor CHW [0..1] */
function chuanBiDauVao(nguon) {
  const w = nguon.width
  const h = nguon.height
  if (!canvasLam) canvasLam = document.createElement('canvas')
  canvasLam.width = KICH_THUOC_VAO
  canvasLam.height = KICH_THUOC_VAO
  const ctx = canvasLam.getContext('2d', { willReadFrequently: true })
  const sc = Math.min(KICH_THUOC_VAO / w, KICH_THUOC_VAO / h)
  const dx = (KICH_THUOC_VAO - Math.round(w * sc)) / 2
  const dy = (KICH_THUOC_VAO - Math.round(h * sc)) / 2
  ctx.fillStyle = '#727272'
  ctx.fillRect(0, 0, KICH_THUOC_VAO, KICH_THUOC_VAO)
  ctx.drawImage(nguon, dx, dy, Math.round(w * sc), Math.round(h * sc))
  const d = ctx.getImageData(0, 0, KICH_THUOC_VAO, KICH_THUOC_VAO).data
  const dienTich = KICH_THUOC_VAO * KICH_THUOC_VAO
  const arr = new Float32Array(3 * dienTich)
  for (let i = 0, p = 0; i < d.length; i += 4, p++) {
    arr[p] = d[i] / 255
    arr[p + dienTich] = d[i + 1] / 255
    arr[p + 2 * dienTich] = d[i + 2] / 255
  }
  return { arr, w, h, sc, dx, dy }
}

/**
 * Phat hien doi tuong COCO tren canvas dang hien khung hinh.
 * @param {HTMLCanvasElement} nguon
 * @param {number[]} lopCanTim ma lop COCO (0 person, 28 suitcase, 39 bottle, 67 cell phone...)
 * @returns {Promise<{x1:number,y1:number,x2:number,y2:number,cls:number,score:number}[]>} toa do pixel cua canvas nguon
 */
export async function phatHienYolo(nguon, lopCanTim) {
  // Chup khung hinh DONG BO truoc khi cho: canvas co the duoc ve khung moi trong luc xep hang
  const vao = chuanBiDauVao(nguon)
  const { ort, session } = await khoiDongYolo()
  const luot = hangDoi.then(() =>
    session.run({ [session.inputNames[0]]: new ort.Tensor('float32', vao.arr, [1, 3, KICH_THUOC_VAO, KICH_THUOC_VAO]) }),
  )
  hangDoi = luot.catch(() => {})
  const out = await luot
  const o = out[session.outputNames[0]]
  const { w, h, sc, dx, dy } = vao
  const hop = []
  // Dau ra [1,300,6]: x1 y1 x2 y2 score cls (YOLO26 end-to-end, khong can NMS)
  for (let k = 0; k < o.dims[1]; k++) {
    const b = k * 6
    const score = o.data[b + 4]
    const cls = Math.round(o.data[b + 5])
    if (score < NGUONG_DIEM || !lopCanTim.includes(cls)) continue
    hop.push({
      x1: Math.max(0, (o.data[b] - dx) / sc),
      y1: Math.max(0, (o.data[b + 1] - dy) / sc),
      x2: Math.min(w, (o.data[b + 2] - dx) / sc),
      y2: Math.min(h, (o.data[b + 3] - dy) / sc),
      cls,
      score,
    })
  }
  return hop
}
