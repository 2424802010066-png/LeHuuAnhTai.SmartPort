import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../api/client'
import { useSuKien } from '../realtime/SuKienContext'

export const MAU_MODULE = { 'Cổng': 'text-bg-primary', 'Bãi': 'sp-badge-dock', 'Tài chính': 'text-bg-warning' }

/**
 * Bang tin su kien cua moi module (Cong → Bai → Tai chinh). Dong moi hien ngay khi co su kien,
 * du luot xe ghi tu may khac hay tu ban MVC — khong can F5.
 */
export default function DongSuKien({ gioiHan = 8 }) {
  const { ganDay, trangThai } = useSuKien()
  const [boPhat, setBoPhat] = useState(null)
  const ds = ganDay.slice(0, gioiHan)

  // Tinh trang bo phat su kien: bao nhieu su kien dang cho / loi vinh vien
  useEffect(() => {
    let conGan = true
    const doc = () =>
      goiApi('/api/su-kien/trang-thai')
        .then((t) => conGan && setBoPhat(t))
        .catch(() => conGan && setBoPhat(null))
    doc()
    const hen = setInterval(doc, 15000)
    return () => {
      conGan = false
      clearInterval(hen)
    }
  }, [])

  return (
    <div className="card sp-the h-100">
      <div className="card-header d-flex align-items-center flex-wrap gap-1">
        <i className="bi bi-broadcast me-2 text-danger" />
        <span className="flex-grow-1">Sự kiện trực tiếp</span>
        <span className="badge text-bg-light fw-normal">{trangThai === 'da-noi' ? 'đang lắng nghe' : 'chưa nối'}</span>
      </div>
      {boPhat && (
        <div className="px-3 py-1 small text-muted border-bottom" title="Bộ phát sự kiện đọc Outbox và gọi các module">
          Bộ phát: {boPhat.dangCho} chờ · {boPhat.loi} lỗi · {boPhat.xong24Gio} đã xử lý 24h
        </div>
      )}
      <ul className="list-group list-group-flush sp-dong-su-kien">
        {ds.length === 0 && (
          <li className="list-group-item text-muted small py-4 text-center">
            Chưa có sự kiện. Ghi một lượt xe ở <Link to="/admin/gate">Cổng thông minh</Link> (máy nào cũng được) — các module xử lý và báo về đây.
          </li>
        )}
        {ds.map((s) => (
          <li key={s.id ?? s.ma} className="list-group-item sp-su-kien-moi">
            <div className="d-flex align-items-center gap-2">
              <span className={'badge ' + (MAU_MODULE[s.module] || 'text-bg-secondary')}>{s.module}</span>
              <span className="small text-muted ms-auto text-nowrap">{new Date(s.luc).toLocaleTimeString('vi-VN')}</span>
            </div>
            <div className="small mt-1">{s.moTa}</div>
          </li>
        ))}
      </ul>
    </div>
  )
}
