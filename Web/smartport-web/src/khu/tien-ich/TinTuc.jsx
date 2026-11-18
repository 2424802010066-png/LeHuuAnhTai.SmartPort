import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, PhanTrang } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgay, dinhDangSo } from '../../utils/dinhDang'
import { AnhTin, DauTrang } from './chung'

/**
 * Tin tuc & su kien tren website cong khai (thay Views/News/Index.cshtml).
 * Dia chi giu nguyen ban MVC: /news?page=2 — link cu, bookmark, ket qua tim kiem van mo dung trang.
 */
export default function TinTuc() {
  // Ten tham so tren dia chi giu "page" nhu ban MVC; API dung "trang"
  const [loc, datLoc] = useBoLoc({ page: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tien-ich/tin-tuc?trang=' + loc.page, { coToken: false })

  const ds = duLieu?.items || []

  return (
    <>
      <DauTrang icon="bi-newspaper" tieuDe="Tin tức & sự kiện" mucHienTai="Tin tức" />
      <div className="container pb-5">
        <BaoLoi loi={loi} thuLai={taiLai} />

        {dangTai && !ds.length ? (
          <div className="row g-4">
            {[0, 1, 2].map((i) => (
              <div className="col-md-6 col-lg-4" key={i}>
                <div className="ti-the-tin placeholder-glow">
                  <span className="placeholder ti-anh-tin d-block" />
                  <div className="p-3">
                    <span className="placeholder col-8 mb-2 d-block" />
                    <span className="placeholder col-12" />
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : !ds.length ? (
          <div className="text-center text-body-secondary py-5">
            <i className="bi bi-inbox fs-2 d-block mb-2" />
            Chưa có tin tức nào được đăng.
          </div>
        ) : (
          <div className="row g-4">
            {ds.map((t) => (
              <div className="col-md-6 col-lg-4" key={t.maTin}>
                <article className="ti-the-tin">
                  <Link to={'/news/details/' + t.maTin} tabIndex={-1} aria-hidden="true">
                    <AnhTin src={t.anh} alt={t.tieuDe} />
                  </Link>
                  <div className="p-3 d-flex flex-column flex-grow-1">
                    <div className="small text-body-secondary mb-2">
                      <i className="bi bi-calendar3 me-1" />
                      {dinhDangNgay(t.ngayDang)}
                      <span className="mx-1">·</span>
                      <i className="bi bi-eye me-1" />
                      {dinhDangSo(t.luotXem)} lượt xem
                      {t.noiBat && <span className="badge text-bg-warning ms-2">Nổi bật</span>}
                    </div>
                    <h2 className="ti-tieu-de-tin mb-2">
                      <Link to={'/news/details/' + t.maTin}>{t.tieuDe}</Link>
                    </h2>
                    <p className="small text-body-secondary mb-0">{t.tomTat}</p>
                  </div>
                </article>
              </div>
            ))}
          </div>
        )}

        <PhanTrang trang={duLieu?.trang} soTrang={duLieu?.soTrang} tong={duLieu?.tong} donVi="tin" doiTrang={(p) => datLoc({ page: p })} />
      </div>
    </>
  )
}
