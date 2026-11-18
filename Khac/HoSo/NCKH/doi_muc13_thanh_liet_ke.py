# -*- coding: utf-8 -*-
"""
Doi muc 13 tu dang BANG sang dang LIET KE ("San pham dat duoc: ..."),
theo dung cach nhom Tran Quoc Nghia va nhom Nguyen Tan Sang da lam.

Mau goc cua Vien khong quy dinh cach trinh bay muc 13 (chi co tieu de roi de trong),
nen ca hai cach deu hop le. Tai chon dang liet ke cho giong cac nhom da duoc duyet.
"""
import os
import time

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

SAN_PHAM = [
    "Đặc tả thuật toán giải mã có ràng buộc số kiểm tra ISO 6346, gồm phát biểu hình thức, chứng minh "
    "tính đúng đắn, đánh giá độ phức tạp và mã nguồn cài đặt.",

    "Bộ dữ liệu kiểm thử ảnh mã container được phân tầng theo bốn mức điều kiện chụp, gán nhãn theo "
    "chuẩn ISO 6346, kèm tài liệu mô tả quy tắc gán nhãn cho phép tái lập kết quả.",

    "Bộ kết quả thực nghiệm đối chứng, báo cáo độ chính xác cấp ký tự và cấp chuỗi trên từng tầng điều "
    "kiện ảnh, có kiểm định ý nghĩa thống kê của chênh lệch.",

    "Một bài báo khoa học gửi hội nghị hoặc tạp chí chuyên ngành công nghệ thông tin, định hướng hội "
    "nghị quốc tế có chỉ mục.",

    "Một báo cáo tổng kết đề tài theo mẫu quy định của Trường.",
]

doc = Document(FILE)


def dat_text(p, text, bold=False, italic=False):
    for r in list(p.runs):
        r._element.getparent().remove(r._element)
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(12)
    run.bold = bold
    run.italic = italic
    run._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:eastAsia"), "Times New Roman")
    return p


# ---------------------------------------------------- 1. Doi tieu de phu
p_sp = p_ud = None
for p in doc.paragraphs:
    t = p.text.strip()
    if t.startswith("a) Sản phẩm"):
        p_sp = dat_text(p, "Sản phẩm đạt được:", bold=True, italic=True)
    elif t.startswith("b) Khả năng ứng dụng"):
        p_ud = dat_text(p, "Khả năng ứng dụng:", bold=True, italic=True)
print("1) Doi tieu de: 'San pham dat duoc:' va 'Kha nang ung dung:'")

if p_sp is None:
    raise SystemExit("Khong tim thay tieu de muc 13a")

# ---------------------------------------------------- 2. Xoa bang san pham
bang_sp = None
for tb in doc.tables:
    if tb.rows and "Tên sản phẩm" in tb.rows[0].cells[1].text:
        bang_sp = tb
        break
if bang_sp is not None:
    bang_sp._tbl.getparent().remove(bang_sp._tbl)
    print("2) Da xoa bang san pham")
else:
    print("2) Khong tim thay bang san pham (co the da xoa truoc do)")

# ---------------------------------------------------- 3. Chen danh sach gach dau dong
neo = p_sp
for muc in SAN_PHAM:
    new_p = neo._p.makeelement(qn("w:p"), {})
    neo._p.addnext(new_p)
    from docx.text.paragraph import Paragraph
    p = Paragraph(new_p, neo._parent)
    pf = p.paragraph_format
    pf.left_indent = Cm(1.0)
    pf.first_line_indent = Cm(-0.5)
    pf.right_indent = Cm(0)
    pf.line_spacing = 1.35
    pf.space_after = Pt(3)
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    run = p.add_run("- " + muc)
    run.font.name = "Times New Roman"
    run.font.size = Pt(12)
    run._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:eastAsia"), "Times New Roman")
    neo = p
print(f"3) Da chen {len(SAN_PHAM)} muc san pham dang gach dau dong")


def luu(doc, dich, lan=5, cho=2.0):
    for i in range(lan):
        try:
            doc.save(dich)
            return dich
        except PermissionError:
            if i == lan - 1:
                alt = dich.replace(".docx", "_MOI.docx")
                doc.save(alt)
                print(f"\nCANH BAO: file dang mo trong Word -> ghi ra {os.path.basename(alt)}")
                return alt
            time.sleep(cho)


out = luu(doc, FILE)
print("\nDA LUU:", out)

d2 = Document(out)
print("\n=== MUC 13 SAU KHI SUA ===")
in_13 = False
for p in d2.paragraphs:
    t = p.text.strip()
    if t.startswith("13. Sản phẩm"):
        in_13 = True
    elif t.startswith("14. Kinh phí"):
        break
    if in_13 and t:
        print("  ", t[:88])
print(f"\nSo bang con lai: {len(d2.tables)}")
