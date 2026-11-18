import { useEffect, useRef } from 'react'
import { Link, useParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { AnhTin, DauTrang, NoiDungBaiViet } from './chung'

/**
 * Chi tiet mot bai viet (thay Views/News/Details.cshtml) — dia chi giu nguyen: /news/details/5.
 *
 * Khac ban MVC mot cho: MVC cong luot xem ngay trong action GET. O day GET chi doc
 * (mot lan tai trang cua trinh duyet / bo doc RSS khong duoc lam sai so lieu),
 * luot xem cong bang POST rieng khi nguoi doc that su mo bai.
 */
export default function ChiTietTinTuc() {
  const { id } = useParams()
  const { duLieu, loi, taiLai } = useApi('/api/tien-ich/tin-tuc/' + id, { coToken: false })
  const daDem = useRef(null)

  useEffect(() => {
    // Moi bai chi dem MOT lan trong lan xem trang nay (React StrictMode goi effect 2 lan khi dev)
    if (!duLieu || daDem.current === id) return
    daDem.current = id
    goiApi('/api/tien-ich/tin-tuc/' + id + '/luot-xem', { method: 'POST', coToken: false }).catch(() => {
      /* dem luot xem hong thi van doc duoc bai — khong lam phien nguoi doc */
    })
  }, [duLieu, id])

  if (loi) {
    return (
      <>
        <DauTrang icon="bi-newspaper" tieuDe="Tin tức" mucHienTai="Không tìm thấy" />
        <div className="container pb-5">
          <BaoLoi loi={loi} thuLai={taiLai} />
          <Link className="btn btn-primary btn-sm" to="/news">
            <i className="bi bi-arrow-left me-1" />
            Về danh sách tin
          </Link>
        </div>
      </>
    )
  }

  if (!duLieu) {
    return (
      <div className="container py-5">
        <DangTai chu="Đang mở bài viết…" />
      </div>
    )
  }

  const t = duLieu

  return (
    <>
      <DauTrang icon="bi-newspaper" tieuDe={t.tieuDe} cha={{ den: '/news', ten: 'Tin tức' }} mucHienTai="Chi tiết" />
      <div className="container pb-5">
        <div className="row g-4">
          <div className="col-lg-8">
            <article className="card sp-the">
              <div className="card-body">
                <div className="small text-body-secondary mb-3">
                  <i className="bi bi-calendar3 me-1" />
                  {dinhDangNgayGio(t.ngayDang)}
                  <span className="mx-2">·</span>
                  <i className="bi bi-eye me-1" />
                  {dinhDangSo(t.luotXem)} lượt xem
                  {t.noiBat && <span className="badge text-bg-warning ms-2">Nổi bật</span>}
                </div>
                <AnhTin src={t.anh} alt={t.tieuDe} lop="ti-anh-bai mb-4" />
                {t.tomTat && <p className="lead fs-6 fw-semibold">{t.tomTat}</p>}
                {/* HTML do admin soan — da loc the an toan truoc khi chen vao DOM */}
                <NoiDungBaiViet html={t.noiDungHtml} />
              </div>
            </article>
          </div>

          <div className="col-lg-4">
            <div className="card sp-the">
              <div className="card-header">
                <i className="bi bi-collection me-2" />
                Tin khác
              </div>
              <div className="card-body">
                {(t.tinKhac || []).length === 0 ? (
                  <div className="text-body-secondary small">Chưa có tin nào khác.</div>
                ) : (
                  t.tinKhac.map((k) => (
                    <div className="d-flex gap-3 mb-3 pb-3 border-bottom" key={k.maTin}>
                      <AnhTin src={k.anh} alt={k.tieuDe} lop="ti-anh-nho" />
                      <div>
                        <Link className="fw-semibold d-block small lh-base text-decoration-none text-body" to={'/news/details/' + k.maTin}>
                          {k.tieuDe}
                        </Link>
                        <small className="text-body-secondary">{dinhDangNgay(k.ngayDang)}</small>
                      </div>
                    </div>
                  ))
                )}
                <Link className="btn btn-outline-primary btn-sm w-100" to="/news">
                  Xem tất cả tin
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
