import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// ---------------------------------------------------------------------------
// MOT UNG DUNG DUY NHAT KHI DEPLOY:
//   "npm run build" ghi ban build React vao SmartPort.Api/wwwroot (canh anh Uploads, mo hinh AI...),
//   API phuc vu luon giao dien -> len hosting chi can publish SmartPort.Api.
// KHI DEV:
//   Vite (cong 5173) chay giao dien, chuyen tiep /api, anh, mo hinh AI sang SmartPort.Api (cong 5200)
//   -> code React luon goi dia chi TUONG DOI ("/api/...") giong het luc deploy, khong vuong CORS.
// ---------------------------------------------------------------------------
const thuMucDuAn = path.dirname(fileURLToPath(import.meta.url))
const WWWROOT_API = path.resolve(thuMucDuAn, '../SmartPort.Api/wwwroot')
const API_DEV = process.env.SMARTPORT_API || 'http://localhost:5200'

/** Truoc khi build: chi xoa phan do Vite sinh lan truoc (index.html, assets/) — KHONG dung toi Uploads, mo hinh AI */
function donBanBuildCu() {
  return {
    name: 'don-ban-build-cu',
    apply: 'build',
    buildStart() {
      fs.rmSync(path.join(WWWROOT_API, 'assets'), { recursive: true, force: true })
      fs.rmSync(path.join(WWWROOT_API, 'index.html'), { force: true })
    },
  }
}

/**
 * VO UNG DUNG NGOAI TUYEN: sau khi build, ghi vao sw.js (ban trong outDir) danh sach MOI file Vite sinh ra
 * (index.html + chunk JS/CSS/font) va ma phien ban = bam cua danh sach do + index.html.
 *   - SW cai san toan bo vo luc install -> tram cong mat Wi-Fi van mo lai duoc moi man hinh (ke ca route lazy).
 *   - Moi ban build co ten cache rieng ('smartport-react-vo-<bam>') -> khong tron vo cu / chunk moi.
 *   - Noi dung sw.js doi theo ban build -> trinh duyet tu phat hien ban moi.
 * Tu viet (khong dung vite-plugin-pwa / workbox): khong tai them goi nao, bai chay offline.
 */
function voUngDungNgoaiTuyen() {
  return {
    name: 'sp-vo-ung-dung-ngoai-tuyen',
    apply: 'build',
    // writeBundle (KHONG phai generateBundle): luc nay bundle da chot — cac chunk chi chua CSS da bi Vite bo, file da nam
    // tren dia. Lay danh sach som hon se co chunk "ma" -> cache.addAll gap 404 -> SW khong cai duoc.
    writeBundle(tuyChon, bundle) {
      const thuMucRa = tuyChon.dir || WWWROOT_API
      const danhSach = Object.values(bundle)
        .map((f) => f.fileName)
        .filter((ten) => ten.startsWith('assets/') && !ten.endsWith('.map') && fs.existsSync(path.join(thuMucRa, ten)))
        .sort()
      const nguonSw = fs.readFileSync(path.join(thuMucDuAn, 'public', 'sw.js'), 'utf8')
      const html = fs.readFileSync(path.join(thuMucRa, 'index.html'), 'utf8')
      const phienBan = createHash('sha256')
        .update(JSON.stringify(danhSach) + '|' + html)
        .digest('hex')
        .slice(0, 12)
      const banDung = { phienBan, taiTruoc: ['/index.html', ...danhSach.map((t) => '/' + t)] }
      const mau = /^var BAN_DUNG = .*\/\/ @@SP_BAN_DUNG@@.*$/m
      if (!mau.test(nguonSw)) throw new Error('public/sw.js thiếu dòng "var BAN_DUNG = ... // @@SP_BAN_DUNG@@"')
      const moi = nguonSw.replace(mau, 'var BAN_DUNG = ' + JSON.stringify(banDung) + ' // @@SP_BAN_DUNG@@ (sinh lúc build)')
      fs.writeFileSync(path.join(thuMucRa, 'sw.js'), moi)
      this.info?.(`sw.js: vỏ ứng dụng ${banDung.taiTruoc.length} tệp, phiên bản ${phienBan}`)
    },
  }
}

const chuyenTiep = { target: API_DEV, changeOrigin: false }

// Giong may chu that (Program.cs): COOP + COEP -> trang "cross-origin isolated" -> ONNX Runtime Web chay WebAssembly
// da luong. Dev va deploy cung header thi loi (neu co) lo ngay luc lap trinh, khong doi toi luc len hosting.
const CO_LAP_NGUON = { 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'credentialless' }

export default defineConfig({
  plugins: [react(), donBanBuildCu(), voUngDungNgoaiTuyen()],
  server: {
    port: 5173,
    strictPort: true,
    headers: CO_LAP_NGUON,
    proxy: {
      '/api': chuyenTiep,
      '/Uploads': chuyenTiep,
      '/uploads': chuyenTiep,
      '/Content': chuyenTiep,
      '/Scripts': chuyenTiep,
      '/swagger': chuyenTiep,
      // Nhip suc khoe cua tram (src/ngoaiTuyen): dev cung goi dung may chu that, khong roi vao trang SPA cua Vite
      '/suc-khoe': chuyenTiep,
    },
  },
  build: {
    outDir: WWWROOT_API,
    emptyOutDir: false,
    chunkSizeWarningLimit: 1500,
  },
})
