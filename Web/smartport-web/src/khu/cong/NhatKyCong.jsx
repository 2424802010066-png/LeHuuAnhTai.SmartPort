import { useState } from 'react'
import { Link } from 'react-router-dom'
import { taiFile, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, OTimKiem, PhanTrang, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import { AnhNho, BienSoXe, CAC_CONG, HopAnhXe, NhanHuong, SoCont, gioGiayNgay } from './dungChung'
import './cong.css'

const KHOANG = [
  { gt: 1, ten: 'Hôm nay' },
  { gt: 7, ten: '7 ngày gần nhất' },
  { gt: 30, ten: '30 ngày gần nhất' },
  { gt: 90, ten: '90 ngày gần nhất' },
]

/**
 * NHAT KY XE RA/VAO CONG (thay Admin/Gate/History + ExportExcel): loc theo bien so, huong,
 * cong, khoang thoi gian; xuat Excel dung bo loc dang xem. Bo loc luu tren URL de gui link duoc.
 */
export default function NhatKyCong() {
  const [loc, datLoc, xoaLoc] = useBoLoc({ bienSo: '', huong: '', cong: '', ngay: 7, trang: 1, coTrang: 50 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/nhat-ky-cong' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const [anhXem, setAnhXem] = useState(null)

  // Xe qua cong la co su kien tu module Cong -> nhat ky tu lam moi, khong can bam F5
  useNgheSuKien('cong.*', () => taiLai({ im: true }))

  const xuatExcel = () =>
    chay(() => taiFile('/api/nhat-ky-cong/excel' + taoQuery({ bienSo: loc.bienSo, huong: loc.huong, cong: loc.cong, ngay: loc.ngay })), {
      thanhCong: 'Đã tải file Excel nhật ký xe ra/vào.',
    })

  const COT = [
    { khoa: 'gio', ten: 'Thời gian', hien: (x) => <span className="fw-semibold small">{gioGiayNgay(x.thoiGian)}</span> },
    { khoa: 'anh', ten: 'Ảnh xe', rong: 74, hien: (x) => <AnhNho anh={x.anh} bienSo={x.bienSo} moAnh={(a) => setAnhXem({ ...a, luc: x.thoiGian })} /> },
    { khoa: 'bienSo', ten: 'Biển số', hien: (x) => <BienSoXe bienSo={x.bienSo} /> },
    { khoa: 'cong', ten: 'Cổng', an: 'md', hien: (x) => x.cong || '—' },
    { khoa: 'huong', ten: 'Hướng', hien: (x) => <NhanHuong huong={x.huongDi} /> },
    { khoa: 'cont', ten: 'Container', an: 'md', hien: (x) => <SoCont so={x.soCont} /> },
    { khoa: 'loai', ten: 'Loại xe', an: 'lg', hien: (x) => <span className="small">{x.loaiXe || '—'}</span> },
    { khoa: 'ocr', ten: 'Tin cậy OCR', canPhai: true, an: 'lg', hien: (x) => (x.doTinCay != null ? dinhDangSo(x.doTinCay, 1) + '%' : '—') },
    { khoa: 'nguon', ten: 'Nguồn', an: 'xl', hien: (x) => <span className="small text-body-secondary">{x.nguon || '—'}</span> },
    { khoa: 'phi', ten: 'Phí cổng', canPhai: true, hien: (x) => (x.phiCong ? dinhDangTien(x.phiCong) : '—') },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-arrow-in-right" mau="la" so={dinhDangSo(d?.soVao ?? 0)} nhan="lượt xe vào" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-arrow-right" mau="cam" so={dinhDangSo(d?.soRa ?? 0)} nhan="lượt xe ra" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-list-ol" mau="xanh" so={dinhDangSo(d?.tong ?? 0)} nhan="lượt khớp bộ lọc" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-calendar-range" mau="xanh-ngoc" nho so={KHOANG.find((k) => k.gt === loc.ngay)?.ten || loc.ngay + ' ngày'} nhan="khoảng thời gian" />
        </div>
      </div>

      <TheNoiDung lop="mb-3">
        <div className="row g-2 align-items-end">
          <div className="col-md-3">
            <span className="form-label small fw-semibold mb-1 d-block">Biển số</span>
            <OTimKiem giaTri={loc.bienSo} doi={(v) => datLoc({ bienSo: v, trang: 1 })} goiY="Biển số, VD: 15C" rong="100%" lop="w-100" />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-nk-huong">
              Hướng
            </label>
            <select id="cg-nk-huong" className="form-select form-select-sm" value={loc.huong} onChange={(e) => datLoc({ huong: e.target.value, trang: 1 })}>
              <option value="">— Tất cả —</option>
              <option value="Vào">Vào</option>
              <option value="Ra">Ra</option>
            </select>
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-nk-cong">
              Cổng
            </label>
            <select id="cg-nk-cong" className="form-select form-select-sm" value={loc.cong} onChange={(e) => datLoc({ cong: e.target.value, trang: 1 })}>
              <option value="">— Tất cả —</option>
              {CAC_CONG.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-nk-ngay">
              Khoảng thời gian
            </label>
            <select id="cg-nk-ngay" className="form-select form-select-sm" value={loc.ngay} onChange={(e) => datLoc({ ngay: Number(e.target.value), trang: 1 })}>
              {KHOANG.map((k) => (
                <option key={k.gt} value={k.gt}>
                  {k.ten}
                </option>
              ))}
            </select>
          </div>
          <div className="col-6 col-md-3 d-flex gap-2">
            <button type="button" className="btn btn-sm btn-outline-secondary" onClick={xoaLoc}>
              Xóa lọc
            </button>
            <button type="button" className="btn btn-sm btn-success flex-fill" onClick={xuatExcel} disabled={dangChay}>
              <i className="bi bi-file-earmark-excel me-1" />
              Xuất Excel
            </button>
          </div>
        </div>
      </TheNoiDung>

      <TheNoiDung
        tieuDe="Nhật ký xe ra / vào cổng (ALPR)"
        icon="bi-clock-history"
        khongDem
        phai={
          <Link className="btn btn-sm btn-outline-secondary" to="/admin/gate">
            <i className="bi bi-upc-scan me-1" />
            Về cổng thông minh
          </Link>
        }
        chan={<PhanTrang trang={d?.trang} soTrang={d?.soTrang} tong={d?.tong} donVi="lượt xe" doiTrang={(t) => datLoc({ trang: t })} />}
      >
        <BangDuLieu cot={COT} dong={d?.items} khoaDong="maXV" dangTai={dangTai} rong="Không có lượt xe nào khớp bộ lọc." />
      </TheNoiDung>

      <HopAnhXe anh={anhXem} dong={() => setAnhXem(null)} />
    </>
  )
}
