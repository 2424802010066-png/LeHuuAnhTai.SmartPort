import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { docFileThanhAnh } from '../../ai/taiThuVien'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, TheNoiDung, Truong, useThongBao } from '../../components/ui'
import { chuoiHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, gioNgayNgan, ngayInput } from '../../utils/dinhDang'
import { chuanBiDinhVi, docChuBangLai, docQrTuAnh, docQrTuVideo, homNayInput, soNgayDen, taiBoDocQr } from './giayTo'
import './tai-xe.css'

const RONG = {
  soGiayTo: '',
  hoTen: '',
  ngaySinh: '',
  gioiTinh: '',
  ngayCapCCCD: '',
  diaChi: '',
  soGPLX: '',
  hangGPLX: '',
  hanGPLX: '',
  bienSo: '',
  dienThoai: '',
  ghiChu: '',
}

const CHU_CHO_QR = 'Đưa mặt trước thẻ vào khung — hệ thống tự nhận, không cần bấm chụp.'
// Quet ~6 giay chua ra: thuong do ma QR qua nho tren webcam (the cam xa) -> nhac dua MA QR lai gan
const CHU_NHAC_GAN = 'Chưa đọc được mã — đưa mã QR (góc trên bên phải thẻ) lại gần camera hơn, giữ yên 1–2 giây, tránh lóa đèn.'
const MAU_TRANG_THAI = { cho: 'text-body-secondary', chay: 'text-info-emphasis', ok: 'text-success-emphasis', loi: 'text-danger-emphasis' }
const ICON_TRANG_THAI = { chay: 'bi-hourglass-split', ok: 'bi-check-circle-fill', loi: 'bi-exclamation-triangle-fill' }

function DongTrangThai({ tt }) {
  return (
    <div className={'small mt-2 tx-trang-thai ' + (MAU_TRANG_THAI[tt.mau] || MAU_TRANG_THAI.cho)} role="status" aria-live="polite">
      {ICON_TRANG_THAI[tt.mau] && <i className={'bi me-1 ' + ICON_TRANG_THAI[tt.mau]} />}
      {tt.chu}
    </div>
  )
}

/** Bang thong tin tai xe sau khi quet the / luu / mo ho so */
function BangKhach({ khach, hoTen }) {
  if (!khach) return null
  if (khach.daLuu)
    return (
      <div className="alert alert-success py-2 small mb-2">
        <i className="bi bi-check-circle me-1" />
        <b>Đã lưu hồ sơ {hoTen}</b> — bấm <b>Ghi vào cổng</b> khi xe vào.
      </div>
    )
  if (khach.capNhat)
    return (
      <div className="alert alert-secondary py-2 small mb-2">
        <i className="bi bi-pencil-square me-1" />
        <b>Đang cập nhật hồ sơ</b> — đã vào cổng {khach.soLanVao} lần. Trường để trống không xóa dữ liệu cũ.
      </div>
    )
  if (khach.daCo)
    return (
      <div className="alert alert-success py-2 small mb-2">
        <i className="bi bi-person-check me-1" />
        <b>Khách quen</b> — đã vào cổng {khach.soLanVao} lần. Bằng lái và biển số điền sẵn từ lần trước, chỉ cần kiểm tra lại.
      </div>
    )
  return (
    <div className="alert alert-info py-2 small mb-2">
      <i className="bi bi-person-plus me-1" />
      <b>Tài xế mới</b> — chưa có hồ sơ.{khach.tuoi ? ` ${khach.tuoi} tuổi.` : ''} Chụp thêm bằng lái ở bước 2 rồi bấm Lưu.
    </div>
  )
}

function BangGplx({ gplx }) {
  if (!gplx) return null
  return gplx.daHetHan ? (
    <div className="alert alert-danger py-2 small mb-2">
      <i className="bi bi-x-octagon-fill me-1" />
      <b>BẰNG LÁI ĐÃ HẾT HẠN</b> ({dinhDangNgay(gplx.hanDen)}) — không được lái xe. Báo điều độ.
    </div>
  ) : (
    <div className="alert alert-warning py-2 small mb-2">
      <i className="bi bi-card-text me-1" />
      <b>Kết quả OCR</b> — số {gplx.soGplx || '?'} · hạng {gplx.hang || '?'} · hạn {gplx.hanDen ? dinhDangNgay(gplx.hanDen) : '?'}. OCR có thể sai, xem lại rồi
      sửa tay.
    </div>
  )
}

function KetQuaVaoCong({ kq }) {
  return (
    <div className={'alert py-2 small mt-3 mb-0 ' + (kq.gplxHetHan ? 'alert-danger' : 'alert-success')}>
      <div>
        <i className="bi bi-box-arrow-in-right me-1" />
        Đã ghi nhận <b>{kq.hoTen || 'tài xế'}</b> vào cổng lúc {gioNgayNgan(kq.lanVaoCuoi)} — lần thứ <b>{kq.soLanVao}</b>.
      </div>
      {kq.gplxHetHan && (
        <div className="fw-bold">
          <i className="bi bi-x-octagon-fill me-1" />
          CẢNH BÁO: bằng lái đã hết hạn{kq.hanGplx ? ' từ ' + dinhDangNgay(kq.hanGplx) : ''}!
        </div>
      )}
      {kq.ganVaoLuot ? (
        <div>
          <i className="bi bi-link-45deg me-1" />
          Đã gắn vào lượt xe <b>{kq.bienSo}</b> ({kq.huongDiLuot} cổng {kq.congLuot} lúc {gioNgayNgan(kq.thoiGianLuot)}).
        </div>
      ) : (
        <div className="text-body-secondary">
          <i className="bi bi-info-circle me-1" />
          Chưa có lượt xe nào của biển số này trong 30 phút qua để gắn.
        </div>
      )}
    </div>
  )
}

/**
 * Quet giay to tai xe tai cong (thay Admin/TaiXe/Quet cua ban MVC):
 * 1) QR mat truoc CCCD (camera hoac anh tai len) -> may chu boc truong
 * 2) OCR bang lai (tuy chon)  3) kiem tra, luu ho so, ghi vao cong.
 * ?maTX=5 : mo ho so co san de cap nhat (nut sua o danh sach).
 */
export default function QuetGiayTo() {
  const [thamSo, setThamSo] = useSearchParams()
  const maTX = Number(thamSo.get('maTX')) || 0
  const hoSo = useApi(maTX ? '/api/ho-so-tai-xe/' + maTX : null)
  const { gt, doi, datGt, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const bao = useThongBao()

  const [khach, setKhach] = useState(null)
  const [gplx, setGplx] = useState(null)
  const [ttQr, setTtQr] = useState({ chu: CHU_CHO_QR, mau: 'cho' })
  const [ttGplx, setTtGplx] = useState({ chu: '', mau: 'cho' })
  const [tienDoOcr, setTienDoOcr] = useState(null)
  const [dangQuay, setDangQuay] = useState(false)
  const [vaoCong, setVaoCong] = useState(null)
  const [goiY, setGoiY] = useState(null)

  const videoRef = useRef(null)
  const canvasRef = useRef(null)
  const fileQrRef = useRef(null)
  const fileGplxRef = useRef(null)
  const luongRef = useRef(null)
  const vongRef = useRef(0)
  // Moi lan bat camera la 1 "phien quet"; tat / bat lai thi vong quet cu (dang doi bo doc) tu dung
  const phienQuetRef = useRef(null)
  const lanDocRef = useRef(0)
  const dangGuiRef = useRef(false)
  const conGanRef = useRef(true)
  const daNapRef = useRef(0)
  // Gia tri moi nhat cho vong quet camera (vong lap chay ngoai chu ky render)
  const gtRef = useRef(gt)
  const guiChuoiRef = useRef(null)

  const baoQr = (chu, mau = 'cho') => setTtQr({ chu, mau })

  const tatCamera = useCallback(() => {
    clearTimeout(vongRef.current)
    phienQuetRef.current = null
    luongRef.current?.getTracks().forEach((t) => t.stop())
    luongRef.current = null
    if (videoRef.current) videoRef.current.srcObject = null
    setDangQuay(false)
  }, [])

  // Roi man hinh -> tat camera (khong de den camera sang)
  useEffect(() => {
    conGanRef.current = true
    return () => {
      conGanRef.current = false
      tatCamera()
    }
  }, [tatCamera])

  // Mo tu danh sach (?maTX=): nap ho so vao form mot lan
  useEffect(() => {
    const t = hoSo.duLieu
    if (!maTX || !t || t.maTX !== maTX || daNapRef.current === t.maTX) return
    daNapRef.current = t.maTX
    datLai({
      soGiayTo: t.soGiayTo || '',
      hoTen: t.hoTen || '',
      ngaySinh: ngayInput(t.ngaySinh),
      gioiTinh: t.gioiTinh || '',
      ngayCapCCCD: ngayInput(t.ngayCapCCCD),
      diaChi: t.diaChi || '',
      soGPLX: t.soGPLX || '',
      hangGPLX: t.hangGPLX || '',
      hanGPLX: ngayInput(t.hanGPLX),
      bienSo: t.bienSo || '',
      dienThoai: t.dienThoai || '',
      ghiChu: t.ghiChu || '',
    })
    setKhach({ daCo: true, capNhat: true, maTX: t.maTX, soLanVao: t.soLanVao, tuoi: t.tuoi })
    setGplx(null)
    setVaoCong(null)
  }, [hoSo.duLieu, maTX, datLai])

  useEffect(() => {
    gtRef.current = gt
  })

  // Go bien so -> tra tai xe lan truoc cua xe nay (cho 0,5 giay sau phim cuoi)
  useEffect(() => {
    const bs = gt.bienSo.trim()
    if (bs.replace(/[^A-Za-z0-9]/g, '').length < 5) {
      setGoiY(null)
      return undefined
    }
    let conHieuLuc = true
    const hen = setTimeout(async () => {
      try {
        const kq = await goiApi('/api/ho-so-tai-xe/theo-bien-so?bienSo=' + encodeURIComponent(bs))
        if (conHieuLuc) setGoiY(kq?.co ? kq : null)
      } catch {
        if (conHieuLuc) setGoiY(null)
      }
    }, 500)
    return () => {
      conHieuLuc = false
      clearTimeout(hen)
    }
  }, [gt.bienSo])

  /** Dien ket qua doc QR vao form. Truong trong trong QR khong xoa gia tri dang co (giong MVC). */
  function dienForm(d) {
    const cu = gtRef.current
    // The cua NGUOI KHAC nguoi dang nhap do -> lam trang form, khong de lan GPLX/bien so cua tai xe truoc
    const theKhac = !!cu.soGiayTo.trim() && cu.soGiayTo.trim() !== d.soCccd
    const moi = { ...(theKhac ? RONG : cu) }
    const dat = (khoa, v) => {
      if (v) moi[khoa] = v
    }
    dat('soGiayTo', d.soCccd)
    dat('hoTen', d.hoTen)
    dat('ngaySinh', ngayInput(d.ngaySinh))
    dat('gioiTinh', d.gioiTinh)
    dat('diaChi', d.diaChi)
    dat('ngayCapCCCD', ngayInput(d.ngayCap))
    if (d.daCo) {
      // Khach quen: do lai GPLX + bien so da luu, khoi phai chup lai bang lai
      dat('soGPLX', d.soGplxCu)
      dat('hangGPLX', d.hangCu)
      dat('hanGPLX', ngayInput(d.hanCu))
      dat('bienSo', d.bienSoCu)
      dat('dienThoai', d.dienThoaiCu)
    }
    datLai(moi)
    if (theKhac) {
      setGplx(null)
      setTtGplx({ chu: '', mau: 'cho' })
    }
    setVaoCong(null)
    setKhach({ daCo: d.daCo, maTX: d.maTX, soLanVao: d.soLanVao, tuoi: d.tuoi })
    // Dang cap nhat ho so A ma quet the cua B -> thoat che do cap nhat
    if (maTX && d.maTX !== maTX) {
      daNapRef.current = 0
      setThamSo({}, { replace: true })
    }
  }

  /** Gui CHUOI THO doc duoc tu QR len may chu (may chu tu boc truong, khong tin client) */
  async function guiChuoi(chuoi) {
    if (dangGuiRef.current) return
    dangGuiRef.current = true
    baoQr('Đọc được mã QR, đang kiểm tra…', 'chay')
    try {
      const d = await goiApi('/api/ho-so-tai-xe/doc-qr', { method: 'POST', body: { chuoi } })
      if (!conGanRef.current) return
      dienForm(d)
      // Diem nghi van (so CCCD lech gioi tinh/nam sinh...): van dien form nhung bao do de nguoi truc xem lai
      if (d.canhBao?.length) baoQr('Đã đọc thẻ của ' + d.hoTen + ' — CẦN XEM LẠI: ' + d.canhBao.join(' '), 'loi')
      else baoQr('Đã đọc xong thẻ của ' + d.hoTen + '.', 'ok')
      tatCamera()
    } catch (e) {
      if (conGanRef.current) baoQr(e.message, 'loi')
    } finally {
      dangGuiRef.current = false
    }
  }
  useEffect(() => {
    guiChuoiRef.current = guiChuoi
  })

  async function batTatCamera() {
    if (dangQuay) {
      tatCamera()
      baoQr('Đã tắt camera.')
      return
    }
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      baoQr('Camera chỉ chạy trên HTTPS hoặc localhost — dùng nút "Chọn ảnh".', 'loi')
      return
    }
    try {
      await taiBoDocQr()
      chuanBiDinhVi() // nap mo hinh dinh vi/khoi phuc QR o nen: chua xong thi van quet bang cach cu
      const luong = await navigator.mediaDevices.getUserMedia({
        // 1080p: moi o cua ma QR CCCD co gap ruoi so diem anh so voi 720p (do thu: ti le doc 25% -> 75%);
        // camera khong ho tro thi trinh duyet tu lay muc cao nhat co the
        video: { facingMode: 'environment', width: { ideal: 1920 }, height: { ideal: 1080 } },
        audio: false,
      })
      if (!conGanRef.current || !videoRef.current) {
        luong.getTracks().forEach((t) => t.stop())
        return
      }
      // Camera dien thoai / webcam co lay net tu dong: bat che do lay net lien tuc (khong ho tro thi bo qua)
      const track = luong.getVideoTracks()[0]
      if (track?.getCapabilities?.().focusMode?.includes('continuous'))
        track.applyConstraints({ advanced: [{ focusMode: 'continuous' }] }).catch(() => {})
      luongRef.current = luong
      const video = videoRef.current
      video.srcObject = luong
      setDangQuay(true) // hien khung video truoc roi moi phat
      await video.play()
      baoQr('Đang tìm mã QR… đưa mặt trước thẻ vào khung.', 'chay')

      const phien = {}
      phienQuetRef.current = phien
      const conQuet = () => phienQuetRef.current === phien && !!luongRef.current
      const batDau = performance.now()
      let luot = 0
      let daNhac = false // da nhac dua ma lai gan, hoac da doc duoc ma -> khong nhac nua
      const vong = async () => {
        if (!conQuet()) return
        const t0 = performance.now()
        if (!dangGuiRef.current && video.readyState >= 2) {
          let chuoi = null
          try {
            chuoi = await docQrTuVideo(video, canvasRef.current, luot++)
          } catch {
            // khung hinh loi (camera vua doi do phan giai...) -> bo qua, doc khung sau
          }
          if (!conQuet()) return
          if (chuoi) daNhac = true
          // Chan quet lien tuc cung mot the: cach nhau it nhat 2 giay
          if (chuoi && Date.now() - lanDocRef.current > 2000) {
            lanDocRef.current = Date.now()
            guiChuoiRef.current?.(chuoi)
          } else if (!daNhac && performance.now() - batDau > 6000) {
            daNhac = true
            baoQr(CHU_NHAC_GAN, 'chay')
          }
        }
        // Nghi >= 80 ms va >= 1,5 lan thoi gian vua doc: may yeu tu giam nhip, giao dien khong bi khoa
        vongRef.current = setTimeout(vong, Math.max(80, (performance.now() - t0) * 1.5))
      }
      vongRef.current = setTimeout(vong, 0)
    } catch (e) {
      tatCamera()
      if (e?.hienThi) baoQr(e.message, 'loi')
      else
        baoQr(
          'Không mở được camera' + (e?.name === 'NotAllowedError' ? ' (chưa cấp quyền)' : e?.name === 'NotFoundError' ? ' (máy không có camera)' : '') + '. Dùng nút "Chọn ảnh".',
          'loi',
        )
    }
  }

  async function chonAnhQr(e) {
    const f = e.target.files?.[0]
    e.target.value = '' // chon lai cung file van chay
    if (!f) return
    if (dangQuay) tatCamera()
    baoQr('Đang đọc ảnh…', 'chay')
    try {
      const img = await docFileThanhAnh(f)
      const chuoi = await docQrTuAnh(img, canvasRef.current)
      if (chuoi) await guiChuoi(chuoi)
      else baoQr('Không tìm thấy mã QR trong ảnh — chụp lại rõ hơn, lấy trọn mã QR.', 'loi')
    } catch (err) {
      baoQr(err?.hienThi ? err.message : 'Không mở được ảnh.', 'loi')
    }
  }

  async function chonAnhGplx(e) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f || tienDoOcr != null) return
    setGplx(null)
    setTtGplx({ chu: 'Đang nhận dạng chữ trên bằng lái…', mau: 'chay' })
    setTienDoOcr(0)
    let vanBan
    try {
      const img = await docFileThanhAnh(f)
      vanBan = await docChuBangLai(img, (p) => setTienDoOcr(p))
    } catch (err) {
      setTienDoOcr(null)
      setTtGplx({ chu: err?.hienThi ? err.message : 'Lỗi khi nhận dạng — thử ảnh khác.', mau: 'loi' })
      return
    }
    setTienDoOcr(null)
    try {
      // May chu boc so / hang / han tu van ban tho
      const d = await goiApi('/api/ho-so-tai-xe/doc-gplx', { method: 'POST', body: { vanBan } })
      const thayDoi = {}
      if (d.soGplx) thayDoi.soGPLX = d.soGplx
      if (d.hang) thayDoi.hangGPLX = d.hang
      if (d.hanDen) thayDoi.hanGPLX = ngayInput(d.hanDen)
      datGt(thayDoi)
      setGplx(d)
      setTtGplx({ chu: 'Đã bóc được thông tin — kiểm tra lại rồi sửa nếu sai.', mau: 'ok' })
    } catch (err) {
      setTtGplx({ chu: err.message, mau: 'loi' })
    }
  }

  async function luu(e) {
    e.preventDefault()
    const than = {
      ...gt,
      loaiGiayTo: 'CCCD',
      ngaySinh: chuoiHoacNull(gt.ngaySinh),
      ngayCapCCCD: chuoiHoacNull(gt.ngayCapCCCD),
      hanGPLX: chuoiHoacNull(gt.hanGPLX),
    }
    const kq = await chay(() => goiApi('/api/ho-so-tai-xe', { method: 'POST', body: than }))
    const t = kq?.duLieu
    if (!t) return
    setKhach({ daCo: true, daLuu: true, maTX: t.maTX, soLanVao: t.soLanVao, tuoi: t.tuoi })
    if (t.gplxHetHan) bao.canhBao('Lưu ý: bằng lái của ' + (t.hoTen || 'tài xế') + ' đã hết hạn.')
  }

  async function ghiVaoCong() {
    const soGiayTo = gt.soGiayTo.trim()
    if (!soGiayTo) {
      bao.canhBao('Chưa có số CCCD — quét thẻ trước.')
      return
    }
    const kq = await chay(() => goiApi('/api/ho-so-tai-xe/ghi-vao-cong', { method: 'POST', body: { soGiayTo, bienSo: gt.bienSo.trim() } }))
    const d = kq?.duLieu
    if (!d) return
    setVaoCong(d)
    if (d.bienSo) datGt({ bienSo: d.bienSo })
    setKhach((k) => ({ ...(k || {}), daCo: true, daLuu: false, maTX: d.maTX, soLanVao: d.soLanVao }))
    if (d.gplxHetHan) bao.loi('CẢNH BÁO: bằng lái của ' + (d.hoTen || 'tài xế') + ' đã hết hạn!')
  }

  function lamMoi() {
    tatCamera()
    datLai(RONG)
    setKhach(null)
    setGplx(null)
    setVaoCong(null)
    setGoiY(null)
    setTtQr({ chu: CHU_CHO_QR, mau: 'cho' })
    setTtGplx({ chu: '', mau: 'cho' })
    daNapRef.current = 0
    if (maTX) setThamSo({}, { replace: true })
  }

  const soNgayConHan = soNgayDen(gt.hanGPLX)
  const gplxHetHanForm = !!gt.hanGPLX && gt.hanGPLX < homNayInput()
  const goiYHanGplx = soNgayConHan != null && soNgayConHan >= 0 && soNgayConHan <= 30 ? `Còn ${soNgayConHan} ngày — nhắc tài xế gia hạn.` : undefined
  // Goi y "xe nay lan truoc do ai lai" chi khi khac nguoi dang nhap tren form
  const hienGoiY = goiY && goiY.soGiayTo !== gt.soGiayTo.trim()

  return (
    <>
      {maTX > 0 && <BaoLoi loi={hoSo.loi} thuLai={hoSo.taiLai} />}
      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung tieuDe="1 · Quét QR mặt trước CCCD" icon="bi-qr-code-scan">
            <div className="tx-khung-quet">
              <video ref={videoRef} className={'tx-video' + (dangQuay ? '' : ' d-none')} playsInline muted aria-label="Khung hình camera quét mã QR" />
              {dangQuay ? (
                <div className="tx-vien-quet" aria-hidden="true" />
              ) : (
                <div className="tx-camera-trong">
                  <i className="bi bi-person-vcard" />
                  <div className="small mt-1">Camera đang tắt</div>
                </div>
              )}
              <canvas ref={canvasRef} className="d-none" />
            </div>
            <div className="d-flex gap-2 mt-2">
              <button type="button" className={'btn flex-fill ' + (dangQuay ? 'btn-outline-danger' : 'btn-primary')} onClick={batTatCamera}>
                <i className={'bi me-1 ' + (dangQuay ? 'bi-stop-circle' : 'bi-camera-video')} />
                {dangQuay ? 'Tắt camera' : 'Bật camera'}
              </button>
              <button type="button" className="btn btn-outline-secondary" onClick={() => fileQrRef.current?.click()}>
                <i className="bi bi-image me-1" />
                Chọn ảnh
              </button>
              <input ref={fileQrRef} type="file" accept="image/*" className="d-none" onChange={chonAnhQr} />
            </div>
            <DongTrangThai tt={ttQr} />
          </TheNoiDung>

          <TheNoiDung tieuDe="2 · Chụp bằng lái (tùy chọn)" icon="bi-card-text">
            <button type="button" className="btn btn-outline-secondary w-100" disabled={tienDoOcr != null} onClick={() => fileGplxRef.current?.click()}>
              {tienDoOcr != null ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-image me-1" />}
              Chọn ảnh bằng lái
            </button>
            <input ref={fileGplxRef} type="file" accept="image/*" className="d-none" onChange={chonAnhGplx} />
            <div className="small text-body-secondary mt-2">Bằng lái không có mã QR — OCR có thể sai, xem lại rồi sửa tay.</div>
            {ttGplx.chu && <DongTrangThai tt={ttGplx} />}
            {tienDoOcr != null && (
              <div className="progress tx-tien-do mt-1" role="progressbar" aria-label="Tiến độ OCR" aria-valuenow={Math.round(tienDoOcr * 100)} aria-valuemin={0} aria-valuemax={100}>
                <div className="progress-bar" style={{ width: Math.round(tienDoOcr * 100) + '%' }} />
              </div>
            )}
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung
            tieuDe={maTX ? 'Cập nhật hồ sơ tài xế' : '3 · Kiểm tra rồi lưu'}
            icon="bi-person-vcard"
            phai={
              <Link className="btn btn-sm btn-outline-secondary" to="/admin/taixe">
                <i className="bi bi-list-ul me-1" />
                Danh sách
              </Link>
            }
          >
            {maTX > 0 && hoSo.dangTai && (
              <div className="small text-body-secondary mb-2">
                <span className="spinner-border spinner-border-sm me-1" />
                Đang mở hồ sơ…
              </div>
            )}
            <BangKhach khach={khach} hoTen={gt.hoTen} />
            <BangGplx gplx={gplx} />

            <form onSubmit={luu}>
              <div className="row g-2">
                <Truong nhan="Số CCCD" batBuoc lop="col-md-5">
                  <input className="form-control tx-so-giay-to" value={gt.soGiayTo} onChange={doi('soGiayTo')} maxLength={20} readOnly={maTX > 0} autoComplete="off" />
                </Truong>
                <Truong nhan="Họ tên" batBuoc lop="col-md-7">
                  <input className="form-control" value={gt.hoTen} onChange={doi('hoTen')} maxLength={100} autoComplete="off" />
                </Truong>

                <Truong nhan="Ngày sinh" lop="col-md-4">
                  <input className="form-control" type="date" value={gt.ngaySinh} onChange={doi('ngaySinh')} />
                </Truong>
                <Truong nhan="Giới tính" lop="col-md-3">
                  <input className="form-control" value={gt.gioiTinh} onChange={doi('gioiTinh')} maxLength={10} list="tx-gioi-tinh" />
                </Truong>
                <Truong nhan="Ngày cấp CCCD" lop="col-md-5">
                  <input className="form-control" type="date" value={gt.ngayCapCCCD} onChange={doi('ngayCapCCCD')} />
                </Truong>

                <Truong nhan="Địa chỉ" lop="col-12">
                  <input className="form-control" value={gt.diaChi} onChange={doi('diaChi')} maxLength={300} autoComplete="off" />
                </Truong>

                <Truong nhan="Số GPLX" lop="col-md-4">
                  <input className="form-control" value={gt.soGPLX} onChange={doi('soGPLX')} maxLength={20} autoComplete="off" />
                </Truong>
                <Truong nhan="Hạng" lop="col-md-3">
                  <input className="form-control text-uppercase" value={gt.hangGPLX} onChange={doi('hangGPLX')} maxLength={10} placeholder="FC / C / B2" />
                </Truong>
                <Truong nhan="GPLX hạn đến" lop="col-md-5" loi={gplxHetHanForm ? 'Đã hết hạn — không được lái xe.' : undefined} goiY={goiYHanGplx}>
                  <input className="form-control" type="date" value={gt.hanGPLX} onChange={doi('hanGPLX')} />
                </Truong>

                <Truong nhan="Biển số xe đang lái" lop="col-md-5">
                  <input className="form-control tx-o-bien-so" value={gt.bienSo} onChange={doi('bienSo')} maxLength={20} placeholder="51C-638.72" autoComplete="off" />
                </Truong>
                <Truong nhan="Điện thoại" lop="col-md-3">
                  <input className="form-control" type="tel" value={gt.dienThoai} onChange={doi('dienThoai')} maxLength={20} autoComplete="off" />
                </Truong>
                <Truong nhan="Ghi chú" lop="col-md-4">
                  <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={300} />
                </Truong>
              </div>
              <datalist id="tx-gioi-tinh">
                <option value="Nam" />
                <option value="Nữ" />
              </datalist>

              {hienGoiY && (
                <div className={'small mt-2 ' + (goiY.gplxHetHan ? 'text-danger-emphasis' : 'text-body-secondary')}>
                  <i className="bi bi-truck me-1" />
                  Xe này lần trước do <b>{goiY.hoTen || 'tài xế khác'}</b> lái · {goiY.soLanVao} lượt
                  {goiY.gplxHetHan ? ' · GPLX đã hết hạn' : goiY.gplxSapHetHan ? ' · GPLX sắp hết hạn' : ''}
                  <button
                    type="button"
                    className="btn btn-link btn-sm p-0 ms-2 align-baseline"
                    onClick={() => {
                      lamMoi()
                      setThamSo({ maTX: String(goiY.maTX) }, { replace: true })
                    }}
                  >
                    Mở hồ sơ
                  </button>
                </div>
              )}

              <div className="d-flex flex-wrap gap-2 mt-3">
                <button type="submit" className="btn btn-primary flex-fill" disabled={dangChay}>
                  {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
                  Lưu hồ sơ tài xế
                </button>
                <button type="button" className="btn btn-outline-success" onClick={ghiVaoCong} disabled={dangChay}>
                  <i className="bi bi-box-arrow-in-right me-1" />
                  Ghi vào cổng
                </button>
                <button type="button" className="btn btn-outline-secondary" onClick={lamMoi} disabled={dangChay} title="Làm trống form, quét tài xế khác" aria-label="Quét tài xế khác">
                  <i className="bi bi-arrow-counterclockwise" />
                </button>
              </div>
            </form>

            {vaoCong && <KetQuaVaoCong kq={vaoCong} />}
          </TheNoiDung>

          <div className="small text-body-secondary">
            <i className="bi bi-shield-lock me-1" />
            Không lưu ảnh CCCD · máy chủ tự bóc trường từ mã QR · chỉ nhân viên đăng nhập xem được.
          </div>
        </div>
      </div>
    </>
  )
}
