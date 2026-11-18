import { useState } from 'react'

const KHOA = 'sp-theme'

// eslint-disable-next-line react-refresh/only-export-components
export function apDungGiaoDien(ten) {
  document.documentElement.setAttribute('data-bs-theme', ten)
  try {
    localStorage.setItem(KHOA, ten)
  } catch {
    /* bo qua */
  }
}

/** Nut doi giao dien sang / toi (Bootstrap 5.3 data-bs-theme). Lua chon duoc nho trong trinh duyet. */
export default function NutGiaoDien() {
  const [toi, setToi] = useState(() => document.documentElement.getAttribute('data-bs-theme') === 'dark')
  return (
    <button
      type="button"
      className="sp-nut-tron me-2"
      title={toi ? 'Chuyển giao diện sáng' : 'Chuyển giao diện tối'}
      aria-label="Đổi giao diện sáng/tối"
      onClick={() => {
        apDungGiaoDien(toi ? 'light' : 'dark')
        setToi(!toi)
      }}
    >
      <i className={'bi ' + (toi ? 'bi-sun-fill' : 'bi-moon-stars-fill')} />
    </button>
  )
}
