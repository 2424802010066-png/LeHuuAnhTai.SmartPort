import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { AI_MAU, docFileThanhAnh, taiAnh } from '../../ai/taiThuVien'
import { goiApi } from '../../api/client'
import './an-ninh.css'
import {
  beepCanhBao,
  chuong,
  coiBatDau,
  coiDangKeu,
  coiDung,
  docMaContainer,
  khoiDongAnNinh,
  taoBoGhiHinh,
  taoBoGiamSat,
} from './anNinhAi'

const VI_TRI = ['Cổng chính', 'Cổng phụ', 'Bãi container', 'Cầu bến', 'Kho CFS']
const NHIP_QUET = 1300 // mot nhip giam sat / 1,3 giay (giong ban MVC)
const CHONG_LAP = 30000 // cung mot loai su co chi bao lai sau 30 giay

/**
 * TRẠM CAMERA AN NINH AI (thay Admin/Security/Monitor + Scripts/security.js của bản MVC).
 * Toàn bộ AI chạy trong trình duyệt: xác nhận số container 2 bước (OCR ISO 6346) và
 * giám sát nguy hiểm (lửa · người ngã · vật thể đứng im). Có sự cố thì hú còi, chụp ảnh,
 * đóng gói video WebM rồi báo về phòng an ninh kèm vị trí camera.
 */
export default function TramCamera() {
  const [engine, setEngine] = useState({ trangThai: 'dang-tai' })
  const [thongBao, setThongBao] = useState('Đang tải engine AI…')
  const [dangQuay, setDangQuay] = useState(false)
  const [dangGiamSat, setDangGiamSat] = useState(false)
  const [dangBan, setDangBan] = useState(false)
  const [buoc, setBuoc] = useState(0) // 0 = chờ quét lần 1 · 1 = đã có mã, chờ đối chiếu
  const [ketQua, setKetQua] = useState(null) // { kieu: 'lan1' | 'khop' | 'lech', ma, maCu }
  const [meta, setMeta] = useState('—')
  const [nhatKy, setNhatKy] = useState([])
  const [viTri, setViTri] = useState(VI_TRI[0])
  const [tuHuCoi, setTuHuCoi] = useState(true)
  const [coiKeu, setCoiKeu] = useState(false)
  const [coHinh, setCoHinh] = useState(false)

  const canvasRef = useRef(null)
  const videoRef = useRef(null)
  const canvasPhuRef = useRef(null)
  const fileRef = useRef(null)
  const streamRef = useRef(null)
  const anhTinhRef = useRef(null)
  const engineRef = useRef(null)
  const ghiHinhRef = useRef(null)
  const giamSatRef = useRef(null)
  const nhipRef = useRef(null)
  const dangGiamSatRef = useRef(false)
  const banRef = useRef(false)
  const conGanRef = useRef(true)
  const baoCuoiRef = useRef({}) // loai -> moc thoi gian bao gan nhat
  const viTriRef = useRef(viTri)
  const huCoiRef = useRef(tuHuCoi)
  const luuLan1Ref = useRef(null)

  viTriRef.current = viTri
  huCoiRef.current = tuHuCoi

  const ghiNhatKy = useCallback((chu) => {
    if (!conGanRef.current) return
    setNhatKy((cu) => [{ id: Date.now() + Math.random(), luc: new Date(), chu }, ...cu].slice(0, 12))
  }, [])

  /* ---------------- Nguon hinh: ve khung hien tai vao canvas ---------------- */
  const veNguon = useCallback(() => {
    const c = canvasRef.current
    if (!c) return false
    const ctx = c.getContext('2d')
    const v = videoRef.current
    if (streamRef.current && v && v.readyState >= 2 && v.videoWidth) {
      if (c.width !== v.videoWidth || c.height !== v.videoHeight) {
        c.width = v.videoWidth
        c.height = v.videoHeight
      }
      ctx.drawImage(v, 0, 0)
      return true
    }
    const img = anhTinhRef.current
    if (img) {
      c.width = img.naturalWidth
      c.height = img.naturalHeight
      ctx.drawImage(img, 0, 0)
      return true
    }
    return false
  }, [])

  /* ---------------- Nap engine AI 1 lan + don dep khi roi trang ---------------- */
  useEffect(() => {
    conGanRef.current = true
    khoiDongAnNinh()
      .then((e) => {
        if (!conGanRef.current) return
        engineRef.current = e
        giamSatRef.current = taoBoGiamSat(e)
        setEngine({ trangThai: 'san-sang', yolo: !!e.yolo, ocr: !!e.worker })
        setThongBao('SmartGuard sẵn sàng — bật camera hoặc chọn ảnh để bắt đầu.')
      })
      .catch((err) => {
        if (!conGanRef.current) return
        setEngine({ trangThai: 'loi', loi: err.message })
        setThongBao(err.message)
      })
    return () => {
      // ROI TRANG: dung webcam, huy vong lap ve, dong bo ghi hinh va tat coi
      conGanRef.current = false
      clearInterval(nhipRef.current)
      nhipRef.current = null
      dangGiamSatRef.current = false
      ghiHinhRef.current?.dung()
      ghiHinhRef.current = null
      streamRef.current?.getTracks().forEach((t) => t.stop())
      streamRef.current = null
      coiDung()
    }
  }, [])

  /* ---------------- Vong lap ve khung hinh webcam (huy khi roi trang) ---------------- */
  useEffect(() => {
    let id = requestAnimationFrame(function ve() {
      // Dang giam sat thi chinh nhip quet ve (kem khung nhan dien) — khong ve de len
      if (!dangGiamSatRef.current) veNguon()
      id = requestAnimationFrame(ve)
    })
    return () => cancelAnimationFrame(id)
  }, [veNguon])

  /* ---------------- Bao ve phong an ninh ---------------- */
  const baoAnNinh = useCallback(
    async (loai, mucDo, moTa) => {
      const now = Date.now()
      if (baoCuoiRef.current[loai] && now - baoCuoiRef.current[loai] < CHONG_LAP) return
      baoCuoiRef.current[loai] = now

      if (mucDo === 'Nguy hiểm' && huCoiRef.current) {
        coiBatDau(6)
        setCoiKeu(true)
        setTimeout(() => conGanRef.current && setCoiKeu(coiDangKeu()), 6200)
      }

      // Anh bang chung thu nho (canh lon nhat 520px) tu khung hinh hien tai
      const c = canvasRef.current
      let anh = null
      if (c && c.width) {
        const thu = document.createElement('canvas')
        const sc = Math.min(1, 520 / Math.max(c.width, 1))
        thu.width = Math.round(c.width * sc)
        thu.height = Math.round(c.height * sc)
        thu.getContext('2d').drawImage(c, 0, 0, thu.width, thu.height)
        anh = thu.toDataURL('image/jpeg', 0.75)
      }

      try {
        const kq = await goiApi('/api/an-ninh/su-co', {
          method: 'POST',
          body: { loai, mucDo, viTri: viTriRef.current, moTa, anh },
        })
        if (!kq?.id) return // 30 giay truoc da co su co cung loai + cung vi tri
        ghiNhatKy((mucDo === 'Nguy hiểm' ? '[NGUY HIỂM] ' : '[Chú ý] ') + moTa + ' — đã báo phòng an ninh (#' + kq.id + ' · ' + viTriRef.current + ')')
        const blob = await ghiHinhRef.current?.dongGoi()
        if (!blob || !conGanRef.current) return
        const fd = new FormData()
        fd.append('file', blob, 'evidence.webm')
        await goiApi('/api/an-ninh/su-co/' + kq.id + '/video', { method: 'POST', body: fd })
        ghiNhatKy('Đã gửi video bằng chứng cho sự cố #' + kq.id + '.')
      } catch (e) {
        ghiNhatKy('Không gửi được cảnh báo về phòng an ninh: ' + e.message)
      }
    },
    [ghiNhatKy],
  )

  /* ---------------- Camera ---------------- */
  function tatCamera() {
    dungGiamSat()
    ghiHinhRef.current?.dung()
    ghiHinhRef.current = null
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
    setDangQuay(false)
    setThongBao('Đã tắt camera.')
  }

  async function batCamera() {
    if (!navigator.mediaDevices?.getUserMedia) {
      setThongBao('Trình duyệt chỉ cho phép dùng camera qua HTTPS hoặc localhost.')
      return
    }
    try {
      const s = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 } }, audio: false })
      if (!conGanRef.current) {
        s.getTracks().forEach((t) => t.stop())
        return
      }
      streamRef.current = s
      anhTinhRef.current = null
      videoRef.current.srcObject = s
      await videoRef.current.play()
      ghiHinhRef.current = taoBoGhiHinh(s)
      setDangQuay(true)
      setCoHinh(true)
      setThongBao('Camera đang chạy — chọn Xác nhận container hoặc Bật giám sát.')
    } catch {
      setThongBao('Không truy cập được camera (cần cấp quyền cho trình duyệt).')
    }
  }

  async function napAnh(hua, chuThongBao) {
    try {
      const img = await hua
      anhTinhRef.current = img
      veNguon()
      setCoHinh(true)
      setThongBao(chuThongBao)
    } catch (e) {
      setThongBao(e.message)
    }
  }

  /* ---------------- 1. Xac nhan container 2 buoc ---------------- */
  async function quet() {
    if (banRef.current || !engineRef.current?.worker) return
    if (!veNguon()) {
      setThongBao('Chưa có hình — bật camera hoặc chọn ảnh trước.')
      return
    }
    banRef.current = true
    setDangBan(true)
    setThongBao('OCR đang đọc mã container (4 chữ + 7 số)…')
    try {
      const ma = await docMaContainer(engineRef.current, canvasRef.current)
      if (!conGanRef.current) return
      if (!ma) {
        setThongBao('Chưa đọc được mã ISO 6346 — đưa mã 4 chữ + 7 số vào giữa khung rồi thử lại.')
        return
      }
      if (buoc === 0) {
        luuLan1Ref.current = ma
        setBuoc(1)
        setKetQua({ kieu: 'lan1', ma })
        ghiNhatKy('Lần 1: đã lưu mã ' + ma + ' + ảnh gốc.')
        chuong()
      } else {
        const maCu = luuLan1Ref.current
        if (ma === maCu) {
          setKetQua({ kieu: 'khop', ma })
          chuong()
          ghiNhatKy('Xác nhận ĐÚNG container ' + ma + '.')
        } else {
          setKetQua({ kieu: 'lech', ma, maCu })
          beepCanhBao()
          baoAnNinh('OCRSai', 'Chú ý', 'Xác nhận container KHÔNG khớp: lần 1 ' + maCu + ' / lần 2 ' + ma)
        }
        luuLan1Ref.current = null
        setBuoc(0)
      }
      setThongBao('Sẵn sàng.')
    } catch (e) {
      setThongBao('Lỗi OCR: ' + e.message)
    } finally {
      banRef.current = false
      if (conGanRef.current) setDangBan(false)
    }
  }

  /* ---------------- 2. Giam sat nguy hiem ---------------- */
  const nhipGiamSat = useCallback(async () => {
    if (banRef.current || !giamSatRef.current || !engineRef.current?.yolo) return
    if (!veNguon()) return
    banRef.current = true
    try {
      const { canhBaos, meta: chu } = await giamSatRef.current.batNhip(canvasRef.current, canvasPhuRef.current)
      if (!conGanRef.current) return
      setMeta(chu)
      for (const cb of canhBaos) baoAnNinh(cb.loai, cb.mucDo, cb.moTa)
    } catch {
      /* mot nhip loi khong duoc lam dung ca vong giam sat */
    } finally {
      banRef.current = false
    }
  }, [baoAnNinh, veNguon])

  function dungGiamSat() {
    clearInterval(nhipRef.current)
    nhipRef.current = null
    dangGiamSatRef.current = false
    setDangGiamSat(false)
  }

  function batGiamSat() {
    if (nhipRef.current) {
      dungGiamSat()
      ghiNhatKy('TẮT giám sát an ninh AI.')
      return
    }
    dangGiamSatRef.current = true
    setDangGiamSat(true)
    nhipRef.current = setInterval(nhipGiamSat, NHIP_QUET)
    nhipGiamSat()
    ghiNhatKy('BẬT giám sát an ninh AI (lửa · người ngã · vật thể đứng im).')
  }

  const sanSang = engine.trangThai === 'san-sang'

  return (
    <div className="row g-3">
      {/* ================= CAMERA ================= */}
      <div className="col-lg-7">
        <div className="card sp-the mb-3">
          <div className="card-header d-flex flex-wrap align-items-center gap-2">
            <div className="flex-grow-1">
              <i className="bi bi-camera-video me-2 text-primary" />
              Camera giám sát
            </div>
            <span className="small text-body-secondary">{meta}</span>
          </div>
          <div className="card-body">
            <div className={'small mb-2 ' + (engine.trangThai === 'loi' ? 'text-danger' : 'text-body-secondary')}>
              {engine.trangThai === 'dang-tai' && <span className="spinner-border spinner-border-sm me-2" />}
              {thongBao}
            </div>

            <div className="sp-man-hinh-camera">
              <canvas ref={canvasRef} />
              <video ref={videoRef} playsInline muted className="d-none">
                <track kind="captions" />
              </video>
              {!coHinh && (
                <div className="sp-camera-trong position-absolute">
                  <i className="bi bi-camera-video-off d-block" />
                  <div className="small mt-1">Chưa có nguồn hình</div>
                </div>
              )}
            </div>

            <div className="row g-2 mt-2">
              <div className="col-md-4">
                <button type="button" className={'btn w-100 ' + (dangQuay ? 'btn-danger' : 'btn-primary')} disabled={!sanSang} onClick={() => (dangQuay ? tatCamera() : batCamera())}>
                  <i className={'bi me-1 ' + (dangQuay ? 'bi-stop-circle' : 'bi-camera-video')} />
                  {dangQuay ? 'Tắt camera' : 'Bật camera'}
                </button>
              </div>
              <div className="col-md-4">
                <input
                  ref={fileRef}
                  type="file"
                  accept="image/*"
                  className="form-control"
                  aria-label="Chọn ảnh từ máy"
                  disabled={!sanSang}
                  onChange={(e) => {
                    const f = e.target.files?.[0]
                    if (f) napAnh(docFileThanhAnh(f), 'Đã nạp ảnh — chọn chức năng bên dưới.')
                  }}
                />
              </div>
              <div className="col-md-4">
                <button type="button" className="btn btn-outline-primary w-100" disabled={!sanSang} onClick={() => napAnh(taiAnh(AI_MAU + 'alpr-demo-1.png'), 'Đã nạp ảnh mẫu container.')}>
                  <i className="bi bi-image me-1" />
                  Ảnh mẫu container
                </button>
              </div>
            </div>

            <div className="d-flex flex-wrap gap-3 align-items-end mt-3">
              <div>
                <label className="form-label small fw-semibold mb-1" htmlFor="anViTri">
                  Vị trí camera (gửi kèm khi báo an ninh)
                </label>
                <select id="anViTri" className="form-select form-select-sm" value={viTri} onChange={(e) => setViTri(e.target.value)}>
                  {VI_TRI.map((v) => (
                    <option key={v}>{v}</option>
                  ))}
                </select>
              </div>
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" id="anTuHuCoi" checked={tuHuCoi} onChange={(e) => setTuHuCoi(e.target.checked)} />
                <label className="form-check-label small fw-semibold" htmlFor="anTuHuCoi">
                  Tự hú còi khi NGUY HIỂM
                </label>
              </div>
              <button
                type="button"
                className="btn btn-sm btn-outline-danger"
                onClick={() => {
                  if (coiDangKeu()) {
                    coiDung()
                    setCoiKeu(false)
                  } else {
                    coiBatDau(3)
                    setCoiKeu(true)
                    setTimeout(() => conGanRef.current && setCoiKeu(coiDangKeu()), 3200)
                  }
                }}
              >
                <i className="bi bi-megaphone me-1" />
                {coiKeu ? 'Tắt còi' : 'Thử còi báo động'}
              </button>
              <button type="button" className="btn btn-sm btn-outline-success" onClick={chuong}>
                <i className="bi bi-bell me-1" />
                Thử chuông
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* ================= HAI CHUC NANG + NHAT KY ================= */}
      <div className="col-lg-5">
        <div className="card sp-the mb-3 an-vien-xanh">
          <div className="card-header d-flex flex-wrap align-items-center gap-2">
            <div className="flex-grow-1">
              <i className="bi bi-upc-scan me-2 text-primary" />
              Xác nhận container 2 bước
            </div>
            <span className="badge text-bg-info">BƯỚC {buoc + 1}/2</span>
          </div>
          <div className="card-body">
            <p className="small text-body-secondary mb-2">
              Lần 1 chụp và <b>lưu mã</b>. Lần 2 quét lại để <b>đối chiếu</b>: đúng thì chuông báo, sai thì còi cảnh báo và báo phòng an ninh.
            </p>
            <button type="button" className="btn btn-warning w-100" disabled={!sanSang || !engine.ocr || dangBan} onClick={quet}>
              {dangBan ? <span className="spinner-border spinner-border-sm me-1" /> : <i className={'bi me-1 ' + (buoc === 0 ? 'bi-camera' : 'bi-check2-circle')} />}
              {buoc === 0 ? 'Quét lần 1 — LƯU MÃ' : 'Quét lần 2 — ĐỐI CHIẾU'}
            </button>
            <div className="small mt-2 an-ket-qua">
              {!ketQua && 'Chưa có lượt quét nào.'}
              {ketQua?.kieu === 'lan1' && (
                <>
                  Lần 1 đã lưu mã <b className="sp-bien-so">{ketQua.ma}</b>
                  <div>Đưa CHÍNH container đó vào camera lần nữa để đối chiếu.</div>
                </>
              )}
              {ketQua?.kieu === 'khop' && (
                <>
                  <span className="an-dung">CHÍNH XÁC</span> — mã <b className="sp-bien-so">{ketQua.ma}</b> khớp với lần quét đầu.
                </>
              )}
              {ketQua?.kieu === 'lech' && (
                <>
                  <span className="an-sai">KHÔNG KHỚP!</span> Lần 1 <b className="sp-bien-so">{ketQua.maCu}</b> · lần 2 <b className="sp-bien-so">{ketQua.ma}</b>
                </>
              )}
            </div>
          </div>
        </div>

        <div className="card sp-the mb-3 an-vien-do">
          <div className="card-header">
            <i className="bi bi-eye me-2 text-primary" />
            Giám sát nguy hiểm (thời gian thực)
          </div>
          <div className="card-body">
            <div className="small text-body-secondary mb-2">Lửa/nhiệt · người ngã · vật thể đứng im bất thường.</div>
            <button type="button" className={'btn w-100 ' + (dangGiamSat ? 'btn-danger' : 'btn-primary')} disabled={!sanSang || !engine.yolo} onClick={batGiamSat}>
              <i className={'bi me-1 ' + (dangGiamSat ? 'bi-pause-circle' : 'bi-eye')} />
              {dangGiamSat ? 'Dừng giám sát' : 'Bật giám sát an ninh'}
            </button>
          </div>
        </div>

        <div className="card sp-the">
          <div className="card-header d-flex flex-wrap align-items-center gap-2">
            <div className="flex-grow-1">
              <i className="bi bi-journal-text me-2 text-primary" />
              Nhật ký trạm
            </div>
            <Link className="btn btn-sm btn-outline-primary" to="/admin/security">
              <i className="bi bi-building-lock me-1" />
              Phòng an ninh
            </Link>
          </div>
          <div className="card-body an-nhat-ky">
            {!nhatKy.length ? (
              <div className="text-body-secondary small">Chưa có sự kiện.</div>
            ) : (
              nhatKy.map((d) => (
                <div key={d.id} className="an-dong-nhat-ky">
                  <span className="text-body-secondary">{d.luc.toLocaleTimeString('vi-VN')}</span> {d.chu}
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      <canvas ref={canvasPhuRef} className="d-none" />
    </div>
  )
}
