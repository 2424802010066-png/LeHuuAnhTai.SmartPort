# -*- coding: utf-8 -*-
"""
HUAN LUYEN MO HINH DO BAO HO (PPE) — YOLO26s tinh chinh tu trong so COCO tren bo Construction-PPE (Ultralytics,
AGPL-3.0; 1.132 anh train / 143 val / 141 test; 11 lop). Lop dung cho canh bao an toan lao dong o cang:
  helmet (mu bao ho) · vest (ao phan quang) · none (THAN NGUOI KHONG mac ao phan quang) · no_helmet (dau tran) · Person
Chon trong so tot nhat theo tap VAL (Ultralytics best.pt), do tap TEST giu rieng DUNG 1 LAN -> runs/bao_ho/ket_qua.json.
Chay: python huan_luyen_bao_ho.py [ten imgsz batch]   vd: bao_ho_960 960 8   (YOLO_OFFLINE=1: khong tai gi)
"""
import json
import os
import sys
import time

os.environ.setdefault("YOLO_OFFLINE", "1")

GOC = os.path.dirname(os.path.abspath(__file__))
DL = os.environ.get("SMARTPORT_PPE_DIR", r"C:\Users\ASUS\Downloads\SmartPort_DuLieuThat\construction_ppe")
RUNS = os.path.join(GOC, "runs")
TEN = sys.argv[1] if len(sys.argv) > 1 else "bao_ho"
CO = int(sys.argv[2]) if len(sys.argv) > 2 else 640
LO = int(sys.argv[3]) if len(sys.argv) > 3 else 16
LOP = ["helmet", "gloves", "vest", "boots", "goggles", "none", "Person", "no_helmet", "no_goggle", "no_gloves", "no_boots"]


def main():
    from ultralytics import YOLO
    yaml = os.path.join(GOC, "du_lieu", TEN + ".yaml")             # moi lan chay 1 tep (khong ghi de khi doi bo du lieu)
    os.makedirs(os.path.dirname(yaml), exist_ok=True)
    with open(yaml, "w", encoding="utf-8") as f:
        f.write("path: %s\ntrain: images/train\nval: images/val\ntest: images/test\nnames:\n" % DL.replace("\\", "/"))
        for i, t in enumerate(LOP):
            f.write("  %d: %s\n" % (i, t))
    t0 = time.time()
    m = YOLO(os.path.join(GOC, "yolo26s.pt"))
    m.train(data=yaml, project=RUNS, name=TEN, exist_ok=True, imgsz=CO, epochs=120, patience=30, batch=LO,
            workers=4, device=0, seed=42, cos_lr=True, close_mosaic=15, verbose=False, plots=True)
    phut = round((time.time() - t0) / 60, 1)
    tot = os.path.join(RUNS, TEN, "weights", "best.pt")
    kq = YOLO(tot).val(data=yaml, split="test", imgsz=CO, device=0, project=RUNS, name=TEN + "_test", exist_ok=True,
                       verbose=False, plots=True)
    theo_lop = {}
    for i, c in enumerate(kq.box.ap_class_index):
        theo_lop[LOP[int(c)]] = dict(AP50=round(float(kq.box.ap50[i]), 4), AP50_95=round(float(kq.box.ap[i]), 4))
    sg = DL.rstrip("\\/").endswith("_suy_giam")
    ra = dict(bo_du_lieu="Construction-PPE (Ultralytics, AGPL-3.0) 1132/143/141" +
              (" + 1 ban SUY GIAM moi anh train/val (thu nho + mo + nen JPEG — tao_du_lieu_suy_giam.py)" if sg else ""),
              mo_hinh="YOLO26s tinh chinh tu COCO, anh %d" % CO,
              chon_theo="VAL (best.pt)", do_tren="TEST giu rieng, 1 lan", phut_huan_luyen=phut,
              mAP50=round(float(kq.box.map50), 4), mAP50_95=round(float(kq.box.map), 4),
              precision=round(float(kq.box.mp), 4), recall=round(float(kq.box.mr), 4), theo_lop=theo_lop, trong_so=tot)
    json.dump(ra, open(os.path.join(RUNS, TEN, "ket_qua.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(ra, ensure_ascii=False, indent=1), flush=True)


if __name__ == "__main__":
    main()
