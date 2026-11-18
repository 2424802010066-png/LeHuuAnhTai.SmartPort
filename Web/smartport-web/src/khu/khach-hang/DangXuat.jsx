import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import DangTai from '../../components/DangTai'

/**
 * Dang xuat roi ve trang chu (thay Account/Logout ban MVC: Session.Abandon + ve Home).
 * Dia chi /account/logout con nam trong link cu / bookmark nen phai giu.
 * Ban API khong giu phien tren may chu: chi can xoa token JWT o trinh duyet.
 * Xoa xong MOI chuyen trang — chuyen truoc thi hieu ung xoa token chua kip chay.
 */
export default function DangXuat() {
  const { dangXuat } = useAuth()
  const [xong, setXong] = useState(false)

  useEffect(() => {
    dangXuat()
    setXong(true)
  }, [dangXuat])

  if (!xong) return <DangTai chu="Đang đăng xuất…" />
  return <Navigate to="/" replace />
}
