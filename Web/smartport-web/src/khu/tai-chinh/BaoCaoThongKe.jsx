import { useMemo } from 'react'
import { Bar } from 'react-chartjs-2'
import { taiFile, taoQuery } from '../../api/client'
import { useApi } from '../../api/useApi'
import { BangDuLieu, BaoLoi, TheChiSo, TheNoiDung } from '../../components/ui'
import { useBoLoc } from '../../hooks/useBoLoc'
import { useThaoTac } from '../../hooks/useThaoTac'
import { dinhDangSo, dinhDangTien } from '../../utils/dinhDang'
import '../../utils/dangKyBieuDo'
import { tienO } from './dungChung'
import './tai-chinh.css'

const TY = 1e9

/** Bao cao thong ke nam: san luong TEU + doanh thu khai thac + doanh thu dich vu ePort (thay Admin/Reports). */
export default function BaoCaoThongKe() {
  const [loc, datLoc] = useBoLoc({ nam: '' })
  const { duLieu: d, loi, dangTai, taiLai } = useApi('/api/bao-cao-thong-ke' + taoQuery(loc))
  const { chay, dangChay } = useThaoTac()

  const bieuDo = useMemo(() => {
    const rows = d?.sanLuong || []
    return {
      labels: rows.map((r) => 'T' + r.thang),
      datasets: [
        { type: 'bar', label: 'TEU', data: rows.map((r) => r.teu), backgroundColor: '#1173c4', borderRadius: 6, yAxisID: 'y', maxBarThickness: 38 },
        {
          type: 'line',
          label: 'Doanh thu (tỷ đồng)',
          data: rows.map((r) => Math.round((r.doanhThu / TY) * 100) / 100),
          borderColor: '#ff7d2d',
          backgroundColor: '#ff7d2d',
          tension: 0.35,
          yAxisID: 'y1',
        },
      ],
    }
  }, [d])

  const tuyChonBieuDo = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } },
    scales: {
      y: { position: 'left', title: { display: true, text: 'TEU' } },
      y1: { position: 'right', grid: { drawOnChartArea: false }, title: { display: true, text: 'Tỷ đồng' } },
    },
  }

  const xuatExcel = () =>
    chay(() => taiFile('/api/bao-cao-thong-ke/excel' + taoQuery(loc), { tenMacDinh: 'BaoCao_SmartPort.xlsx' }), {
      thanhCong: 'Đã tải báo cáo Excel.',
    })

  const nam = d?.nam ?? ''

  const COT_SL = [
    { khoa: 'thang', ten: 'Tháng', hien: (r) => <span className="fw-semibold">Tháng {r.thang}</span> },
    { khoa: 'teu', ten: 'Sản lượng (TEU)', canPhai: true, hien: (r) => dinhDangSo(r.teu) },
    { khoa: 'dt', ten: 'Doanh thu', canPhai: true, hien: (r) => <span className="text-nowrap">{dinhDangTien(r.doanhThu)}</span> },
  ]

  const COT_DV = [
    { khoa: 'thang', ten: 'Tháng', hien: (r) => <span className="fw-semibold">Tháng {r.thang}</span> },
    { khoa: 'sb', ten: 'Số booking', canPhai: true, hien: (r) => dinhDangSo(r.soBooking) },
    { khoa: 'tien', ten: 'Doanh thu', canPhai: true, hien: (r) => <span className="text-nowrap">{dinhDangTien(r.tien)}</span> },
  ]

  return (
    <>
      <BaoLoi loi={loi} thuLai={taiLai} />

      <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
        <label className="form-label mb-0 small fw-semibold" htmlFor="tc-nam">
          Năm báo cáo
        </label>
        <select
          id="tc-nam"
          className="form-select form-select-sm"
          style={{ width: 110 }}
          value={nam}
          onChange={(e) => datLoc({ nam: e.target.value })}
        >
          {(d?.cacNam || []).map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>
        <span className="flex-grow-1" />
        <button type="button" className="btn btn-sm btn-success" disabled={dangChay || !d} onClick={xuatExcel}>
          <i className="bi bi-file-earmark-excel me-1" />
          Xuất Excel
        </button>
      </div>

      <div className="row g-3 mb-3">
        <div className="col-12 col-md-4">
          <TheChiSo icon="bi-box-seam" mau="xanh" so={<span>{dinhDangSo(d?.tongTEU)} TEU</span>} nhan={`Sản lượng thông qua năm ${nam}`} />
        </div>
        <div className="col-6 col-md-4">
          <TheChiSo
            icon="bi-cash-stack"
            mau="la"
            so={<span title={dinhDangTien(d?.tongDoanhThu)}>{tienO(d?.tongDoanhThu)}</span>}
            nhan={`Doanh thu khai thác năm ${nam}`}
          />
        </div>
        <div className="col-6 col-md-4">
          <TheChiSo
            icon="bi-receipt"
            mau="cam"
            so={<span title={dinhDangTien(d?.tongTienDV)}>{tienO(d?.tongTienDV)}</span>}
            nhan={`Dịch vụ ePort · ${dinhDangSo(d?.tongBooking)} booking hoàn thành`}
          />
        </div>
      </div>

      <TheNoiDung tieuDe={`Sản lượng & doanh thu theo tháng — năm ${nam}`} icon="bi-graph-up">
        <div className="tc-bieu-do-in">
          {d?.sanLuong?.length ? (
            <Bar data={bieuDo} options={tuyChonBieuDo} />
          ) : (
            <div className="text-center text-muted py-5">{dangTai ? 'Đang tải…' : `Chưa có dữ liệu sản lượng năm ${nam}.`}</div>
          )}
        </div>
      </TheNoiDung>

      <div className="row g-3">
        <div className="col-xl-7">
          <TheNoiDung tieuDe="Sản lượng thông qua theo tháng" icon="bi-table" khongDem>
            <BangDuLieu cot={COT_SL} dong={d?.sanLuong} khoaDong="thang" dangTai={dangTai} rong={`Chưa có dữ liệu năm ${nam}.`} />
          </TheNoiDung>
        </div>
        <div className="col-xl-5">
          <TheNoiDung tieuDe="Doanh thu dịch vụ ePort theo tháng" icon="bi-receipt" khongDem>
            <BangDuLieu cot={COT_DV} dong={d?.dichVu} khoaDong="thang" dangTai={dangTai} rong={`Chưa có booking hoàn thành năm ${nam}.`} />
          </TheNoiDung>
        </div>
      </div>
    </>
  )
}
