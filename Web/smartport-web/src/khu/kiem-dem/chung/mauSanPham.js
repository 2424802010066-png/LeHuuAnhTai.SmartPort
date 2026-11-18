/* ============================================================
   DANG KY SAN PHAM MAU (few-shot) — chuyen tu phan "mau" cua tally.js
   4-6 anh chup / video quay tong quat / chup truc tiep tu camera ->
   moi anh rut ra 1 histogram HSV. Khi dem video, vat nao co do tuong
   dong >= 0,74 voi MOT trong cac mau moi duoc tinh la "dung san pham".
   ============================================================ */
import { AI_MAU } from '../../../ai/taiThuVien'
import { hsvHist } from './mauSac'

export const SO_MAU_TOI_DA = 6
export const SO_MAU_TOI_THIEU = 4
/** Bo 6 anh mau chuan co san trong wwwroot (chai nuoc cam) */
export const ANH_MAU_DEMO = Array.from({ length: SO_MAU_TOI_DA }, (_, i) => AI_MAU + 'mau-sp-' + (i + 1) + '.png?v=1')

/**
 * Rut dac trung 1 anh mau tu Image / the video / canvas.
 * @returns {{hist:number[], anhNho:string}|null} null khi nguon chua co kich thuoc
 */
export function docMau(nguon, rong, cao) {
  const w = rong || nguon.naturalWidth || nguon.videoWidth || nguon.width || 0
  const h = cao || nguon.naturalHeight || nguon.videoHeight || nguon.height || 0
  if (!w || !h) return null
  const cv = document.createElement('canvas')
  const sc = Math.min(1, 420 / Math.max(w, h))
  cv.width = Math.max(2, Math.round(w * sc))
  cv.height = Math.max(2, Math.round(h * sc))
  cv.getContext('2d', { willReadFrequently: true }).drawImage(nguon, 0, 0, cv.width, cv.height)
  const hist = hsvHist(cv, { x1: 0, y1: 0, x2: cv.width, y2: cv.height })

  // Anh nho 54x54 kieu "cover" de hien thanh mau da dang ky
  const th = document.createElement('canvas')
  th.width = 54
  th.height = 54
  const r = Math.max(54 / cv.width, 54 / cv.height)
  th.getContext('2d').drawImage(cv, (54 - cv.width * r) / 2, (54 - cv.height * r) / 2, cv.width * r, cv.height * r)
  return { hist, anhNho: th.toDataURL('image/jpeg', 0.8) }
}

/** Doc 1 file anh thanh the Image da tai xong */
export function anhTuFile(file) {
  return new Promise((ok, loi) => {
    const rd = new FileReader()
    rd.onload = () => {
      const img = new Image()
      img.onload = () => ok(img)
      img.onerror = () => loi(new Error('Không mở được ảnh mẫu.'))
      img.src = rd.result
    }
    rd.onerror = () => loi(new Error('Không đọc được file ảnh.'))
    rd.readAsDataURL(file)
  })
}

export function taiAnhTheoUrl(url) {
  return new Promise((ok, loi) => {
    const img = new Image()
    img.onload = () => ok(img)
    img.onerror = () => loi(new Error('Không tải được ảnh: ' + url))
    img.src = url
  })
}

/**
 * Trich deu soLuong khung hinh tu VIDEO MAU de lam anh mau.
 * webm do MediaRecorder ghi bao duration = Infinity -> phai ep seek that xa
 * cho trinh duyet tinh lai duration that roi moi chia moc thoi gian.
 */
export function trichKhungTuVideo(file, soLuong = SO_MAU_TOI_DA) {
  return new Promise((ok, loi) => {
    const v = document.createElement('video')
    const url = URL.createObjectURL(file)
    const ketQua = []
    let idx = 0
    let moc = []
    const xong = (e) => {
      URL.revokeObjectURL(url)
      v.removeAttribute('src')
      if (e) loi(e)
      else ok(ketQua)
    }
    const buoc = () => {
      if (idx >= moc.length) {
        xong()
        return
      }
      v.currentTime = moc[idx]
    }
    v.addEventListener('seeked', () => {
      const cv = document.createElement('canvas')
      cv.width = v.videoWidth
      cv.height = v.videoHeight
      if (cv.width && cv.height) {
        cv.getContext('2d').drawImage(v, 0, 0)
        ketQua.push(cv)
      }
      idx++
      buoc()
    })
    v.addEventListener('error', () => xong(new Error('Không đọc được video mẫu.')))
    v.addEventListener('loadedmetadata', () => {
      const batDau = () => {
        moc = Array.from({ length: soLuong }, (_, i) => (v.duration * (i + 0.5)) / soLuong)
        buoc()
      }
      if (isFinite(v.duration) && v.duration > 0) {
        batDau()
        return
      }
      const khiCoDuration = () => {
        if (!isFinite(v.duration) || v.duration <= 0) return
        v.removeEventListener('durationchange', khiCoDuration)
        batDau()
      }
      v.addEventListener('durationchange', khiCoDuration)
      v.currentTime = 1e7
    })
    v.muted = true
    v.playsInline = true
    v.src = url
  })
}
