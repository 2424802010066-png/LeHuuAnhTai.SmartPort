import { danhGiaMatKhau, DO_DAI_TOI_THIEU } from './matKhau'

/**
 * Thanh do do manh + dieu kien cua mat khau moi (dang ky, doi mat khau, quen mat khau).
 *   <DoManhMatKhau matKhau={mk} ten={tenDangNhap} />   day du: thanh do + danh sach dieu kien
 *   <DoManhMatKhau matKhau={mk} ten={ten} gon />       gon: thanh do + dieu kien dau tien con thieu (o nhap hep)
 */
export default function DoManhMatKhau({ matKhau, ten, gon = false }) {
  const dg = danhGiaMatKhau(matKhau, ten)

  if (!matKhau && gon) return <span className="small text-body-secondary">Ít nhất {DO_DAI_TOI_THIEU} ký tự, có chữ và số</span>

  const thieu = dg.dieuKien.find((d) => !d.dat)
  return (
    <div className={'kh-do-manh' + (gon ? ' gon' : '')} aria-live="polite">
      {matKhau && (
        <>
          <div className="kh-do-manh-thanh" aria-hidden="true">
            {[0, 1, 2, 3].map((i) => (
              <span key={i} className={i < Math.max(dg.diem, 1) ? 'bat ' + dg.mau : ''} />
            ))}
          </div>
          <div className="kh-do-manh-nhan">
            Độ mạnh: <b className={'kh-chu-' + dg.mau}>{dg.nhan}</b>
            {gon && thieu && <span className="kh-chu-do"> · còn thiếu: {thieu.ten.toLowerCase()}</span>}
          </div>
        </>
      )}
      {!gon && (
        <ul className="kh-dieu-kien">
          {dg.dieuKien.map((d) => (
            <li key={d.ma} className={d.dat ? 'dat' : ''}>
              <i className={'bi ' + (d.dat ? 'bi-check-circle-fill' : 'bi-circle')} aria-hidden="true" />
              {d.ten}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
