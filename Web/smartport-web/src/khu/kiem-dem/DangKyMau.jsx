import { SO_MAU_TOI_DA, SO_MAU_TOI_THIEU } from './chung/mauSanPham'

/**
 * Thanh dang ky SAN PHAM MAU (few-shot) cua man hinh Kiem dem:
 * 4-6 anh mau -> AI chi dem dung san pham do khi chay video bang chuyen.
 * Thanh phan chi HIEN THI — moi xu ly do trang KiemDemAi giu (mau nam trong ref
 * de vong lap dem doc duoc ngay, khong doi React ve lai).
 */
export default function DangKyMau({ anhMau, khoa, themAnh, themVideo, chupCamera, napDemo, xoaMau }) {
  const du = anhMau.length >= SO_MAU_TOI_THIEU
  return (
    <>
      <div className="d-flex flex-wrap align-items-center gap-2 mb-2">
        <span className={'badge ' + (du ? 'text-bg-success' : 'text-bg-secondary')}>
          {anhMau.length}/{SO_MAU_TOI_DA} mẫu
        </span>
        {anhMau.map((src, i) => (
          <img key={i} src={src} alt={'Ảnh mẫu ' + (i + 1)} className="kd-anh-mau" />
        ))}
        {!anhMau.length && <span className="text-body-secondary small">Chưa đăng ký sản phẩm mẫu.</span>}
      </div>

      <div className="row g-2">
        <div className="col-md-3 col-6">
          <label className={'btn btn-outline-primary btn-sm w-100 mb-0' + (khoa ? ' disabled' : '')}>
            <i className="bi bi-images me-1" />
            Ảnh mẫu
            <input type="file" accept="image/*" multiple hidden disabled={khoa} onChange={themAnh} />
          </label>
        </div>
        <div className="col-md-3 col-6">
          <label className={'btn btn-outline-primary btn-sm w-100 mb-0' + (khoa ? ' disabled' : '')}>
            <i className="bi bi-film me-1" />
            Video mẫu
            <input type="file" accept="video/*" hidden disabled={khoa} onChange={themVideo} />
          </label>
        </div>
        <div className="col-md-2 col-4">
          <button type="button" className="btn btn-outline-primary btn-sm w-100" disabled={khoa} onClick={chupCamera}>
            <i className="bi bi-camera me-1" />
            Chụp
          </button>
        </div>
        <div className="col-md-2 col-4">
          <button type="button" className="btn btn-outline-success btn-sm w-100" disabled={khoa} onClick={napDemo}>
            <i className="bi bi-stars me-1" />
            Mẫu demo
          </button>
        </div>
        <div className="col-md-2 col-4">
          <button type="button" className="btn btn-outline-danger btn-sm w-100" title="Xóa toàn bộ ảnh mẫu" disabled={khoa} onClick={xoaMau}>
            <i className="bi bi-trash" />
          </button>
        </div>
      </div>
    </>
  )
}
