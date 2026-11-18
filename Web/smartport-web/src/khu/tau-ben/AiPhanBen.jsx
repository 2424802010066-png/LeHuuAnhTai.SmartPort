import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { soGon, soMet } from './chung'
import './tau-ben.css'

/**
 * AI GOI Y PHAN BO BEN: moi chuyen Du kien chua co ben -> toi da 3 ben xep theo diem phu hop
 * (chieu dai, do sau, khong trung lich, nang suat cau bo) + nut "Phan ben nay" (thay Admin/AiTools/Berth).
 */
export default function AiPhanBen() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/phan-ben-ai')
  const { chay, dangChay } = useThaoTac()
  const [dangGui, setDangGui] = useState(null) // "maChuyen-maBen" dang ap dung -> vong xoay dung nut

  async function apDung(s, o) {
    setDangGui(s.maChuyen + '-' + o.maBen)
    await chay(() => goiApi('/api/phan-ben-ai/ap-dung', { method: 'POST', body: { maChuyen: s.maChuyen, maBen: o.maBen } }))
    setDangGui(null)
    // Nhu MVC: thanh cong hay bi tu choi deu tinh lai goi y (ben vua xep khong con la phuong an cua chuyen khac)
    taiLai({ im: true })
  }

  if (dangTai && !duLieu) return <DangTai chu="AI đang phân tích cầu bến…" />

  const ds = duLieu || []
  const soCoPhuongAn = ds.filter((s) => s.phuongAn.length > 0).length
  const tongMoves = ds.reduce((tong, s) => tong + s.contNhap + s.contXuat, 0)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="sp-hop-ai mb-3">
        <div className="d-flex flex-wrap align-items-center gap-3">
          <div className="flex-grow-1">
            <span className="sp-nhan-ai">
              <i className="bi bi-stars me-1" />
              AI PHÂN BỔ BẾN
            </span>
            <div className="h5 fw-bold mb-0 mt-2">Gợi ý bến cho các chuyến chưa có bến</div>
          </div>
          <div className="text-center px-2">
            <div className="sp-so-lon">{dinhDangSo(ds.length)}</div>
            <div className="sp-chu-nhat small">chuyến cần phân bến</div>
          </div>
          <div className="text-center px-2">
            <div className="sp-so-lon">{dinhDangSo(soCoPhuongAn)}</div>
            <div className="sp-chu-nhat small">có bến phù hợp</div>
          </div>
          <div className="text-center px-2">
            <div className="sp-so-lon">{dinhDangSo(tongMoves)}</div>
            <div className="sp-chu-nhat small">moves chờ xếp bến</div>
          </div>
          <button type="button" className="btn btn-sm btn-light" onClick={() => taiLai()} disabled={dangTai} title="Chạy lại gợi ý" aria-label="Chạy lại gợi ý">
            {dangTai ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-arrow-clockwise" />}
          </button>
        </div>
      </div>

      {ds.length === 0 && !loi && (
        <div className="card sp-the">
          <div className="card-body text-center py-5">
            <div className="display-5 text-success mb-2">
              <i className="bi bi-check2-circle" />
            </div>
            <h2 className="h5 fw-bold">Mọi chuyến tàu dự kiến đều đã có bến</h2>
            <p className="text-body-secondary mb-3">Khi có chuyến mới chưa phân bến, AI sẽ phân tích và gợi ý tại đây.</p>
            <Link className="btn btn-primary" to="/admin/vesselcalls">
              <i className="bi bi-calendar2-week me-1" />
              Về trang điều độ
            </Link>
          </div>
        </div>
      )}

      {ds.map((s) => (
        <TheNoiDung
          key={s.maChuyen}
          icon="bi-water"
          tieuDe={
            <>
              Chuyến <strong>{s.maChuyenHieu}</strong> — {s.tenTau}
              <span className="sp-nhan sp-nhan-info ms-2">ETA {dinhDangNgayGio(s.eta)}</span>
            </>
          }
          phai={
            <span className="small text-body-secondary">
              LOA <strong>{soMet(s.chieuDaiTau)} m</strong> · Mớn nước <strong>{soMet(s.monNuocTau)} m</strong> · {dinhDangSo(s.sucChuaTEU)} TEU · Cont N/X:{' '}
              {dinhDangSo(s.contNhap)}/{dinhDangSo(s.contXuat)}
            </span>
          }
        >
          {s.phuongAn.length === 0 && (
            <div className="alert alert-warning mb-0">
              <i className="bi bi-exclamation-triangle me-1" />
              Không có bến nào phù hợp trong khung thời gian này (thiếu chiều dài/độ sâu hoặc trùng lịch). Hãy điều chỉnh ETA/ETD hoặc xem xét bến đang bảo trì.
              <Link to={'/admin/vesselcalls/form/' + s.maChuyen} className="alert-link ms-1">
                Sửa chuyến
              </Link>
            </div>
          )}
          <div className="row g-3">
            {s.phuongAn.map((o, i) => (
              <div className="col-lg-4" key={o.maBen}>
                <div className={'tb-phuong-an' + (i === 0 ? ' de-xuat' : '')}>
                  <div className="d-flex justify-content-between align-items-center gap-2 mb-2">
                    <div>
                      <span className="fw-bold fs-5">{o.tenBen}</span>
                      {i === 0 && (
                        <span className="sp-nhan sp-nhan-run ms-2">
                          <i className="bi bi-stars me-1" />
                          AI đề xuất
                        </span>
                      )}
                    </div>
                    <span className="tb-diem-ai" title="Điểm phù hợp (0–99)">
                      {o.diem}
                    </span>
                  </div>
                  <div className="small text-body-secondary mb-1">
                    Dài {soMet(o.chieuDai)} m · Sâu {soMet(o.doSau)} m · {o.soCau} cẩu bờ
                  </div>
                  <div className="small fw-semibold mb-2 tb-gio-ai">
                    <i className="bi bi-stopwatch me-1" />
                    AI dự đoán giải phóng tàu trong ~{soGon(o.gioLamHang)} giờ
                  </div>
                  <ul className="small mb-3">
                    {o.lyDo.map((ly, j) => (
                      <li key={j}>{ly}</li>
                    ))}
                  </ul>
                  <button
                    type="button"
                    className={'btn btn-sm w-100 mt-auto ' + (i === 0 ? 'btn-primary' : 'btn-outline-primary')}
                    disabled={dangChay}
                    onClick={() => apDung(s, o)}
                  >
                    {dangGui === s.maChuyen + '-' + o.maBen ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2-circle me-1" />}
                    Phân bến này
                  </button>
                </div>
              </div>
            ))}
          </div>
        </TheNoiDung>
      ))}
    </>
  )
}
