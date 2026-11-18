# -*- coding: utf-8 -*-
"""
Chot ban de cuong NCKH de nop: doi ten de tai + dong bo noi dung theo ten moi.

Nguon: ThuyetMinh_DeCuong_NCKH_PA2-EdgeAI_....docx  (ban PA2, 23/09/2026)
Ra:    ThuyetMinhDeCuong_NCKH_CHOT_....docx

Sua 5 cho:
  1. Ten de tai (trang bia + muc 1): "Nghien cuu danh gia hieu nang..." -> "Toi uu hieu nang..."
  2. Muc tieu tong quat: them ve "toi uu" cho khop ten moi
  3. Bang muc tieu: chen MT ve toi uu (thanh MT4), don MT4/MT5 cu xuong MT5/MT6
  4. Muc 8: bo sung ket qua toi uu so bo (bang chung kha thi, co so lieu that)
  5. Pham vi du lieu: goi dung ten du lieu demo, khong de hieu nham la du lieu van hanh that
"""
import copy
import os
import shutil

from docx import Document
from docx.oxml.ns import qn
from docx.shared import Pt

FONT = "Times New Roman"

HERE = os.path.dirname(os.path.abspath(__file__))
NGUON = os.path.join(HERE, "ThuyetMinh_DeCuong_NCKH_PA2-EdgeAI_LeHuuAnhTai_2424802010066.docx")
DICH = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_CHOT_LeHuuAnhTai_2424802010066.docx")

TEN_CU = "Nghiên cứu đánh giá hiệu năng suy luận học sâu trên trình duyệt cho bài toán nhận dạng tự động tại cổng cảng container"
TEN_MOI = "Tối ưu hiệu năng suy luận học sâu tại biên cho nhận dạng tự động tại cổng cảng container"

shutil.copyfile(NGUON, DICH)
doc = Document(DICH)
BODY = doc.element.body


def thay_trong_doan(p_el, old, new):
    """Thay chuoi trong 1 doan, ke ca khi Word chia chuoi ra nhieu <w:t>."""
    ts = [t for t in p_el.iter(qn("w:t"))]
    if not ts:
        return False
    gop = "".join(t.text or "" for t in ts)
    if old not in gop:
        return False
    moi = gop.replace(old, new)
    ts[0].text = moi
    ts[0].set(qn("xml:space"), "preserve")
    for t in ts[1:]:
        t.text = ""
    return True


def _doan_la():
    """Chi tra ve doan LA (khong chua doan khac ben trong).

    Tren trang bia, NHIEU textbox cung nam trong MOT <w:p> bao ngoai. Neu gom text
    o cap doan bao do thi se don chu cua tat ca textbox vao mot cho roi xoa phan
    con lai -> mat ten de tai, mat SV/GVHD, lap lai dong dia danh. Phai xuong toi
    doan la moi thay an toan.
    """
    for p_el in BODY.iter(qn("w:p")):
        if any(con is not p_el for con in p_el.iter(qn("w:p"))):
            continue
        yield p_el


def thay_khap_noi(old, new):
    """Duyet moi doan la cua tai lieu, ke ca doan nam trong textbox trang bia."""
    n = 0
    for p_el in _doan_la():
        if thay_trong_doan(p_el, old, new):
            n += 1
    return n


# ---------------------------------------------------------------- 1. Ten de tai
print("1) Doi ten de tai   :", thay_khap_noi(TEN_CU, TEN_MOI), "cho")

# Ten moi ngan hon ten cu (88 vs 118 ky tu) nen tra lai dang bia cho dung mau:
# IN HOA, co chu lon. Chi dung vao dung textbox chua ten, khong dung textbox khac.
def dinh_dang_ten_tren_bia(co_chu_half_pt=32):
    for p_el in _doan_la():
        ts = [t for t in p_el.iter(qn("w:t"))]
        if not ts or "".join(t.text or "" for t in ts).strip() != TEN_MOI:
            continue
        if p_el.getparent().tag != qn("w:txbxContent"):
            continue                      # chi doi tren trang bia, giu nguyen muc 1
        ts[0].text = TEN_MOI.upper()
        for r in p_el.iter(qn("w:r")):
            rPr = r.find(qn("w:rPr"))
            if rPr is None:
                rPr = r.makeelement(qn("w:rPr"), {})
                r.insert(0, rPr)
            for the in ("w:sz", "w:szCs"):
                e = rPr.find(qn(the))
                if e is None:
                    e = rPr.makeelement(qn(the), {})
                    rPr.append(e)
                e.set(qn("w:val"), str(co_chu_half_pt))


dinh_dang_ten_tren_bia()

# ------------------------------------------------- 2. Muc tieu tong quat
MT_TQ_CU = ("Mục tiêu tổng quát: đánh giá định lượng khả năng và giới hạn của suy luận học sâu chạy "
            "hoàn toàn trên trình duyệt cho bài toán nhận dạng tự động tại cổng cảng container, từ đó "
            "đề xuất cấu hình triển khai phù hợp cho doanh nghiệp logistics vừa và nhỏ không có hạ tầng "
            "máy chủ AI.")
MT_TQ_MOI = ("Mục tiêu tổng quát: đánh giá định lượng và tối ưu hiệu năng suy luận học sâu chạy hoàn toàn "
             "tại biên — cụ thể là ngay trên trình duyệt — cho bài toán nhận dạng tự động tại cổng cảng "
             "container; trên cơ sở đó đề xuất và kiểm chứng các biện pháp tăng tốc, đồng thời khuyến nghị "
             "cấu hình triển khai phù hợp cho doanh nghiệp logistics vừa và nhỏ không có hạ tầng máy chủ AI.")
print("2) Muc tieu tong quat:", thay_khap_noi(MT_TQ_CU, MT_TQ_MOI), "cho")

# ------------------------------------------- 3. Chen MT ve toi uu vao bang muc tieu
BANG_MT = doc.tables[1]
hang_mau = BANG_MT.rows[3]._tr           # dong MT3 — dung lam khuon de giu dinh dang
hang_moi = copy.deepcopy(hang_mau)
hang_mau.addnext(hang_moi)

from docx.table import _Row  # noqa: E402

r_moi = _Row(hang_moi, BANG_MT)
noi_dung_moi = (
    "MT4",
    "Đề xuất và kiểm chứng các biện pháp tăng tốc suy luận tại biên",
    "Giảm ≥ 30% độ trễ suy luận so với cấu hình mặc định, thông qua cách ly nguồn gốc (COOP/COEP), "
    "lựa chọn backend WebAssembly/WebGPU và tiền xử lý ảnh; mỗi biện pháp được đo tách riêng để xác "
    "định mức đóng góp",
)
for o, txt in zip(r_moi.cells, noi_dung_moi):
    ts = [t for t in o._tc.iter(qn("w:t"))]
    if ts:
        ts[0].text = txt
        ts[0].set(qn("xml:space"), "preserve")
        for t in ts[1:]:
            t.text = ""

# Don so thu tu cua 2 muc tieu phia sau (lam tu duoi len de khong de nhau)
for cu, moi in (("MT5", "MT6"), ("MT4", "MT5")):
    for r in list(BANG_MT.rows)[5:][::-1]:
        o = r.cells[0]
        if o.text.strip() == cu:
            ts = [t for t in o._tc.iter(qn("w:t"))]
            if ts:
                ts[0].text = moi
                for t in ts[1:]:
                    t.text = ""
            break
print("3) Chen MT4 (toi uu) + don so thu tu: xong")

# --------------------------------- 4. Bo sung ket qua toi uu so bo vao muc 8
NEO = ("Nói cách khác, hệ thống cần đo đã có sẵn; toàn bộ 12 tháng của đề tài dành cho thiết kế thực "
       "nghiệm, thu thập dữ liệu kiểm thử và đo đạc, thay vì xây dựng hạ tầng phần mềm.")
BO_SUNG = (NEO + " Trong quá trình phát triển hệ thống, nhóm cũng đã thực hiện một số phép đo và tối ưu "
           "sơ bộ trên chính các mô-đun này: áp dụng cách ly nguồn gốc (COOP/COEP) giúp giảm độ trễ suy "
           "luận YOLO từ 167 ms xuống 100 ms; bổ sung dữ liệu tiếng Việt cho Tesseract nâng số trường bóc "
           "đúng trên giấy phép lái xe từ 3/8 lên 8/8; thay thư viện giải mã QR jsQR bằng zxing-wasm đưa "
           "tỷ lệ đọc thành công từ 0% lên khoảng 88% với thời gian xử lý khoảng 20 ms. Đây mới là các "
           "phép đo lẻ chưa có thiết kế thực nghiệm chặt chẽ, nhưng đủ cho thấy dư địa tối ưu là có thật "
           "và đo được — đó chính là điểm xuất phát của đề tài.")
print("4) Ket qua so bo    :", thay_khap_noi(NEO, BO_SUNG), "cho")

# ------------------------------- 5. Goi dung ten du lieu (khong de hieu nham la that)
PV_CU = ("Phạm vi dữ liệu: ảnh cổng thu thập từ hệ thống SmartPort đang vận hành, bổ sung ảnh chụp thực "
         "địa tại ICD/depot khu vực Bình Dương và ảnh từ nguồn công khai; phân tầng theo điều kiện chụp.")
PV_MOI = ("Phạm vi dữ liệu: ảnh cổng lấy từ kho ảnh mẫu của hệ thống SmartPort (dữ liệu mô phỏng phục vụ "
          "phát triển, không phải dữ liệu vận hành của doanh nghiệp), bổ sung ảnh chụp thực địa tại "
          "ICD/depot khu vực Bình Dương trong điều kiện được đơn vị cho phép, và ảnh từ các nguồn công "
          "khai; toàn bộ được phân tầng theo điều kiện chụp. Đề tài không sử dụng dữ liệu vận hành thật "
          "của doanh nghiệp, và điều này được nêu rõ trong phần mô tả bộ dữ liệu.")
print("5) Pham vi du lieu  :", thay_khap_noi(PV_CU, PV_MOI), "cho")

# --------------------------------------- 6. Dien thanh vien nhom (muc 7 + trang bia)
THANH_VIEN = [
    # (TT, ho ten, MSSV, lop, ghi chu)
    ("1", "Lê Hữu Anh Tài", "2424802010066", "D24CNTT05", "Nhóm trưởng"),
    ("2", "Nguyễn Quang Long", "2324801030095", "D23KTPM01", ""),
    ("3", "Nguyễn Thành Phát", "2324802010313", "D23CNTT01", ""),
]


def _ghi_o(o, txt):
    ts = [t for t in o._tc.iter(qn("w:t"))]
    if ts:
        ts[0].text = txt
        ts[0].set(qn("xml:space"), "preserve")
        for t in ts[1:]:
            t.text = ""
    elif txt:
        o.paragraphs[0].add_run(txt)


BANG_TV = doc.tables[0]
for i, (_stt, ten, mssv, lop, ghi_chu) in enumerate(THANH_VIEN, start=1):
    r = BANG_TV.rows[i]
    _ghi_o(r.cells[1], ten)
    _ghi_o(r.cells[2], mssv)
    _ghi_o(r.cells[3], lop)
    if ghi_chu:
        _ghi_o(r.cells[4], ghi_chu)
print("6) Dien 3 thanh vien: xong")

# Trang bia: them ten thanh vien 2 va 3 vao danh sach SV
for cu, moi in (("2.", "2. Nguyễn Quang Long"), ("3.", "3. Nguyễn Thành Phát")):
    for p_el in _doan_la():
        if p_el.getparent().tag != qn("w:txbxContent"):
            continue
        ts = [t for t in p_el.iter(qn("w:t"))]
        if ts and "".join(t.text or "" for t in ts).strip() == cu:
            ts[0].text = moi
            ts[0].set(qn("xml:space"), "preserve")
            for t in ts[1:]:
                t.text = ""
            break

# ------------------------------ 7. Phan cong lai tien do cho 3 nguoi
# Co 3 thanh vien ma cot "Nguoi thuc hien" chi ghi mot ten thi hoi dong se hoi ngay.
TAI, LONG, PHAT = "Lê Hữu Anh Tài", "Nguyễn Quang Long", "Nguyễn Thành Phát"
# Tai thuc hien toan bo chuyen mon; Long va Phat chi HO TRO o khau can nhieu nhan
# luc (gan nhan >=800 anh, do lap lai nhieu cau hinh) — day la viec that su can nguoi
# phu nen ghi nhu vay vua dung thuc te vua hop ly truoc hoi dong.
# Ghi GON TRONG MOT DONG va dung lai _ghi_o. Neu tao them paragraph moi trong o,
# paragraph do ke thua style Normal cua mau -> hang bang phinh to, bang tien do bi
# tran tu 1 trang ra 4 trang (da dinh loi nay mot lan).
HO_TRO = TAI + " (chủ trì); " + LONG + ", " + PHAT + " (hỗ trợ)"
PHAN_CONG = [
    TAI,        # tong quan, thiet ke thuc nghiem
    HO_TRO,     # thu thap va gan nhan du lieu — can nguoi phu
    TAI,        # do do chinh xac
    TAI,        # do hieu nang, toi uu
    HO_TRO,     # do lap lai nhieu cau hinh, tich hop
    TAI,        # viet bai bao
    HO_TRO,     # hoan thien bao cao tong ket
]
BANG_TD = doc.tables[2]
for i, nguoi in enumerate(PHAN_CONG, start=1):
    if i < len(BANG_TD.rows):
        _ghi_o(BANG_TD.rows[i].cells[3], nguoi)
print("7) Phan cong tien do: xong")

doc.save(DICH)
print("\nDA LUU:", DICH)
