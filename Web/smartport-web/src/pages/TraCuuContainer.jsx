import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { docSoContainer, dungChuan6346, veAnhContainerDemo } from '../ai/ocrContainer'
import { docFileThanhAnh } from '../ai/taiThuVien'
import { goiApi } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../utils/dinhDang'

function Dong({ nhan, children }) {
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
              {c.maChuyen} · {c.tenTau ?? '—'}
            </div>
            <div className="small text-muted">
              ETA {dinhDangNgayGio(c.eta)} · ETD {dinhDangNgayGio(c.etd)}
            </div>
          </>
        ) : (
          <div className="text-muted">Chưa gắn chuyến</div>
        )}
      </div>
    </div>
  )
}

/**
 * Tra cuu container — dung chung cho khach hang (cong khai, KHONG gui token)
 * va nhan vien (gui token -> API tra them dong thoi gian gom tu nhieu bang).
 */
export default function TraCuuContainer({ congKhai = false }) {
  const [thamSo, setThamSo] = useSearchParams()
  const { taiKhoan } = useAuth()
  const [so, setSo] = useState(thamSo.get('so') || '')
  const [dangTim, setDangTim] = useState(false)
  const [kq, setKq] = useState(null)
  const [loi, setLoi] = useState('')
  // Nhan vien (giong MVC Containers/TraCuu): co trong he thong -> nut sang ho so 360°; chua co -> nut khai bao moi
  const [hoSo, setHoSo] = useState(null)
  const [chuaCo, setChuaCo] = useState('')
  const [ocr, setOcr] = useState({ trangThai: 'nghi' })
  const [anhXemTruoc, setAnhXemTruoc] = useState(null)
  const fileRef = useRef(null)

  const maSach = so.toUpperCase().replace(/[^A-Z0-9]/g, '')
  const dungDinhDang = /^[A-Z]{4}\d{7}$/.test(maSach)

  async function traCuu(ma) {
    const m = (ma ?? maSach).toUpperCase().replace(/[^A-Z0-9]/g, '')
    if (!m) return
    setDangTim(true)
    setLoi('')
    setKq(null)
    setHoSo(null)
    setChuaCo('')
    setThamSo({ so: m }, { replace: true })
    try {
      if (congKhai) setKq(await goiApi('/api/container/' + encodeURIComponent(m), { coToken: false }))
      else {
        const [ketQua, maHoSo] = await Promise.all([
          goiApi('/api/container/' + encodeURIComponent(m)),
          goiApi('/api/quan-ly-container/tra-so/' + encodeURIComponent(m)).catch(() => null),
        ])
        setKq(ketQua)
        setHoSo(maHoSo)
      }
    } catch (e) {
      setLoi(e.message)
      if (!congKhai && e.trangThai === 404) setChuaCo(m)
    } finally {
      setDangTim(false)
    }
  }

  // Mo tu link "?so=..." (vd tu man hinh cong) thi tra cuu ngay
  const daTuTraCuu = useRef(false)
  useEffect(() => {
    if (daTuTraCuu.current) return
    daTuTraCuu.current = true
    if (thamSo.get('so')) traCuu(thamSo.get('so'))
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

  const noiDung = (
    <>
      <div className="row g-3 mb-3">
        <div className="col-lg-7">
          <form
            className="card sp-the"
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
                <input id="soCont" className="form-control sp-o-nhap-bien text-start" value={so} onChange={(e) => setSo(e.target.value.toUpperCase())} placeholder="MSCU2201417" maxLength={15} />
                <button className="btn btn-primary" disabled={dangTim || !maSach}>
                  {dangTim ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-search" />}
                  <span className="ms-1 d-none d-sm-inline">Tra cứu</span>
                </button>
              </div>
              <div className="small mt-2">
                {maSach.length === 0 ? (
                  <span className="text-muted">4 chữ cái (mã chủ hàng + loại) và 7 chữ số, chữ số cuối là số kiểm tra.</span>
                ) : !dungDinhDang ? (
                  <span className="text-muted">Chưa đúng dạng 4 chữ + 7 số.</span>
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
                <input ref={fileRef} type="file" accept="image/*" className="form-control form-control-sm" onChange={chonFile} disabled={ocr.trangThai === 'dang-doc'} />
                <button type="button" className="btn btn-sm btn-outline-secondary text-nowrap" onClick={thuAnhDemo} disabled={ocr.trangThai === 'dang-doc'}>
                  Ảnh demo
                </button>
              </div>
              {anhXemTruoc && <img src={anhXemTruoc} alt="Ảnh container" className="sp-anh-ocr mt-2" />}
              {ocr.trangThai === 'dang-doc' && (
                <div className="mt-2">
                  <div className="progress" style={{ height: 6 }}>
                    <div className="progress-bar" style={{ width: Math.round((ocr.tienDo || 0) * 100) + '%' }} />
                  </div>
                  <div className="small text-muted mt-1">{ocr.nhan}</div>
                </div>
              )}
              {ocr.trangThai === 'loi' && <div className="alert alert-danger py-2 small mt-2 mb-0">Lỗi OCR: {ocr.loi}</div>}
              {ocr.trangThai === 'xong' && (
                <div className="small mt-2">
                  {ocr.ma ? (
                    <>
                      AI đọc được <b className="font-monospace">{ocr.ma}</b> <span className="badge text-bg-light">tin cậy {ocr.doTinCay}%</span>{' '}
                      {ocr.dungChuan ? <span className="badge text-bg-success">checksum khớp</span> : <span className="badge text-bg-warning">checksum lệch — xem lại ảnh</span>}
                    </>
                  ) : (
                    <span className="text-warning-emphasis">Chưa thấy mã hợp lệ (4 chữ + 7 số). Chữ đọc được: “{ocr.raw || 'không có'}”</span>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {loi && (
        <div className="alert alert-warning d-flex flex-wrap align-items-center gap-2">
          <span className="flex-grow-1">
            <i className="bi bi-info-circle me-1" />
            {chuaCo ? `Chưa có container ${chuaCo} trong hệ thống. Kiểm tra lại số hoặc khai báo mới.` : loi}
          </span>
          {chuaCo && (
            <>
              <Link className="btn btn-sm btn-primary" to={'/admin/containers/form?soCont=' + encodeURIComponent(chuaCo)}>
                <i className="bi bi-plus-lg me-1" />
                Khai báo container
              </Link>
              <Link className="btn btn-sm btn-outline-secondary" to={'/admin/containers?q=' + encodeURIComponent(chuaCo)}>
                Tìm trong danh sách
              </Link>
            </>
          )}
        </div>
      )}

      {kq && (
        <div className="row g-3">
          <div className="col-lg-7">
            <div className="card sp-the mb-3">
              <div className="card-header d-flex align-items-center gap-2">
                <i className="bi bi-box-seam" />
                <span className="font-monospace fw-bold fs-5 flex-grow-1">{kq.soCont}</span>
                <span className="badge text-bg-primary">{kq.trangThai}</span>
                {hoSo && (
                  <Link className="btn btn-sm btn-outline-primary" to={hoSo.duongDan} title="Mở hồ sơ container 360°">
                    <i className="bi bi-folder2-open me-1" />
                    Hồ sơ 360°
                  </Link>
                )}
              </div>
              <div className="card-body">
                <div className="row g-3">
                  <Dong nhan="Loại">{kq.loaiCont}</Dong>
                  <Dong nhan="Tình trạng">{kq.tinhTrang}</Dong>
                  <Dong nhan="Hàng hóa">{kq.hangHoa}</Dong>
                  <Dong nhan="Vị trí bãi">{kq.viTriBai}</Dong>
                  <Dong nhan="Ngày vào bãi">{kq.ngayVaoBai && dinhDangNgay(kq.ngayVaoBai)}</Dong>
                  <Dong nhan="Ngày rời bãi">{kq.ngayRaBai && dinhDangNgay(kq.ngayRaBai)}</Dong>
                  <Dong nhan="Khách hàng">{kq.khachHang}</Dong>
                  {kq.nhietDoCaiDat != null && <Dong nhan="Nhiệt độ cài đặt">{dinhDangSo(kq.nhietDoCaiDat, 1)} °C</Dong>}
                </div>
                <div className="row g-2 mt-2">
                  <Chuyen tieuDe="Chuyến nhập" c={kq.chuyenNhap} />
                  <Chuyen tieuDe="Chuyến xuất" c={kq.chuyenXuat} />
                </div>
              </div>
            </div>
          </div>
          <div className="col-lg-5">
            <div className="card sp-the mb-3">
              <div className="card-header">
                <i className="bi bi-cash-coin me-2" />
                Phí lưu bãi
              </div>
              <div className="card-body">
                {kq.phiLuuBai ? (
                  <>
                    <div className="sp-tong-tien">{dinhDangTien(kq.phiLuuBai.tongTien)}</div>
                    <div className="small text-muted mb-3">{kq.phiLuuBai.dangChay ? 'Đang tính — container còn trong bãi' : 'Đã chốt khi container rời bãi'}</div>
                    <div className="row g-2 small">
                      <Dong nhan="Tổng ngày">{kq.phiLuuBai.tongNgay}</Dong>
                      <Dong nhan="Miễn phí">{kq.phiLuuBai.ngayMienPhi}</Dong>
                      <Dong nhan="Tính phí">{kq.phiLuuBai.ngayTinhPhi}</Dong>
                      <Dong nhan="Lưu bãi">{dinhDangTien(kq.phiLuuBai.phiLuuBai)}</Dong>
                      <Dong nhan="Điện lạnh">{dinhDangTien(kq.phiLuuBai.phiDien)}</Dong>
                    </div>
                    {kq.phiLuuBai.dienGiai && <div className="small text-muted mt-2">{kq.phiLuuBai.dienGiai}</div>}
                  </>
                ) : (
                  <div className="text-muted">Container chưa vào bãi — chưa phát sinh phí.</div>
                )}
              </div>
            </div>
          </div>

          <div className="col-12">
            <div className="card sp-the">
              <div className="card-header">
                <i className="bi bi-clock-history me-2" />
                Dòng thời gian
              </div>
              <div className="card-body">
                {kq.dongThoiGian ? (
                  kq.dongThoiGian.length ? (
                    <ul className="sp-dong-thoi-gian">
                      {kq.dongThoiGian.map((s, i) => (
                        <li key={i}>
                          <span className="sp-cham" style={{ background: /^#[0-9a-f]{3,8}$/i.test(s.mau || '') ? s.mau : '#64748b' }}>
                            <i className={'bi ' + (s.icon || 'bi-dot')} />
                          </span>
                          <div>
                            <div className="fw-semibold">
                              {s.tieuDe} <span className="badge text-bg-light fw-normal">{s.nhom}</span>
                            </div>
                            {s.moTa && <div className="small text-muted">{s.moTa}</div>}
                            <div className="small text-muted">{dinhDangNgayGio(s.luc)}</div>
                          </div>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <div className="text-muted">Chưa có sự kiện nào.</div>
                  )
                ) : (
                  <div className="text-muted small">
                    <i className="bi bi-lock me-1" />
                    Dòng thời gian nội bộ (lượt xe, lệnh, phiếu cân, chứng từ…) chỉ hiện cho nhân viên đăng nhập — cùng một API, token quyết định dữ liệu trả về.
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  )

  if (!congKhai) return noiDung

  return (
    <div className="sp-trang-cong-khai">
      <header className="sp-dau-cong-khai">
        <div className="container d-flex align-items-center">
          <span className="sp-logo-chu flex-grow-1">
            <i className="bi bi-box-seam-fill me-2" />
            SmartPort
          </span>
          <Link className="btn btn-sm btn-outline-light" to={taiKhoan ? '/admin' : '/account/login'}>
            {taiKhoan ? 'Vào khu điều hành' : 'Nhân viên đăng nhập'}
          </Link>
        </div>
      </header>
      <div className="container py-4">
        <h1 className="h4 mb-3">Tra cứu container</h1>
        {noiDung}
      </div>
    </div>
  )
}
