import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, PhanTrang, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'

const TRANG_THAI = ['Chờ duyệt', 'Đã duyệt', 'Đang thực hiện', 'Hoàn thành', 'Đã hủy']

/** /booking/my — danh sach booking cua chinh khach dang dang nhap (thay Views/Booking/My). */
export default function DonCuaToi() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/dat-dich-vu-khach' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  async function huy(d) {
    const dongY = await xacNhan({
      tieuDe: 'Hủy booking?',
      noiDung: `Hủy booking ${d.maSoBooking} (${dinhDangTien(d.tongTien)}). Các khoản đang chờ thanh toán của đơn cũng sẽ bị hủy theo.`,
      nutDongY: 'Hủy booking',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(`/api/dat-dich-vu-khach/${d.maBooking}/huy`, { method: 'POST' }))) taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'so',
      ten: 'Số booking',
      hien: (d) => (
        <Link className="fw-semibold text-decoration-none" to={'/booking/details/' + d.maBooking}>
          {d.maSoBooking}
        </Link>
      ),
    },
    { khoa: 'ngayTao', ten: 'Ngày tạo', an: 'md', hien: (d) => dinhDangNgayGio(d.ngayTao) },
    { khoa: 'ngayTh', ten: 'Ngày thực hiện', an: 'lg', hien: (d) => dinhDangNgay(d.ngayCanThucHien) },
    { khoa: 'soDong', ten: 'Số dịch vụ', canPhai: true, an: 'lg', hien: (d) => d.soDongDichVu },
    { khoa: 'tien', ten: 'Tổng tiền', canPhai: true, hien: (d) => <span className="fw-semibold">{dinhDangTien(d.tongTien)}</span> },
    { khoa: 'tt', ten: 'Trạng thái', hien: (d) => <NhanTrangThai giaTri={d.trangThai} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 150,
      hien: (d) => (
        <>
          <Link className="btn btn-sm btn-outline-primary me-1" to={'/booking/details/' + d.maBooking} title="Xem chi tiết booking">
            <i className="bi bi-eye" />
          </Link>
          {d.duocHuy && (
            <button type="button" className="btn btn-sm btn-outline-danger" title="Hủy booking" disabled={dangChay} onClick={() => huy(d)}>
              <i className="bi bi-x-circle" />
            </button>
          )}
        </>
      ),
    },
  ]

  return (
    <>
      <DauTrangKhach
        icon="bi-receipt"
        tieuDe="Booking của tôi"
        duongDan={[{ ten: 'Booking của tôi' }]}
        phai={
          <Link className="btn btn-warning fw-semibold" to="/booking/create">
            <i className="bi bi-plus-circle me-1" />
            Đặt dịch vụ mới
          </Link>
        }
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />

        <TheNoiDung
          tieuDe="Đơn đặt dịch vụ"
          icon="bi-receipt-cutoff"
          khongDem
          phai={
            <>
              <select
                className="form-select form-select-sm w-auto"
                value={loc.trangThai}
                onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
                aria-label="Lọc theo trạng thái"
              >
                <option value="">Mọi trạng thái</option>
                {TRANG_THAI.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
              <Link className="btn btn-sm btn-outline-secondary" to="/payment/history">
                <i className="bi bi-clock-history me-1" />
                Lịch sử thanh toán
              </Link>
            </>
          }
        >
          <BangDuLieu
            cot={COT}
            dong={duLieu?.items}
            khoaDong="maBooking"
            dangTai={dangTai}
            rong={loc.trangThai ? 'Không có booking nào ở trạng thái này.' : 'Bạn chưa có booking nào — bấm “Đặt dịch vụ mới” để bắt đầu.'}
          />
        </TheNoiDung>

        <PhanTrang {...(duLieu || {})} donVi="booking" doiTrang={(t) => datLoc({ trang: t })} />
      </div>
    </>
  )
}
