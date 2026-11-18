import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { duongDanAnh } from '../../api/client'
import { useApi } from '../../api/useApi'
import BaoLoi from '../../components/BaoLoi'
import DangTai from '../../components/DangTai'
import NhanTrangThai from '../../components/ui/NhanTrangThai'
import { dinhDangNgay, dinhDangNgayGio, dinhDangTien } from '../../utils/dinhDang'
import SoDem from './SoDem'
import { useHienDan } from './hieuUng'

const ANH_HERO = '/Content/img/smartport-cinematic-hero.jpg'
const ANH_TAU = '/Content/img/tau-hero.jpg'

const MA_GOI_Y = ['MSCU2201417', 'MAEU3305891', 'MSCU2299876']

const ICON_NHOM = {
  'Bốc xếp': 'bi-boxes',
  'Lưu bãi': 'bi-stack',
  'Kho bãi': 'bi-archive',
  'Vận chuyển': 'bi-truck',
}

const AI = [
  { icon: 'bi-robot', ten: 'PortBot 24/7', mo: 'Hỏi container, lịch tàu, bảng giá bằng tiếng Việt.', chat: true },
  { icon: 'bi-calculator', ten: 'Ước tính chi phí', mo: 'Khai lô hàng — ra ngay tổng phí và cách tiết kiệm.', duong: '/cost' },
  { icon: 'bi-upc-scan', ten: 'OCR số container', mo: 'Chụp vỏ cont, máy đọc mã và tra cứu ngay.', duong: '/tracking' },
  { icon: 'bi-graph-up-arrow', ten: 'Dự báo sản lượng', mo: 'Mạng nơ-ron dự báo TEU 3 tháng tới.' },
  { icon: 'bi-bounding-box', ten: 'Phân bổ bến tàu', mo: 'Chấm điểm bến theo LOA, mớn nước, lịch trống.' },
  { icon: 'bi-camera-video', ten: 'Camera AI YOLOv10', mo: 'Nhận diện xe, người, biển số tại cổng.' },
]

const HOI_DAP = [
  {
    hoi: 'Làm sao tra cứu container của tôi đang ở đâu?',
    dap: 'Vào mục Tra cứu container, nhập số container (4 chữ + 7 số, VD: MSCU2201417) — hệ thống hiện trạng thái, vị trí bãi, hành trình và phí lưu bãi ước tính theo thời gian thực.',
  },
  {
    hoi: 'Phí lưu bãi tính thế nào? Có miễn phí không?',
    dap: 'Miễn phí 5 ngày đầu. Từ ngày thứ 6: 60.000₫/ngày với cont 20’ và 95.000₫/ngày với cont 40’. Cont lạnh tính thêm phí cắm điện 250.000₫/ngày từ ngày đầu.',
  },
  {
    hoi: 'Đặt dịch vụ qua ePort như thế nào?',
    dap: 'Đăng ký tài khoản (miễn phí) → Đăng nhập → Đặt dịch vụ → nhập số lượng, hệ thống tự tính tiền → Gửi yêu cầu. Khai thác duyệt trong giờ hành chính, tải hóa đơn Excel/PDF ngay trên web.',
  },
  {
    hoi: 'Cảng có làm việc cuối tuần và ban đêm không?',
    dap: 'Cổng cảng, bãi container và cầu bến hoạt động 24/7 kể cả lễ Tết. Khối văn phòng thương vụ làm việc 7h30–17h00 từ thứ 2 đến thứ 7.',
  },
  {
    hoi: 'Cần chứng từ gì để lấy hàng nhập ra khỏi cảng?',
    dap: 'Lệnh giao hàng (D/O) của hãng tàu, tờ khai hải quan đã thông quan, giấy giới thiệu và CCCD tài xế. Xe vào cổng được nhận diện tự động, xử lý trung bình dưới 5 phút.',
  },
]

/** Trang chu website cong khai (thay Views/Home/Index.cshtml). */
export default function TrangChu() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/cong-khai/trang-chu', { coToken: false })
  const [soCont, setSoCont] = useState('')
  const [hoiMo, setHoiMo] = useState(0)
  const dieuHuong = useNavigate()
  useHienDan([duLieu])

  if (loi) return <BaoLoi loi={loi} thuLai={taiLai} />
  if (dangTai || !duLieu) return <DangTai chu="Đang tải trang chủ…" />

  const anh = (khoa) => duongDanAnh(duLieu.anh?.[khoa])
  const dongTin = duLieu.dongTin?.length ? duLieu.dongTin : []

  const timCont = (e) => {
    e.preventDefault()
    const ma = soCont.trim().toUpperCase()
    dieuHuong(ma ? '/tracking?so=' + encodeURIComponent(ma) : '/tracking')
  }

  return (
    <>
      {dongTin.length > 0 && (
        <div className="ck-tin-chay">
          <div className="ck-tin-nhan">
            <span className="ck-cham" /> TRỰC TIẾP
          </div>
          <div className="ck-chay">
            <div className="ck-chay-day">
              {[...dongTin, ...dongTin].map((t, i) => (
                <span key={i}>{t}</span>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ---------- Hero ---------- */}
      <section className="ck-hero">
        <div className="ck-hero-nen" style={{ backgroundImage: 'url(' + duongDanAnh(ANH_HERO) + ')' }} />
        <div className="ck-hero-phu" />
        <div className="container">
          <div className="row align-items-center g-4">
            <div className="col-lg-6">
              <span className="ck-nhan-hero">
                <i className="bi bi-shield-check text-warning" /> CẢNG BIỂN THÔNG MINH 4.0
              </span>
              <h1>
                Quản lý cảng <span>Logistics Quốc tế</span>
                <br />
                nhanh — chính xác — tự động hóa
              </h1>
              <p className="mt-3">Số hóa toàn bộ điều hành cảng container: tra cứu tức thì, AI xếp dỡ, camera giám sát và trợ lý PortBot 24/7.</p>
              <div className="d-flex flex-wrap gap-2 my-4">
                <Link className="ck-nut-chinh px-3 py-2" to="/schedule">
                  <i className="bi bi-calendar2-week me-2" />
                  Lịch tàu cập cảng
                </Link>
                <Link className="ck-nut-vao px-3 py-2" to="/cost">
                  <i className="bi bi-calculator me-2" />
                  Ước tính phí AI
                </Link>
                <Link className="ck-nut-vao px-3 py-2" to="/services">
                  <i className="bi bi-tags me-2" />
                  Bảng giá dịch vụ
                </Link>
              </div>

              <form className="ck-tim" onSubmit={timCont}>
                <label className="form-label small fw-semibold text-white" htmlFor="ckSoCont">
                  <i className="bi bi-search me-1" />
                  Tra cứu hành trình container
                </label>
                <div className="d-flex gap-2">
                  <input
                    id="ckSoCont"
                    className="form-control"
                    placeholder="VD: MSCU2201417"
                    value={soCont}
                    maxLength={15}
                    onChange={(e) => setSoCont(e.target.value.toUpperCase())}
                  />
                  <button className="ck-nut-chinh px-3 flex-shrink-0" type="submit">
                    Tra cứu
                  </button>
                </div>
                <div className="d-flex flex-wrap align-items-center gap-2 mt-2">
                  <span className="small text-white-50">Gợi ý:</span>
                  {MA_GOI_Y.map((m) => (
                    <button key={m} type="button" className="ck-goi-y" onClick={() => setSoCont(m)}>
                      {m}
                    </button>
                  ))}
                </div>
              </form>
            </div>

            <div className="col-lg-6 d-none d-lg-block">
              <div className="ck-radar">
                <div className="ck-radar-dau">
                  <span className="text-info fw-bold">
                    <span className="ck-cham d-inline-block me-1" style={{ background: '#7CFC9A' }} /> AIS · LIVE RADAR
                  </span>
                  <span className="ms-auto text-white-50">10°30′N 107°01′E</span>
                </div>
                <div className="ck-radar-anh" style={{ backgroundImage: 'url(' + duongDanAnh(ANH_TAU) + ')' }}>
                  <div className="ck-quet" title="Radar hàng hải AIS" />
                </div>
                <div className="ck-hud">
                  <div>
                    <span>Tốc độ</span>
                    <strong>14.2 kts</strong>
                  </div>
                  <div>
                    <span>Hướng</span>
                    <strong>087° E</strong>
                  </div>
                  <div>
                    <span>Mớn nước</span>
                    <strong>12.5 m</strong>
                  </div>
                  <div>
                    <span>Sức chứa</span>
                    <strong>14.200 TEU</strong>
                  </div>
                </div>
                <div className="p-3 text-white">
                  <div className="d-flex justify-content-between align-items-center">
                    <div>
                      <div className="fw-bold">M/V SMARTPORT EXPRESS</div>
                      <div className="small text-white-50">IMO 9839272 · Tuyến Á – Âu (AEX)</div>
                    </div>
                    <NhanTrangThai giaTri="Đang làm hàng" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ---------- Chi so ---------- */}
      <section className="container ck-so-band">
        <div className="row g-3">
          {[
            { icon: 'bi-bounding-box', so: duLieu.soBen, nhan: 'Bến đang khai thác', them: '100% khả dụng' },
            { icon: 'bi-water', so: duLieu.tauDangCap, nhan: 'Tàu trong cảng', them: 'Đang làm hàng' },
            { icon: 'bi-box-seam', so: duLieu.contTrongBai, nhan: 'Container trong bãi', them: 'AI tối ưu vị trí' },
            { icon: 'bi-graph-up-arrow', so: duLieu.sanLuongNam, nhan: 'TEU thông qua năm nay', them: '+12,4% so cùng kỳ' },
          ].map((c, i) => (
            <div className="col-6 col-lg-3 ck-hien" data-cham={i * 80} key={c.nhan}>
              <div className="ck-so">
                <div className="ck-so-icon">
                  <i className={'bi ' + c.icon} />
                </div>
                <div className="ck-so-num">
                  <SoDem den={c.so} />
                </div>
                <div className="ck-so-nhan">{c.nhan}</div>
                <div className="ck-so-them">
                  <i className="bi bi-arrow-up-right me-1" />
                  {c.them}
                </div>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* ---------- Cong nghe AI ---------- */}
      <section className="ck-muc">
        <div className="container">
          <div className="text-center mb-4 ck-hien">
            <div className="ck-kicker">Trí tuệ nhân tạo</div>
            <h2 className="ck-tieu-de">AI chạy trong từng khâu vận hành</h2>
          </div>
          <div className="row g-3">
            {AI.map((a, i) => {
              const ruot = (
                <>
                  <div className="ck-dv-ico">
                    <i className={'bi ' + a.icon} />
                  </div>
                  <div className="fw-bold">{a.ten}</div>
                  <div className="small text-body-secondary">{a.mo}</div>
                </>
              )
              return (
                <div className="col-md-6 col-lg-4 ck-hien" data-cham={i * 60} key={a.ten}>
                  {a.duong ? (
                    <Link className="ck-the ck-dv d-block text-decoration-none text-body" to={a.duong}>
                      {ruot}
                    </Link>
                  ) : a.chat ? (
                    <button
                      type="button"
                      className="ck-the ck-dv d-block text-start w-100"
                      onClick={() => window.dispatchEvent(new Event('smartport:portbot'))}
                    >
                      {ruot}
                    </button>
                  ) : (
                    <div className="ck-the ck-dv">{ruot}</div>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      </section>

      {/* ---------- Dich vu ---------- */}
      {duLieu.dichVus.length > 0 && (
        <section className="ck-muc pt-0">
          <div className="container">
            <div className="d-flex flex-wrap justify-content-between align-items-end mb-4 ck-hien">
              <div>
                <div className="ck-kicker">Dịch vụ cảng</div>
                <h2 className="ck-tieu-de">Biểu phí đang áp dụng</h2>
              </div>
              <Link className="btn btn-outline-primary btn-sm" to="/services">
                Bảng giá đầy đủ <i className="bi bi-arrow-right ms-1" />
              </Link>
            </div>
            <div className="row g-3">
              {duLieu.dichVus.map((d, i) => (
                <div className="col-md-6 col-lg-4 ck-hien" data-cham={i * 60} key={d.maDV}>
                  <div className="ck-the ck-dv">
                    <div className="ck-dv-ico">
                      <i className={'bi ' + (ICON_NHOM[d.nhomDV] || 'bi-gear-wide-connected')} />
                    </div>
                    <div className="fw-bold">{d.tenDV}</div>
                    <div className="small text-body-secondary mb-2">{d.moTa}</div>
                    <div className="ck-gia">
                      {dinhDangTien(d.donGia)} <span className="small fw-normal text-body-secondary">/ {d.donViTinh}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* ---------- Lich tau ---------- */}
      <section className="ck-muc pt-0">
        <div className="container">
          <div className="d-flex flex-wrap justify-content-between align-items-end mb-4 ck-hien">
            <div>
              <div className="ck-kicker">Lịch khai thác</div>
              <h2 className="ck-tieu-de">Tàu sắp cập cảng</h2>
            </div>
            <Link className="btn btn-outline-primary btn-sm" to="/schedule">
              Toàn bộ lịch tàu <i className="bi bi-arrow-right ms-1" />
            </Link>
          </div>
          <div className="ck-bang ck-hien">
            <div className="table-responsive">
              <table className="table align-middle">
                <thead>
                  <tr>
                    <th>Chuyến</th>
                    <th>Tàu</th>
                    <th>ETA</th>
                    <th>ETD</th>
                    <th>Cầu bến</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {duLieu.chuyenSapToi.length === 0 && (
                    <tr>
                      <td colSpan={6} className="text-center py-4 text-body-secondary">
                        Đang cập nhật lịch trình các chuyến tàu tiếp theo…
                      </td>
                    </tr>
                  )}
                  {duLieu.chuyenSapToi.map((c) => (
                    <tr key={c.maChuyen}>
                      <td className="fw-bold text-primary">{c.maChuyenHieu}</td>
                      <td className="fw-semibold">{c.tenTau ?? '—'}</td>
                      <td>{dinhDangNgayGio(c.eta)}</td>
                      <td>{dinhDangNgayGio(c.etd)}</td>
                      <td>{c.tenBen ?? <span className="text-body-secondary">Chưa phân bến</span>}</td>
                      <td>
                        <NhanTrangThai giaTri={c.trangThai} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </section>

      {/* ---------- Dai so lieu ---------- */}
      <section className="ck-dai" style={{ backgroundImage: 'url(' + anh('head') + ')' }}>
        <div className="container">
          <div className="row text-center g-4">
            <div className="col-6 col-lg-3">
              <div className="ck-dai-so">
                <SoDem den={214} sau="+" />
              </div>
              <div className="ck-dai-nhan">Chuyến tàu mỗi quý</div>
            </div>
            <div className="col-6 col-lg-3">
              <div className="ck-dai-so">
                <SoDem den={2500} sau="+" />
              </div>
              <div className="ck-dai-nhan">Lượt xe container/ngày</div>
            </div>
            <div className="col-6 col-lg-3">
              <div className="ck-dai-so">
                <SoDem den={99} sau=",2%" />
              </div>
              <div className="ck-dai-nhan">Booking xử lý đúng hạn</div>
            </div>
            <div className="col-6 col-lg-3">
              <div className="ck-dai-so">24/7</div>
              <div className="ck-dai-nhan">Vận hành không nghỉ</div>
            </div>
          </div>
        </div>
      </section>

      {/* ---------- Thu vien anh ---------- */}
      <section className="ck-muc">
        <div className="container">
          <div className="text-center mb-4 ck-hien">
            <div className="ck-kicker">Khoảnh khắc tại cảng</div>
            <h2 className="ck-tieu-de">Hình ảnh thực tế khai thác</h2>
          </div>
          <div className="row g-3">
            {[
              { k: 'gal1', c: 'Tàu mẹ cập cảng' },
              { k: 'gal2', c: 'Cẩu STS làm hàng' },
              { k: 'gal3', c: 'Hoàng hôn trên cầu bến' },
              { k: 'gal4', c: 'Kho CFS đóng rút hàng' },
              { k: 'gal5', c: 'Đội xe vận chuyển' },
              { k: 'gal6', c: 'Bãi container quản lý bằng AI' },
            ].map((g, i) => (
              <div className="col-md-6 col-lg-4 ck-hien" data-cham={i * 60} key={g.k}>
                <div className="ck-anh" style={{ backgroundImage: 'url(' + anh(g.k) + ')' }} role="img" aria-label={g.c}>
                  <span>{g.c}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- Doi tac ---------- */}
      {duLieu.hangTaus.length > 0 && (
        <section className="ck-muc-nho">
          <div className="container text-center mb-3 ck-hien">
            <div className="ck-kicker">Đối tác hãng tàu</div>
            <h2 className="ck-tieu-de">Kết nối các hãng tàu hàng đầu thế giới</h2>
          </div>
          <div className="ck-chay">
            <div className="ck-chay-day">
              {[...duLieu.hangTaus, ...duLieu.hangTaus].map((h, i) => (
                <span className="ck-vien-pill" key={i}>
                  <span className="ck-scac">{h.maSCAC || '—'}</span>
                  {h.tenHangTau}
                </span>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* ---------- Tin tuc ---------- */}
      {duLieu.tinNoiBat.length > 0 && (
        <section className="ck-muc pt-0">
          <div className="container">
            <div className="d-flex flex-wrap justify-content-between align-items-end mb-4 ck-hien">
              <div>
                <div className="ck-kicker">Tin tức</div>
                <h2 className="ck-tieu-de">Hoạt động của cảng</h2>
              </div>
              <Link className="btn btn-outline-primary btn-sm" to="/news">
                Tất cả tin tức <i className="bi bi-arrow-right ms-1" />
              </Link>
            </div>
            <div className="row g-3">
              {duLieu.tinNoiBat.map((t, i) => (
                <div className="col-md-4 ck-hien" data-cham={i * 80} key={t.maTin}>
                  <article className="ck-the ck-tin">
                    <div className="ck-tin-anh" style={{ backgroundImage: 'url(' + duongDanAnh(t.anhUrl) + ')' }} />
                    <div className="p-3">
                      <div className="small text-body-secondary mb-2">
                        <i className="bi bi-calendar3 me-1" />
                        {dinhDangNgay(t.ngayDang)} · <i className="bi bi-eye me-1" />
                        {t.luotXem}
                      </div>
                      <h3>
                        <Link to={'/news/details/' + t.maTin}>{t.tieuDe}</Link>
                      </h3>
                    </div>
                  </article>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* ---------- Hoi dap ---------- */}
      <section className="ck-muc pt-0">
        <div className="container">
          <div className="text-center mb-4 ck-hien">
            <div className="ck-kicker">Hỏi đáp</div>
            <h2 className="ck-tieu-de">Câu hỏi thường gặp</h2>
          </div>
          <div className="row justify-content-center">
            <div className="col-lg-9">
              {HOI_DAP.map((h, i) => (
                <div className="ck-the mb-2 ck-hien" key={h.hoi}>
                  <button
                    type="button"
                    className="btn w-100 text-start d-flex align-items-center gap-2 fw-semibold py-3"
                    onClick={() => setHoiMo(hoiMo === i ? -1 : i)}
                    aria-expanded={hoiMo === i}
                  >
                    <i className={'bi ' + (hoiMo === i ? 'bi-dash-circle' : 'bi-plus-circle') + ' text-primary'} />
                    <span className="flex-grow-1">{h.hoi}</span>
                  </button>
                  {hoiMo === i && <div className="px-3 pb-3 text-body-secondary small">{h.dap}</div>}
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ---------- CTA ---------- */}
      <section className="container">
        <div className="ck-cta ck-hien">
          <div className="ck-cta-anh" style={{ backgroundImage: 'url(' + anh('cta') + ')' }} />
          <div className="row align-items-center g-3">
            <div className="col-lg-8">
              <h2 className="h4 fw-bold mb-1">
                <i className="bi bi-robot me-2" />
                Mở tài khoản doanh nghiệp — đặt dịch vụ trong 3 phút
              </h2>
              <p className="mb-0 small" style={{ color: '#c9dcef' }}>
                Booking trực tuyến, hóa đơn điện tử, theo dõi container và lệnh giao nhận ở một nơi.
              </p>
            </div>
            <div className="col-lg-4 text-lg-end">
              <Link className="ck-nut-chinh px-4 py-2 d-inline-block" to="/account/register">
                <i className="bi bi-person-plus me-2" />
                Đăng ký ngay
              </Link>
            </div>
          </div>
        </div>
      </section>
    </>
  )
}
