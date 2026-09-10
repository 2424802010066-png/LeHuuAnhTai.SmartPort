/* ============================================================
   WOW EFFECTS — dieu khien hieu ung cao cap (site public)
   ============================================================ */
(function () {
    'use strict';
    var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    /* ---------- 1. Thanh tien trinh cuon + nut ve dau ---------- */
    function initProgress() {
        var bar = document.createElement('div');
        bar.id = 'wowProgress';
        document.body.appendChild(bar);

        var btn = document.createElement('button');
        btn.id = 'btnWowTop';
        btn.type = 'button';
        btn.title = 'Về đầu trang';
        btn.innerHTML =
            '<svg viewBox="0 0 52 52"><circle cx="26" cy="26" r="24"></circle></svg>' +
            '<i class="bi bi-arrow-up"></i>';
        document.body.appendChild(btn);
        btn.addEventListener('click', function () {
            window.scrollTo({ top: 0, behavior: reduced ? 'auto' : 'smooth' });
        });

        var circle = btn.querySelector('circle');
        var ticking = false;
        function onScroll() {
            if (ticking) return;
            ticking = true;
            requestAnimationFrame(function () {
                var h = document.documentElement;
                var max = h.scrollHeight - h.clientHeight;
                var p = max > 0 ? h.scrollTop / max : 0;
                bar.style.width = (p * 100) + '%';
                circle.style.setProperty('--p', String(151 - 151 * p));
                btn.classList.toggle('show', h.scrollTop > 500);
                ticking = false;
            });
        }
        window.addEventListener('scroll', onScroll, { passive: true });
        onScroll();
    }

    /* ---------- 2. Spotlight theo chuot tren hero ---------- */
    function initSpotlight() {
        var hero = document.querySelector('.hero');
        if (!hero || reduced) return;
        var spot = document.createElement('div');
        spot.className = 'wow-spot';
        // dat sau slides nhung truoc noi dung (container co z-index 2)
        hero.insertBefore(spot, hero.querySelector('.container'));
        hero.addEventListener('mousemove', function (e) {
            var r = hero.getBoundingClientRect();
            spot.style.setProperty('--mx', ((e.clientX - r.left) / r.width * 100) + '%');
            spot.style.setProperty('--my', ((e.clientY - r.top) / r.height * 100) + '%');
        });
    }

    /* ---------- 3. Tilt 3D cho the ---------- */
    function initTilt() {
        if (reduced || !window.matchMedia('(hover: hover)').matches) return;
        var cards = document.querySelectorAll('.ai-card, .card-stats, .svc-card, .news-card');
        var MAX = 7;   // do nghieng toi da (degree)
        cards.forEach(function (c) {
            c.classList.add('wow-tilt');
            var raf = null;
            c.addEventListener('mousemove', function (e) {
                if (raf) return;
                raf = requestAnimationFrame(function () {
                    var r = c.getBoundingClientRect();
                    var px = (e.clientX - r.left) / r.width - 0.5;
                    var py = (e.clientY - r.top) / r.height - 0.5;
                    c.classList.add('tilting');
                    c.style.transform =
                        'perspective(700px) rotateY(' + (px * MAX) + 'deg) rotateX(' + (-py * MAX) + 'deg) translateY(-4px)';
                    raf = null;
                });
            });
            c.addEventListener('mouseleave', function () {
                c.classList.remove('tilting');
                c.style.transform = '';
            });
        });
    }

    /* ---------- 4. Anh sang quet cho anh thu vien / tin tuc ---------- */
    function initShine() {
        document.querySelectorAll('.glib-item, .news-card .news-thumb, .gallery-item').forEach(function (el) {
            el.classList.add('wow-shine');
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initProgress();
        initSpotlight();
        initTilt();
        initShine();
    });
})();

/* ---------- 7. DAI ANH CANG THAT truoc footer ----------
   Banner ANH THAT cang container nhin tu tren cao (luu local) + lop phu
   toi + chip chu thich — tu chen vao MOI trang public, thay cho tau/
   hai au ve hoat hinh ban cu (nguoi dung muon do hoa CHAN THAT). */
(function () {
    var footer = document.querySelector('.footer');
    if (!footer || document.querySelector('.sea-divider')) return;
    var d = document.createElement('div');
    d.className = 'sea-divider';
    d.setAttribute('aria-hidden', 'true');
    d.innerHTML = '<div class="sdv-img"></div><div class="sdv-mo"></div>' +
        '<span class="sdv-chip">⚓ CẢNG HOẠT ĐỘNG 24/7 · GIÁM SÁT BẰNG AI</span>';
    footer.parentNode.insertBefore(d, footer);
})();

/* ---------- 8. DOI TAU MINI tuan tra 2 ben le trong ----------
   Man hinh rong co 2 cot le trong ben ngoai noi dung — tha vao do
   nhung con tau that thu nho (anh tau SMARTPORT da tach nen) chay
   QUA LAI trong lan rieng: troi ngang, bong benh, toi bien thi quay
   dau. Chi hien khi man hinh >= 1500px, khong de len noi dung. */
(function () {
    var footer = document.querySelector('.footer');
    if (!footer || document.querySelector('.doi-tau')) return;
    var ROOT = window.APP_ROOT || '/';
    var anh = ROOT + 'Content/img/tau-noi.png?v=1';
    // [lan trai/phai, top(vh), rong(px), thoi gian 1 vong(s), tre(s), mo]
    var cfg = [
        ['trai', 24, 120, 34, 0, .55],
        ['trai', 52, 78, 46, -18, .38],
        ['trai', 76, 100, 40, -30, .48],
        ['phai', 30, 105, 38, -10, .5],
        ['phai', 58, 130, 32, -24, .58],
        ['phai', 82, 72, 48, -5, .36]
    ];
    ['trai', 'phai'].forEach(function (ben) {
        var lan = document.createElement('div');
        lan.className = 'doi-tau dt-' + ben;
        lan.setAttribute('aria-hidden', 'true');
        var html = '';
        for (var i = 0; i < cfg.length; i++) {
            var c = cfg[i];
            if (c[0] !== ben) continue;
            html += '<span class="dt-tau" style="top:' + c[1] + 'vh;width:' + c[2] +
                'px;--duong:' + (240 - c[2]) + 'px;animation-duration:' + c[3] +
                's;animation-delay:' + c[4] + 's;opacity:' + c[5] + ';">' +
                '<img src="' + anh + '" alt="" style="animation-delay:' + (c[4] * 0.6) + 's;" />' +
                '<i class="dt-song"></i></span>';
        }
        lan.innerHTML = html;
        document.body.appendChild(lan);
    });
})();

/* ---------- 9. HAI AU luon troi + PHAO LUONG nhay den ----------
   Them "su song" cho khoang trong: 3 con hai au bay ngang man hinh
   (2 xuoi 1 nguoc, vo canh lien tuc), va cuoi moi lan tau co 1 phao
   luong bong benh nhay den — dung chuan hang hai IALA: phao XANH
   mep trai, phao DO mep phai cua luong vao cang. */
(function () {
    var footer = document.querySelector('.footer');
    if (!footer || document.querySelector('.bau-troi')) return;
    var troi = document.createElement('div');
    troi.className = 'bau-troi';
    troi.setAttribute('aria-hidden', 'true');
    troi.innerHTML =
        '<span class="ba-chim" style="top:12vh;animation-duration:55s;animation-delay:-12s;"><i></i></span>' +
        '<span class="ba-chim nho" style="top:18vh;animation-duration:74s;animation-delay:-40s;"><i></i></span>' +
        '<span class="ba-chim nguoc nho" style="top:24vh;animation-duration:63s;animation-delay:-25s;"><i></i></span>';
    document.body.appendChild(troi);
    document.querySelectorAll('.doi-tau').forEach(function (lan) {
        var phao = document.createElement('span');
        phao.className = 'dt-phao' + (lan.className.indexOf('dt-phai') >= 0 ? ' do' : '');
        phao.innerHTML = '<i class="dp-den"></i><i class="dp-than"></i>';
        lan.appendChild(phao);
    });
})();
