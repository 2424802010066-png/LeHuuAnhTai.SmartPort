import { useEffect, useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'

const gio = (v) => (v ? new Date(v).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '—')
const tien = (v) => Number(v || 0).toLocaleString('vi-VN') + ' đ'
const LOAI = { GiaoCont: 'Giao container', HaCont: 'Hạ container', TraRong: 'Trả rỗng', LayRong: 'Lấy rỗng' }
const MAU = { 'Đã gọi': 'run', 'Đã vào cổng': 'ok', 'Bỏ lượt': 'warn', 'Rời hàng': 'off' }

/**
 * HANG CHO AO NGOAI CONG: tai xe vao hang tren dien thoai (/taixe), dieu do goi xe theo diem uu tien co ghi ro ly do.
 * Moi xe co cam ket thoi gian cho; goi tre hon cam ket thi he thong tu ghi tien boi hoan.
 */
export default function HangChoAo() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/lich-hen/hang-cho')
  const { chay, dangChay } = useThaoTac()
  const [ch, setCh] = useState(null)

  useEffect(() => {
    const t = setInterval(() => taiLai({ im: true }), 8000)
    return () => clearInterval(t)
  }, [taiLai])

  useEffect(() => {
    if (d?.cauHinh && !ch) setCh(d.cauHinh)
  }, [d, ch])

  const lam = async (url, opt) => {
    if (await chay(() => goiApi(url, opt ?? { method: 'POST' }))) taiLai({ im: true })
  }

  const coDienTap = (d?.hang ?? []).some((x) => x.laDienTap) || (d?.daGoi ?? []).some((x) => x.laDienTap) || (d?.lichSu ?? []).some((x) => x.laDienTap)

  const COT = [
    { khoa: 'vt', ten: '#', rong: 44, hien: (x) => <b>{x.viTri}</b> },
    {
      khoa: 'xe',
      ten: 'Biển số',
      hien: (x) => (
        <>
          <span className="font-monospace fw-semibold">{x.bienSo}</span>
          {x.laDienTap && <span className="badge text-bg-secondary ms-1">diễn tập</span>}
        </>
      ),
    },
    { khoa: 'loai', ten: 'Loại lệnh', an: 'md', hien: (x) => <span className="small">{LOAI[x.loaiLenh] ?? x.loaiLenh}</span> },
    { khoa: 'cho', ten: 'Đã chờ', hien: (x) => <span className="small">{x.phutDaCho} phút</span> },
    { khoa: 'eta', ten: 'Dự kiến gọi', an: 'md', hien: (x) => <span className="small">~{x.phutUocTinh} phút nữa</span> },
    { khoa: 'ck', ten: 'Cam kết', an: 'lg', hien: (x) => <span className={'small ' + (x.phutDaCho > x.camKetPhut ? 'text-danger fw-semibold' : '')}>{x.camKetPhut} phút</span> },
    {
      khoa: 'diem',
      ten: 'Điểm ưu tiên',
      hien: (x) => (
        <span title={x.lyDo.join(' · ')}>
          <b>{x.diem}</b>
          <div className="small text-body-secondary">{x.lyDo.join(' · ')}</div>
        </span>
      ),
    },
    {
      khoa: 'tt',
      ten: '',
      canPhai: true,
      hien: (x) => (
        <div className="d-flex gap-1 justify-content-end">
          <button type="button" className="btn btn-sm btn-primary" disabled={dangChay} onClick={() => lam('/api/lich-hen/hang-cho/' + x.maHangCho + '/goi')}>
            Gọi
          </button>
          <button type="button" className="btn btn-sm btn-outline-secondary" disabled={dangChay} onClick={() => lam('/api/lich-hen/hang-cho/' + x.maHangCho + '/bo-luot')}>
            Bỏ lượt
          </button>
        </div>
      ),
    },
  ]

  const COT_LS = [
    { khoa: 'xe', ten: 'Biển số', hien: (x) => <span className="font-monospace">{x.bienSo}</span> },
    { khoa: 'vao', ten: 'Vào hàng', hien: (x) => <span className="small">{gio(x.vaoHangLuc)}</span> },
    { khoa: 'goi', ten: 'Gọi lúc', hien: (x) => <span className="small">{gio(x.goiLuc)}</span> },
    { khoa: 'cho', ten: 'Chờ', hien: (x) => <span className="small">{x.soPhutCho ?? '—'}′ / {x.camKetPhut}′</span> },
    { khoa: 'bh', ten: 'Bồi hoàn', an: 'md', hien: (x) => <span className="small">{x.tienBoiHoan > 0 ? tien(x.tienBoiHoan) : '—'}</span> },
    {
      khoa: 'tt',
      ten: 'Kết quả',
      hien: (x) => (
        <>
          <NhanTrangThai giaTri={x.trangThai} mau={MAU[x.trangThai]} />
          {x.chenNgang && <span className="badge text-bg-danger ms-1">chen ngang</span>}
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-people" mau="xanh" so={d?.chiSo.dangCho ?? 0} nhan="xe đang chờ ngoài cổng" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-megaphone" mau="la" so={d?.chiSo.daGoi ?? 0} nhan="xe đã gọi, đang tới cổng" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-hourglass-split" mau="cam" so={(d?.chiSo.phutChoTrungBinh ?? 0) + ' phút'} nhan="thời gian chờ trung bình hôm nay" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-cash-coin" mau="do" so={tien(d?.chiSo.tienBoiHoan)} nhan={'bồi hoàn do gọi trễ · ' + (d?.chiSo.chenNgang ?? 0) + ' xe chen ngang'} />
        </div>
      </div>

      <TheNoiDung
        tieuDe="Hàng chờ — xếp theo điểm ưu tiên"
        icon="bi-list-ol"
        phai={
          <>
            <button type="button" className="btn btn-sm btn-primary" disabled={dangChay || !(d?.hang?.length > 0)} onClick={() => lam('/api/lich-hen/hang-cho/goi-tiep?soXe=1')}>
              <i className="bi bi-megaphone me-1" />
              Gọi xe tiếp theo
            </button>
            <button type="button" className="btn btn-sm btn-outline-secondary" disabled={dangChay} onClick={() => lam('/api/lich-hen/hang-cho/dien-tap')}>
              Thêm 5 xe diễn tập
            </button>
            {coDienTap && (
              <button type="button" className="btn btn-sm btn-outline-danger" disabled={dangChay} onClick={() => lam('/api/lich-hen/hang-cho/dien-tap', { method: 'DELETE' })}>
                Xóa diễn tập
              </button>
            )}
          </>
        }
      >
        <BangDuLieu cot={COT} dong={d?.hang} khoaDong="maHangCho" dangTai={dangTai} rong="Chưa có xe trong hàng chờ. Tài xế vào hàng tại trang /taixe bằng số lệnh và mã PIN." />
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Tham số hàng chờ" icon="bi-sliders" khongDem>
            {ch && (
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  lam('/api/lich-hen/hang-cho/cau-hinh', { method: 'PUT', body: ch })
                }}
              >
                {[
                  ['soLanMo', 'Số làn cổng đang mở'],
                  ['phutMoiXe', 'Thời gian xử lý mỗi xe (phút)'],
                  ['phutAnToan', 'Phút dự phòng cộng vào cam kết'],
                  ['tienBoiHoan', 'Tiền bồi hoàn mỗi lượt gọi trễ (đ)'],
                ].map(([k, nhan]) => (
                  <div className="mb-2" key={k}>
                    <label className="form-label small mb-1" htmlFor={'hc-' + k}>
                      {nhan}
                    </label>
                    <input id={'hc-' + k} type="number" min="0" className="form-control form-control-sm" value={ch[k]} onChange={(e) => setCh({ ...ch, [k]: Number(e.target.value) })} />
                  </div>
                ))}
                <button className="btn btn-sm btn-success w-100" disabled={dangChay}>
                  Lưu tham số
                </button>
              </form>
            )}
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-8">
          <TheNoiDung tieuDe="Đã gọi và lịch sử gần đây" icon="bi-clock-history">
            <BangDuLieu cot={COT_LS} dong={[...(d?.daGoi ?? []), ...(d?.lichSu ?? [])]} khoaDong="maHangCho" dangTai={dangTai} rong="Chưa có lượt nào." />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
