/* ============================================================
   CAMERA TRINH DUYET dung chung cho 3 khung hinh cua khu Kiem dem:
   camera kiem dem, camera tai xe (DMS), camera khoang hang (Cargo).
   React cho phep vao/ra man hinh lien tuc — moi luong MO ra deu phai
   duoc DONG lai o ham don dep cua useEffect, neu khong den camera
   van sang va tab giu thiet bi.
   ============================================================ */

/**
 * Mo camera va gan vao the video.
 * @returns {Promise<MediaStream>} nem Error co thong bao tieng Viet neu khong mo duoc
 */
export async function moCamera(video, rongLyTuong = 1280) {
  if (!navigator.mediaDevices?.getUserMedia)
    throw new Error('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost — hãy mở trang qua https:// rồi thử lại.')
  let luong
  try {
    luong = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: rongLyTuong } }, audio: false })
  } catch {
    throw new Error('Không truy cập được camera (cần cấp quyền cho trình duyệt).')
  }
  if (video) {
    video.srcObject = luong
    try {
      await video.play()
    } catch {
      /* mot so trinh duyet chan autoplay: khung hinh van ve duoc khi nguoi dung tuong tac */
    }
  }
  return luong
}

/** Tat camera + go khoi the video (goi duoc nhieu lan, khong loi khi luong da dong) */
export function dongCamera(luong, video) {
  try {
    luong?.getTracks().forEach((t) => t.stop())
  } catch {
    /* bo qua */
  }
  if (video) {
    try {
      video.pause()
    } catch {
      /* bo qua */
    }
    video.srcObject = null
  }
}

/** Ve khung hinh hien tai cua the video/anh vao canvas (dat lai kich thuoc canvas theo nguon) */
export function veLenCanvas(canvas, nguon, w, h) {
  if (!canvas || !w || !h) return false
  canvas.width = w
  canvas.height = h
  canvas.getContext('2d', { willReadFrequently: true }).drawImage(nguon, 0, 0, w, h)
  return true
}
