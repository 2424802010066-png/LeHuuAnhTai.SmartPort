import { useState } from 'react'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import './cang-so.css'

const MAU = { 'Khớp': 'ok', 'Không khớp': 'do', 'Nghi vấn': 'warn', 'Không đọc được': 'off' }

function docAnh(tep) {
  return new Promise((ok, loi) => {
    const r = new FileReader()
    r.onload = () => ok(r.result)
    r.onerror = loi
    r.readAsDataURL(tep)
  })
}

/**
 * CHONG TRAO CONTAINER: cong chup mat container -> dich vu AI thi giac doc so container (CRNN + rang buoc ISO 6346)
 * -> so voi lenh giao nhan. "Không khớp" -> dich vu An ninh tu tao su co muc Nguy hiem, giu xe tai cong.
 */
export default function DoiChieuContainer() {
  const [ketQua, setKetQua] = useState('')
  const { duLieu: ds, loi, dangTai, taiLai } = useApi('/api/doi-chieu-container' + taoQuery({ ketQua }))
  const { duLieu: tk, taiLai: taiTk } = useApi('/api/doi-chieu-container/thong-ke')
  const { chay, dangChay } = useThaoTac()
  const [f, setF] = useState({ maXV: '', soContMongDoi: '', soContDocDuoc: '', anh: null })
  const [kq, setKq] = useState(null)

  useNgheSuKien('cong.container-da-doi-chieu', () => {
    taiLai({ im: true })
    taiTk({ im: true })
  })

  async function doiChieu(e) {
    e.preventDefault()
    const r = await chay(
      () =>
        goiApi('/api/cong/doi-chieu-container', {
          method: 'POST',
          body: {
            maXV: f.maXV ? Number(f.maXV) : null,
            soContMongDoi: f.soContMongDoi || null,
            soContDocDuoc: f.soContDocDuoc || null,
            anh: f.anh,
          },
        }),
      { imLang: true },
    )
    if (r) setKq(r)
  }

  const COT = [
    { khoa: 'tg', ten: 'Thời gian', hien: (x) => <span className="small">{gioNgayNgan(x.thoiGian)}</span> },
    { khoa: 'xe', ten: 'Xe · hướng', an: 'md', hien: (x) => <span className="small">{x.bienSo || '—'} · {x.huongDi || '—'}</span> },
    { khoa: 'lenh', ten: 'Container theo lệnh', hien: (x) => <span className="cs-ma">{x.soContLenh}</span> },
    {
      khoa: 'doc',
      ten: 'Camera đọc được',
      hien: (x) => (
        <span className="cs-ma">
          {x.soContDocDuoc || '—'}
          {x.soContDocDuoc && (
            <i
              className={'bi ms-1 ' + (x.hopLeIso ? 'bi-check-circle text-success' : 'bi-question-circle text-warning')}
              title={x.hopLeIso ? 'Đúng số kiểm tra ISO 6346' : 'Sai số kiểm tra ISO 6346'}
            />
          )}
        </span>
      ),
    },
    { khoa: 'kq', ten: 'Kết quả', hien: (x) => <NhanTrangThai giaTri={x.ketQua} mau={MAU[x.ketQua]} /> },
    {
      khoa: 'anh',
      ten: 'Ảnh',
      an: 'lg',
      hien: (x) =>
        x.anh ? (
          <a href={x.anh} target="_blank" rel="noreferrer noopener">
            <img src={x.anh} alt={'Ảnh container ' + x.soContLenh} className="cs-anh-nho" loading="lazy" />
          </a>
        ) : (
          '—'
        ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-check2-circle" mau="la" so={dinhDangSo(tk?.khop ?? 0)} nhan="khớp lệnh (30 ngày)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-shield-exclamation" mau="do" so={dinhDangSo(tk?.khongKhop ?? 0)} nhan="NGHI TRÁO — đã báo an ninh" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-question-diamond" mau="cam" so={dinhDangSo(tk?.nghiVan ?? 0)} nhan="nghi vấn (lệch 1 ký tự)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-eye-slash" mau="xanh" so={dinhDangSo(tk?.khongDocDuoc ?? 0)} nhan="không đọc được" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-12 col-xl-4">
          <TheNoiDung tieuDe="Đối chiếu tại cổng" icon="bi-upc-scan" khongDem>
            <form onSubmit={doiChieu}>
              <Truong nhan="Mã lượt xe" goiY="Lấy container từ lệnh gắn với lượt xe">
                <input className="form-control" type="number" min={1} value={f.maXV} onChange={(e) => setF({ ...f, maXV: e.target.value })} />
              </Truong>
              <Truong nhan="hoặc số container mong đợi">
                <input className="form-control text-uppercase" maxLength={11} value={f.soContMongDoi} onChange={(e) => setF({ ...f, soContMongDoi: e.target.value })} />
              </Truong>
              <Truong nhan="Ảnh mặt container" goiY="Dịch vụ AI thị giác (máy chủ GPU) đọc mã">
                <input
                  className="form-control"
                  type="file"
                  accept="image/jpeg,image/png"
                  onChange={async (e) => setF({ ...f, anh: e.target.files?.[0] ? await docAnh(e.target.files[0]) : null })}
                />
              </Truong>
              <Truong nhan="hoặc số container camera đã đọc" goiY="Khi AI chạy ngay trong trình duyệt">
                <input className="form-control text-uppercase" maxLength={11} value={f.soContDocDuoc} onChange={(e) => setF({ ...f, soContDocDuoc: e.target.value })} />
              </Truong>
              <button type="submit" className="btn btn-primary w-100" disabled={dangChay || (!f.maXV && !f.soContMongDoi) || (!f.anh && !f.soContDocDuoc)}>
                <i className="bi bi-shield-check me-1" />
                Đối chiếu
              </button>
            </form>
            {kq && (
              <div className={'alert mt-3 mb-0 ' + (kq.ketQua === 'Khớp' ? 'alert-success' : kq.ketQua === 'Không khớp' ? 'alert-danger' : 'alert-warning')}>
                <div className="fw-semibold mb-1">
                  {kq.ketQua}: lệnh <span className="cs-ma">{kq.soContLenh}</span> ↔ đọc <span className="cs-ma">{kq.soContDocDuoc || '?'}</span>
                </div>
                <div className="small">{kq.thongBao}</div>
                <div className="small text-body-secondary">Nguồn đọc: {kq.nguonDoc}</div>
              </div>
            )}
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-8">
          <TheNoiDung
            tieuDe="Nhật ký đối chiếu container"
            icon="bi-journal-check"
            phai={
              <select className="form-select form-select-sm" style={{ maxWidth: 170 }} value={ketQua} onChange={(e) => setKetQua(e.target.value)} aria-label="Lọc kết quả">
                <option value="">— Mọi kết quả —</option>
                {Object.keys(MAU).map((k) => (
                  <option key={k}>{k}</option>
                ))}
              </select>
            }
          >
            <BangDuLieu cot={COT} dong={ds} khoaDong="maDoiChieu" dangTai={dangTai} rong="Chưa có lần đối chiếu nào." />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
