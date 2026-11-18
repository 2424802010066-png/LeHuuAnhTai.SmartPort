import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { duongDanAnh, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { dinhDangNgayGio, dinhDangSo, dinhDangTien, gioNgayNgan } from '../../utils/dinhDang'
import { AnhNho, BienSoXe, HopAnhXe, NhanHuong, NhanLenh, SoCont, gioGiayNgay, khungGio, loaiLenhNgan } from './dungChung'
import './cong.css'

const KHOANG = [
  { gt: 7, ten: '7 ngày qua' },
  { gt: 30, ten: '30 ngày qua' },
  { gt: 90, ten: '90 ngày qua' },
  { gt: 365, ten: '1 năm qua' },
]

/**
 * HO SO XE · TAI XE 360° (thay Admin/Gate/HoSoXe): go MOT bien so la ra toan bo lich su —
 * luot qua cong, container da cho, lenh giao nhan, phieu can, tai xe va dong thoi gian.
 * Dia chi giu nguyen ban MVC: /admin/gate/hosoxe?bienSo=51C-638.72
 */
export default function HoSoXe() {
  const [loc, datLoc] = useBoLoc({ bienSo: '', soNgay: 90 })
  const [chu, setChu] = useState(loc.bienSo)
  const [nhomDtg, setNhomDtg] = useState('')
  const [anhXem, setAnhXem] = useState(null)

  const bienSo = loc.bienSo.trim()
  const { duLieu: h, loi, dangTai, taiLai } = useApi(bienSo ? '/api/ho-so-xe' + taoQuery({ bienSo, soNgay: loc.soNgay }) : null)

  const cacNhom = useMemo(() => [...new Set((h?.dongThoiGian || []).map((s) => s.nhom))], [h])
  const dtg = useMemo(() => (h?.dongThoiGian || []).filter((s) => !nhomDtg || s.nhom === nhomDtg), [h, nhomDtg])

  function traCuu(e) {
    e.preventDefault()
    setNhomDtg('')
    datLoc({ bienSo: chu.trim().toUpperCase() })
  }

  const COT_LUOT = [
    { khoa: 'gio', ten: 'Thời gian', hien: (x) => <span className="fw-semibold small">{gioGiayNgay(x.thoiGian)}</span> },
    { khoa: 'anh', ten: 'Ảnh', rong: 74, hien: (x) => <AnhNho anh={x.anh} bienSo={h?.bienSo} moAnh={(a) => setAnhXem({ ...a, luc: x.thoiGian })} /> },
    { khoa: 'huong', ten: 'Hướng', hien: (x) => <NhanHuong huong={x.huongDi} /> },
    { khoa: 'cong', ten: 'Cổng', an: 'md', hien: (x) => x.cong || '—' },
    { khoa: 'cont', ten: 'Container', hien: (x) => <SoCont so={x.soCont} /> },
    { khoa: 'loai', ten: 'Loại xe', an: 'lg', hien: (x) => <span className="small">{x.loaiXe || '—'}</span> },
    { khoa: 'ocr', ten: 'Tin cậy', canPhai: true, an: 'lg', hien: (x) => (x.doTinCay != null ? dinhDangSo(x.doTinCay, 1) + '%' : '—') },
    { khoa: 'phi', ten: 'Phí cổng', canPhai: true, hien: (x) => (x.phiCong ? dinhDangTien(x.phiCong) : '—') },
  ]

  const COT_LENH = [
    {
      khoa: 'so',
      ten: 'Số lệnh',
      hien: (l) => (
        <Link className="cg-ma text-decoration-none" to={'/admin/gatepass/in/' + l.maLenh} title="Mở bản in lệnh">
          {l.soLenh}
        </Link>
      ),
    },
    { khoa: 'loai', ten: 'Loại', hien: (l) => <span className="small">{loaiLenhNgan(null, l.tenLoaiLenh)}</span> },
    { khoa: 'cont', ten: 'Container', hien: (l) => <SoCont so={l.soCont} /> },
    { khoa: 'hen', ten: 'Khung giờ hẹn', an: 'md', hien: (l) => <span className="small">{khungGio(l.henTuLuc, l.henDenLuc)}</span> },
    { khoa: 'tt', ten: 'Trạng thái', canPhai: true, hien: (l) => <NhanLenh trangThai={l.trangThai} /> },
  ]

  const COT_TX = [
    { khoa: 'ten', ten: 'Tài xế', hien: (t) => <span className="fw-semibold">{t.hoTen || '—'}</span> },
    { khoa: 'dt', ten: 'Điện thoại', hien: (t) => t.dienThoai || '—' },
    {
      khoa: 'gplx',
      ten: 'GPLX',
      an: 'md',
      hien: (t) =>
        t.soGPLX ? (
          <span className="small">
            {t.hangGPLX ? 'Hạng ' + t.hangGPLX + ' · ' : ''}
            {t.gplxHetHan ? (
              <NhanTrangThai giaTri="Hết hạn" mau="do" />
            ) : t.gplxSapHetHan ? (
              <NhanTrangThai giaTri="Sắp hết hạn" mau="warn" />
            ) : (
              <NhanTrangThai giaTri="Còn hạn" mau="ok" />
            )}
          </span>
        ) : (
          <span className="text-muted small">chưa quét giấy tờ</span>
        ),
    },
    { khoa: 'nguon', ten: 'Nguồn', canPhai: true, hien: (t) => <span className="small text-body-secondary">{t.nguon}</span> },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <TheNoiDung lop="mb-3">
        <form className="row g-2 align-items-end" onSubmit={traCuu}>
          <div className="col-md-5">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-bs">
              BIỂN SỐ XE
            </label>
            <input
              id="cg-bs"
              className="form-control form-control-lg sp-o-nhap-bien"
              value={chu}
              onChange={(e) => setChu(e.target.value)}
              placeholder="51C-638.72"
              maxLength={20}
              autoFocus
            />
          </div>
          <div className="col-md-3">
            <label className="form-label small fw-semibold mb-1" htmlFor="cg-ngay">
              KHOẢNG THỜI GIAN
            </label>
            <select id="cg-ngay" className="form-select" value={loc.soNgay} onChange={(e) => datLoc({ soNgay: Number(e.target.value) })}>
              {KHOANG.map((k) => (
                <option key={k.gt} value={k.gt}>
                  {k.ten}
                </option>
              ))}
            </select>
          </div>
          <div className="col-md-4 d-flex gap-2">
            <button className="btn btn-primary flex-fill" type="submit">
              <i className="bi bi-search me-1" />
              Tra cứu
            </button>
            <Link className="btn btn-outline-secondary" to="/admin/gate" title="Về màn hình cổng thông minh">
              <i className="bi bi-upc-scan" />
            </Link>
          </div>
        </form>
      </TheNoiDung>

      {!bienSo ? (
        <TheNoiDung>
          <div className="text-center text-muted py-5">
            <i className="bi bi-truck d-block mb-2" style={{ fontSize: '2.6rem', opacity: 0.35 }} />
            Nhập biển số xe ở ô trên để xem hồ sơ.
            <div className="small mt-1">
              Mẹo: ở bất kỳ trang nào cũng bấm được <kbd>Ctrl</kbd> + <kbd>K</kbd> rồi gõ biển số.
            </div>
          </div>
        </TheNoiDung>
      ) : dangTai && !h ? (
        <TheNoiDung>
          <div className="text-center text-muted py-5">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang dựng hồ sơ xe {bienSo}…
          </div>
        </TheNoiDung>
      ) : !h ? null : !h.coDuLieu ? (
        <TheNoiDung>
          <div className="text-center text-muted py-5">
            <i className="bi bi-emoji-neutral d-block mb-2" style={{ fontSize: '2.6rem', opacity: 0.35 }} />
            Không có dữ liệu nào của xe <b>{h.bienSo}</b> trong {h.soNgay} ngày qua.
            <div className="small mt-1">Thử mở rộng khoảng thời gian, hoặc kiểm tra lại biển số.</div>
          </div>
        </TheNoiDung>
      ) : (
        <>
          <div className="d-flex align-items-center flex-wrap gap-2 mb-3">
            <BienSoXe bienSo={h.bienSo} lienKet={false} />
            <span className="text-body-secondary small">
              {h.lanDau ? 'Ghi nhận từ ' + gioNgayNgan(h.lanDau) : ''}
              {h.bienSo !== bienSo && ' · khớp với biển số bạn gõ (' + bienSo + ')'}
            </span>
          </div>

          <div className="row g-3 mb-3">
            <div className="col-6 col-lg-3">
              <TheChiSo
                icon={h.dangTrongCang ? 'bi-box-arrow-in-right' : 'bi-box-arrow-right'}
                mau={h.dangTrongCang ? 'la' : 'xanh'}
                nho
                so={h.dangTrongCang ? 'Trong cảng' : 'Đã ra cảng'}
                nhan={h.lanCuoi ? 'lần cuối ' + gioNgayNgan(h.lanCuoi) : 'chưa có lượt'}
              />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-arrow-left-right" mau="xanh-ngoc" so={h.soLuotVao + ' / ' + h.soLuotRa} nhan={'lượt vào / ra ' + h.soNgay + ' ngày'} />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-cash-coin" mau="cam" nho so={dinhDangTien(h.tongPhiCong)} nhan="phí cổng đã thu" />
            </div>
            <div className="col-6 col-lg-3">
              <TheChiSo icon="bi-clipboard-check" mau={h.soLenhDangMo > 0 ? 'cam' : 'la'} so={h.soLenhDangMo} nhan="lệnh đang mở" />
            </div>
          </div>

          <div className="row g-3">
            <div className="col-lg-5">
              {h.anhGanNhat && (
                <TheNoiDung tieuDe="Ảnh chụp gần nhất tại cổng" icon="bi-camera">
                  <img src={duongDanAnh(h.anhGanNhat)} alt={'Ảnh xe ' + h.bienSo} className="cg-anh-lon" />
                  <div className="small text-body-secondary mt-1">
                    Ảnh gốc camera cổng lưu lúc {dinhDangNgayGio(h.anhGanNhatLuc)} — dùng đối chiếu khi có khiếu nại.
                  </div>
                </TheNoiDung>
              )}

              <TheNoiDung
                tieuDe="Lệnh giao nhận của xe"
                icon="bi-qr-code-scan"
                khongDem
                phai={
                  <Link className="btn btn-sm btn-outline-primary" to={'/admin/gatepass/form?bienSo=' + encodeURIComponent(h.bienSo)}>
                    <i className="bi bi-plus-lg me-1" />
                    Tạo lệnh
                  </Link>
                }
              >
                <BangDuLieu cot={COT_LENH} dong={h.lenh} khoaDong="maLenh" rong="Xe chưa có lệnh giao nhận nào." />
              </TheNoiDung>

              <TheNoiDung tieuDe="Tài xế đã đi xe này" icon="bi-person-badge" khongDem>
                <BangDuLieu cot={COT_TX} dong={h.taiXe} khoaDong={(t, i) => t.maTX ?? 'ten' + i} rong="Chưa ghi nhận tài xế nào." />
              </TheNoiDung>

              {h.contDaCho?.length > 0 && (
                <TheNoiDung tieuDe="Container xe này đã chở" icon="bi-box-seam">
                  <div className="d-flex flex-wrap gap-2">
                    {h.contDaCho.map((sc) => (
                      <SoCont key={sc} so={sc} nho={false} />
                    ))}
                  </div>
                  <div className="small text-body-secondary mt-2">Bấm một số container để mở hồ sơ 360° của nó.</div>
                </TheNoiDung>
              )}

              <TheNoiDung tieuDe="Trạm cân" icon="bi-speedometer">
                <div className="d-flex justify-content-between">
                  <span className="text-body-secondary">Số phiếu cân</span>
                  <b>{h.soPhieuCan}</b>
                </div>
                <div className="d-flex justify-content-between">
                  <span className="text-body-secondary">Tổng khối lượng hàng đã cân</span>
                  <b>{dinhDangSo(h.tongHangKg)} kg</b>
                </div>
                <div className="d-flex justify-content-between">
                  <span className="text-body-secondary">Độ tin cậy OCR trung bình</span>
                  <b>{h.ocrTrungBinh != null ? dinhDangSo(h.ocrTrungBinh, 1) + '%' : '—'}</b>
                </div>
              </TheNoiDung>
            </div>

            <div className="col-lg-7">
              <TheNoiDung
                tieuDe={'Dòng thời gian của xe ' + h.bienSo}
                icon="bi-clock-history"
                phai={<span className="badge text-bg-light border">{h.dongThoiGian.length} sự kiện</span>}
              >
                <div className="d-flex flex-wrap gap-1 mb-3">
                  <button type="button" className={'btn btn-sm ' + (nhomDtg ? 'btn-outline-secondary' : 'btn-secondary')} onClick={() => setNhomDtg('')}>
                    Tất cả
                  </button>
                  {cacNhom.map((n) => (
                    <button key={n} type="button" className={'btn btn-sm ' + (nhomDtg === n ? 'btn-secondary' : 'btn-outline-secondary')} onClick={() => setNhomDtg(n)}>
                      {n}
                    </button>
                  ))}
                </div>
                <div className="cg-dtg">
                  {dtg.length === 0 ? (
                    <div className="text-muted small">Không có sự kiện nào trong nhóm này.</div>
                  ) : (
                    dtg.map((s, i) => (
                      <div className="cg-dtg-muc" key={s.luc + i}>
                        <span className="cg-dtg-cham" style={{ background: s.mau || '#64748b' }}>
                          <i className={'bi ' + (s.icon || 'bi-dot')} />
                        </span>
                        <div className="cg-dtg-gio">{dinhDangNgayGio(s.luc)}</div>
                        <div className="cg-dtg-td">{s.duongDan ? <Link to={s.duongDan}>{s.tieuDe}</Link> : s.tieuDe}</div>
                        {s.moTa && <div className="cg-dtg-mt">{s.moTa}</div>}
                      </div>
                    ))
                  )}
                </div>
              </TheNoiDung>

              <TheNoiDung
                tieuDe="Lượt qua cổng gần đây"
                icon="bi-upc-scan"
                khongDem
                phai={
                  <Link className="btn btn-sm btn-outline-secondary" to={'/admin/gate/history?bienSo=' + encodeURIComponent(h.bienSo) + '&ngay=365'}>
                    Nhật ký đầy đủ
                  </Link>
                }
              >
                <BangDuLieu cot={COT_LUOT} dong={h.luotGanDay} khoaDong="maXV" rong="Chưa có lượt qua cổng nào." />
              </TheNoiDung>
            </div>
          </div>
        </>
      )}

      <HopAnhXe anh={anhXem ? { ...anhXem, bienSo: h?.bienSo } : null} dong={() => setAnhXem(null)} />
    </>
  )
}
