import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { docPhien, goiApi, luuPhien, xoaPhien } from '../api/client'
import { xoaDuLieuNhayCamKhiDangXuat } from '../ngoaiTuyen/tram'

const AuthContext = createContext(null)

function phienConHan() {
  const p = docPhien()
  return p?.token && new Date(p.hetHanUtc) > new Date() ? p : null
}

/**
 * Quan ly dang nhap phia client: luu token JWT, tu dang xuat khi token het han
 * hoac khi API tra 401 (tai khoan bi khoa, doi vai tro...).
 */
export function AuthProvider({ children }) {
  const [phien, setPhien] = useState(phienConHan)

  const dangXuat = useCallback(() => {
    xoaPhien()
    setPhien(null)
    // Ban sao lenh / eDO / the xe cua tram cong (IndexedDB) la du lieu nhay cam: xoa khi dang xuat (ca het han / 401),
    // de nguoi khong dang nhap khong doc duoc. GIU hop thu di: luot xe da qua that, gui khi tai khoan co quyen dang nhap lai.
    // (Dang nhap lai can mang -> ban sao tu tai lai; mat mang thi cung khong vao duoc man chot cong khi chua dang nhap.)
    xoaDuLieuNhayCamKhiDangXuat()
  }, [])

  const dangNhap = useCallback(async (tenDangNhap, matKhau) => {
    const kq = await goiApi('/api/auth/dang-nhap', {
      method: 'POST',
      body: { tenDangNhap, matKhau },
      coToken: false,
    })
    luuPhien(kq)
    setPhien(kq)
    return kq
  }, [])

  /** Nhan phien tu API khac tra token (dang nhap khuon mat, dang ky xong tu dang nhap...) */
  const nhanPhien = useCallback((kq) => {
    luuPhien(kq)
    setPhien(kq)
    return kq
  }, [])

  useEffect(() => {
    const khiHetPhien = () => dangXuat()
    window.addEventListener('smartport:het-phien', khiHetPhien)
    return () => window.removeEventListener('smartport:het-phien', khiHetPhien)
  }, [dangXuat])

  useEffect(() => {
    if (!phien) return undefined
    const conLai = new Date(phien.hetHanUtc) - new Date()
    const hen = setTimeout(dangXuat, Math.max(0, conLai))
    return () => clearTimeout(hen)
  }, [phien, dangXuat])

  const giaTri = useMemo(
    () => ({ phien, taiKhoan: phien?.taiKhoan ?? null, dangNhap, dangXuat, nhanPhien }),
    [phien, dangNhap, dangXuat, nhanPhien],
  )
  return <AuthContext.Provider value={giaTri}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext)
