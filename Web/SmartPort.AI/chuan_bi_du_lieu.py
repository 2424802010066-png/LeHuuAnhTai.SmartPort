# -*- coding: utf-8 -*-
"""
CHUAN BI DU LIEU HUAN LUYEN cho SmartPort AI (RF-DETR + YOLO26) — KHONG chep anh (Windows Defender quet tung file
moi lam cham ~3 anh/giay): YOLO doc danh sach duong dan anh goc (*.txt), RF-DETR doc COCO voi file_name tuyet doi.
  data/bien_det  : tim BIEN SO tren anh camera (1 lop)                      <- yolo_plate_dataset (8.259 anh)
  data/ky_tu     : nhan dien TUNG KY TU tren anh bien da cat (36 lop 0-9 A-Z) <- yolo_plate_ocr_dataset
  data/cont_det  : tim VUNG MA CONTAINER tren anh camera cong (5 lop)        <- lbf4616 (3.004 anh)
  data/cont_ocr  : vung ma container + chuoi dap an (de huan luyen bo doc chuoi)
NGUYEN TAC CHONG "AN GIAN": bo kiem tra cu (1.000 anh camera bien so, 1.000 anh bien cat, 350 anh container) giu
NGUYEN lam tap TEST; moi anh cat cung KHUNG HINH goc voi anh test cung bi loai khoi tap huan luyen.
"""
import collections
import csv
import glob
import json
import os
import random
import re

from PIL import Image

NGUON = r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat"
RA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "du_lieu")
KT = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
random.seed(42)


def goc_khung(ten):
    """'10xemay112.jpg' -> 'xemay112.jpg' (anh cat mang so thu tu o dau, cung khung hinh goc)"""
    return re.sub(r"^\d+", "", ten)


def ghi_bo(ten_bo, mau, lop):
    """mau: dict split -> list of (duong_anh_tuyet_doi, [(lop, x1, y1, x2, y2) pixel], (w, h))."""
    goc = os.path.join(RA, ten_bo)
    for split, ds in mau.items():
        os.makedirs(os.path.join(goc, "yolo"), exist_ok=True)
        with open(os.path.join(goc, "yolo", split + ".txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(a for a, _, _ in ds) + "\n")
        thu_muc_coco = os.path.join(goc, "coco", "valid" if split == "val" else split)
        os.makedirs(thu_muc_coco, exist_ok=True)
        coco = dict(images=[], annotations=[], categories=[dict(id=i, name=n, supercategory="none")
                                                          for i, n in enumerate(lop)])
        for i, (anh, hop, (w, h)) in enumerate(ds):
            coco["images"].append(dict(id=i, file_name=anh, width=w, height=h))
            for c, x1, y1, x2, y2 in hop:
                coco["annotations"].append(dict(id=len(coco["annotations"]), image_id=i, category_id=c,
                                                bbox=[x1, y1, x2 - x1, y2 - y1], area=(x2 - x1) * (y2 - y1), iscrowd=0))
        json.dump(coco, open(os.path.join(thu_muc_coco, "_annotations.coco.json"), "w"))
    with open(os.path.join(goc, "yolo", "data.yaml"), "w", encoding="utf-8") as f:
        f.write("path: %s\ntrain: train.txt\nval: val.txt\ntest: test.txt\nnames:\n" %
                os.path.join(goc, "yolo").replace("\\", "/"))
        for i, n in enumerate(lop):
            f.write("  %d: '%s'\n" % (i, n))
    print("%-9s" % ten_bo, {k: len(v) for k, v in mau.items()}, "| so khung:",
          {k: sum(len(h) for _, h, _ in v) for k, v in mau.items()}, flush=True)


def doc_yolo(txt, w, h):
    hop = []
    if os.path.exists(txt):
        for l in open(txt, encoding="utf-8"):
            p = l.split()
            if len(p) >= 5:
                c, cx, cy, bw, bh = int(p[0]), *map(float, p[1:5])
                hop.append((c, max(0.0, (cx - bw / 2) * w), max(0.0, (cy - bh / 2) * h),
                            min(w, (cx + bw / 2) * w), min(h, (cy + bh / 2) * h)))
    return hop


def chia(ds, test_ids):
    test = [d for d in ds if os.path.basename(d[0]) in test_ids]
    con = [d for d in ds if os.path.basename(d[0]) not in test_ids]
    random.shuffle(con)
    n_val = max(1, len(con) // 10)
    return dict(train=con[n_val:], val=con[:n_val], test=test)


def tap_test_bien_so():
    """Bo test cu chua TOAN BO anh o to bien 1 dong (CarLong) -> neu loai het khoi tap hoc thi bo doc khong con
    bien VN 1 dong nao de hoc. Chia doi co dinh (seed 2026): 50% anh o to giu lam TEST, 50% dua vao huan luyen.
    Anh xe may giu nguyen. Chi so he thong CU duoc tinh lai tren DUNG tap test moi nay (so_sanh_cu_moi.py)."""
    e2e = json.load(open(os.path.join(NGUON, "danh_sach_bien_so_e2e.json"), encoding="utf-8"))
    cat = json.load(open(os.path.join(NGUON, "danh_sach_bien_so_cat.json"), encoding="utf-8"))
    oto = sorted(d["id"] for d in e2e if d["nhom"] == "oto_1_dong")
    random.Random(2026).shuffle(oto)
    giu = set(oto[:len(oto) // 2])
    e2e_moi = [d for d in e2e if d["nhom"] != "oto_1_dong" or d["id"] in giu]
    cat_moi = [d for d in cat if d["nhom"] != "oto_1_dong" or goc_khung(d["id"]) in giu]
    os.makedirs(RA, exist_ok=True)
    json.dump(e2e_moi, open(os.path.join(RA, "test_bien_so_e2e.json"), "w", encoding="utf-8"), ensure_ascii=False)
    json.dump(cat_moi, open(os.path.join(RA, "test_bien_so_cat.json"), "w", encoding="utf-8"), ensure_ascii=False)
    print("test bien so moi: e2e", len(e2e_moi), "(o to %d)" % sum(d["nhom"] == "oto_1_dong" for d in e2e_moi),
          "| anh cat", len(cat_moi), "(o to %d)" % sum(d["nhom"] == "oto_1_dong" for d in cat_moi), flush=True)
    return {d["id"] for d in e2e_moi}, {d["id"] for d in cat_moi}


def bo_bien_det(test_ids):
    thu_muc = os.path.join(NGUON, "bien_so_vn", "yolo_plate_dataset", "yolo_plate_dataset")
    ds = []
    for anh in sorted(glob.glob(os.path.join(thu_muc, "*.jpg"))):
        wh = Image.open(anh).size
        ds.append((anh, [(0, *b[1:]) for b in doc_yolo(anh[:-4] + ".txt", *wh)], wh))
    ghi_bo("bien_det", chia(ds, test_ids), ["bien_so"])


def bo_ky_tu(test_e2e, test_cat):
    thu_muc = os.path.join(NGUON, "bien_so_vn", "yolo_plate_ocr_dataset", "yolo_plate_ocr_dataset")
    nhan = {r["file"]: r for r in csv.DictReader(open(os.path.join(NGUON, "nhan_bien_so.csv"), encoding="utf-8"))}
    ds, cam = [], set()
    for anh in sorted(glob.glob(os.path.join(thu_muc, "*.jpg"))):
        ten = os.path.basename(anh)
        if ten == "0.jpg":                       # bang chu mau, khong phai bien so
            continue
        wh = Image.open(anh).size
        hop = doc_yolo(anh[:-4] + ".txt", *wh)
        if not hop:
            continue
        if ten in test_cat or goc_khung(ten) in test_e2e:
            cam.add(ten)                         # cung khung hinh voi anh test -> KHONG duoc hoc
        ds.append((anh, hop, wh))
    mau = chia(ds, cam)
    mau["test"] = [d for d in mau["test"] if os.path.basename(d[0]) in test_cat]   # test = dung 1.000 anh cu
    ghi_bo("ky_tu", mau, list(KT))
    with open(os.path.join(RA, "ky_tu", "nhan_test.json"), "w", encoding="utf-8") as f:
        json.dump({os.path.basename(a): nhan.get(os.path.basename(a), {}).get("bien_so") for a, _, _ in mau["test"]}, f)


LOP_CONT = ["ma_ngang", "ma_doc", "so_huu", "seri", "ma_loai"]


def bo_container():
    thu_anh = os.path.join(NGUON, "container_lbf4616", "train_images", "images")
    thu_nhan = os.path.join(NGUON, "container_lbf4616", "train_images_label", "images_label")
    thu_yolo = os.path.join(NGUON, "container_lbf4616", "train_images", "labels")   # YOLO tim nhan o .../labels/
    os.makedirs(thu_yolo, exist_ok=True)
    test_ids = {d["id"] for d in json.load(open(os.path.join(NGUON, "danh_sach_container.json"), encoding="utf-8"))}
    ds, cat, thong_ke = [], [], collections.Counter()
    for f in sorted(glob.glob(os.path.join(thu_nhan, "*.txt"))):
        anh = os.path.join(thu_anh, os.path.basename(f)[:-4] + ".jpg")
        if not os.path.exists(anh):
            continue
        w, h = Image.open(anh).size
        hop = []
        for l in open(f, encoding="utf-8", errors="replace"):
            p = l.strip().split(",")
            if len(p) < 5 or not all(v.strip().lstrip("-").isdigit() for v in p[:4]):
                continue
            x1, y1, x2, y2 = map(int, p[:4])
            x1, x2 = sorted((max(0, x1), min(w, x2)))
            y1, y2 = sorted((max(0, y1), min(h, y2)))
            t = ",".join(p[4:]).strip().upper().replace(" ", "")
            if x2 - x1 < 4 or y2 - y1 < 4:
                continue
            if re.fullmatch(r"[A-Z]{4}\d{7}", t):
                c = 0 if (x2 - x1) > (y2 - y1) else 1
            elif re.fullmatch(r"[A-Z]{4}", t):
                c = 2
            elif re.fullmatch(r"\d{6,7}", t):
                c = 3
            elif re.fullmatch(r"\d{2}[A-Z0-9]{2}", t):
                c = 4
            else:
                thong_ke["khac:" + t[:6]] += 1
                continue
            thong_ke[LOP_CONT[c]] += 1
            hop.append((c, x1, y1, x2, y2))
            cat.append(dict(anh=anh, lop=LOP_CONT[c], hop=[x1, y1, x2, y2], chuoi=t))
        with open(os.path.join(thu_yolo, os.path.basename(f)), "w") as fo:
            for c, x1, y1, x2, y2 in hop:
                fo.write("%d %.6f %.6f %.6f %.6f\n" % (c, (x1 + x2) / 2 / w, (y1 + y2) / 2 / h, (x2 - x1) / w,
                                                       (y2 - y1) / h))
        ds.append((anh, hop, (w, h)))
    mau = chia(ds, test_ids)
    ghi_bo("cont_det", mau, LOP_CONT)
    tach = {a: k for k, v in mau.items() for a, _, _ in v}
    for c in cat:
        c["split"] = tach.get(c["anh"], "train")
    os.makedirs(os.path.join(RA, "cont_ocr"), exist_ok=True)
    json.dump(cat, open(os.path.join(RA, "cont_ocr", "vung_ma.json"), "w", encoding="utf-8"), ensure_ascii=False)
    print("vung ma container:", dict(thong_ke.most_common(12)), flush=True)


if __name__ == "__main__":
    import sys
    if "container" not in sys.argv[1:]:          # "python chuan_bi_du_lieu.py bien" -> chi dung lai bo bien so
        t_e2e, t_cat = tap_test_bien_so()
        bo_bien_det(t_e2e)
        bo_ky_tu(t_e2e, t_cat)
    if "bien" not in sys.argv[1:]:
        bo_container()
