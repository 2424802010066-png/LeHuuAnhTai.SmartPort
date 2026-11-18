# -*- coding: utf-8 -*-
"""
TANG CUONG SUY GIAM cho mo hinh do bao ho — mo phong anh camera cang THAT:
  nguoi o XA (nho trong khung) + luong video NEN (JPEG / H.264) -> dau nguoi con vai chuc pixel, mat chi tiet.
Phat hien (danh_gia_bao_ho_khoang_cach.py + video thu): nen JPEG 85 lam diem "dau tran" cua dau nho tu 0,21 -> 0,03,
mo hinh chi hoc tren anh can canh, net (Construction-PPE) nen khong quen dang anh nay.
Moi anh TRAIN / VAL them 1 BAN SUY GIAM (nhan giu nguyen):
  1. thu nho sao cho nguoi (trung vi chieu cao khung Person) cao T ~ U(40, 160) px  (khong co nguoi: ti le U(0,15; 0,5))
  2. (30%) lam mo Gauss sigma U(0,3; 1,0)  — ong kinh / rung / lay net sai
  3. nen JPEG chat luong U(25, 85) O DO PHAN GIAI NHO (camera nen ca khung luc nguoi con nho)
  4. phong to lai kich thuoc goc (nhu khi cat nguoi roi phong to o tang 2)
TEST giu nguyen (chep sang) — danh gia suy giam lam rieng bang mo phong khung camera (khong dung chung quy trinh nay).
Chay: python tao_du_lieu_suy_giam.py  -> <SmartPort_DuLieuThat>/construction_ppe_suy_giam
"""
import glob
import os
import random
import shutil
import sys

import cv2
import numpy as np

GOC_DL = os.environ.get("SMARTPORT_PPE_DIR", r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat\construction_ppe")
RA = sys.argv[1] if len(sys.argv) > 1 else GOC_DL + "_suy_giam"
NGUOI = 6


def suy_giam(img, nhan, rnd):
    H, W = img.shape[:2]
    cao = [float(l.split()[4]) * H for l in nhan if l.strip() and int(l.split()[0]) == NGUOI]
    if cao:
        s = min(1.0, rnd.uniform(40, 160) / max(1.0, float(np.median(cao))))
    else:
        s = rnd.uniform(0.15, 0.5)
    w, h = max(8, int(round(W * s))), max(8, int(round(H * s)))
    nho = cv2.resize(img, (w, h), interpolation=cv2.INTER_AREA)
    if rnd.random() < 0.3:
        nho = cv2.GaussianBlur(nho, (0, 0), rnd.uniform(0.3, 1.0))
    q = rnd.randint(25, 85)
    nho = cv2.imdecode(cv2.imencode(".jpg", nho, [cv2.IMWRITE_JPEG_QUALITY, q])[1], cv2.IMREAD_COLOR)
    return cv2.resize(nho, (W, H), interpolation=cv2.INTER_LINEAR), dict(s=round(s, 3), q=q)


def main():
    rnd = random.Random(42)
    nhat_ky = []
    for tap in ("train", "val", "test"):
        os.makedirs(os.path.join(RA, "images", tap), exist_ok=True)
        os.makedirs(os.path.join(RA, "labels", tap), exist_ok=True)
        for anh in sorted(glob.glob(os.path.join(GOC_DL, "images", tap, "*.*"))):
            ten, duoi = os.path.splitext(os.path.basename(anh))
            nh = os.path.join(GOC_DL, "labels", tap, ten + ".txt")
            co_nhan = os.path.exists(nh)                 # khong nhan = anh NEN (mau am) — giu nguyen nhu bo goc
            shutil.copy2(anh, os.path.join(RA, "images", tap, ten + duoi))
            if co_nhan:
                shutil.copy2(nh, os.path.join(RA, "labels", tap, ten + ".txt"))
            if tap == "test":
                continue
            img = cv2.imread(anh)
            nhan = open(nh).read().splitlines() if co_nhan else []
            sg, tt = suy_giam(img, nhan, rnd)
            cv2.imwrite(os.path.join(RA, "images", tap, ten + "_sg.jpg"), sg, [cv2.IMWRITE_JPEG_QUALITY, 95])
            if co_nhan:
                shutil.copy2(nh, os.path.join(RA, "labels", tap, ten + "_sg.txt"))
            nhat_ky.append((tap, ten, tt["s"], tt["q"]))
    for tap in ("train", "val", "test"):
        print(tap, len(os.listdir(os.path.join(RA, "images", tap))), "anh")
    s = [x[2] for x in nhat_ky]
    q = [x[3] for x in nhat_ky]
    print("ban suy giam: %d | ti le thu nho trung vi %.2f (%.2f-%.2f) | JPEG trung vi %d" % (
        len(nhat_ky), np.median(s), min(s), max(s), np.median(q)))


if __name__ == "__main__":
    main()
