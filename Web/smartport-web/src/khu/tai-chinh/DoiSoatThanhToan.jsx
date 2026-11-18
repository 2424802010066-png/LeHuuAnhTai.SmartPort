import { useState } from 'react'
import { Link } from 'react-router-dom'
import { taiFile, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangTien, gioNgayNgan, ngayInput } from '../../utils/dinhDang'
import { mauTrangThaiTT, PHUONG_THUC_TT, tienO, TRANG_THAI_TT, useLamMoiTheoSuKien } from './dungChung'
import './tai-chinh.css'

const homNay = () => ngayInput(new Date())
const truoc30Ngay = () => ngayInput(new Date(Date.now() - 30 * 86400000))

/** Doi soat thanh toan: doi chieu giao dich voi sao ke + xuat chung tu ban hang sang MISA (thay Admin/Payments). */
export default function DoiSoatThanhToan() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', phuongThuc: '', tuKhoa: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/doi-soat-thanh-toan' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const [tu, setTu] = useState(truoc30Ngay)
  const [den, setDen] = useState(homNay)

  // Ke toan khac vua xac nhan / cong vua sinh giao dich -> lam moi ngam
  useLamMoiTheoSuKien('tai-chinh.*', taiLai)

  const xuatMisa = () =>
    chay(() => taiFile('/api/doi-soat-thanh-toan/misa' + taoQuery({ tu, den }), { tenMacDinh: 'MISA_BanHang.xlsx' }), {
      thanhCong: 'Đã tải bảng kê chứng từ bán hàng cho MISA.',
    })

  const COT = [
    {
      khoa: 'ma',
      ten: 'Mã GD',
      rong: 130,
      hien: (t) => (
        <>
          <Link className="fw-semibold text-decoration-none" to={'/admin/payments/details/' + t.maTT}>
            #{t.maTT}
          </Link>
          {t.maGiaoDich && <div className="small text-body-secondary tc-ma">{t.maGiaoDich}</div>}
        </>
      ),
    },
    { khoa: 'booking', ten: 'Booking', hien: (t) => <span className="tc-ma">{t.maSoBooking || '—'}</span> },
    { khoa: 'khach', ten: 'Khách hàng', an: 'md', hien: (t) => <span className="small">{t.tenKhach || '—'}</span> },
    {
      khoa: 'pt',
      ten: 'Phương thức',
      an: 'lg',
      hien: (t) => (
        <div className="small">
          {t.tenPhuongThuc}
          {t.noiDungCK && <div className="text-body-secondary tc-ma">{t.noiDungCK}</div>}
        </div>
      ),
    },
    { khoa: 'tien', ten: 'Số tiền', canPhai: true, hien: (t) => <span className="fw-bold tc-tien text-nowrap">{dinhDangTien(t.soTien)}</span> },
    {
      khoa: 'ngay',
      ten: 'Ngày tạo',
      an: 'xl',
      hien: (t) => (
        <div className="small text-body-secondary text-nowrap">
          {gioNgayNgan(t.ngayTao)}
          {t.ngayThanhToan && <div>thu: {gioNgayNgan(t.ngayThanhToan)}</div>}
        </div>
      ),
    },
    { khoa: 'tt', ten: 'Trạng thái', hien: (t) => <NhanTrangThai giaTri={t.trangThai} mau={mauTrangThaiTT(t.trangThai)} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 100,
      hien: (t) => (
        <Link className="btn btn-sm btn-outline-primary" to={'/admin/payments/details/' + t.maTT}>
          <i className="bi bi-eye me-1" />
          Chi tiết
        </Link>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheChiSo
            icon="bi-check-circle"
            mau="la"
            so={<span title={dinhDangTien(d?.tongDaTT)}>{tienO(d?.tongDaTT)}</span>}
            nhan={`Đã thu · ${d?.soGdDaTT ?? 0} giao dịch`}
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo
            icon="bi-hourglass-split"
            mau="cam"
            so={<span title={dinhDangTien(d?.tongChoTT)}>{tienO(d?.tongChoTT)}</span>}
            nhan={`Chờ đối soát · ${d?.soGdChoTT ?? 0} giao dịch`}
          />
        </div>
        <div className="col-12 col-xl-6">
          <TheNoiDung tieuDe="Cơ cấu theo phương thức (đã thu)" icon="bi-pie-chart" lop="mb-0 h-100">
            {!d?.coCau?.length ? (
              <div className="small text-body-secondary">Chưa có giao dịch nào được xác nhận.</div>
            ) : (
              d.coCau.map((c) => (
                <div className="d-flex align-items-center gap-2 mb-1" key={c.phuongThuc}>
                  <span className="small text-truncate" style={{ minWidth: 118 }}>
                    {c.tenPhuongThuc}
                  </span>
                  <div className="tc-thanh flex-grow-1" title={`${c.phanTram}% · ${c.soGd} giao dịch`}>
                    <span style={{ width: Math.round(Number(c.phanTram) || 0) + '%' }} />
                  </div>
                  <span className="small text-body-secondary text-end text-nowrap" style={{ minWidth: 104 }}>
                    {dinhDangTien(c.soTien)}
                  </span>
                </div>
              ))
            )}
          </TheNoiDung>
        </div>
      </div>

      <TheNoiDung tieuDe="Đồng bộ kế toán MISA" icon="bi-calculator">
        <div className="row g-2 align-items-end">
          <div className="col-lg-5">
            <div className="small text-body-secondary">
              Bảng kê chứng từ bán hàng (định khoản <b>Nợ 131 / Có 5113</b>) đúng mẫu <b>Nhập chứng từ từ Excel</b> của MISA AMIS/SME.
            </div>
          </div>
          <div className="col-6 col-lg-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="tc-misa-tu">
              Từ ngày
            </label>
            <input id="tc-misa-tu" type="date" className="form-control form-control-sm" value={tu} onChange={(e) => setTu(e.target.value)} />
          </div>
          <div className="col-6 col-lg-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="tc-misa-den">
              Đến ngày
            </label>
            <input id="tc-misa-den" type="date" className="form-control form-control-sm" value={den} onChange={(e) => setDen(e.target.value)} />
          </div>
          <div className="col-lg-3">
            <button type="button" className="btn btn-sm btn-success w-100" disabled={dangChay} onClick={xuatMisa}>
              <i className="bi bi-file-earmark-excel me-1" />
              Xuất file nhập MISA
            </button>
          </div>
        </div>
      </TheNoiDung>

      <TheNoiDung
        tieuDe="Giao dịch thanh toán"
        icon="bi-cash-stack"
        khongDem
        phai={
          <>
            <select
              className="form-select form-select-sm tc-o-loc"
              aria-label="Lọc trạng thái"
              value={loc.trangThai}
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
            >
              <option value="">— Mọi trạng thái —</option>
              {TRANG_THAI_TT.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
            <select
              className="form-select form-select-sm tc-o-loc"
              aria-label="Lọc phương thức"
              value={loc.phuongThuc}
              onChange={(e) => datLoc({ phuongThuc: e.target.value, trang: 1 })}
            >
              <option value="">— Mọi phương thức —</option>
              {PHUONG_THUC_TT.map((p) => (
                <option key={p.giaTri} value={p.giaTri}>
                  {p.nhan}
                </option>
              ))}
            </select>
            <OTimKiem giaTri={loc.tuKhoa} doi={(tuKhoa) => datLoc({ tuKhoa, trang: 1 })} goiY="Nội dung CK, mã GD, booking, khách…" />
          </>
        }
        chan={<PhanTrang {...(d?.danhSach || {})} donVi="giao dịch" doiTrang={(t) => datLoc({ trang: t })} />}
      >
        <BangDuLieu cot={COT} dong={d?.danhSach?.items} khoaDong="maTT" dangTai={dangTai} rong="Không có giao dịch nào khớp bộ lọc." />
      </TheNoiDung>
    </>
  )
}
