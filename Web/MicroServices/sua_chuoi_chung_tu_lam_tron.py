# -*- coding: utf-8 -*-
"""
SUA CHUNG TU BI "GAY CHUOI" OAN do loi lam tron thoi gian (truoc ban sua ChungTuHelper.DenGiay, 08/10/2026).
  Loi: hash tinh voi NgayLap = 14:31:46.999 (C#), cot SQL datetime lam tron thanh 14:31:47.000 -> tinh lai hash tu CSDL
  khong khop -> KiemTraChuoi bao "bi pha" du chung tu KHONG bi sua.
  Sua: chi voi chung tu ma hash LUU SAN khop CHINH XAC khi lui NgayLap 1 giay (bang chung mat ma: day la loi lam tron,
  khong phai bi sua noi dung) -> dat NgayLap = giay da dung de tinh hash. KHONG doi hash, KHONG doi noi dung nao khac.
  Chung tu hash khong khop ca khi lui 1 giay -> de nguyen va bao ra (co the bi sua that).
Chay:  python sua_chuoi_chung_tu_lam_tron.py <ten CSDL>          (xem truoc, khong ghi)
       python sua_chuoi_chung_tu_lam_tron.py <ten CSDL> --ghi    (ghi sua)
"""
import datetime
import hashlib
import sys

import pyodbc

csdl = sys.argv[1]
ghi = "--ghi" in sys.argv
cn = pyodbc.connect(r"DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\MSSQLSERVER01;DATABASE=%s;Trusted_Connection=yes;" % csdl)
cur = cn.cursor()


def tinh_hash(r, ngay):
    du = "|".join([r.SoChungTu or "", r.LoaiCT or "", r.SoCont or "", r.BienSoXe or "", r.MoTa or "",
                   ngay.strftime("%Y%m%d%H%M%S"), r.HashTruoc or "GENESIS"])
    return hashlib.sha256(du.encode("utf-8")).hexdigest()


ds = cur.execute("SELECT MaCT, SoChungTu, LoaiCT, SoCont, BienSoXe, MoTa, NgayLap, HashTruoc, HashSHA256 "
                 "FROM dbo.ChungTu ORDER BY MaCT").fetchall()
sua, nghi = [], []
truoc = None
for r in ds:
    if r.HashSHA256 is None:
        truoc = None
        continue
    if truoc is not None and r.HashTruoc != truoc:
        nghi.append((r.MaCT, "HashTruoc không nối với chứng từ liền trước"))
    elif tinh_hash(r, r.NgayLap) != r.HashSHA256:
        lui = r.NgayLap.replace(microsecond=0) - datetime.timedelta(seconds=1)
        if tinh_hash(r, lui) == r.HashSHA256:
            sua.append((r.MaCT, r.SoChungTu, r.NgayLap, lui))
        else:
            nghi.append((r.MaCT, "hash không khớp kể cả khi lùi 1 giây"))
    truoc = r.HashSHA256
print("%s: %d chứng từ | gãy do làm tròn: %d | nghi bị sửa thật: %d" % (csdl, len(ds), len(sua), len(nghi)))
for ma, so, cu, moi in sua:
    print("  #%d %s: NgayLap %s -> %s" % (ma, so, cu, moi))
for ma, ly_do in nghi:
    print("  NGHI #%d: %s (để nguyên)" % (ma, ly_do))
if ghi and sua:
    for ma, _, _, moi in sua:
        cur.execute("UPDATE dbo.ChungTu SET NgayLap = ? WHERE MaCT = ?", moi, ma)
    cn.commit()
    print("đã sửa", len(sua), "chứng từ")
elif sua:
    print("(xem trước — thêm --ghi để sửa)")
