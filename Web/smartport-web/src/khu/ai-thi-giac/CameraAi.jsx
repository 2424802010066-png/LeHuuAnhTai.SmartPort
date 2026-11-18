import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { docFileThanhAnh, taiAnh } from '../../ai/taiThuVien'
import { goiApi, taiBlob } from '../../api/client'
import { useApi } from '../../api/useApi'
import { TheChiSo } from '../../components/ui'
import {
  DS_MO_HINH,
  MA_MAC_DINH,
  datBaoTrangThai,
  doiMoHinh,
  giaiPhongYoloNeuLon,
  goiYDieuPhoi,
  khoiDongYolo,
  lamMuot,
  nhanDien,
  taoBanGhiLuu,
  tenViet,
  tomTatKetQua,
  veKetQua,
} from './yoloThiGiac'
import './ai-thi-giac.css'

/** Anh mau giong ban MVC (tai tu Internet — may khong co mang thi dung "Tải ảnh" hoac webcam) */
const ANH_MAU = [
  { icon: 'bi-truck', ten: 'Xe đầu kéo trên đường', url: 'https://images.unsplash.com/photo-1519003722824-194d4455a60c?q=80&w=1100&auto=format&fit=crop' },
  { icon: 'bi-truck-flatbed', ten: 'Xe tải giao nhận', url: 'https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?q=80&w=1100&auto=format&fit=crop' },
  { icon: 'bi-people', ten: 'Nhân sự họp điều độ', url: 'https://images.unsplash.com/photo-1600880292203-757bb62b4baf?q=80&w=1100&auto=format&fit=crop' },
  { icon: 'bi-water', ten: 'Tàu ngoài khơi', url: 'https://images.unsplash.com/photo-1502920514313-52581002a659?q=80&w=1100&auto=format&fit=crop' },
]
const VI_TRI = ['Cổng chính', 'Cổng phụ', 'Bãi container', 'Cầu bến', 'Kho CFS']
const MAU_CANH_BAO = { warn: '#e5691d', ok: '#15803d', info: '#0d6efd' }

/** Camera AI (YOLO26) — nhan dien xe, nguoi, tau thuyen ngay tren trinh duyet (thay Admin/Vision/Index). */
export default function CameraAi() {
  const soLieu = useApi('/api/camera-ai/hom-nay')
  const tuHoc = useApi('/api/camera-ai/nguong')
  const camIp = useApi('/api/camera/danh-sach')

  const [dongCo, setDongCo] = useState(null)
  const [loiDongCo, setLoiDongCo] = useState('')
  const [chuTrangThai, setChuTrangThai] = useState('Đang tải mô hình…')
  const [dangChay, setDangChay] = useState(false)
  const [maMoHinh, setMaMoHinh] = useState(MA_MAC_DINH)
  const [nguong, setNguong] = useState(0.35)
  const [viTri, setViTri] = useState(VI_TRI[0])
  const [tuLuu, setTuLuu] = useState(true)
  const [maCamIp, setMaCamIp] = useState('')
  const [tomTat, setTomTat] = useState(null)
  const [daLuu, setDaLuu] = useState(null)
  const [daPhanHoi, setDaPhanHoi] = useState({})
  const [dangQuay, setDangQuay] = useState(false)
  const [coAnh, setCoAnh] = useState(false)

  const canvasRef = useRef(null)
  const videoRef = useRef(null)
  const luongRef = useRef(null)
  const rafRef = useRef(0)
  const quayRef = useRef(false)
  const banRonRef = useRef(false)
  const truocRef = useRef([])
  const lanLuuRef = useRef(0)
  const nguongRef = useRef(0.35)
  const viTriRef = useRef(VI_TRI[0])
  const tuLuuRef = useRef(true)
  const conGanRef = useRef(true)
  const henBoLuuRef = useRef(null)

  // Nguong TU HOC tung lop (lop hay nhan sai -> nang nguong) do API tinh tu phan hoi nguoi dung
  const nguongLop = {}
  for (const l of tuHoc.duLieu?.lops || []) if (l.nguong) nguongLop[l.nhan] = l.nguong
  const nguongLopRef = useRef(nguongLop)
  nguongLopRef.current = nguongLop
  nguongRef.current = nguong
  viTriRef.current = viTri
  tuLuuRef.current = tuLuu

  /* ---------------- Khoi dong dong co YOLO 1 lan ---------------- */
  useEffect(() => {
    conGanRef.current = true
    datBaoTrangThai((chu) => conGanRef.current && setChuTrangThai(chu))
    khoiDongYolo()
      .then((tt) => {
        if (!conGanRef.current) return
        setDongCo(tt)
        setMaMoHinh(tt.ma || MA_MAC_DINH)
        setChuTrangThai('Mô hình sẵn sàng — chọn ảnh mẫu, tải ảnh lên hoặc bật webcam.')
      })
      .catch((e) => conGanRef.current && setLoiDongCo(e.message))
    return () => {
      conGanRef.current = false
      quayRef.current = false
      datBaoTrangThai(null)
      cancelAnimationFrame(rafRef.current)
      clearTimeout(henBoLuuRef.current)
      luongRef.current?.getTracks().forEach((t) => t.stop())
      luongRef.current = null
      giaiPhongYoloNeuLon()
    }
  }, [])

  /** Hien ket qua 1 khung hinh: chip, canh bao dieu phoi va luu vao CSDL neu bat */
  const apDungKetQua = useCallback(
    async (hop, ms, nguonAnh) => {
      const tt = tomTatKetQua(hop)
      setTomTat({ ...tt, ms, goiY: goiYDieuPhoi(tt.dem) })
      setDaPhanHoi({})
      if (!tuLuuRef.current || !hop.length) return
      // Webcam ban lien tuc -> toi da 1 ban ghi / 8 giay cho khoi ngap CSDL (giong ban MVC)
      const bayGio = Date.now()
      if (nguonAnh === 'Webcam' && bayGio - lanLuuRef.current < 8000) return
      lanLuuRef.current = bayGio
      try {
        const kq = await goiApi('/api/camera-ai/luu', {
          method: 'POST',
          body: taoBanGhiLuu(tt, ms, { nguon: nguonAnh, model: dongCo?.ten || null, viTri: viTriRef.current }),
        })
        if (!conGanRef.current) return
        setDaLuu(kq.duLieu)
        soLieu.taiLai({ im: true })
        clearTimeout(henBoLuuRef.current)
        henBoLuuRef.current = setTimeout(() => conGanRef.current && setDaLuu(null), 4000)
      } catch {
        /* mat mang / API ban: bo qua, khong chan luong nhan dien */
      }
    },
    [dongCo, soLieu],
  )

  /** Phan tich 1 anh tinh (anh mau, anh tai len, anh camera IP) */
  const phanTichAnh = useCallback(
    async (img, nguonAnh) => {
      if (dangChay) return
      setDangChay(true)
      setLoiDongCo('')
      setChuTrangThai('AI đang phân tích khung hình…')
      truocRef.current = []
      try {
        const w = img.naturalWidth || img.width
        const h = img.naturalHeight || img.height
        const r = await nhanDien(img, w, h, { nguong: nguongRef.current, nguongLop: nguongLopRef.current })
        if (!conGanRef.current) return
        veKetQua(canvasRef.current, img, w, h, r.hop)
        setCoAnh(true)
        setChuTrangThai('Xong — chọn ảnh khác hoặc chỉnh ngưỡng rồi phân tích lại.')
        await apDungKetQua(r.hop, r.ms, nguonAnh)
      } catch (e) {
        if (conGanRef.current) {
          setLoiDongCo(e.message)
          setChuTrangThai('Chưa phân tích được khung hình.')
        }
      } finally {
        if (conGanRef.current) setDangChay(false)
      }
    },
    [apDungKetQua, dangChay],
  )

  async function moAnhMau(url) {
    tatWebcam()
    setChuTrangThai('Đang tải ảnh mẫu…')
    try {
      const img = await taiAnh(url)
      await phanTichAnh(img, 'AnhMau')
    } catch {
      setLoiDongCo('Không tải được ảnh mẫu (cần Internet) — hãy dùng “Tải ảnh từ máy” hoặc webcam.')
    }
  }

  async function chonFile(e) {
    const f = e.target.files?.[0]
    e.target.value = '' // chon lai CUNG 1 file van phan tich
    if (!f) return
    tatWebcam()
    try {
      await phanTichAnh(await docFileThanhAnh(f), 'TaiAnh')
    } catch (err) {
      setLoiDongCo(err.message)
    }
  }

  /** Chup anh tu camera IP hien truong: the img khong gui duoc token -> tai blob roi tao URL tam */
  async function chupCameraIp() {
    if (!maCamIp) return
    tatWebcam()
    setChuTrangThai('Đang lấy ảnh từ camera hiện trường…')
    let url = null
    try {
      url = URL.createObjectURL(await taiBlob('/api/camera/' + maCamIp + '/anh'))
      const img = await taiAnh(url)
      await phanTichAnh(img, 'CameraIP')
    } catch (e) {
      setLoiDongCo(e.message)
      setChuTrangThai('Chưa lấy được ảnh camera — vào màn hình Camera IP bấm “Thử kết nối” để xem lý do.')
    } finally {
      if (url) URL.revokeObjectURL(url)
    }
  }

  /* ---------------- Webcam ---------------- */
  // Vong lap webcam song lau hon mot lan ve lai -> doc ham xu ly qua ref de luon dung ban moi nhat
  const apDungRef = useRef(null)
  apDungRef.current = apDungKetQua

  const motVong = useCallback(async () => {
    if (!quayRef.current) return
    const v = videoRef.current
    if (v && v.readyState >= 2 && !banRonRef.current) {
      banRonRef.current = true
      try {
        const w = v.videoWidth
        const h = v.videoHeight
        const r = await nhanDien(v, w, h, { nguong: nguongRef.current, nguongLop: nguongLopRef.current, laLuong: true })
        if (quayRef.current) {
          const hop = lamMuot(r.hop, truocRef.current)
          truocRef.current = hop
          veKetQua(canvasRef.current, v, w, h, hop)
          setCoAnh(true)
          await apDungRef.current(hop, r.ms, 'Webcam')
        }
      } catch (e) {
        if (conGanRef.current) setLoiDongCo(e.message)
      } finally {
        banRonRef.current = false
      }
    }
    if (quayRef.current) rafRef.current = requestAnimationFrame(motVong)
  }, [])

  async function batWebcam() {
    if (!navigator.mediaDevices?.getUserMedia) {
      setLoiDongCo('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.')
      return
    }
    try {
      const s = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 960 } }, audio: false })
      luongRef.current = s
      videoRef.current.srcObject = s
      await videoRef.current.play()
      quayRef.current = true
      truocRef.current = []
      setDangQuay(true)
      setLoiDongCo('')
      setChuTrangThai('Webcam đang chạy — AI phân tích liên tục từng khung hình.')
      rafRef.current = requestAnimationFrame(motVong)
    } catch {
      setLoiDongCo('Không truy cập được webcam (cần cấp quyền camera).')
    }
  }

  function tatWebcam() {
    quayRef.current = false
    cancelAnimationFrame(rafRef.current)
    luongRef.current?.getTracks().forEach((t) => t.stop())
    luongRef.current = null
    truocRef.current = []
    setDangQuay(false)
  }

  /* ---------------- Doi mo hinh ---------------- */
  async function chonMoHinh(ma) {
    const cu = maMoHinh
    setMaMoHinh(ma)
    try {
      const tt = await doiMoHinh(ma)
      if (!conGanRef.current) return
      setDongCo(tt)
      setChuTrangThai('Đã chuyển sang ' + tt.ten + ' — phân tích lại để thấy khác biệt.')
    } catch (e) {
      if (!conGanRef.current) return
      setMaMoHinh(cu)
      setLoiDongCo('Không tải được mô hình — giữ nguyên engine cũ. ' + e.message)
    }
  }

  /* ---------------- Vong lap tu hoc: phan hoi dung/sai ---------------- */
  async function guiPhanHoi(nhan, dung, doTinCay) {
    setDaPhanHoi((cu) => ({ ...cu, [nhan]: dung ? 'dung' : 'sai' }))
    try {
      await goiApi('/api/camera-ai/phan-hoi', { method: 'POST', body: { nhan, dung, doTinCay } })
      tuHoc.taiLai({ im: true })
    } catch {
      setDaPhanHoi((cu) => {
        const moi = { ...cu }
        delete moi[nhan]
        return moi
      })
    }
  }

  const sanSang = !!dongCo
  const hn = soLieu.duLieu
  const th = tuHoc.duLieu

  return (
    <>
      <div className="sp-hop-ai mb-3">
        <div className="d-flex flex-wrap align-items-center gap-3">
          <div className="flex-grow-1">
            <span className="sp-nhan-ai">
              <i className="bi bi-camera-video me-1" />
              CAMERA AI · YOLO26 CHẠY TRÊN TRÌNH DUYỆT
            </span>
            <div className="h5 mb-0 fw-bold mt-2">Nhận diện xe, người, tàu thuyền tại cảng</div>
          </div>
          <span className={'sp-nhan ' + (sanSang ? (dongCo.duPhong ? 'sp-nhan-warn' : 'sp-nhan-ok') : 'sp-nhan-info')}>
            <i className={'bi me-1 ' + (sanSang ? (dongCo.gpu ? 'bi-gpu-card' : 'bi-cpu-fill') : 'bi-hourglass-split')} />
            {sanSang ? `${dongCo.ten} · ${dongCo.gpu ? 'WebGPU' : 'WASM CPU'}${dongCo.tuBoDem ? ' · bộ đệm' : ''}` : 'Đang khởi động…'}
          </span>
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-truck" mau="xanh" so={hn ? hn.xeTaiHomNay : '…'} nhan="Lượt xe tải hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-cpu" mau="tim" so={hn ? hn.luotHomNay : '…'} nhan="Lượt AI quét hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-exclamation-triangle" mau="do" so={hn ? hn.canhBaoHomNay : '…'} nhan="Cảnh báo hôm nay" />
        </div>
        <div className="col-6 col-lg-3 d-grid gap-2">
          <Link className="btn btn-primary" to="/admin/vision/analytics">
            <i className="bi bi-graph-up-arrow me-1" />
            Thống kê lưu lượng
          </Link>
          <Link className="btn btn-outline-secondary" to="/admin/vision/history">
            <i className="bi bi-clock-history me-1" />
            Nhật ký nhận diện
          </Link>
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
                {!sanSang && !loiDongCo && <span className="spinner-border spinner-border-sm me-2" />}
                {chuTrangThai}
              </div>
              {loiDongCo && <div className="alert alert-danger py-2 small">{loiDongCo}</div>}

              <label className="form-label small fw-semibold" htmlFor="tg-model">
                Mô hình YOLO26
              </label>
              <select id="tg-model" className="form-select form-select-sm mb-1" value={maMoHinh} disabled={!sanSang} onChange={(e) => chonMoHinh(e.target.value)}>
                {DS_MO_HINH.map((m) => (
                  <option key={m.ma} value={m.ma}>
                    {m.ten} — {m.moTa} (mAP {m.mAP} · {m.mb} MB)
                  </option>
                ))}
              </select>
              <div className="form-text mb-3">Mô hình lớn hơn bắt được vật nhỏ/ở xa nhưng tải lâu hơn. Webcam nên dùng YOLO26n.</div>

              <label className="form-label small fw-semibold" htmlFor="tg-file">
                Tải ảnh từ máy tính
              </label>
              <input id="tg-file" type="file" accept="image/*" className="form-control form-control-sm mb-3" disabled={!sanSang || dangChay} onChange={chonFile} />

              <div className="form-label small fw-semibold">Hoặc dùng ảnh mẫu</div>
              <div className="d-grid gap-2 mb-3">
                {ANH_MAU.map((a) => (
                  <button key={a.url} type="button" className="btn btn-sm btn-outline-primary" disabled={!sanSang || dangChay} onClick={() => moAnhMau(a.url)}>
                    <i className={'bi ' + a.icon + ' me-1'} />
                    {a.ten}
                  </button>
                ))}
              </div>

              <label className="form-label small fw-semibold" htmlFor="tg-camip">
                Camera IP hiện trường
              </label>
              <div className="input-group input-group-sm mb-1">
                <select id="tg-camip" className="form-select" value={maCamIp} onChange={(e) => setMaCamIp(e.target.value)} disabled={!camIp.duLieu?.length}>
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
              <div className="form-text mb-3">
                {camIp.duLieu?.length ? 'Server lấy ảnh tĩnh từ camera trong LAN rồi đưa về đây.' : 'Chưa có camera IP nào đang bật — thêm ở màn hình Camera IP hiện trường.'}
              </div>

              <div className="form-label small fw-semibold">Webcam trực tiếp (real-time)</div>
              <button type="button" className={'btn w-100 mb-3 ' + (dangQuay ? 'btn-danger' : 'btn-primary')} disabled={!sanSang} onClick={() => (dangQuay ? tatWebcam() : batWebcam())}>
                <i className={'bi me-1 ' + (dangQuay ? 'bi-stop-circle' : 'bi-camera-video')} />
                {dangQuay ? 'Dừng webcam' : 'Bật webcam AI'}
              </button>

              <label className="form-label small fw-semibold d-flex justify-content-between" htmlFor="tg-nguong">
                <span>Ngưỡng tin cậy</span>
                <span className="text-primary">{Math.round(nguong * 100)}%</span>
              </label>
              <input id="tg-nguong" type="range" className="form-range" min={0.15} max={0.8} step={0.05} value={nguong} onChange={(e) => setNguong(+e.target.value)} />

              <hr />
              <label className="form-label small fw-semibold" htmlFor="tg-vitri">
                Vị trí camera
              </label>
              <select id="tg-vitri" className="form-select form-select-sm mb-2" value={viTri} onChange={(e) => setViTri(e.target.value)}>
                {VI_TRI.map((v) => (
                  <option key={v}>{v}</option>
                ))}
              </select>
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" id="tg-luu" checked={tuLuu} onChange={(e) => setTuLuu(e.target.checked)} />
                <label className="form-check-label small fw-semibold" htmlFor="tg-luu">
                  Lưu kết quả vào CSDL
                </label>
              </div>
              <div className="form-text">Bật để ghi nhận mỗi lần quét làm dữ liệu thống kê lưu lượng và cảnh báo ùn tắc (webcam: tối đa 1 bản ghi/8 giây).</div>
              {daLuu && (
                <div className="sp-nhan sp-nhan-ok mt-2">
                  <i className="bi bi-cloud-check-fill me-1" />
                  Đã lưu #{daLuu.maND}
                  {daLuu.mucCanhBao !== 'Bình thường' && <> · {daLuu.mucCanhBao}</>}
                </div>
              )}

              {th?.tongPhanHoi > 0 && (
                <>
                  <hr />
                  <div className="tg-tu-hoc">
                    <div className="small fw-semibold mb-1">
                      <i className="bi bi-arrow-repeat me-1" />
                      Vòng lặp tự học — {th.tongPhanHoi} phản hồi đã thu thập
                    </div>
                    {th.lops.map((l) => (
                      <div key={l.nhan} className="tg-tu-hoc-dong">
                        <span className="text-truncate">
                          {tenViet(l.nhan)} <span className="text-body-secondary">({l.dung}/{l.tong} đúng)</span>
                        </span>
                        <span className={'fw-bold ' + (l.precision >= 90 ? 'text-success' : l.precision >= 70 ? 'text-warning' : 'text-danger')}>{l.precision}%</span>
                        <span className="text-body-secondary text-nowrap">{l.nguong ? 'ngưỡng → ' + Math.round(l.nguong * 100) + '%' : 'ngưỡng chuẩn'}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
              <div className="form-text mt-1">
                Bấm <b>✓ / ✗</b> cạnh mỗi kết quả để dạy AI — hệ thống tự nâng ngưỡng lớp hay nhận sai và hạ ngưỡng lớp nhận chuẩn (Active Learning).
              </div>
            </div>
          </div>

          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-lightbulb me-2" />
              Cảnh báo điều phối AI
            </div>
            <div className="card-body">
              {tomTat?.goiY?.length ? (
                tomTat.goiY.map((g) => (
                  <div key={g.chu} className="sp-canh-bao" style={{ borderLeftColor: MAU_CANH_BAO[g.mau] }}>
                    <i className={'bi ' + g.icon} style={{ color: MAU_CANH_BAO[g.mau] }} />
                    <span className="small">{g.chu}</span>
                  </div>
                ))
              ) : (
                <div className="text-body-secondary small">{tomTat ? 'Khung hình an toàn — không có cảnh báo điều phối.' : 'Chưa có dữ liệu — hãy phân tích một khung hình.'}</div>
              )}
            </div>
          </div>
        </div>

        <div className="col-lg-8">
          <div className="card sp-the">
            <div className="card-header d-flex flex-wrap align-items-center gap-2">
              <span className="flex-grow-1">
                <i className="bi bi-bounding-box-circles me-2" />
                Kết quả nhận diện
              </span>
              <span className="small text-body-secondary">
                {tomTat ? `${tomTat.tong} đối tượng · ${tomTat.ms} ms/khung hình · ngưỡng ${Math.round(nguong * 100)}%` : '—'}
              </span>
            </div>
            <div className="card-body">
              <div className="tg-khung-anh">
                <canvas ref={canvasRef} className={coAnh ? '' : 'd-none'} />
                {!coAnh && (
                  <div className="tg-khung-trong">
                    <i className="bi bi-image" />
                    <div className="mt-2 small">Chọn ảnh mẫu, tải ảnh lên hoặc bật webcam để AI phân tích.</div>
                  </div>
                )}
                <video ref={videoRef} playsInline muted className="d-none" />
              </div>

              <div className="mt-3">
                {tomTat?.danhSach?.length ? (
                  tomTat.danhSach.map((x) => (
                    <span key={x.label} className="tg-chip" style={{ borderLeftColor: x.mau }}>
                      {x.ten} <b>{x.soLuong}</b>
                      <span className="text-body-secondary">{x.tinCay}%</span>
                      <button
                        type="button"
                        className="tg-nut-ph"
                        title={'AI nhận ĐÚNG lớp ' + x.ten}
                        aria-label={'AI nhận đúng lớp ' + x.ten}
                        disabled={!!daPhanHoi[x.label]}
                        onClick={() => guiPhanHoi(x.label, true, x.tinCay)}
                      >
                        {daPhanHoi[x.label] === 'dung' ? '✔' : '✓'}
                      </button>
                      <button
                        type="button"
                        className="tg-nut-ph sai"
                        title={'AI nhận SAI lớp ' + x.ten}
                        aria-label={'AI nhận sai lớp ' + x.ten}
                        disabled={!!daPhanHoi[x.label]}
                        onClick={() => guiPhanHoi(x.label, false, x.tinCay)}
                      >
                        {daPhanHoi[x.label] === 'sai' ? '✔' : '✗'}
                      </button>
                    </span>
                  ))
                ) : (
                  <span className="text-body-secondary small">{tomTat ? 'Không phát hiện đối tượng nào (thử giảm ngưỡng tin cậy).' : 'Chưa có kết quả.'}</span>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
