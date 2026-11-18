# -*- coding: utf-8 -*-
"""
Viet lai muc 13 theo cau truc de cuong nhom Tran Quoc Nghia:
  - "San pham dat duoc:"  -> gach dau dong, san pham KY THUAT cu the truoc.
  - "Kha nang ung dung:"  -> gach dau dong CO NHAN (Nhan: giai thich), thay vi
    mot doan van dai.
  - Bo sung muc "Gioi han ap dung" o cuoi, hoc theo muc "Gioi han su dung" cua
    nhom Nghia. Neu ro ranh gioi cua phuong phap la diem cong khi bao ve.
"""
import os
import time

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt
from docx.text.paragraph import Paragraph

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

SAN_PHAM = [
    "Thuật toán giải mã có ràng buộc số kiểm tra ISO 6346, được phát biểu hình thức, chứng minh tính "
    "đúng đắn và đánh giá độ phức tạp tính toán.",

    "Mã nguồn cài đặt thuật toán, tích hợp được vào pipeline nhận dạng hai tầng chạy trên trình duyệt.",

    "Bộ dữ liệu kiểm thử ảnh mã container được phân tầng theo bốn mức điều kiện chụp, gán nhãn theo "
    "chuẩn ISO 6346, kèm tài liệu mô tả quy tắc gán nhãn cho phép tái lập kết quả.",

    "Bộ kết quả thực nghiệm đối chứng giữa phương pháp đề xuất và các đường cơ sở, có kiểm định ý "
    "nghĩa thống kê của chênh lệch.",

    "Báo cáo định lượng giới hạn của ràng buộc số kiểm tra tại bốn vị trí chữ cái đồng dư modulo 11.",

    "Một bài báo khoa học gửi hội nghị hoặc tạp chí chuyên ngành công nghệ thông tin.",

    "Một báo cáo tổng kết đề tài theo mẫu quy định của Trường.",
]

# (nhan, noi dung) — nhan se duoc in dam
UNG_DUNG = [
    ("Ứng dụng tại cổng cảng cạn và depot",
     "Nâng cao độ chính xác ghi nhận mã container tại cổng, góp phần rút ngắn thời gian xử lý và giảm "
     "sai sót trong đối chiếu chứng từ."),

    ("Triển khai trên hạ tầng sẵn có",
     "Do phương pháp chỉ can thiệp vào khâu giải mã, chi phí tính toán bổ sung không đáng kể, cho phép "
     "triển khai trên thiết bị hiện có mà không yêu cầu hạ tầng máy chủ chuyên dụng."),

    ("Khả năng mở rộng",
     "Cách tiếp cận đưa ràng buộc đại số vào quá trình giải mã có thể áp dụng cho các bài toán nhận "
     "dạng chuỗi định danh khác có cơ chế số kiểm tra, chẳng hạn mã số sách tiêu chuẩn quốc tế hoặc mã "
     "số hàng hóa."),

    ("Phục vụ đào tạo",
     "Kết quả đề tài là học liệu minh họa cho các học phần Trí tuệ nhân tạo, Học máy và Thị giác máy "
     "tính tại Viện Công nghệ số."),

    ("Giới hạn áp dụng",
     "Phương pháp chỉ khôi phục được ký tự khi mô hình xác định được vị trí nghi ngờ thông qua độ tin "
     "cậy thấp; trường hợp sai sót mà không xác định được vị trí thì số kiểm tra chỉ phát hiện chứ "
     "không hiệu chỉnh được. Phương pháp cũng không áp dụng cho biển số phương tiện, do quy cách biển "
     "số Việt Nam không có cơ chế số kiểm tra."),
]

doc = Document(FILE)


def doan_moi(neo, gach=True):
    el = neo._p.makeelement(qn("w:p"), {})
    neo._p.addnext(el)
    p = Paragraph(el, neo._parent)
    pf = p.paragraph_format
    pf.left_indent = Cm(1.0) if gach else Cm(0.5)
    pf.first_line_indent = Cm(-0.5) if gach else Cm(0)
    pf.right_indent = Cm(0)
    pf.line_spacing = 1.35
    pf.space_after = Pt(3)
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    return p


def them_run(p, text, bold=False):
    r = p.add_run(text)
    r.font.name = "Times New Roman"
    r.font.size = Pt(12)
    r.bold = bold
    r._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:eastAsia"), "Times New Roman")


# ------------------------------------------------ 1. Xac dinh vung muc 13
i_sp = i_ud = i_muc14 = -1
for i, p in enumerate(doc.paragraphs):
    t = p.text.strip()
    if t.startswith("Sản phẩm đạt được"):
        i_sp = i
    elif t.startswith("Khả năng ứng dụng"):
        i_ud = i
    elif t.startswith("14. Kinh phí"):
        i_muc14 = i
        break
if min(i_sp, i_ud, i_muc14) < 0:
    raise SystemExit(f"Khong tim du moc: sp={i_sp} ud={i_ud} muc14={i_muc14}")

# Xoa toan bo noi dung cu giua hai tieu de va sau tieu de ung dung
for k in range(i_muc14 - 1, i_ud, -1):
    doc.paragraphs[k]._p.getparent().remove(doc.paragraphs[k]._p)
for k in range(i_ud - 1, i_sp, -1):
    doc.paragraphs[k]._p.getparent().remove(doc.paragraphs[k]._p)
print("1) Da xoa noi dung cu cua muc 13")

# ------------------------------------------------ 2. Chen lai san pham
p_sp = doc.paragraphs[i_sp]
neo = p_sp
for muc in SAN_PHAM:
    p = doan_moi(neo)
    them_run(p, "- " + muc)
    neo = p
print(f"2) Da chen {len(SAN_PHAM)} san pham")

# ------------------------------------------------ 3. Chen lai kha nang ung dung
p_ud = None
for p in doc.paragraphs:
    if p.text.strip().startswith("Khả năng ứng dụng"):
        p_ud = p
        break
neo = p_ud
for nhan, noi_dung in UNG_DUNG:
    p = doan_moi(neo)
    them_run(p, "- " + nhan + ": ", bold=True)
    them_run(p, noi_dung)
    neo = p
print(f"3) Da chen {len(UNG_DUNG)} muc ung dung (co nhan in dam)")


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
print("\n=== MUC 13 SAU KHI VIET LAI ===")
inside = False
for p in d2.paragraphs:
    t = p.text.strip()
    if t.startswith("13. Sản phẩm"):
        inside = True
    elif t.startswith("14. Kinh phí"):
        break
    if inside and t:
        print("  ", t[:92])
