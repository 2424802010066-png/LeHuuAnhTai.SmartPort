// CHAY THU AI CUA SMARTPORT TREN DU LIEU THAT (chi dung de danh gia, khong thuoc ban build).
// Trang danh-gia-du-lieu-that/chay.html goi DUNG ham AI cua ung dung (src/ai/alpr.js, src/ai/ocrContainer.js),
// doc anh tu thu muc du lieu ngoai repo va ghi ket qua ra dia qua POST /__luu.
// Chay: npx vite --config vite.danhgia.config.js  ->  http://localhost:5199/chay.html?bo=bien_so_cat
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'

const THU_MUC = path.dirname(fileURLToPath(import.meta.url))
const DU_LIEU = process.env.SMARTPORT_DU_LIEU || 'C:/Users/ASUS/Downloads/SmartPort_DuLieuThat'

function luuKetQua() {
  return {
    name: 'luu-ket-qua',
    configureServer(server) {
      server.middlewares.use('/__luu', (req, res) => {
        let than = ''
        req.on('data', (c) => (than += c))
        req.on('end', () => {
          const { ten, du_lieu: duLieu } = JSON.parse(than)
          if (!/^ket_qua_[a-z0-9_]+\.json$/.test(ten)) {
            res.statusCode = 400
            return res.end('ten khong hop le')
          }
          fs.writeFileSync(path.join(DU_LIEU, ten), JSON.stringify(duLieu))
          res.end('ok')
        })
      })
    },
  }
}

export default defineConfig({
  root: path.join(THU_MUC, 'danh-gia-du-lieu-that'),
  publicDir: path.resolve(THU_MUC, '../SmartPort.Api/wwwroot'), // mo hinh AI (/Content/models) + thu vien (/Scripts/vendor)
  plugins: [luuKetQua()],
  server: {
    port: 5199,
    strictPort: true,
    // giong may chu that: cross-origin isolated -> ONNX Runtime Web chay WebAssembly da luong
    headers: { 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'credentialless' },
    fs: { allow: [THU_MUC, DU_LIEU] },
  },
})
