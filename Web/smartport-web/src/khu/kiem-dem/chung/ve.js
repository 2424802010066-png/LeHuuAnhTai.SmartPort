/* ============================================================
   VE KHUNG + NHAN len canvas (dung chung cho kiem dem, QC thung hang,
   dem video, camera tai xe va camera khoang hang).
   Co chu/net ve deu tinh theo CHIEU RONG canvas: anh 4K va webcam 480p
   deu ra nhan doc duoc, khong bi chu li ti hay to choan man hinh.
   ============================================================ */

export const MAU = {
  dat: '#22c55e',
  loi: '#ef4444',
  loiDam: '#ff1744',
  chuY: '#f59e0b',
  moi: '#38bdf8',
  chu: '#08131f',
}

/** Chuan bi ctx (net + co chu) theo chieu rong canvas */
export function chuanBiNet(canvas, { chiaNet = 260, chiaChu = 48, netToiThieu = 3, chuToiThieu = 13 } = {}) {
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  ctx.lineWidth = Math.max(netToiThieu, canvas.width / chiaNet)
  ctx.font = '700 ' + Math.max(chuToiThieu, Math.round(canvas.width / chiaChu)) + 'px "Be Vietnam Pro", sans-serif'
  return ctx
}

/** Khung chu nhat + nhan chu nam tren khung */
export function veHop(ctx, hop, mau, nhan, { netDut = false } = {}) {
  ctx.strokeStyle = mau
  if (netDut) ctx.setLineDash([8, 5])
  ctx.strokeRect(hop.x1, hop.y1, hop.x2 - hop.x1, hop.y2 - hop.y1)
  if (netDut) ctx.setLineDash([])
  if (!nhan) return
  const cao = Math.max(22, Math.round(ctx.canvas.width / 46))
  ctx.fillStyle = mau
  ctx.fillRect(hop.x1 - 1, Math.max(0, hop.y1 - cao - 2), ctx.measureText(nhan).width + 12, cao)
  ctx.fillStyle = MAU.chu
  ctx.fillText(nhan, hop.x1 + 5, Math.max(cao - 6, hop.y1 - 8))
}

/** Khoanh net dut vung hu hai ben trong 1 thung */
export function veVungLoi(ctx, rect) {
  if (!rect) return
  ctx.setLineDash([8, 5])
  ctx.strokeStyle = MAU.loiDam
  ctx.strokeRect(rect.x, rect.y, rect.w, rect.h)
  ctx.setLineDash([])
}

/** Bo dem lon giua tren khung hinh (dem theo video) */
export function veBoDem(ctx, chu, du) {
  const w = ctx.canvas.width
  ctx.font = '900 ' + Math.max(26, Math.round(w / 16)) + 'px "Be Vietnam Pro", sans-serif'
  const tw = ctx.measureText(chu).width
  const cao = Math.max(40, Math.round(w / 13))
  ctx.fillStyle = 'rgba(8,19,31,.72)'
  ctx.fillRect(w / 2 - tw / 2 - 18, 10, tw + 36, cao)
  ctx.fillStyle = du ? MAU.dat : '#f8fafc'
  ctx.fillText(chu, w / 2 - tw / 2, cao + 2)
}
