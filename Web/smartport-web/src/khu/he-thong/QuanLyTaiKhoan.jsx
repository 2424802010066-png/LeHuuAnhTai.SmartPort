import { useEffect, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import {
  BangDuLieu,
  BaoLoi,
  HopThoai,
  NhanTrangThai,
  OTimKiem,
  PhanTrang,
  TheChiSo,
  TheNoiDung,
  Truong,
  useThongBao,
  useXacNhan,
} from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo } from '../../utils/dinhDang'
import './he-thong.css'

const VAI_TRO = {
  Admin: { ten: 'Quản trị viên', mau: 'run' },
  NhanVien: { ten: 'Nhân viên', mau: 'info' },
  KhachHang: { ten: 'Khách hàng', mau: 'ok' },
}

const LUA_CHON_VAI_TRO = [
  { ma: 'Admin', ten: 'Quản trị viên (Admin)' },
  { ma: 'NhanVien', ten: 'Nhân viên khai thác' },
  { ma: 'KhachHang', ten: 'Khách hàng' },
]

const RONG = { tenDangNhap: '', matKhauMoi: '', hoTen: '', vaiTro: 'NhanVien', email: '', soDienThoai: '', maKH: '', kichHoat: true }
const MAT_KHAU_MAC_DINH = '123456'
const CO_TRANG = 50

/**
 * Quan ly tai khoan (thay Admin/Accounts): danh sach + loc vai tro/trang thai, tao/sua trong hop thoai,
 * dat lai mat khau, khoa/mo, xoa. Dia chi cu /admin/accounts/form/:id? mo thang hop thoai tuong ung.
 */
export default function QuanLyTaiKhoan() {
  const { id } = useParams()
  const viTri = useLocation()
  const dieuHuong = useNavigate()
  const laDiaChiForm = /\/form(\/|$)/i.test(viTri.pathname)

  const [loc, datLoc, xoaLoc] = useBoLoc({ vaiTro: '', trangThai: '', q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/he-thong/tai-khoan' + taoQuery({ ...loc, coTrang: CO_TRANG }))

  const [dangSua, setDangSua] = useState(null) // null = dong form, 0 = tao moi, >0 = ma tai khoan dang sua
  const [dongDangSua, setDongDangSua] = useState(null)
  const [canKhachHang, setCanKhachHang] = useState(false)
  const { duLieu: dsKhachHang } = useApi(canKhachHang ? '/api/he-thong/tai-khoan/khach-hang' : null)
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()

  function moForm(t) {
    setCanKhachHang(true) // danh sach doanh nghiep chi tai khi mo form lan dau
    setDongDangSua(t)
    datLai(
      t
        ? {
            tenDangNhap: t.tenDangNhap || '',
            matKhauMoi: '',
            hoTen: t.hoTen || '',
            vaiTro: t.vaiTro || 'NhanVien',
            email: t.email || '',
            soDienThoai: t.soDienThoai || '',
            maKH: t.maKH != null ? String(t.maKH) : '',
            kichHoat: !!t.kichHoat,
          }
        : RONG,
    )
    setDangSua(t ? t.maTK : 0)
  }

  function dongForm() {
    setDangSua(null)
    setDongDangSua(null)
    if (laDiaChiForm) dieuHuong('/admin/accounts', { replace: true })
  }

  // Dia chi cu /Admin/Accounts/Form/5 (bookmark, link tro ly): mo hop thoai sua / tao ngay tren danh sach
  useEffect(() => {
    if (!laDiaChiForm) return undefined
    let huy = false
    if (!id) {
      moForm(null)
      return undefined
    }
    goiApi('/api/he-thong/tai-khoan/' + encodeURIComponent(id))
      .then((t) => {
        if (!huy) moForm(t)
      })
      .catch((e) => {
        if (!huy) {
          bao.loi(e)
          dieuHuong('/admin/accounts', { replace: true })
        }
      })
    return () => {
      huy = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [laDiaChiForm, id])

  async function luu(e) {
    e.preventDefault()
    const than = {
      tenDangNhap: dangSua ? null : gt.tenDangNhap.trim(),
      matKhauMoi: gt.matKhauMoi ? gt.matKhauMoi : null,
      hoTen: gt.hoTen.trim(),
      vaiTro: gt.vaiTro,
      email: chuoiHoacNull(gt.email.trim()),
      soDienThoai: chuoiHoacNull(gt.soDienThoai.trim()),
      maKH: gt.vaiTro === 'KhachHang' ? soHoacNull(gt.maKH) : null,
      kichHoat: gt.kichHoat,
    }
    const kq = await chay(() =>
      dangSua
        ? goiApi('/api/he-thong/tai-khoan/' + dangSua, { method: 'PUT', body: than })
        : goiApi('/api/he-thong/tai-khoan', { method: 'POST', body: than }),
    )
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function datLaiMatKhau(t) {
    const dongY = await xacNhan({
      tieuDe: 'Đặt lại mật khẩu?',
      noiDung: `Đặt lại mật khẩu của ${t.tenDangNhap} về ${MAT_KHAU_MAC_DINH}?`,
      nutDongY: 'Đặt lại',
      mau: 'warning',
    })
    if (dongY) await chay(() => goiApi(`/api/he-thong/tai-khoan/${t.maTK}/dat-lai-mat-khau`, { method: 'POST' }))
  }

  async function khoaMo(t) {
    if (await chay(() => goiApi(`/api/he-thong/tai-khoan/${t.maTK}/khoa-mo`, { method: 'POST' }))) taiLai({ im: true })
  }

  async function xoa(t) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa tài khoản?',
      noiDung: `Xóa tài khoản ${t.tenDangNhap}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/he-thong/tai-khoan/' + t.maTK, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const tk = d?.thongKe
  const coLoc = !!(loc.vaiTro || loc.trangThai || loc.q)
  const laToi = !!dangSua && !!dongDangSua?.laToi
  const laKhachHang = gt.vaiTro === 'KhachHang'

  // The chi so kiem nut loc nhanh: bam lan nua de bo loc
  const theLoc = (dangChon, thayDoi, noiDung) => (
    <button
      type="button"
      className={'ht-the-bam' + (dangChon ? ' dang-chon' : '')}
      aria-pressed={dangChon}
      onClick={() => datLoc({ vaiTro: '', trangThai: '', ...(dangChon ? {} : thayDoi), trang: 1 })}
    >
      {noiDung}
    </button>
  )

  const COT = [
    {
      khoa: 'ten',
      ten: 'Tên đăng nhập',
      hien: (t) => (
        <>
          <span className="fw-semibold">{t.tenDangNhap}</span>
          {t.laToi && <span className="badge text-bg-primary ms-1">Bạn</span>}
        </>
      ),
    },
    { khoa: 'hoTen', ten: 'Họ tên', hien: (t) => t.hoTen },
    {
      khoa: 'vaiTro',
      ten: 'Vai trò',
      hien: (t) => <NhanTrangThai mau={VAI_TRO[t.vaiTro]?.mau || 'off'}>{VAI_TRO[t.vaiTro]?.ten || t.vaiTro}</NhanTrangThai>,
    },
    {
      khoa: 'khachHang',
      ten: 'Khách hàng liên kết',
      an: 'lg',
      hien: (t) =>
        t.tenKhachHang ? (
          <span className="small">{t.tenKhachHang}</span>
        ) : t.vaiTro === 'KhachHang' ? (
          <span className="small text-warning-emphasis">Chưa gắn doanh nghiệp</span>
        ) : (
          '—'
        ),
    },
    {
      khoa: 'lienLac',
      ten: 'Email / SĐT',
      an: 'md',
      hien: (t) => (
        <span className="small">
          {t.email || '—'}
          {t.soDienThoai && (
            <>
              <br />
              {t.soDienThoai}
            </>
          )}
        </span>
      ),
    },
    {
      khoa: 'trangThai',
      ten: 'Trạng thái',
      hien: (t) =>
        t.kichHoat ? (
          <NhanTrangThai mau="ok">Hoạt động</NhanTrangThai>
        ) : (
          <NhanTrangThai mau="off" icon="bi-lock-fill">
            Bị khóa
          </NhanTrangThai>
        ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 176,
      lop: 'text-nowrap',
      hien: (t) => (
        <>
          <button
            type="button"
            className="btn btn-sm btn-outline-warning me-1"
            title={`Đặt lại mật khẩu về ${MAT_KHAU_MAC_DINH}`}
            aria-label={`Đặt lại mật khẩu của ${t.tenDangNhap}`}
            disabled={dangChay}
            onClick={() => datLaiMatKhau(t)}
          >
            <i className="bi bi-key" />
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-secondary me-1"
            title={t.laToi ? 'Không thể khóa tài khoản đang đăng nhập' : t.kichHoat ? 'Khóa' : 'Mở khóa'}
            aria-label={(t.kichHoat ? 'Khóa ' : 'Mở khóa ') + t.tenDangNhap}
            disabled={t.laToi || dangChay}
            onClick={() => khoaMo(t)}
          >
            <i className={'bi ' + (t.kichHoat ? 'bi-lock' : 'bi-unlock')} />
          </button>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label={'Sửa ' + t.tenDangNhap} onClick={() => moForm(t)}>
            <i className="bi bi-pencil" />
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-danger"
            title={
              t.laToi
                ? 'Không thể xóa tài khoản đang đăng nhập'
                : t.coKhuonMat
                  ? 'Còn dữ liệu khuôn mặt — hãy khóa thay vì xóa'
                  : 'Xóa'
            }
            aria-label={'Xóa ' + t.tenDangNhap}
            disabled={t.laToi || t.coKhuonMat || dangChay}
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

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          {theLoc(false, {}, <TheChiSo icon="bi-people" mau="xanh" so={dinhDangSo(tk?.tong)} nhan="Tài khoản" />)}
        </div>
        <div className="col-6 col-xl-3">
          {theLoc(
            loc.vaiTro === 'Admin',
            { vaiTro: 'Admin' },
            <TheChiSo icon="bi-person-gear" mau="xanh-ngoc" so={dinhDangSo(tk?.quanTri)} nhan={`Quản trị · ${dinhDangSo(tk?.nhanVien)} nhân viên`} />,
          )}
        </div>
        <div className="col-6 col-xl-3">
          {theLoc(loc.vaiTro === 'KhachHang', { vaiTro: 'KhachHang' }, <TheChiSo icon="bi-buildings" mau="la" so={dinhDangSo(tk?.khachHang)} nhan="Khách hàng" />)}
        </div>
        <div className="col-6 col-xl-3">
          {theLoc(
            loc.trangThai === 'Khoa',
            { trangThai: 'Khoa' },
            <TheChiSo icon="bi-lock" mau={tk?.biKhoa ? 'do' : 'cam'} so={dinhDangSo(tk?.biKhoa)} nhan="Bị khóa" />,
          )}
        </div>
      </div>

      <TheNoiDung
        tieuDe="Tài khoản hệ thống"
        icon="bi-people"
        khongDem
        phai={
          <>
            <select
              className="form-select form-select-sm w-auto"
              value={loc.vaiTro}
              onChange={(e) => datLoc({ vaiTro: e.target.value, trang: 1 })}
              aria-label="Lọc theo vai trò"
            >
              <option value="">Mọi vai trò</option>
              {Object.entries(VAI_TRO).map(([ma, v]) => (
                <option key={ma} value={ma}>
                  {v.ten}
                </option>
              ))}
            </select>
            <select
              className="form-select form-select-sm w-auto"
              value={loc.trangThai}
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
              aria-label="Lọc theo trạng thái"
            >
              <option value="">Mọi trạng thái</option>
              <option value="HoatDong">Hoạt động</option>
              <option value="Khoa">Bị khóa</option>
            </select>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Tên, email, SĐT, khách hàng…" rong={240} />
            {coLoc && (
              <button type="button" className="btn btn-sm btn-link px-1" onClick={xoaLoc}>
                Xóa lọc
              </button>
            )}
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(null)}>
              <i className="bi bi-plus-lg me-1" />
              Tạo tài khoản
            </button>
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={d?.items}
          khoaDong="maTK"
          dangTai={dangTai}
          lopDong={(t) => (t.kichHoat ? '' : 'ht-dong-khoa')}
          rong={coLoc ? 'Không có tài khoản khớp bộ lọc.' : 'Chưa có tài khoản nào.'}
        />
        {d && (
          <div className="px-3 pb-3">
            <PhanTrang trang={d.trang} soTrang={d.soTrang} tong={d.tong} donVi="tài khoản" doiTrang={(t) => datLoc({ trang: t })} />
          </div>
        )}
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="lg"
        tieuDe={dangSua ? 'Sửa tài khoản ' + gt.tenDangNhap : 'Tạo tài khoản'}
        icon="bi-person-gear"
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
          <Truong nhan="Tên đăng nhập" batBuoc={!dangSua} lop="col-md-6" goiY={dangSua ? 'Không đổi được tên đăng nhập.' : 'Chữ không dấu, số và . _ - @'}>
            <input
              className="form-control"
              value={gt.tenDangNhap}
              onChange={doi('tenDangNhap')}
              readOnly={!!dangSua}
              maxLength={50}
              pattern="[A-Za-z0-9_.@\-]+"
              title="Chữ không dấu, số và . _ - @"
              autoComplete="off"
              autoFocus={!dangSua}
            />
          </Truong>
          <Truong nhan={dangSua ? 'Mật khẩu mới (trống = giữ nguyên)' : `Mật khẩu (trống = ${MAT_KHAU_MAC_DINH})`} lop="col-md-6" goiY="Tối thiểu 6 ký tự.">
            <input className="form-control" type="password" value={gt.matKhauMoi} onChange={doi('matKhauMoi')} autoComplete="new-password" minLength={6} maxLength={50} />
          </Truong>
          <Truong nhan="Họ tên" batBuoc lop="col-md-6">
            <input className="form-control" value={gt.hoTen} onChange={doi('hoTen')} maxLength={100} autoFocus={!!dangSua} />
          </Truong>
          <Truong nhan="Vai trò" lop="col-md-6" goiY={laToi ? 'Không tự đổi vai trò của tài khoản đang đăng nhập.' : undefined}>
            <select className="form-select" value={gt.vaiTro} onChange={doi('vaiTro')} disabled={laToi}>
              {LUA_CHON_VAI_TRO.map((v) => (
                <option key={v.ma} value={v.ma}>
                  {v.ten}
                </option>
              ))}
            </select>
          </Truong>
          <Truong nhan="Email" lop="col-md-6">
            <input className="form-control" type="email" value={gt.email} onChange={doi('email')} maxLength={100} autoComplete="off" />
          </Truong>
          <Truong nhan="Số điện thoại" lop="col-md-6">
            <input className="form-control" type="tel" value={gt.soDienThoai} onChange={doi('soDienThoai')} maxLength={20} autoComplete="off" />
          </Truong>
          {laKhachHang && (
            <Truong nhan="Khách hàng liên kết" lop="col-md-8" goiY="Để trống nếu chưa gắn hồ sơ doanh nghiệp.">
              <select className="form-select" value={gt.maKH} onChange={doi('maKH')}>
                <option value="">— Chọn doanh nghiệp —</option>
                {(dsKhachHang || []).map((k) => (
                  <option key={k.giaTri} value={k.giaTri}>
                    {k.nhan}
                  </option>
                ))}
              </select>
            </Truong>
          )}
          <div className={(laKhachHang ? 'col-md-4' : 'col-12') + ' d-flex align-items-end'}>
            <div className="form-check form-switch mb-2">
              <input
                className="form-check-input"
                type="checkbox"
                role="switch"
                id="ht-tai-khoan-kich-hoat"
                checked={gt.kichHoat}
                onChange={doi('kichHoat')}
                disabled={laToi}
              />
              <label className="form-check-label" htmlFor="ht-tai-khoan-kich-hoat">
                Kích hoạt
              </label>
            </div>
          </div>
        </div>
      </HopThoai>
    </>
  )
}
