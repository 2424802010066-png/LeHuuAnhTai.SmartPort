import { useEffect, useRef, useState } from 'react'
import { dinhDangSo } from '../../utils/dinhDang'

/**
 * So lieu lon tu dem tu 0 len gia tri that khi cuon toi (chi bang requestAnimationFrame).
 *   <SoDem den={3280000} sau=" TEU" />
 */
export default function SoDem({ den = 0, truoc = '', sau = '', thoiGian = 1100 }) {
  const oRef = useRef(null)
  const [gt, setGt] = useState(0)

  useEffect(() => {
    const el = oRef.current
    if (!el) return undefined
    const itDong = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    if (itDong || !('IntersectionObserver' in window)) {
      setGt(den)
      return undefined
    }
    let khung = 0
    const doi = new IntersectionObserver(
      (muc) => {
        if (!muc[0].isIntersecting) return
        doi.disconnect()
        const batDau = performance.now()
        const chay = (luc) => {
          const t = Math.min(1, (luc - batDau) / thoiGian)
          // easeOutCubic: chay nhanh luc dau, cham dan ve dich
          setGt(Math.round(den * (1 - Math.pow(1 - t, 3))))
          if (t < 1) khung = requestAnimationFrame(chay)
        }
        khung = requestAnimationFrame(chay)
      },
      { threshold: 0.3 },
    )
    doi.observe(el)
    return () => {
      doi.disconnect()
      cancelAnimationFrame(khung)
    }
  }, [den, thoiGian])

  return (
    <span ref={oRef}>
      {truoc}
      {dinhDangSo(gt)}
      {sau}
    </span>
  )
}
