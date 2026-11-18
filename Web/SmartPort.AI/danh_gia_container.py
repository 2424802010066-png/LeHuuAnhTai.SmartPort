# -*- coding: utf-8 -*-
"""
CHAM DIEM MA CONTAINER tren 350 anh TEST giu rieng (SmartPort_DuLieuThat/danh_sach_container.json), cung cach tinh voi
lan chay thu cu (tinh_diem.py: "toan" = anh camera day du, "cat" = vung ma da biet):
  cat  : vung ma THAT (nhan) -> CRNN + giai ma rang buoc ISO 6346
  toan : anh camera 1920x1080 -> bo phat hien VUNG MA (5 lop) -> CRNN + ISO 6346 + GOP CHUNG CU giua cac vung
Chay: python danh_gia_container.py <yolo|rfdetr> [cat] [toan]
"""
import json
import os
import sys
import time

import numpy as np
from PIL import Image

import doc_ma_container as dmc
from danh_gia_bien_so import PhatHien, tom_tat

GOC = os.path.dirname(os.path.abspath(__file__))
RUNS = os.path.join(GOC, "runs")
NGUON = r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat"
THU_ANH = os.path.join(NGUON, "container_lbf4616", "train_images", "images")


def bang(kq):
    ra = {}
    for nh in ["TAT CA"] + sorted({r["nhom"] for r in kq}):
        ds = [r for r in kq if nh == "TAT CA" or r["nhom"] == nh]
        t = tom_tat([(r["nhan"], r["ma"] if r["hop_le"] else "", r["ms"]) for r in ds])
        t["tra_ma_qua_ISO_6346"] = sum(r["hop_le"] for r in ds)
        t["qua_ISO_nhung_sai"] = sum(r["hop_le"] and r["ma"] != r["nhan"] for r in ds)
        t["tu_choi_chuyen_kiem_tay"] = sum(not r["hop_le"] for r in ds)
        t["dung_neu_khong_rang_buoc"] = round(100 * sum(r["tham_lam"] == r["nhan"] for r in ds) / max(1, len(ds)), 1)
        ra[nh] = t
    return ra


def chay(khung, che_do, bai_cont=None):
    m = dmc.tai_mo_hinh()
    test = json.load(open(os.path.join(NGUON, "danh_sach_container.json"), encoding="utf-8"))
    if che_do == "cat":
        vung_that = {}
        for d in dmc.cat_that("test"):
            vung_that.setdefault(os.path.basename(d["anh"]), []).append(d)
    else:
        # nguong 0.1: vung tin cay thap van la nguon (trong so = conf) — chon tren tap VAL (cai_tien_container.py)
        ph = PhatHien(bai_cont or khung + "_cont", imgsz=1024, nguong=0.1)
    kq = []
    for i, t in enumerate(test):
        duong = os.path.join(THU_ANH, t["id"])
        t0 = time.perf_counter()
        if che_do == "cat":
            vung = vung_that.get(t["id"], [])
        else:
            img = Image.open(duong).convert("RGB")
            det = ph(img)
            vung = [dict(anh=duong, lop=dmc.LOP[int(c)], hop=[int(x1), int(y1), int(x2), int(y2)], conf=float(cf))
                    for x1, y1, x2, y2, cf, c in det]
        ma, hop_le, tl = dmc.chon_ma(m, vung)
        ms = (time.perf_counter() - t0) * 1000
        kq.append(dict(id=t["id"], nhom=t["nhom"], nhan=t["nhan"], ma=ma, hop_le=hop_le, tham_lam=tl,
                       so_vung=len(vung), lop_vung=sorted({v["lop"] for v in vung}), ms=round(ms, 1) if i > 2 else None))
    b = bang(kq)
    ten = "container_%s_%s.json" % (khung if che_do == "toan" else "crnn", che_do)
    os.makedirs(os.path.join(RUNS, "danh_gia"), exist_ok=True)
    json.dump({"bang": b, "chi_tiet": kq}, open(os.path.join(RUNS, "danh_gia", ten), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print("KET_QUA", ten, json.dumps(b, ensure_ascii=False), flush=True)
    return b


if __name__ == "__main__":
    a = sys.argv[1:]
    bc = a[a.index("--cont") + 1] if "--cont" in a else None
    for che_do in [c for c in ("cat", "toan") if c in a] or ["cat", "toan"]:
        chay(a[0], che_do, bc)
