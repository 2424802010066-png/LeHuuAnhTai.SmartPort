# -*- coding: utf-8 -*-
"""Buoc A: doc chuoi dung (ground truth) cua anh THAT giu rieng (test_that) bang zxing no luc cao + vi tri + so module."""
import functools, json, threading
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from playwright.sync_api import sync_playwright

WWW = Path(r"C:\Users\ASUS\Downloads\SmartPort\.claude\worktrees\port-management-logistics-system-a66379\Web\SmartPort.Api\wwwroot")
ANH = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd\yolo_qr\images\test_that")
RA = Path(r"C:\Users\ASUS\AppData\Local\Temp\ccq\du_lieu_cccd\test_that_gt.json")
CONG = 8844


class XuLy(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript"}

    def log_message(self, *a):
        pass

    def translate_path(self, p):
        p = p.split("?")[0]
        if p.startswith("/a/"):
            return str(ANH / p[3:])
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
  for (const tl of [1, 2, 0.5]) {
    c.width = Math.round(W*tl); c.height = Math.round(H*tl)
    const x = c.getContext('2d', {willReadFrequently:true}); x.imageSmoothingQuality='high'; x.drawImage(img, 0, 0, c.width, c.height)
    const kq = await window.ZXingWASM.readBarcodes(x.getImageData(0,0,c.width,c.height), {formats:['QRCode'], tryHarder:true, tryInvert:true, tryRotate:true, tryDownscale:true, maxNumberOfSymbols:4})
    const r = kq.find(k => k.isValid && k.bytes && k.bytes.length > 0)
    if (r) {
      const p = r.position
      let s
      try { s = new TextDecoder('utf-8',{fatal:true}).decode(r.bytes) } catch { s = r.text }
      return {W, H, chuoi: s, ver: r.version, q: [p.topLeft,p.topRight,p.bottomRight,p.bottomLeft].map(q => [q.x/tl, q.y/tl])}
    }
  }
  return {W, H, chuoi: null}
}
"""
sv = ThreadingHTTPServer(("127.0.0.1", CONG), functools.partial(XuLy))
threading.Thread(target=sv.serve_forever, daemon=True).start()
kq = {}
with sync_playwright() as p:
    b = p.chromium.launch()
    pg = b.new_page()
    pg.goto(f"http://127.0.0.1:{CONG}/h.html")
    pg.add_script_tag(url="/Scripts/vendor/zxing/zxing-reader.js")
    pg.evaluate("async () => { await window.ZXingWASM.prepareZXingModule({overrides:{locateFile:(t,x)=>t.endsWith('.wasm')?'/Scripts/vendor/zxing/'+t:x+t}, fireImmediately:true}) }")
    for f in sorted(ANH.glob("*.jpg")):
        kq[f.name] = pg.evaluate(JS, "/a/" + f.name)
    b.close()
sv.shutdown()
RA.write_text(json.dumps(kq, ensure_ascii=False), encoding="utf-8")
print("doc duoc", sum(1 for v in kq.values() if v["chuoi"]), "/", len(kq), "| phien ban:", sorted({v.get("ver") for v in kq.values() if v["chuoi"]}, key=str))
