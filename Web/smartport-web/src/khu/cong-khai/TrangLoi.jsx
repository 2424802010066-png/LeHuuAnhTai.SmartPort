import { Link } from 'react-router-dom'

/** Trang bao su co than thien (thay Views/Home/Loi.cshtml — customErrors cua ban MVC tro ve day). */
export default function TrangLoi() {
  return (
    <div className="container py-5 text-center" style={{ maxWidth: 640 }}>
      <div style={{ fontSize: '5rem', lineHeight: 1 }}>⚓</div>
      <div className="fw-bold" style={{ fontSize: '3.6rem', letterSpacing: 4, color: '#b45309' }}>
        500
      </div>
      <h1 className="h5 fw-bold mb-2">Hệ thống gặp sự cố ngoài ý muốn</h1>
      <p className="text-body-secondary mb-4">
        Sự cố đã được <b>tự động ghi vào nhật ký</b> để đội kỹ thuật xử lý. Bạn thử tải lại trang hoặc quay về trang chủ — thao tác của bạn không mất
        dữ liệu quan trọng.
      </p>
      <div className="d-flex gap-2 justify-content-center flex-wrap">
        <button type="button" className="btn btn-primary px-4" onClick={() => window.location.reload()}>
          <i className="bi bi-arrow-clockwise me-1" />
          Tải lại trang
        </button>
        <Link className="btn btn-outline-primary px-4" to="/">
          <i className="bi bi-house-door me-1" />
          Về trang chủ
        </Link>
        <Link className="btn btn-outline-secondary px-4" to="/home/contact">
          <i className="bi bi-chat-left-text me-1" />
          Báo cho chúng tôi
        </Link>
      </div>
    </div>
  )
}
