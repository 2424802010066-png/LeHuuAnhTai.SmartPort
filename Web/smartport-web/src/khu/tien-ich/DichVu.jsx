import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { useAuth } from '../../auth/AuthContext'
import { BangDuLieu, BaoLoi, DangTai } from '../../components/ui'
import { dinhDangTien } from '../../utils/dinhDang'
import { DauTrang } from './chung'

/** Icon theo nhom dich vu — giu dung ban MVC de khach quen mat */
const ICON_NHOM = {
  'Bốc xếp': 'bi-boxes',
  'Lưu bãi': 'bi-stack',
  'Kho bãi': 'bi-archive',
  'Vận chuyển': 'bi-truck',
}
const iconNhom = (n) => ICON_NHOM[n] || 'bi-gear-wide-connected'

const COT = [
  { khoa: 'ten', ten: 'Dịch vụ', rong: '32%', hien: (d) => <span className="fw-semibold">{d.tenDV}</span> },
  { khoa: 'moTa', ten: 'Mô tả', lop: 'text-body-secondary small', hien: (d) => d.moTa || '—' },
  { khoa: 'dvt', ten: 'Đơn vị', rong: '12%', an: 'sm', hien: (d) => d.donViTinh || '—' },
  {
    khoa: 'gia',
    ten: 'Đơn giá',
    rong: '18%',
    canPhai: true,
    hien: (d) => <span className="fw-bold text-warning-emphasis">{dinhDangTien(d.donGia)}</span>,
  },
]

/** Dich vu & bang gia cong bo tren website (thay Views/Services/Index.cshtml) — dia chi /services. */
export default function DichVu() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tien-ich/dich-vu', { coToken: false })
  const { taiKhoan } = useAuth()
  const laKhach = taiKhoan?.vaiTro === 'KhachHang'

  return (
    <>
      <DauTrang icon="bi-tools" tieuDe="Dịch vụ & bảng giá" mucHienTai="Dịch vụ & bảng giá" />
      <div className="container pb-5">
        <BaoLoi loi={loi} thuLai={taiLai} />

        <div className="row g-3 mb-4">
          <div className="col-lg-8">
            <div className="card sp-the h-100">
              <div className="card-body d-flex flex-column">
                <h2 className="h5 fw-bold">Đặt dịch vụ cảng trực tuyến qua ePort</h2>
                <p className="text-body-secondary mb-3">Gửi yêu cầu online, hệ thống tự tính phí và cập nhật trạng thái theo thời gian thực.</p>
                <div className="d-flex flex-wrap gap-2 mt-auto">
                  {laKhach ? (
                    <Link className="btn btn-warning fw-semibold" to="/booking/create">
                      <i className="bi bi-plus-circle me-1" />
                      Đặt dịch vụ ngay
                    </Link>
                  ) : (
                    <>
                      <Link className="btn btn-warning fw-semibold" to="/account/login">
                        <i className="bi bi-person-circle me-1" />
                        Đăng nhập để đặt
                      </Link>
                      <Link className="btn btn-outline-primary" to="/account/register">
                        Đăng ký tài khoản
                      </Link>
                    </>
                  )}
                  <Link className="btn btn-outline-secondary" to="/cost">
                    <i className="bi bi-calculator me-1" />
                    Ước tính chi phí
                  </Link>
                </div>
              </div>
            </div>
          </div>
          <div className="col-lg-4">
            <div className="card sp-the h-100 text-white" style={{ background: 'linear-gradient(150deg,#0a2f57,#0d4f8b)', borderColor: 'transparent' }}>
              <div className="card-body">
                <h3 className="h6 fw-bold">
                  <i className="bi bi-percent me-2" />
                  Ưu đãi
                </h3>
                <ul className="small mb-0 ps-3" style={{ color: '#c9dcef' }}>
                  <li>
                    Miễn phí lưu bãi <strong>{duLieu?.ngayMienPhiLuuBai ?? 5} ngày đầu</strong> với mọi container
                  </li>
                  <li>Giảm 10% phí bốc xếp cho booking từ 50 cont/tháng</li>
                  <li>Miễn phí cân VGM cho 100 booking đầu qua ePort</li>
                </ul>
              </div>
            </div>
          </div>
        </div>

        {dangTai && !duLieu ? (
          <DangTai chu="Đang tải bảng giá…" />
        ) : !duLieu?.nhom?.length ? (
          <div className="text-center text-body-secondary py-5">
            <i className="bi bi-inbox fs-2 d-block mb-2" />
            Cảng chưa công bố bảng giá dịch vụ nào.
          </div>
        ) : (
          duLieu.nhom.map((g) => (
            <div className="card sp-the mb-3" key={g.nhom}>
              <div className="card-header d-flex align-items-center gap-2">
                <i className={'bi ' + iconNhom(g.nhom) + ' text-primary'} />
                <span className="fw-bold">{g.nhom}</span>
                <span className="badge text-bg-light border ms-auto">{g.dichVu.length} dịch vụ</span>
              </div>
              <BangDuLieu cot={COT} dong={g.dichVu} khoaDong="maDV" rong="Nhóm này chưa có dịch vụ." />
            </div>
          ))
        )}

        <div className="text-body-secondary small mt-3">
          <i className="bi bi-info-circle me-1" />
          Đơn giá chưa gồm VAT. Bảng giá áp dụng từ {duLieu ? new Date(duLieu.apDungTu).toLocaleDateString('vi-VN') : '01/01'}, có thể thay đổi theo thông báo của cảng.
        </div>
      </div>
    </>
  )
}
