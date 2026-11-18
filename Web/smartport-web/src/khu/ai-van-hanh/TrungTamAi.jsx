import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgayGio } from '../../utils/dinhDang'
import { ThanhCongCu } from './chung'
import './ai-van-hanh.css'

const TEN_MUC = { ok: 'Ổn định', warn: 'Cần chú ý', danger: 'Xử lý ngay' }

// Bai toan that moi mo-dun giai — noi bang ngon ngu nguoi van hanh (va de trinh bay khi van dap)
const BAI_TOAN = {
  bai: 'Mỗi lần đảo container mất 3–5 phút xe nâng. Biết trước container nào rời sớm thì xếp lên trên — lúc rút không phải nhấc container khác.',
  cong: 'Giờ cao điểm xe tải xếp hàng dài ở cổng: tài xế mất giờ, đường ùn tắc. Biết trước giờ đông để mở đủ làn và hẹn tài xế vào giờ vắng.',
  'cont-lanh': 'Một container hàng đông lạnh hỏng có thể mất hàng tỉ đồng. Ngưỡng cố định vừa báo muộn (lệch rồi mới báo) vừa báo giả (mở cửa kiểm hàng).',
  'gian-lan': 'Xe ra không có lượt vào, biển số nhập tay, ra vào giờ lạ… là dấu hiệu lọt hàng hoặc sai sót. Không thể soát tay hàng nghìn lượt mỗi tháng.',
}

const NGUYEN_TAC = [
  {
    icon: 'bi-database-check',
    ten: 'Học từ dữ liệu của chính cảng',
    mo: 'Lượt xe, ngày vào/ra bãi, số đo nhiệt đã có sẵn trong hệ thống — không cần thu thập thêm, không gọi dịch vụ ngoài.',
  },
  {
    icon: 'bi-clipboard-data',
    ten: 'Có kiểm định, so với cách làm cũ',
    mo: 'Mỗi mô hình đo sai số trên dữ liệu nó chưa thấy và so với cách làm cũ (thống kê, ngưỡng cố định) — chưa thắng thì hệ thống không dùng.',
  },
  {
    icon: 'bi-chat-square-text',
    ten: 'Giải thích được từng kết luận',
    mo: 'Mọi dự báo, cảnh báo đều kèm căn cứ bằng lời — người điều độ kiểm tra lại được trước khi làm theo.',
  },
]

/** The mot mo-dun AI: so lon + cac dong tom tat + bai toan + thuat toan; bam vao mo trang chi tiet */
function TheMoDun({ t }) {
  const muc = t.muc || 'ok'
  return (
    <Link to={t.url} className={'card sp-the p-3 av-the ' + muc}>
      <div className="d-flex align-items-center gap-2 mb-2">
        <span className="av-the-icon">
          <i className={'bi ' + t.icon} />
        </span>
        <div className="fw-bold flex-grow-1 lh-sm">{t.ten}</div>
        <span className="av-muc">{TEN_MUC[muc] || muc}</span>
      </div>

      {t.loi ? (
        <div className="text-danger small mb-2">
          <i className="bi bi-exclamation-triangle me-1" />
          Mô-đun lỗi: {t.loi}
        </div>
      ) : (
        <>
          <div className="d-flex align-items-baseline gap-2 mb-2">
            <span className="av-the-so">{t.soLon ?? '—'}</span>
            <span className="small text-body-secondary">{t.nhanSoLon}</span>
          </div>
          <ul className="av-the-dong small mb-2">
            {(t.dong || []).map((d) => (
              <li key={d}>{d}</li>
            ))}
          </ul>
        </>
      )}

      <div className="av-bai-toan small text-body-secondary">
        <div className="mb-1">{BAI_TOAN[t.ma]}</div>
        <div className="d-flex align-items-center gap-2">
          <span className="av-thuat-toan">{t.thuatToan}</span>
          <i className="bi bi-chevron-right ms-auto" />
        </div>
      </div>
    </Link>
  )
}

/**
 * TRUNG TAM AI VAN HANH — 4 mo-dun hoc may cho bai toan that cua cang can / depot.
 * So lieu tu GET /api/ai-van-hanh/tong-quan (tinh lai moi lan mo, khong ghi CSDL).
 */
export default function TrungTamAi() {
  // Bang giam sat lanh (/api/container-lanh) "bat kip" so doc cam bien IoT toi hien tai — goi truoc de the container lanh
  // phan tich tren so do moi nhat (giong trang AI container lanh), khong bao nham "mat tin hieu"
  const [sanSang, setSanSang] = useState(false)
  useEffect(() => {
    let huy = false
    goiApi('/api/container-lanh')
      .catch(() => {})
      .finally(() => {
        if (!huy) setSanSang(true)
      })
    return () => {
      huy = true
    }
  }, [])
  const { duLieu: d, loi, dangTai, taiLai } = useApi(sanSang ? '/api/ai-van-hanh/tong-quan' : null)

  if (!sanSang || (dangTai && !d)) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const lamMoi = async () => {
    await goiApi('/api/container-lanh').catch(() => {})
    taiLai()
  }
  const canXuLy = d.the.filter((t) => t.muc === 'danger').length
  const canChuY = d.the.filter((t) => t.muc === 'warn').length

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <ThanhCongCu luc={dinhDangNgayGio(d.lucTinh)} dangTai={dangTai} lamMoi={lamMoi} />

      <div className={'alert d-flex align-items-center gap-2 py-2 ' + (canXuLy ? 'alert-danger' : canChuY ? 'alert-warning' : 'alert-success')}>
        <i className={'bi fs-5 ' + (canXuLy ? 'bi-exclamation-octagon' : canChuY ? 'bi-exclamation-triangle' : 'bi-check-circle')} />
        <div className="small">
          {canXuLy
            ? `${canXuLy} mô-đun báo việc cần xử lý ngay — bấm vào thẻ màu đỏ để xem chi tiết.`
            : canChuY
              ? `${canChuY} mô-đun có việc nên làm sớm — AI đã sắp xếp theo mức ưu tiên trong từng trang.`
              : 'Mọi chỉ số vận hành trong ngưỡng an toàn.'}
        </div>
      </div>

      <div className="row row-cols-1 row-cols-xl-2 g-3 mb-4">
        {d.the.map((t) => (
          <div className="col" key={t.ma}>
            <TheMoDun t={t} />
          </div>
        ))}
      </div>

      <div className="card sp-the p-3">
        <div className="fw-bold mb-3 av-chu-tim">
          <i className="bi bi-patch-check me-1" />
          Vì sao là AI dùng được ngay, không phải trình diễn
        </div>
        <div className="row g-3">
          {NGUYEN_TAC.map((n) => (
            <div className="col-md-4" key={n.ten}>
              <div className="av-nguyen-tac">
                <i className={'bi ' + n.icon} />
                <div>
                  <div className="fw-semibold">{n.ten}</div>
                  <div className="small text-body-secondary">{n.mo}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </>
  )
}
