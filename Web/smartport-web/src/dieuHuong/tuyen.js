// =====================================================================
// SO DANG KY MAN HINH (route) — gom tu tung khu chuc nang trong src/khu/<khu>/tuyen.js.
// Moi khu tu khai bao man hinh cua minh, App.jsx va menu tu doc o day:
// them khu moi khong phai sua App.jsx, cac nhom lam song song khong dung file cua nhau.
//
// Mot tuyen:
//   duong     '/admin/vessels' — tuyet doi, chu thuong, tham so dang :id (giu dia chi ban MVC cu)
//   trang     component (lazy(() => import('./DanhSachTau')))
//   ten       tieu de tren thanh tren (bo trong -> lay ten muc menu)
//   vaiTro    ['Admin'] — gioi han vai tro. Mac dinh: /admin -> Admin + NhanVien; cong khai -> ai cung vao
//   boCuc     'quan-tri' | 'cong-khai' | 'in' (trang in, khong menu) | 'trong' (dang nhap...)
//             mac dinh: /admin/... -> 'quan-tri', con lai -> 'cong-khai'
//   chuyenDen '/admin' — tuyen chi chuyen huong (dia chi cu con nam trong link / bookmark)
//   props     { congKhai: true } — thuoc tinh truyen vao component trang
// =====================================================================
import aiThiGiac from '../khu/ai-thi-giac/tuyen'
import aiVanHanh from '../khu/ai-van-hanh/tuyen'
import anNinh from '../khu/an-ninh/tuyen'
import cangSo from '../khu/cang-so/tuyen'
import cong from '../khu/cong/tuyen'
import congKhai from '../khu/cong-khai/tuyen'
import container from '../khu/container/tuyen'
import dichVuKhach from '../khu/dich-vu-khach/tuyen'
import dieuHanh from '../khu/dieu-hanh/tuyen'
import dieuPhoi from '../khu/dieu-phoi/tuyen'
import ediDepot from '../khu/edi-depot/tuyen'
import heThong from '../khu/he-thong/tuyen'
import khachHang from '../khu/khach-hang/tuyen'
import kiemDem from '../khu/kiem-dem/tuyen'
import kinhDoanh from '../khu/kinh-doanh/tuyen'
import taiChinh from '../khu/tai-chinh/tuyen'
import taiXe from '../khu/tai-xe/tuyen'
import tauBen from '../khu/tau-ben/tuyen'
import tienIch from '../khu/tien-ich/tuyen'
import tongQuan from '../khu/tong-quan/tuyen'

export const NHAN_VIEN = ['Admin', 'NhanVien']

const laKhuQuanTri = (duong) => /^\/admin(\/|$)/i.test(duong)

export const TUYEN = [
  ...tongQuan,
  ...cong,
  ...cangSo,
  ...taiXe,
  ...container,
  ...ediDepot,
  ...tauBen,
  ...taiChinh,
  ...kinhDoanh,
  ...aiThiGiac,
  ...anNinh,
  ...kiemDem,
  ...dieuHanh, ...dieuPhoi,
  ...aiVanHanh,
  ...heThong,
  ...congKhai,
  ...tienIch,
  ...khachHang,
  ...dichVuKhach,
].map((t) => ({
  ...t,
  duong: t.duong.toLowerCase(),
  boCuc: t.boCuc || (laKhuQuanTri(t.duong) ? 'quan-tri' : 'cong-khai'),
  vaiTro: t.vaiTro || (laKhuQuanTri(t.duong) ? NHAN_VIEN : null),
}))

// Bao loi som khi hai khu dang ky trung dia chi (de nham man hinh)
if (import.meta.env.DEV) {
  const daCo = new Set()
  for (const t of TUYEN) {
    if (daCo.has(t.duong)) console.error('[dieuHuong] Trùng địa chỉ màn hình:', t.duong)
    daCo.add(t.duong)
  }
}

/** Duong dan co man hinh that (khong tinh tuyen chuyen huong) — de an/hien muc menu */
export const DUONG_CO_MAN_HINH = new Set(TUYEN.filter((t) => t.trang).map((t) => t.duong))

export const duocVao = (tuyen, vaiTro) => !tuyen?.vaiTro || tuyen.vaiTro.includes(vaiTro)
