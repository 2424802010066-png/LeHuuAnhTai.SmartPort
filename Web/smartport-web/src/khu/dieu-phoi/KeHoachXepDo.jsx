import { useEffect, useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'

const gioNgay = (v) => (v ? new Date(v).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }) : '—')

/** KE HOACH XEP DO TAU: lap vi tri xep cho container xuat, chia doan bay cho tung cau bo, uoc tinh gio xong so voi ETD. */
export default function KeHoachXepDo() {
  const { duLieu: ds, loi, taiLai } = useApi('/api/ke-hoach-xep-do/chuyen')
  const { chay, dangChay } = useThaoTac()
  const [ma, setMa] = useState('')
  const [kh, setKh] = useState(null)

  useEffect(() => {
    if (!ma && ds?.length) setMa(String(ds[0].maChuyen))
  }, [ds, ma])

  useEffect(() => {
    if (!ma) return
    let huy = false
    setKh(null)
    goiApi('/api/ke-hoach-xep-do/' + ma)
      .then((kq) => !huy && setKh(kq && kq.maChuyen ? kq : null))
      .catch(() => !huy && setKh(null))
    return () => {
      huy = true
    }
  }, [ma])

  async function lap() {
    const kq = await chay(() => goiApi('/api/ke-hoach-xep-do/' + ma + '/lap', { method: 'POST' }), { imLang: true })
    if (kq) {
      setKh(kq)
      taiLai({ im: true })
    }
  }

  const tangTren = kh ? kh.hinhHoc.soTang : 0
  const theoBay = {}
  for (const x of kh?.xep ?? []) (theoBay[x.bay] ??= []).push(x)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung tieuDe="Chọn chuyến tàu" icon="bi-water" khongDem>
        <div className="d-flex flex-wrap gap-2 align-items-center">
          <select className="form-select" style={{ maxWidth: 520 }} aria-label="Chuyến tàu" value={ma} onChange={(e) => setMa(e.target.value)}>
            {(ds ?? []).map((c) => (
              <option key={c.maChuyen} value={c.maChuyen}>
                {c.maChuyenHieu} · {c.tenTau} · {c.tenBen || 'chưa có bến'} · {c.contNhap}/{c.contXuat} cont{c.daLap ? ' · đã lập' : ''}
              </option>
            ))}
          </select>
          <button type="button" className="btn btn-primary" disabled={dangChay || !ma} onClick={lap}>
            <i className="bi bi-magic me-1" />
            {kh ? 'Lập lại kế hoạch' : 'Lập kế hoạch'}
          </button>
        </div>
        {(ds ?? []).length === 0 && <div className="small text-body-secondary mt-2">Không có chuyến nào đang hoặc sắp làm hàng.</div>}
      </TheNoiDung>

      {!kh && ma && <div className="alert alert-secondary">Chuyến này chưa có kế hoạch. Bấm “Lập kế hoạch” để hệ thống xếp ô và chia việc cho cẩu.</div>}

      {kh && (
        <>
          <div className="row g-3 mb-3">
            <div className="col-6 col-lg-3"><TheChiSo icon="bi-arrow-left-right" mau="xanh" so={kh.tongMove} nhan="lượt cẩu (dỡ + xếp)" /></div>
            <div className="col-6 col-lg-3"><TheChiSo icon="bi-hourglass" mau="cam" so={kh.gioDuKien + ' giờ'} nhan={'gồm 1 giờ chuẩn bị · ' + kh.moveMoiGioMoiCau + ' move/giờ/cẩu'} /></div>
            <div className="col-6 col-lg-3"><TheChiSo icon="bi-flag" mau={kh.kipEtd ? 'la' : 'do'} so={gioNgay(kh.xongLuc)} nhan={(kh.kipEtd ? 'kịp' : 'trễ') + ' ETD ' + gioNgay(kh.etd)} /></div>
            <div className="col-6 col-lg-3"><TheChiSo icon="bi-box-seam" mau="la" so={kh.xep.length} nhan={'container xuất đã xếp ô · ' + kh.canDoi.tongTan + ' tấn'} /></div>
          </div>

          {kh.canhBao.map((c) => <div key={c} className="alert alert-warning py-2">{c}</div>)}

          <div className="row g-3">
            <div className="col-12 col-xl-5">
              <TheNoiDung tieuDe="Chia việc cho cẩu bờ" icon="bi-diagram-2" khongDem>
                <table className="table table-sm mb-2">
                  <thead><tr><th>Cẩu</th><th>Đoạn bay</th><th>Lượt</th><th>Giờ</th></tr></thead>
                  <tbody>
                    {kh.cau.map((c) => (
                      <tr key={c.cau}><td className="fw-semibold">{c.cau}</td><td>{c.tuBay}–{c.denBay}</td><td>{c.move}</td><td>{c.gio}</td></tr>
                    ))}
                  </tbody>
                </table>
                <p className="small text-body-secondary mb-0">Mỗi cẩu nhận một đoạn bay liền nhau vì các cẩu chạy chung ray, không vượt qua nhau. Quy hoạch động chọn cách chia để cẩu nặng việc nhất là nhẹ nhất.</p>
              </TheNoiDung>
              <TheNoiDung tieuDe="Chỉ số cân bằng" icon="bi-speedometer" khongDem>
                <div className="small">Lệch dọc: <b>{kh.canDoi.lechDocBay}</b> bay · Lệch ngang: <b>{kh.canDoi.lechNgangHang}</b> hàng · Trọng tâm theo tầng: <b>{kh.canDoi.tangTrongTam}</b> / {kh.hinhHoc.soTang}</div>
                <div className="small text-body-secondary mt-1">Mô hình đơn giản hóa để điều độ tham khảo, không thay thế phần mềm tính ổn định của tàu.</div>
              </TheNoiDung>
            </div>
            <div className="col-12 col-xl-7">
              <TheNoiDung tieuDe={'Sơ đồ xếp · ' + kh.tenTau + ' (' + kh.hinhHoc.soBay + ' bay × ' + kh.hinhHoc.soHang + ' hàng × ' + kh.hinhHoc.soTang + ' tầng)'} icon="bi-grid-3x3" khongDem>
                {Object.keys(theoBay).length === 0 && <div className="small text-body-secondary">Chuyến này chưa có container xuất gắn trong hệ thống; phần chia cẩu tính theo số lượt khai báo của chuyến.</div>}
                {Object.keys(theoBay).sort((a, b) => a - b).map((b) => (
                  <div key={b} className="mb-3">
                    <div className="small fw-semibold mb-1">Bay {b}</div>
                    <div className="d-grid gap-1" style={{ gridTemplateColumns: `repeat(${kh.hinhHoc.soHang}, minmax(16px, 1fr))` }}>
                      {Array.from({ length: tangTren }, (_, i) => tangTren - i).flatMap((t) =>
                        Array.from({ length: kh.hinhHoc.soHang }, (_, h) => {
                          const x = theoBay[b].find((c) => c.tang === t && c.hang === h + 1)
                          return (
                            <div key={t + '-' + h} title={x ? `${x.soCont} · ${x.loaiCont} · ${x.trongLuong} tấn · hàng ${x.hang} tầng ${x.tang}` : ''}
                              style={{ height: 18, borderRadius: 2, background: x ? (x.lanh ? '#0dcaf0' : x.trongLuong >= 20 ? '#dc3545' : '#fd7e14') : 'var(--bs-tertiary-bg)' }} />
                          )
                        }),
                      )}
                    </div>
                  </div>
                ))}
                <div className="small text-body-secondary">Đỏ: từ 20 tấn · Cam: dưới 20 tấn · Xanh: container lạnh. Rê chuột vào ô để xem số container.</div>
              </TheNoiDung>
            </div>
          </div>
        </>
      )}
    </>
  )
}
