import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { goiApi, taiFile } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung, useThongBao, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import NhanLoaiBanTin from './NhanLoaiBanTin'
import { MAU_TRANG_THAI_EDI, kyBaoCao, saoChep, trangThaiBanTin } from './chung'
import './edi-depot.css'

/** Mot dong trong bang thong tin ban tin */
function Dong({ nhan, children }) {
  return (
    <tr>
      <td className="text-body-secondary" style={{ width: 150 }}>
        {nhan}
      </td>
      <td>{children}</td>
    </tr>
  )
}

/**
 * Xem mot ban tin EDI (thay Admin/Edi/Xem cua ban MVC): thong tin, noi dung EDIFACT goc,
 * giai thich tung doan bang tieng Viet, sao chep / tai tep .edi / danh dau da gui / xoa.
 */
export default function XemBanTin() {
  const { id } = useParams()
  const { duLieu: b, loi, dangTai, taiLai } = useApi('/api/edi/' + id)
  const [daChep, setDaChep] = useState(false)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()
  const dieuHuong = useNavigate()

  if (!b) {
    return dangTai ? (
      <DangTai chu="Đang tải bản tin…" />
    ) : (
      <>
        <BaoLoi loi={loi} thuLai={taiLai} />
        <Link className="btn btn-sm btn-outline-primary" to="/admin/edi">
          <i className="bi bi-arrow-left me-1" />
          Danh sách bản tin
        </Link>
      </>
    )
  }

  const tt = trangThaiBanTin(b.trangThai)

  async function chep() {
    if (await saoChep(b.noiDung || '')) {
      setDaChep(true)
      setTimeout(() => setDaChep(false), 2500)
    } else {
      bao.loi('Trình duyệt không cho sao chép — bấm vào khung nội dung rồi Ctrl+C.')
    }
  }

  async function tai() {
    try {
      await taiFile('/api/edi/' + b.maBT + '/tai', { tenMacDinh: `${b.loaiBT}_${b.soChungTu || b.maBT}.edi` })
    } catch (e) {
      bao.loi(e)
    }
  }

  async function danhDauGui() {
    if (await chay(() => goiApi('/api/edi/' + b.maBT + '/da-gui', { method: 'POST' }))) taiLai({ im: true })
  }

  async function xoa() {
    const dongY = await xacNhan({
      tieuDe: 'Xóa bản tin?',
      noiDung: `Xóa bản tin ${b.soChungTu || '#' + b.maBT}.\nCác lượt qua cổng trong đó sẽ quay lại trạng thái CHƯA báo cáo.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/edi/' + b.maBT, { method: 'DELETE' }))) dieuHuong('/admin/edi')
  }

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/edi">
          <i className="bi bi-arrow-left me-1" />
          Danh sách
        </Link>
        <b className="ed-mono">{b.soChungTu || '#' + b.maBT}</b>
        <NhanLoaiBanTin loai={b.loaiBT} />
        <span className="badge text-bg-light border">{b.huong}</span>
        <NhanTrangThai giaTri={tt} mau={MAU_TRANG_THAI_EDI[tt]} />
        <span className="flex-grow-1" />
        <button type="button" className={'btn btn-sm ' + (daChep ? 'btn-success' : 'btn-outline-secondary')} onClick={chep}>
          <i className={'bi me-1 ' + (daChep ? 'bi-check2' : 'bi-clipboard')} />
          {daChep ? 'Đã chép' : 'Sao chép'}
        </button>
        <button type="button" className="btn btn-sm btn-primary" onClick={tai}>
          <i className="bi bi-download me-1" />
          Tải tệp .edi
        </button>
        {b.huong === 'Gửi' && tt !== 'Đã gửi' && (
          <button type="button" className="btn btn-sm btn-success" disabled={dangChay} onClick={danhDauGui}>
            <i className="bi bi-send me-1" />
            Đánh dấu đã gửi
          </button>
        )}
        <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangChay} title="Xóa bản tin" aria-label="Xóa bản tin" onClick={xoa}>
          <i className="bi bi-trash" />
        </button>
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung tieuDe="Thông tin bản tin" icon="bi-info-circle" khongDem>
            <table className="table table-sm sp-bang align-middle mb-0">
              <tbody>
                <Dong nhan="Số chứng từ">
                  <span className="fw-semibold">{b.soChungTu || '—'}</span>
                </Dong>
                <Dong nhan="Số tham chiếu">
                  <code>{b.soThamChieu || '—'}</code>
                </Dong>
                <Dong nhan="Hãng tàu">{b.tenHangTau ? b.tenHangTau + (b.maSCAC ? ` (${b.maSCAC})` : '') : '—'}</Dong>
                <Dong nhan="Số container">
                  <b>{dinhDangSo(b.soLuongCont)}</b>
                </Dong>
                {b.tuNgay && <Dong nhan="Kỳ báo cáo">{kyBaoCao(b.tuNgay, b.denNgay)}</Dong>}
                <Dong nhan="Người tạo">
                  {b.nguoiTao || '—'} · {dinhDangNgayGio(b.ngayTao)}
                </Dong>
                <Dong nhan="Trạng thái">
                  <b>{tt}</b>
                  {b.ngayGui && <span className="text-body-secondary"> · gửi {dinhDangNgayGio(b.ngayGui)}</span>}
                </Dong>
                <Dong nhan="Tên tệp">
                  <code className="small">{b.tenTep || '—'}</code>
                </Dong>
                {b.ghiChu && <Dong nhan="Ghi chú">{<span className="small">{b.ghiChu}</span>}</Dong>}
              </tbody>
            </table>
          </TheNoiDung>

          <TheNoiDung tieuDe="Bản tin nói gì (tiếng Việt)" icon="bi-translate">
            <div className="small text-body-secondary mb-2">Không cần biết EDIFACT vẫn kiểm tra được trước khi gửi.</div>
            <div className="ed-doan-ds">
              {(b.giaiThich || []).map((g, i) => (
                <div className="ed-doan" key={i}>
                  <code className="ed-doan-ma">{g.ma}</code>
                  <span className="ed-doan-mo">{g.moTa}</span>
                </div>
              ))}
            </div>
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung
            tieuDe="Nội dung EDIFACT gốc"
            icon="bi-code-slash"
            phai={<span className="badge text-bg-light border">{dinhDangSo(b.soDoan)} đoạn</span>}
          >
            <pre className="ed-edi-goc">{b.noiDung}</pre>
            <div className="small text-body-secondary mt-2">
              Đây chính là tệp gửi cho hãng tàu. Ký tự <code>&apos;</code> kết thúc đoạn, <code>+</code> ngăn phần tử, <code>:</code> ngăn thành
              phần, <code>?</code> là ký tự thoát.
            </div>
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
