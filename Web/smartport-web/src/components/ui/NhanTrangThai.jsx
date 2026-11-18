// Nhan trang thai mau theo nghia — giong Ui.Badge cua ban MVC de nguoi dung quen mat mau cu.
const MAU_THEO_TRANG_THAI = {
  'chờ duyệt': 'warn',
  'chờ xuất tàu': 'warn',
  'bảo trì': 'warn',
  'chờ thanh toán': 'warn',
  'chưa thu': 'warn',
  'đã duyệt': 'info',
  'dự kiến': 'info',
  'trong bãi': 'info',
  'mới': 'info',
  'đang thực hiện': 'run',
  'đang làm hàng': 'run',
  'đã cập bến': 'run',
  'đang xử lý': 'run',
  'đã vào cổng': 'run',
  'hoàn thành': 'ok',
  'hoàn tất': 'ok',
  'đã rời bến': 'ok',
  'đã xuất tàu': 'ok',
  'đã giao khách': 'ok',
  'hoạt động': 'ok',
  'đã thanh toán': 'ok',
  'đã thu': 'ok',
  'thành công': 'ok',
  'đã hủy': 'off',
  'hủy': 'off',
  'hết hạn': 'off',
  'khóa': 'off',
  'từ chối': 'do',
  'thất bại': 'do',
  'lỗi': 'do',
}

/** 'ok' | 'info' | 'warn' | 'run' | 'off' | 'do' */
// eslint-disable-next-line react-refresh/only-export-components
export function mauTrangThai(trangThai) {
  return MAU_THEO_TRANG_THAI[(trangThai || '').trim().toLowerCase()] || 'info'
}

/**
 * <NhanTrangThai giaTri="Chờ duyệt" />  hoac ep mau: <NhanTrangThai giaTri="Quá hạn" mau="do" />
 */
export default function NhanTrangThai({ giaTri, mau, icon, children }) {
  const noiDung = children ?? giaTri
  if (noiDung == null || noiDung === '') return <span className="text-muted">—</span>
  return (
    <span className={'sp-nhan sp-nhan-' + (mau || mauTrangThai(giaTri))}>
      {icon && <i className={'bi ' + icon + ' me-1'} />}
      {noiDung}
    </span>
  )
}
