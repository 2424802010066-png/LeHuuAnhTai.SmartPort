import { Link, useNavigate, useParams } from 'react-router-dom'
import { goiApi, taiFile } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'

/** /booking/details/:id — chi tiet mot booking cua khach (thay Views/Booking/Details). */
export default function ChiTietDon() {
  const { id } = useParams()
  const { duLieu: don, loi, dangTai, taiLai } = useApi('/api/dat-dich-vu-khach/' + id)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const dieuHuong = useNavigate()

  async function huy() {
    const dongY = await xacNhan({
      tieuDe: 'Hủy booking?',
      noiDung: `Hủy booking ${don.maSoBooking} (${dinhDangTien(don.tongTien)}). Các khoản đang chờ thanh toán của đơn cũng sẽ bị hủy theo.`,
      nutDongY: 'Hủy booking',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(`/api/dat-dich-vu-khach/${id}/huy`, { method: 'POST' }))) dieuHuong('/booking/my')
  }

  async function taiExcel() {
    await chay(() => taiFile(`/api/dat-dich-vu-khach/${id}/hoa-don-excel`, { tenMacDinh: `HoaDon_${don.maSoBooking}.xlsx` }), {
      thanhCong: 'Đã tải hóa đơn Excel.',
    })
  }

  async function guiEmail() {
    await chay(() => goiApi(`/api/dat-dich-vu-khach/${id}/gui-email`, { method: 'POST' }))
  }

  const tt = don?.thanhToan

  return (
    <>
      <DauTrangKhach
        icon="bi-receipt-cutoff"
        tieuDe={don ? 'Booking ' + don.maSoBooking : 'Chi tiết booking'}
        duongDan={[{ ten: 'Booking của tôi', den: '/booking/my' }, { ten: don?.maSoBooking || 'Chi tiết' }]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && !don ? (
          <DangTai chu="Đang mở booking…" />
        ) : !don ? null : (
          <div className="row g-4">
            <div className="col-lg-8">
              <TheNoiDung tieuDe="Danh sách dịch vụ" icon="bi-list-check" khongDem>
                <div className="table-responsive">
                  <table className="table sp-bang table-sm align-middle mb-0">
                    <thead>
                      <tr>
                        <th scope="col">Dịch vụ</th>
                        <th scope="col" className="text-center">
                          Số lượng
                        </th>
                        <th scope="col" className="text-end">
                          Đơn giá
                        </th>
                        <th scope="col" className="text-end">
                          Thành tiền
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {don.dong.map((d) => (
                        <tr key={d.maCT}>
                          <td>
                            {d.tenDV}
                            {d.donViTinh && <span className="text-body-secondary small ms-1">({d.donViTinh})</span>}
                          </td>
                          <td className="text-center">{d.soLuong}</td>
                          <td className="text-end">{dinhDangTien(d.donGia)}</td>
                          <td className="text-end fw-semibold">{dinhDangTien(d.thanhTien)}</td>
                        </tr>
                      ))}
                      <tr className="table-active">
                        <td colSpan={3} className="text-end fw-bold">
                          TỔNG CỘNG
                        </td>
                        <td className="text-end fw-bold fs-5 text-warning-emphasis">{dinhDangTien(don.tongTien)}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </TheNoiDung>
            </div>

            <div className="col-lg-4">
              <TheNoiDung tieuDe="Thông tin" icon="bi-info-circle">
                <div className="dvk-hang">
                  <span>Trạng thái</span>
                  <NhanTrangThai giaTri={don.trangThai} />
                </div>
                <div className="dvk-hang">
                  <span>Ngày tạo</span>
                  <strong>{dinhDangNgayGio(don.ngayTao)}</strong>
                </div>
                <div className="dvk-hang">
                  <span>Ngày thực hiện</span>
                  <strong>{dinhDangNgay(don.ngayCanThucHien)}</strong>
                </div>
                {don.ghiChu && (
                  <div className="pt-2">
                    <div className="text-body-secondary small">Ghi chú</div>
                    <div>{don.ghiChu}</div>
                  </div>
                )}

                {don.duocThanhToan &&
                  (tt?.trangThai === 'Đã thanh toán' ? (
                    <>
                      <div className="alert alert-success mt-3 mb-2 py-2">
                        <i className="bi bi-check-circle-fill me-1" />
                        Đơn đã thanh toán
                      </div>
                      <Link className="btn btn-outline-success w-100" to={'/payment/receipt/' + tt.maTT}>
                        <i className="bi bi-receipt me-1" />
                        Xem biên lai thanh toán
                      </Link>
                    </>
                  ) : tt?.trangThai === 'Chờ thanh toán' ? (
                    <Link className="btn btn-primary w-100 mt-3" to={'/payment/pay/' + tt.maTT}>
                      <i className="bi bi-credit-card-2-front me-1" />
                      Tiếp tục thanh toán
                    </Link>
                  ) : (
                    <Link className="btn btn-primary w-100 mt-3" to={'/payment?id=' + don.maBooking}>
                      <i className="bi bi-credit-card-2-front me-1" />
                      Thanh toán ngay
                    </Link>
                  ))}

                {don.coHoaDon && (
                  <>
                    <button type="button" className="btn btn-success w-100 mt-2" disabled={dangChay} onClick={taiExcel}>
                      <i className="bi bi-file-earmark-excel me-1" />
                      Tải hóa đơn Excel (.xlsx)
                    </button>
                    <Link className="btn btn-danger w-100 mt-2" to={'/booking/invoiceprint/' + don.maBooking} target="_blank" rel="noreferrer">
                      <i className="bi bi-file-earmark-pdf me-1" />
                      Hóa đơn PDF (in / lưu)
                    </Link>
                    <button type="button" className="btn btn-outline-primary w-100 mt-2" disabled={dangChay} onClick={guiEmail}>
                      <i className="bi bi-envelope-arrow-up me-1" />
                      Gửi hóa đơn vào email của tôi
                    </button>
                  </>
                )}

                {don.duocHuy && (
                  <button type="button" className="btn btn-outline-danger w-100 mt-3" disabled={dangChay} onClick={huy}>
                    <i className="bi bi-x-circle me-1" />
                    Hủy booking
                  </button>
                )}

                <Link className="btn btn-outline-secondary w-100 mt-2" to="/booking/my">
                  <i className="bi bi-arrow-left me-1" />
                  Quay lại danh sách
                </Link>
              </TheNoiDung>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
