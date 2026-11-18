// =====================================================================
// BO DONG BO HOP THU DI -> POST /api/cong/dong-bo (hop dong H1)
//   { maThietBi, lucGuiTuMay, items: [ { maThaoTac, bienSo, huongDi, cong, loaiXe, maLenh, soCont, lucXayRa,
//                                        tienMatTaiCong, soPhieuThu, anh, ghiChu, nguoiThaoTac } ] }   (≤ 200 muc)
//   -> { ketQua: [ { maThaoTac, trangThai: 'da-luu'|'trung'|'can-doi-soat'|'loi', maXV, thongBao, lyDoDoiSoat, thuLai, thoiGian } ], ... }
// Nguyen tac:
//   - MOT luong: chay trong khoa giua cac tab (navigator.locks / lease localStorage).
//   - Chi xoa muc may chu DA TRA KET QUA KHAC "loi" (da-luu / trung / can-doi-soat). Muc may chu khong nhac toi -> giu.
//   - "loi" + thuLai=true (CSDL ban, loi tam thoi): giu lai, lui roi gui sau. "loi" + thuLai=false (du lieu sai):
//     danh dau loi (hien do cho nguoi xu ly), KHONG gui lai mai mai.
//   - Lo <= 50 muc; lo co anh <= 10 muc (va <= ~3 MB JSON).
//   - Dong tab giua luc gui: muc van nam nguyen trong kho, lan sau gui lai CUNG maThaoTac -> may chu tra "trung".
//   - Ca lo loi mang / 5xx / 404 / 409 / 429: giu nguyen tat ca, bao ben ngoai lui dan roi thu lai.
//   - Ca lo 401 / 403 (het phien / dang nhap tai khoan KHONG co quyen chot cong): giu nguyen, KHONG danh loi vinh vien
//     (xe da qua that) -> 'het-phien' / 'khong-quyen': ben ngoai bao "can dang nhap tai khoan co quyen chot cong".
//   - Ca lo bi tu choi 4xx khac (than sai...): danh dau tung muc 'loi' (hien do) thay vi gui lai mai mai.
//   - WI-FI YEU: thoi gian cho TI LE dung luong lo (30 s + theo kich thuoc, tran 170 s < 180 s gateway cat tuyen dong-bo).
//     Lo het gio (qua gio / 504 / 408) hoac 413 -> lan sau THU NHO: nua so muc (toi 1), muc co anh gui RIENG; gui thanh
//     cong thi noi dan (x2) ve muc goc. Muc le van het gio o lo 1 muc -> hoan muc do 10 phut, gui muc khac truoc (khong de
//     1 anh to chan ca hang doi).
// =====================================================================
import { laLoiMang, phanLoaiLoi } from './phanLoaiLoi.js'

export const DUONG_DONG_BO = '/api/cong/dong-bo'
const TOI_DA_MUC_HOP_DONG = 200

const SO_MUC_LO_CO_ANH = 10 // hop dong H1 (Nhom 1): lo co anh toi da 10 muc

export const CHO_TOI_DA_LO_MS = 170000 // gateway cat tuyen /api/cong/dong-bo o 180 s -> tram bo cuoc truoc
const TOC_DO_TOI_THIEU_BYTE_MS = 25 // ~25 KB/s ≈ 0,2 Mbps: Wi-Fi rat yeu van gui xong lo 3 MB trong 150 s
const HOAN_MUC_KET_MS = 10 * 60000

/** Thoi gian cho 1 lo: coBan (30 s) + dung luong / toc do toi thieu, tran 170 s */
export function thoiGianChoLo(soByte, { coBan = 30000, tocDo = TOC_DO_TOI_THIEU_BYTE_MS, tran = CHO_TOI_DA_LO_MS } = {}) {
  return Math.min(tran, Math.max(coBan, Math.round(coBan + (Number(soByte) || 0) / tocDo)))
}

/** Loi do lo QUA TO / QUA CHAM (khong phai mat mang han) -> thu nho lo */
const laHetGioLo = (e) => !!e?.quaGio || e?.trangThai === 504 || e?.trangThai === 408 || e?.trangThai === 413

/** Cat lo theo so muc (≤ 50; co anh ≤ 10) VA dung luong JSON (anh base64 vai tram KB/muc) — luon it nhat 1 muc */
export function catLo(ds, { soMuc = 50, soByte = 3_000_000, soMucCoAnh = SO_MUC_LO_CO_ANH } = {}) {
  const lo = []
  let tong = 0
  let coAnh = false
  for (const x of ds) {
    const co = JSON.stringify(x.duLieu || {}).length
    const anhMoi = coAnh || !!x.duLieu?.anh
    const tran = Math.min(anhMoi ? soMucCoAnh : soMuc, soMuc, TOI_DA_MUC_HOP_DONG)
    if (lo.length > 0 && (lo.length >= tran || tong + co > soByte)) break
    lo.push(x)
    tong += co
    coAnh = anhMoi
  }
  return lo
}

export function taoBoDongBo({
  hopThu,
  goi,
  khoa,
  maThietBi,
  dongHo = Date.now,
  soMucMoiLo = 50,
  soByteMoiLo = 3_000_000,
  timeoutMs = 30000, // thoi gian cho CO BAN; lo lon duoc cho them theo dung luong (thoiGianChoLo)
  choToiDaMs = CHO_TOI_DA_LO_MS,
  soLoToiDa = 20,
  tenKhoa = 'sp-dong-bo-cong',
}) {
  // Gioi han lo hien tai (thu nho khi het gio, noi dan khi gui duoc) — trong bo nho cua tab, moi tab tu do
  let tranMuc = soMucMoiLo
  let anhRieng = false
  const hoanDen = new Map() // maThaoTac -> gio duoc gui lai (muc le het gio ca khi gui 1 minh)

  const noiGioiHan = () => {
    tranMuc = Math.min(soMucMoiLo, tranMuc * 2)
    if (tranMuc >= soMucMoiLo) anhRieng = false
  }

  async function guiHet() {
    const tong = { trangThai: 'xong', daGui: 0, trung: 0, canDoiSoat: 0, loi: 0, thuLai: 0, soLo: 0, ketQua: [] }
    for (let lan = 0; lan < soLoToiDa; lan++) {
      const tatCa = await hopThu.layCanGui()
      if (!tatCa.length) break
      const bayGio = dongHo()
      const ds = tatCa.filter((x) => !((hoanDen.get(x.maThaoTac) || 0) > bayGio))
      if (!ds.length) break // chi con muc dang hoan: de nhip sau
      const lo = catLo(ds, { soMuc: tranMuc, soByte: soByteMoiLo, soMucCoAnh: anhRieng ? 1 : SO_MUC_LO_CO_ANH })
      const than = {
        maThietBi,
        lucGuiTuMay: new Date(dongHo()).toISOString(),
        items: lo.map((x) => ({ ...x.duLieu, maThaoTac: x.maThaoTac })),
      }
      let phanHoi
      try {
        phanHoi = await goi(DUONG_DONG_BO, {
          method: 'POST',
          body: than,
          timeoutMs: thoiGianChoLo(JSON.stringify(than).length, { coBan: timeoutMs, tran: choToiDaMs }),
        })
      } catch (e) {
        const loai = phanLoaiLoi(e)
        if (e?.trangThai === 401 || e?.trangThai === 403) {
          // Phien het han / tai khoan dang nhap khong co quyen chot cong: muc KHONG sai -> giu nguyen cho dang nhap dung
          return { ...tong, trangThai: e.trangThai === 403 ? 'khong-quyen' : 'het-phien', loi: e }
        }
        // 413 ma lo chi con 1 muc: thu nho khong giup duoc -> xuong duoi danh 'loi' (hien do cho nguoi xu ly) nhu cu
        if (laHetGioLo(e) && !(e?.trangThai === 413 && lo.length === 1)) {
          if (lo.length > 1 || !anhRieng) {
            tranMuc = Math.max(1, Math.floor(lo.length / 2))
            anhRieng = true
          } else hoanDen.set(lo[0].maThaoTac, dongHo() + HOAN_MUC_KET_MS) // 1 muc van qua cham: hoan, gui muc khac truoc
          return { ...tong, trangThai: laLoiMang(e) ? 'loi-mang' : 'loi-may-chu', loi: e, thuNhoLo: true }
        }
        const giuLai =
          laLoiMang(e) || loai === 'huy' || loai === 'may-chu' || loai === 'khac' || [404, 408, 409, 429].includes(e?.trangThai)
        if (giuLai) {
          return {
            ...tong,
            trangThai: laLoiMang(e) || loai === 'huy' ? 'loi-mang' : 'loi-may-chu',
            loi: e,
          }
        }
        // 4xx khac: may chu tu choi CA LO (than sai). Danh dau tung muc de nguoi xu ly thay, khong lap vo han.
        for (const x of lo) await hopThu.danhDauLoi(x.maThaoTac, e?.message || 'Máy chủ từ chối lô đồng bộ.')
        tong.loi += lo.length
        tong.soLo++
        continue
      }

      tong.soLo++
      noiGioiHan()
      for (const x of lo) hoanDen.delete(x.maThaoTac)
      const theoMa = new Map((Array.isArray(phanHoi?.ketQua) ? phanHoi.ketQua : []).filter((k) => k && k.maThaoTac).map((k) => [k.maThaoTac, k]))
      let thieu = 0
      let thuLaiLoNay = 0
      for (const x of lo) {
        const k = theoMa.get(x.maThaoTac)
        if (!k) {
          thieu++ // may chu khong tra muc nay -> giu nguyen, lan sau gui lai (cung maThaoTac nen khong kep)
          continue
        }
        tong.ketQua.push(k)
        if (k.trangThai === 'loi') {
          if (k.thuLai) {
            thuLaiLoNay++ // loi tam thoi phia may chu: giu nguyen 'cho', gui lai sau thoi gian lui
            tong.thuLai++
          } else {
            await hopThu.danhDauLoi(x.maThaoTac, k.thongBao || 'Máy chủ từ chối lượt này.')
            tong.loi++
          }
        } else if (k.trangThai === 'can-doi-soat') {
          await hopThu.chuyenSangDoiSoat(x, k) // DA ghi nhung can nguoi doi soat -> hien vang
          tong.canDoiSoat++
        } else {
          // da-luu / trung (gui lan truoc da toi, mat phan hoi) / trang thai khac "loi": may chu da nhan -> xoa
          await hopThu.xoa(x.maThaoTac)
          if (k.trangThai === 'trung') tong.trung++
          else tong.daGui++
        }
      }
      if (thuLaiLoNay > 0) return { ...tong, trangThai: 'thu-lai-sau' } // de ben ngoai lui dan, khong gui lai ngay
      if (thieu > 0) break // tranh vong lap gui lai ngay muc may chu bo qua; nhip sau se thu
    }
    return tong
  }

  /** Mot vong dong bo (trong khoa giua tab). { trangThai: 'xong'|'tab-khac'|'thu-lai-sau'|'loi-mang'|'loi-may-chu'|'het-phien', ... } */
  async function chayMotLan() {
    const r = await khoa.chayNeuRanh(tenKhoa, guiHet)
    if (!r.daChay) return { trangThai: 'tab-khac', daGui: 0, trung: 0, canDoiSoat: 0, loi: 0, soLo: 0, ketQua: [] }
    return r.ketQua
  }

  return {
    chayMotLan,
    /** cho kiem thu / chan doan: gioi han lo hien tai */
    get gioiHan() {
      return { tranMuc, anhRieng, soHoan: hoanDen.size }
    },
  }
}
