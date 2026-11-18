# -*- coding: utf-8 -*-
"""
Rut thoi gian thuc hien tu 12 thang xuong 6 thang (10/2026 - 03/2027).

Ly do: de tai dang ky cho NAM HOC 2026-2027. Ban cu keo toi thang 9/2027 la
vuot ra ngoai nam hoc. Hai de cuong da duoc thay Luan duyet deu gon trong
6-7 thang (nhom Nguyen Tan Sang: 6 thang; nhom Tran Quoc Nghia: 7 thang).

Sua 3 cho:
  1. Muc 4: "12 thang" -> "06 thang"; moc "den thang 09 nam 2027" -> "03/2027".
  2. Bang tien do 12.2: nen 7 giai doan thanh 6, moi thang mot moc.
  3. Ra soat cac cho con nhac "12 thang" trong than bai.
"""
import os
import time

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Pt

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

TAI = "Lê Hữu Anh Tài"
NHOM = "Nhóm nghiên cứu\n(chủ trì: Lê Hữu Anh Tài)"

# 6 giai doan, moi thang mot moc
TIEN_DO = [
    ("10/2026",
     "Nghiên cứu tổng quan tài liệu; xác lập bộ chỉ số đánh giá và danh mục đường cơ sở đối chứng",
     "Báo cáo tổng quan; danh mục tài liệu tham khảo",
     TAI),
    ("11/2026",
     "Phát biểu hình thức bài toán giải mã có ràng buộc; thiết kế thuật toán quy hoạch động; chứng "
     "minh tính đúng đắn và đánh giá độ phức tạp",
     "Đặc tả thuật toán; chứng minh toán học",
     TAI),
    ("12/2026",
     "Thu thập, gán nhãn và phân tầng bộ dữ liệu kiểm thử",
     "Bộ dữ liệu kèm tài liệu mô tả quy tắc gán nhãn",
     NHOM),
    ("01/2027",
     "Cài đặt phương pháp đề xuất và các đường cơ sở; thực nghiệm đối chứng trên từng tầng điều kiện ảnh",
     "Mã nguồn thực nghiệm; bảng kết quả đối chứng",
     TAI),
    ("02/2027",
     "Định lượng giới hạn tại các vị trí chữ cái đồng dư; kiểm chứng cơ chế bổ sung; phân tích các "
     "trường hợp nhận dạng sai",
     "Báo cáo phân tích lỗi và giới hạn áp dụng",
     NHOM),
    ("03/2027",
     "Biên soạn bài báo khoa học và gửi hội nghị; hoàn thiện báo cáo tổng kết và hồ sơ nghiệm thu",
     "Bản thảo bài báo; báo cáo tổng kết; hồ sơ nghiệm thu",
     NHOM),
]

doc = Document(FILE)


def dat_text(p, text):
    ts = [t for t in p._p.iter(qn("w:t"))]
    if not ts:
        return False
    ts[0].text = text
    ts[0].set(qn("xml:space"), "preserve")
    for t in ts[1:]:
        t.text = ""
    return True


# ---------------------------------------------------------------- 1. Muc 4
n = 0
for p in doc.paragraphs:
    t = p.text.strip()
    if t.startswith("4. Thời gian thực hiện"):
        dat_text(p, "4. Thời gian thực hiện:  06 tháng")
        n += 1
    elif t.startswith("Từ tháng") and "2027" in t:
        dat_text(p, "            Từ tháng   10   năm  2026          đến  tháng  03     năm 2027")
        n += 1
print(f"1) Muc 4: da sua {n} dong -> 06 thang (10/2026 - 03/2027)")

# ------------------------------------------------- 2. Bang tien do 12.2
tbl = doc.tables[2]
so_hang_can = len(TIEN_DO) + 1          # 6 giai doan + 1 hang tieu de
while len(tbl.rows) > so_hang_can:      # xoa hang thua tu duoi len
    tbl._tbl.remove(tbl.rows[-1]._tr)
print(f"2) Bang tien do: con {len(tbl.rows)} hang (1 tieu de + {len(TIEN_DO)} giai doan)")


def ghi_o(cell, text, canh_giua=False):
    cell.text = ""
    p = cell.paragraphs[0]
    pf = p.paragraph_format
    pf.left_indent = pf.right_indent = pf.first_line_indent = None
    pf.line_spacing = 1.15
    pf.space_before = Pt(2)
    pf.space_after = Pt(2)
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER if canh_giua else WD_ALIGN_PARAGRAPH.LEFT
    for k, dong in enumerate(text.split("\n")):
        if k:
            p.add_run().add_break()
        r = p.add_run(dong)
        r.font.name = "Times New Roman"
        r.font.size = Pt(12)
        r._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:eastAsia"), "Times New Roman")


for i, (tg, nd, sp, nguoi) in enumerate(TIEN_DO, start=1):
    r = tbl.rows[i]
    ghi_o(r.cells[0], tg, canh_giua=True)
    ghi_o(r.cells[1], nd)
    ghi_o(r.cells[2], sp)
    ghi_o(r.cells[3], nguoi, canh_giua=True)
print("   da dien lai 6 giai doan")

# --------------------------------------- 3. Ra soat cho con nhac 12 thang
con = [p.text.strip()[:70] for p in doc.paragraphs if "12 tháng" in p.text]
print(f"3) Cho con nhac \"12 thang\" trong than bai: {len(con)}")
for c in con:
    print("     ", c)


def luu(doc, dich, lan=5, cho=2.0):
    for i in range(lan):
        try:
            doc.save(dich)
            return dich
        except PermissionError:
            if i == lan - 1:
                alt = dich.replace(".docx", "_6THANG.docx")
                doc.save(alt)
                print(f"\nCANH BAO: file dang mo trong Word -> ghi ra {os.path.basename(alt)}")
                return alt
            time.sleep(cho)


out = luu(doc, FILE)
print("\nDA LUU:", out)
