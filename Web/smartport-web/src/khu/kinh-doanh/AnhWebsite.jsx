import { useRef, useState } from 'react'
import { duongDanAnh, goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo } from '../../utils/dinhDang'
import { API_ANH } from './chung'
import './kinh-doanh.css'

/** Thu vien anh website: doi URL hoac tai anh tu may len cho tung vi tri (thay Admin/Gallery). */
export default function AnhWebsite() {
  const { duLieu, loi, dangTai, taiLai } = useApi(API_ANH)
  const { chay, dangChay } = useThaoTac()

  const ds = duLieu || []
  const soAnhTaiLen = ds.filter((a) => a.laAnhTaiLen).length

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-4">
          <TheChiSo icon="bi-images" mau="xanh" so={dangTai && !ds.length ? '…' : dinhDangSo(ds.length)} nhan="Vị trí ảnh trên website" />
        </div>
        <div className="col-6 col-xl-4">
          <TheChiSo icon="bi-cloud-arrow-up" mau="la" so={dinhDangSo(soAnhTaiLen)} nhan="Ảnh tự tải lên (còn lại dùng ảnh ngoài)" />
        </div>
        <div className="col-12 col-xl-4">
          <TheChiSo icon="bi-newspaper" mau="cam" so={dinhDangSo(ds.reduce((s, a) => s + (a.soTinDung || 0), 0))} nhan="Lượt bài viết đang dùng làm ảnh bìa" />
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung tieuDe="Ảnh hiển thị trên website" icon="bi-images">
        {dangTai && !ds.length ? (
          <DangTai chu="Đang tải thư viện ảnh…" />
        ) : (
          <div className="row g-3">
            {ds.map((a) => (
              <div className="col-md-6 col-xl-4" key={a.khoa}>
                <TheAnh anh={a} chay={chay} dangChay={dangChay} taiLai={taiLai} />
              </div>
            ))}
          </div>
        )}
        <div className="text-body-secondary small mt-3">
          <i className="bi bi-info-circle me-1" />
          Ảnh tải lên lưu tại <code>/Content/img/uploads</code> (tối đa 5MB — jpg, png, webp, gif). URL ngoài phải bắt đầu bằng <code>https://</code>.
        </div>
      </TheNoiDung>
    </>
  )
}

/** Mot vi tri anh: xem truoc, sua URL, tai anh tu may len */
function TheAnh({ anh, chay, dangChay, taiLai }) {
  const [url, setUrl] = useState(anh.url || '')
  const [tep, setTep] = useState(null)
  const oTep = useRef(null)

  async function luuUrl(e) {
    e.preventDefault()
    if (await chay(() => goiApi(`${API_ANH}/${anh.khoa}`, { method: 'PUT', body: { url: url.trim() } }))) taiLai({ im: true })
  }

  async function taiLen(e) {
    e.preventDefault()
    if (!tep) return
    const form = new FormData()
    form.append('file', tep)
    const kq = await chay(() => goiApi(`${API_ANH}/${anh.khoa}/tai-len`, { method: 'POST', body: form }))
    if (kq) {
      setUrl(kq.duLieu?.url || '')
      setTep(null)
      if (oTep.current) oTep.current.value = ''
      taiLai({ im: true })
    }
  }

  return (
    <div className="card sp-the h-100">
      <div className="card-body">
        <div className="kd-anh-xem" style={{ backgroundImage: anh.duongDan ? `url("${encodeURI(duongDanAnh(anh.duongDan))}")` : undefined }} role="img" aria-label={'Ảnh ' + anh.moTa} />
        <div className="d-flex justify-content-between align-items-center gap-2 mt-2 mb-2">
          <span className="fw-semibold">{anh.moTa}</span>
          <span className="badge text-bg-light border text-nowrap">{anh.khoa}</span>
        </div>
        {anh.soTinDung > 0 && (
          <div className="text-body-secondary small mb-2">
            <i className="bi bi-newspaper me-1" />
            {anh.soTinDung} bài viết dùng ảnh này làm ảnh bìa
          </div>
        )}
        <form className="d-flex gap-1 mb-2" onSubmit={luuUrl}>
          <input className="form-control form-control-sm kd-o-url" value={url} onChange={(e) => setUrl(e.target.value)} aria-label={'URL ảnh ' + anh.moTa} maxLength={500} />
          <button className="btn btn-sm btn-outline-primary" type="submit" title="Lưu URL" aria-label="Lưu URL" disabled={dangChay}>
            <i className="bi bi-save" />
          </button>
        </form>
        <form className="d-flex gap-1" onSubmit={taiLen}>
          <input
            ref={oTep}
            type="file"
            accept="image/*"
            className="form-control form-control-sm kd-o-url"
            aria-label={'Chọn ảnh mới cho ' + anh.moTa}
            onChange={(e) => setTep(e.target.files?.[0] || null)}
          />
          <button className="btn btn-sm btn-primary" type="submit" title="Tải ảnh từ máy lên" aria-label="Tải ảnh lên" disabled={dangChay || !tep}>
            <i className="bi bi-upload" />
          </button>
        </form>
      </div>
    </div>
  )
}
