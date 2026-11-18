# -*- coding: utf-8 -*-
"""
Doi ban de cuong tu 15 muc ve 14 muc, theo dung quy uoc thay Luan dang ap dung.

Can cu: hai de cuong da duoc thay duyet (nhom Nguyen Tan Sang 2025-2026 va nhom
Tran Quoc Nghia) deu dat danh muc "Tai lieu tham khao:" o CUOI MUC 10 (Tong quan),
khong danh so rieng, nho vay Kinh phi la muc 14 thay vi 15.

Mau goc cua Vien ghi 14 = Tai lieu tham khao, 15 = Kinh phi, nhung thuc te thay
cham theo cach tren.

Viec can lam:
  1. Chuyen toan bo danh muc [1]..[12] va tieu de len truoc muc 11.
  2. Doi tieu de "14. Tai lieu tham khao" -> "Tai lieu tham khao:" (bo so).
  3. Doi "15. Kinh phi thuc hien de tai:" -> "14. Kinh phi thuc hien de tai:".
"""
import os
import re

from docx import Document
from docx.oxml.ns import qn

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

doc = Document(FILE)
ps = doc.paragraphs


def tim(dieu_kien):
    for i, p in enumerate(ps):
        if dieu_kien(p.text.strip()):
            return i
    return -1


i_tltk = tim(lambda t: t.startswith("14. Tài liệu tham khảo"))
i_kinhphi = tim(lambda t: t.startswith("15. Kinh phí"))
i_muc11 = tim(lambda t: t.startswith("11. Đối tượng"))

if min(i_tltk, i_kinhphi, i_muc11) < 0:
    raise SystemExit(f"Khong tim du moc: tltk={i_tltk} kinhphi={i_kinhphi} muc11={i_muc11}")

# Cac doan thuoc danh muc: tu tieu de TLTK den ngay truoc muc Kinh phi
khoi = [ps[k] for k in range(i_tltk, i_kinhphi)]
print(f"Tim thay {len(khoi)} doan thuoc danh muc tai lieu (tu [{i_tltk}] den [{i_kinhphi-1}])")

# 1. Doi tieu de: bo so thu tu
ts = [t for t in khoi[0]._p.iter(qn("w:t"))]
if ts:
    ts[0].text = "Tài liệu tham khảo:"
    ts[0].set(qn("xml:space"), "preserve")
    for t in ts[1:]:
        t.text = ""
print('Da doi tieu de -> "Tài liệu tham khảo:"')

# 2. Chuyen ca khoi len TRUOC muc 11 (tuc cuoi muc 10), giu nguyen thu tu
neo = ps[i_muc11]._p
for p in khoi:
    neo.addprevious(p._p)
print("Da chuyen danh muc len cuoi muc 10 (truoc muc 11)")

# 3. Doi so muc Kinh phi 15 -> 14
for p in doc.paragraphs:
    t = p.text.strip()
    if t.startswith("15. Kinh phí"):
        tt = [x for x in p._p.iter(qn("w:t"))]
        gop = "".join(x.text or "" for x in tt)
        tt[0].text = gop.replace("15. Kinh phí", "14. Kinh phí")
        tt[0].set(qn("xml:space"), "preserve")
        for x in tt[1:]:
            x.text = ""
        print("Da doi so muc: 15 -> 14 (Kinh phi)")
        break

def luu_an_toan(doc, dich, lan=5, cho=2.0):
    """Word/preview hay giu file .docx -> thu lai, cuoi cung ghi ra ban _v14."""
    import time
    for i in range(lan):
        try:
            doc.save(dich)
            return dich
        except PermissionError:
            if i == lan - 1:
                alt = dich.replace(".docx", "_14MUC.docx")
                doc.save(alt)
                print(f"\nCANH BAO: file dich dang mo trong Word -> da ghi ra {os.path.basename(alt)}")
                return alt
            time.sleep(cho)


FILE = luu_an_toan(doc, FILE)

# Kiem tra lai
d2 = Document(FILE)
muc = [p.text.strip() for p in d2.paragraphs
       if re.match(r"^\d{1,2}\.\s*[A-ZĐÂĂÊÔƠƯÁÀẢÃẠ]", p.text.strip())]
print("\n=== CAC MUC SAU KHI SUA ===")
for m in muc:
    print("  ", m[:70])
print(f"\nTong so muc danh so: {len(muc)}")
