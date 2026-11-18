import { Link } from 'react-router-dom'
import { duongDanAnh } from '../../api/client'
import { useApi } from '../../api/useApi'
import BaoLoi from '../../components/BaoLoi'
import DangTai from '../../components/DangTai'
import NhanTrangThai from '../../components/ui/NhanTrangThai'
import { dinhDangSo } from '../../utils/dinhDang'
import SoDem from './SoDem'
import { useHienDan } from './hieuUng'

const MOC = [
  { nam: '2015', ten: 'Khởi công xây dựng', mo: 'Động thổ khu bến 120 ha, tổng vốn 6.800 tỷ đồng.' },
  { nam: '2017', ten: 'Đón chuyến tàu đầu tiên', mo: 'Tàu container 2.500 TEU cập bến B1, chính thức khai thác.' },
  { nam: '2020', ten: 'Vượt mốc 300.000 TEU/năm', mo: 'Hoàn thiện 5 bến, 15 cẩu STS, kết nối 30 tuyến dịch vụ.' },
  { nam: '2023', ten: 'Ra mắt ePort', mo: 'Số hóa toàn bộ quy trình đặt dịch vụ, tra cứu trực tuyến.' },
  { nam: '2025', ten: 'Ứng dụng AI vận hành', mo: 'Chatbot, điều độ bến, dự báo sản lượng, cảnh báo thông minh.' },
  { nam: '2026', ten: 'Hướng tới top 5 cảng khu vực', mo: 'Mục tiêu 600.000 TEU/năm, giảm 40% phát thải CO₂.', nay: true },
]

const NANG_LUC = [
  { so: 120, sau: ' ha', nhan: 'Diện tích khai thác' },
  { so: 600000, sau: '+', nhan: 'TEU thông qua mỗi năm' },
  { so: 50, sau: '+', nhan: 'Tuyến dịch vụ toàn cầu' },
  { so: 15, sau: '', nhan: 'Cẩu bờ STS' },
]

/** Trang gioi thieu cang (thay Views/Home/About.cshtml). */
export default function GioiThieu() {
  const { duLieu, loi, dangTai, taiLai } = useApi('/api/cong-khai/gioi-thieu', { coToken: false })
  useHienDan([duLieu])

  const anh = (khoa) => duongDanAnh(duLieu?.anh?.[khoa])

  return (
    <>
      <div className="ck-dau-muc">
        <div className="container">
          <h1>
            <i className="bi bi-anchor me-2" />
            Giới thiệu Cảng Quốc tế SmartPort
          </h1>
          <nav aria-label="Đường dẫn">
            <ol className="breadcrumb">
              <li className="breadcrumb-item">
                <Link to="/">Trang chủ</Link>
              </li>
              <li className="breadcrumb-item active" aria-current="page">
                Giới thiệu
              </li>
            </ol>
          </nav>
        </div>
      </div>

      <div className="container ck-muc">
        {loi && <BaoLoi loi={loi} thuLai={taiLai} />}
        {dangTai && !duLieu && <DangTai chu="Đang tải trang giới thiệu…" />}

        {duLieu && (
          <>
            <div className="row g-3 mb-4">
              {NANG_LUC.map((n, i) => (
                <div className="col-6 col-lg-3 ck-hien" data-cham={i * 70} key={n.nhan}>
                  <div className="ck-so">
                    <div className="ck-so-num">
                      <SoDem den={n.so} sau={n.sau} />
                    </div>
                    <div className="ck-so-nhan">{n.nhan}</div>
                  </div>
                </div>
              ))}
            </div>

            <div className="row g-4">
              <div className="col-lg-8">
                <div className="ck-the p-4 mb-4 ck-hien">
                  <div className="row g-2 mb-3">
                    {['gal1', 'gal2', 'gal3'].map((k) => (
                      <div className="col-4" key={k}>
                        <div className="ck-anh" style={{ backgroundImage: 'url(' + anh(k) + ')', height: 110 }} />
                      </div>
                    ))}
                  </div>
                  <h2 className="h5 fw-bold">Cảng container thông minh hàng đầu khu vực</h2>
                  <p className="mb-2">
                    Cảng nước sâu tại cửa ngõ giao thương TP. Hồ Chí Minh — <strong>120 ha</strong> khai thác, năng lực thông qua trên{' '}
                    <strong>600.000 TEU/năm</strong>, kết nối hơn 50 tuyến dịch vụ toàn cầu.
                  </p>
                  <ul className="mb-0 small">
                    <li>
                      <strong>PortBot</strong> — trợ lý AI tra cứu container, lịch tàu, biểu phí 24/7.
                    </li>
                    <li>
                      <strong>AI điều độ</strong> — gợi ý phân bổ bến theo thông số tàu và lịch khai thác.
                    </li>
                    <li>
                      <strong>AI dự báo</strong> — dự báo sản lượng thông qua trên dữ liệu lịch sử.
                    </li>
                  </ul>
                </div>

                <div className="ck-the p-4 mb-4 ck-hien">
                  <h2 className="h6 fw-bold mb-3">
                    <i className="bi bi-signpost-2 me-2" />
                    Chặng đường phát triển
                  </h2>
                  <div className="ck-moc">
                    {MOC.map((m) => (
                      <div className={'ck-moc-item' + (m.nay ? ' ck-nay' : '')} key={m.nam}>
                        <h3>
                          {m.nam} — {m.ten}
                        </h3>
                        <small className="text-body-secondary">{m.mo}</small>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="ck-the p-4 ck-hien">
                  <h2 className="h6 fw-bold mb-3">
                    <i className="bi bi-bounding-box me-2" />
                    Năng lực cầu bến
                  </h2>
                  <div className="table-responsive">
                    <table className="table align-middle mb-0">
                      <thead>
                        <tr>
                          <th>Bến</th>
                          <th>Chiều dài</th>
                          <th>Độ sâu</th>
                          <th>Cẩu bờ</th>
                          <th>Trạng thái</th>
                          <th>Công năng</th>
                        </tr>
                      </thead>
                      <tbody>
                        {duLieu.bens.length === 0 && (
                          <tr>
                            <td colSpan={6} className="text-center py-4 text-body-secondary">
                              Đang cập nhật thông tin cầu bến…
                            </td>
                          </tr>
                        )}
                        {duLieu.bens.map((b) => (
                          <tr key={b.maBen}>
                            <td className="fw-semibold">{b.tenBen}</td>
                            <td>{dinhDangSo(b.chieuDai)} m</td>
                            <td>{dinhDangSo(b.doSau, 1)} m</td>
                            <td>{b.soCau}</td>
                            <td>
                              <NhanTrangThai giaTri={b.trangThai} />
                            </td>
                            <td className="small text-body-secondary">{b.moTa}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <div className="col-lg-4">
                <div className="ck-cta mb-4 ck-hien">
                  <div className="ck-cta-anh" style={{ backgroundImage: 'url(' + anh('about') + ')' }} />
                  <h2 className="h6 fw-bold mb-3">
                    <i className="bi bi-award me-2" />
                    Số liệu nổi bật
                  </h2>
                  {[
                    ['Diện tích khai thác', '120 ha'],
                    ['Năng lực thông qua', '600.000+ TEU/năm'],
                    ['Tuyến dịch vụ', '50+ tuyến'],
                    ['Cẩu bờ STS', '15 chiếc'],
                    ['Xe container/ngày', '2.500+ lượt'],
                  ].map(([ten, gt]) => (
                    <div className="d-flex justify-content-between border-bottom border-light border-opacity-25 py-2 small" key={ten}>
                      <span>{ten}</span>
                      <strong>{gt}</strong>
                    </div>
                  ))}
                </div>

                <div className="ck-the p-4 ck-hien">
                  <h2 className="h6 fw-bold mb-3">
                    <i className="bi bi-clock-history me-2" />
                    Giờ hoạt động
                  </h2>
                  <p className="mb-1 small">
                    <strong>Cổng cảng &amp; bãi container:</strong> 24/7
                  </p>
                  <p className="mb-3 small">
                    <strong>Văn phòng thương vụ:</strong> 7h30 – 17h00 (T2–T7)
                  </p>
                  <Link className="ck-nut-chinh w-100 d-block text-center py-2" to="/home/contact">
                    <i className="bi bi-envelope me-1" />
                    Gửi liên hệ
                  </Link>
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </>
  )
}
