# -*- coding: utf-8 -*-
"""
DO BAO HO KHI NGUOI O XA CAMERA VA LUONG VIDEO BI NEN — so sanh cach suy luan va mo hinh, o muc TUNG NGUOI:
  (1) CA KHUNG  : chay mo hinh do bao ho tren ca khung camera 1280x720 (YOLO thu ve 640 -> nguoi 100 px chi con 50 px,
                  dau ~12 px: qua nho de thay mu)
  (2) CAT NGUOI : 2 TANG — da co khung nguoi, CAT rieng tung nguoi (+ le), PHONG TO len 320 px roi moi chay mo hinh do
                  bao ho (an_toan_cang.cat_nguoi — CUNG ham voi may canh bao that)
KHUNG NGUOI: may that lay khung nguoi tu bo phat hien YOLO COCO; O DAY lay khung Person NHAN THAT (n["p"]) — tuc coi khau
phat hien nguoi la hoan hao: F1 duoi day chi do khau DO BAO HO, KHONG tinh loi bo sot / lech khung nguoi cua YOLO COCO.
Mo phong camera: thu nho anh Construction-PPE de nguoi cao 200 / 120 / 80 px, dat vao khung 1280x720 ("goc" = anh nguyen
ban); moi co nguoi do 2 lan: khung KHONG NEN va khung NEN JPEG 70 (camera IP / trinh duyet deu nen). Nhan that, vung
xet THEO LOP (dg.vung — giong an_toan_cang._phieu_bao_ho), LUAT (thieu neu diem co < a va diem thieu >= b) giong
danh_gia_bao_ho_nguoi.py. Moi (mo hinh, cach) chon
1 bo (a, b) DUY NHAT tren VAL (gop moi dieu kien — luc chay that khong biet truoc nguoi xa / gan, nen hay khong) theo
F1 trung binh; TEST do 1 lan. Chon MO HINH theo F1 trung binh tren VAL (cach cat nguoi).
Chay: python danh_gia_bao_ho_khoang_cach.py [run ...]   vd: bao_ho bao_ho_ben
"""
import itertools
import os
import sys

import numpy as np

import danh_gia_bao_ho_nguoi as dg
from an_toan_cang import LE_CAT, cat_nguoi        # CUNG ham cat voi may canh bao that

GOC = os.path.dirname(os.path.abspath(__file__))
W_CAM, H_CAM = 1280, 720
CO_NGUOI = ("goc", 200, 120, 80)
NEN = (None, 70)                                    # chat luong JPEG cua khung camera (None = khong nen)
DIEU_KIEN = [(co, q) for co in CO_NGUOI for q in NEN]
CACH = {"ca_khung": None, "cat_320": 320}


def ten_dk(co, q):
    return "%s%s" % (co, "" if q is None else "_jpeg%d" % q)


def dat_vao_khung(img, p, co, q):
    """thu nho anh de nguoi p cao 'co' px, dat vao khung camera 1280x720 (nguoi o giua), nen JPEG q. (khung, p moi) / None"""
    import cv2
    h = p[3] - p[1]
    if co == "goc":
        khung, p2 = img, list(p)
    else:
        s = co / h
        if s >= 1.0:                                   # nguoi nho hon muc can mo phong: bo (khong phong to gia)
            return None
        nho = cv2.resize(img, (max(1, int(img.shape[1] * s)), max(1, int(img.shape[0] * s))), interpolation=cv2.INTER_AREA)
        khung = np.full((H_CAM, W_CAM, 3), 128, np.uint8)
        r = [c * s for c in p]
        dx, dy = int(W_CAM / 2 - (r[0] + r[2]) / 2), int(H_CAM * 0.6 - (r[1] + r[3]) / 2)
        xa, ya = max(0, dx), max(0, dy)
        xb, yb = min(W_CAM, dx + nho.shape[1]), min(H_CAM, dy + nho.shape[0])
        khung[ya:yb, xa:xb] = nho[ya - dy:yb - dy, xa - dx:xb - dx]
        p2 = [r[0] + dx, r[1] + dy, r[2] + dx, r[3] + dy]
    if q is not None:
        # anh RGB: cv2 nen theo BGR — dao kenh 2 lan, chi de nen
        khung = cv2.imdecode(cv2.imencode(".jpg", np.ascontiguousarray(khung[..., ::-1]), [cv2.IMWRITE_JPEG_QUALITY, q])[1],
                             cv2.IMREAD_COLOR)[..., ::-1]
    return khung, p2


def diem(det, p):
    """det: [(x1,y1,x2,y2,conf,lop)] toa do cung he voi p -> diem mu / khong_mu / ao / khong_ao"""
    d = dict(mu=0.0, khong_mu=0.0, ao=0.0, khong_ao=0.0)
    for x1, y1, x2, y2, cf, c in det:
        c = int(c)
        if c in dg.TEN_DIEM and dg.vung(p, ((x1 + x2) / 2, (y1 + y2) / 2), c):   # vung THEO LOP nhu may that
            k = dg.TEN_DIEM[c]
            d[k] = max(d[k], float(cf))
    return d


def chay(m, img, imgsz):
    """img RGB -> N x 6 (Ultralytics hieu mang numpy la BGR)"""
    r = m.predict(np.ascontiguousarray(img[..., ::-1]), imgsz=imgsz, conf=0.02, verbose=False, device=dg.thiet_bi())[0]
    b = r.boxes
    return np.c_[b.xyxy.cpu().numpy(), b.conf.cpu().numpy(), b.cls.cpu().numpy()] if len(b) else np.zeros((0, 6))


def tinh_diem(m, run, nguoi):
    from PIL import Image
    anh_cache = {}
    for n in nguoi:
        if n["anh"] not in anh_cache:
            anh_cache.clear()
            anh_cache[n["anh"]] = np.asarray(Image.open(n["anh"]).convert("RGB"))
        img = anh_cache[n["anh"]]
        H, W = img.shape[:2]
        p = [n["p"][0] * W, n["p"][1] * H, n["p"][2] * W, n["p"][3] * H]
        dk_n = n.setdefault("diem_kc", {}).setdefault(run, {})
        for co, q in DIEU_KIEN:
            dv = dat_vao_khung(img, p, co, q)
            if dv is None:
                continue
            khung, r = dv
            for cach, sz in CACH.items():
                if sz is None:
                    det = chay(m, khung, 640)
                else:
                    c, a1, b1 = cat_nguoi(khung, r)
                    det = chay(m, c, sz)
                    det[:, [0, 2]] += a1
                    det[:, [1, 3]] += b1
                dk_n[(cach, ten_dk(co, q))] = diem(det, r)


def cham(nguoi, run, khoa, tc, a, b):
    tp = fp = fn = tn = 0
    for n in nguoi:
        d = n["diem_kc"][run].get(khoa)
        if n[tc] is None or d is None:
            continue
        thieu = d[tc] < a and d["khong_" + tc] >= b
        if thieu and not n[tc]:
            tp += 1
        elif thieu:
            fp += 1
        elif not n[tc]:
            fn += 1
        else:
            tn += 1
    P, R = tp / max(1, tp + fp), tp / max(1, tp + fn)
    return dict(n=tp + fp + fn + tn, so_thieu_that=tp + fn, precision=round(P, 4), recall=round(R, 4),
                f1=round(2 * P * R / max(1e-9, P + R), 4), accuracy=round((tp + tn) / max(1, tp + fp + fn + tn), 4))


def main():
    from ultralytics import YOLO
    runs = sys.argv[1:] or ["bao_ho"]
    val, test = dg.nhan_that("val"), dg.nhan_that("test")
    print("nguoi co nhan: val %d, test %d" % (len(val), len(test)), flush=True)
    luoi = list(itertools.product((0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6), (0.0, 0.02, 0.05, 0.1, 0.2, 0.3)))
    dks = [ten_dk(co, q) for co, q in DIEU_KIEN]
    ket = {}
    for run in runs:
        m = YOLO(os.path.join(GOC, "runs", run, "weights", "best.pt"))
        tinh_diem(m, run, val)
        tinh_diem(m, run, test)
        ket[run] = {}
        for cach in CACH:
            ket[run][cach] = {}
            for tc in ("mu", "ao"):
                def f1_tb(ab):
                    return float(np.mean([cham(val, run, (cach, dk), tc, *ab)["f1"] for dk in dks]))
                a, b = max(luoi, key=f1_tb)
                test_dk = {dk: cham(test, run, (cach, dk), tc, a, b) for dk in dks}
                ket[run][cach][tc] = dict(a=a, b=b, val_f1_tb=round(f1_tb((a, b)), 4),
                                          test_f1_tb=round(float(np.mean([r["f1"] for r in test_dk.values()])), 4), test=test_dk)
                print("%-11s %-9s %-3s a=%.2f b=%.2f | VAL F1 tb %.3f | TEST F1 %s" % (
                    run, cach, tc, a, b, f1_tb((a, b)), " ".join("%s:%.2f" % (dk, test_dk[dk]["f1"]) for dk in dks)), flush=True)
        ket[run]["ca_khung_luat_cu"] = {tc: {dk: cham(test, run, ("ca_khung", dk), tc, *ab) for dk in dks}
                                        for tc, ab in (("mu", (0.30, 0.05)), ("ao", (0.15, 0.05)))}
    chon = max(runs, key=lambda r: ket[r]["cat_320"]["mu"]["val_f1_tb"] + ket[r]["cat_320"]["ao"]["val_f1_tb"])
    ra = dict(chon_mo_hinh=chon, chon_theo="F1 trung binh (thieu mu + thieu ao), cach cat_320, tren VAL moi dieu kien",
              mo_hinh=runs, dieu_kien=dks, khung_camera="%dx%d" % (W_CAM, H_CAM), cach=list(CACH), le_cat=LE_CAT,
              chon_nguong="1 bo (a, b) moi (mo hinh, cach), F1 trung binh moi dieu kien tren VAL", ket_qua=ket,
              so_nguoi=dict(val=len(val), test=len(test)), vung_xet=dg.MO_TA_VUNG, khung_nguoi=dg.MO_TA_KHUNG_NGUOI)
    dg.ghi_json(os.path.join(GOC, "runs", "danh_gia", "bao_ho_theo_khoang_cach.json"), ra)   # loi giua chung: JSON cu con nguyen
    print("CHON:", chon, "| a, b mu", ket[chon]["cat_320"]["mu"]["a"], ket[chon]["cat_320"]["mu"]["b"],
          "| ao", ket[chon]["cat_320"]["ao"]["a"], ket[chon]["cat_320"]["ao"]["b"])


if __name__ == "__main__":
    main()
