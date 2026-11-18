# -*- coding: utf-8 -*-
"""
Sua TRIET DE bo cuc bang trong ban de cuong.

NGUYEN NHAN GOC (tim ra 29/09/2026):
  Style "Normal" cua mau Vien co left_indent = 1,75 cm va first_line_indent = -1,0 cm
  (thut treo). Cac doan ben trong O BANG khong dat thut le rieng nen KE THUA nguyen
  1,75 cm thut trai. Hau qua:
    - Cot "TT" rong 1,59 cm bi thut 1,75 cm  -> chi con hien 1 ky tu ("TT" -> "T")
    - Cot "So luong" con 0,54 cm             -> chu vo doc tung ky tu
    - Cac cot rong thi dong dau lui ra, dong sau thut vao (thut treo)

CACH SUA:
  1. Ep left_indent = first_line_indent = right_indent = 0 cho MOI doan trong MOI o.
  2. Can le hop ly: hang tieu de can giua; cot so/TT can giua; cot van ban can trai
     (khong dung can deu vi o hep se gian chu, kho doc).
  3. Can giua theo chieu doc de o nhin can doi.
  4. Phan bo lai be rong cot cho vua noi dung, tong dung 9070 dxa = 16,00 cm.
"""
import os

from docx import Document
from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.shared import Cm, Pt

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

TONG = 9070  # 16,00 cm

# Be rong cot (dxa) va kieu can le tung cot: 'c' = giua, 'l' = trai
BANG = {
    0: ((700, 2500, 2300, 1770, 1800), ("c", "l", "c", "c", "c")),   # TT|Ho ten|MSSV|Lop|Ghi chu
    1: ((900, 3000, 5170), ("c", "l", "l")),                          # TT|Muc tieu|Tieu chi
    2: ((1800, 2900, 2370, 2000), ("c", "l", "l", "c")),              # Thoi gian|Noi dung|SP|Nguoi
    3: ((900, 2500, 4470, 1200), ("c", "l", "l", "c")),               # TT|Ten SP|Yeu cau|SL
    4: ((4200, 1015, 3855), ("c", "c", "c")),                         # bang ky
}

doc = Document(FILE)


def ep_le_doan(p, align):
    """Xoa thut le ke thua tu style Normal va dat can le."""
    pf = p.paragraph_format
    pf.left_indent = Cm(0)
    pf.right_indent = Cm(0)
    pf.first_line_indent = Cm(0)
    pf.line_spacing = 1.15
    pf.space_before = Pt(2)
    pf.space_after = Pt(2)
    pf.alignment = {"c": WD_ALIGN_PARAGRAPH.CENTER,
                    "l": WD_ALIGN_PARAGRAPH.LEFT}[align]


def dat_rong(tbl, cols):
    g = tbl._tbl.find(qn("w:tblGrid"))
    for gc, w in zip(g.findall(qn("w:gridCol")), cols):
        gc.set(qn("w:w"), str(w))
    for r in tbl.rows:
        for i, w in enumerate(cols):
            if i >= len(r.cells):
                break
            tcPr = r.cells[i]._tc.get_or_add_tcPr()
            tcW = tcPr.find(qn("w:tcW"))
            if tcW is None:
                tcW = tcPr.makeelement(qn("w:tcW"), {})
                tcPr.append(tcW)
            tcW.set(qn("w:w"), str(w))
            tcW.set(qn("w:type"), "dxa")
    tblPr = tbl._tbl.find(qn("w:tblPr"))
    tblW = tblPr.find(qn("w:tblW"))
    if tblW is None:
        tblW = tblPr.makeelement(qn("w:tblW"), {})
        tblPr.append(tblW)
    tblW.set(qn("w:w"), str(sum(cols)))
    tblW.set(qn("w:type"), "dxa")


print("SUA TRIET DE BO CUC BANG")
print("Nguyen nhan: style Normal cua mau co thut trai 1,75 cm, o bang ke thua.\n")

for bi, (cols, canle) in BANG.items():
    if bi >= len(doc.tables):
        continue
    tbl = doc.tables[bi]
    if len(cols) != len(tbl.columns):
        print(f"  BANG {bi}: bo qua (so cot lech)")
        continue

    dat_rong(tbl, cols)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    tbl.autofit = False

    so_doan = 0
    for ri, r in enumerate(tbl.rows):
        for ci, c in enumerate(r.cells):
            c.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            for p in c.paragraphs:
                # Hang tieu de luon can giua; cac hang sau theo cau hinh cot
                ep_le_doan(p, "c" if ri == 0 else canle[ci])
                so_doan += 1

    hep = min(cols) / 567
    print(f"  BANG {bi}: cot {cols} = {sum(cols)/567:.2f} cm"
          f" | cot hep nhat {hep:.2f} cm | da ep le {so_doan} doan")

doc.save(FILE)
print("\nDA LUU:", FILE)
