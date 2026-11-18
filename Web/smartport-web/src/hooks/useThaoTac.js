import { useCallback, useRef, useState } from 'react'
import { useThongBao } from '../components/ui/ThongBaoNhanh'

/**
 * Chay mot thao tac GHI (luu, xoa, duyet...) va tu bao ket qua bang thong bao nhanh.
 *
 *   const { chay, dangChay } = useThaoTac()
 *   const kq = await chay(() => goiApi('/api/tau', { method: 'POST', body: gt }))
 *   if (kq) { dongHopThoai(); taiLai() }
 *
 * - Thanh cong: hien kq.thongBao cua API (hoac tuyChon.thanhCong), tra ve ket qua.
 * - Loi: hien thong bao loi tieng Viet cua API, tra ve undefined (khong nem loi ra ngoai).
 * - Chan bam 2 lan lien tiep khi thao tac truoc chua xong.
 */
export function useThaoTac() {
  const bao = useThongBao()
  const [dangChay, setDangChay] = useState(false)
  const khoa = useRef(false)

  const chay = useCallback(
    async (hanhDong, { thanhCong, imLang = false } = {}) => {
      if (khoa.current) return undefined
      khoa.current = true
      setDangChay(true)
      try {
        const kq = await hanhDong()
        if (!imLang) {
          const loiNhan = thanhCong ?? kq?.thongBao
          if (loiNhan) bao.thanhCong(loiNhan)
        }
        return kq ?? true
      } catch (e) {
        bao.loi(e)
        return undefined
      } finally {
        khoa.current = false
        setDangChay(false)
      }
    },
    [bao],
  )

  return { chay, dangChay }
}
