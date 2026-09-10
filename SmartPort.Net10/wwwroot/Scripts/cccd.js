/* ============================================================
   QUET GIAY TO TAI XE
   - Ma QR the CCCD gan chip: jsQR doc tu khung hinh camera hoac anh tai len,
     roi gui CHUOI THO len may chu de may chu tu boc truong.
   - Bang lai: Tesseract OCR tra ve van ban tho, may chu boc so/hang/han.
   Ca hai deu KHONG tu tin ket qua o trinh duyet — client sua duoc.
   ============================================================ */
(function () {
    var video = document.getElementById('cam');
    var canvas = document.getElementById('canvasQr');
    if (!video || !canvas) return;
    var ctx = canvas.getContext('2d', { willReadFrequently: true });
    var tinh = document.getElementById('tinhTrangQr');
    var dangChay = false, luong = null, lanQuetCuoi = 0;

    function bao(t, mau) {
        tinh.textContent = t;
        tinh.style.color = mau || '#64748b';
    }

    // ---------- Camera ----------
    document.getElementById('btnCam').addEventListener('click', function () {
        if (dangChay) { dungCam(); return; }
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            bao('Trình duyệt không cho dùng camera. Dùng nút "Chọn ảnh" thay thế.', '#dc2626');
            return;
        }
        navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } }
        }).then(function (s) {
            luong = s; video.srcObject = s; video.play();
            dangChay = true;
            this.innerHTML = '<i class="bi bi-stop-circle me-1"></i>Tắt camera';
            bao('Đang tìm mã QR… đưa mặt trước thẻ vào khung.', '#0891b2');
            requestAnimationFrame(quetKhung);
        }.bind(this)).catch(function (e) {
            bao('Không mở được camera: ' + e.message + '. Dùng nút "Chọn ảnh".', '#dc2626');
        });
    });

    function dungCam() {
        dangChay = false;
        if (luong) { luong.getTracks().forEach(function (t) { t.stop(); }); luong = null; }
        video.srcObject = null;
        document.getElementById('btnCam').innerHTML = '<i class="bi bi-camera-video me-1"></i>Bật camera';
        bao('Đã tắt camera.');
    }

    function quetKhung() {
        if (!dangChay) return;
        if (video.readyState === video.HAVE_ENOUGH_DATA) {
            canvas.width = video.videoWidth; canvas.height = video.videoHeight;
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            var anh = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var kq = window.jsQR ? jsQR(anh.data, anh.width, anh.height, { inversionAttempts: 'dontInvert' }) : null;
            // Chan quet lien tuc cung mot the: cach nhau it nhat 2 giay
            if (kq && kq.data && Date.now() - lanQuetCuoi > 2000) {
                lanQuetCuoi = Date.now();
                guiChuoi(kq.data);
            }
        }
        requestAnimationFrame(quetKhung);
    }

    // ---------- Anh tai len ----------
    document.getElementById('fileQr').addEventListener('change', function (e) {
        var f = e.target.files[0]; if (!f) return;
        bao('Đang đọc ảnh…', '#0891b2');
        var img = new Image();
        img.onload = function () {
            canvas.width = img.width; canvas.height = img.height;
            ctx.drawImage(img, 0, 0);
            var a = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var kq = window.jsQR ? jsQR(a.data, a.width, a.height) : null;
            if (kq && kq.data) guiChuoi(kq.data);
            else bao('Không tìm thấy mã QR trong ảnh — chụp lại rõ hơn, lấy trọn mã QR.', '#dc2626');
            URL.revokeObjectURL(img.src);
        };
        img.onerror = function () { bao('Không mở được ảnh.', '#dc2626'); };
        img.src = URL.createObjectURL(f);
    });

    // ---------- Gui chuoi tho len may chu ----------
    function guiChuoi(chuoi) {
        bao('Đọc được mã QR, đang kiểm tra…', '#0891b2');
        fetch('/Admin/TaiXe/DocQr', {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ chuoi: chuoi })
        }).then(function (r) { return r.json(); })
          .then(function (d) {
              if (!d.ok) { bao('⚠ ' + d.message, '#dc2626'); return; }
              dienForm(d);
              bao('✅ Đã đọc xong thẻ của ' + d.hoTen + '.', '#15803d');
              if (dangChay) dungCam();
          })
          .catch(function () { bao('Mất kết nối máy chủ — thử lại.', '#dc2626'); });
    }

    function dienForm(d) {
        function dat(id, v) { var e = document.getElementById(id); if (e && v) e.value = v; }
        dat('fSo', d.soCccd); dat('fTen', d.hoTen); dat('fNs', d.ngaySinh);
        dat('fGt', d.gioiTinh); dat('fDc', d.diaChi); dat('fNc', d.ngayCap);

        var box = document.getElementById('bangKhach');
        if (d.daCo) {
            // Khach quen: do lai GPLX + bien so da luu, khoi phai chup lai bang lai
            dat('fGplx', d.soGplxCu); dat('fHang', d.hangCu);
            dat('fHan', d.hanCu); dat('fBien', d.bienSoCu);
            box.className = 'alert alert-success py-2 small';
            box.innerHTML = '<b>Khách quen</b> — đã vào cổng ' + d.soLanVao + ' lần. '
                          + 'Thông tin bằng lái và biển số đã điền sẵn từ lần trước, chỉ cần kiểm tra lại.';
        } else {
            box.className = 'alert alert-info py-2 small';
            box.innerHTML = '<b>Tài xế mới</b> — chưa có hồ sơ. '
                          + (d.tuoi ? d.tuoi + ' tuổi. ' : '')
                          + 'Chụp thêm bằng lái ở bước 2 rồi bấm Lưu.';
        }
        box.style.display = '';
    }

    // ---------- OCR bang lai ----------
    var fGplx = document.getElementById('fileGplx');
    if (fGplx) {
        fGplx.addEventListener('change', function (e) {
            var f = e.target.files[0]; if (!f) return;
            var tt = document.getElementById('tinhTrangGplx');
            var thanh = document.getElementById('thanhOcr');
            var trong = document.getElementById('thanhOcrIn');
            if (!window.Tesseract) { tt.textContent = 'Chưa nạp được Tesseract.'; return; }

            tt.textContent = 'Đang nhận dạng chữ trên bằng lái…';
            tt.style.color = '#0891b2';
            thanh.style.display = '';
            Tesseract.recognize(f, 'eng', {
                logger: function (m) {
                    if (m.status === 'recognizing text') trong.style.width = Math.round(m.progress * 100) + '%';
                }
            }).then(function (r) {
                thanh.style.display = 'none';
                return fetch('/Admin/TaiXe/DocGplx', {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ vanBan: r.data.text })
                });
            }).then(function (r) { return r.json(); })
              .then(function (d) {
                  var box = document.getElementById('bangGplx');
                  if (!d.ok) {
                      tt.textContent = '⚠ ' + d.message;
                      tt.style.color = '#dc2626';
                      return;
                  }
                  function dat(id, v) { var e = document.getElementById(id); if (e && v) e.value = v; }
                  dat('fGplx', d.soGplx); dat('fHang', d.hang); dat('fHanDen', d.hanDen);
                  var eHan = document.getElementById('fHan'); if (eHan && d.hanDen) eHan.value = d.hanDen;

                  tt.textContent = '✅ Đã bóc được thông tin — kiểm tra lại rồi sửa nếu sai.';
                  tt.style.color = '#15803d';
                  box.className = d.daHetHan ? 'alert alert-danger py-2 small' : 'alert alert-warning py-2 small';
                  box.innerHTML = d.daHetHan
                      ? '<b>⛔ BẰNG LÁI ĐÃ HẾT HẠN</b> (' + (d.hanDen || '') + ') — không được lái xe. Báo điều độ.'
                      : '<b>Kết quả OCR</b> — số ' + (d.soGplx || '?') + ' · hạng ' + (d.hang || '?')
                        + ' · hạn ' + (d.hanDen || '?') + '. OCR có thể sai, <b>xem lại rồi sửa tay</b>.';
                  box.style.display = '';
              })
              .catch(function () {
                  thanh.style.display = 'none';
                  tt.textContent = 'Lỗi khi nhận dạng — thử ảnh khác.';
                  tt.style.color = '#dc2626';
              });
        });
    }

    // ---------- Ghi vao cong ----------
    var btnVao = document.getElementById('btnVaoCong');
    if (btnVao) {
        btnVao.addEventListener('click', function () {
            var so = document.getElementById('fSo').value.trim();
            var bien = document.getElementById('fBien').value.trim();
            if (!so) { alert('Chưa có số CCCD — quét thẻ trước.'); return; }
            var fd = new FormData();
            fd.append('soGiayTo', so); fd.append('bienSo', bien);
            fetch('/Admin/TaiXe/GhiVaoCong', { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (!d.ok) { alert(d.message); return; }
                    var t = '✅ Đã ghi nhận ' + d.hoTen + ' vào cổng (lần thứ ' + d.soLanVao + ').';
                    if (d.gplxHetHan) t += '\n⛔ CẢNH BÁO: bằng lái đã hết hạn!';
                    if (!d.ganVaoLuot) t += '\nℹ️ Chưa có lượt xe nào của biển số này trong 30 phút qua để gắn.';
                    alert(t);
                })
                .catch(function () { alert('Mất kết nối máy chủ.'); });
        });
    }

    window.addEventListener('beforeunload', dungCam);
})();
