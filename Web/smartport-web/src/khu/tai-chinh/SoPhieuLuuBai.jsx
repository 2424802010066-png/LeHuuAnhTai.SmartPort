import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, PhanTrang, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangTien, gioNgayNgan } from '../../utils/dinhDang'
import { HINH_THUC_THU, tienO, useLamMoiTheoSuKien } from './dungChung'
import './tai-chinh.css'

const ngayThang = (v) => {
  const d = new Date(v)
  return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0')
}

/** So phieu thu phi luu bai = so cong no: thu tien, huy phieu, mo phieu in (thay Admin/LuuBai/DanhSach). */
export default function SoPhieuLuuBai() {
  const [loc, datLoc] = useBoLoc({ trangThai: '', q: '', trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/luu-bai/phieu' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()
  const [phieuThu, setPhieuThu] = useState(null)
  const [hinhThuc, setHinhThuc] = useState(HINH_THUC_THU[0])
  const [phieuHuy, setPhieuHuy] = useState(null)
  const [lyDo, setLyDo] = useState('')

  // Phieu moi do cong tu chot khi container roi bai
  useLamMoiTheoSuKien('tai-chinh.*', taiLai)

  function moThu(p) {
    setHinhThuc(HINH_THUC_THU[0])
    setPhieuThu(p)
  }

  function moHuy(p) {
    setLyDo('Chốt nhầm')
    setPhieuHuy(p)
  }

  async function xacNhanThu(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi(`/api/luu-bai/phieu/${phieuThu.maPhieu}/da-thu`, { method: 'POST', body: { hinhThuc } }))
    if (kq) {
      setPhieuThu(null)
      taiLai({ im: true })
    }
  }

  async function xacNhanHuy(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi(`/api/luu-bai/phieu/${phieuHuy.maPhieu}/huy`, { method: 'POST', body: { lyDo } }))
    if (kq) {
      setPhieuHuy(null)
      taiLai({ im: true })
    }
  }

  const COT = [
    {
      khoa: 'so',
      ten: 'Số phiếu',
      hien: (p) => (
        <>
          <Link className="fw-semibold text-decoration-none tc-ma" to={'/admin/luubai/phieu/' + p.maPhieu}>
            {p.soPhieu}
          </Link>
          <div className="small text-body-secondary">{gioNgayNgan(p.ngayLap)}</div>
        </>
      ),
    },
    {
      khoa: 'cont',
      ten: 'Container',
      hien: (p) => (
        <Link className="tc-ma text-decoration-none" to={'/admin/containers/tracuu?so=' + encodeURIComponent(p.soCont || '')}>
          {p.soCont}
        </Link>
      ),
    },
    {
      khoa: 'khach',
      ten: 'Khách hàng',
      an: 'md',
      hien: (p) => (
        <div className="small">
          {p.tenKhach || '—'}
          {p.tenHangTau && <div className="text-body-secondary">{p.tenHangTau}</div>}
        </div>
      ),
    },
    {
      khoa: 'ky',
      ten: 'Kỳ tính',
      an: 'lg',
      hien: (p) => (
        <div className="small text-body-secondary text-nowrap">
          {ngayThang(p.tuNgay)} → {new Date(p.denNgay).toLocaleDateString('vi-VN')}
          <div>
            {p.tongNgay} ngày · tính {p.ngayTinhPhi}
          </div>
        </div>
      ),
    },
    { khoa: 'tien', ten: 'Số tiền', canPhai: true, hien: (p) => <span className="fw-bold tc-tien text-nowrap">{dinhDangTien(p.tongTien)}</span> },
    {
      khoa: 'tt',
      ten: 'Trạng thái',
      hien: (p) =>
        p.trangThai === 'Đã thu' ? (
          <>
            <NhanTrangThai giaTri="Đã thu" />
            <div className="small text-body-secondary text-nowrap">
              {p.ngayThu ? ngayThang(p.ngayThu) : ''} · {p.hinhThucThu}
            </div>
          </>
        ) : p.trangThai === 'Hủy' ? (
          <span title={p.ghiChu || ''}>
            <NhanTrangThai giaTri="Hủy" />
          </span>
        ) : (
          <>
            <NhanTrangThai giaTri={p.trangThai} />
            {p.soNgayTreo > 30 ? (
              <div>
                <NhanTrangThai mau="do" giaTri={`treo ${p.soNgayTreo} ngày`} />
              </div>
            ) : p.soNgayTreo > 0 ? (
              <div className="small text-body-secondary">treo {p.soNgayTreo} ngày</div>
            ) : null}
          </>
        ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 130,
      hien: (p) => (
        <div className="text-nowrap">
          <Link className="btn btn-sm btn-outline-secondary" to={'/admin/luubai/phieu/' + p.maPhieu} target="_blank" rel="noopener" title="Mở phiếu để in" aria-label="Mở phiếu để in">
            <i className="bi bi-printer" />
          </Link>
          {p.trangThai === 'Chờ thu' && (
            <>
              <button type="button" className="btn btn-sm btn-outline-success ms-1" title="Ghi nhận đã thu tiền" aria-label="Ghi nhận đã thu tiền" disabled={dangChay} onClick={() => moThu(p)}>
                <i className="bi bi-cash-coin" />
              </button>
              <button type="button" className="btn btn-sm btn-outline-danger ms-1" title="Hủy phiếu" aria-label="Hủy phiếu" disabled={dangChay} onClick={() => moHuy(p)}>
                <i className="bi bi-x-lg" />
              </button>
            </>
          )}
        </div>
      ),
    },
  ]

  const LOC_TRANG_THAI = [
    ['', 'Tất cả'],
    ['Chờ thu', 'Chờ thu', d?.soChoThu],
    ['Đã thu', 'Đã thu', d?.soDaThu],
    ['Hủy', 'Hủy', d?.soHuy],
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      {d && (
        <div className="row g-3 mb-3">
          <div className="col-md-4">
            <TheChiSo icon="bi-hourglass-split" mau="cam" so={<span title={dinhDangTien(d.choThu)}>{tienO(d.choThu)}</span>} nhan={`Khách còn nợ · ${d.soChoThu} phiếu`} />
          </div>
          <div className="col-md-4">
            <TheChiSo
              icon="bi-telephone-outbound"
              mau={d.noQuaHan > 0 ? 'do' : 'la'}
              so={<span title={dinhDangTien(d.noQuaHan)}>{tienO(d.noQuaHan)}</span>}
              nhan={`Nợ quá 30 ngày · ${d.soNoQuaHan} phiếu cần gọi đòi`}
            />
          </div>
          <div className="col-md-4">
            <TheChiSo icon="bi-check2-circle" mau="la" so={<span title={dinhDangTien(d.daThu)}>{tienO(d.daThu)}</span>} nhan={`Đã thu · ${d.soDaThu} phiếu`} />
          </div>
        </div>
      )}

      <TheNoiDung
        tieuDe="Sổ phiếu thu phí lưu bãi"
        icon="bi-journal-text"
        khongDem
        phai={
          <>
            <div className="btn-group btn-group-sm" role="group" aria-label="Lọc trạng thái">
              {LOC_TRANG_THAI.map(([giaTri, nhan, so]) => (
                <button
                  key={nhan}
                  type="button"
                  className={'btn ' + (loc.trangThai === giaTri ? 'btn-primary' : 'btn-outline-primary')}
                  onClick={() => datLoc({ trangThai: giaTri, trang: 1 })}
                >
                  {nhan}
                  {so != null && <span className="ms-1 opacity-75">{so}</span>}
                </button>
              ))}
            </div>
            <OTimKiem giaTri={loc.q} doi={(q) => datLoc({ q, trang: 1 })} goiY="Số phiếu, số container…" rong={220} />
            <Link className="btn btn-sm btn-outline-secondary" to="/admin/luubai">
              <i className="bi bi-stopwatch me-1" />
              Bảng kê
            </Link>
          </>
        }
      >
        <BangDuLieu
          cot={COT}
          dong={d?.danhSach.items}
          khoaDong="maPhieu"
          dangTai={dangTai}
          lopDong={(p) => (p.trangThai === 'Hủy' ? 'tc-dong-huy' : '')}
          rong={loc.trangThai || loc.q ? 'Không có phiếu nào khớp bộ lọc.' : 'Chưa có phiếu nào — chốt phiếu từ bảng kê lưu bãi.'}
        />
        {d && (
          <div className="px-3 pb-2">
            <PhanTrang {...d.danhSach} doiTrang={(t) => datLoc({ trang: t })} donVi="phiếu" />
          </div>
        )}
      </TheNoiDung>

      <HopThoai
        mo={!!phieuThu}
        dong={() => setPhieuThu(null)}
        khoaDong={dangChay}
        tieuDe="Ghi nhận đã thu tiền"
        icon="bi-cash-coin"
        kichThuoc="sm"
        khiGui={xacNhanThu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setPhieuThu(null)} disabled={dangChay}>
              Thôi
            </button>
            <button type="submit" className="btn btn-success" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2-circle me-1" />}
              Đã thu
            </button>
          </>
        }
      >
        {phieuThu && (
          <>
            <div className="text-center mb-3">
              <div className="small text-body-secondary tc-ma">
                {phieuThu.soPhieu} · {phieuThu.soCont}
              </div>
              <div className="tc-so-tien-lon tc-tien">{dinhDangTien(phieuThu.tongTien)}</div>
              {phieuThu.tenKhach && <div className="small">{phieuThu.tenKhach}</div>}
            </div>
            <div className="form-label small fw-semibold mb-1">Hình thức thu</div>
            <div className="d-grid gap-1">
              {HINH_THUC_THU.map((h) => (
                <button key={h} type="button" className={'btn btn-sm ' + (h === hinhThuc ? 'btn-primary' : 'btn-outline-primary')} aria-pressed={h === hinhThuc} onClick={() => setHinhThuc(h)}>
                  {h}
                </button>
              ))}
            </div>
            <div className="form-text">Chỉ bấm khi tiền đã về — phiếu đã thu không hủy được.</div>
          </>
        )}
      </HopThoai>

      <HopThoai
        mo={!!phieuHuy}
        dong={() => setPhieuHuy(null)}
        khoaDong={dangChay}
        tieuDe="Hủy phiếu?"
        icon="bi-x-octagon"
        khiGui={xacNhanHuy}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setPhieuHuy(null)} disabled={dangChay}>
              Thôi
            </button>
            <button type="submit" className="btn btn-danger" disabled={dangChay || !lyDo.trim()}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-x-lg me-1" />}
              Hủy phiếu
            </button>
          </>
        }
      >
        {phieuHuy && (
          <>
            <p className="mb-3">
              <b className="tc-ma">{phieuHuy.soPhieu}</b> — <b className="tc-tien">{dinhDangTien(phieuHuy.tongTien)}</b>. Phiếu vẫn nằm trong sổ để truy vết.
            </p>
            <Truong nhan="Lý do hủy" batBuoc lop="mb-0">
              <input className="form-control" value={lyDo} onChange={(e) => setLyDo(e.target.value)} maxLength={250} autoFocus />
            </Truong>
          </>
        )}
      </HopThoai>
    </>
  )
}
