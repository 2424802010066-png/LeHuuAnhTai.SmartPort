/* ============================================================
   AI TU HOC TRANG THAI BAT THUONG (chuyen tu taoAno cua driver.js)
   ------------------------------------------------------------
   Mang no-ron TU MA HOA 10-7-3-7-10 (tanh, SGD lan truyen nguoc thuan JS).
   - Pha "hoc nen": hoc ~150 khung tu the lai BINH THUONG cua chinh tai xe,
     nguong = mean + k*std cua sai so tai tao.
   - Pha "giam sat": khung nao tai tao sai lech cao lien tuc 2,5s = trang thai
     CHUA TUNG THAY -> bao dong. Van hoc cham cac khung binh thuong de thich
     nghi anh sang/tu the.
   - Phan hoi cua nguoi van hanh (Dung / Bao nham) cong-tru nguong k nhu tin
     hieu thuong/phat cua hoc tang cuong; trong so luu localStorage de AI "nho"
     qua cac phien lam viec.
   Vi sao can: luat cung chi bat duoc thu da lap trinh san (nham mat, quay dau,
   dien thoai). Tu the la chua tung nghi toi thi chi mang tu ma hoa phat hien duoc.
   ============================================================ */

export function taoTuHoc(cfg = {}) {
  const KICH = cfg.kich || [10, 7, 3, 7, 10]
  const HOC_NEN = cfg.hocNen ?? 150
  const GIU_MS = cfg.giuMs ?? 2500
  const kho = cfg.kho === undefined ? 'sp_dms_ano' : cfg.kho

  const rnd = (() => {
    let s = cfg.seed || 88
    return () => {
      s = (s * 16807) % 2147483647
      return s / 2147483647 - 0.5
    }
  })()

  let W = []
  let B = []
  for (let l = 0; l < KICH.length - 1; l++) {
    const w = []
    const b = []
    for (let j = 0; j < KICH[l + 1]; j++) {
      const hang = []
      for (let i = 0; i < KICH[l]; i++) hang.push(rnd() * 0.6)
      w.push(hang)
      b.push(0)
    }
    W.push(w)
    B.push(b)
  }

  const STAT_TU = Math.max(20, HOC_NEN - 50) // chi thong ke 50 khung CUOI (mang da hoi tu)
  const st = { n: 0, nStat: 0, mean: 0, m2: 0, sd: 0, k: 3.5, ema: 0, batTu: 0, pha: 'hoc', dem: [], demBao: null }

  try {
    if (kho && window.localStorage) {
      const luu = JSON.parse(localStorage.getItem(kho) || 'null')
      if (luu && luu.kich && luu.kich.join() === KICH.join()) {
        W = luu.W
        B = luu.B
        // k luu theo TRINH DUYET chu khong theo tai xe -> kep lai de khong mu/khong nhay qua
        if (luu.k) st.k = Math.min(6, Math.max(2.6, luu.k))
      }
    }
  } catch {
    /* localStorage bi chan: hoc lai tu dau, khong sao */
  }

  function veHocNen() {
    // hoc lai nen: GIU trong so da hoc, chi lam moi thong ke
    st.pha = 'hoc'
    st.n = 0
    st.nStat = 0
    st.mean = 0
    st.m2 = 0
    st.sd = 0
    st.ema = 0
    st.batTu = 0
    st.demBao = null
  }

  function luuKho() {
    try {
      if (kho && window.localStorage) localStorage.setItem(kho, JSON.stringify({ kich: KICH, W, B, k: st.k }))
    } catch {
      /* bo qua */
    }
  }

  function lan(x) {
    const A = [x]
    for (let l = 0; l < W.length; l++) {
      const vao = A[l]
      const ra = []
      for (let j = 0; j < W[l].length; j++) {
        let s = B[l][j]
        for (let i = 0; i < vao.length; i++) s += W[l][j][i] * vao[i]
        ra.push(l < W.length - 1 ? Math.tanh(s) : s)
      }
      A.push(ra)
    }
    return A
  }

  function saiSo(A, x) {
    const y = A[A.length - 1]
    let e = 0
    for (let i = 0; i < x.length; i++) {
      const d = y[i] - x[i]
      e += d * d
    }
    return e / x.length
  }

  function hoc(x, lr) {
    const A = lan(x)
    const y = A[A.length - 1]
    const eTruoc = saiSo(A, x)
    let delta = new Array(y.length)
    for (let i = 0; i < y.length; i++) delta[i] = (2 * (y[i] - x[i])) / x.length
    for (let l = W.length - 1; l >= 0; l--) {
      const vao = A[l]
      const dTruoc = new Array(vao.length).fill(0)
      for (let j = 0; j < W[l].length; j++) {
        const dj = delta[j]
        for (let i = 0; i < vao.length; i++) {
          dTruoc[i] += W[l][j][i] * dj
          W[l][j][i] -= lr * dj * vao[i]
        }
        B[l][j] -= lr * dj
      }
      if (l > 0) {
        for (let i = 0; i < vao.length; i++) dTruoc[i] *= 1 - vao[i] * vao[i]
        delta = dTruoc
      }
    }
    return eTruoc
  }

  return {
    /**
     * @param {number[]} x vec-to dac trung
     * @param {number} now moc thoi gian ms
     * @param {boolean} luatBat luat cung (ngu gat/quay dau/dien thoai) dang bao
     *   -> KHONG hoc khung nay lam "binh thuong" va khong bao trung
     */
    buoc(x, now, luatBat) {
      let e
      if (st.pha === 'hoc') {
        if (luatBat) return { pha: 'hoc', tienDo: Math.round((st.n * 100) / HOC_NEN), e: 0, nguong: 0, bat: false, baoDong: false, giay: 0 }
        st.dem.push(x)
        if (st.dem.length > 60) st.dem.shift()
        e = hoc(x, 0.02)
        hoc(x, 0.02) // 2 luot / khung cho hoi tu nhanh
        st.n++
        // Chi gom thong ke o 50 khung CUOI — sai so dau pha la qua trinh hoi tu
        // (giam dan), khong phai phan bo "binh thuong" that
        if (st.n > STAT_TU) {
          st.nStat++
          const d = e - st.mean
          st.mean += d / st.nStat
          st.m2 += d * (e - st.mean)
        }
        if (st.n >= HOC_NEN) {
          st.sd = Math.sqrt(st.m2 / Math.max(1, st.nStat - 1))
          st.ema = st.mean
          st.pha = 'giamsat'
          luuKho()
        }
        return { pha: 'hoc', tienDo: Math.round((st.n * 100) / HOC_NEN), e, nguong: 0, bat: false, baoDong: false, giay: 0 }
      }

      st.dem.push(x)
      if (st.dem.length > 60) st.dem.shift()
      st.n++
      e = saiSo(lan(x), x)
      st.ema = st.ema * 0.72 + e * 0.28
      const sdEff = Math.max(st.sd, st.mean * 0.2 + 0.0008)
      const nguong = st.mean + st.k * sdEff
      const bat = st.ema > nguong
      if (bat) {
        if (!st.batTu) st.batTu = now
        // Ket bao dong qua 60s (doi tai xe / doi anh sang?) -> tu hoc lai nen
        if (now - st.batTu > 60000) {
          veHocNen()
          return { pha: 'hoc', tienDo: 0, tuHocLai: true, e, nguong: 0, bat: false, baoDong: false, giay: 0 }
        }
      } else {
        st.batTu = 0
        if (!luatBat && e < st.mean + sdEff) {
          // khung binh thuong -> hoc cham de thich nghi
          hoc(x, 0.004)
          st.mean = st.mean * 0.995 + e * 0.005
          st.sd = st.sd * 0.995 + Math.abs(e - st.mean) * 1.2533 * 0.005
        }
      }
      if (st.n % 200 === 0) luuKho()
      const baoDong = st.batTu > 0 && now - st.batTu >= GIU_MS
      if (baoDong) st.demBao = st.dem.slice() // chup vung gay bao de day lai dung cho
      return { pha: 'giamsat', e, ema: st.ema, nguong, bat, giay: st.batTu ? (now - st.batTu) / 1000 : 0, baoDong }
    },

    /** Mat dau khuon mat / tat camera -> xoa moc bao dong dang dem do */
    ngat() {
      st.batTu = 0
    },

    /** Bat dau phien camera moi -> hoc lai nen (giu trong so da co) */
    hocLai: veHocNen,

    /**
     * Phan hoi nguoi van hanh:
     *  dung = true  -> canh bao chuan, ha k cho nhay hon
     *  dung = false -> bao nham: nang k VA day lai dung VUNG DA GAY BAO
     *                  (chup luc bao dong), vi nguoi van hanh co the bam nut
     *                  tre ca chuc giay so voi luc canh bao no ra.
     */
    phanHoi(dung) {
      if (dung) {
        st.k = Math.max(2.2, st.k - 0.3)
      } else {
        st.k = Math.min(8, st.k + 0.7)
        const vung = st.demBao || st.dem
        for (let ep = 0; ep < 3; ep++) for (let i = 0; i < vung.length; i++) hoc(vung[i], 0.01)
        st.batTu = 0
        st.ema = st.mean
        st.demBao = null
      }
      luuKho()
      return st.k
    },

    trangThai: () => ({ pha: st.pha, k: st.k, mean: st.mean, sd: st.sd, ema: st.ema, n: st.n }),

    quenKho() {
      try {
        if (kho) localStorage.removeItem(kho)
      } catch {
        /* bo qua */
      }
    },
  }
}
