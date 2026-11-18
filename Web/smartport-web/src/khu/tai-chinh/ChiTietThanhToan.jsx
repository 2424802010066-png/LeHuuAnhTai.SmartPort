import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import { mauTrangThaiTT } from './dungChung'
import MaQrVietQr from './MaQrVietQr'
import './tai-chinh.css'

const Dong = ({ nhan, children }) => (
  <div className="tc-dong-tt">
    <span>{nhan}</span>
    <div>{children}</div>
  </div>
)

/** Chi tiet mot giao dich: doi chieu sao ke -> xac nhan da nhan tien / tu choi (thay Admin/Payments/Details). */
export default function ChiTietThanhToan() {
  const { id } = useParams()
  const { duLieu: t, loi, dangTai, taiLai } = useApi('/api/doi-soat-thanh-toan/' + id)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [maGiaoDich, setMaGiaoDich] = useState('')
  const [lyDo, setLyDo] = useState('')

  async function guiXacNhan(e) {
    e.preventDefault()
    // Xac nhan da nhan tien = ghi nhan doanh thu -> hoi lai mot lan, khong de bam nham
    const dong = await xacNhan({
      tieuDe: 'Xác nhận đã nhận tiền',
      noiDung: `Đã đối chiếu sao kê và nhận đủ ${dinhDangTien(t.soTien)} cho giao dịch #${t.maTT}?`,
      nutDongY: 'Xác nhận đã thu',
      mau: 'success',
    })
    if (!dong) return
    if (await chay(() => goiApi(`/api/doi-soat-thanh-toan/${t.maTT}/xac-nhan`, { method: 'POST', body: { maGiaoDich } }))) taiLai({ im: true })
  }

  async function guiTuChoi(e) {
    e.preventDefault()
    const dong = await xacNhan({
      tieuDe: 'Từ chối giao dịch',
      noiDung: `Hủy giao dịch #${t.maTT} (${dinhDangTien(t.soTien)})? Khách sẽ phải tạo lại giao dịch mới.`,
      nutDongY: 'Hủy giao dịch',
      mau: 'danger',
    })
    if (!dong) return
    if (await chay(() => goiApi(`/api/doi-soat-thanh-toan/${t.maTT}/tu-choi`, { method: 'POST', body: { lyDo } }))) taiLai({ im: true })
  }

  if (dangTai && !t) return <DangTai chu="Đang tải giao dịch…" />
  if (!t)
    return (
      <>
        <BaoLoi loi={loi} thuLai={taiLai} />
        <Link className="btn btn-sm btn-outline-primary" to="/admin/payments">
          <i className="bi bi-arrow-left me-1" />
          Về danh sách
        </Link>
      </>
    )

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/payments">
          <i className="bi bi-arrow-left me-1" />
          Về danh sách
        </Link>
        <span className="fw-bold">Giao dịch #{t.maTT}</span>
        <NhanTrangThai giaTri={t.trangThai} mau={mauTrangThaiTT(t.trangThai)} />
      </div>

      <div className="row g-3">
        <div className="col-lg-7">
          <TheNoiDung tieuDe="Thông tin giao dịch" icon="bi-receipt">
            <Dong nhan="Mã giao dịch nội bộ">
              <b>#{t.maTT}</b>
            </Dong>
            <Dong nhan="Mã tham chiếu ngân hàng">
              {t.maGiaoDich ? <b className="tc-ma">{t.maGiaoDich}</b> : <span className="text-body-secondary">— chưa có —</span>}
            </Dong>
            <Dong nhan="Nội dung chuyển khoản">
              <b className="tc-ma text-primary">{t.noiDungCK}</b>
            </Dong>
            <Dong nhan="Phương thức">{t.tenPhuongThuc}</Dong>
            <Dong nhan="Ngày tạo">{dinhDangNgayGio(t.ngayTao)}</Dong>
            {t.ngayThanhToan && <Dong nhan="Ngày thanh toán">{dinhDangNgayGio(t.ngayThanhToan)}</Dong>}
            {t.ghiChu && <Dong nhan="Ghi chú">{t.ghiChu}</Dong>}

            <div className="tc-hop-tien mt-3 d-flex flex-wrap justify-content-between align-items-baseline gap-2">
              <span className="text-body-secondary small fw-semibold">SỐ TIỀN</span>
              <span className="tc-so-tien-lon tc-tien">{dinhDangTien(t.soTien)}</span>
            </div>
            <div className="small fst-italic text-body-secondary mt-1">{t.soTienChu}</div>

            {t.booking && (
              <>
                <h6 className="fw-bold mt-4 mb-2">Đơn đặt dịch vụ</h6>
                <Dong nhan="Mã booking">
                  <b className="tc-ma">{t.booking.maSoBooking}</b>
                </Dong>
                <Dong nhan="Khách hàng">{t.booking.tenKhach || '—'}</Dong>
                <Dong nhan="Trạng thái đơn">
                  <NhanTrangThai giaTri={t.booking.trangThai} />
                </Dong>
                <Dong nhan="Giá trị đơn">{dinhDangTien(t.booking.tongTien)}</Dong>
                <Link className="btn btn-sm btn-outline-secondary mt-2" to={'/admin/bookings/details/' + t.booking.maBooking}>
                  <i className="bi bi-box-arrow-up-right me-1" />
                  Xem chi tiết đơn
                </Link>
              </>
            )}
          </TheNoiDung>
        </div>

        <div className="col-lg-5">
          {t.coTheXacNhan && (
            <TheNoiDung tieuDe="Xác nhận đã nhận tiền" icon="bi-check2-square">
              <p className="small text-body-secondary">
                Đối chiếu sao kê ngân hàng với nội dung <b className="tc-ma">{t.noiDungCK}</b> và số tiền <b>{dinhDangTien(t.soTien)}</b>.
              </p>
              <form onSubmit={guiXacNhan}>
                <Truong nhan="Mã tham chiếu ngân hàng (tùy chọn)" lop="mb-2">
                  <input
                    className="form-control form-control-sm"
                    maxLength={50}
                    placeholder="VD: FT26071234567"
                    value={maGiaoDich}
                    onChange={(e) => setMaGiaoDich(e.target.value)}
                  />
                </Truong>
                <button type="submit" className="btn btn-success w-100" disabled={dangChay}>
                  <i className="bi bi-check-circle me-1" />
                  Xác nhận đã thanh toán
                </button>
              </form>
            </TheNoiDung>
          )}

          {t.coTheTuChoi && (
            <TheNoiDung tieuDe="Từ chối / hủy giao dịch" icon="bi-x-circle">
              <form onSubmit={guiTuChoi}>
                <Truong nhan="Lý do từ chối" lop="mb-2">
                  <input
                    className="form-control form-control-sm"
                    maxLength={250}
                    placeholder="VD: Không tìm thấy giao dịch trong sao kê"
                    value={lyDo}
                    onChange={(e) => setLyDo(e.target.value)}
                  />
                </Truong>
                <button type="submit" className="btn btn-outline-danger w-100" disabled={dangChay}>
                  <i className="bi bi-x-circle me-1" />
                  Từ chối / hủy giao dịch
                </button>
              </form>
            </TheNoiDung>
          )}

          {!t.coTheXacNhan && !t.coTheTuChoi && (
            <TheNoiDung>
              <div className="text-center py-2">
                <div className="fs-4 text-success">
                  <i className="bi bi-check-circle-fill me-1" />
                  Giao dịch đã được xác nhận
                </div>
                <div className="small text-body-secondary mt-2">
                  Đã thu {dinhDangTien(t.soTien)} lúc {dinhDangNgayGio(t.ngayThanhToan)}
                </div>
                <div className="small text-body-secondary">Muốn trả lại tiền phải làm nghiệp vụ hoàn tiền, không hủy giao dịch.</div>
              </div>
            </TheNoiDung>
          )}

          {t.phuongThuc === 'ChuyenKhoan' && (t.qrNoiDung || t.qrUrl) && (
            <TheNoiDung tieuDe="Mã QR của giao dịch" icon="bi-qr-code">
              <div className="text-center">
                {/* Ve tai cho tu chuoi VietQR may chu tu sinh (khong can Internet); anh qrUrl chi du phong */}
                <MaQrVietQr
                  className="tc-qr-anh"
                  noiDung={t.qrNoiDung}
                  url={t.qrUrl}
                  canh={230}
                  style={{ display: 'inline-block' }}
                  moTa={`Mã QR chuyển khoản ${dinhDangTien(t.soTien)}`}
                />
                <div className="small text-body-secondary mt-2">
                  {t.bankName} · {t.bankAccount}
                  <br />
                  {t.bankAccountName}
                </div>
              </div>
            </TheNoiDung>
          )}
        </div>
      </div>
    </>
  )
}
