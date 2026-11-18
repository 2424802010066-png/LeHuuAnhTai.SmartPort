// =====================================================================
// QUY TAC MAT KHAU phia giao dien — GIONG BaoMatMatKhau.KiemTraChinhSach cua server
// (SmartPort.Core/Helpers/BaoMatMatKhau.cs). Server van kiem lai; o day de goi y ngay khi go,
// khong phai bam gui moi biet sai. Sua quy tac thi sua CA HAI noi.
// =====================================================================
export const DO_DAI_TOI_THIEU = 8
export const DO_DAI_TOI_DA = 50

// Mat khau hay bi do nhat (so khop chu thuong, bo khoang trang 2 dau)
const PHO_BIEN = new Set([
  '12345678', '123456789', '1234567890', '123123123', '11111111', '00000000', '88888888', '66666666',
  'password', 'password1', 'password123', 'passw0rd', 'matkhau', 'matkhau1', 'matkhau123', 'matkhau@123',
  'abc12345', 'abcd1234', 'abc123456', 'qwerty123', 'qwertyuiop', '1q2w3e4r', '1qaz2wsx', 'iloveyou',
  'admin123', 'admin1234', 'admin@123', 'smartport', 'smartport1', 'smartport123', '12345678a', 'a12345678',
  'zxcvbnm123', 'anhyeuem', 'anhyeuem1', 'emyeuanh', '123456aa', 'aa123456', '123456abc', 'abc123abc',
])

const MUC = [
  { nhan: 'Rất yếu', mau: 'do' },
  { nhan: 'Yếu', mau: 'do' },
  { nhan: 'Trung bình', mau: 'vang' },
  { nhan: 'Mạnh', mau: 'la' },
  { nhan: 'Rất mạnh', mau: 'la' },
]

/**
 * Danh gia mat khau moi.
 *   ten: ten dang nhap — bo trong khi chua biet (vd nguoi dung nhap email o trang quen mat khau)
 * Tra ve { hopLe, diem 0..4, nhan, mau, dieuKien: [{ ma, ten, dat }] }.
 * hopLe = dat MOI dieu kien bat buoc (giong server); diem = do manh de hien thanh do.
 */
export function danhGiaMatKhau(matKhau = '', ten = '') {
  const s = matKhau || ''
  const tenGon = (ten || '').trim()
  const dieuKien = [
    { ma: 'dai', ten: `Từ ${DO_DAI_TOI_THIEU} đến ${DO_DAI_TOI_DA} ký tự`, dat: s.length >= DO_DAI_TOI_THIEU && s.length <= DO_DAI_TOI_DA },
    { ma: 'chu', ten: 'Có chữ cái', dat: /\p{L}/u.test(s) },
    { ma: 'so', ten: 'Có chữ số', dat: /\d/.test(s) },
    { ma: 'phoBien', ten: 'Không phải mật khẩu phổ biến', dat: s.length > 0 && !PHO_BIEN.has(s.trim().toLowerCase()) && new Set(s).size > 2 },
  ]
  if (tenGon.length >= 3)
    dieuKien.push({ ma: 'ten', ten: 'Không chứa tên đăng nhập', dat: s.length > 0 && !s.toLowerCase().includes(tenGon.toLowerCase()) })
  if (s.length > 0 && s.trim() !== s) dieuKien.push({ ma: 'khoangTrang', ten: 'Không có khoảng trắng ở đầu / cuối', dat: false })
  const hopLe = dieuKien.every((d) => d.dat)

  // Do manh: do dai + so nhom ky tu (thuong, HOA, so, ky tu dac biet). Chua dat dieu kien bat buoc thi toi da "Yếu".
  let diem = 0
  if (s) {
    const nhom = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((r) => r.test(s)).length
    diem = (s.length >= 8 ? 1 : 0) + (s.length >= 12 ? 1 : 0) + (nhom >= 3 ? 1 : 0) + (nhom === 4 || s.length >= 16 ? 1 : 0)
    if (!hopLe) diem = Math.min(diem, 1)
  }
  return { hopLe, diem, nhan: s ? MUC[diem].nhan : '', mau: s ? MUC[diem].mau : '', dieuKien }
}
