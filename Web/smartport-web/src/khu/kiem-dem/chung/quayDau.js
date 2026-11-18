/* ============================================================
   GIAM SAT QUAY DAU DA HUONG (chuyen tu taoGiamSatQuayDau cua driver.js)
   Mat tap trung = roi mat khoi duong >= 5 giay theo BAT KY huong nao:
   trai / phai / ngang len / cui xuong / nghieng dau.
   Cho phep "ho" 450ms: model nhap nhay mat dau hieu 1-2 khung hinh thi
   van tinh la mot lan quay dau lien tuc, khong dem lai tu dau.
   ============================================================ */

export const HUONG_VIET = {
  '→': 'sang phải',
  '←': 'sang trái',
  '↑': 'lên trên',
  '↓': 'xuống dưới',
  '↺': 'nghiêng đầu',
}

export const tenHuong = (h) => HUONG_VIET[h] || 'hướng khác'

export function taoGiamSatQuayDau(cfg = {}) {
  const NG_YAW = cfg.yaw ?? 22 // do — quay trai/phai
  const NG_LEN = cfg.len ?? -16 // pitch am = ngang len
  const NG_XUONG = cfg.xuong ?? 20 // pitch duong = cui xuong
  const NG_ROLL = cfg.roll ?? 20 // nghieng dau
  const GIU_MS = cfg.giuMs ?? 5000 // giu lien tuc bao lau thi bao dong
  const HO_MS = cfg.hoMs ?? 450 // cho phep mat dau hieu thoang qua
  const st = { tu: 0, cuoi: 0, huong: '' }

  function huongLech(goc) {
    if (goc.yaw > NG_YAW) return '→'
    if (goc.yaw < -NG_YAW) return '←'
    if (goc.pitch < NG_LEN) return '↑'
    if (goc.pitch > NG_XUONG) return '↓'
    if (Math.abs(goc.roll) > NG_ROLL) return '↺'
    return ''
  }

  return {
    nguongGiay: GIU_MS / 1000,
    buoc(goc, now) {
      const h = huongLech(goc)
      if (h) {
        if (!st.tu || now - st.cuoi > HO_MS) st.tu = now
        st.cuoi = now
        st.huong = h
      } else if (st.tu && now - st.cuoi > HO_MS) {
        st.tu = 0
        st.cuoi = 0
        st.huong = ''
      }
      const giay = st.tu ? (st.cuoi - st.tu) / 1000 : 0
      return { lech: !!st.tu, huong: st.huong, giay, baoDong: st.tu > 0 && giay >= GIU_MS / 1000 }
    },
    /** Mat dau khuon mat khi DANG quay dau (quay han qua gat) -> van tinh tiep thoi gian roi mat khoi duong */
    matMat(now) {
      if (!st.tu) return { lech: false, huong: '', giay: 0, baoDong: false }
      st.cuoi = now
      const giay = (st.cuoi - st.tu) / 1000
      return { lech: true, huong: st.huong, giay, baoDong: giay >= GIU_MS / 1000 }
    },
    datLai() {
      st.tu = 0
      st.cuoi = 0
      st.huong = ''
    },
  }
}
