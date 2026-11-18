# -*- coding: utf-8 -*-
"""
Got giua thuat ngu trong ban de cuong (5 vi tri, theo gop y Tai gui 29/09/2026).

Can cu cho tung sua doi:
  1. "ri set" -> "an mon": Tu dien Hoang Phe chap nhan CA HAI cach viet "ri set" va
     "gi set", nhung "gi" moi dung tu nguyen (chat do kim loai tac dung voi khong khi
     am). Trong van ban khoa hoc dung thuat ngu "an mon" (corrosion) de tranh han
     tranh cai chinh ta.
  2-3. Dien dat thu tuc -> dien dat khoa hoc.
  4. Bo sung thuat ngu tieng Anh chuan cho "do dong thuan giua nhung nguoi gan nhan".
  5. Cot "Nguoi thuc hien": bo cach viet tat ten rieng.
"""
import os
import shutil

from docx import Document
from docx.oxml.ns import qn

HERE = os.path.dirname(os.path.abspath(__file__))
NGUON = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")
DICH = NGUON  # sua tai cho

SUA = [
    # (cu, moi)
    ("bề mặt vỏ container thường bị rỉ sét, trầy xước hoặc bám bụi",
     "bề mặt vỏ container thường bị ăn mòn, trầy xước hoặc bám bụi"),

    ("(biến dạng do gợn sóng, rỉ sét, ngược sáng, đồng dư chữ cái)",
     "(biến dạng do gợn sóng, ăn mòn bề mặt, ngược sáng, đồng dư chữ cái)"),

    ("trong điều kiện được đơn vị cho phép",
     "khi được sự chấp thuận của đơn vị quản lý"),

    ("máy tính cá nhân phổ thông không trang bị bộ xử lý đồ họa rời",
     "máy tính cá nhân phổ thông chỉ sử dụng bộ xử lý trung tâm và đồ họa tích hợp, "
     "không phụ thuộc vào bộ xử lý đồ họa rời"),

    ("kiểm tra chéo nhãn nhằm đánh giá mức độ đồng thuận giữa những người gán nhãn",
     "kiểm tra chéo nhãn nhằm đánh giá mức độ đồng thuận giữa những người gán nhãn "
     "(inter-annotator agreement)"),
]

SUA_O_BANG = {"Cả nhóm (Tài chủ trì)": "Nhóm nghiên cứu\n(chủ trì: Lê Hữu Anh Tài)"}

if NGUON != DICH:
    shutil.copyfile(NGUON, DICH)
doc = Document(NGUON)


def thay_trong_doan(p_el, cu, moi):
    ts = [t for t in p_el.iter(qn("w:t"))]
    if not ts:
        return False
    gop = "".join(t.text or "" for t in ts)
    if cu not in gop:
        return False
    ts[0].text = gop.replace(cu, moi)
    ts[0].set(qn("xml:space"), "preserve")
    for t in ts[1:]:
        t.text = ""
    return True


print("GOT GIUA THUAT NGU\n")
for cu, moi in SUA:
    n = sum(1 for p in doc.paragraphs if thay_trong_doan(p._p, cu, moi))
    trang_thai = "OK" if n else "KHONG THAY"
    print(f"  [{trang_thai}] {cu[:58]}...")

# Cot "Nguoi thuc hien": xuong dong mem cho gon trong o hep
for tb in doc.tables:
    for r in tb.rows:
        for c in r.cells:
            cu = c.text.strip()
            if cu in SUA_O_BANG:
                moi = SUA_O_BANG[cu]
                c.text = ""
                p = c.paragraphs[0]
                p.paragraph_format.space_after = None
                for k, dong in enumerate(moi.split("\n")):
                    if k:
                        p.add_run().add_break()
                    run = p.add_run(dong)
                    run.font.name = "Times New Roman"
                    run._element.get_or_add_rPr().get_or_add_rFonts().set(
                        qn("w:eastAsia"), "Times New Roman")
                from docx.enum.text import WD_ALIGN_PARAGRAPH
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER

doc.save(DICH)
print("\nDA LUU:", DICH)
