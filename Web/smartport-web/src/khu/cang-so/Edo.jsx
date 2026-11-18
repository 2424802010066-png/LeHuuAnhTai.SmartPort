import { useMemo, useState } from 'react'
import { goiApi, taoBoKhoaGuiLap, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, TheChiSo, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import './cang-so.css'

const TRANG_THAI = ['Hiệu lực', 'Đã sử dụng', 'Hết hạn', 'Đã hủy']
const MAU = { 'Hiệu lực': 'ok', 'Đã sử dụng': 'info', 'Hết hạn': 'off', 'Đã hủy': 'off' }

/**
 * LENH GIAO HANG DIEN TU (eDO) — thay lenh giay D/O: hang tau phat hanh, KY HMAC-SHA256, cong quet QR xac thuc.
 * Cong (che do chan) khong cho container ra neu eDO khong hop le / het han / da dung / sai chu ky.
 */
export default function Edo() {
  const [loc, datLoc] = useBoLoc({ tuKhoa: '', trangThai: '' })
  const { duLieu: ds, loi, dangTai, taiLai } = useApi('/api/edo' + taoQuery(loc))
  const { duLieu: tk, taiLai: taiTk } = useApi('/api/edo/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [moPhatHanh, setMoPhatHanh] = useState(false)
  const [moXacThuc, setMoXacThuc] = useState(false)
  const [xemQr, setXemQr] = useState(null)

  useNgheSuKien('edo.*', () => {
    taiLai({ im: true })
    taiTk({ im: true })
  })

  async function huy(e) {
    const dongY = await xacNhan({
      tieuDe: 'Hủy ' + e.soEDO + '?',
      noiDung: 'Người nhận hàng sẽ không lấy được container ' + e.soCont + ' bằng eDO này nữa.',
      nutDongY: 'Hủy eDO',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/edo/' + e.maEDO + '/huy', { method: 'POST', body: { lyDo: 'Hãng tàu thu hồi' } }))) {
      taiLai({ im: true })
      taiTk({ im: true })
    }
  }

  const COT = [
    { khoa: 'so', ten: 'Số eDO', hien: (e) => <span className="cs-ma">{e.soEDO}</span> },
    { khoa: 'cont', ten: 'Container', hien: (e) => <span className="cs-ma">{e.soCont}</span> },
    { khoa: 'bl', ten: 'Vận đơn', an: 'lg', hien: (e) => <span className="small">{e.soVanDon || '—'}</span> },
    { khoa: 'hang', ten: 'Hãng tàu', an: 'xl', hien: (e) => <span className="small">{e.hangTau || '—'}</span> },
    { khoa: 'nhan', ten: 'Người nhận', an: 'xl', hien: (e) => <span className="small">{e.khachHang || e.tenNguoiNhan || '—'}</span> },
    {
      khoa: 'han',
      ten: 'Hạn lấy hàng',
      an: 'md',
      hien: (e) => (
        <span className={'small' + (e.conGio != null && e.conGio < 24 ? ' text-danger fw-semibold' : '')}>
          {gioNgayNgan(e.hanLenh)}
          {e.conGio != null && <span className="text-body-secondary"> · còn {dinhDangSo(e.conGio, 1)}h</span>}
        </span>
      ),
    },
    {
      khoa: 'ky',
      ten: 'Chữ ký',
      hien: (e) =>
        e.chuKyDung ? (
          <span className="text-success small" title="HMAC-SHA256 khớp dữ liệu gốc">
            <i className="bi bi-patch-check-fill me-1" />
            Đúng
          </span>
        ) : (
          <span className="text-danger small fw-semibold">
            <i className="bi bi-exclamation-octagon-fill me-1" />
            SAI
          </span>
        ),
    },
    { khoa: 'tt', ten: 'Trạng thái', hien: (e) => <NhanTrangThai giaTri={e.trangThai} mau={MAU[e.trangThai]} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 96,
      hien: (e) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-secondary me-1" title="Mã QR xác thực" onClick={() => setXemQr(e)}>
            <i className="bi bi-qr-code" />
          </button>
          {e.trangThai === 'Hiệu lực' && (
            <button type="button" className="btn btn-sm btn-outline-danger" title="Hủy eDO" disabled={dangChay} onClick={() => huy(e)}>
              <i className="bi bi-x-lg" />
            </button>
          )}
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-file-earmark-check" mau="la" so={dinhDangSo(tk?.hieuLuc ?? 0)} nhan="eDO còn hiệu lực" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-alarm" mau="cam" so={dinhDangSo(tk?.sapHetHan ?? 0)} nhan="sắp hết hạn (24 giờ)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-arrow-right" mau="xanh" so={dinhDangSo(tk?.daSuDung ?? 0)} nhan="đã dùng nhận container" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-slash-circle" mau="do" so={dinhDangSo((tk?.hetHan ?? 0) + (tk?.daHuy ?? 0))} nhan="hết hạn / đã hủy" />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Lệnh giao hàng điện tử (eDO)"
        icon="bi-file-earmark-lock2"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.tuKhoa} doi={(v) => datLoc({ tuKhoa: v })} goiY="Số eDO, container, vận đơn…" />
            <select
              className="form-select form-select-sm"
              style={{ maxWidth: 150 }}
              value={loc.trangThai}
              aria-label="Lọc theo trạng thái"
              onChange={(e) => datLoc({ trangThai: e.target.value })}
            >
              <option value="">— Mọi trạng thái —</option>
              {TRANG_THAI.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
            <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setMoXacThuc(true)}>
              <i className="bi bi-qr-code-scan me-1" />
              Xác thực QR
            </button>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => setMoPhatHanh(true)}>
              <i className="bi bi-plus-lg me-1" />
              Phát hành eDO
            </button>
          </>
        }
      >
        <BangDuLieu cot={COT} dong={ds} khoaDong="maEDO" dangTai={dangTai} rong="Chưa có eDO nào — bấm &quot;Phát hành eDO&quot;." />
      </TheNoiDung>

      <PhatHanhEdo
        mo={moPhatHanh}
        dong={() => setMoPhatHanh(false)}
        xong={() => {
          setMoPhatHanh(false)
          taiLai({ im: true })
          taiTk({ im: true })
        }}
      />
      <XacThucEdo mo={moXacThuc} dong={() => setMoXacThuc(false)} />
      <HopThoai mo={!!xemQr} dong={() => setXemQr(null)} tieuDe={'Mã QR ' + (xemQr?.soEDO ?? '')} icon="bi-qr-code">
        {xemQr && (
          <>
            <p className="small text-body-secondary">
              Nội dung mã QR in trên eDO. Cổng quét → hệ thống tính lại chữ ký HMAC-SHA256 → sửa bất kỳ ký tự nào (số container, mã xác thực) đều bị phát hiện.
            </p>
            <pre className="cs-qr">{xemQr.qr}</pre>
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => navigator.clipboard?.writeText(xemQr.qr)}>
              <i className="bi bi-clipboard me-1" />
              Sao chép
            </button>
          </>
        )}
      </HopThoai>
    </>
  )
}

function PhatHanhEdo({ mo, dong, xong }) {
  const { duLieu: lc } = useApi(mo ? '/api/edo/lua-chon' : null)
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState({ soCont: '', soVanDon: '', maHangTau: '', maKH: '', tenNguoiNhan: '', soNgayHieuLuc: 5, ghiChu: '' })
  const [khoaGui] = useState(taoBoKhoaGuiLap) // Idempotency-Key: bam lai sau loi mang khong phat hanh 2 eDO
  const doi = (k) => (e) => setF((x) => ({ ...x, [k]: e.target.value }))
  const cont = useMemo(() => (lc?.container ?? []).find((c) => c.soCont === f.soCont), [lc, f.soCont])

  async function gui(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/edo', {
        method: 'POST',
        body: {
          ...f,
          maHangTau: f.maHangTau ? Number(f.maHangTau) : cont?.maHangTau ?? null,
          maKH: f.maKH ? Number(f.maKH) : cont?.maKH ?? null,
          soNgayHieuLuc: Number(f.soNgayHieuLuc) || 5,
        },
        chongGuiLap: khoaGui,
      }),
    )
    if (kq) xong()
  }

  return (
    <HopThoai
      mo={mo}
      dong={dong}
      khoaDong={dangChay}
      tieuDe="Phát hành lệnh giao hàng điện tử"
      icon="bi-file-earmark-plus"
      khiGui={gui}
      chan={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={dong} disabled={dangChay}>
            Đóng
          </button>
          <button type="submit" className="btn btn-primary" disabled={dangChay || !f.soCont}>
            Phát hành & ký
          </button>
        </>
      }
    >
      <Truong nhan="Container (đang trong bãi, chưa có eDO)" batBuoc>
        <select className="form-select" value={f.soCont} onChange={doi('soCont')} required>
          <option value="">— Chọn container —</option>
          {(lc?.container ?? []).map((c) => (
            <option key={c.soCont} value={c.soCont}>
              {c.soCont} · {c.loaiCont} · {c.tinhTrang}
              {c.hangHoa ? ' · ' + c.hangHoa : ''}
            </option>
          ))}
        </select>
      </Truong>
      <div className="row g-2">
        <div className="col-6">
          <Truong nhan="Số vận đơn (B/L)">
            <input className="form-control" value={f.soVanDon} onChange={doi('soVanDon')} maxLength={30} />
          </Truong>
        </div>
        <div className="col-6">
          <Truong nhan="Hiệu lực (ngày)" goiY="1–30 ngày">
            <input className="form-control" type="number" min={1} max={30} value={f.soNgayHieuLuc} onChange={doi('soNgayHieuLuc')} />
          </Truong>
        </div>
      </div>
      <Truong nhan="Hãng tàu phát hành" goiY="Bỏ trống = lấy theo container">
        <select className="form-select" value={f.maHangTau} onChange={doi('maHangTau')}>
          <option value="">— Theo container —</option>
          {(lc?.hangTau ?? []).map((h) => (
            <option key={h.maHangTau} value={h.maHangTau}>
              {h.tenHangTau}
            </option>
          ))}
        </select>
      </Truong>
      <Truong nhan="Người nhận hàng (khách hàng)">
        <select className="form-select" value={f.maKH} onChange={doi('maKH')}>
          <option value="">— Theo container —</option>
          {(lc?.khachHang ?? []).map((k) => (
            <option key={k.maKH} value={k.maKH}>
              {k.tenKH}
            </option>
          ))}
        </select>
      </Truong>
      <Truong nhan="Ghi chú">
        <input className="form-control" value={f.ghiChu} onChange={doi('ghiChu')} maxLength={300} />
      </Truong>
    </HopThoai>
  )
}

function XacThucEdo({ mo, dong }) {
  const [qr, setQr] = useState('')
  const [kq, setKq] = useState(null)
  const { chay, dangChay } = useThaoTac()

  async function gui(e) {
    e.preventDefault()
    const r = await chay(() => goiApi('/api/edo/xac-thuc', { method: 'POST', body: { qr: qr.trim() } }), { imLang: true })
    if (r) setKq(r)
  }

  return (
    <HopThoai
      mo={mo}
      dong={() => {
        setKq(null)
        dong()
      }}
      tieuDe="Xác thực eDO tại cổng"
      icon="bi-qr-code-scan"
      khiGui={gui}
      chan={
        <button type="submit" className="btn btn-primary" disabled={dangChay || !qr.trim()}>
          Kiểm tra
        </button>
      }
    >
      <Truong nhan="Nội dung mã QR" goiY="Dạng EDO|số eDO|số container|mã xác thực — dán từ máy quét">
        <textarea className="form-control font-monospace" rows={3} value={qr} onChange={(e) => setQr(e.target.value)} />
      </Truong>
      {kq && (
        <div className={'alert mb-0 ' + (kq.hopLe ? 'alert-success' : 'alert-danger')}>
          <i className={'bi me-2 ' + (kq.hopLe ? 'bi-patch-check-fill' : 'bi-shield-x')} />
          <b>{kq.hopLe ? 'HỢP LỆ' : 'KHÔNG HỢP LỆ'}</b> — {kq.lyDo}
        </div>
      )}
    </HopThoai>
  )
}
