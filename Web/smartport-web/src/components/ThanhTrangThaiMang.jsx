import { useCallback, useEffect, useRef, useState } from 'react'
import { kichHoatBanMoi, useCoBanMoi } from '../ngoaiTuyen/capNhatPwa'
import { moTaTrangThai } from '../ngoaiTuyen/dichVuNgoaiTuyen.js'
import { hopThu, tram } from '../ngoaiTuyen/tram'
import { useTrangThaiMang } from '../ngoaiTuyen/useTrangThaiMang'

const MAU = {
  xanh: { nen: '#dcfce7', chu: '#166534', vien: '#86efac' },
  vang: { nen: '#fef3c7', chu: '#92400e', vien: '#fcd34d' },
  do: { nen: '#fee2e2', chu: '#991b1b', vien: '#fca5a5' },
}
const MAU_TOI = {
  xanh: { nen: '#16a34a', chu: '#fff', vien: '#22c55e' },
  vang: { nen: '#b45309', chu: '#fff', vien: '#f59e0b' },
  do: { nen: '#dc2626', chu: '#fff', vien: '#ef4444' },
}

const gioPhut = (ms) => {
  if (!ms) return ''
  const d = new Date(ms)
  return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0')
}

/**
 * THANH TRANG THAI MANG CUA TRAM: 🟢 truc tuyen / 🟡 ngoai tuyen hoac con muc cho (N cho gui, ban sao luc HH:mm) / 🔴 co muc loi.
 * Bam vao -> bang chi tiet: Gui ngay, lam moi ban sao, danh sach luot loi (Thu lai / Bo) va luot can doi soat (Da xem).
 * lon = ban chu to cho man chot bao ve (nen toi).
 */
export default function ThanhTrangThaiMang({ lon = false }) {
  const tt = useTrangThaiMang()
  const coBanMoi = useCoBanMoi()
  const [mo, setMo] = useState(false)
  const [ds, setDs] = useState({ loi: [], doiSoat: [], cho: [] })
  const [dangLam, setDangLam] = useState(false)
  const hopRef = useRef(null)
  const mt = moTaTrangThai(tt)
  const mau = (lon ? MAU_TOI : MAU)[mt.mau]

  const taiDanhSach = useCallback(async () => {
    try {
      const [tatCa, nhatKy] = await Promise.all([hopThu.layTatCa(), hopThu.layNhatKy()])
      setDs({ loi: tatCa.filter((x) => x.trangThai === 'loi'), cho: tatCa.filter((x) => x.trangThai !== 'loi'), doiSoat: nhatKy })
    } catch {
      setDs({ loi: [], doiSoat: [], cho: [] })
    }
  }, [])

  // Bang dang mo: tai lai danh sach moi khi so dem doi
  useEffect(() => {
    if (mo) taiDanhSach()
  }, [mo, taiDanhSach, tt.soCho, tt.soLoi, tt.soCanDoiSoat, tt.soDangGui])

  // Bam ra ngoai -> dong bang
  useEffect(() => {
    if (!mo) return undefined
    const dong = (e) => {
      if (hopRef.current && !hopRef.current.contains(e.target)) setMo(false)
    }
    const phim = (e) => e.key === 'Escape' && setMo(false)
    document.addEventListener('mousedown', dong)
    document.addEventListener('keydown', phim)
    return () => {
      document.removeEventListener('mousedown', dong)
      document.removeEventListener('keydown', phim)
    }
  }, [mo])

  async function lam(viec) {
    setDangLam(true)
    try {
      await viec()
    } finally {
      setDangLam(false)
      await tram.demLai()
      if (mo) taiDanhSach()
    }
  }

  const chuGon = mt.bieuTuong + ' ' + (tt.soCho + tt.soDangGui > 0 ? tt.soCho + tt.soDangGui : tt.soLoi > 0 ? tt.soLoi : '')

  return (
    <div ref={hopRef} className={lon ? 'position-relative' : 'position-relative me-3'}>
      <button
        type="button"
        onClick={() => setMo((x) => !x)}
        aria-expanded={mo}
        aria-label={'Trạng thái mạng: ' + mt.chu}
        title={mt.chu}
        style={{
          background: mau.nen,
          color: mau.chu,
          border: '1px solid ' + mau.vien,
          borderRadius: 999,
          padding: lon ? '6px 14px' : '2px 10px',
          fontWeight: lon ? 800 : 600,
          fontSize: lon ? '0.95rem' : '0.78rem',
          maxWidth: lon ? '100%' : 420,
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {lon ? (
          mt.chu
        ) : (
          <>
            <span className="d-xl-none">{chuGon.trim()}</span>
            <span className="d-none d-xl-inline">{mt.chu}</span>
          </>
        )}
        {coBanMoi && <span className="ms-1">· ⬆ có bản mới</span>}
      </button>

      {mo && (
        <div
          className="card shadow"
          role="dialog"
          aria-label="Chi tiết trạng thái mạng"
          style={{ position: 'absolute', right: 0, top: '110%', zIndex: 1080, width: 'min(420px, 92vw)', color: 'var(--bs-body-color)' }}
          data-bs-theme="light"
        >
          <div className="card-body small">
            <div className="fw-semibold mb-1">{mt.chu}</div>
            <div className="text-body-secondary mb-2">
              {tt.coMang === false
                ? 'Không gọi được máy chủ (nhịp /suc-khoe). Lượt xe vẫn ghi vào máy này và tự gửi khi có lại kết nối.'
                : tt.congNgung
                  ? 'Gateway báo dịch vụ Cổng đang ngừng. Lượt xe ghi tạm vào máy này và tự gửi khi dịch vụ chạy lại.'
                : tt.coMang === null
                  ? 'Đang kiểm tra kết nối tới máy chủ…'
                  : 'Đang kết nối máy chủ' + (tt.kiemLuc ? ' (kiểm lúc ' + gioPhut(tt.kiemLuc) + ')' : '') + '.'}
              {tt.banSao?.luc ? ' Bản sao lệnh / eDO / thẻ xe lúc ' + gioPhut(tt.banSao.luc) + (tt.banSao.cu ? ' — đã CŨ (quá 4 giờ).' : '.') : ' Chưa có bản sao lệnh tại máy này.'}
              {tt.banSao?.biCat && (
                <div className="mt-1" style={{ color: '#92400e' }}>
                  ✂ Bản sao KHÔNG đầy đủ (máy chủ cắt bớt vì quá nhiều lệnh / thẻ xe) — xe không có trong bản sao thì KHÔNG kết luận
                  &quot;không có lệnh&quot;: kiểm giấy tờ bằng mắt / hỏi điều độ.
                </div>
              )}
              {tt.choDangNhap && (
                <div className="text-danger mt-1">
                  🔒 Cần đăng nhập tài khoản có quyền chốt cổng để gửi {tt.soCho + tt.soDangGui} lượt
                  {tt.khongQuyen ? ' (tài khoản đang đăng nhập không có quyền chốt cổng)' : ''} — lượt xe vẫn giữ an toàn trong máy, không bị xóa.
                </div>
              )}
              {tt.loaiKho && tt.loaiKho !== 'indexeddb' && (
                <div className="text-danger mt-1">
                  Trình duyệt không cho dùng IndexedDB — đang lưu tạm bằng {tt.loaiKho === 'local-storage' ? 'localStorage (dung lượng nhỏ)' : 'bộ nhớ (MẤT khi đóng tab!)'}.
                </div>
              )}
              {tt.loiDongBo && <div className="text-warning-emphasis mt-1">Lần gửi gần nhất: {tt.loiDongBo}</div>}
              {tt.dongBoCuoi && (
                <div className="mt-1">
                  Gửi lúc {gioPhut(tt.dongBoCuoi.luc)}: {tt.dongBoCuoi.daGui} đã lưu, {tt.dongBoCuoi.trung} trùng (đã có), {tt.dongBoCuoi.canDoiSoat} cần đối soát, {tt.dongBoCuoi.loi} lỗi.
                </div>
              )}
            </div>

            <div className="d-flex flex-wrap gap-2 mb-2">
              <button type="button" className="btn btn-sm btn-warning" disabled={dangLam || tt.dangDongBo || tt.soCho + tt.soDangGui === 0} onClick={() => lam(() => tram.dongBoNgay())}>
                {tt.dangDongBo ? 'Đang gửi…' : 'Gửi ngay'}
              </button>
              <button type="button" className="btn btn-sm btn-outline-secondary" disabled={dangLam || tt.coMang === false} onClick={() => lam(() => tram.taiLaiBanSao())}>
                Làm mới bản sao
              </button>
              {coBanMoi && (
                <button type="button" className="btn btn-sm btn-primary" onClick={kichHoatBanMoi} title="Bản giao diện mới đã tải xong — tải lại để dùng (lượt xe chờ gửi vẫn giữ nguyên)">
                  Tải lại để cập nhật
                </button>
              )}
            </div>

            {ds.loi.length > 0 && (
              <>
                <div className="fw-semibold text-danger mt-2">🔴 Máy chủ từ chối ({ds.loi.length})</div>
                {ds.loi.map((x) => (
                  <div key={x.maThaoTac} className="border rounded p-2 mt-1">
                    <b>{x.duLieu?.bienSo}</b> · {x.duLieu?.huongDi} · {gioPhut(Date.parse(x.duLieu?.lucXayRa))}
                    <div className="text-danger">{x.loiCuoi}</div>
                    <div className="d-flex gap-2 mt-1">
                      <button type="button" className="btn btn-sm btn-outline-primary" disabled={dangLam} onClick={() => lam(() => hopThu.thuLai(x.maThaoTac))}>
                        Thử lại
                      </button>
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-danger"
                        disabled={dangLam}
                        onClick={() => {
                          if (window.confirm('Bỏ lượt xe ' + (x.duLieu?.bienSo || '') + ' khỏi máy? Lượt này sẽ KHÔNG được gửi lên hệ thống (nhớ ghi phiếu tay).'))
                            lam(() => hopThu.xoa(x.maThaoTac))
                        }}
                      >
                        Bỏ
                      </button>
                    </div>
                  </div>
                ))}
              </>
            )}

            {ds.doiSoat.length > 0 && (
              <>
                <div className="fw-semibold mt-2" style={{ color: '#92400e' }}>
                  🟡 Đã ghi nhưng cần đối soát ({ds.doiSoat.length})
                </div>
                {ds.doiSoat.slice(0, 20).map((x) => (
                  <div key={x.maThaoTac} className="border rounded p-2 mt-1" style={{ background: '#fffbeb' }}>
                    <b>{x.bienSo}</b> · {x.huongDi} · {gioPhut(Date.parse(x.lucXayRa))} {x.maXV ? '· lượt #' + x.maXV : ''}
                    {x.lyDo && <div>{x.lyDo}</div>}
                    <button type="button" className="btn btn-sm btn-outline-secondary mt-1" disabled={dangLam} onClick={() => lam(() => hopThu.xoaNhatKy(x.maThaoTac))}>
                      Đã xem
                    </button>
                  </div>
                ))}
              </>
            )}

            {ds.cho.length > 0 && (
              <>
                <div className="fw-semibold mt-2">⏳ Chờ gửi ({ds.cho.length})</div>
                <div className="text-body-secondary">
                  {ds.cho
                    .slice(0, 8)
                    .map((x) => x.duLieu?.bienSo + ' ' + (x.duLieu?.huongDi || '') + ' ' + gioPhut(Date.parse(x.duLieu?.lucXayRa)))
                    .join(' · ')}
                  {ds.cho.length > 8 ? ' …' : ''}
                </div>
              </>
            )}

            {typeof window !== 'undefined' && !window.isSecureContext && (
              <div className="alert alert-warning py-1 px-2 mt-2 mb-0">
                Đang mở bằng HTTP qua mạng LAN: trình duyệt tắt service worker → mất mạng mà <b>tải lại trang</b> sẽ không mở được. Dùng địa chỉ{' '}
                <b>https://&lt;máy-chủ&gt;:5443</b> cho máy trạm.
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
