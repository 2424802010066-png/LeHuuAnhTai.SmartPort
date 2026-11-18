import { useId } from 'react'

/**
 * Logo thuong hieu SmartPort (chuyen tu Views/Shared/_Logo.cshtml sang SVG trong React).
 * Ma cua gradient duoc sinh rieng cho moi lan ve (useId) de dat 2 logo tren cung mot trang
 * — dau trang va chan trang — khong an mau cua nhau.
 */
export default function LogoSmartPort({ canh = 38, lop = 'ck-logo' }) {
  const id = useId().replace(/:/g, '')
  const nen = 'n' + id
  const sang = 's' + id
  const troi = 't' + id
  const than = 'h' + id
  return (
    <svg className={lop} width={canh} height={canh} viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="Logo SmartPort">
      <defs>
        <linearGradient id={nen} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#083058" />
          <stop offset=".5" stopColor="#0d4f8b" />
          <stop offset="1" stopColor="#14b8d4" />
        </linearGradient>
        <radialGradient id={sang} cx=".3" cy=".18" r=".9">
          <stop offset="0" stopColor="#ffffff" stopOpacity=".28" />
          <stop offset=".45" stopColor="#ffffff" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={troi} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffd98a" />
          <stop offset="1" stopColor="#ff9d45" />
        </linearGradient>
        <linearGradient id={than} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0b2d51" />
          <stop offset="1" stopColor="#061c35" />
        </linearGradient>
      </defs>

      <rect x="2" y="2" width="60" height="60" rx="17" fill={'url(#' + nen + ')'} />
      <rect x="2" y="2" width="60" height="60" rx="17" fill={'url(#' + sang + ')'} />
      <rect x="4.2" y="4.2" width="55.6" height="55.6" rx="14.5" fill="none" stroke="#ffffff" strokeOpacity=".22" strokeWidth="1.3" />

      <circle cx="47" cy="17" r="9" fill={'url(#' + troi + ')'} opacity=".35" />
      <circle cx="47" cy="17" r="5.5" fill={'url(#' + troi + ')'} />

      <g stroke="#ffffff" strokeWidth="3.4" strokeLinecap="round" fill="none">
        <path d="M15 40 V15" />
        <path d="M15 17 H41" />
        <path d="M15 17 L9 25" />
      </g>
      <line x1="33" y1="17" x2="33" y2="25" stroke="#ffffff" strokeWidth="1.8" />
      <rect x="27.5" y="25" width="11" height="8" rx="1.8" fill="#ff7d2d" stroke="#ffffff" strokeOpacity=".35" strokeWidth=".8" />

      <rect x="20" y="36.5" width="8.5" height="6" rx="1.2" fill="#22d3ee" />
      <rect x="29.5" y="36.5" width="8.5" height="6" rx="1.2" fill="#ffb25e" />
      <rect x="39" y="36.5" width="8.5" height="6" rx="1.2" fill="#4ade80" />
      <rect x="46" y="30.5" width="5.5" height="12" rx="1.2" fill="#e9eef5" />
      <rect x="47.2" y="32.3" width="3.1" height="1.7" rx=".6" fill="#0d3b66" />
      <path d="M14 43 H53 L48.5 51 H18.5 Z" fill={'url(#' + than + ')'} stroke="#ffffff" strokeOpacity=".18" strokeWidth=".8" />

      <path d="M11 55 q5 -3.4 10 0 t10 0 t10 0 t10 0" stroke="#7fd0f5" strokeWidth="2.6" fill="none" strokeLinecap="round" />
      <path d="M16 59.5 q5 -3.4 10 0 t10 0 t10 0" stroke="#3f8fc9" strokeWidth="2.2" fill="none" strokeLinecap="round" opacity=".8" />

      <circle cx="52.5" cy="9.5" r="2.6" fill="#7CFC9A" />
    </svg>
  )
}
