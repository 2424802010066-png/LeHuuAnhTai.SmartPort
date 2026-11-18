import { useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'

const TRONG = { soCont: '', soUN: '', tenHang: '', lopIMDG: '3', nhomDongGoi: '', oNhiemBien: false }
const MAU_O = { 1: 'table-success', 2: 'table-warning', 3: 'table-danger', 4: 'table-danger' }

/**
 * HANG NGUY HIEM IMDG: khai bao theo so UN + lop, doi chieu moi cap trong bai voi bang cach ly IMDG 7.2.4,
 * goi y o trong an toan. Cu ly doi ra met la tham so noi bo cua cang.
 */
export default function HangNguyHiem() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/hang-nguy-hiem')
  const { duLieu: mt } = useApi('/api/hang-nguy-hiem/ma-tran')
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState(TRONG)
  const [goiY, setGoiY] = useState(null)

  const cap = d?.cap ?? []
  const viPham = cap.filter((c) => c.viPham)

  async function khai(e) {
    e.preventDefault()
    if (await chay(() => goiApi('/api/hang-nguy-hiem', { method: 'POST', body: f }))) {
      setF(TRONG)
      taiLai({ im: true })
    }
  }

  async function xemGoiY(soCont) {
    const kq = await chay(() => goiApi('/api/hang-nguy-hiem/goi-y?soCont=' + soCont), { imLang: true })
    if (kq) setGoiY(kq)
  }

  async function huy(k) {
    if (await chay(() => goiApi('/api/hang-nguy-hiem/' + k.maKhaiBao, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const COT = [
    { khoa: 'so', ten: 'Container', hien: (k) => <span className="font-monospace fw-semibold">{k.soCont}</span> },
    { khoa: 'un', ten: 'UN', hien: (k) => k.soUN },
    { khoa: 'ten', ten: 'Tên hàng', an: 'md', hien: (k) => <span className="small">{k.tenHang}</span> },
    { khoa: 'lop', ten: 'Lớp IMDG', hien: (k) => <span className="small"><b>{k.lopIMDG}</b> · {k.tenLop}</span> },
    { khoa: 'vt', ten: 'Vị trí bãi', hien: (k) => <span className="font-monospace small">{k.viTri || 'chưa có'}</span> },
    {
      khoa: 'tt',
      ten: '',
      canPhai: true,
      hien: (k) => (
        <div className="d-flex gap-1 justify-content-end">
          <button type="button" className="btn btn-sm btn-outline-primary" disabled={dangChay} onClick={() => xemGoiY(k.soCont)}>
            Gợi ý ô an toàn
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangChay} onClick={() => huy(k)} title="Hủy khai báo">
            <i className="bi bi-x-lg" />
          </button>
        </div>
      ),
    },
  ]

  const COT_CAP = [
    { khoa: 'a', ten: 'Container A', hien: (c) => <span className="font-monospace small">{c.soA} ({c.lopA}) · {c.viTriA}</span> },
    { khoa: 'b', ten: 'Container B', hien: (c) => <span className="font-monospace small">{c.soB} ({c.lopB}) · {c.viTriB}</span> },
    { khoa: 'yc', ten: 'Yêu cầu cách ly', hien: (c) => <span className="small">{c.tenCachLy}{c.canM ? ' · ≥ ' + c.canM + ' m' : ''}</span> },
    { khoa: 'kc', ten: 'Thực tế', hien: (c) => <span className="small">{c.thucTeM} m</span> },
    {
      khoa: 'kq',
      ten: 'Kết luận',
      hien: (c) =>
        c.viPham ? <span className="badge text-bg-danger">Vi phạm</span> : c.canTraThem ? <span className="badge text-bg-secondary">Tra DGL</span> : <span className="badge text-bg-success">Đạt</span>,
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-4">
          <TheChiSo icon="bi-radioactive" mau="cam" so={d?.khai.length ?? 0} nhan="container hàng nguy hiểm đang khai báo" />
        </div>
        <div className="col-6 col-lg-4">
          <TheChiSo icon="bi-exclamation-triangle" mau="do" so={viPham.length} nhan="cặp vi phạm khoảng cách cách ly" />
        </div>
        <div className="col-12 col-lg-4">
          <TheChiSo icon="bi-check2-square" mau="la" so={cap.length} nhan="cặp đã đối chiếu bảng IMDG 7.2.4" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-12 col-xl-8">
          <TheNoiDung tieuDe="Container hàng nguy hiểm trong bãi" icon="bi-radioactive">
            <BangDuLieu cot={COT} dong={d?.khai} khoaDong="maKhaiBao" dangTai={dangTai} rong="Chưa có khai báo hàng nguy hiểm." />
            {goiY && (
              <div className="alert alert-info mt-3 mb-0">
                <b>Ô an toàn cho {goiY.soCont}</b> (lớp {goiY.lopIMDG}, đang ở {goiY.viTriHienTai || 'chưa có vị trí'}):{' '}
                {goiY.goiY.length === 0 ? 'không còn ô trống đạt yêu cầu.' : goiY.goiY.map((o) => o.viTri + (o.duAnToanM != null ? ' (dư ' + o.duAnToanM + ' m)' : '')).join(', ')}
              </div>
            )}
          </TheNoiDung>
          <TheNoiDung tieuDe="Đối chiếu từng cặp trong bãi" icon="bi-arrows-angle-expand">
            <BangDuLieu cot={COT_CAP} dong={cap} khoaDong={(c) => c.soA + c.soB} dangTai={dangTai} rong="Cần ít nhất 2 container hàng nguy hiểm có vị trí bãi." />
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Khai báo mới" icon="bi-plus-circle" khongDem>
            <form onSubmit={khai}>
              <div className="mb-2">
                <label className="form-label small mb-1" htmlFor="dg-so">Số container</label>
                <input id="dg-so" className="form-control form-control-sm font-monospace" maxLength={11} required value={f.soCont} onChange={(e) => setF({ ...f, soCont: e.target.value.toUpperCase() })} />
              </div>
              <div className="row g-2 mb-2">
                <div className="col-5">
                  <label className="form-label small mb-1" htmlFor="dg-un">Số UN</label>
                  <input id="dg-un" className="form-control form-control-sm" maxLength={4} placeholder="1203" required value={f.soUN} onChange={(e) => setF({ ...f, soUN: e.target.value })} />
                </div>
                <div className="col-7">
                  <label className="form-label small mb-1" htmlFor="dg-lop">Lớp IMDG</label>
                  <select id="dg-lop" className="form-select form-select-sm" value={f.lopIMDG} onChange={(e) => setF({ ...f, lopIMDG: e.target.value })}>
                    {(mt?.lop ?? []).map((l) => (
                      <option key={l.ma} value={l.ma}>{l.ma} · {l.ten}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="mb-2">
                <label className="form-label small mb-1" htmlFor="dg-ten">Tên hàng theo vận đơn</label>
                <input id="dg-ten" className="form-control form-control-sm" required value={f.tenHang} onChange={(e) => setF({ ...f, tenHang: e.target.value })} />
              </div>
              <button className="btn btn-sm btn-primary w-100" disabled={dangChay}>Lưu khai báo</button>
            </form>
          </TheNoiDung>
        </div>
      </div>

      {mt && (
        <TheNoiDung tieuDe="Bảng cách ly IMDG 7.2.4" icon="bi-table" khongDem>
          <div className="table-responsive">
            <table className="table table-sm table-bordered text-center small mb-2">
              <thead>
                <tr>
                  <th />
                  {mt.lop.map((l) => <th key={l.ma} title={l.ten}>{l.ma}</th>)}
                </tr>
              </thead>
              <tbody>
                {mt.bang.map((hang, i) => (
                  <tr key={mt.lop[i].ma}>
                    <th title={mt.lop[i].ten}>{mt.lop[i].ma}</th>
                    {hang.map((o, j) => <td key={j} className={MAU_O[o] ?? ''}>{o}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="small text-body-secondary">
            {mt.chuGiai.map((c) => c.ma + ' = ' + c.ten + ' (≥ ' + c.canM + ' m)').join(' · ')}. {mt.ghiChu}
          </div>
        </TheNoiDung>
      )}
    </>
  )
}
