import { useEffect, useMemo, useRef, useState } from 'react'
import { duongDanAnh, goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { ANH_BIA_TIN, API_ANH, API_TIN, locHtmlAnToan, useFormTheoDiaChi } from './chung'
import './kinh-doanh.css'

const RONG = { tieuDe: '', tomTat: '', noiDung: '', anhMau: 'g1', noiBat: false }

const chiTruongForm = (t) => ({
  tieuDe: t?.tieuDe ?? '',
  tomTat: t?.tomTat ?? '',
  noiDung: t?.noiDung ?? '',
  anhMau: t?.anhMau || 'g1',
  noiBat: t?.noiBat ?? false,
})

/** Soan tin tuc cho website: viet bai, doi anh bia, danh dau noi bat, xem truoc (thay Admin/News). */
export default function TinTuc() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi(API_TIN + taoQuery(loc))
  const { duLieu: dsAnh } = useApi(API_ANH)
  const { dangSua, moForm, dongForm } = useFormTheoDiaChi('/admin/news')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [xemTruoc, setXemTruoc] = useState(null)
  const daNap = useRef(null)

  // Anh bia g1..g6 lay tu thu vien anh website (cung khu) de xem truoc ngay tren form
  const anhTheoKhoa = useMemo(() => Object.fromEntries((dsAnh || []).map((a) => [a.khoa, a])), [dsAnh])
  const cacAnhBia = useMemo(() => ANH_BIA_TIN.map((k) => ({ khoa: k, moTa: anhTheoKhoa[k]?.moTa || k, duongDan: anhTheoKhoa[k]?.duongDan })), [anhTheoKhoa])

  useEffect(() => {
    if (dangSua === null) {
      daNap.current = null
      return
    }
    if (daNap.current === dangSua) return
    daNap.current = dangSua
    if (!dangSua) {
      datLai(RONG)
      return
    }
    // Danh sach khong tra noi dung -> luon lay bai viet day du
    goiApi(API_TIN + '/' + dangSua)
      .then((t) => datLai(chiTruongForm(t)))
      .catch(() => datLai(RONG))
  }, [dangSua, datLai])

  async function luu(e) {
    e.preventDefault()
    const than = { ...gt, tieuDe: gt.tieuDe.trim() }
    const kq = await chay(() => (dangSua ? goiApi(API_TIN + '/' + dangSua, { method: 'PUT', body: than }) : goiApi(API_TIN, { method: 'POST', body: than })))
    if (kq) {
      dongForm()
      taiLai({ im: true })
    }
  }

  async function xoa(t) {
    const dongY = await xacNhan({ tieuDe: 'Xóa bài viết?', noiDung: `Xóa "${t.tieuDe}". Thao tác này không hoàn tác được.`, nutDongY: 'Xóa', mau: 'danger' })
    if (!dongY) return
    if (await chay(() => goiApi(API_TIN + '/' + t.maTin, { method: 'DELETE' }))) {
      if (duLieu?.items?.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      taiLai({ im: true })
    }
  }

  async function moXemTruoc(t) {
    const bai = await goiApi(API_TIN + '/' + t.maTin).catch(() => null)
    if (bai) setXemTruoc(bai)
  }

  const COT = [
    {
      khoa: 'tieuDe',
      ten: 'Tiêu đề',
      hien: (t) => (
        <div className="d-flex align-items-center gap-2">
          {t.anhBia ? <img className="kd-anh-nho" src={duongDanAnh(t.anhBia)} alt="" loading="lazy" /> : <span className="kd-anh-nho kd-anh-trong" />}
          <div className="min-w-0">
            <div className="fw-semibold">{t.tieuDe}</div>
            <div className="text-body-secondary small kd-mot-dong">{t.tomTat || '—'}</div>
          </div>
        </div>
      ),
    },
    { khoa: 'ngay', ten: 'Ngày đăng', an: 'md', lop: 'small text-nowrap', hien: (t) => dinhDangNgayGio(t.ngayDang) },
    { khoa: 'luotXem', ten: 'Lượt xem', canPhai: true, hien: (t) => dinhDangSo(t.luotXem) },
    { khoa: 'noiBat', ten: 'Nổi bật', canPhai: true, hien: (t) => (t.noiBat ? <NhanTrangThai giaTri="Nổi bật" mau="warn" /> : <span className="text-muted">—</span>) },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (t) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-secondary me-1" title="Xem trước bài viết" aria-label="Xem trước" onClick={() => moXemTruoc(t)}>
            <i className="bi bi-eye" />
          </button>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" aria-label="Sửa" onClick={() => moForm(t.maTin)}>
            <i className="bi bi-pencil" />
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa" aria-label="Xóa" disabled={dangChay} onClick={() => xoa(t)}>
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  const anhDangChon = cacAnhBia.find((a) => a.khoa === gt.anhMau)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe={`Bài viết tin tức${duLieu ? ` · ${dinhDangSo(duLieu.tong)}` : ''}`}
        icon="bi-newspaper"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Tiêu đề, tóm tắt…" />
            {loc.q && (
              <button type="button" className="btn btn-sm btn-light" onClick={xoaLoc}>
                Xóa lọc
              </button>
            )}
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm()}>
              <i className="bi bi-plus-lg me-1" />
              Viết bài mới
            </button>
          </>
        }
        chan={duLieu && <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="bài viết" />}
      >
        <BangDuLieu cot={COT} dong={duLieu?.items} khoaDong="maTin" dangTai={dangTai} rong={loc.q ? 'Không tìm thấy bài viết.' : 'Chưa có bài viết nào.'} />
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={dongForm}
        khoaDong={dangChay}
        kichThuoc="xl"
        tieuDe={dangSua ? 'Sửa bài viết' : 'Viết bài mới'}
        icon="bi-newspaper"
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={dongForm} disabled={dangChay}>
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu bài viết
            </button>
          </>
        }
      >
        <div className="row g-3">
          <Truong nhan="Tiêu đề" batBuoc lop="col-md-9">
            <input className="form-control" value={gt.tieuDe} onChange={doi('tieuDe')} maxLength={200} autoFocus />
          </Truong>
          <Truong nhan="Ảnh bìa" goiY="Ảnh lấy từ Thư viện ảnh website" lop="col-md-3">
            <select className="form-select" value={gt.anhMau} onChange={doi('anhMau')}>
              {cacAnhBia.map((a) => (
                <option key={a.khoa} value={a.khoa}>
                  {a.khoa} — {a.moTa}
                </option>
              ))}
            </select>
          </Truong>
          {anhDangChon?.duongDan && (
            <div className="col-12">
              <img className="kd-anh-bia-xem" src={duongDanAnh(anhDangChon.duongDan)} alt={'Ảnh bìa ' + anhDangChon.moTa} />
            </div>
          )}
          <Truong nhan="Tóm tắt" lop="col-12">
            <textarea className="form-control" rows={2} value={gt.tomTat} onChange={doi('tomTat')} maxLength={400} />
          </Truong>
          <Truong nhan="Nội dung" goiY="Hỗ trợ HTML đơn giản: <p>, <strong>, <ul>, <li>…" lop="col-12">
            <textarea className="form-control kd-o-noi-dung" rows={12} value={gt.noiDung} onChange={doi('noiDung')} />
          </Truong>
          <div className="col-12">
            <div className="form-check form-switch">
              <input className="form-check-input" type="checkbox" id="kd-tin-noi-bat" checked={!!gt.noiBat} onChange={doi('noiBat')} />
              <label className="form-check-label" htmlFor="kd-tin-noi-bat">
                Tin nổi bật (hiển thị trang chủ)
              </label>
            </div>
          </div>
        </div>
      </HopThoai>

      <HopThoai mo={!!xemTruoc} dong={() => setXemTruoc(null)} kichThuoc="lg" tieuDe="Xem trước bài viết" icon="bi-eye">
        {xemTruoc && (
          <article className="kd-xem-truoc">
            {xemTruoc.anhBia && <img src={duongDanAnh(xemTruoc.anhBia)} alt="" />}
            <h3>{xemTruoc.tieuDe}</h3>
            <div className="text-body-secondary small mb-2">
              {dinhDangNgayGio(xemTruoc.ngayDang)} · {dinhDangSo(xemTruoc.luotXem)} lượt xem
            </div>
            {xemTruoc.tomTat && <p className="fw-semibold">{xemTruoc.tomTat}</p>}
            {/* Noi dung do bien tap vien go: loc bo script/style/thuoc tinh truoc khi hien */}
            <div dangerouslySetInnerHTML={{ __html: locHtmlAnToan(xemTruoc.noiDung) }} />
          </article>
        )}
      </HopThoai>
    </>
  )
}
