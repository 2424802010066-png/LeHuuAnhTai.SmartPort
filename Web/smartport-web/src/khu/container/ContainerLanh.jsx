import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung, Truong, useThongBao } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { duongHoSo, soDo } from './chung'
import './container.css'

const W = 240
const H = 56
const DEM = 4

/** Bieu do nho 24h: duong nhiet do + vach net dut = nhiet do cai dat + cham do = luot do lech >= nguong canh bao */
function BieuDoNhiet({ diem, dat, nguong }) {
  if (!diem?.length) return <span className="small text-body-secondary">chưa có dữ liệu</span>
  const giaTri = diem.map((d) => Number(d.nhietDo))
  const min = Math.min(dat, ...giaTri) - 1.5
  const max = Math.max(dat, ...giaTri) + 1.5
  const y = (v) => H - DEM - ((v - min) / (max - min)) * (H - 2 * DEM)
  const x = (i) => (diem.length === 1 ? W / 2 : DEM + (i * (W - 2 * DEM)) / (diem.length - 1))
  const duong = giaTri.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ')
  const yDat = y(dat).toFixed(1)

  return (
    <svg className="cb-spark" viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Nhiệt độ 24 giờ, ${diem.length} lượt đọc`}>
      <line className="cb-spark-dat" x1="0" y1={yDat} x2={W} y2={yDat} />
      <polyline className="cb-spark-duong" points={duong} />
      {giaTri.map((v, i) =>
        Math.abs(v - dat) >= nguong ? <circle key={i} className="cb-spark-lech" cx={x(i).toFixed(1)} cy={y(v).toFixed(1)} r="2.6" /> : null,
      )}
    </svg>
  )
}

const MUC = {
  'chua-do': { chu: 'CHƯA ĐO', mau: 'off' },
  'on-dinh': { chu: 'ỔN ĐỊNH', mau: 'ok' },
  'canh-bao': { chu: 'CẢNH BÁO', mau: 'warn' },
  'nguy-cap': { chu: 'NGUY CẤP', mau: 'do' },
}

const LOP_SO_DO = { 'nguy-cap': 'text-danger', 'canh-bao': 'text-warning-emphasis', 'on-dinh': 'text-success' }

/** Giam sat container lanh 24/7 (thay Admin/Reefer): so doc IoT + do tay, canh bao lech nhiet. */
export default function ContainerLanh() {
  const { duLieu: bang, loi, dangTai, taiLai } = useApi('/api/container-lanh')
  const { chay, dangChay } = useThaoTac()
  const bao = useThongBao()
  const [maCont, setMaCont] = useState('')
  const [nhietDo, setNhietDo] = useState('')

  const lamMoi = useCallback(() => taiLai({ im: true }), [taiLai])
  // Container vao/roi bai qua cong -> danh sach cont lanh doi
  useNgheSuKien('bai.*', lamMoi)
  // Cam bien doc 30 phut/lan -> tu lam moi ngam 5 phut/lan khi man hinh dang mo
  useEffect(() => {
    const hen = setInterval(lamMoi, 5 * 60 * 1000)
    return () => clearInterval(hen)
  }, [lamMoi])

  const ds = bang?.cont ?? []
  const chonCont = maCont || (ds[0] ? String(ds[0].maCont) : '')

  async function ghiNhan(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/container-lanh/ghi-nhan', { method: 'POST', body: { maCont: Number(chonCont), nhietDo } }), { imLang: true })
    if (!kq) return
    // Luu thanh cong nhung lech NGUY CAP van phai bao do (giong TempData["Loi"] cua MVC)
    const muc = kq.duLieu?.mucDo
    if (muc === 'nguy-cap') bao.loi(kq.thongBao)
    else if (muc === 'canh-bao') bao.canhBao(kq.thongBao)
    else bao.thanhCong(kq.thongBao)
    setNhietDo('')
    lamMoi()
  }

  const nguongCb = bang?.lechCanhBao ?? 3
  const nguongNc = bang?.lechNguyCap ?? 6

  const COT = [
    {
      khoa: 'soCont',
      ten: 'Container',
      hien: (c) => (
        <>
          <Link className="cb-so-cont" to={duongHoSo(c.maCont)}>
            {c.soCont}
          </Link>
          <div className="small text-body-secondary">{c.loaiCont}</div>
        </>
      ),
    },
    { khoa: 'khachHang', ten: 'Khách hàng', an: 'lg', lop: 'small', hien: (c) => c.khachHang || '—' },
    { khoa: 'viTri', ten: 'Vị trí', an: 'md', hien: (c) => (c.viTriBai ? <span className="cb-vi-tri">{c.viTriBai}</span> : '—') },
    { khoa: 'dat', ten: 'Cài đặt', lop: 'text-center fw-semibold cb-lanh', hien: (c) => `${soDo(c.nhietDoCaiDat)}°C` },
    {
      khoa: 'doCuoi',
      ten: 'Đo gần nhất',
      lop: 'text-center',
      hien: (c) =>
        c.doCuoi ? (
          <>
            <span className={'fw-bold ' + (LOP_SO_DO[c.mucDo] || '')}>{soDo(c.doCuoi.nhietDo)}°C</span>
            <div className="small text-body-secondary text-nowrap">
              {gioNgayNgan(c.doCuoi.luc).slice(0, 5)} · {c.doCuoi.nguon === 'ThuCong' ? 'đo tay' : 'IoT'}
            </div>
          </>
        ) : (
          '—'
        ),
    },
    {
      khoa: 'mucDo',
      ten: 'Trạng thái',
      lop: 'text-center',
      hien: (c) => {
        const m = MUC[c.mucDo] || MUC['chua-do']
        return (
          <NhanTrangThai giaTri={m.chu} mau={m.mau}>
            {m.chu}
            {(c.mucDo === 'canh-bao' || c.mucDo === 'nguy-cap') && ` · lệch ${soDo(c.lech)}°C`}
          </NhanTrangThai>
        )
      },
    },
    { khoa: 'bieuDo', ten: 'Biểu đồ 24h', an: 'sm', hien: (c) => <BieuDoNhiet diem={c.diem24Gio} dat={Number(c.nhietDoCaiDat)} nguong={nguongCb} /> },
  ]

  return (
    <>
      <div className="sp-hop-ai cb-hop-lanh mb-3 d-flex flex-wrap align-items-center gap-2">
        <span className="sp-nhan-ai">
          <i className="bi bi-thermometer-snow me-1" />
          CONTAINER LẠNH 24/7
        </span>
        <span className="small" style={{ color: 'rgba(255,255,255,.85)' }}>
          Đọc nhiệt 30 phút/lần · lệch ≥ {soDo(nguongCb)}°C <span className="badge text-bg-warning">CẢNH BÁO</span> · ≥ {soDo(nguongNc)}°C{' '}
          <span className="badge text-bg-danger">NGUY CẤP</span> tự báo an ninh
        </span>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-thermometer-snow" mau="xanh-ngoc" so={bang ? dinhDangSo(bang.soContLanh) : '…'} nhan="Cont lạnh đang cắm điện" />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-exclamation-triangle" mau="cam" so={bang ? dinhDangSo(bang.soCanhBao) : '…'} nhan={`Cảnh báo (lệch ≥ ${soDo(nguongCb)}°C)`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-bell-fill" mau="do" so={bang ? dinhDangSo(bang.soNguyCap) : '…'} nhan={`Nguy cấp (lệch ≥ ${soDo(nguongNc)}°C)`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-clock-history" mau="xanh" so={bang ? dinhDangSo(bang.soLuotDo) : '…'} nhan="Lượt đọc nhiệt 24 giờ" />
        </div>
      </div>

      <TheNoiDung tieuDe="Nhập số đo tay" icon="bi-pencil-square">
        <form className="row g-2 align-items-end" onSubmit={ghiNhan}>
          <Truong nhan="Container" lop="col-md-5">
            <select className="form-select" value={chonCont} onChange={(e) => setMaCont(e.target.value)} required disabled={!ds.length}>
              {!ds.length && <option value="">Không có container lạnh trong bãi</option>}
              {ds.map((c) => (
                <option key={c.maCont} value={c.maCont}>
                  {c.soCont} — cài {soDo(c.nhietDoCaiDat)}°C{c.viTriBai ? ` (${c.viTriBai})` : ''}
                </option>
              ))}
            </select>
          </Truong>
          <Truong nhan="Nhiệt độ đo được (°C)" lop="col-md-3 col-7">
            <input className="form-control" type="number" step="0.1" min="-60" max="60" value={nhietDo} onChange={(e) => setNhietDo(e.target.value)} required placeholder="VD: -17.5" />
          </Truong>
          <div className="col-md-3 col-5">
            <button type="submit" className="btn btn-primary w-100" disabled={dangChay || !ds.length}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Ghi nhận
            </button>
          </div>
        </form>
      </TheNoiDung>

      <TheNoiDung
        tieuDe="Diễn biến nhiệt độ 24 giờ"
        icon="bi-graph-down"
        khongDem
        phai={
          <span className="small text-body-secondary">
            nét đứt = cài đặt · <span className="text-danger">●</span> = vượt ngưỡng
            {bang && <> · cập nhật {gioNgayNgan(bang.lucCapNhat).slice(0, 5)}</>}
          </span>
        }
        chan={
          <span className="small text-body-secondary">
            Sự cố nguy cấp tự hiện ở <Link to="/admin/security">Phòng an ninh</Link> và chuông thông báo.
          </span>
        }
      >
        <BangDuLieu cot={COT} dong={ds} khoaDong="maCont" dangTai={dangTai} rong="Không có container lạnh nào đang trong bãi." />
      </TheNoiDung>
    </>
  )
}
