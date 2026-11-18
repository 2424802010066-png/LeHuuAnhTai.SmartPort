using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// Bảng điều khiển AGENT TỰ HÀNH ĐỘNG (thay màn hình MVC Admin/Agent).
    /// Trả lời ba câu của chủ depot: agent ĐÃ LÀM GÌ (nhật ký có lý do), được PHÉP làm gì (3 chế độ),
    /// làm sai thì GỠ thế nào (hoàn tác).
    /// </summary>
    [Route("api/agent-tu-dong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class AgentTuDongController : ApiGocController
    {
        private static readonly string[] CHE_DO = { "Tắt", "Xin phép", "Tự làm" };

        /// <summary>Tien trinh nen AgentNenService chay 30 phut/lan (hang so CHU_KY trong service)</summary>
        private const int CHU_KY_NEN_PHUT = 30;

        // Ky nang TAO RA ban ghi that -> bang cua ban ghi do. Chi nhung cap nay moi hoan tac duoc:
        // NhacNoQuaHan cung tham chieu "PhieuLuuBai" nhung la phieu CO SAN cua khach (chi nhac no),
        // hoan tac no se huy nham phieu thu that.
        private static readonly Dictionary<string, string> BANG_DO_AGENT_TAO = new Dictionary<string, string>
        {
            ["ChotPhiLuuBai"] = "PhieuLuuBai",
            ["SinhCodeco"] = "BanTinEDI",
            ["ChanGplxHetHan"] = "SuCoAnNinh"
        };

        private readonly AppDb db;
        private readonly IConfiguration cauHinh;

        public AgentTuDongController(AppDb db, IConfiguration cauHinh)
        {
            this.db = db;
            this.cauHinh = cauHinh;
        }

        /// <summary>Cấu hình 7 kỹ năng, thống kê (chờ duyệt, tự làm, tiền agent chốt hộ, đã hoàn tác) và tình trạng tiến trình nền.</summary>
        [HttpGet]
        public ActionResult<TongQuanAgentDto> TongQuan()
        {
            var tatCa = db.NhatKyAgents.AsNoTracking();

            var choTheoKyNang = tatCa.Where(n => n.TrangThai == "Chờ duyệt")
                .GroupBy(n => n.KyNang)
                .Select(g => new { KyNang = g.Key, So = g.Count() })
                .ToList()
                .ToDictionary(x => x.KyNang, x => x.So);

            // Tien agent "chot ho" — chi phieu do ky nang ChotPhiLuuBai lap (tu lam hoac duoc duyet)
            var maPhieu = tatCa.Where(n => n.KyNang == "ChotPhiLuuBai" && n.BangThamChieu == "PhieuLuuBai"
                                        && !n.DaHoanTac && n.TrangThai == "Đã làm" && n.MaThamChieu != null)
                               .Select(n => n.MaThamChieu.Value).Distinct().ToList();
            var tienDaChot = maPhieu.Count == 0 ? 0m
                : db.PhieuLuuBais.AsNoTracking().Where(p => maPhieu.Contains(p.MaPhieu)).Sum(p => (decimal?)p.TongTien) ?? 0m;

            return new TongQuanAgentDto
            {
                KyNang = db.CauHinhAgents.AsNoTracking().OrderBy(c => c.MaCH).ToList()
                    .Select(c => new KyNangAgentDto
                    {
                        MaCH = c.MaCH,
                        KyNang = c.KyNang,
                        TenHienThi = c.TenHienThi,
                        MoTa = c.MoTa,
                        CheDo = c.CheDo,
                        Nguong = c.Nguong,
                        ToiDaMoiLan = c.ToiDaMoiLan,
                        LanChayCuoi = c.LanChayCuoi,
                        GhiDuLieuThat = BANG_DO_AGENT_TAO.ContainsKey(c.KyNang ?? ""),
                        SoChoDuyet = choTheoKyNang.TryGetValue(c.KyNang ?? "", out var so) ? so : 0
                    }).ToList(),
                ThongKe = new ThongKeAgentDto
                {
                    SoChoDuyet = tatCa.Count(n => n.TrangThai == "Chờ duyệt"),
                    SoDaLam = tatCa.Count(n => n.TrangThai == "Đã làm"),
                    SoTuLam = tatCa.Count(n => n.TuDong && n.TrangThai == "Đã làm"),
                    SoHoanTac = tatCa.Count(n => n.DaHoanTac),
                    TienDaChot = tienDaChot
                },
                TienTrinhNen = new TienTrinhNenDto
                {
                    DangBat = cauHinh.GetValue("Agent:ChayNen", true),
                    ChuKyPhut = CHU_KY_NEN_PHUT,
                    LanChayCuoi = AgentNenService.LanChayCuoi,
                    KetQuaCuoi = AgentNenService.KetQuaCuoi
                }
            };
        }

        /// <summary>Nhật ký việc agent đã làm / đề nghị, mới nhất trước — lọc theo kỹ năng, trạng thái (Chờ duyệt / Đã làm / Đã hủy / Lỗi).</summary>
        [HttpGet("nhat-ky")]
        public ActionResult<TrangDto<ViecAgentDto>> NhatKy([FromQuery] string kyNang, [FromQuery] string trangThai,
                                                          [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var q = db.NhatKyAgents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(kyNang)) q = q.Where(n => n.KyNang == kyNang);
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(n => n.TrangThai == trangThai);

            var kq = q.OrderByDescending(n => n.MaNK)
                .Select(n => new ViecAgentDto
                {
                    MaNK = n.MaNK,
                    Luc = n.Luc,
                    KyNang = n.KyNang,
                    DoiTuong = n.DoiTuong,
                    LyDo = n.LyDo,
                    HanhDong = n.HanhDong,
                    KetQua = n.KetQua,
                    TrangThai = n.TrangThai,
                    TuDong = n.TuDong,
                    BangThamChieu = n.BangThamChieu,
                    MaThamChieu = n.MaThamChieu,
                    DaHoanTac = n.DaHoanTac,
                    NguoiXuLy = n.NguoiXuLy,
                    LucXuLy = n.LucXuLy,
                    NguoiChay = n.NguoiChay
                })
                .LayTrang(trang, coTrang);

            // Ten hien thi + nut thao tac + link ban ghi: tinh sau khi lay trang (khong dich duoc sang SQL)
            var tenKyNang = db.CauHinhAgents.AsNoTracking().ToList()
                .GroupBy(c => c.KyNang).ToDictionary(g => g.Key, g => g.First().TenHienThi);
            foreach (var v in kq.Items)
            {
                v.TenKyNang = tenKyNang.TryGetValue(v.KyNang ?? "", out var ten) && !string.IsNullOrEmpty(ten) ? ten : v.KyNang;
                v.CoTheDuyet = v.TrangThai == "Chờ duyệt";
                v.CoTheHoanTac = v.TrangThai == "Đã làm" && !v.DaHoanTac && v.MaThamChieu != null && DoAgentTao(v.KyNang, v.BangThamChieu);
                v.DuongDanBanGhi = DuongDanBanGhi(v);
            }
            return kq;
        }

        /// <summary>Chạy agent ngay — tất cả kỹ năng đang bật, hoặc riêng một kỹ năng (kyNang).</summary>
        [HttpPost("chay-ngay")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult ChayNgay([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChayAgentDto yc)
        {
            var kyNang = string.IsNullOrWhiteSpace(yc?.KyNang) ? null : yc.KyNang.Trim();
            if (kyNang != null && !db.CauHinhAgents.Any(c => c.KyNang == kyNang))
                return Loi("Không có kỹ năng agent \"" + kyNang + "\".");

            var mocTruoc = db.NhatKyAgents.Max(n => (int?)n.MaNK) ?? 0;
            AgentTuDongService.KetQuaChay kq;
            try
            {
                kq = AgentTuDongService.ChayTatCa(db, NguoiDung, kyNang);
            }
            catch (Exception ex)
            {
                GhiNhatKy("Chạy agent tự động: LỖI — " + LoiGoc(ex));
                return Loi("Agent chạy bị lỗi: " + LoiGoc(ex));
            }

            GhiNhatKy("Chạy agent tự động: làm " + kq.DaLam + ", chờ duyệt " + kq.ChoDuyet
                      + (kq.Loi > 0 ? ", lỗi " + kq.Loi : ""));

            var thongBao = kq.DaLam + kq.ChoDuyet == 0 && kq.Loi == 0
                ? "Agent đã quét xong — không có việc gì cần làm lúc này."
                : "Agent xong: đã làm " + kq.DaLam + " việc, " + kq.ChoDuyet + " việc chờ bạn duyệt."
                  + (kq.Loi > 0 ? " (" + kq.Loi + " kỹ năng lỗi)" : "")
                  + (kq.ChiTiet.Count > 0 ? " — " + string.Join("; ", kq.ChiTiet) : "");

            // Viec vua sinh ra trong luot nay (de giao dien to sang)
            var nguoi = NguoiDung;
            var maMoi = db.NhatKyAgents.AsNoTracking()
                .Where(n => n.MaNK > mocTruoc && n.NguoiChay == nguoi)
                .OrderBy(n => n.MaNK).Select(n => n.MaNK).ToList();

            return XongViec(thongBao, null, new KetQuaChayAgentDto
            {
                DaLam = kq.DaLam,
                ChoDuyet = kq.ChoDuyet,
                Loi = kq.Loi,
                ChiTiet = kq.ChiTiet,
                MaViecMoi = maMoi
            });
        }

        /// <summary>Đổi chế độ (Tắt / Xin phép / Tự làm), ngưỡng và số việc tối đa mỗi lần chạy của một kỹ năng.</summary>
        [HttpPut("cau-hinh/{maCH:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DoiCheDo(int maCH, [FromBody] DoiCheDoAgentDto yc)
        {
            var ch = db.CauHinhAgents.Find(maCH);
            if (ch == null) return KhongThay("Không tìm thấy kỹ năng agent #" + maCH + ".");

            // Giong ban MVC: che do la -> "Xin phép"; chan cau hinh nguy hiem (1 lan chay toi da 200 ban ghi)
            var cheDo = CHE_DO.Contains(yc.CheDo) ? yc.CheDo : "Xin phép";
            var toiDaMoiLan = Math.Min(200, Math.Max(1, yc.ToiDaMoiLan));
            var nguong = Math.Max(0, yc.Nguong);

            var cu = ch.CheDo;
            ch.CheDo = cheDo;
            ch.Nguong = nguong;
            ch.ToiDaMoiLan = toiDaMoiLan;
            db.SaveChanges();

            GhiNhatKy("Đổi chế độ agent '" + ch.TenHienThi + "': " + cu + " → " + cheDo);
            return XongViec("Đã đặt \"" + ch.TenHienThi + "\" sang chế độ " + cheDo + ".", ch.MaCH,
                new { cheDo, nguong, toiDaMoiLan });
        }

        /// <summary>Duyệt một đề nghị "Chờ duyệt" — agent thực thi thật (lập phiếu thu, sinh CODECO) hoặc ghi nhận đã xử lý.</summary>
        [HttpPost("{id:int}/duyet")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Duyet(int id)
        {
            var nk = db.NhatKyAgents.Find(id);
            if (nk == null) return KhongThay("Không tìm thấy việc #" + id + ".");
            if (nk.TrangThai != "Chờ duyệt") return Loi("Việc này không ở trạng thái chờ duyệt.");

            string kq;
            try
            {
                kq = AgentTuDongService.Duyet(db, id, NguoiDung);
            }
            catch (Exception ex)
            {
                return Loi("Không thực hiện được việc agent đề nghị: " + LoiGoc(ex));
            }

            // Service da luu tren chinh entity dang theo doi -> doc trang thai moi de biet thanh cong hay khong
            if (nk.TrangThai == "Đã làm" || nk.TrangThai == "Đã hủy")
                return XongViec(kq, id, new { trangThai = nk.TrangThai, ketQua = nk.KetQua });
            return Loi(kq);
        }

        /// <summary>Bỏ qua một đề nghị đang chờ duyệt.</summary>
        [HttpPost("{id:int}/huy")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Huy(int id)
        {
            var nk = db.NhatKyAgents.Find(id);
            if (nk == null) return KhongThay("Không tìm thấy việc #" + id + ".");
            // Ban MVC chi hien nut "Bỏ qua" voi viec chờ duyệt; API chan luon de khong "hủy" nham viec da lam
            if (nk.TrangThai != "Chờ duyệt") return Loi("Chỉ bỏ qua được việc đang chờ duyệt.");

            var kq = AgentTuDongService.Huy(db, id, NguoiDung);
            return XongViec(kq, id);
        }

        /// <summary>Hoàn tác thứ agent đã tạo: hủy phiếu thu chưa thu tiền, xóa bản tin EDI chưa gửi, xóa sự cố agent mở.</summary>
        [HttpPost("{id:int}/hoan-tac")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult HoanTac(int id)
        {
            var nk = db.NhatKyAgents.Find(id);
            if (nk == null) return KhongThay("Không tìm thấy việc #" + id + ".");

            var daHoanTacTruoc = nk.DaHoanTac;
            var biLoi = false;
            string kq;
            if (!daHoanTacTruoc && nk.TrangThai != "Đã làm")
                kq = "Chỉ hoàn tác được việc agent đã làm.";
            else if (!daHoanTacTruoc && nk.MaThamChieu != null && BANG_DO_AGENT_TAO.ContainsValue(nk.BangThamChieu ?? "")
                     && !DoAgentTao(nk.KyNang, nk.BangThamChieu))
                kq = "Việc này chỉ tham chiếu bản ghi có sẵn, không do agent tạo ra — không có gì để hoàn tác.";
            else
            {
                try
                {
                    kq = AgentTuDongService.HoanTac(db, id, NguoiDung);
                }
                catch (Exception ex)
                {
                    biLoi = true;
                    kq = "LỖI — " + LoiGoc(ex);
                }
            }

            // Giong ban MVC: moi lan bam hoan tac deu ghi nhat ky kem ket qua
            GhiNhatKy("Hoàn tác việc agent #" + id + ": " + kq);
            return !biLoi && !daHoanTacTruoc && nk.DaHoanTac ? XongViec(kq, id) : Loi(kq);
        }

        private static bool DoAgentTao(string kyNang, string bang) =>
            kyNang != null && BANG_DO_AGENT_TAO.TryGetValue(kyNang, out var b) && b == bang;

        /// <summary>Man hinh React mo ban ghi agent tham chieu (giu dia chi ban MVC, viet thuong)</summary>
        private static string DuongDanBanGhi(ViecAgentDto v)
        {
            if (v.MaThamChieu == null || string.IsNullOrEmpty(v.BangThamChieu)) return null;
            var ma = v.MaThamChieu.Value;
            var q = string.IsNullOrEmpty(v.DoiTuong) ? "" : "?q=" + Uri.EscapeDataString(v.DoiTuong);
            switch (v.BangThamChieu)
            {
                case "PhieuLuuBai": return "/admin/luubai/phieu/" + ma;
                case "BanTinEDI": return v.DaHoanTac ? null : "/admin/edi/xem/" + ma;       // hoan tac = da xoa ban tin
                case "SuCoAnNinh": return v.DaHoanTac ? null : "/admin/security";
                case "Container": return "/admin/containers/hoso/" + ma;
                case "PhieuSuaChua": return "/admin/depot/ineor/" + ma;
                case "TheXe": return "/admin/phicong/thexe" + q;
                case "TaiXe": return "/admin/taixe" + q;
                case "HangTau": return "/admin/edi";
                default: return null;
            }
        }
    }

    public class TongQuanAgentDto
    {
        public List<KyNangAgentDto> KyNang { get; set; }
        public ThongKeAgentDto ThongKe { get; set; }
        public TienTrinhNenDto TienTrinhNen { get; set; }
    }

    public class KyNangAgentDto
    {
        public int MaCH { get; set; }
        public string KyNang { get; set; }
        public string TenHienThi { get; set; }
        public string MoTa { get; set; }
        /// <summary>Tắt / Xin phép / Tự làm</summary>
        public string CheDo { get; set; }
        public int Nguong { get; set; }
        public int ToiDaMoiLan { get; set; }
        public DateTime? LanChayCuoi { get; set; }
        /// <summary>Ky nang o che do Tu lam se ghi du lieu that (phieu thu, ban tin EDI, su co)</summary>
        public bool GhiDuLieuThat { get; set; }
        public int SoChoDuyet { get; set; }
    }

    public class ThongKeAgentDto
    {
        public int SoChoDuyet { get; set; }
        public int SoDaLam { get; set; }
        public int SoTuLam { get; set; }
        public int SoHoanTac { get; set; }
        /// <summary>Tong tien cac phieu luu bai do agent lap (chua hoan tac)</summary>
        public decimal TienDaChot { get; set; }
    }

    public class TienTrinhNenDto
    {
        /// <summary>Cau hinh Agent:ChayNen cua API (mac dinh bat)</summary>
        public bool DangBat { get; set; }
        public int ChuKyPhut { get; set; }
        /// <summary>Lan chay nen gan nhat tu khi API khoi dong (null = chua chay lan nao)</summary>
        public DateTime? LanChayCuoi { get; set; }
        public string KetQuaCuoi { get; set; }
    }

    public class ViecAgentDto
    {
        public int MaNK { get; set; }
        public DateTime Luc { get; set; }
        public string KyNang { get; set; }
        public string TenKyNang { get; set; }
        public string DoiTuong { get; set; }
        public string LyDo { get; set; }
        public string HanhDong { get; set; }
        public string KetQua { get; set; }
        /// <summary>Chờ duyệt / Đã làm / Đã hủy / Lỗi</summary>
        public string TrangThai { get; set; }
        public bool TuDong { get; set; }
        public string BangThamChieu { get; set; }
        public int? MaThamChieu { get; set; }
        public bool DaHoanTac { get; set; }
        public string NguoiXuLy { get; set; }
        public DateTime? LucXuLy { get; set; }
        public string NguoiChay { get; set; }
        public bool CoTheDuyet { get; set; }
        public bool CoTheHoanTac { get; set; }
        /// <summary>Dia chi man hinh React cua ban ghi duoc tham chieu (null = khong co)</summary>
        public string DuongDanBanGhi { get; set; }
    }

    public class ChayAgentDto
    {
        /// <summary>Bo trong = chay tat ca ky nang dang bat</summary>
        [StringLength(40, ErrorMessage = "Mã kỹ năng tối đa 40 ký tự.")]
        public string KyNang { get; set; }
    }

    public class KetQuaChayAgentDto
    {
        public int DaLam { get; set; }
        public int ChoDuyet { get; set; }
        public int Loi { get; set; }
        public List<string> ChiTiet { get; set; }
        public List<int> MaViecMoi { get; set; }
    }

    public class DoiCheDoAgentDto
    {
        /// <summary>Tắt / Xin phép / Tự làm (gia tri khac -> Xin phép)</summary>
        public string CheDo { get; set; }
        /// <summary>Nguong (ngay / so luong) — am -> 0</summary>
        public int Nguong { get; set; }
        /// <summary>Toi da so viec moi lan chay, gioi han 1..200</summary>
        public int ToiDaMoiLan { get; set; }
    }
}
