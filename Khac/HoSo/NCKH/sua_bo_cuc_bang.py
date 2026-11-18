# -*- coding: utf-8 -*-
"""
Sua bo cuc BANG trong ban de cuong Tai da tu chinh.

Ba loi tim thay trong file goc:
  1. 4/5 bang RONG HON vung in (16,00 cm = 9073 dxa). Bang rong 16,4-16,8 cm nen
     Word phai ep cot lai, lam chu vo xuong tung ky tu (vi du "nghiê m").
  2. Bang tien do bi ghim trHeight = 1433 twips (2,53 cm) o MOI hang, tao khoang
     trong thua trong o du noi dung ngan.
  3. Cot "San pham" chi 1915 dxa (3,38 cm), qua hep cho cum tu dai.

Script lam viec TRUC TIEP tren file cua Tai de khong mat cac chinh sua rieng.
"""
import os
import shutil

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn

NGUON = r"C:\Users\ASUS\Downloads\ThuyetMinhDeCuong_NCKH_HOANCHINH_LeHuuAnhTai_2424802010066.docx"
DICH = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "ThuyetMinhDeCuong_NCKH_FINAL_LeHuuAnhTai_2424802010066.docx")

# Vung in = 21 - 3 (le trai) - 2 (le phai) = 16 cm = 9073 dxa. Chua 9070 cho chac.
TONG = 9070

# Phan bo cot moi cho tung bang, tong = 9070
BO_CUC = {
    0: (620, 2600, 2200, 1750, 1900),   # TT | Ho ten | MSSV | Lop | Ghi chu
    1: (900, 2900, 5270),               # TT | Muc tieu | Tieu chi
    2: (2150, 2650, 2270, 2000),        # Thoi gian | Noi dung | San pham | Nguoi
    3: (800, 2400, 4570, 1300),         # TT | Ten SP | Yeu cau | So luong
    4: (4200, 1015, 3855),              # bang ky (giu nguyen ty le)
}

# Rut gon chuoi qua dai trong cot hep
RUT_GON = {
    "Cả nhóm, Lê Hữu Anh Tài chủ trì": "Cả nhóm (Tài chủ trì)",
    # Bo khoang trang quanh dau gach de moc thoi gian khong bi ngat giua nam
    "11/2026 – 01/2027": "11/2026–01/2027",
    "12/2026 – 02/2027": "12/2026–02/2027",
}

shutil.copyfile(NGUON, DICH)
doc = Document(DICH)


def dat_be_rong(tbl, cols):
    """Dat lai tblGrid, tcW cua moi o, va tblW tong."""
    g = tbl._tbl.find(qn("w:tblGrid"))
    gcs = g.findall(qn("w:gridCol"))
    for gc, w in zip(gcs, cols):
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


def go_chieu_cao_ghim(tbl, giu_hang_dau=True):
    """Bo trHeight o cac hang du lieu de hang tu co theo noi dung."""
    n = 0
    for i, r in enumerate(tbl.rows):
        if giu_hang_dau and i == 0:
            continue
        trPr = r._tr.find(qn("w:trPr"))
        if trPr is None:
            continue
        for h in trPr.findall(qn("w:trHeight")):
            trPr.remove(h)
            n += 1
    return n


def rut_gon_chu(tbl):
    n = 0
    for r in tbl.rows:
        for c in r.cells:
            cu = c.text.strip()
            if cu in RUT_GON:
                ts = [t for t in c._tc.iter(qn("w:t"))]
                if ts:
                    ts[0].text = RUT_GON[cu]
                    for t in ts[1:]:
                        t.text = ""
                    n += 1
    return n


print("Vung in: 16,00 cm = 9073 dxa. Dat tong moi bang = %d dxa.\n" % TONG)
for i, tbl in enumerate(doc.tables):
    cols = BO_CUC.get(i)
    if not cols:
        continue
    if len(cols) != len(tbl.columns):
        print(f"  BANG {i}: bo qua (so cot lech: mau {len(cols)} / thuc te {len(tbl.columns)})")
        continue
    dat_be_rong(tbl, cols)
    bo = go_chieu_cao_ghim(tbl)
    gon = rut_gon_chu(tbl)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    tbl.autofit = False
    print(f"  BANG {i}: cot -> {cols}  (tong {sum(cols)} = {sum(cols)/567:.2f} cm)"
          f" | bo {bo} chieu cao ghim | rut gon {gon} o")

doc.save(DICH)
print("\nDA LUU:", DICH)
