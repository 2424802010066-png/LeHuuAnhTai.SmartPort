import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, NhanTrangThai, PhanTrang, TheNoiDung, useXacNhan } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangNgayGio, dinhDangSo, gioNgayNgan, tienGon } from '../../utils/dinhDang'
import OSo from './dungChung'
import './dieu-hanh.css'

const CHE_DO = ['Tắt', 'Xin phép', 'Tự làm']
const MAU_CHE_DO = { 'Tự làm': 'dh-nhan-tim', 'Xin phép': 'sp-nhan-warn', 'Tắt': 'sp-nhan-off' }
const LOC_TRANG_THAI = ['', 'Chờ duyệt', 'Đã làm']

/** Mot ky nang agent: doi che do / nguong / so viec toi da moi lan chay */
function DongKyNang({ kn, dangChay, luu, chayRieng }) {
  const [gt, setGt] = useState({ cheDo: kn.cheDo, nguong: kn.nguong, toiDaMoiLan: kn.toiDaMoiLan })

  // Sau khi luu xong danh sach tai lai -> dong bo lai o nhap theo du lieu may chu
  useEffect(() => {
    setGt({ cheDo: kn.cheDo, nguong: kn.nguong, toiDaMoiLan: kn.toiDaMoiLan })
  }, [kn.cheDo, kn.nguong, kn.toiDaMoiLan])

  const doi = (k) => (e) => setGt((c) => ({ ...c, [k]: e.target.value }))
  const chuaLuu = gt.cheDo !== kn.cheDo || Number(gt.nguong) !== kn.nguong || Number(gt.toiDaMoiLan) !== kn.toiDaMoiLan

  return (
    <div className={'dh-ky-nang' + (kn.cheDo === 'Tự làm' ? ' dang-tu-lam' : '')}>
      <div className="d-flex justify-content-between align-items-start gap-2">
        <div className="flex-grow-1 min-w-0">
          <div className="fw-semibold small">
            {kn.tenHienThi}
            {kn.ghiDuLieuThat ? (
              <i className="bi bi-pencil-square ms-1 text-warning" title="Kỹ năng này ghi dữ liệu thật (phiếu thu, bản tin EDI, sự cố)" />
            ) : null}
          </div>
          <div className="dh-ky-nang-mo-ta">{kn.moTa}</div>
        </div>
        <div className="text-end">
          <span className={'sp-nhan ' + (MAU_CHE_DO[kn.cheDo] || 'sp-nhan-info')}>{kn.cheDo}</span>
          {kn.soChoDuyet > 0 ? <div className="small text-warning-emphasis">{kn.soChoDuyet} việc chờ duyệt</div> : null}
        </div>
      </div>

      <div className="row g-1 mt-2">
        <div className="col-5">
          <select className="form-select form-select-sm" value={gt.cheDo} onChange={doi('cheDo')} aria-label={'Chế độ của ' + kn.tenHienThi}>
            {CHE_DO.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </div>
        <div className="col-3">
          <input
            className="form-control form-control-sm"
            type="number"
            min={0}
            value={gt.nguong}
            onChange={doi('nguong')}
            title="Ngưỡng (ngày / số lượng)"
            aria-label={'Ngưỡng của ' + kn.tenHienThi}
          />
        </div>
        <div className="col-2">
          <input
            className="form-control form-control-sm"
            type="number"
            min={1}
            max={200}
            value={gt.toiDaMoiLan}
            onChange={doi('toiDaMoiLan')}
            title="Tối đa mỗi lần chạy"
            aria-label={'Tối đa mỗi lần chạy của ' + kn.tenHienThi}
          />
        </div>
        <div className="col-2">
          <button
            type="button"
            className={'btn btn-sm w-100 ' + (chuaLuu ? 'btn-primary' : 'btn-outline-secondary')}
            title="Lưu cấu hình kỹ năng"
            aria-label={'Lưu cấu hình ' + kn.tenHienThi}
            disabled={dangChay}
            onClick={() => luu(kn.maCH, { cheDo: gt.cheDo, nguong: Number(gt.nguong) || 0, toiDaMoiLan: Number(gt.toiDaMoiLan) || 1 })}
          >
            <i className="bi bi-save" />
          </button>
        </div>
      </div>

      <div className="d-flex flex-wrap gap-2 mt-1">
        {kn.lanChayCuoi ? <span className="dh-chip">Chạy lần cuối {gioNgayNgan(kn.lanChayCuoi)}</span> : null}
        <button type="button" className="btn btn-sm btn-outline-secondary dh-nut-tu-lam py-0 ms-auto" disabled={dangChay} onClick={() => chayRieng(kn.kyNang)}>
          <i className="bi bi-play-fill me-1" />
          Chạy riêng
        </button>
      </div>
    </div>
  )
}

/**
 * BANG DIEU KHIEN AGENT TU HANH DONG (thay man hinh MVC Admin/Agent).
 * Ba cau cua chu depot: agent DA LAM GI (nhat ky co ly do) · duoc PHEP lam gi (3 che do) · lam sai thi GO the nao (hoan tac).
 */
export default function AgentTuDong() {
  const [loc, datLoc] = useBoLoc({ kyNang: '', trangThai: '', trang: 1 })
  const tq = useApi('/api/agent-tu-dong')
  const nk = useApi('/api/agent-tu-dong/nhat-ky' + taoQuery({ ...loc, coTrang: 20 }))
  const { chay, dangChay } = useThaoTac()
  const xacNhan = useXacNhan()
  const [viecMoi, setViecMoi] = useState([])

  const lamMoi = () => {
    tq.taiLai({ im: true })
    nk.taiLai({ im: true })
  }

  async function chayNgay(kyNang) {
    const kq = await chay(() => goiApi('/api/agent-tu-dong/chay-ngay', { method: 'POST', body: { kyNang: kyNang || null } }))
    if (!kq) return
    setViecMoi(kq.duLieu?.maViecMoi || [])
    datLoc({ trang: 1 })
    lamMoi()
  }

  async function luuCauHinh(maCH, duLieu) {
    if (await chay(() => goiApi('/api/agent-tu-dong/cau-hinh/' + maCH, { method: 'PUT', body: duLieu }))) tq.taiLai({ im: true })
  }

  async function thaoTacViec(v, viec) {
    if (viec === 'hoan-tac') {
      const dongY = await xacNhan({
        tieuDe: 'Hoàn tác việc agent đã làm?',
        noiDung: `Gỡ thứ agent tạo ra cho "${v.doiTuong || v.tenKyNang}": hủy phiếu thu chưa thu tiền, xóa bản tin EDI chưa gửi hoặc xóa sự cố agent mở.`,
        nutDongY: 'Hoàn tác',
        mau: 'danger',
      })
      if (!dongY) return
    }
    if (await chay(() => goiApi('/api/agent-tu-dong/' + v.maNK + '/' + viec, { method: 'POST' }))) lamMoi()
  }

  const d = tq.duLieu
  if (tq.dangTai && !d) return <DangTai />
  if (!d) return <BaoLoi loi={tq.loi} thuLai={tq.taiLai} />

  const tk = d.thongKe
  const tt = d.tienTrinhNen

  return (
    <>
      <BaoLoi loi={tq.loi} thuLai={tq.taiLai} />

      <div className="row row-cols-2 row-cols-lg-4 g-3 mb-3">
        <div className="col">
          <OSo mau="vang" so={dinhDangSo(tk.soChoDuyet)} nhan="Chờ bạn duyệt" phu="agent đề nghị, chờ gật đầu" icon="bi-hourglass-split" />
        </div>
        <div className="col">
          <OSo mau="tim-dam" so={dinhDangSo(tk.soTuLam)} nhan="Agent tự làm" phu={`/ ${dinhDangSo(tk.soDaLam)} việc đã xong`} icon="bi-robot" />
        </div>
        <div className="col">
          <OSo mau="la" so={tienGon(tk.tienDaChot)} donVi="đ" nhan="Tiền agent chốt hộ" phu="phiếu thu do agent lập" icon="bi-cash-coin" />
        </div>
        <div className="col">
          <OSo mau={tk.soHoanTac > 0 ? 'do' : ''} so={dinhDangSo(tk.soHoanTac)} nhan="Đã hoàn tác" phu="việc agent làm sai, đã gỡ" icon="bi-arrow-counterclockwise" />
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <TheNoiDung
            tieuDe="Agent được phép làm gì"
            icon="bi-sliders"
            phai={
              <button type="button" className="btn btn-sm btn-primary" disabled={dangChay} onClick={() => chayNgay(null)}>
                {dangChay ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-play-fill me-1" />}
                Chạy ngay
              </button>
            }
          >
            <div className="small text-body-secondary mb-3">
              Mặc định mọi kỹ năng ở <b>Xin phép</b> — agent chỉ đề nghị, bạn bấm duyệt. Xem nó làm đúng vài tuần rồi hãy chuyển sang <b>Tự làm</b>.
            </div>

            {d.kyNang.map((kn) => (
              <DongKyNang key={kn.maCH} kn={kn} dangChay={dangChay} luu={luuCauHinh} chayRieng={chayNgay} />
            ))}

            <div className="dh-vong-agent mt-3">
              <NhanTrangThai mau={tt.dangBat ? 'ok' : 'off'} icon="bi-clock-history">
                {tt.dangBat ? `Bộ chạy nền ${tt.chuKyPhut} phút/lần` : 'Bộ chạy nền đang tắt'}
              </NhanTrangThai>
              <span className="text-body-secondary">
                {tt.lanChayCuoi ? `Lần nền gần nhất ${dinhDangNgayGio(tt.lanChayCuoi)} — ${tt.ketQuaCuoi || 'không có việc'}` : 'Chưa chạy nền lần nào từ khi khởi động.'}
              </span>
            </div>

            <div className="alert alert-light border small mt-3 mb-0">
              <b>Vì sao quyết định bằng luật chứ không bằng mô hình ngôn ngữ:</b> agent này đụng tới tiền của khách và báo cáo gửi hãng tàu. Tiền thì
              không được phép “có vẻ đúng” — luật thì giải trình được từng đồng khi bị hỏi.
            </div>
          </TheNoiDung>
        </div>

        <div className="col-lg-7">
          <TheNoiDung
            tieuDe="Agent đã làm gì"
            icon="bi-journal-text"
            phai={
              <div className="btn-group btn-group-sm" role="group" aria-label="Lọc theo trạng thái">
                {LOC_TRANG_THAI.map((t) => (
                  <button
                    key={t || 'tatca'}
                    type="button"
                    className={'btn ' + (loc.trangThai === t ? 'btn-primary' : 'btn-outline-primary')}
                    onClick={() => datLoc({ trangThai: t, trang: 1 })}
                  >
                    {t || 'Tất cả'}
                  </button>
                ))}
              </div>
            }
          >
            <BaoLoi loi={nk.loi} thuLai={nk.taiLai} />

            {loc.kyNang ? (
              <div className="mb-2">
                <span className="dh-chip me-1">
                  Kỹ năng: {d.kyNang.find((k) => k.kyNang === loc.kyNang)?.tenHienThi || loc.kyNang}
                </span>
                <button type="button" className="btn btn-sm btn-link p-0" onClick={() => datLoc({ kyNang: '', trang: 1 })}>
                  bỏ lọc
                </button>
              </div>
            ) : null}

            {nk.dangTai && !nk.duLieu ? (
              <DangTai chu="Đang tải nhật ký agent…" />
            ) : !nk.duLieu?.items?.length ? (
              <div className="text-center text-body-secondary py-5">
                <i className="bi bi-robot" style={{ fontSize: '2.6rem', opacity: 0.35 }} />
                <div className="mt-2">Agent chưa làm gì cả.</div>
                <div className="small">
                  Bấm <b>Chạy ngay</b> để nó quét hệ thống lần đầu.
                </div>
              </div>
            ) : (
              <div className="dh-danh-sach-viec">
                {nk.duLieu.items.map((v) => (
                  <div
                    key={v.maNK}
                    className={
                      'dh-viec' +
                      (v.trangThai === 'Chờ duyệt' ? ' cho-duyet' : v.trangThai === 'Lỗi' ? ' loi' : v.trangThai === 'Đã hủy' ? ' da-huy' : '') +
                      (v.daHoanTac ? ' da-hoan-tac' : '') +
                      (viecMoi.includes(v.maNK) ? ' moi' : '')
                    }
                  >
                    <div className="d-flex justify-content-between align-items-start gap-2">
                      <div className="min-w-0">
                        <NhanTrangThai giaTri={v.trangThai} mau={v.trangThai === 'Đã làm' ? 'ok' : undefined} />
                        {v.tuDong ? (
                          <span className="sp-nhan dh-nhan-tim ms-1" title="Agent tự làm, không ai bấm">
                            TỰ LÀM
                          </span>
                        ) : null}
                        {v.daHoanTac ? <span className="sp-nhan sp-nhan-do ms-1">ĐÃ HOÀN TÁC</span> : null}
                        <button
                          type="button"
                          className="btn btn-link btn-sm p-0 ms-1 fw-semibold align-baseline"
                          title="Chỉ xem việc của kỹ năng này"
                          onClick={() => datLoc({ kyNang: v.kyNang, trang: 1 })}
                        >
                          {v.tenKyNang}
                        </button>
                        {v.doiTuong ? <code className="ms-1 small">{v.doiTuong}</code> : null}
                      </div>
                      <span className="text-body-secondary text-nowrap text-end small">
                        {gioNgayNgan(v.luc)}
                        {v.nguoiChay ? (
                          <>
                            <br />
                            <span title="Ai đã khởi động lượt chạy này">{v.nguoiChay}</span>
                          </>
                        ) : null}
                      </span>
                    </div>

                    <div className="dh-ly-do">
                      <b>Vì sao:</b> {v.lyDo}
                    </div>
                    {v.hanhDong ? (
                      <div className="dh-hanh-dong">
                        <i className="bi bi-arrow-return-right me-1" />
                        {v.hanhDong}
                      </div>
                    ) : null}
                    {v.ketQua ? <div className="dh-ket-qua">{v.ketQua}</div> : null}

                    <div className="d-flex flex-wrap gap-1 mt-2 align-items-center">
                      {v.coTheDuyet ? (
                        <>
                          <button type="button" className="btn btn-sm btn-primary py-0" disabled={dangChay} onClick={() => thaoTacViec(v, 'duyet')}>
                            <i className="bi bi-check2 me-1" />
                            Duyệt
                          </button>
                          <button type="button" className="btn btn-sm btn-outline-secondary py-0" disabled={dangChay} onClick={() => thaoTacViec(v, 'huy')}>
                            Bỏ qua
                          </button>
                        </>
                      ) : null}
                      {v.coTheHoanTac ? (
                        <button type="button" className="btn btn-sm btn-outline-danger py-0" disabled={dangChay} onClick={() => thaoTacViec(v, 'hoan-tac')}>
                          <i className="bi bi-arrow-counterclockwise me-1" />
                          Hoàn tác
                        </button>
                      ) : null}
                      {v.duongDanBanGhi ? (
                        <Link className="btn btn-sm btn-outline-primary py-0" to={v.duongDanBanGhi}>
                          <i className="bi bi-box-arrow-up-right me-1" />
                          Mở bản ghi
                        </Link>
                      ) : null}
                      {v.nguoiXuLy ? (
                        <span className="small text-body-secondary ms-1">
                          {v.nguoiXuLy}
                          {v.lucXuLy ? ' · ' + gioNgayNgan(v.lucXuLy) : ''}
                        </span>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}

            <PhanTrang {...(nk.duLieu || {})} donVi="việc" doiTrang={(t) => datLoc({ trang: t })} />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
