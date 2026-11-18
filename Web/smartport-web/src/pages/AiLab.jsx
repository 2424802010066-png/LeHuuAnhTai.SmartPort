import { useMemo, useState } from 'react'
import { Line } from 'react-chartjs-2'
import { HORIZON, WIN, huanLuyenMlp, lopAn2, nhanTiepTheo, soThamSo } from '../ai/mlp'
import { useApi } from '../api/useApi'
import BaoLoi from '../components/BaoLoi'
import DangTai from '../components/DangTai'
import TheQHoc from '../khu/ai-thi-giac/TheQHoc'
import '../utils/dangKyBieuDo'
import { dinhDangSo } from '../utils/dinhDang'

const NGUON = [
  { ma: 'cuc-hang-hai', ten: 'Toàn quốc — Cục Hàng hải và Đường thủy VN (số liệu thật)' },
  { ma: 'cang', ten: 'Cảng SmartPort — bảng SanLuongThang trong CSDL' },
]

export default function AiLab() {
  const [nguon, setNguon] = useState('cuc-hang-hai')
  const { duLieu: chuoi, loi, dangTai, taiLai } = useApi('/api/ai-lab/san-luong?nguon=' + nguon)
  const [sieuThamSo, setSieuThamSo] = useState({ epochs: 200, lr: 0.01, hidden: 16 })
  const [dangHoc, setDangHoc] = useState(false)
  const [tienDo, setTienDo] = useState({ epoch: 0, losses: [], lan: 1 })
  const [ketQua, setKetQua] = useState(null)
  const [loiHoc, setLoiHoc] = useState('')

  function doiNguon(ma) {
    setNguon(ma)
    setKetQua(null)
    setTienDo({ epoch: 0, losses: [] })
  }

  async function huanLuyen() {
    setDangHoc(true)
    setLoiHoc('')
    setKetQua(null)
    setTienDo({ epoch: 0, losses: [] })
    try {
      const kq = await huanLuyenMlp({
        values: chuoi.giaTri,
        ...sieuThamSo,
        khiXongEpoch: (epoch, losses, lan) => setTienDo({ epoch, losses, lan }),
      })
      setKetQua({ ...kq, nguon: chuoi.nguon })
    } catch (e) {
      setLoiHoc(e.message)
    } finally {
      setDangHoc(false)
    }
  }

  // Du lieu bieu do chinh chi tinh lai khi co chuoi moi / ket qua moi — khong ve lai
  // bieu do sau moi epoch (tienDo doi lien tuc trong luc huan luyen)
  const { bieuDoChinh, nhanDuBao } = useMemo(() => {
    if (!chuoi) return { bieuDoChinh: null, nhanDuBao: [] }
    const v = chuoi.giaTri
    const nhan = nhanTiepTheo(chuoi.nhan[chuoi.nhan.length - 1], HORIZON)
    const kqHopLe = ketQua && ketQua.nguon === chuoi.nguon
    const dem = new Array(v.length - 1).fill(null)
    const duLieu = {
      labels: kqHopLe ? chuoi.nhan.concat(nhan) : chuoi.nhan,
      datasets: [
        { label: 'Thực tế', data: v, borderColor: '#0d4f8b', backgroundColor: 'rgba(13,79,139,.08)', fill: true, tension: 0.35, pointRadius: 2 },
        ...(kqHopLe
          ? [
              {
                label: 'Mạng nơ-ron MLP',
                data: dem.concat([v[v.length - 1]], ketQua.duBao),
                borderColor: '#8b5cf6',
                borderDash: [7, 5],
                tension: 0.35,
                pointRadius: 4,
                pointStyle: 'rectRot',
              },
              { label: 'Hồi quy tuyến tính', data: dem.concat([v[v.length - 1]], ketQua.tuyenTinh), borderColor: '#f97316', borderDash: [3, 4], tension: 0.35, pointRadius: 3 },
            ]
          : []),
      ],
    }
    return { bieuDoChinh: duLieu, nhanDuBao: nhan }
  }, [chuoi, ketQua])

  if (dangTai && !chuoi) return <DangTai />
  const donVi = chuoi?.donVi ?? 'TEU'
  const { epochs, lr, hidden } = sieuThamSo

  return (
    <>
      <div className="sp-hop-ai tim mb-3">
        <span className="sp-nhan-ai">
          <i className="bi bi-diagram-3 me-1" />
          AI LAB · HỌC SÂU TRÊN TRÌNH DUYỆT
        </span>
        <div className="d-flex flex-wrap align-items-center gap-3 mt-2">
          <div className="flex-grow-1">
            <div className="h5 mb-1 fw-bold">Huấn luyện mạng nơ-ron dự báo sản lượng container</div>
            <div className="sp-chu-nhat small">
              Kiến trúc {WIN} → {hidden} → {lopAn2(hidden)} → 1 · {soThamSo(hidden)} tham số · ReLU · Adam · hàm mất mát MSE
            </div>
          </div>
          <select className="form-select form-select-sm sp-chon-nguon" value={nguon} onChange={(e) => doiNguon(e.target.value)} disabled={dangHoc} aria-label="Nguồn số liệu">
            {NGUON.map((n) => (
              <option key={n.ma} value={n.ma}>
                {n.ten}
              </option>
            ))}
          </select>
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />

      {chuoi && (
        <div className="row g-3">
          <div className="col-lg-4">
            <div className="card sp-the mb-3">
              <div className="card-header">
                <i className="bi bi-sliders me-2" />
                Siêu tham số
              </div>
              <div className="card-body">
                <div className="small text-muted mb-3">
                  <i className="bi bi-database me-1" />
                  {chuoi.moTaNguon} · <b>{chuoi.soThang} tháng</b> ({chuoi.nhan[0]} → {chuoi.nhan[chuoi.nhan.length - 1]}) · đơn vị {donVi}
                </div>
                <label className="form-label small fw-semibold d-flex justify-content-between" htmlFor="epochs">
                  <span>Số epoch (vòng học)</span>
                  <span className="text-primary">{epochs}</span>
                </label>
                <input
                  id="epochs"
                  type="range"
                  className="form-range mb-3"
                  min={50}
                  max={500}
                  step={25}
                  value={epochs}
                  disabled={dangHoc}
                  onChange={(e) => setSieuThamSo({ ...sieuThamSo, epochs: +e.target.value })}
                />
                <div className="row g-2 mb-3">
                  <div className="col-6">
                    <label className="form-label small fw-semibold" htmlFor="lr">
                      Tốc độ học
                    </label>
                    <select id="lr" className="form-select form-select-sm" value={lr} disabled={dangHoc} onChange={(e) => setSieuThamSo({ ...sieuThamSo, lr: +e.target.value })}>
                      <option value={0.001}>0.001 (chậm, mịn)</option>
                      <option value={0.005}>0.005</option>
                      <option value={0.01}>0.01 (cân bằng)</option>
                      <option value={0.05}>0.05 (nhanh, dao động)</option>
                    </select>
                  </div>
                  <div className="col-6">
                    <label className="form-label small fw-semibold" htmlFor="hidden">
                      Nơ-ron lớp ẩn 1
                    </label>
                    <select id="hidden" className="form-select form-select-sm" value={hidden} disabled={dangHoc} onChange={(e) => setSieuThamSo({ ...sieuThamSo, hidden: +e.target.value })}>
                      <option value={8}>8 (mạng nhỏ)</option>
                      <option value={16}>16 (chuẩn)</option>
                      <option value={32}>32 (mạng lớn)</option>
                    </select>
                  </div>
                </div>
                <button className="btn btn-lg w-100 sp-nut-tim" onClick={huanLuyen} disabled={dangHoc || chuoi.giaTri.length < WIN + 1}>
                  {dangHoc ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-lightning-charge-fill me-1" />}
                  {dangHoc ? 'Đang huấn luyện…' : ketQua ? 'Huấn luyện lại' : 'Bắt đầu huấn luyện'}
                </button>
                <div className="progress mt-3" style={{ height: 8 }}>
                  <div className="progress-bar sp-thanh-tim" style={{ width: Math.round((tienDo.epoch / epochs) * 100) + '%' }} />
                </div>
                <div className="small text-muted mt-1">
                  {tienDo.epoch
                    ? `${tienDo.lan > 1 ? `Khởi tạo lại lần ${tienDo.lan} · ` : ''}Epoch ${tienDo.epoch}/${epochs} · loss (MSE) = ${tienDo.losses[tienDo.losses.length - 1].toFixed(5)}`
                    : 'Chưa huấn luyện — mọi tính toán chạy trên máy của bạn, không cần GPU server.'}
                </div>
                {loiHoc && <div className="alert alert-danger py-2 small mt-2 mb-0">{loiHoc}</div>}
                {ketQua && (
                  <div className="alert alert-success py-2 small mt-2 mb-0">
                    Xong <b>{epochs} epoch</b> trong <b>{(ketQua.ms / 1000).toFixed(1)} giây</b>. Sai số trung bình trên dữ liệu huấn luyện (MAE) ≈{' '}
                    <b>
                      {dinhDangSo(ketQua.mae)} {donVi}
                    </b>
                    . Tháng tới: <b>{dinhDangSo(ketQua.duBao[0])}</b> {donVi} ({ketQua.duBao[0] - chuoi.giaTri[chuoi.giaTri.length - 1] >= 0 ? '+' : ''}
                    {dinhDangSo(ketQua.duBao[0] - chuoi.giaTri[chuoi.giaTri.length - 1])} so với tháng gần nhất).
                    {ketQua.soLanKhoiTao > 1 && (
                      <div className="mt-1 text-body-secondary">
                        <i className="bi bi-arrow-repeat me-1" />
                        Đã khởi tạo lại trọng số {ketQua.soLanKhoiTao - 1} lần vì mạng bị kẹt (nơ-ron ReLU “chết”, chỉ đoán được một hằng số).
                      </div>
                    )}
                  </div>
                )}
                {ketQua?.biKet && (
                  <div className="alert alert-warning py-2 small mt-2 mb-0">
                    Sau {ketQua.soLanKhoiTao} lần khởi tạo mạng vẫn chỉ đoán một hằng số — hãy giảm tốc độ học (0.001 hoặc 0.005) rồi huấn luyện lại.
                  </div>
                )}
              </div>
            </div>
            <div className="card sp-the">
              <div className="card-header">
                <i className="bi bi-graph-down me-2" />
                Đường học (loss theo epoch)
              </div>
              <div className="card-body" style={{ height: 200 }}>
                <Line
                  data={{
                    labels: tienDo.losses.map((_, i) => i + 1),
                    datasets: [{ label: 'Loss (MSE)', data: tienDo.losses, borderColor: '#8b5cf6', backgroundColor: 'rgba(139,92,246,.12)', fill: true, tension: 0.3, pointRadius: 0 }],
                  }}
                  options={{ maintainAspectRatio: false, animation: false, plugins: { legend: { display: false } }, scales: { y: { type: 'logarithmic' }, x: { display: false } } }}
                />
              </div>
            </div>
          </div>

          <div className="col-lg-8">
            <div className="card sp-the mb-3">
              <div className="card-header d-flex flex-wrap align-items-center gap-2">
                <span className="flex-grow-1">
                  <i className="bi bi-graph-up-arrow me-2" />
                  Dự báo {HORIZON} tháng: mạng nơ-ron và hồi quy tuyến tính ({donVi})
                </span>
                {ketQua && (
                  <span className="badge text-bg-light">
                    MAE ±{dinhDangSo(ketQua.mae)} {donVi}
                  </span>
                )}
              </div>
              <div className="card-body" style={{ height: 360 }}>
                <Line data={bieuDoChinh} options={{ maintainAspectRatio: false, animation: false, plugins: { legend: { position: 'bottom' } }, scales: { x: { grid: { display: false } } } }} />
              </div>
            </div>
            {ketQua && ketQua.nguon === chuoi.nguon && (
              <div className="card sp-the">
                <div className="card-header">
                  <i className="bi bi-table me-2" />
                  Bảng so sánh dự báo
                </div>
                <div className="table-responsive">
                  <table className="table align-middle mb-0 sp-bang">
                    <thead>
                      <tr>
                        <th>Tháng</th>
                        <th className="text-end">Mạng nơ-ron</th>
                        <th className="text-end">Hồi quy tuyến tính</th>
                        <th className="text-end">Chênh lệch</th>
                      </tr>
                    </thead>
                    <tbody>
                      {nhanDuBao.map((nhan, i) => {
                        const chenh = ketQua.duBao[i] - ketQua.tuyenTinh[i]
                        return (
                          <tr key={nhan}>
                            <td className="fw-semibold">{nhan}</td>
                            <td className="text-end fw-bold sp-chu-tim">{dinhDangSo(ketQua.duBao[i])}</td>
                            <td className="text-end">{dinhDangSo(ketQua.tuyenTinh[i])}</td>
                            <td className={'text-end ' + (chenh >= 0 ? 'text-success' : 'text-danger')}>
                              {chenh >= 0 ? '+' : ''}
                              {dinhDangSo(chenh)}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Manh thu 3 cua Machine Learning ben canh co giam sat / khong giam sat: HOC TANG CUONG */}
      <TheQHoc />
    </>
  )
}
