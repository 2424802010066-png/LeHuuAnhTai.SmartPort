import { useEffect, useRef, useState } from 'react'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, HopThoai, NhanTrangThai, PhanTrang, TheChiSo, TheNoiDung, Truong, useThongBao, useXacNhan } from '../../components/ui'
import { useBieuMau } from '../../hooks/useBieuMau'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien, gioNgayNgan, ngayGioInput } from '../../utils/dinhDang'
import './tai-xe.css'

const CAC_CA = ['Ca 1', 'Ca 2', 'Ca 3', 'Cả ngày']
const KHOANG_NGAY = [7, 30, 90]
const FORM_RONG = { cong: '*', ca: 'Ca 1', tuLuc: '', denLuc: '', soDemThucTe: '', moBarieTay: '0', nguon: 'ThuCong', ghiChu: '' }

// Muc do lech — cung quy tac DemXeCong.MucDo o may chu
const mucDoLech = (lech) => {
  const l = Math.abs(lech)
  return l === 0 ? 'Khớp' : l <= 2 ? 'Sai số nhỏ' : l <= 5 ? 'Cần kiểm tra' : 'Nghi vấn'
}
const MAU_NHAN = { Khớp: 'ok', 'Sai số nhỏ': 'info', 'Cần kiểm tra': 'warn', 'Nghi vấn': 'do' }
const MAU_CHU = {
  Khớp: 'text-success-emphasis',
  'Sai số nhỏ': 'text-info-emphasis',
  'Cần kiểm tra': 'text-warning-emphasis',
  'Nghi vấn': 'text-danger-emphasis',
}
const ICON_MUC = {
  Khớp: 'bi-check-circle-fill',
  'Sai số nhỏ': 'bi-info-circle-fill',
  'Cần kiểm tra': 'bi-exclamation-triangle-fill',
  'Nghi vấn': 'bi-shield-exclamation',
}
const GIAI_THICH_LECH = {
  'Sai số nhỏ': 'sai số nhỏ, chấp nhận được',
  'Cần kiểm tra': 'cần kiểm tra lại',
  'Nghi vấn': 'nghi vấn, sẽ mở sự cố an ninh',
}

const tenCong = (c) => (c === '*' ? 'Tất cả cổng' : 'Cổng ' + c)
const coDau = (n) => (n > 0 ? '+' : '') + n
/** "2026-09-18T06:00" (o datetime-local) -> them giay de may chu doc chac chan */
const coGiay = (v) => (v && v.length === 16 ? v + ':00' : v)

/**
 * Doi soat cong — chong gian lan (thay Admin/DoiSoatCong cua ban MVC): cuoi ca nhap so dem doc lap
 * (dem tay / cam bien), he thong tu dem so luot da ghi nhan; lech tren 5 luot tu mo su co an ninh.
 */
export default function DoiSoatCong() {
  const [loc, datLoc] = useBoLoc({ soNgay: 30, trang: 1 })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/doi-soat-cong' + taoQuery(loc))
  const { gt, doi, datGt } = useBieuMau(FORM_RONG)
  const { chay, dangChay } = useThaoTac()
  const bao = useThongBao()
  const xacNhan = useXacNhan()

  const [soHT, setSoHT] = useState({ so: null, dangDem: false, loi: '' })
  const [lanDem, setLanDem] = useState(0)
  const [kyGiaiTrinh, setKyGiaiTrinh] = useState(null)
  const [noiDungGiaiTrinh, setNoiDungGiaiTrinh] = useState('')
  const canNapMacDinh = useRef(true)

  // Ky goi y (ca vua ket thuc) theo gio MAY CHU: nap vao form lan dau va sau moi lan ghi
  useEffect(() => {
    if (!d || !canNapMacDinh.current) return
    canNapMacDinh.current = false
    datGt({ tuLuc: ngayGioInput(d.macDinhTu), denLuc: ngayGioInput(d.macDinhDen) })
  }, [d, datGt])

  // So HE THONG tu dem — hien ngay khi doi cong / khoang thoi gian, truoc khi go so dem thuc te
  useEffect(() => {
    if (!gt.tuLuc || !gt.denLuc) {
      setSoHT({ so: null, dangDem: false, loi: '' })
      return undefined
    }
    let conHieuLuc = true
    setSoHT((x) => ({ ...x, dangDem: true }))
    const hen = setTimeout(async () => {
      try {
        const kq = await goiApi('/api/doi-soat-cong/dem-he-thong' + taoQuery({ cong: gt.cong, tu: coGiay(gt.tuLuc), den: coGiay(gt.denLuc) }))
        if (conHieuLuc) setSoHT({ so: kq.so, dangDem: false, loi: '' })
      } catch (e) {
        if (conHieuLuc) setSoHT({ so: null, dangDem: false, loi: e.message })
      }
    }, 300)
    return () => {
      conHieuLuc = false
      clearTimeout(hen)
    }
  }, [gt.cong, gt.tuLuc, gt.denLuc, lanDem])

  // Xe vua qua cong (o bat ky may nao) -> dem lai ngam; gop cac su kien sat nhau
  const henDemLai = useRef(null)
  useNgheSuKien('cong.*', () => {
    clearTimeout(henDemLai.current)
    henDemLai.current = setTimeout(() => setLanDem((n) => n + 1), 800)
  })
  useEffect(() => () => clearTimeout(henDemLai.current), [])

  const soDem = gt.soDemThucTe === '' ? null : parseInt(gt.soDemThucTe, 10)
  const lech = soHT.so != null && soDem != null && !Number.isNaN(soDem) ? Math.max(0, soDem) - soHT.so : null
  const mucDo = lech != null ? mucDoLech(lech) : null

  async function ghiDoiSoat(e) {
    e.preventDefault()
    const than = {
      cong: gt.cong,
      ca: gt.ca,
      tuLuc: coGiay(gt.tuLuc),
      denLuc: coGiay(gt.denLuc),
      soDemThucTe: Number(gt.soDemThucTe),
      moBarieTay: gt.moBarieTay === '' ? 0 : Number(gt.moBarieTay),
      nguon: gt.nguon,
      ghiChu: gt.ghiChu,
    }
    const kq = await chay(() => goiApi('/api/doi-soat-cong', { method: 'POST', body: than }), { imLang: true })
    if (!kq) return
    // Lech tren 5 luot: ban ghi DA luu nhung phai bao dong (MVC hien khung do)
    if (kq.duLieu?.canhBao) bao.loi(kq.thongBao)
    else bao.thanhCong(kq.thongBao)
    datGt({ soDemThucTe: '', moBarieTay: '0', ghiChu: '' })
    canNapMacDinh.current = true
    if (loc.trang !== 1) datLoc({ trang: 1 })
    else taiLai({ im: true })
  }

  function moGiaiTrinh(k) {
    setNoiDungGiaiTrinh('')
    setKyGiaiTrinh(k)
  }

  async function guiGiaiTrinh(e) {
    e.preventDefault()
    const kq = await chay(() => goiApi(`/api/doi-soat-cong/${kyGiaiTrinh.maDem}/giai-trinh`, { method: 'POST', body: { noiDung: noiDungGiaiTrinh } }))
    if (kq) {
      setKyGiaiTrinh(null)
      taiLai({ im: true })
    }
  }

  async function xoa(k) {
    const dongY = await xacNhan({
      tieuDe: 'Xóa bản đối soát?',
      noiDung: `${tenCong(k.cong)}${k.ca ? ' · ' + k.ca : ''} · ${gioNgayNgan(k.tuLuc)} → ${gioNgayNgan(k.denLuc)} · lệch ${coDau(k.chenhLech)} lượt.\nThao tác này không hoàn tác được.`,
      nutDongY: 'Xóa',
      mau: 'danger',
    })
    if (!dongY) return
    if (await chay(() => goiApi('/api/doi-soat-cong/' + k.maDem, { method: 'DELETE' }))) {
      if (d?.danhSach.items.length === 1 && loc.trang > 1) datLoc({ trang: loc.trang - 1 })
      else taiLai({ im: true })
    }
  }

  const COT = [
    {
      khoa: 'ky',
      ten: 'Kỳ',
      hien: (k) => (
        <>
          <div className="fw-semibold">
            {tenCong(k.cong)}
            {k.ca && <> · {k.ca}</>}
          </div>
          <div className="small text-body-secondary text-nowrap">
            {gioNgayNgan(k.tuLuc)} → {dinhDangNgayGio(k.denLuc)}
          </div>
          <div className="small text-body-secondary">
            {k.nguoiNhap || '—'} · {k.nguon === 'CamBien' ? 'cảm biến' : 'đếm tay'}
          </div>
        </>
      ),
    },
    { khoa: 'dem', ten: 'Đếm', canPhai: true, hien: (k) => <span className="fw-bold">{dinhDangSo(k.soDemThucTe)}</span> },
    { khoa: 'heThong', ten: 'Hệ thống', canPhai: true, hien: (k) => dinhDangSo(k.soHeThong) },
    { khoa: 'lech', ten: 'Lệch', canPhai: true, hien: (k) => <span className={'fw-bold ' + MAU_CHU[k.mucDo]}>{coDau(k.chenhLech)}</span> },
    {
      khoa: 'danhGia',
      ten: 'Đánh giá',
      hien: (k) => (
        <>
          <NhanTrangThai giaTri={k.mucDo} mau={MAU_NHAN[k.mucDo]} />
          {k.moBarieTay > 0 && <span className="badge text-bg-warning ms-1">mở tay {k.moBarieTay} lần</span>}
          {k.ghiChu && <div className="small text-body-secondary">{k.ghiChu}</div>}
          {k.daGiaiTrinh ? (
            <div className="small text-success-emphasis">
              <i className="bi bi-check2 me-1" />
              {k.giaiTrinh}
            </div>
          ) : k.canGiaiTrinh ? (
            <div className="small text-danger-emphasis">
              <i className="bi bi-exclamation-circle me-1" />
              Chưa giải trình
            </div>
          ) : null}
        </>
      ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 96,
      hien: (k) => (
        <span className="text-nowrap">
          {k.canGiaiTrinh && (
            <button type="button" className="btn btn-sm btn-outline-success me-1" title="Ghi giải trình" aria-label="Ghi giải trình" onClick={() => moGiaiTrinh(k)}>
              <i className="bi bi-pencil-square" />
            </button>
          )}
          <button type="button" className="btn btn-sm btn-outline-danger" title="Xóa bản đối soát" aria-label="Xóa bản đối soát" disabled={dangChay} onClick={() => xoa(k)}>
            <i className="bi bi-trash" />
          </button>
        </span>
      ),
    },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="row g-3 mb-3">
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-clipboard-check" mau="xanh" so={dinhDangSo(d?.soKy)} nhan={`Kỳ đã đối soát · ${dinhDangSo(d?.soKhop)} kỳ khớp`} />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo icon="bi-shield-exclamation" mau={d?.soNghiVan > 0 ? 'do' : 'la'} so={dinhDangSo(d?.soNghiVan)} nhan="Kỳ nghi vấn · lệch trên 5 lượt" />
        </div>
        <div className="col-6 col-xl-3 tx-chi-so-tien">
          <TheChiSo
            icon="bi-cash-coin"
            mau="cam"
            so={dinhDangTien(d?.tienNghiThatThoat)}
            nhan={`Nghi thất thoát · ${dinhDangSo(d?.luotVuot)} lượt × ${dinhDangTien(d?.giaTB)}`}
          />
        </div>
        <div className="col-6 col-xl-3">
          <TheChiSo
            icon="bi-hand-index-thumb"
            mau={d?.tongMoTay > 0 ? 'cam' : 'la'}
            so={dinhDangSo(d?.tongMoTay)}
            nhan={`Mở barie tay · ${dinhDangSo(d?.chuaGiaiTrinh)} kỳ chưa giải trình`}
          />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-4">
          <TheNoiDung tieuDe="Ghi đối soát cuối ca" icon="bi-plus-square">
            <form onSubmit={ghiDoiSoat}>
              <div className="row g-2">
                <Truong nhan="Cổng" lop="col-7">
                  <select className="form-select" value={gt.cong} onChange={doi('cong')}>
                    <option value="*">Tất cả cổng</option>
                    {(d?.congs || []).map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </Truong>
                <Truong nhan="Ca" lop="col-5">
                  <select className="form-select" value={gt.ca} onChange={doi('ca')}>
                    {CAC_CA.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </Truong>
                <Truong nhan="Từ lúc" batBuoc lop="col-12">
                  <input className="form-control" type="datetime-local" value={gt.tuLuc} onChange={doi('tuLuc')} />
                </Truong>
                <Truong nhan="Đến lúc" batBuoc lop="col-12">
                  <input className="form-control" type="datetime-local" value={gt.denLuc} onChange={doi('denLuc')} />
                </Truong>

                <div className="col-12">
                  <div className="tx-o-he-thong" aria-live="polite">
                    <div className="tx-chu-giai">Hệ thống ghi nhận trong khoảng này</div>
                    <div className="tx-so-he-thong">
                      {soHT.dangDem ? (
                        <span className="spinner-border spinner-border-sm" />
                      ) : soHT.so != null ? (
                        <>
                          {dinhDangSo(soHT.so)} <span className="fs-6 fw-normal">lượt</span>
                        </>
                      ) : (
                        '—'
                      )}
                    </div>
                    <div className={'tx-chu-giai' + (soHT.loi ? ' text-danger-emphasis' : '')}>{soHT.loi || 'Máy tự đếm — không sửa được.'}</div>
                  </div>
                </div>

                <Truong nhan="Số đếm thực tế" batBuoc lop="col-12">
                  <input
                    className="form-control tx-o-so-dem"
                    type="number"
                    min={0}
                    max={100000}
                    step={1}
                    inputMode="numeric"
                    value={gt.soDemThucTe}
                    onChange={doi('soDemThucTe')}
                    placeholder="Từ cảm biến hoặc đếm tay"
                  />
                </Truong>
                {mucDo && (
                  <div className={'col-12 small ' + MAU_CHU[mucDo] + (Math.abs(lech) > 2 ? ' fw-bold' : '')}>
                    <i className={'bi me-1 ' + ICON_MUC[mucDo]} />
                    {lech === 0 ? 'Khớp hoàn toàn' : `Lệch ${coDau(lech)} — ${GIAI_THICH_LECH[mucDo]}`}
                  </div>
                )}

                <Truong nhan="Mở barie tay" lop="col-6">
                  <input className="form-control" type="number" min={0} max={100000} step={1} value={gt.moBarieTay} onChange={doi('moBarieTay')} />
                </Truong>
                <Truong nhan="Nguồn đếm" lop="col-6">
                  <select className="form-select" value={gt.nguon} onChange={doi('nguon')}>
                    <option value="ThuCong">Đếm tay</option>
                    <option value="CamBien">Cảm biến</option>
                  </select>
                </Truong>
                <Truong nhan="Ghi chú" lop="col-12">
                  <input className="form-control" value={gt.ghiChu} onChange={doi('ghiChu')} maxLength={300} placeholder="VD: mưa lớn, xe cứu hỏa vào diễn tập…" />
                </Truong>
              </div>
              <button type="submit" className="btn btn-primary w-100 mt-3" disabled={dangChay}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2-square me-1" />}
                Ghi đối soát
              </button>
            </form>
          </TheNoiDung>
        </div>

        <div className="col-lg-8">
          <TheNoiDung
            tieuDe="Lịch sử đối soát"
            icon="bi-clock-history"
            khongDem
            phai={
              <div className="btn-group btn-group-sm" role="group" aria-label="Khoảng ngày">
                {KHOANG_NGAY.map((n) => (
                  <button
                    key={n}
                    type="button"
                    className={'btn ' + (loc.soNgay === n ? 'btn-primary' : 'btn-outline-primary')}
                    aria-pressed={loc.soNgay === n}
                    onClick={() => datLoc({ soNgay: n, trang: 1 })}
                  >
                    {n} ngày
                  </button>
                ))}
              </div>
            }
            chan={
              <>
                <PhanTrang {...d?.danhSach} doiTrang={(t) => datLoc({ trang: t })} donVi="kỳ" />
                <div className="small text-body-secondary mt-1">
                  <i className="bi bi-info-circle me-1" />
                  Lệch 1–2: sai số nhỏ · 3–5: cần kiểm tra · trên 5: nghi vấn, tự mở sự cố an ninh.
                </div>
              </>
            }
          >
            <BangDuLieu
              cot={COT}
              dong={d?.danhSach?.items}
              khoaDong="maDem"
              dangTai={dangTai}
              rong={
                <>
                  Chưa có bản đối soát nào trong {loc.soNgay} ngày — cuối mỗi ca ghi số đếm thực tế ở khung bên trái.
                </>
              }
            />
          </TheNoiDung>
        </div>
      </div>

      <HopThoai
        mo={!!kyGiaiTrinh}
        dong={() => setKyGiaiTrinh(null)}
        khoaDong={dangChay}
        tieuDe="Giải trình chênh lệch"
        icon="bi-pencil-square"
        khiGui={guiGiaiTrinh}
        chan={
          <>
            <button type="button" className="btn btn-light" onClick={() => setKyGiaiTrinh(null)} disabled={dangChay}>
              Hủy
            </button>
            <button type="submit" className="btn btn-success" disabled={dangChay}>
              {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2 me-1" />}
              Ghi giải trình
            </button>
          </>
        }
      >
        {kyGiaiTrinh && (
          <>
            <div className="small text-body-secondary mb-3">
              {tenCong(kyGiaiTrinh.cong)}
              {kyGiaiTrinh.ca ? ' · ' + kyGiaiTrinh.ca : ''} · {gioNgayNgan(kyGiaiTrinh.tuLuc)} → {gioNgayNgan(kyGiaiTrinh.denLuc)} · đếm{' '}
              {kyGiaiTrinh.soDemThucTe} / hệ thống {kyGiaiTrinh.soHeThong} · lệch{' '}
              <b className={MAU_CHU[kyGiaiTrinh.mucDo]}>{coDau(kyGiaiTrinh.chenhLech)}</b> lượt
            </div>
            <Truong nhan="Lý do lệch" batBuoc lop="mb-0">
              <textarea
                className="form-control"
                rows={3}
                maxLength={500}
                value={noiDungGiaiTrinh}
                onChange={(e) => setNoiDungGiaiTrinh(e.target.value)}
                placeholder="VD: cảm biến đếm đôi khi xe rơ-moóc dài đi qua…"
                autoFocus
              />
            </Truong>
          </>
        )}
      </HopThoai>
    </>
  )
}
