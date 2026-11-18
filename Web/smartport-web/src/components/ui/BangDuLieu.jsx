/**
 * Bang du lieu dung chung cho cac man hinh danh sach.
 *
 *   const COT = [
 *     { khoa: 'soCont', ten: 'Số container', hien: (d) => <b>{d.soCont}</b> },
 *     { khoa: 'tongTien', ten: 'Tổng tiền', canPhai: true, hien: (d) => dinhDangTien(d.tongTien) },
 *     { khoa: 'thaoTac', ten: '', canPhai: true, hien: (d) => <button ...>Sửa</button> },
 *   ]
 *   <BangDuLieu cot={COT} dong={d?.items} khoaDong="maTau" dangTai={dangTai} rong="Chưa có tàu nào." />
 *
 * cot[i]: khoa (bat buoc, duy nhat) · ten · hien(dong, chiSo) · canPhai · lop (class cho td) · rong (width) · an ('md' = an duoi md)
 */
export default function BangDuLieu({ cot, dong, khoaDong = 'id', dangTai, rong = 'Không có dữ liệu.', bamDong, lopDong, nho = true, chan, cuon }) {
  const ds = dong || []
  const lopAn = (c) => (c.an ? ` d-none d-${c.an}-table-cell` : '')

  return (
    <div className={'table-responsive' + (cuon ? ' sp-bang-cuon' : '')} style={cuon ? { maxHeight: cuon } : undefined}>
      <table className={'table sp-bang align-middle mb-0' + (nho ? ' table-sm' : '') + (bamDong ? ' table-hover' : '')}>
        <thead>
          <tr>
            {cot.map((c) => (
              <th key={c.khoa} className={(c.canPhai ? 'text-end' : '') + lopAn(c)} style={c.rong ? { width: c.rong } : undefined} scope="col">
                {c.ten}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {dangTai && !ds.length ? (
            <tr>
              <td colSpan={cot.length} className="text-center text-muted py-4">
                <span className="spinner-border spinner-border-sm me-2" />
                Đang tải…
              </td>
            </tr>
          ) : !ds.length ? (
            <tr>
              <td colSpan={cot.length} className="text-center text-muted py-4">
                <i className="bi bi-inbox me-2" />
                {rong}
              </td>
            </tr>
          ) : (
            ds.map((d, i) => (
              <tr
                key={typeof khoaDong === 'function' ? khoaDong(d, i) : (d[khoaDong] ?? i)}
                className={(bamDong ? 'sp-dong-bam ' : '') + (lopDong ? lopDong(d) || '' : '')}
                onClick={bamDong ? () => bamDong(d) : undefined}
              >
                {cot.map((c) => (
                  <td key={c.khoa} className={(c.canPhai ? 'text-end ' : '') + (c.lop || '') + lopAn(c)}>
                    {c.hien ? c.hien(d, i) : (d[c.khoa] ?? '—')}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
        {chan && <tfoot>{chan}</tfoot>}
      </table>
    </div>
  )
}
