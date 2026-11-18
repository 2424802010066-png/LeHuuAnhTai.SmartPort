export default function BaoLoi({ loi, thuLai }) {
  if (!loi) return null
  return (
    <div className="alert alert-danger d-flex align-items-center gap-2">
      <i className="bi bi-exclamation-octagon" />
      <div className="flex-grow-1">{loi.message}</div>
      {thuLai && (
        <button className="btn btn-sm btn-outline-danger" onClick={() => thuLai()}>
          Thử lại
        </button>
      )}
    </div>
  )
}
