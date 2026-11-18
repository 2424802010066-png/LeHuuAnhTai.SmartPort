import { useMemo, useState } from 'react'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, OTimKiem, TheNoiDung, Truong, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'

const RONG = { tenHangTau: '', maSCAC: '', quocGia: '', website: '' }

const khongDau = (s) =>
  (s || '')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd')
    .toLowerCase()

/** Danh muc hang tau doi tac: xem, them, sua, xoa (thay Admin/ShippingLines cua ban MVC). */
export default function HangTau() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/hang-tau')
  const [tuKhoa, setTuKhoa] = useState('')
  const [dangSua, setDangSua] = useState(null) // null = dong form, 0 = them moi, >0 = ma dang sua
  const { gt, doi, datLai } = useBieuMau(RONG)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  // Danh muc nho (vai chuc dong) -> loc ngay tren trinh duyet, khong goi lai API
  const dsLoc = useMemo(() => {
    const k = khongDau(tuKhoa)
    return (duLieu || []).filter((h) => !k || khongDau(`${h.tenHangTau} ${h.maSCAC} ${h.quocGia}`).includes(k))
  }, [duLieu, tuKhoa])

  function moForm(h) {
    datLai(h ? { tenHangTau: h.tenHangTau || '', maSCAC: h.maSCAC || '', quocGia: h.quocGia || '', website: h.website || '' } : RONG)
    setDangSua(h ? h.maHangTau : 0)
  }

  async function luu(e) {
    e.preventDefault()
    const kq = await chay(() =>
      dangSua ? goiApi('/api/hang-tau/' + dangSua, { method: 'PUT', body: gt }) : goiApi('/api/hang-tau', { method: 'POST', body: gt }),
    )
    if (kq) {
      setDangSua(null)
      taiLai({ im: true })
    }
  }

  async function xoa(h) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa hãng tàu?',
      noiDung: `Xóa hãng tàu ${h.tenHangTau}. Thao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/hang-tau/' + h.maHangTau, { method: 'DELETE' }))) taiLai({ im: true })
  }

  const COT = [
    { khoa: 'ten', ten: 'Tên hãng tàu', hien: (h) => <span className="fw-semibold">{h.tenHangTau}</span> },
    { khoa: 'scac', ten: 'Mã SCAC', hien: (h) => (h.maSCAC ? <span className="badge text-bg-light border">{h.maSCAC}</span> : '—') },
    { khoa: 'quocGia', ten: 'Quốc gia', hien: (h) => h.quocGia || '—', an: 'md' },
    {
      khoa: 'web',
      ten: 'Website',
      an: 'lg',
      hien: (h) =>
        h.website ? (
          <a className="small" href={/^https?:/i.test(h.website) ? h.website : 'https://' + h.website} target="_blank" rel="noreferrer">
            {h.website}
          </a>
        ) : (
          '—'
        ),
    },
    { khoa: 'soTau', ten: 'Số tàu', canPhai: true, hien: (h) => h.soTau },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 110,
      hien: (h) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-primary me-1" title="Sửa" onClick={() => moForm(h)}>
            <i className="bi bi-pencil" />
          </button>
          <button type="button" className="btn btn-sm btn-outline-danger" title={h.soTau ? 'Còn tàu thuộc hãng — không xóa được' : 'Xóa'} disabled={h.soTau > 0 || dangChay} onClick={() => xoa(h)}>
            <i className="bi bi-trash" />
          </button>
        </>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <TheNoiDung
        tieuDe="Hãng tàu đối tác"
        icon="bi-globe2"
        khongDem
        phai={
          <>
            <OTimKiem giaTri={tuKhoa} doi={setTuKhoa} goiY="Tên, mã SCAC, quốc gia…" cho={0} />
            <button type="button" className="btn btn-sm btn-primary" onClick={() => moForm(null)}>
              <i className="bi bi-plus-lg me-1" />
              Thêm hãng tàu
            </button>
          </>
        }
      >
        <BangDuLieu cot={COT} dong={dsLoc} khoaDong="maHangTau" dangTai={dangTai} rong={tuKhoa ? 'Không có hãng tàu khớp bộ lọc.' : 'Chưa có hãng tàu nào.'} />
      </TheNoiDung>

      <HopThoai
        mo={dangSua !== null}
        dong={() => setDangSua(null)}
        khoaDong={dangChay}
        tieuDe={dangSua ? 'Sửa hãng tàu' : 'Thêm hãng tàu'}
        icon="bi-globe2"
        khiGui={luu}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setDangSua(null)} disabled={dangChay}>
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
              Lưu
            </button>
          </>
        }
      >
        <div className="row g-3">
          <Truong nhan="Tên hãng tàu" batBuoc lop="col-12">
            <input className="form-control" value={gt.tenHangTau} onChange={doi('tenHangTau')} maxLength={100} autoFocus />
          </Truong>
          <Truong nhan="Mã SCAC" goiY="4 ký tự, vd MAEU" lop="col-sm-5">
            <input className="form-control text-uppercase" value={gt.maSCAC} onChange={doi('maSCAC')} maxLength={10} />
          </Truong>
          <Truong nhan="Quốc gia" lop="col-sm-7">
            <input className="form-control" value={gt.quocGia} onChange={doi('quocGia')} maxLength={50} />
          </Truong>
          <Truong nhan="Website" lop="col-12">
            <input className="form-control" value={gt.website} onChange={doi('website')} maxLength={100} placeholder="www.maersk.com" />
          </Truong>
        </div>
      </HopThoai>
    </>
  )
}
