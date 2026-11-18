import { Link } from 'react-router-dom'
import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, NhanTrangThai, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { CachTinh, OChiSo, ThanhCongCu, ThanhDiem } from './chung'
import './ai-van-hanh.css'

const NGUONG = [
  { v: 0.55, t: 'Nhạy (0,55)' },
  { v: 0.62, t: 'Cân bằng (0,62)' },
  { v: 0.7, t: 'Chặt (0,70)' },
]

// Gia tri cot XeRaVao.Nguon: cong thong minh (AI doc bien so tu Webcam / anh tai len / anh mau / camera ALPR),
// NhapTay (nhan vien go), ThuCong (doi soat cong), OFFLINE (lo nhap bu khi mat mang)
const TEN_NGUON = {
  ALPR: 'Camera ALPR',
  Webcam: 'Webcam · AI đọc',
  CameraIP: 'Camera IP · AI đọc',
  TaiAnh: 'Ảnh tải lên · AI đọc',
  AnhMau: 'Ảnh mẫu · AI đọc',
  NhapTay: 'Nhập tay',
  ThuCong: 'Nhập tay',
  OFFLINE: 'Nhập bù',
}

function taoCot(nguong) {
  return [
    {
      khoa: 'diem',
      ten: 'Điểm',
      rong: '7rem',
      hien: (l) => (
        <div className="d-flex align-items-center gap-2">
          <ThanhDiem giaTri={l.diem} toiDa={1} mau={l.diem >= Math.max(0.7, nguong) ? 'do' : l.diem >= nguong ? 'warn' : 'off'} />
          <span className="small fw-semibold">{dinhDangSo(l.diem, 2)}</span>
        </div>
      ),
    },
    { khoa: 'luc', ten: 'Thời gian', lop: 'small text-nowrap', hien: (l) => gioNgayNgan(l.thoiGian) },
    {
      khoa: 'bienSo',
      ten: 'Biển số',
      hien: (l) => (
        <Link className="fw-semibold font-monospace text-nowrap" to={'/admin/gate/hosoxe' + taoQuery({ bienSo: l.bienSo })} title="Mở hồ sơ xe 360°">
          {l.bienSo}
        </Link>
      ),
    },
    {
      khoa: 'huong',
      ten: 'Hướng',
      hien: (l) => <span className={'badge ' + (l.huongDi === 'Vào' ? 'text-bg-success' : 'text-bg-warning')}>{l.huongDi}</span>,
    },
    {
      khoa: 'nguon',
      ten: 'Nguồn ghi',
      an: 'md',
      lop: 'small',
      hien: (l) => (
        <>
          {TEN_NGUON[l.nguon] || l.nguon || 'Không rõ'}
          {l.doTinCay != null && <span className="text-body-secondary"> · {dinhDangSo(l.doTinCay, 0)}%</span>}
        </>
      ),
    },
    { khoa: 'cong', ten: 'Cổng', an: 'lg', lop: 'small', hien: (l) => [l.cong, l.soCont].filter(Boolean).join(' · ') || '—' },
    {
      khoa: 'lyDo',
      ten: 'Vì sao AI nghi vấn',
      hien: (l) =>
        l.lyDo.length ? (
          <ul className="av-ly-do">
            {l.lyDo.map((x) => (
              <li key={x}>{x}</li>
            ))}
          </ul>
        ) : (
          <span className="small text-body-secondary">Trong phạm vi thường gặp</span>
        ),
    },
    {
      khoa: 'kl',
      ten: '',
      canPhai: true,
      hien: (l) => (l.nghiVan ? <NhanTrangThai mau="do">Hậu kiểm</NhanTrangThai> : <NhanTrangThai mau="off">Bình thường</NhanTrangThai>),
    },
  ]
}

/**
 * AI PHAT HIEN LUOT XE BAT THUONG (chong that thoat o cong): Isolation Forest — hoc khong giam sat, khong can nhan truoc.
 * So lieu: GET /api/ai-van-hanh/gian-lan?soNgay=&nguong=.
 */
export default function AiGianLan() {
  const [loc, datLoc] = useBoLoc({ soNgay: 90, nguong: 0.62, tatCa: false })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/ai-van-hanh/gian-lan' + taoQuery({ soNgay: loc.soNgay, nguong: loc.nguong }))

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const dong = loc.tatCa ? d.luot : d.luot.filter((l) => l.nghiVan)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <ThanhCongCu luc={dinhDangNgayGio(d.lucTinh)} dangTai={dangTai} lamMoi={taiLai}>
        <select className="form-select form-select-sm w-auto" aria-label="Khoảng thời gian" value={loc.soNgay} onChange={(e) => datLoc({ soNgay: Number(e.target.value) })}>
          {[7, 30, 90, 180, 365].map((x) => (
            <option key={x} value={x}>
              {x} ngày qua
            </option>
          ))}
        </select>
        <select className="form-select form-select-sm w-auto" aria-label="Ngưỡng điểm" value={loc.nguong} onChange={(e) => datLoc({ nguong: Number(e.target.value) })}>
          {NGUONG.map((x) => (
            <option key={x.v} value={x.v}>
              {x.t}
            </option>
          ))}
        </select>
      </ThanhCongCu>

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OChiSo mau="ngoc" so={dinhDangSo(d.soLuotPhanTich)} donVi="lượt" nhan="Lượt xe đã phân tích" phu={`${d.soNgay} ngày · ${d.soCay || 0} cây quyết định`} icon="bi-truck" />
        </div>
        <div className="col">
          <OChiSo mau={d.soNghiVan ? 'do' : 'la'} so={dinhDangSo(d.soNghiVan)} donVi="lượt" nhan="Cần hậu kiểm" phu={`điểm cô lập ≥ ${dinhDangSo(d.nguong, 2)}`} icon="bi-incognito" />
        </div>
        <div className="col">
          <OChiSo mau={d.soRaKhongVao ? 'cam' : 'la'} so={dinhDangSo(d.soRaKhongVao)} donVi="lượt" nhan="Xe RA không có lượt VÀO" phu="trong 48 giờ trước đó" icon="bi-box-arrow-right" />
        </div>
        <div className="col">
          <OChiSo mau="xam" so={dinhDangSo(d.soNhapTay)} donVi="lượt" nhan="Biển số nhập tay / nhập bù" phu="không qua camera nhận dạng" icon="bi-keyboard" />
        </div>
      </div>

      <div className="small text-body-secondary mb-3">{d.thongDiep}</div>

      <TheNoiDung
        tieuDe={loc.tatCa ? `${d.luot.length} lượt điểm cao nhất` : 'Lượt xe cần hậu kiểm'}
        icon="bi-shield-exclamation"
        khongDem
        phai={
          <div className="form-check form-switch mb-0 small">
            <input className="form-check-input" type="checkbox" role="switch" id="av-tat-ca" checked={loc.tatCa} onChange={(e) => datLoc({ tatCa: e.target.checked })} />
            <label className="form-check-label" htmlFor="av-tat-ca">
              Hiện cả lượt dưới ngưỡng
            </label>
          </div>
        }
        chan={<span className="small text-body-secondary">Bấm biển số để mở hồ sơ xe 360° (ảnh camera, lệnh, lịch sử ra vào) và đối chiếu.</span>}
      >
        <BangDuLieu
          cot={taoCot(d.nguong)}
          dong={dong}
          khoaDong="maXV"
          rong={d.soLuotPhanTich < 8 ? 'Chưa đủ lượt xe để học.' : 'Không có lượt nào vượt ngưỡng — bật "Hiện cả lượt dưới ngưỡng" để xem các lượt điểm cao nhất.'}
          lopDong={(l) => (l.nghiVan ? 'table-danger' : '')}
        />
      </TheNoiDung>

      <CachTinh>
        <p className="mb-1">
          <b>Vì sao không dùng luật cứng?</b> Luật if/else dễ bị lách và chỉ bắt được kiểu gian lận đã biết. <b>Rừng cô lập (Isolation Forest)</b> là học
          máy <i>không giám sát</i>: không cần ai gắn nhãn &quot;gian lận&quot; trước, nó tự học thế nào là lượt xe &quot;bình thường&quot; của chính cảng này.
        </p>
        <p className="mb-1">Mỗi lượt xe được mô tả bằng 9 con số:</p>
        <div className="av-cong-thuc">
          {'giờ qua cổng (sin, cos — 23h và 0h đứng cạnh nhau) · độ tin cậy OCR biển số · không có OCR\n' +
            'nhập tay / nhập bù · có lệnh giao nhận · RA mà không có VÀO trong 48 giờ\n' +
            'log(số phút ở trong cảng) · số lượt của cùng xe trong ngày'}
        </div>
        <p className="mb-1">
          150 cây, mỗi cây lấy ngẫu nhiên 64 lượt rồi cắt ngẫu nhiên từng đặc trưng. Lượt &quot;khác người&quot; bị tách riêng sau rất ít nhát cắt:
        </p>
        <div className="av-cong-thuc">{'điểm s(x) = 2^( −E[độ sâu h(x)] / c(64) )    s gần 1: bất thường · dưới 0,5: bình thường'}</div>
        <p className="mb-0">
          Hạt giống ngẫu nhiên cố định nên cùng dữ liệu luôn ra cùng kết quả (tái hiện được khi có khiếu nại). Mỗi lượt bị gắn cờ đều kèm <b>lý do đọc được</b>{' '}
          (so với phân bố chung) — bảo vệ biết ngay cần kiểm tra gì; AI chỉ khoanh vùng, người quyết định.
        </p>
      </CachTinh>
    </>
  )
}
