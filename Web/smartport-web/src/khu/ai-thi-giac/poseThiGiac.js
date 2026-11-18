/* ============================================================
   SAFEPOSE AI — YOLO26n-Pose: uoc luong 17 khop co the nguoi (chuan COCO)
   (chuyen tu Scripts/pose.js cua ban MVC sang module ES cho React;
   LUAT AN TOAN giu NGUYEN de ket qua hai giao dien giong nhau)
   ------------------------------------------------------------
   - Chay 100% tren trinh duyet bang ONNX Runtime Web (dung chung ortThiGiac.js voi Camera AI).
   - Anh KHONG roi khoi may nguoi dung (khong gui len server).
   - Phien suy luan song qua cac lan mo man hinh: quay lai trang khong phai tai lai mo hinh.
   ============================================================ */
import { AI_MODELS } from '../../ai/taiThuVien'
import { chuanBiTensor, taoPhien, veNhan } from './ortThiGiac'

const URL_MO_HINH = AI_MODELS + 'yolo26n-pose.onnx'
const S = 640

/**
 * Cac canh khung xuong (noi cap khop). Thu tu 17 khop COCO cua mo hinh:
 * 0 mui · 1-2 mat T/P · 3-4 tai T/P · 5-6 vai T/P · 7-8 khuyu T/P · 9-10 co tay T/P
 * 11-12 hong T/P · 13-14 goi T/P · 15-16 co chan T/P
 */
const KHUNG_XUONG = [
  [5, 7], [7, 9], [6, 8], [8, 10], // tay
  [5, 6], [5, 11], [6, 12], [11, 12], // than
  [11, 13], [13, 15], [12, 14], [14, 16], // chan
  [0, 5], [0, 6], // dau -> vai
]

export const MAU_NGUY = '#dc2626'
export const MAU_CHU_Y = '#e5691d'
export const MAU_AN_TOAN = '#22c55e'

/* ---------------- Trang thai dong co (dung chung ca phien lam viec) ---------------- */
const dc = { phien: null, ort: null, gpu: false, tuBoDem: false, dangChay: 0 }
let huaKhoiDong = null
let bao = () => {}
let canvasLamViec = null

/** Giao dien dang ky ham nhan chu trang thai tai mo hinh (bo dang ky khi roi man hinh) */
export function datBaoTrangThaiPose(ham) {
  bao = ham || (() => {})
}

export function thongTinPose() {
  return dc.phien ? { ten: 'YOLO26n-Pose', gpu: dc.gpu, tuBoDem: dc.tuBoDem } : null
}

/** Khoi dong mo hinh pose 1 lan (lan sau tra ngay) */
export function khoiDongPose() {
  if (dc.phien) return Promise.resolve(thongTinPose())
  if (!huaKhoiDong) {
    huaKhoiDong = (async () => {
      const kq = await taoPhien(URL_MO_HINH, {
        uuTienGpu: true,
        bao: (chu) => bao(chu),
        tenMoHinh: 'YOLO26n-Pose',
        mb: 11,
      })
      Object.assign(dc, { phien: kq.phien, ort: kq.ort, gpu: kq.gpu, tuBoDem: kq.tuBoDem })
      return thongTinPose()
    })().finally(() => {
      huaKhoiDong = null
    })
  }
  return huaKhoiDong
}

/**
 * Doc dau ra pose end-to-end: [1, N, D] voi D = 4(box) + 1(score) [+1(class)] + 17*3(kpts)
 * => D = 56 hoac 57. Tu phat hien vi tri bat dau keypoint theo D (giong ban MVC).
 */
export function docDauRaPose(dauRa, meta, nguong) {
  const nguoi = []
  const { dims, data: d } = dauRa
  if (dims.length !== 3) return nguoi
  const N = dims[1]
  const D = dims[2]
  if (D < 56) return nguoi
  const viTriKhop = D - 51

  for (let i = 0; i < N; i++) {
    const o = i * D
    const diem = d[o + 4]
    if (diem < nguong) continue
    const khop = []
    for (let k = 0; k < 17; k++) {
      khop.push({
        x: (d[o + viTriKhop + k * 3] - meta.padX) / meta.scale,
        y: (d[o + viTriKhop + k * 3 + 1] - meta.padY) / meta.scale,
        v: d[o + viTriKhop + k * 3 + 2],
      })
    }
    nguoi.push({
      x: (d[o] - meta.padX) / meta.scale,
      y: (d[o + 1] - meta.padY) / meta.scale,
      w: (d[o + 2] - d[o]) / meta.scale,
      h: (d[o + 3] - d[o + 1]) / meta.scale,
      score: diem,
      khop,
    })
  }
  return nguoi
}

/** Diem giua 2 khop (bo qua khop co do tin cay thap) */
function diemGiua(a, b) {
  if (a.v > 0.3 && b.v > 0.3) return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, ok: true }
  if (a.v > 0.3) return { x: a.x, y: a.y, ok: true }
  if (b.v > 0.3) return { x: b.x, y: b.y, ok: true }
  return { ok: false }
}

/**
 * LUAT AN TOAN LAO DONG (giu nguyen ban MVC):
 *   - Nam ngang (khung rong hon cao 1,35 lan) -> NGUY HIEM, nghi nga
 *   - Dau thap hon hong -> NGUY HIEM
 *   - Than tren gap sau (< 55% than duoi) -> CHU Y, nang vat sai tu the
 *   - Con lai -> An toan
 */
export function danhGiaAnToan(p) {
  const mui = p.khop[0]
  const vai = diemGiua(p.khop[5], p.khop[6])
  const hong = diemGiua(p.khop[11], p.khop[12])
  const goi = diemGiua(p.khop[13], p.khop[14])

  if (p.w > p.h * 1.35) {
    return {
      trangThai: 'NGUY HIỂM — nghi ngã',
      mau: MAU_NGUY,
      lyDo: 'Tư thế nằm ngang (khung rộng ' + Math.round((p.w / p.h) * 100) / 100 + ' lần chiều cao)',
    }
  }
  if (mui.v > 0.3 && hong.ok && mui.y > hong.y + p.h * 0.08) {
    return {
      trangThai: 'NGUY HIỂM — đầu thấp hơn hông',
      mau: MAU_NGUY,
      lyDo: 'Vị trí đầu thấp hơn hông — nghi ngã hoặc tư thế nguy hiểm',
    }
  }
  if (vai.ok && hong.ok && goi.ok) {
    const thanTren = Math.abs(hong.y - vai.y)
    const thanDuoi = Math.abs(goi.y - hong.y)
    if (thanDuoi > 4 && thanTren < thanDuoi * 0.55) {
      return {
        trangThai: 'Chú ý — cúi thấp',
        mau: MAU_CHU_Y,
        lyDo: 'Thân trên gập sâu — tư thế nâng vật nặng sai có thể gây chấn thương lưng',
      }
    }
  }
  return { trangThai: 'An toàn', mau: MAU_AN_TOAN, lyDo: 'Tư thế đứng/di chuyển bình thường' }
}

/** Nhan dien tu the 1 khung hinh -> { nguoi, ms } */
export async function nhanDienTuThe(nguon, w, h, { nguong = 0.4 } = {}) {
  if (!dc.phien) throw new Error('Mô hình pose đang tải, thử lại sau.')
  const t0 = performance.now()
  if (!canvasLamViec) canvasLamViec = document.createElement('canvas')
  const meta = chuanBiTensor(dc.ort, nguon, w, h, S, canvasLamViec)
  dc.dangChay++
  try {
    const out = await dc.phien.run({ [dc.phien.inputNames[0]]: meta.tensor })
    return { nguoi: docDauRaPose(out[dc.phien.outputNames[0]], meta, nguong), ms: Math.round(performance.now() - t0) }
  } finally {
    dc.dangChay--
  }
}

/** Ve khung hinh + khung bao + khung xuong + nhan trang thai; tra ve so nguoi theo muc do */
export function veKetQuaPose(canvas, nguon, w, h, nguoi) {
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d')
  ctx.drawImage(nguon, 0, 0, w, h)
  const day = Math.max(2, w / 400)
  let soNguy = 0
  let soChuY = 0

  for (const p of nguoi) {
    const at = danhGiaAnToan(p)
    if (at.mau === MAU_NGUY) soNguy++
    else if (at.mau === MAU_CHU_Y) soChuY++

    ctx.strokeStyle = at.mau
    ctx.lineWidth = day * 1.4
    ctx.strokeRect(p.x, p.y, p.w, p.h)

    ctx.lineWidth = day
    for (const [i, j] of KHUNG_XUONG) {
      const a = p.khop[i]
      const b = p.khop[j]
      if (a.v > 0.3 && b.v > 0.3) {
        ctx.strokeStyle = at.mau
        ctx.beginPath()
        ctx.moveTo(a.x, a.y)
        ctx.lineTo(b.x, b.y)
        ctx.stroke()
      }
    }
    for (const k of p.khop) {
      if (k.v <= 0.3) continue
      ctx.fillStyle = '#fff'
      ctx.beginPath()
      ctx.arc(k.x, k.y, day * 1.7, 0, Math.PI * 2)
      ctx.fill()
      ctx.fillStyle = at.mau
      ctx.beginPath()
      ctx.arc(k.x, k.y, day * 1.1, 0, Math.PI * 2)
      ctx.fill()
    }
    veNhan(ctx, at.trangThai + ' ' + Math.round(p.score * 100) + '%', p.x, p.y, at.mau, '#fff', w)
  }
  return { tong: nguoi.length, soNguy, soChuY }
}
