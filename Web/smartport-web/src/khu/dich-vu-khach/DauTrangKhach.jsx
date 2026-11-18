import { Link } from 'react-router-dom'
import './dich-vu-khach.css'

/**
 * Dau trang cua cac man hinh khach hang (khung website cong khai khong co thanh tieu de
 * nhu khu dieu hanh, nen moi trang tu ve mot dai tieu de + duong dan dieu huong).
 *
 *   <DauTrangKhach icon="bi-receipt" tieuDe="Booking của tôi" duongDan={[{ ten: 'Booking của tôi' }]} />
 */
export default function DauTrangKhach({ icon, tieuDe, duongDan = [], phai }) {
  return (
    <div className="dvk-dau">
      <div className="container d-flex flex-wrap align-items-center gap-2">
        <div className="flex-grow-1 min-w-0">
          <h1>
            {icon && <i className={'bi ' + icon + ' me-2'} />}
            {tieuDe}
          </h1>
          {duongDan.length > 0 && (
            <nav aria-label="Đường dẫn">
              <ol className="breadcrumb">
                {duongDan.map((m, i) => (
                  <li key={m.ten + i} className={'breadcrumb-item' + (m.den ? '' : ' active')} aria-current={m.den ? undefined : 'page'}>
                    {m.den ? <Link to={m.den}>{m.ten}</Link> : m.ten}
                  </li>
                ))}
              </ol>
            </nav>
          )}
        </div>
        {phai}
      </div>
    </div>
  )
}
