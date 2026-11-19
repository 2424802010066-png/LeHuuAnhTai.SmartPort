import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { duongDanAnh, goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, HopThoai, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { gioNgayNgan } from '../../utils/dinhDang'
import './an-ninh.css'
import { chuongBao } from './anNinhAi'

// Toa do cac khu tren so do cang (SVG 700x300) — giu nguyen ban MVC
const KHU = [
  { ten: 'Cổng chính', x: 90, y: 235 },
  { ten: 'Cổng phụ', x: 250, y: 245 },
  { ten: 'Bãi container', x: 300, y: 130 },
  { ten: 'Cầu bến', x: 520, y: 60 },
  { ten: 'Kho CFS', x: 555, y: 210 },
]

const LOC_RONG = { trangThai: '', mucDo: '', viTri: '', trang: 1, coTrang: 30 }
const NHIP_HOI = 8000 // hoi lai moi 8 giay (dung khi tab an)

/** So do cang: cham do nhap nhay o khu dang co su co CHUA xu ly */
function SoDoCang({ khuNong }) {
  return (
    <svg viewBox="0 0 700 300" className="an-so-do" role="img" aria-label="Sơ đồ cảng với vị trí đang gặp sự cố">
      <rect x="0" y="0" width="700" height="84" fill="#0e5a9e" />
      <rect x="0" y="84" width="700" height="216" fill="#8a939b" />
      <rect x="380" y="70" width="290" height="26" rx="4" fill="#4b5a66" />
      <text x="520" y="45" textAnchor="middle" fontSize="15" fill="#cfe8ff">
        CẦU BẾN
      </text>
      <rect x="180" y="108" width="240" height="64" rx="6" fill="#37424b" />
      <rect x="192" y="116" width="42" height="20" fill="#e5691d" />
      <rect x="240" y="116" width="42" height="20" fill="#1c5fa8" />
      <rect x="288" y="116" width="42" height="20" fill="#15803d" />
      <rect x="336" y="116" width="42" height="20" fill="#7c3aed" />
      <rect x="192" y="142" width="42" height="20" fill="#0891b2" />
      <rect x="240" y="142" width="42" height="20" fill="#e5691d" />
      <rect x="288" y="142" width="42" height="20" fill="#b45309" />
      <rect x="336" y="142" width="42" height="20" fill="#1c5fa8" />
      <text x="300" y="196" textAnchor="middle" fontSize="14" fill="#e8eef4">
        BÃI CONTAINER
      </text>
      <rect x="480" y="170" width="150" height="70" rx="8" fill="#5d6a75" />
      <text x="555" y="262" textAnchor="middle" fontSize="13" fill="#e8eef4">
        KHO CFS
      </text>
      <rect x="40" y="210" width="100" height="50" rx="8" fill="#0a2f57" />
      <text x="90" y="285" textAnchor="middle" fontSize="13" fill="#e8eef4">
        CỔNG CHÍNH
      </text>
      <rect x="205" y="222" width="90" height="42" rx="8" fill="#123c66" />
      <text x="250" y="288" textAnchor="middle" fontSize="12" fill="#e8eef4">
        CỔNG PHỤ
      </text>
      {KHU.filter((k) => khuNong.includes(k.ten)).map((k) => (
        <g key={k.ten}>
          <circle cx={k.x} cy={k.y} r="16" fill="none" stroke="#ef4444" strokeWidth="3">
            <animate attributeName="r" values="10;22;10" dur="1.6s" repeatCount="indefinite" />
            <animate attributeName="opacity" values=".9;.1;.9" dur="1.6s" repeatCount="indefinite" />
          </circle>
          <circle cx={k.x} cy={k.y} r="8" fill="#ef4444" />
        </g>
      ))}
    </svg>
  )
}

/** Mot the su co trong danh sach ben phai */
function TheSuCo({ s, dangChay, xemAnh, xuLy, lapSuCo }) {
  const moi = s.trangThai === 'Mới'
  return (
    <div className={'an-su-co' + (moi ? ' moi' : '')}>
      <div className="d-flex justify-content-between align-items-start gap-2">
        <div>
          <span className={'badge ' + (s.mucDo === 'Nguy hiểm' ? 'text-bg-danger' : 'text-bg-warning')}>{s.mucDo}</span>
          <b className="ms-1">{s.tenLoai}</b>
          <span className="badge text-bg-secondary ms-1">
            <i className="bi bi-geo-alt me-1" />
            {s.viTri}
          </span>
        </div>
        <span className="small text-body-secondary text-nowrap">{gioNgayNgan(s.thoiGian)}</span>
      </div>
      {s.moTa && <div className="small mt-1">{s.moTa}</div>}
      <div className="d-flex gap-2 mt-2 align-items-center flex-wrap">
        {s.coAnh && (
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => xemAnh(s)}>
            <i className="bi bi-image me-1" />
            Xem ảnh
          </button>
        )}
        {s.video && (
          <a className="btn btn-sm btn-outline-primary" target="_blank" rel="noreferrer" href={duongDanAnh(s.video)}>
            <i className="bi bi-play-circle me-1" />
            Video
          </a>
        )}
        {/* UC23: camera AI -> lap SU CO chung de theo doi tiep nhan / ket qua / dong (QT14 neu nguy hiem) */}
        <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangChay} onClick={() => lapSuCo(s)} title="Chuyển cảnh báo này thành sự cố để xử lý tới khi đóng">
          <i className="bi bi-journal-plus me-1" />
          Lập sự cố
        </button>
        {moi ? (
          <button type="button" className="btn btn-sm btn-success" disabled={dangChay} onClick={() => xuLy(s)}>
            <i className="bi bi-check2 me-1" />
            Đã xử lý
          </button>
        ) : (
          <span className="small text-success">
            <i className="bi bi-check-circle me-1" />
            {s.nguoiXuLy || '—'}
            {s.thoiGianXuLy ? ' · ' + gioNgayNgan(s.thoiGianXuLy) : ''}
          </span>
        )}
      </div>
    </div>
  )
}

/**
 * PHONG AN NINH (thay Admin/Security/Index cua ban MVC): sự cố do camera AI phát hiện
 * kèm ảnh, video, vị trí; sơ đồ cảng đánh dấu điểm nóng; xác nhận đã xử lý.
 * Màn hình trực ban nên tự hỏi lại máy chủ mỗi 8 giây (dừng hẳn khi tab bị ẩn).
 */
export default function PhongAnNinh() {
  const [loc, datLoc] = useBoLoc(LOC_RONG)
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/an-ninh/su-co' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const dieuHuong = useNavigate()

  const [nhip, setNhip] = useState(null) // { soMoi, khuNong, luc } — ket qua hoi nhe
  const [anh, setAnh] = useState(null) // { maSC, src } dang xem
  const [dangTaiAnh, setDangTaiAnh] = useState(false)
  const soMoiTruoc = useRef(null)
  const dinhTruoc = useRef(null)

  const soMoi = nhip?.soMoi ?? duLieu?.suCoMoi ?? 0
  const khuNong = nhip?.khuNong ?? duLieu?.khuNong ?? []

  // Hoi nhe 8 giay/lan: chi lay 30 su co gan nhat + so chua xu ly + khu nong.
  // Co su co MOI do ve -> keu chuong va tai lai danh sach dang loc (lam moi ngam).
  const hoi = useCallback(async () => {
    try {
      const kq = await goiApi('/api/an-ninh/su-co/gan-day')
      setNhip({ soMoi: kq.soMoi, khuNong: kq.khuNong || [], luc: new Date() })
      const dinh = kq.ds?.[0]?.maSC ?? 0
      const coMoi = soMoiTruoc.current !== null && kq.soMoi > soMoiTruoc.current
      const doiDinh = dinhTruoc.current !== null && dinh !== dinhTruoc.current
      soMoiTruoc.current = kq.soMoi
      dinhTruoc.current = dinh
      if (coMoi) chuongBao()
      if (coMoi || doiDinh) taiLai({ im: true })
    } catch {
      /* mat mang tam thoi — nhip sau hoi lai */
    }
  }, [taiLai])

  useEffect(() => {
    let hen = null
    const nhipTiep = () => {
      if (!document.hidden) hoi()
      hen = setTimeout(nhipTiep, NHIP_HOI)
    }
    const khiHien = () => {
      if (!document.hidden) hoi()
    }
    nhipTiep()
    document.addEventListener('visibilitychange', khiHien)
    return () => {
      clearTimeout(hen)
      document.removeEventListener('visibilitychange', khiHien)
    }
  }, [hoi])

  async function xemAnh(s) {
    setAnh({ maSC: s.maSC, src: null })
    setDangTaiAnh(true)
    try {
      const kq = await goiApi('/api/an-ninh/su-co/' + s.maSC + '/anh')
      setAnh({ maSC: s.maSC, src: kq.anh })
    } catch {
      setAnh({ maSC: s.maSC, src: null, loi: 'Không lấy được ảnh bằng chứng của sự cố này.' })
    } finally {
      setDangTaiAnh(false)
    }
  }

  async function xuLy(s) {
    if (await chay(() => goiApi('/api/an-ninh/su-co/' + s.maSC + '/xu-ly', { method: 'POST' }))) {
      soMoiTruoc.current = null // vua tu xu ly: khong keu chuong o nhip ke tiep
      taiLai({ im: true })
      hoi()
    }
  }

  // UC23: chuyen canh bao camera AI thanh su co chung (/api/su-co, maSCAnNinh) roi mo man hinh Quan ly su co
  async function lapSuCo(s) {
    const kq = await chay(() =>
      goiApi('/api/su-co', {
        method: 'POST',
        body: {
          loai: 'AnNinhAnToan',
          mucDo: s.mucDo === 'Nguy hiểm' ? 'NguyHiem' : 'TrungBinh',
          tieuDe: (s.tenLoai + ' — ' + s.viTri).slice(0, 150),
          moTa: ((s.moTa || s.tenLoai) + ' (chuyển từ cảnh báo camera AI #' + s.maSC + ')').slice(0, 1000),
          viTri: s.viTri,
          maSCAnNinh: s.maSC,
        },
      }),
    )
    if (kq?.id) dieuHuong('/admin/su-co?id=' + kq.id)
  }

  const nutLoc = (khoa, gt, nhan) => (
    <button
      type="button"
      className={'btn btn-sm ' + (loc[khoa] === gt ? 'btn-primary' : 'btn-outline-secondary')}
      onClick={() => datLoc({ [khoa]: gt, trang: 1 })}
    >
      {nhan}
    </button>
  )

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-exclamation-octagon" mau="do" so={soMoi} nhan="Sự cố CHƯA xử lý" />
        </div>
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-fire" mau="cam" so={duLieu?.nguyHiemHomNay ?? 0} nhan="Mức NGUY HIỂM hôm nay" />
        </div>
        <div className="col-md-4 col-12">
          <TheChiSo icon="bi-check-circle" mau="la" so={duLieu?.daXuLy ?? 0} nhan="Đã xử lý (100 gần nhất)" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung tieuDe="Sơ đồ cảng — vị trí đang gặp sự cố" icon="bi-geo-alt">
            <SoDoCang khuNong={khuNong} />
            <div className="small text-body-secondary mt-2">
              Chấm đỏ nhấp nháy = khu vực có sự cố <b>chưa xử lý</b>.
            </div>
            <div className="d-flex flex-wrap gap-1 mt-2">
              {nutLoc('viTri', '', 'Mọi khu')}
              {KHU.map((k) => (
                <span key={k.ten}>{nutLoc('viTri', k.ten, k.ten)}</span>
              ))}
            </div>
            <Link className="btn btn-primary w-100 mt-3" to="/admin/security/monitor">
              <i className="bi bi-camera-video me-1" />
              Mở trạm camera AI
            </Link>
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung
            tieuDe="Sự cố mới nhất"
            icon="bi-bell"
            phai={
              <span className="small text-body-secondary">
                <i className="bi bi-arrow-repeat me-1" />
                tự làm mới 8 giây · {nhip?.luc ? nhip.luc.toLocaleTimeString('vi-VN') : '—'}
              </span>
            }
          >
            <div className="d-flex flex-wrap gap-1 mb-3">
              {nutLoc('trangThai', '', 'Tất cả')}
              {nutLoc('trangThai', 'moi', 'Chưa xử lý')}
              {nutLoc('trangThai', 'da-xu-ly', 'Đã xử lý')}
              <span className="vr mx-1" />
              {nutLoc('mucDo', '', 'Mọi mức')}
              {nutLoc('mucDo', 'nguy-hiem', 'Nguy hiểm')}
              {nutLoc('mucDo', 'chu-y', 'Chú ý')}
            </div>

            <div className="an-danh-sach">
              {dangTai && !duLieu ? (
                <div className="text-center text-body-secondary py-4">
                  <span className="spinner-border spinner-border-sm me-2" />
                  Đang tải…
                </div>
              ) : !duLieu?.items?.length ? (
                <div className="text-center text-body-secondary py-5">
                  <i className="bi bi-shield-check d-block mb-2" style={{ fontSize: '2.4rem' }} />
                  Không có sự cố nào khớp bộ lọc.
                </div>
              ) : (
                duLieu.items.map((s) => <TheSuCo key={s.maSC} s={s} dangChay={dangChay} xemAnh={xemAnh} xuLy={xuLy} lapSuCo={lapSuCo} />)
              )}
            </div>

            <PhanTrang {...(duLieu || {})} donVi="sự cố" doiTrang={(t) => datLoc({ trang: t })} />
          </TheNoiDung>
        </div>
      </div>

      <HopThoai mo={!!anh} dong={() => setAnh(null)} tieuDe={'Ảnh bằng chứng · sự cố #' + (anh?.maSC ?? '')} icon="bi-image" kichThuoc="lg">
        {dangTaiAnh ? (
          <div className="text-center text-body-secondary py-4">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải ảnh…
          </div>
        ) : anh?.src ? (
          <img src={anh.src} alt={'Ảnh bằng chứng sự cố ' + anh.maSC} className="an-anh-lon" />
        ) : (
          <div className="alert alert-warning mb-0">{anh?.loi || 'Sự cố này không có ảnh bằng chứng.'}</div>
        )}
      </HopThoai>
    </>
  )
}
