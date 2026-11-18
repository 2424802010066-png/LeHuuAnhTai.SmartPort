import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung, Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangTien, ngayGioInput } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'

const SO_LUONG_TOI_DA = 10000

/** /booking/create — dat dich vu cang truc tuyen (thay Views/Booking/Create). */
export default function DatDichVu() {
  const { duLieu: dichVus, loi, dangTai, taiLai } = useApi('/api/dat-dich-vu-khach/dich-vu')
  const [soLuong, setSoLuong] = useState({}) // { [maDV]: so }
  const [ngayCanThucHien, setNgay] = useState('')
  const [ghiChu, setGhiChu] = useState('')
  const { chay, dangChay } = useThaoTac()
  const dieuHuong = useNavigate()

  const bayGio = useMemo(() => ngayGioInput(new Date()), [])

  // Xep theo nhom dich vu de khach nhin ra "nhom nao dang chon"
  const theoNhom = useMemo(() => {
    const nhom = new Map()
    for (const d of dichVus || []) {
      const ten = d.nhomDV || 'Dịch vụ khác'
      if (!nhom.has(ten)) nhom.set(ten, [])
      nhom.get(ten).push(d)
    }
    return [...nhom.entries()]
  }, [dichVus])

  const dongDaChon = useMemo(
    () =>
      (dichVus || [])
        .map((d) => ({ dv: d, sl: Number(soLuong[d.maDV] || 0) }))
        .filter((x) => x.sl > 0)
        .map((x) => ({ ...x, thanhTien: x.sl * x.dv.donGia })),
    [dichVus, soLuong],
  )
  const tongTien = dongDaChon.reduce((s, x) => s + x.thanhTien, 0)

  function doiSoLuong(maDV, giaTri) {
    let n = parseInt(giaTri, 10)
    if (Number.isNaN(n) || n < 0) n = 0
    if (n > SO_LUONG_TOI_DA) n = SO_LUONG_TOI_DA
    setSoLuong((cu) => ({ ...cu, [maDV]: n }))
  }

  async function gui(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/dat-dich-vu-khach', {
        method: 'POST',
        body: {
          ngayCanThucHien,
          ghiChu: ghiChu || null,
          dong: dongDaChon.map((x) => ({ maDV: x.dv.maDV, soLuong: x.sl })),
        },
      }),
    )
    if (kq?.id) dieuHuong('/booking/details/' + kq.id)
  }

  return (
    <>
      <DauTrangKhach
        icon="bi-plus-circle"
        tieuDe="Đặt dịch vụ cảng trực tuyến"
        duongDan={[{ ten: 'Booking của tôi', den: '/booking/my' }, { ten: 'Đặt dịch vụ' }]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        {dangTai && !dichVus ? (
          <DangTai chu="Đang tải bảng dịch vụ…" />
        ) : (
          <form onSubmit={gui} className="row g-4">
            <div className="col-lg-8">
              {theoNhom.length === 0 && (
                <div className="alert alert-warning">
                  <i className="bi bi-exclamation-triangle me-2" />
                  Hiện chưa có dịch vụ nào đang mở bán. Vui lòng liên hệ hotline 0387 562 579.
                </div>
              )}
              {theoNhom.map(([tenNhom, ds]) => (
                <TheNoiDung key={tenNhom} tieuDe={tenNhom} icon="bi-list-check" khongDem>
                  <div className="table-responsive">
                    <table className="table sp-bang table-sm align-middle mb-0">
                      <thead>
                        <tr>
                          <th scope="col">Dịch vụ</th>
                          <th scope="col" className="d-none d-md-table-cell">
                            Đơn vị
                          </th>
                          <th scope="col" className="text-end">
                            Đơn giá
                          </th>
                          <th scope="col" style={{ width: 120 }}>
                            Số lượng
                          </th>
                          <th scope="col" className="text-end" style={{ width: 130 }}>
                            Thành tiền
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {ds.map((d) => {
                          const sl = Number(soLuong[d.maDV] || 0)
                          return (
                            <tr key={d.maDV}>
                              <td>
                                <div className="fw-semibold">{d.tenDV}</div>
                                {d.moTa && <div className="small text-body-secondary">{d.moTa}</div>}
                              </td>
                              <td className="d-none d-md-table-cell small">{d.donViTinh || '—'}</td>
                              <td className="text-end">{dinhDangTien(d.donGia)}</td>
                              <td>
                                <input
                                  type="number"
                                  className="form-control form-control-sm text-center"
                                  min={0}
                                  max={SO_LUONG_TOI_DA}
                                  step={1}
                                  value={soLuong[d.maDV] ?? 0}
                                  onChange={(e) => doiSoLuong(d.maDV, e.target.value)}
                                  aria-label={'Số lượng ' + d.tenDV}
                                />
                              </td>
                              <td className="text-end fw-semibold">{sl > 0 ? dinhDangTien(sl * d.donGia) : '—'}</td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                </TheNoiDung>
              ))}
            </div>

            <div className="col-lg-4">
              <TheNoiDung tieuDe="Thông tin booking" icon="bi-receipt">
                <Truong nhan="Ngày cần thực hiện" batBuoc>
                  <input
                    type="datetime-local"
                    className="form-control"
                    value={ngayCanThucHien}
                    min={bayGio}
                    onChange={(e) => setNgay(e.target.value)}
                  />
                </Truong>
                <Truong nhan="Ghi chú" goiY="VD: số container liên quan, yêu cầu đặc biệt…">
                  <textarea className="form-control" rows={3} maxLength={300} value={ghiChu} onChange={(e) => setGhiChu(e.target.value)} />
                </Truong>

                <div className="border-top pt-3">
                  {dongDaChon.map((x) => (
                    <div key={x.dv.maDV} className="dvk-hang small">
                      <span className="text-truncate">
                        {x.dv.tenDV} × {x.sl}
                      </span>
                      <span>{dinhDangTien(x.thanhTien)}</span>
                    </div>
                  ))}
                  <div className="d-flex justify-content-between align-items-center mt-3">
                    <span className="text-body-secondary">Tổng tạm tính</span>
                    <span className="fs-4 fw-bold text-warning-emphasis">{dinhDangTien(tongTien)}</span>
                  </div>
                </div>

                <button type="submit" className="btn btn-primary btn-lg w-100 mt-3" disabled={dangChay || dongDaChon.length === 0 || !ngayCanThucHien}>
                  {dangChay ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-send me-2" />}
                  Gửi yêu cầu
                </button>
                {dongDaChon.length === 0 && <div className="form-text">Nhập số lượng &gt; 0 cho ít nhất một dịch vụ.</div>}
                <div className="small text-body-secondary mt-2">
                  <i className="bi bi-info-circle me-1" />
                  Sau khi gửi, bộ phận khai thác sẽ duyệt booking và liên hệ xác nhận.
                </div>
                <Link className="btn btn-outline-secondary w-100 mt-2" to="/booking/my">
                  <i className="bi bi-arrow-left me-1" />
                  Quay lại danh sách
                </Link>
              </TheNoiDung>
            </div>
          </form>
        )}
      </div>
    </>
  )
}
