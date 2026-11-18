import { useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import './tien-ich.css'

/**
 * Manh dung chung cua khu TIEN ICH CONG KHAI.
 * Trang cua khu nay nam TRONG khung website cong khai (BoCucCongKhai) nen o day
 * chi ve dai tieu de cua tung trang, KHONG ve dau/chan trang cua website.
 */

/** Dai tieu de + duong dan quay lai trang chu (thay "page-head" cua ban MVC) */
export function DauTrang({ icon, tieuDe, mucHienTai, cha, phu }) {
  return (
    <div className="ti-dau-trang mb-4">
      <div className="container">
        <h1>
          {icon && <i className={'bi ' + icon + ' me-2'} />}
          {tieuDe}
        </h1>
        <nav aria-label="Đường dẫn">
          <ol className="breadcrumb">
            <li className="breadcrumb-item">
              <Link to="/">Trang chủ</Link>
            </li>
            {cha && (
              <li className="breadcrumb-item">
                <Link to={cha.den}>{cha.ten}</Link>
              </li>
            )}
            <li className="breadcrumb-item active" aria-current="page">
              {mucHienTai || tieuDe}
            </li>
          </ol>
        </nav>
        {phu && <div className="small mt-2 text-white-50">{phu}</div>}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// LOC HTML BAI VIET
// Noi dung tin tuc la HTML do admin soan trong khu quan tri. Khong dung
// dangerouslySetInnerHTML: HTML duoc loc theo danh sach the/thuoc tinh cho phep roi
// moi chen vao DOM — cach lam giong bo loc cua tro ly (components/TroLy/TroLyNoi.jsx),
// nen du co ai do dan mot doan script vao bai viet cung khong chay duoc.
// ---------------------------------------------------------------------------

/** The duoc giu lai (the trinh bay bai bao). The la: bo vo, giu chu ben trong. */
const THE_CHO_PHEP = new Set([
  'A', 'B', 'STRONG', 'I', 'EM', 'U', 'SPAN', 'DIV', 'P', 'BR', 'HR', 'UL', 'OL', 'LI',
  'BLOCKQUOTE', 'CODE', 'PRE', 'SMALL', 'FIGURE', 'FIGCAPTION', 'IMG',
  'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD',
])

/** The bo han ca noi dung ben trong */
const THE_BO_HAN = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'LINK', 'META', 'SVG', 'MATH', 'FORM', 'INPUT'])

/** Chi nhan duong dan noi bo mot dau "/" hoac link http(s); bo javascript:, data:, vbscript:... */
function urlAnToan(u) {
  const s = (u || '').trim()
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
    } else if (t === 'title' || t === 'alt') {
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
    } else if (t === 'src' && ten === 'IMG') {
      const u = urlAnToan(a.value)
      if (u) {
        moi.setAttribute('src', u)
        moi.setAttribute('loading', 'lazy')
      }
    } else if ((t === 'colspan' || t === 'rowspan') && (ten === 'TD' || ten === 'TH')) {
      if (/^\d{1,2}$/.test(a.value)) moi.setAttribute(t, a.value)
    }
    // moi thuoc tinh khac (style, on*, srcset, formaction...) bi bo
  }
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

/** Noi dung bai viet da loc the, chen bang DOM chu khong qua dangerouslySetInnerHTML */
export function NoiDungBaiViet({ html, lop = 'ti-noi-dung-bai' }) {
  const o = useRef(null)
  useEffect(() => {
    if (o.current) o.current.replaceChildren(locHtml(html))
  }, [html])
  return <div className={lop} ref={o} />
}

/** Anh minh hoa: API luon tra URL, nhung anh hong / chan quang cao thi an di cho gon */
export function AnhTin({ src, alt, lop = 'ti-anh-tin' }) {
  return <img className={lop} src={src} alt={alt || ''} loading="lazy" onError={(e) => (e.currentTarget.style.visibility = 'hidden')} />
}
