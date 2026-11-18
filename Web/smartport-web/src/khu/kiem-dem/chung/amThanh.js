/* ============================================================
   AM THANH CANH BAO dung chung cho Kiem dem AI va Giam sat tai xe
   (chuyen tu sirenQC/beepTick/chimeOk/noiCanhBao cua tally.js va
   siren/beep/noi cua driver.js — giu nguyen tan so, bien do, thoi luong)
   ------------------------------------------------------------
   MOI AudioContext tao ra deu duoc ghi so vao dsNgu: roi man hinh phai goi
   dungAmThanh() de dong het. Trinh duyet chi cho ~6 AudioContext moi tab —
   khong dong thi sau vai chuc canh bao se khong con keu duoc nua.
   ============================================================ */

const dsNgu = new Set()

function moNgu() {
  const Lop = window.AudioContext || window.webkitAudioContext
  if (!Lop) return null
  const a = new Lop()
  dsNgu.add(a)
  return a
}

function henDong(a, sauMs) {
  setTimeout(() => {
    try {
      a.close()
    } catch {
      /* da dong roi */
    }
    dsNgu.delete(a)
  }, sauMs)
}

/**
 * Coi hu (sawtooth + LFO rung tan so) — dung cho su co nguy hiem.
 * @param {number} giay thoi luong
 * @param {{tanSo?:number, nhip?:number, sau?:number, bienDo?:number}} ts
 *   tally.js: 640Hz / LFO 3Hz / sau 200 / 0.22 — driver.js: 660Hz / 3.2Hz / 210 / 0.20
 */
export function huCoi(giay = 3, ts = {}) {
  const a = moNgu()
  if (!a) return
  try {
    const o = a.createOscillator()
    const g = a.createGain()
    const lfo = a.createOscillator()
    const lg = a.createGain()
    o.type = 'sawtooth'
    o.frequency.value = ts.tanSo ?? 640
    lfo.type = 'sine'
    lfo.frequency.value = ts.nhip ?? 3
    lg.gain.value = ts.sau ?? 200
    lfo.connect(lg)
    lg.connect(o.frequency)
    g.gain.value = ts.bienDo ?? 0.22
    o.connect(g)
    g.connect(a.destination)
    o.start()
    lfo.start()
    setTimeout(() => {
      try {
        o.stop()
        lfo.stop()
      } catch {
        /* bo qua */
      }
    }, giay * 1000)
    henDong(a, giay * 1000 + 300)
  } catch {
    henDong(a, 0)
  }
}

/** Tick ngan moi lan dem duoc 1 vat tren bang chuyen */
export function tickDem() {
  const a = moNgu()
  if (!a) return
  try {
    const o = a.createOscillator()
    const g = a.createGain()
    o.type = 'square'
    o.frequency.value = 1250
    g.gain.value = 0.08
    o.connect(g)
    g.connect(a.destination)
    o.start()
    setTimeout(() => {
      try {
        o.stop()
      } catch {
        /* bo qua */
      }
    }, 70)
    henDong(a, 400)
  } catch {
    henDong(a, 0)
  }
}

/** Hai not chuong khi DU so luong */
export function chuongDu() {
  const a = moNgu()
  if (!a) return
  try {
    for (const [tanSo, treMs] of [
      [880, 0],
      [1318, 180],
    ]) {
      const o = a.createOscillator()
      const g = a.createGain()
      o.type = 'sine'
      o.frequency.value = tanSo
      g.gain.value = 0.16
      o.connect(g)
      g.connect(a.destination)
      o.start(a.currentTime + treMs / 1000)
      o.stop(a.currentTime + treMs / 1000 + 0.32)
    }
  } catch {
    /* bo qua */
  }
  henDong(a, 900)
}

/** Ba tieng bip ngan — canh bao muc "chu y" (ngap nhieu, trang thai la) */
export function beep() {
  const a = moNgu()
  if (!a) return
  try {
    for (const treMs of [0, 220, 440]) {
      const o = a.createOscillator()
      const g = a.createGain()
      o.type = 'square'
      o.frequency.value = 980
      g.gain.value = 0.11
      o.connect(g)
      g.connect(a.destination)
      o.start(a.currentTime + treMs / 1000)
      o.stop(a.currentTime + treMs / 1000 + 0.13)
    }
  } catch {
    /* bo qua */
  }
  henDong(a, 900)
}

/** Doc canh bao bang giong tieng Viet (bo qua neu trinh duyet khong ho tro) */
export function noiCanhBao(chu, tocDo = 1.05) {
  if (!('speechSynthesis' in window)) return
  try {
    const u = new SpeechSynthesisUtterance(chu)
    u.lang = 'vi-VN'
    u.rate = tocDo
    const vi = speechSynthesis.getVoices().find((v) => v.lang && v.lang.toLowerCase().startsWith('vi'))
    if (vi) u.voice = vi
    speechSynthesis.speak(u)
  } catch {
    /* bo qua */
  }
}

/** Roi man hinh: dong moi AudioContext dang mo va cat giong dang doc */
export function dungAmThanh() {
  for (const a of dsNgu) {
    try {
      a.close()
    } catch {
      /* bo qua */
    }
  }
  dsNgu.clear()
  try {
    if ('speechSynthesis' in window) speechSynthesis.cancel()
  } catch {
    /* bo qua */
  }
}

/**
 * Bo han che tan suat canh bao theo loai (thay canAlert cua ban MVC):
 *   const hanChe = taoHanChe(); if (hanChe('NguGat', 20000)) { ... }
 */
export function taoHanChe() {
  const lan = {}
  return (khoa, ms) => {
    const now = Date.now()
    if (lan[khoa] && now - lan[khoa] < ms) return false
    lan[khoa] = now
    return true
  }
}
