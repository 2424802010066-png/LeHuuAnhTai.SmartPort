import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useNgheSuKien } from '../../realtime/SuKienContext'

/**
 * Chuong thong bao: su co an ninh moi, don cho duyet, tau sap cap, lien he moi.
 * Hoi lai moi 60 giay va ngay khi co su kien nghiep vu (khong can F5).
 */
export default function ChuongThongBao() {
  const [d, setD] = useState(null)
  const [mo, setMo] = useState(false)
  const khung = useRef(null)
  const hen = useRef(null)
  const dieuHuong = useNavigate()

  async function nap() {
    try {
      setD(await goiApi('/api/he-thong/thong-bao'))
    } catch {
      /* chuong khong phai chuc nang chinh: loi mang thi giu so cu */
    }
  }

  useEffect(() => {
    nap()
    const lap = setInterval(nap, 60000)
    const bamNgoai = (e) => {
      if (khung.current && !khung.current.contains(e.target)) setMo(false)
    }
    document.addEventListener('mousedown', bamNgoai)
    return () => {
      clearInterval(lap)
      clearTimeout(hen.current)
      document.removeEventListener('mousedown', bamNgoai)
    }
  }, [])

  useNgheSuKien('*', () => {
    clearTimeout(hen.current)
    hen.current = setTimeout(nap, 1500)
  })

  const tong = d?.tong ?? 0
  return (
    <div className="position-relative me-2" ref={khung}>
      <button type="button" className="sp-nut-tron" title="Thông báo" aria-label={'Thông báo: ' + tong} onClick={() => setMo((x) => !x)}>
        <i className="bi bi-bell-fill" />
        {tong > 0 && <span className="sp-so-thong-bao">{tong > 99 ? '99+' : tong}</span>}
      </button>
      {mo && (
        <div className="dropdown-menu dropdown-menu-end show shadow sp-hop-thong-bao">
          <div className="dropdown-header fw-bold">
            <i className="bi bi-bell me-1" />
            Cần chú ý
          </div>
          {!d?.items?.length ? (
            <div className="dropdown-item-text small text-muted">Không có gì cần chú ý.</div>
          ) : (
            d.items.map((m) => (
              <button
                type="button"
                key={m.url}
                className="dropdown-item small d-flex align-items-center gap-2"
                onClick={() => {
                  setMo(false)
                  dieuHuong(m.url)
                }}
              >
                <span className={'badge text-bg-' + m.mau} style={{ width: 28 }}>
                  <i className={'bi ' + m.icon} />
                </span>
                <span className="text-wrap">{m.noiDung}</span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
