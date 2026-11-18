import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Bar } from 'react-chartjs-2'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien, tienGon } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import { CachTinh, OChiSo, ThanhCongCu, ThanhDiem } from './chung'
import './ai-van-hanh.css'

const MAU_NGUON = {
  'Lịch tàu xuất': 'info',
  'Học máy k-NN': 'ok',
  'Thống kê lịch sử': 'run',
  'Quá thời gian dự kiến': 'warn',
  'Tàu đã quá ETD': 'do',
  'Ít dữ liệu': 'off',
}

const LOC = [
  { ma: '', ten: 'Tất cả' },
  { ma: '72h', ten: 'Rời trong 72 giờ' },
  { ma: 'qua', ten: 'Quá dự kiến' },
]

const laQuaHan = (c) => c.nguon === 'Quá thời gian dự kiến' || c.nguon === 'Tàu đã quá ETD'

const COT_CONT = [
  {
    khoa: 'soCont',
    ten: 'Container',
    hien: (c) => (
      <>
        <div className="fw-semibold font-monospace">{c.soCont}</div>
        <div className="small text-body-secondary">
          {c.loaiCont} · {c.tinhTrang}
        </div>
      </>
    ),
  },
  { khoa: 'kh', ten: 'Khách hàng', an: 'md', lop: 'small', hien: (c) => c.khachHang || '—' },
  { khoa: 'viTri', ten: 'Vị trí', hien: (c) => <span className="font-monospace small">{c.viTriBai || '—'}</span> },
  { khoa: 'daLuu', ten: 'Đã lưu', canPhai: true, lop: 'small', hien: (c) => dinhDangSo(c.daLuuNgay, 1) + ' ngày' },
  {
    khoa: 'roi',
    ten: 'Dự báo rời bãi',
    hien: (c) =>
      laQuaHan(c) ? (
        // Qua han: khong co ngay roi dang tin — hien viec can lam thay vi "con X ngay"
        <>
          <div className="fw-semibold text-danger">Cần xử lý</div>
          <div className="small text-body-secondary">{c.nguon === 'Tàu đã quá ETD' ? 'tàu đã đi — rớt tàu / đổi chuyến?' : 'lưu lâu bất thường — gọi chủ hàng'}</div>
        </>
      ) : (
        <>
          <div className="fw-semibold">{dinhDangNgay(c.ngayRoiDuKien)}</div>
          <div className="small text-body-secondary">{c.conLaiNgay <= 0.05 ? 'đến hạn' : 'còn ' + dinhDangSo(c.conLaiNgay, 1) + ' ngày'}</div>
        </>
      ),
  },
  { khoa: 'nguon', ten: 'Căn cứ', hien: (c) => <NhanTrangThai giaTri={c.nguon} mau={MAU_NGUON[c.nguon] || 'info'} /> },
  {
    khoa: 'tin',
    ten: 'Tin cậy',
    an: 'md',
    rong: '7.5rem',
    hien: (c) => (
      <div className="d-flex align-items-center gap-2">
        <ThanhDiem giaTri={c.doTinCay} mau={c.doTinCay >= 70 ? 'ok' : c.doTinCay >= 45 ? 'warn' : 'off'} />
        <span className="small">{c.doTinCay}%</span>
      </div>
    ),
  },
  { khoa: 'phi', ten: 'Phí lưu bãi khi rời', canPhai: true, an: 'lg', hien: (c) => dinhDangTien(c.phiDuKienKhiRoi) },
]

const COT_NUOC = [
  { khoa: 'thuTu', ten: '#', rong: '2.5rem', hien: (n) => <b>{n.thuTu}</b> },
  { khoa: 'soCont', ten: 'Container', hien: (n) => <span className="fw-semibold font-monospace">{n.soCont}</span> },
  {
    khoa: 'tuDen',
    ten: 'Chuyển',
    hien: (n) => (
      <span className="font-monospace small text-nowrap">
        {n.tu} <i className="bi bi-arrow-right mx-1" />
        <b>{n.den}</b>
      </span>
    ),
  },
  { khoa: 'roi', ten: 'Rời dự kiến', an: 'md', lop: 'small', hien: (n) => dinhDangNgay(n.ngayRoiDuKien) },
  { khoa: 'giam', ten: 'Bớt đảo', canPhai: true, hien: (n) => <NhanTrangThai mau="ok">−{n.giamDao}</NhanTrangThai> },
  { khoa: 'lyDo', ten: 'Lý do', an: 'lg', lop: 'small text-body-secondary', hien: (n) => n.lyDo },
]

const COT_CHAN = [
  { khoa: 'soCont', ten: 'Container bị chặn', hien: (b) => <span className="fw-semibold font-monospace">{b.soCont}</span> },
  { khoa: 'viTri', ten: 'Vị trí', hien: (b) => <span className="font-monospace small">{b.viTri}</span> },
  { khoa: 'roi', ten: 'Rời dự kiến', lop: 'small', hien: (b) => dinhDangNgay(b.ngayRoiDuKien) },
  { khoa: 'boi', ten: 'Bị đè bởi (rời muộn hơn)', lop: 'small font-monospace', hien: (b) => b.biChanBoi.join(', ') },
]

/** Dem container du kien roi bai theo ngay (14 ngay toi) — cho bo phan khai thac chuan bi xe nang, nhan luc */
function useBieuDoRoiBai(cont) {
  return useMemo(() => {
    if (!cont?.length) return null
    const homNay = new Date()
    homNay.setHours(0, 0, 0, 0)
    const nhan = []
    const dem = []
    for (let i = 0; i < 14; i++) {
      const d = new Date(homNay)
      d.setDate(d.getDate() + i)
      nhan.push(i === 0 ? 'Hôm nay' : `${d.getDate()}/${d.getMonth() + 1}`)
      dem.push(0)
    }
    let sau = 0
    // Container qua han (tau da di / nam lau hon moi mau) khong co ngay roi dang tin — dem rieng o the "Quá thời gian dự kiến"
    for (const c of cont.filter((x) => !laQuaHan(x))) {
      const d = new Date(c.ngayRoiDuKien)
      d.setHours(0, 0, 0, 0)
      const i = Math.round((d - homNay) / 864e5)
      if (i < 14) dem[Math.max(0, i)]++
      else sau++
    }
    nhan.push('Sau 14 ngày')
    dem.push(sau)
    return {
      labels: nhan,
      datasets: [
        {
          label: 'Container rời bãi',
          data: dem,
          backgroundColor: dem.map((_, i) => (i < 3 ? '#ea580c' : i < 14 ? '#0284c7' : '#94a3b8')),
          borderRadius: 4,
        },
      ],
    }
  }, [cont])
}

const TUY_CHON_COT = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: { callbacks: { label: (c) => c.parsed.y + ' container' } },
  },
  scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
}

/**
 * AI BAI: du bao NGAY ROI BAI tung container (k-NN co trong so, tinh ca du lieu bi cat) va KE HOACH DAO CHUYEN TRUOC.
 * So lieu: GET /api/ai-van-hanh/roi-bai, GET /api/ai-van-hanh/dao-chuyen.
 */
export default function AiBai() {
  const [loc, datLoc] = useBoLoc({ loc: '', phut: 4, nuoc: 12 })
  const { duLieu: roi, loi, dangTai, taiLai } = useApi('/api/ai-van-hanh/roi-bai')
  const dao = useApi('/api/ai-van-hanh/dao-chuyen' + taoQuery({ phutMoiLanDao: loc.phut, toiDaNuoc: loc.nuoc }))
  const [chon, setChon] = useState(null)
  const bieuDo = useBieuDoRoiBai(roi?.cont)

  if (dangTai && !roi) return <DangTai />
  if (!roi) return <BaoLoi loi={loi} thuLai={taiLai} />

  const dsLoc = roi.cont.filter((c) => (loc.loc === '72h' ? !laQuaHan(c) && c.conLaiNgay <= 3 : loc.loc === 'qua' ? laQuaHan(c) : true))
  const contChon = roi.cont.find((c) => c.maCont === chon)
  const k = dao.duLieu
  const lamMoi = () => {
    taiLai()
    dao.taiLai()
  }

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <ThanhCongCu luc={dinhDangNgayGio(roi.lucTinh)} dangTai={dangTai || dao.dangTai} lamMoi={lamMoi} />

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OChiSo so={dinhDangSo(roi.soContTrongBai)} donVi="cont" nhan="Đang nằm bãi" phu={`${roi.soRoiTrong24h} container rời trong 24 giờ`} icon="bi-boxes" />
        </div>
        <div className="col">
          <OChiSo mau="cam" so={dinhDangSo(roi.soRoiTrong72h)} donVi="cont" nhan="Rời bãi trong 72 giờ" phu="chuẩn bị xe nâng, sắp chồng" icon="bi-box-arrow-right" />
        </div>
        <div className="col">
          <OChiSo mau={roi.soQuaDuKien ? 'do' : 'la'} so={dinhDangSo(roi.soQuaDuKien)} donVi="cont" nhan="Quá thời gian dự kiến" phu="nhắc khách lấy hàng / kiểm tra" icon="bi-hourglass-bottom" />
        </div>
        <div className="col">
          <OChiSo mau="la" so={tienGon(roi.tongPhiDuKien)} donVi="đ" nhan="Phí lưu bãi dự kiến khi rời" phu={`đang treo ${tienGon(roi.tongPhiDangTreo)} đ`} icon="bi-cash-coin" />
        </div>
      </div>

      <div className="card sp-the p-3 mb-3">
        <div className="av-chat-luong">
          <span className="fw-bold av-chu-tim">
            <i className="bi bi-clipboard-data me-1" />
            Mô hình đang dùng
          </span>
          <NhanTrangThai mau={roi.moHinhDangDung === 'Học máy k-NN' ? 'ok' : 'run'} icon="bi-cpu">
            {roi.moHinhDangDung}
          </NhanTrangThai>
          {roi.maeMoHinh != null ? (
            <>
              <span>
                Sai số k-NN <b className={roi.moHinhDangDung === 'Học máy k-NN' ? 'av-chu-la' : ''}>{dinhDangSo(roi.maeMoHinh, 1)}</b> ngày
              </span>
              <span>
                Thống kê (trung vị) <b className={roi.moHinhDangDung === 'Học máy k-NN' ? '' : 'av-chu-la'}>{dinhDangSo(roi.maeCoSo, 1)}</b> ngày
              </span>
              <span className="small text-body-secondary">kiểm định chéo bỏ-một trên {roi.soMauHuanLuyen} container đã rời bãi</span>
            </>
          ) : (
            <span className="small text-body-secondary">Chưa đủ container đã rời bãi để kiểm định ({roi.soMauHuanLuyen} mẫu).</span>
          )}
        </div>
        <div className="small text-body-secondary mt-1">{roi.thongDiep}</div>
      </div>

      <div className="row g-3">
        <div className="col-xl-8">
          <TheNoiDung
            tieuDe="Dự báo rời bãi từng container"
            icon="bi-calendar2-range"
            khongDem
            phai={
              <div className="btn-group btn-group-sm" role="group" aria-label="Lọc container">
                {LOC.map((l) => (
                  <button
                    key={l.ma}
                    type="button"
                    className={'btn ' + (loc.loc === l.ma ? 'btn-primary' : 'btn-outline-primary')}
                    onClick={() => datLoc({ loc: l.ma })}
                  >
                    {l.ten}
                  </button>
                ))}
              </div>
            }
            chan={<span className="small text-body-secondary">Bấm một dòng để xem vì sao AI dự báo như vậy.</span>}
          >
            <BangDuLieu
              cot={COT_CONT}
              dong={dsLoc}
              khoaDong="maCont"
              dangTai={dangTai}
              rong="Không có container nào khớp bộ lọc."
              bamDong={(c) => setChon(c.maCont === chon ? null : c.maCont)}
              lopDong={(c) => (c.maCont === chon ? 'av-dong-chon' : '')}
              cuon="30rem"
            />
          </TheNoiDung>
        </div>

        <div className="col-xl-4">
          <TheNoiDung tieuDe={contChon ? 'Căn cứ dự báo · ' + contChon.soCont : 'Căn cứ dự báo'} icon="bi-chat-square-text">
            {contChon ? (
              <>
                <div className="mb-2">
                  Rời bãi khoảng <b>{dinhDangNgay(contChon.ngayRoiDuKien)}</b> — tổng thời gian lưu dự báo{' '}
                  <b>{dinhDangSo(contChon.duBaoTongNgay, 1)} ngày</b>, độ tin cậy <b>{contChon.doTinCay}%</b>.
                </div>
                <ul className="small ps-3 mb-2">
                  {contChon.canCu.map((l) => (
                    <li key={l}>{l}</li>
                  ))}
                </ul>
                <div className="small text-body-secondary">
                  Phí đang treo {dinhDangTien(contChon.phiDangTreo)} · khi rời ≈ <b>{dinhDangTien(contChon.phiDuKienKhiRoi)}</b>
                </div>
              </>
            ) : (
              <div className="small text-body-secondary">Chọn một container trong bảng.</div>
            )}
          </TheNoiDung>

          <TheNoiDung tieuDe="Container rời bãi theo ngày" icon="bi-bar-chart">
            {bieuDo ? (
              <>
                <div className="av-bieu-do-nho">
                  <Bar data={bieuDo} options={TUY_CHON_COT} />
                </div>
                {roi.soQuaDuKien > 0 && (
                  <div className="small text-body-secondary mt-1">Không tính {roi.soQuaDuKien} container quá thời gian dự kiến (xem bộ lọc &quot;Quá dự kiến&quot;).</div>
                )}
              </>
            ) : (
              <div className="small text-body-secondary">Bãi đang trống.</div>
            )}
          </TheNoiDung>
        </div>
      </div>

      <TheNoiDung
        tieuDe="Kế hoạch đảo chuyển trước (làm lúc bãi rảnh)"
        icon="bi-arrow-left-right"
        phai={
          <div className="d-flex flex-wrap gap-2 align-items-center small">
            <label className="d-flex align-items-center gap-1">
              Phút/lần đảo
              <select className="form-select form-select-sm w-auto" value={loc.phut} onChange={(e) => datLoc({ phut: Number(e.target.value) })}>
                {[2, 3, 4, 5, 6, 8, 10].map((x) => (
                  <option key={x} value={x}>
                    {x}
                  </option>
                ))}
              </select>
            </label>
            <label className="d-flex align-items-center gap-1">
              Tối đa
              <select className="form-select form-select-sm w-auto" value={loc.nuoc} onChange={(e) => datLoc({ nuoc: Number(e.target.value) })}>
                {[5, 10, 12, 20, 30, 50].map((x) => (
                  <option key={x} value={x}>
                    {x} nước
                  </option>
                ))}
              </select>
            </label>
          </div>
        }
      >
        <BaoLoi loi={dao.loi} thuLai={dao.taiLai} />
        {!k ? (
          <DangTai />
        ) : (
          <>
            <div className="row row-cols-1 row-cols-md-3 g-3 mb-3">
              <div className="col">
                <OChiSo
                  mau={k.daoChuyenTruoc > k.daoChuyenSau ? 'vang' : 'la'}
                  so={`${k.daoChuyenTruoc} → ${k.daoChuyenSau}`}
                  donVi="lần"
                  nhan="Đảo chuyển khi khách lấy hàng"
                  phu={`nếu làm ${k.nuocDi.length} nước đảo trước`}
                  icon="bi-arrow-down-up"
                />
              </div>
              <div className="col">
                <OChiSo mau="tim" so={dinhDangSo(k.phutTietKiem)} donVi="phút" nhan="Xe nâng bớt lúc cao điểm" phu={`${dinhDangSo(k.phutMoiLanDao)} phút mỗi lần đảo`} icon="bi-stopwatch" />
              </div>
              <div className="col">
                <OChiSo mau="ngoc" so={dinhDangSo(k.soChongCoCont)} donVi="chồng" nhan="Đang có hàng" phu={`${k.soContTrenSoDo} container trên sơ đồ bãi`} icon="bi-grid-3x3-gap" />
              </div>
            </div>
            <div className="small mb-2">{k.thongDiep}</div>
            {k.nuocDi.length > 0 && (
              <div className="mb-3">
                <BangDuLieu cot={COT_NUOC} dong={k.nuocDi} khoaDong="thuTu" />
              </div>
            )}
            {k.biChan.length > 0 && (
              <details>
                <summary className="small fw-semibold mb-2">{k.biChan.length} container đang bị container rời muộn hơn đè lên</summary>
                <BangDuLieu cot={COT_CHAN} dong={k.biChan} khoaDong="soCont" cuon="18rem" />
              </details>
            )}
            <div className="small mt-2">
              <Link to="/admin/containers/yardmap">
                <i className="bi bi-grid-3x3-gap me-1" />
                Mở sơ đồ bãi
              </Link>
            </div>
          </>
        )}
      </TheNoiDung>

      <CachTinh>
        <p className="mb-1">
          <b>1. Dự báo ngày rời bãi — k láng giềng gần nhất (k-NN) có trọng số.</b> Mỗi container đã rời bãi là một mẫu học. Độ giống
          nhau giữa hai container tính theo Gower:
        </p>
        <div className="av-cong-thuc">
          {'giống(a, b) = 1 − Σ wᵢ·lệchᵢ / Σ wᵢ      (lệch: trùng 0 · khác 1 · thiếu thông tin 0,5)\n' +
            'w: khách hàng 2 · rỗng/đầy 1,5 · nhập/xuất 1,5 · hãng tàu 1 · kích cỡ 1 · lạnh 1\n' +
            'số ngày lưu dự báo = Σ (giống² + 0,01)·ngàyⱼ / Σ (giống² + 0,01)   trên 5 mẫu giống nhất (lấy cả mẫu hoà điểm)'}
        </div>
        <p className="mb-1">
          Khách hàng nặng ký nhất vì mỗi chủ hàng có thói quen lấy hàng riêng. Container đã nằm <i>D</i> ngày thì chỉ dùng các mẫu lưu lâu hơn{' '}
          <i>D</i> (dữ liệu bị cắt — <i>E[T | T &gt; D]</i>), nên AI không bao giờ dự báo &quot;lẽ ra đã đi rồi&quot;. Container xuất
          đã gắn chuyến thì lấy theo ETD của tàu.
        </p>
        <p className="mb-1">
          <b>Kiểm định và tự chọn mô hình (champion/challenger):</b> bỏ lần lượt từng container ra, dự báo nó bằng các container còn lại, đo sai
          số tuyệt đối trung bình (MAE) của k-NN và của cách thống kê đơn giản — trung vị thời gian lưu (hằng số tốt nhất theo MAE). k-NN chỉ được
          dùng khi sai số ≤ 95% cách thống kê; chưa thắng thì hệ thống dùng trung vị có điều kiện và nói rõ trên màn hình. Mỗi lần tính đều kiểm
          định lại, nên khi dữ liệu nhiều lên mà k-NN thắng thì tự chuyển — <i>AI không bao giờ được kém cách làm cũ</i>.
        </p>
        <p className="mb-1">
          <b>2. Đếm đảo chuyển:</b> trong một chồng, mỗi cặp (container dưới <i>i</i>, container trên <i>j</i>) mà <i>j</i> rời sau{' '}
          <i>i</i> thì lúc rút <i>i</i> phải nhấc <i>j</i> → +1 lần đảo.
        </p>
        <p className="mb-0">
          <b>3. Đảo trước (pre-marshalling) tham lam:</b> mỗi bước chọn container trên đỉnh đang đè nhiều container rời sớm hơn nó nhất,
          chuyển sang chồng cùng khu còn chỗ mà ở đó nó đè ít hơn; ưu tiên nước bớt được nhiều lần đảo nhất, rồi chồng có đỉnh rời muộn hơn
          sát nhất (giữ chồng trống cho sau). Dừng khi hết nước có lợi. Làm lúc bãi rảnh → giờ cao điểm xe nâng chỉ việc rút.
        </p>
      </CachTinh>
    </>
  )
}
