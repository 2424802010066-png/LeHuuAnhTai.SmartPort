/* PortBot - AI chatbot widget */
(function () {
    var fab = document.getElementById('pbFab');
    var panel = document.getElementById('pbPanel');
    if (!fab || !panel) return;

    var body = document.getElementById('pbBody');
    var input = document.getElementById('pbInput');
    var sendBtn = document.getElementById('pbSend');
    var askUrl = (window.APP_ROOT || '/') + 'Chatbot/Ask';
    var greeted = false;

    function esc(s) {
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function addMsg(text, who, source) {
        var div = document.createElement('div');
        div.className = 'pb-msg ' + who;
        div.innerHTML = esc(text);
        body.appendChild(div);
        if (who === 'bot' && source === 'gemini') {
            var src = document.createElement('div');
            src.className = 'pb-src';
            src.textContent = '✦ Trả lời bởi Gemini AI dựa trên dữ liệu cảng';
            body.appendChild(src);
        }
        body.scrollTop = body.scrollHeight;
        // Doc to khi: da bat loa, HOAC luot hoi vua roi la bang giong noi
        if (who === 'bot' && (speakOn || voiceAsk)) {
            speak(text);
            voiceAsk = false;
        }
    }

    /* ----- Tro ly giong noi: doc cau tra loi (TTS) -----
       Cong nghe manh nhat co san tren trinh duyet:
       - Edge: giong NEURAL tieng Viet cua Microsoft (HoaiMy / NamMinh) — tu nhien nhu nguoi that
       - Chrome: Google Tieng Viet
       Luu y: getVoices() tai BAT DONG BO -> phai cache qua onvoiceschanged
       (day chinh la ly do ban cu thuong "cam"). */
    var speakOn = false;
    var voiceAsk = false;          // luot hoi bang MIC -> tu doc tra loi du chua bat loa
    var viVoice = null;

    function pickVoice() {
        if (!('speechSynthesis' in window)) return;
        var all = speechSynthesis.getVoices();
        if (!all || !all.length) return;
        var vi = all.filter(function (v) { return v.lang && v.lang.toLowerCase().indexOf('vi') === 0; });
        // Uu tien giong neural Microsoft (HoaiMy/NamMinh) > Google > bat ky vi-VN
        viVoice = vi.filter(function (v) { return /HoaiMy|NamMinh|Natural/i.test(v.name); })[0]
               || vi.filter(function (v) { return /Google/i.test(v.name); })[0]
               || vi[0] || null;
    }
    if ('speechSynthesis' in window) {
        pickVoice();
        speechSynthesis.onvoiceschanged = pickVoice;
    }

    function speak(text) {
        if (!('speechSynthesis' in window)) return;
        speechSynthesis.cancel();
        if (!viVoice) pickVoice();
        // Lam sach: bo emoji, bullet, va phan chu thich ky thuat (NLP-RAG...)
        var clean = text
            .replace(/\(NLP[^)]*\)/g, '')
            .replace(/[\uD83C-\uD83E][\uDC00-\uDFFF]|[☀-➿←-⇿•·—]/g, ' ')
            .replace(/\s+/g, ' ').trim().substring(0, 600);
        if (!clean) return;
        // Tach thanh tung cau ngan -> doc muot, khong bi trinh duyet cat ngang
        var parts = clean.match(/[^.!?\n]+[.!?\n]?/g) || [clean];
        var buf = '';
        var chunks = [];
        parts.forEach(function (p) {
            if ((buf + p).length > 180) { if (buf.trim()) chunks.push(buf.trim()); buf = p; }
            else buf += p;
        });
        if (buf.trim()) chunks.push(buf.trim());
        chunks.forEach(function (c) {
            var u = new SpeechSynthesisUtterance(c);
            u.lang = 'vi-VN';
            u.rate = 1.05;
            if (viVoice) u.voice = viVoice;
            speechSynthesis.speak(u);       // tu xep hang doc lan luot
        });
    }

    function addChips() {
        var wrap = document.createElement('div');
        wrap.className = 'pb-chips';
        var chips = [
            'Lịch tàu tuần này',
            'Tra container MSCU2201417',
            'Giá bốc xếp container',
            'Giờ làm việc của cảng'
        ];
        chips.forEach(function (c) {
            var b = document.createElement('button');
            b.className = 'pb-chip';
            b.type = 'button';
            b.textContent = c;
            b.onclick = function () { input.value = c; send(); };
            wrap.appendChild(b);
        });
        body.appendChild(wrap);
        body.scrollTop = body.scrollHeight;
    }

    function typing(on) {
        var t = document.getElementById('pbTyping');
        if (on && !t) {
            t = document.createElement('div');
            t.id = 'pbTyping';
            t.className = 'pb-msg bot pb-typing';
            t.innerHTML = '<span></span><span></span><span></span>';
            body.appendChild(t);
            body.scrollTop = body.scrollHeight;
        } else if (!on && t) {
            t.remove();
        }
    }

    function send() {
        var text = (input.value || '').trim();
        if (!text) return;
        addMsg(text, 'user');
        input.value = '';
        input.focus();
        typing(true);

        var fd = new FormData();
        fd.append('message', text);
        fetch(askUrl, { method: 'POST', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                typing(false);
                addMsg(data.reply, 'bot', data.source);
            })
            .catch(function () {
                typing(false);
                addMsg('Xin lỗi, có lỗi kết nối. Bạn thử lại sau nhé!', 'bot');
            });
    }

    fab.addEventListener('click', function () {
        panel.classList.toggle('open');
        if (panel.classList.contains('open')) {
            if (!greeted) {
                greeted = true;
                addMsg('Xin chào! Tôi là PortBot 🤖 — trợ lý ảo AI của Cảng Quốc tế SmartPort.\nTôi có thể tra cứu container, lịch tàu, bảng giá dịch vụ và trạng thái booking giúp bạn.', 'bot');
                addChips();
            }
            input.focus();
        }
    });
    document.getElementById('pbClose').addEventListener('click', function () {
        panel.classList.remove('open');
    });
    sendBtn.addEventListener('click', send);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); send(); }
    });

    /* ----- Nhan dang giong noi tieng Viet (Web Speech API) ----- */
    var micBtn = document.getElementById('pbMic');
    var speakBtn = document.getElementById('pbSpeak');
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (micBtn) {
        if (!SR) micBtn.style.display = 'none';
        else {
            var rec = null;
            micBtn.addEventListener('click', function () {
                if (rec) { rec.stop(); return; }        // dang nghe -> bam lan nua de dung
                rec = new SR();
                rec.lang = 'vi-VN';
                rec.interimResults = true;              // hien chu NGAY khi dang noi
                rec.maxAlternatives = 1;
                micBtn.classList.add('listening');
                input.placeholder = '🎙️ Đang nghe... hãy nói câu hỏi';
                input.value = '';

                var finalText = '';
                rec.onresult = function (e) {
                    var interim = '';
                    for (var i = e.resultIndex; i < e.results.length; i++) {
                        if (e.results[i].isFinal) finalText += e.results[i][0].transcript;
                        else interim += e.results[i][0].transcript;
                    }
                    input.value = finalText || interim;  // chu chay truc tiep nhu tro ly ao
                };
                rec.onerror = function (e) {
                    if (e.error === 'not-allowed' || e.error === 'service-not-allowed') {
                        addMsg('Trình duyệt chưa cho phép dùng micro. Bạn bấm vào biểu tượng 🔒 cạnh thanh địa chỉ → cho phép Micro rồi thử lại nhé!', 'bot');
                    } else if (e.error === 'no-speech') {
                        addMsg('Tôi chưa nghe thấy gì — bạn bấm mic và nói lại gần hơn nhé!', 'bot');
                    } else if (e.error !== 'aborted') {
                        addMsg('Không nghe rõ (' + e.error + '), bạn thử nói lại nhé!', 'bot');
                    }
                };
                rec.onend = function () {
                    rec = null;
                    micBtn.classList.remove('listening');
                    input.placeholder = 'Nhập hoặc nói câu hỏi...';
                    var text = (input.value || '').trim();
                    if (text) {
                        voiceAsk = true;                 // hoi bang giong -> bot DOC to cau tra loi
                        send();
                    }
                };
                try { rec.start(); }
                catch (err) { rec = null; micBtn.classList.remove('listening'); }
            });
        }
    }
    if (speakBtn) {
        if (!('speechSynthesis' in window)) speakBtn.style.display = 'none';
        else {
            // Trang thai ban dau: TAT (icon mute) — bam de bat che do tu doc
            speakBtn.innerHTML = '<i class="bi bi-volume-mute-fill"></i>';
            speakBtn.title = 'Bật/tắt giọng đọc câu trả lời';
            speakBtn.addEventListener('click', function () {
                speakOn = !speakOn;
                speakBtn.innerHTML = speakOn ? '<i class="bi bi-volume-up-fill"></i>' : '<i class="bi bi-volume-mute-fill"></i>';
                speakBtn.classList.toggle('listening', speakOn);
                if (!speakOn) speechSynthesis.cancel();
                else {
                    pickVoice();
                    var tenGiong = viVoice ? viVoice.name : 'giọng mặc định của máy';
                    speak('Đã bật giọng đọc. Tôi sẽ đọc to các câu trả lời cho bạn.');
                    addMsg('🔊 Đã bật giọng đọc (' + tenGiong + '). Mẹo: hỏi bằng nút mic 🎙️ thì tôi luôn tự đọc câu trả lời!', 'bot');
                }
            });
        }
    }

    /* Auto mo chat phuc vu demo/chup anh: ?chat=open */
    if (location.search.indexOf('chat=open') >= 0) {
        setTimeout(function () {
            var fab = document.getElementById('pbFab');
            if (fab) fab.click();
            setTimeout(function () {
                var inp = document.getElementById('pbInput');
                var btn = document.getElementById('pbSend');
                if (inp && btn) { inp.value = 'lịch tàu tuần này'; btn.click(); }
            }, 800);
        }, 500);
    }
})();