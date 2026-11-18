import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { BienSoXe, SoCont } from './dungChung'
import './cong.css'

const RONG_1 = { bienSo: '', soCont: '', khoiLuong: '', ghiChu: '' }

/**
 * TRAM CAN XE (weighbridge) · VGM — thay Admin/Gate/PhieuCan + LuuCan:
 * can luc vao (lan 1) + can luc ra (lan 2) -> khoi luong hang; phieu chot tu ghi Kho chung tu.
 */
export default function TramCan() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/tram-can')
  const { chay, dangChay } = useThaoTac()
  const { gt, doi, datLai } = useBieuMau(RONG_1)
  const [can2, setCan2] = useState({ maPC: '', khoiLuong: '' })

  async function canLan1(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/tram-can', {
        method: 'POST',
        body: { bienSo: gt.bienSo, soCont: gt.soCont, khoiLuong: gt.khoiLuong === '' ? null : Number(gt.khoiLuong), ghiChu: gt.ghiChu },
      }),
    )
    if (kq) {
      datLai(RONG_1)
      taiLai({ im: true })
    }
  }

  async function chotPhieu(e) {
    e.preventDefault()
    if (!can2.maPC) return
    const kq = await chay(() =>
      goiApi('/api/tram-can/' + can2.maPC + '/can-lan-2', { method: 'POST', body: { khoiLuong: can2.khoiLuong === '' ? null : Number(can2.khoiLuong) } }),
    )
    if (kq) {
      setCan2({ maPC: '', khoiLuong: '' })
      taiLai({ im: true })
    }
  }

  const COT = [
    { khoa: 'so', ten: 'Số phiếu', hien: (p) => <span className="cg-ma">{p.soPhieu}</span> },
    { khoa: 'xe', ten: 'Biển số', hien: (p) => <BienSoXe bienSo={p.bienSoXe} /> },
    { khoa: 'cont', ten: 'Container', an: 'md', hien: (p) => <SoCont so={p.soCont} /> },
    { khoa: 'c1', ten: 'Cân 1 (kg)', canPhai: true, hien: (p) => dinhDangSo(p.canLan1) },
    {
      khoa: 'c2',
      ten: 'Cân 2 (kg)',
      canPhai: true,
      hien: (p) => (p.canLan2 != null ? dinhDangSo(p.canLan2) : <span className="text-warning-emphasis small">chờ cân…</span>),
    },
    {
      khoa: 'hang',
      ten: 'Hàng (kg)',
      canPhai: true,
      hien: (p) => (p.khoiLuongHang != null ? <b className="text-danger-emphasis">{dinhDangSo(p.khoiLuongHang)}</b> : '—'),
    },
    { khoa: 'luc', ten: 'Cân lần 1 lúc', an: 'lg', hien: (p) => <span className="small">{gioNgayNgan(p.luc1)}</span> },
    {
      khoa: 'in',
      ten: '',
      canPhai: true,
      rong: 52,
      hien: (p) => (
        <Link className="btn btn-sm btn-outline-secondary" to={'/admin/gate/inphieucan/' + p.maPC} title={'In phiếu cân ' + p.soPhieu}>
          <i className="bi bi-printer" />
        </Link>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-ticket-detailed" mau="xanh" so={dinhDangSo(d?.phieuHomNay ?? 0)} nhan="phiếu lập hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-hourglass-split" mau="cam" so={dinhDangSo(d?.soChoCanLan2 ?? 0)} nhan="chờ cân lần 2" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-check2-circle" mau="la" so={dinhDangSo(d?.daChotHomNay ?? 0)} nhan="phiếu chốt hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-seam" mau="xanh-ngoc" nho so={dinhDangSo(d?.hangHomNayKg ?? 0) + ' kg'} nhan="khối lượng hàng hôm nay" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung tieuDe="Cân lần 1 — xe vào trạm" icon="bi-1-circle">
            <form onSubmit={canLan1}>
              <Truong nhan="Biển số xe" batBuoc lop="mb-2">
                <input className="form-control sp-o-nhap-bien" value={gt.bienSo} onChange={doi('bienSo')} maxLength={15} placeholder="51C-638.72" />
              </Truong>
              <Truong nhan="Số container (nếu chở)" lop="mb-2">
                <input className="form-control text-uppercase" value={gt.soCont} onChange={doi('soCont')} maxLength={11} placeholder="MSCU2201417" />
              </Truong>
              <Truong nhan="Số cân hiển thị (kg)" batBuoc lop="mb-2">
                <input className="form-control" type="number" step="0.1" min="1" value={gt.khoiLuong} onChange={doi('khoiLuong')} placeholder="32500" />
              </Truong>
              <Truong nhan="Ghi chú" lop="mb-3">
                <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={200} />
              </Truong>
              <button className="btn btn-primary w-100" type="submit" disabled={dangChay}>
                <i className="bi bi-plus-circle me-1" />
                Tạo phiếu — cân lần 1
              </button>
            </form>
          </TheNoiDung>

          <TheNoiDung tieuDe="Cân lần 2 — chốt phiếu" icon="bi-2-circle">
            <form onSubmit={chotPhieu}>
              <Truong nhan="Phiếu đang chờ cân lần 2" batBuoc lop="mb-2">
                <select className="form-select" value={can2.maPC} onChange={(e) => setCan2((c) => ({ ...c, maPC: e.target.value }))}>
                  <option value="">— Chọn phiếu —</option>
                  {(d?.choCanLan2 || []).map((p) => (
                    <option key={p.maPC} value={p.maPC}>
                      {p.soPhieu} · {p.bienSoXe} · lần 1: {dinhDangSo(p.canLan1)} kg
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Số cân lần 2 (kg)" batBuoc lop="mb-3">
                <input
                  className="form-control"
                  type="number"
                  step="0.1"
                  min="1"
                  value={can2.khoiLuong}
                  onChange={(e) => setCan2((c) => ({ ...c, khoiLuong: e.target.value }))}
                />
              </Truong>
              <button className="btn btn-warning w-100" type="submit" disabled={dangChay || !can2.maPC}>
                <i className="bi bi-check2-circle me-1" />
                Chốt phiếu cân
              </button>
              <div className="form-text mt-2">Phiếu chốt được ghi vào Kho chứng từ (chuỗi băm) — không sửa được về sau.</div>
            </form>
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung tieuDe="Phiếu cân gần đây" icon="bi-clock-history" khongDem>
            <BangDuLieu cot={COT} dong={d?.ganDay} khoaDong="maPC" dangTai={dangTai} rong="Chưa có phiếu cân nào." />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
