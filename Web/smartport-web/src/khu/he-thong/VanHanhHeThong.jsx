import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, TheChiSo, TheNoiDung } from '../../components/ui'
import { dinhDangNgayGio, dinhDangSo, thoiGianTruoc } from '../../utils/dinhDang'
import './he-thong.css'

/** 1536 byte -> "1,5 KB"; giong ban MVC: duoi 1 MB ghi KB, tu 1 GB ghi GB */
function dungLuong(soByte) {
  if (soByte == null) return '?'
  if (soByte < 1024 * 1024) return dinhDangSo(soByte / 1024, 1) + ' KB'
  if (soByte < 1024 * 1024 * 1024) return dinhDangSo(soByte / 1048576, 1) + ' MB'
  return dinhDangSo(soByte / 1073741824, 2) + ' GB'
}

/** Uptime nhu ban MVC: tu 24 gio tro len "x ngày y giờ", duoi 24 gio "x giờ y phút" */
function thoiGianChay(giay) {
  if (giay >= 86400) return Math.floor(giay / 86400) + ' ngày ' + Math.floor((giay % 86400) / 3600) + ' giờ'
  return Math.floor(giay / 3600) + ' giờ ' + Math.floor((giay % 3600) / 60) + ' phút'
}

const hai = (n) => String(n).padStart(2, '0')
const gioGiayNgay = (v) => {
  const d = new Date(v)
  return `${hai(d.getHours())}:${hai(d.getMinutes())}:${hai(d.getSeconds())} ${hai(d.getDate())}/${hai(d.getMonth() + 1)}/${d.getFullYear()}`
}

/** Van hanh he thong: suc khoe CSDL, so lieu phan he, kho file, uptime, loi, tien trinh nen (thay Admin/System/Index). */
export default function VanHanhHeThong() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/he-thong/suc-khoe')

  if (dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={loi} thuLai={taiLai} />

  const csdl = d.coSoDuLieu
  const mt = d.moiTruong
  const tt = d.tienTrinhNen
  const tongKho = d.khoFile.reduce((s, k) => s + (k.soByte || 0), 0)
  const khoLoi = d.khoFile.some((k) => k.docLoi)
  const onDinh = csdl.ketNoi && d.loi24h === 0 && !(tt.outbox?.loi > 0)

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <NhanTrangThai mau={onDinh ? 'ok' : csdl.ketNoi ? 'warn' : 'do'} icon="bi-heart-pulse">
          {onDinh ? 'Hệ thống ổn định' : csdl.ketNoi ? 'Cần kiểm tra' : 'Mất kết nối CSDL'}
        </NhanTrangThai>
        <span className="small text-body-secondary">Giờ máy chủ {gioGiayNgay(mt.gioMayChu)}</span>
        <div className="ms-auto d-flex gap-2">
          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => taiLai()} disabled={dangTai}>
            {dangTai ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-arrow-clockwise me-1" />}
            Làm mới
          </button>
          <Link className="btn btn-sm btn-primary" to="/admin/system/nhatky">
            <i className="bi bi-journal-text me-1" />
            Nhật ký hoạt động
          </Link>
        </div>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-sm-6 col-xl-3">
          <TheChiSo
            icon={csdl.ketNoi ? 'bi-database-check' : 'bi-database-x'}
            mau={csdl.ketNoi ? 'la' : 'do'}
            so={csdl.ketNoi ? dinhDangSo(csdl.pingMs) + ' ms' : 'Mất kết nối'}
            nhan={
              <span title={csdl.mayChu}>
                CSDL {csdl.tenCsdl} · {csdl.mayChu}
              </span>
            }
          />
        </div>
        <div className="col-sm-6 col-xl-3">
          <TheChiSo icon="bi-stopwatch" mau="xanh" so={thoiGianChay(mt.uptimeGiay)} nhan={`Uptime · ${mt.may} · .NET ${mt.dotNet}`} />
        </div>
        <div className="col-sm-6 col-xl-3">
          <Link className="ht-the-bam" to="/admin/system/nhatky?loai=Loi" title="Xem nhật ký lỗi">
            <TheChiSo
              icon={d.loi24h === 0 ? 'bi-check2-circle' : 'bi-bug'}
              mau={d.loi24h === 0 ? 'la' : 'do'}
              so={dinhDangSo(d.loi24h)}
              nhan={`Lỗi 24 giờ · ${dinhDangSo(d.baoMat7Ngay)} cảnh báo bảo mật / 7 ngày`}
            />
          </Link>
        </div>
        <div className="col-sm-6 col-xl-3">
          <TheChiSo
            icon="bi-hdd-stack"
            mau="tim"
            so={khoLoi && tongKho === 0 ? '?' : dungLuong(tongKho)}
            nhan={d.khoFile.map((k) => `${k.ten} ${k.docLoi ? '?' : dungLuong(k.soByte)}`).join(' · ')}
          />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-7">
          <TheNoiDung tieuDe="Số liệu từng phân hệ" icon="bi-database">
            {d.soLieuPhanHe.length ? (
              <div className="row g-2">
                {d.soLieuPhanHe.map((b) => (
                  <div className="col-6 col-md-4" key={b.ten}>
                    <div className="ht-o-so">
                      <i className={'bi ' + b.icon} />
                      <div className="min-w-0">
                        <div className="ht-o-so-so">{dinhDangSo(b.so)}</div>
                        <div className="ht-o-so-nhan text-truncate">{b.ten}</div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center text-body-secondary py-4">Không đọc được số liệu — CSDL đang mất kết nối.</div>
            )}
          </TheNoiDung>

          <TheNoiDung tieuDe="Tiến trình nền" icon="bi-cpu">
            <div className="ht-tien-trinh">
              <i className="bi bi-robot" />
              <div className="flex-grow-1 min-w-0">
                <div className="fw-semibold">Agent tự động</div>
                <div className="small text-body-secondary">
                  {!tt.agentBat
                    ? 'Tắt chạy nền trên máy chủ này (Agent:ChayNen = false).'
                    : tt.agentLanChayCuoi
                      ? `Chạy lần cuối ${dinhDangNgayGio(tt.agentLanChayCuoi)} (${thoiGianTruoc(tt.agentLanChayCuoi)})`
                      : 'Chưa chạy lần nào kể từ khi khởi động — lần đầu sau 2 phút, sau đó 30 phút/lần.'}
                </div>
                {tt.agentKetQuaCuoi && <div className={'small ' + (tt.agentKetQuaCuoi.startsWith('LỖI') ? 'text-danger' : '')}>{tt.agentKetQuaCuoi}</div>}
              </div>
              <NhanTrangThai mau={tt.agentBat ? 'ok' : 'off'}>{tt.agentBat ? 'Đang bật' : 'Tắt'}</NhanTrangThai>
            </div>

            <div className="ht-tien-trinh">
              <i className="bi bi-broadcast" />
              <div className="flex-grow-1 min-w-0">
                <div className="fw-semibold">Bộ phát sự kiện (Outbox)</div>
                {tt.outbox ? (
                  <div className="small text-body-secondary">
                    Đang chờ <b className={tt.outbox.dangCho > 0 ? 'text-warning-emphasis' : ''}>{dinhDangSo(tt.outbox.dangCho)}</b>
                    {' · '}lỗi <b className={tt.outbox.loi > 0 ? 'text-danger' : ''}>{dinhDangSo(tt.outbox.loi)}</b>
                    {' · '}xử lý 24 giờ <b>{dinhDangSo(tt.outbox.xong24Gio)}</b>
                    {tt.outbox.choLauNhatGiay > 0 && <> · chờ lâu nhất {dinhDangSo(tt.outbox.choLauNhatGiay)} giây</>}
                  </div>
                ) : (
                  <div className="small text-danger">Không đọc được bảng sự kiện: {tt.outboxLoi}</div>
                )}
              </div>
              <NhanTrangThai mau={tt.boPhatBat ? 'ok' : 'off'}>{tt.boPhatBat ? 'Đang bật' : 'Tắt'}</NhanTrangThai>
            </div>

            <div className="ht-tien-trinh">
              <i className="bi bi-activity" />
              <div className="flex-grow-1 min-w-0">
                <div className="fw-semibold">Kết nối realtime</div>
                <div className="small text-body-secondary">Màn hình đang nhận sự kiện trực tiếp (SSE)</div>
              </div>
              <span className="fs-5 fw-bold">{dinhDangSo(tt.soKetNoiRealtime)}</span>
            </div>
          </TheNoiDung>
        </div>

        <div className="col-lg-5">
          <TheNoiDung
            tieuDe="Lỗi gần nhất"
            icon="bi-bug"
            phai={
              <Link className="small" to="/admin/system/nhatky?loai=Loi">
                Xem tất cả <i className="bi bi-arrow-right" />
              </Link>
            }
          >
            {!d.loiGanNhat.length ? (
              <div className="text-center text-body-secondary py-4">
                <i className="bi bi-emoji-smile fs-2 d-block mb-1 text-success" />
                Chưa ghi nhận lỗi nào.
              </div>
            ) : (
              d.loiGanNhat.map((l) => (
                <div key={l.maNK} className="ht-dong-loi">
                  <div>{l.noiDung}</div>
                  <div className="text-body-secondary" style={{ fontSize: '.72rem' }}>
                    {dinhDangNgayGio(l.thoiGian)}
                  </div>
                </div>
              ))
            )}
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
