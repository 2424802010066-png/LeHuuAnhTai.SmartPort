/* ============================================================
   OCR SO CONTAINER tu anh (Tesseract.js — mang LSTM) + kiem tra ISO 6346
   ============================================================ */
import { taoWorkerTesseract } from './taiThuVien'

let huaWorker = null
let baoTienDo = () => {}

function layWorker() {
  if (!huaWorker) {
    huaWorker = taoWorkerTesseract({ tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ' }, (m) => {
      if (m.status === 'recognizing text') baoTienDo(m.progress, 'Đang đọc ký tự… ' + Math.round(m.progress * 100) + '%')
      else baoTienDo(m.progress || 0, m.status)
    }).catch((e) => {
      huaWorker = null // lan sau thu tai lai
      throw e
    })
  }
  return huaWorker
}

/* ISO 6346: ky tu thu 11 la CHECK DIGIT tinh tu 10 ky tu dau.
   A=10, B=12... (bo boi so cua 11), nhan trong so 2^vi_tri, lay mod 11 roi mod 10.
   OCR doc nham 1 ky tu la checksum lech ngay -> chan ma "hop le gia". */
const GT6346 = { A: 10, B: 12, C: 13, D: 14, E: 15, F: 16, G: 17, H: 18, I: 19, J: 20, K: 21, L: 23, M: 24, N: 25, O: 26, P: 27, Q: 28, R: 29, S: 30, T: 31, U: 32, V: 34, W: 35, X: 36, Y: 37, Z: 38 }

export function dungChuan6346(code) {
  // Ky tu thu 4 la KY TU PHAN LOAI: U (container hang), J (thiet bi thao roi), Z (ro-moc/chassis) — chu khac la sai chuan
  if (!/^[A-Z]{3}[UJZ]\d{7}$/.test(code)) return false
  let sum = 0
  for (let i = 0; i < 10; i++) {
    const ch = code.charAt(i)
    sum += (ch >= '0' && ch <= '9' ? +ch : GT6346[ch]) * Math.pow(2, i)
  }
  return (sum % 11) % 10 === +code.charAt(10)
}

/** Tien xu ly: phong to (toi da 2.2 lan) + grayscale + tang tuong phan 1.35 lan */
function tienXuLy(source) {
  const w = source.naturalWidth || source.videoWidth || source.width
  const h = source.naturalHeight || source.videoHeight || source.height
  const scale = Math.max(1, Math.min(2.2, 1300 / w))
  const c = document.createElement('canvas')
  c.width = Math.round(w * scale)
  c.height = Math.round(h * scale)
  const x = c.getContext('2d')
  x.imageSmoothingEnabled = true
  x.drawImage(source, 0, 0, c.width, c.height)
  const d = x.getImageData(0, 0, c.width, c.height)
  const p = d.data
  for (let i = 0; i < p.length; i += 4) {
    let g = 0.299 * p[i] + 0.587 * p[i + 1] + 0.114 * p[i + 2]
    g = Math.max(0, Math.min(255, (g - 128) * 1.35 + 128))
    p[i] = p[i + 1] = p[i + 2] = g
  }
  x.putImageData(d, 0, 0)
  return c
}

/* Nham chu <-> so theo hinh dang — chi dung de sua ky tu O DUNG LOAI theo vi tri ISO 6346 */
const CHU_SANG_SO = { O: '0', Q: '0', D: '0', I: '1', L: '1', T: '7', Z: '2', S: '5', B: '8', G: '6', A: '4' }
const SO_SANG_CHU = { 0: 'O', 1: 'I', 2: 'Z', 4: 'A', 5: 'S', 6: 'G', 7: 'T', 8: 'B' }

/**
 * TIM MA CONTAINER THEO CAU TRUC ISO 6346 (thay cho bat chuoi lien tuc [A-Z]{4}\d{7}):
 *  - "neo" vao KY TU PHAN LOAI U/J/Z o vi tri thu 4 (doc DUNG nhu Tesseract tra ve, khong ep) ngay sau 3 chu
 *    ma chu so huu -> chuoi rac "RSUJ..." (lech khung 1 ky tu) khong con duoc chon.
 *  - 7 ky tu sau la SO: cho noi qua dau cach ("693 344 0"), sua nham chu<->so theo vi tri (toi da 2 lan).
 *  - Bo rac: 1-2 chu dinh ngay sau U ("MRKUY 693…") hoac mot cum 1-2 chu dung rieng ("CRSU J 921148 5").
 * Cham diem: qua so kiem tra +100, moi ky tu phai sua -5, moi lan bo rac -3, ky tu phan loai la U +5 (gan nhu moi
 * container hang deu la U), ma chu so huu khong bat dau o dau cum chu -3 ("TRIUJ" -> TRIU, khong phai RIUJ).
 * Tra ve ung vien diem cao nhat, hoac null neu khong co ung vien nao dung cau truc.
 */
export function timMaContainer(raw) {
  const tu = (raw || '').toUpperCase().split(/[^A-Z0-9]+/).filter(Boolean)
  // Ma xep DOC: Tesseract tra moi ky tu mot dong -> ghep cac cum 1 ky tu lien nhau thanh mot tu
  const cumDon = []
  let dang = ''
  for (const t of tu) {
    if (t.length === 1) dang += t
    else {
      if (dang.length >= 6) cumDon.push(dang)
      dang = ''
    }
  }
  if (dang.length >= 6) cumDon.push(dang)
  const nguon = tu.concat(cumDon)

  let tot = null
  const xet = (ma, sua, boRac, phanLoai, lech) => {
    if (!/^[A-Z]{3}[UJZ]\d{7}$/.test(ma)) return
    const qua = dungChuan6346(ma)
    const diem = (qua ? 100 : 0) - 5 * sua - 3 * boRac + (phanLoai === 'U' ? 5 : 0) - (lech ? 3 : 0)
    if (!tot || diem > tot.diem) tot = { ma, dungChuan: qua, diem, sua, boRac }
  }

  nguon.forEach((t, i) => {
    for (let s = 0; s + 4 <= t.length; s++) {
      const pl = t[s + 3]
      if (!'UJZ'.includes(pl)) continue
      // 3 chu ma chu so huu: cho sua toi da 1 ky tu so -> chu
      let chu = ''
      let suaChu = 0
      for (let k = s; k < s + 3; k++) {
        const c = /[A-Z]/.test(t[k]) ? t[k] : SO_SANG_CHU[t[k]]
        if (!c) break
        if (c !== t[k]) suaChu++
        chu += c
      }
      if (chu.length < 3 || suaChu > 1) continue
      const conLai = t.slice(s + 4)
      const sau = i < tu.length ? tu.slice(i + 1) : []
      // cac cach lay 7 so: (a) noi tiep binh thuong, (b) bo 1-2 chu dinh sau U, (c) bo 1 cum rac 1-2 chu dung rieng
      const cachLay = [{ dong: conLai + sau.join(' '), boRac: 0 }]
      const m = conLai.match(/^[A-Z]{1,2}/)
      if (m) cachLay.push({ dong: conLai.slice(m[0].length) + ' ' + sau.join(' '), boRac: 1 })
      if (!conLai && sau.length > 1 && /^[A-Z]{1,2}$/.test(sau[0])) cachLay.push({ dong: sau.slice(1).join(' '), boRac: 1 })
      for (const c of cachLay) {
        const kyTu = c.dong.replace(/\s+/g, '')
        if (kyTu.length < 7) continue
        let so = ''
        let suaSo = 0
        for (let k = 0; k < 7; k++) {
          const ch = /[0-9]/.test(kyTu[k]) ? kyTu[k] : CHU_SANG_SO[kyTu[k]]
          if (!ch) break
          if (ch !== kyTu[k]) suaSo++
          so += ch
        }
        if (so.length === 7 && suaSo <= 2) xet(chu + pl + so, suaChu + suaSo, c.boRac, pl, s > 0)
      }
    }
  })
  return tot
}

/** Doc so container: tra ve ma uu tien ma qua duoc checksum ISO 6346 */
export async function docSoContainer(source, khiTienDo) {
  baoTienDo = khiTienDo || (() => {})
  const worker = await layWorker()
  const res = await worker.recognize(tienXuLy(source))
  baoTienDo(1, 'Hoàn tất')
  const raw = (res.data.text || '').toUpperCase()
  const theoCauTruc = timMaContainer(raw)
  // Du phong cach cu (bat chuoi lien tuc) khi khong co ung vien nao dung cau truc — van bat buoc ky tu phan loai U/J/Z
  const cacMa = raw.replace(/[^A-Z0-9]/g, '').match(/[A-Z]{3}[UJZ]\d{7}/g) || []
  const chuan = cacMa.find(dungChuan6346)
  const ma = theoCauTruc ? theoCauTruc.ma : chuan || cacMa[0] || null
  return {
    raw: raw.trim().substring(0, 220),
    ma,
    dungChuan: !!ma && dungChuan6346(ma),
    doTinCay: Math.round(res.data.confidence || 0),
  }
}

/** Anh demo: ve "vo container" co so hieu bang canvas de thu ngay khong can anh that */
export function veAnhContainerDemo(soHieu = 'MSCU 2201417') {
  const c = document.createElement('canvas')
  c.width = 900
  c.height = 340
  const x = c.getContext('2d')
  x.fillStyle = '#b45309'
  x.fillRect(0, 0, 900, 340)
  x.fillStyle = '#92400e'
  for (let i = 0; i < 900; i += 56) x.fillRect(i, 0, 22, 340)
  x.fillStyle = '#fff'
  x.fillRect(60, 90, 780, 160)
  x.fillStyle = '#000'
  x.font = '700 92px "Courier New", monospace'
  x.fillText(soHieu, 95, 200)
  return c
}
