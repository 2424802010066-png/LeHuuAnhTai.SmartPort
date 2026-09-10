/* ============================================================
   AI LAB — HUAN LUYEN MANG NO-RON DU BAO SAN LUONG (TensorFlow.js)
   Mo hinh MLP (Multi-Layer Perceptron) hoc chuoi thoi gian TEU:
   dung cua so 3 thang truoc de du doan thang ke tiep, huan luyen
   bang thuat toan Adam ngay tren trinh duyet, ve duong loss realtime.
   ============================================================ */
(function () {
    var TF_CDN = (window.APP_ROOT || '/') + 'Scripts/vendor/tf/tf.min.js';   // LOCAL — khong can CDN
    var WIN = 3, HORIZON = 6;
    var model = null, training = false;
    var lossChart = null, mainChart = null;

    function $(id) { return document.getElementById(id); }

    function loadScript(src) {
        return new Promise(function (ok, err) {
            var s = document.createElement('script');
            s.src = src; s.onload = ok; s.onerror = err;
            document.head.appendChild(s);
        });
    }

    /* ---------- Du lieu ---------- */
    var DATA = null; // {labels:[], values:[]}
    var yMin = 0, yMax = 1;
    function norm(v) { return (v - yMin) / (yMax - yMin); }
    function denorm(v) { return v * (yMax - yMin) + yMin; }

    function nextLabels(n) {
        // sinh nhan T{m}/{yy} tiep theo tu nhan cuoi "T6/26"
        var last = DATA.labels[DATA.labels.length - 1];
        var m = parseInt(last.split('/')[0].substring(1), 10);
        var y = parseInt(last.split('/')[1], 10);
        var out = [];
        for (var i = 0; i < n; i++) {
            m++;
            if (m > 12) { m = 1; y++; }
            out.push('T' + m + '/' + y);
        }
        return out;
    }

    /* ---------- Hoi quy tuyen tinh (doi chieu) ---------- */
    function linearForecast(values, n) {
        var N = values.length, sx = 0, sy = 0, sxy = 0, sx2 = 0;
        for (var i = 0; i < N; i++) { sx += i; sy += values[i]; sxy += i * values[i]; sx2 += i * i; }
        var b = (N * sxy - sx * sy) / (N * sx2 - sx * sx);
        var a = (sy - b * sx) / N;
        var out = [];
        for (var k = 0; k < n; k++) out.push(Math.round(a + b * (N + k)));
        return out;
    }

    /* ---------- Huan luyen ---------- */
    function train() {
        if (training || !window.tf || !DATA) return;
        training = true;
        $('btnTrain').disabled = true;

        var epochs = parseInt($('labEpochs').value, 10);
        var lr = parseFloat($('labLr').value);
        var hidden = parseInt($('labHidden').value, 10);
        $('labStatus').innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang huấn luyện mạng ' + WIN + ' → ' + hidden + ' → ' + Math.max(4, hidden / 2) + ' → 1 ...';

        var v = DATA.values;
        // Guard: can it nhat WIN+1 thang du lieu moi tao duoc 1 mau huan luyen
        if (!v || v.length < WIN + 1) {
            $('labStatus').innerHTML = '⚠️ Cần ít nhất ' + (WIN + 1) + ' tháng số liệu sản lượng để huấn luyện (hiện có ' + (v ? v.length : 0) + ').';
            return;
        }
        yMin = Math.min.apply(null, v); yMax = Math.max.apply(null, v);
        if (yMax - yMin < 1e-9) yMax = yMin + 1;   // du lieu phang tuyet doi -> tranh chia 0/NaN
        var xs = [], ys = [];
        for (var i = WIN; i < v.length; i++) {
            xs.push([norm(v[i - 3]), norm(v[i - 2]), norm(v[i - 1])]);
            ys.push([norm(v[i])]);
        }
        var tx = tf.tensor2d(xs), ty = tf.tensor2d(ys);

        if (model) model.dispose();
        model = tf.sequential();
        model.add(tf.layers.dense({ units: hidden, activation: 'relu', inputShape: [WIN] }));
        model.add(tf.layers.dense({ units: Math.max(4, Math.round(hidden / 2)), activation: 'relu' }));
        model.add(tf.layers.dense({ units: 1 }));
        model.compile({ optimizer: tf.train.adam(lr), loss: 'meanSquaredError' });

        var losses = [];
        resetLossChart();
        var t0 = performance.now();

        model.fit(tx, ty, {
            epochs: epochs,
            batchSize: 8,
            shuffle: true,
            callbacks: {
                onEpochEnd: function (ep, logs) {
                    losses.push(logs.loss);
                    if (ep % 5 === 0 || ep === epochs - 1) {
                        pushLoss(losses);
                        $('labEpochInfo').textContent = 'Epoch ' + (ep + 1) + '/' + epochs + ' · loss (MSE) = ' + logs.loss.toFixed(5);
                        $('labProg').style.width = Math.round((ep + 1) / epochs * 100) + '%';
                    }
                    return tf.nextFrame();
                }
            }
        }).then(function () {
            var ms = Math.round(performance.now() - t0);

            // Danh gia tren tap huan luyen (MAE thuc te)
            var predTrain = model.predict(tx);
            return predTrain.data().then(function (pd) {
                var mae = 0;
                for (var i = 0; i < ys.length; i++) mae += Math.abs(denorm(pd[i]) - denorm(ys[i][0]));
                mae = Math.round(mae / ys.length);
                predTrain.dispose();

                // Du bao HORIZON thang (autoregressive rollout)
                var winV = v.slice(-WIN).map(norm);
                var preds = [];
                function step() {
                    var t = tf.tensor2d([winV]);
                    var p = model.predict(t);
                    return p.data().then(function (d) {
                        t.dispose(); p.dispose();
                        var val = d[0];
                        preds.push(Math.round(denorm(val)));
                        winV = winV.slice(1).concat([val]);
                        if (preds.length < HORIZON) return step();
                    });
                }
                return step().then(function () {
                    tx.dispose(); ty.dispose();
                    var lin = linearForecast(v, HORIZON);
                    drawMain(preds, lin);
                    fillTable(preds, lin, mae);
                    var doi = preds[0] - v[v.length - 1];
                    $('labStatus').innerHTML = '✅ Huấn luyện xong <strong>' + epochs + ' epochs trong ' + (ms / 1000).toFixed(1) + 's</strong> · MAE ≈ <strong>' + mae.toLocaleString('vi-VN') + ' TEU</strong>. ' +
                        'Mạng nơ-ron dự báo tháng tới đạt <strong>' + preds[0].toLocaleString('vi-VN') + ' TEU</strong> (' + (doi >= 0 ? '+' : '') + doi.toLocaleString('vi-VN') + ' so với tháng gần nhất).';
                    training = false;
                    $('btnTrain').disabled = false;
                    $('btnTrain').innerHTML = '<i class="bi bi-arrow-repeat me-1"></i>Huấn luyện lại';
                });
            });
        }).catch(function (e) {
            console.error(e);
            try { tx.dispose(); ty.dispose(); } catch (e2) { }   // khong ro ri tensor khi loi
            $('labStatus').textContent = '❌ Lỗi huấn luyện: ' + e.message;
            training = false;
            $('btnTrain').disabled = false;
        });
    }

    /* ---------- Bieu do ---------- */
    function resetLossChart() {
        var ex = Chart.getChart($('lossChart'));
        if (ex) ex.destroy();
        lossChart = null;
        lossChart = new Chart($('lossChart'), {
            type: 'line',
            data: { labels: [], datasets: [{ label: 'Loss (MSE)', data: [], borderColor: '#8b5cf6', backgroundColor: 'rgba(139,92,246,.12)', fill: true, tension: .3, pointRadius: 0 }] },
            options: { maintainAspectRatio: false, animation: false, plugins: { legend: { display: false } }, scales: { y: { type: 'logarithmic', grid: { color: '#eef2f7' } }, x: { display: false } } }
        });
    }
    function pushLoss(losses) {
        lossChart.data.labels = losses.map(function (_, i) { return i + 1; });
        lossChart.data.datasets[0].data = losses;
        lossChart.update('none');
    }

    function drawMain(nn, lin) {
        var hist = DATA.values;
        var labels = DATA.labels.concat(nextLabels(HORIZON));
        var histData = hist.concat(new Array(HORIZON).fill(null));
        var pad = new Array(hist.length - 1).fill(null);
        var nnData = pad.concat([hist[hist.length - 1]]).concat(nn);
        var linData = pad.concat([hist[hist.length - 1]]).concat(lin);
        var ex = Chart.getChart($('mainChart'));
        if (ex) ex.destroy();
        mainChart = null;
        mainChart = new Chart($('mainChart'), {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    { label: 'Thực tế', data: histData, borderColor: '#0d4f8b', backgroundColor: 'rgba(13,79,139,.08)', fill: true, tension: .35, pointRadius: 3 },
                    { label: 'Mạng nơ-ron (Deep Learning)', data: nnData, borderColor: '#8b5cf6', borderDash: [7, 5], tension: .35, pointRadius: 4, pointStyle: 'rectRot' },
                    { label: 'Hồi quy tuyến tính (đối chiếu)', data: linData, borderColor: '#ff7d2d', borderDash: [3, 4], tension: .35, pointRadius: 3 }
                ]
            },
            options: { maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }, scales: { y: { grid: { color: '#eef2f7' } }, x: { grid: { display: false } } } }
        });
    }

    function fillTable(nn, lin, mae) {
        var labels = nextLabels(HORIZON);
        var html = '';
        for (var i = 0; i < HORIZON; i++) {
            var d = nn[i] - lin[i];
            html += '<tr><td class="fw-semibold">' + labels[i] + '</td>' +
                '<td class="text-end" style="color:#8b5cf6;font-weight:700;">' + nn[i].toLocaleString('vi-VN') + '</td>' +
                '<td class="text-end">' + lin[i].toLocaleString('vi-VN') + '</td>' +
                '<td class="text-end ' + (d >= 0 ? 'text-success' : 'text-danger') + '">' + (d >= 0 ? '+' : '') + d.toLocaleString('vi-VN') + '</td></tr>';
        }
        $('labTable').innerHTML = html;
        $('labMae').textContent = '±' + mae.toLocaleString('vi-VN') + ' TEU';
        $('labResult').style.display = '';
    }

    /* ---------- Khoi dong ---------- */
    document.addEventListener('DOMContentLoaded', function () {
        var el = $('labData');
        if (!el) return;
        DATA = JSON.parse(el.textContent);

        $('labEpochs').addEventListener('input', function () { $('labEpochsVal').textContent = this.value; });
        if (location.search.indexOf('auto=1') >= 0) {
            var em = location.search.match(/[?&]ep=(\d+)/);
            if (em) { $('labEpochs').value = em[1]; $('labEpochsVal').textContent = em[1]; }
            setTimeout(function () { $('btnTrain').click(); }, 700);
        }
        $('btnTrain').addEventListener('click', function () {
            if (window.tf) { train(); return; }
            $('labStatus').innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang tải TensorFlow.js...';
            loadScript(TF_CDN).then(train).catch(function () {
                $('labStatus').textContent = '❌ Không tải được TensorFlow.js — kiểm tra Internet.';
            });
        });
    });
})();
