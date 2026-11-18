export default function DangTai({ chu = 'Đang tải dữ liệu từ API…' }) {
  return (
    <div className="text-center text-muted py-5">
      <span className="spinner-border spinner-border-sm me-2" />
      {chu}
    </div>
  )
}
