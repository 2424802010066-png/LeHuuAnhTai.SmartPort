/* =====================================================================
   SmartPort PWA — service worker (ban React) — OFFLINE-FIRST cho tram cong

   Quy tac (ung dung dieu hanh cang: KHONG BAO GIO tra so lieu cu thay so that):
     1. /api/*, /suc-khoe, /dich-vu/*, /swagger, /Uploads/* -> KHONG cache, khong dong vao.
                             Du lieu dung khi mat mang do UNG DUNG tu giu trong IndexedDB (hop thu di luot xe,
                             ban sao lenh / eDO co ghi ro "ban sao luc HH:mm") — xem src/ngoaiTuyen.
     2. Dieu huong (mo trang) -> MANG TRUOC, cho toi da 4 giay; mat mang / qua gio / may chu 5xx
                             -> index.html DA CACHE cua dung ban build nay (SPA tu dinh tuyen -> man chot cong
                             /admin/gate/baove mo lai duoc khi mat Wi-Fi, ke ca sau khi tai lai / khoi dong lai may).
                             Chua tung cai vo -> /offline.html.
     3. /assets/* (Vite, ten co ma bam, noi dung khong doi) -> cache truoc. TOAN BO vo ung dung (index.html + moi chunk,
                             CSS, font) duoc cai SAN luc install: danh sach do vite.config.js sinh luc build (BAN_DUNG).
     4. /Content/, /Scripts/ (anh, thu vien AI, mo hinh) -> stale-while-revalidate vao cache 'smartport-react-tinh'
                             (giu qua cac lan cap nhat giao dien).
     5. Activate -> CHI xoa cache do CHINH SW nay tao (tien to 'smartport-react-', 'smartport-v1' cua ban MVC cu),
                             giu lai 1 vo cu cho tab con mo ban cu. KHONG BAO GIO xoa 'sp-models-v1' (mo hinh AI 10-50 MB
                             do src/khu/ai-thi-giac/ortThiGiac.js, an-ninh/anNinhAi.js, kiem-dem/chung/yolo.js tu quan ly):
                             mat cache nay la cong mat AI doc bien khi mat mang.
     6. Cap nhat AN TOAN -> ban moi KHONG tu chiem quyen (khong skipWaiting): cho nguoi dung bam "Tai lai de cap nhat"
                             (postMessage KICH_HOAT) hoac dong het tab. Tab dang mo khong bi tron vo moi + chunk cu.
                             Ngoai le: thay SW doi cu (cache 'smartport-react-v1', khong co vo offline) -> thay ngay.
   ===================================================================== */

var BAN_DUNG = {"phienBan":"06e9ee816240","taiTruoc":["/index.html","/assets/AgentDieuPhoi-RITz8lV7.js","/assets/AgentTuDong-D5eXZ6pS.js","/assets/AiBai-B-LW3LmK.js","/assets/AiCong-Bpm6pw0_.js","/assets/AiContLanh-CX-TswQ-.js","/assets/AiGianLan-Da7GJQs1.js","/assets/AiLab-DpBoaLIo.js","/assets/AiMayChu-U3tj-NgV.js","/assets/AiPhanBen-CSCEBuAQ.js","/assets/AnhWebsite-Pgyp4bhw.js","/assets/AppDiDong-DIAVuAmW.js","/assets/BangKeLuuBai-CkrmJj62.js","/assets/BaoCaoThongKe-3CkM49Sh.js","/assets/BaoLoi-BUZcFWPv.js","/assets/BaoSuCoKhach-Bh3APjPt.js","/assets/BenCang-CXP9S9hX.js","/assets/BienLai-XWQ64KG_.js","/assets/CaiDatKhuonMat-DHSZtOft.js","/assets/CameraAi-CwSHP2o3.js","/assets/CameraIp-mkamF8h3.js","/assets/CangXanh-DfdaxvAx.js","/assets/CanhBaoNguyHiem-yFOX7fzE.js","/assets/ChiTietDon-CPJAQEHU.js","/assets/ChiTietDon-CVdzvBgo.js","/assets/ChiTietThanhToan-CY4NTtIV.js","/assets/ChiTietTinTuc-BgAzoaqF.js","/assets/ChonPhuongThuc-BHM2YJUc.js","/assets/ChotBaoVe-h3-gqXrt.js","/assets/ChuyenTau-Clh9BXa3.js","/assets/CongTaiXe-OWslIa9V.js","/assets/CongThongMinh-wkBIiAng.js","/assets/ContainerLanh-DWaTvorp.js","/assets/DangKy-CmhrK751.js","/assets/DangNhap-BrcP8f4q.js","/assets/DangNhapNgoai-Dqk7IFeX.js","/assets/DangNhapNhanh-9W-hI5LY.js","/assets/DangXuat-D-upXRdC.js","/assets/DanhSachContainer-DAwCXFOy.js","/assets/DanhSachTaiXe-HiLwVnZR.js","/assets/DatDichVu-4QkUDxE_.js","/assets/DauTrangKhach-DRFCWqoh.js","/assets/DichVu-BuF4klU0.js","/assets/DichVuGia-CSwelBy1.js","/assets/DoManhMatKhau-tLFkW3fp.js","/assets/DoiChieuContainer-DlSpooI2.js","/assets/DoiSoatCong-CiqbNuLR.js","/assets/DoiSoatThanhToan-5-8n5nCw.js","/assets/DonCuaToi-CEFKXIXx.js","/assets/DonDatDichVu-o-fj-YiU.js","/assets/DongSuKien-4Q8JLu7z.js","/assets/Edi-JM44LEPH.js","/assets/Edo-CJzwJn2R.js","/assets/Eor-Cm2Ee6fF.js","/assets/FormContainer-D_QOk5HL.js","/assets/FormLenh-CAMm8MZA.js","/assets/GanttBen-CdhDsaZK.js","/assets/GiaTriAi-Ck0CYLlV.js","/assets/GiamSatMicroservices-DTQHOiwR.js","/assets/GiamSatTaiXe-3QMzaf8X.js","/assets/GioiThieu-BWMntjJT.js","/assets/HaiQuan-JlDE5WWt.js","/assets/HangChoAo-DELAYPRl.js","/assets/HangCuaToi-BJTJDJ12.js","/assets/HangNguyHiem-BCAAhpdq.js","/assets/HangTau-C2C0QQ0x.js","/assets/HoSo-BeoJKMGW.js","/assets/HoSoContainer-3_LtUS80.js","/assets/HoSoKhachHang-D-TrHLpU.js","/assets/HoSoXe-BjOckvz0.js","/assets/HoaDonIn-DK-oJgZu.js","/assets/HoaDonInKhach-CDlwKKND.js","/assets/HocMay-B_-wC7AN.js","/assets/HopThuLienHe-CuWr1S83.js","/assets/InEor-BTH0u61N.js","/assets/InLenh-CI2vnyBN.js","/assets/InPhieuCan-BXek4-kX.js","/assets/KeHoachXepDo-CJV1hNor.js","/assets/KhachHang-DuJ1Zt2X.js","/assets/KhoChungTu-EIv_82Ky.js","/assets/KhoMedia-Z3x4r-i6.js","/assets/KhongTimThay-Cqkwddqk.js","/assets/KiemDemAi-BhC7vqYR.js","/assets/LenhGiaoNhan-BCE3_NBg.js","/assets/LichHenCong-CsJgJrpN.js","/assets/LichSuThanhToan-CNhE3GuG.js","/assets/LichTau-BdhH8Xbj.js","/assets/LienHe-_p5Xodrt.js","/assets/MaQrVietQr-C2KXvlod.js","/assets/NhanCoparn-CvsgHu54.js","/assets/NhanLoaiBanTin-pWrBXj4Y.js","/assets/NhanTrangThai-D-sNN2nS.js","/assets/NhatKyCong-BXkMj0fY.js","/assets/NhatKyHoatDong-B3EE9Lkm.js","/assets/NhatKyNhanDien-C-ghWzCH.js","/assets/PhiCong-Bd58WlmC.js","/assets/PhieuEir-CGHbe7PH.js","/assets/PhieuTay-B0JElLv0.js","/assets/PhieuThuLuuBai-BNepkKcW.js","/assets/PhongAnNinh-Bq6tnDah.js","/assets/QuanLySuCo-pA0oJ91s.js","/assets/QuanLyTaiKhoan-DoCvPKm9.js","/assets/QuenMatKhau-BR3Y9LUP.js","/assets/QuetGiayTo-BClfSkwk.js","/assets/QuyTrinhVanHanh-DmWCodZ_.js","/assets/SafePose-CQvqppBE.js","/assets/SoDem-BnZoFRwU.js","/assets/SoDoBai-CkOUPMTb.js","/assets/SoPhieuLuuBai-xM9N8KYb.js","/assets/TaiHoaDon-By9cSMZg.js","/assets/TaiLieuTichHop-06j5lkui.js","/assets/TauBien-9BtxJWAg.js","/assets/TheChiSo-C2XZZL8V.js","/assets/TheXe-CV1YetcG.js","/assets/ThietBiBai-gqSu2pF6.js","/assets/ThongKeLuuLuong-D25IGpM9.js","/assets/TichHop-CDaBzrQG.js","/assets/TinTuc-C1NOdQNu.js","/assets/TinTuc-CfNaG2FZ.js","/assets/TinhTrangCong-El--yTL2.js","/assets/TonRong-Co6yPfEv.js","/assets/TongKetCa-BrO-Cc0U.js","/assets/TongQuan-BEeyQ09-.js","/assets/TraCuuContainer-Bu8Uemsg.js","/assets/TraCuuContainer-CQxQBV77.js","/assets/TramCamera-zuRennpB.js","/assets/TramCan-qnaOfpqz.js","/assets/TrangChu-CJJCwJZO.js","/assets/TrangLoi-DZhP-8pC.js","/assets/TrangThanhToan-51TrcWXs.js","/assets/TrungTamAi-XtrfINrj.js","/assets/Truong-DWlRGxum.js","/assets/UocTinhChiPhi-DZzeFDbQ.js","/assets/VanHanhHeThong-BjSr8uSj.js","/assets/XemBanTin-CfitL7B1.js","/assets/ai-thi-giac-C743JOlb.css","/assets/ai-van-hanh-DmAKDfZ5.css","/assets/ai-van-hanh-hBdbIsUf.js","/assets/an-ninh-DbTGVQN6.css","/assets/bootstrap-icons-BeopsB42.woff","/assets/bootstrap-icons-mSm7cUeB.woff2","/assets/cang-so-CXwIxzou.css","/assets/chung-4LJltA96.js","/assets/chung-8yEbW0aL.js","/assets/chung-BHOR_4hi.css","/assets/chung-D1WrE_UR.js","/assets/client-B9lktsA3.js","/assets/cong-0gIh34VE.css","/assets/container-CnUuqwdt.css","/assets/container-DhisSSnI.js","/assets/dangKyBieuDo-ByuRCQQT.js","/assets/dich-vu-khach-BFhJsziY.css","/assets/dieu-hanh-C06l2KxS.css","/assets/dinhDang-DqcdN4Fl.js","/assets/dungChung-CDSxsm8e.js","/assets/dungChung-DGADWToi.js","/assets/dungChung-DtkyvxBC.js","/assets/edi-depot-CFPiVdGQ.css","/assets/he-thong-CgUNyFdQ.css","/assets/index-DlsUwJLC.js","/assets/index-Dmo1MKaP.css","/assets/jsx-runtime-Dk72oS4N.js","/assets/khach-hang-DVxPBYa6.css","/assets/kiem-dem-C-DL5Sjs.css","/assets/kiem-dem-mz1XWp3L.js","/assets/kinh-doanh-Brx9oxHs.js","/assets/kinh-doanh-CII4gHLu.css","/assets/maQr-BuTvOod-.js","/assets/ocrContainer-BUze4BUd.js","/assets/ortThiGiac-DEPq2zlR.js","/assets/su-co-BoyGyDk_.css","/assets/su-co-D_LGV6If.js","/assets/tai-chinh-DIcKjIDm.css","/assets/tai-xe-BoSab2pJ.css","/assets/tau-ben-CGxc2qu7.css","/assets/ui-C8l6X1BC.js","/assets/useBieuMau-C7Tue_5o.js","/assets/useBoLoc-B9TuHmjN.js","/assets/useWebcam-DE1A5f5J.js"]} // @@SP_BAN_DUNG@@ (sinh lúc build)

var TIEN_TO = 'smartport-react-'
var CACHE_VO = TIEN_TO + 'vo-' + BAN_DUNG.phienBan
var CACHE_TINH = TIEN_TO + 'tinh'
var CACHE_DOI_CU = 'smartport-react-v1' // SW truoc nang cap (xoa het cache khac, khong cache index.html)
var CACHE_MVC_CU = 'smartport-v1'
var VO = '/index.html'
var TRANG_OFFLINE = '/offline.html'
var PHU = [TRANG_OFFLINE, '/favicon.svg', '/manifest.webmanifest', '/Content/img/app-icons/icon-192.png']
var CHO_DIEU_HUONG_MS = 4000

function yeuCau(u) {
  return new Request(u, { cache: 'reload' }) // bo qua bo dem HTTP: lay dung ban tren may chu luc cai
}

self.addEventListener('install', function (e) {
  e.waitUntil(
    caches.open(CACHE_VO).then(function (c) {
      // Phu: thieu file nao bo qua file do
      var phu = Promise.all(
        PHU.map(function (u) {
          return c.add(yeuCau(u)).catch(function () {})
        }),
      )
      // Bat buoc: index.html + moi chunk cua ban build nay — hong 1 file thi cai dat THAT BAI, SW cu van chay voi vo cu
      // (khong bao gio co vo "nua cu nua moi" -> trang trang khi mat mang)
      var batBuoc = BAN_DUNG.taiTruoc.length
        ? c.addAll(BAN_DUNG.taiTruoc.map(yeuCau))
        : c.add(yeuCau(VO)).catch(function () {})
      return Promise.all([phu, batBuoc])
        .then(function () {
          return caches.has(CACHE_DOI_CU)
        })
        .then(function (laSwDoiCu) {
          // SW doi cu khong giu duoc vo offline -> thay ngay; con lai cho nguoi dung bam cap nhat
          if (laSwDoiCu) return self.skipWaiting()
        })
    }),
  )
})

self.addEventListener('activate', function (e) {
  e.waitUntil(
    caches
      .keys()
      .then(function (ks) {
        // caches.keys() tra theo thu tu tao -> phan tu cuoi trong cac vo cu la vo gan nhat
        var voCu = ks.filter(function (k) {
          return k.indexOf(TIEN_TO + 'vo-') === 0 && k !== CACHE_VO
        })
        var giuLai = voCu.length ? voCu[voCu.length - 1] : null
        return Promise.all(
          ks.map(function (k) {
            if (k === CACHE_VO || k === CACHE_TINH || k === giuLai) return null
            // CHI xoa cache cua SmartPort SW. 'sp-models-v1' (mo hinh AI) va moi cache khac: GIU NGUYEN.
            if (k.indexOf(TIEN_TO) === 0 || k === CACHE_MVC_CU) return caches.delete(k)
            return null
          }),
        )
      })
      .then(function () {
        return self.clients.claim()
      }),
  )
})

self.addEventListener('message', function (e) {
  var d = (e && e.data) || {}
  if (d.loai === 'KICH_HOAT') self.skipWaiting()
  else if (d.loai === 'PHIEN_BAN' && e.source) e.source.postMessage({ loai: 'PHIEN_BAN', phienBan: BAN_DUNG.phienBan })
})

function khongDongVao(p) {
  var t = p.toLowerCase()
  return t.indexOf('/api/') === 0 || t === '/suc-khoe' || t.indexOf('/dich-vu/') === 0 || t.indexOf('/swagger') === 0 || t.indexOf('/uploads/') === 0
}

function laTinh(p) {
  return p.indexOf('/Content/') === 0 || p.indexOf('/Scripts/') === 0 || p === '/favicon.svg' || p === '/manifest.webmanifest' || p === TRANG_OFFLINE
}

/** index.html cua ban nay; khong co (vua cap nhat) -> vo nao con giu */
function voDaLuu() {
  return caches
    .open(CACHE_VO)
    .then(function (c) {
      return c.match(VO)
    })
    .then(function (r) {
      return r || caches.match(VO)
    })
    .catch(function () {
      return null
    })
}

function trangDuPhong() {
  return caches.match(TRANG_OFFLINE).then(function (r) {
    return r || new Response('Mất kết nối mạng.', { status: 503, headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
  })
}

function dieuHuong(req) {
  return new Promise(function (traVe) {
    var daTra = false
    var hen = null
    function tra(r) {
      if (daTra || !r) return
      daTra = true
      if (hen) clearTimeout(hen)
      traVe(r)
    }
    // Wi-Fi con bat nhung may chu khong toi duoc: fetch co the treo ca chuc giay -> sau 4 giay tra vo da luu
    hen = setTimeout(function () {
      voDaLuu().then(tra)
    }, CHO_DIEU_HUONG_MS)
    fetch(req).then(
      function (res) {
        if (res.status >= 500)
          return voDaLuu().then(function (r) {
            tra(r || res)
          })
        tra(res)
      },
      function () {
        voDaLuu()
          .then(function (r) {
            return r || trangDuPhong()
          })
          .then(tra)
      },
    )
  })
}

function cacheTruoc(req) {
  return caches.match(req).then(function (hit) {
    if (hit) return hit
    return fetch(req).then(function (res) {
      if (res && res.status === 200 && res.type === 'basic') {
        var ban = res.clone()
        caches
          .open(CACHE_VO)
          .then(function (c) {
            return c.put(req, ban)
          })
          .catch(function () {})
      }
      return res
    })
  })
}

function moiNen(req) {
  return caches.match(req).then(function (hit) {
    var moi = fetch(req)
      .then(function (res) {
        if (res && res.status === 200 && res.type === 'basic') {
          var ban = res.clone()
          caches
            .open(CACHE_TINH)
            .then(function (c) {
              return c.put(req, ban)
            })
            .catch(function () {})
        }
        return res
      })
      .catch(function () {
        return hit || Response.error()
      })
    return hit || moi
  })
}

self.addEventListener('fetch', function (e) {
  var req = e.request
  if (req.method !== 'GET') return

  var url = new URL(req.url)
  if (url.origin !== self.location.origin) return // CDN, anh ngoai: de trinh duyet tu lo

  var p = url.pathname
  // 1) API / suc khoe / file tai len: khong bao gio dong vao
  if (khongDongVao(p)) return

  // 2) Mo trang: mang truoc, mat mang -> vo da cai san
  if (req.mode === 'navigate') {
    e.respondWith(dieuHuong(req))
    return
  }

  // 3) Chunk Vite: cache truoc
  if (p.indexOf('/assets/') === 0) {
    e.respondWith(cacheTruoc(req))
    return
  }

  // 4) Tai nguyen tinh khac (bo yeu cau Range — video tua: de trinh duyet tu lo)
  if (!laTinh(p) || (req.headers && req.headers.has && req.headers.has('range'))) return
  e.respondWith(moiNen(req))
})
