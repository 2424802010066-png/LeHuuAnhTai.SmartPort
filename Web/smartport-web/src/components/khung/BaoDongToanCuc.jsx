import { useState } from 'react'
import { Link } from 'react-router-dom'
import { chuongBao } from '../../khu/an-ninh/anNinhAi'
import { useNgheSuKien } from '../../realtime/SuKienContext'

/**
 * BAO DONG TOAN CUC: nghe su kien "an-ninh.su-co-da-bao" (dich vu An ninh phat qua Outbox -> RabbitMQ -> dich vu Thong bao
 * -> SSE). Su co NGUY HIEM (sap va cham, nguoi vao vung cam, nga...) hien bang do + chuong o MOI trang dang mo,
 * khong phai dang dung o man hinh giam sat. Tu an sau 15 giay.
 * QT14: su co CHUNG (bang SuCo, "an-ninh.su-co-chung-da-bao") muc NguyHiem cung bao dong o day — ai bao (khach, nhan vien,
 * camera AI) cung vay — va dan toi man hinh Quan ly su co de tiep nhan trong 5 phut. Muc khac khong bat bao dong.
 */
export default function BaoDongToanCuc() {
  const [ds, setDs] = useState([])

  function them(muc) {
    setDs((cu) => [muc, ...cu.filter((x) => x.ma !== muc.ma)].slice(0, 3))
    if (muc.nguy) chuongBao()
    setTimeout(() => setDs((cu) => cu.filter((x) => x.ma !== muc.ma)), 15000)
  }

  useNgheSuKien('an-ninh.su-co-da-bao', (sk) => {
    them({ ma: sk.id ?? Date.now(), nguy: sk.mucDo === 'Nguy hiểm', moTa: sk.moTa, den: '/admin/security', nhanLink: 'Mở phòng an ninh' })
  })

  useNgheSuKien('an-ninh.su-co-chung-da-bao', (sk) => {
    if (sk.mucDo !== 'NguyHiem') return
    them({ ma: sk.id ?? Date.now(), nguy: true, moTa: sk.moTa, den: '/admin/su-co?id=' + sk.maSuCo, nhanLink: 'Mở sự cố để tiếp nhận (≤ 5 phút)' })
  })

  if (!ds.length) return null
  return (
    <div className="sp-bao-dong" role="alert" aria-live="assertive">
      {ds.map((x) => (
        <div key={x.ma} className={'sp-bao-dong-the ' + (x.nguy ? 'nguy' : 'chu-y')}>
          <i className={'bi me-2 ' + (x.nguy ? 'bi-exclamation-octagon-fill' : 'bi-exclamation-triangle-fill')} />
          <div className="flex-fill small">
            <div className="fw-semibold">{x.moTa}</div>
            <Link to={x.den} className="link-light small">
              {x.nhanLink}
            </Link>
          </div>
          <button type="button" className="btn-close btn-close-white ms-2" aria-label="Đóng cảnh báo" onClick={() => setDs((cu) => cu.filter((y) => y.ma !== x.ma))} />
        </div>
      ))}
    </div>
  )
}
