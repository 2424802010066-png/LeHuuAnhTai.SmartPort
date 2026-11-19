import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { goiApi } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BaoLoi, HopThoai, NhanTrangThai, TheChiSo, TheNoiDung, useThongBao } from '../../components/ui'
import { dinhDangNgayGio, thoiGianTruoc } from '../../utils/dinhDang'
import DauTrangKhach from '../dich-vu-khach/DauTrangKhach'
import BieuMauSuCo from './BieuMauSuCo'
import { iconLoai, LOAI_CUA_KHACH, mauMucDo } from './suCo'
import './su-co.css'

// Ten trang thai nhin tu phia khach (khach khong can biet "Đã xử lý" noi bo — voi ho la "Đã trả lời")
const TT_KHACH = {
  Moi: { ten: 'Chờ tiếp nhận', mau: 'info' },
  DangXuLy: { ten: 'Đang xử lý', mau: 'run' },
  DaXuLy: { ten: 'Đã trả lời', mau: 'ok' },
  Dong: { ten: 'Đã đóng', mau: 'off' },
  BaoGia: { ten: 'Đã kết luận', mau: 'off' },
}

function lopThe(s) {
  if (s.treHan) return 'sc-phan-anh tre'
  if (s.trangThai === 'Dong' || s.trangThai === 'BaoGia') return 'sc-phan-anh dong'
  if (s.ketQua) return 'sc-phan-anh xong'
  return 'sc-phan-anh'
}

/** Mot the phan anh / su co (khach chi xem, khong co thao tac) */
function ThePhanAnh({ s, xemAnh, lienQuan }) {
  const tt = TT_KHACH[s.trangThai] || { ten: s.tenTrangThai, mau: 'info' }
  return (
    <div className={lopThe(s)}>
      <div className="d-flex flex-wrap align-items-center gap-1 mb-1">
        <span className="fw-bold me-1">#{s.maSuCo}</span>
        <NhanTrangThai giaTri={s.tenLoai} mau="info" icon={iconLoai(s.loai)} />
        <NhanTrangThai giaTri={s.tenMucDo} mau={mauMucDo(s.mucDo)} />
        {s.tamGiuContainer && <NhanTrangThai giaTri="Container đang tạm giữ" mau="do" icon="bi-lock-fill" />}
        <span className="ms-auto">
          <NhanTrangThai giaTri={tt.ten} mau={tt.mau} />
        </span>
      </div>
      <div className="fw-semibold">{s.tieuDe}</div>
      <div className="small text-body-secondary mb-1" style={{ whiteSpace: 'pre-line' }}>
        {s.moTa}
      </div>
      <div className="small text-body-secondary d-flex flex-wrap gap-3">
        <span title={dinhDangNgayGio(s.thoiDiemBao)}>
          <i className="bi bi-clock me-1" />
          {lienQuan ? 'Cảng ghi nhận ' : 'Gửi '}
          {thoiGianTruoc(s.thoiDiemBao)}
        </span>
        {s.soContainer && (
          <span>
            <i className="bi bi-box-seam me-1" />
            {s.soContainer}
          </span>
        )}
        {s.soLenh && (
          <span>
            <i className="bi bi-qr-code me-1" />
            {s.soLenh}
          </span>
        )}
        {s.coAnh && !lienQuan && (
          <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => xemAnh(s)}>
            <i className="bi bi-image me-1" />
            Xem ảnh
          </button>
        )}
      </div>
      {s.ketQua ? (
        <div className={'sc-ket-qua mt-2' + (s.trangThai === 'BaoGia' ? ' gia' : '')}>
          <div className="fw-semibold mb-1">
            <i className="bi bi-reply-fill me-1" />
            Trả lời của SmartPort{s.nguoiXuLy ? ' · ' + s.nguoiXuLy : ''}
          </div>
          {s.ketQua}
        </div>
      ) : (
        !lienQuan && (
          <div className={'small mt-2 ' + (s.treHan ? 'text-danger fw-semibold' : 'text-body-secondary')}>
            <i className="bi bi-hourglass-split me-1" />
            {s.trangThai === 'DangXuLy' && s.nguoiXuLy ? s.nguoiXuLy + ' đang xử lý · ' : ''}
            {s.canhBaoHan || 'SmartPort sẽ trả lời trong 24 giờ'}
          </div>
        )
      )}
    </div>
  )
}

/** /bao-su-co — KHACH HANG bao su co, phan anh, khieu nai (UC22, UC26) va theo doi tra loi */
export default function BaoSuCoKhach() {
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/su-co/cua-toi')
  const { duLieu: dt } = useApi('/api/su-co/doi-tuong-cua-toi')
  const bao = useThongBao()
  const [anh, setAnh] = useState(null)

  // Khach khong co luong realtime (SSE chi danh cho nhan vien): hoi lai moi 60 giay de thay tra loi moi
  useEffect(() => {
    const lap = setInterval(() => taiLai({ im: true }), 60000)
    return () => clearInterval(lap)
  }, [taiLai])

  async function xemAnh(s) {
    setAnh({ maSuCo: s.maSuCo, src: null })
    try {
      const ct = await goiApi('/api/su-co/' + s.maSuCo)
      setAnh({ maSuCo: s.maSuCo, src: ct.anhBase64 })
    } catch (e) {
      setAnh(null)
      bao.loi(e)
    }
  }

  const container = (dt?.container || []).map((c) => ({ soCont: c.soCont, nhan: `${c.soCont} · ${c.loaiCont || ''} · ${c.trangThai || ''}` }))
  const lenh = (dt?.lenh || []).map((l) => ({
    maLenh: l.maLenh,
    soCont: l.soCont,
    nhan: `${l.soLenh || '#' + l.maLenh} · ${l.tenLoaiLenh}${l.soCont ? ' · ' + l.soCont : ''} · ${l.trangThai}`,
  }))

  return (
    <>
      <DauTrangKhach
        icon="bi-megaphone"
        tieuDe="Báo sự cố, phản ánh"
        duongDan={[{ ten: 'Hàng & lệnh của tôi', den: '/hangcuatoi' }, { ten: 'Báo sự cố, phản ánh' }]}
      />

      <div className="container py-4">
        <BaoLoi loi={loi} thuLai={taiLai} />
        <div className="row g-4">
          <div className="col-lg-5">
            <TheNoiDung tieuDe="Gửi phản ánh mới" icon="bi-pencil-square">
              <p className="small text-body-secondary">
                Hàng hóa hư hỏng, container sai lệch, hóa đơn – thanh toán chưa đúng hay dịch vụ chưa hài lòng: hãy cho SmartPort biết. Mọi phản ánh
                được ghi nhận, <b>không bị xóa</b> và được trả lời <b>trong 24 giờ</b>.
              </p>
              <BieuMauSuCo
                cacLoai={LOAI_CUA_KHACH}
                container={container}
                lenh={lenh}
                goiYViTri="VD: Cổng chính, kho CFS…"
                khiXong={() => taiLai({ im: true })}
              />
            </TheNoiDung>
            <div className="small text-body-secondary">
              <i className="bi bi-info-circle me-1" />
              Thanh toán đã trừ tiền nhưng lệnh chưa hiệu lực? Chọn loại <b>Thanh toán, hóa đơn</b> — cảng đối soát trong ngày, không kích hoạt được
              lệnh thì hoàn tiền. Xem giao dịch tại <Link to="/payment/history">Lịch sử thanh toán</Link>.
            </div>
          </div>

          <div className="col-lg-7">
            <div className="row g-3 mb-3">
              <div className="col-6">
                <TheChiSo icon="bi-hourglass-split" mau="cam" so={d?.soDangMo ?? 0} nhan="Đang chờ trả lời" />
              </div>
              <div className="col-6">
                <TheChiSo icon="bi-reply-all" mau="la" so={d?.soDaTraLoi ?? 0} nhan="Đã được trả lời" />
              </div>
            </div>

            <TheNoiDung
              tieuDe="Phản ánh của tôi"
              icon="bi-chat-left-dots"
              phai={
                <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => taiLai()} disabled={dangTai}>
                  <i className="bi bi-arrow-clockwise me-1" />
                  Làm mới
                </button>
              }
            >
              {dangTai && !d ? (
                <div className="sc-trong">
                  <span className="spinner-border spinner-border-sm me-2" />
                  Đang tải…
                </div>
              ) : !d?.cuaToi?.length ? (
                <div className="sc-trong">
                  <i className="bi bi-inbox d-block mb-2 fs-3" />
                  Bạn chưa gửi phản ánh nào.
                </div>
              ) : (
                d.cuaToi.map((s) => <ThePhanAnh key={s.maSuCo} s={s} xemAnh={xemAnh} />)
              )}
            </TheNoiDung>

            {d?.lienQuan?.length > 0 && (
              <TheNoiDung tieuDe="Sự cố cảng ghi nhận liên quan hàng của tôi" icon="bi-box-seam">
                <p className="small text-body-secondary">Cảng thông báo các sự cố liên quan container / lệnh của doanh nghiệp bạn (kèm email).</p>
                {d.lienQuan.map((s) => (
                  <ThePhanAnh key={s.maSuCo} s={s} xemAnh={xemAnh} lienQuan />
                ))}
              </TheNoiDung>
            )}
          </div>
        </div>
      </div>

      <HopThoai mo={!!anh} dong={() => setAnh(null)} tieuDe={'Ảnh đã gửi — phản ánh #' + (anh?.maSuCo ?? '')} icon="bi-image" kichThuoc="lg">
        {anh?.src ? (
          <img src={anh.src} alt={'Ảnh phản ánh #' + anh.maSuCo} className="w-100 rounded" />
        ) : (
          <div className="sc-trong">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải ảnh…
          </div>
        )}
      </HopThoai>
    </>
  )
}
