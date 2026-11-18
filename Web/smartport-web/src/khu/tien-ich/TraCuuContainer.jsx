import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { docSoContainer, dungChuan6346, veAnhContainerDemo } from '../../ai/ocrContainer'
import { docFileThanhAnh } from '../../ai/taiThuVien'
import { goiApi } from '../../api/client'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import { DauTrang } from './chung'

const ICON_BUOC = {
  'do-tau': 'bi-box-arrow-down',
  'ha-bai': 'bi-stack',
  'ra-bai': 'bi-box-arrow-right',
  'hien-tai': 'bi-geo-alt-fill',
  'du-kien-xuat': 'bi-calendar-event',
}

function O({ nhan, children }) {
  return (
    <div className="col-6 col-md-4">
      <div className="sp-truong-nhan">{nhan}</div>
      <div className="sp-truong-gia-tri">{children ?? '—'}</div>
    </div>
  )
}

function Chuyen({ tieuDe, c }) {
  return (
    <div className="col-md-6">
      <div className="sp-o-chuyen">
        <div className="sp-truong-nhan">{tieuDe}</div>
        {c ? (
          <>
            <div className="fw-semibold">
              {c.maChuyen} · {c.tenTau || '—'}
            </div>
            <div className="small text-body-secondary">
              ETA {dinhDangNgayGio(c.ata || c.eta)} · ETD {dinhDangNgayGio(c.atd || c.etd)}
            </div>
          </>
        ) : (
          <div className="text-body-secondary">Chưa gắn chuyến</div>
        )}
      </div>
    </div>
  )
}

/**
 * Tra cuu container cho khach vang lai (thay Views/Tracking/Index.cshtml) — dia chi /tracking?soCont=...
 *
 * Goi API cong khai cua khu nay (khong gui token) nen khong lo du lieu noi bo: khong co
 * dong thoi gian luot xe / chung tu, chi co thong tin container, chuyen tau, phi luu bai va hanh trinh.
 * OCR chay NGAY TREN TRINH DUYET (Tesseract.js) — anh khong roi may khach.
 */
export default function TraCuuContainer() {
  const [thamSo, setThamSo] = useSearchParams()
  // Dia chi moi dung ?soCont=, van doc ?so= cua link cu cho khong gay link da gui di
  const soTrenUrl = thamSo.get('soCont') || thamSo.get('so') || ''
  const [so, setSo] = useState(soTrenUrl)
  const [dangTim, setDangTim] = useState(false)
  const [kq, setKq] = useState(null)
  const [loi, setLoi] = useState('')
  const [ocr, setOcr] = useState({ trangThai: 'nghi' })
  const [anhXemTruoc, setAnhXemTruoc] = useState(null)

  const maSach = so.toUpperCase().replace(/[^A-Z0-9]/g, '')
  const dungDinhDang = /^[A-Z]{4}\d{7}$/.test(maSach)

  async function traCuu(ma) {
    const m = (ma ?? maSach).toUpperCase().replace(/[^A-Z0-9]/g, '')
    if (!m) return
    setDangTim(true)
    setLoi('')
    setKq(null)
    setThamSo({ soCont: m }, { replace: true })
    try {
      setKq(await goiApi('/api/tien-ich/tra-cuu-container/' + encodeURIComponent(m), { coToken: false }))
    } catch (e) {
      setLoi(e.message)
    } finally {
      setDangTim(false)
    }
  }

  // Mo tu link co san so container (ma QR tren phieu, link tu man hinh cong) -> tra cuu ngay
  const daTuTraCuu = useRef(false)
  useEffect(() => {
    if (daTuTraCuu.current) return
    daTuTraCuu.current = true
    if (soTrenUrl) traCuu(soTrenUrl)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function docAnh(nguonAnh, xemTruoc) {
    setAnhXemTruoc(xemTruoc)
    setOcr({ trangThai: 'dang-doc', tienDo: 0, nhan: 'Đang tải mô hình OCR (lần đầu ~10 MB)…' })
    try {
      const r = await docSoContainer(nguonAnh, (p, nhan) => setOcr({ trangThai: 'dang-doc', tienDo: p, nhan }))
      setOcr({ trangThai: 'xong', ...r })
      if (r.ma) {
        setSo(r.ma)
        if (r.dungChuan) traCuu(r.ma)
      }
    } catch (e) {
      setOcr({ trangThai: 'loi', loi: e.message })
    }
  }

  async function chonFile(e) {
    const f = e.target.files[0]
    e.target.value = ''
    if (!f) return
    try {
      const img = await docFileThanhAnh(f)
      docAnh(img, img.src)
    } catch (err) {
      setOcr({ trangThai: 'loi', loi: err.message })
    }
  }

  function thuAnhDemo() {
    const c = veAnhContainerDemo()
    docAnh(c, c.toDataURL('image/png'))
  }

  return (
    <>
      <DauTrang icon="bi-search" tieuDe="Tra cứu container" mucHienTai="Tra cứu container" />
      <div className="container pb-5">
        <div className="row g-3 mb-3">
          <div className="col-lg-7">
            <form
              className="card sp-the h-100"
              onSubmit={(e) => {
                e.preventDefault()
                traCuu()
              }}
            >
              <div className="card-body">
                <label className="form-label fw-semibold" htmlFor="soCont">
                  Số container (ISO 6346)
                </label>
                <div className="input-group input-group-lg">
                  <input
                    id="soCont"
                    className="form-control ti-o-ma text-center"
                    value={so}
                    onChange={(e) => setSo(e.target.value.toUpperCase())}
                    placeholder="MSCU2201417"
                    maxLength={15}
                    autoComplete="off"
                  />
                  <button className="btn btn-primary" disabled={dangTim || !maSach} title="Tra cứu">
                    {dangTim ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-search" />}
                    <span className="ms-1 d-none d-sm-inline">Tra cứu</span>
                  </button>
                </div>
                <div className="small mt-2">
                  {maSach.length === 0 ? (
                    <span className="text-body-secondary">4 chữ cái (mã chủ hàng + loại) và 7 chữ số, chữ số cuối là số kiểm tra.</span>
                  ) : !dungDinhDang ? (
                    <span className="text-body-secondary">Chưa đúng dạng 4 chữ + 7 số.</span>
                  ) : dungChuan6346(maSach) ? (
                    <span className="text-success">
                      <i className="bi bi-check-circle-fill me-1" />
                      Số kiểm tra ISO 6346 hợp lệ
                    </span>
                  ) : (
                    <span className="text-danger">
                      <i className="bi bi-x-circle-fill me-1" />
                      Số kiểm tra ISO 6346 không khớp — có thể gõ/đọc nhầm một ký tự
                    </span>
                  )}
                </div>
              </div>
            </form>
          </div>

          <div className="col-lg-5">
            <div className="card sp-the h-100">
              <div className="card-body">
                <div className="fw-semibold mb-2">
                  <i className="bi bi-upc-scan me-1" />
                  Đọc số container từ ảnh (OCR)
                </div>
                <div className="d-flex gap-2">
                  <input type="file" accept="image/*" className="form-control form-control-sm" onChange={chonFile} disabled={ocr.trangThai === 'dang-doc'} aria-label="Chọn ảnh container" />
                  <button type="button" className="btn btn-sm btn-outline-secondary text-nowrap" onClick={thuAnhDemo} disabled={ocr.trangThai === 'dang-doc'}>
                    Ảnh demo
                  </button>
                </div>
                {anhXemTruoc && <img src={anhXemTruoc} alt="Ảnh container vừa chọn" className="ti-anh-ocr mt-2" />}
                {ocr.trangThai === 'dang-doc' && (
                  <div className="mt-2">
                    <div className="progress" style={{ height: 6 }}>
                      <div className="progress-bar" style={{ width: Math.round((ocr.tienDo || 0) * 100) + '%' }} />
                    </div>
                    <div className="small text-body-secondary mt-1">{ocr.nhan}</div>
                  </div>
                )}
                {ocr.trangThai === 'loi' && <div className="alert alert-danger py-2 small mt-2 mb-0">Lỗi OCR: {ocr.loi}</div>}
                {ocr.trangThai === 'xong' && (
                  <div className="small mt-2">
                    {ocr.ma ? (
                      <>
                        Đọc được <b className="font-monospace">{ocr.ma}</b> <span className="badge text-bg-light border">tin cậy {ocr.doTinCay}%</span>{' '}
                        {ocr.dungChuan ? <span className="badge text-bg-success">checksum khớp</span> : <span className="badge text-bg-warning">checksum lệch — xem lại ảnh</span>}
                      </>
                    ) : (
                      <span className="text-warning-emphasis">Chưa thấy mã hợp lệ (4 chữ + 7 số) trong ảnh.</span>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {loi && (
          <div className="alert alert-warning">
            <i className="bi bi-info-circle me-1" />
            {loi}
          </div>
        )}

        {kq && (
          <div className="row g-3">
            <div className="col-lg-7">
              <div className="card sp-the">
                <div className="card-header d-flex flex-wrap align-items-center gap-2">
                  <i className="bi bi-box-seam" />
                  <span className="font-monospace fw-bold fs-5 flex-grow-1">{kq.soCont}</span>
                  <span className="badge text-bg-primary">{kq.trangThai}</span>
                </div>
                <div className="card-body">
                  <div className="row g-3">
                    <O nhan="Loại">{kq.loaiCont}</O>
                    <O nhan="Tình trạng">{kq.tinhTrang}</O>
                    <O nhan="Trọng lượng">{kq.trongLuong != null && dinhDangSo(kq.trongLuong, 2) + ' tấn'}</O>
                    <O nhan="Hàng hóa">{kq.hangHoa}</O>
                    <O nhan="Vị trí bãi">{kq.viTriBai}</O>
                    <O nhan="Chủ hàng">{kq.chuHang}</O>
                    <O nhan="Ngày vào bãi">{kq.ngayVaoBai && dinhDangNgay(kq.ngayVaoBai)}</O>
                    <O nhan="Ngày rời bãi">{kq.ngayRaBai && dinhDangNgay(kq.ngayRaBai)}</O>
                    {kq.nhietDo != null && <O nhan="Nhiệt độ cài đặt">{dinhDangSo(kq.nhietDo, 1)} °C</O>}
                  </div>
                  <div className="row g-2 mt-2">
                    <Chuyen tieuDe="Chuyến nhập" c={kq.chuyenNhap} />
                    <Chuyen tieuDe="Chuyến xuất" c={kq.chuyenXuat} />
                  </div>
                </div>
              </div>
            </div>

            <div className="col-lg-5">
              <div className="card sp-the h-100">
                <div className="card-header">
                  <i className="bi bi-cash-coin me-2" />
                  Phí lưu bãi
                </div>
                <div className="card-body">
                  {kq.phiLuuBai ? (
                    <>
                      <div className="sp-tong-tien">{dinhDangTien(kq.phiLuuBai.tongTien)}</div>
                      <div className="small text-body-secondary mb-3">{kq.phiLuuBai.dangChay ? 'Đang tính — container còn trong bãi' : 'Đã chốt khi container rời bãi'}</div>
                      <div className="row g-2 small">
                        <O nhan="Tổng ngày">{kq.phiLuuBai.tongNgay}</O>
                        <O nhan="Miễn phí">{kq.phiLuuBai.ngayMienPhi}</O>
                        <O nhan="Tính phí">{kq.phiLuuBai.ngayTinhPhi}</O>
                        <O nhan="Lưu bãi">{dinhDangTien(kq.phiLuuBai.phiLuuBai)}</O>
                        <O nhan="Điện lạnh">{dinhDangTien(kq.phiLuuBai.phiDien)}</O>
                      </div>
                      {kq.phiLuuBai.dienGiai && <div className="small text-body-secondary mt-2">{kq.phiLuuBai.dienGiai}</div>}
                    </>
                  ) : (
                    <div className="text-body-secondary">Container chưa vào bãi — chưa phát sinh phí.</div>
                  )}
                </div>
              </div>
            </div>

            <div className="col-12">
              <div className="card sp-the">
                <div className="card-header">
                  <i className="bi bi-signpost-split me-2" />
                  Hành trình container
                </div>
                <div className="card-body">
                  <ul className="ti-hanh-trinh">
                    {(kq.hanhTrinh || []).map((b) => (
                      <li key={b.ma}>
                        <span className={'ti-cham ' + b.trangThai}>
                          <i className={'bi ' + (ICON_BUOC[b.ma] || 'bi-dot')} />
                        </span>
                        <div>
                          <div className="fw-semibold">{b.tieuDe}</div>
                          <div className="small text-body-secondary">{dinhDangNgayGio(b.luc)}</div>
                        </div>
                      </li>
                    ))}
                  </ul>
                  <div className="small text-body-secondary">
                    <i className="bi bi-clock-history me-1" />
                    Số liệu tại thời điểm {dinhDangNgayGio(kq.capNhatLuc)}.
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
