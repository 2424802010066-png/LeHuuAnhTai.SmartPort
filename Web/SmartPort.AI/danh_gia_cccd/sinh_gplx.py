# -*- coding: utf-8 -*-
"""Sinh the GPLX MO PHONG (du lieu gia, co chu MAU THU, khong co quoc huy/hoa van that) de danh gia OCR Tesseract + bo boc truong."""
import json, random, sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, str(Path(__file__).parent))
from sinh_the import HO, DEM, TEN, FONT, FONT_B, nen_ngau_nhien, suy_giam

RA = Path(sys.argv[1])
SO = int(sys.argv[2]) if len(sys.argv) > 2 else 25
(RA / "anh").mkdir(parents=True, exist_ok=True)
r = random.Random(555123)
HANG = ["A1", "A2", "B1", "B2", "C", "D", "E", "FC", "FB2", "C1", "C1E", "CE", "D2"]
MUC = ["sach", "nghieng", "mo_net", "jpeg_thap", "thieu_sang"]
meta = []
i = 0
for muc in MUC:
    for _ in range(SO):
        W, H = 1712, 1080
        img = Image.new("RGB", (W, H), (r.randint(235, 250), r.randint(230, 245), r.randint(215, 235)))
        d = ImageDraw.Draw(img)
        f = ImageFont.truetype(FONT, 46)
        fb = ImageFont.truetype(FONT_B, 54)
        so = f"{r.randint(1, 96):02d}{r.randint(0, 9999999999):010d}"
        hang = r.choice(HANG)
        han = f"{r.randint(1, 28):02d}/{r.randint(1, 12):02d}/{r.randint(2027, 2036)}"
        ten = f"{r.choice(HO)} {r.choice(DEM)} {r.choice(TEN)}".upper()
        ns = f"{r.randint(1, 28):02d}/{r.randint(1, 12):02d}/{r.randint(1965, 2003)}"
        tt = f"{r.randint(1, 28):02d}/{r.randint(1, 12):02d}/{r.randint(2015, 2024)}"
        d.text((420, 70), "GIẤY PHÉP LÁI XE / DRIVER'S LICENSE", fill=(20, 40, 90), font=fb)
        d.text((80, 80), "THẺ MÔ PHỎNG", fill=(90, 90, 110), font=ImageFont.truetype(FONT_B, 36))
        d.rectangle([80, 240, 420, 700], fill=(205, 205, 210), outline=(130, 130, 140), width=3)
        dong = [f"Số/No: {so}", f"Họ tên/Full name: {ten}", f"Ngày sinh/Date of Birth: {ns}", f"Hạng/Class: {hang}", f"Ngày trúng tuyển/Beginning date: {tt}", f"Có giá trị đến/Expires: {han}"]
        for k, s in enumerate(dong):
            d.text((470, 240 + k * 92), s, fill=(25, 25, 35), font=f)
        d.text((480, 830), "MẪU THỬ - KHÔNG CÓ GIÁ TRỊ", fill=(210, 40, 40), font=ImageFont.truetype(FONT_B, 76))
        the = np.array(img)[:, :, ::-1].copy()
        # Anh chup: the chiem ~55-85% khung, nghieng nhe
        CW, CH = r.choice([(1280, 720), (1600, 1200), (1920, 1080)])
        tl = r.uniform(.55, .85) * CW / W
        goc = r.uniform(-6, 6) if muc == "nghieng" else r.uniform(-2, 2)
        M = cv2.getRotationMatrix2D((W / 2, H / 2), goc, tl)
        M[0, 2] += CW / 2 - W / 2
        M[1, 2] += CH / 2 - H / 2
        if muc == "nghieng":
            P = np.vstack([M, [0, 0, 1]])
            e = 0.00004 * r.uniform(-1, 1)
            P = P @ np.array([[1, 0, 0], [0, 1, 0], [e, e * .5, 1]])
            anh = cv2.warpPerspective(the, P, (CW, CH), borderValue=(120, 120, 120))
            trong = cv2.warpPerspective(np.ones((H, W), np.float32), P, (CW, CH))
        else:
            anh = cv2.warpAffine(the, M, (CW, CH), flags=cv2.INTER_AREA if tl < 1 else cv2.INTER_LINEAR, borderValue=(120, 120, 120))
            trong = cv2.warpAffine(np.ones((H, W), np.float32), M, (CW, CH))
        nen = nen_ngau_nhien(r, CW, CH)
        a = trong[..., None]
        kq = nen * (1 - a) + anh.astype(np.float32) * a
        kieu = {"sach": "sach", "nghieng": "sach", "mo_net": "mo_net", "jpeg_thap": "sach", "thieu_sang": "thieu_sang"}[muc]
        kq = suy_giam(r, kq, kieu)
        q = r.randint(25, 40) if muc == "jpeg_thap" else r.randint(82, 92)
        ten_tep = f"{i:04d}.jpg"
        cv2.imwrite(str(RA / "anh" / ten_tep), kq, [cv2.IMWRITE_JPEG_QUALITY, q])
        meta.append(dict(tep=ten_tep, muc=muc, so=so, hang=hang, han=han))
        i += 1
(RA / "meta.json").write_text(json.dumps(meta, ensure_ascii=False), encoding="utf-8")
print("xong", len(meta))
