import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { NhanTrangThai } from '../../components/ui'
import { dinhDangNgayGio, gioNgayNgan } from '../../utils/dinhDang'
import { DauTrang } from './chung'

/**
 * Cong tai xe cong khai (thay Views/TaiXe/Index.cshtml) — dia chi /taixe.
 * Mo phong app ePort Driver: tai xe nhap SO LENH + MA PIN in tren lenh de xem chuyen
 * cua minh va CHECK-IN ONLINE truoc khi den cang.
 *
 * Ma QR in tren lenh giao nhan tro ve /taixe?soLenh=... nen o so lenh duoc dien san,
 * tai xe chi con go ma PIN (PIN khong bao gio nam trong ma QR).
 */
export default function CongTaiXe() {
  const [thamSo] = useSearchParams()
  const [soLenh, setSoLenh] = useState(thamSo.get('soLenh') || '')
  const [maPin, setMaPin] = useState('')
  const [lenh, setLenh] = useState(null)
  const [loi, setLoi] = useState('')
  const [thongBao, setThongBao] = useState('')
  const [dangChay, setDangChay] = useState(null) // 'xem' | 'checkin'
  const [hangCho, setHangCho] = useState(null) // luot trong hang cho ao

  // Lam moi bang ma theo doi da cap luc vao hang: khong gui lai PIN moi lan
  const maTheoDoi = hangCho?.maTheoDoi
  const conCho = hangCho?.trangThai === 'Đang chờ' || hangCho?.trangThai === 'Đã gọi'
  useEffect(() => {
    if (!maTheoDoi || !conCho) return undefined
    const t = setInterval(() => {
      goiApi('/api/lich-hen/hang-cho/tai-xe/' + maTheoDoi, { coToken: false }).then(setHangCho).catch(() => {})
    }, 10000)
    return () => clearInterval(t)
  }, [maTheoDoi, conCho])

  async function vaoHang() {
    setDangChay('hang')
    setLoi('')
    setThongBao('')
    try {
      setHangCho(await goiApi('/api/lich-hen/hang-cho/tai-xe/vao', { method: 'POST', coToken: false, body: { soLenh: soLenh.trim().toUpperCase(), maPin: maPin.trim() } }))
    } catch (e) {
      setLoi(e.message)
    } finally {
      setDangChay(null)
    }
  }

  async function roiHang() {
    try {
      await goiApi('/api/lich-hen/hang-cho/tai-xe/' + maTheoDoi + '/roi', { method: 'POST', coToken: false })
      setHangCho(null)
    } catch (e) {
      setLoi(e.message)
    }
  }

  async function gui(hanhDong) {
    setDangChay(hanhDong)
    setLoi('')
    setThongBao('')
    const than = { soLenh: soLenh.trim().toUpperCase(), maPin: maPin.trim() }
    try {
      if (hanhDong === 'checkin') {
        const kq = await goiApi('/api/tien-ich/tai-xe/check-in', { method: 'POST', body: than, coToken: false })
        setLenh(kq.lenh)
        setThongBao(kq.thongBao)
      } else {
        setLenh(await goiApi('/api/tien-ich/tai-xe/tra-lenh', { method: 'POST', body: than, coToken: false }))
      }
    } catch (e) {
      // API tra CUNG mot thong bao cho "khong co lenh" va "sai PIN" -> khong lo lenh nao co that
      setLoi(e.message)
      setLenh(null)
    } finally {
      setDangChay(null)
    }
  }

  const khoa = !!dangChay || soLenh.trim().length < 5 || maPin.trim().length < 4

  return (
    <>
      <DauTrang icon="bi-truck" tieuDe="Cổng tài xế" mucHienTai="Cổng tài xế" phu="Nhập số lệnh + mã PIN in trên Lệnh giao nhận để xem chuyến và check-in online." />
      <div className="container pb-5" style={{ maxWidth: 720 }}>
        {loi && (
          <div className="alert alert-danger d-flex align-items-center gap-2">
            <i className="bi bi-exclamation-octagon" />
            <div>{loi}</div>
          </div>
        )}
        {thongBao && (
          <div className="alert alert-success d-flex align-items-center gap-2">
            <i className="bi bi-check-circle-fill" />
            <div>{thongBao}</div>
          </div>
        )}

        <form
          className="card sp-the mb-3"
          onSubmit={(e) => {
            e.preventDefault()
            gui('xem')
          }}
        >
          <div className="card-body">
            <div className="row g-3">
              <div className="col-md-7">
                <label className="form-label small fw-semibold mb-1" htmlFor="soLenh">
                  SỐ LỆNH
                </label>
                <input
                  id="soLenh"
                  className="form-control form-control-lg ti-o-ma"
                  value={soLenh}
                  onChange={(e) => setSoLenh(e.target.value.toUpperCase())}
                  placeholder="EG-2608-00001"
                  maxLength={20}
                  autoComplete="off"
                  required
                />
              </div>
              <div className="col-md-5">
                <label className="form-label small fw-semibold mb-1" htmlFor="maPin">
                  MÃ PIN (6 số)
                </label>
                <input
                  id="maPin"
                  type="password"
                  inputMode="numeric"
                  className="form-control form-control-lg"
                  value={maPin}
                  onChange={(e) => setMaPin(e.target.value)}
                  placeholder="••••••"
                  maxLength={8}
                  autoComplete="off"
                  required
                />
              </div>
            </div>
            <div className="d-flex flex-wrap gap-2 mt-3">
              <button className="btn btn-primary btn-lg flex-grow-1" type="submit" disabled={khoa}>
                {dangChay === 'xem' ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-search me-1" />}
                Xem lệnh
              </button>
              <button className="btn btn-warning btn-lg flex-grow-1 fw-semibold" type="button" onClick={() => gui('checkin')} disabled={khoa}>
                {dangChay === 'checkin' ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-geo-alt-fill me-1" />}
                CHECK-IN ngay
              </button>
              <button className="btn btn-outline-primary btn-lg flex-grow-1 fw-semibold" type="button" onClick={vaoHang} disabled={khoa}>
                {dangChay === 'hang' ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-people-fill me-1" />}
                Vào hàng chờ ảo
              </button>
            </div>
            <div className="form-text mt-2">Mã PIN 6 số nằm trên bản in lệnh — không chia sẻ cho người ngoài.</div>
          </div>
        </form>

        {hangCho && (
          <div className={'card sp-the mb-3 ' + (hangCho.trangThai === 'Đã gọi' ? 'border-success border-2' : '')}>
            <div className="card-body text-center">
              <div className="small text-body-secondary">
                Xe <b>{hangCho.bienSo}</b> · {hangCho.trangThai}
              </div>
              {hangCho.trangThai === 'Đang chờ' && (
                <>
                  <div className="display-3 fw-bold">{hangCho.viTri}</div>
                  <div>
                    Còn {hangCho.soXeTruoc} xe trước bạn · dự kiến khoảng <b>{hangCho.phutUocTinh} phút</b>
                  </div>
                  <div className="small text-body-secondary mt-1">
                    Cảng cam kết gọi trong {hangCho.camKetPhut} phút. Bạn có thể đợi ở bãi đỗ, không cần đậu xe trên đường trước cổng.
                  </div>
                </>
              )}
              {hangCho.trangThai === 'Đã gọi' && (
                <div className="h3 text-success my-2">
                  <i className="bi bi-megaphone-fill me-2" />
                  Mời xe vào cổng trong {hangCho.phutGiuCho} phút
                </div>
              )}
              {hangCho.tienBoiHoan > 0 && <div className="small text-success mt-1">Cảng gọi trễ hơn cam kết: bồi hoàn {Number(hangCho.tienBoiHoan).toLocaleString('vi-VN')} đ</div>}
              {conCho && (
                <button type="button" className="btn btn-sm btn-outline-secondary mt-2" onClick={roiHang}>
                  Rời hàng chờ
                </button>
              )}
            </div>
          </div>
        )}

        {lenh && (
          <div className="card sp-the">
            <div className="card-header d-flex align-items-center gap-2">
              <span className="font-monospace fw-bold flex-grow-1">{lenh.soLenh}</span>
              <NhanTrangThai giaTri={lenh.trangThai} />
            </div>
            <div className="card-body">
              <table className="table table-sm mb-2">
                <tbody>
                  <tr>
                    <td className="text-body-secondary" style={{ width: 150 }}>
                      Loại lệnh
                    </td>
                    <td>{lenh.tenLoaiLenh}</td>
                  </tr>
                  <tr>
                    <td className="text-body-secondary">Biển số xe</td>
                    <td className="fw-bold">{lenh.bienSoXe}</td>
                  </tr>
                  <tr>
                    <td className="text-body-secondary">Container</td>
                    <td className="font-monospace">{lenh.soCont || '—'}</td>
                  </tr>
                  <tr>
                    <td className="text-body-secondary">Khung giờ hẹn</td>
                    <td className="fw-semibold">
                      {gioNgayNgan(lenh.henTuLuc)} – {dinhDangNgayGio(lenh.henDenLuc)}
                    </td>
                  </tr>
                  <tr>
                    <td className="text-body-secondary">Check-in</td>
                    <td>
                      {lenh.daCheckIn ? (
                        <span className="badge text-bg-info">
                          <i className="bi bi-check2 me-1" />
                          Đã check-in {gioNgayNgan(lenh.checkInLuc)}
                        </span>
                      ) : (
                        <span className="text-body-secondary">Chưa — bấm "CHECK-IN ngay" trước khi khởi hành</span>
                      )}
                    </td>
                  </tr>
                </tbody>
              </table>
              <div className="small text-body-secondary">
                <i className="bi bi-camera me-1" />
                Khi xe đến cổng, camera đọc biển số và tự đối chiếu với lệnh này — check-in trước giúp nhân viên cổng thấy nhãn <b>"đã check-in online"</b> và xử lý ưu tiên.
              </div>
            </div>
          </div>
        )}

        <div className="text-center mt-3">
          <Link className="small" to="/cong">
            <i className="bi bi-stoplights me-1" />
            Xem tình trạng cổng trước khi chạy
          </Link>
        </div>
      </div>
    </>
  )
}
