# -*- coding: utf-8 -*-
"""SINH ANH THE MO PHONG DE DANH GIA / HUAN LUYEN DOC QR CCCD.

Quy tac an toan: the mo phong LUON co chu "MAU THU - KHONG CO GIA TRI", khong co quoc huy, khong hoa van bao mat,
du lieu 100% gia (so CCCD gia, ten ghep ngau nhien). Khong the dung nhu giay to that.

Chay (python cua SmartPort.AI/.venv):
  sinh_the.py test  <thu_muc> [so_moi_o]      # bo danh gia giu rieng (seed co dinh, 9 kieu suy giam x 5 co module + am tinh)
  sinh_the.py train <thu_muc> [so_anh]        # du lieu huan luyen YOLO (seed khac, nhan YOLO cho vung QR)
"""
import json, math, random, sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

FONT = "C:/Windows/Fonts/arial.ttf"
FONT_B = "C:/Windows/Fonts/arialbd.ttf"
HO = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý"]
DEM = ["Văn", "Thị", "Hữu", "Đức", "Minh", "Ngọc", "Thanh", "Quốc", "Thu", "Gia", "Xuân", "Bảo", "Công", "Mỹ"]
TEN = ["An", "Bình", "Chi", "Dũng", "Em", "Giang", "Hà", "Hải", "Hùng", "Khánh", "Linh", "Long", "Mai", "Nam", "Oanh", "Phúc", "Quân", "Sơn", "Tâm", "Tú", "Uyên", "Việt", "Yến"]
DUONG = ["Trần Hưng Đạo", "Lê Lợi", "Nguyễn Huệ", "Hai Bà Trưng", "Phan Đình Phùng", "Võ Thị Sáu", "Điện Biên Phủ", "Cách Mạng Tháng Tám"]
XA = ["Phường Bến Nghé", "Xã Tân Thạnh Đông", "Phường Phú Hòa", "Thị trấn Củ Chi", "Xã Đông Hòa", "Phường Thủ Dầu Một", "Xã Vĩnh Hưng"]
HUYEN = ["Quận 1", "Huyện Củ Chi", "Thành phố Thuận An", "Thành phố Dĩ An", "Quận Bình Thạnh", "Huyện Nhà Bè"]
TINH = ["TP. Hồ Chí Minh", "Bình Dương", "Đồng Nai", "Long An", "Tây Ninh", "Bà Rịa - Vũng Tàu"]
TINH_MA = ["079", "074", "075", "080", "072", "077", "001", "046", "048", "092"]
SUY_GIAM = ["sach", "phoi_canh", "mo_chuyen_dong", "mo_net", "jpeg_thap", "thieu_sang", "choi", "nhieu", "tong_hop"]
MODULE = [1.5, 2.0, 3.0, 4.0, 6.0]


def chuoi_qr(r):
    """Dung dinh dang CCCD: So|CMND|Ho ten|ddMMyyyy|Gioi tinh|Dia chi|ddMMyyyy. Du lieu GIA."""
    gt = r.choice(["Nam", "Nữ"])
    nam = r.randint(1960, 2006)
    ten = f"{r.choice(HO)} {r.choice(DEM)} {r.choice(DEM)} {r.choice(TEN)}" if r.random() < .4 else f"{r.choice(HO)} {r.choice(DEM)} {r.choice(TEN)}"
    tk = (0 if nam < 2000 else 2) + (1 if gt == "Nữ" else 0)
    cccd = f"{r.choice(TINH_MA)}{tk}{nam % 100:02d}{r.randint(0, 999999):06d}"
    cmnd = "" if r.random() < .35 else f"{r.randint(100000000, 399999999)}"
    dc = f"{r.randint(1, 500)}/{r.randint(1, 40)} {r.choice(DUONG)}, {r.choice(XA)}, {r.choice(HUYEN)}, {r.choice(TINH)}" if r.random() < .5 else f"Tổ {r.randint(1, 30)}, Ấp {r.randint(1, 9)}, {r.choice(XA)}, {r.choice(HUYEN)}, {r.choice(TINH)}"
    ns = f"{r.randint(1, 28):02d}{r.randint(1, 12):02d}{nam}"
    nc = f"{r.randint(1, 28):02d}{r.randint(1, 12):02d}{r.randint(2021, 2025)}"
    return f"{cccd}|{cmnd}|{ten}|{ns}|{gt}|{dc}|{nc}"


def anh_qr(chuoi, muc="M"):
    """Ma tran QR (0/255, moi module 1 px, khong vien) bang bo ma hoa cua OpenCV (byte mode, UTF-8)."""
    p = cv2.QRCodeEncoder_Params()
    p.correction_level = {"L": cv2.QRCODE_ENCODER_CORRECT_LEVEL_L, "M": cv2.QRCODE_ENCODER_CORRECT_LEVEL_M, "Q": cv2.QRCODE_ENCODER_CORRECT_LEVEL_Q}[muc]
    p.mode = cv2.QRCODE_ENCODER_MODE_BYTE
    e = cv2.QRCodeEncoder_create(p)
    m = e.encode(chuoi)
    # OpenCV tra anh co quiet zone 4 module, moi module 1 px -> bo vien de kiem soat vi tri
    k = np.where(m == 0)
    y0, y1, x0, x1 = k[0].min(), k[0].max(), k[1].min(), k[1].max()
    return m[y0:y1 + 1, x0:x1 + 1]


def ve_the(r, chuoi, qr, co_qr=True):
    """The mo phong 1712x1080 (=20 px/mm). Tra ve anh BGR + 4 goc vung QR tren the."""
    W, H = 1712, 1080
    nen = np.ones((H, W, 3), np.float32)
    mau = np.array([r.uniform(.86, .97), r.uniform(.88, .98), r.uniform(.90, .99)])
    for y in range(H):
        nen[y, :] = mau * (0.97 + 0.05 * y / H)
    img = Image.fromarray((nen * 255).astype(np.uint8)[:, :, ::-1])
    d = ImageDraw.Draw(img)
    d.rectangle([60, 60, W - 60, 190], fill=(205, 220, 232))
    d.text((90, 90), "THẺ MÔ PHỎNG PHỤC VỤ KIỂM THỬ", fill=(40, 60, 90), font=ImageFont.truetype(FONT_B, 54))
    d.rectangle([90, 300, 450, 780], fill=(200, 200, 205), outline=(120, 120, 130), width=3)
    d.ellipse([190, 370, 350, 530], fill=(150, 150, 158))
    d.pieslice([140, 540, 400, 900], 180, 360, fill=(150, 150, 158))
    f = ImageFont.truetype(FONT, 42)
    p = chuoi.split("|")
    dong = [("Số:", p[0]), ("Họ tên:", p[2]), ("Ngày sinh:", f"{p[3][:2]}/{p[3][2:4]}/{p[3][4:]}"), ("Giới tính:", p[4]), ("Nơi thường trú:", p[5][:42])]
    for i, (a, b) in enumerate(dong):
        d.text((500, 310 + i * 92), f"{a} {b}", fill=(30, 30, 40), font=f)
    arr = np.array(img)[:, :, ::-1].copy()
    # QR goc tren-phai, canh ~16 mm (=320 px) nhu the that; quiet zone trang
    goc = None
    if co_qr:
        n = qr.shape[0]
        canh = 320
        x0, y0 = W - 60 - canh, 215
        o = cv2.resize(qr, (canh, canh), interpolation=cv2.INTER_NEAREST)
        cv2.rectangle(arr, (x0 - 16, y0 - 16), (x0 + canh + 16, y0 + canh + 16), (255, 255, 255), -1)
        arr[y0:y0 + canh, x0:x0 + canh] = cv2.cvtColor(o, cv2.COLOR_GRAY2BGR)
        goc = np.array([[x0, y0], [x0 + canh, y0], [x0 + canh, y0 + canh], [x0, y0 + canh]], np.float32)
    # Dau MAU THU cheo the (bat buoc), nam duoi lop QR chu khong de len QR
    lop = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dl = ImageDraw.Draw(lop)
    fm = ImageFont.truetype(FONT_B, 150)
    dl.text((120, 520), "MẪU THỬ", fill=(200, 30, 30, 120), font=fm)
    dl.text((120, 700), "KHÔNG CÓ GIÁ TRỊ", fill=(200, 30, 30, 120), font=ImageFont.truetype(FONT_B, 96))
    lop = np.array(lop)
    a = lop[:, :, 3:4] / 255.0
    bgr = lop[:, :, 2::-1].astype(np.float32)
    arr = (arr * (1 - a) + bgr * a).astype(np.uint8)
    if co_qr:
        n0 = arr[y0:y0 + canh, x0:x0 + canh]  # giu QR sach, khong bi dau de len
        arr[y0:y0 + canh, x0:x0 + canh] = cv2.cvtColor(o, cv2.COLOR_GRAY2BGR)
    return arr, goc


def nen_ngau_nhien(r, w, h):
    """Mat ban / quay gate: mau + van nhieu tan so thap."""
    mau = np.array([r.randint(30, 200) for _ in range(3)], np.float32)
    s = max(8, r.randint(8, 60))
    tho = np.random.RandomState(r.randint(0, 2**31 - 1)).rand(h // s + 2, w // s + 2, 3).astype(np.float32)
    v = cv2.resize(tho, (w, h), interpolation=cv2.INTER_CUBIC)
    return np.clip(mau * (0.6 + 0.8 * v), 0, 255).astype(np.float32)


def dat_vao_canh(r, the, goc_qr, n_module, px_module, cuong_do_phoi_canh, W, H):
    """Dat the vao khung W x H sao cho moi module ~ px_module, QR nam tron trong khung."""
    h, w = the.shape[:2]
    qr_px = n_module * px_module
    tl = qr_px / 320.0
    cw, ch = w * tl, h * tl
    pts = np.array([[0, 0], [w, 0], [w, h], [0, h]], np.float32)
    goc = r.uniform(-cuong_do_phoi_canh * 25, cuong_do_phoi_canh * 25) * math.pi / 180
    ca, sa = math.cos(goc), math.sin(goc)
    dx = cuong_do_phoi_canh * 0.20
    bien = [(r.uniform(-dx, dx) * cw, r.uniform(-dx, dx) * ch) for _ in range(4)]
    tr = np.array([[-cw / 2, -ch / 2], [cw / 2, -ch / 2], [cw / 2, ch / 2], [-cw / 2, ch / 2]], np.float32)
    dst = np.array([[x * ca - y * sa + bx, x * sa + y * ca + by] for (x, y), (bx, by) in zip(tr, bien)], np.float32)
    M0 = cv2.getPerspectiveTransform(pts, dst)
    q0 = cv2.perspectiveTransform(goc_qr[None], M0)[0] if goc_qr is not None else dst
    x0, y0 = q0.min(0)
    x1, y1 = q0.max(0)
    # Chon tam the sao cho vung QR nam tron trong khung (the co the tran khoi khung)
    lo = 10
    txmin, txmax = lo - x0, W - lo - x1
    tymin, tymax = lo - y0, H - lo - y1
    if txmin > txmax or tymin > tymax:
        return None, None
    # uu tien the nam giua khung (nguoi dung dua vao khung net dut)
    tx = np.clip(r.gauss(W / 2, W * .12), txmin, txmax)
    ty = np.clip(r.gauss(H / 2, H * .12), tymin, tymax)
    dst2 = dst + [tx, ty]
    M = cv2.getPerspectiveTransform(pts, np.array(dst2, np.float32))
    nen = nen_ngau_nhien(r, W, H)
    # INTER_AREA khong ho tro warp -> thu nho truoc de chong rang cua (giong quang hoc camera)
    trong = cv2.warpPerspective(np.ones((h, w), np.float32), M, (W, H), flags=cv2.INTER_LINEAR)
    if tl < 0.5:
        k = max(1, int(round(0.5 / tl)))
        nho = cv2.resize(the, (w // k, h // k), interpolation=cv2.INTER_AREA)
        S = np.array([[k, 0, 0], [0, k, 0], [0, 0, 1]], np.float64)
        anh = cv2.warpPerspective(nho, M @ S, (W, H), flags=cv2.INTER_LINEAR)
    else:
        anh = cv2.warpPerspective(the, M, (W, H), flags=cv2.INTER_LINEAR)
    # them vung mo nhe cua ong kinh sau khi lay mau
    a = np.clip(trong, 0, 1)[..., None]
    kq = nen * (1 - a) + anh.astype(np.float32) * a
    q = cv2.perspectiveTransform(goc_qr[None], M)[0] if goc_qr is not None else None
    return kq, q


def suy_giam(r, a, kieu):
    a = a.copy()
    H, W = a.shape[:2]
    a = cv2.GaussianBlur(a, (0, 0), 0.45)  # quang hoc camera
    if kieu == "mo_chuyen_dong":
        do = r.randint(3, 8)
        goc = r.uniform(0, 180)
        k = np.zeros((do * 2 + 1, do * 2 + 1), np.float32)
        c = do
        for t in np.linspace(-do, do, do * 8):
            k[int(round(c + t * math.sin(math.radians(goc)))), int(round(c + t * math.cos(math.radians(goc))))] = 1
        a = cv2.filter2D(a, -1, k / k.sum())
    elif kieu == "mo_net":
        a = cv2.GaussianBlur(a, (0, 0), r.uniform(1.0, 2.2))
    elif kieu == "thieu_sang":
        a = 255 * (a / 255.0) ** r.uniform(1.8, 2.6) * r.uniform(.28, .5)
        a = a + np.random.RandomState(r.randint(0, 2**31 - 1)).normal(0, r.uniform(4, 10), a.shape)
    elif kieu == "choi":
        rs = np.zeros((H, W), np.float32)
        cx, cy = r.randint(W // 3, 2 * W // 3), r.randint(H // 4, 3 * H // 4)
        cv2.ellipse(rs, (cx, cy), (r.randint(W // 10, W // 4), r.randint(H // 10, H // 4)), r.uniform(0, 180), 0, 360, 1.0, -1)
        rs = cv2.GaussianBlur(rs, (0, 0), r.uniform(25, 70))
        a = np.clip(a + rs[..., None] * r.uniform(90, 200), 0, 255)
    elif kieu == "nhieu":
        a = a + np.random.RandomState(r.randint(0, 2**31 - 1)).normal(0, r.uniform(12, 28), a.shape)
    elif kieu == "tong_hop":
        for k2 in r.sample(["mo_chuyen_dong", "mo_net", "thieu_sang", "choi", "nhieu", "jpeg"], 2):
            if k2 == "jpeg":
                continue
            a = suy_giam(r, np.clip(a, 0, 255).astype(np.float32), k2)
    return np.clip(a, 0, 255).astype(np.uint8)


def luu(a, f, q):
    cv2.imwrite(str(f), a, [cv2.IMWRITE_JPEG_QUALITY, q])


def sinh_test(ra, so):
    ra = Path(ra)
    (ra / "anh").mkdir(parents=True, exist_ok=True)
    r = random.Random(20261109)
    meta = []
    i = 0
    for kieu in SUY_GIAM:
        for px in MODULE:
            for j in range(so):
                ch = chuoi_qr(r)
                muc = "M"
                qr = anh_qr(ch, muc)
                n = qr.shape[0]
                W, H = r.choice([(1280, 720), (1920, 1080)])
                cd = {"sach": .12, "phoi_canh": 1.0}.get(kieu, r.uniform(.15, .6))
                the, g = ve_the(r, ch, qr)
                kq, q = dat_vao_canh(r, the, g, n, px, cd, W, H)
                if kq is None:
                    W, H = 1920, 1080
                    kq, q = dat_vao_canh(r, the, g, n, px, min(cd, .3), W, H)
                    if kq is None:
                        continue
                a = suy_giam(r, kq, kieu)
                qj = r.randint(22, 42) if kieu == "jpeg_thap" else (r.randint(35, 70) if kieu == "tong_hop" else r.randint(82, 92))
                ten = f"{i:05d}.jpg"
                luu(a, ra / "anh" / ten, qj)
                meta.append(dict(tep=ten, kieu=kieu, px=px, n=n, w=W, h=H, chuoi=ch, jpeg=qj, q=np.round(q, 1).tolist()))
                i += 1
    # Am tinh: the khong co QR -> moi ket qua doc duoc deu la DOC SAI
    for j in range(so * 5):
        ch = chuoi_qr(r)
        qr = anh_qr(ch)
        the, _ = ve_the(r, ch, qr, co_qr=False)
        g = np.array([[1332, 215], [1652, 215], [1652, 535], [1332, 535]], np.float32)
        W, H = r.choice([(1280, 720), (1920, 1080)])
        kq, q = dat_vao_canh(r, the, g, 49, r.choice(MODULE), r.uniform(.1, .7), W, H)
        if kq is None:
            continue
        a = suy_giam(r, kq, r.choice(SUY_GIAM))
        ten = f"{i:05d}.jpg"
        luu(a, ra / "anh" / ten, r.randint(60, 90))
        meta.append(dict(tep=ten, kieu="am_tinh", px=0, n=0, w=W, h=H, chuoi=None, jpeg=0, q=None))
        i += 1
    (ra / "meta.json").write_text(json.dumps(meta, ensure_ascii=False), encoding="utf-8")
    print("test:", len(meta), "anh ->", ra)


def sinh_train(ra, so):
    """Du lieu huan luyen YOLO (1 lop: qr). Seed + phan phoi KHAC bo test."""
    ra = Path(ra)
    for t in ("train", "val"):
        (ra / "images" / t).mkdir(parents=True, exist_ok=True)
        (ra / "labels" / t).mkdir(parents=True, exist_ok=True)
    r = random.Random(777001)
    for i in range(so):
        t = "val" if i % 10 == 0 else "train"
        ch = chuoi_qr(r)
        muc = r.choice(["L", "M", "Q"])
        qr = anh_qr(ch, muc)
        n = qr.shape[0]
        W, H = r.choice([(1280, 720), (1920, 1080), (1600, 900), (1024, 768)])
        px = r.uniform(1.2, 7.5)
        the, g = ve_the(r, ch, qr, co_qr=r.random() > .04)
        co = g is not None
        if g is None:
            g = np.array([[1332, 215], [1652, 215], [1652, 535], [1332, 535]], np.float32)
        kq, q = dat_vao_canh(r, the, g, n, px, r.uniform(.05, 1.0), W, H)
        if kq is None:
            continue
        a = suy_giam(r, kq, r.choice(SUY_GIAM))
        ten = f"m{i:05d}"
        luu(a, ra / "images" / t / (ten + ".jpg"), r.randint(30, 92))
        with open(ra / "labels" / t / (ten + ".txt"), "w", encoding="utf-8") as f:
            if co:
                x0, y0 = q.min(0)
                x1, y1 = q.max(0)
                f.write(f"0 {(x0 + x1) / 2 / W:.6f} {(y0 + y1) / 2 / H:.6f} {(x1 - x0) / W:.6f} {(y1 - y0) / H:.6f}\n")
        if i % 500 == 0:
            print(i, flush=True)
    print("train xong ->", ra)


if __name__ == "__main__":
    che_do, thu_muc = sys.argv[1], sys.argv[2]
    so = int(sys.argv[3]) if len(sys.argv) > 3 else (40 if che_do == "test" else 3000)
    (sinh_test if che_do == "test" else sinh_train)(thu_muc, so)
