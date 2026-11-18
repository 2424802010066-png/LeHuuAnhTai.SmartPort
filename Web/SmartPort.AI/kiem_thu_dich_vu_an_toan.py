# -*- coding: utf-8 -*-
"""
KIEM THU DICH VU AI (dich_vu_ai.py) — phan CANH BAO NGUY HIEM + bao mat. Bo phat hien GIA (khong GPU, khong nap mo hinh),
may chu dinh-danh GIA tren 127.0.0.1 cong ngau nhien (khong goi mang ngoai, khong ghi CSDL). Hoi quy cac loi da sua:
  1. phien het han / bi day ra -> tao lai VOI cau hinh cu (vung cam, hieu chinh, nguong) + canCauHinh=true cho giao dien
  2. bang phien: moi tai khoan <= 16, phien dang chay KHONG bi day ra, het cho -> 429 ro rang (17 camera van chay)
  3. nguong bo phat hien nguoi / xe lay tu an_toan_cang.NGUONG_PHAT_HIEN (ByteTrack ghep duoc hop diem thap khi bi che)
  4. anh khai kich thuoc khong lo / bom nen -> 400 ro rang (khong giai ma ra hang GB, khong mat phan hoi -> 502)
  5. cauHinh qua nhieu vung / dinh / nguong lam cham MOI camera -> 400, cau hinh dang chay giu nguyen
  6. JWT: iss / aud + THU HOI token qua dinh-danh (bo dem 60 s; dinh-danh sap -> trang thai biet gan nhat)
Chay: python kiem_thu_dich_vu_an_toan.py   (ma thoat 0 = dat het)
"""
import base64
import hashlib
import hmac
import http.client
import io
import json
import math
import os
import sys
import threading
import time
import urllib.error
import urllib.request
import warnings
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

warnings.filterwarnings("ignore")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import dich_vu_ai as dv  # noqa: E402

ket_qua = []


def kiem(ten, dk, ct=""):
    ket_qua.append(bool(dk))
    print(("  OK  " if dk else "  LOI ") + ten + ("" if dk else "  -> " + str(ct)), flush=True)


def loi_cua(ham, *a):
    """chay ham, tra ngoai le (hoac None)"""
    try:
        ham(*a)
    except Exception as e:  # noqa: BLE001
        return e
    return None


# ---------------------------------------------------------------------------------------------- bo phat hien gia
def gia_vat_the(_img):
    # 1 nguoi dung yen giua anh 640x480 (chan y = 400), diem 0.9, lop COCO 0
    return np.array([[300.0, 250.0, 340.0, 400.0, 0.9, 0.0]])


dv.MO_HINH.clear()
dv.MO_HINH["vat_the"] = gia_vat_the
dv.KHOA_JWT = ""                         # che do phat trien cho phan 1-5 (phan 6 bat lai)


def anh_b64(img, dang="JPEG"):
    buf = io.BytesIO()
    img.save(buf, dang)
    return "data:image/%s;base64,%s" % (dang.lower(), base64.b64encode(buf.getvalue()).decode())


ANH = anh_b64(Image.new("RGB", (640, 480), "gray"))
VUNG = [{"ten": "Dưới cần trục", "loai": "cam-nguoi", "diem": [[0.3, 0.6], [0.7, 0.6], [0.7, 1.0], [0.3, 1.0]]}]
DONG_HO = {}


def gui(ma, nguoi="A", cau_hinh=None):
    """1 khung cua phien ma (dong ho rieng moi phien, 8 khung/giay)"""
    t = DONG_HO.get(ma, 0.0)
    DONG_HO[ma] = t + 0.125
    yc = dict(phien=ma, anh=ANH, t=t, fps=8)
    if cau_hinh is not None:
        yc["cauHinh"] = cau_hinh
    return dv.khung_an_toan(yc, nguoi)


def rui_ro(kq):
    return [r["loai"] for r in kq["ruiRo"]]


def lam_moi():
    dv.PHIEN_AN_TOAN.clear()
    dv.CAU_HINH_PHIEN.clear()
    DONG_HO.clear()


# ---------------------------------------------------------------------------------------------- 1. tao lai phien
print("== 1. Phiên hết hạn / bị đẩy ra: tạo lại KHÔNG mất cấu hình ==")
lam_moi()
MA = "camera-A-0001"
ds = [gui(MA, cau_hinh={"vung": VUNG} if i == 0 else None) for i in range(20)]
kiem("Phiên mới gửi kèm cấu hình: không đòi gửi lại cấu hình (không có canCauHinh)", not any("canCauHinh" in k for k in ds))
kiem("Người đứng trong vùng cấm → 'vao-vung-cam'", "vao-vung-cam" in rui_ro(ds[-1]), rui_ro(ds[-1]))
dv.PHIEN_AN_TOAN[MA]["luc"] -= dv.PHIEN_HET_HAN + 1          # camera IP mat tin hieu / video tam dung > 2 phut
kq = gui(MA)
kiem("Phiên quá hạn bị tạo lại → canCauHinh=true (giao diện gửi lại cấu hình)", kq.get("canCauHinh") is True, kq.get("canCauHinh"))
kiem("Phiên tạo lại TỰ khôi phục vùng cấm đã cấu hình", len(dv.PHIEN_AN_TOAN[MA]["phien"].vung) == 1,
     dv.PHIEN_AN_TOAN[MA]["phien"].vung)
ds = [gui(MA) for _ in range(20)]
kiem("Sau khi tạo lại vẫn cảnh báo vào vùng cấm (trước đây: ruiRo = [] im lặng)", "vao-vung-cam" in rui_ro(ds[-1]), rui_ro(ds[-1]))
kiem("Chỉ khung đầu của phiên tạo lại mới có canCauHinh", not any("canCauHinh" in k for k in ds))

MA3 = "camera-A-0003"
gui(MA3, cau_hinh={"vung": VUNG, "nguong": {"toc_do_toi_da": 20},
                   "hieuChinh": {"anh": [[0.1, 0.9], [0.9, 0.9], [0.7, 0.5], [0.3, 0.5]], "met": [[0, 0], [10, 0], [10, 20], [0, 20]]}})
gui(MA3, cau_hinh={"nguong": {"can_bao_ho": False}})          # gui TUNG PHAN: nguong gop theo khoa
dv.PHIEN_AN_TOAN[MA3]["luc"] -= dv.PHIEN_HET_HAN + 1
kq = gui(MA3)
ph = dv.PHIEN_AN_TOAN[MA3]["phien"]
kiem("Khôi phục ĐỦ cấu hình gửi từng phần: vùng + hiệu chỉnh + 2 ngưỡng",
     len(ph.vung) == 1 and ph.hieu_chinh is not None and ph.nguong["toc_do_toi_da"] == 20 and ph.nguong["can_bao_ho"] is False,
     (len(ph.vung), ph.hieu_chinh, ph.nguong["toc_do_toi_da"], ph.nguong["can_bao_ho"]))
kiem("Hiệu chỉnh khôi phục có hiệu lực (mặt đất theo homography)", kq["matDat"] == "homography" or gui(MA3)["matDat"] == "homography")

e = loi_cua(gui, MA3, "A", {"vung": VUNG * (dv.GIOI_HAN_VUNG + 1)})
kiem("Cấu hình sai (quá số vùng) → 400, cấu hình đang chạy giữ nguyên",
     isinstance(e, ValueError) and len(dv.PHIEN_AN_TOAN[MA3]["phien"].vung) == 1, e)

dv.PHIEN_AN_TOAN[MA]["luc"] -= dv.PHIEN_HET_HAN + 1
kq = gui(MA, nguoi="B")                                        # B dung lai ma phien cua A sau khi phien A het han
kiem("Tài khoản khác dùng lại mã phiên: KHÔNG nhận cấu hình của tài khoản cũ",
     dv.PHIEN_AN_TOAN[MA]["phien"].vung == [] and kq.get("canCauHinh") is True, dv.PHIEN_AN_TOAN[MA]["phien"].vung)
kiem("Cấu hình của A vẫn được giữ cho A", (MA, "A") in dv.CAU_HINH_PHIEN)
kq = gui("camera-moi-hoan-toan")
kiem("Phiên mới tinh mà khung không kèm cấu hình → canCauHinh=true", kq.get("canCauHinh") is True)
lon = {"vung": [dict(VUNG[0], rac="x" * 50000)], "rac": list(range(50000)), "nguong": {"khong_co": 1, "toc_do_toi_da": 12}}
gui("camera-A-rac-01", cau_hinh=lon)
luu = dv.PHIEN_AN_TOAN["camera-A-rac-01"]["cauHinh"]
kiem("Chỉ lưu trường máy cảnh báo dùng (không giữ rác trong thân yêu cầu)",
     "rac" not in luu and "rac" not in luu["vung"][0] and luu["nguong"] == {"toc_do_toi_da": 12}, str(luu)[:200])

# ---------------------------------------------------------------------------------------------- 2. bang phien
print("== 2. Bảng phiên: không đẩy phiên đang chạy, giới hạn theo tài khoản, 429 rõ ràng ==")
lam_moi()
cam = [("cam-A-%02d" % i, "A") for i in range(16)] + [("cam-B-00", "B")]
kq_cuoi = {}
for vong in range(30):                                         # 17 camera chay luan phien (truoc day: thrash, khung = 1)
    for ma, ng in cam:
        kq_cuoi[ma] = gui(ma, ng, {"vung": VUNG} if vong == 0 else None)
kiem("17 camera chạy luân phiên 30 vòng: mọi phiên giữ trạng thái (khung = 30)",
     all(k["khung"] == 30 for k in kq_cuoi.values()), sorted({k["khung"] for k in kq_cuoi.values()}))
kiem("17 camera: camera nào cũng cảnh báo vùng cấm", all("vao-vung-cam" in rui_ro(k) for k in kq_cuoi.values()))
e = loi_cua(gui, "cam-A-16", "A")
kiem("Tài khoản A mở phiên thứ 17 khi 16 phiên đều đang chạy → 429 (QuaTai) kèm lý do",
     isinstance(e, dv.QuaTai) and "tối đa %d" % dv.GIOI_HAN_PHIEN_NGUOI in str(e), e)
for i in range(1, 16):
    gui("cam-B-%02d" % i, "B")
kiem("Bảng đủ %d phiên" % dv.GIOI_HAN_PHIEN, len(dv.PHIEN_AN_TOAN) == dv.GIOI_HAN_PHIEN, len(dv.PHIEN_AN_TOAN))
e = loi_cua(gui, "cam-C-00", "C")
kiem("Tài khoản C mở phiên khi mọi phiên đang chạy → 429, không đẩy ai",
     isinstance(e, dv.QuaTai) and len(dv.PHIEN_AN_TOAN) == dv.GIOI_HAN_PHIEN and "cam-C-00" not in dv.PHIEN_AN_TOAN, e)
dv.PHIEN_AN_TOAN["cam-B-00"]["luc"] -= dv.PHIEN_DANG_CHAY + 1     # B tam dung 1 camera (co vung cam) > 10 s
kq = gui("cam-C-00", "C")
kiem("Có phiên đã im ≥ 10 s → C được cấp chỗ, chỉ phiên im đó bị đẩy, 16 phiên của A còn nguyên",
     "cam-C-00" in dv.PHIEN_AN_TOAN and "cam-B-00" not in dv.PHIEN_AN_TOAN
     and all("cam-A-%02d" % i in dv.PHIEN_AN_TOAN for i in range(16)))
dv.PHIEN_AN_TOAN["cam-A-03"]["luc"] -= dv.PHIEN_DANG_CHAY + 1
kq = gui("cam-B-00", "B")                                      # B phat tiep camera da bi day ra
kiem("Camera bị đẩy ra quay lại: tạo lại với vùng cấm cũ + canCauHinh",
     kq.get("canCauHinh") is True and len(dv.PHIEN_AN_TOAN["cam-B-00"]["phien"].vung) == 1,
     (kq.get("canCauHinh"), dv.PHIEN_AN_TOAN["cam-B-00"]["phien"].vung))
ds = [gui("cam-B-00", "B") for _ in range(20)]
kiem("... và vẫn cảnh báo vùng cấm sau khi quay lại", "vao-vung-cam" in rui_ro(ds[-1]), rui_ro(ds[-1]))

lam_moi()
for i in range(16):
    gui("cam-A-%02d" % i, "A")
gui("cam-B-00", "B")
dv.PHIEN_AN_TOAN["cam-B-00"]["luc"] -= 60                      # B im lau hon
dv.PHIEN_AN_TOAN["cam-A-07"]["luc"] -= dv.PHIEN_DANG_CHAY + 1
gui("cam-A-99", "A")
kiem("A đủ 16 phiên mở thêm: đẩy phiên IM của chính A, không đụng phiên của B (dù B im lâu hơn)",
     "cam-A-07" not in dv.PHIEN_AN_TOAN and "cam-B-00" in dv.PHIEN_AN_TOAN and "cam-A-99" in dv.PHIEN_AN_TOAN)
dv.PHIEN_AN_TOAN["cam-A-01"]["luc"] -= dv.PHIEN_HET_HAN + 1
dv.PHIEN_AN_TOAN["cam-A-01"]["dang"] = 1                       # dang co khung xu ly
gui("cam-A-02", "A")
kiem("Phiên đang có khung xử lý không bị dọn giữa chừng dù quá hạn", "cam-A-01" in dv.PHIEN_AN_TOAN)
dv.PHIEN_AN_TOAN["cam-A-01"]["dang"] = 0

# ---------------------------------------------------------------------------------------------- 3. nguong phat hien
print("== 3. Ngưỡng bộ phát hiện người / xe theo máy cảnh báo (NGUONG_PHAT_HIEN) ==")


class YoloGia:
    def __init__(self, duong, imgsz=640, nguong=0.3, lop=None):
        self.nguong, self.lop = nguong, lop

    def __call__(self, img):
        return np.zeros((0, 6))


cu = (dv.PhatHienYolo, dv.dgbs.PhatHien, dv.dmc.tai_mo_hinh, dict(dv.MO_HINH))
co_san = hasattr(dv.atc, "NGUONG_PHAT_HIEN")
gia_tri_san = getattr(dv.atc, "NGUONG_PHAT_HIEN", None)
try:
    dv.PhatHienYolo = YoloGia
    dv.dgbs.PhatHien = lambda *a, **k: (lambda img: np.zeros((0, 6)))
    dv.dmc.tai_mo_hinh = lambda: object()
    dv.atc.NGUONG_PHAT_HIEN = 0.123
    dv.nap_mo_hinh()
    kiem("Mô hình người / xe dùng ngưỡng an_toan_cang.NGUONG_PHAT_HIEN", dv.MO_HINH["vat_the"].nguong == 0.123,
         dv.MO_HINH["vat_the"].nguong)
    del dv.atc.NGUONG_PHAT_HIEN
    dv.nap_mo_hinh()
    kiem("Máy cảnh báo bản cũ (chưa có hằng số) → giữ 0,3 như trước", dv.MO_HINH["vat_the"].nguong == 0.3)
finally:
    if co_san:
        dv.atc.NGUONG_PHAT_HIEN = gia_tri_san
    dv.PhatHienYolo, dv.dgbs.PhatHien, dv.dmc.tai_mo_hinh = cu[:3]
    dv.MO_HINH.clear()
    dv.MO_HINH.update(cu[3])
if co_san:
    kiem("NGUONG_PHAT_HIEN (%.2f) < track_activation_threshold 0,3 → ByteTrack nhận được hộp điểm thấp" % gia_tri_san,
         gia_tri_san < 0.3)

# ---------------------------------------------------------------------------------------------- 4. anh qua lon
print("== 4. Ảnh khai kích thước khổng lồ / bom nén → 400 rõ ràng ==")
lam_moi()
BOM_12K = anh_b64(Image.new("1", (12000, 12000)), "PNG")
BOM_14K = anh_b64(Image.new("1", (14000, 14000)), "PNG")
a = time.perf_counter()
e = loi_cua(dv.doc_anh, BOM_12K)
kiem("PNG %d KB khai 12000x12000: từ chối từ đầu tệp (%.0f ms), ValueError có lý do"
     % (len(BOM_12K) * 3 // 4 // 1024, (time.perf_counter() - a) * 1000),
     isinstance(e, ValueError) and "quá lớn" in str(e) and time.perf_counter() - a < 1.0, repr(e))
e = loi_cua(dv.doc_anh, BOM_14K)
kiem("PNG khai 14000x14000 (bom nén): ValueError, không phải DecompressionBombError lọt ra ngoài",
     isinstance(e, ValueError) and "quá lớn" in str(e), repr(e))
e = loi_cua(dv.doc_anh, anh_b64(Image.new("1", (9000, 7000)), "PNG"))
kiem("PNG khai 9000x7000 = 63 MP (dưới ngưỡng bom của PIL, trên giới hạn 50 MP) → 400 nêu kích thước",
     isinstance(e, ValueError) and "9000 x 7000" in str(e), repr(e))
ANH_12MP = anh_b64(Image.new("RGB", (4000, 3000), "white"))
kiem("Ảnh chụp điện thoại 12 MP: vẫn đọc được cho biển số / container", dv.doc_anh(ANH_12MP).size == (4000, 3000))
e = loi_cua(gui, "camera-thuong-01", "A")
kiem("Khung camera 640x480 bình thường vẫn qua", e is None, e)
e = loi_cua(dv.khung_an_toan, dict(phien="camera-anh-lon-01", anh=ANH_12MP, t=0), "A")
kiem("Khung camera 12 MP (> 4K) → 400 rõ ràng", isinstance(e, ValueError) and "quá lớn" in str(e), repr(e))
for than in ([], "abc", {"a": 1}):
    e = loi_cua(dv.khung_an_toan, dict(phien="camera-t-sai-01", anh=ANH, t=than), "A")
    kiem("t = %r → ValueError (400), không TypeError làm rớt kết nối" % (than,), isinstance(e, ValueError), repr(e))
for than in ([], "abc", 5):
    e = loi_cua(dv.khung_an_toan, than, "A")
    kiem("Thân %r → ValueError (400), không AttributeError" % (than,), isinstance(e, ValueError), repr(e))

# qua HTTP that (cong ngau nhien): do_POST tra JSON 400 / 404 / 429 thay vi dong ket noi (cong API -> 502)
may = ThreadingHTTPServer(("127.0.0.1", 0), dv.XuLy)
dv.XuLy.log_message = lambda *a: None
threading.Thread(target=may.serve_forever, daemon=True).start()
GOC_HTTP = "http://127.0.0.1:%d" % may.server_address[1]
KHONG_PROXY = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def post(duong, than, token=None):
    nd = than if isinstance(than, bytes) else json.dumps(than).encode()
    h = {"Content-Type": "application/json"}
    if token:
        h["Authorization"] = "Bearer " + token
    try:
        with KHONG_PROXY.open(urllib.request.Request(GOC_HTTP + duong, data=nd, headers=h, method="POST"), timeout=30) as r:
            return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


m, j = post("/api/ai-thi-giac/bien-so", {"anh": BOM_14K})
kiem("HTTP: bom nén gửi /bien-so → 400 kèm thông báo (trước: mất phản hồi, gateway 502)", m == 400 and j.get("thongBao"), (m, j))
m, j = post("/api/ai-thi-giac/an-toan/khung", {"phien": "camera-http-0001", "anh": BOM_12K, "t": 0})
kiem("HTTP: ảnh 12000x12000 gửi /an-toan/khung → 400", m == 400, (m, j))
m, j = post("/api/ai-thi-giac/an-toan/khung", [])
kiem("HTTP: thân JSON không phải đối tượng → 400", m == 400, (m, j))
m, j = post("/api/ai-thi-giac/khong-co", {"anh": ANH})
kiem("HTTP: đường dẫn không có → 404", m == 404, (m, j))
m, j = post("/api/ai-thi-giac/bien-so", {"anh": base64.b64encode(b"khong phai anh").decode()})
kiem("HTTP: tệp không phải ảnh → 400, thông báo tiếng Việt, không lộ địa chỉ bộ nhớ / đường dẫn máy chủ",
     m == 400 and "Không đọc được" in j.get("thongBao", "") and "0x" not in j.get("thongBao", ""), (m, j))
gioi_han_cu = dv.GIOI_HAN_PHIEN_NGUOI
dv.GIOI_HAN_PHIEN_NGUOI = 0
m, j = post("/api/ai-thi-giac/an-toan/khung", {"phien": "camera-http-0002", "anh": ANH, "t": 0})
dv.GIOI_HAN_PHIEN_NGUOI = gioi_han_cu
kiem("HTTP: hết chỗ phiên → 429 kèm lý do", m == 429 and "tối đa" in j.get("thongBao", ""), (m, j))

# ---------------------------------------------------------------------------------------------- 5. gioi han cauHinh
print("== 5. Giới hạn cauHinh (một phiên không được làm chậm mọi camera) ==")


def vung(k, loai="cam-nguoi", so=1):
    return [{"ten": "v%d" % i, "loai": loai, "diem": [[0.5 + 0.1 * math.cos(2 * math.pi * j / k), 0.5 + 0.1 * math.sin(2 * math.pi * j / k)]
                                                       for j in range(k)]} for i in range(so)]


MAC_DINH_GD = {"vung": [], "hieuChinh": None, "gocNhin": 60, "caoCamera": 6, "nguong": {"toc_do_toi_da": 15, "can_bao_ho": True}}
CA = [
    ("cấu hình mặc định của giao diện", MAC_DINH_GD, True),
    ("%d vùng" % dv.GIOI_HAN_VUNG, {"vung": vung(4, so=dv.GIOI_HAN_VUNG)}, True),
    ("%d vùng" % (dv.GIOI_HAN_VUNG + 1), {"vung": vung(4, so=dv.GIOI_HAN_VUNG + 1)}, False),
    ("vùng %d điểm" % dv.GIOI_HAN_DINH_VUNG, {"vung": vung(dv.GIOI_HAN_DINH_VUNG)}, True),
    ("vùng %d điểm" % (dv.GIOI_HAN_DINH_VUNG + 1), {"vung": vung(dv.GIOI_HAN_DINH_VUNG + 1)}, False),
    ("vật cản 16 x 4 = 64 điểm", {"vung": vung(4, "vat-can", 16)}, True),
    ("vật cản 17 x 4 = 68 điểm", {"vung": vung(4, "vat-can", 17)}, False),
    ("tọa độ NaN (JSON cho phép)", json.loads('{"vung": [{"loai": "cam-nguoi", "diem": [[NaN, 0.1], [0.2, 0.2], [0.3, 0.1]]}]}'), False),
    ("tọa độ ngoài khung ảnh", {"vung": [{"loai": "cam-nguoi", "diem": [[5, 0.1], [0.2, 0.2], [0.3, 0.1]]}]}, False),
    ("ttc_canh_bao = 1e6 (1e7 bước mô phỏng / cặp)", {"nguong": {"ttc_canh_bao": 1e6}}, False),
    ("bao_ho_toi_da = 1000 (1000 ảnh cắt chạy GPU / khung)", {"nguong": {"bao_ho_toi_da": 1000}}, False),
    ("gioiHan vùng là chữ", {"vung": [dict(vung(4)[0], loai="gioi-han-toc-do", gioiHan="abc")]}, False),
    ("hiệu chỉnh 3 điểm", {"hieuChinh": {"anh": [[0, 0], [1, 0], [1, 1]], "met": [[0, 0], [1, 0], [1, 1]]}}, False),
    ("cauHinh là mảng", [1, 2], False),
]
for ten, ch, dung in CA:
    e = loi_cua(dv.kiem_cau_hinh, ch)
    kiem(("Nhận " if dung else "Từ chối (400) ") + ten, (e is None) if dung else isinstance(e, ValueError), repr(e))
s = dv.kiem_cau_hinh(dict({"k%d" % i: 0 for i in range(100000)}, gocNhin=70))
kiem("100 000 khóa lạ kiểu số KHÔNG được lưu (16 MB khóa lạ ~ 100 MB RAM / phiên) — chỉ giữ khóa máy cảnh báo đọc",
     s == {"gocNhin": 70}, len(s))
e = loi_cua(dv.kiem_cau_hinh, {"vung": [dict(VUNG[0], gioiHan=-5)]})
s = None if e else dv.kiem_cau_hinh({"vung": [dict(VUNG[0], gioiHan=-5)]})
kiem("Vùng cam-nguoi còn gioiHan rác (ô nhập bị ẩn) → nhận, bỏ gioiHan (không 400 dừng cả camera)",
     e is None and "gioiHan" not in s["vung"][0], repr(e) if e else s)

# ---------------------------------------------------------------------------------------------- 6. JWT + thu hoi
print("== 6. JWT: iss/aud + thu hồi token qua dịch vụ định danh ==")
KHOA = "khoa-kiem-thu-" + "k" * 40


def _b64u(b):
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode()


def token(vai_tro="NhanVien", iss="SmartPort.Api", aud="smartport-web", han=3600, ten="nv01", khoa=KHOA):
    dau = _b64u(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    than = _b64u(json.dumps({"sub": "5", "ten": ten, "vaiTro": vai_tro, "iss": iss, "aud": aud, "exp": int(time.time() + han),
                             "nbf": int(time.time() - 5), "jti": os.urandom(8).hex()}).encode())
    return dau + "." + than + "." + _b64u(hmac.new(khoa.encode(), (dau + "." + than).encode(), hashlib.sha256).digest())


TRANG_THAI, SO_LAN_HOI = {}, [0]                                # token -> (ma HTTP, vaiTro trong CSDL)


class DinhDanhGia(BaseHTTPRequestHandler):
    def do_GET(self):
        SO_LAN_HOI[0] += 1
        tk = (self.headers.get("Authorization") or "")[7:]
        ma, vt = TRANG_THAI.get(tk, (401, None))
        nd = json.dumps({"maTK": 5, "vaiTro": vt}).encode() if ma == 200 else b""
        self.send_response(ma)
        self.send_header("Content-Length", str(len(nd)))
        self.end_headers()
        self.wfile.write(nd)

    def log_message(self, *a):
        pass


dd = ThreadingHTTPServer(("127.0.0.1", 0), DinhDanhGia)
threading.Thread(target=dd.serve_forever, daemon=True).start()
dv.KHOA_JWT, dv.JWT_ISS, dv.JWT_AUD = KHOA, "SmartPort.Api", "smartport-web"
dv.DINH_DANH = "http://127.0.0.1:%d" % dd.server_address[1]
dv.THU_HOI.clear()
dv._LOI_DINH_DANH[:] = [0.0, 0.0]
T_TOT, T_KHOA, T_HA = token(), token(ten="nv-bi-khoa"), token(ten="nv-bi-ha-vai")
TRANG_THAI.update({T_TOT: (200, "NhanVien"), T_KHOA: (401, None), T_HA: (200, "KhachHang")})

kiem("Token hợp lệ, tài khoản còn hoạt động → cho qua", dv.kiem_jwt("Bearer " + T_TOT) == (True, "nv01"), dv.kiem_jwt("Bearer " + T_TOT))
n = SO_LAN_HOI[0]
for _ in range(5):
    dv.kiem_jwt("Bearer " + T_TOT)
kiem("Bộ đệm 60 s: 5 khung tiếp theo không hỏi lại định danh", SO_LAN_HOI[0] == n, SO_LAN_HOI[0] - n)
ok, ly_do = dv.kiem_jwt("Bearer " + T_KHOA)
kiem("Tài khoản bị khóa / đổi mật khẩu (định danh trả 401) → chặn ngay dù token còn hạn", not ok and "đăng nhập lại" in ly_do, ly_do)
ok, ly_do = dv.kiem_jwt("Bearer " + T_HA)
kiem("Vai trò trong CSDL đã hạ xuống KhachHang → chặn", not ok, ly_do)
n = SO_LAN_HOI[0]
for ten, tk in (("sai iss", token(iss="ke-gia-mao")), ("sai aud", token(aud="app-khac")), ("hết hạn", token(han=-10)),
                ("sai chữ ký", token(khoa="khoa-khac-" + "z" * 40)), ("vai trò KhachHang", token(vai_tro="KhachHang"))):
    ok, ly_do = dv.kiem_jwt("Bearer " + tk)
    kiem("Token %s → chặn tại chỗ" % ten, not ok, ly_do)
kiem("Token sai bị chặn tại chỗ, không tốn lượt hỏi định danh", SO_LAN_HOI[0] == n, SO_LAN_HOI[0] - n)

TRANG_THAI[T_TOT] = (401, None)                                # admin khoa tai khoan sau khi da dem
k_tot = hashlib.sha256(T_TOT.encode()).hexdigest()
dv.THU_HOI[k_tot] = dv.THU_HOI[k_tot][:2] + (time.time() - 61,) + dv.THU_HOI[k_tot][3:]
kiem("Bộ đệm quá 60 s → hỏi lại → tài khoản vừa bị khóa bị chặn (như dịch vụ .NET)", not dv.kiem_jwt("Bearer " + T_TOT)[0])

m, j = post("/api/ai-thi-giac/an-toan/khung", {"phien": "camera-http-0003", "anh": ANH, "t": 0}, token=T_KHOA)
kiem("HTTP: token của tài khoản bị khóa gửi khung camera → 401 kèm lý do", m == 401 and "đăng nhập lại" in j.get("thongBao", ""), (m, j))

T_LOI = token(ten="nv-dinh-danh-loi")
TRANG_THAI[T_LOI] = (500, None)
kiem("Định danh trả 500 → không chặn oan (kiểm cục bộ), bật ngắt mạch", dv.kiem_jwt("Bearer " + T_LOI)[0] and dv._LOI_DINH_DANH[0] > 0)
dv._LOI_DINH_DANH[:] = [0.0, 0.0]


class MoLoi:
    """thay _KHONG_PROXY: dinh-danh tat GIUA CHUNG phan hoi -> IncompleteRead (khong phai OSError / ValueError)"""
    def open(self, *a, **k):
        raise http.client.IncompleteRead(b"")


mo_cu, dia_chi_cu = dv._KHONG_PROXY, dv.DINH_DANH
dv._KHONG_PROXY = MoLoi()
e = loi_cua(dv.kiem_jwt, "Bearer " + token(ten="nv-dinh-danh-tat-giua-chung"))
kiem("Định danh tắt giữa chừng (IncompleteRead) → không ném lỗi ra do_POST (trước: mất phản hồi, cổng API 502), bật ngắt mạch",
     e is None and dv._LOI_DINH_DANH[0] > 0, repr(e))
dv._KHONG_PROXY = mo_cu
dv._LOI_DINH_DANH[:] = [0.0, 0.0]
dv.DINH_DANH = "http//127.0.0.1:%d" % dd.server_address[1]    # AI_DINH_DANH go sai -> Request() nem ValueError
e = loi_cua(dv.kiem_jwt, "Bearer " + token(ten="nv-sai-dia-chi"))
kiem("AI_DINH_DANH gõ sai ('http//...') → ValueError không lọt ra ngoài (trước: MỌI yêu cầu mất phản hồi), bật ngắt mạch",
     e is None and dv._LOI_DINH_DANH[0] > 0, repr(e))
dv.DINH_DANH = dia_chi_cu
dv._LOI_DINH_DANH[:] = [0.0, 0.0]
dd.shutdown()
dd.server_close()                                              # dinh-danh SAP
k_khoa = hashlib.sha256(T_KHOA.encode()).hexdigest()
dv.THU_HOI[k_khoa] = dv.THU_HOI[k_khoa][:2] + (time.time() - 3600,) + dv.THU_HOI[k_khoa][3:]
a = time.perf_counter()
ok_moi = dv.kiem_jwt("Bearer " + token(ten="nv-moi"))[0]
t_dau = time.perf_counter() - a
kiem("Định danh sập: token CHƯA từng kiểm → cho qua theo kiểm cục bộ (%.0f ms, chờ tối đa 2 s)" % (t_dau * 1000),
     ok_moi and t_dau < 3.0, t_dau)
a = time.perf_counter()
ok_khoa = dv.kiem_jwt("Bearer " + T_KHOA)[0]
ok_moi2 = dv.kiem_jwt("Bearer " + token(ten="nv-moi-2"))[0]
t_sau = time.perf_counter() - a
kiem("Định danh sập: token ĐÃ BIẾT bị thu hồi vẫn bị chặn (trạng thái biết gần nhất)", not ok_khoa)
kiem("Ngắt mạch: không chờ định danh ở mỗi khung (%.1f ms cho 2 lần kiểm)" % (t_sau * 1000), ok_moi2 and t_sau < 0.1, t_sau)
dv.DINH_DANH = ""
kiem("AI_DINH_DANH=tat → chỉ kiểm cục bộ", dv.kiem_jwt("Bearer " + T_KHOA)[0])

may.shutdown()
dv.LUONG_AN_TOAN.shutdown()
so_dat = sum(ket_qua)
print("\nKET QUA: %s %d/%d" % ("DAT" if so_dat == len(ket_qua) else "%d LOI /" % (len(ket_qua) - so_dat), so_dat, len(ket_qua)))
sys.exit(0 if so_dat == len(ket_qua) else 1)
