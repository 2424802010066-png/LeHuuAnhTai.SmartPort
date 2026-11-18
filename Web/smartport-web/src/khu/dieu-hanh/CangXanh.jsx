import { useEffect, useRef } from 'react'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import OSo, { tienVaDonVi } from './dungChung'
import './dieu-hanh.css'

const DAT = [
  'Số hóa chứng từ',
  'Điều tiết xe, giảm khí thải cổng',
  'Giám sát container lạnh',
  'Báo cáo phát thải',
  'An toàn lao động bằng camera AI',
]

/**
 * CANG XANH · ESG — trang de doanh nghiep NHIN SO va IN BAO CAO gui doi tac, ngan hang.
 * Thay man hinh MVC Admin/Dashboard/CangXanh. So lieu tu GET /api/cang-xanh (tinh tu du lieu van hanh that).
 */
export default function CangXanh() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/cang-xanh')
  const oCachTinh = useRef(null)

  // In bao cao thi phai in ca cach tinh — doi tac doc so la hoi ngay "tinh the nao"
  useEffect(() => {
    const moRa = () => {
      if (oCachTinh.current) oCachTinh.current.open = true
    }
    window.addEventListener('beforeprint', moRa)
    return () => window.removeEventListener('beforeprint', moRa)
  }, [])

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const tien = tienVaDonVi(d.tienTietKiem)
  const hs = d.heSo

  return (
    <div className="dh-in-duoc">
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
        <div className="small text-body-secondary">
          <i className="bi bi-arrow-repeat me-1" />
          Tính từ dữ liệu vận hành · {dinhDangNgayGio(d.thoiDiem)}
        </div>
        <div className="d-flex gap-2 d-print-none">
          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => taiLai()} disabled={dangTai}>
            <i className="bi bi-arrow-clockwise me-1" />
            Làm mới
          </button>
          <button type="button" className="btn btn-sm btn-success" onClick={() => window.print()}>
            <i className="bi bi-printer me-1" />
            In báo cáo ESG
          </button>
        </div>
      </div>

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OSo mau="la" so={tien.so} donVi={tien.donVi} nhan="Tiền tiết kiệm" phu="giấy + dầu không phải đốt" icon="bi-piggy-bank" />
        </div>
        <div className="col">
          <OSo mau="la-sang" so={dinhDangSo(d.co2Kg, 1)} donVi="kg" nhan="CO₂ cắt giảm" phu={`giấy ${dinhDangSo(d.co2GiayKg, 1)} · xe chờ ${dinhDangSo(d.co2XeChoKg, 1)}`} icon="bi-cloud-minus" />
        </div>
        <div className="col">
          <OSo mau="ngoc" so={dinhDangSo(d.toGiay)} donVi="tờ" nhan="Giấy bớt in" phu={`${dinhDangSo(d.soLenh)} lệnh · ${dinhDangSo(d.soChungTu)} chứng từ`} icon="bi-file-earmark-minus" />
        </div>
        <div className="col">
          <OSo mau="vang" so={dinhDangSo(d.litDau, 1)} donVi="lít" nhan="Dầu bớt đốt" phu={`${dinhDangSo(d.phutIdle)} phút xe khỏi nổ máy chờ`} icon="bi-fuel-pump" />
        </div>
      </div>

      <TheNoiDung tieuDe="Tiêu chí cảng xanh TCCS 02:2022/CHHVN" icon="bi-patch-check">
        <div className="row g-2">
          {DAT.map((t) => (
            <div className="col-md-4" key={t}>
              <div className="dh-tieu-chi">
                <i className="bi bi-check-circle-fill dh-chu-xanh-la" />
                <span>
                  {t}
                  {t === 'Số hóa chứng từ' ? <b className="ms-1">{d.tyLeDienTu}% lượt xe không giấy</b> : null}
                </span>
              </div>
            </div>
          ))}
          <div className="col-md-4">
            <div className="dh-tieu-chi text-body-secondary">
              <i className="bi bi-hourglass-split" />
              <span>Điện mặt trời, điện bờ cho tàu</span>
            </div>
          </div>
        </div>
      </TheNoiDung>

      <details className="card sp-the p-3 dh-cach-tinh" ref={oCachTinh}>
        <summary className="fw-bold dh-chu-xanh-la">
          <i className="bi bi-calculator me-1" />
          Cách tính
        </summary>
        <div className="table-responsive">
          <table className="table table-sm sp-bang align-middle mt-2 mb-0">
            <tbody>
              <tr>
                <td>
                  {dinhDangSo(d.soLenh)} lệnh giao nhận điện tử × {hs.toMoiLenh} liên EIR
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.soLenh * hs.toMoiLenh)} tờ</td>
              </tr>
              <tr>
                <td>
                  {dinhDangSo(d.soChungTu)} chứng từ số × {hs.toMoiChungTu} tờ
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.soChungTu * hs.toMoiChungTu)} tờ</td>
              </tr>
              <tr>
                <td>
                  {dinhDangSo(d.soCheckIn)} lượt check-in trước × {hs.phutMoiCheckIn} phút bớt nổ máy chờ cổng
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.phutIdle)} phút</td>
              </tr>
              <tr>
                <td>
                  CO₂: {dinhDangSo(hs.gramCo2MoiTo, 1)} g/tờ giấy · {dinhDangSo(hs.kgCo2MoiPhutCho, 3)} kg/phút nổ máy chờ ·{' '}
                  {dinhDangSo(hs.litDauMoiPhutCho, 3)} lít dầu/phút
                </td>
                <td className="text-end fw-bold">{dinhDangSo(d.co2Kg, 1)} kg</td>
              </tr>
              <tr>
                <td className="fw-semibold">
                  Tiền: {dinhDangTien(hs.giaMotTo)}/tờ ({dinhDangTien(d.tienGiay)}) · {dinhDangTien(hs.giaDauLit)}/lít dầu ({dinhDangTien(d.tienDau)})
                </td>
                <td className="text-end fw-bold dh-chu-xanh-la">{dinhDangTien(d.tienTietKiem)}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>
    </div>
  )
}
