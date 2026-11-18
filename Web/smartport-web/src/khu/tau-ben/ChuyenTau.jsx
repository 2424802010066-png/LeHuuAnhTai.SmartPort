import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo, ngayGioInput } from '../../utils/dinhDang'
import { TRANG_THAI_CHUYEN, loiXepBen, soMet, useFormTheoDiaChi } from './chung'
import './tau-ben.css'

const DUONG = '/admin/vesselcalls'

const RONG = { maChuyenHieu: '', maTau: '', maBen: '', eta: '', etd: '', ata: '', atd: '', trangThai: 'Dự kiến', contNhap: '0', contXuat: '0', ghiChu: '' }

// Nut chuyen trang thai nhanh — hien dung nhu danh sach MVC
const NUT = {
  capben: { nhan: 'Cập bến', icon: 'bi-box-arrow-in-down', lop: 'btn-outline-info', goiY: 'Xác nhận tàu cập bến (ghi ATA = bây giờ)' },
  lamhang: { nhan: 'Làm hàng', icon: 'bi-boxes', lop: 'btn-outline-info', goiY: 'Bắt đầu làm hàng' },
  roiben: { nhan: 'Rời bến', icon: 'bi-box-arrow-up', lop: 'btn-outline-success', goiY: 'Tàu rời bến (ghi ATD = bây giờ)' },
}

function nutCua(c) {
  if (c.trangThai === 'Dự kiến') return ['capben']
  if (c.trangThai === 'Đã cập bến') return ['lamhang', 'roiben']
  if (c.trangThai === 'Đang làm hàng') return ['roiben']
  return []
}

/** Chuyen moi: ETA 8h sang mai, ETD 18h ngay kia (nhu form MVC) */
function formMoi() {
  const eta = new Date()
  eta.setDate(eta.getDate() + 1)
  eta.setHours(8, 0, 0, 0)
  const etd = new Date(eta)
  etd.setDate(etd.getDate() + 1)
  etd.setHours(18, 0, 0, 0)
  return { ...RONG, eta: ngayGioInput(eta), etd: ngayGioInput(etd) }
}

const tuDong = (c) => ({
  maChuyenHieu: c.maChuyenHieu || '',
  maTau: c.maTau ? String(c.maTau) : '',
  maBen: c.maBen ? String(c.maBen) : '',
  eta: ngayGioInput(c.eta),
  etd: ngayGioInput(c.etd),
  ata: ngayGioInput(c.ata),
  atd: ngayGioInput(c.atd),
  trangThai: c.trangThai || 'Dự kiến',
  contNhap: String(c.contNhap ?? 0),
  contXuat: String(c.contXuat ?? 0),
  ghiChu: c.ghiChu || '',
})

const thanGui = (gt) => ({
  maChuyenHieu: gt.maChuyenHieu,
  maTau: soHoacNull(gt.maTau),
  maBen: soHoacNull(gt.maBen),
  eta: gt.eta || null,
  etd: gt.etd || null,
  ata: gt.ata || null,
  atd: gt.atd || null,
  trangThai: gt.trangThai,
  contNhap: soHoacNull(gt.contNhap),
  contXuat: soHoacNull(gt.contXuat),
  ghiChu: gt.ghiChu,
})

function NutLoc({ chon, nhan, so, onClick, mau = 'primary' }) {
  return (
    <button type="button" className={'btn btn-sm rounded-pill ' + (chon ? 'btn-' + mau : 'btn-outline-secondary')} onClick={onClick} aria-pressed={chon}>
      {nhan}
      {so != null && <span className={'badge rounded-pill ms-2 ' + (chon ? 'text-bg-light' : 'text-bg-secondary')}>{dinhDangSo(so)}</span>}
    </button>
  )
}

/** Dieu do chuyen tau: loc, cap ben / lam hang / roi ben, them - sua - xoa (thay Admin/VesselCalls cua ban MVC). */
export default function ChuyenTau() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', q: '', maTau: '', maBen: '', chuaPhanBen: false, trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/chuyen-tau' + taoQuery({ ...loc, chuaPhanBen: loc.chuaPhanBen ? 'true' : '' }))
  const { duLieu: tk, taiLai: taiLaiTk } = useApi('/api/chuyen-tau/thong-ke')
  const { duLieu: luaChon, loi: loiLuaChon } = useApi('/api/chuyen-tau/lua-chon')

  const { dangSua, moForm, dongForm } = useFormTheoDiaChi(DUONG)
  const { gt, doi, datLai } = useBieuMau(RONG)
  const [goc, setGoc] = useState(null) // chuyen dang sua (ban doc tu API) — tieu de + ben dang bao tri
  const [dangTaiForm, setDangTaiForm] = useState(false)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()

  const dongFormMoiNhat = useRef(dongForm)
  useEffect(() => {
    dongFormMoiNhat.current = dongForm
  })

  // Mo form theo dia chi: /form -> chuyen moi, /form/5 -> doc chuyen #5 tu API
  useEffect(() => {
    setGoc(null)
    setDangTaiForm(false)
    if (dangSua === null) return undefined
    if (dangSua === 0) {
      datLai(formMoi())
      return undefined
    }
    let conHieuLuc = true
    setDangTaiForm(true)
    goiApi('/api/chuyen-tau/' + dangSua)
      .then((c) => {
        if (!conHieuLuc) return
        setGoc(c)
        datLai(tuDong(c))
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

  const lamMoi = () => {
    taiLai({ im: true })
    taiLaiTk({ im: true })
  }

  async function luu(e) {
    e.preventDefault()
    const than = thanGui(gt)
    const kq = await chay(() =>
      dangSua ? goiApi('/api/chuyen-tau/' + dangSua, { method: 'PUT', body: than }) : goiApi('/api/chuyen-tau', { method: 'POST', body: than }),
    )
    if (kq) {
      dongForm()
      lamMoi()
    }
  }

  async function doiTrangThai(c, hanhDong) {
    if (hanhDong === 'huy') {
      const dongY = await xacNhan({
        tieuDe: 'Hủy chuyến tàu?',
        noiDung: `Chuyển chuyến ${c.maChuyenHieu} (${c.tenTau || 'tàu'}) sang trạng thái Hủy.`,
        nutDongY: 'Hủy chuyến',
        mau: 'danger',
      })
      if (!dongY) return
    }
    if (await chay(() => goiApi(`/api/chuyen-tau/${c.maChuyen}/trang-thai`, { method: 'POST', body: { hanhDong } }))) lamMoi()
  }

  async function xoa(c) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa chuyến tàu?',
      noiDung: `Xóa chuyến ${c.maChuyenHieu}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/chuyen-tau/' + c.maChuyen, { method: 'DELETE' }))) lamMoi()
  }

  // ------------------------- Form: canh bao som -------------------------
  const dsTau = luaChon?.tau || []
  const dsBen = [...(luaChon?.ben || [])]
  // Chuyen dang xep vao ben bao tri: van hien ben do trong o chon (khong am tham bo phan ben khi luu)
  if (goc?.maBen && !dsBen.some((b) => b.maBen === goc.maBen)) {
    dsBen.push({ maBen: goc.maBen, tenBen: goc.tenBen || 'Bến #' + goc.maBen, trangThai: goc.trangThaiBen || 'Bảo trì' })
  }
  const tauChon = dsTau.find((t) => String(t.maTau) === gt.maTau)
  const benChon = dsBen.find((b) => String(b.maBen) === gt.maBen)
  const canhBaoBen = benChon?.chieuDai != null ? loiXepBen(benChon, tauChon, luaChon?.dayBuoc, luaChon?.chanAnToan) : []
  const loiEtd = gt.eta && gt.etd && gt.etd <= gt.eta ? 'ETD phải sau ETA.' : null
  const loiAtd = gt.ata && gt.atd && gt.atd <= gt.ata ? 'ATD phải sau ATA.' : null

  // ------------------------- Bang -------------------------
  const tenLoc = (ma, ds, khoa, ten) => ds?.find((x) => String(x[khoa]) === String(ma))?.[ten]
  const tenTauLoc = loc.maTau && (tenLoc(loc.maTau, luaChon?.tau, 'maTau', 'tenTau') || duLieu?.items?.[0]?.tenTau || 'Tàu #' + loc.maTau)
  const tenBenLoc = loc.maBen && (tenLoc(loc.maBen, luaChon?.ben, 'maBen', 'tenBen') || duLieu?.items?.[0]?.tenBen || 'Bến #' + loc.maBen)
  const coLoc = !!(loc.trangThai || loc.q || loc.maTau || loc.maBen || loc.chuaPhanBen)
  const soTheoTrangThai = (t) => tk?.theoTrangThai?.find((n) => n.nhom === t)?.soLuong

  const COT = [
    {
      khoa: 'chuyen',
      ten: 'Chuyến',
      hien: (c) => (
        <>
          <div className="fw-semibold text-nowrap">{c.maChuyenHieu}</div>
          {c.ghiChu && (
            <div className="small text-body-secondary text-truncate" style={{ maxWidth: 220 }} title={c.ghiChu}>
              {c.ghiChu}
            </div>
          )}
        </>
      ),
    },
    { khoa: 'tau', ten: 'Tàu', hien: (c) => c.tenTau || '—' },
    {
      khoa: 'ben',
      ten: 'Bến',
      hien: (c) =>
        c.maBen ? (
          <span className="text-nowrap">
            {c.tenBen}
            {c.trangThaiBen && c.trangThaiBen !== 'Hoạt động' && (
              <i className="bi bi-cone-striped text-warning ms-1" title={'Bến đang ' + c.trangThaiBen.toLowerCase()} aria-label={'Bến đang ' + c.trangThaiBen.toLowerCase()} />
            )}
          </span>
        ) : c.trangThai === 'Dự kiến' ? (
          <Link to="/admin/aitools/berth" className="text-decoration-none" title="Mở AI gợi ý phân bổ bến">
            <NhanTrangThai giaTri="Chưa phân bến" mau="warn" />
          </Link>
        ) : (
          '—'
        ),
    },
    {
      khoa: 'eta',
      ten: 'ETA / ATA',
      hien: (c) => (
        <div className="text-nowrap">
          <div>{dinhDangNgayGio(c.eta)}</div>
          {c.ata && (
            <div className="small text-success">
              <i className="bi bi-check2" /> {dinhDangNgayGio(c.ata)}
            </div>
          )}
        </div>
      ),
    },
    {
      khoa: 'etd',
      ten: 'ETD / ATD',
      hien: (c) => (
        <div className="text-nowrap">
          <div className={c.quaHan ? 'text-danger fw-semibold' : ''}>
            {dinhDangNgayGio(c.etd)}
            {c.quaHan && <span className="badge text-bg-danger ms-1">Trễ</span>}
          </div>
          {c.atd && (
            <div className="small text-success">
              <i className="bi bi-check2" /> {dinhDangNgayGio(c.atd)}
            </div>
          )}
        </div>
      ),
    },
    { khoa: 'cont', ten: 'Cont N/X', canPhai: true, an: 'lg', hien: (c) => <span className="text-nowrap">{dinhDangSo(c.contNhap)} / {dinhDangSo(c.contXuat)}</span> },
    { khoa: 'trangThai', ten: 'Trạng thái', hien: (c) => <NhanTrangThai giaTri={c.trangThai} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      hien: (c) => (
        <div className="d-inline-flex flex-wrap justify-content-end gap-1">
          {nutCua(c).map((k) => (
            <button
              key={k}
              type="button"
              className={'btn btn-sm text-nowrap ' + NUT[k].lop}
              title={NUT[k].goiY}
              aria-label={NUT[k].nhan}
              disabled={dangChay}
              onClick={() => doiTrangThai(c, k)}
            >
              <i className={'bi ' + NUT[k].icon} />
              <span className="d-none d-xl-inline ms-1">{NUT[k].nhan}</span>
            </button>
          ))}
          {c.trangThai === 'Dự kiến' && (
            <button type="button" className="btn btn-sm btn-outline-secondary" title="Hủy chuyến" aria-label="Hủy chuyến" disabled={dangChay} onClick={() => doiTrangThai(c, 'huy')}>
              <i className="bi bi-x-circle" />
            </button>
          )}
          <button type="button" className="btn btn-sm btn-outline-primary" title="Sửa" aria-label="Sửa" onClick={() => moForm(c.maChuyen)}>
            <i className="bi bi-pencil" />
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-danger"
            title={c.soContainer ? `Có ${c.soContainer} container liên kết — không xóa được` : 'Xóa'}
            aria-label="Xóa"
            disabled={c.soContainer > 0 || dangChay}
            onClick={() => xoa(c)}
          >
            <i className="bi bi-trash" />
          </button>
        </div>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row row-cols-2 row-cols-md-3 row-cols-xl-5 g-3 mb-3">
        <div className="col">
          <TheChiSo icon="bi-water" mau="xanh-ngoc" so={tk ? dinhDangSo(tk.trongCang) : '…'} nhan="Tàu trong cảng" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-calendar2-week" mau="xanh" so={tk ? dinhDangSo(tk.denTrong7Ngay) : '…'} nhan="Chuyến đến trong 7 ngày" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-box-seam" mau="tim" so={tk ? dinhDangSo(tk.contDenTrong7Ngay) : '…'} nhan="Container dự kiến 7 ngày" />
        </div>
        <div className="col">
          <Link to="/admin/aitools/berth" className="tb-the-link" title="Mở AI gợi ý phân bổ bến">
            <TheChiSo icon="bi-stars" mau={tk?.chuaPhanBen ? 'cam' : 'la'} so={tk ? dinhDangSo(tk.chuaPhanBen) : '…'} nhan="Chưa phân bến · AI gợi ý" />
          </Link>
        </div>
        <div className="col">
          <Link to="/admin/berths/gantt" className="tb-the-link" title="Xem Gantt cầu bến">
            <TheChiSo icon="bi-alarm" mau={tk?.quaHanEtd ? 'do' : 'la'} so={tk ? dinhDangSo(tk.quaHanEtd) : '…'} nhan="Quá hạn ETD chưa rời" />
          </Link>
        </div>
      </div>

      <TheNoiDung
        tieuDe="Chuyến tàu"
        icon="bi-calendar2-week"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Mã chuyến, tên tàu…" />
            <Link className="btn btn-sm btn-outline-secondary" to="/admin/berths/gantt" title="Gantt cầu bến" aria-label="Gantt cầu bến">
              <i className="bi bi-bar-chart-steps" />
              <span className="d-none d-lg-inline ms-1">Gantt</span>
            </Link>
            <Link className="btn btn-sm btn-outline-primary" to="/admin/aitools/berth">
              <i className="bi bi-stars me-1" />
              AI phân bổ bến
            </Link>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(0)}>
              <i className="bi bi-plus-lg me-1" />
              Thêm chuyến
            </button>
          </>
        }
      >
        <div className="d-flex flex-wrap align-items-center gap-2 px-3 py-2 border-bottom">
          <NutLoc chon={!loc.trangThai && !loc.chuaPhanBen} nhan="Tất cả" so={tk?.tong} onClick={() => datLoc({ trangThai: '', chuaPhanBen: false, trang: 1 })} />
          {TRANG_THAI_CHUYEN.map((t) => (
            <NutLoc key={t} chon={loc.trangThai === t} nhan={t} so={soTheoTrangThai(t)} onClick={() => datLoc({ trangThai: t, chuaPhanBen: false, trang: 1 })} />
          ))}
          <NutLoc
            chon={loc.chuaPhanBen}
            mau="warning"
            nhan="Chưa phân bến"
            so={tk?.chuaPhanBen}
            onClick={() => datLoc({ chuaPhanBen: !loc.chuaPhanBen, trangThai: '', trang: 1 })}
          />
          {tenTauLoc && (
            <button type="button" className="btn btn-sm btn-info rounded-pill" onClick={() => datLoc({ maTau: '', trang: 1 })} title="Bỏ lọc theo tàu">
              <i className="bi bi-water me-1" />
              {tenTauLoc}
              <i className="bi bi-x-lg ms-2" />
            </button>
          )}
          {tenBenLoc && (
            <button type="button" className="btn btn-sm btn-info rounded-pill" onClick={() => datLoc({ maBen: '', trang: 1 })} title="Bỏ lọc theo bến">
              <i className="bi bi-bounding-box me-1" />
              {tenBenLoc}
              <i className="bi bi-x-lg ms-2" />
            </button>
          )}
        </div>

        <BangDuLieu
          cot={COT}
          dong={duLieu?.items}
          khoaDong="maChuyen"
          dangTai={dangTai}
          rong={coLoc ? 'Không có chuyến khớp bộ lọc.' : 'Không có chuyến nào.'}
        />
        <div className="px-3 pb-3">
          <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="chuyến" />
        </div>
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="lg"
        icon="bi-calendar2-week"
        tieuDe={dangSua ? 'Sửa chuyến ' + (goc?.maChuyenHieu || '') : 'Thêm chuyến tàu'}
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={dongForm} disabled={dangChay}>
              Quay lại
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay || dangTaiForm}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu chuyến
            </button>
          </>
        }
      >
        {dangTaiForm ? (
          <DangTai chu="Đang tải chuyến tàu…" />
        ) : (
          <div className="row g-3">
            {loiLuaChon && (
              <div className="col-12">
                <BaoLoi loi={loiLuaChon} />
              </div>
            )}
            <Truong nhan="Mã chuyến" batBuoc lop="col-md-4">
              <input className="form-control text-uppercase" value={gt.maChuyenHieu} onChange={doi('maChuyenHieu')} maxLength={20} placeholder="VD: MSCU-2609N" autoFocus />
            </Truong>
            <Truong
              nhan="Tàu"
              batBuoc
              lop="col-md-4"
              goiY={tauChon ? `LOA ${soMet(tauChon.chieuDai)} m · mớn nước ${soMet(tauChon.monNuoc)} m · ${dinhDangSo(tauChon.sucChuaTEU)} TEU` : null}
            >
              <select className="form-select" value={gt.maTau} onChange={doi('maTau')}>
                <option value="">— Chọn tàu —</option>
                {dsTau.map((t) => (
                  <option key={t.maTau} value={t.maTau}>
                    {t.tenTau}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong
              nhan={
                <>
                  Bến
                  <Link to="/admin/aitools/berth" className="fw-normal small ms-2">
                    <i className="bi bi-stars me-1" />
                    AI gợi ý
                  </Link>
                </>
              }
              lop="col-md-4"
              loi={canhBaoBen.length ? canhBaoBen.join(' ') : null}
              goiY={
                benChon && tauChon ? (
                  <span className="text-success">
                    <i className="bi bi-check2-circle me-1" />
                    Đủ dài, đủ sâu cho tàu
                  </span>
                ) : null
              }
            >
              <select className="form-select" value={gt.maBen} onChange={doi('maBen')}>
                <option value="">— Chưa phân bến —</option>
                {dsBen.map((b) => (
                  <option key={b.maBen} value={b.maBen}>
                    {b.tenBen}
                    {b.chieuDai != null ? ` · dài ${soMet(b.chieuDai)} m · sâu ${soMet(b.doSau)} m` : ''}
                    {b.trangThai && b.trangThai !== 'Hoạt động' ? ` (${b.trangThai})` : ''}
                  </option>
                ))}
              </select>
            </Truong>

            <Truong nhan="ETA (dự kiến đến)" batBuoc lop="col-sm-6">
              <input type="datetime-local" className="form-control" value={gt.eta} onChange={doi('eta')} />
            </Truong>
            <Truong nhan="ETD (dự kiến rời)" batBuoc lop="col-sm-6" loi={loiEtd}>
              <input type="datetime-local" className="form-control" value={gt.etd} onChange={doi('etd')} />
            </Truong>
            <Truong nhan="ATA (thực tế đến)" lop="col-sm-6">
              <input type="datetime-local" className="form-control" value={gt.ata} onChange={doi('ata')} />
            </Truong>
            <Truong nhan="ATD (thực tế rời)" lop="col-sm-6" loi={loiAtd}>
              <input type="datetime-local" className="form-control" value={gt.atd} onChange={doi('atd')} />
            </Truong>

            <Truong nhan="Trạng thái" batBuoc lop="col-sm-6 col-md-4">
              <select className="form-select" value={gt.trangThai} onChange={doi('trangThai')}>
                {TRANG_THAI_CHUYEN.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Container nhập" lop="col-6 col-md-4">
              <input type="number" min={0} max={100000} className="form-control" value={gt.contNhap} onChange={doi('contNhap')} />
            </Truong>
            <Truong nhan="Container xuất" lop="col-6 col-md-4">
              <input type="number" min={0} max={100000} className="form-control" value={gt.contXuat} onChange={doi('contXuat')} />
            </Truong>
            <Truong nhan="Ghi chú" lop="col-12">
              <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={200} />
            </Truong>
          </div>
        )}
      </HopThoai>
    </>
  )
}
