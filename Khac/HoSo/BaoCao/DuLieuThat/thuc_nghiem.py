# -*- coding: utf-8 -*-
"""THUC NGHIEM: du bao san luong container thong qua cang bien Viet Nam (nghin TEU/thang).

Mo hinh chinh = dung kien truc AI Lab cua SmartPort (ailab.js):
    cua so 3 thang -> Dense(16, ReLU) -> Dense(8, ReLU) -> Dense(1), Adam lr=0.01, batch 8, 200 epoch, MSE,
    chuan hoa Min-Max.
Doi chung: Naive (y_t = y_t-1), Seasonal naive (y_t = y_t-12), Hoi quy tuyen tinh theo thoi gian (ForecastService.cs),
           Hoi quy tuyen tinh tren 3 do tre (AR(3)).
Cai tien (chon tren tap validation 2024): them dac trung Tet / do tre 12 thang / du bao ty le tang truong.

Chia theo thoi gian: train 1/2021-12/2024, test 1/2025-6/2026. Validation (chon bien the) = 2024 khi train 2021-2023.
"""
import csv
import json
import math
import os
import sys
import warnings
from datetime import date, timedelta

import numpy as np
from sklearn.exceptions import ConvergenceWarning
from sklearn.linear_model import LinearRegression
from sklearn.neural_network import MLPRegressor

warnings.filterwarnings("ignore", category=ConvergenceWarning)
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SP = os.path.dirname(os.path.abspath(__file__))   # doc container_vn_thang.csv cung thu muc
SO_SEED = 20

# ------------------------------------------------------------------ du lieu
bang = list(csv.DictReader(open(os.path.join(SP, "container_vn_thang.csv"), encoding="utf-8-sig")))
ky = [r["ky"] for r in bang]
y = np.array([float(r["CONT_TEU"]) for r in bang])
thang = np.array([int(r["thang"]) for r in bang])
nam = np.array([int(r["nam"]) for r in bang])
N = len(y)
assert N == 66, N

# Ngay mung 1 Tet Nguyen dan
TET = {2020: date(2020, 1, 25), 2021: date(2021, 2, 12), 2022: date(2022, 2, 1), 2023: date(2023, 1, 22),
       2024: date(2024, 2, 10), 2025: date(2025, 1, 29), 2026: date(2026, 2, 17), 2027: date(2027, 2, 6)}


def he_so_tet(nm, th):
    """Ty le so ngay cua 'giai doan Tet' (7 ngay truoc -> 10 ngay sau mung 1) roi vao thang (nm, th)."""
    t = TET[nm]
    ngay = [t + timedelta(days=k) for k in range(-7, 11)]
    return sum(1 for d in ngay if d.year == nm and d.month == th) / len(ngay)


def so_ngay_thang(nm, th):
    d1 = date(nm, th, 1)
    d2 = date(nm + (th == 12), th % 12 + 1, 1)
    return (d2 - d1).days


def ky_sau(nm, th, k=1):
    t = (nm * 12 + th - 1) + k
    return t // 12, t % 12 + 1


tet = np.array([he_so_tet(a, b) for a, b in zip(nam, thang)])
i_test0 = ky.index("2025-01")      # 48
i_val0 = ky.index("2024-01")       # 36


# ------------------------------------------------------------------ do do
def do_do(thuc, du):
    thuc, du = np.asarray(thuc, float), np.asarray(du, float)
    e = du - thuc
    return {"MAE": float(np.mean(np.abs(e))), "RMSE": float(np.sqrt(np.mean(e ** 2))),
            "MAPE": float(np.mean(np.abs(e) / thuc) * 100)}


# ------------------------------------------------------------------ bien the dac trung
# Moi bien the: ham tao vector dau vao cho thoi diem t (dung chuoi 'lich_su' da biet toi t-1)
BIEN_THE = {
    "AI Lab (3 độ trễ)": {"tre": [3, 2, 1], "tet": False, "tre12": False, "muc_tieu": "muc"},
    "3 độ trễ + Tết": {"tre": [3, 2, 1], "tet": True, "tre12": False, "muc_tieu": "muc"},
    "3 độ trễ + trễ 12 + Tết": {"tre": [3, 2, 1], "tet": True, "tre12": True, "muc_tieu": "muc"},
    "Tăng trưởng so cùng kỳ + Tết": {"tre": [3, 2, 1], "tet": True, "tre12": True, "muc_tieu": "ty_le_12"},
}


def vector(cfg, ls, t, nm, th, lo, hi):
    """ls: mang lich su (co gia tri toi t-1). lo/hi: min-max tren tap train."""
    chuan = lambda v: (v - lo) / (hi - lo)
    x = [chuan(ls[t - k]) for k in cfg["tre"]]
    if cfg["tre12"]:
        x.append(chuan(ls[t - 12]))
    if cfg["tet"]:
        x.append(he_so_tet(nm, th))
    return x


def muc_tieu(cfg, ls, t, lo, hi):
    if cfg["muc_tieu"] == "muc":
        return (ls[t] - lo) / (hi - lo)
    return ls[t] / ls[t - 12] - 1.0          # ty le tang truong so cung ky


def tu_muc_tieu(cfg, gia_tri, ls, t, lo, hi):
    if cfg["muc_tieu"] == "muc":
        return gia_tri * (hi - lo) + lo
    return ls[t - 12] * (1.0 + gia_tri)


def bat_dau(cfg):
    return 12 if (cfg["tre12"] or cfg["muc_tieu"] == "ty_le_12") else max(cfg["tre"])


def huan_luyen(cfg, ls, t_dau, t_cuoi, seed, epoch=200):
    """Huan luyen tren cac muc tieu t thuoc [bat_dau, t_cuoi). Min-max tinh CHI tren doan train."""
    lo, hi = float(np.min(ls[:t_cuoi])), float(np.max(ls[:t_cuoi]))
    X, Y = [], []
    for t in range(max(t_dau, bat_dau(cfg)), t_cuoi):
        X.append(vector(cfg, ls, t, nam_all[t], thang_all[t], lo, hi))
        Y.append(muc_tieu(cfg, ls, t, lo, hi))
    X, Y = np.array(X), np.array(Y)
    m = MLPRegressor(hidden_layer_sizes=(16, 8), activation="relu", solver="adam", learning_rate_init=0.01,
                     batch_size=8, max_iter=epoch, shuffle=True, alpha=0.0, tol=0.0, n_iter_no_change=10 ** 6,
                     random_state=seed)
    m.fit(X, Y)
    return m, lo, hi, X.shape


# lich/nam/thang mo rong them 12 thang de du bao tuong lai
nam_all = list(nam) + [ky_sau(2026, 6, k)[0] for k in range(1, 13)]
thang_all = list(thang) + [ky_sau(2026, 6, k)[1] for k in range(1, 13)]


def du_bao_1_buoc(cfg, m, ls, t_dau, t_cuoi, lo, hi):
    out = []
    for t in range(t_dau, t_cuoi):
        x = np.array([vector(cfg, ls, t, nam_all[t], thang_all[t], lo, hi)])
        out.append(tu_muc_tieu(cfg, float(m.predict(x)[0]), ls, t, lo, hi))
    return np.array(out)


def du_bao_cuon(cfg, m, ls, t_dau, so_buoc, lo, hi):
    """Du bao nhieu buoc: gia tri du bao duoc dua nguoc lai lam dau vao (nhu ailab.js)."""
    ls2 = list(ls[:t_dau]) + [np.nan] * so_buoc
    for t in range(t_dau, t_dau + so_buoc):
        x = np.array([vector(cfg, ls2, t, nam_all[t], thang_all[t], lo, hi)])
        ls2[t] = tu_muc_tieu(cfg, float(m.predict(x)[0]), ls2, t, lo, hi)
    return np.array(ls2[t_dau:t_dau + so_buoc])


# ================================================================ 1. CHON BIEN THE TREN VALIDATION 2024
print("=== CHON BIEN THE (train 2021-2023, validation 2024, du bao 1 buoc) ===")
ket_qua_val = {}
for ten, cfg in BIEN_THE.items():
    mae = []
    for s in range(SO_SEED):
        m, lo, hi, _ = huan_luyen(cfg, y, 0, i_val0, s)
        du = du_bao_1_buoc(cfg, m, y, i_val0, i_test0, lo, hi)
        mae.append(do_do(y[i_val0:i_test0], du)["MAE"])
    ket_qua_val[ten] = (float(np.mean(mae)), float(np.std(mae)))
    print(f"  {ten:32} MAE validation = {np.mean(mae):7.1f} ± {np.std(mae):5.1f} nghin TEU")
ten_tot = min((k for k in BIEN_THE if k != "AI Lab (3 độ trễ)"), key=lambda k: ket_qua_val[k][0])
print("  -> bien the cai tien duoc chon:", ten_tot)

# ================================================================ 2. DANH GIA TREN TAP TEST
print("\n=== TAP TEST 1/2025 - 6/2026 (train 1/2021 - 12/2024) ===")
thuc_test = y[i_test0:]
ket_qua = {}
du_doan = {}

# Naive & seasonal naive (1 buoc / 12 buoc)
du_doan["Naive (tháng trước)"] = y[i_test0 - 1:N - 1]
du_doan["Naive theo mùa (cùng kỳ năm trước)"] = y[i_test0 - 12:N - 12]
# Hoi quy tuyen tinh theo thoi gian (ForecastService.cs): fit tren train, du bao nhieu buoc
x_tr = np.arange(i_test0)
a, b = np.polyfit(x_tr, y[:i_test0], 1)
du_doan["Hồi quy tuyến tính theo thời gian (ForecastService)"] = a * np.arange(i_test0, N) + b
# AR(3) tuyen tinh
Xl = np.array([[y[t - 3], y[t - 2], y[t - 1]] for t in range(3, i_test0)])
lr = LinearRegression().fit(Xl, y[3:i_test0])
du_doan["Hồi quy tuyến tính trên 3 độ trễ (AR(3))"] = lr.predict(np.array([[y[t - 3], y[t - 2], y[t - 1]] for t in range(i_test0, N)]))
for k, v in du_doan.items():
    ket_qua[k] = do_do(thuc_test, v)

chi_tiet_seed = {}
loss_curves = {}
cuon = {}
for ten in ["AI Lab (3 độ trễ)", ten_tot]:
    cfg = BIEN_THE[ten]
    ds_1b, ds_cuon, mets, mets_cuon = [], [], [], []
    for s in range(SO_SEED):
        m, lo, hi, shape = huan_luyen(cfg, y, 0, i_test0, s)
        p1 = du_bao_1_buoc(cfg, m, y, i_test0, N, lo, hi)
        pc = du_bao_cuon(cfg, m, y, i_test0, N - i_test0, lo, hi)
        ds_1b.append(p1)
        ds_cuon.append(pc)
        mets.append(do_do(thuc_test, p1))
        mets_cuon.append(do_do(thuc_test, pc))
        if ten == "AI Lab (3 độ trễ)":
            loss_curves[s] = [2 * v for v in m.loss_curve_]      # sklearn loss = MSE/2
            if s == 0:
                print(f"  kich thuoc X_train = {shape}, so tham so = {sum(w.size for w in m.coefs_) + sum(b_.size for b_ in m.intercepts_)}")
    ds_1b, ds_cuon = np.array(ds_1b), np.array(ds_cuon)
    nhan = f"MLP – {ten}"
    du_doan[nhan] = ds_1b.mean(axis=0)
    cuon[nhan] = ds_cuon.mean(axis=0)
    chi_tiet_seed[nhan] = {
        "MAE": [x["MAE"] for x in mets], "RMSE": [x["RMSE"] for x in mets], "MAPE": [x["MAPE"] for x in mets],
        "MAE_cuon": [x["MAE"] for x in mets_cuon], "MAPE_cuon": [x["MAPE"] for x in mets_cuon],
    }
    ket_qua[nhan] = {k: float(np.mean([x[k] for x in mets])) for k in ("MAE", "RMSE", "MAPE")}
    ket_qua[nhan].update({k + "_std": float(np.std([x[k] for x in mets])) for k in ("MAE", "RMSE", "MAPE")})
    ket_qua[nhan + " (dự báo cuộn 18 tháng)"] = {k: float(np.mean([x[k] for x in mets_cuon])) for k in ("MAE", "RMSE", "MAPE")}

for k, v in ket_qua.items():
    them = f" (± {v['MAE_std']:.1f})" if "MAE_std" in v else ""
    print(f"  {k:62} MAE {v['MAE']:7.1f}{them}  RMSE {v['RMSE']:7.1f}  MAPE {v['MAPE']:5.2f}%")

# ================================================================ 3. DU BAO 6 THANG TOI (7-12/2026)
print("\n=== DU BAO 7/2026 - 12/2026 (huan luyen tren toan bo 66 thang) ===")
tuong_lai = {}
for ten in ["AI Lab (3 độ trễ)", ten_tot]:
    cfg = BIEN_THE[ten]
    ds = []
    for s in range(SO_SEED):
        m, lo, hi, _ = huan_luyen(cfg, y, 0, N, s)
        ds.append(du_bao_cuon(cfg, m, y, N, 6, lo, hi))
    ds = np.array(ds)
    tuong_lai[f"MLP – {ten}"] = {"tb": ds.mean(axis=0).tolist(), "p10": np.percentile(ds, 10, axis=0).tolist(),
                                 "p90": np.percentile(ds, 90, axis=0).tolist()}
a2, b2 = np.polyfit(np.arange(N), y, 1)
tuong_lai["Hồi quy tuyến tính theo thời gian (ForecastService)"] = {"tb": (a2 * np.arange(N, N + 6) + b2).tolist()}
nhan_tl = [f"{ky_sau(2026, 6, k)[1]:02d}/{ky_sau(2026, 6, k)[0]}" for k in range(1, 7)]
for k, v in tuong_lai.items():
    print(f"  {k:52}", " ".join(f"{x:7.0f}" for x in v["tb"]))
tong_2026 = float(y[i_test0 + 12:].sum())
print("  6 thang dau 2026 thuc te:", tong_2026)

json.dump({
    "ky": ky, "y": y.tolist(), "tet": tet.tolist(), "i_test0": i_test0, "i_val0": i_val0,
    "ket_qua_val": ket_qua_val, "ten_tot": ten_tot, "ket_qua": ket_qua, "du_doan": {k: list(map(float, v)) for k, v in du_doan.items()},
    "cuon": {k: list(map(float, v)) for k, v in cuon.items()}, "chi_tiet_seed": chi_tiet_seed,
    "loss_curves": loss_curves, "tuong_lai": tuong_lai, "nhan_tuong_lai": nhan_tl,
    "hoi_quy_train": {"a": float(a), "b": float(b)}, "hoi_quy_all": {"a": float(a2), "b": float(b2)},
    "ar3": {"he_so": lr.coef_.tolist(), "chan": float(lr.intercept_)},
}, open(os.path.join(SP, "ket_qua_thuc_nghiem.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n-> ket_qua_thuc_nghiem.json")
