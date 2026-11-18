import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useSuKien } from '../realtime/SuKienContext'
import { MAU_MODULE } from './DongSuKien'

const ICON = {
  xong: 'bi-check-circle-fill text-success',
  'bo-qua': 'bi-dash-circle text-muted',
  'canh-bao': 'bi-exclamation-triangle-fill text-warning',
  'qua-gio': 'bi-clock-history text-danger',
}

function Buoc({ trangThai, module, children }) {
  return (
    <li className="d-flex gap-2 align-items-start py-1">
      {trangThai === 'cho' ? <span className="spinner-border spinner-border-sm text-primary mt-1 flex-shrink-0" /> : <i className={'bi mt-1 ' + ICON[trangThai]} />}
      <span className={'badge mt-1 ' + (MAU_MODULE[module] || 'text-bg-secondary')}>{module}</span>
      <span className={trangThai === 'bo-qua' ? 'text-muted' : ''}>{children}</span>
    </li>
  )
}

/**
 * TIEN TRINH XU LY QUA SU KIEN cua mot luot xe vua ghi: Cong xong ngay; Bai va Tai chinh
 * tick xanh khi su kien cua ho (cung maXV) ve toi qua luong realtime.
 */
export default function TienTrinhSuKien({ ketQua }) {
  const { ganDay } = useSuKien()
  const [quaGio, setQuaGio] = useState(false)

  useEffect(() => {
    setQuaGio(false)
    const hen = setTimeout(() => setQuaGio(true), 10000)
    return () => clearTimeout(hen)
  }, [ketQua])

  const cuaXe = ganDay.filter((s) => s.maXV === ketQua.maXV)
  const bai = cuaXe.find((s) => s.loai.startsWith('bai.'))
  const phiCong = cuaXe.find((s) => s.loai === 'tai-chinh.phi-cong-da-xu-ly')
  const phiLuuBai = cuaXe.find((s) => s.loai === 'tai-chinh.phi-luu-bai-da-chot')
  const choPhiLuuBai = bai?.loai === 'bai.container-da-roi-bai'
  const conCho = (ketQua.choBai && !bai) || (ketQua.choPhiCong && !phiCong) || (choPhiLuuBai && !phiLuuBai)
  const dangCho = (suKien) => (suKien ? 'xong' : quaGio ? 'qua-gio' : 'cho')

  return (
    <div className="sp-tien-trinh mt-3">
      <div className="fw-semibold small mb-1">
        <i className="bi bi-diagram-3 me-1" />
        Tiến trình xử lý qua sự kiện {conCho ? (quaGio ? '— chậm hơn bình thường' : '') : '— hoàn tất'}
      </div>
      <ul className="list-unstyled small mb-0">
        <Buoc trangThai="xong" module="Cổng">
          Đã ghi lượt xe <b>{ketQua.bienSo}</b> ({ketQua.huongDi} cổng {ketQua.cong} lúc {ketQua.gio})
          {ketQua.dock && (
            <>
              {' '}
              · dock <b>{ketQua.dock}</b>
            </>
          )}
          {ketQua.anh && ' · 📷 kèm ảnh'}
          {ketQua.soLenh && <> · lệnh {ketQua.soLenh}</>}
        </Buoc>
        {ketQua.canhBao && (
          <Buoc trangThai="canh-bao" module="Cổng">
            {ketQua.canhBao}
          </Buoc>
        )}

        {ketQua.choBai ? (
          <Buoc trangThai={bai ? (bai.loai === 'bai.container-khong-doi' ? 'canh-bao' : 'xong') : dangCho(null)} module="Bãi">
            {bai ? (
              bai.moTa
            ) : quaGio ? (
              'Chưa nhận sự kiện từ module Bãi — kiểm tra bộ phát sự kiện'
            ) : (
              <>
                Đang đồng bộ container <Link to={'/admin/containers/tracuu?so=' + ketQua.soCont}>{ketQua.soCont}</Link>…
              </>
            )}
          </Buoc>
        ) : (
          <Buoc trangThai="bo-qua" module="Bãi">
            Không áp dụng — xe không gắn lệnh có container
          </Buoc>
        )}

        {choPhiLuuBai && (
          <Buoc trangThai={dangCho(phiLuuBai)} module="Tài chính">
            {phiLuuBai ? phiLuuBai.moTa : quaGio ? 'Chưa nhận sự kiện chốt phí lưu bãi' : 'Đang chốt phí lưu bãi…'}
          </Buoc>
        )}

        {ketQua.choPhiCong ? (
          <Buoc trangThai={phiCong ? (phiCong.canhBao ? 'canh-bao' : 'xong') : dangCho(null)} module="Tài chính">
            {phiCong ? phiCong.moTa : quaGio ? 'Chưa nhận sự kiện phí cổng' : 'Đang tính phí cổng…'}
          </Buoc>
        ) : (
          <Buoc trangThai="bo-qua" module="Tài chính">
            Phí cổng: không áp dụng — chỉ thu khi xe ra
          </Buoc>
        )}
      </ul>
    </div>
  )
}
