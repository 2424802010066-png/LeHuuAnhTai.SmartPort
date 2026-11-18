import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Webcam dung chung cho dang nhap khuon mat va cai dat khuon mat
 * (thay phan "CAMERA DUNG CHUNG" cua wwwroot/Scripts/facelogin.js ban MVC).
 *
 *   const { videoRef, batDau, dungLai, chup, loiCamera, dangChay } = useWebcam()
 *   <video ref={videoRef} playsInline muted />
 *
 * Luong camera LUON duoc tat khi component bi go (roi trang, dong hop thoai)
 * — khong de den LED camera sang mai nhu quen clearInterval.
 */
export function useWebcam() {
  const videoRef = useRef(null)
  const luongRef = useRef(null)
  const canvasRef = useRef(null)
  // Tang moi lan tat camera: biet duoc luong vua xin xong con dung nua khong
  const lanRef = useRef(0)
  const [loiCamera, setLoiCamera] = useState('')
  const [dangChay, setDangChay] = useState(false)

  const dungLai = useCallback(() => {
    lanRef.current++
    const luong = luongRef.current
    if (luong) {
      luong.getTracks().forEach((t) => t.stop())
      luongRef.current = null
    }
    if (videoRef.current) videoRef.current.srcObject = null
    setDangChay(false)
  }, [])

  const batDau = useCallback(async () => {
    if (luongRef.current) return true
    if (!navigator.mediaDevices?.getUserMedia) {
      // Trinh duyet chi mo camera tren HTTPS hoac localhost
      setLoiCamera('Trình duyệt chỉ cho phép dùng camera qua HTTPS hoặc localhost.')
      return false
    }
    const lan = lanRef.current
    try {
      const luong = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
        audio: false,
      })
      // Trong luc cho cap quyen: component da bi go, da tat camera, hoac lan goi khac
      // (che do StrictMode goi hieu ung 2 lan) da gan luong roi -> tat luong vua xin
      if (!videoRef.current || lan !== lanRef.current || luongRef.current) {
        luong.getTracks().forEach((t) => t.stop())
        return !!luongRef.current
      }
      luongRef.current = luong
      videoRef.current.srcObject = luong
      await videoRef.current.play().catch(() => {})
      setLoiCamera('')
      setDangChay(true)
      return true
    } catch {
      setLoiCamera('Không mở được camera — hãy cấp quyền camera cho trình duyệt rồi thử lại.')
      return false
    }
  }, [])

  /**
   * Chup khung hinh hien tai -> chuoi JPEG base64 (data URL).
   * Canh dai 640px + chat luong 0,9 dung bang det_size cua ArcFace ben dich vu AI.
   */
  const chup = useCallback(() => {
    const v = videoRef.current
    if (!v || v.readyState < 2 || !v.videoWidth) return null
    if (!canvasRef.current) canvasRef.current = document.createElement('canvas')
    const canvas = canvasRef.current
    const ti = Math.min(1, 640 / Math.max(v.videoWidth, v.videoHeight))
    canvas.width = Math.round(v.videoWidth * ti)
    canvas.height = Math.round(v.videoHeight * ti)
    canvas.getContext('2d').drawImage(v, 0, 0, canvas.width, canvas.height)
    return canvas.toDataURL('image/jpeg', 0.9)
  }, [])

  useEffect(() => dungLai, [dungLai])

  return { videoRef, batDau, dungLai, chup, loiCamera, dangChay }
}

/** Cho n mili giay (chup nhieu khung cach nhau mot chut) */
export const cho = (ms) => new Promise((r) => setTimeout(r, ms))
