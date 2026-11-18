// =====================================================================
// DICH VU NGOAI TUYEN CUA TRAM — dieu phoi: nhip /suc-khoe + bo dong bo + ban sao + dem hop thu di.
// Thuan JS: moi phu thuoc trinh duyet (fetch, window event, BroadcastChannel) duoc CHEN VAO -> kiem thu bang Node.
//
// Vong doi: tu BAT khi co nguoi nghe dau tien (thanh trang thai, man chot cong), tu TAT (sau treDung ms) khi khong con
// ai nghe -> moi hen gio / nguoi nghe su kien deu duoc go, khong ro ri khi doi man hinh.
// Khi nao dong bo: co mang tro lai · moi nhip con muc cho (sau thoi gian lui) · vua ghi muc moi · bam "Gui ngay".
// Lui khi dong bo hong: 2 s -> 4 -> 8 ... toi da 60 s; thanh cong thi ve 0.
// May chu tra 401 / 403 cho lo dong bo (het phien / dang nhap tai khoan khong co quyen chot cong): KHONG danh loi muc
// (xe da qua that) — nho phien bi tu choi, ngung tu gui toi khi DOI phien (dang nhap tai khoan khac), thanh trang thai bao
// "can dang nhap tai khoan co quyen chot cong de gui N luot".
// =====================================================================
import { taoNhipSucKhoe } from './nhipSucKhoe.js'

export const TRANG_THAI_BAN_DAU = Object.freeze({
  coMang: null, // null = dang kiem lan dau
  mayChuOn: null,
  congNgung: false, // gateway bao dich vu "cong" ngung (dichVuNgung) -> xep hang thang
  kiemLuc: null,
  soCho: 0,
  soLoi: 0,
  soDangGui: 0,
  soCanDoiSoat: 0,
  dangDongBo: false,
  dongBoCuoi: null, // { luc, daGui, trung, canDoiSoat, loi }
  loiDongBo: null,
  choDangNhap: false, // con luot cho gui ma chua co phien / phien hien tai bi may chu tu choi (401/403)
  khongQuyen: false, // phien hien tai bi 403: tai khoan khong co quyen chot cong
  banSao: null, // { cong, luc, cu, phienBan, biCat }
  loaiKho: null, // 'indexeddb' | 'local-storage' | 'bo-nho'
})

export function taoDichVuNgoaiTuyen({
  hopThu,
  boDongBo,
  banSao,
  kiemSucKhoe,
  coPhien = () => true,
  layMaPhien = () => null, // dinh danh phien hien tai (token) — doi phien thi het "bi tu choi"
  dongHo = Date.now,
  henGio = setTimeout,
  huyHen = clearTimeout,
  ngheMoiTruong = () => () => {}, // ({ kiemNgay, coPhanHoi, tabKhacDoi }) => ham go nghe
  phatThayDoi = () => {}, // bao tab khac: hop thu di vua doi
  khiBatDau = async () => {},
  nhip: tuyChonNhip = {},
  luiToiThieu = 2000,
  luiToiDa = 60000,
  chuKyBanSao = 3 * 60000,
  treDung = 2000,
}) {
  let trangThai = { ...TRANG_THAI_BAN_DAU }
  const nguoiNghe = new Set()
  const congCanBanSao = new Map() // cong -> so man hinh dang can
  let dangChay = false
  let henDung = null
  let henBanSao = null
  let goNghe = null
  let nhip = null
  let lui = 0
  let choDongBoDen = 0
  let dangDongBo = false
  let canChayLai = false
  let dangTaiBanSao = false
  let phienBiTuChoi = null // { ma, khongQuyen } — phien vua bi may chu tra 401/403 khi gui lo

  function dat(moi) {
    let doi = false
    for (const k of Object.keys(moi)) {
      if (trangThai[k] !== moi[k]) {
        doi = true
        break
      }
    }
    if (!doi) return
    trangThai = { ...trangThai, ...moi }
    for (const f of [...nguoiNghe]) {
      try {
        f()
      } catch {
        /* mot nguoi nghe loi khong chan nguoi khac */
      }
    }
  }

  /** Phien hien tai van la phien may chu vua tu choi (chua dang nhap lai / chua doi tai khoan) */
  function phienDangBiTuChoi() {
    if (!phienBiTuChoi) return false
    if (phienBiTuChoi.ma === layMaPhien()) return true
    phienBiTuChoi = null // da doi phien -> thu lai
    return false
  }

  async function demLai() {
    try {
      const tk = await hopThu.thongKe()
      const conCho = tk.soCho + tk.soDangGui > 0
      const biTuChoi = phienDangBiTuChoi()
      dat({ ...tk, choDangNhap: conCho && (!coPhien() || biTuChoi), khongQuyen: conCho && biTuChoi && !!phienBiTuChoi?.khongQuyen })
    } catch {
      /* kho tam loi: giu so cu */
    }
  }

  async function capNhatTomTatBanSao() {
    try {
      dat({ banSao: await banSao.tomTat() })
    } catch {
      /* bo qua */
    }
  }

  async function dongBo({ epBuoc = false } = {}) {
    if (!dangChay && !epBuoc) return null
    if (dangDongBo) {
      canChayLai = true
      return null
    }
    if (!coPhien() || (!epBuoc && phienDangBiTuChoi())) {
      await demLai()
      return null
    }
    if (!epBuoc) {
      if (trangThai.coMang === false) return null
      if (trangThai.congNgung) return null // dich vu Cong dang ngung: gui chi nhan 502/503, cho nhip bao song lai
      if (dongHo() < choDongBoDen) return null
      if (trangThai.soCho + trangThai.soDangGui === 0) return null // dang-gui het han giu (tab chet) cung phai gui
    }
    dangDongBo = true
    dat({ dangDongBo: true })
    let kq = null
    try {
      kq = await boDongBo.chayMotLan()
    } catch (e) {
      kq = { trangThai: 'loi-may-chu', loi: e }
    }
    try {
      // Co lo da duoc may chu tra ket qua (ke ca khi dung giua chung) -> ghi lai de hien "Gui luc HH:mm: ..."
      if (kq.soLo > 0) {
        dat({ dongBoCuoi: { luc: dongHo(), daGui: kq.daGui || 0, trung: kq.trung || 0, canDoiSoat: kq.canDoiSoat || 0, loi: kq.loi || 0 } })
        phatThayDoi()
      }
      if (kq.trangThai === 'xong') {
        lui = 0
        choDongBoDen = 0
        dat({ loiDongBo: null })
      } else if (kq.trangThai === 'tab-khac') {
        // tab khac dang gui: nhip sau xem lai (so dem tu giam nho ban tin tu tab do)
      } else if (kq.trangThai === 'het-phien' || kq.trangThai === 'khong-quyen') {
        // Muc dung, phien sai: giu nguyen, dung tu gui toi khi doi phien (khong lap 401/403 moi nhip)
        phienBiTuChoi = { ma: layMaPhien(), khongQuyen: kq.trangThai === 'khong-quyen' }
        dat({
          loiDongBo:
            kq.trangThai === 'khong-quyen'
              ? 'Tài khoản đang đăng nhập không có quyền chốt cổng — lượt xe vẫn giữ trong máy.'
              : 'Phiên đăng nhập hết hạn — lượt xe vẫn giữ trong máy.',
        })
      } else {
        lui = lui ? Math.min(luiToiDa, lui * 2) : luiToiThieu
        choDongBoDen = dongHo() + lui
        dat({
          loiDongBo:
            kq.trangThai === 'thu-lai-sau'
              ? kq.thuLai + ' lượt máy chủ báo lỗi tạm thời — tự gửi lại sau.'
              : kq.loi?.message || 'Chưa gửi được — sẽ thử lại.',
        })
        if (kq.trangThai === 'loi-mang') nhip?.kiemNgay()
      }
    } finally {
      dangDongBo = false
      await demLai()
      dat({ dangDongBo: false })
    }
    if (canChayLai) {
      canChayLai = false
      if (dangChay) dongBo()
    }
    return kq
  }

  async function taiBanSao() {
    if (!dangChay || dangTaiBanSao || !coPhien() || trangThai.coMang === false || trangThai.congNgung || congCanBanSao.size === 0) return
    dangTaiBanSao = true
    try {
      for (const cong of congCanBanSao.keys()) {
        try {
          await banSao.taiVe(cong) // dang xuat giua chung -> banSao tu bo ket qua (the he)
        } catch {
          /* mat mang / may chu chua co API: giu ban sao cu (van hien gio cu de nguoi dung biet) */
        }
      }
    } finally {
      dangTaiBanSao = false
      await capNhatTomTatBanSao()
    }
  }

  function henTaiBanSao() {
    if (henBanSao) huyHen(henBanSao)
    henBanSao = henGio(() => {
      henBanSao = null
      if (!dangChay) return
      taiBanSao().finally(() => dangChay && henTaiBanSao())
    }, chuKyBanSao)
  }

  function khiNhip(kq) {
    // "Co the gui" = toi duoc gateway VA dich vu cong khong ngung; doi tu khong -> co thi gui ngay
    const truoc = trangThai.coMang === true && !trangThai.congNgung
    // Dem hop thu di TRUOC roi moi doi trang thai mang: vua mo trang khong hien 🟢 trong khi con luot cho gui
    demLai().then(() => {
      dat({ coMang: kq.coMang, mayChuOn: kq.mayChuOn, congNgung: !!kq.congNgung, kiemLuc: kq.kiemLuc })
      if (kq.coMang && !kq.congNgung && !truoc) {
        // Vua co mang lai: bo qua thoi gian lui, gui ngay + lam moi ban sao
        lui = 0
        choDongBoDen = 0
        taiBanSao()
      }
      dongBo()
    })
    capNhatTomTatBanSao()
  }

  function batDau() {
    if (dangChay) return
    dangChay = true
    nhip = taoNhipSucKhoe({ kiem: kiemSucKhoe, khiDoi: khiNhip, henGio, huyHen, dongHo, ...tuyChonNhip })
    nhip.batDau()
    goNghe = ngheMoiTruong({
      kiemNgay: () => nhip?.kiemNgay(),
      coPhanHoi: () => nhip?.baoCoPhanHoi(),
      tabKhacDoi: () => Promise.all([demLai(), capNhatTomTatBanSao()]),
    })
    henTaiBanSao()
    Promise.resolve()
      .then(khiBatDau)
      .catch(() => {})
      .then(() => Promise.all([demLai(), capNhatTomTatBanSao()]))
  }

  function dung() {
    if (!dangChay) return
    dangChay = false
    nhip?.dung()
    nhip = null
    if (henBanSao) huyHen(henBanSao)
    henBanSao = null
    try {
      goNghe?.()
    } catch {
      /* bo qua */
    }
    goNghe = null
  }

  return {
    /** useSyncExternalStore: dang ky nghe -> tra ham huy */
    dangKy(fn) {
      nguoiNghe.add(fn)
      if (henDung) {
        huyHen(henDung)
        henDung = null
      }
      batDau()
      return () => {
        nguoiNghe.delete(fn)
        if (nguoiNghe.size === 0 && !henDung) {
          // tre mot chut: chuyen man (BoCuc -> chot cong) khong phai tat / bat lai nhip
          henDung = henGio(() => {
            henDung = null
            if (nguoiNghe.size === 0) dung()
          }, treDung)
        }
      }
    },
    layTrangThai: () => trangThai,

    /** Vua them / sua muc trong hop thu di (tab nay) -> dem lai, bao tab khac, thu gui */
    async daThayDoi() {
      phatThayDoi()
      await demLai()
      dongBo()
    },

    /** Nut "Gui ngay": bo qua thoi gian lui (van can phien dang nhap) */
    async dongBoNgay() {
      lui = 0
      choDongBoDen = 0
      nhip?.kiemNgay()
      return dongBo({ epBuoc: true })
    },

    /** Man hinh can ban sao cua cong nao (chot cong A1...) -> tra ham bo dang ky */
    dungBanSaoCho(cong = 'A1') {
      congCanBanSao.set(cong, (congCanBanSao.get(cong) || 0) + 1)
      if (dangChay) taiBanSao()
      return () => {
        const n = (congCanBanSao.get(cong) || 1) - 1
        if (n <= 0) congCanBanSao.delete(cong)
        else congCanBanSao.set(cong, n)
      }
    },
    taiLaiBanSao: () => taiBanSao(),
    /** Dang xuat: xoa ban sao lenh / eDO / the xe (du lieu nhay cam), GIU hop thu di + nhat ky (xe da qua that) */
    async xoaBanSao() {
      try {
        await banSao.xoaHet()
      } finally {
        dat({ banSao: null })
        phatThayDoi()
        await demLai()
      }
    },
    demLai,
    datLoaiKho: (loai) => dat({ loaiKho: loai }),
    // cho kiem thu
    get dangChay() {
      return dangChay
    },
    get choDongBoDen() {
      return choDongBoDen
    },
  }
}

/**
 * Mo ta trang thai cho thanh trang thai: 🟢 / 🟡 (N cho gui, ban sao luc HH:mm) / 🔴 (co muc loi).
 */
export function moTaTrangThai(tt) {
  const hhmm = (ms) => {
    const d = new Date(ms)
    return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0')
  }
  const phu = []
  if (tt.soCho + tt.soDangGui > 0) phu.push(tt.soCho + tt.soDangGui + ' chờ gửi')
  if (tt.soCanDoiSoat > 0) phu.push(tt.soCanDoiSoat + ' cần đối soát')
  if (tt.banSao?.luc) phu.push('bản sao lúc ' + hhmm(tt.banSao.luc) + (tt.banSao.cu ? ' (bản sao cũ)' : ''))
  if (tt.banSao?.biCat) phu.push('bản sao không đầy đủ')
  if (tt.choDangNhap) phu.push('cần đăng nhập tài khoản có quyền chốt cổng để gửi ' + (tt.soCho + tt.soDangGui) + ' lượt')

  let mau = 'xanh'
  let chinh = 'Trực tuyến'
  if (tt.soLoi > 0) {
    mau = 'do'
    chinh = tt.soLoi + ' lượt lỗi cần xử lý'
  } else if (tt.coMang === false) {
    mau = 'vang'
    chinh = 'Mất kết nối máy chủ — vẫn ghi được'
  } else if (tt.congNgung) {
    mau = 'vang'
    chinh = 'Dịch vụ Cổng đang ngừng — vẫn ghi được'
  } else if (tt.coMang === null) {
    mau = 'vang'
    chinh = 'Đang kiểm tra kết nối…'
  } else if (tt.soCho + tt.soDangGui > 0 || tt.soCanDoiSoat > 0 || tt.banSao?.cu || tt.mayChuOn === false) {
    mau = 'vang'
    chinh = tt.mayChuOn === false ? 'Máy chủ đang suy giảm' : tt.dangDongBo ? 'Đang gửi…' : 'Trực tuyến'
  }
  if (tt.soLoi > 0 && tt.coMang === false) phu.unshift('mất kết nối')
  const bieuTuong = mau === 'xanh' ? '🟢' : mau === 'vang' ? '🟡' : '🔴'
  return { mau, bieuTuong, chinh, phu, chu: bieuTuong + ' ' + [chinh, ...phu].join(' · ') }
}
