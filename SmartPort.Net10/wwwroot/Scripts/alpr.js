/* ============================================================
   SMARTGATE ALPR — Nhan dien BIEN SO XE tai cong cang (v14)
   ------------------------------------------------------------
   Pipeline AI 3 tang (chay 100% tren trinh duyet):
     1. YOLO26n (ONNX Runtime)     : phat hien XE trong khung hinh
     2. CCT-S v2 (fast-plate-ocr)  : model CHUYEN DOC BIEN SO
        (Compact Convolutional Transformer, train tren bien so
        65 quoc gia GOM VIET NAM; file ONNX luu LOCAL trong
        Content/models — chay duoc ca khi mat mang)
     3. Tesseract.js (LSTM OCR)    : engine doc lap de DOI CHIEU
        + du phong khi model chuyen dung khong tai duoc
   Regex bien so VN loc & chuan hoa (15C-274.84 ...). Hai engine
   doc RIENG re — trung nhau moi bao "2/2 khop".
   ============================================================ */
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    var ROOT = window.APP_ROOT || '/';

    var ORT_CDN = ROOT + 'Scripts/vendor/ort/';   // LOCAL — khong can CDN
    var VEN = ROOT + 'Scripts/vendor/';
    var TESS_CDN = VEN + 'tess/tesseract.min.js';   // LOCAL
    var YOLO_URL = ROOT + 'Content/models/yolo26n.onnx';   // LOCAL
    var INPUT = 640;
    // Nhom lop phuong tien trong COCO: 2=car 5=bus 7=truck
    var VEHICLE_CLS = { 2: 'Ô tô', 5: 'Xe khách', 7: 'Đầu kéo container' };

    var session = null, worker = null, busy = false, stream = null;
    var lastPlate = null;      // { text, conf, loai, nguon, raw }
    var anhXe = null;          // anh chup xe (dataURL) — luu kem nhat ky lam bang chung
    var hongHoc = null;        // ket qua soi ngoai quan {pct, x, y, w, h}
    var lenhKhop = null;       // lenh giao nhan dien tu khop bien so (eGate Pass)
    var els = {};

    function status(html, spin) {
        els.status.innerHTML = (spin ? '<span class="spinner-border spinner-border-sm me-2"></span>' : '') + html;
    }
    function loadScript(src) {
        return new Promise(function (ok, err) {
            var s = document.createElement('script');
            s.src = src; s.onload = ok; s.onerror = err;
            document.head.appendChild(s);
        });
    }

    /* ---------------- 1. Khoi dong 3 engine AI ---------------- */
    function init() {
        status('Đang tải YOLO26 + CCT-S v2 + Tesseract OCR (lần đầu hơi lâu)...', true);
        var pOrt = loadScript(ORT_CDN + 'ort.min.js')
            .then(function () { ort.env.wasm.wasmPaths = ORT_CDN; });
        var pYolo = pOrt
            .then(function () { return ort.InferenceSession.create(YOLO_URL, { executionProviders: ['wasm'] }); })
            .then(function (s) { session = s; });
        // Model chuyen doc bien so — file LOCAL, hong thi van con Tesseract
        var pBien = pOrt.then(napBienOcr).catch(function () { bienSession = null; });

        var pOcr = loadScript(TESS_CDN)
            .then(function () {
                return Tesseract.createWorker('eng', 1, { workerPath: VEN + 'tess/worker.min.js', corePath: VEN + 'tess-core', langPath: VEN + 'tessdata', logger: function () { } });
            })
            .then(function (w) {
                worker = w;
                // Bien so VN chi gom A-Z 0-9 . -
                return worker.setParameters({
                    tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-',
                    preserve_interword_spaces: '0'
                });
            });

        // Cho TAT CA engine "an dinh" xong roi moi ket luan (khong dung
        // Promise.all/catch: 1 engine fail som se ket luan oan khi engine khac
        // van dang tai — chi can worker HOAC bienSession la doc bien duoc)
        function anDinh(p) { return p.then(function () { return true; }, function () { return false; }); }
        Promise.all([anDinh(pYolo), anDinh(pOcr), anDinh(pBien)])
            .then(function (ok) {
                var coYolo = ok[0], coTess = ok[1] && !!worker, coCct = !!bienSession;
                if (!coTess && !coCct) {
                    status('❌ Không tải được engine đọc biển — kiểm tra Internet rồi tải lại trang.');
                    return;
                }
                var ten = [];
                if (coYolo) ten.push('YOLO26n');
                if (coCct) ten.push('CCT-S v2');
                if (coTess) ten.push('Tesseract');
                els.badge.innerHTML = '<i class="bi bi-cpu-fill me-1"></i>' + ten.join(' + ');
                els.badge.className = 'badge-st ' + (coYolo && coCct && coTess ? 'st-ok' : 'st-warn');
                status((coYolo ? '✅ Hệ thống ALPR sẵn sàng!' : '⚠️ YOLO26 không tải được — vẫn đọc biển trên toàn khung hình.') +
                    (!coTess ? ' (Tesseract lỗi — dùng model chuyên dụng CCT-S v2 local)' : '') +
                    (!coCct ? ' (model chuyên dụng lỗi — dùng Tesseract)' : '') +
                    ' Chọn ảnh mẫu, tải ảnh xe hoặc bật camera làn vào.');
                enableUi(true);
            });
    }
    function enableUi(on) {
        ['fileInput', 'btnCam', 'btnShot'].forEach(function (id) {
            if ($(id)) $(id).disabled = !on;
        });
        document.querySelectorAll('.gs-sample').forEach(function (b) { b.disabled = !on; });
    }

    /* ============================================================
       1b. CCT-S v2 — MODEL CHUYEN DOC BIEN SO (fast-plate-ocr)
       Compact Convolutional Transformer train rieng cho bien so
       (65 quoc gia, co Viet Nam). Vao: anh RGB 128x64 (uint8, model
       tu chuan hoa). Ra: 10 slot x 37 ky tu '0-9A-Z_' + xac suat
       tung ky tu. Chinh xac hon han OCR da dung tren anh bien nho/mo.
       ============================================================ */
    var bienSession = null;
    var BIEN_MODEL = ROOT + 'Content/models/cct_s_v2_global.onnx';
    var BIEN_ALPHABET = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_';
    var BIEN_SLOTS = 10, BIEN_W = 128, BIEN_H = 64;

    function napBienOcr() {
        return ort.InferenceSession.create(BIEN_MODEL, { executionProviders: ['wasm'] })
            .then(function (s) { bienSession = s; });
    }

    /** Doc 1 vung bien bang model chuyen dung. gheHang=true: bien 2 hang ->
        cat doi tren/duoi, ghep thanh 1 hang ngang truoc khi dua vao model. */
    function docBienCCT(srcCanvas, rect, gheHang) {
        if (!bienSession) return Promise.resolve(null);
        try {
            var c = document.createElement('canvas');
            c.width = BIEN_W; c.height = BIEN_H;
            var cx = c.getContext('2d');
            var pad = 0.05;
            var sx = Math.max(0, rect.x - rect.w * pad);
            var sy = Math.max(0, rect.y - rect.h * pad);
            var sw = Math.min(srcCanvas.width - sx, rect.w * (1 + 2 * pad));
            var sh = Math.min(srcCanvas.height - sy, rect.h * (1 + 2 * pad));
            if (sw < 8 || sh < 8) return Promise.resolve(null);
            if (gheHang) {
                cx.drawImage(srcCanvas, sx, sy, sw, sh / 2, 0, 0, BIEN_W / 2, BIEN_H);
                cx.drawImage(srcCanvas, sx, sy + sh / 2, sw, sh / 2, BIEN_W / 2, 0, BIEN_W / 2, BIEN_H);
            } else {
                cx.drawImage(srcCanvas, sx, sy, sw, sh, 0, 0, BIEN_W, BIEN_H);
            }
            var d = cx.getImageData(0, 0, BIEN_W, BIEN_H).data;
            var arr = new Uint8Array(BIEN_H * BIEN_W * 3);
            for (var i = 0, p = 0; i < d.length; i += 4) {
                arr[p++] = d[i]; arr[p++] = d[i + 1]; arr[p++] = d[i + 2];
            }
            var feeds = {};
            feeds[bienSession.inputNames[0]] = new ort.Tensor('uint8', arr, [1, BIEN_H, BIEN_W, 3]);
            return bienSession.run(feeds).then(function (out) {
                // v2 co 2 dau ra (chu bien + quoc gia) -> chon tensor 10x37
                var o = null;
                for (var k = 0; k < bienSession.outputNames.length; k++) {
                    var t = out[bienSession.outputNames[k]];
                    if (!t || !t.dims) continue;
                    var n = 1;
                    for (var q = 0; q < t.dims.length; q++) n *= t.dims[q];
                    if (n === BIEN_SLOTS * BIEN_ALPHABET.length) { o = t; break; }
                }
                if (!o) return null;
                var text = '', sum = 0, dem = 0, minC = 1;
                for (var s2 = 0; s2 < BIEN_SLOTS; s2++) {
                    var best = -1, bi = 0;
                    for (var a2 = 0; a2 < BIEN_ALPHABET.length; a2++) {
                        var v = Number(o.data[s2 * BIEN_ALPHABET.length + a2]);
                        if (v > best) { best = v; bi = a2; }
                    }
                    var ch = BIEN_ALPHABET[bi];
                    if (ch !== '_') {
                        text += ch; sum += best; dem++;
                        if (best < minC) minC = best;
                    }
                }
                if (!dem) return null;
                return { text: text, conf: sum / dem, minConf: minC };
            }).catch(function () { return null; });
        } catch (e) { return Promise.resolve(null); }
    }

    /** Thu model chuyen dung tren TAT CA ung vien vung bien, lay ban tot nhat */
    function docBienChuyen(srcCanvas, ungVien) {
        if (!bienSession || !ungVien.length) return Promise.resolve(null);
        var thu = [];
        ungVien.forEach(function (uv) {
            thu.push({ rect: uv, ghe: false });
            if (uv.haiHang) thu.push({ rect: uv, ghe: true });
        });
        var best = null;
        function chay(i) {
            if (i >= thu.length) return Promise.resolve(best);
            return docBienCCT(srcCanvas, thu[i].rect, thu[i].ghe).then(function (r) {
                if (window.__alprDebug) window.__alprDebug.lanThu.push({
                    ten: 'cct/' + thu[i].rect.mau + (thu[i].ghe ? '/ghep-hang' : ''),
                    raw: r ? r.text : '', conf: r ? Math.round(r.conf * 100) : 0
                });
                if (r && r.text.length >= 5 && r.text.length <= 10) {
                    r.plate = timBienSo(r.text);
                    var diem = r.conf + (r.plate ? 0.15 : 0) + (thu[i].rect.mau === 'vang' ? 0.02 : 0);
                    if (!best || diem > best.diem) {
                        r.diem = diem; r.rect = thu[i].rect; r.ghe = thu[i].ghe;
                        best = r;
                    }
                }
                return chay(i + 1);
            });
        }
        return chay(0);
    }

    /* ---------------- 2. YOLO: tim bbox XE lon nhat ---------------- */
    function detectVehicle(imgEl, w, h) {
        if (!session) return Promise.resolve(null);
        var cv = els.work, ctx = cv.getContext('2d');
        cv.width = INPUT; cv.height = INPUT;
        var sc = Math.min(INPUT / w, INPUT / h);
        var nw = Math.round(w * sc), nh = Math.round(h * sc);
        var dx = (INPUT - nw) / 2, dy = (INPUT - nh) / 2;
        ctx.fillStyle = '#727272';
        ctx.fillRect(0, 0, INPUT, INPUT);
        ctx.drawImage(imgEl, dx, dy, nw, nh);

        var d = ctx.getImageData(0, 0, INPUT, INPUT).data;
        var arr = new Float32Array(3 * INPUT * INPUT);
        for (var i = 0, p = 0; i < d.length; i += 4, p++) {
            arr[p] = d[i] / 255;
            arr[p + INPUT * INPUT] = d[i + 1] / 255;
            arr[p + 2 * INPUT * INPUT] = d[i + 2] / 255;
        }
        var tensor = new ort.Tensor('float32', arr, [1, 3, INPUT, INPUT]);
        var feeds = {}; feeds[session.inputNames[0]] = tensor;

        return session.run(feeds).then(function (out) {
            var o = out[session.outputNames[0]];
            var best = null;
            // Output [1,300,6]: x1 y1 x2 y2 score cls
            for (var k = 0; k < o.dims[1]; k++) {
                var b = k * 6;
                var score = o.data[b + 4], cls = Math.round(o.data[b + 5]);
                if (score < 0.35 || !VEHICLE_CLS[cls]) continue;
                var x1 = (o.data[b] - dx) / sc, y1 = (o.data[b + 1] - dy) / sc;
                var x2 = (o.data[b + 2] - dx) / sc, y2 = (o.data[b + 3] - dy) / sc;
                var area = (x2 - x1) * (y2 - y1);
                if (!best || area > best.area)
                    best = { x1: Math.max(0, x1), y1: Math.max(0, y1), x2: Math.min(w, x2), y2: Math.min(h, y2), area: area, score: score, loai: VEHICLE_CLS[cls] };
            }
            return best;
        });
    }

    /* ---------------- 3. OCR bien so trong vung anh ---------------- */
    // Chuan hoa + regex bien so VN: "15C-274.84", "29C-468.28", "51D-12345"
    var RE_PLATE = /(\d{2})[\s\-.]?([A-Z]{1,2})[\s\-.]?(\d{3})[\s.]?(\d{2})|(\d{2})[\s\-.]?([A-Z]{1,2})[\s\-.]?(\d{4,5})/;

    function _khop(t) {
        var m = t.match(RE_PLATE);
        if (!m) return null;
        if (m[1]) return m[1] + m[2] + '-' + m[3] + '.' + m[4];      // 15C-274.84
        var so = m[7];
        if (so.length === 5) return m[5] + m[6] + '-' + so.substring(0, 3) + '.' + so.substring(3);
        return m[5] + m[6] + '-' + so;                               // 29C-4682
    }

    /* OCR hay nham chu <-> so vi hinh dang giong nhau. Biển số VN co cau
       truc CO DINH (2 so + 1-2 chu + 4-5 so) nen ta thu cac hoan doi pho
       bien de "cuu" nhung lan doc gan dung. */
    var THAY_SO = { 'O': '0', 'Q': '0', 'D': '0', 'I': '1', 'L': '1', 'J': '1', 'Z': '2', 'S': '5', 'B': '8', 'G': '6', 'T': '7', 'A': '4' };
    var THAY_CHU = { '0': 'D', '1': 'L', '8': 'B', '5': 'S', '6': 'G', '2': 'Z' };

    /* Bien KHONG theo chuan Viet Nam: xe nuoc ngoai (Campuchia/Lao qua cua
       khau), xe chuyen dung noi bo cang, bien tam... Van phai ghi nhan duoc.
       Lay "tu" giong bien so nhat: 4-10 ky tu chu-so, co it nhat 2 chu so. */
    function timBienTuDo(rawText) {
        var t = (rawText || '').toUpperCase().replace(/[^A-Z0-9.\-]/g, ' ');
        var tu = t.split(/\s+/).filter(function (s) { return s.length > 0; });
        var best = null, bestDiem = 0;
        tu.forEach(function (s) {
            var sach = s.replace(/[^A-Z0-9]/g, '');
            if (sach.length < 4 || sach.length > 10) return;
            var soChuSo = (sach.match(/[0-9]/g) || []).length;
            var soChu = (sach.match(/[A-Z]/g) || []).length;
            if (soChuSo < 2) return;                       // bien so phai co so
            // Uu tien: co ca chu lan so, do dai 6-9 (nhu bien that)
            var diem = soChuSo * 2 + (soChu > 0 ? 12 : 0) +
                       (sach.length >= 6 && sach.length <= 9 ? 15 : 0);
            if (diem > bestDiem) { bestDiem = diem; best = s; }
        });
        return best ? suaOSo(best) : null;
    }

    /* Sua nham 0 <-> O theo NGU CANH: chu "O" va so "0" gan nhu giong het
       nhau. Neu mot so "0" dung NGAY SAU mot CHU CAI va KHONG co so theo
       sau, gan nhu chac chan do la chu "O" (vd doc "N0.175B" -> "NO.175B").
       Nguoc lai, "O" ke giua cac chu so thi la so "0". */
    function suaOSo(s) {
        var a = s.split('');
        for (var i = 0; i < a.length; i++) {
            var truoc = a[i - 1] || '', sau = a[i + 1] || '';
            if (a[i] === '0' && /[A-Z]/.test(truoc) && !/[0-9]/.test(sau)) a[i] = 'O';
            else if (a[i] === 'O' && /[0-9]/.test(truoc) && /[0-9]/.test(sau)) a[i] = '0';
        }
        return a.join('');
    }

    function timBienSo(rawText) {
        var t = (rawText || '').toUpperCase().replace(/[^A-Z0-9.\-\s]/g, ' ');
        var kq = _khop(t);
        if (kq) return kq;

        // Thu 1: ep TAT CA ky tu de nham thanh SO roi khop lai
        var t2 = t.replace(/[OQDILJZSBGTA]/g, function (c) { return THAY_SO[c] || c; });
        kq = _khop(t2);
        if (kq) return kq;

        // Thu 2: quet tung "tu" — sua theo dung VI TRI (so-chu-so)
        var tu = t.split(/\s+/);
        for (var i = 0; i < tu.length; i++) {
            var s = tu[i].replace(/[^A-Z0-9]/g, '');
            if (s.length < 6 || s.length > 9) continue;
            var out = '';
            for (var j = 0; j < s.length; j++) {
                var c = s[j];
                // 2 ky tu dau = MA TINH (so) · ky tu thu 3 (4) = SERI (chu) · con lai = so
                if (j < 2) out += (THAY_SO[c] || c);
                else if (j === 2) out += (THAY_CHU[c] || c);
                else if (j === 3 && /[A-Z]/.test(c) && /[0-9]/.test(s[j + 1] || '')) out += c;
                else out += (THAY_SO[c] || c);
            }
            kq = _khop(out);
            if (kq) return kq;
        }
        return null;
    }

    /* ---- TU DINH VI VUNG BIEN SO (trang VA vang) ----
       Biển số Việt Nam có 2 nền:
         · NỀN TRẮNG chữ đen  — xe cá nhân
         · NỀN VÀNG  chữ đen  — xe KINH DOANH VẬN TẢI (đầu kéo container,
           xe tải chở hàng) theo Thông tư 58/2020 — chính là loại xe ra vào
           cảng nhiều nhất! Bản cũ chỉ dò nền trắng nên biển vàng KHÔNG BAO
           GIỜ được định vị, OCR phải đọc cả thân xe -> ra chuỗi rác.
       Quét lưới 16px, gom cụm liền kề, chấm điểm theo hình dáng biển số
       (tỉ lệ ngang 1.8–7 cho biển 1 hàng, 1.0–1.8 cho biển 2 hàng) rồi trả
       về TOP ứng viên để thử OCR lần lượt.  */
    function timVungBien(srcCanvas) {
        var w = srcCanvas.width, h = srcCanvas.height;
        if (w < 64 || h < 64) return [];
        var CELL = 16;
        var gw = Math.floor(w / CELL), gh = Math.floor(h / CELL);
        if (gw < 3 || gh < 3) return [];
        var img = srcCanvas.getContext('2d').getImageData(0, 0, w, h).data;

        // 2 lưới riêng: nền trắng và nền vàng
        var luoi = { trang: [], vang: [] };
        for (var gy = 0; gy < gh; gy++) {
            luoi.trang.push(new Array(gw).fill(0));
            luoi.vang.push(new Array(gw).fill(0));
            for (var gx = 0; gx < gw; gx++) {
                var soTrang = 0, soVang = 0, tong = 0;
                for (var y = gy * CELL; y < (gy + 1) * CELL; y += 4) {
                    for (var x = gx * CELL; x < (gx + 1) * CELL; x += 4) {
                        var i = (y * w + x) * 4;
                        var r = img[i], g = img[i + 1], b = img[i + 2];
                        var mx = Math.max(r, g, b), mn = Math.min(r, g, b);
                        if (mn > 140 && (mx - mn) < 60) soTrang++;
                        // VÀNG: đỏ & lục cao, lam thấp rõ rệt (hue ~40–60°)
                        else if (r > 120 && g > 95 && b < g - 35 && (r - b) > 55 && Math.abs(r - g) < 85) soVang++;
                        tong++;
                    }
                }
                luoi.trang[gy][gx] = soTrang / tong > 0.38 ? 1 : 0;
                luoi.vang[gy][gx] = soVang / tong > 0.34 ? 1 : 0;
            }
        }

        var ungVien = [];
        ['vang', 'trang'].forEach(function (mau) {          // ưu tiên VÀNG (xe cảng)
            var grid = luoi[mau], seen = {};
            for (var sy = 0; sy < gh; sy++) {
                for (var sx = 0; sx < gw; sx++) {
                    if (!grid[sy][sx] || seen[sy + ',' + sx]) continue;
                    var stack = [[sx, sy]], minX = sx, maxX = sx, minY = sy, maxY = sy, size = 0;
                    while (stack.length) {
                        var p = stack.pop();
                        var key = p[1] + ',' + p[0];
                        if (seen[key]) continue;
                        seen[key] = 1;
                        if (p[0] < 0 || p[0] >= gw || p[1] < 0 || p[1] >= gh || !grid[p[1]][p[0]]) continue;
                        size++;
                        minX = Math.min(minX, p[0]); maxX = Math.max(maxX, p[0]);
                        minY = Math.min(minY, p[1]); maxY = Math.max(maxY, p[1]);
                        stack.push([p[0] + 1, p[1]]); stack.push([p[0] - 1, p[1]]);
                        stack.push([p[0], p[1] + 1]); stack.push([p[0], p[1] - 1]);
                    }
                    var cw = (maxX - minX + 1), ch = (maxY - minY + 1);
                    var ar = cw / ch;
                    // Loại cụm quá bé, cụm chiếm cả khung (trời/tường sáng)
                    if (size < 3 || size > gw * gh * 0.30) continue;
                    var mot_hang = ar >= 1.8 && ar <= 7.5;
                    var hai_hang = ar >= 0.95 && ar < 1.8;      // biển vuông 2 hàng của xe tải
                    if (!mot_hang && !hai_hang) continue;
                    // Điểm ưu tiên: biển vàng > trắng, hình 1 hàng > 2 hàng, cụm to hơn tốt hơn
                    var diem = size + (mau === 'vang' ? 60 : 0) + (mot_hang ? 20 : 0);
                    ungVien.push({
                        diem: diem, size: size, mau: mau, haiHang: hai_hang,
                        // Pad rộng tay để KHÔNG cắt mất ký tự mảnh ở mép (vd số "1")
                        x: Math.max(0, minX * CELL - 14), y: Math.max(0, minY * CELL - 10),
                        w: Math.min(w, cw * CELL + 28), h: Math.min(h, ch * CELL + 20)
                    });
                }
            }
        });
        ungVien.sort(function (a, b) { return b.diem - a.diem; });
        return ungVien.slice(0, 3);        // thử tối đa 3 vùng khả dĩ nhất
    }

    /* ---- TIEN XU LY OCR — grayscale + keo gian tuong phan + phong to ----
       nhiPhan=true: them buoc NHI PHAN Otsu (chu den tuyet doi tren nen
       trang) — cach doc thu 2 de DOI CHIEU voi cach thu 1. */
    function chuanBiOCR(srcCanvas, rect, scale, nhiPhan) {
        var big = document.createElement('canvas');
        var sw = rect ? rect.w : srcCanvas.width, sh = rect ? rect.h : srcCanvas.height;
        var sx = rect ? rect.x : 0, sy = rect ? rect.y : 0;
        big.width = Math.round(sw * scale); big.height = Math.round(sh * scale);
        var bctx = big.getContext('2d');
        bctx.imageSmoothingEnabled = true;
        bctx.drawImage(srcCanvas, sx, sy, sw, sh, 0, 0, big.width, big.height);
        var im = bctx.getImageData(0, 0, big.width, big.height);
        var d = im.data;
        // Grayscale + KEO GIAN TUONG PHAN (percentile 5-95) — lam ro chu
        // ma KHONG nhi phan cung (nhi phan de "nuot" net manh nhu so 1)
        var hist = new Array(256).fill(0);
        for (var i = 0; i < d.length; i += 4) {
            var gr = Math.round(0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2]);
            d[i] = gr; hist[gr]++;
        }
        var total = d.length / 4;
        var lo = 0, hi = 255, acc = 0;
        for (var t = 0; t < 256; t++) { acc += hist[t]; if (acc >= total * 0.05) { lo = t; break; } }
        acc = 0;
        for (var t2 = 255; t2 >= 0; t2--) { acc += hist[t2]; if (acc >= total * 0.05) { hi = t2; break; } }
        var range = Math.max(20, hi - lo);
        for (var j = 0; j < d.length; j += 4) {
            var v = Math.max(0, Math.min(255, Math.round((d[j] - lo) * 255 / range)));
            d[j] = d[j + 1] = d[j + 2] = v;
        }

        /* LAM NET (unsharp mask nhe): anh phong to bi nhoe do noi suy, lam
           net lai giup Tesseract tach ky tu dinh nhau (vd "51" vs "9"). */
        if (big.width * big.height < 4000000) {
            var goc = new Uint8ClampedArray(d.length);
            goc.set(d);
            var W = big.width, H = big.height;
            for (var y2 = 1; y2 < H - 1; y2++) {
                for (var x2 = 1; x2 < W - 1; x2++) {
                    var p0 = (y2 * W + x2) * 4;
                    var xungQuanh = goc[p0 - 4] + goc[p0 + 4] + goc[(y2 - 1) * W * 4 + x2 * 4] + goc[(y2 + 1) * W * 4 + x2 * 4];
                    // 5*giua - 4*lan can = nhan manh bien canh
                    var net = 5 * goc[p0] - xungQuanh;
                    var vv = Math.max(0, Math.min(255, Math.round(0.45 * net + 0.55 * goc[p0])));
                    d[p0] = d[p0 + 1] = d[p0 + 2] = vv;
                }
            }
        }

        if (nhiPhan) {
            // Nguong Otsu tren anh da keo gian — tach chu/nen dut khoat
            var h2 = new Array(256).fill(0), n2 = 0;
            for (var k = 0; k < d.length; k += 4) { h2[d[k]]++; n2++; }
            var tong = 0;
            for (var t3 = 0; t3 < 256; t3++) tong += t3 * h2[t3];
            var tongB = 0, wB = 0, maxVar = -1, nguong = 128;
            for (var t4 = 0; t4 < 256; t4++) {
                wB += h2[t4];
                if (wB === 0) continue;
                var wF = n2 - wB;
                if (wF === 0) break;
                tongB += t4 * h2[t4];
                var mB = tongB / wB, mF = (tong - tongB) / wF;
                var giua = wB * wF * (mB - mF) * (mB - mF);
                if (giua > maxVar) { maxVar = giua; nguong = t4; }
            }
            for (var m = 0; m < d.length; m += 4) {
                var bw = d[m] > nguong ? 255 : 0;
                d[m] = d[m + 1] = d[m + 2] = bw;
            }
        }
        bctx.putImageData(im, 0, 0);
        return big;
    }

    /* Rut danh sach TUNG KY TU kem do tin cay tu ket qua Tesseract v5
       (blocks -> paragraphs -> lines -> words -> symbols). */
    function layKyTu(data) {
        var ds = [];
        (data.blocks || []).forEach(function (b) {
            (b.paragraphs || []).forEach(function (p) {
                (p.lines || []).forEach(function (l) {
                    (l.words || []).forEach(function (w) {
                        (w.symbols || []).forEach(function (s) {
                            ds.push({ t: s.text, c: s.confidence || 0 });
                        });
                    });
                });
            });
        });
        return ds;
    }

    /* LOC KY TU RAC: Tesseract hay "bia" them 1 ky tu o DAU/CUOI do viền
       biển số, ốc vít, hoa văn ca-rô... Nhung ky tu do luon co do tin cay
       THAP han cac ky tu that. Cat bo dan tu 2 dau khi conf < 55 va cat
       moi ky tu conf < 32 o giua. */
    function locKyTuRac(ds) {
        var a = ds.slice();
        while (a.length > 4 && a[0].c < 55) a.shift();
        while (a.length > 4 && a[a.length - 1].c < 55) a.pop();
        a = a.filter(function (s) { return s.c >= 32; });
        var txt = a.map(function (s) { return s.t; }).join('').trim();
        var tb = a.length ? a.reduce(function (t, s) { return t + s.c; }, 0) / a.length : 0;
        return { text: txt, conf: tb, soKyTu: a.length };
    }

    /** Doc 1 vung anh voi 1 che do PSM — tra {plate, conf, raw} */
    function docVung(srcCanvas, rect, psm, scale, nhiPhan) {
        // Tesseract chua tai duoc (mat mang) -> tra rong, CCT-S v2 local van doc duoc
        if (!worker) return Promise.resolve({ plate: null, conf: 0, raw: '', rect: rect });
        var cv = chuanBiOCR(srcCanvas, rect, scale, nhiPhan);
        return worker.setParameters({ tessedit_pageseg_mode: psm })
            .then(function () {
                // Yeu cau tra ve CA CAY blocks de doc duoc do tin cay tung ky tu
                return worker.recognize(cv.toDataURL('image/png'), {}, { text: true, blocks: true });
            })
            .then(function (r) {
                var text = (r.data.text || '').trim();
                var conf = r.data.confidence || 0;

                // Uu tien chuoi DA LOC ky tu rac (neu Tesseract tra ve blocks)
                var kyTu = layKyTu(r.data);
                var sach = null;
                if (kyTu.length >= 4) {
                    sach = locKyTuRac(kyTu);
                    if (sach.text && sach.conf > 0) conf = sach.conf;
                }
                var thoRaw = sach && sach.text ? sach.text : text;

                var plate = timBienSo(thoRaw) || timBienSo(text);
                // Neu doc nhieu dong, lay do tin cay cua dong CHUA bien so
                var lines = r.data.lines || [];
                for (var i = 0; i < lines.length; i++) {
                    if (timBienSo(lines[i].text)) { conf = lines[i].confidence; break; }
                }
                return {
                    plate: plate, conf: Math.round(conf * 10) / 10,
                    raw: thoRaw, rawGoc: text, rect: rect
                };
            })
            .catch(function () { return { plate: null, conf: 0, raw: '', rect: rect }; });
    }

    /**
     * Doc bien so: thu LAN LUOT tung vung ung vien (bien vang truoc), moi
     * vung thu 2 che do doc (1 dong / nhieu dong) — dung ngay khi khop
     * dinh dang bien so VN. Het ung vien thi doc ca vung xe lam du phong.
     */
    function ocrRegion(srcCanvas) {
        var ungVien = timVungBien(srcCanvas);
        window.__alprDebug = {
            srcW: srcCanvas.width, srcH: srcCanvas.height,
            soUngVien: ungVien.length, ungVien: ungVien, lanThu: []
        };

        // Xay chuoi thu tuan tu: moi ung vien x nhieu che do doc.
        // TU DONG CHON HE SO PHONG: Tesseract doc tot nhat khi chieu cao chu
        // ~40-60px, nen vung bien cang nho thi phong cang nhieu (2x..6x).
        var buoc = [];
        ungVien.forEach(function (uv) {
            var sc = Math.max(2, Math.min(6, Math.round(150 / Math.max(12, uv.h))));
            buoc.push({ rect: uv, psm: uv.haiHang ? '6' : '7', scale: sc, ten: uv.mau + (uv.haiHang ? '/2hang' : '/1hang') });
            buoc.push({ rect: uv, psm: uv.haiHang ? '7' : '6', scale: sc, ten: uv.mau + '/doi-che-do' });
            buoc.push({ rect: uv, psm: '8', scale: sc, ten: uv.mau + '/mot-tu' });   // PSM 8: cả biển là 1 "từ"
        });
        buoc.push({ rect: null, psm: '6', scale: 2, ten: 'toan-vung-xe' });

        /* CHAM DIEM ket qua khong khop chuan VN, de con biet cai nao "giong
           bien so" nhat ma bao cho nhan vien — TRUOC DAY chi lay chuoi DAI
           NHAT nen hay chon nham chu doc duoc tu ca can phong/than xe. */
        function chamDiem(r, coRect) {
            var bien = timBienTuDo(r.raw);
            if (!bien) return { diem: coRect ? 1 : 0, bien: '' };
            var sach = bien.replace(/[^A-Z0-9]/g, '');
            var soChuSo = (sach.match(/[0-9]/g) || []).length;
            var soChu = (sach.match(/[A-Z]/g) || []).length;
            var diem = 0;
            if (coRect) diem += 100;                       // đọc từ VÙNG BIỂN đã khoanh -> tin hơn hẳn
            if (sach.length >= 6 && sach.length <= 9) diem += 40;
            if (soChuSo >= 3) diem += 25;
            if (soChu >= 1 && soChu <= 3) diem += 20;      // biển thật ít chữ cái
            diem += Math.min(30, (r.conf || 0) * 0.3);
            return { diem: diem, bien: bien };
        }

        var tot = null, totDiem = -1;       // ket qua "giong bien so" nhat

        /* Khi khong khop chuan VN: van DOI CHIEU 2 cach doc tren dung vung
           bien tot nhat — 2 cach trung nhau thi yen tam, lech nhau thi bao
           nhan vien kiem tra (truoc day chi doc 1 lan roi tin luon). */
        function xacNhanTuDo() {
            if (!tot || !tot.rect || !tot.bienTuDo) return Promise.resolve(tot);
            var uv = tot.rect;
            var sc = Math.max(2, Math.min(6, Math.round(150 / Math.max(12, uv.h))));
            return docVung(srcCanvas, uv, uv.haiHang ? '6' : '7', sc, true).then(function (r2) {
                var b2 = timBienTuDo(r2.raw);
                window.__alprDebug.lanThu.push({ ten: 'xac-nhan/nhi-phan', psm: '7', raw: r2.raw, plate: b2 });
                if (b2 && b2 === tot.bienTuDo) tot.dongThuan = true;
                else if (b2 && r2.conf > tot.conf + 8) { tot.khac = tot.bienTuDo; tot.bienTuDo = b2; tot.conf = r2.conf; tot.dongThuan = false; }
                else if (b2) { tot.khac = b2; tot.dongThuan = false; }
                return tot;
            }).catch(function () { return tot; });
        }

        function thu(i) {
            if (i >= buoc.length) {
                return tot ? xacNhanTuDo() : Promise.resolve({ plate: null, conf: 0, raw: '', rect: null });
            }
            var b = buoc[i];
            return docVung(srcCanvas, b.rect, b.psm, b.scale, false).then(function (r) {
                window.__alprDebug.lanThu.push({ ten: b.ten, psm: b.psm, raw: r.raw, plate: r.plate });
                if (!r.plate) {
                    var cd = chamDiem(r, !!b.rect);
                    if (cd.diem > totDiem) {
                        totDiem = cd.diem;
                        r.bienTuDo = cd.bien;              // biển "không chuẩn VN" đọc được
                        r.mau = b.rect ? b.rect.mau : null;
                        tot = r;
                    }
                    return thu(i + 1);
                }
                r.mau = b.rect ? b.rect.mau : null;
                // ĐỐI CHIẾU: đọc lại chính vùng đó bằng ảnh NHỊ PHÂN. Hai cách
                // xử lý ảnh khác nhau mà ra cùng kết quả -> gần như chắc chắn
                // đúng; khác nhau -> cảnh báo nhân viên kiểm tra lại bằng mắt.
                return docVung(srcCanvas, b.rect, b.psm, b.scale, true).then(function (r2) {
                    window.__alprDebug.lanThu.push({ ten: b.ten + '/nhi-phan', psm: b.psm, raw: r2.raw, plate: r2.plate });
                    if (r2.plate && r2.plate === r.plate) {
                        r.dongThuan = true;                       // 2 cách đọc TRÙNG khớp
                        r.conf = Math.max(r.conf, r2.conf, 90);
                    } else if (r2.plate && r2.conf > r.conf + 8) {
                        r2.mau = r.mau; r2.dongThuan = false; r2.khac = r.plate;
                        return r2;                                // bản nhị phân tự tin hơn hẳn
                    } else {
                        r.dongThuan = false;
                        if (r2.plate) r.khac = r2.plate;
                    }
                    return r;
                });
            });
        }

        /** Dong goi ket qua tu model chuyen dung + doi chieu Tesseract (neu co) */
        function ketQuaTuCCT(cct, tessR) {
            var r = {
                plate: cct.plate, conf: Math.round(cct.conf * 100), raw: cct.text,
                rect: cct.rect, mau: cct.rect.mau, engine: 'cct'
            };
            if (tessR && tessR.plate && tessR.plate === cct.plate) {
                r.dongThuan = true;
                r.conf = Math.max(r.conf, 95);
            } else if (tessR && tessR.plate && (tessR.conf || 0) >= 60) {
                r.dongThuan = false;
                r.khac = tessR.plate;
            } else {
                // Tesseract khong doc noi vung nay — model chuyen dung tu tin thi van dung
                r.dongThuan = false;
            }
            return r;
        }

        /* ======= TANG 1: model CHUYEN DOC BIEN SO (CCT-S v2) =======
           Doc truoc bang model chuyen dung — chinh xac hon han tren bien
           nho/mo/nghieng. Tesseract chi con vai tro DOI CHIEU doc lap
           (va du phong khi model chua tai duoc). */
        return docBienChuyen(srcCanvas, ungVien).then(function (cct) {
            if (window.__alprDebug) window.__alprDebug.cct = cct ? { text: cct.text, conf: Math.round(cct.conf * 100), plate: cct.plate, ghe: !!cct.ghe } : null;

            // Model doc ra bien CHUAN VN voi do tin cao -> chi can 1 lan doi chieu
            if (cct && cct.plate && cct.conf >= 0.88) {
                var sc = Math.max(2, Math.min(6, Math.round(150 / Math.max(12, cct.rect.h))));
                return docVung(srcCanvas, cct.rect, cct.rect.haiHang ? '6' : '7', sc, false).then(function (tessR) {
                    if (window.__alprDebug) window.__alprDebug.lanThu.push({ ten: 'tess/doi-chieu-cct', raw: tessR.raw, plate: tessR.plate });
                    return ketQuaTuCCT(cct, tessR);
                }).catch(function () { return ketQuaTuCCT(cct, null); });
            }

            // Model yeu/khong doc duoc -> chay day du chuoi Tesseract roi HOP NHAT
            return thu(0).then(function (tessKq) {
                if (!cct) return tessKq;
                if (tessKq.plate && cct.plate) {
                    if (tessKq.plate === cct.plate) {
                        tessKq.dongThuan = true;
                        tessKq.conf = Math.max(tessKq.conf, Math.round(cct.conf * 100), 92);
                        tessKq.engine = 'cct+tess';
                        return tessKq;
                    }
                    if (cct.conf >= 0.75) return ketQuaTuCCT(cct, tessKq);   // model chuyen dung dang tin hon
                    tessKq.khac = cct.plate; tessKq.dongThuan = false;
                    return tessKq;
                }
                if (cct.plate && cct.conf >= 0.6) return ketQuaTuCCT(cct, tessKq);
                if (!tessKq.plate && cct.text && cct.conf >= 0.55) {
                    // Bien khong chuan VN: goi y chuoi cua model chuyen dung cho nhan vien sua
                    if (!tessKq.bienTuDo || cct.conf >= 0.75) tessKq.bienTuDo = suaOSo(cct.text);
                    if (!tessKq.rect) { tessKq.rect = cct.rect; tessKq.mau = cct.rect.mau; }
                    if (Math.round(cct.conf * 100) > (tessKq.conf || 0)) tessKq.conf = Math.round(cct.conf * 100);
                }
                return tessKq;
            });
        });
    }

    /* ============================================================
       PHAN TICH NGOAI QUAN (Smart Gate 2.0): quet be mat THUNG XE /
       container tim vet MOP MEO / RACH / THUNG — vung toi bat thuong,
       NHO va CO CUM (phan biet voi kinh chan gio/banh xe la vung toi
       LON co cau truc). Heuristic muc do — he thong thuong mai dung
       model phan doan (segmentation) huan luyen rieng.
       ============================================================ */
    function soiNgoaiQuan(veh, plateRect, rx, ry) {
        try {
            var cv = els.canvas, ctx = cv.getContext('2d');
            // Vung THAN XE: bo 8% mep, bo 30% duoi (banh xe/gam), bo 18% tren (kinh/cabin)
            var bx = veh.x1 + (veh.x2 - veh.x1) * 0.08;
            var by = veh.y1 + (veh.y2 - veh.y1) * 0.18;
            var bw = (veh.x2 - veh.x1) * 0.84;
            var bh = (veh.y2 - veh.y1) * 0.52;
            if (bw < 80 || bh < 60) return null;
            var img = ctx.getImageData(Math.round(bx), Math.round(by), Math.round(bw), Math.round(bh)).data;
            var G = 14, cw = Math.floor(bw / G), ch = Math.floor(bh / G);
            if (cw < 4 || ch < 4) return null;

            // Do sang trung binh tung o
            var L = [];
            for (var gy = 0; gy < G; gy++) {
                L.push([]);
                for (var gx = 0; gx < G; gx++) {
                    var sum = 0, n = 0;
                    for (var y = gy * ch; y < (gy + 1) * ch; y += 3) {
                        for (var x = gx * cw; x < (gx + 1) * cw; x += 3) {
                            var i = (Math.round(y) * Math.round(bw) + Math.round(x)) * 4;
                            sum += 0.299 * img[i] + 0.587 * img[i + 1] + 0.114 * img[i + 2];
                            n++;
                        }
                    }
                    L[gy].push(sum / (n || 1));
                }
            }
            var flat = [];
            for (var a = 0; a < G; a++) for (var b = 0; b < G; b++) flat.push(L[a][b]);
            flat.sort(function (p, q) { return p - q; });
            var med = flat[Math.floor(flat.length / 2)];

            // O HONG: toi hon han trung vi; can cum >=2 o ke; loai o trung vung bien so
            var demHong = 0, mnX = G, mxX = -1, mnY = G, mxY = -1;
            for (var yy = 0; yy < G; yy++) {
                for (var xx = 0; xx < G; xx++) {
                    if (L[yy][xx] >= med * 0.58) continue;
                    var ke = (xx > 0 && L[yy][xx - 1] < med * 0.58) || (xx < G - 1 && L[yy][xx + 1] < med * 0.58) ||
                             (yy > 0 && L[yy - 1][xx] < med * 0.58) || (yy < G - 1 && L[yy + 1][xx] < med * 0.58);
                    if (!ke) continue;
                    // Bo qua neu o nam trong vung bien so (bien den chu trang de nham)
                    if (plateRect) {
                        var ox = bx + xx * cw, oy = by + yy * ch;
                        var px1 = rx + plateRect.x, py1 = ry + plateRect.y;
                        if (ox + cw > px1 && ox < px1 + plateRect.w && oy + ch > py1 && oy < py1 + plateRect.h) continue;
                    }
                    demHong++;
                    mnX = Math.min(mnX, xx); mxX = Math.max(mxX, xx);
                    mnY = Math.min(mnY, yy); mxY = Math.max(mxY, yy);
                }
            }
            var pct = Math.round(demHong * 100 / (G * G));
            // 2..18% dien tich = vet hong cuc bo; >18% = ca mang toi (kinh/bong) -> bo qua
            if (demHong < 2 || pct > 18) return null;
            return {
                pct: pct,
                x: bx + mnX * cw, y: by + mnY * ch,
                w: (mxX - mnX + 1) * cw, h: (mxY - mnY + 1) * ch
            };
        } catch (e) { return null; }
    }

    /** Hien "ANH VUNG BIEN SO": neu khoanh duoc vung thi cat DUNG vung do
        (phong to cho de doc), khong thi hien ca khung nhung bao ro cho user. */
    function veAnhVungBien(cropCanvas, rect) {
        if (rect) {
            var pc = document.createElement('canvas');
            var ph = Math.max(2, Math.round(140 / Math.max(1, rect.h)));   // phóng cho dễ nhìn
            ph = Math.min(4, ph);
            pc.width = rect.w * ph; pc.height = rect.h * ph;
            var c2 = pc.getContext('2d');
            c2.imageSmoothingEnabled = true;
            c2.drawImage(cropCanvas, rect.x, rect.y, rect.w, rect.h, 0, 0, pc.width, pc.height);
            els.plateCrop.src = pc.toDataURL('image/jpeg', 0.9);
        } else {
            els.plateCrop.src = cropCanvas.toDataURL('image/jpeg', 0.8);
        }
    }

    /* ---------------- 4. Phan tich 1 khung hinh ---------------- */
    /** DOI CHIEU LENH GIAO NHAN DIEN TU: doc duoc bien so -> hoi server xem
        co lenh eGate Pass nao dang ky bien nay khong (quy trinh ePort that:
        khong lenh = nhan vien phai kiem tra thu cong truoc khi cho vao) */
    function doiChieuLenh(bienSo) {
        lenhKhop = null;
        var o = $('gsLenhKq');
        if (!o) return;
        o.style.display = 'none';
        fetch(ROOT + 'Admin/Gate/DoiChieuLenh?bienSo=' + encodeURIComponent(bienSo), { credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) return;
                o.style.display = '';
                if (d.co) {
                    lenhKhop = d;
                    o.innerHTML = '<div class="alert alert-success py-2 mb-0 small">' +
                        '<i class="bi bi-patch-check-fill me-1"></i><b>KHỚP LỆNH ' + thoat(d.soLenh) + '</b> — ' + thoat(d.loai) +
                        (d.soCont ? ' · cont <b>' + thoat(d.soCont) + '</b>' : '') +
                        (d.taiXe ? ' · tài xế ' + thoat(d.taiXe) : '') +
                        ' · hẹn ' + thoat(d.hen) +
                        (d.dungGio ? ' <span class="badge bg-success">đúng giờ</span>'
                                   : ' <span class="badge bg-warning text-dark">lệch giờ hẹn</span>') +
                        (d.daCheckIn ? ' <span class="badge bg-info text-dark">đã check-in online</span>' : '') +
                        '</div>';
                } else {
                    o.innerHTML = '<div class="alert alert-warning py-2 mb-0 small">' +
                        '<i class="bi bi-exclamation-triangle me-1"></i><b>KHÔNG CÓ LỆNH</b> cho biển số này — ' +
                        'kiểm tra thủ công hoặc tạo lệnh trong mục <b>Lệnh giao nhận</b> trước khi cho vào.</div>';
                }
            })
            .catch(function () { });
    }

    function analyze(imgEl, w, h, nguon) {
        if (busy) return;
        busy = true;
        lastPlate = null;
        hongHoc = null;
        status('🧠 YOLO26 đang tìm phương tiện...', true);

        // Ve khung hinh goc
        var cv = els.canvas, ctx = cv.getContext('2d');
        cv.width = w; cv.height = h;
        ctx.drawImage(imgEl, 0, 0, w, h);

        detectVehicle(imgEl, w, h).then(function (veh) {
            // Vung OCR: bbox xe (neu co) — mo rong nhe; khong co thi toan anh
            var rx = 0, ry = 0, rw = w, rh = h, loai = 'Phương tiện';
            if (veh) {
                loai = veh.loai;
                rx = Math.max(0, veh.x1 - 8); ry = Math.max(0, veh.y1 - 8);
                rw = Math.min(w - rx, veh.x2 - veh.x1 + 16);
                rh = Math.min(h - ry, veh.y2 - veh.y1 + 16);
                ctx.lineWidth = Math.max(3, w / 300);
                ctx.strokeStyle = '#22d3ee';
                ctx.strokeRect(veh.x1, veh.y1, veh.x2 - veh.x1, veh.y2 - veh.y1);
                ctx.fillStyle = '#22d3ee';
                var fs = Math.max(14, Math.round(w / 50));
                ctx.font = '700 ' + fs + 'px "Be Vietnam Pro", sans-serif';
                ctx.fillRect(veh.x1 - 1, veh.y1 - fs - 8, ctx.measureText(loai).width + 12, fs + 8);
                ctx.fillStyle = '#04222e';
                ctx.fillText(loai, veh.x1 + 5, veh.y1 - 6);
            }

            status(bienSession ? '🔤 CCT-S v2 (AI chuyên biển số) đang đọc...' : '🔤 Tesseract LSTM đang đọc biển số...', true);
            var crop = document.createElement('canvas');
            crop.width = rw; crop.height = rh;
            crop.getContext('2d').drawImage(imgEl, rx, ry, rw, rh, 0, 0, rw, rh);

            return ocrRegion(crop).then(function (r) {
                if (!r.plate) {
                    // KHONG dau hang: van hien bang bien so voi chuoi OCR tho de
                    // nhan vien SUA TAY roi luu — kem anh xe lam bang chung.
                    var goiY = (r.bienTuDo || r.raw || '').toUpperCase()
                        .replace(/[^A-Z0-9.\-]/g, '').substring(0, 15);
                    lastPlate = { text: '', conf: r.conf, loai: loai, nguon: nguon, raw: r.raw };
                    els.plateText.value = goiY;
                if (goiY) doiChieuLenh(goiY);
                    els.plateConf.innerHTML = (r.conf > 0 ? Math.round(r.conf) + '%' : '—') +
                        ' <span class="badge bg-warning text-dark" style="font-size:.6rem;">không chuẩn VN</span>' +
                        (r.dongThuan ? ' <span class="badge bg-success" style="font-size:.6rem;">2/2 khớp</span>' : '');
                    els.plateType.value = loai;
                    els.plateBox.style.display = '';
                    veAnhVungBien(crop, r.rect);
                    if (r.rect) {                                  // khoanh vùng biển trên ảnh chính
                        ctx.strokeStyle = '#f5c542';
                        ctx.lineWidth = Math.max(3, w / 300);
                        ctx.strokeRect(rx + r.rect.x, ry + r.rect.y, r.rect.w, r.rect.h);
                    }
                    // Anh bang chung: chup SAU khi da ve day du khung nhan dien
                    anhXe = cv.toDataURL('image/jpeg', 0.85);
                    status('⚠️ Biển <b>không theo chuẩn Việt Nam</b>' +
                           (r.rect ? ' · đã khoanh vùng biển ' + (r.mau === 'vang' ? 'nền vàng' : 'nền trắng') : ' · chưa khoanh được vùng biển') +
                           (goiY ? ' · AI đọc được <b>"' + goiY + '"</b>' : '') +
                           (r.dongThuan ? ' · <span class="text-success fw-semibold">2 cách đọc đều khớp</span>'
                                        : (r.khac ? ' · <span class="text-warning fw-semibold">cách 2 đọc ra "' + r.khac + '"</span>' : '')) +
                           ' — hãy <b>kiểm tra/sửa lại</b> rồi bấm Lưu (ảnh xe vẫn lưu kèm làm bằng chứng).');
                    return;
                }
                lastPlate = { text: r.plate, conf: r.conf, loai: loai, nguon: nguon, raw: r.raw };
                // Hien "bang bien so" nhu he thong cong that
                els.plateText.value = r.plate;
                doiChieuLenh(r.plate);
                els.plateType.value = loai;
                var nhanKq = r.dongThuan
                    ? ' <span class="badge bg-success" style="font-size:.6rem;">2 engine khớp</span>'
                    : (!r.khac && r.engine === 'cct' && r.conf >= 85)
                        ? ' <span class="badge bg-info text-dark" style="font-size:.6rem;">AI chuyên dụng</span>'
                        : ' <span class="badge bg-warning text-dark" style="font-size:.6rem;">cần kiểm tra</span>';
                els.plateConf.innerHTML = (r.conf > 0 ? Math.round(r.conf) + '%' : '—') + nhanKq;
                els.plateBox.style.display = '';
                // Hien DUNG vung bien so da dinh vi (neu tim duoc) — nhu he thong ALPR thuc te
                veAnhVungBien(crop, r.rect);
                if (r.rect) {
                    // Ve khung vang quanh bien so tren anh chinh
                    ctx.strokeStyle = '#f5c542';
                    ctx.lineWidth = Math.max(3, w / 300);
                    ctx.strokeRect(rx + r.rect.x, ry + r.rect.y, r.rect.w, r.rect.h);
                }

                // ===== SMART GATE 2.0: soi ngoai quan tim mop meo/rach =====
                // YOLO khong bat duoc xe (anh ve/goc la) -> van soi tren vung
                // trung tam anh (coi ca khung hinh la than xe)
                var vungSoi = veh || { x1: w * 0.05, y1: h * 0.05, x2: w * 0.95, y2: h * 0.95 };
                hongHoc = soiNgoaiQuan(vungSoi, r.rect, rx, ry);
                if (hongHoc) {
                    ctx.setLineDash([9, 6]);
                    ctx.strokeStyle = '#ff1744';
                    ctx.lineWidth = Math.max(3, w / 280);
                    ctx.strokeRect(hongHoc.x, hongHoc.y, hongHoc.w, hongHoc.h);
                    ctx.setLineDash([]);
                    var nhanH = '⚠ MÓP MÉO/HƯ HẠI ~' + hongHoc.pct + '%';
                    ctx.font = '700 ' + Math.max(13, Math.round(w / 55)) + 'px "Be Vietnam Pro", sans-serif';
                    ctx.fillStyle = '#ff1744';
                    ctx.fillRect(hongHoc.x - 1, Math.max(0, hongHoc.y - 24), ctx.measureText(nhanH).width + 10, 22);
                    ctx.fillStyle = '#fff';
                    ctx.fillText(nhanH, hongHoc.x + 4, Math.max(16, hongHoc.y - 7));
                }
                // Anh bang chung: chup SAU khi da ve khung xe + bien + vet hong
                anhXe = cv.toDataURL('image/jpeg', 0.85);
                var chuThichDoc = r.dongThuan
                    ? ' · <span class="text-success fw-semibold">CCT-S v2 và Tesseract đọc ĐỘC LẬP đều ra kết quả này</span>'
                    : r.khac
                        ? ' · <span class="text-warning fw-semibold">⚠ 2 engine đọc lệch nhau (engine kia: "' + r.khac + '") — hãy đối chiếu ảnh rồi sửa nếu cần</span>'
                        : (r.engine === 'cct'
                            ? ' · <span class="text-info fw-semibold">đọc bằng model chuyên biển số CCT-S v2 (tin cậy ' + Math.round(r.conf) + '%)</span>'
                            : ' · <span class="text-warning fw-semibold">⚠ chỉ một cách đọc ra kết quả — nên đối chiếu ảnh</span>');
                status('✅ Đọc được biển số <b>' + r.plate + '</b>' +
                       (hongHoc ? ' · <span class="text-danger fw-bold">⚠ PHÁT HIỆN MÓP MÉO/HƯ HẠI ~' + hongHoc.pct + '% bề mặt — đã khoanh đỏ, kiểm tra trước khi cho vào</span>' : '') +
                       (r.rect ? ' · vùng biển <b>' + (r.mau === 'vang' ? 'NỀN VÀNG (xe kinh doanh vận tải)' : 'nền trắng') + '</b>' : '') +
                       chuThichDoc +
                       ' — bấm <b>Lưu vào nhật ký</b>.');
            });
        }).catch(function (e) {
            console.error(e);
            status('❌ Lỗi phân tích: ' + e.message);
        }).then(function () { busy = false; });
    }

    /* ---------------- 5. Luu + lam moi danh sach ---------------- */
    function save() {
        var bien = els.plateText.value.trim().toUpperCase();
        if (!bien) return;
        var fd = new FormData();
        fd.append('bienSo', bien);
        fd.append('cong', els.selCong.value);
        fd.append('huongDi', els.selHuong.value);
        // LOAI XE: lay DUNG chu nhan vien go trong o (AI chi dien san goi y).
        // Nho vay danh sach xe cong hien y het loai xe da nhap, khong bi kep
        // vao 2 lop co san cua YOLO.
        var loaiNhap = (els.plateType && els.plateType.value ? els.plateType.value : '').trim();
        if (loaiNhap.length > 30) loaiNhap = loaiNhap.substring(0, 30);
        fd.append('loaiXe', loaiNhap || (lastPlate ? lastPlate.loai : 'Phương tiện'));
        fd.append('doTinCay', lastPlate ? lastPlate.conf : '');
        fd.append('nguon', lastPlate ? lastPlate.nguon : 'TaiAnh');
        if (anhXe) fd.append('anh', anhXe);                          // ảnh bằng chứng
        if (lastPlate && lastPlate.raw) fd.append('ocrTho', lastPlate.raw.substring(0, 120));
        if (hongHoc) fd.append('ghiChu', 'AI ngoại quan: móp méo/hư hại ~' + hongHoc.pct + '% bề mặt');
        if (lenhKhop) fd.append('maLenh', lenhKhop.maLenh);

        els.btnSave.disabled = true;
        fetch(ROOT + 'Admin/Gate/Save', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) { status('❌ ' + d.message); return; }
                status('💾 Đã lưu <b>' + bien + '</b> (' + els.selHuong.value + ' cổng ' + els.selCong.value + ' lúc ' + d.gio + ')' +
                       (d.dock ? ' → <span class="badge" style="background:#0d9488;">🅿 Điều phối vào Dock ' + d.dock + '</span>' : '') +
                       (d.anh ? ' <span class="text-info">📷 kèm ảnh xe</span>' : '') + '.' +
                       (d.canhBao ? ' <span class="text-warning fw-semibold">⚠ ' + d.canhBao + '</span>' : ''));
                els.plateBox.style.display = 'none';
                anhXe = null;
                hongHoc = null;
                refreshList();
            })
            .catch(function () { status('❌ Mất kết nối máy chủ.'); })
            .then(function () { els.btnSave.disabled = false; });
    }

    /** Thoat ky tu HTML — loai xe do nguoi dung tu go, khong duoc phep
        chen the HTML vao bang danh sach. */
    function thoat(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function refreshList() {
        fetch(ROOT + 'Admin/Gate/Recent', { credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) return;
                $('gsVao').textContent = d.vao;
                $('gsRa').textContent = d.ra;
                var html = '';
                d.ds.forEach(function (x, i) {
                    var oAnh = x.anh
                        ? '<img src="' + thoat(x.anh) + '" class="gs-thumb" data-anh="' + thoat(x.anh) + '" data-bien="' + thoat(x.BienSo) + '" ' +
                          'title="Bấm để xem ảnh lớn" alt="Ảnh xe ' + thoat(x.BienSo) + '" />'
                        : '<span class="text-muted small" title="Lượt này không có ảnh">—</span>';
                    var hong = x.ghiChu && x.ghiChu.indexOf('móp méo') >= 0;
                    html += '<tr>' +
                        '<td class="text-muted">' + (i + 1) + '</td>' +
                        '<td>' + oAnh + '</td>' +
                        '<td><span class="gs-plate">' + thoat(x.BienSo) + '</span>' +
                            (hong ? '<div><span class="badge bg-danger" style="font-size:.58rem;" title="' + thoat(x.ghiChu) + '">⚠ móp méo</span></div>' : '') + '</td>' +
                        '<td>' + x.Cong + '</td>' +
                        '<td><span class="badge ' + (x.HuongDi === 'Vào' ? 'bg-success' : 'bg-warning text-dark') + '">' + x.HuongDi + '</span></td>' +
                        '<td>' + (x.dock ? '<span class="badge" style="background:#0d9488;">' + thoat(x.dock) + '</span>' : '<span class="text-muted small">—</span>') + '</td>' +
                        '<td class="small">' + thoat(x.LoaiXe || '—') + '</td>' +
                        '<td class="small text-muted">' + x.gio + '</td></tr>';
                });
                $('gsBody').innerHTML = html || '<tr><td colspan="8" class="text-center text-muted">Chưa có dữ liệu</td></tr>';
            }).catch(function () { });
    }

    /* ---- Xem anh xe kich thuoc lon (bam vao anh nho trong danh sach) ---- */
    function moAnhLon(src, bien) {
        var ov = $('gsAnhOverlay');
        if (!ov) return;
        $('gsAnhLon').src = src;
        $('gsAnhTieuDe').textContent = 'Ảnh xe ' + (bien || '');
        var tai = $('gsAnhTai');
        if (tai) { tai.href = src; tai.setAttribute('download', 'xe_' + (bien || 'cong') + '.jpg'); }
        ov.style.display = 'flex';
    }
    function dongAnhLon() {
        var ov = $('gsAnhOverlay');
        if (ov) ov.style.display = 'none';
    }

    /* ---------------- 6. Nguon hinh anh ---------------- */
    function fromImageEl(img, nguon) {
        analyze(img, img.naturalWidth || img.width, img.naturalHeight || img.height, nguon);
    }
    function startCam() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.\nHãy truy cập site qua https:// rồi thử lại nhé.');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 } }, audio: false })
            .then(function (s) {
                stream = s;
                els.video.srcObject = s;
                els.video.style.display = '';
                els.canvas.style.display = 'none';
                els.video.play();
                els.btnCam.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
                els.btnShot.style.display = '';
                status('🎥 Camera làn vào đang chạy — canh biển số rồi bấm <b>Chụp & đọc biển</b>.');
            })
            .catch(function () { status('❌ Không truy cập được camera (cần cấp quyền).'); });
    }
    function stopCam() {
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        els.video.style.display = 'none';
        els.canvas.style.display = '';
        els.btnCam.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật camera làn vào';
        els.btnShot.style.display = 'none';
    }
    function shot() {
        if (!stream || els.video.readyState < 2) return;
        var v = els.video;
        var tmp = document.createElement('canvas');
        tmp.width = v.videoWidth; tmp.height = v.videoHeight;
        tmp.getContext('2d').drawImage(v, 0, 0);
        var img = new Image();
        img.onload = function () {
            els.video.style.display = 'none';
            els.canvas.style.display = '';
            analyze(img, img.width, img.height, 'Webcam');
        };
        img.src = tmp.toDataURL('image/jpeg', 0.9);
    }

    // Loi cot loi cho AI Lab / kiem thu tu dong (khong dung DOM cua trang Gate)
    window.__alprCore = {
        timVungBien: timVungBien, timBienSo: timBienSo, timBienTuDo: timBienTuDo,
        napBienOcr: napBienOcr, docBienCCT: docBienCCT, docBienChuyen: docBienChuyen
    };

    /* ---------------- Khoi dong ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        els = {
            status: $('gsStatus'), badge: $('gsEngine'), canvas: $('gsCanvas'), work: $('gsWork'),
            video: $('gsVideo'), btnCam: $('btnCam'), btnShot: $('btnShot'),
            plateBox: $('gsPlateBox'), plateText: $('gsPlateText'), plateConf: $('gsPlateConf'),
            plateType: $('gsPlateType'), plateCrop: $('gsPlateCrop'),
            btnSave: $('btnGsSave'), selCong: $('gsCong'), selHuong: $('gsHuong')
        };
        if (!els.status) return;                    // khong phai trang Gate

        $('fileInput').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            stopCam();
            var rd = new FileReader();
            rd.onload = function () {
                var img = new Image();
                img.onload = function () { fromImageEl(img, 'TaiAnh'); };
                img.src = rd.result;
            };
            rd.readAsDataURL(f);
        });
        document.querySelectorAll('.gs-sample').forEach(function (b) {
            b.addEventListener('click', function () {
                stopCam();
                var img = new Image();
                img.crossOrigin = 'anonymous';
                img.onload = function () { fromImageEl(img, 'AnhMau'); };
                img.src = b.getAttribute('data-src');
            });
        });
        els.btnCam.addEventListener('click', function () { stream ? stopCam() : startCam(); });
        els.btnShot.addEventListener('click', shot);
        els.btnSave.addEventListener('click', save);

        // Bam anh nho trong danh sach -> mo anh lon (uy quyen su kien vi
        // cac dong duoc ve lai moi lan lam moi danh sach)
        var bang = $('gsBody');
        if (bang) bang.addEventListener('click', function (e) {
            var im = e.target.closest ? e.target.closest('.gs-thumb') : null;
            if (im) moAnhLon(im.getAttribute('data-anh'), im.getAttribute('data-bien'));
        });
        var dong = $('gsAnhDong');
        if (dong) dong.addEventListener('click', dongAnhLon);
        var ovl = $('gsAnhOverlay');
        if (ovl) ovl.addEventListener('click', function (e) { if (e.target === ovl) dongAnhLon(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') dongAnhLon(); });

        refreshList();
        init();
    });
})();
