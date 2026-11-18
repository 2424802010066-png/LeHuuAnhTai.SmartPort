import { Link } from 'react-router-dom'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangSo, gioNgayNgan, ngayInput } from '../../utils/dinhDang'
import './tau-ben.css'

const THU = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7']
const SO_NGAY = [9, 14, 21, 31]
const hai = (n) => String(n).padStart(2, '0')
const ngayThang = (d) => `${hai(d.getDate())}/${hai(d.getMonth() + 1)}`

/** Mau thanh giong ban MVC: da roi = xam, tre ETD = nau cam, dang lam hang = xanh la, ke hoach = xanh duong */
function kieuThanh(c) {
  if (c.atd) return 'tb-mau-da-roi'
  if (c.quaHan) return 'tb-mau-tre'
  if (c.ata) return 'tb-mau-lam-hang'
  return 'tb-mau-ke-hoach'
}

const CHU_GIAI = [
  ['tb-mau-lam-hang', 'Đang làm hàng'],
  ['tb-mau-ke-hoach', 'Kế hoạch'],
  ['tb-mau-da-roi', 'Đã rời bến'],
  ['tb-mau-tre', 'Trễ · quá hạn ETD chưa rời'],
  ['tb-vien-trung', 'Trùng lịch'],
]

/**
 * GANTT CAU BEN (berth planning kieu TOS): truc ngang = thoi gian, moi hang = mot cau ben,
 * moi thanh = mot chuyen chiem ben tu (ATA ?? ETA) den (ATD ?? ETD). Ve bang CSS thuan, cuon ngang duoc.
 */
export default function GanttBen() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ tu: '', soNgay: 9 })
  const { duLieu: g, loi, dangTai, taiLai } = useApi('/api/ben-cang/gantt' + taoQuery(loc))

  if (dangTai && !g) return <DangTai />
  if (!g) return <BaoLoi loi={loi} thuLai={taiLai} />

  const tu = new Date(g.tu)
  const den = new Date(g.den)
  const bayGio = new Date(g.bayGio)
  const tongMs = den - tu
  const ngays = Array.from({ length: g.soNgay }, (_, i) => {
    const d = new Date(tu)
    d.setDate(d.getDate() + i)
    return d
  })
  const viTriCuoiTuan = ngays.map((d, i) => (d.getDay() === 0 || d.getDay() === 6 ? i : -1)).filter((i) => i >= 0)
  const homNay = ngayInput(bayGio)
  const phanTramBayGio = ((bayGio - tu) / tongMs) * 100

  const hangs = g.ben.map((b) => ({
    khoa: 'ben-' + b.maBen,
    ten: b.tenBen,
    phu: `${b.soCau} cẩu bờ${b.trangThai && b.trangThai !== 'Hoạt động' ? ' · ' + b.trangThai.toLowerCase() : ''}`,
    chuyen: g.chuyen.filter((c) => c.maBen === b.maBen),
  }))
  const chuaBen = g.chuyen.filter((c) => c.maBen == null)
  if (chuaBen.length) hangs.push({ khoa: 'chua-ben', ten: 'Chưa phân bến', canhBao: true, chuyen: chuaBen })

  const trongKy = g.chuyen.filter((c) => new Date(c.ketThuc) > tu && new Date(c.batDau) < den)
  const soLamHang = g.chuyen.filter((c) => c.ata && !c.atd).length
  const soTre = g.chuyen.filter((c) => c.quaHan).length

  const doiCuaSo = (soNgayDich) => {
    const d = new Date(tu)
    d.setDate(d.getDate() + soNgayDich)
    datLoc({ tu: ngayInput(d) })
  }

  function thanh(c) {
    const batDau = Math.max(new Date(c.batDau), tu)
    const ketThuc = Math.min(new Date(c.ketThuc), den)
    if (ketThuc <= batDau) return null
    const trai = ((batDau - tu) / tongMs) * 100
    const rong = Math.max(((ketThuc - batDau) / tongMs) * 100, 1.6)
    const moTa =
      `${c.maChuyenHieu} · ${c.tenTau || '?'} — ${gioNgayNgan(c.batDau)} → ${gioNgayNgan(c.ketThuc)}` +
      ` · nhập ${dinhDangSo(c.contNhap)} / xuất ${dinhDangSo(c.contXuat)} cont · ${c.trangThai}` +
      (c.quaHan ? ' · QUÁ HẠN ETD' : '') +
      (c.trungLich ? ' · TRÙNG LỊCH' : '')
    return (
      <Link
        key={c.maChuyen}
        to={'/admin/vesselcalls/form/' + c.maChuyen}
        className={'tb-gantt-thanh ' + kieuThanh(c) + (c.trungLich ? ' trung' : '')}
        style={{ left: trai + '%', width: rong + '%' }}
        title={moTa + ' — bấm để sửa chuyến'}
        aria-label={moTa}
      >
        <b>
          {c.trungLich && <i className="bi bi-exclamation-triangle-fill me-1" />}
          {c.maChuyenHieu}
          {c.quaHan ? ' · TRỄ' : ''}
        </b>
        <span>{c.tenTau}</span>
      </Link>
    )
  }

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <TheChiSo icon="bi-calendar3-week" mau="xanh" so={dinhDangSo(trongKy.length)} nhan={`Chuyến trong ${g.soNgay} ngày`} />
        </div>
        <div className="col">
          <TheChiSo icon="bi-boxes" mau="la" so={dinhDangSo(soLamHang)} nhan="Tàu đang nằm bến" />
        </div>
        <div className="col">
          <TheChiSo icon="bi-exclamation-octagon" mau={g.canhBao.length ? 'do' : 'la'} so={dinhDangSo(g.canhBao.length)} nhan={soTre ? `Xung đột · ${soTre} tàu trễ ETD` : 'Xung đột lịch'} />
        </div>
        <div className="col">
          <Link to="/admin/aitools/berth" className="tb-the-link" title="Mở AI gợi ý phân bổ bến">
            <TheChiSo icon="bi-stars" mau={chuaBen.length ? 'cam' : 'la'} so={dinhDangSo(chuaBen.length)} nhan="Chưa phân bến · AI gợi ý" />
          </Link>
        </div>
      </div>

      {g.canhBao.length ? (
        <div className="alert alert-danger py-2" role="alert">
          <div className="fw-bold">
            <i className="bi bi-exclamation-octagon me-1" />
            Phát hiện {g.canhBao.length} xung đột lịch cầu bến — điều độ cần xử lý:
          </div>
          <ul className="small mb-0 mt-1 ps-3">
            {g.canhBao.map((cb, i) => (
              <li key={i}>{cb}</li>
            ))}
          </ul>
        </div>
      ) : (
        <div className="alert alert-success py-2">
          <i className="bi bi-check-circle me-1" />
          Không có xung đột lịch cầu bến trong cửa sổ {g.soNgay} ngày.
        </div>
      )}

      <TheNoiDung
        tieuDe={`${ngayThang(ngays[0])} → ${ngayThang(ngays[ngays.length - 1])} (${g.soNgay} ngày)`}
        icon="bi-bar-chart-steps"
        phai={
          <div className="d-flex flex-wrap align-items-center gap-2">
            <div className="btn-group btn-group-sm" role="group" aria-label="Đổi khoảng thời gian">
              <button type="button" className="btn btn-outline-secondary" onClick={() => doiCuaSo(-7)} title="Lùi 7 ngày" aria-label="Lùi 7 ngày">
                <i className="bi bi-chevron-left" />
              </button>
              <button type="button" className="btn btn-outline-secondary" onClick={xoaLoc} disabled={!loc.tu && loc.soNgay === 9}>
                Hôm nay
              </button>
              <button type="button" className="btn btn-outline-secondary" onClick={() => doiCuaSo(7)} title="Tới 7 ngày" aria-label="Tới 7 ngày">
                <i className="bi bi-chevron-right" />
              </button>
            </div>
            <input
              type="date"
              className="form-control form-control-sm"
              style={{ width: 150 }}
              value={ngayInput(tu)}
              onChange={(e) => e.target.value && datLoc({ tu: e.target.value })}
              aria-label="Ngày bắt đầu"
            />
            <select
              className="form-select form-select-sm"
              style={{ width: 110 }}
              value={g.soNgay}
              onChange={(e) => datLoc({ soNgay: Number(e.target.value) })}
              aria-label="Số ngày hiển thị"
            >
              {[...new Set([...SO_NGAY, g.soNgay])].sort((a, b) => a - b).map((n) => (
                <option key={n} value={n}>
                  {n} ngày
                </option>
              ))}
            </select>
            <Link className="btn btn-sm btn-outline-primary" to="/admin/berths">
              <i className="bi bi-diagram-3 me-1" />
              Sơ đồ bến
            </Link>
          </div>
        }
      >
        <div className="d-flex flex-wrap gap-3 mb-2">
          {CHU_GIAI.map(([lop, ten]) => (
            <span key={lop} className="tb-chu-giai">
              <i className={lop} />
              {ten}
            </span>
          ))}
          <span className="tb-chu-giai">
            <i className="tb-mau-trung" style={{ width: 2, borderRadius: 0 }} />
            Bây giờ
          </span>
        </div>

        {hangs.length === 0 ? (
          <div className="text-center text-body-secondary py-4">
            <i className="bi bi-inbox me-2" />
            Chưa có bến nào.
          </div>
        ) : (
          <div className="tb-gantt-cuon">
            <div className="tb-gantt" style={{ '--tb-so-ngay': g.soNgay, minWidth: Math.max(980, 140 + g.soNgay * 92) }}>
              <div className="tb-gantt-hang dau">
                <div className="tb-gantt-nhan" />
                <div className="tb-gantt-truc">
                  {ngays.map((d) => (
                    <div
                      key={d.getTime()}
                      className={'tb-gantt-ngay' + (d.getDay() === 0 || d.getDay() === 6 ? ' cuoi-tuan' : '') + (ngayInput(d) === homNay ? ' hom-nay' : '')}
                    >
                      {ngayThang(d)}
                      <br />
                      <span>{THU[d.getDay()]}</span>
                    </div>
                  ))}
                </div>
              </div>

              {hangs.map((h) => (
                <div className="tb-gantt-hang" key={h.khoa}>
                  <div className="tb-gantt-nhan">
                    {h.canhBao ? (
                      <span className="text-warning-emphasis">
                        <i className="bi bi-exclamation-triangle me-1" />
                        {h.ten}
                      </span>
                    ) : (
                      <>
                        <span className="text-truncate">
                          <i className="bi bi-life-preserver me-1 text-primary" />
                          {h.ten}
                        </span>
                        <small>{h.phu}</small>
                      </>
                    )}
                  </div>
                  <div className="tb-gantt-lan">
                    {viTriCuoiTuan.map((i) => (
                      <div key={i} className="tb-gantt-cuoi-tuan" style={{ left: (i / g.soNgay) * 100 + '%', width: 100 / g.soNgay + '%' }} />
                    ))}
                    {phanTramBayGio >= 0 && phanTramBayGio <= 100 && <div className="tb-gantt-bay-gio" style={{ left: phanTramBayGio + '%' }} />}
                    {h.chuyen.map(thanh)}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="small text-body-secondary mt-2">
          <i className="bi bi-lightbulb me-1" />
          Vạch đỏ = bây giờ · bấm thanh để sửa chuyến · tàu chưa phân bến xếp ở <Link to="/admin/vesselcalls">Điều độ chuyến tàu</Link> hoặc{' '}
          <Link to="/admin/aitools/berth">AI phân bổ bến</Link>.
        </div>
      </TheNoiDung>
    </>
  )
}
