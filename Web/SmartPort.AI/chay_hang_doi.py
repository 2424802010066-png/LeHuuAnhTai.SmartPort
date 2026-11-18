# -*- coding: utf-8 -*-
"""HANG DOI HUAN LUYEN: chay lan luot cac bai, moi bai 1 tien trinh (giai phong VRAM sau moi bai).
Nhat ky tom tat: runs/hang_doi.log (moi dong: thoi gian | BAT_DAU/XONG/LOI | bai | ket qua). Log day du: runs/<bai>.log
Chay: python chay_hang_doi.py crnn_cont yolo_kytu yolo_bien ...   (bo qua bai da co ket qua)
  yolo_* / rfdetr_*  -> huan_luyen.py <bai>          (xong khi co runs/<bai>/ket_qua.json)
  crnn_cont          -> doc_ma_container.py train    (xong khi co runs/doc_ma_container/ket_qua_test_khung_that.json)"""
import datetime
import os
import subprocess
import sys

GOC = os.path.dirname(os.path.abspath(__file__))
RUNS = os.path.join(GOC, "runs")
PY = os.path.join(GOC, ".venv", "Scripts", "python.exe")
DAC_BIET = {
    "crnn_cont": ([os.path.join(GOC, "doc_ma_container.py"), "train"],
                  os.path.join(RUNS, "doc_ma_container", "ket_qua_test_khung_that.json")),
}


def ghi(dong):
    os.makedirs(RUNS, exist_ok=True)
    with open(os.path.join(RUNS, "hang_doi.log"), "a", encoding="utf-8") as f:
        f.write("%s | %s\n" % (datetime.datetime.now().strftime("%d/%m %H:%M:%S"), dong))
    print(dong, flush=True)


if __name__ == "__main__":
    env = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUNBUFFERED="1")
    for bai in sys.argv[1:]:
        lenh, xong = DAC_BIET.get(bai, ([os.path.join(GOC, "huan_luyen.py"), bai],
                                        os.path.join(RUNS, bai, "ket_qua.json")))
        if os.path.exists(xong):
            ghi("BO_QUA | %s | da co ket qua" % bai)
            continue
        ghi("BAT_DAU | %s" % bai)
        with open(os.path.join(RUNS, bai + ".log"), "w", encoding="utf-8") as log:
            r = subprocess.run([PY] + lenh, stdout=log, stderr=subprocess.STDOUT, env=env, cwd=GOC)
        kq = ""
        for l in open(os.path.join(RUNS, bai + ".log"), encoding="utf-8", errors="replace"):
            if l.startswith("KET_QUA"):
                kq = l.strip()[:400]
        ghi(("XONG | %s | %s" % (bai, kq)) if r.returncode == 0 else ("LOI | %s | ma thoat %d" % (bai, r.returncode)))
    ghi("HET_HANG_DOI")
