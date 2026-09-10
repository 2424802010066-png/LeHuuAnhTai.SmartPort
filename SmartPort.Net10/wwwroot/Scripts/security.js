/* ============================================================
   SMARTGUARD — TRAM CAMERA AN NINH AI
   ------------------------------------------------------------
   1. XAC NHAN CONTAINER 2 BUOC (OCR ISO 6346: 4 chu + 7 so)
      - Quet lan 1: luu ma + anh goc
      - Quet lan 2: doi chieu -> DUNG: chuong "ting" ✔
                                -> SAI : coi canh bao + bao an ninh
   2. GIAM SAT NGUY HIEM (vong lap AI tren webcam/anh)
      - LUA      : phan tich mau HSV vung do-cam ruc + nhap nhay giua khung
      - NGUOI NGA: 4 dieu kien dong thoi (nam ngang RO RET w/h>1.45,
        vi tri THAP trong khung, khong choan man hinh, xac nhan 3 luot
        quet lien tiep) - chong bao oan nguoi NGOI gan webcam
      - VAT TINH : doi tuong dung im qua nhieu khung trong khi
                   xung quanh van chuyen dong (theo doi IoU)
   3. KHI CO SU CO: coi hu (Web Audio) + chup anh + ghi VIDEO 10s
      (MediaRecorder) + gui KEM VI TRI ve phong an ninh (server)
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

    var session = null, worker = null, stream = null, busy = false, watchTimer = null;
    var els = {};

    /* ================== AM THANH (Web Audio — khong can file mp3) ================== */
    var actx = null;
    function audio() {
        if (!actx) actx = new (window.AudioContext || window.webkitAudioContext)();
        if (actx.state === 'suspended') actx.resume();
        return actx;
    }
    // Chuong "ting-ting" khi xac nhan DUNG
    function chime() {
        var a = audio(), t = a.currentTime;
        [[880, 0], [1318.5, 0.16]].forEach(function (n) {
            var o = a.createOscillator(), g = a.createGain();
            o.type = 'sine'; o.frequency.value = n[0];
            g.gain.setValueAtTime(0.0001, t + n[1]);
            g.gain.exponentialRampToValueAtTime(0.4, t + n[1] + 0.02);
            g.gain.exponentialRampToValueAtTime(0.0001, t + n[1] + 0.9);
            o.connect(g); g.connect(a.destination);
            o.start(t + n[1]); o.stop(t + n[1] + 1);
        });
    }
    // Coi hu len-xuong (sirens) khi NGUY HIEM
    var sirenNodes = null;
    function sirenStart(seconds) {
        sirenStop();
        var a = audio(), t = a.currentTime;
        var o = a.createOscillator(), g = a.createGain(), lfo = a.createOscillator(), lg = a.createGain();
        o.type = 'sawtooth'; o.frequency.value = 650;
        lfo.type = 'sine'; lfo.frequency.value = 3;        // dao dong 3Hz
        lg.gain.value = 220;                               // bien do +-220Hz
        lfo.connect(lg); lg.connect(o.frequency);
        g.gain.value = 0.25;
        o.connect(g); g.connect(a.destination);
        o.start(t); lfo.start(t);
        sirenNodes = { o: o, lfo: lfo, g: g };
        if (seconds) setTimeout(sirenStop, seconds * 1000);
    }
    function sirenStop() {
        if (!sirenNodes) return;
        try { sirenNodes.o.stop(); sirenNodes.lfo.stop(); } catch (e) { }
        sirenNodes = null;
    }
    // Beep canh bao ngan (sai OCR)
    function warnBeep() {
        var a = audio(), t = a.currentTime;
        for (var i = 0; i < 3; i++) {
            var o = a.createOscillator(), g = a.createGain();
            o.type = 'square'; o.frequency.value = 320;
            g.gain.setValueAtTime(0.3, t + i * 0.25);
            g.gain.exponentialRampToValueAtTime(0.0001, t + i * 0.25 + 0.18);
            o.connect(g); g.connect(a.destination);
            o.start(t + i * 0.25); o.stop(t + i * 0.25 + 0.2);
        }
    }

    /* ================== NAP ENGINE ================== */
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
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    return c.put(url, r.clone()).then(function () { return r.arrayBuffer(); });
                });
            });
        });
    }
    function init() {
        status('Đang tải YOLO26 + Tesseract OCR...', true);
        var useGpu = !!navigator.gpu;
        var pY = loadScript(ORT_CDN + (useGpu ? 'ort.webgpu.min.js' : 'ort.min.js'))
            .then(function () {
                ort.env.wasm.wasmPaths = ORT_CDN;
                return fetchModel(YOLO_URL);
            })
            .then(function (buf) {
                return ort.InferenceSession.create(buf, { executionProviders: useGpu ? ['webgpu', 'wasm'] : ['wasm'] })
                    .catch(function () { return ort.InferenceSession.create(buf, { executionProviders: ['wasm'] }); });
            })
            .then(function (s) { session = s; });
        var pT = loadScript(TESS_CDN)
            .then(function () { return Tesseract.createWorker('eng', 1, { workerPath: VEN + 'tess/worker.min.js', corePath: VEN + 'tess-core', langPath: VEN + 'tessdata', logger: function () { } }); })
            .then(function (w) {
                worker = w;
                return worker.setParameters({ tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ' });
            });
        Promise.all([pY, pT]).then(function () {
            status('✅ SmartGuard sẵn sàng! Bật camera hoặc chọn ảnh để bắt đầu.');
            enableUi(true);
        }).catch(function (e) {
            console.error(e);
            status('❌ Không tải được engine AI — kiểm tra Internet rồi tải lại trang.');
        });
    }
    function enableUi(on) {
        ['btnCam', 'fileInput', 'btnStep', 'btnWatch', 'btnSiren'].forEach(function (id) {
            if ($(id)) $(id).disabled = !on;
        });
        document.querySelectorAll('.sg-sample').forEach(function (b) { b.disabled = !on; });
    }
    function status(html, spin) {
        if (els.status) els.status.innerHTML = (spin ? '<span class="spinner-border spinner-border-sm me-2"></span>' : '') + html;
    }

    /* ================== NGUON HINH ================== */
    function drawSource() {
        // ve khung hinh hien tai (video hoac anh) vao canvas chinh, tra ve true neu co
        var c = els.canvas, ctx = c.getContext('2d');
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
    function startCam() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.\nHãy truy cập site qua https:// rồi thử lại nhé.');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 1280 } }, audio: false })
            .then(function (s) {
                stream = s;
                els.video.srcObject = s;
                els.video.play();
                els.btnCam.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
                status('🎥 Camera đang chạy — chọn chế độ Xác nhận container hoặc Giám sát.');
                startRecorderBuffer();
            })
            .catch(function () { status('❌ Không truy cập được camera (cần cấp quyền).'); });
    }
    function stopCam() {
        watchStop();
        stopRecorderBuffer();
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        els.btnCam.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật camera';
    }

    /* ================== VIDEO BANG CHUNG (MediaRecorder xoay vong) ==================
       WebM chi PHAT DUOC khi blob bat dau bang chunk header (EBML/init segment).
       Ban cu dung ring buffer shift() -> sau ~14s chunk header bi day ra ngoai,
       video bang chung mo khong len. Ban nay: cu ~16s thi stop + start lai
       recorder — MOI doan luon tu chua header, ghep blob luc nao cung phat duoc. */
    var mediaRec = null, chunks = [], recGen = 0;
    function startRecorderBuffer() {
        if (!stream || !window.MediaRecorder) return;
        recGen++;
        var gen = recGen;
        function batDau() {
            if (!stream || gen !== recGen) return;
            try { mediaRec = new MediaRecorder(stream, { mimeType: 'video/webm' }); }
            catch (e) { mediaRec = null; return; }
            chunks = [];
            mediaRec.ondataavailable = function (e) {
                if (e.data && e.data.size) {
                    chunks.push(e.data);
                    // Du ~16s thi xoay vong doan moi (tru khi dang dong goi bang chung)
                    if (chunks.length >= 8 && !uploadEvidence._cho) {
                        try { mediaRec.stop(); } catch (e2) { }
                    }
                }
            };
            mediaRec.onstop = function () {
                if (gen === recGen && stream) batDau();
            };
            try { mediaRec.start(2000); } catch (e) { mediaRec = null; }
        }
        batDau();
    }
    function stopRecorderBuffer() {
        recGen++;                                            // chan onstop tu khoi dong lai
        if (mediaRec && mediaRec.state !== 'inactive') { try { mediaRec.stop(); } catch (e) { } }
        mediaRec = null; chunks = [];
    }
    function uploadEvidence(maSC) {
        // ghi them ~2s roi dong goi video gui len phong an ninh
        if (!mediaRec) return;
        uploadEvidence._cho = true;                          // hoan xoay vong trong luc cho
        setTimeout(function () {
            var blob = new Blob(chunks.slice(), { type: 'video/webm' });
            uploadEvidence._cho = false;
            if (blob.size < 5000) return;
            var fd = new FormData();
            fd.append('maSC', maSC);
            fd.append('video', blob, 'evidence.webm');
            fetch(ROOT + 'Admin/Security/UploadVideo?maSC=' + maSC, { method: 'POST', body: fd, credentials: 'same-origin' })
                .catch(function () { });
        }, 2200);
    }

    /* ================== BAO CAO VE PHONG AN NINH ================== */
    var lastReport = {};       // chong lap client: loai -> timestamp
    function baoAnNinh(loai, mucDo, moTa) {
        var now = Date.now();
        if (lastReport[loai] && now - lastReport[loai] < 30000) return;   // 30s/loai
        lastReport[loai] = now;

        // anh bang chung thu nho
        var thumb = document.createElement('canvas');
        var c = els.canvas;
        var sc = Math.min(1, 520 / Math.max(c.width, 1));
        thumb.width = Math.round(c.width * sc); thumb.height = Math.round(c.height * sc);
        thumb.getContext('2d').drawImage(c, 0, 0, thumb.width, thumb.height);

        var fd = new FormData();
        fd.append('loai', loai);
        fd.append('mucDo', mucDo);
        fd.append('viTri', els.viTri.value);
        fd.append('moTa', moTa);
        fd.append('anh', thumb.toDataURL('image/jpeg', 0.75));
        fetch(ROOT + 'Admin/Security/Report', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (d.ok && d.maSC) {
                    themNhatKy((mucDo === 'Nguy hiểm' ? '🚨 ' : '⚠️ ') + moTa + ' — đã báo phòng an ninh (#' + d.maSC + ', vị trí: ' + els.viTri.value + ')');
                    uploadEvidence(d.maSC);
                }
            }).catch(function () { });

        if (mucDo === 'Nguy hiểm' && els.autoSiren.checked) sirenStart(6);
    }
    function themNhatKy(text) {
        var d = document.createElement('div');
        d.className = 'sg-log-item';
        d.innerHTML = '<span class="text-muted">' + new Date().toLocaleTimeString('vi-VN') + '</span> ' + text;
        els.log.insertBefore(d, els.log.firstChild);
        while (els.log.children.length > 12) els.log.removeChild(els.log.lastChild);
    }

    /* ================== 1. XAC NHAN CONTAINER 2 BUOC ================== */
    var step = 0;              // 0 = cho quet lan 1; 1 = da co ma, cho quet lan 2
    var luuLan1 = null;        // { ma, anhDataUrl }
    var RE_CONT = /([A-Z]{4})\s*(\d{6})\s*(\d)/;

    function ocrContainer() {
        return worker.recognize(els.canvas.toDataURL('image/png')).then(function (r) {
            var m = (r.data.text || '').toUpperCase().match(RE_CONT);
            return m ? (m[1] + m[2] + m[3]) : null;
        });
    }
    function quet() {
        if (busy || !worker) return;
        if (!drawSource()) { status('Chưa có hình — bật camera hoặc chọn ảnh trước.'); return; }
        busy = true;
        status('🔤 OCR đang đọc mã container (4 chữ + 7 số)...', true);
        ocrContainer().then(function (ma) {
            if (!ma) {
                status('⚠️ Chưa đọc được mã ISO 6346 — đưa mã 4 chữ + 7 số vào giữa khung rồi thử lại.');
                return;
            }
            if (step === 0) {
                luuLan1 = { ma: ma, anh: els.canvas.toDataURL('image/jpeg', 0.7) };
                step = 1;
                els.stepBadge.textContent = 'BƯỚC 2/2';
                els.btnStep.innerHTML = '<i class="bi bi-check2-circle me-1"></i>Quét lần 2 — ĐỐI CHIẾU';
                els.ocrKq.innerHTML = 'Lần 1 đã lưu mã: <b class="sg-code">' + ma + '</b><br/>Đưa CHÍNH container đó vào camera lần nữa để đối chiếu.';
                themNhatKy('📸 Lần 1: đã lưu mã <b>' + ma + '</b> + ảnh gốc');
                chime();
            } else {
                if (ma === luuLan1.ma) {
                    els.ocrKq.innerHTML = '<span class="sg-ok">✔ CHÍNH XÁC</span> — mã <b class="sg-code">' + ma + '</b> khớp với lần quét đầu.';
                    chime();
                    themNhatKy('✅ Xác nhận ĐÚNG container <b>' + ma + '</b>');
                } else {
                    els.ocrKq.innerHTML = '<span class="sg-bad">✘ KHÔNG KHỚP!</span> Lần 1: <b class="sg-code">' + luuLan1.ma + '</b> · Lần 2: <b class="sg-code">' + ma + '</b>';
                    warnBeep();
                    baoAnNinh('OCRSai', 'Chú ý', 'Xác nhận container KHÔNG khớp: lần 1 ' + luuLan1.ma + ' / lần 2 ' + ma);
                }
                step = 0; luuLan1 = null;
                els.stepBadge.textContent = 'BƯỚC 1/2';
                els.btnStep.innerHTML = '<i class="bi bi-camera me-1"></i>Quét lần 1 — LƯU MÃ';
            }
            status('Sẵn sàng.');
        }).catch(function (e) {
            status('❌ Lỗi OCR: ' + e.message);
        }).then(function () { busy = false; });
    }

    /* ================== 2. GIAM SAT NGUY HIEM ================== */
    // ---- YOLO detect ----
    function detect() {
        var c = els.canvas;
        var w = c.width, h = c.height;
        var cv = els.work, ctx = cv.getContext('2d');
        cv.width = INPUT; cv.height = INPUT;
        var sc = Math.min(INPUT / w, INPUT / h);
        var nw = Math.round(w * sc), nh = Math.round(h * sc);
        var dx = (INPUT - nw) / 2, dy = (INPUT - nh) / 2;
        ctx.fillStyle = '#727272'; ctx.fillRect(0, 0, INPUT, INPUT);
        ctx.drawImage(c, dx, dy, nw, nh);
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
                if (scr < 0.4) continue;
                boxes.push({
                    x1: (o.data[b] - dx) / sc, y1: (o.data[b + 1] - dy) / sc,
                    x2: (o.data[b + 2] - dx) / sc, y2: (o.data[b + 3] - dy) / sc,
                    score: scr, cls: cls
                });
            }
            return boxes;
        });
    }
    // ---- TOI UU: phat hien LUA theo LUOI 8x8 — khoanh DUNG vung chay,
    //      yeu cau >=2 o chay KE NHAU de giam bao nham (mot diem do le loi
    //      nhu ao do, den xe... se khong kich hoat) + van doi hoi nhap nhay ----
    var fireRatioPrev = null;
    function fireScan() {
        var c = els.canvas, ctx = c.getContext('2d');
        var w = c.width, h = c.height;
        if (!w || !h) return { ratio: 0, flicker: 0, rect: null };
        var img = ctx.getImageData(0, 0, w, h).data;
        var G = 8, cw = Math.floor(w / G), ch = Math.floor(h / G);
        var cell = [];
        for (var gy = 0; gy < G; gy++) {
            cell.push(new Array(G).fill(0));
            for (var gx = 0; gx < G; gx++) {
                var hot = 0, tot = 0;
                for (var y = gy * ch; y < (gy + 1) * ch; y += 6) {
                    for (var x = gx * cw; x < (gx + 1) * cw; x += 6) {
                        var i = (y * w + x) * 4;
                        var r = img[i], g = img[i + 1], b = img[i + 2];
                        if (r > 200 && g > 50 && g < 190 && b < 90 && r - b > 120) hot++;
                        tot++;
                    }
                }
                cell[gy][gx] = tot ? hot / tot : 0;
            }
        }
        // O "chay" = >=8% pixel nong; tim o chay co HANG XOM chay (cum >=2)
        var minX = G, maxX = -1, minY = G, maxY = -1, cum = 0, tongRatio = 0;
        for (var yy = 0; yy < G; yy++) {
            for (var xx = 0; xx < G; xx++) {
                tongRatio += cell[yy][xx];
                if (cell[yy][xx] < 0.08) continue;
                var keCanh = (xx > 0 && cell[yy][xx - 1] >= 0.08) || (xx < G - 1 && cell[yy][xx + 1] >= 0.08) ||
                             (yy > 0 && cell[yy - 1][xx] >= 0.08) || (yy < G - 1 && cell[yy + 1][xx] >= 0.08);
                if (!keCanh) continue;
                cum++;
                minX = Math.min(minX, xx); maxX = Math.max(maxX, xx);
                minY = Math.min(minY, yy); maxY = Math.max(maxY, yy);
            }
        }
        var ratio = tongRatio / (G * G);
        var flicker = fireRatioPrev === null ? 0 : Math.abs(ratio - fireRatioPrev);
        fireRatioPrev = ratio;
        var rect = cum >= 2 ? {
            x: minX * cw, y: minY * ch,
            w: (maxX - minX + 1) * cw, h: (maxY - minY + 1) * ch
        } : null;
        return { ratio: ratio, flicker: flicker, rect: rect, cum: cum };
    }
    // ---- Vat the dung im: IoU cao qua nhieu khung (dem theo TUNG vat, gan vao b.still) ----
    var trackPrev = [];
    function iou(a, b) {
        var x1 = Math.max(a.x1, b.x1), y1 = Math.max(a.y1, b.y1);
        var x2 = Math.min(a.x2, b.x2), y2 = Math.min(a.y2, b.y2);
        var inter = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
        var ua = (a.x2 - a.x1) * (a.y2 - a.y1) + (b.x2 - b.x1) * (b.y2 - b.y1) - inter;
        return ua > 0 ? inter / ua : 0;
    }

    var WATCH_CLS = { 0: 'Người', 2: 'Ô tô', 5: 'Xe khách', 7: 'Xe tải', 28: 'Kiện hàng' };

    /* NGUOI NGA — 4 dieu kien DONG THOI (chong bao oan nguoi NGOI gan webcam):
       1. Khung nam ngang RO RET: w/h > 1.45 (nguoi ngoi gan cam la 1.1-1.4)
       2. Vi tri THAP: tam khung o nua duoi khung hinh (nga = nam duoi san;
          nguoi ngoi truoc webcam thuong chiem giua/tren khung)
       3. Khong choan man hinh: dien tich < 55% (qua gan cam = dang ngoi lam viec)
       4. Be ngang < 55% khung: nguoi NGOI GAN webcam goc rong chiem rat rong,
          nguoi nga that trong camera giam sat (gan xa/cao) thi nho hon nhieu
       5. Xac nhan >= 3 luot quet lien tiep (~4s) moi bao - chong nhieu 1 khung
       (Demo nga: nam cach camera ~2m de giong goc camera giam sat that) */
    var ngaDem = 0;
    function dauHieuNga(b, W, H) {
        var bw = b.x2 - b.x1, bh = Math.max(1, b.y2 - b.y1);
        var ar = bw / bh;
        var yTam = (b.y1 + b.y2) / 2 / Math.max(1, H);
        var dienTich = (bw * bh) / Math.max(1, W * H);
        return ar > 1.45 && yTam > 0.52 && dienTich < 0.55 && (bw / Math.max(1, W)) < 0.55;
    }
    window.__spNga = dauHieuNga;            // de kiem thu luat qua console
    function watchTick() {
        if (busy || !session) return;
        if (!drawSource()) return;
        busy = true;
        var fire = fireScan();
        detect().then(function (boxes) {
            var ctx = els.canvas.getContext('2d');
            var w = els.canvas.width;
            ctx.lineWidth = Math.max(2.5, w / 320);
            ctx.font = '600 ' + Math.max(13, Math.round(w / 55)) + 'px "Be Vietnam Pro", sans-serif';

            var canhBaos = [];

            // LUA: can CUM >=2 o chay ke nhau + nhap nhay giua cac khung
            if (fire.rect && fire.flicker > 0.004) {
                canhBaos.push({ loai: 'Lua', muc: 'Nguy hiểm', mota: 'Phát hiện cụm lửa/nhiệt (' + fire.cum + ' vùng cháy liền kề, có nhấp nháy)' });
            }

            /* PASS 1 — danh gia theo KHUNG HINH + ghep doi tuong voi khung truoc:
               · NGA: chi can MOT nguoi co dau hieu nga la dem tang; nguoi khac
                 dang DUNG trong cung khung KHONG duoc triet tieu bo dem (ban cu
                 ++/-- theo tung nguoi -> co dong nghiep dung canh la khong bao
                 gio bao duoc). Kep tran 6 de het nga thi ha nhanh, khong "nho dai".
               · VAT TINH: bo dem di theo TUNG VAT (ghep IoU khung truoc), khong
                 theo lop — 2 xe cung lop, 1 dung 1 chay khong con reset nhau. */
            var boxNga = null, moving = 0;
            boxes.forEach(function (b) {
                if (!WATCH_CLS[b.cls]) return;
                if (b.cls === 0 && !boxNga && dauHieuNga(b, els.canvas.width, els.canvas.height)) boxNga = b;
                var same = trackPrev.filter(function (p) { return p.cls === b.cls && iou(p, b) > 0.85; })[0];
                b.still = same ? (same.still || 0) + 1 : 0;
                if (!same) moving++;
            });
            if (boxNga) ngaDem = Math.min(6, ngaDem + 1);
            else if (ngaDem > 0) ngaDem--;

            boxes.forEach(function (b) {
                if (!WATCH_CLS[b.cls]) return;
                var mau = '#22d3ee';
                var nhan = WATCH_CLS[b.cls];

                // NGUOI NGA: 4 dieu kien + xac nhan nhieu luot (xem dauHieuNga)
                if (b === boxNga) {
                    if (ngaDem >= 3) {
                        mau = '#ef4444'; nhan = 'NGƯỜI NGÃ!';
                        canhBaos.push({ loai: 'NguoiNga', muc: 'Nguy hiểm', mota: 'Người nằm/ngã bất động trong khung hình (xác nhận ' + ngaDem + ' lượt quét)' });
                    } else {
                        mau = '#f59e0b'; nhan = 'Nghi ngã · xác nhận ' + ngaDem + '/3';
                    }
                }

                // VAT TINH: bo dem theo tung vat (b.still tu PASS 1)
                if (b.still >= 6 && moving > 0 && b.cls !== 0) {
                    mau = '#f59e0b'; nhan += ' · ĐỨNG IM';
                    canhBaos.push({ loai: 'VatTinh', muc: 'Chú ý', mota: WATCH_CLS[b.cls] + ' đứng im bất thường giữa khu vực lưu thông' });
                    b.still = 0;
                }

                ctx.strokeStyle = mau;
                ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
                ctx.fillStyle = mau;
                var tw = ctx.measureText(nhan).width + 10;
                ctx.fillRect(b.x1 - 1, b.y1 - 22, tw, 20);
                ctx.fillStyle = '#08131f';
                ctx.fillText(nhan, b.x1 + 4, b.y1 - 6);
            });
            trackPrev = boxes;

            // Ve khung DO khoanh dung vung lua da dinh vi
            if (fire.rect) {
                ctx.strokeStyle = '#ef4444';
                ctx.lineWidth = Math.max(4, els.canvas.width / 200);
                ctx.strokeRect(fire.rect.x, fire.rect.y, fire.rect.w, fire.rect.h);
                ctx.fillStyle = 'rgba(239,68,68,.85)';
                ctx.fillRect(fire.rect.x, Math.max(0, fire.rect.y - 26), 220, 24);
                ctx.fillStyle = '#fff';
                ctx.fillText('🔥 VÙNG LỬA/NHIỆT', fire.rect.x + 6, Math.max(16, fire.rect.y - 8));
            }

            canhBaos.forEach(function (cb) { baoAnNinh(cb.loai, cb.muc, cb.mota); });
            els.watchMeta.textContent = boxes.length + ' đối tượng · lửa ' + (fire.ratio * 100).toFixed(1) + '% · ' + new Date().toLocaleTimeString('vi-VN');
        }).catch(function (e) { console.warn(e); }).then(function () { busy = false; });
    }
    function watchStart() {
        if (watchTimer) return;
        watchTimer = setInterval(watchTick, 1300);
        els.btnWatch.innerHTML = '<i class="bi bi-pause-circle me-1"></i>Dừng giám sát';
        els.btnWatch.classList.add('btn-danger');
        themNhatKy('👁️ BẬT giám sát an ninh AI (lửa · người ngã · vật thể đứng im)');
    }
    function watchStop() {
        if (watchTimer) { clearInterval(watchTimer); watchTimer = null; }
        if (els.btnWatch) {
            els.btnWatch.innerHTML = '<i class="bi bi-eye me-1"></i>Bật giám sát an ninh';
            els.btnWatch.classList.remove('btn-danger');
        }
    }

    /* ================== KHOI DONG ================== */
    document.addEventListener('DOMContentLoaded', function () {
        els = {
            status: $('sgStatus'), canvas: $('sgCanvas'), video: $('sgVideo'), work: $('sgWork'),
            btnCam: $('btnCam'), btnStep: $('btnStep'), stepBadge: $('sgStepBadge'), ocrKq: $('sgOcrKq'),
            btnWatch: $('btnWatch'), watchMeta: $('sgWatchMeta'), viTri: $('sgViTri'),
            autoSiren: $('sgAutoSiren'), log: $('sgLog'), lastImg: null
        };
        if (!els.status) return;                    // khong phai trang Monitor

        $('fileInput').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            var rd = new FileReader();
            rd.onload = function () {
                var img = new Image();
                img.onload = function () { els.lastImg = img; drawSource(); status('🖼️ Đã nạp ảnh — chọn chức năng bên dưới.'); };
                img.src = rd.result;
            };
            rd.readAsDataURL(f);
        });
        document.querySelectorAll('.sg-sample').forEach(function (b) {
            b.addEventListener('click', function () {
                var img = new Image();
                img.onload = function () { els.lastImg = img; drawSource(); status('🖼️ Đã nạp ảnh mẫu.'); };
                img.src = b.getAttribute('data-src');
            });
        });
        els.btnCam.addEventListener('click', function () { stream ? stopCam() : startCam(); });
        els.btnStep.addEventListener('click', quet);
        els.btnWatch.addEventListener('click', function () { watchTimer ? watchStop() : watchStart(); });
        $('btnSiren').addEventListener('click', function () {
            if (sirenNodes) { sirenStop(); this.innerHTML = '<i class="bi bi-megaphone me-1"></i>Thử còi báo động'; }
            else { sirenStart(3); }
        });
        $('btnChime').addEventListener('click', chime);

        init();
    });
})();
