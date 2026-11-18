# -*- coding: utf-8 -*-
"""
HUAN LUYEN 1 BAI (goi: python huan_luyen.py <ten_bai> [--thu]) — chay boi chay_hang_doi.py, moi bai mot tien trinh.
  yolo_*   : Ultralytics YOLO26 (AGPL-3.0)  -> doi chung + ban nhe cho trinh duyet
  rfdetr_* : Roboflow RF-DETR (Apache 2.0)  -> mo hinh chinh, dung thuong mai duoc
Ket qua (mAP tren tap TEST giu rieng) ghi vao runs/<bai>/ket_qua.json.
"""
import json
import os
import sys
import time

GOC = os.path.dirname(os.path.abspath(__file__))
DL = os.path.join(GOC, "du_lieu")
RUNS = os.path.join(GOC, "runs")

BAI = {
    # ten: (khung, bo du lieu, tham so)
    "yolo_bien": ("yolo", "bien_det", dict(model="yolo26s.pt", imgsz=640, epochs=60, batch=16, patience=15)),
    "yolo_kytu": ("yolo", "ky_tu", dict(model="yolo26s.pt", imgsz=320, epochs=150, batch=32, patience=30,
                                        fliplr=0.0, mosaic=0.5)),
    "yolo_cont": ("yolo", "cont_det", dict(model="yolo26s.pt", imgsz=1024, epochs=80, batch=8, patience=20,
                                           fliplr=0.0)),
    # VRAM 8 GB: batch 16 lam tran bo nho sang RAM (Windows sysmem fallback, GPU 100% nhung chi 32 W, cuc cham)
    # -> batch nho + tich luy gradient, batch hieu dung van 16
    "rfdetr_bien": ("rfdetr", "bien_det", dict(lop="RFDETRSmall", epochs=20, batch_size=4, grad_accum_steps=4)),
    "rfdetr_kytu": ("rfdetr", "ky_tu", dict(lop="RFDETRSmall", epochs=80, batch_size=4, grad_accum_steps=4)),
    "rfdetr_cont": ("rfdetr", "cont_det", dict(lop="RFDETRMedium", epochs=40, batch_size=2, grad_accum_steps=8)),
}


def chay_yolo(ten, bo, ts, thu):
    from ultralytics import YOLO
    ts = dict(ts)
    m = YOLO(os.path.join(GOC, "trong_so", ts.pop("model")) if os.path.exists(
        os.path.join(GOC, "trong_so", ts["model"])) else ts.pop("model"))
    ts.pop("model", None)
    if thu:
        ts.update(epochs=1, fraction=0.03, patience=1)
    m.train(data=os.path.join(DL, bo, "yolo", "data.yaml"), project=RUNS, name=ten, exist_ok=True, workers=4,
            device=0, seed=42, verbose=False, plots=True, **ts)
    tot = os.path.join(RUNS, ten, "weights", "best.pt")
    kq = YOLO(tot).val(data=os.path.join(DL, bo, "yolo", "data.yaml"), split="test", imgsz=ts["imgsz"], device=0,
                       project=RUNS, name=ten + "_test", exist_ok=True, verbose=False)
    return dict(mAP50=round(float(kq.box.map50), 4), mAP50_95=round(float(kq.box.map), 4),
                precision=round(float(kq.box.mp), 4), recall=round(float(kq.box.mr), 4), trong_so=tot)


def chay_rfdetr(ten, bo, ts, thu):
    import rfdetr
    ts = dict(ts)
    lop = getattr(rfdetr, ts.pop("lop"))
    m = lop()
    if thu:
        ts.update(epochs=1)
    out = os.path.join(RUNS, ten)
    m.train(dataset_dir=os.path.join(DL, bo, "coco"), output_dir=out, run_test=True, early_stopping=True,
            early_stopping_patience=8, num_workers=2, seed=42, progress_bar=thu, tensorboard=False, **ts)
    # RF-DETR 1.11 ghi chi so vao metrics.csv: dong cuoi co test/* (tap TEST giu rieng), cac dong val/* moi epoch
    import csv
    test, val_tot = {}, {}
    for r in csv.DictReader(open(os.path.join(out, "metrics.csv"))):
        t = {k: round(float(v), 4) for k, v in r.items() if v and k.startswith("test/")}
        v = {k: round(float(x), 4) for k, x in r.items() if x and k.startswith("val/")}
        if t:
            test = t
        if v and v.get("val/mAP_50_95", -1) >= val_tot.get("val/mAP_50_95", -1):
            val_tot = dict(v, epoch=int(r["epoch"]))
    return dict(mAP50=test.get("test/mAP_50"), mAP50_95=test.get("test/mAP_50_95"),
                precision=test.get("test/precision"), recall=test.get("test/recall"), test=test, val_tot_nhat=val_tot,
                trong_so=os.path.join(out, "checkpoint_best_total.pth"))


if __name__ == "__main__":
    ten = sys.argv[1]
    thu = "--thu" in sys.argv
    khung, bo, ts = BAI[ten]
    t0 = time.time()
    kq = (chay_yolo if khung == "yolo" else chay_rfdetr)(ten + ("_thu" if thu else ""), bo, ts, thu)
    kq["phut"] = round((time.time() - t0) / 60, 1)
    os.makedirs(os.path.join(RUNS, ten + ("_thu" if thu else "")), exist_ok=True)
    json.dump(kq, open(os.path.join(RUNS, ten + ("_thu" if thu else ""), "ket_qua.json"), "w"), indent=1, default=str)
    print("KET_QUA", ten, json.dumps(kq, default=str)[:600], flush=True)
