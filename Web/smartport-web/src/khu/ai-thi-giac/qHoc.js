/* ============================================================
   HOC TANG CUONG — Q-LEARNING: "Xe nang tu hoc tim duong trong bai container"
   (chuyen tu Scripts/rl.js cua ban MVC sang module ES; tham so va phan thuong giu NGUYEN)
   ------------------------------------------------------------
   Bai toan gridworld: xe nang xuat phat tu CONG, phai toi o DICH (vi tri xep container),
   tranh cac o CONTAINER chan duong. Agent KHONG duoc day duong di — no tu thu-sai
   hang tram episode va hoc bang Q(s,a) qua phuong trinh Bellman:
       Q(s,a) += lr * ( r + gamma * max Q(s',a') - Q(s,a) )
   ============================================================ */

export const COT = 9
export const HANG = 6
export const O = 58 // kich thuoc 1 o tren canvas (px)
export const BAT_DAU = { x: 0, y: 5 }
export const DICH = { x: 8, y: 0 }

const HANH_DONG = [
  [0, -1],
  [0, 1],
  [-1, 0],
  [1, 0],
] // len, xuong, trai, phai
const SO_CONTAINER = 11

const maO = (x, y) => y * COT + x

/** BFS: tu BAT_DAU co duong di toi DICH khong (4 huong) */
function coDuongDi(luoi) {
  const daXet = new Set([maO(BAT_DAU.x, BAT_DAU.y)])
  const hang = [[BAT_DAU.x, BAT_DAU.y]]
  while (hang.length) {
    const [x, y] = hang.shift()
    if (x === DICH.x && y === DICH.y) return true
    for (const [kx, ky] of [
      [x + 1, y],
      [x - 1, y],
      [x, y + 1],
      [x, y - 1],
    ]) {
      if (kx < 0 || kx >= COT || ky < 0 || ky >= HANG || luoi[ky][kx]) continue
      const khoa = maO(kx, ky)
      if (daXet.has(khoa)) continue
      daXet.add(khoa)
      hang.push([kx, ky])
    }
  }
  return false
}

/**
 * Sinh bai ngau nhien — LAP LAI toi khi CHAC CHAN co duong di:
 * ban do bi container bit kin thi agent hoc 600 episode cung vo ich (loi cua ban MVC cu).
 */
export function taoBai() {
  let luoi
  do {
    luoi = Array.from({ length: HANG }, () => new Array(COT).fill(0))
    let dat = 0
    while (dat < SO_CONTAINER) {
      const x = Math.floor(Math.random() * COT)
      const y = Math.floor(Math.random() * HANG)
      if ((x === BAT_DAU.x && y === BAT_DAU.y) || (x === DICH.x && y === DICH.y) || luoi[y][x]) continue
      luoi[y][x] = 1
      dat++
    }
  } while (!coDuongDi(luoi))
  return luoi
}

export const taoQ = () => Array.from({ length: COT * HANG }, () => [0, 0, 0, 0])

/** Moi truong: ra ngoai luoi -5, dam container -10, toi dich +100, moi buoc -1 */
function buocMoiTruong(luoi, x, y, a) {
  const nx = x + HANH_DONG[a][0]
  const ny = y + HANH_DONG[a][1]
  if (nx < 0 || nx >= COT || ny < 0 || ny >= HANG) return { x, y, r: -5, xong: false }
  if (luoi[ny][nx] === 1) return { x, y, r: -10, xong: false }
  if (nx === DICH.x && ny === DICH.y) return { x: nx, y: ny, r: 100, xong: true }
  return { x: nx, y: ny, r: -1, xong: false }
}

function viTriLonNhat(mang) {
  let tot = 0
  for (let i = 1; i < mang.length; i++) if (mang[i] > mang[tot]) tot = i
  return tot
}

/** Huan luyen Q-learning: lr 0.2, gamma 0.92, epsilon giam dan -> tra ve lich su phan thuong */
export function huanLuyen(luoi, Q, soEpisode = 600) {
  const lr = 0.2
  const gamma = 0.92
  const lichSu = []
  for (let ep = 0; ep < soEpisode; ep++) {
    const eps = Math.max(0.05, 1 - ep / (soEpisode * 0.75))
    let x = BAT_DAU.x
    let y = BAT_DAU.y
    let tong = 0
    for (let buoc = 0; buoc < 200; buoc++) {
      const s = maO(x, y)
      const a = Math.random() < eps ? Math.floor(Math.random() * 4) : viTriLonNhat(Q[s])
      const kq = buocMoiTruong(luoi, x, y, a)
      const s2 = maO(kq.x, kq.y)
      Q[s][a] += lr * (kq.r + gamma * Math.max(...Q[s2]) - Q[s][a])
      tong += kq.r
      x = kq.x
      y = kq.y
      if (kq.xong) break
    }
    lichSu.push(tong)
  }
  return lichSu
}

/** Duong di toi uu theo chinh sach greedy da hoc (null = chua hoi tu) */
export function duongToiUu(luoi, Q) {
  const duong = [{ x: BAT_DAU.x, y: BAT_DAU.y }]
  let x = BAT_DAU.x
  let y = BAT_DAU.y
  for (let i = 0; i < 60; i++) {
    const kq = buocMoiTruong(luoi, x, y, viTriLonNhat(Q[maO(x, y)]))
    if (kq.x === x && kq.y === y) break // ket: dam tuong/container
    x = kq.x
    y = kq.y
    duong.push({ x, y })
    if (kq.xong) return duong
  }
  return null
}

/** Ve luoi bai + heatmap gia tri Q hoc duoc + duong di toi uu (mau cam) */
export function veBai(canvas, luoi, Q, duong) {
  const ctx = canvas.getContext('2d')
  ctx.clearRect(0, 0, canvas.width, canvas.height)

  let vMax = 1
  for (const qs of Q) {
    const m = Math.max(...qs)
    if (m > vMax) vMax = m
  }

  for (let y = 0; y < HANG; y++) {
    for (let x = 0; x < COT; x++) {
      const px = x * O
      const py = y * O
      if (luoi[y][x] === 1) {
        ctx.fillStyle = '#8a5a2b'
        ctx.fillRect(px + 2, py + 2, O - 4, O - 4)
        ctx.strokeStyle = '#6b4520'
        for (let l = 1; l < 4; l++) {
          ctx.beginPath()
          ctx.moveTo(px + 4, py + 2 + (l * (O - 4)) / 4)
          ctx.lineTo(px + O - 4, py + 2 + (l * (O - 4)) / 4)
          ctx.stroke()
        }
      } else {
        const v = Math.max(0, Math.max(...Q[maO(x, y)])) / vMax
        ctx.fillStyle = 'rgba(34, 211, 238, ' + (v * 0.55).toFixed(3) + ')'
        ctx.fillRect(px + 2, py + 2, O - 4, O - 4)
      }
      ctx.strokeStyle = 'rgba(148,163,184,.35)'
      ctx.strokeRect(px + 2, py + 2, O - 4, O - 4)
    }
  }

  ctx.font = O * 0.55 + 'px serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText(String.fromCodePoint(0x1f69c), BAT_DAU.x * O + O / 2, BAT_DAU.y * O + O / 2 + 2)
  ctx.fillText(String.fromCodePoint(0x1f3af), DICH.x * O + O / 2, DICH.y * O + O / 2 + 2)

  if (duong && duong.length > 1) {
    ctx.strokeStyle = '#f59e0b'
    ctx.lineWidth = 5
    ctx.lineJoin = 'round'
    ctx.beginPath()
    duong.forEach((p, i) => {
      const cx = p.x * O + O / 2
      const cy = p.y * O + O / 2
      if (i === 0) ctx.moveTo(cx, cy)
      else ctx.lineTo(cx, cy)
    })
    ctx.stroke()
    ctx.lineWidth = 1
  }
}

export const trungBinh = (mang) => (mang.length ? Math.round(mang.reduce((s, v) => s + v, 0) / mang.length) : 0)
