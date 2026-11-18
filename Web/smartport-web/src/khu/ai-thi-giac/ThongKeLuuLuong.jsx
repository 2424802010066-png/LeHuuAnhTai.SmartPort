import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { taiFile } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo } from '../../utils/dinhDang'
import './ai-thi-giac.css'

const KY = [
  { ngay: 1, ten: 'Hôm nay' },
  { ngay: 7, ten: '7 ngày' },
  { ngay: 30, ten: '30 ngày' },
]

/* ============================================================
   DU BAO LUU LUONG — SELECTIVE SSM (lay cam hung tu Mamba, 2023)
   Mo hinh khong gian trang thai: h(t) = A_t * h(t-1) + B_t * x(t)
   voi cong CHON LOC delta = sigmoid(w*x+b) dieu bien A, B theo TUNG dau vao
   (diem khac biet cot loi cua Mamba so voi SSM co dien). Quet chuoi tuyen tinh O(n),
   khong can attention. Cong thuc va he so giu NGUYEN nhu ban MVC.
   ============================================================ */
const sigmoid = (z) => 1 / (1 + Math.exp(-z))

function duBaoSsm(chuoi, soBuoc = 6) {
  if (!chuoi?.length) return []
  const tb = chuoi.reduce((s, v) => s + v, 0) / chuoi.length || 1
  let h = 0
  const hs = []
  for (const v of chuoi) {
    const x = v / tb // chuan hoa
    const delta = sigmoid(1.6 * x - 0.8) // cong chon loc theo dau vao
    h = Math.pow(0.82, delta) * h + delta * 0.9 * x
    hs.push(h)
  }
  // He so doc C: khop trang thai an -> gia tri thuc (binh phuong toi thieu don gian)
  let tu = 0
  let mau = 0
  for (let t = 1; t < chuoi.length; t++) {
    tu += (hs[t - 1] * chuoi[t]) / tb
    mau += hs[t - 1] * hs[t - 1]
  }
  const C = mau > 1e-9 ? tu / mau : 1

  const duBao = []
  let hHienTai = h
  for (let k = 0; k < soBuoc; k++) {
    const xHat = Math.max(0, C * hHienTai)
    duBao.push(Math.round(xHat * tb))
    const d = sigmoid(1.6 * xHat - 0.8)
    hHienTai = Math.pow(0.82, d) * hHienTai + d * 0.9 * xHat
  }
  return duBao
}

const haiSo = (n) => (n < 10 ? '0' + n : String(n))

/** Thong ke luu luong cong tu Camera AI (thay Admin/Vision/Analytics). */
export default function ThongKeLuuLuong() {
  const [loc, datLoc] = useBoLoc({ ngay: 7 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/camera-ai/thong-ke?ngay=' + loc.ngay)
  const { chay, dangChay } = useThaoTac()

  const ssm = useMemo(() => duBaoSsm((d?.theoGio || []).map((g) => g.soXeTai)), [d])

  if (loi) return <BaoLoi loi={loi} thuLai={taiLai} />
  if (!d) return <DangTai />

  const maxGio = Math.max(1, ...d.theoGio.map((g) => g.soXeTai))
  const maxCoCau = Math.max(1, ...d.coCauDoiTuong.map((c) => c.soLuong))
  const maxNgay = Math.max(1, ...d.theoNgay.map((x) => x.soXeTai))
  const maxSsm = Math.max(1, ...ssm)
  const gioCuoi = new Date().getHours()
  const tongSsm = ssm.reduce((s, v) => s + v, 0)

  return (
    <>
      <div className="sp-hop-ai mb-3">
        <span className="sp-nhan-ai">
          <i className="bi bi-graph-up-arrow me-1" />
          THỐNG KÊ LƯU LƯỢNG CỔNG
        </span>
        <div className="h5 mb-0 fw-bold mt-2">Lưu lượng cổng theo khung giờ · {d.soNgay} ngày gần nhất</div>
      </div>

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <div className="btn-group btn-group-sm" role="group" aria-label="Khoảng thời gian">
          {KY.map((k) => (
            <button key={k.ngay} type="button" className={'btn ' + (loc.ngay === k.ngay ? 'btn-primary' : 'btn-outline-secondary')} onClick={() => datLoc({ ngay: k.ngay })}>
              {k.ten}
            </button>
          ))}
        </div>
        <div className="flex-grow-1" />
        <button
          type="button"
          className="btn btn-sm btn-warning"
          disabled={dangChay}
          onClick={() => chay(() => taiFile('/api/camera-ai/xuat-excel?ngay=' + loc.ngay, { tenMacDinh: 'BaoCao_LuuLuong_Camera_AI.xlsx' }), { thanhCong: 'Đã tải báo cáo Excel.' })}
        >
          <i className="bi bi-file-earmark-excel me-1" />
          Xuất báo cáo Excel
        </button>
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/vision/history">
          <i className="bi bi-clock-history me-1" />
          Nhật ký nhận diện
        </Link>
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/vision">
          <i className="bi bi-camera-video me-1" />
          Về Camera AI
        </Link>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-truck" mau="xanh" so={dinhDangSo(d.tongXeTai)} nhan="Lượt xe tải/đầu kéo" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-cpu" mau="tim" so={dinhDangSo(d.tongLuot)} nhan="Lượt AI quét" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-exclamation-triangle" mau="do" so={dinhDangSo(d.soUnTac)} nhan="Cảnh báo ùn tắc" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-patch-check" mau="la" so={d.tinCayTB + '%'} nhan={'Độ tin cậy TB · ' + d.tgXuLyTB + ' ms'} />
        </div>
      </div>

      <div className="card sp-the mb-3 border-start border-4 border-warning">
        <div className="card-body">
          <div className="fw-bold mb-1">
            <i className="bi bi-lightbulb-fill me-1 text-warning" />
            Khuyến nghị điều phối từ dữ liệu
          </div>
          <div>
            Khung giờ cao điểm <b className="text-warning">{d.gioCaoDiem}</b> ({dinhDangSo(d.xeCaoDiem)} lượt xe tải) — nên bố trí thêm nhân sự và mở thêm làn cổng trong khung này.{' '}
            {d.soUnTac > 0 ? (
              <>
                Đã ghi nhận <b className="text-danger">{d.soUnTac} lần ùn tắc</b> (từ 5 xe tải/khung hình) trong {d.soNgay} ngày qua.
              </>
            ) : (
              'Chưa ghi nhận tình trạng ùn tắc nghiêm trọng.'
            )}
          </div>
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-7">
          <TheNoiDung tieuDe="Lưu lượng xe tải theo khung giờ" icon="bi-bar-chart-line">
            <div className="tg-cot-gio">
              {d.theoGio.map((g) => {
                const cao = Math.round((g.soXeTai * 100) / maxGio)
                const dinh = g.soXeTai > 0 && g.soXeTai >= maxGio * 0.75
                return (
                  <div key={g.gio} className="tg-cot" title={`${haiSo(g.gio)}:00 — ${g.soXeTai} xe tải, ${g.soLuot} lượt quét`}>
                    <div className={'tg-cot-than' + (dinh ? ' dinh' : '')} style={{ height: Math.max(cao, g.soXeTai > 0 ? 4 : 1) + '%' }}>
                      {g.soXeTai > 0 && <span className="tg-cot-so">{g.soXeTai}</span>}
                    </div>
                    <div className="tg-cot-nhan">{g.gio}</div>
                  </div>
                )
              })}
            </div>
            <div className="text-center text-body-secondary small mt-2">Trục ngang: giờ trong ngày (0–23) · cột cam = khung giờ cao điểm</div>
          </TheNoiDung>

          <TheNoiDung
            tieuDe={
              <>
                Dự báo lưu lượng 6 giờ tới <span className="badge text-bg-primary ms-1">MAMBA · SSM</span>
              </>
            }
            icon="bi-activity"
            phai={<span className="small fw-semibold text-primary">Dự kiến ~{dinhDangSo(tongSsm)} lượt xe tải</span>}
          >
            <div className="tg-ssm">
              {ssm.map((v, i) => {
                const gio = (gioCuoi + 1 + i) % 24
                return (
                  <div key={gio} className="tg-ssm-cot">
                    <div className="small fw-bold text-primary">{v}</div>
                    <div className="tg-ssm-than" style={{ height: Math.max(8, Math.round((v * 100) / maxSsm)) + '%' }} />
                    <div className="tg-cot-nhan">{haiSo(gio)}:00</div>
                  </div>
                )
              })}
            </div>
            <div className="form-text mt-2">
              Mô hình <b>không gian trạng thái chọn lọc</b> (bản tối giản theo kiến trúc Mamba — Selective SSM, 2023): quét chuỗi thời gian tuyến tính O(n) với cổng chọn lọc Δ điều biến trạng
              thái ẩn theo từng đầu vào.
            </div>
          </TheNoiDung>
        </div>

        <div className="col-lg-5">
          <TheNoiDung tieuDe="Cơ cấu đối tượng nhận diện" icon="bi-pie-chart">
            {d.coCauDoiTuong.length ? (
              d.coCauDoiTuong.map((c) => (
                <div key={c.nhan} className="d-flex align-items-center gap-2 mb-2">
                  <span className="small text-truncate" style={{ minWidth: 110 }}>
                    {c.nhan}
                  </span>
                  <div className="tg-thanh">
                    <span style={{ width: Math.round((c.soLuong * 100) / maxCoCau) + '%' }} />
                  </div>
                  <strong className="small text-end" style={{ minWidth: 44 }}>
                    {dinhDangSo(c.soLuong)}
                  </strong>
                </div>
              ))
            ) : (
              <div className="text-body-secondary small">Chưa có dữ liệu.</div>
            )}
          </TheNoiDung>

          <TheNoiDung tieuDe="Xe tải theo vị trí camera" icon="bi-geo-alt">
            {d.theoViTri.length ? (
              d.theoViTri.map((v) => (
                <div key={v.nhan} className="d-flex justify-content-between py-1 border-bottom small">
                  <span>{v.nhan}</span>
                  <strong>{dinhDangSo(v.soLuong)} lượt</strong>
                </div>
              ))
            ) : (
              <div className="text-body-secondary small">Chưa có dữ liệu.</div>
            )}
          </TheNoiDung>
        </div>
      </div>

      <TheNoiDung tieuDe="Lưu lượng theo ngày" icon="bi-calendar3" khongDem>
        <div className="table-responsive">
          <table className="table table-sm sp-bang align-middle mb-0">
            <thead>
              <tr>
                <th>Ngày</th>
                <th className="text-end">Lượt quét</th>
                <th className="text-end">Xe tải</th>
                <th className="text-end d-none d-md-table-cell">Ô tô</th>
                <th className="text-end d-none d-md-table-cell">Người</th>
                <th style={{ minWidth: 160 }}>Biểu đồ</th>
              </tr>
            </thead>
            <tbody>
              {d.theoNgay.map((x) => (
                <tr key={x.nhan}>
                  <td className="fw-semibold">{x.nhan}</td>
                  <td className="text-end">{x.soLuot}</td>
                  <td className="text-end fw-bold text-primary">{x.soXeTai}</td>
                  <td className="text-end d-none d-md-table-cell">{x.soOto}</td>
                  <td className="text-end d-none d-md-table-cell">{x.soNguoi}</td>
                  <td>
                    <div className="tg-thanh">
                      <span className="cam" style={{ width: Math.round((x.soXeTai * 100) / maxNgay) + '%' }} />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </TheNoiDung>

      {dangTai && <div className="small text-body-secondary mt-2">Đang cập nhật…</div>}
    </>
  )
}
