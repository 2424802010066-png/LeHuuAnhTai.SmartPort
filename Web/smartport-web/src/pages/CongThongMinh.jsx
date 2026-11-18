import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { khoiDongAlpr, phanTichAnhXe } from '../ai/alpr'
import { AI_MAU, docFileThanhAnh, taiAnh } from '../ai/taiThuVien'
import { duongDanAnh } from '../api/client'
import { useApi } from '../api/useApi'
import { useAuth } from '../auth/AuthContext'
import { tenLoaiLenh } from '../ngoaiTuyen/banSao.js'
import { LY_DO_VUOT_MAY_CHU_CHAM } from '../ngoaiTuyen/hopThuDi.js'
import { doiChieuLenhTaiTram, ghiLuotXeTaiTram, tram } from '../ngoaiTuyen/tram'
import TheChiSo from '../components/TheChiSo'
import TienTrinhSuKien from '../components/TienTrinhSuKien'
import { useNgheSuKien } from '../realtime/SuKienContext'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien, gioNgayNgan } from '../utils/dinhDang'

const ANH_MAU = [
  { file: 'alpr-demo-1.png', ten: 'Đầu kéo container', bien: '15C-274.84', icon: 'bi-truck', lop: 'btn-outline-primary' },
  { file: 'alpr-demo-2.png', ten: 'Xe tải thùng', bien: '29C-468.28', icon: 'bi-truck-front', lop: 'btn-outline-primary' },
  { file: 'alpr-bienvang.png', ten: 'Biển nền vàng', bien: '51C-638.72', icon: 'bi-sign-stop', lop: 'btn-outline-warning' },
  { file: 'alpr-demo-mop.png', ten: 'Vỏ móp méo', bien: '61H-529.30', icon: 'bi-exclamation-octagon', lop: 'btn-outline-danger' },
]
// Anh xe (dataURL) qua lon thi khong dua vao hang doi ngoai tuyen (may chu nhan toi da 8 MB, lo dong bo ~3 MB)
const ANH_TOI_DA_KY_TU = 2_500_000
const gioPhutNgan = (ms) => {
  const d = new Date(ms)
  return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0')
}

/** Lenh tu ban sao ngoai tuyen (lenhMo) -> dang LenhKhop de hien chung */
function lenhTuBanSao(l) {
  return { maLenh: l.maLenh, soLenh: l.soLenh || '#' + l.maLenh, loai: tenLoaiLenh(l.loaiLenh), soCont: l.soCont, taiXe: null, hen: null, dungGio: null, daCheckIn: false }
}

const LOAI_XE_GOI_Y = ['Đầu kéo container', 'Xe tải thùng', 'Rơ-moóc 20ft', 'Rơ-moóc 40ft', 'Xe bồn', 'Xe container lạnh', 'Xe nâng hàng', 'Xe khách', 'Ô tô con', 'Xe nội bộ cảng']

function NhanKetQuaAi({ ai }) {
  if (!ai.chuanVN) return <span className="badge text-bg-warning">không chuẩn VN</span>
  if (ai.dongThuan) return <span className="badge text-bg-success">2 engine khớp</span>
  if (!ai.khac && ai.engine === 'cct' && ai.doTinCay >= 85) return <span className="badge text-bg-info">AI chuyên dụng</span>
  return <span className="badge text-bg-warning">cần kiểm tra</span>
}

function KetQuaLenh({ lenh }) {
  if (lenh.trangThai === 'dang-tim')
    return (
      <div className="small text-muted mb-2">
        <span className="spinner-border spinner-border-sm me-1" />
        Đối chiếu lệnh giao nhận…
      </div>
    )
  const nhanBanSao = lenh.banSao ? (
    <span className="badge text-bg-warning ms-1" title="Mất kết nối máy chủ — đối chiếu bằng bản sao lưu trong máy">
      bản sao lúc {gioPhutNgan(lenh.banSao.luc)}
      {lenh.banSao.cu ? ' (cũ)' : ''}
    </span>
  ) : null
  const chan = lenh.chan ? (
    <div className="alert alert-danger py-2 small mb-2">
      <i className="bi bi-sign-stop-fill me-1" />
      <b>Theo bản sao: KHÔNG cho qua</b> — {lenh.chan}
    </div>
  ) : null
  if (lenh.trangThai === 'khong-kiem-duoc')
    return lenh.biCat ? (
      <>
        {chan}
        <div className="alert alert-warning py-2 small mb-2">
          <i className="bi bi-scissors me-1" />
          <b>Bản sao không đầy đủ (bị cắt)</b> — không thấy lệnh trong bản sao nhưng KHÔNG có nghĩa là không có lệnh: không kiểm được
          lệnh, kiểm giấy tờ bằng mắt / hỏi điều độ.{nhanBanSao}
        </div>
      </>
    ) : (
      <div className="alert alert-warning py-2 small mb-2">
        <i className="bi bi-wifi-off me-1" />
        <b>Mất kết nối máy chủ</b> và máy này chưa có bản sao lệnh — không kiểm được lệnh, kiểm giấy tờ bằng mắt.
      </div>
    )
  if (lenh.trangThai === 'khong')
    return (
      <>
        {chan}
        <div className="alert alert-warning py-2 small mb-2">
          <i className="bi bi-exclamation-triangle me-1" />
          <b>Không có lệnh</b> cho biển số này — kiểm tra thủ công trước khi cho vào.{nhanBanSao}
        </div>
      </>
    )
  if (lenh.trangThai !== 'co') return null
  const l = lenh.duLieu
  if (lenh.banSao)
    return (
      <>
        {chan}
        <div className="alert alert-warning py-2 small mb-2">
          <i className="bi bi-patch-check me-1" />
          <b>Khớp lệnh {l.soLenh}</b> {l.loai ? '— ' + l.loai : ''}
          {l.soCont && (
            <>
              {' '}
              · cont <b>{l.soCont}</b>
            </>
          )}
          {nhanBanSao}
          {(lenh.canhBao || []).map((c) => (
            <div key={c}>{c}</div>
          ))}
        </div>
      </>
    )
  return (
    <div className="alert alert-success py-2 small mb-2">
      <i className="bi bi-patch-check-fill me-1" />
      <b>Khớp lệnh {l.soLenh}</b> — {l.loai}
      {l.soCont && (
        <>
          {' '}
          · cont <b>{l.soCont}</b>
        </>
      )}
      {l.taiXe && <> · tài xế {l.taiXe}</>} · hẹn {l.hen}{' '}
      {l.dungGio ? <span className="badge text-bg-success">đúng giờ</span> : <span className="badge text-bg-warning">lệch giờ hẹn</span>}{' '}
      {l.daCheckIn && <span className="badge text-bg-info">đã check-in online</span>}
    </div>
  )
}

export default function CongThongMinh() {
  const { taiKhoan } = useAuth()
  const tongHop = useApi('/api/cong/tong-hop-hom-nay')
  const luotXe = useApi('/api/cong/luot-xe?soLuong=30')
  const lenhMo = useApi('/api/cong/lenh-dang-mo')

  const [engine, setEngine] = useState({ trangThai: 'dang-tai' })
  const [dangPhanTich, setDangPhanTich] = useState(false)
  const [buoc, setBuoc] = useState('')
  const [loiAi, setLoiAi] = useState('')
  const [daCoAnh, setDaCoAnh] = useState(false)
  const [ai, setAi] = useState(null)
  const [nguon, setNguon] = useState('NhapTay')
  const [form, setForm] = useState({ bienSo: '', loaiXe: '', cong: 'A1', huongDi: 'Vào' })
  const [lenh, setLenh] = useState({ trangThai: 'chua' })
  const [dangLuu, setDangLuu] = useState(false)
  const [ketQuaLuu, setKetQuaLuu] = useState(null)
  const [loiLuu, setLoiLuu] = useState('')
  const [ghiTam, setGhiTam] = useState(null) // luot vua ghi TAM trong may (mat mang)
  const [chuaXacNhan, setChuaXacNhan] = useState(null) // XE RA: may chu chua tra loi kip -> chua cho ra (Thu lai / Van cho qua)
  const [dangQuay, setDangQuay] = useState(false)
  const [anhLon, setAnhLon] = useState(null)

  const canvasRef = useRef(null)
  const videoRef = useRef(null)
  const streamRef = useRef(null)
  const fileRef = useRef(null)

  // Khoi dong 3 engine AI 1 lan (tai ve trinh duyet, lan sau lay tu bo nho dem)
  useEffect(() => {
    let conGan = true
    khoiDongAlpr()
      .then((e) => conGan && setEngine({ trangThai: 'san-sang', yolo: !!e.yolo, cct: !!e.cct, tesseract: !!e.worker }))
      .catch((err) => conGan && setEngine({ trangThai: 'loi', loi: err.message }))
    return () => {
      conGan = false
      streamRef.current?.getTracks().forEach((t) => t.stop())
    }
  }, [])

  // HUONG SU KIEN: co su kien cua bat ky module nao (xe qua cong o may khac, bai doi trang thai,
  // tai chinh vua thu phi...) -> lam moi ngam danh sach, chi so, lenh (gop cac su kien sat nhau)
  const henLamMoi = useRef(null)
  useNgheSuKien('*', () => {
    clearTimeout(henLamMoi.current)
    henLamMoi.current = setTimeout(() => {
      tongHop.taiLai({ im: true })
      luotXe.taiLai({ im: true })
      lenhMo.taiLai({ im: true })
    }, 400)
  })

  // Man cong can ban sao lenh / eDO cua cong dang chon (dung khi mat mang)
  useEffect(() => tram.dungBanSaoCho(form.cong), [form.cong])

  // Doi chieu lenh giao nhan moi khi bien so doi (cho nguoi dung go xong 0,45 giay).
  // Mat mang -> doi chieu bang ban sao trong may (ghi ro gio ban sao) thay vi bao nham "khong co lenh".
  useEffect(() => {
    const bien = form.bienSo.trim()
    if (bien.replace(/[^A-Za-z0-9]/g, '').length < 5) {
      setLenh({ trangThai: 'chua' })
      return undefined
    }
    let conHieuLuc = true
    setLenh({ trangThai: 'dang-tim' })
    const hen = setTimeout(async () => {
      try {
        const kq = await doiChieuLenhTaiTram(bien, { huongDi: form.huongDi, cong: form.cong, timeoutMs: 6000 })
        if (!conHieuLuc) return
        if (kq.nguon === 'may-chu') setLenh(kq.lenh ? { trangThai: 'co', duLieu: kq.lenh } : kq.loi ? { trangThai: 'chua' } : { trangThai: 'khong' })
        else if (kq.nguon === 'ban-sao') {
          const banSao = { luc: kq.banSaoLuc, cu: kq.banSaoCu }
          setLenh(
            kq.lenh
              ? { trangThai: 'co', duLieu: lenhTuBanSao(kq.lenh), banSao, chan: kq.chan, canhBao: kq.canhBao }
              : kq.khongDayDu // goi ban sao bi CAT: khong thay lenh != khong co lenh
                ? { trangThai: 'khong-kiem-duoc', biCat: true, banSao, chan: kq.chan, canhBao: kq.canhBao }
                : { trangThai: 'khong', banSao, chan: kq.chan, canhBao: kq.canhBao },
          )
        } else setLenh({ trangThai: 'khong-kiem-duoc' })
      } catch {
        if (conHieuLuc) setLenh({ trangThai: 'chua' })
      }
    }, 450)
    return () => {
      conHieuLuc = false
      clearTimeout(hen)
    }
  }, [form.bienSo, form.huongDi, form.cong])

  async function phanTich(img, nguonAnh) {
    if (dangPhanTich) return
    setDangPhanTich(true)
    setDaCoAnh(true)
    setLoiAi('')
    setKetQuaLuu(null)
    setGhiTam(null)
    setChuaXacNhan(null)
    setLoiLuu('')
    try {
      const kq = await phanTichAnhXe(img, canvasRef.current, setBuoc)
      setAi(kq)
      setNguon(nguonAnh)
      setForm((f) => ({ ...f, bienSo: kq.bienSo, loaiXe: kq.loaiXe }))
    } catch (e) {
      setLoiAi('Lỗi phân tích: ' + e.message)
    } finally {
      setBuoc('')
      setDangPhanTich(false)
    }
  }

  async function chonAnhMau(m) {
    tatCamera()
    try {
      phanTich(await taiAnh(AI_MAU + m.file), 'AnhMau')
    } catch (e) {
      setLoiAi(e.message)
    }
  }

  async function chonFile(e) {
    const f = e.target.files[0]
    e.target.value = '' // chon lai cung file van chay
    if (!f) return
    tatCamera()
    try {
      phanTich(await docFileThanhAnh(f), 'TaiAnh')
    } catch (err) {
      setLoiAi(err.message)
    }
  }

  async function batCamera() {
    if (!navigator.mediaDevices?.getUserMedia) {
      setLoiAi('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.')
      return
    }
    try {
      const s = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 } }, audio: false })
      streamRef.current = s
      videoRef.current.srcObject = s
      await videoRef.current.play()
      setDangQuay(true)
      setLoiAi('')
    } catch {
      setLoiAi('Không truy cập được camera (cần cấp quyền).')
    }
  }

  function tatCamera() {
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
    setDangQuay(false)
  }

  async function chupVaDoc() {
    const v = videoRef.current
    if (!v || v.readyState < 2) return
    const tmp = document.createElement('canvas')
    tmp.width = v.videoWidth
    tmp.height = v.videoHeight
    tmp.getContext('2d').drawImage(v, 0, 0)
    const img = await taiAnh(tmp.toDataURL('image/jpeg', 0.9))
    tatCamera()
    phanTich(img, 'Webcam')
  }

  function chonLenh(l) {
    // Nhap tay theo lenh: bo ket qua AI cu de khong gan nham anh xe khac vao lenh nay
    setAi(null)
    setNguon('NhapTay')
    setKetQuaLuu(null)
    setGhiTam(null)
    setChuaXacNhan(null)
    setForm((f) => ({ ...f, bienSo: l.bienSoXe || '', huongDi: l.trangThai === 'Đã vào cổng' ? 'Ra' : 'Vào' }))
  }

  /** Xu ly ket qua ghi luot xe (lan dau, Thu lai, Van cho qua) — ngu: { bien, huongDi, cong, anh, boAnh, phu, lenhBiet } */
  function xuLyKetQua(kq, ngu) {
    setChuaXacNhan(null)
    if (kq.ketQua === 'da-ghi') {
      setKetQuaLuu({ ...kq.duLieu, bienSo: ngu.bien, huongDi: ngu.huongDi, cong: ngu.cong })
      setAi(null)
      setForm((f) => ({ ...f, bienSo: '', loaiXe: '' }))
      tongHop.taiLai({ im: true })
      luotXe.taiLai({ im: true })
      lenhMo.taiLai({ im: true })
    } else if (kq.ketQua === 'chua-xac-nhan') {
      // XE RA luc dang co mang ma may chu khong tra loi kip: CHUA biet eDO / hai quan -> chua cho ra, KHONG tu xep hang
      setChuaXacNhan({ ...ngu, muc: kq.muc, lyDo: kq.lyDo, thongBao: kq.loi?.message })
    } else if (kq.ketQua === 'da-ghi-tam' || kq.ketQua === 'cho-dang-nhap') {
      setGhiTam({
        bienSo: ngu.bien,
        huongDi: ngu.huongDi,
        cong: ngu.cong,
        coAnh: !!ngu.anh,
        boAnh: !!ngu.boAnh,
        choDangNhap: kq.ketQua === 'cho-dang-nhap',
        vuotMayChu: !!ngu.vuotMayChu,
      })
      setAi(null)
      setForm((f) => ({ ...f, bienSo: '', loaiXe: '' }))
    } else if (kq.ketQua === 'chan-ban-sao') {
      setLoiLuu('Mất kết nối máy chủ và theo bản sao: ' + kq.lyDo + ' Chưa ghi lượt xe — gọi điều độ (màn Chốt bảo vệ có nút cho qua có đối soát).')
    } else if (kq.ketQua === 'khong-luu-duoc') {
      setLoiLuu('Mất kết nối máy chủ và trình duyệt không cho lưu tạm (' + (kq.loi?.message || 'bộ nhớ đầy') + ') — ghi phiếu tay.')
    } else {
      setLoiLuu(kq.loi?.message || 'Máy chủ từ chối lượt xe.')
    }
  }

  async function luu(e) {
    e.preventDefault()
    const bien = form.bienSo.trim().toUpperCase()
    if (!bien) return
    setDangLuu(true)
    setLoiLuu('')
    setGhiTam(null)
    setChuaXacNhan(null)
    try {
      const anh = ai?.anhXe && ai.anhXe.length <= ANH_TOI_DA_KY_TU ? ai.anhXe : null
      const ngu = {
        bien,
        huongDi: form.huongDi,
        cong: form.cong,
        anh,
        boAnh: !!ai?.anhXe && !anh,
        phu: { nguon: ai ? nguon : 'NhapTay', ocrTho: ai?.ocrTho ? ai.ocrTho.slice(0, 120) : null },
        lenhBiet: lenh.trangThai === 'co' && !lenh.banSao ? lenh.duLieu : null,
      }
      // Co mang: POST /api/cong/luot-xe kem Idempotency-Key (bam Luu 2 lan / gui lai khong ghi kep).
      // Mat mang / qua gio / 502-504: ghi vao hop thu di (IndexedDB, kem anh) roi tu dong bo. 4xx: hien do, KHONG xep hang.
      // XE RA khi dang co mang ma may chu khong tra loi kip: 'chua-xac-nhan' (do, Thu lai / Van cho qua) — khong tu xep hang.
      const kq = await ghiLuotXeTaiTram(
        {
          bienSo: bien,
          huongDi: form.huongDi,
          cong: form.cong,
          // Loai xe: lay dung chu nhan vien go (AI chi dien goi y)
          loaiXe: form.loaiXe.trim().slice(0, 30) || ai?.loaiXe || 'Phương tiện',
          maLenh: lenh.trangThai === 'co' ? lenh.duLieu.maLenh : null,
          soCont: lenh.trangThai === 'co' ? lenh.duLieu.soCont || null : null,
          anh,
          ghiChu: ai?.hongHoc ? `AI ngoại quan: móp méo/hư hại ~${ai.hongHoc.pct}% bề mặt` : null,
          nguoiThaoTac: taiKhoan?.hoTen || taiKhoan?.tenDangNhap || null,
          doTinCay: ai?.doTinCay ?? null,
        },
        { phu: ngu.phu, lenhBiet: ngu.lenhBiet, timeoutMs: 15000 },
      )
      xuLyKetQua(kq, ngu)
    } catch (err) {
      setLoiLuu(err.message)
    } finally {
      setDangLuu(false)
    }
  }

  /** Thu lai sau CHUA XAC NHAN: dung muc cu (cung maThaoTac) — may chu tra ket qua cu neu lan truoc da ghi */
  async function thuLaiXacNhan() {
    const xn = chuaXacNhan
    if (!xn) return
    setDangLuu(true)
    setLoiLuu('')
    try {
      xuLyKetQua(await ghiLuotXeTaiTram(null, { mucCu: xn.muc, phu: xn.phu, lenhBiet: xn.lenhBiet, timeoutMs: 15000 }), xn)
    } catch (err) {
      setLoiLuu(err.message)
    } finally {
      setDangLuu(false)
    }
  }

  /** Van cho qua sau CHUA XAC NHAN: xep hang muc cu kem vuotBanSao + lyDoVuot -> may chu gan can doi soat */
  async function vanChoQua() {
    const xn = chuaXacNhan
    if (!xn) return
    setDangLuu(true)
    setLoiLuu('')
    try {
      xuLyKetQua(
        await ghiLuotXeTaiTram(null, { mucCu: xn.muc, phu: xn.phu, vuotBanSao: true, lyDoVuot: LY_DO_VUOT_MAY_CHU_CHAM, xepHangLuon: true }),
        { ...xn, vuotMayChu: true },
      )
    } catch (err) {
      setLoiLuu(err.message)
    } finally {
      setDangLuu(false)
    }
  }

  useEffect(() => {
    if (!anhLon) return undefined
    const dong = (e) => e.key === 'Escape' && setAnhLon(null)
    document.addEventListener('keydown', dong)
    return () => document.removeEventListener('keydown', dong)
  }, [anhLon])

  const th = tongHop.duLieu
  const sanSang = engine.trangThai === 'san-sang'
  const duEngine = sanSang && engine.yolo && engine.cct && engine.tesseract

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-arrow-in-right" mau="la" so={th ? th.xeVao : '…'} nhan="Lượt xe vào hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-arrow-right" mau="cam" so={th ? th.xeRa : '…'} nhan="Lượt xe ra hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-truck" mau="xanh" so={th ? th.dangTrongCang : '…'} nhan="Xe đang trong cảng" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-bullseye" mau="tim" so={th ? dinhDangSo(th.tinCayTrungBinh, 1) + '%' : '…'} nhan="Độ tin cậy OCR trung bình" />
        </div>
      </div>

      {/* Cac man hinh cung luong cong — giu dung cac nut cua ban MVC (Nhat ky day du, Tram can, Chot bao ve) */}
      <div className="d-flex flex-wrap gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/gate/history">
          <i className="bi bi-clock-history me-1" />
          Nhật ký đầy đủ
        </Link>
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/gate/hosoxe">
          <i className="bi bi-truck me-1" />
          Hồ sơ xe · tài xế
        </Link>
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/gate/phieucan">
          <i className="bi bi-speedometer me-1" />
          Trạm cân · VGM
        </Link>
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/gatepass">
          <i className="bi bi-qr-code-scan me-1" />
          Lệnh giao nhận
        </Link>
        <Link className="btn btn-sm btn-outline-dark" to="/admin/gate/baove" title="Màn hình chữ to cho bảo vệ tại chốt — ghi được cả khi mất mạng">
          <i className="bi bi-shield-check me-1" />
          Chốt bảo vệ
        </Link>
      </div>

      <div className="row g-3">
        <div className="col-xl-7">
          <div className="card sp-the mb-3">
            <div className="card-header d-flex align-items-center flex-wrap gap-2">
              <span className="flex-grow-1">
                <i className="bi bi-camera-video me-2" />
                Camera làn vào · đọc biển số
              </span>
              {engine.trangThai === 'dang-tai' && (
                <span className="badge text-bg-light fw-normal">
                  <span className="spinner-border spinner-border-sm me-1" />
                  Đang tải YOLO26 + CCT-S v2 + Tesseract…
                </span>
              )}
              {sanSang && (
                <span className={'badge ' + (duEngine ? 'text-bg-success' : 'text-bg-warning')}>
                  <i className="bi bi-cpu-fill me-1" />
                  {[engine.yolo && 'YOLO26n', engine.cct && 'CCT-S v2', engine.tesseract && 'Tesseract'].filter(Boolean).join(' + ')}
                </span>
              )}
              {engine.trangThai === 'loi' && <span className="badge text-bg-danger">AI lỗi</span>}
            </div>
            <div className="card-body">
              {engine.trangThai === 'loi' && <div className="alert alert-danger py-2 small">{engine.loi}</div>}
              {loiAi && <div className="alert alert-danger py-2 small">{loiAi}</div>}

              <div className="sp-man-hinh-camera">
                <canvas ref={canvasRef} className={daCoAnh && !dangQuay ? '' : 'd-none'} />
                <video ref={videoRef} playsInline muted className={dangQuay ? '' : 'd-none'} />
                {!daCoAnh && !dangQuay && (
                  <div className="sp-camera-trong">
                    <i className="bi bi-camera" />
                    <div>Chọn ảnh mẫu, tải ảnh xe hoặc bật camera</div>
                  </div>
                )}
                {dangPhanTich && (
                  <div className="sp-camera-dang-doc">
                    <span className="spinner-border spinner-border-sm me-2" />
                    {buoc || 'Đang xử lý…'}
                  </div>
                )}
              </div>

              <div className="row g-2 mt-2">
                {ANH_MAU.map((m) => (
                  <div className="col-6 col-md-3" key={m.file}>
                    <button className={'btn btn-sm w-100 ' + m.lop} disabled={!sanSang || dangPhanTich} onClick={() => chonAnhMau(m)} title={'Biển ' + m.bien}>
                      <i className={'bi ' + m.icon + ' me-1'} />
                      {m.ten}
                    </button>
                  </div>
                ))}
                <div className="col-md-6">
                  <input ref={fileRef} type="file" accept="image/*" className="form-control form-control-sm" disabled={!sanSang || dangPhanTich} onChange={chonFile} />
                </div>
                <div className="col-6 col-md-3">
                  <button className="btn btn-sm btn-dark w-100" disabled={!sanSang || dangPhanTich} onClick={dangQuay ? tatCamera : batCamera}>
                    <i className={'bi me-1 ' + (dangQuay ? 'bi-stop-circle' : 'bi-camera-video')} />
                    {dangQuay ? 'Tắt camera' : 'Bật camera'}
                  </button>
                </div>
                <div className="col-6 col-md-3">
                  {dangQuay && (
                    <button className="btn btn-sm btn-warning w-100" onClick={chupVaDoc}>
                      <i className="bi bi-camera me-1" />
                      Chụp &amp; đọc
                    </button>
                  )}
                </div>
              </div>
            </div>
          </div>

          <form className="card sp-the sp-o-bien-so mb-3" onSubmit={luu}>
            <div className="card-body">
              <div className="row g-3 align-items-start">
                {ai && (
                  <div className="col-md-4 text-center">
                    <div className="small text-muted fw-semibold mb-1">VÙNG BIỂN SỐ</div>
                    <img src={ai.anhVungBien} alt="Vùng biển số" className="sp-anh-vung-bien" />
                    <div className="small mt-1">
                      {ai.mauBien === 'vang' ? 'Nền vàng · xe kinh doanh vận tải' : ai.mauBien === 'trang' ? 'Nền trắng' : 'Chưa khoanh được vùng biển'}
                    </div>
                  </div>
                )}
                <div className={ai ? 'col-md-8' : 'col-12'}>
                  <KetQuaLenh lenh={lenh} />
                  {ai?.hongHoc && (
                    <div className="alert alert-danger py-2 small mb-2">
                      <i className="bi bi-exclamation-octagon-fill me-1" />
                      Phát hiện <b>móp méo / hư hại ~{ai.hongHoc.pct}%</b> bề mặt — đã khoanh đỏ, kiểm tra trước khi cho vào.
                    </div>
                  )}
                  <label className="small text-muted fw-semibold" htmlFor="bienSo">
                    BIỂN SỐ {ai ? '(AI đọc — sửa nếu sai)' : '(nhập tay hoặc dùng AI)'}
                  </label>
                  <input
                    id="bienSo"
                    className="form-control form-control-lg sp-o-nhap-bien"
                    maxLength={15}
                    value={form.bienSo}
                    onChange={(e) => setForm({ ...form, bienSo: e.target.value.toUpperCase() })}
                    placeholder="51C-638.72"
                    required
                  />
                  {ai && (
                    <div className="small mt-1 d-flex flex-wrap gap-2 align-items-center">
                      <span>
                        Tin cậy: <b>{ai.doTinCay != null ? ai.doTinCay + '%' : '—'}</b>
                      </span>
                      <NhanKetQuaAi ai={ai} />
                      {ai.khac && <span className="text-warning-emphasis">engine kia đọc: “{ai.khac}”</span>}
                      {!ai.coXe && <span className="text-muted">YOLO không thấy xe — đã đọc toàn khung</span>}
                    </div>
                  )}
                  <div className="row g-2 mt-1">
                    <div className="col-sm-6">
                      <input
                        className="form-control form-control-sm"
                        list="goiYLoaiXe"
                        maxLength={30}
                        placeholder="Loại xe"
                        value={form.loaiXe}
                        onChange={(e) => setForm({ ...form, loaiXe: e.target.value })}
                      />
                      <datalist id="goiYLoaiXe">
                        {LOAI_XE_GOI_Y.map((x) => (
                          <option key={x} value={x} />
                        ))}
                      </datalist>
                    </div>
                    <div className="col-3 col-sm-2">
                      <select className="form-select form-select-sm" value={form.cong} onChange={(e) => setForm({ ...form, cong: e.target.value })} aria-label="Cổng">
                        <option>A1</option>
                        <option>A2</option>
                        <option>B1</option>
                      </select>
                    </div>
                    <div className="col-3 col-sm-2">
                      <select className="form-select form-select-sm" value={form.huongDi} onChange={(e) => setForm({ ...form, huongDi: e.target.value })} aria-label="Hướng">
                        <option>Vào</option>
                        <option>Ra</option>
                      </select>
                    </div>
                    <div className="col-6 col-sm-2 d-grid">
                      <button className="btn btn-sm btn-warning fw-semibold" disabled={dangLuu || !form.bienSo.trim()}>
                        {dangLuu ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-save" />} Lưu
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              {loiLuu && <div className="alert alert-danger py-2 small mt-3 mb-0">{loiLuu}</div>}
              {chuaXacNhan && (
                <div className="alert alert-danger py-2 small mt-3 mb-0" role="alert">
                  <i className="bi bi-sign-stop-fill me-1" />
                  <b>CHƯA XÁC NHẬN ĐƯỢC — CHƯA CHO XE RA</b> — {chuaXacNhan.bien} (cổng {chuaXacNhan.cong}).{' '}
                  {chuaXacNhan.lyDo === 'het-phien'
                    ? 'Phiên đăng nhập hết hạn — đăng nhập lại rồi thử lại.'
                    : chuaXacNhan.lyDo === 'dang-xu-ly'
                      ? 'Máy chủ đang xử lý lần bấm trước — đợi vài giây rồi thử lại.'
                      : (chuaXacNhan.thongBao || 'Máy chủ không trả lời kịp.') + ' Chưa biết lệnh / eDO / hải quan có hợp lệ không; lượt xe CHƯA được ghi.'}
                  <div className="d-flex flex-wrap gap-2 mt-2">
                    <button type="button" className="btn btn-sm btn-danger" disabled={dangLuu} onClick={thuLaiXacNhan}>
                      <i className="bi bi-arrow-clockwise me-1" />
                      Thử lại
                    </button>
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-danger"
                      disabled={dangLuu}
                      onClick={vanChoQua}
                      title="Chỉ khi điều độ đồng ý — ghi tạm và gắn cờ cần đối soát"
                    >
                      Vẫn cho qua (ghi tạm, cần đối soát)
                    </button>
                  </div>
                </div>
              )}
              {ghiTam && (
                <div className="alert alert-warning py-2 small mt-3 mb-0" role="status">
                  <i className="bi bi-hdd me-1" />
                  <b>Đã ghi tạm trong máy</b> — {ghiTam.bienSo} ({ghiTam.huongDi}, cổng {ghiTam.cong}){ghiTam.coAnh ? ', kèm ảnh xe' : ''}.{' '}
                  {ghiTam.choDangNhap
                    ? 'Phiên đăng nhập hết hạn: đăng nhập lại để gửi.'
                    : ghiTam.vuotMayChu
                      ? 'Cho qua khi máy chủ chưa trả lời — tự gửi, máy chủ gắn CẦN ĐỐI SOÁT.'
                      : 'Mất kết nối máy chủ — tự gửi khi có mạng lại, không gửi trùng.'}
                  {ghiTam.boAnh && <div>Ảnh xe quá lớn nên không lưu kèm lượt ghi tạm.</div>}
                </div>
              )}
              {ketQuaLuu && <TienTrinhSuKien ketQua={ketQuaLuu} />}
            </div>
          </form>

          <div className="card sp-the">
            <div className="card-header d-flex align-items-center">
              <span className="flex-grow-1">
                <i className="bi bi-card-checklist me-2" />
                Lệnh giao nhận đang mở
              </span>
              <span className="small text-muted">{lenhMo.duLieu ? lenhMo.duLieu.length + ' lệnh' : ''}</span>
            </div>
            <div className="table-responsive">
              <table className="table table-sm table-hover align-middle mb-0 sp-bang">
                <thead>
                  <tr>
                    <th>Lệnh</th>
                    <th>Biển số</th>
                    <th>Container</th>
                    <th>Giờ hẹn</th>
                    <th>Trạng thái</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {lenhMo.duLieu?.length === 0 && (
                    <tr>
                      <td colSpan={6} className="text-center text-muted small py-3">
                        Không có lệnh nào đang chờ xe.
                      </td>
                    </tr>
                  )}
                  {lenhMo.duLieu?.map((l) => (
                    <tr key={l.maLenh}>
                      <td>
                        <div className="fw-semibold">{l.soLenh}</div>
                        <div className="small text-muted">{l.loai}</div>
                      </td>
                      <td>
                        <span className="sp-bien-so">{l.bienSoXe || '—'}</span>
                      </td>
                      <td className="small">{l.soCont || '—'}</td>
                      <td className="small">
                        {dinhDangNgayGio(l.henTuLuc)}
                        <br />→ {dinhDangNgayGio(l.henDenLuc)}
                      </td>
                      <td>
                        <span className="badge text-bg-light">{l.trangThai}</span>
                        {/* Cung quy tac voi CongService.DoiChieuLenh: qua gio hen 2 tieng thi cong khong khop lenh nua */}
                        {new Date(l.henDenLuc) < Date.now() - 2 * 3600 * 1000 && (
                          <div>
                            <span className="badge text-bg-danger" title="Quá giờ hẹn hơn 2 tiếng — cổng sẽ không tự khớp lệnh này">
                              quá giờ hẹn
                            </span>
                          </div>
                        )}
                      </td>
                      <td className="text-end">
                        <button type="button" className="btn btn-sm btn-outline-primary" disabled={!l.bienSoXe} onClick={() => chonLenh(l)}>
                          Chọn
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="col-xl-5">
          <div className="card sp-the">
            <div className="card-header d-flex align-items-center">
              <span className="flex-grow-1">
                <i className="bi bi-list-check me-2" />
                Danh sách xe qua cổng
              </span>
              <span className="small text-muted">30 lượt gần nhất</span>
            </div>
            <div className="sp-bang-cuon">
              <table className="table table-sm table-hover align-middle mb-0 sp-bang">
                <thead>
                  <tr>
                    <th>Ảnh</th>
                    <th>Biển số</th>
                    <th>Cổng</th>
                    <th>Dock</th>
                    <th>Loại xe</th>
                    <th className="text-end">Phí</th>
                    <th>Giờ</th>
                  </tr>
                </thead>
                <tbody>
                  {luotXe.duLieu?.map((x) => (
                    <tr key={x.maXV}>
                      <td>
                        {x.anh ? (
                          <img
                            src={duongDanAnh(x.anh)}
                            className="sp-anh-nho"
                            alt={'Ảnh xe ' + x.bienSo}
                            title="Bấm để xem ảnh lớn"
                            onClick={() => setAnhLon({ src: duongDanAnh(x.anh), bien: x.bienSo })}
                          />
                        ) : (
                          <span className="text-muted small">—</span>
                        )}
                      </td>
                      <td>
                        <Link className="sp-bien-so text-decoration-none" to={'/admin/gate/hosoxe?bienSo=' + encodeURIComponent(x.bienSo)} title={'Hồ sơ xe ' + x.bienSo}>
                          {x.bienSo}
                        </Link>
                        {x.soCont && (
                          <div>
                            <Link className="small" to={'/admin/containers/tracuu?so=' + x.soCont}>
                              <i className="bi bi-box-seam" /> {x.soCont}
                            </Link>
                          </div>
                        )}
                        {x.ghiChu?.includes('móp méo') && (
                          <div>
                            <span className="badge text-bg-danger" title={x.ghiChu}>
                              ⚠ móp méo
                            </span>
                          </div>
                        )}
                      </td>
                      <td>
                        {x.cong}
                        <div>
                          <span className={'badge ' + (x.huongDi === 'Vào' ? 'text-bg-success' : 'text-bg-warning')}>{x.huongDi}</span>
                        </div>
                      </td>
                      <td>{x.dock ? <span className="badge sp-badge-dock">{x.dock}</span> : <span className="text-muted small">—</span>}</td>
                      <td className="small">{x.loaiXe || '—'}</td>
                      <td className="small text-end text-nowrap" title={x.hinhThucThu || ''}>
                        {x.phiCong != null ? dinhDangTien(x.phiCong) : <span className="text-muted">—</span>}
                      </td>
                      <td className="small text-muted text-nowrap">{gioNgayNgan(x.thoiGian)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      {anhLon && (
        <div className="sp-lop-phu" onClick={(e) => e.target === e.currentTarget && setAnhLon(null)} role="dialog" aria-label="Ảnh xe">
          <div className="sp-hop-anh">
            <div className="d-flex align-items-center mb-2">
              <h2 className="h6 mb-0 flex-grow-1">Ảnh xe {anhLon.bien}</h2>
              <button className="btn btn-sm btn-outline-secondary" onClick={() => setAnhLon(null)} aria-label="Đóng">
                <i className="bi bi-x-lg" />
              </button>
            </div>
            <img src={anhLon.src} alt={'Ảnh xe ' + anhLon.bien} />
          </div>
        </div>
      )}
    </>
  )
}
