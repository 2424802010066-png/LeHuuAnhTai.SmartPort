import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'

/**
 * TIM KIEM TOAN CUC tren thanh tren (Ctrl+K): go so container / bien so / so lenh / ten khach
 * -> ra ket qua o moi phan he, bam la nhay thang toi ho so. Mui ten len/xuong + Enter, Esc de dong.
 */
export default function TimToanCuc() {
  const [chu, setChu] = useState('')
  const [ketQua, setKetQua] = useState(null)
  const [mo, setMo] = useState(false)
  const [chon, setChon] = useState(-1)
  const [dangTim, setDangTim] = useState(false)
  const oNhap = useRef(null)
  const khung = useRef(null)
  const hen = useRef(null)
  const lanGoi = useRef(0)
  const dieuHuong = useNavigate()

  useEffect(() => {
    const phimTat = (e) => {
      if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
        e.preventDefault()
        oNhap.current?.focus()
        oNhap.current?.select()
      }
    }
    const bamNgoai = (e) => {
      if (khung.current && !khung.current.contains(e.target)) setMo(false)
    }
    document.addEventListener('keydown', phimTat)
    document.addEventListener('mousedown', bamNgoai)
    return () => {
      document.removeEventListener('keydown', phimTat)
      document.removeEventListener('mousedown', bamNgoai)
      clearTimeout(hen.current)
    }
  }, [])

  function tim(q) {
    clearTimeout(hen.current)
    if (q.length < 2) {
      setKetQua(null)
      setMo(false)
      return
    }
    hen.current = setTimeout(async () => {
      const so = ++lanGoi.current
      setDangTim(true)
      try {
        const kq = await goiApi('/api/he-thong/tim-kiem' + taoQuery({ q }))
        if (so === lanGoi.current) {
          setKetQua(kq || [])
          setChon(-1)
          setMo(true)
        }
      } catch {
        if (so === lanGoi.current) setMo(false)
      } finally {
        if (so === lanGoi.current) setDangTim(false)
      }
    }, 220)
  }

  function moKetQua(muc) {
    if (!muc?.url) return
    setMo(false)
    setChu('')
    setKetQua(null)
    if (/^https?:/i.test(muc.url)) window.open(muc.url, '_blank', 'noopener')
    else dieuHuong(muc.url)
  }

  function phim(e) {
    if (e.key === 'Escape') {
      setMo(false)
      oNhap.current?.blur()
      return
    }
    if (!mo || !ketQua?.length) return
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault()
      setChon((c) => {
        const n = ketQua.length
        return e.key === 'ArrowDown' ? (c + 1) % n : (c - 1 + n) % n
      })
    } else if (e.key === 'Enter') {
      e.preventDefault()
      moKetQua(ketQua[chon >= 0 ? chon : 0])
    }
  }

  return (
    <div className="sp-tim-toan-cuc" ref={khung}>
      <i className="bi bi-search sp-tim-icon" />
      <input
        ref={oNhap}
        type="text"
        className="form-control form-control-sm"
        placeholder="Tìm số container, biển số, số lệnh, khách hàng…"
        aria-label="Tìm kiếm toàn hệ thống"
        autoComplete="off"
        spellCheck={false}
        value={chu}
        onChange={(e) => {
          setChu(e.target.value)
          tim(e.target.value.trim())
        }}
        onFocus={() => ketQua && chu.trim().length >= 2 && setMo(true)}
        onKeyDown={phim}
      />
      {dangTim ? <span className="spinner-border spinner-border-sm sp-tim-kbd text-muted" /> : <kbd className="sp-tim-kbd">Ctrl K</kbd>}
      {mo && (
        <div className="sp-tim-ket-qua shadow" role="listbox">
          {!ketQua?.length ? (
            <div className="p-3 small text-muted">
              Không tìm thấy “{chu}”.
              <div style={{ fontSize: '.75rem' }}>Thử số container (MSCU1234567), biển số xe, số lệnh hoặc tên khách hàng.</div>
            </div>
          ) : (
            ketQua.map((m, i) => (
              <button
                type="button"
                key={i + m.url}
                role="option"
                aria-selected={i === chon}
                className={'sp-tim-hang' + (i === chon ? ' chon' : '')}
                onMouseEnter={() => setChon(i)}
                onClick={() => moKetQua(m)}
              >
                <span className="sp-tim-cham" style={{ background: m.mau || '#0d4f8b' }}>
                  <i className={'bi ' + (m.icon || 'bi-dot')} />
                </span>
                <span className="min-w-0 flex-grow-1 text-start">
                  <span className="d-block fw-semibold text-truncate">{m.tieuDe}</span>
                  <span className="d-block small text-muted text-truncate">{m.moTa}</span>
                </span>
                <span className="sp-tim-nhom">{m.nhom}</span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
