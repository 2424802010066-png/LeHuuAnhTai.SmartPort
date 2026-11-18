// =====================================================================
// KHO CUC BO CUA TRAM — lop TRUU TUONG tren IndexedDB.
// Moi kho deu co cung giao dien (deu async):
//   lay(kho, khoa) · ghi(kho, banGhi) · xoa(kho, khoa) · tatCa(kho) · dem(kho) · xoaHet(kho) (dang xuat: xoa ban sao)
//   demTheo(kho, truong, giaTri) — dem theo chi muc (khong nap ca anh base64 len chi de dem)
//   capNhat(kho, khoa, ham)  — doc-sua-ghi NGUYEN TU trong 1 giao dich; ham(cu) dong bo, tra:
//                              doi tuong moi -> ghi | null -> xoa | undefined -> giu nguyen
// Ban cai dat:
//   taoKhoIndexedDb  — that, ben vung qua tai lai / dong tab / mat dien (giao dich durability 'strict')
//   taoKhoLocalStorage — du phong khi IndexedDB bi chan (vd che do rieng tu cu); van ben qua tai lai
//   taoKhoBoNho      — trong bo nho, CHO KIEM THU chen vao (khong can cai fake-indexeddb)
// Vi sao khong dung localStorage lam kho chinh: gioi han ~5 MB, ghi dong bo chan giao dien, khong co giao dich
// -> 2 tab ghi de mat du lieu cua nhau (loi cua hang doi cu sp_chotcong_hangdoi).
// =====================================================================

/** Ten kho -> truong khoa chinh */
export const CAC_KHO = {
  hopThuDi: 'maThaoTac', // luot xe cho gui (hoac loi cho nguoi xu ly)
  nhatKy: 'maThaoTac', // ket qua can chu y sau dong bo (can-doi-soat)
  banSao: 'khoa', // goi ngoai tuyen: lenh / eDO / the xe / bieu gia
  caiDat: 'khoa', // linh tinh cua tram
}

/** Chi muc phu (IndexedDB) de dem nhanh: kho -> [truong] */
export const CHI_MUC = { hopThuDi: ['trangThai'] }

const saoChep = (v) => (v === undefined || v === null ? v : structuredClone(v))

function kiemKho(kho) {
  if (!CAC_KHO[kho]) throw new Error('Kho cục bộ không tồn tại: ' + kho)
}

/** Kho trong bo nho — dung cho kiem thu va lam duong lui cuoi cung */
export function taoKhoBoNho() {
  const du = new Map(Object.keys(CAC_KHO).map((k) => [k, new Map()]))
  return {
    loai: 'bo-nho',
    async lay(kho, khoa) {
      kiemKho(kho)
      return saoChep(du.get(kho).get(khoa))
    },
    async ghi(kho, banGhi) {
      kiemKho(kho)
      du.get(kho).set(banGhi[CAC_KHO[kho]], saoChep(banGhi))
    },
    async xoa(kho, khoa) {
      kiemKho(kho)
      du.get(kho).delete(khoa)
    },
    async tatCa(kho) {
      kiemKho(kho)
      return [...du.get(kho).values()].map(saoChep)
    },
    async dem(kho) {
      kiemKho(kho)
      return du.get(kho).size
    },
    async demTheo(kho, truong, giaTri) {
      kiemKho(kho)
      let n = 0
      for (const v of du.get(kho).values()) if (v?.[truong] === giaTri) n++
      return n
    },
    async xoaHet(kho) {
      kiemKho(kho)
      du.get(kho).clear()
    },
    async capNhat(kho, khoa, ham) {
      kiemKho(kho)
      const m = du.get(kho)
      const moi = ham(saoChep(m.get(khoa)))
      if (moi === null) m.delete(khoa)
      else if (moi !== undefined) m.set(khoa, saoChep(moi))
      return saoChep(moi)
    },
  }
}

/** Kho tren localStorage (moi kho = 1 khoa JSON) — du phong khi khong mo duoc IndexedDB */
export function taoKhoLocalStorage(luuTru, tienTo = 'sp-tram:') {
  const doc = (kho) => {
    try {
      return JSON.parse(luuTru.getItem(tienTo + kho) || '{}') || {}
    } catch {
      return {}
    }
  }
  // setItem nem loi khi day bo nho -> de loi noi len cho ben goi biet "khong luu duoc" (khong im lang mat du lieu)
  const viet = (kho, obj) => luuTru.setItem(tienTo + kho, JSON.stringify(obj))
  return {
    loai: 'local-storage',
    async lay(kho, khoa) {
      kiemKho(kho)
      return doc(kho)[khoa]
    },
    async ghi(kho, banGhi) {
      kiemKho(kho)
      const o = doc(kho)
      o[banGhi[CAC_KHO[kho]]] = banGhi
      viet(kho, o)
    },
    async xoa(kho, khoa) {
      kiemKho(kho)
      const o = doc(kho)
      delete o[khoa]
      viet(kho, o)
    },
    async tatCa(kho) {
      kiemKho(kho)
      return Object.values(doc(kho))
    },
    async dem(kho) {
      kiemKho(kho)
      return Object.keys(doc(kho)).length
    },
    async demTheo(kho, truong, giaTri) {
      kiemKho(kho)
      return Object.values(doc(kho)).filter((v) => v?.[truong] === giaTri).length
    },
    async xoaHet(kho) {
      kiemKho(kho)
      luuTru.removeItem(tienTo + kho)
    },
    async capNhat(kho, khoa, ham) {
      kiemKho(kho)
      const o = doc(kho)
      const moi = ham(saoChep(o[khoa]))
      if (moi === null) delete o[khoa]
      else if (moi !== undefined) o[khoa] = moi
      if (moi !== undefined) viet(kho, o)
      return moi
    },
  }
}

const TEN_CSDL = 'smartport-tram'
const PHIEN_BAN_CSDL = 1

function moCsdl(idb, ten) {
  return new Promise((ok, loi) => {
    let yc
    try {
      yc = idb.open(ten, PHIEN_BAN_CSDL)
    } catch (e) {
      loi(e)
      return
    }
    yc.onupgradeneeded = () => {
      const db = yc.result
      for (const [kho, khoa] of Object.entries(CAC_KHO)) {
        const s = db.objectStoreNames.contains(kho) ? yc.transaction.objectStore(kho) : db.createObjectStore(kho, { keyPath: khoa })
        for (const truong of CHI_MUC[kho] || []) if (!s.indexNames.contains(truong)) s.createIndex(truong, truong)
      }
    }
    yc.onsuccess = () => ok(yc.result)
    yc.onerror = () => loi(yc.error || new Error('Không mở được IndexedDB'))
  })
}

/**
 * Kho IndexedDB. Moi thao tac ghi cho giao dich HOAN TAT (oncomplete) moi tra ve -> ben goi chi bao
 * "da ghi tam" khi du lieu DA nam tren dia (durability 'strict': ep ghi xuong dia truoc khi xong).
 */
export function taoKhoIndexedDb({ idb = globalThis.indexedDB, ten = TEN_CSDL } = {}) {
  let huaDb = null
  const layDb = () => {
    if (!huaDb) {
      huaDb = moCsdl(idb, ten).then((db) => {
        // Tab khac nang cap CSDL / trinh duyet dong ket noi -> lan sau tu mo lai
        db.onversionchange = () => {
          db.close()
          huaDb = null
        }
        db.onclose = () => {
          huaDb = null
        }
        return db
      })
      huaDb.catch(() => {
        huaDb = null
      })
    }
    return huaDb
  }

  async function chay(kho, cheDo, lam) {
    kiemKho(kho)
    const db = await layDb()
    return new Promise((ok, loi) => {
      let tx
      try {
        tx = cheDo === 'readwrite' ? db.transaction(kho, cheDo, { durability: 'strict' }) : db.transaction(kho, cheDo)
      } catch (e) {
        huaDb = null
        loi(e)
        return
      }
      let ketQua
      tx.oncomplete = () => ok(ketQua)
      tx.onerror = () => loi(tx.error || new Error('Lỗi ghi IndexedDB'))
      tx.onabort = () => loi(tx.error || new Error('Giao dịch IndexedDB bị hủy (có thể đầy bộ nhớ)'))
      try {
        lam(tx.objectStore(kho), (v) => {
          ketQua = v
        })
      } catch (e) {
        try {
          tx.abort()
        } catch {
          /* da hong san */
        }
        loi(e)
      }
    })
  }

  return {
    loai: 'indexeddb',
    lay: (kho, khoa) =>
      chay(kho, 'readonly', (s, tra) => {
        const r = s.get(khoa)
        r.onsuccess = () => tra(r.result)
      }),
    ghi: (kho, banGhi) => chay(kho, 'readwrite', (s) => s.put(banGhi)),
    xoa: (kho, khoa) => chay(kho, 'readwrite', (s) => s.delete(khoa)),
    tatCa: (kho) =>
      chay(kho, 'readonly', (s, tra) => {
        const r = s.getAll()
        r.onsuccess = () => tra(r.result || [])
      }),
    dem: (kho) =>
      chay(kho, 'readonly', (s, tra) => {
        const r = s.count()
        r.onsuccess = () => tra(r.result || 0)
      }),
    demTheo: (kho, truong, giaTri) =>
      chay(kho, 'readonly', (s, tra) => {
        const r = s.indexNames.contains(truong) ? s.index(truong).count(giaTri) : null
        if (r) r.onsuccess = () => tra(r.result || 0)
        else {
          const tc = s.getAll()
          tc.onsuccess = () => tra((tc.result || []).filter((v) => v?.[truong] === giaTri).length)
        }
      }),
    capNhat: (kho, khoa, ham) =>
      chay(kho, 'readwrite', (s, tra) => {
        const r = s.get(khoa)
        r.onsuccess = () => {
          // ham PHAI dong bo: await o day lam giao dich tu dong ket thuc giua chung
          const moi = ham(r.result)
          if (moi === null) s.delete(khoa)
          else if (moi !== undefined) s.put(moi)
          tra(moi)
        }
      }),
    xoaHet: (kho) => chay(kho, 'readwrite', (s) => s.clear()),
  }
}

/**
 * Kho tu chon: IndexedDB -> (khong mo duoc) localStorage -> (khong co) bo nho.
 * khiDoiLoai(loai) bao cho giao dien biet dang chay kho nao.
 *
 * Truoc day quyet dinh 1 LAN moi tab: IndexedDB loi THOANG QUA (dia ban, trinh duyet dang nang cap CSDL...) -> ca doi tab
 * chay localStorage; tai lai trang thi tab chon lai IndexedDB -> luot xe ghi trong localStorage MO COI (khong ai doc, khong
 * ai gui). Nay:
 *   1) Mo IndexedDB thu lai soLanThuIdb lan (lui 150 / 300 ms) truoc khi chiu du phong.
 *   2) Dang du phong (localStorage / bo nho): moi chuKyThuLaiMs, thao tac ke tiep thu mo lai IndexedDB; mo duoc thi CHUYEN
 *      moi ban ghi sang IndexedDB (them neu chua co — ban trong IndexedDB thang) roi moi doi kho. Thao tac goi trong luc
 *      chuyen DOI chuyen xong (khong doc nham kho rong).
 *   3) Mo duoc IndexedDB (ke ca luc vua mo trang): don ban ghi mo coi con trong localStorage 'sp-tram:*' sang IndexedDB.
 *   Xoa ban cu TUNG ban ghi, SAU KHI ban ghi do da nam trong IndexedDB -> dut giua chung khong mat gi (lan sau chuyen tiep;
 *   trung maThaoTac thi may chu tra "trung").
 */
export function taoKhoTuDong({
  idb = globalThis.indexedDB,
  luuTru,
  khiDoiLoai,
  taoKhoIdb = (i) => taoKhoIndexedDb({ idb: i }),
  soLanThuIdb = 3,
  choThuIdbMs = 150,
  chuKyThuLaiMs = 30000,
  dongHo = Date.now,
  ngu = (ms) => new Promise((ok) => setTimeout(ok, ms)),
} = {}) {
  let huaKho = null
  let khoHienTai = null
  let lanThuCuoi = 0
  let dangThu = false

  async function moIdb() {
    if (!idb) return null
    for (let i = 0; i < soLanThuIdb; i++) {
      const k = taoKhoIdb(idb)
      try {
        await k.dem('caiDat') // thu mo that su
        return k
      } catch {
        if (i < soLanThuIdb - 1) await ngu(choThuIdbMs * (i + 1))
      }
    }
    return null
  }

  /** Chuyen moi ban ghi tu kho nguon sang IndexedDB: them neu chua co, roi xoa ban ghi do o nguon */
  async function chuyenSang(dich, nguon) {
    let soChuyen = 0
    for (const ten of Object.keys(CAC_KHO)) {
      let ds = []
      try {
        ds = await nguon.tatCa(ten)
      } catch {
        continue
      }
      for (const bg of ds) {
        const khoa = bg?.[CAC_KHO[ten]]
        if (khoa === undefined || khoa === null) continue
        await dich.capNhat(ten, khoa, (cu) => (cu ? undefined : bg)) // nem loi -> dung, phan con lai chuyen lan sau
        await nguon.xoa(ten, khoa)
        soChuyen++
      }
    }
    return soChuyen
  }

  async function donLocalStorage(k) {
    if (!luuTru) return
    try {
      await chuyenSang(k, taoKhoLocalStorage(luuTru))
    } catch {
      /* IndexedDB ghi loi giua chung: ban ghi chua chuyen van nam trong localStorage, lan mo sau chuyen tiep */
    }
  }

  const datKho = (k) => {
    khoHienTai = k
    lanThuCuoi = dongHo()
    khiDoiLoai?.(k.loai)
    return k
  }

  const chon = () => {
    if (!huaKho) {
      huaKho = (async () => {
        const k = await moIdb()
        if (k) {
          await donLocalStorage(k)
          return k
        }
        if (luuTru) {
          try {
            luuTru.setItem('sp-tram:thu', '1')
            luuTru.removeItem('sp-tram:thu')
            return taoKhoLocalStorage(luuTru)
          } catch {
            /* localStorage cung bi chan */
          }
        }
        return taoKhoBoNho()
      })().then(datKho)
    }
    return huaKho
  }

  /** Dang du phong ma da qua chu ky -> thu lai IndexedDB (khong chan thao tac hien tai) */
  function thuLaiIdbNeuCan() {
    if (dangThu || !idb || !khoHienTai || khoHienTai.loai === 'indexeddb') return
    if (dongHo() - lanThuCuoi < chuKyThuLaiMs) return
    dangThu = true
    lanThuCuoi = dongHo()
    const cu = khoHienTai
    ;(async () => {
      const k = await moIdb()
      if (!k) return
      // Tu day moi thao tac moi deu DOI chuyen xong roi chay tren IndexedDB
      huaKho = (async () => {
        await ngu(0) // de thao tac da lay kho cu (dang cho microtask) chay xong tren kho cu truoc khi doc de chuyen
        try {
          await chuyenSang(k, cu)
          if (cu.loai !== 'local-storage') await donLocalStorage(k)
        } catch {
          return cu // IndexedDB lai hong giua chung: o lai kho du phong (ban ghi da chuyen van nam an toan trong IndexedDB)
        }
        return k
      })().then(datKho)
      await huaKho
    })()
      .catch(() => {})
      .finally(() => {
        dangThu = false
      })
  }

  const uyQuyen =
    (ten) =>
    async (...a) => {
      const k = await chon()
      thuLaiIdbNeuCan()
      return k[ten](...a)
    }
  return {
    get loai() {
      return 'tu-dong'
    },
    layLoai: async () => (await chon()).loai,
    lay: uyQuyen('lay'),
    ghi: uyQuyen('ghi'),
    xoa: uyQuyen('xoa'),
    tatCa: uyQuyen('tatCa'),
    dem: uyQuyen('dem'),
    demTheo: uyQuyen('demTheo'),
    capNhat: uyQuyen('capNhat'),
    xoaHet: uyQuyen('xoaHet'),
  }
}
