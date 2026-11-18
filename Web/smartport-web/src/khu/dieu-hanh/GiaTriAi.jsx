import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import OSo, { tienVaDonVi } from './dungChung'
import './dieu-hanh.css'

/** Moi o bam vao la mo dung module do — dia chi giu nguyen ban MVC, viet thuong */
function TheModule({ den, icon, ten, children }) {
  return (
    <Link className="card sp-the p-3 h-100 dh-the-bam" to={den}>
      <div className="d-flex align-items-center mb-2 fw-bold dh-chu-tim">
        <i className={'bi ' + icon + ' me-2'} />
        {ten}
        <i className="bi bi-chevron-right ms-auto text-body-secondary" />
      </div>
      <div className="small">{children}</div>
    </Link>
  )
}

/**
 * GIA TRI AI · ROI — moi module AI tu dem viec da lam tu du lieu van hanh that, quy ra gio cong + tien.
 * Thay man hinh MVC Admin/Dashboard/GiaTriAI. So lieu tu GET /api/gia-tri-ai.
 */
export default function GiaTriAi() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/gia-tri-ai')

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const tien = tienVaDonVi(d.tienCong)
  const dm = d.dinhMuc

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
        <div className="small text-body-secondary">
          <i className="bi bi-arrow-repeat me-1" />
          Tự đếm từ dữ liệu vận hành · {dinhDangNgayGio(d.thoiDiem)}
        </div>
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => taiLai()} disabled={dangTai}>
          <i className="bi bi-arrow-clockwise me-1" />
          Làm mới
        </button>
      </div>

      <div className="row row-cols-2 row-cols-md-4 g-3 mb-3">
        <div className="col">
          <OSo mau="la" so={tien.so} donVi={tien.donVi} nhan="Nhân công tiết kiệm" phu={`${dinhDangTien(dm.donGiaGio)}/giờ công`} icon="bi-cash-coin" />
        </div>
        <div className="col">
          <OSo mau="tim" so={dinhDangSo(d.gioCong, 1)} donVi="giờ" nhan="Giờ công AI làm thay" icon="bi-clock-history" />
        </div>
        <div className="col">
          <OSo mau="do" so={dinhDangSo(d.tongSuCo)} nhan="Sự cố AI phát hiện" phu="an toàn · hàng hóa · tài xế · lạnh" icon="bi-shield-exclamation" />
        </div>
        <div className="col">
          <OSo mau="ngoc" so={dinhDangSo(d.soAnhXe)} nhan="Ảnh bằng chứng tự lưu" phu="dùng được khi tranh chấp" icon="bi-camera" />
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/gate" icon="bi-upc-scan" ten="Đọc biển số tại cổng">
            <b>{dinhDangSo(d.soALPR)}</b> lượt tự đọc và đối chiếu lệnh · <b>{dinhDangSo(d.phutALPR)}</b> phút
            <br />
            <b>{dinhDangSo(d.soAnhXe)}</b> ảnh xe làm bằng chứng
          </TheModule>
        </div>
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/security" icon="bi-shield-check" ten="Camera an ninh 24/7">
            <b>{dinhDangSo(d.soAnToan)}</b> sự cố an toàn (lửa, ngã, xâm nhập)
            <br />
            <b>{dinhDangSo(d.soHangHoa)}</b> sự cố hàng hóa (móp, nghiêng, sai số cont)
          </TheModule>
        </div>
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/driver" icon="bi-person-video3" ten="Giám sát tài xế">
            <b>{dinhDangSo(d.soDMS)}</b> cảnh báo ngủ gật, mất tập trung, dùng điện thoại
          </TheModule>
        </div>
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/reefer" icon="bi-thermometer-snow" ten="Container lạnh">
            <b>{dinhDangSo(d.soDocIoT)}</b> lượt đọc nhiệt tự động · thay <b>{dinhDangSo(d.phutIoT)}</b> phút đi tua
            <br />
            <b>{dinhDangSo(d.soReefer)}</b> cảnh báo lệch nhiệt
          </TheModule>
        </div>
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/security/tally" icon="bi-123" ten="Kiểm đếm hàng">
            <b>{dinhDangSo(d.soItemDem)}</b> kiện đếm hộ qua <b>{dinhDangSo(d.soPhienDem)}</b> phiên · <b>{dinhDangSo(d.phutDem)}</b> phút
            <br />
            <b>{dinhDangSo(d.soLechDem)}</b> kiện sai mẫu bắt được
          </TheModule>
        </div>
        <div className="col-lg-4 col-md-6">
          <TheModule den="/admin/vision/analytics" icon="bi-graph-up-arrow" ten="Điều hành cổng">
            <b>{dinhDangSo(d.soNhanDien)}</b> lượt nhận diện ({dinhDangSo(d.soDoiTuong)} đối tượng)
            <br />
            <b>{dinhDangSo(d.soCheckIn)}</b> lượt check-in trước · bớt <b>{dinhDangSo(d.phutCheckIn)}</b> phút chờ cổng
          </TheModule>
        </div>
      </div>

      <details className="card sp-the p-3 dh-cach-tinh">
        <summary className="fw-bold dh-chu-tim">
          <i className="bi bi-calculator me-1" />
          Cách tính giờ công
        </summary>
        <div className="table-responsive">
          <table className="table table-sm sp-bang align-middle mt-2 mb-0">
            <tbody>
              <tr>
                <td>
                  Đọc biển số: {dinhDangSo(d.soALPR)} lượt × {dinhDangSo(dm.phutMoiXeALPR, 1)} phút ghi tay, đối chiếu
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.phutALPR)} phút</td>
              </tr>
              <tr>
                <td>
                  Container lạnh: {dinhDangSo(d.soDocIoT)} lượt đọc × {dinhDangSo(dm.phutMoiLuotIoT, 1)} phút đi tua
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.phutIoT)} phút</td>
              </tr>
              <tr>
                <td>
                  Kiểm đếm: {dinhDangSo(d.soItemDem)} kiện × {dm.giayMoiKien} giây + {dinhDangSo(d.soPhienDem)} phiên ×{' '}
                  {dinhDangSo(dm.phutMoiPhienDem, 1)} phút biên bản
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.phutDem)} phút</td>
              </tr>
              <tr>
                <td>
                  Check-in trước: {dinhDangSo(d.soCheckIn)} lượt × {dinhDangSo(dm.phutMoiCheckIn, 1)} phút chờ cổng
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.phutCheckIn)} phút</td>
              </tr>
              <tr>
                <td className="fw-semibold">Tổng ÷ 60 × {dinhDangTien(dm.donGiaGio)}/giờ công</td>
                <td className="text-end fw-bold dh-chu-xanh-la">
                  {dinhDangSo(d.gioCong, 1)} giờ ≈ {dinhDangTien(d.tienCong)}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>
    </>
  )
}
