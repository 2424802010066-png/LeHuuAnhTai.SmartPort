# -*- coding: utf-8 -*-
"""
AN TOAN CANG — CANH BAO NGUY HIEM THOI GIAN THUC tu camera / video (chay trong dich vu AI Python, GPU).

  1. PHAT HIEN (hoc sau): YOLO26s (COCO) -> nguoi, o to, xe tai, xe buyt, xe may, xe dap.
     YOLO26s tinh chinh tren bo Construction-PPE -> mu bao ho / dau tran / ao phan quang / than khong ao phan quang.
  2. THEO VET: ByteTrack (ECCV 2022 — ghep ca khung diem thap de khong mat vet khi bi che) -> ma doi tuong on dinh.
     Bo phat hien chay voi NGUONG_PHAT_HIEN (0,1) THAP hon nguong kich hoat vet (0,3): hop diem thap (bi che) chi de
     ByteTrack GHEP VAO VET CU (buoc ghep thu 2) — KHONG tao vet moi, KHONG lam mau tu uoc ti le / bo phieu lop / nga /
     bao ho, nhieu do x2. Giu vet mat 3 GIAY (supervision tinh bo dem = frame_rate/30 * lost_track_buffer khung ->
     lost_track_buffer = 90; moi khung dat lai theo fps THUC).
  3. DOI SANG MET TREN MAT DAT: diem cham dat (giua canh duoi khung) ->
       - DA HIEU CHINH: ma tran HOMOGRAPHY H tu 4 diem anh <-> 4 diem thuc te (met) — chinh xac nhat;
       - CHUA HIEU CHINH: TU UOC TI LE theo MO HINH CAMERA LO KIM: vat cao h dung cach Z cao f.h/Z pixel, nen moi doi
         tuong dung tren mat dat cho 1 mau s = (chieu cao khung) / (chieu cao thuc cua loai: nguoi 1,7 m, o to 1,5 m,
         xe tai 3,2 m...) = f / Z tai cao do chan y. Khop s(y) = a*y + b (camera nhin xien: s tuyen tinh theo y)
         -> Z = f / s(y), X = (x - W/2) / s(y); f = (W/2) / tan(goc nhin / 2), mac dinh goc nhin 60 do.
         Sai so f lam khoang cach VA van toc cung ti le -> THOI GIAN TOI VA CHAM theo chieu sau KHONG doi.
  4. BO LOC KALMAN (van toc khong doi, 4 trang thai x, z, vx, vz) moi doi tuong -> vi tri / van toc muot, it rung.
     NHIEU DO THEO KHOANG CACH: sai so 2 pixel (nguoi) / 3 pixel (xe) o chan doi tuong -> lech bao nhieu met tren mat dat
     (o xa / gan chan troi lech hang met) -> bo loc tu tin it hon vao do do o xa. CHE KHUAT: mep duoi nam trong khung cua
     vat GAN camera hon -> nhieu x3. Phep do lech > 4 sigma (Mahalanobis: hop gop 2 vat, xe khac chay ngang che) -> BO QUA,
     chi DU DOAN theo van toc (vet di xuyen qua doan bi che, khong mat van toc); lech keo dai >= 0,5 s moi khoi tao lai.
     Buoc thoi gian dt THAT (khong cat): mat phat hien 2 s / may chu 1 khung moi 2 s -> du doan dung quang duong 2 s,
     nhieu qua trinh tu lon theo dt; khoang trong > 3 s -> khoi tao lai van toc. Van toc > 72 km/h trong bai = khong
     tin cay, khong dung de bao. Tu uoc ti le: MOI lan khop lai mo hinh mat dat deu chuyen trang thai Kalman sang mo hinh
     moi (khong de buoc nhay toa do bi hieu nham la van toc).
  5. MAY RUI RO (moi khung):
     - SAP VA CHAM xe-nguoi, xe-xe: moi xe la DOAN THANG mep duoi khung tren mat dat (xe dai 16 m nhin ngang khong
       bi coi la hinh tron ban kinh 8 m -> het bao dong gia khi nguoi dung canh hong xe o xa), nua be day xe la ban kinh
       quanh doan; 2 dau doan THUT VAO nua be day (khong no ngang ra ngoai mep that), rieng dau PHIA TRUOC theo huong chay
       giu le nua be day. Mo phong chuyen dong TUONG DOI 3 s toi (buoc 0,1 s, van toc Kalman), khoang cach diem-doan /
       doan-doan; CHI KHI DANG TIEN LAI (khoang cach giam > 0,3 m/s — chay song song / cung toc do khong bao);
       THOI GIAN TOI VA CHAM (TTC) = lan dau khoang cach <= nguoi 0,35 m + nua be day xe. NGUY HIEM: TTC <= 1,5 s;
       CANH BAO: TTC <= 3 s (le them 1 m) (nguong theo he thong canh bao va cham FCW: 2-3 s canh bao, < 1,5 s khan cap).
       Ham cpa() la cong thuc giai tich cho 2 chat diem (t* = -(dp.dv)/|dv|^2) — dung de kiem chung bo mo phong.
     - KHOANG CACH AN TOAN: nguoi cach mep xe DANG CHAY < 1,5 m (nguy hiem) / < 3 m (canh bao); cung cap voi va cham
       CANH BAO thi lay muc CAO hon. Nguoi DANG LAI xe may / xe dap (hop nguoi nam phan lon trong hop xe + cung van toc)
       khong bi xet voi chinh xe do.
     - VUNG NGUY HIEM ve tren anh: nguoi vao vung cam (duoi can truc, bai xep cont); xe vuot toc do gioi han cua vung
       (nhieu vung chong nhau -> lay gioi han NHO nhat); nguoi dung tren lan xe khi co xe dang chay toi gan.
     - QUA TOC DO chung (mac dinh 15 km/h trong bai).
     - XE SAP DAM VAT CAN CO DINH: vung "vat-can" ve quanh CHAN vat (day cont xep, chan can cau, tuong, cot) -> doi sang
       met, mo phong doan mep duoi xe 3 s toi; TTC <= 1,5 s nguy hiem, <= 3 s (le 1 m) canh bao; chi khi xe dang TIEN LAI
       (> 0,3 m/s). Da hieu chinh: dinh vung loai theo DAU mau so cua H (dinh tren chan troi), khong theo Z (goc met cua
       giao dien o goc gan-trai -> Z am van hop le). Vung bi bo qua -> tra canhBaoCauHinh cho giao dien.
     - NGUOI NGA: khung nguoi chuyen sang NAM (rong/cao > 1,25) LIEN TUC >= 1,5 s THEO THOI GIAN (khung thua 1 khung /
       1,2 s hay video 60 khung/giay deu dung) va gan nhu bat dong.
     - THIEU BAO HO (2 TANG): CAT tung nguoi (+ le 20% ngang, 15% tren) PHONG TO 320 px roi moi chay mo hinh do bao ho
       (ca khung 1280 bi YOLO thu ve 640 -> nguoi 80 px chi con dau ~10 px). Vung DAU (40% tren khung nguoi) / THAN
       (15-85%): CO neu diem "helmet"/"vest" >= a; THIEU neu < a VA diem "no_helmet"/"none" >= b (a, b CHON TREN VAL, gop
       moi khoang cach — danh_gia_bao_ho_khoang_cach.py; dua vao lop MANH helmet AP50 0,92 thay vi lop yeu no_helmet
       0,23). Mo hinh train them anh SUY GIAM (xa + nen JPEG). TEST, nguoi 80 px khung nen JPEG 70: thieu mu F1 0,05
       (ca khung, mo hinh cu) -> 0,85; thieu ao 0,06 -> 0,88. Bo phieu 15 khung.
     CHONG BAO DONG GIA: rui ro phai co trong >= 75% khung cua cua so ~0,4 s gan nhat (10 khung/giay: 3 / 4 khung; it
     nhat 2 khung) moi bao; moi (loai, doi tuong) nghi 4 s roi moi bao lai (da bao NGUY HIEM thi trong luc nghi khong bao
     them CANH BAO cung cap); khoa da nghi xong thi xoa (phien camera chay 24/7 khong phinh bo nho).
     NGUONG THEO THOI GIAN: fps THUC uoc tu dau thoi gian khung -> tin van toc khi vet >= 0,5 s VA >= 3 lan cap nhat,
     cua so xac nhan ~0,4 s: may chu cham (2-3 khung/giay) khong tre them hang giay, video phat 0,25x / 60 khung/giay
     khong co lai con 0,1 s.
Module KHONG phu thuoc GPU / mo hinh: bo phat hien duoc truyen vao (de kiem thu bang du lieu gia lap, kiem_thu_an_toan.py).
"""
import copy
import math
from collections import Counter, defaultdict, deque

import numpy as np

LOP_COCO = {0: "nguoi", 1: "xe-dap", 2: "o-to", 3: "xe-may", 5: "xe-buyt", 7: "xe-tai"}
TEN_LOP = {"nguoi": "Người", "xe-dap": "Xe đạp", "o-to": "Ô tô", "xe-may": "Xe máy", "xe-buyt": "Xe buýt", "xe-tai": "Xe tải"}
CAO_THUC = {"nguoi": 1.70, "xe-dap": 1.10, "o-to": 1.50, "xe-may": 1.20, "xe-buyt": 3.00, "xe-tai": 3.20}   # m
NUA_DAY = {"nguoi": 0.35, "xe-dap": 0.3, "o-to": 0.9, "xe-may": 0.4, "xe-buyt": 1.25, "xe-tai": 1.25}  # m quanh doan mep duoi
XE = {"o-to", "xe-may", "xe-buyt", "xe-tai", "xe-dap"}
XE_HAI_BANH = {"xe-may", "xe-dap"}                     # co nguoi NGOI TREN xe (hop nguoi rieng) -> khong xet nguoi lai
# NGUONG TIN CAY: bo phat hien nguoi / xe chay voi NGUONG_PHAT_HIEN (thap) de ByteTrack ghep duoc hop diem thap khi bi
# che (buoc ghep thu 2 cua ByteTrack chi lay 0,1 < diem < nguong kich hoat). Hop < NGUONG_KICH_HOAT chi duoc GHEP VAO
# VET CU: khong tao vet moi, khong lam mau tu uoc ti le (dich_vu_ai.py doc NGUONG_PHAT_HIEN cho mo hinh vat_the).
NGUONG_PHAT_HIEN = 0.1
NGUONG_KICH_HOAT = 0.3                                # track_activation_threshold (vet moi can >= 0,4 = nguong + 0,1)
GIU_VET_GIAY = 3.0                                    # giu vet mat phat hien toi 3 s (ByteTrack + bo vet cua may)
DT_TOI_DA_KALMAN = 3.0                                # khoang trong giua 2 lan do > 3 s: van toc cu het tin -> khoi tao lai
# lop cua mo hinh do bao ho (Construction-PPE)
PPE_MU, PPE_AO, PPE_KHONG_AO, PPE_NGUOI, PPE_KHONG_MU = 0, 2, 5, 6, 7
# MO HINH + LUAT DO BAO HO — chon tren VAL bang danh_gia_bao_ho_khoang_cach.py (cat nguoi 320 px; 8 dieu kien: nguoi
# goc / 200 / 120 / 80 px x khung khong nen / JPEG 70). a = nguong diem "co" (helmet / vest), b = nguong diem "thieu"
# (no_helmet / none). "bao_ho_ben" = YOLO26s train them 1 ban SUY GIAM moi anh (thu nho + mo + JPEG; tao_du_lieu_suy_giam.py).
# Danh gia 09/10 (vung xet THEO LOP giong _phieu_bao_ho; VAL 235 / TEST 234 nguoi). TEST do 1 lan — thieu mu F1: 0,91 anh
# goc · 0,90 nguoi 120 px nen JPEG · 0,85 nguoi 80 px nen JPEG (he cu ca khung: 0,89 · 0,18 · 0,05); thieu ao F1 0,84-0,94
# (he cu 0,04-0,89), precision 0,90-0,98.
LUAT_BAO_HO = {"mu": (0.30, 0.02), "ao": (0.40, 0.05)}
MO_HINH_BAO_HO = "bao_ho_ben"                         # thu muc runs/<ten>/weights/best.pt — di cung bo luat tren
NGUONG_DIEM_BAO_HO = 0.02                             # nguong tin cay toi thieu cua bo phat hien, DUNG nhu luc danh gia
CO_CAT_BAO_HO = 320                                   # canh dai anh cat dua vao mo hinh do bao ho
LE_CAT = (0.20, 0.15, 0.20, 0.05)                     # le cat quanh nguoi: trai, tren, phai, duoi (ti le rong / cao)


def cat_nguoi(img, hop):
    """cat nguoi (pixel) + le -> (anh cat, goc x, goc y) — dung chung cho may canh bao va bo danh gia"""
    H, W = img.shape[:2]
    x1, y1, x2, y2 = hop
    w, h = x2 - x1, y2 - y1
    a1, b1 = int(max(0, x1 - LE_CAT[0] * w)), int(max(0, y1 - LE_CAT[1] * h))
    a2, b2 = int(min(W, x2 + LE_CAT[2] * w)), int(min(H, y2 + LE_CAT[3] * h))
    return img[b1:b2, a1:a2], a1, b1

NGUONG_MAC_DINH = dict(
    ttc_nguy_hiem=1.5, ttc_canh_bao=3.0, le_canh_bao=1.0,        # giay / met
    gan_nguy_hiem=1.5, gan_canh_bao=3.0,                          # met tu mep xe toi nguoi
    xe_dang_chay=1.0,                                             # m/s: duoi muc nay coi nhu dung yen
    tiep_can=0.3,                                                 # m/s: khoang cach giam cham hon = khong tien lai
    toc_do_hop_ly=20.0,                                           # m/s (72 km/h): lon hon = phep do sai, khong tin
    toc_do_toi_da=15.0,                                           # km/h toan bai
    nga_ti_le=1.25, nga_giay=1.5, nga_toc_do=0.6,                 # nguoi nga: rong/cao, giay, m/s
    cua_so_giay=0.4, ti_le_xac_nhan=0.75, nghi_giay=4.0,          # chong bao dong gia (theo thoi gian)
    can_bao_ho=True,                                              # bat buoc mu + ao phan quang trong toan khung
    bao_ho_cao_toi_thieu=80, bao_ho_toi_da=8,                     # px: nguoi nho hon chua du chi tiet (da do toi 80 px)
    tuoi_toi_thieu=3, tuoi_tin_giay=0.5,                          # tin van toc: >= 3 lan cap nhat VA vet >= 0,5 s
)
# kep cac nguong THOI GIAN (cau_hinh): dich vu chi kiem so huu han -> gia tri vo ly lam TAT HAN canh bao ma khong ai biet
# (tuoi_tin_giay 100: khong bao gio tin van toc; ti_le_xac_nhan 5: khong bao gio du khung xac nhan)
KEP_NGUONG = dict(cua_so_giay=(0.1, 3.0), ti_le_xac_nhan=(0.5, 1.0), tuoi_tin_giay=(0.0, 3.0), tiep_can=(0.0, 5.0))

MO_TA = {
    "va-cham-xe-nguoi": "Sắp va chạm xe – người", "va-cham-xe-xe": "Sắp va chạm xe – xe",
    "qua-gan-xe": "Người quá gần xe đang chạy", "vao-vung-cam": "Người vào vùng nguy hiểm",
    "lan-xe": "Người trên làn xe có xe tới gần", "qua-toc-do": "Xe vượt tốc độ cho phép",
    "nguoi-nga": "Người ngã / nằm bất động", "thieu-mu": "Không đội mũ bảo hộ", "thieu-ao": "Không mặc áo phản quang",
    "va-cham-vat-can": "Xe sắp va chạm vật cản",
}
MUC_NGUY_HIEM, MUC_CANH_BAO = "nguy-hiem", "canh-bao"


# ====================================================================== MAT DAT (anh -> met)
class MatDat:
    """Doi diem anh (pixel) -> toa do mat dat (met). hieu_chinh = {"anh": [[x, y] x4 (0..1)], "met": [[X, Z] x4]}"""

    def __init__(self, goc_nhin=60.0, cao_camera=6.0):
        self.goc_nhin = goc_nhin
        self.cao_camera = cao_camera          # m — chi dung khi moi mau cung 1 do sau (khong uoc duoc phoi canh)
        self.H = self.Hinv = None
        self.w_goc = 1.0                      # mau so H@[x,y,1] tai tam 4 diem hieu chinh (mat dat) -> dau cua mat dat
        self.loi = None                       # hieu chinh khong hop le -> ly do (hien cho nguoi van hanh)
        self.mau = deque(maxlen=300)          # (y_chan, so pixel / met)
        self.so_mau = 0                       # TONG so mau da nhan (len(mau) dung o 300 -> khong dung de dem nhip khop)
        self.a, self.b = 0.0, None
        self.phien_ban = 0                    # tang MOI lan khop lai -> chuyen trang thai Kalman sang mo hinh moi
        self.W = self.Hh = None

    def ban_sao(self):
        """ban sao gon mo hinh hien tai (khong mang theo mau) — de chuyen trang thai Kalman khi khop lai"""
        b = copy.copy(self)
        b.mau = None
        return b

    def dat_hieu_chinh(self, hc, W, Hh):
        """hieu chinh 4 diem: kiem tra KHONG suy bien (kich thuoc > 0, khong 3 diem thang hang, dung thu tu) roi moi gan
        H / Hinv. Khong hop le -> H = Hinv = None (quay ve tu uoc ti le) + self.loi (khong nem loi: phien van chay)"""
        import cv2
        self.W, self.Hh = W, Hh
        self.H = self.Hinv = None
        self.loi = None
        if not hc:
            return
        try:
            src = np.array([[float(p[0]) * W, float(p[1]) * Hh] for p in hc.get("anh") or []], float)
            dst = np.array([[float(p[0]), float(p[1])] for p in hc.get("met") or []], float)
        except (TypeError, ValueError, IndexError):
            src = dst = np.zeros((0, 2))
        if src.shape != (4, 2) or dst.shape != (4, 2) or not (np.all(np.isfinite(src)) and np.all(np.isfinite(dst))):
            self.loi = "Hiệu chỉnh 4 điểm không hợp lệ (cần đúng 4 điểm ảnh và 4 điểm mét) — đang tự ước lượng tỉ lệ."
            return

        def suy_bien(Q, nguong):
            # 4 diem: dien tich da giac ~0 hoac co 3 diem gan thang hang (tam giac qua nho so voi dien tich)
            dt = abs(sum(Q[i, 0] * Q[(i + 1) % 4, 1] - Q[(i + 1) % 4, 0] * Q[i, 1] for i in range(4))) / 2
            if dt < nguong:
                return True
            for i in range(4):
                a, b, c = Q[i], Q[(i + 1) % 4], Q[(i + 2) % 4]
                if abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2 < 0.01 * dt:
                    return True
            return False
        if suy_bien(dst, 0.25):
            self.loi = ("Hiệu chỉnh 4 điểm không hợp lệ: chiều rộng / chiều dài thật phải > 0 và 4 điểm mét không thẳng "
                        "hàng — đang tự ước lượng tỉ lệ.")
            return
        if suy_bien(src, 1e-4 * W * Hh):
            self.loi = ("Hiệu chỉnh 4 điểm không hợp lệ: 4 điểm bấm trên ảnh trùng nhau hoặc có 3 điểm thẳng hàng — "
                        "bấm lại 4 góc hình chữ nhật trên mặt đất.")
            return
        try:
            H = cv2.getPerspectiveTransform(np.float32(src), np.float32(dst)).astype(float)
            Hinv = np.linalg.inv(H)
        except (cv2.error, np.linalg.LinAlgError):
            H = Hinv = None
        ok = H is not None and np.all(np.isfinite(H)) and np.all(np.isfinite(Hinv)) and np.linalg.cond(H) < 1e12
        if ok:
            # 4 dinh + tam phai CUNG phia chan troi (mau so cung dau): bam sai thu tu (cheo) -> duong chan troi cat qua
            # hinh hieu chinh -> phep doi vo nghia
            w = np.array([H @ np.array([x, y, 1.0]) for x, y in np.vstack([src, src.mean(axis=0)])])[:, 2]
            ok = bool(np.all(w * w[-1] > 0) and np.all(np.abs(w) > 1e-9))
            if ok:
                p = np.array([H @ np.array([x, y, 1.0]) for x, y in src])
                ok = bool(np.max(np.linalg.norm(p[:, :2] / p[:, 2:] - dst, axis=1)) < 0.05)
        if not ok:
            self.loi = ("Hiệu chỉnh 4 điểm không hợp lệ: thứ tự bấm không khớp (gần-trái → gần-phải → xa-phải → xa-trái) "
                        "hoặc điểm suy biến — đang tự ước lượng tỉ lệ.")
            return
        self.H, self.Hinv, self.w_goc = H, Hinv, float(w[-1])

    def tren_mat_dat(self, x, y):
        """(da hieu chinh) diem anh co nam tren MAT DAT (duoi chan troi cua mat phang hieu chinh) khong: mau so H@[x,y,1]
        cung dau voi tam hinh hieu chinh va khong qua sat chan troi (xa gap > 50 lan tam hieu chinh)"""
        if self.H is None:
            return True
        w = float((self.H @ np.array([x, y, 1.0]))[2])
        return w / self.w_goc > 0.02

    @property
    def che_do(self):
        return "homography" if self.H is not None else ("uoc-luong" if self.b is not None else "chua-du-mau")

    def them_mau(self, y_chan, cao_px, lop, ti_le=0.4):
        """ti_le = rong/cao khung: nguoi dang nam / cui (rong > 0,75 cao) khong cho mau chieu cao dung"""
        if self.H is not None or cao_px < 12 or lop not in CAO_THUC or (lop == "nguoi" and ti_le > 0.75):
            return
        self.mau.append((y_chan, cao_px / CAO_THUC[lop]))
        self.so_mau += 1
        n = self.so_mau
        # khop day luc dau (moi 5 mau), on dinh roi thi thua (moi 30 mau). MOI lan khop lai (du doi it) toa do met cua moi
        # vet deu nhay -> tang phien_ban de PhienAnToan chuyen trang thai Kalman (khong thi buoc nhay thanh van toc gia)
        if (n >= 5 and n % 5 == 0 and n <= 60) or (n > 60 and n % 30 == 0):
            self._khop()
            self.phien_ban += 1

    def _khop(self):
        """khop s(y) = a*y + b bang binh phuong toi thieu co trong so, loai diem ngoai (2 vong)"""
        y = np.array([m[0] for m in self.mau], float)
        s = np.array([m[1] for m in self.mau], float)
        dung = np.ones(len(y), bool)
        for _ in range(2):
            if dung.sum() < 4 or np.ptp(y[dung]) < 0.05 * (self.Hh or 1):
                # moi mau cung 1 do sau: KHONG uoc duoc do doc s(y). Da co mo hinh phoi canh -> giu nguyen; chua co ->
                # phoi canh voi chieu cao camera gia dinh: s(y) = (y - y0) / h => a = 1/h, b = s_tv - a.y_tv
                if self.b is not None and self.a > 0:
                    return
                a = 1.0 / max(1.0, float(self.cao_camera))
                b = float(np.median(s[dung])) - a * float(np.median(y[dung]))
            else:
                a, b = np.polyfit(y[dung], s[dung], 1)
            du = np.abs(s - (a * y + b))
            dung = du <= max(2.5 * np.median(du), 1e-6)
        a = max(0.0, float(a))                                # vat o xa (y nho) phai nho hon vat o gan
        self.a, self.b = a, float(b)

    @property
    def f(self):
        """tieu cu (pixel) tu goc nhin ngang"""
        return (self.W or 1280) / 2 / math.tan(math.radians(self.goc_nhin) / 2)

    @property
    def phoi_canh(self):
        """co phoi canh (s tang ro theo y) -> dung Z = f/s; khong (camera nhin thang xuong) -> Z = (H - y)/s"""
        return self.b is not None and self.a > 0 and self.a * (self.Hh or 720) > 0.25 * abs(self.b)

    def s(self, y):
        """so pixel / met tai cao do chan y (chua hieu chinh)"""
        if self.b is None:
            return max(1.0, (self.Hh or 720) / 12.0)        # chua co mau: gia dinh khung nhin cao ~12 m
        return max(self.a * y + self.b, 0.05 * (self.a * (self.Hh or 0) + self.b), 1.0)

    def sang_met(self, x, y):
        if self.H is not None:
            p = self.H @ np.array([x, y, 1.0])
            return np.array([p[0] / p[2], p[1] / p[2]])
        sy = self.s(y)
        X = (x - (self.W or 0) / 2) / sy
        Z = self.f / sy if self.phoi_canh else ((self.Hh or 0) - y) / sy
        return np.array([X, Z])

    def sang_anh(self, P):
        """nguoc lai (ve duong du bao len anh)"""
        if self.H is not None:
            p = self.Hinv @ np.array([P[0], P[1], 1.0])
            return (float(p[0] / p[2]), float(p[1] / p[2]))
        if self.phoi_canh:
            sy = self.f / max(float(P[1]), 0.5)
            y = (sy - self.b) / self.a
        else:
            sy = self.s(0)
            y = (self.Hh or 0) - P[1] * sy
        return (float(P[0] * sy + (self.W or 0) / 2), float(y))


# ====================================================================== KALMAN
class Kalman:
    """van toc khong doi tren mat dat: trang thai [x, z, vx, vz]; nhieu qua trinh = gia toc ngau nhien q (m/s^2)"""

    def __init__(self, p, t, q=2.0, r=0.5):
        self.x = np.array([p[0], p[1], 0.0, 0.0])
        self.P = np.diag([r * r, r * r, 9.0, 9.0])
        self.t, self.q, self.r = t, q, r
        self.t0 = t                            # luc khoi tao -> tuoi vet theo THOI GIAN (tin van toc)

    # dt THAT giua 2 lan do (khong cat 1 s: mat phat hien 2 s / 1 khung moi 2 s ma cat thi phan quang duong vuot 1 s bi
    # don vao van toc -> bao qua toc do gia). Nhieu qua trinh g.g^T.q^2 tu lon theo dt; dt > DT_TOI_DA_KALMAN: PhienAnToan
    # khoi tao lai vet truoc khi goi.
    def lech(self, p, t, r):
        """khoang cach Mahalanobis^2 cua phep do p so voi du doan (de loai ngoai lai)"""
        dt = max(1e-3, t - self.t)
        x = self.x[:2] + self.x[2:] * dt
        P = self.P[:2, :2] + self.P[2:, 2:] * dt * dt + np.eye(2) * (self.q * dt * dt / 2) ** 2
        S = P + np.eye(2) * r * r
        d = np.asarray(p) - x
        return float(d @ np.linalg.solve(S, d))

    def du_doan(self, t):
        """chi du doan toi t (khong co phep do tin cay: bi che / ngoai lai) — hiep phuong sai tang -> cong loai rong dan"""
        dt = max(0.0, t - self.t)
        if dt <= 0:
            return
        self.t = t
        F = np.eye(4)
        F[0, 2] = F[1, 3] = dt
        g = np.array([[dt * dt / 2, 0], [0, dt * dt / 2], [dt, 0], [0, dt]])
        self.x = F @ self.x
        self.P = F @ self.P @ F.T + g @ g.T * self.q ** 2

    def buoc(self, p, t, r=None):
        dt = max(1e-3, t - self.t)
        self.t = t
        F = np.eye(4)
        F[0, 2] = F[1, 3] = dt
        g = np.array([[dt * dt / 2, 0], [0, dt * dt / 2], [dt, 0], [0, dt]])
        Q = g @ g.T * self.q ** 2
        self.x = F @ self.x
        self.P = F @ self.P @ F.T + Q
        r = self.r if r is None else r
        Hm = np.array([[1.0, 0, 0, 0], [0, 1.0, 0, 0]])
        S = Hm @ self.P @ Hm.T + np.eye(2) * r * r
        K = self.P @ Hm.T @ np.linalg.inv(S)
        self.x = self.x + K @ (np.asarray(p) - Hm @ self.x)
        self.P = (np.eye(4) - K @ Hm) @ self.P

    @property
    def vi_tri(self):
        return self.x[:2]

    @property
    def van_toc(self):
        return self.x[2:]


def cpa(pa, va, pb, vb):
    """(t*, d_min, d_hien_tai) — diem tiep can gan nhat cua 2 chat diem chuyen dong deu"""
    dp, dv = np.asarray(pb) - np.asarray(pa), np.asarray(vb) - np.asarray(va)
    d0 = float(np.linalg.norm(dp))
    vv = float(dv @ dv)
    if vv < 1e-6:
        return math.inf, d0, d0
    t = -float(dp @ dv) / vv
    return t, float(np.linalg.norm(dp + dv * max(t, 0.0))), d0


def kc_diem_doan(p, a, b):
    ab = b - a
    L = float(ab @ ab)
    if L < 1e-12:
        return float(np.linalg.norm(p - a))
    u = min(1.0, max(0.0, float((p - a) @ ab) / L))
    return float(np.linalg.norm(p - (a + ab * u)))


def _cat_nhau(a1, a2, b1, b2):
    def ccw(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    d1, d2, d3, d4 = ccw(b1, b2, a1), ccw(b1, b2, a2), ccw(a1, a2, b1), ccw(a1, a2, b2)
    return (d1 * d2 < 0) and (d3 * d4 < 0)


def kc_doan_doan(a1, a2, b1, b2):
    if _cat_nhau(a1, a2, b1, b2):
        return 0.0
    return min(kc_diem_doan(a1, b1, b2), kc_diem_doan(a2, b1, b2), kc_diem_doan(b1, a1, a2), kc_diem_doan(b2, a1, a2))


def doan_than_xe(trai, phai, v, nua_day):
    """doan mep duoi xe (tren mat dat) dung lam LOI cua vien bao ban kinh nua_day: 2 dau THUT VAO nua_day de vien bao
    khong no ngang ra ngoai mep that cua xe (nhin truc dien: xe tai rong 2,5 m khong thanh 5 m); rieng dau PHIA TRUOC theo
    huong chay (thanh phan van toc doc doan) duoc giu lai -> van con le nua_day phia truoc mui xe nhu truoc."""
    trai, phai = np.asarray(trai, float), np.asarray(phai, float)
    e = phai - trai
    L = float(np.linalg.norm(e))
    if L < 1e-6:
        return trai, phai
    e = e / L
    co = min(float(nua_day), L / 2)
    a1, a2 = trai + e * co, phai - e * co
    toc = float(np.linalg.norm(v))
    if toc > 0.5:
        c = float(np.dot(v, e)) / toc                 # cos goc giua huong chay va doan
        if c > 0:
            a2 = a2 + e * (co * c)
        else:
            a1 = a1 + e * (co * c)
    return a1, a2


def du_bao_va_cham(doan_a, va, doan_b, vb, R, le, T=3.0, buoc=0.1, tiep_can=0.3):
    """Mo phong chuyen dong tuong doi: B dich (vb - va) t so voi A. Tra (ttc_R, ttc_R_le, d_min, d_hien_tai):
    ttc_R = lan dau khoang cach <= R; ttc_R_le = lan dau <= R + le (inf neu khong co trong T giay).
    CHI tinh TTC khi 2 doi tuong DANG TIEN LAI: khoang cach giam nhanh hon tiep_can (m/s) o buoc dau (khoang cach doan -
    doan theo thoi gian la ham LOI -> co gan lai ve sau thi buoc dau da giam). Chay song song / cung van toc (dv ~ 0, nhieu
    Kalman vai cm/s) -> inf: khong bao TTC = 0 vo ly; dang sat (<= R) MA van tien lai -> TTC = 0."""
    a1, a2 = doan_a
    b1, b2 = doan_b
    dv = np.asarray(vb) - np.asarray(va)
    ttc_r = ttc_rl = math.inf
    n_buoc = int(round(T / buoc))
    d0 = kc_doan_doan(a1, a2, b1, b2)
    # khoang cach giam toi da |dv| m/s: con xa hon R + le + |dv|.T thi khong the toi trong T giay -> bo mo phong (ket qua
    # y het; canh dong 20 xe x 60 nguoi khong ton 31 buoc moi cap)
    if d0 - float(np.hypot(dv[0], dv[1])) * n_buoc * buoc > R + le + 1e-9:
        return ttc_r, ttc_rl, d0, d0
    d1 = kc_doan_doan(a1, a2, b1 + dv * buoc, b2 + dv * buoc)
    d_min = min(d0, d1)
    if d0 - d1 <= tiep_can * buoc:
        return ttc_r, ttc_rl, d_min, d0
    d_truoc = math.inf
    for k in range(n_buoc + 1):
        tt = k * buoc
        o = dv * tt
        d = d0 if k == 0 else (d1 if k == 1 else kc_doan_doan(a1, a2, b1 + o, b2 + o))
        d_min = min(d_min, d)
        if d <= R + le and ttc_rl == math.inf:
            ttc_rl = tt
        if d <= R:
            ttc_r = tt
            break
        if d > d_truoc + 1e-9:          # ham LOI theo t: da qua diem gan nhat thi chi xa dan -> khong con lan cham nao
            break
        d_truoc = d
    return ttc_r, ttc_rl, d_min, d0


def kc_doan_da_giac(a1, a2, P):
    """khoang cach doan (a1, a2) toi da giac P (N x 2, cung he toa do); 0 neu cham / nam trong"""
    if trong_da_giac(a1[0], a1[1], P) or trong_da_giac(a2[0], a2[1], P):
        return 0.0
    n = len(P)
    return min(kc_doan_doan(a1, a2, P[i], P[(i + 1) % n]) for i in range(n))


def du_bao_dam_vat_can(doan, v, P, R, le, T=3.0, buoc=0.1, tiep_can=0.3):
    """doan mep duoi xe tinh tien theo v (m/s): (ttc_R, ttc_R_le, d_hien_tai). Khong TIEN LAI gan vat (khoang cach giam
    cham hon tiep_can m/s — chay song song, nhieu van toc ngang Kalman) -> (inf, inf, d0); dang sat ma van tien vao -> 0"""
    a1, a2 = np.asarray(doan[0], float), np.asarray(doan[1], float)
    v = np.asarray(v, float)
    n_buoc = int(round(T / buoc))
    d0 = kc_doan_da_giac(a1, a2, P)
    # doan xe dich toi da |v| m/s: con xa hon R + le + |v|.T thi khong the toi trong T giay -> bo mo phong (ket qua y het)
    if d0 - float(np.hypot(v[0], v[1])) * n_buoc * buoc > R + le + 1e-9:
        return math.inf, math.inf, d0
    if d0 - kc_doan_da_giac(a1 + v * buoc, a2 + v * buoc, P) <= tiep_can * buoc:   # di xa / song song: khong bao
        return math.inf, math.inf, d0
    ttc_r = ttc_l = math.inf
    for k in range(n_buoc + 1):
        tk = k * buoc
        d = d0 if k == 0 else kc_doan_da_giac(a1 + v * tk, a2 + v * tk, P)
        if ttc_l == math.inf and d <= R + le:
            ttc_l = tk
        if d <= R:
            ttc_r = tk
            break
    return ttc_r, ttc_l, d0


def trong_da_giac(px, py, poly):
    """ray casting (diem trong da giac, toa do cung he)"""
    o = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > py) != (y2 > py) and px < (x2 - x1) * (py - y1) / (y2 - y1 + 1e-12) + x1:
            o = not o
    return o


def la_nguoi_lai(nguoi, xe):
    """nguoi dang NGOI TREN xe may / xe dap (YOLO tra 2 hop rieng): >= 50% hop nguoi nam trong hop xe VA van toc gan
    bang van toc xe. Nguoi dung yen sau / truoc xe dang chay (hop chong nhau tren anh) bi loai nho dieu kien van toc."""
    if xe.lop not in XE_HAI_BANH:
        return False
    a, b = nguoi.hop, xe.hop
    iw = min(a[2], b[2]) - max(a[0], b[0])
    ih = min(a[3], b[3]) - max(a[1], b[1])
    if iw <= 0 or ih <= 0:
        return False
    if iw * ih / max(1.0, (a[2] - a[0]) * (a[3] - a[1])) < 0.5:
        return False
    return float(np.linalg.norm(nguoi.kf.van_toc - xe.kf.van_toc)) <= max(1.0, 0.35 * xe.toc_do())


# ====================================================================== VET DOI TUONG
class Vet:
    def __init__(self, ma, lop, hop, p, t, r):
        self.ma = ma
        self.lop_phieu = Counter({lop: 1})
        self.hop = hop
        self.kf = Kalman(p, t, q=1.5 if lop == "nguoi" else 4.0, r=r)
        self.tuoi = 1
        self.so_lech = 0                       # so khung lien tiep phep do bi loai (ngoai lai)
        self.t_lech_dau = None                 # thoi diem bat dau chuoi ngoai lai
        self.t_cuoi = t
        self.diem = 1.0                        # do tin cay cua hop o lan thay gan nhat
        self.ti_le = deque()                   # (t, rong/cao) — cat theo THOI GIAN (them_ti_le), khong theo so khung
        self.mu = deque(maxlen=15)
        self.ao = deque(maxlen=15)
        self.nua_day = NUA_DAY.get(lop, 0.9)
        self.doan = (np.asarray(p, float), np.asarray(p, float))     # mep duoi tren mat dat (nguoi: 1 diem)

    def them_ti_le(self, t, ti, giu_giay):
        """luu rong/cao, bo mau cu hon giu_giay (60 khung/giay van phu du cua so nga); thoi gian lui (tua video) -> xoa"""
        if self.ti_le and t < self.ti_le[-1][0]:
            self.ti_le.clear()
        self.ti_le.append((t, ti))
        while self.ti_le and self.ti_le[0][0] < t - giu_giay:
            self.ti_le.popleft()

    def giay_nam(self, nguong_ti_le):
        """so giay NAM LIEN TUC (rong/cao > nguong) tinh toi mau moi nhat; 0 neu mau moi nhat khong nam"""
        if not self.ti_le or self.ti_le[-1][1] <= nguong_ti_le:
            return 0.0
        t_dau = self.ti_le[-1][0]
        for tt, ti in reversed(self.ti_le):
            if ti <= nguong_ti_le:
                break
            t_dau = tt
        return self.ti_le[-1][0] - t_dau

    @property
    def lop(self):
        return self.lop_phieu.most_common(1)[0][0]

    @property
    def la_xe(self):
        return self.lop in XE

    def toc_do(self):
        return float(np.linalg.norm(self.kf.van_toc))


# ====================================================================== PHIEN (1 camera / 1 video)
class PhienAnToan:
    def __init__(self, phat_hien, phat_hien_bao_ho=None, fps=10.0):
        """phat_hien(img) -> mang N x 6 (x1, y1, x2, y2, conf, lop_coco); phat_hien_bao_ho(img) -> N x 6 (lop PPE)"""
        import supervision as sv
        self._sv = sv
        self.phat_hien, self.phat_hien_bao_ho = phat_hien, phat_hien_bao_ho
        # supervision: so khung giu vet mat = int(frame_rate / 30 * lost_track_buffer) -> 90 = GIU_VET_GIAY x 30 (KHONG phai
        # 3 * fps: fps 8 chi con 6 khung ~0,75 s). Moi khung con dat lai max_time_lost theo fps THUC (may chu cham).
        self.tracker = sv.ByteTrack(track_activation_threshold=NGUONG_KICH_HOAT, lost_track_buffer=int(GIU_VET_GIAY * 30),
                                    minimum_matching_threshold=0.8, frame_rate=max(0.5, float(fps)))
        self.mat_dat = MatDat()
        self.vet = {}
        self.nguong = dict(NGUONG_MAC_DINH)
        self.vung = []          # [{ten, loai: cam-nguoi|gioi-han-toc-do|lan-xe|vat-can, diem: [[x,y] 0..1], gioiHan}]
        self.hieu_chinh = None
        self.cua_so = defaultdict(lambda: deque(maxlen=48))   # khoa rui ro -> deque(co) moi khung (toi da 48 khung)
        self.lan_bao = {}                            # khoa -> t lan bao cuoi (xoa khi da nghi xong)
        self._t_don = None                           # lan don lan_bao gan nhat
        self.so_khung = 0
        self.t_khung = deque(maxlen=12)              # dau thoi gian 12 khung gan nhat -> fps THUC
        self.fps_khai, self.fps_thuc = float(fps), float(fps)
        self.dem = Counter()                         # so su kien theo loai
        self.kich_thuoc = None
        self._canh_bao = set()                       # canh bao cau hinh cua khung hien tai (vung bo qua, hieu chinh loi)

    def _da_giac_met(self, z, W, Hh):
        """vung ve tren anh (0..1) -> (da giac tren mat dat (met), None) hoac (None, ly do) neu khong doi duoc.
        DA HIEU CHINH: Z tinh tu CANH GAN cua hinh hieu chinh (giao dien dat goc gan-trai = [0, 0]) nen Z am / nho van hop
        le -> chi loai dinh tren chan troi (mau so H@[x,y,1] doi dau). TU UOC: Z tinh tu camera -> loai Z <= 0,5 / > 300."""
        md = self.mat_dat
        ten = z.get("ten") or "không tên"
        ly_do = ("Vùng vật cản '%s' có đỉnh nằm trên đường chân trời / ngoài mặt đất — không đổi được sang mét, đang bỏ "
                 "qua. Vẽ lại vùng quanh chân vật trên mặt đất." % ten)
        if md.H is not None and not all(md.tren_mat_dat(x * W, y * Hh) for x, y in z["diem"]):
            return None, ly_do
        P = np.array([md.sang_met(x * W, y * Hh) for x, y in z["diem"]], float)
        if not np.all(np.isfinite(P)):
            return None, ly_do
        if md.H is None and (np.any(P[:, 1] <= 0.5) or np.any(P[:, 1] > 300)):
            return None, (ly_do if md.b is not None else None)      # chua du mau: tam thoi, khong canh bao
        return P, None

    def _tin(self, v, t):
        """tin van toc cua vet: >= tuoi_toi_thieu lan cap nhat Kalman VA vet da song >= tuoi_tin_giay (THOI GIAN) — 10 khung/
        giay: 6 khung (0,5 s) nhu truoc; 2,5 khung/giay: 3 khung; 30-60 khung/giay khong co lai con 0,1-0,2 s"""
        n = self.nguong
        return v.tuoi >= n["tuoi_toi_thieu"] and t - v.kf.t0 >= n["tuoi_tin_giay"] - 1e-6

    def _cua_so(self):
        """(so khung cua so, so khung can co rui ro) theo THOI GIAN: cua so ~cua_so_giay (0,4 s) x fps THUC, can >= 75%;
        10 khung/giay -> (4, 3); 2,6 khung/giay -> (2, 2); 30 khung/giay -> (12, 9) — khong co lai con 0,1 s"""
        n = self.nguong
        N = int(max(2, min(48, round(n["cua_so_giay"] * self.fps_thuc))))
        return N, int(min(N, max(2, math.ceil(n["ti_le_xac_nhan"] * N - 1e-9))))     # K <= N: ti le > 1 khong tat het bao

    # ------------------------------------------------------------------ cau hinh
    def cau_hinh(self, ch):
        if not ch:
            return
        md = self.mat_dat
        he_cu = (md.goc_nhin, md.cao_camera, repr(self.hieu_chinh))
        if "nguong" in ch and isinstance(ch["nguong"], dict):
            for k, v in ch["nguong"].items():
                if k in NGUONG_MAC_DINH and isinstance(v, (int, float, bool)):
                    if k in KEP_NGUONG:
                        if not math.isfinite(v):
                            continue
                        v = min(max(float(v), KEP_NGUONG[k][0]), KEP_NGUONG[k][1])
                    self.nguong[k] = type(NGUONG_MAC_DINH[k])(v)
        if "vung" in ch:
            self.vung = [v for v in (ch["vung"] or []) if isinstance(v, dict) and len(v.get("diem") or []) >= 3]
        if isinstance(ch.get("gocNhin"), (int, float)) and 20 <= ch["gocNhin"] <= 150:
            md.goc_nhin = float(ch["gocNhin"])
        if isinstance(ch.get("caoCamera"), (int, float)) and 2 <= ch["caoCamera"] <= 60:
            md.cao_camera = float(ch["caoCamera"])
        if "hieuChinh" in ch:
            self.hieu_chinh = ch["hieuChinh"] or None
            if self.kich_thuoc:
                md.dat_hieu_chinh(self.hieu_chinh, *self.kich_thuoc)
        if (md.goc_nhin, md.cao_camera, repr(self.hieu_chinh)) != he_cu:
            # doi he toa do mat dat giua phien: toa do met cua moi vet nhay -> Kalman se hieu la van toc. Bo vet (ma
            # ByteTrack giu nguyen), van toc hoc lai ~0,5 s
            self.vet.clear()

    # ------------------------------------------------------------------ 1 khung
    def xu_ly(self, img, t):
        """img: mang HxWx3 (RGB) ; t: giay (thoi diem khung). Tra dict ket qua."""
        Hh, W = img.shape[:2]
        if self.kich_thuoc != (W, Hh):
            self.kich_thuoc = (W, Hh)
            self.mat_dat.W, self.mat_dat.Hh = W, Hh
            self.mat_dat.dat_hieu_chinh(self.hieu_chinh, W, Hh)
        self.so_khung += 1
        self._canh_bao = set()
        if self.t_khung and t < self.t_khung[-1] - 1.0:
            # thoi gian LUI ro (tua video / client phat lai tu dau): trang thai theo thoi gian cua dong thoi gian cu vo nghia
            # (Kalman, t0 tin van toc -> vet cu "chua du tuoi" toi khi t vuot lai t0, cua so xac nhan, nghi bao lai) -> bo,
            # hoc lai nhu phien moi; ma ByteTrack giu tang dan (khong dung lai ma cu -> khoa chong trung khong lan)
            self.vet.clear()
            self.cua_so.clear()
            self.lan_bao.clear()
            self.t_khung.clear()
            self._t_don = None
        self.t_khung.append(float(t))
        if len(self.t_khung) >= 3 and self.t_khung[-1] > self.t_khung[0]:
            self.fps_thuc = (len(self.t_khung) - 1) / (self.t_khung[-1] - self.t_khung[0])
        else:
            self.fps_thuc = self.fps_khai
        # giu vet mat GIU_VET_GIAY theo fps THUC (fps khai 8 ma may chu chi xu ly 2,5 khung/giay -> van ~3 s); kep fps 120
        # (nhu cua so xac nhan <= 48 khung): dau thoi gian sai (buoc 1e-6 s) khong lam ByteTrack giu vet mat vo han
        self.tracker.max_time_lost = max(1, int(round(GIU_VET_GIAY * min(self.fps_thuc, 120.0))))
        det = np.asarray(self.phat_hien(img), dtype=float).reshape(-1, 6)
        det = det[[int(c) in LOP_COCO for c in det[:, 5]]] if len(det) else det
        sv = self._sv
        d = sv.Detections(xyxy=det[:, :4].copy(), confidence=det[:, 4].copy(), class_id=det[:, 5].astype(int)) if len(det) \
            else sv.Detections.empty()
        d = self.tracker.update_with_detections(d)
        diem_hop = d.confidence if len(d) and d.confidence is not None else np.ones(len(d))
        r_min = 0.25 if self.mat_dat.H is not None else 0.35
        dang_thay = set()
        hop_tat_ca = d.xyxy.copy() if len(d) else np.zeros((0, 4))
        md = self.mat_dat
        truoc = md.ban_sao() if md.H is None else None          # mo hinh mat dat ma trang thai Kalman dang dung
        pb_truoc = md.phien_ban
        for (x1, y1, x2, y2), lop_id, cf in zip(d.xyxy, d.class_id, diem_hop):
            lop = LOP_COCO.get(int(lop_id))
            if lop is None or cf < NGUONG_KICH_HOAT:            # hop diem thap (thuong bi che, cao sai): khong lam mau
                continue
            md.them_mau(y2, y2 - y1, lop, (x2 - x1) / max(1.0, y2 - y1))
        # mo hinh mat dat vua khop lai (tu uoc ti le, ke ca nhieu lan khop ngay trong khung nay): toa do met cua MOI vet nhay
        # -> Kalman se hieu nham la van toc. CHUYEN trang thai tu mo hinh DAU khung sang mo hinh moi: vi tri + vi tri 0,5 s
        # toi -> anh (mo hinh cu) -> met (mo hinh moi); van toc giu lien tuc. Lam o MOI lan khop (du doi < 5%: buoc nhay
        # nho van du thanh van toc gia luc gain Kalman con lon); chi noi hiep phuong sai khi mo hinh doi dang ke (> 5%).
        if truoc is not None and md.phien_ban != pb_truoc and self.vet:
            y_tham_chieu = Hh * 0.6
            doi = abs(md.s(y_tham_chieu) - truoc.s(y_tham_chieu)) / max(truoc.s(y_tham_chieu), 1e-6)
            for v in self.vet.values():
                p_anh = truoc.sang_anh(v.kf.vi_tri)
                f_anh = truoc.sang_anh(v.kf.vi_tri + v.kf.van_toc * 0.5)
                p_moi = md.sang_met(*p_anh)
                f_moi = md.sang_met(*f_anh)
                v.kf.x = np.r_[p_moi, (f_moi - p_moi) / 0.5]
                if doi > 0.05:
                    v.kf.P = v.kf.P * 1.5
        for (x1, y1, x2, y2), lop_id, ma, cf in zip(d.xyxy, d.class_id, d.tracker_id, diem_hop):
            lop = LOP_COCO.get(int(lop_id))
            if lop is None:
                continue
            ma = int(ma)
            thap = float(cf) < NGUONG_KICH_HOAT
            v = self.vet.get(ma)
            if v is None and thap:
                continue                                # hop diem thap chi duoc ghep vao vet DA CO, khong tao vet moi
            dang_thay.add(ma)
            p = md.sang_met((x1 + x2) / 2, y2)
            # nhieu do = do lech mat dat khi chan lech k pixel (xa chan troi: lon; gan camera: nho). k = 2 (nguoi), 3 (xe:
            # mep duoi = banh / bong / ro-mooc, kem net). MEP DUOI BI CHE (nam trong khung cua vat GAN camera hon) -> x3;
            # hop diem thap (bi che mot phan, mep duoi kem tin) -> x2; lop cua hop diem thap kem tin -> theo lop cua vet.
            k_px = 2.0 if (v.lop if thap else lop) == "nguoi" else 3.0
            for ox1, oy1, ox2, oy2 in hop_tat_ca:
                if oy2 > y2 + 2 and ox1 < x2 and ox2 > x1 and oy1 < y2 and min(x2, ox2) - max(x1, ox1) > 0.25 * (x2 - x1):
                    k_px *= 3.0
                    break
            else:
                if thap:
                    k_px *= 2.0
            r_do = max(r_min, float(np.linalg.norm(md.sang_met((x1 + x2) / 2 + k_px / 2, y2 + k_px) - p)))
            hop = [float(x1), float(y1), float(x2), float(y2)]
            bo_qua = False
            if v is not None and t - v.kf.t > DT_TOI_DA_KALMAN:
                # khoang trong qua dai (bi che lau / may chu qua cham): van toc cu het tin -> khoi tao lai, hoc lai
                v.kf = Kalman(p, t, q=v.kf.q, r=r_do)
                v.tuoi, v.so_lech, v.t_lech_dau = 0, 0, None
            elif v is not None and v.tuoi >= 2 and v.kf.lech(p, t, r_do) > 16.0:
                # phep do NGOAI LAI 4 sigma (vat khac di ngang che, hop gop 2 vat, hop nhay luc om ca ro-mooc luc chi cabin):
                # BO QUA, chi du doan theo van toc. Lech KEO DAI >= 0,5 s (>= 3 khung) moi coi la that -> khoi tao lai.
                v.so_lech += 1
                v.t_lech_dau = t if v.t_lech_dau is None else v.t_lech_dau
                if v.so_lech >= 3 and t - v.t_lech_dau >= 0.5:
                    v.kf = Kalman(p, t, q=v.kf.q, r=r_do)
                    v.tuoi, v.so_lech, v.t_lech_dau = 0, 0, None
                else:
                    bo_qua = True
            elif v is not None:
                v.so_lech, v.t_lech_dau = 0, None
            if v is None:
                v = self.vet[ma] = Vet(ma, lop, hop, p, t, r_do)
            elif v.tuoi == 0:
                v.tuoi = 1
            elif bo_qua:
                if not thap:
                    v.lop_phieu[lop] += 1
                v.kf.du_doan(t)
            else:
                if not thap:
                    v.lop_phieu[lop] += 1
                v.kf.buoc(p, t, r_do)
                v.tuoi += 1
                if v.toc_do() > self.nguong["toc_do_hop_ly"]:     # vuot gioi han vat ly: do sai -> lam lai, chua tin
                    v.kf = Kalman(p, t, q=v.kf.q, r=r_do)
                    v.tuoi = 1
            v.hop, v.t_cuoi, v.diem = hop, t, float(cf)
            if not thap:                                # hop bi che mot phan: rong/cao sai -> khong dung cho luat nga
                v.them_ti_le(t, (x2 - x1) / max(1.0, y2 - y1), self.nguong["nga_giay"] + 1.0)
            # xe: DOAN mep duoi khung tren mat dat, tinh tien theo vi tri Kalman (muot), 2 dau thut vao nua be day (tru dau
            # phia truoc theo huong chay); nguoi: 1 diem
            v.nua_day = NUA_DAY.get(v.lop, 0.9)
            if v.la_xe:
                trai = md.sang_met(x1, y2) - p
                phai = md.sang_met(x2, y2) - p
                v.doan = doan_than_xe(v.kf.vi_tri + trai, v.kf.vi_tri + phai, v.kf.van_toc, v.nua_day)
            else:
                v.doan = (v.kf.vi_tri.copy(), v.kf.vi_tri.copy())
        # bo vet mat qua GIU_VET_GIAY
        for ma in [m for m, v in self.vet.items() if t - v.t_cuoi > GIU_VET_GIAY]:
            self.vet.pop(ma)
        if md.loi:
            self._canh_bao.add(md.loi)
        if self.phat_hien_bao_ho is not None and any(self.vet[m].lop == "nguoi" for m in dang_thay):
            self._bao_ho(img, dang_thay)

        ung_vien = self._tim_rui_ro(dang_thay, t, W, Hh)
        rui_ro, su_kien = self._xac_nhan(ung_vien, t)
        return self._ket_qua(dang_thay, rui_ro, su_kien, t)

    # ------------------------------------------------------------------ do bao ho
    def _bao_ho(self, img, dang_thay):
        """2 TANG: bo do bao ho co ham theo_nguoi(list anh cat, co) -> cat tung nguoi phong to (manh voi nguoi o xa);
        khong co (bo gia lap khi kiem thu) -> chay ca khung"""
        n = self.nguong
        # hop diem thap (bi che mot phan, vd mat dau / than) khong bo phieu: de sinh phieu "thieu" gia
        nguoi = [self.vet[m] for m in dang_thay if self.vet[m].lop == "nguoi" and self.vet[m].diem >= NGUONG_KICH_HOAT
                 and self.vet[m].hop[3] - self.vet[m].hop[1] >= n["bao_ho_cao_toi_thieu"]]
        nguoi = sorted(nguoi, key=lambda v: v.hop[1] - v.hop[3])[:n["bao_ho_toi_da"]]     # uu tien nguoi gan (to)
        if not nguoi:
            return
        theo_nguoi = getattr(self.phat_hien_bao_ho, "theo_nguoi", None)
        if theo_nguoi is not None:
            cat = [cat_nguoi(img, v.hop) for v in nguoi]
            ket = theo_nguoi([c for c, _, _ in cat], CO_CAT_BAO_HO)
            for v, (_, a1, b1), det in zip(nguoi, cat, ket):
                det = np.asarray(det, dtype=float).reshape(-1, 6).copy()
                det[:, [0, 2]] += a1                  # ve toa do khung goc
                det[:, [1, 3]] += b1
                self._phieu_bao_ho(v, det)
        else:
            det = np.asarray(self.phat_hien_bao_ho(img), dtype=float).reshape(-1, 6)
            for v in nguoi:
                self._phieu_bao_ho(v, det)

    @staticmethod
    def _phieu_bao_ho(v, ppe):
        """1 phieu / khung cho nguoi v: co / thieu mu, co / thieu ao (hoac khong ket luan neu khong du dau hieu)"""
        x1, y1, x2, y2 = v.hop
        h = y2 - y1
        d = {PPE_MU: 0.0, PPE_KHONG_MU: 0.0, PPE_AO: 0.0, PPE_KHONG_AO: 0.0}
        for bx1, by1, bx2, by2, cf, c in ppe:
            cx, cy = (bx1 + bx2) / 2, (by1 + by2) / 2
            if not (x1 - 0.1 * (x2 - x1) <= cx <= x2 + 0.1 * (x2 - x1)):
                continue
            tuong_doi = (cy - y1) / h
            c = int(c)
            if (c in (PPE_MU, PPE_KHONG_MU) and -0.1 <= tuong_doi <= 0.4) or \
                    (c in (PPE_AO, PPE_KHONG_AO) and 0.15 <= tuong_doi <= 0.85):
                d[c] = max(d[c], float(cf))
        for phieu, co, thieu, (a, b) in ((v.mu, PPE_MU, PPE_KHONG_MU, LUAT_BAO_HO["mu"]),
                                         (v.ao, PPE_AO, PPE_KHONG_AO, LUAT_BAO_HO["ao"])):
            if d[co] >= a:
                phieu.append(True)
            elif d[thieu] >= b:
                phieu.append(False)          # khong thay mu/ao ma co dau hieu dau tran / than khong ao

    @staticmethod
    def _ket_luan(phieu):
        """True (co) / False (thieu) / None (chua du chung cu): >= 6 quan sat, >= 60% cung chieu"""
        if len(phieu) < 6:
            return None
        ti_le_co = sum(phieu) / len(phieu)
        return True if ti_le_co >= 0.6 else (False if ti_le_co <= 0.4 else None)

    # ------------------------------------------------------------------ tim rui ro (chua xac nhan)
    def _tim_rui_ro(self, dang_thay, t, W, Hh):
        n = self.nguong
        out = []
        vs = [self.vet[m] for m in dang_thay if self._tin(self.vet[m], t)]
        xe = [v for v in vs if v.la_xe]
        nguoi = [v for v in vs if v.lop == "nguoi"]
        # nguoi DANG LAI xe may / xe dap (2 hop rieng cua YOLO): khong xet va cham / qua gan voi CHINH xe do, khong tinh
        # la "nguoi di bo tren lan xe"; van xet voi xe khac (nguoi lai xe may sat xe tai van nguy hiem)
        lai = {(a.ma, b.ma) for a in xe if a.lop in XE_HAI_BANH for b in nguoi if la_nguoi_lai(b, a)}
        nguoi_lai = {mb for _, mb in lai}

        def chan(v):
            return ((v.hop[0] + v.hop[2]) / 2, v.hop[3])

        # 1) SAP VA CHAM + QUA GAN
        for i, a in enumerate(xe):
            for b in xe[i + 1:] + nguoi:
                if b is a or (a.ma, b.ma) in lai:
                    continue
                chay_a, chay_b = a.toc_do() > n["xe_dang_chay"], b.la_xe and b.toc_do() > n["xe_dang_chay"]
                if not (chay_a or chay_b):
                    continue
                R = a.nua_day + b.nua_day
                # TTC chi khi DANG TIEN LAI (du_bao_va_cham): song song / cung toc do khong bao; da sat (<= R) ma con tien
                # lai -> TTC 0, NGUY HIEM (truoc day bo qua d0 <= R: cap sat hon lai khong bi bao)
                ttc_r, ttc_rl, dmin, d0 = du_bao_va_cham(a.doan, a.kf.van_toc, b.doan, b.kf.van_toc, R, n["le_canh_bao"],
                                                         T=n["ttc_canh_bao"], tiep_can=n["tiep_can"])
                loai = "va-cham-xe-xe" if b.la_xe else "va-cham-xe-nguoi"
                muc = ttc = None
                if ttc_r <= n["ttc_nguy_hiem"]:
                    muc, ttc = MUC_NGUY_HIEM, ttc_r
                elif ttc_rl <= n["ttc_canh_bao"]:
                    muc, ttc = MUC_CANH_BAO, ttc_rl
                va_cham = dict(loai=loai, muc=muc, ids=(a.ma, b.ma), ttc=round(ttc, 2), khoangCachM=round(max(d0 - R, 0), 1),
                               dMinM=round(max(dmin - R, 0), 1), diem=[chan(a), chan(b)]) if muc else None
                gan = None
                if not b.la_xe and chay_a:
                    mep = d0 - R
                    if mep < n["gan_nguy_hiem"]:
                        gan = dict(loai="qua-gan-xe", muc=MUC_NGUY_HIEM, ids=(a.ma, b.ma), khoangCachM=round(max(mep, 0), 1),
                                   diem=[chan(a), chan(b)])
                    elif mep < n["gan_canh_bao"]:
                        gan = dict(loai="qua-gan-xe", muc=MUC_CANH_BAO, ids=(a.ma, b.ma), khoangCachM=round(mep, 1),
                                   diem=[chan(a), chan(b)])
                # lay muc CAO hon: va cham CANH BAO khong duoc che mat "qua gan" NGUY HIEM (nguoi sat xe dang chay qua);
                # cung muc -> giu va cham (co TTC)
                if va_cham and (va_cham["muc"] == MUC_NGUY_HIEM or not gan or gan["muc"] != MUC_NGUY_HIEM):
                    out.append(va_cham)
                elif gan:
                    out.append(gan)
        # 2) QUA TOC DO (toan bai + vung gioi han; nhieu vung chong nhau -> gioi han NHO nhat, khong phu thuoc thu tu ve)
        for v in xe:
            kmh = v.toc_do() * 3.6
            gioi_han, ten_vung = n["toc_do_toi_da"], None
            x, y = chan(v)
            trong_vung = False
            for z in self.vung:
                if z.get("loai") == "gioi-han-toc-do" and trong_da_giac(x / W, y / Hh, z["diem"]):
                    gh = float(z.get("gioiHan") or n["toc_do_toi_da"])
                    if not trong_vung or gh < gioi_han:
                        gioi_han, ten_vung = gh, z.get("ten")
                    trong_vung = True
            if kmh > gioi_han:
                out.append(dict(loai="qua-toc-do", muc=MUC_NGUY_HIEM if kmh > 1.5 * gioi_han else MUC_CANH_BAO, ids=(v.ma,),
                                tocDoKmh=round(kmh, 1), gioiHan=gioi_han, vung=ten_vung, diem=[(x, y)]))
        # 3) VUNG NGUY HIEM
        for v in nguoi:
            x, y = chan(v)
            for z in self.vung:
                if not trong_da_giac(x / W, y / Hh, z["diem"]):
                    continue
                if z.get("loai") == "cam-nguoi":
                    out.append(dict(loai="vao-vung-cam", muc=MUC_NGUY_HIEM, ids=(v.ma,), vung=z.get("ten"), diem=[(x, y)]))
                elif z.get("loai") == "lan-xe" and v.ma not in nguoi_lai:
                    gan = [a for a in xe if a.toc_do() > n["xe_dang_chay"]
                           and kc_diem_doan(v.kf.vi_tri, *a.doan) - a.nua_day < 10.0]
                    if gan:
                        out.append(dict(loai="lan-xe", muc=MUC_CANH_BAO, ids=(v.ma, gan[0].ma), vung=z.get("ten"),
                                        diem=[(x, y), chan(gan[0])]))
        # 3b) XE SAP DAM VAT CAN CO DINH (vung "vat-can" ve quanh chan vat tren mat dat)
        for z in self.vung:
            if z.get("loai") != "vat-can":
                continue
            P, ly_do = self._da_giac_met(z, W, Hh)
            if P is None:
                if ly_do:
                    self._canh_bao.add(ly_do)              # bao nguoi van hanh: vung nay dang bi bo qua
                continue
            tam = (float(np.mean([p[0] for p in z["diem"]])) * W, float(np.mean([p[1] for p in z["diem"]])) * Hh)
            for v in xe:
                if v.toc_do() <= n["xe_dang_chay"]:
                    continue
                ttc_r, ttc_l, d0 = du_bao_dam_vat_can(v.doan, v.kf.van_toc, P, v.nua_day, n["le_canh_bao"],
                                                      T=n["ttc_canh_bao"], tiep_can=n["tiep_can"])
                if d0 <= 1e-6:          # mep duoi xe da nam TRONG vung (vung ve de len duong xe / da cham): bo
                    continue
                muc = ttc = None
                if ttc_r <= n["ttc_nguy_hiem"]:
                    muc, ttc = MUC_NGUY_HIEM, ttc_r
                elif ttc_l <= n["ttc_canh_bao"]:
                    muc, ttc = MUC_CANH_BAO, ttc_l
                if muc:
                    out.append(dict(loai="va-cham-vat-can", muc=muc, ids=(v.ma,), ttc=round(ttc, 2),
                                    khoangCachM=round(max(d0 - v.nua_day, 0), 1), vung=z.get("ten"), diem=[chan(v), tam]))
        # 4) NGUOI NGA: dang nam (rong/cao lon) LIEN TUC >= nga_giay THEO THOI GIAN (dung sai 0,15 s) va gan nhu dung yen —
        # khong dem so khung: 1 khung / 1,2 s hay 60 khung/giay deu bao duoc
        for v in nguoi:
            if v.giay_nam(n["nga_ti_le"]) >= n["nga_giay"] - 0.15 and v.toc_do() < n["nga_toc_do"]:
                out.append(dict(loai="nguoi-nga", muc=MUC_NGUY_HIEM, ids=(v.ma,), diem=[chan(v)]))
        # 5) THIEU BAO HO
        if n["can_bao_ho"]:
            for v in nguoi:
                if self._ket_luan(v.mu) is False:
                    out.append(dict(loai="thieu-mu", muc=MUC_CANH_BAO, ids=(v.ma,), diem=[chan(v)]))
                if self._ket_luan(v.ao) is False:
                    out.append(dict(loai="thieu-ao", muc=MUC_CANH_BAO, ids=(v.ma,), diem=[chan(v)]))
        return out

    # ------------------------------------------------------------------ chong bao dong gia
    def _xac_nhan(self, ung_vien, t):
        n = self.nguong
        hien_tai = {}
        for r in ung_vien:
            k = (r["loai"],) + tuple(sorted(r["ids"]))
            if k not in hien_tai or (r["muc"] == MUC_NGUY_HIEM and hien_tai[k]["muc"] != MUC_NGUY_HIEM):
                hien_tai[k] = r
        for k in set(self.cua_so) | set(hien_tai):
            self.cua_so[k].append(k in hien_tai)
        rui_ro, su_kien = [], []
        N, K = self._cua_so()
        for k, r in hien_tai.items():
            w = list(self.cua_so[k])[-N:]
            if sum(w) < K:
                continue
            r = dict(r, ma=":".join(map(str, k)), moTa=MO_TA[r["loai"]])
            rui_ro.append(r)
            khoa_bao = k + (r["muc"],)
            vua_bao_nguy_hiem = r["muc"] != MUC_NGUY_HIEM and t - self.lan_bao.get(k + (MUC_NGUY_HIEM,), -1e9) < n["nghi_giay"]
            if not vua_bao_nguy_hiem and t - self.lan_bao.get(khoa_bao, -1e9) >= n["nghi_giay"]:
                self.lan_bao[khoa_bao] = t
                self.dem[r["loai"]] += 1
                su_kien.append(dict(r, t=round(t, 2)))
        # don cua so cua khoa khong con rui ro trong N khung gan nhat (bo di khong doi ket qua: K >= 2)
        for k in [k for k, w in self.cua_so.items() if not any(list(w)[-N:])]:
            self.cua_so.pop(k)
        # don lan_bao: khoa da nghi xong (t - lan_bao >= nghi_giay) tuong duong khong co khoa -> xoa (ma ByteTrack tang mai,
        # camera chay 24/7 khong lam dict phinh vo han). Moi ~5 s mot lan; thoi gian lui (tua video) -> don lai tu dau
        if self._t_don is None or t - self._t_don >= 5.0 or t < self._t_don:
            self._t_don = t
            for k in [k for k, tb in self.lan_bao.items() if t - tb >= n["nghi_giay"]]:
                self.lan_bao.pop(k)
        rui_ro.sort(key=lambda r: (r["muc"] != MUC_NGUY_HIEM, r.get("ttc", 99)))
        return rui_ro, su_kien

    # ------------------------------------------------------------------ dau ra
    def _ket_qua(self, dang_thay, rui_ro, su_kien, t):
        doi_tuong = []
        dang_rui_ro = {i for r in rui_ro for i in r["ids"]}
        for ma in sorted(dang_thay):
            v = self.vet[ma]
            vt, vv = v.kf.vi_tri, v.kf.van_toc
            o = dict(id=ma, lop=v.lop, ten=TEN_LOP[v.lop], hop=[round(x, 1) for x in v.hop],
                     viTriM=[round(float(vt[0]), 2), round(float(vt[1]), 2)],
                     tocDoKmh=round(v.toc_do() * 3.6, 1) if self._tin(v, t) else None,
                     nguyHiem=ma in dang_rui_ro)
            if v.lop == "nguoi":
                o["baoHo"] = dict(mu=self._ket_luan(v.mu), ao=self._ket_luan(v.ao))
            # duong du bao 3 giay toi (ve len anh) cho doi tuong dang chuyen dong
            if self._tin(v, t) and v.toc_do() > 0.5:
                o["duBao"] = [[round(c, 1) for c in self.mat_dat.sang_anh(vt + vv * k)] for k in (0.0, 1.0, 2.0, 3.0)]
            doi_tuong.append(o)
        # canhBaoCauHinh: hieu chinh 4 diem khong hop le, vung vat can bi bo qua... (giao dien hien cho nguoi van hanh)
        return dict(t=round(t, 3), khung=self.so_khung, fpsThuc=round(self.fps_thuc, 1), matDat=self.mat_dat.che_do,
                    doiTuong=doi_tuong,
                    ruiRo=[self._json(r) for r in rui_ro], suKien=[self._json(r) for r in su_kien],
                    demSuKien=dict(self.dem), canhBaoCauHinh=sorted(self._canh_bao))

    @staticmethod
    def _json(r):
        r = dict(r)
        r["doiTuong"] = list(r.pop("ids"))
        r["diem"] = [[round(float(a), 1), round(float(b), 1)] for a, b in r.get("diem", [])]
        return r
