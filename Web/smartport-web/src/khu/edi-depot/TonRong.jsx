import { useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, DangTai, TheChiSo, TheNoiDung, Truong } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangSo } from '../../utils/dinhDang'
import { tachSoCont } from './chung'
import './edi-depot.css'

/**
 * Ton rong theo hang tau (thay Admin/Depot/TonRong cua ban MVC): con so hang tau hoi depot moi ngay,
 * kem o gan hang tau cho vo dang nam bai chua ro chu.
 */
export default function TonRong() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/depot/ton-rong')
  const [gan, setGan] = useState({ maHangTau: '', soConts: '' })
  const [khongThay, setKhongThay] = useState([])
  const { chay, dangChay } = useThaoTac()

  // Xe qua cong / ha cont vao bai lam doi ton bai -> lam moi ngam
  useNgheSuKien('bai.*', () => taiLai({ im: true }))

  if (!d) return dangTai ? <DangTai /> : <BaoLoi loi={loi} thuLai={taiLai} />

  const hangTaus = d.hangTaus || []
  const maHangTau = gan.maHangTau || hangTaus[0]?.giaTri || ''
  const soCont = tachSoCont(gan.soConts)

  async function luuGan(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi('/api/depot/gan-hang-tau', { method: 'POST', body: { maHangTau: Number(maHangTau), soConts: gan.soConts } }))
    if (!kq) return
    setKhongThay(kq.duLieu?.khongThay || [])
    setGan({ ...gan, soConts: '' })
    taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'hang',
      ten: 'Hãng tàu',
      hien: (n) =>
        n.maHangTau == null ? (
          <span className="fw-semibold text-warning-emphasis">
            <i className="bi bi-exclamation-triangle me-1" />
            {n.tenHang}
          </span>
        ) : (
          <span className="fw-semibold">
            <i className="bi bi-globe2 me-1 text-primary" />
            {n.tenHang}
          </span>
        ),
    },
    { khoa: 'tong', ten: 'Tổng', canPhai: true, rong: 80, hien: (n) => <span className="badge text-bg-dark fs-6">{dinhDangSo(n.tong)}</span> },
    {
      khoa: 'theoLoai',
      ten: 'Phân theo loại',
      an: 'md',
      hien: (n) => (
        <div className="d-flex flex-wrap gap-1">
          {(n.theoLoai || []).map((l) => (
            <span className="badge text-bg-light border" key={l.loai}>
              {l.loai}: <b>{dinhDangSo(l.soLuong)}</b>
            </span>
          ))}
        </div>
      ),
    },
    { khoa: 'tot', ten: 'Vỏ tốt', canPhai: true, rong: 90, hien: (n) => <span className="text-success fw-bold">{dinhDangSo(n.tot)}</span> },
    {
      khoa: 'hong',
      ten: 'Cần sửa',
      canPhai: true,
      rong: 90,
      hien: (n) => <span className={'fw-bold ' + (n.hong > 0 ? 'text-danger' : 'text-body-secondary')}>{dinhDangSo(n.hong)}</span>,
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-box-seam" mau="xanh-ngoc" so={dinhDangSo(d.tongCont)} nhan="Container đang trong bãi" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-globe2" mau="xanh" so={dinhDangSo(d.soHang)} nhan="Hãng tàu đang gửi vỏ" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-question-circle" mau={d.chuaGan > 0 ? 'cam' : 'la'} so={dinhDangSo(d.chuaGan)} nhan="Chưa gán hãng tàu — cần xử lý" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-tools" mau={d.choDuyetEor > 0 ? 'do' : 'la'} so={dinhDangSo(d.choDuyetEor)} nhan="Phiếu EOR chờ hãng tàu duyệt" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-8">
          <TheNoiDung
            tieuDe="Tồn bãi phân theo hãng tàu"
            icon="bi-list-columns"
            khongDem
            phai={
              <Link className="btn btn-sm btn-outline-primary" to="/admin/depot/eor">
                <i className="bi bi-tools me-1" />
                Phiếu EOR
              </Link>
            }
          >
            <BangDuLieu cot={COT} dong={d.nhom} khoaDong={(n) => n.maHangTau ?? 'chua-gan'} dangTai={dangTai} rong="Không có container nào trong bãi." />
          </TheNoiDung>
        </div>

        <div className="col-lg-4">
          <TheNoiDung tieuDe="Gán hãng tàu cho container" icon="bi-link-45deg">
            <div className="small text-body-secondary mb-3">
              Dán danh sách số container (cách nhau bởi dấu phẩy hoặc xuống dòng) rồi chọn hãng tàu chủ vỏ.
            </div>
            <form onSubmit={luuGan}>
              <Truong nhan="Hãng tàu" batBuoc>
                <select className="form-select" value={maHangTau} onChange={(e) => setGan({ ...gan, maHangTau: e.target.value })}>
                  {!hangTaus.length && <option value="">— Chưa có hãng tàu —</option>}
                  {hangTaus.map((h) => (
                    <option key={h.giaTri} value={h.giaTri}>
                      {h.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Số container" goiY={soCont.length ? `Đã nhận ${dinhDangSo(soCont.length)} số container` : undefined}>
                <textarea
                  className="form-control ed-mono"
                  rows={5}
                  value={gan.soConts}
                  onChange={(e) => setGan({ ...gan, soConts: e.target.value })}
                  placeholder={'MSCU2299876\nTCLU1234567\n...'}
                />
              </Truong>
              <button type="submit" className="btn btn-primary w-100" disabled={dangChay || !maHangTau || !soCont.length}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
                Gán hãng tàu
              </button>
            </form>

            {khongThay.length > 0 && (
              <div className="alert alert-warning small mt-3 mb-0 py-2">
                <b>{dinhDangSo(khongThay.length)} số không có trong bãi:</b>
                <div className="ed-mono mt-1">{khongThay.join(', ')}</div>
              </div>
            )}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
