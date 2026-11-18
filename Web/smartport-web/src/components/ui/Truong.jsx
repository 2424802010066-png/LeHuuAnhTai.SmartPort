import { cloneElement, isValidElement, useId } from 'react'

/**
 * O nhap co nhan + goi y + loi, tu noi label voi input (htmlFor/id).
 *   <Truong nhan="Tên tàu" batBuoc goiY="Đúng như giấy chứng nhận đăng ký">
 *     <input className="form-control" value={gt.tenTau} onChange={doi('tenTau')} />
 *   </Truong>
 * lop: class cho khung ngoai (vd "col-md-6").
 */
export default function Truong({ nhan, batBuoc, goiY, loi, lop = 'mb-3', children }) {
  const idTuSinh = useId()
  const con = isValidElement(children) ? children : null
  const id = con?.props.id || idTuSinh
  return (
    <div className={lop}>
      {nhan && (
        <label className="form-label small fw-semibold mb-1" htmlFor={id}>
          {nhan}
          {batBuoc && <span className="text-danger ms-1">*</span>}
        </label>
      )}
      {con
        ? cloneElement(con, {
            id,
            required: con.props.required ?? batBuoc,
            className: (con.props.className || '') + (loi ? ' is-invalid' : ''),
          })
        : children}
      {loi ? <div className="invalid-feedback d-block">{loi}</div> : goiY ? <div className="form-text">{goiY}</div> : null}
    </div>
  )
}
