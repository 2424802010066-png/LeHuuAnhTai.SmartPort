import { BarController, Chart as ChartJS, LineController } from 'chart.js'
import { useMemo } from 'react'
import { Bar, Chart } from 'react-chartjs-2'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import { CachTinh, OChiSo, ThanhCongCu } from './chung'
import './ai-van-hanh.css'

// Bieu do ket hop cot + duong can dang ky controller cua ca hai loai
ChartJS.register(BarController, LineController)

const LOP_MUC = { 'Thông thoáng': 'thong-thoang', 'Bình thường': 'binh-thuong', Đông: 'dong', 'Ùn tắc': 'un-tac' }
const MAU_MUC = { 'Thông thoáng': '#16a34a', 'Bình thường': '#0284c7', Đông: '#d97706', 'Ùn tắc': '#dc2626' }
const THU = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
const hai = (n) => String(n).padStart(2, '0')

const PHUT_MOI_XE = [
  { v: 0.5, t: '0,5 phút — cổng tự động (ALPR + ePass)' },
  { v: 1, t: '1 phút' },
  { v: 2, t: '2 phút' },
  { v: 3, t: '3 phút — cổng thủ công' },
  { v: 5, t: '5 phút — kiểm hóa' },
]

const phutCho = (p) => (p >= 999 ? 'quá tải' : dinhDangSo(p, 1) + ' phút')

const COT_NGAY = [
  {
    khoa: 'ngay',
    ten: 'Ngày',
    hien: (n) => {
      const d = new Date(n.ngay)
      return (
        <span className="fw-semibold">
          {n.thu} {hai(d.getDate())}/{hai(d.getMonth() + 1)}
        </span>
      )
    },
  },
  { khoa: 'tong', ten: 'Tổng lượt xe', canPhai: true, hien: (n) => dinhDangSo(n.tongXe, 0) },
  {
    khoa: 'dinh',
    ten: 'Giờ cao điểm',
    canPhai: true,
    hien: (n) => (
      <>
        {hai(n.gioCaoDiem)}:00 <span className="small text-body-secondary">(~{dinhDangSo(n.xeGioCaoDiem, 1)} xe)</span>
      </>
    ),
  },
  {
    khoa: 'lan',
    ten: 'Làn cần mở',
    canPhai: true,
    hien: (n) => <NhanTrangThai mau={n.soLanToiDaCan >= 3 ? 'do' : n.soLanToiDaCan === 2 ? 'warn' : 'ok'}>{n.soLanToiDaCan} làn</NhanTrangThai>,
  },
  { khoa: 'hen', ten: 'Lệnh hẹn', canPhai: true, an: 'md', hien: (n) => dinhDangSo(n.soLenhHen, 0) },
]

/** Luoi ngay x gio: mau theo muc do — nguoi dieu do nhin mot phat biet ca tuan dong luc nao */
function LuoiGio({ gio, ngay }) {
  const theoNgay = useMemo(() => {
    const m = new Map()
    for (const o of gio) {
      const d = new Date(o.luc)
      const k = d.toDateString()
      if (!m.has(k)) m.set(k, {})
      m.get(k)[d.getHours()] = o
    }
    return m
  }, [gio])

  return (
    <div className="table-responsive">
      <div className="av-luoi-gio">
        <div />
        {Array.from({ length: 24 }, (_, h) => (
          <div key={h} className="av-nhan-cot">
            {h}
          </div>
        ))}
        {ngay.map((n) => {
          const d = new Date(n.ngay)
          const o24 = theoNgay.get(d.toDateString()) || {}
          return [
            <div key={'n' + n.ngay} className="av-nhan-hang">
              {n.thu} {hai(d.getDate())}/{hai(d.getMonth() + 1)}
            </div>,
            ...Array.from({ length: 24 }, (_, h) => {
              const o = o24[h]
              if (!o) return <div key={n.ngay + '-' + h} className="av-o-gio trong" />
              return (
                <div
                  key={n.ngay + '-' + h}
                  className={'av-o-gio ' + (LOP_MUC[o.mucDo] || 'trong')}
                  title={`${hai(h)}:00 · ~${dinhDangSo(o.soXeDuKien, 1)} xe (80%: ${dinhDangSo(o.canDuoi, 1)}–${dinhDangSo(o.canTren, 1)}) · ${o.soLanCan} làn · chờ ${phutCho(o.phutChoDuKien)}${o.soLenhHen > 0 ? ' · ' + dinhDangSo(o.soLenhHen, 1) + ' lượt theo lệnh hẹn' : ''}`}
                >
                  {o.soXeDuKien >= 0.5 ? Math.round(o.soXeDuKien) : ''}
                </div>
              )
            }),
          ]
        })}
      </div>
    </div>
  )
}

/**
 * AI CONG: du bao luot xe tung gio (thu x gio + lenh hen), so lan can mo theo hang doi M/M/c, khung gio nen hen,
 * thoi gian quay vong xe (TTT) va ty le den dung hen. So lieu: GET /api/ai-van-hanh/cong.
 */
export default function AiCong() {
  const [loc, datLoc] = useBoLoc({ phutMoiXe: 3, soLanToiDa: 4, choToiDa: 10, soNgay: 7 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/ai-van-hanh/cong' + taoQuery(loc))

  const bieuDo48 = useMemo(() => {
    if (!d?.gio?.length) return null
    const g = d.gio.slice(0, 48)
    return {
      labels: g.map((o) => {
        const t = new Date(o.luc)
        return t.getHours() === 0 ? `${hai(t.getDate())}/${hai(t.getMonth() + 1)}` : hai(t.getHours()) + 'h'
      }),
      datasets: [
        { type: 'line', label: 'Cận dưới', data: g.map((o) => o.canDuoi), borderWidth: 0, pointRadius: 0, fill: false, order: 3 },
        {
          type: 'line',
          label: 'Khoảng tin cậy 80%',
          data: g.map((o) => o.canTren),
          borderWidth: 0,
          pointRadius: 0,
          backgroundColor: 'rgba(2, 132, 199, 0.13)',
          fill: '-1',
          order: 3,
        },
        {
          type: 'bar',
          label: 'Lượt xe dự kiến',
          data: g.map((o) => o.soXeDuKien),
          backgroundColor: g.map((o) => MAU_MUC[o.mucDo] || '#0284c7'),
          borderRadius: 3,
          order: 2,
        },
        {
          type: 'line',
          label: 'Số làn cần mở',
          data: g.map((o) => o.soLanCan),
          borderColor: '#7c3aed',
          backgroundColor: '#7c3aed',
          borderWidth: 2,
          stepped: true,
          pointRadius: 0,
          yAxisID: 'y1',
          order: 1,
        },
      ],
    }
  }, [d])

  const tuyChon48 = useMemo(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: { labels: { filter: (i) => i.text !== 'Cận dưới', boxWidth: 12 } },
        tooltip: {
          filter: (i) => i.dataset.label !== 'Cận dưới',
          callbacks: {
            label: (c) =>
              c.dataset.label === 'Số làn cần mở'
                ? `Cần mở ${c.parsed.y} làn`
                : c.dataset.label === 'Khoảng tin cậy 80%'
                  ? `Cận trên 80%: ${dinhDangSo(c.parsed.y, 1)} xe`
                  : `${dinhDangSo(c.parsed.y, 1)} xe · ${d?.gio[c.dataIndex]?.mucDo ?? ''} · chờ ${phutCho(d?.gio[c.dataIndex]?.phutChoDuKien ?? 0)}`,
          },
        },
      },
      scales: {
        x: { ticks: { autoSkip: true, maxRotation: 0 } },
        y: { beginAtZero: true, title: { display: true, text: 'xe / giờ' } },
        y1: { position: 'right', beginAtZero: true, suggestedMax: 3, ticks: { precision: 0 }, grid: { drawOnChartArea: false }, title: { display: true, text: 'làn' } },
      },
    }),
    [d],
  )

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const g24 = d.gio.slice(0, 24)
  const dinh24 = g24.reduce((m, o) => (!m || o.soXeDuKien > m.soXeDuKien ? o : m), null)
  const tong24 = g24.reduce((s, o) => s + o.soXeDuKien, 0)
  const luDinh = dinh24 ? new Date(dinh24.luc) : null

  const bieuDoThu = {
    labels: THU,
    datasets: [{ data: d.heSoTheoThu, backgroundColor: d.heSoTheoThu.map((v) => (v >= 1 ? '#ea580c' : '#0284c7')), borderRadius: 4 }],
  }
  const bieuDoGio = {
    labels: Array.from({ length: 24 }, (_, h) => h + 'h'),
    datasets: [{ data: d.phanBoTheoGio.map((v) => Math.round(v * 1000) / 10), backgroundColor: '#6366f1', borderRadius: 3 }],
  }
  const tuyChonNho = (donVi) => ({
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false }, tooltip: { callbacks: { label: (c) => dinhDangSo(c.parsed.y, donVi === '%' ? 1 : 2) + donVi } } },
    scales: { y: { beginAtZero: true }, x: { ticks: { autoSkip: true, maxRotation: 0 } } },
  })

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <ThanhCongCu luc={dinhDangNgayGio(d.lucTinh)} dangTai={dangTai} lamMoi={taiLai}>
        <select
          className="form-select form-select-sm w-auto"
          aria-label="Thời gian xử lý mỗi xe"
          value={loc.phutMoiXe}
          onChange={(e) => datLoc({ phutMoiXe: Number(e.target.value) })}
        >
          {PHUT_MOI_XE.map((x) => (
            <option key={x.v} value={x.v}>
              {x.t}
            </option>
          ))}
        </select>
        <select className="form-select form-select-sm w-auto" aria-label="Số làn tối đa" value={loc.soLanToiDa} onChange={(e) => datLoc({ soLanToiDa: Number(e.target.value) })}>
          {[1, 2, 3, 4, 5, 6, 8].map((x) => (
            <option key={x} value={x}>
              Tối đa {x} làn
            </option>
          ))}
        </select>
        <select className="form-select form-select-sm w-auto" aria-label="Thời gian chờ mục tiêu" value={loc.choToiDa} onChange={(e) => datLoc({ choToiDa: Number(e.target.value) })}>
          {[5, 10, 15, 20, 30].map((x) => (
            <option key={x} value={x}>
              Chờ ≤ {x} phút
            </option>
          ))}
        </select>
        <select className="form-select form-select-sm w-auto" aria-label="Số ngày dự báo" value={loc.soNgay} onChange={(e) => datLoc({ soNgay: Number(e.target.value) })}>
          {[1, 3, 7, 14].map((x) => (
            <option key={x} value={x}>
              {x} ngày tới
            </option>
          ))}
        </select>
      </ThanhCongCu>

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OChiSo so={dinhDangSo(tong24, 0)} donVi="lượt" nhan="Dự kiến 24 giờ tới" phu={`lịch sử TB ${dinhDangSo(d.xeMoiNgayTB, 1)} lượt/ngày`} icon="bi-truck" />
        </div>
        <div className="col">
          <OChiSo
            mau={dinh24?.mucDo === 'Ùn tắc' ? 'do' : dinh24?.soLanCan >= 2 ? 'cam' : 'la'}
            so={dinh24 ? dinhDangSo(dinh24.soXeDuKien, 1) : '—'}
            donVi="xe/giờ"
            nhan="Cao điểm 24 giờ tới"
            phu={luDinh ? `${hai(luDinh.getHours())}:00 ${hai(luDinh.getDate())}/${hai(luDinh.getMonth() + 1)} · mở ${dinh24.soLanCan} làn · chờ ${phutCho(dinh24.phutChoDuKien)}` : ''}
            icon="bi-stoplights"
          />
        </div>
        <div className="col">
          <OChiSo
            mau="tim"
            so={d.quayVongTrungViPhut != null ? dinhDangSo(d.quayVongTrungViPhut, 0) : '—'}
            donVi="phút"
            nhan="Quay vòng xe (TTT, trung vị)"
            phu={d.quayVongP90Phut != null ? `P90 ${dinhDangSo(d.quayVongP90Phut, 0)} phút · ${d.soCapQuayVong} lượt vào–ra` : 'chưa có cặp vào–ra'}
            icon="bi-arrow-repeat"
          />
        </div>
        <div className="col">
          <OChiSo
            mau="ngoc"
            so={d.tyLeDungHenPct != null ? dinhDangSo(d.tyLeDungHenPct, 0) : '—'}
            donVi="%"
            nhan="Tài xế đến đúng hẹn (±15 phút)"
            phu={`${d.soLenhCoVaoCong} lệnh đã vào cổng · tỉ lệ đến ${dinhDangSo(d.tyLeDenTheoHen * 100, 0)}%`}
            icon="bi-calendar-check"
          />
        </div>
      </div>

      <div className="card sp-the p-3 mb-3">
        <div className="av-chat-luong">
          <span className="fw-bold av-chu-tim">
            <i className="bi bi-clipboard-data me-1" />
            Độ chính xác mô hình
          </span>
          {d.maeNgayMoHinh != null ? (
            <>
              <span>
                Sai số mô hình <b className="av-chu-la">{dinhDangSo(d.maeNgayMoHinh, 1)}</b> lượt/ngày
              </span>
              <span>
                Lấy trung bình <b>{dinhDangSo(d.maeNgayCoSo, 1)}</b> lượt/ngày
              </span>
              <span className="small text-body-secondary">giữ lại {d.soNgayKiemDinh} ngày cuối để kiểm tra (mô hình không được thấy)</span>
            </>
          ) : (
            <span className="small text-body-secondary">Cần ≥ 21 ngày dữ liệu để kiểm định (hiện có {d.soNgayLichSu} ngày).</span>
          )}
        </div>
        <div className="small text-body-secondary mt-1">{d.thongDiep}</div>
      </div>

      <TheNoiDung tieuDe="48 giờ tới: lượt xe dự kiến và số làn cần mở" icon="bi-graph-up">
        {bieuDo48 && (
          <div className="av-bieu-do">
            <Chart type="bar" data={bieuDo48} options={tuyChon48} />
          </div>
        )}
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-xl-8">
          <TheNoiDung
            tieuDe={`Lịch đông cổng ${d.ngay.length} ngày tới`}
            icon="bi-calendar3"
            chan={
              <div className="small">
                {Object.entries(MAU_MUC).map(([ten, mau]) => (
                  <span className="av-chu-giai" key={ten}>
                    <span style={{ background: mau }} />
                    {ten}
                  </span>
                ))}
                <span className="text-body-secondary">· số trong ô = lượt xe dự kiến; rê chuột để xem số làn, thời gian chờ</span>
              </div>
            }
          >
            <LuoiGio gio={d.gio} ngay={d.ngay} />
          </TheNoiDung>
        </div>
        <div className="col-xl-4">
          <TheNoiDung tieuDe="Khung giờ nên hẹn tài xế" icon="bi-clock">
            {d.khungGioNenHen.length ? (
              <div className="d-flex flex-wrap gap-2">
                {d.khungGioNenHen.map((k) => {
                  const tu = new Date(k.tu)
                  const den = new Date(k.den)
                  return (
                    <div className="av-khung" key={k.tu}>
                      <b>
                        {hai(tu.getHours())}:00–{hai(den.getHours())}:00
                      </b>
                      <span className="small">
                        {hai(tu.getDate())}/{hai(tu.getMonth() + 1)} · ~{dinhDangSo(k.soXeDuKien, 1)} xe · {k.mucDo}
                      </span>
                    </div>
                  )
                })}
              </div>
            ) : (
              <div className="small text-body-secondary">Chưa có khung giờ phù hợp trong 3 ngày tới.</div>
            )}
            <div className="small text-body-secondary mt-2">
              Khung 2 giờ vắng nhất (6h–22h, 3 ngày tới). Tài xế xem công khai tại trang <b>Tình trạng cổng</b>.
            </div>
          </TheNoiDung>
        </div>
      </div>

      <TheNoiDung tieuDe="Dự báo theo ngày" icon="bi-list-ol" khongDem>
        <BangDuLieu cot={COT_NGAY} dong={d.ngay} khoaDong="ngay" />
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-md-6">
          <TheNoiDung tieuDe="Mô hình học được: hệ số theo thứ" icon="bi-calendar-week">
            <div className="av-bieu-do-nho">
              <Bar data={bieuDoThu} options={tuyChonNho('×')} />
            </div>
            <div className="small text-body-secondary mt-1">1,0 = ngày trung bình; &gt; 1 đông hơn (cam), &lt; 1 vắng hơn (xanh).</div>
          </TheNoiDung>
        </div>
        <div className="col-md-6">
          <TheNoiDung tieuDe="Mô hình học được: tỉ trọng theo giờ" icon="bi-clock-history">
            <div className="av-bieu-do-nho">
              <Bar data={bieuDoGio} options={tuyChonNho('%')} />
            </div>
            <div className="small text-body-secondary mt-1">Phần trăm lượt xe rơi vào từng giờ trong ngày.</div>
          </TheNoiDung>
        </div>
      </div>

      <CachTinh>
        <p className="mb-1">
          <b>1. Dự báo lượt xe mỗi giờ</b> — mô hình nhân (thứ × giờ) học từ lượt xe 90 ngày qua, cộng thêm lệnh giao nhận đã hẹn:
        </p>
        <div className="av-cong-thuc">
          {'λ(ngày, giờ) = mức TB/ngày × hệ số thứ × tỉ trọng giờ × xu hướng 14 ngày + lệnh hẹn × tỉ lệ đến\n' +
            'hệ số thứ = (lượt của thứ đó + 2·mức TB) / (số ngày thứ đó + 2) ÷ mức TB     ← co về 1 (Bayes) khi ít dữ liệu\n' +
            'tỉ trọng giờ = (lượt giờ h + 0,25) / (tổng lượt + 6)                          ← làm trơn Laplace'}
        </div>
        <p className="mb-1">
          Khoảng tin cậy 80% theo phân phối Poisson: λ ± 1,28·√λ. Mỗi lệnh hẹn tính 1 lượt vào lúc hẹn và 1 lượt ra sau thời gian quay vòng
          trung vị; tỉ lệ đến theo hẹn co về 85% khi ít lệnh.
        </p>
        <p className="mb-1">
          <b>2. Số làn cần mở</b> — lý thuyết hàng đợi M/M/c (công thức Erlang C): mỗi làn phục vụ μ = 60 / phút-mỗi-xe xe/giờ, chọn số làn{' '}
          <i>c</i> nhỏ nhất để thời gian chờ trung bình ≤ mục tiêu:
        </p>
        <div className="av-cong-thuc">
          {'ρ = λ / (c·μ) < 1,   P(chờ) = Erlang C(c, λ/μ),   Wq = P(chờ) / (c·μ − λ)'}
        </div>
        <p className="mb-1">
          Đổi ô &quot;phút mỗi xe&quot; sang <b>0,5 phút (cổng tự động)</b> để thấy cổng ALPR + ePass cần ít làn hơn bao nhiêu so với cổng thủ công
          — con số dùng để tính lợi ích đầu tư cổng tự động.
        </p>
        <p className="mb-0">
          <b>3. Kiểm định:</b> học trên dữ liệu trừ 7 ngày cuối, dự báo 7 ngày đó rồi so với thực tế (sai số tuyệt đối trung bình), đặt cạnh
          cách &quot;ngày nào cũng bằng trung bình&quot;.
        </p>
      </CachTinh>
    </>
  )
}
