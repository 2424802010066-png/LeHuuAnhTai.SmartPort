/* ============================================================
   MEDIAPIPE FACE LANDMARKER — 478 diem moc + blendshapes, chay WASM
   ngay tren trinh duyet (chuyen tu initFace/gocTuLandmark/dacTrung cua driver.js).
   Anh tai xe KHONG roi khoi may — khong gui khung hinh nao len server,
   chi gui ANH BANG CHUNG khi that su co su co.
   ------------------------------------------------------------
   Thu vien va mo hinh deu nam trong wwwroot (khong CDN): bai cham co the
   chay offline. Phien landmarker duoc giu lai cho ca phien lam viec —
   vao ra man hinh nhieu lan khong phai tai lai mo hinh 3,7 MB.
   ============================================================ */
import { AI_MODELS, AI_VENDOR } from '../../../ai/taiThuVien'

const MP_GOC = AI_VENDOR + 'mediapipe'
const MP_MO_HINH = AI_MODELS + 'face_landmarker.task'

let huaKhoiDong = null
let boNhan = null

/** Nap FaceLandmarker (1 lan cho ca phien). Loi thi lan goi sau thu lai. */
export function khoiDongKhuonMat() {
  if (!huaKhoiDong) {
    huaKhoiDong = (async () => {
      // Duong dan dong (thu muc vendor trong wwwroot) -> bao Vite dung phan tich
      const mod = await import(/* @vite-ignore */ MP_GOC + '/vision_bundle.mjs')
      const fs = await mod.FilesetResolver.forVisionTasks(MP_GOC + '/wasm')
      const opts = {
        baseOptions: { modelAssetPath: MP_MO_HINH, delegate: 'GPU' },
        runningMode: 'VIDEO',
        numFaces: 1,
        outputFaceBlendshapes: true,
      }
      boNhan = await mod.FaceLandmarker.createFromOptions(fs, opts).catch(() => {
        opts.baseOptions.delegate = 'CPU' // may khong co WebGL -> chay CPU
        return mod.FaceLandmarker.createFromOptions(fs, opts)
      })
      return boNhan
    })().catch((e) => {
      huaKhoiDong = null
      throw e
    })
  }
  return huaKhoiDong
}

export const khuonMatSanSang = () => !!boNhan

/** Nhan dien tren 1 khung hinh video/anh; tra null neu chua nap xong hoac loi */
export function nhanDienKhuonMat(nguon) {
  if (!boNhan) return null
  try {
    return boNhan.detectForVideo(nguon, performance.now())
  } catch {
    return null
  }
}

/** Lay diem blendshape theo ten (eyeBlinkLeft, jawOpen...) */
export function diemBlend(cats, ten) {
  for (const c of cats) if (c.categoryName === ten) return c.score
  return 0
}

/**
 * Goc dau tu 4 diem moc: mat trai ngoai 33, mat phai ngoai 263, mui 1, cam 152.
 * yaw: mui lech ngang so voi trung diem 2 mat (da khu roll)
 * pitch: vi tri mui giua duong mat va cam (chinh dien ~0,42-0,55) — duong = cui xuong
 */
export function gocTuLandmark(lm) {
  const mt = lm[33]
  const mp = lm[263]
  const mui = lm[1]
  const cam = lm[152]
  const gx = (mt.x + mp.x) / 2
  const gy = (mt.y + mp.y) / 2
  const kcMat = Math.hypot(mp.x - mt.x, mp.y - mt.y) + 1e-6
  const roll = Math.atan2(mp.y - mt.y, mp.x - mt.x)
  const ex = mui.x - gx
  const ey = mui.y - gy
  const ngang = ex * Math.cos(-roll) - ey * Math.sin(-roll)
  const yaw = Math.asin(Math.max(-1, Math.min(1, (2 * ngang) / kcMat))) * (180 / Math.PI)
  const caoMat = Math.hypot(cam.x - gx, cam.y - gy) + 1e-6
  const doc = (ey * Math.cos(-roll) + ex * Math.sin(-roll)) / caoMat
  return { yaw, pitch: (doc - 0.48) * 120, roll: roll * (180 / Math.PI) }
}

/** Vec-to 10 dac trung chuan hoa [-1..1] cho mang tu ma hoa */
export function dacTrung(goc, nham, mieng, lm, truoc = { yaw: 0, pitch: 0 }) {
  const mt = lm[33]
  const mp = lm[263]
  const cx = (mt.x + mp.x) / 2
  const cy = (mt.y + mp.y) / 2
  const kc = Math.hypot(mp.x - mt.x, mp.y - mt.y)
  const kep = (v) => Math.max(-1, Math.min(1, v))
  return [
    kep(goc.yaw / 45),
    kep(goc.pitch / 45),
    kep(goc.roll / 45),
    kep(nham * 2 - 1),
    kep(mieng * 2 - 1),
    kep((cx - 0.5) * 2),
    kep((cy - 0.5) * 2),
    kep(kc * 5 - 1),
    kep((goc.yaw - truoc.yaw) / 10),
    kep((goc.pitch - truoc.pitch) / 10),
  ]
}

const DIEM_MAT = [33, 263, 133, 362, 159, 145, 386, 374]
const DIEM_MIENG = [61, 291, 13, 14]

/** HUD gon: cham sang cac diem moc mat (xanh) va mieng (cam) */
export function veDiemMoc(ctx, lm, w, h) {
  ctx.fillStyle = '#22d3ee'
  for (const i of DIEM_MAT) {
    ctx.beginPath()
    ctx.arc(lm[i].x * w, lm[i].y * h, 2.4, 0, 7)
    ctx.fill()
  }
  ctx.fillStyle = '#ffb25e'
  for (const i of DIEM_MIENG) {
    ctx.beginPath()
    ctx.arc(lm[i].x * w, lm[i].y * h, 2.4, 0, 7)
    ctx.fill()
  }
}
