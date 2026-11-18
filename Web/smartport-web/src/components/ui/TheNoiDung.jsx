/**
 * The (card) chuan cua khu dieu hanh: tieu de + nut thao tac ben phai + noi dung.
 *   <TheNoiDung tieuDe="Danh sách tàu" icon="bi-water" phai={<button…>Thêm</button>} khongDem>
 *     <BangDuLieu … />
 *   </TheNoiDung>
 * khongDem: bo padding cua than (bang sat vien the).
 */
export default function TheNoiDung({ tieuDe, icon, phai, children, khongDem, lop = 'mb-3', chan }) {
  return (
    <div className={'card sp-the ' + lop}>
      {(tieuDe || phai) && (
        <div className="card-header d-flex flex-wrap align-items-center gap-2">
          <div className="flex-grow-1">
            {icon && <i className={'bi ' + icon + ' me-2 text-primary'} />}
            {tieuDe}
          </div>
          {phai}
        </div>
      )}
      <div className={khongDem ? '' : 'card-body'}>{children}</div>
      {chan && <div className="card-footer bg-transparent">{chan}</div>}
    </div>
  )
}
