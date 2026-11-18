import { useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgayGio } from '../../utils/dinhDang'
import VeMaQr from './VeMaQr'
import { khungGio } from './dungChung'
import './cong.css'

/**
 * BAN IN LENH GIAO NHAN (Electronic Gate Pass) — thay Admin/GatePass/In.
 * Tai xe cam to nay den cong: co SO LENH + MA PIN + ma QR de check-in online truoc khi toi cang.
 */
export default function InLenh() {
  const { id } = useParams()
  const { duLieu: l, loi, dangTai, taiLai } = useApi('/api/lenh-giao-nhan/' + id)

  useEffect(() => {
    if (!l) return undefined
    const cu = document.title
    // Ten tai lieu = ten file PDF khi chon "Luu thanh PDF" trong hop thoai in
    document.title = `${l.soLenh} — Lệnh giao nhận ${l.bienSoXe}`
    return () => {
      document.title = cu
    }
  }, [l])

  if (!l) {
    return <div data-bs-theme="light">{loi ? <BaoLoi loi={loi} thuLai={taiLai} /> : dangTai ? <DangTai chu="Đang chuẩn bị bản in lệnh…" /> : null}</div>
  }

  // Dia chi check-in cua tai xe giu nguyen ban MVC (/TaiXe) — ma QR in ra van dung khi trang cong khai len song
  const duongCheckIn = `${window.location.origin}/taixe?soLenh=${encodeURIComponent(l.soLenh)}&bienSo=${encodeURIComponent(l.bienSoXe)}`

  return (
    <div className="cg-in" data-bs-theme="light">
      <div className="cg-in-dau">
        <div>
          <h1>
            <i className="bi bi-box-seam-fill me-1" />
            CẢNG QUỐC TẾ SMARTPORT
          </h1>
          <div className="cg-in-ct">
            Khu bến cảng Cát Lái, TP. Thủ Đức · Hotline 0387 562 579
            <br />
            smartport.vn
          </div>
        </div>
        <div className="cg-in-so">
          Số lệnh / No.:
          <b>{l.soLenh}</b>
        </div>
      </div>

      <h2>LỆNH GIAO NHẬN CONTAINER</h2>
      <div className="cg-in-en">ELECTRONIC GATE PASS — xuất trình khi vào cổng cảng</div>

      <div className="cg-in-pin">
        <span>MÃ PIN XÁC THỰC TẠI CỔNG</span>
        <b>{l.maPin}</b>
      </div>

      <div className="cg-in-qr">
        <VeMaQr chu={duongCheckIn} canh={108} moTa={'Mã QR check-in lệnh ' + l.soLenh} />
        <div className="cg-in-qr-chu">
          <b>Quét mã để CHECK-IN ONLINE trước khi đến cảng.</b>
          <br />
          Khai báo trước giúp xe qua cổng nhanh hơn và được ưu tiên làn.
          <br />
          Hoặc vào <b>smartport.vn/TaiXe</b>, nhập số lệnh <b>{l.soLenh}</b> + mã PIN.
        </div>
      </div>

      <table>
        <tbody>
          <tr>
            <th>Loại lệnh / Type</th>
            <td>
              <b>{l.tenLoaiLenh}</b>
            </td>
          </tr>
          <tr>
            <th>Biển số xe / Truck No.</th>
            <td className="cg-in-lon">{l.bienSoXe}</td>
          </tr>
          <tr>
            <th>Số container / Container No.</th>
            <td className="cg-ma">{l.soCont || '—'}</td>
          </tr>
          <tr>
            <th>Tài xế / Driver</th>
            <td>{[l.tenTaiXe, l.sdtTaiXe].filter(Boolean).join(' · ') || '—'}</td>
          </tr>
          <tr>
            <th>Khách hàng / Customer</th>
            <td>{l.khachHang || '—'}</td>
          </tr>
          <tr>
            <th>Khung giờ hẹn / Time slot</th>
            <td>
              <b>{khungGio(l.henTuLuc, l.henDenLuc)}</b>
            </td>
          </tr>
          <tr>
            <th>Trạng thái / Status</th>
            <td>
              {l.trangThai}
              {l.daCheckIn && ' — đã check-in online'}
            </td>
          </tr>
          {l.ghiChu && (
            <tr>
              <th>Ghi chú / Note</th>
              <td>{l.ghiChu}</td>
            </tr>
          )}
        </tbody>
      </table>

      <div className="cg-in-luuy">
        • Tài xế <b>check-in online</b> trước khi đến cảng (quét mã QR ở trên hoặc vào smartport.vn/TaiXe).
        <br />• Xe đến cổng trong khung giờ hẹn — camera ALPR tự đối chiếu biển số với lệnh.
        <br />• Quá giờ hẹn lệnh tự chuyển &quot;Quá hạn&quot;, cần liên hệ điều độ gia hạn.
        <br />• Lệnh phát hành {dinhDangNgayGio(l.ngayTao)} · tra cứu bằng số lệnh trên hệ thống SmartPort.
      </div>
    </div>
  )
}
