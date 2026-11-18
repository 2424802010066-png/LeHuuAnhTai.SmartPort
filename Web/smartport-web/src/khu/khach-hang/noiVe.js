/**
 * Quy tac dieu huong sau khi dang nhap / dang ky — dung chung cho trang dang nhap va dang ky.
 */

/** Chi cho quay lai dia chi NOI BO ("/admin/..."), chan chuyen huong sang site khac */
export const noiBo = (d) => (typeof d === 'string' && d.startsWith('/') && !d.startsWith('//') ? d : null)

/** Co returnUrl thi ve do; khong thi nhan vien / quan tri ve /admin, khach hang ve trang chu */
export function noiVeSauDangNhap(vaiTro, returnUrl) {
  return noiBo(returnUrl) || (vaiTro === 'Admin' || vaiTro === 'NhanVien' ? '/admin' : '/')
}
