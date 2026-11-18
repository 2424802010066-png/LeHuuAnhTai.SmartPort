import { Fragment, useCallback, useEffect, useRef, useState } from 'react'
import { goiApi } from '../../api/client'

const DAI_TOI_DA = 500

const CHAO =
  'Xin chào! Tôi là PortBot 🤖 — trợ lý ảo AI của Cảng Quốc tế SmartPort.\n' +
  'Tôi có thể tra cứu container, lịch tàu, bảng giá dịch vụ và trạng thái booking giúp bạn.'

const GOI_Y = ['Lịch tàu tuần này', 'Tra container MSCU2201417', 'Giá bốc xếp container', 'Giờ làm việc của cảng']

/** Chon giong doc tieng Viet tot nhat co san (Edge: HoaiMy/NamMinh neural, Chrome: Google) */
function chonGiong() {
  if (!('speechSynthesis' in window)) return null
  const tatCa = window.speechSynthesis.getVoices() || []
  const vi = tatCa.filter((v) => v.lang && v.lang.toLowerCase().startsWith('vi'))
  return vi.find((v) => /HoaiMy|NamMinh|Natural/i.test(v.name)) || vi.find((v) => /Google/i.test(v.name)) || vi[0] || null
}

/**
 * PortBot — tro ly ao AI cua website cong khai (thay _Chatbot.cshtml + Scripts/chatbot.js).
 * Hoi: POST /api/cong-khai/chatbot. Neu dang dang nhap, token di kem nen bot duoc phep
 * tra loi chi tiet booking/container CUA CHINH khach do (chong IDOR o phia API).
 */
export default function PortBot({ khiDoiTrangThai }) {
  const [mo, setMo] = useState(false)
  const [tinNhan, setTinNhan] = useState([])
  const [cauHoi, setCauHoi] = useState('')
  const [dangGo, setDangGo] = useState(false)
  const [docTo, setDocTo] = useState(false)
  const [dangNghe, setDangNghe] = useState(false)

  const thanRef = useRef(null)
  const oNhapRef = useRef(null)
  const giongRef = useRef(null)
  const nhanDienRef = useRef(null)
  const hoiBangGiongRef = useRef(false)

  useEffect(() => {
    if (!('speechSynthesis' in window)) return undefined
    const nap = () => {
      giongRef.current = chonGiong()
    }
    nap()
    window.speechSynthesis.addEventListener('voiceschanged', nap)
    return () => window.speechSynthesis.removeEventListener('voiceschanged', nap)
  }, [])

  useEffect(() => {
    khiDoiTrangThai?.(mo)
  }, [mo, khiDoiTrangThai])

  // Cac trang cong khai mo khung chat bang: window.dispatchEvent(new Event('smartport:portbot'))
  useEffect(() => {
    const moTuTrang = () => {
      setMo(true)
      setTinNhan((cu) => (cu.length === 0 ? [{ id: 'chao', noiDung: CHAO, ai: 'bot' }] : cu))
      setTimeout(() => oNhapRef.current?.focus(), 60)
    }
    window.addEventListener('smartport:portbot', moTuTrang)
    return () => window.removeEventListener('smartport:portbot', moTuTrang)
  }, [])

  useEffect(() => {
    if (thanRef.current) thanRef.current.scrollTop = thanRef.current.scrollHeight
  }, [tinNhan, dangGo])

  const doc = useCallback((chu) => {
    if (!('speechSynthesis' in window)) return
    window.speechSynthesis.cancel()
    if (!giongRef.current) giongRef.current = chonGiong()
    // Bo emoji, bullet va chu thich ky thuat de giong doc tu nhien
    const sach = chu
      .replace(/\(NLP[^)]*\)/g, '')
      .replace(/\p{Extended_Pictographic}/gu, ' ')
      .replace(/[•·—→←]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 600)
    if (!sach) return
    // Cat thanh cau ngan: trinh duyet doc muot, khong bi ngat giua chung
    const manh = sach.match(/[^.!?\n]+[.!?\n]?/g) || [sach]
    const khuc = []
    let dem = ''
    manh.forEach((m) => {
      if ((dem + m).length > 180) {
        if (dem.trim()) khuc.push(dem.trim())
        dem = m
      } else dem += m
    })
    if (dem.trim()) khuc.push(dem.trim())
    khuc.forEach((k) => {
      const u = new SpeechSynthesisUtterance(k)
      u.lang = 'vi-VN'
      u.rate = 1.05
      if (giongRef.current) u.voice = giongRef.current
      window.speechSynthesis.speak(u)
    })
  }, [])

  const them = useCallback((noiDung, ai, nguon) => {
    setTinNhan((cu) => [...cu, { id: cu.length + '-' + Date.now(), noiDung, ai, nguon }])
  }, [])

  const gui = useCallback(
    async (chu) => {
      const hoi = (chu ?? '').trim().slice(0, DAI_TOI_DA)
      if (!hoi || dangGo) return
      them(hoi, 'toi')
      setCauHoi('')
      setDangGo(true)
      try {
        const kq = await goiApi('/api/cong-khai/chatbot', { method: 'POST', body: { cauHoi: hoi } })
        them(kq.traLoi, 'bot', kq.nguon)
        if (docTo || hoiBangGiongRef.current) doc(kq.traLoi)
      } catch {
        them('Xin lỗi, có lỗi kết nối. Bạn thử lại sau nhé!', 'bot')
      } finally {
        hoiBangGiongRef.current = false
        setDangGo(false)
      }
    },
    [dangGo, doc, docTo, them],
  )

  const moKhung = () => {
    setMo(true)
    if (tinNhan.length === 0) them(CHAO, 'bot')
    setTimeout(() => oNhapRef.current?.focus(), 60)
  }

  const batMic = () => {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition
    if (!SR) return
    if (nhanDienRef.current) {
      nhanDienRef.current.stop()
      return
    }
    const nd = new SR()
    nhanDienRef.current = nd
    nd.lang = 'vi-VN'
    nd.interimResults = true
    nd.maxAlternatives = 1
    setDangNghe(true)
    let chotLai = ''
    nd.onresult = (e) => {
      let tam = ''
      for (let i = e.resultIndex; i < e.results.length; i += 1) {
        if (e.results[i].isFinal) chotLai += e.results[i][0].transcript
        else tam += e.results[i][0].transcript
      }
      setCauHoi((chotLai || tam).slice(0, DAI_TOI_DA))
    }
    nd.onerror = (e) => {
      if (e.error === 'not-allowed' || e.error === 'service-not-allowed')
        them('Trình duyệt chưa cho phép dùng micro. Bạn bấm biểu tượng 🔒 cạnh thanh địa chỉ → cho phép Micro rồi thử lại nhé!', 'bot')
      else if (e.error === 'no-speech') them('Tôi chưa nghe thấy gì — bạn bấm mic và nói lại gần hơn nhé!', 'bot')
    }
    nd.onend = () => {
      nhanDienRef.current = null
      setDangNghe(false)
      const chu = chotLai.trim()
      if (chu) {
        hoiBangGiongRef.current = true // hoi bang giong -> tu doc to cau tra loi
        gui(chu)
      }
    }
    try {
      nd.start()
    } catch {
      nhanDienRef.current = null
      setDangNghe(false)
    }
  }

  const doiLoa = () => {
    const bat = !docTo
    setDocTo(bat)
    if (!bat) window.speechSynthesis?.cancel()
    else {
      giongRef.current = chonGiong()
      doc('Đã bật giọng đọc. Tôi sẽ đọc to các câu trả lời cho bạn.')
    }
  }

  const coMic = typeof window !== 'undefined' && (window.SpeechRecognition || window.webkitSpeechRecognition)
  const coLoa = typeof window !== 'undefined' && 'speechSynthesis' in window

  return (
    <>
      {!mo && (
        <button type="button" className="ck-pb-nut" onClick={moKhung} title="Chat với PortBot AI" aria-label="Mở khung chat PortBot">
          <i className="bi bi-robot" />
        </button>
      )}

      {mo && (
        <div className="ck-pb-hop" role="dialog" aria-label="Trợ lý ảo PortBot">
          <div className="ck-pb-dau">
            <span className="ck-ico-tron">
              <i className="bi bi-robot" />
            </span>
            <div className="flex-grow-1 min-w-0">
              <div className="fw-bold">PortBot</div>
              <div className="small" style={{ color: '#bcd3ea' }}>
                <span style={{ color: '#7CFC9A' }}>●</span> Trợ lý ảo AI — Cảng SmartPort
              </div>
            </div>
            <button type="button" className="btn-close btn-close-white" onClick={() => setMo(false)} aria-label="Đóng khung chat" />
          </div>

          <div className="ck-pb-than" ref={thanRef}>
            {tinNhan.map((t) => (
              <Fragment key={t.id}>
                <div className={'ck-pb-tin ' + (t.ai === 'toi' ? 'ck-toi' : 'ck-bot')}>{t.noiDung}</div>
                {t.nguon === 'gemini' && <div className="ck-pb-nguon">✦ Trả lời bởi Gemini AI dựa trên dữ liệu cảng</div>}
              </Fragment>
            ))}
            {tinNhan.length === 1 && (
              <div className="ck-pb-goi">
                {GOI_Y.map((g) => (
                  <button key={g} type="button" onClick={() => gui(g)}>
                    {g}
                  </button>
                ))}
              </div>
            )}
            {dangGo && (
              <div className="ck-pb-tin ck-bot ck-pb-go">
                <span />
                <span />
                <span />
              </div>
            )}
          </div>

          <div className="ck-pb-chan">
            {coMic && (
              <button
                type="button"
                className={'ck-pb-icon' + (dangNghe ? ' ck-bat' : '')}
                onClick={batMic}
                title="Nói câu hỏi (tiếng Việt)"
                aria-label="Nói câu hỏi bằng micro"
              >
                <i className="bi bi-mic-fill" />
              </button>
            )}
            <input
              ref={oNhapRef}
              value={cauHoi}
              maxLength={DAI_TOI_DA}
              placeholder={dangNghe ? 'Đang nghe... hãy nói câu hỏi' : 'Nhập hoặc nói câu hỏi...'}
              onChange={(e) => setCauHoi(e.target.value.slice(0, DAI_TOI_DA))}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault()
                  gui(cauHoi)
                }
              }}
              aria-label="Câu hỏi gửi PortBot"
            />
            {coLoa && (
              <button
                type="button"
                className={'ck-pb-icon' + (docTo ? ' ck-bat' : '')}
                onClick={doiLoa}
                title="Bật/tắt đọc câu trả lời"
                aria-label="Bật hoặc tắt giọng đọc câu trả lời"
              >
                <i className={'bi ' + (docTo ? 'bi-volume-up-fill' : 'bi-volume-mute-fill')} />
              </button>
            )}
            <button type="button" className="ck-pb-gui" onClick={() => gui(cauHoi)} disabled={dangGo} title="Gửi" aria-label="Gửi câu hỏi">
              <i className="bi bi-send-fill" />
            </button>
          </div>
        </div>
      )}
    </>
  )
}
