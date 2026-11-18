import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, tienGon } from '../../utils/dinhDang'
import { API_DON, NUT_TRANG_THAI, TRANG_THAI_DON, duongChiTietDon } from './chung'
import './kinh-doanh.css'

/** The chi so bam duoc: loc nhanh danh sach theo trang thai */
function TheBam({ den, dangChon, ...the }) {
  return (
    <Link className={'kd-the-lien-ket' + (dangChon ? ' dang-chon' : '')} to={den}>
      <TheChiSo {...the} />
    </Link>
  )
}

/** Danh sach booking dich vu: loc trang thai, thong ke tien, duyet nhanh (thay Admin/Bookings/Index). */
export default function DonDatDichVu() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', trangThai: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi(API_DON + taoQuery(loc))
  const { duLieu: tk, taiLai: taiLaiThongKe } = useApi(API_DON + '/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  const lamMoi = useCallback(() => {
    taiLai({ im: true })
    taiLaiThongKe({ im: true })
  }, [taiLai, taiLaiThongKe])

  const theo = (tt) => tk?.theoTrangThai?.find((x) => x.trangThai === tt)
  const dangLoc = !!(loc.q || loc.trangThai)

  /** Nut buoc tiep chinh ngay tren dong (Duyet / Bat dau / Hoan thanh) */
  async function doiTrangThai(d, trangThai) {
    const nut = NUT_TRANG_THAI[trangThai]
    if (nut?.hoi) {
      const dongY = await xacNhan({ tieuDe: nut.nhan + '?', noiDung: `Booking ${d.maSoBooking}. ` + nut.hoi, nutDongY: nut.nhan, mau: 'danger' })
      if (!dongY) return
    }
    if (await chay(() => goiApi(`${API_DON}/${d.maBooking}/trang-thai`, { method: 'POST', body: { trangThai } }))) lamMoi()
  }

  const COT = [
    {
      khoa: 'maSo',
      ten: 'Số booking',
      hien: (d) => (
        <Link className="kd-ma-don" to={duongChiTietDon(d.maBooking)} title="Mở booking để xử lý">
          {d.maSoBooking}
        </Link>
      ),
    },
    {
      khoa: 'khach',
      ten: 'Khách hàng',
      hien: (d) => (
        <>
          <div className="fw-semibold">{d.tenKhachHang || '—'}</div>
          <div className="text-body-secondary small">{d.soDichVu} dịch vụ</div>
        </>
      ),
    },
    { khoa: 'ngayTao', ten: 'Ngày tạo', an: 'md', lop: 'small text-nowrap', hien: (d) => dinhDangNgayGio(d.ngayTao) },
    { khoa: 'ngayLam', ten: 'Cần thực hiện', an: 'lg', lop: 'small text-nowrap', hien: (d) => dinhDangNgay(d.ngayCanThucHien) },
    { khoa: 'tien', ten: 'Tổng tiền', canPhai: true, lop: 'fw-semibold text-nowrap', hien: (d) => tienGon(d.tongTien) },
    { khoa: 'trangThai', ten: 'Trạng thái', hien: (d) => <NhanTrangThai giaTri={d.trangThai} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (d) => {
        const buoc = d.trangThaiTiep?.[0]
        const nut = buoc ? NUT_TRANG_THAI[buoc] : null
        return (
          <>
            {nut && (
              <button type="button" className={'btn btn-sm btn-' + nut.mau + ' me-1'} disabled={dangChay} title={nut.nhan} onClick={() => doiTrangThai(d, buoc)}>
                <i className={'bi ' + nut.icon} />
                <span className="d-none d-xl-inline ms-1">{nut.nhan}</span>
              </button>
            )}
            <Link className="btn btn-sm btn-outline-primary" to={duongChiTietDon(d.maBooking)} title="Xử lý booking">
              <i className="bi bi-eye" />
              <span className="d-none d-lg-inline ms-1">Xử lý</span>
            </Link>
          </>
        )
      },
    },
  ]

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheBam
            den={'/admin/bookings' + taoQuery({ trangThai: 'Chờ duyệt' })}
            dangChon={loc.trangThai === 'Chờ duyệt'}
            icon="bi-hourglass-split"
            mau="cam"
            so={tk ? dinhDangSo(theo('Chờ duyệt')?.soDon ?? 0) : '…'}
            nhan={'Chờ duyệt' + (tk?.choDuyetQua48h ? ` · ${tk.choDuyetQua48h} đơn quá 48 giờ` : '')}
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheBam
            den={'/admin/bookings' + taoQuery({ trangThai: 'Đang thực hiện' })}
            dangChon={loc.trangThai === 'Đang thực hiện'}
            icon="bi-gear-wide-connected"
            mau="xanh"
            so={tk ? dinhDangSo(theo('Đang thực hiện')?.soDon ?? 0) : '…'}
            nhan="Đang thực hiện"
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-cash-coin" mau="la" so={tk ? tienGon(tk.giaTriDangMo) : '…'} nhan="Giá trị đơn đang mở (đ)" />
        </div>
        <div className="col-6 col-xl-3">
          <TheBam
            den={'/admin/bookings' + taoQuery({ trangThai: 'Hoàn thành' })}
            dangChon={loc.trangThai === 'Hoàn thành'}
            icon="bi-flag-fill"
            mau="xanh-ngoc"
            so={tk ? tienGon(theo('Hoàn thành')?.tongTien ?? 0) : '…'}
            nhan={`Đã hoàn thành (đ)${tk ? ` · ${theo('Hoàn thành')?.soDon ?? 0} đơn` : ''}`}
          />
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe={`Danh sách booking${tk ? ` · ${dinhDangSo(tk.tongDon)}` : ''}`}
        icon="bi-receipt-cutoff"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số booking, tên khách hàng…" />
            <select
              className="form-select form-select-sm"
              style={{ width: 190 }}
              value={loc.trangThai}
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
              aria-label="Lọc theo trạng thái"
            >
              <option value="">— Tất cả trạng thái —</option>
              {TRANG_THAI_DON.map((s) => (
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
          </>
        }
        chan={duLieu && <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="booking" />}
      >
        <BangDuLieu cot={COT} dong={duLieu?.items} khoaDong="maBooking" dangTai={dangTai} rong={dangLoc ? 'Không có booking khớp bộ lọc.' : 'Không có booking nào.'} />
      </TheNoiDung>
    </>
  )
}
