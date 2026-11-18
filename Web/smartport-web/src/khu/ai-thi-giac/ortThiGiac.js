/* ============================================================
   ONNX RUNTIME WEB dung chung cho khu AI THI GIAC
   (Camera AI YOLO26 + SafePose YOLO26-Pose)
   ------------------------------------------------------------
   - Thu vien nap 1 LAN cho ca phien lam viec (taiScript tu chong nap trung).
   - Trinh duyet co WebGPU -> nap ban ort.webgpu.min.js (chua ca WebGPU + WASM),
     khong co -> ort.min.js (WASM CPU) giong man hinh Cong ALPR.
   - Man hinh khac da nap ORT truoc (vd Cong ALPR) -> DUNG CHUNG, khong nap ban thu 2.
   - Mo hinh tai qua Cache API: lan dau tai mang roi luu bo dem,
     cac lan sau nap tuc thi tu dia (ke ca khi mat mang).
   ============================================================ */
import { AI_VENDOR, taiScript } from '../../ai/taiThuVien'

const THU_MUC_ORT = AI_VENDOR + 'ort/'
const TEN_BO_DEM = 'sp-models-v1'

let huaOrt = null

/** Nap ONNX Runtime Web -> { ort, webGpu } (webGpu = da nap ban co WebGPU) */
export function taiOrt() {
  if (!huaOrt) {
    huaOrt = (async () => {
      if (window.ort?.InferenceSession) {
        window.ort.env.wasm.wasmPaths = THU_MUC_ORT
        return { ort: window.ort, webGpu: false }
      }
      let webGpu = false
      if (navigator.gpu) {
        try {
          await taiScript(THU_MUC_ORT + 'ort.webgpu.min.js')
          webGpu = !!window.ort?.InferenceSession
        } catch {
          /* khong nap duoc ban WebGPU -> dung ban CPU ben duoi */
        }
      }
      if (!window.ort?.InferenceSession) await taiScript(THU_MUC_ORT + 'ort.min.js')
      // Giu tham chieu RIENG: man hinh khac nap them ban ORT sau nay cung khong lam lech phien cua khu nay
      const ort = window.ort
      ort.env.wasm.wasmPaths = THU_MUC_ORT
      return { ort, webGpu }
    })()
    huaOrt.catch(() => {
      huaOrt = null
    })
  }
  return huaOrt
}

async function taiQuaMang(url) {
  const r = await fetch(url)
  if (!r.ok) throw new Error(`Không tải được mô hình (HTTP ${r.status})`)
  if ((r.headers.get('content-type') || '').includes('text/html')) throw new Error('Không tìm thấy file mô hình ' + url)
  return r
}

/** Tai file mo hinh .onnx -> { buf, tuBoDem } (uu tien bo dem Cache API) */
export async function taiMoHinh(url) {
  // Cache API chi co tren HTTPS / localhost
  if (!('caches' in window)) return { buf: await (await taiQuaMang(url)).arrayBuffer(), tuBoDem: false }
  let kho = null
  try {
    kho = await caches.open(TEN_BO_DEM)
    const coSan = await kho.match(url)
    if (coSan) return { buf: await coSan.arrayBuffer(), tuBoDem: true }
  } catch {
    kho = null
  }
  const r = await taiQuaMang(url)
  if (kho) {
    try {
      await kho.put(url, r.clone())
    } catch {
      /* bo dem day / bi chan: van dung ban vua tai */
    }
  }
  return { buf: await r.arrayBuffer(), tuBoDem: false }
}

/**
 * Tao phien suy luan tu URL mo hinh.
 * uuTienGpu: thu WebGPU truoc, loi driver / khong ho tro -> tu chay lai bang WASM CPU.
 * bao(chu): bao tien do cho giao dien.
 */
export async function taoPhien(url, { uuTienGpu = false, bao = () => {}, tenMoHinh = 'mô hình', mb } = {}) {
  bao('Đang tải thư viện ONNX Runtime…')
  const { ort, webGpu } = await taiOrt()
  bao(`Đang tải ${tenMoHinh}${mb ? ` (~${mb} MB, lần đầu hơi lâu)` : ''}…`)
  const { buf, tuBoDem } = await taiMoHinh(url)
  let gpu = uuTienGpu && webGpu
  bao(`Đang khởi tạo ${tenMoHinh} trên ${gpu ? 'WebGPU' : 'WASM (CPU)'}…`)
  let phien
  try {
    phien = await ort.InferenceSession.create(buf, { executionProviders: gpu ? ['webgpu', 'wasm'] : ['wasm'] })
  } catch (e) {
    if (!gpu) throw e
    gpu = false
    phien = await ort.InferenceSession.create(buf, { executionProviders: ['wasm'] })
  }
  return { phien, ort, gpu, tuBoDem }
}

/** Giai phong phien ONNX (bo qua loi neu da giai phong) */
export function giaiPhongPhien(phien) {
  try {
    phien?.release?.()
  } catch {
    /* da giai phong */
  }
}

/**
 * LETTERBOX giu ti le, dem xam 114 nhu luc huan luyen YOLO -> tensor [1,3,S,S] (RGB 0..1)
 * canvas: canvas lam viec rieng cua tung dong co (khong ve len man hinh).
 */
export function chuanBiTensor(ort, nguon, w, h, S, canvas) {
  canvas.width = S
  canvas.height = S
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const scale = Math.min(S / w, S / h)
  const nw = Math.round(w * scale)
  const nh = Math.round(h * scale)
  const padX = Math.floor((S - nw) / 2)
  const padY = Math.floor((S - nh) / 2)
  ctx.fillStyle = '#727272'
  ctx.fillRect(0, 0, S, S)
  ctx.drawImage(nguon, 0, 0, w, h, padX, padY, nw, nh)
  const img = ctx.getImageData(0, 0, S, S).data
  const dienTich = S * S
  const t = new Float32Array(3 * dienTich)
  for (let i = 0; i < dienTich; i++) {
    t[i] = img[i * 4] / 255
    t[dienTich + i] = img[i * 4 + 1] / 255
    t[2 * dienTich + i] = img[i * 4 + 2] / 255
  }
  return { tensor: new ort.Tensor('float32', t, [1, 3, S, S]), scale, padX, padY }
}

/** Ve nhan chu co nen mau len canvas (dung chung cho khung doi tuong va khung nguoi) */
export function veNhan(ctx, chu, x, y, mauNen, mauChu, w) {
  const fs = Math.max(13, Math.round(w / 55))
  ctx.font = `600 ${fs}px "Segoe UI", system-ui, sans-serif`
  const rong = ctx.measureText(chu).width + 10
  const ty = y > fs + 8 ? y - fs - 6 : y + 2
  ctx.fillStyle = mauNen
  ctx.fillRect(x - 1, ty, rong, fs + 6)
  ctx.fillStyle = mauChu
  ctx.fillText(chu, x + 4, ty + fs)
}
