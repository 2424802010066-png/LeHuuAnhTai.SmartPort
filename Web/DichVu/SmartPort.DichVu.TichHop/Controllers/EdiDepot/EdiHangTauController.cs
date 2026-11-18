using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.EdiDepot
{
    /// <summary>
    /// EDI hãng tàu (thay màn hình MVC Admin/Edi): sinh CODECO/COARRI gửi hãng tàu, xem, tải tệp .edi,
    /// đánh dấu đã gửi, nhận COPARN -> xem trước -> tự lập lệnh giao nhận.
    /// </summary>
    [Route("api/edi")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class EdiHangTauController : ApiGocController
    {
        /// <summary>Tep COPARN tai len toi da 2 MB — ban tin EDI that hiem khi lon vay</summary>
        private const long TEP_TOI_DA = 2 * 1024 * 1024;

        private static readonly string[] LOAI_LENH_HOP_LE = { "GiaoCont", "HaCont", "TraRong", "LayRong" };

        private readonly AppDb db;

        public EdiHangTauController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Ma dinh danh depot trong ban tin — doi o appsettings.json: AppSettings:MaDepotEDI</summary>
        private static string MaDepot
        {
            get
            {
                var s = CauHinh.AppSettings["MaDepotEDI"];
                return string.IsNullOrWhiteSpace(s) ? "SMARTPORT" : s;
            }
        }

        // ============================================================
        // DANH SACH + LUA CHON
        // ============================================================

        /// <summary>Lịch sử bản tin EDI (lọc theo loại, tìm số chứng từ / tham chiếu / hãng tàu) kèm số bản tin theo loại và số lượt qua cổng chưa báo cáo.</summary>
        [HttpGet]
        public ActionResult<TongQuanEdiDto> DanhSach([FromQuery] string loai, [FromQuery] string q, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.BanTinEDIs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(loai)) truyVan = truyVan.Where(b => b.LoaiBT == loai);
            var k = (q ?? "").Trim();
            if (k.Length > 0)
                truyVan = truyVan.Where(b => b.SoChungTu.Contains(k) || b.SoThamChieu.Contains(k)
                                             || (b.HangTau != null && b.HangTau.TenHangTau.Contains(k)));

            // Dem theo loai bang mot cau GROUP BY thay vi 3 cau COUNT
            var dem = db.BanTinEDIs.AsNoTracking()
                .GroupBy(b => b.LoaiBT)
                .Select(g => new { Loai = g.Key, So = g.Count() })
                .ToList();
            int SoTheoLoai(string l) => dem.Where(x => x.Loai == l).Select(x => x.So).FirstOrDefault();

            return new TongQuanEdiDto
            {
                Loai = loai,
                DanhSach = truyVan
                    .OrderByDescending(b => b.MaBT)
                    .Select(b => new BanTinEdiDto
                    {
                        MaBT = b.MaBT,
                        LoaiBT = b.LoaiBT,
                        Huong = b.Huong,
                        MaHangTau = b.MaHangTau,
                        TenHangTau = b.HangTau != null ? b.HangTau.TenHangTau : null,
                        SoThamChieu = b.SoThamChieu,
                        SoChungTu = b.SoChungTu,
                        TuNgay = b.TuNgay,
                        DenNgay = b.DenNgay,
                        SoLuongCont = b.SoLuongCont,
                        TrangThai = b.TrangThai,
                        GhiChu = b.GhiChu,
                        NgayTao = b.NgayTao,
                        NguoiTao = b.NguoiTao,
                        NgayGui = b.NgayGui
                    })
                    .LayTrang(trang, coTrang),
                SoCodeco = SoTheoLoai("CODECO"),
                SoCoparn = SoTheoLoai("COPARN"),
                SoCoarri = SoTheoLoai("COARRI"),
                // Con bao nhieu luot chua bao cao — con so nay nhac phai sinh CODECO
                ChuaBaoCao = db.XeRaVaos.Count(x => x.MaBT == null && x.SoCont != null),
                MaDepot = MaDepot
            };
        }

        /// <summary>Danh sách chọn: hãng tàu (sinh CODECO, nhận COPARN) và 40 chuyến tàu gần nhất đã gán container (sinh COARRI).</summary>
        [HttpGet("lua-chon")]
        public ActionResult<LuaChonEdiDto> LuaChon()
        {
            var hangTaus = db.HangTaus.AsNoTracking()
                .OrderBy(h => h.TenHangTau)
                .Select(h => new { h.MaHangTau, h.TenHangTau })
                .ToList()
                .Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau))
                .ToList();

            // Chuyen tau co container gan vao — chuyen trong thi sinh COARRI cung vo nghia
            var chuyen = db.ChuyenTaus.AsNoTracking()
                .Where(c => db.Containers.Any(x => x.MaChuyenNhap == c.MaChuyen || x.MaChuyenXuat == c.MaChuyen))
                .OrderByDescending(c => c.ETA)
                .Take(40)
                .Select(c => new
                {
                    c.MaChuyen,
                    c.MaChuyenHieu,
                    TenTau = c.Tau != null ? c.Tau.TenTau : null,
                    c.ETA,
                    SoNhap = db.Containers.Count(x => x.MaChuyenNhap == c.MaChuyen),
                    SoXuat = db.Containers.Count(x => x.MaChuyenXuat == c.MaChuyen)
                })
                .ToList()
                .Select(c => new ChuyenCoarriDto
                {
                    MaChuyen = c.MaChuyen,
                    Nhan = c.MaChuyenHieu + " — " + (c.TenTau ?? "?") + " (" + c.ETA.ToString("dd/MM") + ")",
                    SoNhap = c.SoNhap,
                    SoXuat = c.SoXuat
                })
                .ToList();

            return new LuaChonEdiDto { HangTaus = hangTaus, ChuyenTaus = chuyen };
        }

        // ============================================================
        // SINH CODECO / COARRI
        // ============================================================

        /// <summary>
        /// Sinh CODECO cho một hãng tàu: gom mọi lượt container vào/ra CHƯA báo cáo trong N ngày (1–90),
        /// lưu bản tin "Chờ gửi", đánh dấu các lượt đã báo để lần sau không lấy lại, ghi sổ chứng từ.
        /// </summary>
        [HttpPost("codeco")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult SinhCodeco([FromBody] SinhCodecoDto yc)
        {
            var hang = db.HangTaus.Find(yc.MaHangTau);
            if (hang == null) return KhongThay("Không tìm thấy hãng tàu.");

            var soNgay = yc.SoNgay;
            if (soNgay < 1) soNgay = 1;
            if (soNgay > 90) soNgay = 90;
            var tu = DateTime.Now.AddDays(-soNgay);
            var maHangTau = hang.MaHangTau;

            if (!db.Containers.Any(c => c.MaHangTau == maHangTau))
                return Loi("Chưa có container nào gán cho hãng " + hang.TenHangTau + ". Vào Tồn rỗng theo hãng để gán trước.");

            // Loc luot theo hang tau bang doi chieu container -> MaHangTau (EXISTS trong SQL,
            // khong keo ca danh sach so container cua hang ve roi gui nguoc thanh tham so)
            var luot = db.XeRaVaos
                .Where(x => x.MaBT == null && x.SoCont != null && x.ThoiGian >= tu
                            && db.Containers.Any(c => c.SoCont == x.SoCont && c.MaHangTau == maHangTau))
                .OrderBy(x => x.ThoiGian)
                .Take(500)
                .ToList();

            if (luot.Count == 0)
                return Loi("Không có lượt vào/ra nào của hãng " + hang.TenHangTau + " trong " + soNgay
                           + " ngày qua cần báo cáo (hoặc đã báo hết rồi).");

            var bt = CodecoHelper.Lap(db, hang, luot, MaDepot, NguoiDung, null);
            GhiNhatKy("Sinh CODECO " + bt.SoChungTu + " cho " + hang.TenHangTau + " (" + bt.SoLuongCont + " cont)");
            return XongViec("Đã sinh CODECO " + bt.SoChungTu + " — " + bt.SoLuongCont + " container.", bt.MaBT);
        }

        /// <summary>Sinh COARRI cho một chuyến tàu: container đã dỡ khỏi tàu (chiều "Do") hoặc sẽ xếp lên tàu (chiều "Xep").</summary>
        [HttpPost("coarri")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult SinhCoarri([FromBody] SinhCoarriDto yc)
        {
            var ct = db.ChuyenTaus.AsNoTracking()
                .Include(c => c.Tau).ThenInclude(t => t.HangTau)
                .FirstOrDefault(c => c.MaChuyen == yc.MaChuyen);
            if (ct == null) return KhongThay("Không tìm thấy chuyến tàu.");

            var laDoHang = yc.Chieu != "Xep";
            var conts = laDoHang
                ? db.Containers.AsNoTracking().Where(c => c.MaChuyenNhap == yc.MaChuyen).ToList()
                : db.Containers.AsNoTracking().Where(c => c.MaChuyenXuat == yc.MaChuyen).ToList();

            if (conts.Count == 0)
                return Loi("Chuyến " + ct.MaChuyenHieu + " chưa có container nào được gán "
                           + (laDoHang ? "chuyến nhập" : "chuyến xuất") + ".");

            var hang = ct.Tau != null && ct.Tau.HangTau != null ? ct.Tau.HangTau : null;
            var maHang = hang != null && !string.IsNullOrWhiteSpace(hang.MaSCAC) ? hang.MaSCAC
                       : (hang != null ? hang.TenHangTau : "UNKNOWN");

            var dong = conts.Select(c => new EdiService.DongCoarri
            {
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                TrongLuongTan = c.TrongLuong,
                ViTriBai = c.ViTriBai
            }).ToList();

            var thoiDiemTau = laDoHang ? (ct.ATA ?? ct.ETA) : (ct.ATD ?? ct.ETD);
            var kq = EdiService.SinhCoarri(MaDepot, maHang,
                ct.Tau != null ? ct.Tau.TenTau : "UNKNOWN",
                ct.Tau != null ? ct.Tau.SoIMO : null,
                ct.MaChuyenHieu, MaDepot, laDoHang, dong, thoiDiemTau);

            var bt = new BanTinEDI
            {
                LoaiBT = "COARRI",
                Huong = "Gửi",
                MaHangTau = hang != null ? (int?)hang.MaHangTau : null,
                SoThamChieu = kq.SoThamChieu,
                SoChungTu = kq.SoChungTu,
                TuNgay = thoiDiemTau,
                DenNgay = thoiDiemTau,
                SoLuongCont = kq.SoLuongCont,
                NoiDung = kq.NoiDung,
                TrangThai = "Chờ gửi",
                NgayTao = DateTime.Now,
                NguoiTao = Cat(NguoiDung, 50),
                GhiChu = Cat((laDoHang ? "Dỡ hàng" : "Xếp hàng") + " chuyến " + ct.MaChuyenHieu
                             + " · tàu " + (ct.Tau != null ? ct.Tau.TenTau : "?") + " · " + kq.SoDoan + " đoạn", 500)
            };
            db.BanTinEDIs.Add(bt);
            db.SaveChanges();

            GhiNhatKy("Sinh COARRI " + bt.SoChungTu + " — " + (laDoHang ? "dỡ" : "xếp") + " chuyến "
                      + ct.MaChuyenHieu + " (" + kq.SoLuongCont + " cont)");

            return XongViec("Đã sinh COARRI " + bt.SoChungTu + " — " + kq.SoLuongCont + " container "
                            + (laDoHang ? "dỡ khỏi tàu" : "xếp lên tàu") + ".", bt.MaBT);
        }

        // ============================================================
        // XEM / TAI / DANH DAU DA GUI / XOA
        // ============================================================

        /// <summary>Chi tiết một bản tin: thông tin, nội dung EDIFACT gốc và giải thích từng đoạn bằng tiếng Việt.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ChiTietBanTinEdiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ChiTietBanTinEdiDto> ChiTiet(int id)
        {
            var bt = db.BanTinEDIs.AsNoTracking().Include(b => b.HangTau).FirstOrDefault(b => b.MaBT == id);
            if (bt == null) return KhongThay("Không tìm thấy bản tin.");

            var giai = GiaiThichDoan(bt.NoiDung);
            return new ChiTietBanTinEdiDto
            {
                MaBT = bt.MaBT,
                LoaiBT = bt.LoaiBT,
                Huong = bt.Huong,
                MaHangTau = bt.MaHangTau,
                TenHangTau = bt.HangTau != null ? bt.HangTau.TenHangTau : null,
                MaSCAC = bt.HangTau != null ? bt.HangTau.MaSCAC : null,
                SoThamChieu = bt.SoThamChieu,
                SoChungTu = bt.SoChungTu,
                TuNgay = bt.TuNgay,
                DenNgay = bt.DenNgay,
                SoLuongCont = bt.SoLuongCont,
                TrangThai = bt.TrangThai,
                GhiChu = bt.GhiChu,
                NgayTao = bt.NgayTao,
                NguoiTao = bt.NguoiTao,
                NgayGui = bt.NgayGui,
                NoiDung = bt.NoiDung,
                TenTep = TenTepAnToan(bt.TenTep),
                SoDoan = giai.Count,
                GiaiThich = giai
            };
        }

        /// <summary>Tải tệp .edi (UTF-8 không BOM) để gửi cho hãng tàu.</summary>
        [HttpGet("{id:int}/tai")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Tai(int id)
        {
            var bt = db.BanTinEDIs.AsNoTracking().FirstOrDefault(b => b.MaBT == id);
            if (bt == null) return KhongThay("Không tìm thấy bản tin.");
            // EDIFACT bo UNOA la ASCII — ghi khong BOM, hang tau doc bang phan mem cu
            var byt = new UTF8Encoding(false).GetBytes(bt.NoiDung ?? "");
            return File(byt, "application/edifact", TenTepAnToan(bt.TenTep));
        }

        /// <summary>Đánh dấu bản tin gửi đi đã được gửi cho hãng tàu.</summary>
        [HttpPost("{id:int}/da-gui")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DanhDauDaGui(int id)
        {
            var bt = db.BanTinEDIs.Find(id);
            if (bt == null) return KhongThay("Không tìm thấy bản tin.");
            if (bt.Huong != "Gửi") return Loi("Bản tin " + bt.SoChungTu + " là bản tin nhận từ hãng tàu — không đánh dấu gửi.");
            if (bt.TrangThai == "Đã gửi") return Loi("Bản tin " + bt.SoChungTu + " đã đánh dấu gửi rồi.");

            bt.TrangThai = "Đã gửi";
            bt.NgayGui = DateTime.Now;
            db.SaveChanges();
            GhiNhatKy("Đánh dấu đã gửi EDI " + bt.SoChungTu);
            return XongViec("Đã đánh dấu gửi bản tin " + bt.SoChungTu + ".", bt.MaBT);
        }

        /// <summary>Xóa bản tin — các lượt qua cổng trong bản tin quay lại trạng thái CHƯA báo cáo.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var bt = db.BanTinEDIs.Find(id);
            if (bt == null) return KhongThay("Không tìm thấy bản tin.");

            // Tra lai cac luot xe ve trang thai CHUA BAO CAO, neu khong chung se bi ket vinh vien:
            // khong thuoc ban tin nao ma cung khong bao gio duoc gom lai.
            var luot = db.XeRaVaos.Where(x => x.MaBT == bt.MaBT).ToList();
            CodecoHelper.GanLuot(db, luot, null, bt.SoChungTu);     // luot xe thuoc dich vu Cong: gui su kien

            db.BanTinEDIs.Remove(bt);
            db.SaveChanges();
            GhiNhatKy("Xóa bản tin EDI " + bt.SoChungTu);
            return XongViec("Đã xóa bản tin. " + luot.Count + " lượt qua cổng được trả về trạng thái chưa báo cáo.", id);
        }

        // ============================================================
        // NHAN COPARN
        // ============================================================

        /// <summary>
        /// Đọc COPARN (dán nội dung hoặc tải tệp .edi ≤ 2MB) rồi trả BẢN XEM TRƯỚC — chưa tạo lệnh nào.
        /// Mỗi dòng có kết quả: tao (sẽ tạo lệnh) / boQua (đã có lệnh đang mở, trùng trong bản tin) / loi (sai định dạng).
        /// </summary>
        [HttpPost("coparn/xem-truoc")]
        [ProducesResponseType(typeof(XemTruocCoparnDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<XemTruocCoparnDto> XemTruocCoparn([FromForm] DocCoparnDto yc)
        {
            var noiDung = yc.NoiDung;
            if (yc.Tep != null && yc.Tep.Length > 0)
            {
                if (yc.Tep.Length > TEP_TOI_DA)
                    return Loi("Tệp quá 2MB — bản tin EDI thật hiếm khi lớn vậy, kiểm tra lại tệp.");
                using (var sr = new StreamReader(yc.Tep.OpenReadStream())) noiDung = sr.ReadToEnd();
            }

            var kq = EdiService.DocCoparn(noiDung);
            if (!kq.Ok) return Loi(kq.Loi);
            if (yc.MaHangTau != null && !db.HangTaus.Any(h => h.MaHangTau == yc.MaHangTau))
                return KhongThay("Không tìm thấy hãng tàu.");

            var loaiLenh = ChuanLoaiLenh(yc.LoaiLenh);
            var dong = PhanLoaiDong(kq);
            return new XemTruocCoparnDto
            {
                LoaiBanTin = kq.LoaiBanTin,
                SoChungTu = kq.SoChungTu,
                BenGui = kq.BenGui,
                BenNhan = kq.BenNhan,
                SoThamChieu = kq.SoThamChieu,
                NoiDung = noiDung,
                MaHangTau = yc.MaHangTau,
                LoaiLenh = loaiLenh,
                TenLoaiLenh = new LenhGiaoNhan { LoaiLenh = loaiLenh }.TenLoaiLenh,
                SoHopLe = dong.Count(d => d.KetQua == KQ_TAO),
                SoBoQua = dong.Count(d => d.KetQua == KQ_BO_QUA),
                SoLoi = dong.Count(d => d.KetQua == KQ_LOI),
                Dong = dong
            };
        }

        /// <summary>
        /// Tạo lệnh giao nhận từ các dòng COPARN hợp lệ (đọc lại nội dung, kiểm tra trùng như bước xem trước).
        /// Container chưa có trong hệ thống được khai báo ở trạng thái "Chờ về". Không tạo lượt xe.
        /// </summary>
        [HttpPost("coparn")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult TaoLenhTuCoparn([FromBody] TaoLenhCoparnDto yc)
        {
            var kq = EdiService.DocCoparn(yc.NoiDung);
            if (!kq.Ok) return Loi(kq.Loi);
            if (yc.MaHangTau != null && !db.HangTaus.Any(h => h.MaHangTau == yc.MaHangTau))
                return KhongThay("Không tìm thấy hãng tàu.");

            var loaiLenh = ChuanLoaiLenh(yc.LoaiLenh);
            var phanLoai = PhanLoaiDong(kq);

            var bt = new BanTinEDI
            {
                LoaiBT = string.IsNullOrEmpty(kq.LoaiBanTin) ? "COPARN" : Cat(kq.LoaiBanTin, 10),
                Huong = "Nhận",
                MaHangTau = yc.MaHangTau,
                SoThamChieu = Cat(kq.SoThamChieu, 30),
                SoChungTu = Cat(kq.SoChungTu, 30),
                SoLuongCont = kq.Dong.Count,
                NoiDung = yc.NoiDung,
                TrangThai = "Đã xử lý",
                NgayTao = DateTime.Now,
                NguoiTao = Cat(NguoiDung, 50)
            };

            int taoMoi = 0, boQua = 0, loi = 0;
            using (var gd = db.Database.BeginTransaction())
            {
                db.BanTinEDIs.Add(bt);
                db.SaveChanges();

                var soContTao = phanLoai.Where(p => p.KetQua == KQ_TAO).Select(p => p.SoCont).Distinct().ToList();
                var contDaCo = db.Containers.Where(c => soContTao.Contains(c.SoCont))
                    .ToList()
                    .GroupBy(c => c.SoCont, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < kq.Dong.Count; i++)
                {
                    var d = kq.Dong[i];
                    var ketQua = phanLoai[i].KetQua;
                    if (ketQua == KQ_LOI) { loi++; continue; }
                    if (ketQua == KQ_BO_QUA) { boQua++; continue; }

                    // Cont chua co trong he thong -> khai bao luon, vi hang tau da bao la no SE den;
                    // khong co thi luc xe toi cong khong khop lenh
                    if (!contDaCo.TryGetValue(d.SoCont, out var cont))
                    {
                        cont = new Container
                        {
                            SoCont = d.SoCont,
                            LoaiCont = d.LoaiContNoiBo,
                            TinhTrang = d.Rong ? "Empty" : "Full",
                            TrangThai = "Chờ về",
                            MaHangTau = yc.MaHangTau
                        };
                        db.Containers.Add(cont);
                        contDaCo[d.SoCont] = cont;
                    }
                    else if (cont.MaHangTau == null && yc.MaHangTau != null)
                    {
                        cont.MaHangTau = yc.MaHangTau;
                    }

                    var henTu = d.HenLuc ?? DateTime.Now;
                    db.LenhGiaoNhans.Add(new LenhGiaoNhan
                    {
                        SoLenh = "LGN-" + DateTime.Now.ToString("yyMM") + "-E" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant(),
                        LoaiLenh = loaiLenh,
                        SoCont = d.SoCont,
                        BienSoXe = string.IsNullOrWhiteSpace(d.BienSoXe) ? "CHUA-GAN" : Cat(d.BienSoXe, 15),
                        HenTuLuc = henTu,
                        HenDenLuc = henTu.AddHours(8),
                        MaPin = Random.Shared.Next(100000, 999999).ToString(),
                        TrangThai = "Chờ xe",
                        GhiChu = Cat("Tạo tự động từ " + bt.LoaiBT + " " + (bt.SoChungTu ?? "")
                                     + (string.IsNullOrEmpty(d.SoBooking) ? "" : " · booking " + d.SoBooking), 300),
                        NgayTao = DateTime.Now
                    });
                    taoMoi++;
                }

                bt.GhiChu = "Tạo mới " + taoMoi + " lệnh · bỏ qua " + boQua + " (đã có lệnh mở) · lỗi " + loi;
                db.SaveChanges();
                gd.Commit();
            }

            GhiNhatKy("Nhận " + bt.LoaiBT + " " + (bt.SoChungTu ?? "") + " → tạo " + taoMoi + " lệnh giao nhận");
            return XongViec("Đã tạo " + taoMoi + " lệnh giao nhận từ bản tin. "
                            + (boQua > 0 ? "Bỏ qua " + boQua + " container đã có lệnh mở. " : "")
                            + (loi > 0 ? loi + " dòng lỗi bị bỏ." : ""), bt.MaBT,
                            new { taoMoi, boQua, loi });
        }

        // ============================================================
        // TIEN ICH NOI BO
        // ============================================================

        private const string KQ_TAO = "tao";
        private const string KQ_BO_QUA = "boQua";
        private const string KQ_LOI = "loi";

        private static string ChuanLoaiLenh(string loaiLenh) =>
            !string.IsNullOrEmpty(loaiLenh) && LOAI_LENH_HOP_LE.Contains(loaiLenh) ? loaiLenh : "LayRong";

        /// <summary>
        /// Phan loai tung dong COPARN — dung chung cho XEM TRUOC va TAO LENH de hai buoc ra cung mot ket qua:
        ///   loi   : sai dinh dang (EdiService da danh dau)
        ///   boQua : container da co lenh dang mo ("Chờ xe"/"Đã vào cổng"), hoac lap lai trong chinh ban tin
        ///   tao   : se tao lenh
        /// </summary>
        private List<DongCoparnXemDto> PhanLoaiDong(EdiService.KetQuaDoc kq)
        {
            var soConts = kq.Dong.Where(d => string.IsNullOrEmpty(d.Loi)).Select(d => d.SoCont).Distinct().ToList();
            var daCoLenh = new HashSet<string>(
                db.LenhGiaoNhans.AsNoTracking()
                    .Where(l => soConts.Contains(l.SoCont) && (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"))
                    .Select(l => l.SoCont)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
            var daGap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var ds = new List<DongCoparnXemDto>();
            foreach (var d in kq.Dong)
            {
                var dong = new DongCoparnXemDto
                {
                    SoCont = d.SoCont,
                    MaISO = d.MaISO,
                    LoaiContNoiBo = d.LoaiContNoiBo,
                    Rong = d.Rong,
                    SoBooking = d.SoBooking,
                    HenLuc = d.HenLuc,
                    BienSoXe = d.BienSoXe
                };
                if (!string.IsNullOrEmpty(d.Loi))
                {
                    dong.KetQua = KQ_LOI;
                    dong.LyDo = d.Loi;
                }
                else if (daCoLenh.Contains(d.SoCont))
                {
                    dong.KetQua = KQ_BO_QUA;
                    dong.LyDo = "Đã có lệnh đang mở — bỏ qua";
                }
                else if (!daGap.Add(d.SoCont))
                {
                    // Ban MVC tao 2 container trung so trong cung ban tin -> loi khoa duy nhat khi luu
                    dong.KetQua = KQ_BO_QUA;
                    dong.LyDo = "Lặp lại trong bản tin — bỏ qua";
                }
                else
                {
                    dong.KetQua = KQ_TAO;
                    dong.LyDo = "Sẽ tạo lệnh";
                }
                ds.Add(dong);
            }
            return ds;
        }

        /// <summary>
        /// Giai thich tung doan bang tieng Viet — de nguoi khong biet EDIFACT van kiem tra duoc
        /// ban tin truoc khi gui (giu nguyen noi dung ban MVC).
        /// </summary>
        private static List<DoanEdiDto> GiaiThichDoan(string noiDung)
        {
            var ds = new List<DoanEdiDto>();
            foreach (var d in EdiService.TachDoan(noiDung ?? ""))
            {
                var tag = d.Length >= 3 ? d.Substring(0, 3) : d;
                string mo;
                switch (tag)
                {
                    case "UNB": mo = "Mở phong bì trao đổi — bên gửi, bên nhận, ngày giờ, số tham chiếu"; break;
                    case "UNH": mo = "Mở bản tin — loại bản tin và phiên bản chuẩn"; break;
                    case "BGM": mo = "Số và loại chứng từ (34 = báo cáo container vào/ra depot)"; break;
                    case "DTM":
                        mo = d.Contains("+137:") ? "Ngày lập bản tin"
                           : d.Contains("+178:") ? "Thời điểm container VÀO bãi (thực tế)"
                           : d.Contains("+186:") ? "Thời điểm container RA khỏi bãi (thực tế)"
                           : "Mốc thời gian";
                        break;
                    case "NAD":
                        mo = d.StartsWith("NAD+MS", StringComparison.Ordinal) ? "Bên gửi bản tin (depot)"
                           : d.StartsWith("NAD+MR", StringComparison.Ordinal) ? "Bên nhận bản tin (hãng tàu)"
                           : d.StartsWith("NAD+CF", StringComparison.Ordinal) ? "Hãng khai thác vỏ container"
                           : "Thông tin bên tham gia";
                        break;
                    case "LOC": mo = "Địa điểm giao nhận"; break;
                    case "EQD": mo = "Container: số hiệu, mã kích thước ISO 6346, trạng thái, rỗng(4)/có hàng(5)"; break;
                    case "RFF": mo = "Số tham chiếu kèm theo (booking / lệnh giao nhận)"; break;
                    case "TDT": mo = "Phương tiện vận chuyển (31 = đường bộ) và biển số xe"; break;
                    case "MEA": mo = "Khối lượng hàng (kg) lấy từ phiếu cân"; break;
                    case "SEL": mo = "Số chì niêm phong"; break;
                    case "FTX": mo = "Ghi chú tự do"; break;
                    case "CNT": mo = "Tổng kiểm — số container trong bản tin"; break;
                    case "UNT": mo = "Đóng bản tin — tổng số đoạn (dùng để đối chiếu toàn vẹn)"; break;
                    case "UNZ": mo = "Đóng phong bì trao đổi"; break;
                    default: mo = "—"; break;
                }
                ds.Add(new DoanEdiDto { Doan = d, Ma = tag, MoTa = mo });
            }
            return ds;
        }

        /// <summary>So chung tu COPARN do hang tau dat co the chua ky tu cam trong ten tep (/ \ : * ? ...)</summary>
        private static string TenTepAnToan(string ten)
        {
            var cam = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(ten ?? "EDI.edi");
            for (var i = 0; i < sb.Length; i++)
                if (Array.IndexOf(cam, sb[i]) >= 0) sb[i] = '_';
            return sb.ToString();
        }

        private static string Cat(string s, int toiDa) => s != null && s.Length > toiDa ? s.Substring(0, toiDa) : s;
    }

    // ================================================================
    // DTO
    // ================================================================

    public class BanTinEdiDto
    {
        public int MaBT { get; set; }
        /// <summary>CODECO / COPARN / COARRI</summary>
        public string LoaiBT { get; set; }
        /// <summary>Gửi / Nhận</summary>
        public string Huong { get; set; }
        public int? MaHangTau { get; set; }
        public string TenHangTau { get; set; }
        public string SoThamChieu { get; set; }
        public string SoChungTu { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public int SoLuongCont { get; set; }
        /// <summary>Chờ gửi / Đã gửi / Đã xử lý / Lỗi</summary>
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public string NguoiTao { get; set; }
        public DateTime? NgayGui { get; set; }
    }

    public class TongQuanEdiDto
    {
        public TrangDto<BanTinEdiDto> DanhSach { get; set; }
        /// <summary>Loại đang lọc (rỗng = tất cả)</summary>
        public string Loai { get; set; }
        public int SoCodeco { get; set; }
        public int SoCoparn { get; set; }
        public int SoCoarri { get; set; }
        /// <summary>Lượt container qua cổng chưa được báo cáo cho hãng tàu nào</summary>
        public int ChuaBaoCao { get; set; }
        /// <summary>Mã định danh depot dùng trong bản tin (AppSettings:MaDepotEDI)</summary>
        public string MaDepot { get; set; }
    }

    public class ChuyenCoarriDto
    {
        public int MaChuyen { get; set; }
        public string Nhan { get; set; }
        /// <summary>Số container gán chuyến nhập (dỡ hàng)</summary>
        public int SoNhap { get; set; }
        /// <summary>Số container gán chuyến xuất (xếp hàng)</summary>
        public int SoXuat { get; set; }
    }

    public class LuaChonEdiDto
    {
        public List<LuaChonDto> HangTaus { get; set; }
        public List<ChuyenCoarriDto> ChuyenTaus { get; set; }
    }

    public class DoanEdiDto
    {
        /// <summary>Nguyên văn đoạn EDIFACT (không kèm dấu ' kết đoạn)</summary>
        public string Doan { get; set; }
        /// <summary>Mã đoạn 3 ký tự: UNB, EQD...</summary>
        public string Ma { get; set; }
        public string MoTa { get; set; }
    }

    public class ChiTietBanTinEdiDto : BanTinEdiDto
    {
        public string MaSCAC { get; set; }
        public string NoiDung { get; set; }
        public string TenTep { get; set; }
        public int SoDoan { get; set; }
        public List<DoanEdiDto> GiaiThich { get; set; }
    }

    public class SinhCodecoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hãng tàu.")]
        public int MaHangTau { get; set; }

        /// <summary>Lấy lượt trong N ngày qua (1–90, mặc định 7)</summary>
        public int SoNgay { get; set; } = 7;
    }

    public class SinhCoarriDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chuyến tàu.")]
        public int MaChuyen { get; set; }

        /// <summary>"Do" = dỡ hàng (container rời tàu) · "Xep" = xếp hàng (container lên tàu)</summary>
        [StringLength(10, ErrorMessage = "Chiều không hợp lệ.")]
        public string Chieu { get; set; } = "Do";
    }

    /// <summary>Form đọc COPARN: dán nội dung HOẶC tải tệp .edi (tệp được ưu tiên)</summary>
    public class DocCoparnDto
    {
        public string NoiDung { get; set; }
        public IFormFile Tep { get; set; }
        public int? MaHangTau { get; set; }
        /// <summary>LayRong / TraRong / GiaoCont / HaCont (sai -> LayRong)</summary>
        public string LoaiLenh { get; set; }
    }

    public class TaoLenhCoparnDto
    {
        [Required(ErrorMessage = "Chưa có nội dung bản tin COPARN.")]
        [StringLength(2 * 1024 * 1024, ErrorMessage = "Nội dung bản tin quá 2MB.")]
        public string NoiDung { get; set; }

        public int? MaHangTau { get; set; }

        [StringLength(20, ErrorMessage = "Loại lệnh không hợp lệ.")]
        public string LoaiLenh { get; set; }
    }

    public class DongCoparnXemDto
    {
        public string SoCont { get; set; }
        public string MaISO { get; set; }
        public string LoaiContNoiBo { get; set; }
        public bool Rong { get; set; }
        public string SoBooking { get; set; }
        public DateTime? HenLuc { get; set; }
        public string BienSoXe { get; set; }
        /// <summary>tao / boQua / loi</summary>
        public string KetQua { get; set; }
        public string LyDo { get; set; }
    }

    public class XemTruocCoparnDto
    {
        public string LoaiBanTin { get; set; }
        public string SoChungTu { get; set; }
        public string BenGui { get; set; }
        public string BenNhan { get; set; }
        public string SoThamChieu { get; set; }
        /// <summary>Nội dung đã đọc (kể cả từ tệp) — gửi lại khi xác nhận tạo lệnh</summary>
        public string NoiDung { get; set; }
        public int? MaHangTau { get; set; }
        public string LoaiLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public int SoHopLe { get; set; }
        public int SoBoQua { get; set; }
        public int SoLoi { get; set; }
        public List<DongCoparnXemDto> Dong { get; set; }
    }
}
