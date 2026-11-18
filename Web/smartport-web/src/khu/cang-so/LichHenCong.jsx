import { useMemo, useState } from 'react'
import { goiApi, taoBoKhoaGuiLap, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { ngayInput } from '../../utils/dinhDang'
import './cang-so.css'

const MAU = { 'Đã đặt': 'info', 'Đúng giờ': 'ok', 'Đến sớm': 'run', 'Đến trễ': 'warn', 'Không đến': 'do', 'Đã hủy': 'off' }
const gio = (v) => new Date(v).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
const ngayNgan = (v) => new Date(v).toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit' })

/**
 * LICH HEN GIO VAO CONG (TAS): ban do nhiet khung gio (suc chua / da dat), dat lich, doi chieu dung - som - tre khi xe
 * vao cong, xe khong den, goi y khung gio vang de dan xe khoi gio cao diem (giam ket xe truoc cong).
 */
export default function LichHenCong() {
  const [ngay, setNgay] = useState(ngayInput(new Date()))
  const { duLieu: luoi, loi, taiLai: taiLuoi } = useApi('/api/lich-hen/luoi' + taoQuery({ ngay, soNgay: 3 }))
  const { duLieu: ds, dangTai, taiLai: taiDs } = useApi('/api/lich-hen' + taoQuery({ ngay }))
  const { duLieu: tk, taiLai: taiTk } = useApi('/api/lich-hen/thong-ke?soNgay=7')
  const { duLieu: goiY, taiLai: taiGoiY } = useApi('/api/lich-hen/goi-y' + taoQuery({ ngay }))
  const { chay, dangChay } = useThaoTac()
  const [dat, setDat] = useState(null)

  const lamMoi = () => {
    taiLuoi({ im: true })
    taiDs({ im: true })
    taiTk({ im: true })
    taiGoiY({ im: true })
  }
  useNgheSuKien('lich-hen.*', lamMoi)
  useNgheSuKien('cong.lenh-giao-nhan-*', lamMoi)

  // gom luoi theo ngay -> hang, gio -> cot
  const bang = useMemo(() => {
    const theoNgay = new Map()
    for (const o of luoi ?? []) {
      const k = new Date(o.luc).toDateString()
      if (!theoNgay.has(k)) theoNgay.set(k, [])
      theoNgay.get(k).push(o)
    }
    return [...theoNgay.values()]
  }, [luoi])
  const cacGio = bang[0]?.map((o) => new Date(o.luc).getHours()) ?? []

  async function huy(l) {
    if (await chay(() => goiApi('/api/lich-hen/' + l.maLich + '/huy', { method: 'POST' }))) lamMoi()
  }

  const COT = [
    { khoa: 'gio', ten: 'Khung hẹn', hien: (l) => <span className="cs-ma">{gio(l.henTuLuc)}–{gio(l.henDenLuc)}</span> },
    { khoa: 'xe', ten: 'Biển số', hien: (l) => <span className="cs-ma">{l.bienSo}</span> },
    { khoa: 'lenh', ten: 'Lệnh · container', an: 'md', hien: (l) => <span className="small">{l.soLenh || '—'}{l.soCont ? ' · ' + l.soCont : ''}</span> },
    { khoa: 'nguon', ten: 'Nguồn', an: 'lg', hien: (l) => <span className="small">{l.nguonDat}</span> },
    {
      khoa: 'den',
      ten: 'Đến lúc',
      an: 'md',
      hien: (l) =>
        l.denLuc ? (
          <span className="small">
            {gio(l.denLuc)}
            {l.soPhutLech ? <span className="text-body-secondary"> ({l.soPhutLech > 0 ? 'trễ ' : 'sớm '}{Math.abs(l.soPhutLech)}′)</span> : null}
          </span>
        ) : (
          <span className="text-body-secondary small">—</span>
        ),
    },
    { khoa: 'tt', ten: 'Trạng thái', hien: (l) => <NhanTrangThai giaTri={l.trangThai} mau={MAU[l.trangThai]} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 52,
      hien: (l) =>
        l.trangThai === 'Đã đặt' && (
          <button type="button" className="btn btn-sm btn-outline-danger" title="Hủy lịch hẹn" disabled={dangChay} onClick={() => huy(l)}>
            <i className="bi bi-x-lg" />
          </button>
        ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLuoi} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-alarm" mau="la" so={(tk?.dungGio ?? 0) + '%'} nhan="xe đến đúng giờ (7 ngày)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-skip-forward" mau="xanh" so={(tk?.denSom ?? 0) + '%'} nhan="đến sớm" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-hourglass-bottom" mau="cam" so={(tk?.denTre ?? 0) + '%'} nhan="đến trễ" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-person-x" mau="do" so={(tk?.khongDen ?? 0) + '%'} nhan="không đến (no-show)" />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Bản đồ khung giờ cổng — sức chứa / đã đặt"
        icon="bi-calendar-week"
        khongDem
        phai={
          <>
            <input type="date" className="form-control form-control-sm" style={{ maxWidth: 160 }} value={ngay} onChange={(e) => setNgay(e.target.value)} aria-label="Từ ngày" />
            <button type="button" className="btn btn-sm btn-primary" onClick={() => setDat({ henTuLuc: null })}>
              <i className="bi bi-plus-lg me-1" />
              Đặt lịch hẹn
            </button>
          </>
        }
      >
        <div className="table-responsive">
          <table className="table table-sm cs-luoi mb-1">
            <thead>
              <tr>
                <th />
                {cacGio.map((g) => (
                  <th key={g} className="text-center small">
                    {g}h
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {bang.map((hang) => (
                <tr key={hang[0].luc}>
                  <th className="small text-nowrap">{ngayNgan(hang[0].luc)}</th>
                  {hang.map((o) => {
                    const qua = new Date(o.luc).getTime() + 3600e3 < Date.now()
                    const muc = o.tiLe >= 1 ? 'day' : o.tiLe >= 0.67 ? 'cao' : o.tiLe >= 0.34 ? 'vua' : 'thap'
                    return (
                      <td key={o.luc} className="p-1">
                        <button
                          type="button"
                          className={'cs-o ' + (qua ? 'qua' : muc)}
                          disabled={qua || o.conTrong === 0}
                          title={o.daDat + '/' + o.sucChua + ' xe'}
                          onClick={() => setDat({ henTuLuc: o.luc })}
                        >
                          {o.daDat}/{o.sucChua}
                        </button>
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="small text-body-secondary">
          Bấm một ô còn chỗ để đặt lịch. Màu: <span className="cs-chu thap">vắng</span> <span className="cs-chu vua">vừa</span>{' '}
          <span className="cs-chu cao">đông</span> <span className="cs-chu day">kín</span>
        </div>
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-12 col-xl-8">
          <TheNoiDung tieuDe={'Lịch hẹn ngày ' + new Date(ngay).toLocaleDateString('vi-VN')} icon="bi-list-check">
            <BangDuLieu cot={COT} dong={ds} khoaDong="maLich" dangTai={dangTai} rong="Chưa có lịch hẹn trong ngày." />
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Gợi ý khung giờ vắng" icon="bi-lightbulb" khongDem>
            <p className="small text-body-secondary">Điểm = 60% tỷ lệ đã đặt + 40% lưu lượng xe lịch sử cùng giờ (14 ngày). Điểm thấp = ít kẹt.</p>
            {(goiY ?? []).length === 0 && <div className="small text-body-secondary">Không còn khung trống trong ngày.</div>}
            {(goiY ?? []).map((o) => (
              <button
                key={o.luc}
                type="button"
                className="btn btn-sm btn-outline-success w-100 text-start mb-2"
                onClick={() => setDat({ henTuLuc: o.luc })}
              >
                <b>{gio(o.luc)}</b> · còn {o.conTrong}/{o.sucChua} chỗ · TB {o.xeTrungBinhLichSu} xe/giờ
                <span className="float-end small">điểm {o.diem}</span>
              </button>
            ))}
          </TheNoiDung>
        </div>
      </div>

      <DatLich
        mo={!!dat}
        henTuLuc={dat?.henTuLuc}
        dong={() => setDat(null)}
        xong={() => {
          setDat(null)
          lamMoi()
        }}
      />
    </>
  )
}

function DatLich({ mo, henTuLuc, dong, xong }) {
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState({ bienSo: '', soCont: '', loaiLenh: '', gioHen: '' })
  const [khoaGui] = useState(taoBoKhoaGuiLap) // Idempotency-Key: bam lai sau loi mang khong dat 2 lich hen
  const doi = (k) => (e) => setF((x) => ({ ...x, [k]: e.target.value }))
  const macDinh = henTuLuc ? ngayInput(henTuLuc) + 'T' + String(new Date(henTuLuc).getHours()).padStart(2, '0') + ':00' : ''

  async function gui(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/lich-hen', {
        method: 'POST',
        body: { bienSo: f.bienSo, soCont: f.soCont || null, loaiLenh: f.loaiLenh || null, henTuLuc: f.gioHen || macDinh },
        chongGuiLap: khoaGui,
      }),
    )
    if (kq) {
      setF({ bienSo: '', soCont: '', loaiLenh: '', gioHen: '' })
      xong()
    }
  }

  return (
    <HopThoai
      mo={mo}
      dong={dong}
      khoaDong={dangChay}
      tieuDe="Đặt lịch hẹn vào cổng"
      icon="bi-calendar-plus"
      khiGui={gui}
      chan={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={dong} disabled={dangChay}>
            Đóng
          </button>
          <button type="submit" className="btn btn-primary" disabled={dangChay || !f.bienSo || !(f.gioHen || macDinh)}>
            Đặt lịch
          </button>
        </>
      }
    >
      <Truong nhan="Biển số xe" batBuoc>
        <input className="form-control text-uppercase" value={f.bienSo} onChange={doi('bienSo')} maxLength={15} required placeholder="51C-123.45" />
      </Truong>
      <Truong nhan="Khung giờ" batBuoc goiY="Mỗi khung 1 giờ, từ 6h đến 22h">
        <input type="datetime-local" step={3600} className="form-control" value={f.gioHen || macDinh} onChange={doi('gioHen')} required />
      </Truong>
      <div className="row g-2">
        <div className="col-6">
          <Truong nhan="Container (nếu có)">
            <input className="form-control text-uppercase" value={f.soCont} onChange={doi('soCont')} maxLength={11} />
          </Truong>
        </div>
        <div className="col-6">
          <Truong nhan="Việc">
            <select className="form-select" value={f.loaiLenh} onChange={doi('loaiLenh')}>
              <option value="">—</option>
              <option value="GiaoCont">Nhận container</option>
              <option value="HaCont">Hạ container</option>
              <option value="TraRong">Trả rỗng</option>
              <option value="LayRong">Lấy rỗng</option>
            </select>
          </Truong>
        </div>
      </div>
    </HopThoai>
  )
}
