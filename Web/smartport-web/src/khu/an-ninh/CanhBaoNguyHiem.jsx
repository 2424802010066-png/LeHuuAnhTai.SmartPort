import { useEffect, useRef, useState } from 'react'
import { goiApi, taiBlob } from '../../api/client'
import { useApi } from '../../api/useApi'
import { NhanTrangThai, TheChiSo, TheNoiDung, Truong, useThongBao } from '../../components/ui'
import { chuongBao, coiBatDau, coiDung } from './anNinhAi'
import {
  LOAI_VUNG,
  TEN_MUC,
  chiTietRuiRo,
  chuanBiGuiCauHinh,
  daNhanKetQua,
  diemQuaGan,
  gomBaoAnNinh,
  kichThuocHopLe,
  loiHieuChinh,
  noiDungBaoAnNinh,
  taoHangTuanTu,
  veAnToan,
} from './veAnToan'
import './an-ninh.css'

const CANH_TOI_DA = 960 // khung gui may chu: canh dai toi da (YOLO chay 640, du chi tiet nguoi o xa)
const MAC_DINH = { vung: [], hieuChinh: null, gocNhin: 60, caoCamera: 6, nguong: { toc_do_toi_da: 15, can_bao_ho: true } }
const NGHI_GUI_LAI_CAU_HINH = 90000 // ms: may chu bo phien sau 120 s khong nhan khung -> nghi lau thi gui lai cau hinh
const LOI_MAT_TIN_HIEU = 3 // so lan lien tiep khong lay duoc anh camera IP -> bao mat tin hieu
const HANG_BAO_TOI_DA = 6 // so lan bao phong an ninh dang cho toi da: may chu an ninh treo -> khong don hang vo han (bao tre, ton RAM)

function docCauHinh(khoa) {
  try {
    const ch = { ...MAC_DINH, ...JSON.parse(localStorage.getItem('smartport.an-toan.' + khoa)) }
    // hieu chinh da luu bi suy bien (diem trung, kich thuoc 0 tu ban cu) -> bo, khong de moi lan Bat dau deu loi
    if (ch.hieuChinh && loiHieuChinh(ch.hieuChinh.anh, ch.hieuChinh.met, 16, 9, 0.5)) ch.hieuChinh = null
    return ch
  } catch {
    return MAC_DINH
  }
}

function luuCauHinh(khoa, ch) {
  try {
    localStorage.setItem('smartport.an-toan.' + khoa, JSON.stringify(ch))
  } catch {
    /* trinh duyet chan localStorage: van dung trong phien */
  }
}

const ngu = (ms) => new Promise((ok) => setTimeout(ok, ms))
const maPhien = () => (crypto.randomUUID ? crypto.randomUUID() : String(Math.random()).slice(2) + Date.now())
const maNhatKy = (e) => e.ma + '@' + e.t

function dinhGio(t) {
  const p = Math.floor(t / 60)
  const g = t - p * 60
  return p + ':' + (g < 10 ? '0' : '') + g.toFixed(1)
}

/**
 * CANH BAO NGUY HIEM AI (AN TOAN LAO DONG CANG): webcam / video / camera IP -> moi khung gui dich vu AI (Python, GPU):
 * YOLO26 phat hien -> ByteTrack theo vet -> doi sang met -> Kalman van toc -> du bao va cham (TTC), vung nguy hiem,
 * qua toc do, nguoi nga, thieu mu / ao phan quang. NGUY HIEM: coi + bang do + tu bao phong an ninh (su kien realtime).
 */
export default function CanhBaoNguyHiem() {
  const bao = useThongBao()
  const { duLieu: tt, loi: loiAi, taiLai: taiLaiAi } = useApi('/api/ai-thi-giac/an-toan/thong-tin')
  const { duLieu: dsCam } = useApi('/api/camera')
  const [nguon, setNguon] = useState('video')
  const [tep, setTep] = useState(null)
  const [urlVideo, setUrlVideo] = useState(null)
  const [maCam, setMaCam] = useState('')
  const [dangChay, setDangChay] = useState(false)
  const [dangMo, setDangMo] = useState(false)
  const [kq, setKq] = useState(null)
  const [coKhung, setCoKhung] = useState(false)
  const [loiGiamSat, setLoiGiamSat] = useState(null)
  const [matTinHieu, setMatTinHieu] = useState(null)
  const [nhatKy, setNhatKy] = useState([])
  const [cheDoVe, setCheDoVe] = useState(null)
  const [diemTam, setDiemTam] = useState([])
  const [vungMoi, setVungMoi] = useState({ ten: 'Dưới cần trục', loai: 'cam-nguoi', gioiHan: 10 })
  const [kichThuocHC, setKichThuocHC] = useState({ rong: 10, dai: 20 })
  const [tuBao, setTuBao] = useState(true)
  const [tatTieng, setTatTieng] = useState(false)
  const [tocPhat, setTocPhat] = useState(1)
  const [viTri, setViTri] = useState('Bãi container')
  const [hieuNang, setHieuNang] = useState({ fps: 0, ms: 0 })
  const khoa = nguon === 'ip' ? 'ip.' + maCam : nguon
  const [cauHinh, setCauHinh] = useState(() => docCauHinh('video'))
  const ban = dangChay || dangMo

  const videoRef = useRef(null)
  const hienRef = useRef(null)
  const chupRef = useRef(null) // canvas chup khung de GUI may chu
  const khungRef = useRef(null) // canvas khung DA PHAN TICH: man hinh chi ve khung nay + ket qua cua chinh no
  const chayRef = useRef(0)
  const dangChayRef = useRef(false) // dang mo nguon / dang giam sat (chan bam dup Bat dau)
  const kqRef = useRef(null)
  const cauHinhRef = useRef(cauHinh)
  const phienBanRef = useRef({ gui: -1, hien: 0 })
  const luongRef = useRef(null)
  const hangBaoRef = useRef(null) // bao phong an ninh gui TUAN TU (khong ban song song qua chong trung), co gioi han hang
  if (!hangBaoRef.current) hangBaoRef.current = taoHangTuanTu(HANG_BAO_TOI_DA)
  const diemTamRef = useRef([])
  const cheDoVeRef = useRef(null)
  // vong lap giam sat la closure cua lan render luc bam Bat dau -> doc gia tri MOI NHAT qua ref
  const tuBaoRef = useRef(tuBao)
  const tatTiengRef = useRef(tatTieng)
  const viTriRef = useRef(viTri)
  cauHinhRef.current = cauHinh
  diemTamRef.current = diemTam
  cheDoVeRef.current = cheDoVe
  tuBaoRef.current = tuBao
  tatTiengRef.current = tatTieng
  viTriRef.current = viTri

  // doi nguon -> nap cau hinh rieng cua nguon do
  useEffect(() => {
    const ch = docCauHinh(khoa)
    cauHinhRef.current = ch
    setCauHinh(ch)
    phienBanRef.current.hien++
  }, [khoa])

  function doiCauHinh(f) {
    setCauHinh((c) => {
      const moi = f(c)
      luuCauHinh(khoa, moi)
      cauHinhRef.current = moi // cung luc voi phien ban: vong lap khong gui cau hinh cu kem phien ban moi
      phienBanRef.current.hien++
      return moi
    })
  }

  // dich vu AI dang nap mo hinh luc mo trang -> tu kiem tra lai moi 5 s (khong khoa nut Bat dau vinh vien)
  const thuLaiAi = !!loiAi && loiAi.trangThai !== 401 && loiAi.trangThai !== 403
  useEffect(() => {
    if (!thuLaiAi) return undefined
    const id = setInterval(() => taiLaiAi({ im: true }), 5000)
    return () => clearInterval(id)
  }, [thuLaiAi, taiLaiAi])

  useEffect(() => {
    if (!tep) {
      setUrlVideo(null) // bo chon tep (huy hop chon file): khong giu URL blob vua bi thu hoi
      return undefined
    }
    const u = URL.createObjectURL(tep)
    setUrlVideo(u)
    return () => URL.revokeObjectURL(u)
  }, [tep])

  useEffect(() => {
    if (videoRef.current) videoRef.current.playbackRate = tocPhat
  }, [tocPhat, urlVideo])

  /** tat webcam + go luong khoi the video (srcObject uu tien hon src: con giu thi tep video khong hien khung dau) */
  function nhaWebcam() {
    luongRef.current?.getTracks().forEach((t) => t.stop())
    luongRef.current = null
    const v = videoRef.current
    if (v?.srcObject) v.srcObject = null
  }

  // doi nguon luc dang nghi: nha webcam con giu
  useEffect(() => {
    if (nguon !== 'webcam' && !dangChayRef.current) nhaWebcam()
  }, [nguon])

  // dung han khi roi trang
  useEffect(
    () => () => {
      chayRef.current++
      dangChayRef.current = false
      coiDung()
      nhaWebcam()
    },
    [],
  )

  /** ve lai man hinh: khung da phan tich + lop canh bao (+ diem dang ve) */
  function veLai() {
    const c = hienRef.current
    const nguonAnh = khungRef.current
    if (!c || !nguonAnh || !nguonAnh.width) return
    c.width = nguonAnh.width
    c.height = nguonAnh.height
    const g = c.getContext('2d')
    g.drawImage(nguonAnh, 0, 0)
    veAnToan(g, c.width, c.height, kqRef.current, cauHinhRef.current, diemTamRef.current, cheDoVeRef.current)
  }
  useEffect(veLai, [cauHinh, diemTam, cheDoVe, kq])

  /** chep khung (canvas) sang canvas khung da phan tich */
  function chepKhung(nguonAnh) {
    if (!nguonAnh?.width) return
    if (!khungRef.current) khungRef.current = document.createElement('canvas')
    const k = khungRef.current
    k.width = nguonAnh.width
    k.height = nguonAnh.height
    k.getContext('2d').drawImage(nguonAnh, 0, 0)
    setCoKhung(true)
  }

  /** chup 1 khung tu nguon vao canvas cv (thu nho canh dai <= 960) — tra thoi diem t (giay) hoac null */
  async function chup(cv) {
    let nguonAnh
    let w
    let h
    let t
    if (nguon === 'ip') {
      if (!maCam) return null
      const blob = await taiBlob('/api/camera/' + maCam + '/anh')
      nguonAnh = await createImageBitmap(blob)
      w = nguonAnh.width
      h = nguonAnh.height
      t = performance.now() / 1000
    } else {
      const v = videoRef.current
      if (!v || v.readyState < 2 || !v.videoWidth) return null
      if (nguon === 'video' && (v.paused || v.ended)) return null
      nguonAnh = v
      w = v.videoWidth
      h = v.videoHeight
      t = nguon === 'video' ? v.currentTime : performance.now() / 1000
    }
    const ti = Math.min(1, CANH_TOI_DA / Math.max(w, h))
    cv.width = Math.round(w * ti)
    cv.height = Math.round(h * ti)
    cv.getContext('2d').drawImage(nguonAnh, 0, 0, cv.width, cv.height)
    if (nguon === 'ip') nguonAnh.close?.() // ImageBitmap: giai phong anh giai ma ngay, khong doi GC (camera IP ~2 khung/s)
    return t
  }

  /** 1 POST cho ca nhom su kien cung loai (vd 3 nguoi thieu mu cung khung -> 1 su co, khong ghi trung) */
  async function baoPhongAnNinh(nhom, anh) {
    try {
      const r = await goiApi('/api/an-ninh/su-co', {
        method: 'POST',
        body: { ...noiDungBaoAnNinh(nhom), viTri: viTriRef.current, anh },
        signal: AbortSignal.timeout?.(20000), // hang tuan tu: 1 yeu cau treo khong chan cac lan bao sau
      })
      return r?.duLieu?.trung ? 'trùng (đã báo < 30 s)' : r?.id ? '#' + r.id : 'đã gửi'
    } catch (er) {
      return 'lỗi: ' + (er?.message ?? '')
    }
  }

  /** bo ket qua cu: khong giu bang NGUY HIEM / chi so cua khung cuoi khi da dung / mat tin hieu */
  function xoaKetQua() {
    kqRef.current = null
    setKq(null)
    setHieuNang({ fps: 0, ms: 0 })
    veLai()
  }

  async function batDau() {
    if (dangChayRef.current) return // dang mo nguon / dang chay (bam dup): bo qua
    if (nguon === 'video' && !urlVideo) return bao.loi('Chọn video trước.')
    if (nguon === 'ip' && !maCam) return bao.loi('Chọn camera IP.')
    dangChayRef.current = true
    const lan = ++chayRef.current
    const phien = maPhien()
    const pb = phienBanRef.current
    pb.gui = -1
    setNhatKy([])
    setLoiGiamSat(null)
    setMatTinHieu(null)
    kqRef.current = null
    setKq(null)
    setDangMo(true)
    try {
      if (nguon === 'webcam') {
        nhaWebcam()
        const luong = await navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 }, height: { ideal: 720 } } })
        // roi trang trong luc trinh duyet hoi quyen -> tat ngay luong vua nhan (khong de camera sang mai)
        if (chayRef.current !== lan) {
          luong.getTracks().forEach((t) => t.stop())
          return
        }
        luongRef.current = luong
        videoRef.current.srcObject = luong
        await videoRef.current.play()
      } else if (nguon === 'video') {
        nhaWebcam()
        const v = videoRef.current
        v.currentTime = 0
        v.playbackRate = tocPhat
        await v.play()
      }
    } catch (e) {
      if (chayRef.current === lan) {
        nhaWebcam()
        dangChayRef.current = false
        setDangMo(false)
        bao.loi('Không mở được nguồn hình: ' + (e?.message ?? e))
      }
      return
    }
    if (chayRef.current !== lan) return
    // moi lan chay 1 canvas rieng: vong lap cu (Dung roi Bat dau lai khi dang cho anh camera IP) khong ghi de khung cua vong moi
    chupRef.current = document.createElement('canvas')
    setDangMo(false)
    setDangChay(true)
    let dem = 0
    let moc = performance.now()
    let lucNhan = performance.now() // lan cuoi may chu tra ket qua
    let khungTruoc = 0
    let loiLienTiep = 0
    let loiDung = null
    try {
      while (chayRef.current === lan) {
        let t = null
        let loiChup = null
        try {
          t = await chup(chupRef.current)
        } catch (e) {
          loiChup = e
        }
        if (chayRef.current !== lan) break
        // webcam bi rut: the video giu khung cuoi nen chup VAN tra khung dong bang (t != null) -> kiem track truc tiep
        if (nguon === 'webcam' && luongRef.current?.getVideoTracks().every((tr) => tr.readyState === 'ended')) {
          loiDung = 'Webcam đã ngắt kết nối.'
          break
        }
        if (t == null) {
          if (nguon === 'video' && videoRef.current?.ended) break
          // camera IP mat tin hieu: bao ro + bo canh bao cu (khong dung hinh nhu dang chay)
          if (loiChup && ++loiLienTiep === LOI_MAT_TIN_HIEU) {
            setMatTinHieu(loiChup?.message || 'không lấy được ảnh')
            xoaKetQua()
          }
          await ngu(nguon === 'ip' ? 500 : 60)
          continue
        }
        if (loiLienTiep >= LOI_MAT_TIN_HIEU) {
          setMatTinHieu(null)
          dem = 0
          moc = performance.now()
        }
        loiLienTiep = 0
        const anh = chupRef.current.toDataURL('image/jpeg', 0.82)
        if (performance.now() - lucNhan > NGHI_GUI_LAI_CAU_HINH) pb.gui = -1
        // chup phien ban + cau hinh CUNG luc; xong moi danh dau dung phien ban nay (doi cau hinh trong luc cho -> gui lai)
        const lanGui = chuanBiGuiCauHinh(pb)
        const chGui = lanGui.guiCauHinh ? cauHinhRef.current : undefined
        let r
        try {
          r = await goiApi('/api/ai-thi-giac/an-toan/khung', {
            method: 'POST',
            body: { phien, anh, t, fps: 8, cauHinh: chGui },
          })
        } catch (e) {
          if (chayRef.current === lan) {
            loiDung = e?.message ?? String(e)
            bao.loi(e)
          }
          break
        }
        if (chayRef.current !== lan) break
        if (!r || typeof r !== 'object') {
          loiDung = 'Dịch vụ AI trả kết quả không hợp lệ.'
          break
        }
        // phien may chu vua tao lai (canCauHinh / so khung lui) -> khung ke gui lai vung + hieu chinh + nguong
        khungTruoc = daNhanKetQua(pb, lanGui, r, khungTruoc)
        lucNhan = performance.now()
        // man hinh = khung VUA GUI (chua bi lan chup ke ghi de) + ket qua cua chinh khung do -> hop / du bao khong lech nhip
        chepKhung(chupRef.current)
        kqRef.current = r
        veLai()
        setKq(r)
        dem++
        const bayGio = performance.now()
        if (bayGio - moc > 1000) {
          setHieuNang({ fps: Math.round((dem * 10000) / (bayGio - moc)) / 10, ms: r.ms })
          dem = 0
          moc = bayGio
        }
        if (r.suKien?.length) {
          const anhSuKien = hienRef.current?.toDataURL('image/jpeg', 0.7)
          if (!tatTiengRef.current) {
            if (r.suKien.some((e) => e.muc === 'nguy-hiem')) coiBatDau(3)
            else chuongBao()
          }
          const lucMay = new Date()
          const moi = r.suKien.map((e) => ({ ...e, ma: maNhatKy(e), anh: anhSuKien, lucMay, baoAnNinh: null })).reverse()
          setNhatKy((ds) => [...moi, ...ds].slice(0, 60))
          if (tuBaoRef.current) {
            // moi loai su co chi 1 POST / khung, cac POST xep hang tuan tu
            for (const nhom of gomBaoAnNinh(r.suKien)) {
              const dsMa = new Set(nhom.map(maNhatKy))
              const ghiKq = (kqBao) => setNhatKy((ds) => ds.map((x) => (dsMa.has(x.ma) ? { ...x, baoAnNinh: kqBao } : x)))
              const hang = hangBaoRef.current
              // toi luot moi xet lai cong tac: vua tat "Tu bao" thi cac lan con trong hang cung khong gui
              const daXep = hang.them(async () =>
                ghiKq(tuBaoRef.current ? await baoPhongAnNinh(nhom, anhSuKien) : 'không gửi — đã tắt tự báo'),
              )
              // may chu an ninh treo: hang day thi bo lan bao nay (rui ro con thi AI bao lai sau 4 s), khong xep vo han
              if (!daXep) ghiKq('chưa gửi — máy chủ an ninh đang chậm (' + hang.soCho + ' lần báo đang chờ)')
            }
          }
        }
      }
    } catch (e) {
      loiDung = e?.message ?? String(e)
    } finally {
      if (chayRef.current === lan) {
        // tu dung (het video / loi / mat webcam): nha webcam, bo bang canh bao cua khung cuoi
        dangChayRef.current = false
        nhaWebcam()
        videoRef.current?.pause()
        setMatTinHieu(null)
        xoaKetQua()
        setDangChay(false)
        if (loiDung) setLoiGiamSat(loiDung)
      }
    }
  }

  function dung() {
    chayRef.current++
    dangChayRef.current = false
    setDangChay(false)
    setDangMo(false)
    setMatTinHieu(null)
    coiDung()
    videoRef.current?.pause()
    nhaWebcam()
    xoaKetQua()
  }

  // click len khung hinh: them diem cho vung / hieu chinh (toa do 0..1)
  function bamKhung(ev) {
    if (!cheDoVe) return
    const c = hienRef.current
    const r = c.getBoundingClientRect()
    if (!r.width || !r.height) return
    const p = [(ev.clientX - r.left) / r.width, (ev.clientY - r.top) / r.height]
    const W = c.width || 16
    const H = c.height || 9
    const ds = diemTamRef.current
    if (cheDoVe === 'hieu-chinh') {
      // bam dup / bam lai cung goc -> bo qua (2 goc trung lam phep doi sang met suy bien ma van bao "da hieu chinh")
      if (diemQuaGan(p, ds, W, H)) return bao.canhBao('Điểm này trùng hoặc quá gần góc đã chọn — bấm góc tiếp theo.')
      const moi = [...ds, p]
      if (moi.length < 4) return setDiemTam(moi)
      const rong = Number(kichThuocHC.rong)
      const dai = Number(kichThuocHC.dai)
      const met = [[0, 0], [rong, 0], [rong, dai], [0, dai]]
      const loi = loiHieuChinh(moi, met, W, H)
      if (loi) {
        setDiemTam([])
        return bao.loi(loi + ' Đã xóa 4 điểm — bấm lại từ góc gần-trái.')
      }
      doiCauHinh((c0) => ({ ...c0, hieuChinh: { anh: moi, met } }))
      setDiemTam([])
      setCheDoVe(null)
      bao.thanhCong('Đã hiệu chỉnh mặt đất: khoảng cách và tốc độ tính bằng mét thật.')
    } else {
      if (diemQuaGan(p, ds.slice(-1), W, H, 0.005)) return // bam dup khi ve vung
      setDiemTam([...ds, p])
    }
  }

  function batDauHieuChinh() {
    if (!kichThuocHopLe(Number(kichThuocHC.rong)) || !kichThuocHopLe(Number(kichThuocHC.dai)))
      return bao.loi('Nhập kích thước thật Rộng / Dài ≥ 1 m trước khi hiệu chỉnh.')
    setCheDoVe('hieu-chinh')
    setDiemTam([])
    hienKhungDau()
  }

  function xongVung() {
    if (diemTam.length < 3) return bao.loi('Vùng cần ít nhất 3 điểm.')
    doiCauHinh((c0) => ({ ...c0, vung: [...c0.vung, { ...vungMoi, gioiHan: Number(vungMoi.gioiHan) || 10, diem: diemTam }] }))
    setDiemTam([])
    setCheDoVe(null)
  }

  // video vua chon: hien khung dau de ve vung / hieu chinh truoc khi chay
  async function hienKhungDau() {
    if (dangChayRef.current) return
    const cv = document.createElement('canvas')
    const t = await chup(cv).catch(() => null)
    if (dangChayRef.current) return // vua bam Bat dau trong luc cho anh: vong lap giam sat lo man hinh
    const v = videoRef.current
    if (t == null) {
      if (!(v?.readyState >= 2 && v.videoWidth)) return veLai()
      const ti = Math.min(1, CANH_TOI_DA / Math.max(v.videoWidth, v.videoHeight))
      cv.width = Math.round(v.videoWidth * ti)
      cv.height = Math.round(v.videoHeight * ti)
      cv.getContext('2d').drawImage(v, 0, 0, cv.width, cv.height)
    }
    chepKhung(cv)
    veLai()
  }

  const nguyHiem = (kq?.ruiRo ?? []).filter((r) => r.muc === 'nguy-hiem')
  const canhBao = (kq?.ruiRo ?? []).filter((r) => r.muc !== 'nguy-hiem')
  const soNguoi = (kq?.doiTuong ?? []).filter((o) => o.lop === 'nguoi').length
  const soXe = (kq?.doiTuong ?? []).length - soNguoi
  const bh = tt?.baoHo
  // chat luong canh bao thieu mu o MUC TUNG NGUOI (TEST giu rieng): trung binh moi dieu kien xa / gan / nen JPEG
  const f1Mu = bh?.f1TheoCoNguoi?.mu
  const f1MuTb = f1Mu ? Object.values(f1Mu).reduce((s, x) => s + x.catNguoi, 0) / Object.values(f1Mu).length : null
  const phanTram = (x) => (x * 100).toFixed(0) + '%'

  return (
    <>
      {loiAi && (
        <div className="alert alert-warning">
          <i className="bi bi-info-circle me-1" />
          Dịch vụ AI máy chủ (Python + GPU) chưa chạy — cảnh báo nguy hiểm cần chế độ microservices (lối tắt “SmartPort - Chay”).
          {thuLaiAi && ' Đang tự kiểm tra lại mỗi 5 giây…'}
        </div>
      )}
      {loiGiamSat && !dangChay && (
        <div className="alert alert-danger d-flex align-items-start gap-2" role="alert">
          <i className="bi bi-stop-circle-fill mt-1" />
          <div className="flex-fill">
            <b>Giám sát đã dừng vì lỗi:</b> {loiGiamSat} Cảnh báo không còn được cập nhật — bấm “Bắt đầu giám sát” để chạy lại.
          </div>
          <button type="button" className="btn-close" aria-label="Đóng" onClick={() => setLoiGiamSat(null)} />
        </div>
      )}
      {matTinHieu && dangChay && (
        <div className="alert alert-danger" role="alert">
          <i className="bi bi-camera-video-off-fill me-1" />
          <b>Mất tín hiệu camera</b> ({matTinHieu}) — đang thử kết nối lại; cảnh báo tạm ngưng cho tới khi có hình.
        </div>
      )}
      {kq?.canhBaoCauHinh?.length > 0 && (
        // may chu bo qua 1 phan cau hinh (hieu chinh 4 diem suy bien, vung vat can tren chan troi...) -> nguoi van hanh phai biet
        <div className="alert alert-warning" role="status">
          <i className="bi bi-sliders me-1" />
          <b>Cấu hình chưa dùng được:</b>
          <ul className="mb-0 mt-1 small">
            {kq.canhBaoCauHinh.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </div>
      )}
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-exclamation-octagon" mau="do" so={nguyHiem.length} nhan="nguy hiểm đang diễn ra" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-people" mau="xanh" so={soNguoi + ' · ' + soXe} nhan="người · xe đang theo vết" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-speedometer2" mau="la" so={hieuNang.fps + ' fps'} nhan={'máy chủ ' + hieuNang.ms + ' ms/khung'} />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo
            icon="bi-cone-striped"
            mau="cam"
            so={f1MuTb != null ? f1MuTb.toFixed(2) : bh?.mAP50 != null ? (bh.mAP50 * 100).toFixed(1) + '%' : '—'}
            nhan={f1MuTb != null ? 'F1 phát hiện thiếu mũ (test, xa/gần, nén)' : 'mAP50 mô hình đồ bảo hộ (test)'}
          />
        </div>
      </div>

      {(nguyHiem.length > 0 || canhBao.length > 0) && (
        <div className={'an-bang-canh-bao ' + (nguyHiem.length ? 'nguy' : 'chu-y')} role="alert" aria-live="assertive">
          {[...nguyHiem, ...canhBao].slice(0, 4).map((r) => (
            <div key={r.ma}>
              <i className={'bi me-2 ' + (r.muc === 'nguy-hiem' ? 'bi-exclamation-octagon-fill' : 'bi-exclamation-triangle-fill')} />
              <b>{TEN_MUC[r.muc]}:</b> {r.moTa} <span className="opacity-75">— {chiTietRuiRo(r)}</span>
            </div>
          ))}
        </div>
      )}

      <div className="row g-3">
        <div className="col-12 col-xl-8">
          <TheNoiDung
            tieuDe="Hình camera và vùng phát hiện"
            icon="bi-camera-video"
            phai={
              kq && (
                <span className="small text-body-secondary">
                  {kq.matDat === 'homography' ? 'Đo bằng mét đã hiệu chỉnh' : kq.matDat === 'uoc-luong' ? 'Tự ước lượng tỉ lệ (nên hiệu chỉnh)' : 'Đang ước lượng tỉ lệ…'}
                </span>
              )
            }
          >
            <div className="an-khung-an-toan">
              <canvas
                ref={hienRef}
                onClick={bamKhung}
                className={cheDoVe ? 'dang-ve' : ''}
                aria-label="Khung hình camera kèm vùng nguy hiểm và cảnh báo AI"
              />
              {!kq && !coKhung && (
                <div className="an-khung-trong">
                  <i className="bi bi-camera-reels fs-1 d-block mb-2" />
                  Chọn nguồn hình rồi bấm “Bắt đầu giám sát”.
                </div>
              )}
              {/* the video an: chi de giai ma khung (man hinh hien khung DA GUI + ket qua cua chinh khung do — luon khop) */}
              <video
                ref={videoRef}
                src={nguon === 'video' ? urlVideo ?? undefined : undefined}
                muted
                playsInline
                className="an-video-an"
                onLoadedData={hienKhungDau}
                onSeeked={hienKhungDau}
              />
            </div>
            {cheDoVe && (
              <div className="alert alert-info small mt-2 mb-0 d-flex flex-wrap gap-2 align-items-center">
                {cheDoVe === 'hieu-chinh' ? (
                  <span>
                    Bấm lần lượt <b>4 góc một hình chữ nhật trên mặt đất</b> (gần-trái → gần-phải → xa-phải → xa-trái), kích thước thật{' '}
                    {kichThuocHC.rong} × {kichThuocHC.dai} m — đã chọn {diemTam.length}/4.
                  </span>
                ) : (
                  <span>Bấm các đỉnh của vùng “{vungMoi.ten}” trên khung hình — đã chọn {diemTam.length} điểm.</span>
                )}
                {cheDoVe === 'vung' && (
                  <button type="button" className="btn btn-sm btn-primary" onClick={xongVung}>
                    Xong vùng
                  </button>
                )}
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => (setCheDoVe(null), setDiemTam([]))}>
                  Hủy
                </button>
              </div>
            )}
          </TheNoiDung>

          <TheNoiDung tieuDe={`Nhật ký cảnh báo (${nhatKy.length})`} icon="bi-journal-text">
            {nhatKy.length === 0 ? (
              <div className="small text-body-secondary">Chưa có cảnh báo. Mỗi rủi ro phải xuất hiện ≥ 3/4 khung liên tiếp mới báo (chống báo động giả).</div>
            ) : (
              <div className="an-nhat-ky-at">
                {nhatKy.map((e) => (
                  <div key={e.ma} className={'an-dong-nk ' + e.muc}>
                    {e.anh && <img src={e.anh} alt={'Khung hình lúc ' + e.moTa} />}
                    <div className="small">
                      <div>
                        <NhanTrangThai giaTri={TEN_MUC[e.muc]} mau={e.muc === 'nguy-hiem' ? 'do' : 'warn'} /> <b>{e.moTa}</b>
                      </div>
                      <div className="text-body-secondary">
                        {nguon === 'video' ? 'giây ' + dinhGio(e.t) : e.lucMay.toLocaleTimeString('vi-VN')} · {chiTietRuiRo(e)}
                      </div>
                      {e.baoAnNinh && <div className="text-body-secondary">Phòng an ninh: {e.baoAnNinh}</div>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </TheNoiDung>
        </div>

        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Nguồn hình" icon="bi-sliders">
            <div className="btn-group w-100 mb-3" role="group" aria-label="Nguồn hình">
              {[
                ['video', 'bi-film', 'Video'],
                ['webcam', 'bi-webcam', 'Webcam'],
                ['ip', 'bi-router', 'Camera IP'],
              ].map(([gt, icon, ten]) => (
                <button
                  key={gt}
                  type="button"
                  disabled={ban}
                  className={'btn btn-sm ' + (nguon === gt ? 'btn-primary' : 'btn-outline-primary')}
                  onClick={() => setNguon(gt)}
                >
                  <i className={'bi me-1 ' + icon} />
                  {ten}
                </button>
              ))}
            </div>
            {nguon === 'video' && (
              <>
                <Truong nhan="Video camera hiện trường" goiY="Phát như camera thật — AI đọc từng khung đang phát">
                  <input className="form-control" type="file" accept="video/*" disabled={ban} onChange={(e) => setTep(e.target.files?.[0] ?? null)} />
                </Truong>
                <Truong nhan="Tốc độ phát" goiY="Phát chậm để AI lấy được nhiều khung hơn mỗi giây video">
                  <select className="form-select" value={tocPhat} onChange={(e) => setTocPhat(Number(e.target.value))}>
                    {[0.25, 0.5, 1].map((x) => (
                      <option key={x} value={x}>
                        {x}×
                      </option>
                    ))}
                  </select>
                </Truong>
              </>
            )}
            {nguon === 'ip' && (
              <Truong nhan="Camera IP">
                <select className="form-select" value={maCam} disabled={ban} onChange={(e) => setMaCam(e.target.value)}>
                  <option value="">— chọn camera —</option>
                  {(Array.isArray(dsCam) ? dsCam : []).map((c) => (
                    <option key={c.maCam} value={c.maCam}>
                      {(c.tenCam || 'Camera ' + c.maCam) + (c.viTri ? ' · ' + c.viTri : '')}
                    </option>
                  ))}
                </select>
              </Truong>
            )}
            <Truong nhan="Vị trí (ghi vào sự cố)">
              <input className="form-control" value={viTri} maxLength={50} onChange={(e) => setViTri(e.target.value)} />
            </Truong>
            <div className="d-flex gap-2">
              {/* ev.detail > 1: lan bam thu 2 cua cu bam dup roi vao nut vua doi (Bat dau <-> Dung) -> bo qua */}
              {/* dang mo nguon: van cho HUY — getUserMedia co the treo mai (nguoi dung bo qua hop hoi quyen) */}
              {!ban ? (
                <button type="button" className="btn btn-danger flex-fill" disabled={!!loiAi} onClick={(ev) => ev.detail > 1 || batDau()}>
                  <i className="bi bi-play-circle me-1" />
                  Bắt đầu giám sát
                </button>
              ) : (
                <button type="button" className="btn btn-outline-danger flex-fill" onClick={(ev) => ev.detail > 1 || dung()}>
                  <i className={'bi me-1 ' + (dangMo ? 'bi-hourglass-split' : 'bi-stop-circle')} />
                  {dangMo ? 'Đang mở nguồn hình… (bấm để hủy)' : 'Dừng'}
                </button>
              )}
              <button type="button" className={'btn ' + (tatTieng ? 'btn-secondary' : 'btn-outline-secondary')} onClick={() => (setTatTieng(!tatTieng), coiDung())} title="Tắt / bật còi">
                <i className={'bi ' + (tatTieng ? 'bi-volume-mute' : 'bi-volume-up')} />
              </button>
            </div>
            <div className="form-check form-switch mt-2">
              <input className="form-check-input" type="checkbox" id="tu-bao" checked={tuBao} onChange={(e) => setTuBao(e.target.checked)} />
              <label className="form-check-label small" htmlFor="tu-bao">
                Tự báo phòng an ninh khi NGUY HIỂM / thiếu bảo hộ (sự kiện realtime tới mọi màn hình)
              </label>
            </div>
          </TheNoiDung>

          <TheNoiDung tieuDe="Vùng nguy hiểm" icon="bi-bounding-box-circles">
            <div className="row g-2">
              <div className="col-7">
                <input className="form-control form-control-sm" value={vungMoi.ten} onChange={(e) => setVungMoi({ ...vungMoi, ten: e.target.value })} aria-label="Tên vùng" />
              </div>
              <div className="col-5">
                <select className="form-select form-select-sm" value={vungMoi.loai} onChange={(e) => setVungMoi({ ...vungMoi, loai: e.target.value })} aria-label="Loại vùng">
                  {Object.entries(LOAI_VUNG).map(([k, v]) => (
                    <option key={k} value={k}>
                      {v.ten}
                    </option>
                  ))}
                </select>
              </div>
              {vungMoi.loai === 'gioi-han-toc-do' && (
                <div className="col-12">
                  <div className="input-group input-group-sm">
                    <span className="input-group-text">Giới hạn</span>
                    <input type="number" min={1} max={60} className="form-control" value={vungMoi.gioiHan} onChange={(e) => setVungMoi({ ...vungMoi, gioiHan: e.target.value })} />
                    <span className="input-group-text">km/h</span>
                  </div>
                </div>
              )}
              <div className="col-12">
                <button type="button" className="btn btn-sm btn-outline-danger w-100" onClick={() => (setCheDoVe('vung'), setDiemTam([]), hienKhungDau())}>
                  <i className="bi bi-pencil me-1" />
                  Vẽ vùng trên khung hình
                </button>
              </div>
            </div>
            <ul className="list-unstyled small mt-2 mb-0">
              {cauHinh.vung.map((z, i) => (
                <li key={i} className="d-flex align-items-center gap-2 py-1 border-bottom">
                  <span className="an-o-mau" style={{ background: LOAI_VUNG[z.loai]?.vien }} />
                  <span className="flex-fill">
                    {z.ten} · {LOAI_VUNG[z.loai]?.ten}
                    {z.loai === 'gioi-han-toc-do' ? ' ' + z.gioiHan + ' km/h' : ''}
                  </span>
                  <button type="button" className="btn btn-sm btn-link text-danger p-0" aria-label={'Xóa vùng ' + z.ten}
                    onClick={() => doiCauHinh((c0) => ({ ...c0, vung: c0.vung.filter((_, j) => j !== i) }))}>
                    <i className="bi bi-x-lg" />
                  </button>
                </li>
              ))}
              {cauHinh.vung.length === 0 && <li className="text-body-secondary">Chưa có vùng — vẽ vùng dưới cần trục, bãi xếp, làn xe…</li>}
            </ul>
          </TheNoiDung>

          <TheNoiDung tieuDe="Hiệu chỉnh khoảng cách & ngưỡng" icon="bi-rulers">
            <div className="row g-2 mb-2">
              <div className="col-6">
                <div className="input-group input-group-sm">
                  <span className="input-group-text">Rộng</span>
                  <input type="number" min={1} aria-label="Chiều rộng thật (m)" value={kichThuocHC.rong}
                    className={'form-control' + (kichThuocHopLe(Number(kichThuocHC.rong)) ? '' : ' is-invalid')}
                    onChange={(e) => setKichThuocHC({ ...kichThuocHC, rong: e.target.value })} />
                  <span className="input-group-text">m</span>
                </div>
              </div>
              <div className="col-6">
                <div className="input-group input-group-sm">
                  <span className="input-group-text">Dài</span>
                  <input type="number" min={1} aria-label="Chiều dài thật (m)" value={kichThuocHC.dai}
                    className={'form-control' + (kichThuocHopLe(Number(kichThuocHC.dai)) ? '' : ' is-invalid')}
                    onChange={(e) => setKichThuocHC({ ...kichThuocHC, dai: e.target.value })} />
                  <span className="input-group-text">m</span>
                </div>
              </div>
            </div>
            <div className="d-flex gap-2 mb-3">
              <button type="button" className="btn btn-sm btn-outline-success flex-fill" onClick={batDauHieuChinh}>
                <i className="bi bi-bullseye me-1" />
                Hiệu chỉnh 4 điểm
              </button>
              {cauHinh.hieuChinh && (
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => doiCauHinh((c0) => ({ ...c0, hieuChinh: null }))}>
                  Bỏ hiệu chỉnh
                </button>
              )}
            </div>
            <Truong nhan={`Góc nhìn camera: ${cauHinh.gocNhin}°`} goiY="Dùng khi chưa hiệu chỉnh; sai góc nhìn không làm sai thời điểm báo va chạm theo chiều sâu">
              <input type="range" className="form-range" min={30} max={120} value={cauHinh.gocNhin} onChange={(e) => doiCauHinh((c0) => ({ ...c0, gocNhin: Number(e.target.value) }))} />
            </Truong>
            <Truong nhan={`Chiều cao cột camera: ${cauHinh.caoCamera ?? 6} m`} goiY="Chỉ dùng khi mọi xe / người ở cùng một độ sâu (chưa ước được phối cảnh)">
              <input type="range" className="form-range" min={3} max={30} value={cauHinh.caoCamera ?? 6} onChange={(e) => doiCauHinh((c0) => ({ ...c0, caoCamera: Number(e.target.value) }))} />
            </Truong>
            <Truong nhan={`Tốc độ tối đa trong bãi: ${cauHinh.nguong.toc_do_toi_da} km/h`}>
              <input type="range" className="form-range" min={5} max={40} value={cauHinh.nguong.toc_do_toi_da}
                onChange={(e) => doiCauHinh((c0) => ({ ...c0, nguong: { ...c0.nguong, toc_do_toi_da: Number(e.target.value) } }))} />
            </Truong>
            <div className="form-check form-switch">
              <input className="form-check-input" type="checkbox" id="can-bao-ho" checked={cauHinh.nguong.can_bao_ho}
                onChange={(e) => doiCauHinh((c0) => ({ ...c0, nguong: { ...c0.nguong, can_bao_ho: e.target.checked } }))} />
              <label className="form-check-label small" htmlFor="can-bao-ho">
                Bắt buộc mũ bảo hộ + áo phản quang {tt && !tt.moHinh?.includes('bao_ho') ? '(mô hình bảo hộ chưa nạp)' : ''}
              </label>
            </div>
          </TheNoiDung>

          <TheNoiDung tieuDe="Cách AI phát hiện nguy hiểm" icon="bi-diagram-3">
            <ol className="small mb-0 ps-3 cs-giai-thich">
              <li>
                <b>Phát hiện</b>: YOLO26s (người, ô tô, xe tải, xe buýt, xe máy) + YOLO26s đồ bảo hộ huấn luyện trên Construction-PPE
                {bh?.mAP50 != null && <> (mAP50 test {(bh.mAP50 * 100).toFixed(1)}%)</>}
                {bh?.moHinhChon === 'bao_ho_ben' && <>, thêm ảnh suy giảm (thu nhỏ + mờ + nén JPEG) để bền với camera thật</>}.
              </li>
              <li>
                <b>Đồ bảo hộ 2 tầng</b>: cắt từng người, phóng to 320 px rồi mới tìm mũ / áo phản quang (cả khung bị thu nhỏ thì đầu người
                ở xa chỉ còn vài pixel)
                {f1Mu?.['80'] && <> — người cao 80 px: F1 thiếu mũ {phanTram(f1Mu['80'].caKhung)} → {phanTram(f1Mu['80'].catNguoi)}</>}
                {f1Mu?.['80_jpeg70'] && <>, khung nén JPEG: {phanTram(f1Mu['80_jpeg70'].caKhung)} → {phanTram(f1Mu['80_jpeg70'].catNguoi)}</>}; bỏ phiếu 15 khung.
              </li>
              <li>
                <b>Theo vết</b>: ByteTrack giữ mã từng đối tượng qua các khung (kể cả khi bị che một lúc); phép đo lệch &gt; 4σ (xe khác chạy
                ngang che, hộp gộp 2 xe) bị bỏ qua — Kalman chỉ dự đoán, vết không mất vận tốc.
              </li>
              <li>
                <b>Đổi sang mét</b>: hiệu chỉnh 4 điểm (homography) hoặc tự ước tỉ lệ theo chiều cao người / xe (camera lỗ kim: Z = f/s).
              </li>
              <li>
                <b>Kalman</b> làm mượt vị trí, tính vận tốc (km/h) và đường đi 3 giây tới.
              </li>
              <li>
                <b>Thời gian tới va chạm (TTC)</b>: mô phỏng chuyển động tương đối, xe là đoạn thẳng mép dưới trên mặt đất — NGUY HIỂM khi TTC ≤ 1,5 s,
                cảnh báo ≤ 3 s (chuẩn FCW).
              </li>
              <li>
                <b>Chống báo động giả</b>: rủi ro phải có ≥ 3/4 khung liên tiếp (~0,4 s — tự co theo số khung/giây thật khi máy chủ
                chậm, để không báo trễ); mỗi rủi ro nghỉ 4 s mới báo lại; đã báo NGUY HIỂM thì không báo thêm mức cảnh báo cùng cặp.
              </li>
            </ol>
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
