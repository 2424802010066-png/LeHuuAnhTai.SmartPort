import { Fragment, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, TheNoiDung } from '../../components/ui'
import { dinhDangSo, dinhDangTien, tienGon } from '../../utils/dinhDang'
import OSo from './dungChung'
import { GIAI_DOAN, NHOM_CHUC_NANG } from './quyTrinhDuLieu'
import './dieu-hanh.css'

const NhanAI = () => <span className="dh-nhan-ai ms-1">AI</span>

/**
 * QUY TRINH VAN HANH — so do 12 giai doan gate-to-gate + danh muc day du chuc nang.
 * Thay man hinh MVC Admin/Dashboard/QuyTrinh: so do la noi dung tinh (quyTrinhDuLieu.js),
 * GET /api/quy-trinh-van-hanh chi cap CON SO dang nam o tung giai doan de moi buoc hien mot so song.
 */
export default function QuyTrinhVanHanh() {
  const { duLieu: d, loi, taiLai } = useApi('/api/quy-trinh-van-hanh')

  const soTheoBuoc = useMemo(() => {
    const m = new Map()
    for (const g of d?.giaiDoan || []) m.set(g.so, g)
    return m
  }, [d])

  const tongChucNang = NHOM_CHUC_NANG.reduce((s, n) => s + n.muc.length, 0)
  const soCoAi = NHOM_CHUC_NANG.reduce((s, n) => s + n.muc.filter((m) => m[3]).length, 0)

  let stt = 0

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row row-cols-2 row-cols-md-4 g-3 mb-3">
        <div className="col">
          <OSo mau="cam" so={dinhDangSo(d?.soViecCanXuLy ?? 0)} nhan="Việc đang chờ người xử lý" phu="booking · bến · sự cố · agent" icon="bi-exclamation-diamond" />
        </div>
        <div className="col">
          <OSo mau="la" so={tienGon(d?.tienChoThu ?? 0)} donVi="đ" nhan="Tiền chờ thu" phu="booking + phí lưu bãi" icon="bi-cash-stack" />
        </div>
        <div className="col">
          <OSo so={GIAI_DOAN.length} nhan="Giai đoạn gate-to-gate" icon="bi-signpost-split" />
        </div>
        <div className="col">
          <OSo mau="tim" so={tongChucNang} nhan="Chức năng đang chạy" phu={soCoAi + ' chức năng có AI'} icon="bi-grid-3x3-gap" />
        </div>
      </div>

      <h6 className="fw-bold mb-2">
        <i className="bi bi-1-square me-1 text-primary" />
        PHẦN A — LUỒNG CHÍNH {GIAI_DOAN.length} GIAI ĐOẠN
      </h6>
      <div className="row row-cols-1 row-cols-md-2 row-cols-xl-3 g-3 mb-4">
        {GIAI_DOAN.map((b) => {
          const s = soTheoBuoc.get(b.so)
          return (
            <div className="col" key={b.so}>
              <Link className={'dh-buoc dh-the-bam' + (s?.canXuLy ? ' can-xu-ly' : '')} to={b.duong}>
                <div className="d-flex align-items-start gap-2">
                  <span className="dh-buoc-so">{b.so}</span>
                  <div className="flex-grow-1 min-w-0">
                    <div className="dh-buoc-ten">
                      <i className={'bi ' + b.icon + ' me-1 text-primary'} />
                      {b.ten}
                      {b.ai ? <NhanAI /> : null}
                    </div>
                    <div className="dh-buoc-vai-tro">{b.vaiTro}</div>
                  </div>
                  <i className="bi bi-arrow-right-circle text-primary" />
                </div>
                <div className="dh-buoc-mo-ta">{b.moTa}</div>
                <div className="dh-buoc-chi-so">
                  {s ? (
                    <>
                      <b>{s.laTien ? dinhDangTien(s.giaTri) : dinhDangSo(s.giaTri)}</b>
                      <span className="small text-body-secondary">{s.nhan}</span>
                    </>
                  ) : (
                    <span className="small text-body-secondary">—</span>
                  )}
                </div>
              </Link>
            </div>
          )
        })}
      </div>

      <h6 className="fw-bold mb-2">
        <i className="bi bi-2-square me-1 text-primary" />
        PHẦN B — DANH MỤC ĐẦY ĐỦ {tongChucNang} CHỨC NĂNG
      </h6>
      <TheNoiDung khongDem>
        <div className="table-responsive">
          <table className="table table-sm sp-bang table-hover align-middle mb-0">
            <thead>
              <tr>
                <th scope="col" style={{ width: 52 }}>
                  #
                </th>
                <th scope="col">CHỨC NĂNG</th>
                <th scope="col">VAI TRÒ</th>
                <th scope="col" style={{ width: 60 }}>
                  AI
                </th>
                <th scope="col" style={{ width: 50 }} />
              </tr>
            </thead>
            <tbody>
              {NHOM_CHUC_NANG.map((nhom) => (
                <Fragment key={nhom.ten}>
                  <tr className="dh-nhom-dong">
                    <td colSpan={5}>{nhom.ten}</td>
                  </tr>
                  {nhom.muc.map(([ten, vaiTro, duong, ai]) => {
                    stt += 1
                    return (
                      <tr key={nhom.ten + ten}>
                        <td>
                          <span className="dh-stt">{stt}</span>
                        </td>
                        <td>{ten}</td>
                        <td className="small text-body-secondary">{vaiTro}</td>
                        <td>{ai ? <span className="dh-nhan-ai">AI</span> : null}</td>
                        <td>
                          <Link className="btn btn-sm btn-outline-primary py-0" to={duong} title={'Mở: ' + ten} aria-label={'Mở: ' + ten}>
                            <i className="bi bi-box-arrow-up-right" />
                          </Link>
                        </td>
                      </tr>
                    )
                  })}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      </TheNoiDung>
    </>
  )
}
