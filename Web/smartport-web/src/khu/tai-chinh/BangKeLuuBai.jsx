import { Link, useNavigate } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import { tienO, useLamMoiTheoSuKien } from './dungChung'
import './tai-chinh.css'

/** Phi luu bai — bang ke tien dang treo ngoai bai, chot phieu (thay Admin/LuuBai cua ban MVC). */
export default function BangKeLuuBai() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ maKH: '', maHangTau: '' })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/luu-bai/bang-ke' + taoQuery(loc))
  const { duLieu: dm } = useApi('/api/thu-tien/danh-muc')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const dieuHuong = useNavigate()

  // Phieu do cong tu chot khi container roi bai (tai-chinh.*), container vao/ra bai (bai.*)
  useLamMoiTheoSuKien('tai-chinh.*', taiLai)
  useLamMoiTheoSuKien('bai.*', taiLai)

  async function chot(x) {
    const boSung = x.daCoPhieu
    const dongY = await xacNhan({
      tieuDe: boSung ? 'Chốt phiếu bổ sung?' : 'Chốt phiếu thu?',
      noiDung: boSung
        ? `Container ${x.soCont} đã có phiếu ${x.soPhieuDaChot} nhưng còn ở bãi thêm ngày sau lần chốt đó.\nPhiếu bổ sung chỉ thu phần chưa chốt: ${dinhDangTien(x.tienChuaChot)}.`
        : `Container ${x.soCont} — ${dinhDangTien(x.tongTien)}\n${x.dienGiai}`,
      nutDongY: boSung ? 'Chốt bổ sung' : 'Chốt phiếu',
      mau: 'warning',
    })
    if (!dongY) return
    const kq = await chay(() => goiApi('/api/luu-bai/chot/' + x.maCont, { method: 'POST' }))
    if (kq?.id) dieuHuong('/admin/luubai/phieu/' + kq.id)
    else if (kq) taiLai({ im: true })
  }

  async function chotKy() {
    const dongY = await xacNhan({
      tieuDe: `Chốt ${d.soContChuaChot} phiếu?`,
      noiDung: `Tổng ${dinhDangTien(d.tienChuaChot)}.\nChốt rồi thì số tiền trên phiếu không đổi nữa.`,
      nutDongY: 'Chốt kỳ',
      mau: 'warning',
    })
    if (!dongY) return
    const kq = await chay(() =>
      goiApi('/api/luu-bai/chot-ky', {
        method: 'POST',
        body: { maKH: loc.maKH ? Number(loc.maKH) : null, maHangTau: loc.maHangTau ? Number(loc.maHangTau) : null },
      }),
    )
    if (kq) dieuHuong('/admin/luubai/danhsach')
  }

  const COT = [
    {
      khoa: 'cont',
      ten: 'Container',
      hien: (x) => (
        <>
          <Link className="fw-semibold text-decoration-none tc-ma" to={'/admin/containers/hoso/' + x.maCont}>
            {x.soCont}
          </Link>
          <div className="small text-body-secondary">{[x.loaiCont, x.tinhTrang, x.viTriBai].filter(Boolean).join(' · ')}</div>
        </>
      ),
    },
    {
      khoa: 'khach',
      ten: 'Khách hàng · hãng tàu',
      an: 'md',
      hien: (x) => (
        <div className="small">
          {x.tenKhach || '—'}
          {x.tenHangTau && <div className="text-body-secondary">{x.tenHangTau}</div>}
        </div>
      ),
    },
    { khoa: 'vao', ten: 'Vào bãi', an: 'lg', hien: (x) => <span className="small text-body-secondary">{dinhDangNgay(x.tuNgay)}</span> },
    {
      khoa: 'ngay',
      ten: 'Số ngày',
      hien: (x) => (
        <>
          <span className={'fw-bold' + (x.tongNgay > 30 ? ' text-danger' : '')}>{x.tongNgay}</span>
          <div>{x.ngayTinhPhi === 0 ? <NhanTrangThai mau="ok" giaTri="miễn phí" /> : <span className="small text-body-secondary">tính {x.ngayTinhPhi}</span>}</div>
        </>
      ),
    },
    { khoa: 'luuBai', ten: 'Lưu bãi', canPhai: true, an: 'lg', hien: (x) => (x.phiLuuBai > 0 ? dinhDangTien(x.phiLuuBai) : '—') },
    {
      khoa: 'dien',
      ten: 'Điện lạnh',
      canPhai: true,
      an: 'lg',
      hien: (x) =>
        x.laContLanh ? (
          <span title={`${x.soNgayDien} ngày × ${dinhDangTien(x.donGiaDien)}`}>{dinhDangTien(x.phiDien)}</span>
        ) : (
          <span className="text-body-secondary">—</span>
        ),
    },
    {
      khoa: 'treo',
      ten: 'Đang treo',
      canPhai: true,
      hien: (x) => <span className={'fw-bold text-nowrap ' + (x.tongTien > 0 ? 'tc-tien' : 'text-body-secondary')}>{dinhDangTien(x.tongTien)}</span>,
    },
    {
      khoa: 'chot',
      ten: 'Phiếu',
      canPhai: true,
      rong: 140,
      hien: (x) =>
        x.daCoPhieu ? (
          <>
            {x.maPhieuDaChot ? (
              <Link className="badge text-bg-light border text-decoration-none" to={'/admin/luubai/phieu/' + x.maPhieuDaChot} title="Mở phiếu đã chốt">
                <i className="bi bi-check2 me-1" />
                {x.soPhieuDaChot}
              </Link>
            ) : (
              <span className="badge text-bg-light border">{x.soPhieuDaChot}</span>
            )}
            {/* da chot giua ky ma container con o bai them ngay -> con tien chua chot */}
            {x.tienChuaChot > 0 && (
              <button
                type="button"
                className="btn btn-sm btn-outline-warning text-nowrap d-block ms-auto mt-1"
                title={'Phát sinh sau lần chốt: ' + dinhDangTien(x.tienChuaChot)}
                disabled={dangChay}
                onClick={() => chot(x)}
              >
                <i className="bi bi-plus-circle me-1" />
                Bổ sung {dinhDangTien(x.tienChuaChot)}
              </button>
            )}
          </>
        ) : x.tongTien > 0 ? (
          <button type="button" className="btn btn-sm btn-warning text-nowrap" title={x.dienGiai} disabled={dangChay} onClick={() => chot(x)}>
            <i className="bi bi-receipt me-1" />
            Chốt
          </button>
        ) : (
          <span className="small text-body-secondary text-nowrap">chưa tới hạn</span>
        ),
    },
  ]

  const coLoc = loc.maKH || loc.maHangTau

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      {d && (
        <div className="row g-3 mb-3">
          <div className="col-6 col-xl-3">
            <TheChiSo icon="bi-stopwatch" mau="cam" so={<span title={dinhDangTien(d.tongTien)}>{tienO(d.tongTien)}</span>} nhan={`Đang treo · ${d.tongCont} container`} />
          </div>
          <div className="col-6 col-xl-3">
            <TheChiSo icon="bi-receipt" mau="la" so={<span title={dinhDangTien(d.tienChuaChot)}>{tienO(d.tienChuaChot)}</span>} nhan={`Chưa chốt · ${d.soContChuaChot} cont`} />
          </div>
          <div className="col-6 col-xl-3">
            <TheChiSo icon="bi-hourglass-split" mau="xanh" so={d.soContDenHan} nhan={`Quá ${d.ngayMienPhi} ngày miễn phí`} />
          </div>
          <div className="col-6 col-xl-3">
            <TheChiSo icon="bi-exclamation-triangle" mau={d.soContTonLau > 0 ? 'do' : 'la'} so={d.soContTonLau} nhan="Tồn quá 30 ngày" />
          </div>
        </div>
      )}

      <TheNoiDung
        tieuDe="Đồng hồ lưu bãi"
        icon="bi-list-columns"
        khongDem
        phai={
          <>
            <select className="form-select form-select-sm tc-o-loc" aria-label="Lọc khách hàng" value={loc.maKH} onChange={(e) => datLoc({ maKH: e.target.value })}>
              <option value="">Tất cả khách hàng</option>
              {(dm?.khachHang || []).map((k) => (
                <option key={k.giaTri} value={k.giaTri}>
                  {k.nhan}
                </option>
              ))}
            </select>
            <select className="form-select form-select-sm tc-o-loc" aria-label="Lọc hãng tàu" value={loc.maHangTau} onChange={(e) => datLoc({ maHangTau: e.target.value })}>
              <option value="">Tất cả hãng tàu</option>
              {(dm?.hangTau || []).map((h) => (
                <option key={h.giaTri} value={h.giaTri}>
                  {h.nhan}
                </option>
              ))}
            </select>
            {coLoc && (
              <button type="button" className="btn btn-sm btn-light" onClick={xoaLoc} title="Xóa lọc" aria-label="Xóa lọc">
                <i className="bi bi-x-lg" />
              </button>
            )}
            <Link className="btn btn-sm btn-outline-primary" to="/admin/luubai/danhsach">
              <i className="bi bi-journal-text me-1" />
              Sổ phiếu
            </Link>
            {d?.soContChuaChot > 0 && (
              <button type="button" className="btn btn-sm btn-warning" disabled={dangChay} onClick={chotKy}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-receipt me-1" />}
                Chốt kỳ · {d.soContChuaChot} phiếu
              </button>
            )}
          </>
        }
        chan={
          d && (
            <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 small">
              <span className="text-body-secondary">
                <i className="bi bi-clock me-1" />
                Tính đến {dinhDangNgayGio(d.tinhDen)} · miễn {d.ngayMienPhi} ngày đầu · chốt là khóa số tiền
              </span>
              <span>
                Tổng đang treo <b className="tc-tien fs-6 ms-1">{dinhDangTien(d.tongTien)}</b>
              </span>
            </div>
          )
        }
      >
        <BangDuLieu
          cot={COT}
          dong={d?.dong}
          khoaDong="maCont"
          dangTai={dangTai}
          rong={coLoc ? 'Không có container nào khớp bộ lọc.' : 'Không có container nào đang nằm trong bãi.'}
        />
      </TheNoiDung>
    </>
  )
}
