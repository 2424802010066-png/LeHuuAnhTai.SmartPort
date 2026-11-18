import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react'

// =====================================================================
// THONG BAO NHANH (toast): "Đã lưu", "Không xóa được vì..." hien goc phai tren, tu tat.
// Thay cho TempData["Ok"] / TempData["Err"] cua ban MVC.
//   const bao = useThongBao(); bao.thanhCong('Đã lưu'); bao.loi(err)
// =====================================================================
const ThongBaoContext = createContext(null)

const KIEU = {
  thanhCong: { lop: 'text-bg-success', icon: 'bi-check-circle-fill', giay: 4 },
  loi: { lop: 'text-bg-danger', icon: 'bi-exclamation-octagon-fill', giay: 7 },
  canhBao: { lop: 'text-bg-warning', icon: 'bi-exclamation-triangle-fill', giay: 6 },
  thongTin: { lop: 'text-bg-primary', icon: 'bi-info-circle-fill', giay: 4 },
}

export function ThongBaoNhanhProvider({ children }) {
  const [ds, setDs] = useState([])
  const soThuTu = useRef(0)

  const dong = useCallback((id) => setDs((x) => x.filter((t) => t.id !== id)), [])

  const hien = useCallback(
    (kieu, noiDung) => {
      if (!noiDung) return
      const id = ++soThuTu.current
      const k = KIEU[kieu] || KIEU.thongTin
      setDs((x) => [...x.slice(-4), { id, kieu, noiDung: String(noiDung) }])
      setTimeout(() => dong(id), k.giay * 1000)
    },
    [dong],
  )

  const api = useMemo(
    () => ({
      thanhCong: (m) => hien('thanhCong', m),
      loi: (m) => hien('loi', m?.message ?? m),
      canhBao: (m) => hien('canhBao', m),
      thongTin: (m) => hien('thongTin', m),
    }),
    [hien],
  )

  return (
    <ThongBaoContext.Provider value={api}>
      {children}
      <div className="sp-toast-vung" aria-live="polite">
        {ds.map((t) => {
          const k = KIEU[t.kieu] || KIEU.thongTin
          return (
            <div key={t.id} className={'sp-toast ' + k.lop} role={t.kieu === 'loi' ? 'alert' : 'status'}>
              <i className={'bi ' + k.icon} />
              <div className="flex-grow-1">{t.noiDung}</div>
              <button type="button" className="btn-close btn-close-white btn-sm" aria-label="Đóng" onClick={() => dong(t.id)} />
            </div>
          )
        })}
      </div>
    </ThongBaoContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useThongBao() {
  const ctx = useContext(ThongBaoContext)
  if (!ctx) throw new Error('useThongBao phải dùng bên trong ThongBaoNhanhProvider')
  return ctx
}
