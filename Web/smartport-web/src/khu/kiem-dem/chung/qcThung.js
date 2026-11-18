/* ============================================================
   TRAM QC — SOI LOI THUNG HANG (chuyen tu kiemTraThung cua tally.js)
   1. Tim thung: YOLO26 lop 28 (suitcase/kien hang) -> khong thay thi quet MAU CARTON
   2. Soi be mat tung thung: vung toi hon trung vi = uot/tham mau/hu hai
   3. Thung loi -> khung DO + khoanh vung loi (phan coi/doc/luu anh do trang goi)
   ============================================================ */
import { soiLoiThung, timThungCarton } from './mauSac'
import { phatHienYolo } from './yolo'
import { MAU, chuanBiNet, veHop, veVungLoi } from './ve'

/** Lop COCO 28 = suitcase — dai dien cho "kien hang" trong bo COCO */
const LOP_KIEN_HANG = 28

/**
 * Soi toan bo thung hang dang co trong canvas va VE ket qua len chinh canvas do.
 * @returns {Promise<{soThung:number, soLoi:number, pctMax:number}>}
 */
export async function kiemTraThungHang(canvas) {
  let hops = await phatHienYolo(canvas, [LOP_KIEN_HANG])
  if (!hops.length) hops = timThungCarton(canvas) // YOLO khong thay thung -> quet mau carton
  const ctx = chuanBiNet(canvas, { chiaNet: 260, chiaChu: 42, chuToiThieu: 14 })
  if (!hops.length) return { soThung: 0, soLoi: 0, pctMax: 0 }

  let soLoi = 0
  let pctMax = 0
  for (const hop of hops) {
    const kq = soiLoiThung(canvas, hop)
    const loi = kq.rect !== null && kq.pct >= 2
    if (loi) {
      soLoi++
      pctMax = Math.max(pctMax, kq.pct)
    }
    veHop(ctx, hop, loi ? MAU.loi : MAU.dat, loi ? 'THÙNG LỖI — hư hại ~' + kq.pct + '%' : 'ĐẠT')
    if (loi) veVungLoi(ctx, kq.rect)
  }
  return { soThung: hops.length, soLoi, pctMax }
}
