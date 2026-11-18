import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import './tai-xe.css'

const TINH_TRANG = [
  { gt: '', ten: 'Mọi tình trạng GPLX' },
  { gt: 'het-han', ten: 'GPLX hết hạn' },
  { gt: 'sap-het-han', ten: 'GPLX sắp hết hạn' },
]

/** O bang lai: so · hang + han (do = het han, cam = con <= 30 ngay) */
function OBangLai({ t }) {
  if (!t.soGPLX && !t.hanGPLX) return <span className="small text-body-secondary">chưa có</span>
  return (
    <div className="small">
      <div>
        {t.soGPLX || '—'}
        {t.hangGPLX && (
          <>
            {' '}
            · hạng <b>{t.hangGPLX}</b>
          </>
        )}
      </div>
      {t.hanGPLX &&
        (t.gplxHetHan ? (
          <span className="text-danger-emphasis fw-bold">
            <i className="bi bi-x-octagon-fill me-1" />
            hết hạn {dinhDangNgay(t.hanGPLX)}
          </span>
        ) : t.gplxSapHetHan ? (
          <span className="text-warning-emphasis fw-bold">
            <i className="bi bi-exclamation-triangle-fill me-1" />
            sắp hết hạn {dinhDangNgay(t.hanGPLX)}
          </span>
        ) : (
          <span className="text-body-secondary">hạn {dinhDangNgay(t.hanGPLX)}</span>
        ))}
    </div>
  )
}

/** The chi so bam de loc nhanh (bam lai lan nua de bo loc) */
function TheLoc({ chon, bam, ...the }) {
  return (
    <button type="button" className={'tx-the-bam' + (chon ? ' tx-dang-chon' : '')} aria-pressed={chon} onClick={bam}>
      <TheChiSo {...the} />
    </button>
  )
}

/** Ho so tai xe: danh sach + tim + loc GPLX, xoa (thay Admin/TaiXe/Index cua ban MVC). */
export default function DanhSachTaiXe() {
  // bienSo: link "Hồ sơ xe · tài xế" do trợ lý sinh ra (/admin/taixe?bienSo=...)
  const [loc, datLoc] = useBoLoc({ q: '', bienSo: '', tinhTrang: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/ho-so-tai-xe' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  const trang = d?.danhSach
  const dangLoc = !!(loc.q || loc.bienSo || loc.tinhTrang)
  const locTinhTrang = (tt) => datLoc({ tinhTrang: loc.tinhTrang === tt ? '' : tt, trang: 1 })

  async function xoa(t) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa hồ sơ tài xế?',
      noiDung: `Xóa hồ sơ của ${t.hoTen || t.soGiayTo}. Các lượt xe đã gắn tài xế này sẽ được gỡ liên kết.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/ho-so-tai-xe/' + t.maTX, { method: 'DELETE' }))) {
      if (trang?.items.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      else taiLai({ im: true })
    }
  }

  const COT = [
    {
      khoa: 'taiXe',
      ten: 'Tài xế',
      hien: (t) => (
        <>
          <div className="fw-semibold">{t.hoTen || '—'}</div>
          <div className="small text-body-secondary">{[t.gioiTinh, t.tuoi != null ? t.tuoi + ' tuổi' : null, t.dienThoai].filter(Boolean).join(' · ')}</div>
          {t.diaChi && <div className="small text-body-secondary tx-dia-chi d-none d-lg-block">{t.diaChi}</div>}
        </>
      ),
    },
    { khoa: 'cccd', ten: 'CCCD', hien: (t) => <span className="small tx-so-giay-to">{t.soGiayTo}</span> },
    { khoa: 'gplx', ten: 'Bằng lái', hien: (t) => <OBangLai t={t} /> },
    {
      khoa: 'xe',
      ten: 'Xe',
      an: 'md',
      hien: (t) =>
        t.bienSo ? (
          <Link className="sp-bien-so text-decoration-none" to={'/admin/gate/hosoxe?bienSo=' + encodeURIComponent(t.bienSo)} title="Hồ sơ xe">
            {t.bienSo}
          </Link>
        ) : (
          '—'
        ),
    },
    {
      khoa: 'luot',
      ten: 'Lượt vào',
      canPhai: true,
      hien: (t) => (
        <>
          <div className="fw-bold">{dinhDangSo(t.soLanVao)}</div>
          {t.lanVaoCuoi && <div className="small text-body-secondary text-nowrap">{gioNgayNgan(t.lanVaoCuoi)}</div>}
        </>
      ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 100,
      hien: (t) => (
        <span className="text-nowrap">
          <Link className="btn btn-sm btn-outline-primary me-1" to={'/admin/taixe/quet?maTX=' + t.maTX} title="Cập nhật hồ sơ" aria-label="Cập nhật hồ sơ">
            <i className="bi bi-pencil" />
          </Link>
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa hồ sơ" aria-label="Xóa hồ sơ" disabled={dangChay} onClick={() => xoa(t)}>
            <i className="bi bi-trash" />
          </button>
        </span>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-12 col-sm-4">
          <TheLoc
            chon={!loc.tinhTrang}
            bam={() => datLoc({ tinhTrang: '', trang: 1 })}
            icon="bi-person-vcard"
            mau="xanh"
            so={dinhDangSo(d?.tongHoSo)}
            nhan="Hồ sơ tài xế đã đăng ký"
          />
        </div>
        <div className="col-6 col-sm-4">
          <TheLoc
            chon={loc.tinhTrang === 'het-han'}
            bam={() => locTinhTrang('het-han')}
            icon="bi-x-octagon"
            mau={d?.soHetHan > 0 ? 'do' : 'la'}
            so={dinhDangSo(d?.soHetHan)}
            nhan="GPLX hết hạn · không được lái"
          />
        </div>
        <div className="col-6 col-sm-4">
          <TheLoc
            chon={loc.tinhTrang === 'sap-het-han'}
            bam={() => locTinhTrang('sap-het-han')}
            icon="bi-hourglass-split"
            mau="cam"
            so={dinhDangSo(d?.soSapHetHan)}
            nhan="GPLX sắp hết hạn · 30 ngày tới"
          />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Danh sách tài xế"
        icon="bi-person-vcard"
        khongDem
        phai={
          <>
            {loc.bienSo && (
              <button type="button" className="btn btn-sm btn-outline-secondary" title="Bỏ lọc biển số" onClick={() => datLoc({ bienSo: '', trang: 1 })}>
                <span className="sp-bien-so me-1">{loc.bienSo}</span>
                <i className="bi bi-x-lg" />
              </button>
            )}
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Họ tên, số CCCD, biển số, số GPLX…" />
            <select
              className="form-select form-select-sm w-auto"
              aria-label="Tình trạng GPLX"
              value={loc.tinhTrang}
              onChange={(e) => datLoc({ tinhTrang: e.target.value, trang: 1 })}
            >
              {TINH_TRANG.map((x) => (
                <option key={x.gt} value={x.gt}>
                  {x.ten}
                </option>
              ))}
            </select>
            <Link className="btn btn-sm btn-primary" to="/admin/taixe/quet">
              <i className="bi bi-qr-code-scan me-1" />
              Quét CCCD đăng ký
            </Link>
          </>
        }
        chan={
          <>
            <PhanTrang {...trang} doiTrang={(t) => datLoc({ trang: t })} donVi="tài xế" />
            <div className="small text-body-secondary mt-1">
              <i className="bi bi-shield-lock me-1" />
              Không lưu ảnh CCCD — chỉ giữ các trường cần cho việc ra vào cổng.
            </div>
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={trang?.items}
          khoaDong="maTX"
          dangTai={dangTai}
          lopDong={(t) => (t.gplxHetHan ? 'table-danger' : '')}
          rong={
            dangLoc ? (
              'Không có hồ sơ tài xế khớp bộ lọc.'
            ) : (
              <>
                Chưa có hồ sơ tài xế nào — bấm <b>Quét CCCD đăng ký</b> để tạo hồ sơ đầu tiên.
              </>
            )
          }
        />
      </TheNoiDung>
    </>
  )
}
