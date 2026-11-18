export default function TheChiSo({ icon, mau = 'xanh', so, nhan, nho }) {
  return (
    <div className="sp-chi-so">
      <div className={'sp-chi-so-icon ' + mau}>
        <i className={'bi ' + icon} />
      </div>
      <div className="min-w-0">
        <div className={'sp-chi-so-so' + (nho ? ' nho' : '')}>{so}</div>
        <div className="sp-chi-so-nhan">{nhan}</div>
      </div>
    </div>
  )
}
