// Thu vien AI (ONNX Runtime, Tesseract, TensorFlow.js) nang vai chuc MB nen KHONG dong goi
// vao bundle React: chi tai khi nguoi dung mo man hinh can dung, va moi file chi tai 1 lan.
export const AI_VENDOR = '/Scripts/vendor/'
export const AI_MODELS = '/Content/models/'
export const AI_MAU = '/Content/img/'

const dangTai = new Map()

export function taiScript(src) {
  if (!dangTai.has(src)) {
    dangTai.set(
      src,
      new Promise((ok, loi) => {
        const s = document.createElement('script')
        s.src = src
        s.onload = ok
        s.onerror = () => {
          dangTai.delete(src)
          loi(new Error('Không tải được ' + src))
        }
        document.head.appendChild(s)
      }),
    )
  }
  return dangTai.get(src)
}

/**
 * Tesseract worker dung chung. ngonNgu: 'eng' (bien so, so container — chi chu Latin + so)
 * hoac 'vie' (bang lai — chu co dau; file vendor/tessdata/vie.traineddata.gz, xem DOC-KY-vie.txt).
 */
export async function taoWorkerTesseract(thamSo, logger, ngonNgu = 'eng') {
  await taiScript(AI_VENDOR + 'tess/tesseract.min.js')
  const worker = await window.Tesseract.createWorker(ngonNgu, 1, {
    workerPath: AI_VENDOR + 'tess/worker.min.js',
    corePath: AI_VENDOR + 'tess-core',
    langPath: AI_VENDOR + 'tessdata',
    logger: logger || (() => {}),
  })
  await worker.setParameters(thamSo)
  return worker
}

export function taiAnh(src) {
  return new Promise((ok, loi) => {
    const img = new Image()
    // Anh o MIEN KHAC (vd anh mau Unsplash): nap kieu CORS, khong thi canvas bi "nhiem ban" (tainted) va
    // getImageData / AI doc diem anh bao SecurityError. Anh cung mien, data:, blob: giu nguyen nhu cu.
    if (/^https?:/i.test(src) && new URL(src, location.href).origin !== location.origin) img.crossOrigin = 'anonymous'
    img.onload = () => ok(img)
    img.onerror = () => loi(new Error('Không mở được ảnh'))
    img.src = src
  })
}

export function docFileThanhAnh(file) {
  return new Promise((ok, loi) => {
    const rd = new FileReader()
    rd.onload = () => taiAnh(rd.result).then(ok, loi)
    rd.onerror = () => loi(new Error('Không đọc được file'))
    rd.readAsDataURL(file)
  })
}
