/* ============================================================
   SMARTPORT — HIEU UNG GIAO DIEN DUNG CHUNG
   1. Count-up: cac so trong the chi so (.ps-num) dem tang dan
   2. Money rain: hieu ung to tien roi tu tren cao (trang thanh
      toan thanh cong / bien lai) — CSS tu inject, khong can file rieng
   ============================================================ */
(function () {

    /* ================= 1. COUNT-UP SO LIEU ================= */
    // Doc so dinh dang vi-VN ("24.040.000 đ", "359", "83,8%") -> dem tu 0 len
    function initCountUp() {
        var els = document.querySelectorAll('.pay-stat .ps-num');
        if (!els.length) return;

        els.forEach(function (el) {
            var raw = el.textContent.trim();
            // Tach phan so va phan duoi (don vi/ky hieu)
            var m = raw.match(/^([\d.,]+)\s*(.*)$/);
            if (!m) return;
            var numStr = m[1], suffix = m[2] ? ' ' + m[2] : '';
            var hasComma = numStr.indexOf(',') >= 0;          // so thap phan kieu vi-VN
            var target = parseFloat(numStr.replace(/\./g, '').replace(',', '.'));
            if (isNaN(target) || target <= 0) return;

            var decimals = hasComma ? (numStr.split(',')[1] || '').length : 0;
            var dur = 1100;

            function fmt(v) {
                return v.toLocaleString('vi-VN', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
            }
            // Dung setInterval + dong ho that (khong dung rAF vi tab nen bi chan
            // se lam so ket o 0) — luon ket thuc DUNG gia tri goc.
            function chay() {
                var t0 = Date.now();
                var timer = setInterval(function () {
                    var p = Math.min(1, (Date.now() - t0) / dur);
                    var ease = 1 - Math.pow(1 - p, 3);        // easeOutCubic
                    el.textContent = fmt(target * ease) + suffix;
                    if (p >= 1) { clearInterval(timer); el.textContent = fmt(target) + suffix; }
                }, 24);
            }
            el.textContent = fmt(0) + suffix;
            // chi chay khi the vao khung nhin
            if ('IntersectionObserver' in window) {
                var io = new IntersectionObserver(function (entries) {
                    entries.forEach(function (e) {
                        if (e.isIntersecting) { chay(); io.unobserve(el); }
                    });
                }, { threshold: .35 });
                io.observe(el);
                // phong ho: neu observer khong kich hoat (trinh duyet la), van chay sau 1.2s
                setTimeout(function () {
                    if (el.textContent.replace(/[^\d]/g, '') === '0' && target > 0) { chay(); try { io.unobserve(el); } catch (e) { } }
                }, 1200);
            } else {
                chay();
            }
        });
    }

    /* ================= 2. MONEY RAIN — TIEN ROI ================= */
    var CSS = '' +
        '.sp-money-rain{position:fixed;inset:0;pointer-events:none;z-index:9999;overflow:hidden;}' +
        '.sp-money{position:absolute;top:-90px;will-change:transform;animation:spMoneyFall linear forwards;filter:drop-shadow(0 4px 8px rgba(0,0,0,.22));}' +
        '.sp-money svg{display:block;animation:spMoneySway ease-in-out infinite alternate;}' +
        '@keyframes spMoneyFall{0%{transform:translateY(-90px) rotate(0deg);opacity:0;}6%{opacity:1;}92%{opacity:1;}100%{transform:translateY(108vh) rotate(var(--rot,340deg));opacity:0;}}' +
        '@keyframes spMoneySway{from{transform:translateX(calc(var(--sway,18px) * -1)) rotateY(0deg);}to{transform:translateX(var(--sway,18px)) rotateY(160deg);}}';

    function injectCss() {
        if (document.getElementById('spMoneyRainCss')) return;
        var s = document.createElement('style');
        s.id = 'spMoneyRainCss';
        s.textContent = CSS;
        document.head.appendChild(s);
    }

    // To tien SVG mini (xanh la kieu tien polymer) — nhe, khong can anh ngoai
    function billSvg(w) {
        var h = Math.round(w * 0.46);
        return '<svg width="' + w + '" height="' + h + '" viewBox="0 0 100 46" xmlns="http://www.w3.org/2000/svg">' +
            '<rect x="1.5" y="1.5" width="97" height="43" rx="6" fill="#1f9d55" stroke="#0e6b38" stroke-width="3"/>' +
            '<rect x="8" y="8" width="84" height="30" rx="4" fill="none" stroke="#7ddfa8" stroke-width="1.6" stroke-dasharray="5 3"/>' +
            '<circle cx="50" cy="23" r="11" fill="#168247"/>' +
            '<text x="50" y="29" text-anchor="middle" font-family="Arial,sans-serif" font-weight="900" font-size="16" fill="#d6ffe8">₫</text>' +
            '<circle cx="18" cy="23" r="4.5" fill="#7ddfa8" opacity=".55"/>' +
            '<circle cx="82" cy="23" r="4.5" fill="#7ddfa8" opacity=".55"/>' +
            '</svg>';
    }
    // Dong xu vang xen ke cho sinh dong
    function coinSvg(w) {
        return '<svg width="' + w + '" height="' + w + '" viewBox="0 0 40 40" xmlns="http://www.w3.org/2000/svg">' +
            '<circle cx="20" cy="20" r="18" fill="#f5c542" stroke="#c8951c" stroke-width="3"/>' +
            '<circle cx="20" cy="20" r="12.5" fill="none" stroke="#e0a92c" stroke-width="1.6"/>' +
            '<text x="20" y="26.5" text-anchor="middle" font-family="Arial,sans-serif" font-weight="900" font-size="16" fill="#8a5f0a">₫</text>' +
            '</svg>';
    }

    /**
     * Tha "mua tien": count = so to (mac dinh 34), duration = tong thoi gian (ms)
     */
    function moneyRain(count, duration) {
        injectCss();
        count = count || 34;
        duration = duration || 5200;

        var wrap = document.createElement('div');
        wrap.className = 'sp-money-rain';
        document.body.appendChild(wrap);

        for (var i = 0; i < count; i++) {
            var el = document.createElement('div');
            el.className = 'sp-money';
            var size = 30 + Math.random() * 34;               // 30-64px
            el.innerHTML = Math.random() < 0.72 ? billSvg(Math.round(size)) : coinSvg(Math.round(size * 0.6));
            el.style.left = (Math.random() * 98) + 'vw';
            el.style.setProperty('--rot', (200 + Math.random() * 340) + 'deg');
            el.style.animationDuration = (2.6 + Math.random() * 2.6) + 's';
            el.style.animationDelay = (Math.random() * (duration / 1000 - 2.4)) + 's';
            var sway = el.querySelector('svg');
            sway.style.setProperty('--sway', (10 + Math.random() * 22) + 'px');
            sway.style.animationDuration = (0.7 + Math.random() * 0.9) + 's';
            wrap.appendChild(el);
        }
        setTimeout(function () {
            if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
        }, duration + 3400);
    }
    window.spMoneyRain = moneyRain;   // cho phep goi thu cong o trang khac

    /* ================= KHOI DONG ================= */
    document.addEventListener('DOMContentLoaded', function () {
        initCountUp();

        // Tu dong mua tien khi: bien lai DA THANH TOAN (.rc-stamp)
        // hoac bat ky phan tu nao danh dau data-money-rain
        if (document.querySelector('.rc-stamp') || document.querySelector('[data-money-rain]')) {
            setTimeout(function () { moneyRain(); }, 350);
        }
    });
})();
