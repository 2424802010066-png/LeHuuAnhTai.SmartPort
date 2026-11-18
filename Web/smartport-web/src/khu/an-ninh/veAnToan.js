// CANH BAO NGUY HIEM AI — hang so + ve ket qua may chu (an_toan_cang.py) de len khung hinh.
// Toa do ket qua = pixel cua KHUNG DA GUI (vung, hieu chinh: 0..1 theo khung).

export const MAU_MUC = { 'nguy-hiem': '#dc2626', 'canh-bao': '#f59e0b' }
export const TEN_MUC = { 'nguy-hiem': 'NGUY HIỂM', 'canh-bao': 'Cảnh báo' }

export const LOAI_VUNG = {
  'cam-nguoi': { ten: 'Cấm người (cần trục, bãi xếp)', mau: 'rgba(220,38,38,0.22)', vien: '#dc2626' },
  'lan-xe': { ten: 'Làn xe chạy', mau: 'rgba(245,158,11,0.18)', vien: '#f59e0b' },
  'gioi-han-toc-do': { ten: 'Giới hạn tốc độ', mau: 'rgba(59,130,246,0.16)', vien: '#3b82f6' },
  'vat-can': { ten: 'Vật cản cố định (cont xếp, chân cẩu, tường) — vẽ quanh chân', mau: 'rgba(124,58,237,0.20)', vien: '#7c3aed' },
}

/** loai rui ro (may chu) -> Loai su co ghi vao phong an ninh (<= 20 ky tu) */
export const LOAI_SU_CO = {
  'va-cham-xe-nguoi': 'VaChamXeNguoi',
  'va-cham-xe-xe': 'VaChamXeXe',
  'qua-gan-xe': 'QuaGanXe',
  'vao-vung-cam': 'VaoVungCam',
  'lan-xe': 'NguoiTrenLanXe',
  'qua-toc-do': 'QuaTocDo',
  'nguoi-nga': 'NguoiNga',
  'thieu-mu': 'ThieuMuBaoHo',
  'thieu-ao': 'ThieuAoPhanQuang',
  'va-cham-vat-can': 'VaChamVatCan',
}

// ---------- tien ich thuan (khong DOM, khong React) — kiem thu bang Node ----------
const KC_DIEM_TOI_THIEU = 0.02 // 2% duong cheo khung: gan hon = bam dup / bam lai cung goc
const DT_HIEU_CHINH_TOI_THIEU = 0.004 // tu giac hieu chinh >= 0.4% dien tich khung
const SIN_GOC_TOI_THIEU = 0.05 // goc tai moi dinh >= ~3 do (3 diem gan thang hang -> homography suy bien)

/** kich thuoc that (m) cua hinh chu nhat hieu chinh: so huu han >= 1 */
export const kichThuocHopLe = (x) => Number.isFinite(x) && x >= 1

/** p cach mot diem trong ds duoi tiLe * duong cheo khung? (toa do 0..1, W x H de doi ra pixel) */
export function diemQuaGan(p, ds, W = 1, H = 1, tiLe = KC_DIEM_TOI_THIEU) {
  const nguong = tiLe * Math.hypot(W, H)
  return (ds ?? []).some(([x, y]) => Math.hypot((x - p[0]) * W, (y - p[1]) * H) < nguong)
}

/**
 * Kiem 4 diem hieu chinh mat dat (toa do 0..1) + kich thuoc that -> null neu dung, nguoc lai cau bao loi.
 * Diem trung (bam dup) / 3 diem thang hang / tu giac tu cat -> homography suy bien: may chu doi sang met sai ma van bao
 * "da hieu chinh"; rong / dai = 0 -> ma tran suy bien, moi khung deu loi. heSo < 1: kiem long hon (cau hinh da luu).
 */
export function loiHieuChinh(anh, met, W = 1, H = 1, heSo = 1) {
  if (!Array.isArray(anh) || anh.length !== 4 || !Array.isArray(met) || met.length !== 4) return 'Hiệu chỉnh cần đúng 4 điểm.'
  if (!kichThuocHopLe(met[1]?.[0]) || !kichThuocHopLe(met[2]?.[1])) return 'Kích thước thật Rộng / Dài phải ≥ 1 m.'
  const p = anh.map((d) => (Array.isArray(d) ? [d[0] * W, d[1] * H] : [NaN, NaN]))
  if (p.some(([x, y]) => !Number.isFinite(x) || !Number.isFinite(y))) return 'Tọa độ điểm hiệu chỉnh không hợp lệ.'
  const cheo = Math.hypot(W, H)
  for (let i = 0; i < 4; i++)
    for (let j = i + 1; j < 4; j++)
      if (Math.hypot(p[i][0] - p[j][0], p[i][1] - p[j][1]) < KC_DIEM_TOI_THIEU * heSo * cheo)
        return 'Có 2 góc trùng hoặc quá gần nhau (bấm đúp?).'
  // tu giac loi: 4 tich co huong cua 2 canh lien tiep cung dau va du lon (khong lom, khong tu cat, khong 3 diem thang hang)
  let dau = 0
  for (let i = 0; i < 4; i++) {
    const [ax, ay] = p[i]
    const [bx, by] = p[(i + 1) % 4]
    const [cx, cy] = p[(i + 2) % 4]
    const tich = (bx - ax) * (cy - by) - (by - ay) * (cx - bx)
    const sinGoc = tich / (Math.hypot(bx - ax, by - ay) * Math.hypot(cx - bx, cy - by))
    if (Math.abs(sinGoc) < SIN_GOC_TOI_THIEU * heSo || (dau && Math.sign(sinGoc) !== dau))
      return '4 góc phải tạo thành tứ giác lồi, bấm đúng thứ tự gần-trái → gần-phải → xa-phải → xa-trái.'
    dau = Math.sign(sinGoc)
  }
  let s2 = 0
  for (let i = 0; i < 4; i++) s2 += p[i][0] * p[(i + 1) % 4][1] - p[(i + 1) % 4][0] * p[i][1]
  if (Math.abs(s2) / 2 < DT_HIEU_CHINH_TOI_THIEU * heSo * W * H) return 'Hình chữ nhật hiệu chỉnh quá nhỏ trên khung hình — chọn vùng mặt đất lớn hơn.'
  return null
}

/** su kien can bao phong an ninh (NGUY HIEM / thieu bao ho) -> gom theo loai su co: moi loai chi 1 lan bao / khung */
export function gomBaoAnNinh(suKien) {
  const nhom = new Map()
  for (const e of suKien ?? []) {
    if (!(e.muc === 'nguy-hiem' || e.loai?.startsWith('thieu-'))) continue
    const loai = LOAI_SU_CO[e.loai] ?? 'Khac'
    if (!nhom.has(loai)) nhom.set(loai, [])
    nhom.get(loai).push(e)
  }
  // nguy hiem truoc, roi theo ma -> su kien dau nhom (mo ta + khoaTrung) on dinh giua cac lan bao lai
  const thuTu = (a, b) =>
    (b.muc === 'nguy-hiem') - (a.muc === 'nguy-hiem') || String(a.ma).localeCompare(String(b.ma), 'en', { numeric: true })
  return [...nhom.values()].map((ds) => ds.sort(thuTu))
}

/** than POST /api/an-ninh/su-co cho 1 nhom (chua co viTri, anh) — khoaTrung = ma su kien dau nhom (chong trung 30 s) */
export function noiDungBaoAnNinh(nhom) {
  const e = nhom[0]
  const them = nhom.length > 1 ? ' (' + nhom.length + ' đối tượng)' : ''
  const chiTiet = nhom.map(chiTietRuiRo).filter(Boolean).join('; ')
  return {
    loai: LOAI_SU_CO[e.loai] ?? 'Khac',
    mucDo: nhom.some((x) => x.muc === 'nguy-hiem') ? 'Nguy hiểm' : 'Chú ý',
    moTa: (e.moTa + them + (chiTiet ? ' — ' + chiTiet : '')).slice(0, 300),
    khoaTrung: String(e.ma ?? '').slice(0, 100) || undefined,
  }
}

/**
 * Hang viec bat dong bo chay TUAN TU (bao phong an ninh: khong ban song song qua chong trung), toi da `toiDa` viec
 * dang cho — may chu treo thi them() tra false thay vi xep vo han (bao tre hang phut, ton RAM vi moi viec giu anh).
 * Viec loi khong lam dut hang.
 */
export function taoHangTuanTu(toiDa) {
  let duoi = Promise.resolve()
  let soCho = 0
  return {
    get soCho() {
      return soCho
    },
    them(viec) {
      if (soCho >= toiDa) return false
      soCho++
      duoi = duoi
        .then(viec)
        .catch(() => {})
        .finally(() => soCho--)
      return true
    },
  }
}

/** pb = { gui, hien }: hien tang moi lan doi cau hinh; gui = phien ban may chu DA NHAN (-1: phai gui) */
export function chuanBiGuiCauHinh(pb) {
  const ban = pb.hien
  return { ban, guiCauHinh: pb.gui !== ban }
}

/**
 * Sau khi may chu tra ket qua: danh dau DUNG phien ban da gui (doi cau hinh trong luc cho -> khung sau gui lai);
 * may chu bao canCauHinh (phien vua tao lai: het han / bi day ra) hoac so khung lui ma khung nay khong kem cau hinh
 * -> gui lai o khung ke. Tra so khung moi nhat.
 */
export function daNhanKetQua(pb, lanGui, r, khungTruoc = 0) {
  if (lanGui.guiCauHinh) pb.gui = lanGui.ban
  const khung = typeof r?.khung === 'number' ? r.khung : null
  if (r?.canCauHinh || (!lanGui.guiCauHinh && khung != null && khung <= khungTruoc)) pb.gui = -1
  return khung ?? khungTruoc
}

/** cau chi tiet cua 1 rui ro / su kien */
export function chiTietRuiRo(r) {
  const p = []
  if (r.ttc != null) p.push('còn ' + r.ttc.toFixed(1) + ' s')
  if (r.khoangCachM != null) p.push('cách ' + r.khoangCachM.toFixed(1) + ' m')
  if (r.tocDoKmh != null) p.push(r.tocDoKmh.toFixed(0) + ' km/h' + (r.gioiHan ? ' / giới hạn ' + r.gioiHan : ''))
  if (r.vung) p.push('vùng "' + r.vung + '"')
  if (r.doiTuong?.length) p.push('#' + r.doiTuong.join(' ↔ #'))
  return p.join(' · ')
}

function nhan(g, x, y, chu, nen, co = 13) {
  g.font = `600 ${co}px system-ui, sans-serif`
  const w = g.measureText(chu).width + 8
  const h = co + 6
  const yy = Math.max(0, y - h)
  g.fillStyle = nen
  g.fillRect(x, yy, w, h)
  g.fillStyle = '#fff'
  g.fillText(chu, x + 4, yy + co + 1)
}

function muiTen(g, ax, ay, bx, by, mau) {
  const goc = Math.atan2(by - ay, bx - ax)
  g.fillStyle = mau
  g.beginPath()
  g.moveTo(bx, by)
  g.lineTo(bx - 10 * Math.cos(goc - 0.4), by - 10 * Math.sin(goc - 0.4))
  g.lineTo(bx - 10 * Math.cos(goc + 0.4), by - 10 * Math.sin(goc + 0.4))
  g.closePath()
  g.fill()
}

/** Ve vung + diem hieu chinh + doi tuong + duong du bao + rui ro len canvas (g da ve san khung hinh, W x H) */
export function veAnToan(g, W, H, kq, cauHinh, diemTam = [], cheDoVe = null) {
  const co = Math.max(11, Math.round(W / 70))
  // vung nguy hiem
  for (const z of cauHinh?.vung ?? []) {
    const lv = LOAI_VUNG[z.loai] ?? LOAI_VUNG['cam-nguoi']
    g.beginPath()
    z.diem.forEach(([x, y], i) => (i ? g.lineTo(x * W, y * H) : g.moveTo(x * W, y * H)))
    g.closePath()
    g.fillStyle = lv.mau
    g.fill()
    g.lineWidth = 2
    g.setLineDash([8, 5])
    g.strokeStyle = lv.vien
    g.stroke()
    g.setLineDash([])
    const [x0, y0] = z.diem[0]
    nhan(g, x0 * W, y0 * H + co + 6, z.ten + (z.loai === 'gioi-han-toc-do' ? ' ≤ ' + z.gioiHan + ' km/h' : ''), lv.vien, co - 1)
  }
  // khung hieu chinh mat dat
  const hc = cauHinh?.hieuChinh?.anh
  if (hc?.length === 4) {
    g.beginPath()
    hc.forEach(([x, y], i) => (i ? g.lineTo(x * W, y * H) : g.moveTo(x * W, y * H)))
    g.closePath()
    g.lineWidth = 1.5
    g.strokeStyle = 'rgba(16,185,129,0.9)'
    g.stroke()
  }
  // diem dang ve (vung / hieu chinh)
  if (diemTam.length) {
    g.strokeStyle = cheDoVe === 'hieu-chinh' ? '#10b981' : '#e11d48'
    g.fillStyle = g.strokeStyle
    g.lineWidth = 2
    g.beginPath()
    diemTam.forEach(([x, y], i) => (i ? g.lineTo(x * W, y * H) : g.moveTo(x * W, y * H)))
    g.stroke()
    diemTam.forEach(([x, y], i) => {
      g.beginPath()
      g.arc(x * W, y * H, 5, 0, Math.PI * 2)
      g.fill()
      if (cheDoVe === 'hieu-chinh') nhan(g, x * W + 6, y * H - 4, String(i + 1), '#10b981', co - 1)
    })
  }
  if (!kq) return
  // duong du bao 3 giay toi
  for (const o of kq.doiTuong ?? []) {
    if (!o.duBao) continue
    const mau = o.nguyHiem ? '#dc2626' : o.lop === 'nguoi' ? '#22c55e' : '#38bdf8'
    g.strokeStyle = mau
    g.lineWidth = 2
    g.setLineDash([6, 6])
    g.beginPath()
    o.duBao.forEach(([x, y], i) => (i ? g.lineTo(x, y) : g.moveTo(x, y)))
    g.stroke()
    g.setLineDash([])
    const n = o.duBao.length
    if (n >= 2) muiTen(g, o.duBao[n - 2][0], o.duBao[n - 2][1], o.duBao[n - 1][0], o.duBao[n - 1][1], mau)
  }
  // doi tuong
  for (const o of kq.doiTuong ?? []) {
    const [x1, y1, x2, y2] = o.hop
    const mau = o.nguyHiem ? '#dc2626' : o.lop === 'nguoi' ? '#22c55e' : '#38bdf8'
    g.lineWidth = o.nguyHiem ? 4 : 2
    g.strokeStyle = mau
    g.strokeRect(x1, y1, x2 - x1, y2 - y1)
    const tocDo = o.tocDoKmh != null ? ' · ' + Math.round(o.tocDoKmh) + ' km/h' : ''
    nhan(g, x1, y1, '#' + o.id + ' ' + o.ten + tocDo, mau, co)
    if (o.baoHo) {
      const the = []
      if (o.baoHo.mu === false) the.push('KHÔNG MŨ')
      if (o.baoHo.ao === false) the.push('KHÔNG ÁO PQ')
      if (the.length) nhan(g, x1, y2 + co + 8, the.join(' · '), '#b91c1c', co - 1)
      else if (o.baoHo.mu && o.baoHo.ao) nhan(g, x1, y2 + co + 8, 'Đủ bảo hộ', '#15803d', co - 1)
    }
  }
  // rui ro: duong noi cap doi tuong + TTC
  for (const r of kq.ruiRo ?? []) {
    const mau = MAU_MUC[r.muc] ?? '#f59e0b'
    if (r.diem?.length === 2) {
      const [[ax, ay], [bx, by]] = r.diem
      g.strokeStyle = mau
      g.lineWidth = r.muc === 'nguy-hiem' ? 4 : 2.5
      g.beginPath()
      g.moveTo(ax, ay)
      g.lineTo(bx, by)
      g.stroke()
      const chu = r.ttc != null ? 'TTC ' + r.ttc.toFixed(1) + ' s' : r.khoangCachM != null ? r.khoangCachM.toFixed(1) + ' m' : r.moTa
      nhan(g, (ax + bx) / 2 - 30, (ay + by) / 2, chu, mau, co + 1)
    } else if (r.diem?.length === 1) {
      const [[x, y]] = r.diem
      g.strokeStyle = mau
      g.lineWidth = 3
      g.beginPath()
      g.arc(x, y, co * 1.6, 0, Math.PI * 2)
      g.stroke()
    }
  }
}
