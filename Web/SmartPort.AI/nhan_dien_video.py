# -*- coding: utf-8 -*-
"""
NHAN DIEN TREN VIDEO — dung lai dung 2 bo doc ANH cua dich_vu_ai (bien so + ma container), them chieu THOI GIAN:
  1. Doc video bang OpenCV (kem san FFmpeg): mp4 (H.264/H.265), mov, avi, mkv, webm. Video quay doc tren dien thoai
     duoc OpenCV tu xoay dung chieu.
  2. LAY MAU khung: moi giay lay `fps` khung (mac dinh 3). Xe qua cong mat vai giay -> 3 khung/giay du thay bien/ma
     nhieu lan; doc ca 30 khung/giay chi ton GPU (2 khung lien tiep gan nhu giong het nhau).
  3. Moi khung lay mau chay dung bo doc anh: YOLO26 tim bien / vung ma -> doc ky tu (YOLO26 + RF-DETR) / CRNN
     -> rang buoc mau bien VN / so kiem tra ISO 6346.
  4. GOM THEO DOI TUONG (theo vet don gian): ket qua cac khung GAN NHAU ve thoi gian cua cung 1 xe / 1 container
     (chuoi gan giong — sai khac <= 2 ky tu — hoac khung bien trung vi tri) gom thanh 1 doi tuong.
  5. BO PHIEU THEO THOI GIAN: moi khung bo 1 phieu, trong so = do tin cay cua khung -> chuoi nhieu phieu nhat thang.
     Khung mo / bi che / nghieng doc sai 1 ky tu bi cac khung ro "bau lai" -> ket qua video tin cay hon 1 anh le.
Han che: moi khung chi lay 1 bien so (bien ro nhat) va 1 ma container (bo doc anh chon ma tot nhat) — hop voi camera
cong (moi luot 1 xe). Video nhieu xe CUNG luc trong 1 khung can them bo theo vet nhieu doi tuong (ByteTrack...).
"""
import base64
import io
import math
import os
import time

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

CANH_TOI_DA = 1920            # khung lon hon (4K) thu nho: bo tim van chay o 640/1024 -> chi ton thoi gian + bo nho
THOI_LUONG_TOI_DA = 600       # giay — video dai hon: tu choi (bao cat ngan)
KHUNG_TOI_DA = 1800           # so khung lay mau toi da / video
KHOANG_NOI = {"bien-so": 2.5, "container": 4.0}   # giay: mat dau qua khoang nay coi nhu doi tuong moi
# 1 khung doc ma container chi tinh la CHAC khi do tin cay >= nguong nay (anh le dung nguong rong hon vi anh cong luon co
# container; video co nhieu khung duong trong / dau keo de "nan" ra ma dung ISO voi tin cay ~0). CHON TREN VAL: giu >= 99%
# ma doc dung (chon_nguong_video_container.py -> runs/danh_gia/nguong_video_container.json).
NGUONG_CHAC_CONT = 0.421
MAU_LOP = {"ma_ngang": (239, 68, 68), "ma_doc": (249, 115, 22), "so_huu": (34, 197, 94), "seri": (6, 182, 212),
           "ma_loai": (217, 70, 239)}


def thong_tin(duong):
    """Mo video, tra (cap, dict thong tin) hoac nem ValueError voi thong bao tieng Viet"""
    cap = cv2.VideoCapture(duong)
    if not cap.isOpened():
        raise ValueError("Không đọc được video — định dạng không hỗ trợ hoặc tệp hỏng.")
    fps = cap.get(cv2.CAP_PROP_FPS)
    if not fps or math.isnan(fps) or fps <= 0 or fps > 240:
        fps = 30.0
    n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    ok, khung = cap.read()
    if not ok or khung is None:
        cap.release()
        raise ValueError("Không giải mã được khung hình nào — video hỏng hoặc dùng codec lạ.")
    cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
    h, w = khung.shape[:2]
    thoi_luong = n / fps if n > 0 else None
    if thoi_luong and thoi_luong > THOI_LUONG_TOI_DA:
        cap.release()
        raise ValueError("Video dài %.0f giây — tối đa %d phút, hãy cắt ngắn đoạn có xe/container." % (thoi_luong, THOI_LUONG_TOI_DA // 60))
    return cap, dict(fpsVideo=round(fps, 2), soKhungVideo=n, thoiLuong=round(thoi_luong, 2) if thoi_luong else None, rong=w, cao=h)


# ------------------------------------------------------------------ gom theo doi tuong + bo phieu
def sai_khac(a, b):
    """khoang cach Levenshtein (so ky tu phai them/xoa/thay)"""
    if a == b:
        return 0
    truoc = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        nay = [i]
        for j, cb in enumerate(b, 1):
            nay.append(min(truoc[j] + 1, nay[j - 1] + 1, truoc[j - 1] + (ca != cb)))
        truoc = nay
    return truoc[-1]


def iou(a, b):
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    giao = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    hop = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - giao
    return giao / hop if hop > 0 else 0.0


class DoiTuong:
    """1 xe (bien so) hoac 1 container xuat hien lien tuc trong 1 doan video"""

    def __init__(self, ma, loai, t):
        self.ma, self.loai = ma, loai
        self.bat_dau = self.ket_thuc = t
        self.phieu = {}               # chuoi -> [tong trong so, so khung, so khung "chac"]
        self.tc = {}                  # chuoi -> list do tin cay tung khung
        self.hop_cuoi = None
        self.tot = None               # (do tin cay, t, anh PIL, [(hop, mau, nhan)]) — khung ro nhat de lam anh minh hoa

    def them(self, t, chuoi, tc, chac, hop, anh, ve):
        p = self.phieu.setdefault(chuoi, [0.0, 0, 0])
        p[0] += max(tc, 0.01)
        p[1] += 1
        p[2] += int(chac)
        self.tc.setdefault(chuoi, []).append(tc)
        self.ket_thuc = t
        if hop is not None:
            self.hop_cuoi = hop
        if self.tot is None or tc > self.tot[0]:
            self.tot = (tc, t, anh, ve)

    def thang(self):
        return max(self.phieu, key=lambda c: (self.phieu[c][0], self.phieu[c][1]))

    def ket_qua(self, kem_anh):
        c = self.thang()
        tong = sum(p[0] for p in self.phieu.values())
        chia_se = self.phieu[c][0] / tong
        tc_tb = float(np.mean(self.tc[c]))
        ra = dict(ma=self.ma, loai=self.loai, giaTri=c,
                  # chac: it nhat 1 khung doc DUNG chuan (bien du tin cay / ma dung ISO 6346) VA chuoi thang chiem >= 1/2 phieu
                  chac=self.phieu[c][2] > 0 and chia_se >= 0.5,
                  doTinCay=round(chia_se * tc_tb, 3), tyLePhieu=round(chia_se, 3),
                  soKhung=sum(p[1] for p in self.phieu.values()), soKhungDongY=self.phieu[c][1],
                  batDau=round(self.bat_dau, 2), ketThuc=round(self.ket_thuc, 2), tKhungRoNhat=round(self.tot[1], 2),
                  phieu=[dict(giaTri=k, soKhung=v[1], trongSo=round(v[0], 3))
                         for k, v in sorted(self.phieu.items(), key=lambda kv: -kv[1][0])[:4]])
        if kem_anh:
            ra["anh"] = anh_minh_hoa(self.tot[2], self.tot[3])
        return ra


class BoGom:
    """Gom ket qua tung khung thanh doi tuong (theo vet don gian theo thoi gian)"""

    def __init__(self, loai, tien_to):
        self.loai, self.tien_to = loai, tien_to
        self.ds = []

    def them(self, t, chuoi, tc, chac, hop, anh, ve):
        ung_vien = []
        for d in self.ds:
            if t - d.ket_thuc > KHOANG_NOI[self.loai]:
                continue
            k = min(sai_khac(chuoi, c) for c in d.phieu)
            cung_cho = hop is not None and d.hop_cuoi is not None and iou(hop, d.hop_cuoi) >= 0.3
            if k <= 2 or cung_cho:
                ung_vien.append((k, -d.ket_thuc, d))
        if ung_vien:
            d = min(ung_vien, key=lambda x: (x[0], x[1]))[2]
        else:
            d = DoiTuong("%s%d" % (self.tien_to, len(self.ds) + 1), self.loai, t)
            self.ds.append(d)
        d.them(t, chuoi, tc, chac, hop, anh, ve)

    def ket_qua(self, kem_anh=False):
        ra = []
        for d in self.ds:
            r = d.ket_qua(kem_anh)
            # nhieu: chua khung nao doc chac chan VA chuoi thang chi thay o 1 khung -> bo (thuong la chu/bien la thoang qua)
            if r["chac"] or r["soKhungDongY"] >= 2:
                ra.append(r)
        return ra


_FONT = {}


def _font(co):
    if co not in _FONT:
        try:
            _FONT[co] = ImageFont.truetype(os.path.join(r"C:\Windows\Fonts", "segoeuib.ttf"), co)
        except OSError:
            _FONT[co] = ImageFont.load_default()
    return _FONT[co]


def anh_minh_hoa(anh, ve, rong=560):
    """khung ro nhat + khung vung phat hien -> JPEG data URL nho (hien trong danh sach ket qua)"""
    ti = min(1.0, rong / anh.width)
    img = anh.resize((round(anh.width * ti), round(anh.height * ti)), Image.LANCZOS) if ti < 1 else anh.copy()
    v = ImageDraw.Draw(img)
    f = _font(15)
    for muc in ve:                    # (hop, mau, nhan[, ve_khung])
        hop, mau, nhan = muc[:3]
        x1, y1, x2, y2 = [c * ti for c in hop]
        if len(muc) < 4 or muc[3]:
            v.rectangle([x1, y1, x2, y2], outline=mau, width=3)
        if nhan:
            w = v.textlength(nhan, font=f) + 10
            y = y1 - 22 if y1 >= 22 else y2 + 2
            v.rectangle([x1, y, x1 + w, y + 21], fill=mau)
            v.text((x1 + 5, y + 2), nhan, font=f, fill=(255, 255, 255))
    b = io.BytesIO()
    img.save(b, "JPEG", quality=82)
    return "data:image/jpeg;base64," + base64.b64encode(b.getvalue()).decode()


# ------------------------------------------------------------------ vong xu ly
def _ghi_khung(viec, bo_bien, bo_cont, t, ti, img, kb, kc, ms):
    """ghi ket qua 1 khung: ban ghi tung khung (giao dien ve khung de len video) + bo phieu vao doi tuong"""
    goc = lambda hop: [round(v / ti, 1) for v in hop]  # noqa: E731 — toa do theo khung GOC cua video
    ban_ghi = dict(t=round(t, 3), ms=round(ms))
    if kb and kb.get("hop"):
        chuoi = kb.get("bienSo") or kb.get("bienSoDoan")
        chac = bool(kb.get("bienSo"))
        ban_ghi["bien"] = dict(hop=goc(kb["hop"]), giaTri=chuoi, chac=chac, doTinCay=kb.get("doTinCay"))
        if chuoi:
            mau = (34, 197, 94) if chac else (234, 179, 8)
            bo_bien.them(t, chuoi, float(kb.get("doTinCay") or 0), chac, kb["hop"], img, [(kb["hop"], mau, chuoi)])
    if kc and kc.get("vung"):
        vung = [v for v in kc["vung"] if v.get("conf") is not None]      # bo vung "ca anh" du phong
        tc = float(kc.get("doTinCay") or 0)
        chac = bool(kc.get("soCont")) and tc >= NGUONG_CHAC_CONT
        ban_ghi["cont"] = dict(vung=[dict(lop=v["lop"], hop=goc(v["hop"])) for v in vung], giaTri=kc.get("soCont"), chac=chac,
                               doTinCay=kc.get("doTinCay"))
        if kc.get("soCont") and vung:
            ve = [(v["hop"], MAU_LOP.get(v["lop"], (59, 130, 246)), None) for v in vung]
            x1 = min(v["hop"][0] for v in vung)
            y1 = min(v["hop"][1] for v in vung)
            ve.append(([x1, y1, x1, y1], (220, 38, 38), kc["soCont"], False))   # chi ghi nhan ma o goc vung ma
            bo_cont.them(t, kc["soCont"], tc, chac, None, img, ve)
    viec["khung"].append(ban_ghi)
    viec["msTong"] += ms


def xu_ly(viec, doc_khung):
    """viec: dict trang thai (dich_vu_ai giu, giao dien hoi tien do). doc_khung(img PIL) -> (kq bien | None, kq cont | None)
    — chay dung bo doc anh (giu khoa GPU tung khung nen yeu cau doc ANH van chen vao duoc giua chung)."""
    cap = None
    try:
        cap, tt = thong_tin(viec["tep"])
        viec.update(tt)
        fps_v = tt["fpsVideo"]
        buoc = max(1, round(fps_v / viec["fpsLay"]))
        viec["buocKhung"] = buoc
        viec["tongKhung"] = min(KHUNG_TOI_DA, math.ceil(tt["soKhungVideo"] / buoc)) if tt["soKhungVideo"] > 0 else None
        viec["trangThai"] = "dang-xu-ly"
        viec["batDauXuLy"] = time.time()
        bo_bien, bo_cont = BoGom("bien-so", "B"), BoGom("container", "C")
        viec["_bo"] = (bo_bien, bo_cont)
        i = 0
        while not viec["huy"] and len(viec["khung"]) < KHUNG_TOI_DA:
            if not cap.grab():
                break
            i += 1
            if (i - 1) % buoc:
                continue
            ok, khung = cap.retrieve()
            if not ok or khung is None:
                continue
            t = (i - 1) / fps_v
            h, w = khung.shape[:2]
            ti = min(1.0, CANH_TOI_DA / max(h, w))
            if ti < 1:
                khung = cv2.resize(khung, (round(w * ti), round(h * ti)), interpolation=cv2.INTER_AREA)
            img = Image.fromarray(cv2.cvtColor(khung, cv2.COLOR_BGR2RGB))
            t0 = time.perf_counter()
            kb, kc = doc_khung(img)
            ms = (time.perf_counter() - t0) * 1000
            with viec["khoa"]:                # luong HTTP dang doc tien do cung luc
                _ghi_khung(viec, bo_bien, bo_cont, t, ti, img, kb, kc, ms)
        if not viec["khung"] and not viec["huy"]:          # dung truoc khung dau tien: "da-huy", khong phai loi
            raise ValueError("Không giải mã được khung hình nào của video.")
        viec["trangThai"] = "da-huy" if viec["huy"] else "xong"
    except ValueError as e:
        viec["trangThai"], viec["thongBao"] = "loi", str(e)
    except Exception as e:  # noqa: BLE001 — loi bat ngo: bao ra giao dien, khong lam sap dich vu
        viec["trangThai"], viec["thongBao"] = "loi", "Lỗi xử lý video: %s" % str(e)[:200]
    finally:
        if cap is not None:
            cap.release()
        try:
            os.remove(viec["tep"])
        except OSError:
            pass
        viec["xongLuc"] = time.time()
        if "_bo" in viec:
            # anh minh hoa chi tao 1 lan khi xong (ve + nen JPEG ton CPU, khong lam o moi lan hoi tien do)
            cuoi = sorted([r for bo in viec["_bo"] for r in bo.ket_qua(kem_anh=True)], key=lambda r: r["batDau"])
            with viec["khoa"]:
                viec["doiTuongCuoi"] = cuoi
                viec.pop("_bo")


def anh_chup(viec, tu=0):
    """trang thai gui giao dien (JSON). tu: chi gui ket qua tung khung tu chi so nay (giao dien hoi lien tuc)"""
    with viec["khoa"]:
        bo = viec.get("_bo")
        cuoi = viec.get("doiTuongCuoi")
        xong = cuoi is not None or (viec["trangThai"] in ("xong", "loi", "da-huy") and bo is None)
        doi_tuong = cuoi if cuoi is not None else (sorted([r for b in bo for r in b.ket_qua()], key=lambda r: r["batDau"]) if bo else [])
        khung = viec["khung"][tu:]
        da = len(viec["khung"])
    tong = viec.get("tongKhung")
    # xong = da co ket qua CUOI (kem anh minh hoa) -> giao dien thoi hoi
    return dict(maViec=viec["ma"], xong=xong, trangThai=viec["trangThai"],
                thongBao=viec.get("thongBao"), tenTep=viec.get("tenTep"),
                loai=viec["loai"], fpsLay=viec["fpsLay"], fpsVideo=viec.get("fpsVideo"), thoiLuong=viec.get("thoiLuong"),
                rong=viec.get("rong"), cao=viec.get("cao"), khungDaXuLy=da, tongKhung=tong,
                tienDo=1.0 if xong else (round(min(0.99, da / tong), 3) if tong else None),
                msMoiKhung=round(viec["msTong"] / da) if da else None,
                giayXuLy=round((viec.get("xongLuc") or time.time()) - viec["batDauXuLy"], 1) if viec.get("batDauXuLy") else None,
                viTriHangDoi=viec.get("viTriHangDoi"), doiTuong=doi_tuong, tu=tu, khung=khung)
