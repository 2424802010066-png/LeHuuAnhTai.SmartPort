import { Link } from 'react-router-dom'

/**
 * Trang 404 cua website cong khai (thay Views/Home/KhongTimThay.cshtml).
 * Dang ky o /home/khongtimthay; nguoi dieu phoi gan them lam route "*" trong khung cong khai
 * cua App.jsx de dia chi la khong bi da ve trang chu mot cach im lang.
 */
export default function KhongTimThay() {
  return (
    <div className="container py-5 text-center" style={{ maxWidth: 640 }}>
      <div style={{ fontSize: '5rem', lineHeight: 1 }}>🧭</div>
      <div className="fw-bold" style={{ fontSize: '3.6rem', letterSpacing: 4, color: '#0d4f8b' }}>
        404
      </div>
      <h1 className="h5 fw-bold mb-2">Trang này đã “rời cảng” hoặc chưa từng tồn tại</h1>
      <p className="text-body-secondary mb-4">
        Đường dẫn bạn truy cập không đúng hoặc nội dung đã được di chuyển. Hãy quay về trang chủ hoặc dùng menu để tiếp tục nhé.
      </p>
      <div className="d-flex gap-2 justify-content-center flex-wrap">
        <Link className="btn btn-primary px-4" to="/">
          <i className="bi bi-house-door me-1" />
          Về trang chủ
        </Link>
        <Link className="btn btn-outline-primary px-4" to="/tracking">
          <i className="bi bi-box-seam me-1" />
          Tra cứu container
        </Link>
        <Link className="btn btn-outline-secondary px-4" to="/home/contact">
          <i className="bi bi-chat-left-text me-1" />
          Liên hệ hỗ trợ
        </Link>
      </div>
    </div>
  )
}
