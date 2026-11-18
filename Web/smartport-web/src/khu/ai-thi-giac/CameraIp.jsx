import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taiBlob } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { gioNgayNgan } from '../../utils/dinhDang'
import './ai-thi-giac.css'

const HANG = ['Hikvision', 'Dahua', 'Reolink', 'Khac']
const GOI_Y = {
  Hikvision: 'Hikvision → /ISAPI/Streaming/channels/{kênh}01/picture (hệ thống tự ghép)',
  Dahua: 'Dahua → /cgi-bin/snapshot.cgi?channel={kênh} (hệ thống tự ghép)',
  Reolink: 'Reolink → /cgi-bin/api.cgi?cmd=Snap&channel={kênh−1} (hệ thống tự ghép)',
  Khac: 'Hãng khác → bắt buộc nhập “Đường dẫn ảnh riêng”, ví dụ /snapshot.jpg',
}
const RONG = { tenCam: '', viTri: '', hang: 'Hikvision', diaChi: '', tenDangNhap: '', matKhau: '', kenh: 1, duongDanRieng: '', kichHoat: true }

/** Camera IP tai hien truong (thay Admin/Camera) — dung camera Hikvision/Dahua cang da lap. */
export default function CameraIp() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/camera')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const [dangSua, setDangSua] = useState(0) // 0 = them moi, >0 = ma camera dang sua
  const [anh, setAnh] = useState({}) // { [maCam]: { url, gio, loi, dangTai } }
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const anhRef = useRef({})
  anhRef.current = anh

  // Roi man hinh: thu hoi moi URL tam cua anh camera (khong de ro ri bo nho)
  useEffect(
    () => () => {
      for (const a of Object.values(anhRef.current)) if (a?.url) URL.revokeObjectURL(a.url)
    },
    [],
  )

  function moForm(c) {
    setDangSua(c ? c.maCam : 0)
    datLai(
      c
        ? {
            tenCam: c.tenCam || '',
            viTri: c.viTri || '',
            hang: c.hang || 'Hikvision',
            diaChi: c.diaChi || '',
            tenDangNhap: c.tenDangNhap || '',
            matKhau: '',
            kenh: c.kenh ?? 1,
            duongDanRieng: c.duongDanRieng || '',
            kichHoat: c.kichHoat,
          }
        : RONG,
    )
  }

  async function luu(e) {
    e.preventDefault()
    const than = { ...gt, kenh: gt.kenh === '' ? null : Number(gt.kenh) }
    const kq = await chay(() => (dangSua ? goiApi('/api/camera/' + dangSua, { method: 'PUT', body: than }) : goiApi('/api/camera', { method: 'POST', body: than })))
    if (kq) {
      moForm(null)
      taiLai({ im: true })
    }
  }

  async function xoa(c) {
    const dongY = await xacNhan({ tieuDe: 'Xóa camera?', noiDung: `Xóa camera ${c.tenCam}. Thao tác này không hoàn tác được.`, nutDongY: 'Xóa', mau: 'danger' })
    if (!dongY) return
    if (await chay(() => goiApi('/api/camera/' + c.maCam, { method: 'DELETE' }))) {
      boAnh(c.maCam)
      if (dangSua === c.maCam) moForm(null)
      taiLai({ im: true })
    }
  }

  async function thuKetNoi(c) {
    await chay(() => goiApi('/api/camera/' + c.maCam + '/thu', { method: 'POST' }))
    // Thanh cong hay loi thi API deu da luu ket qua thu vao CSDL -> lam moi de thay ly do
    taiLai({ im: true })
  }

  function boAnh(maCam) {
    setAnh((cu) => {
      if (cu[maCam]?.url) URL.revokeObjectURL(cu[maCam].url)
      const moi = { ...cu }
      delete moi[maCam]
      return moi
    })
  }

  /** Anh camera CAN DANG NHAP: the <img src> khong gui duoc token -> tai blob roi tao URL tam */
  async function xemAnh(c) {
    setAnh((cu) => ({ ...cu, [c.maCam]: { ...cu[c.maCam], dangTai: true, loi: '' } }))
    try {
      const blob = await taiBlob('/api/camera/' + c.maCam + '/anh')
      const url = URL.createObjectURL(blob)
      setAnh((cu) => {
        if (cu[c.maCam]?.url) URL.revokeObjectURL(cu[c.maCam].url)
        return { ...cu, [c.maCam]: { url, gio: new Date().toLocaleTimeString('vi-VN'), dangTai: false, loi: '' } }
      })
    } catch (e) {
      setAnh((cu) => ({ ...cu, [c.maCam]: { ...cu[c.maCam], dangTai: false, loi: e.message } }))
    }
  }

  const ds = duLieu || []

  return (
    <>
      <div className="sp-hop-ai mb-3">
        <span className="sp-nhan-ai">
          <i className="bi bi-camera-video-fill me-1" />
          CAMERA IP HIỆN TRƯỜNG
        </span>
        <div className="h5 mb-0 fw-bold mt-2">Dùng luôn camera cảng đã lắp — không mua thiết bị mới</div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3">
        <div className="col-lg-5">
          <div className="card sp-the mb-3">
            <div className="card-header d-flex align-items-center gap-2">
              <span className="flex-grow-1">
                <i className={'bi me-2 ' + (dangSua ? 'bi-pencil-square' : 'bi-plus-square')} />
                {dangSua ? 'Sửa camera #' + dangSua : 'Thêm camera'}
              </span>
              {dangSua > 0 && (
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => moForm(null)}>
                  Hủy sửa
                </button>
              )}
            </div>
            <form className="card-body" onSubmit={luu}>
              <Truong nhan="Tên camera" batBuoc lop="mb-2">
                <input className="form-control" value={gt.tenCam} onChange={doi('tenCam')} maxLength={60} placeholder="Camera cổng vào" />
              </Truong>
              <Truong nhan="Vị trí" lop="mb-2">
                <input className="form-control" value={gt.viTri} onChange={doi('viTri')} maxLength={60} placeholder="Cổng A1" />
              </Truong>
              <Truong nhan="Hãng" lop="mb-2" goiY={GOI_Y[gt.hang]}>
                <select className="form-select" value={gt.hang} onChange={doi('hang')}>
                  {HANG.map((h) => (
                    <option key={h} value={h}>
                      {h === 'Khac' ? 'Hãng khác (tự nhập đường dẫn)' : h}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Địa chỉ IP (kèm cổng nếu khác 80)" batBuoc lop="mb-2">
                <input className="form-control" value={gt.diaChi} onChange={doi('diaChi')} maxLength={120} placeholder="192.168.1.64" />
              </Truong>
              <div className="row g-2">
                <Truong nhan="Tên đăng nhập" lop="col-7 mb-2">
                  <input className="form-control" value={gt.tenDangNhap} onChange={doi('tenDangNhap')} maxLength={60} autoComplete="off" placeholder="admin" />
                </Truong>
                <Truong nhan="Kênh" lop="col-5 mb-2">
                  <input className="form-control" type="number" min={1} max={999} value={gt.kenh} onChange={doi('kenh')} />
                </Truong>
              </div>
              <Truong nhan="Mật khẩu" lop="mb-2" goiY={dangSua ? 'Để trống = giữ mật khẩu cũ.' : undefined}>
                <input className="form-control" type="password" value={gt.matKhau} onChange={doi('matKhau')} maxLength={120} autoComplete="new-password" />
              </Truong>
              <Truong nhan="Đường dẫn ảnh riêng (chỉ khi chọn “Hãng khác”)" lop="mb-2">
                <input className="form-control" value={gt.duongDanRieng} onChange={doi('duongDanRieng')} maxLength={200} placeholder="/snapshot.jpg" />
              </Truong>
              {dangSua > 0 && (
                <div className="form-check form-switch mb-3">
                  <input className="form-check-input" type="checkbox" id="tg-cam-bat" checked={gt.kichHoat} onChange={doi('kichHoat')} />
                  <label className="form-check-label small fw-semibold" htmlFor="tg-cam-bat">
                    Camera đang bật
                  </label>
                </div>
              )}
              <button type="submit" className="btn btn-primary w-100" disabled={dangChay}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
                Lưu camera
              </button>
            </form>
          </div>

          <TheNoiDung tieuDe="Cách lấy thông tin từ đầu ghi" icon="bi-info-circle">
            <ol className="small text-body-secondary mb-0" style={{ paddingLeft: 18, lineHeight: 1.8 }}>
              <li>
                Hỏi bên lắp camera <b>IP của đầu ghi/camera</b> và tài khoản xem.
              </li>
              <li>Mở trình duyệt gõ IP đó — vào được trang đăng nhập camera là mạng thông.</li>
              <li>
                Nhập vào khung bên trên rồi bấm <b>Thử kết nối</b>.
              </li>
              <li>Báo lỗi 401 là sai mật khẩu; quá 8 giây là khác mạng LAN.</li>
            </ol>
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung tieuDe={`Camera đã cấu hình (${ds.length})`} icon="bi-list-ul">
            {dangTai && !ds.length && (
              <div className="text-center text-body-secondary py-4">
                <span className="spinner-border spinner-border-sm me-2" />
                Đang tải…
              </div>
            )}
            {!dangTai && !ds.length && (
              <div className="text-center text-body-secondary py-4">
                Chưa có camera nào. Thêm camera đầu tiên ở khung bên trái.
                <div className="small">Chưa có camera IP thì hệ thống vẫn chạy bằng webcam như cũ.</div>
              </div>
            )}

            {ds.map((c) => {
              const a = anh[c.maCam]
              return (
                <div key={c.maCam} className="tg-cam">
                  <div className="d-flex justify-content-between align-items-start flex-wrap gap-2">
                    <div className="min-w-0">
                      <span className="fw-bold">{c.tenCam}</span>
                      <span className="badge text-bg-light border ms-1">{c.hang}</span>
                      {!c.kichHoat && <span className="badge text-bg-secondary ms-1">Tắt</span>}
                      {!c.coMatKhau && c.tenDangNhap && <span className="badge text-bg-warning ms-1">Chưa có mật khẩu</span>}
                      <div className="small text-body-secondary">
                        {c.viTri || '—'} · kênh {c.kenh ?? 1}
                      </div>
                      <div className="tg-cam-url">{c.urlHienThi}</div>
                      {c.ketQuaThu && (
                        <div className={'small mt-1 fw-semibold ' + (c.thuOk ? 'text-success' : 'text-danger')}>
                          {c.ketQuaThu}
                          {c.lanThuCuoi && <span className="text-body-secondary fw-normal"> · {gioNgayNgan(c.lanThuCuoi)}</span>}
                        </div>
                      )}
                    </div>
                    <div className="d-flex gap-1">
                      <button type="button" className="btn btn-sm btn-primary" disabled={dangChay} onClick={() => thuKetNoi(c)}>
                        <i className="bi bi-plug me-1" />
                        Thử kết nối
                      </button>
                      <button type="button" className="btn btn-sm btn-outline-secondary" title="Xem ảnh hiện tại" aria-label={'Xem ảnh ' + c.tenCam} disabled={a?.dangTai} onClick={() => xemAnh(c)}>
                        <i className="bi bi-eye" />
                      </button>
                      <button type="button" className="btn btn-sm btn-outline-secondary" title="Sửa camera" aria-label={'Sửa ' + c.tenCam} onClick={() => moForm(c)}>
                        <i className="bi bi-pencil" />
                      </button>
                      <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa camera" aria-label={'Xóa ' + c.tenCam} disabled={dangChay} onClick={() => xoa(c)}>
                        <i className="bi bi-trash" />
                      </button>
                    </div>
                  </div>

                  {a && (
                    <div className="mt-2">
                      {a.dangTai && (
                        <div className="small text-body-secondary">
                          <span className="spinner-border spinner-border-sm me-2" />
                          Đang lấy ảnh từ camera…
                        </div>
                      )}
                      {a.loi && <div className="alert alert-warning py-2 small mb-0">{a.loi}</div>}
                      {a.url && (
                        <>
                          <img className="tg-cam-anh" src={a.url} alt={'Ảnh từ ' + c.tenCam} />
                          <div className="small text-body-secondary mt-1 d-flex gap-2">
                            <span className="flex-grow-1">Ảnh chụp lúc {a.gio} — bấm biểu tượng mắt để chụp lại.</span>
                            <button type="button" className="btn btn-sm btn-link p-0" onClick={() => boAnh(c.maCam)}>
                              Đóng ảnh
                            </button>
                          </div>
                        </>
                      )}
                    </div>
                  )}
                </div>
              )
            })}

            <div className="small text-body-secondary">
              <i className="bi bi-lightbulb me-1" />
              Camera đang bật xuất hiện trong ô chọn nguồn ở màn hình{' '}
              <Link to="/admin/vision">Camera AI</Link> và <Link to="/admin/vision/pose">SafePose AI</Link> — AI phân tích thẳng ảnh camera hiện trường.
            </div>
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
