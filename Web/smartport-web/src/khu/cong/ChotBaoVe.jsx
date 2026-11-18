import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import ThanhTrangThaiMang from '../../components/ThanhTrangThaiMang'
import { tenLoaiLenh } from '../../ngoaiTuyen/banSao.js'
import { LY_DO_VUOT_MAY_CHU_CHAM } from '../../ngoaiTuyen/hopThuDi.js'
import { doiChieuLenhTaiTram, ghiLuotXeTaiTram, tram } from '../../ngoaiTuyen/tram'
import { useTrangThaiMang } from '../../ngoaiTuyen/useTrangThaiMang'
import './cong.css'

const CONG_MAC_DINH = 'A1'

const gioPhut = (v) => {
  const d = v ? new Date(v) : new Date()
  return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0')
}
const dinhDangTien = (n) => Number(n).toLocaleString('vi-VN') + ' đ'

// So dem VAO / RA + 6 luot gan nhat CUA HOM NAY luu trong may: tai lai trang luc mat mang khong bi ve 0
// (may chu tra loi duoc thi so cua may chu ghi de). Sang ngay moi thi bo so cu.
const KHOA_DEM = 'smartport.chotCong.dem.' + CONG_MAC_DINH
const ngayHomNay = () => new Date().toDateString()
function docDemLuu() {
  try {
    const v = JSON.parse(localStorage.getItem(KHOA_DEM))
    return v && v.ngay === ngayHomNay() ? v : null
  } catch {
    return null
  }
}
function luuDem(dem, ganNhat) {
  try {
    localStorage.setItem(KHOA_DEM, JSON.stringify({ ngay: ngayHomNay(), dem, ganNhat }))
  } catch {
    /* trinh duyet chan localStorage: chi mat so dem khi tai lai, khong anh huong ghi luot */
  }
}

// Khung ket qua VANG (ghi tam / doi chieu ban sao) — cong.css chi co xanh / do
const KQ_VANG = { background: '#713f12', border: '4px solid #f59e0b' }

/** Dong mo ta lenh (may chu tra LenhKhop; ban sao tra muc lenhMo) */
function dongLenh(lenh, nguon) {
  if (!lenh) return []
  const loai = lenh.loai || tenLoaiLenh(lenh.loaiLenh)
  const dong = ['Lệnh ' + (lenh.soLenh || '#' + lenh.maLenh) + (loai ? ' · ' + loai : '')]
  if (lenh.soCont) dong.push('Container ' + lenh.soCont)
  if (lenh.taiXe) dong.push('Tài xế: ' + lenh.taiXe)
  if (nguon === 'may-chu') {
    if (lenh.hen) dong.push('Hẹn: ' + lenh.hen)
    if (lenh.daCheckIn) dong.push('Đã check-in online — ƯU TIÊN')
    if (lenh.dungGio === false) dong.push('NGOÀI KHUNG GIỜ HẸN')
  }
  return dong
}

/**
 * MAN HINH CHOT CONG CHO BAO VE (thay Admin/Gate/BaoVe) — thiet ke cho nguoi dung nhieu nhat
 * nhung it duoc quan tam nhat: chu to, tuong phan cao (doc duoc ban dem / ngoai nang), CHI 3 NUT,
 * bam duoc bang tay ban, va VAN GHI DUOC KHI MAT MANG.
 * Ngoai tuyen (src/ngoaiTuyen): moi lan bam ghi XUONG IndexedDB truoc, co mang thi gui ngay (Idempotency-Key = maThaoTac),
 * mat mang / qua gio / 502-504 thi de trong hop thu di va tu dong bo; may chu TU CHOI (4xx, vd "KHONG CHO RA CONG")
 * thi hien DO va KHONG xep hang. Mat mang thi doi chieu lenh / eDO / the xe bang BAN SAO (ghi ro gio ban sao).
 * XE RA luc tram dang TOI DUOC may chu ma may chu khong tra loi kip (qua gio / loi mang / 502-504): DO "CHUA XAC NHAN
 * DUOC — CHUA CHO XE RA", chi 2 loi ra: Thu lai (cung maThaoTac) hoac Van cho qua (ghi tam, mang co can doi soat) —
 * KHONG tu xep hang (xep hang = may chu ghi tre, eDO het han chi bi gan can doi soat thay vi bi chan).
 */
export default function ChotBaoVe() {
  const { taiKhoan } = useAuth()
  const mang = useTrangThaiMang()
  const [bienSo, setBienSo] = useState('')
  const [dem, setDem] = useState(() => docDemLuu()?.dem ?? { vao: 0, ra: 0 })
  const [ganNhat, setGanNhat] = useState(() => docDemLuu()?.ganNhat ?? [])
  const [kq, setKq] = useState(null) // { loai: 'xanh'|'do'|'vang', tieuDe, dong: [], vuot? }
  const [dangGhi, setDangGhi] = useState(false)
  const [tinDongBo, setTinDongBo] = useState('')
  const oNhap = useRef(null)
  const dongBoTruoc = useRef(null)

  const taiTongHop = useCallback(async () => {
    try {
      const [t, ds] = await Promise.all([goiApi('/api/cong/tong-hop-hom-nay', { timeoutMs: 8000 }), goiApi('/api/cong/luot-xe?soLuong=6', { timeoutMs: 8000 })])
      setDem({ vao: t?.xeVao ?? 0, ra: t?.xeRa ?? 0 })
      setGanNhat((ds || []).map((x) => ({ bienSo: x.bienSo, huongDi: x.huongDi, gio: gioPhut(x.thoiGian) })))
    } catch {
      /* mat mang: giu so dem trong may, khong chan man hinh */
    }
  }, [])

  useEffect(() => {
    taiTongHop()
    oNhap.current?.focus()
  }, [taiTongHop])

  useEffect(() => luuDem(dem, ganNhat), [dem, ganNhat])

  // Mo trang luc mat mang roi co mang lai ma khong co luot cho gui (khong co lo dong bo nao) -> van lay lai so cua may chu
  // (coMang null = dang kiem lan dau: mo trang luc co mang thi useEffect tren da tai roi, khong tai lan 2)
  const tinhTrangMayChu = mang.coMang === null ? 'chua-biet' : mang.coMang && !mang.congNgung ? 'toi-duoc' : 'mat'
  const tinhTrangTruoc = useRef(tinhTrangMayChu)
  useEffect(() => {
    const truoc = tinhTrangTruoc.current
    tinhTrangTruoc.current = tinhTrangMayChu
    if (tinhTrangMayChu === 'toi-duoc' && truoc === 'mat') taiTongHop()
  }, [tinhTrangMayChu, taiTongHop])

  // Man chot cong can ban sao lenh / eDO / the xe cua cong nay (tram tu tai lai moi 3 phut, co ETag)
  useEffect(() => tram.dungBanSaoCho(CONG_MAC_DINH), [])

  // Vua dong bo xong mot lo -> bao ngan 6 giay + lam moi so dem tu may chu
  useEffect(() => {
    const d = mang.dongBoCuoi
    if (!d || d === dongBoTruoc.current) return undefined
    const lanDau = dongBoTruoc.current === null
    dongBoTruoc.current = d
    if (lanDau && Date.now() - d.luc > 10000) return undefined
    const tong = d.daGui + d.trung
    setTinDongBo(
      'Đã gửi lên hệ thống ' + tong + ' lượt xe ghi tạm' + (d.canDoiSoat ? ' · ' + d.canDoiSoat + ' lượt cần đối soát' : '') + (d.loi ? ' · ' + d.loi + ' lượt bị từ chối' : '') + '.',
    )
    taiTongHop()
    const hen = setTimeout(() => setTinDongBo(''), 6000)
    return () => clearTimeout(hen)
  }, [mang.dongBoCuoi, taiTongHop])

  function demLuot(bs, huongDi) {
    setGanNhat((cu) => [{ bienSo: bs, huongDi, gio: gioPhut() }, ...cu].slice(0, 6))
    setDem((cu) => (huongDi === 'Vào' ? { ...cu, vao: cu.vao + 1 } : { ...cu, ra: cu.ra + 1 }))
  }

  async function ghi(huongDi, { vuot = null } = {}) {
    const bs = (vuot?.bienSo ?? bienSo).trim().toUpperCase()
    if (bs.length < 4) {
      setKq({ loai: 'do', tieuDe: 'CHƯA NHẬP BIỂN SỐ', dong: ['Gõ biển số xe rồi bấm lại.'] })
      oNhap.current?.focus()
      return
    }
    setDangGhi(true)
    try {
      // 1) Doi chieu lenh: co mang -> may chu; mat mang -> ban sao (KHONG bao nham "KHONG CO LENH")
      const dc = await doiChieuLenhTaiTram(bs, { huongDi, cong: CONG_MAC_DINH })
      const nhanBanSao = dc.banSaoLuc ? 'bản sao lúc ' + gioPhut(dc.banSaoLuc) + (dc.banSaoCu ? ' (đã cũ)' : '') : ''

      // Mat mang + ban sao bao vi pham ro rang (bien cam, eDO huy / da dung / het han / thieu khi che do chan) -> mac dinh KHONG cho qua
      if (dc.nguon === 'ban-sao' && dc.chan && !vuot) {
        setKq({
          loai: 'do',
          tieuDe: 'KHÔNG CHO QUA (theo bản sao)',
          dong: [dc.chan, 'Đang mất mạng — đối chiếu ' + nhanBanSao + '. Chưa ghi lượt xe.', 'Gọi điều độ trước khi quyết định.'],
          vuot: { bienSo: bs, huongDi, lyDo: dc.chan },
        })
        return
      }

      const lenh = dc.lenh
      const ngu = { bs, huongDi, dc, lenh, nhanBanSao, vuot, phu: { nguon: 'ChotCong' }, lenhBiet: dc.nguon === 'may-chu' ? lenh : null }
      const kqGhi = await ghiLuotXeTaiTram(
        {
          bienSo: bs,
          huongDi,
          cong: CONG_MAC_DINH,
          loaiXe: lenh ? 'Đầu kéo container' : null,
          maLenh: lenh?.maLenh ?? null,
          soCont: lenh?.soCont ?? null,
          ghiChu: vuot ? 'Bảo vệ cho qua dù bản sao báo: ' + vuot.lyDo : null,
          nguoiThaoTac: taiKhoan?.hoTen || taiKhoan?.tenDangNhap || null,
        },
        {
          phu: ngu.phu,
          lenhBiet: ngu.lenhBiet,
          vuotBanSao: !!vuot,
          lyDoVuot: vuot ? 'Bảo vệ cho qua dù bản sao báo: ' + vuot.lyDo : null,
          mucCu: vuot?.muc ?? null, // vuot sau "Thu lai" -> giu maThaoTac cu (lan dau co the da toi may chu)
          timeoutMs: 8000,
        },
      )
      if (hienKetQua(kqGhi, ngu)) setBienSo('')
    } finally {
      setDangGhi(false)
      oNhap.current?.focus()
    }
  }

  /** Hien ket qua 1 lan ghi; tra true neu xong luot (xoa o bien so) */
  function hienKetQua(kqGhi, ngu) {
    const { bs, huongDi, dc, lenh, nhanBanSao, vuot } = ngu
    if (kqGhi.ketQua === 'da-ghi') {
      demLuot(bs, huongDi)
      const canhBao = kqGhi.duLieu?.canhBao
      if (lenh) setKq({ loai: 'xanh', tieuDe: 'KHỚP LỆNH — CHO QUA', dong: [...dongLenh(lenh, dc.nguon), ...(canhBao ? [canhBao] : [])] })
      else
        setKq({
          loai: 'do',
          tieuDe: 'KHÔNG CÓ LỆNH',
          dong: ['Xe ' + bs + ' không khớp lệnh giao nhận nào đang hiệu lực.', 'Hỏi tài xế số lệnh, hoặc gọi điều độ.', ...(canhBao ? [canhBao] : [])],
        })
      return true
    }
    if (kqGhi.ketQua === 'chua-xac-nhan') {
      // XE RA, tram tin la toi duoc may chu nhung may chu chua tra loi: CHUA biet eDO / hai quan -> KHONG cho ra, KHONG xep hang
      const lyDo =
        kqGhi.lyDo === 'het-phien'
          ? 'Phiên đăng nhập hết hạn / bị thu hồi — đăng nhập lại rồi bấm Thử lại.'
          : kqGhi.lyDo === 'dang-xu-ly'
            ? 'Máy chủ đang xử lý lần bấm trước — đợi vài giây rồi bấm Thử lại.'
            : (kqGhi.loi?.message || 'Máy chủ không trả lời.') + ' Chưa biết lệnh / eDO / hải quan có hợp lệ không.'
      setKq({
        loai: 'do',
        tieuDe: 'CHƯA XÁC NHẬN ĐƯỢC — CHƯA CHO XE RA',
        dong: [
          lyDo,
          'Lượt xe CHƯA được ghi. Bấm THỬ LẠI (không ghi trùng) — nếu trạm đã mất mạng hẳn, Thử lại sẽ đối chiếu bản sao và ghi tạm.',
          'Chỉ bấm VẪN CHO QUA khi điều độ đồng ý — lượt sẽ gắn CẦN ĐỐI SOÁT.',
        ],
        xacNhan: { ...ngu, muc: kqGhi.muc },
      })
      return false
    }
    if (kqGhi.ketQua === 'da-ghi-tam' || kqGhi.ketQua === 'cho-dang-nhap') {
      // Chi den day khi muc DA nam trong IndexedDB (ghiLuotXe cho giao dich xong)
      demLuot(bs, huongDi)
      const dong = [...dongLenh(lenh, dc.nguon)]
      if (dc.nguon === 'ban-sao')
        dong.unshift(
          lenh
            ? 'Đối chiếu ' + nhanBanSao + ':'
            : dc.khongDayDu
              ? 'Bản sao không đầy đủ (bị cắt) — không kiểm được lệnh, kiểm giấy tờ bằng mắt / hỏi điều độ.'
              : 'B' + nhanBanSao.slice(1) + ' không có lệnh cho xe này — hỏi tài xế số lệnh.',
        )
      if (dc.nguon === 'khong-kiem-duoc') dong.unshift('Chưa có bản sao lệnh trong máy — không kiểm được lệnh, kiểm giấy tờ bằng mắt.')
      dong.push(...(dc.canhBao || []))
      if (dc.phiTamTinh != null) dong.push('Phí cổng tạm tính ' + dinhDangTien(dc.phiTamTinh) + ' — thu qua thẻ / công nợ khi có mạng.')
      if (ngu.vuotMayChu) dong.push('Bảo vệ cho qua khi máy chủ chưa trả lời — máy chủ sẽ gắn CẦN ĐỐI SOÁT khi đồng bộ.')
      else if (vuot) dong.push('Đã ghi kèm lý do cho qua — máy chủ sẽ gắn CẦN ĐỐI SOÁT khi đồng bộ.')
      dong.push(
        kqGhi.ketQua === 'cho-dang-nhap'
          ? 'Phiên đăng nhập hết hạn — lượt xe đã giữ trong máy, đăng nhập lại để gửi.'
          : ngu.vuotMayChu
            ? 'Đã ghi tạm trong máy — tự gửi lên hệ thống, không gửi trùng.'
            : 'Đã ghi tạm trong máy (mất mạng) — tự gửi lên khi có mạng lại.',
      )
      setKq({ loai: 'vang', tieuDe: ngu.vuotMayChu || vuot ? 'ĐÃ GHI TẠM — CẦN ĐỐI SOÁT' : lenh ? 'ĐÃ GHI TẠM — KHỚP LỆNH' : 'ĐÃ GHI TẠM', dong })
      return true
    }
    if (kqGhi.ketQua === 'chan-ban-sao') {
      setKq({
        loai: 'do',
        tieuDe: 'KHÔNG CHO QUA (theo bản sao)',
        dong: [kqGhi.lyDo, 'Mất mạng giữa chừng — chưa ghi lượt xe.', 'Gọi điều độ trước khi quyết định.'],
        vuot: { bienSo: bs, huongDi, lyDo: kqGhi.lyDo, muc: kqGhi.muc },
      })
      return false
    }
    if (kqGhi.ketQua === 'bi-tu-choi') {
      // 4xx (vd "KHONG CHO RA CONG: eDO...") hoac 500: may chu DA tra loi -> KHONG xep hang, KHONG cho qua
      const ma = kqGhi.loi?.trangThai
      setKq({
        loai: 'do',
        tieuDe: ma >= 500 ? 'CHƯA GHI ĐƯỢC — LỖI MÁY CHỦ' : 'KHÔNG CHO QUA',
        dong: [kqGhi.loi?.message || 'Máy chủ từ chối.', 'Lượt xe CHƯA được ghi.', ma >= 500 ? 'Bấm lại, hoặc IN PHIẾU TAY nếu lỗi kéo dài.' : 'Gọi điều độ nếu cần xử lý.'],
      })
      return false
    }
    // khong-luu-duoc: mat mang VA khong ghi duoc xuong may (bo nho day / bi chan)
    setKq({
      loai: 'do',
      tieuDe: 'KHÔNG LƯU ĐƯỢC TRONG MÁY',
      dong: ['Mất mạng và trình duyệt không cho lưu tạm (' + (kqGhi.loi?.message || 'bộ nhớ đầy') + ').', 'Bấm IN PHIẾU TAY và ghi tay lượt này.'],
    })
    return false
  }

  /** "Thu lai" sau CHUA XAC NHAN: gui lai DUNG muc cu (cung maThaoTac = Idempotency-Key) — tram tinh lai co mang hay khong */
  async function thuLaiXacNhan(xn) {
    setDangGhi(true)
    try {
      const kqGhi = await ghiLuotXeTaiTram(null, { mucCu: xn.muc, phu: xn.phu, lenhBiet: xn.lenhBiet, vuotBanSao: !!xn.vuot, timeoutMs: 8000 })
      if (hienKetQua(kqGhi, xn)) setBienSo('')
    } finally {
      setDangGhi(false)
      oNhap.current?.focus()
    }
  }

  /** "Van cho qua" sau CHUA XAC NHAN: xep hang muc do (cung maThaoTac) kem vuotBanSao + lyDoVuot -> may chu gan can doi soat */
  async function vanChoQua(xn) {
    setDangGhi(true)
    try {
      const kqGhi = await ghiLuotXeTaiTram(null, { mucCu: xn.muc, phu: xn.phu, vuotBanSao: true, lyDoVuot: LY_DO_VUOT_MAY_CHU_CHAM, xepHangLuon: true })
      if (hienKetQua(kqGhi, { ...xn, vuotMayChu: true })) setBienSo('')
    } finally {
      setDangGhi(false)
      oNhap.current?.focus()
    }
  }

  function xoa() {
    setBienSo('')
    setKq(null)
    oNhap.current?.focus()
  }

  const soCho = mang.soCho + mang.soDangGui
  const kieuKq = kq?.loai === 'xanh' ? 'co' : kq?.loai === 'do' ? 'khong' : ''

  return (
    <div className="cg-chot" data-bs-theme="dark">
      <div className="cg-chot-thanh">
        <h1>CHỐT CỔNG {CONG_MAC_DINH}</h1>
        <div className="cg-chot-dem">
          <span>
            VÀO <b>{dem.vao}</b>
          </span>
          <span>
            RA <b>{dem.ra}</b>
          </span>
        </div>
        <ThanhTrangThaiMang lon />
      </div>

      <div className="cg-chot-khung">
        <label className="cg-chot-nhan" htmlFor="cg-chot-bs">
          BIỂN SỐ XE
        </label>
        <input
          id="cg-chot-bs"
          ref={oNhap}
          className="cg-chot-o"
          value={bienSo}
          onChange={(e) => setBienSo(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !dangGhi) ghi('Vào')
          }}
          autoComplete="off"
          autoCapitalize="characters"
          maxLength={20}
          placeholder="51C-638.72"
        />

        <div className="cg-chot-hang">
          <button type="button" className="cg-chot-vao" onClick={() => ghi('Vào')} disabled={dangGhi}>
            ⬅ XE VÀO
          </button>
          <button type="button" className="cg-chot-ra" onClick={() => ghi('Ra')} disabled={dangGhi}>
            XE RA ➡
          </button>
        </div>
        <div className="cg-chot-phu">
          <button type="button" onClick={xoa}>
            ✖ XÓA
          </button>
          <Link to={'/admin/gate/phieutay?bienSo=' + encodeURIComponent(bienSo.trim().toUpperCase())} target="_blank" rel="noreferrer">
            🖨 IN PHIẾU TAY
          </Link>
        </div>

        {tinDongBo ? (
          <div className="cg-chot-bang xong">{tinDongBo}</div>
        ) : soCho > 0 ? (
          <div className="cg-chot-bang">
            ⏳ <b>{soCho}</b> lượt xe đang chờ gửi lên hệ thống (đã ghi an toàn trong máy).{' '}
            <button type="button" className="btn btn-sm btn-warning ms-2" onClick={() => tram.dongBoNgay()} disabled={mang.dangDongBo}>
              {mang.dangDongBo ? 'Đang gửi…' : 'Gửi ngay'}
            </button>
          </div>
        ) : null}
        {mang.soLoi > 0 && (
          <div className="cg-chot-bang" style={{ background: '#7f1d1d', borderColor: '#ef4444' }}>
            🔴 <b>{mang.soLoi}</b> lượt bị máy chủ từ chối khi gửi lại — bấm thanh trạng thái phía trên để xem / thử lại.
          </div>
        )}

        {kq && (
          <div className={'cg-chot-kq ' + kieuKq} style={kq.loai === 'vang' ? KQ_VANG : undefined} role="status" aria-live="polite">
            <div className="cg-chot-kq-tieu">
              {kq.loai === 'xanh' ? '✅ ' : kq.loai === 'vang' ? '🟡 ' : '⛔ '}
              {kq.tieuDe}
            </div>
            <div className="cg-chot-kq-ct">
              {kq.dong.map((c, i) => (
                <div key={i}>{c}</div>
              ))}
            </div>
            {kq.vuot && (
              <button
                type="button"
                className="btn btn-outline-light mt-3 fw-bold"
                disabled={dangGhi}
                onClick={() => ghi(kq.vuot.huongDi, { vuot: kq.vuot })}
                title="Chỉ khi điều độ đồng ý — lượt xe được ghi và gắn cờ cần đối soát"
              >
                VẪN CHO QUA (điều độ đồng ý · ghi cần đối soát)
              </button>
            )}
            {kq.xacNhan && (
              <div className="d-flex flex-wrap gap-2 mt-3">
                <button type="button" className="btn btn-light fw-bold" disabled={dangGhi} onClick={() => thuLaiXacNhan(kq.xacNhan)}>
                  {dangGhi ? 'ĐANG GỬI…' : '↻ THỬ LẠI'}
                </button>
                <button
                  type="button"
                  className="btn btn-outline-light fw-bold"
                  disabled={dangGhi}
                  onClick={() => vanChoQua(kq.xacNhan)}
                  title="Chỉ khi điều độ đồng ý — lượt xe được ghi tạm và gắn cờ cần đối soát"
                >
                  Vẫn cho qua (ghi tạm, cần đối soát)
                </button>
              </div>
            )}
          </div>
        )}

        <div className="mt-4">
          <h2 className="cg-chot-nhan">LƯỢT GẦN NHẤT</h2>
          {ganNhat.length === 0 ? (
            <div className="cg-chot-dong">
              <span>Chưa có lượt nào hôm nay</span>
            </div>
          ) : (
            ganNhat.map((x, i) => (
              <div className="cg-chot-dong" key={x.bienSo + x.gio + i}>
                <span className="fw-bold">{x.bienSo}</span>
                <span className={'cg-chot-huong ' + (x.huongDi === 'Vào' ? 'v' : 'r')}>{x.huongDi}</span>
                <span>{x.gio}</span>
              </div>
            ))
          )}
        </div>

        <div className="cg-chot-ghi">
          Mất mạng vẫn bấm được — máy ghi vào bộ nhớ trong (IndexedDB) trước, tự gửi lên khi có mạng lại, không gửi trùng.
          <br />
          Hệ thống hỏng hoàn toàn thì bấm <b>IN PHIẾU TAY</b>, ghi tay rồi nhập bù sau — không kẹt xe nào.
          {typeof window !== 'undefined' && !window.isSecureContext && (
            <>
              <br />
              <span style={{ color: '#fbbf24' }}>
                ⚠ Đang mở bằng HTTP qua mạng LAN: mất mạng thì ĐỪNG tải lại trang (trình duyệt chỉ lưu giao diện khi dùng https://&lt;máy-chủ&gt;:5443).
              </span>
            </>
          )}
          <br />
          <Link to="/admin/gate">Màn hình đầy đủ (có camera AI)</Link> · <Link to="/admin/gate/history">Nhật ký ra vào</Link>
        </div>
      </div>
    </div>
  )
}
