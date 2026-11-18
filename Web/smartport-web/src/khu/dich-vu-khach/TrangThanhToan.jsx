import { Fragment, useEffect, useRef, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import MaQrVietQr from '../tai-chinh/MaQrVietQr'
import DauTrangKhach from './DauTrangKhach'
import './dich-vu-khach.css'

const NHIP_HOI = 6000 // 6 giay — bang nhip polling cua ban MVC

const hai = (n) => String(n).padStart(2, '0')
const dongHo = (giay) => `${hai(Math.floor(Math.max(0, giay) / 60))}:${hai(Math.max(0, giay) % 60)}`

/** Chu bam duoc de sao chep (so tai khoan, so tien, noi dung CK) */
function ChepNhanh({ giaTri, hien, lop = '' }) {
  const [daChep, setDaChep] = useState(false)
  const hen = useRef(null)

  useEffect(() => () => clearTimeout(hen.current), [])

  async function chep() {
    const chuoi = String(giaTri ?? '')
    try {
      if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(chuoi)
      else {
        const o = document.createElement('textarea')
        o.value = chuoi
        o.style.position = 'fixed'
        o.style.opacity = '0'
        document.body.appendChild(o)
        o.select()
        document.execCommand('copy')
        o.remove()
      }
      setDaChep(true)
      clearTimeout(hen.current)
      hen.current = setTimeout(() => setDaChep(false), 1400)
    } catch {
      /* trinh duyet chan clipboard: khach tu boi den chep tay */
    }
  }

  return (
    <button type="button" className={'dvk-chep ' + lop} onClick={chep} title="Bấm để sao chép">
      {daChep ? (
        <span className="text-success">
          <i className="bi bi-check-lg me-1" />
          Đã sao chép
        </span>
      ) : (
        <>
          {hien ?? giaTri}
          <i className="bi bi-clipboard ms-1 small" />
        </>
      )}
    </button>
  )
}

/** Thanh 3 buoc cua cong thanh toan */
function ThanhBuoc({ daXong }) {
  const buoc = ['Chọn phương thức', 'Thanh toán', 'Hoàn tất']
  const dangO = daXong ? 2 : 1
  return (
    <div className="dvk-buoc mb-4">
      {buoc.map((b, i) => (
        <Fragment key={b}>
          <span className={'dvk-buoc-mot' + (i < dangO ? ' xong' : i === dangO ? ' dang' : '')}>
            <span className="dvk-buoc-cham">{i < dangO ? <i className="bi bi-check" /> : i + 1}</span>
            <span>{b}</span>
          </span>
          {i < buoc.length - 1 && <span className="dvk-buoc-vach" />}
        </Fragment>
      ))}
    </div>
  )
}

/** Bieu mau the ngan hang MO PHONG — du lieu chi nam trong trinh duyet, khong gui len may chu. */
function TheMoPhong() {
  const [soThe, setSoThe] = useState('')
  const [chuThe, setChuThe] = useState('')
  const [hetHan, setHetHan] = useState('')

  const soHien = (soThe.replace(/\D/g, '') + '••••••••••••••••').slice(0, 16).replace(/(.{4})/g, '$1 ').trim()

  return (
    <>
      <div className="p-3 mb-3 rounded-4 text-white" style={{ background: 'linear-gradient(135deg,#1e293b,#475569)' }}>
        <div className="mb-3" style={{ width: 40, height: 28, borderRadius: 6, background: 'linear-gradient(135deg,#fde68a,#f59e0b)' }} />
        <div className="fs-5 fw-semibold font-monospace">{soHien}</div>
        <div className="d-flex justify-content-between mt-3 small">
          <div>
            <div className="opacity-75" style={{ fontSize: '.65rem' }}>
              CHỦ THẺ
            </div>
            <div>{(chuThe || 'NGUYEN VAN A').toUpperCase()}</div>
          </div>
          <div className="text-end">
            <div className="opacity-75" style={{ fontSize: '.65rem' }}>
              HẾT HẠN
            </div>
            <div>{hetHan || 'MM/YY'}</div>
          </div>
        </div>
      </div>
      <div className="row g-2">
        <div className="col-12">
          <label className="form-label small fw-semibold mb-1" htmlFor="dvk-so-the">
            Số thẻ
          </label>
          <input
            id="dvk-so-the"
            className="form-control"
            autoComplete="off"
            inputMode="numeric"
            maxLength={19}
            placeholder="0000 0000 0000 0000"
            value={soThe}
            onChange={(e) =>
              setSoThe(
                e.target.value
                  .replace(/\D/g, '')
                  .slice(0, 16)
                  .replace(/(.{4})/g, '$1 ')
                  .trim(),
              )
            }
          />
        </div>
        <div className="col-6">
          <label className="form-label small fw-semibold mb-1" htmlFor="dvk-chu-the">
            Tên chủ thẻ
          </label>
          <input id="dvk-chu-the" className="form-control" autoComplete="off" placeholder="NGUYEN VAN A" value={chuThe} onChange={(e) => setChuThe(e.target.value)} />
        </div>
        <div className="col-6">
          <label className="form-label small fw-semibold mb-1" htmlFor="dvk-het-han">
            Hết hạn
          </label>
          <input
            id="dvk-het-han"
            className="form-control"
            autoComplete="off"
            inputMode="numeric"
            maxLength={5}
            placeholder="MM/YY"
            value={hetHan}
            onChange={(e) => {
              const v = e.target.value.replace(/\D/g, '').slice(0, 4)
              setHetHan(v.length >= 3 ? v.slice(0, 2) + '/' + v.slice(2) : v)
            }}
          />
        </div>
      </div>
      <div className="alert alert-warning small mt-3 mb-0">
        <i className="bi bi-shield-exclamation me-1" />
        Biểu mẫu <strong>mô phỏng</strong> của đồ án: thông tin thẻ chỉ hiển thị trên máy bạn, không gửi đi đâu.{' '}
        <strong>Không nhập số thẻ thật.</strong>
      </div>
    </>
  )
}

const VI = [
  { ma: 'MoMo', ten: 'Ví MoMo', mau: '#ae2070' },
  { ma: 'ZaloPay', ten: 'ZaloPay', mau: '#0068ff' },
  { ma: 'VNPay', ten: 'VNPay', mau: '#005baa' },
]

/** /payment/pay/:id — trang hoan tat thanh toan (thay Views/Payment/Pay.cshtml + Scripts/payment.js). */
export default function TrangThanhToan() {
  const { id } = useParams()
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/thanh-toan-khach/' + id)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const dieuHuong = useNavigate()

  const [conLai, setConLai] = useState(null)
  const [tongGiay, setTongGiay] = useState(1)
  const [trangThai, setTrangThai] = useState(null)
  const [vi, setVi] = useState('MoMo')

  const gd = d?.giaoDich
  const daThanhToan = trangThai === 'Đã thanh toán' || gd?.trangThai === 'Đã thanh toán'
  const coDongHo = !!d?.coDongHo

  // Nap lai dong ho moi khi API tra du lieu moi
  useEffect(() => {
    if (!d) return
    setTrangThai(d.giaoDich.trangThai)
    setConLai(d.giayConLai)
    setTongGiay(Math.max(1, d.cauHinh?.soPhutGiuDon ? d.cauHinh.soPhutGiuDon * 60 : d.giayConLai || 1))
  }, [d])

  // Dem nguoc tai cho (khong goi API moi giay)
  useEffect(() => {
    if (!coDongHo || daThanhToan) return undefined
    const dem = setInterval(() => setConLai((c) => (c == null ? c : Math.max(0, c - 1))), 1000)
    return () => clearInterval(dem)
  }, [coDongHo, daThanhToan])

  // Hoi trang thai dinh ky: ke toan xac nhan xong thi tu nhay sang bien lai.
  // DUNG hoi khi tab bi an hoac khi roi trang (khong quay API cua may chu vo ich).
  useEffect(() => {
    if (!id || daThanhToan) return undefined
    let con = true
    const hoi = async () => {
      if (document.hidden || !con) return
      try {
        const kq = await goiApi(`/api/thanh-toan-khach/${id}/trang-thai`)
        if (!con || !kq?.ok) return
        setTrangThai(kq.trangThai)
        if (kq.coDongHo) setConLai(kq.giayConLai)
        if (kq.daThanhToan) dieuHuong('/payment/receipt/' + id, { replace: true })
      } catch {
        /* mat mang tam thoi: lan hoi sau tu thu lai */
      }
    }
    const nhip = setInterval(hoi, NHIP_HOI)
    document.addEventListener('visibilitychange', hoi)
    return () => {
      con = false
      clearInterval(nhip)
      document.removeEventListener('visibilitychange', hoi)
    }
  }, [id, daThanhToan, dieuHuong])

  if (gd?.trangThai === 'Đã thanh toán') return <Navigate to={'/payment/receipt/' + id} replace />

  async function baoDaTra() {
    const kq = await chay(() => goiApi(`/api/thanh-toan-khach/${id}/xac-nhan`, { method: 'POST' }))
    if (kq) taiLai({ im: true })
  }

  async function huy() {
    const dongY = await xacNhan({
      tieuDe: 'Hủy giao dịch thanh toán?',
      noiDung: `Hủy giao dịch #${id}. Bạn vẫn có thể tạo lại giao dịch mới cho booking ${d?.maSoBooking || ''}.`,
      nutDongY: 'Hủy giao dịch',
      mau: 'danger',
    })
    if (!dongY) return
    const kq = await chay(() => goiApi(`/api/thanh-toan-khach/${id}/huy`, { method: 'POST' }))
    if (kq) dieuHuong('/booking/details/' + (d?.maBooking || ''))
  }

  const ttHien = trangThai || gd?.trangThai
  const dangCho = ttHien === 'Chờ thanh toán' || ttHien === 'Quá hạn'

  return (
    <>
      <DauTrangKhach
        icon="bi-qr-code"
        tieuDe="Hoàn tất thanh toán"
        duongDan={[
          { ten: 'Booking của tôi', den: '/booking/my' },
          { ten: d?.maSoBooking || 'Đơn', den: '/booking/details/' + (d?.maBooking || '') },
          { ten: 'Thanh toán' },
        ]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && !d ? (
          <DangTai chu="Đang mở giao dịch…" />
        ) : !d ? null : (
          <>
            {d.cauHinh?.cheDoDemo && (
              <div className="alert alert-info">
                <i className="bi bi-info-circle-fill me-2" />
                <strong>Chế độ DEMO</strong> — mã QR bên dưới đúng chuẩn VietQR/NAPAS 247. Vui lòng <u>không chuyển tiền thật</u>.
              </div>
            )}

            <ThanhBuoc daXong={daThanhToan} />

            <div className="row g-4">
              <div className="col-lg-7">
                <TheNoiDung
                  tieuDe={
                    gd.phuongThuc === 'ChuyenKhoan'
                      ? 'Quét mã QR để chuyển khoản'
                      : gd.phuongThuc === 'TheNganHang'
                        ? 'Thanh toán bằng thẻ'
                        : gd.phuongThuc === 'ViDienTu'
                          ? 'Thanh toán qua ví điện tử'
                          : 'Thanh toán tiền mặt tại quầy'
                  }
                  icon={
                    gd.phuongThuc === 'ChuyenKhoan'
                      ? 'bi-qr-code-scan'
                      : gd.phuongThuc === 'TheNganHang'
                        ? 'bi-credit-card'
                        : gd.phuongThuc === 'ViDienTu'
                          ? 'bi-wallet2'
                          : 'bi-cash-coin'
                  }
                >
                  {gd.phuongThuc === 'ChuyenKhoan' && (
                    <>
                      <p className="text-body-secondary small">
                        Mở app ngân hàng → chọn <strong>Quét QR</strong> → số tiền và nội dung đã được điền sẵn.
                      </p>
                      <div className="dvk-qr">
                        {d.cauHinh?.coTaiKhoan && (d.qrNoiDung || d.qrUrl) ? (
                          // Ve tai cho tu chuoi VietQR may chu tu sinh (chay ca khi mat Internet); anh qrUrl chi du phong
                          <MaQrVietQr
                            noiDung={d.qrNoiDung}
                            url={d.qrUrl}
                            canh={240}
                            style={{ maxWidth: '62vw' }}
                            moTa={'Mã QR VietQR thanh toán ' + dinhDangTien(gd.soTien)}
                          />
                        ) : (
                          <div className="dvk-qr-trong">
                            <div>
                              <i className="bi bi-qr-code fs-1" />
                              <div className="small mt-2">
                                Chưa cấu hình tài khoản nhận
                                <br />
                                (khai báo <code>AppSettings:BankAccount</code>)
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                      <div className="text-center small text-body-secondary mt-2">
                        Hỗ trợ toàn bộ ngân hàng qua <strong>NAPAS 247 · VietQR</strong>
                      </div>

                      <div className="mt-3">
                        <div className="dvk-hang">
                          <span>Ngân hàng</span>
                          <strong>{d.cauHinh?.tenNganHang}</strong>
                        </div>
                        <div className="dvk-hang">
                          <span>Chủ tài khoản</span>
                          <strong>{d.cauHinh?.chuTaiKhoan}</strong>
                        </div>
                        <div className="dvk-hang">
                          <span>Số tài khoản</span>
                          <ChepNhanh giaTri={d.cauHinh?.soTaiKhoan || ''} />
                        </div>
                        <div className="dvk-hang">
                          <span>Số tiền</span>
                          <ChepNhanh giaTri={Math.round(gd.soTien)} hien={dinhDangTien(gd.soTien)} lop="text-danger" />
                        </div>
                        <div className="dvk-hang">
                          <span>Nội dung CK</span>
                          <ChepNhanh giaTri={gd.noiDungCK || ''} />
                        </div>
                      </div>
                      <div className="small text-body-secondary mt-2">
                        <i className="bi bi-exclamation-triangle me-1" />
                        Giữ nguyên nội dung <strong>{gd.noiDungCK}</strong> để hệ thống đối soát tự động.
                      </div>
                    </>
                  )}

                  {gd.phuongThuc === 'TheNganHang' && <TheMoPhong />}

                  {gd.phuongThuc === 'ViDienTu' && (
                    <>
                      <p className="text-body-secondary small">Chọn ví và xác nhận trên ứng dụng.</p>
                      <div className="row g-2">
                        {VI.map((v) => (
                          <div className="col-4" key={v.ma}>
                            <label className={'dvk-pt-mot flex-column text-center' + (vi === v.ma ? ' chon' : '')}>
                              <input
                                type="radio"
                                className="visually-hidden"
                                name="vi"
                                value={v.ma}
                                checked={vi === v.ma}
                                onChange={() => setVi(v.ma)}
                              />
                              <span className="dvk-pt-ico mx-auto" style={{ background: v.mau }} aria-hidden="true">
                                <i className="bi bi-wallet2" />
                              </span>
                              <span className="dvk-pt-ten small">{v.ten}</span>
                            </label>
                          </div>
                        ))}
                      </div>
                      <div className="alert alert-light border mt-3 mb-0">
                        <i className="bi bi-phone me-2" />
                        Mở app ví trên điện thoại và xác nhận yêu cầu thanh toán <strong>{dinhDangTien(gd.soTien)}</strong>.
                      </div>
                    </>
                  )}

                  {gd.phuongThuc === 'TienMat' && (
                    <>
                      <p className="text-body-secondary small">Vui lòng đến quầy thu ngân của cảng để hoàn tất.</p>
                      <div className="dvk-hang">
                        <span>Địa điểm</span>
                        <span>Quầy thu ngân — Khu bến cảng Cát Lái, TP. Thủ Đức</span>
                      </div>
                      <div className="dvk-hang">
                        <span>Giờ làm việc</span>
                        <span>Thứ 2 – Thứ 7, 07:30 – 17:00</span>
                      </div>
                      <div className="dvk-hang">
                        <span>Mã tham chiếu</span>
                        <strong>{gd.noiDungCK}</strong>
                      </div>
                      <div className="dvk-hang">
                        <span>Số tiền cần thanh toán</span>
                        <strong className="text-danger">{dinhDangTien(gd.soTien)}</strong>
                      </div>
                    </>
                  )}

                  {/* ==== Trang thai + hanh dong ==== */}
                  <div className="mt-4">
                    {ttHien === 'Đã hủy' ? (
                      <div className="alert alert-secondary mb-0">
                        <i className="bi bi-x-circle me-1" />
                        Giao dịch đã hủy.
                        <Link className="btn btn-sm btn-primary ms-2" to={'/payment?id=' + d.maBooking}>
                          Tạo giao dịch mới
                        </Link>
                      </div>
                    ) : (
                      dangCho && (
                        <>
                          {d.daBaoThanhToan && (
                            <div className="alert alert-warning py-2">
                              <i className="bi bi-hourglass-split me-1" />
                              Đã ghi nhận báo có{gd.maGiaoDich ? ' — mã ' + gd.maGiaoDich : ''}. Kế toán đang đối chiếu.
                            </div>
                          )}
                          <button type="button" className="btn btn-primary btn-lg w-100" disabled={dangChay || d.daBaoThanhToan} onClick={baoDaTra}>
                            {dangChay ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-check2-circle me-2" />}
                            {gd.phuongThuc === 'TienMat' ? 'Xác nhận sẽ thanh toán tại quầy' : 'Tôi đã thanh toán'}
                          </button>
                          <button type="button" className="btn btn-outline-danger w-100 mt-2" disabled={dangChay} onClick={huy}>
                            <i className="bi bi-x-circle me-1" />
                            Hủy giao dịch
                          </button>
                        </>
                      )
                    )}
                  </div>
                </TheNoiDung>
              </div>

              <div className="col-lg-5">
                {coDongHo && !daThanhToan && (
                  <TheNoiDung tieuDe="Đơn được giữ trong" icon="bi-stopwatch">
                    <div className={'dvk-dongho' + (conLai != null && conLai <= 60 ? ' sap-het' : '')}>{dongHo(conLai ?? 0)}</div>
                    <div className="dvk-thanh">
                      <span style={{ width: Math.max(0, Math.min(100, ((conLai ?? 0) / tongGiay) * 100)) + '%' }} />
                    </div>
                    <div className="small mt-2">
                      {conLai === 0 ? (
                        <span className="text-danger fw-semibold">Giao dịch đã hết hiệu lực — vui lòng tạo lại.</span>
                      ) : (
                        <span className="text-body-secondary">Quá thời gian, giao dịch sẽ hết hiệu lực và bạn cần tạo lại.</span>
                      )}
                    </div>
                  </TheNoiDung>
                )}

                <TheNoiDung tieuDe="Thông tin giao dịch" icon="bi-info-circle">
                  <div className="dvk-hang">
                    <span>Mã giao dịch</span>
                    <strong>#{gd.maTT}</strong>
                  </div>
                  <div className="dvk-hang">
                    <span>Booking</span>
                    <strong>{d.maSoBooking}</strong>
                  </div>
                  <div className="dvk-hang">
                    <span>Phương thức</span>
                    <strong>{gd.tenPhuongThuc}</strong>
                  </div>
                  <div className="dvk-hang">
                    <span>Trạng thái</span>
                    <NhanTrangThai giaTri={ttHien} mau={ttHien === 'Quá hạn' ? 'do' : undefined} />
                  </div>
                  <div className="dvk-hang">
                    <span>Tạo lúc</span>
                    <strong>{dinhDangNgayGio(gd.ngayTao)}</strong>
                  </div>
                  <div className="d-flex justify-content-between align-items-center border-top pt-3 mt-3">
                    <span className="text-body-secondary">Số tiền</span>
                    <span className="fs-4 fw-bold text-warning-emphasis">{dinhDangTien(gd.soTien)}</span>
                  </div>
                  <div className="small text-body-secondary fst-italic">{d.soTienBangChu}</div>
                </TheNoiDung>

                <TheNoiDung tieuDe="Hướng dẫn nhanh" icon="bi-question-circle">
                  <ol className="ps-3 mb-2 small">
                    <li>Mở ứng dụng ngân hàng / ví trên điện thoại</li>
                    <li>
                      Chọn chức năng <strong>Quét mã QR</strong>
                    </li>
                    <li>Kiểm tra số tiền &amp; nội dung đã điền sẵn</li>
                    <li>
                      Xác nhận → bấm <strong>“Tôi đã thanh toán”</strong>
                    </li>
                  </ol>
                  <div className="small text-body-secondary">
                    Cần hỗ trợ? Hotline <strong>0387 562 579</strong>
                  </div>
                </TheNoiDung>
              </div>
            </div>
          </>
        )}
      </div>
    </>
  )
}
