import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { DauTrang } from './chung'

/** Ban MVC tu tai lai ca trang sau 30 giay; o day chi goi lai API (nhe hon, khong nhay man hinh) */
const NHIP_LAM_MOI = 30000

const COT = [
  { khoa: 'bienSo', ten: 'Biển số', hien: (x) => <span className="fw-semibold font-monospace">{x.bienSo}</span> },
  {
    khoa: 'huong',
    ten: 'Hướng',
    hien: (x) => <span className={'badge ' + (x.huongDi === 'Vào' ? 'text-bg-success' : 'text-bg-warning')}>{x.huongDi}</span>,
  },
  { khoa: 'cong', ten: 'Cổng', hien: (x) => x.cong || '—' },
  { khoa: 'loaiXe', ten: 'Loại xe', an: 'md', lop: 'small text-body-secondary', hien: (x) => x.loaiXe || '—' },
  { khoa: 'luc', ten: 'Thời gian', canPhai: true, lop: 'small', hien: (x) => gioNgayNgan(x.thoiGian) },
]

const LOP_MUC = { 'Thông thoáng': 'thong-thoang', 'Bình thường': 'binh-thuong', Đông: 'dong', 'Ùn tắc': 'un-tac' }
const hai = (n) => String(n).padStart(2, '0')

/**
 * AI du bao muc dong cua cong trong 12 gio toi (GET /api/ai-van-hanh/gio-nen-den, khong can dang nhap):
 * nha xe chon gio vang de cho xe chay den — cang bot un tac, tai xe bot cho.
 */
function DuBaoGioToi({ gio }) {
  if (!gio?.length) return null
  // Goi y trong gio lam viec thong thuong (6h–21h) neu co, tranh khuyen tai xe den luc 3 gio sang
  const ungVien = gio.filter((o) => {
    const h = new Date(o.luc).getHours()
    return h >= 6 && h <= 21
  })
  const nenDen = (ungVien.length ? ungVien : gio).reduce((m, o) => (!m || o.soXeDuKien < m.soXeDuKien - 1e-9 ? o : m), null)
  const gioNenDen = new Date(nenDen.luc)

  return (
    <div className="card sp-the mb-3">
      <div className="card-header d-flex flex-wrap align-items-center gap-2">
        <i className="bi bi-stars text-primary" />
        <span className="flex-grow-1">Dự báo {gio.length} giờ tới — nên đến lúc nào?</span>
        <small className="text-body-secondary">AI học từ lượt xe 90 ngày qua + lệnh hẹn đã đặt</small>
      </div>
      <div className="card-body">
        <div className="table-responsive">
          <div className="ti-du-bao-gio">
            {gio.map((o) => {
              const t = new Date(o.luc)
              return (
                <div
                  key={o.luc}
                  className={'ti-o-du-bao ' + (LOP_MUC[o.mucDo] || 'binh-thuong') + (o === nenDen ? ' nen-den' : '')}
                  title={`${hai(t.getHours())}:00 · ${o.mucDo} · ~${dinhDangSo(o.soXeDuKien, 1)} xe/giờ · ${o.soLanCan} làn`}
                >
                  <b>{hai(t.getHours())}:00</b>
                  {o.mucDo}
                </div>
              )
            })}
          </div>
        </div>
        <div className="small mt-2">
          <i className="bi bi-lightbulb me-1 text-warning" />
          Nên đến khoảng <b>{hai(gioNenDen.getHours())}:00</b> ({hai(gioNenDen.getDate())}/{hai(gioNenDen.getMonth() + 1)}) — dự kiến{' '}
          {nenDen.mucDo.toLowerCase()}, {nenDen.soXeDuKien < 1 ? 'dưới 1 xe/giờ' : 'khoảng ' + dinhDangSo(nenDen.soXeDuKien, 1) + ' xe/giờ'}.
        </div>
      </div>
    </div>
  )
}

function TheSo({ so, nhan, mau }) {
  return (
    <div className="col-6 col-md-3">
      <div className="card sp-the h-100 text-center">
        <div className="card-body py-3">
          <div className={'ti-so-lon text-' + mau}>{dinhDangSo(so)}</div>
          <div className="small text-body-secondary">{nhan}</div>
        </div>
      </div>
    </div>
  )
}

/**
 * Bang tinh trang cong cang cong khai (thay Views/Cong/Index.cshtml) — dia chi /cong.
 * Nha xe xem mat do truoc khi cho xe chay den cang -> bot un tac o cong.
 */
export default function TinhTrangCong() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tien-ich/tinh-trang-cong', { coToken: false })
  const duBao = useApi('/api/ai-van-hanh/gio-nen-den?soGio=12', { coToken: false })
  const taiLaiDuBao = duBao.taiLai

  useEffect(() => {
    const hen = setInterval(() => {
      taiLai({ im: true })
      taiLaiDuBao({ im: true })
    }, NHIP_LAM_MOI)
    return () => clearInterval(hen)
  }, [taiLai, taiLaiDuBao])

  return (
    <>
      <DauTrang icon="bi-stoplights" tieuDe="Tình trạng cổng cảng" mucHienTai="Tình trạng cổng" phu="Tự làm mới mỗi 30 giây — xem trước khi cho xe chạy đến cảng." />
      <div className="container pb-5">
        <BaoLoi loi={loi} thuLai={taiLai} />

        {duLieu && (
          <>
            <div className={'alert alert-' + duLieu.mucMatDo + ' text-center py-3'}>
              <div className="fs-4 fw-bold mb-1">Cổng hiện tại: {duLieu.matDo}</div>
              <div>
                {dinhDangSo(duLieu.luotGioQua)} lượt xe trong 60 phút qua · xử lý trung bình ~{duLieu.giayXuLy} giây/xe
              </div>
            </div>

            <div className="row g-3 mb-3">
              <TheSo so={duLieu.vaoHomNay} nhan="Lượt VÀO hôm nay" mau="success" />
              <TheSo so={duLieu.raHomNay} nhan="Lượt RA hôm nay" mau="warning-emphasis" />
              <TheSo so={duLieu.trongCang} nhan="Xe đang trong cảng" mau="primary" />
              <TheSo so={duLieu.soLenhChoXe} nhan="Lệnh đang chờ xe đến" mau="danger" />
            </div>
          </>
        )}

        <DuBaoGioToi gio={duBao.duLieu} />

        <div className="card sp-the">
          <div className="card-header d-flex align-items-center gap-2">
            <i className="bi bi-clock-history text-primary" />
            <span className="flex-grow-1">10 lượt xe gần nhất</span>
            {duLieu && <small className="text-body-secondary">Lúc {dinhDangNgayGio(duLieu.capNhatLuc)}</small>}
          </div>
          <BangDuLieu cot={COT} dong={duLieu?.ganNhat} khoaDong={(x, i) => x.bienSo + i} dangTai={dangTai} rong="Chưa có lượt xe nào hôm nay." />
        </div>

        <div className="alert alert-light border mt-3 d-flex flex-wrap align-items-center gap-2">
          <span className="flex-grow-1 small">
            <i className="bi bi-lightbulb me-1" />
            Mẹo nhà xe: <b>check-in online</b> trước khi chạy — cổng ưu tiên xử lý, đỡ xếp hàng.
          </span>
          <Link className="btn btn-sm btn-primary" to="/taixe">
            <i className="bi bi-truck me-1" />
            Cổng tài xế
          </Link>
        </div>
      </div>
    </>
  )
}
