import { dinhDangSo } from '../../utils/dinhDang'

// Thanh phan nho dung chung cho cac man hinh khu DIEU HANH THONG MINH.
// Goi y cua thay cho giao dien doanh nghiep: IT CHU, so to quy ra tien / CO2 / gio cong.

/** 1.234.567.000 -> { so: "1,2", donVi: "tỷ đ" } — so to tren o mau, don vi chu nho ben canh */
export function tienVaDonVi(v) {
  const x = v || 0
  const a = Math.abs(x)
  if (a >= 1e9) return { so: dinhDangSo(x / 1e9, 1), donVi: 'tỷ đ' }
  if (a >= 1e6) return { so: dinhDangSo(x / 1e6, 1), donVi: 'tr đ' }
  return { so: dinhDangSo(x), donVi: 'đ' }
}

/**
 * O so lon co nen mau.
 *   <OSo mau="la" so="12,4" donVi="tr đ" nhan="Tiền tiết kiệm" icon="bi-piggy-bank" />
 * mau: '' (navy) | la | la-sang | cam | vang | do | tim | tim-dam | ngoc
 */
export default function OSo({ mau = '', so, donVi, nhan, phu, icon }) {
  return (
    <div className={'dh-o-so ' + mau}>
      <div className="dh-o-so-so">
        {so}
        {donVi ? <span className="dh-o-so-don-vi">{donVi}</span> : null}
      </div>
      <div className="dh-o-so-nhan">{nhan}</div>
      {phu ? <div className="dh-o-so-phu">{phu}</div> : null}
      {icon ? <i className={'bi ' + icon + ' dh-o-so-icon'} aria-hidden="true" /> : null}
    </div>
  )
}
