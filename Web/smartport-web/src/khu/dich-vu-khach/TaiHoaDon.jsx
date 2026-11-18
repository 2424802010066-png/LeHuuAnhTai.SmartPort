import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { taiFile } from '../../api/client'
import DauTrangKhach from './DauTrangKhach'

/**
 * /booking/invoice/:id — dia chi tai hoa don Excel cua ban MVC (Booking/Invoice).
 * Ban MVC tra thang file; o day dia chi van song: mo trang la tu tai file xuong,
 * roi moi link cu / ma QR da in van dung. Tai khong duoc thi co nut bam lai.
 */
export default function TaiHoaDon() {
  const { id } = useParams()
  const [trangThai, setTrangThai] = useState('dang') // dang | xong | loi
  const [loi, setLoi] = useState(null)
  const [tenFile, setTenFile] = useState(null)
  const daChay = useRef(false)

  const tai = useCallback(async () => {
    setTrangThai('dang')
    setLoi(null)
    try {
      const ten = await taiFile(`/api/dat-dich-vu-khach/${id}/hoa-don-excel`, { tenMacDinh: `HoaDon_${id}.xlsx` })
      setTenFile(ten)
      setTrangThai('xong')
    } catch (e) {
      setLoi(e)
      setTrangThai('loi')
    }
  }, [id])

  useEffect(() => {
    // StrictMode goi effect hai lan o che do phat trien -> chan tai file hai lan
    if (daChay.current) return
    daChay.current = true
    tai()
  }, [tai])

  return (
    <>
      <DauTrangKhach
        icon="bi-file-earmark-excel"
        tieuDe="Tải hóa đơn"
        duongDan={[{ ten: 'Booking của tôi', den: '/booking/my' }, { ten: 'Hóa đơn' }]}
      />
      <div className="container py-5 text-center">
        {trangThai === 'dang' && (
          <>
            <div className="spinner-border text-primary mb-3" role="status" aria-hidden="true" />
            <div>Đang tạo hóa đơn Excel…</div>
          </>
        )}
        {trangThai === 'xong' && (
          <>
            <div className="display-6 text-success mb-2">
              <i className="bi bi-check-circle" />
            </div>
            <div className="mb-3">
              Đã tải <strong>{tenFile}</strong> xuống máy.
            </div>
          </>
        )}
        {trangThai === 'loi' && (
          <div className="alert alert-danger d-inline-block text-start">
            <i className="bi bi-exclamation-octagon me-2" />
            {loi?.message || 'Không tải được hóa đơn.'}
          </div>
        )}
        <div className="mt-3 d-flex flex-wrap justify-content-center gap-2">
          <button type="button" className="btn btn-outline-primary" onClick={tai} disabled={trangThai === 'dang'}>
            <i className="bi bi-download me-1" />
            Tải lại
          </button>
          <Link className="btn btn-primary" to={'/booking/details/' + id}>
            <i className="bi bi-arrow-left me-1" />
            Về đơn hàng
          </Link>
        </div>
      </div>
    </>
  )
}
