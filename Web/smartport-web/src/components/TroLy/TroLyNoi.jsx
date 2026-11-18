import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import './TroLy.css'

/**
 * TRO LY NGHIEP VU — khung chat noi o goc phai duoi khu dieu hanh (chuyen tu wwwroot/Scripts/troly.js).
 *
 * Khac PortBot o trang cong khai: PortBot TRA LOI cau hoi cua khach, con tro ly nay LAM VIEC cho
 * nhan vien — no goi thang nghiep vu he thong. Vi the co mot buoc ma PortBot khong co: viec dung
 * tien thi hien the XAC NHAN, nguoi phai bam Dong y roi may moi lam (POST /api/tro-ly/thuc-hien).
 *
 * Cau tra loi tu may chu la HTML (TroLyService tu dinh dang bang, danh sach bam duoc). Khong dung
 * dangerouslySetInnerHTML: HTML duoc LOC theo danh sach the/thuoc tinh cho phep (locHtml) roi moi
 * chen vao DOM, nen du sau nay nghiep vu co lo nhet chuoi nguoi dung vao cau tra loi cung khong
 * chay duoc script.
 */

// ---------------- Loc HTML cau tra loi ----------------

/** The duoc giu lai. The la (script, iframe...) bi bo han; the khac chi bo vo, giu chu ben trong. */
const THE_CHO_PHEP = new Set([
  'A', 'B', 'STRONG', 'I', 'EM', 'U', 'SPAN', 'DIV', 'P', 'BR', 'HR', 'UL', 'OL', 'LI',
  'CODE', 'SMALL', 'DETAILS', 'SUMMARY', 'BUTTON', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD',
])

/** The bo han ca noi dung ben trong */
const THE_BO_HAN = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'LINK', 'META', 'SVG', 'MATH'])

/**
 * Chi nhan: lenh noi bo (#goi: / #dien:), duong dan noi bo mot dau "/" (khong nhan "//ten-mien-la"),
 * hoac link http(s) ro rang. Moi thu khac (javascript:, data:, vbscript:...) bi bo.
 */
function urlAnToan(u) {
  const s = (u || '').trim()
  if (/^#(goi|dien):/i.test(s)) return s
  if (/^\/(?!\/)/.test(s)) return s
  if (/^https?:\/\//i.test(s)) return s
  return null
}

const lopAnToan = (v) =>
  (v || '')
    .split(/\s+/)
    .filter((x) => /^[a-z][a-z0-9-]*$/i.test(x))
    .join(' ')

function chepThuocTinh(nguon, moi, ten) {
  for (const a of Array.from(nguon.attributes)) {
    const t = a.name.toLowerCase()
    if (t === 'class') {
      const lop = lopAnToan(a.value)
      if (lop) moi.setAttribute('class', lop)
    } else if (t === 'title' || t === 'aria-label') {
      moi.setAttribute(t, a.value)
    } else if (t === 'href' && ten === 'A') {
      const u = urlAnToan(a.value)
      if (u) {
        moi.setAttribute('href', u)
        if (/^https?:/i.test(u)) {
          moi.setAttribute('target', '_blank')
          moi.setAttribute('rel', 'noopener noreferrer')
        }
      }
    } else if ((t === 'data-goi' || t === 'data-dien') && ten === 'BUTTON') {
      moi.setAttribute(t, a.value)
    } else if (t === 'open' && ten === 'DETAILS') {
      moi.setAttribute('open', '')
    } else if ((t === 'colspan' || t === 'rowspan') && (ten === 'TD' || ten === 'TH')) {
      if (/^\d{1,2}$/.test(a.value)) moi.setAttribute(t, a.value)
    }
    // moi thuoc tinh khac (style, on*, src, formaction...) bi bo
  }
  // nut trong cau tra loi luon la nut thuong, khong bao gio gui form
  if (ten === 'BUTTON') moi.setAttribute('type', 'button')
}

function chepNut(nguon, dich) {
  for (const n of Array.from(nguon.childNodes)) {
    if (n.nodeType === 3) {
      dich.appendChild(document.createTextNode(n.nodeValue))
      continue
    }
    if (n.nodeType !== 1) continue // binh luan, CDATA... bo
    const ten = n.tagName.toUpperCase()
    if (THE_BO_HAN.has(ten)) continue
    if (!THE_CHO_PHEP.has(ten)) {
      chepNut(n, dich) // the la: bo vo, giu chu ben trong
      continue
    }
    const moi = document.createElement(ten.toLowerCase())
    chepThuocTinh(n, moi, ten)
    chepNut(n, moi)
    dich.appendChild(moi)
  }
}

/** HTML (chuoi) -> DocumentFragment chi con the/thuoc tinh trong danh sach cho phep */
function locHtml(html) {
  const frag = document.createDocumentFragment()
  if (!html) return frag
  // DOMParser tao tai lieu "cho" — anh/script trong do khong duoc tai hay chay
  const doc = new DOMParser().parseFromString('<!doctype html><body>' + html + '</body>', 'text/html')
  chepNut(doc.body, frag)
  return frag
}

/** Cau tra loi da loc, chen bang DOM chu khong qua dangerouslySetInnerHTML */
function NoiDungTraLoi({ html }) {
  const o = useRef(null)
  useEffect(() => {
    if (o.current) o.current.replaceChildren(locHtml(html))
  }, [html])
  return <div ref={o} />
}

// ---------------- Go chu thay cho bam nut xac nhan ----------------
// Nguoi ta hay go "ok" ngay sau the xac nhan. Viec GHI van phai BAM nut — mot chu go voi khong
// duoc phep lap phieu thu tien — nhung cung khong duoc tra "chua hieu": chi vao nut can bam.
// Go "thoi" thi huy luon, vi huy khong dong vao du lieu nen khong can bat bam.
const DONG_Y = /^(ok|oke|okay|okie|oki|u|uh|um|co|yes|dong y|duoc|lam di|xac nhan|lam luon|ok lam di)( (nha|nhe|a|di|luon))?$/
const THOI = /^(thoi|khong|huy|bo qua|no|cancel|khoi|thoi khoi|khong can|dung)( (nha|nhe|a|di))?$/

const khongDau = (s) =>
  (s || '')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[đĐ]/g, 'd')
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

/** Loi mang / het phien: noi cho nguoi dung biet phai lam gi, dung bao chung chung "mat mang" */
function moTaLoi(e) {
  if (!e?.trangThai) return { chu: 'Không kết nối được máy chủ. Kiểm tra lại mạng rồi thử lần nữa.' }
  if (e.trangThai === 401) return { chu: 'Phiên đăng nhập đã hết hạn.', taiLai: true }
  if (e.trangThai === 403) return { chu: 'Tài khoản này không được dùng trợ lý nghiệp vụ.' }
  return { chu: e.message || 'Máy chủ báo lỗi ' + e.trangThai + '.' }
}

const GOI_Y_MAC_DINH = ['việc hôm nay', 'công nợ hôm nay', 'booking nào chờ duyệt', 'tồn bãi']

// ---------------- Khung chat ----------------

export default function TroLyNoi() {
  const { taiKhoan } = useAuth()
  const dieuHuong = useNavigate()

  const [mo, setMo] = useState(false)
  const [chao, setChao] = useState(null)
  const [tin, setTin] = useState([])
  const [dangGo, setDangGo] = useState(false)
  const [oNhap, setONhap] = useState('')
  const [vungChon, setVungChon] = useState(null)

  const oThan = useRef(null)
  const oInput = useRef(null)
  const soTin = useRef(0)

  const laNhanVien = taiKhoan?.vaiTro === 'Admin' || taiKhoan?.vaiTro === 'NhanVien'

  const themTin = useCallback((t) => {
    soTin.current += 1
    setTin((ds) => [...ds, { id: soTin.current, ...t }])
  }, [])

  const capNhat = useCallback((id, thayDoi) => {
    setTin((ds) => ds.map((m) => (m.id === id ? { ...m, ...thayDoi } : m)))
  }, [])

  const hoi = useCallback(
    async (cau) => {
      const c = (cau || '').trim().slice(0, 500)
      if (!c) return
      themTin({ ai: 'toi', chu: c })
      setDangGo(true)
      try {
        const kq = await goiApi('/api/tro-ly/hoi', { method: 'POST', body: { cauHoi: c } })
        themTin({ ai: 'bot', kq, xn: kq?.canXacNhan ? 'cho' : null, nhay: 0 })
      } catch (e) {
        themTin({ ai: 'bot', loi: moTaLoi(e) })
      } finally {
        setDangGo(false)
      }
    },
    [themTin],
  )

  /** Dien san cau mau vao o nhap va boi den PHAN MA — nguoi dung go ma that de len roi Enter */
  const dienSan = useCallback((cau) => {
    setONhap(cau)
    const m = /\S*\d\S*/.exec(cau)
    setVungChon(m ? [m.index, m.index + m[0].length] : [cau.length, cau.length])
  }, [])

  // Loi chao: chi goi khi mo khung lan dau
  useEffect(() => {
    if (!mo || chao || !laNhanVien) return
    let huy = false
    goiApi('/api/tro-ly/chao')
      .then((d) => {
        if (!huy) setChao({ tenGoi: d?.tenGoi || 'bạn', goiY: d?.goiY?.length ? d.goiY : GOI_Y_MAC_DINH })
      })
      .catch(() => {
        if (!huy) setChao({ tenGoi: 'bạn', goiY: GOI_Y_MAC_DINH })
      })
    return () => {
      huy = true
    }
  }, [mo, chao, laNhanVien])

  // Esc dong khung
  useEffect(() => {
    if (!mo) return undefined
    const bat = (e) => {
      if (e.key === 'Escape') setMo(false)
    }
    document.addEventListener('keydown', bat)
    return () => document.removeEventListener('keydown', bat)
  }, [mo])

  // Mo khung thi dua con tro vao o nhap (doi hoat hinh truot xong)
  useEffect(() => {
    if (!mo) return undefined
    const h = setTimeout(() => oInput.current?.focus(), 220)
    return () => clearTimeout(h)
  }, [mo])

  // Cuon xuong tin moi nhat
  useEffect(() => {
    const el = oThan.current
    if (el) el.scrollTop = el.scrollHeight
  }, [tin, dangGo, chao, mo])

  // Boi den phan ma sau khi dien san cau mau
  useEffect(() => {
    if (!vungChon) return
    const el = oInput.current
    if (el) {
      el.focus()
      el.setSelectionRange(vungChon[0], vungChon[1])
    }
    setVungChon(null)
  }, [vungChon])

  // Mo mot nhom trong danh muc sat day khung thi keo cho thay het.
  // "toggle" khong noi bot len cha, nen phai bat o pha capture.
  useEffect(() => {
    const el = oThan.current
    if (!el) return undefined
    const bat = (e) => {
      if (e.target.open) e.target.scrollIntoView({ block: 'nearest' })
    }
    el.addEventListener('toggle', bat, true)
    return () => el.removeEventListener('toggle', bat, true)
  }, [])

  if (!laNhanVien) return null

  const theDangCho = () => [...tin].reverse().find((m) => m.kq?.canXacNhan && m.xn === 'cho')

  async function thucHien(m) {
    const x = m.kq.canXacNhan
    capNhat(m.id, { xn: 'dang' })
    setDangGo(true)
    try {
      const kq = await goiApi('/api/tro-ly/thuc-hien', { method: 'POST', body: { congCu: x.congCu, thamSo: x.thamSo || '' } })
      capNhat(m.id, { xn: 'xong' })
      themTin({ ai: 'bot', kq, xn: kq?.canXacNhan ? 'cho' : null, nhay: 0 })
    } catch (e) {
      capNhat(m.id, { xn: 'cho' })
      themTin({ ai: 'bot', loi: moTaLoi(e) })
    } finally {
      setDangGo(false)
    }
  }

  function gui() {
    const v = oNhap.trim()
    if (!v || dangGo) return
    setONhap('')
    const m = theDangCho()
    if (m) {
      const k = khongDau(v)
      if (THOI.test(k)) {
        themTin({ ai: 'toi', chu: v })
        capNhat(m.id, { xn: 'thoi' })
        return
      }
      if (DONG_Y.test(k)) {
        themTin({ ai: 'toi', chu: v })
        themTin({ ai: 'bot', nhacBam: true })
        capNhat(m.id, { nhay: (m.nhay || 0) + 1 })
        return
      }
    }
    hoi(v)
  }

  /** Uy quyen su kien cho cac nut / link do may chu ve trong cau tra loi */
  function bamTrongThan(e) {
    const goi = e.target.closest('[data-goi]')
    if (goi) {
      hoi(goi.getAttribute('data-goi'))
      return
    }
    const dien = e.target.closest('[data-dien]')
    if (dien) {
      dienSan(dien.getAttribute('data-dien'))
      return
    }
    const a = e.target.closest('a[href]')
    if (!a) return
    const href = a.getAttribute('href') || ''
    if (/^#goi:/i.test(href)) {
      e.preventDefault()
      hoi(href.slice(5))
    } else if (/^#dien:/i.test(href)) {
      e.preventDefault()
      dienSan(href.slice(6))
    } else if (href.startsWith('/')) {
      // link noi bo: chuyen man hinh bang React Router (khong tai lai ca trang)
      e.preventDefault()
      setMo(false)
      dieuHuong(href)
    }
    // link ngoai (http...) da co target="_blank" — de trinh duyet tu mo
  }

  const veLienKet = (l, i) => {
    const url = l.url || ''
    // "#goi:<cau lenh>" la lenh noi bo — bam vao thi tro ly tu hoi tiep cau do thay vi mo trang
    if (url.startsWith('#goi:'))
      return (
        <button key={i} type="button" className="tl-nut tl-nut-goi" onClick={() => hoi(url.slice(5))}>
          <i className={'bi ' + (l.icon || 'bi-arrow-right')} />
          {l.nhan}
        </button>
      )
    // "#dien:<cau mau>" — dien san vao o nhap, chua gui
    if (url.startsWith('#dien:'))
      return (
        <button key={i} type="button" className="tl-nut tl-nut-goi" title="Điền sẵn — sửa mã rồi Enter" onClick={() => dienSan(url.slice(6))}>
          <i className={'bi ' + (l.icon || 'bi-pencil')} />
          {l.nhan}
        </button>
      )
    if (/^https?:/i.test(url))
      return (
        <a key={i} className="tl-nut" href={url} target="_blank" rel="noopener noreferrer">
          <i className={'bi ' + (l.icon || 'bi-arrow-right')} />
          {l.nhan}
        </a>
      )
    const noiBo = urlAnToan(url)
    if (!noiBo) return null
    return (
      <button
        key={i}
        type="button"
        className="tl-nut"
        onClick={() => {
          setMo(false)
          dieuHuong(noiBo)
        }}
      >
        <i className={'bi ' + (l.icon || 'bi-arrow-right')} />
        {l.nhan}
      </button>
    )
  }

  function veBong(m) {
    if (m.ai === 'toi')
      return (
        <div key={m.id} className="tl-bong tl-toi">
          {m.chu}
        </div>
      )

    if (m.loi)
      return (
        <div key={m.id} className="tl-bong tl-bot">
          {m.loi.chu}
          {m.loi.taiLai && (
            <>
              {' '}
              <button type="button" className="tl-nut mt-1" onClick={() => window.location.reload()}>
                <i className="bi bi-arrow-clockwise" />
                Tải lại trang
              </button>
            </>
          )}
        </div>
      )

    if (m.nhacBam)
      return (
        <div key={m.id} className="tl-bong tl-bot">
          Bấm <b>Đồng ý</b> ở thẻ vàng phía trên để tôi làm.
        </div>
      )

    const kq = m.kq || {}
    const x = kq.canXacNhan
    return (
      <div key={m.id} className="tl-bong tl-bot">
        {kq.congCu && (
          <div className="tl-congcu">
            <i className="bi bi-tools" /> {kq.congCu}
          </div>
        )}
        <NoiDungTraLoi html={kq.traLoi || 'Không có kết quả.'} />

        {!!kq.lienKets?.length && <div className="tl-lienket">{kq.lienKets.map(veLienKet)}</div>}

        {x && m.xn === 'xong' && (
          <div className="tl-xn-xong">
            <i className="bi bi-check-circle-fill" /> Đã thực hiện
          </div>
        )}
        {x && m.xn === 'thoi' && <div className="tl-xn-thoi">Đã bỏ qua.</div>}
        {x && (m.xn === 'cho' || m.xn === 'dang') && (
          // key doi theo so lan go "ok" -> hoat hinh nhay chay lai, chi cho nguoi dung nut can bam
          <div key={'xn' + (m.nhay || 0)} className={'tl-xacnhan' + (m.nhay ? ' tl-nhay' : '')}>
            <div className="tl-xn-hoi">
              <i className="bi bi-exclamation-triangle-fill" /> {x.moTa}
            </div>
            {x.canhBao && <div className="tl-xn-canhbao">{x.canhBao}</div>}
            <div className="tl-xn-nut">
              <button type="button" className="tl-dongy" disabled={m.xn === 'dang'} onClick={() => thucHien(m)}>
                {m.xn === 'dang' ? 'Đang làm…' : 'Đồng ý'}
              </button>
              <button type="button" className="tl-thoi" disabled={m.xn === 'dang'} onClick={() => capNhat(m.id, { xn: 'thoi' })}>
                Thôi
              </button>
            </div>
          </div>
        )}
      </div>
    )
  }

  return (
    <>
      <button
        type="button"
        className={'tl-fab' + (mo ? ' tl-fab-an' : '')}
        title="Trợ lý nghiệp vụ — nhờ việc gì cũng được"
        aria-label="Mở trợ lý nghiệp vụ"
        aria-expanded={mo}
        onClick={() => setMo(true)}
      >
        <i className="bi bi-stars" />
        <span className="tl-cham" />
      </button>

      {/* Khung luon nam trong DOM de co hoat hinh truot; dong lai thi inert -> khong bat duoc phim Tab */}
      <div className={'tl-panel' + (mo ? ' tl-open' : '')} role="dialog" aria-label="Trợ lý nghiệp vụ" aria-hidden={!mo} inert={!mo}>
        <div className="tl-dau">
          <div className="tl-avt">
            <i className="bi bi-stars" />
          </div>
          <div>
            <div className="tl-ten">Trợ lý nghiệp vụ</div>
            <div className="tl-phu">Nhờ việc bằng tiếng Việt</div>
          </div>
          <button type="button" title="Đóng" aria-label="Đóng trợ lý" onClick={() => setMo(false)}>
            <i className="bi bi-x-lg" />
          </button>
        </div>

        <div className="tl-body" ref={oThan} role="log" aria-live="polite" onClick={bamTrongThan}>
          <div className="tl-bong tl-bot">
            Chào <b>{chao?.tenGoi || taiKhoan?.hoTen || 'bạn'}</b>, cần gì cứ gõ — hoặc bấm:
            <div className="tl-goiys">
              {(chao?.goiY || GOI_Y_MAC_DINH).map((g) => (
                <button key={g} type="button" className="tl-goiy" onClick={() => hoi(g)}>
                  {g}
                </button>
              ))}
            </div>
          </div>

          {tin.map(veBong)}

          {dangGo && (
            <div className="tl-typing" aria-label="Trợ lý đang trả lời">
              <span />
              <span />
              <span />
            </div>
          )}
        </div>

        <div className="tl-chan">
          <input
            type="text"
            ref={oInput}
            autoComplete="off"
            maxLength={500}
            aria-label="Nội dung nhờ trợ lý"
            placeholder="Gõ việc cần làm, số container, biển số…"
            value={oNhap}
            onChange={(e) => setONhap(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                gui()
              }
            }}
          />
          <button type="button" title="Gửi" aria-label="Gửi" disabled={dangGo || !oNhap.trim()} onClick={gui}>
            <i className="bi bi-send-fill" />
          </button>
        </div>
      </div>
    </>
  )
}
