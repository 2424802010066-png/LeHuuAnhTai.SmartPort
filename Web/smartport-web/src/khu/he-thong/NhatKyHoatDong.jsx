import { taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, OTimKiem, PhanTrang, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangSo } from '../../utils/dinhDang'
import './he-thong.css'

// Loai nhat ky (cot Loai trong CSDL) -> chu hien thi + mau nhan (Loi do, BaoMat vang, DangNhap xanh nhu ban MVC)
const LOAI = [
  { ma: '', ten: 'Tất cả' },
  { ma: 'DangNhap', ten: 'Đăng nhập', mau: 'info', icon: 'bi-box-arrow-in-right' },
  { ma: 'ThaoTac', ten: 'Thao tác', mau: 'off', icon: 'bi-pencil-square' },
  { ma: 'BaoMat', ten: 'Bảo mật', mau: 'warn', icon: 'bi-shield-lock' },
  { ma: 'Loi', ten: 'Lỗi', mau: 'do', icon: 'bi-bug' },
]
const THEO_MA = Object.fromEntries(LOAI.map((l) => [l.ma, l]))
const CO_TRANG = 30

const hai = (n) => String(n).padStart(2, '0')
/** "08:15:03 17/09" nhu cot THOI GIAN cua ban MVC */
const gioGiayNgay = (v) => {
  const d = new Date(v)
  return `${hai(d.getHours())}:${hai(d.getMinutes())}:${hai(d.getSeconds())} ${hai(d.getDate())}/${hai(d.getMonth() + 1)}`
}

const COT = [
  {
    khoa: 'thoiGian',
    ten: 'Thời gian',
    rong: 130,
    hien: (n) => (
      <span className="small text-nowrap" title={new Date(n.thoiGian).toLocaleString('vi-VN')}>
        {gioGiayNgay(n.thoiGian)}
      </span>
    ),
  },
  {
    khoa: 'loai',
    ten: 'Loại',
    rong: 110,
    hien: (n) => {
      const l = THEO_MA[n.loai]
      return (
        <NhanTrangThai mau={l?.mau || 'off'} icon={l?.icon}>
          {l?.ten || n.loai}
        </NhanTrangThai>
      )
    },
  },
  { khoa: 'taiKhoan', ten: 'Tài khoản', rong: 130, hien: (n) => <span className="small fw-semibold">{n.taiKhoan || '—'}</span> },
  { khoa: 'noiDung', ten: 'Nội dung', lop: 'ht-noi-dung-nk', hien: (n) => n.noiDung },
  { khoa: 'ip', ten: 'IP', rong: 120, an: 'md', hien: (n) => <span className="small text-body-secondary">{n.diaChiIP || '—'}</span> },
]

/** Nhat ky hoat dong (audit log): loc loai + tim + phan trang 30 dong (thay Admin/System/NhatKy). */
export default function NhatKyHoatDong() {
  const [loc, datLoc] = useBoLoc({ loai: '', q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/he-thong/nhat-ky' + taoQuery({ ...loc, coTrang: CO_TRANG }))

  const soCuaLoai = (ma) => (ma ? d?.soTheoLoai?.[ma] || 0 : d?.tongTatCa || 0)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe={
          <>
            Nhật ký hoạt động
            {d && <span className="badge text-bg-secondary ms-2">{dinhDangSo(d.tong)} bản ghi</span>}
          </>
        }
        icon="bi-journal-text"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Tài khoản, nội dung, IP…" />
            <button type="button" className="btn btn-sm btn-outline-secondary" title="Làm mới" aria-label="Làm mới" onClick={() => taiLai()}>
              <i className="bi bi-arrow-clockwise" />
            </button>
          </>
        }
      >
        <div className="d-flex flex-wrap gap-1 px-3 py-2 border-bottom" role="group" aria-label="Lọc theo loại">
          {LOAI.map((l) => {
            const dangChon = loc.loai === l.ma
            return (
              <button
                key={l.ma || 'tat-ca'}
                type="button"
                className={'btn btn-sm ' + (dangChon ? 'btn-primary' : 'btn-outline-secondary')}
                aria-pressed={dangChon}
                onClick={() => datLoc({ loai: l.ma, trang: 1 })}
              >
                {l.ten}
                {d && <span className={'badge ms-1 ' + (dangChon ? 'text-bg-light' : 'text-bg-secondary')}>{dinhDangSo(soCuaLoai(l.ma))}</span>}
              </button>
            )
          })}
        </div>
        <BangDuLieu
          cot={COT}
          dong={d?.items}
          khoaDong="maNK"
          dangTai={dangTai}
          rong={loc.loai || loc.q ? 'Chưa có bản ghi nào khớp bộ lọc.' : 'Chưa có bản ghi nào.'}
        />
        {d && (
          <div className="px-3 pb-3">
            <PhanTrang trang={d.trang} soTrang={d.soTrang} tong={d.tong} donVi="bản ghi" doiTrang={(t) => datLoc({ trang: t })} />
          </div>
        )}
      </TheNoiDung>
    </>
  )
}
