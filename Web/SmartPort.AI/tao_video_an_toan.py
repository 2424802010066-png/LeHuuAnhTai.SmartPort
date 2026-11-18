# -*- coding: utf-8 -*-
"""
VIDEO KIEM THU CANH BAO NGUY HIEM co DAP AN, dung tu ANH THAT:
  - cong nhan: cat tu tap TEST cua Construction-PPE (1 nguoi DU mu + ao phan quang, 1 nguoi DAU TRAN) — khong dung anh train
  - xe tai: cat tu anh nguoi dung gui (11.png); o to NHIN NGANG: ngoaigiao7.jpg (bo bien so VN)
Dat len mat bai theo DUNG phoi canh camera lo kim (cao 6 m, f = 900 px, chan troi y = 150): cao anh = f.h/Z.
Kich ban 12 s @ 10 khung/giay:
  A (du bao ho) dung yen X=0, Z=14  <-  XE TAI lao toi 6 m/s tu Z=62, phanh dung o Z=16,6 luc t=7,5 s (sat nguoi)
  B (dau tran) di bo phia TRAI, gan camera (Z=12, cao 127 px — du lon de thay dau / mu) tu X=-9 sang X=-4 (0,8 m/s)
    -> vao VUNG CAM ben trai (x 0.15..0.30, y 0.75..0.92) luc t ~ 3,8 s, dung lai trong vung
  O TO chay ngang 30 km/h o Z=28 (qua toc do: gioi han bai 15 km/h), di sau xe tai / cong nhan A
Ghi: video_an_toan.webm + dap_an_an_toan.json (moc thoi gian ky vong).
"""
import glob
import json
import os
import sys

import cv2
import numpy as np

GOC = os.path.dirname(os.path.abspath(__file__))
DU_LIEU = os.environ.get("SMARTPORT_DULIEU_DIR", os.path.join(os.path.expanduser("~"), "Downloads", "SmartPort_DuLieuThat"))
PPE = os.path.join(DU_LIEU, "construction_ppe")
RA = sys.argv[2] if len(sys.argv) > 2 else os.path.join(DU_LIEU, "video_kiem_thu")
ANH_TAI = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RA, "nguon")      # chua 11.png (anh xe tai nguoi dung gui)
YOLO_PT = os.path.join(GOC, "yolo26s.pt")
os.makedirs(RA, exist_ok=True)
W, H, FPS, Y0, F, HC = 1280, 720, 10, 150.0, 900.0, 6.0


def doc(p):
    return cv2.imdecode(np.fromfile(p, np.uint8), cv2.IMREAD_COLOR)


def chon_cong_nhan():
    """2 cong nhan dung thang, to ro: 1 co mu + ao, 1 dau tran (theo nhan tap TEST)"""
    du, tran = None, None
    for f in sorted(glob.glob(os.path.join(PPE, "labels", "test", "*.txt"))):
        anh = os.path.join(PPE, "images", "test", os.path.basename(f)[:-4] + ".jpg")
        if not os.path.exists(anh):
            continue
        L = [list(map(float, l.split())) for l in open(f).read().splitlines() if l.strip()]
        img = None
        for c, x, y, w, h in [l[:5] for l in L]:
            if int(c) != 6:
                continue
            if img is None:
                img = doc(anh)
            Hh, Ww = img.shape[:2]
            bw, bh = w * Ww, h * Hh
            if bh < 280 or not (0.25 <= bw / bh <= 0.55):
                continue
            x1, y1, x2, y2 = (x - w / 2) * Ww, (y - h / 2) * Hh, (x + w / 2) * Ww, (y + h / 2) * Hh
            ben_trong = [int(l[0]) for l in L if x1 <= l[1] * Ww <= x2 and y1 <= l[2] * Hh <= y2]
            if 0 in ben_trong and 2 in ben_trong and 7 not in ben_trong and du is None:
                du = (img[int(y1):int(y2), int(x1):int(x2)].copy(), os.path.basename(anh))
            if 7 in ben_trong and 0 not in ben_trong and tran is None:
                tran = (img[int(y1):int(y2), int(x1):int(x2)].copy(), os.path.basename(anh))
        if du and tran:
            break
    return du, tran


def cat_xe(anh, lop):
    from ultralytics import YOLO
    m = YOLO(YOLO_PT)
    r = m.predict(anh, conf=0.4, classes=[lop], verbose=False)[0]
    b = r.boxes.xyxy.cpu().numpy()
    if not len(b):
        raise SystemExit("khong tim thay xe lop %d" % lop)
    x1, y1, x2, y2 = max(b, key=lambda z: (z[2] - z[0]) * (z[3] - z[1])).astype(int)
    return anh[y1:y2, x1:x2].copy()


def nen(k):
    """mat bai: troi xam xanh, nen be tong, vach ke lan"""
    img = np.full((H, W, 3), (120, 128, 132), np.uint8)
    img[:int(Y0)] = (190, 170, 150)
    for Z in np.arange(8, 120, 6.0):
        y = int(Y0 + F * HC / Z)
        cv2.line(img, (0, y), (W, y), (110, 118, 122), 1)
    for X in (-6, 6):
        pts = [(int(W / 2 + F * X / Z), int(Y0 + F * HC / Z)) for Z in (6, 120)]
        cv2.line(img, pts[0], pts[1], (230, 230, 230), 3)
    return img


def dan(img, crop, X, Z, cao_m):
    """dan crop sao cho CHAN o (X, Z) mat dat, cao dung f.h/Z"""
    h = max(8, int(round(F * cao_m / Z)))
    w = max(4, int(round(crop.shape[1] * h / crop.shape[0])))
    c = cv2.resize(crop, (w, h), interpolation=cv2.INTER_AREA if h < crop.shape[0] else cv2.INTER_LINEAR)
    xc, yc = W / 2 + F * X / Z, Y0 + F * HC / Z
    x1, y1 = int(round(xc - w / 2)), int(round(yc - h))
    xa, ya, xb, yb = max(0, x1), max(0, y1), min(W, x1 + w), min(H, y1 + h)
    if xb > xa and yb > ya:
        img[ya:yb, xa:xb] = c[ya - y1:yb - y1, xa - x1:xb - x1]


def main():
    du, tran = chon_cong_nhan()
    print("cong nhan du bao ho:", du[1], "| dau tran:", tran[1])
    xe_tai = cat_xe(doc(os.path.join(ANH_TAI, "11.png")), 7)
    # o to NHIN NGANG (dau xe huong sang phai = huong chay). Ban dau dung anh dau xe nhin thang truot ngang: phi thuc te
    # va YOLO khong nhan ra o 48 px -> kiem thu "qua toc do" khong do dung o to
    o_to_nguon = [os.path.join(DU_LIEU, "bien_so_vn", "yolo_plate_dataset", "yolo_plate_dataset", "ngoaigiao7.jpg")]
    o_to = cat_xe(doc(o_to_nguon[0]), 2)
    tep = os.path.join(RA, "video_an_toan.webm")
    ghi = cv2.VideoWriter(tep, cv2.VideoWriter_fourcc(*"VP80"), FPS, (W, H))
    for k in range(12 * FPS):
        t = k / FPS
        img = nen(k)
        Z_xe = max(62 - 6 * t, 16.6)                  # phanh dung sat nguoi luc t = 7,57 s
        X_b = min(-9 + 0.8 * t, -4.0)
        X_oto = -40 + (30 / 3.6) * t
        # ve xa truoc, gan sau (che khuat dung)
        doi = sorted([("o-to", X_oto, 28, 1.5, o_to), ("xe-tai", 0.3, Z_xe, 3.2, xe_tai), ("A", 0.0, 14.0, 1.7, du[0]),
                      ("B", X_b, 12.0, 1.7, tran[0])], key=lambda d: -d[2])
        for ten, X, Z, cao, crop in doi:
            if ten == "o-to" and not (-25 < X < 25):
                continue
            dan(img, crop, X, Z, cao)
        ghi.write(img)
    ghi.release()
    # vung cam theo toa do anh (0..1): phan phai mat bai o tam xa ~ Z 20-30
    vung = [{"ten": "Dưới cần trục", "loai": "cam-nguoi", "diem": [[0.15, 0.75], [0.30, 0.75], [0.30, 0.92], [0.15, 0.92]]}]
    x_vao = (0.15 * W - W / 2) * 12 / F               # B vao vung khi chan x >= 0,15 W
    t_vao = (x_vao + 9) / 0.8 if x_vao <= -4.0 else None
    t_cham = (62 - 14 - (0.35 + 1.25)) / 6           # neu khong phanh
    dap_an = dict(video=os.path.basename(tep), fps=FPS, thoiLuong=12, gocNhin=round(np.degrees(2 * np.arctan(W / 2 / F)), 1),
                  vung=vung, t_xe_toi_sat_nguoi=round(t_cham, 2), t_b_vao_vung=round(t_vao, 2) if t_vao else None,
                  o_to_kmh=30, gioi_han_kmh=15, nguon=dict(cong_nhan_du=du[1], cong_nhan_dau_tran=tran[1], xe_tai="11.png",
                                                           o_to=os.path.basename(o_to_nguon[0])))
    json.dump(dap_an, open(os.path.join(RA, "dap_an_an_toan.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(dap_an, ensure_ascii=False))
    xuat_khung_kiem_thu(tep, dap_an)


def xuat_khung_kiem_thu(tep, dap_an, tu=3.0, den=7.0):
    """khung 3,0-7,0 s (giai ma tu video WebM nhu camera that) -> JPEG cho kiem thu C# qua gateway (KiemTraMicroservices):
    xe tai lao toi (canh bao va cham), cong nhan dau tran vao vung cam, o to qua toc do"""
    thu = os.path.join(RA, "khung_an_toan")
    os.makedirs(thu, exist_ok=True)
    cap = cv2.VideoCapture(tep)
    ds = []
    k = 0
    while True:
        ok, f = cap.read()
        if not ok:
            break
        t = k / FPS
        if tu - 1e-6 <= t <= den + 1e-6:
            ten = "k%03d.jpg" % k
            cv2.imwrite(os.path.join(thu, ten), f, [cv2.IMWRITE_JPEG_QUALITY, 85])
            ds.append(dict(tep=ten, t=round(t, 2)))
        k += 1
    json.dump(dict(fps=FPS, gocNhin=dap_an["gocNhin"], vung=dap_an["vung"], khung=ds,
                   t_xe_toi_sat_nguoi=dap_an["t_xe_toi_sat_nguoi"], t_b_vao_vung=dap_an["t_b_vao_vung"]),
              open(os.path.join(thu, "khung_an_toan.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("khung kiem thu:", len(ds), "->", thu)


if __name__ == "__main__":
    main()
