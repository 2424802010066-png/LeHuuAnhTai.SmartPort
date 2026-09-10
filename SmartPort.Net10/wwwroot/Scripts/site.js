/* SmartPort - script chung */
(function () {
    // Che do chup anh tai lieu: ?shot=1 -> hien het reveal, tat animation, an nut noi
    if (location.search.indexOf('shot=1') >= 0) {
        var st = document.createElement('style');
        st.textContent = '[data-reveal]{opacity:1!important;transform:none!important;transition:none!important}' +
            '*,*::before,*::after{animation:none!important;transition:none!important}' +
            '#pbFab,.zalo-fab,.btn-top,.scroll-progress{display:none!important}' +
            '.marquee-track{transform:none!important}';
        document.documentElement.appendChild(st);
        document.addEventListener('DOMContentLoaded', function () {
            document.querySelectorAll('[data-reveal]').forEach(function (e) { e.classList.add('revealed'); });
        });
    }

    // Tu dong an alert noi sau 5s
    document.querySelectorAll('.alert-float').forEach(function (el) {
        setTimeout(function () {
            el.classList.remove('show');
            setTimeout(function () { el.remove(); }, 400);
        }, 5000);
    });

    // Navbar do bong khi cuon
    var nav = document.querySelector('.navbar-sp');
    if (nav) {
        window.addEventListener('scroll', function () {
            nav.classList.toggle('scrolled', window.scrollY > 30);
        }, { passive: true });
    }

    // Hieu ung dem so (trang chu)
    var nums = document.querySelectorAll('.count-up');
    if (nums.length && 'IntersectionObserver' in window) {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                if (!en.isIntersecting) return;
                var el = en.target;
                io.unobserve(el);
                var target = parseInt(el.getAttribute('data-target') || '0', 10);
                var dur = 1200, start = null;
                function step(ts) {
                    if (!start) start = ts;
                    var p = Math.min((ts - start) / dur, 1);
                    var eased = 1 - Math.pow(1 - p, 3);
                    el.textContent = Math.round(target * eased).toLocaleString('vi-VN');
                    if (p < 1) requestAnimationFrame(step);
                }
                requestAnimationFrame(step);
            });
        }, { threshold: 0.4 });
        nums.forEach(function (el) { io.observe(el); });
    }

    // Reveal khi cuon toi (data-reveal)
    var reveals = document.querySelectorAll('[data-reveal]');
    if (reveals.length && 'IntersectionObserver' in window) {
        var ro = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                if (!en.isIntersecting) return;
                var el = en.target;
                var delay = parseInt(el.getAttribute('data-delay') || '0', 10);
                el.style.transitionDelay = delay + 'ms';
                el.classList.add('revealed');
                ro.unobserve(el);
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });
        reveals.forEach(function (el) { ro.observe(el); });
    } else {
        reveals.forEach(function (el) { el.classList.add('revealed'); });
    }

    // Marquee: nhan doi noi dung de chay vong lien mach
    document.querySelectorAll('.marquee-track').forEach(function (track) {
        var html = track.innerHTML;
        track.innerHTML = html + html;
        var itemCount = track.children.length / 2;
        var dur = Math.max(18, itemCount * 5);
        track.style.setProperty('--dur', dur + 's');
    });

    // ----- Che do toi / sang -----
    var themeBtn = document.getElementById('themeToggle');
    function applyThemeIcon() {
        if (!themeBtn) return;
        var dark = document.documentElement.getAttribute('data-theme') === 'dark';
        themeBtn.innerHTML = dark ? '<i class="bi bi-sun-fill"></i>' : '<i class="bi bi-moon-stars-fill"></i>';
        themeBtn.title = dark ? 'Chuyển chế độ sáng' : 'Chuyển chế độ tối';
    }
    if (themeBtn) {
        applyThemeIcon();
        themeBtn.addEventListener('click', function () {
            var dark = document.documentElement.getAttribute('data-theme') === 'dark';
            var next = dark ? 'light' : 'dark';
            document.documentElement.setAttribute('data-theme', next);
            try { localStorage.setItem('sp-theme', next); } catch (e) { }
            applyThemeIcon();
        });
    }

    // ----- Hero slider anh that tu chay -----
    var slides = document.querySelectorAll('.hero-slide');
    var dots = document.querySelectorAll('.hero-dots button');
    if (slides.length > 1) {
        var cur = 0, timer;
        function show(i) {
            slides[cur].classList.remove('active');
            if (dots[cur]) dots[cur].classList.remove('active');
            cur = (i + slides.length) % slides.length;
            slides[cur].classList.add('active');
            if (dots[cur]) dots[cur].classList.add('active');
        }
        function autoNext() { timer = setInterval(function () { show(cur + 1); }, 5500); }
        dots.forEach(function (d, i) {
            d.addEventListener('click', function () { clearInterval(timer); show(i); autoNext(); });
        });
        autoNext();
    }

    // ----- Thanh tien trinh cuon + nut len dau trang -----
    var prog = document.getElementById('scrollProgress');
    var btnTop = document.getElementById('btnTop');
    if (prog || btnTop) {
        window.addEventListener('scroll', function () {
            var h = document.documentElement.scrollHeight - window.innerHeight;
            if (prog && h > 0) prog.style.width = (window.scrollY / h * 100) + '%';
            if (btnTop) btnTop.classList.toggle('show', window.scrollY > 500);
        }, { passive: true });
    }
    if (btnTop) btnTop.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: 'smooth' }); });

    // ----- Tilt 3D theo chuot (chi may co chuot) -----
    if (window.matchMedia && matchMedia('(hover: hover)').matches) {
        document.querySelectorAll('.ai-card, .photo-tile, .tst-card, .svc-card, .card-stats').forEach(function (el) {
            el.classList.add('tiltable');
            el.addEventListener('mousemove', function (e) {
                var r = el.getBoundingClientRect();
                var x = (e.clientX - r.left) / r.width - .5;
                var y = (e.clientY - r.top) / r.height - .5;
                el.style.transform = 'perspective(750px) rotateY(' + (x * 6).toFixed(2) + 'deg) rotateX(' + (-y * 6).toFixed(2) + 'deg) translateY(-5px)';
            });
            el.addEventListener('mouseleave', function () { el.style.transform = ''; });
        });

        // ----- Parallax hero art theo chuot -----
        var hero = document.querySelector('.hero');
        var heroArt = document.querySelector('.hero-art');
        if (hero && heroArt) {
            hero.addEventListener('mousemove', function (e) {
                var cx = e.clientX / window.innerWidth - .5;
                var cy = e.clientY / window.innerHeight - .5;
                heroArt.style.transform = 'translate(' + (cx * 16).toFixed(1) + 'px,' + (cy * 10).toFixed(1) + 'px)';
            });
            hero.addEventListener('mouseleave', function () { heroArt.style.transform = ''; });
        }
    }
})();
