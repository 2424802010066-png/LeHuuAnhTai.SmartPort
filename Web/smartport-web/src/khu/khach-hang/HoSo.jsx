import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { useAuth } from '../../auth/AuthContext'
import { BaoLoi, DangTai, TheNoiDung, Truong } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay } from '../../utils/dinhDang'
import DoManhMatKhau from './DoManhMatKhau'
import LienKetDangNhap from './LienKetDangNhap'
import './khach-hang.css'

const MK_RONG = { matKhauCu: '', matKhauMoi: '', xacNhan: '' }

const TEN_VAI_TRO = { Admin: 'Quản trị viên', NhanVien: 'Nhân viên khai thác', KhachHang: 'Khách hàng' }

/** Ho so ca nhan: thong tin tai khoan, doi mat khau, doanh nghiep lien ket (thay Account/Profile ban MVC). */
export default function HoSo() {
  const { phien, nhanPhien } = useAuth()
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/tai-khoan-ca-nhan/ho-so')
  const { gt, doi, datLai } = useBieuMau({ hoTen: '', email: '', soDienThoai: '' })
  const { gt: mk, doi: doiMk, datLai: datLaiMk } = useBieuMau(MK_RONG)
  const { chay, dangChay } = useThaoTac()
  const [luuMk, setLuuMk] = useState(false)

  useEffect(() => {
    if (duLieu) datLai({ hoTen: duLieu.hoTen || '', email: duLieu.email || '', soDienThoai: duLieu.soDienThoai || '' })
  }, [duLieu, datLai])

  async function luuHoSo(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/tai-khoan-ca-nhan/ho-so', { method: 'PUT', body: gt }))
    if (kq) {
      // Ten hien tren thanh tren doc tu phien -> cap nhat lai, token giu nguyen
      if (kq.duLieu && phien) nhanPhien({ ...phien, taiKhoan: kq.duLieu })
      taiLai({ im: true })
    }
  }

  async function doiMatKhau(e) {
    e.preventDefault()
    setLuuMk(true)
    const kq = await chay(() => goiApi('/api/tai-khoan-ca-nhan/doi-mat-khau', { method: 'POST', body: mk }))
    setLuuMk(false)
    if (kq) {
      // duLieu la PHIEN MOI (token moi + co matKhauMacDinh tinh lai) -> thay phien dang dung
      if (kq.duLieu?.token) nhanPhien(kq.duLieu)
      datLaiMk(MK_RONG)
    }
  }

  if (dangTai && !duLieu) return <DangTai chu="Đang tải hồ sơ…" />
  if (loi) return <BaoLoi loi={loi} thuLai={taiLai} />
  if (!duLieu) return null

  const dn = duLieu.doanhNghiep

  return (
    <>
      <div className="kh-dau-trang">
        <div className="container">
          <h1>
            <i className="bi bi-person-gear me-2" />
            Hồ sơ cá nhân
          </h1>
          <div className="small text-white-50">
            {duLieu.tenDangNhap} · {TEN_VAI_TRO[duLieu.vaiTro] || duLieu.vaiTro} · tạo ngày {dinhDangNgay(duLieu.ngayTao)}
          </div>
        </div>
      </div>

      <div className="container pb-5">
        {phien?.matKhauMacDinh && (
          <div className="alert alert-warning d-flex align-items-center gap-2 py-2">
            <i className="bi bi-shield-exclamation fs-5" />
            <div className="small">
              <b>Bạn đang dùng mật khẩu mặc định.</b> Hãy đổi sang mật khẩu riêng ở khung “Đổi mật khẩu” bên dưới.
            </div>
          </div>
        )}

        <div className="row g-3">
          <div className="col-lg-7">
            <TheNoiDung tieuDe="Thông tin tài khoản" icon="bi-person-vcard">
              <form onSubmit={luuHoSo}>
                <div className="row g-2">
                  <Truong nhan="Tên đăng nhập" lop="col-md-6 mb-2">
                    <input className="form-control" value={duLieu.tenDangNhap} disabled />
                  </Truong>
                  <Truong nhan="Vai trò" lop="col-md-6 mb-2">
                    <input className="form-control" value={TEN_VAI_TRO[duLieu.vaiTro] || duLieu.vaiTro} disabled />
                  </Truong>
                  <Truong nhan="Họ tên" batBuoc lop="col-12 mb-2">
                    <input className="form-control" value={gt.hoTen} onChange={doi('hoTen')} maxLength={100} autoComplete="name" />
                  </Truong>
                  <Truong nhan="Email" lop="col-md-6 mb-2">
                    <input type="email" className="form-control" value={gt.email} onChange={doi('email')} maxLength={100} autoComplete="email" />
                  </Truong>
                  <Truong nhan="Số điện thoại" lop="col-md-6 mb-2">
                    <input className="form-control" value={gt.soDienThoai} onChange={doi('soDienThoai')} maxLength={20} autoComplete="tel" />
                  </Truong>
                </div>
                <button className="btn btn-primary" disabled={dangChay && !luuMk}>
                  <i className="bi bi-save me-1" />
                  Lưu thay đổi
                </button>
              </form>
            </TheNoiDung>

            <LienKetDangNhap />

            <TheNoiDung tieuDe="Đăng nhập bằng khuôn mặt" icon="bi-person-bounding-box">
              <div className="d-flex flex-wrap align-items-center gap-3">
                <div className="flex-grow-1">
                  {duLieu.soGocKhuonMat > 0 ? (
                    <div className="fs-5 fw-bold text-success">
                      Đang bật · {duLieu.soGocKhuonMat} góc mặt
                    </div>
                  ) : (
                    <div className="fs-5 fw-bold text-body-secondary">Chưa bật</div>
                  )}
                  <div className="small text-body-secondary">Đăng nhập không cần gõ mật khẩu, AI ArcFace kiểm tra ảnh thật.</div>
                </div>
                <Link className="btn btn-outline-primary" to="/account/facesetup">
                  <i className="bi bi-camera me-1" />
                  {duLieu.soGocKhuonMat > 0 ? 'Cài đặt lại' : 'Cài đặt khuôn mặt'}
                </Link>
              </div>
            </TheNoiDung>

            <TheNoiDung tieuDe="Đổi mật khẩu" icon="bi-shield-lock">
              <form onSubmit={doiMatKhau}>
                <div className="row g-2">
                  <Truong nhan="Mật khẩu hiện tại" batBuoc lop="col-md-4 mb-2">
                    <input type="password" className="form-control" value={mk.matKhauCu} onChange={doiMk('matKhauCu')} autoComplete="current-password" />
                  </Truong>
                  <Truong
                    nhan="Mật khẩu mới"
                    batBuoc
                    lop="col-md-4 mb-2"
                    goiY={<DoManhMatKhau matKhau={mk.matKhauMoi} ten={duLieu.tenDangNhap} gon />}
                  >
                    <input
                      type="password"
                      className="form-control"
                      value={mk.matKhauMoi}
                      onChange={doiMk('matKhauMoi')}
                      minLength={8}
                      maxLength={50}
                      autoComplete="new-password"
                    />
                  </Truong>
                  <Truong nhan="Xác nhận mật khẩu mới" batBuoc lop="col-md-4 mb-2">
                    <input
                      type="password"
                      className="form-control"
                      value={mk.xacNhan}
                      onChange={doiMk('xacNhan')}
                      minLength={8}
                      maxLength={50}
                      autoComplete="new-password"
                    />
                  </Truong>
                </div>
                <button className="btn btn-warning" disabled={dangChay && luuMk}>
                  <i className="bi bi-key me-1" />
                  Đổi mật khẩu
                </button>
              </form>
            </TheNoiDung>
          </div>

          <div className="col-lg-5">
            {dn ? (
              <>
                <TheNoiDung tieuDe="Doanh nghiệp liên kết" icon="bi-buildings">
                  <div className="fs-5 fw-bold mb-3">{dn.tenCongTy}</div>
                  <div className="row g-2">
                    <div className="col-6">
                      <div className="kh-o-tin">
                        <div className="kh-nhan">Mã số thuế</div>
                        <div className="fw-semibold">{dn.maSoThue || '—'}</div>
                      </div>
                    </div>
                    <div className="col-6">
                      <div className="kh-o-tin">
                        <div className="kh-nhan">Người liên hệ</div>
                        <div className="fw-semibold">{dn.nguoiLienHe || '—'}</div>
                      </div>
                    </div>
                    <div className="col-12">
                      <div className="kh-o-tin">
                        <div className="kh-nhan">Địa chỉ</div>
                        <div className="fw-semibold">{dn.diaChi || '—'}</div>
                      </div>
                    </div>
                    <div className="col-6">
                      <div className="kh-o-tin">
                        <div className="kh-nhan">Email</div>
                        <div className="fw-semibold text-truncate">{dn.email || '—'}</div>
                      </div>
                    </div>
                    <div className="col-6">
                      <div className="kh-o-tin">
                        <div className="kh-nhan">Điện thoại</div>
                        <div className="fw-semibold">{dn.soDienThoai || '—'}</div>
                      </div>
                    </div>
                  </div>
                </TheNoiDung>
                <TheNoiDung tieuDe="Lối tắt" icon="bi-lightning-charge">
                  <div className="d-grid gap-2">
                    <Link className="btn btn-primary" to="/booking/create">
                      <i className="bi bi-plus-circle me-1" />
                      Đặt dịch vụ mới
                    </Link>
                    <Link className="btn btn-outline-secondary" to="/booking/my">
                      <i className="bi bi-receipt me-1" />
                      Booking của tôi
                    </Link>
                    <Link className="btn btn-outline-secondary" to="/tracking">
                      <i className="bi bi-search me-1" />
                      Tra cứu container
                    </Link>
                  </div>
                </TheNoiDung>
              </>
            ) : (
              <TheNoiDung tieuDe="Lối tắt" icon="bi-lightning-charge">
                <div className="d-grid gap-2">
                  <Link className="btn btn-primary" to="/admin">
                    <i className="bi bi-speedometer2 me-1" />
                    Về màn hình điều hành
                  </Link>
                  <Link className="btn btn-outline-secondary" to="/account/logout">
                    <i className="bi bi-box-arrow-right me-1" />
                    Đăng xuất
                  </Link>
                </div>
              </TheNoiDung>
            )}
          </div>
        </div>
      </div>
    </>
  )
}
