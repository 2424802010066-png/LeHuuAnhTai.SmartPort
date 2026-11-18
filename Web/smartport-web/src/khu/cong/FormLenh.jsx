import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { goiApi, taoBoKhoaGuiLap } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung, Truong } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { ngayGioInput } from '../../utils/dinhDang'
import './cong.css'

const RONG = {
  loaiLenh: 'GiaoCont',
  maKH: '',
  bienSoXe: '',
  soCont: '',
  tenTaiXe: '',
  sdtTaiXe: '',
  henTuLuc: '',
  henDenLuc: '',
  ghiChu: '',
}

const hai = (n) => String(n).padStart(2, '0')
/** Date -> "yyyy-MM-ddTHH:00" cho o datetime-local (bam slot la dien thang gio tron) */
const gioTron = (d) => `${d.getFullYear()}-${hai(d.getMonth() + 1)}-${hai(d.getDate())}T${hai(d.getHours())}:00`

/**
 * TAO / SUA LENH GIAO NHAN (thay Admin/GatePass/Form + Save) kem LUOI SLOT CONG 24 gio toi:
 * mo hinh TAS (Truck Appointment System) dieu tiet xe theo gio nhu HHIT Lach Huyen —
 * khung gio da du suc chua thi khong dat them duoc.
 */
export default function FormLenh() {
  const { id } = useParams()
  const [thamSo] = useSearchParams()
  const dieuHuong = useNavigate()
  const { chay, dangChay } = useThaoTac()
  const { gt, doi, datGt, datLai } = useBieuMau(RONG)
  const [slotChon, setSlotChon] = useState('')
  const [khoaGui] = useState(taoBoKhoaGuiLap) // Idempotency-Key cho POST tao lenh: bam lai sau loi mang khong tao 2 lenh

  const { duLieu: form, loi: loiForm, taiLai: taiForm } = useApi('/api/lenh-giao-nhan/form-du-lieu')
  const { duLieu: lenh, loi: loiLenh, dangTai: dangTaiLenh } = useApi(id ? '/api/lenh-giao-nhan/' + id : null)

  // Nap du lieu lenh cu (sua) hoac gia tri mac dinh (tao moi: hen tu gio ke tiep, dai 2 tieng)
  useEffect(() => {
    if (id) {
      if (!lenh) return
      datLai({
        loaiLenh: lenh.loaiLenh || 'GiaoCont',
        maKH: lenh.maKH ?? '',
        bienSoXe: lenh.bienSoXe || '',
        soCont: lenh.soCont || '',
        tenTaiXe: lenh.tenTaiXe || '',
        sdtTaiXe: lenh.sdtTaiXe || '',
        henTuLuc: ngayGioInput(lenh.henTuLuc),
        henDenLuc: ngayGioInput(lenh.henDenLuc),
        ghiChu: lenh.ghiChu || '',
      })
      return
    }
    const tu = new Date()
    tu.setMinutes(0, 0, 0)
    tu.setHours(tu.getHours() + 1)
    const den = new Date(tu.getTime() + 2 * 3600 * 1000)
    datLai({ ...RONG, bienSoXe: (thamSo.get('bienSo') || '').toUpperCase(), soCont: (thamSo.get('soCont') || '').toUpperCase(), henTuLuc: gioTron(tu), henDenLuc: gioTron(den) })
  }, [id, lenh, datLai, thamSo])

  function chonSlot(s) {
    const luc = new Date(s.luc)
    const tu = gioTron(luc)
    const den = gioTron(new Date(luc.getTime() + 2 * 3600 * 1000))
    setSlotChon(s.luc)
    datGt({ henTuLuc: tu, henDenLuc: den })
  }

  async function luu(e) {
    e.preventDefault()
    const than = { ...gt, maKH: gt.maKH === '' ? null : Number(gt.maKH) }
    const kq = await chay(() =>
      id ? goiApi('/api/lenh-giao-nhan/' + id, { method: 'PUT', body: than }) : goiApi('/api/lenh-giao-nhan', { method: 'POST', body: than, chongGuiLap: khoaGui }),
    )
    if (!kq) return
    // Tao moi -> mo thang ban in de giao cho tai xe (co so lenh + PIN + QR)
    dieuHuong(id ? '/admin/gatepass' : '/admin/gatepass/in/' + kq.id)
  }

  if (id && dangTaiLenh && !lenh) return <DangTai chu="Đang mở lệnh giao nhận…" />

  const sucChua = form?.sucChuaSlot ?? 6

  return (
    <>
      <BaoLoi loi={loiLenh || loiForm} thuLai={taiForm} />

      <div className="d-flex align-items-center gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/gatepass">
          <i className="bi bi-arrow-left me-1" />
          Danh sách lệnh
        </Link>
        {id && lenh && <span className="cg-ma">{lenh.soLenh}</span>}
      </div>

      <form onSubmit={luu} style={{ maxWidth: 860 }}>
        <TheNoiDung tieuDe={id ? 'Sửa lệnh giao nhận' : 'Tạo lệnh giao nhận'} icon="bi-qr-code-scan">
          <div className="row g-3">
            <Truong nhan="Loại lệnh" batBuoc lop="col-md-6">
              <select className="form-select" value={gt.loaiLenh} onChange={doi('loaiLenh')}>
                {(form?.loaiLenh || []).map((l) => (
                  <option key={l.giaTri} value={l.giaTri}>
                    {l.nhan}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Khách hàng" lop="col-md-6">
              <select className="form-select" value={gt.maKH} onChange={doi('maKH')}>
                <option value="">— Không gắn khách —</option>
                {(form?.khachHang || []).map((k) => (
                  <option key={k.giaTri} value={k.giaTri}>
                    {k.nhan}
                  </option>
                ))}
              </select>
            </Truong>
            <Truong nhan="Biển số xe" batBuoc lop="col-md-6">
              <input className="form-control sp-o-nhap-bien" value={gt.bienSoXe} onChange={doi('bienSoXe')} maxLength={15} placeholder="51C-638.72" />
            </Truong>
            <Truong nhan="Số container (nếu có)" lop="col-md-6">
              <input className="form-control text-uppercase" value={gt.soCont} onChange={doi('soCont')} maxLength={11} placeholder="MSCU2201417" />
            </Truong>
            <Truong nhan="Tài xế" lop="col-md-6">
              <input className="form-control" value={gt.tenTaiXe} onChange={doi('tenTaiXe')} maxLength={100} />
            </Truong>
            <Truong nhan="SĐT tài xế" lop="col-md-6">
              <input className="form-control" value={gt.sdtTaiXe} onChange={doi('sdtTaiXe')} maxLength={20} />
            </Truong>
            <Truong nhan="Hẹn từ" batBuoc lop="col-md-6">
              <input className="form-control" type="datetime-local" value={gt.henTuLuc} onChange={doi('henTuLuc')} />
            </Truong>
            <Truong nhan="Hẹn đến" batBuoc lop="col-md-6">
              <input className="form-control" type="datetime-local" value={gt.henDenLuc} onChange={doi('henDenLuc')} />
            </Truong>
            <Truong nhan="Ghi chú" lop="col-12">
              <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={300} />
            </Truong>
          </div>
        </TheNoiDung>

        <TheNoiDung tieuDe={'Lưới slot cổng 24 giờ tới — tối đa ' + sucChua + ' xe/giờ'} icon="bi-calendar-week">
          <div className="cg-luoi-slot">
            {(form?.slot || []).map((s) => {
              const day = s.soLenh >= s.sucChua
              const sapDay = !day && s.soLenh >= s.sucChua - 2
              const luc = new Date(s.luc)
              return (
                <button
                  key={s.luc}
                  type="button"
                  className={'cg-slot ' + (day ? 'het-cho' : sapDay ? 'sap-day' : 'con-cho') + (slotChon === s.luc ? ' dang-chon' : '')}
                  disabled={day}
                  onClick={() => chonSlot(s)}
                  title={day ? 'Khung giờ đã đủ xe — chọn khung khác' : 'Chọn khung giờ này'}
                >
                  <b>{hai(luc.getHours())}:00</b>
                  <span>
                    {hai(luc.getDate())}/{hai(luc.getMonth() + 1)}
                  </span>
                  <br />
                  <span>
                    {s.soLenh}/{s.sucChua} xe
                  </span>
                </button>
              )
            })}
          </div>
          <div className="small text-body-secondary mt-2">
            Còn chỗ · sắp đầy · hết slot — mô hình TAS (Truck Appointment System) điều tiết xe theo giờ như HHIT Lạch Huyện. Bấm một khung giờ là tự điền
            giờ hẹn (2 tiếng).
          </div>
        </TheNoiDung>

        <div className="d-flex gap-2">
          <button className="btn btn-primary" type="submit" disabled={dangChay}>
            {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
            {id ? 'Lưu thay đổi' : 'Tạo lệnh + cấp PIN'}
          </button>
          <Link className="btn btn-outline-secondary" to="/admin/gatepass">
            Hủy
          </Link>
        </div>
      </form>
    </>
  )
}
