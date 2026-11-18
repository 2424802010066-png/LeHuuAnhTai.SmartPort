# -*- coding: utf-8 -*-
"""
NGUONG "CHAC CHAN" CHO MA CONTAINER TRONG VIDEO.
  Anh le (cong chup 1 anh khi xe dung): nguong log P >= -12 (rat rong) — anh luon CO container nen it ma "ao".
  Video: phan lon khung la duong trong / dau keo / thung xe -> khung khong co ma that van co the "nan" ra 1 ma dung
  ISO 6346 voi do tin cay ~0 (gap thuc te: HENU6869046 0.000, GGUU6960576 0.000 tren video thu).
  -> Trong video, 1 khung chi duoc tinh la doc CHAC neu do_tin_cay (= P(ma | nguon tot nhat)) >= nguong chon tren VAL:
     giu >= 99% ma doc DUNG cua VAL. Ma xuat hien >= 2 khung dong y van duoc giu (bo phieu theo thoi gian).
Chay: python chon_nguong_video_container.py
"""
import json
import os
import pickle

import numpy as np

import cai_tien_container as ctc
import doc_ma_container as dmc

GOC = os.path.dirname(os.path.abspath(__file__))
GIU = 0.99


def do_tin_cay(bd, ten_tap, tap, ch, chu):
    ra = []
    for t in tap:
        b = bd[(ten_tap, t["id"])]
        ng = ctc.nguon_tu_bo_dem(b["vung"], ch["xoay"], ch["det"], b["kich_thuoc"] if ch["cat_mep"] else None)
        if not ng:
            continue
        ma, hl, _, ct = dmc.quyet_dinh(ng, chu, ch["thuong"], ch["nguong"], chi_tiet=True)
        if hl:
            ra.append((ma == t["nhan"], ct["do_tin_cay"]))
    return ra


if __name__ == "__main__":
    ch = json.load(open(os.path.join(GOC, "runs", "danh_gia", "container_toan_gop_chung_cu_test.json"), encoding="utf-8"))["cau_hinh"]
    bd = pickle.load(open(ctc.BO_DEM, "rb"))
    val = do_tin_cay(bd, "val", ctc.tap_val(), ch, ctc.chu_tu_nhan({"train"}))
    dung = np.array(sorted(tc for ok, tc in val if ok))
    sai = np.array(sorted(tc for ok, tc in val if not ok))
    nguong = float(np.floor(dung[int((1 - GIU) * len(dung))] * 1000) / 1000)
    print("cau hinh:", ch)
    print("VAL: %d ma duoc tin | dung %d: tc min %.4f, p1 %.4f, p5 %.4f, trung vi %.4f | sai %d: %s" % (
        len(val), len(dung), dung[0], np.percentile(dung, 1), np.percentile(dung, 5), np.median(dung), len(sai), np.round(sai, 4).tolist()))
    print("nguong video (giu >= %.0f%% ma dung VAL) = %.3f" % (100 * GIU, nguong))
    test = do_tin_cay(bd, "test", ctc.tap_test(), ch, ctc.chu_tu_nhan({"train", "val"}))
    giu_dung = sum(ok and tc >= nguong for ok, tc in test)
    giu_sai = sum((not ok) and tc >= nguong for ok, tc in test)
    print("TEST (1 lan): ma dung %d -> giu %d | ma sai van qua ISO %d -> con %d" % (
        sum(ok for ok, _ in test), giu_dung, sum(not ok for ok, _ in test), giu_sai))
    json.dump(dict(nguong=nguong, cach_chon="giu >= %.0f%% ma doc dung cua VAL" % (100 * GIU), cau_hinh=ch,
                   val=dict(dung=len(dung), sai=len(sai), tc_dung_min=float(dung[0]), tc_dung_trung_vi=float(np.median(dung))),
                   test=dict(dung=sum(ok for ok, _ in test), dung_giu=giu_dung, sai=sum(not ok for ok, _ in test), sai_con=giu_sai)),
              open(os.path.join(GOC, "runs", "danh_gia", "nguong_video_container.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
