import { useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { dinhDangNgay } from '../../utils/dinhDang'
import './cong.css'

/**
 * PHIEU RA VAO CONG GHI TAY (thay Admin/Gate/PhieuTay) — ban in du phong khi he thong / mang chet:
 * bao ve van cho xe qua cong, ghi tay vao phieu nay, nhap bu sau.
 * Cam ket voi khach: "he thong hong thi khong ket mot xe nao". In 2 lien: bao ve giu 1, tai xe 1.
 */
export default function PhieuTay() {
  const [thamSo] = useSearchParams()
  const bienSo = (thamSo.get('bienSo') || '').trim().toUpperCase()
  const huongDi = thamSo.get('huongDi') === 'Ra' ? 'Ra' : 'Vào'
  const homNay = dinhDangNgay(new Date())

  useEffect(() => {
    const cu = document.title
    document.title = 'Phiếu ra vào cổng (ghi tay)' + (bienSo ? ' — ' + bienSo : '')
    return () => {
      document.title = cu
    }
  }, [bienSo])

  return (
    <div className="cg-tay" data-bs-theme="light">
      <div className="cg-tay-huongdan">
        <b>Dùng khi nào:</b> hệ thống hoặc mạng chết mà xe vẫn phải qua cổng. Bảo vệ in phiếu này, ghi tay đầy đủ, đưa tài xế 1 liên và giữ lại 1 liên. Khi
        hệ thống chạy lại, nhập bù vào màn hình chốt cổng — <b>không được để kẹt xe vì phần mềm</b>.
      </div>

      {[0, 1].map((i) => (
        <div className="cg-tay-lien" key={i}>
          <div className="cg-tay-dau">
            <div>
              <h1>PHIẾU RA VÀO CỔNG</h1>
              <div className="cg-tay-phu">SmartPort · Liên {i + 1}: {i === 0 ? 'Bảo vệ giữ' : 'Giao tài xế'}</div>
            </div>
            <div className="cg-tay-phu text-end">
              Số phiếu: <span className="cg-tay-o" style={{ minWidth: 90 }} />
              <br />
              Ngày: {homNay}
            </div>
          </div>

          <div className="cg-tay-canhbao">PHIẾU DỰ PHÒNG — GHI TAY KHI HỆ THỐNG GIÁN ĐOẠN</div>

          <table>
            <tbody>
              <tr>
                <td className="nhan">Biển số xe</td>
                <td className="cg-tay-to">
                  {bienSo}
                  <span className="cg-tay-o" />
                </td>
              </tr>
              <tr>
                <td className="nhan">Hướng</td>
                <td>
                  <b>{huongDi === 'Ra' ? '☐ VÀO   ☒ RA' : '☒ VÀO   ☐ RA'}</b>
                </td>
              </tr>
              <tr>
                <td className="nhan">Giờ qua cổng</td>
                <td>
                  <span className="cg-tay-o" style={{ minWidth: 70 }} /> giờ <span className="cg-tay-o" style={{ minWidth: 70 }} /> phút
                </td>
              </tr>
              <tr>
                <td className="nhan">Số container</td>
                <td>
                  <span className="cg-tay-o" />
                </td>
              </tr>
              <tr>
                <td className="nhan">Số lệnh giao nhận</td>
                <td>
                  <span className="cg-tay-o" />
                </td>
              </tr>
              <tr>
                <td className="nhan">Tên tài xế / SĐT</td>
                <td>
                  <span className="cg-tay-o" />
                </td>
              </tr>
              <tr>
                <td className="nhan">Tình trạng vỏ</td>
                <td>
                  ☐ Nguyên vẹn &nbsp;&nbsp; ☐ Có hư hỏng: <span className="cg-tay-o" style={{ minWidth: 100 }} />
                </td>
              </tr>
              <tr>
                <td className="nhan">Ghi chú</td>
                <td>
                  <span className="cg-tay-o" style={{ minWidth: 240 }} />
                </td>
              </tr>
            </tbody>
          </table>

          <div className="cg-tay-ky">
            <div>
              <b>BẢO VỆ CỔNG</b>
              <br />
              (ký, ghi rõ họ tên)
              <div className="cho" />
            </div>
            <div>
              <b>TÀI XẾ</b>
              <br />
              (ký, ghi rõ họ tên)
              <div className="cho" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
