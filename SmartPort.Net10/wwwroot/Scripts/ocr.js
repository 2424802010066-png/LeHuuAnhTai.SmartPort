/* ============================================================
   OCR DEEP LEARNING — Doc so container tu anh (Tesseract.js LSTM)
   Mo hinh mang no-ron hoi quy LSTM chay ngay trong trinh duyet.
   ============================================================ */
(function () {
    var VEN = (window.APP_ROOT || '/') + 'Scripts/vendor/';
    var TESS_CDN = VEN + 'tess/tesseract.min.js';   // LOCAL — khong can CDN
    var worker = null, loading = false;

    function $(id) { return document.getElementById(id); }

    function loadScript(src) {
        return new Promise(function (ok, err) {
            var s = document.createElement('script');
            s.src = src; s.onload = ok; s.onerror = err;
            document.head.appendChild(s);
        });
    }

    function setProgress(p, label) {
        var bar = $('ocrBar');
        if (!bar) return;
        bar.style.width = Math.round(p * 100) + '%';
        $('ocrProgLabel').textContent = label || '';
    }

    function status(html) { $('ocrStatus').innerHTML = html; }

    var workerHua = null;                      // promise dung chung — goi lan 2 khi
    function ensureWorker() {                  // dang tai se CHO chu khong bao loi ao
        if (workerHua) return workerHua;
        status('<span class="spinner-border spinner-border-sm me-1"></span>Đang tải mô hình OCR (mạng LSTM ~10MB, lần đầu hơi lâu)...');
        workerHua = loadScript(TESS_CDN)
            .then(function () {
                return Tesseract.createWorker('eng', 1, {
                    workerPath: VEN + 'tess/worker.min.js',
                    corePath: VEN + 'tess-core',
                    langPath: VEN + 'tessdata',
                    logger: function (m) {
                        if (m.status === 'recognizing text') setProgress(m.progress, 'Đang đọc ký tự... ' + Math.round(m.progress * 100) + '%');
                        else setProgress(m.progress || 0, m.status);
                    }
                });
            })
            .then(function (w) {
                worker = w;
                return worker.setParameters({
                    tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 '
                }).then(function () { return worker; });
            })
            .catch(function (e) { workerHua = null; throw e; });
        return workerHua;
    }

    /* KIEM TRA SO ISO 6346 — chu so thu 11 la CHECK DIGIT tinh tu 10 ky tu
       dau (A=10, B=12... bo qua boi so cua 11; nhan trong so 2^vi_tri, mod 11).
       OCR doc nham 1 ky tu la checksum lech ngay -> chan ma "hop le gia". */
    var GT6346 = { A: 10, B: 12, C: 13, D: 14, E: 15, F: 16, G: 17, H: 18, I: 19, J: 20, K: 21, L: 23, M: 24, N: 25, O: 26, P: 27, Q: 28, R: 29, S: 30, T: 31, U: 32, V: 34, W: 35, X: 36, Y: 37, Z: 38 };
    function dungChuan6346(code) {
        if (!/^[A-Z]{4}\d{7}$/.test(code)) return false;
        var sum = 0;
        for (var i = 0; i < 10; i++) {
            var ch = code.charAt(i);
            var v = ch >= '0' && ch <= '9' ? +ch : GT6346[ch];
            sum += v * Math.pow(2, i);
        }
        return (sum % 11) % 10 === +code.charAt(10);
    }

    // Tien xu ly: phong to + grayscale + tang tuong phan de mang doc chinh xac hon
    function prep(source) {
        var w = source.naturalWidth || source.videoWidth || source.width;
        var h = source.naturalHeight || source.videoHeight || source.height;
        var scale = Math.max(1, Math.min(2.2, 1300 / w));
        var c = document.createElement('canvas');
        c.width = Math.round(w * scale);
        c.height = Math.round(h * scale);
        var x = c.getContext('2d');
        x.imageSmoothingEnabled = true;
        x.drawImage(source, 0, 0, c.width, c.height);
        var d = x.getImageData(0, 0, c.width, c.height), p = d.data;
        for (var i = 0; i < p.length; i += 4) {
            var g = 0.299 * p[i] + 0.587 * p[i + 1] + 0.114 * p[i + 2];
            g = (g - 128) * 1.35 + 128;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            p[i] = p[i + 1] = p[i + 2] = g;
        }
        x.putImageData(d, 0, 0);
        return c;
    }

    function scan(source) {
        status('<span class="spinner-border spinner-border-sm me-1"></span>🧠 Mạng nơ-ron đang phân tích ảnh...');
        $('ocrResult').style.display = 'none';
        return ensureWorker()
            .then(function (w) { return w.recognize(prep(source)); })
            .then(function (res) {
                setProgress(1, 'Hoàn tất');
                var raw = (res.data.text || '').toUpperCase();
                var clean = raw.replace(/[^A-Z0-9]/g, '');
                var conf = Math.round(res.data.confidence || 0);
                // Lay TAT CA ung vien 4 chu + 7 so, UU TIEN ma qua duoc checksum
                // ISO 6346 (neu nhieu chuoi trong anh, chon dung ma container that)
                var cacMa = clean.match(/[A-Z]{4}\d{7}/g) || [];
                var code = null, chuan = false;
                for (var im = 0; im < cacMa.length; im++) {
                    if (dungChuan6346(cacMa[im])) { code = cacMa[im]; chuan = true; break; }
                }
                if (!code && cacMa.length) code = cacMa[0];
                var m = code;

                $('ocrResult').style.display = '';
                $('ocrRawText').textContent = raw.trim().substring(0, 220) || '(không đọc được ký tự nào)';
                if (m) {
                    var nhan6346 = chuan
                        ? ' <span class="badge bg-success ms-2">✔ checksum ISO 6346 khớp</span>'
                        : ' <span class="badge bg-warning text-dark ms-2">⚠ checksum ISO 6346 lệch — xem lại ảnh</span>';
                    $('ocrCode').innerHTML = '<i class="bi bi-upc-scan me-2"></i>' + code +
                        ' <span class="badge bg-success ms-2">độ tin cậy ' + conf + '%</span>' + nhan6346;
                    $('ocrGo').style.display = '';
                    $('ocrGo').onclick = function () {
                        window.location = window.APP_ROOT + 'Tracking?soCont=' + code;
                    };
                    var inp = document.querySelector('input[name="soCont"]');
                    if (inp) inp.value = code;
                    status('✅ AI đã đọc được số container! Bấm "Tra cứu ngay" để xem thông tin.');
                } else {
                    $('ocrCode').innerHTML = '<i class="bi bi-exclamation-triangle me-2"></i>Chưa nhận ra mã container hợp lệ (4 chữ + 7 số)';
                    $('ocrGo').style.display = 'none';
                    status('Ảnh chưa rõ số container — thử ảnh chụp thẳng, đủ sáng, chữ to. (Văn bản AI đọc được hiển thị bên dưới.)');
                }
            })
            .catch(function (e) {
                console.error(e);
                status('❌ Lỗi OCR: ' + (e.message || 'không xác định') + ' — kiểm tra kết nối mạng rồi thử lại.');
            });
    }

    // Anh demo: tu ve "container" bang canvas de ai cung thu duoc ngay
    function demoImage() {
        var c = document.createElement('canvas');
        c.width = 900; c.height = 340;
        var x = c.getContext('2d');
        x.fillStyle = '#b45309';                       // vo container mau cam dat
        x.fillRect(0, 0, 900, 340);
        x.fillStyle = '#92400e';
        for (var i = 0; i < 900; i += 56) x.fillRect(i, 0, 22, 340);   // song ton
        x.fillStyle = '#fff';
        x.fillRect(60, 90, 780, 160);                  // bang so trang, sach
        x.fillStyle = '#000';
        x.font = '700 92px "Courier New", monospace';  // net thanh, cach deu — de OCR
        x.fillText('MSCU 2201417', 95, 200);
        var img = $('ocrPreview');
        img.src = c.toDataURL('image/png');
        img.style.display = '';
        scan(c);
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (!$('ocrFile')) return;
        $('ocrFile').addEventListener('change', function () {
            var f = this.files[0];
            if (!f) return;
            var rd = new FileReader();
            rd.onload = function () {
                var img = $('ocrPreview');
                // Gan onload TRUOC khi doi src + tu go sau 1 lan chay — khong de
                // handler cu "song sot" gay quet trung khi bam demo sau do
                img.onload = function () { img.onload = null; scan(img); };
                img.src = rd.result;
                img.style.display = '';
            };
            rd.readAsDataURL(f);
            this.value = '';                   // chon lai CUNG file van chay
        });
        $('ocrDemo').addEventListener('click', demoImage);
        if (location.search.indexOf('auto=1') >= 0) setTimeout(demoImage, 400);
    });

    window.SP_OCR = { scan: scan, demo: demoImage };
})();
