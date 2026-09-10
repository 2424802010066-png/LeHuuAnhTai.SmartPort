/* ============================================================
   SMARTPORT — MODULE THANH TOAN (hieu ung + tuong tac)
   - Chon phuong thuc: highlight, ripple
   - Dem so tien tang dan
   - Dong ho dem nguoc giu don + vong tron tien trinh
   - Copy nhanh STK / so tien / noi dung CK
   - Nhap the: format so, lat the 3D preview
   - Polling trang thai giao dich (doi soat tu dong)
   ============================================================ */
(function () {
    function $(id) { return document.getElementById(id); }
    function fmtVnd(n) { return Number(n).toLocaleString('vi-VN') + ' ₫'; }

    /* ---------- 1. Chon phuong thuc thanh toan ---------- */
    function initMethods() {
        var items = document.querySelectorAll('.pay-method');
        if (!items.length) return;
        items.forEach(function (el) {
            el.addEventListener('click', function () {
                items.forEach(function (x) { x.classList.remove('active'); });
                el.classList.add('active');
                var r = el.querySelector('input[type=radio]');
                if (r) r.checked = true;
                // hieu ung nhun nhe
                el.animate(
                    [{ transform: 'scale(.985)' }, { transform: 'scale(1)' }],
                    { duration: 220, easing: 'ease-out' });
            });
        });
    }

    /* ---------- 2. Dem so tien tang dan ---------- */
    function initCountUp() {
        document.querySelectorAll('[data-count]').forEach(function (el) {
            var dich = parseInt(el.getAttribute('data-count'), 10) || 0;
            if (dich <= 0) { el.textContent = fmtVnd(0); return; }
            // setInterval + dong ho that: khong bi dung khi tab nen (rAF bi chan)
            var t0 = Date.now(), thoiGian = 900;
            var timer = setInterval(function () {
                var p = Math.min(1, (Date.now() - t0) / thoiGian);
                var ease = 1 - Math.pow(1 - p, 3);           // easeOutCubic
                el.textContent = fmtVnd(Math.round(dich * ease));
                if (p >= 1) { clearInterval(timer); el.textContent = fmtVnd(dich); }
            }, 24);
        });
    }

    /* ---------- 3. Copy nhanh ---------- */
    function initCopy() {
        document.querySelectorAll('.copyable').forEach(function (el) {
            el.addEventListener('click', function () {
                var v = el.getAttribute('data-copy') || el.textContent.trim();
                var xong = function () {
                    var cu = el.innerHTML;
                    el.innerHTML = '<span class="text-success"><i class="bi bi-check-lg"></i> Đã sao chép</span>';
                    setTimeout(function () { el.innerHTML = cu; }, 1400);
                };
                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(v).then(xong).catch(fallback);
                } else { fallback(); }
                function fallback() {
                    var ta = document.createElement('textarea');
                    ta.value = v; ta.style.position = 'fixed'; ta.style.opacity = '0';
                    document.body.appendChild(ta); ta.select();
                    try { document.execCommand('copy'); xong(); } catch (e) { }
                    document.body.removeChild(ta);
                }
            });
        });
    }

    /* ---------- 4. Dong ho dem nguoc + polling ---------- */
    function initCountdown() {
        var box = $('payBox');
        if (!box) return;
        var maTT = box.getAttribute('data-id');
        var conLai = parseInt(box.getAttribute('data-giay'), 10) || 0;
        var tong = conLai > 0 ? conLai : 1;
        var cdText = $('cdText'), cdBar = $('cdBar'), cdNote = $('cdNote');

        function ve() {
            if (!cdText) return;
            var p = Math.max(0, conLai);
            var ph = Math.floor(p / 60), gi = p % 60;
            cdText.textContent = (ph < 10 ? '0' : '') + ph + ':' + (gi < 10 ? '0' : '') + gi;
            if (cdBar) cdBar.style.width = Math.max(0, (p / tong) * 100) + '%';
            if (p <= 60) { cdText.classList.add('cd-warn'); }
            if (p <= 0) {
                cdText.textContent = '00:00';
                cdText.classList.add('cd-out');
                if (cdNote) cdNote.innerHTML = '<span class="text-danger fw-semibold">Giao dịch đã hết hiệu lực — vui lòng tạo lại.</span>';
            }
        }
        ve();
        if (cdText && conLai > 0) {
            setInterval(function () { conLai--; ve(); }, 1000);
        }

        // Polling trang thai: neu ke toan xac nhan thi tu chuyen sang bien lai
        if (maTT) {
            setInterval(function () {
                fetch(window.APP_ROOT + 'Payment/Status/' + maTT, { credentials: 'same-origin' })
                    .then(function (r) { return r.json(); })
                    .then(function (d) {
                        if (!d || !d.ok) return;
                        var badge = $('ttBadge');
                        if (badge && badge.textContent.trim() !== d.trangThai) badge.textContent = d.trangThai;
                        if (d.daThanhToan) {
                            window.location = window.APP_ROOT + 'Payment/Receipt/' + maTT;
                        }
                    }).catch(function () { });
            }, 6000);
        }
    }

    /* ---------- 5. Nhap the ngan hang (mo phong) ---------- */
    function initCard() {
        var inpCard = $('inpCard'), inpName = $('inpName'), inpExp = $('inpExp');
        if (!inpCard) return;

        inpCard.addEventListener('input', function () {
            var v = this.value.replace(/\D/g, '').substring(0, 16);
            this.value = v.replace(/(.{4})/g, '$1 ').trim();
            var hien = (v + '••••••••••••••••').substring(0, 16).replace(/(.{4})/g, '$1 ').trim();
            $('cvNum').textContent = hien;
            // doi thuong hieu theo dau so
            var brand = $('.cv-brand') ? null : document.querySelector('.cv-brand');
            if (brand) {
                if (v[0] === '4') brand.textContent = 'VISA';
                else if (v[0] === '5') brand.textContent = 'MASTERCARD';
                else if (v[0] === '9') brand.textContent = 'NAPAS';
                else brand.textContent = 'VISA';
            }
        });
        if (inpName) inpName.addEventListener('input', function () {
            $('cvName').textContent = (this.value || 'NGUYEN VAN A').toUpperCase();
        });
        if (inpExp) inpExp.addEventListener('input', function () {
            var v = this.value.replace(/\D/g, '').substring(0, 4);
            if (v.length >= 3) v = v.substring(0, 2) + '/' + v.substring(2);
            this.value = v;
            $('cvExp').textContent = v || 'MM/YY';
        });

        // Lat the 3D theo con tro
        var card = document.querySelector('.card-visual');
        if (card) {
            card.addEventListener('mousemove', function (e) {
                var r = card.getBoundingClientRect();
                var x = (e.clientX - r.left) / r.width - .5;
                var y = (e.clientY - r.top) / r.height - .5;
                card.style.transform = 'perspective(900px) rotateY(' + (x * 12) + 'deg) rotateX(' + (-y * 12) + 'deg)';
            });
            card.addEventListener('mouseleave', function () {
                card.style.transform = 'perspective(900px) rotateY(0) rotateX(0)';
            });
        }
    }

    /* ---------- 6. Chon vi dien tu ---------- */
    function initWallet() {
        var items = document.querySelectorAll('.wallet-item');
        items.forEach(function (el) {
            el.addEventListener('click', function () {
                items.forEach(function (x) { x.classList.remove('active'); });
                el.classList.add('active');
            });
        });
    }

    /* ---------- 7. Nut xac nhan: trang thai dang xu ly ---------- */
    function initConfirm() {
        var btn = $('btnConfirm');
        if (!btn) return;
        btn.form && btn.form.addEventListener('submit', function () {
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang gửi xác nhận...';
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (!window.APP_ROOT) window.APP_ROOT = '/';
        initMethods();
        initCountUp();
        initCopy();
        initCountdown();
        initCard();
        initWallet();
        initConfirm();
    });
})();
