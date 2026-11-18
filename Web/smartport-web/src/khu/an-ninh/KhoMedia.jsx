import { duongDanAnh, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { gioNgayNgan } from '../../utils/dinhDang'
import './an-ninh.css'

const NGUON = [
  { ma: '', ten: 'Tất cả' },
  { ma: 'KiemDem', ten: 'Kiểm đếm/QC' },
  { ma: 'AnNinh', ten: 'An ninh' },
  { ma: 'ALPR', ten: 'ALPR' },
  { ma: 'CameraAI', ten: 'Camera AI' },
]
const LOAI = [
  { ma: '', ten: 'Ảnh + video' },
  { ma: 'Anh', ten: 'Chỉ ảnh' },
  { ma: 'Video', ten: 'Chỉ video' },
]
const LOC_RONG = { nguon: '', loai: '', trang: 1, coTrang: 24 }

/**
 * KHO MEDIA AI (thay Admin/Security/Media của bản MVC): ảnh và video do các module AI
 * (kiểm đếm/QC, an ninh, ALPR, camera AI) ghi lại — xem lại mọi bằng chứng ở một chỗ.
 */
export default function KhoMedia() {
  const [loc, datLoc] = useBoLoc(LOC_RONG)
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/kho-media' + taoQuery(loc))

  const soTheoNguon = (ma) => duLieu?.theoNguon?.find((x) => x.nguon === ma)?.so ?? 0

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-image" mau="xanh" so={duLieu?.tongAnh ?? 0} nhan="Ảnh AI đã lưu" />
        </div>
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-camera-reels" mau="do" so={duLieu?.tongVideo ?? 0} nhan="Video AI đã lưu" />
        </div>
        <div className="col-md-4 col-12">
          <TheChiSo icon="bi-collection-play" mau="tim" so={duLieu?.tong ?? 0} nhan="Mục khớp bộ lọc" />
        </div>
      </div>

      <TheNoiDung tieuDe="Kho ảnh và video AI" icon="bi-collection-play">
        <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
          <span className="small fw-semibold">Nguồn:</span>
          {NGUON.map((n) => (
            <button
              key={n.ma || 'tat-ca'}
              type="button"
              className={'btn btn-sm ' + (loc.nguon === n.ma ? 'btn-primary' : 'btn-outline-secondary')}
              onClick={() => datLoc({ nguon: n.ma, trang: 1 })}
            >
              {n.ten}
              {n.ma && soTheoNguon(n.ma) > 0 && <span className="badge text-bg-light ms-1">{soTheoNguon(n.ma)}</span>}
            </button>
          ))}
          <span className="vr mx-1 d-none d-md-inline" />
          {LOAI.map((l) => (
            <button
              key={l.ma || 'moi-loai'}
              type="button"
              className={'btn btn-sm ' + (loc.loai === l.ma ? 'btn-primary' : 'btn-outline-secondary')}
              onClick={() => datLoc({ loai: l.ma, trang: 1 })}
            >
              {l.ten}
            </button>
          ))}
        </div>

        {dangTai && !duLieu ? (
          <div className="text-center text-body-secondary py-5">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải…
          </div>
        ) : !duLieu?.items?.length ? (
          <div className="text-center text-body-secondary py-5">
            <i className="bi bi-inbox d-block mb-2" style={{ fontSize: '2.6rem' }} />
            Kho trống — chạy <b>Trạm camera AI</b> hoặc <b>Kiểm đếm AI</b> để bắt đầu lưu ảnh/video.
          </div>
        ) : (
          <div className="row g-3">
            {duLieu.items.map((m) => (
              <div key={m.maMD} className="col-xl-3 col-lg-4 col-md-6">
                <div className="card sp-the an-the-media p-2 h-100">
                  {m.loai === 'Video' ? (
                    <video className="an-media" src={duongDanAnh(m.duongDan)} controls preload="metadata" aria-label={m.moTa || 'Video AI ' + m.maMD}>
                      <track kind="captions" />
                    </video>
                  ) : (
                    <a href={duongDanAnh(m.duongDan)} target="_blank" rel="noreferrer">
                      <img className="an-media" src={duongDanAnh(m.duongDan)} alt={m.moTa || 'Ảnh AI ' + m.maMD} loading="lazy" />
                    </a>
                  )}
                  <div className="d-flex justify-content-between align-items-center gap-1 mt-2 small">
                    <span className={'badge ' + (m.loai === 'Video' ? 'text-bg-danger' : 'text-bg-primary')}>
                      <i className={'bi me-1 ' + (m.loai === 'Video' ? 'bi-camera-reels' : 'bi-image')} />
                      {m.loai === 'Video' ? 'Video' : 'Ảnh'}
                    </span>
                    <span className="badge text-bg-secondary">{m.nguon}</span>
                    <span className="text-body-secondary">{gioNgayNgan(m.thoiGian)}</span>
                  </div>
                  {m.moTa && <div className="small mt-1 an-ket-qua">{m.moTa}</div>}
                </div>
              </div>
            ))}
          </div>
        )}

        <PhanTrang {...(duLieu || {})} donVi="mục" doiTrang={(t) => datLoc({ trang: t })} />
      </TheNoiDung>
    </>
  )
}
