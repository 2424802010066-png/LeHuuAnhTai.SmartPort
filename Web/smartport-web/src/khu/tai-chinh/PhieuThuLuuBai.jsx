import { useEffect } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import MaQrVietQr from './MaQrVietQr'
import './tai-chinh.css'

/** Phieu thu phi luu bai song ngu kem QR VietQR — trang in (thay Admin/LuuBai/Phieu cua ban MVC). */
export default function PhieuThuLuuBai() {
  const { id } = useParams()
  const { duLieu: p, loi, dangTai, taiLai } = useApi('/api/luu-bai/phieu/' + id)

  useEffect(() => {
    if (p) document.title = 'Phiếu thu ' + p.soPhieu + ' · SmartPort'
  }, [p])

  if (dangTai && !p) return <DangTai chu="Đang tải phiếu thu…" />
  if (!p)
    return (
      <div data-bs-theme="light">
        <BaoLoi loi={loi} thuLai={taiLai} />
        <Link className="btn btn-sm btn-outline-primary" to="/admin/luubai/danhsach">
          <i className="bi bi-journal-text me-1" />
          Sổ phiếu
        </Link>
      </div>
    )

  const daHuy = p.trangThai === 'Hủy'
  const daThu = p.trangThai === 'Đã thu'

  return (
    <div data-bs-theme="light" className="tc-giay">
      <div className="tc-thanh-in-trong d-print-none">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/luubai/danhsach">
          <i className="bi bi-journal-text me-1" />
          Sổ phiếu
        </Link>
        <b className="tc-ma ms-1">{p.soPhieu}</b>
        <NhanTrangThai giaTri={p.trangThai} />
        <span className="flex-grow-1" />
        {p.maCont && (
          <Link className="btn btn-sm btn-outline-secondary" to={'/admin/containers/hoso/' + p.maCont}>
            <i className="bi bi-box-seam me-1" />
            Hồ sơ container
          </Link>
        )}
      </div>

      {daHuy && <div className="tc-dau-an huy">ĐÃ HỦY</div>}
      {daThu && <div className="tc-dau-an da-thu">ĐÃ THU</div>}

      <div className="tc-giay-dau">
        <div>
          <div className="tc-giay-cang">CẢNG QUỐC TẾ SMARTPORT</div>
          <div className="tc-nho">Hệ thống điều hành cảng &amp; depot container</div>
        </div>
        <div className="text-end">
          <div className="tc-giay-so">{p.soPhieu}</div>
          <div className="tc-nho">Lập lúc {dinhDangNgayGio(p.ngayLap)}</div>
        </div>
      </div>

      <h2 className="tc-giay-ten">PHIẾU THU PHÍ LƯU BÃI</h2>
      <div className="tc-nho text-center mb-3">STORAGE CHARGE NOTE</div>

      <table className="tc-bang-tt">
        <tbody>
          <tr>
            <td className="tc-nhan">
              Khách hàng
              <br />
              <span className="tc-en">Customer</span>
            </td>
            <td>
              <b>{p.tenKhach || '—'}</b>
              {p.maSoThue && <div className="tc-nho">MST: {p.maSoThue}</div>}
              {p.diaChi && <div className="tc-nho">{p.diaChi}</div>}
            </td>
            <td className="tc-nhan">
              Số container
              <br />
              <span className="tc-en">Container No.</span>
            </td>
            <td>
              <b className="tc-ma" style={{ fontSize: '1.05rem' }}>
                {p.soCont}
              </b>
            </td>
          </tr>
          <tr>
            <td className="tc-nhan">
              Hãng tàu
              <br />
              <span className="tc-en">Shipping line</span>
            </td>
            <td>{p.tenHangTau || '—'}</td>
            <td className="tc-nhan">
              Kỳ tính phí
              <br />
              <span className="tc-en">Period</span>
            </td>
            <td>
              {dinhDangNgay(p.tuNgay)} → {dinhDangNgay(p.denNgay)}
            </td>
          </tr>
        </tbody>
      </table>

      <table className="tc-bang-in">
        <thead>
          <tr>
            <th style={{ width: 38 }}>TT</th>
            <th>
              Khoản mục <span className="fw-normal">/ Description</span>
            </th>
            <th className="text-center" style={{ width: 90 }}>
              Số ngày
            </th>
            <th className="text-end" style={{ width: 110 }}>
              Đơn giá
            </th>
            <th className="text-end" style={{ width: 130 }}>
              Thành tiền
            </th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td className="text-center">1</td>
            <td>
              Phí lưu bãi container
              <div className="tc-nho">
                Lưu {p.tongNgay} ngày, miễn phí {p.ngayMienPhi} ngày đầu → tính phí {p.ngayTinhPhi} ngày
              </div>
            </td>
            <td className="text-center">{p.ngayTinhPhi}</td>
            <td className="text-end">{dinhDangSo(p.donGiaNgay)}</td>
            <td className="text-end fw-bold">{dinhDangSo(p.phiLuuBai)}</td>
          </tr>
          {p.phiDien > 0 && (
            <tr>
              <td className="text-center">2</td>
              <td>
                Phí cắm điện &amp; giám sát container lạnh
                <div className="tc-nho">Tính từ ngày đầu tiên, không áp dụng miễn phí</div>
              </td>
              <td className="text-center">{p.soNgayDien}</td>
              <td className="text-end">{dinhDangSo(p.donGiaDien)}</td>
              <td className="text-end fw-bold">{dinhDangSo(p.phiDien)}</td>
            </tr>
          )}
        </tbody>
        <tfoot>
          <tr className="tc-tong">
            <td colSpan={4} className="text-end">
              TỔNG CỘNG <span className="fw-normal fst-italic">/ TOTAL</span>
            </td>
            <td className="text-end text-nowrap">{dinhDangSo(p.tongTien)} đ</td>
          </tr>
        </tfoot>
      </table>

      <div className="mt-2">
        Bằng chữ: <i>{p.tienBangChu}</i>
      </div>

      {daHuy && p.ghiChu && <div className="tc-nho mt-1 text-danger">{p.ghiChu}</div>}
      {daThu && (
        <div className="tc-nho mt-1">
          Đã thu lúc {dinhDangNgayGio(p.ngayThu)} · {p.hinhThucThu}
        </div>
      )}

      <div className="tc-duoi">
        <div className="flex-grow-1">
          <div className="fw-bold mb-1">THANH TOÁN CHUYỂN KHOẢN</div>
          {p.coTaiKhoan ? (
            <>
              <div className="tc-nho">{p.nganHang}</div>
              <div className="tc-nho">
                Nội dung: <b>{p.noiDungChuyenKhoan}</b>
              </div>
              <div className="tc-nho mt-2">Quét mã QR — số tiền và nội dung đã điền sẵn.</div>
            </>
          ) : (
            <div className="tc-nho">Chưa cấu hình tài khoản ngân hàng trong hệ thống.</div>
          )}
        </div>
        {(p.qrNoiDung || p.qrUrl) && !daHuy && (
          <div className="tc-qr">
            {/* Ve tai cho tu chuoi VietQR may chu tu sinh -> in phieu duoc ca khi mat Internet; anh qrUrl chi du phong */}
            <MaQrVietQr
              noiDung={p.qrNoiDung}
              url={p.qrUrl}
              canh={132}
              style={{ width: 132, height: 132 }}
              moTa={`Mã QR chuyển khoản ${dinhDangSo(p.tongTien)} đồng`}
            />
            <div className="tc-nho text-center">VietQR · NAPAS 247</div>
          </div>
        )}
      </div>

      <div className="tc-ky">
        <div>
          <div className="fw-bold">NGƯỜI LẬP PHIẾU</div>
          <div className="tc-en">Prepared by</div>
          <div className="tc-cho-ky" />
          <div>{p.nguoiLap}</div>
        </div>
        <div>
          <div className="fw-bold">KHÁCH HÀNG</div>
          <div className="tc-en">Customer</div>
          <div className="tc-cho-ky" />
          <div className="tc-nho">(Ký, ghi rõ họ tên)</div>
        </div>
      </div>

      <div className="tc-nho mt-3" style={{ borderTop: '1px dashed #cbd5e1', paddingTop: 8 }}>
        Phiếu lập tự động từ dữ liệu cổng và bãi. Số tiền <b>chốt tại thời điểm lập</b>, không đổi theo dữ liệu phát sinh sau.
      </div>
    </div>
  )
}
