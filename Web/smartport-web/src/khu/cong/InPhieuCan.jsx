import { useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import './cong.css'

/**
 * BAN IN PHIEU CAN XE (weighbridge ticket) phuc vu khai VGM theo cong uoc SOLAS —
 * thay Admin/Gate/InPhieuCan. Khung "in" tu co nut In / Quay lai.
 */
export default function InPhieuCan() {
  const { id } = useParams()
  const { duLieu: p, loi, dangTai, taiLai } = useApi('/api/tram-can/' + id)

  useEffect(() => {
    if (!p) return undefined
    const cu = document.title
    // Ten tai lieu = ten file PDF khi chon "Luu thanh PDF" trong hop thoai in
    document.title = `${p.soPhieu} — Phiếu cân xe ${p.bienSoXe}`
    return () => {
      document.title = cu
    }
  }, [p])

  if (!p) {
    return <div data-bs-theme="light">{loi ? <BaoLoi loi={loi} thuLai={taiLai} /> : dangTai ? <DangTai chu="Đang chuẩn bị phiếu cân…" /> : null}</div>
  }

  return (
    <div className="cg-in" data-bs-theme="light">
      <div className="cg-in-dau">
        <div>
          <h1>
            <i className="bi bi-box-seam-fill me-1" />
            CẢNG QUỐC TẾ SMARTPORT
          </h1>
          <div className="cg-in-ct">
            Trạm cân điện tử · Khu bến cảng Cát Lái, TP. Thủ Đức
            <br />
            Hotline: 0387 562 579 · smartport.vn
          </div>
        </div>
        <div className="cg-in-so">
          Số phiếu / No.:
          <b>{p.soPhieu}</b>
        </div>
      </div>

      <h2>PHIẾU CÂN XE</h2>
      <div className="cg-in-en">WEIGHBRIDGE TICKET — phục vụ khai VGM (SOLAS)</div>

      <table>
        <tbody>
          <tr>
            <th>Biển số xe / Truck No.</th>
            <td className="cg-in-lon">{p.bienSoXe}</td>
          </tr>
          <tr>
            <th>Số container / Container No.</th>
            <td className="cg-ma">{p.soCont || '—'}</td>
          </tr>
          <tr>
            <th>Cân lần 1 (kg) / 1st weighing</th>
            <td>
              {dinhDangSo(p.canLan1, 1)} — lúc {dinhDangNgayGio(p.luc1)}
            </td>
          </tr>
          <tr>
            <th>Cân lần 2 (kg) / 2nd weighing</th>
            <td>{p.canLan2 != null ? `${dinhDangSo(p.canLan2, 1)} — lúc ${dinhDangNgayGio(p.luc2)}` : '(chưa cân)'}</td>
          </tr>
          <tr className="cg-in-tong">
            <th>KHỐI LƯỢNG HÀNG / Cargo weight</th>
            <td>{p.khoiLuongHang != null ? dinhDangSo(p.khoiLuongHang, 1) + ' kg' : '— chưa chốt —'}</td>
          </tr>
          <tr>
            <th>Người cân / Operator</th>
            <td>{p.nguoiCan || '—'}</td>
          </tr>
          {p.ghiChu && (
            <tr>
              <th>Ghi chú / Note</th>
              <td>{p.ghiChu}</td>
            </tr>
          )}
        </tbody>
      </table>

      <div className="cg-in-ky">
        <div>
          <b>NHÂN VIÊN TRẠM CÂN</b>
          <em>Ký, ghi rõ họ tên</em>
        </div>
        <div>
          <b>TÀI XẾ</b>
          <em>Ký, ghi rõ họ tên</em>
        </div>
      </div>

      <div className="cg-in-luuy">
        • Phiếu in từ hệ thống SmartPort — có giá trị đối chiếu khối lượng tại cảng.
        <br />• Mã tra cứu: {p.soPhieu} · xe {p.bienSoXe}
        {p.soCont ? ' · cont ' + p.soCont : ''}.
      </div>
    </div>
  )
}
