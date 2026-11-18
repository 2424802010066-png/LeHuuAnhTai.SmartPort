import { useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'

const MAU_TB = { 'Sẵn sàng': 'ok', 'Đang làm': 'run', 'Bảo trì': 'warn' }
const MAU_L = { 'Chờ phân công': 'warn', 'Đã phân công': 'info', 'Đang làm': 'run', 'Hoàn tất': 'ok' }
const TEN_TB = { RTG: 'Cẩu khung RTG', RS: 'Reach stacker', FL: 'Xe nâng vỏ rỗng' }
const TRONG = { soCont: '', loaiViec: 'Nâng lên xe', denViTri: '', uuTien: 1 }

/** DIEU PHOI THIET BI BAI: tao lenh nang/ha/dao container, tu giao cho thiet bi phu hop gan nhat de giam chay rong. */
export default function ThietBiBai() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/thiet-bi-bai')
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState(TRONG)
  const [kq, setKq] = useState(null)

  const lam = async (url) => {
    if (await chay(() => goiApi(url, { method: 'POST' }))) taiLai({ im: true })
  }

  async function taoLenh(e) {
    e.preventDefault()
    if (await chay(() => goiApi('/api/thiet-bi-bai/lenh', { method: 'POST', body: f }))) {
      setF(TRONG)
      taiLai({ im: true })
    }
  }

  async function phanCong() {
    const r = await chay(() => goiApi('/api/thiet-bi-bai/phan-cong', { method: 'POST' }), { imLang: true })
    if (r) {
      setKq(r)
      taiLai({ im: true })
    }
  }

  const can = f.loaiViec !== 'Nâng lên xe'

  const COT = [
    { khoa: 'so', ten: 'Container', hien: (l) => <span className="font-monospace fw-semibold">{l.soCont}</span> },
    { khoa: 'viec', ten: 'Việc', hien: (l) => <span className="small">{l.loaiViec}</span> },
    { khoa: 'vt', ten: 'Từ → đến', an: 'md', hien: (l) => <span className="font-monospace small">{l.tuViTri || '(xe)'} → {l.denViTri || '(xe)'}</span> },
    { khoa: 'ut', ten: 'Ưu tiên', an: 'lg', hien: (l) => <span className="small">{'★'.repeat(l.uuTien)}</span> },
    { khoa: 'tb', ten: 'Thiết bị', hien: (l) => <span className="small">{l.thietBi || '—'}{l.quangDuongRongM != null ? ' · chạy rỗng ' + l.quangDuongRongM + ' m' : ''}</span> },
    { khoa: 'tt', ten: 'Trạng thái', hien: (l) => <NhanTrangThai giaTri={l.trangThai} mau={MAU_L[l.trangThai]} /> },
    {
      khoa: 'nut',
      ten: '',
      canPhai: true,
      hien: (l) => (
        <>
          {l.trangThai === 'Đã phân công' && <button type="button" className="btn btn-sm btn-outline-primary" disabled={dangChay} onClick={() => lam('/api/thiet-bi-bai/lenh/' + l.maLenhTB + '/bat-dau')}>Nhận lệnh</button>}
          {l.trangThai === 'Đang làm' && <button type="button" className="btn btn-sm btn-success" disabled={dangChay} onClick={() => lam('/api/thiet-bi-bai/lenh/' + l.maLenhTB + '/xong')}>Hoàn tất</button>}
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3"><TheChiSo icon="bi-check-circle" mau="la" so={d?.chiSo.sanSang ?? 0} nhan="thiết bị sẵn sàng" /></div>
        <div className="col-6 col-lg-3"><TheChiSo icon="bi-gear-wide-connected" mau="xanh" so={d?.chiSo.dangLam ?? 0} nhan={'đang làm · ' + (d?.chiSo.baoTri ?? 0) + ' bảo trì'} /></div>
        <div className="col-6 col-lg-3"><TheChiSo icon="bi-inbox" mau="cam" so={d?.chiSo.choPhanCong ?? 0} nhan="lệnh chờ phân công" /></div>
        <div className="col-6 col-lg-3"><TheChiSo icon="bi-stopwatch" mau="do" so={d?.chiSo.xongHomNay ?? 0} nhan={'lệnh xong hôm nay · TB ' + (d?.chiSo.phutTrungBinh ?? 0) + ' phút'} /></div>
      </div>

      <div className="row g-3">
        <div className="col-12 col-xl-8">
          <TheNoiDung
            tieuDe="Lệnh công việc"
            icon="bi-list-task"
            phai={
              <button type="button" className="btn btn-sm btn-primary" disabled={dangChay || !(d?.chiSo.choPhanCong > 0)} onClick={phanCong}>
                <i className="bi bi-magic me-1" />
                Tự phân công
              </button>
            }
          >
            {kq && (
              <div className="alert alert-success py-2 small">
                Đã giao {kq.soLenh} lệnh ({kq.conCho} lệnh còn chờ thiết bị). Chạy rỗng {kq.quangDuongM} m, giao theo thứ tự danh sách sẽ là {kq.theoThuTuM} m — tiết kiệm <b>{kq.tietKiemM} m</b>.
              </div>
            )}
            <BangDuLieu cot={COT} dong={d?.lenh} khoaDong="maLenhTB" dangTai={dangTai} rong="Chưa có lệnh nào đang mở." />
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Tạo lệnh" icon="bi-plus-circle" khongDem>
            <form onSubmit={taoLenh}>
              <div className="mb-2">
                <label className="form-label small mb-1" htmlFor="tb-so">Container trong bãi</label>
                <select id="tb-so" className="form-select form-select-sm" required value={f.soCont} onChange={(e) => setF({ ...f, soCont: e.target.value })}>
                  <option value="">— Chọn container —</option>
                  {(d?.contTrongBai ?? []).map((c) => <option key={c.soCont} value={c.soCont}>{c.soCont} · {c.viTriBai}</option>)}
                </select>
              </div>
              <div className="mb-2">
                <label className="form-label small mb-1" htmlFor="tb-viec">Loại việc</label>
                <select id="tb-viec" className="form-select form-select-sm" value={f.loaiViec} onChange={(e) => setF({ ...f, loaiViec: e.target.value })}>
                  <option>Nâng lên xe</option>
                  <option>Hạ xuống bãi</option>
                  <option>Đảo chuyển</option>
                </select>
              </div>
              {can && (
                <div className="mb-2">
                  <label className="form-label small mb-1" htmlFor="tb-den">Vị trí đến (Khu-Bay-Hàng-Tầng)</label>
                  <input id="tb-den" className="form-control form-control-sm font-monospace" placeholder="A-02-03-1" required value={f.denViTri} onChange={(e) => setF({ ...f, denViTri: e.target.value.toUpperCase() })} />
                </div>
              )}
              <div className="mb-2">
                <label className="form-label small mb-1" htmlFor="tb-ut">Mức ưu tiên</label>
                <select id="tb-ut" className="form-select form-select-sm" value={f.uuTien} onChange={(e) => setF({ ...f, uuTien: Number(e.target.value) })}>
                  <option value={1}>Bình thường</option>
                  <option value={2}>Cao</option>
                  <option value={3}>Khẩn</option>
                </select>
              </div>
              <button className="btn btn-sm btn-primary w-100" disabled={dangChay}>Tạo lệnh</button>
            </form>
          </TheNoiDung>
          <TheNoiDung tieuDe="Đội thiết bị" icon="bi-truck-flatbed" khongDem>
            {(d?.thietBi ?? []).map((t) => (
              <div key={t.maThietBi} className="d-flex justify-content-between align-items-center border-bottom py-1 small">
                <span><b>{t.soHieu}</b> · {TEN_TB[t.loai]}</span>
                <span className="text-body-secondary">Khu {t.khuHienTai}, bay {t.bayHienTai}</span>
                <NhanTrangThai giaTri={t.trangThai} mau={MAU_TB[t.trangThai]} />
              </div>
            ))}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
