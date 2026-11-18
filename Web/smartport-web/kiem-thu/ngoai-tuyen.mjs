// Kiem thu NHOM 4 — TRAM CONG OFFLINE-FIRST (phia trinh duyet cua hop dong H1)
// Chay (trong smartport-web): node kiem-thu/ngoai-tuyen.mjs
// Khong can trinh duyet, khong cai goi nao: kho IndexedDB duoc THAY bang kho trong bo nho (cung giao dien),
// mang / may chu / dong ho / localStorage / navigator.locks deu gia lap. sw.js chay that trong vm voi Cache API gia.
import { createHash, randomBytes } from 'node:crypto'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'

// duong dan TUONG DOI tep nay (smartport-web/kiem-thu) -> chay duoc o moi may
const GOC = new URL('../src/ngoaiTuyen/', import.meta.url)
const nap = (ten) => import(new URL(ten, GOC).href)
const { LoiApi, laLoiMang, laLoiNghiepVu, phanLoaiLoi, laLoiTuGateway } = await nap('phanLoaiLoi.js')
const { taoMaThaoTac, uuidTuByte } = await nap('maThaoTac.js')
const { taoKhoBoNho, taoKhoLocalStorage, taoKhoTuDong } = await nap('khoCucBo.js')
const { taoHopThuDi, taoMucLuotXe, ghiLuotXe, ganVuot, LY_DO_VUOT_MAY_CHU_CHAM } = await nap('hopThuDi.js')
const { taoBoDongBo, catLo, thoiGianChoLo, CHO_TOI_DA_LO_MS } = await nap('dongBo.js')
const { KHOA_HANG_DOI_CU, bamSha256Hex, chuyenHangDoiCu, khoaSuyRaLo, nguonKhoaSuyRa, sha256ThuanJs } = await nap('hangDoiCu.js')
const { goiChongGuiLap, taoBoKhoaGuiLap } = await nap('khoaGuiLap.js')
const { taoKhoaGiuaTab } = await nap('khoaGiuaTab.js')
const { taoNhipSucKhoe } = await nap('nhipSucKhoe.js')
const { taoBanSao, doiChieuBanSao } = await nap('banSao.js')
const { taoDichVuNgoaiTuyen, moTaTrangThai } = await nap('dichVuNgoaiTuyen.js')

let dat = 0
let truot = 0
function kiem(ten, dk) {
  if (dk) dat++
  else {
    truot++
    console.log('  TRUOT:', ten)
  }
}
const xaHet = () => new Promise((ok) => setImmediate(ok))
const ngu = (ms) => new Promise((ok) => setTimeout(ok, ms))

// Ten truong hop dong H1 (POST /api/cong/dong-bo) — khop MucDongBoCongDto cua Nhom 1 (co them doTinCay)
const TRUONG_MUC_H1 = ['maThaoTac', 'bienSo', 'huongDi', 'cong', 'loaiXe', 'maLenh', 'soCont', 'lucXayRa', 'tienMatTaiCong', 'soPhieuThu', 'anh', 'ghiChu', 'nguoiThaoTac', 'doTinCay'].sort()
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

/** localStorage gia (dung chung giua 2 "tab") */
function taoLuuTruGia() {
  const m = new Map()
  return {
    getItem: (k) => (m.has(k) ? m.get(k) : null),
    setItem: (k, v) => m.set(k, String(v)),
    removeItem: (k) => m.delete(k),
    _m: m,
  }
}

/** Dong ho + hen gio gia: kiem soat thoi gian, dem hen gio con treo (phat hien ro ri) */
function taoDongHoGia(batDau = 1_700_000_000_000) {
  let bayGio = batDau
  let id = 0
  const hen = new Map()
  const lich = []
  return {
    dongHo: () => bayGio,
    henGio: (fn, ms = 0) => {
      const k = ++id
      hen.set(k, { fn, luc: bayGio + ms })
      lich.push(ms)
      return k
    },
    huyHen: (k) => {
      hen.delete(k)
    },
    soHen: () => hen.size,
    lich,
    tien(ms) {
      bayGio += ms
    },
    async chay(ms) {
      const den = bayGio + ms
      for (;;) {
        const ds = [...hen.entries()].filter(([, h]) => h.luc <= den).sort((a, b) => a[1].luc - b[1].luc)
        if (!ds.length) break
        const [k, h] = ds[0]
        hen.delete(k)
        bayGio = Math.max(bayGio, h.luc)
        h.fn()
        for (let i = 0; i < 5; i++) await xaHet()
      }
      bayGio = den
      for (let i = 0; i < 5; i++) await xaHet()
    },
  }
}

/**
 * May chu Cong gia: idempotent theo maThaoTac (nhu bang LuotXeNgoaiTuyen cua Nhom 1).
 * Bien so dac biet: DOISOAT* -> can-doi-soat, LOI* -> loi (thuLai=false), TAM* -> loi tam thoi (thuLai=true), BOQUA* -> khong tra muc nay.
 * khoa422: Idempotency-Key bi coi la "da dung cho xe khac" (422).
 */
function taoMayChuGia() {
  const daGhi = new Map() // maThaoTac -> maXV
  let maXV = 100
  const mc = {
    daGhi,
    yeuCau: [], // { duong, tuyChon }
    soLanDongBo: 0,
    cheDo: 'ok', // 'ok' | 'mat-mang' | 'qua-gio' | 'mat-phan-hoi' | so HTTP (400, 401, 500, 502...)
    khoa422: new Set(),
    treMs: 0,
    thongBaoLoi: 'KHÔNG CHO RA CỔNG: eDO EDO-001 đã bị hãng tàu hủy.',
    async goi(duong, tuyChon = {}) {
      mc.yeuCau.push({ duong, tuyChon: structuredClone({ ...tuyChon, signal: undefined }) })
      if (mc.treMs) await ngu(mc.treMs)
      if (mc.cheDo === 'mat-mang') throw new LoiApi(0, 'Không kết nối được máy chủ SmartPort.')
      if (mc.cheDo === 'qua-gio') throw new LoiApi(0, 'Máy chủ không trả lời sau 8 giây.', null, { quaGio: true })
      if (typeof mc.cheDo === 'number') throw new LoiApi(mc.cheDo, mc.cheDo === 400 ? mc.thongBaoLoi : 'Lỗi ' + mc.cheDo)
      let kq
      if (duong === '/api/cong/luot-xe') {
        const khoa = tuyChon.headers?.['Idempotency-Key']
        if (mc.khoa422.has(khoa)) throw new LoiApi(422, 'Idempotency-Key đã dùng cho lượt xe khác.')
        if (khoa && daGhi.has(khoa)) kq = { maXV: daGhi.get(khoa), daCo: true }
        else {
          maXV++
          if (khoa) daGhi.set(khoa, maXV)
          kq = { maXV, gio: '08:00:00' }
        }
      } else if (duong === '/api/cong/dong-bo') {
        mc.soLanDongBo++
        const ketQua = []
        for (const it of tuyChon.body.items) {
          if (it.bienSo.startsWith('BOQUA')) continue
          if (it.bienSo.startsWith('LOI')) {
            ketQua.push({ maThaoTac: it.maThaoTac, trangThai: 'loi', thuLai: false, thongBao: 'Biển số không hợp lệ.' })
            continue
          }
          if (it.bienSo.startsWith('TAM')) {
            ketQua.push({ maThaoTac: it.maThaoTac, trangThai: 'loi', thuLai: true, thongBao: 'CSDL đang bận — gửi lại sau.' })
            continue
          }
          if (daGhi.has(it.maThaoTac)) {
            ketQua.push({ maThaoTac: it.maThaoTac, trangThai: 'trung', maXV: daGhi.get(it.maThaoTac) })
            continue
          }
          maXV++
          daGhi.set(it.maThaoTac, maXV)
          ketQua.push(
            it.bienSo.startsWith('DOISOAT')
              ? { maThaoTac: it.maThaoTac, trangThai: 'can-doi-soat', maXV, thongBao: 'Đã ghi', lyDoDoiSoat: 'Lệnh đã HỦY lúc trạm mất mạng.', thuLai: false, thoiGian: it.lucXayRa }
              : { maThaoTac: it.maThaoTac, trangThai: 'da-luu', maXV, thuLai: false, thoiGian: it.lucXayRa },
          )
        }
        kq = { ketQua, daLuu: ketQua.filter((k) => k.trangThai === 'da-luu').length, gioMayChu: new Date().toISOString() }
      } else throw new LoiApi(404, 'Không có API ' + duong)
      if (mc.cheDo === 'mat-phan-hoi') throw new LoiApi(0, 'Mất kết nối khi đang nhận dữ liệu từ máy chủ.') // DA ghi ma phan hoi rot giua duong
      return kq
    },
  }
  return mc
}

const mucMau = (bienSo, them = {}) => taoMucLuotXe({ bienSo, huongDi: 'Vào', cong: 'A1', nguoiThaoTac: 'Bảo vệ A', ...them })

// =====================================================================
// 1) Phan loai loi: chi loi MANG moi duoc xep hang
// =====================================================================
{
  for (const ma of [0, 502, 503, 504]) kiem('laLoiMang(' + ma + ') = true', laLoiMang(new LoiApi(ma, 'x')))
  kiem('qua gio = loi mang', laLoiMang(new LoiApi(0, 'x', null, { quaGio: true })) && phanLoaiLoi(new LoiApi(0, 'x', null, { quaGio: true })) === 'qua-gio')
  for (const ma of [400, 401, 403, 404, 409, 422, 500]) kiem('laLoiMang(' + ma + ') = false', !laLoiMang(new LoiApi(ma, 'x')))
  kiem('400 = loi nghiep vu', laLoiNghiepVu(new LoiApi(400, 'x')) && phanLoaiLoi(new LoiApi(400, 'x')) === 'nghiep-vu')
  kiem('401 = het phien (khong phai nghiep vu)', !laLoiNghiepVu(new LoiApi(401, 'x')) && phanLoaiLoi(new LoiApi(401, 'x')) === 'het-phien')
  kiem('loi khong phai LoiApi -> khong phai loi mang', !laLoiMang(new Error('x')) && !laLoiMang(null))
  const gw = new LoiApi(504, 'Dịch vụ cong không trả lời', { thongBao: 'x', dichVu: 'cong', nguon: 'gateway', loi: 'Timeout' })
  kiem('502/503/504 tu gateway (nguon:"gateway") = loi mang, xep hang', laLoiMang(gw) && laLoiTuGateway(gw) && !laLoiNghiepVu(gw))
  const tuDichVu = new LoiApi(503, 'Còn lượt lỗi tạm thời', { thongBao: 'x' })
  kiem('503 KHONG tu gateway = van gui lai sau, khong phai nghiep vu', laLoiMang(tuDichVu) && !laLoiTuGateway(tuDichVu) && !laLoiNghiepVu(tuDichVu))
}

// =====================================================================
// 2) maThaoTac: randomUUID neu co, khong thi getRandomValues (HTTP qua LAN)
// =====================================================================
{
  kiem('co randomUUID -> dung randomUUID', taoMaThaoTac({ randomUUID: () => 'tu-random-uuid', getRandomValues: () => null }) === 'tu-random-uuid')
  const khongUuid = { getRandomValues: (b) => globalThis.crypto.getRandomValues(b) } // nhu http://<IP-LAN>: khong co randomUUID
  const ds = new Set()
  let hopLe = true
  for (let i = 0; i < 2000; i++) {
    const m = taoMaThaoTac(khongUuid)
    if (!UUID.test(m) || m.length > 64) hopLe = false
    ds.add(m)
  }
  kiem('khong co randomUUID -> UUID v4 tu getRandomValues, ≤ 64 ky tu', hopLe)
  kiem('2000 ma khong trung', ds.size === 2000)
  kiem('khong co Web Crypto -> van ra UUID hop le', UUID.test(taoMaThaoTac(null)))
  kiem('uuidTuByte dat dung bit phien ban / bien the', uuidTuByte(new Uint8Array(16).fill(255)) === 'ffffffff-ffff-4fff-bfff-ffffffffffff')
  kiem('taoMucLuotXe dung DU va DUNG ten truong H1', JSON.stringify(Object.keys(mucMau('51C12345')).sort()) === JSON.stringify(TRUONG_MUC_H1))
  const m = taoMucLuotXe({ bienSo: ' 51c-123.45 ', huongDi: 'Ra' }, { dongHo: () => Date.UTC(2026, 9, 14, 7, 41, 10) })
  kiem('lucXayRa la gio xe qua cong (ISO UTC), bien so chuan hoa', m.lucXayRa === '2026-10-14T07:41:10.000Z' && m.bienSo === '51C-123.45' && m.huongDi === 'Ra')
}

// =====================================================================
// 3) Ghi luot xe tai tram: XEP HANG khi loi mang, KHONG xep hang khi 400
// =====================================================================
async function thuGhi(cheDo, { guiTrucTiep = true, truocKhiXepHang = null } = {}) {
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho })
  const mc = taoMayChuGia()
  mc.cheDo = cheDo
  const muc = mucMau('51C12345', { huongDi: 'Ra', maLenh: 12, soCont: 'MSCU1234567' })
  const kq = await ghiLuotXe({ hopThu, goi: mc.goi, muc, phu: { nguon: 'ChotCong' }, guiTrucTiep, truocKhiXepHang })
  return { kq, hopThu, mc, muc, ds: await hopThu.layTatCa(), nk: await hopThu.layNhatKy() }
}
{
  const a = await thuGhi('mat-mang')
  kiem('mat mang -> da-ghi-tam', a.kq.ketQua === 'da-ghi-tam')
  kiem('mat mang -> 1 muc trong hop thu di, trang thai cho', a.ds.length === 1 && a.ds[0].trangThai === 'cho' && a.ds[0].maThaoTac === a.muc.maThaoTac)
  kiem('muc xep hang giu nguyen du lieu H1 (maLenh, soCont, lucXayRa)', a.ds[0].duLieu.maLenh === 12 && a.ds[0].duLieu.soCont === 'MSCU1234567' && a.ds[0].duLieu.lucXayRa === a.muc.lucXayRa)
  kiem('gui truc tiep kem header Idempotency-Key = maThaoTac', a.mc.yeuCau[0]?.tuyChon.headers['Idempotency-Key'] === a.muc.maThaoTac && a.mc.yeuCau[0].duong === '/api/cong/luot-xe')
  kiem('gui truc tiep co timeout', a.mc.yeuCau[0]?.tuyChon.timeoutMs > 0)

  const b = await thuGhi('qua-gio')
  kiem('qua gio -> xep hang', b.kq.ketQua === 'da-ghi-tam' && b.ds.length === 1)
  for (const ma of [502, 503, 504]) {
    const c = await thuGhi(ma)
    kiem(ma + ' (gateway) -> xep hang', c.kq.ketQua === 'da-ghi-tam' && c.ds.length === 1 && c.ds[0].trangThai === 'cho')
  }

  const d = await thuGhi(400)
  kiem('400 "KHÔNG CHO RA CỔNG" -> bi-tu-choi', d.kq.ketQua === 'bi-tu-choi' && d.kq.loi?.trangThai === 400)
  kiem('400 -> KHONG xep hang (hop thu di rong, ca ban ghi truoc da xoa)', d.ds.length === 0 && d.nk.length === 0)
  kiem('400 -> thong bao may chu giu nguyen de hien DO', d.kq.loi?.message.startsWith('KHÔNG CHO RA CỔNG'))
  for (const ma of [403, 404, 422, 500]) {
    const e = await thuGhi(ma)
    kiem(ma + ' -> KHONG xep hang', e.kq.ketQua === 'bi-tu-choi' && e.ds.length === 0)
  }
  const k409 = await thuGhi(409)
  kiem('409 (may chu dang xu ly cung khoa) -> giu trong hop thu di, CUNG khoa, gui lai sau', k409.kq.ketQua === 'da-ghi-tam' && k409.ds.length === 1 && k409.ds[0].maThaoTac === k409.muc.maThaoTac)

  const f = await thuGhi(401)
  kiem('401 het phien -> giu trong may cho dang nhap lai (khong gan voi phien)', f.kq.ketQua === 'cho-dang-nhap' && f.ds.length === 1 && f.ds[0].trangThai === 'cho')

  const g = await thuGhi('ok')
  kiem('co mang -> da-ghi, hop thu di rong', g.kq.ketQua === 'da-ghi' && g.ds.length === 0 && g.kq.duLieu?.maXV > 0)

  // 422: khoa da dung cho xe khac -> sinh khoa MOI, gui lai 1 lan
  {
    const kho = taoKhoBoNho()
    const hopThu = taoHopThuDi({ kho })
    const mc = taoMayChuGia()
    const muc = mucMau('51C42242')
    mc.khoa422.add(muc.maThaoTac)
    const kq = await ghiLuotXe({ hopThu, goi: mc.goi, muc })
    const khoaGui = mc.yeuCau.map((y) => y.tuyChon.headers['Idempotency-Key'])
    kiem('422 -> doi khoa moi va ghi duoc', kq.ketQua === 'da-ghi' && khoaGui.length === 2 && khoaGui[0] === muc.maThaoTac && khoaGui[1] !== muc.maThaoTac && UUID.test(khoaGui[1]))
    kiem('422 -> ket qua tra muc mang khoa moi, khong de lai ban ghi khoa cu', kq.muc.maThaoTac === khoaGui[1] && (await hopThu.layTatCa()).length === 0)
  }

  const h = await thuGhi('ok', { guiTrucTiep: false })
  kiem('nhip bao mat mang -> ghi tam ngay, KHONG goi may chu (bao ve khong phai cho timeout)', h.kq.ketQua === 'da-ghi-tam' && h.mc.yeuCau.length === 0 && h.ds.length === 1)

  const i = await thuGhi('mat-mang', { truocKhiXepHang: async () => 'eDO của container MSCU1234567 ở trạng thái "Đã hủy".' })
  kiem('mat mang + ban sao bao vi pham -> chan-ban-sao, khong ghi', i.kq.ketQua === 'chan-ban-sao' && i.ds.length === 0)
  const i2 = await thuGhi('ok', { guiTrucTiep: false, truocKhiXepHang: async () => 'Biển số nằm trong danh sách cấm.' })
  kiem('biet truoc mat mang + ban sao chan -> khong ghi, khong goi may chu', i2.kq.ketQua === 'chan-ban-sao' && i2.ds.length === 0 && i2.mc.yeuCau.length === 0)
}
{
  // Ghi XUONG KHO TRUOC khi bao "da ghi tam": kho ghi cham 30 ms -> luc ghiLuotXe tra ve, muc PHAI dang nam trong kho
  const goc = taoKhoBoNho()
  const thuTu = []
  const khoCham = {
    ...goc,
    async ghi(k, bg) {
      await ngu(30)
      await goc.ghi(k, bg)
      thuTu.push('ghi-xong:' + bg.trangThai)
    },
    async capNhat(k, khoa, ham) {
      await ngu(30)
      const r = await goc.capNhat(k, khoa, ham)
      thuTu.push('cap-nhat-xong')
      return r
    },
  }
  const hopThu = taoHopThuDi({ kho: khoCham })
  const mc = taoMayChuGia()
  mc.cheDo = 'mat-mang'
  const muc = mucMau('29C46828')
  const kq = await ghiLuotXe({ hopThu, goi: async (...a) => (thuTu.push('goi-may-chu'), mc.goi(...a)), muc })
  thuTu.push('bao:' + kq.ketQua)
  const coTrongKho = (await goc.tatCa('hopThuDi')).some((x) => x.maThaoTac === muc.maThaoTac && x.trangThai === 'cho')
  kiem('write-ahead: ghi xuong kho TRUOC khi goi may chu', thuTu.indexOf('ghi-xong:dang-gui') >= 0 && thuTu.indexOf('ghi-xong:dang-gui') < thuTu.indexOf('goi-may-chu'))
  kiem('chi bao "da ghi tam" SAU KHI kho xac nhan', thuTu[thuTu.length - 1] === 'bao:da-ghi-tam' && thuTu.indexOf('cap-nhat-xong') < thuTu.length - 1 && coTrongKho)

  const hopThuHong = taoHopThuDi({
    kho: {
      ...taoKhoBoNho(),
      ghi: async () => {
        throw new Error('QuotaExceededError')
      },
    },
  })
  const kq2 = await ghiLuotXe({ hopThu: hopThuHong, goi: mc.goi, muc: mucMau('29C11111'), guiTrucTiep: false })
  kiem('kho khong ghi duoc -> KHONG noi "da ghi tam" (bao in phieu tay)', kq2.ketQua === 'khong-luu-duoc')
}
{
  // Tab dong GIUA LUC gui truc tiep: muc 'dang-gui' giu cho 60 s, het han -> bo dong bo gui lai CUNG maThaoTac
  const dh = taoDongHoGia()
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho, dongHo: dh.dongHo })
  const muc = mucMau('51D99999')
  ghiLuotXe({ hopThu, goi: () => new Promise(() => {}), muc }) // yeu cau treo mai: tab "chet" truoc khi co phan hoi
  await xaHet()
  await xaHet()
  kiem('dang gui truc tiep -> muc nam trong kho trang thai dang-gui', (await hopThu.layTatCa())[0]?.trangThai === 'dang-gui')
  kiem('con han giu -> bo dong bo KHONG nhat (khong gui chen tab dang song)', (await hopThu.layCanGui()).length === 0)
  dh.tien(61_000)
  const canGui = await hopThu.layCanGui()
  kiem('het han giu -> bo dong bo nhat lai dung maThaoTac cu', canGui.length === 1 && canGui[0].maThaoTac === muc.maThaoTac)
  kiem('thong ke van dem muc dang-gui la cho gui', (await hopThu.thongKe()).soDangGui === 1)
}

// =====================================================================
// 4) Dong bo theo lo + ket qua tung muc
// =====================================================================
function taoBo({ kho = taoKhoBoNho(), mc = taoMayChuGia(), dongHo = Date.now, khoa = taoKhoaGiuaTab({}), soMucMoiLo = 50, soByteMoiLo } = {}) {
  const hopThu = taoHopThuDi({ kho, dongHo })
  const bo = taoBoDongBo({ hopThu, goi: mc.goi, khoa, maThietBi: 'tram-cong-A1', dongHo, soMucMoiLo, soByteMoiLo })
  return { kho, hopThu, mc, bo }
}
{
  const { hopThu, mc, bo } = taoBo()
  for (let i = 0; i < 120; i++) await hopThu.them(mucMau('51C' + String(i).padStart(5, '0')))
  const kq = await bo.chayMotLan()
  const lo = mc.yeuCau.filter((y) => y.duong === '/api/cong/dong-bo')
  kiem('120 muc, lo 50 -> 3 lo (50, 50, 20)', lo.length === 3 && lo.map((y) => y.tuyChon.body.items.length).join(',') === '50,50,20')
  kiem('ket qua: 120 da-luu, hop thu di rong', kq.trangThai === 'xong' && kq.daGui === 120 && (await hopThu.layTatCa()).length === 0)
  const body = lo[0].tuyChon.body
  kiem('than dong bo dung ten H1: maThietBi, lucGuiTuMay, items', JSON.stringify(Object.keys(body).sort()) === JSON.stringify(['items', 'lucGuiTuMay', 'maThietBi']))
  kiem('lucGuiTuMay la ISO, maThietBi dung', !Number.isNaN(Date.parse(body.lucGuiTuMay)) && body.maThietBi === 'tram-cong-A1')
  kiem('moi muc gui dung DU ten truong H1', body.items.every((it) => JSON.stringify(Object.keys(it).sort()) === JSON.stringify(TRUONG_MUC_H1)))
  kiem('moi maThaoTac chi gui 1 lan', new Set(lo.flatMap((y) => y.tuyChon.body.items.map((x) => x.maThaoTac))).size === 120)
  kiem('dong bo co timeout', lo.every((y) => y.tuyChon.timeoutMs > 0))
}
{
  const { hopThu, mc, bo } = taoBo()
  await hopThu.them(mucMau('51C00001'))
  await hopThu.them(mucMau('DOISOAT1'))
  await hopThu.them(mucMau('LOI00001'))
  await hopThu.them(mucMau('BOQUA001'))
  const kq = await bo.chayMotLan()
  const conLai = await hopThu.layTatCa()
  const nk = await hopThu.layNhatKy()
  kiem('ket qua tung muc: 1 da-luu, 1 can-doi-soat, 1 loi', kq.daGui === 1 && kq.canDoiSoat === 1 && kq.loi === 1)
  kiem('can-doi-soat: may chu DA ghi -> ra khoi hang cho, vao nhat ky (hien vang)', nk.length === 1 && nk[0].bienSo === 'DOISOAT1' && nk[0].lyDo?.includes('HỦY') && !conLai.some((x) => x.duLieu.bienSo === 'DOISOAT1'))
  kiem('loi thuLai=false: giu lai trang thai loi + thong bao (hien do)', conLai.some((x) => x.duLieu.bienSo === 'LOI00001' && x.trangThai === 'loi' && x.loiCuoi === 'Biển số không hợp lệ.'))
  kiem('can-doi-soat: luu gio may chu da ghi (thoiGian)', !!nk[0]?.thoiGian)
  kiem('muc may chu khong tra ket qua -> giu nguyen cho gui lai', conLai.some((x) => x.duLieu.bienSo === 'BOQUA001' && x.trangThai === 'cho'))
  const tk = await hopThu.thongKe()
  kiem('thong ke: 1 cho, 1 loi, 1 can doi soat', tk.soCho === 1 && tk.soLoi === 1 && tk.soCanDoiSoat === 1)
  const truocLan2 = mc.yeuCau.length
  await bo.chayMotLan()
  const lan2 = mc.yeuCau.slice(truocLan2).flatMap((y) => y.tuyChon.body.items.map((x) => x.bienSo))
  kiem('muc loi KHONG tu gui lai (cho nguoi xu ly bam Thu lai)', !lan2.includes('LOI00001') && lan2.includes('BOQUA001'))
  const loi = conLai.find((x) => x.duLieu.bienSo === 'LOI00001')
  if (loi) await hopThu.thuLai(loi.maThaoTac)
  kiem('bam Thu lai -> ve hang cho', !!loi && (await hopThu.layCanGui()).some((x) => x.maThaoTac === loi.maThaoTac))
  const mt = moTaTrangThai({ coMang: true, soCho: 0, soDangGui: 0, soLoi: 1, soCanDoiSoat: 0 })
  const mv = moTaTrangThai({ coMang: false, soCho: 3, soDangGui: 0, soLoi: 0, soCanDoiSoat: 0, banSao: { luc: new Date(2026, 9, 14, 7, 41).getTime(), cu: false } })
  const mx = moTaTrangThai({ coMang: true, soCho: 0, soDangGui: 0, soLoi: 0, soCanDoiSoat: 0 })
  kiem('thanh trang thai: co muc loi -> 🔴', mt.mau === 'do' && mt.chu.startsWith('🔴'))
  kiem('thanh trang thai: mat mang -> 🟡 "3 chờ gửi · bản sao lúc 07:41"', mv.mau === 'vang' && mv.chu.includes('3 chờ gửi') && mv.chu.includes('bản sao lúc 07:41'))
  kiem('thanh trang thai: binh thuong -> 🟢', mx.mau === 'xanh' && mx.chu.startsWith('🟢'))
}
{
  // Ca lo loi mang / 503: giu nguyen; ca lo 400: danh dau loi (khong lap vo han)
  const a = taoBo()
  for (let i = 0; i < 3; i++) await a.hopThu.them(mucMau('51C1000' + i))
  a.mc.cheDo = 503
  const k1 = await a.bo.chayMotLan()
  kiem('ca lo 503 -> loi-mang, giu nguyen 3 muc cho', k1.trangThai === 'loi-mang' && (await a.hopThu.layCanGui()).length === 3)
  a.mc.cheDo = 'mat-mang'
  const k2 = await a.bo.chayMotLan()
  kiem('ca lo mat mang -> loi-mang, giu nguyen', k2.trangThai === 'loi-mang' && (await a.hopThu.layCanGui()).length === 3)
  a.mc.cheDo = 401
  const k3 = await a.bo.chayMotLan()
  kiem('ca lo 401 -> het-phien, giu nguyen', k3.trangThai === 'het-phien' && (await a.hopThu.layCanGui()).length === 3)
  a.mc.cheDo = 400
  const k4 = await a.bo.chayMotLan()
  kiem('ca lo 400 -> tung muc thanh loi (hien do), khong gui lai mai', k4.loi === 3 && (await a.hopThu.layCanGui()).length === 0 && (await a.hopThu.thongKe()).soLoi === 3)

  // Loi tam thoi tung muc (thuLai=true): giu 'cho', dung vong nay de ben ngoai lui dan (khong gui lai ngay)
  const t = taoBo()
  await t.hopThu.them(mucMau('51C20001'))
  await t.hopThu.them(mucMau('TAM00001'))
  const kt = await t.bo.chayMotLan()
  const conT = await t.hopThu.layTatCa()
  kiem('loi thuLai=true -> giu nguyen cho, KHONG hien do', conT.length === 1 && conT[0].duLieu.bienSo === 'TAM00001' && conT[0].trangThai === 'cho')
  kiem('loi thuLai=true -> tra "thu-lai-sau" (lui dan), muc khac van xoa', kt.trangThai === 'thu-lai-sau' && kt.thuLai === 1 && kt.daGui === 1 && t.mc.soLanDongBo === 1)

  // Lo co anh <= 10 muc; khong anh <= 50
  const coAnh = Array.from({ length: 15 }, (_, i) => ({ duLieu: { anh: 'data:image/jpeg;base64,AAAA', i } }))
  kiem('lo co anh: toi da 10 muc', catLo(coAnh).length === 10)
  const tron = [...Array.from({ length: 12 }, (_, i) => ({ duLieu: { i } })), { duLieu: { anh: 'data:image/jpeg;base64,AAAA' } }]
  kiem('lo tron (12 khong anh roi 1 co anh): muc co anh sang lo sau', catLo(tron).length === 12)
  kiem('lo khong anh: toi da 50 muc', catLo(Array.from({ length: 80 }, (_, i) => ({ duLieu: { i } }))).length === 50)

  const ds = Array.from({ length: 10 }, (_, i) => ({ duLieu: { anh: 'x'.repeat(1_000_000), i } }))
  kiem('cat lo theo dung luong: anh 1 MB/muc, gioi han 3 MB -> 2-3 muc/lo', catLo(ds, { soMuc: 50, soByte: 3_000_000 }).length === 2)
  kiem('muc qua lon van gui 1 minh (khong ket hang)', catLo([{ duLieu: { anh: 'x'.repeat(5_000_000) } }], { soByte: 3_000_000 }).length === 1)
}

// =====================================================================
// 5) Gui lai KHONG nhan doi (phan hoi rot giua duong -> gui lai cung maThaoTac -> may chu tra "trung")
// =====================================================================
{
  const { hopThu, mc, bo } = taoBo()
  for (let i = 0; i < 5; i++) await hopThu.them(mucMau('60A0000' + i))
  mc.cheDo = 'mat-phan-hoi'
  const k1 = await bo.chayMotLan()
  const maLan1 = mc.yeuCau.flatMap((y) => y.tuyChon.body.items.map((x) => x.maThaoTac)).sort()
  kiem('may chu DA ghi nhung phan hoi rot -> tram giu nguyen 5 muc', k1.trangThai === 'loi-mang' && (await hopThu.layTatCa()).length === 5 && mc.daGhi.size === 5)
  mc.cheDo = 'ok'
  const k2 = await bo.chayMotLan()
  const maLan2 = mc.yeuCau.slice(1).flatMap((y) => y.tuyChon.body.items.map((x) => x.maThaoTac)).sort()
  kiem('gui lai dung CUNG maThaoTac', JSON.stringify(maLan1) === JSON.stringify(maLan2))
  kiem('may chu tra "trung" -> coi nhu da gui, hop thu di rong', k2.trung === 5 && k2.daGui === 0 && (await hopThu.layTatCa()).length === 0)
  kiem('khong nhan doi: may chu chi co 5 luot (khong phai 10)', mc.daGhi.size === 5)

  // Truc tiep: bam Luu, phan hoi rot -> xep hang -> dong bo -> "trung" (dung maXV cu), van 1 luot
  const t = taoBo()
  t.mc.cheDo = 'mat-phan-hoi'
  const muc = mucMau('61H52930', { huongDi: 'Ra' })
  const kq = await ghiLuotXe({ hopThu: t.hopThu, goi: t.mc.goi, muc })
  t.mc.cheDo = 'ok'
  const kd = await t.bo.chayMotLan()
  kiem('luot-xe truc tiep rot phan hoi -> dong bo tra trung, may chu 1 luot', kq.ketQua === 'da-ghi-tam' && kd.trung === 1 && t.mc.daGhi.size === 1)
}

// =====================================================================
// 6) Hai "tab" khong dong bo trung (lease localStorage khi khong co navigator.locks; va Web Locks)
// =====================================================================
async function haiTab(taoKhoa) {
  const kho = taoKhoBoNho() // IndexedDB dung chung giua cac tab cung nguon
  const mc = taoMayChuGia()
  mc.treMs = 40
  const a = taoBo({ kho, mc, khoa: taoKhoa('tab-A') })
  const b = taoBo({ kho, mc, khoa: taoKhoa('tab-B') })
  for (let i = 0; i < 30; i++) await a.hopThu.them(mucMau('51F' + String(i).padStart(5, '0')))
  const [ka, kb] = await Promise.all([a.bo.chayMotLan(), b.bo.chayMotLan()])
  const da = mc.yeuCau.flatMap((y) => y.tuyChon.body.items.map((x) => x.maThaoTac))
  return { ka, kb, mc, da, conLai: (await a.hopThu.layTatCa()).length }
}
{
  const lt = taoLuuTruGia()
  const r = await haiTab((maTab) => taoKhoaGiuaTab({ locks: null, luuTru: lt, maTab, choXacNhanMs: 15 }))
  kiem('lease localStorage: dung 1 tab gui, tab kia "tab-khac"', [r.ka.trangThai, r.kb.trangThai].sort().join(',') === 'tab-khac,xong')
  kiem('lease localStorage: 1 yeu cau dong bo, moi maThaoTac gui dung 1 lan', r.mc.soLanDongBo === 1 && r.da.length === 30 && new Set(r.da).size === 30 && r.conLai === 0)
  kiem('lease duoc tra lai sau khi xong', lt.getItem('sp-khoa:sp-dong-bo-cong') === null)

  // Tab chet de lai lease: het han thi tab khac tiep quan; con han thi khong
  const lt2 = taoLuuTruGia()
  const dh = taoDongHoGia()
  lt2.setItem('sp-khoa:sp-dong-bo-cong', JSON.stringify({ chu: 'tab-chet', nonce: 'x', het: dh.dongHo() + 5000 }))
  const k = taoKhoaGiuaTab({ luuTru: lt2, maTab: 'tab-moi', dongHo: dh.dongHo, choXacNhanMs: 1 })
  kiem('lease cua tab khac con han -> khong chay', !(await k.chayNeuRanh('sp-dong-bo-cong', async () => 1)).daChay)
  dh.tien(6000)
  kiem('lease het han (tab da chet) -> tab moi tiep quan', (await k.chayNeuRanh('sp-dong-bo-cong', async () => 1)).daChay)

  // Web Locks: dung navigator.locks that cua Node neu co, khong thi gia lap toi thieu
  const khoaGia = (() => {
    const dangGiu = new Set()
    return {
      async request(ten, tc, fn) {
        if (dangGiu.has(ten)) return fn(null)
        dangGiu.add(ten)
        try {
          return await fn({ name: ten })
        } finally {
          dangGiu.delete(ten)
        }
      },
    }
  })()
  const locks = globalThis.navigator?.locks ?? khoaGia
  const r2 = await haiTab((maTab) => taoKhoaGiuaTab({ locks, maTab }))
  kiem('Web Locks: dung 1 tab gui, 1 yeu cau dong bo, khong trung', [r2.ka.trangThai, r2.kb.trangThai].sort().join(',') === 'tab-khac,xong' && r2.mc.soLanDongBo === 1 && new Set(r2.da).size === 30)
}

// =====================================================================
// 7) Nhip suc khoe 2-5 s, lui dan; khong dung navigator.onLine
// =====================================================================
{
  const dh = taoDongHoGia()
  let mang = false
  let soLanKiem = 0
  const doi = []
  const nhip = taoNhipSucKhoe({
    kiem: async () => {
      soLanKiem++
      return { coMang: mang, mayChuOn: mang }
    },
    khiDoi: (t) => doi.push(t.coMang),
    henGio: dh.henGio,
    huyHen: dh.huyHen,
    dongHo: dh.dongHo,
  })
  nhip.batDau()
  await dh.chay(0)
  kiem('kiem ngay khi bat dau; chua biet + hong -> mat mang', soLanKiem === 1 && nhip.layTrangThai().coMang === false)
  await dh.chay(2000 + 3000 + 4500 + 5000)
  kiem('mat mang: khoang kiem lui dan 2 s -> 3 s -> 4,5 s -> 5 s', JSON.stringify(dh.lich.slice(0, 5)) === JSON.stringify([0, 2000, 3000, 4500, 5000]))
  await dh.chay(5000)
  kiem('lui dan dung tran 5 s', dh.lich[dh.lich.length - 1] === 5000 && dh.lich.every((ms) => ms === 0 || (ms >= 2000 && ms <= 5000)))
  mang = true
  await dh.chay(5000)
  kiem('co mang lai -> trang thai co mang, kiem moi 5 s', nhip.layTrangThai().coMang === true && dh.lich[dh.lich.length - 1] === 5000)
  mang = false
  await dh.chay(5000)
  kiem('dang co mang: 1 lan hong chua bao mat (chong nhay), kiem lai sau 2 s', nhip.layTrangThai().coMang === true && dh.lich[dh.lich.length - 1] === 2000)
  await dh.chay(2000)
  kiem('hong lan 2 lien tiep -> mat mang', nhip.layTrangThai().coMang === false)
  const truocKiem = soLanKiem
  nhip.kiemNgay()
  await dh.chay(0)
  kiem('kiemNgay (API bao loi mang / tab hien lai) -> kiem lien', soLanKiem === truocKiem + 1)
  nhip.baoCoPhanHoi()
  kiem('API vua co phan hoi -> co mang ngay, khong doi chu ky', nhip.layTrangThai().coMang === true)
  // gateway 200 nhung "cong" trong dichVuNgung -> co mang + congNgung
  let ngung = true
  const dh2 = taoDongHoGia()
  const nhip2 = taoNhipSucKhoe({ kiem: async () => ({ coMang: true, mayChuOn: true, congNgung: ngung }), henGio: dh2.henGio, huyHen: dh2.huyHen, dongHo: dh2.dongHo })
  nhip2.batDau()
  await dh2.chay(0)
  kiem('gateway 200 + cong trong dichVuNgung -> co mang, congNgung', nhip2.layTrangThai().coMang === true && nhip2.layTrangThai().congNgung === true)
  ngung = false
  await dh2.chay(5000)
  kiem('dich vu cong chay lai -> het congNgung', nhip2.layTrangThai().congNgung === false)
  nhip2.dung()
  kiem('thanh trang thai: cong ngung -> 🟡 "Dịch vụ Cổng đang ngừng"', (() => { const m = moTaTrangThai({ coMang: true, congNgung: true, soCho: 0, soDangGui: 0, soLoi: 0, soCanDoiSoat: 0 }); return m.mau === 'vang' && m.chu.includes('Dịch vụ Cổng đang ngừng') })())

  nhip.dung()
  kiem('dung() -> khong con hen gio treo (khong ro ri)', dh.soHen() === 0)
  kiem('chuyen trang thai duoc bao ra ngoai', doi.includes(false) && doi.includes(true))

  const src = readFileSync(new URL('nhipSucKhoe.js', GOC), 'utf8') + readFileSync(new URL('dichVuNgoaiTuyen.js', GOC), 'utf8') + readFileSync(new URL('tram.js', GOC), 'utf8')
  const jsx = readFileSync(new URL('../src/khu/cong/ChotBaoVe.jsx', import.meta.url), 'utf8')
  const boChu = (s) => s.replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '')
  kiem('khong quyet dinh co mang bang navigator.onLine (tram + ChotBaoVe)', !boChu(src).includes('navigator.onLine') && !boChu(jsx).includes('navigator.onLine'))
  kiem('ChotBaoVe het dung hang doi localStorage cu / dong-bo-lo', !jsx.includes("'sp_chotcong_hangdoi'") && !jsx.includes('dong-bo-lo'))
}

// =====================================================================
// 8) Dieu phoi: co mang lai -> tu dong bo; go nguoi nghe -> tat het hen gio / su kien
// =====================================================================
{
  const dh = taoDongHoGia()
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho, dongHo: dh.dongHo })
  let mang = false
  let congNgung = false
  let soLanDongBo = 0
  let daGoNghe = 0
  const dv = taoDichVuNgoaiTuyen({
    hopThu,
    boDongBo: {
      chayMotLan: async () => {
        soLanDongBo++
        for (const x of await hopThu.layCanGui()) await hopThu.xoa(x.maThaoTac)
        return { trangThai: 'xong', soLo: 1, daGui: 1, trung: 0, canDoiSoat: 0, loi: 0 }
      },
    },
    banSao: { tomTat: async () => null, taiVe: async () => ({}) },
    kiemSucKhoe: async () => ({ coMang: mang, mayChuOn: mang, congNgung }),
    dongHo: dh.dongHo,
    henGio: dh.henGio,
    huyHen: dh.huyHen,
    ngheMoiTruong: () => () => daGoNghe++,
  })
  // may chu bao loi tam thoi (thu-lai-sau) -> lui, khong gui lai ngay
  let lanTamThoi = true
  const dvTam = taoDichVuNgoaiTuyen({
    hopThu,
    boDongBo: {
      chayMotLan: async () => {
        if (lanTamThoi) return { trangThai: 'thu-lai-sau', soLo: 1, daGui: 0, trung: 0, canDoiSoat: 0, loi: 0, thuLai: 1 }
        return { trangThai: 'xong', soLo: 0 }
      },
    },
    banSao: { tomTat: async () => null, taiVe: async () => ({}) },
    kiemSucKhoe: async () => ({ coMang: true }),
    dongHo: dh.dongHo,
    henGio: dh.henGio,
    huyHen: dh.huyHen,
  })
  await hopThu.them(mucMau('TAM00009'))
  const goTam = dvTam.dangKy(() => {})
  await dh.chay(0)
  await dvTam.daThayDoi()
  await dh.chay(0)
  kiem('dieu phoi: "thu-lai-sau" -> lui (khong gui lai ngay) + bao loi tam thoi', dvTam.choDongBoDen > dh.dongHo() && /tạm thời/.test(dvTam.layTrangThai().loiDongBo || ''))
  goTam()
  await dh.chay(2500)
  for (const x of await hopThu.layTatCa()) await hopThu.xoa(x.maThaoTac)
  lanTamThoi = false

  const go = dv.dangKy(() => {})
  await dh.chay(0)
  kiem('dieu phoi: nhip dau tien -> mat mang', dv.layTrangThai().coMang === false)
  await hopThu.them(mucMau('51C77777'))
  await dv.daThayDoi()
  await dh.chay(0)
  kiem('mat mang + co muc moi -> KHONG goi dong bo', soLanDongBo === 0 && dv.layTrangThai().soCho === 1)
  mang = true
  await dh.chay(5000)
  kiem('co mang lai -> tu dong bo ngay (khong can bam)', soLanDongBo === 1 && dv.layTrangThai().soCho === 0 && dv.layTrangThai().coMang === true)
  // gateway bao "cong" ngung: co mang nhung KHONG tu dong bo; cong chay lai -> gui ngay
  congNgung = true
  await dh.chay(5000)
  await hopThu.them(mucMau('51C88888'))
  await dv.daThayDoi()
  await dh.chay(0)
  kiem('cong ngung -> giu cho, khong goi dong bo', soLanDongBo === 1 && dv.layTrangThai().soCho === 1 && dv.layTrangThai().congNgung === true)
  congNgung = false
  await dh.chay(5000)
  kiem('cong chay lai -> tu dong bo', soLanDongBo === 2 && dv.layTrangThai().soCho === 0)
  go()
  await dh.chay(2500)
  kiem('khong con ai nghe -> tat: khong con hen gio treo, da go nghe su kien', !dv.dangChay && dh.soHen() === 0 && daGoNghe === 1)
}

// =====================================================================
// 9) Ban sao ngoai tuyen (ETag / 304, qua 4 gio = cu, doi chieu lenh / eDO / bien cam)
// =====================================================================
{
  const dh = taoDongHoGia(Date.UTC(2026, 9, 14, 1, 0, 0))
  const kho = taoKhoBoNho()
  const goiDuoc = []
  const goi = {
    lan: 0,
    async goiDayDu(duong, tc) {
      goiDuoc.push({ duong, headers: tc.headers })
      if (tc.headers['If-None-Match'] === '"v1"') return { trangThai: 304, duLieu: null, etag: '"v1"' }
      return {
        trangThai: 200,
        etag: '"v1"',
        duLieu: {
          phienBan: 'v1',
          gioMayChu: new Date(dh.dongHo()).toISOString(),
          lenhMo: [
            { maLenh: 12, soLenh: 'GN-012', loaiLenh: 'GiaoCont', bienSo: '51C-123.45', soCont: 'MSCU1234567', trangThai: 'Đã vào cổng', hetHan: new Date(dh.dongHo() + 3600e3).toISOString() },
            { maLenh: 13, soLenh: 'GN-013', loaiLenh: 'HaCont', bienSo: '29C-468.28', soCont: 'TGHU7654321', trangThai: 'Chờ xe', hetHan: new Date(dh.dongHo() + 3600e3).toISOString() },
          ],
          edo: [{ soCont: 'MSCU1234567', trangThai: 'Đã hủy', hieuLucDen: new Date(dh.dongHo() + 86400e3).toISOString() }],
          theXe: [{ bienSo: '29C46828', conHan: false }],
          bieuGiaPhiCong: [{ loaiXe: 'Đầu kéo container', gia: 50000 }],
          bienCam: ['30A99999'],
        },
      }
    },
  }
  const bs = taoBanSao({ kho, goiDayDu: goi.goiDayDu, dongHo: dh.dongHo })
  const r1 = await bs.taiVe('A1')
  kiem('ban sao: tai lan dau (200) luu kem ETag', r1.thayDoi && (await bs.lay('A1')).etag === '"v1"' && goiDuoc[0].duong === '/api/cong/goi-ngoai-tuyen?cong=A1')
  dh.tien(3 * 3600e3)
  const r2 = await bs.taiVe('A1')
  kiem('ban sao: lan sau gui If-None-Match, 304 -> giu du lieu, cap nhat gio xac nhan', !r2.thayDoi && goiDuoc[1].headers['If-None-Match'] === '"v1"' && (await bs.lay('A1')).xacNhanLuc === dh.dongHo())
  kiem('ban sao: vua xac nhan -> chua cu', !bs.laCu(await bs.lay('A1')))
  dh.tien(4 * 3600e3 + 1000)
  kiem('ban sao: qua 4 gio khong xac nhan duoc -> "ban sao cu"', bs.laCu(await bs.lay('A1')) && (await bs.tomTat()).cu === true)
  const b = await bs.lay('A1')
  const dRa = doiChieuBanSao(b, { bienSo: '51C12345', huongDi: 'Ra', bayGio: Date.UTC(2026, 9, 14, 2, 0, 0) })
  kiem('doi chieu ban sao: khop lenh theo bien so chuan hoa', dRa.lenh?.maLenh === 12)
  kiem('doi chieu ban sao: giao cont ra, eDO "Đã hủy" -> chan', dRa.chan?.includes('Đã hủy'))
  kiem('doi chieu ban sao: phi cong tam tinh cho xe ra', dRa.phiTamTinh === 50000)
  const dVao = doiChieuBanSao(b, { bienSo: '29C-468.28', huongDi: 'Vào', bayGio: Date.UTC(2026, 9, 14, 2, 0, 0) })
  kiem('doi chieu ban sao: xe vao ha cont -> khong chan, canh bao the het han', dVao.lenh?.maLenh === 13 && !dVao.chan && dVao.canhBao.some((c) => c.includes('Thẻ xe')))
  kiem('doi chieu ban sao: bien cam -> chan', !!doiChieuBanSao(b, { bienSo: '30A-999.99', huongDi: 'Vào' }).chan)
  kiem('doi chieu ban sao: khong co lenh -> lenh null, khong chan', doiChieuBanSao(b, { bienSo: '99Z00000', huongDi: 'Vào' }).lenh === null)

  // cheDoKiemTra + haiQuan (hop dong H1 mo rong cua Nhom 1): cung muc kiem voi TuanThuCongService
  const voi = (cheDoKiemTra, haiQuan = []) => ({ duLieu: { ...b.duLieu, cheDoKiemTra, haiQuan } })
  const yc = { bienSo: '51C12345', huongDi: 'Ra', bayGio: Date.UTC(2026, 9, 14, 2, 0, 0) }
  kiem('cheDoKiemTra edo=canh-bao -> eDO huy chi canh bao, khong chan', (() => { const r = doiChieuBanSao(voi({ edo: 'canh-bao', haiQuan: 'tat' }), yc); return !r.chan && r.canhBao.some((c) => c.includes('Đã hủy')) })())
  kiem('cheDoKiemTra edo=tat -> bo qua eDO', (() => { const r = doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'tat' }), yc); return !r.chan && !r.canhBao.some((c) => c.includes('eDO')) })())
  kiem('cheDoKiemTra edo=chan -> chan', !!doiChieuBanSao(voi({ edo: 'chan', haiQuan: 'tat' }), yc).chan)
  const hqTamDung = [{ soCont: 'MSCU1234567', trangThai: 'Tạm dừng', daThongQuan: false }]
  kiem('hai quan TAM DUNG + haiQuan=chan -> chan', doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'chan' }, hqTamDung), yc).chan?.includes('TẠM DỪNG'))
  kiem('hai quan da thong quan -> khong chan', !doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'chan' }, [{ soCont: 'MSCU1234567', trangThai: 'Đã thông quan', daThongQuan: true }]), yc).chan)
  // F2 (15/10): truoc day "thieu to khai -> chi canh bao" ca o che do chan (fail-open). Nay che do chan -> CHAN, canh-bao -> canh bao
  kiem('ban sao chua co to khai + haiQuan=chan -> CHAN (giong may chu, khong fail-open)', (() => { const r = doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'chan' }, []), yc); return !!r.chan && r.chan.includes('tờ khai') })())
  kiem('ban sao chua co to khai + haiQuan=canh-bao -> chi canh bao', (() => { const r = doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'canh-bao' }, []), yc); return !r.chan && r.canhBao.some((c) => c.includes('tờ khai')) })())
}

// =====================================================================
// 10) Kho du phong localStorage (khi trinh duyet chan IndexedDB) giu duoc qua "tai lai trang"
// =====================================================================
{
  const lt = taoLuuTruGia()
  const h1 = taoHopThuDi({ kho: taoKhoLocalStorage(lt) })
  await h1.them(mucMau('51C55555'))
  const h2 = taoHopThuDi({ kho: taoKhoLocalStorage(lt) }) // "tab moi" sau khi tai lai
  kiem('kho localStorage: muc con sau khi tao lai kho (tai lai trang)', (await h2.layCanGui()).length === 1 && (await h2.thongKe()).soCho === 1)
}

// =====================================================================
// 11) sw.js chay that trong vm: vo offline, khong cache API, KHONG xoa sp-models-v1, cap nhat an toan
// =====================================================================
const NGUON = 'https://tram.local'
function taoMoiTruongSw(mayChu) {
  const kho = new Map()
  const khoaUrl = (r) => new URL(typeof r === 'string' ? r : r.url, NGUON).href
  const mt = { matMang: false, soSkip: 0, nghe: {} }
  const fetchGia = async (r) => {
    if (mt.matMang) throw new TypeError('Failed to fetch')
    const p = new URL(typeof r === 'string' ? r : r.url, NGUON).pathname
    const t = mayChu[p]
    if (typeof t === 'number') return new Response('loi', { status: t })
    return t ? new Response(t, { status: 200, headers: { 'content-type': 'text/html' } }) : new Response('khong co', { status: 404 })
  }
  const taoCache = (m) => ({
    match: async (r) => m.get(khoaUrl(r))?.clone(),
    put: async (r, res) => void m.set(khoaUrl(r), res),
    add: async (r) => {
      const res = await fetchGia(r)
      if (!res.ok) throw new TypeError('add ' + res.status)
      m.set(khoaUrl(r), res)
    },
    addAll: async (ds) => {
      const kq = await Promise.all(
        ds.map(async (r) => {
          const res = await fetchGia(r)
          if (!res.ok) throw new TypeError('addAll ' + res.status)
          return [r, res]
        }),
      )
      for (const [r, res] of kq) m.set(khoaUrl(r), res)
    },
  })
  const caches = {
    open: async (ten) => {
      if (!kho.has(ten)) kho.set(ten, new Map())
      return taoCache(kho.get(ten))
    },
    keys: async () => [...kho.keys()],
    has: async (ten) => kho.has(ten),
    delete: async (ten) => kho.delete(ten),
    match: async (r) => {
      for (const m of kho.values()) if (m.has(khoaUrl(r))) return m.get(khoaUrl(r)).clone()
      return undefined
    },
  }
  class RequestGia extends Request {
    constructor(u, o) {
      super(new URL(u, NGUON), o)
    }
  }
  const self = {
    location: { origin: NGUON },
    addEventListener: (t, f) => (mt.nghe[t] = f),
    skipWaiting: async () => void mt.soSkip++,
    clients: { claim: async () => {} },
  }
  mt.kho = kho
  mt.caches = caches
  mt.ctx = vm.createContext({ self, caches, fetch: fetchGia, Request: RequestGia, Response, Headers, URL, Promise, setTimeout, clearTimeout, console })
  return mt
}
const suKienCho = () => {
  const e = { p: null, waitUntil: (p) => (e.p = p) }
  return e
}
const suKienFetch = (duong, mode = 'no-cors') => {
  const e = { p: null, request: { url: NGUON + duong, method: 'GET', mode, headers: new Headers() }, respondWith: (p) => (e.p = p) }
  return e
}
{
  const nguonSw = readFileSync(new URL('../public/sw.js', import.meta.url), 'utf8')
  kiem('sw.js nguon co dong BAN_DUNG de vite.config.js thay luc build', /^var BAN_DUNG = .*\/\/ @@SP_BAN_DUNG@@/m.test(nguonSw))
  const banDung = { phienBan: 'thu123', taiTruoc: ['/index.html', '/assets/index-a.js', '/assets/ChotBaoVe-b.js', '/assets/index-c.css'] }
  const swThu = nguonSw.replace(/^var BAN_DUNG = .*$/m, 'var BAN_DUNG = ' + JSON.stringify(banDung))
  const mayChu = { '/index.html': '<!doctype html><title>SmartPort</title>', '/assets/index-a.js': 'a', '/assets/ChotBaoVe-b.js': 'b', '/assets/index-c.css': 'c', '/offline.html': 'offline' }
  const mt = taoMoiTruongSw(mayChu)
  // Cache da co tu truoc: mo hinh AI, SW doi cu, SW MVC, 2 vo cu, cache cua phan khac
  for (const ten of ['sp-models-v1', 'smartport-react-v1', 'smartport-v1', 'smartport-react-vo-cu1', 'smartport-react-vo-cu2', 'cache-khac']) await mt.caches.open(ten)
  vm.runInContext(swThu, mt.ctx)

  const ci = suKienCho()
  mt.nghe.install(ci)
  await ci.p
  const vo = mt.kho.get('smartport-react-vo-thu123')
  kiem('install: cai san index.html + moi chunk cua ban build (vo ung dung)', !!vo && banDung.taiTruoc.every((u) => vo.has(NGUON + u)))
  kiem('install: con SW doi cu (khong co vo offline) -> thay ngay', mt.soSkip === 1)

  const ca = suKienCho()
  mt.nghe.activate(ca)
  await ca.p
  const con = [...mt.kho.keys()]
  kiem('activate: GIU sp-models-v1 (mo hinh AI)', con.includes('sp-models-v1'))
  kiem('activate: giu cache khong phai cua SW', con.includes('cache-khac'))
  kiem('activate: xoa cache SW doi cu + MVC cu', !con.includes('smartport-react-v1') && !con.includes('smartport-v1'))
  kiem('activate: giu 1 vo cu gan nhat cho tab dang mo, xoa vo cu hon', con.includes('smartport-react-vo-cu2') && !con.includes('smartport-react-vo-cu1') && con.includes('smartport-react-vo-thu123'))

  const fApi = suKienFetch('/api/cong/luot-xe?soLuong=6')
  mt.nghe.fetch(fApi)
  const fSk = suKienFetch('/suc-khoe')
  mt.nghe.fetch(fSk)
  kiem('fetch /api/* va /suc-khoe: SW khong dong vao (khong cache API)', fApi.p === null && fSk.p === null)

  mt.matMang = true
  const fNav = suKienFetch('/admin/gate/baove', 'navigate')
  mt.nghe.fetch(fNav)
  const rNav = await fNav.p
  kiem('MAT MANG + tai lai /admin/gate/baove -> tra index.html da cai (mo duoc man chot cong)', (await rNav.text()).includes('<title>SmartPort</title>'))
  const fChunk = suKienFetch('/assets/ChotBaoVe-b.js')
  mt.nghe.fetch(fChunk)
  kiem('MAT MANG: chunk lazy cua man chot cong lay tu cache', (await (await fChunk.p).text()) === 'b')
  mt.matMang = false
  mayChu['/admin/gate/baove'] = 503
  const fNav2 = suKienFetch('/admin/gate/baove', 'navigate')
  mt.nghe.fetch(fNav2)
  kiem('may chu tra 5xx khi mo trang -> van tra vo da cai', (await (await fNav2.p).text()).includes('SmartPort'))
  mayChu['/admin/gate/baove'] = '<!doctype html><title>Ban moi tu mang</title>'
  const fNav3 = suKienFetch('/admin/gate/baove', 'navigate')
  mt.nghe.fetch(fNav3)
  kiem('co mang -> mang truoc (khong tra vo cu)', (await (await fNav3.p).text()).includes('Ban moi tu mang'))

  // Lan cai sau (khong con SW doi cu): KHONG tu skipWaiting; cho nguoi dung bam "Tai lai de cap nhat"
  const mt2 = taoMoiTruongSw(mayChu)
  vm.runInContext(swThu, mt2.ctx)
  const ci2 = suKienCho()
  mt2.nghe.install(ci2)
  await ci2.p
  kiem('cap nhat an toan: ban moi KHONG tu chiem quyen', mt2.soSkip === 0)
  mt2.nghe.message({ data: { loai: 'KICH_HOAT' } })
  kiem('nguoi dung bam cap nhat (KICH_HOAT) -> skipWaiting', mt2.soSkip === 1)

  // Thieu 1 chunk tren may chu -> cai dat THAT BAI (SW cu giu vo cu nguyen ven, khong ra vo nua voi)
  const mt3 = taoMoiTruongSw({ ...mayChu, '/assets/index-c.css': undefined })
  vm.runInContext(swThu, mt3.ctx)
  const ci3 = suKienCho()
  mt3.nghe.install(ci3)
  let hong = false
  await ci3.p.catch(() => (hong = true))
  kiem('thieu 1 tep vo -> install that bai (khong cai vo thieu)', hong)

  const boChu = nguonSw.replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '')
  kiem('sw.js khong co lenh xoa nao nham sp-models', !/delete\([^)]*sp-models/.test(boChu) && !boChu.includes("'sp-models"))
}

// =====================================================================
// 12) Kiem tinh tren ma nguon giao dien (noi dung bat buoc)
// =====================================================================
{
  const doc = (p) => readFileSync(new URL(p, import.meta.url), 'utf8')
  const client = doc('../src/api/client.js')
  const chot = doc('../src/khu/cong/ChotBaoVe.jsx')
  const cong = doc('../src/pages/CongThongMinh.jsx')
  const boCuc = doc('../src/components/BoCuc.jsx')
  const vite = doc('../vite.config.js')
  kiem('client.js: co timeout + phan biet loi mang / nghiep vu', client.includes('timeoutMs') && client.includes('quaGio: true') && client.includes('export { LoiApi, laLoiMang, laLoiNghiepVu, phanLoaiLoi }'))
  kiem('client.js: tra them ETag / 304 cho ban sao', client.includes('goiApiDayDu') && client.includes('choPhep304'))
  kiem('ChotBaoVe + CongThongMinh ghi qua hop thu di (ghiLuotXeTaiTram)', chot.includes('ghiLuotXeTaiTram(') && cong.includes('ghiLuotXeTaiTram('))
  kiem('ChotBaoVe: 4xx hien DO "KHÔNG CHO QUA"', chot.includes("kqGhi.ketQua === 'bi-tu-choi'") && chot.includes("'KHÔNG CHO QUA'"))
  kiem('BoCuc hien thanh trang thai mang', boCuc.includes('<ThanhTrangThaiMang />'))
  kiem('vite.config.js: sinh danh sach vo cho sw.js + proxy /suc-khoe', vite.includes('voUngDungNgoaiTuyen()') && vite.includes("'/suc-khoe': chuyenTiep"))
}

// =====================================================================
// 13) RA SOAT DOC LAP (15/10/2026): F1–F8
// =====================================================================
const TRUONG_VUOT = [...TRUONG_MUC_H1, 'lyDoVuot', 'vuotBanSao'].sort()

// ---------------------------------------------------------------- F1: XE RA khi dang co mang ma may chu cham -> CHUA XAC NHAN
async function thuGhiRa(cheDo, { guiTrucTiep = true, canXacNhan = true, kho = taoKhoBoNho() } = {}) {
  const hopThu = taoHopThuDi({ kho })
  const mc = taoMayChuGia()
  mc.cheDo = cheDo
  const muc = mucMau('51C12345', { huongDi: 'Ra', maLenh: 12, soCont: 'MSCU1234567' })
  const kq = await ghiLuotXe({ hopThu, goi: mc.goi, muc, phu: { nguon: 'ChotCong' }, guiTrucTiep, canXacNhan, truocKhiXepHang: async () => null })
  return { kq, hopThu, mc, muc, ds: await hopThu.layTatCa() }
}
{
  const a = await thuGhiRa('qua-gio')
  kiem('F1: XE RA + co mang + may chu qua gio -> chua-xac-nhan (KHONG phai da-ghi-tam)', a.kq.ketQua === 'chua-xac-nhan' && a.kq.lyDo === 'mang')
  kiem('F1: chua-xac-nhan -> KHONG xep hang, hop thu di rong (bo dong bo khong the gui ghi tre)', a.ds.length === 0)
  kiem('F1: chua-xac-nhan tra lai muc + maThaoTac de Thu lai cung khoa', a.kq.muc?.maThaoTac === a.muc.maThaoTac && a.kq.maThaoTac === a.muc.maThaoTac)
  for (const ma of ['mat-mang', 502, 503, 504]) {
    const b = await thuGhiRa(ma)
    kiem('F1: XE RA + co mang + ' + ma + ' -> chua-xac-nhan, khong xep hang', b.kq.ketQua === 'chua-xac-nhan' && b.ds.length === 0)
  }
  const c409 = await thuGhiRa(409)
  kiem('F1: XE RA + 409 (dang xu ly cung khoa) -> chua-xac-nhan lyDo dang-xu-ly, khong xep hang', c409.kq.ketQua === 'chua-xac-nhan' && c409.kq.lyDo === 'dang-xu-ly' && c409.ds.length === 0)
  const c401 = await thuGhiRa(401)
  kiem('F1: XE RA + 401 -> chua-xac-nhan lyDo het-phien (khong ghi tre de lach kiem tra)', c401.kq.ketQua === 'chua-xac-nhan' && c401.kq.lyDo === 'het-phien' && c401.ds.length === 0)
  const c400 = await thuGhiRa(400)
  kiem('F1: XE RA + 400 KHONG CHO RA CONG -> van bi-tu-choi (do)', c400.kq.ketQua === 'bi-tu-choi' && c400.ds.length === 0)
  const cok = await thuGhiRa('ok')
  kiem('F1: XE RA + may chu tra loi -> da-ghi', cok.kq.ketQua === 'da-ghi' && cok.ds.length === 0)
  const xv = await thuGhiRa('qua-gio', { canXacNhan: false })
  kiem('F1: XE VAO (canXacNhan=false) + qua gio -> giu hanh vi cu: da-ghi-tam', xv.kq.ketQua === 'da-ghi-tam' && xv.ds.length === 1)
  const mm = await thuGhiRa('ok', { guiTrucTiep: false })
  kiem('F1: nhip DA bao mat mang -> giu hanh vi cu: ghi tam ngay, khong goi may chu', mm.kq.ketQua === 'da-ghi-tam' && mm.mc.yeuCau.length === 0 && mm.ds.length === 1)

  // Che do xac nhan KHONG ghi truoc (write-ahead) — tab chet giua chung khong de lai muc 'dang-gui' cho bo dong bo gui tre
  const goc = taoKhoBoNho()
  const thuTu = []
  const khoTheoDoi = { ...goc, ghi: async (k, bg) => (thuTu.push('ghi:' + k), goc.ghi(k, bg)) }
  const hopThu = taoHopThuDi({ kho: khoTheoDoi })
  const muc = mucMau('51D11111', { huongDi: 'Ra' })
  ghiLuotXe({ hopThu, goi: () => new Promise(() => {}), muc, canXacNhan: true }) // yeu cau treo: tab "chet"
  await xaHet()
  await xaHet()
  kiem('F1: XE RA cho xac nhan -> KHONG ghi truoc vao hop thu di (khong co muc dang-gui de gui tre)', thuTu.length === 0 && (await hopThu.layTatCa()).length === 0)

  // Thu lai: lan 1 may chu DA ghi nhung phan hoi rot -> chua-xac-nhan; Thu lai CUNG maThaoTac -> may chu tra ket qua cu
  const kho = taoKhoBoNho()
  const h2 = taoHopThuDi({ kho })
  const mc = taoMayChuGia()
  const muc2 = mucMau('51C22222', { huongDi: 'Ra' })
  mc.cheDo = 'mat-phan-hoi'
  const k1 = await ghiLuotXe({ hopThu: h2, goi: mc.goi, muc: muc2, canXacNhan: true })
  mc.cheDo = 'ok'
  const k2 = await ghiLuotXe({ hopThu: h2, goi: mc.goi, muc: k1.muc, canXacNhan: true })
  const khoaGui = mc.yeuCau.map((y) => y.tuyChon.headers['Idempotency-Key'])
  kiem('F1: Thu lai gui lai DUNG Idempotency-Key cu', khoaGui.length === 2 && khoaGui[0] === khoaGui[1] && khoaGui[0] === muc2.maThaoTac)
  kiem('F1: Thu lai sau phan hoi rot -> da-ghi bang ket qua cu, may chu chi 1 luot', k1.ketQua === 'chua-xac-nhan' && k2.ketQua === 'da-ghi' && k2.duLieu?.daCo === true && mc.daGhi.size === 1)
  mc.cheDo = 400
  const k3 = await ghiLuotXe({ hopThu: h2, goi: mc.goi, muc: mucMau('51C33333', { huongDi: 'Ra' }), canXacNhan: true })
  kiem('F1: Thu lai ma may chu chan (400) -> bi-tu-choi', k3.ketQua === 'bi-tu-choi')

  // Van cho qua: xep hang muc cu (cung khoa) + vuotBanSao + lyDoVuot -> /dong-bo
  const t = taoBo()
  t.mc.cheDo = 'qua-gio'
  const mucRa = mucMau('51C44444', { huongDi: 'Ra', maLenh: 7 })
  const r1 = await ghiLuotXe({ hopThu: t.hopThu, goi: t.mc.goi, muc: mucRa, canXacNhan: true })
  const mucVuot = ganVuot(r1.muc, LY_DO_VUOT_MAY_CHU_CHAM)
  const r2 = await ghiLuotXe({ hopThu: t.hopThu, goi: t.mc.goi, muc: mucVuot, guiTrucTiep: false })
  t.mc.cheDo = 'ok'
  const kd = await t.bo.chayMotLan()
  const itVuot = t.mc.yeuCau.filter((y) => y.duong === '/api/cong/dong-bo').flatMap((y) => y.tuyChon.body.items)[0]
  kiem('F1: Van cho qua -> da-ghi-tam (xep hang) roi dong bo', r1.ketQua === 'chua-xac-nhan' && r2.ketQua === 'da-ghi-tam' && kd.daGui === 1)
  kiem('F1: muc Van cho qua gui vuotBanSao=true + lyDoVuot dung ly do tram bao, CUNG maThaoTac', itVuot?.vuotBanSao === true && itVuot?.lyDoVuot === LY_DO_VUOT_MAY_CHU_CHAM && itVuot?.maThaoTac === mucRa.maThaoTac)
  kiem('F1: muc vuot = du truong H1 + vuotBanSao + lyDoVuot', JSON.stringify(Object.keys(itVuot || {}).sort()) === JSON.stringify(TRUONG_VUOT))
  kiem('F1: ly do vuot <= 300 ky tu', ganVuot(mucRa, 'x'.repeat(500)).lyDoVuot.length === 300 && LY_DO_VUOT_MAY_CHU_CHAM.length <= 300)
  kiem('F1: muc thuong KHONG mang vuotBanSao / lyDoVuot', !('vuotBanSao' in mucRa) && !('lyDoVuot' in mucRa))
  const mucTaoVuot = taoMucLuotXe({ bienSo: '51C55555', huongDi: 'Ra', vuotBanSao: true, lyDoVuot: 'Bảo vệ cho qua dù bản sao báo: eDO hết hạn.' })
  kiem('F1/F2: taoMucLuotXe(vuotBanSao) gan co vuot (khong chi ghi vao ghiChu)', mucTaoVuot.vuotBanSao === true && mucTaoVuot.lyDoVuot.includes('eDO hết hạn'))
}

// ---------------------------------------------------------------- F2: ban sao "fail-open"
{
  const goc = {
    duLieu: {
      lenhMo: [{ maLenh: 12, soLenh: 'GN-012', loaiLenh: 'GiaoCont', bienSo: '51C-123.45', soCont: 'MSCU1234567', trangThai: 'Đã vào cổng' }],
      edo: [],
      haiQuan: [],
    },
  }
  const voi = (cheDoKiemTra, them = {}) => ({ duLieu: { ...goc.duLieu, cheDoKiemTra, ...them } })
  const yc = { bienSo: '51C12345', huongDi: 'Ra', bayGio: Date.UTC(2026, 9, 14, 2, 0, 0) }
  const r1 = doiChieuBanSao(voi({ edo: 'chan', haiQuan: 'tat' }), yc)
  kiem('F2: edo=chan + ban sao KHONG co eDO cho GiaoCont ra -> CHAN (mac dinh khong cho qua)', !!r1.chan && r1.chan.includes('eDO') && r1.chan.includes('MSCU1234567'))
  const r2 = doiChieuBanSao(voi({ edo: 'canh-bao', haiQuan: 'tat' }), yc)
  kiem('F2: edo=canh-bao + thieu eDO -> chi canh bao nhu cu', !r2.chan && r2.canhBao.some((c) => c.includes('eDO')))
  const r3 = doiChieuBanSao(voi(null), yc)
  kiem('F2: may chu cu (khong co cheDoKiemTra) + thieu eDO -> chi canh bao nhu cu', !r3.chan && r3.canhBao.some((c) => c.includes('eDO')))
  const r4 = doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'chan' }), yc)
  kiem('F2: haiQuan=chan + ban sao KHONG co to khai -> CHAN', !!r4.chan && r4.chan.includes('tờ khai'))
  const r5 = doiChieuBanSao(voi({ edo: 'tat', haiQuan: 'canh-bao' }), yc)
  kiem('F2: haiQuan=canh-bao + thieu to khai -> chi canh bao', !r5.chan && r5.canhBao.some((c) => c.includes('tờ khai')))
  const r6 = doiChieuBanSao(voi({ edo: 'chan', haiQuan: 'chan' }, { edo: [{ soCont: 'MSCU1234567', soEdo: 'E1', trangThai: 'Hiệu lực', hieuLucDen: '2026-10-20T00:00:00Z' }], haiQuan: [{ soCont: 'MSCU1234567', trangThai: 'Đã thông quan', daThongQuan: true }] }), yc)
  kiem('F2: eDO hieu luc + da thong quan -> cho qua', !r6.chan)
  const r7 = doiChieuBanSao(voi({ edo: 'chan', haiQuan: 'chan' }), { ...yc, huongDi: 'Vào', bienSo: '51C12345' })
  kiem('F2: xe VAO khong bi kiem eDO / to khai', !r7.chan)
}

// ---------------------------------------------------------------- F3: chuyen hang doi cu — khoa tat dinh, khoa giua tab, xoa tung muc
{
  // Vector tinh bang CHINH cong thuc C# CongController.KhoaSuyRa (dotnet, System.Text.Json doc "…Z" -> Kind=Utc)
  const v1 = { bienSo: '51C-638.72', huongDi: 'Vào', cong: 'A1', luc: '2026-10-14T07:41:10.123Z' }
  const v2 = { bienSo: ' 29c46828 ', huongDi: 'Ra', cong: ' a1 ', luc: '2026-09-30T23:59:59.999Z' }
  kiem('F3: khoa suy ra khop may chu C# (Web Crypto)', (await khoaSuyRaLo(v1)) === 'lo-cb46a3923b8f09f91c0150c491bc1ad203dd80a9')
  kiem('F3: khoa suy ra khop may chu C# (SHA-256 thuan JS — http://<IP-LAN> khong co crypto.subtle)', (await khoaSuyRaLo(v2, { subtle: null })) === 'lo-ab43e10b7473652f67f5a0673acda88c0ce866ea')
  kiem('F3: UtcTicks dung BigInt (khong mat chinh xac > 2^53)', nguonKhoaSuyRa(v1).endsWith('|639275604701230000'))
  let shaDung = true
  for (let n = 0; n < 200; n += 7) {
    const b = randomBytes(n)
    if (Buffer.from(sha256ThuanJs(new Uint8Array(b))).toString('hex') !== createHash('sha256').update(b).digest('hex')) shaDung = false
  }
  kiem('F3: SHA-256 thuan JS khop node:crypto', shaDung && (await bamSha256Hex('Vào', { subtle: null })) === createHash('sha256').update('Vào').digest('hex'))

  const cu = [
    { bienSo: '51C63872', huongDi: 'Vào', cong: 'A1', luc: '2026-10-14T07:41:10.123Z' },
    { bienSo: '51C63872', huongDi: 'Ra', cong: 'A1', luc: '2026-10-14T09:02:00.000Z' },
    { bienSo: '29C46828', huongDi: 'Vào', cong: 'A1', luc: '2026-10-14T09:05:30.500Z' },
  ]
  // 2 tab cung mo (lease localStorage dung chung, IndexedDB dung chung)
  const lt = taoLuuTruGia()
  lt.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu))
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho })
  const [ka, kb] = await Promise.all(
    ['tab-A', 'tab-B'].map((maTab) => chuyenHangDoiCu({ luuTru: lt, hopThu, khoa: taoKhoaGiuaTab({ luuTru: lt, maTab, choXacNhanMs: 15 }) })),
  )
  const ds = await hopThu.layTatCa()
  kiem('F3: 2 tab cung chuyen -> chi 1 tab chay (khoa giua tab)', [ka.daChay, kb.daChay].filter(Boolean).length === 1)
  kiem('F3: moi luot cu vao hop thu di DUNG 1 lan', ds.length === 3 && new Set(ds.map((x) => x.maThaoTac)).size === 3)
  const khoaMongDoi = await Promise.all(cu.map((x) => khoaSuyRaLo(x)))
  kiem('F3: maThaoTac = khoa may chu suy ra cho dong-bo-lo (khong ngau nhien)', ds.every((x) => khoaMongDoi.includes(x.maThaoTac)))
  kiem('F3: giu gio xe qua cong that (lucXayRa = luc cu)', ds.some((x) => x.duLieu.lucXayRa === '2026-10-14T09:05:30.500Z'))
  kiem('F3: chuyen xong -> xoa hang doi cu', lt.getItem(KHOA_HANG_DOI_CU) === null)

  // Muc cu DA toi may chu qua dong-bo-lo (mat phan hoi) -> /dong-bo tra "trung", khong ghi kep
  const t = taoBo({ kho })
  t.mc.daGhi.set(khoaMongDoi[0], 555)
  const kd = await t.bo.chayMotLan()
  kiem('F3: muc da gui qua dong-bo-lo -> may chu tra "trung", tong 3 luot (khong kep)', kd.trung === 1 && kd.daGui === 2 && t.mc.daGhi.size === 3)

  // Chay lai (tab khac / tai lai trang) sau khi da chuyen: khong them lan 2
  const lt2 = taoLuuTruGia()
  lt2.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu))
  const kho2 = taoKhoBoNho()
  const h2 = taoHopThuDi({ kho: kho2 })
  await chuyenHangDoiCu({ luuTru: lt2, hopThu: h2, khoa: null })
  lt2.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu)) // gia lap: chet sau khi ghi hop thu di, truoc khi xoa localStorage
  const lai = await chuyenHangDoiCu({ luuTru: lt2, hopThu: h2, khoa: null })
  kiem('F3: chay lai -> them-neu-chua-co, khong nhan doi', lai.soChuyen === 0 && lai.soDaCo === 3 && (await h2.layTatCa()).length === 3 && lt2.getItem(KHOA_HANG_DOI_CU) === null)

  // Ghi hop thu di hong o muc thu 2 -> muc 1 da xoa khoi localStorage, muc 2-3 CON NGUYEN (xoa tung muc)
  const lt3 = taoLuuTruGia()
  lt3.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu))
  const goc3 = taoKhoBoNho()
  let soGhi = 0
  const kho3 = { ...goc3, capNhat: async (...a) => (++soGhi === 2 ? Promise.reject(new Error('QuotaExceededError')) : goc3.capNhat(...a)) }
  let nem = false
  await chuyenHangDoiCu({ luuTru: lt3, hopThu: taoHopThuDi({ kho: kho3 }), khoa: null }).catch(() => (nem = true))
  const conCu = JSON.parse(lt3.getItem(KHOA_HANG_DOI_CU) || '[]')
  kiem('F3: hong giua chung -> chi xoa muc DA an toan, phan con lai giu nguyen', nem && conCu.length === 2 && conCu[0].luc === cu[1].luc && (await goc3.tatCa('hopThuDi')).length === 1)

  // Tab client cu (con mo) them muc moi trong luc chuyen -> khong bi mat
  const lt4 = taoLuuTruGia()
  lt4.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu.slice(0, 1)))
  const goc4 = taoKhoBoNho()
  const moiCuaTabCu = { bienSo: '15C27484', huongDi: 'Vào', cong: 'A1', luc: '2026-10-14T10:00:00.000Z' }
  const kho4 = {
    ...goc4,
    capNhat: async (...a) => {
      const r = await goc4.capNhat(...a)
      const dsLt = JSON.parse(lt4.getItem(KHOA_HANG_DOI_CU) || '[]')
      if (!dsLt.some((x) => x.bienSo === moiCuaTabCu.bienSo)) lt4.setItem(KHOA_HANG_DOI_CU, JSON.stringify([...dsLt, moiCuaTabCu]))
      return r
    },
  }
  await chuyenHangDoiCu({ luuTru: lt4, hopThu: taoHopThuDi({ kho: kho4 }), khoa: null })
  const con4 = JSON.parse(lt4.getItem(KHOA_HANG_DOI_CU) || '[]')
  kiem('F3: muc tab cu vua them (khong nam trong lan doc) KHONG bi xoa', con4.length === 1 && con4[0].bienSo === '15C27484')

  // Tab khac dang giu khoa -> khong lam gi, localStorage nguyen
  const lt5 = taoLuuTruGia()
  lt5.setItem(KHOA_HANG_DOI_CU, JSON.stringify(cu))
  const k5 = await chuyenHangDoiCu({ luuTru: lt5, hopThu: taoHopThuDi({ kho: taoKhoBoNho() }), khoa: { chayNeuRanh: async () => ({ daChay: false }) } })
  kiem('F3: khoa dang bi tab khac giu -> bo qua, khong dong vao hang doi cu', !k5.daChay && JSON.parse(lt5.getItem(KHOA_HANG_DOI_CU)).length === 3)
  const srcTram = readFileSync(new URL('tram.js', GOC), 'utf8')
  kiem('F3: tram.js chuyen hang doi cu qua hangDoiCu.js trong khoa giua tab', srcTram.includes('chuyenHangDoiCu({ luuTru, hopThu, khoa })') && !srcTram.replace(/\/\/.*$/gm, '').includes("'sp_chotcong_hangdoi'"))
}

// ---------------------------------------------------------------- F4: Wi-Fi yeu — cho ti le dung luong, thu nho lo khi het gio
{
  kiem('F4: thoi gian cho lo nho = 30 s', thoiGianChoLo(2000) >= 30000 && thoiGianChoLo(2000) < 31000)
  kiem('F4: lo 3 MB duoc cho lau hon (≈150 s), tran 170 s (< 180 s gateway)', thoiGianChoLo(3_000_000) === 150000 && thoiGianChoLo(10_000_000) === 170000 && CHO_TOI_DA_LO_MS === 170000)

  // Wi-Fi yeu: lo > 6 muc luon het gio
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho })
  const mc = taoMayChuGia()
  const kichThuoc = []
  const goiCham = async (duong, tc) => {
    kichThuoc.push(tc.body.items.length)
    if (tc.body.items.length > 6) throw new LoiApi(0, 'Máy chủ không trả lời sau 30 giây.', null, { quaGio: true })
    return mc.goi(duong, tc)
  }
  for (let i = 0; i < 50; i++) await hopThu.them(mucMau('52A' + String(i).padStart(5, '0')))
  const bo = taoBoDongBo({ hopThu, goi: goiCham, khoa: taoKhoaGiuaTab({}), maThietBi: 't', soMucMoiLo: 50 })
  let lan = 0
  while ((await hopThu.layCanGui()).length && lan++ < 40) await bo.chayMotLan()
  kiem('F4: het gio -> lan sau nua lo: 50 -> 25 -> 12 -> 6', kichThuoc.slice(0, 4).join(',') === '50,25,12,6')
  kiem('F4: gui duoc -> noi dan (6 thanh cong -> thu 12)', kichThuoc[4] === 12)
  kiem('F4: Wi-Fi yeu van gui HET hang doi, moi luot 1 lan', (await hopThu.layTatCa()).length === 0 && mc.daGhi.size === 50)

  // Lo co anh: het gio -> muc co anh gui RIENG
  const h2 = taoHopThuDi({ kho: taoKhoBoNho() })
  const mc2 = taoMayChuGia()
  const kt2 = []
  for (let i = 0; i < 4; i++) await h2.them(mucMau('53A0000' + i, { anh: 'data:image/jpeg;base64,' + 'A'.repeat(200_000) }))
  const bo2 = taoBoDongBo({
    hopThu: h2,
    goi: async (d, tc) => {
      kt2.push({ n: tc.body.items.length, cho: tc.timeoutMs })
      if (tc.body.items.length > 1) throw new LoiApi(504, 'Gateway hết giờ', { nguon: 'gateway' })
      return mc2.goi(d, tc)
    },
    khoa: taoKhoaGiuaTab({}),
    maThietBi: 't',
  })
  lan = 0
  while ((await h2.layCanGui()).length && lan++ < 20) await bo2.chayMotLan()
  kiem('F4: lo co anh het gio (504) -> anh gui rieng tung muc, gui het', kt2.map((x) => x.n).join(',') === '4,1,1,1,1' && mc2.daGhi.size === 4)
  kiem('F4: lo 800 KB duoc cho > 30 s (ti le dung luong), <= 170 s', kt2[0].cho > 30000 && kt2[0].cho <= 170000)

  // 1 muc van qua cham ca khi gui 1 minh -> hoan, muc khac van di (khong ket hang)
  const h3 = taoHopThuDi({ kho: taoKhoBoNho() })
  const mc3 = taoMayChuGia()
  await h3.them(mucMau('54ATO001', { anh: 'data:image/jpeg;base64,' + 'B'.repeat(500_000) }))
  await h3.them(mucMau('54ANHO01'))
  const bo3 = taoBoDongBo({
    hopThu: h3,
    goi: async (d, tc) => {
      if (tc.body.items.some((x) => x.bienSo === '54ATO001')) throw new LoiApi(0, 'het gio', null, { quaGio: true })
      return mc3.goi(d, tc)
    },
    khoa: taoKhoaGiuaTab({}),
    maThietBi: 't',
  })
  for (let i = 0; i < 4; i++) await bo3.chayMotLan()
  const con3 = await h3.layTatCa()
  kiem('F4: muc le qua cham -> hoan, muc sau no van gui duoc', con3.length === 1 && con3[0].duLieu.bienSo === '54ATO001' && con3[0].trangThai === 'cho' && mc3.daGhi.size === 1 && bo3.gioiHan.soHoan === 1)

  // 413 (lo qua lon) nhieu muc -> thu nho, KHONG danh loi
  const h4 = taoHopThuDi({ kho: taoKhoBoNho() })
  for (let i = 0; i < 4; i++) await h4.them(mucMau('55A0000' + i))
  const bo4 = taoBoDongBo({ hopThu: h4, goi: async () => Promise.reject(new LoiApi(413, 'File quá lớn')), khoa: taoKhoaGiuaTab({}), maThietBi: 't' })
  const k4 = await bo4.chayMotLan()
  kiem('F4: 413 lo nhieu muc -> giu nguyen (khong loi vinh vien), thu nho lo', k4.trangThai === 'loi-may-chu' && (await h4.thongKe()).soLoi === 0 && bo4.gioiHan.tranMuc === 2)
  const h5 = taoHopThuDi({ kho: taoKhoBoNho() })
  await h5.them(mucMau('55B00001'))
  const bo5 = taoBoDongBo({ hopThu: h5, goi: async () => Promise.reject(new LoiApi(413, 'File quá lớn')), khoa: taoKhoaGiuaTab({}), maThietBi: 't' })
  await bo5.chayMotLan()
  kiem('F4: 413 khi lo chi con 1 muc -> danh loi (hien do) nhu cu, khong hoan mai mai', (await h5.thongKe()).soLoi === 1)
}

// ---------------------------------------------------------------- F5: Idempotency-Key cho bieu mau tao du lieu
function taoMayChuChongLap() {
  const daLuu = new Map()
  const mc = {
    soBanGhi: 0,
    goiDen: [],
    hanhVi: [], // moi lan goi lay 1: 'ok' | 'mat-mang' | 'qua-gio' | 'mat-phan-hoi' | 409 | 422
    async goi(duong, tc) {
      const k = tc.headers?.['Idempotency-Key']
      mc.goiDen.push({ duong, khoa: k, body: structuredClone(tc.body ?? null) })
      const hv = mc.hanhVi.shift() || 'ok'
      if (hv === 'mat-mang') throw new LoiApi(0, 'Không kết nối được máy chủ SmartPort.')
      if (hv === 'qua-gio') throw new LoiApi(0, 'Máy chủ không trả lời sau 45 giây.', null, { quaGio: true })
      if (typeof hv === 'number') throw new LoiApi(hv, 'Lỗi ' + hv)
      if (k && daLuu.has(k)) return { ...daLuu.get(k), phatLai: true }
      const kq = { id: ++mc.soBanGhi }
      if (k) daLuu.set(k, kq)
      if (hv === 'mat-phan-hoi') throw new LoiApi(0, 'Mất kết nối khi đang nhận dữ liệu từ máy chủ.')
      return kq
    },
  }
  return mc
}
{
  const khongCho = { ngu: async () => {} }
  const mc = taoMayChuChongLap()
  const bk = taoBoKhoaGuiLap()
  const than = { soCont: 'MSCU1234567', soNgayHieuLuc: 5 }
  mc.hanhVi = ['mat-phan-hoi', 'mat-mang']
  let loi1 = null
  await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: than }, { boKhoa: bk, ...khongCho }).catch((e) => (loi1 = e))
  kiem('F5: loi mang -> tu thu lai 1 lan CUNG khoa roi bao "bấm lại — không tạo trùng"', mc.goiDen.length === 2 && mc.goiDen[0].khoa === mc.goiDen[1].khoa && /không tạo trùng/.test(loi1?.message || ''))
  const kq = await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: { ...than } }, { boKhoa: bk, ...khongCho })
  kiem('F5: bam lai cung noi dung -> cung khoa, may chu phat lai ket qua cu (1 eDO, khong 2)', kq.phatLai === true && kq.id === 1 && mc.soBanGhi === 1 && mc.goiDen[2].khoa === mc.goiDen[0].khoa)
  await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: than }, { boKhoa: bk, ...khongCho })
  kiem('F5: thanh cong -> lan gui sau (ke ca y het) dung khoa MOI', mc.goiDen[3].khoa !== mc.goiDen[0].khoa && mc.soBanGhi === 2)
  mc.hanhVi = ['qua-gio']
  await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: { soCont: 'A' } }, { boKhoa: bk, ...khongCho }).catch(() => {})
  const khoaA = mc.goiDen[mc.goiDen.length - 1].khoa
  kiem('F5: qua gio (da cho lau) -> KHONG tu thu lai', mc.goiDen.filter((g) => g.khoa === khoaA).length === 1)
  await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: { soCont: 'B' } }, { boKhoa: bk, ...khongCho })
  const khoaB = mc.goiDen[mc.goiDen.length - 1].khoa
  kiem('F5: nguoi dung sua noi dung -> khoa MOI (tranh 422 cung khoa khac than)', khoaB !== khoaA)
  await goiChongGuiLap(mc.goi, '/api/edo', { method: 'POST', body: { soCont: 'A' } }, { boKhoa: bk, ...khongCho })
  kiem('F5: sua roi quay lai noi dung chua xac nhan -> dung lai khoa cu cua noi dung do', mc.goiDen[mc.goiDen.length - 1].khoa === khoaA)
  mc.hanhVi = [409, 409]
  let loi409 = null
  await goiChongGuiLap(mc.goi, '/api/lich-hen', { method: 'POST', body: { bienSo: '51C' } }, { boKhoa: bk, ...khongCho }).catch((e) => (loi409 = e))
  kiem('F5: 409 -> hoi lai 1 lan cung khoa, van 409 thi bao "đang xử lý"', loi409?.trangThai === 409 && /đang xử lý/.test(loi409.message) && mc.goiDen.slice(-2).every((g) => g.khoa === mc.goiDen[mc.goiDen.length - 1].khoa))
  mc.hanhVi = [422]
  await goiChongGuiLap(mc.goi, '/api/lich-hen', { method: 'POST', body: { bienSo: '51D' } }, { boKhoa: bk, ...khongCho }).catch(() => {})
  const k422 = mc.goiDen[mc.goiDen.length - 1].khoa
  await goiChongGuiLap(mc.goi, '/api/lich-hen', { method: 'POST', body: { bienSo: '51D' } }, { boKhoa: bk, ...khongCho })
  kiem('F5: 422 -> bo khoa, lan bam sau khoa moi', mc.goiDen[mc.goiDen.length - 1].khoa !== k422)
  const put1 = taoBoKhoaGuiLap()
  kiem('F5: PUT nap the xe khac the -> khoa khac', put1.lay('PUT', '/api/phi-cong/the-xe/1', { napThem: 100000 }) !== put1.lay('PUT', '/api/phi-cong/the-xe/2', { napThem: 100000 }))
  kiem('F5: khoa la UUID (<= 64 ky tu, bo loc may chu)', UUID.test(put1.lay('POST', '/x', {})))

  const doc = (p) => readFileSync(new URL(p, import.meta.url), 'utf8')
  const client = doc('../src/api/client.js')
  kiem('F5: client.js co tuy chon chongGuiLap (Idempotency-Key)', client.includes('chongGuiLap') && client.includes('goiChongGuiLap(goiApi'))
  const trang = {
    'Edo.jsx (phat hanh eDO)': ['../src/khu/cang-so/Edo.jsx', "'/api/edo', {"],
    'HaiQuan.jsx (khai hai quan)': ['../src/khu/cang-so/HaiQuan.jsx', "'/api/hai-quan', {"],
    'LichHenCong.jsx (dat lich hen)': ['../src/khu/cang-so/LichHenCong.jsx', "'/api/lich-hen', {"],
    'FormLenh.jsx (tao lenh)': ['../src/khu/cong/FormLenh.jsx', "'/api/lenh-giao-nhan', {"],
    'TheXe.jsx (cap / sua / nap the)': ['../src/khu/tai-chinh/TheXe.jsx', "'/api/phi-cong/the-xe', {"],
  }
  for (const [ten, [p, duong]] of Object.entries(trang)) {
    const s = doc(p)
    const i = s.indexOf(duong)
    kiem('F5: ' + ten + ' gui kem chongGuiLap', i >= 0 && s.slice(i, i + 600).includes('chongGuiLap: khoaGui') && s.includes('useState(taoBoKhoaGuiLap)'))
  }
  kiem('F5: TheXe sua / nap the (PUT) cung gui khoa', doc('../src/khu/tai-chinh/TheXe.jsx').includes("method: 'PUT', body: { ...than, kichHoat: gt.kichHoat }, chongGuiLap: khoaGui"))
}

// ---------------------------------------------------------------- F6: dang xuat xoa ban sao; 401/403 khong danh loi vinh vien
{
  // Lo bi 403 (tai khoan khac, khong co quyen chot cong) / 401 -> giu nguyen 'cho'
  const a = taoBo()
  for (let i = 0; i < 3; i++) await a.hopThu.them(mucMau('56A0000' + i))
  a.mc.cheDo = 403
  const k403 = await a.bo.chayMotLan()
  kiem('F6: lo 403 -> khong-quyen, 3 muc van "cho" (KHONG thanh loi)', k403.trangThai === 'khong-quyen' && (await a.hopThu.thongKe()).soLoi === 0 && (await a.hopThu.layCanGui()).length === 3)

  // Dieu phoi: 403 -> cho dang nhap, ngung tu gui toi khi DOI phien
  const dh = taoDongHoGia()
  const kho = taoKhoBoNho()
  const hopThu = taoHopThuDi({ kho, dongHo: dh.dongHo })
  await hopThu.them(mucMau('56B00001'))
  await hopThu.them(mucMau('56B00002'))
  let token = 'token-thu-ngan'
  let soGoi = 0
  let traVe = 'khong-quyen'
  const dv = taoDichVuNgoaiTuyen({
    hopThu,
    boDongBo: {
      chayMotLan: async () => {
        soGoi++
        if (traVe === 'khong-quyen') return { trangThai: 'khong-quyen', soLo: 0, loi: new LoiApi(403, 'Tài khoản không có quyền dùng chức năng này.') }
        for (const x of await hopThu.layCanGui()) await hopThu.xoa(x.maThaoTac)
        return { trangThai: 'xong', soLo: 1, daGui: 2, trung: 0, canDoiSoat: 0, loi: 0 }
      },
    },
    banSao: { tomTat: async () => null, taiVe: async () => ({}) },
    kiemSucKhoe: async () => ({ coMang: true, mayChuOn: true }),
    layMaPhien: () => token,
    dongHo: dh.dongHo,
    henGio: dh.henGio,
    huyHen: dh.huyHen,
  })
  const go = dv.dangKy(() => {})
  await dh.chay(0)
  await dh.chay(0)
  const tt = dv.layTrangThai()
  kiem('F6: 403 khi gui -> choDangNhap + khongQuyen, muc giu nguyen', soGoi === 1 && tt.choDangNhap === true && tt.khongQuyen === true && tt.soCho === 2)
  kiem('F6: thanh trang thai bao "cần đăng nhập tài khoản có quyền chốt cổng để gửi 2 lượt"', moTaTrangThai(tt).chu.includes('cần đăng nhập tài khoản có quyền chốt cổng để gửi 2 lượt'))
  await dh.chay(5000)
  await dh.chay(5000)
  kiem('F6: cung phien -> khong lap lai 403 moi nhip', soGoi === 1)
  token = 'token-bao-ve'
  traVe = 'xong'
  await dh.chay(5000)
  kiem('F6: dang nhap tai khoan co quyen (doi phien) -> tu gui, het cho dang nhap', soGoi === 2 && dv.layTrangThai().soCho === 0 && dv.layTrangThai().choDangNhap === false)
  go()
  await dh.chay(2500)

  // Dang xuat: xoa ban sao (moi cong), GIU hop thu di + nhat ky
  const khoX = taoKhoBoNho()
  const hX = taoHopThuDi({ kho: khoX })
  await hX.them(mucMau('56C00001'))
  await khoX.ghi('nhatKy', { maThaoTac: 'nk-1', trangThai: 'can-doi-soat', luc: 1 })
  const traMau = async () => ({ trangThai: 200, etag: '"v1"', duLieu: { lenhMo: [{ maLenh: 1, bienSo: '51C1' }], theXe: [{ bienSo: '51C1', conHan: true }] } })
  const bsX = taoBanSao({ kho: khoX, goiDayDu: traMau })
  await bsX.taiVe('A1')
  await bsX.taiVe('B1')
  await bsX.xoaHet()
  kiem('F6: dang xuat -> xoa HET ban sao lenh / the xe (moi cong)', (await khoX.tatCa('banSao')).length === 0 && (await bsX.tomTat()) === null)
  kiem('F6: dang xuat -> GIU hop thu di + nhat ky (xe da qua that)', (await hX.layTatCa()).length === 1 && (await khoX.tatCa('nhatKy')).length === 1)
  // Lan tai dang bay (token cu) ve SAU khi dang xuat -> bo, khong ghi lai ban sao
  let mo
  const bsY = taoBanSao({ kho: khoX, goiDayDu: () => new Promise((ok) => (mo = ok)) })
  const dangTai = bsY.taiVe('A1')
  await xaHet()
  await bsY.xoaHet()
  mo({ trangThai: 200, etag: '"v2"', duLieu: { lenhMo: [{ maLenh: 2 }] } })
  const rY = await dangTai
  kiem('F6: ban sao tai ve sau khi dang xuat -> bo (khong ghi lai du lieu nhay cam)', rY.boQua === true && (await khoX.tatCa('banSao')).length === 0)
  // dichVu.xoaBanSao
  let daXoa = 0
  const dvX = taoDichVuNgoaiTuyen({ hopThu: hX, boDongBo: { chayMotLan: async () => ({ trangThai: 'xong', soLo: 0 }) }, banSao: { tomTat: async () => null, xoaHet: async () => void daXoa++ }, kiemSucKhoe: async () => ({ coMang: true }) })
  await dvX.xoaBanSao()
  kiem('F6: dich vu xoaBanSao -> goi banSao.xoaHet, thanh trang thai het "ban sao luc"', daXoa === 1 && dvX.layTrangThai().banSao === null)
  const auth = readFileSync(new URL('../src/auth/AuthContext.jsx', import.meta.url), 'utf8')
  const iDx = auth.indexOf('const dangXuat = useCallback(')
  kiem('F6: AuthContext.dangXuat xoa ban sao tram (xoaDuLieuNhayCamKhiDangXuat), khong dong vao hop thu di', iDx > 0 && auth.slice(iDx, iDx + 700).includes('xoaDuLieuNhayCamKhiDangXuat()') && !auth.includes('hopThu'))
}

// ---------------------------------------------------------------- F7: kho tu chon — IndexedDB loi thoang qua
function taoIdbGia() {
  const du = taoKhoBoNho()
  const g = { soLanHong: 0, soLanMo: 0 }
  g.tao = () => ({
    ...du,
    loai: 'indexeddb',
    dem: async (k) => {
      g.soLanMo++
      if (g.soLanHong > 0) {
        g.soLanHong--
        throw new Error('InvalidStateError: thoang qua')
      }
      return du.dem(k)
    },
  })
  g.du = du
  return g
}
{
  const khongCho = async () => {}
  // 1) Loi 2 lan roi mo duoc -> van chon IndexedDB (truoc day: ca tab chay localStorage)
  const g1 = taoIdbGia()
  g1.soLanHong = 2
  const k1 = taoKhoTuDong({ idb: {}, luuTru: taoLuuTruGia(), taoKhoIdb: g1.tao, ngu: khongCho })
  kiem('F7: IndexedDB loi thoang qua 2 lan -> thu lai, van dung IndexedDB', (await k1.layLoai()) === 'indexeddb' && g1.soLanMo === 3)

  // 2) Hong ca 3 lan -> localStorage; IndexedDB song lai -> CHUYEN muc sang, doi kho
  let gio = 1_000_000
  const g2 = taoIdbGia()
  g2.soLanHong = 3
  const lt = taoLuuTruGia()
  const loaiDoi = []
  const k2 = taoKhoTuDong({ idb: {}, luuTru: lt, taoKhoIdb: g2.tao, ngu: khongCho, dongHo: () => gio, chuKyThuLaiMs: 30000, khiDoiLoai: (l) => loaiDoi.push(l) })
  const h2 = taoHopThuDi({ kho: k2 })
  await h2.them(mucMau('57A00001'))
  kiem('F7: hong han -> du phong localStorage', (await k2.layLoai()) === 'local-storage' && JSON.parse(lt.getItem('sp-tram:hopThuDi') || '{}') && Object.keys(JSON.parse(lt.getItem('sp-tram:hopThuDi'))).length === 1)
  gio += 31000
  await k2.dem('hopThuDi') // thao tac ke tiep sau chu ky -> thu lai IndexedDB (khong chan thao tac nay)
  for (let i = 0; i < 10; i++) await xaHet()
  kiem('F7: IndexedDB song lai -> tu chuyen sang IndexedDB', (await k2.layLoai()) === 'indexeddb' && loaiDoi.join(',') === 'local-storage,indexeddb')
  kiem('F7: muc ghi trong localStorage duoc CHUYEN sang IndexedDB (khong mo coi), localStorage het muc', (await g2.du.tatCa('hopThuDi')).length === 1 && Object.keys(JSON.parse(lt.getItem('sp-tram:hopThuDi') || '{}')).length === 0)
  kiem('F7: sau khi chuyen, hop thu di doc thay muc', (await h2.layCanGui()).length === 1)

  // 3) Mo trang: con muc MO COI trong localStorage tu lan du phong truoc -> don sang IndexedDB; ban IndexedDB thang
  const g3 = taoIdbGia()
  const lt3 = taoLuuTruGia()
  await g3.du.ghi('hopThuDi', { maThaoTac: 'trung-1', trangThai: 'loi', taoLuc: 1, duLieu: { bienSo: 'A' } })
  lt3.setItem('sp-tram:hopThuDi', JSON.stringify({ 'mo-coi-1': { maThaoTac: 'mo-coi-1', trangThai: 'cho', taoLuc: 2, duLieu: { bienSo: 'B' } }, 'trung-1': { maThaoTac: 'trung-1', trangThai: 'cho', taoLuc: 1, duLieu: { bienSo: 'A' } } }))
  const k3 = taoKhoTuDong({ idb: {}, luuTru: lt3, taoKhoIdb: g3.tao, ngu: khongCho })
  const ds3 = await k3.tatCa('hopThuDi')
  kiem('F7: mo trang -> don muc mo coi localStorage sang IndexedDB', ds3.length === 2 && ds3.some((x) => x.maThaoTac === 'mo-coi-1') && Object.keys(JSON.parse(lt3.getItem('sp-tram:hopThuDi'))).length === 0)
  kiem('F7: trung khoa -> giu ban trong IndexedDB (khong ghi de trang thai loi)', ds3.find((x) => x.maThaoTac === 'trung-1')?.trangThai === 'loi')

  // 4) Khong co localStorage, IndexedDB hong -> bo nho; IndexedDB song lai -> muc trong bo nho cung duoc chuyen
  let gio4 = 0
  const g4 = taoIdbGia()
  g4.soLanHong = 3
  const k4 = taoKhoTuDong({ idb: {}, luuTru: null, taoKhoIdb: g4.tao, ngu: khongCho, dongHo: () => gio4 })
  await taoHopThuDi({ kho: k4 }).them(mucMau('57D00001'))
  gio4 += 31000
  await k4.dem('hopThuDi')
  for (let i = 0; i < 10; i++) await xaHet()
  kiem('F7: du phong bo nho -> IndexedDB song lai thi chuyen ca muc trong bo nho', (await k4.layLoai()) === 'indexeddb' && (await g4.du.tatCa('hopThuDi')).length === 1)
  kiem('F7: kho tu chon co xoaHet (dang xuat)', typeof k4.xoaHet === 'function')
}

// ---------------------------------------------------------------- F8: goi ban sao bi CAT (biCat)
{
  const dh = taoDongHoGia(Date.UTC(2026, 9, 14, 1, 0, 0))
  const kho = taoKhoBoNho()
  const bs = taoBanSao({
    kho,
    dongHo: dh.dongHo,
    goiDayDu: async () => ({
      trangThai: 200,
      etag: '"c1"',
      duLieu: { biCat: true, lenhMo: [{ maLenh: 21, soLenh: 'GN-021', loaiLenh: 'HaCont', bienSo: '51C-111.11', trangThai: 'Chờ xe' }], theXe: [] },
    }),
  })
  await bs.taiVe('A1')
  const b = await bs.lay('A1')
  kiem('F8: luu co biCat cua goi ngoai tuyen', b.duLieu.biCat === true)
  const khong = doiChieuBanSao(b, { bienSo: '99Z99999', huongDi: 'Vào' })
  kiem('F8: bi cat + khong thay lenh -> khongDayDu (KHONG khang dinh "khong co lenh")', khong.lenh === null && khong.khongDayDu === true && khong.biCat === true)
  const co = doiChieuBanSao(b, { bienSo: '51C11111', huongDi: 'Vào' })
  kiem('F8: bi cat nhung thay lenh -> doi chieu binh thuong', co.lenh?.maLenh === 21 && co.khongDayDu === false)
  const du = doiChieuBanSao({ duLieu: { ...b.duLieu, biCat: false } }, { bienSo: '99Z99999', huongDi: 'Vào' })
  kiem('F8: goi day du + khong thay lenh -> khongDayDu = false (van bao khong co lenh)', du.khongDayDu === false)
  const tt = await bs.tomTat()
  kiem('F8: tom tat ban sao co biCat -> thanh trang thai "bản sao không đầy đủ"', tt.biCat === true && moTaTrangThai({ coMang: false, soCho: 0, soDangGui: 0, soLoi: 0, soCanDoiSoat: 0, banSao: tt }).chu.includes('bản sao không đầy đủ'))
  const chot = readFileSync(new URL('../src/khu/cong/ChotBaoVe.jsx', import.meta.url), 'utf8')
  const cong = readFileSync(new URL('../src/pages/CongThongMinh.jsx', import.meta.url), 'utf8')
  const thanh = readFileSync(new URL('../src/components/ThanhTrangThaiMang.jsx', import.meta.url), 'utf8')
  kiem('F8: ChotBaoVe noi ro "Bản sao không đầy đủ (bị cắt) — không kiểm được lệnh"', chot.includes('dc.khongDayDu') && chot.includes('Bản sao không đầy đủ (bị cắt) — không kiểm được lệnh, kiểm giấy tờ bằng mắt / hỏi điều độ.'))
  kiem('F8: CongThongMinh: bi cat -> nhanh khong-kiem-duoc (khong bao "Không có lệnh")', cong.includes('kq.khongDayDu') && cong.includes("trangThai: 'khong-kiem-duoc', biCat: true") && cong.includes('Bản sao không đầy đủ (bị cắt)'))
  kiem('F8: thanh trang thai co chi bao ban sao bi cat', thanh.includes('tt.banSao?.biCat'))
}

// ---------------------------------------------------------------- F1 / F6: kiem tinh tren ma nguon giao dien
{
  const doc = (p) => readFileSync(new URL(p, import.meta.url), 'utf8')
  const chot = doc('../src/khu/cong/ChotBaoVe.jsx')
  const cong = doc('../src/pages/CongThongMinh.jsx')
  const tramSrc = readFileSync(new URL('tram.js', GOC), 'utf8')
  kiem("F1: tram: XE RA + toi duoc may chu -> canXacNhan", tramSrc.includes("canXacNhan: guiTrucTiep && muc.huongDi === 'Ra'") && tramSrc.includes('const toiDuoc = tt.coMang !== false && !tt.congNgung'))
  kiem('F1: tram: "Vẫn cho qua" xep hang ngay (xepHangLuon), khong gui truc tiep', tramSrc.includes('const guiTrucTiep = toiDuoc && !xepHangLuon'))
  kiem('F1: ChotBaoVe hien DO "CHƯA XÁC NHẬN ĐƯỢC — CHƯA CHO XE RA" + 2 nut', chot.includes("kqGhi.ketQua === 'chua-xac-nhan'") && chot.includes('CHƯA XÁC NHẬN ĐƯỢC — CHƯA CHO XE RA') && chot.includes('THỬ LẠI') && chot.includes('Vẫn cho qua (ghi tạm, cần đối soát)'))
  kiem('F1: ChotBaoVe Thu lai dung muc cu (cung maThaoTac); Van cho qua mang ly do may chu cham + xepHangLuon', chot.includes('mucCu: xn.muc') && chot.includes('lyDoVuot: LY_DO_VUOT_MAY_CHU_CHAM, xepHangLuon: true'))
  kiem('F1/F2: ChotBaoVe vuot ban sao gui vuotBanSao + lyDoVuot', chot.includes("lyDoVuot: vuot ? 'Bảo vệ cho qua dù bản sao báo: ' + vuot.lyDo : null"))
  kiem('F1: CongThongMinh cung xu ly chua-xac-nhan (Thu lai / Van cho qua)', cong.includes("kq.ketQua === 'chua-xac-nhan'") && cong.includes('CHƯA XÁC NHẬN ĐƯỢC — CHƯA CHO XE RA') && cong.includes('xepHangLuon: true'))
}

console.log(`Kiem thu tram cong ngoai tuyen (Nhom 4): ${dat}/${dat + truot} DAT`)
process.exit(truot ? 1 : 0)
