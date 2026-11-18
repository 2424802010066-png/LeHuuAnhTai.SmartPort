import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung, Truong, useThongBao } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import { LOAI_LENH_COPARN } from './chung'
import './edi-depot.css'

// Ban tin mau de nguoi dung thu ngay khi chua co tep that cua hang tau (giu nguyen ban MVC)
const BAN_TIN_MAU = [
  "UNB+UNOA:2+MAEU:ZZZ+SMARTPORT:ZZZ+260904:0800+TEST001'",
  "UNH+1+COPARN:D:95B:UN'",
  "BGM+126+BK2609001+9'",
  "DTM+137:202609040800:203'",
  "NAD+MS+MAEU::ZZZ'",
  "NAD+MR+SMARTPORT::ZZZ'",
  "EQD+CN+MAEU1234567+22G1:102:5++2+4'",
  "RFF+BN:BK2609001'",
  "DTM+2:202609051000:203'",
  "EQD+CN+MAEU7654321+45G1:102:5++2+4'",
  "RFF+BN:BK2609002'",
  "DTM+2:202609051400:203'",
  "CNT+16:2'",
  "UNT+13+1'",
  "UNZ+1+TEST001'",
].join('\n')

const MAU_KET_QUA = { tao: 'text-success', boQua: 'text-warning-emphasis', loi: 'text-danger' }
const ICON_KET_QUA = { tao: 'bi-check-circle', boQua: 'bi-dash-circle', loi: 'bi-x-circle' }
const LOP_DONG = { tao: '', boQua: 'table-warning', loi: 'table-danger' }

/**
 * Nhan COPARN tu hang tau (gop Admin/Edi/NhanCoparn + XemTruocCoparn cua ban MVC vao mot man hinh):
 * buoc 1 dan noi dung / tai tep -> buoc 2 xem truoc tung dong -> buoc 3 xac nhan tao lenh giao nhan.
 */
export default function NhanCoparn() {
  const { duLieu: luaChon, loi: loiLuaChon } = useApi('/api/edi/lua-chon')
  const [form, setForm] = useState({ noiDung: '', maHangTau: '', loaiLenh: 'LayRong' })
  const [xem, setXem] = useState(null) // ket qua doc thu — null = dang o buoc nhap
  const oTep = useRef(null)
  const { chay, dangChay } = useThaoTac()
  const bao = useThongBao()
  const dieuHuong = useNavigate()

  const hangTaus = luaChon?.hangTaus || []

  async function docThu(e) {
    e.preventDefault()
    const tep = oTep.current?.files?.[0]
    if (!tep && !form.noiDung.trim()) {
      bao.loi('Chưa có nội dung — tải tệp .edi hoặc dán nội dung bản tin vào ô bên dưới.')
      return
    }
    const fd = new FormData()
    fd.append('noiDung', form.noiDung || '')
    if (tep) fd.append('tep', tep)
    if (form.maHangTau) fd.append('maHangTau', form.maHangTau)
    fd.append('loaiLenh', form.loaiLenh)
    const kq = await chay(() => goiApi('/api/edi/coparn/xem-truoc', { method: 'POST', body: fd }), { imLang: true })
    if (kq) setXem(kq)
  }

  async function taoLenh() {
    const kq = await chay(() =>
      goiApi('/api/edi/coparn', {
        method: 'POST',
        body: { noiDung: xem.noiDung, maHangTau: xem.maHangTau, loaiLenh: xem.loaiLenh },
      }),
    )
    if (kq?.id) dieuHuong('/admin/edi/xem/' + kq.id)
  }

  function dungMau() {
    setForm({ ...form, noiDung: BAN_TIN_MAU })
    if (oTep.current) oTep.current.value = ''
  }

  const COT = [
    { khoa: 'stt', ten: '#', rong: 40, hien: (d, i) => <span className="text-body-secondary small">{i + 1}</span> },
    { khoa: 'soCont', ten: 'Số container', hien: (d) => <span className="fw-semibold ed-mono">{d.soCont || '—'}</span> },
    {
      khoa: 'loai',
      ten: 'Mã ISO → loại',
      an: 'md',
      hien: (d) => (
        <span className="small">
          <code>{d.maISO || '—'}</code> → {d.loaiContNoiBo || '—'}
        </span>
      ),
    },
    {
      khoa: 'tinhTrang',
      ten: 'Tình trạng',
      hien: (d) => <span className={'badge ' + (d.rong ? 'text-bg-secondary' : 'text-bg-primary')}>{d.rong ? 'Rỗng' : 'Có hàng'}</span>,
    },
    { khoa: 'booking', ten: 'Booking', an: 'lg', hien: (d) => <span className="small">{d.soBooking || '—'}</span> },
    { khoa: 'hen', ten: 'Hẹn lúc', an: 'lg', hien: (d) => <span className="small">{dinhDangNgayGio(d.henLuc)}</span> },
    {
      khoa: 'ketQua',
      ten: 'Kết quả',
      hien: (d) => (
        <span className={'small ' + (MAU_KET_QUA[d.ketQua] || '')}>
          <i className={'bi me-1 ' + (ICON_KET_QUA[d.ketQua] || 'bi-dash')} />
          {d.lyDo}
        </span>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loiLuaChon} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-primary" to="/admin/edi">
          <i className="bi bi-arrow-left me-1" />
          EDI
        </Link>
        <div className="ed-buoc ms-2">
          <span className={xem ? '' : 'dang'}>
            <b>1</b> Nhập bản tin
          </span>
          <span className={xem ? 'dang' : ''}>
            <b>2</b> Xem trước
          </span>
          <span>
            <b>3</b> Tạo lệnh giao nhận
          </span>
        </div>
      </div>

      {xem ? (
        /* ================= BUOC 2: XEM TRUOC ================= */
        <>
          <div className="row g-3 mb-3">
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-envelope-paper" mau="tim" so={xem.loaiBanTin || '—'} nhan={xem.soChungTu || 'Bản tin COPARN'} nho />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-check2-square" mau="la" so={dinhDangSo(xem.soHopLe)} nhan="Container sẽ tạo lệnh" />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-dash-circle" mau="cam" so={dinhDangSo(xem.soBoQua)} nhan="Bỏ qua — đã có lệnh mở" />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-exclamation-triangle" mau={xem.soLoi > 0 ? 'do' : 'la'} so={dinhDangSo(xem.soLoi)} nhan="Dòng lỗi — không tạo được" />
            </div>
          </div>

          <TheNoiDung
            tieuDe="Từng dòng trong bản tin"
            icon="bi-list-ol"
            khongDem
            phai={
              <span className="small text-body-secondary">
                {xem.benGui || '—'} <i className="bi bi-arrow-right mx-1" /> {xem.benNhan || '—'} · tham chiếu <code>{xem.soThamChieu || '—'}</code>
              </span>
            }
          >
            <BangDuLieu
              cot={COT}
              dong={xem.dong}
              khoaDong={(d, i) => i}
              lopDong={(d) => LOP_DONG[d.ketQua]}
              rong="Bản tin không có dòng container nào."
            />
          </TheNoiDung>

          <TheNoiDung>
            <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
              <div className="small text-body-secondary">
                Sẽ tạo <b>{dinhDangSo(xem.soHopLe)} lệnh {xem.tenLoaiLenh || xem.loaiLenh}</b>. Container chưa có trong hệ thống được khai báo tự
                động ở trạng thái “Chờ về”.
              </div>
              <div className="d-flex gap-2">
                <button type="button" className="btn btn-light" disabled={dangChay} onClick={() => setXem(null)}>
                  <i className="bi bi-arrow-left me-1" />
                  Nhập lại
                </button>
                <button type="button" className="btn btn-primary" disabled={dangChay || !xem.soHopLe} onClick={taoLenh}>
                  <i className="bi bi-check2-square me-1" />
                  Xác nhận tạo {dinhDangSo(xem.soHopLe)} lệnh
                </button>
              </div>
            </div>
          </TheNoiDung>
        </>
      ) : (
        /* ================= BUOC 1: NHAP BAN TIN ================= */
        <div className="row g-3">
          <div className="col-lg-7">
            <TheNoiDung tieuDe="Nội dung bản tin" icon="bi-file-earmark-arrow-up">
              <form onSubmit={docThu}>
                <Truong nhan="Cách 1 — tải tệp .edi hãng tàu gửi">
                  <input className="form-control" type="file" accept=".edi,.txt,.edifact" ref={oTep} />
                </Truong>
                <Truong nhan="Cách 2 — dán thẳng nội dung">
                  <textarea
                    className="form-control ed-o-edi"
                    rows={12}
                    value={form.noiDung}
                    onChange={(e) => setForm({ ...form, noiDung: e.target.value })}
                    placeholder={"UNB+UNOA:2+MAEU:ZZZ+SMARTPORT:ZZZ+260904:1200+REF001'\nUNH+1+COPARN:D:95B:UN'\n..."}
                  />
                </Truong>
                <div className="row g-2">
                  <Truong nhan="Hãng tàu gửi" lop="col-md-6 mb-3">
                    <select className="form-select" value={form.maHangTau} onChange={(e) => setForm({ ...form, maHangTau: e.target.value })}>
                      <option value="">— Chưa xác định —</option>
                      {hangTaus.map((h) => (
                        <option key={h.giaTri} value={h.giaTri}>
                          {h.nhan}
                        </option>
                      ))}
                    </select>
                  </Truong>
                  <Truong nhan="Loại lệnh sẽ tạo" lop="col-md-6 mb-3">
                    <select className="form-select" value={form.loaiLenh} onChange={(e) => setForm({ ...form, loaiLenh: e.target.value })}>
                      {LOAI_LENH_COPARN.map((l) => (
                        <option key={l.giaTri} value={l.giaTri}>
                          {l.nhan}
                        </option>
                      ))}
                    </select>
                  </Truong>
                </div>
                <button type="submit" className="btn btn-primary w-100" disabled={dangChay}>
                  {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-search me-1" />}
                  Đọc &amp; xem trước
                </button>
                <div className="small text-body-secondary mt-2">
                  Bước này hệ thống <b>chỉ đọc và hiển thị</b> — chưa tạo lệnh nào.
                </div>
              </form>
            </TheNoiDung>
          </div>

          <div className="col-lg-5">
            <TheNoiDung tieuDe="COPARN là gì" icon="bi-question-circle">
              <p className="small text-body-secondary mb-2">
                Là bản tin hãng tàu <b>báo trước</b> cho depot: sắp có xe đến lấy/trả những container này. Có nó thì lúc xe tới cổng, hệ thống đã sẵn
                lệnh để đối chiếu.
              </p>
              <div className="small fw-semibold mb-1">Hệ thống lấy gì từ bản tin</div>
              <ul className="small text-body-secondary" style={{ lineHeight: 1.9, paddingLeft: 18 }}>
                <li>
                  <code>EQD</code> → số container + mã kích thước ISO + rỗng/có hàng
                </li>
                <li>
                  <code>RFF+BN</code> → số booking
                </li>
                <li>
                  <code>DTM</code> → thời điểm hẹn
                </li>
                <li>
                  <code>TDT</code> → biển số xe (nếu hãng có ghi)
                </li>
              </ul>
              <div className="alert alert-warning small mb-0 py-2">
                Container <b>chưa có trong hệ thống sẽ được khai báo tự động</b> ở trạng thái “Chờ về” — nếu không, lúc xe tới cổng sẽ không khớp
                được lệnh.
              </div>
            </TheNoiDung>

            <TheNoiDung tieuDe="Chưa có bản tin thật để thử?" icon="bi-lightbulb">
              <div className="small text-body-secondary mb-2">Dùng bản tin mẫu để xem hệ thống đọc ra sao:</div>
              <pre className="ed-edi-goc ed-edi-mau">{BAN_TIN_MAU}</pre>
              <button type="button" className="btn btn-sm btn-outline-primary w-100 mt-2" onClick={dungMau}>
                <i className="bi bi-clipboard-check me-1" />
                Điền mẫu vào ô nội dung
              </button>
            </TheNoiDung>
          </div>
        </div>
      )}
    </>
  )
}
