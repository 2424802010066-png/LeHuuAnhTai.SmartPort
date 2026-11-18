import { useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, OTimKiem, PhanTrang, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, tienGon } from '../../utils/dinhDang'
import { API_KHACH, duongHoSoKhach, useFormTheoDiaChi } from './chung'
import './kinh-doanh.css'

const RONG = { tenCongTy: '', maSoThue: '', diaChi: '', nguoiLienHe: '', email: '', soDienThoai: '' }

/** Chi lay dung cac truong cua form (khong gui kem so lieu thong ke len API) */
const chiTruongForm = (k) => Object.fromEntries(Object.keys(RONG).map((t) => [t, k?.[t] ?? '']))

/** Danh ba khach hang doanh nghiep: tim, them, sua, xoa, mo ho so (thay Admin/Customers). */
export default function KhachHang() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi(API_KHACH + taoQuery(loc))
  const { dangSua, moForm, dongForm } = useFormTheoDiaChi('/admin/customers')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const daNap = useRef(null)

  // Nap du lieu vao form khi dia chi doi (bam Sua, hoac vao thang /admin/customers/form/5)
  useEffect(() => {
    if (dangSua === null) {
      daNap.current = null
      return
    }
    if (daNap.current === dangSua) return
    daNap.current = dangSua
    if (!dangSua) {
      datLai(RONG)
      return
    }
    const co = duLieu?.items?.find((k) => k.maKH === dangSua)
    if (co) {
      datLai(chiTruongForm(co))
      return
    }
    // Vao thang bang link: lay ho so khach hang tu API
    goiApi(API_KHACH + '/' + dangSua)
      .then((k) => datLai(chiTruongForm(k)))
      .catch(() => datLai(RONG))
  }, [dangSua, duLieu, datLai])

  async function luu(e) {
    e.preventDefault()
    const than = Object.fromEntries(Object.entries(gt).map(([k, v]) => [k, typeof v === 'string' ? v.trim() : v]))
    const kq = await chay(() => (dangSua ? goiApi(API_KHACH + '/' + dangSua, { method: 'PUT', body: than }) : goiApi(API_KHACH, { method: 'POST', body: than })))
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function xoa(k) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa khách hàng?',
      noiDung: `Xóa ${k.tenCongTy}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(API_KHACH + '/' + k.maKH, { method: 'DELETE' }))) {
      if (duLieu?.items?.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      taiLai({ im: true })
    }
  }

  const COT = [
    {
      khoa: 'congTy',
      ten: 'Công ty',
      hien: (k) => (
        <>
          <Link className="fw-semibold" to={duongHoSoKhach(k.maKH)} title="Mở hồ sơ khách hàng">
            {k.tenCongTy}
          </Link>
          <div className="text-body-secondary small">{k.diaChi || '—'}</div>
        </>
      ),
    },
    { khoa: 'mst', ten: 'MST', an: 'md', hien: (k) => k.maSoThue || '—' },
    { khoa: 'lienHe', ten: 'Người liên hệ', an: 'md', hien: (k) => k.nguoiLienHe || '—' },
    { khoa: 'dienThoai', ten: 'Điện thoại', an: 'lg', lop: 'text-nowrap', hien: (k) => k.soDienThoai || '—' },
    { khoa: 'email', ten: 'Email', an: 'xl', lop: 'small', hien: (k) => k.email || '—' },
    {
      khoa: 'booking',
      ten: 'Booking',
      canPhai: true,
      hien: (k) => (
        <>
          {dinhDangSo(k.soBooking)}
          <div className="text-body-secondary small text-nowrap">{tienGon(k.giaTriBooking)} đ</div>
        </>
      ),
    },
    { khoa: 'cont', ten: 'Container', canPhai: true, an: 'lg', hien: (k) => dinhDangSo(k.soContainer) },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (k) => (
        <>
          <Link className="btn btn-sm btn-outline-secondary me-1" to={duongHoSoKhach(k.maKH)} title="Hồ sơ khách hàng" aria-label="Hồ sơ khách hàng">
            <i className="bi bi-folder2-open" />
          </Link>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label="Sửa" onClick={() => moForm(k.maKH)}>
            <i className="bi bi-pencil" />
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa" aria-label="Xóa" disabled={dangChay} onClick={() => xoa(k)}>
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe={`Khách hàng${duLieu ? ` · ${dinhDangSo(duLieu.tong)}` : ''}`}
        icon="bi-buildings"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Tên công ty / MST / người liên hệ" />
            {loc.q && (
              <button type="button" className="btn btn-sm btn-light" onClick={xoaLoc}>
                Xóa lọc
              </button>
            )}
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm()}>
              <i className="bi bi-plus-lg me-1" />
              Thêm khách hàng
            </button>
          </>
        }
        chan={duLieu && <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="khách hàng" />}
      >
        <BangDuLieu cot={COT} dong={duLieu?.items} khoaDong="maKH" dangTai={dangTai} rong={loc.q ? 'Không tìm thấy khách hàng.' : 'Chưa có khách hàng nào.'} />
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="lg"
        tieuDe={dangSua ? 'Sửa khách hàng' : 'Thêm khách hàng'}
        icon="bi-buildings"
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={dongForm} disabled={dangChay}>
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu
            </button>
          </>
        }
      >
        <div className="row g-3">
          <Truong nhan="Tên công ty" batBuoc lop="col-md-8">
            <input className="form-control" value={gt.tenCongTy} onChange={doi('tenCongTy')} maxLength={150} autoFocus />
          </Truong>
          <Truong nhan="Mã số thuế" lop="col-md-4">
            <input className="form-control" value={gt.maSoThue} onChange={doi('maSoThue')} maxLength={20} />
          </Truong>
          <Truong nhan="Địa chỉ" lop="col-12">
            <input className="form-control" value={gt.diaChi} onChange={doi('diaChi')} maxLength={200} />
          </Truong>
          <Truong nhan="Người liên hệ" lop="col-md-4">
            <input className="form-control" value={gt.nguoiLienHe} onChange={doi('nguoiLienHe')} maxLength={100} />
          </Truong>
          <Truong nhan="Email" lop="col-md-4">
            <input type="email" className="form-control" value={gt.email} onChange={doi('email')} maxLength={100} />
          </Truong>
          <Truong nhan="Số điện thoại" lop="col-md-4">
            <input className="form-control" value={gt.soDienThoai} onChange={doi('soDienThoai')} maxLength={20} />
          </Truong>
        </div>
      </HopThoai>
    </>
  )
}
