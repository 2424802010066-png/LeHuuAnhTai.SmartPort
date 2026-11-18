import { Link, useParams } from 'react-router-dom'
import { goiApi, taiFile } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import { API_DON, NUT_TRANG_THAI, duongHoSoKhach, duongInHoaDon } from './chung'
import './kinh-doanh.css'

/** Xu ly mot booking: dich vu, khach hang, hoa don Excel / PDF / email, doi trang thai (thay Admin/Bookings/Details). */
export default function ChiTietDon() {
  const { id } = useParams()
  const { duLieu: d, loi, dangTai, taiLai } = useApi(`${API_DON}/${id}`)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  async function doiTrangThai(trangThai) {
    const nut = NUT_TRANG_THAI[trangThai]
    if (nut?.hoi) {
      const dongY = await xacNhan({ tieuDe: nut.nhan + '?', noiDung: `Booking ${d.maSoBooking}. ` + nut.hoi, nutDongY: nut.nhan, mau: 'danger' })
      if (!dongY) return
    }
    if (await chay(() => goiApi(`${API_DON}/${id}/trang-thai`, { method: 'POST', body: { trangThai } }))) taiLai({ im: true })
  }

  const xuatExcel = () => chay(() => taiFile(`${API_DON}/${id}/hoa-don-excel`, { tenMacDinh: 'HoaDon.xlsx' }), { thanhCong: 'Đã tải hóa đơn Excel về máy.' })

  async function guiEmail() {
    const email = d.khachHang?.email
    const dongY = await xacNhan({
      tieuDe: 'Gửi hóa đơn qua email?',
      noiDung: email ? `Gửi hóa đơn ${d.maSoBooking} (đính kèm file Excel) tới ${email}.` : 'Khách hàng chưa có email — hệ thống sẽ báo lỗi.',
      nutDongY: 'Gửi',
      mau: 'primary',
    })
    if (!dongY) return
    await chay(() => goiApi(`${API_DON}/${id}/gui-email`, { method: 'POST' }))
  }

  if (!d) {
    return (
      <>
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && <DangTai chu="Đang tải booking…" />}
      </>
    )
  }

  const kh = d.khachHang
  const COT = [
    { khoa: 'ten', ten: 'Dịch vụ', hien: (c) => <span className="fw-semibold">{c.tenDV || '#' + c.maDV}</span> },
    { khoa: 'dvt', ten: 'ĐVT', an: 'md', hien: (c) => c.donViTinh || '—' },
    { khoa: 'sl', ten: 'SL', canPhai: true, hien: (c) => c.soLuong },
    { khoa: 'donGia', ten: 'Đơn giá', canPhai: true, lop: 'text-nowrap', hien: (c) => dinhDangTien(c.donGia) },
    { khoa: 'thanhTien', ten: 'Thành tiền', canPhai: true, lop: 'fw-semibold text-nowrap', hien: (c) => dinhDangTien(c.thanhTien) },
    { khoa: 'ghiChu', ten: 'Ghi chú', an: 'lg', lop: 'small text-body-secondary', hien: (c) => c.ghiChu || '' },
  ]

  return (
    <div className="row g-3">
      <div className="col-lg-8">
        <TheNoiDung
          tieuDe={`Dịch vụ trong booking ${d.maSoBooking}`}
          icon="bi-list-check"
          khongDem
          chan={
            <div className="d-flex flex-wrap justify-content-end gap-4 kd-tong-don">
              <div>
                <span className="text-body-secondary me-2">Cộng tiền dịch vụ</span>
                <b>{dinhDangTien(d.tongTien)}</b>
              </div>
              <div>
                <span className="text-body-secondary me-2">Thuế GTGT 8%</span>
                <b>{dinhDangTien(d.tienVat)}</b>
              </div>
              <div className="kd-tong-lon">
                <span className="text-body-secondary me-2">TỔNG THANH TOÁN</span>
                <b>{dinhDangTien(d.tongThanhToan)}</b>
              </div>
            </div>
          }
        >
          <BangDuLieu cot={COT} dong={d.dichVus} khoaDong="maCT" rong="Booking chưa có dòng dịch vụ nào." />
        </TheNoiDung>

        {kh && (
          <TheNoiDung tieuDe="Khách hàng" icon="bi-buildings">
            <div className="row g-2 small">
              <div className="col-md-6">
                <span className="text-body-secondary">Công ty:</span>{' '}
                <Link className="fw-semibold" to={duongHoSoKhach(kh.maKH)}>
                  {kh.tenCongTy}
                </Link>
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">MST:</span> {kh.maSoThue || '—'}
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">Người liên hệ:</span> {kh.nguoiLienHe || '—'}
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">SĐT:</span> {kh.soDienThoai || '—'} — {kh.email || '—'}
              </div>
              <div className="col-12">
                <span className="text-body-secondary">Địa chỉ:</span> {kh.diaChi || '—'}
              </div>
            </div>
          </TheNoiDung>
        )}

        {!!d.thanhToans?.length && (
          <TheNoiDung tieuDe="Thanh toán của booking" icon="bi-credit-card" khongDem>
            <BangDuLieu
              khoaDong="maTT"
              dong={d.thanhToans}
              cot={[
                { khoa: 'pt', ten: 'Phương thức', hien: (t) => t.tenPhuongThuc || t.phuongThuc },
                { khoa: 'maGd', ten: 'Mã giao dịch', an: 'md', lop: 'small', hien: (t) => t.maGiaoDich || '—' },
                { khoa: 'ngay', ten: 'Ngày thanh toán', an: 'lg', lop: 'small text-nowrap', hien: (t) => dinhDangNgayGio(t.ngayThanhToan) },
                { khoa: 'tien', ten: 'Số tiền', canPhai: true, lop: 'fw-semibold text-nowrap', hien: (t) => dinhDangTien(t.soTien) },
                { khoa: 'tt', ten: 'Trạng thái', canPhai: true, hien: (t) => <NhanTrangThai giaTri={t.trangThai} /> },
              ]}
            />
          </TheNoiDung>
        )}
      </div>

      <div className="col-lg-4">
        <TheNoiDung tieuDe="Trạng thái xử lý" icon="bi-clipboard-check">
          <div className="d-flex justify-content-between py-2 border-bottom">
            <span className="text-body-secondary">Hiện tại</span>
            <NhanTrangThai giaTri={d.trangThai} />
          </div>
          <div className="d-flex justify-content-between py-2 border-bottom">
            <span className="text-body-secondary">Ngày tạo</span>
            <strong>{dinhDangNgayGio(d.ngayTao)}</strong>
          </div>
          <div className="d-flex justify-content-between py-2 border-bottom">
            <span className="text-body-secondary">Cần thực hiện</span>
            <strong>{dinhDangNgay(d.ngayCanThucHien)}</strong>
          </div>
          <div className="d-flex justify-content-between py-2 border-bottom">
            <span className="text-body-secondary">Đã thanh toán</span>
            <strong>{dinhDangTien(d.daThanhToan)}</strong>
          </div>
          {d.ghiChu && (
            <div className="py-2 border-bottom">
              <div className="text-body-secondary small">Ghi chú của khách</div>
              {d.ghiChu}
            </div>
          )}

          <div className="d-grid gap-2 mt-3">
            {d.trangThaiTiep?.map((tt) => {
              const nut = NUT_TRANG_THAI[tt]
              if (!nut) return null
              return (
                <button key={tt} type="button" className={'btn btn-' + nut.mau} disabled={dangChay} onClick={() => doiTrangThai(tt)}>
                  <i className={'bi ' + nut.icon + ' me-1'} />
                  {nut.nhan}
                </button>
              )
            })}
            <button type="button" className="btn btn-success" disabled={dangChay} onClick={xuatExcel}>
              <i className="bi bi-file-earmark-excel me-1" />
              Xuất hóa đơn Excel (.xlsx)
            </button>
            <Link className="btn btn-danger" to={duongInHoaDon(d.maBooking)} target="_blank" rel="noreferrer">
              <i className="bi bi-file-earmark-pdf me-1" />
              Hóa đơn PDF (in / lưu)
            </Link>
            <button type="button" className="btn btn-outline-primary" disabled={dangChay} onClick={guiEmail} title={'Gửi tới ' + (kh?.email || 'email khách hàng')}>
              <i className="bi bi-envelope-arrow-up me-1" />
              Gửi hóa đơn qua email KH
            </button>
            <Link className="btn btn-outline-secondary" to="/admin/bookings">
              <i className="bi bi-arrow-left me-1" />
              Danh sách booking
            </Link>
          </div>
        </TheNoiDung>
      </div>
    </div>
  )
}
