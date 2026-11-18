import { useEffect, useState } from 'react'
import { Doughnut } from 'react-chartjs-2'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { dinhDangTien, tienGon } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import { DauTrang } from './chung'

const MAU_BIEU_DO = ['#1173c4', '#ff7d2d', '#10b981', '#8b5cf6', '#f43f5e']

const COT = [
  { khoa: 'ten', ten: 'Khoản mục', hien: (l) => <span className="fw-semibold">{l.ten}</span> },
  { khoa: 'dienGiai', ten: 'Diễn giải', lop: 'text-body-secondary small', an: 'sm', hien: (l) => l.dienGiai },
  {
    khoa: 'tien',
    ten: 'Thành tiền',
    canPhai: true,
    hien: (l) => (l.thanhTien === 0 ? <span className="text-success">Miễn phí</span> : <span className="fw-semibold">{dinhDangTien(l.thanhTien)}</span>),
  },
]

const DICH_VU_KEM = [
  ['canVGM', 'Cân VGM (SOLAS)'],
  ['dongRut', 'Đóng/Rút kho CFS'],
  ['veSinh', 'Vệ sinh container'],
  ['giamDinh', 'Giám định lô hàng'],
]

/**
 * AI uoc tinh chi phi dich vu cang (thay Views/Cost/Index.cshtml) — dia chi /cost, khong can dang nhap.
 * Form va bang gia lay tu API (CostService dung chung voi ban MVC) nen so tien trung voi booking that.
 */
export default function UocTinhChiPhi() {
  const { duLieu: mau, loi: loiMau, taiLai } = useApi('/api/tien-ich/uoc-tinh-chi-phi', { coToken: false })
  const { gt, doi, datLai } = useBieuMau({ loaiCont: '40HC', soLuong: 2, chieuHang: 'Nhap', soNgayLuuBai: 5, veSinh: false, canVGM: false, dongRut: false, giamDinh: false, vanChuyen: 'Khong' })
  const [kq, setKq] = useState(null)
  const [dangTinh, setDangTinh] = useState(false)
  const [loi, setLoi] = useState(null)

  // Nap gia tri mac dinh cua bieu gia hien hanh (API quyet dinh, khong viet cung trong giao dien)
  useEffect(() => {
    if (mau?.macDinh) datLai({ ...mau.macDinh })
  }, [mau, datLai])

  async function tinh(e) {
    e.preventDefault()
    setDangTinh(true)
    setLoi(null)
    try {
      setKq(
        await goiApi('/api/tien-ich/uoc-tinh-chi-phi', {
          method: 'POST',
          coToken: false,
          body: { ...gt, soLuong: Number(gt.soLuong) || 0, soNgayLuuBai: Number(gt.soNgayLuuBai) || 0 },
        }),
      )
    } catch (e2) {
      setLoi(e2)
      setKq(null)
    } finally {
      setDangTinh(false)
    }
  }

  const bieuDo = kq && {
    labels: kq.coCau.map((c) => c.nhom),
    datasets: [{ data: kq.coCau.map((c) => c.giaTri), backgroundColor: MAU_BIEU_DO, borderWidth: 2 }],
  }

  return (
    <>
      <DauTrang icon="bi-calculator" tieuDe="Ước tính chi phí dịch vụ cảng" mucHienTai="Ước tính chi phí" phu="Miễn phí, không cần đăng nhập — tính theo biểu giá đang áp dụng của cảng." />
      <div className="container pb-5">
        <BaoLoi loi={loiMau} thuLai={taiLai} />

        <div className="row g-4">
          <div className="col-lg-5">
            <form className="card sp-the" onSubmit={tinh}>
              <div className="card-header">
                <i className="bi bi-ui-checks me-2" />
                Thông tin lô hàng
              </div>
              <div className="card-body">
                {!mau ? (
                  <DangTai chu="Đang tải biểu giá…" />
                ) : (
                  <div className="row g-3">
                    <div className="col-6">
                      <label className="form-label small fw-semibold mb-1" htmlFor="loaiCont">
                        Loại container
                      </label>
                      <select id="loaiCont" className="form-select" value={gt.loaiCont} onChange={doi('loaiCont')}>
                        {mau.loaiCont.map((l) => (
                          <option key={l} value={l}>
                            {l}
                          </option>
                        ))}
                      </select>
                      <div className="form-text">RF = container lạnh (tự tính phí điện)</div>
                    </div>
                    <div className="col-6">
                      <label className="form-label small fw-semibold mb-1" htmlFor="soLuong">
                        Số lượng (cont)
                      </label>
                      <input id="soLuong" type="number" min={1} max={1000} className="form-control" value={gt.soLuong} onChange={doi('soLuong')} required />
                    </div>
                    <div className="col-6">
                      <label className="form-label small fw-semibold mb-1" htmlFor="chieuHang">
                        Chiều hàng
                      </label>
                      <select id="chieuHang" className="form-select" value={gt.chieuHang} onChange={doi('chieuHang')}>
                        {mau.chieuHang.map((c) => (
                          <option key={c.giaTri} value={c.giaTri}>
                            {c.nhan}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="col-6">
                      <label className="form-label small fw-semibold mb-1" htmlFor="soNgay">
                        Số ngày lưu bãi
                      </label>
                      <input id="soNgay" type="number" min={0} max={120} className="form-control" value={gt.soNgayLuuBai} onChange={doi('soNgayLuuBai')} required />
                      <div className="form-text">Miễn phí {mau.soNgayMienPhi} ngày đầu</div>
                    </div>
                    <div className="col-12">
                      <div className="form-label small fw-semibold mb-1">Dịch vụ kèm theo</div>
                      <div className="row g-2">
                        {DICH_VU_KEM.map(([khoa, nhan]) => (
                          <div className="col-6" key={khoa}>
                            <div className="form-check">
                              <input className="form-check-input" type="checkbox" id={'dv-' + khoa} checked={!!gt[khoa]} onChange={doi(khoa)} />
                              <label className="form-check-label small" htmlFor={'dv-' + khoa}>
                                {nhan}
                              </label>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                    <div className="col-12">
                      <label className="form-label small fw-semibold mb-1" htmlFor="vanChuyen">
                        Vận chuyển đường bộ
                      </label>
                      <select id="vanChuyen" className="form-select" value={gt.vanChuyen} onChange={doi('vanChuyen')}>
                        {mau.vanChuyen.map((v) => (
                          <option key={v.giaTri} value={v.giaTri}>
                            {v.nhan}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="col-12">
                      <button className="btn btn-warning btn-lg w-100 fw-semibold" type="submit" disabled={dangTinh}>
                        {dangTinh ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-magic me-1" />}
                        Tính chi phí ngay
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </form>
          </div>

          <div className="col-lg-7">
            <BaoLoi loi={loi} />
            {!kq ? (
              <div className="card sp-the h-100">
                <div className="card-body text-center d-flex flex-column justify-content-center py-5">
                  <div className="fs-1 mb-2" aria-hidden="true">
                    <i className="bi bi-arrow-left-circle text-primary" />
                  </div>
                  <h2 className="h5 fw-bold">Điền thông tin lô hàng rồi bấm "Tính chi phí ngay"</h2>
                  <p className="text-body-secondary mb-0">Kết quả gồm bảng chi tiết từng khoản, cơ cấu chi phí và gợi ý tiết kiệm.</p>
                </div>
              </div>
            ) : (
              <>
                <div className="card sp-the mb-3">
                  <div className="card-header d-flex flex-wrap align-items-center gap-2">
                    <span className="flex-grow-1">
                      <i className="bi bi-receipt me-2" />
                      Bảng ước tính chi tiết
                    </span>
                    <span className="text-end">
                      <span className="ti-tong-tien d-block">{dinhDangTien(kq.tong)}</span>
                      <small className="text-body-secondary">
                        ≈ {tienGon(kq.trenMoiCont)} đ/container · {kq.soLuong} × {kq.loaiCont} · chưa gồm VAT
                      </small>
                    </span>
                  </div>
                  <BangDuLieu cot={COT} dong={kq.dong} khoaDong={(l, i) => l.ten + i} rong="Không có khoản phí nào." />
                </div>

                <div className="row g-3">
                  <div className="col-md-5">
                    <div className="card sp-the h-100">
                      <div className="card-header">
                        <i className="bi bi-pie-chart me-2" />
                        Cơ cấu chi phí
                      </div>
                      <div className="card-body" style={{ height: 240 }}>
                        <Doughnut
                          data={bieuDo}
                          options={{
                            maintainAspectRatio: false,
                            cutout: '60%',
                            plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } } },
                          }}
                        />
                      </div>
                    </div>
                  </div>
                  <div className="col-md-7">
                    <div className="card sp-the h-100">
                      <div className="card-header">
                        <i className="bi bi-lightbulb me-2" />
                        Nhận xét & gợi ý tiết kiệm
                      </div>
                      <div className="card-body">
                        {(kq.nhanXet || []).map((n, i) => (
                          <div className="ti-ghi-chu-ai" key={i}>
                            {n}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>

        <div className="text-body-secondary small mt-4">
          <i className="bi bi-info-circle me-1" />
          Kết quả mang tính tham khảo theo biểu giá công bố; chi phí thực tế theo booking được duyệt. Xem{' '}
          <Link to="/services">bảng giá dịch vụ đầy đủ</Link>.
        </div>
      </div>
    </>
  )
}
