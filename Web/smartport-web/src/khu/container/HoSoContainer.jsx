import { Fragment, useCallback, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheNoiDung, useXacNhan } from '../../components/ui'
import { useThaoTac } from '../../hooks/useThaoTac'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien, gioNgayNgan } from '../../utils/dinhDang'
import { API_CONT, buocVongDoi, duongEir, duongForm, laDuongNoiBo, mauAnToan, soDo, useGiaoKhach } from './chung'
import './container.css'

const TEN_BUOC = ['Trên tàu / nhập cảng', 'Hạ bãi — lưu bãi', 'Lệnh giao / chờ xuất', 'Đã giao / đã xuất']

function VongDoi({ buoc }) {
  return (
    <div className="cb-buoc" aria-label={`Vòng đời container: bước ${buoc}/4`}>
      {TEN_BUOC.map((ten, i) => {
        const so = i + 1
        const lop = so < buoc ? 'xong' : so === buoc ? 'hien' : ''
        return (
          <Fragment key={ten}>
            <div className={'cb-buoc-muc ' + lop}>
              <span className="cb-buoc-cham">{so < buoc ? <i className="bi bi-check-lg" /> : so}</span>
              <span className="cb-buoc-ten">{ten}</span>
            </div>
            {so < 4 && <div className={'cb-buoc-noi' + (so < buoc ? ' xong' : '')} />}
          </Fragment>
        )
      })}
    </div>
  )
}

function TenChuyen({ c }) {
  if (!c) return '—'
  return (
    <>
      <span className="fw-semibold">{c.maChuyenHieu}</span>
      {c.tenTau && <> — {c.tenTau}</>}
      <div className="small text-body-secondary">
        ETA {gioNgayNgan(c.eta)} · ETD {gioNgayNgan(c.etd)}
      </div>
    </>
  )
}

/** Dong thoi gian hop nhat: loc theo nhom ngay tren trinh duyet (khong goi lai API) */
function DongThoiGian({ ds }) {
  const [nhom, setNhom] = useState('')
  const cacNhom = useMemo(() => [...new Set(ds.map((s) => s.nhom).filter(Boolean))], [ds])
  const dsLoc = nhom ? ds.filter((s) => s.nhom === nhom) : ds

  return (
    <TheNoiDung
      tieuDe="Dòng thời gian hợp nhất"
      icon="bi-clock-history"
      phai={<span className="badge text-bg-light border fw-normal">{ds.length} sự kiện</span>}
    >
      {ds.length === 0 ? (
        <div className="text-center text-body-secondary small py-3">
          Chưa có sự kiện. Dòng thời gian tự có khi xe chở container qua cổng theo lệnh giao nhận.
        </div>
      ) : (
        <>
          <div className="cb-dtg-loc" role="group" aria-label="Lọc dòng thời gian theo nhóm">
            <button type="button" className={nhom === '' ? 'on' : ''} onClick={() => setNhom('')}>
              Tất cả
            </button>
            {cacNhom.map((n) => (
              <button type="button" key={n} className={nhom === n ? 'on' : ''} onClick={() => setNhom(n)}>
                {n}
              </button>
            ))}
          </div>
          <div className="cb-dtg-cuon">
            <div className="cb-dtg">
              {dsLoc.map((s, i) => (
                <div className="cb-dtg-muc" key={i + s.luc + s.tieuDe}>
                  <span className="cb-dtg-cham" style={{ background: mauAnToan(s.mau) }}>
                    <i className={'bi ' + (/^bi-[a-z0-9-]+$/.test(s.icon || '') ? s.icon : 'bi-dot')} />
                  </span>
                  <div className="cb-dtg-gio">
                    {dinhDangNgayGio(s.luc)} · {s.nhom}
                  </div>
                  <div className="cb-dtg-td">{laDuongNoiBo(s.duongDan) ? <Link to={s.duongDan}>{s.tieuDe}</Link> : s.tieuDe}</div>
                  {s.moTa && <div className="cb-dtg-mt">{s.moTa}</div>}
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </TheNoiDung>
  )
}

function NoiDungHoSo({ id }) {
  const { duLieu: hs, loi, dangTai, taiLai } = useApi(`${API_CONT}/${id}/ho-so`)
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()

  const lamMoi = useCallback(() => taiLai({ im: true }), [taiLai])
  useNgheSuKien('bai.*', lamMoi)
  const { giaoKhach, dangGiao } = useGiaoKhach(lamMoi)

  if (!hs) {
    if (loi)
      return (
        <>
          <BaoLoi loi={loi} thuLai={taiLai} />
          <Link className="btn btn-sm btn-outline-secondary" to="/admin/containers">
            <i className="bi bi-arrow-left me-1" />
            Danh sách container
          </Link>
        </>
      )
    return dangTai ? <DangTai /> : null
  }

  const buoc = buocVongDoi(hs.trangThai)
  const dh = hs.dongHo

  async function chotPhieu() {
    const dongY = await xacNhan({
      tieuDe: 'Chốt phí lưu bãi?',
      noiDung: `Chốt phí lưu bãi ${dinhDangTien(dh.tongTien)} cho container ${hs.soCont} thành phiếu thu.`,
      nutDongY: 'Chốt phiếu thu',
    })
    if (!dongY) return
    if (await chay(() => goiApi(`${API_CONT}/${hs.maCont}/chot-luu-bai`, { method: 'POST' }))) lamMoi()
  }

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <Link className="btn btn-sm btn-outline-secondary" to="/admin/containers">
          <i className="bi bi-arrow-left me-1" />
          Danh sách
        </Link>
        <span className="cb-so-cont fs-4 ms-1">{hs.soCont}</span>
        <NhanTrangThai giaTri={hs.trangThai} />
        {hs.nhietDo != null && (
          <span className="cb-lanh">
            <i className="bi bi-thermometer-snow" /> {soDo(hs.nhietDo)}°C
          </span>
        )}
        <div className="ms-auto d-flex flex-wrap gap-2">
          <Link className="btn btn-sm btn-primary" to={duongEir(hs.maCont)} target="_blank" rel="noopener">
            <i className="bi bi-printer me-1" />
            In phiếu EIR
          </Link>
          <Link className="btn btn-sm btn-outline-primary" to={duongForm(hs.maCont)} state={{ quayVe: '/admin/containers/hoso/' + hs.maCont }}>
            <i className="bi bi-pencil me-1" />
            Sửa
          </Link>
          {buoc < 4 && (
            <button type="button" className="btn btn-sm btn-outline-success" disabled={dangGiao} onClick={() => giaoKhach(hs)}>
              <i className="bi bi-box-arrow-right me-1" />
              Gate out
            </button>
          )}
        </div>
      </div>

      <div className="card sp-the mb-3">
        <div className="card-body py-3">
          <VongDoi buoc={buoc} />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-7">
          <TheNoiDung tieuDe="Thông tin container" icon="bi-info-circle">
            <table className="cb-bang-tt">
              <tbody>
                <tr>
                  <th scope="row">Loại / tình trạng</th>
                  <td>
                    {hs.loaiCont} · {hs.tinhTrang || '—'}
                  </td>
                </tr>
                <tr>
                  <th scope="row">Hàng hóa</th>
                  <td>
                    {hs.hangHoa || '—'}
                    {hs.trongLuong != null && <span className="text-body-secondary"> · {dinhDangSo(hs.trongLuong, 2)} tấn</span>}
                  </td>
                </tr>
                <tr>
                  <th scope="row">Khách hàng</th>
                  <td>
                    {hs.khachHang ? (
                      <>
                        <span className="fw-semibold">{hs.khachHang.tenCongTy}</span>
                        <div className="small text-body-secondary">
                          MST {hs.khachHang.maSoThue || '—'}
                          {hs.khachHang.soDienThoai && <> · {hs.khachHang.soDienThoai}</>}
                        </div>
                      </>
                    ) : (
                      '—'
                    )}
                  </td>
                </tr>
                {hs.hangTau && (
                  <tr>
                    <th scope="row">Hãng tàu (chủ vỏ)</th>
                    <td>{hs.hangTau}</td>
                  </tr>
                )}
                <tr>
                  <th scope="row">Vị trí bãi</th>
                  <td>
                    {hs.viTriBai ? <span className="cb-vi-tri">{hs.viTriBai}</span> : <span className="text-body-secondary">chưa gán</span>}
                    <Link className="btn btn-sm btn-outline-secondary py-0 ms-2" to="/admin/containers/yardmap">
                      <i className="bi bi-grid-3x3-gap me-1" />
                      Sơ đồ bãi
                    </Link>
                  </td>
                </tr>
                <tr>
                  <th scope="row">Chuyến nhập</th>
                  <td>
                    <TenChuyen c={hs.chuyenNhap} />
                  </td>
                </tr>
                <tr>
                  <th scope="row">Chuyến xuất</th>
                  <td>
                    <TenChuyen c={hs.chuyenXuat} />
                  </td>
                </tr>
                <tr>
                  <th scope="row">Vào bãi → ra bãi</th>
                  <td>
                    {dinhDangNgayGio(hs.ngayVaoBai)} → {hs.ngayRaBai ? dinhDangNgayGio(hs.ngayRaBai) : <span className="text-body-secondary">đang trong bãi</span>}
                  </td>
                </tr>
              </tbody>
            </table>
          </TheNoiDung>

          <TheNoiDung
            tieuDe="Đồng hồ lưu bãi"
            icon="bi-stopwatch"
            phai={
              dh.dangChay ? (
                <NhanTrangThai giaTri="đang chạy" mau="warn" icon="bi-broadcast" />
              ) : (
                <NhanTrangThai giaTri="đã dừng" mau="off" />
              )
            }
          >
            <div className="d-flex flex-wrap align-items-end justify-content-between gap-2 mb-2">
              <div>
                <div className="small text-body-secondary">Tổng phí{dh.dangChay ? ' (tạm tính)' : ''}</div>
                <div className="cb-tien-lon">{dinhDangTien(dh.tongTien)}</div>
              </div>
              <div className="text-end small text-body-secondary">
                <div>
                  <span className="fs-5 fw-bold text-body">{dh.tongNgay}</span> ngày lưu bãi
                </div>
                <div>miễn phí {dh.ngayMienPhi} ngày đầu</div>
              </div>
            </div>

            <table className="cb-bang-tt mb-2">
              <tbody>
                <tr>
                  <th scope="row">Biểu giá</th>
                  <td>
                    {dh.tenBieuGia} — <span className="fw-semibold">{dinhDangTien(dh.donGiaNgay)}</span>/cont/ngày
                  </td>
                </tr>
                <tr>
                  <th scope="row">Phí lưu bãi</th>
                  <td>
                    {dh.ngayTinhPhi} ngày × {dinhDangTien(dh.donGiaNgay)} = <span className="fw-semibold">{dinhDangTien(dh.phiLuuBai)}</span>
                  </td>
                </tr>
                {dh.phiDien > 0 && (
                  <tr>
                    <th scope="row">Điện lạnh</th>
                    <td>
                      {dh.soNgayDien} ngày × {dinhDangTien(dh.donGiaDien)} = <span className="fw-semibold">{dinhDangTien(dh.phiDien)}</span>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            {hs.phieu ? (
              <div className="alert alert-success py-2 mb-2 small d-flex flex-wrap align-items-center gap-2">
                <i className="bi bi-receipt" />
                <span>
                  Đã chốt phiếu{' '}
                  <Link className="fw-bold" to={hs.phieu.duongDan}>
                    {hs.phieu.soPhieu}
                  </Link>{' '}
                  — {dinhDangTien(hs.phieu.tongTien)}
                </span>
                <NhanTrangThai giaTri={hs.phieu.trangThai} />
              </div>
            ) : (
              dh.tongTien > 0 && (
                <button type="button" className="btn btn-warning w-100 mb-2 fw-semibold" disabled={dangChay} onClick={chotPhieu}>
                  {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-receipt me-1" />}
                  Chốt thành phiếu thu {dinhDangTien(dh.tongTien)}
                </button>
              )
            )}

            <div className="small text-body-secondary">
              {dh.dienGiai} Tính đến {dinhDangNgayGio(hs.tinhDenLuc)} · <Link to="/admin/luubai">công nợ lưu bãi</Link>
            </div>
          </TheNoiDung>
        </div>

        <div className="col-lg-5">
          <DongThoiGian ds={hs.dongThoiGian || []} />

          <TheNoiDung tieuDe="Nhật ký thao tác" icon="bi-journal-text">
            {!hs.nhatKy?.length ? (
              <div className="small text-body-secondary">Chưa có thao tác nào ghi nhận với container này.</div>
            ) : (
              hs.nhatKy.map((n) => (
                <div className="cb-nhat-ky" key={n.maNK}>
                  <span className="text-body-secondary text-nowrap">{gioNgayNgan(n.thoiGian)}</span>
                  <span className={'badge text-bg-' + (['danger', 'warning', 'info', 'secondary'].includes(n.mauLoai) ? n.mauLoai : 'secondary')}>{n.loai}</span>
                  <span className="flex-grow-1">
                    {n.noiDung}
                    {n.taiKhoan && <span className="text-body-secondary"> · {n.taiKhoan}</span>}
                  </span>
                </div>
              ))
            )}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}

/** Ho so container 360° (thay Admin/Containers/HoSo/{id}). Doi id -> dung lai tu dau (bo loc dong thoi gian ve "Tat ca"). */
export default function HoSoContainer() {
  const { id } = useParams()
  return <NoiDungHoSo key={id} id={id} />
}
