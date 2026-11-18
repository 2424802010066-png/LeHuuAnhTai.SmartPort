import { useCallback, useState } from 'react'

/**
 * State cho form nhap lieu.
 *   const { gt, doi, datGt, datLai } = useBieuMau({ tenTau: '', trongTai: '' })
 *   <input value={gt.tenTau} onChange={doi('tenTau')} />
 *   <input type="checkbox" checked={gt.kichHoat} onChange={doi('kichHoat')} />
 *   datLai(dongDangSua)   // mo form sua: nap du lieu cu
 * doi() tu doc checkbox -> boolean, input so -> giu chuoi (API tu doc so), select -> chuoi.
 */
export function useBieuMau(giaTriDau) {
  const [gt, setGt] = useState(giaTriDau)

  const doi = useCallback(
    (ten) => (e) => {
      const v = e?.target ? (e.target.type === 'checkbox' ? e.target.checked : e.target.value) : e
      setGt((cu) => ({ ...cu, [ten]: v }))
    },
    [],
  )

  const datGt = useCallback((thayDoi) => setGt((cu) => ({ ...cu, ...thayDoi })), [])
  const datLai = useCallback((moi) => setGt(moi), [])

  return { gt, doi, datGt, datLai }
}

/** Chuoi rong -> null, chuoi so -> Number: dung khi gui form len API (truong so khong bat buoc) */
export const soHoacNull = (v) => (v === '' || v === null || v === undefined ? null : Number(v))
export const chuoiHoacNull = (v) => (v === '' || v === undefined ? null : v)
