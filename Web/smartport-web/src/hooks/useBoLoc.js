import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'

/**
 * Bo loc cua man hinh danh sach LUU TREN URL (?q=MSCU&trangThai=Trong+bãi&trang=2):
 * tai lai trang, bam Back, gui link cho dong nghiep — van giu dung bo loc.
 *
 *   const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', trangThai: '', trang: 1 })
 *   const { duLieu } = useApi('/api/container' + taoQuery(loc))
 *   datLoc({ q: 'MSCU', trang: 1 })
 *
 * Kieu gia tri theo gia tri mac dinh: so -> Number, true/false -> boolean, con lai -> chuoi.
 */
export function useBoLoc(macDinh) {
  const [thamSo, setThamSo] = useSearchParams()
  // macDinh thuong la object viet thang trong component -> so sanh theo noi dung, khong theo tham chieu
  const khoaMacDinh = JSON.stringify(macDinh)

  const loc = useMemo(() => {
    const md = JSON.parse(khoaMacDinh)
    const kq = {}
    for (const [k, v] of Object.entries(md)) {
      const s = thamSo.get(k)
      if (s === null) kq[k] = v
      else if (typeof v === 'number') kq[k] = Number.isNaN(Number(s)) ? v : Number(s)
      else if (typeof v === 'boolean') kq[k] = s === 'true' || s === '1'
      else kq[k] = s
    }
    return kq
  }, [thamSo, khoaMacDinh])

  const datLoc = useCallback(
    (thayDoi) => {
      const md = JSON.parse(khoaMacDinh)
      setThamSo(
        (cu) => {
          const moi = new URLSearchParams(cu)
          for (const [k, v] of Object.entries(thayDoi)) {
            // Gia tri bang mac dinh thi bo khoi URL cho gon
            if (v === undefined || v === null || v === '' || v === md[k]) moi.delete(k)
            else moi.set(k, String(v))
          }
          return moi
        },
        { replace: true },
      )
    },
    [setThamSo, khoaMacDinh],
  )

  const xoaLoc = useCallback(() => setThamSo(new URLSearchParams(), { replace: true }), [setThamSo])

  return [loc, datLoc, xoaLoc]
}
