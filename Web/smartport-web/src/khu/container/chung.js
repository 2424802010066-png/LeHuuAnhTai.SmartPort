import { useCallback } from 'react'
import { goiApi } from '../../api/client'
import { useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'

// =====================================================================
// Dung chung cho cac man hinh khu CONTAINER & BAI
// =====================================================================

export const API_CONT = '/api/quan-ly-container'

/** 4 trang thai chuan cua ban MVC (API tra them trang thai khac neu CSDL co) */
export const TRANG_THAI_MAC_DINH = ['Trong bãi', 'Chờ xuất tàu', 'Đã xuất tàu', 'Đã giao khách']

export const duongHoSo = (maCont) => '/admin/containers/hoso/' + maCont
export const duongForm = (maCont) => '/admin/containers/form' + (maCont ? '/' + maCont : '')
export const duongEir = (maCont) => '/admin/containers/eir/' + maCont

/** Vong doi: 1 tren tau -> 2 trong bai -> 3 cho giao/xuat -> 4 hoan tat (giong ho so MVC) */
export function buocVongDoi(trangThai) {
  const tt = trangThai || 'Trong bãi'
  if (tt === 'Đã giao khách' || tt === 'Đã xuất tàu') return 4
  if (tt === 'Chờ xuất tàu' || tt === 'Chờ giao') return 3
  return 2
}

/** Duong dan noi bo ("/Admin/GatePass/In/5") -> <Link>; con lai (http...) mo tab moi */
export const laDuongNoiBo = (u) => typeof u === 'string' && u.startsWith('/') && !u.startsWith('//')

/** Mau hex hop le cho cham dong thoi gian (du lieu tu API — khong dua chuoi la vao style) */
export const mauAnToan = (m, macDinh = '#64748b') => (/^#[0-9a-f]{3,8}$/i.test(m || '') ? m : macDinh)

/** "-18" / "-17,5" theo kieu Viet Nam */
export const soDo = (v) => (v == null ? '—' : Number(v).toLocaleString('vi-VN', { maximumFractionDigits: 1 }))

/**
 * Gate out (giao container cho khach): hoi xac nhan -> POST giao-khach -> thong bao nhanh
 * (API tra san so phieu luu bai + so tien trong thongBao, giong TempData["Ok"] cua MVC).
 */
export function useGiaoKhach(khiXong) {
  const xacNhan = useXacNhan()
  const { chay, dangChay } = useThaoTac()

  const giaoKhach = useCallback(
    async (c) => {
      const dongY = await xacNhan({
        tieuDe: 'Gate out container?',
        noiDung: `Giao container ${c.soCont} cho khách (gate out)?\nĐồng hồ lưu bãi dừng, phí phát sinh được chốt thành phiếu thu.`,
        nutDongY: 'Gate out',
        mau: 'success',
      })
      if (!dongY) return undefined
      const kq = await chay(() => goiApi(`${API_CONT}/${c.maCont}/giao-khach`, { method: 'POST' }))
      if (kq) khiXong?.(kq)
      return kq
    },
    [xacNhan, chay, khiXong],
  )

  return { giaoKhach, dangGiao: dangChay }
}
