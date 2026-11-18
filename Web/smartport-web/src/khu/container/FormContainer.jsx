import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { dungChuan6346 } from '../../ai/ocrContainer'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, TheNoiDung, Truong } from '../../components/ui'
import { chuoiHoacNull, soHoacNull, useBieuMau } from '../../hooks/useBieuMau'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgay, ngayGioInput } from '../../utils/dinhDang'
import { API_CONT, TRANG_THAI_MAC_DINH } from './chung'
import './container.css'

const LOAI_MAC_DINH = ['20GP', '40GP', '40HC', '45HC', '20RF', '40RF', '20OT', '40OT']
const chuoi = (v) => (v == null ? '' : String(v))

/** O chon co them lua chon hien tai neu no khong nam trong danh muc (vd chuyen tau cu hon 30 chuyen gan nhat) */
function luaChonCo(ds, giaTri, nhan) {
  const kq = ds || []
  if (!giaTri || kq.some((x) => x.giaTri === giaTri)) return kq
  return [{ giaTri, nhan: nhan || '#' + giaTri }, ...kq]
}

function BieuMauContainer({ maSua }) {
  const [thamSo] = useSearchParams()
  const viTriTrang = useLocation()
  const dieuHuong = useNavigate()
  const quayVe = viTriTrang.state?.quayVe || '/admin/containers'

  const { duLieu: dm, loi: loiDm, taiLai: taiLaiDm } = useApi(API_CONT + '/danh-muc')
  const { duLieu: ct, loi: loiCt, dangTai: dangTaiCt, taiLai: taiLaiCt } = useApi(maSua ? `${API_CONT}/${maSua}` : null)
  const { chay, dangChay } = useThaoTac()
  const [goiY, setGoiY] = useState(null) // null | { dangTai } | ket qua API | { loi }

  // Them moi: mac dinh giong MVC (40HC, Full, Trong bai, vao bai luc nay); ?soCont= / ?viTriBai= tu tra cuu, so do bai
  const { gt, doi, datGt, datLai } = useBieuMau(() => ({
    soCont: (thamSo.get('soCont') || '').toUpperCase(),
    loaiCont: '40HC',
    tinhTrang: 'Full',
    trongLuong: '',
    hangHoa: '',
    maKH: '',
    maHangTau: '',
    maChuyenNhap: '',
    maChuyenXuat: '',
    viTriBai: (thamSo.get('viTriBai') || '').toUpperCase(),
    trangThai: 'Trong bãi',
    ngayVaoBai: maSua ? '' : ngayGioInput(new Date()),
    ngayRaBai: '',
    nhietDo: '',
  }))

  // Sua: nap du lieu container vao form khi API tra ve
  useEffect(() => {
    if (!ct) return
    datLai({
      soCont: ct.soCont || '',
      loaiCont: ct.loaiCont || '',
      tinhTrang: ct.tinhTrang || '',
      trongLuong: chuoi(ct.trongLuong),
      hangHoa: ct.hangHoa || '',
      maKH: chuoi(ct.maKH),
      maHangTau: chuoi(ct.maHangTau),
      maChuyenNhap: chuoi(ct.maChuyenNhap),
      maChuyenXuat: chuoi(ct.maChuyenXuat),
      viTriBai: ct.viTriBai || '',
      trangThai: ct.trangThai || 'Trong bãi',
      ngayVaoBai: ngayGioInput(ct.ngayVaoBai),
      ngayRaBai: ngayGioInput(ct.ngayRaBai),
      nhietDo: chuoi(ct.nhietDo),
    })
  }, [ct, datLai])

  if (maSua && !ct) {
    if (loiCt)
      return (
        <>
          <BaoLoi loi={loiCt} thuLai={taiLaiCt} />
          <Link className="btn btn-sm btn-outline-secondary" to="/admin/containers">
            <i className="bi bi-arrow-left me-1" />
            Danh sách container
          </Link>
        </>
      )
    return dangTaiCt ? <DangTai /> : null
  }

  const soSach = gt.soCont.trim().toUpperCase()
  const dungDang = /^[A-Z]{4}\d{7}$/.test(soSach)
  const loiSo = soSach.length >= 11 && !dungDang ? 'Số container phải gồm 4 chữ cái + 7 chữ số (VD: MSCU2201417).' : null
  const laLanh = /RF/i.test(gt.loaiCont)
  const cacLoai = [...new Set([...(dm?.loaiCont ?? LOAI_MAC_DINH), ...(gt.loaiCont ? [gt.loaiCont] : [])])]
  const cacTinhTrang = [...new Set([...(dm?.tinhTrang ?? ['Full', 'Empty']), ...(gt.tinhTrang ? [gt.tinhTrang] : [])])]
  const cacTrangThai = [...new Set([...(dm?.trangThai ?? TRANG_THAI_MAC_DINH), ...(gt.trangThai ? [gt.trangThai] : [])])]

  async function goiYViTri() {
    setGoiY({ dangTai: true })
    try {
      // Gui kem khach hang / hang tau / chuyen xuat: AI dua vao do du bao NGAY ROI BAI de chon chong it phai dao
      const kq = await goiApi(
        '/api/so-do-bai/goi-y-o' +
          taoQuery({
            loai: gt.loaiCont,
            trangThai: gt.trangThai,
            tinhTrang: gt.tinhTrang,
            nhietDo: gt.nhietDo,
            maKH: gt.maKH,
            maHangTau: gt.maHangTau,
            maChuyenXuat: gt.maChuyenXuat,
          }),
      )
      if (kq.viTri) datGt({ viTriBai: kq.viTri })
      setGoiY(kq)
    } catch (e) {
      setGoiY({ loi: e.trangThai === 0 ? 'Không kết nối được AI, thử lại sau.' : e.message })
    }
  }

  async function luu(e) {
    e.preventDefault()
    const body = {
      soCont: soSach,
      loaiCont: gt.loaiCont,
      tinhTrang: chuoiHoacNull(gt.tinhTrang),
      trongLuong: soHoacNull(gt.trongLuong),
      hangHoa: chuoiHoacNull(gt.hangHoa.trim()),
      maKH: soHoacNull(gt.maKH),
      maHangTau: soHoacNull(gt.maHangTau),
      maChuyenNhap: soHoacNull(gt.maChuyenNhap),
      maChuyenXuat: soHoacNull(gt.maChuyenXuat),
      viTriBai: chuoiHoacNull(gt.viTriBai.trim().toUpperCase()),
      trangThai: gt.trangThai,
      ngayVaoBai: chuoiHoacNull(gt.ngayVaoBai),
      ngayRaBai: chuoiHoacNull(gt.ngayRaBai),
      nhietDo: soHoacNull(gt.nhietDo),
    }
    const kq = await chay(() =>
      maSua ? goiApi(`${API_CONT}/${maSua}`, { method: 'PUT', body }) : goiApi(API_CONT, { method: 'POST', body }),
    )
    if (kq) dieuHuong(quayVe)
  }

  return (
    <div className="row justify-content-center">
      <div className="col-xl-10">
        <BaoLoi loi={loiDm} thuLai={taiLaiDm} />
        <form onSubmit={luu}>
          <TheNoiDung
            tieuDe={maSua ? `Sửa container ${ct?.soCont ?? ''}` : 'Thêm container'}
            icon="bi-box-seam"
            chan={
              <div className="d-flex flex-wrap gap-2">
                <button type="submit" className="btn btn-primary px-4" disabled={dangChay}>
                  {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-save me-1" />}
                  Lưu container
                </button>
                <Link className="btn btn-light" to={quayVe}>
                  Quay lại
                </Link>
              </div>
            }
          >
            <div className="row g-3">
              <Truong
                nhan="Số container"
                batBuoc
                lop="col-md-4"
                loi={loiSo}
                goiY={
                  !soSach ? (
                    '4 chữ cái + 7 chữ số, chữ số cuối là số kiểm tra.'
                  ) : !dungDang ? null : dungChuan6346(soSach) ? (
                    <span className="text-success">
                      <i className="bi bi-check-circle-fill me-1" />
                      Số kiểm tra ISO 6346 hợp lệ
                    </span>
                  ) : (
                    <span className="text-warning-emphasis">
                      <i className="bi bi-exclamation-triangle-fill me-1" />
                      Số kiểm tra ISO 6346 không khớp — xem lại từng ký tự
                    </span>
                  )
                }
              >
                <input
                  className="form-control text-uppercase cb-so-cont"
                  value={gt.soCont}
                  onChange={(e) => datGt({ soCont: e.target.value.toUpperCase() })}
                  placeholder="VD: MSCU2201417"
                  maxLength={11}
                  autoFocus={!maSua}
                />
              </Truong>
              <Truong nhan="Loại" batBuoc lop="col-md-3 col-6">
                <select className="form-select" value={gt.loaiCont} onChange={doi('loaiCont')}>
                  {cacLoai.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Tình trạng" lop="col-md-2 col-6">
                <select className="form-select" value={gt.tinhTrang} onChange={doi('tinhTrang')}>
                  {cacTinhTrang.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Trọng lượng (tấn)" lop="col-md-3">
                <input className="form-control" type="number" step="0.01" min="0" value={gt.trongLuong} onChange={doi('trongLuong')} />
              </Truong>

              <Truong nhan="Hàng hóa" lop="col-md-6">
                <input className="form-control" value={gt.hangHoa} onChange={doi('hangHoa')} maxLength={200} />
              </Truong>
              <Truong nhan="Khách hàng (chủ hàng)" lop="col-md-6">
                <select className="form-select" value={gt.maKH} onChange={doi('maKH')}>
                  <option value="">— Không —</option>
                  {luaChonCo(dm?.khachHang, gt.maKH).map((x) => (
                    <option key={x.giaTri} value={x.giaTri}>
                      {x.nhan}
                    </option>
                  ))}
                </select>
              </Truong>

              <Truong nhan="Chuyến nhập (dỡ từ tàu)" lop="col-md-4">
                <select className="form-select" value={gt.maChuyenNhap} onChange={doi('maChuyenNhap')}>
                  <option value="">— Không —</option>
                  {luaChonCo(dm?.chuyenTau, gt.maChuyenNhap, ct?.tenChuyenNhap).map((x) => (
                    <option key={x.giaTri} value={x.giaTri}>
                      {x.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Chuyến xuất (xếp lên tàu)" lop="col-md-4">
                <select className="form-select" value={gt.maChuyenXuat} onChange={doi('maChuyenXuat')}>
                  <option value="">— Không —</option>
                  {luaChonCo(dm?.chuyenTau, gt.maChuyenXuat, ct?.tenChuyenXuat).map((x) => (
                    <option key={x.giaTri} value={x.giaTri}>
                      {x.nhan}
                    </option>
                  ))}
                </select>
              </Truong>
              <Truong nhan="Hãng tàu (chủ vỏ)" goiY="Bắt buộc với container rỗng của depot" lop="col-md-4">
                <select className="form-select" value={gt.maHangTau} onChange={doi('maHangTau')}>
                  <option value="">— Không —</option>
                  {luaChonCo(dm?.hangTau, gt.maHangTau).map((x) => (
                    <option key={x.giaTri} value={x.giaTri}>
                      {x.nhan}
                    </option>
                  ))}
                </select>
              </Truong>

              <div className="col-md-4">
                <label className="form-label small fw-semibold mb-1" htmlFor="cb-vi-tri-bai">
                  Vị trí bãi (Khu-Bay-Hàng-Tầng)
                </label>
                <div className="input-group">
                  <input
                    id="cb-vi-tri-bai"
                    className="form-control text-uppercase"
                    value={gt.viTriBai}
                    onChange={doi('viTriBai')}
                    placeholder="VD: A-02-03-2"
                    maxLength={20}
                  />
                  <button
                    type="button"
                    className="btn btn-outline-primary"
                    onClick={goiYViTri}
                    disabled={goiY?.dangTai}
                    title="AI gợi ý vị trí hạ bãi tối ưu"
                  >
                    {goiY?.dangTai ? <span className="spinner-border spinner-border-sm" /> : <i className="bi bi-stars" />}
                    <span className="ms-1">AI</span>
                  </button>
                </div>
                {goiY && !goiY.dangTai && (
                  <div className={'cb-goi-y' + (goiY.viTri ? '' : ' loi')} role="status">
                    {goiY.viTri ? (
                      <>
                        <div className="fw-semibold text-success">
                          <i className="bi bi-stars me-1" />
                          AI đề xuất <span className="cb-so-cont">{goiY.viTri}</span> · {goiY.tenKhu}
                        </div>
                        <ul>
                          {goiY.lyDo.map((l) => (
                            <li key={l}>{l}</li>
                          ))}
                        </ul>
                        {goiY.ngayRoiDuKien && (
                          <div className="small">
                            <i className="bi bi-calendar-check me-1" />
                            Dự báo rời bãi <b>{dinhDangNgay(goiY.ngayRoiDuKien)}</b> · đặt ở đây chặn{' '}
                            <b>{goiY.soContSeBiChan}</b> container
                            {goiY.viTriCachCu && goiY.viTriCachCu !== goiY.viTri && (
                              <>
                                {' '}
                                (cách cũ {goiY.viTriCachCu} chặn <b>{goiY.soContBiChanCachCu}</b>)
                              </>
                            )}
                            {goiY.nguon && <span className="text-body-secondary"> · {goiY.nguon}</span>}
                            <Link className="ms-2" to="/admin/ai/bai">
                              Xem AI bãi
                            </Link>
                          </div>
                        )}
                      </>
                    ) : (
                      <span className="text-danger">
                        <i className="bi bi-exclamation-triangle me-1" />
                        {goiY.loi || goiY.lyDo?.[0] || 'Không tìm được vị trí phù hợp.'}
                      </span>
                    )}
                  </div>
                )}
              </div>
              <Truong nhan="Trạng thái" batBuoc lop="col-md-4 col-6">
                <select className="form-select" value={gt.trangThai} onChange={doi('trangThai')}>
                  {cacTrangThai.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </Truong>
              <Truong
                nhan="Nhiệt độ (°C, cont lạnh)"
                lop="col-md-4 col-6"
                goiY={laLanh && gt.nhietDo === '' ? <span className="text-warning-emphasis">Cont lạnh — nhập nhiệt độ cài đặt</span> : null}
              >
                <input className="form-control" type="number" step="0.1" min="-60" max="60" value={gt.nhietDo} onChange={doi('nhietDo')} placeholder="VD: -18" />
              </Truong>

              <Truong nhan="Ngày vào bãi" lop="col-md-4 col-6">
                <input className="form-control" type="datetime-local" value={gt.ngayVaoBai} onChange={doi('ngayVaoBai')} />
              </Truong>
              <Truong nhan="Ngày ra bãi" lop="col-md-4 col-6">
                <input className="form-control" type="datetime-local" value={gt.ngayRaBai} onChange={doi('ngayRaBai')} />
              </Truong>
            </div>
          </TheNoiDung>
        </form>
      </div>
    </div>
  )
}

/** Them / sua container (thay Admin/Containers/Form + Save + SuggestSlot). Doi id -> dung lai form moi. */
export default function FormContainer() {
  const { id } = useParams()
  const maSua = Number(id) > 0 ? Number(id) : 0
  return <BieuMauContainer key={maSua || 'moi'} maSua={maSua} />
}
