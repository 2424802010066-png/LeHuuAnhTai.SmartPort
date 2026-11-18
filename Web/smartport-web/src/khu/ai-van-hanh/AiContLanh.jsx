import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Line } from 'react-chartjs-2'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import { CachTinh, OChiSo, ThanhCongCu, ThanhDiem } from './chung'
import './ai-van-hanh.css'

// Nguong lech so voi nhiet dat (°C, duong = am hon) — trung voi AiContLanhService
const NGUONG_CANH_BAO = 3
const NGUONG_NGUY_CAP = 6
/** Cam bien IoT gui so do 30 phut/lan — lam moi moi phut la du "thoi gian thuc" */
const NHIP_LAM_MOI = 60000

const MAU_TT = {
  'on-dinh': 'ok',
  'vot-tam-thoi': 'info',
  'am-dan': 'warn',
  'cam-bien-ket': 'warn',
  'lech-keo-dai': 'do',
  'nguy-cap': 'do',
  'mat-tin-hieu': 'do',
  'chua-du-lieu': 'off',
}

const mauRuiRo = (r) => (r >= 80 ? 'do' : r >= 60 ? 'warn' : r >= 35 ? 'info' : 'ok')
const coDau = (v, le = 1) => (v == null ? '—' : (v > 0 ? '+' : '') + dinhDangSo(v, le))

const COT = [
  {
    khoa: 'soCont',
    ten: 'Container',
    hien: (c) => (
      <>
        <div className="fw-semibold font-monospace">
          {c.soCont} {c.dienTap && <span className="av-nhan-dien-tap">DIỄN TẬP</span>}
        </div>
        <div className="small text-body-secondary">
          {c.loaiCont} · {c.viTriBai || '—'}
        </div>
      </>
    ),
  },
  { khoa: 'hang', ten: 'Hàng · khách', an: 'lg', lop: 'small', hien: (c) => [c.hangHoa, c.khachHang].filter(Boolean).join(' · ') || '—' },
  { khoa: 'dat', ten: 'Cài đặt', canPhai: true, hien: (c) => dinhDangSo(c.nhietDoCaiDat, 1) + '°C' },
  {
    khoa: 'lech',
    ten: 'Lệch hiện tại',
    canPhai: true,
    hien: (c) => (c.ai.mucHienTai == null ? '—' : <b className={c.ai.mucHienTai >= NGUONG_CANH_BAO ? 'text-danger' : ''}>{coDau(c.ai.mucHienTai)}°C</b>),
  },
  {
    khoa: 'doc',
    ten: 'Xu hướng',
    canPhai: true,
    an: 'md',
    hien: (c) =>
      c.ai.doDocMoiGio == null ? (
        '—'
      ) : (
        <span className={c.ai.doDocMoiGio >= 0.25 ? 'text-danger fw-semibold' : ''}>
          <i className={'bi me-1 ' + (c.ai.doDocMoiGio > 0.05 ? 'bi-arrow-up-right' : c.ai.doDocMoiGio < -0.05 ? 'bi-arrow-down-right' : 'bi-arrow-right')} />
          {coDau(c.ai.doDocMoiGio, 2)}°C/giờ
        </span>
      ),
  },
  { khoa: 'tt', ten: 'AI đánh giá', hien: (c) => <NhanTrangThai giaTri={c.ai.tenTrangThai} mau={MAU_TT[c.ai.trangThai] || 'info'} /> },
  {
    khoa: 'rui',
    ten: 'Rủi ro',
    rong: '7rem',
    hien: (c) => (
      <div className="d-flex align-items-center gap-2">
        <ThanhDiem giaTri={c.ai.diemRuiRo} mau={mauRuiRo(c.ai.diemRuiRo)} />
        <span className="small fw-semibold">{c.ai.diemRuiRo}</span>
      </div>
    ),
  },
]

/** Bieu do nhiet 24 gio cua mot container + duong nhiet dat, nguong canh bao, nguong nguy cap */
function BieuDoNhiet({ c }) {
  const duLieu = useMemo(() => {
    const ds = c.diem24Gio
    const hai = (n) => String(n).padStart(2, '0')
    const ngang = (v) => ds.map(() => v)
    return {
      labels: ds.map((p) => {
        const t = new Date(p.luc)
        return hai(t.getHours()) + ':' + hai(t.getMinutes())
      }),
      datasets: [
        {
          label: 'Nhiệt độ đo',
          data: ds.map((p) => p.nhietDo),
          borderColor: '#0284c7',
          backgroundColor: 'rgba(2, 132, 199, 0.12)',
          borderWidth: 2,
          pointRadius: 1.5,
          tension: 0.25,
          fill: false,
        },
        { label: 'Nhiệt đặt', data: ngang(c.nhietDoCaiDat), borderColor: '#16a34a', borderDash: [6, 4], borderWidth: 1.5, pointRadius: 0 },
        {
          label: `Cảnh báo (+${NGUONG_CANH_BAO}°C)`,
          data: ngang(c.nhietDoCaiDat + NGUONG_CANH_BAO),
          borderColor: '#d97706',
          borderDash: [4, 4],
          borderWidth: 1.5,
          pointRadius: 0,
        },
        {
          label: `Nguy cấp (+${NGUONG_NGUY_CAP}°C)`,
          data: ngang(c.nhietDoCaiDat + NGUONG_NGUY_CAP),
          borderColor: '#dc2626',
          borderDash: [4, 4],
          borderWidth: 1.5,
          pointRadius: 0,
        },
      ],
    }
  }, [c])

  const tuyChon = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { labels: { boxWidth: 12 } },
      tooltip: { callbacks: { label: (x) => `${x.dataset.label}: ${dinhDangSo(x.parsed.y, 1)}°C` } },
    },
    scales: { x: { ticks: { autoSkip: true, maxRotation: 0 } }, y: { title: { display: true, text: '°C' } } },
  }

  if (!c.diem24Gio.length) return <div className="small text-body-secondary py-4 text-center">Chưa có số đo trong 24 giờ.</div>
  return (
    <div className="av-bieu-do">
      <Line data={duLieu} options={tuyChon} />
    </div>
  )
}

/**
 * AI CONTAINER LANH: canh bao SOM (may nen yeu dan) va LOC bao dong gia (mo cua kiem hang, xa da).
 * So lieu: GET /api/ai-van-hanh/cont-lanh (?dienTap=true: mo phong may nen hong dan tren 1 container, khong ghi CSDL).
 */
export default function AiContLanh() {
  const [loc, datLoc] = useBoLoc({ dienTap: false })
  const [sanSang, setSanSang] = useState(false)
  const [chon, setChon] = useState(null)

  // GET /api/container-lanh "bat kip" so doc cam bien IoT toi hien tai (giong trang giam sat lanh) — goi truoc khi phan tich
  const batKipCamBien = useCallback(async () => {
    try {
      await goiApi('/api/container-lanh')
    } catch {
      /* van phan tich tren so do da co */
    }
  }, [])

  useEffect(() => {
    let huy = false
    batKipCamBien().finally(() => {
      if (!huy) setSanSang(true)
    })
    return () => {
      huy = true
    }
  }, [batKipCamBien])

  const { duLieu: d, loi, dangTai, taiLai } = useApi(sanSang ? '/api/ai-van-hanh/cont-lanh' + taoQuery({ dienTap: loc.dienTap || undefined }) : null)

  const lamMoi = useCallback(
    async (tuyChon) => {
      await batKipCamBien()
      await taiLai(tuyChon)
    },
    [batKipCamBien, taiLai],
  )

  useEffect(() => {
    const hen = setInterval(() => lamMoi({ im: true }), NHIP_LAM_MOI)
    return () => clearInterval(hen)
  }, [lamMoi])

  if (!sanSang || (dangTai && !d)) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const c = d.cont.find((x) => x.maCont === chon) || d.cont[0]
  const ai = c?.ai

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <ThanhCongCu luc={dinhDangNgayGio(d.lucTinh) + ' · tự làm mới mỗi phút'} dangTai={dangTai} lamMoi={lamMoi}>
        <div className="form-check form-switch mb-0 small">
          <input
            className="form-check-input"
            type="checkbox"
            role="switch"
            id="av-dien-tap"
            checked={loc.dienTap}
            onChange={(e) => datLoc({ dienTap: e.target.checked })}
          />
          <label className="form-check-label" htmlFor="av-dien-tap">
            Diễn tập máy nén yếu dần
          </label>
        </div>
      </ThanhCongCu>

      {d.dienTap && (
        <div className="alert alert-warning py-2 small d-flex gap-2 align-items-center">
          <i className="bi bi-cone-striped fs-5" />
          <div>
            <b>Đang diễn tập:</b> container có nhiệt ổn định nhất được cộng nhiệt tăng dần 0,3–0,4°C/giờ trong 6 giờ cuối (máy nén yếu dần) — xem AI báo
            sớm trước khi chạm ngưỡng. Chỉ tính trên màn hình, không ghi vào dữ liệu thật.
          </div>
        </div>
      )}

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OChiSo mau="ngoc" so={d.soCont} donVi="cont" nhan="Container lạnh đang giám sát" phu="cảm biến gửi 30 phút/lần" icon="bi-thermometer-snow" />
        </div>
        <div className="col">
          <OChiSo mau={d.soCanXuLy ? 'do' : 'la'} so={d.soCanXuLy} donVi="cont" nhan="Cần xử lý (rủi ro ≥ 60)" phu="xếp theo mức rủi ro bên dưới" icon="bi-exclamation-octagon" />
        </div>
        <div className="col">
          <OChiSo mau={d.soAmDan ? 'cam' : 'la'} so={d.soAmDan} donVi="cont" nhan="Đang ấm dần" phu="báo trước khi chạm ngưỡng" icon="bi-graph-up-arrow" />
        </div>
        <div className="col">
          <OChiSo
            mau="tim"
            so={d.baoDongGiaLocBo}
            donVi="lần"
            nhan="Báo động giả đã lọc (24 giờ)"
            phu={`cách cũ ${d.baoDongCachCu24h} lần → AI giữ ${d.baoDongAi24h}`}
            icon="bi-funnel"
          />
        </div>
      </div>

      <div className="small text-body-secondary mb-3">{d.thongDiep}</div>

      <div className="row g-3">
        <div className="col-xl-7">
          <TheNoiDung
            tieuDe="Container lạnh theo mức rủi ro"
            icon="bi-list-ol"
            khongDem
            chan={
              <span className="small">
                <Link to="/admin/reefer">
                  <i className="bi bi-thermometer-half me-1" />
                  Mở bảng giám sát lạnh (ghi nhận đo tay)
                </Link>
              </span>
            }
          >
            <BangDuLieu
              cot={COT}
              dong={d.cont}
              khoaDong="maCont"
              rong="Không có container lạnh nào trong bãi."
              bamDong={(x) => setChon(x.maCont)}
              lopDong={(x) => (c && x.maCont === c.maCont ? 'av-dong-chon' : '')}
              cuon="32rem"
            />
          </TheNoiDung>
        </div>

        <div className="col-xl-5">
          {c ? (
            <TheNoiDung tieuDe={'Nhiệt độ 24 giờ · ' + c.soCont} icon="bi-activity">
              <BieuDoNhiet c={c} />
              <div className={'av-khuyen-nghi mt-3 ' + (MAU_TT[ai.trangThai] === 'info' ? '' : MAU_TT[ai.trangThai])}>
                <div className="fw-semibold mb-1">
                  <NhanTrangThai giaTri={ai.tenTrangThai} mau={MAU_TT[ai.trangThai] || 'info'} /> <span className="ms-1">Rủi ro {ai.diemRuiRo}/100</span>
                </div>
                <div className="small">{ai.khuyenNghi}</div>
              </div>
              <div className="row row-cols-2 g-2 small mt-2">
                <div className="col">
                  Lệch hiện tại (trung vị 3 lần đo): <b>{coDau(ai.mucHienTai)}°C</b>
                </div>
                <div className="col">
                  Lần đo cuối: <b>{coDau(ai.lanDoCuoi)}°C</b> · {ai.phutTuLanDoCuoi} phút trước
                </div>
                <div className="col">
                  Xu hướng 6 giờ: <b>{coDau(ai.doDocMoiGio, 2)}°C/giờ</b>
                  {ai.tyLeTang != null && <span className="text-body-secondary"> · {dinhDangSo(ai.tyLeTang * 100, 0)}% cặp điểm tăng</span>}
                </div>
                <div className="col">
                  Chạm ngưỡng cảnh báo:{' '}
                  <b className={ai.gioDenCanhBao != null ? 'text-danger' : ''}>{ai.gioDenCanhBao != null ? '~' + dinhDangSo(ai.gioDenCanhBao, 1) + ' giờ nữa' : 'không có xu hướng'}</b>
                  {ai.gioDenNguyCap != null && <span className="text-body-secondary"> · nguy cấp ~{dinhDangSo(ai.gioDenNguyCap, 1)} giờ</span>}
                </div>
                <div className="col">
                  Vọt tạm thời 24 giờ: <b>{ai.soVotTamThoi24h}</b> lần
                </div>
                <div className="col">
                  Báo động 24 giờ: cách cũ <b>{ai.baoDongCachCu24h}</b> · AI <b>{ai.baoDongAi24h}</b>
                </div>
              </div>
            </TheNoiDung>
          ) : (
            <TheNoiDung tieuDe="Nhiệt độ 24 giờ" icon="bi-activity">
              <div className="small text-body-secondary">Không có container lạnh nào trong bãi.</div>
            </TheNoiDung>
          )}
        </div>
      </div>

      <CachTinh>
        <p className="mb-1">
          <b>Vấn đề của cách cũ</b> (báo động khi lần đo cuối lệch ≥ 3°C): <b>báo giả</b> khi mở cửa kiểm hàng / xả đá (nhiệt vọt lên rồi tự về) và{' '}
          <b>báo muộn</b> khi máy nén yếu dần (tăng 0,3–0,5°C/giờ, lúc chạm ngưỡng thì hàng đã hỏng).
        </p>
        <p className="mb-1">
          <b>AI dùng thống kê bền vững</b> (chịu được điểm nhiễu):
        </p>
        <div className="av-cong-thuc">
          {'Mức hiện tại  = trung vị 3 lần đo cuối            ← 1 lần vọt đơn lẻ không kéo lệch\n' +
            'Xu hướng      = độ dốc Theil–Sen 6 giờ gần nhất   = trung vị các (yⱼ − yᵢ)/(xⱼ − xᵢ)\n' +
            'Giờ đến ngưỡng = (3°C − mức hiện tại) / độ dốc\n' +
            'Vọt tạm thời  = điểm ≥ 3°C nhưng hai điểm kề bên bình thường'}
        </div>
        <p className="mb-1">
          Thứ tự đánh giá: mất tín hiệu (&gt; 90 phút không có số đo) → cảm biến kẹt (6 giờ dao động dưới 0,05°C) → nguy cấp (mức hiện tại ≥ 6°C)
          → lệch kéo dài (mức hiện tại ≥ 3°C) → đang ấm dần (dốc ≥ 0,25°C/giờ và ≥ 65% cặp điểm tăng) → vọt tạm thời → ổn định.
        </p>
        <p className="mb-0">
          <b>Kiểm chứng:</b> trên 24 giờ gần nhất, đếm số lần cách cũ báo động và số lần AI giữ lại — phần chênh lệch là báo động giả đã lọc (thợ lạnh
          không phải chạy oan).
        </p>
      </CachTinh>
    </>
  )
}
