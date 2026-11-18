import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useBieuMau, soHoacNull } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, dinhDangTien, gioNgayNgan, tienGon } from '../../utils/dinhDang'
import { MAU_TRANG_THAI_EOR } from './chung'
import './edi-depot.css'

const VI_TRI = ['Vách trước', 'Vách bên trái', 'Vách bên phải', 'Cửa', 'Sàn', 'Nóc', 'Khung / góc']

// Ma hu hong theo bang hang tau — go tay duoc, danh sach chi de goi y
const MA_HU_HONG = [
  { ma: 'DT', ten: 'Móp / lõm (dent)' },
  { ma: 'BR', ten: 'Gãy / vỡ (broken)' },
  { ma: 'CU', ten: 'Cong vênh (curved)' },
  { ma: 'HO', ten: 'Thủng (hole)' },
  { ma: 'RU', ten: 'Gỉ sét (rust)' },
  { ma: 'CL', ten: 'Bẩn, cần vệ sinh' },
]

const LOC_TRANG_THAI = [
  { giaTri: '', nhan: 'Tất cả' },
  { giaTri: 'Chờ duyệt', nhan: 'Chờ duyệt' },
  { giaTri: 'Đã duyệt', nhan: 'Đã duyệt' },
  { giaTri: 'Đã sửa', nhan: 'Đã sửa' },
  { giaTri: 'Từ chối', nhan: 'Từ chối' },
]

const RONG = { soCont: '', maHangTau: '', viTriHuHong: VI_TRI[0], maHuHong: '', moTa: '', gioCong: '', chiPhiVatTu: '' }

/**
 * Phieu sua chua vo EOR (thay Admin/Depot/Eor cua ban MVC): lap bao gia sua -> hang tau duyet ->
 * sua xong. Con so dang tien nhat la TIEN DANG CHO HANG TAU DUYET.
 */
export default function Eor() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/depot/eor' + taoQuery(loc))
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()

  if (!d) return dangTai ? <DangTai /> : <BaoLoi loi={loi} thuLai={taiLai} />

  const donGia = d.donGiaGioCong || 0
  const uocTinh = (Number(gt.gioCong) || 0) * donGia + (Number(gt.chiPhiVatTu) || 0)

  async function lapPhieu(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/depot/eor', {
        method: 'POST',
        body: {
          soCont: (gt.soCont || '').trim().toUpperCase(),
          maHangTau: soHoacNull(gt.maHangTau),
          viTriHuHong: gt.viTriHuHong,
          maHuHong: gt.maHuHong,
          moTa: gt.moTa,
          gioCong: soHoacNull(gt.gioCong),
          chiPhiVatTu: soHoacNull(gt.chiPhiVatTu),
        },
      }),
    )
    if (!kq) return
    datLai(RONG)
    taiLai({ im: true })
  }

  async function xuLy(e, quyetDinh) {
    if (await chay(() => goiApi('/api/depot/eor/' + e.maEOR + '/duyet', { method: 'POST', body: { quyetDinh } }))) taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'soEor',
      ten: 'Số EOR',
      hien: (e) => (
        <>
          <span className="fw-semibold small">{e.soEOR}</span>
          <div className="small text-body-secondary">{gioNgayNgan(e.ngayLap)}</div>
        </>
      ),
    },
    {
      khoa: 'cont',
      ten: 'Container',
      hien: (e) => (
        <Link className="fw-semibold text-decoration-none ed-mono" to={'/admin/containers/tracuu?so=' + encodeURIComponent(e.soCont)} title="Hồ sơ container">
          {e.soCont}
        </Link>
      ),
    },
    { khoa: 'hang', ten: 'Hãng tàu', an: 'md', hien: (e) => <span className="small">{e.tenHangTau || '—'}</span> },
    {
      khoa: 'huHong',
      ten: 'Hư hỏng',
      an: 'lg',
      hien: (e) => (
        <>
          <span className="small">{e.viTriHuHong || '—'}</span>
          {e.maHuHong && <span className="badge text-bg-light border ms-1">{e.maHuHong}</span>}
          {e.moTa && (
            <div className="small text-body-secondary text-truncate" style={{ maxWidth: 240 }} title={e.moTa}>
              {e.moTa}
            </div>
          )}
        </>
      ),
    },
    {
      khoa: 'tien',
      ten: 'Ước tính',
      canPhai: true,
      hien: (e) => (
        <>
          <b>{dinhDangTien(e.chiPhiUocTinh)}</b>
          <div className="small text-body-secondary">
            {dinhDangSo(e.gioCong, 1)} giờ + {tienGon(e.chiPhiVatTu)} vật tư
          </div>
        </>
      ),
    },
    { khoa: 'trangThai', ten: 'Trạng thái', hien: (e) => <NhanTrangThai giaTri={e.trangThai} mau={MAU_TRANG_THAI_EOR[e.trangThai]} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 150,
      hien: (e) => (
        <div className="text-nowrap">
          <Link
            className="btn btn-sm btn-outline-secondary me-1"
            to={'/admin/depot/ineor/' + e.maEOR}
            target="_blank"
            title="In gửi hãng tàu"
            aria-label="In gửi hãng tàu"
          >
            <i className="bi bi-printer" />
          </Link>
          {e.trangThai === 'Chờ duyệt' && (
            <>
              <button
                type="button"
                className="btn btn-sm btn-success me-1"
                title="Hãng tàu duyệt"
                aria-label="Hãng tàu duyệt"
                disabled={dangChay}
                onClick={() => xuLy(e, 'duyet')}
              >
                <i className="bi bi-check-lg" />
              </button>
              <button
                type="button"
                className="btn btn-sm btn-outline-danger"
                title="Hãng tàu từ chối"
                aria-label="Hãng tàu từ chối"
                disabled={dangChay}
                onClick={() => xuLy(e, 'tuchoi')}
              >
                <i className="bi bi-x-lg" />
              </button>
            </>
          )}
          {e.trangThai === 'Đã duyệt' && (
            <button type="button" className="btn btn-sm btn-primary" disabled={dangChay} onClick={() => xuLy(e, 'sua')}>
              Đã sửa
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-hourglass-split" mau={d.choDuyet > 0 ? 'cam' : 'la'} so={dinhDangSo(d.choDuyet)} nhan="Chờ hãng tàu duyệt" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-check2-circle" mau="la" so={dinhDangSo(d.daDuyet)} nhan="Đã duyệt — được sửa" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-wrench-adjustable" mau="xanh" so={dinhDangSo(d.daSua)} nhan="Đã sửa xong" />
        </div>
        <div className="col-6 col-lg-3">
          <div className="ed-chi-so-tien">
            <TheChiSo icon="bi-cash-stack" mau="tim" so={dinhDangTien(d.tongTienChoDuyet)} nhan="Tiền đang chờ hãng tàu duyệt" />
          </div>
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Lập phiếu sửa chữa" icon="bi-plus-square">
            <form onSubmit={lapPhieu}>
              <Truong nhan="Số container" batBuoc>
                <input className="form-control text-uppercase ed-mono" value={gt.soCont} onChange={doi('soCont')} maxLength={11} placeholder="MSCU2299876" />
              </Truong>
              <Truong nhan="Hãng tàu" goiY="Để trống = lấy theo container">
                <select className="form-select" value={gt.maHangTau} onChange={doi('maHangTau')}>
                  <option value="">— Tự lấy theo container —</option>
                  {(d.hangTaus || []).map((h) => (
                    <option key={h.giaTri} value={h.giaTri}>
                      {h.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Vị trí hư hỏng">
                <select className="form-select" value={gt.viTriHuHong} onChange={doi('viTriHuHong')}>
                  {VI_TRI.map((v) => (
                    <option key={v} value={v}>
                      {v}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Mã hư hỏng" goiY="Theo bảng mã của hãng tàu">
                <input className="form-control text-uppercase" list="ed-ma-hu-hong" value={gt.maHuHong} onChange={doi('maHuHong')} maxLength={20} placeholder="DT" />
              </Truong>
              <datalist id="ed-ma-hu-hong">
                {MA_HU_HONG.map((m) => (
                  <option key={m.ma} value={m.ma}>
                    {m.ten}
                  </option>
                ))}
              </datalist>
              <Truong nhan="Mô tả">
                <textarea className="form-control" rows={2} value={gt.moTa} onChange={doi('moTa')} maxLength={300} placeholder="Móp ~30x20cm, sâu 3cm…" />
              </Truong>
              <div className="row g-2">
                <Truong nhan="Giờ công" lop="col-6 mb-3">
                  <input className="form-control" type="number" step="0.25" min="0" value={gt.gioCong} onChange={doi('gioCong')} placeholder="2.5" />
                </Truong>
                <Truong nhan="Vật tư (đ)" lop="col-6 mb-3">
                  <input className="form-control" type="number" step="1000" min="0" value={gt.chiPhiVatTu} onChange={doi('chiPhiVatTu')} placeholder="350000" />
                </Truong>
              </div>
              <div className="d-flex justify-content-between align-items-baseline mb-2">
                <span className="small text-body-secondary">Ước tính = giờ công × {dinhDangTien(donGia)} + vật tư</span>
                <b className="fs-5">{dinhDangTien(uocTinh)}</b>
              </div>
              <button type="submit" className="btn btn-primary w-100" disabled={dangChay}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
                Lập EOR gửi hãng tàu
              </button>
            </form>
          </TheNoiDung>
        </div>

        <div className="col-lg-8">
          <TheNoiDung
            tieuDe="Danh sách phiếu"
            icon="bi-list-check"
            khongDem
            phai={
              <>
                <div className="btn-group btn-group-sm flex-wrap" role="group" aria-label="Lọc theo trạng thái phiếu">
                  {LOC_TRANG_THAI.map((t) => (
                    <button
                      key={t.giaTri || 'tat-ca'}
                      type="button"
                      className={'btn ' + (loc.trangThai === t.giaTri ? 'btn-primary' : 'btn-outline-primary')}
                      aria-pressed={loc.trangThai === t.giaTri}
                      onClick={() => datLoc({ trangThai: t.giaTri, trang: 1 })}
                    >
                      {t.nhan}
                    </button>
                  ))}
                </div>
                <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số EOR, số container…" rong={220} />
                <Link className="btn btn-sm btn-outline-secondary" to="/admin/depot/tonrong">
                  <i className="bi bi-box2 me-1" />
                  Tồn rỗng
                </Link>
              </>
            }
          >
            <BangDuLieu
              cot={COT}
              dong={d.danhSach.items}
              khoaDong="maEOR"
              dangTai={dangTai}
              rong={loc.trangThai || loc.q ? 'Không có phiếu khớp bộ lọc.' : 'Chưa có phiếu sửa chữa nào.'}
            />
            <div className="px-3 pb-2">
              <PhanTrang {...d.danhSach} doiTrang={(t) => datLoc({ trang: t })} donVi="phiếu" />
            </div>
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
