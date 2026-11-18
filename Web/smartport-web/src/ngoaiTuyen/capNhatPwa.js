// =====================================================================
// CAP NHAT PHIEN BAN AN TOAN (PWA)
// Service worker moi KHONG tu chiem quyen (khong skipWaiting): trang dang mo van chay tron bo ban cu (vo + chunk cu).
// Khi co ban moi dang cho -> thanh trang thai hien "Có bản cập nhật"; nguoi dung bam -> gui KICH_HOAT cho SW moi,
// SW moi chiem quyen (controllerchange) -> CHI tab vua bam tai lai. Hang doi luot xe nam trong IndexedDB nen tai lai khong mat.
// =====================================================================
import { useSyncExternalStore } from 'react'

let coBanMoi = false
let dangKyHienTai = null
const nguoiNghe = new Set()

function dat(v) {
  if (coBanMoi === v) return
  coBanMoi = v
  nguoiNghe.forEach((f) => f())
}

/** Goi tu main.jsx sau khi dang ky /sw.js */
export function theoDoiCapNhat(reg) {
  dangKyHienTai = reg
  const kiem = () => {
    // controller co san = trang dang do SW cu dieu khien -> ban moi la "cap nhat" (lan cai dau thi khong can bao)
    if (reg.waiting && navigator.serviceWorker.controller) dat(true)
  }
  kiem()
  reg.addEventListener('updatefound', () => {
    const w = reg.installing
    w?.addEventListener('statechange', () => {
      if (w.state === 'installed') kiem()
    })
  })
}

/** Nguoi dung bam "Tai lai de cap nhat" */
export function kichHoatBanMoi() {
  const w = dangKyHienTai?.waiting
  if (!w) {
    window.location.reload()
    return
  }
  let daTai = false
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (daTai) return
    daTai = true
    window.location.reload()
  })
  w.postMessage({ loai: 'KICH_HOAT' })
}

const dangKy = (f) => {
  nguoiNghe.add(f)
  return () => nguoiNghe.delete(f)
}
const lay = () => coBanMoi

export function useCoBanMoi() {
  return useSyncExternalStore(dangKy, lay, lay)
}
