import { Suspense } from 'react'
import { Outlet, useNavigate } from 'react-router-dom'
import DangTai from './DangTai'

/**
 * Khung cho trang IN (lenh giao nhan, phieu can, hoa don, EOR...): nen trang, kho A4,
 * thanh nut "In / Quay lai" an khi in that (@media print trong styles.css).
 */
export default function BoCucIn() {
  const dieuHuong = useNavigate()
  return (
    <div className="sp-trang-in-nen">
      <div className="sp-thanh-in d-print-none">
        <button type="button" className="btn btn-sm btn-light" onClick={() => (window.history.length > 1 ? dieuHuong(-1) : window.close())}>
          <i className="bi bi-arrow-left me-1" />
          Quay lại
        </button>
        <button type="button" className="btn btn-sm btn-primary" onClick={() => window.print()}>
          <i className="bi bi-printer me-1" />
          In
        </button>
      </div>
      <div className="sp-trang-in">
        <Suspense fallback={<DangTai chu="Đang chuẩn bị bản in…" />}>
          <Outlet />
        </Suspense>
      </div>
    </div>
  )
}
