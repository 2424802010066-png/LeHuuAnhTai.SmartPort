import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangNgay, dinhDangSo, tienGon } from '../../utils/dinhDang'
import { API_CONT, TRANG_THAI_MAC_DINH, duongForm, duongHoSo, soDo, useGiaoKhach } from './chung'
import './container.css'

/** The chi so bam duoc: loc nhanh theo trang thai hoac sang man hinh lien quan */
function TheBam({ den, dangChon, ...the }) {
  return (
    <Link className={'cb-the-lien-ket' + (dangChon ? ' dang-chon' : '')} to={den}>
      <TheChiSo {...the} />
    </Link>
  )
}

/** Danh sach container: loc, thong ke nhanh, gate out, ho so 360°, sua, xoa (thay Admin/Containers/Index). */
export default function DanhSachContainer() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', trangThai: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi(API_CONT + taoQuery(loc))
  const { duLieu: tk, taiLai: taiLaiThongKe } = useApi(API_CONT + '/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  const lamMoi = useCallback(() => {
    taiLai({ im: true })
    taiLaiThongKe({ im: true })
  }, [taiLai, taiLaiThongKe])

  // Xe qua cong theo lenh -> module Bai doi trang thai container -> bang tu cap nhat
  useNgheSuKien('bai.*', lamMoi)
  const { giaoKhach, dangGiao } = useGiaoKhach(lamMoi)

  const soTheo = (tt) => tk?.theoTrangThai?.find((x) => x.nhom === tt)?.soLuong ?? 0
  const cacTrangThai = [...new Set([...(tk?.theoTrangThai?.map((x) => x.nhom) ?? TRANG_THAI_MAC_DINH), ...(loc.trangThai ? [loc.trangThai] : [])])]
  const dangLoc = !!(loc.q || loc.trangThai)

  async function xoa(c) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa container?',
      noiDung: `Xóa container ${c.soCont}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(`${API_CONT}/${c.maCont}`, { method: 'DELETE' }))) {
      // Xoa dong cuoi cua trang cuoi -> lui ve trang truoc cho khoi hien bang trong
      if (duLieu?.items?.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      lamMoi()
    }
  }

  const COT = [
    {
      khoa: 'soCont',
      ten: 'Số container',
      hien: (c) => (
        <>
          <Link className="cb-so-cont" to={duongHoSo(c.maCont)} title="Mở hồ sơ container 360°">
            {c.soCont}
          </Link>
          {c.nhietDo != null && (
            <span className="cb-lanh ms-2" title="Container lạnh — nhiệt độ cài đặt">
              <i className="bi bi-thermometer-snow" /> {soDo(c.nhietDo)}°C
            </span>
          )}
        </>
      ),
    },
    {
      khoa: 'loai',
      ten: 'Loại',
      hien: (c) => (
        <>
          {c.loaiCont} <span className="text-body-secondary small">({c.tinhTrang || '—'})</span>
        </>
      ),
    },
    { khoa: 'hangHoa', ten: 'Hàng hóa', an: 'lg', lop: 'small', hien: (c) => c.hangHoa || '—' },
    { khoa: 'khachHang', ten: 'Khách hàng', an: 'md', lop: 'small', hien: (c) => c.khachHang || '—' },
    { khoa: 'viTri', ten: 'Vị trí bãi', hien: (c) => (c.viTriBai ? <span className="cb-vi-tri">{c.viTriBai}</span> : '—') },
    { khoa: 'vaoBai', ten: 'Vào bãi', an: 'md', lop: 'small text-nowrap', hien: (c) => dinhDangNgay(c.ngayVaoBai) },
    { khoa: 'trangThai', ten: 'Trạng thái', hien: (c) => <NhanTrangThai giaTri={c.trangThai} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (c) => (
        <>
          {c.trangThai === 'Trong bãi' && (
            <button type="button" className="btn btn-sm btn-outline-success me-1" title="Giao cho khách (gate out)" disabled={dangGiao} onClick={() => giaoKhach(c)}>
              <i className="bi bi-box-arrow-right" />
              <span className="d-none d-xl-inline ms-1">Gate out</span>
            </button>
          )}
          <Link className="btn btn-sm btn-outline-secondary me-1" to={duongHoSo(c.maCont)} title="Hồ sơ 360°" aria-label="Hồ sơ 360°">
            <i className="bi bi-folder2-open" />
          </Link>
          <Link className="btn btn-sm btn-outline-primary me-1" to={duongForm(c.maCont)} title="Sửa" aria-label="Sửa">
            <i className="bi bi-pencil" />
          </Link>
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa" aria-label="Xóa" disabled={dangChay} onClick={() => xoa(c)}>
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheBam
            den={'/admin/containers' + taoQuery({ trangThai: 'Trong bãi' })}
            dangChon={loc.trangThai === 'Trong bãi'}
            icon="bi-box-seam"
            mau="xanh"
            so={tk ? dinhDangSo(soTheo('Trong bãi')) : '…'}
            nhan={'Trong bãi' + (tk?.luuBaiQua15Ngay ? ` · ${tk.luuBaiQua15Ngay} cont quá 15 ngày` : '')}
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheBam
            den={'/admin/containers' + taoQuery({ trangThai: 'Chờ xuất tàu' })}
            dangChon={loc.trangThai === 'Chờ xuất tàu'}
            icon="bi-box-arrow-up-right"
            mau="cam"
            so={tk ? dinhDangSo(soTheo('Chờ xuất tàu')) : '…'}
            nhan="Chờ xuất tàu"
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheBam den="/admin/reefer" icon="bi-thermometer-snow" mau="xanh-ngoc" so={tk ? dinhDangSo(tk.contLanhTrongBai) : '…'} nhan="Container lạnh trong bãi" />
        </div>
        <div className="col-6 col-xl-3">
          <TheBam
            den="/admin/luubai"
            icon="bi-stopwatch"
            mau="do"
            so={tk ? tienGon(tk.tienLuuBaiDangChay) : '…'}
            nhan={`Tiền lưu bãi đang chạy (đ)${tk ? ` · ${tk.soContDangTinhPhi} cont` : ''}`}
          />
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe={`Container${tk ? ` · ${dinhDangSo(tk.tong)}` : ''}`}
        icon="bi-box-seam"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số cont, khách hàng, hàng hóa…" />
            <select
              className="form-select form-select-sm"
              style={{ width: 170 }}
              value={loc.trangThai}
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
              aria-label="Lọc theo trạng thái"
            >
              <option value="">— Trạng thái —</option>
              {cacTrangThai.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
            {dangLoc && (
              <button type="button" className="btn btn-sm btn-light" onClick={xoaLoc}>
                Xóa lọc
              </button>
            )}
            <Link className="btn btn-sm btn-outline-primary" to="/admin/containers/yardmap">
              <i className="bi bi-grid-3x3-gap me-1" />
              Sơ đồ bãi
            </Link>
            <Link className="btn btn-sm btn-primary" to={duongForm()}>
              <i className="bi bi-plus-lg me-1" />
              Thêm container
            </Link>
          </>
        }
        chan={duLieu && <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="container" />}
      >
        <BangDuLieu cot={COT} dong={duLieu?.items} khoaDong="maCont" dangTai={dangTai} rong={dangLoc ? 'Không tìm thấy container.' : 'Chưa có container nào.'} />
      </TheNoiDung>
    </>
  )
}
