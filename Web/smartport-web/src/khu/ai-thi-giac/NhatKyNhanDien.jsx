import { useState } from 'react'
import { Link } from 'react-router-dom'
import { taiFile, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, PhanTrang, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, gioNgayNgan } from '../../utils/dinhDang'
import './ai-thi-giac.css'

const KY = [
  { ngay: 1, ten: 'Hôm nay' },
  { ngay: 7, ten: '7 ngày' },
  { ngay: 30, ten: '30 ngày' },
]
const MUC = ['Bình thường', 'Chú ý', 'Ùn tắc']
const NGUON = ['Webcam', 'CameraIP', 'TaiAnh', 'AnhMau']
const MAU_MUC = { 'Ùn tắc': 'do', 'Chú ý': 'warn', 'Bình thường': 'ok' }

/** Nhat ky nhan dien Camera AI (thay Admin/Vision/History) — loc theo kỳ, mức cảnh báo, nguồn. */
export default function NhatKyNhanDien() {
  const [loc, datLoc] = useBoLoc({ ngay: 7, muc: '', nguon: '', trang: 1 })
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/camera-ai/lich-su' + taoQuery(loc))
  const [xem, setXem] = useState(null)
  const { chay, dangChay } = useThaoTac()

  const COT = [
    { khoa: 'thoiGian', ten: 'Thời gian', hien: (n) => <span className="fw-semibold">{gioNgayNgan(n.thoiGian)}</span> },
    { khoa: 'viTri', ten: 'Vị trí', hien: (n) => n.viTriCamera || '—' },
    { khoa: 'nguon', ten: 'Nguồn', an: 'md', hien: (n) => <span className="badge text-bg-light border">{n.nguon}</span> },
    { khoa: 'model', ten: 'Mô hình', an: 'lg', hien: (n) => <span className="small">{n.tenModel || '—'}</span> },
    { khoa: 'xeTai', ten: 'Xe tải', canPhai: true, hien: (n) => <span className="fw-bold text-primary">{n.soXeTai}</span> },
    { khoa: 'oto', ten: 'Ô tô', canPhai: true, an: 'md', hien: (n) => n.soOto },
    { khoa: 'nguoi', ten: 'Người', canPhai: true, an: 'md', hien: (n) => n.soNguoi },
    { khoa: 'tong', ten: 'Tổng', canPhai: true, hien: (n) => n.tongDoiTuong },
    { khoa: 'tinCay', ten: 'Tin cậy', canPhai: true, an: 'lg', hien: (n) => (n.doTinCayTB != null ? Math.round(n.doTinCayTB) + '%' : '—') },
    { khoa: 'xuLy', ten: 'Xử lý', canPhai: true, an: 'xl', hien: (n) => (n.thoiGianXuLy != null ? n.thoiGianXuLy + ' ms' : '—') },
    { khoa: 'canhBao', ten: 'Cảnh báo', hien: (n) => <NhanTrangThai giaTri={n.mucCanhBao} mau={MAU_MUC[n.mucCanhBao]} /> },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="card sp-the mb-3">
        <div className="card-body d-flex flex-wrap align-items-end gap-2">
          <div className="btn-group btn-group-sm" role="group" aria-label="Khoảng thời gian">
            {KY.map((k) => (
              <button key={k.ngay} type="button" className={'btn ' + (loc.ngay === k.ngay ? 'btn-primary' : 'btn-outline-secondary')} onClick={() => datLoc({ ngay: k.ngay, trang: 1 })}>
                {k.ten}
              </button>
            ))}
          </div>
          <div>
            <label className="form-label small fw-semibold mb-1" htmlFor="tg-muc">
              Mức cảnh báo
            </label>
            <select id="tg-muc" className="form-select form-select-sm" value={loc.muc} onChange={(e) => datLoc({ muc: e.target.value, trang: 1 })}>
              <option value="">— Tất cả —</option>
              {MUC.map((m) => (
                <option key={m}>{m}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="form-label small fw-semibold mb-1" htmlFor="tg-nguon">
              Nguồn
            </label>
            <select id="tg-nguon" className="form-select form-select-sm" value={loc.nguon} onChange={(e) => datLoc({ nguon: e.target.value, trang: 1 })}>
              <option value="">— Tất cả —</option>
              {NGUON.map((n) => (
                <option key={n}>{n}</option>
              ))}
            </select>
          </div>
          <div className="flex-grow-1" />
          <button
            type="button"
            className="btn btn-sm btn-warning"
            disabled={dangChay}
            onClick={() => chay(() => taiFile('/api/camera-ai/xuat-excel?ngay=' + loc.ngay, { tenMacDinh: 'BaoCao_LuuLuong_Camera_AI.xlsx' }), { thanhCong: 'Đã tải báo cáo Excel.' })}
          >
            <i className="bi bi-file-earmark-excel me-1" />
            Xuất Excel
          </button>
          <Link className="btn btn-sm btn-outline-secondary" to="/admin/vision/analytics">
            <i className="bi bi-graph-up-arrow me-1" />
            Thống kê
          </Link>
          <Link className="btn btn-sm btn-outline-secondary" to="/admin/vision">
            <i className="bi bi-camera-video me-1" />
            Camera AI
          </Link>
        </div>
      </div>

      <TheNoiDung tieuDe="Nhật ký nhận diện AI" icon="bi-clock-history" khongDem>
        <BangDuLieu
          cot={COT}
          dong={duLieu?.items}
          khoaDong="maND"
          dangTai={dangTai}
          bamDong={setXem}
          rong="Chưa có dữ liệu nhận diện trong khoảng thời gian này."
        />
        <div className="px-3 pb-3">
          <PhanTrang {...(duLieu || {})} doiTrang={(t) => datLoc({ trang: t })} donVi="lượt quét" />
        </div>
      </TheNoiDung>

      <HopThoai mo={!!xem} dong={() => setXem(null)} tieuDe={'Lượt quét #' + (xem?.maND ?? '')} icon="bi-bounding-box-circles">
        {xem && (
          <>
            <div className="row g-2 small mb-3">
              <div className="col-6">
                <div className="text-body-secondary">Thời gian</div>
                <div className="fw-semibold">{dinhDangNgayGio(xem.thoiGian)}</div>
              </div>
              <div className="col-6">
                <div className="text-body-secondary">Vị trí camera</div>
                <div className="fw-semibold">{xem.viTriCamera || '—'}</div>
              </div>
              <div className="col-6">
                <div className="text-body-secondary">Nguồn · mô hình</div>
                <div className="fw-semibold">
                  {xem.nguon} · {xem.tenModel || '—'}
                </div>
              </div>
              <div className="col-6">
                <div className="text-body-secondary">Mức cảnh báo</div>
                <NhanTrangThai giaTri={xem.mucCanhBao} mau={MAU_MUC[xem.mucCanhBao]} />
              </div>
            </div>
            <table className="table table-sm sp-bang align-middle mb-0">
              <thead>
                <tr>
                  <th>Đối tượng</th>
                  <th className="text-end">Số lượng</th>
                  <th className="text-end">Độ tin cậy</th>
                </tr>
              </thead>
              <tbody>
                {(xem.chiTiet || []).map((c) => (
                  <tr key={c.nhanGoc || c.nhan}>
                    <td>{c.nhan}</td>
                    <td className="text-end fw-semibold">{c.soLuong}</td>
                    <td className="text-end">{c.doTinCay != null ? c.doTinCay + '%' : '—'}</td>
                  </tr>
                ))}
                {!xem.chiTiet?.length && (
                  <tr>
                    <td colSpan={3} className="text-center text-body-secondary py-3">
                      Không có chi tiết đối tượng.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </>
        )}
      </HopThoai>
    </>
  )
}
