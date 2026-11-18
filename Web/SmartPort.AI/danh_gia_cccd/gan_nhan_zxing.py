# -*- coding: utf-8 -*-
"""Gan nhan khung QR cho anh that/tong hop KHONG co toa do bang zxing-wasm (chi lay anh giai ma duoc ma -> nhan chac chan dung).
Chay: python gan_nhan_zxing.py <thu_muc_goc_anh> <duong_ra.json> <mau_glob ...>"""
import base64, functools, json, sys, threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from playwright.sync_api import sync_playwright

WWW = Path(r"C:\Users\ASUS\Downloads\SmartPort\.claude\worktrees\port-management-logistics-system-a66379\Web\SmartPort.Api\wwwroot")
GOC = Path(sys.argv[1])
RA = Path(sys.argv[2])
MAU = sys.argv[3:]
CONG = 8877


class XuLy(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript"}

    def log_message(self, *a):
        pass

    def translate_path(self, p):
        p = p.split("?")[0]
        if p.startswith("/g/"):
            from urllib.parse import unquote
            return str(GOC / unquote(p[3:]))
        return str(WWW / p[1:])

    def do_GET(self):
        if self.path == "/h.html":
            b = b"<!doctype html><meta charset=utf-8><canvas id=c></canvas>"
            self.send_response(200)
            self.send_header("Content-Type", "text/html")
            self.send_header("Content-Length", str(len(b)))
            self.end_headers()
            self.wfile.write(b)
            return
        super().do_GET()


JS = r"""
async (u) => {
  const img = new Image(); img.src = u; await img.decode()
  const W = img.naturalWidth, H = img.naturalHeight
  const c = document.getElementById('c')
  const out = []
  for (const canh of [Math.min(Math.max(W,H), 3000), 1400, 800]) {
    const tl = Math.min(1, canh / Math.max(W, H))
    c.width = Math.round(W*tl); c.height = Math.round(H*tl)
    const x = c.getContext('2d', {willReadFrequently:true}); x.drawImage(img, 0, 0, c.width, c.height)
    const kq = await window.ZXingWASM.readBarcodes(x.getImageData(0,0,c.width,c.height), {formats:['QRCode'], tryHarder:true, tryInvert:true, tryRotate:true, tryDownscale:true, maxNumberOfSymbols:8})
    for (const r of kq) if (r.isValid) { const p = r.position; out.push([p.topLeft,p.topRight,p.bottomRight,p.bottomLeft].map(q => [q.x/tl, q.y/tl])) }
    if (out.length) break
  }
  return {W, H, qr: out}
}
"""

ds = []
for m in MAU:
    ds += sorted(GOC.glob(m))
sv = ThreadingHTTPServer(("127.0.0.1", CONG), functools.partial(XuLy))
threading.Thread(target=sv.serve_forever, daemon=True).start()
kq = {}
with sync_playwright() as p:
    b = p.chromium.launch()
    pg = b.new_page()
    pg.goto(f"http://127.0.0.1:{CONG}/h.html")
    pg.add_script_tag(url="/Scripts/vendor/zxing/zxing-reader.js")
    pg.evaluate("async () => { await window.ZXingWASM.prepareZXingModule({overrides:{locateFile:(t,x)=>t.endsWith('.wasm')?'/Scripts/vendor/zxing/'+t:x+t}, fireImmediately:true}) }")
    for i, f in enumerate(ds):
        ten = f.relative_to(GOC).as_posix()
        try:
            r = pg.evaluate(JS, "/g/" + ten)
        except Exception as e:
            print("loi", ten, str(e)[:80])
            continue
        kq[ten] = r
        if i % 100 == 0:
            print(i, "/", len(ds), flush=True)
    b.close()
sv.shutdown()
RA.write_text(json.dumps(kq), encoding="utf-8")
co = sum(1 for v in kq.values() if v["qr"])
print("co QR doc duoc:", co, "/", len(kq))
