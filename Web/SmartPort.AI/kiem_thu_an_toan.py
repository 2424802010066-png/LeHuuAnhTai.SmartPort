# -*- coding: utf-8 -*-
"""
KIEM THU MAY CANH BAO NGUY HIEM (an_toan_cang.py) bang KICH BAN CO DAP AN — khong can GPU / mo hinh.
Camera ao lo kim: anh 1280x720, chan troi y0 = 150, tieu cu f = 900 px, dat cao 6 m. Doi tuong tren mat dat (X, Z met)
chieu len anh dung hinh hoc phoi canh: chan y = y0 + f.6/Z, x = 640 + f.X/Z, cao = f.h/Z. Bo phat hien gia tra khung
dung (co nhieu +-1 px) -> kiem: doi sang met, van toc Kalman, du bao va cham, vung, toc do, nga, do bao ho, chong bao gia.
Chay: python kiem_thu_an_toan.py   (ma thoat 0 = dat het)
"""
import math
import os
import random
import sys
import warnings

warnings.filterwarnings("ignore")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402

import an_toan_cang as atc  # noqa: E402

W, H, Y0, F, HC = 1280, 720, 150.0, 900.0, 6.0
GOC_NHIN_THAT = math.degrees(2 * math.atan(W / 2 / F))
COCO = {"nguoi": 0, "xe-dap": 1, "o-to": 2, "xe-may": 3, "xe-tai": 7}
KICH_THUOC = {"nguoi": (1.7, 0.5), "o-to": (1.5, 4.5), "xe-tai": (3.2, 12.0)}   # (cao, dai) m
ket_qua = []


def kiem(ten, dk, ct=""):
    ket_qua.append(dk)
    print(("  OK  " if dk else "  LOI ") + ten + ("" if dk else "  -> " + str(ct)))


def chieu(X, Z, cao, rong, chan=0.0):
    """chan = do cao mep duoi hop so voi mat dat (m): nguoi ngoi tren xe, hop bi che phan duoi"""
    y = Y0 + F * (HC - chan) / Z
    x = W / 2 + F * X / Z
    h, w = F * cao / Z, F * rong / Z
    return [x - w / 2, y - h, x + w / 2, y]


def chay(kich_ban, giay, fps=10, cau_hinh=None, bao_ho=None, nhieu=1.0, seed=1):
    """kich_ban(t) -> list (lop, X, Z, rong_m (be ngang nhin thay), cao_m). Tra list ket qua tung khung."""
    rnd = random.Random(seed)

    def phat_hien(_img):
        out = []
        for lop, X, Z, rong, cao in kich_ban(t_hien[0]):
            b = chieu(X, Z, cao, rong)
            out.append([c + rnd.uniform(-nhieu, nhieu) for c in b] + [0.9, COCO[lop]])
        return np.array(out, float).reshape(-1, 6)

    t_hien = [0.0]
    ph = atc.PhienAnToan(phat_hien, (lambda img: bao_ho(t_hien[0])) if bao_ho else None, fps=fps)
    ph.cau_hinh(cau_hinh or {"gocNhin": GOC_NHIN_THAT})
    img = np.zeros((H, W, 3), np.uint8)
    ra = []
    for k in range(int(giay * fps)):
        t_hien[0] = k / fps
        ra.append(ph.xu_ly(img, t_hien[0]))
    return ph, ra


def su_kien(ra, loai=None, muc=None):
    return [(r["t"], e) for r in ra for e in r["suKien"] if (loai is None or e["loai"] == loai) and (muc is None or e["muc"] == muc)]


def nguoi_dung(X, Z):
    return ("nguoi", X, Z, 0.5, 1.7)


# ---------------------------------------------------------------------------------------------- 1. hinh hoc
print("== Đổi ảnh sang mét ==")
md = atc.MatDat()
goc = [(-5, 10), (5, 10), (5, 30), (-5, 30)]
anh = []
for X, Z in goc:
    b = chieu(X, Z, 0, 0)
    anh.append([(b[0]) / W, b[3] / H])
md.dat_hieu_chinh({"anh": anh, "met": [list(p) for p in goc]}, W, H)
sai = max(float(np.linalg.norm(md.sang_met(*chieu(X, Z, 0, 0)[2:4]) - np.array([X, Z]))) for X, Z in [(2, 14), (-3, 22), (0, 27)])
kiem("Hiệu chỉnh 4 điểm (homography): sai số đổi ảnh→mét < 1 cm (đo %.4f m)" % sai, sai < 0.01, sai)
p = md.sang_anh(md.sang_met(500, 400))
kiem("Đổi ngược mét→ảnh khớp (vẽ đường dự báo)", abs(p[0] - 500) < 0.5 and abs(p[1] - 400) < 0.5, p)

md2 = atc.MatDat(goc_nhin=GOC_NHIN_THAT)
md2.W, md2.Hh = W, H
for X, Z in [(0, 8), (2, 12), (-3, 18), (1, 25), (4, 35), (-2, 45), (0, 15), (3, 28), (-4, 10), (1, 40)]:
    b = chieu(X, Z, 1.7, 0.5)
    md2.them_mau(b[3], b[3] - b[1], "nguoi", 0.3)
saiZ = max(abs(md2.sang_met(*chieu(X, Z, 0, 0)[2:4])[1] - Z) / Z for X, Z in [(0, 9), (2, 20), (-1, 40)])
saiX = max(abs(md2.sang_met(*chieu(X, Z, 0, 0)[2:4])[0] - X) for X, Z in [(3, 9), (-4, 20), (2, 40)])
kiem("Tự ước tỉ lệ từ chiều cao người (chưa hiệu chỉnh): sai số Z < 3%% (đo %.1f%%), X < 0,2 m (đo %.2f m)" % (saiZ * 100, saiX),
     saiZ < 0.03 and saiX < 0.2, (saiZ, saiX))

# ---------------------------------------------------------------------------------------------- 2. van toc
print("== Vận tốc (Kalman) ==")
ph, ra = chay(lambda t: [("o-to", -15 + 5 * t, 20, 4.5, 1.5), nguoi_dung(6, 9), nguoi_dung(-6, 30)], 6)
v = [o["tocDoKmh"] for o in ra[-1]["doiTuong"] if o["lop"] == "o-to"][0]
kiem("Ô tô chạy ngang 18 km/h → ước %.1f km/h (sai < 10%%)" % v, abs(v - 18) / 18 < 0.10, v)

# ---------------------------------------------------------------------------------------------- 3. va cham
print("== Dự báo va chạm ==")
# xe tai lao THANG toi nguoi (nhin truc dien: be ngang 2,5 m), 6 m/s tu Z=60 (camera thay xe tu xa), nguoi dung Z=14
ph, ra = chay(lambda t: [("xe-tai", 0, 60 - 6 * t, 2.5, 3.2), nguoi_dung(0, 14)], 8)
t_cham = (60 - 14 - (0.35 + 1.25)) / 6
nh = su_kien(ra, "va-cham-xe-nguoi", atc.MUC_NGUY_HIEM)
cb = su_kien(ra, "va-cham-xe-nguoi", atc.MUC_CANH_BAO)
kiem("Xe tải 21,6 km/h lao thẳng tới người: báo NGUY HIỂM trước va chạm ≥ 1,0 s (báo lúc %s s, chạm lúc %.1f s)"
     % (nh[0][0] if nh else None, t_cham), bool(nh) and t_cham - nh[0][0] >= 1.0, nh[:1])
kiem("… và CẢNH BÁO sớm hơn nữa (≥ 2 s trước va chạm)", bool(cb) and t_cham - cb[0][0] >= 2.0, cb[:1])
ttc = nh[0][1]["ttc"] if nh else None
kiem("TTC báo ra khớp thời gian còn lại thật (±0,4 s): TTC %.1f s" % (ttc or -1),
     ttc is not None and abs(ttc - (t_cham - nh[0][0])) <= 0.4, (ttc, t_cham - nh[0][0]) if nh else None)

# cung kich ban nhung GOC NHIN SAI (60 do thay vi 70,8): TTC theo chieu sau van dung
ph, ra = chay(lambda t: [("xe-tai", 0, 60 - 6 * t, 2.5, 3.2), nguoi_dung(0, 14)], 8, cau_hinh={"gocNhin": 60})
nh2 = su_kien(ra, "va-cham-xe-nguoi", atc.MUC_NGUY_HIEM)
kiem("Góc nhìn camera khai sai (60° thay 70,8°): vẫn báo đúng lúc (lệch ≤ 0,3 s so với khai đúng)",
     bool(nh2) and bool(nh) and abs(nh2[0][0] - nh[0][0]) <= 0.3, (nh2[:1], nh[:1]))

# may chu CHAM (GPU ban / mang yeu): chi 2,5 khung/giay -> nguong theo THOI GIAN, van canh bao som
ph, ra = chay(lambda t: [("xe-tai", 0, 60 - 6 * t, 2.5, 3.2), nguoi_dung(0, 14)], 8, fps=2.5)
cb3 = su_kien(ra, "va-cham-xe-nguoi")
kiem("Máy chủ chậm, chỉ 2,5 khung/giây: vẫn báo va chạm ≥ 2 s trước khi chạm (báo lúc %s s)" % (cb3[0][0] if cb3 else None),
     bool(cb3) and t_cham - cb3[0][0] >= 2.0, cb3[:1])

# BI CHE 0,3 s: o to chay ngang truoc mat xe tai -> hop xe tai bi gop / nhay (chan "gan" hon 5 m) 3 khung lien tiep.
# Phep do ngoai lai bi BO QUA (chi du doan) -> vet khong khoi tao lai, khong mat van toc, khong bao toc do vo ly
ph, ra = chay(lambda t: [("xe-tai", 0, 60 - 6 * t - (5.0 if 2.95 <= t < 3.25 else 0.0), 2.5, 3.2), nguoi_dung(0, 14)], 8)
v_sau = [o["tocDoKmh"] for r in ra if 3.3 <= r["t"] <= 3.5 for o in r["doiTuong"] if o["lop"] == "xe-tai"]
nh4 = su_kien(ra, "va-cham-xe-nguoi", atc.MUC_NGUY_HIEM)
kiem("Hộp xe tải nhảy 5 m trong 0,3 s (bị xe khác che): vết giữ vận tốc (%s km/h, thật 21,6), không báo tốc độ vô lý"
     % (v_sau[:1] or None), bool(v_sau) and all(v is not None and abs(v - 21.6) / 21.6 < 0.15 for v in v_sau)
     and not any((e.get("tocDoKmh") or 0) > 40 for _, e in su_kien(ra, "qua-toc-do")), v_sau)
kiem("… vẫn báo NGUY HIỂM va chạm ≥ 1 s trước khi chạm", bool(nh4) and t_cham - nh4[0][0] >= 1.0, nh4[:1])
cap = [(t, e["muc"]) for t, e in su_kien(ra, "va-cham-xe-nguoi")]
t_nh = nh4[0][0] if nh4 else 99
kiem("Đã báo NGUY HIỂM thì 4 s sau đó không báo thêm CẢNH BÁO cùng cặp (nhật ký gọn)",
     not [1 for t, m in cap if m == atc.MUC_CANH_BAO and t_nh < t < t_nh + 4.0], cap)

# xe chay ngang qua, nguoi cach duong xe 8 m -> KHONG bao va cham
ph, ra = chay(lambda t: [("o-to", -25 + 6 * t, 22, 4.5, 1.5), nguoi_dung(0, 14)], 8)
kiem("Ô tô chạy ngang cách người 8 m: không báo va chạm / quá gần",
     not su_kien(ra, "va-cham-xe-nguoi") and not su_kien(ra, "qua-gan-xe"), su_kien(ra)[:2])

# xe dau keo DAI 16 m chay ngang, nguoi dung cach suon xe 4 m: mo hinh doan thang -> khong bao (hinh tron 8 m se bao nham)
ph, ra = chay(lambda t: [("xe-tai", -30 + 5 * t, 20, 16.0, 3.2), nguoi_dung(0, 16)], 10)
kiem("Đầu kéo dài 16 m chạy ngang, người cách sườn xe 4 m: không báo nhầm va chạm (mô hình đoạn thẳng)",
     not su_kien(ra, "va-cham-xe-nguoi", atc.MUC_NGUY_HIEM), su_kien(ra)[:2])

# 2 xe cat ngang: o to chay +X tai Z=20, xe tai chay -Z, gap nhau tai (0, 20) luc t = 4 s
ph, ra = chay(lambda t: [("o-to", -24 + 6 * t, 20, 4.5, 1.5), ("xe-tai", 0, 44 - 6 * t, 2.5, 3.2)], 4.5)
kiem("Hai xe cắt ngang đường nhau: báo va chạm xe – xe trước khi gặp",
     any(e["muc"] == atc.MUC_NGUY_HIEM and t < 3.9 for t, e in su_kien(ra, "va-cham-xe-xe")), su_kien(ra)[:3])

# XE SAP DAM VAT CAN CO DINH: day container xep (chan tren mat dat X -3..3 m, Z 18..20 m) chan ngang duong xe
def vung_mat_dat(ten, loai, goc_met):
    return {"ten": ten, "loai": loai, "diem": [[chieu(X, Z, 0, 0)[2] / W, chieu(X, Z, 0, 0)[3] / H] for X, Z in goc_met]}


day_cont = vung_mat_dat("Dãy cont xếp", "vat-can", [(-3, 18), (3, 18), (3, 20), (-3, 20)])
ch_vc = {"gocNhin": GOC_NHIN_THAT, "vung": [day_cont]}
ph, ra = chay(lambda t: [("xe-tai", 0, 50 - 6 * t, 2.5, 3.2)], 6, cau_hinh=ch_vc)
t_dam = (50 - 20 - 1.25) / 6
vc_nh = su_kien(ra, "va-cham-vat-can", atc.MUC_NGUY_HIEM)
vc_cb = su_kien(ra, "va-cham-vat-can", atc.MUC_CANH_BAO)
kiem("Xe tải 21,6 km/h lao tới dãy container xếp (vùng vật cản): CẢNH BÁO ≥ 2 s và NGUY HIỂM ≥ 1 s trước khi đâm (đâm lúc %.1f s)"
     % t_dam, bool(vc_cb) and t_dam - vc_cb[0][0] >= 2.0 and bool(vc_nh) and t_dam - vc_nh[0][0] >= 1.0, (vc_cb[:1], vc_nh[:1]))
goc_hc = [(-6, 12), (6, 12), (6, 40), (-6, 40)]
hc = {"anh": [[chieu(X, Z, 0, 0)[2] / W, chieu(X, Z, 0, 0)[3] / H] for X, Z in goc_hc], "met": [list(p) for p in goc_hc]}
ph, ra = chay(lambda t: [("xe-tai", -20 + 5 * t, 24, 2.5, 3.2), ("xe-tai", 6, 21.5, 2.5, 3.2)], 8,
              cau_hinh=dict(ch_vc, hieuChinh=hc))
kiem("Xe chạy NGANG song song dãy cont (cách 4 m) + xe đỗ yên sát dãy (đã hiệu chỉnh 4 điểm): không báo vật cản",
     not su_kien(ra, "va-cham-vat-can"), su_kien(ra, "va-cham-vat-can")[:2])
# canh SUY BIEN cho tu uoc ti le: moi xe cung 1 do sau -> khong uoc duoc phoi canh tu mau; nguoi van hanh khai chieu cao
# cot camera (6 m) -> phoi canh s(y) = (y - y0)/h. Truoc day roi ve "nhin thang xuong": Z 24 m bi doi thanh 8,7 m
ph, ra = chay(lambda t: [("xe-tai", -20 + 5 * t, 24, 2.5, 3.2), ("xe-tai", 6, 21.5, 2.5, 3.2)], 2,
              cau_hinh={"gocNhin": GOC_NHIN_THAT, "caoCamera": 6.0})
z_do = [o["viTriM"][1] for o in ra[-1]["doiTuong"] if abs(o["viTriM"][0]) > 3][0]
kiem("Mọi xe cùng một độ sâu (không ước được phối cảnh): dùng chiều cao cột camera khai báo → Z %.1f m (thật 24, sai < 10%%)"
     % z_do, abs(z_do - 24) / 24 < 0.10, z_do)
tren_troi = {"ten": "Vẽ nhầm lên trời", "loai": "vat-can", "diem": [[0.4, 0.05], [0.6, 0.05], [0.6, 0.15], [0.4, 0.15]]}
ph, ra = chay(lambda t: [("xe-tai", 0, 50 - 6 * t, 2.5, 3.2)], 3, cau_hinh={"gocNhin": GOC_NHIN_THAT, "vung": [tren_troi]})
kiem("Vùng vật cản vẽ nhầm trên chân trời (không đổi được sang mặt đất): bỏ qua, không lỗi, không báo",
     not su_kien(ra, "va-cham-vat-can") and len(ra) == 30, len(ra))

# xe dung yen, nguoi di bo ngang qua sat dau xe -> khong bao va cham (xe khong chay)
ph, ra = chay(lambda t: [("xe-tai", 0, 18, 2.5, 3.2), nguoi_dung(-6 + 1.2 * t, 15.8)], 8)
kiem("Xe đỗ yên, người đi bộ ngang qua đầu xe: không báo va chạm (xe không chạy)",
     not su_kien(ra, "va-cham-xe-nguoi") and not su_kien(ra, "qua-gan-xe"), su_kien(ra)[:2])

# ---------------------------------------------------------------------------------------------- 4. toc do + vung
print("== Tốc độ và vùng nguy hiểm ==")
ph, ra = chay(lambda t: [("o-to", -30 + 30 / 3.6 * t, 25, 4.5, 1.5)], 5)
kiem("Ô tô 30 km/h (giới hạn bãi 15): báo QUÁ TỐC ĐỘ mức nguy hiểm",
     bool(su_kien(ra, "qua-toc-do", atc.MUC_NGUY_HIEM)), su_kien(ra)[:2])
ph, ra = chay(lambda t: [("o-to", -10 + 10 / 3.6 * t, 25, 4.5, 1.5)], 5)
kiem("Ô tô 10 km/h: không báo quá tốc độ", not su_kien(ra, "qua-toc-do"), su_kien(ra)[:2])
vung_cham = {"gocNhin": GOC_NHIN_THAT, "vung": [{"ten": "Cổng xuất", "loai": "gioi-han-toc-do", "gioiHan": 5,
                                                   "diem": [[0, 0], [1, 0], [1, 1], [0, 1]]}]}
ph, ra = chay(lambda t: [("o-to", -10 + 10 / 3.6 * t, 25, 4.5, 1.5)], 5, cau_hinh=vung_cham)
e = su_kien(ra, "qua-toc-do")
kiem("Ô tô 10 km/h trong vùng giới hạn 5 km/h: báo quá tốc độ (ghi tên vùng)", bool(e) and e[0][1].get("vung") == "Cổng xuất", e[:1])

# vung cam: hinh chu nhat giua anh (toa do 0..1); nguoi di tu ngoai vao
ch = {"gocNhin": GOC_NHIN_THAT, "vung": [{"ten": "Dưới cần trục", "loai": "cam-nguoi",
                                           "diem": [[0.55, 0.3], [0.9, 0.3], [0.9, 0.9], [0.55, 0.9]]}]}
ph, ra = chay(lambda t: [nguoi_dung(-4 + 1.4 * t, 14)], 8, cau_hinh=ch)
e = su_kien(ra, "vao-vung-cam")
# nguoi vao vung khi x = 640 + 900*X/14 >= 0.55*1280 = 704  ->  X >= 0.996 -> t >= 3.57 s
kiem("Người đi vào vùng cấm 'Dưới cần trục': báo NGUY HIỂM sau khi vào (3,6 s) ≤ 0,5 s",
     bool(e) and 3.5 <= e[0][0] <= 4.1 and e[0][1].get("vung") == "Dưới cần trục", e[:1])
ph, ra = chay(lambda t: [nguoi_dung(-4 + 0.5 * t, 14)], 8, cau_hinh=ch)
kiem("Người đi bên ngoài vùng cấm: không báo", not su_kien(ra, "vao-vung-cam"), su_kien(ra)[:1])

# ---------------------------------------------------------------------------------------------- 5. nga, bao gia
print("== Người ngã và chống báo động giả ==")


def nga(t):
    if t < 2.0:
        return [nguoi_dung(1, 14)]
    return [("nguoi", 1, 14, 1.7, 0.45)]          # nam: be ngang 1,7 m, cao 0,45 m


ph, ra = chay(nga, 5)
e = su_kien(ra, "nguoi-nga")
kiem("Người ngã lúc 2 s và nằm im: báo NGƯỜI NGÃ trong 1,5–2,5 s sau khi ngã", bool(e) and 3.5 <= e[0][0] <= 4.5, e[:1])


def cui(t):
    return [("nguoi", 1, 14, 1.2, 0.9)] if 2.0 <= t < 2.5 else [nguoi_dung(1, 14)]


ph, ra = chay(cui, 5)
kiem("Người cúi nhặt đồ 0,5 s rồi đứng dậy: không báo ngã", not su_kien(ra, "nguoi-nga"), su_kien(ra)[:1])


def bong_ma(t):
    base = [nguoi_dung(0, 14)]
    if abs(t - 2.0) < 0.05:                       # 1 khung duy nhat: phat hien nham 1 xe ngay sat nguoi
        base.append(("xe-tai", 0.5, 15.0, 2.5, 3.2))
    return base


ph, ra = chay(bong_ma, 4)
kiem("Phát hiện nhầm 1 khung (xe 'ma' sát người): không báo động giả", not su_kien(ra), su_kien(ra)[:1])

# su kien khong lap lai lien tuc: xe chay sat nguoi lau -> bao, nghi 4 s roi moi bao lai
ph, ra = chay(lambda t: [("o-to", -12 + 2.5 * t, 15.5, 4.5, 1.5), nguoi_dung(0, 14.4)], 9)
e = su_kien(ra, "qua-gan-xe")
kiem("Báo 'quá gần xe' không lặp mỗi khung (có thời gian nghỉ 4 s): %d lần trong 9 s" % len(e), 1 <= len(e) <= 4, len(e))

# ---------------------------------------------------------------------------------------------- 6. do bao ho
print("== Đồ bảo hộ (bỏ phiếu theo thời gian) ==")


def ppe(khong_mu, khong_ao):
    def f(t):
        b = chieu(0, 12, 1.7, 0.5)
        x1, y1, x2, y2 = b
        h = y2 - y1
        dau = [x1 + 0.3 * (x2 - x1), y1, x2 - 0.3 * (x2 - x1), y1 + 0.15 * h]
        than = [x1, y1 + 0.25 * h, x2, y1 + 0.6 * h]
        return np.array([dau + [0.8, atc.PPE_KHONG_MU if khong_mu else atc.PPE_MU],
                         than + [0.8, atc.PPE_KHONG_AO if khong_ao else atc.PPE_AO]], float)
    return f


ph, ra = chay(lambda t: [nguoi_dung(0, 12)], 3, bao_ho=ppe(True, True))
kiem("Người không mũ, không áo phản quang: báo thiếu mũ + thiếu áo",
     bool(su_kien(ra, "thieu-mu")) and bool(su_kien(ra, "thieu-ao")), su_kien(ra)[:2])
ph, ra = chay(lambda t: [nguoi_dung(0, 12)], 3, bao_ho=ppe(False, False))
kiem("Người đủ mũ + áo phản quang: không báo, nhãn baoHo = có mũ / có áo",
     not su_kien(ra) and ra[-1]["doiTuong"][0]["baoHo"] == {"mu": True, "ao": True}, (su_kien(ra)[:1], ra[-1]["doiTuong"][0].get("baoHo")))

# ---------------------------------------------------------------------------------------------- 7. cong thuc giai tich
print("== Công thức CPA giải tích ==")
ts, dmin, d0 = atc.cpa([0, 0], [0, 0], [0, 10], [0, -5])
kiem("CPA 2 chất điểm: lao thẳng 5 m/s từ 10 m → t* = 2 s, d_min = 0", abs(ts - 2) < 1e-9 and dmin < 1e-9, (ts, dmin))
ts, dmin, d0 = atc.cpa([0, 0], [0, 0], [-10, 3], [5, 0])
kiem("CPA đi ngang lệch 3 m → t* = 2 s, d_min = 3 m", abs(ts - 2) < 1e-9 and abs(dmin - 3) < 1e-9, (ts, dmin))


# ---------------------------------------------------------------------------------------------- 8. hoi quy ra soat doc lap
# Moi kich ban duoi day tai hien 1 loi da kiem chung (loi_xac_nhan.json) — engine CU truot, engine da sua dat.
print("== Hồi quy rà soát độc lập ==")


def chay_moc(kich_ban, moc, fps_khai=8.0, cau_hinh=None, nhieu=1.0, seed=1):
    """nhu chay() nhung khung o THOI DIEM tuy y (may chu cham, khung thua, mat phat hien) va fps KHAI tuy y (giao dien
    luon khai 8). kich_ban(t) -> list (lop, X, Z, rong, cao[, do tin cay[, chan]])"""
    rnd = random.Random(seed)
    t_hien = [0.0]

    def phat_hien(_img):
        out = []
        for it in kich_ban(t_hien[0]):
            lop, X, Z, rong, cao = it[:5]
            b = chieu(X, Z, cao, rong, it[6] if len(it) > 6 else 0.0)
            out.append([c + rnd.uniform(-nhieu, nhieu) for c in b] + [it[5] if len(it) > 5 else 0.9, COCO[lop]])
        return np.array(out, float).reshape(-1, 6)

    ph = atc.PhienAnToan(phat_hien, None, fps=fps_khai)
    ph.cau_hinh(cau_hinh or {"gocNhin": GOC_NHIN_THAT})
    img = np.zeros((H, W, 3), np.uint8)
    ra = []
    for t in moc:
        t_hien[0] = t
        ra.append(ph.xu_ly(img, t))
    return ph, ra


def deu(giay, fps):
    return [k / fps for k in range(int(round(giay * fps)))]


def hieu_chinh(goc, met=None):
    """hieu chinh 4 diem tu hinh chu nhat mat dat goc (X, Z); met = toa do giao dien gui (mac dinh = toa do that)"""
    return {"anh": [[chieu(X, Z, 0, 0)[2] / W, chieu(X, Z, 0, 0)[3] / H] for X, Z in goc], "met": met or [list(p) for p in goc]}


HC_RONG = hieu_chinh([(-12, 10), (12, 10), (12, 60), (-12, 60)])
CH_HC = {"gocNhin": GOC_NHIN_THAT, "hieuChinh": HC_RONG}

# LOI 1: Kalman cat dt <= 1 s -> sau che khuat / khung thua, quang duong vuot 1 s don vao van toc -> qua toc do gia
sai_1a = []
for ch in (None, CH_HC):
    for g in (1.2, 1.6, 2.0):
        def kb(t, g=g):
            ds = [nguoi_dung(8, 9), nguoi_dung(-8, 35)]
            if not (5.0 <= t < 5.0 + g):
                ds.append(("o-to", -20 + 12 / 3.6 * t, 20, 4.5, 1.5))
            return ds
        ph, ra = chay_moc(kb, [k * 0.4 for k in range(35)], 8, ch)
        ma = {o["id"] for r in ra for o in r["doiTuong"] if o["lop"] == "o-to"}
        v_sau = [o["tocDoKmh"] for r in ra if r["t"] >= 5.0 + g for o in r["doiTuong"] if o["lop"] == "o-to"]
        if len(ma) != 1 or not v_sau or any(v is None or not 9 <= v <= 15 for v in v_sau) or su_kien(ra, "qua-toc-do"):
            sai_1a.append((ch is not None, g, sorted(ma), v_sau[:2], su_kien(ra, "qua-toc-do")[:1]))
kiem("Ô tô 12 km/h mất phát hiện 1,2 / 1,6 / 2,0 s (khung 0,4 s, giữ mã vết): tốc độ sau che 9–15 km/h, không báo quá tốc độ",
     not sai_1a, sai_1a[:2])
sai_1b = []
for ch in (None, CH_HC):
    for kc in (1.5, 2.0):
        ph, ra = chay_moc(lambda t: [("xe-tai", 1, 75 - 12 / 3.6 * t, 2.5, 3.2), nguoi_dung(-9, 12), nguoi_dung(9, 40)],
                          [k * kc for k in range(int(18 / kc))], 8, ch)
        v = [o["tocDoKmh"] for r in ra for o in r["doiTuong"] if o["lop"] == "xe-tai" and o["tocDoKmh"] is not None]
        # lan tin dau (3 lan do / 4 s) Kalman con hoi tu tu v = 0 -> co the THAP; loi la uoc CAO -> bao qua toc do gia
        if len(v) < 4 or max(v) > 15 or any(abs(x - 12) > 2 for x in v[-3:]) or su_kien(ra, "qua-toc-do"):
            sai_1b.append((ch is not None, kc, v, su_kien(ra, "qua-toc-do")[:1]))
kiem("Máy chủ chỉ xử lý 1 khung / 1,5–2 s: xe tải 12 km/h không bao giờ ước > 15 km/h, hội tụ 12 ± 2 km/h, không báo quá "
     "tốc độ", not sai_1b, sai_1b[:2])

# LOI 2: va cham CANH BAO (TTC 0, khong tien lai) che mat "qua gan xe" NGUY HIEM -> nguoi cang sat xe cang bao thap
t_nh = {}
for mep in (0.25, 0.55, 1.15):
    ph, ra = chay_moc(lambda t: [("o-to", -15 + 2.5 * t, 14 + mep + 0.9 + 0.35, 4.5, 1.5), nguoi_dung(0, 14)], deu(10, 10), 10)
    nh = [t for t, e in su_kien(ra, muc=atc.MUC_NGUY_HIEM) if e["loai"] in ("qua-gan-xe", "va-cham-xe-nguoi")]
    t_nh[mep] = nh[0] if nh else None
t_toi = (15 - 2.25) / 2.5                     # mui o to toi ngang nguoi
kiem("Người đứng sát (mép xe 0,25 / 0,55 m) ô tô 9 km/h chạy ngang qua: báo NGUY HIỂM trước khi mũi xe tới (%.1f s), "
     "không muộn hơn khi cách 1,15 m (%s)" % (t_toi, t_nh),
     all(t_nh[m] is not None and t_nh[m] < t_toi for m in t_nh) and t_nh[1.15] is not None
     and all(t_nh[m] <= t_nh[1.15] + 0.3 for m in (0.25, 0.55) if t_nh[m] is not None), t_nh)

# LOI 3: da hieu chinh kieu GIAO DIEN (goc gan-trai = [0, 0], Z tinh tu canh gan) -> vung vat can giua camera va canh gan
# bi loai vi "Z <= 0,5" -> khong bao gi bao
hc_gd = hieu_chinh([(-6, 20), (6, 20), (6, 40), (-6, 40)], [[0, 0], [12, 0], [12, 20], [0, 20]])
vc = vung_mat_dat("Dãy cont gần", "vat-can", [(-3, 14), (3, 14), (3, 16), (-3, 16)])
ph, ra = chay_moc(lambda t: [("xe-tai", 0, 50 - 6 * t, 2.5, 3.2)], deu(7, 10), 10,
                  {"gocNhin": GOC_NHIN_THAT, "vung": [vc], "hieuChinh": hc_gd})
t_dam3 = (50 - 16 - 1.25) / 6
cb3 = su_kien(ra, "va-cham-vat-can", atc.MUC_CANH_BAO)
nh3 = su_kien(ra, "va-cham-vat-can", atc.MUC_NGUY_HIEM)
kiem("Hiệu chỉnh kiểu giao diện (gốc gần-trái = 0,0), vùng vật cản nằm giữa camera và cạnh gần: CẢNH BÁO ≥ 2 s và "
     "NGUY HIỂM ≥ 1 s trước khi đâm (%.1f s)" % t_dam3,
     ra[-1]["matDat"] == "homography" and bool(cb3) and t_dam3 - cb3[0][0] >= 2.0 and bool(nh3) and t_dam3 - nh3[0][0] >= 1.0,
     (ra[-1]["matDat"], cb3[:1], nh3[:1]))
ph, ra = chay_moc(lambda t: [("xe-tai", 0, 50 - 6 * t, 2.5, 3.2)], deu(3, 10), 10,
                  {"gocNhin": GOC_NHIN_THAT, "vung": [tren_troi], "hieuChinh": hc_gd})
kiem("… vùng vẽ nhầm trên chân trời (đã hiệu chỉnh): bỏ qua, không lỗi, có canhBaoCauHinh cho người vận hành",
     not su_kien(ra, "va-cham-vat-can") and len(ra) == 30 and bool(ra[-1].get("canhBaoCauHinh")), ra[-1].get("canhBaoCauHinh"))

# LOI 4: luat nga dem >= 3 KHUNG trong 1,5 s va ti_le giu 60 khung -> khung thua / fps cao khong bao gio bao
sai_4 = []
for kc in (0.8, 1.0, 1.2):
    ph, ra = chay_moc(nga, [k * kc for k in range(int(20 / kc))], 8)
    e = su_kien(ra, "nguoi-nga")
    if not e or e[0][0] - 2.0 > 1.5 + 4 * kc:          # 1,5 s nam + toi da 4 khoang khung (kich hoat vet, tin, xac nhan)
        sai_4.append((kc, e[:1]))
kiem("Người ngã, máy chủ chỉ 1 khung / 0,8–1,2 s: vẫn báo NGƯỜI NGÃ (≤ 1,5 s + 4 khoảng khung sau khi ngã)", not sai_4, sai_4)
sai_4b = []
for fps in (45, 60):
    ph, ra = chay_moc(nga, deu(6, fps), fps)
    e = su_kien(ra, "nguoi-nga")
    if not e or not 3.5 <= e[0][0] <= 4.5:
        sai_4b.append((fps, e[:1]))
kiem("Video 45 / 60 khung/giây (hoặc phát 0,25×): báo NGƯỜI NGÃ 1,5–2,5 s sau khi ngã", not sai_4b, sai_4b)

# LOI 5: nguoi NGOI TREN xe may / xe dap (YOLO tra 2 hop) bi bao "qua gan xe" NGUY HIEM lap moi 4 s
sai_5 = []
for lop, rong, cao in (("xe-may", 1.9, 1.2), ("xe-dap", 1.7, 1.1)):
    for chan in (0.0, 0.15, 0.3):
        ph, ra = chay_moc(lambda t: [(lop, -10 + 10 / 3.6 * t, 15, rong, cao),
                                     ("nguoi", -10 + 10 / 3.6 * t, 15, 0.5, 1.6 - chan, 0.9, chan)], deu(7, 10), 10, CH_HC)
        if su_kien(ra):
            sai_5.append((lop, chan, su_kien(ra)[:1]))
kiem("Người ngồi trên xe máy / xe đạp 10 km/h (chân cao 0 / 0,15 / 0,3 m): không báo quá gần / va chạm với chính xe đó",
     not sai_5, sai_5[:2])
ph, ra = chay_moc(lambda t: [("xe-may", -10 + 10 / 3.6 * t, 15, 1.9, 1.2), nguoi_dung(0, 14.5)], deu(7, 10), 10, CH_HC)
kiem("… người đi bộ đứng sát đường xe máy (hộp chồng lên nhau trên ảnh) vẫn bị báo NGUY HIỂM",
     bool([1 for _, e in su_kien(ra, muc=atc.MUC_NGUY_HIEM) if e["loai"] in ("qua-gan-xe", "va-cham-xe-nguoi")]), su_kien(ra)[:2])

# LOI 6: xe chay SONG SONG sat day cont (khong tien lai): nhieu van toc ngang Kalman du "tien lai 1 mm", nua be day no
# ngang ra 2 dau doan -> bao NGUY HIEM TTC 0,1; xe sat hon (d0 <= 0,625 m) lai bi bo qua
day_doc = vung_mat_dat("Dãy cont dọc đường", "vat-can", [(-9, 12), (-5, 12), (-5, 45), (-9, 45)])
sai_6 = []
for ch in ({"gocNhin": GOC_NHIN_THAT, "vung": [day_doc]}, dict(CH_HC, vung=[day_doc])):
    for khe in (0.6, 0.9, 1.2, 1.5, 2.0):
        for seed in (1, 2, 3):
            ph, ra = chay_moc(lambda t: [("xe-tai", -5 + khe + 1.25, 44 - 5 * t, 2.5, 3.2)], deu(6, 10), 10, ch, seed=seed)
            if su_kien(ra, "va-cham-vat-can"):
                sai_6.append(("hc" if "hieuChinh" in ch else "tu-uoc", khe, seed, su_kien(ra, "va-cham-vat-can")[:1]))
kiem("Xe tải chạy song song sát dãy cont 18 km/h (khe 0,6–2,0 m, 3 seed, tự ước + hiệu chỉnh): không báo vật cản",
     not sai_6, sai_6[:2])
ph, ra = chay_moc(lambda t: [("xe-tai", 0, 21.3 - 1.2 * t, 2.5, 3.2)], deu(1.2, 10), 10, dict(ch_vc, hieuChinh=HC_RONG))
nh6 = su_kien(ra, "va-cham-vat-can", atc.MUC_NGUY_HIEM)
kiem("Xe tải bò sát dãy cont (cách 1,3 m, 4,3 km/h) mà vẫn tiến vào: báo NGUY HIỂM trước khi chạm (1,08 s)",
     bool(nh6) and nh6[0][0] < 1.08, nh6[:1])

# LOI 7: 2 xe chay song song cung toc do: TTC = 0 o k = 0 khi d0 <= R + le (khong xet tien lai) -> bao CANH BAO lap
sai_7 = []
for khe in (1.0, 2.0, 3.0, 3.4, 4.0):
    for seed in (1, 2):
        ph, ra = chay_moc(lambda t: [("xe-tai", -(khe / 2 + 1.25), 50 - 3 * t, 2.5, 3.2),
                                     ("xe-tai", khe / 2 + 1.25, 50 - 3 * t, 2.5, 3.2)], deu(8, 10), 10, seed=seed)
        if su_kien(ra, "va-cham-xe-xe"):
            sai_7.append((khe, seed, su_kien(ra, "va-cham-xe-xe")[:1]))
kiem("Hai xe tải chạy song song cùng 11 km/h (khe 1–4 m): không báo va chạm xe – xe", not sai_7, sai_7[:2])

# LOI 8: hieu chinh 4 diem suy bien -> LinAlgError, H suy bien con Hinv = None, phien hong vinh vien
goc_gd = hc_gd["anh"]
sai_8 = []
for ten, hc in (("rộng = 0", dict(hc_gd, met=[[0, 0], [0, 0], [0, 20], [0, 20]])),
                ("3 điểm thẳng hàng", {"anh": [[0.2, 0.9], [0.5, 0.9], [0.8, 0.9], [0.5, 0.5]], "met": hc_gd["met"]}),
                ("bấm chéo thứ tự", {"anh": [goc_gd[0], goc_gd[1], goc_gd[3], goc_gd[2]], "met": hc_gd["met"]})):
    try:
        ph, ra = chay_moc(lambda t: [("xe-tai", 0, 50 - 6 * t, 2.5, 3.2), nguoi_dung(0, 14)], deu(3, 10), 10,
                          {"gocNhin": GOC_NHIN_THAT, "hieuChinh": hc})
        if len(ra) != 30 or ra[-1]["matDat"] == "homography" or not ra[-1].get("canhBaoCauHinh") or ph.mat_dat.H is not None:
            sai_8.append((ten, ra[-1]["matDat"], ra[-1].get("canhBaoCauHinh")))
    except Exception as loi_8:  # noqa: BLE001
        sai_8.append((ten, type(loi_8).__name__, str(loi_8)[:60]))
kiem("Hiệu chỉnh suy biến (rộng = 0 / 3 điểm thẳng hàng / bấm chéo): không lỗi, quay về tự ước, có canhBaoCauHinh",
     not sai_8, sai_8)

# LOI 9: lan khop lai mo hinh mat dat doi < 5% khong chuyen trang thai Kalman -> xe do bi coi la dang chay, NGUY HIEM gia
bo_cuc = [lambda: [("o-to", 2, 20, 4.5, 1.5), nguoi_dung(-3, 12), nguoi_dung(5, 30)],
          lambda: [("xe-tai", -2, 25, 2.5, 3.2), nguoi_dung(3, 12), nguoi_dung(-5, 35)],
          lambda: [("o-to", 0, 20, 4.5, 1.5), nguoi_dung(0, 20 - 1.15 - 0.9 - 0.35)]]
v_do, nh9, vuot9 = 0.0, 0, 0
for kb in bo_cuc:
    for seed in range(30):
        ph, ra = chay_moc(lambda t: kb(), deu(3, 10), 10, None, nhieu=2.0, seed=seed)
        vs = [o["tocDoKmh"] for r in ra for o in r["doiTuong"] if o["lop"] != "nguoi" and o["tocDoKmh"] is not None]
        v_do = max([v_do] + vs)
        vuot9 += bool(vs) and max(vs) > 3.6
        nh9 += len(su_kien(ra, muc=atc.MUC_NGUY_HIEM))
kiem("Cảnh tĩnh, tự ước tỉ lệ, nhiễu ±2 px (3 bố cục × 30 seed): không NGUY HIỂM giả, xe đỗ ước > 3,6 km/h ở ≤ 2/90 lần "
     "(đo: cao nhất %.1f km/h, %d lần vượt, %d NGUY HIỂM)" % (v_do, vuot9, nh9), nh9 == 0 and vuot9 <= 2, (v_do, vuot9, nh9))

# LOI 10: vung gioi han toc do chong nhau -> vung ve SAU thang, vung nghiem hon bi bo qua
cong = {"ten": "Cổng", "loai": "gioi-han-toc-do", "gioiHan": 5, "diem": [[0.3, 0.3], [0.7, 0.3], [0.7, 0.9], [0.3, 0.9]]}
bai = {"ten": "Toàn bãi", "loai": "gioi-han-toc-do", "gioiHan": 10, "diem": [[0, 0], [1, 0], [1, 1], [0, 1]]}
kq10 = []
for vv in ([cong, bai], [bai, cong]):
    ph, ra = chay_moc(lambda t: [("o-to", -1 + 8 / 3.6 * t, 25, 4.5, 1.5)], deu(3, 10), 10, {"gocNhin": GOC_NHIN_THAT, "vung": vv})
    e = su_kien(ra, "qua-toc-do")
    kq10.append((e[0][1].get("gioiHan"), e[0][1].get("vung")) if e else None)
kiem("Vùng 'Cổng' 5 km/h nằm trong 'Toàn bãi' 10 km/h, ô tô 8 km/h: báo theo giới hạn NHỎ nhất, không phụ thuộc thứ tự vẽ",
     kq10 == [(5.0, "Cổng"), (5.0, "Cổng")], kq10)

# LOI 11: o fps THUC cao, tuoi tin van toc / cua so xac nhan bi chan theo SO KHUNG -> co lai con 0,1-0,2 s
so_11 = 0
for seed in range(5):
    def kb(t):
        ds = [nguoi_dung(0, 14)]
        if 2.0 <= t < 2.35:
            ds.append(("xe-tai", 0.8 + 3 * (t - 2.0), 15.6, 2.5, 3.2))
        return ds
    ph, ra = chay_moc(kb, deu(4, 30), 30, seed=seed)
    so_11 += len(su_kien(ra))
kiem("Phát hiện nhầm 1 xe 'ma' 0,35 s sát người ở 30 khung/giây (video phát chậm): không báo động giả (đo %d sự kiện)" % so_11,
     so_11 == 0, so_11)
v_11 = 0.0
for seed in range(5):
    ph, ra = chay_moc(lambda t: [("o-to", 2, 20, 4.5, 1.5), nguoi_dung(-1, 18.5)], deu(3, 40), 40, CH_HC, nhieu=2.0, seed=seed)
    v_11 = max([v_11] + [o["tocDoKmh"] for r in ra for o in r["doiTuong"] if o["lop"] == "o-to" and o["tocDoKmh"] is not None])
kiem("Cảnh tĩnh nhiễu ±2 px ở 40 khung/giây: xe đỗ ước ≤ 3,6 km/h (đo %.1f km/h)" % v_11, v_11 <= 3.6, v_11)

# LOI 12 + 18: lost_track_buffer = int(3.fps) bi supervision nhan them frame_rate/30 -> chi giu vet mat ~0,75 s o fps 8
cb_goc = None
sai_12 = []
for g in (0.0, 0.9, 1.2, 2.0):
    def kb(t, g=g):
        ds = [nguoi_dung(0, 14)]
        if not (2.0 <= t < 2.0 + g):
            ds.append(("xe-tai", 0, 60 - 6 * t, 2.5, 3.2))
        return ds
    ph, ra = chay_moc(kb, deu(8, 8), 8)
    ma = {o["id"] for r in ra for o in r["doiTuong"] if o["lop"] == "xe-tai"}
    cb = su_kien(ra, "va-cham-xe-nguoi", atc.MUC_CANH_BAO)
    if g == 0.0:
        cb_goc = cb[0][0] if cb else None
    elif len(ma) != 1 or not cb or cb_goc is None or cb[0][0] > cb_goc + 0.25:
        sai_12.append((g, sorted(ma), cb[:1]))
kiem("Xe tải lao tới người, mất phát hiện 0,9 / 1,2 / 2,0 s ở 8 khung/giây: giữ nguyên mã vết, CẢNH BÁO va chạm không trễ "
     "quá 0,25 s", cb_goc is not None and not sai_12, (cb_goc, sai_12[:2]))
ph8, _ = chay_moc(lambda t: [nguoi_dung(0, 14)], deu(2, 8), 8)
ph25, _ = chay_moc(lambda t: [nguoi_dung(0, 14)], [k * 0.4 for k in range(6)], 8)
kiem("ByteTrack giữ vết mất 3 s theo fps THỰC: %d khung ở 8 khung/giây, %d khung ở 2,5 khung/giây (khai 8)"
     % (ph8.tracker.max_time_lost, ph25.tracker.max_time_lost),
     ph8.tracker.max_time_lost == 24 and ph25.tracker.max_time_lost in (7, 8), (ph8.tracker.max_time_lost, ph25.tracker.max_time_lost))

# LOI 13: len(deque(maxlen=300)) dung o 300 -> khop lai mo hinh mat dat o MOI mau thay vi 30 mau 1 lan
md13 = atc.MatDat()
md13.W, md13.Hh = W, H
so_khop = [0]
khop_goc = md13._khop


def dem_khop():
    so_khop[0] += 1
    khop_goc()


md13._khop = dem_khop
for i in range(1200):
    b = chieu(0, 8 + (i * 7) % 40, 1.7, 0.5)
    md13.them_mau(b[3], b[3] - b[1], "nguoi", 0.3)
kiem("Mô hình mặt đất khớp lại thưa khi đủ mẫu: %d lần sau 1200 mẫu (≤ 60, mỗi 30 mẫu)" % so_khop[0], so_khop[0] <= 60, so_khop[0])

# LOI 14: lan_bao khong bao gio duoc don -> phien camera 24/7 phinh bo nho theo so doi tuong moi
ph14 = atc.PhienAnToan(lambda img: np.zeros((0, 6)), None, fps=10)
ph14.fps_thuc = 10.0
for i in range(600):
    for k in range(5):
        ph14._xac_nhan([dict(loai="thieu-mu", muc=atc.MUC_CANH_BAO, ids=(i + 1,), diem=[(0, 0)])], i * 0.5 + k * 0.1)
kiem("Camera chạy lâu, 600 người mới lần lượt thiếu mũ: báo đủ 600 lần, bộ nhớ chống báo trùng gọn (%d khóa ≤ 50)"
     % len(ph14.lan_bao), ph14.dem["thieu-mu"] == 600 and len(ph14.lan_bao) <= 50, (dict(ph14.dem), len(ph14.lan_bao)))

# LOI 17 (hop dong NGUONG_PHAT_HIEN): bo phat hien chay nguong thap de ByteTrack ghep hop diem thap khi bi che; engine
# tu dam bao hop diem thap KHONG tao vet moi, KHONG lam mau tu uoc ti le
nguong_thap = getattr(atc, "NGUONG_PHAT_HIEN", None)
kiem("NGUONG_PHAT_HIEN (%s) thấp hơn ngưỡng kích hoạt vết của ByteTrack (%s)" % (nguong_thap, getattr(atc, "NGUONG_KICH_HOAT", None)),
     nguong_thap is not None and nguong_thap < getattr(atc, "NGUONG_KICH_HOAT", 0) <= 0.3, nguong_thap)


def che_than_duoi(t):
    # 2-4 s: nguoi dung sau mui xe, chi thay tu 0,5 m tro len, diem YOLO tut con 0,2
    if 2.0 <= t < 4.0:
        return [("nguoi", 0, 14, 0.5, 1.2, 0.2, 0.5), nguoi_dung(-6, 9), nguoi_dung(6, 30)]
    return [nguoi_dung(0, 14), nguoi_dung(-6, 9), nguoi_dung(6, 30)]


ph_a, _ = chay_moc(che_than_duoi, deu(1.95, 10), 10)
ph_b, ra_b = chay_moc(che_than_duoi, deu(4.0, 10), 10)
mau_them = len(ph_b.mat_dat.mau) - len(ph_a.mat_dat.mau)
ma_17 = {o["id"] for r in ra_b for o in r["doiTuong"]}
co_mat = all(any(abs(o["hop"][0] + o["hop"][2] - W) < 40 for o in r["doiTuong"]) for r in ra_b)
kiem("Người bị che thân dưới 2 s, điểm tụt 0,2: giữ mã vết + vẫn có trong doiTuong; hộp điểm thấp không làm mẫu tự ước "
     "(thêm %d mẫu, chỉ 2 người kia = 40)" % mau_them, mau_them == 40 and len(ma_17) == 3 and co_mat, (mau_them, ma_17, co_mat))
ph, ra = chay_moc(lambda t: [nguoi_dung(0, 14), ("nguoi", 3, 20, 0.5, 1.7, 0.2)], deu(3, 10), 10)
kiem("Bóng ma chỉ có điểm thấp (0,2): không tạo vết mới", max(len(r["doiTuong"]) for r in ra) == 1,
     max(len(r["doiTuong"]) for r in ra))

# ---------------------------------------------------------------------------------------------- 9. kiem lai doc lap
print("== Kiểm lại độc lập (nhóm engine) ==")

# THOI GIAN LUI (tua video / client phat lai tu dau) ve TRUOC luc vet bat dau: tin van toc theo t0 -> nguoi dung yen bi coi
# "chua du tuoi" toi khi t vuot lai t0 cu (20 s) -> mat bao va cham. Lui > 1 s: bo trang thai theo thoi gian, hoc lai
tua = {"pha": 0, "t": -1.0}


def kb_tua(t):
    if t < tua["t"]:
        tua["pha"] = 1
    tua["t"] = t
    ds = [nguoi_dung(0, 14), nguoi_dung(-8, 30)]
    tx = t - 22 if tua["pha"] == 0 else t           # pha 0: xe tai toi luc 22-30 s; sau khi tua ve 0: toi luc 0-8 s
    if 0 <= tx < 8:
        ds.append(("xe-tai", 0, 60 - 6 * tx, 2.5, 3.2))
    return ds


ph, ra = chay_moc(kb_tua, [k / 10 for k in range(200, 300)] + [k / 10 for k in range(90)], 10)
t_cham_tua = (60 - 14 - 1.6) / 6
cb_t0 = su_kien(ra[:100], "va-cham-xe-nguoi", atc.MUC_CANH_BAO)
cb_t1 = su_kien(ra[100:], "va-cham-xe-nguoi", atc.MUC_CANH_BAO)
nh_t1 = su_kien(ra[100:], "va-cham-xe-nguoi", atc.MUC_NGUY_HIEM)
kiem("Tua video về 0 (trước lúc vết người bắt đầu, 20 s): xe tải lao tới lại vẫn CẢNH BÁO ≥ 2 s và NGUY HIỂM ≥ 1 s trước "
     "khi chạm (%.1f s)" % t_cham_tua,
     bool(cb_t0) and bool(cb_t1) and t_cham_tua - cb_t1[0][0] >= 2.0 and bool(nh_t1) and t_cham_tua - nh_t1[0][0] >= 1.0,
     (cb_t0[:1], cb_t1[:1], nh_t1[:1]))

# TOI UU mo phong (bo khi khong the toi R + le trong T giay; dung khi khoang cach tang lai — ham loi) phai cho KET QUA Y HET
# mo phong day du 31 buoc (doi chieu voi ban vet can ngay tai day)
def vc_vet_can(doan_a, va, doan_b, vb, R, le, T=3.0, buoc=0.1, tiep_can=0.3):
    (a1, a2), (b1, b2) = doan_a, doan_b
    dv = np.asarray(vb) - np.asarray(va)
    if atc.kc_doan_doan(a1, a2, b1, b2) - atc.kc_doan_doan(a1, a2, b1 + dv * buoc, b2 + dv * buoc) <= tiep_can * buoc:
        return math.inf, math.inf
    r = rl = math.inf
    for k in range(int(round(T / buoc)) + 1):
        d = atc.kc_doan_doan(a1, a2, b1 + dv * k * buoc, b2 + dv * k * buoc)
        if d <= R + le and rl == math.inf:
            rl = k * buoc
        if d <= R:
            r = k * buoc
            break
    return r, rl


def vat_can_vet_can(doan, v, P, R, le, T=3.0, buoc=0.1, tiep_can=0.3):
    a1, a2 = np.asarray(doan[0], float), np.asarray(doan[1], float)
    d0 = atc.kc_doan_da_giac(a1, a2, P)
    if d0 - atc.kc_doan_da_giac(a1 + v * buoc, a2 + v * buoc, P) <= tiep_can * buoc:
        return math.inf, math.inf, d0
    r = rl = math.inf
    for k in range(int(round(T / buoc)) + 1):
        d = atc.kc_doan_da_giac(a1 + v * k * buoc, a2 + v * k * buoc, P)
        if d <= R + le and rl == math.inf:
            rl = k * buoc
        if d <= R:
            r = k * buoc
            break
    return r, rl, d0


rnd9 = random.Random(9)
lech9 = []
for i in range(3000):
    def p9(s=20.0):
        return np.array([rnd9.uniform(-s, s), rnd9.uniform(-s, s)])
    a1 = p9()
    a2 = a1 + (p9(8) if rnd9.random() < 0.7 else 0)
    b1 = p9() if rnd9.random() < 0.7 else a1 + p9(3)
    b2 = b1 + (p9(8) if rnd9.random() < 0.4 else 0)
    va, vb = p9(8), p9(8)
    R, le, T = rnd9.uniform(0.5, 3), rnd9.choice([0, 1, 2]), rnd9.choice([1.5, 3.0, 5.0])
    x = atc.du_bao_va_cham((a1, a2), va, (b1, b2), vb, R, le, T=T)[:2]
    if x != vc_vet_can((a1, a2), va, (b1, b2), vb, R, le, T=T):
        lech9.append(("va-cham", i))
    P = np.array([p9(4) + np.array([5.0, 5.0]) for _ in range(rnd9.randint(3, 7))])     # co the khong loi / tu cat
    d9 = (p9(25), None)
    d9 = (d9[0], d9[0] + p9(6))
    v9 = p9(8)
    if atc.du_bao_dam_vat_can(d9, v9, P, R, le) != vat_can_vet_can(d9, v9, P, R, le):
        lech9.append(("vat-can", i))
kiem("Tối ưu mô phỏng (bỏ khi không thể tới, dừng khi khoảng cách tăng lại): TTC y hệt mô phỏng đủ 31 bước (6000 ca ngẫu "
     "nhiên, lệch %d)" % len(lech9), not lech9, lech9[:3])

# dau thoi gian sai (buoc 1e-6 s -> fps THUC 1 trieu): ByteTrack khong duoc giu vet mat vo han
ph, _ = chay_moc(lambda t: [nguoi_dung(0, 14)], [k * 1e-6 for k in range(15)], 8)
kiem("Dấu thời gian sai (bước 1e-6 s): ByteTrack giữ vết mất có trần (%d khung ≤ 360)" % ph.tracker.max_time_lost,
     1 <= ph.tracker.max_time_lost <= 360, ph.tracker.max_time_lost)
ph.nguong["ti_le_xac_nhan"] = 1.5
ph.fps_thuc = 10.0
N9, K9 = ph._cua_so()
kiem("Ngưỡng xác nhận > 100%% không tắt hết cảnh báo: cần %d / %d khung" % (K9, N9), 2 <= K9 <= N9, (K9, N9))
ph.cau_hinh({"nguong": {"ti_le_xac_nhan": 5, "tuoi_tin_giay": 100, "cua_so_giay": -1, "tiep_can": 99,
                        "ttc_canh_bao": 2.5}})
kep9 = {k: ph.nguong[k] for k in ("ti_le_xac_nhan", "tuoi_tin_giay", "cua_so_giay", "tiep_can", "ttc_canh_bao")}
kiem("Ngưỡng thời gian vô lý (xác nhận 500%%, tin vận tốc sau 100 s...) bị kẹp về khoảng hợp lệ, ngưỡng khác giữ nguyên: %s"
     % kep9, kep9 == {"ti_le_xac_nhan": 1.0, "tuoi_tin_giay": 3.0, "cua_so_giay": 0.1, "tiep_can": 5.0, "ttc_canh_bao": 2.5},
     kep9)

so_dat = sum(ket_qua)
print("\nKET QUA: %s %d/%d" % ("DAT" if so_dat == len(ket_qua) else "%d LOI /" % (len(ket_qua) - so_dat), so_dat, len(ket_qua)))
sys.exit(0 if so_dat == len(ket_qua) else 1)
