import { useCallback, useEffect, useRef, useState } from 'react'
import { goiApi } from '../../api/client'
import { cho, useWebcam } from './useWebcam'

const DUONG = '/api/tai-khoan-ca-nhan/khuon-mat'

// Moi lan goi gui 3 khung hinh (API cho toi da 3) — du de bo phieu 2 khung lien tiep
// va tiet kiem luot goi: endpoint dang nhap chung gioi han 10 lan / phut / IP voi mat khau.
const SO_KHUNG = 3
const CACH_KHUNG = 300 // ms giua 2 khung trong cung mot lan goi
const CHO_LAN_SAU = 7 // giay cho truoc lan goi tiep (10 lan/phut/IP)
const TOI_DA_LAN = 8

// Cac trang thai khien phai DUNG han phien quet (khong thu lai tu dong)
const DUNG_HAN = ['spoof', 'service_down', 'multi_face', 'rate_limit']

/**
 * Hop thoai quet khuon mat tren trang dang nhap (thay modal #faceModal + facelogin.js ban MVC).
 * Thanh cong -> goi khiXong(phienDangNhap) de trang cha nhan phien va dieu huong.
 */
export default function QuetKhuonMat({ mo, dong, khiXong }) {
  const { videoRef, batDau, dungLai, chup, loiCamera } = useWebcam()
  const [pha, setPha] = useState('cho') // cho | quet | dat | hong
  const [thongBao, setThongBao] = useState('Đưa khuôn mặt vào giữa vòng tròn rồi bấm Bắt đầu quét.')
  const [chiSo, setChiSo] = useState(null) // { doKhop, chatLuong, buoc, canBuoc, thuThach }
  const [dichVu, setDichVu] = useState(null)
  const [demNguoc, setDemNguoc] = useState(0)
  const huyRef = useRef(false)
  const dangQuetRef = useRef(false)

  // --- Mo hop thoai: hoi dich vu AI con song khong + bat camera ---
  useEffect(() => {
    if (!mo) return undefined
    let con = true
    huyRef.current = false
    setPha('cho')
    setChiSo(null)
    setDemNguoc(0)
    setThongBao('Đưa khuôn mặt vào giữa vòng tròn rồi bấm Bắt đầu quét.')
    goiApi(DUONG + '/dich-vu', { coToken: false })
      .then((d) => {
        if (!con) return
        setDichVu(d)
        if (!d.sanSang) {
          setPha('hong')
          setThongBao('Dịch vụ AI khuôn mặt chưa chạy — bạn hãy đăng nhập bằng mật khẩu, hoặc khởi động dịch vụ rồi bấm Thử lại.')
        }
      })
      .catch(() => {
        if (con) setDichVu(null)
      })
    batDau()
    return () => {
      con = false
      huyRef.current = true
      dungLai()
    }
  }, [mo, batDau, dungLai])

  const dongHop = useCallback(() => {
    huyRef.current = true
    dangQuetRef.current = false
    dungLai()
    dong?.()
  }, [dong, dungLai])

  // Esc de dong
  useEffect(() => {
    if (!mo) return undefined
    const bam = (e) => {
      if (e.key === 'Escape') dongHop()
    }
    document.addEventListener('keydown', bam)
    return () => document.removeEventListener('keydown', bam)
  }, [mo, dongHop])

  /** Dem nguoc giua 2 lan goi API — tra ve false neu nguoi dung huy giua chung */
  async function doiLuot(giay) {
    for (let con = giay; con > 0; con--) {
      if (huyRef.current) return false
      setDemNguoc(con)
      await cho(1000)
    }
    setDemNguoc(0)
    return !huyRef.current
  }

  async function quet() {
    if (dangQuetRef.current) return
    dangQuetRef.current = true
    huyRef.current = false
    setPha('quet')
    setChiSo(null)
    setThongBao('Đang quét… giữ yên khuôn mặt trong vòng tròn.')

    if (!(await batDau())) {
      dangQuetRef.current = false
      setPha('hong')
      setThongBao('Không mở được camera — hãy cấp quyền camera cho trình duyệt rồi thử lại.')
      return
    }

    const maPhien = (crypto.randomUUID?.() || 'p' + Date.now() + '-' + Math.random().toString(36).slice(2)).slice(0, 64)

    for (let lan = 1; lan <= TOI_DA_LAN; lan++) {
      const khung = []
      for (let i = 0; i < SO_KHUNG; i++) {
        if (huyRef.current) return dungQuet()
        const anh = chup()
        if (anh) khung.push(anh)
        if (i < SO_KHUNG - 1) await cho(CACH_KHUNG)
      }
      if (huyRef.current) return dungQuet()
      if (!khung.length) {
        setThongBao('Chưa lấy được hình từ camera — kiểm tra lại quyền camera.')
        setPha('hong')
        dangQuetRef.current = false
        return
      }

      let kq
      try {
        kq = await goiApi(DUONG + '/dang-nhap', { method: 'POST', body: { maPhien, anh: khung }, coToken: false })
      } catch (e) {
        dangQuetRef.current = false
        setPha('hong')
        setThongBao(e.message)
        return
      }
      if (huyRef.current) return dungQuet()

      if (kq.ok && kq.dangNhap) {
        dangQuetRef.current = false
        dungLai()
        setPha('dat')
        setChiSo({ doKhop: kq.doKhop, chatLuong: kq.chatLuong })
        setThongBao(kq.thongBao)
        await cho(1100)
        khiXong?.(kq.dangNhap)
        return
      }

      setChiSo({
        doKhop: kq.doKhop,
        chatLuong: kq.chatLuong,
        buoc: kq.buoc,
        canBuoc: kq.canBuoc,
        thuThach: kq.trangThai === 'challenge',
      })
      setThongBao(kq.thongBao)

      if (DUNG_HAN.includes(kq.trangThai)) {
        dangQuetRef.current = false
        setPha('hong')
        return
      }
      if (lan === TOI_DA_LAN) {
        dangQuetRef.current = false
        setPha('hong')
        setThongBao('Chưa nhận diện được sau nhiều lần thử — hãy thử nơi đủ sáng hoặc đăng nhập bằng mật khẩu.')
        return
      }
      if (!(await doiLuot(CHO_LAN_SAU))) return dungQuet()
    }
  }

  function dungQuet() {
    dangQuetRef.current = false
    setDemNguoc(0)
    return undefined
  }

  function huyQuet() {
    huyRef.current = true
    dangQuetRef.current = false
    setDemNguoc(0)
    setPha('cho')
    setChiSo(null)
    setThongBao('Đã dừng quét. Bấm Bắt đầu quét để thử lại.')
  }

  if (!mo) return null

  const vong = 'kh-vong' + (pha === 'cho' ? ' kh-cho' : pha === 'dat' ? ' kh-dat' : pha === 'hong' ? ' kh-hong' : '')

  return (
    <div className="kh-phu-man" role="dialog" aria-modal="true" aria-label="Đăng nhập bằng khuôn mặt">
      <div className="kh-hop-quet">
        <div className="d-flex justify-content-between align-items-start gap-2">
          <div>
            <h2>Đăng nhập bằng khuôn mặt</h2>
            <div className="kh-ghi-chu">
              AI ArcFace 512D · chống giả mạo
              {dichVu ? ` · ngưỡng khớp ≥ ${dichVu.nguongTinCay}% · ${dichVu.soKhungCan} khung liên tiếp` : ''}
            </div>
          </div>
          <button type="button" className="kh-nut-dong" onClick={dongHop} title="Đóng" aria-label="Đóng">
            <i className="bi bi-x-lg" />
          </button>
        </div>

        <div className={vong}>
          <div className="kh-long">
            <video ref={videoRef} playsInline muted />
            {pha === 'quet' && (
              <>
                <div className="kh-luoi" />
                <div className="kh-tia" />
                <div className="kh-song" />
                <div className="kh-song kh-song2" />
              </>
            )}
            {(pha === 'dat' || pha === 'hong') && (
              <div className={'kh-ket-qua ' + (pha === 'dat' ? 'kh-dat' : 'kh-hong')}>
                <span className="kh-huy-hieu">
                  <i className={'bi ' + (pha === 'dat' ? 'bi-check-lg' : 'bi-x-lg')} />
                </span>
              </div>
            )}
          </div>
        </div>

        <div className={'kh-trang-thai ' + (pha === 'dat' ? 'kh-dat' : pha === 'hong' ? 'kh-hong' : '')}>
          {thongBao}
          <div>
            {chiSo?.thuThach && <span className="kh-chi-so kh-vang">Quay đầu nhẹ trái – phải</span>}
            {chiSo?.doKhop != null && <span className="kh-chi-so">Độ khớp {chiSo.doKhop}%</span>}
            {chiSo?.chatLuong != null && <span className="kh-chi-so kh-lam">Ảnh {Math.round(chiSo.chatLuong)}/100</span>}
            {chiSo?.buoc != null && chiSo?.canBuoc != null && (
              <span className="kh-chi-so">
                Xác minh {chiSo.buoc}/{chiSo.canBuoc}
              </span>
            )}
            {demNguoc > 0 && <span className="kh-chi-so kh-vang">Quét tiếp sau {demNguoc}s</span>}
          </div>
        </div>

        <div className="kh-nut-doc">
          {pha !== 'quet' && (
            <button type="button" className="btn kh-nut-mat" onClick={quet} disabled={dichVu != null && !dichVu.sanSang}>
              <i className="bi bi-radar" />
              {pha === 'hong' ? 'Thử lại' : 'Bắt đầu quét'}
            </button>
          )}
          {pha === 'quet' && (
            <button type="button" className="btn kh-nut-mo-toi" onClick={huyQuet}>
              Huỷ quét
            </button>
          )}
          <button type="button" className="btn kh-nut-mo-toi" onClick={dongHop}>
            <i className="bi bi-key" />
            Dùng mật khẩu thay thế
          </button>
        </div>

        {loiCamera && <div className="alert alert-warning small mt-3 mb-0 py-2">{loiCamera}</div>}
      </div>
    </div>
  )
}
