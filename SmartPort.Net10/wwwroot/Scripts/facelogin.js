/* ============================================================
   FACE LOGIN — SmartPort (JS thuan, khong can thu vien)
   ------------------------------------------------------------
   Phan 1: MODAL QUET tren trang Dang nhap
     idle -> scanning (gui 1 frame/1.2s toi /Account/FaceLogin)
          -> success (tick xanh + fade-out + redirect)
          -> error   (X do + nut thu lai / dung mat khau)
   Phan 2: TRANG CAI DAT /Account/FaceSetup
     chup 3 goc mat -> POST /Account/FaceEnroll ; nut xoa dang ky
   ============================================================ */
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    var ROOT = window.APP_ROOT || '/';

    /* ---------------- CAMERA DUNG CHUNG ---------------- */
    var stream = null, hidCanvas = null;

    function camStart(videoEl) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            return Promise.reject(new Error('Trình duyệt chỉ cho phép camera trên HTTPS/localhost — hãy mở site qua https://'));
        }
        return navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
            audio: false
        }).then(function (s) {
            stream = s;
            videoEl.srcObject = s;
            return videoEl.play();
        });
    }
    function camStop(videoEl) {
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        if (videoEl) videoEl.srcObject = null;
    }
    /** Chup frame hien tai -> base64 JPEG.
        TOI UU: canh dai 640px + chat luong 0.9 — khop dung det_size cua
        ArcFace server (640), giup bat mat nho/xa tot hon han ban 480. */
    function camCapture(videoEl) {
        if (!videoEl || videoEl.readyState < 2) return null;
        if (!hidCanvas) hidCanvas = document.createElement('canvas');
        var MAX = 640,
            sc = Math.min(1, MAX / Math.max(videoEl.videoWidth, videoEl.videoHeight));
        hidCanvas.width = Math.round(videoEl.videoWidth * sc);
        hidCanvas.height = Math.round(videoEl.videoHeight * sc);
        hidCanvas.getContext('2d').drawImage(videoEl, 0, 0, hidCanvas.width, hidCanvas.height);
        return hidCanvas.toDataURL('image/jpeg', 0.9);
    }
    /** Lay anti-forgery token tu form co san tren trang */
    function getToken() {
        var inp = document.querySelector('input[name="__RequestVerificationToken"]');
        return inp ? inp.value : '';
    }
    /** POST dang FormData -> JSON */
    function postForm(url, fields) {
        var fd = new FormData();
        fd.append('__RequestVerificationToken', getToken());
        Object.keys(fields).forEach(function (k) { fd.append(k, fields[k]); });
        return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); });
    }

    /* ============================================================
       PHAN 1 — MODAL QUET TREN TRANG DANG NHAP
       ============================================================ */
    function initLoginModal() {
        var btnOpen = $('btnFaceLogin');
        if (!btnOpen) return;                       // khong phai trang login

        var overlay = $('faceModal'), ring = $('faceRing'), video = $('faceVideo'),
            fxWrap = $('faceFx'), result = $('faceResult'), statusEl = $('faceStatus'),
            btnScan = $('btnFaceScan'), btnCancel = $('btnFaceCancel'),
            btnRetry = $('btnFaceRetry'), btnFallback = $('btnFaceFallback');
        var timer = null, busy = false, fails = 0;
        var MAX_FAILS = 14, INTERVAL = 1100;      // nhieu lan hon vi can bo phieu 2 khung

        function setPhase(phase, msg, conf) {
            ring.className = 'face-ring ' + (phase === 'success' ? 'ok' : phase === 'error' ? 'err' : phase === 'idle' ? 'idle' : '');
            fxWrap.style.display = (phase === 'scanning') ? '' : 'none';
            result.style.display = (phase === 'success' || phase === 'error') ? 'flex' : 'none';
            result.className = 'face-result ' + (phase === 'success' ? 'ok' : 'err');
            $('faceBadgeIcon').textContent = phase === 'success' ? '✔' : '✕';
            statusEl.className = 'face-status ' + (phase === 'success' ? 'ok' : phase === 'error' ? 'err' : '');
            statusEl.innerHTML = msg +
                (conf != null ? '<span class="fs-conf">Độ khớp: ' + conf + '%</span>' : '');
            btnScan.style.display = (phase === 'idle') ? '' : 'none';
            btnCancel.style.display = (phase === 'scanning') ? '' : 'none';
            btnRetry.style.display = btnFallback.style.display = (phase === 'error') ? '' : 'none';
        }

        function open() {
            overlay.style.display = 'flex';
            fails = 0;
            setPhase('idle', 'Đưa khuôn mặt vào giữa vòng tròn rồi bấm <b>Bắt đầu quét</b>.');
            // Kiem tra dich vu AI truoc
            fetch(ROOT + 'Account/FaceCheck', { credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (!d.alive) setPhase('error', '⚠️ Đăng nhập khuôn mặt tạm không khả dụng.<br/>Bạn hãy dùng mật khẩu, hoặc khởi động dịch vụ AI rồi bấm Thử lại.');
                }).catch(function () { });
            camStart(video).catch(function () {
                setPhase('error', 'Không truy cập được camera — hãy cấp quyền camera cho trình duyệt.');
            });
        }
        function close() {
            clearInterval(timer);
            camStop(video);
            overlay.style.display = 'none';
        }

        function tick() {
            if (busy) return;
            var frame = camCapture(video);
            if (!frame) return;
            busy = true;
            postForm(ROOT + 'Account/FaceLogin', { image: frame, returnUrl: overlay.getAttribute('data-return') || '' })
                .then(function (d) {
                    if (d.ok && d.status === 'success') {
                        clearInterval(timer);
                        camStop(video);
                        setPhase('success', d.message, d.confidence);
                        // Fade-out ca trang roi chuyen huong
                        setTimeout(function () {
                            document.body.style.transition = 'opacity .55s ease, transform .55s ease';
                            document.body.style.opacity = '0';
                            document.body.style.transform = 'scale(1.03)';
                            setTimeout(function () { window.location = d.redirectUrl; }, 560);
                        }, 900);
                    } else if (d.status === 'spoof' || d.status === 'service_down' ||
                               d.status === 'multi_face' || d.status === 'rate_limit') {
                        clearInterval(timer);
                        setPhase('error', d.message);
                    } else if (d.status === 'verifying' || d.status === 'challenge') {
                        // Dang tren duong ve dich — KHONG tinh la that bai
                        var them = d.status === 'challenge'
                            ? '<span class="fs-conf" style="background:#f59e0b;color:#08131f;">🔄 Hãy quay đầu nhẹ</span>'
                            : '<span class="fs-conf">Xác minh ' + (d.buoc || 1) + '/' + (d.canBuoc || 2) + ' · ' + d.confidence + '%</span>';
                        statusEl.innerHTML = d.message + them;
                    } else {
                        // quality / no_face / no_match -> hien GOI Y SUA cu the
                        fails++;
                        var phu = '';
                        if (d.confidence != null) phu += '<span class="fs-conf">Độ khớp: ' + d.confidence + '%</span>';
                        if (d.chatLuong != null) phu += '<span class="fs-conf" style="background:#0ea5e9;">Ảnh: ' + Math.round(d.chatLuong) + '/100</span>';
                        statusEl.innerHTML = d.message + phu;
                        if (fails >= MAX_FAILS) {
                            clearInterval(timer);
                            setPhase('error', 'Không nhận diện được sau nhiều lần thử — hãy thử nơi đủ sáng hoặc dùng mật khẩu.');
                        }
                    }
                })
                .catch(function () { clearInterval(timer); setPhase('error', 'Mất kết nối máy chủ.'); })
                .then(function () { busy = false; });
        }

        btnOpen.addEventListener('click', function (e) { e.preventDefault(); open(); });
        btnScan.addEventListener('click', function () {
            fails = 0;
            setPhase('scanning', 'Đang quét... giữ yên khuôn mặt trong vòng tròn');
            timer = setInterval(tick, INTERVAL);
        });
        btnCancel.addEventListener('click', function () { clearInterval(timer); setPhase('idle', 'Đưa khuôn mặt vào giữa vòng tròn rồi bấm <b>Bắt đầu quét</b>.'); });
        btnRetry.addEventListener('click', function () {
            fails = 0;
            if (!stream) camStart(video).catch(function () { });
            setPhase('scanning', 'Đang quét lại...');
            timer = setInterval(tick, INTERVAL);
        });
        btnFallback.addEventListener('click', function () {
            close();
            var u = $('inpUser'); if (u) u.focus();
        });
        $('btnFaceClose').addEventListener('click', close);
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
    }

    /* ============================================================
       PHAN 2 — TRANG CAI DAT KHUON MAT (/Account/FaceSetup)
       ============================================================ */
    function initSetupPage() {
        var video = $('fsVideo');
        if (!video) return;                          // khong phai trang setup

        var GOC = ['Nhìn THẲNG vào camera', 'Quay mặt nhẹ sang TRÁI', 'Quay mặt nhẹ sang PHẢI',
                   'NGẨNG mặt lên một chút', 'CÚI mặt xuống một chút'];
        var TEN = ['Thẳng', 'Trái', 'Phải', 'Ngẩng', 'Cúi'];
        var SO_GOC = 5, TOI_THIEU = 3;
        var shots = [null, null, null, null, null];
        var hole = $('fsHole'), guide = $('fsGuide'), qEl = $('fsQuality'),
            btnShot = $('btnFsShot'), btnSubmit = $('btnFsSubmit'), btnRemove = $('btnFsRemove'),
            msg = $('fsMsg');

        camStart(video).catch(function () {
            guide.innerHTML = '<span style="color:#fca5a5">Không truy cập được camera — hãy cấp quyền cho trình duyệt.</span>';
            btnShot.disabled = true;
        });

        function daChup() { return shots.filter(function (s) { return s; }).length; }

        function refresh() {
            var idx = shots.indexOf(null);
            for (var i = 0; i < SO_GOC; i++) {
                var cell = $('fsShot' + i);
                if (!cell) continue;
                cell.className = 'face-shot' + (shots[i] ? ' filled' : '');
                cell.innerHTML = shots[i]
                    ? '<img src="' + shots[i] + '" alt=""/><button type="button" class="fs-del" data-i="' + i + '">✕</button>'
                    : TEN[i];
            }
            document.querySelectorAll('#fsShots .fs-del').forEach(function (b) {
                b.addEventListener('click', function () { shots[+b.getAttribute('data-i')] = null; refresh(); });
            });
            if (idx === -1) {
                guide.innerHTML = '✅ Đã đủ ' + SO_GOC + ' góc — bấm <b>Hoàn tất đăng ký</b>';
                btnShot.style.display = 'none';
                btnSubmit.style.display = '';
            } else {
                guide.innerHTML = 'Góc ' + (idx + 1) + '/' + SO_GOC + ': <b style="color:#67e8f9">' + GOC[idx] + '</b>';
                btnShot.style.display = '';
                btnShot.textContent = '📸 Chụp góc ' + TEN[idx];
                // Du toi thieu 3 goc thi cho phep hoan tat som
                btnSubmit.style.display = daChup() >= TOI_THIEU ? '' : 'none';
            }
        }

        btnShot.addEventListener('click', function () {
            var img = camCapture(video);
            if (!img) return;
            // hieu ung nhay flash
            var fl = document.createElement('div');
            fl.className = 'face-flash';
            hole.appendChild(fl);
            setTimeout(function () { fl.remove(); }, 220);

            // KIEM DINH CHAT LUONG NGAY — anh xau bi tu choi kem goi y sua,
            // khong de den luc dang nhap moi phat hien mau te
            btnShot.disabled = true;
            qEl.innerHTML = '<span class="text-muted">⏳ AI đang chấm chất lượng ảnh...</span>';
            postForm(ROOT + 'Account/FaceCheckShot', { image: img })
                .then(function (d) {
                    btnShot.disabled = false;
                    if (d.ok) {
                        qEl.innerHTML = '<span style="color:#15803d;font-weight:600;">✔ Ảnh đạt chuẩn — chất lượng ' +
                            Math.round(d.chatLuong || 0) + '/100</span>';
                        var ô = shots.indexOf(null);
                        if (ô >= 0) shots[ô] = img;
                        refresh();
                    } else {
                        qEl.innerHTML = '<span style="color:#b91c1c;font-weight:600;">✕ ' + d.message + '</span>' +
                            '<div class="text-muted" style="font-size:.78rem;">Hãy sửa rồi chụp lại góc này.</div>';
                    }
                })
                .catch(function () {
                    btnShot.disabled = false;
                    qEl.innerHTML = '<span style="color:#b91c1c;">Không kết nối được dịch vụ AI.</span>';
                });
        });

        btnSubmit.addEventListener('click', function () {
            btnSubmit.disabled = true;
            btnSubmit.textContent = 'Đang xử lý AI (' + daChup() + ' ảnh)...';
            msg.innerHTML = '';
            postForm(ROOT + 'Account/FaceEnroll', {
                img1: shots[0] || '', img2: shots[1] || '', img3: shots[2] || '',
                img4: shots[3] || '', img5: shots[4] || ''
            })
                .then(function (d) {
                    msg.innerHTML = '<div class="alert ' + (d.ok ? 'alert-success' : 'alert-danger') + ' mt-3">' + d.message + '</div>';
                    if (d.ok) {
                        camStop(video);
                        setTimeout(function () { window.location.reload(); }, 1600);
                    } else {
                        btnSubmit.disabled = false;
                        btnSubmit.textContent = '✔ Hoàn tất đăng ký';
                    }
                })
                .catch(function () {
                    msg.innerHTML = '<div class="alert alert-danger mt-3">Mất kết nối máy chủ.</div>';
                    btnSubmit.disabled = false;
                    btnSubmit.textContent = '✔ Hoàn tất đăng ký';
                });
        });

        if (btnRemove) btnRemove.addEventListener('click', function () {
            if (!confirm('Xóa đăng ký khuôn mặt của bạn?')) return;
            postForm(ROOT + 'Account/FaceRemove', {}).then(function (d) {
                if (d.ok) window.location.reload();
            });
        });

        refresh();
        window.addEventListener('beforeunload', function () { camStop(video); });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initLoginModal();
        initSetupPage();
    });
})();
