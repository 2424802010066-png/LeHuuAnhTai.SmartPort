// Tien ich dung chung cua khu THU TIEN (phi luu bai, phi cong, doi soat thanh toan, bao cao)
import { useEffect, useRef } from 'react'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangTien, tienGon } from '../../utils/dinhDang'

/**
 * Lam moi du lieu NGAM khi co su kien realtime khop mau (vd 'tai-chinh.*': phi cong / phieu luu bai
 * do luong cong tao bat dong bo). Gop nhieu su kien sat nhau thanh 1 lan goi API.
 * Trang in khong co SuKienProvider -> hook tu khong lam gi.
 */
export function useLamMoiTheoSuKien(mau, taiLai, cho = 700) {
  const hen = useRef(null)
  useEffect(() => () => clearTimeout(hen.current), [])
  useNgheSuKien(mau, () => {
    clearTimeout(hen.current)
    hen.current = setTimeout(() => taiLai({ im: true }), cho)
  })
}

/** So tien cho o chi so: tu 1 trieu tro len viet gon "104,9 tr", nho hon viet du "500.000 đ" */
export const tienO = (v) => (Math.abs(Number(v) || 0) >= 1e6 ? tienGon(v) : dinhDangTien(v || 0))

/** O nhap tien: chi giu chu so roi hien lai kieu 1.234.567 (API hieu ca "1.234.567" lan 1234567) */
export function goTien(chuoi) {
  const so = String(chuoi ?? '').replace(/\D/g, '').replace(/^0+(?=\d)/, '')
  return so ? Number(so).toLocaleString('vi-VN') : ''
}

export const HINH_THUC_THU = ['Chuyển khoản', 'Tiền mặt', 'Thẻ ngân hàng']

export const PHUONG_THUC_TT = [
  { giaTri: 'ChuyenKhoan', nhan: 'Chuyển khoản QR' },
  { giaTri: 'TheNganHang', nhan: 'Thẻ ngân hàng' },
  { giaTri: 'ViDienTu', nhan: 'Ví điện tử' },
  { giaTri: 'TienMat', nhan: 'Tiền mặt' },
]

export const TRANG_THAI_TT = ['Chờ thanh toán', 'Đã thanh toán', 'Đã hủy', 'Quá hạn', 'Đã hoàn tiền']

/** Mau nhan trang thai giao dich: "Quá hạn" la no can xu ly -> do; "Đã hoàn tiền" (UC25.3) -> xam */
export const mauTrangThaiTT = (tt) => (tt === 'Quá hạn' ? 'do' : tt === 'Đã hoàn tiền' ? 'off' : undefined)
