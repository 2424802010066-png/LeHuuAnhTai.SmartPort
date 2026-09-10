/* ============================================================
   IN-CABIN & CARGO AI — Giam sat TAI XE va KHOANG HANG (v3)
   ------------------------------------------------------------
   CAMERA 1 (DMS): MediaPipe FaceLandmarker (478 diem moc + blend-
   shapes, WASM chay ngay trinh duyet):
     - NGU GAT      : eyeBlink 2 mat > nguong lien tuc >= 1.4s
     - NGAP         : jawOpen > nguong; >=3 lan/60s = met moi
     - MAT TAP TRUNG: quay dau BAT KY huong nao (trai/phai/len/
                      xuong/nghieng) lien tuc >= 5s -> bao dong
     - DIEN THOAI   : YOLO26 lop "cell phone" xuat hien canh mat
     - TRANG THAI LA: mang no-ron TU MA HOA (autoencoder 10-7-3-
                      7-10, thuan JS) hoc ONLINE tu the lai binh
                      thuong cua CHINH tai xe; sai so tai tao cao
                      keo dai = trang thai chua tung thay -> bao.
                      Nut Dung / Bao nham dieu chinh nguong kieu
                      hoc tang cuong tu phan hoi; nho qua phien
                      bang localStorage.
   CAMERA 2 (Cargo): YOLO26 person (xam nhap khi xe dung) + cum
   mau carton -> goc truc chinh PCA -> HANG NGHIENG > 25 do.
   Su co -> coi + doc canh bao + bao Phong an ninh + luu Kho media.
   ============================================================ */
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    var ROOT = window.APP_ROOT || '/';

    var ORT_CDN = ROOT + 'Scripts/vendor/ort/';   // LOCAL — khong can CDN
    var YOLO_URL = ROOT + 'Content/models/yolo26n.onnx';   // LOCAL
    var MP_CDN = ROOT + 'Scripts/vendor/mediapipe';   // LOCAL
    var MP_MODEL = ROOT + 'Content/models/face_landmarker.task';   // LOCAL
    var INPUT = 640;

    var yolo = null, landmarker = null;
    var dmsStream = null, cgStream = null;
    var lastAlert = {};                      // rate-limit theo loai canh bao
    window.__dv = { faceReady: false, yoloReady: false, face: false, mat: 0, yaw: 0, ngap: 0, trangThai: '', suKien: [], cargo: { goc: 0, nghieng: false, nguoi: false, thung: 0 } };

    /* ---------------- Tien ich chung ---------------- */
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
    function canAlert(key, ms) {
        var now = Date.now();
        if (lastAlert[key] && now - lastAlert[key] < ms) return false;
        lastAlert[key] = now;
        return true;
    }
    function siren(seconds) {
        try {
            var a = new (window.AudioContext || window.webkitAudioContext)();
            var o = a.createOscillator(), g = a.createGain(), lfo = a.createOscillator(), lg = a.createGain();
            o.type = 'sawtooth'; o.frequency.value = 660;
            lfo.type = 'sine'; lfo.frequency.value = 3.2; lg.gain.value = 210;
            lfo.connect(lg); lg.connect(o.frequency);
            g.gain.value = 0.2;
            o.connect(g); g.connect(a.destination);
            o.start(); lfo.start();
            setTimeout(function () { try { o.stop(); lfo.stop(); a.close(); } catch (e) { } }, (seconds || 2.5) * 1000);
        } catch (e) { }
    }
    function beep() {
        try {
            var a = new (window.AudioContext || window.webkitAudioContext)();
            [0, 220, 440].forEach(function (t) {
                var o = a.createOscillator(), g = a.createGain();
                o.type = 'square'; o.frequency.value = 980; g.gain.value = 0.11;
                o.connect(g); g.connect(a.destination);
                o.start(a.currentTime + t / 1000); o.stop(a.currentTime + t / 1000 + 0.13);
            });
            setTimeout(function () { try { a.close(); } catch (e) { } }, 900);
        } catch (e) { }
    }
    function noi(text) {
        if (!('speechSynthesis' in window)) return;
        var u = new SpeechSynthesisUtterance(text);
        u.lang = 'vi-VN'; u.rate = 1.08;
        var vi = speechSynthesis.getVoices().filter(function (v) { return v.lang && v.lang.toLowerCase().indexOf('vi') === 0; })[0];
        if (vi) u.voice = vi;
        speechSynthesis.speak(u);
    }
    /** Bao Phong an ninh + luu anh bang chung vao Kho media (tai su dung API co san) */
    function baoSuCo(loai, mucDo, viTri, moTa, canvas) {
        try {
            var thumb = document.createElement('canvas');
            var sc = Math.min(1, 520 / Math.max(canvas.width, 1));
            thumb.width = Math.round(canvas.width * sc); thumb.height = Math.round(canvas.height * sc);
            thumb.getContext('2d').drawImage(canvas, 0, 0, thumb.width, thumb.height);
            var fd = new FormData();
            fd.append('loai', loai);
            fd.append('mucDo', mucDo);
            fd.append('viTri', viTri);
            fd.append('moTa', moTa);
            fd.append('anh', thumb.toDataURL('image/jpeg', 0.75));
            fetch(ROOT + 'Admin/Security/Report', { method: 'POST', body: fd, credentials: 'same-origin' }).catch(function () { });
            canvas.toBlob(function (blob) {
                if (!blob) return;
                var fm = new FormData();
                fm.append('anh', blob, 'dms.jpg');
                fetch(ROOT + 'Admin/Security/UploadMedia?loai=Anh&nguon=CameraAI&moTa=' + encodeURIComponent(moTa),
                    { method: 'POST', body: fm, credentials: 'same-origin' }).catch(function () { });
            }, 'image/jpeg', 0.85);
        } catch (e) { }
        window.__dv.suKien.push({ loai: loai, moTa: moTa, luc: new Date().toISOString() });
    }

    /* ---------------- Nap 2 engine AI ---------------- */
    function initYolo() {
        return loadScript(ORT_CDN + 'ort.min.js')
            .then(function () {
                ort.env.wasm.wasmPaths = ORT_CDN;
                return fetchModel(YOLO_URL);
            })
            .then(function (buf) { return ort.InferenceSession.create(buf, { executionProviders: ['wasm'] }); })
            .then(function (s) { yolo = s; window.__dv.yoloReady = true; });
    }
    function initFace() {
        return import(MP_CDN + '/vision_bundle.mjs').then(function (mod) {
            return mod.FilesetResolver.forVisionTasks(MP_CDN + '/wasm').then(function (fs) {
                var opts = {
                    baseOptions: { modelAssetPath: MP_MODEL, delegate: 'GPU' },
                    runningMode: 'VIDEO', numFaces: 1, outputFaceBlendshapes: true
                };
                return mod.FaceLandmarker.createFromOptions(fs, opts)
                    .catch(function () {
                        opts.baseOptions.delegate = 'CPU';
                        return mod.FaceLandmarker.createFromOptions(fs, opts);
                    });
            });
        }).then(function (lm) { landmarker = lm; window.__dv.faceReady = true; });
    }

    /** Chay YOLO tren 1 canvas, tra cac box cua lop can tim */
    function yoloDetect(cv, wantCls) {
        if (!yolo) return Promise.resolve([]);
        var w = cv.width, h = cv.height;
        var work = $('dvWork'), ctx = work.getContext('2d');
        work.width = INPUT; work.height = INPUT;
        var sc = Math.min(INPUT / w, INPUT / h);
        var dx = (INPUT - Math.round(w * sc)) / 2, dy = (INPUT - Math.round(h * sc)) / 2;
        ctx.fillStyle = '#727272'; ctx.fillRect(0, 0, INPUT, INPUT);
        ctx.drawImage(cv, dx, dy, Math.round(w * sc), Math.round(h * sc));
        var d = ctx.getImageData(0, 0, INPUT, INPUT).data;
        var arr = new Float32Array(3 * INPUT * INPUT);
        for (var i = 0, p = 0; i < d.length; i += 4, p++) {
            arr[p] = d[i] / 255;
            arr[p + INPUT * INPUT] = d[i + 1] / 255;
            arr[p + 2 * INPUT * INPUT] = d[i + 2] / 255;
        }
        var feeds = {}; feeds[yolo.inputNames[0]] = new ort.Tensor('float32', arr, [1, 3, INPUT, INPUT]);
        return yolo.run(feeds).then(function (out) {
            var o = out[yolo.outputNames[0]], boxes = [];
            for (var k = 0; k < o.dims[1]; k++) {
                var b = k * 6, scr = o.data[b + 4], cls = Math.round(o.data[b + 5]);
                if (scr < 0.35 || wantCls.indexOf(cls) < 0) continue;
                boxes.push({
                    x1: Math.max(0, (o.data[b] - dx) / sc), y1: Math.max(0, (o.data[b + 1] - dy) / sc),
                    x2: Math.min(w, (o.data[b + 2] - dx) / sc), y2: Math.min(h, (o.data[b + 3] - dy) / sc),
                    cls: cls, score: scr
                });
            }
            return boxes;
        });
    }

    /* ============================================================
       GIAM SAT QUAY DAU DA HUONG — mat tap trung khi roi mat khoi
       duong >= 5 giay (trai / phai / ngang len / cui xuong / nghieng)
       ============================================================ */
    function taoGiamSatQuayDau(cfg) {
        cfg = cfg || {};
        var NG_YAW = cfg.yaw || 22;          // do — quay trai/phai
        var NG_LEN = cfg.len || -16;         // pitch am = ngang len
        var NG_XUONG = cfg.xuong || 20;      // pitch duong = cui xuong
        var NG_ROLL = cfg.roll || 20;        // nghieng dau
        var GIU_MS = cfg.giuMs || 5000;      // giu lien tuc bao lau thi bao dong
        var HO_MS = cfg.hoMs || 450;         // cho phep mat dau hieu thoang qua
        var st = { tu: 0, cuoi: 0, huong: '' };
        function huongLech(goc) {
            if (goc.yaw > NG_YAW) return '→';
            if (goc.yaw < -NG_YAW) return '←';
            if (goc.pitch < NG_LEN) return '↑';
            if (goc.pitch > NG_XUONG) return '↓';
            if (Math.abs(goc.roll) > NG_ROLL) return '↺';
            return '';
        }
        return {
            step: function (goc, now) {
                var h = huongLech(goc);
                if (h) {
                    if (!st.tu || now - st.cuoi > HO_MS) st.tu = now;
                    st.cuoi = now; st.huong = h;
                } else if (st.tu && now - st.cuoi > HO_MS) {
                    st.tu = 0; st.cuoi = 0; st.huong = '';
                }
                var giay = st.tu ? (st.cuoi - st.tu) / 1000 : 0;
                return {
                    lech: !!st.tu, huong: st.huong, giay: giay,
                    baoDong: st.tu > 0 && giay >= GIU_MS / 1000
                };
            },
            /** Mat dau khuon mat khi DANG quay dau (quay qua gat, model khong bat
                duoc mat nua) -> van tinh tiep thoi gian roi mat khoi duong */
            matMat: function (now) {
                if (!st.tu) return { lech: false, huong: '', giay: 0, baoDong: false };
                st.cuoi = now;
                var giay = (st.cuoi - st.tu) / 1000;
                return { lech: true, huong: st.huong, giay: giay, baoDong: giay >= GIU_MS / 1000 };
            },
            reset: function () { st.tu = 0; st.cuoi = 0; st.huong = ''; }
        };
    }
    window.SPGaze = taoGiamSatQuayDau;       // cho AI Lab / kiem thu

    /* ============================================================
       AI TU HOC TRANG THAI BAT THUONG (khong can gan nhan truoc)
       Mang no-ron TU MA HOA 10-7-3-7-10 (tanh, SGD lan truyen nguoc
       thuan JS). Pha 1 "hoc nen": hoc ~150 khung tu the lai binh
       thuong -> nguong = mean + k*std cua sai so tai tao. Pha 2
       "giam sat": khung nao tai tao sai lech cao lien tuc = trang
       thai CHUA TUNG THAY -> bao dong. Van hoc cham cac khung binh
       thuong de thich nghi anh sang/tu the. Phan hoi cua nguoi van
       hanh (Dung / Bao nham) cong-tru nguong k nhu tin hieu thuong/
       phat cua hoc tang cuong; trong so luu localStorage de AI
       "nho" qua cac phien.
       ============================================================ */
    function taoAno(cfg) {
        cfg = cfg || {};
        var KICH = cfg.kich || [10, 7, 3, 7, 10];
        var HOC_NEN = cfg.hocNen || 150;
        var GIU_MS = cfg.giuMs || 2500;
        var kho = cfg.kho === undefined ? 'sp_dms_ano' : cfg.kho;
        var rnd = (function (seed) {
            var s = seed || 88;
            return function () { s = (s * 16807) % 2147483647; return s / 2147483647 - 0.5; };
        })(cfg.seed);
        var W = [], B = [];
        for (var l = 0; l < KICH.length - 1; l++) {
            var w = [], b = [];
            for (var j = 0; j < KICH[l + 1]; j++) {
                var row = [];
                for (var i = 0; i < KICH[l]; i++) row.push(rnd() * 0.6);
                w.push(row); b.push(0);
            }
            W.push(w); B.push(b);
        }
        var STAT_TU = Math.max(20, HOC_NEN - 50);   // chi thong ke 50 khung CUOI (mang da hoi tu)
        var st = { n: 0, nStat: 0, mean: 0, m2: 0, sd: 0, k: 3.5, ema: 0, batTu: 0, pha: 'hoc', dem: [], demBao: null };
        try {
            if (kho && window.localStorage) {
                var luu = JSON.parse(localStorage.getItem(kho) || 'null');
                if (luu && luu.kich && luu.kich.join() === KICH.join()) {
                    W = luu.W; B = luu.B;
                    // k luu theo TRINH DUYET, khong theo tai xe -> kep lai de khong mu/khong nhay qua
                    if (luu.k) st.k = Math.min(6, Math.max(2.6, luu.k));
                }
            }
        } catch (e) { }
        function veHocNen() {                       // hoc lai nen: giu trong so, lam moi thong ke
            st.pha = 'hoc'; st.n = 0; st.nStat = 0; st.mean = 0; st.m2 = 0; st.sd = 0;
            st.ema = 0; st.batTu = 0; st.demBao = null;
        }
        function luuKho() {
            try {
                if (kho && window.localStorage)
                    localStorage.setItem(kho, JSON.stringify({ kich: KICH, W: W, B: B, k: st.k }));
            } catch (e) { }
        }
        function lan(x) {
            var A = [x];
            for (var l2 = 0; l2 < W.length; l2++) {
                var inp = A[l2], out = [];
                for (var j2 = 0; j2 < W[l2].length; j2++) {
                    var s = B[l2][j2];
                    for (var i2 = 0; i2 < inp.length; i2++) s += W[l2][j2][i2] * inp[i2];
                    out.push(l2 < W.length - 1 ? Math.tanh(s) : s);
                }
                A.push(out);
            }
            return A;
        }
        function saiSo(A, x) {
            var y = A[A.length - 1], e = 0;
            for (var i3 = 0; i3 < x.length; i3++) { var d = y[i3] - x[i3]; e += d * d; }
            return e / x.length;
        }
        function hoc(x, lr) {
            var A = lan(x), y = A[A.length - 1];
            var eTruoc = saiSo(A, x);
            var delta = new Array(y.length);
            for (var i4 = 0; i4 < y.length; i4++) delta[i4] = 2 * (y[i4] - x[i4]) / x.length;
            for (var l3 = W.length - 1; l3 >= 0; l3--) {
                var inp2 = A[l3];
                var dTruoc = new Array(inp2.length);
                for (var z = 0; z < inp2.length; z++) dTruoc[z] = 0;
                for (var j3 = 0; j3 < W[l3].length; j3++) {
                    var dj = delta[j3];
                    for (var i5 = 0; i5 < inp2.length; i5++) {
                        dTruoc[i5] += W[l3][j3][i5] * dj;
                        W[l3][j3][i5] -= lr * dj * inp2[i5];
                    }
                    B[l3][j3] -= lr * dj;
                }
                if (l3 > 0) {
                    for (var i6 = 0; i6 < inp2.length; i6++) dTruoc[i6] *= (1 - inp2[i6] * inp2[i6]);
                    delta = dTruoc;
                }
            }
            return eTruoc;
        }
        return {
            /** x: vec-to dac trung; now: ms; ruleBat: luat cung (ngu gat/quay dau/dien thoai)
                dang bao -> KHONG hoc khung nay lam "binh thuong" */
            step: function (x, now, ruleBat) {
                var e;
                if (st.pha === 'hoc') {
                    // Khung dang bi luat cung danh dau -> bo qua, khong hoc lam nen
                    if (ruleBat) return { pha: 'hoc', tienDo: Math.round(st.n * 100 / HOC_NEN), e: 0, nguong: 0, bat: false, baoDong: false, giay: 0 };
                    st.dem.push(x);
                    if (st.dem.length > 60) st.dem.shift();
                    e = hoc(x, 0.02); hoc(x, 0.02);          // 2 luot / khung cho hoi tu nhanh
                    st.n++;
                    // Chi gom thong ke o 50 khung CUOI — sai so dau pha la qua trinh
                    // hoi tu (giam dan), khong phai phan bo "binh thuong" that
                    if (st.n > STAT_TU) {
                        st.nStat++;
                        var d = e - st.mean;
                        st.mean += d / st.nStat;
                        st.m2 += d * (e - st.mean);
                    }
                    if (st.n >= HOC_NEN) {
                        st.sd = Math.sqrt(st.m2 / Math.max(1, st.nStat - 1));
                        st.ema = st.mean;
                        st.pha = 'giamsat';
                        luuKho();
                    }
                    return { pha: 'hoc', tienDo: Math.round(st.n * 100 / HOC_NEN), e: e, nguong: 0, bat: false, baoDong: false, giay: 0 };
                }
                st.dem.push(x);
                if (st.dem.length > 60) st.dem.shift();
                st.n++;
                e = saiSo(lan(x), x);
                st.ema = st.ema * 0.72 + e * 0.28;
                var sdEff = Math.max(st.sd, st.mean * 0.2 + 0.0008);
                var nguong = st.mean + st.k * sdEff;
                var bat = st.ema > nguong;
                if (bat) {
                    if (!st.batTu) st.batTu = now;
                    // Ket bao dong qua 60s (doi tai xe / doi anh sang?) -> tu hoc lai nen
                    if (now - st.batTu > 60000) {
                        veHocNen();
                        return { pha: 'hoc', tienDo: 0, tuHocLai: true, e: e, nguong: 0, bat: false, baoDong: false, giay: 0 };
                    }
                } else {
                    st.batTu = 0;
                    if (!ruleBat && e < st.mean + sdEff) {   // khung binh thuong -> hoc cham de thich nghi
                        hoc(x, 0.004);
                        st.mean = st.mean * 0.995 + e * 0.005;
                        st.sd = st.sd * 0.995 + Math.abs(e - st.mean) * 1.2533 * 0.005;
                    }
                }
                if (st.n % 200 === 0) luuKho();
                var baoDong = st.batTu > 0 && now - st.batTu >= GIU_MS;
                if (baoDong) st.demBao = st.dem.slice();     // chup vung gay bao de day lai dung cho
                return {
                    pha: 'giamsat', e: e, ema: st.ema, nguong: nguong, bat: bat,
                    giay: st.batTu ? (now - st.batTu) / 1000 : 0,
                    baoDong: baoDong
                };
            },
            /** Mat dau khuon mat / tat camera -> xoa moc bao dong dang dem do */
            ngat: function () { st.batTu = 0; },
            /** Bat dau phien camera moi -> hoc lai nen (giu trong so da co) */
            hocLai: veHocNen,
            /** Phan hoi nguoi van hanh: dung=true (canh bao chuan, tang nhay) /
                dung=false (bao nham -> nang nguong + hoc lai vung vua bao) */
            phanHoi: function (dung) {
                if (dung) {
                    st.k = Math.max(2.2, st.k - 0.3);
                } else {
                    st.k = Math.min(8, st.k + 0.7);
                    // Day lai dung VUNG DA GAY BAO (chup luc bao dong), khong phai
                    // buffer hien tai — nguoi van hanh co the bam nut tre ca chuc giay
                    var vung = st.demBao || st.dem;
                    for (var ep = 0; ep < 3; ep++)
                        for (var i7 = 0; i7 < vung.length; i7++) hoc(vung[i7], 0.01);
                    st.batTu = 0; st.ema = st.mean; st.demBao = null;
                }
                luuKho();
                return st.k;
            },
            trangThai: function () { return { pha: st.pha, k: st.k, mean: st.mean, sd: st.sd, ema: st.ema, n: st.n }; },
            quenKho: function () { try { if (kho) localStorage.removeItem(kho); } catch (e) { } }
        };
    }
    window.SPAno = taoAno;                   // cho AI Lab / kiem thu

    /* ============================================================
       CAMERA 1 — DMS (giam sat tai xe)
       ============================================================ */
    var dms = {
        matNhamTu: 0,
        ngapBatDau: 0, ngapMoc: [],          // moc thoi gian cac lan ngap
        phoneHits: 0, phoneCheckLuc: 0, phoneCo: false,
        prevYaw: 0, prevPitch: 0,
        loopOn: false, gen: 0
    };
    var gaze = taoGiamSatQuayDau();
    var ano = null;                          // khoi tao khi khung hinh live dau tien

    function gocTuLandmark(lm) {
        // 5 diem: mat trai ngoai 33, mat phai ngoai 263, mui 1, cam 152
        var mt = lm[33], mp = lm[263], mui = lm[1], cam = lm[152];
        var gx = (mt.x + mp.x) / 2, gy = (mt.y + mp.y) / 2;
        var kcMat = Math.hypot(mp.x - mt.x, mp.y - mt.y) + 1e-6;
        var roll = Math.atan2(mp.y - mt.y, mp.x - mt.x);
        // yaw: mui lech ngang so voi trung diem 2 mat (khu roll)
        var ex = mui.x - gx, ey = mui.y - gy;
        var ngang = ex * Math.cos(-roll) - ey * Math.sin(-roll);
        var yaw = Math.asin(Math.max(-1, Math.min(1, 2 * ngang / kcMat))) * 180 / Math.PI;
        // pitch: vi tri mui giua duong mat va cam (chinh dien ~0.42-0.55)
        var caoMat = Math.hypot(cam.x - gx, cam.y - gy) + 1e-6;
        var doc = (ey * Math.cos(-roll) + ex * Math.sin(-roll)) / caoMat;
        var pitch = (doc - 0.48) * 120;      // duong = cui dau xuong
        return { yaw: yaw, pitch: pitch, roll: roll * 180 / Math.PI };
    }

    function blendVal(cats, ten) {
        for (var i = 0; i < cats.length; i++) {
            if (cats[i].categoryName === ten) return cats[i].score;
        }
        return 0;
    }

    function tenHuong(h) {
        return h === '→' ? 'sang phải' : h === '←' ? 'sang trái'
             : h === '↑' ? 'lên trên' : h === '↓' ? 'xuống dưới'
             : h === '↺' ? 'nghiêng đầu' : 'hướng khác';
    }

    /** Vec-to 10 dac trung chuan hoa [-1..1] cho mang tu ma hoa */
    function dacTrung(goc, nham, mieng, lm) {
        var mt = lm[33], mp = lm[263];
        var cx = (mt.x + mp.x) / 2, cy = (mt.y + mp.y) / 2;
        var kc = Math.hypot(mp.x - mt.x, mp.y - mt.y);
        var clamp = function (v) { return Math.max(-1, Math.min(1, v)); };
        return [
            clamp(goc.yaw / 45), clamp(goc.pitch / 45), clamp(goc.roll / 45),
            clamp(nham * 2 - 1), clamp(mieng * 2 - 1),
            clamp((cx - 0.5) * 2), clamp((cy - 0.5) * 2), clamp(kc * 5 - 1),
            clamp((goc.yaw - dms.prevYaw) / 10), clamp((goc.pitch - dms.prevPitch) / 10)
        ];
    }

    function capNhatAnoUI(kq) {
        var tt = $('anoTT'), bar = $('anoBar');
        if (!tt || !bar) return;
        // Giu thong bao phan hoi (✔/✖) vai giay — khong de vong lap 9fps de mat
        if (capNhatAnoUI._giu && Date.now() < capNhatAnoUI._giu) return;
        if (kq.tuHocLai) capNhatAnoUI._laiTu = Date.now();
        if (kq.pha === 'hoc') {
            var laiTxt = (capNhatAnoUI._laiTu && Date.now() - capNhatAnoUI._laiTu < 30000)
                ? 'môi trường thay đổi — AI tự học lại nền... ' : 'đang học tư thế lái bình thường... ';
            tt.textContent = laiTxt + kq.tienDo + '%';
            tt.className = 'text-info';
            bar.style.width = kq.tienDo + '%';
            bar.style.background = '#38bdf8';
            return;
        }
        var pct = Math.min(100, Math.round(kq.ema / (kq.nguong * 1.6) * 100));
        bar.style.width = pct + '%';
        bar.style.background = kq.bat ? '#ef4444' : (pct > 45 ? '#f59e0b' : '#22c55e');
        if (kq.bat) {
            tt.textContent = 'BẤT THƯỜNG! độ lệch ' + kq.ema.toFixed(4) + ' > ngưỡng ' + kq.nguong.toFixed(4) + (kq.giay ? ' · ' + kq.giay.toFixed(1) + 's' : '');
            tt.className = 'text-danger fw-semibold';
        } else {
            tt.textContent = 'đang giám sát · độ lệch ' + kq.ema.toFixed(4) + ' / ngưỡng ' + kq.nguong.toFixed(4);
            tt.className = 'text-muted';
        }
    }

    function hienPhanHoi() {
        var fb = $('anoFb');
        if (!fb) return;
        fb.style.display = 'inline-block';
        clearTimeout(hienPhanHoi._t);
        hienPhanHoi._t = setTimeout(function () { fb.style.display = 'none'; }, 30000);
    }

    function dmsAlert(text, hienMs) {
        var box = $('dmsAlert');
        $('dmsAlertText').textContent = text;
        box.style.display = 'flex';
        clearTimeout(dmsAlert._t);
        dmsAlert._t = setTimeout(function () { box.style.display = 'none'; }, hienMs || 3500);
    }

    /** Xu ly 1 khung hinh DMS (goi tu vong lap webcam hoac anh mau) */
    function dmsXuLy(nguon, w, h, laAnhMau) {
        var cv = $('dmsCanvas'), ctx = cv.getContext('2d');
        cv.width = w; cv.height = h;
        ctx.drawImage(nguon, 0, 0, w, h);

        var res;
        try { res = landmarker.detectForVideo(nguon, performance.now()); }
        catch (e) { return; }

        var co = res && res.faceLandmarks && res.faceLandmarks.length > 0;
        window.__dv.face = co;
        var now = Date.now();
        if (!co) {
            $('hudMat').textContent = '—';
            $('hudQuay').textContent = '—';
            window.__dv.trangThai = 'khong thay mat';
            if (!laAnhMau) {
                // Xoa moc thoi gian cu — khong duoc de "nham mat 1 khung + mat dau
                // vai phut + nham mat 1 khung" bien thanh bao ngu gat oan
                dms.matNhamTu = 0; dms.ngapBatDau = 0;
                if (ano) ano.ngat();
                // Dang quay dau roi MAT DAU mat (quay qua gat) -> van tinh tiep 5s
                var gKA = gaze.matMat(now);
                if (gKA && gKA.lech) {
                    $('dmsKq').innerHTML = '<span class="text-warning">Mất dấu mặt khi đang quay đầu — ' + gKA.giay.toFixed(1) + 's / 5s</span>';
                    if (gKA.baoDong && canAlert('MatTapTrung', 25000)) {
                        siren(2.2);
                        noi('Cảnh báo. Không thấy mặt tài xế, có thể đang quay đầu quá lâu.');
                        dmsAlert('⚠ MẤT TẬP TRUNG — KHÔNG THẤY MẶT TÀI XẾ', 3000);
                        baoSuCo('MatTapTrung', 'Chú ý', 'Cabin xe — camera DMS',
                            'DMS: mất dấu khuôn mặt khi đang quay đầu, tổng ' + gKA.giay.toFixed(1) + 's rời mắt khỏi đường', cv);
                    }
                } else {
                    $('dmsKq').innerHTML = '<span class="text-muted">Chưa thấy khuôn mặt trong khung.</span>';
                }
            }
            return;
        }
        var lm = res.faceLandmarks[0];
        var cats = (res.faceBlendshapes && res.faceBlendshapes[0]) ? res.faceBlendshapes[0].categories : [];
        var nhamT = blendVal(cats, 'eyeBlinkLeft'), nhamP = blendVal(cats, 'eyeBlinkRight');
        var mieng = blendVal(cats, 'jawOpen');
        var nham = (nhamT + nhamP) / 2;
        var matMo = Math.round((1 - nham) * 100);
        var goc = gocTuLandmark(lm);

        // Ve diem moc mat + mieng (HUD gon)
        ctx.fillStyle = '#22d3ee';
        [33, 263, 133, 362, 159, 145, 386, 374].forEach(function (i) {
            ctx.beginPath(); ctx.arc(lm[i].x * w, lm[i].y * h, 2.4, 0, 7); ctx.fill();
        });
        ctx.fillStyle = '#ffb25e';
        [61, 291, 13, 14].forEach(function (i) {
            ctx.beginPath(); ctx.arc(lm[i].x * w, lm[i].y * h, 2.4, 0, 7); ctx.fill();
        });

        $('hudMat').textContent = matMo + '%';
        $('hudQuay').textContent = Math.round(Math.abs(goc.yaw)) + '°';
        window.__dv.mat = matMo; window.__dv.yaw = Math.round(goc.yaw);

        if (laAnhMau) {
            window.__dv.trangThai = 'tinh tao';
            $('dmsKq').innerHTML = '<span class="text-success fw-semibold">✔ Tỉnh táo</span> · mắt mở ' + matMo + '% · yaw ' + Math.round(goc.yaw) + '°';
            return;
        }

        var trangThai = 'Tỉnh táo', mauTT = 'text-success';

        // ---- 1. NGU GAT: mat nham keo dai HOAC dau guc xuong ----
        var nguDauHieu = nham > 0.52 || goc.pitch > 34;
        if (nguDauHieu) {
            if (!dms.matNhamTu) dms.matNhamTu = now;
            if (now - dms.matNhamTu > 1400) {
                trangThai = 'NGỦ GẬT'; mauTT = 'text-danger';
                if (canAlert('NguGat', 20000)) {
                    siren(2.6);
                    noi('Cảnh báo! Tài xế có dấu hiệu ngủ gật. Hãy dừng xe nghỉ ngơi ngay.');
                    dmsAlert('⚠ TÀI XẾ NGỦ GẬT — DỪNG XE NGAY');
                    baoSuCo('NguGat', 'Nguy hiểm', 'Cabin xe — camera DMS', 'DMS: tài xế nhắm mắt/gục đầu ' + ((now - dms.matNhamTu) / 1000).toFixed(1) + 's', cv);
                }
            }
        } else dms.matNhamTu = 0;

        // ---- 2. NGAP: jawOpen giu >0.55; >=3 lan trong 60s = met moi ----
        if (mieng > 0.55) {
            if (!dms.ngapBatDau) dms.ngapBatDau = now;
            if (now - dms.ngapBatDau > 550 && canAlert('demNgap', 3000)) {
                dms.ngapMoc.push(now);
            }
        } else dms.ngapBatDau = 0;
        dms.ngapMoc = dms.ngapMoc.filter(function (t) { return now - t < 60000; });
        $('hudNgap').textContent = dms.ngapMoc.length;
        window.__dv.ngap = dms.ngapMoc.length;
        if (dms.ngapMoc.length >= 3 && canAlert('MetMoi', 45000)) {
            beep();
            noi('Tài xế ngáp nhiều lần. Có dấu hiệu mệt mỏi, nên nghỉ giải lao.');
            baoSuCo('MatTapTrung', 'Chú ý', 'Cabin xe — camera DMS', 'DMS: ngáp ' + dms.ngapMoc.length + ' lần trong 1 phút — dấu hiệu mệt mỏi', cv);
        }

        // ---- 3. MAT TAP TRUNG: quay dau BAT KY huong nao >= 5 giay ----
        var g = gaze.step(goc, now);
        window.__dv.gaze = { huong: g.huong, giay: Math.round(g.giay * 10) / 10 };
        if (g.lech && g.huong) {
            $('hudQuay').textContent = Math.round(Math.max(Math.abs(goc.yaw), Math.abs(goc.pitch), Math.abs(goc.roll))) + '° ' + g.huong;
            if (g.giay >= 1.2 && !g.baoDong && trangThai === 'Tỉnh táo') {
                trangThai = 'Nhìn ' + tenHuong(g.huong) + ' ' + g.giay.toFixed(1) + 's / 5s';
                mauTT = 'text-warning';
            }
        }
        // Khi dang la NGU GAT (pitch>34 / nham mat) thi de luat ngu gat "so huu"
        // su kien — khong bao trung MAT TAP TRUNG cho cung 1 cai guc dau
        if (g.baoDong && !nguDauHieu) {
            if (trangThai === 'Tỉnh táo' || mauTT === 'text-warning') { trangThai = 'MẤT TẬP TRUNG'; mauTT = 'text-danger'; }
            if (canAlert('MatTapTrung', 25000)) {
                siren(2.2);
                noi('Cảnh báo. Tài xế nhìn ' + tenHuong(g.huong) + ' quá lâu. Hãy tập trung nhìn đường.');
                dmsAlert('⚠ MẤT TẬP TRUNG — NHÌN ' + tenHuong(g.huong).toUpperCase() + ' ' + g.giay.toFixed(0) + ' GIÂY', 3000);
                baoSuCo('MatTapTrung', 'Chú ý', 'Cabin xe — camera DMS',
                    'DMS: tài xế nhìn ' + tenHuong(g.huong) + ' liên tục ' + g.giay.toFixed(1) + 's (ngưỡng 5s)', cv);
            }
        }

        // ---- 3b. AI TU HOC: mang tu ma hoa phat hien trang thai LA ----
        // ruleBat: luat cung dang bao -> (1) khong hoc khung nay lam "binh thuong",
        // (2) khong bao trung — AI tu hoc chi bao cai ma luat cung KHONG bat duoc
        var ruleBat = nguDauHieu || dms.phoneCo || (g.lech && g.giay >= 1.2);
        if (!ano) ano = taoAno();
        var kqAno = ano.step(dacTrung(goc, nham, mieng, lm), now, ruleBat);
        window.__dv.ano = { pha: kqAno.pha, e: kqAno.e, nguong: kqAno.nguong, tienDo: kqAno.tienDo || 100 };
        capNhatAnoUI(kqAno);
        if (kqAno.baoDong && !ruleBat && canAlert('TrangThaiLa', 30000)) {
            if (trangThai === 'Tỉnh táo') { trangThai = 'TRẠNG THÁI LẠ'; mauTT = 'text-danger'; }
            beep();
            noi('Cảnh báo. AI phát hiện trạng thái bất thường của tài xế.');
            dmsAlert('⚠ AI TỰ HỌC: TRẠNG THÁI BẤT THƯỜNG', 3200);
            baoSuCo('TrangThaiLa', 'Chú ý', 'Cabin xe — camera DMS',
                'DMS: mạng tự mã hóa phát hiện trạng thái lạ (sai số ' + kqAno.ema.toFixed(4) + ' > ngưỡng ' + kqAno.nguong.toFixed(4) + ')', cv);
            hienPhanHoi();
        }
        dms.prevYaw = goc.yaw; dms.prevPitch = goc.pitch;

        // ---- 4. DIEN THOAI: YOLO lop 67, kiem tra moi 1.6s ----
        if (yolo && now - dms.phoneCheckLuc > 1600) {
            dms.phoneCheckLuc = now;
            yoloDetect(cv, [67]).then(function (bs) {
                if (bs.length > 0) {
                    dms.phoneHits++;
                    var b = bs[0];
                    var c2 = $('dmsCanvas').getContext('2d');
                    c2.strokeStyle = '#f59e0b'; c2.lineWidth = 3;
                    c2.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
                    if (dms.phoneHits >= 2 && canAlert('DungDienThoai', 30000)) {
                        siren(1.8);
                        noi('Cảnh báo! Tài xế đang dùng điện thoại khi lái xe.');
                        dmsAlert('⚠ DÙNG ĐIỆN THOẠI KHI LÁI XE');
                        baoSuCo('DungDienThoai', 'Nguy hiểm', 'Cabin xe — camera DMS', 'DMS: phát hiện điện thoại trong cabin (YOLO ' + Math.round(bs[0].score * 100) + '%)', cv);
                    }
                } else dms.phoneHits = 0;
                dms.phoneCo = bs.length > 0;
                $('hudPhone').textContent = dms.phoneCo ? 'CÓ ⚠' : '—';
            });
        }

        window.__dv.trangThai = trangThai;
        $('dmsKq').innerHTML = '<span class="' + mauTT + ' fw-semibold">' + trangThai + '</span>';
    }

    function dmsLoop(gen) {
        // Token the he: bat/tat nhanh khong duoc de 2 chuoi timer chay song song
        if (!dms.loopOn || gen !== dms.gen) return;
        var v = $('dmsVideo');
        if (v.readyState >= 2 && landmarker) {
            dmsXuLy(v, v.videoWidth, v.videoHeight, false);
        }
        setTimeout(function () { dmsLoop(gen); }, 110);   // ~9 fps — du nhay, nhe CPU
    }

    /* ============================================================
       CAMERA 2 — CARGO (khoang hang)
       ============================================================ */

    /** Tim cum mau CARTON (nau/cam) + goc truc chinh PCA cua tung cum */
    function timThungVaGoc(cv) {
        var w = cv.width, h = cv.height;
        var img = cv.getContext('2d').getImageData(0, 0, w, h).data;
        var CELL = 14, gw = Math.floor(w / CELL), gh = Math.floor(h / CELL);
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
                        var mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
                        var hue = 0;
                        if (d > 0) {
                            if (mx === r) hue = 60 * (((g - b) / d) % 6);
                            else if (mx === g) hue = 60 * ((b - r) / d + 2);
                            else hue = 60 * ((r - g) / d + 4);
                            if (hue < 0) hue += 360;
                        }
                        var s = mx > 0 ? d / mx : 0;
                        if (hue >= 15 && hue <= 52 && s >= 0.18 && s <= 0.85 && mx >= 0.22 && mx <= 0.95) hit++;
                        tot++;
                    }
                }
                grid[gy][gx] = (tot && hit / tot > 0.45) ? 1 : 0;
            }
        }
        var seen = {}, thungs = [];
        for (var sy = 0; sy < gh; sy++) for (var sx = 0; sx < gw; sx++) {
            if (!grid[sy][sx] || seen[sy + ',' + sx]) continue;
            var st = [[sx, sy]], cells = [];
            while (st.length) {
                var p = st.pop(), key = p[1] + ',' + p[0];
                if (seen[key]) continue;
                seen[key] = 1;
                if (p[0] < 0 || p[0] >= gw || p[1] < 0 || p[1] >= gh || !grid[p[1]][p[0]]) continue;
                cells.push(p);
                st.push([p[0] + 1, p[1]]); st.push([p[0] - 1, p[1]]); st.push([p[0], p[1] + 1]); st.push([p[0], p[1] - 1]);
            }
            if (cells.length < gw * gh * 0.012) continue;    // cum qua nho
            if (cells.length > gw * gh * 0.38) continue;     // cum qua lon = NEN (san/tuong) — khong phai kien hang
            // PCA: goc truc chinh cua dam o
            var mx2 = 0, my2 = 0;
            cells.forEach(function (c) { mx2 += c[0]; my2 += c[1]; });
            mx2 /= cells.length; my2 /= cells.length;
            var cxx = 0, cyy = 0, cxy = 0, mnX = 1e9, mxX = -1e9, mnY = 1e9, mxY = -1e9;
            cells.forEach(function (c) {
                var ddx = c[0] - mx2, ddy = c[1] - my2;
                cxx += ddx * ddx; cyy += ddy * ddy; cxy += ddx * ddy;
                mnX = Math.min(mnX, c[0]); mxX = Math.max(mxX, c[0]);
                mnY = Math.min(mnY, c[1]); mxY = Math.max(mxY, c[1]);
            });
            var goc = 0.5 * Math.atan2(2 * cxy, cxx - cyy) * 180 / Math.PI;   // -90..90
            // Do nghieng = do lech khoi phuong ngang HOAC doc (thung dung/thung nam deu OK)
            var nghieng = Math.min(Math.abs(goc), 90 - Math.abs(goc));
            thungs.push({
                x1: mnX * CELL, y1: mnY * CELL, x2: (mxX + 1) * CELL, y2: (mxY + 1) * CELL,
                goc: Math.round(Math.abs(goc)), nghieng: Math.round(nghieng), size: cells.length
            });
        }
        return thungs;
    }

    function cargoPhanTich() {
        var cv = $('cgCanvas');
        if (!cv.width) { $('cgStatus').textContent = 'Chưa có hình — chọn ảnh mẫu hoặc bật camera.'; return; }
        var ctx = cv.getContext('2d');
        // Ve lai nguon truoc khi phan tich (xoa overlay cu)
        if (cargo.nguon) ctx.drawImage(cargo.nguon, 0, 0, cv.width, cv.height);

        var thungs = timThungVaGoc(cv);
        var soNghieng = 0;
        ctx.lineWidth = Math.max(3, cv.width / 260);
        ctx.font = '700 ' + Math.max(13, Math.round(cv.width / 48)) + 'px "Be Vietnam Pro", sans-serif';
        thungs.forEach(function (t) {
            var xau = t.nghieng >= 25;
            if (xau) soNghieng++;
            ctx.strokeStyle = xau ? '#ef4444' : '#22c55e';
            ctx.strokeRect(t.x1, t.y1, t.x2 - t.x1, t.y2 - t.y1);
            var nhan = xau ? ('⚠ NGHIÊNG ~' + t.nghieng + '°') : ('✔ Ổn định · ' + t.nghieng + '°');
            ctx.fillStyle = xau ? '#ef4444' : '#22c55e';
            ctx.fillRect(t.x1 - 1, Math.max(0, t.y1 - 24), ctx.measureText(nhan).width + 10, 22);
            ctx.fillStyle = '#08131f';
            ctx.fillText(nhan, t.x1 + 4, Math.max(16, t.y1 - 7));
        });
        window.__dv.cargo.thung = thungs.length;
        window.__dv.cargo.goc = thungs.length ? Math.max.apply(null, thungs.map(function (t) { return t.nghieng; })) : 0;
        window.__dv.cargo.nghieng = soNghieng > 0;

        var pXam = Promise.resolve([]);
        if (yolo && $('swXeDung').checked) pXam = yoloDetect(cv, [0]);
        return pXam.then(function (nguoi) {
            window.__dv.cargo.nguoi = nguoi.length > 0;
            nguoi.forEach(function (b) {
                ctx.strokeStyle = '#f59e0b';
                ctx.setLineDash([8, 5]);
                ctx.strokeRect(b.x1, b.y1, b.x2 - b.x1, b.y2 - b.y1);
                ctx.setLineDash([]);
                var nh = '⚠ NGƯỜI TRONG KHOANG';
                ctx.fillStyle = '#f59e0b';
                ctx.fillRect(b.x1 - 1, Math.max(0, b.y1 - 24), ctx.measureText(nh).width + 10, 22);
                ctx.fillStyle = '#08131f';
                ctx.fillText(nh, b.x1 + 4, Math.max(16, b.y1 - 7));
            });

            var kq = [];
            if (soNghieng > 0) {
                var gocMax = window.__dv.cargo.goc;
                kq.push('<span class="text-danger fw-bold">⚠ ' + soNghieng + ' kiện NGHIÊNG (~' + gocMax + '°)</span>');
                if (canAlert('HangNghieng', 20000)) {
                    siren(2.4);
                    noi('Cảnh báo! Hàng trên xe bị nghiêng ' + gocMax + ' độ. Nguy cơ đổ vỡ.');
                    baoSuCo('HangNghieng', 'Nguy hiểm', 'Khoang hàng — camera cargo', 'Cargo: ' + soNghieng + ' kiện hàng nghiêng ~' + gocMax + '° (ngưỡng 25°)', cv);
                }
            }
            if (nguoi.length > 0) {
                kq.push('<span class="text-warning fw-bold">⚠ Phát hiện NGƯỜI trong khoang</span>');
                if (canAlert('XamNhap', 25000)) {
                    siren(2.4);
                    noi('Cảnh báo an ninh! Phát hiện người xâm nhập khoang hàng khi xe đang dừng.');
                    baoSuCo('XamNhap', 'Nguy hiểm', 'Khoang hàng — camera cargo', 'Cargo: phát hiện người trong khoang hàng khi xe dừng', cv);
                }
            }
            if (!kq.length) kq.push('<span class="text-success fw-semibold">✔ ' + thungs.length + ' kiện ổn định — khoang hàng an toàn</span>');
            $('cgKq').innerHTML = kq.join('<br/>');
            $('cgStatus').innerHTML = '🔎 Đã phân tích: ' + thungs.length + ' kiện hàng' +
                (nguoi.length ? ' · ' + nguoi.length + ' người' : '') + '.';
        });
    }

    var cargo = { nguon: null, loopOn: false, gen: 0 };
    function cargoNapAnh(src) {
        var img = new Image();
        img.onload = function () {
            var cv = $('cgCanvas');
            cv.width = img.naturalWidth; cv.height = img.naturalHeight;
            cv.getContext('2d').drawImage(img, 0, 0);
            cargo.nguon = img;
            $('cgStatus').textContent = '🖼️ Đã nạp ảnh khoang hàng — bấm Phân tích.';
            cargoPhanTich();
        };
        img.src = src;
    }
    function cargoLoop(gen) {
        // Token the he nhu dmsLoop — bat/tat nhanh khong tao 2 chuoi timer song song
        if (!cargo.loopOn || gen !== cargo.gen) return;
        var v = $('cgVideo');
        if (v.readyState >= 2) {
            var cv = $('cgCanvas');
            cv.width = v.videoWidth; cv.height = v.videoHeight;
            cv.getContext('2d').drawImage(v, 0, 0);
            cargo.nguon = null;                 // nguon la video truc tiep
            cargoPhanTich();
        }
        setTimeout(function () { cargoLoop(gen); }, 2500);
    }

    /* ---------------- Camera helpers ---------------- */
    function moCam(videoEl, onOk) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert('Trình duyệt chỉ cho phép camera trên HTTPS hoặc localhost.');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { width: { ideal: 960 } }, audio: false })
            .then(function (s) { videoEl.srcObject = s; videoEl.play(); onOk(s); })
            .catch(function () { alert('Không truy cập được camera (cần cấp quyền).'); });
    }

    /* ---------------- Khoi dong ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        if (!$('dmsStatus')) return;            // khong phai trang Driver

        // Nap song song 2 engine
        var pY = initYolo().catch(function () { window.__dv.yoloReady = false; });
        var pF = initFace().catch(function () { window.__dv.faceReady = false; });

        pF.then(function () {
            if (landmarker) {
                $('dmsStatus').innerHTML = '✅ FaceLandmarker sẵn sàng (478 điểm mốc + blendshapes).';
                $('btnDmsCam').disabled = false;
                document.querySelectorAll('.dv-sample-face').forEach(function (b) { b.disabled = false; });
            } else {
                $('dmsStatus').innerHTML = '❌ Không tải được model khuôn mặt — kiểm tra Internet.';
            }
        });
        pY.then(function () {
            // pY da .catch nuot loi o tren -> LUON vao day; phai check bien yolo
            // that su (ban cu bao "YOLO san sang" ca khi tai fail — nguoi dung
            // tuong phat hien nguoi/dien thoai dang bat ma thuc ra da chet im)
            if (yolo) {
                $('cgStatus').innerHTML = '✅ YOLO26 sẵn sàng.';
                ['btnCgCam', 'btnCgScan'].forEach(function (id) { $(id).disabled = false; });
            } else {
                // Van cho phan tich nghieng bang mau sac (khong can YOLO)
                $('cgStatus').innerHTML = '⚠️ YOLO lỗi — vẫn đo nghiêng bằng phân tích màu (không phát hiện người/điện thoại).';
                ['btnCgScan'].forEach(function (id) { $(id).disabled = false; });
            }
            document.querySelectorAll('.cg-sample').forEach(function (b) { b.disabled = false; });
        });
        Promise.all([pY, pF]).then(function () {
            var ok = window.__dv.faceReady && window.__dv.yoloReady;
            $('dvEngine').innerHTML = ok
                ? '<i class="bi bi-cpu-fill me-1"></i>FaceLandmarker + YOLO26n'
                : '<i class="bi bi-exclamation-triangle me-1"></i>Thiếu engine';
            $('dvEngine').className = 'badge-st ' + (ok ? 'st-ok' : 'st-info');
        });

        // --- DMS ---
        $('btnDmsCam').addEventListener('click', function () {
            var self = this, v = $('dmsVideo');
            if (dmsStream) {
                dmsStream.getTracks().forEach(function (t) { t.stop(); });
                dmsStream = null; dms.loopOn = false; dms.gen++;
                dms.matNhamTu = 0; dms.ngapBatDau = 0; dms.phoneHits = 0;
                gaze.reset();
                if (ano) ano.ngat();
                self.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật camera tài xế';
                return;
            }
            moCam(v, function (s) {
                dmsStream = s; dms.loopOn = true; dms.gen++;
                // Phien camera moi: hoc lai nen (tai xe/anh sang co the da khac),
                // trong so mang van giu nen hoc lai rat nhanh
                dms.matNhamTu = 0; dms.ngapBatDau = 0; dms.phoneHits = 0;
                gaze.reset();
                if (ano) ano.hocLai();
                self.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
                $('dmsStatus').innerHTML = '🎥 Đang giám sát tài xế — <b>~20 giây đầu AI tự học tư thế lái bình thường</b>, hãy nhìn thẳng tự nhiên; sau đó thử <b>quay đầu 5 giây</b>, <b>nhắm mắt</b> hoặc <b>ngáp</b>.';
                dmsLoop(dms.gen);
            });
        });
        document.querySelectorAll('.dv-sample-face').forEach(function (b) {
            b.addEventListener('click', function () {
                var img = new Image();
                img.crossOrigin = 'anonymous';
                img.onload = function () { dmsXuLy(img, img.naturalWidth, img.naturalHeight, true); };
                img.src = b.getAttribute('data-src');
            });
        });

        // --- Phan hoi day AI tu hoc (thuong/phat kieu hoc tang cuong) ---
        if ($('btnAnoDung')) {
            $('btnAnoDung').addEventListener('click', function () {
                if (!ano) return;
                var k = ano.phanHoi(true);
                $('anoFb').style.display = 'none';
                capNhatAnoUI._giu = Date.now() + 4000;
                $('anoTT').textContent = 'đã ghi nhận cảnh báo ĐÚNG — AI nhạy hơn (k=' + k.toFixed(1) + ')';
                $('anoTT').className = 'text-success';
            });
            $('btnAnoNham').addEventListener('click', function () {
                if (!ano) return;
                var k = ano.phanHoi(false);
                $('anoFb').style.display = 'none';
                capNhatAnoUI._giu = Date.now() + 4000;
                $('anoTT').textContent = 'đã học lại vùng vừa báo — nâng ngưỡng (k=' + k.toFixed(1) + ')';
                $('anoTT').className = 'text-success';
            });
        }

        // --- CARGO ---
        document.querySelectorAll('.cg-sample').forEach(function (b) {
            b.addEventListener('click', function () {
                if (cgStream) { cgStream.getTracks().forEach(function (t) { t.stop(); }); cgStream = null; cargo.loopOn = false; }
                cargoNapAnh(b.getAttribute('data-src'));
            });
        });
        $('btnCgScan').addEventListener('click', cargoPhanTich);
        $('btnCgCam').addEventListener('click', function () {
            var self = this, v = $('cgVideo');
            if (cgStream) {
                cgStream.getTracks().forEach(function (t) { t.stop(); });
                cgStream = null; cargo.loopOn = false; cargo.gen++;
                self.innerHTML = '<i class="bi bi-camera-video me-1"></i>Camera khoang';
                return;
            }
            moCam(v, function (s) {
                cgStream = s; cargo.loopOn = true; cargo.gen++;
                self.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
                $('cgStatus').innerHTML = '🎥 Đang giám sát khoang hàng (tự phân tích 2,5s/lần).';
                cargoLoop(cargo.gen);
            });
        });
    });
})();
