import { createContext, useCallback, useContext, useRef, useState } from 'react'
import HopThoai from './HopThoai'

// =====================================================================
// HOP XAC NHAN dung chung: thay confirm() cua trinh duyet.
//   const xacNhan = useXacNhan()
//   if (!(await xacNhan({ tieuDe: 'Xóa tàu?', noiDung: 'Không hoàn tác được.', nutDongY: 'Xóa', mau: 'danger' }))) return
// =====================================================================
const XacNhanContext = createContext(null)

export function XacNhanProvider({ children }) {
  const [hoi, setHoi] = useState(null)
  const traLoi = useRef(null)

  const xacNhan = useCallback(
    (tuyChon) =>
      new Promise((resolve) => {
        traLoi.current = resolve
        setHoi({ tieuDe: 'Xác nhận', nutDongY: 'Đồng ý', nutHuy: 'Thôi', mau: 'primary', ...tuyChon })
      }),
    [],
  )

  const ketThuc = (dongY) => {
    traLoi.current?.(dongY)
    traLoi.current = null
    setHoi(null)
  }

  return (
    <XacNhanContext.Provider value={xacNhan}>
      {children}
      <HopThoai
        mo={!!hoi}
        dong={() => ketThuc(false)}
        tieuDe={hoi?.tieuDe}
        icon={hoi?.mau === 'danger' ? 'bi-exclamation-triangle' : 'bi-question-circle'}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => ketThuc(false)}>
              {hoi?.nutHuy}
            </button>
            <button type="button" className={'btn btn-' + (hoi?.mau || 'primary')} onClick={() => ketThuc(true)} autoFocus>
              {hoi?.nutDongY}
            </button>
          </>
        }
      >
        <div style={{ whiteSpace: 'pre-line' }}>{hoi?.noiDung}</div>
      </HopThoai>
    </XacNhanContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useXacNhan() {
  const ctx = useContext(XacNhanContext)
  if (!ctx) throw new Error('useXacNhan phải dùng bên trong XacNhanProvider')
  return ctx
}
