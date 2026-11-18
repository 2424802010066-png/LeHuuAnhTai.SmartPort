# -*- coding: utf-8 -*-
"""Huan luyen YOLO26n dinh vi vung QR (1 lop), danh gia tren anh that giu rieng + bo the mo phong giu rieng, xuat ONNX."""
import json, shutil
from pathlib import Path
import cv2
import numpy as np
import torch
from ultralytics import YOLO

B = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd")
AI = Path(r"C:\Users\ASUS\Downloads\SmartPort\Web\SmartPort.AI")


def iou(a, b):
    x0, y0 = max(a[0], b[0]), max(a[1], b[1])
    x1, y1 = min(a[2], b[2]), min(a[3], b[3])
    i = max(0, x1 - x0) * max(0, y1 - y0)
    return i / ((a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - i + 1e-9)


def main():
    m = YOLO(str(AI / "weights" / "yolo26n.pt"))
    m.train(data=str(B / "yolo_qr" / "qr.yaml"), imgsz=640, epochs=30, batch=16, device=0, workers=6, patience=10, project=str(B / "runs"), name="qr_n", exist_ok=True,
            mosaic=0.5, close_mosaic=6, degrees=15, perspective=0.0005, scale=0.5, fliplr=0.5, flipud=0.1, hsv_h=0.02, hsv_s=0.5, hsv_v=0.5, mixup=0.0, plots=False, verbose=False, seed=7)
    tot = B / "runs" / "qr_n" / "weights" / "best.pt"
    m = YOLO(str(tot))
    r = m.val(data=str(B / "yolo_qr" / "qr.yaml"), split="test", imgsz=640, device=0, plots=False, verbose=False)
    tk = dict(test_that=dict(mAP50=float(r.box.map50), mAP50_95=float(r.box.map), P=float(r.box.mp), R=float(r.box.mr)))

    # Bo the mo phong giu rieng: recall IoU>=0.5 theo co module
    T = B / "test_giu_rieng"
    meta = [x for x in json.loads((T / "meta.json").read_text(encoding="utf-8"))]
    gop = {}
    am = 0
    for x in meta:
        pred = m.predict(str(T / "anh" / x["tep"]), imgsz=640, conf=0.25, device=0, verbose=False)[0]
        bx = pred.boxes.xyxy.cpu().numpy() if pred.boxes is not None else np.zeros((0, 4))
        if x["q"] is None:
            am += int(len(bx) > 0)
            continue
        q = np.array(x["q"])
        gt = (q[:, 0].min(), q[:, 1].min(), q[:, 0].max(), q[:, 1].max())
        tot_iou = max([iou(gt, b) for b in bx], default=0)
        for k in (f"px{x['px']}", f"{x['kieu']}", "tong"):
            g = gop.setdefault(k, [0, 0])
            g[0] += 1
            g[1] += int(tot_iou >= 0.5)
    tk["the_giu_rieng_recall"] = {k: round(100 * v[1] / v[0], 1) for k, v in sorted(gop.items())}
    tk["the_am_tinh_co_hop"] = am
    print(json.dumps(tk, ensure_ascii=False, indent=1))
    (B / "runs" / "qr_n" / "danh_gia.json").write_text(json.dumps(tk, ensure_ascii=False, indent=1), encoding="utf-8")

    # Xuat ONNX (end-to-end [1,300,6], khong can NMS o trinh duyet)
    f = YOLO(str(tot)).export(format="onnx", imgsz=640, opset=17, simplify=False, nms=False, device="cpu")
    import onnxruntime as ort
    s = ort.InferenceSession(str(f), providers=["CPUExecutionProvider"])
    out = s.run(None, {s.get_inputs()[0].name: np.zeros((1, 3, 640, 640), np.float32)})
    print("onnx:", f, [o.shape for o in out], round(Path(f).stat().st_size / 1e6, 2), "MB")
    D = Path(r"C:\Users\ASUS\Downloads\SmartPort\.claude\worktrees\port-management-logistics-system-a66379\Web\SmartPort.Api\wwwroot\Content\models\yolo26n_qr.onnx")
    shutil.copy(f, D)
    print("da chep:", D)


if __name__ == "__main__":
    torch.multiprocessing.freeze_support()
    main()
