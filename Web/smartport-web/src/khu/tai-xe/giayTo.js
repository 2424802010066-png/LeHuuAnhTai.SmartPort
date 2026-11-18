/* ============================================================
   QUET GIAY TO TAI XE (chuyen tu Scripts/cccd.js cua ban MVC sang module ES)
   - Ma QR the CCCD gan chip: doc tu khung hinh camera hoac anh tai len,
     roi gui CHUOI THO len may chu de may chu tu boc truong.
     Bo doc: zxing-wasm (ZXing-C++ bien dich WebAssembly, vendor/zxing) — do thu tren khung webcam gia lap:
     ~19 ms/khung va doc duoc ma nho, trong khi jsQR mat ~1,6 giay/khung ma khong doc duoc.
     Trinh duyet khong nap duoc WebAssembly thi quay ve jsQR.
   - Bang lai: Tesseract OCR (du lieu tieng Viet) tra ve van ban tho, may chu boc so/hang/han.
   Ca hai KHONG tu tin ket qua o trinh duyet — client sua duoc.
   Du lieu ca nhan: khong ghi ra console, khong luu anh.
   ============================================================ */
import { AI_MODELS, AI_VENDOR, taiScript, taoWorkerTesseract } from '../../ai/taiThuVien'
import { chuanBiTensor, taoPhien } from '../ai-thi-giac/ortThiGiac'

/** Loi co thong bao tieng Viet hien thang cho nguoi dung */
function loiHienThi(thongBao) {
  const e = new Error(thongBao)
  e.hienThi = true
  return e
}

/** Nap thu vien jsQR (1 lan cho ca phien) — chi dung khi khong nap duoc zxing-wasm */
async function taiJsQr() {
  try {
    await taiScript(AI_VENDOR + 'qr/jsQR.js')
  } catch {
    throw loiHienThi('Không nạp được thư viện đọc mã QR — kiểm tra kết nối máy chủ.')
  }
  if (typeof window.jsQR !== 'function') throw loiHienThi('Không nạp được thư viện đọc mã QR.')
  return window.jsQR
}

/* ---------------- Bo doc QR: zxing-wasm, du phong jsQR ---------------- */
const THU_MUC_ZXING = AI_VENDOR + 'zxing/'
// Chi tim QR (bo qua 20+ dinh dang khac cho nhanh); tryHarder: do ky hon voi anh mo / nghieng;
// the CCCD in ma den tren nen sang nen khong can thu dao mau
const TUY_CHON_ZXING = { formats: ['QRCode'], tryHarder: true, tryInvert: false, maxNumberOfSymbols: 1 }
const UTF8 = new TextDecoder('utf-8', { fatal: true })
let huaBoDoc = null

/** Chuoi trong ma: giai byte tho bang UTF-8 (CCCD ghi tieng Viet UTF-8); khong phai UTF-8 thi lay text zxing tu doan */
function chuoiTuKetQua(kq) {
  if (!kq || kq.isValid === false) return null
  if (kq.bytes?.length) {
    try {
      return UTF8.decode(kq.bytes)
    } catch {
      // khong phai UTF-8 -> dung text ben duoi
    }
  }
  return kq.text || null
}

async function taoBoDocZxing() {
  if (typeof WebAssembly !== 'object') throw new Error('Trình duyệt không có WebAssembly')
  await taiScript(THU_MUC_ZXING + 'zxing-reader.js')
  const zx = window.ZXingWASM
  if (typeof zx?.readBarcodes !== 'function') throw new Error('Không nạp được zxing-wasm')
  // Mac dinh zxing-wasm tai file .wasm tu CDN jsDelivr -> doi sang file tren may chu (chay offline, khong phu thuoc ben ngoai)
  await zx.prepareZXingModule({
    overrides: { locateFile: (ten, tienTo) => (ten.endsWith('.wasm') ? THU_MUC_ZXING + ten : tienTo + ten) },
    fireImmediately: true,
  })
  // them: ghi de tuy chon theo tung lan thu (vd binarizer khac cho vung QR da khoanh)
  return { ten: 'zxing', doc: async (anh, them) => chuoiTuKetQua((await zx.readBarcodes(anh, them ? { ...TUY_CHON_ZXING, ...them } : TUY_CHON_ZXING))[0]) }
}

async function taoBoDocJsQr() {
  const jsQR = await taiJsQr()
  return { ten: 'jsQR', doc: async (anh) => jsQR(anh.data, anh.width, anh.height, { inversionAttempts: 'dontInvert' })?.data || null }
}

/**
 * Nap bo doc QR (1 lan cho ca phien): zxing-wasm, nap loi thi quay ve jsQR.
 * Tra ve { ten: 'zxing' | 'jsQR', doc(imageData) -> Promise<chuoi | null> }.
 */
export function taiBoDocQr() {
  if (!huaBoDoc) {
    huaBoDoc = taoBoDocZxing()
      .catch(() => taoBoDocJsQr())
      .catch((e) => {
        huaBoDoc = null // lan sau thu tai lai
        throw e
      })
  }
  return huaBoDoc
}

/** Ve nguon (video / anh) len canvas lam viec theo kich thuoc cho truoc, tra ve mang pixel */
function layPixel(canvas, nguon, rong, cao) {
  canvas.width = rong
  canvas.height = cao
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  ctx.drawImage(nguon, 0, 0, rong, cao)
  return ctx.getImageData(0, 0, rong, cao)
}

/** Cat vung (sx, sy, sw, sh) cua nguon roi ve thanh rong x cao (lam min khi phong to), tra ve mang pixel */
function layVung(canvas, nguon, sx, sy, sw, sh, rong, cao) {
  canvas.width = rong
  canvas.height = cao
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  ctx.imageSmoothingEnabled = true
  ctx.imageSmoothingQuality = 'high' // dat SAU khi doi kich thuoc: doi width/height la canvas ve trang thai mac dinh
  ctx.drawImage(nguon, sx, sy, sw, sh, 0, 0, rong, cao)
  return ctx.getImageData(0, 0, rong, cao)
}

// Khung net dut tren man hinh: .tx-vien-quet { inset: 12% } va video hien du khung (khong cat)
// -> tren khung hinh goc cung la le 12% moi canh
const LE_KHUNG = 0.12
// Anh dua vao bo doc rong toi da ~2000 px: du phong to ma QR nho ma moi lan doc van nhanh
const RONG_TOI_DA = 2000

/* ---------------- Mo hinh tu huan luyen cho QR (chay ngay trong trinh duyet, anh giay to KHONG roi may) ----------------
   - yolo26n_qr.onnx: khoanh vung ma QR trong khung hinh.
   - qr_khoi_phuc.onnx: mang U-Net khoi phuc vung QR bi mo/nhieu thanh anh den trang sac net.
   Ma QR co ma sua loi Reed-Solomon: neu mang "doan" sai o nao, zxing khong giai ma duoc chu khong sinh ra chuoi sai.
   Khong nap duoc mo hinh (may yeu, thieu file) thi van doc nhu cu — mo hinh chi la tang them. */
const S_DINH_VI = 640
const S_KHOI_PHUC = 320
const NGUONG_DINH_VI = 0.2
const BIEN_VUNG = 0.16 // noi vung QR them 16% moi phia: quiet zone + sai so khung
export const SO_LUOT_VIDEO = 4

let huaMoHinhQr = null
let moHinhDaNap = null // da nap xong: vong quet camera chi dung khi san sang, khong dung lai cho tai mo hinh

/** Nap 2 mo hinh QR (1 lan cho ca phien). Loi nap thi tra ve truong null, khong nem loi */
export function chuanBiDinhVi() {
  if (!huaMoHinhQr) {
    huaMoHinhQr = (async () => {
      const [dv, kp] = await Promise.allSettled([
        taoPhien(AI_MODELS + 'yolo26n_qr.onnx', { uuTienGpu: true, tenMoHinh: 'bộ định vị QR' }),
        taoPhien(AI_MODELS + 'qr_khoi_phuc.onnx', { uuTienGpu: true, tenMoHinh: 'bộ khôi phục QR' }),
      ])
      moHinhDaNap = { dinhVi: dv.status === 'fulfilled' ? dv.value : null, khoiPhuc: kp.status === 'fulfilled' ? kp.value : null }
      return moHinhDaNap
    })()
  }
  return huaMoHinhQr
}

/** Chuoi co dang QR CCCD (12 so + it nhat 5 dau |)? Hoa van nhieu doi khi bi doc thanh vai chu so (gia duong tinh). */
const giongCccd = (s) => /^\s*\d{12}\s*\|/.test(s || '') && (s.match(/\|/g) || []).length >= 5

/** YOLO khoanh vung QR -> [{x0,y0,x1,y1,diem}] theo diem giam dan (toi da 2 vung) */
async function timVungQr(md, nguon, w, h, canvas) {
  const { phien, ort } = md.dinhVi
  const { tensor, scale, padX, padY } = chuanBiTensor(ort, nguon, w, h, S_DINH_VI, canvas)
  const kq = await phien.run({ [phien.inputNames[0]]: tensor })
  const o = kq[phien.outputNames[0]]
  const ds = []
  // Dau ra YOLO26 end-to-end [1,N,6]: x1 y1 x2 y2 diem lop (khong can NMS)
  for (let i = 0; i < o.dims[1]; i++) {
    const b = i * 6
    if (o.data[b + 4] < NGUONG_DINH_VI) continue
    ds.push({ x0: (o.data[b] - padX) / scale, y0: (o.data[b + 1] - padY) / scale, x1: (o.data[b + 2] - padX) / scale, y1: (o.data[b + 3] - padY) / scale, diem: o.data[b + 4] })
  }
  return ds.sort((a, b) => b.diem - a.diem).slice(0, 2)
}

/** Vung vuong bao quanh QR (co bien), day vao trong anh -> {sx,sy,c}; c <= canh ngan cua anh */
function vungVuong(v, w, h) {
  const c = Math.min(w, h, Math.max(v.x1 - v.x0, v.y1 - v.y0) * (1 + 2 * BIEN_VUNG))
  const sx = Math.max(0, Math.min(w - c, (v.x0 + v.x1) / 2 - c / 2))
  const sy = Math.max(0, Math.min(h - c, (v.y0 + v.y1) / 2 - c / 2))
  return { sx, sy, c }
}

/** U-Net khoi phuc: ImageData S x S -> ImageData den trang cung kich thuoc */
async function khoiPhucQr(md, anh) {
  const { phien, ort } = md.khoiPhuc
  const { width: w, height: h, data: d } = anh
  const t = new Float32Array(w * h)
  for (let i = 0; i < w * h; i++) t[i] = (0.299 * d[4 * i] + 0.587 * d[4 * i + 1] + 0.114 * d[4 * i + 2]) / 255
  const kq = await phien.run({ [phien.inputNames[0]]: new ort.Tensor('float32', t, [1, 1, h, w]) })
  const o = kq[phien.outputNames[0]].data
  const ra = new ImageData(w, h)
  for (let i = 0; i < w * h; i++) {
    const v = Math.max(0, Math.min(255, Math.round(o[i] * 255)))
    ra.data[4 * i] = v
    ra.data[4 * i + 1] = v
    ra.data[4 * i + 2] = v
    ra.data[4 * i + 3] = 255
  }
  return ra
}

/** Doc 1 vung QR theo 1 cach: 'kp' khoi phuc roi doc, 'kpGh' khoi phuc + binarizer toan cuc, 'x3' phong to 3 lan roi doc */
async function docMotVung(bo, md, nguon, v, w, h, canvas, cach) {
  const { sx, sy, c } = vungVuong(v, w, h)
  if (cach === 'x3' || !md.khoiPhuc) {
    const canh = Math.round(Math.max(S_KHOI_PHUC, Math.min(960, c * 3)))
    return bo.doc(layVung(canvas, nguon, sx, sy, c, c, canh, canh), { binarizer: 'LocalAverage', tryRotate: true })
  }
  const anh = await khoiPhucQr(md, layVung(canvas, nguon, sx, sy, c, c, S_KHOI_PHUC, S_KHOI_PHUC))
  return bo.doc(anh, { binarizer: cach === 'kpGh' ? 'GlobalHistogram' : 'LocalAverage', tryRotate: true })
}

// Chuoi khong phai CCCD doc duoc lan truoc (camera): phai doc lap lai moi bao len, tranh gia duong tinh tu hoa van nhieu
let chuoiLaTruoc = null

/**
 * Doc ma QR tu khung hinh camera hien tai -> chuoi tho hoac null.
 * zxing luan phien 4 cach theo luot:
 *   - luot 0: ca khung o do phan giai goc (nhanh; bat ca khi the lech khoi khung net dut);
 *   - luot 1: vung khung net dut PHONG TO toi 2 lan — ma QR tren CCCD rat nho (~2 px/o tren webcam 720p),
 *     phong to thi bo doc moi tach duoc tung o (do thu: ti le doc tu 25% len 41%);
 *   - luot 2: YOLO khoanh vung QR -> U-Net khoi phuc anh mo -> doc;
 *   - luot 3: vung YOLO phong to 3 lan -> doc.
 * jsQR (du phong) cham hon ~80 lan -> chi doc ca khung da thu ve <= 1280 px nhu truoc.
 */
export async function docQrTuVideo(video, canvas, luot = 0) {
  const w = video?.videoWidth
  const h = video?.videoHeight
  if (!w || !h || !canvas) return null
  const bo = await taiBoDocQr()
  let chuoi = null
  const buoc = luot % SO_LUOT_VIDEO
  if (bo.ten === 'zxing' && buoc >= 2) {
    const md = moHinhDaNap
    if (md?.dinhVi) {
      const ds = await timVungQr(md, video, w, h, canvas)
      for (const v of ds) {
        chuoi = await docMotVung(bo, md, video, v, w, h, canvas, buoc === 2 ? 'kp' : 'x3')
        if (giongCccd(chuoi)) return chuoi
      }
      return null
    }
    // Khong co mo hinh: quay ve 2 cach cu
  }
  let anh
  if (bo.ten !== 'zxing' || buoc % 2 === 0) {
    const tiLe = Math.min(1, (bo.ten === 'zxing' ? RONG_TOI_DA : 1280) / w)
    anh = layPixel(canvas, video, Math.round(w * tiLe), Math.round(h * tiLe))
  } else {
    const sx = Math.round(w * LE_KHUNG)
    const sy = Math.round(h * LE_KHUNG)
    const sw = w - 2 * sx
    const sh = h - 2 * sy
    const tiLe = Math.min(2, RONG_TOI_DA / sw)
    anh = layVung(canvas, video, sx, sy, sw, sh, Math.round(sw * tiLe), Math.round(sh * tiLe))
  }
  chuoi = await bo.doc(anh)
  if (!chuoi || giongCccd(chuoi)) return chuoi
  // Chuoi la (QR khac hoac gia duong tinh): chi bao khi doc lai duoc dung chuoi do lan nua
  if (chuoi === chuoiLaTruoc) return chuoi
  chuoiLaTruoc = chuoi
  return null
}

/**
 * Doc ma QR tu anh tai len -> chuoi tho hoac null.
 * zxing: doc o canh dai <= 3000 px (ben trong tu thu them ban thu nho); anh nho (chup xa, QR chi vai chuc px)
 * thi thu them ban phong to 2 lan. jsQR (du phong): thu ban thu nho 1600 px truoc roi moi den 3000 px.
 */
export async function docQrTuAnh(img, canvas) {
  const bo = await taiBoDocQr()
  const w = img.naturalWidth || img.width
  const h = img.naturalHeight || img.height
  if (!w || !h) return null
  const canhDai = Math.max(w, h)
  const dsCanh = bo.ten === 'zxing' ? [Math.min(canhDai, 3000), Math.min(canhDai * 2, 3000)] : [Math.min(canhDai, 1600), Math.min(canhDai, 3000)]
  let daThu = 0
  let chuoiKhac = null // doc duoc nhung khong phai dang CCCD: giu lai, thu het cach khac roi moi tra ve
  for (const canh of dsCanh) {
    if (canh === daThu) continue // anh da lon san -> khong thu lai cung kich thuoc
    daThu = canh
    const rong = Math.max(1, Math.round((w * canh) / canhDai))
    const cao = Math.max(1, Math.round((h * canh) / canhDai))
    const anh = canh > canhDai ? layVung(canvas, img, 0, 0, w, h, rong, cao) : layPixel(canvas, img, rong, cao)
    const chuoi = await bo.doc(anh)
    if (giongCccd(chuoi)) return chuoi
    chuoiKhac = chuoiKhac || chuoi
  }
  if (bo.ten === 'zxing') {
    const md = await chuanBiDinhVi()
    if (md.dinhVi) {
      for (const v of await timVungQr(md, img, w, h, canvas)) {
        for (const cach of ['kp', 'x3', 'kpGh']) {
          const chuoi = await docMotVung(bo, md, img, v, w, h, canvas, cach)
          if (giongCccd(chuoi)) return chuoi
        }
      }
    }
  }
  return chuoiKhac
}

/* ---------------- OCR bang lai (Tesseract, dung lai worker ca phien) ---------------- */
let huaWorker = null
let baoTienDo = () => {}

function layWorkerOcr() {
  if (!huaWorker) {
    // Khong gioi han ky tu: bang lai co chu, so, dau ngay thang — may chu tu loc bang bieu thuc chinh quy.
    // Du lieu TIENG VIET (vie, 1,4 MB): doc duoc nhan co dau "Hạng/Class", "Có giá trị đến/Expires"
    // (du lieu eng 10,9 MB lam mat dau) — may chu so khop tren ban bo dau nen doc kieu nao cung duoc.
    huaWorker = taoWorkerTesseract({}, (m) => {
      if (m.status === 'recognizing text') baoTienDo(m.progress || 0)
    }, 'vie').catch(() => {
      huaWorker = null // lan sau thu tai lai
      throw loiHienThi('Chưa nạp được bộ đọc chữ Tesseract — thử lại sau.')
    })
  }
  return huaWorker
}

/**
 * Xoay anh cho dong chu nam ngang truoc khi OCR: Tesseract giam do chinh xac ro ret khi the nghieng vai do.
 * Tim goc lam bieu do hang cua diem toi "sac" nhat (dong chu thang hang thi bieu do co dinh cao xen ke khe trong).
 * Goc nho hon 0,4 do thi giu nguyen anh. Tra ve { anh, goc }.
 */
function xoayThang(nguon, w, h) {
  const rong = 700
  const cao = Math.round((h * rong) / w)
  const c = document.createElement('canvas')
  c.width = rong
  c.height = cao
  const x = c.getContext('2d', { willReadFrequently: true })
  x.drawImage(nguon, 0, 0, rong, cao)
  const d = x.getImageData(0, 0, rong, cao).data
  // Diem NET CHU: toi hon han trung binh lan can (anh tich phan) — mang toi cua nen / bong khong lot vao,
  // chi xet 70% giua khung (the nam giua, nen ban lon xon ben ngoai)
  const tp = new Float64Array((rong + 1) * (cao + 1))
  for (let yy = 0; yy < cao; yy++) {
    let hang = 0
    for (let xx = 0; xx < rong; xx++) {
      hang += d[4 * (yy * rong + xx) + 1]
      tp[(yy + 1) * (rong + 1) + xx + 1] = tp[yy * (rong + 1) + xx + 1] + hang
    }
  }
  const R = 9
  const diem = []
  const leX = Math.round(rong * 0.15)
  const leY = Math.round(cao * 0.15)
  for (let yy = leY; yy < cao - leY; yy++) {
    for (let xx = leX; xx < rong - leX; xx++) {
      const x0 = Math.max(0, xx - R)
      const y0 = Math.max(0, yy - R)
      const x1 = Math.min(rong, xx + R + 1)
      const y1 = Math.min(cao, yy + R + 1)
      const tb = (tp[y1 * (rong + 1) + x1] - tp[y0 * (rong + 1) + x1] - tp[y1 * (rong + 1) + x0] + tp[y0 * (rong + 1) + x0]) / ((x1 - x0) * (y1 - y0))
      if (d[4 * (yy * rong + xx) + 1] < tb - 28) diem.push(xx, yy)
    }
  }
  if (diem.length < 400) return { anh: nguon, goc: 0 }
  const diemGoc = (doDo) => {
    const r = (doDo * Math.PI) / 180
    const sin = Math.sin(r)
    const cos = Math.cos(r)
    const n = cao + rong
    const hs = new Int32Array(n)
    for (let i = 0; i < diem.length; i += 2) {
      const y = Math.round(diem[i + 1] * cos - diem[i] * sin + rong * 0.5)
      if (y >= 0 && y < n) hs[y]++
    }
    let s = 0
    for (let i = 1; i < n; i++) s += (hs[i] - hs[i - 1]) ** 2
    return s
  }
  let tot = 0
  let sTot = -1
  for (let g = -10; g <= 10; g += 1) {
    const s = diemGoc(g)
    if (s > sTot) [sTot, tot] = [s, g]
  }
  for (let g = tot - 1; g <= tot + 1; g += 0.25) {
    const s = diemGoc(g)
    if (s > sTot) [sTot, tot] = [s, g]
  }
  if (Math.abs(tot) < 0.4) return { anh: nguon, goc: 0 }
  const ra = document.createElement('canvas')
  ra.width = w
  ra.height = h
  const k = ra.getContext('2d')
  k.fillStyle = '#fff'
  k.fillRect(0, 0, w, h)
  k.translate(w / 2, h / 2)
  k.rotate((-tot * Math.PI) / 180)
  k.drawImage(nguon, -w / 2, -h / 2, w, h)
  return { anh: ra, goc: tot }
}

/**
 * Nhan dang chu tren anh bang lai -> van ban tho (chua boc truong).
 * khiTienDo(0..1) bao tien do doc de ve thanh tien trinh.
 */
export async function docChuBangLai(img, khiTienDo) {
  baoTienDo = khiTienDo || (() => {})
  const worker = await layWorkerOcr()
  const w = img.naturalWidth || img.width
  const h = img.naturalHeight || img.height
  // Anh dien thoai 4000 px doc rat lau ma khong ro hon: thu ve canh dai 2400 px
  const tiLe = w && h ? Math.min(1, 2400 / Math.max(w, h)) : 1
  let nguon = img
  if (tiLe < 1) {
    nguon = document.createElement('canvas')
    nguon.width = Math.round(w * tiLe)
    nguon.height = Math.round(h * tiLe)
    nguon.getContext('2d').drawImage(img, 0, 0, nguon.width, nguon.height)
  }
  // The nghieng >= 1,5 do: doc them ban da xoay thang. Ghep ban xoay truoc (do chu chuan hon khi nghieng), ban goc sau de
  // may chu van co cho lay truong ma ban xoay doc thieu; the gan nhu thang thi giu nguyen 1 lan doc nhu cu.
  const { anh: banThang, goc } = xoayThang(nguon, nguon === img ? w : nguon.width, nguon === img ? h : nguon.height)
  const kq = await worker.recognize(nguon)
  if (Math.abs(goc) >= 1.5) {
    const kqThang = await worker.recognize(banThang)
    baoTienDo(1)
    return (kqThang?.data?.text || '') + '\n' + (kq?.data?.text || '')
  }
  baoTienDo(1)
  return kq?.data?.text || ''
}

/* ---------------- Quy tac hien thi dung chung ---------------- */

/** "yyyy-MM-dd" cua hom nay theo gio may (so sanh chuoi voi gia tri <input type="date">) */
export function homNayInput() {
  const d = new Date()
  const hai = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${hai(d.getMonth() + 1)}-${hai(d.getDate())}`
}

/** So ngay tu hom nay den ngay "yyyy-MM-dd" (am = da qua) */
export function soNgayDen(ngay) {
  if (!ngay) return null
  const [y, m, d] = ngay.split('-').map(Number)
  if (!y || !m || !d) return null
  const bayGio = new Date()
  const homNay = new Date(bayGio.getFullYear(), bayGio.getMonth(), bayGio.getDate())
  return Math.round((new Date(y, m - 1, d) - homNay) / 86400000)
}
