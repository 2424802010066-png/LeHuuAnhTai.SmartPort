# -*- coding: utf-8 -*-
"""
BO DOC MA CONTAINER: CRNN (CNN + BiLSTM + CTC) + GIAI MA CO RANG BUOC SO KIEM TRA ISO 6346.
- Hoc tren: anh cat vung ma THAT (du_lieu/cont_ocr/vung_ma.json, chi tap train/val) + anh GIA LAP sinh ngau nhien
  (ma hop le ISO 6346, ngang / doc / 2 dong, nhieu font Windows, mau son vo container, nhieu, mo, nghieng).
- Ma doc (chu xep chong theo chieu dung): xoay 90 do de chieu doc thanh chieu thoi gian cua CTC.
- Giai ma rang buoc (de tai NCKH): tim chuoi XAC SUAT CAO NHAT trong cac chuoi DUNG CAU TRUC [A-Z]{3}[UJZ][0-9]{7}
  VA DUNG SO KIEM TRA; khong co thi tra chuoi tham lam (danh dau khong hop le).
Chay: python doc_ma_container.py train | danh_gia
"""
import collections
import functools
import io
import json
import math
import os
import pickle
import random
import re
import sys
import time

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

GOC = os.path.dirname(os.path.abspath(__file__))
KT = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
C2I = {c: i + 1 for i, c in enumerate(KT)}       # 0 = blank cua CTC
LOP = ["ma_ngang", "ma_doc", "so_huu", "seri", "ma_loai"]   # thu tu lop bo phat hien vung ma (= LOP_CONT)
CAO, RONG = 48, 384
THIET_BI = "cuda" if torch.cuda.is_available() else "cpu"
RA = os.path.join(GOC, "runs", "doc_ma_container")
GT6346 = dict(zip("ABCDEFGHIJKLMNOPQRSTUVWXYZ", [10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29,
                                                30, 31, 32, 34, 35, 36, 37, 38]))
XOAY_THEO_TI_LE = [True]     # xoay vung theo ti le khung (moi) / theo lop ma_doc (cu) — xem la_doc()
TEP_CHU_SO_HUU = os.path.join(GOC, "du_lieu", "cont_ocr", "chu_so_huu_da_biet.json")
THUONG_CHU_BIET = 2.0        # diem cong (log) cho ma co chu so huu da biet — chinh tren tap VAL
NGUONG_TIN_CAY = -12.0       # log P(ma | vung) thap hon -> khong tin, chuyen kiem tay — chinh tren tap VAL


def so_kiem_tra(ma10):
    s = sum((int(c) if c.isdigit() else GT6346[c]) * 2 ** i for i, c in enumerate(ma10))
    return s % 11 % 10


def dung_6346(ma):
    return bool(re.fullmatch(r"[A-Z]{3}[UJZ]\d{7}", ma)) and so_kiem_tra(ma[:10]) == int(ma[10])


# ----------------------------------------------------------------------------------------------- anh gia lap
FONTS = [f for f in ["arialbd.ttf", "arial.ttf", "impact.ttf", "tahomabd.ttf", "verdanab.ttf", "consolab.ttf",
                     "segoeuib.ttf", "calibrib.ttf", "trebucbd.ttf", "framd.ttf", "bahnschrift.ttf", "lucon.ttf"]
         if os.path.exists(os.path.join(r"C:\Windows\Fonts", f))]
MAU_VO = [(28, 64, 140), (160, 30, 35), (35, 110, 60), (120, 120, 120), (230, 230, 230), (20, 20, 20), (200, 120, 30),
          (90, 40, 30), (40, 90, 120), (240, 200, 60)]


def ma_ngau_nhien():
    so_huu = "".join(random.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ") for _ in range(3)) + random.choice("UUUUUUJZ")
    seri = "".join(random.choice("0123456789") for _ in range(6))
    return so_huu + seri + str(so_kiem_tra(so_huu + seri))


def ve_chuoi(chuoi, font, mau_chu, mau_nen, doc=False, khung_cuoi=False):
    """ve 1 dong (ngang) hoac chu xep chong (doc) len nen co hoa van vo container"""
    co = font.size
    if not doc:
        rong = int(sum(font.getlength(c) for c in chuoi) + co * (0.25 * len(chuoi) + 1.2))
        img = Image.new("RGB", (rong, int(co * 1.6)), mau_nen)
        d = ImageDraw.Draw(img)
        x = co * 0.5
        for i, c in enumerate(chuoi):
            if i in (4, 10) and random.random() < 0.6:
                x += co * random.uniform(0.3, 0.9)          # khoang trong truoc seri / so kiem tra
            d.text((x, co * 0.25), c, font=font, fill=mau_chu)
            if khung_cuoi and i == len(chuoi) - 1:
                w = font.getlength(c)
                d.rectangle([x - co * 0.12, co * 0.18, x + w + co * 0.12, co * 1.4], outline=mau_chu,
                            width=max(1, co // 14))
            x += font.getlength(c) + co * random.uniform(0.05, 0.3)
        return img.crop((0, 0, int(x + co * 0.5), img.height))
    cao = int(co * 1.15 * len(chuoi) + co * 2)
    img = Image.new("RGB", (int(co * 1.6), cao), mau_nen)
    d = ImageDraw.Draw(img)
    y = co * 0.4
    for i, c in enumerate(chuoi):
        if i in (4, 10) and random.random() < 0.5:
            y += co * random.uniform(0.2, 0.6)
        w = font.getlength(c)
        d.text(((img.width - w) / 2, y), c, font=font, fill=mau_chu)
        if khung_cuoi and i == len(chuoi) - 1:
            d.rectangle([(img.width - w) / 2 - co * 0.12, y - co * 0.05, (img.width + w) / 2 + co * 0.12, y + co * 1.2],
                        outline=mau_chu, width=max(1, co // 14))
        y += co * random.uniform(1.0, 1.25)
    return img.crop((0, 0, img.width, int(min(img.height, y + co * 0.4))))


@functools.lru_cache(maxsize=512)
def _font(ten, co):
    return ImageFont.truetype(os.path.join(r"C:\Windows\Fonts", ten), co)


def gia_lap():
    """tra ve (anh PIL da dua ve chieu ngang, chuoi dap an)"""
    font = _font(random.choice(FONTS), random.randint(28, 56))
    nen = random.choice(MAU_VO)
    chu = (245, 245, 245) if sum(nen) < 380 else (15, 15, 15)
    if random.random() < 0.15:
        chu = random.choice([(250, 250, 250), (10, 10, 10), (220, 200, 40)])
    loai = random.choices(["ngang", "doc", "so_huu", "seri", "loai"], [45, 30, 8, 10, 7])[0]
    ma = ma_ngau_nhien()
    if loai == "so_huu":
        chuoi = ma[:4]
    elif loai == "seri":
        chuoi = ma[4:]
    elif loai == "loai":
        chuoi = random.choice(["22G1", "45G1", "42G1", "22R1", "45R1", "22U1", "L5G1", "22T1", "25G1", "45U1"])
    else:
        chuoi = ma
    img = ve_chuoi(chuoi, font, chu, nen, doc=(loai == "doc"),
                   khung_cuoi=(len(chuoi) in (7, 11) and chuoi[-1].isdigit() and random.random() < 0.5))
    # font container thuong la chu DUNG HEP (condensed): ep ngang ngau nhien
    img = img.resize((max(8, int(img.width * random.uniform(0.55, 1.1))), img.height), Image.BILINEAR)
    if loai == "doc":
        img = img.rotate(90, expand=True)                        # chieu doc -> chieu thoi gian CTC
    # hoa van gan song (vach vo container), nhieu, mo, nghieng nhe
    arr = np.asarray(img).astype(np.float32)
    if random.random() < 0.7:
        ky = random.uniform(6, 22)
        song = (np.sin(np.arange(arr.shape[1]) / ky * 2 * np.pi) * random.uniform(5, 25))[None, :, None]
        arr = arr + song
    arr = arr + np.random.normal(0, random.uniform(0, 14), arr.shape)
    arr = arr * random.uniform(0.6, 1.25) + random.uniform(-30, 30)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    if random.random() < 0.5:
        img = img.filter(ImageFilter.GaussianBlur(random.uniform(0.3, 1.6)))
    if random.random() < 0.5:
        img = img.rotate(random.uniform(-4, 4), expand=True, fillcolor=nen)
    return img, chuoi


# ----------------------------------------------------------------------------------------------- du lieu that
def cat_that(split):
    ds = json.load(open(os.path.join(GOC, "du_lieu", "cont_ocr", "vung_ma.json"), encoding="utf-8"))
    return [d for d in ds if d["split"] == split]


_CACHE = {}
_VUNG = None
BO_DEM = os.path.join(GOC, "du_lieu", "cont_ocr", "vung_cat.pkl")


def tao_bo_dem_vung():
    """cat san moi vung ma (le rong 0.6 x canh ngan) luu JPEG vao 1 file pickle; anh_that lay tu day neu co"""
    ds = json.load(open(os.path.join(GOC, "du_lieu", "cont_ocr", "vung_ma.json"), encoding="utf-8"))
    theo_anh = collections.defaultdict(list)
    for d in ds:
        theo_anh[d["anh"]].append(d)
    ra = {}
    for anh, ds_anh in theo_anh.items():
        img = Image.open(anh).convert("RGB")
        for d in ds_anh:
            x1, y1, x2, y2 = d["hop"]
            le = 0.6 * min(x2 - x1, y2 - y1)
            X1, Y1 = max(0, int(x1 - le)), max(0, int(y1 - le))
            cat = img.crop((X1, Y1, min(img.width, int(x2 + le) + 1), min(img.height, int(y2 + le) + 1)))
            b = io.BytesIO()
            cat.save(b, "JPEG", quality=95)
            ra[(anh, tuple(d["hop"]))] = (b.getvalue(), X1, Y1, img.width, img.height)
    pickle.dump(ra, open(BO_DEM, "wb"))
    print("bo dem vung:", len(ra), "vung,", round(os.path.getsize(BO_DEM) / 1e6, 1), "MB", flush=True)


def _bo_dem():
    global _VUNG
    if _VUNG is None:
        _VUNG = pickle.load(open(BO_DEM, "rb")) if os.path.exists(BO_DEM) else {}
    return _VUNG


def la_doc(hop):
    """Vung chu xep chong theo chieu dung (cao > 1.2 lan rong) -> xoay 90 do truoc khi doc.
    Xet theo TI LE KHUNG, khong theo lop: so_huu/seri cua ma 2 khoi DUNG khong co lop rieng (truoc day khong xoay ->
    doc sai), va bo phat hien co the nham lop ma_ngang/ma_doc."""
    x1, y1, x2, y2 = hop
    return (y2 - y1) > 1.2 * (x2 - x1)


def anh_that(d, le=0.2, ngau_nhien=True, hop=None):
    """cat vung ma tu anh goc; le tinh theo CANH NGAN (chieu cao chu) de ma ngang va ma doc co ti le chu/nen nhu nhau.
    Huan luyen: le ngau nhien moi canh tu -5% (cat lem nhu khung phat hien lech) den +le."""
    x1, y1, x2, y2 = hop or d["hop"]
    s = min(x2 - x1, y2 - y1)
    le4 = [random.uniform(-0.05, le) * s for _ in range(4)] if ngau_nhien else [le / 2 * s] * 4
    vc = _bo_dem().get((d["anh"], (x1, y1, x2, y2))) if le <= 0.5 else None
    if vc:
        img, ox, oy, W, H = Image.open(io.BytesIO(vc[0])).convert("RGB"), vc[1], vc[2], vc[3], vc[4]
    else:
        if d["anh"] not in _CACHE:
            if len(_CACHE) > 400:
                _CACHE.pop(next(iter(_CACHE)))
            _CACHE[d["anh"]] = Image.open(d["anh"]).convert("RGB")
        img, ox, oy = _CACHE[d["anh"]], 0, 0
        W, H = img.size
    cat = img.crop((max(0, int(x1 - le4[0])) - ox, max(0, int(y1 - le4[1])) - oy,
                    min(W, int(x2 + le4[2])) - ox, min(H, int(y2 + le4[3])) - oy))
    if XOAY_THEO_TI_LE[0] and d["lop"] != "ma_loai":
        doc = la_doc((x1, y1, x2, y2))
    else:
        doc = d["lop"] == "ma_doc"                    # cach cu (mo hinh crnn_tot_nhat.pt hoc theo cach nay)
    if doc:
        cat = cat.rotate(90, expand=True)
    return cat


def chuan_hoa(img):
    """PIL -> tensor 1xCAOxRONG (xam, giu ti le, dem phai)"""
    img = ImageOps.grayscale(img)
    w = max(8, min(RONG, int(img.width * CAO / max(1, img.height))))
    img = img.resize((w, CAO), Image.BILINEAR)
    nen = Image.new("L", (RONG, CAO), int(np.median(np.asarray(img))))
    nen.paste(img, (0, 0))
    t = torch.from_numpy(np.asarray(nen, dtype=np.float32) / 255.0)[None]
    return (t - 0.5) / 0.5


class TapDuLieu(torch.utils.data.Dataset):
    def __init__(self, that, so_gia_lap, tang_cuong=True):
        self.that, self.n_gia, self.tc = that, so_gia_lap, tang_cuong

    def __len__(self):
        return len(self.that) + self.n_gia

    def __getitem__(self, i):
        if i < len(self.that):
            d = self.that[i]
            img, chuoi = anh_that(d, ngau_nhien=self.tc), d["chuoi"]
            if self.tc and random.random() < 0.3:
                img = img.filter(ImageFilter.GaussianBlur(random.uniform(0.2, 1.0)))
        else:
            img, chuoi = gia_lap()
        return chuan_hoa(img), torch.tensor([C2I[c] for c in chuoi if c in C2I]), chuoi


def gom(batch):
    x = torch.stack([b[0] for b in batch])
    nhan = torch.cat([b[1] for b in batch])
    dai = torch.tensor([len(b[1]) for b in batch])
    return x, nhan, dai, [b[2] for b in batch]


# ----------------------------------------------------------------------------------------------- mo hinh
def khoi(vao, ra, buoc=(1, 1)):
    return nn.Sequential(nn.Conv2d(vao, ra, 3, buoc, 1, bias=False), nn.BatchNorm2d(ra), nn.SiLU(),
                         nn.Conv2d(ra, ra, 3, 1, 1, bias=False), nn.BatchNorm2d(ra), nn.SiLU())


class CRNN(nn.Module):
    def __init__(self, so_lop=len(KT) + 1):
        super().__init__()
        self.cnn = nn.Sequential(khoi(1, 48), nn.MaxPool2d(2),              # 24 x 192
                                 khoi(48, 96), nn.MaxPool2d(2),             # 12 x 96
                                 khoi(96, 192), nn.MaxPool2d((2, 1)),       # 6 x 96
                                 khoi(192, 256), nn.MaxPool2d((2, 1)),      # 3 x 96
                                 nn.Conv2d(256, 256, (3, 1)), nn.BatchNorm2d(256), nn.SiLU())  # 1 x 96
        self.rnn = nn.LSTM(256, 192, num_layers=2, bidirectional=True, batch_first=True, dropout=0.2)
        self.fc = nn.Linear(384, so_lop)

    def forward(self, x):
        f = self.cnn(x).squeeze(2).permute(0, 2, 1)       # B x T x C
        f, _ = self.rnn(f)
        return self.fc(f)                                  # B x T x lop (logit)


# ----------------------------------------------------------------------------------------------- giai ma
def giai_tham_lam(logp):
    """logp: T x lop (numpy). Tham lam: bo lap + bo blank."""
    idx = logp.argmax(1)
    ra, truoc = [], 0
    for i in idx:
        if i != truoc and i != 0:
            ra.append(KT[i - 1])
        truoc = i
    return "".join(ra)


def _cho_phep(vi_tri, c):
    if vi_tri < 3:
        return c.isalpha()
    if vi_tri == 3:
        return c in "UJZ"
    return c.isdigit()


def giai_rang_buoc(logp, beam=24, tat_ca=False):
    """CTC prefix beam search CO RANG BUOC cau truc ISO 6346 (11 ky tu) + chon chuoi xac suat cao nhat DUNG SO KIEM TRA.
    Tra (chuoi, hop_le, log_xac_suat); tat_ca=True -> list (chuoi, log_xac_suat) moi chuoi dung ISO trong chum."""
    T = logp.shape[0]
    p = np.exp(logp)
    # beam: prefix -> (P_blank, P_nonblank)
    beams = {"": (1.0, 0.0)}
    for t in range(T):
        moi = {}
        for pre, (pb, pnb) in beams.items():
            # blank
            b0, n0 = moi.get(pre, (0.0, 0.0))
            moi[pre] = (b0 + (pb + pnb) * p[t, 0], n0)
            # lap lai ky tu cuoi (khong qua blank)
            if pre:
                k = C2I[pre[-1]]
                b0, n0 = moi[pre]
                moi[pre] = (b0, n0 + pnb * p[t, k])
            if len(pre) >= 11:
                continue
            for k in range(1, len(KT) + 1):
                c = KT[k - 1]
                if not _cho_phep(len(pre), c):
                    continue
                q = p[t, k]
                if q < 1e-4:
                    continue
                moi_pre = pre + c
                b1, n1 = moi.get(moi_pre, (0.0, 0.0))
                if pre and pre[-1] == c:
                    moi[moi_pre] = (b1, n1 + pb * q)
                else:
                    moi[moi_pre] = (b1, n1 + (pb + pnb) * q)
        beams = dict(sorted(moi.items(), key=lambda kv: -(kv[1][0] + kv[1][1]))[:beam])
    ung_vien = sorted(((pre, pb + pnb) for pre, (pb, pnb) in beams.items() if len(pre) == 11),
                      key=lambda kv: -kv[1])
    if tat_ca:
        return [(pre, math.log(max(xs, 1e-300))) for pre, xs in ung_vien if dung_6346(pre)]
    for pre, xs in ung_vien:
        if dung_6346(pre):
            return pre, True, math.log(max(xs, 1e-300))
    tl = giai_tham_lam(logp)
    return tl, dung_6346(tl), None


# ----------------------------------------------------------------------------------------------- huan luyen
def huan_luyen(so_vong=40, so_gia_lap=40000):
    random.seed(42)
    torch.manual_seed(42)
    os.makedirs(RA, exist_ok=True)
    tr = TapDuLieu(cat_that("train") * 3, so_gia_lap)            # anh that lap 3 lan (tang cuong khac nhau)
    va = TapDuLieu(cat_that("val"), 0, tang_cuong=False)
    dl_tr = torch.utils.data.DataLoader(tr, batch_size=96, shuffle=True, num_workers=8, collate_fn=gom,
                                        persistent_workers=True)
    dl_va = torch.utils.data.DataLoader(va, batch_size=128, shuffle=False, num_workers=2, collate_fn=gom)
    torch.backends.cudnn.benchmark = True
    m = CRNN().to(THIET_BI).to(memory_format=torch.channels_last)
    opt = torch.optim.AdamW(m.parameters(), lr=1e-3, weight_decay=1e-4)
    sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=1.5e-3, total_steps=so_vong * len(dl_tr), pct_start=0.1)
    ctc = nn.CTCLoss(blank=0, zero_infinity=True)
    tot = -1
    for v in range(so_vong):
        m.train()
        t0, tong = time.time(), 0.0
        for x, nhan, dai, _ in dl_tr:
            x = x.to(THIET_BI, non_blocking=True).to(memory_format=torch.channels_last)
            with torch.autocast("cuda", dtype=torch.bfloat16):    # bf16: nhanh gap 2 (821 so voi 398 mau/s)
                lg = m(x)
            out = lg.float().log_softmax(2)                        # CTC tinh bang float32 cho on dinh
            T = out.shape[1]
            loss = ctc(out.permute(1, 0, 2), nhan, torch.full((x.shape[0],), T, dtype=torch.long), dai)
            opt.zero_grad()
            loss.backward()
            nn.utils.clip_grad_norm_(m.parameters(), 5)
            opt.step()
            sched.step()
            tong += loss.item()
        acc = danh_gia_tap(m, dl_va)
        print("VONG %d/%d loss %.4f | val dung tron chuoi %.4f | %.0fs" % (v + 1, so_vong, tong / len(dl_tr), acc,
                                                                         time.time() - t0), flush=True)
        if acc > tot:
            tot = acc
            torch.save(m.state_dict(), os.path.join(RA, "crnn_tot_nhat.pt"))
    print("XONG huan luyen, val tot nhat %.4f" % tot, flush=True)


@torch.no_grad()
def danh_gia_tap(m, dl):
    m.eval()
    dung = n = 0
    for x, _, _, chuoi in dl:
        lp = m(x.to(THIET_BI)).log_softmax(2).cpu().numpy()
        for i, c in enumerate(chuoi):
            dung += giai_tham_lam(lp[i]) == c
            n += 1
    return dung / max(1, n)


@torch.no_grad()
def doc_vung(m, d, cac_le=(0.1, 0.2, 0.35)):
    """doc 1 vung (dict anh/lop/hop) voi vai muc le (TTA); tra list log-prob numpy T x lop"""
    xs = torch.stack([chuan_hoa(anh_that(d, le=l, ngau_nhien=False)) for l in cac_le]).to(THIET_BI)
    return list(m(xs).log_softmax(2).float().cpu().numpy())


_CHU_BIET = [None]


def chu_so_huu_da_biet():
    """Ma chu so huu (BIC, 4 chu) da gap trong du lieu HUAN LUYEN — o cang that thay bang so dang ky BIC / danh sach
    hang tau khach hang. Chi la uu tien NHE (THUONG_CHU_BIET), ma la van doc duoc."""
    if _CHU_BIET[0] is None:
        _CHU_BIET[0] = set(json.load(open(TEP_CHU_SO_HUU, encoding="utf-8"))) if os.path.exists(TEP_CHU_SO_HUU) else set()
    return _CHU_BIET[0]


def log_p_chuoi(views, cac_chuoi):
    """log P(chuoi | view) cho MOI cap (chuoi, view) bang thuat toan CTC forward (F.ctc_loss, cong moi cach canh
    hang) -> mang K x V. Chinh xac hon diem beam search (beam chi giu mot phan duong di)."""
    if not views or not cac_chuoi:
        return np.zeros((len(cac_chuoi), len(views)))
    x = torch.from_numpy(np.stack(views).astype(np.float32))                       # V x T x C
    V, T, C = x.shape
    K = len(cac_chuoi)
    xs = x.unsqueeze(0).expand(K, V, T, C).reshape(K * V, T, C).permute(1, 0, 2)   # T x (K*V) x C
    tg = torch.tensor([[C2I[c] for c in s] for s in cac_chuoi]).repeat_interleave(V, 0)
    nll = F.ctc_loss(xs, tg, torch.full((K * V,), T, dtype=torch.long), torch.full((K * V,), tg.shape[1], dtype=torch.long),
                     blank=0, reduction="none", zero_infinity=False)
    return np.nan_to_num(-nll.reshape(K, V).numpy(), neginf=-1e4, posinf=-1e4)


def _logsumexp(a, axis=None):
    m = np.max(a, axis=axis, keepdims=True)
    return np.squeeze(m, axis=axis) + np.log(np.sum(np.exp(a - m), axis=axis))


def bi_cat_mep(hop, kich_thuoc, le=3):
    """Vung cham mep anh THEO CHIEU DOC CHU (ma ngang cham trai/phai, ma dung cham tren/duoi) -> co the MAT ky tu:
    giai ma rang buoc se 'bia' ky tu cho qua ISO -> khong tin, chuyen kiem tay."""
    if not kich_thuoc:
        return False
    W, H = kich_thuoc
    x1, y1, x2, y2 = hop
    if la_doc(hop):
        return y1 <= le or y2 >= H - le
    return x1 <= le or x2 >= W - le


@torch.no_grad()
def chon_ma(m, vung, kich_thuoc=None, chu_biet=None, thuong=None, nguong=None, so_ung_vien=5, chi_tiet=False):
    """GOP CHUNG CU tu moi vung cua MOT anh (vung: list dict anh, lop, hop, conf tuy chon).
    Nguon = moi vung ma_ngang/ma_doc + moi cap (so_huu, seri) cua ma 2 khoi; moi nguon doc voi 3 muc le (TTA).
    1. Ung vien: chuoi DUNG ISO 6346 trong chum beam rang buoc cua moi lan doc + bien the doi CHU SO HUU sang ma da
       biet (sai <= 2 chu, van dung so kiem tra).
    2. Diem = log [ tong_nguon w * P(chuoi | nguon) ] + thuong neu chu so huu da biet; P tinh DUNG bang CTC forward,
       w = do tin cay bo phat hien (vung sai/le loi khong keo diem ma dung xuong nhu bo phieu).
    3. Tin cay khi co nguon KHONG cat mep ung ho voi log P >= nguong; nguoc lai TU CHOI (chuyen kiem tay).
    Tra (ma, hop_le, ma_tham_lam_dau_tien)."""
    chu_biet = chu_so_huu_da_biet() if chu_biet is None else chu_biet
    thuong = THUONG_CHU_BIET if thuong is None else thuong
    nguong = NGUONG_TIN_CAY if nguong is None else nguong
    doc = {id(d): doc_vung(m, d) for d in vung if d["lop"] in ("ma_ngang", "ma_doc", "so_huu", "seri")}
    nguon = []                                                   # (views, trong_so, bi_cat)
    for d in vung:
        if d["lop"] in ("ma_ngang", "ma_doc"):
            nguon.append((doc[id(d)], d.get("conf", 1.0), bi_cat_mep(d["hop"], kich_thuoc)))
    for a in (d for d in vung if d["lop"] == "so_huu"):
        for b in (d for d in vung if d["lop"] == "seri"):
            nguon.append(([np.concatenate([x, y], 0) for x, y in zip(doc[id(a)], doc[id(b)])],
                          min(a.get("conf", 1.0), b.get("conf", 1.0)),
                          bi_cat_mep(a["hop"], kich_thuoc) or bi_cat_mep(b["hop"], kich_thuoc)))
    if not nguon:
        return ("", False, "", dict(do_tin_cay=0.0, so_nguon=0)) if chi_tiet else ("", False, "")
    return quyet_dinh(nguon, chu_biet, thuong, nguong, so_ung_vien, chi_tiet)


def ung_vien_nguon(views, so_ung_vien=5):
    """Chuoi dung ISO 6346 trong chum beam rang buoc cua moi lan doc (TTA) cua 1 nguon"""
    ra = set()
    for v in views:
        ra.update(c for c, _ in giai_rang_buoc(v, tat_ca=True)[:so_ung_vien])
    return ra


def quyet_dinh(nguon, chu_biet, thuong, nguong, so_ung_vien=5, chi_tiet=False):
    """Phan quyet dinh cua chon_ma (tach rieng de chinh tham so tren tap val ma khong phai chay lai CRNN).
    nguon: list (views, trong_so, bi_cat[, ung_vien_tinh_san]).
    chi_tiet=True -> them dict(do_tin_cay = P(ma | nguon tot nhat) trung binh qua cac muc le, so_nguon, so_ung_vien)"""
    tham_lam = giai_tham_lam(nguon[0][0][0]) if nguon else ""
    ung = set()
    for ng in nguon:
        ung.update(ng[3] if len(ng) > 3 and ng[3] is not None else ung_vien_nguon(ng[0], so_ung_vien))
    if chu_biet:
        for c in list(ung):
            for k in chu_biet:
                if k != c[:4] and sum(p != q for p, q in zip(k, c[:4])) <= 2 and dung_6346(k + c[4:]):
                    ung.add(k + c[4:])
    if not ung:
        return (tham_lam, False, tham_lam, dict(do_tin_cay=0.0, so_nguon=len(nguon), so_ung_vien=0)) if chi_tiet \
            else (tham_lam, False, tham_lam)
    cs = sorted(ung)
    log_w = np.log(np.array([max(ng[1], 1e-3) for ng in nguon]))
    Ls = np.stack([_logsumexp(log_p_chuoi(ng[0], cs), axis=1) - math.log(len(ng[0])) for ng in nguon], 1)  # K x S
    diem = _logsumexp(Ls + log_w[None, :], axis=1) - _logsumexp(log_w)
    diem = diem + thuong * np.array([c[:4] in chu_biet for c in cs])
    i = int(np.argmax(diem))
    khong_cat = [s for s, ng in enumerate(nguon) if not ng[2]]
    tin = bool(khong_cat) and float(np.max(Ls[i, khong_cat])) >= nguong
    if chi_tiet:
        return cs[i], tin, tham_lam, dict(do_tin_cay=round(float(np.exp(np.max(Ls[i]))), 4), so_nguon=len(nguon),
                                          so_ung_vien=len(cs))
    return cs[i], tin, tham_lam


@torch.no_grad()
def chon_ma_bo_phieu(m, vung):
    """(CACH CU, giu de so sanh) vung: list dict(anh, lop, hop) cua MOT anh (tu nhan that hoac tu bo phat hien).
    Ung vien = moi vung ma_ngang/ma_doc + moi cap (so_huu, seri) cho ma 2 dong; moi ung vien doc voi 3 muc le.
    Chon: chuoi DUNG ISO 6346 duoc nhieu ung vien bau nhat (hoa -> xac suat cao nhat). Khong co -> TU CHOI (tra
    chuoi tham lam de hien thi, danh dau khong hop le -> nhan vien kiem tra tay).
    Tra (ma, hop_le, ma_tham_lam_dau_tien)."""
    ung_vien, tham_lam = [], None
    for d in vung:
        if d["lop"] in ("ma_ngang", "ma_doc"):
            for lp in doc_vung(m, d):
                tham_lam = tham_lam or giai_tham_lam(lp)
                ung_vien.append(giai_rang_buoc(lp))
    sh = [d for d in vung if d["lop"] == "so_huu"]
    se = [d for d in vung if d["lop"] == "seri"]
    for a in sh:
        for b in se:
            for lp1, lp2 in zip(doc_vung(m, a), doc_vung(m, b)):
                tham_lam = tham_lam or (giai_tham_lam(lp1) + giai_tham_lam(lp2))
                ung_vien.append(giai_rang_buoc(np.concatenate([lp1, lp2], 0)))
    hop_le = [u for u in ung_vien if u[1]]
    if hop_le:
        dem = collections.Counter(u[0] for u in hop_le)
        tot = max(hop_le, key=lambda u: (dem[u[0]], u[2] if u[2] is not None else -1e9))
        return tot[0], True, tham_lam or ""
    return tham_lam or "", False, tham_lam or ""


def tai_mo_hinh(duong=None):
    m = CRNN().to(THIET_BI)
    m.load_state_dict(torch.load(duong or os.path.join(RA, "crnn_tot_nhat.pt"), map_location=THIET_BI))
    return m.eval()


def tong_ket(ket_qua, ten):
    """ket_qua: list dict(nhom, nhan, ma, hop_le, tham_lam) -> chi so tong + theo nhom"""
    def chi_so(ds):
        n = max(1, len(ds))
        return {"n": len(ds),
                "dung_tham_lam": round(sum(r["tham_lam"] == r["nhan"] for r in ds) / n, 4),
                "dung_rang_buoc": round(sum(r["hop_le"] and r["ma"] == r["nhan"] for r in ds) / n, 4),   # chi tinh ma DUOC TIN
                "sai_ma_van_qua_ISO": sum(r["hop_le"] and r["ma"] != r["nhan"] for r in ds),
                "tu_choi_chuyen_kiem_tay": sum(not r["hop_le"] for r in ds)}
    tom = chi_so(ket_qua)
    tom["theo_nhom"] = {k: chi_so([r for r in ket_qua if r["nhom"] == k])
                        for k in sorted({r["nhom"] for r in ket_qua})}
    json.dump({"tom_tat": tom, "chi_tiet": ket_qua}, open(os.path.join(RA, ten), "w", encoding="utf-8"), indent=1,
              ensure_ascii=False)
    print("KET_QUA", ten, json.dumps(tom, ensure_ascii=False), flush=True)
    return tom


@torch.no_grad()
def danh_gia_test():
    """350 anh container TEST (giu rieng), dung KHUNG NHAN THAT (do rieng bo doc, chua tinh bo phat hien).
    KHONG dung dap an de chon khung: doc moi khung roi chon theo ISO 6346 + bo phieu nhu luc chay that."""
    m = tai_mo_hinh()
    test = json.load(open(os.path.join(r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat", "danh_sach_container.json"),
                          encoding="utf-8"))
    vung = {}
    for d in cat_that("test"):
        vung.setdefault(os.path.basename(d["anh"]), []).append(d)
    ket_qua = []
    for t in test:
        ma, hop_le, tl = chon_ma(m, vung.get(t["id"], []))
        ket_qua.append(dict(id=t["id"], nhom=t["nhom"], nhan=t["nhan"], ma=ma, hop_le=hop_le, tham_lam=tl))
    return tong_ket(ket_qua, "ket_qua_test_khung_that.json")


if __name__ == "__main__":
    if sys.argv[1:2] == ["thu"]:                   # chay thu nhanh, ghi vao thu muc rieng
        RA = os.path.join(GOC, "runs", "doc_ma_container_thu")
        huan_luyen(1, 500)
        danh_gia_test()
    elif sys.argv[1:2] == ["train"]:
        huan_luyen(int(sys.argv[2]) if len(sys.argv) > 2 else 40)
        danh_gia_test()
    elif sys.argv[1:2] == ["bo_dem"]:
        tao_bo_dem_vung()
    elif sys.argv[1:2] == ["danh_gia"]:
        danh_gia_test()
    elif sys.argv[1:2] == ["mau"]:                 # luu vai anh gia lap de xem
        os.makedirs(os.path.join(RA, "mau"), exist_ok=True)
        for i in range(24):
            img, c = gia_lap()
            img.save(os.path.join(RA, "mau", "%02d_%s.png" % (i, c)))
