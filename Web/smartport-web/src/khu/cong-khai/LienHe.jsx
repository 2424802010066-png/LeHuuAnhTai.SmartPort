import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import Truong from '../../components/ui/Truong'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useHienDan } from './hieuUng'

const TRONG = { hoTen: '', email: '', soDienThoai: '', chuDe: '', noiDung: '' }

/** Trang lien he (thay Views/Home/Contact.cshtml — GET hien form, POST luu vao hop thu lien he). */
export default function LienHe() {
  const { duLieu: cang } = useApi('/api/cong-khai/thong-tin-lien-he', { coToken: false })
  const { gt, doi, datLai } = useBieuMau(TRONG)
  const { chay, dangChay } = useThaoTac()
  useHienDan([cang])

  const gui = async (e) => {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/cong-khai/lien-he', { method: 'POST', body: { ...gt, email: gt.email?.trim() || null }, coToken: false }))
    if (kq) datLai(TRONG)
  }

  const zalo = cang?.zaloPhone

  return (
    <>
      <div className="ck-dau-muc">
        <div className="container">
          <h1>
            <i className="bi bi-envelope-paper me-2" />
            Liên hệ với chúng tôi
          </h1>
          <nav aria-label="Đường dẫn">
            <ol className="breadcrumb">
              <li className="breadcrumb-item">
                <Link to="/">Trang chủ</Link>
              </li>
              <li className="breadcrumb-item active" aria-current="page">
                Liên hệ
              </li>
            </ol>
          </nav>
        </div>
      </div>

      <div className="container ck-muc">
        <div className="row g-4">
          <div className="col-lg-7">
            <div className="ck-the p-4 ck-hien">
              <h2 className="h6 fw-bold mb-3">Gửi yêu cầu hỗ trợ</h2>
              <form onSubmit={gui}>
                <div className="row g-3">
                  <Truong nhan="Họ tên" batBuoc lop="col-md-6 mb-0">
                    <input className="form-control" value={gt.hoTen} maxLength={100} onChange={doi('hoTen')} placeholder="Nguyễn Văn A" />
                  </Truong>
                  <Truong nhan="Email" lop="col-md-6 mb-0">
                    <input type="email" className="form-control" value={gt.email} maxLength={100} onChange={doi('email')} placeholder="email@congty.vn" />
                  </Truong>
                  <Truong nhan="Số điện thoại" lop="col-md-6 mb-0">
                    <input className="form-control" value={gt.soDienThoai} maxLength={20} onChange={doi('soDienThoai')} placeholder="09xx xxx xxx" />
                  </Truong>
                  <Truong nhan="Chủ đề" lop="col-md-6 mb-0">
                    <input className="form-control" value={gt.chuDe} maxLength={150} onChange={doi('chuDe')} placeholder="VD: Báo giá lưu bãi" />
                  </Truong>
                  <Truong nhan="Nội dung" batBuoc lop="col-12 mb-0" goiY="Tối đa 1000 ký tự.">
                    <textarea className="form-control" rows={5} value={gt.noiDung} maxLength={1000} onChange={doi('noiDung')} placeholder="Nội dung cần hỗ trợ..." />
                  </Truong>
                  <div className="col-12">
                    <button className="ck-nut-chinh px-4 py-2" type="submit" disabled={dangChay}>
                      <i className="bi bi-send me-1" />
                      {dangChay ? 'Đang gửi…' : 'Gửi liên hệ'}
                    </button>
                  </div>
                </div>
              </form>
            </div>
          </div>

          <div className="col-lg-5">
            <div className="ck-the p-4 mb-4 ck-hien">
              <h2 className="h6 fw-bold mb-3">
                <i className="bi bi-geo-alt me-2" />
                Thông tin liên hệ
              </h2>
              <p className="mb-2 small">
                <i className="bi bi-building me-2 text-primary" />
                {cang?.tenCang} — {cang?.diaChi}
              </p>
              <p className="mb-2 small">
                <i className="bi bi-telephone me-2 text-primary" />
                Hotline: <strong>{cang?.hotline}</strong> — Quản lý: {cang?.nguoiQuanLy}
              </p>
              <p className="mb-2 small">
                <i className="bi bi-envelope me-2 text-primary" />
                <a href={'mailto:' + (cang?.email ?? '')}>{cang?.email}</a>
              </p>
              <p className="mb-2 small">
                <i className="bi bi-clock me-2 text-primary" />
                Cổng cảng {cang?.gioCong} · Văn phòng {cang?.gioVanPhong}
              </p>
              {cang?.facebookCaNhan && (
                <p className="mb-2 small">
                  <i className="bi bi-facebook me-2 text-primary" />
                  <a href={cang.facebookCaNhan} target="_blank" rel="noopener noreferrer">
                    Facebook chủ dự án
                  </a>
                </p>
              )}
              {zalo && (
                <p className="mb-0 small">
                  <span className="me-2 fw-bold" style={{ color: '#0068ff' }}>
                    Z
                  </span>
                  Zalo:{' '}
                  <a href={'https://zalo.me/' + zalo} target="_blank" rel="noopener noreferrer">
                    {zalo}
                  </a>
                </p>
              )}
            </div>

            <div className="ck-cta mb-4 ck-hien">
              <h2 className="h6 fw-bold">
                <i className="bi bi-robot me-2" />
                Cần trả lời ngay?
              </h2>
              <p className="mb-3 small" style={{ color: '#c9dcef' }}>
                Hỏi PortBot — tra container, lịch tàu, bảng giá… phản hồi trong vài giây.
              </p>
              <button type="button" className="ck-nut-chinh px-3 py-2" onClick={() => window.dispatchEvent(new Event('smartport:portbot'))}>
                Mở khung chat PortBot
              </button>
            </div>

            <div className="ck-the p-4 ck-hien">
              <h2 className="h6 fw-bold mb-3">
                <i className="bi bi-people me-2" />
                Cộng đồng Logistics
              </h2>
              {cang?.facebookNhom && (
                <a className="d-flex align-items-center gap-3 mb-3 text-decoration-none" href={cang.facebookNhom} target="_blank" rel="noopener noreferrer">
                  <span className="ck-ico-tron text-primary">
                    <i className="bi bi-facebook" />
                  </span>
                  <span>
                    <span className="fw-semibold d-block">Cộng đồng Logistics &amp; XNK</span>
                    <small className="text-body-secondary">Group hỏi đáp nghiệp vụ</small>
                  </span>
                </a>
              )}
              {cang?.youtube && (
                <a className="d-flex align-items-center gap-3 text-decoration-none" href={cang.youtube} target="_blank" rel="noopener noreferrer">
                  <span className="ck-ico-tron text-danger">
                    <i className="bi bi-youtube" />
                  </span>
                  <span>
                    <span className="fw-semibold d-block">Vietnam Logistics Review</span>
                    <small className="text-body-secondary">Kênh video kiến thức logistics</small>
                  </span>
                </a>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
