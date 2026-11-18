# -*- coding: utf-8 -*-
"""KIEM THU DAU-CUOI may canh bao nguy hiem: chay video kiem thu QUA CONG API (nhu trinh duyet) — moi khung ->
/api/ai-thi-giac/an-toan/khung, so voi DAP AN (dap_an_an_toan.json, tao_video_an_toan.py) + ghi video ket qua co ve lop
canh bao (de xem / demo). Can he thong dang chay (SMARTPORT.bat) va tai khoan kiem thu (.bimat/tai-khoan-kiem-thu.json,
tu tao khi chay nhom kiem thu microservices).
Ma thoat: 0 dat het · 1 co dieu kien LOI / goi API loi · 2 KHONG kiem duoc (thieu video / dap an / tai khoan, khong doc
duoc khung nao) — thieu du lieu KHONG bao gio duoc tinh la dat.
Chay: python kiem_thu_an_toan_video.py [video] [dap_an.json] [video_ket_qua.webm]   (mac dinh: SmartPort_DuLieuThat)"""
import base64
import json
import os
import sys
import time
import urllib.request
import uuid

import cv2
import numpy as np

WEB = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))           # Web (SmartPort.AI nam trong Web)
G = "http://localhost:5100"
_KT = os.path.join(os.environ.get("SMARTPORT_DULIEU_DIR", os.path.join(os.path.expanduser("~"), "Downloads", "SmartPort_DuLieuThat")),
                   "video_kiem_thu")
V = sys.argv[1] if len(sys.argv) > 1 else os.path.join(_KT, "video_an_toan.webm")
_DA = sys.argv[2] if len(sys.argv) > 2 else os.path.join(_KT, "dap_an_an_toan.json")
RA = sys.argv[3] if len(sys.argv) > 3 else os.path.join(_KT, "an_toan_ket_qua.webm")
_TK = os.path.join(WEB, "MicroServices", ".bimat", "tai-khoan-kiem-thu.json")
KHOA_DA = ("gocNhin", "vung", "t_xe_toi_sat_nguoi", "t_b_vao_vung")


def loi(noi_dung, ma=2):
    """in dong '  LOI ' (SMARTPORT.bat loc dong nay) roi thoat ma khac 0 — khong kiem duoc thi KHONG duoc coi la dat"""
    print("  LOI " + noi_dung, flush=True)
    sys.exit(ma)


def goi(duong, du_lieu=None, token=None):
    yc = urllib.request.Request(G + duong, data=None if du_lieu is None else json.dumps(du_lieu).encode(),
                                headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
    with urllib.request.urlopen(yc, timeout=60) as r:
        return json.loads(r.read())


MAU = {"nguy-hiem": (38, 38, 220), "canh-bao": (11, 158, 245)}


def ve(img, kq, vung):
    H, W = img.shape[:2]
    for z in vung or []:
        pts = np.array([[x * W, y * H] for x, y in z["diem"]], np.int32)
        lop = img.copy()
        cv2.fillPoly(lop, [pts], (40, 40, 200))
        img[:] = cv2.addWeighted(lop, 0.25, img, 0.75, 0)
        cv2.polylines(img, [pts], True, (40, 40, 220), 2)
    for o in kq["doiTuong"]:
        x1, y1, x2, y2 = map(int, o["hop"])
        mau = (38, 38, 220) if o["nguyHiem"] else ((94, 197, 34) if o["lop"] == "nguoi" else (248, 189, 56))
        cv2.rectangle(img, (x1, y1), (x2, y2), mau, 3 if o["nguyHiem"] else 2)
        chu = "#%d %s%s" % (o["id"], "nguoi" if o["lop"] == "nguoi" else o["lop"], "" if o["tocDoKmh"] is None else " %.0fkm/h" % o["tocDoKmh"])
        cv2.putText(img, chu, (x1, max(14, y1 - 6)), cv2.FONT_HERSHEY_SIMPLEX, 0.55, mau, 2)
        if o.get("baoHo") and o["baoHo"].get("mu") is False:
            cv2.putText(img, "KHONG MU", (x1, y2 + 18), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (38, 38, 220), 2)
        if o.get("duBao"):
            cv2.polylines(img, [np.array(o["duBao"], np.int32)], False, mau, 2)
    for r in kq["ruiRo"]:
        if len(r["diem"]) == 2:
            (ax, ay), (bx, by) = r["diem"]
            cv2.line(img, (int(ax), int(ay)), (int(bx), int(by)), MAU[r["muc"]], 3)
            if r.get("ttc") is not None:
                cv2.putText(img, "TTC %.1fs" % r["ttc"], (int((ax + bx) / 2), int((ay + by) / 2)), cv2.FONT_HERSHEY_SIMPLEX, 0.7, MAU[r["muc"]], 2)
    y = 26
    for r in kq["ruiRo"][:4]:
        cv2.putText(img, ("NGUY HIEM: " if r["muc"] == "nguy-hiem" else "CANH BAO: ") + r["loai"], (10, y), cv2.FONT_HERSHEY_SIMPLEX, 0.7, MAU[r["muc"]], 2)
        y += 26


# kiem du lieu TRUOC khi dung toi he thong: thieu dap an / video thi khong co gi de so -> LOI (truoc day thoat 0 "suong")
if not os.path.isfile(_DA):
    loi("Thiếu tệp đáp án %s — tạo bằng tao_video_an_toan.py hoặc đặt SMARTPORT_DULIEU_DIR" % _DA)
try:
    DA = json.load(open(_DA, encoding="utf-8"))
except (OSError, ValueError) as e:
    loi("Không đọc được tệp đáp án %s: %s" % (_DA, e))
if not isinstance(DA, dict) or any(x not in DA for x in KHOA_DA):
    loi("Tệp đáp án %s thiếu khóa %s" % (_DA, [x for x in KHOA_DA if not isinstance(DA, dict) or x not in DA]))
cap = cv2.VideoCapture(V)
if not cap.isOpened():
    loi("Không mở được video kiểm thử %s — tạo bằng tao_video_an_toan.py hoặc đặt SMARTPORT_DULIEU_DIR" % V)
if not os.path.isfile(_TK):
    loi("Thiếu tài khoản kiểm thử %s — chạy nhóm kiểm thử microservices để tạo" % _TK)
try:
    tk = json.load(open(_TK, encoding="utf-8"))
    token = goi("/api/auth/dang-nhap", {"tenDangNhap": tk["tenDangNhap"], "matKhau": tk["matKhau"]})["token"]
except (OSError, ValueError, KeyError, TypeError) as e:          # URLError / HTTPError / het gio deu la OSError
    loi("Đăng nhập tài khoản kiểm thử qua gateway thất bại: %s" % e, 1)
fps = cap.get(cv2.CAP_PROP_FPS) or 10
phien = uuid.uuid4().hex
cau_hinh = {"gocNhin": DA["gocNhin"], "vung": DA["vung"]}
gui_cau_hinh = True              # gui o khung dau + khi dich vu bao phien vua tao lai ma chua co cau hinh (canCauHinh)
su_kien, ms, vong = [], [], []
lop_cua = {}
ghi = None
k = 0
t_dau = time.time()
while True:
    ok, f = cap.read()
    if not ok:
        break
    t = k / fps
    _, jpg = cv2.imencode(".jpg", f, [cv2.IMWRITE_JPEG_QUALITY, 85])
    t0 = time.perf_counter()
    try:
        kq = goi("/api/ai-thi-giac/an-toan/khung", {"phien": phien, "anh": "data:image/jpeg;base64," + base64.b64encode(jpg).decode(),
                                                    "t": t, "fps": fps, "cauHinh": cau_hinh if gui_cau_hinh else None}, token)
    except (OSError, ValueError) as e:
        if ghi:
            ghi.release()
        loi("Gọi /api/ai-thi-giac/an-toan/khung thất bại ở khung %d: %s" % (k, e), 1)
    gui_cau_hinh = bool(kq.get("canCauHinh"))      # phien het han / bi day ra -> gui lai cau hinh o khung ke tiep
    vong.append((time.perf_counter() - t0) * 1000)
    ms.append(kq["ms"])
    for o in kq["doiTuong"]:
        lop_cua[o["id"]] = o["lop"]
    for e in kq["suKien"]:
        su_kien.append(e)
    if RA:
        if ghi is None:
            ghi = cv2.VideoWriter(RA, cv2.VideoWriter_fourcc(*"VP80"), fps, (f.shape[1], f.shape[0]))
            if not ghi.isOpened():                 # thu muc khong co / codec loi: VideoWriter van "truthy" ma khong ghi
                print("Video ket qua KHONG ghi duoc (thu muc / codec): %s" % RA, flush=True)
                ghi = RA = None
    if ghi is not None:
        ve(f, kq, cau_hinh.get("vung"))
        ghi.write(f)
    k += 1
if ghi:
    ghi.release()
if k == 0:
    loi("Không đọc được khung nào từ video %s — không có gì để kiểm" % V)
print("khung %d | may chu trung vi %.0f ms | vong tron qua gateway trung vi %.0f ms | %.1f s" % (
    k, np.median(ms), np.median(vong), time.time() - t_dau))
if ghi:
    print("Video ket qua co ve canh bao: %s" % RA)
for e in su_kien:
    print("  %5.1fs %-10s %-18s #%s %s %s" % (e["t"], e["muc"], e["loai"], e["doiTuong"], [lop_cua.get(i) for i in e["doiTuong"]],
                                         {x: e[x] for x in ("ttc", "khoangCachM", "tocDoKmh", "vung") if e.get(x) is not None}))


def co(loai, muc=None, tu=0.0, den=99.0, y_min=None, y_max=None):
    return [e for e in su_kien if e["loai"] == loai and (muc is None or e["muc"] == muc) and tu <= e["t"] <= den
            and (y_min is None or e["diem"][0][1] >= y_min) and (y_max is None or e["diem"][0][1] <= y_max)]


tc = DA["t_xe_toi_sat_nguoi"]
kt = [
    ("Xe tải lao tới công nhân: CẢNH BÁO va chạm ≥ 2 s trước", bool(co("va-cham-xe-nguoi", "canh-bao", 0, tc - 2.0))),
    ("… và NGUY HIỂM va chạm trước khi tới sát (≥ 1 s)", bool(co("va-cham-xe-nguoi", "nguy-hiem", 0, tc - 1.0))),
    ("Công nhân đầu trần vào vùng cấm 'Dưới cần trục': báo ≤ 1 s sau khi vào (%.1f s)" % DA["t_b_vao_vung"],
     bool(co("vao-vung-cam", "nguy-hiem", DA["t_b_vao_vung"] - 0.3, DA["t_b_vao_vung"] + 1.0))),
    ("Ô tô 30 km/h (giới hạn 15): báo quá tốc độ ĐÚNG Ô TÔ, đo 20–45 km/h",
     bool([e for e in co("qua-toc-do") if lop_cua.get(e["doiTuong"][0]) == "o-to" and 20 <= (e.get("tocDoKmh") or 0) <= 45])),
    ("Công nhân ĐẦU TRẦN (chân y ≈ 600): báo thiếu mũ", bool(co("thieu-mu", y_min=570, y_max=640))),
    ("Công nhân ĐỦ mũ + áo (chân y ≈ 536): KHÔNG báo thiếu mũ / áo",
     not co("thieu-mu", y_min=505, y_max=565) and not co("thieu-ao", y_min=505, y_max=565)),
    ("Không có báo động VÔ LÝ (tốc độ > 60 km/h, va chạm khi còn cách > 30 m)",
     not [e for e in su_kien if (e.get("tocDoKmh") or 0) > 60 or (e["loai"].startswith("va-cham") and (e.get("khoangCachM") or 0) > 30)]),
]
for ten, dk in kt:
    print(("  OK  " if dk else "  LOI ") + ten)
print("KET QUA E2E: %d/%d" % (sum(d for _, d in kt), len(kt)))
sys.exit(0 if all(d for _, d in kt) else 1)
