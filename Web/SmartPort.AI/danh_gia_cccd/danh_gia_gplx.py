# -*- coding: utf-8 -*-
"""Danh gia OCR GPLX: trinh duyet chay Tesseract that (docChuBangLai cua giayTo.js) + goi API boc truong tren he thong dang chay (chi doc, khong ghi).
Khong in mat khau/token. Chay: python danh_gia_gplx.py <thu_muc>"""
import collections, functools, json, sys, threading, urllib.request
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from playwright.sync_api import sync_playwright

W = Path(r"C:\Users\ASUS\Downloads\SmartPort\.claude\worktrees\port-management-logistics-system-a66379\Web")
REACT = W / "smartport-web"
WWW = W / "SmartPort.Api" / "wwwroot"
TEST = Path(sys.argv[1])
CONG = 8855
TK = json.loads(Path(r"C:\Users\ASUS\Downloads\SmartPort\Web\MicroServices\.bimat\tai-khoan-kiem-thu.json").read_text(encoding="utf-8-sig"))


class XuLy(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript", ".mjs": "text/javascript", ".gz": "application/octet-stream"}

    def log_message(self, *a):
        pass

    def translate_path(self, p):
        p = p.split("?")[0]
        if p.startswith("/tn/"):
            return str(TEST / p[4:])
        if p.startswith("/src/"):
            f = REACT / p[1:]
            if not f.exists() and (REACT / (p[1:] + ".js")).exists():
                f = REACT / (p[1:] + ".js")
            return str(f)
        return str(WWW / p[1:])

    def do_GET(self):
        if self.path == "/h.html":
            b = b"<!doctype html><meta charset=utf-8><body>"
            self.send_response(200)
            self.send_header("Content-Type", "text/html")
            self.send_header("Content-Length", str(len(b)))
            self.end_headers()
            self.wfile.write(b)
            return
        super().do_GET()


than = json.dumps({"tenDangNhap": TK["tenDangNhap"], "matKhau": TK["matKhau"]}).encode()
rq = urllib.request.Request("http://127.0.0.1:5100/api/auth/dang-nhap", data=than, headers={"Content-Type": "application/json"}, method="POST")
phien = json.loads(urllib.request.urlopen(rq, timeout=30).read().decode())
tok = phien.get("token") or phien.get("accessToken") or phien.get("jwt")


def boc(van_ban):
    rq = urllib.request.Request("http://127.0.0.1:5100/api/ho-so-tai-xe/doc-gplx", data=json.dumps({"vanBan": van_ban}).encode(), headers={"Content-Type": "application/json", "Authorization": "Bearer " + str(tok)}, method="POST")
    try:
        return json.loads(urllib.request.urlopen(rq, timeout=30).read().decode())
    except Exception as e:
        return {"loi": type(e).__name__}


meta = json.loads((TEST / "meta.json").read_text(encoding="utf-8"))
sv = ThreadingHTTPServer(("127.0.0.1", CONG), functools.partial(XuLy))
threading.Thread(target=sv.serve_forever, daemon=True).start()
tk = collections.defaultdict(collections.Counter)
with sync_playwright() as p:
    b = p.chromium.launch()
    pg = b.new_page()
    pg.goto(f"http://127.0.0.1:{CONG}/h.html")
    pg.evaluate("async () => { window.__m = await import('/src/khu/tai-xe/giayTo.js') }")
    for i, m in enumerate(meta):
        t = pg.evaluate("async (u) => { const img = new Image(); img.src = u; await img.decode(); const t0 = performance.now(); const v = await window.__m.docChuBangLai(img); return {v, ms: performance.now() - t0} }", "/tn/anh/" + m["tep"])
        k = boc(t["v"])
        g = tk[m["muc"]]
        g["n"] += 1
        g["so"] += int(k.get("soGplx") == m["so"])
        g["so_sai"] += int(bool(k.get("soGplx")) and k.get("soGplx") != m["so"])
        g["hang"] += int(k.get("hang") == m["hang"])
        g["hang_sai"] += int(bool(k.get("hang")) and k.get("hang") != m["hang"])
        h = (k.get("hanDen") or "")[:10]
        dd, mm, yy = m["han"].split("/")
        dung_han = f"{yy}-{mm}-{dd}"
        g["han"] += int(h == dung_han)
        g["han_sai"] += int(bool(h) and h != dung_han)
        g["ca3"] += int(k.get("soGplx") == m["so"] and k.get("hang") == m["hang"] and h == dung_han)
        g["ms"] += t["ms"]
        if i % 25 == 0:
            print(i, "/", len(meta), flush=True)
    b.close()
sv.shutdown()
print("\nMUC        |  n | so% (sai) | hang% (sai) | han% (sai) | ca 3 dung% | ms")
for k, g in tk.items():
    n = g["n"]
    print(f"{k:11}|{n:3} |{100 * g['so'] / n:6.1f} ({g['so_sai']:2}) |{100 * g['hang'] / n:6.1f} ({g['hang_sai']:2}) |{100 * g['han'] / n:6.1f} ({g['han_sai']:2}) |{100 * g['ca3'] / n:6.1f} |{g['ms'] / n:7.0f}")
(TEST / "ket_qua.json").write_text(json.dumps(tk), encoding="utf-8")
