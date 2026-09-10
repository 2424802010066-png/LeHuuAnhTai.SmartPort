/* ============================================================
   REINFORCEMENT LEARNING — Q-LEARNING
   "Xe nâng tự học tìm đường tối ưu trong bãi container"
   ------------------------------------------------------------
   Bai toan gridworld: xe nâng xuất phát từ CỔNG, phải tới ô ĐÍCH
   (vị trí xếp container) — tránh các ô CONTAINER chắn đường.
   Agent KHÔNG được dạy đường đi; nó tự thử-sai hàng trăm episode
   và học bảng Q(s,a) qua phương trình Bellman:
       Q(s,a) += lr * ( r + gamma * max Q(s',a') - Q(s,a) )
   Kết quả: heat-map giá trị học được + đường đi tối ưu.
   ============================================================ */
(function () {
    'use strict';
    var COLS = 9, ROWS = 6, CELL = 58;
    var ACTIONS = [[0, -1], [0, 1], [-1, 0], [1, 0]];   // len, xuong, trai, phai
    var START = { x: 0, y: 5 }, GOAL = { x: 8, y: 0 };

    var grid, Q, trained = false, rewardHist = [];
    var cv, ctx, elInfo, elBtn, elBtnMap;

    function $(id) { return document.getElementById(id); }

    /* ---------------- Khoi tao ban do ---------------- */
    /** BFS: kiem tra tu START co duong di toi GOAL khong (4 huong) */
    function coDuongDi() {
        var seen = {}, hang = [[START.x, START.y]];
        seen[START.y * COLS + START.x] = 1;
        while (hang.length) {
            var p = hang.shift();
            if (p[0] === GOAL.x && p[1] === GOAL.y) return true;
            var kes = [[p[0] + 1, p[1]], [p[0] - 1, p[1]], [p[0], p[1] + 1], [p[0], p[1] - 1]];
            for (var i = 0; i < kes.length; i++) {
                var kx = kes[i][0], ky = kes[i][1], key = ky * COLS + kx;
                if (kx < 0 || kx >= COLS || ky < 0 || ky >= ROWS || grid[ky][kx] || seen[key]) continue;
                seen[key] = 1;
                hang.push([kx, ky]);
            }
        }
        return false;
    }
    function newMap() {
        // Sinh lai toi khi CHAC CHAN co duong di — ban do bi container bit kin
        // thi agent hoc 600 episode cung vo ich (loi ban cu)
        do {
            grid = [];
            for (var y = 0; y < ROWS; y++) {
                grid.push(new Array(COLS).fill(0));
            }
            // Dat ngau nhien 11 o container chan duong (tranh start/goal)
            var dat = 0;
            while (dat < 11) {
                var x = Math.floor(Math.random() * COLS), y2 = Math.floor(Math.random() * ROWS);
                if ((x === START.x && y2 === START.y) || (x === GOAL.x && y2 === GOAL.y) || grid[y2][x]) continue;
                grid[y2][x] = 1;
                dat++;
            }
        } while (!coDuongDi());
        resetQ();
        trained = false;
        rewardHist = [];
        draw(null);
        info('Bản đồ mới đã tạo — bấm <b>Huấn luyện 600 episode</b> để agent tự học.');
    }

    function resetQ() {
        Q = [];
        for (var s = 0; s < COLS * ROWS; s++) Q.push([0, 0, 0, 0]);
    }

    /* ---------------- Q-Learning ---------------- */
    function stateOf(x, y) { return y * COLS + x; }

    function stepEnv(x, y, a) {
        var nx = x + ACTIONS[a][0], ny = y + ACTIONS[a][1];
        // Ra ngoai luoi -> dung yen, phat nhe
        if (nx < 0 || nx >= COLS || ny < 0 || ny >= ROWS) return { x: x, y: y, r: -5, done: false };
        if (grid[ny][nx] === 1) return { x: x, y: y, r: -10, done: false };     // dam container
        if (nx === GOAL.x && ny === GOAL.y) return { x: nx, y: ny, r: 100, done: true };
        return { x: nx, y: ny, r: -1, done: false };                            // moi buoc ton -1
    }

    function train(episodes) {
        var lr = 0.2, gamma = 0.92;
        for (var ep = 0; ep < episodes; ep++) {
            var eps = Math.max(0.05, 1 - ep / (episodes * 0.75));   // epsilon giam dan
            var x = START.x, y = START.y, total = 0;
            for (var step = 0; step < 200; step++) {
                var s = stateOf(x, y);
                var a = Math.random() < eps
                    ? Math.floor(Math.random() * 4)
                    : argmax(Q[s]);
                var res = stepEnv(x, y, a);
                var s2 = stateOf(res.x, res.y);
                Q[s][a] += lr * (res.r + gamma * Math.max.apply(null, Q[s2]) - Q[s][a]);
                total += res.r;
                x = res.x; y = res.y;
                if (res.done) break;
            }
            rewardHist.push(total);
        }
        trained = true;
    }

    function argmax(arr) {
        var bi = 0;
        for (var i = 1; i < arr.length; i++) if (arr[i] > arr[bi]) bi = i;
        return bi;
    }

    /** Duong di toi uu theo chinh sach greedy da hoc */
    function bestPath() {
        var path = [{ x: START.x, y: START.y }];
        var x = START.x, y = START.y;
        for (var i = 0; i < 60; i++) {
            var res = stepEnv(x, y, argmax(Q[stateOf(x, y)]));
            if (res.x === x && res.y === y) break;         // ket
            x = res.x; y = res.y;
            path.push({ x: x, y: y });
            if (res.done) return path;
        }
        return null;                                        // chua hoc xong
    }

    /* ---------------- Ve canvas ---------------- */
    function draw(path) {
        ctx.clearRect(0, 0, cv.width, cv.height);
        // Gia tri max cua Q de to heatmap
        var vmax = 1;
        Q.forEach(function (qs) { var m = Math.max.apply(null, qs); if (m > vmax) vmax = m; });

        for (var y = 0; y < ROWS; y++) {
            for (var x = 0; x < COLS; x++) {
                var px = x * CELL, py = y * CELL;
                if (grid[y][x] === 1) {
                    ctx.fillStyle = '#8a5a2b';                          // container chan
                    ctx.fillRect(px + 2, py + 2, CELL - 4, CELL - 4);
                    ctx.strokeStyle = '#6b4520';
                    for (var l = 1; l < 4; l++) {
                        ctx.beginPath();
                        ctx.moveTo(px + 4, py + 2 + l * (CELL - 4) / 4);
                        ctx.lineTo(px + CELL - 4, py + 2 + l * (CELL - 4) / 4);
                        ctx.stroke();
                    }
                } else {
                    var v = Math.max(0, Math.max.apply(null, Q[stateOf(x, y)])) / vmax;
                    ctx.fillStyle = 'rgba(34, 211, 238, ' + (v * 0.55).toFixed(3) + ')';   // heatmap Q
                    ctx.fillRect(px + 2, py + 2, CELL - 4, CELL - 4);
                }
                ctx.strokeStyle = 'rgba(148,163,184,.35)';
                ctx.strokeRect(px + 2, py + 2, CELL - 4, CELL - 4);
            }
        }
        // Cong xuat phat + dich
        text('🚜', START.x, START.y);
        text('🎯', GOAL.x, GOAL.y);

        // Duong di toi uu
        if (path && path.length > 1) {
            ctx.strokeStyle = '#f59e0b';
            ctx.lineWidth = 5;
            ctx.lineJoin = 'round';
            ctx.beginPath();
            path.forEach(function (p, i) {
                var cx = p.x * CELL + CELL / 2, cy = p.y * CELL + CELL / 2;
                if (i === 0) ctx.moveTo(cx, cy); else ctx.lineTo(cx, cy);
            });
            ctx.stroke();
            ctx.lineWidth = 1;
        }
    }
    function text(t, x, y) {
        ctx.font = (CELL * 0.55) + 'px serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(t, x * CELL + CELL / 2, y * CELL + CELL / 2 + 2);
    }
    function info(html) { if (elInfo) elInfo.innerHTML = html; }

    /* ---------------- Khoi dong ---------------- */
    document.addEventListener('DOMContentLoaded', function () {
        cv = $('rlCanvas');
        if (!cv) return;
        ctx = cv.getContext('2d');
        cv.width = COLS * CELL; cv.height = ROWS * CELL;
        elInfo = $('rlInfo'); elBtn = $('btnRlTrain'); elBtnMap = $('btnRlMap');

        elBtn.addEventListener('click', function () {
            var t0 = performance.now();
            train(600);
            var ms = Math.round(performance.now() - t0);
            var path = bestPath();
            draw(path);
            var avgDau = trung(rewardHist.slice(0, 30));
            var avgCuoi = trung(rewardHist.slice(-30));
            if (path) {
                info('✅ Agent đã học xong sau <b>600 episode</b> (' + ms + 'ms): tìm được đường ' +
                     '<b>' + (path.length - 1) + ' bước</b> tới đích.<br/>' +
                     'Phần thưởng TB 30 episode đầu: <b class="text-danger">' + avgDau + '</b> → 30 episode cuối: ' +
                     '<b class="text-success">' + avgCuoi + '</b> — agent TỰ TIẾN BỘ rõ rệt nhờ thử-sai! ' +
                     '<span class="text-muted">(màu xanh đậm dần = giá trị Q học được)</span>');
            } else {
                info('⚠ Chính sách chưa hội tụ — bấm huấn luyện thêm lần nữa.');
            }
        });
        elBtnMap.addEventListener('click', newMap);

        newMap();
    });

    function trung(arr) {
        if (!arr.length) return 0;
        return Math.round(arr.reduce(function (s, v) { return s + v; }, 0) / arr.length);
    }
})();
