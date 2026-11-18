// =====================================================================
// BAN SAO NGHIEP VU TAI TRAM — GET /api/cong/goi-ngoai-tuyen?cong=A1 (hop dong H1, co ETag)
//   { phienBan, gioMayChu, cong, lenhMo:[{maLenh,soLenh,loaiLenh,bienSo,soCont,trangThai,hetHan,henTuLuc}],
//     edo:[{soCont,soEdo,trangThai,hieuLucDen}], theXe:[{bienSo,conHan,loaiThe}], bieuGiaPhiCong:[{loaiXe,gia}],
//     bienCam:["51C12345"], haiQuan:[{soCont,trangThai,daThongQuan}], cheDoKiemTra:{edo,haiQuan: 'tat'|'canh-bao'|'chan'},
//     biCat: true khi goi bi CAT (vuot 500 lenh / 3000 the xe) }
//   + header ETag (bam noi dung, khong ke gioMayChu) va X-Gio-May-Chu.
// biCat: ban sao KHONG day du -> khong tim thay lenh trong ban sao KHONG co nghia la "khong co lenh" (khongDayDu).
// Dang xuat -> xoaHet(): ban sao la du lieu nhay cam (lenh, chu xe, the xe) — KHONG de lai tren may tram.
// Dung KHI MAT MANG de doi chieu lenh / eDO / the xe thay vi bao nham "KHONG CO LENH".
// Luon ghi ro "ban sao luc HH:mm"; qua 4 gio khong xac nhan lai duoc -> "ban sao cu" (vang).
// Ban sao chi de CANH BAO / chan mac dinh — may chu van la noi phan xu cuoi cung khi dong bo (can-doi-soat).
// =====================================================================

export const TUOI_BAN_SAO_CU_MS = 4 * 3600 * 1000
const GIA_HAN_HEN_MS = 2 * 3600 * 1000 // giong DoiChieuLenh phia may chu: noi long 2 tieng sau gio hen
const TRANG_THAI_LENH_MO = ['Chờ xe', 'Đã vào cổng']
const EDO_KHONG_DUNG_DUOC = ['Đã hủy', 'Đã sử dụng', 'Hết hạn']

const TEN_LOAI_LENH = { GiaoCont: 'Giao container (khách nhận hàng)', HaCont: 'Hạ container vào bãi', NhanRong: 'Nhận container rỗng', TraRong: 'Trả container rỗng' }
/** Ma loai lenh trong ban sao (GiaoCont...) -> ten hien cho nguoi dung */
export const tenLoaiLenh = (ma) => TEN_LOAI_LENH[ma] || ma || ''

export const chuanHoaBien = (s) =>
  String(s || '')
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, '')

const khoaBanSao = (cong) => 'goi-ngoai-tuyen:' + (cong || 'A1')
const gio = (v) => {
  const t = v ? new Date(v).getTime() : NaN
  return Number.isFinite(t) ? t : null
}

export function taoBanSao({ kho, goiDayDu, dongHo = Date.now, tuoiCu = TUOI_BAN_SAO_CU_MS }) {
  // Tang moi lan xoaHet (dang xuat): lan tai dang bay (gui bang token cu) ve SAU khi xoa thi bo, khong ghi lai ban sao
  let theHe = 0

  /**
   * Tai goi ban sao (co If-None-Match). 304 -> chi cap nhat "xac nhan luc".
   * goiDayDu(duong, { headers, choPhep304 }) -> { trangThai, duLieu, etag }
   */
  async function taiVe(cong = 'A1', { timeoutMs = 20000 } = {}) {
    const khoa = khoaBanSao(cong)
    const theHeLuc = theHe
    const cu = await kho.lay('banSao', khoa)
    const headers = cu?.etag ? { 'If-None-Match': cu.etag } : {}
    const r = await goiDayDu('/api/cong/goi-ngoai-tuyen?cong=' + encodeURIComponent(cong), { headers, choPhep304: true, timeoutMs })
    const bayGio = dongHo()
    if (theHeLuc !== theHe) return { thayDoi: false, banGhi: null, boQua: true } // da dang xuat giua chung
    if (r.trangThai === 304 && cu) {
      const moi = { ...cu, xacNhanLuc: bayGio, gioMayChu: r.gioMayChu || cu.gioMayChu }
      await kho.ghi('banSao', moi)
      return { thayDoi: false, banGhi: moi }
    }
    const d = r.duLieu || {}
    const banGhi = {
      khoa,
      cong,
      etag: r.etag || null,
      phienBan: d.phienBan ?? null,
      gioMayChu: d.gioMayChu ?? r.gioMayChu ?? null,
      taiLuc: bayGio,
      xacNhanLuc: bayGio,
      duLieu: {
        lenhMo: Array.isArray(d.lenhMo) ? d.lenhMo : [],
        edo: Array.isArray(d.edo) ? d.edo : [],
        theXe: Array.isArray(d.theXe) ? d.theXe : [],
        bieuGiaPhiCong: Array.isArray(d.bieuGiaPhiCong) ? d.bieuGiaPhiCong : [],
        bienCam: Array.isArray(d.bienCam) ? d.bienCam : [],
        haiQuan: Array.isArray(d.haiQuan) ? d.haiQuan : [],
        cheDoKiemTra: d.cheDoKiemTra && typeof d.cheDoKiemTra === 'object' ? d.cheDoKiemTra : null,
        biCat: d.biCat === true,
      },
    }
    await kho.ghi('banSao', banGhi)
    return { thayDoi: true, banGhi }
  }

  const lay = (cong = 'A1') => kho.lay('banSao', khoaBanSao(cong))

  const laCu = (banGhi) => !banGhi || dongHo() - (banGhi.xacNhanLuc || 0) > tuoiCu

  /** Tom tat ban sao moi nhat (cho thanh trang thai): { cong, luc, cu } | null */
  async function tomTat() {
    const ds = await kho.tatCa('banSao')
    if (!ds.length) return null
    const moi = ds.reduce((a, b) => ((b.xacNhanLuc || 0) > (a.xacNhanLuc || 0) ? b : a))
    return { cong: moi.cong, luc: moi.xacNhanLuc, cu: laCu(moi), phienBan: moi.phienBan, biCat: moi.duLieu?.biCat === true }
  }

  /** Dang xuat: xoa MOI ban sao (moi cong) — giu hop thu di / nhat ky (xe da qua that) */
  async function xoaHet() {
    theHe++
    await kho.xoaHet('banSao')
  }

  return { taiVe, lay, laCu, tomTat, xoaHet, doiChieu: (banGhi, yc) => doiChieuBanSao(banGhi, { ...yc, bayGio: yc?.bayGio ?? dongHo() }) }
}

/**
 * Doi chieu 1 xe voi ban sao (THUAN — kiem thu duoc).
 * lenhBiet: lenh may chu da tra luc con mang (neu co) -> chi can kiem tuan thu tu ban sao.
 * Tra { lenh, edo, theXe, chan: ly do KHONG cho qua | null, canhBao: [], phiTamTinh, biCat, khongDayDu }
 *   khongDayDu = goi bi cat VA khong tim thay lenh -> KHONG duoc khang dinh "khong co lenh" (kiem giay to bang mat).
 */
export function doiChieuBanSao(banGhi, { bienSo, huongDi, loaiXe, lenhBiet = null, bayGio = Date.now() } = {}) {
  const d = banGhi?.duLieu || {}
  const sach = chuanHoaBien(bienSo)
  const canhBao = []
  let chan = null

  // 1) Bien so trong danh sach cam
  const cam = (d.bienCam || []).find((x) => chuanHoaBien(typeof x === 'string' ? x : x?.bienSo) === sach)
  if (sach && cam) chan = 'Biển số ' + bienSo + ' nằm trong danh sách cấm' + (cam?.lyDo ? ' (' + cam.lyDo + ')' : '') + '.'

  // 2) Lenh giao nhan: uu tien lenh dung huong (xe Vao -> "Chờ xe", xe Ra -> "Đã vào cổng")
  let lenh = null
  if (lenhBiet) {
    lenh = (d.lenhMo || []).find((l) => l.maLenh === lenhBiet.maLenh) || {
      maLenh: lenhBiet.maLenh,
      soLenh: lenhBiet.soLenh,
      soCont: lenhBiet.soCont,
      loaiLenh: /giao container/i.test(lenhBiet.loai || '') ? 'GiaoCont' : null,
      bienSo,
      trangThai: lenhBiet.trangThai,
    }
  } else if (sach) {
    const ung = (d.lenhMo || []).filter((l) => {
      if (chuanHoaBien(l.bienSo) !== sach) return false
      if (l.trangThai && !TRANG_THAI_LENH_MO.includes(l.trangThai)) return false
      const het = gio(l.hetHan)
      return het == null || het > bayGio - GIA_HAN_HEN_MS
    })
    const uuTien = huongDi === 'Ra' ? 'Đã vào cổng' : 'Chờ xe'
    lenh = ung.find((l) => l.trangThai === uuTien) || ung[0] || null
  }

  // 3) Giao container ra cong: eDO + to khai hai quan (cung luat TuanThuCongService phia may chu).
  //    Muc kiem theo cheDoKiemTra cua may chu: 'chan' -> khong cho qua | 'canh-bao' -> chi canh bao | 'tat' -> bo qua.
  //    Ban sao THIEU eDO / to khai: che do 'chan' -> CHAN (mac dinh khong cho qua, bao ve van co nut "vẫn cho qua" ->
  //    muc mang vuotBanSao + lyDoVuot, may chu gan can-doi-soat). Truoc day chi canh bao = "fail-open": ban sao thieu
  //    eDO thi container van ra du may chu se chan neu con mang. Che do khac 'chan' (hoac may chu cu) -> canh bao nhu cu.
  const cheDo = d.cheDoKiemTra || {}
  const viPham = (mucKiem, lyDo) => {
    if (mucKiem === 'tat') return
    if (mucKiem === 'canh-bao') canhBao.push(lyDo)
    else chan = chan || lyDo
  }
  let edo = null
  let toKhai = null
  if (lenh && huongDi === 'Ra' && lenh.loaiLenh === 'GiaoCont' && lenh.soCont) {
    if (cheDo.edo !== 'tat') {
      edo = (d.edo || []).find((e) => e.soCont === lenh.soCont) || null
      const tenEdo = 'eDO ' + (edo?.soEdo ? edo.soEdo + ' ' : '') + 'của container ' + lenh.soCont
      if (!edo) {
        if (cheDo.edo === 'chan') chan = chan || 'Bản sao không có eDO hợp lệ cho container ' + lenh.soCont + ' — chưa xác nhận được quyền lấy hàng.'
        else canhBao.push('Bản sao chưa có eDO cho container ' + lenh.soCont + ' — kiểm tra giấy tờ.')
      } else {
        const het = gio(edo.hieuLucDen)
        if (EDO_KHONG_DUNG_DUOC.includes(edo.trangThai)) viPham(cheDo.edo, tenEdo + ' ở trạng thái "' + edo.trangThai + '".')
        else if (het != null && het < bayGio) viPham(cheDo.edo, tenEdo + ' đã hết hạn lấy hàng.')
      }
    }
    if (cheDo.haiQuan !== 'tat') {
      toKhai = (d.haiQuan || []).find((h) => h.soCont === lenh.soCont) || null
      if (!toKhai) {
        // may chu cu chua gui cheDoKiemTra / haiQuan -> khong canh bao gia
        if (cheDo.haiQuan === 'chan') chan = chan || 'Bản sao không có tờ khai thông quan cho container ' + lenh.soCont + ' — chưa xác nhận được thông quan.'
        else if (cheDo.haiQuan) canhBao.push('Bản sao chưa có tờ khai hải quan cho container ' + lenh.soCont + ' — kiểm tra giấy tờ.')
      } else if (!toKhai.daThongQuan)
        viPham(
          cheDo.haiQuan,
          /tạm dừng/i.test(toKhai.trangThai || '')
            ? 'Hải quan đang TẠM DỪNG container ' + lenh.soCont + '.'
            : 'Tờ khai của container ' + lenh.soCont + ' chưa thông quan (' + (toKhai.trangThai || 'chưa rõ') + ').',
        )
    }
  }

  // 4) The xe (thu phi tu dong khi co mang lai)
  const theXe = sach ? (d.theXe || []).find((t) => chuanHoaBien(t.bienSo) === sach) || null : null
  if (theXe && theXe.conHan === false) canhBao.push('Thẻ xe' + (theXe.loaiThe ? ' (' + theXe.loaiThe + ')' : '') + ' đã hết hạn — phí cổng sẽ tính công nợ.')

  // 5) Phi cong tam tinh cho xe ra (tham khao; Tai chinh thu that khi dong bo)
  let phiTamTinh = null
  if (huongDi === 'Ra') {
    const lx = (loaiXe || (lenh ? 'Đầu kéo container' : '')).toLowerCase()
    const gia = (d.bieuGiaPhiCong || []).find((g) => (g.loaiXe || '').toLowerCase() === lx)
    if (gia && Number.isFinite(Number(gia.gia))) phiTamTinh = Number(gia.gia)
  }

  // 6) Goi bi cat (vuot gioi han lenh / the xe): khong thay lenh trong ban sao != khong co lenh
  const biCat = d.biCat === true
  const khongDayDu = biCat && !lenh

  return { lenh, edo, toKhai, theXe, chan, canhBao, phiTamTinh, biCat, khongDayDu }
}
