# -*- coding: utf-8 -*-
"""Gop du lieu thanh bo YOLO 1 lop (qr).
  train/val: the mo phong + Mendeley (nhan zxing) + Huawei that + warp2014 (mat na)
  test_that: anh THAT giu rieng (20% Mendeley that + 20% Huawei that, chia theo ten co dinh) — KHONG vao train/val.
Anh lon thu ve canh dai 1600 de do ton dia; toa do YOLO la chuan hoa nen khong doi."""
import json, random, shutil, sys
from pathlib import Path
import cv2
import numpy as np

B = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd")
RA = B / "yolo_qr"
for t in ("train", "val", "test_that"):
    (RA / "images" / t).mkdir(parents=True, exist_ok=True)
    (RA / "labels" / t).mkdir(parents=True, exist_ok=True)


def ghi(anh_duong, tap, ten, hop, W, H):
    """hop: list (x0,y0,x1,y1) px anh goc"""
    img = cv2.imread(str(anh_duong))
    if img is None:
        return False
    h, w = img.shape[:2]
    if max(h, w) > 1600:
        tl = 1600 / max(h, w)
        img = cv2.resize(img, (int(w * tl), int(h * tl)), interpolation=cv2.INTER_AREA)
    cv2.imwrite(str(RA / "images" / tap / (ten + ".jpg")), img, [cv2.IMWRITE_JPEG_QUALITY, 90])
    with open(RA / "labels" / tap / (ten + ".txt"), "w", encoding="utf-8") as f:
        for x0, y0, x1, y1 in hop:
            x0, x1 = max(0, x0), min(W, x1)
            y0, y1 = max(0, y0), min(H, y1)
            if x1 - x0 < 4 or y1 - y0 < 4:
                continue
            f.write(f"0 {(x0 + x1) / 2 / W:.6f} {(y0 + y1) / 2 / H:.6f} {(x1 - x0) / W:.6f} {(y1 - y0) / H:.6f}\n")
    return True


def hop_tu_mat_na(f, nguong=0.002):
    m = cv2.imread(str(f), 0)
    if m is None:
        return None, 0, 0
    H, W = m.shape
    n, nhan, tk, _ = cv2.connectedComponentsWithStats((m > 127).astype(np.uint8), 8)
    return [(int(x), int(y), int(x + w), int(y + h)) for x, y, w, h, a in tk[1:] if a > nguong * W * H], W, H


r = random.Random(4242)
dem = dict(train=0, val=0, test_that=0)

# 1. Anh the mo phong (da o dinh dang YOLO)
g = B / "the_mo_phong_train"
for t in ("train", "val"):
    for f in (g / "images" / t).glob("*.jpg"):
        shutil.copy(f, RA / "images" / t / f.name)
        shutil.copy(g / "labels" / t / (f.stem + ".txt"), RA / "labels" / t / (f.stem + ".txt"))
        dem[t] += 1
print("the mo phong", dem, flush=True)

# 2. Mendeley: anh that (flat, random) + tong hop (synthetic_small); chi anh zxing doc duoc
g = B / "mendeley_m6mfwc52vk" / "giai_nen"
nhan = json.load(open(B / "mendeley_m6mfwc52vk" / "nhan_zxing.json"))
for k, v in nhan.items():
    if not v["qr"]:
        continue
    hop = []
    for q in v["qr"]:
        a = np.array(q, float)
        hop.append((a[:, 0].min(), a[:, 1].min(), a[:, 0].max(), a[:, 1].max()))
    that = not k.startswith("synthetic")
    x = r.random()
    tap = "test_that" if that and x < .2 else ("val" if x < .3 else "train")
    if ghi(g / k, tap, "md_" + k.replace("/", "_").rsplit(".", 1)[0], hop, v["W"], v["H"]):
        dem[tap] += 1
print("mendeley", dem, flush=True)

# 3. Huawei that (mat na goc)
g = B / "bodnar_syn10k" / "huawei"
for f in sorted((g / "huawei").glob("*.jpg")):
    hop, W, H = hop_tu_mat_na(g / "huawei_gt" / (f.stem + ".png"))
    if not hop:
        continue
    x = r.random()
    tap = "test_that" if x < .2 else ("val" if x < .3 else "train")
    if ghi(f, tap, "hw_" + f.stem, hop, W, H):
        dem[tap] += 1
print("huawei", dem, flush=True)

# 4. warp2014 tong hop (mat na goc)
g = B / "bodnar_syn10k" / "warp"
for f in sorted((g / "warp2014_jpg").glob("*.jpg")):
    hop, W, H = hop_tu_mat_na(g / "warp2014_gt" / (f.stem + ".png"))
    if not hop:
        continue
    tap = "val" if r.random() < .1 else "train"
    if ghi(f, tap, "wp_" + f.stem[:16], hop, W, H):
        dem[tap] += 1
print("tong", dem, flush=True)
(RA / "qr.yaml").write_text(f"path: {RA.as_posix()}\ntrain: images/train\nval: images/val\ntest: images/test_that\nnames:\n  0: qr\n", encoding="utf-8")
