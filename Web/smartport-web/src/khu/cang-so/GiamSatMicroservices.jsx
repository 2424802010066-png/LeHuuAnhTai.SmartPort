import { useEffect } from 'react'
import { useApi } from '../../api/useApi'
import DongSuKien from '../../components/DongSuKien'
import { BaoLoi, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { dinhDangSo } from '../../utils/dinhDang'
import './cang-so.css'

/**
 * GIAM SAT KIEN TRUC MICROSERVICES — moi dich vu: tien trinh rieng, cong rieng, CSDL rieng; giao tiep bang su kien
 * qua RabbitMQ. Trang tu lam moi 5 giay: suc khoe tung dich vu (/suc-khoe), Outbox, hang doi RabbitMQ.
 * Ban gop (somee): chi co 1 tien trinh — trang van chay, hien "chế độ gộp".
 */
export default function GiamSatMicroservices() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/su-kien/dich-vu')

  useEffect(() => {
    const t = setInterval(() => taiLai({ im: true }), 5000)
    return () => clearInterval(t)
  }, [taiLai])

  const dichVu = d?.dichVu ?? []
  const laTach = d?.cheDo === 'microservices'
  const dangChay = dichVu.filter((x) => (laTach ? x.chay : true)).length
  const hangDoi = (d?.hangDoi ?? []).filter((q) => q.ten)
  const tinCho = hangDoi.reduce((t, q) => t + (q.tinCho || 0), 0)
  const daXuLy = dichVu.reduce((t, x) => t + (x.sucKhoe?.daXuLy || 0), 0)
  const hangLoi = hangDoi.find((q) => q.ten === 'smartport.loi')

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />
      <div className="row g-3 mb-3">
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-diagram-3" mau="xanh" so={laTach ? dangChay + '/' + dichVu.length : 'Gộp'} nhan={laTach ? 'dịch vụ đang chạy' : 'chế độ 1 tiến trình'} />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-inboxes" mau="cam" so={dinhDangSo(tinCho)} nhan="tin đang chờ trong RabbitMQ" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-check2-all" mau="la" so={dinhDangSo(daXuLy)} nhan="sự kiện đã xử lý (từ khi chạy)" />
        </div>
        <div className="col-6 col-lg-3">
          <TheChiSo icon="bi-bug" mau={hangLoi?.tinCho ? 'do' : 'xanh'} so={dinhDangSo(hangLoi?.tinCho ?? 0)} nhan="tin trong hàng đợi lỗi" />
        </div>
      </div>

      <TheNoiDung
        tieuDe={laTach ? 'Các dịch vụ (mỗi dịch vụ 1 tiến trình, 1 CSDL riêng)' : 'Chế độ gộp — toàn bộ dịch vụ trong 1 tiến trình'}
        icon="bi-hdd-network"
        phai={<span className="small text-body-secondary">{dangTai ? 'Đang tải…' : 'Tự làm mới mỗi 5 giây'}</span>}
      >
        <div className="row g-3">
          {dichVu.map((x) => {
            const s = x.sucKhoe ?? x
            const tot = (laTach ? x.chay : true) && s.tinhTrang === 'tốt'
            // suy giam = CSDL on nhung mat RabbitMQ: van phuc vu, su kien nam cho o Outbox -> vang, khong phai do
            const suyGiam = !tot && (laTach ? x.chay : true) && s.tinhTrang === 'suy-giam'
            return (
              <div key={x.ten ?? s.dichVu} className="col-12 col-md-6 col-xl-4">
                <div className={'cs-dich-vu ' + (tot ? 'tot' : suyGiam ? 'suy-giam' : 'loi')}>
                  <div className="d-flex align-items-center gap-2 mb-1">
                    <i className={'bi ' + (x.nenTang === 'python' ? 'bi-filetype-py' : 'bi-hexagon-half') + ' fs-5'} />
                    <div className="fw-semibold flex-grow-1 text-truncate">{x.tenHienThi ?? s.tieuDe}</div>
                    <NhanTrangThai giaTri={suyGiam ? 'suy giảm' : s.tinhTrang ?? 'không phản hồi'} mau={tot ? 'ok' : suyGiam ? 'warn' : 'do'} />
                  </div>
                  {suyGiam && s.lyDo && <div className="small text-warning-emphasis mb-1"><i className="bi bi-exclamation-triangle me-1" />{s.lyDo}</div>}
                  <div className="small text-body-secondary mb-2">
                    {x.ten ?? s.dichVu} · cổng {x.cong ?? '—'} · {x.nenTang ?? 'dotnet'}
                    {x.phanHoiMs != null && <> · {x.phanHoiMs} ms</>}
                  </div>
                  {s.coSoDuLieu && (
                    <div className="small">
                      <i className="bi bi-database me-1" />
                      {s.coSoDuLieu} — sở hữu <b>{(s.bangSoHuu ?? []).length}</b> bảng, bản sao <b>{(s.bangBanSao ?? []).length}</b>
                    </div>
                  )}
                  {x.nenTang === 'python' ? (
                    // Dich vu AI (Python): khong CSDL, khong RabbitMQ — hien mo hinh dang nap + chi so do tren tap test giu rieng
                    <>
                      <div className="small">
                        <i className="bi bi-cpu me-1" />
                        {s.gpu ?? '—'} · JWT {s.xacThucJwt ? 'bật' : 'tắt'}
                      </div>
                      <div className="small text-truncate" title={(s.moHinh ?? []).join(', ')}>
                        <i className="bi bi-boxes me-1" />
                        Mô hình: {(s.moHinh ?? []).join(', ') || '—'}
                      </div>
                      {s.chiSoTest && (
                        <div className="small">
                          <i className="bi bi-bullseye me-1" />
                          Test: biển số {s.chiSoTest.bienSoDauCuoi ?? '—'}% · mã cont {s.chiSoTest.containerVungMa ?? '—'}%
                        </div>
                      )}
                    </>
                  ) : (
                    <>
                      <div className="small">
                        <i className="bi bi-broadcast me-1" />
                        {s.bus === 'RabbitMQ' ? <>RabbitMQ: {s.rabbitMQ ?? '—'} · hàng đợi {s.hangDoi ?? '—'}</> : 'Bộ phát trong tiến trình (Outbox)'}
                      </div>
                      <div className="small">
                        <i className="bi bi-envelope-paper me-1" />
                        Outbox chờ {s.outbox?.dangCho ?? 0} · lỗi {s.outbox?.loi ?? 0} · đã gửi {dinhDangSo(s.daGuiLenRabbit ?? 0)} · đã xử lý{' '}
                        {dinhDangSo(s.daXuLy ?? 0)}
                        {s.dayHangDoiLoi ? <span className="text-danger"> · đẩy lỗi {s.dayHangDoiLoi}</span> : null}
                      </div>
                    </>
                  )}
                  {(x.duongDan ?? []).length > 0 && (
                    <div className="small text-body-secondary text-truncate" title={(x.duongDan ?? []).join(' ')}>
                      <i className="bi bi-signpost me-1" />
                      {(x.duongDan ?? []).join(' · ')}
                    </div>
                  )}
                  {laTach && x.nenTang !== 'python' && (
                    <a className="small" href={'/dich-vu/' + x.ten + '/swagger/index.html'} target="_blank" rel="noreferrer noopener">
                      Swagger của dịch vụ <i className="bi bi-box-arrow-up-right" />
                    </a>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-12 col-xl-7">
          <TheNoiDung tieuDe="Hàng đợi RabbitMQ" icon="bi-inboxes" khongDem>
            {hangDoi.length === 0 ? (
              <div className="text-body-secondary small">{laTach ? 'Chưa đọc được hàng đợi.' : 'Chế độ gộp không dùng RabbitMQ.'}</div>
            ) : (
              <div className="table-responsive">
                <table className="table table-sm sp-bang align-middle mb-0">
                  <thead>
                    <tr>
                      <th>Hàng đợi</th>
                      <th className="text-end">Tin chờ</th>
                      <th className="text-end">Người nhận</th>
                      <th className="text-end">Vào / giây</th>
                      <th className="text-end">Nhận / giây</th>
                    </tr>
                  </thead>
                  <tbody>
                    {hangDoi.map((q) => (
                      <tr key={q.ten} className={q.ten === 'smartport.loi' && q.tinCho ? 'table-danger' : ''}>
                        <td className="small">
                          {q.tam ? <span className="text-body-secondary">{q.ten} (tạm — luồng realtime)</span> : q.ten}
                        </td>
                        <td className="text-end">{q.tinCho}</td>
                        <td className="text-end">{q.nguoiNhan}</td>
                        <td className="text-end">{q.tocDoVao}</td>
                        <td className="text-end">{q.tocDoNhan}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </TheNoiDung>
          <TheNoiDung tieuDe="Kiến trúc (để trình bày)" icon="bi-info-circle" khongDem>
            <ul className="small mb-0 cs-giai-thich">
              <li>
                <b>Database-per-service</b>: mỗi bảng chỉ <b>một</b> dịch vụ được ghi; dịch vụ khác giữ <b>bản sao chỉ-đọc</b> cập nhật bằng sự kiện{' '}
                <code>du-lieu.&lt;bảng&gt;</code> (event-carried state transfer). Ghi nhầm bảng của dịch vụ khác bị chặn ngay (EF interceptor + chốt chặn SQL).
              </li>
              <li>
                <b>Transactional Outbox</b>: dữ liệu và sự kiện lưu <b>cùng giao dịch</b>; bộ chuyển tiếp đẩy lên exchange <code>smartport.su-kien</code> (topic) với
                publisher confirm.
              </li>
              <li>
                <b>Inbox + thử lại có độ trễ</b>: mỗi dịch vụ một hàng đợi bền; xử lý xong mới ack; tin lỗi sang hàng đợi chờ (TTL) rồi quay lại, quá 6 lần vào{' '}
                <code>smartport.loi</code> (dead-letter).
              </li>
              <li>
                <b>API Gateway (YARP)</b> 1 địa chỉ cho trình duyệt; định tuyến theo <code>dich-vu.json</code>. Bản somee vẫn chạy <b>gộp</b> cùng mã nguồn.
              </li>
            </ul>
          </TheNoiDung>
        </div>
        <div className="col-12 col-xl-5">
          <TheNoiDung tieuDe="Sự kiện nghiệp vụ realtime" icon="bi-activity" khongDem>
            <DongSuKien gioiHan={14} />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
