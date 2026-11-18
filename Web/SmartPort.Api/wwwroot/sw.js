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

var BAN_DUNG = {"phienBan":"de2e923761d2","taiTruoc":["/index.html","/assets/AgentDieuPhoi-DlMnUEGL.js","/assets/AgentTuDong-C-P13-QY.js","/assets/AiBai-yAgbZhrB.js","/assets/AiCong-Y3KSSexU.js","/assets/AiContLanh-DKh_SL_J.js","/assets/AiGianLan-C3Mv5vy9.js","/assets/AiLab-D04jMcfp.js","/assets/AiMayChu-BD_HuaqN.js","/assets/AiPhanBen-BguLENrO.js","/assets/AnhWebsite-2gSEOwOI.js","/assets/AppDiDong-CzOyl2uT.js","/assets/BangKeLuuBai-Xl138tOK.js","/assets/BaoCaoThongKe-DnqNBsYG.js","/assets/BaoLoi-BUZcFWPv.js","/assets/BenCang-DRtkAFJD.js","/assets/BienLai-DSVbPtWo.js","/assets/CaiDatKhuonMat-H8qxqiH5.js","/assets/CameraAi-BjuOtV_D.js","/assets/CameraIp-BwGMYbiR.js","/assets/CangXanh-DqsRAKhs.js","/assets/CanhBaoNguyHiem-DtwDplbN.js","/assets/ChiTietDon-B8dQY8Mz.js","/assets/ChiTietDon-XfOKniMD.js","/assets/ChiTietThanhToan-DGtjveUh.js","/assets/ChiTietTinTuc-SaZH6Fb7.js","/assets/ChonPhuongThuc-j1EWOEwh.js","/assets/ChotBaoVe-CUMv9kSd.js","/assets/ChuyenTau-CCZjANmX.js","/assets/CongTaiXe-Dzf0vudV.js","/assets/CongThongMinh-TcXAs1cd.js","/assets/ContainerLanh-B82nUyqq.js","/assets/DangKy-D_cxqW37.js","/assets/DangNhap-CtLgbC55.js","/assets/DangNhapNgoai-Bnc1qE1j.js","/assets/DangNhapNhanh-hw8DfbOE.js","/assets/DangXuat-CoRaPxYa.js","/assets/DanhSachContainer-B2xPnSI7.js","/assets/DanhSachTaiXe-DX_CyIIF.js","/assets/DatDichVu-DTmqHDCe.js","/assets/DauTrangKhach-B8aNR142.js","/assets/DichVu-bq3PPrKT.js","/assets/DichVuGia-COXwwhmK.js","/assets/DoManhMatKhau-tLFkW3fp.js","/assets/DoiChieuContainer-Cq6ymI5h.js","/assets/DoiSoatCong-Dow_mlyE.js","/assets/DoiSoatThanhToan-DGFHdTYj.js","/assets/DonCuaToi-CTor0keT.js","/assets/DonDatDichVu-FC0JHJUp.js","/assets/DongSuKien-KGZFoIQj.js","/assets/Edi-CDMF4gPE.js","/assets/Edo-CRl9TxRI.js","/assets/Eor-aPsy82la.js","/assets/FormContainer-C4U8XifE.js","/assets/FormLenh-B4WozbIu.js","/assets/GanttBen-DV9XdM6L.js","/assets/GiaTriAi-DVVwHF_v.js","/assets/GiamSatMicroservices-CSsgr6PN.js","/assets/GiamSatTaiXe-nKZt4guH.js","/assets/GioiThieu-BSSXGJuH.js","/assets/HaiQuan-DQQ_dQph.js","/assets/HangChoAo-BIamTbiK.js","/assets/HangCuaToi-Cp0B18j9.js","/assets/HangNguyHiem-CP7T87Tp.js","/assets/HangTau-CvrVULo1.js","/assets/HoSo-Bzj-4h7X.js","/assets/HoSoContainer-NcpS_3d8.js","/assets/HoSoKhachHang-D8FJMAan.js","/assets/HoSoXe-FukqajhT.js","/assets/HoaDonIn-DNQM6ak9.js","/assets/HoaDonInKhach-_frrhLsX.js","/assets/HocMay-CLaoINrH.js","/assets/HopThuLienHe-D5Ffs9H1.js","/assets/InEor-CbO-szAU.js","/assets/InLenh-DejafcA4.js","/assets/InPhieuCan-MybeKobK.js","/assets/KeHoachXepDo-CxGiX4Rr.js","/assets/KhachHang-Cjosht_w.js","/assets/KhoChungTu-Da9rL4ZF.js","/assets/KhoMedia-D86afCYc.js","/assets/KhongTimThay-D82WhMRm.js","/assets/KiemDemAi-B2aby98R.js","/assets/LenhGiaoNhan-C4fHqpVo.js","/assets/LichHenCong-BXP7HRMe.js","/assets/LichSuThanhToan-BeDnx2IN.js","/assets/LichTau-DFHh-jUI.js","/assets/LienHe-DodqbRxS.js","/assets/MaQrVietQr-C2KXvlod.js","/assets/NhanCoparn-D7nZLy2F.js","/assets/NhanLoaiBanTin-pWrBXj4Y.js","/assets/NhanTrangThai-D-sNN2nS.js","/assets/NhatKyCong-BI1cuGbh.js","/assets/NhatKyHoatDong-CPpDgCRC.js","/assets/NhatKyNhanDien-BOhtgrdX.js","/assets/PhiCong-D4ovoIKb.js","/assets/PhieuEir-C8QhHJOS.js","/assets/PhieuTay-Npfxl1hV.js","/assets/PhieuThuLuuBai-Cm49Vs4R.js","/assets/PhongAnNinh-BqEsdCfm.js","/assets/QuanLyTaiKhoan-DC8oyyyv.js","/assets/QuenMatKhau-BENshMIA.js","/assets/QuetGiayTo-DmcI0RQd.js","/assets/QuyTrinhVanHanh-CS3j3eBC.js","/assets/SafePose-CqEdiGdU.js","/assets/SoDem-BnZoFRwU.js","/assets/SoDoBai-QOgd3VD_.js","/assets/SoPhieuLuuBai-BBybTIJN.js","/assets/TaiHoaDon-BunbLUzO.js","/assets/TaiLieuTichHop-CMFfqaXw.js","/assets/TauBien-Dy3KDaLg.js","/assets/TheChiSo-C2XZZL8V.js","/assets/TheXe-DaqQluw_.js","/assets/ThietBiBai-DZgxp0ll.js","/assets/ThongKeLuuLuong-DTO-4X4j.js","/assets/TichHop-BguoOaAI.js","/assets/TinTuc-DgI5-Mw0.js","/assets/TinTuc-DnRkuViZ.js","/assets/TinhTrangCong-CCEoTXP-.js","/assets/TonRong-CSr2ig3J.js","/assets/TongKetCa-C-jk03eI.js","/assets/TongQuan-BXzVJ28X.js","/assets/TraCuuContainer-BNb61Q7T.js","/assets/TraCuuContainer-CcPzyber.js","/assets/TramCamera-BWDql78M.js","/assets/TramCan-DYm94-Gn.js","/assets/TrangChu-CTOgwA4U.js","/assets/TrangLoi-CC8o51Lf.js","/assets/TrangThanhToan-BEJ9A7yR.js","/assets/TrungTamAi-D4yEfxaT.js","/assets/Truong-DWlRGxum.js","/assets/UocTinhChiPhi-6BIpWY3r.js","/assets/VanHanhHeThong-By8hpUf4.js","/assets/XemBanTin-Dh2ijSll.js","/assets/ai-thi-giac-C743JOlb.css","/assets/ai-van-hanh-DmAKDfZ5.css","/assets/ai-van-hanh-hBdbIsUf.js","/assets/an-ninh-DbTGVQN6.css","/assets/bootstrap-icons-BeopsB42.woff","/assets/bootstrap-icons-mSm7cUeB.woff2","/assets/cang-so-CXwIxzou.css","/assets/chung-8yEbW0aL.js","/assets/chung-B1KgGUXy.js","/assets/chung-BHOR_4hi.css","/assets/chung-DlbquG5d.js","/assets/client-B9lktsA3.js","/assets/cong-0gIh34VE.css","/assets/container-CnUuqwdt.css","/assets/container-DYEnRx4J.js","/assets/dangKyBieuDo-ByuRCQQT.js","/assets/dich-vu-khach-BFhJsziY.css","/assets/dieu-hanh-C06l2KxS.css","/assets/dinhDang-DqcdN4Fl.js","/assets/dungChung-CDSxsm8e.js","/assets/dungChung-Cy2LlL25.js","/assets/dungChung-DnOG7nGg.js","/assets/edi-depot-CFPiVdGQ.css","/assets/he-thong-CgUNyFdQ.css","/assets/index-Dmo1MKaP.css","/assets/index-fSIC5NJn.js","/assets/jsx-runtime-Dk72oS4N.js","/assets/khach-hang-DVxPBYa6.css","/assets/kiem-dem-BxOzM5R7.js","/assets/kiem-dem-C-DL5Sjs.css","/assets/kinh-doanh-CII4gHLu.css","/assets/kinh-doanh-QFme249e.js","/assets/maQr-BuTvOod-.js","/assets/ocrContainer-lVRgc8Tr.js","/assets/ortThiGiac-GnGYLYHc.js","/assets/tai-chinh-DIcKjIDm.css","/assets/tai-xe-BoSab2pJ.css","/assets/tau-ben-CGxc2qu7.css","/assets/ui-xUHw10sl.js","/assets/useBieuMau-C7Tue_5o.js","/assets/useBoLoc-D707E1Lr.js","/assets/useWebcam-DE1A5f5J.js"]} // @@SP_BAN_DUNG@@ (sinh lúc build)

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
