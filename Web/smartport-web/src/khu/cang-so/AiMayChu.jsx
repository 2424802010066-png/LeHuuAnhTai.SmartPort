import { useEffect, useRef, useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { NhanTrangThai, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import AiVideo from './AiVideo'
import { MAU_BIEN_CHAC, MAU_BIEN_NGHI, MAU_LOP, NHAN_VIDEO, TEN_LOP, laVideo, veKhung } from './aiMau'
import './cang-so.css'

const LOAI = [
  { gt: 'ca-hai', ten: 'Cả hai', icon: 'bi-layers' },
  { gt: 'container', ten: 'Mã container', icon: 'bi-box-seam' },
  { gt: 'bien', ten: 'Biển số', icon: 'bi-car-front' },
]

function docAnh(tep) {
  return new Promise((ok, loi) => {
    const r = new FileReader()
    r.onload = () => ok(r.result)
    r.onerror = loi
    r.readAsDataURL(tep)
  })
}

function phanTram(x) {
  return x == null ? '—' : x + '%'
}

/** Ket qua 1 bo doc (bien so HOAC container) tren anh */
function KetQuaAnh({ laBien, kq, msVongTron }) {
  const ketQua = laBien ? kq.bienSo : kq.soCont
  const hopLe = laBien ? kq.hopLeVN && !kq.canKiemTay : kq.hopLe
  return (
    <div className={'alert mt-3 mb-0 ' + (ketQua && hopLe ? 'alert-success' : ketQua || kq.bienSoDoan ? 'alert-warning' : 'alert-secondary')}>
      <div className="small fw-semibold text-body-secondary">
        <i className={'bi me-1 ' + (laBien ? 'bi-car-front' : 'bi-box-seam')} />
        {laBien ? 'Biển số xe' : 'Mã container'}
      </div>
      <div className="fs-4 cs-ma">{ketQua || (kq.bienSoDoan ? 'Không chắc' : 'Không đọc được')}</div>
      <div className="small mt-1">
        {laBien ? (
          <NhanTrangThai
            giaTri={kq.canKiemTay ? 'Chuyển kiểm tay' : kq.hopLeVN ? 'Đúng mẫu biển VN' : 'Không đúng mẫu'}
            mau={kq.hopLeVN && !kq.canKiemTay ? 'ok' : 'warn'}
          />
        ) : (
          <NhanTrangThai giaTri={kq.hopLe ? 'Đúng ISO 6346 — tin cậy' : 'Chuyển kiểm tay'} mau={kq.hopLe ? 'ok' : 'warn'} />
        )}
      </div>
      <div className="small text-body-secondary mt-1">
        Độ tin cậy {kq.doTinCay != null ? Number(kq.doTinCay).toFixed(3) : '—'}
        {laBien && kq.nguongTinCay != null && <> (ngưỡng {kq.nguongTinCay})</>} · máy chủ {kq.ms} ms
        {msVongTron != null && <> · vòng tròn {msVongTron} ms</>}
      </div>
      {kq.cachDoc && <div className="small text-body-secondary">{kq.cachDoc}</div>}
      {laBien && kq.bienSoDoan && (
        <div className="small text-body-secondary">
          AI đoán: <span className="cs-ma">{kq.bienSoDoan}</span> (chưa đủ tin cậy để tự mở cổng)
        </div>
      )}
      {laBien && kq.bienSoTho && kq.bienSoTho !== (kq.bienSo ?? kq.bienSoDoan) && (
        <div className="small text-body-secondary">
          1 mô hình (YOLO26) đọc: <span className="cs-ma">{kq.bienSoTho}</span>
        </div>
      )}
      {!laBien && kq.docThamLam && kq.docThamLam !== kq.soCont && (
        <div className="small text-body-secondary">
          Đọc thô (chưa ràng buộc ISO): <span className="cs-ma">{kq.docThamLam}</span>
        </div>
      )}
      {kq.thongBao && <div className="small">{kq.thongBao}</div>}
    </div>
  )
}

/** Doc 1 ANH: bien so / ma container / ca hai -> ve khung vung phat hien + ket qua. Chon nham VIDEO o day -> chuyen sang che do video */
function AiAnh({ tatDichVu, onVideo }) {
  const { chay, dangChay } = useThaoTac()
  const [loai, setLoai] = useState('ca-hai')
  const [anh, setAnh] = useState(null)
  const [kq, setKq] = useState(null)
  const khung = useRef(null)

  // ve anh + khung vung phat hien len canvas
  useEffect(() => {
    if (!anh) return
    const img = new Image()
    img.onload = () => {
      const c = khung.current
      if (!c) return
      const ti = Math.min(1, 960 / img.width)
      c.width = Math.round(img.width * ti)
      c.height = Math.round(img.height * ti)
      const g = c.getContext('2d')
      g.drawImage(img, 0, 0, c.width, c.height)
      const doi = (hop) => hop.map((v) => v * ti)
      for (const v of kq?.cont?.vung ?? [])
        veKhung(g, doi(v.hop), MAU_LOP[v.lop] ?? '#3b82f6', (TEN_LOP[v.lop] ?? v.lop) + (v.conf != null ? ' ' + Number(v.conf).toFixed(2) : ''))
      const b = kq?.bien
      if (b?.hop) veKhung(g, doi(b.hop), b.bienSo ? MAU_BIEN_CHAC : MAU_BIEN_NGHI, b.bienSo ?? b.bienSoDoan ?? 'biển số')
    }
    img.src = anh
  }, [anh, kq])

  async function doc() {
    const t0 = performance.now()
    const r = await chay(
      async () => {
        const ra = {}
        // 2 bo doc tuan tu (dich vu AI chi co 1 GPU, tu xep hang) — gui song song cung khong nhanh hon
        if (loai !== 'bien') ra.cont = await goiApi('/api/ai-thi-giac/container', { method: 'POST', body: { anh } })
        if (loai !== 'container') ra.bien = await goiApi('/api/ai-thi-giac/bien-so', { method: 'POST', body: { anh } })
        return ra
      },
      { imLang: true },
    )
    if (r) setKq({ ...r, msVongTron: Math.round(performance.now() - t0) })
  }

  return (
    <div className="row g-3">
      <div className="col-12 col-xl-4">
        <TheNoiDung tieuDe="Đọc ảnh bằng AI máy chủ" icon="bi-cpu">
          <Truong nhan="Nhận diện">
            <div className="btn-group w-100" role="group" aria-label="Loại nhận diện">
              {LOAI.map((l) => (
                <button
                  key={l.gt}
                  type="button"
                  className={'btn btn-sm ' + (loai === l.gt ? 'btn-primary' : 'btn-outline-primary')}
                  onClick={() => {
                    setLoai(l.gt)
                    setKq(null)
                  }}
                >
                  <i className={'bi me-1 ' + l.icon} />
                  {l.ten}
                </button>
              ))}
            </div>
          </Truong>
          <Truong nhan="Ảnh hoặc video camera" goiY="Ảnh JPEG / PNG / WebP ≤ 12 MB · chọn video (mp4, mov, avi, mkv, webm) thì tự chuyển sang nhận diện video">
            <input
              className="form-control"
              type="file"
              accept={'image/jpeg,image/png,image/webp,' + NHAN_VIDEO}
              onChange={async (e) => {
                const t = e.target.files?.[0]
                setKq(null)
                if (laVideo(t)) {
                  e.target.value = ''
                  onVideo(t)
                  return
                }
                setAnh(t ? await docAnh(t) : null)
              }}
            />
          </Truong>
          <button type="button" className="btn btn-primary w-100" disabled={!anh || dangChay || !!tatDichVu} onClick={doc}>
            <i className="bi bi-magic me-1" />
            {dangChay ? 'Đang đọc…' : 'Đọc'}
          </button>

          {kq?.cont && <KetQuaAnh kq={kq.cont} msVongTron={kq.bien ? null : kq.msVongTron} />}
          {kq?.bien && <KetQuaAnh laBien kq={kq.bien} msVongTron={kq.msVongTron} />}
        </TheNoiDung>
      </div>
      <div className="col-12 col-xl-8">
        <TheNoiDung tieuDe="Ảnh và vùng phát hiện" icon="bi-bounding-box">
          {anh ? (
            <canvas ref={khung} className="w-100 rounded" style={{ maxHeight: 640, objectFit: 'contain' }} aria-label="Ảnh và khung vùng AI phát hiện" />
          ) : (
            <div className="text-center text-body-secondary py-5">
              <i className="bi bi-image fs-1 d-block mb-2" />
              Chọn ảnh camera để AI đọc.
            </div>
          )}
        </TheNoiDung>
      </div>
    </div>
  )
}

/**
 * AI THI GIAC MAY CHU (dich vu Python + GPU, sau API gateway):
 *  - ANH: doc BIEN SO (YOLO26 tim bien + YOLO26 & RF-DETR doc ky tu, gop + giai ma theo mau bien VN + nguong tin cay)
 *    va/hoac MA CONTAINER (YOLO26 tim vung ma + CRNN + rang buoc ISO 6346 + gop chung cu nhieu vung).
 *  - VIDEO: tai video len -> may chu lay mau khung, doc tung khung, gom theo doi tuong + bo phieu theo thoi gian.
 * Chi so the tren cung do tren tap TEST giu rieng.
 */
export default function AiMayChu() {
  const { duLieu: mh, loi: loiMh } = useApi('/api/ai-thi-giac/mo-hinh')
  const [che, setChe] = useState('anh')
  const [videoTuAnh, setVideoTuAnh] = useState(null)
  const cs = mh?.chiSoTest ?? {}

  return (
    <>
      {loiMh && (
        <div className="alert alert-warning">
          <i className="bi bi-info-circle me-1" />
          Dịch vụ AI thị giác máy chủ (Python + GPU) chưa chạy. Nó chỉ có ở chế độ <b>microservices</b>
          (<code>MicroServices/chay_microservices.ps1</code>); bản gộp vẫn dùng AI chạy trong trình duyệt.
        </div>
      )}
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-car-front" mau="xanh" so={phanTram(cs.bienSoDauCuoi)} nhan="biển số — nguyên ảnh camera (test)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-card-text" mau="la" so={phanTram(cs.bienSoAnhCat)} nhan="biển số — ảnh biển đã cắt (test)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-seam" mau="cam" so={phanTram(cs.containerToanAnh)} nhan="mã container — nguyên ảnh (test)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-upc" mau="do" so={phanTram(cs.containerVungMa)} nhan="mã container — vùng mã (test)" />
        </div>
      </div>

      <ul className="nav nav-pills mb-3" role="tablist">
        <li className="nav-item" role="presentation">
          <button type="button" role="tab" aria-selected={che === 'anh'} className={'nav-link' + (che === 'anh' ? ' active' : '')} onClick={() => setChe('anh')}>
            <i className="bi bi-image me-1" />
            Ảnh
          </button>
        </li>
        <li className="nav-item" role="presentation">
          <button type="button" role="tab" aria-selected={che === 'video'} className={'nav-link' + (che === 'video' ? ' active' : '')} onClick={() => setChe('video')}>
            <i className="bi bi-camera-video me-1" />
            Video
          </button>
        </li>
      </ul>

      {/* giu ca 2 che do trong DOM: chuyen tab khong mat video dang xu ly */}
      <div className={che === 'anh' ? '' : 'd-none'}>
        <AiAnh
          tatDichVu={!!loiMh}
          onVideo={(tep) => {
            setChe('video')
            setVideoTuAnh({ tep, lan: Date.now() })
          }}
        />
      </div>
      <div className={che === 'video' ? '' : 'd-none'}>
        <AiVideo tatDichVu={!!loiMh} tepNgoai={videoTuAnh} />
      </div>

      <TheNoiDung tieuDe="Mô hình đang nạp" icon="bi-boxes" lop="mt-3">
        <ul className="small mb-0 cs-giai-thich">
          {(mh?.moHinh ?? []).map((m) => (
            <li key={m}>
              <b>{m}</b>: {mh?.moTa?.[m] ?? ''}
            </li>
          ))}
          {mh?.video && (
            <li>
              <b>video</b>: {mh.video.cachLam} · {mh.video.dinhDang} · ≤ {mh.video.toiDaMB} MB, ≤ {Math.round(mh.video.toiDaGiay / 60)} phút
            </li>
          )}
          {!mh?.moHinh?.length && <li className="text-body-secondary">—</li>}
        </ul>
      </TheNoiDung>
    </>
  )
}
