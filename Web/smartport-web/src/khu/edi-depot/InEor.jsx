import { useEffect } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai } from '../../components/ui'
import { dinhDangNgay, dinhDangNgayGio, dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import './edi-depot.css'

/** Mot dong cua bang phieu in: nhan tieng Viet + tieng Anh */
function Dong({ vi, en, children }) {
  return (
    <tr>
      <th>
        {vi}
        <span className="ed-in-en">{en}</span>
      </th>
      <td>{children}</td>
    </tr>
  )
}

/**
 * Ban in phieu bao gia sua chua vo EOR song ngu Viet - Anh gui hang tau duyet
 * (thay Admin/Depot/InEor cua ban MVC). Trang dung bo cuc 'in' — nut In nam o khung.
 */
export default function InEor() {
  const { id } = useParams()
  const { duLieu: e, loi, dangTai, taiLai } = useApi('/api/depot/eor/' + id)

  useEffect(() => {
    if (e) document.title = e.soEOR + ' — Phiếu sửa chữa vỏ container'
  }, [e])

  if (dangTai && !e) return <DangTai chu="Đang tải phiếu EOR…" />
  if (!e)
    return (
      <div data-bs-theme="light">
        <BaoLoi loi={loi} thuLai={taiLai} />
        <Link className="btn btn-sm btn-outline-primary" to="/admin/depot/eor">
          <i className="bi bi-tools me-1" />
          Danh sách phiếu EOR
        </Link>
      </div>
    )

  return (
    <div data-bs-theme="light" className="ed-in">
      <div className="ed-in-dau">
        <div>
          <h1>PHIẾU BÁO GIÁ SỬA CHỮA VỎ CONTAINER</h1>
          <div className="ed-in-en">ESTIMATE OF REPAIR (EOR)</div>
        </div>
        <div className="ed-in-so">
          Số / <i>No.</i> <b>{e.soEOR}</b>
          <br />
          Ngày / <i>Date</i>: {dinhDangNgay(e.ngayLap)}
        </div>
      </div>

      <table className="ed-in-bang">
        <tbody>
          <Dong vi="Số container" en="Container No.">
            <span className="ed-in-cont">{e.soCont}</span>
          </Dong>
          <Dong vi="Hãng tàu" en="Shipping line">
            {e.tenHangTau || '—'}
          </Dong>
          <Dong vi="Vị trí hư hỏng" en="Damage location">
            {e.viTriHuHong || '—'}
          </Dong>
          <Dong vi="Mã hư hỏng" en="Damage code">
            {e.maHuHong || '—'}
          </Dong>
          <Dong vi="Mô tả" en="Description">
            {e.moTa || '—'}
          </Dong>
          <Dong vi="Giờ công" en="Labour hours">
            {dinhDangSo(e.gioCong ?? 0, 2)} giờ × {dinhDangTien(e.donGiaGioCong)} = <b>{dinhDangTien(e.tienCong)}</b>
          </Dong>
          <Dong vi="Vật tư" en="Material cost">
            {dinhDangTien(e.chiPhiVatTu ?? 0)}
          </Dong>
          <Dong vi="TỔNG ƯỚC TÍNH" en="TOTAL ESTIMATE">
            <span className="ed-in-tien">{dinhDangTien(e.chiPhiUocTinh ?? 0)}</span>
          </Dong>
          <Dong vi="Trạng thái" en="Status">
            <b>{e.trangThai}</b>
            {e.ngayDuyet && <> — {dinhDangNgayGio(e.ngayDuyet)}</>}
            {e.ghiChu && <div className="small">{e.ghiChu}</div>}
          </Dong>
        </tbody>
      </table>

      <div className="ed-in-ghi-chu">
        Phiếu này chỉ có giá trị thực hiện sau khi <b>hãng tàu phê duyệt</b>.{' '}
        <i>This estimate is valid for execution only after the shipping line&apos;s approval.</i>
      </div>

      <div className="ed-in-ky">
        <div>
          <b>NGƯỜI LẬP PHIẾU</b>
          <br />
          <i>Prepared by</i>
          <div className="ed-in-cho-ky" />
          {e.nguoiLap || ''}
        </div>
        <div>
          <b>GIÁM ĐỊNH VIÊN</b>
          <br />
          <i>Surveyor</i>
          <div className="ed-in-cho-ky" />
        </div>
        <div>
          <b>HÃNG TÀU DUYỆT</b>
          <br />
          <i>Line approval</i>
          <div className="ed-in-cho-ky" />
        </div>
      </div>
    </div>
  )
}
