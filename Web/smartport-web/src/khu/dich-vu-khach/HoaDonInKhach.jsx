import { useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import './dich-vu-khach.css'

const tien = (v) => dinhDangSo(v) + ' đ'

/**
 * /booking/invoiceprint/:id — ban in hoa don cua CHINH khach (thay Views/Booking/InvoicePrint.cshtml).
 * Khung 'in' cua he thong da lo nut "In / Quay lai" nen trang nay chi ve to hoa don.
 */
export default function HoaDonInKhach() {
  const { id } = useParams()
  const { duLieu: d, loi, dangTai, taiLai } = useApi(`/api/dat-dich-vu-khach/${id}/hoa-don`)

  useEffect(() => {
    if (!d) return undefined
    // Ten tai lieu = ten file PDF khi chon "Luu thanh PDF" trong hop thoai in
    const cu = document.title
    document.title = `Hóa đơn ${d.maSoBooking} — SmartPort`
    return () => {
      document.title = cu
    }
  }, [d])

  if (!d) {
    return <div data-bs-theme="light">{loi ? <BaoLoi loi={loi} thuLai={taiLai} /> : dangTai ? <DangTai chu="Đang chuẩn bị hóa đơn…" /> : null}</div>
  }

  return (
    <div className="dvk-in" data-bs-theme="light">
      <div className="dvk-in-dau">
        <div>
          <div className="fs-4 fw-bold" style={{ color: '#0a2f57', letterSpacing: '.04em' }}>
            SMARTPORT
          </div>
          <div className="small" style={{ color: '#5b6b7c' }}>
            CẢNG QUỐC TẾ · Khu bến Cát Lái, TP. Thủ Đức, TP.HCM
            <br />
            Hotline 0387 562 579 · MST 0312 999 888
          </div>
        </div>
        <div className="text-end">
          <div className="small" style={{ color: '#5b6b7c', letterSpacing: '.12em' }}>
            SỐ HÓA ĐƠN
          </div>
          <div className="fs-5 fw-bold" style={{ color: '#c2410c' }}>
            {d.maSoBooking}
          </div>
          <div className="small" style={{ color: '#5b6b7c' }}>
            Ngày lập: {dinhDangNgay(d.ngayLap)}
          </div>
        </div>
      </div>

      <h2>HÓA ĐƠN DỊCH VỤ CẢNG</h2>
      <div className="text-center fst-italic mb-3" style={{ color: '#5b6b7c' }}>
        (Kèm bảng kê chi tiết dịch vụ theo booking {d.maSoBooking})
      </div>

      <div className="row g-3 mb-3">
        <div className="col-6">
          <div className="dvk-in-hop h-100">
            <h6>Thông tin khách hàng</h6>
            <p className="mb-1">
              <strong>{d.tenCongTy || '—'}</strong>
            </p>
            <p className="mb-1">MST: {d.maSoThue || '—'}</p>
            <p className="mb-1">Địa chỉ: {d.diaChi || '—'}</p>
            <p className="mb-0">
              Người liên hệ: {d.nguoiLienHe || '—'} {d.soDienThoai ? '· ' + d.soDienThoai : ''}
            </p>
          </div>
        </div>
        <div className="col-6">
          <div className="dvk-in-hop h-100">
            <h6>Thông tin booking</h6>
            <p className="mb-1">
              Ngày tạo: <strong>{dinhDangNgayGio(d.ngayTao)}</strong>
            </p>
            <p className="mb-1">
              Ngày thực hiện: <strong>{dinhDangNgay(d.ngayCanThucHien)}</strong>
            </p>
            <p className="mb-1">
              Trạng thái: <strong>{d.trangThai}</strong>
            </p>
            {d.ghiChu && <p className="mb-0">Ghi chú: {d.ghiChu}</p>}
          </div>
        </div>
      </div>

      <table>
        <thead>
          <tr>
            <th className="text-center" style={{ width: 44 }}>
              STT
            </th>
            <th>Tên dịch vụ</th>
            <th className="text-center" style={{ width: 70 }}>
              ĐVT
            </th>
            <th className="text-center" style={{ width: 70 }}>
              SL
            </th>
            <th className="text-end" style={{ width: 110 }}>
              Đơn giá (đ)
            </th>
            <th className="text-end" style={{ width: 130 }}>
              Thành tiền (đ)
            </th>
          </tr>
        </thead>
        <tbody>
          {d.dong.map((c, i) => (
            <tr key={c.maCT}>
              <td className="text-center">{i + 1}</td>
              <td>{c.tenDV}</td>
              <td className="text-center">{c.donViTinh || ''}</td>
              <td className="text-center">{c.soLuong}</td>
              <td className="text-end">{dinhDangSo(c.donGia)}</td>
              <td className="text-end">
                <strong>{dinhDangSo(c.thanhTien)}</strong>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="dvk-in-tong">
        <div>
          <span>Cộng tiền dịch vụ</span>
          <span>{tien(d.congTienDichVu)}</span>
        </div>
        <div>
          <span>Thuế GTGT (8%)</span>
          <span>{tien(d.thueGtgt)}</span>
        </div>
        <div className="cong">
          <span>TỔNG THANH TOÁN</span>
          <span>{tien(d.tongThanhToan)}</span>
        </div>
      </div>

      <div className="dvk-in-chu">
        Bằng chữ: <strong>{d.bangChu}</strong>
      </div>

      <div className="dvk-in-ky">
        <div>
          <strong>NGƯỜI LẬP HÓA ĐƠN</strong>
          <br />
          <small>(Ký, họ tên)</small>
          <div className="cho-ky" />
        </div>
        <div>
          <strong>KẾ TOÁN TRƯỞNG</strong>
          <br />
          <small>(Ký, họ tên)</small>
          <div className="cho-ky" />
        </div>
        <div>
          <strong>KHÁCH HÀNG</strong>
          <br />
          <small>(Ký, họ tên)</small>
          <div className="cho-ky" />
        </div>
      </div>

      <div className="text-center small mt-4" style={{ color: '#5b6b7c' }}>
        Hóa đơn phát hành từ hệ thống ePort — Cảng Quốc tế SmartPort · Cảm ơn Quý khách đã sử dụng dịch vụ!
      </div>
    </div>
  )
}
