import { useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { API_DON } from './chung'
import './kinh-doanh.css'

const tien = (v) => dinhDangSo(v) + ' đ'

/** Hoa don dich vu cang — ban in (khung "in": nut In / Quay lai), thay Views/Booking/InvoicePrint.cshtml. */
export default function HoaDonIn() {
  const { id } = useParams()
  const { duLieu: d, loi, dangTai, taiLai } = useApi(`${API_DON}/${id}`)

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

  const kh = d.khachHang

  return (
    <div className="kd-hd" data-bs-theme="light">
      <div className="kd-hd-dau">
        <div>
          <h1>SMARTPORT</h1>
          <div className="kd-hd-ct">
            CẢNG QUỐC TẾ · Khu bến Cát Lái, TP. Thủ Đức, TP.HCM
            <br />
            Hotline 0387 562 579 · MST 0312 999 888
          </div>
        </div>
        <div className="kd-hd-so">
          <span className="kd-hd-nhan">SỐ HÓA ĐƠN</span>
          <b>{d.maSoBooking}</b>
          <span className="kd-hd-ngay">Ngày lập: {dinhDangNgay(new Date())}</span>
        </div>
      </div>

      <h2>HÓA ĐƠN DỊCH VỤ CẢNG</h2>
      <div className="kd-hd-phu">(Kèm bảng kê chi tiết dịch vụ theo booking {d.maSoBooking})</div>

      <div className="kd-hd-luoi">
        <div className="kd-hd-o">
          <h6>Thông tin khách hàng</h6>
          <p>
            <strong>{kh?.tenCongTy || '—'}</strong>
          </p>
          <p>MST: {kh?.maSoThue || '—'}</p>
          <p>Địa chỉ: {kh?.diaChi || '—'}</p>
          <p>
            Người liên hệ: {kh?.nguoiLienHe || '—'} · {kh?.soDienThoai || ''}
          </p>
        </div>
        <div className="kd-hd-o">
          <h6>Thông tin booking</h6>
          <p>
            Ngày tạo: <strong>{dinhDangNgayGio(d.ngayTao)}</strong>
          </p>
          <p>
            Ngày thực hiện: <strong>{dinhDangNgay(d.ngayCanThucHien)}</strong>
          </p>
          <p>
            Trạng thái: <strong>{d.trangThai}</strong>
          </p>
          {d.ghiChu && <p>Ghi chú: {d.ghiChu}</p>}
        </div>
      </div>

      <table className="kd-hd-dong">
        <thead>
          <tr>
            <th className="giua" style={{ width: 44 }}>
              STT
            </th>
            <th>Tên dịch vụ</th>
            <th className="giua" style={{ width: 70 }}>
              ĐVT
            </th>
            <th className="giua" style={{ width: 70 }}>
              SL
            </th>
            <th className="phai" style={{ width: 110 }}>
              Đơn giá (đ)
            </th>
            <th className="phai" style={{ width: 130 }}>
              Thành tiền (đ)
            </th>
          </tr>
        </thead>
        <tbody>
          {d.dichVus.map((c, i) => (
            <tr key={c.maCT}>
              <td className="giua">{i + 1}</td>
              <td>{c.tenDV || 'Dịch vụ #' + c.maDV}</td>
              <td className="giua">{c.donViTinh || ''}</td>
              <td className="giua">{c.soLuong}</td>
              <td className="phai">{dinhDangSo(c.donGia)}</td>
              <td className="phai">
                <strong>{dinhDangSo(c.thanhTien)}</strong>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="kd-hd-cong">
        <div className="hang">
          <span>Cộng tiền dịch vụ</span>
          <span>{tien(d.tongTien)}</span>
        </div>
        <div className="hang">
          <span>Thuế GTGT (8%)</span>
          <span>{tien(d.tienVat)}</span>
        </div>
        <div className="hang tong">
          <span>TỔNG THANH TOÁN</span>
          <span>{tien(d.tongThanhToan)}</span>
        </div>
      </div>

      <div className="kd-hd-chu">
        Bằng chữ: <strong>{d.bangChu}</strong>
      </div>

      <div className="kd-hd-ky">
        <div>
          <strong>NGƯỜI LẬP HÓA ĐƠN</strong>
          <em>(Ký, họ tên)</em>
        </div>
        <div>
          <strong>KẾ TOÁN TRƯỞNG</strong>
          <em>(Ký, họ tên)</em>
        </div>
        <div>
          <strong>KHÁCH HÀNG</strong>
          <em>(Ký, họ tên)</em>
        </div>
      </div>

      <div className="kd-hd-chan">Hóa đơn phát hành từ hệ thống ePort — Cảng Quốc tế SmartPort · Cảm ơn Quý khách đã sử dụng dịch vụ!</div>
    </div>
  )
}
