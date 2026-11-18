import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { goiApi, taiFile, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, OTimKiem, PhanTrang, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import NhanLoaiBanTin from './NhanLoaiBanTin'
import { MAU_TRANG_THAI_EDI, kyBaoCao, trangThaiBanTin } from './chung'
import './edi-depot.css'

const LOC_LOAI = [
  { giaTri: '', nhan: 'Tất cả' },
  { giaTri: 'CODECO', nhan: 'CODECO', dem: 'soCodeco' },
  { giaTri: 'COPARN', nhan: 'COPARN', dem: 'soCoparn' },
  { giaTri: 'COARRI', nhan: 'COARRI', dem: 'soCoarri' },
]

const SO_NGAY = [
  { giaTri: 1, nhan: '1 ngày qua (cuối ngày)' },
  { giaTri: 7, nhan: '7 ngày qua' },
  { giaTri: 30, nhan: '30 ngày qua' },
  { giaTri: 90, nhan: '90 ngày qua' },
]

/**
 * EDI hang tau (thay Admin/Edi cua ban MVC): sinh CODECO theo hang tau, sinh COARRI theo chuyen tau,
 * mo man hinh nhan COPARN; lich su ban tin kem xem / tai .edi / danh dau da gui / xoa.
 */
export default function Edi() {
  const [loc, datLoc] = useBoLoc({ loai: '', q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/edi' + taoQuery(loc))
  const { duLieu: luaChon, loi: loiLuaChon } = useApi('/api/edi/lua-chon')
  const [codeco, setCodeco] = useState({ maHangTau: '', soNgay: 7 })
  const [coarri, setCoarri] = useState({ maChuyen: '', chieu: 'Do' })
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()
  const dieuHuong = useNavigate()

  // Xe qua cong sinh them luot can bao cao -> lam moi so "chua bao cao" ngam
  useNgheSuKien('cong.*', () => taiLai({ im: true }))

  if (!d) return dangTai ? <DangTai /> : <BaoLoi loi={loi} thuLai={taiLai} />

  const hangTaus = luaChon?.hangTaus || []
  const chuyenTaus = luaChon?.chuyenTaus || []
  // O chon mac dinh la muc dau tien (giong ban MVC: select khong co dong trong)
  const maHangTau = codeco.maHangTau || hangTaus[0]?.giaTri || ''
  const maChuyen = coarri.maChuyen || (chuyenTaus[0] ? String(chuyenTaus[0].maChuyen) : '')
  const chuyenChon = chuyenTaus.find((c) => String(c.maChuyen) === maChuyen)
  const soContChieu = chuyenChon ? (coarri.chieu === 'Xep' ? chuyenChon.soXuat : chuyenChon.soNhap) : 0

  async function sinhCodeco(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/edi/codeco', { method: 'POST', body: { maHangTau: Number(maHangTau), soNgay: Number(codeco.soNgay) } }))
    if (kq?.id) dieuHuong('/admin/edi/xem/' + kq.id)
  }

  async function sinhCoarri(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/edi/coarri', { method: 'POST', body: { maChuyen: Number(maChuyen), chieu: coarri.chieu } }))
    if (kq?.id) dieuHuong('/admin/edi/xem/' + kq.id)
  }

  async function tai(b) {
    try {
      await taiFile('/api/edi/' + b.maBT + '/tai', { tenMacDinh: `${b.loaiBT}_${b.soChungTu || b.maBT}.edi` })
    } catch (e) {
      bao.loi(e)
    }
  }

  async function danhDauGui(b) {
    if (await chay(() => goiApi('/api/edi/' + b.maBT + '/da-gui', { method: 'POST' }))) taiLai({ im: true })
  }

  async function xoa(b) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa bản tin?',
      noiDung: `Xóa bản tin ${b.soChungTu || '#' + b.maBT}.\nCác lượt qua cổng trong đó sẽ quay lại trạng thái CHƯA báo cáo.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (!(await chay(() => goiApi('/api/edi/' + b.maBT, { method: 'DELETE' })))) return
    // Xoa dong cuoi cung cua trang cuoi -> lui ve trang truoc
    if (d.danhSach.items.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
    else taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'chungTu',
      ten: 'Chứng từ',
      hien: (b) => (
        <>
          <Link className="fw-semibold text-decoration-none" to={'/admin/edi/xem/' + b.maBT}>
            {b.soChungTu || '#' + b.maBT}
          </Link>
          <div className="small text-body-secondary">
            {gioNgayNgan(b.ngayTao)} · {b.nguoiTao || '—'}
          </div>
        </>
      ),
    },
    {
      khoa: 'loai',
      ten: 'Loại · hướng',
      hien: (b) => (
        <>
          <NhanLoaiBanTin loai={b.loaiBT} />
          <div className="small text-body-secondary">
            <i className={'bi me-1 ' + (b.huong === 'Gửi' ? 'bi-arrow-up-right' : 'bi-arrow-down-left')} />
            {b.huong}
          </div>
        </>
      ),
    },
    { khoa: 'hang', ten: 'Hãng tàu', an: 'md', hien: (b) => <span className="small">{b.tenHangTau || '—'}</span> },
    { khoa: 'cont', ten: 'Cont', canPhai: true, hien: (b) => <b>{dinhDangSo(b.soLuongCont)}</b> },
    {
      khoa: 'ky',
      ten: 'Kỳ',
      an: 'lg',
      hien: (b) => (
        <>
          <span className="small text-body-secondary">{kyBaoCao(b.tuNgay, b.denNgay)}</span>
          {b.ghiChu && <div className="small text-body-secondary text-truncate" style={{ maxWidth: 260 }} title={b.ghiChu}>{b.ghiChu}</div>}
        </>
      ),
    },
    {
      khoa: 'trangThai',
      ten: 'Trạng thái',
      hien: (b) => {
        const tt = trangThaiBanTin(b.trangThai)
        return (
          <>
            <NhanTrangThai giaTri={tt} mau={MAU_TRANG_THAI_EDI[tt]} />
            {tt === 'Đã gửi' && b.ngayGui && <div className="small text-body-secondary">{gioNgayNgan(b.ngayGui)}</div>}
          </>
        )
      },
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 170,
      hien: (b) => (
        <div className="text-nowrap">
          <Link className="btn btn-sm btn-outline-primary me-1" to={'/admin/edi/xem/' + b.maBT} title="Xem nội dung" aria-label="Xem nội dung">
            <i className="bi bi-eye" />
          </Link>
          <button type="button" className="btn btn-sm btn-outline-secondary me-1" title="Tải tệp .edi" aria-label="Tải tệp .edi" onClick={() => tai(b)}>
            <i className="bi bi-download" />
          </button>
          {b.huong === 'Gửi' && b.trangThai !== 'Đã gửi' && (
            <button
              type="button"
              className="btn btn-sm btn-outline-success me-1"
              title="Đánh dấu đã gửi cho hãng tàu"
              aria-label="Đánh dấu đã gửi cho hãng tàu"
              disabled={dangChay}
              onClick={() => danhDauGui(b)}
            >
              <i className="bi bi-send" />
            </button>
          )}
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa bản tin" aria-label="Xóa bản tin" disabled={dangChay} onClick={() => xoa(b)}>
            <i className="bi bi-trash" />
          </button>
        </div>
      ),
    },
  ]

  const coLoc = !!(loc.loai || loc.q)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <BaoLoi loi={loiLuaChon} />

      <div className="row g-3 mb-3">
        {/* ---------- CODECO: bao cao xe vao/ra depot cho hang tau ---------- */}
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Sinh CODECO gửi hãng tàu" icon="bi-upload" lop="h-100 mb-0">
            <div className="d-flex align-items-baseline gap-2 mb-3">
              <span className={'ed-so-the ' + (d.chuaBaoCao ? 'text-warning-emphasis' : 'text-success')}>{dinhDangSo(d.chuaBaoCao)}</span>
              <span className="small text-body-secondary">
                {d.chuaBaoCao ? 'lượt qua cổng chưa báo cáo' : 'lượt tồn đọng — đã báo hết'}
              </span>
            </div>
            <form className="row g-2" onSubmit={sinhCodeco}>
              <Truong nhan="Hãng tàu" lop="col-sm-7">
                <select className="form-select" value={maHangTau} onChange={(e) => setCodeco({ ...codeco, maHangTau: e.target.value })} required>
                  {!hangTaus.length && <option value="">— Chưa có hãng tàu —</option>}
                  {hangTaus.map((h) => (
                    <option key={h.giaTri} value={h.giaTri}>
                      {h.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Lấy lượt trong" lop="col-sm-5">
                <select className="form-select" value={codeco.soNgay} onChange={(e) => setCodeco({ ...codeco, soNgay: e.target.value })}>
                  {SO_NGAY.map((s) => (
                    <option key={s.giaTri} value={s.giaTri}>
                      {s.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <div className="col-12">
                <button type="submit" className="btn btn-primary w-100" disabled={dangChay || !maHangTau}>
                  <i className="bi bi-file-earmark-code me-1" />
                  Sinh bản tin CODECO
                </button>
              </div>
            </form>
          </TheNoiDung>
        </div>

        {/* ---------- COARRI: do / xep container cua mot chuyen tau ---------- */}
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Sinh COARRI theo chuyến tàu" icon="bi-water" lop="h-100 mb-0">
            <div className="d-flex align-items-baseline gap-2 mb-3">
              <span className="ed-so-the text-primary-emphasis">{dinhDangSo(chuyenTaus.length)}</span>
              <span className="small text-body-secondary">chuyến tàu đã gán container</span>
            </div>
            {luaChon && !chuyenTaus.length ? (
              <div className="small text-body-secondary">
                Chưa có chuyến nào được gán container.{' '}
                <Link to="/admin/containers">Gán chuyến nhập/xuất</Link>
              </div>
            ) : (
              <form className="row g-2" onSubmit={sinhCoarri}>
                <Truong nhan="Chuyến tàu" lop="col-12">
                  <select className="form-select" value={maChuyen} onChange={(e) => setCoarri({ ...coarri, maChuyen: e.target.value })} required>
                    {chuyenTaus.map((c) => (
                      <option key={c.maChuyen} value={c.maChuyen}>
                        {c.nhan}
                      </option>
                    ))}
                  </select>
                </Truong>
                <Truong nhan="Chiều" lop="col-sm-7">
                  <select className="form-select" value={coarri.chieu} onChange={(e) => setCoarri({ ...coarri, chieu: e.target.value })}>
                    <option value="Do">Dỡ hàng — {chuyenChon?.soNhap ?? 0} cont</option>
                    <option value="Xep">Xếp hàng — {chuyenChon?.soXuat ?? 0} cont</option>
                  </select>
                </Truong>
                <div className="col-sm-5 d-flex align-items-end">
                  <button
                    type="submit"
                    className="btn btn-outline-primary w-100"
                    disabled={dangChay || !maChuyen || !soContChieu}
                    title={maChuyen && !soContChieu ? 'Chuyến này chưa có container ở chiều đã chọn' : undefined}
                  >
                    <i className="bi bi-file-earmark-code me-1" />
                    Sinh COARRI
                  </button>
                </div>
              </form>
            )}
          </TheNoiDung>
        </div>

        {/* ---------- COPARN: hang tau bao truoc -> tu lap lenh ---------- */}
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Nhận COPARN từ hãng tàu" icon="bi-inbox" lop="h-100 mb-0">
            <div className="d-flex align-items-baseline gap-2 mb-3">
              <span className="ed-so-the ed-chu-tim">{dinhDangSo(d.soCoparn)}</span>
              <span className="small text-body-secondary">bản tin COPARN đã nhận</span>
            </div>
            <p className="small text-body-secondary mb-3">Dán nội dung hoặc tải tệp .edi — hệ thống tự lập lệnh giao nhận.</p>
            <Link className="btn btn-outline-primary w-100 mb-3" to="/admin/edi/nhancoparn">
              <i className="bi bi-inbox me-1" />
              Nhận COPARN
            </Link>
            <div className="small text-body-secondary border-top pt-2">
              Mã depot trong bản tin: <code>{d.maDepot}</code>
            </div>
          </TheNoiDung>
        </div>
      </div>

      <TheNoiDung
        tieuDe="Lịch sử bản tin"
        icon="bi-journal-code"
        khongDem
        phai={
          <>
            <div className="btn-group btn-group-sm flex-wrap" role="group" aria-label="Lọc theo loại bản tin">
              {LOC_LOAI.map((l) => (
                <button
                  key={l.giaTri || 'tat-ca'}
                  type="button"
                  className={'btn ' + (loc.loai === l.giaTri ? 'btn-primary' : 'btn-outline-primary')}
                  aria-pressed={loc.loai === l.giaTri}
                  onClick={() => datLoc({ loai: l.giaTri, trang: 1 })}
                >
                  {l.nhan}
                  {l.dem && <span className="ms-1 opacity-75">({dinhDangSo(d[l.dem])})</span>}
                </button>
              ))}
            </div>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số chứng từ, hãng tàu…" rong={240} />
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={d.danhSach.items}
          khoaDong="maBT"
          dangTai={dangTai}
          rong={coLoc ? 'Không có bản tin khớp bộ lọc.' : 'Chưa có bản tin nào — sinh CODECO đầu tiên ở khung bên trên.'}
        />
        <div className="px-3 pb-2">
          <PhanTrang {...d.danhSach} doiTrang={(t) => datLoc({ trang: t })} donVi="bản tin" />
        </div>
      </TheNoiDung>
    </>
  )
}
