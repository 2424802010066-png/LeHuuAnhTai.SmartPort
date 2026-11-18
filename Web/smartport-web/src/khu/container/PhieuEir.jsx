import { useEffect, useRef } from 'react'
import { useParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import { API_CONT, soDo } from './chung'
import './container.css'

/**
 * Phieu giao nhan container EIR (Equipment Interchange Receipt) — ban in (khung "in": nut In / Quay lai).
 * Mo phieu = ghi so mot lan in (nhat ky + so chung tu chuoi bam), giong ban MVC.
 */
export default function PhieuEir() {
  const { id } = useParams()
  const { duLieu: p, loi, dangTai, taiLai } = useApi(`${API_CONT}/${id}/eir`)
  const daGhiSo = useRef('')

  // Ghi so chung tu MOT lan cho moi phieu (StrictMode chay effect 2 lan -> chan bang ref)
  useEffect(() => {
    if (!p || daGhiSo.current === id) return
    daGhiSo.current = id
    goiApi(`${API_CONT}/${id}/eir`, { method: 'POST' }).catch(() => {
      /* ghi so loi khong chan viec in phieu */
    })
  }, [p, id])

  useEffect(() => {
    if (!p) return undefined
    const cu = document.title
    // Ten tai lieu = ten file PDF khi chon "Luu thanh PDF" trong hop thoai in
    document.title = `${p.soPhieu} — Phiếu giao nhận container ${p.soCont}`
    return () => {
      document.title = cu
    }
  }, [p])

  if (!p) {
    return (
      <div data-bs-theme="light">
        {loi ? <BaoLoi loi={loi} thuLai={taiLai} /> : dangTai ? <DangTai chu="Đang chuẩn bị phiếu EIR…" /> : null}
      </div>
    )
  }

  const dh = p.dongHo
  const loaiTinhTrang = [p.loaiCont, p.tinhTrang].filter(Boolean).join(' · ') + (p.nhietDo != null ? ` · Nhiệt độ cài đặt ${soDo(p.nhietDo)}°C` : '')

  return (
    <div className="cb-eir" data-bs-theme="light">
      <div className="cb-eir-dau">
        <div>
          <h1>
            <i className="bi bi-box-seam-fill me-1" />
            CẢNG QUỐC TẾ SMARTPORT
          </h1>
          <div className="cb-eir-ct">
            Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM
            <br />
            Hotline: 0387 562 579 · smartport.vn
          </div>
        </div>
        <div className="cb-eir-so">
          Số phiếu / No.:
          <b>{p.soPhieu}</b>
          Ngày in: {dinhDangNgayGio(p.ngayIn)}
        </div>
      </div>

      <h2>PHIẾU GIAO NHẬN CONTAINER</h2>
      <div className="cb-eir-en">EQUIPMENT INTERCHANGE RECEIPT (EIR)</div>

      <table>
        <tbody>
          <tr>
            <th>Số container / Container No.</th>
            <td className="cb-eir-cont">{p.soCont}</td>
          </tr>
          <tr>
            <th>Loại — Tình trạng / Type — Status</th>
            <td>{loaiTinhTrang}</td>
          </tr>
          <tr>
            <th>Hàng hóa / Cargo</th>
            <td>
              {p.hangHoa || '—'}
              {p.trongLuong != null && ` · ${dinhDangSo(p.trongLuong, 2)} tấn`}
            </td>
          </tr>
          <tr>
            <th>Khách hàng / Consignee</th>
            <td>{p.khachHang ? p.khachHang + (p.maSoThue ? ` — MST ${p.maSoThue}` : '') : '—'}</td>
          </tr>
          <tr>
            <th>Chuyến nhập / Inbound voyage</th>
            <td>{p.chuyenNhap || '—'}</td>
          </tr>
          <tr>
            <th>Vị trí bãi / Yard position</th>
            <td>{p.viTriBai || '—'}</td>
          </tr>
          <tr>
            <th>Vào bãi / Gate-in</th>
            <td>{p.ngayVaoBai ? dinhDangNgayGio(p.ngayVaoBai) : '—'}</td>
          </tr>
          <tr>
            <th>Ra bãi / Gate-out</th>
            <td>{p.ngayRaBai ? dinhDangNgayGio(p.ngayRaBai) : '(chưa gate-out)'}</td>
          </tr>
          <tr>
            <th>Trạng thái / Status</th>
            <td>
              <b>{p.trangThai}</b>
            </td>
          </tr>
        </tbody>
      </table>

      <table>
        <tbody>
          <tr>
            <th>Phí lưu bãi / Storage charge</th>
            <td>
              {dh.tongNgay} ngày lưu bãi — miễn phí {dh.ngayMienPhi} ngày đầu → tính phí <b>{dh.ngayTinhPhi} ngày</b> × {dinhDangTien(dh.donGiaNgay)} ={' '}
              <b>{dinhDangTien(dh.phiLuuBai)}</b>
            </td>
          </tr>
          {dh.phiDien > 0 && (
            <tr>
              <th>Điện lạnh / Reefer power</th>
              <td>
                {dh.soNgayDien} ngày × {dinhDangTien(dh.donGiaDien)} = <b>{dinhDangTien(dh.phiDien)}</b>
              </td>
            </tr>
          )}
          <tr className="cb-eir-tien">
            <td colSpan={2}>
              TỔNG PHÍ LƯU BÃI{dh.phiDien > 0 ? ' + ĐIỆN LẠNH' : ''}
              {p.ngayRaBai ? '' : ' (TẠM TÍNH)'}: {dinhDangTien(dh.tongTien)}
            </td>
          </tr>
        </tbody>
      </table>

      <div className="cb-eir-ky">
        <div>
          <b>
            ĐẠI DIỆN CẢNG
            <br />
            <em>(Terminal)</em>
          </b>
          <em>Ký, ghi rõ họ tên</em>
        </div>
        <div>
          <b>
            TÀI XẾ / VẬN TẢI
            <br />
            <em>(Trucker)</em>
          </b>
          <em>Ký, ghi rõ họ tên</em>
        </div>
        <div>
          <b>
            KHÁCH HÀNG
            <br />
            <em>(Consignee)</em>
          </b>
          <em>Ký, ghi rõ họ tên</em>
        </div>
      </div>

      <div className="cb-eir-chan">
        <span>Phiếu in từ hệ thống SmartPort — có giá trị đối chiếu giao nhận tại cổng.</span>
        <span>
          Mã tra cứu: {p.soCont} · {p.soPhieu}
        </span>
      </div>
    </div>
  )
}
