import { useState } from 'react'
import { goiApi, taoBoKhoaGuiLap, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, OTimKiem, TheChiSo, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, gioNgayNgan, tienGon } from '../../utils/dinhDang'
import './cang-so.css'

const MAU_LUONG = { Xanh: 'ok', 'Vàng': 'warn', 'Đỏ': 'do' }
const MAU_TT = {
  'Đã khai báo': 'info',
  'Chờ kiểm tra': 'warn',
  'Đã thông quan': 'ok',
  'Đã qua khu vực giám sát': 'off',
  'Tạm dừng': 'do',
}
const LOAI_HINH = [
  ['A11', 'A11 — Nhập kinh doanh tiêu dùng'],
  ['A12', 'A12 — Nhập kinh doanh sản xuất'],
  ['E31', 'E31 — Nhập nguyên liệu sản xuất xuất khẩu'],
  ['B11', 'B11 — Xuất kinh doanh'],
]

/**
 * THONG QUAN HAI QUAN (mo phong VNACCS + VASSCM): to khai, phan luong Xanh / Vang / Do bang mo hinh diem rui ro
 * (co ly do), thong quan, tam dung. Container nhap chua thong quan -> cong khong cho ra khoi khu vuc giam sat.
 */
export default function HaiQuan() {
  const [loc, datLoc] = useBoLoc({ tuKhoa: '', trangThai: '', luong: '' })
  const { duLieu: ds, loi, dangTai, taiLai } = useApi('/api/hai-quan' + taoQuery(loc))
  const { duLieu: tk, taiLai: taiTk } = useApi('/api/hai-quan/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [moKhai, setMoKhai] = useState(false)
  const [xem, setXem] = useState(null)

  const lamMoi = () => {
    taiLai({ im: true })
    taiTk({ im: true })
  }
  useNgheSuKien('hai-quan.*', lamMoi)

  async function thongQuan(t) {
    const dongY = await xacNhan({
      tieuDe: 'Thông quan tờ khai ' + t.soToKhai + '?',
      noiDung: 'Container ' + t.soCont + ' sẽ đủ điều kiện qua khu vực giám sát — cổng cho xe nhận hàng.',
      nutDongY: 'Thông quan',
    })
    if (dongY && (await chay(() => goiApi('/api/hai-quan/' + t.maToKhai + '/thong-quan', { method: 'POST', body: { ghiChu: 'Đã kiểm tra' } })))) lamMoi()
  }

  async function tamDung(t) {
    const dongY = await xacNhan({
      tieuDe: 'Tạm dừng container ' + t.soCont + '?',
      noiDung: 'Cổng sẽ KHÔNG cho container ra cho đến khi hải quan thông quan lại.',
      nutDongY: 'Tạm dừng',
      mau: 'danger',
    })
    if (dongY && (await chay(() => goiApi('/api/hai-quan/' + t.maToKhai + '/tam-dung', { method: 'POST', body: { ghiChu: 'Nghi vấn — chờ kiểm tra thêm' } })))) lamMoi()
  }

  const COT = [
    { khoa: 'so', ten: 'Số tờ khai', hien: (t) => <span className="cs-ma">{t.soToKhai}</span> },
    { khoa: 'lh', ten: 'Loại hình', an: 'md', hien: (t) => <span className="small">{t.loaiHinh} · {t.huongHang}</span> },
    { khoa: 'cont', ten: 'Container', hien: (t) => <span className="cs-ma">{t.soCont}</span> },
    { khoa: 'hang', ten: 'Hàng · HS', an: 'lg', hien: (t) => <span className="small">{t.tenHang || '—'}{t.maHS ? ' · ' + t.maHS : ''}</span> },
    { khoa: 'tg', ten: 'Trị giá', an: 'xl', canPhai: true, hien: (t) => <span className="small">{t.triGia != null ? tienGon(t.triGia) : '—'}</span> },
    {
      khoa: 'luong',
      ten: 'Luồng',
      hien: (t) => (
        <button type="button" className="btn btn-link p-0 text-decoration-none" title="Xem lý do phân luồng" onClick={() => setXem(t)}>
          <NhanTrangThai giaTri={t.phanLuong} mau={MAU_LUONG[t.phanLuong]} />
        </button>
      ),
    },
    { khoa: 'tt', ten: 'Trạng thái', hien: (t) => <NhanTrangThai giaTri={t.trangThai} mau={MAU_TT[t.trangThai]} /> },
    { khoa: 'ngay', ten: 'Khai báo', an: 'xl', hien: (t) => <span className="small">{gioNgayNgan(t.ngayKhai)}</span> },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 96,
      hien: (t) => (
        <>
          {(t.trangThai === 'Chờ kiểm tra' || t.trangThai === 'Tạm dừng' || t.trangThai === 'Đã khai báo') && (
            <button type="button" className="btn btn-sm btn-outline-success me-1" title="Thông quan" disabled={dangChay} onClick={() => thongQuan(t)}>
              <i className="bi bi-check2-circle" />
            </button>
          )}
          {t.trangThai !== 'Tạm dừng' && t.trangThai !== 'Đã qua khu vực giám sát' && (
            <button type="button" className="btn btn-sm btn-outline-danger" title="Tạm dừng" disabled={dangChay} onClick={() => tamDung(t)}>
              <i className="bi bi-pause-circle" />
            </button>
          )}
        </>
      ),
    },
  ]

  const tong = tk?.tong || 0
  const phanTram = (n) => (tong ? Math.round((100 * n) / tong) + '%' : '—')

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-traffic-light" mau="la" so={phanTram(tk?.xanh ?? 0)} nhan={'luồng Xanh (' + (tk?.xanh ?? 0) + ' tờ khai)'} />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-hourglass-split" mau="cam" so={dinhDangSo(tk?.choKiemTra ?? 0)} nhan="chờ kiểm tra (Vàng / Đỏ)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-pause-circle" mau="do" so={dinhDangSo(tk?.tamDung ?? 0)} nhan="container tạm dừng" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-stopwatch" mau="xanh" so={dinhDangSo(tk?.gioThongQuanTrungBinh ?? 0, 1) + ' giờ'} nhan="thông quan trung bình" />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Tờ khai hải quan & giám sát container (VASSCM)"
        icon="bi-bank"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={loc.tuKhoa} doi={(v) => datLoc({ tuKhoa: v })} goiY="Số tờ khai, container…" />
            <select className="form-select form-select-sm" style={{ maxWidth: 120 }} value={loc.luong} onChange={(e) => datLoc({ luong: e.target.value })} aria-label="Lọc luồng">
              <option value="">— Luồng —</option>
              <option>Xanh</option>
              <option>Vàng</option>
              <option>Đỏ</option>
            </select>
            <select className="form-select form-select-sm" style={{ maxWidth: 170 }} value={loc.trangThai} onChange={(e) => datLoc({ trangThai: e.target.value })} aria-label="Lọc trạng thái">
              <option value="">— Mọi trạng thái —</option>
              {Object.keys(MAU_TT).map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
            <button type="button" className="btn btn-sm btn-primary" onClick={() => setMoKhai(true)}>
              <i className="bi bi-plus-lg me-1" />
              Khai báo tờ khai
            </button>
          </>
        }
      >
        <BangDuLieu cot={COT} dong={ds} khoaDong="maToKhai" dangTai={dangTai} rong="Chưa có tờ khai nào." />
      </TheNoiDung>

      <KhaiBao
        mo={moKhai}
        dong={() => setMoKhai(false)}
        xong={() => {
          setMoKhai(false)
          lamMoi()
        }}
      />
      <HopThoai mo={!!xem} dong={() => setXem(null)} tieuDe={'Phân luồng tờ khai ' + (xem?.soToKhai ?? '')} icon="bi-traffic-light">
        {xem && (
          <>
            <div className="mb-2">
              <NhanTrangThai giaTri={'Luồng ' + xem.phanLuong} mau={MAU_LUONG[xem.phanLuong]} />
            </div>
            <p className="small">{xem.lyDoPhanLuong}</p>
            <p className="small text-body-secondary mb-0">
              Mô hình rủi ro mô phỏng: doanh nghiệp chưa có lịch sử thông quan, mã HS nhóm rủi ro (máy tính, dược phẩm, thuốc lá, vũ khí, phế liệu…), trị giá lớn,
              loại hình sản xuất (giảm điểm) và mẫu kiểm tra ngẫu nhiên ~5%. Dưới 30 điểm: Xanh (tự thông quan); 30–59: Vàng (kiểm hồ sơ); từ 60: Đỏ (kiểm
              thực tế hàng).
            </p>
          </>
        )}
      </HopThoai>
    </>
  )
}

function KhaiBao({ mo, dong, xong }) {
  const { duLieu: lc } = useApi(mo ? '/api/hai-quan/lua-chon' : null)
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState({ soCont: '', loaiHinh: 'A11', tenHang: '', maHS: '', triGia: '', soToKhai: '' })
  const [khoaGui] = useState(taoBoKhoaGuiLap) // Idempotency-Key: bam lai sau loi mang khong khai 2 to khai
  const doi = (k) => (e) => setF((x) => ({ ...x, [k]: e.target.value }))

  async function gui(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/hai-quan', { method: 'POST', body: { ...f, triGia: f.triGia ? Number(f.triGia) : null }, chongGuiLap: khoaGui }))
    if (kq) xong()
  }

  return (
    <HopThoai
      mo={mo}
      dong={dong}
      khoaDong={dangChay}
      tieuDe="Khai báo tờ khai hải quan"
      icon="bi-file-earmark-text"
      khiGui={gui}
      chan={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={dong} disabled={dangChay}>
            Đóng
          </button>
          <button type="submit" className="btn btn-primary" disabled={dangChay || !f.soCont}>
            Khai báo & phân luồng
          </button>
        </>
      }
    >
      <Truong nhan="Container" batBuoc>
        <select className="form-select" value={f.soCont} onChange={doi('soCont')} required>
          <option value="">— Chọn container trong bãi —</option>
          {(lc?.container ?? []).map((c) => (
            <option key={c.soCont} value={c.soCont}>
              {c.soCont} · {c.loaiCont}
              {c.hangHoa ? ' · ' + c.hangHoa : ''}
            </option>
          ))}
        </select>
      </Truong>
      <Truong nhan="Loại hình">
        <select className="form-select" value={f.loaiHinh} onChange={doi('loaiHinh')}>
          {LOAI_HINH.map(([ma, ten]) => (
            <option key={ma} value={ma}>
              {ten}
            </option>
          ))}
        </select>
      </Truong>
      <div className="row g-2">
        <div className="col-7">
          <Truong nhan="Tên hàng">
            <input className="form-control" value={f.tenHang} onChange={doi('tenHang')} maxLength={200} />
          </Truong>
        </div>
        <div className="col-5">
          <Truong nhan="Mã HS" goiY="vd 8471 (máy tính)">
            <input className="form-control" value={f.maHS} onChange={doi('maHS')} maxLength={10} />
          </Truong>
        </div>
      </div>
      <div className="row g-2">
        <div className="col-6">
          <Truong nhan="Trị giá (đồng)">
            <input className="form-control" type="number" min={0} value={f.triGia} onChange={doi('triGia')} />
          </Truong>
        </div>
        <div className="col-6">
          <Truong nhan="Số tờ khai" goiY="Bỏ trống = hệ thống cấp 12 số">
            <input className="form-control" value={f.soToKhai} onChange={doi('soToKhai')} maxLength={12} />
          </Truong>
        </div>
      </div>
    </HopThoai>
  )
}
