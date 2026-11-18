import { useEffect, useRef, useState } from 'react'

/**
 * O tim kiem tu goi khi ngung go 350 ms (khong ban request theo tung phim).
 *   <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số container, khách hàng…" />
 */
export default function OTimKiem({ giaTri = '', doi, goiY = 'Tìm…', cho = 350, lop = '', rong = 280 }) {
  const [chu, setChu] = useState(giaTri)
  const hen = useRef(null)
  const giaTriCuoi = useRef(giaTri)

  // Bo loc doi tu ben ngoai (bam "Xóa lọc", quay lai trang truoc) -> cap nhat o nhap
  useEffect(() => {
    if (giaTri !== giaTriCuoi.current) {
      giaTriCuoi.current = giaTri
      setChu(giaTri)
    }
  }, [giaTri])

  useEffect(() => () => clearTimeout(hen.current), [])

  const guiNgay = (v) => {
    clearTimeout(hen.current)
    if (v === giaTriCuoi.current) return
    giaTriCuoi.current = v
    doi?.(v)
  }

  return (
    <div className={'input-group input-group-sm ' + lop} style={{ maxWidth: rong }}>
      <span className="input-group-text bg-body">
        <i className="bi bi-search" />
      </span>
      <input
        type="search"
        className="form-control"
        placeholder={goiY}
        value={chu}
        aria-label={goiY}
        onChange={(e) => {
          const v = e.target.value
          setChu(v)
          clearTimeout(hen.current)
          hen.current = setTimeout(() => guiNgay(v.trim()), cho)
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault()
            guiNgay(chu.trim())
          }
        }}
      />
    </div>
  )
}
