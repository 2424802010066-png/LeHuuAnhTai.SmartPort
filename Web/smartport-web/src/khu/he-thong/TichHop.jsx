import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo } from '../../utils/dinhDang'
import './he-thong.css'

const RONG = { tenUngDung: '', maKH: '', quyen: 'Doc', ghiChu: '' }

/**
 * Tich hop · khoa API cho phan mem doi tac (thay Admin/TichHop): cap khoa, thu hoi / bat lai, xoa.
 * Khoa day du chi hien MOT lan ngay sau khi cap (giu trong state, khong luu lai o dau ca).
 */
export default function TichHop() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/he-thong/tich-hop')
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const bao = useThongBao()
  const [khoaMoi, setKhoaMoi] = useState(null)
  const [daChep, setDaChep] = useState(false)
  const oKhoa = useRef(null)

  async function capKhoa(e) {
    e.preventDefault()
    const kq = await chay(() =>
      goiApi('/api/he-thong/tich-hop', {
        method: 'POST',
        body: { tenUngDung: gt.tenUngDung.trim(), maKH: soHoacNull(gt.maKH), quyen: gt.quyen, ghiChu: chuoiHoacNull(gt.ghiChu.trim()) },
      }),
    )
    if (kq) {
      setKhoaMoi(kq.duLieu)
      setDaChep(false)
      datLai(RONG)
      taiLai({ im: true })
    }
  }

  async function chepKhoa() {
    if (!khoaMoi) return
    try {
      await navigator.clipboard.writeText(khoaMoi.khoa)
      setDaChep(true)
      return
    } catch {
      /* trinh duyet chan clipboard (http khong bao mat...) -> boi den de nguoi dung tu chep */
    }
    const vung = document.createRange()
    vung.selectNodeContents(oKhoa.current)
    const chon = window.getSelection()
    chon.removeAllRanges()
    chon.addRange(vung)
    bao.canhBao('Trình duyệt chặn sao chép tự động — khóa đã được bôi đen, nhấn Ctrl+C để chép.')
  }

  async function batTat(k) {
    if (await chay(() => goiApi(`/api/he-thong/tich-hop/${k.maKey}/bat`, { method: 'POST' }))) taiLai({ im: true })
  }

  async function xoa(k) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa khóa API?',
      noiDung: `Xóa hẳn khóa của ${k.tenUngDung}. Phần mềm đang dùng khóa này sẽ bị từ chối ngay.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/he-thong/tich-hop/' + k.maKey, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const COT = [
    {
      khoa: 'ungDung',
      ten: 'Ứng dụng',
      hien: (k) => (
        <>
          <div className="fw-semibold">{k.tenUngDung}</div>
          {k.ghiChu && <div className="small text-body-secondary">{k.ghiChu}</div>}
          {!k.kichHoat && (
            <NhanTrangThai mau="off" icon="bi-slash-circle">
              Đã thu hồi
            </NhanTrangThai>
          )}
        </>
      ),
    },
    { khoa: 'khoa', ten: 'Khóa', an: 'md', hien: (k) => <code className="small">{k.khoaRutGon}</code> },
    {
      khoa: 'phamVi',
      ten: 'Phạm vi',
      hien: (k) => (
        <>
          <NhanTrangThai mau={k.quyen === 'Ghi' ? 'warn' : 'info'}>{k.quyen === 'Ghi' ? 'Đọc + Ghi' : 'Chỉ đọc'}</NhanTrangThai>
          <div className="small text-body-secondary mt-1">{k.tenKhachHang || 'Toàn hệ thống'}</div>
        </>
      ),
    },
    { khoa: 'soLanGoi', ten: 'Đã gọi', canPhai: true, hien: (k) => <span className="fw-semibold">{dinhDangSo(k.soLanGoi)}</span> },
    {
      khoa: 'lanGoiCuoi',
      ten: 'Gọi lần cuối',
      an: 'lg',
      hien: (k) => <span className="small text-body-secondary">{k.lanGoiCuoi ? dinhDangNgayGio(k.lanGoiCuoi) : 'chưa dùng lần nào'}</span>,
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      lop: 'text-nowrap',
      hien: (k) => (
        <>
          <button
            type="button"
            className={'btn btn-sm me-1 ' + (k.kichHoat ? 'btn-outline-danger' : 'btn-outline-success')}
            disabled={dangChay}
            onClick={() => batTat(k)}
          >
            {k.kichHoat ? 'Thu hồi' : 'Bật lại'}
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-secondary"
            title="Xóa hẳn khóa"
            aria-label={'Xóa khóa của ' + k.tenUngDung}
            disabled={dangChay}
            onClick={() => xoa(k)}
          >
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3 align-items-stretch">
        <div className="col-sm-4 col-xl-3">
          <TheChiSo icon="bi-key-fill" mau="la" so={dinhDangSo(d?.dangBat)} nhan="Khóa đang bật" />
        </div>
        <div className="col-sm-4 col-xl-3">
          <TheChiSo icon="bi-slash-circle" mau="cam" so={dinhDangSo(d?.daThuHoi)} nhan="Đã thu hồi" />
        </div>
        <div className="col-sm-4 col-xl-3">
          <TheChiSo icon="bi-arrow-left-right" mau="xanh" so={dinhDangSo(d?.tongLuotGoi)} nhan="Lượt gọi API của đối tác" />
        </div>
        <div className="col-xl-3 d-flex align-items-center justify-content-xl-end">
          <Link className="btn btn-outline-primary" to="/admin/tichhop/tailieu">
            <i className="bi bi-book me-1" />
            Tài liệu kỹ thuật
          </Link>
        </div>
      </div>

      {khoaMoi && (
        <div className="alert alert-warning" role="status">
          <div className="d-flex align-items-start gap-2">
            <div className="flex-grow-1 min-w-0">
              <div className="fw-bold mb-2">
                <i className="bi bi-key-fill me-1" />
                Khóa của {khoaMoi.tenUngDung} — chỉ hiện lần này
              </div>
              <div className="d-flex gap-2 align-items-center flex-wrap">
                <code ref={oKhoa} className="ht-khoa-moi">
                  {khoaMoi.khoa}
                </code>
                <button type="button" className={'btn btn-sm ' + (daChep ? 'btn-success' : 'btn-dark')} onClick={chepKhoa}>
                  <i className={'bi me-1 ' + (daChep ? 'bi-check2' : 'bi-clipboard')} />
                  {daChep ? 'Đã chép' : 'Sao chép'}
                </button>
              </div>
              <div className="small mt-2">Gửi cho kỹ thuật bên khách qua kênh riêng. Lỡ lộ khóa: bấm <b>Thu hồi</b> ở bảng dưới.</div>
            </div>
            <button type="button" className="btn-close" aria-label="Đã lưu khóa, ẩn đi" title="Đã lưu khóa, ẩn đi" onClick={() => setKhoaMoi(null)} />
          </div>
        </div>
      )}

      <div className="row g-3">
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Cấp khóa mới" icon="bi-plus-square">
            <form onSubmit={capKhoa}>
              <Truong nhan="Ứng dụng sẽ dùng khóa" batBuoc>
                <input className="form-control" value={gt.tenUngDung} onChange={doi('tenUngDung')} maxLength={100} placeholder="Kế toán MISA — Công ty Trường Thành" />
              </Truong>
              <Truong nhan="Giới hạn theo khách hàng" goiY="Chọn khách hàng thì khóa chỉ thấy dữ liệu của khách đó.">
                <select className="form-select" value={gt.maKH} onChange={doi('maKH')}>
                  <option value="">— Không giới hạn (khóa nội bộ) —</option>
                  {(d?.khachHangs || []).map((k) => (
                    <option key={k.giaTri} value={k.giaTri}>
                      {k.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Quyền" goiY="Chỉ cấp quyền ghi khi bên kia thực sự cần tự đặt lệnh.">
                <select className="form-select" value={gt.quyen} onChange={doi('quyen')}>
                  <option value="Doc">Chỉ đọc (an toàn nhất)</option>
                  <option value="Ghi">Đọc + đặt lệnh giao nhận</option>
                </select>
              </Truong>
              <Truong nhan="Ghi chú">
                <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={300} placeholder="Anh Hùng IT — 0912xxxxxx" />
              </Truong>
              <button type="submit" className="btn btn-primary w-100" disabled={dangChay}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-key me-1" />}
                Cấp khóa
              </button>
            </form>
          </TheNoiDung>
        </div>

        <div className="col-lg-8">
          <TheNoiDung tieuDe={`Khóa đã cấp (${dinhDangSo(d?.items?.length ?? 0)})`} icon="bi-list-ul" khongDem>
            {d && !d.items.length ? (
              <div className="text-center text-body-secondary py-5">
                <i className="bi bi-plug fs-1 d-block opacity-50" />
                <div className="mt-2">Chưa cấp khóa nào.</div>
                <div className="small">API chỉ cần khi khách muốn nối phần mềm của họ vào.</div>
              </div>
            ) : (
              <BangDuLieu cot={COT} dong={d?.items} khoaDong="maKey" dangTai={dangTai} lopDong={(k) => (k.kichHoat ? '' : 'ht-khoa-thu-hoi')} />
            )}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
