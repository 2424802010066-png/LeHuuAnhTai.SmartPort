import { useEffect, useState } from 'react'
import { API_URL, goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { TheNoiDung } from '../../components/ui'
import { dinhDangNgay } from '../../utils/dinhDang'
import { LogoGoogle } from './DangNhapNhanh'

/** Doc ket qua lien ket Google (trang /api/auth/google/goi-lai chuyen ve /account/profile#lienKet=google | #loi=...) */
function docKetQuaLienKet() {
  const p = new URLSearchParams(window.location.hash.replace(/^#/, ''))
  return { lienKet: p.get('lienKet'), loi: p.get('loi') }
}

/**
 * PHUONG THUC DANG NHAP trong Ho so: lien ket / go tai khoan Google va so dien thoai (OTP) cho tai khoan dang dang nhap.
 * Tai khoan noi bo (quan tri / nhan vien) CHI dang nhap bang so dien thoai sau khi tu lien ket o day.
 */
export default function LienKetDangNhap() {
  const { duLieu: d, taiLai } = useApi('/api/auth/ngoai/lien-ket')
  const [kq] = useState(docKetQuaLienKet)
  const [thongBao, setThongBao] = useState(kq.lienKet === 'google' ? 'Đã liên kết tài khoản Google — từ nay bấm “Tiếp tục với Google” để đăng nhập.' : '')
  const [loi, setLoi] = useState(kq.loi || '')
  const [so, setSo] = useState('')
  const [ma, setMa] = useState('')
  const [daGuiMa, setDaGuiMa] = useState(null)
  const [dangGui, setDangGui] = useState(false)

  useEffect(() => {
    if (window.location.hash) window.history.replaceState(null, '', window.location.pathname)
  }, [])

  const ds = d?.lienKet ?? []
  const google = ds.find((x) => x.nhaCungCap === 'google')
  const sdt = ds.find((x) => x.nhaCungCap === 'sdt')

  async function chay(viec) {
    setLoi('')
    setThongBao('')
    setDangGui(true)
    try {
      await viec()
    } catch (err) {
      setLoi(err.message)
    } finally {
      setDangGui(false)
    }
  }

  const lienKetGoogle = () =>
    chay(async () => {
      const r = await goiApi('/api/auth/google/ve-lien-ket', { method: 'POST' })
      window.location.href = API_URL + r.url
    })

  const go = (x) =>
    chay(async () => {
      await goiApi('/api/auth/ngoai/lien-ket/' + x.maLK, { method: 'DELETE' })
      setThongBao('Đã gỡ liên kết ' + (x.nhaCungCap === 'google' ? 'Google' : 'số điện thoại') + '.')
      taiLai({ im: true })
    })

  const guiMa = (e) => {
    e?.preventDefault()
    return chay(async () => {
      const r = await goiApi('/api/auth/sdt/lien-ket/gui-ma', { method: 'POST', body: { soDienThoai: so.trim() } })
      setDaGuiMa(r)
      setMa('')
    })
  }

  const xacNhan = (e) => {
    e.preventDefault()
    return chay(async () => {
      const r = await goiApi('/api/auth/sdt/lien-ket', { method: 'POST', body: { soDienThoai: so.trim(), ma } })
      setThongBao(r.thongBao)
      setDaGuiMa(null)
      setSo('')
      setMa('')
      taiLai({ im: true })
    })
  }

  return (
    <TheNoiDung tieuDe="Phương thức đăng nhập" icon="bi-key">
      {thongBao && (
        <div className="alert alert-success py-2 small" role="status">
          <i className="bi bi-check-circle-fill me-1" />
          {thongBao}
        </div>
      )}
      {loi && (
        <div className="alert alert-danger py-2 small" role="alert">
          {loi}
        </div>
      )}

      <div className="kh-dong-lien-ket">
        <LogoGoogle co={22} />
        <div className="flex-grow-1">
          <div className="fw-semibold">Google</div>
          <div className="small text-body-secondary">
            {google ? (
              <>
                {google.hienThi} · liên kết {dinhDangNgay(google.taoLuc)}
              </>
            ) : d && !d.google ? (
              'Máy chủ chưa bật đăng nhập Google'
            ) : (
              'Chưa liên kết'
            )}
          </div>
        </div>
        {google ? (
          <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangGui} onClick={() => go(google)}>
            Gỡ
          </button>
        ) : (
          <button type="button" className="btn btn-sm btn-outline-primary" disabled={dangGui || !d?.google} onClick={lienKetGoogle}>
            Liên kết
          </button>
        )}
      </div>

      <div className="kh-dong-lien-ket">
        <i className="bi bi-phone fs-4 text-primary" />
        <div className="flex-grow-1">
          <div className="fw-semibold">Số điện thoại (mã OTP)</div>
          <div className="small text-body-secondary">
            {sdt ? (
              <>
                {sdt.hienThi} · liên kết {dinhDangNgay(sdt.taoLuc)}
              </>
            ) : (
              'Chưa liên kết'
            )}
          </div>
        </div>
        {sdt && (
          <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangGui} onClick={() => go(sdt)}>
            Gỡ
          </button>
        )}
      </div>

      {!sdt && (
        <form className="mt-2" onSubmit={daGuiMa ? xacNhan : guiMa}>
          <div className="input-group input-group-sm">
            <input
              className="form-control"
              value={so}
              onChange={(e) => setSo(e.target.value)}
              placeholder="Số điện thoại, vd 0901 234 567"
              inputMode="tel"
              maxLength={20}
              disabled={!!daGuiMa}
              aria-label="Số điện thoại cần liên kết"
            />
            {daGuiMa && (
              <input
                className="form-control kh-o-ma-nho"
                value={ma}
                onChange={(e) => setMa(e.target.value.replace(/\D/g, '').slice(0, 6))}
                placeholder="Mã 6 số"
                inputMode="numeric"
                autoComplete="one-time-code"
                aria-label="Mã xác nhận"
                autoFocus
              />
            )}
            <button className="btn btn-primary" disabled={dangGui || !so.trim() || (daGuiMa && ma.length !== 6)}>
              {daGuiMa ? 'Xác nhận' : 'Gửi mã'}
            </button>
          </div>
          {daGuiMa && (
            <div className="small text-body-secondary mt-1">
              {daGuiMa.thongBao}
              {daGuiMa.cheDoDemo && daGuiMa.maDemo && (
                <>
                  {' '}
                  (chế độ demo — mã thử <b className="kh-ma-demo">{daGuiMa.maDemo}</b>)
                </>
              )}{' '}
              <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => setDaGuiMa(null)}>
                Đổi số
              </button>
            </div>
          )}
        </form>
      )}
      <div className="small text-body-secondary mt-2">
        Tài khoản nội bộ (quản trị, nhân viên) chỉ đăng nhập được bằng số điện thoại sau khi tự liên kết ở đây.
      </div>
    </TheNoiDung>
  )
}
