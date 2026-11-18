# -*- coding: utf-8 -*-
"""DANH GIA DOC QR CCCD QUA DUNG LOGIC TRINH DUYET (khong can build, khong dung may chu SmartPort dang chay).

May chu tinh nho phuc vu: /src/* (ma nguon worktree), /Scripts/* va /Content/* (wwwroot worktree), /tn/* (bo test).
Playwright mo trang, import that src/khu/tai-xe/giayTo.js roi goi docQrTuAnh / docQrTuVideo tren tung anh.

Chay: python danh_gia.py <thu_muc_test> <nhan_ket_qua> [gioi_han_anh]
Ra:   <thu_muc_test>/ket_qua_<nhan>.json + in bang theo kieu suy giam va co module.
"""
import collections, functools, json, os, sys, threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from playwright.sync_api import sync_playwright

W = Path(r"C:\Users\ASUS\Downloads\SmartPort\.claude\worktrees\port-management-logistics-system-a66379\Web")
REACT = W / "smartport-web"
WWW = W / "SmartPort.Api" / "wwwroot"
TEST = Path(sys.argv[1])
NHAN = sys.argv[2]
GIOI_HAN = int(sys.argv[3]) if len(sys.argv) > 3 else 0
CONG = 8931 + (hash(NHAN) & 255)

TRANG = "<!doctype html><meta charset=utf-8><title>dg</title><body><canvas id=c></canvas>"


class XuLy(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript", ".onnx": "application/octet-stream", ".mjs": "text/javascript"}

    def log_message(self, *a):
        pass

    def translate_path(self, p):
        p = p.split("?")[0].split("#")[0]
        if p.startswith("/tn/"):
            return str(TEST / p[4:])
        if p.endswith("giayTo.js") and os.environ.get("SmartPort_BASE"):
            return os.environ["SmartPort_BASE"]
        if p.startswith("/src/"):
            f = REACT / p[1:]
            if not f.exists() and (REACT / (p[1:] + ".js")).exists():
                f = REACT / (p[1:] + ".js")
            return str(f)
        if os.environ.get("KHONG_KP") and p.endswith("qr_khoi_phuc.onnx"):
            return str(WWW / "khong_co_file.onnx")
        return str(WWW / p[1:])

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "credentialless")
        super().end_headers()

    def do_GET(self):
        if self.path.split("?")[0] == "/h.html":
            b = TRANG.encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(b)))
            self.end_headers()
            self.wfile.write(b)
            return
        super().do_GET()


JS = r"""
async ({tep, soLuot}) => {
  const m = window.__m
  const img = new Image()
  img.src = '/tn/anh/' + tep
  await img.decode()
  const c = document.getElementById('c')
  // Che do ANH: nhu nut "Chon anh"
  let t0 = performance.now()
  let anh = null
  try { anh = await m.docQrTuAnh(img, c) } catch (e) { anh = 'LOI:' + e.message }
  const msAnh = performance.now() - t0
  // Che do VIDEO: cung khung hinh, chay soLuot luot lien tiep nhu vong quet camera (dung o luot dau doc duoc)
  img.videoWidth = img.naturalWidth; img.videoHeight = img.naturalHeight
  let vid = null, luotDoc = -1
  t0 = performance.now()
  let daChay = 0
  for (let l = 0; l < soLuot; l++) {
    daChay++
    try { vid = await m.docQrTuVideo(img, c, l) } catch (e) { vid = 'LOI:' + e.message }
    if (vid) { luotDoc = l; break }
  }
  const msVid = (performance.now() - t0) / daChay
  return { anh, msAnh, vid, msVid, luotDoc }
}
"""


def bang(kq):
    gop = collections.defaultdict(lambda: dict(n=0, a_dung=0, a_sai=0, v_dung=0, v_sai=0, ma=0.0, mv=0.0))
    for r in kq:
        for k in ((r["kieu"], "*"), ("*", r["px"]), (r["kieu"], r["px"])):
            g = gop[k]
            g["n"] += 1
            g["a_dung"] += r["anh_ok"]
            g["a_sai"] += r["anh_sai"]
            g["v_dung"] += r["vid_ok"]
            g["v_sai"] += r["vid_sai"]
            g["ma"] += r["msAnh"]
            g["mv"] += r["msVid"]
    ket = {}
    for k, g in gop.items():
        n = g["n"]
        ket[f"{k[0]}@{k[1]}"] = dict(n=n, anh_dung=round(100 * g["a_dung"] / n, 1), anh_sai=g["a_sai"], video_dung=round(100 * g["v_dung"] / n, 1), video_sai=g["v_sai"], ms_anh=round(g["ma"] / n, 1), ms_video=round(g["mv"] / n, 1))
    return ket


def main():
    meta = json.loads((TEST / "meta.json").read_text(encoding="utf-8"))
    if GIOI_HAN:
        buoc = max(1, len(meta) // GIOI_HAN)
        meta = meta[::buoc]
    sv = ThreadingHTTPServer(("127.0.0.1", CONG), functools.partial(XuLy))
    threading.Thread(target=sv.serve_forever, daemon=True).start()
    kq = []
    with sync_playwright() as p:
        b = p.chromium.launch(args=["--disable-gpu-sandbox"])
        pg = b.new_page()
        loi = []
        pg.on("pageerror", lambda e: loi.append(str(e)[:200]))
        pg.goto(f"http://127.0.0.1:{CONG}/h.html")
        pg.evaluate("async () => { window.__m = await import('/src/khu/tai-xe/giayTo.js'); const b = await window.__m.taiBoDocQr(); if (window.__m.chuanBiDinhVi) { try { await window.__m.chuanBiDinhVi() } catch (e) { console.log('dinh vi loi', e.message) } } window.__boDoc = b.ten }")
        print("bo doc:", pg.evaluate("window.__boDoc"), flush=True)
        so_luot = int(pg.evaluate("window.__m.SO_LUOT_VIDEO || 2"))
        print("so luot video:", so_luot, flush=True)
        khoi_tao = "async () => { window.__m = await import('/src/khu/tai-xe/giayTo.js'); const b = await window.__m.taiBoDocQr(); if (window.__m.chuanBiDinhVi) { try { await window.__m.chuanBiDinhVi() } catch (e) { console.log('dinh vi loi', e.message) } } window.__boDoc = b.ten }"
        for i, r in enumerate(meta):
            if i and i % 120 == 0:
                pg.goto(f"http://127.0.0.1:{CONG}/h.html")
                pg.evaluate(khoi_tao)
            try:
                x = pg.evaluate(JS, dict(tep=r["tep"], soLuot=so_luot))
            except Exception as e:
                print("LOI anh", r["tep"], r["kieu"], r["px"], r["w"], r["h"], str(e)[:90], flush=True)
                pg.close()
                pg = b.new_page()
                pg.goto(f"http://127.0.0.1:{CONG}/h.html")
                pg.evaluate(khoi_tao)
                x = dict(anh=None, msAnh=0, vid=None, msVid=0, luotDoc=-1)
            dung = r["chuoi"]
            a, v = x["anh"], x["vid"]
            rec = dict(tep=r["tep"], kieu=r["kieu"], px=r["px"], msAnh=x["msAnh"], msVid=x["msVid"], luot=x["luotDoc"],
                       anh_ok=int(bool(dung) and a == dung), anh_sai=int(bool(a) and a != dung), vid_ok=int(bool(dung) and v == dung), vid_sai=int(bool(v) and v != dung))
            kq.append(rec)
            if i % 200 == 0:
                print(i, "/", len(meta), flush=True)
        b.close()
    sv.shutdown()
    if loi:
        print("loi trang:", loi[:3])
    t = bang([r for r in kq if r["kieu"] != "am_tinh"])
    am = [r for r in kq if r["kieu"] == "am_tinh"]
    t2 = dict(tong=t, am_tinh=dict(n=len(am), anh_sai=sum(r["anh_sai"] for r in am), video_sai=sum(r["vid_sai"] for r in am)), chi_tiet=kq)
    (TEST / f"ket_qua_{NHAN}.json").write_text(json.dumps(t2, ensure_ascii=False), encoding="utf-8")
    print("\nKIEU            |  n  | anh dung% sai | video dung% sai | ms anh | ms video/luot")
    for k in sorted(t):
        if k.endswith("@*") or k.startswith("*@"):
            g = t[k]
            print(f"{k:16}|{g['n']:5}|{g['anh_dung']:7.1f} {g['anh_sai']:3} |{g['video_dung']:7.1f} {g['video_sai']:3} |{g['ms_anh']:7.0f} |{g['ms_video']:7.0f}")
    print("am tinh:", t2["am_tinh"])


main()
