import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoBoKhoaGuiLap, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangTien, ngayInput } from '../../utils/dinhDang'
import { goTien, tienO, useLamMoiTheoSuKien } from './dungChung'
import './tai-chinh.css'

const THANG = 'Tháng'
const TRA_TRUOC = 'Trả trước'
const RONG = { bienSo: '', tenChuXe: '', dienThoai: '', maKH: '', loaiThe: TRA_TRUOC, hanDen: '', napThem: '', kichHoat: true, ghiChu: '' }

/** The xe qua cong: the thang (mien phi luot) / the tra truoc (tru dan) — thay Admin/PhiCong/TheXe cua ban MVC. */
export default function TheXe() {
  const [loc, datLoc] = useBoLoc({ q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/phi-cong/the-xe' + taoQuery(loc))
  const { duLieu: dm } = useApi('/api/thu-tien/danh-muc')
  const [dangSua, setDangSua] = useState(null) // null = dong, {} = cap the moi, the = sua / nap / gia han
  const { gt, doi, datGt, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  // Idempotency-Key cho cap the / sua-nap the: bam lai sau loi mang KHONG nap tien 2 lan (khoa theo noi dung + the)
  const [khoaGui] = useState(taoBoKhoaGuiLap)

  // So du the tra truoc bi tru khi xe ra cong (tai-chinh.phi-cong-da-xu-ly)
  useLamMoiTheoSuKien('tai-chinh.*', taiLai)

  const laSua = !!dangSua?.maThe

  function moForm(t) {
    datLai(
      t
        ? {
            bienSo: t.bienSo,
            tenChuXe: t.tenChuXe || '',
            dienThoai: t.dienThoai || '',
            maKH: t.maKH ? String(t.maKH) : '',
            loaiThe: t.loaiThe === THANG ? THANG : TRA_TRUOC,
            hanDen: ngayInput(t.hanDen),
            napThem: '',
            kichHoat: t.kichHoat,
            ghiChu: t.ghiChu || '',
          }
        : RONG,
    )
    setDangSua(t || {})
  }

  async function luu(e) {
    e.preventDefault()
    const than = {
      tenChuXe: gt.tenChuXe,
      dienThoai: gt.dienThoai,
      maKH: gt.maKH ? Number(gt.maKH) : null,
      loaiThe: gt.loaiThe,
      hanDen: gt.loaiThe === THANG && gt.hanDen ? gt.hanDen : null,
      napThem: gt.loaiThe === TRA_TRUOC ? gt.napThem : '',
      ghiChu: gt.ghiChu,
    }
    const kq = await chay(() =>
      laSua
        ? goiApi('/api/phi-cong/the-xe/' + dangSua.maThe, { method: 'PUT', body: { ...than, kichHoat: gt.kichHoat }, chongGuiLap: khoaGui })
        : goiApi('/api/phi-cong/the-xe', { method: 'POST', body: { ...than, bienSo: gt.bienSo }, chongGuiLap: khoaGui }),
    )
    if (kq) {
      setDangSua(null)
      taiLai({ im: true })
    }
  }

  async function xoa(t) {
    const conTien = t.loaiThe === TRA_TRUOC && t.soDu > 0
    const dongY = await xacNhan({
      tieuDe: `Xóa thẻ xe ${t.bienSo}?`,
      noiDung: conTien ? `Thẻ còn số dư ${dinhDangTien(t.soDu)} — hoàn lại cho khách trước khi xóa.` : 'Xe vẫn qua cổng được, chỉ là thu tiền mặt.',
      nutDongY: 'Xóa thẻ',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/phi-cong/the-xe/' + t.maThe, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'bien',
      ten: 'Biển số · chủ xe',
      hien: (t) => (
        <>
          <Link className="sp-bien-so text-decoration-none" to={'/admin/gate/hosoxe?bienSo=' + encodeURIComponent(t.bienSo)} title="Hồ sơ xe">
            {t.bienSo}
          </Link>
          <div className="small text-body-secondary">
            {t.tenChuXe || '—'}
            {t.tenKhach && <> · {t.tenKhach}</>}
          </div>
        </>
      ),
    },
    {
      khoa: 'loai',
      ten: 'Loại',
      hien: (t) => <span className={'badge ' + (t.loaiThe === THANG ? 'text-bg-primary' : 'text-bg-info')}>{t.loaiThe}</span>,
    },
    {
      khoa: 'soDu',
      ten: 'Số dư / hạn',
      canPhai: true,
      hien: (t) =>
        t.loaiThe === THANG ? (
          <>
            <div className="small">{t.hanDen ? dinhDangNgay(t.hanDen) : 'chưa đặt hạn'}</div>
            {t.hanDen && (
              <div className={'small ' + (t.soNgayConLai < 0 ? 'text-danger fw-bold' : t.soNgayConLai <= 7 ? 'text-warning fw-bold' : 'text-body-secondary')}>
                {t.soNgayConLai < 0 ? `quá hạn ${-t.soNgayConLai} ngày` : `còn ${t.soNgayConLai} ngày`}
              </div>
            )}
          </>
        ) : (
          <span className={'fw-bold text-nowrap ' + (t.soDu > 0 ? 'tc-tien' : 'text-danger')}>{dinhDangTien(t.soDu)}</span>
        ),
    },
    {
      khoa: 'tt',
      ten: 'Trạng thái',
      an: 'sm',
      hien: (t) =>
        t.conDung ? (
          <NhanTrangThai mau="ok" giaTri="Còn dùng" />
        ) : !t.kichHoat ? (
          <NhanTrangThai mau="off" giaTri="Đã khóa" />
        ) : (
          <NhanTrangThai mau="do" giaTri={t.loaiThe === THANG ? 'Hết hạn' : 'Hết tiền'} />
        ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 150,
      hien: (t) => (
        <div className="text-nowrap">
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => moForm(t)} disabled={dangChay}>
            <i className={'bi me-1 ' + (t.loaiThe === THANG ? 'bi-calendar-plus' : 'bi-plus-circle')} />
            {t.loaiThe === THANG ? 'Gia hạn' : 'Nạp'}
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger ms-1" title="Xóa thẻ" aria-label={'Xóa thẻ ' + t.bienSo} onClick={() => xoa(t)} disabled={dangChay}>
            <i className="bi bi-trash" />
          </button>
        </div>
      ),
    },
  ]

  const soNap = Number(String(gt.napThem).replace(/\D/g, '')) || 0

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      {d && (
        <div className="row g-3 mb-3">
          <div className="col-sm-6 col-xl-4">
            <TheChiSo icon="bi-safe" mau="tim" so={<span title={dinhDangTien(d.tongSoDu)}>{tienO(d.tongSoDu)}</span>} nhan="Số dư trả trước đang giữ hộ" />
          </div>
          <div className="col-sm-6 col-xl-4">
            <TheChiSo icon="bi-credit-card-2-front" mau="xanh" so={d.soTheHieuLuc} nhan="Thẻ đang hiệu lực" />
          </div>
        </div>
      )}

      <TheNoiDung
        tieuDe="Thẻ xe qua cổng"
        icon="bi-credit-card-2-front"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Biển số, tên chủ xe…" rong={240} />
            <Link className="btn btn-sm btn-outline-secondary" to="/admin/phicong">
              <i className="bi bi-tags me-1" />
              Biểu giá
            </Link>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(null)}>
              <i className="bi bi-plus-lg me-1" />
              Cấp thẻ
            </button>
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={d?.danhSach.items}
          khoaDong="maThe"
          dangTai={dangTai}
          lopDong={(t) => (t.kichHoat ? '' : 'tc-dong-huy')}
          rong={loc.q ? 'Không có thẻ nào khớp.' : 'Chưa có thẻ nào — xe không thẻ vẫn qua cổng, thu tiền mặt.'}
        />
        {d && (
          <div className="px-3 pb-2">
            <PhanTrang {...d.danhSach} doiTrang={(t) => datLoc({ trang: t })} donVi="thẻ" />
          </div>
        )}
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={() => setDangSua(null)}
        khoaDong={dangChay}
        tieuDe={laSua ? `Thẻ xe ${dangSua.bienSo}` : 'Cấp thẻ mới'}
        icon="bi-credit-card-2-front"
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setDangSua(null)} disabled={dangChay}>
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              {laSua ? 'Lưu' : 'Cấp thẻ'}
            </button>
          </>
        }
      >
        <div className="row g-3">
          {!laSua && (
            <Truong nhan="Biển số xe" batBuoc lop="col-12">
              <input className="form-control text-uppercase fw-bold" value={gt.bienSo} onChange={doi('bienSo')} maxLength={20} placeholder="51C-638.72" autoFocus />
            </Truong>
          )}

          <Truong nhan="Loại thẻ" lop="col-sm-5">
            <select className="form-select" value={gt.loaiThe} onChange={doi('loaiThe')}>
              <option value={TRA_TRUOC}>Trả trước</option>
              <option value={THANG}>Tháng</option>
            </select>
          </Truong>
          {gt.loaiThe === THANG ? (
            <Truong nhan={laSua ? 'Gia hạn đến' : 'Hạn đến'} lop="col-sm-7">
              <input type="date" className="form-control" value={gt.hanDen} onChange={doi('hanDen')} autoFocus={laSua} />
            </Truong>
          ) : (
            <Truong
              nhan={laSua ? 'Nạp thêm (đ)' : 'Nạp ban đầu (đ)'}
              lop="col-sm-7"
              goiY={laSua ? `Cộng dồn: ${dinhDangTien(dangSua.soDu)} → ${dinhDangTien((dangSua.soDu || 0) + soNap)}` : undefined}
            >
              <input
                className="form-control text-end fw-semibold"
                inputMode="numeric"
                value={gt.napThem}
                onChange={(e) => datGt({ napThem: goTien(e.target.value) })}
                placeholder="500.000"
                autoFocus={laSua}
              />
            </Truong>
          )}

          <Truong nhan="Chủ xe / nhà xe" lop="col-sm-7">
            <input className="form-control" value={gt.tenChuXe} onChange={doi('tenChuXe')} maxLength={150} />
          </Truong>
          <Truong nhan="Điện thoại" lop="col-sm-5">
            <input className="form-control" value={gt.dienThoai} onChange={doi('dienThoai')} maxLength={20} inputMode="tel" />
          </Truong>
          <Truong nhan="Khách hàng" lop="col-12">
            <select className="form-select" value={gt.maKH} onChange={doi('maKH')}>
              <option value="">— Không gắn công ty —</option>
              {(dm?.khachHang || []).map((k) => (
                <option key={k.giaTri} value={k.giaTri}>
                  {k.nhan}
                </option>
              ))}
            </select>
          </Truong>
          <Truong nhan="Ghi chú" lop="col-12">
            <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={300} />
          </Truong>
          {laSua && (
            <div className="col-12">
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="tc-the-kich-hoat" checked={gt.kichHoat} onChange={doi('kichHoat')} />
                <label className="form-check-label" htmlFor="tc-the-kich-hoat">
                  Thẻ đang hiệu lực
                </label>
              </div>
            </div>
          )}
        </div>
      </HopThoai>
    </>
  )
}
