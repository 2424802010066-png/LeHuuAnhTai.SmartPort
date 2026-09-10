/* SmartPort PWA service worker v1
   - Trang (navigation): LUON uu tien mang, mat mang -> offline.html
   - Tai nguyen tinh /Content /Scripts: cache-first (model AI, css, js nang)
   - KHONG cache trang co dang nhap/dong (Admin, API) de khong lo du lieu cu */
var CACHE = 'smartport-v1';
var LOI_OFFLINE = '/offline.html';
var TINH = ['/offline.html', '/Content/site.css', '/Content/img/app-icons/icon-192.png'];

self.addEventListener('install', function (e) {
    e.waitUntil(caches.open(CACHE).then(function (c) { return c.addAll(TINH); }).then(function () { return self.skipWaiting(); }));
});

self.addEventListener('activate', function (e) {
    e.waitUntil(caches.keys().then(function (ks) {
        return Promise.all(ks.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); }));
});

self.addEventListener('fetch', function (e) {
    var req = e.request;
    if (req.method !== 'GET') return;
    var url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    // Trang HTML: mang truoc, offline thi tra trang du phong
    if (req.mode === 'navigate') {
        e.respondWith(fetch(req).catch(function () { return caches.match(LOI_OFFLINE); }));
        return;
    }

    // Tai nguyen tinh: cache-first + tu nap lai nen
    if (url.pathname.indexOf('/Content/') === 0 || url.pathname.indexOf('/Scripts/') === 0) {
        e.respondWith(caches.match(req).then(function (hit) {
            var moi = fetch(req).then(function (res) {
                if (res && res.status === 200) {
                    var ban = res.clone();
                    caches.open(CACHE).then(function (c) { c.put(req, ban); });
                }
                return res;
            }).catch(function () { return hit; });
            return hit || moi;
        }));
    }
});
