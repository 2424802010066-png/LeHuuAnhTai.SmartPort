import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { API_URL } from '../../api/client'
import { TheNoiDung } from '../../components/ui'
import './he-thong.css'

/**
 * Tai lieu API tich hop (thay Admin/TichHop/TaiLieu) — gui link nay cho ky thuat vien ben khach,
 * hoac bam "In / xuat PDF" de gui file. Dia chi goc lay theo noi API dang phuc vu, khong go cung.
 */
const GOC = (API_URL || window.location.origin).replace(/\/$/, '')

/** Mot endpoint trong muc 2 */
function Endpoint({ verb, duong, canGhi, children }) {
  return (
    <div className="ht-ep">
      <div>
        <span className={'ht-verb ' + (verb === 'GET' ? 'get' : 'post')}>{verb}</span>
        <code className="ht-ep-duong">
          {GOC}
          {duong}
        </code>
        {canGhi && <span className="badge bg-warning text-dark ms-2 align-middle">cần quyền Ghi</span>}
      </div>
      {children}
    </div>
  )
}

const Ma = ({ json, children }) => <pre className={'ht-code' + (json ? ' ht-json' : '')}>{children}</pre>

export default function TaiLieuTichHop() {
  // Chi trang nay moi an menu / thanh tren khi in (xem @media print trong he-thong.css)
  useEffect(() => {
    document.body.classList.add('ht-in-tai-lieu')
    return () => document.body.classList.remove('ht-in-tai-lieu')
  }, [])

  return (
    <div className="ht-tai-lieu">
      <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3 ht-an-khi-in">
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/tichhop">
          <i className="bi bi-arrow-left me-1" />
          Quản lý khóa
        </Link>
        <button type="button" className="btn btn-sm btn-primary" onClick={() => window.print()}>
          <i className="bi bi-printer me-1" />
          In / xuất PDF gửi khách
        </button>
      </div>

      <TheNoiDung tieuDe="1. Xác thực" icon="bi-shield-lock">
        <p className="mb-2 small">
          Mọi request đều gửi kèm khóa ở header. Không có khóa hoặc khóa đã thu hồi → HTTP <code>401</code>.
        </p>
        <Ma>X-API-Key: sp_xxxxxxxxxxxxxxxxxxxxxxxxxxxx</Ma>
        <div className="small text-body-secondary">
          Khi thử nhanh trên trình duyệt có thể dùng <code>?key=sp_...</code>, nhưng khi chạy thật hãy dùng header (URL bị ghi vào log máy chủ).
        </div>
      </TheNoiDung>

      <TheNoiDung tieuDe="2. Các endpoint" icon="bi-plug">
        <Endpoint verb="GET" duong="/api/v1/ping">
          <div className="small text-body-secondary">Kiểm tra khóa còn sống không. Nên gọi đầu tiên khi đấu nối.</div>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/container/{số container}">
          <div className="small text-body-secondary">
            Trạng thái container + <b>dòng thời gian đầy đủ</b> (qua cổng, cân, sửa vỏ, nhiệt độ, chứng từ).
          </div>
          <Ma>{`curl -H "X-API-Key: sp_..." ${GOC}/api/v1/container/MSCU2277456`}</Ma>
          <Ma json>{`{
  "ok": true,
  "container": {
    "soCont": "MSCU2277456", "loai": "40RF", "tinhTrang": "Full",
    "trangThai": "Trong bãi", "viTriBai": "R-02-3",
    "ngayVaoBai": "2026-07-15T00:00:00", "soNgayLuuBai": 51
  },
  "dongThoiGian": [
    { "luc": "...", "nhom": "Cổng", "tieuDe": "Xe 51C-638.72 vào cổng A1", "moTa": "..." }
  ]
}`}</Ma>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/ton-bai?loai=40HC&tinhTrang=Rỗng&trang=1&moiTrang=100">
          <div className="small text-body-secondary">
            Toàn bộ container đang trong bãi, có phân trang (tối đa 500 dòng/lần). Dùng để đối chiếu tồn kho hằng đêm.
          </div>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/xe/{biển số}?soNgay=30">
          <div className="small text-body-secondary">
            Lịch sử ra vào của một đầu kéo — nhà xe dùng để đối soát chuyến. Khóa bị giới hạn theo khách hàng thì không gọi được endpoint này.
          </div>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/lenh/{số lệnh}">
          <div className="small text-body-secondary">Xe đã vào cổng chưa, đã hoàn tất chưa — thay cho việc gọi điện hỏi điều độ.</div>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/cong-no?trangThai=Chờ thu">
          <div className="small text-body-secondary">
            <b>Phần mềm kế toán kéo về mỗi đêm</b> — toàn bộ phiếu thu phí lưu bãi, kèm tổng tiền và số còn nợ. Khỏi phải gõ tay từng phiếu.
          </div>
          <Ma json>{`{
  "ok": true, "tong": 3, "tongTien": 13230000, "conNo": 6960000,
  "items": [
    { "soPhieu": "PLB-2609-0002", "soCont": "HLCU7705888",
      "khachHang": "...", "maSoThue": "...",
      "tuNgay": "2026-07-02T00:00:00", "denNgay": "2026-09-04T22:50:00",
      "tongNgay": 65, "ngayTinhPhi": 60,
      "phiLuuBai": 3600000, "phiDien": 0, "tongTien": 3600000,
      "trangThai": "Chờ thu", "soNgayTreo": 0 }
  ]
}`}</Ma>
        </Endpoint>

        <Endpoint verb="POST" duong="/api/v1/dem-xe/xung" canGhi>
          <div className="small text-body-secondary">
            <b>Dành cho thiết bị đếm xe</b> (cảm biến vòng từ dưới mặt đường, bộ đếm gắn trên barie). Mỗi xe đi qua thì gửi một xung. Gửi được cả lô
            tối đa 500 xung để tiết kiệm pin/sóng. <code>maXung</code> do thiết bị tự sinh — hệ thống dùng nó để <b>chống nhận trùng</b> khi mạng
            chập chờn.
          </div>
          <Ma>{`curl -X POST ${GOC}/api/v1/dem-xe/xung \\
  -H "X-API-Key: sp_..." -H "Content-Type: application/json" \\
  -d '{"items":[
        {"cong":"A1","huongDi":"Vào","thoiGian":"2026-09-05T08:15:03","thietBi":"LOOP-A1","maXung":"A1-000123"},
        {"cong":"A1","huongDi":"Ra","thoiGian":"2026-09-05T08:17:41","thietBi":"LOOP-A1","maXung":"A1-000124"}
      ]}'`}</Ma>
          <Ma json>{`{ "ok": true, "nhan": 2, "trung": 0, "hong": 0 }`}</Ma>
        </Endpoint>

        <Endpoint verb="POST" duong="/api/v1/dem-xe/chot" canGhi>
          <div className="small text-body-secondary">
            Chốt một kỳ đối soát (cuối ca).{' '}
            <b>
              Không cần gửi <code>soDem</code>
            </b>{' '}
            — hệ thống tự cộng các xung chưa chốt trong khoảng đó. Số “hệ thống ghi nhận” <b>luôn do máy tự đếm</b>, không nhận từ bên ngoài. Lệch
            quá 5 lượt sẽ tự mở sự cố an ninh.
          </div>
          <Ma>{`curl -X POST ${GOC}/api/v1/dem-xe/chot \\
  -H "X-API-Key: sp_..." -H "Content-Type: application/json" \\
  -d '{"cong":"A1","tuLuc":"2026-09-05T06:00:00","denLuc":"2026-09-05T14:00:00","moBarieTay":0}'`}</Ma>
          <Ma json>{`{ "ok": true, "soDemThucTe": 128, "soHeThong": 126,
  "chenhLech": 2, "mucDo": "Sai số nhỏ", "nguonDem": "CamBien",
  "soXungDaGom": 128, "canhBao": false }`}</Ma>
        </Endpoint>

        <Endpoint verb="GET" duong="/api/v1/dem-xe/chua-chot?cong=A1">
          <div className="small text-body-secondary">Thiết bị tự kiểm tra xung đã lên máy chủ chưa trước khi xoá bộ nhớ đệm.</div>
        </Endpoint>

        <Endpoint verb="POST" duong="/api/v1/lenh" canGhi>
          <div className="small text-body-secondary">Phần mềm vận tải bên khách tự đặt lệnh giao nhận. Trả về số lệnh + mã PIN cho tài xế.</div>
          <Ma>{`curl -X POST ${GOC}/api/v1/lenh \\
  -H "X-API-Key: sp_..." -H "Content-Type: application/json" \\
  -d '{"loaiLenh":"LayRong","bienSoXe":"51C-638.72",
       "tenTaiXe":"Trần Văn A","sdtTaiXe":"0912345678",
       "henTuLuc":"2026-09-05T08:00:00","henDenLuc":"2026-09-05T12:00:00"}'`}</Ma>
          <Ma json>{`{ "ok": true, "soLenh": "LGN-2609-A1B2C", "maPin": "418205", "trangThai": "Chờ xe" }`}</Ma>
          <div className="small text-body-secondary">
            <code>loaiLenh</code> nhận: <code>GiaoCont</code> · <code>HaCont</code> · <code>TraRong</code> · <code>LayRong</code>.
          </div>
        </Endpoint>
      </TheNoiDung>

      <TheNoiDung tieuDe="3. Mã lỗi" icon="bi-exclamation-octagon">
        <div className="table-responsive">
          <table className="table table-sm sp-bang mb-0">
            <thead>
              <tr>
                <th style={{ width: 90 }}>Mã</th>
                <th>Nghĩa</th>
                <th>Xử lý</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>
                  <code>401</code>
                </td>
                <td>Thiếu khóa hoặc khóa đã thu hồi</td>
                <td>Liên hệ cảng cấp lại khóa</td>
              </tr>
              <tr>
                <td>
                  <code>403</code>
                </td>
                <td>Khóa không đủ quyền, hoặc dữ liệu thuộc khách hàng khác</td>
                <td>Xin nâng quyền hoặc kiểm tra lại phạm vi khóa</td>
              </tr>
              <tr>
                <td>
                  <code>404</code>
                </td>
                <td>Không tìm thấy container / lệnh / xe</td>
                <td>Kiểm tra lại mã đã gửi</td>
              </tr>
              <tr>
                <td>
                  <code>400</code>
                </td>
                <td>Dữ liệu gửi lên thiếu hoặc sai định dạng</td>
                <td>
                  Đọc trường <code>loi</code> trong phản hồi
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div className="small text-body-secondary mt-2">
          Mọi phản hồi lỗi đều có dạng <code>{'{ "ok": false, "loi": "mô tả bằng tiếng Việt" }'}</code> — hiển thị thẳng cho người dùng cuối được.
        </div>
      </TheNoiDung>

      <TheNoiDung tieuDe="4. Khuyến nghị khi đấu nối" icon="bi-lightbulb" lop="mb-0">
        <ul className="small mb-0" style={{ lineHeight: 1.9 }}>
          <li>
            Gọi <code>/ping</code> lúc khởi động ứng dụng để phát hiện sớm khóa hết hiệu lực.
          </li>
          <li>
            Đồng bộ tồn bãi bằng <code>/ton-bai</code> theo trang, đừng gọi <code>/container/{'{số}'}</code> cho từng cont một.
          </li>
          <li>Không nhúng khóa vào ứng dụng chạy trên máy người dùng — để ở máy chủ của bên tích hợp.</li>
          <li>
            Mỗi phần mềm dùng <b>một khóa riêng</b>, để khi cần thu hồi thì không ảnh hưởng hệ thống khác.
          </li>
        </ul>
      </TheNoiDung>
    </div>
  )
}
