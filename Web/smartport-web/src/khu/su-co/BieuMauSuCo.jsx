import { useRef, useState } from 'react'
import { goiApi, taoBoKhoaGuiLap } from '../../api/client'
import { Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { LOAI_SU_CO, MUC_DO, nenAnh } from './suCo'

const RONG = { loai: '', mucDo: 'TrungBinh', tieuDe: '', moTa: '', viTri: '', soContainer: '', maLenh: '' }

/**
 * BIEU MAU BAO SU CO (UC22) dung chung cho khach hang va nhan vien.
 *   cacLoai   ma loai duoc chon (khach: 3 loai; nhan vien: ca 5)
 *   container null -> o nhap tu do (nhan vien); mang [{ soCont, nhan }] -> chi chon container cua minh (khach)
 *   lenh      [{ maLenh, nhan, soCont }] — lenh duoc dinh kem
 *   khiXong   (ketQuaApi) => void
 */
export default function BieuMauSuCo({ cacLoai, container, lenh = [], khiXong, khiHuy, nutGui = 'Gửi phản ánh', goiYViTri = 'VD: Cổng chính, Bãi B…' }) {
  const [gt, setGt] = useState(RONG)
  const [anh, setAnh] = useState(null)
  const [loiAnh, setLoiAnh] = useState(null)
  const [dangNen, setDangNen] = useState(false)
  const [khoaGui] = useState(taoBoKhoaGuiLap)
  const tepRef = useRef(null)
  const { chay, dangChay } = useThaoTac()
  const loai = LOAI_SU_CO.filter((l) => cacLoai.includes(l.ma))

  const doi = (k) => (e) => setGt((cu) => ({ ...cu, [k]: e.target.value }))

  async function chonAnh(e) {
    const tep = e.target.files?.[0]
    setLoiAnh(null)
    if (!tep) return
    setDangNen(true)
    try {
      setAnh(await nenAnh(tep))
    } catch (er) {
      setAnh(null)
      setLoiAnh(er.message)
      if (tepRef.current) tepRef.current.value = ''
    } finally {
      setDangNen(false)
    }
  }

  function chonLenh(e) {
    const ma = e.target.value
    const l = lenh.find((x) => String(x.maLenh) === ma)
    // Chon lenh co container -> tu dien container cua lenh (API cung tu dien neu bo trong)
    setGt((cu) => ({ ...cu, maLenh: ma, soContainer: l?.soCont && container ? l.soCont : cu.soContainer }))
  }

  async function gui(e) {
    e.preventDefault()
    const body = {
      loai: gt.loai,
      mucDo: gt.mucDo,
      tieuDe: gt.tieuDe.trim(),
      moTa: gt.moTa.trim(),
      viTri: gt.viTri.trim() || null,
      soContainer: gt.soContainer.trim() || null,
      maLenh: gt.maLenh ? Number(gt.maLenh) : null,
      anhBase64: anh,
    }
    const kq = await chay(() => goiApi('/api/su-co', { method: 'POST', body, chongGuiLap: khoaGui }))
    if (kq) {
      setGt(RONG)
      setAnh(null)
      if (tepRef.current) tepRef.current.value = ''
      khiXong?.(kq)
    }
  }

  return (
    <form onSubmit={gui}>
      <div className="row g-2">
        <Truong nhan="Loại sự cố" batBuoc lop="col-md-7 mb-2">
          <select className="form-select form-select-sm" value={gt.loai} onChange={doi('loai')}>
            <option value="">— Chọn loại —</option>
            {loai.map((l) => (
              <option key={l.ma} value={l.ma}>
                {l.ten}
              </option>
            ))}
          </select>
        </Truong>
        <div className="col-md-5 mb-2">
          <div className="form-label small fw-semibold mb-1">
            Mức độ<span className="text-danger ms-1">*</span>
          </div>
          <div className="btn-group btn-group-sm w-100" role="group" aria-label="Mức độ">
            {MUC_DO.map((m) => (
              <button
                key={m.ma}
                type="button"
                className={'btn ' + (gt.mucDo === m.ma ? (m.ma === 'NguyHiem' ? 'btn-danger' : 'btn-primary') : 'btn-outline-secondary')}
                aria-pressed={gt.mucDo === m.ma}
                onClick={() => setGt((cu) => ({ ...cu, mucDo: m.ma }))}
              >
                {m.ten}
              </button>
            ))}
          </div>
        </div>
      </div>
      {gt.mucDo === 'NguyHiem' && (
        <div className="alert alert-danger py-2 small mb-2">
          <i className="bi bi-exclamation-octagon-fill me-1" />
          Mức <b>Nguy hiểm</b> sẽ phát <b>báo động toàn cảng</b> ngay khi gửi — chỉ chọn khi có nguy cơ cho người, hàng hóa hoặc thiết bị.
        </div>
      )}

      <Truong nhan="Tiêu đề" batBuoc lop="mb-2">
        <input className="form-control form-control-sm" maxLength={150} placeholder="Tóm tắt ngắn sự việc" value={gt.tieuDe} onChange={doi('tieuDe')} />
      </Truong>
      <Truong nhan="Mô tả chi tiết" batBuoc lop="mb-2" goiY={`${gt.moTa.length}/1000 ký tự`}>
        <textarea
          className="form-control form-control-sm"
          rows={4}
          maxLength={1000}
          placeholder="Diễn biến, thời điểm, mức ảnh hưởng, đề nghị của bạn…"
          value={gt.moTa}
          onChange={doi('moTa')}
        />
      </Truong>

      <div className="row g-2">
        <Truong nhan="Lệnh giao nhận liên quan" lop="col-md-6 mb-2">
          <select className="form-select form-select-sm" value={gt.maLenh} onChange={chonLenh}>
            <option value="">— Không chọn —</option>
            {lenh.map((l) => (
              <option key={l.maLenh} value={l.maLenh}>
                {l.nhan}
              </option>
            ))}
          </select>
        </Truong>
        <Truong nhan="Container liên quan" lop="col-md-6 mb-2">
          {container ? (
            <select className="form-select form-select-sm" value={gt.soContainer} onChange={doi('soContainer')}>
              <option value="">— Không chọn —</option>
              {container.map((c) => (
                <option key={c.soCont} value={c.soCont}>
                  {c.nhan}
                </option>
              ))}
            </select>
          ) : (
            <input
              className="form-control form-control-sm text-uppercase"
              maxLength={15}
              placeholder="VD: MSCU2299876"
              value={gt.soContainer}
              onChange={doi('soContainer')}
            />
          )}
        </Truong>
      </div>

      <div className="row g-2">
        <Truong nhan="Nơi xảy ra" lop="col-md-6 mb-2">
          <input className="form-control form-control-sm" maxLength={100} placeholder={goiYViTri} value={gt.viTri} onChange={doi('viTri')} />
        </Truong>
        <Truong nhan="Ảnh hiện trường (tùy chọn)" lop="col-md-6 mb-2" loi={loiAnh} goiY="JPG/PNG — tự nén còn vài trăm KB">
          <input ref={tepRef} type="file" accept="image/*" className="form-control form-control-sm" onChange={chonAnh} />
        </Truong>
      </div>
      {(anh || dangNen) && (
        <div className="d-flex align-items-center gap-2 mb-2">
          {dangNen ? (
            <span className="small text-body-secondary">
              <span className="spinner-border spinner-border-sm me-1" />
              Đang nén ảnh…
            </span>
          ) : (
            <>
              <img src={anh} alt="Ảnh hiện trường sẽ gửi kèm" className="sc-anh-nho" />
              <button
                type="button"
                className="btn btn-sm btn-outline-secondary"
                onClick={() => {
                  setAnh(null)
                  if (tepRef.current) tepRef.current.value = ''
                }}
              >
                <i className="bi bi-x-lg me-1" />
                Bỏ ảnh
              </button>
            </>
          )}
        </div>
      )}

      <div className="d-flex justify-content-end gap-2 mt-2">
        {khiHuy && (
          <button type="button" className="btn btn-light" onClick={khiHuy} disabled={dangChay}>
            Thôi
          </button>
        )}
        <button type="submit" className={'btn ' + (gt.mucDo === 'NguyHiem' ? 'btn-danger' : 'btn-primary')} disabled={dangChay || dangNen}>
          {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-send me-1" />}
          {nutGui}
        </button>
      </div>
    </form>
  )
}
