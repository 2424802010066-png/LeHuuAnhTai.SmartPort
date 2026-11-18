import { useRef } from 'react'
import { Bar, Doughnut, Line } from 'react-chartjs-2'
import { useApi } from '../api/useApi'
import BaoLoi from '../components/BaoLoi'
import DangTai from '../components/DangTai'
import DongSuKien from '../components/DongSuKien'
import TheChiSo from '../components/TheChiSo'
import { useNgheSuKien } from '../realtime/SuKienContext'
import '../utils/dangKyBieuDo'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien, tienGon } from '../utils/dinhDang'

const MAU_TRANG_THAI = ['#1266b0', '#f59e0b', '#10b981', '#8b5cf6', '#64748b', '#ef4444']
const MUC_CANH_BAO = { danger: 'danger', warn: 'warning', info: 'info', ok: 'success' }

export default function TongQuan() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/tong-quan')

  // HUONG SU KIEN: module nao phat su kien (xe qua cong, container roi bai, phieu thu moi...) -> lam moi
  // so lieu ngam (gop nhieu su kien sat nhau thanh 1 lan goi)
  const hen = useRef(null)
  useNgheSuKien('*', () => {
    clearTimeout(hen.current)
    hen.current = setTimeout(() => taiLai({ im: true }), 800)
  })

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const { chiSo, duBao } = d
  const diem = duBao.diem
  const cuoiThucTe = diem.findLastIndex((x) => !x.laDuBao)
  const bieuDoTeu = {
    labels: diem.map((x) => x.nhan),
    datasets: [
      {
        label: 'Thực tế',
        data: diem.map((x) => (x.laDuBao ? null : x.teu)),
        borderColor: '#0d4f8b',
        backgroundColor: 'rgba(13,79,139,.08)',
        fill: true,
        tension: 0.35,
        pointRadius: 3,
      },
      {
        label: 'AI dự báo',
        // Noi duong du bao voi diem thuc te cuoi cung cho lien mach
        data: diem.map((x, i) => (x.laDuBao || i === cuoiThucTe ? x.teu : null)),
        borderColor: '#f97316',
        borderDash: [6, 5],
        tension: 0.35,
        pointRadius: 4,
        pointStyle: 'rectRot',
      },
    ],
  }

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        {[
          ['bi-water', 'xanh-ngoc', chiSo.tauTrongCang, 'Tàu trong cảng'],
          ['bi-calendar2-week', 'xanh', chiSo.chuyenSapDen, 'Chuyến đến trong 7 ngày'],
          ['bi-box-seam', 'cam', chiSo.contTrongBai, 'Container trong bãi'],
          ['bi-hourglass-split', 'do', chiSo.bookingChoDuyet, 'Booking chờ duyệt'],
          ['bi-cash-stack', 'la', tienGon(chiSo.doanhThuThang), 'Doanh thu dịch vụ tháng'],
          ['bi-buildings', 'tim', chiSo.soKhachHang, 'Khách hàng'],
        ].map(([icon, mau, so, nhan]) => (
          <div className="col-6 col-md-4 col-xxl-2" key={nhan}>
            <TheChiSo icon={icon} mau={mau} so={so} nhan={nhan} />
          </div>
        ))}
      </div>

      <div className="row g-3 mb-3">
        <div className="col-lg-8">
          <div className="sp-hop-ai h-100">
            <span className="sp-nhan-ai">
              <i className="bi bi-stars me-1" />
              AI DỰ BÁO SẢN LƯỢNG · {duBao.thuatToan}
            </span>
            <div className="row g-3 mt-1">
              <div className="col-sm-4">
                <div className="sp-so-lon">{dinhDangSo(duBao.teuThangToi)} TEU</div>
                <div className="sp-chu-nhat">Dự báo {duBao.nhanThangToi}</div>
              </div>
              <div className="col-sm-4">
                <div className="sp-so-lon">
                  {duBao.tangTruongPhanTram >= 0 ? '+' : ''}
                  {dinhDangSo(duBao.tangTruongPhanTram, 1)}%
                </div>
                <div className="sp-chu-nhat">So với tháng gần nhất</div>
              </div>
              <div className="col-sm-4">
                <div className="sp-so-lon">
                  {duBao.teuTangMoiThang >= 0 ? '+' : ''}
                  {dinhDangSo(duBao.teuTangMoiThang)}
                </div>
                <div className="sp-chu-nhat">TEU/tháng theo xu hướng</div>
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-4">
          <div className="card sp-the h-100">
            <div className="card-body d-flex align-items-center gap-3">
              {chiSo.soChuyenChuaGanBen > 0 ? (
                <>
                  <div className="sp-chi-so-icon cam">
                    <i className="bi bi-exclamation-triangle" />
                  </div>
                  <div>
                    <div className="fw-bold">{chiSo.soChuyenChuaGanBen} chuyến tàu chưa phân bến</div>
                    <div className="text-muted small">Cần điều độ gán bến trước giờ tàu đến</div>
                  </div>
                </>
              ) : (
                <>
                  <div className="sp-chi-so-icon la">
                    <i className="bi bi-check2-circle" />
                  </div>
                  <div>
                    <div className="fw-bold">Tất cả chuyến đã có bến</div>
                    <div className="text-muted small">Điều độ trong tầm kiểm soát</div>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-xl-8">
          <div className="card sp-the h-100">
            <div className="card-header">
              <i className="bi bi-bell me-2 text-warning" />
              Cảnh báo vận hành
            </div>
            <div className="card-body">
              {d.canhBao.length === 0 && (
                <div className="text-center text-muted py-3">
                  <i className="bi bi-check2-circle text-success me-1" />
                  Mọi chỉ số vận hành đều ổn.
                </div>
              )}
              {d.canhBao.map((a, i) => (
                <div key={i} className={'sp-canh-bao border-' + (MUC_CANH_BAO[a.muc] || 'secondary')}>
                  <i className={'bi ' + a.icon + ' text-' + (MUC_CANH_BAO[a.muc] || 'secondary')} />
                  <span className="flex-grow-1">{a.noiDung}</span>
                  {a.duongDan && (
                    <a className="btn btn-sm btn-outline-primary" href={a.duongDan} target="_blank" rel="noreferrer">
                      Xử lý <i className="bi bi-arrow-right" />
                    </a>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
        <div className="col-xl-4">
          <DongSuKien gioiHan={5} />
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-lg-8">
          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-graph-up me-2 text-primary" />
              Sản lượng container (TEU) — lịch sử và dự báo
            </div>
            <div className="card-body sp-bieu-do">
              <Line data={bieuDoTeu} options={{ maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }, scales: { x: { grid: { display: false } } } }} />
            </div>
          </div>
        </div>
        <div className="col-lg-4">
          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-pie-chart me-2 text-primary" />
              Container theo trạng thái
            </div>
            <div className="card-body sp-bieu-do">
              <Doughnut
                data={{
                  labels: d.containerTheoTrangThai.map((x) => x.nhom),
                  datasets: [{ data: d.containerTheoTrangThai.map((x) => x.soLuong), backgroundColor: MAU_TRANG_THAI, borderWidth: 2 }],
                }}
                options={{ maintainAspectRatio: false, cutout: '62%', plugins: { legend: { position: 'bottom' } } }}
              />
            </div>
          </div>
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-lg-5">
          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-bar-chart me-2 text-primary" />
              Doanh thu khai thác 6 tháng (tỷ đồng)
            </div>
            <div className="card-body sp-bieu-do">
              <Bar
                data={{
                  labels: d.doanhThu6Thang.map((x) => x.nhan),
                  datasets: [{ label: 'Tỷ đồng', data: d.doanhThu6Thang.map((x) => x.giaTri), backgroundColor: '#1173c4', borderRadius: 7, maxBarThickness: 40 }],
                }}
                options={{ maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { x: { grid: { display: false } } } }}
              />
            </div>
          </div>
        </div>
        <div className="col-lg-7">
          <div className="card sp-the">
            <div className="card-header">
              <i className="bi bi-receipt me-2 text-primary" />
              Booking mới nhất
            </div>
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0 sp-bang">
                <thead>
                  <tr>
                    <th>Số booking</th>
                    <th>Khách hàng</th>
                    <th>Ngày tạo</th>
                    <th className="text-end">Tổng tiền</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {d.bookingMoi.map((b) => (
                    <tr key={b.maSo}>
                      <td className="fw-semibold">{b.maSo}</td>
                      <td>{b.khachHang ?? '—'}</td>
                      <td>{dinhDangNgay(b.ngayTao)}</td>
                      <td className="text-end">{dinhDangTien(b.tongTien)}</td>
                      <td>
                        <span className="badge text-bg-light">{b.trangThai}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <div className="card sp-the">
        <div className="card-header">
          <i className="bi bi-calendar2-week me-2 text-primary" />
          Chuyến tàu sắp tới / đang làm hàng
        </div>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0 sp-bang">
            <thead>
              <tr>
                <th>Chuyến</th>
                <th>Tàu</th>
                <th>ETA</th>
                <th>ETD</th>
                <th>Bến</th>
                <th>Cont nhập/xuất</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {d.chuyenSapToi.map((c) => (
                <tr key={c.maChuyen}>
                  <td className="fw-semibold">{c.maChuyen}</td>
                  <td>{c.tenTau ?? '—'}</td>
                  <td>{dinhDangNgayGio(c.eta)}</td>
                  <td>{dinhDangNgayGio(c.etd)}</td>
                  <td>{c.ben ?? <span className="badge text-bg-warning">Chưa phân bến</span>}</td>
                  <td>
                    {c.contNhap} / {c.contXuat}
                  </td>
                  <td>
                    <span className="badge text-bg-light">{c.trangThai}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  )
}
