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

var BAN_DUNG = { phienBan: 'dev', taiTruoc: [] } // @@SP_BAN_DUNG@@ (vite.config.js thay dong nay luc build)

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
