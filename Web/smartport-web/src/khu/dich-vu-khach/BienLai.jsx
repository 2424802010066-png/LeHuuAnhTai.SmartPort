import { Link, useParams } from 'react-router-dom'
import { taiFile } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'
import './dich-vu-khach.css'

/** /payment/receipt/:id — bien lai giao dich (thay Views/Payment/Receipt.cshtml). */
export default function BienLai() {
  const { id } = useParams()
  const { duLieu: d, loi, dangTai, taiLai } = useApi(`/api/thanh-toan-khach/${id}/bien-lai`)
  const { chay, dangChay } = useThaoTac()

  const gd = d?.giaoDich
  const daThanhToan = gd?.trangThai === 'Đã thanh toán'

  async function taiExcel() {
    await chay(() => taiFile(`/api/dat-dich-vu-khach/${d.maBooking}/hoa-don-excel`, { tenMacDinh: `HoaDon_${d.maSoBooking}.xlsx` }), {
      thanhCong: 'Đã tải hóa đơn Excel.',
    })
  }

  return (
    <>
      <DauTrangKhach
        icon="bi-patch-check"
        tieuDe="Biên lai thanh toán"
        duongDan={[
          { ten: 'Booking của tôi', den: '/booking/my' },
          { ten: 'Lịch sử thanh toán', den: '/payment/history' },
          { ten: 'Biên lai #' + id },
        ]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && !d ? (
          <DangTai chu="Đang mở biên lai…" />
        ) : !d ? null : (
          <>
            <div className="dvk-bien-lai">
              <div className={'dvk-bien-lai-dau' + (daThanhToan ? '' : ' cho')}>
                <div className="dvk-bien-lai-ico" aria-hidden="true">
                  <i className={'bi ' + (daThanhToan ? 'bi-check-lg' : 'bi-hourglass-split')} />
                </div>
                <h2 className="h5 fw-bold mb-1">{daThanhToan ? 'Thanh toán thành công' : 'Biên lai giao dịch'}</h2>
                <div className="small opacity-75">SmartPort — Cảng Quốc tế · Logistics</div>
                <div className="fs-3 fw-bold mt-2">{dinhDangTien(gd.soTien)}</div>
              </div>

              <div className="p-3 p-md-4">
                <div className="dvk-hang">
                  <span>Mã giao dịch</span>
                  <strong>
                    #{gd.maTT}
                    {gd.maGiaoDich ? ' · ' + gd.maGiaoDich : ''}
                  </strong>
                </div>
                <div className="dvk-hang">
                  <span>Mã booking</span>
                  <strong>{d.maSoBooking || '—'}</strong>
                </div>
                <div className="dvk-hang">
                  <span>Khách hàng</span>
                  <strong>{d.tenKhach || '—'}</strong>
                </div>
                <div className="dvk-hang">
                  <span>Phương thức</span>
                  <strong>{gd.tenPhuongThuc}</strong>
                </div>
                {gd.noiDungCK && (
                  <div className="dvk-hang">
                    <span>Nội dung CK</span>
                    <strong>{gd.noiDungCK}</strong>
                  </div>
                )}
                <div className="dvk-hang">
                  <span>Trạng thái</span>
                  <NhanTrangThai giaTri={gd.trangThai} mau={gd.trangThai === 'Quá hạn' ? 'do' : undefined} />
                </div>
                <div className="dvk-hang">
                  <span>Ngày tạo</span>
                  <strong>{dinhDangNgayGio(gd.ngayTao)}</strong>
                </div>
                {gd.ngayThanhToan && (
                  <div className="dvk-hang">
                    <span>Ngày thanh toán</span>
                    <strong>{dinhDangNgayGio(gd.ngayThanhToan)}</strong>
                  </div>
                )}

                {d.dong.length > 0 && (
                  <>
                    <h3 className="h6 fw-bold mt-4 mb-2">Chi tiết dịch vụ</h3>
                    <div className="table-responsive">
                      <table className="table sp-bang table-sm align-middle mb-0">
                        <thead>
                          <tr>
                            <th scope="col">Dịch vụ</th>
                            <th scope="col" className="text-center">
                              SL
                            </th>
                            <th scope="col" className="text-end">
                              Thành tiền
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {d.dong.map((c) => (
                            <tr key={c.maCT}>
                              <td>{c.tenDV}</td>
                              <td className="text-center">{c.soLuong}</td>
                              <td className="text-end">{dinhDangTien(c.thanhTien)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}

                <div className="d-flex justify-content-between align-items-center border-top pt-3 mt-3">
                  <span className="fw-semibold">TỔNG THANH TOÁN</span>
                  <span className="fs-4 fw-bold text-warning-emphasis">{dinhDangTien(gd.soTien)}</span>
                </div>
                <div className="text-center fst-italic text-body-secondary small mt-1">Bằng chữ: {d.soTienBangChu}</div>

                {daThanhToan && (
                  <div className="text-center mt-4">
                    <span className="dvk-dau-mong">ĐÃ THANH TOÁN</span>
                  </div>
                )}
              </div>

              <div className="border-top p-3 text-center small text-body-secondary">
                {d.cheDoDemo && (
                  <div className="fw-semibold text-warning-emphasis mb-1">⚠ BIÊN LAI DEMO — đồ án môn Lập trình Web</div>
                )}
                Cảm ơn Quý khách đã sử dụng dịch vụ của SmartPort
                <br />
                Hotline 0387 562 579
              </div>
            </div>

            <div className="d-flex flex-wrap justify-content-center gap-2 mt-4 d-print-none">
              <button type="button" className="btn btn-primary" onClick={() => window.print()}>
                <i className="bi bi-printer me-1" />
                In biên lai
              </button>
              <button type="button" className="btn btn-success" disabled={dangChay} onClick={taiExcel}>
                <i className="bi bi-file-earmark-excel me-1" />
                Hóa đơn Excel
              </button>
              <Link className="btn btn-outline-secondary" to={'/booking/details/' + d.maBooking}>
                <i className="bi bi-arrow-left me-1" />
                Về đơn hàng
              </Link>
              <Link className="btn btn-outline-secondary" to="/payment/history">
                <i className="bi bi-clock-history me-1" />
                Lịch sử thanh toán
              </Link>
            </div>
          </>
        )}
      </div>
    </>
  )
}
