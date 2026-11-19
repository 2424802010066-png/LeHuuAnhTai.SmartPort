using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Contracts.AnNinh;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.XuLySuCo
{
    /// <summary>
    /// XỬ LÝ SỰ CỐ CHUNG (UC22 → UC28) — bảng SuCo, mọi vai trò dùng:
    ///   UC22 Báo cáo sự cố: khách hàng (hàng hóa · thanh toán · dịch vụ), nhân viên (mọi loại), camera AI (chuyển từ SuCoAnNinh).
    ///   UC23 An ninh, an toàn · UC24 Hàng hóa, thiết bị (tiếp nhận, tạm giữ container UC24.2, báo khách UC24.3)
    ///   UC25 Thanh toán lỗi (đối soát / hoàn tiền ở api/doi-soat-thanh-toan) · UC26 Phản ánh, khiếu nại
    ///   UC27 Sự cố hệ thống (theo dõi ở api/he-thong) · UC28 Thống kê sự cố.
    /// Vòng đời: Moi → DangXuLy (tiếp nhận) → DaXuLy (ghi kết quả) → Dong; hoặc Moi/DangXuLy → BaoGia.
    /// QT14: NguyHiem → báo động toàn cảng ngay khi ghi (sự kiện Outbox → SSE → BaoDongToanCuc), tiếp nhận trong 5 phút.
    /// QT15: tạm giữ container → cổng không giao ra (TuanThuCongService.LyDoTamGiu, gọi từ module Cổng).
    /// QT17: KHÔNG có API xóa; chỉ đóng khi đã có kết quả; phản ánh của khách phải có kết quả trong 24 giờ.
    /// Mọi đổi trạng thái là cập nhật CÓ ĐIỀU KIỆN (WHERE TrangThai = ...) — hai người bấm cùng lúc thì chỉ một người được.
    /// </summary>
    [Route("api/su-co")]
    [Authorize]
    public class SuCoController : ApiGocController
    {
        private const string NHAN_VIEN = "Admin,NhanVien";
        private const string MOI_VAI_TRO = "Admin,NhanVien,KhachHang";
        private const string VT_KHACH = "KhachHang";
        private const int ANH_BASE64_TOI_DA = 2_800_000;   // ~2 MB anh (giong Phong an ninh)
        private const string TIEN_TO_BAO_GIA = "Báo giả: ";

        private readonly AppDb db;

        public SuCoController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Ma tai khoan dang goi API (claim "sub" cua JWT)</summary>
        private int? MaTK
        {
            get
            {
                var c = User.FindFirst("sub");
                return c != null && int.TryParse(c.Value, out var ma) ? ma : (int?)null;
            }
        }

        private bool LaKhach => User.IsInRole(VT_KHACH);
        private bool LaNhanVien => User.IsInRole("Admin") || User.IsInRole("NhanVien");

        // HIEU NANG: danh sach KHONG keo cot AnhBase64 (co the ~2 MB) — chi co "co anh"; anh tai qua GET su-co/{id}
        private static readonly Expression<Func<SuCo, SuCoDongDto>> ChieuDong = s => new SuCoDongDto
        {
            MaSuCo = s.MaSuCo,
            Loai = s.Loai,
            MucDo = s.MucDo,
            TieuDe = s.TieuDe,
            MoTa = s.MoTa,
            ViTri = s.ViTri,
            SoContainer = s.SoContainer,
            MaLenh = s.MaLenh,
            SoLenh = s.Lenh != null ? s.Lenh.SoLenh : null,
            CoAnh = s.AnhBase64 != null && s.AnhBase64 != "",
            MaTKBao = s.MaTKBao,
            NguoiBao = s.NguoiBao.HoTen,
            VaiTroNguoiBao = s.NguoiBao.VaiTro,
            KhachHang = s.NguoiBao.VaiTro == VT_KHACH && s.NguoiBao.KhachHang != null ? s.NguoiBao.KhachHang.TenCongTy
                      : s.Lenh != null && s.Lenh.KhachHang != null ? s.Lenh.KhachHang.TenCongTy : null,
            MaTKXuLy = s.MaTKXuLy,
            NguoiXuLy = s.NguoiXuLy != null ? s.NguoiXuLy.HoTen : null,
            TrangThai = s.TrangThai,
            TamGiuContainer = s.TamGiuContainer,
            KetQua = s.KetQua,
            ThoiDiemBao = s.ThoiDiemBao,
            ThoiDiemTiepNhan = s.ThoiDiemTiepNhan,
            ThoiDiemDong = s.ThoiDiemDong,
            MaSCAnNinh = s.MaSCAnNinh
        };

        // =====================================================================
        // UC22 — BAO CAO SU CO (moi nguoi dung)
        // =====================================================================

        /// <summary>
        /// Báo sự cố / gửi phản ánh. Bắt buộc: loai, mucDo, tieuDe, moTa; tùy chọn: viTri, soContainer, maLenh, anhBase64 (≤ ~2 MB),
        /// maSCAnNinh (nhân viên chuyển cảnh báo camera AI). Khách hàng chỉ gửi loại HangHoaThietBi / ThanhToan / DichVu và chỉ
        /// đính kèm container / lệnh của chính doanh nghiệp mình. QT14: mức NguyHiem → báo động toàn cảng ngay (sự kiện realtime).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = MOI_VAI_TRO)]
        [RequestSizeLimit(4_000_000)]
        [ChongGuiLap]   // mang chap chon / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong ghi 2 su co
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult BaoSuCo([FromBody] TaoSuCoDto yc)
        {
            var maTK = MaTK;
            if (maTK == null) return Loi("Không xác định được tài khoản đăng nhập.");

            var loai = (yc.Loai ?? "").Trim();
            if (!SuCo.CAC_LOAI.Contains(loai)) return Loi("Loại sự cố không hợp lệ (" + string.Join(", ", SuCo.CAC_LOAI) + ").");
            if (LaKhach && !SuCo.LOAI_CUA_KHACH.Contains(loai))
                return Loi("Khách hàng chỉ phản ánh được: hàng hóa – thiết bị, thanh toán – hóa đơn, dịch vụ.");
            var mucDo = (yc.MucDo ?? "").Trim();
            if (!SuCo.CAC_MUC_DO.Contains(mucDo)) return Loi("Mức độ không hợp lệ (Thap, TrungBinh, NguyHiem).");
            var tieuDe = (yc.TieuDe ?? "").Trim();
            var moTa = (yc.MoTa ?? "").Trim();
            if (tieuDe.Length == 0) return Loi("Nhập tiêu đề sự cố.");
            if (moTa.Length == 0) return Loi("Nhập mô tả sự cố.");
            var viTri = string.IsNullOrWhiteSpace(yc.ViTri) ? null : Cat(yc.ViTri.Trim(), 100);
            var soCont = ChuanCont(yc.SoContainer);
            if (soCont != null && soCont.Length > 15) return Loi("Số container tối đa 15 ký tự.");

            string anh = null;
            if (!string.IsNullOrEmpty(yc.AnhBase64))
            {
                if (yc.AnhBase64.Length > ANH_BASE64_TOI_DA || !yc.AnhBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    return Loi("Ảnh hiện trường phải là ảnh (JPG/PNG) và không quá 2 MB.");
                anh = yc.AnhBase64;
            }

            LenhGiaoNhan lenh = null;
            if (yc.MaLenh != null)
            {
                lenh = db.LenhGiaoNhans.AsNoTracking().FirstOrDefault(l => l.MaLenh == yc.MaLenh.Value);
                if (lenh == null) return Loi("Không tìm thấy lệnh giao nhận #" + yc.MaLenh + ".");
            }

            // Khach hang: chi dinh kem container / lenh CUA CHINH DOANH NGHIEP MINH (khong do duoc hang cua khach khac)
            if (LaKhach && (lenh != null || soCont != null))
            {
                var maKH = MaKhachHang;
                if (maKH == null) return Loi("Tài khoản chưa gắn hồ sơ doanh nghiệp — không đính kèm được container / lệnh.");
                if (lenh != null && lenh.MaKH != maKH)
                    return Loi("Lệnh " + TenLenh(lenh) + " không thuộc doanh nghiệp của bạn — chỉ đính kèm được container / lệnh của chính mình.");
                if (soCont != null && (lenh == null || !CungCont(lenh.SoCont, soCont)))
                {
                    var cuaMinh = db.Containers.AsNoTracking().Any(c => c.SoCont == soCont && c.MaKH == maKH)
                               || db.LenhGiaoNhans.AsNoTracking().Any(l => l.SoCont == soCont && l.MaKH == maKH);
                    if (!cuaMinh)
                        return Loi("Container " + soCont + " không thuộc doanh nghiệp của bạn — chỉ đính kèm được container / lệnh của chính mình.");
                }
            }
            if (lenh != null && soCont != null && !string.IsNullOrWhiteSpace(lenh.SoCont) && !CungCont(lenh.SoCont, soCont))
                return Loi("Container " + soCont + " không khớp container của lệnh " + TenLenh(lenh) + " (" + lenh.SoCont + ").");
            if (soCont == null && lenh != null) soCont = ChuanCont(lenh.SoCont);

            // UC23: camera AI -> nhan vien chuyen canh bao (SuCoAnNinh) thanh su co de theo doi toi khi dong
            int? maSCAnNinh = null;
            if (yc.MaSCAnNinh != null)
            {
                if (!LaNhanVien) return Loi("Chỉ nhân viên mới chuyển được cảnh báo camera AI thành sự cố.");
                if (!db.SuCoAnNinhs.AsNoTracking().Any(s => s.MaSC == yc.MaSCAnNinh.Value))
                    return Loi("Không tìm thấy cảnh báo camera AI #" + yc.MaSCAnNinh + ".");
                var daChuyen = db.SuCos.AsNoTracking().Where(s => s.MaSCAnNinh == yc.MaSCAnNinh.Value).Select(s => (int?)s.MaSuCo).FirstOrDefault();
                if (daChuyen != null) return Loi("Cảnh báo camera AI #" + yc.MaSCAnNinh + " đã được chuyển thành sự cố #" + daChuyen + ".");
                maSCAnNinh = yc.MaSCAnNinh;
                // Khong gui anh rieng -> dung anh bang chung camera AI da chup
                if (anh == null)
                {
                    var anhAi = db.SuCoAnNinhs.AsNoTracking().Where(s => s.MaSC == yc.MaSCAnNinh.Value).Select(s => s.AnhBase64).FirstOrDefault();
                    if (!string.IsNullOrEmpty(anhAi) && anhAi.Length <= ANH_BASE64_TOI_DA && anhAi.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                        anh = anhAi;
                }
            }

            var nguoiBao = !string.IsNullOrWhiteSpace(User.HoTen()) ? User.HoTen() : NguoiDung;
            var nguyHiem = mucDo == SuCo.MUC_NGUY_HIEM;
            var sc = new SuCo
            {
                Loai = loai,
                MucDo = mucDo,
                TieuDe = Cat(tieuDe, 150),
                MoTa = Cat(moTa, 1000),
                ViTri = viTri,
                SoContainer = soCont,
                MaLenh = lenh?.MaLenh,
                AnhBase64 = anh,
                MaTKBao = maTK.Value,
                TrangThai = SuCo.TT_MOI,
                TamGiuContainer = false,
                ThoiDiemBao = DateTime.Now,
                MaSCAnNinh = maSCAnNinh
            };
            // HUONG SU KIEN (giong Phong an ninh): ghi su co + su kien vao Outbox trong CUNG giao dich -> dich vu Thong bao day
            // realtime toi moi man hinh nhan vien; QT14: muc NguyHiem -> BaoDongToanCuc bat bang do + chuong o MOI trang
            using (var gd = db.Database.BeginTransaction())
            {
                db.SuCos.Add(sc);
                db.SaveChanges();
                HopThuDiTinh.Them(db, new SuCoChungDaBao
                {
                    MaSuCo = sc.MaSuCo,
                    LoaiSuCo = sc.Loai,
                    MucDo = sc.MucDo,
                    TieuDe = sc.TieuDe,
                    ViTri = sc.ViTri,
                    SoContainer = sc.SoContainer,
                    NguoiBao = nguoiBao,
                    LaKhachHang = LaKhach
                });
                db.SaveChanges();
                gd.Commit();
            }

            GhiNhatKy((LaKhach ? "Khách gửi phản ánh #" : "Báo sự cố #") + sc.MaSuCo + " (" + SuCo.TenLoaiCua(loai) + " · "
                      + SuCo.TenMucDoCua(mucDo) + "): " + Cat(sc.TieuDe, 120));
            var thongBao = LaKhach
                ? "Đã gửi phản ánh #" + sc.MaSuCo + ". SmartPort sẽ trả lời trong " + SuCo.GIO_TRA_LOI_KHACH + " giờ."
                : nguyHiem
                    ? "Đã ghi sự cố #" + sc.MaSuCo + " và phát BÁO ĐỘNG toàn cảng — an ninh phải tiếp nhận trong " + SuCo.PHUT_TIEP_NHAN_NGUY_HIEM + " phút."
                    : "Đã ghi sự cố #" + sc.MaSuCo + ".";
            return XongViec(thongBao, sc.MaSuCo, new KetQuaTaoSuCoDto
            {
                BaoDongToanCang = nguyHiem,
                HanTiepNhan = nguyHiem ? sc.ThoiDiemBao.AddMinutes(SuCo.PHUT_TIEP_NHAN_NGUY_HIEM) : (DateTime?)null,
                HanTraLoi = LaKhach ? sc.ThoiDiemBao.AddHours(SuCo.GIO_TRA_LOI_KHACH) : (DateTime?)null
            });
        }

        /// <summary>
        /// Phản ánh / sự cố do chính tôi gửi (mới nhất trước, tối đa 100) kèm trạng thái và kết quả trả lời; khách hàng còn thấy
        /// thêm sự cố liên quan container / lệnh của doanh nghiệp mình do cảng ghi nhận (UC24.3 — vd container bị tạm giữ).
        /// </summary>
        [HttpGet("cua-toi")]
        [Authorize(Roles = MOI_VAI_TRO)]
        public ActionResult<SuCoCuaToiDto> CuaToi()
        {
            var maTK = MaTK;
            if (maTK == null) return Loi("Không xác định được tài khoản đăng nhập.");
            var cuaToi = db.SuCos.AsNoTracking().Where(s => s.MaTKBao == maTK.Value)
                .OrderByDescending(s => s.ThoiDiemBao).ThenByDescending(s => s.MaSuCo)
                .Take(100).Select(ChieuDong).ToList();

            var lienQuan = new List<SuCoDongDto>();
            var maKH = LaKhach ? MaKhachHang : null;
            if (maKH != null)
            {
                var kh = maKH.Value;
                lienQuan = db.SuCos.AsNoTracking()
                    .Where(s => s.MaTKBao != maTK.Value && s.TrangThai != SuCo.TT_BAO_GIA
                                && ((s.MaLenh != null && s.Lenh.MaKH == kh)
                                    || (s.SoContainer != null && db.Containers.Any(c => c.SoCont == s.SoContainer && c.MaKH == kh))))
                    .OrderByDescending(s => s.ThoiDiemBao).ThenByDescending(s => s.MaSuCo)
                    .Take(50).Select(ChieuDong).ToList();
            }
            HoanThien(cuaToi);
            HoanThien(lienQuan);
            return new SuCoCuaToiDto
            {
                CuaToi = cuaToi,
                LienQuan = lienQuan,
                SoDangMo = cuaToi.Count(x => SuCo.ConMo(x.TrangThai) && string.IsNullOrEmpty(x.KetQua)),
                SoDaTraLoi = cuaToi.Count(x => !string.IsNullOrEmpty(x.KetQua))
            };
        }

        /// <summary>Container và lệnh giao nhận của doanh nghiệp tôi — để khách chọn đính kèm khi phản ánh.</summary>
        [HttpGet("doi-tuong-cua-toi")]
        [Authorize(Roles = VT_KHACH)]
        public ActionResult<DoiTuongSuCoCuaToiDto> DoiTuongCuaToi()
        {
            var kq = new DoiTuongSuCoCuaToiDto();
            var maKH = MaKhachHang;
            if (maKH == null) return kq;
            var kh = maKH.Value;
            kq.Container = db.Containers.AsNoTracking().Where(c => c.MaKH == kh)
                .OrderByDescending(c => c.NgayVaoBai).ThenBy(c => c.SoCont).Take(100)
                .Select(c => new ContainerChonSuCoDto { SoCont = c.SoCont, LoaiCont = c.LoaiCont, TrangThai = c.TrangThai })
                .ToList();
            var lenh = db.LenhGiaoNhans.AsNoTracking().Where(l => l.MaKH == kh)
                .OrderByDescending(l => l.MaLenh).Take(50).ToList();
            kq.Lenh = lenh.Select(l => new LenhChonSuCoDto
            {
                MaLenh = l.MaLenh,
                SoLenh = l.SoLenh,
                TenLoaiLenh = l.TenLoaiLenh,
                SoCont = l.SoCont,
                BienSoXe = l.BienSoXe,
                TrangThai = l.TrangThai
            }).ToList();
            return kq;
        }

        // =====================================================================
        // NHAN VIEN — DANH SACH, CHI TIET (UC23 → UC27)
        // =====================================================================

        /// <summary>
        /// Danh sách sự cố cho nhân viên, mới nhất trước. Lọc: loai, trangThai, mucDo, tuKhoa (mã #, tiêu đề, mô tả, container,
        /// vị trí, người báo, kết quả), tuNgay / denNgay (ngày báo), chiDangMo (chưa Dong / BaoGia), tamGiu (đang tạm giữ container).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = NHAN_VIEN)]
        public ActionResult<TrangDto<SuCoDongDto>> DanhSach([FromQuery] string loai, [FromQuery] string trangThai, [FromQuery] string mucDo,
            [FromQuery] string tuKhoa, [FromQuery] DateTime? tuNgay, [FromQuery] DateTime? denNgay,
            [FromQuery] bool chiDangMo = false, [FromQuery] bool tamGiu = false, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var q = db.SuCos.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(loai))
            {
                if (!SuCo.CAC_LOAI.Contains(loai.Trim())) return Loi("Loại sự cố không hợp lệ.");
                var l = loai.Trim();
                q = q.Where(s => s.Loai == l);
            }
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                if (!SuCo.CAC_TRANG_THAI.Contains(trangThai.Trim())) return Loi("Trạng thái không hợp lệ.");
                var tt = trangThai.Trim();
                q = q.Where(s => s.TrangThai == tt);
            }
            if (!string.IsNullOrWhiteSpace(mucDo))
            {
                if (!SuCo.CAC_MUC_DO.Contains(mucDo.Trim())) return Loi("Mức độ không hợp lệ.");
                var m = mucDo.Trim();
                q = q.Where(s => s.MucDo == m);
            }
            if (chiDangMo) q = q.Where(s => s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA);
            if (tamGiu) q = q.Where(s => s.TamGiuContainer && s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA);
            if (tuNgay != null && denNgay != null && tuNgay.Value.Date > denNgay.Value.Date) return Loi("Từ ngày phải trước hoặc bằng đến ngày.");
            if (tuNgay != null)
            {
                var tu = tuNgay.Value.Date;
                q = q.Where(s => s.ThoiDiemBao >= tu);
            }
            if (denNgay != null)
            {
                var den = denNgay.Value.Date.AddDays(1);
                q = q.Where(s => s.ThoiDiemBao < den);
            }
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var k = tuKhoa.Trim();
                var coMa = int.TryParse(k.TrimStart('#'), out var ma);
                q = q.Where(s => (coMa && s.MaSuCo == ma)
                                 || s.TieuDe.Contains(k) || s.MoTa.Contains(k) || s.SoContainer.Contains(k) || s.ViTri.Contains(k)
                                 || s.NguoiBao.HoTen.Contains(k) || s.KetQua.Contains(k) || (s.Lenh != null && s.Lenh.SoLenh.Contains(k)));
            }

            var kq = q.OrderByDescending(s => s.ThoiDiemBao).ThenByDescending(s => s.MaSuCo)
                .Select(ChieuDong)
                .LayTrang(trang, coTrang);
            HoanThien(kq.Items);
            return kq;
        }

        /// <summary>Chi tiết một sự cố kèm ảnh hiện trường — nhân viên, hoặc chính người đã báo.</summary>
        [HttpGet("{id:int}")]
        [Authorize(Roles = MOI_VAI_TRO)]
        [ProducesResponseType(typeof(SuCoChiTietDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<SuCoChiTietDto> ChiTiet(int id)
        {
            var dong = db.SuCos.AsNoTracking().Where(s => s.MaSuCo == id).Select(ChieuDong).FirstOrDefault();
            if (dong == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (!LaNhanVien && dong.MaTKBao != MaTK)
                return StatusCode(StatusCodes.Status403Forbidden, new ThongBaoDto { ThongBao = "Bạn chỉ xem được sự cố / phản ánh do chính mình gửi." });

            HoanThien(new List<SuCoDongDto> { dong });
            var ct = SuCoChiTietDto.Tu(dong);
            ct.AnhBase64 = db.SuCos.AsNoTracking().Where(s => s.MaSuCo == id).Select(s => s.AnhBase64).FirstOrDefault();
            if (dong.MaSCAnNinh != null)
            {
                var ai = db.SuCoAnNinhs.AsNoTracking().Where(s => s.MaSC == dong.MaSCAnNinh.Value)
                    .Select(s => new { s.Loai, s.ViTri, s.MoTa, s.ThoiGian }).FirstOrDefault();
                if (ai != null)
                    ct.CanhBaoCameraAi = SuCoAnNinh.TenLoaiCua(ai.Loai) + " · " + ai.ViTri + " · " + ai.ThoiGian.ToString("HH:mm dd/MM/yyyy")
                                         + (string.IsNullOrWhiteSpace(ai.MoTa) ? "" : " — " + ai.MoTa);
            }
            return ct;
        }

        /// <summary>Nhân viên có thể nhận xử lý sự cố (Admin / NhanVien đang hoạt động) — cho ô chọn khi tiếp nhận.</summary>
        [HttpGet("nguoi-xu-ly")]
        [Authorize(Roles = NHAN_VIEN)]
        public ActionResult<List<NhanVienXuLySuCoDto>> NguoiXuLy()
        {
            return db.TaiKhoans.AsNoTracking()
                .Where(t => t.KichHoat && (t.VaiTro == "Admin" || t.VaiTro == "NhanVien"))
                .OrderBy(t => t.HoTen)
                .Select(t => new NhanVienXuLySuCoDto { MaTK = t.MaTK, HoTen = t.HoTen, VaiTro = t.VaiTro })
                .ToList();
        }

        // =====================================================================
        // NHAN VIEN — DOI TRANG THAI (khong co API xoa: QT17)
        // =====================================================================

        /// <summary>Tiếp nhận sự cố Mới → Đang xử lý; người xử lý mặc định là tôi (hoặc maTKXuLy). QT14: NguyHiem phải tiếp nhận trong 5 phút.</summary>
        [HttpPost("{id:int}/tiep-nhan")]
        [Authorize(Roles = NHAN_VIEN)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult TiepNhan(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TiepNhanSuCoDto yc)
        {
            var truoc = DocTomTat(id);
            if (truoc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (truoc.TrangThai != SuCo.TT_MOI)
                return Loi("Sự cố #" + id + " đang ở trạng thái \"" + SuCo.TenTrangThaiCua(truoc.TrangThai) + "\" — chỉ tiếp nhận được sự cố Mới.");

            var maXuLy = yc?.MaTKXuLy ?? MaTK;
            if (maXuLy == null) return Loi("Không xác định được người xử lý.");
            var nguoiXuLy = db.TaiKhoans.AsNoTracking()
                .Where(t => t.MaTK == maXuLy.Value && t.KichHoat && (t.VaiTro == "Admin" || t.VaiTro == "NhanVien"))
                .Select(t => t.HoTen).FirstOrDefault();
            if (nguoiXuLy == null) return Loi("Người xử lý phải là tài khoản nhân viên đang hoạt động.");

            var luc = DateTime.Now;
            var ma = maXuLy;
            var thongBao = "Đã tiếp nhận sự cố #" + id + " — người xử lý: " + nguoiXuLy + ".";
            if (truoc.MucDo == SuCo.MUC_NGUY_HIEM)
            {
                var phut = (luc - truoc.ThoiDiemBao).TotalMinutes;
                thongBao += phut <= SuCo.PHUT_TIEP_NHAN_NGUY_HIEM
                    ? " Đạt QT14 (tiếp nhận sau " + Math.Max(0, Math.Round(phut, 1)) + " phút ≤ 5 phút)."
                    : " TRỄ QT14: tiếp nhận sau " + Math.Round(phut) + " phút (quy định ≤ 5 phút).";
            }
            return ApDung(id, "tiep-nhan", thongBao, false, () => db.SuCos
                .Where(s => s.MaSuCo == id && s.TrangThai == SuCo.TT_MOI)
                .ExecuteUpdate(u => u
                    .SetProperty(s => s.TrangThai, SuCo.TT_DANG_XU_LY)
                    .SetProperty(s => s.MaTKXuLy, ma)
                    .SetProperty(s => s.ThoiDiemTiepNhan, (DateTime?)luc)));
        }

        /// <summary>UC24.2 — tạm giữ (tamGiu = true) / gỡ tạm giữ (false) container của sự cố. QT15: container tạm giữ không được giao ra cổng.</summary>
        [HttpPost("{id:int}/tam-giu")]
        [Authorize(Roles = NHAN_VIEN)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult TamGiu(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TamGiuSuCoDto yc)
        {
            if (yc?.TamGiu == null) return Loi("Thiếu tamGiu (true = tạm giữ container, false = gỡ tạm giữ).");
            var giu = yc.TamGiu.Value;
            var truoc = DocTomTat(id);
            if (truoc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (string.IsNullOrWhiteSpace(truoc.SoContainer)) return Loi("Sự cố #" + id + " chưa gắn số container — không tạm giữ được.");
            if (!SuCo.ConMo(truoc.TrangThai))
                return Loi("Sự cố #" + id + " đã " + (truoc.TrangThai == SuCo.TT_DONG ? "đóng" : "kết luận báo giả") + " — không đổi tạm giữ được.");
            if (truoc.TamGiuContainer == giu)
                return Loi(giu ? "Container " + truoc.SoContainer + " đang bị tạm giữ theo sự cố #" + id + " rồi."
                               : "Container " + truoc.SoContainer + " không bị tạm giữ theo sự cố #" + id + ".");

            var thongBao = giu
                ? "Đã tạm giữ container " + truoc.SoContainer + " — cổng sẽ KHÔNG cho giao ra cho tới khi gỡ tạm giữ hoặc đóng sự cố #" + id + "."
                : "Đã gỡ tạm giữ container " + truoc.SoContainer + " (sự cố #" + id + ").";
            return ApDung(id, giu ? "tam-giu" : "go-tam-giu", thongBao, true, () => db.SuCos
                .Where(s => s.MaSuCo == id && s.TamGiuContainer == !giu && s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA)
                .ExecuteUpdate(u => u.SetProperty(s => s.TamGiuContainer, giu)));
        }

        /// <summary>Ghi kết quả xử lý (bắt buộc) → Đã xử lý. Sự cố chưa tiếp nhận thì tự ghi tôi là người xử lý. Báo khách liên quan (UC24.3).</summary>
        [HttpPost("{id:int}/ket-qua")]
        [Authorize(Roles = NHAN_VIEN)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GhiKetQua(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] KetQuaSuCoDto yc)
        {
            var ketQua = (yc?.KetQua ?? "").Trim();
            if (ketQua.Length == 0) return Loi("Nhập kết quả xử lý.");
            if (ketQua.Length > 1000) return Loi("Kết quả xử lý tối đa 1000 ký tự.");
            var truoc = DocTomTat(id);
            if (truoc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (!SuCo.ConMo(truoc.TrangThai))
                return Loi("Sự cố #" + id + " đã " + (truoc.TrangThai == SuCo.TT_DONG ? "đóng" : "kết luận báo giả") + " — không ghi kết quả được nữa.");

            var luc = DateTime.Now;
            var ma = MaTK;
            var thongBao = "Đã ghi kết quả xử lý sự cố #" + id + (truoc.TrangThai == SuCo.TT_DA_XU_LY ? " (cập nhật)." : " — chờ đóng sự cố.");
            return ApDung(id, "ket-qua", thongBao, true, () => db.SuCos
                .Where(s => s.MaSuCo == id && (s.TrangThai == SuCo.TT_MOI || s.TrangThai == SuCo.TT_DANG_XU_LY || s.TrangThai == SuCo.TT_DA_XU_LY))
                .ExecuteUpdate(u => u
                    .SetProperty(s => s.KetQua, ketQua)
                    .SetProperty(s => s.TrangThai, SuCo.TT_DA_XU_LY)
                    .SetProperty(s => s.MaTKXuLy, s => s.MaTKXuLy ?? ma)
                    .SetProperty(s => s.ThoiDiemTiepNhan, s => s.ThoiDiemTiepNhan ?? (DateTime?)luc)));
        }

        /// <summary>Đóng sự cố (QT17: chỉ khi đã ghi kết quả). Đóng cũng gỡ tạm giữ container.</summary>
        [HttpPost("{id:int}/dong")]
        [Authorize(Roles = NHAN_VIEN)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Dong(int id)
        {
            var truoc = DocTomTat(id);
            if (truoc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (truoc.TrangThai == SuCo.TT_DONG) return Loi("Sự cố #" + id + " đã đóng trước đó.");
            if (truoc.TrangThai == SuCo.TT_BAO_GIA) return Loi("Sự cố #" + id + " đã được kết luận báo giả — không cần đóng.");
            // QT17: khong co ket qua thi khong dong (CSDL cung chan bang CK_SuCo_KetQua)
            if (truoc.TrangThai != SuCo.TT_DA_XU_LY || string.IsNullOrWhiteSpace(truoc.KetQua))
                return Loi("Sự cố #" + id + " chưa ghi kết quả xử lý — phải ghi kết quả trước khi đóng.");

            var luc = DateTime.Now;
            var thongBao = "Đã đóng sự cố #" + id + (truoc.TamGiuContainer ? " và gỡ tạm giữ container " + truoc.SoContainer + "." : ".");
            return ApDung(id, "dong", thongBao, truoc.TamGiuContainer, () => db.SuCos
                .Where(s => s.MaSuCo == id && s.TrangThai == SuCo.TT_DA_XU_LY && s.KetQua != null)
                .ExecuteUpdate(u => u
                    .SetProperty(s => s.TrangThai, SuCo.TT_DONG)
                    .SetProperty(s => s.ThoiDiemDong, (DateTime?)luc)
                    .SetProperty(s => s.TamGiuContainer, false)));
        }

        /// <summary>Đánh dấu báo giả (bắt buộc lý do — lưu vào KetQua) — chỉ khi sự cố Mới / Đang xử lý. Gỡ tạm giữ nếu có.</summary>
        [HttpPost("{id:int}/bao-gia")]
        [Authorize(Roles = NHAN_VIEN)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult BaoGia(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] BaoGiaSuCoDto yc)
        {
            var lyDo = (yc?.LyDo ?? "").Trim();
            if (lyDo.Length == 0) return Loi("Nhập lý do kết luận báo giả.");
            if (lyDo.Length > 1000 - TIEN_TO_BAO_GIA.Length) return Loi("Lý do tối đa " + (1000 - TIEN_TO_BAO_GIA.Length) + " ký tự.");
            var truoc = DocTomTat(id);
            if (truoc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (truoc.TrangThai != SuCo.TT_MOI && truoc.TrangThai != SuCo.TT_DANG_XU_LY)
                return Loi("Sự cố #" + id + " đang ở trạng thái \"" + SuCo.TenTrangThaiCua(truoc.TrangThai)
                           + "\" — chỉ đánh dấu báo giả được khi sự cố Mới hoặc Đang xử lý.");

            var luc = DateTime.Now;
            var ma = MaTK;
            var ketQua = TIEN_TO_BAO_GIA + lyDo;
            return ApDung(id, "bao-gia", "Đã kết luận sự cố #" + id + " là báo giả.", truoc.TamGiuContainer, () => db.SuCos
                .Where(s => s.MaSuCo == id && (s.TrangThai == SuCo.TT_MOI || s.TrangThai == SuCo.TT_DANG_XU_LY))
                .ExecuteUpdate(u => u
                    .SetProperty(s => s.TrangThai, SuCo.TT_BAO_GIA)
                    .SetProperty(s => s.KetQua, ketQua)
                    .SetProperty(s => s.ThoiDiemDong, (DateTime?)luc)
                    .SetProperty(s => s.ThoiDiemTiepNhan, s => s.ThoiDiemTiepNhan ?? (DateTime?)luc)
                    .SetProperty(s => s.MaTKXuLy, s => s.MaTKXuLy ?? ma)
                    .SetProperty(s => s.TamGiuContainer, false)));
        }

        // =====================================================================
        // UC28 — THONG KE SU CO
        // =====================================================================

        /// <summary>
        /// Thống kê sự cố báo trong khoảng ngày (mặc định 30 ngày gần nhất): số theo loại / mức độ / trạng thái, thời gian trung bình
        /// báo → tiếp nhận và báo → đóng, tỷ lệ đạt QT14; cộng số đang mở HIỆN TẠI (nguy hiểm, tạm giữ, trễ hạn).
        /// </summary>
        [HttpGet("thong-ke")]
        [Authorize(Roles = NHAN_VIEN)]
        public ActionResult<ThongKeSuCoDto> ThongKe([FromQuery] DateTime? tuNgay, [FromQuery] DateTime? denNgay)
        {
            var tu = (tuNgay ?? DateTime.Today.AddDays(-29)).Date;
            var den = (denNgay ?? DateTime.Today).Date;
            if (tu > den) return Loi("Từ ngày phải trước hoặc bằng đến ngày.");
            var denSau = den.AddDays(1);
            var q = db.SuCos.AsNoTracking().Where(s => s.ThoiDiemBao >= tu && s.ThoiDiemBao < denSau);

            List<DemSuCoDto> Dem(IEnumerable<(string Ma, int So)> co, string[] tatCa, Func<string, string> ten) =>
                tatCa.Select(m => new DemSuCoDto { Ma = m, Ten = ten(m), So = co.Where(x => x.Ma == m).Select(x => x.So).FirstOrDefault() }).ToList();
            var theoLoai = q.GroupBy(s => s.Loai).Select(g => new { g.Key, So = g.Count() }).ToList().Select(x => (x.Key, x.So)).ToList();
            var theoMuc = q.GroupBy(s => s.MucDo).Select(g => new { g.Key, So = g.Count() }).ToList().Select(x => (x.Key, x.So)).ToList();
            var theoTT = q.GroupBy(s => s.TrangThai).Select(g => new { g.Key, So = g.Count() }).ToList().Select(x => (x.Key, x.So)).ToList();

            double? TbPhut(IQueryable<double?> giay)
            {
                var tb = giay.Average();
                return tb == null ? (double?)null : Math.Round(tb.Value / 60d, 1);
            }
            var tbTiepNhan = TbPhut(q.Where(s => s.ThoiDiemTiepNhan != null)
                .Select(s => (double?)EF.Functions.DateDiffSecond(s.ThoiDiemBao, s.ThoiDiemTiepNhan.Value)));
            var tbDong = TbPhut(q.Where(s => s.TrangThai == SuCo.TT_DONG && s.ThoiDiemDong != null)
                .Select(s => (double?)EF.Functions.DateDiffSecond(s.ThoiDiemBao, s.ThoiDiemDong.Value)));
            var tbTiepNhanNguy = TbPhut(q.Where(s => s.MucDo == SuCo.MUC_NGUY_HIEM && s.ThoiDiemTiepNhan != null)
                .Select(s => (double?)EF.Functions.DateDiffSecond(s.ThoiDiemBao, s.ThoiDiemTiepNhan.Value)));
            var nguyDaTiepNhan = q.Count(s => s.MucDo == SuCo.MUC_NGUY_HIEM && s.ThoiDiemTiepNhan != null);
            var nguyDat = q.Count(s => s.MucDo == SuCo.MUC_NGUY_HIEM && s.ThoiDiemTiepNhan != null
                                       && EF.Functions.DateDiffSecond(s.ThoiDiemBao, s.ThoiDiemTiepNhan.Value) <= SuCo.PHUT_TIEP_NHAN_NGUY_HIEM * 60);

            // So HIEN TAI (khong theo khoang ngay) — viec dang cho xu ly
            var bayGio = DateTime.Now;
            var mocNguy = bayGio.AddMinutes(-SuCo.PHUT_TIEP_NHAN_NGUY_HIEM);
            var mocKhach = bayGio.AddHours(-SuCo.GIO_TRA_LOI_KHACH);
            var mo = db.SuCos.AsNoTracking().Where(s => s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA);
            var tong = theoLoai.Sum(x => x.So);
            return new ThongKeSuCoDto
            {
                TuNgay = tu,
                DenNgay = den,
                Tong = tong,
                TheoLoai = Dem(theoLoai, SuCo.CAC_LOAI, SuCo.TenLoaiCua),
                TheoMucDo = Dem(theoMuc, SuCo.CAC_MUC_DO, SuCo.TenMucDoCua),
                TheoTrangThai = Dem(theoTT, SuCo.CAC_TRANG_THAI, SuCo.TenTrangThaiCua),
                TbPhutTiepNhan = tbTiepNhan,
                TbPhutDong = tbDong,
                TbPhutTiepNhanNguyHiem = tbTiepNhanNguy,
                SoNguyHiemDaTiepNhan = nguyDaTiepNhan,
                SoNguyHiemDatQT14 = nguyDat,
                SoBaoGia = theoTT.Where(x => x.Item1 == SuCo.TT_BAO_GIA).Select(x => x.Item2).FirstOrDefault(),
                DangMo = mo.Count(),
                NguyHiemDangMo = mo.Count(s => s.MucDo == SuCo.MUC_NGUY_HIEM),
                NguyHiemChuaTiepNhan = mo.Count(s => s.MucDo == SuCo.MUC_NGUY_HIEM && s.TrangThai == SuCo.TT_MOI),
                DangTamGiu = mo.Count(s => s.TamGiuContainer),
                KhachChuaTraLoi = mo.Count(s => s.NguoiBao.VaiTro == VT_KHACH && s.KetQua == null),
                TreHan = mo.Count(s => (s.MucDo == SuCo.MUC_NGUY_HIEM && s.TrangThai == SuCo.TT_MOI && s.ThoiDiemBao < mocNguy)
                                       || (s.NguoiBao.VaiTro == VT_KHACH && s.KetQua == null && s.ThoiDiemBao < mocKhach))
            };
        }

        // =====================================================================
        // NOI BO
        // =====================================================================

        private sealed class TomTat
        {
            public int MaSuCo;
            public string TrangThai;
            public string MucDo;
            public string TieuDe;
            public string SoContainer;
            public int? MaLenh;
            public bool TamGiuContainer;
            public string KetQua;
            public DateTime ThoiDiemBao;
            public int MaTKBao;
        }

        private TomTat DocTomTat(int id) => db.SuCos.AsNoTracking().Where(s => s.MaSuCo == id).Select(s => new TomTat
        {
            MaSuCo = s.MaSuCo,
            TrangThai = s.TrangThai,
            MucDo = s.MucDo,
            TieuDe = s.TieuDe,
            SoContainer = s.SoContainer,
            MaLenh = s.MaLenh,
            TamGiuContainer = s.TamGiuContainer,
            KetQua = s.KetQua,
            ThoiDiemBao = s.ThoiDiemBao,
            MaTKBao = s.MaTKBao
        }).FirstOrDefault();

        /// <summary>
        /// Cap nhat CO DIEU KIEN + su kien Outbox trong CUNG giao dich. capNhat tra 0 dong = nguoi khac vua doi trang thai
        /// (hoac du lieu khong con hop le) -> bao loi, khong ghi gi. baoKhach: gui thong bao khach hang lien quan (UC24.3).
        /// </summary>
        private IActionResult ApDung(int id, string thaoTac, string thongBao, bool baoKhach, Func<int> capNhat)
        {
            TomTat sau;
            using (var gd = db.Database.BeginTransaction())
            {
                if (capNhat() == 0)
                {
                    gd.Rollback();
                    var hienTai = DocTomTat(id);
                    return hienTai == null
                        ? KhongThay("Không tìm thấy sự cố #" + id + ".")
                        : Loi("Sự cố #" + id + " vừa được người khác cập nhật (hiện: " + SuCo.TenTrangThaiCua(hienTai.TrangThai) + ") — tải lại rồi thử lại.");
                }
                PhatLaiDong.SauCapNhat(db, db.SuCos.Where(s => s.MaSuCo == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
                sau = DocTomTat(id);
                HopThuDiTinh.Them(db, new SuCoChungDaCapNhat
                {
                    MaSuCo = id,
                    ThaoTac = thaoTac,
                    TrangThai = sau.TrangThai,
                    TieuDe = sau.TieuDe,
                    SoContainer = sau.SoContainer,
                    TamGiuContainer = sau.TamGiuContainer,
                    NguoiThaoTac = !string.IsNullOrWhiteSpace(User.HoTen()) ? User.HoTen() : NguoiDung
                });
                db.SaveChanges();
                gd.Commit();
            }
            GhiNhatKy("Sự cố #" + id + " — " + thaoTac + ": " + Cat(thongBao, 300));

            string daBao = null;
            if (baoKhach)
            {
                try { daBao = BaoKhachHang(sau, thaoTac); }
                catch (Exception ex) { daBao = "Chưa gửi được thông báo cho khách: " + LoiGoc(ex); }
            }
            return XongViec(thongBao + (daBao != null ? " " + daBao : ""), id, new KetQuaThaoTacSuCoDto
            {
                TrangThai = sau.TrangThai,
                TenTrangThai = SuCo.TenTrangThaiCua(sau.TrangThai),
                TamGiuContainer = sau.TamGiuContainer,
                BaoKhach = daBao
            });
        }

        /// <summary>
        /// UC24.3 — BAO KHACH HANG lien quan qua co che thong bao khach co san: EMAIL (EmailService — co SMTP thi gui that,
        /// chua cau hinh thi luu .eml vao App_Data/Outbox nhu thu hoa don). Khach = nguoi phan anh (neu la khach), chu lenh,
        /// hoac chu container. Tra cau mo ta cho nhan vien (null = su co khong lien quan khach nao).
        /// </summary>
        private string BaoKhachHang(TomTat s, string thaoTac)
        {
            var bao = db.TaiKhoans.AsNoTracking().Where(t => t.MaTK == s.MaTKBao)
                .Select(t => new { t.VaiTro, t.Email, t.MaKH, t.HoTen }).FirstOrDefault();
            int? maKH = null;
            string email = null, tenNhan = null;
            if (bao != null && bao.VaiTro == VT_KHACH)
            {
                maKH = bao.MaKH;
                email = bao.Email;
                tenNhan = bao.HoTen;
            }
            if (maKH == null && s.MaLenh != null)
                maKH = db.LenhGiaoNhans.AsNoTracking().Where(l => l.MaLenh == s.MaLenh.Value).Select(l => l.MaKH).FirstOrDefault();
            if (maKH == null && !string.IsNullOrWhiteSpace(s.SoContainer))
                maKH = db.Containers.AsNoTracking().Where(c => c.SoCont == s.SoContainer && c.MaKH != null).Select(c => c.MaKH).FirstOrDefault();
            if (maKH == null && string.IsNullOrWhiteSpace(email)) return null;

            var kh = maKH == null ? null : db.KhachHangs.AsNoTracking().Where(k => k.MaKH == maKH.Value)
                .Select(k => new { k.TenCongTy, k.Email }).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(email)) email = kh?.Email;
            if (string.IsNullOrWhiteSpace(email) && maKH != null)
                email = db.TaiKhoans.AsNoTracking()
                    .Where(t => t.MaKH == maKH.Value && t.VaiTro == VT_KHACH && t.KichHoat && t.Email != null && t.Email != "")
                    .Select(t => t.Email).FirstOrDefault();
            var ten = kh?.TenCongTy ?? tenNhan ?? "khách hàng";
            if (string.IsNullOrWhiteSpace(email)) return "Khách hàng " + ten + " chưa có email — chưa gửi được thông báo.";

            var chiTiet = db.SuCos.AsNoTracking().Where(x => x.MaSuCo == s.MaSuCo)
                .Select(x => new { x.TieuDe, x.KetQua, x.TrangThai, x.SoContainer, x.TamGiuContainer }).First();
            string viec;
            switch (thaoTac)
            {
                case "tam-giu": viec = "Container " + chiTiet.SoContainer + " đang được TẠM GIỮ tại cảng để xử lý sự cố — tạm thời chưa giao ra cổng."; break;
                case "go-tam-giu": viec = "Container " + chiTiet.SoContainer + " đã được GỠ tạm giữ, có thể giao nhận bình thường."; break;
                // dong / bao-gia chi bao khach khi truoc do DANG tam giu (ApDung goi voi baoKhach = truoc.TamGiuContainer)
                case "dong": viec = "Sự cố đã được đóng" + (!string.IsNullOrEmpty(s.SoContainer) ? ", container " + s.SoContainer + " đã được gỡ tạm giữ." : "."); break;
                case "bao-gia": viec = "Sự cố được xác định là báo giả" + (!string.IsNullOrEmpty(s.SoContainer) ? ", container " + s.SoContainer + " đã được gỡ tạm giữ." : "."); break;
                default: viec = "Cảng đã ghi kết quả xử lý."; break;
            }
            var html = "<p>Kính gửi " + WebUtility.HtmlEncode(ten) + ",</p>"
                     + "<p>SmartPort thông báo về sự cố <b>#" + s.MaSuCo + "</b>: " + WebUtility.HtmlEncode(chiTiet.TieuDe) + ".</p>"
                     + "<p>" + WebUtility.HtmlEncode(viec) + "</p>"
                     + (string.IsNullOrWhiteSpace(chiTiet.KetQua) ? "" : "<p><b>Kết quả xử lý:</b> " + WebUtility.HtmlEncode(chiTiet.KetQua) + "</p>")
                     + "<p>Trạng thái hiện tại: <b>" + SuCo.TenTrangThaiCua(chiTiet.TrangThai) + "</b>. Xem chi tiết tại mục "
                     + "<i>Báo sự cố, phản ánh</i> trên website SmartPort.</p><p>Trân trọng,<br/>Cảng Quốc tế SmartPort</p>";
            EmailService.GuiNen(email.Trim(), "[SmartPort] Thông báo sự cố #" + s.MaSuCo + " — " + chiTiet.TieuDe, html);
            return "Đã gửi email thông báo cho " + ten + " (" + CheEmail(email.Trim()) + ").";
        }

        /// <summary>Ten hien thi + han / canh bao theo quy tac + thao tac hop le cho tung dong (tinh SAU khi lay du lieu)</summary>
        private void HoanThien(List<SuCoDongDto> ds)
        {
            if (ds.Count == 0) return;
            // Chu container (khi su co khong co lenh / khong do khach bao) — mot truy van cho ca trang
            var canTim = ds.Where(x => x.KhachHang == null && !string.IsNullOrEmpty(x.SoContainer)).Select(x => x.SoContainer).Distinct().ToList();
            var chuCont = canTim.Count == 0 ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : db.Containers.AsNoTracking().Where(c => canTim.Contains(c.SoCont) && c.KhachHang != null)
                    .Select(c => new { c.SoCont, c.KhachHang.TenCongTy }).ToList()
                    .GroupBy(c => c.SoCont, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().TenCongTy, StringComparer.OrdinalIgnoreCase);
            var bayGio = DateTime.Now;
            foreach (var x in ds)
            {
                x.TenLoai = SuCo.TenLoaiCua(x.Loai);
                x.TenMucDo = SuCo.TenMucDoCua(x.MucDo);
                x.TenTrangThai = SuCo.TenTrangThaiCua(x.TrangThai);
                x.LaKhachHang = x.VaiTroNguoiBao == VT_KHACH;
                if (x.KhachHang == null && !string.IsNullOrEmpty(x.SoContainer) && chuCont.TryGetValue(x.SoContainer, out var chu)) x.KhachHang = chu;
                var conMo = SuCo.ConMo(x.TrangThai);

                // QT14: nguy hiem phai tiep nhan trong 5 phut
                if (x.MucDo == SuCo.MUC_NGUY_HIEM)
                {
                    x.HanTiepNhan = x.ThoiDiemBao.AddMinutes(SuCo.PHUT_TIEP_NHAN_NGUY_HIEM);
                    if (x.ThoiDiemTiepNhan == null && conMo)
                    {
                        var con = (x.HanTiepNhan.Value - bayGio).TotalMinutes;
                        x.TreHan = con < 0;
                        x.CanhBaoHan = con >= 0 ? "Còn " + Math.Max(1, (int)Math.Ceiling(con)) + " phút để tiếp nhận (QT14)"
                                                : "Quá hạn tiếp nhận " + ThoiLuong(-con) + " (QT14 ≤ 5 phút)";
                    }
                    else if (x.ThoiDiemTiepNhan != null && x.ThoiDiemTiepNhan.Value > x.HanTiepNhan.Value)
                        x.CanhBaoHan = "Tiếp nhận trễ " + ThoiLuong((x.ThoiDiemTiepNhan.Value - x.HanTiepNhan.Value).TotalMinutes) + " so với QT14";
                }
                // QT17: phan anh cua khach phai co ket qua trong 24 gio
                if (x.LaKhachHang)
                {
                    x.HanTraLoi = x.ThoiDiemBao.AddHours(SuCo.GIO_TRA_LOI_KHACH);
                    if (string.IsNullOrEmpty(x.KetQua) && conMo)
                    {
                        var qua = bayGio > x.HanTraLoi.Value;
                        x.TreHan = x.TreHan || qua;
                        if (x.CanhBaoHan == null)
                            x.CanhBaoHan = qua ? "Quá hạn trả lời khách " + ThoiLuong((bayGio - x.HanTraLoi.Value).TotalMinutes) + " (24 giờ)"
                                               : "Hạn trả lời khách: " + x.HanTraLoi.Value.ToString("HH:mm dd/MM");
                    }
                }

                x.CoTheTiepNhan = x.TrangThai == SuCo.TT_MOI;
                x.CoTheGhiKetQua = conMo;
                x.CoTheDong = x.TrangThai == SuCo.TT_DA_XU_LY && !string.IsNullOrEmpty(x.KetQua);
                x.CoTheBaoGia = x.TrangThai == SuCo.TT_MOI || x.TrangThai == SuCo.TT_DANG_XU_LY;
                x.CoTheTamGiu = conMo && !string.IsNullOrEmpty(x.SoContainer) && !x.TamGiuContainer;
                x.CoTheGoTamGiu = conMo && x.TamGiuContainer;
            }
        }

        private static string ThoiLuong(double phut)
        {
            if (phut < 60) return Math.Max(1, (int)Math.Round(phut)) + " phút";
            if (phut < 48 * 60) return Math.Round(phut / 60, 1).ToString("0.#") + " giờ";
            return Math.Round(phut / 1440) + " ngày";
        }

        private static string ChuanCont(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

        private static bool CungCont(string a, string b)
        {
            string Sach(string x) => new string((x ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            return Sach(a).Length > 0 && Sach(a) == Sach(b);
        }

        private static string TenLenh(LenhGiaoNhan l) => string.IsNullOrWhiteSpace(l.SoLenh) ? "#" + l.MaLenh : l.SoLenh;

        private static string CheEmail(string e)
        {
            var a = e.IndexOf('@');
            return a <= 1 ? "***" + (a >= 0 ? e.Substring(a) : "") : e.Substring(0, 1) + "***" + e.Substring(a);
        }

        private static string Cat(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;
    }

    // =====================================================================
    // DTO — XU LY SU CO
    // =====================================================================

    /// <summary>Báo sự cố / gửi phản ánh (UC22)</summary>
    public class TaoSuCoDto
    {
        /// <summary>AnNinhAnToan / HangHoaThietBi / ThanhToan / DichVu / HeThong (khách: 3 loại giữa)</summary>
        [Required(ErrorMessage = "Chọn loại sự cố.")]
        public string Loai { get; set; }

        /// <summary>Thap / TrungBinh / NguyHiem</summary>
        [Required(ErrorMessage = "Chọn mức độ.")]
        public string MucDo { get; set; }

        [Required(ErrorMessage = "Nhập tiêu đề sự cố."), StringLength(150, ErrorMessage = "Tiêu đề tối đa 150 ký tự.")]
        public string TieuDe { get; set; }

        [Required(ErrorMessage = "Nhập mô tả sự cố."), StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string MoTa { get; set; }

        [StringLength(100, ErrorMessage = "Vị trí tối đa 100 ký tự.")]
        public string ViTri { get; set; }

        [StringLength(15, ErrorMessage = "Số container tối đa 15 ký tự.")]
        public string SoContainer { get; set; }

        /// <summary>Mã lệnh giao nhận liên quan (khách: chỉ lệnh của doanh nghiệp mình)</summary>
        public int? MaLenh { get; set; }

        /// <summary>Ảnh hiện trường data:image/jpeg;base64,... (≤ ~2 MB)</summary>
        public string AnhBase64 { get; set; }

        /// <summary>Nhân viên chuyển cảnh báo camera AI (SuCoAnNinh.MaSC) thành sự cố</summary>
        public int? MaSCAnNinh { get; set; }
    }

    public class TiepNhanSuCoDto
    {
        /// <summary>Giao cho nhân viên khác (bỏ trống = chính tôi)</summary>
        public int? MaTKXuLy { get; set; }
    }

    public class TamGiuSuCoDto
    {
        /// <summary>true = tạm giữ container, false = gỡ tạm giữ</summary>
        public bool? TamGiu { get; set; }
    }

    public class KetQuaSuCoDto
    {
        public string KetQua { get; set; }
    }

    public class BaoGiaSuCoDto
    {
        public string LyDo { get; set; }
    }

    public class KetQuaTaoSuCoDto
    {
        /// <summary>true = đã phát báo động toàn cảng (QT14, mức NguyHiem)</summary>
        public bool BaoDongToanCang { get; set; }
        public DateTime? HanTiepNhan { get; set; }
        public DateTime? HanTraLoi { get; set; }
    }

    public class KetQuaThaoTacSuCoDto
    {
        public string TrangThai { get; set; }
        public string TenTrangThai { get; set; }
        public bool TamGiuContainer { get; set; }
        /// <summary>Kết quả báo khách hàng liên quan (UC24.3) — null khi không báo</summary>
        public string BaoKhach { get; set; }
    }

    /// <summary>Một dòng sự cố (không kèm ảnh)</summary>
    public class SuCoDongDto
    {
        public int MaSuCo { get; set; }
        public string Loai { get; set; }
        public string TenLoai { get; set; }
        public string MucDo { get; set; }
        public string TenMucDo { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        public string ViTri { get; set; }
        public string SoContainer { get; set; }
        public int? MaLenh { get; set; }
        public string SoLenh { get; set; }
        public bool CoAnh { get; set; }
        public int MaTKBao { get; set; }
        public string NguoiBao { get; set; }
        public string VaiTroNguoiBao { get; set; }
        /// <summary>true = phản ánh của khách hàng (QT17: trả lời trong 24 giờ)</summary>
        public bool LaKhachHang { get; set; }
        /// <summary>Doanh nghiệp liên quan (khách phản ánh / chủ lệnh / chủ container)</summary>
        public string KhachHang { get; set; }
        public int? MaTKXuLy { get; set; }
        public string NguoiXuLy { get; set; }
        public string TrangThai { get; set; }
        public string TenTrangThai { get; set; }
        public bool TamGiuContainer { get; set; }
        public string KetQua { get; set; }
        public DateTime ThoiDiemBao { get; set; }
        public DateTime? ThoiDiemTiepNhan { get; set; }
        public DateTime? ThoiDiemDong { get; set; }
        public int? MaSCAnNinh { get; set; }
        /// <summary>QT14: hạn tiếp nhận (chỉ mức NguyHiem)</summary>
        public DateTime? HanTiepNhan { get; set; }
        /// <summary>QT17: hạn trả lời khách (chỉ phản ánh của khách)</summary>
        public DateTime? HanTraLoi { get; set; }
        /// <summary>Đang quá hạn (chưa tiếp nhận nguy hiểm sau 5 phút / chưa trả lời khách sau 24 giờ)</summary>
        public bool TreHan { get; set; }
        public string CanhBaoHan { get; set; }
        public bool CoTheTiepNhan { get; set; }
        public bool CoTheGhiKetQua { get; set; }
        public bool CoTheDong { get; set; }
        public bool CoTheBaoGia { get; set; }
        public bool CoTheTamGiu { get; set; }
        public bool CoTheGoTamGiu { get; set; }
    }

    public class SuCoChiTietDto : SuCoDongDto
    {
        public string AnhBase64 { get; set; }
        /// <summary>Tóm tắt cảnh báo camera AI gốc (khi chuyển từ SuCoAnNinh)</summary>
        public string CanhBaoCameraAi { get; set; }

        public static SuCoChiTietDto Tu(SuCoDongDto d)
        {
            var ct = new SuCoChiTietDto();
            foreach (var p in typeof(SuCoDongDto).GetProperties())
                if (p.CanWrite) p.SetValue(ct, p.GetValue(d));
            return ct;
        }
    }

    public class SuCoCuaToiDto
    {
        public List<SuCoDongDto> CuaToi { get; set; } = new List<SuCoDongDto>();
        /// <summary>Khách hàng: sự cố cảng ghi nhận liên quan container / lệnh của doanh nghiệp mình</summary>
        public List<SuCoDongDto> LienQuan { get; set; } = new List<SuCoDongDto>();
        public int SoDangMo { get; set; }
        public int SoDaTraLoi { get; set; }
    }

    public class DoiTuongSuCoCuaToiDto
    {
        public List<ContainerChonSuCoDto> Container { get; set; } = new List<ContainerChonSuCoDto>();
        public List<LenhChonSuCoDto> Lenh { get; set; } = new List<LenhChonSuCoDto>();
    }

    public class ContainerChonSuCoDto
    {
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TrangThai { get; set; }
    }

    public class LenhChonSuCoDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string TrangThai { get; set; }
    }

    public class NhanVienXuLySuCoDto
    {
        public int MaTK { get; set; }
        public string HoTen { get; set; }
        public string VaiTro { get; set; }
    }

    public class DemSuCoDto
    {
        public string Ma { get; set; }
        public string Ten { get; set; }
        public int So { get; set; }
    }

    /// <summary>UC28 — thống kê sự cố</summary>
    public class ThongKeSuCoDto
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        /// <summary>Số sự cố báo trong khoảng ngày</summary>
        public int Tong { get; set; }
        public List<DemSuCoDto> TheoLoai { get; set; }
        public List<DemSuCoDto> TheoMucDo { get; set; }
        public List<DemSuCoDto> TheoTrangThai { get; set; }
        /// <summary>Phút trung bình báo → tiếp nhận (sự cố đã tiếp nhận trong khoảng)</summary>
        public double? TbPhutTiepNhan { get; set; }
        /// <summary>Phút trung bình báo → đóng (sự cố đã đóng, không tính báo giả)</summary>
        public double? TbPhutDong { get; set; }
        public double? TbPhutTiepNhanNguyHiem { get; set; }
        public int SoNguyHiemDaTiepNhan { get; set; }
        /// <summary>QT14: số sự cố nguy hiểm được tiếp nhận trong ≤ 5 phút</summary>
        public int SoNguyHiemDatQT14 { get; set; }
        public int SoBaoGia { get; set; }
        /// <summary>HIỆN TẠI: sự cố chưa đóng / chưa kết luận báo giả</summary>
        public int DangMo { get; set; }
        public int NguyHiemDangMo { get; set; }
        public int NguyHiemChuaTiepNhan { get; set; }
        public int DangTamGiu { get; set; }
        public int KhachChuaTraLoi { get; set; }
        /// <summary>HIỆN TẠI: đang quá hạn (QT14 5 phút / QT17 24 giờ)</summary>
        public int TreHan { get; set; }
    }
}
