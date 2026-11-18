import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import { goTien, tienO, useLamMoiTheoSuKien } from './dungChung'
import './tai-chinh.css'

/** Mot dong bieu gia sua tai cho. key cua dong doi theo du lieu goc -> luu xong / co nguoi khac sua thi o nhap tu nap lai. */
function DongBieuGia({ b, luu, dangChay }) {
  const [phiLuot, setPhiLuot] = useState(dinhDangSo(b.phiLuot))
  const [phiThang, setPhiThang] = useState(dinhDangSo(b.phiThang))
  const [kichHoat, setKichHoat] = useState(b.kichHoat)
  const daDoi = phiLuot !== dinhDangSo(b.phiLuot) || phiThang !== dinhDangSo(b.phiThang) || kichHoat !== b.kichHoat

  return (
    <tr className={kichHoat ? '' : 'opacity-50'}>
      <td>
        <div className="fw-semibold">{b.loaiXe}</div>
        {b.ghiChu && <div className="small text-body-secondary">{b.ghiChu}</div>}
      </td>
      <td>
        <input
          className="form-control form-control-sm tc-o-tien ms-auto"
          inputMode="numeric"
          aria-label={'Phí lượt ' + b.loaiXe}
          value={phiLuot}
          onChange={(e) => setPhiLuot(goTien(e.target.value))}
        />
      </td>
      <td>
        <input
          className="form-control form-control-sm tc-o-tien ms-auto"
          inputMode="numeric"
          aria-label={'Phí tháng ' + b.loaiXe}
          value={phiThang}
          onChange={(e) => setPhiThang(goTien(e.target.value))}
        />
      </td>
      <td className="text-center">
        <input className="form-check-input" type="checkbox" aria-label={'Áp dụng ' + b.loaiXe} checked={kichHoat} onChange={(e) => setKichHoat(e.target.checked)} />
      </td>
      <td className="text-end">
        <button
          type="button"
          className={'btn btn-sm ' + (daDoi ? 'btn-primary' : 'btn-outline-secondary')}
          title="Lưu biểu giá"
          aria-label={'Lưu biểu giá ' + b.loaiXe}
          disabled={!daDoi || dangChay}
          onClick={() => luu(b, { phiLuot, phiThang, kichHoat })}
        >
          <i className="bi bi-save" />
        </button>
      </td>
    </tr>
  )
}

/** Thu phi cong: bieu gia theo loai xe + so thu hom nay (thay Admin/PhiCong cua ban MVC). */
export default function PhiCong() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/phi-cong')
  const { chay, dangChay } = useThaoTac()

  // Moi luot xe RA cong: module Tai chinh thu phi roi phat tai-chinh.phi-cong-da-xu-ly
  useLamMoiTheoSuKien('tai-chinh.*', taiLai)

  async function luu(b, gt) {
    if (await chay(() => goiApi('/api/phi-cong/bieu-gia/' + b.maBP, { method: 'PUT', body: gt }))) taiLai({ im: true })
  }

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />
  const hn = d.homNay

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-cash-coin" mau="tim" so={<span title={dinhDangTien(hn.tongThu)}>{tienO(hn.tongThu)}</span>} nhan={`Thu hôm nay · ${hn.soLuot} lượt`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-wallet2" mau="cam" so={<span title={dinhDangTien(hn.tienMat)}>{tienO(hn.tienMat)}</span>} nhan={`Tiền mặt phải nộp · trừ TK ${tienO(hn.truTaiKhoan)}`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-credit-card-2-front" mau="xanh" so={d.soThe} nhan={`Thẻ hiệu lực · ${hn.luotTheThang} lượt thẻ tháng`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-calendar-x" mau={d.theSapHetHan > 0 ? 'do' : 'la'} so={d.theSapHetHan} nhan="Thẻ tháng hết hạn trong 7 ngày" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-xl-8">
          <TheNoiDung
            tieuDe="Biểu giá theo loại xe"
            icon="bi-tags"
            khongDem
            phai={
              <>
                <Link className="btn btn-sm btn-outline-primary" to="/admin/phicong/thexe">
                  <i className="bi bi-credit-card-2-front me-1" />
                  Thẻ xe
                </Link>
                <Link className="btn btn-sm btn-outline-primary" to="/admin/phicong/tongket">
                  <i className="bi bi-clipboard-data me-1" />
                  Tổng kết ca
                </Link>
              </>
            }
            chan={<span className="small text-body-secondary">Thẻ tháng còn hạn → miễn · thẻ trả trước → trừ dần · còn lại → tiền mặt.</span>}
          >
            <div className="table-responsive">
              <table className="table table-sm sp-bang align-middle mb-0">
                <thead>
                  <tr>
                    <th scope="col">Loại xe</th>
                    <th scope="col" className="text-end" style={{ width: 170 }}>
                      Phí lượt (đ)
                    </th>
                    <th scope="col" className="text-end" style={{ width: 170 }}>
                      Phí tháng (đ)
                    </th>
                    <th scope="col" className="text-center" style={{ width: 90 }}>
                      Áp dụng
                    </th>
                    <th scope="col" style={{ width: 60 }} />
                  </tr>
                </thead>
                <tbody>
                  {d.bieuGia.length === 0 && (
                    <tr>
                      <td colSpan={5} className="text-center text-muted py-4">
                        Chưa có biểu giá.
                      </td>
                    </tr>
                  )}
                  {d.bieuGia.map((b) => (
                    <DongBieuGia key={`${b.maBP}-${b.phiLuot}-${b.phiThang}-${b.kichHoat}`} b={b} luu={luu} dangChay={dangChay} />
                  ))}
                </tbody>
              </table>
            </div>
          </TheNoiDung>
        </div>

        <div className="col-xl-4">
          <TheNoiDung tieuDe="Hôm nay theo loại xe" icon="bi-bar-chart" khongDem>
            {hn.theoLoaiXe.length === 0 ? (
              <div className="text-center text-muted py-4 small">
                <i className="bi bi-inbox me-1" />
                Chưa có lượt thu phí hôm nay.
              </div>
            ) : (
              <table className="table table-sm sp-bang align-middle mb-0">
                <thead>
                  <tr>
                    <th scope="col">Loại xe</th>
                    <th scope="col" className="text-end">
                      Lượt
                    </th>
                    <th scope="col" className="text-end">
                      Thành tiền
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {hn.theoLoaiXe.map((x) => (
                    <tr key={x.loaiXe}>
                      <td>{x.loaiXe}</td>
                      <td className="text-end">{x.soLuot}</td>
                      <td className="text-end fw-semibold text-nowrap">{dinhDangTien(x.thanhTien)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
