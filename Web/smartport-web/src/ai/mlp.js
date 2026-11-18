/* ============================================================
   AI LAB — MANG NO-RON MLP DU BAO SAN LUONG (TensorFlow.js)
   Cua so 3 thang truoc -> du doan thang ke tiep. Huan luyen bang
   Adam, ham mat mat MSE, ngay tren trinh duyet (khong can GPU server).
   ============================================================ */
import { AI_VENDOR, taiScript } from './taiThuVien'

export const WIN = 3
export const HORIZON = 6

export async function taiTensorFlow() {
  await taiScript(AI_VENDOR + 'tf/tf.min.js')
  return window.tf
}

/** So no-ron lop an thu 2 = mot nua lop an thu nhat (toi thieu 4) */
export const lopAn2 = (hidden) => Math.max(4, Math.round(hidden / 2))

/** So tham so hoc duoc: (vao x ra + bias) cua 3 lop Dense. 16 no-ron -> 209 */
export function soThamSo(hidden) {
  const h2 = lopAn2(hidden)
  return WIN * hidden + hidden + (hidden * h2 + h2) + (h2 + 1)
}

/** Sinh nhan "T7/26", "T8/26"... tiep sau nhan cuoi cua chuoi */
export function nhanTiepTheo(nhanCuoi, n) {
  const [phanThang, phanNam] = nhanCuoi.split('/')
  let m = parseInt(phanThang.substring(1), 10)
  let y = parseInt(phanNam, 10)
  const out = []
  for (let i = 0; i < n; i++) {
    m++
    if (m > 12) {
      m = 1
      y++
    }
    out.push('T' + m + '/' + y)
  }
  return out
}

/** Hoi quy tuyen tinh bang binh phuong toi thieu — duong doi chieu */
export function hoiQuyTuyenTinh(values, n) {
  const N = values.length
  let sx = 0
  let sy = 0
  let sxy = 0
  let sx2 = 0
  for (let i = 0; i < N; i++) {
    sx += i
    sy += values[i]
    sxy += i * values[i]
    sx2 += i * i
  }
  const b = (N * sxy - sx * sy) / (N * sx2 - sx * sx)
  const a = (sy - b * sx) / N
  return Array.from({ length: n }, (_, k) => Math.round(a + b * (N + k)))
}

let moHinhCu = null
const SO_LAN_KHOI_TAO_TOI_DA = 3

/**
 * Huan luyen MLP va du bao HORIZON thang bang cach cuon (rollout): du bao thang 1
 * duoc nap nguoc lam dau vao de du bao thang 2...
 *
 * NO-RON ReLU "CHET": neu mot buoc cap nhat day bias cua ca lop an xuong am, moi
 * no-ron ReLU ra 0 voi MOI dau vao -> gradient bang 0, mang ket o viec du doan mot
 * HANG SO (gan trung binh, du bao 6 thang phang). Thu tren 66 thang so lieu that
 * gap khoang 1/3 so lan chay. Phat hien: loss cuoi van xap xi MSE cua cach doan hang
 * so (= phuong sai cua nhan) -> khoi tao lai trong so va hoc lai, giu nguyen kien truc.
 */
export async function huanLuyenMlp({ values, epochs, lr, hidden, khiXongEpoch }) {
  if (!values || values.length < WIN + 1) throw new Error(`Cần ít nhất ${WIN + 1} tháng số liệu (hiện có ${values ? values.length : 0}).`)
  const tf = await taiTensorFlow()

  // Chuan hoa min-max ve [0,1] de Adam hoi tu on dinh
  const yMin = Math.min(...values)
  let yMax = Math.max(...values)
  if (yMax - yMin < 1e-9) yMax = yMin + 1
  const norm = (v) => (v - yMin) / (yMax - yMin)
  const denorm = (v) => v * (yMax - yMin) + yMin

  const xs = []
  const ys = []
  for (let i = WIN; i < values.length; i++) {
    xs.push([norm(values[i - 3]), norm(values[i - 2]), norm(values[i - 1])])
    ys.push([norm(values[i])])
  }
  const tx = tf.tensor2d(xs)
  const ty = tf.tensor2d(ys)

  // MSE cua cach "doan hang so = trung binh" (tren nhan da chuan hoa) — moc de nhan ra mang bi ket
  const tbNhan = ys.reduce((a, v) => a + v[0], 0) / ys.length
  const mseHangSo = ys.reduce((a, v) => a + (v[0] - tbNhan) ** 2, 0) / ys.length

  try {
    // Nhuong luot cho trinh duyet ve lai giao dien sau moi epoch. Tab bi AN thi
    // requestAnimationFrame (tf.nextFrame) ngung chay -> doi sang setTimeout,
    // neu khong viec huan luyen dung han khi nguoi dung chuyen tab.
    const nhuongLuot = () => (document.hidden ? new Promise((ok) => setTimeout(ok, 0)) : tf.nextFrame())
    const t0 = performance.now()
    let model = null
    let losses = []
    let soLanKhoiTao = 0
    do {
      soLanKhoiTao++
      if (moHinhCu) moHinhCu.dispose()
      model = tf.sequential()
      model.add(tf.layers.dense({ units: hidden, activation: 'relu', inputShape: [WIN] }))
      model.add(tf.layers.dense({ units: lopAn2(hidden), activation: 'relu' }))
      model.add(tf.layers.dense({ units: 1 }))
      model.compile({ optimizer: tf.train.adam(lr), loss: 'meanSquaredError' })
      moHinhCu = model

      const lanNay = []
      losses = lanNay
      await model.fit(tx, ty, {
        epochs,
        batchSize: 8,
        shuffle: true,
        yieldEvery: 'never', // tu nhuong luot theo epoch o duoi, khong dung co che rAF mac dinh
        callbacks: {
          onEpochEnd: async (ep, logs) => {
            lanNay.push(logs.loss)
            if (ep % 5 === 0 || ep === epochs - 1) khiXongEpoch?.(ep + 1, lanNay.slice(), soLanKhoiTao)
            await nhuongLuot()
          },
        },
      })
    } while (losses[losses.length - 1] > 0.5 * mseHangSo && soLanKhoiTao < SO_LAN_KHOI_TAO_TOI_DA)
    const ms = Math.round(performance.now() - t0)
    const biKet = losses[losses.length - 1] > 0.5 * mseHangSo

    // Sai so tuyet doi trung binh (MAE) tren tap huan luyen, doi ve don vi goc
    const predTrain = model.predict(tx)
    const pd = await predTrain.data()
    predTrain.dispose()
    let mae = 0
    for (let i = 0; i < ys.length; i++) mae += Math.abs(denorm(pd[i]) - denorm(ys[i][0]))
    mae = Math.round(mae / ys.length)

    // Du bao cuon HORIZON thang
    let cuaSo = values.slice(-WIN).map(norm)
    const duBao = []
    while (duBao.length < HORIZON) {
      const t = tf.tensor2d([cuaSo])
      const p = model.predict(t)
      const [val] = await p.data()
      t.dispose()
      p.dispose()
      duBao.push(Math.round(denorm(val)))
      cuaSo = cuaSo.slice(1).concat([val])
    }
    return { duBao, tuyenTinh: hoiQuyTuyenTinh(values, HORIZON), mae, ms, losses, soLanKhoiTao, biKet }
  } finally {
    tx.dispose()
    ty.dispose()
  }
}
