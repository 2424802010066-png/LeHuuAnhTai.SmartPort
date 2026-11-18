# -*- coding: utf-8 -*-
"""Buoc B: tu anh THAT giu rieng tao bien the suy giam (thu nho ve p px/module + mo/nhieu) bang toan tu KHAC bo the mo phong.
Dinh dang meta.json giong bo test de dung lai danh_gia.py."""
import json, math, random
from pathlib import Path
import cv2
import numpy as np

B = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd")
ANH = B / "yolo_qr" / "images" / "test_that"
RA = B / "test_that_suy_giam"
(RA / "anh").mkdir(parents=True, exist_ok=True)
gt = json.loads((B / "test_that_gt.json").read_text(encoding="utf-8"))
r = random.Random(31337)
meta = []
i = 0
for ten, v in sorted(gt.items()):
    if not v.get("chuoi"):
        continue
    img = cv2.imread(str(ANH / ten))
    q = np.array(v["q"], np.float32)
    canh = max(np.linalg.norm(q[0] - q[1]), np.linalg.norm(q[1] - q[2]))
    n = 17 + 4 * int(v["ver"]) if str(v.get("ver", "")).isdigit() else 33
    for p in (2.0, 3.0, 4.0):
        tl = p * n / canh
        if tl > 1.0:  # anh goc khong du net cho muc nay (khong phong to gia)
            continue
        W, H = int(img.shape[1] * tl), int(img.shape[0] * tl)
        nho = cv2.resize(img, (W, H), interpolation=cv2.INTER_AREA)
        for kieu in ("sach", "mo_chuyen_dong", "mo_net", "jpeg_thap", "thieu_sang"):
            a = nho.astype(np.float32)
            if kieu == "mo_chuyen_dong":
                do = r.randint(3, 7)
                g = r.uniform(0, 180)
                # hat nhan hinh thang (rung tay), khac hat nhan duong thang cua bo the mo phong
                k = np.zeros((2 * do + 3, 2 * do + 3), np.float32)
                for t in np.linspace(-do, do, do * 10):
                    k[int(round(do + 1 + t * math.sin(math.radians(g)))), int(round(do + 1 + t * math.cos(math.radians(g))))] += 1.0 - .4 * abs(t) / do
                a = cv2.filter2D(a, -1, k / k.sum())
            elif kieu == "mo_net":
                # mo dia tron (ngoai net) thay vi Gauss
                R = r.randint(2, 4)
                k = np.zeros((2 * R + 1, 2 * R + 1), np.float32)
                cv2.circle(k, (R, R), R, 1.0, -1)
                a = cv2.filter2D(a, -1, k / k.sum())
            elif kieu == "thieu_sang":
                a = 255 * (a / 255) ** 2.2 * .4 + np.random.RandomState(i).normal(0, 6, a.shape)
            f = f"{i:05d}.jpg"
            cv2.imwrite(str(RA / "anh" / f), np.clip(a, 0, 255).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 30 if kieu == "jpeg_thap" else 88])
            meta.append(dict(tep=f, kieu=kieu, px=p, n=n, w=W, h=H, chuoi=v["chuoi"], jpeg=0, q=(q * tl).round(1).tolist()))
            i += 1
(RA / "meta.json").write_text(json.dumps(meta, ensure_ascii=False), encoding="utf-8")
print("bien the:", len(meta), "tu", sum(1 for v in gt.values() if v.get("chuoi")), "anh that")
