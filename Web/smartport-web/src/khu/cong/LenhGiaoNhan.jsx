import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo } from '../../utils/dinhDang'
import { BienSoXe, NhanLenh, NutChiSo, SoCont, khungGio, loaiLenhNgan } from './dungChung'
import './cong.css'

const TRANG_THAI = ['Chờ xe', 'Đã vào cổng', 'Hoàn tất', 'Quá hạn', 'Hủy']

/**
 * LENH GIAO NHAN DIEN TU (eGate Pass) — thay Admin/GatePass/Index:
 * dang ky truoc xe / container / khung gio -> cap SO LENH + MA PIN, camera cong tu doi chieu.
 */
export default function LenhGiaoNhan() {
  const [loc, datLoc] = useBoLoc({ q: '', trangThai: '', trang: 1, coTrang: 20 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/lenh-giao-nhan' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  // Xe qua cong doi trang thai lenh (Cho xe -> Da vao cong -> Hoan tat) -> lam moi ngam
  useNgheSuKien('cong.*', () => taiLai({ im: true }))

  async function huy(l) {
    const dongY = await xacNhan({
      tieuDe: 'Hủy lệnh ' + l.soLenh + '?',
      noiDung: `Lệnh ${l.soLenh} của xe ${l.bienSoXe} sẽ chuyển sang trạng thái Hủy — xe không qua cổng bằng lệnh này được nữa.`,
      nutDongY: 'Hủy lệnh',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/lenh-giao-nhan/' + l.maLenh + '/huy', { method: 'POST' }))) taiLai({ im: true })
  }

  const locTrangThai = (tt) => datLoc({ trangThai: loc.trangThai === tt ? '' : tt, trang: 1 })

  const COT = [
    {
      khoa: 'so',
      ten: 'Số lệnh',
      hien: (l) => (
        <Link className="cg-ma text-decoration-none" to={'/admin/gatepass/in/' + l.maLenh} title="Mở bản in lệnh">
          {l.soLenh}
        </Link>
      ),
    },
    { khoa: 'loai', ten: 'Loại', hien: (l) => <span className="small">{loaiLenhNgan(l.loaiLenh, l.tenLoaiLenh)}</span> },
    { khoa: 'xe', ten: 'Biển số', hien: (l) => <BienSoXe bienSo={l.bienSoXe} /> },
    { khoa: 'cont', ten: 'Container', hien: (l) => <SoCont so={l.soCont} /> },
    { khoa: 'tx', ten: 'Tài xế', an: 'lg', hien: (l) => <span className="small">{l.tenTaiXe || '—'}</span> },
    { khoa: 'hen', ten: 'Khung giờ hẹn', an: 'md', hien: (l) => <span className="small">{khungGio(l.henTuLuc, l.henDenLuc)}</span> },
    { khoa: 'pin', ten: 'PIN', an: 'xl', hien: (l) => <span className="cg-ma-mo small">{l.maPin}</span> },
    { khoa: 'tt', ten: 'Trạng thái', hien: (l) => <NhanLenh trangThai={l.trangThai} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 132,
      hien: (l) => (
        <>
          <Link className="btn btn-sm btn-outline-secondary me-1" to={'/admin/gatepass/in/' + l.maLenh} title="In lệnh">
            <i className="bi bi-printer" />
          </Link>
          <Link className="btn btn-sm btn-outline-primary me-1" to={'/admin/gatepass/form/' + l.maLenh} title="Sửa lệnh">
            <i className="bi bi-pencil" />
          </Link>
          {(l.trangThai === 'Chờ xe' || l.trangThai === 'Quá hạn') && (
            <button type="button" className="btn btn-sm btn-outline-danger" title="Hủy lệnh" disabled={dangChay} onClick={() => huy(l)}>
              <i className="bi bi-x-lg" />
            </button>
          )}
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <NutChiSo dangChon={loc.trangThai === 'Chờ xe'} bam={() => locTrangThai('Chờ xe')} title="Lọc lệnh đang chờ xe">
            <TheChiSo icon="bi-hourglass-split" mau="cam" so={dinhDangSo(d?.choXe ?? 0)} nhan="lệnh đang chờ xe" />
          </NutChiSo>
        </div>
        <div className="col-6 col-lg-3">
          <NutChiSo dangChon={loc.trangThai === 'Đã vào cổng'} bam={() => locTrangThai('Đã vào cổng')} title="Lọc lệnh xe đã vào cổng">
            <TheChiSo icon="bi-truck" mau="xanh" so={dinhDangSo(d?.daVao ?? 0)} nhan="xe đã vào cổng" />
          </NutChiSo>
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-calendar-check" mau="la" so={dinhDangSo(d?.homNay ?? 0)} nhan="lệnh tạo hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <NutChiSo dangChon={loc.trangThai === 'Quá hạn'} bam={() => locTrangThai('Quá hạn')} title="Lọc lệnh quá hạn">
            <TheChiSo icon="bi-exclamation-triangle" mau="do" so={dinhDangSo(d?.quaHan ?? 0)} nhan="lệnh quá hạn" />
          </NutChiSo>
        </div>
      </div>

      <TheNoiDung
        tieuDe="Lệnh giao nhận điện tử"
        icon="bi-qr-code-scan"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(v) => datLoc({ q: v, trang: 1 })} goiY="Số lệnh, biển số, container, tài xế…" />
            <select
              className="form-select form-select-sm"
              style={{ maxWidth: 160 }}
              value={loc.trangThai}
              aria-label="Lọc theo trạng thái"
              onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
            >
              <option value="">— Mọi trạng thái —</option>
              {TRANG_THAI.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
            <Link className="btn btn-sm btn-primary" to="/admin/gatepass/form">
              <i className="bi bi-plus-lg me-1" />
              Tạo lệnh mới
            </Link>
          </>
        }
        chan={<PhanTrang trang={d?.trang} soTrang={d?.soTrang} tong={d?.tong} donVi="lệnh" doiTrang={(t) => datLoc({ trang: t })} />}
      >
        <BangDuLieu
          cot={COT}
          dong={d?.items}
          khoaDong="maLenh"
          dangTai={dangTai}
          rong={loc.q || loc.trangThai ? 'Không có lệnh nào khớp bộ lọc.' : 'Chưa có lệnh nào — bấm "Tạo lệnh mới".'}
        />
      </TheNoiDung>
    </>
  )
}
