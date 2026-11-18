/* ============================================================
   DEM LUY KE THEO VIDEO — theo doi tung vat tren bang chuyen
   (chuyen tu capNhatTracks/veKhungVideo cua tally.js)
   ------------------------------------------------------------
   Ghep hop phat hien voi vet cu bang IoU CO DU DOAN VAN TOC: bang chuyen
   chay nhanh, neu so trung khung hinh truoc thi mot vat se bi dem 2-3 lan.
   Vat con CHAM MEP khung (dang di vao) thi hoan xac minh mau: luc do
   histogram lay ca nen nen rat de bao nham "sai mau".
   ============================================================ */
import { histSim, hsvHist } from './mauSac'
import { MAU, chuanBiNet, veBoDem, veHop } from './ve'

/** Do tuong dong toi thieu voi anh mau de coi la "dung san pham" */
export const NGUONG_DUNG_MAU = 0.74
const MEP = 12 // so pixel coi la "cham mep khung"

export function taoBoTheoDoi() {
  return { vets: [], maKe: 1 }
}

/**
 * Ghep hop phat hien vao cac vet dang theo doi, dem nhung vat moi on dinh.
 * @param {object} bo bo theo doi (taoBoTheoDoi)
 * @param {Array} hops hop phat hien cua khung hinh nay
 * @param {HTMLCanvasElement} canvas khung hinh dang ve (de lay mau)
 * @param {number[][]} mauHists histogram cac anh san pham mau
 * @returns {Array<{vet:object, dung:boolean, sim:number}>} nhung vat VUA duoc dem
 */
export function capNhatTheoDoi(bo, hops, canvas, mauHists) {
  for (const t of bo.vets) {
    t.duDoan = { x1: t.hop.x1 + t.vx, y1: t.hop.y1 + t.vy, x2: t.hop.x2 + t.vx, y2: t.hop.y2 + t.vy }
    t.ghep = false
  }
  for (const d of hops) {
    let tot = null
    let diem = 0
    for (const t of bo.vets) {
      if (t.ghep) continue
      const v = giaoTren(d, t.duDoan)
      if (v > diem) {
        diem = v
        tot = t
      }
    }
    if (tot && diem > 0.22) {
      tot.vx = (d.x1 - tot.hop.x1 + (d.x2 - tot.hop.x2)) / 2
      tot.vy = (d.y1 - tot.hop.y1 + (d.y2 - tot.hop.y2)) / 2
      tot.hop = d
      tot.thay++
      tot.mat = 0
      tot.ghep = true
    } else {
      bo.vets.push({ ma: bo.maKe++, hop: d, vx: 0, vy: 0, thay: 1, mat: 0, ghep: true, daDem: false, dung: null, sim: 0 })
    }
  }
  for (const t of bo.vets) if (!t.ghep) t.mat++
  bo.vets = bo.vets.filter((t) => t.mat <= 4)

  const vuaDem = []
  const cw = canvas.width
  const ch = canvas.height
  for (const t of bo.vets) {
    if (t.daDem || t.thay < 2 || t.mat > 0) continue
    const oRia = t.hop.x1 <= MEP || t.hop.y1 <= MEP || t.hop.x2 >= cw - MEP || t.hop.y2 >= ch - MEP
    if (oRia && t.thay < 6) continue
    t.daDem = true
    const hist = hsvHist(canvas, t.hop)
    let sim = 0
    for (const m of mauHists) sim = Math.max(sim, histSim(hist, m))
    t.sim = sim
    t.dung = mauHists.length ? sim >= NGUONG_DUNG_MAU : true
    vuaDem.push({ vet: t, dung: t.dung, sim })
  }
  return vuaDem
}

function giaoTren(a, b) {
  const x1 = Math.max(a.x1, b.x1)
  const y1 = Math.max(a.y1, b.y1)
  const x2 = Math.min(a.x2, b.x2)
  const y2 = Math.min(a.y2, b.y2)
  const giao = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
  const sa = (a.x2 - a.x1) * (a.y2 - a.y1)
  const sb = (b.x2 - b.x1) * (b.y2 - b.y1)
  return giao / (sa + sb - giao + 1e-9)
}

/** Ve cac vet dang theo doi + bo dem lon giua tren */
export function veKhungTheoDoi(canvas, bo, dem, mucTieu) {
  const ctx = chuanBiNet(canvas, { chiaNet: 260, chiaChu: 52 })
  for (const t of bo.vets) {
    if (t.mat > 0) continue
    const mau = t.dung === false ? MAU.loi : t.dung === true ? MAU.dat : MAU.moi
    const nhan =
      t.dung === false
        ? 'KHÔNG ĐÚNG MẪU ' + Math.round(t.sim * 100) + '%'
        : t.dung === true
          ? '#' + t.ma + ' · ' + Math.round(t.sim * 100) + '%'
          : '#' + t.ma
    veHop(ctx, t.hop, mau, nhan)
  }
  veBoDem(ctx, dem + ' / ' + (mucTieu || '—'), !!mucTieu && dem >= mucTieu)
}
