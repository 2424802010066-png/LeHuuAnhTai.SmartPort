import { createContext, useContext, useEffect, useRef, useState } from 'react'
import { API_URL, docPhien, goiApi } from '../api/client'

// =====================================================================
// NHAN SU KIEN REALTIME tu SmartPort.Api qua Server-Sent Events (SSE).
// Su kien la cua MOI module (cong.*, bai.*, tai-chinh.*) — API doc tu bang Outbox,
// nen ca luot xe ghi tu ban MVC cung hien o day.
// Moi tab trinh duyet giu 1 ket noi; cac man hinh chi "dang ky nghe".
// Dung fetch (khong dung EventSource) de gui duoc header Authorization
// — token KHONG phai dat tren URL, khong bi ghi vao log may chu.
// =====================================================================
const SuKienContext = createContext(null)
const SO_SU_KIEN_GIU = 50

/** Tach luong text/event-stream thanh tung su kien { id, event, data } */
function tachSuKien(boDem, khiCo) {
  let viTri
  while ((viTri = boDem.indexOf('\n\n')) >= 0) {
    const khoi = boDem.slice(0, viTri)
    boDem = boDem.slice(viTri + 2)
    let loai = 'message'
    let id = null
    const dong = []
    for (const d of khoi.split('\n')) {
      if (d.startsWith(':')) continue // dong chu thich = nhip giu ket noi
      if (d.startsWith('event:')) loai = d.slice(6).trim()
      else if (d.startsWith('id:')) id = d.slice(3).trim()
      else if (d.startsWith('data:')) dong.push(d.slice(5).trimStart())
    }
    if (dong.length) {
      try {
        khiCo(loai, id, JSON.parse(dong.join('\n')))
      } catch {
        /* bo qua goi tin hong */
      }
    }
  }
  return boDem
}

export function SuKienProvider({ children }) {
  const [trangThai, setTrangThai] = useState('dang-noi')
  const [ganDay, setGanDay] = useState([])
  const nguoiNghe = useRef(new Set())

  useEffect(() => {
    const huy = new AbortController()
    const daNhan = new Set()
    let idCuoi = 0
    let choThuLai = 0

    function nhan(suKien, phatChoNguoiNghe) {
      if (suKien.id != null) {
        if (daNhan.has(suKien.id)) return // chong trung giua lich su va luong truc tiep
        daNhan.add(suKien.id)
        idCuoi = Math.max(idCuoi, suKien.id)
      }
      setGanDay((ds) =>
        [suKien, ...ds.filter((x) => x.id == null || x.id !== suKien.id)].sort((a, b) => (b.id ?? 0) - (a.id ?? 0)).slice(0, SO_SU_KIEN_GIU),
      )
      if (phatChoNguoiNghe) nguoiNghe.current.forEach((f) => f(suKien))
    }

    async function ketNoi() {
      // Lich su gan day de bang tin khong trong ngay khi vua mo trang
      try {
        const lichSu = await goiApi('/api/su-kien/gan-day?soLuong=20')
        if (!huy.signal.aborted) lichSu.forEach((sk) => nhan(sk, false))
      } catch {
        /* chua co lich su van nghe tiep duoc */
      }

      while (!huy.signal.aborted) {
        const token = docPhien()?.token
        try {
          setTrangThai('dang-noi')
          const headers = { Accept: 'text/event-stream', Authorization: `Bearer ${token}` }
          // Noi lai sau khi mat ket noi: xin may chu phat bu cac su kien bi lo
          if (idCuoi > 0) headers['Last-Event-ID'] = String(idCuoi)
          const phanHoi = await fetch(API_URL + '/api/su-kien', { headers, signal: huy.signal, cache: 'no-store' })
          if (phanHoi.status === 401) {
            window.dispatchEvent(new Event('smartport:het-phien'))
            return
          }
          if (!phanHoi.ok || !phanHoi.body) throw new Error('HTTP ' + phanHoi.status)
          setTrangThai('da-noi')
          choThuLai = 0
          const doc = phanHoi.body.pipeThrough(new TextDecoderStream()).getReader()
          let boDem = ''
          for (;;) {
            const { value, done } = await doc.read()
            if (done) break
            boDem = tachSuKien((boDem + value).replace(/\r\n/g, '\n'), (loai, id, duLieu) => {
              if (loai === 'xin-chao') return
              nhan({ ...duLieu, loai, id: id != null ? Number(id) : duLieu.id }, true)
            })
          }
          throw new Error('Máy chủ đóng luồng')
        } catch {
          if (huy.signal.aborted) return
          setTrangThai('mat-ket-noi')
          // Thu lai gian dan 2s, 4s, 8s... toi da 30s — may chu khoi dong lai van tu noi lai
          choThuLai = Math.min(30000, choThuLai ? choThuLai * 2 : 2000)
          await new Promise((ok) => setTimeout(ok, choThuLai))
        }
      }
    }
    ketNoi()
    return () => huy.abort()
  }, [])

  const giaTri = { trangThai, ganDay, nguoiNghe: nguoiNghe.current }
  return <SuKienContext.Provider value={giaTri}>{children}</SuKienContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSuKien() {
  return useContext(SuKienContext)
}

/** Loai su kien khop mau: '*' = tat ca, 'bai.*' = moi su kien cua module Bai, hoac ten chinh xac */
function khopMau(mau, loai) {
  if (mau === '*') return true
  return mau.endsWith('*') ? loai.startsWith(mau.slice(0, -1)) : loai === mau
}

/** Dang ky nghe su kien (theo mau loai) trong suot vong doi component */
// eslint-disable-next-line react-refresh/only-export-components
export function useNgheSuKien(mau, xuLy) {
  // Tap nguoi nghe la 1 doi tuong co dinh -> khong dang ky lai moi lan co su kien moi
  const nguoiNghe = useContext(SuKienContext)?.nguoiNghe
  const xuLyMoiNhat = useRef(xuLy)
  useEffect(() => {
    xuLyMoiNhat.current = xuLy
  })
  useEffect(() => {
    if (!nguoiNghe) return undefined
    const f = (sk) => {
      if (khopMau(mau, sk.loai)) xuLyMoiNhat.current(sk)
    }
    nguoiNghe.add(f)
    return () => nguoiNghe.delete(f)
  }, [nguoiNghe, mau])
}
