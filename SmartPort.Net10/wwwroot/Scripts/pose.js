/* ============================================================
   SAFEPOSE AI — Deep Learning #4: YOLO26-Pose (uoc luong tu the)
   Nhan dien 17 diem khop co the nguoi (COCO keypoints), ve khung
   xuong (skeleton) va PHAN TICH AN TOAN LAO DONG:
   - Phat hien nguoi NGA / cui thap bat thuong
   - Dem so nguoi trong khu vuc quan sat
   Chay ngay tren trinh duyet bang ONNX Runtime Web.
   ============================================================ */
(function () {
    var MODEL_URL = (window.APP_ROOT || '/') + 'Content/models/yolo26n-pose.onnx';   // LOCAL
    var ORT_CDN = (window.APP_ROOT || '/') + 'Scripts/vendor/ort/';   // LOCAL — khong can CDN
    var INPUT = 640;

    // 17 khop COCO + ten tieng Viet
    var KP_NAMES = ['Mũi', 'Mắt trái', 'Mắt phải', 'Tai trái', 'Tai phải',
        'Vai trái', 'Vai phải', 'Khuỷu trái', 'Khuỷu phải', 'Cổ tay trái', 'Cổ tay phải',
        'Hông trái', 'Hông phải', 'Gối trái', 'Gối phải', 'Cổ chân trái', 'Cổ chân phải'];

    // Cac canh khung xuong (noi cap khop)
    var SKELETON = [
        [5, 7], [7, 9], [6, 8], [8, 10],       // tay
        [5, 6], [5, 11], [6, 12], [11, 12],    // than
        [11, 13], [13, 15], [12, 14], [14, 16],// chan
        [0, 5], [0, 6]                          // dau -> vai
    ];

    var session = null, busy = false, stream = null, camTimer = null;
    var els = {};
    function $(id) { return document.getElementById(id); }

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

    /* ---------------- Khoi tao ---------------- */
    function initEngine() {
        status('Đang tải thư viện ONNX Runtime...', true);
        loadScript(ORT_CDN + 'ort.min.js')
            .then(function () {
                ort.env.wasm.wasmPaths = ORT_CDN;
                status('Đang tải mô hình <strong>YOLO26n-Pose</strong> (~11MB, lần đầu hơi lâu)...', true);
                return ort.InferenceSession.create(MODEL_URL, { executionProviders: ['wasm'] });
            })
            .then(function (s) {
                session = s;
                els.engineBadge.innerHTML = '<i class="bi bi-person-arms-up me-1"></i>YOLO26n-Pose · ONNX Runtime';
                els.engineBadge.className = 'badge-st st-ok';
                status('✅ Mô hình pose sẵn sàng! Tải ảnh có người hoặc bật webcam để phân tích tư thế.');
                ['fileInput', 'btnCam'].forEach(function (id) { if ($(id)) $(id).disabled = false; });
                document.querySelectorAll('.ps-sample').forEach(function (b) { b.disabled = false; });
            })
            .catch(function (e) {
                console.error(e);
                status('❌ Không tải được mô hình pose (kiểm tra Internet rồi tải lại trang).');
            });
    }

    /* ---------------- Tien xu ly ---------------- */
    function preprocess(source, w, h) {
        var c = els.work;
        c.width = INPUT; c.height = INPUT;
        var ctx = c.getContext('2d');
        var scale = Math.min(INPUT / w, INPUT / h);
        var nw = Math.round(w * scale), nh = Math.round(h * scale);
        var padX = Math.floor((INPUT - nw) / 2), padY = Math.floor((INPUT - nh) / 2);
        ctx.fillStyle = '#727272';
        ctx.fillRect(0, 0, INPUT, INPUT);
        ctx.drawImage(source, 0, 0, w, h, padX, padY, nw, nh);
        var img = ctx.getImageData(0, 0, INPUT, INPUT).data;
        var area = INPUT * INPUT;
        var t = new Float32Array(3 * area);
        for (var i = 0; i < area; i++) {
            t[i] = img[i * 4] / 255;
            t[area + i] = img[i * 4 + 1] / 255;
            t[2 * area + i] = img[i * 4 + 2] / 255;
        }
        return { tensor: new ort.Tensor('float32', t, [1, 3, INPUT, INPUT]), scale: scale, padX: padX, padY: padY };
    }

    /* ---------------- Phan tich dau ra pose ----------------
       End-to-end: [1, N, D] voi D = 4(box) + 1(score) [+1(class)] + 17*3(kpts)
       => D = 56 hoac 57. Tu phat hien offset keypoint theo D.            */
    function parsePose(output, meta, conf) {
        var dims = output.dims, d = output.data;
        var people = [];
        if (dims.length !== 3) return people;
        var N = dims[1], D = dims[2];
        if (D < 56) return people;
        var kpOff = D - 51;             // 56 -> 5, 57 -> 6

        for (var i = 0; i < N; i++) {
            var o = i * D;
            var sc = d[o + 4];
            if (sc < conf) continue;
            var kps = [];
            for (var k = 0; k < 17; k++) {
                var kx = d[o + kpOff + k * 3];
                var ky = d[o + kpOff + k * 3 + 1];
                var kv = d[o + kpOff + k * 3 + 2];
                kps.push({
                    x: (kx - meta.padX) / meta.scale,
                    y: (ky - meta.padY) / meta.scale,
                    v: kv
                });
            }
            people.push({
                x: (d[o] - meta.padX) / meta.scale,
                y: (d[o + 1] - meta.padY) / meta.scale,
                w: (d[o + 2] - d[o]) / meta.scale,
                h: (d[o + 3] - d[o + 1]) / meta.scale,
                score: sc,
                kps: kps
            });
        }
        return people;
    }

    /* ---------------- Phan tich an toan tu the ---------------- */
    function danhGiaAnToan(p) {
        var kq = { trangThai: 'An toàn', mau: '#22c55e', lyDo: [] };
        var mui = p.kps[0], vaiT = p.kps[5], vaiP = p.kps[6],
            hongT = p.kps[11], hongP = p.kps[12],
            goiT = p.kps[13], goiP = p.kps[14];

        function tb(a, b) {
            if (a.v > .3 && b.v > .3) return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, ok: true };
            if (a.v > .3) return { x: a.x, y: a.y, ok: true };
            if (b.v > .3) return { x: b.x, y: b.y, ok: true };
            return { ok: false };
        }
        var vai = tb(vaiT, vaiP), hong = tb(hongT, hongP), goi = tb(goiT, goiP);

        // Nam ngang: khung bao rong hon cao ro ret
        if (p.w > p.h * 1.35) {
            kq.trangThai = 'NGUY HIỂM — nghi ngã';
            kq.mau = '#dc2626';
            kq.lyDo.push('Tư thế nằm ngang (khung rộng ' + Math.round(p.w / p.h * 100) / 100 + ' lần chiều cao)');
            return kq;
        }
        // Dau thap hon hong => nga / treo nguoc
        if (mui.v > .3 && hong.ok && mui.y > hong.y + p.h * 0.08) {
            kq.trangThai = 'NGUY HIỂM — đầu thấp hơn hông';
            kq.mau = '#dc2626';
            kq.lyDo.push('Vị trí đầu thấp hơn hông — nghi ngã hoặc tư thế nguy hiểm');
            return kq;
        }
        // Cui sau: vai gan hong
        if (vai.ok && hong.ok && goi.ok) {
            var thanTren = Math.abs(hong.y - vai.y);
            var thanDuoi = Math.abs(goi.y - hong.y);
            if (thanDuoi > 4 && thanTren < thanDuoi * 0.55) {
                kq.trangThai = 'Chú ý — cúi thấp';
                kq.mau = '#e5691d';
                kq.lyDo.push('Thân trên gập sâu — tư thế nâng vật nặng sai có thể gây chấn thương lưng');
                return kq;
            }
        }
        kq.lyDo.push('Tư thế đứng/di chuyển bình thường');
        return kq;
    }

    /* ---------------- Ve ket qua ---------------- */
    function render(source, w, h, people, ms) {
        var c = els.canvas, ctx = c.getContext('2d');
        c.width = w; c.height = h;
        ctx.drawImage(source, 0, 0, w, h);
        var lw = Math.max(2, w / 400);

        var soNguy = 0, soChuY = 0;
        people.forEach(function (p) {
            var at = danhGiaAnToan(p);
            if (at.mau === '#dc2626') soNguy++;
            else if (at.mau === '#e5691d') soChuY++;

            // khung bao
            ctx.strokeStyle = at.mau;
            ctx.lineWidth = lw * 1.4;
            ctx.strokeRect(p.x, p.y, p.w, p.h);

            // khung xuong
            ctx.lineWidth = lw;
            SKELETON.forEach(function (e) {
                var a = p.kps[e[0]], b = p.kps[e[1]];
                if (a.v > .3 && b.v > .3) {
                    ctx.strokeStyle = at.mau;
                    ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
                }
            });
            // cac khop
            p.kps.forEach(function (k) {
                if (k.v > .3) {
                    ctx.fillStyle = '#fff';
                    ctx.beginPath(); ctx.arc(k.x, k.y, lw * 1.7, 0, Math.PI * 2); ctx.fill();
                    ctx.fillStyle = at.mau;
                    ctx.beginPath(); ctx.arc(k.x, k.y, lw * 1.1, 0, Math.PI * 2); ctx.fill();
                }
            });
            // nhan
            var fs = Math.max(13, Math.round(w / 55));
            ctx.font = '600 ' + fs + 'px "Be Vietnam Pro", sans-serif';
            var txt = at.trangThai + ' ' + Math.round(p.score * 100) + '%';
            var tw = ctx.measureText(txt).width + 10;
            var ty = p.y > fs + 8 ? p.y - fs - 6 : p.y + 2;
            ctx.fillStyle = at.mau;
            ctx.fillRect(p.x - 1, ty, tw, fs + 6);
            ctx.fillStyle = '#fff';
            ctx.fillText(txt, p.x + 4, ty + fs);
        });

        els.meta.textContent = people.length + ' người · ' + ms + 'ms/khung hình';

        // Bang tong hop an toan
        var html = '';
        if (!people.length) {
            html = '<div class="ai-note">Không phát hiện người trong khung hình (thử giảm ngưỡng).</div>';
        } else {
            if (soNguy > 0) html += '<div class="ai-note" style="border-color:#dc2626;"><strong style="color:#dc2626;">🚨 ' + soNguy + ' người ở tư thế NGUY HIỂM</strong> — cử nhân viên kiểm tra hiện trường NGAY!</div>';
            if (soChuY > 0) html += '<div class="ai-note" style="border-color:#e5691d;">⚠️ ' + soChuY + ' người tư thế cần chú ý (cúi sâu/nâng vật sai cách).</div>';
            if (!soNguy && !soChuY) html += '<div class="ai-note">✅ Tất cả ' + people.length + ' người có tư thế an toàn.</div>';
            html += '<div class="ai-note">👷 Tổng ' + people.length + ' người trong khu vực quan sát.</div>';
        }
        els.insight.innerHTML = html;
    }

    /* ---------------- Nhan dien ---------------- */
    function detect(source, w, h) {
        var conf = parseFloat(els.conf.value);
        var t0 = performance.now();
        var meta = preprocess(source, w, h);
        var feeds = {};
        feeds[session.inputNames[0]] = meta.tensor;
        return session.run(feeds).then(function (out) {
            var people = parsePose(out[session.outputNames[0]], meta, conf);
            return { people: people, ms: Math.round(performance.now() - t0) };
        });
    }

    function analyzeImage(img) {
        if (busy || !session) return;
        busy = true;
        status('🧠 AI đang phân tích tư thế...', true);
        detect(img, img.naturalWidth || img.width, img.naturalHeight || img.height)
            .then(function (r) {
                render(img, img.naturalWidth || img.width, img.naturalHeight || img.height, r.people, r.ms);
                status('Xong! Chọn ảnh khác hoặc bật webcam để theo dõi liên tục.');
            })
            .catch(function (e) { console.error(e); status('❌ Lỗi phân tích: ' + e.message); })
            .then(function () { busy = false; });
    }

    function loadUrl(url) {
        var img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = function () { analyzeImage(img); };
        img.onerror = function () { status('❌ Không tải được ảnh (CORS/mạng).'); };
        status('Đang tải ảnh...', true);
        img.src = url;
    }

    /* ---------------- Webcam ---------------- */
    function startCam() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.\nHãy truy cập site qua https:// rồi thử lại nhé.');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { width: 960 } })
            .then(function (s) {
                stream = s;
                els.video.srcObject = s;
                els.video.play();
                els.btnCam.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Dừng webcam';
                camLoop();
            })
            .catch(function () { status('❌ Không truy cập được webcam (cần cho phép quyền camera).'); });
    }
    function camLoop() {
        if (!stream) return;
        if (els.video.readyState >= 2 && !busy) {
            busy = true;
            var w = els.video.videoWidth, h = els.video.videoHeight;
            detect(els.video, w, h).then(function (r) {
                render(els.video, w, h, r.people, r.ms);
                busy = false;
                camTimer = setTimeout(camLoop, 140);
            }).catch(function () { busy = false; camTimer = setTimeout(camLoop, 500); });
        } else camTimer = setTimeout(camLoop, 200);
    }
    function stopCam() {
        if (camTimer) clearTimeout(camTimer);
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        if (els.btnCam) els.btnCam.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật webcam pose';
    }

    /* ---------------- Khoi dong ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        els = { status: $('psStatus'), engineBadge: $('psEngine'), canvas: $('psCanvas'), work: $('psWork'),
                meta: $('psMeta'), insight: $('psInsight'), conf: $('psConf'),
                video: $('psVideo'), btnCam: $('btnCam') };
        if (!els.status) return;

        $('fileInput').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            stopCam();
            var rd = new FileReader();
            rd.onload = function () {
                var img = new Image();
                img.onload = function () { analyzeImage(img); };
                img.src = rd.result;
            };
            rd.readAsDataURL(f);
            this.value = '';               // chon lai CUNG 1 file van phan tich
        });
        document.querySelectorAll('.ps-sample').forEach(function (b) {
            b.addEventListener('click', function () { stopCam(); loadUrl(b.getAttribute('data-src')); });
        });
        els.btnCam.addEventListener('click', function () { stream ? stopCam() : startCam(); });
        els.conf.addEventListener('input', function () {
            $('psConfVal').textContent = Math.round(els.conf.value * 100) + '%';
        });
        initEngine();
    });
})();
