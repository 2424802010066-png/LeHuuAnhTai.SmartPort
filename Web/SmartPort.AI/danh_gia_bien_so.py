# -*- coding: utf-8 -*-
"""
CHAM DIEM BIEN SO tren tap TEST giu rieng (du_lieu/test_bien_so_*.json), CUNG CACH TINH voi lan chay thu cu
(SmartPort_DuLieuThat/tinh_diem.py) de so sanh truoc/sau:
  cat : anh bien DA CAT    -> bo phat hien KY TU (36 lop) -> ghep 1 hoac 2 dong -> chuoi
  e2e : anh CAMERA day du  -> bo phat hien BIEN SO -> cat -> bo phat hien KY TU -> chuoi
  cu  : tinh lai diem he thong CU (trinh duyet, 01/10) tren DUNG tap test moi
Chay: python danh_gia_bien_so.py <yolo|rfdetr|cu> [cat] [e2e] [--tap val]
"""
import csv
import json
import os
import re
import statistics
import sys
import time

import numpy as np
from PIL import Image

GOC = os.path.dirname(os.path.abspath(__file__))
DL = os.path.join(GOC, "du_lieu")
RUNS = os.path.join(GOC, "runs")
NGUON = r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat"
THU_ANH = os.path.join(NGUON, "bien_so_vn", "yolo_plate_dataset", "yolo_plate_dataset")
THU_CAT = os.path.join(NGUON, "bien_so_vn", "yolo_plate_ocr_dataset", "yolo_plate_ocr_dataset")
KT = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"


# ------------------------------------------------------------------ cach tinh diem (giong tinh_diem.py)
def sach(s):
    return re.sub(r"[^A-Z0-9]", "", (s or "").upper())


def lev(a, b):
    d = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        p, d[0] = d[0], i
        for j, cb in enumerate(b, 1):
            p, d[j] = d[j], min(d[j] + 1, d[j - 1] + 1, p + (ca != cb))
    return d[len(b)]


def tom_tat(ds):
    """ds: [(nhan, doc, ms)] -> dict chi so"""
    n = len(ds)
    if not n:
        return {}
    dung = sum(sach(d) == nh for nh, d, _ in ds)
    sai1 = sum(lev(sach(d), nh) == 1 for nh, d, _ in ds)
    ky_tu = sum(len(nh) for nh, _, _ in ds)
    loi = sum(min(lev(sach(d), nh), len(nh)) for nh, d, _ in ds)
    ms = [m for _, _, m in ds if m is not None]
    return dict(n=n, dung=dung, ti_le_dung=round(100 * dung / n, 1), sai_1_ky_tu=sai1,
                ti_le_dung_hoac_sai_1=round(100 * (dung + sai1) / n, 1),
                do_chinh_xac_ky_tu=round(100 * (1 - loi / ky_tu), 1), khong_doc_duoc=sum(not sach(d) for _, d, _ in ds),
                ms_trung_vi=round(statistics.median(ms)) if ms else None,
                ms_p90=round(sorted(ms)[int(0.9 * (len(ms) - 1))]) if ms else None)


def bang_diem(kq, khoa="doc"):
    """kq: list dict(nhom, nhan, <khoa>, ms) -> {TAT CA, theo nhom}"""
    return {nh: tom_tat([(r["nhan"], r.get(khoa), r.get("ms")) for r in kq if nh == "TAT CA" or r["nhom"] == nh])
            for nh in ["TAT CA"] + sorted({r["nhom"] for r in kq})}


# ------------------------------------------------------------------ tap du lieu
def goc_khung(ten):
    return re.sub(r"^\d+", "", ten)


def lay_tap(che_do, tap):
    """tra list dict(id, duong, nhan, nhom)"""
    if tap == "test":
        ds = json.load(open(os.path.join(DL, "test_bien_so_%s.json" % che_do), encoding="utf-8"))
        thu = THU_CAT if che_do == "cat" else THU_ANH
        return [dict(id=d["id"], duong=os.path.join(thu, d["id"]), nhan=d["nhan"], nhom=d["nhom"]) for d in ds]
    # tap VAL (de chinh tham so, KHONG dung test): lay tu phan val cua bo du lieu huan luyen
    nhan = [x for x in csv.DictReader(open(os.path.join(NGUON, "nhan_bien_so.csv"), encoding="utf-8"))
            if x["dang_VN"] == "True"]
    if che_do == "cat":
        theo_ten = {x["file"]: x for x in nhan}
        ds = []
        for a in open(os.path.join(DL, "ky_tu", "yolo", "val.txt"), encoding="utf-8").read().split("\n"):
            x = theo_ten.get(os.path.basename(a))
            if a and x:
                ds.append(dict(id=x["file"], duong=a, nhan=x["bien_so"],
                               nhom="oto_1_dong" if x["so_dong"] == "1" else "xe_may_2_dong"))
        return ds
    theo_goc = {}
    for x in nhan:
        theo_goc.setdefault(goc_khung(x["file"]), set()).add((x["bien_so"], x["so_dong"]))
    ds = []
    for a in open(os.path.join(DL, "bien_det", "yolo", "val.txt"), encoding="utf-8").read().split("\n"):
        v = theo_goc.get(os.path.basename(a))
        if a and v and len(v) == 1:
            bs, dong = next(iter(v))
            ds.append(dict(id=os.path.basename(a), duong=a, nhan=bs, nhom="oto_1_dong" if dong == "1" else "xe_may_2_dong"))
    return ds


# ------------------------------------------------------------------ mo hinh
class PhatHien:
    """bo phat hien chung cho YOLO26 (ultralytics) va RF-DETR; goi(img PIL) -> mang N x 6 (x1,y1,x2,y2,conf,lop)"""

    def __init__(self, bai, imgsz=640, nguong=0.25):
        khung = "yolo" if bai.startswith("yolo") else "rfdetr"
        self.khung, self.imgsz, self.nguong = khung, imgsz, nguong
        if khung == "yolo":
            from ultralytics import YOLO
            self.m = YOLO(os.path.join(RUNS, bai, "weights", "best.pt"))
        else:
            import rfdetr
            cfg = json.load(open(os.path.join(RUNS, bai, "training_config.json")))["model_config"]
            self.m = getattr(rfdetr, cfg["model_name"])(pretrain_weights=os.path.join(RUNS, bai, "checkpoint_best_total.pth"),
                                                        num_classes=cfg["num_classes"])
            try:
                self.m.inference(compile=False)          # che do suy luan (float32, giu nguyen do chinh xac)
            except Exception as e:
                print("bo qua inference():", str(e)[:120])

    def __call__(self, img):
        if self.khung == "yolo":
            r = self.m.predict(img, imgsz=self.imgsz, conf=self.nguong, verbose=False, device=0)[0]
            b = r.boxes
            return np.c_[b.xyxy.cpu().numpy(), b.conf.cpu().numpy(), b.cls.cpu().numpy()]
        d = self.m.predict(img, threshold=self.nguong, include_source_image=False)
        return np.c_[d.xyxy, d.confidence, d.class_id] if len(d) else np.zeros((0, 6))


def iou(a, b):
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    giao = max(0, x2 - x1) * max(0, y2 - y1)
    return giao / max(1e-6, (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - giao)


def ghep_ky_tu(det):
    """khung ky tu -> chuoi: NMS khong phan lop, tach 1/2 dong theo khoang trong lon nhat giua tam y, sap trai->phai"""
    giu = []
    for d in det[np.argsort(-det[:, 4])] if len(det) else []:
        if all(iou(d, k) < 0.6 for k in giu):
            giu.append(d)
    if not giu:
        return "", 0.0
    k = np.array(giu)
    cy = (k[:, 1] + k[:, 3]) / 2
    h = float(np.median(k[:, 3] - k[:, 1]))
    ys = np.sort(cy)
    hang = [k]
    if len(ys) > 1 and np.diff(ys).max() > 0.5 * h:
        cat = ys[np.diff(ys).argmax()] + np.diff(ys).max() / 2
        hang = [k[cy < cat], k[cy >= cat]]
    s = "".join(KT[int(c)] for r in hang for c in r[np.argsort((r[:, 0] + r[:, 2]) / 2)][:, 5])
    return s, float(k[:, 4].min())


# vi tri 1-2 la SO (ma tinh), vi tri 3 la CHU (seri), tu vi tri 5 la SO; vi tri 4 chu hoac so
SO = dict(O="0", D="0", Q="0", I="1", Z="2", S="5", G="6", B="8", A="4", T="7")
CHU = {"0": "D", "8": "B", "5": "S", "2": "Z", "6": "G", "4": "A"}


def sua_dinh_dang_vn(s):
    if not 7 <= len(s) <= 9:
        return s
    c = list(s)
    for i in range(len(c)):
        if i in (0, 1) or i >= 4:
            c[i] = SO.get(c[i], c[i])
        elif i == 2:
            c[i] = CHU.get(c[i], c[i])
    return "".join(c)


def doc_bien(ky_tu, img):
    t = time.perf_counter()
    raw, conf = ghep_ky_tu(ky_tu(img))
    return raw, sua_dinh_dang_vn(raw), conf, (time.perf_counter() - t) * 1000


def cat_bien(img, hop, le):
    x1, y1, x2, y2 = hop[:4]
    w, h = x2 - x1, y2 - y1
    return img.crop((max(0, x1 - w * le), max(0, y1 - h * le), min(img.width, x2 + w * le), min(img.height, y2 + h * le)))


# ------------------------------------------------------------------ chay
def chay(khung, che_do, tap, le=0.1, bai_kytu=None, bai_bien=None):
    ky_tu = PhatHien(bai_kytu or khung + "_kytu", imgsz=320, nguong=0.3)
    bien = PhatHien(bai_bien or khung + "_bien", imgsz=640, nguong=0.25) if che_do == "e2e" else None
    ds = lay_tap(che_do, tap)
    kq = []
    for i, d in enumerate(ds):
        img = Image.open(d["duong"]).convert("RGB")
        t0 = time.perf_counter()
        if che_do == "cat":
            raw, doc, conf, _ = doc_bien(ky_tu, img)
            so_bien = 1
        else:
            hb = bien(img)
            so_bien = len(hb)
            raw = doc = ""
            conf = 0.0
            if len(hb):
                tot = hb[np.argmax(hb[:, 4])]
                raw, doc, conf, _ = doc_bien(ky_tu, cat_bien(img, tot, le))
        ms = (time.perf_counter() - t0) * 1000
        kq.append(dict(id=d["id"], nhom=d["nhom"], nhan=d["nhan"], doc=doc, raw=raw, conf=round(conf, 3),
                       so_bien=so_bien, ms=round(ms, 1)))
        if i == 2:                                          # bo 3 anh dau (khoi dong GPU) khoi thoi gian
            for r in kq:
                r["ms"] = None
    bang = {"sau_sua_dinh_dang": bang_diem(kq, "doc"), "chua_sua_dinh_dang": bang_diem(kq, "raw")}
    ten = "bien_so_%s_%s_%s.json" % (khung, che_do, tap)
    os.makedirs(os.path.join(RUNS, "danh_gia"), exist_ok=True)
    json.dump({"bang": bang, "chi_tiet": kq}, open(os.path.join(RUNS, "danh_gia", ten), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print("KET_QUA", ten, json.dumps(bang["sau_sua_dinh_dang"], ensure_ascii=False), flush=True)
    print("   (chua sua dinh dang)", json.dumps(bang["chua_sua_dinh_dang"]["TAT CA"], ensure_ascii=False), flush=True)
    return bang


def diem_cu():
    """he thong CU (trinh duyet, ket qua luu ngay 01/10) tinh lai tren DUNG tap test moi"""
    ra = {}
    for che_do, f in (("e2e", "ket_qua_bien_so_e2e.json"), ("cat", "ket_qua_bien_so_cct.json")):
        ids = {d["id"] for d in json.load(open(os.path.join(DL, "test_bien_so_%s.json" % che_do), encoding="utf-8"))}
        kq = [r for r in json.load(open(os.path.join(NGUON, f), encoding="utf-8"))["ketQua"] if r["id"] in ids]
        ra[che_do] = bang_diem(kq, "doc")
        print("CU", che_do, len(kq), "anh:", json.dumps(ra[che_do], ensure_ascii=False))
    os.makedirs(os.path.join(RUNS, "danh_gia"), exist_ok=True)
    json.dump(ra, open(os.path.join(RUNS, "danh_gia", "bien_so_he_thong_cu_tren_test_moi.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    return ra


if __name__ == "__main__":
    a = sys.argv[1:]
    if a[:1] == ["cu"]:
        diem_cu()
    else:
        tap = a[a.index("--tap") + 1] if "--tap" in a else "test"
        le = float(a[a.index("--le") + 1]) if "--le" in a else 0.1
        bk = a[a.index("--kytu") + 1] if "--kytu" in a else None
        bb = a[a.index("--bien") + 1] if "--bien" in a else None
        for che_do in [c for c in ("cat", "e2e") if c in a] or ["cat", "e2e"]:
            chay(a[0], che_do, tap, le, bk, bb)
