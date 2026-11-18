import { useState } from 'react'
import { Link } from 'react-router-dom'
import { chuongBao } from '../../khu/an-ninh/anNinhAi'
import { useNgheSuKien } from '../../realtime/SuKienContext'

/**
 * BAO DONG TOAN CUC: nghe su kien "an-ninh.su-co-da-bao" (dich vu An ninh phat qua Outbox -> RabbitMQ -> dich vu Thong bao
 * -> SSE). Su co NGUY HIEM (sap va cham, nguoi vao vung cam, nga...) hien bang do + chuong o MOI trang dang mo,
 * khong phai dang dung o man hinh giam sat. Tu an sau 15 giay.
 */
export default function BaoDongToanCuc() {
  const [ds, setDs] = useState([])

  useNgheSuKien('an-ninh.su-co-da-bao', (sk) => {
    const nguy = sk.mucDo === 'Nguy hiểm'
    const muc = { ma: sk.id ?? Date.now(), nguy, moTa: sk.moTa, maSC: sk.maSC }
    setDs((cu) => [muc, ...cu.filter((x) => x.ma !== muc.ma)].slice(0, 3))
    if (nguy) chuongBao()
    setTimeout(() => setDs((cu) => cu.filter((x) => x.ma !== muc.ma)), 15000)
  })

  if (!ds.length) return null
  return (
    <div className="sp-bao-dong" role="alert" aria-live="assertive">
      {ds.map((x) => (
        <div key={x.ma} className={'sp-bao-dong-the ' + (x.nguy ? 'nguy' : 'chu-y')}>
          <i className={'bi me-2 ' + (x.nguy ? 'bi-exclamation-octagon-fill' : 'bi-exclamation-triangle-fill')} />
          <div className="flex-fill small">
            <div className="fw-semibold">{x.moTa}</div>
            <Link to="/admin/security" className="link-light small">
              Mở phòng an ninh
            </Link>
          </div>
          <button type="button" className="btn-close btn-close-white ms-2" aria-label="Đóng cảnh báo" onClick={() => setDs((cu) => cu.filter((y) => y.ma !== x.ma))} />
        </div>
      ))}
    </div>
  )
}
