/**
 * Thanh phan trang cho du lieu TrangDto cua API ({ items, tong, trang, coTrang, soTrang }).
 *   <PhanTrang {...d} doiTrang={(t) => datLoc({ trang: t })} />
 */
export default function PhanTrang({ trang = 1, soTrang = 1, tong, doiTrang, donVi = 'dòng' }) {
  const tongChu = tong != null ? Number(tong).toLocaleString('vi-VN') : null
  if (!soTrang || soTrang <= 1) {
    return tongChu != null ? (
      <div className="small text-muted mt-2">
        Tổng {tongChu} {donVi}
      </div>
    ) : null
  }

  // Hien toi da: 1 … (trang-2 .. trang+2) … cuoi
  const cacTrang = []
  const tu = Math.max(1, trang - 2)
  const den = Math.min(soTrang, trang + 2)
  if (tu > 1) cacTrang.push(1)
  if (tu > 2) cacTrang.push('trai')
  for (let i = tu; i <= den; i++) cacTrang.push(i)
  if (den < soTrang - 1) cacTrang.push('phai')
  if (den < soTrang) cacTrang.push(soTrang)

  const nut = (t, nhan, tat, dangChon, khoa) => (
    <li key={khoa} className={'page-item' + (tat ? ' disabled' : '') + (dangChon ? ' active' : '')}>
      <button type="button" className="page-link" onClick={() => doiTrang?.(t)} disabled={tat} aria-current={dangChon ? 'page' : undefined}>
        {nhan}
      </button>
    </li>
  )

  return (
    <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mt-2">
      <div className="small text-muted">
        Trang {trang}/{soTrang}
        {tongChu != null && (
          <>
            {' '}
            · tổng {tongChu} {donVi}
          </>
        )}
      </div>
      <nav aria-label="Phân trang">
        <ul className="pagination pagination-sm mb-0">
          {nut(trang - 1, '‹', trang <= 1, false, 'truoc')}
          {cacTrang.map((t) =>
            typeof t === 'string' ? (
              <li key={t} className="page-item disabled">
                <span className="page-link">…</span>
              </li>
            ) : (
              nut(t, t, false, t === trang, 'so' + t)
            ),
          )}
          {nut(trang + 1, '›', trang >= soTrang, false, 'sau')}
        </ul>
      </nav>
    </div>
  )
}
