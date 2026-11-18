// =====================================================================
// NHIP SUC KHOE — "co mang hay khong" quyet dinh bang viec GOI DUOC may chu (GET /suc-khoe), KHONG dung navigator.onLine:
// navigator.onLine van la true khi Wi-Fi con bat nhung khong toi duoc may chu (router mat Internet, may chu ngu, sai VLAN...).
//   - Dang co mang: kiem moi khoangToiDa (5 s).
//   - Hong: kiem lai sau khoangToiThieu (2 s) roi LUI DAN x1.5 toi khoangToiDa (2 -> 3 -> 4,5 -> 5 s).
//   - nguongMat lan hong lien tiep moi chuyen sang "mat mang" (chong nhay trang thai vi 1 goi tin rot).
//   - kiemNgay(): co bang chung moi (API bao loi mang, tab hien lai) -> kiem lien, khong cho het chu ky.
//   - baoCoPhanHoi(): vua co yeu cau API nhan duoc phan hoi -> chac chan dang toi duoc may chu.
//   - congNgung: gateway tra 200 nhung dich vu "cong" nam trong dichVuNgung -> CO mang nhung chua ghi truc tiep duoc
//     (tram xep hang thang, khong bat bao ve cho timeout).
// Thuan JS, henGio / huyHen chen vao duoc -> kiem thu lui dan bang dong ho gia.
// =====================================================================

export function taoNhipSucKhoe({
  kiem, // async () => { coMang: bool, mayChuOn?: bool, congNgung?: bool }
  khiDoi = () => {}, // (trangThai) moi lan co ket qua kiem
  henGio = setTimeout,
  huyHen = clearTimeout,
  dongHo = Date.now,
  khoangToiThieu = 2000,
  khoangToiDa = 5000,
  heSoLui = 1.5,
  nguongMat = 2,
}) {
  let dangChay = false
  let hen = null
  let dangKiem = false
  let theHe = 0 // tang moi lan dung/chay lai: ket qua kiem cu ve tre bi bo qua
  let soLanHong = 0
  let khoangKe = khoangToiDa
  const tt = { coMang: null, mayChuOn: null, congNgung: false, kiemLuc: null, soLanHong: 0, khoangKe: khoangToiDa }

  function datLich(ms) {
    if (hen) huyHen(hen)
    hen = henGio(() => {
      hen = null
      chayKiem()
    }, ms)
  }

  function ghiKetQua(coMang, mayChuOn, congNgung) {
    if (coMang) {
      soLanHong = 0
      khoangKe = khoangToiDa
      tt.coMang = true
      tt.mayChuOn = mayChuOn !== false
      if (congNgung !== undefined) tt.congNgung = !!congNgung
    } else {
      soLanHong++
      khoangKe = soLanHong === 1 ? khoangToiThieu : Math.min(khoangToiDa, Math.round(khoangKe * heSoLui))
      // Chua biet trang thai (vua mo trang) -> hong la bao mat mang ngay; dang co mang -> doi du nguong
      if (tt.coMang === null || soLanHong >= nguongMat) {
        tt.coMang = false
        tt.mayChuOn = false
      }
    }
    tt.kiemLuc = dongHo()
    tt.soLanHong = soLanHong
    tt.khoangKe = khoangKe
  }

  async function chayKiem() {
    if (!dangChay || dangKiem) return
    dangKiem = true
    const theHeLuc = theHe
    let kq
    try {
      kq = await kiem()
    } catch {
      kq = { coMang: false }
    }
    dangKiem = false
    if (!dangChay || theHeLuc !== theHe) return
    ghiKetQua(!!kq?.coMang, kq?.mayChuOn, kq?.congNgung ?? false)
    datLich(khoangKe)
    khiDoi({ ...tt })
  }

  return {
    batDau() {
      if (dangChay) return
      dangChay = true
      theHe++
      datLich(0)
    },
    dung() {
      dangChay = false
      theHe++
      dangKiem = false
      if (hen) huyHen(hen)
      hen = null
    },
    kiemNgay() {
      if (!dangChay || dangKiem) return
      datLich(0)
    },
    baoCoPhanHoi() {
      if (!dangChay) return
      const doi = tt.coMang !== true
      ghiKetQua(true, true)
      datLich(khoangKe)
      if (doi) khiDoi({ ...tt })
    },
    layTrangThai: () => ({ ...tt }),
    get dangChay() {
      return dangChay
    },
  }
}
