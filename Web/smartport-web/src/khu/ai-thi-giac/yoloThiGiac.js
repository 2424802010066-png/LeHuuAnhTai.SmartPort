/* ============================================================
   CAMERA AI — YOLO26 nhan dien doi tuong tai cang: xe tai, o to, nguoi, tau thuyen...
   (chuyen tu Scripts/vision.js cua ban MVC sang module ES cho React;
   thuat toan giu NGUYEN de ket qua hai giao dien giong nhau)
   ------------------------------------------------------------
   - Chay 100% tren trinh duyet bang ONNX Runtime Web (WebGPU neu co, khong thi WASM CPU).
   - Chuoi du phong: YOLO26 (mo hinh chon) -> YOLOv10n -> SSD MobileNet (TensorFlow.js).
   - Dong co SONG QUA CAC LAN MO MAN HINH (quay lai trang khong tai lai mo hinh);
     rieng mo hinh lon (s/m/l, du phong) duoc giai phong khi roi man hinh cho nhe bo nho.
   - Khong giai phong phien dang chay do (session.run) — cho chay xong moi release.
   ============================================================ */
import { AI_MODELS, AI_VENDOR, taiScript } from '../../ai/taiThuVien'
import { chuanBiTensor, giaiPhongPhien, taoPhien, veNhan } from './ortThiGiac'

const HUGGING_FACE = 'https://huggingface.co/zwh20081/yolo26-onnx/resolve/main/'

// YOLO26 (Ultralytics, 01/2026) — end-to-end, khong can NMS, dau ra [1,300,6]
export const DS_MO_HINH = [
  { ma: 'yolo26n', ten: 'YOLO26n', url: AI_MODELS + 'yolo26n.onnx', mAP: 40.9, mb: 9.5, moTa: 'nhanh nhất' },
  { ma: 'yolo26s', ten: 'YOLO26s', url: HUGGING_FACE + 'yolo26s.onnx', mAP: 48.6, mb: 36.5, moTa: 'cân bằng' },
  { ma: 'yolo26m', ten: 'YOLO26m', url: HUGGING_FACE + 'yolo26m.onnx', mAP: 53.1, mb: 78.2, moTa: 'chính xác cao' },
  { ma: 'yolo26l', ten: 'YOLO26l', url: HUGGING_FACE + 'yolo26l.onnx', mAP: 55, mb: 95, moTa: 'mạnh nhất, nên có GPU' },
]
export const MA_MAC_DINH = 'yolo26n'
const DU_PHONG = {
  ma: 'yolov10n',
  ten: 'YOLOv10n',
  url: 'https://huggingface.co/onnx-community/yolov10n/resolve/main/onnx/model.onnx',
  mAP: 38.5,
  mb: 9,
  moTa: 'dự phòng',
}

export const COCO = [
  'person', 'bicycle', 'car', 'motorcycle', 'airplane', 'bus', 'train', 'truck', 'boat', 'traffic light',
  'fire hydrant', 'stop sign', 'parking meter', 'bench', 'bird', 'cat', 'dog', 'horse', 'sheep', 'cow',
  'elephant', 'bear', 'zebra', 'giraffe', 'backpack', 'umbrella', 'handbag', 'tie', 'suitcase', 'frisbee',
  'skis', 'snowboard', 'sports ball', 'kite', 'baseball bat', 'baseball glove', 'skateboard', 'surfboard', 'tennis racket', 'bottle',
  'wine glass', 'cup', 'fork', 'knife', 'spoon', 'bowl', 'banana', 'apple', 'sandwich', 'orange',
  'broccoli', 'carrot', 'hot dog', 'pizza', 'donut', 'cake', 'chair', 'couch', 'potted plant', 'bed',
  'dining table', 'toilet', 'tv', 'laptop', 'mouse', 'remote', 'keyboard', 'cell phone', 'microwave', 'oven',
  'toaster', 'sink', 'refrigerator', 'book', 'clock', 'vase', 'scissors', 'teddy bear', 'hair drier', 'toothbrush',
]

const TEN_VIET = {
  person: 'Người', bicycle: 'Xe đạp', car: 'Ô tô', motorcycle: 'Xe máy', airplane: 'Máy bay',
  bus: 'Xe buýt', train: 'Tàu hỏa', truck: 'Xe tải/đầu kéo', boat: 'Tàu/thuyền', 'traffic light': 'Đèn giao thông',
  bench: 'Băng ghế', backpack: 'Ba lô', suitcase: 'Vali/thùng', laptop: 'Laptop', 'cell phone': 'Điện thoại',
  chair: 'Ghế', tv: 'Màn hình', clock: 'Đồng hồ', bottle: 'Chai', cup: 'Cốc', book: 'Sách', tie: 'Cà vạt',
}

export const tenViet = (nhan) => TEN_VIET[nhan] || nhan

// Mau khung ve tren anh camera (nen anh that) — giu bang mau cua ban MVC
const BANG_MAU = ['#ff7d2d', '#22d3ee', '#a3e635', '#f472b6', '#facc15', '#818cf8', '#34d399', '#fb7185']
export function mauCua(nhan) {
  let bam = 0
  for (let i = 0; i < nhan.length; i++) bam = (bam * 31 + nhan.charCodeAt(i)) & 0xffff
  return BANG_MAU[bam % BANG_MAU.length]
}

export const timMoHinh = (ma) => DS_MO_HINH.find((m) => m.ma === ma) || null

/* ---------------- Trang thai dong co (dung chung ca phien lam viec) ---------------- */
const dc = {
  loai: null, // 'yolo' | 'ssd'
  phien: null,
  ort: null,
  info: null,
  gpu: false,
  tuBoDem: false,
  duPhong: false,
  ssd: null,
  dangNap: null, // mo hinh dang tai
  dangChay: 0, // so lan suy luan dang do dang
  choGiaiPhong: [],
  ep640: false, // mo hinh co dau vao TINH 640 -> khong dung 448 cho webcam nua
  theHe: 0, // tang khi roi man hinh -> huy lan tai mo hinh lon dang do
}
let huaKhoiDong = null
let bao = () => {}
let canvasLamViec = null

class DaRoiManHinh extends Error {}

/** Giao dien dang ky ham nhan chu trang thai tai mo hinh (null khi roi man hinh) */
export function datBaoTrangThai(ham) {
  bao = ham || (() => {})
}

export function thongTinDongCo() {
  if (dc.loai === 'ssd') return { loai: 'ssd', ma: null, ten: 'SSD MobileNet', mAP: null, gpu: false, duPhong: true, tuBoDem: false }
  if (dc.loai === 'yolo') return { loai: 'yolo', ma: dc.info.ma, ten: dc.info.ten, mAP: dc.info.mAP, gpu: dc.gpu, duPhong: dc.duPhong, tuBoDem: dc.tuBoDem }
  return null
}

function donHangCho() {
  if (dc.dangChay) return
  while (dc.choGiaiPhong.length) giaiPhongPhien(dc.choGiaiPhong.pop())
}

function giaiPhongKhiRanh(phien) {
  if (!phien) return
  dc.choGiaiPhong.push(phien)
  donHangCho()
}

async function napYolo(info, duPhong) {
  const theHe = dc.theHe
  dc.dangNap = info
  try {
    const kq = await taoPhien(info.url, { uuTienGpu: true, bao: (chu) => bao(chu), tenMoHinh: info.ten, mb: info.mb })
    if (theHe !== dc.theHe) {
      giaiPhongPhien(kq.phien)
      throw new DaRoiManHinh('Đã rời màn hình trước khi tải xong ' + info.ten + '.')
    }
    const cu = dc.phien
    Object.assign(dc, { loai: 'yolo', phien: kq.phien, ort: kq.ort, info, gpu: kq.gpu, tuBoDem: kq.tuBoDem, duPhong, ssd: null })
    if (cu && cu !== kq.phien) giaiPhongKhiRanh(cu)
  } finally {
    if (dc.dangNap === info) dc.dangNap = null
  }
}

async function napSsd() {
  bao('Không tải được YOLO — chuyển sang engine dự phòng SSD MobileNet…')
  await taiScript(AI_VENDOR + 'tf/tf.min.js')
  await taiScript(AI_VENDOR + 'tf/coco-ssd.min.js')
  const m = await window.cocoSsd.load()
  const cu = dc.phien
  Object.assign(dc, { loai: 'ssd', ssd: m, phien: null, ort: null, info: null, gpu: false, tuBoDem: false, duPhong: true })
  if (cu) giaiPhongKhiRanh(cu)
}

/** Khoi dong dong co 1 lan (lan sau tra ngay). Chuoi: YOLO26n -> YOLOv10n -> SSD MobileNet */
export function khoiDongYolo() {
  if (dc.loai) return Promise.resolve(thongTinDongCo())
  if (!huaKhoiDong) {
    huaKhoiDong = (async () => {
      try {
        await napYolo(timMoHinh(MA_MAC_DINH), false)
      } catch (e1) {
        if (e1 instanceof DaRoiManHinh) throw e1
        try {
          bao('Không tải được YOLO26n — thử mô hình dự phòng YOLOv10n…')
          await napYolo(DU_PHONG, true)
        } catch (e2) {
          if (e2 instanceof DaRoiManHinh) throw e2
          try {
            await napSsd()
          } catch {
            throw new Error('Không tải được mô hình AI (kiểm tra kết nối Internet rồi tải lại trang).')
          }
        }
      }
      return thongTinDongCo()
    })().finally(() => {
      huaKhoiDong = null
    })
  }
  return huaKhoiDong
}

/**
 * Doi mo hinh (n / s / m / l). GIU phien cu cho toi khi mo hinh moi nap xong — webcam van chay
 * bang mo hinh cu trong luc tai; tai loi thi giu nguyen dong co cu va nem loi cho giao dien bao.
 */
export async function doiMoHinh(ma) {
  const info = timMoHinh(ma)
  if (!info) throw new Error('Không có mô hình ' + ma + '.')
  if (dc.loai === 'yolo' && dc.info?.ma === ma) return thongTinDongCo()
  await napYolo(info, false)
  return thongTinDongCo()
}

/** Roi man hinh: huy lan tai mo hinh lon dang do, giai phong mo hinh lon (giu YOLO26n cho lan sau) */
export function giaiPhongYoloNeuLon() {
  bao = () => {}
  if (dc.dangNap && dc.dangNap.ma !== MA_MAC_DINH) dc.theHe++
  if (dc.loai === 'yolo' && dc.info.ma !== MA_MAC_DINH) {
    giaiPhongKhiRanh(dc.phien)
    Object.assign(dc, { loai: null, phien: null, ort: null, info: null, gpu: false, tuBoDem: false, duPhong: false })
  }
}

/* ---------------- Doc dau ra YOLO ---------------- */
function sangHop(x1, y1, x2, y2, diem, lop, meta) {
  return {
    x: (x1 - meta.padX) / meta.scale,
    y: (y1 - meta.padY) / meta.scale,
    w: (x2 - x1) / meta.scale,
    h: (y2 - y1) / meta.scale,
    score: diem,
    label: COCO[lop] || '#' + lop,
  }
}

function iou(a, b) {
  const x1 = Math.max(a.x1, b.x1)
  const y1 = Math.max(a.y1, b.y1)
  const x2 = Math.min(a.x2, b.x2)
  const y2 = Math.min(a.y2, b.y2)
  const giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
  const hopNhat = (a.x2 - a.x1) * (a.y2 - a.y1) + (b.x2 - b.x1) * (b.y2 - b.y1) - giao
  return hopNhat > 0 ? giao / hopNhat : 0
}

/**
 * Tensor dau ra -> danh sach khung { x, y, w, h, score, label } theo toa do anh goc.
 * Nguong hieu luc cua 1 lop = MAX(nguong thanh truot, nguong AI TU HOC cua lop do).
 *   [1,300,6]  YOLOv10/26 end-to-end (x1,y1,x2,y2,score,cls) — khong can NMS
 *   [1,84,8400] YOLOv8/11 — can NMS
 */
export function docDauRaYolo(dauRa, meta, nguong, nguongLop = {}) {
  const hop = []
  const { dims, data: d } = dauRa
  const nguongCua = (lop) => Math.max(nguong, nguongLop[COCO[lop]] || 0)
  if (dims.length === 3 && dims[2] === 6) {
    for (let i = 0; i < dims[1]; i++) {
      const o = i * 6
      const diem = d[o + 4]
      const lop = Math.round(d[o + 5])
      if (diem < nguongCua(lop)) continue
      hop.push(sangHop(d[o], d[o + 1], d[o + 2], d[o + 3], diem, lop, meta))
    }
  } else if (dims.length === 3 && dims[1] === 84) {
    const n = dims[2]
    const tho = []
    for (let a = 0; a < n; a++) {
      let tot = 0
      let lop = -1
      for (let k = 0; k < 80; k++) {
        const s = d[(4 + k) * n + a]
        if (s > tot) {
          tot = s
          lop = k
        }
      }
      if (lop < 0 || tot < nguongCua(lop)) continue
      const cx = d[a]
      const cy = d[n + a]
      const w = d[2 * n + a]
      const h = d[3 * n + a]
      tho.push({ x1: cx - w / 2, y1: cy - h / 2, x2: cx + w / 2, y2: cy + h / 2, diem: tot, lop })
    }
    tho.sort((p, q) => q.diem - p.diem)
    const giu = []
    for (const b of tho) if (!giu.some((k) => iou(b, k) > 0.45)) giu.push(b)
    for (const b of giu) hop.push(sangHop(b.x1, b.y1, b.x2, b.y2, b.diem, b.lop, meta))
  }
  return hop
}

/**
 * LAM MUOT THEO THOI GIAN cho luong video: khop khung moi voi khung truoc (cung nhan, IoU > 0.4)
 * roi lam muot toa do bang EMA -> khung nhan dien on dinh, khong giat nhay.
 */
export function lamMuot(hop, truoc) {
  if (!truoc?.length) return hop
  const A = 0.55 // trong so khung moi
  return hop.map((b) => {
    let tot = null
    let totIou = 0.4
    for (const p of truoc) {
      if (p.label !== b.label) continue
      const x1 = Math.max(p.x, b.x)
      const y1 = Math.max(p.y, b.y)
      const x2 = Math.min(p.x + p.w, b.x + b.w)
      const y2 = Math.min(p.y + p.h, b.y + b.h)
      const giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
      const hopNhat = p.w * p.h + b.w * b.h - giao
      const v = hopNhat > 0 ? giao / hopNhat : 0
      if (v > totIou) {
        totIou = v
        tot = p
      }
    }
    if (!tot) return b
    return {
      x: A * b.x + (1 - A) * tot.x,
      y: A * b.y + (1 - A) * tot.y,
      w: A * b.w + (1 - A) * tot.w,
      h: A * b.h + (1 - A) * tot.h,
      score: A * b.score + (1 - A) * tot.score,
      label: b.label,
    }
  })
}

/* ---------------- Nhan dien 1 khung hinh ---------------- */
/**
 * @param nguon HTMLImageElement | HTMLVideoElement | HTMLCanvasElement
 * @param laLuong webcam / video: tren CPU dung dau vao 448 px de tang FPS gap ~2 lan (anh tinh van 640)
 */
export async function nhanDien(nguon, w, h, { nguong = 0.35, nguongLop = {}, laLuong = false } = {}) {
  const t0 = performance.now()
  if (dc.loai === 'yolo' && dc.phien) {
    const { phien, ort } = dc
    const S = !dc.gpu && laLuong && !dc.ep640 ? 448 : 640
    if (!canvasLamViec) canvasLamViec = document.createElement('canvas')
    const meta = chuanBiTensor(ort, nguon, w, h, S, canvasLamViec)
    dc.dangChay++
    try {
      const out = await phien.run({ [phien.inputNames[0]]: meta.tensor })
      const hop = docDauRaYolo(out[phien.outputNames[0]], meta, nguong, nguongLop)
      return { hop, ms: Math.round(performance.now() - t0) }
    } catch (e) {
      // Mo hinh export voi dau vao TINH 640x640 -> chay 448 bao loi kich thuoc: tu chuyen ve 640 (tu chua lanh)
      if (S !== 640 && /dim|shape|size|448/i.test(String(e?.message || e))) dc.ep640 = true
      throw e
    } finally {
      dc.dangChay--
      donHangCho()
    }
  }
  if (dc.loai === 'ssd' && dc.ssd) {
    const du = await dc.ssd.detect(nguon, 40, Math.min(0.5, nguong))
    const hop = du
      .filter((p) => p.score >= Math.max(nguong, nguongLop[p.class] || 0))
      .map((p) => ({ x: p.bbox[0], y: p.bbox[1], w: p.bbox[2], h: p.bbox[3], score: p.score, label: p.class }))
    return { hop, ms: Math.round(performance.now() - t0) }
  }
  throw new Error('Mô hình đang tải, thử lại sau.')
}

/** Ve khung hinh + khung doi tuong + nhan len canvas hien thi */
export function veKetQua(canvas, nguon, w, h, hop) {
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d')
  ctx.drawImage(nguon, 0, 0, w, h)
  ctx.lineWidth = Math.max(2.5, w / 320)
  for (const b of hop) {
    const mau = mauCua(b.label)
    ctx.strokeStyle = mau
    ctx.strokeRect(b.x, b.y, b.w, b.h)
    veNhan(ctx, `${tenViet(b.label)} ${Math.round(b.score * 100)}%`, b.x, b.y, mau, '#0a1420', w)
  }
}

/** Gom khung theo nhan: so luong + do tin cay trung binh (%) — nhieu nhat truoc */
export function tomTatKetQua(hop) {
  const nhom = new Map()
  for (const b of hop) {
    const g = nhom.get(b.label) || { label: b.label, soLuong: 0, tongDiem: 0 }
    g.soLuong++
    g.tongDiem += b.score
    nhom.set(b.label, g)
  }
  const danhSach = [...nhom.values()]
    .sort((a, b) => b.soLuong - a.soLuong)
    .map((g) => ({
      label: g.label,
      ten: tenViet(g.label),
      soLuong: g.soLuong,
      tinCay: Math.round((g.tongDiem / g.soLuong) * 1000) / 10,
      mau: mauCua(g.label),
    }))
  const dem = Object.fromEntries(danhSach.map((x) => [x.label, x.soLuong]))
  const tinCayTB = hop.length ? Math.round((hop.reduce((s, b) => s + b.score, 0) / hop.length) * 1000) / 10 : 0
  return { danhSach, dem, tong: hop.length, tinCayTB }
}

/** Canh bao dieu phoi tu so dem (giong ban MVC) — mau: 'warn' | 'ok' | 'info' */
export function goiYDieuPhoi(dem) {
  const ds = []
  const xeTai = (dem.truck || 0) + (dem.bus || 0)
  if (xeTai >= 3) ds.push({ icon: 'bi-truck', mau: 'warn', chu: `${xeTai} xe tải trong khung hình — lưu lượng cổng đang cao, cân nhắc mở thêm làn check-in.` })
  else if (xeTai > 0) ds.push({ icon: 'bi-truck', mau: 'ok', chu: `${xeTai} xe tải — lưu lượng cổng bình thường.` })
  if ((dem.person || 0) >= 4) ds.push({ icon: 'bi-people', mau: 'warn', chu: `${dem.person} người — nhắc tuân thủ khu vực an toàn và đồ bảo hộ.` })
  if (dem.boat) ds.push({ icon: 'bi-water', mau: 'info', chu: `${dem.boat} tàu/phương tiện thủy trong khu nước.` })
  if (dem.car || dem.motorcycle) ds.push({ icon: 'bi-car-front', mau: 'info', chu: 'Phương tiện cá nhân xuất hiện — kiểm tra đăng ký ra vào cảng.' })
  return ds
}

/** Than yeu cau POST /api/camera-ai/luu tu ket qua tom tat */
export function taoBanGhiLuu(tt, ms, { nguon, model, viTri }) {
  return {
    nguon,
    model,
    viTri,
    thoiGianXuLy: ms,
    doTinCayTB: tt.tinCayTB,
    items: tt.danhSach.map((x) => ({ nhan: x.ten, nhanGoc: x.label, soLuong: x.soLuong, doTinCay: x.tinCay })),
  }
}
