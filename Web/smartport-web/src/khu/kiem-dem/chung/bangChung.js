/* ============================================================
   GUI BANG CHUNG do AI tren trinh duyet sinh ra ve SmartPort.Api
   (thay 2 lan fetch rieng Security/Report + Security/UploadMedia cua ban MVC
    bang 1 lan goi — server tu ghi su co an ninh + luu anh vao Kho media AI).
   Anh chup tu canvas KHONG bao gio chan luong AI: loi mang chi ghi lai de
   hien nhan "chua gui duoc", vong lap camera van chay tiep.
   ============================================================ */
import { goiApi } from '../../../api/client'

/** Canvas -> Blob JPEG (chat luong 0,85 nhu ban MVC) */
export function canvasThanhBlob(canvas, chatLuong = 0.85) {
  return new Promise((ok) => canvas.toBlob((b) => ok(b), 'image/jpeg', chatLuong))
}

/** Anh thu nho dang data URL de luu kem su co an ninh (Phong an ninh dat thang vao the img) */
export function anhThuNho(canvas, rongToiDa = 520, chatLuong = 0.75) {
  const nho = document.createElement('canvas')
  const sc = Math.min(1, rongToiDa / Math.max(canvas.width, 1))
  nho.width = Math.round(canvas.width * sc)
  nho.height = Math.round(canvas.height * sc)
  nho.getContext('2d').drawImage(canvas, 0, 0, nho.width, nho.height)
  return nho.toDataURL('image/jpeg', chatLuong)
}

/**
 * Anh bang chung cua man hinh Kiem dem (nguon KiemDem trong Kho media AI).
 * @param {boolean} baoAnNinh true = bao them Phong an ninh su co "Thùng hàng lỗi"
 */
export async function guiBangChungKiemDem(canvas, { moTa, baoAnNinh = false } = {}) {
  const blob = await canvasThanhBlob(canvas)
  if (!blob) throw new Error('Không chụp được ảnh từ khung hình.')
  const fd = new FormData()
  fd.append('anh', blob, 'kiem-dem.jpg')
  fd.append('moTa', moTa || '')
  fd.append('baoAnNinh', baoAnNinh ? 'true' : 'false')
  if (baoAnNinh) fd.append('anhNho', anhThuNho(canvas))
  return goiApi('/api/kiem-dem/bang-chung', { method: 'POST', body: fd })
}

/**
 * Su co tai xe / khoang hang: bao Phong an ninh kem anh thu nho, dong thoi luu
 * anh day du vao Kho media AI (nguon CameraAI).
 * @param {{loai:string, mucDo:string, viTri:string, moTa:string}} tt
 */
export async function guiSuCoTaiXe(canvas, tt) {
  const fd = new FormData()
  fd.append('loai', tt.loai)
  fd.append('mucDo', tt.mucDo || 'Chú ý')
  fd.append('viTri', tt.viTri || '')
  fd.append('moTa', tt.moTa || '')
  if (canvas?.width) {
    fd.append('anhNho', anhThuNho(canvas))
    const blob = await canvasThanhBlob(canvas)
    if (blob) fd.append('anh', blob, 'dms.jpg')
  }
  return goiApi('/api/giam-sat-tai-xe/su-co', { method: 'POST', body: fd })
}
