import { useCallback, useEffect, useRef, useState } from 'react'
import { COT, HANG, O, duongToiUu, huanLuyen, taoBai, taoQ, trungBinh, veBai } from './qHoc'
import './ai-thi-giac.css'

/**
 * HOC TANG CUONG (Q-Learning) — the gan duoi man hinh AI Lab:
 * xe nang tu thu-sai tim duong toi uu trong bai container (thay phan Q-LEARNING
 * trong Views/AiLab/Index.cshtml + Scripts/rl.js cua ban MVC).
 */
export default function TheQHoc() {
  const canvasRef = useRef(null)
  const baiRef = useRef(null)
  const qRef = useRef(null)
  const lichSuRef = useRef([])
  const [tin, setTin] = useState(null)
  const [dangHoc, setDangHoc] = useState(false)

  const baiMoi = useCallback(() => {
    baiRef.current = taoBai()
    qRef.current = taoQ()
    lichSuRef.current = []
    veBai(canvasRef.current, baiRef.current, qRef.current, null)
    setTin(null)
  }, [])

  useEffect(() => {
    const c = canvasRef.current
    c.width = COT * O
    c.height = HANG * O
    baiMoi()
  }, [baiMoi])

  function hoc() {
    setDangHoc(true)
    // Nhuong 1 nhip cho trinh duyet ve lai nut "Đang học…" truoc khi chay 600 episode (dong bo, nang CPU)
    setTimeout(() => {
      const t0 = performance.now()
      lichSuRef.current = lichSuRef.current.concat(huanLuyen(baiRef.current, qRef.current))
      const ms = Math.round(performance.now() - t0)
      const duong = duongToiUu(baiRef.current, qRef.current)
      veBai(canvasRef.current, baiRef.current, qRef.current, duong)
      setTin({
        ms,
        soBuoc: duong ? duong.length - 1 : null,
        dau: trungBinh(lichSuRef.current.slice(0, 30)),
        cuoi: trungBinh(lichSuRef.current.slice(-30)),
      })
      setDangHoc(false)
    }, 30)
  }

  return (
    <div className="card sp-the mt-3">
      <div className="card-header d-flex flex-wrap align-items-center gap-2">
        <span className="flex-grow-1">
          <i className="bi bi-joystick me-2" />
          Học tăng cường — xe nâng tự học tìm đường trong bãi
          <span className="badge text-bg-warning ms-2">Q-LEARNING</span>
        </span>
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={baiMoi} disabled={dangHoc}>
          <i className="bi bi-shuffle me-1" />
          Bãi ngẫu nhiên mới
        </button>
        <button type="button" className="btn btn-sm btn-primary" onClick={hoc} disabled={dangHoc}>
          {dangHoc ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-lightning-charge me-1" />}
          {dangHoc ? 'Đang học…' : 'Huấn luyện 600 episode'}
        </button>
      </div>
      <div className="card-body">
        <div className="row g-3 align-items-start">
          <div className="col-lg-8 text-center">
            <canvas ref={canvasRef} className="tg-rl-canvas" aria-label="Lưới bãi container và lộ trình xe nâng" />
          </div>
          <div className="col-lg-4">
            <div className="d-flex flex-wrap gap-3 mb-3">
              <div>
                <div className="h4 mb-0 fw-bold text-primary">{tin?.soBuoc ?? '—'}</div>
                <div className="small text-body-secondary">Số bước tới đích</div>
              </div>
              <div>
                <div className="h4 mb-0 fw-bold">
                  <span className="text-danger">{tin ? tin.dau : '—'}</span>
                  <i className="bi bi-arrow-right mx-1 small text-body-secondary" />
                  <span className="text-success">{tin ? tin.cuoi : '—'}</span>
                </div>
                <div className="small text-body-secondary">Thưởng TB: 30 lượt đầu → 30 lượt cuối</div>
              </div>
            </div>
            {tin ? (
              tin.soBuoc != null ? (
                <div className="alert alert-success py-2 small mb-2">
                  Agent học xong <b>600 episode</b> trong <b>{tin.ms} ms</b> và tìm được lộ trình <b>{tin.soBuoc} bước</b> tới đích — tự tiến bộ nhờ thử–sai.
                </div>
              ) : (
                <div className="alert alert-warning py-2 small mb-2">Chính sách chưa hội tụ — bấm huấn luyện thêm một lần nữa.</div>
              )
            ) : (
              <div className="alert alert-light border py-2 small mb-2">Bãi mới đã tạo — bấm “Huấn luyện 600 episode” để agent tự học.</div>
            )}
            <div className="small text-body-secondary" style={{ lineHeight: 1.7 }}>
              Xe nâng <b>không được dạy trước đường đi</b>: mỗi lượt nhận thưởng/phạt (<b>+100</b> tới đích, <b>−10</b> đâm container, <b>−1</b> mỗi bước) rồi cập nhật bảng Q theo phương trình
              Bellman. Ô xanh đậm dần = giá trị đã học, đường cam = lộ trình tối ưu.
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
