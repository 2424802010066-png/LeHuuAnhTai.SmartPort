# -*- coding: utf-8 -*-
"""Huan luyen mang U-Net nho KHOI PHUC anh QR bi mo/nhieu -> anh QR den trang sac net (320x320).
Du lieu tu sinh ngau nhien vo han (khong co anh nguoi that): QR chua chuoi gia, seed KHAC bo test.
Muc tieu la anh ma tran QR sach cung hinh hoc (warp) voi dau vao; QR co Reed-Solomon nen mang doan sai
thi zxing se khong giai ma duoc (khong sinh ket qua sai)."""
import math, random, sys, time
from pathlib import Path
import cv2
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.data import DataLoader, IterableDataset

sys.path.insert(0, str(Path(__file__).parent))
RA = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd\runs\khoi_phuc_qr")
RA.mkdir(parents=True, exist_ok=True)
S = 320


def chuoi_gia(r):
    so = "".join(r.choice("0123456789") for _ in range(12))
    ten = "".join(r.choice("AĂÂBCDĐEÊGHIKLMNOÔƠPQRSTUƯVXYaăâbcdđeêghiklmnoôơpqrstuưvxy ") for _ in range(r.randint(10, 26)))
    dc = "".join(r.choice("abcdeghiklmnopqrstuvxyáàảãạđêôơưíóúý ,/0123456789") for _ in range(r.randint(35, 95)))
    return f"{so}|{'' if r.random() < .4 else '123456789'}|{ten}|15041995|{r.choice(['Nam', 'Nữ'])}|{dc}|20082021"


def ma_tran(r):
    p = cv2.QRCodeEncoder_Params()
    p.correction_level = r.choice([cv2.QRCODE_ENCODER_CORRECT_LEVEL_L, cv2.QRCODE_ENCODER_CORRECT_LEVEL_M, cv2.QRCODE_ENCODER_CORRECT_LEVEL_M, cv2.QRCODE_ENCODER_CORRECT_LEVEL_Q])
    p.mode = cv2.QRCODE_ENCODER_MODE_BYTE
    m = cv2.QRCodeEncoder_create(p).encode(chuoi_gia(r))
    k = np.where(m == 0)
    return m[k[0].min():k[0].max() + 1, k[1].min():k[1].max() + 1]


def mau(r, np_r):
    m = ma_tran(r)
    n = m.shape[0]
    sc = 8
    goc_px = n * sc
    clean = cv2.resize(m, (goc_px, goc_px), interpolation=cv2.INTER_NEAREST)
    # Hinh hoc: QR nam trong canvas S x S, canh 180-295 px, xoay + phoi canh
    canh = r.uniform(180, 295)
    a = math.radians(r.uniform(-180, 180) if r.random() < .12 else r.uniform(-35, 35))
    tam = (S / 2 + r.uniform(-14, 14), S / 2 + r.uniform(-14, 14))
    nua = canh / 2
    goc = [(-nua, -nua), (nua, -nua), (nua, nua), (-nua, nua)]
    lech = canh * r.uniform(0, .10)
    dst = [(x * math.cos(a) - y * math.sin(a) + tam[0] + r.uniform(-lech, lech), x * math.sin(a) + y * math.cos(a) + tam[1] + r.uniform(-lech, lech)) for x, y in goc]
    H = cv2.getPerspectiveTransform(np.float32([[0, 0], [goc_px, 0], [goc_px, goc_px], [0, goc_px]]), np.float32(dst))
    # Nen the: sang, co vat can ngau nhien ngoai vung QR
    c = r.randint(195, 250)
    nen = np.full((S, S), c, np.float32)
    for _ in range(r.randint(0, 9)):
        x, y = r.randint(0, S), r.randint(0, S)
        cv2.line(nen, (x, y), (x + r.randint(-120, 120), y + r.randint(-14, 14)), r.randint(20, 120), r.randint(1, 5))
    # vien lang (quiet zone) trang quanh QR
    H2 = H
    mat_qr = cv2.warpPerspective(np.ones_like(clean, np.uint8), H2, (S, S), flags=cv2.INTER_NEAREST)
    k = max(3, int(canh / 49 * 4)) | 1
    lang = cv2.dilate(mat_qr, np.ones((k, k), np.uint8))
    nen[lang > 0] = min(255, c + 8)
    # den cua QR khong tuyet doi den tren the in
    den = r.uniform(5, 45)
    sang = float(c)
    qr_w = cv2.warpPerspective(clean.astype(np.float32) / 255.0, H2, (S, S), flags=cv2.INTER_AREA if False else cv2.INTER_LINEAR)
    anh = nen.copy()
    anh[mat_qr > 0] = den + (sang - den) * qr_w[mat_qr > 0]
    # Dich: QR nhi phan sach tren nen trang (sac net) + mat na vung QR
    dich = np.ones((S, S), np.float32)
    bin_w = cv2.warpPerspective(clean, H2, (S, S), flags=cv2.INTER_NEAREST)
    dich[mat_qr > 0] = (bin_w[mat_qr > 0] > 127).astype(np.float32)
    trong_so = np.full((S, S), .15, np.float32)
    trong_so[lang > 0] = 1.0

    # Lay mau ve do phan giai nguon (px/module o nguon = p) roi suy giam o nguon
    m_c = canh / n
    p = r.uniform(1.25, 6.5)
    k = m_c / p
    w = int(max(70, min(640, S / k)))
    src = cv2.resize(anh, (w, w), interpolation=cv2.INTER_AREA if w < S else cv2.INTER_LINEAR)
    nhe = r.random() < .22
    src = cv2.GaussianBlur(src, (0, 0), r.uniform(.3, .7))
    if not nhe:
        if r.random() < .55:
            do = r.randint(2, 11)
            ga = r.uniform(0, 180)
            ker = np.zeros((2 * do + 1, 2 * do + 1), np.float32)
            for t in np.linspace(-do, do, do * 8):
                ker[int(round(do + t * math.sin(math.radians(ga)))), int(round(do + t * math.cos(math.radians(ga))))] = 1
            src = cv2.filter2D(src, -1, ker / ker.sum())
        if r.random() < .45:
            src = cv2.GaussianBlur(src, (0, 0), r.uniform(.6, 2.6))
        if r.random() < .25:
            src = 255 * (np.clip(src, 0, 255) / 255.0) ** r.uniform(1.4, 2.8) * r.uniform(.25, .7)
        if r.random() < .25:
            gl = np.zeros(src.shape, np.float32)
            cv2.ellipse(gl, (r.randint(0, w), r.randint(0, w)), (r.randint(w // 6, w // 2), r.randint(w // 6, w // 2)), r.uniform(0, 180), 0, 360, 1.0, -1)
            gl = cv2.GaussianBlur(gl, (0, 0), w / r.uniform(6, 20))
            src = src + gl * r.uniform(40, 170)
        if r.random() < .5:
            src = src + np_r.normal(0, r.uniform(3, 26), src.shape)
    src = np.clip(src, 0, 255).astype(np.uint8)
    if r.random() < .7:
        _, b = cv2.imencode(".jpg", src, [cv2.IMWRITE_JPEG_QUALITY, r.randint(25, 95)])
        src = cv2.imdecode(b, 0)
    vao = cv2.resize(src, (S, S), interpolation=cv2.INTER_CUBIC if w < S else cv2.INTER_AREA).astype(np.float32) / 255.0
    return vao[None], dich[None], trong_so[None]


class Ds(IterableDataset):
    def __init__(self, n, seed):
        self.n, self.seed = n, seed

    def __iter__(self):
        w = torch.utils.data.get_worker_info()
        id_ = w.id if w else 0
        so = self.n // (w.num_workers if w else 1)
        cv2.setNumThreads(0)
        r = random.Random(self.seed + id_ * 1000 + torch.initial_seed() % 100000)
        np_r = np.random.RandomState((self.seed + id_ * 7) % (2**31))
        for _ in range(so):
            a, b, c = mau(r, np_r)
            yield torch.from_numpy(a), torch.from_numpy(b), torch.from_numpy(c)


class Khoi(nn.Module):
    def __init__(self, v, r):
        super().__init__()
        self.a = nn.Conv2d(v, r, 3, padding=1)
        self.b = nn.Conv2d(r, r, 3, padding=1)

    def forward(self, x):
        return F.leaky_relu(self.b(F.leaky_relu(self.a(x), .1)), .1)


class UNet(nn.Module):
    def __init__(self, c=(20, 40, 80, 120)):
        super().__init__()
        self.d1, self.d2, self.d3 = Khoi(1, c[0]), Khoi(c[0], c[1]), Khoi(c[1], c[2])
        self.g = Khoi(c[2], c[3])
        self.u3, self.u2, self.u1 = Khoi(c[3] + c[2], c[2]), Khoi(c[2] + c[1], c[1]), Khoi(c[1] + c[0], c[0])
        self.ra = nn.Conv2d(c[0], 1, 1)

    def forward(self, x):
        d1 = self.d1(x)
        d2 = self.d2(F.avg_pool2d(d1, 2))
        d3 = self.d3(F.avg_pool2d(d2, 2))
        g = self.g(F.avg_pool2d(d3, 2))
        u = self.u3(torch.cat([F.interpolate(g, scale_factor=2.0, mode="bilinear", align_corners=False), d3], 1))
        u = self.u2(torch.cat([F.interpolate(u, scale_factor=2.0, mode="bilinear", align_corners=False), d2], 1))
        u = self.u1(torch.cat([F.interpolate(u, scale_factor=2.0, mode="bilinear", align_corners=False), d1], 1))
        return self.ra(u)


class XuatSigmoid(nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, x):
        return torch.sigmoid(self.m(x))


def main():
    so_epoch = int(sys.argv[1]) if len(sys.argv) > 1 else 16
    dev = "cuda"
    m = UNet().to(dev)
    print("tham so:", sum(p.numel() for p in m.parameters()), flush=True)
    op = torch.optim.AdamW(m.parameters(), 1e-3, weight_decay=1e-5)
    sch = torch.optim.lr_scheduler.CosineAnnealingLR(op, so_epoch)
    scaler = torch.amp.GradScaler("cuda")
    dl = DataLoader(Ds(9000, 91001), batch_size=20, num_workers=6, persistent_workers=True, prefetch_factor=3, drop_last=True)
    dv = DataLoader(Ds(800, 55000011), batch_size=20, num_workers=3, persistent_workers=True)
    tot = 1e9
    for ep in range(so_epoch):
        t0 = time.time()
        m.train()
        s, n = 0.0, 0
        for a, b, c in dl:
            a, b, c = a.to(dev, non_blocking=True), b.to(dev, non_blocking=True), c.to(dev, non_blocking=True)
            op.zero_grad(set_to_none=True)
            with torch.amp.autocast("cuda"):
                l = (F.binary_cross_entropy_with_logits(m(a), b, reduction="none") * c).mean()
            scaler.scale(l).backward()
            scaler.step(op)
            scaler.update()
            s += float(l)
            n += 1
        sch.step()
        m.eval()
        vs, vn, dung, tong = 0.0, 0, 0, 0
        with torch.no_grad():
            for a, b, c in dv:
                a, b, c = a.to(dev), b.to(dev), c.to(dev)
                with torch.amp.autocast("cuda"):
                    o = m(a)
                    vs += float((F.binary_cross_entropy_with_logits(o, b, reduction="none") * c).mean())
                vn += 1
                k = c > .5
                dung += int((((o > 0) == (b > .5)) & k).sum())
                tong += int(k.sum())
        print(f"epoch {ep + 1}/{so_epoch} train {s / n:.4f} val {vs / vn:.4f} acc_vung_qr {100 * dung / tong:.2f}% {time.time() - t0:.0f}s", flush=True)
        if vs / vn < tot:
            tot = vs / vn
            torch.save(m.state_dict(), RA / "tot.pt")
    m.load_state_dict(torch.load(RA / "tot.pt", weights_only=True))
    xuat = XuatSigmoid(m.cpu().eval())
    f = RA / "qr_khoi_phuc.onnx"
    torch.onnx.export(xuat, torch.zeros(1, 1, S, S), str(f), opset_version=17, input_names=["anh"], output_names=["ra"], dynamo=False)
    print("onnx:", f, round(f.stat().st_size / 1e6, 2), "MB", flush=True)


if __name__ == "__main__":
    main()
