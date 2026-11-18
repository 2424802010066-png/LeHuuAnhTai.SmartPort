import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useWebcam } from './useWebcam'
import './khach-hang.css'

const DUONG = '/api/tai-khoan-ca-nhan/khuon-mat'

// Dung THU TU nay: API gan ten goc theo vi tri trong mang anh
const GOC = [
  { ten: 'Thẳng', huong: 'Nhìn THẲNG vào camera' },
  { ten: 'Trái', huong: 'Quay mặt nhẹ sang TRÁI' },
  { ten: 'Phải', huong: 'Quay mặt nhẹ sang PHẢI' },
  { ten: 'Ngẩng', huong: 'NGẨNG mặt lên một chút' },
  { ten: 'Cúi', huong: 'CÚI mặt xuống một chút' },
]
const TOI_THIEU = 3

/** Cai dat dang nhap bang khuon mat (thay Account/FaceSetup + FaceCheckShot/FaceEnroll/FaceRemove ban MVC). */
export default function CaiDatKhuonMat() {
  const { duLieu, loi, dangTai, taiLai } = useApi(DUONG)
  const { videoRef, batDau, dungLai, chup, loiCamera } = useWebcam()
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  const [anh, setAnh] = useState([null, null, null, null, null])
  const [dangCham, setDangCham] = useState(false)
  const [nhanXet, setNhanXet] = useState(null) // { dat, thongBao, chatLuong }
  const [nhay, setNhay] = useState(false)
  const nhayRef = useRef(null)

  const sanSang = !!duLieu?.sanSang

  useEffect(() => {
    if (sanSang) batDau()
  }, [sanSang, batDau])

  useEffect(() => () => clearTimeout(nhayRef.current), [])

  const daChup = anh.filter(Boolean).length
  const viTriTiep = anh.indexOf(null)

  async function chupGoc() {
    // Dang ky xong camera da duoc tat cho kin dao -> bat lai khi nguoi dung muon chup tiep
    if (!(await batDau())) {
      setNhanXet({ dat: false, thongBao: 'Chưa mở được camera — hãy cấp quyền camera cho trình duyệt.' })
      return
    }
    const hinh = chup()
    if (!hinh) {
      setNhanXet({ dat: false, thongBao: 'Chưa lấy được hình từ camera — thử lại sau một giây.' })
      return
    }
    setNhay(true)
    nhayRef.current = setTimeout(() => setNhay(false), 220)

    setDangCham(true)
    setNhanXet(null)
    try {
      // Cham chat luong NGAY luc chup — anh xau bi tra lai kem goi y sua,
      // khong de den luc dang nhap moi phat hien mau te (giong ban MVC)
      const kq = await goiApi(DUONG + '/kiem-tra-anh', { method: 'POST', body: { anh: hinh } })
      setNhanXet(kq)
      if (kq.dat) {
        setAnh((cu) => {
          const moi = [...cu]
          const o = moi.indexOf(null)
          if (o >= 0) moi[o] = hinh
          return moi
        })
      }
    } catch (e) {
      setNhanXet({ dat: false, thongBao: e.message })
    } finally {
      setDangCham(false)
    }
  }

  function boAnh(i) {
    setAnh((cu) => {
      // Bo mot o roi don cac o sau len: thu tu goc luon lien tuc tu dau
      const con = cu.filter((a, vt) => a && vt !== i)
      return [0, 1, 2, 3, 4].map((vt) => con[vt] ?? null)
    })
  }

  async function guiDangKy() {
    const kq = await chay(() => goiApi(DUONG + '/dang-ky', { method: 'POST', body: { anh: anh.map((a) => a || '') } }))
    if (kq) {
      setAnh([null, null, null, null, null])
      setNhanXet(null)
      dungLai()
      taiLai({ im: true })
    }
  }

  async function xoaDangKy() {
    const dongY = await xacNhan({
      tieuDe: 'Xóa đăng ký khuôn mặt?',
      noiDung: 'Sau khi xóa, bạn chỉ đăng nhập được bằng mật khẩu cho tới khi đăng ký lại.',
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi(DUONG, { method: 'DELETE' }))) taiLai({ im: true })
  }

  if (dangTai && !duLieu) return <DangTai chu="Đang kiểm tra dịch vụ AI khuôn mặt…" />
  if (loi) return <BaoLoi loi={loi} thuLai={taiLai} />
  if (!duLieu) return null

  return (
    <>
      <div className="kh-dau-trang">
        <div className="container">
          <h1>
            <i className="bi bi-person-bounding-box me-2" />
            Cài đặt đăng nhập khuôn mặt
          </h1>
          <div className="small text-white-50">
            AI ArcFace 512D · chấm chất lượng từng ảnh · ngưỡng khớp ≥ {duLieu.nguongTinCay}% · {duLieu.soKhungCan} khung liên tiếp
          </div>
        </div>
      </div>

      <div className="container pb-5">
        <div className="row g-3">
          <div className="col-lg-7">
            <TheNoiDung tieuDe="Chụp các góc mặt" icon="bi-camera">
              {!sanSang && (
                <div className="alert alert-warning small">
                  <i className="bi bi-exclamation-triangle me-1" />
                  <b>Dịch vụ AI khuôn mặt chưa chạy.</b> Hãy khởi động dịch vụ (<code>4-chay-ai-cho-smartport.bat</code> trong thư mục FaceAuth) rồi
                  bấm Tải lại.
                  <button type="button" className="btn btn-sm btn-outline-secondary ms-2" onClick={() => taiLai()}>
                    Tải lại
                  </button>
                </div>
              )}
              {loiCamera && <div className="alert alert-warning small">{loiCamera}</div>}

              <div className="kh-vong kh-vong-nho kh-cho">
                <div className="kh-long">
                  <video ref={videoRef} playsInline muted />
                  {nhay && <div className="kh-nhay" />}
                </div>
              </div>

              <p className="text-center mt-3 mb-1">
                {viTriTiep === -1 ? (
                  <span className="fw-semibold text-success">Đã đủ 5 góc — bấm “Hoàn tất đăng ký”.</span>
                ) : (
                  <>
                    Góc {viTriTiep + 1}/5: <b>{GOC[viTriTiep].huong}</b>
                  </>
                )}
              </p>
              <div className="text-center small" style={{ minHeight: 24 }}>
                {dangCham && (
                  <span className="text-body-secondary">
                    <span className="spinner-border spinner-border-sm me-2" />
                    AI đang chấm chất lượng ảnh…
                  </span>
                )}
                {!dangCham && nhanXet && (
                  <span className={nhanXet.dat ? 'text-success fw-semibold' : 'text-danger fw-semibold'}>
                    <i className={'bi me-1 ' + (nhanXet.dat ? 'bi-check-circle' : 'bi-x-circle')} />
                    {nhanXet.thongBao}
                    {nhanXet.chatLuong != null && ` (chất lượng ${Math.round(nhanXet.chatLuong)}/100)`}
                  </span>
                )}
              </div>

              <div className="kh-o-anh">
                {GOC.map((g, i) => (
                  <div key={g.ten} className={'kh-anh' + (anh[i] ? ' kh-co' : '')}>
                    {anh[i] ? (
                      <>
                        <img src={anh[i]} alt={'Ảnh góc ' + g.ten} />
                        <button type="button" className="kh-xoa-anh" onClick={() => boAnh(i)} title={'Bỏ ảnh góc ' + g.ten} aria-label={'Bỏ ảnh góc ' + g.ten}>
                          <i className="bi bi-x" />
                        </button>
                      </>
                    ) : (
                      g.ten
                    )}
                  </div>
                ))}
              </div>

              <div className="d-flex flex-wrap gap-2 justify-content-center mt-3">
                {viTriTiep !== -1 && (
                  <button type="button" className="btn btn-primary" onClick={chupGoc} disabled={!sanSang || dangCham}>
                    <i className="bi bi-camera me-1" />
                    Chụp góc {GOC[viTriTiep].ten}
                  </button>
                )}
                <button type="button" className="btn btn-success" onClick={guiDangKy} disabled={daChup < TOI_THIEU || dangChay}>
                  <i className="bi bi-check2-circle me-1" />
                  Hoàn tất đăng ký ({daChup} ảnh)
                </button>
                <Link className="btn btn-outline-secondary" to="/account/profile">
                  <i className="bi bi-arrow-left me-1" />
                  Về hồ sơ
                </Link>
              </div>
            </TheNoiDung>
          </div>

          <div className="col-lg-5">
            <TheNoiDung tieuDe="Tình trạng hiện tại" icon="bi-shield-check">
              {duLieu.soGoc > 0 ? (
                <>
                  <div className="fs-4 fw-bold text-success">Đang bật · {duLieu.soGoc} góc mặt</div>
                  {duLieu.chatLuongTB != null && (
                    <div className="text-body-secondary small mb-3">Chất lượng trung bình {duLieu.chatLuongTB}/100</div>
                  )}
                  <ul className="list-group list-group-flush mb-3">
                    {duLieu.cacGoc.map((g, i) => (
                      <li key={i} className="list-group-item d-flex justify-content-between px-0 bg-transparent">
                        <span>{g.gocChup || '—'}</span>
                        <span className="text-body-secondary small">{g.chatLuong != null ? g.chatLuong + '/100' : '—'}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="small text-body-secondary">Chụp lại ở bên trái sẽ thay thế toàn bộ dữ liệu cũ.</p>
                  <button type="button" className="btn btn-outline-danger btn-sm" onClick={xoaDangKy} disabled={dangChay}>
                    <i className="bi bi-trash me-1" />
                    Xóa đăng ký khuôn mặt
                  </button>
                </>
              ) : (
                <>
                  <div className="fs-4 fw-bold text-body-secondary">Chưa bật</div>
                  <p className="small text-body-secondary mb-0">
                    Chụp tối thiểu {TOI_THIEU} góc (thẳng / trái / phải) để bật đăng nhập bằng khuôn mặt.
                  </p>
                </>
              )}
            </TheNoiDung>

            <TheNoiDung tieuDe="Cách hoạt động" icon="bi-cpu">
              <ul className="small mb-0 ps-3">
                <li>Ảnh được mạng ArcFace chuyển thành vector 512 số — hệ thống KHÔNG lưu ảnh gốc.</li>
                <li>Mỗi lần đăng nhập, AI chấm chất lượng ảnh và kiểm tra liveness để chặn ảnh in / màn hình.</li>
                <li>Phải khớp ≥ {duLieu.nguongTinCay}% và đủ {duLieu.soKhungCan} khung liên tiếp mới cho vào.</li>
              </ul>
            </TheNoiDung>
          </div>
        </div>
      </div>
    </>
  )
}
