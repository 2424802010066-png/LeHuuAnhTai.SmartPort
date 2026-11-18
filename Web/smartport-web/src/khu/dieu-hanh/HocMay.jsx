import { Bar } from 'react-chartjs-2'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import './dieu-hanh.css'

/** Ve de quy cay CART: nut trong la cau hoi, la la ket luan an toan / nguy co huy */
function NhanhCay({ nut }) {
  if (!nut) return null
  if (nut.laLa) {
    const nguyCo = nut.tyLeHuy >= 50
    return (
      <span className={'dh-la ' + (nguyCo ? 'nguy-co' : 'an-toan')}>
        {nguyCo ? 'NGUY CƠ HỦY' : 'An toàn'} · {dinhDangSo(nut.tyLeHuy, 1)}% ({nut.soMau} mẫu)
      </span>
    )
  }
  return (
    <li>
      <span className="dh-cay-hoi">
        {nut.tenDacTrung} ≤ {dinhDangSo(nut.nguongCat, 1)} ?
      </span>
      <span className="dh-cay-meta">
        ({nut.soMau} mẫu · {dinhDangSo(nut.tyLeHuy, 1)}% hủy)
      </span>
      <ul>
        <li>
          <span className="dh-cay-nhanh">Đúng →</span>
          <NhanhCay nut={nut.trai} />
        </li>
        <li>
          <span className="dh-cay-nhanh">Sai →</span>
          <NhanhCay nut={nut.phai} />
        </li>
      </ul>
    </li>
  )
}

/**
 * HOC MAY chay tren may chu (thay man hinh MVC Admin/AiTools/ML):
 * K-Means phan khuc khach hang · Z-score bat san luong bat thuong · cay quyet dinh CART du doan huy booking.
 * Ba thuat toan ba API rieng -> tai song song, phan nao xong hien phan do.
 */
export default function HocMay() {
  const [loc, datLoc] = useBoLoc({ k: 3, nguong: 1.8 })

  const cum = useApi('/api/hoc-may/phan-cum' + taoQuery({ k: loc.k }))
  const bt = useApi('/api/hoc-may/bat-thuong' + taoQuery({ nguong: loc.nguong }))
  const cay = useApi('/api/hoc-may/cay-quyet-dinh')

  const diem = bt.duLieu?.diem || []
  const duLieuCot = {
    labels: diem.map((a) => a.nhan),
    datasets: [
      {
        label: 'TEU',
        data: diem.map((a) => a.teu),
        backgroundColor: diem.map((a) => (a.batThuong ? 'rgba(220,38,38,.85)' : 'rgba(28,95,168,.8)')),
        borderRadius: 4,
      },
    ],
  }
  const tuyChonCot = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (c) => dinhDangSo(c.parsed.y) + ' TEU',
          afterLabel: (c) => {
            const a = diem[c.dataIndex]
            if (!a) return ''
            return `Biến động ${a.bienDongPct > 0 ? '+' : ''}${dinhDangSo(a.bienDongPct, 1)}% · z = ${dinhDangSo(a.zScore, 2)}`
          },
        },
      },
    },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  }

  const COT_BT = [
    { khoa: 'nhan', ten: 'Tháng', hien: (a) => <span className="fw-semibold">{a.nhan}</span> },
    { khoa: 'teu', ten: 'Sản lượng (TEU)', canPhai: true, hien: (a) => dinhDangSo(a.teu) },
    {
      khoa: 'bd',
      ten: 'Biến động',
      canPhai: true,
      hien: (a) => (
        <span className={a.bienDongPct >= 0 ? 'text-success' : 'text-danger'}>
          {a.bienDongPct >= 0 ? '+' : ''}
          {dinhDangSo(a.bienDongPct, 1)}%
        </span>
      ),
    },
    { khoa: 'z', ten: 'Z-score', canPhai: true, hien: (a) => <b className="text-danger">{dinhDangSo(a.zScore, 2)}</b> },
    {
      khoa: 'nhanDinh',
      ten: 'Nhận định của thuật toán',
      an: 'md',
      hien: (a) => <span className="small">Sản lượng {a.huong} đột biến so với nhịp bình thường — nên rà soát nguyên nhân (mùa vụ, tuyến mới, sự cố…).</span>,
    },
  ]

  return (
    <>
      {/* ============ 1. K-MEANS ============ */}
      <TheNoiDung
        tieuDe="K-Means — phân khúc khách hàng (học không giám sát)"
        icon="bi-diagram-3"
        phai={
          <>
            {cum.duLieu ? <NhanTrangThai mau={cum.duLieu.hoiTu ? 'ok' : 'warn'}>{cum.duLieu.thongBao}</NhanTrangThai> : null}
            <label className="small text-body-secondary" htmlFor="dh-k">
              Số nhóm k
            </label>
            <select
              id="dh-k"
              className="form-select form-select-sm dh-o-nho"
              value={loc.k}
              onChange={(e) => datLoc({ k: Number(e.target.value) })}
              disabled={cum.dangTai}
            >
              {[2, 3, 4, 5].map((n) => (
                <option key={n} value={n}>
                  {n} nhóm
                </option>
              ))}
            </select>
          </>
        }
      >
        <BaoLoi loi={cum.loi} thuLai={cum.taiLai} />
        <p className="text-body-secondary small mb-3">
          3 đặc trưng đầu vào: <b>tổng doanh thu · số booking · số container</b> (chuẩn hóa Min-Max về [0,1]). Khởi tạo tâm cụm kiểu k-means++, lặp gán
          cụm — cập nhật tâm đến khi hội tụ ({cum.duLieu ? cum.duLieu.soVongLap + ' vòng, ' + cum.duLieu.soKhachHang + ' khách hàng' : '…'}).
        </p>
        {cum.dangTai && !cum.duLieu ? (
          <DangTai chu="Đang phân cụm…" />
        ) : !cum.duLieu?.cum?.length ? (
          <div className="text-body-secondary">{cum.duLieu?.thongBao || 'Chưa có dữ liệu.'}</div>
        ) : (
          <div className="row g-3">
            {cum.duLieu.cum.map((c) => (
              <div className="col-lg-4 col-md-6" key={c.ten}>
                <div className="dh-cum" style={{ borderTopColor: c.mau }}>
                  <div className="d-flex align-items-center gap-2 mb-1">
                    <span className="dh-cum-cham" style={{ background: c.mau }} />
                    <strong className="flex-grow-1">{c.ten}</strong>
                    <span className="dh-chip">{c.khachHang.length} KH</span>
                  </div>
                  <div className="small text-body-secondary">{c.moTa}</div>
                  <div className="dh-cum-so">
                    <div title={dinhDangTien(c.doanhThuTB)}>
                      <span>Doanh thu TB</span>
                      <b>{dinhDangTien(c.doanhThuTB)}</b>
                    </div>
                    <div>
                      <span>Booking TB</span>
                      <b>{dinhDangSo(c.bookingTB, 1)}</b>
                    </div>
                    <div>
                      <span>Container TB</span>
                      <b>{dinhDangSo(c.containerTB, 1)}</b>
                    </div>
                  </div>
                  <div className="dh-cum-ds">
                    {c.khachHang.map((kh) => (
                      <div className="dh-cum-kh" key={kh.maKH}>
                        <span className="text-truncate">{kh.tenCongTy}</span>
                        <span className="text-body-secondary small text-nowrap">
                          {dinhDangTien(kh.doanhThu)} · {kh.soBooking} BK
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </TheNoiDung>

      {/* ============ 2. PHAT HIEN BAT THUONG ============ */}
      <TheNoiDung
        tieuDe="Phát hiện bất thường sản lượng (Z-score)"
        icon="bi-activity"
        phai={
          <>
            {bt.duLieu ? (
              <NhanTrangThai mau={bt.duLieu.soBatThuong ? 'warn' : 'ok'}>
                {bt.duLieu.soBatThuong ? bt.duLieu.soBatThuong + ' điểm bất thường' : 'Không có bất thường'}
              </NhanTrangThai>
            ) : null}
            <label className="small text-body-secondary" htmlFor="dh-nguong">
              Ngưỡng |z|
            </label>
            <input
              id="dh-nguong"
              type="number"
              className="form-control form-control-sm dh-o-nho"
              min={1}
              max={3}
              step={0.1}
              value={loc.nguong}
              onChange={(e) => datLoc({ nguong: e.target.value })}
            />
          </>
        }
      >
        <BaoLoi loi={bt.loi} thuLai={bt.taiLai} />
        <p className="text-body-secondary small mb-3">
          Tính biến động % tháng-sang-tháng của chuỗi TEU rồi chuẩn hóa thành Z-score <code>z = (x − μ) / σ</code>. Điểm có{' '}
          <b>|z| ≥ {dinhDangSo(bt.duLieu?.nguong ?? loc.nguong, 1)}</b> được coi là bất thường và tô đỏ.
        </p>
        {bt.dangTai && !bt.duLieu ? (
          <DangTai chu="Đang tính Z-score…" />
        ) : !diem.length ? (
          <div className="text-body-secondary">Chưa đủ dữ liệu chuỗi thời gian.</div>
        ) : (
          <>
            <div className="dh-bieu-do mb-3">
              <Bar data={duLieuCot} options={tuyChonCot} />
            </div>
            <BangDuLieu
              cot={COT_BT}
              dong={diem.filter((a) => a.batThuong)}
              khoaDong="nhan"
              rong="Không tháng nào vượt ngưỡng — sản lượng đi đúng nhịp."
            />
          </>
        )}
      </TheNoiDung>

      {/* ============ 3. CAY QUYET DINH ============ */}
      <TheNoiDung
        tieuDe="Cây quyết định — dự đoán rủi ro hủy booking"
        icon="bi-diagram-2"
        phai={
          cay.duLieu?.duDuLieu ? (
            <span className="small text-body-secondary">
              {cay.duLieu.thongBao} · độ chính xác <b className="text-primary">{dinhDangSo(cay.duLieu.doChinhXac, 1)}%</b>
            </span>
          ) : null
        }
      >
        <BaoLoi loi={cay.loi} thuLai={cay.taiLai} />
        <p className="text-body-secondary small mb-3">
          Học <b>có giám sát</b>: thuật toán <b>CART</b> tự chọn điểm cắt tối thiểu hóa <b>Gini impurity</b> trên 3 đặc trưng (giá trị đơn · số dịch vụ ·
          số ngày chờ). Mô hình <b>giải thích được</b> từng bước ra quyết định, khác hộp đen của mạng nơ-ron.
        </p>
        {cay.dangTai && !cay.duLieu ? (
          <DangTai chu="Đang huấn luyện cây…" />
        ) : !cay.duLieu?.duDuLieu ? (
          <div className="alert alert-light border small mb-0">{cay.duLieu?.thongBao || 'Chưa có dữ liệu.'}</div>
        ) : (
          <div className="row g-3">
            <div className="col-lg-7">
              <div className="dh-cay">
                <ul>
                  <NhanhCay nut={cay.duLieu.goc} />
                </ul>
              </div>
            </div>
            <div className="col-lg-5">
              <div className="fw-semibold small mb-2">Áp dụng cho đơn ĐANG CHỜ DUYỆT:</div>
              {!cay.duLieu.duDoan.length ? (
                <div className="text-body-secondary small">Hiện không có đơn nào chờ duyệt.</div>
              ) : (
                cay.duLieu.duDoan.map((p) => (
                  <div className={'dh-du-doan' + (p.nguyCo >= 50 ? ' nguy-co' : '')} key={p.maSo}>
                    <div className="d-flex justify-content-between gap-2">
                      <b>{p.maSo}</b>
                      <span className={'fw-bold ' + (p.nguyCo >= 50 ? 'text-danger' : 'text-success')}>{dinhDangSo(p.nguyCo, 1)}% nguy cơ hủy</span>
                    </div>
                    {p.giaiThich ? <div className="small text-body-secondary">Đường quyết định: {p.giaiThich}</div> : null}
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </TheNoiDung>
    </>
  )
}
