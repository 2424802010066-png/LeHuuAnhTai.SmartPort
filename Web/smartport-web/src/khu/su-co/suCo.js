// =====================================================================
// Tien ich dung chung cua khu XU LY SU CO (UC22 -> UC28): ten hien thi, mau nhan, nen anh hien truong.
// Ma loai / muc do / trang thai GIONG HET cot CSDL (bang SuCo) va API /api/su-co.
// =====================================================================

export const LOAI_SU_CO = [
  { ma: 'AnNinhAnToan', ten: 'An ninh, an toàn', icon: 'bi-shield-exclamation' },
  { ma: 'HangHoaThietBi', ten: 'Hàng hóa, thiết bị', icon: 'bi-box-seam' },
  { ma: 'ThanhToan', ten: 'Thanh toán, hóa đơn', icon: 'bi-credit-card' },
  { ma: 'DichVu', ten: 'Dịch vụ, phản ánh', icon: 'bi-chat-square-text' },
  { ma: 'HeThong', ten: 'Hệ thống', icon: 'bi-hdd-network' },
]

/** Khach hang chi phan anh duoc 3 loai (an ninh / he thong la viec noi bo cua cang) */
export const LOAI_CUA_KHACH = ['HangHoaThietBi', 'ThanhToan', 'DichVu']

export const MUC_DO = [
  { ma: 'Thap', ten: 'Thấp', mau: 'off' },
  { ma: 'TrungBinh', ten: 'Trung bình', mau: 'warn' },
  { ma: 'NguyHiem', ten: 'Nguy hiểm', mau: 'do' },
]

export const TRANG_THAI = [
  { ma: 'Moi', ten: 'Mới', mau: 'info' },
  { ma: 'DangXuLy', ten: 'Đang xử lý', mau: 'run' },
  { ma: 'DaXuLy', ten: 'Đã xử lý', mau: 'ok' },
  { ma: 'Dong', ten: 'Đã đóng', mau: 'off' },
  { ma: 'BaoGia', ten: 'Báo giả', mau: 'off' },
]

const tim = (ds, ma) => ds.find((x) => x.ma === ma)

export const tenLoai = (ma) => tim(LOAI_SU_CO, ma)?.ten ?? ma
export const iconLoai = (ma) => tim(LOAI_SU_CO, ma)?.icon ?? 'bi-exclamation-circle'
export const mauMucDo = (ma) => tim(MUC_DO, ma)?.mau ?? 'info'
export const mauTrangThaiSuCo = (ma) => tim(TRANG_THAI, ma)?.mau ?? 'info'

/** "42 phút", "3,5 giờ", "2 ngày" — cho o chi so thoi gian trung binh */
export function thoiLuongPhut(phut) {
  if (phut == null) return '—'
  if (phut < 60) return Math.round(phut).toLocaleString('vi-VN') + ' phút'
  if (phut < 48 * 60) return (Math.round((phut / 60) * 10) / 10).toLocaleString('vi-VN') + ' giờ'
  return Math.round(phut / 1440).toLocaleString('vi-VN') + ' ngày'
}

/**
 * Nen anh hien truong truoc khi gui: thu nho canh dai toi da 1280 px, JPEG chat luong 0.8 -> chuoi data:image/jpeg;base64.
 * Anh dien thoai 4-8 MB thanh ~200-400 KB, vua gioi han ~2 MB cua API.
 */
export function nenAnh(tep, canhToiDa = 1280, chatLuong = 0.8) {
  return new Promise((xong, loi) => {
    if (!tep || !tep.type?.startsWith('image/')) {
      loi(new Error('Chỉ chọn được tệp ảnh (JPG, PNG…).'))
      return
    }
    const doc = new FileReader()
    doc.onerror = () => loi(new Error('Không đọc được tệp ảnh.'))
    doc.onload = () => {
      const anh = new Image()
      anh.onerror = () => loi(new Error('Tệp ảnh bị hỏng hoặc không hỗ trợ.'))
      anh.onload = () => {
        const tiLe = Math.min(1, canhToiDa / Math.max(anh.width, anh.height))
        const c = document.createElement('canvas')
        c.width = Math.round(anh.width * tiLe)
        c.height = Math.round(anh.height * tiLe)
        c.getContext('2d').drawImage(anh, 0, 0, c.width, c.height)
        const ra = c.toDataURL('image/jpeg', chatLuong)
        if (ra.length > 2_800_000) loi(new Error('Ảnh vẫn quá 2 MB sau khi nén — chọn ảnh khác.'))
        else xong(ra)
      }
      anh.src = doc.result
    }
    doc.readAsDataURL(tep)
  })
}
