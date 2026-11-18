// TRINH DIEN: AI cua SmartPort doc anh THAT ngay tren man hinh — moi lan bam chon ngau nhien anh khac.
// Dung dung ham cua ung dung: phanTichAnhXe (ve khung xe + vung bien len canvas) va docSoContainer.
import { phanTichAnhXe } from '../src/ai/alpr.js'
import { docSoContainer } from '../src/ai/ocrContainer.js'
import { taiAnh } from '../src/ai/taiThuVien.js'

const DU_LIEU = '/@fs/C:/Users/ASUS/Downloads/SmartPort_DuLieuThat/'
const SO_O_TO = 6
const SO_XE_MAY = 6
const SO_CONT_NGANG = 6
const SO_CONT_2_DONG = 2

const $ = (id) => document.getElementById(id)
const sach = (s) => (s || '').toUpperCase().replace(/[^A-Z0-9]/g, '')
const tron = (ds) => {
  const a = ds.slice()
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

let dsBien = null
let dsCont = null

function veDem(bien, cont) {
  const o = (ten, d) =>
    `<div class="o-dem"><span>${ten}</span><b>${d.dung}/${d.xong}${d.xong ? ' · ' + Math.round((100 * d.dung) / d.xong) + '%' : ''}</b>` +
    `<span>${d.xong ? 'trung bình ' + Math.round(d.ms / d.xong) + ' ms/ảnh' : 'chưa chạy'}</span></div>`
  $('dem').innerHTML = o('Biển số đọc đúng', bien) + o('Mã container đọc đúng', cont)
}

function theMoi(luoi, dongDau) {
  const the = document.createElement('div')
  the.className = 'the cho'
  const cv = document.createElement('canvas')
  const noi = document.createElement('div')
  noi.className = 'noi'
  noi.innerHTML = `<span class="xam">${dongDau}</span><br><span class="xam nho">đang chờ…</span>`
  the.append(cv, noi)
  luoi.append(the)
  return { the, cv, noi }
}

/** Cat vung ma theo khung nhan (noi 15%) — y nhu buoc "tim vung ma" da lam dung */
function catVung(img, [x1, y1, x2, y2], cv, le = 0.15) {
  const w = x2 - x1
  const h = y2 - y1
  const sx = Math.max(0, Math.round(x1 - w * le))
  const sy = Math.max(0, Math.round(y1 - h * le))
  const ex = Math.min(img.naturalWidth, Math.round(x2 + w * le))
  const ey = Math.min(img.naturalHeight, Math.round(y2 + h * le))
  cv.width = ex - sx
  cv.height = ey - sy
  cv.getContext('2d').drawImage(img, sx, sy, cv.width, cv.height, 0, 0, cv.width, cv.height)
  return cv
}

async function chay() {
  $('nut').disabled = true
  $('luoi-bien').innerHTML = ''
  $('luoi-cont').innerHTML = ''
  const dem = { bien: { xong: 0, dung: 0, ms: 0 }, cont: { xong: 0, dung: 0, ms: 0 } }
  veDem(dem.bien, dem.cont)
  try {
    $('tt').textContent = 'Đang nạp danh sách ảnh…'
    dsBien = dsBien || (await (await fetch(DU_LIEU + 'danh_sach_bien_so_e2e.json')).json())
    dsCont = dsCont || (await (await fetch(DU_LIEU + 'danh_sach_container.json')).json())
    const bien = tron(dsBien.filter((d) => d.nhom === 'oto_1_dong')).slice(0, SO_O_TO)
      .concat(tron(dsBien.filter((d) => d.nhom === 'xe_may_2_dong')).slice(0, SO_XE_MAY))
    const cont = tron(dsCont.filter((d) => d.nhom === 'ngang')).slice(0, SO_CONT_NGANG)
      .concat(tron(dsCont.filter((d) => d.nhom === 'ngang_2_dong')).slice(0, SO_CONT_2_DONG))
    const theBien = bien.map((d) => theMoi($('luoi-bien'), (d.nhom === 'oto_1_dong' ? 'Ô tô · ' : 'Xe máy · ') + 'đáp án ' + d.nhan))
    const theCont = cont.map((d) => theMoi($('luoi-cont'), 'Đáp án ' + d.nhan))

    for (let i = 0; i < bien.length; i++) {
      const d = bien[i]
      const t = theBien[i]
      $('tt').textContent = `Biển số ${i + 1}/${bien.length}` + (i === 0 ? ' — lần đầu nạp mô hình AI (YOLO26n, CCT-S v2, Tesseract)…' : '…')
      const t0 = performance.now()
      const img = await taiAnh(d.url)
      const kq = await phanTichAnhXe(img, t.cv)
      const ms = Math.round(performance.now() - t0)
      const ok = sach(kq.bienSo) === d.nhan
      dem.bien.xong++
      dem.bien.dung += ok ? 1 : 0
      dem.bien.ms += ms
      t.the.className = 'the ' + (ok ? 'dung-vien' : 'sai-vien')
      t.noi.innerHTML =
        `<span class="xam">${d.nhom === 'oto_1_dong' ? 'Ô tô' : 'Xe máy'} · đáp án <b>${d.nhan}</b></span><br>` +
        `<span class="${ok ? 'dung' : 'sai'}">${ok ? '✓' : '✗'} AI đọc: ${kq.bienSo || '(không đọc được)'}</span><br>` +
        `<span class="xam nho">${kq.engine === 'cct' ? 'mô hình CCT-S v2' : 'Tesseract (dự phòng)'} · tin cậy ${kq.doTinCay ?? '–'} · ${ms} ms</span>`
      veDem(dem.bien, dem.cont)
    }

    for (let i = 0; i < cont.length; i++) {
      const d = cont[i]
      const t = theCont[i]
      $('tt').textContent = `Mã container ${i + 1}/${cont.length}…`
      const img = await taiAnh(d.url)
      const t0 = performance.now()
      const kq = await docSoContainer(catVung(img, d.hop, t.cv))
      const ms = Math.round(performance.now() - t0)
      const ok = kq.ma === d.nhan
      dem.cont.xong++
      dem.cont.dung += ok ? 1 : 0
      dem.cont.ms += ms
      t.the.className = 'the ' + (ok ? 'dung-vien' : 'sai-vien')
      t.noi.innerHTML =
        `<span class="xam">${d.nhom === 'ngang' ? 'Mã ngang' : 'Mã ngang 2 dòng'} · đáp án <b>${d.nhan}</b></span><br>` +
        `<span class="${ok ? 'dung' : 'sai'}">${ok ? '✓' : '✗'} AI đọc: ${kq.ma || '(không ra mã hợp lệ)'}</span><br>` +
        `<span class="xam nho">${kq.ma ? (kq.dungChuan ? 'qua kiểm số ISO 6346' : 'KHÔNG qua kiểm số ISO 6346') : 'Tesseract đọc: ' +
          (kq.raw || '').replace(/\s+/g, ' ').slice(0, 28)} · ${ms} ms</span>`
      veDem(dem.bien, dem.cont)
    }
    $('tt').textContent = 'Xong lượt này. Bấm “Chạy thử” lần nữa để chọn ảnh khác (kết quả đo trên 1.000 ảnh: xem báo cáo).'
  } catch (e) {
    $('tt').textContent = 'Lỗi: ' + e.message
  } finally {
    $('nut').disabled = false
  }
}

$('nut').addEventListener('click', chay)
veDem({ xong: 0, dung: 0, ms: 0 }, { xong: 0, dung: 0, ms: 0 })
