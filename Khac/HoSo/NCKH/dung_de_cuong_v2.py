# -*- coding: utf-8 -*-
"""
Dung ban de cuong NCKH v2 tu MAU GOC cua Vien + noi dung trong noi_dung_de_cuong.py.

Thay doi so voi ban truoc (theo yeu cau GVHD ngay 29/09/2026):
  - Doi ten de tai cho khop noi dung (truc: giai ma co rang buoc ISO 6346).
  - Viet lai toan bo muc 8..13 theo van phong khoa hoc, bo em-dash va khau ngu.
  - Sua 6 loi trong danh muc tai lieu tham khao da kiem chung.

Chay:  python dung_de_cuong_v2.py
"""
import copy
import os
import shutil

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.table import _Row
from docx.text.paragraph import Paragraph

import noi_dung_de_cuong as ND

HERE = os.path.dirname(os.path.abspath(__file__))
MAU = os.path.join(HERE, "_Mau_goc_Vien.docx")
OUT = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_HOANCHINH_LeHuuAnhTai_2424802010066.docx")
FONT, SZ = "Times New Roman", 12

shutil.copyfile(MAU, OUT)
doc = Document(OUT)
BODY = doc.element.body
T_TV, T_TD, T_KY = doc.tables[0], doc.tables[1], doc.tables[2]

A_NS = "http://schemas.openxmlformats.org/drawingml/2006/main"


# ------------------------------------------------------------------ tien ich
def _style(r, bold=False, italic=False, size=SZ):
    r.font.name, r.font.size, r.bold, r.italic = FONT, Pt(size), bold, italic
    rf = r._element.get_or_add_rPr().get_or_add_rFonts()
    rf.set(qn("w:eastAsia"), FONT)


def _runs(p, text, bold=False, italic=False, size=SZ):
    for i, seg in enumerate(text.split("**")):
        if seg:
            _style(p.add_run(seg), bold or (i % 2 == 1), italic, size)


def _la():
    """Chi doan LA — tren trang bia nhieu textbox nam chung mot <w:p>."""
    for p in BODY.iter(qn("w:p")):
        if not any(c is not p for c in p.iter(qn("w:p"))):
            yield p


def thay(old, new):
    n = 0
    for p in _la():
        ts = [t for t in p.iter(qn("w:t"))]
        if not ts:
            continue
        gop = "".join(t.text or "" for t in ts)
        if old in gop:
            ts[0].text = gop.replace(old, new)
            ts[0].set(qn("xml:space"), "preserve")
            for t in ts[1:]:
                t.text = ""
            n += 1
    return n


def tim(prefix):
    for p in doc.paragraphs:
        if p.text.strip().startswith(prefix):
            return p
    raise KeyError(prefix)


def dat(p, text, bold=False, size=SZ):
    for r in list(p.runs):
        r._element.getparent().remove(r._element)
    _runs(p, text, bold, size=size)
    return p


def chen(anchor, text="", bold=False, italic=False, align="just",
         indent=0.8, left=0.0, after=4, size=SZ):
    new = copy.deepcopy(anchor._p)
    for c in list(new):
        if c.tag != qn("w:pPr"):
            new.remove(c)
    anchor._p.addnext(new)
    p = Paragraph(new, anchor._parent)
    pf = p.paragraph_format
    pf.line_spacing, pf.space_after, pf.space_before = 1.35, Pt(after), Pt(0)
    pf.first_line_indent, pf.left_indent = Cm(indent), Cm(left)
    p.alignment = {"just": WD_ALIGN_PARAGRAPH.JUSTIFY, "l": WD_ALIGN_PARAGRAPH.LEFT,
                   "c": WD_ALIGN_PARAGRAPH.CENTER}[align]
    if text:
        _runs(p, text, bold, italic, size)
    return p


def o(cell, text, bold=False, align="l", size=SZ):
    cell.text = ""
    p = cell.paragraphs[0]
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.space_after = Pt(2)
    p.alignment = {"l": WD_ALIGN_PARAGRAPH.LEFT, "c": WD_ALIGN_PARAGRAPH.CENTER,
                   "just": WD_ALIGN_PARAGRAPH.JUSTIFY}[align]
    _runs(p, text, bold, size=size)


def tick(para, k):
    s = [x for x in para._p.iter(qn("w:sym"))][k]
    s.set(qn("w:font"), "Wingdings 2")
    s.set(qn("w:char"), "F052")


# ------------------------------------------------------- 1. Trang bia
thay("TÊN ĐỀ TÀI", ND.TEN_VI)
thay("Nguyễn Văn A (Nhóm trưởng)", "Lê Hữu Anh Tài (Nhóm trưởng)")



def thay_o_bia(dung_bang, new):
    """Chi thay trong textbox trang bia va khi noi dung TRUNG KHOP HOAN TOAN.

    Khong dung thay() cho chuoi ngan nhu "2." vi no se dung vao ca dong
    "2. Loai hinh nghien cuu" o phan than bai.
    """
    for p in _la():
        if p.getparent().tag != qn("w:txbxContent"):
            continue
        ts = [t for t in p.iter(qn("w:t"))]
        if ts and "".join(t.text or "" for t in ts).strip() == dung_bang:
            ts[0].text = new
            ts[0].set(qn("xml:space"), "preserve")
            for t in ts[1:]:
                t.text = ""
            return True
    return False


thay_o_bia("2.", "2. Nguyễn Quang Long")
thay_o_bia("3.", "3. Nguyễn Thành Phát")
thay("TS/ThS.", "TS. Huỳnh Nguyễn Thành Luân")
thay("20…-20…", "2026-2027")
thay("201…-201…", "2026-2027")
thay("năm 201…", "năm 2026")
thay("Thành phố Hồ Chí Minh, ngày       tháng       năm",
     "Thành phố Hồ Chí Minh, ngày       tháng       năm 2026")

# Ten de tai tren bia: IN HOA, noi khung cho du cho
for p in _la():
    ts = [t for t in p.iter(qn("w:t"))]
    if not ts or "".join(t.text or "" for t in ts).strip() != ND.TEN_VI:
        continue
    if p.getparent().tag != qn("w:txbxContent"):
        continue
    ts[0].text = ND.TEN_VI.upper()
    node = p
    while node is not None and node.tag.split("}")[-1] not in ("AlternateContent", "drawing", "pict"):
        node = node.getparent()
    if node is not None:
        for the in ("w:sz", "w:szCs"):
            for e in node.iter(qn(the)):
                e.set(qn("w:val"), "32")
        for e in node.iter():
            if e.tag.split("}")[-1] in ("ext", "extent") and e.get("cy"):
                e.set("cy", "1600000")
            if e.tag.split("}")[-1] == "bodyPr":
                e.append(e.makeelement("{%s}spAutoFit" % A_NS, {}))

# ------------------------------------------------------- 2. Muc 1..7
chen(tim("1. Tên đề tài:"), ND.TEN_VI + ".")
chen(tim("1. Tên đề tài:"), "Tên tiếng Anh: " + ND.TEN_EN + ".", italic=True)
tick(tim("2. Loại hình nghiên cứu"), 1)

p4 = tim("4. Thời gian thực hiện")
dat(p4, "4. Thời gian thực hiện:  12 tháng", bold=True)
dat(Paragraph(p4._p.getnext(), p4._parent),
    "            Từ tháng   10   năm  2026          đến  tháng  09     năm 2027")
dat(tim("Đơn vị: Viện Công nghệ số"),
    "Đơn vị: Viện Công nghệ số                  Chương trình đào tạo: Công nghệ thông tin")
dat(tim("Họ và tên:"),
    "Họ và tên: Huỳnh Nguyễn Thành Luân                              Học vị: Tiến sĩ")
dat(tim("Địa chỉ nhà riêng:"), "Địa chỉ nhà riêng: Phường Bình Dương, Thành phố Hồ Chí Minh")
dat(tim("Di động:"), "Di động: 0777833681                    E-mail: luanhnt@tdmu.edu.vn")

for i, (ten, mssv, lop, gc) in enumerate([
    ("Lê Hữu Anh Tài", "2424802010066", "D24CNTT05", "Nhóm trưởng"),
    ("Nguyễn Quang Long", "2324801030095", "D23KTPM01", ""),
    ("Nguyễn Thành Phát", "2324802010313", "D23CNTT01", ""),
], start=1):
    r = T_TV.rows[i]
    o(r.cells[1], ten)
    o(r.cells[2], mssv, align="c")
    o(r.cells[3], lop, align="c")
    if gc:
        o(r.cells[4], gc, align="c")

for nhan, gt in (("Họ và tên:", "Họ và tên: Lê Hữu Anh Tài"),
                 ("SĐT:", "SĐT: 0387 562 579"),
                 ("Email:", "Email: lehuuanhtai2020tkh@gmail.com")):
    for p in doc.paragraphs:
        if p.text.strip() == nhan:
            dat(p, gt)
            break

# ------------------------------------------------------- 3. Muc 8
cur = tim("8. Tính cấp thiết")
for doan in ND.MUC_8:
    cur = chen(cur, doan)

# ------------------------------------------------------- 4. Muc 9
cur = tim("9. Mục tiêu đề tài")
cur = chen(cur, ND.MUC_9_TQ)
cur = chen(cur, "Mục tiêu cụ thể của đề tài được trình bày trong bảng sau.", after=6)

bang_mt = doc.add_table(rows=1, cols=3)
bang_mt.style = "Table Grid"
for i, h in enumerate(["TT", "Mục tiêu cụ thể", "Tiêu chí đánh giá"]):
    o(bang_mt.rows[0].cells[i], h, bold=True, align="c")
for tt, mt, tc in ND.MUC_9_CT:
    c = bang_mt.add_row().cells
    o(c[0], tt, align="c")
    o(c[1], mt)
    o(c[2], tc)
for r in bang_mt.rows:
    for i, w in enumerate([1.3, 5.2, 9.5]):
        r.cells[i].width = Cm(w)
cur._p.addnext(bang_mt._tbl)

# ------------------------------------------------------- 5. Muc 10, 11
def khoi_muc(prefix, du_lieu):
    cur = tim(prefix)
    for tieu_de, doans in du_lieu:
        cur = chen(cur, tieu_de, bold=True, italic=True, indent=0.5, after=3)
        for d in doans:
            cur = chen(cur, d)
    return cur


khoi_muc("10. Tổng quan", ND.MUC_10)
khoi_muc("11. Đối tượng", ND.MUC_11)

# ------------------------------------------------------- 6. Muc 12.1
cur = tim("12.1. Nội dung nghiên cứu")
for ch, subs in ND.CHUONG:
    cur = chen(cur, ch, bold=True, indent=0.5, align="l", after=2)
    for s in subs:
        cur = chen(cur, s, indent=0.0, left=1.2, align="l", after=1)
    cur = chen(cur, "", after=2)

# ------------------------------------------------------- 7. Muc 12.2
# Cot "Nguoi thuc hien" trong mau chi rong 1496 dxa (2,64 cm). Chuoi dai se bi Word
# ngat xuong TUNG KY TU, lam hang cao gan het trang. Phan bo lai, giu nguyen tong 9537.
_grid_td = (2400, 2800, 2137, 2200)
_g = T_TD._tbl.find(qn("w:tblGrid"))
for _gc, _w in zip(_g.findall(qn("w:gridCol")), _grid_td):
    _gc.set(qn("w:w"), str(_w))
for _r in T_TD.rows:
    for _i, _w in enumerate(_grid_td):
        if _i < len(_r.cells):
            _tcW = _r.cells[_i]._tc.get_or_add_tcPr().find(qn("w:tcW"))
            if _tcW is not None:
                _tcW.set(qn("w:w"), str(_w))
                _tcW.set(qn("w:type"), "dxa")

mau_row = T_TD.rows[1]._tr
for i, (tg, nd, sp) in enumerate(ND.TIEN_DO):
    tr = mau_row if i == 0 else copy.deepcopy(mau_row)
    if i:
        T_TD._tbl.append(tr)
    r = _Row(tr, T_TD)
    o(r.cells[0], tg)
    o(r.cells[1], nd)
    o(r.cells[2], sp)
    o(r.cells[3], "Cả nhóm, Lê Hữu Anh Tài chủ trì" if i in (2, 4, 6) else "Lê Hữu Anh Tài")

# ------------------------------------------------------- 8. Muc 13
cur = tim("13. Sản phẩm")
cur = chen(cur, "a) Sản phẩm của đề tài", bold=True, italic=True, indent=0.5, after=3)
bang_sp = doc.add_table(rows=1, cols=4)
bang_sp.style = "Table Grid"
for i, h in enumerate(["TT", "Tên sản phẩm", "Yêu cầu chất lượng", "Số lượng"]):
    o(bang_sp.rows[0].cells[i], h, bold=True, align="c")
for tt, ten, yc, sl in ND.SAN_PHAM:
    c = bang_sp.add_row().cells
    o(c[0], tt, align="c")
    o(c[1], ten)
    o(c[2], yc)
    o(c[3], sl, align="c")
for r in bang_sp.rows:
    for i, w in enumerate([1.0, 4.4, 9.4, 1.2]):
        r.cells[i].width = Cm(w)
cur._p.addnext(bang_sp._tbl)

sau = chen(cur, "b) Khả năng ứng dụng", bold=True, italic=True, indent=0.5, after=3)
bang_sp._tbl.addnext(sau._p)
chen(sau, ND.UNG_DUNG)

# ------------------------------------------------------- 9. Muc 14
REFS = ND.REFS
cur = tim("14. Tài liệu tham khảo")
for i, r in enumerate(REFS, 1):
    cur = chen(cur, "[%d]  %s" % (i, r), indent=-1.0, left=1.6, after=3)

# ------------------------------------------------------- 10. Muc 15 + ky
dat(tim("Kinh phí thực hiện (đồng):"), "Kinh phí thực hiện (đồng): 4.950.000 đồng", bold=True)
dat(tim("Bằng chữ:"), "Bằng chữ: Bốn triệu chín trăm năm mươi nghìn đồng", bold=True)

grid = T_KY._tbl.find(qn("w:tblGrid"))
for gc, w in zip(grid.findall(qn("w:gridCol")), (4200, 1015, 3847)):
    gc.set(qn("w:w"), str(w))
for r in T_KY.rows:
    for i, w in enumerate((4200, 1015, 3847)):
        if i < len(r.cells):
            tcW = r.cells[i]._tc.get_or_add_tcPr().find(qn("w:tcW"))
            if tcW is not None:
                tcW.set(qn("w:w"), str(w))
                tcW.set(qn("w:type"), "dxa")
for cell, ten, truoc in ((T_KY.rows[0].cells[0], "Huỳnh Nguyễn Thành Luân", 36),
                         (T_KY.rows[0].cells[2], "Lê Hữu Anh Tài", 24)):
    p = cell.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(truoc)
    p.paragraph_format.space_after = Pt(0)
    _style(p.add_run(ten), bold=True)

doc.save(OUT)
print("DA LUU:", OUT)
