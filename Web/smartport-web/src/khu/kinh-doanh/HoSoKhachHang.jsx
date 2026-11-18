import { Link, useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien, tienGon } from '../../utils/dinhDang'
import { API_KHACH, duongChiTietDon } from './chung'
import './kinh-doanh.css'

/** Ho so khach hang: cong no, booking, container dang gui — mo tu danh sach khach hang. */
export default function HoSoKhachHang() {
  const { id } = useParams()
  const { duLieu: k, loi, dangTai, taiLai } = useApi(`${API_KHACH}/${id}`)

  if (!k) {
    return (
      <>
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && <DangTai chu="Đang tải hồ sơ khách hàng…" />}
      </>
    )
  }

  const COT = [
    {
      khoa: 'maSo',
      ten: 'Số booking',
      hien: (b) => (
        <Link className="kd-ma-don" to={duongChiTietDon(b.maBooking)}>
          {b.maSoBooking}
        </Link>
      ),
    },
    { khoa: 'ngayTao', ten: 'Ngày tạo', lop: 'small text-nowrap', hien: (b) => dinhDangNgayGio(b.ngayTao) },
    { khoa: 'ngayLam', ten: 'Cần thực hiện', an: 'md', lop: 'small text-nowrap', hien: (b) => dinhDangNgay(b.ngayCanThucHien) },
    { khoa: 'tien', ten: 'Tổng tiền', canPhai: true, lop: 'fw-semibold text-nowrap', hien: (b) => dinhDangTien(b.tongTien) },
    { khoa: 'trangThai', ten: 'Trạng thái', canPhai: true, hien: (b) => <NhanTrangThai giaTri={b.trangThai} /> },
  ]

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-receipt-cutoff" mau="xanh" so={dinhDangSo(k.soBooking)} nhan={`Booking · ${k.bookingDangMo} đang mở`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-cash-stack" mau="la" so={tienGon(k.giaTriBooking)} nhan="Giá trị booking (đ)" />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-exclamation-circle" mau={k.choThanhToan > 0 ? 'do' : 'xanh-ngoc'} so={tienGon(k.choThanhToan)} nhan="Chờ thanh toán (đ)" />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-box-seam" mau="cam" so={dinhDangSo(k.soContainer)} nhan={`Container · ${k.contTrongBai} đang trong bãi`} />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung
            tieuDe={k.tenCongTy}
            icon="bi-buildings"
            phai={
              <Link className="btn btn-sm btn-outline-primary" to={'/admin/customers/form/' + k.maKH}>
                <i className="bi bi-pencil me-1" />
                Sửa
              </Link>
            }
          >
            <dl className="row mb-0 small">
              <dt className="col-5 text-body-secondary fw-normal">Mã số thuế</dt>
              <dd className="col-7">{k.maSoThue || '—'}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Địa chỉ</dt>
              <dd className="col-7">{k.diaChi || '—'}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Người liên hệ</dt>
              <dd className="col-7">{k.nguoiLienHe || '—'}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Điện thoại</dt>
              <dd className="col-7">{k.soDienThoai || '—'}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Email</dt>
              <dd className="col-7">{k.email || '—'}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Khách từ ngày</dt>
              <dd className="col-7">{dinhDangNgay(k.ngayTao)}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Đã thanh toán</dt>
              <dd className="col-7 fw-semibold">{dinhDangTien(k.daThanhToan)}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Phí lưu bãi tạm tính</dt>
              <dd className="col-7 fw-semibold">{dinhDangTien(k.phiLuuBaiTamTinh)}</dd>
              <dt className="col-5 text-body-secondary fw-normal">Tài khoản ePort</dt>
              <dd className="col-7">{k.soTaiKhoan}</dd>
            </dl>
            <Link className="btn btn-sm btn-outline-secondary mt-3" to="/admin/customers">
              <i className="bi bi-arrow-left me-1" />
              Danh sách khách hàng
            </Link>
          </TheNoiDung>
        </div>
        <div className="col-lg-7">
          <TheNoiDung tieuDe="Booking gần đây" icon="bi-clock-history" khongDem>
            <BangDuLieu cot={COT} dong={k.bookingGanDay} khoaDong="maBooking" rong="Khách hàng chưa có booking nào." />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
