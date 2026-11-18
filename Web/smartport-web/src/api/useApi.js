import { useCallback, useEffect, useRef, useState } from 'react'
import { goiApi } from './client'

/**
 * Hook doc du lieu tu API: tu goi khi duong dan doi, tra ve trang thai tai / loi.
 * taiLai({ im: true }) lam moi ngam (khong hien vong xoay) — dung khi nhan su kien realtime.
 */
export function useApi(duong, { coToken = true } = {}) {
  const [duLieu, setDuLieu] = useState(null)
  const [loi, setLoi] = useState(null)
  const [dangTai, setDangTai] = useState(!!duong)
  const lanGoi = useRef(0)

  const taiLai = useCallback(
    async ({ im = false } = {}) => {
      if (!duong) return
      const so = ++lanGoi.current
      if (!im) setDangTai(true)
      try {
        const kq = await goiApi(duong, { coToken })
        if (so === lanGoi.current) {
          setDuLieu(kq)
          setLoi(null)
        }
      } catch (e) {
        if (so === lanGoi.current) setLoi(e)
      } finally {
        if (so === lanGoi.current) setDangTai(false)
      }
    },
    [duong, coToken],
  )

  useEffect(() => {
    taiLai()
  }, [taiLai])

  return { duLieu, loi, dangTai, taiLai }
}
