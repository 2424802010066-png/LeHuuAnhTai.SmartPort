import { useEffect } from 'react'

const ITMOTION = '(prefers-reduced-motion: reduce)'

/**
 * Hien dan cac khoi co lop "ck-hien" khi nguoi dung cuon toi (IntersectionObserver —
 * khong can thu vien hieu ung nao). Goi lai moi khi du lieu doi de bat cac khoi moi ve.
 *   useHienDan([duLieu])
 */
export function useHienDan(phuThuoc = []) {
  useEffect(() => {
    const cacKhoi = Array.from(document.querySelectorAll('.ck-hien:not(.ck-ro)'))
    if (cacKhoi.length === 0) return undefined
    if (!('IntersectionObserver' in window) || window.matchMedia?.(ITMOTION).matches) {
      cacKhoi.forEach((el) => el.classList.add('ck-ro'))
      return undefined
    }
    const doi = new IntersectionObserver(
      (muc) => {
        muc.forEach((m) => {
          if (!m.isIntersecting) return
          const tre = Number(m.target.dataset.cham || 0)
          setTimeout(() => m.target.classList.add('ck-ro'), tre)
          doi.unobserve(m.target)
        })
      },
      { threshold: 0.12, rootMargin: '0px 0px -40px 0px' },
    )
    cacKhoi.forEach((el) => doi.observe(el))
    return () => doi.disconnect()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, phuThuoc)
}

/** Thanh tien do cuon trang (mong o dinh man hinh) */
export function useTienDoCuon(ref) {
  useEffect(() => {
    const ve = () => {
      const el = ref.current
      if (!el) return
      const cao = document.documentElement.scrollHeight - window.innerHeight
      el.style.width = (cao > 0 ? Math.min(100, (window.scrollY / cao) * 100) : 0) + '%'
    }
    ve()
    window.addEventListener('scroll', ve, { passive: true })
    window.addEventListener('resize', ve)
    return () => {
      window.removeEventListener('scroll', ve)
      window.removeEventListener('resize', ve)
    }
  }, [ref])
}
