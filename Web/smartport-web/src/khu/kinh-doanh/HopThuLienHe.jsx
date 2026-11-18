import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { API_LIEN_HE } from './chung'
import './kinh-doanh.css'

/** Hop thu lien he tu website: doc, danh dau da xu ly, xoa (thay Admin/Contacts). */
export default function HopThuLienHe() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ q: '', xuLy: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi(API_LIEN_HE + taoQuery(loc))
  const { duLieu: tk, taiLai: taiLaiThongKe } = useApi(API_LIEN_HE + '/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [dangDoc, setDangDoc] = useState(null)

  const lamMoi = useCallback(() => {
    taiLai({ im: true })
    taiLaiThongKe({ im: true })
  }, [taiLai, taiLaiThongKe])

  async function doiXuLy(l) {
    const kq = await chay(() => goiApi(`${API_LIEN_HE}/${l.maLH}/doi-xu-ly`, { method: 'POST' }))
    if (kq) {
      setDangDoc((cu) => (cu && cu.maLH === l.maLH ? kq.duLieu : cu))
      lamMoi()
    }
  }

  async function xoa(l) {
    const dongY = await xacNhan({ tieuDe: 'Xóa liên hệ?', noiDung: `Xóa liên hệ của ${l.hoTen}. Thao tác này không hoàn tác được.`, nutDongY: 'Xóa', mau: 'danger' })
    if (!dongY) return
    if (await chay(() => goiApi(`${API_LIEN_HE}/${l.maLH}`, { method: 'DELETE' }))) {
      setDangDoc((cu) => (cu && cu.maLH === l.maLH ? null : cu))
      if (duLieu?.items?.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      lamMoi()
    }
  }

  const dangLoc = !!(loc.q || loc.xuLy)

  const COT = [
    {
      khoa: 'nguoiGui',
      ten: 'Người gửi',
      hien: (l) => (
        <>
          <div className="fw-semibold">{l.hoTen}</div>
          <div className="text-body-secondary small">
            {l.email || '—'} · {l.soDienThoai || '—'}
          </div>
        </>
      ),
    },
    { khoa: 'chuDe', ten: 'Chủ đề', an: 'md', hien: (l) => l.chuDe || '—' },
    { khoa: 'noiDung', ten: 'Nội dung', lop: 'small kd-mot-dong', hien: (l) => l.noiDung || '—' },
    { khoa: 'ngay', ten: 'Ngày gửi', an: 'lg', lop: 'small text-nowrap', hien: (l) => dinhDangNgayGio(l.ngayGui) },
    { khoa: 'trangThai', ten: 'Trạng thái', canPhai: true, hien: (l) => <NhanTrangThai giaTri={l.daXuLy ? 'Đã xử lý' : 'Chưa xử lý'} mau={l.daXuLy ? 'ok' : 'warn'} /> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (l) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-secondary me-1" title="Đọc nội dung" aria-label="Đọc nội dung" onClick={() => setDangDoc(l)}>
            <i className="bi bi-envelope-open" />
          </button>
          <button
            type="button"
            className={'btn btn-sm me-1 ' + (l.daXuLy ? 'btn-outline-secondary' : 'btn-outline-success')}
            title={l.daXuLy ? 'Đánh dấu chưa xử lý' : 'Đánh dấu đã xử lý'}
            aria-label={l.daXuLy ? 'Đánh dấu chưa xử lý' : 'Đánh dấu đã xử lý'}
            disabled={dangChay}
            onClick={() => doiXuLy(l)}
          >
            <i className={'bi ' + (l.daXuLy ? 'bi-arrow-counterclockwise' : 'bi-check2')} />
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa" aria-label="Xóa" disabled={dangChay} onClick={() => xoa(l)}>
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-4">
          <Link className={'kd-the-lien-ket' + (loc.xuLy === 'chua' ? ' dang-chon' : '')} to={'/admin/contacts' + taoQuery({ xuLy: 'chua' })}>
            <TheChiSo icon="bi-envelope-exclamation" mau={tk?.chuaXuLy ? 'do' : 'xanh-ngoc'} so={tk ? dinhDangSo(tk.chuaXuLy) : '…'} nhan="Liên hệ chưa xử lý" />
          </Link>
        </div>
        <div className="col-6 col-xl-4">
          <Link className={'kd-the-lien-ket' + (loc.xuLy === 'da' ? ' dang-chon' : '')} to={'/admin/contacts' + taoQuery({ xuLy: 'da' })}>
            <TheChiSo icon="bi-envelope-check" mau="la" so={tk ? dinhDangSo(tk.daXuLy) : '…'} nhan="Đã xử lý" />
          </Link>
        </div>
        <div className="col-12 col-xl-4">
          <Link className={'kd-the-lien-ket' + (loc.xuLy === '' ? ' dang-chon' : '')} to="/admin/contacts">
            <TheChiSo icon="bi-chat-left-text" mau="xanh" so={tk ? dinhDangSo(tk.tong) : '…'} nhan="Tổng liên hệ từ website" />
          </Link>
        </div>
      </div>

      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe="Liên hệ từ khách truy cập website"
        icon="bi-chat-left-text"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Tên, email, SĐT, nội dung…" />
            <select className="form-select form-select-sm" style={{ width: 170 }} value={loc.xuLy} onChange={(e) => datLoc({ xuLy: e.target.value, trang: 1 })} aria-label="Lọc theo trạng thái xử lý">
              <option value="">— Tất cả —</option>
              <option value="chua">Chưa xử lý</option>
              <option value="da">Đã xử lý</option>
            </select>
            {dangLoc && (
              <button type="button" className="btn btn-sm btn-light" onClick={xoaLoc}>
                Xóa lọc
              </button>
            )}
          </>
        }
        chan={duLieu && <PhanTrang {...duLieu} doiTrang={(t) => datLoc({ trang: t })} donVi="liên hệ" />}
      >
        <BangDuLieu
          cot={COT}
          dong={duLieu?.items}
          khoaDong="maLH"
          dangTai={dangTai}
          lopDong={(l) => (l.daXuLy ? 'kd-dong-ngung' : '')}
          rong={dangLoc ? 'Không có liên hệ khớp bộ lọc.' : 'Chưa có liên hệ nào.'}
        />
      </TheNoiDung>

      <HopThoai
        mo={!!dangDoc}
        dong={() => setDangDoc(null)}
        kichThuoc="lg"
        tieuDe={dangDoc?.chuDe || 'Nội dung liên hệ'}
        icon="bi-envelope-open"
        chan={
          dangDoc && (
            <>
              <button type="button" className="btn btn-light" onClick={() => setDangDoc(null)}>
                Đóng
              </button>
              <button type="button" className={'btn ' + (dangDoc.daXuLy ? 'btn-outline-secondary' : 'btn-success')} disabled={dangChay} onClick={() => doiXuLy(dangDoc)}>
                <i className={'bi me-1 ' + (dangDoc.daXuLy ? 'bi-arrow-counterclockwise' : 'bi-check2')} />
                {dangDoc.daXuLy ? 'Chuyển về chưa xử lý' : 'Đánh dấu đã xử lý'}
              </button>
            </>
          )
        }
      >
        {dangDoc && (
          <>
            <div className="row g-2 small mb-3">
              <div className="col-md-6">
                <span className="text-body-secondary">Người gửi:</span> <strong>{dangDoc.hoTen}</strong>
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">Ngày gửi:</span> {dinhDangNgayGio(dangDoc.ngayGui)}
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">Email:</span> {dangDoc.email ? <a href={'mailto:' + dangDoc.email}>{dangDoc.email}</a> : '—'}
              </div>
              <div className="col-md-6">
                <span className="text-body-secondary">Điện thoại:</span> {dangDoc.soDienThoai ? <a href={'tel:' + dangDoc.soDienThoai}>{dangDoc.soDienThoai}</a> : '—'}
              </div>
            </div>
            <div className="kd-noi-dung-thu">{dangDoc.noiDung}</div>
          </>
        )}
      </HopThoai>
    </>
  )
}
