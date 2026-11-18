// =====================================================================
// HOP THU DI CUA TRAM (outbox phia trinh duyet) — moi luot xe ghi XUONG DIA TRUOC, gui sau.
//
// Ban ghi trong kho 'hopThuDi' (khoa = maThaoTac):
//   { maThaoTac, loai: 'luot-xe', duLieu: <muc dung hop dong H1>, phu: <truong chi dung khi gui truc tiep luot-xe>,
//     taoLuc, trangThai: 'cho' | 'dang-gui' | 'loi', giuDen, soLanThu, loiCuoi }
//   'dang-gui' = tab nay dang gui TRUC TIEP (POST /api/cong/luot-xe); giuDen = han giu cho. Tab chet giua chung
//   -> het han giu -> bo dong bo nhat lai, gui bang CUNG maThaoTac (may chu tra "trung", khong ghi kep).
//   'loi' = may chu tu choi rieng muc nay -> KHONG tu gui lai, hien DO cho nguoi xu ly (Thu lai / Bo).
// Kho 'nhatKy': muc may chu da ghi nhung gan "can-doi-soat" -> hien VANG toi khi nguoi dung bam "Da xem".
// =====================================================================
import { laLoiMang, phanLoaiLoi } from './phanLoaiLoi.js'
import { taoMaThaoTac } from './maThaoTac.js'

const GIU_CHO_MAC_DINH = 60000 // > timeout gui truc tiep (~10 s): tab con song thi khong bao gio bi tab khac gui chen
export const DO_DAI_LY_DO_VUOT = 300 // hop dong /api/cong/dong-bo: lyDoVuot <= 300 ky tu
export const LY_DO_VUOT_MAY_CHU_CHAM = 'Máy chủ không trả lời kịp khi đang có mạng (hết giờ/502-504)'

/**
 * Gan co "bao ve da cho qua du chua duoc xac nhan" vao muc (hop dong moi cua /api/cong/dong-bo):
 *   vuotBanSao: true + lyDoVuot (<= 300 ky tu) -> may chu LUON gan "can doi soat" kem ly do tram da bao.
 * Chi them 2 truong khi THAT SU vuot (muc thuong giu dung bo truong H1 cu, may chu cu khong bi la).
 */
export function ganVuot(muc, lyDo) {
  const lyDoVuot = String(lyDo || 'Bảo vệ cho qua khi chưa được máy chủ xác nhận').trim().slice(0, DO_DAI_LY_DO_VUOT)
  return { ...muc, vuotBanSao: true, lyDoVuot }
}

/**
 * Tao muc luot xe dung ten truong hop dong H1 (POST /api/cong/dong-bo, items[]).
 * lucXayRa = gio xe qua cong THAT (gio may tram); may chu tu hieu chinh lech dong ho bang lucGuiTuMay.
 */
export function taoMucLuotXe(
  { bienSo, huongDi, cong, loaiXe, maLenh, soCont, anh, ghiChu, nguoiThaoTac, tienMatTaiCong, soPhieuThu, doTinCay, vuotBanSao, lyDoVuot },
  { dongHo = Date.now, taoMa = taoMaThaoTac } = {},
) {
  const muc = {
    maThaoTac: taoMa(),
    bienSo: String(bienSo || '')
      .trim()
      .toUpperCase()
      .slice(0, 20),
    huongDi: huongDi === 'Ra' ? 'Ra' : 'Vào',
    cong: cong || 'A1',
    loaiXe: loaiXe || null,
    maLenh: maLenh ?? null,
    soCont: soCont || null,
    lucXayRa: new Date(dongHo()).toISOString(),
    tienMatTaiCong: tienMatTaiCong ?? null,
    soPhieuThu: soPhieuThu || null,
    anh: anh || null,
    ghiChu: ghiChu || null,
    nguoiThaoTac: nguoiThaoTac || null,
    doTinCay: Number.isFinite(doTinCay) ? doTinCay : null, // MucDongBoCongDto.DoTinCay (Nhom 1): giu do tin cay AI cua luot ghi tam
  }
  return vuotBanSao ? ganVuot(muc, lyDoVuot) : muc
}

/** Than POST /api/cong/luot-xe (API cu, dang dung) tu muc H1 + truong phu (nguon, doTinCay, ocrTho) */
export function thanLuotXeTrucTiep(muc, phu = {}) {
  return {
    bienSo: muc.bienSo,
    huongDi: muc.huongDi,
    cong: muc.cong,
    loaiXe: muc.loaiXe,
    maLenh: muc.maLenh,
    nguon: phu.nguon ?? null,
    doTinCay: muc.doTinCay ?? phu.doTinCay ?? null,
    anh: muc.anh,
    ocrTho: phu.ocrTho ?? null,
    ghiChu: muc.ghiChu,
  }
}

export function taoHopThuDi({ kho, dongHo = Date.now, giuCho = GIU_CHO_MAC_DINH, khiDoi = () => {} }) {
  const bao = () => {
    try {
      khiDoi()
    } catch {
      /* nguoi nghe loi khong duoc lam hong hop thu */
    }
  }

  return {
    kho,

    /** Ghi muc moi (CHO giao dich xong moi tra ve). dangGui = tab nay se gui truc tiep ngay */
    async them(muc, { phu = null, dangGui = false } = {}) {
      const bayGio = dongHo()
      await kho.ghi('hopThuDi', {
        maThaoTac: muc.maThaoTac,
        loai: 'luot-xe',
        duLieu: muc,
        phu,
        taoLuc: bayGio,
        trangThai: dangGui ? 'dang-gui' : 'cho',
        giuDen: dangGui ? bayGio + giuCho : 0,
        soLanThu: 0,
        loiCuoi: null,
      })
      bao()
    },

    /**
     * Ghi muc NEU CHUA CO (cung maThaoTac) trong 1 giao dich — chuyen hang doi cu chay lai / 2 tab cung chuyen khong
     * ghi de muc da 'dang-gui' / 'loi'. Tra true neu vua them.
     */
    async themNeuChuaCo(muc, { phu = null } = {}) {
      const bayGio = dongHo()
      let daThem = false
      await kho.capNhat('hopThuDi', muc.maThaoTac, (cu) => {
        if (cu) return undefined
        daThem = true
        return { maThaoTac: muc.maThaoTac, loai: 'luot-xe', duLieu: muc, phu, taoLuc: bayGio, trangThai: 'cho', giuDen: 0, soLanThu: 0, loiCuoi: null }
      })
      if (daThem) bao()
      return daThem
    },

    /** Gui truc tiep that bai vi mang -> tra ve hang cho cho bo dong bo */
    async traVeCho(ma, loi) {
      await kho.capNhat('hopThuDi', ma, (cu) => (cu ? { ...cu, trangThai: 'cho', giuDen: 0, soLanThu: (cu.soLanThu || 0) + 1, loiCuoi: loi || null } : undefined))
      bao()
    },

    async xoa(ma) {
      await kho.xoa('hopThuDi', ma)
      bao()
    },

    /** Muc can gui: 'cho', hoac 'dang-gui' da het han giu (tab gui truc tiep da chet). Cu nhat truoc. */
    async layCanGui() {
      const bayGio = dongHo()
      const ds = await kho.tatCa('hopThuDi')
      return ds.filter((x) => x.trangThai === 'cho' || (x.trangThai === 'dang-gui' && (x.giuDen || 0) <= bayGio)).sort((a, b) => a.taoLuc - b.taoLuc)
    },

    async layTatCa() {
      return (await kho.tatCa('hopThuDi')).sort((a, b) => a.taoLuc - b.taoLuc)
    },

    /** May chu tu choi rieng muc nay -> giu lai, danh dau loi (khong tu gui lai) */
    async danhDauLoi(ma, thongBao) {
      await kho.capNhat('hopThuDi', ma, (cu) => (cu ? { ...cu, trangThai: 'loi', giuDen: 0, loiCuoi: thongBao || 'Máy chủ từ chối.' } : undefined))
      bao()
    },

    async thuLai(ma) {
      await kho.capNhat('hopThuDi', ma, (cu) => (cu && cu.trangThai === 'loi' ? { ...cu, trangThai: 'cho', loiCuoi: null } : undefined))
      bao()
    },

    /** May chu DA GHI nhung gan can-doi-soat: chuyen sang nhat ky (hien vang), roi xoa khoi hang cho */
    async chuyenSangDoiSoat(banGhi, kq) {
      const m = banGhi.duLieu || {}
      await kho.ghi('nhatKy', {
        maThaoTac: banGhi.maThaoTac,
        trangThai: 'can-doi-soat',
        bienSo: m.bienSo,
        huongDi: m.huongDi,
        cong: m.cong,
        lucXayRa: m.lucXayRa,
        maXV: kq?.maXV ?? null,
        thongBao: kq?.thongBao || null,
        lyDo: kq?.lyDoDoiSoat || kq?.thongBao || null,
        thoiGian: kq?.thoiGian || null, // gio may chu da ghi (gio goc da bu lech dong ho)
        luc: dongHo(),
      })
      await kho.xoa('hopThuDi', banGhi.maThaoTac)
      bao()
    },

    async layNhatKy() {
      return (await kho.tatCa('nhatKy')).sort((a, b) => b.luc - a.luc)
    },

    async xoaNhatKy(ma) {
      await kho.xoa('nhatKy', ma)
      bao()
    },

    /** Dem theo chi muc trangThai (khong nap anh) — goi moi nhip nen phai re */
    async thongKe() {
      const [tong, soLoi, soDangGui, soCanDoiSoat] = await Promise.all([
        kho.dem('hopThuDi'),
        kho.demTheo('hopThuDi', 'trangThai', 'loi'),
        kho.demTheo('hopThuDi', 'trangThai', 'dang-gui'),
        kho.dem('nhatKy'),
      ])
      return { soCho: Math.max(0, tong - soLoi - soDangGui), soLoi, soDangGui, soCanDoiSoat }
    },
  }
}

/**
 * GHI 1 LUOT XE TAI TRAM — loi chung cua ChotBaoVe va CongThongMinh.
 *   guiTrucTiep = true  (co mang): ghi xuong dia TRUOC (write-ahead), POST /api/cong/luot-xe kem header Idempotency-Key.
 *   guiTrucTiep = false (nhip suc khoe bao mat mang): ghi xuong dia roi bao "da ghi tam" ngay, khong bat bao ve cho.
 * Ket qua (ketQua):
 *   'da-ghi'          may chu da ghi (duLieu = phan hoi)
 *   'da-ghi-tam'      loi MANG / qua gio / 502-504 -> nam trong hop thu di (da xuong dia), tu gui khi co mang
 *   'cho-dang-nhap'   401: giu trong hop thu di (khong gan voi phien), gui sau khi dang nhap lai
 *   'bi-tu-choi'      400/403/404... nghiep vu (vd KHONG CHO RA CONG) hoac 500: HIEN DO, KHONG xep hang
 *   (409 = may chu dang xu ly CUNG khoa -> giu trong hop thu di, gui lai sau bang CUNG khoa -> 'da-ghi-tam';
 *    422 = khoa da dung cho luot xe khac -> sinh khoa MOI, gui lai 1 lan)
 *   'chan-ban-sao'    mat mang + ban sao bao vi pham ro rang (truocKhiXepHang tra ly do) -> khong ghi
 *   'khong-luu-duoc'  mat mang ma khong ghi duoc xuong dia (day bo nho...) -> bao in phieu tay
 *   'chua-xac-nhan'   (chi khi canXacNhan) may chu CHUA tra loi duoc (qua gio / loi mang / 502-504 / 409 dang xu ly /
 *                     401) -> KHONG xep hang, KHONG ghi gi xuong may: hien DO "CHUA CHO XE RA" voi 2 lua chon
 *                     Thu lai (cung maThaoTac) | Van cho qua (ganVuot + xep hang). lyDo: 'mang' | 'dang-xu-ly' | 'het-phien'
 *
 * canXacNhan = true: XE RA trong luc tram tin la TOI DUOC may chu. Ly do: may chu cham 8 giay ma tram tu xep hang thi
 * xe cho container ra, duong dong bo ghi tre chi gan "can doi soat" thay vi CHAN eDO het han -> lach tuan thu. Che do nay
 * cung KHONG ghi truoc (write-ahead) vao hop thu di: neu tab chet giua chung, ban ghi 'dang-gui' het han giu se bi bo dong
 * bo gui di (= lach tuan thu, hoac luot RA "ma" trong khi xe con dung o cong). Xe chi ra khi may chu xac nhan (xanh) hoac
 * bao ve CHU DONG bam "Van cho qua" — luc do muc moi vao hop thu di, mang co vuot -> may chu luon gan can doi soat.
 */
export async function ghiLuotXe({
  hopThu,
  goi,
  muc,
  phu = null,
  guiTrucTiep = true,
  timeoutMs = 10000,
  truocKhiXepHang = null,
  taoMa = taoMaThaoTac,
  daDoiKhoa = false,
  canXacNhan = false,
}) {
  const kq = await ghiLuotXeMotKhoa({ hopThu, goi, muc, phu, guiTrucTiep, timeoutMs, truocKhiXepHang, canXacNhan: guiTrucTiep && !!canXacNhan })
  // 422: Idempotency-Key da gan voi luot xe KHAC (bien / huong khac) -> khoa moi, thu lai dung 1 lan
  if (kq.ketQua === 'bi-tu-choi' && kq.loi?.trangThai === 422 && !daDoiKhoa) {
    const mucMoi = { ...muc, maThaoTac: taoMa() }
    return ghiLuotXe({ hopThu, goi, muc: mucMoi, phu, guiTrucTiep, timeoutMs, truocKhiXepHang, taoMa, daDoiKhoa: true, canXacNhan })
  }
  return { ...kq, muc }
}

async function ghiLuotXeMotKhoa({ hopThu, goi, muc, phu, guiTrucTiep, timeoutMs, truocKhiXepHang, canXacNhan }) {
  const ma = muc.maThaoTac
  const kiemTruocXepHang = async () => (truocKhiXepHang ? truocKhiXepHang(muc) : null)

  if (!guiTrucTiep) {
    const lyDo = await kiemTruocXepHang()
    if (lyDo) return { ketQua: 'chan-ban-sao', lyDo, maThaoTac: ma }
    try {
      await hopThu.them(muc, { phu, dangGui: false })
    } catch (e) {
      return { ketQua: 'khong-luu-duoc', loi: e, maThaoTac: ma }
    }
    return { ketQua: 'da-ghi-tam', maThaoTac: ma }
  }

  // Write-ahead: dong tab / sap trinh duyet giua luc gui cung khong mat luot (het han giu -> bo dong bo gui lai cung khoa)
  // (tru che do canXacNhan — xem ghi chu ghiLuotXe: xe RA chua duoc xac nhan thi chua co gi de ghi)
  let daGhiTruoc = false
  if (!canXacNhan) {
    try {
      await hopThu.them(muc, { phu, dangGui: true })
      daGhiTruoc = true
    } catch {
      daGhiTruoc = false // kho hong: van thu gui truc tiep, chi khong co luoi an toan
    }
  }

  try {
    const duLieu = await goi('/api/cong/luot-xe', {
      method: 'POST',
      body: thanLuotXeTrucTiep(muc, phu || {}),
      headers: { 'Idempotency-Key': ma },
      timeoutMs,
    })
    if (daGhiTruoc) await hopThu.xoa(ma).catch(() => {})
    return { ketQua: 'da-ghi', duLieu, maThaoTac: ma }
  } catch (e) {
    const loai = phanLoaiLoi(e)
    const dangXuLy = e?.trangThai === 409 // may chu dang xu ly CUNG khoa (lan bam truoc): gui lai sau, cung khoa
    if (canXacNhan && (laLoiMang(e) || loai === 'huy' || loai === 'het-phien' || dangXuLy)) {
      // Chua biet may chu da kiem eDO / hai quan chua -> KHONG tu xep hang. Ke ca khi mang vua rot han giua luc gui:
      // luc nay khong phan biet duoc "may chu cham" voi "mat mang" (deu la LoiApi 0), doan sai ve phia cho qua chinh la
      // lo hong. Bao ve bam "Thu lai" -> ghiLuotXeTaiTram tinh lai tu nhip suc khoe MOI: neu nhip da xac nhan mat mang thi
      // di duong ngoai tuyen cu (doi chieu ban sao + xep hang, CUNG maThaoTac -> may chu tra "trung" neu lan nay da toi).
      return { ketQua: 'chua-xac-nhan', lyDo: loai === 'het-phien' ? 'het-phien' : dangXuLy ? 'dang-xu-ly' : 'mang', loi: e, maThaoTac: ma }
    }
    if (laLoiMang(e) || loai === 'huy' || loai === 'het-phien' || dangXuLy) {
      const lyDo = loai === 'het-phien' || dangXuLy ? null : await kiemTruocXepHang()
      if (lyDo) {
        if (daGhiTruoc) await hopThu.xoa(ma).catch(() => {})
        return { ketQua: 'chan-ban-sao', lyDo, loi: e, maThaoTac: ma }
      }
      try {
        if (daGhiTruoc) await hopThu.traVeCho(ma, e?.message)
        else await hopThu.them(muc, { phu, dangGui: false })
      } catch (e2) {
        return { ketQua: 'khong-luu-duoc', loi: e2, maThaoTac: ma }
      }
      return { ketQua: loai === 'het-phien' ? 'cho-dang-nhap' : 'da-ghi-tam', loi: e, maThaoTac: ma }
    }
    // May chu DA NGHE va tu choi (4xx) hoac loi xu ly (500): KHONG xep hang — xep hang la lach kiem tra tuan thu
    if (daGhiTruoc) await hopThu.xoa(ma).catch(() => {})
    return { ketQua: 'bi-tu-choi', loi: e, maThaoTac: ma }
  }
}
