import { useEffect, useRef, useState } from 'react'
import { goiApi } from '../../api/client'
import { NhanTrangThai, TheNoiDung, Truong, useThongBao } from '../../components/ui'
import { MAU_BIEN_CHAC, MAU_BIEN_NGHI, MAU_LOP, NHAN_VIDEO, veKhung } from './aiMau'

const LOAI = [
  { gt: 'ca-hai', ten: 'Cả hai', icon: 'bi-layers' },
  { gt: 'container', ten: 'Mã container', icon: 'bi-box-seam' },
  { gt: 'bien-so', ten: 'Biển số', icon: 'bi-car-front' },
]
const TRANG_THAI = { 'dang-cho': 'Đang chờ', 'dang-xu-ly': 'Đang xử lý', xong: 'Hoàn tất', 'da-huy': 'Đã hủy', loi: 'Lỗi' }

function dinhGio(s) {
  if (s == null) return '—'
  const p = Math.floor(s / 60)
  const g = s - p * 60
  return p + ':' + (g < 10 ? '0' : '') + g.toFixed(1)
}

/** khung da xu ly gan thoi diem t nhat (mang da sap theo t) — tim nhi phan */
function khungGanNhat(ds, t) {
  let lo = 0
  let hi = ds.length - 1
  if (hi < 0) return null
  while (lo < hi) {
    const m = (lo + hi) >> 1
    if (ds[m].t < t) lo = m + 1
    else hi = m
  }
  return lo > 0 && Math.abs(ds[lo - 1].t - t) < Math.abs(ds[lo].t - t) ? ds[lo - 1] : ds[lo]
}

/**
 * AI VIDEO (dich vu Python + GPU): chon video la tai len NGAY -> may chu lay mau khung (OpenCV + FFmpeg), doc bien so
 * + ma container tung khung, GOM theo doi tuong va BO PHIEU theo thoi gian. Giao dien hoi tien do moi 0,7 giay: doi tuong
 * hien dan khi dang xu ly; phat video thi khung phat hien ve de len dung thoi diem.
 */
export default function AiVideo({ tatDichVu, tepNgoai }) {
  const bao = useThongBao()
  const [loai, setLoai] = useState('ca-hai')
  const [fps, setFps] = useState(3)
  const [tep, setTep] = useState(null)
  const [urlVideo, setUrlVideo] = useState(null)
  const [viec, setViec] = useState(null)
  const [dangTai, setDangTai] = useState(false)
  const [khongPhatDuoc, setKhongPhatDuoc] = useState(false)
  const [, setSoKhung] = useState(0)
  const khungRef = useRef([])
  const videoRef = useRef(null)
  const canvasRef = useRef(null)
  const hoiRef = useRef(null)
  const viecRef = useRef(null)
  viecRef.current = viec

  useEffect(() => {
    if (!tep) return undefined
    const u = URL.createObjectURL(tep)
    setUrlVideo(u)
    setKhongPhatDuoc(false)
    return () => URL.revokeObjectURL(u)
  }, [tep])

  // video chon tu o "Anh" (tab anh): nhan dien luon
  useEffect(() => {
    if (!tepNgoai?.tep) return
    setTep(tepNgoai.tep)
    batDau(tepNgoai.tep)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tepNgoai])

  // roi trang: thoi hoi + dung viec dang chay tren may chu (khong de GPU chay video khong ai xem)
  useEffect(
    () => () => {
      hoiRef.current?.abort()
      const v = viecRef.current
      if (v && !v.xong) goiApi('/api/ai-thi-giac/video/' + v.maViec, { method: 'DELETE' }).catch(() => {})
    },
    [],
  )

  async function batDau(t = tep) {
    if (!t) return
    hoiRef.current?.abort()
    const v0 = viecRef.current
    if (v0 && !v0.xong) goiApi('/api/ai-thi-giac/video/' + v0.maViec, { method: 'DELETE' }).catch(() => {})
    const ac = new AbortController()
    hoiRef.current = ac
    khungRef.current = []
    setSoKhung(0)
    setViec(null)
    setDangTai(true)
    let v
    try {
      v = await goiApi(`/api/ai-thi-giac/video?loai=${loai}&fps=${fps}`, {
        method: 'POST',
        body: t,
        headers: { 'X-Ten-Tep': encodeURIComponent(t.name) },
        signal: ac.signal,
      })
    } catch (e) {
      if (e?.name !== 'AbortError') bao.loi(e)
      return
    } finally {
      setDangTai(false)
    }
    setViec(v)
    let tu = 0
    while (!ac.signal.aborted) {
      await new Promise((ok) => setTimeout(ok, 700))
      if (ac.signal.aborted) return
      let r
      try {
        r = await goiApi(`/api/ai-thi-giac/video/${v.maViec}?tu=${tu}`, { signal: ac.signal })
      } catch (e) {
        if (e?.name !== 'AbortError') bao.loi(e)
        return
      }
      if (r.khung?.length) {
        khungRef.current = khungRef.current.concat(r.khung)
        tu = khungRef.current.length
        setSoKhung(tu)
      }
      setViec(r)
      if (r.xong) {
        if (r.trangThai === 'loi') bao.loi(r.thongBao)
        return
      }
    }
  }

  async function dung() {
    const v = viecRef.current
    if (!v) return
    try {
      await goiApi('/api/ai-thi-giac/video/' + v.maViec, { method: 'DELETE' })
    } catch (e) {
      bao.loi(e)
    }
  }

  function tua(giay) {
    const vd = videoRef.current
    if (!vd) return
    vd.currentTime = Math.max(0, giay - 0.2)
    vd.play().catch(() => {})
  }

  // ve khung phat hien cua khung DA XU LY gan thoi diem dang phat nhat, de len video
  useEffect(() => {
    let id
    let truoc = ''
    const lap = () => {
      id = requestAnimationFrame(lap)
      const vd = videoRef.current
      const c = canvasRef.current
      const v = viecRef.current
      if (!vd || !c) return
      const w = vd.clientWidth
      const h = vd.clientHeight
      const k = v?.rong && vd.videoWidth ? khungGanNhat(khungRef.current, vd.currentTime) : null
      const gan = k && Math.abs(k.t - vd.currentTime) <= 0.75 / (v.fpsLay || 3) + 0.05 ? k : null
      const dau = [w, h, gan?.t, khungRef.current.length].join('|')
      if (dau === truoc) return
      truoc = dau
      const dpr = window.devicePixelRatio || 1
      c.width = Math.round(w * dpr)
      c.height = Math.round(h * dpr)
      const g = c.getContext('2d')
      g.setTransform(dpr, 0, 0, dpr, 0, 0)
      g.clearRect(0, 0, w, h)
      if (!gan) return
      // video hien kieu "contain" trong the <video>: tinh khung noi dung + ti le tu toa do may chu
      const ti = Math.min(w / vd.videoWidth, h / vd.videoHeight)
      const ox = (w - vd.videoWidth * ti) / 2
      const oy = (h - vd.videoHeight * ti) / 2
      const sx = (vd.videoWidth / v.rong) * ti
      const sy = (vd.videoHeight / v.cao) * ti
      const doi = ([x1, y1, x2, y2]) => [ox + x1 * sx, oy + y1 * sy, ox + x2 * sx, oy + y2 * sy]
      if (gan.cont?.vung?.length) {
        for (const vg of gan.cont.vung) veKhung(g, doi(vg.hop), MAU_LOP[vg.lop] ?? '#3b82f6', null)
        if (gan.cont.giaTri) {
          const x1 = Math.min(...gan.cont.vung.map((vg) => vg.hop[0]))
          const y1 = Math.min(...gan.cont.vung.map((vg) => vg.hop[1]))
          const [px, py] = doi([x1, y1, x1, y1])
          veKhung(g, [px, py, px, py], '#dc2626', gan.cont.giaTri, 15)
        }
      }
      if (gan.bien) veKhung(g, doi(gan.bien.hop), gan.bien.chac ? MAU_BIEN_CHAC : MAU_BIEN_NGHI, gan.bien.giaTri ?? 'biển số', 15)
    }
    lap()
    return () => cancelAnimationFrame(id)
  }, [])

  const dangChay = dangTai || (viec && !viec.xong)
  const doiTuong = viec?.doiTuong ?? []
  const thoiLuong = viec?.thoiLuong || khungRef.current.at(-1)?.t || 1
  const tDaXuLy = khungRef.current.at(-1)?.t ?? 0
  const conLai =
    viec?.tongKhung && viec?.msMoiKhung && !viec.xong ? Math.round(((viec.tongKhung - viec.khungDaXuLy) * viec.msMoiKhung) / 1000) : null

  return (
    <div className="row g-3">
      <div className="col-12 col-xl-4">
        <TheNoiDung tieuDe="Nhận diện trên video" icon="bi-camera-video">
          <Truong nhan="Nhận diện">
            <div className="btn-group w-100" role="group" aria-label="Loại nhận diện trên video">
              {LOAI.map((l) => (
                <button key={l.gt} type="button" disabled={dangChay} className={'btn btn-sm ' + (loai === l.gt ? 'btn-primary' : 'btn-outline-primary')} onClick={() => setLoai(l.gt)}>
                  <i className={'bi me-1 ' + l.icon} />
                  {l.ten}
                </button>
              ))}
            </div>
          </Truong>
          <Truong nhan="Lấy mẫu" goiY="Xe qua cổng mất vài giây — 3 khung/giây đủ thấy biển/mã nhiều lần; nhiều khung hơn thì chậm hơn">
            <select className="form-select" value={fps} disabled={dangChay} onChange={(e) => setFps(Number(e.target.value))}>
              {[1, 2, 3, 5, 10].map((f) => (
                <option key={f} value={f}>
                  {f} khung / giây
                </option>
              ))}
            </select>
          </Truong>
          <Truong nhan="Video camera" goiY="Chọn là AI chạy luôn · mp4, mov, avi, mkv, webm · ≤ 200 MB, ≤ 10 phút">
            <input
              className="form-control"
              type="file"
              accept={NHAN_VIDEO}
              disabled={!!tatDichVu}
              onChange={(e) => {
                const t = e.target.files?.[0] ?? null
                setTep(t)
                if (t) batDau(t)
              }}
            />
          </Truong>
          {tep && (
            <div className="small text-body-secondary mb-2 text-truncate" title={tep.name}>
              <i className="bi bi-film me-1" />
              {tep.name} · {(tep.size / 1048576).toFixed(1)} MB
            </div>
          )}
          <div className="d-flex gap-2">
            <button type="button" className="btn btn-primary flex-fill" disabled={!tep || dangChay || !!tatDichVu} onClick={() => batDau()}>
              <i className="bi bi-arrow-repeat me-1" />
              Nhận diện lại
            </button>
            <button type="button" className="btn btn-outline-danger" disabled={!viec || viec.xong} onClick={dung}>
              <i className="bi bi-stop-circle me-1" />
              Dừng
            </button>
          </div>

          {(dangTai || viec) && (
            <div className="mt-3">
              <div className="d-flex justify-content-between small mb-1">
                <span>
                  {dangTai ? (
                    'Đang tải video lên…'
                  ) : (
                    <NhanTrangThai giaTri={TRANG_THAI[viec.trangThai] ?? viec.trangThai} mau={viec.trangThai === 'loi' ? 'do' : viec.xong ? 'ok' : 'run'} />
                  )}
                </span>
                {viec && (
                  <span className="text-body-secondary">
                    {viec.khungDaXuLy}/{viec.tongKhung ?? '?'} khung
                  </span>
                )}
              </div>
              <div className="progress" role="progressbar" aria-label="Tiến độ nhận diện video" aria-valuenow={Math.round((viec?.tienDo ?? 0) * 100)} aria-valuemin={0} aria-valuemax={100}>
                <div
                  className={'progress-bar' + (viec?.xong ? '' : ' progress-bar-striped progress-bar-animated')}
                  style={{ width: (dangTai ? 100 : Math.round((viec?.tienDo ?? 0) * 100)) + '%' }}
                />
              </div>
              {viec && (
                <div className="small text-body-secondary mt-2">
                  {viec.trangThai === 'dang-cho' && viec.viTriHangDoi > 0 && <div>Đang chờ {viec.viTriHangDoi} video trước xử lý xong.</div>}
                  <div>
                    Video {dinhGio(viec.thoiLuong)} · {viec.rong}×{viec.cao} · {viec.fpsVideo} khung/giây gốc → lấy {viec.fpsLay} khung/giây
                  </div>
                  {viec.msMoiKhung != null && (
                    <div>
                      GPU {viec.msMoiKhung} ms/khung
                      {conLai != null && <> · còn khoảng {conLai} giây</>}
                      {viec.xong && viec.giayXuLy != null && <> · xử lý xong trong {viec.giayXuLy} giây</>}
                    </div>
                  )}
                  {viec.thongBao && <div className={viec.trangThai === 'loi' ? 'text-danger' : ''}>{viec.thongBao}</div>}
                </div>
              )}
            </div>
          )}
        </TheNoiDung>

        <TheNoiDung tieuDe="Cách AI xử lý video" icon="bi-diagram-3">
          <ol className="small mb-0 cs-giai-thich ps-3">
            <li>
              <b>Lấy mẫu khung</b> bằng OpenCV + FFmpeg (mặc định 3 khung/giây) — khung liền nhau gần như giống hệt, đọc hết chỉ tốn GPU.
            </li>
            <li>
              <b>Đọc từng khung</b> bằng đúng bộ đọc ảnh: YOLO26 tìm biển/vùng mã → YOLO26 + RF-DETR đọc ký tự / CRNN + ISO 6346.
            </li>
            <li>
              <b>Gom theo đối tượng</b>: khung gần nhau về thời gian, chuỗi sai khác ≤ 2 ký tự (hoặc khung biển trùng chỗ) là cùng một xe / container.
            </li>
            <li>
              <b>Bỏ phiếu theo thời gian</b>: mỗi khung một phiếu, trọng số = độ tin cậy → khung mờ đọc sai bị các khung rõ “bầu lại”.
            </li>
          </ol>
        </TheNoiDung>
      </div>

      <div className="col-12 col-xl-8">
        <TheNoiDung tieuDe="Video và vùng phát hiện" icon="bi-bounding-box">
          {urlVideo ? (
            <>
              <div className="cs-video">
                <video
                  ref={videoRef}
                  src={urlVideo}
                  controls
                  muted
                  playsInline
                  preload="metadata"
                  onError={() => setKhongPhatDuoc(true)}
                  aria-label="Video đang nhận diện"
                />
                <canvas ref={canvasRef} aria-hidden="true" />
              </div>
              {khongPhatDuoc && (
                <div className="alert alert-info small mt-2 mb-0">
                  Trình duyệt không phát được định dạng video này — máy chủ vẫn nhận diện bình thường, xem ảnh khung rõ nhất ở danh sách bên dưới.
                </div>
              )}
              {viec && (
                <div className="cs-dong-thoi-gian mt-2" aria-label="Dòng thời gian: đoạn xuất hiện của từng đối tượng">
                  <div className="cs-dtg-da-xu-ly" style={{ width: Math.min(100, (100 * tDaXuLy) / thoiLuong) + '%' }} />
                  {doiTuong.map((d) => (
                    <button
                      key={d.ma}
                      type="button"
                      title={`${d.giaTri} · ${dinhGio(d.batDau)}–${dinhGio(d.ketThuc)}`}
                      className={'cs-dtg-doan ' + (d.loai === 'container' ? 'cont' : 'bien') + (d.chac ? '' : ' nghi')}
                      style={{ left: (100 * d.batDau) / thoiLuong + '%', width: Math.max(1.2, (100 * (d.ketThuc - d.batDau)) / thoiLuong) + '%' }}
                      onClick={() => tua(d.batDau)}
                    />
                  ))}
                </div>
              )}
              <div className="small text-body-secondary mt-1">
                Phát video để xem khung AI vẽ đè đúng thời điểm · <span style={{ color: MAU_BIEN_CHAC }}>■</span> biển số chắc chắn ·{' '}
                <span style={{ color: MAU_BIEN_NGHI }}>■</span> cần kiểm tra · <span style={{ color: '#f97316' }}>■</span> container
              </div>
            </>
          ) : (
            <div className="text-center text-body-secondary py-5">
              <i className="bi bi-camera-reels fs-1 d-block mb-2" />
              Chọn video camera cổng / bãi — AI tự đọc biển số và mã container.
            </div>
          )}
        </TheNoiDung>

        {viec && (
          <TheNoiDung tieuDe={`Đã nhận diện (${doiTuong.length})`} icon="bi-list-check">
            {doiTuong.length === 0 ? (
              <div className="text-body-secondary small">
                {viec.xong
                  ? 'Không thấy biển số xe Việt Nam hay mã container đọc được trong video.'
                  : 'Đang tìm… đối tượng sẽ hiện dần khi AI đọc tới.'}
              </div>
            ) : (
              <div className="row g-3">
                {doiTuong.map((d) => (
                  <div key={d.ma} className="col-12 col-md-6">
                    <div className={'cs-doi-tuong h-100' + (d.chac ? '' : ' nghi')}>
                      {d.anh && (
                        <button type="button" className="cs-doi-tuong-anh" onClick={() => tua(d.tKhungRoNhat)} title="Xem đoạn này trong video">
                          <img src={d.anh} alt={'Khung rõ nhất của ' + d.giaTri} />
                        </button>
                      )}
                      <div className="p-2">
                        <div className="d-flex align-items-center gap-2 flex-wrap">
                          <i className={'bi ' + (d.loai === 'container' ? 'bi-box-seam text-warning' : 'bi-car-front text-success')} />
                          <span className="fs-5 cs-ma">{d.giaTri}</span>
                          <NhanTrangThai giaTri={d.chac ? 'Chắc chắn' : 'Cần kiểm tra'} mau={d.chac ? 'ok' : 'warn'} />
                        </div>
                        <div className="small text-body-secondary mt-1">
                          {d.loai === 'container' ? 'Mã container (đúng ISO 6346)' : 'Biển số xe'} · {dinhGio(d.batDau)} → {dinhGio(d.ketThuc)} ·{' '}
                          {d.soKhungDongY}/{d.soKhung} khung đồng ý · tin cậy {Number(d.doTinCay).toFixed(2)}
                        </div>
                        {d.phieu?.length > 1 && (
                          <div className="small text-body-secondary">
                            Các khung đọc:{' '}
                            {d.phieu.map((p, i) => (
                              <span key={p.giaTri}>
                                {i > 0 && ', '}
                                <span className="cs-ma">{p.giaTri}</span> ×{p.soKhung}
                              </span>
                            ))}
                          </div>
                        )}
                        <button type="button" className="btn btn-link btn-sm px-0" onClick={() => tua(d.batDau)}>
                          <i className="bi bi-play-circle me-1" />
                          Xem trong video
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </TheNoiDung>
        )}
      </div>
    </div>
  )
}
