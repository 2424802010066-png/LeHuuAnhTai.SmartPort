/* ============================================================
   TRỢ LÝ NGHIỆP VỤ — khung chat ở khu quản trị

   Khac voi PortBot o trang cong khai: PortBot TRA LOI cau hoi cua khach,
   con tro ly nay LAM VIEC cho nhan vien — no goi thang nghiep vu he thong.

   Vi the co mot buoc ma PortBot khong co: viec dung tien thi hien the
   XAC NHAN, nguoi phai bam dong y roi may moi lam.
   ============================================================ */
(function () {
    'use strict';

    var fab = document.getElementById('tlFab');
    if (!fab) return;                       // trang khong nhung khung chat

    var panel = document.getElementById('tlPanel');
    var body = document.getElementById('tlBody');
    var input = document.getElementById('tlInput');
    var send = document.getElementById('tlSend');
    var close = document.getElementById('tlClose');
    var typing = document.getElementById('tlTyping');
    var goc = fab.getAttribute('data-goc') || '';

    // ---------- mo / dong ----------
    function moPanel(mo) {
        panel.classList.toggle('tl-open', mo);
        fab.classList.toggle('tl-fab-an', mo);
        if (mo) setTimeout(function () { input.focus(); }, 220);
    }
    fab.addEventListener('click', function () { moPanel(true); });
    close.addEventListener('click', function () { moPanel(false); });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && panel.classList.contains('tl-open')) moPanel(false);
    });

    // ---------- ve bong bong ----------
    function bong(ai, html) {
        var d = document.createElement('div');
        d.className = 'tl-bong tl-' + ai;
        d.innerHTML = html;
        body.insertBefore(d, typing);
        body.scrollTop = body.scrollHeight;
        return d;
    }

    function veKetQua(kq) {
        var h = kq.traLoi || 'Không có kết quả.';

        if (kq.congCu)
            h = '<div class="tl-congcu"><i class="bi bi-tools"></i> ' + kq.congCu + '</div>' + h;

        if (kq.lienKets && kq.lienKets.length) {
            h += '<div class="tl-lienket">';
            kq.lienKets.forEach(function (l) {
                // "#goi:<cau lenh>" la lenh noi bo — bam vao thi tro ly tu hoi tiep
                // cau do, thay vi mo trang. Dung cho "Duyệt BK-...", "Đóng #3"...
                if (l.url && l.url.indexOf('#goi:') === 0) {
                    var lenh = l.url.substring(5).replace(/"/g, '&quot;');
                    h += '<button type="button" class="tl-nut tl-nut-goi" data-goi="' + lenh
                       + '"><i class="bi ' + (l.icon || 'bi-arrow-right')
                       + '"></i> ' + l.nhan + '</button>';
                } else {
                    h += '<a class="tl-nut" href="' + l.url + '"><i class="bi '
                       + (l.icon || 'bi-arrow-right') + '"></i> ' + l.nhan + '</a>';
                }
            });
            h += '</div>';
        }

        if (kq.canXacNhan) {
            var x = kq.canXacNhan;
            h += '<div class="tl-xacnhan">'
               + '<div class="tl-xn-hoi"><i class="bi bi-exclamation-triangle-fill"></i> ' + x.moTa + '</div>'
               + (x.canhBao ? '<div class="tl-xn-canhbao">' + x.canhBao + '</div>' : '')
               + '<div class="tl-xn-nut">'
               + '<button type="button" class="tl-dongy" data-congcu="' + x.congCu
               + '" data-thamso="' + (x.thamSo || '') + '">Đồng ý</button>'
               + '<button type="button" class="tl-thoi">Thôi</button>'
               + '</div></div>';
        }

        bong('bot', h);
    }

    // ---------- goi may chu ----------
    function dangGo(bat) {
        typing.style.display = bat ? 'flex' : 'none';
        if (bat) body.scrollTop = body.scrollHeight;
    }

    function token() {
        var e = document.querySelector('input[name="__RequestVerificationToken"]');
        return e ? e.value : '';
    }

    /* Máy chủ trả trang lỗi HTML chứ không phải JSON khi phiên hết hạn hoặc
       token chống giả mạo không còn hợp lệ. Nếu cứ gọi r.json() thì sẽ ném lỗi
       phân tích và người dùng nhận thông báo sai là "mất mạng" — trong khi thật
       ra chỉ cần tải lại trang. Vì vậy phải phân biệt hai trường hợp. */
    function docJson(r) {
        if (r.ok) return r.json();
        var e = new Error('http');
        e.ma = r.status;
        throw e;
    }

    function baoLoi(e) {
        if (e && (e.ma === 401 || e.ma === 403 || e.ma === 404 || e.ma === 400))
            bong('bot', 'Phiên đăng nhập đã hết hạn. '
                      + '<a href="javascript:location.reload()">Tải lại trang</a> rồi thử lại.');
        else
            bong('bot', 'Không kết nối được máy chủ. Kiểm tra lại mạng rồi thử lần nữa.');
    }

    function hoi(cau) {
        bong('toi', cau.replace(/</g, '&lt;'));
        dangGo(true);
        var fd = new FormData();
        fd.append('cauHoi', cau);
        fetch(goc + '/Admin/TroLy/Hoi', { method: 'POST', body: fd })
            .then(docJson)
            .then(function (kq) { dangGo(false); veKetQua(kq); })
            .catch(function (e) { dangGo(false); baoLoi(e); });
    }

    function thucHien(congCu, thamSo, nut) {
        nut.disabled = true;
        nut.textContent = 'Đang làm…';
        dangGo(true);
        var fd = new FormData();
        fd.append('congCu', congCu);
        fd.append('thamSo', thamSo);
        fd.append('__RequestVerificationToken', token());
        fetch(goc + '/Admin/TroLy/ThucHien', { method: 'POST', body: fd })
            .then(docJson)
            .then(function (kq) {
                dangGo(false);
                var hop = nut.closest('.tl-xacnhan');
                if (hop) hop.innerHTML = '<div class="tl-xn-xong"><i class="bi bi-check-circle-fill"></i> Đã thực hiện</div>';
                veKetQua(kq);
            })
            .catch(function (e) {
                dangGo(false);
                nut.disabled = false;
                nut.textContent = 'Đồng ý';
                baoLoi(e);
            });
    }

    // ---------- bat su kien ----------
    function gui() {
        var v = input.value.trim();
        if (!v) return;
        input.value = '';
        hoi(v);
    }
    send.addEventListener('click', gui);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); gui(); }
    });

    // uy quyen su kien — cac nut duoc ve dong sau khi co ket qua
    body.addEventListener('click', function (e) {
        var dy = e.target.closest('.tl-dongy');
        if (dy) { thucHien(dy.dataset.congcu, dy.dataset.thamso, dy); return; }

        var th = e.target.closest('.tl-thoi');
        if (th) {
            var hop = th.closest('.tl-xacnhan');
            if (hop) hop.innerHTML = '<div class="tl-xn-thoi">Đã bỏ qua.</div>';
            return;
        }

        var goi = e.target.closest('[data-goi]');
        if (goi) { hoi(goi.dataset.goi); return; }

        var g = e.target.closest('.tl-goiy');
        if (g) { hoi(g.textContent.trim()); }
    });
})();
