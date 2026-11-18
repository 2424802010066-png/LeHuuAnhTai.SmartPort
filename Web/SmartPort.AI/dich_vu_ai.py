# -*- coding: utf-8 -*-
"""
DICH VU AI THI GIAC (microservice Python) — cong 8100, KHONG trang thai, KHONG CSDL.
  GET  /suc-khoe                      : tinh trang + mo hinh dang nap + chi so do tren tap test giu rieng
  GET  /api/ai-thi-giac/mo-hinh       : thong tin mo hinh
  POST /api/ai-thi-giac/bien-so       : {anh: data URL / base64} -> bien so (YOLO26 tim bien + YOLO26 doc tung ky tu)
  POST /api/ai-thi-giac/container     : {anh} -> so container (YOLO26 tim vung ma + CRNN + giai ma rang buoc ISO 6346)
  POST /api/ai-thi-giac/video?loai=ca-hai|bien-so|container&fps=3  (than = tep video, <= 200 MB, <= 10 phut)
                                      -> 202 {maViec}: xu ly NEN (lay mau khung -> doc tung khung -> gom + bo phieu)
  GET  /api/ai-thi-giac/video/{ma}?tu=n : tien do + doi tuong da nhan dien + ket qua tung khung (tu khung thu n)
  DELETE /api/ai-thi-giac/video/{ma}  : dung xu ly
  POST /api/ai-thi-giac/an-toan/khung : {phien, anh, t, cauHinh?} -> CANH BAO NGUY HIEM 1 khung (an_toan_cang.py): theo vet
                                        + du bao va cham (TTC) + vung nguy hiem + toc do + nguoi nga + do bao ho.
                                        canCauHinh=true: phien vua TAO LAI (het han / bi day ra) ma khung khong kem cauHinh
                                        -> giao dien gui lai cau hinh o khung ke tiep (da tu khoi phuc cau hinh cuoi neu con giu)
                                        429: het cho phien (moi tai khoan <= 16, tong <= 32; phien dang chay khong bi day ra)
  GET  /api/ai-thi-giac/an-toan/thong-tin : mo hinh, nguong mac dinh, do chinh xac mo hinh do bao ho (tap TEST)
Cong API gateway dinh tuyen /api/ai-thi-giac/* toi day (dich-vu.json, nenTang = python). Dich vu .NET (Cong) goi khi
doi chieu container (chong trao). Xac thuc: JWT HS256 cung khoa voi moi dich vu .NET (bien moi truong Jwt__Key) + iss/aud
+ THU HOI token (khoa tai khoan / doi vai tro / doi mat khau) qua dich vu dinh-danh, bo dem 60 s (xem kiem_thu_hoi).
Chi dung thu vien chuan cho phan HTTP (http.server) — khong can FastAPI.
"""
import base64
import concurrent.futures
import hashlib
import hmac
import http.client
import io
import json
import math
import os
import secrets
import socket
import sys
import threading
import time
import traceback
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlsplit

GOC = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, GOC)

import numpy as np  # noqa: E402
import torch  # noqa: E402
from PIL import Image  # noqa: E402

import cai_tien_bien_so as ctbs  # noqa: E402
import danh_gia_bien_so as dgbs  # noqa: E402
import doc_ma_container as dmc  # noqa: E402
import an_toan_cang as atc  # noqa: E402
import nhan_dien_video as ndv  # noqa: E402

# Cau hinh gop YOLO26 + RF-DETR doc ky tu bien so — CHON TREN TAP VAL (cai_tien_bien_so.py, runs/danh_gia/chinh_bien_so_val.json)
CAU_HINH_BIEN = dict(w_rf=1.5, nguong=0.05, p_min=0.1, lam=1.0, chuan_hoa=False, p_kieu="noisy_or")
LE_CAT_BIEN = 0.1                    # le cat vung bien truoc khi doc ky tu (dung nhu luc danh gia)
# Do tin cay (trung binh nhan p_co x q moi ky tu) duoi nguong -> KHONG tra bien so, chuyen kiem tay. CHON TREN VAL
# (chon_nguong_tin_cay_bien_so.py -> runs/danh_gia/nguong_tin_cay_bien_so.json): TEST dung 91,2 -> 91,1%,
# doc sai ma van cho qua 8,8 -> 7,3%, tu choi 1,6%. Chan kieu "ep ra bien hop le" tu anh qua nho / bien la (quan doi, nuoc ngoai).
NGUONG_TIN_CAY_BIEN = 0.638

CONG = int(os.environ.get("AI_CONG", "8100"))
KHOA_JWT = os.environ.get("Jwt__Key", "")
VAI_TRO_DUOC_DUNG = {"Admin", "NhanVien"}
KHOA_GPU = threading.Lock()          # 1 GPU: suy luan tuan tu, tranh tranh chap bo nho
KHOI_DONG = time.time()
MO_HINH = {}
GIOI_HAN_THAN = 16 * 1024 * 1024     # than yeu cau toi da (anh base64)
GIOI_HAN_VIDEO = 200 * 1024 * 1024   # = gioi han tai len cua cong API gateway
THU_MUC_VIDEO = os.path.join(GOC, "tam_video")   # tep video tam — xoa ngay khi xu ly xong
VIEC_VIDEO = {}                      # ma viec -> trang thai (giu trong RAM 30 phut sau khi xong)
KHOA_VIEC = threading.Lock()
HANG_VIDEO = threading.Semaphore(1)  # 1 video xu ly 1 luc (chung 1 GPU); video sau xep hang
# GIOI HAN DIEM ANH — kiem kich thuoc tu DAU TEP (Image.open chua giai ma) truoc khi giai ma: PNG 17 KB co the khai
# 12000 x 12000 -> giai ma ra 412 MB + cac ban sao. Anh tinh (bien so / container: anh chup dien thoai 12-48 MP) <= 50 MP;
# khung camera (giao dien thu ve <= 960 px) <= 3840 px moi canh (4K). PIL tu chan o 2 x MAX_IMAGE_PIXELS (bom nen).
GIOI_HAN_ANH_TINH = (10000, 50_000_000)         # (canh dai toi da px, tong diem anh toi da)
GIOI_HAN_ANH_KHUNG = (3840, 3840 * 2160)
Image.MAX_IMAGE_PIXELS = GIOI_HAN_ANH_TINH[1]
# anh tinh (toi 50 MP ~ 150 MB RGB / anh) xu ly tuan tu duoi KHOA_GPU -> chi cho 2 yeu cau GIU anh da giai ma cung luc
# (1 dang chay + 1 giai ma san): giu nhieu hon khong nhanh hon, chi ton RAM. Khung camera (<= 4K) khong can: cung co RAM
# voi than yeu cau 16 MB, va de 1 phien chiem het cho thi camera khac bi 429 oan.
CHO_ANH_TINH = threading.BoundedSemaphore(2)
PHIEN_AN_TOAN = {}                   # ma phien (camera / video dang phat) -> {phien, luc, khoa, nguoi, dang, cauHinh}
KHOA_PHIEN = threading.Lock()        # giu ngan (chi thao tac dict); KHONG bao gio cho khoa khac khi dang giu
GIOI_HAN_PHIEN = 32                  # tong so phien giu cung luc (moi phien chi ton RAM: vet + mau mat dat)
GIOI_HAN_PHIEN_NGUOI = 16            # moi tai khoan toi da 16 phien: 1 tai khoan khong chiem het bang phien cua nguoi khac
PHIEN_HET_HAN = 120                  # giay khong co khung -> bo phien (cau hinh van giu o CAU_HINH_PHIEN)
PHIEN_DANG_CHAY = 10                 # giay: phien co khung trong 10 s gan nhat / dang xu ly KHONG bao gio bi day ra
# cau hinh HIEU LUC cuoi cua phien da bo (het han / bi day ra): (ma phien, nguoi) -> {ch, luc}. Khung sau cung ma phien
# -> tao lai phien VOI cau hinh nay (khong mat vung cam / hieu chinh / nguong). Giu 30 phut, <= 256 muc, <= 32 / tai khoan.
CAU_HINH_PHIEN = {}
GIU_CAU_HINH, GIOI_HAN_CAU_HINH, GIOI_HAN_CAU_HINH_NGUOI = 1800, 256, 32
# GIOI HAN cauHinh: _tim_rui_ro duyet moi vung moi khung tren LUONG_AN_TOAN (1 luong chung moi camera). Vung vat-can ton
# ~0,8 ms / dinh / xe dang tien lai (mo phong 30 buoc, do 10/2026) -> tong dinh vat-can <= 64 (vd 16 vat x 4 dinh)
GIOI_HAN_VUNG, GIOI_HAN_DINH_VUNG, GIOI_HAN_DINH_VAT_CAN = 32, 32, 64
# nguong lam tang chi phi moi khung: ttc_* = so giay mo phong (T / 0,1 s buoc), bao_ho_toi_da = so anh cat chay GPU
# (cua_so_giay da bi may canh bao kep <= 48 khung, cac nguong con lai khong doi chi phi -> chi kiem la so huu han)
GIOI_HAN_NGUONG = dict(ttc_nguy_hiem=(0, 10), ttc_canh_bao=(0, 10), le_canh_bao=(0, 20), bao_ho_toi_da=(0, 16),
                       tuoi_toi_thieu=(1, 100))
# MAY CANH BAO chay tren 1 LUONG CO DINH: PyTorch giu handle cuBLAS / cuDNN + stream CUDA THEO LUONG, ma ThreadingHTTPServer
# tao luong moi cho moi yeu cau -> moi khung khoi tao lai (do: 45 ms/khung neu cung luong, 117 ms neu moi khung 1 luong moi,
# ~400 ms trong dich vu that). Cac phien (camera) xep hang tren luong nay; khoa GPU van dung chung voi API khac.
LUONG_AN_TOAN = concurrent.futures.ThreadPoolExecutor(max_workers=1, thread_name_prefix="an-toan-gpu")
# Thu tu khoa (mot chieu -> khong deadlock): khoa phien -> KHOA_PHIEN (ngan, chi dict) ; khoa phien -> LUONG_AN_TOAN ->
# KHOA_GPU ; CHO_ANH_TINH -> KHOA_GPU. Khong ai cho khoa phien / GPU khi dang giu KHOA_PHIEN. KHOA_THU_HOI chi giu khi
# doc / ghi bo dem (khong goi mang khi dang giu).


class QuaLon(Exception):
    """Than yeu cau vuot GIOI_HAN_THAN -> tra 413"""


class QuaTai(Exception):
    """Het cho (bang phien / cho giu anh) -> tra 429 kem thong bao ro (khong im lang day phien nguoi khac)"""


def nap_mo_hinh():
    """Nap cac mo hinh da huan luyen (bo qua cai chua co — dich vu van chay, bao ro thieu gi)"""
    def thu(ten, ham):
        try:
            MO_HINH[ten] = ham()
            print("  da nap", ten, flush=True)
        except Exception as e:  # noqa: BLE001
            print("  CHUA nap duoc", ten, ":", str(e)[:160], flush=True)

    thu("bien", lambda: dgbs.PhatHien("yolo_bien", imgsz=640, nguong=0.25))
    thu("ky_tu", lambda: dgbs.PhatHien("yolo_kytu", imgsz=320, nguong=0.05))       # nguong thap: bo gop tu loc
    thu("vung_cont", lambda: dgbs.PhatHien("yolo_cont", imgsz=1024, nguong=0.1))   # 0.1: chon tren tap VAL
    thu("crnn", dmc.tai_mo_hinh)
    thu("ky_tu_rfdetr", lambda: dgbs.PhatHien("rfdetr_kytu", imgsz=320, nguong=0.05))
    # CANH BAO NGUY HIEM: YOLO26s COCO goc (nguoi + xe) va YOLO26s tinh chinh do bao ho (Construction-PPE)
    # nguong THAP hon track_activation_threshold cua ByteTrack (0,3): buoc ghep lan 2 cua ByteTrack chi dung hop diem
    # 0,1-0,3 (nguoi bi xe che) — loc 0,3 ngay o YOLO thi buoc nay khong bao gio chay, vet doi ma khi bi che. May canh bao
    # (an_toan_cang.NGUONG_PHAT_HIEN) tu dam bao hop diem thap khong tao vet moi / khong lam mau tu uoc ti le.
    # Ban an_toan_cang cu chua co hang so -> giu 0,3 nhu truoc (khong doi hanh vi khi chua chep ban may canh bao moi)
    thu("vat_the", lambda: PhatHienYolo(os.path.join(GOC, "yolo26s.pt"), nguong=getattr(atc, "NGUONG_PHAT_HIEN", 0.3),
                                        lop=sorted(atc.LOP_COCO)))
    pt_bao_ho = os.path.join(GOC, "runs", atc.MO_HINH_BAO_HO, "weights", "best.pt")
    if os.path.exists(pt_bao_ho):            # nguong thap (dung nhu luc chon luat): luat an_toan_cang.LUAT_BAO_HO tu loc
        thu("bao_ho", lambda: PhatHienYolo(pt_bao_ho, nguong=atc.NGUONG_DIEM_BAO_HO))
    if "ky_tu_rfdetr" in MO_HINH:
        MO_HINH["ky_tu_rfdetr"](Image.new("RGB", (160, 48), "white"))   # chay nong: RF-DETR dat che do TF32 cua no
    # RF-DETR bat TF32; CRNN/YOLO vung ma duoc danh gia o FP32 day du -> moi yeu cau tu dat lai (xem che_do_tf32)
    TF32_RFDETR[0] = (torch.backends.cuda.matmul.allow_tf32, torch.get_float32_matmul_precision())


TF32_RFDETR = [(False, "highest")]


class PhatHienYolo:
    """YOLO26 -> goi(anh RGB numpy) -> mang N x 6 (x1, y1, x2, y2, conf, lop). Ultralytics hieu mang numpy la BGR."""

    def __init__(self, duong, imgsz=640, nguong=0.3, lop=None):
        from ultralytics import YOLO
        self.m, self.imgsz, self.nguong, self.lop = YOLO(duong), imgsz, nguong, lop

    def __call__(self, img):
        r = self.m.predict(np.ascontiguousarray(img[..., ::-1]), imgsz=self.imgsz, conf=self.nguong, classes=self.lop,
                           verbose=False, device=0)[0]
        b = r.boxes
        return np.c_[b.xyxy.cpu().numpy(), b.conf.cpu().numpy(), b.cls.cpu().numpy()] if len(b) else np.zeros((0, 6))

    def theo_nguoi(self, anh_cat, co):
        """do bao ho 2 TANG: tung anh cat 1 nguoi (RGB) phong to len 'co' px -> list mang N x 6 theo toa do anh cat.
        Chay tung anh (khong gop lo) de giong het luc danh gia (danh_gia_bao_ho_khoang_cach.py)"""
        ra = []
        for c in anh_cat:
            r = self.m.predict(np.ascontiguousarray(c[..., ::-1]), imgsz=co, conf=self.nguong, classes=self.lop,
                               verbose=False, device=0)[0]
            b = r.boxes
            ra.append(np.c_[b.xyxy.cpu().numpy(), b.conf.cpu().numpy(), b.cls.cpu().numpy()] if len(b) else np.zeros((0, 6)))
        return ra


class che_do_tf32:
    """Dat TF32 dung nhu luc DANH GIA tung mo hinh (bo gop bien so: nhu khi nap RF-DETR; ma container: FP32 day du)"""

    def __init__(self, bat):
        self.bat = bat

    def __enter__(self):
        tf32, do_chinh_xac = TF32_RFDETR[0] if self.bat else (False, "highest")
        torch.backends.cuda.matmul.allow_tf32 = tf32
        torch.set_float32_matmul_precision(do_chinh_xac)

    def __exit__(self, *a):
        return False


def chi_so():
    """Chi so do tren tap test giu rieng (doc tu ket qua danh gia) — de giao dien hien minh bach"""
    ra = {}
    for ten, tep in (("bienSoDauCuoi", "bien_so_gop_e2e_test.json"), ("bienSoAnhCat", "bien_so_gop_cat_test.json"),
                     ("containerToanAnh", "container_yolo_toan.json")):
        p = os.path.join(GOC, "runs", "danh_gia", tep)
        if os.path.exists(p):
            b = json.load(open(p, encoding="utf-8"))["bang"]
            b = b.get("sau_sua_dinh_dang", b)
            ra[ten] = b.get("TAT CA", {}).get("ti_le_dung")
    p = os.path.join(GOC, "runs", "doc_ma_container", "ket_qua_test_khung_that.json")
    if os.path.exists(p):
        ra["containerVungMa"] = round(100 * json.load(open(p, encoding="utf-8"))["tom_tat"]["dung_rang_buoc"], 1)
    return ra


# ------------------------------------------------------------------ JWT HS256 (thu vien chuan)
def _b64(s):
    return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))


def _cau_hinh_jwt():
    """Issuer / Audience GIONG dich vu .NET (KhoiDongWeb.cs: Jwt:Issuer / Jwt:Audience): bien moi truong > tep cau hinh
    chung (DichVu__CauHinhChung, chay_microservices.ps1 dat truoc khi chay dich vu nay) > mac dinh"""
    jwt = {}
    try:
        jwt = json.load(open(os.environ["DichVu__CauHinhChung"], encoding="utf-8-sig")).get("Jwt") or {}
    except (KeyError, OSError, ValueError, AttributeError):
        pass
    return (os.environ.get("Jwt__Issuer") or jwt.get("Issuer") or "SmartPort.Api",
            os.environ.get("Jwt__Audience") or jwt.get("Audience") or "smartport-web")


def _dia_chi_dinh_danh():
    """Dich vu dinh-danh (hoi trang thai tai khoan): AI_DINH_DANH (URL; "tat" = khong hoi) > cong cua dinh-danh trong
    dich-vu.json (DichVu__DanhMuc hoac ..\\dich-vu.json). Goi thang 127.0.0.1 (khong qua cong API)"""
    dat = os.environ.get("AI_DINH_DANH", "").strip()
    if dat:
        return "" if dat.lower() == "tat" else dat.rstrip("/")
    for p in (os.environ.get("DichVu__DanhMuc"), os.path.join(GOC, "..", "dich-vu.json")):
        try:
            for dv in json.load(open(p, encoding="utf-8-sig"))["dichVu"]:
                if dv.get("ten") == "dinh-danh":
                    return "http://127.0.0.1:%d" % int(dv["cong"])
        except (TypeError, OSError, ValueError, KeyError):
            continue
    return ""


JWT_ISS, JWT_AUD = _cau_hinh_jwt()
DINH_DANH = _dia_chi_dinh_danh()
THU_HOI = {}                         # sha256(token) -> (ok, ly_do, luc kiem, han token): trang thai BIET GAN NHAT
KHOA_THU_HOI = threading.Lock()
THU_HOI_GIAY, NGAT_MACH_GIAY, GIOI_HAN_THU_HOI = 60, 15, 4096
_LOI_DINH_DANH = [0.0, 0.0]          # [luc goi loi gan nhat (ngat mach), luc ghi nhat ky loi gan nhat]
_KHONG_PROXY = urllib.request.build_opener(urllib.request.ProxyHandler({}))   # 127.0.0.1: khong di qua proxy he thong
LY_DO_THU_HOI = "Tài khoản đã bị khóa, đổi vai trò hoặc đổi mật khẩu — vui lòng đăng nhập lại."


def _hoi_dinh_danh(token):
    """GET dinh-danh /api/auth/toi bang chinh token: .NET chay JwtBearer + KiemTraTaiKhoan.KhiTokenHopLe (khoa tai khoan,
    doi vai tro, dau mat khau dauMk so voi CSDL). Tra (ok, ly_do) hoac None neu KHONG hoi duoc (loi mang / 5xx / 404)."""
    # KHONG duoc nem loi ra ngoai: kiem_jwt chay TRUOC khoi try cua do_POST / do_GET -> loi lot ra = dong ket noi khong
    # phan hoi (cong API bao 502) va ngat mach khong bat (moi khung lai hoi). Request() trong try: AI_DINH_DANH go sai
    # (vd "http//127.0.0.1:5101", chi ten may) -> ValueError; dinh-danh tat giua chung -> IncompleteRead / BadStatusLine
    # (http.client.HTTPException, khong phai OSError)
    try:
        yc = urllib.request.Request(DINH_DANH + "/api/auth/toi", headers={"Authorization": "Bearer " + token})
        with _KHONG_PROXY.open(yc, timeout=2) as r:
            vai_tro = json.loads(r.read() or b"{}").get("vaiTro")
        if vai_tro not in VAI_TRO_DUOC_DUNG:            # vai tro HIEN TAI trong CSDL (khong tin claim trong token)
            return False, "Tài khoản không có quyền dùng AI thị giác."
        return True, None
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            return False, LY_DO_THU_HOI
        loi = "HTTP %d" % e.code
    except (OSError, ValueError, AttributeError, TypeError, http.client.HTTPException) as e:   # mang / timeout / JSON hong
        loi = "%s: %s" % (type(e).__name__, e)
    now = time.time()
    _LOI_DINH_DANH[0] = now
    if now - _LOI_DINH_DANH[1] > 60:                     # nhat ky <= 1 dong / phut
        _LOI_DINH_DANH[1] = now
        print("  [canh bao] khong hoi duoc dinh-danh (%s) tai %s — tam dung trang thai biet gan nhat / kiem cuc bo"
              % (loi[:160], DINH_DANH), flush=True)
    return None


def kiem_thu_hoi(token, han):
    """THU HOI token nhu dich vu .NET (BaoMatJwt.KhiTokenHopLe, bo dem 60 s): hoi dinh-danh, nho ket qua 60 s theo bam
    SHA-256 cua token (khong giu token tho). KHONG HOI DUOC dinh-danh (dang khoi dong lai / sap) -> LUA CHON:
      - token da biet bi thu hoi: VAN CHAN (trang thai biet gan nhat, ke ca da qua 60 s);
      - token da biet hop le / chua tung hoi: CHO QUA theo kiem cuc bo (chu ky + han + iss/aud + vai tro trong token).
    Ly do khong chan het: dich vu AI KHONG ghi CSDL (token lot qua luc dinh-danh sap chi dung duoc GPU), con chan het thi
    moi lan dinh-danh khoi dong lai MOI camera canh bao nguy hiem (an toan tinh mang) dung theo. Ngat mach 15 s sau loi:
    khong goi lai lien tuc, khung camera khong phai doi het thoi gian cho 2 s. Tat han: AI_DINH_DANH=tat."""
    if not DINH_DANH:
        return True, None
    k = hashlib.sha256(token.encode()).hexdigest()
    now = time.time()
    with KHOA_THU_HOI:
        c = THU_HOI.get(k)
    if c is not None and now - c[2] < THU_HOI_GIAY:
        return c[0], c[1]
    kq = _hoi_dinh_danh(token) if now - _LOI_DINH_DANH[0] >= NGAT_MACH_GIAY else None
    if kq is None:
        return (c[0], c[1]) if c is not None else (True, None)
    with KHOA_THU_HOI:
        THU_HOI[k] = (kq[0], kq[1], now, han)
        if len(THU_HOI) > GIOI_HAN_THU_HOI:              # don: token het han truoc, con day thi bo muc kiem lau nhat
            for m in [m for m, v in THU_HOI.items() if v[3] < now]:
                THU_HOI.pop(m)
            while len(THU_HOI) > GIOI_HAN_THU_HOI:
                THU_HOI.pop(min(THU_HOI, key=lambda m: THU_HOI[m][2]))
    return kq


def kiem_jwt(header):
    """Tra (ok, ly_do). Kiem chu ky HMAC-SHA256 bang khoa chung, han dung, iss / aud (nhu JwtBearer .NET), vai tro, roi
    THU HOI (tai khoan bi khoa / doi vai tro / doi mat khau — kiem_thu_hoi)."""
    if not KHOA_JWT:
        return True, "che do phat trien (chua dat Jwt__Key)"
    if not header or not header.startswith("Bearer "):
        return False, "Thiếu token đăng nhập."
    try:
        dau, than, ky = header[7:].split(".")
        ky_dung = base64.urlsafe_b64encode(hmac.new(KHOA_JWT.encode(), (dau + "." + than).encode(), hashlib.sha256).digest()).rstrip(b"=")
        if not hmac.compare_digest(ky_dung, ky.encode()):
            return False, "Token sai chữ ký."
        nd = json.loads(_b64(than))
        if nd.get("exp", 0) < time.time():
            return False, "Token đã hết hạn."
        if nd.get("nbf", 0) > time.time() + 60:          # lech dong ho toi da 1 phut (ClockSkew cua .NET)
            return False, "Token chưa có hiệu lực."
        aud = nd.get("aud")
        if nd.get("iss") != JWT_ISS or JWT_AUD not in (aud if isinstance(aud, list) else [aud]):
            return False, "Token không do SmartPort cấp (sai iss / aud)."
        if nd.get("vaiTro") not in VAI_TRO_DUOC_DUNG:
            return False, "Tài khoản không có quyền dùng AI thị giác."
        ten, han = nd.get("ten"), float(nd["exp"])
    except Exception:  # noqa: BLE001
        return False, "Token không hợp lệ."
    ok, ly_do = kiem_thu_hoi(header[7:], han)
    return (True, ten) if ok else (False, ly_do)


# ------------------------------------------------------------------ suy luan
def doc_anh(chuoi, gioi_han=GIOI_HAN_ANH_TINH):
    """data URL / base64 -> anh PIL RGB. KIEM KICH THUOC TRUOC KHI GIAI MA (Image.open chi doc dau tep): anh khai qua
    gioi_han (canh dai, tong diem anh) / bom nen -> ValueError (400) ro rang, khong cap phat hang GB"""
    if not chuoi:
        raise ValueError("Thiếu ảnh.")
    if not isinstance(chuoi, str):
        raise ValueError("Ảnh phải là chuỗi base64 / data URL.")
    i = chuoi.find("base64,")
    raw = base64.b64decode(chuoi[i + 7:] if i >= 0 else chuoi)
    if len(raw) > 12 * 1024 * 1024:
        raise ValueError("Ảnh quá 12 MB.")
    canh, diem = gioi_han
    try:
        im = Image.open(io.BytesIO(raw))
        w, h = im.size
        if max(w, h) > canh or w * h > diem:
            raise ValueError("Ảnh %d x %d px quá lớn (tối đa %d px mỗi cạnh, %.1f triệu điểm ảnh) — thu nhỏ ảnh rồi gửi lại."
                             % (w, h, canh, diem / 1e6))
        return im.convert("RGB")
    except Image.DecompressionBombError:              # Exception thuong (khong phai ValueError / OSError): bat rieng
        raise ValueError("Ảnh quá lớn (khai hơn %d triệu điểm ảnh — nghi bom nén), từ chối."
                         % (2 * Image.MAX_IMAGE_PIXELS // 1_000_000)) from None


def nhan_bien_so(img):
    if "ky_tu" not in MO_HINH:
        raise RuntimeError("Chưa có mô hình đọc ký tự biển số.")
    t0 = time.perf_counter()
    hop = None
    vung = img
    if "bien" in MO_HINH:
        det = MO_HINH["bien"](img)
        if len(det):
            tot = det[np.argmax(det[:, 4])]
            hop = [round(float(v), 1) for v in tot[:4]]
            vung = dgbs.cat_bien(img, tot, LE_CAT_BIEN)
        elif max(img.size) > 400:
            return dict(bienSo=None, thongBao="Không thấy biển số trong ảnh.", ms=round((time.perf_counter() - t0) * 1000))
    k_yolo = MO_HINH["ky_tu"](vung)
    k_yolo_chac = k_yolo[k_yolo[:, 4] >= 0.3] if len(k_yolo) else k_yolo
    raw, conf = dgbs.ghep_ky_tu(k_yolo_chac)                      # doc nhanh 1 mo hinh (de so sanh / du phong)
    tin_cay = conf
    if "ky_tu_rfdetr" in MO_HINH:
        # GOP 2 mo hinh + quy hoach dong theo mau bien so VN (bo khung thua, sua nham chu/so theo vi tri)
        bien, _, tin_cay = ctbs.giai_ma_tin_cay([k_yolo, MO_HINH["ky_tu_rfdetr"](vung)], CAU_HINH_BIEN)
        cach = "YOLO26 + RF-DETR gộp, giải mã theo mẫu biển VN"
    else:
        bien = dgbs.sua_dinh_dang_vn(raw)
        cach = "YOLO26"
    hop_le = bool(dgbs.re.fullmatch(r"\d{2}[A-Z][A-Z0-9]?\d{3,6}", bien or ""))
    ra = dict(bienSo=bien or None, bienSoTho=raw or None, doTinCay=round(tin_cay, 3), nguongTinCay=NGUONG_TIN_CAY_BIEN,
              hop=hop, cachDoc=cach, hopLeVN=hop_le, ms=round((time.perf_counter() - t0) * 1000))
    if hop and not bien:
        ra["thongBao"] = "Thấy vùng biển số nhưng không đọc ra biển số Việt Nam (biển nước ngoài, quá mờ hoặc bị che)."
    if bien and tin_cay < NGUONG_TIN_CAY_BIEN:
        # khong chac: KHONG tra bien (cong khong tu mo barrier theo chuoi doan), van cho xem chuoi doan de nguoi kiem
        ra.update(bienSo=None, bienSoDoan=bien, canKiemTay=True,
                  thongBao="Ký tự mờ/không rõ (độ tin cậy %.2f < %.2f) — có thể ảnh quá nhỏ hoặc biển không theo mẫu biển "
                           "dân sự VN (biển quân đội, nước ngoài). Chuyển kiểm tay." % (tin_cay, NGUONG_TIN_CAY_BIEN))
    return ra


_dem_anh = [0]


def nhan_container(img):
    if "crnn" not in MO_HINH:
        raise RuntimeError("Chưa có mô hình CRNN đọc mã container.")
    t0 = time.perf_counter()
    _dem_anh[0] += 1
    khoa = "anh-tai-len-%d" % _dem_anh[0]
    dmc._CACHE[khoa] = img                    # anh_that() doc anh tu bo nho dem theo khoa
    try:
        vung = []
        if "vung_cont" in MO_HINH:
            det = MO_HINH["vung_cont"](img)
            # moi vung la 1 NGUON chung cu, trong so = do tin cay bo phat hien (vung mo van giup, vung sai khong keo xuong)
            vung = [dict(anh=khoa, lop=dmc.LOP[int(c)], hop=[int(x1), int(y1), int(x2), int(y2)], conf=round(float(cf), 3))
                    for x1, y1, x2, y2, cf, c in det]
        lop_co = {v["lop"] for v in vung}
        du_ma = "ma_ngang" in lop_co or "ma_doc" in lop_co or {"so_huu", "seri"} <= lop_co
        w, h = img.size
        if not du_ma and (w >= 2.2 * h or h >= 2.2 * w or not vung):
            # anh DA CAT SAN vung ma (dai -> ma ngang, cao -> ma doc): doc ca anh; rang buoc ISO 6346 + bo phieu chon ket qua
            vung.append(dict(anh=khoa, lop="ma_ngang" if w >= h else "ma_doc", hop=[0, 0, w, h]))
        if not vung:
            return dict(soCont=None, hopLe=False, thongBao="Không thấy vùng mã container trong ảnh.", ms=round((time.perf_counter() - t0) * 1000))
        ma, hop_le, tham_lam, ct = dmc.chon_ma(MO_HINH["crnn"], vung, chi_tiet=True)
        # hop_le=False: khong co ma dung ISO 6346 du tin cay -> KHONG tra ma (de cong chuyen kiem tay, khong bao trao nham)
        return dict(soCont=(ma or None) if hop_le else None, hopLe=bool(hop_le), docThamLam=tham_lam or None,
                    doTinCay=ct["do_tin_cay"], soNguon=ct["so_nguon"],
                    vung=[dict(lop=v["lop"], hop=v["hop"], conf=v.get("conf")) for v in vung],
                    ms=round((time.perf_counter() - t0) * 1000))
    finally:
        dmc._CACHE.pop(khoa, None)


# ------------------------------------------------------------------ video
def doc_khung_video(loai):
    def doc(img):
        kb = kc = None
        with KHOA_GPU:                            # nha khoa sau MOI khung: yeu cau doc ANH khong phai doi het video
            if loai in ("ca-hai", "bien-so") and "ky_tu" in MO_HINH:
                with che_do_tf32(True):
                    kb = nhan_bien_so(img)
            if loai in ("ca-hai", "container") and "crnn" in MO_HINH:
                with che_do_tf32(False):
                    kc = nhan_container(img)
        return kb, kc
    return doc


def chay_viec_video(viec):
    viec["viTriHangDoi"] = sum(1 for v in list(VIEC_VIDEO.values()) if v["trangThai"] in ("dang-cho", "dang-xu-ly")) - 1
    with HANG_VIDEO:
        viec["viTriHangDoi"] = 0
        if viec["huy"]:
            viec["trangThai"] = "da-huy"
            try:
                os.remove(viec["tep"])
            except OSError:
                pass
            viec["xongLuc"] = time.time()
            return
        ndv.xu_ly(viec, doc_khung_video(viec["loai"]))


def chi_so_bao_ho():
    p = os.path.join(GOC, "runs", atc.MO_HINH_BAO_HO, "ket_qua.json")
    if not os.path.exists(p):
        return None
    k = json.load(open(p, encoding="utf-8"))
    ra = dict(mAP50=k.get("mAP50"), mAP50_95=k.get("mAP50_95"), theoLop=k.get("theo_lop"), boDuLieu=k.get("bo_du_lieu"),
              doTren=k.get("do_tren"))
    # F1 muc tung nguoi tren TEST theo co nguoi trong khung 1280x720: ca khung vs cat nguoi 2 tang (dang dung)
    kc = os.path.join(GOC, "runs", "danh_gia", "bao_ho_theo_khoang_cach.json")
    try:
        d = json.load(open(kc, encoding="utf-8"))
        k = d["ket_qua"][d["chon_mo_hinh"]]
        cu = d["ket_qua"].get("bao_ho", k)["ca_khung_luat_cu"]    # doi chung = HE CU: mo hinh bao_ho, ca khung, luat cu
        ra["f1TheoCoNguoi"] = {tc: {dk: dict(caKhung=cu[tc][dk]["f1"], catNguoi=k["cat_320"][tc]["test"][dk]["f1"])
                                    for dk in d["dieu_kien"]} for tc in ("mu", "ao")}
        ra["moHinhChon"] = d["chon_mo_hinh"]
    except (OSError, KeyError, ValueError):
        pass                                   # chua chay danh_gia_bao_ho_khoang_cach.py: chi tra chi so phat hien
    return ra


def _so(v, ten, tu=-1e9, den=1e9):
    """so huu han trong [tu, den] (JSON cho ca NaN / Infinity; bool khong tinh la so)"""
    if isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) or not tu <= v <= den:
        raise ValueError("cauHinh: %s phải là số trong [%g, %g]." % (ten, tu, den))
    return v


def _diem(ds, ten, so_toi_thieu, so_toi_da, trong_anh=True):
    if not isinstance(ds, list) or not so_toi_thieu <= len(ds) <= so_toi_da:
        raise ValueError("cauHinh: %s cần %d–%d điểm (đang có %s)." % (ten, so_toi_thieu, so_toi_da,
                                                                   len(ds) if isinstance(ds, list) else "sai kiểu"))
    for p in ds:
        if not isinstance(p, list) or len(p) != 2:
            raise ValueError("cauHinh: mỗi điểm của %s là [x, y]." % ten)
        for c in p:          # toa do anh 0..1 (le 5% cho diem bam sat mep); met: |c| <= 10 km
            _so(c, "tọa độ điểm của " + ten, -0.05 if trong_anh else -1e4, 1.05 if trong_anh else 1e4)


def kiem_cau_hinh(ch):
    """Chan cauHinh lam CHAM MOI camera (xu_ly cua moi phien chay tuan tu tren LUONG_AN_TOAN): so vung, so dinh, tong dinh
    vat-can, toa do huu han, nguong lam tang so buoc mo phong. Sai -> ValueError (400), cau hinh dang chay giu nguyen.
    Tra BAN SACH (chi cac truong may canh bao dung; khoa la bi bo) -> dua vao may canh bao + luu de tao lai
    phien (khong giu nguyen than yeu cau toi 16 MB trong RAM)."""
    if not isinstance(ch, dict):
        raise ValueError("cauHinh phải là đối tượng JSON.")
    # CHI giu khoa PhienAnToan.cau_hinh doc (vung, hieuChinh, nguong, gocNhin, caoCamera): giu moi khoa le kieu so / chuoi
    # thi than 16 MB {"a1": 0, "a2": 0, ...} = ~1,2 trieu khoa (do 10/2026: ~100 MB / phien) luu trong phien +
    # CAU_HINH_PHIEN (1 tai khoan: 16 phien + 32 ban luu ~ 5 GB), sao chep ~90 ms duoi KHOA_PHIEN o moi lan gop
    sach = {}
    if "vung" in ch:
        vung = ch["vung"] or []
        if not isinstance(vung, list) or len(vung) > GIOI_HAN_VUNG:
            raise ValueError("cauHinh: tối đa %d vùng (đang gửi %s)." % (GIOI_HAN_VUNG, len(vung) if isinstance(vung, list) else "sai kiểu"))
        sach["vung"], dinh_vat_can = [], 0
        for z in vung:
            if not isinstance(z, dict):
                raise ValueError("cauHinh: mỗi vùng là một đối tượng {ten, loai, diem}.")
            ten, loai = z.get("ten"), z.get("loai")
            if any(s is not None and (not isinstance(s, str) or len(s) > 100) for s in (ten, loai)):
                raise ValueError("cauHinh: tên / loại vùng là chuỗi <= 100 ký tự.")
            diem = z.get("diem") or []
            _diem(diem, "vùng '%s'" % (ten or "?"), 0, GIOI_HAN_DINH_VUNG)
            # gioiHan chi may canh bao doc o vung gioi-han-toc-do; vung khac giao dien van gui kem (o nhap bi an giu gia tri
            # cu, vd -5) -> bo, khong bat loi oan ca camera
            gioi_han = z.get("gioiHan") if loai == "gioi-han-toc-do" else None
            if gioi_han is not None:
                _so(gioi_han, "giới hạn tốc độ của vùng '%s'" % (ten or "?"), 0, 200)
            if loai == "vat-can":
                dinh_vat_can += len(diem)
            sach["vung"].append({k: z[k] for k in ("ten", "loai") if z.get(k) is not None} | {"diem": diem}
                                | ({"gioiHan": gioi_han} if gioi_han is not None else {}))
        if dinh_vat_can > GIOI_HAN_DINH_VAT_CAN:
            raise ValueError("cauHinh: tổng số điểm các vùng vật cản tối đa %d (đang có %d) — vẽ vùng vật cản gọn hơn."
                             % (GIOI_HAN_DINH_VAT_CAN, dinh_vat_can))
    if "hieuChinh" in ch:
        hc = ch["hieuChinh"] or None
        if hc is not None:
            if not isinstance(hc, dict):
                raise ValueError("cauHinh: hieuChinh là {anh: 4 điểm, met: 4 điểm}.")
            _diem(hc.get("anh"), "hiệu chỉnh (ảnh)", 4, 4)
            _diem(hc.get("met"), "hiệu chỉnh (mét)", 4, 4, trong_anh=False)
            hc = dict(anh=hc["anh"], met=hc["met"])
        sach["hieuChinh"] = hc
    if ch.get("nguong") is not None:
        ng = ch["nguong"]
        if not isinstance(ng, dict):
            raise ValueError("cauHinh: nguong là đối tượng {tên: giá trị}.")
        sach["nguong"] = {}
        for k, v in ng.items():
            if k in atc.NGUONG_MAC_DINH:                 # may canh bao bo qua khoa la -> khong luu
                if not isinstance(v, bool):
                    _so(v, "nguong." + k, *GIOI_HAN_NGUONG.get(k, (-1e9, 1e9)))
                sach["nguong"][k] = v
    for k in ("gocNhin", "caoCamera"):
        if ch.get(k) is not None:
            sach[k] = _so(ch[k], k)
    return sach


def _gop_cau_hinh(cu, moi):
    """PhienAnToan.cau_hinh cap nhat TUNG PHAN (nguong gop theo khoa; vung / hieuChinh / goc / cao thay ca) -> gop giong
    vay de luu dung cau hinh HIEU LUC (gui lai tung phan van khoi phuc du)"""
    ra = dict(cu or {})
    for k, v in moi.items():
        ra[k] = dict(ra.get("nguong") or {}, **v) if k == "nguong" and isinstance(v, dict) else v
    return ra


def _bo_phien(ma, now):
    """bo phien khoi bang (dang giu KHOA_PHIEN); cau hinh cua no chuyen sang CAU_HINH_PHIEN de tao lai khong mat"""
    p = PHIEN_AN_TOAN.pop(ma)
    if not p.get("cauHinh"):
        return
    CAU_HINH_PHIEN[(ma, p["nguoi"])] = dict(ch=p["cauHinh"], luc=now)
    for k in [k for k, v in CAU_HINH_PHIEN.items() if now - v["luc"] > GIU_CAU_HINH]:
        CAU_HINH_PHIEN.pop(k)
    cua_nguoi = [k for k in CAU_HINH_PHIEN if k[1] == p["nguoi"]]
    while len(cua_nguoi) > GIOI_HAN_CAU_HINH_NGUOI:      # 1 tai khoan khong day het cau hinh luu cua nguoi khac
        cu_nhat = min(cua_nguoi, key=lambda k: CAU_HINH_PHIEN[k]["luc"])
        CAU_HINH_PHIEN.pop(cu_nhat)
        cua_nguoi.remove(cu_nhat)
    while len(CAU_HINH_PHIEN) > GIOI_HAN_CAU_HINH:
        CAU_HINH_PHIEN.pop(min(CAU_HINH_PHIEN, key=lambda k: CAU_HINH_PHIEN[k]["luc"]))


def _phien_bo_duoc(ds, now):
    """phien it dung nhat CO THE bo trong ds: im >= PHIEN_DANG_CHAY giay va khong co khung dang xu ly; khong co -> None"""
    ds = [k for k in ds if not PHIEN_AN_TOAN[k]["dang"] and now - PHIEN_AN_TOAN[k]["luc"] >= PHIEN_DANG_CHAY]
    return min(ds, key=lambda k: PHIEN_AN_TOAN[k]["luc"]) if ds else None


def _tao_phien(ma, nguoi, fps, now):
    """lay cho cho phien moi (dang giu KHOA_PHIEN). Day: chi bo phien DA IM (uu tien cua chinh tai khoan); phien dang chay
    khong bao gio bi day ra -> het cho thi 429 ro rang (truoc day: day phien it dung nhat cua BAT KY AI, 17 camera luan
    phien day nhau lien tuc -> khung nao cung tao phien moi, khong canh bao duoc gi)"""
    cua_nguoi = [k for k, v in PHIEN_AN_TOAN.items() if v["nguoi"] == nguoi]
    if len(cua_nguoi) >= GIOI_HAN_PHIEN_NGUOI:
        bo = _phien_bo_duoc(cua_nguoi, now)
        if bo is None:
            raise QuaTai("Tài khoản đang giám sát %d camera / video cùng lúc (tối đa %d) — dừng bớt rồi thử lại."
                         % (len(cua_nguoi), GIOI_HAN_PHIEN_NGUOI))
        _bo_phien(bo, now)
    elif len(PHIEN_AN_TOAN) >= GIOI_HAN_PHIEN:
        bo = _phien_bo_duoc(cua_nguoi, now) or _phien_bo_duoc(list(PHIEN_AN_TOAN), now)
        if bo is None:
            raise QuaTai("Máy chủ AI đang giám sát đủ %d camera / video — thử lại sau." % GIOI_HAN_PHIEN)
        _bo_phien(bo, now)

    def vat_the(anh):
        with KHOA_GPU:
            with che_do_tf32(False):
                return MO_HINH["vat_the"](anh)

    def bao_ho(anh):
        with KHOA_GPU:
            with che_do_tf32(False):
                return MO_HINH["bao_ho"](anh)

    def bao_ho_theo_nguoi(anh_cat, co):
        with KHOA_GPU:
            with che_do_tf32(False):
                return MO_HINH["bao_ho"].theo_nguoi(anh_cat, co)
    bao_ho.theo_nguoi = bao_ho_theo_nguoi          # may canh bao thay ham nay -> cat tung nguoi (2 tang)

    phien = atc.PhienAnToan(vat_the, bao_ho if "bao_ho" in MO_HINH else None, fps=fps)
    luu = CAU_HINH_PHIEN.pop((ma, nguoi), None)    # cung ma phien + cung tai khoan: khoi phuc cau hinh hieu luc cuoi
    if luu:
        try:
            phien.cau_hinh(luu["ch"])              # phien moi chua co kich thuoc khung -> chi gan gia tri, nhanh
        except Exception as e:  # noqa: BLE001
            print("  bo cau hinh luu cua phien %s: %s" % (ma, e), flush=True)
            luu = None
    p = PHIEN_AN_TOAN[ma] = dict(phien=phien, luc=now, khoa=threading.Lock(), nguoi=nguoi, dang=0,
                                 cauHinh=luu["ch"] if luu else None)
    return p


def khung_an_toan(yc, nguoi):
    """1 khung camera / video -> ket qua may canh bao (an_toan_cang.PhienAnToan cua phien do)"""
    if not isinstance(yc, dict):
        raise ValueError("Thân yêu cầu phải là đối tượng JSON.")
    if "vat_the" not in MO_HINH:
        raise RuntimeError("Chưa có mô hình phát hiện người / xe.")
    ma = str(yc.get("phien") or "")[:64]
    if len(ma) < 8:
        raise ValueError("Thiếu mã phiên (phien) — mỗi camera / video một mã.")
    try:
        t = float(yc.get("t") if yc.get("t") is not None else time.time())
        fps = min(30.0, max(2.0, float(yc.get("fps") or 8)))
    except (TypeError, ValueError):
        raise ValueError("t (giây) và fps phải là số.") from None
    if not math.isfinite(t):
        raise ValueError("t (giây) phải là số hữu hạn.")
    ch = yc.get("cauHinh") or None
    if ch is not None:
        ch = kiem_cau_hinh(ch)               # truoc khi giai ma anh / tao phien: sai thi khong ton gi
    img = np.asarray(doc_anh(yc.get("anh"), GIOI_HAN_ANH_KHUNG))
    now = time.time()
    with KHOA_PHIEN:
        for k in [k for k, v in PHIEN_AN_TOAN.items() if now - v["luc"] > PHIEN_HET_HAN and not v["dang"]]:
            _bo_phien(k, now)                                           # bo phien bo do > 2 phut (giu cau hinh)
        p = PHIEN_AN_TOAN.get(ma)
        if p is not None and p["nguoi"] != nguoi:
            raise ValueError("Mã phiên đang được tài khoản khác dùng.")
        moi = p is None
        if moi:
            p = _tao_phien(ma, nguoi, fps, now)
        p["luc"] = now
        p["dang"] += 1                       # dang xu ly: khong bi don / day ra giua chung
    try:
        with p["khoa"]:                      # cung 1 phien: cac khung xu ly lan luot (bo theo vet co trang thai)
            if ch:
                p["phien"].cau_hinh(ch)
                with KHOA_PHIEN:             # ap dung xong moi luu (cau hinh loi khong de lai ban luu hong)
                    p["cauHinh"] = _gop_cau_hinh(p["cauHinh"], ch)
            t0 = time.perf_counter()
            kq = LUONG_AN_TOAN.submit(p["phien"].xu_ly, img, t).result()
    finally:
        with KHOA_PHIEN:
            p["dang"] -= 1
            p["luc"] = time.time()
    kq.update(ms=round((time.perf_counter() - t0) * 1000), rong=int(img.shape[1]), cao=int(img.shape[0]),
              coBaoHo="bao_ho" in MO_HINH)
    if moi and not ch:
        # HOP DONG voi giao dien: phien vua tao lai (het han / bi day ra / dich vu khoi dong lai) ma khung khong kem
        # cauHinh -> giao dien gui lai cau hinh o khung ke tiep (da tu khoi phuc ban luu neu con, gui lai cho chac)
        kq["canCauHinh"] = True
    return kq


def don_viec_cu():
    """bo viec da xong > 30 phut (giu RAM gon)"""
    with KHOA_VIEC:
        for ma in [m for m, v in VIEC_VIDEO.items() if v.get("xongLuc") and time.time() - v["xongLuc"] > 1800]:
            VIEC_VIDEO.pop(ma, None)


# ------------------------------------------------------------------ HTTP
class XuLy(BaseHTTPRequestHandler):
    server_version = "SmartPortAI/1.0"

    def _tra(self, ma, du_lieu):
        nd = json.dumps(du_lieu, ensure_ascii=False).encode("utf-8")
        self.send_response(ma)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(nd)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(nd)

    def log_message(self, fmt, *args):  # gon nhat ky (flush: stdout dang ghi ra tep, khong flush thi khong thay)
        sys.stdout.write("%s %s\n" % (self.address_string(), fmt % args))
        sys.stdout.flush()

    def _doc_than(self):
        """Doc than yeu cau: Content-Length HOAC Transfer-Encoding: chunked (HttpClient .NET gui JsonContent kieu chunked)"""
        if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
            phan, tong = [], 0
            while True:
                n = int(self.rfile.readline(1024).split(b";")[0].strip() or b"0", 16)
                if n == 0:
                    while self.rfile.readline(1024).strip():  # bo qua trailer
                        pass
                    return b"".join(phan)
                tong += n
                if tong > GIOI_HAN_THAN:
                    raise QuaLon()
                phan.append(self.rfile.read(n))
                self.rfile.readline(1024)                       # CRLF sau moi khuc
        dai = int(self.headers.get("Content-Length") or 0)
        if dai > GIOI_HAN_THAN:
            raise QuaLon()
        return self.rfile.read(dai) if dai else b""

    def _bo_than(self):
        """Tra loi SOM (401/400/429) khi chua doc than: than nho (<= 1 MB) thi doc bo; than lon / chunked thi dong ket noi.
        Dong ket noi khi con du lieu chua doc -> TCP reset -> cong API (YARP) bao 502 thay vi ma loi that."""
        dai = int(self.headers.get("Content-Length") or 0)
        if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower() or dai > (1 << 20):
            self.close_connection = True
        elif dai > 0:
            self.rfile.read(dai)

    def _luu_than_vao_tep(self, duong, gioi_han):
        """Ghi than yeu cau (tep video) thang ra dia theo tung khuc 1 MB — khong giu ca tep trong RAM"""
        tong = 0
        with open(duong, "wb") as f:
            if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
                while True:
                    n = int(self.rfile.readline(1024).split(b";")[0].strip() or b"0", 16)
                    if n == 0:
                        while self.rfile.readline(1024).strip():
                            pass
                        break
                    tong += n
                    if tong > gioi_han:
                        raise QuaLon()
                    while n > 0:
                        khuc = self.rfile.read(min(n, 1 << 20))
                        if not khuc:
                            raise ValueError("Tải video lên bị ngắt giữa chừng.")
                        f.write(khuc)
                        n -= len(khuc)
                    self.rfile.readline(1024)
            else:
                con = int(self.headers.get("Content-Length") or 0)
                if con > gioi_han:
                    raise QuaLon()
                while con > 0:
                    khuc = self.rfile.read(min(con, 1 << 20))
                    if not khuc:
                        raise ValueError("Tải video lên bị ngắt giữa chừng.")
                    f.write(khuc)
                    tong += len(khuc)
                    con -= len(khuc)
        if tong == 0:
            raise ValueError("Thiếu tệp video.")
        return tong

    def _tim_viec(self, phan, nguoi):
        ma = phan[len("/api/ai-thi-giac/video/"):].strip("/")
        with KHOA_VIEC:
            v = VIEC_VIDEO.get(ma)
        return v if v is not None and v["nguoi"] == nguoi else None    # chi nguoi tai len xem/dung duoc

    def _tai_video_len(self, nguoi, q):
        loai = (q.get("loai") or ["ca-hai"])[0]
        if loai not in ("ca-hai", "bien-so", "container"):
            self._bo_than()
            return self._tra(400, dict(thongBao="loai phải là ca-hai, bien-so hoặc container."))
        try:
            fps = float((q.get("fps") or ["3"])[0])
        except ValueError:
            fps = 3.0
        fps = min(10.0, max(0.5, fps))
        don_viec_cu()
        with KHOA_VIEC:
            if sum(1 for v in VIEC_VIDEO.values() if v["trangThai"] in ("dang-cho", "dang-xu-ly")) >= 5:
                self._bo_than()
                return self._tra(429, dict(thongBao="Đang có 5 video chờ xử lý — đợi xong rồi gửi tiếp."))
        os.makedirs(THU_MUC_VIDEO, exist_ok=True)
        ma = secrets.token_hex(12)
        tep = os.path.join(THU_MUC_VIDEO, ma + ".video")
        try:
            co = self._luu_than_vao_tep(tep, GIOI_HAN_VIDEO)
            cap, tt = ndv.thong_tin(tep)          # mo thu NGAY: video hong / qua dai bao loi luon, khong xep hang
            cap.release()
        except Exception:
            try:
                os.remove(tep)
            except OSError:
                pass
            raise
        viec = dict(ma=ma, nguoi=nguoi, loai=loai, fpsLay=fps, tep=tep, tenTep=unquote(self.headers.get("X-Ten-Tep") or "")[:120] or None,
                    kichThuoc=co, trangThai="dang-cho", huy=False, khung=[], msTong=0.0, taoLuc=time.time(),
                    khoa=threading.Lock(), **tt)
        with KHOA_VIEC:
            VIEC_VIDEO[ma] = viec
        threading.Thread(target=chay_viec_video, args=(viec,), daemon=True, name="video-" + ma).start()
        return self._tra(202, dict(ndv.anh_chup(viec), kichThuoc=co))

    def do_GET(self):
        phan, q = urlsplit(self.path).path, parse_qs(urlsplit(self.path).query)
        if phan.startswith("/api/ai-thi-giac/video/"):
            ok, ly_do = kiem_jwt(self.headers.get("Authorization"))
            if not ok:
                return self._tra(401, dict(thongBao=ly_do))
            viec = self._tim_viec(phan, ly_do)
            if viec is None:
                return self._tra(404, dict(thongBao="Không thấy việc xử lý video này (đã quá 30 phút hoặc dịch vụ AI vừa khởi động lại)."))
            try:
                tu = max(0, int((q.get("tu") or ["0"])[0]))
            except ValueError:
                tu = 0
            return self._tra(200, ndv.anh_chup(viec, tu))
        if phan.startswith("/api/ai-thi-giac/an-toan/thong-tin"):
            return self._tra(200, dict(moHinh=[m for m in ("vat_the", "bao_ho") if m in MO_HINH], nguong=atc.NGUONG_MAC_DINH,
                                       loaiRuiRo=atc.MO_TA, lopTheoDoi=atc.TEN_LOP, baoHo=chi_so_bao_ho(),
                                       cachLam="YOLO26s phát hiện → ByteTrack theo vết → đổi sang mét (homography / tự ước tỉ lệ) "
                                               "→ Kalman vận tốc → dự báo va chạm (TTC) + vùng + tốc độ + ngã + đồ bảo hộ"))
        if self.path.startswith("/suc-khoe"):
            return self._tra(200, dict(
                dichVu="ai-thi-giac", tieuDe="SmartPort – AI thị giác (Python)", cheDo="microservice", nenTang="python",
                tinhTrang="tốt" if MO_HINH else "lỗi", moHinh=sorted(MO_HINH), chiSoTest=chi_so(),
                gpu=torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU",
                chayTu=round(time.time() - KHOI_DONG), xacThucJwt=bool(KHOA_JWT)))
        if self.path.startswith("/api/ai-thi-giac/mo-hinh"):
            return self._tra(200, dict(moHinh=sorted(MO_HINH), chiSoTest=chi_so(),
                                       moTa={"bien": "YOLO26s tìm biển số", "ky_tu": "YOLO26s nhận diện từng ký tự (36 lớp)",
                                             "ky_tu_rfdetr": "RF-DETR nhận diện từng ký tự — gộp với YOLO26, giải mã theo mẫu biển VN",
                                             "vung_cont": "YOLO26s tìm vùng mã container (5 lớp)",
                                             "crnn": "CRNN + giải mã ràng buộc ISO 6346 + gộp chứng cứ nhiều vùng"},
                                       video=dict(dinhDang="mp4 (H.264/H.265), mov, avi, mkv, webm", toiDaMB=GIOI_HAN_VIDEO >> 20,
                                                  toiDaGiay=ndv.THOI_LUONG_TOI_DA, cachLam="OpenCV + FFmpeg lấy mẫu khung → đọc từng khung "
                                                  "→ gom theo đối tượng + bỏ phiếu theo thời gian"),
                                       nguongTinCayBienSo=NGUONG_TIN_CAY_BIEN, nguongVideoContainer=ndv.NGUONG_CHAC_CONT,
                                       baoHo=chi_so_bao_ho()))
        return self._tra(404, dict(thongBao="Không có đường dẫn này."))

    def do_DELETE(self):
        ok, ly_do = kiem_jwt(self.headers.get("Authorization"))
        if not ok:
            return self._tra(401, dict(thongBao=ly_do))
        phan = urlsplit(self.path).path
        viec = self._tim_viec(phan, ly_do) if phan.startswith("/api/ai-thi-giac/video/") else None
        if viec is None:
            return self._tra(404, dict(thongBao="Không thấy việc xử lý video này."))
        viec["huy"] = True
        return self._tra(200, dict(thongBao="Đã dừng nhận diện video.", maViec=viec["ma"]))

    def do_POST(self):
        ok, ly_do = kiem_jwt(self.headers.get("Authorization"))
        if not ok:
            self._bo_than()                                     # than nho: doc bo; than video lon: dong ket noi
            return self._tra(401, dict(thongBao=ly_do))
        try:
            phan = urlsplit(self.path)
            if phan.path.rstrip("/") == "/api/ai-thi-giac/video":
                return self._tai_video_len(ly_do, parse_qs(phan.query))
            la_bien = phan.path.startswith("/api/ai-thi-giac/bien-so")
            if not (la_bien or phan.path.startswith("/api/ai-thi-giac/container")
                    or phan.path.rstrip("/") == "/api/ai-thi-giac/an-toan/khung"):
                self._bo_than()
                return self._tra(404, dict(thongBao="Không có đường dẫn này."))
            yc = json.loads(self._doc_than() or b"{}")
            if not isinstance(yc, dict):
                raise ValueError("Thân yêu cầu phải là đối tượng JSON.")
            if phan.path.rstrip("/") == "/api/ai-thi-giac/an-toan/khung":
                return self._tra(200, khung_an_toan(yc, ly_do))
            if not CHO_ANH_TINH.acquire(timeout=20):
                raise QuaTai("Máy chủ AI đang bận xử lý ảnh khác — thử lại sau ít giây.")
            try:
                img = doc_anh(yc.get("anh"), GIOI_HAN_ANH_TINH)
                with KHOA_GPU:
                    with che_do_tf32(la_bien):
                        return self._tra(200, nhan_bien_so(img) if la_bien else nhan_container(img))
            finally:
                CHO_ANH_TINH.release()
        except QuaLon:
            self.close_connection = True                        # chua doc het than -> khong dung lai ket noi
            return self._tra(413, dict(thongBao="Yêu cầu quá lớn."))
        except QuaTai as e:
            return self._tra(429, dict(thongBao=str(e)))
        except ValueError as e:
            return self._tra(400, dict(thongBao=str(e)))
        except OSError as e:
            # OSError (PIL khong nhan ra anh / anh cut, ghi tep video tam...) kem DUONG DAN may chu / dia chi bo nho
            # ("<_io.BytesIO object at 0x...>") -> nguoi goi chi nhan loai loi; chi tiet ghi nhat ky
            print("  [400] %s: %s" % (type(e).__name__, str(e)[:300]), flush=True)
            return self._tra(400, dict(thongBao="Không đọc được ảnh / tệp gửi lên — hỏng hoặc sai định dạng (%s)."
                                                % type(e).__name__))
        except RuntimeError as e:
            return self._tra(503, dict(thongBao=str(e)))
        except Exception as e:  # noqa: BLE001  loi khong luong truoc: tra 500 ro rang (truoc: dong ket noi -> gateway 502)
            traceback.print_exc()
            sys.stdout.flush()
            self.close_connection = True
            return self._tra(500, dict(thongBao="Lỗi máy chủ AI: %s" % type(e).__name__))


class MayChuIPv6(ThreadingHTTPServer):
    address_family = socket.AF_INET6


def chay_het_toc_do():
    """Windows 11 + CPU lai (loi P / loi E): tien trinh chay NEN, cua so an (dich vu khoi dong bang script) bi "power
    throttling / EcoQoS" xep len loi tiet kiem dien -> phan CPU cua AI (giai ma anh, theo vet, tien / hau xu ly YOLO) cham
    gap nhieu lan. Xin tat throttling cho tien trinh nay (SetProcessInformation, ProcessPowerThrottling = 4)."""
    if sys.platform != "win32":
        return
    try:
        import ctypes
        from ctypes import wintypes

        class ThrottlingState(ctypes.Structure):
            _fields_ = [("Version", wintypes.ULONG), ("ControlMask", wintypes.ULONG), ("StateMask", wintypes.ULONG)]
        # Version 1; ControlMask = EXECUTION_SPEED (0x1); StateMask 0 = KHONG throttle
        st = ThrottlingState(1, 0x1, 0x0)
        k32 = ctypes.WinDLL("kernel32", use_last_error=True)
        k32.GetCurrentProcess.restype = wintypes.HANDLE            # handle 64 bit: khai bao kieu, khong ctypes cat con 32 bit
        k32.SetProcessInformation.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
        k32.SetProcessInformation.restype = wintypes.BOOL
        ok = k32.SetProcessInformation(k32.GetCurrentProcess(), 4, ctypes.byref(st), ctypes.sizeof(st))
        print("  power throttling: %s" % ("tat (chay het toc do)" if ok else "khong doi duoc (loi %d)" % ctypes.get_last_error()),
              flush=True)
    except Exception as e:  # noqa: BLE001
        print("  power throttling: bo qua (%s)" % e, flush=True)


if __name__ == "__main__":
    print("Dich vu AI thi giac — nap mo hinh...", flush=True)
    chay_het_toc_do()
    nap_mo_hinh()
    may = ThreadingHTTPServer(("127.0.0.1", CONG), XuLy)
    # Nghe them ::1: tren Windows "localhost" phan giai ra ::1 truoc — chi nghe IPv4 thi moi yeu cau goi "localhost"
    # mat ~2 giay cho bi tu choi roi moi thu 127.0.0.1. Chi loopback, khong mo ra mang LAN.
    try:
        may6 = MayChuIPv6(("::1", CONG), XuLy)
        threading.Thread(target=may6.serve_forever, daemon=True).start()
    except OSError as e:
        print("  khong nghe duoc ::1 (%s) — chi IPv4" % e, flush=True)
    print("San sang: http://127.0.0.1:%d (JWT: %s; thu hoi token: %s)" % (
        CONG, "bat, iss=%s aud=%s" % (JWT_ISS, JWT_AUD) if KHOA_JWT else "TAT — che do phat trien",
        "hoi dinh-danh " + DINH_DANH if DINH_DANH else "KHONG hoi (khong thay dinh-danh / AI_DINH_DANH=tat)"), flush=True)
    may.serve_forever()
