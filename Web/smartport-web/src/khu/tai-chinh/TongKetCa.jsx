import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo, ngayGioInput } from '../../utils/dinhDang'
import './tai-chinh.css'

const dauNgay = () => ngayGioInput(new Date(new Date().setHours(0, 0, 0, 0)))
const cuoiNgay = () => ngayGioInput(new Date(new Date().setHours(23, 59, 0, 0)))

const gioNgay = (v) => (v ? new Date(v).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '—')

const MAU_HINH_THUC = { 'Tiền mặt': 'bg-success', 'Trừ tài khoản': 'bg-info text-dark' }

/** Bang giao ca thu phi cong — trang in (thay Admin/PhiCong/TongKet cua ban MVC). */
export default function TongKetCa() {
  const [loc, datLoc] = useBoLoc({ tu: '', den: '' })
  const { duLieu: tk, loi, dangTai, taiLai } = useApi('/api/phi-cong/tong-ket' + taoQuery(loc))

  useEffect(() => {
    document.title = 'Bảng giao ca thu phí cổng · SmartPort'
  }, [])

  if (dangTai && !tk) return <DangTai chu="Đang tổng hợp ca thu phí…" />
  if (!tk)
    return (
      <div data-bs-theme="light">
        <BaoLoi loi={loi} thuLai={taiLai} />
        <Link className="btn btn-sm btn-outline-primary" to="/admin/phicong">
          <i className="bi bi-cash-coin me-1" />
          Phí cổng
        </Link>
      </div>
    )

  const ct = tk.chiTiet || []

  return (
    <div data-bs-theme="light" className="tc-giay">
      <div className="tc-thanh-in-trong d-print-none">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/phicong">
          <i className="bi bi-cash-coin me-1" />
          Phí cổng
        </Link>
        <label className="small fw-semibold ms-2" htmlFor="tc-tu">
          Từ
        </label>
        <input
          id="tc-tu"
          type="datetime-local"
          className="form-control form-control-sm"
          style={{ width: 200 }}
          value={loc.tu || ngayGioInput(tk.tu)}
          onChange={(e) => datLoc({ tu: e.target.value || dauNgay() })}
        />
        <label className="small fw-semibold" htmlFor="tc-den">
          đến
        </label>
        <input
          id="tc-den"
          type="datetime-local"
          className="form-control form-control-sm"
          style={{ width: 200 }}
          value={loc.den || ngayGioInput(tk.den)}
          onChange={(e) => datLoc({ den: e.target.value || cuoiNgay() })}
        />
      </div>

      <div className="tc-giay-dau">
        <div>
          <div className="tc-giay-cang">CẢNG QUỐC TẾ SMARTPORT</div>
          <div className="tc-nho">Bảng tổng kết thu phí cổng</div>
        </div>
        <div className="text-end tc-nho">In lúc {dinhDangNgayGio(tk.inLuc)}</div>
      </div>

      <h2 className="tc-giay-ten">BẢNG GIAO CA THU PHÍ CỔNG</h2>
      <div className="tc-nho text-center mb-3">
        Từ <b>{gioNgay(tk.tu)}</b> đến <b>{gioNgay(tk.den)}</b>
      </div>

      <div className="row g-2 mb-3">
        <div className="col-4">
          <div className="tc-o-chi-so-in">
            <b>{dinhDangSo(tk.tienMat)} đ</b>
            <span className="tc-nho">Tiền mặt phải nộp quỹ</span>
          </div>
        </div>
        <div className="col-4">
          <div className="tc-o-chi-so-in">
            <b>{dinhDangSo(tk.tongPhatSinh ?? tk.tongThu)} đ</b>
            <span className="tc-nho">Tổng phát sinh trong ca</span>
          </div>
        </div>
        <div className="col-4">
          <div className="tc-o-chi-so-in">
            <b>{dinhDangSo(tk.soLuot)}</b>
            <span className="tc-nho">Lượt xe qua cổng</span>
          </div>
        </div>
      </div>

      <table className="tc-bang-in mb-3">
        <thead>
          <tr>
            <th>Khoản mục</th>
            <th className="text-center" style={{ width: 110 }}>
              Số lượt
            </th>
            <th className="text-end" style={{ width: 160 }}>
              Số tiền
            </th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>
              <b>Tiền mặt thu tại cổng</b>
              <div className="tc-nho">Thu ngân phải nộp quỹ đúng số này</div>
            </td>
            <td className="text-center">{tk.soLuotTienMat}</td>
            <td className="text-end fw-bold">{dinhDangSo(tk.tienMat)}</td>
          </tr>
          <tr>
            <td>
              Trừ tài khoản trả trước
              <div className="tc-nho">Khách đã nạp từ trước — không nộp quỹ lần nữa</div>
            </td>
            <td className="text-center">{tk.soLuotTruTaiKhoan}</td>
            <td className="text-end">{dinhDangSo(tk.truTaiKhoan)}</td>
          </tr>
          {tk.soLuotCongNo > 0 && (
            <tr>
              <td>
                Công nợ (chờ thu)
                <div className="tc-nho">Lượt ghi trễ lúc trạm cổng mất mạng, chưa ai thu tiền — KHÔNG nộp quỹ, thu khách sau / đối soát phiếu tay</div>
              </td>
              <td className="text-center">{tk.soLuotCongNo}</td>
              <td className="text-end">{dinhDangSo(tk.congNo)}</td>
            </tr>
          )}
          <tr>
            <td>Miễn phí do thẻ tháng</td>
            <td className="text-center">{tk.luotTheThang}</td>
            <td className="text-end tc-nho">0</td>
          </tr>
        </tbody>
        <tfoot>
          <tr className="tc-phu">
            <td className="text-end" colSpan={2}>
              TỔNG PHÁT SINH
            </td>
            <td className="text-end">{dinhDangSo(tk.tongPhatSinh ?? tk.tongThu)} đ</td>
          </tr>
          <tr className="tc-tong">
            <td className="text-end" colSpan={2}>
              TIỀN MẶT PHẢI NỘP QUỸ
            </td>
            <td className="text-end">{dinhDangSo(tk.tienMat)} đ</td>
          </tr>
        </tfoot>
      </table>

      {tk.theoLoaiXe?.length > 0 && (
        <>
          <div className="fw-bold mb-1">Chi tiết theo loại xe</div>
          <table className="tc-bang-in mb-3">
            <thead>
              <tr>
                <th>Loại xe</th>
                <th className="text-center" style={{ width: 110 }}>
                  Lượt
                </th>
                <th className="text-end" style={{ width: 160 }}>
                  Thành tiền
                </th>
              </tr>
            </thead>
            <tbody>
              {tk.theoLoaiXe.map((x) => (
                <tr key={x.loaiXe}>
                  <td>{x.loaiXe}</td>
                  <td className="text-center">{x.soLuot}</td>
                  <td className="text-end">{dinhDangSo(x.thanhTien)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <div className="tc-ky">
        <div>
          <div className="fw-bold">THU NGÂN GIAO CA</div>
          <div className="tc-cho-ky" />
          <div className="tc-nho">(Ký, ghi rõ họ tên)</div>
        </div>
        <div>
          <div className="fw-bold">THU NGÂN NHẬN CA</div>
          <div className="tc-cho-ky" />
          <div className="tc-nho">(Ký, ghi rõ họ tên)</div>
        </div>
        <div>
          <div className="fw-bold">KẾ TOÁN</div>
          <div className="tc-cho-ky" />
          <div className="tc-nho">(Ký, ghi rõ họ tên)</div>
        </div>
      </div>

      <div className="d-print-none mt-4">
        <div className="fw-bold mb-2">Chi tiết từng lượt ({ct.length})</div>
        {ct.length === 0 ? (
          <div className="tc-nho">Không có lượt thu phí nào trong khoảng này.</div>
        ) : (
          <div className="table-responsive" style={{ maxHeight: 520 }}>
            <table className="table table-sm align-middle mb-0">
              <thead>
                <tr>
                  <th scope="col">Thời gian</th>
                  <th scope="col">Biển số</th>
                  <th scope="col">Loại xe</th>
                  <th scope="col">Cổng</th>
                  <th scope="col">Hình thức</th>
                  <th scope="col" className="text-end">
                    Số tiền
                  </th>
                </tr>
              </thead>
              <tbody>
                {ct.map((x) => (
                  <tr key={x.maXV}>
                    <td className="small text-muted text-nowrap">{gioNgay(x.thoiGian)}</td>
                    <td>
                      {/* Dia chi ho so xe giu nguyen ban MVC (Admin/Gate/HoSoXe) — khu Cong dang ky man hinh nay */}
                      <Link className="sp-bien-so text-decoration-none" to={'/admin/gate/hosoxe?bienSo=' + encodeURIComponent(x.bienSo || '')}>
                        {x.bienSo}
                      </Link>
                    </td>
                    <td className="small">{x.loaiXe || '—'}</td>
                    <td className="small">{x.cong || '—'}</td>
                    <td>
                      <span className={'badge ' + (MAU_HINH_THUC[x.hinhThucThu] || 'bg-primary')}>{x.hinhThucThu}</span>
                    </td>
                    <td className="text-end fw-semibold text-nowrap">{dinhDangSo(x.phiCong)} đ</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {ct.length >= 300 && <div className="tc-nho mt-1">Chỉ hiện 300 lượt gần nhất — thu hẹp khoảng thời gian để xem đủ.</div>}
      </div>
    </div>
  )
}
