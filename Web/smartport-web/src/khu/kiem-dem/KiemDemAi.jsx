import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { AI_MAU } from '../../ai/taiThuVien'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { gioNgayNgan } from '../../utils/dinhDang'
import DangKyMau from './DangKyMau'
import { chuongDu, dungAmThanh, huCoi, noiCanhBao, tickDem } from './chung/amThanh'
import { guiBangChungKiemDem } from './chung/bangChung'
import { dongCamera, moCamera, veLenCanvas } from './chung/camera'
import { capNhatTheoDoi, taoBoTheoDoi, veKhungTheoDoi } from './chung/demVideo'
import { histSim, hsvHist, iou, timVatMau } from './chung/mauSac'
import {
  ANH_MAU_DEMO,
  SO_MAU_TOI_DA,
  SO_MAU_TOI_THIEU,
  anhTuFile,
  docMau,
  taiAnhTheoUrl,
  trichKhungTuVideo,
} from './chung/mauSanPham'
import { kiemTraThungHang } from './chung/qcThung'
import { MAU, chuanBiNet, veHop } from './chung/ve'
import { khoiDongYolo, phatHienYolo } from './chung/yolo'
import './kiem-dem.css'

/** Lop COCO duoc phep dem (giong danh sach cua ban MVC) */
const LOP_DEM = [
  { ma: 'bottle', coco: 39, ten: 'Chai/lọ' },
  { ma: 'cup', coco: 41, ten: 'Cốc/ly' },
  { ma: 'suitcase', coco: 28, ten: 'Vali/kiện hàng' },
  { ma: 'book', coco: 73, ten: 'Sách/hộp mỏng' },
  { ma: 'cell phone', coco: 67, ten: 'Điện thoại' },
  { ma: 'apple', coco: 47, ten: 'Táo' },
  { ma: 'orange', coco: 49, ten: 'Cam' },
  { ma: 'banana', coco: 46, ten: 'Chuối' },
]
const cocoCua = (ma) => LOP_DEM.find((l) => l.ma === ma)?.coco ?? 39
const tenLop = (ma) => LOP_DEM.find((l) => l.ma === ma)?.ten ?? ma

/** Do tuong dong toi thieu (histogram HSV) de coi la GIONG mau chuan */
const NGUONG_GIONG = 0.78
const ANH_QC_DEMO = AI_MAU + 'box-qc-demo.png?v=2'
/** Video bang chuyen demo co san trong wwwroot cua API */
const VIDEO_DEMO = '/Content/video/demo-count.webm?v=1'
const RONG = { tenLo: '', duKien: '', lopDem: 'bottle', chieuCao: '', trongLuong: '', ghiChu: '' }

/**
 * KIỂM ĐẾM & PHÂN LOẠI HÀNG HÓA AI (thay Admin/Security/Tally của bản MVC).
 * YOLO26 + histogram HSV chạy 100% trên trình duyệt: đếm theo ảnh/camera, soi lỗi
 * thùng hàng ở trạm QC, và đếm lũy kế theo video băng chuyền đúng sản phẩm mẫu.
 */
export default function KiemDemAi() {
  const lichSu = useApi('/api/kiem-dem')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()

  const [trangThai, setTrangThai] = useState('Đang tải YOLO26…')
  const [sanSang, setSanSang] = useState(false)
  const [banRon, setBanRon] = useState(false)
  const [camBat, setCamBat] = useState(false)
  const [dongBang, setDongBang] = useState(false)
  const [coAnh, setCoAnh] = useState(false)
  const [kq, setKq] = useState(null) // { dem, giong, khac, nguon }
  const [qc, setQc] = useState(null) // { soThung, soLoi, pctMax }
  const [anhMau, setAnhMau] = useState([])
  const [coVideo, setCoVideo] = useState(false)
  const [vcUi, setVcUi] = useState({ dangChay: false, dem: 0, sai: 0, mucTieu: 0 })

  const canvasRef = useRef(null)
  const videoRef = useRef(null)
  const videoFileRef = useRef(null)
  const luongRef = useRef(null)
  const anhRef = useRef(null) // anh tinh dang hien (file / anh mau)
  const rafRef = useRef(0)
  const henRef = useRef(0)
  const urlVideoRef = useRef(null)
  const conGanRef = useRef(true)
  const xemRef = useRef(false)
  const mauRef = useRef([]) // histogram cac anh san pham mau
  const boRef = useRef(taoBoTheoDoi())
  const gtRef = useRef(gt)
  gtRef.current = gt
  const vcRef = useRef({
    dangChay: false,
    dem: 0,
    sai: 0,
    mucTieu: 0,
    theHe: 0,
    choKhung: 0,
    lanCanhBao: 0,
    luuSaiRoi: false,
    dungVideo: false,
  })

  /* ---------------- Nap YOLO26 1 lan ---------------- */
  useEffect(() => {
    conGanRef.current = true
    khoiDongYolo()
      .then(() => {
        if (!conGanRef.current) return
        setSanSang(true)
        setTrangThai('Sẵn sàng — nhập thông tin lô rồi đưa hàng vào camera hoặc chọn ảnh.')
      })
      .catch((e) => conGanRef.current && setTrangThai('Không tải được mô hình: ' + e.message))
    return () => {
      // Roi man hinh: dung het camera, vong lap ve, hen gio va am thanh
      conGanRef.current = false
      xemRef.current = false
      vcRef.current.dangChay = false
      vcRef.current.theHe++
      cancelAnimationFrame(rafRef.current)
      clearTimeout(henRef.current)
      dongCamera(luongRef.current, videoRef.current)
      luongRef.current = null
      if (urlVideoRef.current) URL.revokeObjectURL(urlVideoRef.current)
      dungAmThanh()
    }
  }, [])

  /* ---------------- Khung hinh nguon ---------------- */
  /** Ve khung hinh dang co vao canvas (uu tien video khi dang dem video) */
  function veNguon() {
    const c = canvasRef.current
    const vFile = videoFileRef.current
    const v = videoRef.current
    if (!c) return false
    if (vcRef.current.dangChay && vcRef.current.dungVideo && vFile?.src && vFile.readyState >= 2 && !vFile.ended)
      return veLenCanvas(c, vFile, vFile.videoWidth || 720, vFile.videoHeight || 420)
    if (luongRef.current && v?.readyState >= 2) return veLenCanvas(c, v, v.videoWidth, v.videoHeight)
    if (anhRef.current) return veLenCanvas(c, anhRef.current, anhRef.current.naturalWidth, anhRef.current.naturalHeight)
    return false
  }

  /** Vong xem truc tiep (chi chay khi camera bat va khong dong bang ket qua) */
  function vongXem() {
    if (!xemRef.current) return
    const v = videoRef.current
    if (luongRef.current && v?.readyState >= 2) veLenCanvas(canvasRef.current, v, v.videoWidth, v.videoHeight)
    rafRef.current = requestAnimationFrame(vongXem)
  }

  function batXem() {
    if (xemRef.current) return
    xemRef.current = true
    setDongBang(false)
    cancelAnimationFrame(rafRef.current)
    rafRef.current = requestAnimationFrame(vongXem)
  }

  function ngungXem() {
    xemRef.current = false
    cancelAnimationFrame(rafRef.current)
    if (luongRef.current) setDongBang(true)
  }

  /* ---------------- Camera / anh ---------------- */
  async function doiCamera() {
    if (luongRef.current) {
      ngungXem()
      dongCamera(luongRef.current, videoRef.current)
      luongRef.current = null
      setCamBat(false)
      setDongBang(false)
      setTrangThai('Đã tắt camera.')
      return
    }
    try {
      luongRef.current = await moCamera(videoRef.current, 1280)
      setCamBat(true)
      setCoAnh(true)
      anhRef.current = null
      batXem()
      setTrangThai('Camera đang chạy — sắp xếp hàng rồi bấm "Đếm & phân loại".')
    } catch (e) {
      setTrangThai(e.message)
    }
  }

  async function chonAnh(e) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f) return
    try {
      anhRef.current = await anhTuFile(f)
      ngungXem()
      setCoAnh(true)
      veNguon()
      setDongBang(false)
      setTrangThai('Đã nạp ảnh — bấm "Đếm & phân loại".')
    } catch (err) {
      setTrangThai(err.message)
    }
  }

  async function napAnhQcDemo() {
    try {
      anhRef.current = await taiAnhTheoUrl(ANH_QC_DEMO)
      ngungXem()
      setCoAnh(true)
      veNguon()
      setDongBang(false)
      setTrangThai('Đã nạp ảnh mẫu 2 thùng (1 bị ướt góc) — bấm "Kiểm tra lỗi thùng hàng".')
    } catch (err) {
      setTrangThai(err.message)
    }
  }

  /* ---------------- 1. Đếm & phân loại theo màu ---------------- */
  async function demVaPhanLoai() {
    if (vcRef.current.dangChay) {
      setTrangThai('Đang đếm video — bấm "Dừng" trước khi dùng chức năng này.')
      return
    }
    if (banRon || !sanSang) return
    ngungXem()
    if (!veNguon()) {
      setTrangThai('Chưa có hình — bật camera hoặc chọn ảnh.')
      return
    }
    setBanRon(true)
    setTrangThai('AI đang đếm "' + tenLop(gt.lopDem) + '"…')
    try {
      const canvas = canvasRef.current
      const hops = (await phatHienYolo(canvas, [cocoCua(gt.lopDem)])).sort((a, b) => b.score - a.score)
      if (!hops.length) {
        setTrangThai('Không thấy đối tượng nào thuộc lớp đã chọn — thử ảnh khác hoặc đưa hàng lại gần.')
        return
      }
      // MAU CHUAN = vat co do tin cay cao nhat; so histogram HSV bang cosine
      const histChuan = hsvHist(canvas, hops[0])
      const ctx = chuanBiNet(canvas, { chiaNet: 320, chiaChu: 50, netToiThieu: 2.5 })
      let giong = 0
      let khac = 0
      hops.forEach((h, i) => {
        const sim = i === 0 ? 1 : histSim(hsvHist(canvas, h), histChuan)
        const laGiong = sim >= NGUONG_GIONG
        if (laGiong) giong++
        else khac++
        const nhan = (i === 0 ? 'MẪU' : (laGiong ? 'PHẢI ' : 'TRÁI ') + Math.round(sim * 100) + '%') + ' #' + (i + 1)
        veHop(ctx, h, laGiong ? MAU.dat : MAU.chuY, nhan)
      })
      setKq({ dem: hops.length, giong, khac, nguon: 'anh' })
      setTrangThai('Xong — kiểm tra kết quả rồi bấm "Lưu phiên kiểm đếm".')
    } catch (e) {
      setTrangThai('Lỗi: ' + e.message)
    } finally {
      setBanRon(false)
    }
  }

  /* ---------------- 2. Trạm QC — soi lỗi thùng hàng ---------------- */
  async function kiemTraQc() {
    if (vcRef.current.dangChay) {
      setTrangThai('Đang đếm video — bấm "Dừng" trước khi dùng chức năng này.')
      return
    }
    if (banRon || !sanSang) return
    ngungXem()
    if (!veNguon()) {
      setTrangThai('Chưa có hình — bật camera hoặc chọn ảnh.')
      return
    }
    setBanRon(true)
    setTrangThai('AI đang soi bề mặt thùng hàng…')
    try {
      const canvas = canvasRef.current
      const r = await kiemTraThungHang(canvas)
      if (!r.soThung) {
        setQc({ soThung: 0, soLoi: 0, pctMax: 0 })
        setTrangThai('Không tìm thấy thùng hàng trong khung — đưa thùng vào giữa và đủ sáng.')
        return
      }
      setQc(r)
      if (!r.soLoi) {
        setTrangThai(r.soThung + ' thùng đều ĐẠT — không phát hiện vùng hư hại.')
        return
      }
      huCoi(4)
      noiCanhBao('Cảnh báo! Phát hiện ' + r.soLoi + ' thùng hàng bị lỗi trên băng chuyền. Vui lòng kiểm tra ngay.')
      const moTa = 'QC: phát hiện ' + r.soLoi + '/' + r.soThung + ' thùng hàng bị lỗi (ướt/hư hại bề mặt)'
      setTrangThai('Phát hiện ' + r.soLoi + ' thùng LỖI — đang lưu ảnh bằng chứng…')
      try {
        await guiBangChungKiemDem(canvas, { moTa, baoAnNinh: true })
        if (conGanRef.current) setTrangThai('Đã hú còi, lưu ảnh vào Kho media AI và báo Phòng an ninh (' + r.soLoi + ' thùng lỗi).')
      } catch (e) {
        if (conGanRef.current) setTrangThai('Phát hiện ' + r.soLoi + ' thùng lỗi nhưng chưa gửi được bằng chứng: ' + e.message)
      }
    } catch (e) {
      setTrangThai('Lỗi: ' + e.message)
    } finally {
      setBanRon(false)
    }
  }

  /* ---------------- 3. Sản phẩm mẫu (few-shot) ---------------- */
  function themMau(nguon, rong, cao) {
    if (mauRef.current.length >= SO_MAU_TOI_DA) return false
    const m = docMau(nguon, rong, cao)
    if (!m) return false
    mauRef.current = [...mauRef.current, m.hist]
    setAnhMau((ds) => [...ds, m.anhNho])
    return true
  }

  async function themAnhMau(e) {
    const fs = Array.from(e.target.files || [])
    e.target.value = ''
    for (const f of fs.slice(0, SO_MAU_TOI_DA - mauRef.current.length)) {
      try {
        themMau(await anhTuFile(f))
      } catch {
        /* bo qua anh loi, van nhan cac anh con lai */
      }
    }
    if (conGanRef.current) setTrangThai('Đã đăng ký ' + mauRef.current.length + '/' + SO_MAU_TOI_DA + ' ảnh mẫu.')
  }

  async function themVideoMau(e) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f) return
    setTrangThai('Đang trích khung hình từ video mẫu…')
    try {
      const khung = await trichKhungTuVideo(f, SO_MAU_TOI_DA)
      for (const cv of khung) {
        if (mauRef.current.length >= SO_MAU_TOI_DA) break
        themMau(cv, cv.width, cv.height)
      }
      if (conGanRef.current) setTrangThai('Đã trích ' + mauRef.current.length + ' khung hình từ video làm mẫu.')
    } catch (err) {
      if (conGanRef.current) setTrangThai(err.message)
    }
  }

  function chupMauTuCamera() {
    const v = videoRef.current
    if (!luongRef.current || !v || v.readyState < 2) {
      setTrangThai('Bật camera trước rồi mới chụp mẫu được.')
      return
    }
    if (themMau(v, v.videoWidth, v.videoHeight)) setTrangThai('Đã chụp mẫu ' + mauRef.current.length + '/' + SO_MAU_TOI_DA + ' từ camera.')
    else setTrangThai('Đã đủ ' + SO_MAU_TOI_DA + ' ảnh mẫu — bấm nút xóa nếu muốn làm lại.')
  }

  async function napMauDemo() {
    xoaMau()
    setTrangThai('Đang nạp bộ ảnh mẫu chuẩn…')
    for (const url of ANH_MAU_DEMO) {
      try {
        themMau(await taiAnhTheoUrl(url))
      } catch {
        if (conGanRef.current) setTrangThai('Thiếu ảnh mẫu trong thư mục Content/img.')
        return
      }
    }
    if (conGanRef.current) setTrangThai('Đã nạp bộ 6 ảnh mẫu chuẩn — nhập số lượng khai báo rồi bấm "Bắt đầu đếm video".')
  }

  function xoaMau() {
    mauRef.current = []
    setAnhMau([])
  }

  /* ---------------- 4. Đếm lũy kế theo video ---------------- */
  function napVideo(e) {
    const f = e.target.files?.[0]
    e.target.value = ''
    if (!f) return
    if (urlVideoRef.current) URL.revokeObjectURL(urlVideoRef.current)
    urlVideoRef.current = URL.createObjectURL(f)
    videoFileRef.current.src = urlVideoRef.current
    videoFileRef.current.load()
    setCoVideo(true)
    setTrangThai('Đã nạp video — nhập số lượng khai báo rồi bấm "Bắt đầu đếm video".')
  }

  function napVideoDemo() {
    if (urlVideoRef.current) URL.revokeObjectURL(urlVideoRef.current)
    urlVideoRef.current = null
    videoFileRef.current.src = VIDEO_DEMO
    videoFileRef.current.load()
    setCoVideo(true)
    setTrangThai('Đã nạp video demo băng chuyền — nhập số lượng khai báo (VD 11) rồi bấm "Bắt đầu đếm video".')
  }

  function goVideo() {
    if (vcRef.current.dangChay) return
    const v = videoFileRef.current
    try {
      v.pause()
    } catch {
      /* bo qua */
    }
    v.removeAttribute('src')
    v.load()
    if (urlVideoRef.current) URL.revokeObjectURL(urlVideoRef.current)
    urlVideoRef.current = null
    setCoVideo(false)
    setTrangThai('Đã gỡ video — bật camera rồi bấm "Bắt đầu đếm video" để đếm trực tiếp.')
  }

  function capNhatVcUi() {
    const vc = vcRef.current
    if (conGanRef.current) setVcUi({ dangChay: vc.dangChay, dem: vc.dem, sai: vc.sai, mucTieu: vc.mucTieu })
  }

  function batDauDemVideo() {
    const vc = vcRef.current
    if (vc.dangChay) return
    if (mauRef.current.length < SO_MAU_TOI_THIEU) {
      setTrangThai('Cần đăng ký ít nhất ' + SO_MAU_TOI_THIEU + ' ảnh mẫu sản phẩm trước.')
      return
    }
    const mucTieu = parseInt(gtRef.current.duKien, 10) || 0
    if (mucTieu <= 0) {
      setTrangThai('Nhập "Số lượng khai báo" (mục tiêu, VD 500) rồi bấm lại.')
      return
    }
    const vFile = videoFileRef.current
    vc.dungVideo = !!(vFile?.src && !vFile.error)
    if (!vc.dungVideo && !luongRef.current) {
      setTrangThai('Bật camera HOẶC nạp video băng chuyền trước.')
      return
    }
    ngungXem()
    Object.assign(vc, {
      dangChay: true,
      dem: 0,
      sai: 0,
      mucTieu,
      theHe: vc.theHe + 1,
      choKhung: 0,
      lanCanhBao: 0,
      luuSaiRoi: false,
    })
    boRef.current = taoBoTheoDoi()
    if (vc.dungVideo) {
      try {
        vFile.currentTime = 0
      } catch {
        /* video chua san sang tua: van phat tu dau */
      }
      vFile.play().catch(() => {})
    }
    setTrangThai('Đang đếm theo video — mục tiêu ' + mucTieu + ' sản phẩm đúng mẫu…')
    capNhatVcUi()
    tickVideo(vc.theHe)
  }

  function ketThucDemVideo(du) {
    const vc = vcRef.current
    if (!vc.dangChay) return
    vc.dangChay = false
    vc.theHe++
    clearTimeout(henRef.current)
    try {
      videoFileRef.current?.pause()
    } catch {
      /* bo qua */
    }
    capNhatVcUi()
    setKq({ dem: vc.dem, giong: vc.dem, khac: vc.sai, nguon: 'video', mucTieu: vc.mucTieu })
    if (du) {
      chuongDu()
      noiCanhBao('Hoàn thành! Đã đếm đủ ' + vc.dem + ' trên ' + vc.mucTieu + ' sản phẩm. Đạt yêu cầu.')
      setTrangThai('ĐỦ SỐ LƯỢNG ' + vc.dem + '/' + vc.mucTieu + ' — bấm "Lưu phiên kiểm đếm" để ghi sổ.')
    } else if (vc.mucTieu > 0 && vc.dem < vc.mucTieu) {
      noiCanhBao('Video kết thúc. Mới đếm được ' + vc.dem + ' trên ' + vc.mucTieu + ' sản phẩm. Chưa đủ số lượng.')
      setTrangThai('Dừng ở ' + vc.dem + '/' + vc.mucTieu + ' (thiếu ' + (vc.mucTieu - vc.dem) + ').')
    } else {
      setTrangThai('Đã dừng — đếm được ' + vc.dem + ' sản phẩm đúng mẫu, ' + vc.sai + ' sai mẫu.')
    }
  }

  /** Mot nhip cua phien dem video (~3 lan/giay, du nhay ma nhe CPU) */
  async function tickVideo(theHe) {
    const vc = vcRef.current
    if (!vc.dangChay || theHe !== vc.theHe || !conGanRef.current) return
    const vFile = videoFileRef.current
    if (vc.dungVideo && vFile?.ended) {
      ketThucDemVideo(false)
      return
    }
    if (!veNguon()) {
      // chua co khung hinh — cho toi da ~12s roi bao loi
      vc.choKhung++
      if (vc.choKhung > 40) {
        setTrangThai('Không lấy được khung hình từ video/camera.')
        ketThucDemVideo(false)
        return
      }
      henRef.current = setTimeout(() => tickVideo(theHe), 300)
      return
    }
    vc.choKhung = 0
    // Tam DUNG video trong luc AI xu ly de khong bo sot san pham tren may yeu
    if (vc.dungVideo && vFile && !vFile.paused) {
      try {
        vFile.pause()
      } catch {
        /* bo qua */
      }
    }
    const canvas = canvasRef.current
    let hops = []
    try {
      hops = await phatHienYolo(canvas, [cocoCua(gtRef.current.lopDem)])
    } catch {
      hops = []
    }
    if (!vc.dangChay || theHe !== vc.theHe || !conGanRef.current) return
    // Ket hop YOLO + do mau: them cum mau KHONG trung voi hop YOLO nao
    for (const f of timVatMau(canvas)) if (!hops.some((b) => iou(f, b) > 0.3)) hops.push(f)

    const vuaDem = capNhatTheoDoi(boRef.current, hops, canvas, mauRef.current)
    let coSai = false
    for (const v of vuaDem) {
      if (v.dung) {
        vc.dem++
        tickDem()
      } else {
        vc.sai++
        coSai = true
      }
    }
    if (coSai && Date.now() - vc.lanCanhBao > 6000) {
      vc.lanCanhBao = Date.now()
      huCoi(2.5)
      noiCanhBao('Cảnh báo lỗi! Phát hiện sản phẩm không đúng mẫu trên băng chuyền.')
    }
    veKhungTheoDoi(canvas, boRef.current, vc.dem, vc.mucTieu)
    capNhatVcUi()
    // Luu 1 anh bang chung (sau khi da ve khung do) cho ca phien
    if (coSai && !vc.luuSaiRoi) {
      vc.luuSaiRoi = true
      guiBangChungKiemDem(canvas, {
        moTa: 'Đếm video: phát hiện sản phẩm KHÔNG ĐÚNG MẪU trong lô "' + (gtRef.current.tenLo || 'chưa đặt tên') + '"',
      }).catch(() => {})
    }
    if (vc.mucTieu > 0 && vc.dem >= vc.mucTieu) {
      ketThucDemVideo(true)
      return
    }
    if (vc.dungVideo && vFile && !vFile.ended) vFile.play().catch(() => {})
    henRef.current = setTimeout(() => tickVideo(theHe), 300)
  }

  /* ---------------- Lưu phiên ---------------- */
  async function luuPhien() {
    if (!kq) return
    const than = {
      tenLo: gt.tenLo,
      lopDem: chuoiHoacNull(gt.lopDem),
      duKien: soHoacNull(gt.duKien),
      demDuoc: kq.dem,
      soGiong: kq.giong,
      soKhac: kq.khac,
      chieuCao: chuoiHoacNull(gt.chieuCao),
      trongLuong: chuoiHoacNull(gt.trongLuong),
      ghiChu: chuoiHoacNull(gt.ghiChu),
    }
    const r = await chay(() => goiApi('/api/kiem-dem', { method: 'POST', body: than }))
    if (!r) return
    setKq(null)
    datLai({ ...RONG, lopDem: gt.lopDem })
    lichSu.taiLai({ im: true })
    setTrangThai('Đã lưu phiên #' + r.id + ' vào lịch sử kiểm đếm.')
  }

  /* ---------------- Chỉ số + bảng lịch sử ---------------- */
  const ds = useMemo(() => lichSu.duLieu || [], [lichSu.duLieu])
  const soKhop = ds.filter((k) => k.lech === 0).length
  const soLech = ds.filter((k) => k.lech !== null && k.lech !== 0).length

  const COT = [
    { khoa: 'gio', ten: 'Thời gian', hien: (k) => <span className="small">{gioNgayNgan(k.thoiGian)}</span> },
    { khoa: 'lo', ten: 'Lô hàng', hien: (k) => <span className="fw-semibold">{k.tenLo}</span> },
    { khoa: 'duKien', ten: 'Khai báo', canPhai: true, hien: (k) => k.duKien ?? '—' },
    {
      khoa: 'demDuoc',
      ten: 'AI đếm',
      canPhai: true,
      hien: (k) => (
        <>
          <b>{k.demDuoc ?? '—'}</b>
          {k.lech !== null &&
            (k.lech === 0 ? (
              <span className="badge text-bg-success ms-1">Khớp</span>
            ) : (
              <span className="badge text-bg-danger ms-1">Lệch {Math.abs(k.lech)}</span>
            ))}
        </>
      ),
    },
    {
      khoa: 'phaiTrai',
      ten: 'Phải / Trái',
      an: 'md',
      canPhai: true,
      hien: (k) => (
        <span className="small">
          <b className="text-success">{k.soGiong ?? 0}</b> / <b className="text-warning">{k.soKhac ?? 0}</b>
        </span>
      ),
    },
    { khoa: 'ghiChu', ten: 'Ghi chú', an: 'lg', hien: (k) => <span className="small text-body-secondary">{k.ghiChu || '—'}</span> },
  ]

  const khoaMau = vcUi.dangChay || banRon
  const lech = kq && gt.duKien !== '' ? Number(gt.duKien) - kq.dem : null

  return (
    <>
      <BaoLoi loi={lichSu.loi} thuLai={lichSu.taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-ui-checks-grid" mau="xanh" so={ds.length} nhan="Phiên kiểm đếm gần đây" />
        </div>
        <div className="col-md-4 col-6">
          <TheChiSo icon="bi-check2-circle" mau="la" so={soKhop} nhan="Phiên khớp khai báo" />
        </div>
        <div className="col-md-4 col-12">
          <TheChiSo icon="bi-exclamation-triangle" mau="cam" so={soLech} nhan="Phiên lệch số lượng" />
        </div>
      </div>

      <div className="row g-3">
        {/* ---------------- Cột trái: thông tin lô + kết quả ---------------- */}
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Thông tin lô hàng" icon="bi-box-seam">
            <Truong nhan="Tên lô hàng" batBuoc lop="mb-2">
              <input className="form-control form-control-sm" value={gt.tenLo} onChange={doi('tenLo')} maxLength={100} placeholder="VD: Lô nước suối 500ml" />
            </Truong>
            <div className="row g-2">
              <Truong nhan="Số lượng khai báo" lop="col-6 mb-2">
                <input type="number" min="0" className="form-control form-control-sm" value={gt.duKien} onChange={doi('duKien')} placeholder="24" />
              </Truong>
              <Truong nhan="Loại hàng (lớp đếm)" lop="col-6 mb-2">
                <select className="form-select form-select-sm" value={gt.lopDem} onChange={doi('lopDem')}>
                  {LOP_DEM.map((l) => (
                    <option key={l.ma} value={l.ma}>
                      {l.ten}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Chiều cao" lop="col-6 mb-2">
                <input className="form-control form-control-sm" value={gt.chieuCao} onChange={doi('chieuCao')} maxLength={30} placeholder="VD: 21 cm" />
              </Truong>
              <Truong nhan="Trọng lượng" lop="col-6 mb-2">
                <input className="form-control form-control-sm" value={gt.trongLuong} onChange={doi('trongLuong')} maxLength={30} placeholder="VD: 0,5 kg" />
              </Truong>
              <Truong nhan="Ghi chú" lop="col-12 mb-0">
                <input className="form-control form-control-sm" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={200} placeholder="Tùy chọn" />
              </Truong>
            </div>
          </TheNoiDung>

          <TheNoiDung tieuDe="Kết quả" icon="bi-123">
            {kq ? (
              <>
                <div className="d-flex align-items-baseline gap-2">
                  <span className="kd-so-lon">{kq.dem}</span>
                  <span className="text-body-secondary small">
                    vật{kq.nguon === 'video' ? ' đúng mẫu' : ''}
                    {kq.nguon === 'video' && kq.mucTieu ? ' / mục tiêu ' + kq.mucTieu : ''}
                  </span>
                </div>
                {lech !== null && kq.nguon === 'anh' && (
                  <div className="mt-1">
                    {lech === 0 ? (
                      <span className="badge text-bg-success">Khớp khai báo</span>
                    ) : (
                      <span className="badge text-bg-danger">
                        Lệch {Math.abs(lech)} ({lech > 0 ? 'thiếu' : 'thừa'})
                      </span>
                    )}
                  </div>
                )}
                <div className="mt-2 small">
                  <span className="text-success fw-semibold">{kq.giong}</span> {kq.nguon === 'video' ? 'đúng mẫu' : 'lăn phải (giống mẫu)'} ·{' '}
                  <span className="text-warning fw-semibold">{kq.khac}</span> {kq.nguon === 'video' ? 'sai mẫu' : 'lăn trái (khác mẫu)'}
                </div>
              </>
            ) : (
              <div className="text-body-secondary small">Chưa có lượt đếm nào.</div>
            )}
            <button type="button" className="btn btn-primary w-100 mt-3" disabled={!kq || dangChay || !gt.tenLo.trim()} onClick={luuPhien}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu phiên kiểm đếm
            </button>
            {!gt.tenLo.trim() && kq && <div className="form-text">Nhập tên lô hàng để lưu được phiên.</div>}
          </TheNoiDung>
        </div>

        {/* ---------------- Cột phải: camera + QC + đếm video + lịch sử ---------------- */}
        <div className="col-lg-8">
          <TheNoiDung
            tieuDe="Camera kiểm đếm"
            icon="bi-camera-video"
            phai={
              <Link className="btn btn-sm btn-outline-secondary" to="/admin/security">
                <i className="bi bi-building-lock me-1" />
                Phòng an ninh
              </Link>
            }
          >
            <div className="kd-khung mb-2">
              <span className="kd-lan kd-lan-trai">LÀN TRÁI · KHÁC MẪU</span>
              <span className="kd-lan kd-lan-phai">LÀN PHẢI · GIỐNG MẪU</span>
              <canvas ref={canvasRef} style={{ display: coAnh ? 'block' : 'none' }} />
              {!coAnh && (
                <div className="kd-trong">
                  <i className="bi bi-camera-video-off" />
                  Bật camera hoặc chọn ảnh để bắt đầu.
                </div>
              )}
              <video ref={videoRef} playsInline muted />
            </div>
            <div className="kd-trang-thai mb-2">{trangThai}</div>
            <div className="row g-2">
              <div className="col-md-3 col-6">
                <button type="button" className={'btn w-100 ' + (camBat ? 'btn-outline-danger' : 'btn-outline-primary')} onClick={doiCamera} disabled={vcUi.dangChay}>
                  <i className={'bi me-1 ' + (camBat ? 'bi-stop-circle' : 'bi-camera-video')} />
                  {camBat ? 'Tắt camera' : 'Bật camera'}
                </button>
              </div>
              <div className="col-md-3 col-6">
                <label className={'btn btn-outline-primary w-100 mb-0' + (vcUi.dangChay ? ' disabled' : '')}>
                  <i className="bi bi-image me-1" />
                  Chọn ảnh
                  <input type="file" accept="image/*" hidden disabled={vcUi.dangChay} onChange={chonAnh} />
                </label>
              </div>
              <div className="col-md-3 col-6">
                <button type="button" className="btn btn-primary w-100" disabled={!sanSang || banRon || vcUi.dangChay} onClick={demVaPhanLoai}>
                  <i className="bi bi-123 me-1" />
                  Đếm &amp; phân loại
                </button>
              </div>
              <div className="col-md-3 col-6">
                <button type="button" className="btn btn-outline-secondary w-100" disabled={!camBat || !dongBang || vcUi.dangChay} onClick={batXem} title="Quay lại xem hình trực tiếp từ camera">
                  <i className="bi bi-play-circle me-1" />
                  Xem trực tiếp
                </button>
              </div>
            </div>
          </TheNoiDung>

          <TheNoiDung
            tieuDe="Trạm QC — soi lỗi thùng hàng"
            icon="bi-clipboard2-check"
            phai={
              <Link className="btn btn-sm btn-outline-secondary" to="/admin/security/media">
                <i className="bi bi-collection-play me-1" />
                Kho ảnh/video AI
              </Link>
            }
          >
            <div className="row g-2 align-items-center">
              <div className="col-md-4 col-6">
                <button type="button" className="btn btn-outline-primary w-100" disabled={vcUi.dangChay} onClick={napAnhQcDemo}>
                  <i className="bi bi-box-seam me-1" />
                  Ảnh mẫu 2 thùng
                </button>
              </div>
              <div className="col-md-4 col-6">
                <button type="button" className="btn btn-danger w-100" disabled={!sanSang || banRon || vcUi.dangChay} onClick={kiemTraQc}>
                  <i className="bi bi-search me-1" />
                  Kiểm tra lỗi thùng
                </button>
              </div>
              <div className="col-md-4 col-12">
                {qc ? (
                  qc.soLoi > 0 ? (
                    <span className="small">
                      <span className="badge text-bg-danger">{qc.soLoi} thùng lỗi</span> / {qc.soThung} thùng — đã báo Phòng an ninh
                    </span>
                  ) : qc.soThung ? (
                    <span className="small">
                      <span className="badge text-bg-success">Tất cả đạt</span> {qc.soThung} thùng
                    </span>
                  ) : (
                    <span className="small text-body-secondary">Không thấy thùng hàng trong khung.</span>
                  )
                ) : (
                  <span className="small text-body-secondary">Chưa kiểm tra.</span>
                )}
              </div>
            </div>
          </TheNoiDung>

          <TheNoiDung tieuDe="Đếm theo video — đúng sản phẩm mẫu" icon="bi-camera-reels">
            <DangKyMau
              anhMau={anhMau}
              khoa={khoaMau}
              themAnh={themAnhMau}
              themVideo={themVideoMau}
              chupCamera={chupMauTuCamera}
              napDemo={napMauDemo}
              xoaMau={xoaMau}
            />
            <hr className="my-3" />
            <div className="row g-2 align-items-center">
              <div className="col-md-3 col-12 text-center">
                <div className="kd-so-lon">
                  {vcUi.dem}/{vcUi.mucTieu || '—'}
                </div>
                <div className="progress mt-1" style={{ height: 8 }}>
                  <div
                    className={'progress-bar' + (vcUi.mucTieu && vcUi.dem >= vcUi.mucTieu ? ' bg-success' : ' progress-bar-striped progress-bar-animated')}
                    style={{ width: (vcUi.mucTieu ? Math.min(100, Math.round((vcUi.dem * 100) / vcUi.mucTieu)) : 0) + '%' }}
                  />
                </div>
                <div className="small mt-1">
                  <span className="text-success fw-semibold">{vcUi.dem}</span> đúng · <span className="text-danger fw-semibold">{vcUi.sai}</span> sai mẫu
                </div>
              </div>
              <div className="col-md-3 col-12">
                <label className={'btn btn-outline-secondary btn-sm w-100 mb-1' + (vcUi.dangChay ? ' disabled' : '')}>
                  <i className="bi bi-upload me-1" />
                  Nạp video băng chuyền
                  <input type="file" accept="video/*" hidden disabled={vcUi.dangChay} onChange={napVideo} />
                </label>
                <button type="button" className="btn btn-outline-secondary btn-sm w-100 mb-1" disabled={vcUi.dangChay} onClick={napVideoDemo}>
                  <i className="bi bi-collection-play me-1" />
                  Video demo
                </button>
                <button type="button" className="btn btn-outline-secondary btn-sm w-100" disabled={vcUi.dangChay || !coVideo} onClick={goVideo}>
                  <i className="bi bi-x-circle me-1" />
                  Gỡ video
                </button>
              </div>
              <div className="col-md-3 col-6">
                <button type="button" className="btn btn-primary w-100" disabled={vcUi.dangChay || anhMau.length < SO_MAU_TOI_THIEU} onClick={batDauDemVideo}>
                  <i className="bi bi-play-fill me-1" />
                  Bắt đầu đếm
                </button>
              </div>
              <div className="col-md-3 col-6">
                <button type="button" className="btn btn-outline-danger w-100" disabled={!vcUi.dangChay} onClick={() => ketThucDemVideo(false)}>
                  <i className="bi bi-stop-fill me-1" />
                  Dừng
                </button>
              </div>
            </div>
          </TheNoiDung>

          <TheNoiDung tieuDe="Lịch sử kiểm đếm" icon="bi-clock-history" khongDem>
            <BangDuLieu cot={COT} dong={ds} khoaDong="maKD" dangTai={lichSu.dangTai} rong="Chưa có phiên kiểm đếm nào." />
          </TheNoiDung>
        </div>
      </div>

      {/* The video doc file — an, chi dung lam nguon khung hinh cho phien dem */}
      <video ref={videoFileRef} muted playsInline style={{ display: 'none' }} onError={() => vcRef.current.dangChay && vcRef.current.dungVideo && ketThucDemVideo(false)} />
    </>
  )
}
