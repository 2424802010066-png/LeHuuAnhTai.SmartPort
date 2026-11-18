# -*- coding: utf-8 -*-
"""
CHON NGUONG TU CHOI cho bo doc bien so (gop YOLO26 + RF-DETR + quy hoach dong theo mau VN).
  Van de: quy hoach dong LUON ep ra 1 chuoi dung mau VN — ke ca khi anh qua nho / bien la (bien do quan doi,
  bien nuoc ngoai) -> "doc" ra bien hop le nhung SAI (nguy hiem o cong: mo barrier nham xe).
  Cach lam: do tin cay = trung binh nhan (p_co x q) cac ky tu duoc giu. Duoi nguong -> tra "khong chac", chuyen kiem tay.
  Nguong chon tren VAL (giu >= 99.5% so bien doc DUNG cua VAL), roi do TEST 1 lan.
Chay: python chon_nguong_tin_cay_bien_so.py
"""
import json
import os
import pickle

import cai_tien_bien_so as ct

GOC = os.path.dirname(os.path.abspath(__file__))
CH = dict(w_rf=1.5, nguong=0.05, p_min=0.1, lam=1.0, chuan_hoa=False, p_kieu="noisy_or")   # = CAU_HINH_BIEN dich vu
GIU_DUNG_TOI_THIEU = 0.995


def cham(bd, tap):
    kq = []
    for (t, c, _), b in bd.items():
        if t != tap or c != "e2e":
            continue
        d = b["d"]
        if b["dets"] is None:
            kq.append(dict(id=d["id"], nhan=d["nhan"], doc="", tc=0.0))
            continue
        doc, _, tc = ct.giai_ma_tin_cay(b["dets"], CH)
        kq.append(dict(id=d["id"], nhan=d["nhan"], doc=doc, tc=round(tc, 4)))
    return kq


def thong_ke(kq, nguong):
    n = len(kq)
    nhan = [r for r in kq if r["doc"] and r["tc"] >= nguong]
    dung = sum(r["doc"] == r["nhan"] for r in nhan)
    sai = len(nhan) - dung
    return dict(n=n, nguong=nguong, dung=dung, ti_le_dung=round(100 * dung / n, 2), sai_lot_qua=sai,
                ti_le_sai_lot_qua=round(100 * sai / n, 2), tu_choi=n - len(nhan), ti_le_tu_choi=round(100 * (n - len(nhan)) / n, 2))


if __name__ == "__main__":
    bd = pickle.load(open(ct.BO_DEM, "rb"))
    val = cham(bd, "val")
    dung_val = sorted(r["tc"] for r in val if r["doc"] == r["nhan"])
    sai_val = sorted(r["tc"] for r in val if r["doc"] and r["doc"] != r["nhan"])
    print("VAL: %d anh | dung %d (tc trung vi %.3f, min %.3f) | sai %d (tc trung vi %.3f)" % (
        len(val), len(dung_val), dung_val[len(dung_val) // 2], dung_val[0], len(sai_val), sai_val[len(sai_val) // 2] if sai_val else 0))
    # nguong lon nhat ma van giu >= 99.5% bien doc dung cua VAL
    k = int((1 - GIU_DUNG_TOI_THIEU) * len(dung_val))
    nguong = round(max(0.0, dung_val[k] - 1e-4), 3)
    for t in (0.0, 0.1, 0.2, nguong, 0.3, 0.4):
        print("  VAL", thong_ke(val, t))
    test = cham(bd, "test")
    truoc, sau = thong_ke(test, 0.0), thong_ke(test, nguong)
    print("TEST truoc:", truoc)
    print("TEST sau  :", sau)
    json.dump(dict(cau_hinh=CH, nguong=nguong, cach_chon="giu >= %.1f%% bien doc dung cua VAL" % (100 * GIU_DUNG_TOI_THIEU),
                   val=thong_ke(val, nguong), val_khong_nguong=thong_ke(val, 0.0), test_truoc=truoc, test_sau=sau),
              open(os.path.join(GOC, "runs", "danh_gia", "nguong_tin_cay_bien_so.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("nguong =", nguong)
