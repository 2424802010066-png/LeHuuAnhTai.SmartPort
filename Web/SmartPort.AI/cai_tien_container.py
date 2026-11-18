# -*- coding: utf-8 -*-
"""
CAI TIEN DOC MA CONTAINER TREN ANH TOAN CANH — chinh tham so tren tap VAL, do tap TEST 1 lan.
  1. Phat hien vung ma (YOLO26 yolo_cont, nguong 0.05) cho moi anh val/test -> luu bo dem
  2. Doc moi vung bang CRNN (3 muc le) theo CA 2 cach xoay (cu: theo lop / moi: theo ti le khung) -> luu bo dem
  3. So sanh: BO PHIEU cu (nguong 0.25, xoay cu) vs GOP CHUNG CU moi (nhieu bo tham so)
Chay: python cai_tien_container.py [chuan_bi] [chinh] [test]
"""
import collections
import json
import math
import os
import pickle
import sys
import time

import numpy as np
from PIL import Image

import doc_ma_container as dmc
from danh_gia_bien_so import PhatHien

GOC = os.path.dirname(os.path.abspath(__file__))
NGUON = r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat"
THU_ANH = os.path.join(NGUON, "container_lbf4616", "train_images", "images")
BO_DEM = os.path.join(GOC, "runs", "danh_gia", "bo_dem_container_toan.pkl")
RA = os.path.join(GOC, "runs", "danh_gia")


def tap_val():
    """Anh VAL: dap an = ma day du tu nhan (ma_ngang/ma_doc, hoac so_huu + seri 7 so). Bo anh co 2 ma khac nhau."""
    v = json.load(open(os.path.join(GOC, "du_lieu", "cont_ocr", "vung_ma.json"), encoding="utf-8"))
    theo = collections.defaultdict(list)
    for x in v:
        if x["split"] == "val":
            theo[x["anh"]].append(x)
    ra = []
    for anh, ds in sorted(theo.items()):
        ma, nhom = set(), None
        for x in ds:
            if x["lop"] in ("ma_ngang", "ma_doc"):
                ma.add(x["chuoi"])
                nhom = nhom or ("doc" if x["lop"] == "ma_doc" else "ngang")
        sh = [x["chuoi"] for x in ds if x["lop"] == "so_huu"]
        se = [x["chuoi"] for x in ds if x["lop"] == "seri" and len(x["chuoi"]) == 7]
        for a in sh:
            for b in se:
                if dmc.dung_6346(a + b):
                    ma.add(a + b)
                    nhom = nhom or "ngang_2_dong"
        ma = {m for m in ma if dmc.dung_6346(m)}
        if len(ma) == 1:
            ra.append(dict(id=os.path.basename(anh), nhan=ma.pop(), nhom=nhom))
    return ra


def tap_test():
    return [dict(id=t["id"], nhan=t["nhan"], nhom=t["nhom"])
            for t in json.load(open(os.path.join(NGUON, "danh_sach_container.json"), encoding="utf-8"))]


def chu_tu_nhan(splits):
    v = json.load(open(os.path.join(GOC, "du_lieu", "cont_ocr", "vung_ma.json"), encoding="utf-8"))
    ra = set()
    for x in v:
        if x["split"] in splits and x["lop"] in ("ma_ngang", "ma_doc", "so_huu") and len(x["chuoi"]) >= 4:
            if x["chuoi"][3] in "UJZ" and x["chuoi"][:4].isalpha():
                ra.add(x["chuoi"][:4])
    return ra


def chuan_bi():
    """Phat hien + doc moi vung 1 lan (ca 2 cach xoay), luu bo dem"""
    m = dmc.tai_mo_hinh()
    ph = PhatHien("yolo_cont", imgsz=1024, nguong=0.05)
    bd = {}
    for ten_tap, tap in (("val", tap_val()), ("test", tap_test())):
        t0 = time.time()
        for t in tap:
            duong = os.path.join(THU_ANH, t["id"])
            img = Image.open(duong).convert("RGB")
            det = ph(img)
            vung = []
            for x1, y1, x2, y2, cf, c in det:
                d = dict(anh=duong, lop=dmc.LOP[int(c)], hop=[int(x1), int(y1), int(x2), int(y2)], conf=float(cf))
                if d["lop"] in ("ma_ngang", "ma_doc", "so_huu", "seri"):
                    dmc.XOAY_THEO_TI_LE[0] = False
                    d["doc_cu"] = [x.astype(np.float16) for x in dmc.doc_vung(m, d)]
                    dmc.XOAY_THEO_TI_LE[0] = True
                    d["doc_moi"] = [x.astype(np.float16) for x in dmc.doc_vung(m, d)]
                vung.append(d)
            bd[(ten_tap, t["id"])] = dict(vung=vung, kich_thuoc=img.size)
            dmc._CACHE.clear()
        print(ten_tap, len(tap), "anh, %.0f s" % (time.time() - t0), flush=True)
    pickle.dump(bd, open(BO_DEM, "wb"))


_NHO = {}     # ghi nho: views / ung vien / ket qua beam cua moi nguon (khong phu thuoc tham so -> tinh 1 lan)


def _nho(khoa, ham):
    if khoa not in _NHO:
        _NHO[khoa] = ham()
    return _NHO[khoa]


def nguon_tu_bo_dem(vung, khoa, nguong_det, kich_thuoc):
    ds = [d for d in vung if d["conf"] >= nguong_det]
    nguon = []
    for d in ds:
        if d["lop"] in ("ma_ngang", "ma_doc"):
            views = _nho(("v", id(d), khoa), lambda: [x.astype(np.float32) for x in d[khoa]])
            nguon.append((views, d["conf"], dmc.bi_cat_mep(d["hop"], kich_thuoc),
                          _nho(("u", id(d), khoa), lambda: dmc.ung_vien_nguon(views)), ("r", id(d), khoa)))
    for a in (d for d in ds if d["lop"] == "so_huu"):
        for b in (d for d in ds if d["lop"] == "seri"):
            views = _nho(("v", id(a), id(b), khoa),
                         lambda: [np.concatenate([x, y], 0).astype(np.float32) for x, y in zip(a[khoa], b[khoa])])
            nguon.append((views, min(a["conf"], b["conf"]),
                          dmc.bi_cat_mep(a["hop"], kich_thuoc) or dmc.bi_cat_mep(b["hop"], kich_thuoc),
                          _nho(("u", id(a), id(b), khoa), lambda: dmc.ung_vien_nguon(views)), ("r", id(a), id(b), khoa)))
    return nguon


def bo_phieu(nguon):
    """lap lai chon_ma_bo_phieu (cach cu) tren cac lan doc da luu"""
    uv, tl = [], None
    for views, _, _, _, khoa in nguon:
        for i, lp in enumerate(views):
            tl = tl or dmc.giai_tham_lam(lp)
            uv.append(_nho(khoa + (i,), lambda: dmc.giai_rang_buoc(lp)))
    hl = [u for u in uv if u[1]]
    if hl:
        dem = collections.Counter(u[0] for u in hl)
        tot = max(hl, key=lambda u: (dem[u[0]], u[2] if u[2] is not None else -1e9))
        return tot[0], True, tl or ""
    return tl or "", False, tl or ""


def cham(bd, ten_tap, tap, cau_hinh, chu_biet):
    kq = []
    for t in tap:
        b = bd[(ten_tap, t["id"])]
        if cau_hinh["cach"] == "bo_phieu":
            ma, hl, _ = bo_phieu(nguon_tu_bo_dem(b["vung"], cau_hinh["xoay"], cau_hinh["det"], None))
        else:
            ng = nguon_tu_bo_dem(b["vung"], cau_hinh["xoay"], cau_hinh["det"], b["kich_thuoc"] if cau_hinh["cat_mep"] else None)
            if not ng:
                ma, hl = "", False
            else:
                ma, hl, _ = dmc.quyet_dinh(ng, chu_biet, cau_hinh["thuong"], cau_hinh["nguong"])
        kq.append(dict(id=t["id"], nhom=t["nhom"], nhan=t["nhan"], ma=ma, hop_le=hl))
    n = len(kq)
    dung = sum(r["hop_le"] and r["ma"] == r["nhan"] for r in kq)
    sai = sum(r["hop_le"] and r["ma"] != r["nhan"] for r in kq)
    return dict(n=n, dung=dung, ti_le=round(100 * dung / n, 2), sai_qua_ISO=sai, tu_choi=n - dung - sai), kq


def chinh():
    """Quet tham so tren VAL (chu so huu da biet = chi tap TRAIN)"""
    bd = pickle.load(open(BO_DEM, "rb"))
    val = tap_val()
    chu = chu_tu_nhan({"train"})
    print("val:", len(val), "anh | chu so huu da biet (train):", len(chu), flush=True)
    co_so, _ = cham(bd, "val", val, dict(cach="bo_phieu", xoay="doc_cu", det=0.25), chu)
    print("CU (bo phieu):", co_so, flush=True)
    bang = []
    for xoay in ("doc_cu", "doc_moi"):
        for det in (0.25, 0.1):
            for thuong in (0.0, 1.0, 2.0, 3.0, 5.0):
                for nguong in (-1e9, -20.0, -12.0, -8.0):
                    for cat_mep in (False, True):
                        ch = dict(cach="gop", xoay=xoay, det=det, thuong=thuong, nguong=nguong, cat_mep=cat_mep)
                        r, _ = cham(bd, "val", val, ch, chu)
                        bang.append((ch, r))
    bang.sort(key=lambda x: (-(x[1]["dung"] - 2 * x[1]["sai_qua_ISO"]), x[1]["sai_qua_ISO"]))
    for ch, r in bang[:12]:
        print(r, ch, flush=True)
    json.dump(dict(co_so=co_so, bang=[dict(cau_hinh=c, ket_qua=r) for c, r in bang]),
              open(os.path.join(RA, "chinh_container_val.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def do_test(ch):
    """Do TEST 1 lan voi cau hinh da chon tren val (chu so huu da biet = TRAIN + VAL)"""
    bd = pickle.load(open(BO_DEM, "rb"))
    test = tap_test()
    chu = chu_tu_nhan({"train", "val"})
    cu, kq_cu = cham(bd, "test", test, dict(cach="bo_phieu", xoay="doc_cu", det=0.25), chu)
    moi, kq = cham(bd, "test", test, ch, chu)
    theo_nhom = {}
    for nh in sorted({r["nhom"] for r in kq}):
        ds = [r for r in kq if r["nhom"] == nh]
        theo_nhom[nh] = dict(n=len(ds), dung=sum(r["hop_le"] and r["ma"] == r["nhan"] for r in ds),
                             sai_qua_ISO=sum(r["hop_le"] and r["ma"] != r["nhan"] for r in ds))
    print("TEST cu :", cu, flush=True)
    print("TEST moi:", moi, theo_nhom, flush=True)
    for r in kq:
        if not (r["hop_le"] and r["ma"] == r["nhan"]):
            print("   ", r["id"], r["nhom"], "nhan", r["nhan"], "doc", r["ma"], "tin" if r["hop_le"] else "TU CHOI")
    json.dump(dict(cau_hinh=ch, cu=cu, moi=moi, theo_nhom=theo_nhom, chi_tiet=kq),
              open(os.path.join(RA, "container_toan_gop_chung_cu_test.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump(sorted(chu), open(dmc.TEP_CHU_SO_HUU, "w", encoding="utf-8"))


if __name__ == "__main__":
    a = sys.argv[1:] or ["chuan_bi", "chinh"]
    if "chuan_bi" in a:
        chuan_bi()
    if "chinh" in a:
        chinh()
    if "test" in a:
        do_test(json.loads(a[a.index("test") + 1]))
