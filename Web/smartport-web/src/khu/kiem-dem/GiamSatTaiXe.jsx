import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { AI_MAU } from '../../ai/taiThuVien'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung } from '../../components/ui'
import { gioNgayNgan } from '../../utils/dinhDang'
import { beep, dungAmThanh, huCoi, noiCanhBao, taoHanChe } from './chung/amThanh'
import { guiSuCoTaiXe } from './chung/bangChung'
import { dongCamera, moCamera, veLenCanvas } from './chung/camera'
import { dacTrung, diemBlend, gocTuLandmark, khoiDongKhuonMat, nhanDienKhuonMat, veDiemMoc } from './chung/khuonMat'
import { timThungVaGoc } from './chung/mauSac'
import { taoGiamSatQuayDau, tenHuong } from './chung/quayDau'
import { taoTuHoc } from './chung/tuHoc'
import { MAU, chuanBiNet, veHop } from './chung/ve'
import { khoiDongYolo, phatHienYolo } from './chung/yolo'
import './kiem-dem.css'

const ANH_MAU_TAI_XE = AI_MAU + 'dms-demo-face.jpg?v=1'
const ANH_CARGO_NGAY = AI_MAU + 'cargo-ngay.png?v=2'
const ANH_CARGO_NGHIENG = AI_MAU + 'cargo-nghieng.png?v=2'
const VI_TRI_CABIN = 'Cabin xe — camera DMS'
const VI_TRI_KHOANG = 'Khoang hàng — camera cargo'
const LOP_NGUOI = 0
const LOP_DIEN_THOAI = 67
/** Goc nghieng cua kien hang vuot nguong nay la nguy co do vo */
const NGUONG_NGHIENG = 25

const taiAnh = (url) =>
  new Promise((ok, loi) => {
    const img = new Image()
    img.crossOrigin = 'anonymous'
    img.onload = () => ok(img)
    img.onerror = () => loi(new Error('Không tải được ảnh mẫu.'))
    img.src = url
  })

/**
 * GIÁM SÁT TÀI XẾ & KHOANG HÀNG — In-Cabin + Cargo AI (thay Admin/Driver của bản MVC).
 * Camera 1: MediaPipe FaceLandmarker (ngủ gật, ngáp, mất tập trung, điện thoại, AI tự học
 * trạng thái lạ). Camera 2: YOLO26 + PCA cụm màu carton (hàng nghiêng, người xâm nhập).
 * Mọi suy luận chạy trên trình duyệt; chỉ ẢNH BẰNG CHỨNG của sự cố mới gửi về máy chủ.
 */
export default function GiamSatTaiXe() {
  const tongHop = useApi('/api/giam-sat-tai-xe')

  const [chuDms, setChuDms] = useState('Đang tải mô hình khuôn mặt…')
  const [chuCargo, setChuCargo] = useState('Đang tải YOLO26…')
  const [matSanSang, setMatSanSang] = useState(false)
  const [yoloSanSang, setYoloSanSang] = useState(false)
  const [dmsBat, setDmsBat] = useState(false)
  const [cgBat, setCgBat] = useState(false)
  const [xeDung, setXeDung] = useState(true)
  const [hud, setHud] = useState({ mat: '—', ngap: 0, quay: '—', phone: '—' })
  const [dmsKq, setDmsKq] = useState({ chu: 'Chưa chạy.', mau: 'text-body-secondary' })
  const [canhBao, setCanhBao] = useState('')
  const [tuHocUi, setTuHocUi] = useState({ chu: 'chờ bật camera…', mau: 'text-body-secondary', pct: 0, nen: '#38bdf8' })
  const [hienPhanHoi, setHienPhanHoi] = useState(false)
  const [cgKq, setCgKq] = useState([])
  const [dmsCoHinh, setDmsCoHinh] = useState(false)
  const [cgCoHinh, setCgCoHinh] = useState(false)
  const [dangQuet, setDangQuet] = useState(false)

  const dmsVideoRef = useRef(null)
  const dmsCanvasRef = useRef(null)
  const cgVideoRef = useRef(null)
  const cgCanvasRef = useRef(null)
  const dmsLuongRef = useRef(null)
  const cgLuongRef = useRef(null)
  const dmsHenRef = useRef(0)
  const cgHenRef = useRef(0)
  const canhBaoHenRef = useRef(0)
  const phanHoiHenRef = useRef(0)
  const giuChuHenRef = useRef(0)
  const conGanRef = useRef(true)
  const xeDungRef = useRef(true)
  xeDungRef.current = xeDung
  const cgAnhRef = useRef(null) // anh tinh dang phan tich (null = khung hinh camera)

  const hanCheRef = useRef(taoHanChe())
  const quayDauRef = useRef(taoGiamSatQuayDau())
  const tuHocRef = useRef(null)
  const dmsRef = useRef({
    matNhamTu: 0,
    ngapBatDau: 0,
    ngapMoc: [],
    phoneDem: 0,
    phoneLuc: 0,
    phoneCo: false,
    truoc: { yaw: 0, pitch: 0 },
    theHe: 0,
    chay: false,
  })
  const cargoRef = useRef({ theHe: 0, chay: false })

  /* ---------------- Nạp 2 engine AI ---------------- */
  useEffect(() => {
    conGanRef.current = true
    khoiDongKhuonMat()
      .then(() => {
        if (!conGanRef.current) return
        setMatSanSang(true)
        setChuDms('FaceLandmarker sẵn sàng (478 điểm mốc + blendshapes).')
      })
      .catch(() => conGanRef.current && setChuDms('Không tải được mô hình khuôn mặt — kiểm tra thư mục Content/models.'))
    khoiDongYolo()
      .then(() => {
        if (!conGanRef.current) return
        setYoloSanSang(true)
        setChuCargo('YOLO26 sẵn sàng.')
      })
      .catch(() => conGanRef.current && setChuCargo('YOLO lỗi — vẫn đo nghiêng bằng phân tích màu (không phát hiện người/điện thoại).'))
    return () => {
      // Roi man hinh: tat 2 camera, huy moi vong lap va am thanh dang phat
      conGanRef.current = false
      dmsRef.current.chay = false
      dmsRef.current.theHe++
      cargoRef.current.chay = false
      cargoRef.current.theHe++
      clearTimeout(dmsHenRef.current)
      clearTimeout(cgHenRef.current)
      clearTimeout(canhBaoHenRef.current)
      clearTimeout(phanHoiHenRef.current)
      clearTimeout(giuChuHenRef.current)
      dongCamera(dmsLuongRef.current, dmsVideoRef.current)
      dongCamera(cgLuongRef.current, cgVideoRef.current)
      dmsLuongRef.current = null
      cgLuongRef.current = null
      dungAmThanh()
    }
  }, [])

  /** Bao Phong an ninh + luu anh bang chung, roi lam moi bang su co */
  function baoSuCo(loai, mucDo, viTri, moTa, canvas) {
    guiSuCoTaiXe(canvas, { loai, mucDo, viTri, moTa })
      .then(() => conGanRef.current && tongHop.taiLai({ im: true }))
      .catch(() => {
        /* mat mang: khong chan vong lap giam sat */
      })
  }

  function hienCanhBao(chu, ms = 3500) {
    setCanhBao(chu)
    clearTimeout(canhBaoHenRef.current)
    canhBaoHenRef.current = setTimeout(() => conGanRef.current && setCanhBao(''), ms)
  }

  function moPhanHoi() {
    setHienPhanHoi(true)
    clearTimeout(phanHoiHenRef.current)
    phanHoiHenRef.current = setTimeout(() => conGanRef.current && setHienPhanHoi(false), 30000)
  }

  /* ============================================================
     CAMERA 1 — DMS
     ============================================================ */
  function veTuHoc(kq) {
    if (giuChuHenRef.current) return // dang giu thong bao phan hoi vai giay
    if (kq.pha === 'hoc') {
      setTuHocUi({
        chu: (kq.tuHocLai ? 'môi trường thay đổi — AI tự học lại nền… ' : 'đang học tư thế lái bình thường… ') + kq.tienDo + '%',
        mau: 'text-info',
        pct: kq.tienDo,
        nen: '#38bdf8',
      })
      return
    }
    const pct = Math.min(100, Math.round((kq.ema / (kq.nguong * 1.6)) * 100))
    setTuHocUi({
      chu: kq.bat
        ? 'BẤT THƯỜNG! độ lệch ' + kq.ema.toFixed(4) + ' > ngưỡng ' + kq.nguong.toFixed(4) + (kq.giay ? ' · ' + kq.giay.toFixed(1) + 's' : '')
        : 'đang giám sát · độ lệch ' + kq.ema.toFixed(4) + ' / ngưỡng ' + kq.nguong.toFixed(4),
      mau: kq.bat ? 'text-danger fw-semibold' : 'text-body-secondary',
      pct,
      nen: kq.bat ? MAU.loi : pct > 45 ? MAU.chuY : MAU.dat,
    })
  }

  /** Xu ly 1 khung hinh tai xe (goi tu vong lap camera hoac tu anh mau) */
  function dmsXuLy(nguon, w, h, laAnhMau) {
    const cv = dmsCanvasRef.current
    if (!veLenCanvas(cv, nguon, w, h)) return
    const ctx = cv.getContext('2d', { willReadFrequently: true })
    const res = nhanDienKhuonMat(nguon)
    const now = Date.now()
    const dms = dmsRef.current
    const hanChe = hanCheRef.current

    if (!res?.faceLandmarks?.length) {
      setHud((c) => ({ ...c, mat: '—', quay: '—' }))
      if (laAnhMau) {
        setDmsKq({ chu: 'Không thấy khuôn mặt trong ảnh mẫu.', mau: 'text-body-secondary' })
        return
      }
      // Xoa moc thoi gian cu — khong duoc de "nham mat 1 khung + mat dau vai phut
      // + nham mat 1 khung" bien thanh mot bao ngu gat oan
      dms.matNhamTu = 0
      dms.ngapBatDau = 0
      tuHocRef.current?.ngat()
      const g = quayDauRef.current.matMat(now)
      if (g.lech) {
        setDmsKq({ chu: 'Mất dấu mặt khi đang quay đầu — ' + g.giay.toFixed(1) + 's / 5s', mau: 'text-warning' })
        if (g.baoDong && hanChe('MatTapTrung', 25000)) {
          huCoi(2.2, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
          noiCanhBao('Cảnh báo. Không thấy mặt tài xế, có thể đang quay đầu quá lâu.', 1.08)
          hienCanhBao('MẤT TẬP TRUNG — KHÔNG THẤY MẶT TÀI XẾ', 3000)
          baoSuCo(
            'MatTapTrung',
            'Chú ý',
            VI_TRI_CABIN,
            'DMS: mất dấu khuôn mặt khi đang quay đầu, tổng ' + g.giay.toFixed(1) + 's rời mắt khỏi đường',
            cv,
          )
        }
      } else {
        setDmsKq({ chu: 'Chưa thấy khuôn mặt trong khung.', mau: 'text-body-secondary' })
      }
      return
    }

    const lm = res.faceLandmarks[0]
    const cats = res.faceBlendshapes?.[0]?.categories || []
    const nham = (diemBlend(cats, 'eyeBlinkLeft') + diemBlend(cats, 'eyeBlinkRight')) / 2
    const mieng = diemBlend(cats, 'jawOpen')
    const matMo = Math.round((1 - nham) * 100)
    const goc = gocTuLandmark(lm)
    veDiemMoc(ctx, lm, w, h)
    setHud((c) => ({ ...c, mat: matMo + '%', quay: Math.round(Math.abs(goc.yaw)) + '°' }))

    if (laAnhMau) {
      setDmsKq({ chu: 'Tỉnh táo · mắt mở ' + matMo + '% · yaw ' + Math.round(goc.yaw) + '°', mau: 'text-success fw-semibold' })
      return
    }

    let trangThai = 'Tỉnh táo'
    let mauTT = 'text-success fw-semibold'

    // ---- 1. NGU GAT: mat nham keo dai HOAC dau guc xuong ----
    const nguDauHieu = nham > 0.52 || goc.pitch > 34
    if (nguDauHieu) {
      if (!dms.matNhamTu) dms.matNhamTu = now
      if (now - dms.matNhamTu > 1400) {
        trangThai = 'NGỦ GẬT'
        mauTT = 'text-danger fw-semibold'
        if (hanChe('NguGat', 20000)) {
          huCoi(2.6, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
          noiCanhBao('Cảnh báo! Tài xế có dấu hiệu ngủ gật. Hãy dừng xe nghỉ ngơi ngay.', 1.08)
          hienCanhBao('TÀI XẾ NGỦ GẬT — DỪNG XE NGAY')
          baoSuCo('NguGat', 'Nguy hiểm', VI_TRI_CABIN, 'DMS: tài xế nhắm mắt/gục đầu ' + ((now - dms.matNhamTu) / 1000).toFixed(1) + 's', cv)
        }
      }
    } else dms.matNhamTu = 0

    // ---- 2. NGAP: jawOpen giu > 0,55; >= 3 lan trong 60s = met moi ----
    if (mieng > 0.55) {
      if (!dms.ngapBatDau) dms.ngapBatDau = now
      if (now - dms.ngapBatDau > 550 && hanChe('demNgap', 3000)) dms.ngapMoc.push(now)
    } else dms.ngapBatDau = 0
    dms.ngapMoc = dms.ngapMoc.filter((t) => now - t < 60000)
    setHud((c) => ({ ...c, ngap: dms.ngapMoc.length }))
    if (dms.ngapMoc.length >= 3 && hanChe('MetMoi', 45000)) {
      beep()
      noiCanhBao('Tài xế ngáp nhiều lần. Có dấu hiệu mệt mỏi, nên nghỉ giải lao.', 1.08)
      baoSuCo('MatTapTrung', 'Chú ý', VI_TRI_CABIN, 'DMS: ngáp ' + dms.ngapMoc.length + ' lần trong 1 phút — dấu hiệu mệt mỏi', cv)
    }

    // ---- 3. MAT TAP TRUNG: quay dau BAT KY huong nao >= 5 giay ----
    const g = quayDauRef.current.buoc(goc, now)
    if (g.lech && g.huong) {
      const doLech = Math.round(Math.max(Math.abs(goc.yaw), Math.abs(goc.pitch), Math.abs(goc.roll)))
      setHud((c) => ({ ...c, quay: doLech + '° ' + g.huong }))
      if (g.giay >= 1.2 && !g.baoDong && trangThai === 'Tỉnh táo') {
        trangThai = 'Nhìn ' + tenHuong(g.huong) + ' ' + g.giay.toFixed(1) + 's / 5s'
        mauTT = 'text-warning fw-semibold'
      }
    }
    // Khi dang la NGU GAT thi de luat ngu gat "so huu" su kien — khong bao trung
    // MAT TAP TRUNG cho cung mot cai guc dau
    if (g.baoDong && !nguDauHieu) {
      if (trangThai === 'Tỉnh táo' || mauTT.startsWith('text-warning')) {
        trangThai = 'MẤT TẬP TRUNG'
        mauTT = 'text-danger fw-semibold'
      }
      if (hanChe('MatTapTrung', 25000)) {
        huCoi(2.2, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
        noiCanhBao('Cảnh báo. Tài xế nhìn ' + tenHuong(g.huong) + ' quá lâu. Hãy tập trung nhìn đường.', 1.08)
        hienCanhBao('MẤT TẬP TRUNG — NHÌN ' + tenHuong(g.huong).toUpperCase() + ' ' + g.giay.toFixed(0) + ' GIÂY', 3000)
        baoSuCo('MatTapTrung', 'Chú ý', VI_TRI_CABIN, 'DMS: tài xế nhìn ' + tenHuong(g.huong) + ' liên tục ' + g.giay.toFixed(1) + 's (ngưỡng 5s)', cv)
      }
    }

    // ---- 3b. AI TU HOC: mang tu ma hoa phat hien trang thai LA ----
    // luatBat: luat cung dang bao -> (1) khong hoc khung nay lam "binh thuong",
    // (2) khong bao trung — AI tu hoc chi bao cai ma luat cung KHONG bat duoc
    const luatBat = nguDauHieu || dms.phoneCo || (g.lech && g.giay >= 1.2)
    if (!tuHocRef.current) tuHocRef.current = taoTuHoc()
    const kqAno = tuHocRef.current.buoc(dacTrung(goc, nham, mieng, lm, dms.truoc), now, luatBat)
    veTuHoc(kqAno)
    if (kqAno.baoDong && !luatBat && hanChe('TrangThaiLa', 30000)) {
      if (trangThai === 'Tỉnh táo') {
        trangThai = 'TRẠNG THÁI LẠ'
        mauTT = 'text-danger fw-semibold'
      }
      beep()
      noiCanhBao('Cảnh báo. AI phát hiện trạng thái bất thường của tài xế.', 1.08)
      hienCanhBao('AI TỰ HỌC: TRẠNG THÁI BẤT THƯỜNG', 3200)
      baoSuCo(
        'TrangThaiLa',
        'Chú ý',
        VI_TRI_CABIN,
        'DMS: mạng tự mã hóa phát hiện trạng thái lạ (sai số ' + kqAno.ema.toFixed(4) + ' > ngưỡng ' + kqAno.nguong.toFixed(4) + ')',
        cv,
      )
      moPhanHoi()
    }
    dms.truoc = { yaw: goc.yaw, pitch: goc.pitch }

    // ---- 4. DIEN THOAI: YOLO lop 67, kiem tra moi 1,6 giay ----
    if (yoloSanSang && now - dms.phoneLuc > 1600) {
      dms.phoneLuc = now
      phatHienYolo(cv, [LOP_DIEN_THOAI])
        .then((hops) => {
          if (!conGanRef.current) return
          if (hops.length) {
            dms.phoneDem++
            const ctx2 = chuanBiNet(cv, { chiaNet: 300, chiaChu: 46 })
            veHop(ctx2, hops[0], MAU.chuY, 'ĐIỆN THOẠI')
            if (dms.phoneDem >= 2 && hanChe('DungDienThoai', 30000)) {
              huCoi(1.8, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
              noiCanhBao('Cảnh báo! Tài xế đang dùng điện thoại khi lái xe.', 1.08)
              hienCanhBao('DÙNG ĐIỆN THOẠI KHI LÁI XE')
              baoSuCo(
                'DungDienThoai',
                'Nguy hiểm',
                VI_TRI_CABIN,
                'DMS: phát hiện điện thoại trong cabin (YOLO ' + Math.round(hops[0].score * 100) + '%)',
                cv,
              )
            }
          } else dms.phoneDem = 0
          dms.phoneCo = hops.length > 0
          setHud((c) => ({ ...c, phone: dms.phoneCo ? 'CÓ' : '—' }))
        })
        .catch(() => {})
    }

    setDmsKq({ chu: trangThai, mau: mauTT })
  }

  /** Vong lap camera tai xe ~9 fps (du nhay ma nhe CPU) */
  function dmsVong(theHe) {
    const dms = dmsRef.current
    if (!dms.chay || theHe !== dms.theHe || !conGanRef.current) return
    const v = dmsVideoRef.current
    if (v?.readyState >= 2) {
      try {
        dmsXuLy(v, v.videoWidth, v.videoHeight, false)
      } catch {
        /* 1 khung hinh loi khong duoc lam chet ca vong lap */
      }
    }
    dmsHenRef.current = setTimeout(() => dmsVong(theHe), 110)
  }

  async function doiCamTaiXe() {
    const dms = dmsRef.current
    if (dmsLuongRef.current) {
      dms.chay = false
      dms.theHe++
      clearTimeout(dmsHenRef.current)
      dongCamera(dmsLuongRef.current, dmsVideoRef.current)
      dmsLuongRef.current = null
      quayDauRef.current.datLai()
      tuHocRef.current?.ngat()
      setDmsBat(false)
      setChuDms('Đã tắt camera tài xế.')
      return
    }
    try {
      dmsLuongRef.current = await moCamera(dmsVideoRef.current, 960)
      Object.assign(dms, { matNhamTu: 0, ngapBatDau: 0, phoneDem: 0, chay: true, theHe: dms.theHe + 1 })
      quayDauRef.current.datLai()
      // Phien camera moi: hoc lai nen (tai xe / anh sang co the da khac) —
      // trong so mang van giu nen hoc lai rat nhanh
      tuHocRef.current?.hocLai()
      setDmsBat(true)
      setDmsCoHinh(true)
      setChuDms('Đang giám sát — khoảng 20 giây đầu AI tự học tư thế lái bình thường, hãy nhìn thẳng tự nhiên.')
      dmsVong(dms.theHe)
    } catch (e) {
      setChuDms(e.message)
    }
  }

  async function anhMauTaiXe() {
    try {
      const img = await taiAnh(ANH_MAU_TAI_XE)
      setDmsCoHinh(true)
      dmsXuLy(img, img.naturalWidth, img.naturalHeight, true)
      setChuDms('Đã phân tích ảnh mẫu tài xế.')
    } catch (e) {
      setChuDms(e.message)
    }
  }

  function phanHoiTuHoc(dung) {
    const ai = tuHocRef.current
    if (!ai) return
    const k = ai.phanHoi(dung)
    setHienPhanHoi(false)
    setTuHocUi((c) => ({
      ...c,
      chu: dung ? 'đã ghi nhận cảnh báo ĐÚNG — AI nhạy hơn (k=' + k.toFixed(1) + ')' : 'đã học lại vùng vừa báo — nâng ngưỡng (k=' + k.toFixed(1) + ')',
      mau: 'text-success',
    }))
    clearTimeout(giuChuHenRef.current)
    giuChuHenRef.current = setTimeout(() => {
      giuChuHenRef.current = 0
    }, 4000)
  }

  /* ============================================================
     CAMERA 2 — KHOANG HANG
     ============================================================ */
  async function cargoPhanTich() {
    const cv = cgCanvasRef.current
    if (!cv?.width) {
      setChuCargo('Chưa có hình — chọn ảnh mẫu hoặc bật camera khoang.')
      return
    }
    setDangQuet(true)
    try {
      // Ve lai nguon truoc khi phan tich (xoa overlay cu)
      if (cgAnhRef.current) veLenCanvas(cv, cgAnhRef.current, cv.width, cv.height)
      const thungs = timThungVaGoc(cv)
      const ctx = chuanBiNet(cv, { chiaNet: 260, chiaChu: 48 })
      let soNghieng = 0
      let gocMax = 0
      for (const t of thungs) {
        const xau = t.nghieng >= NGUONG_NGHIENG
        if (xau) {
          soNghieng++
          gocMax = Math.max(gocMax, t.nghieng)
        }
        veHop(ctx, t, xau ? MAU.loi : MAU.dat, xau ? 'NGHIÊNG ~' + t.nghieng + '°' : 'Ổn định · ' + t.nghieng + '°')
      }

      let nguoi = []
      if (yoloSanSang && xeDungRef.current) nguoi = await phatHienYolo(cv, [LOP_NGUOI]).catch(() => [])
      for (const b of nguoi) veHop(ctx, b, MAU.chuY, 'NGƯỜI TRONG KHOANG', { netDut: true })
      if (!conGanRef.current) return

      const dong = []
      if (soNghieng > 0) {
        dong.push({ mau: 'text-danger', chu: soNghieng + ' kiện NGHIÊNG (~' + gocMax + '°)' })
        if (hanCheRef.current('HangNghieng', 20000)) {
          huCoi(2.4, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
          noiCanhBao('Cảnh báo! Hàng trên xe bị nghiêng ' + gocMax + ' độ. Nguy cơ đổ vỡ.', 1.08)
          baoSuCo('HangNghieng', 'Nguy hiểm', VI_TRI_KHOANG, 'Cargo: ' + soNghieng + ' kiện hàng nghiêng ~' + gocMax + '° (ngưỡng 25°)', cv)
        }
      }
      if (nguoi.length) {
        dong.push({ mau: 'text-warning', chu: 'Phát hiện NGƯỜI trong khoang' })
        if (hanCheRef.current('XamNhap', 25000)) {
          huCoi(2.4, { tanSo: 660, nhip: 3.2, sau: 210, bienDo: 0.2 })
          noiCanhBao('Cảnh báo an ninh! Phát hiện người xâm nhập khoang hàng khi xe đang dừng.', 1.08)
          baoSuCo('XamNhap', 'Nguy hiểm', VI_TRI_KHOANG, 'Cargo: phát hiện người trong khoang hàng khi xe dừng', cv)
        }
      }
      if (!dong.length) dong.push({ mau: 'text-success', chu: thungs.length + ' kiện ổn định — khoang hàng an toàn' })
      setCgKq(dong)
      setChuCargo('Đã phân tích: ' + thungs.length + ' kiện hàng' + (nguoi.length ? ' · ' + nguoi.length + ' người' : '') + '.')
    } finally {
      if (conGanRef.current) setDangQuet(false)
    }
  }

  async function napAnhKhoang(url) {
    if (cgLuongRef.current) {
      cargoRef.current.chay = false
      cargoRef.current.theHe++
      clearTimeout(cgHenRef.current)
      dongCamera(cgLuongRef.current, cgVideoRef.current)
      cgLuongRef.current = null
      setCgBat(false)
    }
    try {
      const img = await taiAnh(url)
      veLenCanvas(cgCanvasRef.current, img, img.naturalWidth, img.naturalHeight)
      cgAnhRef.current = img
      setCgCoHinh(true)
      await cargoPhanTich()
    } catch (e) {
      setChuCargo(e.message)
    }
  }

  function cargoVong(theHe) {
    const cg = cargoRef.current
    if (!cg.chay || theHe !== cg.theHe || !conGanRef.current) return
    const v = cgVideoRef.current
    if (v?.readyState >= 2) {
      veLenCanvas(cgCanvasRef.current, v, v.videoWidth, v.videoHeight)
      cgAnhRef.current = null // nguon la video truc tiep
      cargoPhanTich()
    }
    cgHenRef.current = setTimeout(() => cargoVong(theHe), 2500)
  }

  async function doiCamKhoang() {
    const cg = cargoRef.current
    if (cgLuongRef.current) {
      cg.chay = false
      cg.theHe++
      clearTimeout(cgHenRef.current)
      dongCamera(cgLuongRef.current, cgVideoRef.current)
      cgLuongRef.current = null
      setCgBat(false)
      setChuCargo('Đã tắt camera khoang hàng.')
      return
    }
    try {
      cgLuongRef.current = await moCamera(cgVideoRef.current, 960)
      cg.chay = true
      cg.theHe++
      setCgBat(true)
      setCgCoHinh(true)
      setChuCargo('Đang giám sát khoang hàng (tự phân tích 2,5 giây/lần).')
      cargoVong(cg.theHe)
    } catch (e) {
      setChuCargo(e.message)
    }
  }

  /* ---------------- Bảng sự cố ---------------- */
  const d = tongHop.duLieu
  const COT = [
    { khoa: 'gio', ten: 'Thời gian', hien: (s) => <span className="small">{gioNgayNgan(s.thoiGian)}</span> },
    { khoa: 'loai', ten: 'Loại', hien: (s) => <span className="small fw-semibold">{s.tenLoai || s.loai}</span> },
    {
      khoa: 'mucDo',
      ten: 'Mức độ',
      hien: (s) => <span className={'badge ' + (s.mucDo === 'Nguy hiểm' ? 'text-bg-danger' : 'text-bg-warning')}>{s.mucDo}</span>,
    },
    { khoa: 'viTri', ten: 'Vị trí/xe', an: 'md', hien: (s) => <span className="small">{s.viTri}</span> },
    { khoa: 'moTa', ten: 'Mô tả', an: 'lg', hien: (s) => <span className="small text-body-secondary">{s.moTa || '—'}</span> },
    {
      khoa: 'trangThai',
      ten: 'Trạng thái',
      canPhai: true,
      hien: (s) => <span className={'badge ' + (s.trangThai === 'Mới' ? 'text-bg-danger' : 'text-bg-success')}>{s.trangThai}</span>,
    },
  ]

  return (
    <>
      <BaoLoi loi={tongHop.loi} thuLai={tongHop.taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-emoji-dizzy" mau="do" so={d ? d.soNguGat : '…'} nhan="Cảnh báo ngủ gật (7 ngày)" />
        </div>
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-phone" mau="cam" so={d ? d.soDienThoai : '…'} nhan="Dùng điện thoại (7 ngày)" />
        </div>
        <div className="col-md-4 col-12">
          <TheChiSo icon="bi-box-seam" mau="xanh" so={d ? d.soHang : '…'} nhan="Sự cố khoang hàng (7 ngày)" />
        </div>
      </div>

      <div className="row g-3">
        {/* ---------------- CAMERA 1: DMS ---------------- */}
        <div className="col-lg-6">
          <TheNoiDung
            tieuDe="Camera 1 — Giám sát tài xế (DMS)"
            icon="bi-person-video3"
            phai={<span className={'badge ' + (matSanSang ? 'text-bg-danger' : 'text-bg-secondary')}>{matSanSang ? 'LIVE' : 'Đang tải'}</span>}
          >
            <div className="kd-khung mb-2">
              <canvas ref={dmsCanvasRef} style={{ display: dmsCoHinh ? 'block' : 'none' }} />
              {!dmsCoHinh && !canhBao && (
                <div className="kd-trong">
                  <i className="bi bi-person-video3" />
                  Bật camera tài xế hoặc xem ảnh mẫu.
                </div>
              )}
              {canhBao && (
                <div className="kd-den-canh-bao">
                  <div>
                    <i className="bi bi-exclamation-triangle-fill me-2" />
                    {canhBao}
                  </div>
                </div>
              )}
              <video ref={dmsVideoRef} playsInline muted />
            </div>

            <div className="row g-2 text-center mb-2">
              <div className="col-3">
                <div className="kd-hud">
                  <div className="kd-hud-so">{hud.mat}</div>
                  <div className="kd-hud-nhan">Mắt mở</div>
                </div>
              </div>
              <div className="col-3">
                <div className="kd-hud">
                  <div className="kd-hud-so">{hud.ngap}</div>
                  <div className="kd-hud-nhan">Ngáp/phút</div>
                </div>
              </div>
              <div className="col-3">
                <div className="kd-hud">
                  <div className="kd-hud-so">{hud.quay}</div>
                  <div className="kd-hud-nhan">Quay đầu (5s)</div>
                </div>
              </div>
              <div className="col-3">
                <div className="kd-hud">
                  <div className="kd-hud-so">{hud.phone}</div>
                  <div className="kd-hud-nhan">Điện thoại</div>
                </div>
              </div>
            </div>

            <div className="kd-tu-hoc mb-2">
              <div className="d-flex justify-content-between align-items-center flex-wrap gap-1">
                <div className="small">
                  <b>AI tự học</b> <span className="text-body-secondary">(mạng tự mã hóa)</span> — <span className={tuHocUi.mau}>{tuHocUi.chu}</span>
                </div>
                {hienPhanHoi && (
                  <div className="d-flex gap-1">
                    <button type="button" className="btn btn-sm btn-outline-success py-0" onClick={() => phanHoiTuHoc(true)}>
                      Cảnh báo đúng
                    </button>
                    <button type="button" className="btn btn-sm btn-outline-danger py-0" onClick={() => phanHoiTuHoc(false)}>
                      Báo nhầm — dạy lại AI
                    </button>
                  </div>
                )}
              </div>
              <div className="kd-thanh">
                <span style={{ width: tuHocUi.pct + '%', background: tuHocUi.nen }} />
              </div>
            </div>

            <div className="row g-2">
              <div className="col-md-5 col-6">
                <button type="button" className={'btn w-100 ' + (dmsBat ? 'btn-outline-danger' : 'btn-outline-primary')} disabled={!matSanSang} onClick={doiCamTaiXe}>
                  <i className={'bi me-1 ' + (dmsBat ? 'bi-stop-circle' : 'bi-camera-video')} />
                  {dmsBat ? 'Tắt camera' : 'Bật camera tài xế'}
                </button>
              </div>
              <div className="col-md-4 col-6">
                <button type="button" className="btn btn-outline-secondary w-100" disabled={!matSanSang || dmsBat} onClick={anhMauTaiXe}>
                  <i className="bi bi-person-badge me-1" />
                  Ảnh mẫu
                </button>
              </div>
              <div className="col-md-3 col-12 d-flex align-items-center">
                <span className={'small ' + dmsKq.mau}>{dmsKq.chu}</span>
              </div>
            </div>
            <div className="kd-trang-thai mt-2">{chuDms}</div>
          </TheNoiDung>
        </div>

        {/* ---------------- CAMERA 2: CARGO ---------------- */}
        <div className="col-lg-6">
          <TheNoiDung
            tieuDe="Camera 2 — Khoang hàng (Cargo Vision)"
            icon="bi-box-seam"
            phai={
              <div className="form-check form-switch small mb-0">
                <input className="form-check-input" type="checkbox" id="kdXeDung" checked={xeDung} onChange={(e) => setXeDung(e.target.checked)} />
                <label className="form-check-label" htmlFor="kdXeDung" title="Khi xe dừng, phát hiện người trong khoang = xâm nhập">
                  Xe đang dừng
                </label>
              </div>
            }
          >
            <div className="kd-khung mb-2">
              <canvas ref={cgCanvasRef} style={{ display: cgCoHinh ? 'block' : 'none' }} />
              {!cgCoHinh && (
                <div className="kd-trong">
                  <i className="bi bi-box-seam" />
                  Chọn ảnh mẫu hoặc bật camera khoang hàng.
                </div>
              )}
              <video ref={cgVideoRef} playsInline muted />
            </div>

            <div className="row g-2">
              <div className="col-md-4 col-6">
                <button type="button" className="btn btn-outline-primary w-100" disabled={dangQuet} onClick={() => napAnhKhoang(ANH_CARGO_NGAY)}>
                  <i className="bi bi-box me-1" />
                  Hàng ngay ngắn
                </button>
              </div>
              <div className="col-md-4 col-6">
                <button type="button" className="btn btn-outline-warning w-100" disabled={dangQuet} onClick={() => napAnhKhoang(ANH_CARGO_NGHIENG)}>
                  <i className="bi bi-box-arrow-down-right me-1" />
                  Hàng bị nghiêng
                </button>
              </div>
              <div className="col-md-4 col-12">
                <button type="button" className={'btn w-100 ' + (cgBat ? 'btn-outline-danger' : 'btn-outline-primary')} onClick={doiCamKhoang}>
                  <i className={'bi me-1 ' + (cgBat ? 'bi-stop-circle' : 'bi-camera-video')} />
                  {cgBat ? 'Tắt camera' : 'Camera khoang'}
                </button>
              </div>
              <div className="col-md-6 col-12">
                <button type="button" className="btn btn-primary w-100" disabled={dangQuet || !cgCoHinh} onClick={cargoPhanTich}>
                  {dangQuet ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-search me-1" />}
                  Phân tích khoang hàng
                </button>
              </div>
              <div className="col-md-6 col-12 d-flex align-items-center">
                <div className="small">
                  {cgKq.length ? (
                    cgKq.map((x, i) => (
                      <div key={i} className={x.mau + ' fw-semibold'}>
                        {x.chu}
                      </div>
                    ))
                  ) : (
                    <span className="text-body-secondary">Chưa phân tích.</span>
                  )}
                </div>
              </div>
            </div>
            <div className="kd-trang-thai mt-2">{chuCargo}</div>
          </TheNoiDung>
        </div>

        {/* ---------------- NHAT KY SU CO ---------------- */}
        <div className="col-12">
          <TheNoiDung
            tieuDe="Sự cố tài xế / khoang hàng — 7 ngày"
            icon="bi-journal-text"
            khongDem
            phai={
              <>
                <button type="button" className="btn btn-sm btn-outline-secondary" title="Làm mới" onClick={() => tongHop.taiLai()}>
                  <i className="bi bi-arrow-clockwise" />
                </button>
                <Link className="btn btn-sm btn-outline-secondary" to="/admin/security">
                  <i className="bi bi-building-lock me-1" />
                  Phòng an ninh
                </Link>
              </>
            }
          >
            <BangDuLieu
              cot={COT}
              dong={d?.suCo}
              khoaDong="maSC"
              dangTai={tongHop.dangTai}
              rong="Chưa ghi nhận sự cố nào — đội xe đang vận hành an toàn."
            />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
