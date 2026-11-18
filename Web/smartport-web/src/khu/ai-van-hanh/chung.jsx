// Thanh phan nho dung chung cho cac man hinh khu AI VAN HANH.
// Nguyen tac giao dien: IT CHU — so to, mau noi muc do; cong thuc gap lai trong "AI tính thế nào?".

/**
 * O so lon co nen mau.
 *   <OChiSo mau="la" so="12" donVi="xe" nhan="Rời trong 72 giờ" phu="..." icon="bi-boxes" />
 * mau: '' (navy) | la | cam | vang | do | tim | ngoc | xam
 */
export function OChiSo({ mau = '', so, donVi, nhan, phu, icon }) {
  return (
    <div className={'av-o-so ' + mau}>
      <div className="av-o-so-so">
        {so}
        {donVi ? <span className="av-o-so-don-vi">{donVi}</span> : null}
      </div>
      <div className="av-o-so-nhan">{nhan}</div>
      {phu ? <div className="av-o-so-phu">{phu}</div> : null}
      {icon ? <i className={'bi ' + icon + ' av-o-so-icon'} aria-hidden="true" /> : null}
    </div>
  )
}

/** Hop gap "AI tinh the nao?" — cong thuc, gia dinh, cach kiem dinh (de giai thich khi van dap / voi khach) */
export function CachTinh({ tieuDe = 'AI tính thế nào?', children }) {
  return (
    <details className="card sp-the p-3 av-cach-tinh mb-3">
      <summary className="fw-bold av-chu-tim">
        <i className="bi bi-lightbulb me-1" />
        {tieuDe}
      </summary>
      <div className="small mt-2">{children}</div>
    </details>
  )
}

/** Thanh ngang 0..toiDa; mau: ok | info | warn | do | off */
export function ThanhDiem({ giaTri, toiDa = 100, mau = 'info' }) {
  const pct = Math.max(0, Math.min(100, ((giaTri || 0) / toiDa) * 100))
  return (
    <div className="av-thanh" role="presentation">
      <span className={'av-nen-' + mau} style={{ width: pct + '%' }} />
    </div>
  )
}

/** Dong thanh cong cu dau trang: thoi diem tinh + nut lam moi + phan tuy chinh */
export function ThanhCongCu({ luc, dangTai, lamMoi, children }) {
  return (
    <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
      <div className="small text-body-secondary">
        <i className="bi bi-cpu me-1" />
        Tính trực tiếp từ dữ liệu trong hệ thống{luc ? ' · ' + luc : ''}
      </div>
      <div className="d-flex flex-wrap align-items-center gap-2">
        {children}
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => lamMoi()} disabled={dangTai}>
          {dangTai ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-arrow-clockwise me-1" />}
          Làm mới
        </button>
      </div>
    </div>
  )
}
