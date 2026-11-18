import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../api/useApi'
import { BaoLoi, DangTai, HopThoai, NhanTrangThai } from '../../components/ui'
import { useNgheSuKien } from '../../realtime/SuKienContext'
import { dinhDangNgay, dinhDangSo, gioNgayNgan } from '../../utils/dinhDang'
import { duongForm, duongHoSo, soDo } from './chung'
import './container.css'

const hai = (n) => String(n).padStart(2, '0')

const CHU_GIAI = {
  tang: [
    { lop: '', ten: 'Trống' },
    { lop: 't1', ten: '1 tầng' },
    { lop: 't2', ten: '2 tầng' },
    { lop: 't3', ten: '3 tầng' },
    { lop: 't4', ten: '4+ tầng' },
  ],
  trangThai: [
    { lop: '', ten: 'Trống' },
    { lop: 'tt-bai', ten: 'Trong bãi' },
    { lop: 'tt-xuat', ten: 'Chờ xuất tàu' },
    { lop: 'tt-lanh', ten: 'Có cont lạnh' },
  ],
}

function lopO(chong, cheDo) {
  if (!chong) return ''
  if (cheDo === 'trangThai') return chong.coLanh ? 'tt-lanh' : chong.trangThaiTren === 'Chờ xuất tàu' ? 'tt-xuat' : 'tt-bai'
  return 't' + Math.min(4, chong.soTang)
}

/** Mot khu bai: luoi bay (hang ngang) × hang (cot), moi o la mot chong container */
function KhuBai({ khu, cheDo, chonO }) {
  const theoO = new Map(khu.chong.map((c) => [c.bay + '-' + c.hang, c]))
  const cot = Array.from({ length: khu.soHang }, (_, i) => i + 1)
  const hangNgang = Array.from({ length: khu.soBay }, (_, i) => i + 1)

  return (
    <div className="cb-khu">
      <div className="cb-khu-dau">
        <span className="cb-khu-ma">{khu.ma}</span>
        <span className="text-truncate">{khu.ten}</span>
        <span className="ms-auto text-body-secondary fw-normal small text-nowrap">
          {khu.soCont}/{khu.sucChua} · {khu.tyLe}%
        </span>
      </div>
      <div className="cb-luoi" style={{ gridTemplateColumns: `22px repeat(${khu.soHang}, minmax(0, 1fr))` }}>
        <span />
        {cot.map((h) => (
          <span key={'h' + h} className="cb-luoi-nhan" title={'Hàng ' + hai(h)}>
            {hai(h)}
          </span>
        ))}
        {hangNgang.map((b) => (
          <Bay key={'b' + b} khu={khu} bay={b} cot={cot} theoO={theoO} cheDo={cheDo} chonO={chonO} />
        ))}
      </div>
      <div className="cb-thanh-day" role="progressbar" aria-valuenow={khu.tyLe} aria-valuemin={0} aria-valuemax={100} aria-label={`Khu ${khu.ma} lấp đầy ${khu.tyLe}%`}>
        <span style={{ width: Math.min(100, khu.tyLe) + '%' }} />
      </div>
    </div>
  )
}

function Bay({ khu, bay, cot, theoO, cheDo, chonO }) {
  return (
    <>
      <span className="cb-luoi-nhan" title={'Bay ' + hai(bay)}>
        {hai(bay)}
      </span>
      {cot.map((h) => {
        const chong = theoO.get(bay + '-' + h)
        const ma = `${khu.ma}-${hai(bay)}-${hai(h)}`
        const tieuDe =
          `${ma} · ${chong ? chong.soTang : 0} cont` + (chong ? '\n' + chong.cont.map((c) => `${c.soCont} (${c.loaiCont}) — ${c.hangHoa || '—'}`).join('\n') : '')
        return (
          <button
            type="button"
            key={h}
            className={'cb-o ' + lopO(chong, cheDo)}
            title={tieuDe}
            aria-label={`Ô ${ma}: ${chong ? chong.soTang + ' container' : 'trống'}`}
            onClick={() => chonO({ khu, bay, hang: h, chong })}
          >
            {chong ? chong.soTang : ''}
            {chong?.coLanh && (
              <span className="cb-o-lanh" aria-hidden="true">
                <i className="bi bi-snow" />
              </span>
            )}
          </button>
        )
      })}
    </>
  )
}

/** So do bai container theo thoi gian thuc (thay Admin/Containers/YardMap). */
export default function SoDoBai() {
  const { duLieu: sd, loi, dangTai, taiLai } = useApi('/api/so-do-bai')
  const [cheDo, setCheDo] = useState('tang')
  const [chon, setChon] = useState(null)

  const lamMoi = useCallback(() => taiLai({ im: true }), [taiLai])
  useNgheSuKien('bai.*', lamMoi)

  if (!sd) return loi ? <BaoLoi loi={loi} thuLai={taiLai} /> : dangTai ? <DangTai /> : null

  // Chong dang mo trong hop thoai: lay ban moi nhat sau moi lan tai lai
  const chongDangMo = chon ? sd.khu.find((k) => k.ma === chon.khu.ma)?.chong.find((c) => c.bay === chon.bay && c.hang === chon.hang) : null
  const khuDangMo = chon ? sd.khu.find((k) => k.ma === chon.khu.ma) || chon.khu : null
  const tangTiep = chongDangMo ? Math.max(chongDangMo.soTang, chongDangMo.tangCaoNhat) + 1 : 1
  const conCho = khuDangMo ? tangTiep <= khuDangMo.soTangToiDa : false

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="sp-hop-ai mb-3 d-flex flex-wrap align-items-center gap-3">
        <div className="flex-grow-1">
          <span className="sp-nhan-ai">
            <i className="bi bi-grid-3x3-gap me-1" />
            SƠ ĐỒ BÃI
          </span>
          <div className="fw-semibold fs-5 mt-1">Sơ đồ bãi theo thời gian thực</div>
          <div className="sp-chu-nhat small">Cập nhật {gioNgayNgan(sd.lucCapNhat)}</div>
        </div>
        <div className="text-end">
          <div className="sp-so-lon">{dinhDangSo(sd.tongCont)}</div>
          <div className="sp-chu-nhat small">container đang chiếm bãi</div>
        </div>
        <div className="text-end">
          <div className="sp-so-lon">{sd.tyLeLapDay}%</div>
          <div className="sp-chu-nhat small">lấp đầy · sức chứa {dinhDangSo(sd.sucChua)}</div>
        </div>
      </div>

      <div className="card sp-the mb-3">
        <div className="card-body py-2 d-flex flex-wrap align-items-center gap-2">
          <div className="btn-group btn-group-sm" role="group" aria-label="Tô màu ô theo">
            <button type="button" className={'btn ' + (cheDo === 'tang' ? 'btn-primary' : 'btn-outline-primary')} onClick={() => setCheDo('tang')}>
              Số tầng
            </button>
            <button type="button" className={'btn ' + (cheDo === 'trangThai' ? 'btn-primary' : 'btn-outline-primary')} onClick={() => setCheDo('trangThai')}>
              Trạng thái
            </button>
          </div>
          <div className="cb-chu-giai">
            {CHU_GIAI[cheDo].map((m) => (
              <span key={m.ten}>
                <span className={'cb-o cb-o-mau ' + m.lop} />
                {m.ten}
              </span>
            ))}
            {cheDo === 'tang' && (
              <span>
                <i className="bi bi-snow cb-lanh me-1" />
                có container lạnh
              </span>
            )}
          </div>
          <Link className="btn btn-sm btn-primary ms-auto" to={duongForm()} state={{ quayVe: '/admin/containers/yardmap' }}>
            <i className="bi bi-stars me-1" />
            Thêm container (AI gợi ý vị trí)
          </Link>
        </div>
      </div>

      <div className="row g-3">
        {sd.khu.map((k) => (
          <div key={k.ma} className="col-md-6 col-xl-4">
            <KhuBai khu={k} cheDo={cheDo} chonO={setChon} />
          </div>
        ))}
      </div>

      {sd.ngoaiSoDo?.length > 0 && (
        <div className="card sp-the mt-3">
          <div className="card-body py-2 small">
            <i className="bi bi-exclamation-triangle text-warning me-1" />
            <span className="fw-semibold">{sd.ngoaiSoDo.length} container</span> có vị trí ngoài sơ đồ:{' '}
            {sd.ngoaiSoDo.map((c, i) => (
              <span key={c.maCont}>
                {i > 0 && ', '}
                <Link className="cb-so-cont" to={duongHoSo(c.maCont)}>
                  {c.soCont}
                </Link>{' '}
                <span className="cb-vi-tri">{c.viTriBai}</span>
              </span>
            ))}
          </div>
        </div>
      )}

      <HopThoai
        mo={!!chon}
        dong={() => setChon(null)}
        icon="bi-grid-3x3-gap"
        tieuDe={chon ? `Ô ${chon.khu.ma}-${hai(chon.bay)}-${hai(chon.hang)} · ${chongDangMo?.soTang ?? 0}/${khuDangMo?.soTangToiDa} tầng` : ''}
        chan={
          <>
            {conCho && (
              <Link
                className="btn btn-primary"
                to={duongForm() + `?viTriBai=${chon.khu.ma}-${hai(chon.bay)}-${hai(chon.hang)}-${tangTiep}`}
                state={{ quayVe: '/admin/containers/yardmap' }}
              >
                <i className="bi bi-box-arrow-in-down me-1" />
                Hạ container vào ô này
              </Link>
            )}
            <button type="button" className="btn btn-light" onClick={() => setChon(null)}>
              Đóng
            </button>
          </>
        }
      >
        {!chongDangMo ? (
          <div className="text-body-secondary">Ô trống — chưa có container.</div>
        ) : (
          <ul className="list-group list-group-flush">
            {chongDangMo.cont.map((c) => (
              <li key={c.maCont} className="list-group-item px-0">
                <div className="d-flex align-items-center gap-2">
                  <span className="badge text-bg-secondary" title="Tầng">
                    T{c.tang || '?'}
                  </span>
                  <Link className="cb-so-cont" to={duongHoSo(c.maCont)}>
                    {c.soCont}
                  </Link>
                  <span className="small text-body-secondary">
                    {c.loaiCont} · {c.tinhTrang || '—'}
                  </span>
                  <span className="ms-auto">
                    <NhanTrangThai giaTri={c.trangThai} />
                  </span>
                </div>
                <div className="small text-body-secondary mt-1">
                  {c.hangHoa || '—'}
                  {c.khachHang && <> · {c.khachHang}</>}
                  {c.ngayVaoBai && <> · vào bãi {dinhDangNgay(c.ngayVaoBai)}</>}
                  {c.nhietDo != null && (
                    <span className="cb-lanh ms-1">
                      <i className="bi bi-thermometer-snow" /> {soDo(c.nhietDo)}°C
                    </span>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </HopThoai>
    </>
  )
}
