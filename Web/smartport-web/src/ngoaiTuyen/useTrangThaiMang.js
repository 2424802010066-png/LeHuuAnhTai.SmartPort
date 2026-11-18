import { useSyncExternalStore } from 'react'
import { tram } from './tram'

/**
 * Trang thai mang + hop thu di cua tram cho React (🟢 / 🟡 / 🔴).
 * Dang ky lan dau -> tram tu bat nhip /suc-khoe; component cuoi cung go ra -> tu tat (khong ro ri hen gio).
 */
export function useTrangThaiMang() {
  return useSyncExternalStore(tram.dangKy, tram.layTrangThai, tram.layTrangThai)
}
