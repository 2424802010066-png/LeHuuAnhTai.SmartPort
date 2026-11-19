import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { useAuth } from '../../auth/AuthContext'
import { BangDuLieu, BaoLoi, DangTai, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan, ngayInput, thoiGianTruoc } from '../../utils/dinhDang'
import BieuMauSuCo from './BieuMauSuCo'
import { iconLoai, LOAI_SU_CO, mauMucDo, mauTrangThaiSuCo, MUC_DO, thoiLuongPhut, TRANG_THAI } from './suCo'
import './su-co.css'

const TAT_CA_LOAI = LOAI_SU_CO.map((l) => l.ma)
const LOC_RONG = { loai: '', mucDo: '', trangThai: '', tuKhoa: '', tuNgay: '', denNgay: '', chiDangMo: false, tamGiu: false, trang: 1, id: 0 }
const MAU_THANH_MUC = { Thap: 'xam', TrungBinh: 'cam', NguyHiem: 'do' }
const MAU_THANH_TT = { Moi: 'ngoc', DangXuLy: 'cam', DaXuLy: 'la', Dong: 'xam', BaoGia: 'xam' }

/** Lam moi NGAM khi co su kien su co (nguoi khac vua bao / tiep nhan / dong) — gop nhieu su kien sat nhau */
function useLamMoiSuCo(lamMoi) {
  const hen = useRef(null)
  useEffect(() => () => clearTimeout(hen.current), [])
  useNgheSuKien('an-ninh.su-co-chung-*', () => {
    clearTimeout(hen.current)
    hen.current = setTimeout(lamMoi, 600)
  })
}

/** Mot nhom thanh ngang trong khoi thong ke */
function NhomThongKe({ tieuDe, ds, mau }) {
  const lonNhat = Math.max(1, ...(ds || []).map((x) => x.so))
  return (
    <div className="sc-tk-nhom">
      <h6>{tieuDe}</h6>
      {(ds || []).map((x) => (
        <div className="sc-tk-dong" key={x.ma}>
          <span className="text-truncate" title={x.ten}>
            {x.ten}
          </span>
          <div className={'sc-tk-thanh ' + (mau?.[x.ma] || '')} title={`${x.so} sự cố`}>
            <span style={{ width: x.so ? Math.max(4, Math.round((x.so / lonNhat) * 100)) + '%' : 0 }} />
          </div>
          <span className="sc-tk-so">{x.so}</span>
        </div>
      ))}
    </div>
  )
}

/** UC28 — chi so hien tai + thong ke theo khoang ngay */
function ThongKeSuCo({ tk, tu, den, setTu, setDen, loi, taiLai }) {
  const tiLeQt14 = tk?.soNguyHiemDaTiepNhan ? Math.round((tk.soNguyHiemDatQT14 / tk.soNguyHiemDaTiepNhan) * 100) : null
  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3" id="sc-chi-so">
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo icon="bi-folder2-open" mau="xanh" so={dinhDangSo(tk?.dangMo)} nhan="Sự cố đang mở" />
        </div>
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo
            icon="bi-exclamation-octagon"
            mau="do"
            so={dinhDangSo(tk?.nguyHiemDangMo)}
            nhan={`Nguy hiểm đang mở · ${tk?.nguyHiemChuaTiepNhan ?? 0} chưa tiếp nhận`}
          />
        </div>
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo icon="bi-stopwatch" mau="cam" so={thoiLuongPhut(tk?.tbPhutTiepNhan)} nhan="Phản hồi TB (báo → tiếp nhận)" nho />
        </div>
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo icon="bi-check2-circle" mau="la" so={thoiLuongPhut(tk?.tbPhutDong)} nhan="Xử lý TB (báo → đóng)" nho />
        </div>
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo icon="bi-lock" mau="tim" so={dinhDangSo(tk?.dangTamGiu)} nhan="Container đang tạm giữ (QT15)" />
        </div>
        <div className="col-6 col-md-4 col-xl-2">
          <TheChiSo icon="bi-alarm" mau={tk?.treHan ? 'do' : 'xanh-ngoc'} so={dinhDangSo(tk?.treHan)} nhan={`Trễ hạn · ${tk?.khachChuaTraLoi ?? 0} phản ánh chưa trả lời`} />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Thống kê sự cố (UC28)"
        icon="bi-bar-chart-line"
        phai={
          <div className="d-flex align-items-center gap-1 flex-wrap">
            <label className="small text-body-secondary" htmlFor="sc-tk-tu">
              Từ
            </label>
            <input id="sc-tk-tu" type="date" className="form-control form-control-sm w-auto" value={tu} max={den} onChange={(e) => setTu(e.target.value)} />
            <label className="small text-body-secondary" htmlFor="sc-tk-den">
              đến
            </label>
            <input id="sc-tk-den" type="date" className="form-control form-control-sm w-auto" value={den} min={tu} onChange={(e) => setDen(e.target.value)} />
          </div>
        }
      >
        <div className="row g-4" id="sc-thong-ke">
          <div className="col-md-6 col-xl-4">
            <NhomThongKe tieuDe={`Theo loại · ${tk?.tong ?? 0} sự cố`} ds={tk?.theoLoai} />
          </div>
          <div className="col-md-6 col-xl-4">
            <NhomThongKe tieuDe="Theo mức độ" ds={tk?.theoMucDo} mau={MAU_THANH_MUC} />
            <div className="sc-qt14 mt-3">
              <div className="fw-semibold mb-1">
                <i className="bi bi-shield-check me-1 text-danger" />
                QT14 · tiếp nhận sự cố nguy hiểm ≤ 5 phút
              </div>
              {tk?.soNguyHiemDaTiepNhan ? (
                <>
                  <b>
                    {tk.soNguyHiemDatQT14}/{tk.soNguyHiemDaTiepNhan}
                  </b>{' '}
                  sự cố đạt ({tiLeQt14}%) · TB {thoiLuongPhut(tk.tbPhutTiepNhanNguyHiem)}
                </>
              ) : (
                <span className="text-body-secondary">Chưa có sự cố nguy hiểm nào được tiếp nhận trong khoảng này.</span>
              )}
            </div>
          </div>
          <div className="col-md-12 col-xl-4">
            <NhomThongKe tieuDe={`Theo trạng thái · ${tk?.soBaoGia ?? 0} báo giả`} ds={tk?.theoTrangThai} mau={MAU_THANH_TT} />
          </div>
        </div>
      </TheNoiDung>
    </>
  )
}

const Dong = ({ nhan, children }) => (
  <div className="sc-dong-tt">
    <span>{nhan}</span>
    <div>{children}</div>
  </div>
)

/** Khung chi tiet + thao tac hop le theo trang thai (API tra co CoThe... cho tung su co) */
function ChiTietSuCo({ id, khiDoi }) {
  const { taiKhoan } = useAuth()
  const { duLieu: s, loi, dangTai, taiLai } = useApi(id ? '/api/su-co/' + id : null)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [hop, setHop] = useState(null) // 'tiep-nhan' | 'ket-qua' | 'bao-gia' | 'anh'
  const [chu, setChu] = useState('')
  const [nguoi, setNguoi] = useState('')
  const { duLieu: dsNguoi } = useApi(hop === 'tiep-nhan' ? '/api/su-co/nguoi-xu-ly' : null)

  useLamMoiSuCo(() => taiLai({ im: true }))

  if (!id)
    return (
      <TheNoiDung lop="mb-3 sc-chi-tiet">
        <div className="sc-trong">
          <i className="bi bi-cursor d-block mb-2 fs-3" />
          Chọn một sự cố trong danh sách để xem chi tiết và xử lý.
        </div>
      </TheNoiDung>
    )
  if (dangTai && !s) return <DangTai chu="Đang tải sự cố…" />
  if (!s)
    return (
      <TheNoiDung lop="mb-3 sc-chi-tiet">
        <BaoLoi loi={loi} thuLai={taiLai} />
      </TheNoiDung>
    )

  async function goi(thaoTac, body) {
    const kq = await chay(() => goiApi(`/api/su-co/${s.maSuCo}/${thaoTac}`, { method: 'POST', body }))
    if (kq) {
      setHop(null)
      setChu('')
      await taiLai({ im: true })
      khiDoi?.()
    }
    return kq
  }

  async function tamGiu(giu) {
    const dongY = await xacNhan({
      tieuDe: giu ? 'Tạm giữ container' : 'Gỡ tạm giữ container',
      noiDung: giu
        ? `Tạm giữ container ${s.soContainer} theo sự cố #${s.maSuCo}?\nCổng sẽ KHÔNG cho giao container này ra cho tới khi gỡ tạm giữ hoặc đóng sự cố (QT15). Khách hàng liên quan sẽ được báo.`
        : `Gỡ tạm giữ container ${s.soContainer}? Container được giao nhận bình thường trở lại.`,
      nutDongY: giu ? 'Tạm giữ' : 'Gỡ tạm giữ',
      mau: giu ? 'danger' : 'primary',
    })
    if (dongY) goi('tam-giu', { tamGiu: giu })
  }

  async function dongSuCo() {
    const dongY = await xacNhan({
      tieuDe: 'Đóng sự cố',
      noiDung:
        `Đóng sự cố #${s.maSuCo}? Sự cố đã đóng không sửa được nữa và không bao giờ bị xóa (QT17).` +
        (s.tamGiuContainer ? `\nContainer ${s.soContainer} sẽ được gỡ tạm giữ.` : ''),
      nutDongY: 'Đóng sự cố',
      mau: 'success',
    })
    if (dongY) goi('dong')
  }

  const laGia = s.trangThai === 'BaoGia'
  const coThaoTac = s.coTheTiepNhan || s.coTheGhiKetQua || s.coTheDong || s.coTheBaoGia || s.coTheTamGiu || s.coTheGoTamGiu
  const treTiepNhan = s.mucDo === 'NguyHiem' && s.hanTiepNhan && (s.thoiDiemTiepNhan ? new Date(s.thoiDiemTiepNhan) > new Date(s.hanTiepNhan) : s.treHan)

  return (
    <TheNoiDung
      lop="mb-3 sc-chi-tiet"
      tieuDe={
        <>
          Sự cố #{s.maSuCo} <NhanTrangThai giaTri={s.tenTrangThai} mau={mauTrangThaiSuCo(s.trangThai)} />
        </>
      }
      icon={iconLoai(s.loai)}
      phai={
        <button type="button" className="btn-close" aria-label="Đóng khung chi tiết" onClick={() => khiDoi?.(0)} />
      }
    >
      <h2 className="h6 fw-bold mb-1">{s.tieuDe}</h2>
      <div className="d-flex flex-wrap gap-1 mb-2">
        <NhanTrangThai giaTri={s.tenMucDo} mau={mauMucDo(s.mucDo)} icon={s.mucDo === 'NguyHiem' ? 'bi-exclamation-octagon-fill' : undefined} />
        <NhanTrangThai giaTri={s.tenLoai} mau="info" icon={iconLoai(s.loai)} />
        {s.laKhachHang && <NhanTrangThai giaTri="Phản ánh của khách" mau="warn" icon="bi-person" />}
        {s.maSCAnNinh && <NhanTrangThai giaTri={'Camera AI #' + s.maSCAnNinh} mau="run" icon="bi-camera-video" />}
      </div>
      {s.canhBaoHan && (
        <div className={'small mb-2 ' + (s.treHan || treTiepNhan ? 'text-danger fw-semibold' : 'text-body-secondary')}>
          <i className="bi bi-alarm me-1" />
          {s.canhBaoHan}
        </div>
      )}
      {s.tamGiuContainer && (
        <div className="sc-tam-giu mb-2">
          <i className="bi bi-lock-fill me-1" />
          Container <b>{s.soContainer}</b> đang bị TẠM GIỮ — cổng không cho giao ra (QT15).
        </div>
      )}
      <p className="small mb-2" style={{ whiteSpace: 'pre-line' }}>
        {s.moTa}
      </p>

      {s.anhBase64 && (
        <button type="button" className="btn p-0 border-0 w-100 mb-2" onClick={() => setHop('anh')} aria-label="Xem ảnh hiện trường cỡ lớn">
          <img src={s.anhBase64} alt={'Ảnh hiện trường sự cố #' + s.maSuCo} className="sc-anh-lon" />
        </button>
      )}

      <Dong nhan="Nơi xảy ra">{s.viTri || '—'}</Dong>
      <Dong nhan="Container">
        {s.soContainer ? (
          <Link className="text-decoration-none fw-semibold" to={'/admin/containers/tracuu?so=' + encodeURIComponent(s.soContainer)}>
            {s.soContainer}
          </Link>
        ) : (
          '—'
        )}
        {s.soLenh && <span className="text-body-secondary"> · lệnh {s.soLenh}</span>}
      </Dong>
      <Dong nhan="Người báo">
        {s.nguoiBao}
        {s.khachHang && <div className="small text-body-secondary">{s.khachHang}</div>}
      </Dong>
      <Dong nhan="Người xử lý">{s.nguoiXuLy || <span className="text-body-secondary">— chưa tiếp nhận —</span>}</Dong>
      {s.canhBaoCameraAi && (
        <Dong nhan="Cảnh báo AI gốc">
          {s.canhBaoCameraAi}{' '}
          <Link className="small" to="/admin/security">
            Phòng an ninh
          </Link>
        </Dong>
      )}

      <h3 className="h6 fw-bold mt-3 mb-2">Mốc thời gian</h3>
      <ul className="sc-moc">
        <li className="xong">
          Báo lúc <b>{dinhDangNgayGio(s.thoiDiemBao)}</b> ({thoiGianTruoc(s.thoiDiemBao)})
        </li>
        <li className={s.thoiDiemTiepNhan ? (treTiepNhan ? 'tre' : 'xong') : s.treHan ? 'tre' : ''}>
          {s.thoiDiemTiepNhan ? (
            <>
              Tiếp nhận <b>{dinhDangNgayGio(s.thoiDiemTiepNhan)}</b> — sau{' '}
              {thoiLuongPhut((new Date(s.thoiDiemTiepNhan) - new Date(s.thoiDiemBao)) / 60000)}
            </>
          ) : (
            'Chưa tiếp nhận'
          )}
        </li>
        <li className={s.thoiDiemDong ? 'xong' : ''}>
          {s.thoiDiemDong ? (
            <>
              {laGia ? 'Kết luận báo giả' : 'Đóng'} <b>{dinhDangNgayGio(s.thoiDiemDong)}</b> — tổng{' '}
              {thoiLuongPhut((new Date(s.thoiDiemDong) - new Date(s.thoiDiemBao)) / 60000)}
            </>
          ) : (
            'Chưa đóng'
          )}
        </li>
      </ul>

      {s.ketQua && (
        <>
          <h3 className="h6 fw-bold mt-2 mb-2">{laGia ? 'Kết luận báo giả' : 'Kết quả xử lý'}</h3>
          <div className={'sc-ket-qua mb-2' + (laGia ? ' gia' : '')}>{s.ketQua}</div>
        </>
      )}

      {(s.loai === 'ThanhToan' || s.loai === 'HeThong') && (
        <div className="small mb-2">
          <i className="bi bi-box-arrow-up-right me-1" />
          {s.loai === 'ThanhToan' ? (
            <>
              UC25 — đối soát / kích hoạt / hoàn tiền giao dịch tại <Link to="/admin/payments">Đối soát thanh toán</Link> (QT16: trong ngày).
            </>
          ) : (
            <>
              UC27 — kiểm tra sức khỏe dịch vụ, trạm cổng ngoại tuyến tại <Link to="/admin/system">Vận hành hệ thống</Link>.
            </>
          )}
        </div>
      )}

      {coThaoTac ? (
        <div className="d-flex flex-wrap gap-2 mt-3 pt-2 border-top">
          {s.coTheTiepNhan && (
            <button type="button" className="btn btn-sm btn-primary" disabled={dangChay} onClick={() => setHop('tiep-nhan')}>
              <i className="bi bi-person-check me-1" />
              Tiếp nhận
            </button>
          )}
          {s.coTheTamGiu && (
            <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangChay} onClick={() => tamGiu(true)}>
              <i className="bi bi-lock me-1" />
              Tạm giữ container
            </button>
          )}
          {s.coTheGoTamGiu && (
            <button type="button" className="btn btn-sm btn-outline-secondary" disabled={dangChay} onClick={() => tamGiu(false)}>
              <i className="bi bi-unlock me-1" />
              Gỡ tạm giữ
            </button>
          )}
          {s.coTheGhiKetQua && (
            <button
              type="button"
              className="btn btn-sm btn-success"
              disabled={dangChay}
              onClick={() => {
                setChu(s.ketQua || '')
                setHop('ket-qua')
              }}
            >
              <i className="bi bi-journal-check me-1" />
              {s.ketQua ? 'Sửa kết quả' : 'Ghi kết quả'}
            </button>
          )}
          {s.coTheDong && (
            <button type="button" className="btn btn-sm btn-dark" disabled={dangChay} onClick={dongSuCo}>
              <i className="bi bi-check2-all me-1" />
              Đóng sự cố
            </button>
          )}
          {s.coTheBaoGia && (
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              disabled={dangChay}
              onClick={() => {
                setChu('')
                setHop('bao-gia')
              }}
            >
              <i className="bi bi-slash-circle me-1" />
              Đánh dấu báo giả
            </button>
          )}
        </div>
      ) : (
        <div className="small text-body-secondary mt-3 pt-2 border-top">
          <i className="bi bi-archive me-1" />
          Sự cố đã {laGia ? 'kết luận báo giả' : 'đóng'} — lưu vĩnh viễn, không xóa được (QT17).
        </div>
      )}

      <HopThoai
        mo={hop === 'tiep-nhan'}
        dong={() => setHop(null)}
        tieuDe={'Tiếp nhận sự cố #' + s.maSuCo}
        icon="bi-person-check"
        khoaDong={dangChay}
        khiGui={(e) => {
          e.preventDefault()
          goi('tiep-nhan', nguoi ? { maTKXuLy: Number(nguoi) } : {})
        }}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setHop(null)} disabled={dangChay}>
              Thôi
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay}>
              Tiếp nhận
            </button>
          </>
        }
      >
        {s.mucDo === 'NguyHiem' && (
          <div className="alert alert-danger py-2 small">
            QT14: sự cố nguy hiểm phải được tiếp nhận trong 5 phút kể từ {gioNgayNgan(s.thoiDiemBao)}.
          </div>
        )}
        <Truong nhan="Người xử lý" lop="mb-0">
          <select className="form-select" value={nguoi} onChange={(e) => setNguoi(e.target.value)}>
            <option value="">Tôi — {taiKhoan?.hoTen}</option>
            {(dsNguoi || [])
              .filter((n) => n.maTK !== taiKhoan?.maTK)
              .map((n) => (
                <option key={n.maTK} value={n.maTK}>
                  {n.hoTen} ({n.vaiTro === 'Admin' ? 'Quản trị' : 'Nhân viên'})
                </option>
              ))}
          </select>
        </Truong>
      </HopThoai>

      <HopThoai
        mo={hop === 'ket-qua' || hop === 'bao-gia'}
        dong={() => setHop(null)}
        tieuDe={(hop === 'bao-gia' ? 'Kết luận báo giả — sự cố #' : 'Kết quả xử lý — sự cố #') + s.maSuCo}
        icon={hop === 'bao-gia' ? 'bi-slash-circle' : 'bi-journal-check'}
        khoaDong={dangChay}
        khiGui={(e) => {
          e.preventDefault()
          if (hop === 'bao-gia') goi('bao-gia', { lyDo: chu })
          else goi('ket-qua', { ketQua: chu })
        }}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setHop(null)} disabled={dangChay}>
              Thôi
            </button>
            <button type="submit" className={'btn ' + (hop === 'bao-gia' ? 'btn-secondary' : 'btn-success')} disabled={dangChay || !chu.trim()}>
              {hop === 'bao-gia' ? 'Kết luận báo giả' : 'Lưu kết quả'}
            </button>
          </>
        }
      >
        <Truong
          nhan={hop === 'bao-gia' ? 'Lý do kết luận báo giả' : 'Kết quả xử lý'}
          batBuoc
          lop="mb-0"
          goiY={
            hop === 'bao-gia'
              ? 'Ghi vào kết quả với tiền tố "Báo giả:"; sự cố được đóng và gỡ tạm giữ (nếu có).'
              : s.laKhachHang || s.soContainer
                ? 'Khách hàng liên quan sẽ nhận email thông báo kết quả (UC24.3).'
                : 'Ghi rõ đã làm gì, ai làm, hiện trạng sau xử lý.'
          }
        >
          <textarea className="form-control" rows={5} maxLength={hop === 'bao-gia' ? 990 : 1000} value={chu} onChange={(e) => setChu(e.target.value)} />
        </Truong>
      </HopThoai>

      <HopThoai mo={hop === 'anh'} dong={() => setHop(null)} tieuDe={'Ảnh hiện trường — sự cố #' + s.maSuCo} icon="bi-image" kichThuoc="lg">
        <img src={s.anhBase64} alt={'Ảnh hiện trường sự cố #' + s.maSuCo} className="w-100 rounded" />
      </HopThoai>
    </TheNoiDung>
  )
}

/** /admin/su-co — QUAN LY SU CO cho nhan vien (UC23 → UC28) */
export default function QuanLySuCo() {
  const [loc, datLoc, xoaLoc] = useBoLoc(LOC_RONG)
  const locDs = { ...loc, id: undefined }
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/su-co' + taoQuery(locDs))
  const [tu, setTu] = useState(() => ngayInput(new Date(Date.now() - 29 * 86400000)))
  const [den, setDen] = useState(() => ngayInput(new Date()))
  const tk = useApi('/api/su-co/thong-ke' + taoQuery({ tuNgay: tu, denNgay: den }))
  const [moBao, setMoBao] = useState(false)
  const { duLieu: lenhMo } = useApi(moBao ? '/api/cong/lenh-dang-mo' : null)

  const lamMoi = () => {
    taiLai({ im: true })
    tk.taiLai({ im: true })
  }
  useLamMoiSuCo(lamMoi)

  const coLoc = loc.loai || loc.mucDo || loc.trangThai || loc.tuKhoa || loc.tuNgay || loc.denNgay || loc.chiDangMo || loc.tamGiu

  const COT = [
    { khoa: 'ma', ten: '#', rong: 56, hien: (s) => <span className="fw-semibold">#{s.maSuCo}</span> },
    {
      khoa: 'tieuDe',
      ten: 'Sự cố',
      hien: (s) => (
        <div className="min-w-0">
          <div className="sc-tieu-de">
            <i className={'bi me-1 text-primary ' + iconLoai(s.loai)} title={s.tenLoai} />
            {s.tieuDe}
          </div>
          <div className="sc-mo-ta-ngan">
            {s.tenLoai}
            {s.laKhachHang ? ' · phản ánh của khách' : ''}
            {s.coAnh ? ' · có ảnh' : ''}
          </div>
        </div>
      ),
    },
    {
      khoa: 'mucDo',
      ten: 'Mức độ',
      rong: 110,
      hien: (s) => <NhanTrangThai giaTri={s.tenMucDo} mau={mauMucDo(s.mucDo)} icon={s.mucDo === 'NguyHiem' ? 'bi-exclamation-octagon-fill' : undefined} />,
    },
    {
      khoa: 'cont',
      ten: 'Container / lệnh',
      an: 'lg',
      hien: (s) =>
        s.soContainer || s.soLenh ? (
          <div className="small">
            {s.soContainer && <div className="fw-semibold">{s.soContainer}</div>}
            {s.soLenh && <div className="text-body-secondary">{s.soLenh}</div>}
            {s.tamGiuContainer && <NhanTrangThai giaTri="Tạm giữ" mau="do" icon="bi-lock-fill" />}
          </div>
        ) : (
          <span className="text-body-secondary">—</span>
        ),
    },
    {
      khoa: 'nguoi',
      ten: 'Người báo',
      an: 'xl',
      hien: (s) => (
        <div className="small">
          {s.nguoiBao}
          {s.khachHang && <div className="text-body-secondary text-truncate" style={{ maxWidth: 170 }}>{s.khachHang}</div>}
        </div>
      ),
    },
    {
      khoa: 'luc',
      ten: 'Báo lúc',
      an: 'md',
      rong: 100,
      hien: (s) => (
        <div className="small text-nowrap">
          {gioNgayNgan(s.thoiDiemBao)}
          <div className="text-body-secondary">{thoiGianTruoc(s.thoiDiemBao)}</div>
        </div>
      ),
    },
    {
      khoa: 'tt',
      ten: 'Trạng thái',
      rong: 150,
      hien: (s) => (
        <div>
          <NhanTrangThai giaTri={s.tenTrangThai} mau={mauTrangThaiSuCo(s.trangThai)} />
          {s.treHan && (
            <div className="small text-danger fw-semibold mt-1">
              <i className="bi bi-alarm me-1" />
              Trễ hạn
            </div>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <div className="sc-quy-tac flex-grow-1">
          <span className="sc-qt">
            <b>QT14</b> nguy hiểm → báo động toàn cảng, tiếp nhận ≤ 5 phút
          </span>
          <span className="sc-qt">
            <b>QT15</b> container tạm giữ không ra cổng
          </span>
          <span className="sc-qt">
            <b>QT16</b> thanh toán lỗi đối soát trong ngày
          </span>
          <span className="sc-qt">
            <b>QT17</b> không xóa sự cố · trả lời khách ≤ 24 giờ
          </span>
        </div>
        <button type="button" className="btn btn-danger btn-sm" onClick={() => setMoBao(true)}>
          <i className="bi bi-megaphone me-1" />
          Báo sự cố
        </button>
      </div>

      <ThongKeSuCo tk={tk.duLieu} tu={tu} den={den} setTu={setTu} setDen={setDen} loi={tk.loi} taiLai={tk.taiLai} />

      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3">
        <div className="col-xl-8">
          <TheNoiDung
            tieuDe="Danh sách sự cố"
            icon="bi-list-task"
            khongDem
            phai={
              <>
                <select className="form-select form-select-sm w-auto" aria-label="Lọc loại" value={loc.loai} onChange={(e) => datLoc({ loai: e.target.value, trang: 1 })}>
                  <option value="">— Mọi loại —</option>
                  {LOAI_SU_CO.map((l) => (
                    <option key={l.ma} value={l.ma}>
                      {l.ten}
                    </option>
                  ))}
                </select>
                <select className="form-select form-select-sm w-auto" aria-label="Lọc mức độ" value={loc.mucDo} onChange={(e) => datLoc({ mucDo: e.target.value, trang: 1 })}>
                  <option value="">— Mọi mức độ —</option>
                  {MUC_DO.map((m) => (
                    <option key={m.ma} value={m.ma}>
                      {m.ten}
                    </option>
                  ))}
                </select>
                <select
                  className="form-select form-select-sm w-auto"
                  aria-label="Lọc trạng thái"
                  value={loc.trangThai}
                  onChange={(e) => datLoc({ trangThai: e.target.value, trang: 1 })}
                >
                  <option value="">— Mọi trạng thái —</option>
                  {TRANG_THAI.map((t) => (
                    <option key={t.ma} value={t.ma}>
                      {t.ten}
                    </option>
                  ))}
                </select>
                <OTimKiem giaTri={loc.tuKhoa} doi={(tuKhoa) => datLoc({ tuKhoa, trang: 1 })} goiY="#mã, tiêu đề, container…" rong={220} />
              </>
            }
            chan={<PhanTrang {...(d || {})} donVi="sự cố" doiTrang={(t) => datLoc({ trang: t })} />}
          >
            <div className="d-flex flex-wrap align-items-center gap-2 px-3 py-2 border-bottom small">
              <span className="text-body-secondary">Ngày báo</span>
              <input type="date" className="form-control form-control-sm w-auto" aria-label="Từ ngày" value={loc.tuNgay} onChange={(e) => datLoc({ tuNgay: e.target.value, trang: 1 })} />
              <span className="text-body-secondary">–</span>
              <input type="date" className="form-control form-control-sm w-auto" aria-label="Đến ngày" value={loc.denNgay} onChange={(e) => datLoc({ denNgay: e.target.value, trang: 1 })} />
              <div className="form-check form-check-inline mb-0 ms-2">
                <input className="form-check-input" type="checkbox" id="sc-dang-mo" checked={loc.chiDangMo} onChange={(e) => datLoc({ chiDangMo: e.target.checked, trang: 1 })} />
                <label className="form-check-label" htmlFor="sc-dang-mo">
                  Chỉ đang mở
                </label>
              </div>
              <div className="form-check form-check-inline mb-0">
                <input className="form-check-input" type="checkbox" id="sc-tam-giu" checked={loc.tamGiu} onChange={(e) => datLoc({ tamGiu: e.target.checked, trang: 1 })} />
                <label className="form-check-label" htmlFor="sc-tam-giu">
                  Đang tạm giữ container
                </label>
              </div>
              {coLoc && (
                <button type="button" className="btn btn-sm btn-link ms-auto" onClick={xoaLoc}>
                  <i className="bi bi-x-circle me-1" />
                  Xóa lọc
                </button>
              )}
            </div>
            <BangDuLieu
              cot={COT}
              dong={d?.items}
              khoaDong="maSuCo"
              dangTai={dangTai}
              bamDong={(s) => datLoc({ id: s.maSuCo })}
              lopDong={(s) =>
                (s.maSuCo === loc.id ? 'sc-dong-chon ' : '') + (s.mucDo === 'NguyHiem' && s.trangThai !== 'Dong' && s.trangThai !== 'BaoGia' ? 'sc-dong-nguy' : '')
              }
              rong={coLoc ? 'Không có sự cố nào khớp bộ lọc.' : 'Chưa có sự cố nào được báo.'}
            />
          </TheNoiDung>
        </div>
        <div className="col-xl-4">
          <ChiTietSuCo
            key={loc.id || 'trong'}
            id={loc.id || 0}
            khiDoi={(maMoi) => {
              if (maMoi === 0) datLoc({ id: 0 })
              lamMoi()
            }}
          />
        </div>
      </div>

      <HopThoai mo={moBao} dong={() => setMoBao(false)} tieuDe="Báo sự cố" icon="bi-megaphone" kichThuoc="lg">
        <BieuMauSuCo
          cacLoai={TAT_CA_LOAI}
          container={null}
          lenh={(lenhMo || []).map((l) => ({ maLenh: l.maLenh, soCont: l.soCont, nhan: `${l.soLenh} · ${l.bienSoXe}${l.soCont ? ' · ' + l.soCont : ''}` }))}
          nutGui="Ghi sự cố"
          khiHuy={() => setMoBao(false)}
          khiXong={(kq) => {
            setMoBao(false)
            lamMoi()
            if (kq?.id) datLoc({ id: kq.id })
          }}
        />
      </HopThoai>
    </>
  )
}
