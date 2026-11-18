import { useEffect, useMemo, useRef, useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, TheChiSo, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, dinhDangTien, tienGon } from '../../utils/dinhDang'
import { API_DICH_VU, NHOM_DICH_VU, khongDau, useFormTheoDiaChi } from './chung'
import './kinh-doanh.css'

const RONG = { tenDV: '', nhomDV: 'Bốc xếp', donViTinh: '', donGia: '', moTa: '', kichHoat: true }

const chiTruongForm = (d) => ({
  tenDV: d?.tenDV ?? '',
  nhomDV: d?.nhomDV || 'Bốc xếp',
  donViTinh: d?.donViTinh ?? '',
  donGia: d?.donGia ?? '',
  moTa: d?.moTa ?? '',
  kichHoat: d?.kichHoat ?? true,
})

/** Danh muc dich vu cang va bang gia: them, sua, ngung cung cap / xoa (thay Admin/Services). */
export default function DichVuGia() {
  const { duLieu, loi, dangTai, taiLai } = useApi(API_DICH_VU)
  const { dangSua, moForm, dongForm } = useFormTheoDiaChi('/admin/services')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [tuKhoa, setTuKhoa] = useState('')
  const [nhom, setNhom] = useState('')
  const daNap = useRef(null)

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
    const co = duLieu?.find((d) => d.maDV === dangSua)
    if (co) {
      datLai(chiTruongForm(co))
      return
    }
    goiApi(API_DICH_VU + '/' + dangSua)
      .then((d) => datLai(chiTruongForm(d)))
      .catch(() => datLai(RONG))
  }, [dangSua, duLieu, datLai])

  // Danh muc nho (vai chuc dong) -> loc ngay tren trinh duyet, khong goi lai API
  const dsLoc = useMemo(() => {
    const k = khongDau(tuKhoa)
    return (duLieu || []).filter((d) => (!nhom || d.nhomDV === nhom) && (!k || khongDau(`${d.tenDV} ${d.nhomDV} ${d.donViTinh} ${d.moTa}`).includes(k)))
  }, [duLieu, tuKhoa, nhom])

  const tk = useMemo(() => {
    const ds = duLieu || []
    return {
      tong: ds.length,
      dangCungCap: ds.filter((d) => d.kichHoat).length,
      doanhThu: ds.reduce((s, d) => s + (d.doanhThu || 0), 0),
      caoNhat: ds.reduce((a, d) => (a && a.doanhThu >= (d.doanhThu || 0) ? a : d), null),
    }
  }, [duLieu])

  const cacNhom = useMemo(() => [...new Set([...NHOM_DICH_VU, ...(duLieu || []).map((d) => d.nhomDV).filter(Boolean)])], [duLieu])

  async function luu(e) {
    e.preventDefault()
    const than = { ...gt, tenDV: gt.tenDV.trim(), donGia: gt.donGia === '' ? null : Number(gt.donGia) }
    const kq = await chay(() => (dangSua ? goiApi(API_DICH_VU + '/' + dangSua, { method: 'PUT', body: than }) : goiApi(API_DICH_VU, { method: 'POST', body: than })))
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function xoa(d) {
    const coBooking = d.soLanDat > 0
    const dongY = await xacNhan({
      tieuDe: coBooking ? 'Ngừng cung cấp dịch vụ?' : 'Xóa dịch vụ?',
      noiDung: coBooking
        ? `${d.tenDV} đã có ${d.soLanDat} booking nên không xóa được — hệ thống sẽ chuyển sang trạng thái ngừng cung cấp.`
        : `Xóa ${d.tenDV}. Thao tác này không hoàn tác được.`,
      nutDongY: coBooking ? 'Ngừng cung cấp' : 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(API_DICH_VU + '/' + d.maDV, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'ten',
      ten: 'Tên dịch vụ',
      hien: (d) => (
        <>
          <span className="fw-semibold">{d.tenDV}</span>
          <div className="text-body-secondary small">{d.moTa || '—'}</div>
        </>
      ),
    },
    { khoa: 'nhom', ten: 'Nhóm', an: 'md', hien: (d) => d.nhomDV || '—' },
    { khoa: 'dvt', ten: 'Đơn vị', an: 'lg', hien: (d) => d.donViTinh || '—' },
    { khoa: 'gia', ten: 'Đơn giá', canPhai: true, lop: 'kd-gia text-nowrap', hien: (d) => dinhDangTien(d.donGia) },
    {
      khoa: 'datHang',
      ten: 'Đã đặt',
      canPhai: true,
      hien: (d) => (
        <>
          {dinhDangSo(d.soLanDat)}
          <div className="text-body-secondary small text-nowrap">{tienGon(d.doanhThu)} đ</div>
        </>
      ),
    },
    { khoa: 'trangThai', ten: 'Trạng thái', canPhai: true, hien: (d) => <NhanTrangThai giaTri={d.kichHoat ? 'Đang cung cấp' : 'Ngừng'} mau={d.kichHoat ? 'ok' : 'off'} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (d) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label="Sửa" onClick={() => moForm(d.maDV)}>
            <i className="bi bi-pencil" />
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-danger"
            title={d.soLanDat > 0 ? 'Đã có booking — chuyển sang ngừng cung cấp' : 'Xóa dịch vụ'}
            aria-label="Xóa hoặc ngừng cung cấp"
            disabled={dangChay}
            onClick={() => xoa(d)}
          >
            <i className={'bi ' + (d.soLanDat > 0 ? 'bi-slash-circle' : 'bi-trash')} />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-4">
          <TheChiSo icon="bi-tags" mau="xanh" so={dinhDangSo(tk.dangCungCap)} nhan={`Dịch vụ đang cung cấp / ${tk.tong} dịch vụ`} />
        </div>
        <div className="col-6 col-xl-4">
          <TheChiSo icon="bi-cash-coin" mau="la" so={tienGon(tk.doanhThu)} nhan="Doanh thu đã ghi nhận (đ)" />
        </div>
        <div className="col-12 col-xl-4">
          <TheChiSo icon="bi-trophy" mau="cam" nho so={tk.caoNhat?.tenDV || '—'} nhan={tk.caoNhat ? `Dịch vụ bán chạy nhất · ${tienGon(tk.caoNhat.doanhThu)} đ` : 'Chưa có doanh thu'} />
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe="Danh mục dịch vụ cảng"
        icon="bi-tools"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={tuKhoa} doi={setTuKhoa} goiY="Tên dịch vụ, mô tả…" cho={0} />
            <select className="form-select form-select-sm" style={{ width: 160 }} value={nhom} onChange={(e) => setNhom(e.target.value)} aria-label="Lọc theo nhóm dịch vụ">
              <option value="">— Tất cả nhóm —</option>
              {cacNhom.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm()}>
              <i className="bi bi-plus-lg me-1" />
              Thêm dịch vụ
            </button>
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={dsLoc}
          khoaDong="maDV"
          dangTai={dangTai}
          lopDong={(d) => (d.kichHoat ? '' : 'kd-dong-ngung')}
          rong={tuKhoa || nhom ? 'Không có dịch vụ khớp bộ lọc.' : 'Chưa có dịch vụ nào.'}
        />
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="lg"
        tieuDe={dangSua ? 'Sửa dịch vụ' : 'Thêm dịch vụ'}
        icon="bi-tools"
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
          <Truong nhan="Tên dịch vụ" batBuoc lop="col-md-8">
            <input className="form-control" value={gt.tenDV} onChange={doi('tenDV')} maxLength={100} autoFocus />
          </Truong>
          <Truong nhan="Nhóm dịch vụ" lop="col-md-4">
            <select className="form-select" value={gt.nhomDV} onChange={doi('nhomDV')}>
              {cacNhom.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </Truong>
          <Truong nhan="Đơn vị tính" goiY="Cont / Tấn / Chuyến…" lop="col-md-4">
            <input className="form-control" value={gt.donViTinh} onChange={doi('donViTinh')} maxLength={30} />
          </Truong>
          <Truong nhan="Đơn giá (đ)" batBuoc lop="col-md-4">
            <input type="number" min="0" step="1000" className="form-control" value={gt.donGia} onChange={doi('donGia')} />
          </Truong>
          <div className="col-md-4 d-flex align-items-center">
            <div className="form-check form-switch mt-3">
              <input className="form-check-input" type="checkbox" id="kd-dv-kich-hoat" checked={!!gt.kichHoat} onChange={doi('kichHoat')} />
              <label className="form-check-label" htmlFor="kd-dv-kich-hoat">
                Đang cung cấp
              </label>
            </div>
          </div>
          <Truong nhan="Mô tả" lop="col-12">
            <textarea className="form-control" rows={3} value={gt.moTa} onChange={doi('moTa')} maxLength={300} />
          </Truong>
        </div>
      </HopThoai>
    </>
  )
}
