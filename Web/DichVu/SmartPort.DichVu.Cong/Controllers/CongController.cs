using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Contracts.Cong;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Modules.Cong;
using SmartPort.Services.CangSo;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// Cổng thông minh: nhận kết quả nhận dạng biển số từ giao diện, đối chiếu lệnh giao nhận,
    /// ghi lượt xe. Controller chỉ điều phối — nghiệp vụ nằm trong module Cổng.
    /// Chạy khi mất mạng: trạm cổng tải gói ngoại tuyến, ghi tạm lượt xe, đồng bộ lại (idempotent, giờ gốc, vẫn kiểm tuân thủ).
    /// </summary>
    [ApiController]
    [Route("api/cong")]
    [Authorize(Roles = "Admin,NhanVien")]
    [Produces("application/json")]
    public class CongController : ControllerBase
    {
        /// <summary>Số lượt tối đa mỗi lô đồng bộ — mục vượt trả "loi" (thuLai) để trạm gửi ở lô sau, không cắt im lặng</summary>
        public const int ToiDaMoiLo = 200;

        /// <summary>Số dòng tối đa mỗi loại trong gói ngoại tuyến</summary>
        public const int ToiDaMoiLoai = 500;
        /// <summary>The xe trong goi ngoai tuyen (moi dong ~60 byte; cang co vai nghin the dang dung)</summary>
        public const int ToiDaTheXe = 3000;

        private const string TieuDeKhoa = "Idempotency-Key";

        private readonly ICongLenh lenh;
        private readonly ICongTruyVan truyVan;
        private readonly AppDb db;
        private readonly IServiceScopeFactory phamVi;

        public CongController(ICongLenh lenh, ICongTruyVan truyVan, AppDb db, IServiceScopeFactory phamVi)
        {
            this.lenh = lenh;
            this.truyVan = truyVan;
            this.db = db;
            this.phamVi = phamVi;
        }

        /// <summary>Tổng hợp lượt xe hôm nay.</summary>
        [HttpGet("tong-hop-hom-nay")]
        public ActionResult<TongHopCongDto> TongHopHomNay()
        {
            var t = truyVan.TongHopHomNay();
            return new TongHopCongDto { XeVao = t.XeVao, XeRa = t.XeRa, DangTrongCang = t.DangTrongCang, TinCayTrungBinh = t.TinCayTrungBinh };
        }

        /// <summary>Danh sách lượt xe mới nhất (kèm phí cổng đã thu, nếu có).</summary>
        [HttpGet("luot-xe")]
        public ActionResult<List<LuotXeDto>> DanhSachLuotXe([FromQuery] int soLuong = 30)
        {
            return truyVan.LuotXeGanDay(soLuong).Select(x => new LuotXeDto
            {
                MaXV = x.MaXV,
                BienSo = x.BienSo,
                Cong = x.Cong,
                HuongDi = x.HuongDi,
                LoaiXe = x.LoaiXe,
                DoTinCay = x.DoTinCay,
                Anh = DuongDan.AnhTuApi(x.AnhXe),
                Dock = x.Dock,
                SoCont = x.SoCont,
                GhiChu = x.GhiChu,
                ThoiGian = x.ThoiGian,
                PhiCong = x.PhiCong,
                HinhThucThu = x.HinhThucThu
            }).ToList();
        }

        /// <summary>Các lệnh giao nhận đang mở (chờ xe / đã vào cổng) — để chọn khi demo hoặc khi OCR không khớp.</summary>
        [HttpGet("lenh-dang-mo")]
        public ActionResult<List<LenhGiaoNhanDto>> LenhDangMo()
        {
            return truyVan.LenhDangMo().Select(l => new LenhGiaoNhanDto
            {
                MaLenh = l.MaLenh,
                SoLenh = l.SoLenh,
                Loai = l.Loai,
                SoCont = l.SoCont,
                BienSoXe = l.BienSoXe,
                TaiXe = l.TaiXe,
                TrangThai = l.TrangThai,
                HenTuLuc = l.HenTuLuc,
                HenDenLuc = l.HenDenLuc,
                DaCheckIn = l.DaCheckIn
            }).ToList();
        }

        /// <summary>Đối chiếu biển số vừa đọc được với lệnh giao nhận đang mở.</summary>
        /// <response code="200">Có lệnh khớp</response>
        /// <response code="204">Không có lệnh nào khớp biển số</response>
        [HttpGet("doi-chieu-lenh")]
        [ProducesResponseType(typeof(LenhKhop), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult DoiChieuLenh([FromQuery] string bienSo)
        {
            if (string.IsNullOrWhiteSpace(bienSo))
                return BadRequest(new ThongBaoDto { ThongBao = "Thiếu biển số." });
            var khop = truyVan.DoiChieuLenh(bienSo);
            return khop == null ? NoContent() : Ok(khop);
        }

        /// <summary>Ghi một lượt xe qua cổng.</summary>
        /// <remarks>
        /// Module Cổng ghi lượt xe (và cập nhật lệnh giao nhận nếu có) rồi phát sự kiện <c>cong.xe-da-qua-cong</c>
        /// trong cùng giao dịch. Các bước sau chạy BẤT ĐỒNG BỘ qua sự kiện, thường xong trong khoảng 1 giây:
        /// module Bãi đồng bộ container (<c>bai.*</c>), module Tài chính thu phí cổng khi xe ra và chốt phí lưu bãi
        /// khi container rời bãi (<c>tai-chinh.*</c>). Theo dõi kết quả qua luồng <c>/api/su-kien</c>
        /// (các sự kiện có cùng <c>maXV</c>). <c>choBai</c> / <c>choPhiCong</c> cho biết bước nào sẽ phản hồi.
        ///
        /// CHỐNG GỬI LẶP: gửi kèm header <c>Idempotency-Key</c> (≤ 64 ký tự, sinh một lần cho mỗi lượt, GIỮ NGUYÊN khi bấm lại).
        /// Trùng khóa → 200 + header <c>Idempotent-Replayed: true</c> với đúng kết quả cũ, không ghi lượt thứ hai, không thu phí lần hai.
        /// Cùng khóa nhưng biển / hướng khác → 422. Khóa này dùng chung với <c>maThaoTac</c> của <c>/api/cong/dong-bo</c>.
        /// </remarks>
        /// <response code="201">Đã ghi lượt mới</response>
        /// <response code="200">Gửi lặp (cùng Idempotency-Key) — kết quả của lần đầu</response>
        /// <response code="400">Dữ liệu sai hoặc KHÔNG CHO RA CỔNG (eDO / hải quan) — trạm phải hiện đỏ, KHÔNG xếp hàng gửi lại</response>
        /// <response code="409">Cùng khóa đang được xử lý — gửi lại sau giây lát</response>
        /// <response code="422">Idempotency-Key đã dùng cho lượt xe khác</response>
        [HttpPost("luot-xe")]
        [ProducesResponseType(typeof(KetQuaLuotXeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(KetQuaLuotXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<KetQuaLuotXeDto>> GhiLuotXe([FromBody] YeuCauLuotXeDto yc, CancellationToken ct)
        {
            if (yc.HuongDi != "Vào" && yc.HuongDi != "Ra")
                return BadRequest(new ThongBaoDto { ThongBao = "Hướng đi phải là \"Vào\" hoặc \"Ra\"." });

            var khoa = Request.Headers[TieuDeKhoa].ToString().Trim();
            if (khoa.Length > 64)
                return BadRequest(new ThongBaoDto { ThongBao = "Idempotency-Key tối đa 64 ký tự." });

            // OFFLINE / NGOAI-TUYEN la nguon cua luong DONG BO (ghi tre: vi pham tuan thu thanh "can doi soat" thay vi bi
            // chan) — API luot thuong KHONG cho tu gan, neu khong client goi thang day se lach duoc kiem eDO / hai quan
            var nguon = string.IsNullOrWhiteSpace(yc.Nguon) || LaNguonDongBo(yc.Nguon) ? "WEB-API" : yc.Nguon.Trim();

            var kq = await lenh.GhiLuotXeAsync(new YeuCauGhiLuotXe
            {
                BienSo = yc.BienSo,
                HuongDi = yc.HuongDi,
                Cong = yc.Cong,
                LoaiXe = yc.LoaiXe,
                DoTinCay = yc.DoTinCay,
                Nguon = nguon,
                Anh = yc.Anh,
                OcrTho = yc.OcrTho,
                GhiChu = yc.GhiChu,
                MaLenh = yc.MaLenh,
                NguoiGhi = User.HoTen(),
                MaThaoTac = khoa.Length == 0 ? null : khoa
            }, ct);
            if (!kq.Ok && kq.KhacNoiDung) return UnprocessableEntity(new ThongBaoDto { ThongBao = kq.ThongBao });
            if (!kq.Ok && kq.LoiTamThoi) return Conflict(new ThongBaoDto { ThongBao = kq.ThongBao });
            if (!kq.Ok) return BadRequest(new ThongBaoDto { ThongBao = kq.ThongBao });

            var dto = new KetQuaLuotXeDto
            {
                MaXV = kq.MaXV,
                Gio = kq.Gio,
                Anh = DuongDan.AnhTuApi(kq.AnhXe),
                Dock = kq.Dock,
                CanhBao = kq.CanhBao,
                SoCont = kq.SoCont,
                SoLenh = kq.SoLenh,
                MaSuKien = kq.MaSuKien,
                ChoBai = kq.ChoBai,
                ChoPhiCong = kq.ChoPhiCong
            };
            if (kq.Trung)
            {
                Response.Headers["Idempotent-Replayed"] = "true";
                return Ok(dto);
            }

            NhatKyHelper.Ghi("ThaoTac", User.Ten(),
                "Ghi lượt xe " + yc.BienSo + " (" + yc.HuongDi + ") qua Web API" + (kq.SoCont != null ? " · cont " + kq.SoCont : ""),
                HttpContext);
            return Created("/api/cong/luot-xe/" + kq.MaXV, dto);
        }

        /// <summary>Đồng bộ lô lượt xe trạm cổng ghi lúc MẤT MẠNG (hợp đồng H1 — trạm offline-first).</summary>
        /// <remarks>
        /// - Idempotent theo <c>maThaoTac</c> (bắt buộc, ≤ 64): gửi lại → <c>trung</c> + đúng <c>maXV</c> cũ; 2 lô song song cùng mục
        ///   không sinh lượt kép (khóa chính bảng LuotXeNgoaiTuyen + bắt lỗi trùng khóa).
        /// - Giờ: lệch = giờ máy chủ nhận − <c>lucGuiTuMay</c>; giờ ghi = <c>lucXayRa</c> + lệch; tương lai → bây giờ; cũ hơn 72 giờ → vẫn ghi nhưng cần đối soát.
        /// - Tuân thủ (eDO, hải quan, lệnh hủy / hoàn tất / quá hạn, biển khác lệnh) VẪN KIỂM theo giờ xảy ra. Xe đã qua thật nên
        ///   vẫn ghi lượt nhưng trả <c>can-doi-soat</c> + lý do và phát sự kiện <c>cong.luot-xe-can-doi-soat</c>.
        /// - Không gửi <c>maLenh</c> → máy chủ tự đối chiếu lệnh theo biển + giờ xảy ra; phí cổng VẪN thu (có <c>tienMatTaiCong</c> thì ghi theo phiếu).
        /// - Ghi theo thứ tự giờ xảy ra; mỗi mục một giao dịch riêng; trả kết quả TỪNG mục (trạm chỉ giữ lại mục "loi").
        /// </remarks>
        [HttpPost("dong-bo")]
        [ProducesResponseType(typeof(KetQuaDongBoCongDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<KetQuaDongBoCongDto>> DongBo([FromBody] YeuCauDongBoCongDto yc, CancellationToken ct)
        {
            var nhan = DateTimeOffset.Now;
            var kq = new KetQuaDongBoCongDto();
            var items = yc?.Items ?? new List<MucDongBoCongDto>();
            var maThietBi = Cat(yc?.MaThietBi?.Trim(), 64);
            if (string.IsNullOrEmpty(maThietBi)) maThietBi = null;
            var gioGuiDung = DongHoNgoaiTuyen.ThuDoc(yc?.LucGuiTuMay, out var lucGui);
            var nguoiDongBo = User.HoTen();

            var viec = new List<MucCanGhi>();
            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var r = new KetQuaMucDongBoCongDto { MaThaoTac = it?.MaThaoTac };
                kq.KetQua.Add(r);
                if (i >= ToiDaMoiLo)
                {
                    DatLoi(r, "Lô vượt " + ToiDaMoiLo + " mục — mục này CHƯA xử lý, gửi lại ở lô sau.", thuLai: true);
                    continue;
                }
                var loi = KiemTraMuc(it, out var huong);
                if (loi != null)
                {
                    DatLoi(r, loi, thuLai: false);
                    continue;
                }

                var docDuoc = DongHoNgoaiTuyen.ThuDoc(it.LucXayRa, out var lucXayRa);
                var gio = DongHoNgoaiTuyen.Tinh(lucXayRa, gioGuiDung ? lucGui : null, nhan);
                if (!docDuoc)
                {
                    gio.LyDo.Clear();
                    gio.LyDo.Add("Giờ xe qua cổng sai định dạng (\"" + Cat(it.LucXayRa, 40) + "\") — tạm ghi theo giờ máy chủ nhận.");
                }
                // Bao ve bam "VAN CHO QUA" du tram bao khong duoc qua -> LUON doi soat (truoc day chi nam trong GhiChu, may chu ghi da-luu)
                if (it.VuotBanSao == true)
                    gio.LyDo.Add("Bảo vệ cho qua dù trạm báo: " + (string.IsNullOrWhiteSpace(it.LyDoVuot) ? "(không rõ lý do)" : Cat(it.LyDoVuot.Trim(), 300)));
                viec.Add(new MucCanGhi
                {
                    ViTri = i,
                    KetQua = r,
                    YeuCau = new YeuCauGhiLuotXe
                    {
                        BienSo = it.BienSo,
                        HuongDi = huong,
                        Cong = it.Cong,
                        LoaiXe = it.LoaiXe,
                        DoTinCay = it.DoTinCay,
                        Nguon = XeDaQuaCong.NguonNgoaiTuyen,
                        Anh = it.Anh,
                        GhiChu = "Ghi trễ khi mất mạng" + (maThietBi != null ? " · " + maThietBi : "")
                                 + (string.IsNullOrWhiteSpace(it.GhiChu) ? "" : " · " + it.GhiChu.Trim()),
                        MaLenh = it.MaLenh,
                        NguoiGhi = string.IsNullOrWhiteSpace(it.NguoiThaoTac) ? nguoiDongBo : it.NguoiThaoTac.Trim(),
                        NguoiDongBo = nguoiDongBo,
                        MaThaoTac = it.MaThaoTac.Trim(),
                        LaGhiTre = true,
                        MaThietBi = maThietBi,
                        TienMatTaiCong = it.TienMatTaiCong,
                        SoPhieuThu = it.SoPhieuThu,
                        SoContTram = it.SoCont,
                        ThoiGian = gio.ThoiGian,
                        NhanLuc = gio.NhanLuc,
                        LucXayRaGoc = gio.LucXayRaGoc,
                        LucGuiTuMay = gio.LucGuiTuMay,
                        DoLechGiay = gio.DoLechGiay,
                        LyDoDoiSoat = gio.LyDo
                    }
                });
            }

            await GhiTheoThuTuAsync(viec, ct);

            kq.DaLuu = kq.KetQua.Count(x => x.TrangThai == TrangThaiDongBo.DaLuu);
            kq.Trung = kq.KetQua.Count(x => x.TrangThai == TrangThaiDongBo.Trung);
            kq.CanDoiSoat = kq.KetQua.Count(x => x.TrangThai == TrangThaiDongBo.CanDoiSoat);
            kq.Loi = kq.KetQua.Count(x => x.TrangThai == TrangThaiDongBo.Loi);
            kq.GioMayChu = DateTime.UtcNow;
            if (kq.DaLuu + kq.CanDoiSoat > 0)
                NhatKyHelper.Ghi("ThaoTac", User.Ten(),
                    "Đồng bộ ngoại tuyến" + (maThietBi != null ? " trạm " + maThietBi : "") + ": " + kq.DaLuu + " lượt mới, "
                    + kq.CanDoiSoat + " cần đối soát, " + kq.Trung + " trùng, " + kq.Loi + " lỗi", HttpContext);
            return kq;
        }

        /// <summary>Đồng bộ LÔ lượt xe ghi tạm (client CŨ — giữ đường dẫn để tương thích; trạm mới dùng <c>/api/cong/dong-bo</c>).</summary>
        /// <remarks>
        /// Chạy CHUNG logic với <c>/api/cong/dong-bo</c>: vẫn kiểm tuân thủ (vi phạm → ghi + cần đối soát), vẫn thu phí cổng
        /// (luật H2 — không còn "nguồn OFFLINE thì bỏ phí"), máy chủ tự đối chiếu lệnh theo biển + giờ nếu không gửi <c>maLenh</c>.
        /// Chống trùng: khóa suy ra từ (biển, hướng, cổng, giờ) + cửa sổ ±90 giây. Còn lượt lỗi TẠM THỜI → trả 503 để client cũ
        /// giữ nguyên hàng đợi và gửi lại cả lô (lượt đã ghi sẽ thành "trùng", không ghi kép). Tối đa 200 lượt mỗi lô.
        /// </remarks>
        [HttpPost("dong-bo-lo")]
        [ProducesResponseType(typeof(KetQuaDongBoLoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KetQuaDongBoLoDto), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<KetQuaDongBoLoDto>> DongBoLo([FromBody] LoLuotXeOfflineDto lo, CancellationToken ct)
        {
            if (lo?.Items == null || lo.Items.Count == 0)
                return new KetQuaDongBoLoDto { DaLuu = 0, BoQua = 0, ThongBao = "Không có lượt xe nào chờ gửi." };

            var nhan = DateTimeOffset.Now;
            var nguoi = User.HoTen();
            var ketQua = new List<KetQuaMucDongBoCongDto>();
            var viec = new List<MucCanGhi>();
            for (var i = 0; i < lo.Items.Count; i++)
            {
                var it = lo.Items[i];
                var r = new KetQuaMucDongBoCongDto { MaThaoTac = it?.MaThaoTac };
                ketQua.Add(r);
                if (i >= ToiDaMoiLo)
                {
                    DatLoi(r, "Lô vượt " + ToiDaMoiLo + " lượt — lượt này CHƯA xử lý, gửi lại ở lô sau.", thuLai: true);
                    continue;
                }
                var bs = (it?.BienSo ?? "").Trim().ToUpperInvariant();
                if (bs.Length == 0)
                {
                    DatLoi(r, "Thiếu biển số.", thuLai: false);
                    continue;
                }
                var huong = it.HuongDi == "Ra" ? "Ra" : "Vào";
                DateTimeOffset? lucXayRa = null;
                try { if (it.Luc != null) lucXayRa = new DateTimeOffset(it.Luc.Value); }
                catch (ArgumentOutOfRangeException) { lucXayRa = null; }
                // Client cu khong gui gio may luc gui -> khong bu lech duoc; giu chan gio tuong lai / qua cu nhu luong moi
                var gio = DongHoNgoaiTuyen.Tinh(lucXayRa, null, nhan);
                // Client cu khong gui gio may luc gui -> khong hieu chinh / phat hien lui gio duoc: ghi tre qua 2 gio -> doi soat gio
                if (lucXayRa != null && nhan - lucXayRa.Value > TimeSpan.FromHours(2) && gio.LyDo.Count == 0)
                    gio.LyDo.Add("Client cũ không gửi giờ máy lúc gửi — không hiệu chỉnh được đồng hồ trạm; lượt ghi trễ "
                                 + Math.Round((nhan - lucXayRa.Value).TotalHours).ToString("0") + " giờ cần đối soát giờ.");
                var ma = string.IsNullOrWhiteSpace(it.MaThaoTac) ? KhoaSuyRa(bs, huong, it.Cong, lucXayRa) : it.MaThaoTac.Trim();
                r.MaThaoTac = ma;
                if (ma != null && ma.Length > 64)
                {
                    DatLoi(r, "Mã thao tác tối đa 64 ký tự.", thuLai: false);
                    continue;
                }
                viec.Add(new MucCanGhi
                {
                    ViTri = i,
                    KetQua = r,
                    YeuCau = new YeuCauGhiLuotXe
                    {
                        BienSo = bs,
                        Cong = it.Cong,
                        HuongDi = huong,
                        LoaiXe = it.LoaiXe,
                        Nguon = XeDaQuaCong.NguonNhapBu,
                        GhiChu = "Nhập bù khi mất mạng" + (string.IsNullOrWhiteSpace(it.GhiChu) ? "" : " · " + it.GhiChu.Trim()),
                        MaLenh = it.MaLenh,
                        SoContTram = it.SoCont,
                        TienMatTaiCong = it.TienMatTaiCong,
                        SoPhieuThu = it.SoPhieuThu,
                        NguoiGhi = nguoi,
                        NguoiDongBo = nguoi,
                        MaThaoTac = ma,
                        LaGhiTre = true,
                        ChongTrungTheoCuaSo = true,
                        ThoiGian = gio.ThoiGian,
                        NhanLuc = gio.NhanLuc,
                        LucXayRaGoc = gio.LucXayRaGoc,
                        LyDoDoiSoat = gio.LyDo
                    }
                });
            }

            await GhiTheoThuTuAsync(viec, ct);

            var daLuu = ketQua.Count(x => x.TrangThai == TrangThaiDongBo.DaLuu || x.TrangThai == TrangThaiDongBo.CanDoiSoat);
            var canDoiSoat = ketQua.Count(x => x.TrangThai == TrangThaiDongBo.CanDoiSoat);
            var trung = ketQua.Count(x => x.TrangThai == TrangThaiDongBo.Trung);
            var loiVinhVien = ketQua.Count(x => x.TrangThai == TrangThaiDongBo.Loi && !x.ThuLai);
            var loiTam = ketQua.Count(x => x.TrangThai == TrangThaiDongBo.Loi && x.ThuLai);
            if (daLuu > 0)
                NhatKyHelper.Ghi("ThaoTac", User.Ten(), "Đồng bộ " + daLuu + " lượt xe ghi khi mất mạng (" + canDoiSoat + " cần đối soát)", HttpContext);

            var dto = new KetQuaDongBoLoDto
            {
                DaLuu = daLuu,
                BoQua = trung + loiVinhVien,
                CanDoiSoat = canDoiSoat,
                Loi = loiVinhVien + loiTam,
                KetQua = ketQua,
                ThongBao = "Đã gửi lên hệ thống " + daLuu + " lượt xe"
                           + (canDoiSoat > 0 ? " (" + canDoiSoat + " lượt CẦN ĐỐI SOÁT)" : "")
                           + (trung > 0 ? ", bỏ qua " + trung + " bản trùng" : "")
                           + (loiVinhVien > 0 ? ", " + loiVinhVien + " lượt sai dữ liệu" : "") + "."
            };
            if (loiTam > 0)
            {
                // Client cu: loi lo thi GIU ca hang doi va gui lai -> luot da ghi thanh "trung" (khoa suy ra + cua so 90 giay),
                // luot loi tam thoi duoc thu lai. Tra 200 thi client cu XOA ca hang doi -> mat luot.
                dto.ThongBao += " Còn " + loiTam + " lượt chưa ghi được do lỗi tạm thời — giữ hàng đợi và gửi lại.";
                return StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
            }
            return dto;
        }

        /// <summary>Gói dữ liệu cho trạm cổng tự đối chiếu khi mất mạng (ETag — không đổi thì 304, không tải lại).</summary>
        /// <remarks>
        /// Lệnh mở 24 giờ tới (+ trễ 2 giờ) và mọi lệnh "Đã vào cổng"; eDO + tờ khai hải quan mới nhất của container trong các lệnh đó;
        /// thẻ xe đang kích hoạt; biểu giá phí cổng; biển số cấm (AppSettings:BienSoCam); mức kiểm tuân thủ. Tối đa 500 dòng mỗi loại.
        /// Gửi lại <c>If-None-Match</c> bằng ETag lần trước → 304 khi dữ liệu chưa đổi.
        /// </remarks>
        /// <param name="cong">Mã cổng của trạm, vd A1</param>
        [HttpGet("goi-ngoai-tuyen")]
        [ProducesResponseType(typeof(GoiNgoaiTuyenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public ActionResult<GoiNgoaiTuyenDto> GoiNgoaiTuyen([FromQuery] string cong)
        {
            var goi = TaoGoiNgoaiTuyen(string.IsNullOrWhiteSpace(cong) ? null : Cat(cong.Trim().ToUpperInvariant(), 20));

            // Phien ban = bam NOI DUNG (khong ke gioMayChu): du lieu khong doi -> cung ETag -> 304, tram khong tai lai
            var phienBan = BamNoiDung(goi);
            var etag = "\"" + phienBan + "\"";
            Response.Headers.ETag = etag;
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Gio-May-Chu"] = DateTime.UtcNow.ToString("o");
            if (KhopIfNoneMatch(Request.Headers.IfNoneMatch.ToString(), phienBan))
                return StatusCode(StatusCodes.Status304NotModified);

            goi.PhienBan = phienBan;
            goi.GioMayChu = DateTime.UtcNow;
            return goi;
        }

        /// <summary>Lượt xe ghi trễ CẦN ĐỐI SOÁT (vi phạm tuân thủ / xung đột lúc trạm mất mạng), mới nhất trước.</summary>
        /// <param name="tatCa">true: cả lượt đã đối soát xong</param>
        /// <param name="soLuong">Tối đa 500</param>
        /// <param name="ct"></param>
        [HttpGet("can-doi-soat")]
        [ProducesResponseType(typeof(List<LuotCanDoiSoatDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<LuotCanDoiSoatDto>>> DanhSachCanDoiSoat([FromQuery] bool tatCa = false, [FromQuery] int soLuong = 100,
            CancellationToken ct = default)
        {
            soLuong = Math.Max(1, Math.Min(500, soLuong));
            try
            {
                var q = db.LuotXeNgoaiTuyens.AsNoTracking().Where(x => x.TrangThai == TrangThaiDongBo.CanDoiSoat);
                if (!tatCa) q = q.Where(x => !x.DaDoiSoat);
                return await q.OrderByDescending(x => x.NhanLuc).Take(soLuong).Select(x => new LuotCanDoiSoatDto
                {
                    MaThaoTac = x.MaThaoTac,
                    MaXV = x.MaXV,
                    BienSo = x.BienSo,
                    HuongDi = x.HuongDi,
                    Cong = x.Cong,
                    MaThietBi = x.MaThietBi,
                    LyDoDoiSoat = x.LyDoDoiSoat,
                    ThoiGian = x.ThoiGianGhi,
                    NhanLuc = x.NhanLuc,
                    DoLechGiay = x.DoLechGiay,
                    TienMatTaiCong = x.TienMatTaiCong,
                    SoPhieuThu = x.SoPhieuThu,
                    NguoiThaoTac = x.NguoiThaoTac,
                    DaDoiSoat = x.DaDoiSoat,
                    DoiSoatLuc = x.DoiSoatLuc,
                    NguoiDoiSoat = x.NguoiDoiSoat,
                    GhiChuDoiSoat = x.GhiChuDoiSoat
                }).ToListAsync(ct);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Ban gop chua chay Add_LuotXeNgoaiTuyen.sql: chua co luot nao duoc theo doi -> danh sach rong (khong 500)
                return new List<LuotCanDoiSoatDto>();
            }
        }

        /// <summary>Ghi kết luận cho một lượt cần đối soát (đánh dấu đã xử lý).</summary>
        [HttpPost("can-doi-soat/{maThaoTac}/da-xu-ly")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> XuLyCanDoiSoat(string maThaoTac, [FromBody] YeuCauXuLyDoiSoatDto yc, CancellationToken ct)
        {
            var ghiChu = (yc?.GhiChu ?? "").Trim();
            if (ghiChu.Length == 0) return BadRequest(new ThongBaoDto { ThongBao = "Nhập kết luận đối soát." });
            var ma = (maThaoTac ?? "").Trim();
            var nguoi = Cat(User.HoTen() ?? User.Ten(), 100);
            var luc = DateTime.Now;
            var noiDung = Cat(ghiChu, 500);
            int soDong;
            try
            {
                // KHONG tu doi soat: nguoi da cho xe qua (khai luc ghi) / nguoi dong bo luot do khong duoc tu dong muc cua minh
                var muc = await db.LuotXeNgoaiTuyens.AsNoTracking().Where(x => x.MaThaoTac == ma)
                    .Select(x => new { x.NguoiThaoTac, x.NguoiDongBo }).FirstOrDefaultAsync(ct);
                if (muc != null && nguoi != null
                    && (string.Equals(muc.NguoiDongBo?.Trim(), nguoi.Trim(), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(muc.NguoiThaoTac?.Trim(), nguoi.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ThongBaoDto { ThongBao = "Không tự đối soát lượt do chính mình ghi / đồng bộ — nhờ quản trị viên khác." });
                soDong = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.LuotXeNgoaiTuyen SET DaDoiSoat = 1, DoiSoatLuc = {luc}, NguoiDoiSoat = {nguoi}, GhiChuDoiSoat = {noiDung}
WHERE MaThaoTac = {ma} AND TrangThai = N'can-doi-soat' AND DaDoiSoat = 0", ct);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new ThongBaoDto { ThongBao = "CSDL chưa có bảng LuotXeNgoaiTuyen (chạy Database/Add_LuotXeNgoaiTuyen.sql)." });
            }
            if (soDong == 0) return NotFound(new ThongBaoDto { ThongBao = "Không có lượt cần đối soát (chưa xử lý) với mã " + ma + "." });
            NhatKyHelper.Ghi("ThaoTac", User.Ten(), "Đối soát lượt ghi trễ " + ma + ": " + noiDung, HttpContext);
            return Ok(new ThongBaoDto { ThongBao = "Đã ghi kết luận đối soát." });
        }

        // ------------------------------------------------------------------ dong bo: dung chung cho dong-bo va dong-bo-lo

        private sealed class MucCanGhi
        {
            public int ViTri;
            public KetQuaMucDongBoCongDto KetQua;
            public YeuCauGhiLuotXe YeuCau;
        }

        /// <summary>
        /// Ghi tung muc THEO THU TU XAY RA (khong theo thu tu trong lo): "Vào" truoc "Ra" thi lenh moi chuyen dung
        /// "Đã vào cổng" -> "Hoàn tất" va Bai thay container vao / ra dung trinh tu. Moi muc mot pham vi DI rieng
        /// (DbContext + hop thu di moi) va mot giao dich rieng: muc hong khong de lai thay doi do dang cho muc sau.
        /// </summary>
        private async Task GhiTheoThuTuAsync(List<MucCanGhi> ds, CancellationToken ct)
        {
            foreach (var m in ds.OrderBy(x => x.YeuCau.ThoiGian).ThenBy(x => x.ViTri))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var scope = phamVi.CreateScope();
                    var kq = await scope.ServiceProvider.GetRequiredService<ICongLenh>().GhiLuotXeAsync(m.YeuCau, ct);
                    GanKetQua(m.KetQua, kq);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;      // tram ngat ket noi: muc da commit van dung, muc chua lam se thanh "trung" / duoc ghi o lan gui sau
                }
                catch (Exception ex)
                {
                    DatLoi(m.KetQua, "Lỗi ghi lượt " + m.YeuCau.BienSo + ": " + Cat(ex.GetBaseException().Message, 300),
                           PhanLoaiLoiCong.LaTamThoi(ex));
                }
            }
        }

        private static void GanKetQua(KetQuaMucDongBoCongDto r, KetQuaGhiLuotXe kq)
        {
            if (!kq.Ok)
            {
                DatLoi(r, kq.ThongBao, kq.LoiTamThoi);
                return;
            }
            r.MaXV = kq.MaXV > 0 ? kq.MaXV : (int?)null;
            r.ThoiGian = kq.ThoiGian == default ? (DateTime?)null : kq.ThoiGian;
            r.LyDoDoiSoat = kq.LyDoDoiSoat;
            r.TrangThai = kq.Trung ? TrangThaiDongBo.Trung : kq.CanDoiSoat ? TrangThaiDongBo.CanDoiSoat : TrangThaiDongBo.DaLuu;
            r.ThongBao = kq.Trung ? kq.ThongBao : string.IsNullOrWhiteSpace(kq.CanhBao) ? null : kq.CanhBao;
            r.ThuLai = false;
        }

        private static void DatLoi(KetQuaMucDongBoCongDto r, string thongBao, bool thuLai)
        {
            r.TrangThai = TrangThaiDongBo.Loi;
            r.ThongBao = thongBao;
            r.ThuLai = thuLai;
            r.MaXV = null;
        }

        /// <summary>Kiem tra mot muc cua lo (khong de mot muc sai lam hong ca lo)</summary>
        private static string KiemTraMuc(MucDongBoCongDto it, out string huong)
        {
            huong = null;
            if (it == null) return "Mục rỗng.";
            if (string.IsNullOrWhiteSpace(it.MaThaoTac)) return "Thiếu maThaoTac (khóa chống gửi lặp, bắt buộc).";
            if (it.MaThaoTac.Trim().Length > 64) return "maThaoTac tối đa 64 ký tự.";
            if (string.IsNullOrWhiteSpace(it.BienSo)) return "Thiếu biển số.";
            huong = ChuanHuong(it.HuongDi);
            if (huong == null) return "Hướng đi phải là \"Vào\" hoặc \"Ra\".";
            return null;
        }

        /// <summary>"Vào" / "Ra" (chap nhan "vao", "VAO", "ra" — tram go tay / ban phim khong dau)</summary>
        private static string ChuanHuong(string s)
        {
            var h = (s ?? "").Trim();
            if (h.Equals("Ra", StringComparison.OrdinalIgnoreCase)) return "Ra";
            if (h.Equals("Vào", StringComparison.OrdinalIgnoreCase) || h.Equals("Vao", StringComparison.OrdinalIgnoreCase)) return "Vào";
            return null;
        }

        private static bool LaNguonDongBo(string nguon)
        {
            var n = (nguon ?? "").Trim();
            return n.Equals(XeDaQuaCong.NguonNhapBu, StringComparison.OrdinalIgnoreCase)
                   || n.Equals(XeDaQuaCong.NguonNgoaiTuyen, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Khoa chong gui lap cho lo client CU (khong tu sinh khoa): bam (bien, huong, cong, gio may ghi). Client cu gui lai
        /// cung lo -> cung khoa -> "trung"; 2 tab gui song song -> khoa chinh chan. Khong co gio -> khong suy ra duoc (chi con
        /// cua so 90 giay).
        /// </summary>
        private static string KhoaSuyRa(string bienSo, string huong, string cong, DateTimeOffset? luc)
        {
            if (luc == null) return null;
            var nguon = bienSo + "|" + huong + "|" + (cong ?? "").Trim().ToUpperInvariant() + "|" + luc.Value.UtcTicks;
            var bam = SHA256.HashData(Encoding.UTF8.GetBytes(nguon));
            return "lo-" + Convert.ToHexString(bam).Substring(0, 40).ToLowerInvariant();
        }

        // ------------------------------------------------------------------ goi ngoai tuyen

        private GoiNgoaiTuyenDto TaoGoiNgoaiTuyen(string cong)
        {
            var bayGio = DateTime.Now;
            var tu = bayGio.AddHours(-2);           // noi long 2 tieng sau khung hen, cung luat voi doi chieu lenh
            var den = bayGio.AddHours(24);
            var goi = new GoiNgoaiTuyenDto { Cong = cong };

            // Uu tien lenh TRONG khung gio (cho xe / qua han) truoc, roi xe "Da vao cong" vao trong 48 gio (cu hon la ton dong /
            // quen dong lenh — truoc day chiem het 500 cho, lenh 24 gio toi bi bo, tram mat mang bao "khong co lenh").
            var vaoTu = bayGio.AddHours(-48);
            var qLenh = db.LenhGiaoNhans.AsNoTracking()
                .Where(l => ((l.TrangThai == "Chờ xe" || l.TrangThai == "Quá hạn") && l.HenDenLuc > tu && l.HenTuLuc < den)
                            || (l.TrangThai == "Đã vào cổng" && (l.VaoCongLuc == null || l.VaoCongLuc > vaoTu)));
            goi.LenhMo = qLenh
                .OrderBy(l => l.TrangThai == "Đã vào cổng" ? 1 : 0).ThenBy(l => l.HenTuLuc).ThenBy(l => l.MaLenh)
                .Take(ToiDaMoiLoai)
                .Select(l => new LenhMoNgoaiTuyenDto
                {
                    MaLenh = l.MaLenh,
                    SoLenh = l.SoLenh,
                    LoaiLenh = l.LoaiLenh,
                    BienSo = l.BienSoXe,
                    SoCont = l.SoCont,
                    TrangThai = l.TrangThai,
                    HetHan = l.HenDenLuc,
                    HenTuLuc = l.HenTuLuc
                }).ToList();

            var dsCont = goi.LenhMo.Where(l => !string.IsNullOrWhiteSpace(l.SoCont)).Select(l => l.SoCont).Distinct().ToList();
            if (dsCont.Count > 0)
            {
                // eDO / to khai la BAN SAO (thuoc dich vu eDO / Hai quan); ban gop chua cai bang Cang so -> bo qua, khong 500
                try
                {
                    goi.Edo = db.LenhGiaoHangDienTus.AsNoTracking().Where(e => dsCont.Contains(e.SoCont))
                        .Select(e => new { e.MaEDO, e.SoCont, e.SoEDO, e.TrangThai, e.HanLenh, e.NgayPhatHanh }).ToList()
                        .GroupBy(e => e.SoCont)
                        .Select(g => g.OrderByDescending(e => e.NgayPhatHanh).ThenByDescending(e => e.MaEDO).First())
                        .OrderBy(e => e.SoCont).Take(ToiDaMoiLoai)
                        .Select(e => new EdoNgoaiTuyenDto { SoCont = e.SoCont, SoEdo = e.SoEDO, TrangThai = e.TrangThai, HieuLucDen = e.HanLenh })
                        .ToList();
                }
                catch (SqlException ex) when (ex.Number == 208) { }
                try
                {
                    goi.HaiQuan = db.ToKhaiHaiQuans.AsNoTracking().Where(t => dsCont.Contains(t.SoCont) && t.HuongHang == "Nhập")
                        .Select(t => new { t.MaToKhai, t.SoCont, t.TrangThai, t.NgayKhai }).ToList()
                        .GroupBy(t => t.SoCont)
                        .Select(g => g.OrderByDescending(t => t.NgayKhai).ThenByDescending(t => t.MaToKhai).First())
                        .OrderBy(t => t.SoCont).Take(ToiDaMoiLoai)
                        .Select(t => new HaiQuanNgoaiTuyenDto
                        {
                            SoCont = t.SoCont,
                            TrangThai = t.TrangThai,
                            DaThongQuan = t.TrangThai == HaiQuanService.DaThongQuan || t.TrangThai == HaiQuanService.DaQuaGiamSat
                        }).ToList();
                }
                catch (SqlException ex) when (ex.Number == 208) { }
            }

            goi.BiCat = goi.LenhMo.Count >= ToiDaMoiLoai && qLenh.Count() > ToiDaMoiLoai;

            // The xe: uu tien the cua xe dang co lenh mo (tram can nhat), roi the moi nhat; vuot gioi han -> bao bi cat
            var homNay = DateTime.Today;
            var bienLenh = goi.LenhMo.Where(l => !string.IsNullOrWhiteSpace(l.BienSo)).Select(l => l.BienSo).Distinct().ToList();
            goi.TheXe = db.TheXes.AsNoTracking().Where(t => t.KichHoat)
                .OrderBy(t => bienLenh.Contains(t.BienSo) ? 0 : 1).ThenByDescending(t => t.MaThe).Take(ToiDaTheXe)
                .Select(t => new { t.BienSo, t.LoaiThe, t.HanDen, t.SoDu }).ToList()
                .Select(t => new TheXeNgoaiTuyenDto
                {
                    BienSo = t.BienSo,
                    LoaiThe = t.LoaiThe,
                    // Cung luat TheXe.ConDung: the thang xet han, the tra truoc xet so du
                    ConHan = t.LoaiThe == "Tháng" ? t.HanDen != null && t.HanDen.Value.Date >= homNay : t.SoDu > 0
                })
                .OrderBy(t => t.BienSo, StringComparer.Ordinal).ToList();

            if (goi.TheXe.Count >= ToiDaTheXe && db.TheXes.Count(t => t.KichHoat) > ToiDaTheXe) goi.BiCat = true;

            goi.BieuGiaPhiCong = db.BieuPhiCongs.AsNoTracking().Where(b => b.KichHoat)
                .OrderBy(b => b.LoaiXe).ThenBy(b => b.MaBP).Take(ToiDaMoiLoai)
                .Select(b => new GiaPhiCongNgoaiTuyenDto { LoaiXe = b.LoaiXe, Gia = b.PhiLuot }).ToList();

            goi.BienCam = TuanThuCongService.DanhSachBienCam().Take(ToiDaMoiLoai).ToList();

            goi.CheDoKiemTra = new CheDoKiemTraNgoaiTuyenDto
            {
                Edo = TuanThuCongService.CheDo("KiemTraEdo"),
                HaiQuan = TuanThuCongService.CheDo("KiemTraHaiQuan")
            };
            return goi;
        }

        private static string BamNoiDung(GoiNgoaiTuyenDto goi)
        {
            var phienBan = goi.PhienBan;
            var gio = goi.GioMayChu;
            goi.PhienBan = null;
            goi.GioMayChu = default;
            var json = JsonSerializer.SerializeToUtf8Bytes(goi);
            goi.PhienBan = phienBan;
            goi.GioMayChu = gio;
            return Convert.ToHexString(SHA256.HashData(json)).Substring(0, 32).ToLowerInvariant();
        }

        /// <summary>If-None-Match: "a", W/"a", danh sach cach nhau dau phay, hoac *</summary>
        private static bool KhopIfNoneMatch(string tieuDe, string phienBan)
        {
            if (string.IsNullOrWhiteSpace(tieuDe)) return false;
            foreach (var phan in tieuDe.Split(','))
            {
                var t = phan.Trim();
                if (t == "*") return true;
                if (t.StartsWith("W/", StringComparison.Ordinal)) t = t.Substring(2);
                if (t.Trim('"') == phienBan) return true;
            }
            return false;
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n);
    }

    /// <summary>Lô lượt xe ghi tạm trong trình duyệt lúc mất mạng (client CŨ)</summary>
    public class LoLuotXeOfflineDto
    {
        public List<LuotXeOfflineDto> Items { get; set; }
    }

    public class LuotXeOfflineDto
    {
        public string BienSo { get; set; }
        /// <summary>"Vào" hoặc "Ra"</summary>
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public string LoaiXe { get; set; }
        public string GhiChu { get; set; }
        /// <summary>Thời điểm xe qua cổng thật (lúc bấm ghi trên máy)</summary>
        public DateTime? Luc { get; set; }

        /// <summary>(tùy chọn) Khóa chống gửi lặp do máy sinh; bỏ trống thì máy chủ suy ra từ biển + hướng + cổng + giờ</summary>
        public string MaThaoTac { get; set; }

        /// <summary>(tùy chọn) Mã lệnh đã đối chiếu — bỏ trống thì máy chủ tự đối chiếu theo biển + giờ</summary>
        public int? MaLenh { get; set; }

        /// <summary>(tùy chọn) Số container trạm ghi nhận</summary>
        public string SoCont { get; set; }

        /// <summary>(tùy chọn) Tiền mặt đã thu tại cổng + số phiếu thu tay</summary>
        public decimal? TienMatTaiCong { get; set; }

        public string SoPhieuThu { get; set; }
    }

    public class KetQuaDongBoLoDto
    {
        /// <summary>Số lượt đã ghi (kể cả lượt cần đối soát)</summary>
        public int DaLuu { get; set; }
        /// <summary>Số bản trùng / dữ liệu sai đã bỏ qua</summary>
        public int BoQua { get; set; }
        /// <summary>Số lượt đã ghi nhưng CẦN ĐỐI SOÁT (vi phạm tuân thủ / xung đột lệnh)</summary>
        public int CanDoiSoat { get; set; }
        /// <summary>Số lượt lỗi (tạm thời + dữ liệu sai)</summary>
        public int Loi { get; set; }
        public string ThongBao { get; set; }
        /// <summary>Kết quả từng lượt, cùng thứ tự gửi lên</summary>
        public List<KetQuaMucDongBoCongDto> KetQua { get; set; }
    }
}
