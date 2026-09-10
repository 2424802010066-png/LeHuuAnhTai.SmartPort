/* ============================================================
   SMARTTALLY — KIEM DEM & PHAN LOAI HANG HOA BANG AI
   ------------------------------------------------------------
   B1: nhap thong tin lo hang (ten, so luong khai bao, kich thuoc...)
   B2: camera/anh -> YOLO26 dem doi tuong theo LOP da chon
   B3: PHAN LOAI theo mau sac so voi MAU CHUAN (vat co do tin cay
       cao nhat): GIONG -> lan PHAI (xanh) · KHAC -> lan TRAI (cam)
   ============================================================ */
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    var ROOT = window.APP_ROOT || '/';

    var ORT_CDN = ROOT + 'Scripts/vendor/ort/';   // LOCAL — khong can CDN
    var YOLO_URL = ROOT + 'Content/models/yolo26n.onnx';   // LOCAL
    var INPUT = 640;
    var session = null, stream = null, busy = false;
    var els = {};

    function loadScript(src) {
        return new Promise(function (ok, err) {
            var s = document.createElement('script');
            s.src = src; s.onload = ok; s.onerror = err;
            document.head.appendChild(s);
        });
    }
    function fetchModel(url) {
        if (!('caches' in window)) return fetch(url).then(function (r) { return r.arrayBuffer(); });
        return caches.open('sp-models-v1').then(function (c) {
            return c.match(url).then(function (hit) {
                if (hit) return hit.arrayBuffer();
                return fetch(url).then(function (r) {
                    // KHONG cache response loi (403/503...) — cache hong la
                    // trang do KHONG BAO GIO tai duoc model nua du mang da co lai
                    if (!r.ok) throw new Error('HTTP ' + r.status + ' khi tai model');
                    return c.put(url, r.clone()).then(function () { return r.arrayBuffer(); });
                });
            });
        });
    }
    function status(html, spin) {
        els.status.innerHTML = (spin ? '<span class="spinner-border spinner-border-sm me-2"></span>' : '') + html;
    }
    function init() {
        status('Đang tải YOLO26...', true);
        var useGpu = !!navigator.gpu;
        loadScript(ORT_CDN + (useGpu ? 'ort.webgpu.min.js' : 'ort.min.js'))
            .then(function () {
                ort.env.wasm.wasmPaths = ORT_CDN;
                return fetchModel(YOLO_URL);
            })
            .then(function (buf) {
                return ort.InferenceSession.create(buf, { executionProviders: useGpu ? ['webgpu', 'wasm'] : ['wasm'] })
                    .catch(function () { return ort.InferenceSession.create(buf, { executionProviders: ['wasm'] }); });
            })
            .then(function (s) {
                session = s;
                status('✅ Sẵn sàng! Nhập thông tin lô rồi đưa hàng vào camera/ảnh.');
                ['btnCam', 'fileInput', 'btnCount', 'btnQC'].forEach(function (id) { if ($(id)) $(id).disabled = false; });
                document.querySelectorAll('.tl-sample').forEach(function (b) { b.disabled = false; });
            })
            .catch(function () { status('❌ Không tải được model — kiểm tra Internet.'); });
    }

    var CLS_MAP = { bottle: 39, cup: 41, suitcase: 28, book: 73, 'cell phone': 67, banana: 46, apple: 47, orange: 49 };

    function drawSource() {
        var c = els.canvas, ctx = c.getContext('2d');
        // Dang dem video: uu tien khung hinh tu file video
        if (vc.running && vc.dungVideo && els.vidFile && els.vidFile.src && els.vidFile.readyState >= 2 && !els.vidFile.ended) {
            c.width = els.vidFile.videoWidth || 720; c.height = els.vidFile.videoHeight || 420;
            ctx.drawImage(els.vidFile, 0, 0);
            return true;
        }
        if (stream && els.video.readyState >= 2) {
            c.width = els.video.videoWidth; c.height = els.video.videoHeight;
            ctx.drawImage(els.video, 0, 0);
            return true;
        }
        if (els.lastImg) {
            c.width = els.lastImg.naturalWidth; c.height = els.lastImg.naturalHeight;
            ctx.drawImage(els.lastImg, 0, 0);
            return true;
        }
        return false;
    }

    function detect(clsWanted) {
        var c = els.canvas, w = c.width, h = c.height;
        var cv = els.work, ctx = cv.getContext('2d');
        cv.width = INPUT; cv.height = INPUT;
        var sc = Math.min(INPUT / w, INPUT / h);
        var dx = (INPUT - Math.round(w * sc)) / 2, dy = (INPUT - Math.round(h * sc)) / 2;
        ctx.fillStyle = '#727272'; ctx.fillRect(0, 0, INPUT, INPUT);
        ctx.drawImage(c, dx, dy, Math.round(w * sc), Math.round(h * sc));
        var d = ctx.getImageData(0, 0, INPUT, INPUT).data;
        var arr = new Float32Array(3 * INPUT * INPUT);
        for (var i = 0, p = 0; i < d.length; i += 4, p++) {
            arr[p] = d[i] / 255;
            arr[p + INPUT * INPUT] = d[i + 1] / 255;
            arr[p + 2 * INPUT * INPUT] = d[i + 2] / 255;
        }
        var feeds = {}; feeds[session.inputNames[0]] = new ort.Tensor('float32', arr, [1, 3, INPUT, INPUT]);
        return session.run(feeds).then(function (out) {
            var o = out[session.outputNames[0]];
            var boxes = [];
            for (var k = 0; k < o.dims[1]; k++) {
                var b = k * 6, scr = o.data[b + 4], cls = Math.round(o.data[b + 5]);
                if (scr < 0.35 || cls !== clsWanted) continue;
                boxes.push({
                    x1: Math.max(0, (o.data[b] - dx) / sc), y1: Math.max(0, (o.data[b + 1] - dy) / sc),
                    x2: Math.min(w, (o.data[b + 2] - dx) / sc), y2: Math.min(h, (o.data[b + 3] - dy) / sc),
                    score: scr
                });
            }
            return boxes;
        });
    }

    /* TOI UU: dac trung HISTOGRAM HSV (8 bin Hue x 3 bin Sat = 24 chieu,
       BO kenh V) — phan loai theo "chat mau" that cua vat, KHONG bi anh
       huong boi chenh lech anh sang/bong do nhu so sanh RGB tho. */
    function hsvHist(b, cv) {
        var ctx = (cv || els.canvas).getContext('2d');
        var w = Math.max(2, Math.round(b.x2 - b.x1)), h = Math.max(2, Math.round(b.y2 - b.y1));
        // vung loi 60% giua bbox de tranh nen
        var mx = Math.round(b.x1 + w * 0.2), my = Math.round(b.y1 + h * 0.2);
        var mw = Math.max(2, Math.round(w * 0.6)), mh = Math.max(2, Math.round(h * 0.6));
        var img = ctx.getImageData(mx, my, mw, mh).data;
        var hist = new Array(24).fill(0);
        var n = 0;
        for (var i = 0; i < img.length; i += 8) {
            var r = img[i] / 255, g = img[i + 1] / 255, bl = img[i + 2] / 255;
            var maxv = Math.max(r, g, bl), minv = Math.min(r, g, bl), d = maxv - minv;
            var hDeg = 0;
            if (d > 0) {
                if (maxv === r) hDeg = 60 * (((g - bl) / d) % 6);
                else if (maxv === g) hDeg = 60 * ((bl - r) / d + 2);
                else hDeg = 60 * ((r - g) / d + 4);
                if (hDeg < 0) hDeg += 360;
            }
            var s = maxv > 0 ? d / maxv : 0;
            var hBin = Math.min(7, Math.floor(hDeg / 45));
            var sBin = s < 0.2 ? 0 : (s < 0.55 ? 1 : 2);
            hist[hBin * 3 + sBin]++;
            n++;
        }
        for (var k = 0; k < 24; k++) hist[k] /= (n || 1);
        return hist;
    }
    /** Do tuong dong cosine giua 2 histogram [0..1] */
    function histSim(a, b) {
        var dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return dot / (Math.sqrt(na) * Math.sqrt(nb) + 1e-9);
    }

    /* ============================================================
       QC THUNG HANG — phat hien thung LOI (uot/tham mau/hu hai)
       1. Tim thung: YOLO (suitcase) -> fallback quet MAU CARTON nau
       2. Soi be mat: luoi 12x12 trong thung, o LOI = toi hon han
          muc sang trung vi cua thung (vd goc bi uot tham mau)
       3. Thung loi -> khung DO + khoanh vung loi + COI + GIONG NOI
          + tu luu anh vao KHO MEDIA + bao Phong an ninh
       ============================================================ */
    function sirenQC(seconds) {
        try {
            var a = new (window.AudioContext || window.webkitAudioContext)();
            var o = a.createOscillator(), g = a.createGain(), lfo = a.createOscillator(), lg = a.createGain();
            o.type = 'sawtooth'; o.frequency.value = 640;
            lfo.type = 'sine'; lfo.frequency.value = 3; lg.gain.value = 200;
            lfo.connect(lg); lg.connect(o.frequency);
            g.gain.value = 0.22;
            o.connect(g); g.connect(a.destination);
            o.start(); lfo.start();
            setTimeout(function () { try { o.stop(); lfo.stop(); a.close(); } catch (e) { } }, (seconds || 3) * 1000);
        } catch (e) { }
    }
    function noiCanhBao(text) {
        if (!('speechSynthesis' in window)) return;
        var u = new SpeechSynthesisUtterance(text);
        u.lang = 'vi-VN'; u.rate = 1.05;
        var vi = speechSynthesis.getVoices().filter(function (v) { return v.lang && v.lang.toLowerCase().indexOf('vi') === 0; })[0];
        if (vi) u.voice = vi;
        speechSynthesis.speak(u);
    }

    /** Fallback: tim vung THUNG CARTON theo mau nau dac trung (khi YOLO khong bat duoc) */
    function timThungCarton() {
        var c = els.canvas, w = c.width, h = c.height;
        var img = c.getContext('2d').getImageData(0, 0, w, h).data;
        var CELL = 20, gw = Math.floor(w / CELL), gh = Math.floor(h / CELL);
        var grid = [], nau = 0;
        for (var gy = 0; gy < gh; gy++) {
            grid.push(new Array(gw).fill(0));
            for (var gx = 0; gx < gw; gx++) {
                var hit = 0, tot = 0;
                for (var y = gy * CELL; y < (gy + 1) * CELL; y += 5) {
                    for (var x = gx * CELL; x < (gx + 1) * CELL; x += 5) {
                        var i = (y * w + x) * 4;
                        var r = img[i] / 255, g = img[i + 1] / 255, b = img[i + 2] / 255;
                        var mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
                        var hue = 0;
                        if (d > 0) {
                            if (mx === r) hue = 60 * (((g - b) / d) % 6);
                            else if (mx === g) hue = 60 * ((b - r) / d + 2);
                            else hue = 60 * ((r - g) / d + 4);
                            if (hue < 0) hue += 360;
                        }
                        var s = mx > 0 ? d / mx : 0;
                        // mau carton: cam-nau 15..50 do, bao hoa vua, khong qua toi/sang
                        if (hue >= 15 && hue <= 50 && s >= 0.15 && s <= 0.8 && mx >= 0.25 && mx <= 0.92) hit++;
                        tot++;
                    }
                }
                grid[gy][gx] = hit / tot > 0.5 ? 1 : 0;
                if (grid[gy][gx]) nau++;
            }
        }
        if (nau < gw * gh * 0.04) return [];        // khong du vung carton
        // Gom TUNG CUM rieng (flood fill) — moi cum = 1 thung
        var seen = {}, thungs = [];
        for (var sy = 0; sy < gh; sy++) for (var sx = 0; sx < gw; sx++) {
            if (!grid[sy][sx] || seen[sy + ',' + sx]) continue;
            var st = [[sx, sy]], mnX = sx, mxX = sx, mnY = sy, mxY = sy, size = 0;
            while (st.length) {
                var p = st.pop(), key = p[1] + ',' + p[0];
                if (seen[key]) continue;
                seen[key] = 1;
                if (p[0] < 0 || p[0] >= gw || p[1] < 0 || p[1] >= gh || !grid[p[1]][p[0]]) continue;
                size++;
                mnX = Math.min(mnX, p[0]); mxX = Math.max(mxX, p[0]);
                mnY = Math.min(mnY, p[1]); mxY = Math.max(mxY, p[1]);
                st.push([p[0] + 1, p[1]]); st.push([p[0] - 1, p[1]]); st.push([p[0], p[1] + 1]); st.push([p[0], p[1] - 1]);
            }
            // Cum du lon (>= 2.5% anh) moi coi la 1 thung
            if (size >= gw * gh * 0.025) {
                thungs.push({ size: size, x1: mnX * CELL, y1: mnY * CELL, x2: (mxX + 1) * CELL, y2: (mxY + 1) * CELL, score: 0.9, fallback: true });
            }
        }
        return thungs;
    }

    /** Soi be mat 1 thung: tra ve { pct, rect } — vung LOI toi/tham mau */
    function soiLoiThung(b) {
        var ctx = els.canvas.getContext('2d');
        var bw = Math.round(b.x2 - b.x1), bh = Math.round(b.y2 - b.y1);
        if (bw < 40 || bh < 40) return { pct: 0, rect: null };
        // vung loi 92% (tranh vien/nen)
        var mx = Math.round(b.x1 + bw * 0.04), my = Math.round(b.y1 + bh * 0.04);
        var mw = Math.round(bw * 0.92), mh = Math.round(bh * 0.92);
        var img = ctx.getImageData(mx, my, mw, mh).data;
        var G = 12, cw = Math.floor(mw / G), ch = Math.floor(mh / G);
        if (cw < 3 || ch < 3) return { pct: 0, rect: null };
        var L = [];
        for (var gy = 0; gy < G; gy++) {
            L.push([]);
            for (var gx = 0; gx < G; gx++) {
                var sum = 0, n = 0;
                for (var y = gy * ch; y < (gy + 1) * ch; y += 3) {
                    for (var x = gx * cw; x < (gx + 1) * cw; x += 3) {
                        var i = (y * mw + x) * 4;
                        sum += 0.299 * img[i] + 0.587 * img[i + 1] + 0.114 * img[i + 2];
                        n++;
                    }
                }
                L[gy][gx] = sum / (n || 1);
            }
        }
        var flat = [];
        for (var a = 0; a < G; a++) for (var b2 = 0; b2 < G; b2++) flat.push(L[a][b2]);
        flat.sort(function (p, q) { return p - q; });
        var med = flat[Math.floor(flat.length / 2)];
        // O LOI: toi hon 32% so voi trung vi (vet uot/tham/no chay mau)
        var loi = [], minX = G, maxX = -1, minY = G, maxY = -1, demLoi = 0;
        for (var yy = 0; yy < G; yy++) {
            for (var xx = 0; xx < G; xx++) {
                if (L[yy][xx] < med * 0.68) {
                    var ke = (xx > 0 && L[yy][xx - 1] < med * 0.68) || (xx < G - 1 && L[yy][xx + 1] < med * 0.68) ||
                             (yy > 0 && L[yy - 1][xx] < med * 0.68) || (yy < G - 1 && L[yy + 1][xx] < med * 0.68);
                    if (!ke) continue;              // can cum >=2 o ke — loai nhieu le
                    demLoi++;
                    minX = Math.min(minX, xx); maxX = Math.max(maxX, xx);
                    minY = Math.min(minY, yy); maxY = Math.max(maxY, yy);
                }
            }
        }
        var pct = Math.round(demLoi * 100 / (G * G));
        var rect = demLoi >= 2 ? {
            x: mx + minX * cw, y: my + minY * ch,
            w: (maxX - minX + 1) * cw, h: (maxY - minY + 1) * ch
        } : null;
        return { pct: pct, rect: rect };
    }

    function uploadAnhKho(moTa) {
        els.canvas.toBlob(function (blob) {
            if (!blob) return;
            var fd = new FormData();
            fd.append('loai', 'Anh');
            fd.append('nguon', 'KiemDem');
            fd.append('moTa', moTa);
            fd.append('anh', blob, 'qc.jpg');
            fetch(ROOT + 'Admin/Security/UploadMedia?loai=Anh&nguon=KiemDem&moTa=' + encodeURIComponent(moTa),
                { method: 'POST', body: fd, credentials: 'same-origin' }).catch(function () { });
        }, 'image/jpeg', 0.85);
    }
    function baoAnNinhHangLoi(moTa) {
        var thumb = document.createElement('canvas');
        var c = els.canvas;
        var sc = Math.min(1, 520 / Math.max(c.width, 1));
        thumb.width = Math.round(c.width * sc); thumb.height = Math.round(c.height * sc);
        thumb.getContext('2d').drawImage(c, 0, 0, thumb.width, thumb.height);
        var fd = new FormData();
        fd.append('loai', 'HangLoi');
        fd.append('mucDo', 'Chú ý');
        fd.append('viTri', 'Kho CFS');
        fd.append('moTa', moTa);
        fd.append('anh', thumb.toDataURL('image/jpeg', 0.75));
        fetch(ROOT + 'Admin/Security/Report', { method: 'POST', body: fd, credentials: 'same-origin' }).catch(function () { });
    }

    /** KIEM TRA LOI THUNG HANG — nhu tram QC tren bang chuyen */
    function kiemTraThung() {
        if (vc.running) { status('⚠️ Đang đếm video — bấm Dừng trước khi dùng chức năng này.'); return; }
        if (busy || !session) return;
        if (!drawSource()) { status('Chưa có hình — bật camera hoặc chọn ảnh.'); return; }
        busy = true;
        status('🔎 AI đang soi bề mặt thùng hàng...', true);

        detect(28).then(function (boxes) {                      // 28 = suitcase/kien hang
            // YOLO khong thay thung -> fallback quet mau carton
            if (!boxes.length) {
                var ths = timThungCarton();
                if (ths && ths.length) boxes = ths;
            }
            var ctx = els.canvas.getContext('2d');
            var w = els.canvas.width;
            ctx.lineWidth = Math.max(3, w / 260);
            ctx.font = '700 ' + Math.max(14, Math.round(w / 42)) + 'px "Be Vietnam Pro", sans-serif';

            if (!boxes.length) {
                status('⚠️ Không tìm thấy thùng hàng trong khung — đưa thùng vào giữa và đủ sáng.');
                busy = false;
                return;
            }

            var soLoi = 0;
            boxes.forEach(function (b, i) {
                var kq = soiLoiThung(b);
                var loi = kq.rect !== null && kq.pct >= 2;
                var col = loi ? '#ef4444' : '#22c55e';
                if (loi) soLoi++;
                ctx.strokeStyle = col;
                ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
                var nhan = loi ? ('⚠ THÙNG LỖI — hư hại ~' + kq.pct + '%') : '✔ ĐẠT';
                ctx.fillStyle = col;
                ctx.fillRect(b.x1 - 1, Math.max(0, b.y1 - 30), ctx.measureText(nhan).width + 12, 28);
                ctx.fillStyle = '#08131f';
                ctx.fillText(nhan, b.x1 + 5, Math.max(20, b.y1 - 8));
                if (loi && kq.rect) {
                    // Khoanh DUNG vung uot/hu hai
                    ctx.setLineDash([8, 5]);
                    ctx.strokeStyle = '#ff1744';
                    ctx.strokeRect(kq.rect.x, kq.rect.y, kq.rect.w, kq.rect.h);
                    ctx.setLineDash([]);
                }
            });

            var qcEl = $('tlQcKq');
            if (soLoi > 0) {
                sirenQC(4);
                noiCanhBao('Cảnh báo! Phát hiện ' + soLoi + ' thùng hàng bị lỗi trên băng chuyền. Vui lòng kiểm tra ngay.');
                var mota = 'QC: phát hiện ' + soLoi + '/' + boxes.length + ' thùng hàng bị lỗi (ướt/hư hại bề mặt)';
                uploadAnhKho(mota);
                baoAnNinhHangLoi(mota);
                if (qcEl) qcEl.innerHTML = '<span class="badge bg-danger">⚠ ' + soLoi + ' THÙNG LỖI</span> trên tổng ' + boxes.length +
                    ' thùng — đã <b>hú còi</b>, <b>đọc cảnh báo</b>, lưu ảnh vào <b>Kho media</b> và báo <b>Phòng an ninh</b>.';
                status('🚨 Phát hiện ' + soLoi + ' thùng LỖI! Ảnh bằng chứng đã lưu vào kho.');
            } else {
                if (qcEl) qcEl.innerHTML = '<span class="badge bg-success">✔ TẤT CẢ ĐẠT</span> ' + boxes.length + ' thùng không phát hiện hư hại bề mặt.';
                status('✅ ' + boxes.length + ' thùng đều ĐẠT — không phát hiện vùng hư hại.');
            }
        }).catch(function (e) { status('❌ Lỗi: ' + e.message); }).then(function () { busy = false; });
    }

    function demVaPhanLoai() {
        if (vc.running) { status('⚠️ Đang đếm video — bấm Dừng trước khi dùng chức năng này.'); return; }
        if (busy || !session) return;
        if (!drawSource()) { status('Chưa có hình — bật camera hoặc chọn ảnh.'); return; }
        busy = true;
        var lop = els.selLop.value;
        status('🧠 AI đang đếm "' + els.selLop.options[els.selLop.selectedIndex].text + '"...', true);

        detect(CLS_MAP[lop]).then(function (boxes) {
            var ctx = els.canvas.getContext('2d');
            var w = els.canvas.width;
            ctx.lineWidth = Math.max(2.5, w / 320);
            ctx.font = '700 ' + Math.max(13, Math.round(w / 50)) + 'px "Be Vietnam Pro", sans-serif';

            if (!boxes.length) {
                status('⚠️ Không thấy đối tượng nào thuộc lớp đã chọn — thử ảnh khác hoặc giảm khoảng cách.');
                busy = false;
                return;
            }

            // MAU CHUAN = vat co score cao nhat; so HISTOGRAM HSV (cosine)
            boxes.sort(function (a, b) { return b.score - a.score; });
            var histChuan = hsvHist(boxes[0]);
            var NGUONG_SIM = 0.78;                      // do tuong dong toi thieu de coi la "giong"

            var giong = 0, khac = 0;
            boxes.forEach(function (b, i) {
                var sim = i === 0 ? 1 : histSim(hsvHist(b), histChuan);
                var laGiong = sim >= NGUONG_SIM;
                if (laGiong) giong++; else khac++;
                var col = laGiong ? '#22c55e' : '#f59e0b';
                var nhan = (i === 0 ? '★ MẪU' : (laGiong ? '➜ PHẢI' : '⬅ TRÁI') + ' ' + Math.round(sim * 100) + '%') + ' #' + (i + 1);
                ctx.strokeStyle = col;
                ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
                ctx.fillStyle = col;
                ctx.fillRect(b.x1 - 1, b.y1 - 24, ctx.measureText(nhan).width + 10, 22);
                ctx.fillStyle = '#08131f';
                ctx.fillText(nhan, b.x1 + 4, b.y1 - 7);
            });

            var duKien = parseInt(els.duKien.value, 10) || null;
            var lech = duKien !== null ? duKien - boxes.length : null;
            var html = 'Đếm được <b style="font-size:1.3rem;color:#0a2f57;">' + boxes.length + '</b> vật';
            if (duKien !== null) {
                html += ' / khai báo ' + duKien + ' — ' +
                    (lech === 0 ? '<b class="text-success">KHỚP ✔</b>'
                                : '<b class="text-danger">LỆCH ' + Math.abs(lech) + ' (' + (lech > 0 ? 'thiếu' : 'thừa') + ') ⚠</b>');
            }
            html += '<div class="mt-1">➜ Lăn PHẢI (giống mẫu): <b class="text-success">' + giong + '</b> · ⬅ Lăn TRÁI (khác mẫu): <b style="color:#b45309;">' + khac + '</b></div>';
            els.kq.innerHTML = html;
            els.kq.dataset.dem = boxes.length;
            els.kq.dataset.giong = giong;
            els.kq.dataset.khac = khac;
            $('btnSaveTally').disabled = false;
            status('✅ Xong! Kiểm tra kết quả rồi bấm Lưu phiên kiểm đếm.');
        }).catch(function (e) { status('❌ Lỗi: ' + e.message); }).then(function () { busy = false; });
    }

    /* ============================================================
       DEM LUY KE THEO VIDEO + SAN PHAM MAU (few-shot)
       ------------------------------------------------------------
       B1: dang ky SAN PHAM MAU bang 4-6 anh chup / video quay tong
           quat / chup truc tiep tu camera -> luu HSV histogram
       B2: nhap so luong muc tieu (VD 500) -> chay video/webcam,
           AI theo doi tung vat (tracker IoU + du doan van toc),
           moi vat DUNG MAU dem 1 lan: 0/500 -> 500/500
       B3: vat KHONG DUNG MAU -> coi + doc canh bao + luu kho media
           Du so luong -> chuong + doc "da du so luong"
       ============================================================ */
    var mau = { hists: [] };                // dac trung 4-6 mau da dang ky
    var vc = {                              // trang thai phien dem video
        running: false, dem: 0, sai: 0, target: 0,
        tracks: [], nextId: 1, timer: null, gen: 0, choKhung: 0,
        lastWarn: 0, luuSaiPending: false, luuSaiRoi: false, dungVideo: false
    };
    window.__vc = { mau: 0, dem: 0, sai: 0, running: false, done: false };

    function beepTick() {                   // tick ngan moi lan dem duoc 1 vat
        try {
            var a = new (window.AudioContext || window.webkitAudioContext)();
            var o = a.createOscillator(), g = a.createGain();
            o.type = 'square'; o.frequency.value = 1250;
            g.gain.value = 0.08;
            o.connect(g); g.connect(a.destination);
            o.start();
            setTimeout(function () { try { o.stop(); a.close(); } catch (e) { } }, 70);
        } catch (e) { }
    }
    function chimeOk() {                    // 2 not sine khi DU so luong
        try {
            var a = new (window.AudioContext || window.webkitAudioContext)();
            [[880, 0], [1318, 180]].forEach(function (nt) {
                var o = a.createOscillator(), g = a.createGain();
                o.type = 'sine'; o.frequency.value = nt[0];
                g.gain.value = 0.16;
                o.connect(g); g.connect(a.destination);
                o.start(a.currentTime + nt[1] / 1000);
                o.stop(a.currentTime + nt[1] / 1000 + 0.32);
            });
            setTimeout(function () { try { a.close(); } catch (e) { } }, 900);
        } catch (e) { }
    }

    /** Them 1 mau tu Image/video frame (lay vung trung tam lam dac trung) */
    function themMauTuNguon(src, srcW, srcH) {
        if (mau.hists.length >= 6) { status('Đã đủ 6 ảnh mẫu — bấm "Xóa mẫu" nếu muốn làm lại.'); return false; }
        var w = srcW || src.naturalWidth || src.videoWidth || 0;
        var h = srcH || src.naturalHeight || src.videoHeight || 0;
        if (!w || !h) return false;
        var cv = document.createElement('canvas');
        var sc = Math.min(1, 420 / Math.max(w, h));
        cv.width = Math.max(2, Math.round(w * sc)); cv.height = Math.max(2, Math.round(h * sc));
        cv.getContext('2d').drawImage(src, 0, 0, cv.width, cv.height);
        mau.hists.push(hsvHist({ x1: 0, y1: 0, x2: cv.width, y2: cv.height }, cv));
        // thumbnail hien thi
        var th = document.createElement('canvas'); th.width = 54; th.height = 54;
        var r = Math.max(54 / cv.width, 54 / cv.height);
        th.getContext('2d').drawImage(cv, (54 - cv.width * r) / 2, (54 - cv.height * r) / 2, cv.width * r, cv.height * r);
        var im = document.createElement('img');
        im.src = th.toDataURL('image/jpeg', 0.8);
        im.style.cssText = 'width:54px;height:54px;border-radius:8px;border:2px solid #22c55e;object-fit:cover;';
        var box = $('tlMauThumbs');
        if (box) box.appendChild(im);
        capNhatMauUI();
        return true;
    }
    function capNhatMauUI() {
        var n = mau.hists.length;
        window.__vc.mau = n;
        if ($('tlMauSo')) {
            $('tlMauSo').textContent = n + '/6 mẫu';
            $('tlMauSo').className = 'badge ' + (n >= 4 ? 'bg-success' : 'bg-secondary');
        }
        if ($('btnVidStart')) $('btnVidStart').disabled = n < 4 || vc.running;
    }
    function xoaMau() {
        if (vc.running) { status('⚠️ Đang đếm video — bấm "Dừng" trước khi xóa mẫu.'); return; }
        mau.hists = [];
        if ($('tlMauThumbs')) $('tlMauThumbs').innerHTML = '';
        capNhatMauUI();
        status('Đã xóa toàn bộ ảnh mẫu.');
    }
    /** Trich 6 khung hinh deu nhau tu video mau de lam anh mau */
    function mauTuVideo(file) {
        var v = document.createElement('video');
        v.muted = true; v.playsInline = true;
        v.src = URL.createObjectURL(file);
        v.addEventListener('loadedmetadata', function () {
            var batDau = function () {
                var idx = 0, moc = [];
                for (var i = 0; i < 6; i++) moc.push(v.duration * (i + 0.5) / 6);
                var buoc = function () {
                    if (idx >= moc.length || mau.hists.length >= 6) {
                        URL.revokeObjectURL(v.src);
                        status('🎞️ Đã trích ' + Math.min(idx, 6) + ' khung hình từ video làm mẫu.');
                        return;
                    }
                    v.currentTime = moc[idx];
                };
                v.addEventListener('seeked', function () {
                    themMauTuNguon(v, v.videoWidth, v.videoHeight);
                    idx++;
                    buoc();
                });
                buoc();
            };
            if (isFinite(v.duration) && v.duration > 0) { batDau(); return; }
            // webm tu MediaRecorder bao duration=Infinity -> ep seek xa de trinh duyet tinh duration that
            v.addEventListener('durationchange', function chinhXong() {
                if (!isFinite(v.duration) || v.duration <= 0) return;
                v.removeEventListener('durationchange', chinhXong);
                batDau();
            });
            v.currentTime = 1e7;
        });
        v.addEventListener('error', function () { status('❌ Không đọc được video mẫu.'); });
    }

    /** Fallback khi YOLO khong bat duoc: tim MOI vat co mau bao hoa
        (khac hoan toan nen bang chuyen xam) — moi cum = 1 vat */
    function timVatMau() {
        var c = els.canvas, w = c.width, h = c.height;
        if (!w || !h) return [];
        var img = c.getContext('2d').getImageData(0, 0, w, h).data;
        var CELL = 16, gw = Math.floor(w / CELL), gh = Math.floor(h / CELL);
        if (gw < 4 || gh < 4) return [];
        var grid = [];
        for (var gy = 0; gy < gh; gy++) {
            grid.push(new Array(gw).fill(0));
            for (var gx = 0; gx < gw; gx++) {
                var hit = 0, tot = 0;
                for (var y = gy * CELL; y < (gy + 1) * CELL; y += 4) {
                    for (var x = gx * CELL; x < (gx + 1) * CELL; x += 4) {
                        var i = (y * w + x) * 4;
                        var r = img[i] / 255, g = img[i + 1] / 255, b = img[i + 2] / 255;
                        var mx = Math.max(r, g, b), mn = Math.min(r, g, b);
                        var s = mx > 0 ? (mx - mn) / mx : 0;
                        if (s >= 0.3 && mx >= 0.18 && mx <= 0.97) hit++;
                        tot++;
                    }
                }
                grid[gy][gx] = (tot && hit / tot > 0.45) ? 1 : 0;
            }
        }
        // BAC CAU DOC: tem nhan/vach sang nam ngang hay "cat" vat thanh 2 cum
        // -> o trong co o mau phia TREN va DUOI (cach <=2) thi lap day
        for (var by = 1; by < gh - 1; by++) {
            for (var bx = 0; bx < gw; bx++) {
                if (grid[by][bx]) continue;
                var tren = grid[by - 1][bx] || (by >= 2 && grid[by - 2][bx]);
                var duoi = grid[by + 1][bx] || (by + 2 < gh && grid[by + 2][bx]);
                if (tren && duoi) grid[by][bx] = 1;
            }
        }
        var seen = {}, vats = [];
        for (var sy = 0; sy < gh; sy++) for (var sx = 0; sx < gw; sx++) {
            if (!grid[sy][sx] || seen[sy + ',' + sx]) continue;
            var st = [[sx, sy]], mnX = sx, mxX = sx, mnY = sy, mxY = sy, size = 0;
            while (st.length) {
                var p = st.pop(), key = p[1] + ',' + p[0];
                if (seen[key]) continue;
                seen[key] = 1;
                if (p[0] < 0 || p[0] >= gw || p[1] < 0 || p[1] >= gh || !grid[p[1]][p[0]]) continue;
                size++;
                mnX = Math.min(mnX, p[0]); mxX = Math.max(mxX, p[0]);
                mnY = Math.min(mnY, p[1]); mxY = Math.max(mxY, p[1]);
                st.push([p[0] + 1, p[1]]); st.push([p[0] - 1, p[1]]); st.push([p[0], p[1] + 1]); st.push([p[0], p[1] - 1]);
            }
            // vat du lon (>=0.5% khung) va khong choan ca khung (<=30%)
            if (size >= gw * gh * 0.005 && size <= gw * gh * 0.3) {
                vats.push({ x1: mnX * CELL, y1: mnY * CELL, x2: (mxX + 1) * CELL, y2: (mxY + 1) * CELL, score: 0.85, fallback: true });
            }
        }
        // GOP cum chong lan nhau (manh vo cua cung 1 vat) de khong dem trung
        for (var a = 0; a < vats.length; a++) {
            for (var b2 = a + 1; b2 < vats.length; b2++) {
                var A = vats[a], B = vats[b2];
                var ix = Math.max(0, Math.min(A.x2, B.x2) - Math.max(A.x1, B.x1));
                var iy = Math.max(0, Math.min(A.y2, B.y2) - Math.max(A.y1, B.y1));
                var giao = ix * iy;
                var nho = Math.min((A.x2 - A.x1) * (A.y2 - A.y1), (B.x2 - B.x1) * (B.y2 - B.y1));
                if (nho > 0 && giao / nho > 0.45) {
                    A.x1 = Math.min(A.x1, B.x1); A.y1 = Math.min(A.y1, B.y1);
                    A.x2 = Math.max(A.x2, B.x2); A.y2 = Math.max(A.y2, B.y2);
                    vats.splice(b2, 1); b2--;
                }
            }
        }
        return vats;
    }

    function iou(a, b) {
        var x1 = Math.max(a.x1, b.x1), y1 = Math.max(a.y1, b.y1);
        var x2 = Math.min(a.x2, b.x2), y2 = Math.min(a.y2, b.y2);
        var giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
        var sa = (a.x2 - a.x1) * (a.y2 - a.y1), sb = (b.x2 - b.x1) * (b.y2 - b.y1);
        return giao / (sa + sb - giao + 1e-9);
    }

    /** So khop detections voi tracks (du doan theo van toc), dem vat moi on dinh */
    function capNhatTracks(dets) {
        vc.tracks.forEach(function (t) {
            t.pred = { x1: t.box.x1 + t.vx, y1: t.box.y1 + t.vy, x2: t.box.x2 + t.vx, y2: t.box.y2 + t.vy };
            t.matched = false;
        });
        dets.forEach(function (d) {
            var best = null, bi = 0;
            vc.tracks.forEach(function (t) {
                if (t.matched) return;
                var v = iou(d, t.pred);
                if (v > bi) { bi = v; best = t; }
            });
            if (best && bi > 0.22) {
                best.vx = ((d.x1 - best.box.x1) + (d.x2 - best.box.x2)) / 2;
                best.vy = ((d.y1 - best.box.y1) + (d.y2 - best.box.y2)) / 2;
                best.box = d; best.seen++; best.miss = 0; best.matched = true;
                d.track = best;
            } else {
                var t2 = { id: vc.nextId++, box: d, vx: 0, vy: 0, seen: 1, miss: 0, matched: true, counted: false, ok: null, sim: 0 };
                vc.tracks.push(t2);
                d.track = t2;
            }
        });
        vc.tracks.forEach(function (t) { if (!t.matched) t.miss++; });
        vc.tracks = vc.tracks.filter(function (t) { return t.miss <= 4; });

        // Vat xuat hien on dinh (>=2 khung) -> doi chieu MAU va dem.
        // Vat con CHAM MEP khung (dang di vao) thi hoan xac minh — hist chua du tin cay
        var cw = els.canvas.width, ch = els.canvas.height, MEP = 12;
        vc.tracks.forEach(function (t) {
            if (t.counted || t.seen < 2 || t.miss > 0) return;
            var oRia = t.box.x1 <= MEP || t.box.y1 <= MEP || t.box.x2 >= cw - MEP || t.box.y2 >= ch - MEP;
            if (oRia && t.seen < 6) return;
            t.counted = true;
            var hist = hsvHist(t.box);
            var sim = 0;
            mau.hists.forEach(function (m) { var s = histSim(hist, m); if (s > sim) sim = s; });
            t.sim = sim;
            t.ok = mau.hists.length ? sim >= 0.74 : true;
            if (t.ok) {
                vc.dem++;
                beepTick();
            } else {
                vc.sai++;
                var now = Date.now();
                if (now - vc.lastWarn > 6000) {
                    vc.lastWarn = now;
                    sirenQC(2.5);
                    noiCanhBao('Cảnh báo lỗi! Phát hiện sản phẩm không đúng mẫu trên băng chuyền.');
                }
                if (!vc.luuSaiRoi) { vc.luuSaiRoi = true; vc.luuSaiPending = true; }
            }
        });
    }

    /** Ve khung + bo dem lon len canvas */
    function veKhungVideo() {
        var ctx = els.canvas.getContext('2d');
        var w = els.canvas.width;
        ctx.lineWidth = Math.max(3, w / 260);
        ctx.font = '700 ' + Math.max(13, Math.round(w / 52)) + 'px "Be Vietnam Pro", sans-serif';
        vc.tracks.forEach(function (t) {
            if (t.miss > 0) return;
            var b = t.box;
            var col = t.ok === false ? '#ef4444' : (t.ok === true ? '#22c55e' : '#38bdf8');
            ctx.strokeStyle = col;
            ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
            var nhan = t.ok === false ? '⚠ KHÔNG ĐÚNG MẪU ' + Math.round(t.sim * 100) + '%'
                     : (t.ok === true ? '✓ #' + t.id + ' · ' + Math.round(t.sim * 100) + '%' : '#' + t.id);
            ctx.fillStyle = col;
            ctx.fillRect(b.x1 - 1, Math.max(0, b.y1 - 26), ctx.measureText(nhan).width + 10, 24);
            ctx.fillStyle = '#08131f';
            ctx.fillText(nhan, b.x1 + 4, Math.max(17, b.y1 - 7));
        });
        // Bo dem lon giua tren
        var txt = vc.dem + ' / ' + (vc.target || '—');
        ctx.font = '900 ' + Math.max(26, Math.round(w / 16)) + 'px "Be Vietnam Pro", sans-serif';
        var tw = ctx.measureText(txt).width;
        ctx.fillStyle = 'rgba(8,19,31,.72)';
        ctx.fillRect(w / 2 - tw / 2 - 18, 10, tw + 36, Math.max(40, Math.round(w / 13)));
        ctx.fillStyle = vc.target && vc.dem >= vc.target ? '#22c55e' : '#f8fafc';
        ctx.fillText(txt, w / 2 - tw / 2, Math.max(38, Math.round(w / 13)) + 2);
        // Luu anh bang chung vat SAI MAU (sau khi da ve khung do)
        if (vc.luuSaiPending) {
            vc.luuSaiPending = false;
            uploadAnhKho('Đếm video: phát hiện sản phẩm KHÔNG ĐÚNG MẪU trong lô "' + (els.tenLo.value || 'chưa đặt tên') + '"');
        }
    }

    function capNhatVidUI() {
        window.__vc.dem = vc.dem; window.__vc.sai = vc.sai; window.__vc.running = vc.running;
        if ($('tlVidDem')) $('tlVidDem').textContent = vc.dem + '/' + (vc.target || '—');
        if ($('tlVidBar')) {
            var pct = vc.target ? Math.min(100, Math.round(vc.dem * 100 / vc.target)) : 0;
            $('tlVidBar').style.width = pct + '%';
            $('tlVidBar').className = 'progress-bar ' + (pct >= 100 ? 'bg-success' : 'progress-bar-striped progress-bar-animated');
        }
        if ($('tlVidKq')) $('tlVidKq').innerHTML =
            'Đúng mẫu: <b class="text-success">' + vc.dem + '</b> · Sai mẫu: <b class="text-danger">' + vc.sai + '</b>' +
            (vc.running ? ' · <span class="text-muted">đang đếm...</span>' : '');
    }

    function vcTick(gen) {
        if (!vc.running || gen !== vc.gen) return;
        if (vc.dungVideo && els.vidFile && els.vidFile.ended) { vcXong(false); return; }
        if (!drawSource()) {
            // chua co khung hinh — cho toi da ~12s roi bao loi
            vc.choKhung++;
            if (vc.choKhung > 40) { status('❌ Không lấy được khung hình từ video/camera.'); vcXong(false); return; }
            vc.timer = setTimeout(function () { vcTick(gen); }, 300);
            return;
        }
        vc.choKhung = 0;
        // Tam DUNG video trong luc AI xu ly de khong bo sot san pham tren may yeu
        if (vc.dungVideo && els.vidFile && !els.vidFile.paused) { try { els.vidFile.pause(); } catch (e) { } }
        var lop = CLS_MAP[els.selLop.value];
        var p = session ? detect(lop).catch(function () { return []; }) : Promise.resolve([]);
        p.then(function (boxes) {
            if (!vc.running || gen !== vc.gen) return;
            // Ket hop YOLO + do mau: them cum mau mau KHONG trung voi box YOLO
            timVatMau().forEach(function (f) {
                var trung = boxes.some(function (b) { return iou(f, b) > 0.3; });
                if (!trung) boxes.push(f);
            });
            capNhatTracks(boxes);
            veKhungVideo();
            capNhatVidUI();
            if (vc.target > 0 && vc.dem >= vc.target) { vcXong(true); return; }
            if (vc.dungVideo && els.vidFile && !els.vidFile.ended) { try { els.vidFile.play(); } catch (e) { } }
            vc.timer = setTimeout(function () { vcTick(gen); }, 300);
        });
    }

    /** Khoa/mo cac nut co the pha phien dem dang chay */
    function khoaVidControls(lock) {
        ['tlVidInput', 'btnVidDemo', 'btnVidClear', 'btnMauXoa', 'btnMauDemo', 'btnMauCam', 'tlMauFile', 'tlMauVid', 'btnCount', 'btnQC', 'btnCam', 'fileInput'].forEach(function (id) {
            if ($(id)) $(id).disabled = lock;
        });
    }

    function vcStart() {
        if (vc.running) return;
        if (mau.hists.length < 4) { status('⚠️ Cần đăng ký ít nhất 4 ảnh mẫu sản phẩm trước (mục ①).'); return; }
        var target = parseInt(els.duKien.value, 10) || 0;
        if (target <= 0) { status('⚠️ Nhập "Số lượng khai báo" (mục tiêu, VD 500) rồi bấm lại.'); return; }
        vc.dungVideo = !!(els.vidFile && els.vidFile.src && !els.vidFile.error);
        if (!vc.dungVideo && !stream) { status('⚠️ Bật camera HOẶC nạp video băng chuyền trước.'); return; }
        vc.running = true; vc.dem = 0; vc.sai = 0; vc.target = target;
        vc.tracks = []; vc.nextId = 1; vc.lastWarn = 0; vc.luuSaiRoi = false; vc.luuSaiPending = false;
        vc.choKhung = 0; vc.gen++;
        window.__vc.done = false;
        if (vc.dungVideo) { try { els.vidFile.currentTime = 0; } catch (e) { } els.vidFile.play(); }
        $('btnVidStart').disabled = true;
        if ($('btnVidStop')) $('btnVidStop').disabled = false;
        khoaVidControls(true);
        status('🎬 Đang đếm theo video — mục tiêu ' + target + ' sản phẩm đúng mẫu...');
        capNhatVidUI();
        vcTick(vc.gen);
    }

    function vcXong(du) {
        if (!vc.running && window.__vc.done) return;
        vc.running = false; vc.gen++;
        clearTimeout(vc.timer);
        if (els.vidFile) { try { els.vidFile.pause(); } catch (e) { } }
        if ($('btnVidStop')) $('btnVidStop').disabled = true;
        khoaVidControls(false);
        capNhatMauUI();
        capNhatVidUI();
        if (du) {
            chimeOk();
            noiCanhBao('Hoàn thành! Đã đếm đủ ' + vc.dem + ' trên ' + vc.target + ' sản phẩm. Đạt yêu cầu.');
            status('✅ ĐỦ SỐ LƯỢNG ' + vc.dem + '/' + vc.target + '! Bấm "Lưu phiên kiểm đếm" để ghi sổ.');
        } else if (vc.target > 0 && vc.dem < vc.target) {
            noiCanhBao('Video kết thúc. Mới đếm được ' + vc.dem + ' trên ' + vc.target + ' sản phẩm. Chưa đủ số lượng.');
            status('⚠️ Video hết — mới ' + vc.dem + '/' + vc.target + ' (THIẾU ' + (vc.target - vc.dem) + ').');
        } else {
            status('⏹ Đã dừng — đếm được ' + vc.dem + ' sản phẩm đúng mẫu, ' + vc.sai + ' sai mẫu.');
        }
        els.kq.dataset.dem = vc.dem;
        els.kq.dataset.giong = vc.dem;
        els.kq.dataset.khac = vc.sai;
        els.kq.innerHTML = 'Đếm video: <b style="font-size:1.3rem;color:#0a2f57;">' + vc.dem + '</b>/' + (vc.target || '—') +
            (vc.target > 0 ? (vc.dem >= vc.target ? ' <b class="text-success">ĐỦ ✔</b>' : ' <b class="text-danger">THIẾU ' + (vc.target - vc.dem) + ' ⚠</b>') : '') +
            '<div class="mt-1">Sai mẫu bị cảnh báo: <b class="text-danger">' + vc.sai + '</b></div>';
        $('btnSaveTally').disabled = false;
        window.__vc.done = true;
    }

    function luuPhien() {
        var fd = new FormData();
        fd.append('tenLo', els.tenLo.value);
        fd.append('lopDem', els.selLop.value);
        fd.append('duKien', els.duKien.value);
        fd.append('demDuoc', els.kq.dataset.dem || '');
        fd.append('soGiong', els.kq.dataset.giong || '');
        fd.append('soKhac', els.kq.dataset.khac || '');
        fd.append('chieuCao', $('tlCao').value);
        fd.append('trongLuong', $('tlNang').value);
        fd.append('ghiChu', $('tlGhiChu').value);
        $('btnSaveTally').disabled = true;
        fetch(ROOT + 'Admin/Security/SaveTally', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (d.ok) { status('💾 Đã lưu phiên #' + d.maKD + ' — tải lại trang để thấy trong lịch sử.'); }
                else { status('❌ ' + (d.message || 'Không lưu được.')); $('btnSaveTally').disabled = false; }
            })
            .catch(function () { status('❌ Mất kết nối.'); $('btnSaveTally').disabled = false; });
    }

    document.addEventListener('DOMContentLoaded', function () {
        els = {
            status: $('tlStatus'), canvas: $('tlCanvas'), video: $('tlVideo'), work: $('tlWork'),
            selLop: $('tlLop'), tenLo: $('tlTenLo'), duKien: $('tlDuKien'), kq: $('tlKq'),
            vidFile: $('tlVidFile'), lastImg: null
        };
        if (!els.status) return;

        /* ---- Dang ky san pham mau (4-6 anh / video / camera) ---- */
        if ($('tlMauFile')) $('tlMauFile').addEventListener('change', function () {
            var fs = Array.prototype.slice.call(this.files || []);
            if (!fs.length) return;
            fs.slice(0, 6 - mau.hists.length).forEach(function (f) {
                var rd = new FileReader();
                rd.onload = function () {
                    var img = new Image();
                    img.onload = function () { themMauTuNguon(img); };
                    img.src = rd.result;
                };
                rd.readAsDataURL(f);
            });
            this.value = '';
        });
        if ($('tlMauVid')) $('tlMauVid').addEventListener('change', function () {
            if (this.files && this.files[0]) mauTuVideo(this.files[0]);
            this.value = '';
        });
        if ($('btnMauCam')) $('btnMauCam').addEventListener('click', function () {
            if (!stream || els.video.readyState < 2) { status('⚠️ Bật camera trước rồi mới chụp mẫu được.'); return; }
            if (themMauTuNguon(els.video, els.video.videoWidth, els.video.videoHeight))
                status('📸 Đã chụp mẫu ' + mau.hists.length + '/6 từ camera.');
        });
        if ($('btnMauDemo')) $('btnMauDemo').addEventListener('click', function () {
            xoaMau();
            var i = 1;
            var napTiep = function () {
                if (i > 6) { status('✅ Đã nạp bộ 6 ảnh mẫu chuẩn (chai nước cam) — nhập mục tiêu rồi Bắt đầu đếm.'); return; }
                var img = new Image();
                img.onload = function () { themMauTuNguon(img); i++; napTiep(); };
                img.onerror = function () { status('❌ Thiếu ảnh mẫu số ' + i + '.'); };
                img.src = ROOT + 'Content/img/mau-sp-' + i + '.png?v=1';
            };
            napTiep();
        });
        if ($('btnMauXoa')) $('btnMauXoa').addEventListener('click', xoaMau);

        /* ---- Dem luy ke theo video ---- */
        if ($('tlVidInput')) $('tlVidInput').addEventListener('change', function () {
            if (!this.files || !this.files[0]) return;
            els.vidFile.src = URL.createObjectURL(this.files[0]);
            els.vidFile.load();
            status('🎬 Đã nạp video — nhập mục tiêu rồi bấm "Bắt đầu đếm video".');
        });
        if ($('btnVidDemo')) $('btnVidDemo').addEventListener('click', function () {
            els.vidFile.src = ROOT + 'Content/video/demo-count.webm?v=1';
            els.vidFile.load();
            status('🎬 Đã nạp video demo băng chuyền — nhập mục tiêu (VD 11) rồi Bắt đầu đếm.');
        });
        if ($('btnVidClear')) $('btnVidClear').addEventListener('click', function () {
            if (vc.running) return;
            try { els.vidFile.pause(); } catch (e) { }
            els.vidFile.removeAttribute('src');
            els.vidFile.load();
            status('Đã gỡ video — bật camera rồi bấm "Bắt đầu đếm video" để đếm trực tiếp.');
        });
        if (els.vidFile) els.vidFile.addEventListener('error', function () {
            if (vc.running && vc.dungVideo) { status('❌ Không phát được video — định dạng/codec không hỗ trợ.'); vcXong(false); }
            else status('❌ Không đọc được video — hãy dùng MP4 (H.264) hoặc WebM.');
        });
        if ($('btnVidStart')) $('btnVidStart').addEventListener('click', vcStart);
        if ($('btnVidStop')) $('btnVidStop').addEventListener('click', function () { vcXong(false); });
        // Webcam + dem video khong phu thuoc model YOLO (co fallback mau sac)
        $('btnCam').disabled = false;
        capNhatMauUI();

        $('fileInput').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            var rd = new FileReader();
            rd.onload = function () {
                var img = new Image();
                img.onload = function () { els.lastImg = img; drawSource(); status('🖼️ Đã nạp ảnh — bấm Đếm & phân loại.'); };
                img.src = rd.result;
            };
            rd.readAsDataURL(f);
        });
        $('btnCam').addEventListener('click', function () {
            if (stream) {
                stream.getTracks().forEach(function (t) { t.stop(); }); stream = null;
                this.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật camera';
                return;
            }
            var self = this;
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                status('⚠️ Trình duyệt chỉ cho phép camera trên HTTPS/localhost — hãy mở site qua https:// rồi thử lại.');
                return;
            }
            navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 } }, audio: false })
                .then(function (s) {
                    stream = s; els.video.srcObject = s; els.video.play();
                    self.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
                    status('🎥 Camera chạy — sắp xếp hàng rồi bấm Đếm & phân loại.');
                })
                .catch(function () { status('❌ Không truy cập được camera.'); });
        });
        $('btnCount').addEventListener('click', demVaPhanLoai);
        $('btnSaveTally').addEventListener('click', luuPhien);
        if ($('btnQC')) $('btnQC').addEventListener('click', kiemTraThung);
        document.querySelectorAll('.tl-sample').forEach(function (b) {
            b.addEventListener('click', function () {
                var img = new Image();
                img.onload = function () { els.lastImg = img; drawSource(); status('🖼️ Đã nạp ảnh mẫu — bấm Kiểm tra lỗi thùng hàng.'); };
                img.src = b.getAttribute('data-src');
            });
        });
        init();
    });
})();
