# -*- coding: utf-8 -*-
"""
DANH GIA DO BAO HO O MUC TUNG NGUOI (dung nghiep vu canh bao: "nguoi nay THIEU mu / THIEU ao phan quang?")
  Nhan that: moi khung Person (lop 6) cua tap Construction-PPE; vung xet THEO LOP, giong het may canh bao that
  (an_toan_cang._phieu_bao_ho): helmet (co mu) / no_helmet (dau tran) co tam o -0,1..0,4 chieu cao nguoi; vest (co ao) /
  none (khong ao) co tam o 0,15..0,85 (2 dai chong nhau o 0,15..0,4: hop ao o do VAN tinh cho ao); x trong +-10% be rong.
  Nguoi khong co nhan mu / ao trong vung -> bo qua cho tieu chi do.
  KHUNG NGUOI = khung Person NHAN THAT (coi khau phat hien nguoi la hoan hao) — F1 o day KHONG tinh loi bo sot / lech khung
  cua bo phat hien nguoi YOLO COCO ma may that dung.
  Du doan: mo hinh do bao ho chay nguong thap (0,05) -> diem mu = max conf "helmet" trong vung mu, diem dau tran = max conf
  "no_helmet"; tuong tu ao. LUAT: THIEU neu diem co < a VA diem thieu >= b (b = 0: chi can KHONG thay mu/ao).
  -> mo hinh "khong co mu" dua tren lop MANH (helmet AP50 0,92) thay vi lop yeu (no_helmet AP50 0,23).
  Chon (a, b) tren VAL theo F1 cua lop "thieu" (canh bao vi pham), do TEST 1 lan.
Chay: python danh_gia_bao_ho_nguoi.py <thu muc run> [<thu muc run 2> ...]   vd: bao_ho bao_ho_960
      python danh_gia_bao_ho_nguoi.py --tu-kiem     (kiem nhanh ham vung() + doi chieu an_toan_cang._phieu_bao_ho,
                                                    khong can du lieu / GPU; SMARTPORT.bat kiem-thu-nhanh co chay)
"""
import functools
import glob
import itertools
import json
import os
import sys

import numpy as np

os.environ.setdefault("YOLO_OFFLINE", "1")
GOC = os.path.dirname(os.path.abspath(__file__))
DL = os.environ.get("SMARTPORT_PPE_DIR", r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat\construction_ppe")
MU, AO, KHONG_AO, NGUOI, KHONG_MU = 0, 2, 5, 6, 7
TEN_DIEM = {MU: "mu", KHONG_MU: "khong_mu", AO: "ao", KHONG_AO: "khong_ao"}
# dai chieu cao (ti le tu dinh khung nguoi) xet cho tung lop — PHAI GIU GIONG an_toan_cang._phieu_bao_ho
VUNG_LOP = {MU: (-0.1, 0.4), KHONG_MU: (-0.1, 0.4), AO: (0.15, 0.85), KHONG_AO: (0.15, 0.85)}
MO_TA_VUNG = "theo lop (giong an_toan_cang._phieu_bao_ho): mu / dau tran tam o -0.1..0.4 chieu cao, ao / khong ao 0.15..0.85"
MO_TA_KHUNG_NGUOI = "khung Person NHAN THAT (coi khau phat hien nguoi hoan hao, khong tinh loi bo sot / lech khung YOLO COCO)"


def vung(p, c, lop):
    """hop lop 'lop', tam c (x, y), co nam trong vung xet cua nguoi p (x1, y1, x2, y2)? -> True / False.
    Xet THEO LOP — phai giu giong an_toan_cang._phieu_bao_ho: mu / dau tran -0,1..0,4 chieu cao, ao / khong ao
    0,15..0,85, x trong +-10% be rong. (Ban cu xet 'dau' TRUOC nen ao / khong ao co tam o 0,15..0,4 bi bo -> lech may that.)"""
    lop = int(lop)
    if lop not in VUNG_LOP:
        return False
    x1, y1, x2, y2 = p
    h, w = y2 - y1, x2 - x1
    if not (x1 - 0.1 * w <= c[0] <= x2 + 0.1 * w):
        return False
    tu, den = VUNG_LOP[lop]
    return tu <= (c[1] - y1) / max(h, 1e-6) <= den


@functools.lru_cache(maxsize=None)
def thiet_bi():
    """GPU 0 neu co CUDA, khong thi CPU (cham hon nhieu). Dat SMARTPORT_THIET_BI (vd 'cpu', '1') de chon tay —
    vd khi GPU dang bi dich vu AI chiem het bo nho."""
    tb = os.environ.get("SMARTPORT_THIET_BI", "").strip()
    if tb:
        return int(tb) if tb.isdigit() else tb
    import torch
    if torch.cuda.is_available():
        return 0
    print("CANH BAO: khong co CUDA -> chay tren CPU (cham hon GPU nhieu lan)", flush=True)
    return "cpu"


def ghi_json(duong, du_lieu):
    """ghi qua tep tam roi os.replace: loi giua chung thi tep JSON cu con nguyen, khong bi cat cut"""
    os.makedirs(os.path.dirname(duong), exist_ok=True)
    tam = duong + ".tam"
    try:
        with open(tam, "w", encoding="utf-8") as f:
            json.dump(du_lieu, f, ensure_ascii=False, indent=1)
        os.replace(tam, duong)
    finally:
        if os.path.exists(tam):                       # loi giua chung: don tep tam
            os.remove(tam)


def nhan_that(tap):
    ra = []
    for f in sorted(glob.glob(os.path.join(DL, "labels", tap, "*.txt"))):
        anh = [os.path.join(DL, "images", tap, os.path.basename(f)[:-4] + d) for d in (".jpg", ".jpeg", ".png")]
        anh = next((a for a in anh if os.path.exists(a)), None)     # bo du lieu co ca .jpeg / .png (truoc day bo sot)
        if anh is None:
            continue
        L = [list(map(float, l.split()[:5])) for l in open(f).read().splitlines() if l.strip()]
        nguoi = [(x - w / 2, y - h / 2, x + w / 2, y + h / 2) for c, x, y, w, h in L if int(c) == NGUOI]
        for p in nguoi:
            mu = ao = None
            for c, x, y, w, h in L:
                c = int(c)
                if not vung(p, (x, y), c):
                    continue
                if c in (MU, KHONG_MU):
                    mu = c == MU if mu is None else mu or c == MU
                elif c in (AO, KHONG_AO):
                    ao = c == AO if ao is None else ao or c == AO
            if mu is not None or ao is not None:
                ra.append(dict(anh=anh, p=p, mu=mu, ao=ao))
    return ra


def du_doan(run, tap, nguoi):
    from PIL import Image
    from ultralytics import YOLO
    m = YOLO(os.path.join(GOC, "runs", run, "weights", "best.pt"))
    co = int(json.load(open(os.path.join(GOC, "runs", run, "ket_qua.json")))["mo_hinh"].split("anh ")[-1]) \
        if "anh " in json.load(open(os.path.join(GOC, "runs", run, "ket_qua.json")))["mo_hinh"] else 640
    cache = {}
    for n in nguoi:
        if n["anh"] not in cache:
            img = Image.open(n["anh"]).convert("RGB")
            r = m.predict(img, imgsz=co, conf=0.05, verbose=False, device=thiet_bi())[0]
            W, H = img.size
            b = r.boxes
            cache[n["anh"]] = [(float((x1 + x2) / 2 / W), float((y1 + y2) / 2 / H), float(cf), int(c))
                               for (x1, y1, x2, y2), cf, c in zip(b.xyxy.cpu().numpy(), b.conf.cpu().numpy(), b.cls.cpu().numpy())]
        d = dict(mu=0.0, khong_mu=0.0, ao=0.0, khong_ao=0.0)
        for cx, cy, cf, c in cache[n["anh"]]:
            if c in TEN_DIEM and vung(n["p"], (cx, cy), c):
                d[TEN_DIEM[c]] = max(d[TEN_DIEM[c]], cf)
        n.setdefault("diem", {})[run] = d
    return nguoi


def cham(nguoi, run, tieu_chi, a, b):
    """tieu_chi 'mu' | 'ao' -> dict P/R/F1 cua lop THIEU + do chinh xac"""
    tp = fp = fn = tn = 0
    for n in nguoi:
        that = n[tieu_chi]
        if that is None:
            continue
        d = n["diem"][run]
        thieu = d[tieu_chi] < a and d["khong_" + tieu_chi] >= b
        if thieu and not that:
            tp += 1
        elif thieu and that:
            fp += 1
        elif not thieu and not that:
            fn += 1
        else:
            tn += 1
    P = tp / max(1, tp + fp)
    R = tp / max(1, tp + fn)
    return dict(n=tp + fp + fn + tn, so_thieu_that=tp + fn, precision=round(P, 4), recall=round(R, 4),
                f1=round(2 * P * R / max(1e-9, P + R), 4), accuracy=round((tp + tn) / max(1, tp + fp + fn + tn), 4), a=a, b=b)


def main():
    runs = sys.argv[1:] or ["bao_ho"]
    val, test = nhan_that("val"), nhan_that("test")
    print("nguoi co nhan: val %d, test %d" % (len(val), len(test)))
    ket = {}
    for run in runs:
        du_doan(run, "val", val)
        du_doan(run, "test", test)
        ket[run] = {}
        for tc in ("mu", "ao"):
            luoi = [cham(val, run, tc, a, b) for a, b in itertools.product((0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5), (0.0, 0.05, 0.1, 0.2, 0.3))]
            tot = max(luoi, key=lambda r: (r["f1"], r["accuracy"]))
            ket[run][tc] = dict(val=tot, test=cham(test, run, tc, tot["a"], tot["b"]),
                                chi_lop_yeu_val=cham(val, run, tc, 1.01, 0.25))   # chi dua vao lop "no_*" (doi chung)
            print(run, tc, "VAL", tot, "| TEST", ket[run][tc]["test"], "| chi lop no_* (val)", ket[run][tc]["chi_lop_yeu_val"])
    chon = max(runs, key=lambda r: ket[r]["mu"]["val"]["f1"] + ket[r]["ao"]["val"]["f1"])
    ra = dict(chon_mo_hinh=chon, chon_theo="tong F1 (thieu mu + thieu ao) tren VAL", ket_qua=ket,
              cach_danh_gia="muc tung nguoi (khung Person nhan that), luat 'thieu neu diem co < a va diem thieu >= b'",
              vung_xet=MO_TA_VUNG, khung_nguoi=MO_TA_KHUNG_NGUOI)
    ghi_json(os.path.join(GOC, "runs", "danh_gia", "bao_ho_muc_nguoi.json"), ra)
    print("CHON:", chon, json.dumps({k: ket[chon][k]["test"] for k in ("mu", "ao")}, ensure_ascii=False))


def _lech_may_that():
    """doi chieu vung() voi may canh bao THAT (an_toan_cang._phieu_bao_ho) tren luoi vi tri day, 4 lop, toa do pixel va
    chuan hoa -> so diem lech (None neu khong nap duoc ham may that). Ai sua vung o 1 ben ma quen ben kia -> lech > 0."""
    import types
    try:
        import an_toan_cang as atc
        phieu = atc.PhienAnToan._phieu_bao_ho
    except (ImportError, AttributeError) as e:
        print("  LOI khong nap duoc an_toan_cang.PhienAnToan._phieu_bao_ho de doi chieu: %s" % e)
        return None
    luat = atc.LUAT_BAO_HO
    atc.LUAT_BAO_HO = {"mu": (0.5, 0.5), "ao": (0.5, 0.5)}    # 1 hop conf 0,9: duoc tinh <=> co phieu
    lech = 0
    try:
        for p in ((300.0, 100.0, 380.0, 300.0), (0.41, 0.22, 0.47, 0.61)):
            w, h = p[2] - p[0], p[3] - p[1]
            for lop in VUNG_LOP:
                for i in range(-30, 121):
                    for j in range(-30, 131, 2):
                        cx, cy = p[0] + w * j / 100.0, p[1] + h * i / 100.0
                        hop = np.array([[cx - 1e-3 * w, cy - 1e-3 * h, cx + 1e-3 * w, cy + 1e-3 * h, 0.9, lop]])
                        v = types.SimpleNamespace(hop=p, mu=[], ao=[])
                        phieu(v, hop)
                        that = bool(v.mu if lop in (MU, KHONG_MU) else v.ao)
                        tam = ((hop[0, 0] + hop[0, 2]) / 2, (hop[0, 1] + hop[0, 3]) / 2)     # tam tinh nhu may that
                        lech += vung(p, tam, lop) != that
    except Exception as e:                       # ham may that doi chu ky / kieu du lieu: bao LOI ro, khong traceback
        print("  LOI doi chieu voi may canh bao that that bai: %s: %s" % (type(e).__name__, e))
        return None
    finally:
        atc.LUAT_BAO_HO = luat
    return lech


def tu_kiem():
    """kiem nhanh vung() (khong can du lieu / GPU) — ca loi cu: ao / khong ao tam o 0,15..0,4 bi bo; doi chieu may that"""
    p = (0.0, 0.0, 100.0, 200.0)                          # nguoi rong 100, cao 200
    lech = _lech_may_that()
    ca = [("khong ao tam 0,33 chieu cao (nguoi dung thang) -> tinh cho ao", vung(p, (50, 66), KHONG_AO), True),
          ("ao tam 0,25 -> tinh", vung(p, (50, 50), AO), True),
          ("ao tam 0,15 / 0,85 (bien) -> tinh", vung(p, (50, 30), AO) and vung(p, (50, 170), AO), True),
          ("ao tam 0,10 / 0,90 -> bo", vung(p, (50, 20), AO) or vung(p, (50, 180), AO), False),
          ("mu tam -0,05 / 0,05 / 0,40 -> tinh", vung(p, (50, -10), MU) and vung(p, (50, 10), MU) and vung(p, (50, 80), MU), True),
          ("dau tran tam 0,33 -> tinh cho mu", vung(p, (50, 66), KHONG_MU), True),
          ("mu tam 0,50 / -0,15 -> bo", vung(p, (50, 100), MU) or vung(p, (50, -30), MU), False),
          ("x lech 5% be rong -> tinh, 15% -> bo", vung(p, (-5, 50), AO) and not vung(p, (115, 50), AO), True),
          ("lop nguoi / lop khac -> khong xet", vung(p, (50, 50), NGUOI) or vung(p, (50, 50), 1), False),
          ("khop may canh bao that an_toan_cang._phieu_bao_ho (~98 nghin vi tri x lop, lech %s)" % lech, lech == 0, True)]
    loi = 0
    for ten, that, mong in ca:
        ok = bool(that) == mong
        loi += not ok
        print(("  OK  " if ok else "  LOI ") + ten)
    print("KET QUA: %s %d/%d" % ("DAT" if loi == 0 else "%d LOI /" % loi, len(ca) - loi, len(ca)))
    return 0 if loi == 0 else 1


if __name__ == "__main__":
    if "--tu-kiem" in sys.argv[1:]:
        sys.exit(tu_kiem())
    main()
