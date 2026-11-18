// Kiem thu hoi quy NHOM GIAO DIEN — canh bao nguy hiem AI (loi 26-35 + hop dong khoaTrung / canCauHinh)
// Chay (trong smartport-web): node kiem-thu/canh-bao-nguy-hiem.mjs
import { readFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'

// duong dan TUONG DOI tep nay (smartport-web/kiem-thu) -> chay duoc o moi may: node kiem-thu/canh-bao-nguy-hiem.mjs
const GOC = new URL('../src/khu/an-ninh/', import.meta.url)
const v = await import(new URL('veAnToan.js', GOC).href)
const jsx = readFileSync(new URL('CanhBaoNguyHiem.jsx', GOC), 'utf8')
const css = readFileSync(new URL('an-ninh.css', GOC), 'utf8')

let dat = 0
let truot = 0
function kiem(ten, dk) {
  if (dk) dat++
  else {
    truot++
    console.log('  TRUOT:', ten)
  }
}

// ---------- Loi 27 + hop dong 2: phien ban cau hinh ----------
{
  // doi cau hinh trong luc yeu cau mang cau hinh dang cho -> khung sau PHAI gui lai
  const pb = { gui: -1, hien: 0 }
  const lan1 = v.chuanBiGuiCauHinh(pb)
  kiem('khung dau luon gui cau hinh', lan1.guiCauHinh)
  pb.hien++ // nguoi dung keo thanh truot trong luc cho
  v.daNhanKetQua(pb, lan1, { khung: 1 }, 0)
  kiem('loi 27: phien ban da gui = ban chup luc gui (khong lay ban hien tai)', pb.gui === 0)
  const lan2 = v.chuanBiGuiCauHinh(pb)
  kiem('loi 27: khung ke gui lai cau hinh moi', lan2.guiCauHinh && lan2.ban === 1)
  v.daNhanKetQua(pb, lan2, { khung: 2 }, 1)
  kiem('khong doi gi -> khung sau khong gui', !v.chuanBiGuiCauHinh(pb).guiCauHinh)

  // logic CU (pb.gui = pb.hien sau khi tra ve) de doi chung: mat cau hinh
  const cu = { gui: -1, hien: 0 }
  const g = cu.gui !== cu.hien
  cu.hien++
  if (g) cu.gui = cu.hien
  kiem('doi chung: logic cu bo sot cau hinh (chung minh kiem thu bat duoc loi)', cu.gui === cu.hien)
}
{
  // hop dong 2: may chu bao canCauHinh (phien vua tao lai, khung khong kem cau hinh)
  const pb = { gui: 5, hien: 5 }
  const lan = v.chuanBiGuiCauHinh(pb)
  kiem('dang dong bo -> khong gui', !lan.guiCauHinh)
  const k = v.daNhanKetQua(pb, lan, { khung: 1, canCauHinh: true }, 300)
  kiem('canCauHinh=true -> pb.gui = -1', pb.gui === -1 && k === 1)
  kiem('canCauHinh -> khung ke gui lai cau hinh', v.chuanBiGuiCauHinh(pb).guiCauHinh)
}
{
  // may chu cu (chua co canCauHinh): so khung lui ma khung khong kem cau hinh -> phien moi -> gui lai
  const pb = { gui: 2, hien: 2 }
  const lan = v.chuanBiGuiCauHinh(pb)
  v.daNhanKetQua(pb, lan, { khung: 1 }, 480)
  kiem('loi 28: so khung lui -> gui lai cau hinh', pb.gui === -1)
  const pb2 = { gui: 2, hien: 2 }
  const k2 = v.daNhanKetQua(pb2, v.chuanBiGuiCauHinh(pb2), { khung: 481 }, 480)
  kiem('so khung tang -> giu nguyen', pb2.gui === 2 && k2 === 481)
  const pb3 = { gui: -1, hien: 2 }
  const lan3 = v.chuanBiGuiCauHinh(pb3)
  v.daNhanKetQua(pb3, lan3, { khung: 1 }, 480)
  kiem('khung lui NHUNG da kem cau hinh -> khong gui lai', pb3.gui === 2)
}
{
  // mo phong vong lap that: yeu cau 30 ms / khung, keo thanh truot 15 -> 30 moi 8 ms; may chu PHAI nhan gia tri cuoi
  const ngu = (ms) => new Promise((ok) => setTimeout(ok, ms))
  let soLanMat = 0
  for (let lap = 0; lap < 20; lap++) {
    const pb = { gui: -1, hien: 0 }
    const may = { ch: null }
    let ch = { toc: 15 }
    let dung = false
    const vong = (async () => {
      while (!dung) {
        const lan = v.chuanBiGuiCauHinh(pb)
        const gui = lan.guiCauHinh ? ch : undefined
        await ngu(10 + (lap % 5) * 5)
        if (gui) may.ch = gui
        v.daNhanKetQua(pb, lan, { khung: 1 }, 0)
      }
    })()
    for (let x = 16; x <= 30; x++) {
      await ngu(3 + (lap % 3))
      ch = { toc: x }
      pb.hien++
    }
    await ngu(120)
    dung = true
    await vong
    if (may.ch?.toc !== 30) soLanMat++
  }
  kiem('loi 27 (mo phong 20 lan keo thanh truot): may chu luon nhan gia tri cuoi 30', soLanMat === 0)
}

// ---------- Loi 32: kiem 4 diem hieu chinh ----------
{
  const W = 960
  const H = 540
  const met = [[0, 0], [10, 0], [10, 20], [0, 20]]
  const A = [0.3, 0.9]
  const B = [0.7, 0.9]
  const C = [0.62, 0.55]
  const D = [0.38, 0.55]
  kiem('hinh thang hop le -> null', v.loiHieuChinh([A, B, C, D], met, W, H) === null)
  kiem('nguoc chieu (guong) van hop le', v.loiHieuChinh([B, A, D, C], met, W, H) === null)
  kiem('[A,A,B,C] (bam dup goc 1) -> loi', v.loiHieuChinh([A, A, B, C], met, W, H) !== null)
  kiem('[A,B,B,C] -> loi', v.loiHieuChinh([A, B, B, C], met, W, H) !== null)
  kiem('2 diem gan nhau 5 px -> loi', v.loiHieuChinh([A, [A[0] + 5 / W, A[1]], C, D], met, W, H) !== null)
  kiem('tu giac tu cat (no) -> loi', v.loiHieuChinh([A, B, D, C], met, W, H) !== null)
  kiem('3 diem thang hang -> loi', v.loiHieuChinh([A, [0.5, 0.9], B, C], met, W, H) !== null)
  kiem('tu giac lom -> loi', v.loiHieuChinh([A, B, [0.5, 0.85], D], met, W, H) !== null)
  kiem('qua nho (30x20 px) -> loi', v.loiHieuChinh([[0.5, 0.5], [0.5 + 30 / W, 0.5], [0.5 + 30 / W, 0.5 - 20 / H], [0.5, 0.5 - 20 / H]], met, W, H) !== null)
  const vuong40 = [[0.5, 0.5], [0.5 + 40 / W, 0.5], [0.5 + 40 / W, 0.5 - 40 / H], [0.5, 0.5 - 40 / H]]
  kiem('dien tich nho (40x40 px, cac goc du xa) -> loi dien tich', /quá nhỏ/.test(v.loiHieuChinh(vuong40, met, W, H) ?? ''))
  kiem('rong = 0 -> loi', v.loiHieuChinh([A, B, C, D], [[0, 0], [0, 0], [0, 20], [0, 20]], W, H) !== null)
  kiem('dai = 0 -> loi', v.loiHieuChinh([A, B, C, D], [[0, 0], [10, 0], [10, 0], [0, 0]], W, H) !== null)
  kiem('rong = NaN (o trong) -> loi', v.loiHieuChinh([A, B, C, D], [[0, 0], [NaN, 0], [NaN, 20], [0, 20]], W, H) !== null)
  kiem('rong = Infinity -> loi', v.loiHieuChinh([A, B, C, D], [[0, 0], [Infinity, 0], [Infinity, 20], [0, 20]], W, H) !== null)
  kiem('3 diem -> loi', v.loiHieuChinh([A, B, C], met, W, H) !== null)
  kiem('du lieu luu hong (khong phai mang) -> loi', v.loiHieuChinh('x', met) !== null && v.loiHieuChinh([A, B, C, null], met) !== null)
  kiem('kiem long (cau hinh da luu, 16x9) van nhan hinh hop le', v.loiHieuChinh([A, B, C, D], met, 16, 9, 0.5) === null)
  kiem('kiem long van bo diem trung', v.loiHieuChinh([A, A, B, C], met, 16, 9, 0.5) !== null)
  kiem('kichThuocHopLe', v.kichThuocHopLe(1) && v.kichThuocHopLe(25.5) && !v.kichThuocHopLe(0) && !v.kichThuocHopLe(0.5) && !v.kichThuocHopLe(NaN) && !v.kichThuocHopLe(Number('')) === true)
  // bam dup: 2 click cung toa do
  kiem('bam dup -> diem qua gan', v.diemQuaGan(A, [A], W, H))
  kiem('goc khac -> khong qua gan', !v.diemQuaGan(B, [A], W, H))
  kiem('cach 10 px -> qua gan (nguong 2% duong cheo ~22 px)', v.diemQuaGan([A[0] + 10 / W, A[1]], [A], W, H))
}

// ---------- Loi 29 + hop dong 1: gom su kien, khoaTrung ----------
{
  const sk = (loai, ma, muc = 'canh-bao', them = {}) => ({ loai, ma, muc, moTa: 'Mô tả ' + loai, t: 12.5, doiTuong: [Number(ma.split(':')[1])], ...them })
  const ba = [sk('thieu-mu', 'thieu-mu:7'), sk('thieu-mu', 'thieu-mu:3'), sk('thieu-mu', 'thieu-mu:10')]
  const nhom = v.gomBaoAnNinh(ba)
  kiem('3 nguoi thieu mu cung khung -> 1 nhom (1 POST)', nhom.length === 1 && nhom[0].length === 3)
  const than = v.noiDungBaoAnNinh(nhom[0])
  kiem('khoaTrung = ma su kien dau nhom (on dinh: thu tu so tu nhien)', than.khoaTrung === 'thieu-mu:3')
  const daoThuTu = v.noiDungBaoAnNinh(v.gomBaoAnNinh([...ba].reverse())[0])
  kiem('khoaTrung khong doi khi may chu tra thu tu khac', daoThuTu.khoaTrung === 'thieu-mu:3')
  kiem('loai = ThieuMuBaoHo, muc Chu y, mo ta ghi so doi tuong', than.loai === 'ThieuMuBaoHo' && than.mucDo === 'Chú ý' && than.moTa.includes('(3 đối tượng)'))
  const tron = [
    sk('va-cham-xe-nguoi', 'va-cham-xe-nguoi:4:1', 'nguy-hiem', { ttc: 1.2 }),
    sk('va-cham-xe-nguoi', 'va-cham-xe-nguoi:4:2', 'nguy-hiem', { ttc: 1.4 }),
    sk('va-cham-xe-nguoi', 'va-cham-xe-nguoi:9:1', 'canh-bao', { ttc: 2.5 }), // canh bao: khong bao phong an ninh
    sk('thieu-ao', 'thieu-ao:2'),
    sk('qua-toc-do', 'qua-toc-do:5', 'canh-bao'), // canh bao: khong bao
  ]
  const n2 = v.gomBaoAnNinh(tron)
  kiem('1 xe tien ve 2 nguoi + thieu ao -> 2 nhom, bo muc canh bao khong can bao', n2.length === 2 && n2.reduce((s, x) => s + x.length, 0) === 3)
  const vc = n2.find((x) => x[0].loai === 'va-cham-xe-nguoi')
  const tVc = v.noiDungBaoAnNinh(vc)
  kiem('nhom va cham: Nguy hiem, khoaTrung dung hop dong', tVc.mucDo === 'Nguy hiểm' && tVc.khoaTrung === 'va-cham-xe-nguoi:4:1' && tVc.loai === 'VaChamXeNguoi')
  const dai = v.noiDungBaoAnNinh([sk('thieu-mu', 'thieu-mu:1', 'canh-bao', { ma: 'x'.repeat(250), moTa: 'y'.repeat(400) })])
  kiem('khoaTrung <= 100 ky tu, moTa <= 300 ky tu', dai.khoaTrung.length === 100 && dai.moTa.length === 300)
  kiem('khong co su kien -> 0 nhom', v.gomBaoAnNinh([]).length === 0 && v.gomBaoAnNinh(undefined).length === 0)
}

// ---------- Loi 29 (kiem lai): hang bao tuan tu co gioi han ----------
{
  const ngu = (ms) => new Promise((ok) => setTimeout(ok, ms))
  const hang = v.taoHangTuanTu(3)
  let dangChay = 0
  let chongCheo = 0
  const xong = []
  const viec = (ten, ms, loi = false) => async () => {
    dangChay++
    chongCheo = Math.max(chongCheo, dangChay)
    await ngu(ms)
    dangChay--
    xong.push(ten)
    if (loi) throw new Error('may chu loi')
  }
  const nhan = [hang.them(viec('a', 15, true)), hang.them(viec('b', 5)), hang.them(viec('c', 5))]
  kiem('hang: nhan du toi da 3 viec', nhan.every(Boolean) && hang.soCho === 3)
  kiem('hang: day -> them() tra false (khong xep vo han)', hang.them(viec('d', 1)) === false && hang.soCho === 3)
  await ngu(80)
  kiem('hang: chay tuan tu dung thu tu, khong chong cheo', chongCheo === 1 && xong.join('') === 'abc')
  kiem('hang: viec loi khong lam dut hang, het viec -> soCho = 0', hang.soCho === 0)
  kiem('hang: rut het thi nhan lai', hang.them(viec('e', 1)) === true)
  await ngu(20)
  kiem('hang: viec sau khi rut het van chay', xong.join('') === 'abce' && hang.soCho === 0)
}

// ---------- Loi 35: CSS khong dinh nghia lai ten cu ----------
{
  const soKeyframe = (ten) => (css.match(new RegExp('@keyframes\\s+' + ten + '\\b', 'g')) || []).length
  kiem('@keyframes an-nhap-nhay chi 1 lan (ban goc opacity)', soKeyframe('an-nhap-nhay') === 1 && /@keyframes an-nhap-nhay\s*\{\s*50%\s*\{\s*opacity:\s*0\.35/.test(css))
  kiem('.an-nhat-ky chi 1 lan, cao 230px', (css.match(/\.an-nhat-ky\s*\{/g) || []).length === 1 && /\.an-nhat-ky\s*\{\s*max-height:\s*230px/.test(css))
  kiem('bang canh bao dung keyframes rieng an-bao-dong-nhay', soKeyframe('an-bao-dong-nhay') === 1 && /\.an-bang-canh-bao\.nguy\s*\{[^}]*an-bao-dong-nhay/.test(css))
  kiem('.an-sai van dung an-nhap-nhay', /\.an-sai\s*\{[^}]*animation:\s*an-nhap-nhay 1s ease-in-out 3/.test(css))
  kiem('nhat ky canh bao nguy hiem dung lop moi an-nhat-ky-at', /\.an-nhat-ky-at\s*\{/.test(css) && jsx.includes('className="an-nhat-ky-at"') && !jsx.includes('"an-nhat-ky"'))
}

// ---------- Kiem tinh tren ma nguon (loi 26, 28, 30, 31, 33, 34 + hop dong) ----------
{
  kiem('loi 26: vong lap doc tuBaoRef / tatTiengRef / viTriRef', jsx.includes('tuBaoRef.current)') && jsx.includes('!tatTiengRef.current') && jsx.includes('viTri: viTriRef.current'))
  kiem('loi 26: khong con doc state tuBao / tatTieng trong vong lap', !/if \(tuBao &&/.test(jsx) && !/if \(!tatTieng\)/.test(jsx))
  kiem('hop dong 1: than POST su co lay tu noiDungBaoAnNinh (co khoaTrung)', jsx.includes('...noiDungBaoAnNinh(nhom)'))
  kiem('loi 29: bao tuan tu qua hangBaoRef (taoHangTuanTu co gioi han)', jsx.includes('hangBaoRef.current = taoHangTuanTu(HANG_BAO_TOI_DA)') && jsx.includes('hang.them(async () =>'))
  kiem('loi 26: toi luot trong hang moi xet lai tuBaoRef (vua tat Tu bao -> lan con cho cung khong gui)', /hang\.them\(async \(\) =>\s*\r?\n?\s*ghiKq\(tuBaoRef\.current \? await baoPhongAnNinh/.test(jsx))
  // kiem lai: webcam bi rut -> the video giu khung cuoi, chup van tra t != null -> phai kiem track TRUOC nhanh t == null
  const iWebcam = jsx.indexOf("getVideoTracks().every((tr) => tr.readyState === 'ended')")
  const iTNull = jsx.indexOf('if (t == null) {', jsx.indexOf('while (chayRef.current === lan)'))
  kiem('kiem lai: phat hien webcam bi rut ca khi chup tra khung dong bang', iWebcam > 0 && iTNull > 0 && iWebcam < iTNull)
  // kiem lai: getUserMedia co the treo mai (bo qua hop hoi quyen) -> khi dang mo van co nut huy, khong khoa ca bang dieu khien
  kiem('kiem lai: dang mo nguon van bam Huy duoc (nut Dung hien khi ban)', jsx.includes('{!ban ? (') && jsx.includes('(bấm để hủy)'))
  kiem('kiem lai: moi lan chay 1 canvas chup rieng', /chupRef\.current = document\.createElement\('canvas'\)\s*\r?\n\s*setDangMo\(false\)/.test(jsx))
  kiem('kiem lai: giai phong ImageBitmap camera IP', jsx.includes("if (nguon === 'ip') nguonAnh.close?.()"))
  kiem('kiem lai: bo chon tep -> xoa urlVideo (khong giu URL blob da thu hoi)', /if \(!tep\) \{\s*\r?\n\s*setUrlVideo\(null\)/.test(jsx) && jsx.includes('return () => URL.revokeObjectURL(u)'))
  kiem('hop dong 2: dung daNhanKetQua (canCauHinh)', jsx.includes('daNhanKetQua(pb, lanGui, r, khungTruoc)'))
  kiem('loi 30: sau getUserMedia kiem chayRef roi tat luong', /getUserMedia[\s\S]{0,300}chayRef\.current !== lan\) \{\s*luong\.getTracks\(\)\.forEach\(\(t\) => t\.stop\(\)\)/.test(jsx))
  kiem('loi 30: nha webcam o dung(), cuoi vong lap, roi trang', (jsx.match(/nhaWebcam\(\)/g) || []).length >= 6)
  kiem('loi 31: ve khung DA GUI (khungRef) truoc lan chup ke', /chepKhung\(chupRef\.current\)\s*\r?\n\s*kqRef\.current = r\s*\r?\n\s*veLai\(\)/.test(jsx))
  kiem('loi 33: ket thuc xoa ket qua cu', /finally \{[\s\S]{0,400}xoaKetQua\(\)/.test(jsx))
  kiem('loi 34: tu thu lai thong-tin AI', jsx.includes('setInterval(() => taiLaiAi({ im: true }), 5000)'))
}

console.log(`Kiem thu giao dien canh bao nguy hiem: ${dat}/${dat + truot} DAT`)
process.exit(truot ? 1 : 0)
