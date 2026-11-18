import { useEffect, useId } from 'react'

/**
 * Hop thoai (modal) kieu Bootstrap nhung dieu khien bang state React — khong can bootstrap.js.
 *   <HopThoai mo={mo} dong={() => setMo(false)} tieuDe="Thêm tàu" chan={<nút...>}>...</HopThoai>
 * kichThuoc: 'sm' | 'lg' | 'xl'. Esc hoac bam nen toi de dong (tru khi khoaDong = true, vd dang luu).
 *
 * Hop thoai NHAP LIEU: truyen khiGui -> than + chan nam trong 1 <form>, nut type="submit" o chan
 * gui duoc form va go Enter trong o nhap cung luu (khong can viet onKeyDown):
 *   <HopThoai mo khiGui={luu} chan={<button type="submit">Lưu</button>}>...o nhap...</HopThoai>
 */
export default function HopThoai({ mo, dong, tieuDe, children, chan, kichThuoc, khoaDong = false, icon, khiGui }) {
  const idTieuDe = useId()

  useEffect(() => {
    if (!mo) return undefined
    const khiBam = (e) => {
      if (e.key === 'Escape' && !khoaDong) dong?.()
    }
    document.addEventListener('keydown', khiBam)
    document.body.classList.add('modal-open')
    return () => {
      document.removeEventListener('keydown', khiBam)
      document.body.classList.remove('modal-open')
    }
  }, [mo, dong, khoaDong])

  if (!mo) return null
  return (
    <>
      <div className="modal-backdrop fade show" />
      <div
        className="modal fade show d-block"
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTieuDe}
        tabIndex={-1}
        onMouseDown={(e) => {
          if (e.target === e.currentTarget && !khoaDong) dong?.()
        }}
      >
        <div className={'modal-dialog modal-dialog-scrollable modal-dialog-centered' + (kichThuoc ? ' modal-' + kichThuoc : '')}>
          <div className="modal-content">
            <div className="modal-header">
              <h2 className="modal-title h5" id={idTieuDe}>
                {icon && <i className={'bi ' + icon + ' me-2 text-primary'} />}
                {tieuDe}
              </h2>
              <button type="button" className="btn-close" aria-label="Đóng" onClick={() => dong?.()} disabled={khoaDong} />
            </div>
            {khiGui ? (
              <form className="d-flex flex-column flex-grow-1" style={{ minHeight: 0 }} onSubmit={khiGui}>
                <div className="modal-body">{children}</div>
                {chan && <div className="modal-footer">{chan}</div>}
              </form>
            ) : (
              <>
                <div className="modal-body">{children}</div>
                {chan && <div className="modal-footer">{chan}</div>}
              </>
            )}
          </div>
        </div>
      </div>
    </>
  )
}
