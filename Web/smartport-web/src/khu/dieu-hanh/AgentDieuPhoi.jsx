import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import OSo from './dungChung'
import './dieu-hanh.css'

const VONG_AGENT = [
  ['Quan sát', 'Quét các nguồn dữ liệu nghiệp vụ theo thời gian thực'],
  ['Suy luận', 'Liên kết dữ kiện, đối chiếu ràng buộc an toàn và ngưỡng vận hành'],
  ['Lập kế hoạch', 'Chấm điểm ưu tiên 0–100, xếp việc cần làm trước'],
  ['Hành động', 'Đề xuất thao tác mở đúng màn hình xử lý, một cú nhấp'],
]

/**
 * AI AGENT DIEU PHOI — viec can lam xep theo muc uu tien, kem chuoi suy luan.
 * Thay man hinh MVC Admin/AiTools/Agent. Du lieu tu GET /api/agent-dieu-phoi (chay agent mot luot).
 */
export default function AgentDieuPhoi() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/agent-dieu-phoi')

  if (dangTai && !d) return <DangTai chu="Agent đang quét toàn cảng…" />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const khanCap = d.soKhanCap > 0

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
        <div className="small text-body-secondary">
          <i className="bi bi-stopwatch me-1" />
          Quét lúc {dinhDangNgayGio(d.thoiDiem)} · xử lý trong {dinhDangSo(d.msXuLy)} ms
        </div>
        <button type="button" className="btn btn-sm btn-primary" onClick={() => taiLai()} disabled={dangTai}>
          {dangTai ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-arrow-repeat me-1" />}
          Chạy lại agent
        </button>
      </div>

      <div className={'dh-ket-luan mb-3' + (khanCap ? ' khan-cap' : '')}>
        <i className={'bi dh-ket-luan-icon ' + (khanCap ? 'bi-exclamation-octagon-fill' : 'bi-check-circle-fill')} aria-hidden="true" />
        <div className="flex-grow-1">
          <div className="fw-bold small text-uppercase">Kết luận của agent</div>
          <div>{d.ketLuan}</div>
        </div>
      </div>

      <div className="row row-cols-2 row-cols-md-4 g-3 mb-3">
        <div className="col">
          <OSo mau="do" so={d.soKhanCap} nhan="Việc khẩn cấp" phu="điểm ưu tiên từ 85" icon="bi-exclamation-octagon" />
        </div>
        <div className="col">
          <OSo mau="cam" so={d.soCao} nhan="Ưu tiên cao" phu="65 – 84 điểm" icon="bi-arrow-up-circle" />
        </div>
        <div className="col">
          <OSo so={d.soTrungBinh} nhan="Ưu tiên trung bình" phu="dưới 65 điểm" icon="bi-list-check" />
        </div>
        <div className="col">
          <OSo mau="tim" so={d.tongHanhDong} nhan="Hành động đề xuất" icon="bi-diagram-3" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-8">
          <TheNoiDung tieuDe="Kế hoạch hành động (xếp theo mức ưu tiên)" icon="bi-list-ol">
            {d.hanhDong.length === 0 ? (
              <div className="text-center text-body-secondary py-5">
                <i className="bi bi-check2-circle dh-chu-xanh-la" style={{ fontSize: '3rem' }} />
                <div className="mt-2">Không phát hiện vấn đề nào cần can thiệp. Hệ thống đang vận hành tốt.</div>
              </div>
            ) : (
              d.hanhDong.map((a, i) => (
                <div className={'dh-the-viec muc-' + (a.mauMuc || 'info')} key={a.tieuDe + i}>
                  <div className="d-flex gap-3 align-items-start">
                    <span className="dh-hang">{i + 1}</span>
                    <div className="flex-grow-1 min-w-0">
                      <div className="d-flex flex-wrap align-items-center gap-2">
                        <span className="dh-tieu-de-viec">{a.tieuDe}</span>
                        <span className={'badge text-bg-' + (a.mauMuc || 'info')}>{a.mucDo}</span>
                        <span className="dh-chip">
                          <i className="bi bi-tag me-1" />
                          {a.nhom}
                        </span>
                      </div>
                      <div className="small text-body-secondary mt-1">{a.moTa}</div>
                    </div>
                    <div className="dh-diem">
                      <div className="dh-diem-so">{a.diemUuTien}</div>
                      <div className="dh-diem-nhan">điểm</div>
                    </div>
                  </div>

                  {a.lyDo?.length > 0 && (
                    <>
                      <div className="small fw-semibold mt-2">
                        <i className="bi bi-diagram-2 me-1" />
                        Chuỗi suy luận của agent
                      </div>
                      <ol className="dh-suy-luan">
                        {a.lyDo.map((l, j) => (
                          <li key={j}>{l}</li>
                        ))}
                      </ol>
                    </>
                  )}

                  <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mt-2">
                    <div className="dh-tac-dong">
                      <i className="bi bi-graph-up-arrow me-1" />
                      {a.tacDong}
                    </div>
                    {a.duongDan ? (
                      <Link className="btn btn-sm btn-primary" to={a.duongDan}>
                        <i className="bi bi-lightning-charge me-1" />
                        {a.tenHanhDong}
                      </Link>
                    ) : (
                      <span className="small text-body-secondary">{a.tenHanhDong}</span>
                    )}
                  </div>
                </div>
              ))
            )}
          </TheNoiDung>
        </div>

        <div className="col-lg-4">
          <TheNoiDung tieuDe="Dữ liệu agent đã quan sát" icon="bi-eye">
            <ul className="list-unstyled mb-0 dh-quan-sat">
              {d.quanSat.map((o, i) => (
                <li key={i}>
                  <i className="bi bi-dot" />
                  <span>{o}</span>
                </li>
              ))}
            </ul>
          </TheNoiDung>

          <TheNoiDung tieuDe="Cách agent hoạt động" icon="bi-cpu">
            {VONG_AGENT.map(([ten, mo], i) => (
              <div className="d-flex gap-2 align-items-start mb-2" key={ten}>
                <span className="dh-hang">{i + 1}</span>
                <div>
                  <div className="fw-semibold small">{ten}</div>
                  <div className="small text-body-secondary">{mo}</div>
                </div>
              </div>
            ))}
            <div className="alert alert-light border small mb-0 mt-2">
              Agent này chỉ <b>đề xuất</b> — người bấm nút mới thực hiện. Muốn agent tự làm (lập phiếu thu, sinh EDI) thì bật ở{' '}
              <Link to="/admin/agent">Agent tự động</Link>.
            </div>
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
