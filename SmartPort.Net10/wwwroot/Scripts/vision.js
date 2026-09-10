/* ============================================================
   SMARTVISION AI — Deep Learning nhan dien doi tuong tai cang
   Engine chinh : YOLOv10n (ONNX Runtime Web, chay ngay trong trinh duyet)
   Du phong     : SSD MobileNet (TensorFlow.js coco-ssd)
   ============================================================ */
(function () {
    // YOLO26 (Ultralytics, 01/2026) — end-to-end NMS-free, output [1,300,6]
    // Nhanh hon YOLO11n toi 43% khi chay ONNX tren CPU.
    var MODELS = {
        'yolo26n': { url: (window.APP_ROOT || '/') + 'Content/models/yolo26n.onnx', ten: 'YOLO26n', mAP: 40.9, mb: 9.5, mota: 'Nhanh nhất — hợp webcam real-time' },
        'yolo26s': { url: 'https://huggingface.co/zwh20081/yolo26-onnx/resolve/main/yolo26s.onnx', ten: 'YOLO26s', mAP: 48.6, mb: 36.5, mota: 'Cân bằng — chính xác hơn rõ rệt' },
        'yolo26m': { url: 'https://huggingface.co/zwh20081/yolo26-onnx/resolve/main/yolo26m.onnx', ten: 'YOLO26m', mAP: 53.1, mb: 78.2, mota: 'Chính xác cao — nhận diện vật nhỏ/xa' },
        'yolo26l': { url: 'https://huggingface.co/zwh20081/yolo26-onnx/resolve/main/yolo26l.onnx', ten: 'YOLO26l', mAP: 55.0, mb: 95, mota: 'MẠNH NHẤT trên web — nên dùng khi có WebGPU' }
    };
    var FALLBACK = { url: 'https://huggingface.co/onnx-community/yolov10n/resolve/main/onnx/model.onnx', ten: 'YOLOv10n', mAP: 38.5, mb: 9, mota: 'Dự phòng' };
    var curModel = 'yolo26n';
    var ORT_CDN = (window.APP_ROOT || '/') + 'Scripts/vendor/ort/';   // LOCAL — khong can CDN
    var INPUT = 640;

    // WebGPU: chay AI bang GPU ngay trong trinh duyet (Edge/Chrome 113+),
    // nhanh gap nhieu lan WASM CPU — tu phat hien, khong co thi fallback CPU.
    var useGpu = !!navigator.gpu;
    var modelFromCache = false;

    // Nguong tin cay RIENG tung lop do he thong TU HOC tu phan hoi nguoi dung
    var nguongLop = {};   // vd { person: 0.5, truck: 0.3 }

    var COCO = ['person','bicycle','car','motorcycle','airplane','bus','train','truck','boat','traffic light',
        'fire hydrant','stop sign','parking meter','bench','bird','cat','dog','horse','sheep','cow',
        'elephant','bear','zebra','giraffe','backpack','umbrella','handbag','tie','suitcase','frisbee',
        'skis','snowboard','sports ball','kite','baseball bat','baseball glove','skateboard','surfboard','tennis racket','bottle',
        'wine glass','cup','fork','knife','spoon','bowl','banana','apple','sandwich','orange',
        'broccoli','carrot','hot dog','pizza','donut','cake','chair','couch','potted plant','bed',
        'dining table','toilet','tv','laptop','mouse','remote','keyboard','cell phone','microwave','oven',
        'toaster','sink','refrigerator','book','clock','vase','scissors','teddy bear','hair drier','toothbrush'];
    var VI = { person: 'Người', bicycle: 'Xe đạp', car: 'Ô tô', motorcycle: 'Xe máy', airplane: 'Máy bay',
        bus: 'Xe buýt', train: 'Tàu hỏa', truck: 'Xe tải/đầu kéo', boat: 'Tàu/thuyền', 'traffic light': 'Đèn giao thông',
        bench: 'Băng ghế', backpack: 'Ba lô', suitcase: 'Vali/thùng', laptop: 'Laptop', 'cell phone': 'Điện thoại',
        chair: 'Ghế', tv: 'Màn hình', clock: 'Đồng hồ', bottle: 'Chai', cup: 'Cốc', book: 'Sách', tie: 'Cà vạt' };

    var engine = null;        // 'yolo' | 'ssd'
    var session = null, ssdModel = null;
    var busy = false, camTimer = null, stream = null;
    var nguonHienTai = 'AnhMau';   // AnhMau | TaiAnh | Webcam

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

    /* ---------------- Nap engine ---------------- */
    // Tai model qua Cache API: LAN DAU tai tu mang roi luu bo dem —
    // cac lan sau nap TUC THI tu dia (ke ca offline).
    function fetchModel(url) {
        if (!('caches' in window)) {
            modelFromCache = false;
            return fetch(url).then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.arrayBuffer();
            });
        }
        return caches.open('sp-models-v1').then(function (c) {
            return c.match(url).then(function (hit) {
                if (hit) { modelFromCache = true; return hit.arrayBuffer(); }
                modelFromCache = false;
                return fetch(url).then(function (r) {
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    return c.put(url, r.clone()).then(function () { return r.arrayBuffer(); });
                });
            });
        });
    }

    // Chuoi du phong: YOLO26 (model chon) -> YOLOv10n -> SSD MobileNet
    function loadOnnx(info, isFallback) {
        status('Đang tải mô hình <strong>' + info.ten + '</strong> (~' + info.mb + 'MB, lần đầu hơi lâu — các lần sau nạp tức thì từ bộ đệm)...', true);
        return fetchModel(info.url)
            .then(function (buf) {
                status('Đang khởi tạo <strong>' + info.ten + '</strong> trên ' + (useGpu ? 'WebGPU (GPU ⚡)' : 'WASM (CPU)') + '...', true);
                var providers = useGpu ? ['webgpu', 'wasm'] : ['wasm'];
                return ort.InferenceSession.create(buf, { executionProviders: providers })
                    .catch(function (e) {
                        // GPU khong kha dung/loi driver -> thu lai bang CPU
                        if (useGpu) {
                            console.warn('WebGPU that bai, chuyen sang WASM CPU:', e);
                            useGpu = false;
                            return ort.InferenceSession.create(buf, { executionProviders: ['wasm'] });
                        }
                        throw e;
                    });
            })
            .then(function (s) {
                session = s; engine = 'yolo';
                els.engineBadge.innerHTML = '<i class="bi bi-' + (useGpu ? 'gpu-card' : 'cpu-fill') + ' me-1"></i>' + info.ten +
                    (useGpu ? ' · WebGPU ⚡' : ' · WASM CPU');
                els.engineBadge.className = isFallback ? 'badge-st st-warn' : 'badge-st st-ok';
                status('✅ <strong>' + info.ten + '</strong> (mAP ' + info.mAP + ') sẵn sàng trên <b>' +
                    (useGpu ? 'GPU (WebGPU)' : 'CPU (WASM)') + '</b>' +
                    (modelFromCache ? ' · nạp từ bộ đệm ⚡' : '') +
                    '! Chọn ảnh mẫu, tải ảnh lên hoặc bật webcam.');
                enableUi();
                if (location.search.indexOf('auto=1') >= 0) setTimeout(function () { var b = document.querySelectorAll('.vs-sample')[0]; if (b) b.click(); }, 500);
            });
    }

    function initEngine() {
        status('Đang tải thư viện ONNX Runtime' + (useGpu ? ' (WebGPU)' : '') + '...', true);
        // Ban webgpu chua bundle rieng: ort.webgpu.min.js co ca wasm + webgpu
        loadScript(ORT_CDN + (useGpu ? 'ort.webgpu.min.js' : 'ort.min.js'))
            .then(function () {
                ort.env.wasm.wasmPaths = ORT_CDN;
                return loadOnnx(MODELS[curModel], false);
            })
            .catch(function (e) {
                console.warn('YOLO26 load failed, thu YOLOv10n:', e);
                return loadOnnx(FALLBACK, true).catch(function (e2) {
                    console.warn('YOLOv10n cung loi, fallback SSD:', e2);
                    initSsd();
                });
            });

        // Nap nguong tu hoc tu server (active learning)
        fetch((window.APP_ROOT || '/') + 'Admin/Vision/Nguong', { credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) return;
                d.lops.forEach(function (l) { if (l.nguong) nguongLop[l.nhan] = l.nguong; });
                veBangTuHoc(d);
            }).catch(function () { });
    }

    // Doi model khi nguoi dung chon (n / s / m).
    // GIU session cu cho toi khi model moi nap xong — webcam van chay tiep
    // bang model cu trong luc tai, va "giu nguyen engine cu" khi loi la THAT
    // (ban truoc release som -> doi model luc webcam dang chay la treo trang).
    function switchModel(key) {
        if (!MODELS[key] || key === curModel) return;
        var keyCu = curModel, sesCu = session;
        curModel = key;
        disableUi();
        loadOnnx(MODELS[key], false).then(function () {
            if (sesCu && sesCu !== session && sesCu.release) { try { sesCu.release(); } catch (e) { } }
        }).catch(function (e) {
            console.warn('Doi model that bai:', e);
            curModel = keyCu; session = sesCu;
            status('❌ Không tải được ' + MODELS[key].ten + ' — giữ nguyên engine cũ.');
            enableUi();
        });
    }

    function initSsd() {
        status('Không tải được YOLO — chuyển sang engine dự phòng SSD MobileNet...', true);
        loadScript((window.APP_ROOT || '/') + 'Scripts/vendor/tf/tf.min.js')
            .then(function () { return loadScript((window.APP_ROOT || '/') + 'Scripts/vendor/tf/coco-ssd.min.js'); })
            .then(function () { return cocoSsd.load(); })
            .then(function (m) {
                ssdModel = m; engine = 'ssd';
                els.engineBadge.innerHTML = '<i class="bi bi-cpu me-1"></i>SSD MobileNet · TF.js (dự phòng)';
                els.engineBadge.className = 'badge-st st-warn';
                status('✅ Engine dự phòng sẵn sàng! Chọn ảnh hoặc bật webcam.');
                enableUi();
            })
            .catch(function () {
                status('❌ Không tải được mô hình AI (kiểm tra kết nối Internet rồi tải lại trang).');
            });
    }

    function enableUi() {
        ['btnCam', 'fileInput', 'vsModel'].forEach(function (id) { if ($(id)) $(id).disabled = false; });
        document.querySelectorAll('.vs-sample').forEach(function (b) { b.disabled = false; });
    }
    function disableUi() {
        ['btnCam', 'fileInput', 'vsModel'].forEach(function (id) { if ($(id)) $(id).disabled = true; });
        document.querySelectorAll('.vs-sample').forEach(function (b) { b.disabled = true; });
    }

    /* ---------------- Tien xu ly YOLO ---------------- */
    // TOI UU: do phan giai THICH UNG — webcam tren CPU (WASM) dung 448px
    // de tang FPS gap ~2 lan; anh tinh hoac co WebGPU van dung 640px cho
    // do chinh xac toi da. YOLO chap nhan moi kich thuoc chia het cho 32.
    // force640: model export co the co input TINH 640x640 — neu chay 448 bi
    // loi kich thuoc thi tu chuyen ve 640 vinh vien trong phien (tu chua lanh)
    var force640 = false;
    function inputSizeNow() {
        return (!useGpu && stream && !force640) ? 448 : INPUT;
    }

    function preprocess(source, w, h) {
        var SZ = inputSizeNow();
        var c = els.work;
        c.width = SZ; c.height = SZ;
        var ctx = c.getContext('2d');
        var scale = Math.min(SZ / w, SZ / h);
        var nw = Math.round(w * scale), nh = Math.round(h * scale);
        var padX = Math.floor((SZ - nw) / 2), padY = Math.floor((SZ - nh) / 2);
        ctx.fillStyle = '#727272';
        ctx.fillRect(0, 0, SZ, SZ);
        ctx.drawImage(source, 0, 0, w, h, padX, padY, nw, nh);
        var img = ctx.getImageData(0, 0, SZ, SZ).data;
        var area = SZ * SZ;
        var t = new Float32Array(3 * area);
        for (var i = 0; i < area; i++) {
            t[i] = img[i * 4] / 255;
            t[area + i] = img[i * 4 + 1] / 255;
            t[2 * area + i] = img[i * 4 + 2] / 255;
        }
        return { tensor: new ort.Tensor('float32', t, [1, 3, SZ, SZ]), scale: scale, padX: padX, padY: padY };
    }

    /* TOI UU: LAM MUOT THEO THOI GIAN (temporal smoothing) cho webcam —
       khop box moi voi box khung truoc (IoU + cung nhan) roi lam muot
       toa do bang EMA => khung nhan dien on dinh, khong con giat nhay. */
    var smoothPrev = [];
    function smoothBoxes(boxes) {
        if (!stream) { smoothPrev = []; return boxes; }
        var A = 0.55;   // trong so khung moi
        var out = boxes.map(function (b) {
            var best = null, bestIou = 0.4;
            for (var i = 0; i < smoothPrev.length; i++) {
                var p = smoothPrev[i];
                if (p.label !== b.label) continue;
                var x1 = Math.max(p.x, b.x), y1 = Math.max(p.y, b.y);
                var x2 = Math.min(p.x + p.w, b.x + b.w), y2 = Math.min(p.y + p.h, b.y + b.h);
                var inter = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
                var u = p.w * p.h + b.w * b.h - inter;
                var v = u > 0 ? inter / u : 0;
                if (v > bestIou) { bestIou = v; best = p; }
            }
            if (best) {
                return {
                    x: A * b.x + (1 - A) * best.x, y: A * b.y + (1 - A) * best.y,
                    w: A * b.w + (1 - A) * best.w, h: A * b.h + (1 - A) * best.h,
                    score: A * b.score + (1 - A) * best.score, label: b.label
                };
            }
            return b;
        });
        smoothPrev = out;
        return out;
    }

    /* ---------------- Phan tich dau ra ---------------- */
    // Nguong hieu luc cua 1 lop = MAX(nguong slider, nguong AI TU HOC cua lop do)
    function nguongCua(cls, conf) {
        var lbl = COCO[cls];
        return Math.max(conf, nguongLop[lbl] || 0);
    }

    function parseYolo(output, meta, conf) {
        var boxes = [];
        var dims = output.dims, d = output.data;
        if (dims.length === 3 && dims[2] === 6) {
            // YOLOv10/26: [1, 300, (x1,y1,x2,y2,score,cls)] — NMS-free
            for (var i = 0; i < dims[1]; i++) {
                var o = i * 6, sc = d[o + 4], cls6 = Math.round(d[o + 5]);
                if (sc < nguongCua(cls6, conf)) continue;
                boxes.push(toBox(d[o], d[o + 1], d[o + 2], d[o + 3], sc, cls6, meta));
            }
        } else if (dims.length === 3 && dims[1] === 84) {
            // YOLOv8/11: [1, 84, 8400] — can NMS
            var n = dims[2], raw = [];
            for (var a = 0; a < n; a++) {
                var best = 0, cls = -1;
                for (var k = 0; k < 80; k++) {
                    var s = d[(4 + k) * n + a];
                    if (s > best) { best = s; cls = k; }
                }
                if (best < conf) continue;
                var cx = d[a], cy = d[n + a], w = d[2 * n + a], h = d[3 * n + a];
                raw.push({ x1: cx - w / 2, y1: cy - h / 2, x2: cx + w / 2, y2: cy + h / 2, sc: best, cls: cls });
            }
            raw.sort(function (p, q) { return q.sc - p.sc; });
            var keep = [];
            raw.forEach(function (b) {
                for (var j = 0; j < keep.length; j++) { if (iou(b, keep[j]) > 0.45) return; }
                keep.push(b);
            });
            keep.forEach(function (b) { boxes.push(toBox(b.x1, b.y1, b.x2, b.y2, b.sc, b.cls, meta)); });
        }
        return boxes;
    }

    function toBox(x1, y1, x2, y2, sc, cls, meta) {
        return {
            x: (x1 - meta.padX) / meta.scale,
            y: (y1 - meta.padY) / meta.scale,
            w: (x2 - x1) / meta.scale,
            h: (y2 - y1) / meta.scale,
            score: sc,
            label: COCO[cls] || ('#' + cls)
        };
    }

    function iou(a, b) {
        var x1 = Math.max(a.x1, b.x1), y1 = Math.max(a.y1, b.y1);
        var x2 = Math.min(a.x2, b.x2), y2 = Math.min(a.y2, b.y2);
        var inter = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
        var ua = (a.x2 - a.x1) * (a.y2 - a.y1) + (b.x2 - b.x1) * (b.y2 - b.y1) - inter;
        return ua > 0 ? inter / ua : 0;
    }

    /* ---------------- Nhan dien ---------------- */
    function detect(source, w, h) {
        var conf = parseFloat(els.conf.value);
        var t0 = performance.now();
        // Guard: model dang duoc thay/chua san sang -> tra promise REJECT thay
        // vi nem loi dong bo (nem dong bo lam camLoop khong bat duoc, busy ket true)
        if (engine === 'yolo' && !session) return Promise.reject(new Error('Model đang tải, thử lại sau.'));
        if (engine !== 'yolo' && !ssdModel) return Promise.reject(new Error('Engine chưa sẵn sàng.'));
        if (engine === 'yolo') {
            var meta = preprocess(source, w, h);
            var feeds = {};
            feeds[session.inputNames[0]] = meta.tensor;
            return session.run(feeds).then(function (out) {
                var boxes = smoothBoxes(parseYolo(out[session.outputNames[0]], meta, conf));
                return { boxes: boxes, ms: Math.round(performance.now() - t0) };
            });
        }
        return ssdModel.detect(source).then(function (preds) {
            var boxes = preds.filter(function (p) { return p.score >= conf; }).map(function (p) {
                return { x: p.bbox[0], y: p.bbox[1], w: p.bbox[2], h: p.bbox[3], score: p.score, label: p.class };
            });
            return { boxes: boxes, ms: Math.round(performance.now() - t0) };
        });
    }

    /* ---------------- Ve + tong hop ---------------- */
    var PALETTE = ['#ff7d2d', '#22d3ee', '#a3e635', '#f472b6', '#facc15', '#818cf8', '#34d399', '#fb7185'];
    function colorOf(label) {
        var hsh = 0;
        for (var i = 0; i < label.length; i++) hsh = (hsh * 31 + label.charCodeAt(i)) & 0xffff;
        return PALETTE[hsh % PALETTE.length];
    }
    function viName(label) { return VI[label] || label; }

    function render(source, w, h, result) {
        var c = els.canvas, ctx = c.getContext('2d');
        c.width = w; c.height = h;
        ctx.drawImage(source, 0, 0, w, h);
        var fs = Math.max(13, Math.round(w / 55));
        ctx.lineWidth = Math.max(2.5, w / 320);
        ctx.font = '600 ' + fs + 'px "Be Vietnam Pro", sans-serif';
        result.boxes.forEach(function (b) {
            var col = colorOf(b.label);
            ctx.strokeStyle = col;
            ctx.strokeRect(b.x, b.y, b.w, b.h);
            var txt = viName(b.label) + ' ' + Math.round(b.score * 100) + '%';
            var tw = ctx.measureText(txt).width + 10;
            var ty = b.y > fs + 8 ? b.y - fs - 6 : b.y + 2;
            ctx.fillStyle = col;
            ctx.fillRect(b.x - 1, ty, tw, fs + 6);
            ctx.fillStyle = '#0a1420';
            ctx.fillText(txt, b.x + 4, ty + fs);
        });
        summarize(result);
    }

    function summarize(result) {
        var counts = {}, avgSc = {};
        result.boxes.forEach(function (b) {
            counts[b.label] = (counts[b.label] || 0) + 1;
            avgSc[b.label] = (avgSc[b.label] || 0) + b.score;
        });
        var chips = '';
        Object.keys(counts).sort(function (a, b) { return counts[b] - counts[a]; }).forEach(function (k) {
            var tb = Math.round(avgSc[k] / counts[k] * 1000) / 10;
            chips += '<span class="vs-chip" style="border-color:' + colorOf(k) + '">' + viName(k) + ' <strong>' + counts[k] + '</strong>' +
                ' <button type="button" class="vs-fb" data-nhan="' + k + '" data-dung="1" data-tc="' + tb + '" title="AI nhận ĐÚNG lớp này">✓</button>' +
                '<button type="button" class="vs-fb vs-fb-sai" data-nhan="' + k + '" data-dung="0" data-tc="' + tb + '" title="AI nhận SAI lớp này">✗</button></span>';
        });
        els.chips.innerHTML = chips || '<span class="text-muted">Không phát hiện đối tượng nào (thử giảm ngưỡng tin cậy).</span>';
        // Gan su kien phan hoi (vong lap tu hoc)
        els.chips.querySelectorAll('.vs-fb').forEach(function (btn) {
            btn.addEventListener('click', function () { guiPhanHoi(btn); });
        });
        els.meta.textContent = result.boxes.length + ' đối tượng · ' + result.ms + 'ms/khung hình · ngưỡng ' + Math.round(parseFloat(els.conf.value) * 100) + '%';

        var ins = [];
        var trucks = (counts['truck'] || 0) + (counts['bus'] || 0);
        if (trucks >= 3) ins.push('🚛 Có <strong>' + trucks + ' xe tải</strong> trong khung hình — lưu lượng cổng đang cao, cân nhắc mở thêm làn check-in.');
        else if (trucks > 0) ins.push('🚛 ' + trucks + ' xe tải — lưu lượng cổng bình thường.');
        if ((counts['person'] || 0) >= 4) ins.push('👥 Phát hiện <strong>' + counts['person'] + ' người</strong> — nhắc tuân thủ khu vực an toàn và đồ bảo hộ.');
        if (counts['boat']) ins.push('🚢 Phát hiện <strong>' + counts['boat'] + ' tàu/phương tiện thủy</strong> trong khu nước.');
        if (counts['car'] || counts['motorcycle']) ins.push('🚗 Phương tiện cá nhân xuất hiện — kiểm tra đăng ký ra vào cảng.');
        els.insight.innerHTML = ins.length
            ? ins.map(function (s) { return '<div class="ai-note">' + s + '</div>'; }).join('')
            : '<div class="ai-note">Khung hình an toàn — không có cảnh báo điều phối.</div>';

        luuKetQua(counts, result);
    }

    /* ---------------- VONG LAP TU HOC (Active Learning) ---------------- */
    // Gui phan hoi dung/sai -> he thong tu tinh lai nguong tung lop
    function guiPhanHoi(btn) {
        var fd = new FormData();
        fd.append('nhan', btn.getAttribute('data-nhan'));
        fd.append('dung', btn.getAttribute('data-dung') === '1');
        fd.append('doTinCay', btn.getAttribute('data-tc'));
        btn.disabled = true;
        fetch((window.APP_ROOT || '/') + 'Admin/Vision/Feedback', { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) return;
                btn.textContent = '✔';
                // Tai lai nguong moi + ve lai bang tu hoc
                return fetch((window.APP_ROOT || '/') + 'Admin/Vision/Nguong', { credentials: 'same-origin' })
                    .then(function (r) { return r.json(); })
                    .then(function (n) {
                        if (!n.ok) return;
                        nguongLop = {};
                        n.lops.forEach(function (l) { if (l.nguong) nguongLop[l.nhan] = l.nguong; });
                        veBangTuHoc(n);
                    });
            }).catch(function () { btn.disabled = false; });
    }

    // Ve panel "AI tu hoc": precision tung lop + nguong da tu dieu chinh
    function veBangTuHoc(d) {
        var el = $('vsSelfLearn');
        if (!el) return;
        var html = '<div class="small fw-semibold mb-1">🧠 Vòng lặp tự học — <b>' + d.tongPhanHoi + '</b> phản hồi đã thu thập</div>';
        d.lops.forEach(function (l) {
            var mau = l.precision >= 90 ? '#15803d' : l.precision >= 70 ? '#b45309' : '#b91c1c';
            html += '<div class="d-flex justify-content-between align-items-center" style="font-size:.78rem;padding:2px 0;">' +
                '<span>' + viName(l.nhan) + ' <span class="text-muted">(' + l.dung + '/' + l.tong + ' đúng)</span></span>' +
                '<span style="color:' + mau + ';font-weight:700;">' + l.precision + '%</span>' +
                '<span class="text-muted">' + (l.nguong ? 'ngưỡng tự chỉnh → <b>' + Math.round(l.nguong * 100) + '%</b>' : 'ngưỡng chuẩn') + '</span>' +
                '</div>';
        });
        el.innerHTML = html;
        el.style.display = '';
    }

    /* ---------------- Luu ket qua vao CSDL (module nghiep vu) ---------------- */
    var lanLuuCuoi = 0;
    function luuKetQua(counts, result) {
        var chk = $('vsAutoSave');
        if (!chk || !chk.checked) return;                 // nguoi dung tat luu
        if (!result.boxes.length) return;                 // khong co gi de luu
        // Webcam ban lien tuc -> chi luu toi da 1 lan/8 giay de khong spam CSDL
        var now = Date.now();
        if (stream && now - lanLuuCuoi < 8000) return;
        lanLuuCuoi = now;

        // Gom theo nhan + do tin cay trung binh cua tung nhan
        var tong = {}, diem = {};
        result.boxes.forEach(function (b) {
            tong[b.label] = (tong[b.label] || 0) + 1;
            diem[b.label] = (diem[b.label] || 0) + b.score;
        });
        var items = Object.keys(tong).map(function (k) {
            return {
                nhan: viName(k),
                nhanGoc: k,
                soLuong: tong[k],
                doTinCay: Math.round((diem[k] / tong[k]) * 1000) / 10
            };
        });
        var tbAll = result.boxes.reduce(function (s, b) { return s + b.score; }, 0) / result.boxes.length;

        var viTriEl = $('vsViTri');
        var payload = {
            nguon: stream ? 'Webcam' : (nguonHienTai || 'AnhMau'),
            model: (els.engineBadge && els.engineBadge.textContent || '').replace(/·.*/, '').trim(),
            viTri: viTriEl ? viTriEl.value : 'Cổng chính',
            thoiGianXuLy: result.ms,
            doTinCayTB: Math.round(tbAll * 1000) / 10,
            items: items
        };

        fetch(window.APP_ROOT + 'Admin/Vision/Save', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ data: payload })
        }).then(function (r) { return r.json(); })
          .then(function (d) {
              if (!d || !d.ok) return;
              var el = $('vsSaved');
              if (el) {
                  el.style.display = '';
                  el.innerHTML = '<i class="bi bi-cloud-check-fill me-1"></i>Đã lưu vào CSDL (#' + d.maND + ')' +
                      (d.mucCanhBao !== 'Bình thường' ? ' · <strong>' + d.mucCanhBao + '</strong>' : '');
                  setTimeout(function () { el.style.display = 'none'; }, 4000);
              }
          }).catch(function () { });
    }

    /* ---------------- Nguon anh ---------------- */
    function analyzeImage(img) {
        if (busy || !engine) return;
        busy = true;
        status('🧠 AI đang phân tích...', true);
        detect(img, img.naturalWidth || img.width, img.naturalHeight || img.height)
            .then(function (r) {
                render(img, img.naturalWidth || img.width, img.naturalHeight || img.height, r);
                status('Xong! Chọn ảnh khác hoặc chỉnh ngưỡng rồi phân tích lại.');
            })
            .catch(function (e) { console.error(e); status('❌ Lỗi phân tích: ' + e.message); })
            .then(function () { busy = false; });
    }

    function loadUrl(url) {
        stopCam();
        var img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = function () { analyzeImage(img); };
        img.onerror = function () { status('❌ Không tải được ảnh (CORS/mạng).'); };
        img.src = url;
        status('Đang tải ảnh...', true);
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
                render(els.video, w, h, r);
                busy = false;
                camTimer = setTimeout(camLoop, 120);
            }).catch(function (e) {
                // Loi kich thuoc input (model tinh 640) -> doi ve 640 roi chay tiep
                if (!force640 && !useGpu && e && /dim|shape|size|448/i.test(String(e.message || e))) force640 = true;
                busy = false; camTimer = setTimeout(camLoop, 500);
            });
        } else camTimer = setTimeout(camLoop, 200);
    }
    function stopCam() {
        if (camTimer) clearTimeout(camTimer);
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        if (els.btnCam) els.btnCam.innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật webcam AI';
    }

    /* ---------------- Khoi dong ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        els = { status: $('vsStatus'), engineBadge: $('vsEngine'), canvas: $('vsCanvas'), work: $('vsWork'),
                chips: $('vsChips'), meta: $('vsMeta'), insight: $('vsInsight'), conf: $('vsConf'),
                video: $('vsVideo'), btnCam: $('btnCam') };
        if (!els.status) return;

        $('fileInput').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            stopCam();
            nguonHienTai = 'TaiAnh';
            var rd = new FileReader();
            rd.onload = function () {
                var img = new Image();
                img.onload = function () { analyzeImage(img); };
                img.src = rd.result;
            };
            rd.readAsDataURL(f);
            this.value = '';               // chon lai CUNG 1 file van phan tich
        });
        document.querySelectorAll('.vs-sample').forEach(function (b) {
            b.addEventListener('click', function () { nguonHienTai = 'AnhMau'; loadUrl(b.getAttribute('data-src')); });
        });
        els.btnCam.addEventListener('click', function () { stream ? stopCam() : startCam(); });
        if ($('vsModel')) $('vsModel').addEventListener('change', function () { switchModel(this.value); });
        els.conf.addEventListener('input', function () {
            $('vsConfVal').textContent = Math.round(els.conf.value * 100) + '%';
        });
        initEngine();
    });
})();
