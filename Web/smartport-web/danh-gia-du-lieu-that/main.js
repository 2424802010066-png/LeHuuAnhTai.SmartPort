// Chay DUNG ham AI cua SmartPort tren tung anh cua bo du lieu that, luu ket qua ra dia moi 25 anh.
//   ?bo=bien_so_e2e : anh camera day du -> phanTichAnhXe (YOLO26n tim xe -> CCT-S v2 + Tesseract doc bien)
//   ?bo=bien_so_cat : anh bien so da cat -> phanTichAnhXe (do rieng buoc DOC)
//   ?bo=container   : anh camera cong 1920x1080 -> docSoContainer tren (1) ca khung hinh, (2) vung ma da cat
//   ?bo=bien_so_cct : anh bien so da cat -> CHI mo hinh doc bien CCT-S v2 cua he thong (cung tien xu ly nhu
//                     docBienCCT trong alpr.js) -> do kha nang DOC khi vung bien da duoc tim dung
import { khoiDongAlpr, phanTichAnhXe, tenEngineDaTai, timBienSo } from '../src/ai/alpr.js'
import { docSoContainer, dungChuan6346, timMaContainer } from '../src/ai/ocrContainer.js'

// ?bo=kiem_ham : kiem nhanh 2 ham chuan hoa tren cac ca loi THAT da gap (khong chay AI)
if (new URLSearchParams(location.search).get('bo') === 'kiem_ham') {
  const bien = [
    ['60B523822', true, '60B5-238.22'], ['59L154063', true, '59L1-540.63'], ['51F88270', false, '51F-882.70'],
    ['51F88270', true, '51F-882.70'], ['51LD12345', true, '51LD-123.45'], ['51C63872', true, '51C-638.72'],
    ['59-L1\n540.63', true, '59L1-540.63'], ['29C46828', false, '29C-468.28'],
  ].map(([vao, hai, mong]) => ({ vao, hai, ra: timBienSo(vao, hai), dung: timBienSo(vao, hai) === mong, mong }))
  const cont = [
    ['CRSU J 921148 5', 'CRSU9211485'], ['MRKUY 693 344 O1', 'MRKU6933440'], ['UESU 232026 O', 'UESU2320260'],
    ['MNBUY 368 443 0', 'MNBU3684430'], ['TCNU 831030 6', 'TCNU8310306'], ['SNBU J 228926 3', 'SNBU2289263'],
    ['MAX GROSS 30480 KG\nEITU 178639 3\n22G1', 'EITU1786393'], ['TRIUJ 8926834', 'TRIU8926834'],
    ['MARRY\n430 6588', null], ['SUE C\n456046 14', null],
  ].map(([vao, mong]) => {
    const r = timMaContainer(vao)
    const ra = r ? r.ma : null
    return { vao, ra, iso: r && r.dungChuan, dung: ra === mong, mong }
  })
  cont.push({ vao: 'dungChuan6346(ARRY4306588)', ra: String(dungChuan6346('ARRY4306588')), mong: 'false',
              dung: dungChuan6346('ARRY4306588') === false })
  document.getElementById('tt').textContent = JSON.stringify({ bien, cont })
  throw new Error('kiem_ham xong')
}
import { taiAnh } from '../src/ai/taiThuVien.js'

const THAM_SO = new URLSearchParams(location.search)
const BO = THAM_SO.get('bo') || 'bien_so_cat'
const DANH_SACH = BO === 'bien_so_cct' ? 'bien_so_cat' : BO

// Giong het hang so + tien xu ly cua docBienCCT (src/ai/alpr.js): anh 128x64 RGB uint8, 10 o x 37 ky tu
const BIEN_ALPHABET = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_'
const BIEN_SLOTS = 10
const BIEN_W = 128
const BIEN_H = 64

async function docCCT(e, img, ghepHang) {
  const c = document.createElement('canvas')
  c.width = BIEN_W
  c.height = BIEN_H
  const cx = c.getContext('2d')
  const w = img.naturalWidth
  const h = img.naturalHeight
  if (ghepHang) {
    // bien 2 hang: cat doi tren/duoi roi ghep thanh 1 hang ngang (y nhu ung dung)
    cx.drawImage(img, 0, 0, w, h / 2, 0, 0, BIEN_W / 2, BIEN_H)
    cx.drawImage(img, 0, h / 2, w, h / 2, BIEN_W / 2, 0, BIEN_W / 2, BIEN_H)
  } else {
    cx.drawImage(img, 0, 0, w, h, 0, 0, BIEN_W, BIEN_H)
  }
  const d = cx.getImageData(0, 0, BIEN_W, BIEN_H).data
  const arr = new Uint8Array(BIEN_H * BIEN_W * 3)
  for (let i = 0, p = 0; i < d.length; i += 4) {
    arr[p++] = d[i]
    arr[p++] = d[i + 1]
    arr[p++] = d[i + 2]
  }
  const out = await e.cct.run({ [e.cct.inputNames[0]]: new window.ort.Tensor('uint8', arr, [1, BIEN_H, BIEN_W, 3]) })
  let o = null
  for (const ten of e.cct.outputNames) {
    const t = out[ten]
    if (t?.dims && t.dims.reduce((a, b) => a * b, 1) === BIEN_SLOTS * BIEN_ALPHABET.length) o = t
  }
  let text = ''
  let tong = 0
  let dem = 0
  for (let s = 0; s < BIEN_SLOTS; s++) {
    let best = -1
    let bi = 0
    for (let a = 0; a < BIEN_ALPHABET.length; a++) {
      const v = Number(o.data[s * BIEN_ALPHABET.length + a])
      if (v > best) {
        best = v
        bi = a
      }
    }
    if (BIEN_ALPHABET[bi] !== '_') {
      text += BIEN_ALPHABET[bi]
      tong += best
      dem++
    }
  }
  return { text, conf: dem ? tong / dem : 0 }
}
const GIOI_HAN = +(THAM_SO.get('n') || 0) // 0 = chay het danh sach
const DU_LIEU = '/@fs/C:/Users/ASUS/Downloads/SmartPort_DuLieuThat/'
const tt = document.getElementById('tt')

async function luu(duLieu) {
  await fetch('/__luu', { method: 'POST', body: JSON.stringify({ ten: 'ket_qua_' + BO + '.json', du_lieu: duLieu }) })
}

/** Cat vung ma container theo khung nhan (noi rong 15% moi phia) — gia lap buoc "tim vung ma" da lam dung */
function catVung(img, [x1, y1, x2, y2], le = 0.15) {
  const w = x2 - x1
  const h = y2 - y1
  const sx = Math.max(0, Math.round(x1 - w * le))
  const sy = Math.max(0, Math.round(y1 - h * le))
  const ex = Math.min(img.naturalWidth, Math.round(x2 + w * le))
  const ey = Math.min(img.naturalHeight, Math.round(y2 + h * le))
  const c = document.createElement('canvas')
  c.width = ex - sx
  c.height = ey - sy
  c.getContext('2d').drawImage(img, sx, sy, c.width, c.height, 0, 0, c.width, c.height)
  return c
}

async function chay() {
  const ds = await (await fetch(DU_LIEU + 'danh_sach_' + DANH_SACH + '.json')).json()
  const may = BO === 'bien_so_cct' ? await khoiDongAlpr() : null
  const tong = GIOI_HAN > 0 ? Math.min(GIOI_HAN, ds.length) : ds.length
  const ketQua = []
  const cv = document.createElement('canvas')
  const batDau = performance.now()
  let engine = null
  for (let i = 0; i < tong; i++) {
    const d = ds[i]
    const r = { id: d.id, nhom: d.nhom, nhan: d.nhan }
    const t0 = performance.now()
    try {
      const img = await taiAnh(d.url)
      if (BO === 'bien_so_cct') {
        // 2 hang hay 1 hang: theo ti le khung (bien 2 hang gan vuong) — khong dung nhan de "gian lan"
        const ghep = img.naturalWidth / img.naturalHeight < 2.5
        const kq = await docCCT(may, img, ghep)
        Object.assign(r, { doc: timBienSo(kq.text, ghep) || kq.text, raw: kq.text, chuanVN: !!timBienSo(kq.text, ghep),
                           doTinCay: Math.round(kq.conf * 100), ghepHang: ghep, engine: 'cct' })
      } else if (BO.startsWith('bien_so')) {
        const kq = await phanTichAnhXe(img, cv)
        Object.assign(r, {
          doc: kq.bienSo, chuanVN: kq.chuanVN, doTinCay: kq.doTinCay, engine: kq.engine, coXe: kq.coXe,
          dongThuan: kq.dongThuan, khac: kq.khac, loaiXe: kq.loaiXe,
        })
      } else {
        const toan = await docSoContainer(img)
        r.toan = { ma: toan.ma, dungChuan: toan.dungChuan, doTinCay: toan.doTinCay, raw: (toan.raw || '').slice(0, 90) }
        r.msToan = Math.round(performance.now() - t0)
        const t1 = performance.now()
        const cat = await docSoContainer(catVung(img, d.hop))
        r.cat = { ma: cat.ma, dungChuan: cat.dungChuan, doTinCay: cat.doTinCay, raw: (cat.raw || '').slice(0, 90) }
        r.msCat = Math.round(performance.now() - t1)
      }
    } catch (e) {
      r.loi = String((e && e.message) || e)
    }
    r.ms = Math.round(performance.now() - t0)
    ketQua.push(r)
    tt.textContent = BO + ': ' + (i + 1) + '/' + tong
    if (!engine && BO.startsWith('bien_so')) engine = await tenEngineDaTai().catch(() => null)
    if ((i + 1) % 25 === 0 || i === tong - 1) {
      await luu({ bo: BO, xong: i + 1, tong, giay: Math.round((performance.now() - batDau) / 1000), engine,
                  coLapNguon: self.crossOriginIsolated, ua: navigator.userAgent, ketQua })
    }
  }
  document.title = 'XONG ' + BO
  tt.textContent = 'XONG ' + BO + ' — ' + tong + ' ảnh'
}

chay().catch((e) => {
  tt.textContent = 'LỖI: ' + e.message
  document.title = 'LOI ' + BO
})
