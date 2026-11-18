import { Link } from 'react-router-dom'
import { duongDanAnh } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { dinhDangNgay, dinhDangTien, gioNgayNgan } from '../../utils/dinhDang'
import DauTrangKhach from './DauTrangKhach'
import './dich-vu-khach.css'

const BUOC_LENH = ['Chờ xe', 'Đã vào cổng', 'Hoàn tất']

/** Mau canh bao theo do lech nhiet do cont lanh (giong nguong cua ban MVC) */
function lopNhietDo(lech) {
  if (lech == null) return 'text-body-secondary'
  if (lech >= 6) return 'text-danger'
  if (lech >= 3) return 'text-warning'
  return 'text-success'
}

function AnhHang({ href, src, alt, dong1, dong2 }) {
  return (
    <a className="dvk-anh" href={href} target="_blank" rel="noreferrer">
      <figure className="mb-0">
        <img src={src} alt={alt} loading="lazy" />
        <figcaption>
          {dong1}
          <br />
          {dong2}
        </figcaption>
      </figure>
    </a>
  )
}

/** /hangcuatoi — hang &amp; lenh cua khach (thay Views/HangCuaToi/Index.cshtml), them tien luu bai dang chay. */
export default function HangCuaToi() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/hang-cua-toi')

  const COT_CONT = [
    { khoa: 'so', ten: 'Số cont', hien: (c) => <span className="fw-semibold">{c.soCont}</span> },
    { khoa: 'loai', ten: 'Loại', an: 'md', hien: (c) => c.loaiCont || '—' },
    { khoa: 'hang', ten: 'Hàng hóa', an: 'lg', hien: (c) => c.hangHoa || '—' },
    {
      khoa: 'viTri',
      ten: 'Vị trí bãi',
      hien: (c) => (c.viTriBai ? <span className="badge text-bg-light border">{c.viTriBai}</span> : '—'),
    },
    {
      khoa: 'nhiet',
      ten: 'Nhiệt độ ❄',
      an: 'md',
      hien: (c) =>
        c.nhietDoCai == null ? (
          '—'
        ) : (
          <>
            <span className="text-info small">cài {c.nhietDoCai}°C</span>
            {c.nhietDoDo != null && (
              <>
                <span className={'fw-bold small ms-1 ' + lopNhietDo(c.lechNhietDo)}>· đo {c.nhietDoDo}°C</span>
                <div className="text-body-secondary" style={{ fontSize: '.68rem' }}>
                  lúc {gioNgayNgan(c.doLuc)}
                </div>
              </>
            )}
          </>
        ),
    },
    { khoa: 'tt', ten: 'Trạng thái', hien: (c) => <NhanTrangThai giaTri={c.trangThai} /> },
    { khoa: 'vao', ten: 'Vào bãi', an: 'lg', hien: (c) => dinhDangNgay(c.ngayVaoBai) },
    {
      khoa: 'phi',
      ten: 'Phí lưu bãi',
      canPhai: true,
      hien: (c) => (
        <>
          <div className={'fw-semibold' + (c.tongPhiLuuBai > 0 ? ' text-warning-emphasis' : '')}>{dinhDangTien(c.tongPhiLuuBai)}</div>
          <div className="text-body-secondary" style={{ fontSize: '.68rem' }} title={c.dienGiaiPhi}>
            {c.phiDangChay ? 'đang chạy · ' : ''}
            {c.tongNgayLuuBai} ngày
          </div>
        </>
      ),
    },
    {
      khoa: 'thaoTac',
      ten: '',
      canPhai: true,
      rong: 90,
      hien: (c) => (
        <Link className="btn btn-sm btn-outline-primary" to={'/tracking?so=' + encodeURIComponent(c.soCont || '')} title={'Tra cứu ' + c.soCont}>
          Tra cứu
        </Link>
      ),
    },
  ]

  const COT_CT = [
    { khoa: 'so', ten: 'Số chứng từ', hien: (t) => <span className="fw-semibold small">{t.soChungTu}</span> },
    { khoa: 'loai', ten: 'Loại', hien: (t) => <span className="badge text-bg-light border">{t.tenLoai || t.loaiCT}</span> },
    { khoa: 'cont', ten: 'Cont / xe', an: 'md', hien: (t) => [t.soCont, t.bienSoXe].filter(Boolean).join(' · ') || '—' },
    { khoa: 'mo', ten: 'Mô tả', an: 'lg', hien: (t) => <span className="small text-body-secondary">{t.moTa}</span> },
    { khoa: 'ngay', ten: 'Ngày lập', hien: (t) => gioNgayNgan(t.ngayLap) },
    {
      khoa: 'hash',
      ten: 'Hash ⛓',
      hien: (t) => (
        <span className="small font-monospace" title={t.hashSHA256 || ''}>
          {t.hashSHA256 ? t.hashSHA256.slice(0, 10) + '…' : '—'}
        </span>
      ),
    },
  ]

  return (
    <>
      <DauTrangKhach icon="bi-boxes" tieuDe="Hàng & lệnh của tôi" duongDan={[{ ten: 'Hàng & lệnh của tôi' }]} />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />

        <div className="row g-3 mb-3">
          <div className="col-6 col-lg-3">
            <TheChiSo icon="bi-box-seam" mau="xanh" so={d?.tongCont ?? '—'} nhan="Container của tôi" />
          </div>
          <div className="col-6 col-lg-3">
            <TheChiSo icon="bi-geo-alt" mau="la" so={d?.trongBai ?? '—'} nhan="Đang nằm trong bãi" />
          </div>
          <div className="col-6 col-lg-3">
            <TheChiSo icon="bi-truck" mau="cam" so={d?.lenhChoXe ?? '—'} nhan="Lệnh đang chờ xe" />
          </div>
          <div className="col-6 col-lg-3">
            <TheChiSo icon="bi-cash-stack" mau="do" so={dinhDangTien(d?.tienLuuBaiDangChay ?? 0)} nhan="Phí lưu bãi đang chạy" nho />
          </div>
        </div>

        <TheNoiDung
          tieuDe="Container của tôi"
          icon="bi-box-seam"
          khongDem
          phai={<span className="small text-body-secondary">Miễn {d?.ngayMienPhiLuuBai ?? 5} ngày lưu bãi đầu tiên</span>}
        >
          <BangDuLieu
            cot={COT_CONT}
            dong={d?.container}
            khoaDong="maCont"
            dangTai={dangTai}
            rong="Chưa có container nào gắn với tài khoản của bạn."
          />
        </TheNoiDung>

        <TheNoiDung tieuDe="Lệnh giao nhận của tôi" icon="bi-truck">
          {!d?.lenh?.length ? (
            <div className="text-center text-body-secondary py-3">
              Chưa có lệnh giao nhận nào. Liên hệ cảng để đăng ký lệnh trước khi điều xe.
            </div>
          ) : (
            <>
              {d.lenh.map((l) => (
                <div key={l.maLenh} className="border rounded-3 p-3 mb-2">
                  <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
                    <div className="min-w-0">
                      <span className="fw-bold">{l.soLenh}</span>
                      <span className="small text-body-secondary ms-2">
                        {l.tenLoaiLenh}
                        {l.soCont ? ' · ' + l.soCont : ''}
                      </span>
                      <div className="small text-body-secondary">
                        Xe {l.bienSoXe} · hẹn {gioNgayNgan(l.henTuLuc)} → {gioNgayNgan(l.henDenLuc)} · PIN tài xế: <b>{l.maPin}</b>
                      </div>
                    </div>
                    {l.buoc === 0 ? (
                      <NhanTrangThai giaTri={l.trangThai} />
                    ) : (
                      <div className="d-flex align-items-center gap-1 small flex-wrap">
                        {BUOC_LENH.map((b, i) => (
                          <span
                            key={b}
                            className={'badge rounded-pill ' + (l.buoc >= i + 1 ? 'text-bg-success' : 'text-bg-light border text-body-secondary')}
                          >
                            {i + 1}. {b}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              ))}
              <div className="small text-body-secondary">
                Đưa <b>số lệnh + mã PIN</b> cho tài xế để check-in online tại <Link to="/taixe">Cổng tài xế</Link> — xe được ưu tiên xử lý khi
                tới cổng. Xem mật độ cổng trực tiếp tại <Link to="/cong">Tình trạng cổng</Link>.
              </div>
            </>
          )}
        </TheNoiDung>

        <TheNoiDung tieuDe="Ảnh hàng của tôi — hệ thống AI chụp tự động" icon="bi-camera">
          <div className="small text-body-secondary mb-3">
            Camera AI tại cổng (ALPR) chụp xe chở hàng của bạn lúc ra vào; trạm kiểm đếm chụp khi soi container.
          </div>
          {!d?.anhCong?.length && !d?.anhMedia?.length ? (
            <div className="text-center text-body-secondary py-3">
              Chưa có ảnh nào — ảnh sẽ xuất hiện tự động khi xe của bạn qua cổng hoặc container được kiểm đếm.
            </div>
          ) : (
            <div className="d-flex flex-wrap gap-2">
              {(d.anhCong || []).map((a) => (
                <AnhHang
                  key={'x' + a.maXV}
                  href={duongDanAnh(a.anhXe)}
                  src={duongDanAnh(a.anhXe)}
                  alt={'Ảnh xe ' + a.bienSo + ' tại cổng'}
                  dong1={`${a.bienSo} · ${a.huongDi || ''} cổng`}
                  dong2={gioNgayNgan(a.thoiGian)}
                />
              ))}
              {(d.anhMedia || []).map((m) => (
                <AnhHang
                  key={'m' + m.maMD}
                  href={duongDanAnh(m.duongDan)}
                  src={duongDanAnh(m.duongDan)}
                  alt={m.moTa || 'Ảnh hàng do AI chụp'}
                  dong1={m.tenNguon}
                  dong2={gioNgayNgan(m.thoiGian)}
                />
              ))}
            </div>
          )}
        </TheNoiDung>

        <TheNoiDung
          tieuDe="Chứng từ điện tử của tôi"
          icon="bi-file-earmark-lock2"
          khongDem
          phai={<span className="badge text-bg-dark">⛓ chuỗi khối</span>}
          chan={
            <span className="small text-body-secondary">
              Mỗi chứng từ được niêm phong bằng chuỗi hash SHA-256 nối tiếp nhau (mô hình eB/L) — không ai sửa lén được nội dung sau khi phát
              hành.
            </span>
          }
        >
          <BangDuLieu cot={COT_CT} dong={d?.chungTu} khoaDong="maCT" dangTai={dangTai} rong="Chưa có chứng từ nào." />
        </TheNoiDung>
      </div>
    </>
  )
}
