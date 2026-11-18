using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// THU PHI TAI CONG.
    ///
    /// Ba hinh thuc, xet theo dung thu tu nay (quan trong — dao thu tu la
    /// thu nham tien cua khach da mua the thang):
    ///   1. THE THANG con han  -> mien phi luot, chi ghi nhan
    ///   2. THE TRA TRUOC du tien -> tru dan vao so du (MOT cau UPDATE nguyen tu co dieu kien)
    ///   3. Con lai            -> thu tien mat theo bieu gia loai xe
    /// Rieng luot GHI TRE (tram cong mat mang, bao ve da thu tien mat kem phieu tay): ghi nhan dung so tien da thu,
    /// KHONG tru the lan nua (ThuTienMatTaiCong).
    ///
    /// Thu MOT LAN cho mot luot xe, tinh o CHIEU RA (giong bai xe): xe vao
    /// chua biet o bao lau, ra moi chot. Nhung o cang thi phi la phi LUOT
    /// nen thu luc ra cho gon mot dau moi.
    /// </summary>
    public static class PhiCongService
    {
        public const string TheThang = "Thẻ tháng";
        public const string TruTaiKhoan = "Trừ tài khoản";
        public const string TienMat = "Tiền mặt";
        /// <summary>Tiền mặt bảo vệ thu tại cổng lúc mất mạng (có phiếu tay) — vẫn là tiền mặt phải nộp quỹ</summary>
        public const string TienMatPhieuTay = "Tiền mặt (phiếu tay)";
        /// <summary>
        /// Lượt GHI TRỄ (trạm cổng mất mạng) không kèm tiền mặt tại cổng mà xe không có thẻ dùng được: KHÔNG ai thu tiền
        /// lúc xe qua -> ghi khoản phải thu của khách, KHÔNG tính vào "tiền mặt phải nộp quỹ" (trước đây ghi "Tiền mặt"
        /// -> quỹ thu ngân bị đòi tiền chưa ai thu, báo cáo ca đã chốt bị đổi khi đồng bộ muộn).
        /// </summary>
        public const string CongNo = "Công nợ (chờ thu)";

        private const string LOAI_THE_THANG = "Tháng";
        private const string LOAI_THE_TRA_TRUOC = "Trả trước";

        /// <summary>Tiền mặt thường hoặc tiền mặt phiếu tay — đều là tiền thu ngân phải nộp quỹ (Công nợ thì KHÔNG)</summary>
        public static bool LaTienMat(string hinhThuc) => hinhThuc == TienMat || hinhThuc == TienMatPhieuTay;

        /// <summary>Lượt xe được thu theo hoàn cảnh nào</summary>
        public enum CheDoThu
        {
            /// <summary>Xe qua cổng lúc có mạng: bảo vệ / thu ngân thu tiền mặt ngay tại cổng</summary>
            TaiCong,
            /// <summary>Lượt ghi trễ (mất mạng) KHÔNG kèm tiền mặt: thẻ tháng / thẻ trả trước như thường, còn lại -> Công nợ</summary>
            GhiTre,
            /// <summary>Nhập bù kiểu cũ (OFFLINE, không có số tiền / số phiếu): không biết bảo vệ đã thu chưa -> KHÔNG trừ thẻ
            /// (tránh thu 2 lần), thẻ tháng vẫn miễn, còn lại -> Công nợ chờ đối soát với cuống phiếu tay</summary>
            NhapBuKhongRo
        }

        public class KetQuaThu
        {
            public bool CoThu { get; set; }
            public decimal SoTien { get; set; }
            public string HinhThuc { get; set; }     // Thẻ tháng / Trừ tài khoản / Tiền mặt / Tiền mặt (phiếu tay)
            public decimal? SoDuConLai { get; set; }
            public string ThongBao { get; set; }
            public bool CanhBao { get; set; }        // true = nhac thu ngan de y (het han, thieu tien, lech bieu gia)
        }

        /// <summary>Chuan hoa loai xe ve dung mot muc trong bieu gia</summary>
        public static string NhomLoaiXe(string loaiXe)
        {
            var l = (loaiXe ?? "").ToLowerInvariant();
            if (l.Contains("kéo") || l.Contains("keo") || l.Contains("container")
                || l.Contains("moóc") || l.Contains("mooc") || l.Contains("rơ")) return "Đầu kéo container";
            if (l.Contains("tải lớn") || l.Contains("tai lon") || l.Contains("lớn")) return "Xe tải lớn";
            if (l.Contains("tải nhỏ") || l.Contains("tai nho") || l.Contains("nhỏ")) return "Xe tải nhỏ";
            if (l.Contains("tải") || l.Contains("tai") || l.Contains("truck")) return "Xe tải lớn";
            if (l.Contains("con") || l.Contains("bán tải") || l.Contains("car")) return "Xe con";
            return "Khác";
        }

        public static decimal PhiLuot(AppDb db, string loaiXe)
        {
            var nhom = NhomLoaiXe(loaiXe);
            var bp = db.BieuPhiCongs.AsNoTracking().FirstOrDefault(b => b.KichHoat && b.LoaiXe == nhom);
            return bp != null ? bp.PhiLuot : 0m;
        }

        /// <summary>
        /// Tinh va THU phi cho mot luot ra cong. Ghi thang so tien vao ban ghi
        /// luot xe — sau nay doi soat khong phai tinh lai theo bieu gia hien
        /// tai (bieu gia doi theo thoi gian, tinh lai la ra so khac).
        ///
        /// KHONG goi SaveChanges cho luot xe — de nguoi goi luu mot lan cung cac thay doi khac. Rieng so du the tra truoc
        /// tru NGAY bang mot cau UPDATE nguyen tu (xem TruTheTraTruoc) trong giao dich cua nguoi goi.
        /// </summary>
        public static KetQuaThu Thu(AppDb db, XeRaVao luot, CheDoThu cheDo = CheDoThu.TaiCong)
        {
            var kq = new KetQuaThu();
            if (luot == null || luot.HuongDi != "Ra") return kq;      // chi thu o chieu RA

            var nhom = NhomLoaiXe(luot.LoaiXe);
            var bp = db.BieuPhiCongs.AsNoTracking().FirstOrDefault(b => b.KichHoat && b.LoaiXe == nhom);
            var gia = bp != null ? bp.PhiLuot : 0m;
            // Han the thang xet theo NGAY XE QUA (luot ghi tre dong bo sang hom sau van duoc mien neu luc qua the con han)
            var ngayXet = NgayXet(luot);

            // Doc KHONG theo doi: so du chi duoc doi bang cau UPDATE co dieu kien ben duoi, khong qua ChangeTracker
            // (tru tren entity roi SaveChanges = "UPDATE SoDu = <gia tri tuyet doi>" -> de mat lan nap chay song song)
            var the = db.TheXes.AsNoTracking().FirstOrDefault(t => t.BienSo == luot.BienSo && t.KichHoat);

            // 1. THE THANG con han -> khong thu luot
            if (the != null && the.LoaiThe == LOAI_THE_THANG)
            {
                if (the.HanDen != null && the.HanDen.Value.Date >= ngayXet)
                {
                    luot.PhiCong = 0m;
                    luot.HinhThucThu = TheThang;
                    kq.CoThu = true; kq.SoTien = 0m; kq.HinhThuc = TheThang;
                    var conLai = (the.HanDen.Value.Date - DateTime.Today).Days;
                    if (conLai < 0)
                    {
                        kq.ThongBao = "Lượt ghi trễ ngày " + ngayXet.ToString("dd/MM/yyyy") + " — lúc xe qua thẻ tháng còn hạn, miễn phí lượt. "
                                    + "⚠ Thẻ ĐÃ HẾT HẠN từ " + the.HanDen.Value.ToString("dd/MM/yyyy") + ", nhắc chủ xe gia hạn.";
                        kq.CanhBao = true;
                        return kq;
                    }
                    kq.ThongBao = "Thẻ tháng còn hạn " + conLai + " ngày — miễn phí lượt.";
                    kq.CanhBao = conLai <= 5;
                    if (kq.CanhBao) kq.ThongBao += " ⚠ Sắp hết hạn, nhắc chủ xe gia hạn.";
                    return kq;
                }
                // Het han -> roi xuong thu tien mat, va noi ro ly do
                kq.CanhBao = true;
                kq.ThongBao = "Thẻ tháng ĐÃ HẾT HẠN "
                            + (the.HanDen != null ? "từ " + the.HanDen.Value.ToString("dd/MM/yyyy") : "")
                            + " — thu tiền lượt. ";
            }

            // 2. THE TRA TRUOC -> tru nguyen tu; khong du tien (0 dong duoc cap nhat) -> roi xuong tien mat
            //    Nhap bu kieu cu: KHONG tru the (bao ve co the da thu tien mat bang phieu tay -> tru nua la thu 2 lan)
            if (the != null && the.LoaiThe == LOAI_THE_TRA_TRUOC && gia > 0 && cheDo == CheDoThu.NhapBuKhongRo)
            {
                kq.CanhBao = true;
                kq.ThongBao = (kq.ThongBao ?? "") + "Xe có thẻ trả trước nhưng lượt nhập bù kiểu cũ không cho biết bảo vệ đã thu tiền chưa — "
                            + "KHÔNG trừ thẻ (tránh thu 2 lần). ";
            }
            else if (the != null && the.LoaiThe == LOAI_THE_TRA_TRUOC && gia > 0)
            {
                var tru = TruTheTraTruoc(db, the.MaThe, the.BienSo, luot.MaXV, gia, "Phí cổng " + nhom + (luot.Cong != null ? " · cổng " + luot.Cong : ""));
                if (tru.Kieu != KieuTru.KhongDu)
                {
                    luot.PhiCong = tru.SoTien;
                    luot.HinhThucThu = TruTaiKhoan;
                    kq.CoThu = true; kq.SoTien = tru.SoTien; kq.HinhThuc = TruTaiKhoan;
                    kq.SoDuConLai = tru.SoDu;
                    kq.ThongBao = (kq.ThongBao ?? "")
                                + (tru.Kieu == KieuTru.DaTruTruoc
                                    ? "Lượt này đã trừ " + tru.SoTien.ToString("N0") + "đ từ trước (sổ cái thẻ) — không trừ lần nữa."
                                    : "Trừ " + gia.ToString("N0") + "đ · số dư còn " + tru.SoDu.ToString("N0") + "đ.");
                    if (tru.SoDu < gia * 3) { kq.CanhBao = true; kq.ThongBao += " ⚠ Sắp hết tiền, nhắc nạp thêm."; }
                    return kq;
                }

                kq.CanhBao = true;
                kq.ThongBao = (kq.ThongBao ?? "") + "Tài khoản chỉ còn " + tru.SoDu.ToString("N0")
                            + "đ, không đủ " + gia.ToString("N0") + "đ — thu tiền mặt. ";
                kq.SoDuConLai = tru.SoDu;
            }

            // 3. Thu tien mat theo bieu gia
            if (gia <= 0)
            {
                kq.ThongBao = (kq.ThongBao ?? "") + "Loại xe \"" + nhom + "\" chưa có trong biểu phí — không thu.";
                return kq;
            }
            if (cheDo != CheDoThu.TaiCong)
            {
                // Khong ai thu tien mat luc xe qua (tram mat mang) -> khoan phai thu, KHONG vao quy tien mat cua ca nao
                luot.PhiCong = gia;
                luot.HinhThucThu = CongNo;
                kq.CoThu = true; kq.SoTien = gia; kq.HinhThuc = CongNo;
                kq.CanhBao = true;
                kq.ThongBao = (kq.ThongBao ?? "") + "Ghi CÔNG NỢ " + gia.ToString("N0") + "đ (" + nhom + ") — lượt ghi trễ, lúc xe qua không ai thu tiền mặt; "
                            + (cheDo == CheDoThu.NhapBuKhongRo
                                ? "đối soát với cuống phiếu tay: bảo vệ đã thu thì ghi nhận vào quỹ, chưa thu thì thu khách sau."
                                : "thu khách sau (không tính vào tiền mặt phải nộp quỹ).");
                return kq;
            }
            luot.PhiCong = gia;
            luot.HinhThucThu = TienMat;
            kq.CoThu = true; kq.SoTien = gia; kq.HinhThuc = TienMat;
            kq.ThongBao = (kq.ThongBao ?? "") + "Thu " + gia.ToString("N0") + "đ tiền mặt (" + nhom + ").";
            return kq;
        }

        /// <summary>Ngày xét hạn thẻ tháng: ngày xe qua cổng (lượt ghi trễ có thể xử lý sang hôm sau)</summary>
        private static DateTime NgayXet(XeRaVao luot) =>
            luot.ThoiGian == default || luot.ThoiGian.Date > DateTime.Today ? DateTime.Today : luot.ThoiGian.Date;

        /// <summary>
        /// LUOT GHI TRE kem TIEN MAT DA THU TAI CONG (hop dong H2): tram cong mat mang, bao ve thu tien mat va xe phieu tay
        /// so <paramref name="soPhieuThu"/>. Ghi nhan DUNG so tien da thu (tien da nam trong ngan keo), KHONG tru the lan nua.
        /// Lech bieu gia / xe co the thang / thieu so phieu -> van ghi nhung bat CanhBao de thu ngan doi soat.
        /// KHONG goi SaveChanges.
        /// </summary>
        public static KetQuaThu ThuTienMatTaiCong(AppDb db, XeRaVao luot, decimal tienMat, string soPhieuThu)
        {
            var kq = new KetQuaThu();
            if (luot == null || tienMat <= 0) return kq;

            var nhom = NhomLoaiXe(luot.LoaiXe);
            var gia = PhiLuot(db, luot.LoaiXe);
            var soPhieu = string.IsNullOrWhiteSpace(soPhieuThu) ? null : soPhieuThu.Trim();

            luot.PhiCong = tienMat;
            luot.HinhThucThu = TienMatPhieuTay;
            kq.CoThu = true; kq.SoTien = tienMat; kq.HinhThuc = TienMatPhieuTay;
            kq.ThongBao = "Bảo vệ đã thu " + tienMat.ToString("N0") + "đ tiền mặt tại cổng"
                        + (soPhieu != null ? " (phiếu thu " + soPhieu + ")" : "") + " — ghi nhận vào quỹ, không trừ thẻ.";

            if (soPhieu == null)
            {
                kq.CanhBao = true;
                kq.ThongBao += " ⚠ Thiếu số phiếu thu — đối soát với cuống phiếu tay.";
            }
            if (gia > 0 && tienMat != gia)
            {
                kq.CanhBao = true;
                kq.ThongBao += " ⚠ Lệch biểu giá " + nhom + " (" + gia.ToString("N0") + "đ) — kiểm tra lại với bảo vệ.";
            }
            if (luot.HuongDi != "Ra")
            {
                kq.CanhBao = true;
                kq.ThongBao += " ⚠ Thu ở chiều VÀO (phí cổng thu ở chiều RA) — kiểm tra lượt ra của xe để không thu 2 lần.";
            }
            var the = db.TheXes.AsNoTracking().FirstOrDefault(t => t.BienSo == luot.BienSo && t.KichHoat);
            if (the != null && the.LoaiThe == LOAI_THE_THANG && the.HanDen != null && the.HanDen.Value.Date >= NgayXet(luot))
            {
                kq.CanhBao = true;
                kq.ThongBao += " ⚠ Lúc xe qua, thẻ tháng còn hạn — xem xét hoàn tiền cho khách.";
            }
            return kq;
        }

        // ============================================================
        // TRU THE TRA TRUOC — NGUYEN TU + SO CAI
        // ============================================================

        public enum KieuTru
        {
            /// <summary>Vừa trừ xong</summary>
            DaTru,
            /// <summary>Sổ cái đã có dòng TRU của lượt này — không trừ lần nữa</summary>
            DaTruTruoc,
            /// <summary>Không đủ tiền / thẻ vừa bị khóa (UPDATE 0 dòng) — người gọi thu tiền mặt</summary>
            KhongDu
        }

        public sealed class KetQuaTru
        {
            public KieuTru Kieu { get; set; }
            /// <summary>Số tiền đã trừ (dương)</summary>
            public decimal SoTien { get; set; }
            /// <summary>Số dư thẻ sau thao tác</summary>
            public decimal SoDu { get; set; }
        }

        /// <summary>
        /// TRU PHI VAO THE TRA TRUOC — mot cau UPDATE nguyen tu co dieu kien (khong doc-roi-ghi-de):
        ///   UPDATE TheXe SET SoDu = SoDu - @gia WHERE MaThe = @id AND KichHoat = 1 AND LoaiThe = N'Trả trước' AND SoDu >= @gia
        /// SQL Server khoa dong trong luc cap nhat nen 2 lan tru / tru + nap chay song song khong de len nhau.
        /// 0 dong -> khong du tien -> KhongDu (nguoi goi thu tien mat / cong no).
        /// So cai GiaoDichTheXe (neu da chay Database/Add_GiaoDichTheXe.sql): luot da co dong TRU -> DaTruTruoc, khong tru;
        /// chua co -> ghi dong TRU CUNG GIAO DICH; trung khoa (MaXV, TRU) do 2 lan xu ly dong thoi -> nem loi de HUY
        /// ca giao dich (UPDATE so du cung bi huy) — su kien thu lai se thay dong so cai va khong tru lan 2.
        /// Phai chay trong giao dich (bo phat su kien da mo san); khong co thi tu mo giao dich rieng.
        /// </summary>
        public static KetQuaTru TruTheTraTruoc(AppDb db, int maThe, string bienSo, int maXV, decimal gia, string ghiChu, string nguoi = null)
        {
            if (gia <= 0) throw new ArgumentOutOfRangeException(nameof(gia), "Số tiền trừ phải > 0.");
            var coSoCai = maXV > 0 && SoCaiTheXe.CoBang(db);
            if (coSoCai)
            {
                var cu = SoCaiTheXe.TimTru(db, maXV);
                if (cu != null)
                    return new KetQuaTru { Kieu = KieuTru.DaTruTruoc, SoTien = -cu.SoTien, SoDu = DocSoDu(db, maThe) };
            }

            var giaoDichRieng = db.Database.CurrentTransaction == null ? db.Database.BeginTransaction() : null;
            try
            {
                var soDong = db.TheXes
                    .Where(t => t.MaThe == maThe && t.KichHoat && t.LoaiThe == LOAI_THE_TRA_TRUOC && t.SoDu >= gia)
                    .ExecuteUpdate(s => s.SetProperty(t => t.SoDu, t => t.SoDu - gia));
                // Dong dang bi giao dich nay khoa ghi -> so doc duoc chinh la so du ngay sau lenh tru
                var soDu = DocSoDu(db, maThe);
                if (soDong == 0)
                {
                    giaoDichRieng?.Commit();
                    return new KetQuaTru { Kieu = KieuTru.KhongDu, SoTien = 0m, SoDu = soDu };
                }

                // microservices: bao ban sao TheXe (ExecuteUpdate khong qua ChangeTracker) — giong ben nap tien
                SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.TheXes.Where(t => t.MaThe == maThe));
                if (coSoCai)
                    SoCaiTheXe.Ghi(db, maThe, bienSo, SoCaiTheXe.Tru, -gia, soDu, maXV, ghiChu, nguoi ?? "Hệ thống (phí cổng)");
                giaoDichRieng?.Commit();
                return new KetQuaTru { Kieu = KieuTru.DaTru, SoTien = gia, SoDu = soDu };
            }
            catch (Exception ex) when (SoCaiTheXe.LaTrungKhoa(ex))
            {
                throw new InvalidOperationException("Lượt xe #" + maXV + " vừa được trừ thẻ ở một lần xử lý khác — hủy lần này để không trừ 2 lần "
                                                    + "(sự kiện thử lại sẽ thấy sổ cái thẻ).", ex);
            }
            finally
            {
                giaoDichRieng?.Dispose();
            }
        }

        /// <summary>
        /// NAP TIEN vao the: MOT cau "SoDu = SoDu + nap" (cong don, khong ghi de lan tru dang chay song song) + bao ban sao
        /// + ghi so cai NAP trong CUNG giao dich. Tra so du ngay sau lan nap. Khong co giao dich ngoai thi tu mo.
        /// </summary>
        public static decimal NapThe(AppDb db, int maThe, string bienSo, decimal nap, string ghiChu, string nguoi)
        {
            if (nap <= 0) return DocSoDu(db, maThe);
            var giaoDichRieng = db.Database.CurrentTransaction == null ? db.Database.BeginTransaction() : null;
            try
            {
                db.TheXes.Where(t => t.MaThe == maThe)
                  .ExecuteUpdate(s => s.SetProperty(t => t.SoDu, t => t.SoDu + nap));
                SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.TheXes.Where(t => t.MaThe == maThe));   // microservices: bao ban sao
                var soDu = DocSoDu(db, maThe);     // dong dang bi giao dich nay khoa ghi -> dung so du sau lan nap
                if (SoCaiTheXe.CoBang(db))
                    SoCaiTheXe.Ghi(db, maThe, bienSo, SoCaiTheXe.Nap, nap, soDu, null, ghiChu, nguoi);
                giaoDichRieng?.Commit();
                return soDu;
            }
            finally
            {
                giaoDichRieng?.Dispose();
            }
        }

        private static decimal DocSoDu(AppDb db, int maThe) =>
            db.TheXes.AsNoTracking().Where(t => t.MaThe == maThe).Select(t => (decimal?)t.SoDu).FirstOrDefault() ?? 0m;

        // ============================================================
        // BAO CAO
        // ============================================================

        public class TongKetCa
        {
            public DateTime Tu { get; set; }
            public DateTime Den { get; set; }
            public int SoLuot { get; set; }
            /// <summary>Tiền mặt phải nộp quỹ (gồm cả tiền mặt phiếu tay lúc mất mạng)</summary>
            public decimal TienMat { get; set; }
            /// <summary>Phần tiền mặt thu bằng phiếu tay lúc trạm cổng mất mạng (đã nằm trong TienMat)</summary>
            public decimal TienMatPhieuTay { get; set; }
            public decimal TruTaiKhoan { get; set; }
            /// <summary>Khoản phải thu của lượt ghi trễ chưa ai thu tiền (KHÔNG nằm trong TienMat, KHÔNG nằm trong TongThu)</summary>
            public decimal CongNo { get; set; }
            public int LuotTheThang { get; set; }
            /// <summary>Đã thu thật: tiền mặt + trừ tài khoản (công nợ chưa thu thì chưa tính)</summary>
            public decimal TongThu { get; set; }
            /// <summary>Tổng phát sinh trong ca = đã thu + công nợ chờ thu</summary>
            public decimal TongPhatSinh { get; set; }
            public List<DongLoaiXe> TheoLoaiXe { get; set; }
            public TongKetCa() { TheoLoaiXe = new List<DongLoaiXe>(); }
        }

        public class DongLoaiXe
        {
            public string LoaiXe { get; set; }
            public int SoLuot { get; set; }
            public decimal ThanhTien { get; set; }
        }

        /// <summary>
        /// Tong ket thu phi trong mot khoang — thu ngan giao ca phai in cai nay.
        /// Chi tinh TIEN MAT (ke ca phieu tay luc mat mang) vao "phai nop", vi tru tai khoan da thu tu truoc.
        /// </summary>
        public static TongKetCa TongKet(AppDb db, DateTime tu, DateTime den)
        {
            var ds = db.XeRaVaos.AsNoTracking()
                       .Where(x => x.ThoiGian >= tu && x.ThoiGian <= den && x.HinhThucThu != null)
                       .ToList();

            var tk = new TongKetCa
            {
                Tu = tu,
                Den = den,
                SoLuot = ds.Count,
                TienMat = ds.Where(x => LaTienMat(x.HinhThucThu)).Sum(x => x.PhiCong ?? 0m),
                TienMatPhieuTay = ds.Where(x => x.HinhThucThu == TienMatPhieuTay).Sum(x => x.PhiCong ?? 0m),
                TruTaiKhoan = ds.Where(x => x.HinhThucThu == TruTaiKhoan).Sum(x => x.PhiCong ?? 0m),
                CongNo = ds.Where(x => x.HinhThucThu == CongNo).Sum(x => x.PhiCong ?? 0m),
                LuotTheThang = ds.Count(x => x.HinhThucThu == TheThang)
            };
            tk.TongThu = tk.TienMat + tk.TruTaiKhoan;
            tk.TongPhatSinh = tk.TongThu + tk.CongNo;
            tk.TheoLoaiXe = ds.GroupBy(x => NhomLoaiXe(x.LoaiXe))
                              .Select(g => new DongLoaiXe
                              {
                                  LoaiXe = g.Key,
                                  SoLuot = g.Count(),
                                  ThanhTien = g.Sum(x => x.PhiCong ?? 0m)
                              })
                              .OrderByDescending(d => d.ThanhTien).ToList();
            return tk;
        }
    }

    /// <summary>
    /// SO CAI THE XE — bang CUC BO GiaoDichTheXe cua dich vu Tai chinh (Database/Add_GiaoDichTheXe.sql): moi lan
    /// NAP (+) / TRU (-) ghi THEM mot dong kem so du sau giao dich; trigger chan sua / xoa. UNIQUE (MaXV, Loai) chong tru 2 lan.
    /// Ghi / doc bang SQL tho tham so hoa — KHONG them DbSet vao AppDb (bang chi co o CSDL Tai chinh, khong sao chep).
    /// CSDL chua chay script (vd ban gop tren somee) -> CoBang = false: ghi log MOT lan, nghiep vu chay nhu cu.
    /// </summary>
    public static class SoCaiTheXe
    {
        public const string Nap = "NAP";
        public const string Tru = "TRU";

        /// <summary>Một dòng sổ cái (tên cột trùng tên thuộc tính để SqlQuery ánh xạ)</summary>
        public sealed class DongSoCai
        {
            public long MaGD { get; set; }
            public int MaThe { get; set; }
            public string BienSo { get; set; }
            public string Loai { get; set; }
            public decimal SoTien { get; set; }
            public decimal SoDuSau { get; set; }
            public int? MaXV { get; set; }
            public string GhiChu { get; set; }
            public string NguoiTao { get; set; }
            public DateTime LucTao { get; set; }
        }

        // Ket qua kiem tra bang theo tung CSDL: co -> nho mai; chua co -> 2 phut kiem lai (chay script xong khong can khoi dong lai)
        private static readonly ConcurrentDictionary<string, (bool Co, DateTime KiemLuc)> daKiem =
            new ConcurrentDictionary<string, (bool, DateTime)>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> daBaoThieu = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan KiemLaiSau = TimeSpan.FromMinutes(2);

        /// <summary>CSDL đang dùng đã có bảng GiaoDichTheXe chưa (có bộ nhớ đệm, không làm lỗi nghiệp vụ)</summary>
        public static bool CoBang(AppDb db)
        {
            string khoa;
            try
            {
                var cn = db.Database.GetDbConnection();
                khoa = cn.DataSource + "|" + cn.Database;
            }
            catch
            {
                return false;
            }
            if (daKiem.TryGetValue(khoa, out var c) && (c.Co || DateTime.UtcNow - c.KiemLuc < KiemLaiSau)) return c.Co;

            // Truy van LOI (het gio, deadlock, mat ket noi) -> KHONG nho "chua co bang" va NEM loi: nuot loi o day tung tat so cai
            // + chong tru 2 lan suot 2 phut ma khong ai biet. Loi SQL tam thoi -> su kien thu lai (PhanLoaiLoi), API tra 500.
            var co = db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID(N'dbo.GiaoDichTheXe', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
                       .ToList().FirstOrDefault() == 1;
            daKiem[khoa] = (co, DateTime.UtcNow);
            if (!co && daBaoThieu.TryAdd(khoa, true))
            {
                var tb = "Chưa có bảng sổ cái GiaoDichTheXe ở CSDL " + khoa.Split('|').Last()
                       + " — trừ / nạp thẻ vẫn nguyên tử nhưng không ghi sổ cái. Chạy Database/Add_GiaoDichTheXe.sql để bật.";
                Console.Error.WriteLine("[CANH BAO] " + tb);
                NhatKyHelper.Ghi("Loi", null, tb);
            }
            else if (co) daBaoThieu.TryRemove(khoa, out _);
            return co;
        }

        /// <summary>Dòng TRU đã ghi cho lượt xe (null = chưa trừ)</summary>
        public static DongSoCai TimTru(AppDb db, int maXV) =>
            db.Database.SqlQuery<DongSoCai>(
                    $"SELECT TOP (1) MaGD, MaThe, BienSo, Loai, SoTien, SoDuSau, MaXV, GhiChu, NguoiTao, LucTao FROM dbo.GiaoDichTheXe WHERE MaXV = {maXV} AND Loai = 'TRU'")
                .ToList().FirstOrDefault();

        /// <summary>Sổ cái của một thẻ, mới nhất trước (tối đa <paramref name="toiDa"/> dòng). Chưa có bảng → danh sách rỗng.</summary>
        public static List<DongSoCai> TheoThe(AppDb db, int maThe, int toiDa = 200)
        {
            if (!CoBang(db)) return new List<DongSoCai>();
            toiDa = Math.Clamp(toiDa, 1, 1000);
            return db.Database.SqlQuery<DongSoCai>(
                    $"SELECT TOP ({toiDa}) MaGD, MaThe, BienSo, Loai, SoTien, SoDuSau, MaXV, GhiChu, NguoiTao, LucTao FROM dbo.GiaoDichTheXe WHERE MaThe = {maThe} ORDER BY MaGD DESC")
                .ToList();
        }

        /// <summary>
        /// Ghi thêm một dòng (NAP: soTien > 0, MaXV null; TRU: soTien &lt; 0, có MaXV). Chạy trong giao dịch của người gọi.
        /// Trùng (MaXV, Loai) → SqlException 2601 (người gọi bắt bằng LaTrungKhoa).
        /// </summary>
        public static void Ghi(AppDb db, int maThe, string bienSo, string loai, decimal soTien, decimal soDuSau, int? maXV, string ghiChu, string nguoi)
        {
            var bs = Cat(bienSo, 20);
            var gc = Cat(ghiChu, 300);
            var ng = Cat(nguoi, 100);
            db.Database.ExecuteSql(
                $"INSERT INTO dbo.GiaoDichTheXe (MaThe, BienSo, Loai, SoTien, SoDuSau, MaXV, GhiChu, NguoiTao) VALUES ({maThe}, {bs}, {loai}, {soTien}, {soDuSau}, {maXV}, {gc}, {ng})");
        }

        /// <summary>Lỗi vi phạm khóa duy nhất (2601 / 2627) — ở đây nghĩa là lượt đã bị trừ ở lần xử lý khác</summary>
        public static bool LaTrungKhoa(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                if (e is SqlException sql && (sql.Number == 2601 || sql.Number == 2627)) return true;
            return false;
        }

        private static string Cat(string s, int n) =>
            string.IsNullOrWhiteSpace(s) ? null : (s = s.Trim()).Length <= n ? s : s.Substring(0, n);
    }
}
