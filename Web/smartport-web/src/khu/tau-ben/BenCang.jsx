import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, HopThoai, NhanTrangThai, TheChiSo, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { TRANG_THAI_BEN, soMet, useFormTheoDiaChi } from './chung'
import './tau-ben.css'

const DUONG = '/admin/berths'
const LAM_MOI_MOI = 60000 // so do "truc tiep": tu lam moi ngam moi phut

const RONG = { tenBen: '', trangThai: 'Hoạt động', chieuDai: '', doSau: '', soCau: '2', moTa: '' }

const tuDong = (b) => ({
  tenBen: b.tenBen || '',
  trangThai: b.trangThai || 'Hoạt động',
  chieuDai: b.chieuDai ?? '',
  doSau: b.doSau ?? '',
  soCau: b.soCau ?? '',
  moTa: b.moTa || '',
})

const thanGui = (gt) => ({
  tenBen: gt.tenBen,
  trangThai: gt.trangThai,
  chieuDai: soHoacNull(gt.chieuDai),
  doSau: soHoacNull(gt.doSau),
  soCau: soHoacNull(gt.soCau),
  moTa: chuoiHoacNull(gt.moTa),
})

/** 'trong' | 'co-tau' | 'tre' | 'bao-tri' */
function tinhTrang(b) {
  if (b.trangThai !== 'Hoạt động') return 'bao-tri'
  if (!b.chuyenDangCap) return 'trong'
  return b.chuyenDangCap.quaHan ? 'tre' : 'co-tau'
}

function TauNho() {
  return (
    <svg width="112" height="52" viewBox="0 0 112 52" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path className="tb-than-tau" d="M6 34 L106 34 L96 50 L16 50 Z" />
      <rect className="tb-cabin-tau" x="84" y="16" width="14" height="18" rx="2" />
      <rect x="14" y="24" width="20" height="9" rx="1" fill="#ff7d2d" />
      <rect x="36" y="24" width="20" height="9" rx="1" fill="#22d3ee" />
      <rect x="58" y="24" width="20" height="9" rx="1" fill="#10b981" />
      <rect x="25" y="14" width="20" height="9" rx="1" fill="#f59e0b" />
    </svg>
  )
}

/** Ben cang: so do cau ben truc quan, the tung ben, them - sua - xoa (thay Admin/Berths cua ban MVC). */
export default function BenCang() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/ben-cang')
  const { dangSua, moForm, dongForm } = useFormTheoDiaChi(DUONG)
  const { gt, doi, datLai } = useBieuMau(RONG)
  const [tenGoc, setTenGoc] = useState('')
  const [dangTaiForm, setDangTaiForm] = useState(false)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()

  const dongFormMoiNhat = useRef(dongForm)
  useEffect(() => {
    dongFormMoiNhat.current = dongForm
  })

  useEffect(() => {
    const hen = setInterval(() => taiLai({ im: true }), LAM_MOI_MOI)
    return () => clearInterval(hen)
  }, [taiLai])

  useEffect(() => {
    setTenGoc('')
    setDangTaiForm(false)
    if (dangSua === null) return undefined
    if (dangSua === 0) {
      datLai(RONG)
      return undefined
    }
    let conHieuLuc = true
    setDangTaiForm(true)
    goiApi('/api/ben-cang/' + dangSua)
      .then((b) => {
        if (!conHieuLuc) return
        setTenGoc(b.tenBen)
        datLai(tuDong(b))
      })
      .catch((e) => {
        if (!conHieuLuc) return
        bao.loi(e)
        dongFormMoiNhat.current()
      })
      .finally(() => {
        if (conHieuLuc) setDangTaiForm(false)
      })
    return () => {
      conHieuLuc = false
    }
  }, [dangSua, datLai, bao])

  async function luu(e) {
    e.preventDefault()
    const than = thanGui(gt)
    const kq = await chay(() =>
      dangSua ? goiApi('/api/ben-cang/' + dangSua, { method: 'PUT', body: than }) : goiApi('/api/ben-cang', { method: 'POST', body: than }),
    )
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function xoa(b) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa bến?',
      noiDung: `Xóa ${b.tenBen}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/ben-cang/' + b.maBen, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const ds = duLieu || []
  const soHoatDong = ds.filter((b) => b.trangThai === 'Hoạt động').length
  const soCoTau = ds.filter((b) => b.chuyenDangCap).length
  const soTre = ds.filter((b) => b.chuyenDangCap?.quaHan).length
  const soBaoTri = ds.length - soHoatDong
  const soSanSang = ds.filter((b) => tinhTrang(b) === 'trong').length
  const tongCau = ds.filter((b) => b.trangThai === 'Hoạt động').reduce((s, b) => s + (b.soCau || 0), 0)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <TheChiSo icon="bi-water" mau="xanh-ngoc" so={duLieu ? `${soCoTau}/${soHoatDong}` : '…'} nhan={soTre ? `Bến có tàu · ${soTre} quá hạn ETD` : 'Bến đang có tàu'} />
        </div>
        <div className="col">
          <TheChiSo icon="bi-check2-circle" mau="la" so={duLieu ? soSanSang : '…'} nhan="Bến sẵn sàng tiếp nhận" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-cone-striped" mau={soBaoTri ? 'cam' : 'la'} so={duLieu ? soBaoTri : '…'} nhan="Bến bảo trì" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-truck-flatbed" mau="xanh" so={duLieu ? dinhDangSo(tongCau) : '…'} nhan="Cẩu bờ sẵn dùng" />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Sơ đồ cầu bến trực quan"
        icon="bi-broadcast"
        phai={
          <>
            <NhanTrangThai giaTri="TRỰC TIẾP" mau="run" icon="bi-circle-fill" />
            <Link className="btn btn-sm btn-outline-secondary" to="/admin/berths/gantt">
              <i className="bi bi-bar-chart-steps me-1" />
              Gantt cầu bến
            </Link>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(0)}>
              <i className="bi bi-plus-lg me-1" />
              Thêm bến mới
            </button>
          </>
        }
      >
        {dangTai && !duLieu ? (
          <DangTai />
        ) : ds.length === 0 ? (
          <div className="text-center text-body-secondary py-4">
            <i className="bi bi-inbox me-2" />
            Chưa có bến nào.
          </div>
        ) : (
          <div className="tb-cau-ben">
            <div className="tb-cau-ben-hang">
              {ds.map((b) => {
                const tt = tinhTrang(b)
                const c = b.chuyenDangCap
                return (
                  <Link
                    key={b.maBen}
                    to={'/admin/vesselcalls?maBen=' + b.maBen}
                    className={'tb-ben ' + tt}
                    title={(c ? `${c.maChuyenHieu} · ${c.tenTau || ''} · ${c.trangThai}` : b.tenBen) + ' — xem các chuyến của bến'}
                  >
                    <div className="tb-ben-tau">{c && <TauNho />}</div>
                    <div className="tb-ben-cau">
                      <div className="tb-ben-ten">{b.tenBen}</div>
                      <div className="tb-ben-phu">
                        {soMet(b.chieuDai)}m · {soMet(b.doSau)}m · {b.soCau} cẩu
                      </div>
                      <span className="tb-ben-nhan">
                        {tt === 'bao-tri' ? (
                          <>
                            <i className="bi bi-tools me-1" />
                            Bảo trì
                          </>
                        ) : c ? (
                          <>
                            <i className="bi bi-life-preserver me-1" />
                            {c.tenTau || c.maChuyenHieu}
                            {b.soTauDangCap > 1 ? ` +${b.soTauDangCap - 1}` : ''}
                          </>
                        ) : (
                          <>
                            <i className="bi bi-check2 me-1" />
                            Sẵn sàng
                          </>
                        )}
                      </span>
                    </div>
                  </Link>
                )
              })}
            </div>
          </div>
        )}
      </TheNoiDung>

      <div className="row g-3 mb-3">
        {ds.map((b) => {
          const c = b.chuyenDangCap
          const k = b.chuyenKeTiep
          const tt = tinhTrang(b)
          const mauIcon = tt === 'bao-tri' ? 'cam' : tt === 'trong' ? 'la' : tt === 'tre' ? 'do' : 'xanh-ngoc'
          return (
            <div className="col-md-6 col-xl-4" key={b.maBen}>
              <div className="card sp-the h-100">
                <div className="card-body">
                  <div className="d-flex justify-content-between align-items-start gap-2">
                    <div className="d-flex gap-3 align-items-center min-w-0">
                      <div className={'sp-chi-so-icon ' + mauIcon}>
                        <i className="bi bi-bounding-box" />
                      </div>
                      <div className="min-w-0">
                        <div className="fw-bold fs-5 text-truncate">{b.tenBen}</div>
                        <NhanTrangThai giaTri={b.trangThai} />
                      </div>
                    </div>
                    <div className="text-nowrap">
                      <Link className="btn btn-sm btn-outline-secondary me-1" to={'/admin/vesselcalls?maBen=' + b.maBen} title="Các chuyến của bến" aria-label="Các chuyến của bến">
                        <i className="bi bi-list-ul" />
                      </Link>
                      <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label="Sửa" onClick={() => moForm(b.maBen)}>
                        <i className="bi bi-pencil" />
                      </button>
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-danger"
                        title={b.soChuyen ? `Bến đã có ${b.soChuyen} chuyến tàu sử dụng — không xóa được` : 'Xóa'}
                        aria-label="Xóa"
                        disabled={b.soChuyen > 0 || dangChay}
                        onClick={() => xoa(b)}
                      >
                        <i className="bi bi-trash" />
                      </button>
                    </div>
                  </div>

                  <div className="row text-center mt-3 g-2">
                    <div className="col-4">
                      <div className="tb-o-ben-so">{soMet(b.chieuDai)} m</div>
                      <div className="tb-o-ben-nhan">Chiều dài</div>
                    </div>
                    <div className="col-4">
                      <div className="tb-o-ben-so">{soMet(b.doSau)} m</div>
                      <div className="tb-o-ben-nhan">Độ sâu</div>
                    </div>
                    <div className="col-4">
                      <div className="tb-o-ben-so">{b.soCau}</div>
                      <div className="tb-o-ben-nhan">Cẩu bờ</div>
                    </div>
                  </div>

                  <div className="mt-3 pt-2 border-top small">
                    {c ? (
                      <div className="text-truncate" title={`${c.maChuyenHieu} · ETD ${dinhDangNgayGio(c.etd)}`}>
                        <i className={'bi bi-water me-1 ' + (c.quaHan ? 'text-danger' : 'text-info')} />
                        <strong>{c.tenTau}</strong>
                        <span className="text-body-secondary"> · {c.maChuyenHieu} · {c.trangThai.toLowerCase()}</span>
                        {c.quaHan && <span className="badge text-bg-danger ms-1">Quá ETD {gioNgayNgan(c.etd)}</span>}
                      </div>
                    ) : b.trangThai === 'Hoạt động' ? (
                      <div>
                        <i className="bi bi-check2-circle text-success me-1" />
                        <span className="text-body-secondary">Bến trống — sẵn sàng tiếp nhận tàu</span>
                      </div>
                    ) : (
                      <div className="text-truncate" title={b.moTa || ''}>
                        <i className="bi bi-cone-striped text-warning me-1" />
                        <span className="text-body-secondary">{b.moTa || 'Đang bảo trì'}</span>
                      </div>
                    )}
                    {k && (
                      <div className="text-truncate text-body-secondary mt-1" title={`${k.maChuyenHieu} · ${k.tenTau || ''}`}>
                        <i className="bi bi-calendar-event me-1" />
                        Kế tiếp: {k.maChuyenHieu} · ETA {gioNgayNgan(k.eta)}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )
        })}
      </div>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        icon="bi-bounding-box"
        tieuDe={dangSua ? 'Sửa ' + tenGoc : 'Thêm bến mới'}
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={dongForm} disabled={dangChay}>
              Quay lại
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay || dangTaiForm}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu
            </button>
          </>
        }
      >
        {dangTaiForm ? (
          <DangTai chu="Đang tải thông tin bến…" />
        ) : (
          <div className="row g-3">
            <Truong nhan="Tên bến" batBuoc lop="col-sm-7">
              <input className="form-control" value={gt.tenBen} onChange={doi('tenBen')} maxLength={50} placeholder="VD: Bến B7" autoFocus />
            </Truong>
            <Truong nhan="Trạng thái" batBuoc lop="col-sm-5">
              <select className="form-select" value={gt.trangThai} onChange={doi('trangThai')}>
                {TRANG_THAI_BEN.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Chiều dài (m)" batBuoc lop="col-4">
              <input type="number" step="0.1" min={0} max={5000} className="form-control" value={gt.chieuDai} onChange={doi('chieuDai')} />
            </Truong>
            <Truong nhan="Độ sâu (m)" batBuoc lop="col-4">
              <input type="number" step="0.1" min={0} max={50} className="form-control" value={gt.doSau} onChange={doi('doSau')} />
            </Truong>
            <Truong nhan="Số cẩu bờ" batBuoc lop="col-4">
              <input type="number" min={0} max={50} className="form-control" value={gt.soCau} onChange={doi('soCau')} />
            </Truong>
            <Truong nhan="Mô tả / công năng" lop="col-12">
              <input className="form-control" value={gt.moTa} onChange={doi('moTa')} maxLength={200} />
            </Truong>
          </div>
        )}
      </HopThoai>
    </>
  )
}
