import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangTien } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'
import './dich-vu-khach.css'

const PHUONG_THUC = [
  {
    ma: 'ChuyenKhoan',
    ten: 'Chuyển khoản QR',
    nhan: 'Khuyên dùng',
    icon: 'bi-qr-code-scan',
    nen: 'linear-gradient(135deg,#0a2f57,#1c5fa8)',
    mo: 'Quét mã VietQR bằng app ngân hàng — số tiền và nội dung đã điền sẵn, không lo sai sót.',
    the: ['NAPAS 247', 'VietQR', 'Miễn phí'],
  },
  {
    ma: 'TheNganHang',
    ten: 'Thẻ ngân hàng',
    icon: 'bi-credit-card',
    nen: 'linear-gradient(135deg,#7c3aed,#a855f7)',
    mo: 'Thẻ nội địa ATM hoặc thẻ quốc tế Visa / Mastercard / JCB.',
    the: ['ATM nội địa', 'Visa', 'Mastercard'],
  },
  {
    ma: 'ViDienTu',
    ten: 'Ví điện tử',
    icon: 'bi-wallet2',
    nen: 'linear-gradient(135deg,#e5691d,#ff9d4d)',
    mo: 'Thanh toán nhanh qua ví đã liên kết, xác nhận bằng mã PIN.',
    the: ['MoMo', 'ZaloPay', 'VNPay'],
  },
  {
    ma: 'TienMat',
    ten: 'Tiền mặt tại quầy',
    icon: 'bi-cash-coin',
    nen: 'linear-gradient(135deg,#15803d,#22c55e)',
    mo: 'Thanh toán trực tiếp tại quầy thu ngân của cảng khi làm thủ tục.',
    the: ['Giờ hành chính 7:30–17:00'],
  },
]

/**
 * /payment?id=<mã booking> (và địa chỉ cũ /payment/index/:id) — bước 1 của cổng thanh toán:
 * chọn phương thức rồi mở giao dịch (thay Views/Payment/Index.cshtml).
 */
export default function ChonPhuongThuc() {
  const { id: idTuTuyen } = useParams()
  const [thamSo] = useSearchParams()
  const maBooking = idTuTuyen || thamSo.get('id') || ''
  const dieuHuong = useNavigate()
  const { chay, dangChay } = useThaoTac()
  const [chon, setChon] = useState('ChuyenKhoan')

  const { duLieu: d, loi, dangTai, taiLai } = useApi(maBooking ? '/api/thanh-toan-khach/don/' + maBooking : null)

  useEffect(() => {
    if (d) document.title = 'Thanh toán ' + (d.maSoBooking || '') + ' — SmartPort'
  }, [d])

  if (!maBooking) {
    return (
      <>
        <DauTrangKhach icon="bi-credit-card-2-front" tieuDe="Thanh toán trực tuyến" duongDan={[{ ten: 'Thanh toán' }]} />
        <div className="container py-5 text-center">
          <p className="text-body-secondary">Chưa chọn đơn cần thanh toán.</p>
          <Link className="btn btn-primary" to="/booking/my">
            <i className="bi bi-receipt me-1" />
            Chọn booking của tôi
          </Link>
        </div>
      </>
    )
  }

  // Don da thanh toan xong -> vao thang bien lai (giong MVC)
  if (d?.maTTDaThanhToan) return <Navigate to={'/payment/receipt/' + d.maTTDaThanhToan} replace />

  async function tiepTuc() {
    const kq = await chay(() => goiApi(`/api/thanh-toan-khach/don/${maBooking}/bat-dau`, { method: 'POST', body: { phuongThuc: chon } }), {
      imLang: true,
    })
    if (kq?.id) dieuHuong(kq.duLieu?.daThanhToan ? '/payment/receipt/' + kq.id : '/payment/pay/' + kq.id)
  }

  return (
    <>
      <DauTrangKhach
        icon="bi-credit-card-2-front"
        tieuDe="Thanh toán trực tuyến"
        duongDan={[
          { ten: 'Booking của tôi', den: '/booking/my' },
          { ten: d?.maSoBooking || 'Đơn', den: '/booking/details/' + maBooking },
          { ten: 'Thanh toán' },
        ]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && !d ? (
          <DangTai chu="Đang mở cổng thanh toán…" />
        ) : !d ? null : (
          <>
            {d.cauHinh?.cheDoDemo && (
              <div className="alert alert-info">
                <i className="bi bi-info-circle-fill me-2" />
                <strong>Chế độ DEMO</strong> — cổng thanh toán mô phỏng quy trình thật (QR VietQR chuẩn NAPAS 247). Vui lòng{' '}
                <u>không chuyển tiền thật</u> khi chạy thử.
              </div>
            )}

            <div className="row g-4">
              <div className="col-lg-8">
                <TheNoiDung tieuDe="Chọn phương thức thanh toán" icon="bi-ui-checks">
                  <div className="dvk-pt">
                    {PHUONG_THUC.map((p) => (
                      <label key={p.ma} className={'dvk-pt-mot' + (chon === p.ma ? ' chon' : '')}>
                        <input
                          type="radio"
                          className="form-check-input mt-2"
                          name="phuongThuc"
                          value={p.ma}
                          checked={chon === p.ma}
                          onChange={() => setChon(p.ma)}
                        />
                        <span className="dvk-pt-ico" style={{ background: p.nen }} aria-hidden="true">
                          <i className={'bi ' + p.icon} />
                        </span>
                        <span className="flex-grow-1 min-w-0">
                          <span className="dvk-pt-ten">
                            {p.ten}
                            {p.nhan && <span className="badge text-bg-warning ms-2">{p.nhan}</span>}
                          </span>
                          <span className="dvk-pt-mo d-block">{p.mo}</span>
                          <span>
                            {p.the.map((t) => (
                              <span key={t} className="dvk-the-nho">
                                {t}
                              </span>
                            ))}
                          </span>
                        </span>
                      </label>
                    ))}
                  </div>

                  <button type="button" className="btn btn-primary btn-lg w-100 mt-4" disabled={dangChay} onClick={tiepTuc}>
                    {dangChay ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-arrow-right-circle me-2" />}
                    Tiếp tục thanh toán
                  </button>
                  {d.maTTDangCho && (
                    <div className="form-text">
                      Đơn đang có giao dịch chờ (#{d.maTTDangCho}) — bấm tiếp tục sẽ dùng lại giao dịch này và làm mới thời gian giữ đơn.
                    </div>
                  )}

                  <div className="d-flex flex-wrap gap-3 mt-3 small text-body-secondary">
                    <span>
                      <i className="bi bi-shield-lock-fill me-1" />
                      Mã hóa giao dịch
                    </span>
                    <span>
                      <i className="bi bi-patch-check-fill me-1" />
                      Chuẩn NAPAS 247
                    </span>
                    <span>
                      <i className="bi bi-clock-history me-1" />
                      Đối soát tự động
                    </span>
                  </div>
                </TheNoiDung>
              </div>

              <div className="col-lg-4">
                <TheNoiDung tieuDe="Đơn hàng" icon="bi-receipt">
                  <div className="dvk-hang">
                    <span>Mã booking</span>
                    <strong>{d.maSoBooking}</strong>
                  </div>
                  <div className="dvk-hang">
                    <span>Khách hàng</span>
                    <strong>{d.tenKhach || '—'}</strong>
                  </div>
                  <div className="dvk-hang">
                    <span>Trạng thái đơn</span>
                    <NhanTrangThai giaTri={d.trangThaiDon} />
                  </div>
                  <div className="mt-2">
                    {d.dong.map((c) => (
                      <div key={c.maCT} className="dvk-hang small">
                        <span className="text-truncate">
                          {c.tenDV} × {c.soLuong}
                        </span>
                        <span>{dinhDangTien(c.thanhTien)}</span>
                      </div>
                    ))}
                  </div>
                  <div className="d-flex justify-content-between align-items-center border-top pt-3 mt-3">
                    <span className="text-body-secondary">Tổng thanh toán</span>
                    <span className="fs-4 fw-bold text-warning-emphasis">{dinhDangTien(d.tongTien)}</span>
                  </div>
                  <Link className="btn btn-outline-secondary w-100 mt-3" to={'/booking/details/' + maBooking}>
                    <i className="bi bi-arrow-left me-1" />
                    Quay lại đơn
                  </Link>
                </TheNoiDung>
              </div>
            </div>
          </>
        )}
      </div>
    </>
  )
}
