# -*- coding: utf-8 -*-
"""
Dong bo thong tin nhom va GVHD tren TOAN BO de cuong (cap nhat 30/09/2026).

Tai tu sua trong Word (Downloads/...FINAL...docx): nhom truong Nguyen Phan Quoc Hung,
Tai la thanh vien 2, bo Nguyen Quang Long, GVHD moi, ngay 30 tren bia. Nhung con sot:
bia van ghi GVHD cu, o ky van ten thay Luan, bang tien do van ghi Long, muc 8 van noi
"sinh vien chu nhiem de tai xay dung" SmartPort (nay chu nhiem la Hung).

Script dat LAI TOAN BO cac truong duoi day (idempotent), chay duoc tren ca file Tai da sua
lan ban dung lai bang lam_theo_mau_sang.py:
    python dong_bo_thanh_vien.py [duong_dan.docx ...]
"""
import os
import sys
import time

from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.text.paragraph import Paragraph
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_ALIGN_VERTICAL

HERE = os.path.dirname(os.path.abspath(__file__))

# ------------------------------------------------------------------ Thong tin chot
THANH_VIEN = [  # (ho ten, MSSV, lop, ghi chu)
    ("Nguyễn Phan Quốc Hưng", "2324802010046", "D23CNTT03", "Nhóm trưởng"),
    ("Lê Hữu Anh Tài", "2424802010066", "D24CNTT05", ""),
]
# Ten GVHD theo trang cua Truong/Vien: "ThS. Trinh Trong Thanh" (email thanhtt@ khop quy uoc
# ten + chu cai dau ho, dem). Ban Tai go co chu "Huynh" thua - con sot tu ten thay Luan.
GVHD_TEN = "Trịnh Trọng Thành"
GVHD_HOC_VI = "Thạc sĩ"
GVHD_VIET_TAT = "ThS."
GVHD_DIA_CHI = "Số 6 Trần Văn Ơn, phường Phú Lợi, Thành phố Hồ Chí Minh"  # dia gioi tu 01/7/2025
GVHD_DI_DONG = "0362733555"
GVHD_EMAIL = "thanhtt@tdmu.edu.vn"
NGAY_BIA = "30"
# SV chiu trach nhiem chinh (muc 7 + o ky) - Tai gui 30/09/2026. SDT trung SDT cua Tai
# (Tai de so cua minh lam dau moi lien lac) - giu dung nhu Tai gui.
SV_CHINH_TEN = "Nguyễn Phan Quốc Hưng"
SV_CHINH_SDT = "0387 562 579"
SV_CHINH_EMAIL = "2324802010046@student.tdmu.edu.vn"
# Kinh phi Tai chot 30/09/2026: 4tr9 (truoc la 4.950.000)
KINH_PHI_SO = "4.900.000"
KINH_PHI_CHU = "Bốn triệu chín trăm nghìn đồng"


def text_of(el):
    return "".join(t.text or "" for t in el.iter(qn("w:t")))


def dat_text(p, text):
    """Ghi text vao w:t dau tien cua doan, xoa cac w:t con lai (giu dinh dang run dau)."""
    ts = list(p.iter(qn("w:t")))
    if not ts:
        return False
    ts[0].text = text
    ts[0].set(qn("xml:space"), "preserve")
    for t in ts[1:]:
        t.text = ""
    return True


def trong_textbox(el):
    return any(a.tag == qn("w:txbxContent") for a in el.iterancestors())


def ghi_o(cell, text, canh="l"):
    tc = cell._tc
    for p in list(tc.iter(qn("w:p"))):
        p.getparent().remove(p)
    p = OxmlElement("w:p")
    tc.append(p)
    para = Paragraph(p, cell)
    pf = para.paragraph_format
    pf.left_indent = pf.right_indent = pf.first_line_indent = Cm(0)
    pf.line_spacing = 1.15
    pf.space_before = pf.space_after = Pt(2)
    para.alignment = WD_ALIGN_PARAGRAPH.CENTER if canh == "c" else WD_ALIGN_PARAGRAPH.LEFT
    runs = []
    for k, dong in enumerate(text.split("\n")):
        if k:  # run rieng cho dau xuong dong; gan run.text se xoa mat <w:br/>
            br = para.add_run()
            br.add_break()
            runs.append(br)
        runs.append(para.add_run(dong))
    for run in runs:
        run.font.name = "Times New Roman"
        run.font.size = Pt(13)
        run._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:eastAsia"), "Times New Roman")
    cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER


def dat_rong(tbl, cols):
    g = tbl._tbl.find(qn("w:tblGrid"))
    for gc, w in zip(g.findall(qn("w:gridCol")), cols):
        gc.set(qn("w:w"), str(w))
    for r in tbl.rows:
        for tc, w in zip(r._tr.findall(qn("w:tc")), cols):
            tcw = tc.get_or_add_tcPr().find(qn("w:tcW"))
            if tcw is not None:
                tcw.set(qn("w:w"), str(w))
                tcw.set(qn("w:type"), "dxa")


def ap_dung(path, dich=None):
    doc = Document(path)
    path = dich or path
    body = doc.element.body
    log = []

    # ---- Trang bia (textbox, moi textbox co 2 ban Choice/Fallback)
    ten1, ten2 = THANH_VIEN[0][0], THANH_VIEN[1][0]
    for tx in body.iter(qn("w:txbxContent")):
        for p in tx.iter(qn("w:p")):
            if p.find(".//" + qn("w:p")) is not None:
                continue
            t = text_of(p).strip()
            if t.startswith("SV/ Nhóm SV:"):
                dat_text(p, f"SV/ Nhóm SV:  1. {ten1} (Nhóm trưởng)")
                log.append("bia-SV1")
            elif t.startswith("2. ") and len(t) < 40:
                dat_text(p, f"2. {ten2}")
                log.append("bia-SV2")
            elif t.startswith("GVHD:"):
                dat_text(p, f"GVHD:  {GVHD_VIET_TAT} {GVHD_TEN}")
                log.append("bia-GVHD")
            elif t.startswith("Thành phố Hồ Chí Minh, ngày"):
                dat_text(p, f"Thành phố Hồ Chí Minh, ngày {NGAY_BIA} tháng 9 năm 2026")
                log.append("bia-ngay")

    # ---- Than bai: muc 6 va muc 8
    dem_ten = 0
    for p in body.iterchildren(qn("w:p")):
        t = text_of(p).strip()
        if t.startswith("Họ và tên:") and "Học vị" in t:
            dat_text(p, f"Họ và tên: {GVHD_TEN}" + " " * 36 + f"Học vị: {GVHD_HOC_VI}")
            log.append("muc6-ten")
        elif t.startswith("Địa chỉ nhà riêng:"):
            dat_text(p, f"Địa chỉ nhà riêng: {GVHD_DIA_CHI}")
            log.append("muc6-diachi")
        elif t.startswith("Di động:"):
            dat_text(p, f"Di động: {GVHD_DI_DONG}" + " " * 20 + f"E-mail: {GVHD_EMAIL}")
            log.append("muc6-lienhe")
        elif "do sinh viên chủ nhiệm đề tài xây dựng" in t:
            for x in p.iter(qn("w:t")):
                if x.text and "do sinh viên chủ nhiệm đề tài xây dựng" in x.text:
                    x.text = x.text.replace("do sinh viên chủ nhiệm đề tài xây dựng",
                                            "do thành viên nhóm nghiên cứu xây dựng")
            log.append("muc8-SmartPort")
        elif t.startswith("Thông tin SV chịu trách nhiệm chính"):
            dat_text(p, f"Thông tin SV chịu trách nhiệm chính: {SV_CHINH_TEN}")
            log.append("muc7-SVchinh")
        elif t.startswith("Họ và tên:"):  # dong ho ten SV (dong GVHD co "Hoc vi" da xu ly o tren)
            dat_text(p, f"Họ và tên: {SV_CHINH_TEN}")
            dem_ten += 1
            log.append("muc7-hoten")
        elif t.startswith("SĐT:"):
            dat_text(p, f"SĐT: {SV_CHINH_SDT}")
            log.append("muc7-sdt")
        elif t.startswith("Email:"):
            dat_text(p, f"Email: {SV_CHINH_EMAIL}")
            log.append("muc7-email")
        elif t.startswith("Kinh phí thực hiện (đồng):"):
            dat_text(p, f"Kinh phí thực hiện (đồng): {KINH_PHI_SO} đồng")
            log.append("muc14-so")
        elif t.startswith("Bằng chữ:"):
            dat_text(p, f"Bằng chữ: {KINH_PHI_CHU}")
            log.append("muc14-chu")

    # ---- Bang thanh vien (bang dau tien)
    tv = doc.tables[0]
    while len(tv.rows) > len(THANH_VIEN) + 1:
        tv._tbl.remove(tv.rows[-1]._tr)
    for i, (ten, mssv, lop, ghi_chu) in enumerate(THANH_VIEN, start=1):
        c = tv.rows[i].cells
        ghi_o(c[0], str(i), "c")
        ghi_o(c[1], ten)
        ghi_o(c[2], mssv, "c")
        ghi_o(c[3], lop, "c")
        ghi_o(c[4], ghi_chu, "c")
    dat_rong(tv, (600, 3000, 2000, 1720, 1750))  # cot ho ten du cho "Nguyen Phan Quoc Hung"
    log.append("bang-thanh-vien")

    # ---- Bang tien do: cot nguoi thuc hien
    for tb in doc.tables:
        dau = [text_of(tc) for tc in tb.rows[0]._tr.findall(qn("w:tc"))]
        if any("Người thực hiện" in x for x in dau):
            for r in tb.rows[1:]:
                ghi_o(r.cells[3], f"{ten1}\n{ten2}")
            dat_rong(tb, (1600, 2670, 1700, 3100))  # "Nguyen Phan Quoc Hung" khong bi ngat dong
            log.append("bang-tien-do")

    # ---- Bang ky: ten GVHD
    ky = doc.tables[-1]
    for t in ky._tbl.iter(qn("w:t")):
        if t.text and "Huỳnh Nguyễn Thành Luân" in t.text:
            t.text = t.text.replace("Huỳnh Nguyễn Thành Luân", GVHD_TEN)
            log.append("ky-GVHD")
        elif t.text and t.text.strip() == "Lê Hữu Anh Tài":
            t.text = SV_CHINH_TEN
            log.append("ky-SVchinh")

    for i in range(5):
        try:
            doc.save(path)
            break
        except PermissionError:
            if i == 4:
                raise SystemExit(f"File dang mo trong Word, dong lai roi chay lai: {path}")
            time.sleep(2)
    print(os.path.basename(path), "->", ", ".join(log))


if __name__ == "__main__":
    # python dong_bo_thanh_vien.py nguon.docx [dich.docx]   (khong co dich -> ghi de nguon)
    args = sys.argv[1:] or [os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")]
    ap_dung(args[0], args[1] if len(args) > 1 else None)
