import { useCallback, useEffect, useRef, useState } from 'react'
import { docFileThanhAnh, taiAnh } from '../../ai/taiThuVien'
import { taiBlob } from '../../api/client'
import { useApi } from '../../api/useApi'
import { MAU_AN_TOAN, MAU_CHU_Y, MAU_NGUY, datBaoTrangThaiPose, khoiDongPose, nhanDienTuThe, veKetQuaPose } from './poseThiGiac'
import './ai-thi-giac.css'

/** Anh mau giong ban MVC (tai tu Internet) */
const ANH_MAU = [
  { icon: 'bi-people', ten: 'Nhân sự đang họp', url: 'https://images.unsplash.com/photo-1600880292203-757bb62b4baf?q=80&w=1100&auto=format&fit=crop' },
  { icon: 'bi-person-standing', ten: 'Hai người bắt tay', url: 'https://images.unsplash.com/photo-1521791136064-7986c2920216?q=80&w=1100&auto=format&fit=crop' },
]

/** SafePose AI — giam sat an toan lao dong bang uoc luong tu the (thay Admin/Vision/Pose). */
export default function SafePose() {
  const camIp = useApi('/api/camera/danh-sach')

  const [dongCo, setDongCo] = useState(null)
  const [loiAi, setLoiAi] = useState('')
  const [chuTrangThai, setChuTrangThai] = useState('Đang tải mô hình…')
  const [nguong, setNguong] = useState(0.4)
  const [dangChay, setDangChay] = useState(false)
  const [dangQuay, setDangQuay] = useState(false)
  const [coAnh, setCoAnh] = useState(false)
  const [maCamIp, setMaCamIp] = useState('')
  const [ketQua, setKetQua] = useState(null)

  const canvasRef = useRef(null)
  const videoRef = useRef(null)
  const luongRef = useRef(null)
  const rafRef = useRef(0)
  const quayRef = useRef(false)
  const banRonRef = useRef(false)
  const nguongRef = useRef(0.4)
  const conGanRef = useRef(true)
  nguongRef.current = nguong

  useEffect(() => {
    conGanRef.current = true
    datBaoTrangThaiPose((chu) => conGanRef.current && setChuTrangThai(chu))
    khoiDongPose()
      .then((tt) => {
        if (!conGanRef.current) return
        setDongCo(tt)
        setChuTrangThai('Mô hình pose sẵn sàng — tải ảnh có người hoặc bật webcam.')
      })
      .catch((e) => conGanRef.current && setLoiAi(e.message))
    return () => {
      conGanRef.current = false
      quayRef.current = false
      datBaoTrangThaiPose(null)
      cancelAnimationFrame(rafRef.current)
      luongRef.current?.getTracks().forEach((t) => t.stop())
      luongRef.current = null
    }
  }, [])

  const phanTichAnh = useCallback(
    async (img) => {
      if (dangChay) return
      setDangChay(true)
      setLoiAi('')
      setChuTrangThai('AI đang phân tích tư thế…')
      try {
        const w = img.naturalWidth || img.width
        const h = img.naturalHeight || img.height
        const r = await nhanDienTuThe(img, w, h, { nguong: nguongRef.current })
        if (!conGanRef.current) return
        const dem = veKetQuaPose(canvasRef.current, img, w, h, r.nguoi)
        setCoAnh(true)
        setKetQua({ ...dem, ms: r.ms })
        setChuTrangThai('Xong — chọn ảnh khác hoặc bật webcam để theo dõi liên tục.')
      } catch (e) {
        if (conGanRef.current) {
          setLoiAi(e.message)
          setChuTrangThai('Chưa phân tích được khung hình.')
        }
      } finally {
        if (conGanRef.current) setDangChay(false)
      }
    },
    [dangChay],
  )

  async function moAnhMau(url) {
    tatWebcam()
    setChuTrangThai('Đang tải ảnh mẫu…')
    try {
      await phanTichAnh(await taiAnh(url))
    } catch {
      setLoiAi('Không tải được ảnh mẫu (cần Internet) — hãy dùng “Tải ảnh” hoặc webcam.')
    }
  }

  async function chonFile(e) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f) return
    tatWebcam()
    try {
      await phanTichAnh(await docFileThanhAnh(f))
    } catch (err) {
      setLoiAi(err.message)
    }
  }

  /** Anh camera IP can dang nhap: tai blob roi tao URL tam (the img khong gui duoc token) */
  async function chupCameraIp() {
    if (!maCamIp) return
    tatWebcam()
    setChuTrangThai('Đang lấy ảnh từ camera hiện trường…')
    let url = null
    try {
      url = URL.createObjectURL(await taiBlob('/api/camera/' + maCamIp + '/anh'))
      await phanTichAnh(await taiAnh(url))
    } catch (e) {
      setLoiAi(e.message)
      setChuTrangThai('Chưa lấy được ảnh camera — vào màn hình Camera IP bấm “Thử kết nối” để xem lý do.')
    } finally {
      if (url) URL.revokeObjectURL(url)
    }
  }

  const motVong = useCallback(async () => {
    if (!quayRef.current) return
    const v = videoRef.current
    if (v && v.readyState >= 2 && !banRonRef.current) {
      banRonRef.current = true
      try {
        const w = v.videoWidth
        const h = v.videoHeight
        const r = await nhanDienTuThe(v, w, h, { nguong: nguongRef.current })
        if (quayRef.current) {
          const dem = veKetQuaPose(canvasRef.current, v, w, h, r.nguoi)
          setCoAnh(true)
          setKetQua({ ...dem, ms: r.ms })
        }
      } catch (e) {
        if (conGanRef.current) setLoiAi(e.message)
      } finally {
        banRonRef.current = false
      }
    }
    if (quayRef.current) rafRef.current = requestAnimationFrame(motVong)
  }, [])

  async function batWebcam() {
    if (!navigator.mediaDevices?.getUserMedia) {
      setLoiAi('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.')
      return
    }
    try {
      const s = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 960 } }, audio: false })
      luongRef.current = s
      videoRef.current.srcObject = s
      await videoRef.current.play()
      quayRef.current = true
      setDangQuay(true)
      setLoiAi('')
      setChuTrangThai('Webcam đang chạy — AI theo dõi tư thế liên tục.')
      rafRef.current = requestAnimationFrame(motVong)
    } catch {
      setLoiAi('Không truy cập được webcam (cần cấp quyền camera).')
    }
  }

  function tatWebcam() {
    quayRef.current = false
    cancelAnimationFrame(rafRef.current)
    luongRef.current?.getTracks().forEach((t) => t.stop())
    luongRef.current = null
    setDangQuay(false)
  }

  const sanSang = !!dongCo

  return (
    <>
      <div className="sp-hop-ai mb-3">
        <div className="d-flex flex-wrap align-items-center gap-3">
          <div className="flex-grow-1">
            <span className="sp-nhan-ai">
              <i className="bi bi-person-arms-up me-1" />
              AN TOÀN LAO ĐỘNG · YOLO26-POSE
            </span>
            <div className="h5 mb-0 fw-bold mt-2">Phát hiện người ngã, nâng vật sai tư thế</div>
          </div>
          <span className={'sp-nhan ' + (sanSang ? 'sp-nhan-ok' : 'sp-nhan-info')}>
            <i className={'bi me-1 ' + (sanSang ? (dongCo.gpu ? 'bi-gpu-card' : 'bi-cpu-fill') : 'bi-hourglass-split')} />
            {sanSang ? `YOLO26n-Pose · ${dongCo.gpu ? 'WebGPU' : 'WASM CPU'}${dongCo.tuBoDem ? ' · bộ đệm' : ''}` : 'Đang khởi động…'}
          </span>
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-4">
          <div className="card sp-the mb-3">
            <div className="card-header">
              <i className="bi bi-sliders me-2" />
              Nguồn hình ảnh
            </div>
            <div className="card-body">
              <div className="small text-body-secondary mb-3">
                {!sanSang && !loiAi && <span className="spinner-border spinner-border-sm me-2" />}
                {chuTrangThai}
              </div>
              {loiAi && <div className="alert alert-danger py-2 small">{loiAi}</div>}

              <label className="form-label small fw-semibold" htmlFor="tg-ps-file">
                Tải ảnh có người
              </label>
              <input id="tg-ps-file" type="file" accept="image/*" className="form-control form-control-sm mb-3" disabled={!sanSang || dangChay} onChange={chonFile} />

              <div className="form-label small fw-semibold">Hoặc dùng ảnh mẫu</div>
              <div className="d-grid gap-2 mb-3">
                {ANH_MAU.map((a) => (
                  <button key={a.url} type="button" className="btn btn-sm btn-outline-primary" disabled={!sanSang || dangChay} onClick={() => moAnhMau(a.url)}>
                    <i className={'bi ' + a.icon + ' me-1'} />
                    {a.ten}
                  </button>
                ))}
              </div>

              <label className="form-label small fw-semibold" htmlFor="tg-ps-camip">
                Camera IP hiện trường
              </label>
              <div className="input-group input-group-sm mb-3">
                <select id="tg-ps-camip" className="form-select" value={maCamIp} onChange={(e) => setMaCamIp(e.target.value)} disabled={!camIp.duLieu?.length}>
                  <option value="">— Chọn camera —</option>
                  {(camIp.duLieu || []).map((c) => (
                    <option key={c.maCam} value={c.maCam}>
                      {c.tenCam}
                      {c.viTri ? ' · ' + c.viTri : ''}
                    </option>
                  ))}
                </select>
                <button type="button" className="btn btn-outline-primary" disabled={!sanSang || !maCamIp || dangChay} onClick={chupCameraIp}>
                  <i className="bi bi-camera me-1" />
                  Chụp
                </button>
              </div>

              <div className="form-label small fw-semibold">Webcam trực tiếp</div>
              <button type="button" className={'btn w-100 mb-3 ' + (dangQuay ? 'btn-danger' : 'btn-primary')} disabled={!sanSang} onClick={() => (dangQuay ? tatWebcam() : batWebcam())}>
                <i className={'bi me-1 ' + (dangQuay ? 'bi-stop-circle' : 'bi-camera-video')} />
                {dangQuay ? 'Dừng webcam' : 'Bật webcam pose'}
              </button>

              <label className="form-label small fw-semibold d-flex justify-content-between" htmlFor="tg-ps-nguong">
                <span>Ngưỡng tin cậy</span>
                <span className="text-primary">{Math.round(nguong * 100)}%</span>
              </label>
              <input id="tg-ps-nguong" type="range" className="form-range" min={0.2} max={0.8} step={0.05} value={nguong} onChange={(e) => setNguong(+e.target.value)} />
            </div>
          </div>

          <div className="card sp-the mb-3">
            <div className="card-header">
              <i className="bi bi-shield-exclamation me-2" />
              Phân tích an toàn
            </div>
            <div className="card-body">
              {!ketQua ? (
                <div className="text-body-secondary small">Chưa có dữ liệu — hãy phân tích một khung hình.</div>
              ) : !ketQua.tong ? (
                <div className="text-body-secondary small">Không phát hiện người trong khung hình (thử giảm ngưỡng).</div>
              ) : (
                <>
                  {ketQua.soNguy > 0 && (
                    <div className="sp-canh-bao" style={{ borderLeftColor: MAU_NGUY }}>
                      <i className="bi bi-exclamation-octagon-fill" style={{ color: MAU_NGUY }} />
                      <span className="small">
                        <b>{ketQua.soNguy} người ở tư thế NGUY HIỂM</b> — cử nhân viên kiểm tra hiện trường ngay.
                      </span>
                    </div>
                  )}
                  {ketQua.soChuY > 0 && (
                    <div className="sp-canh-bao" style={{ borderLeftColor: MAU_CHU_Y }}>
                      <i className="bi bi-exclamation-triangle" style={{ color: MAU_CHU_Y }} />
                      <span className="small">{ketQua.soChuY} người tư thế cần chú ý (cúi sâu / nâng vật sai cách).</span>
                    </div>
                  )}
                  {!ketQua.soNguy && !ketQua.soChuY && (
                    <div className="sp-canh-bao" style={{ borderLeftColor: MAU_AN_TOAN }}>
                      <i className="bi bi-check-circle" style={{ color: MAU_AN_TOAN }} />
                      <span className="small">Tất cả {ketQua.tong} người có tư thế an toàn.</span>
                    </div>
                  )}
                  <div className="small text-body-secondary mt-2">
                    <i className="bi bi-people me-1" />
                    Tổng {ketQua.tong} người trong khu vực quan sát.
                  </div>
                </>
              )}
            </div>
          </div>

          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-list-check me-2" />
              Luật an toàn AI áp dụng
            </div>
            <div className="card-body small text-body-secondary" style={{ lineHeight: 1.8 }}>
              <div>
                <span className="sp-nhan sp-nhan-do me-1">Nguy hiểm</span> nằm ngang (nghi ngã) hoặc đầu thấp hơn hông
              </div>
              <div>
                <span className="sp-nhan sp-nhan-warn me-1">Chú ý</span> thân trên gập sâu — tư thế nâng vật dễ chấn thương lưng
              </div>
              <div>
                <span className="sp-nhan sp-nhan-ok me-1">An toàn</span> đứng / di chuyển bình thường
              </div>
            </div>
          </div>
        </div>

        <div className="col-lg-8">
          <div className="card sp-the">
            <div className="card-header d-flex flex-wrap align-items-center gap-2">
              <span className="flex-grow-1">
                <i className="bi bi-bounding-box-circles me-2" />
                Kết quả phân tích tư thế
              </span>
              <span className="small text-body-secondary">{ketQua ? `${ketQua.tong} người · ${ketQua.ms} ms/khung hình` : '—'}</span>
            </div>
            <div className="card-body">
              <div className="tg-khung-anh">
                <canvas ref={canvasRef} className={coAnh ? '' : 'd-none'} />
                {!coAnh && (
                  <div className="tg-khung-trong">
                    <i className="bi bi-person-bounding-box" />
                    <div className="mt-2 small">Tải ảnh có người hoặc bật webcam để AI phân tích tư thế.</div>
                  </div>
                )}
                <video ref={videoRef} playsInline muted className="d-none" />
              </div>
              <div className="form-text mt-2">
                <i className="bi bi-info-circle me-1" />
                Mô hình YOLO26n-Pose nhận diện 17 khớp/người theo chuẩn COCO, chạy hoàn toàn trên trình duyệt — hình ảnh <b>không rời khỏi máy</b>.
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
