import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { BienSoXe, SoCont } from './dungChung'
import './cong.css'

const MAU_LOAI = { EIR: 'info', PhieuCan: 'run', LenhGiaoNhan: 'warn', HoaDon: 'ok' }

/**
 * KHO CHUNG TU SO (thay Admin/Docs): moi phieu EIR, phieu can, lenh giao nhan, hoa don phat sinh
 * deu duoc ghi so tu dong. Tra cuu, mo lai ban in, KIEM TRA TOAN VEN CHUOI BAM SHA-256 (mo hinh eB/L).
 * Man hinh CHI DOC — khong co chuc nang sua/xoa chung tu.
 */
export default function KhoChungTu() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ loai: '', q: '', tuNgay: '', denNgay: '', trang: 1, coTrang: 30 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/kho-chung-tu' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const [ketQua, setKetQua] = useState(null)

  async function kiemTra() {
    const kq = await chay(() => goiApi('/api/kho-chung-tu/kiem-tra', { method: 'POST' }))
    if (kq) setKetQua(kq.duLieu)
  }

  const COT = [
    { khoa: 'so', ten: 'Số chứng từ', hien: (c) => <span className="cg-ma">{c.soChungTu}</span> },
    { khoa: 'loai', ten: 'Loại', hien: (c) => <NhanTrangThai giaTri={c.tenLoai} mau={MAU_LOAI[c.loaiCT] || 'info'} /> },
    { khoa: 'cont', ten: 'Container', hien: (c) => <SoCont so={c.soCont} /> },
    { khoa: 'xe', ten: 'Biển số', hien: (c) => (c.bienSoXe ? <BienSoXe bienSo={c.bienSoXe} /> : <span className="text-muted">—</span>) },
    { khoa: 'kh', ten: 'Khách hàng', an: 'xl', hien: (c) => <span className="small">{c.khachHang || '—'}</span> },
    { khoa: 'mota', ten: 'Mô tả', an: 'lg', hien: (c) => <span className="small text-body-secondary">{c.moTa || '—'}</span> },
    { khoa: 'nguoi', ten: 'Người lập', an: 'xl', hien: (c) => <span className="small">{c.nguoiLap || '—'}</span> },
    { khoa: 'ngay', ten: 'Ngày lập', an: 'md', hien: (c) => <span className="small text-nowrap">{gioNgayNgan(c.ngayLap)}</span> },
    {
      khoa: 'hash',
      ten: 'Hash',
      an: 'lg',
      hien: (c) => (
        <span className="cg-ma-mo small" title={c.hashSHA256 || ''}>
          {c.hashSHA256 ? c.hashSHA256.slice(0, 10) + '…' : '—'}
        </span>
      ),
    },
    {
      khoa: 'in',
      ten: '',
      canPhai: true,
      rong: 52,
      hien: (c) =>
        c.duongDan ? (
          <Link className="btn btn-sm btn-outline-secondary" to={c.duongDan} title="Mở bản in">
            <i className="bi bi-printer" />
          </Link>
        ) : null,
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-archive" mau="xanh" so={dinhDangSo(d?.tongSo ?? 0)} nhan="chứng từ lưu trữ" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-calendar-plus" mau="la" so={dinhDangSo(d?.homNay ?? 0)} nhan="phát sinh hôm nay" />
        </div>
        <div className="col-12 col-lg-6 d-flex align-items-center flex-wrap gap-2">
          {(d?.theoLoai || []).map((t) => (
            <button
              key={t.loai}
              type="button"
              className={'btn btn-sm ' + (loc.loai === t.loai ? 'btn-secondary' : 'btn-outline-secondary')}
              onClick={() => datLoc({ loai: loc.loai === t.loai ? '' : t.loai, trang: 1 })}
            >
              {t.tenLoai}
              <span className="badge text-bg-light ms-1">{dinhDangSo(t.so)}</span>
            </button>
          ))}
        </div>
      </div>

      {ketQua && (
        <div className={'alert ' + (ketQua.nguyenVen ? 'alert-success' : 'alert-danger')}>
          <i className={'bi me-2 ' + (ketQua.nguyenVen ? 'bi-link-45deg' : 'bi-exclamation-octagon')} />
          {ketQua.nguyenVen ? (
            <>
              Chuỗi khối <b>NGUYÊN VẸN</b> — {dinhDangSo(ketQua.tongSo)} chứng từ không có dấu hiệu sửa đổi.
            </>
          ) : (
            <>
              <b>PHÁT HIỆN SỬA ĐỔI</b> tại chứng từ #{ketQua.maHong}
              {ketQua.soChungTuHong ? ' (' + ketQua.soChungTuHong + ')' : ''}
              {ketQua.ngayLapHong ? ' lập lúc ' + dinhDangNgayGio(ketQua.ngayLapHong) : ''} — chuỗi hash bị đứt từ bản ghi này.
            </>
          )}
        </div>
      )}

      <TheNoiDung lop="mb-3">
        <div className="row g-2 align-items-end">
          <div className="col-md-3">
            <span className="form-label small fw-semibold mb-1 d-block">Tra cứu</span>
            <OTimKiem giaTri={loc.q} doi={(v) => datLoc({ q: v, trang: 1 })} goiY="Số chứng từ, container, biển số…" rong="100%" lop="w-100" />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-ct-loai">
              Loại chứng từ
            </label>
            <select id="cg-ct-loai" className="form-select form-select-sm" value={loc.loai} onChange={(e) => datLoc({ loai: e.target.value, trang: 1 })}>
              <option value="">— Mọi loại —</option>
              {(d?.cacLoai || []).map((t) => (
                <option key={t.giaTri} value={t.giaTri}>
                  {t.nhan}
                </option>
              ))}
            </select>
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-ct-tu">
              Từ ngày
            </label>
            <input id="cg-ct-tu" type="date" className="form-control form-control-sm" value={loc.tuNgay} onChange={(e) => datLoc({ tuNgay: e.target.value, trang: 1 })} />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-ct-den">
              Đến ngày
            </label>
            <input
              id="cg-ct-den"
              type="date"
              className="form-control form-control-sm"
              value={loc.denNgay}
              onChange={(e) => datLoc({ denNgay: e.target.value, trang: 1 })}
            />
          </div>
          <div className="col-6 col-md-3 d-flex gap-2">
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={xoaLoc}>
              Xóa lọc
            </button>
            <button
              type="button"
              className="btn btn-sm btn-outline-primary flex-fill"
              onClick={kiemTra}
              disabled={dangChay}
              title="Tính lại SHA-256 toàn bộ sổ và đối chiếu chuỗi liên kết — mô hình eB/L của hãng tàu"
            >
              <i className="bi bi-link-45deg me-1" />
              Kiểm tra chuỗi khối
            </button>
          </div>
        </div>
      </TheNoiDung>

      <TheNoiDung
        tieuDe="Kho chứng từ số"
        icon="bi-archive"
        khongDem
        chan={<PhanTrang trang={d?.trang} soTrang={d?.soTrang} tong={d?.tong} donVi="chứng từ" doiTrang={(t) => datLoc({ trang: t })} />}
      >
        <BangDuLieu cot={COT} dong={d?.items} khoaDong="maCT" dangTai={dangTai} rong="Chưa có chứng từ khớp bộ lọc." />
      </TheNoiDung>
    </>
  )
}
