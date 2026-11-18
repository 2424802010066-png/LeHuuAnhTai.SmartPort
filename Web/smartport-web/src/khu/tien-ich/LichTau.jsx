import { useApi } from '../../api/useApi'
import { taoQuery } from '../../api/client'
import { BangDuLieu, BaoLoi, NhanTrangThai } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { DauTrang } from './chung'

/** Gio du kien + gio thuc te (ATA/ATD) hien duoi, mau xanh — giong ban MVC */
function GioKep({ duKien, thucTe }) {
  return (
    <>
      <div>{dinhDangNgayGio(duKien)}</div>
      {thucTe && (
        <div className="text-success small">
          <i className="bi bi-check2 me-1" />
          {dinhDangNgayGio(thucTe)}
        </div>
      )}
    </>
  )
}

const COT = [
  { khoa: 'chuyen', ten: 'Chuyến', hien: (c) => <span className="fw-semibold font-monospace">{c.maChuyen}</span> },
  {
    khoa: 'tau',
    ten: 'Tàu',
    hien: (c) => (
      <>
        <div>{c.tenTau || '—'}</div>
        {c.sucChuaTeu != null && <div className="text-body-secondary small">{dinhDangSo(c.sucChuaTeu)} TEU</div>}
      </>
    ),
  },
  { khoa: 'hang', ten: 'Hãng tàu', an: 'md', hien: (c) => c.hangTau || '—' },
  { khoa: 'eta', ten: 'ETA / ATA', hien: (c) => <GioKep duKien={c.eta} thucTe={c.ata} /> },
  { khoa: 'etd', ten: 'ETD / ATD', an: 'lg', hien: (c) => <GioKep duKien={c.etd} thucTe={c.atd} /> },
  { khoa: 'ben', ten: 'Bến', an: 'md', hien: (c) => c.ben || <span className="text-body-secondary">Chưa phân bến</span> },
  { khoa: 'cont', ten: 'Cont nhập/xuất', canPhai: true, hien: (c) => `${c.contNhap} / ${c.contXuat}` },
  { khoa: 'tt', ten: 'Trạng thái', hien: (c) => <NhanTrangThai giaTri={c.trangThai} /> },
]

/** Lich tau cap cang cong khai (thay Views/Schedule/Index.cshtml) — dia chi /schedule. */
export default function LichTau() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ trangThai: '', hangTau: '' })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tien-ich/lich-tau' + taoQuery(loc), { coToken: false })

  return (
    <>
      <DauTrang icon="bi-calendar2-week" tieuDe="Lịch tàu cập cảng" mucHienTai="Lịch tàu" />
      <div className="container pb-5">
        <BaoLoi loi={loi} thuLai={taiLai} />

        <div className="card sp-the mb-3">
          <div className="card-body">
            <div className="row g-2 align-items-end">
              <div className="col-md-4">
                <label className="form-label small fw-semibold mb-1" htmlFor="locTrangThai">
                  Trạng thái
                </label>
                <select id="locTrangThai" className="form-select" value={loc.trangThai} onChange={(e) => datLoc({ trangThai: e.target.value })}>
                  <option value="">— Tất cả —</option>
                  {(duLieu?.trangThai || []).map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-md-4">
                <label className="form-label small fw-semibold mb-1" htmlFor="locHangTau">
                  Hãng tàu
                </label>
                <select id="locHangTau" className="form-select" value={loc.hangTau} onChange={(e) => datLoc({ hangTau: e.target.value })}>
                  <option value="">— Tất cả —</option>
                  {(duLieu?.hangTau || []).map((h) => (
                    <option key={h.giaTri} value={h.giaTri}>
                      {h.nhan}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-md-4 d-flex gap-2">
                <button type="button" className="btn btn-outline-secondary w-100" onClick={xoaLoc} disabled={!loc.trangThai && !loc.hangTau}>
                  <i className="bi bi-x-lg me-1" />
                  Bỏ lọc
                </button>
                <button type="button" className="btn btn-primary w-100" onClick={() => taiLai()} disabled={dangTai}>
                  <i className="bi bi-arrow-clockwise me-1" />
                  Làm mới
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="card sp-the">
          <BangDuLieu
            cot={COT}
            dong={duLieu?.chuyen}
            khoaDong="maChuyen"
            dangTai={dangTai}
            rong={loc.trangThai || loc.hangTau ? 'Không có chuyến tàu nào phù hợp bộ lọc.' : 'Chưa có chuyến tàu nào trong lịch công bố.'}
          />
        </div>

        <div className="text-body-secondary small mt-3">
          <i className="bi bi-info-circle me-1" />
          Lịch hiển thị các chuyến có ETA từ {duLieu ? new Date(duLieu.tuNgay).toLocaleDateString('vi-VN') : '14 ngày trước'} trở đi. ETA/ETD là giờ dự kiến, ATA/ATD là giờ thực tế — lịch có thể
          thay đổi theo điều kiện khai thác và thời tiết.
        </div>
      </div>
    </>
  )
}
