import { Link } from 'react-router-dom'
import { duongDanAnh } from '../../api/client'
import { HopThoai, NhanTrangThai } from '../../components/ui'
import { dinhDangNgayGio } from '../../utils/dinhDang'

// Thanh phan nho dung chung cho cac man hinh khu Cong & lenh giao nhan

export const CAC_CONG = ['A1', 'A2', 'B1']

/** Goi y loai xe — cung danh sach voi o nhap cua ban MVC (Views/Gate/Index.cshtml) */
export const LOAI_XE_GOI_Y = [
  'Đầu kéo container',
  'Xe tải thùng',
  'Rơ-moóc 20ft',
  'Rơ-moóc 40ft',
  'Xe bồn',
  'Xe container lạnh',
  'Xe nâng hàng',
  'Xe cẩu',
  'Xe ben',
  'Xe khách',
  'Ô tô con',
  'Xe máy',
  'Xe cứu hỏa',
  'Xe cứu thương',
  'Xe nội bộ cảng',
]

const LOAI_LENH_NGAN = { GiaoCont: 'Giao cont', HaCont: 'Hạ cont', TraRong: 'Trả rỗng', LayRong: 'Lấy rỗng' }
export const loaiLenhNgan = (ma, ten) => LOAI_LENH_NGAN[ma] || ten || ma || '—'

const MAU_LENH = { 'Chờ xe': 'warn', 'Đã vào cổng': 'run', 'Hoàn tất': 'ok', 'Quá hạn': 'do', Hủy: 'off' }
export const mauLenh = (trangThai) => MAU_LENH[trangThai] || 'info'

export const duongHoSoXe = (bienSo) => '/admin/gate/hosoxe?bienSo=' + encodeURIComponent(bienSo || '')
export const duongTraCuuCont = (soCont) => '/admin/containers/tracuu?so=' + encodeURIComponent(soCont || '')

const hai = (n) => String(n).padStart(2, '0')

/** "22:57:56 17/09" — giong cot thoi gian nhat ky cong cua ban MVC */
export function gioGiayNgay(v) {
  if (!v) return '—'
  const d = new Date(v)
  return `${hai(d.getHours())}:${hai(d.getMinutes())}:${hai(d.getSeconds())} ${hai(d.getDate())}/${hai(d.getMonth() + 1)}`
}

/** "08:00 – 12:00 18/09" */
export function khungGio(tu, den) {
  if (!tu) return '—'
  const a = new Date(tu)
  const b = new Date(den)
  const cungNgay = a.toDateString() === b.toDateString()
  const gio = (d) => `${hai(d.getHours())}:${hai(d.getMinutes())}`
  const ngay = (d) => `${hai(d.getDate())}/${hai(d.getMonth() + 1)}`
  return cungNgay ? `${gio(a)} – ${gio(b)} ${ngay(b)}` : `${gio(a)} ${ngay(a)} – ${gio(b)} ${ngay(b)}`
}

/** Bien so bam duoc -> ho so xe 360° */
export function BienSoXe({ bienSo, lienKet = true }) {
  if (!bienSo) return <span className="text-muted">—</span>
  if (!lienKet) return <span className="sp-bien-so">{bienSo}</span>
  return (
    <Link className="sp-bien-so text-decoration-none" to={duongHoSoXe(bienSo)} title={'Hồ sơ xe ' + bienSo}>
      {bienSo}
    </Link>
  )
}

/** So container bam duoc -> tra cuu container */
export function SoCont({ so, nho = true }) {
  if (!so) return <span className="text-muted">—</span>
  return (
    <Link className={'cg-ma text-decoration-none' + (nho ? ' small' : '')} to={duongTraCuuCont(so)} title={'Tra cứu container ' + so}>
      <i className="bi bi-box-seam me-1" />
      {so}
    </Link>
  )
}

export function NhanHuong({ huong }) {
  if (!huong) return <span className="text-muted">—</span>
  return (
    <NhanTrangThai giaTri={huong} mau={huong === 'Vào' ? 'ok' : 'warn'} icon={huong === 'Vào' ? 'bi-box-arrow-in-right' : 'bi-box-arrow-right'} />
  )
}

export function NhanLenh({ trangThai }) {
  return <NhanTrangThai giaTri={trangThai} mau={mauLenh(trangThai)} />
}

/** Anh nho cua luot xe; bam de mo anh lon */
export function AnhNho({ anh, bienSo, moAnh }) {
  if (!anh) return <span className="text-muted small">—</span>
  return (
    <button type="button" className="btn p-0 border-0" onClick={() => moAnh({ anh, bienSo })} title="Bấm để xem ảnh lớn">
      <img src={duongDanAnh(anh)} className="sp-anh-nho" alt={'Ảnh xe ' + (bienSo || '')} loading="lazy" />
    </button>
  )
}

/** Hop thoai xem anh xe kich thuoc lon + tai ve (anh = { anh, bienSo, luc }) */
export function HopAnhXe({ anh, dong }) {
  const src = anh ? duongDanAnh(anh.anh) : null
  return (
    <HopThoai
      mo={!!anh}
      dong={dong}
      kichThuoc="lg"
      icon="bi-camera"
      tieuDe={'Ảnh xe ' + (anh?.bienSo || '') + (anh?.luc ? ' · ' + dinhDangNgayGio(anh.luc) : '')}
      chan={
        <>
          <a className="btn btn-outline-primary" href={src || '#'} download={'xe_' + (anh?.bienSo || 'cong').replace(/[^A-Za-z0-9]/g, '') + '.jpg'}>
            <i className="bi bi-download me-1" />
            Tải ảnh về
          </a>
          <button type="button" className="btn btn-light" onClick={dong}>
            Đóng
          </button>
        </>
      }
    >
      {src && <img src={src} alt={'Ảnh xe ' + (anh?.bienSo || '')} className="cg-anh-lon" />}
    </HopThoai>
  )
}

/** The chi so bam duoc de loc nhanh (vd bam "Chờ xe" -> loc lenh cho xe) */
export function NutChiSo({ dangChon, bam, children, title }) {
  return (
    <button type="button" className={'cg-nut-chi-so' + (dangChon ? ' dang-chon' : '')} onClick={bam} title={title} aria-pressed={!!dangChon}>
      {children}
    </button>
  )
}
