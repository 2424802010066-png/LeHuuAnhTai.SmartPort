# -*- coding: utf-8 -*-
"""
CAI TIEN DOC BIEN SO: GOP 2 MO HINH (YOLO26 + RF-DETR) + GIAI MA THEO CU PHAP BIEN SO VIET NAM.
  - Moi mo hinh tim khung ky tu (nguong thap 0.05) -> gom khung trung nhau (IoU > 0.5) thanh 1 VI TRI ky tu:
      p_co  = trung binh (theo trong so mo hinh) do tin cay lon nhat cua moi mo hinh tai vi tri do
      q(c)  = phan bo lop ky tu = tong do tin cay x trong so cua cac khung lop c
  - Giai ma = QUY HOACH DONG chon tap vi tri giu lai + ky tu cho tung vi tri, sao cho chuoi khop 1 MAU bien so
    hoc tu nhan TRAIN (DD L [L|D] D{3..5}; bien 2 dong: dong tren dung 4 ky tu), toi da
      tong log p_co + log q(ky tu)  (vi tri giu)  +  tong log (1 - p_co)  (vi tri bo)  +  lambda * log P(mau)
    -> tu bo khung thua (oc vit, vien bien), sua nham chu/so theo vi tri, chon do dai dung.
  Chinh tham so tren VAL, do TEST 1 lan.
Chay: python cai_tien_bien_so.py [chuan_bi] [chinh] [test '<json cau hinh>']
"""
import collections
import csv
import itertools
import json
import math
import os
import pickle
import sys
import time

import numpy as np
from PIL import Image

import danh_gia_bien_so as dg

GOC = os.path.dirname(os.path.abspath(__file__))
RA = os.path.join(GOC, "runs", "danh_gia")
BO_DEM = os.path.join(RA, "bo_dem_bien_so_kytu.pkl")
TEP_MAU = os.path.join(GOC, "du_lieu", "ky_tu", "mau_bien_so.json")
KT = dg.KT


# ------------------------------------------------------------------ mau bien so tu nhan TRAIN
def hoc_mau():
    """dem mau D/L theo so dong tren nhan TRAIN (anh cat bien) -> {so_dong: {mau: so_lan}}"""
    nhan = {x["file"]: x for x in csv.DictReader(open(os.path.join(dg.NGUON, "nhan_bien_so.csv"), encoding="utf-8"))
            if x["dang_VN"] == "True"}
    dem = collections.defaultdict(collections.Counter)
    for a in open(os.path.join(dg.DL, "ky_tu", "yolo", "train.txt"), encoding="utf-8").read().split("\n"):
        x = nhan.get(os.path.basename(a))
        if a and x:
            dem[x["so_dong"]]["".join("D" if c.isdigit() else "L" for c in x["bien_so"])] += 1
    ra = {k: dict(v) for k, v in dem.items()}
    json.dump(ra, open(TEP_MAU, "w", encoding="utf-8"), indent=1)
    return ra


_MAU = [None]


def mau_bien():
    if _MAU[0] is None:
        _MAU[0] = json.load(open(TEP_MAU, encoding="utf-8")) if os.path.exists(TEP_MAU) else hoc_mau()
    return _MAU[0]


# ------------------------------------------------------------------ gom khung 2 mo hinh
def gom_vi_tri(dets, trong_so, iou_gom=0.5, p_min=0.08, chuan_hoa=False, p_kieu="tb"):
    """dets: list mang N x 6 (x1,y1,x2,y2,conf,lop) cua TUNG mo hinh; trong_so: list trong so mo hinh.
    chuan_hoa=True: phieu lop cua MOI mo hinh tai 1 vi tri duoc chuan hoa thanh phan bo (tong = conf lon nhat cua no)
    -> mo hinh tra nhieu khung trung (nguong thap) khong lan at mo hinh kia.
    Tra list dict(hop, p_co, q[36], cy, cx, h)"""
    tat_ca = [(d, mi) for mi, ds in enumerate(dets) for d in (ds if len(ds) else [])]
    tat_ca.sort(key=lambda t: -float(t[0][4]))
    cum = []
    for d, mi in tat_ca:
        for c in cum:
            if dg.iou(d, c["dai_dien"]) > iou_gom:
                c["tv"].append((d, mi))
                break
        else:
            cum.append(dict(dai_dien=d, tv=[(d, mi)]))
    tong_w = sum(trong_so)
    ra = []
    for c in cum:
        q = np.full(len(KT), 1e-3)
        qm = [np.zeros(len(KT)) for _ in dets]
        cmax = [0.0] * len(dets)
        hop = np.zeros(4)
        sw = 0.0
        for d, mi in c["tv"]:
            w = trong_so[mi] * float(d[4])
            qm[mi][int(d[5])] += float(d[4])
            cmax[mi] = max(cmax[mi], float(d[4]))
            hop += w * np.asarray(d[:4], dtype=float)
            sw += w
        if sw <= 0:
            continue
        for mi in range(len(dets)):
            if qm[mi].sum() > 0:
                q += trong_so[mi] * (qm[mi] / qm[mi].sum() * cmax[mi] if chuan_hoa else qm[mi])
        if p_kieu == "noisy_or":
            # "co ky tu" neu IT NHAT 1 mo hinh thay ro: 1 - tich (1 - conf)^(trong so / trong so lon nhat)
            wmax = max(trong_so)
            p = 1 - float(np.prod([(1 - cmax[mi]) ** (trong_so[mi] / wmax) for mi in range(len(dets)) if trong_so[mi] > 0]))
        else:
            p = sum(trong_so[mi] * cmax[mi] for mi in range(len(dets))) / tong_w
        if p < p_min:
            continue
        hop /= sw
        ra.append(dict(hop=hop, p_co=float(np.clip(p, 0.02, 0.98)), q=q / q.sum(),
                       cx=(hop[0] + hop[2]) / 2, cy=(hop[1] + hop[3]) / 2, h=hop[3] - hop[1]))
    return ra


def chia_dong(vt):
    """tach 1/2 dong theo khoang trong lon nhat giua tam y (giong ghep_ky_tu), sap trai -> phai"""
    if not vt:
        return []
    cy = np.array([v["cy"] for v in vt])
    h = float(np.median([v["h"] for v in vt]))
    ys = np.sort(cy)
    if len(ys) > 1 and np.diff(ys).max() > 0.5 * h:
        cat = ys[np.diff(ys).argmax()] + np.diff(ys).max() / 2
        dong = [[v for v in vt if v["cy"] < cat], [v for v in vt if v["cy"] >= cat]]
    else:
        dong = [vt]
    return [sorted(d, key=lambda v: v["cx"]) for d in dong]


SO_IDX = [KT.index(c) for c in "0123456789"]
CHU_IDX = [KT.index(c) for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"]


def _dp(vt, mau):
    """quy hoach dong: khop day vi tri vt (da sap) voi mau (chuoi 'D'/'L'), duoc bo vi tri.
    Tra (diem, chuoi, tong log p cac ky tu GIU) tot nhat hoac (-inf, "", 0)"""
    n, L = len(vt), len(mau)
    NEG = -1e18
    dp = [[(NEG, "", 0.0)] * (L + 1) for _ in range(n + 1)]
    dp[0][0] = (0.0, "", 0.0)
    lq = [np.log(v["q"]) for v in vt]
    for i in range(n):
        lp, lbo = math.log(vt[i]["p_co"]), math.log(1 - vt[i]["p_co"])
        for j in range(L + 1):
            s, chuoi, giu = dp[i][j]
            if s <= NEG / 2:
                continue
            if s + lbo > dp[i + 1][j][0]:                       # bo vi tri i
                dp[i + 1][j] = (s + lbo, chuoi, giu)
            if j < L:                                           # giu vi tri i cho ky tu thu j cua mau
                idx = SO_IDX if mau[j] == "D" else CHU_IDX
                k = idx[int(np.argmax(lq[i][idx]))]
                s2 = s + lp + lq[i][k]
                if s2 > dp[i + 1][j + 1][0]:
                    dp[i + 1][j + 1] = (s2, chuoi + KT[k], giu + lp + lq[i][k])
    return dp[n][L]


def giai_ma(dets, ch):
    """dets: [yolo, rfdetr] (N x 6 moi cai) -> (chuoi, diem)"""
    chuoi, diem, _ = giai_ma_tin_cay(dets, ch)
    return chuoi, diem


def giai_ma_tin_cay(dets, ch):
    """nhu giai_ma, tra them DO TIN CAY = trung binh nhan (p_co x q(ky tu)) cua cac ky tu duoc giu:
    ~0.9 khi moi ky tu ro, ~0.1 khi phai 'ghep' tu khung mo (anh qua nho, bien la ngoai mau VN) -> de TU CHOI"""
    w = [1.0, ch["w_rf"]]
    # lop >= 36: o "nen" phu cua RF-DETR (num_classes + 1) lot ra o nguong thap -> bo
    dets2 = [d[(d[:, 4] >= ch["nguong"]) & (d[:, 5] < len(KT))] if len(d) else d for d in dets]
    if ch["w_rf"] <= 0:
        dets2[1] = np.zeros((0, 6))
    vt = gom_vi_tri(dets2, w, p_min=ch["p_min"], chuan_hoa=ch.get("chuan_hoa", False), p_kieu=ch.get("p_kieu", "tb"))
    dong = chia_dong(vt)
    if not dong:
        return "", -1e18, 0.0
    mau = mau_bien()
    so_dong = "2" if len(dong) == 2 else "1"
    tk = mau.get(so_dong) or {}
    tong = sum(tk.values()) or 1
    tot = (-1e18, "", 0.0)
    for m, n in tk.items():
        prior = ch["lam"] * math.log(n / tong)
        if so_dong == "2":
            s1, c1, g1 = _dp(dong[0], m[:4])
            s2, c2, g2 = _dp(dong[1], m[4:])
            s, c, g = s1 + s2, (c1 or "") + (c2 or ""), g1 + g2
        else:
            s, c, g = _dp(dong[0], m)
        if s + prior > tot[0] and len(c) == len(m):
            tot = (s + prior, c, g)
    if tot[1] == "":                                            # khong khop mau nao -> doc thuong (argmax)
        giu = [v for d in dong for v in d if v["p_co"] >= 0.5]
        tc = math.exp(float(np.mean([math.log(v["p_co"] * float(v["q"].max())) for v in giu]))) if giu else 0.0
        return "".join(KT[int(np.argmax(v["q"]))] for v in giu), -1e18, tc
    return tot[1], tot[0], math.exp(tot[2] / len(tot[1]))


# ------------------------------------------------------------------ chuan bi bo dem khung ky tu
def chuan_bi():
    ky_y = dg.PhatHien("yolo_kytu", imgsz=320, nguong=0.05)
    ky_r = dg.PhatHien("rfdetr_kytu", imgsz=320, nguong=0.05)
    bien = dg.PhatHien("yolo_bien", imgsz=640, nguong=0.25)
    bd = {}
    for tap in ("val", "test"):
        for che_do in ("cat", "e2e"):
            ds = dg.lay_tap(che_do, tap)
            t0 = time.time()
            for d in ds:
                img = Image.open(d["duong"]).convert("RGB")
                if che_do == "e2e":
                    hb = bien(img)
                    if not len(hb):
                        bd[(tap, che_do, d["id"])] = dict(d=d, dets=None)
                        continue
                    img = dg.cat_bien(img, hb[np.argmax(hb[:, 4])], 0.1)
                bd[(tap, che_do, d["id"])] = dict(d=d, dets=[ky_y(img), ky_r(img)])
            print(tap, che_do, len(ds), "anh %.0fs" % (time.time() - t0), flush=True)
    pickle.dump(bd, open(BO_DEM, "wb"))


BO_DEM_BT = os.path.join(RA, "bo_dem_bien_so_e2e_bien_the.pkl")


def chuan_bi_bien_the():
    """e2e voi cac BIEN THE cat bien: le cat (0.05/0.1) x co anh bo tim bien (640/960) -> chon tren VAL"""
    ky_y = dg.PhatHien("yolo_kytu", imgsz=320, nguong=0.05)
    ky_r = dg.PhatHien("rfdetr_kytu", imgsz=320, nguong=0.05)
    bd = {}
    for co in (640, 960):
        bien = dg.PhatHien("yolo_bien", imgsz=co, nguong=0.25)
        for tap in ("val", "test"):
            ds = dg.lay_tap("e2e", tap)
            t0 = time.time()
            for d in ds:
                img = Image.open(d["duong"]).convert("RGB")
                hb = bien(img)
                for le in (0.05, 0.1):
                    if not len(hb):
                        bd[(tap, "e2e_%d_%.2f" % (co, le), d["id"])] = dict(d=d, dets=None)
                        continue
                    cat = dg.cat_bien(img, hb[np.argmax(hb[:, 4])], le)
                    bd[(tap, "e2e_%d_%.2f" % (co, le), d["id"])] = dict(d=d, dets=[ky_y(cat), ky_r(cat)])
            print(co, tap, len(ds), "anh %.0fs" % (time.time() - t0), flush=True)
    pickle.dump(bd, open(BO_DEM_BT, "wb"))


def chinh_bien_the(ch):
    bd = pickle.load(open(BO_DEM_BT, "rb"))
    for bt in sorted({k[1] for k in bd}):
        cu, _ = cham(bd, "val", bt, dict(cach="cu"))
        moi, _ = cham(bd, "val", bt, ch)
        print("VAL", bt, "| cu", cu, "| gop", moi, flush=True)


def test_bien_the(ch, bt):
    bd = pickle.load(open(BO_DEM_BT, "rb"))
    cu, _ = cham(bd, "test", bt, dict(cach="cu"))
    moi, kq = cham(bd, "test", bt, ch)
    bang = dg.bang_diem([dict(r, ms=None) for r in kq], "doc")
    print("TEST", bt, "| cu", cu, "| gop", json.dumps(bang, ensure_ascii=False)[:600], flush=True)
    json.dump({"cau_hinh": ch, "bien_the": bt, "cu": cu, "bang": {"sau_sua_dinh_dang": bang}, "chi_tiet": kq},
              open(os.path.join(RA, "bien_so_gop_e2e_test.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def cham(bd, tap, che_do, ch):
    kq = []
    for (t, c, _), b in bd.items():
        if t != tap or c != che_do:
            continue
        d = b["d"]
        if b["dets"] is None:
            doc = ""
        elif ch.get("cach") == "cu":
            y = b["dets"][0]
            raw, _ = dg.ghep_ky_tu(y[y[:, 4] >= 0.3] if len(y) else y)
            doc = dg.sua_dinh_dang_vn(raw)
        else:
            doc, _ = giai_ma(b["dets"], ch)
        kq.append(dict(id=d["id"], nhom=d["nhom"], nhan=d["nhan"], doc=doc))
    n = len(kq)
    dung = sum(r["doc"] == r["nhan"] for r in kq)
    return dict(n=n, dung=dung, ti_le=round(100 * dung / max(1, n), 2)), kq


def chinh():
    bd = pickle.load(open(BO_DEM, "rb"))
    mau_bien()
    for che_do in ("cat", "e2e"):
        print(che_do, "CU (YOLO, ghep + sua dinh dang):", cham(bd, "val", che_do, dict(cach="cu"))[0], flush=True)
    bang = []
    for w_rf, nguong, p_min, lam, p_kieu in itertools.product((0.0, 0.75, 1.0, 1.5), (0.05,), (0.1,),
                                                               (0.25, 0.5, 1.0, 2.0), ("tb", "noisy_or")):
        ch = dict(w_rf=w_rf, nguong=nguong, p_min=p_min, lam=lam, chuan_hoa=False, p_kieu=p_kieu)
        r_cat, _ = cham(bd, "val", "cat", ch)
        r_e2e, _ = cham(bd, "val", "e2e", ch)
        bang.append((ch, r_cat, r_e2e))
    bang.sort(key=lambda x: -(x[1]["dung"] + x[2]["dung"]))
    for ch, a, b in bang[:12]:
        print(a, b, ch, flush=True)
    json.dump([dict(cau_hinh=c, cat=a, e2e=b) for c, a, b in bang], open(os.path.join(RA, "chinh_bien_so_val.json"), "w",
                                                                        encoding="utf-8"), indent=1)


def do_test(ch):
    bd = pickle.load(open(BO_DEM, "rb"))
    ra = {}
    for che_do in ("cat", "e2e"):
        cu, _ = cham(bd, "test", che_do, dict(cach="cu"))
        moi, kq = cham(bd, "test", che_do, ch)
        bang = dg.bang_diem([dict(r, ms=None) for r in kq], "doc")
        ra[che_do] = dict(cu=cu, moi=moi, bang=bang)
        print("TEST", che_do, "cu", cu, "| moi", json.dumps(bang, ensure_ascii=False)[:420], flush=True)
        json.dump({"cau_hinh": ch, "bang": {"sau_sua_dinh_dang": bang}, "chi_tiet": kq},
                  open(os.path.join(RA, "bien_so_gop_%s_test.json" % che_do), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return ra


if __name__ == "__main__":
    a = sys.argv[1:] or ["chuan_bi", "chinh"]
    if "chuan_bi" in a:
        hoc_mau()
        chuan_bi()
    if "chinh" in a:
        chinh()
    if "test" in a:
        do_test(json.loads(a[a.index("test") + 1]))
    if "bien_the" in a:
        chuan_bi_bien_the()
    if "chinh_bien_the" in a:
        chinh_bien_the(json.loads(a[a.index("chinh_bien_the") + 1]))
    if "test_bien_the" in a:
        i = a.index("test_bien_the")
        test_bien_the(json.loads(a[i + 1]), a[i + 2])
