import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, HopThoai, NhanTrangThai, OTimKiem, TheChiSo, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo } from '../../utils/dinhDang'
import { LOAI_TAU, soMet, useFormTheoDiaChi } from './chung'
import './tau-ben.css'

const DUONG = '/admin/vessels'

const RONG = { tenTau: '', soIMO: '', maHangTau: '', loaiTau: 'Container', quocTich: '', chieuDai: '', monNuoc: '', sucChuaTEU: '' }

const tuDong = (t) => ({
  tenTau: t.tenTau || '',
  soIMO: t.soIMO || '',
  maHangTau: t.maHangTau ? String(t.maHangTau) : '',
  loaiTau: t.loaiTau || '',
  quocTich: t.quocTich || '',
  chieuDai: t.chieuDai ?? '',
  monNuoc: t.monNuoc ?? '',
  sucChuaTEU: t.sucChuaTEU ?? '',
})

const thanGui = (gt) => ({
  tenTau: gt.tenTau,
  soIMO: chuoiHoacNull(gt.soIMO),
  maHangTau: soHoacNull(gt.maHangTau),
  loaiTau: chuoiHoacNull(gt.loaiTau),
  quocTich: chuoiHoacNull(gt.quocTich),
  chieuDai: soHoacNull(gt.chieuDai),
  monNuoc: soHoacNull(gt.monNuoc),
  sucChuaTEU: soHoacNull(gt.sucChuaTEU),
})

/** Quan ly tau bien: tim theo ten / so IMO, loc theo hang, them - sua - xoa (thay Admin/Vessels cua ban MVC). */
export default function TauBien() {
  const [loc, datLoc] = useBoLoc({ q: '', maHangTau: '' })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tau-bien' + taoQuery(loc))
  const { duLieu: luaChon } = useApi('/api/tau-bien/lua-chon')

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
    setTenGoc('')
    setDangTaiForm(false)
    if (dangSua === null) return undefined
    if (dangSua === 0) {
      datLai(RONG)
      return undefined
    }
    let conHieuLuc = true
    setDangTaiForm(true)
    goiApi('/api/tau-bien/' + dangSua)
      .then((t) => {
        if (!conHieuLuc) return
        setTenGoc(t.tenTau)
        datLai(tuDong(t))
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

  const tongKet = useMemo(() => {
    const ds = duLieu || []
    return {
      soTau: ds.length,
      teu: ds.reduce((s, t) => s + (t.sucChuaTEU || 0), 0),
      trongCang: ds.filter((t) => t.chuyenTrongCang).length,
      soHang: new Set(ds.filter((t) => t.maHangTau).map((t) => t.maHangTau)).size,
    }
  }, [duLieu])

  async function luu(e) {
    e.preventDefault()
    const than = thanGui(gt)
    const kq = await chay(() =>
      dangSua ? goiApi('/api/tau-bien/' + dangSua, { method: 'PUT', body: than }) : goiApi('/api/tau-bien', { method: 'POST', body: than }),
    )
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function xoa(t) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa tàu?',
      noiDung: `Xóa tàu ${t.tenTau}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/tau-bien/' + t.maTau, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const dsHang = luaChon?.hangTau || []
  const dsLoai = gt.loaiTau && !LOAI_TAU.includes(gt.loaiTau) ? [...(luaChon?.loaiTau || LOAI_TAU), gt.loaiTau] : luaChon?.loaiTau || LOAI_TAU
  const coLoc = !!(loc.q || loc.maHangTau)

  const COT = [
    {
      khoa: 'ten',
      ten: 'Tên tàu',
      hien: (t) => (
        <>
          <span className="fw-semibold">{t.tenTau}</span>
          {t.chuyenTrongCang && (
            <span className="ms-2" title={'Chuyến ' + t.chuyenTrongCang + ' đang ở bến'}>
              <NhanTrangThai giaTri="Trong cảng" mau="run" icon="bi-water" />
            </span>
          )}
        </>
      ),
    },
    { khoa: 'imo', ten: 'Số IMO', hien: (t) => t.soIMO || '—' },
    { khoa: 'hang', ten: 'Hãng tàu', an: 'md', hien: (t) => t.tenHangTau || '—' },
    { khoa: 'loai', ten: 'Loại', an: 'lg', hien: (t) => t.loaiTau || '—' },
    { khoa: 'quocTich', ten: 'Quốc tịch', an: 'xl', hien: (t) => t.quocTich || '—' },
    { khoa: 'loa', ten: 'LOA (m)', canPhai: true, hien: (t) => soMet(t.chieuDai) },
    { khoa: 'mon', ten: 'Mớn nước (m)', canPhai: true, an: 'sm', hien: (t) => soMet(t.monNuoc) },
    { khoa: 'teu', ten: 'TEU', canPhai: true, an: 'sm', hien: (t) => dinhDangSo(t.sucChuaTEU) },
    {
      khoa: 'chuyen',
      ten: 'Chuyến',
      canPhai: true,
      an: 'md',
      hien: (t) =>
        t.soChuyen ? (
          <Link to={'/admin/vesselcalls?maTau=' + t.maTau} title="Xem các chuyến của tàu">
            {t.soChuyen}
          </Link>
        ) : (
          0
        ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 110,
      hien: (t) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label="Sửa" onClick={() => moForm(t.maTau)}>
            <i className="bi bi-pencil" />
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-danger"
            title={t.soChuyen ? 'Tàu đã có chuyến trong hệ thống — không xóa được' : 'Xóa'}
            aria-label="Xóa"
            disabled={t.soChuyen > 0 || dangChay}
            onClick={() => xoa(t)}
          >
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <TheChiSo icon="bi-water" mau="xanh" so={duLieu ? dinhDangSo(tongKet.soTau) : '…'} nhan={coLoc ? 'Tàu khớp bộ lọc' : 'Tàu đăng ký'} />
        </div>
        <div className="col">
          <TheChiSo icon="bi-box-seam" mau="tim" so={duLieu ? dinhDangSo(tongKet.teu) : '…'} nhan="Tổng sức chứa (TEU)" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-geo-alt" mau="xanh-ngoc" so={duLieu ? dinhDangSo(tongKet.trongCang) : '…'} nhan="Đang trong cảng" />
        </div>
        <div className="col">
          <Link to="/admin/shippinglines" className="tb-the-link" title="Danh mục hãng tàu">
            <TheChiSo icon="bi-globe2" mau="cam" so={duLieu ? dinhDangSo(tongKet.soHang) : '…'} nhan="Hãng tàu" />
          </Link>
        </div>
      </div>

      <TheNoiDung
        tieuDe="Tàu biển"
        icon="bi-water"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q })} goiY="Tên tàu, số IMO…" />
            <select
              className="form-select form-select-sm"
              style={{ maxWidth: 200 }}
              value={loc.maHangTau}
              onChange={(e) => datLoc({ maHangTau: e.target.value })}
              aria-label="Lọc theo hãng tàu"
            >
              <option value="">Mọi hãng tàu</option>
              {dsHang.map((h) => (
                <option key={h.giaTri} value={h.giaTri}>
                  {h.nhan}
                </option>
              ))}
            </select>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(0)}>
              <i className="bi bi-plus-lg me-1" />
              Thêm tàu
            </button>
          </>
        }
      >
        <BangDuLieu cot={COT} dong={duLieu} khoaDong="maTau" dangTai={dangTai} rong={coLoc ? 'Không có tàu khớp bộ lọc.' : 'Chưa có tàu nào.'} />
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="lg"
        icon="bi-water"
        tieuDe={dangSua ? 'Sửa tàu ' + tenGoc : 'Thêm tàu mới'}
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
          <DangTai chu="Đang tải thông tin tàu…" />
        ) : (
          <div className="row g-3">
            <Truong nhan="Tên tàu" batBuoc lop="col-md-8">
              <input className="form-control" value={gt.tenTau} onChange={doi('tenTau')} maxLength={100} autoFocus />
            </Truong>
            <Truong nhan="Số IMO" goiY="7 chữ số, vd 9776171" lop="col-md-4">
              <input className="form-control" value={gt.soIMO} onChange={doi('soIMO')} maxLength={15} inputMode="numeric" />
            </Truong>
            <Truong nhan="Hãng tàu" lop="col-md-6">
              <select className="form-select" value={gt.maHangTau} onChange={doi('maHangTau')}>
                <option value="">— Chọn hãng tàu —</option>
                {dsHang.map((h) => (
                  <option key={h.giaTri} value={h.giaTri}>
                    {h.nhan}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Loại tàu" lop="col-sm-6 col-md-3">
              <select className="form-select" value={gt.loaiTau} onChange={doi('loaiTau')}>
                {!gt.loaiTau && <option value="">—</option>}
                {dsLoai.map((l) => (
                  <option key={l} value={l}>
                    {l}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Quốc tịch" lop="col-sm-6 col-md-3">
              <input className="form-control" value={gt.quocTich} onChange={doi('quocTich')} maxLength={50} />
            </Truong>
            <Truong nhan="Chiều dài LOA (m)" lop="col-sm-4">
              <input type="number" step="0.1" min={0} max={600} className="form-control" value={gt.chieuDai} onChange={doi('chieuDai')} />
            </Truong>
            <Truong nhan="Mớn nước (m)" lop="col-sm-4">
              <input type="number" step="0.1" min={0} max={40} className="form-control" value={gt.monNuoc} onChange={doi('monNuoc')} />
            </Truong>
            <Truong nhan="Sức chứa (TEU)" lop="col-sm-4">
              <input type="number" min={0} max={50000} className="form-control" value={gt.sucChuaTEU} onChange={doi('sucChuaTEU')} />
            </Truong>
          </div>
        )}
      </HopThoai>
    </>
  )
}
