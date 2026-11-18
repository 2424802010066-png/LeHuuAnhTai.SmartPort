import { Link } from 'react-router-dom'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'
import './dich-vu-khach.css'

const TRANG_THAI = ['Chờ thanh toán', 'Đã thanh toán', 'Quá hạn', 'Đã hủy']

/** /payment/history — lich su thanh toan cua khach (thay Views/Payment/History.cshtml). */
export default function LichSuThanhToan() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/thanh-toan-khach/lich-su' + taoQuery(loc))

  const COT = [
    { khoa: 'ma', ten: 'Mã GD', hien: (t) => <span className="fw-semibold">#{t.maTT}</span> },
    {
      khoa: 'bk',
      ten: 'Booking',
      hien: (t) => (
        <Link className="text-decoration-none" to={'/booking/details/' + t.maBooking}>
          {t.maSoBooking || '—'}
        </Link>
      ),
    },
    { khoa: 'pt', ten: 'Phương thức', an: 'md', hien: (t) => t.tenPhuongThuc },
    { khoa: 'tien', ten: 'Số tiền', canPhai: true, hien: (t) => <span className="fw-semibold">{dinhDangTien(t.soTien)}</span> },
    { khoa: 'ngay', ten: 'Ngày tạo', an: 'lg', hien: (t) => dinhDangNgayGio(t.ngayTao) },
    { khoa: 'tt', ten: 'Trạng thái', hien: (t) => <NhanTrangThai giaTri={t.trangThai} mau={t.trangThai === 'Quá hạn' ? 'do' : undefined} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 130,
      hien: (t) =>
        t.trangThai === 'Đã thanh toán' ? (
          <Link className="btn btn-sm btn-outline-success" to={'/payment/receipt/' + t.maTT}>
            <i className="bi bi-receipt me-1" />
            Biên lai
          </Link>
        ) : t.trangThai === 'Chờ thanh toán' ? (
          <Link className="btn btn-sm btn-primary" to={'/payment/pay/' + t.maTT}>
            <i className="bi bi-credit-card me-1" />
            Thanh toán
          </Link>
        ) : (
          <Link className="btn btn-sm btn-outline-secondary" to={'/payment?id=' + t.maBooking}>
            <i className="bi bi-arrow-repeat me-1" />
            Tạo lại
          </Link>
        ),
    },
  ]

  return (
    <>
      <DauTrangKhach
        icon="bi-clock-history"
        tieuDe="Lịch sử thanh toán"
        duongDan={[{ ten: 'Booking của tôi', den: '/booking/my' }, { ten: 'Lịch sử thanh toán' }]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />

        <div className="row g-3 mb-3">
          <div className="col-md-4">
            <TheChiSo icon="bi-check-circle-fill" mau="la" so={dinhDangTien(d?.tongDaThanhToan ?? 0)} nhan="Đã thanh toán" nho />
          </div>
          <div className="col-md-4">
            <TheChiSo icon="bi-hourglass-split" mau="cam" so={dinhDangTien(d?.tongChoThanhToan ?? 0)} nhan="Chờ thanh toán" nho />
          </div>
          <div className="col-md-4">
            <TheChiSo icon="bi-receipt" mau="xanh" so={d?.tongSoGiaoDich ?? 0} nhan="Tổng số giao dịch" />
          </div>
        </div>

        <TheNoiDung
          tieuDe="Giao dịch của tôi"
          icon="bi-cash-stack"
          khongDem
          phai={
            <select
              className="form-select form-select-sm w-auto"
              value={loc.trangThai}
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
              aria-label="Lọc theo trạng thái giao dịch"
            >
              <option value="">Mọi trạng thái</option>
              {TRANG_THAI.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          }
        >
          <BangDuLieu
            cot={COT}
            dong={d?.trang?.items}
            khoaDong="maTT"
            dangTai={dangTai}
            rong={loc.trangThai ? 'Không có giao dịch nào ở trạng thái này.' : 'Chưa có giao dịch thanh toán nào.'}
          />
        </TheNoiDung>

        <PhanTrang {...(d?.trang || {})} donVi="giao dịch" doiTrang={(t) => datLoc({ trang: t })} />

        {d && d.tongSoGiaoDich === 0 && (
          <div className="text-center mt-3">
            <Link className="btn btn-primary" to="/booking/create">
              <i className="bi bi-plus-circle me-1" />
              Đặt dịch vụ ngay
            </Link>
          </div>
        )}
      </div>
    </>
  )
}
