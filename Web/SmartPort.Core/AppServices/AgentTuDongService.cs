using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ================= AGENT TỰ HÀNH ĐỘNG =================
    ///
    /// KHAC GI VOI AgentService CU:
    ///   AgentService  : PERCEIVE → REASON → PLAN → *đề xuất* (người bấm nút)
    ///   AgentTuDong   : PERCEIVE → REASON → PLAN → **ACT** (agent tự gọi
    ///                   nghiệp vụ của chính hệ thống: lập phiếu thu, sinh
    ///                   bản tin EDI, mở sự cố an ninh...)
    ///
    /// MOI KY NANG LA MOT "CONG CU" agent duoc phep goi. Moi lan goi deu:
    ///   - ghi LY DO doc duoc (khong phai "AI quyết định vậy")
    ///   - ghi lai BANG + KHOA de HOAN TAC
    ///   - bi chan boi ToiDaMoiLan (agent loi khong the tao 500 phieu)
    ///   - tuan theo CHE DO: Tắt / Xin phép / Tự làm
    ///
    /// VI SAO QUYET DINH BANG LUAT CHU KHONG BANG MO HINH NGON NGU:
    /// agent nay dung toi TIEN cua khach (lap phieu thu) va toi bao cao gui
    /// hang tau. Tien khong duoc phep "co ve dung". Luat thi giai trinh duoc
    /// tung dong khi bi hoi. Mo hinh ngon ngu chi nen dung de VIET NOI DUNG
    /// (soan email nhac no), khong dung de QUYET DINH co doi tien hay khong.
    /// </summary>
    public static class AgentTuDongService
    {
        /// <summary>Mot viec agent da lam (hoac de nghi lam)</summary>
        public class KetQuaChay
        {
            public int DaLam { get; set; }
            public int ChoDuyet { get; set; }
            public int BoQua { get; set; }
            public int Loi { get; set; }
            public List<string> ChiTiet { get; set; }
            public KetQuaChay() { ChiTiet = new List<string>(); }
        }

        // ============================================================
        // BO DIEU PHOI
        // ============================================================

        /// <summary>
        /// Chay het cac ky nang dang bat. Goi tu nut "Chạy ngay" hoac tu
        /// tien trinh nen theo lich.
        /// </summary>
        public static KetQuaChay ChayTatCa(AppDb db, string nguoiChay, string chiKyNang = null)
        {
            var kq = new KetQuaChay();
            var dsCauHinh = db.CauHinhAgents.ToList();

            foreach (var ch in dsCauHinh)
            {
                if (chiKyNang != null && ch.KyNang != chiKyNang) continue;
                if (ch.CheDo == "Tắt") continue;

                try
                {
                    int lam = 0, cho = 0;
                    switch (ch.KyNang)
                    {
                        case "ChotPhiLuuBai": ChotPhiLuuBai(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "SinhCodeco": SinhCodeco(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "NhacNoQuaHan": NhacNoQuaHan(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "CanhBaoTheXe": CanhBaoTheXe(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "GiucDuyetEor": GiucDuyetEor(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "CanhBaoTonLau": CanhBaoTonLau(db, ch, nguoiChay, ref lam, ref cho); break;
                        case "ChanGplxHetHan": ChanGplxHetHan(db, ch, nguoiChay, ref lam, ref cho); break;
                        default: continue;
                    }
                    ch.LanChayCuoi = DateTime.Now;
                    kq.DaLam += lam; kq.ChoDuyet += cho;
                    if (lam + cho > 0)
                        kq.ChiTiet.Add(ch.TenHienThi + ": " + (lam > 0 ? "đã làm " + lam : "")
                            + (lam > 0 && cho > 0 ? ", " : "") + (cho > 0 ? "chờ duyệt " + cho : ""));
                }
                catch (Exception ex)
                {
                    kq.Loi++;
                    kq.ChiTiet.Add(ch.TenHienThi + ": LỖI — " + ex.GetBaseException().Message);
                    Ghi(db, nguoiChay, ch.KyNang, null, "Lỗi khi chạy kỹ năng", null,
                        ex.GetBaseException().Message, "Lỗi", false, null, null);
                }
            }
            db.SaveChanges();
            return kq;
        }

        private static NhatKyAgent Ghi(AppDb db, string nguoiChay, string kyNang, string doiTuong,
                                       string lyDo, string hanhDong, string ketQua, string trangThai,
                                       bool tuDong, string bang, int? ma)
        {
            Func<string, int, string> cat = (x, n) => x != null && x.Length > n ? x.Substring(0, n) : x;
            var nk = new NhatKyAgent
            {
                Luc = DateTime.Now,
                KyNang = kyNang,
                DoiTuong = cat(doiTuong, 100),
                LyDo = cat(lyDo, 600),
                HanhDong = cat(hanhDong, 400),
                KetQua = cat(ketQua, 400),
                TrangThai = trangThai,
                TuDong = tuDong,
                BangThamChieu = bang,
                MaThamChieu = ma,
                NguoiChay = cat(nguoiChay, 100)
            };
            db.NhatKyAgents.Add(nk);
            return nk;
        }

        /// <summary>Da co viec y het dang cho hoac vua lam roi thi thoi — tranh nhac lai moi lan chay</summary>
        private static bool DaCo(AppDb db, string kyNang, string doiTuong, int soGioIm = 24)
        {
            var moc = DateTime.Now.AddHours(-Math.Abs(soGioIm));
            return db.NhatKyAgents.Any(n => n.KyNang == kyNang && n.DoiTuong == doiTuong
                                         && !n.DaHoanTac && n.TrangThai != "Đã hủy" && n.Luc >= moc);
        }

        // ============================================================
        // KY NANG 1 — CHOT PHI LUU BAI  (hanh dong THAT: lap phieu thu)
        // ============================================================

        private static void ChotPhiLuuBai(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var ds = LuuBaiService.BangKeDangChay(db)
                     .Where(d => d.Phi.TongTien > 0 && !d.DaCoPhieu)
                     .OrderByDescending(d => d.Phi.TongTien)
                     .Take(ch.ToiDaMoiLan)
                     .ToList();

            foreach (var d in ds)
            {
                if (DaCo(db, ch.KyNang, d.Cont.SoCont)) continue;

                var lyDo = "Container " + d.Cont.SoCont + " đã nằm bãi " + d.Phi.TongNgay
                         + " ngày, vượt " + d.Phi.NgayTinhPhi + " ngày so với hạn miễn phí "
                         + d.Phi.NgayMienPhi + " ngày. Chưa có phiếu thu nào. Số tiền đang treo: "
                         + d.Phi.TongTien.ToString("N0") + "đ"
                         + (d.TenKhach != null ? " (khách: " + d.TenKhach + ")" : "") + ".";

                if (!ch.TuLam)
                {
                    Ghi(db, nguoi, ch.KyNang, d.Cont.SoCont, lyDo,
                        "Đề nghị lập phiếu thu " + d.Phi.TongTien.ToString("N0") + "đ", null,
                        "Chờ duyệt", false, "Container", d.Cont.MaCont);
                    cho++;
                    continue;
                }

                var cont = db.Containers.Find(d.Cont.MaCont);
                var p = LuuBaiService.LapPhieu(db, cont, "Agent tự động");
                if (p == null) continue;

                Ghi(db, nguoi, ch.KyNang, d.Cont.SoCont, lyDo,
                    "Lập phiếu thu " + p.SoPhieu, p.TongTien.ToString("N0") + "đ · chờ thu",
                    "Đã làm", true, "PhieuLuuBai", p.MaPhieu);
                lam++;
            }
        }

        // ============================================================
        // KY NANG 2 — SINH CODECO  (hanh dong THAT: sinh ban tin EDI)
        // ============================================================

        private static void SinhCodeco(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var nguong = Math.Max(1, ch.Nguong);

            // Gom luot CHUA bao cao, nhom theo hang tau so huu container
            var chuaBao = db.XeRaVaos.Where(x => x.MaBT == null && x.SoCont != null).ToList();
            if (chuaBao.Count == 0) return;

            var soConts = chuaBao.Select(x => x.SoCont).Distinct().ToList();
            var contHang = db.Containers.Where(c => soConts.Contains(c.SoCont) && c.MaHangTau != null)
                             .ToDictionary(c => c.SoCont, c => c.MaHangTau.Value);

            var theoHang = chuaBao.Where(x => contHang.ContainsKey(x.SoCont))
                                  .GroupBy(x => contHang[x.SoCont])
                                  .Where(g => g.Count() >= nguong)
                                  .Take(ch.ToiDaMoiLan)
                                  .ToList();

            foreach (var g in theoHang)
            {
                var hang = db.HangTaus.Find(g.Key);
                if (hang == null) continue;
                if (DaCo(db, ch.KyNang, hang.TenHangTau, 12)) continue;

                var lyDo = "Hãng " + hang.TenHangTau + " có " + g.Count()
                         + " lượt container vào/ra chưa báo cáo (ngưỡng " + nguong
                         + "). Lượt cũ nhất từ " + g.Min(x => x.ThoiGian).ToString("HH:mm dd/MM")
                         + ". Depot giữ vỏ hộ hãng tàu nên bắt buộc phải báo.";

                if (!ch.TuLam)
                {
                    Ghi(db, nguoi, ch.KyNang, hang.TenHangTau, lyDo,
                        "Đề nghị sinh CODECO cho " + g.Count() + " lượt", null,
                        "Chờ duyệt", false, "HangTau", hang.MaHangTau);
                    cho++;
                    continue;
                }

                var luot = g.OrderBy(x => x.ThoiGian).Take(500).ToList();
                // Dung chung CodecoHelper (ban gop: lap ngay; microservices: gui lenh cho dich vu Tich hop)
                var bt = CodecoHelper.LapHoacYeuCau(db, hang, luot, "SMARTPORT", "Agent tự động", null);
                if (bt == null)
                    Ghi(db, nguoi, ch.KyNang, hang.TenHangTau, lyDo,
                        "Gửi dịch vụ Tích hợp sinh CODECO cho " + luot.Count + " lượt", "Tích hợp lập bản tin trong vài giây",
                        "Đã làm", true, "HangTau", hang.MaHangTau);
                else
                    Ghi(db, nguoi, ch.KyNang, hang.TenHangTau, lyDo,
                        "Sinh bản tin CODECO " + bt.SoChungTu,
                        bt.SoLuongCont + " container · chờ gửi cho hãng tàu",
                        "Đã làm", true, "BanTinEDI", bt.MaBT);
                lam++;
            }
        }

        // ============================================================
        // KY NANG 3 — NHAC NO QUA HAN
        // ============================================================

        private static void NhacNoQuaHan(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var nguong = Math.Max(1, ch.Nguong);
            var moc = DateTime.Now.AddDays(-nguong);
            var ds = db.PhieuLuuBais.Include(p => p.KhachHang)
                       .Where(p => p.TrangThai == "Chờ thu" && p.NgayLap <= moc)
                       .OrderBy(p => p.NgayLap)
                       .Take(ch.ToiDaMoiLan).ToList();

            foreach (var p in ds)
            {
                if (DaCo(db, ch.KyNang, p.SoPhieu, 24 * 7)) continue;   // 1 tuần nhắc 1 lần

                var soNgay = (int)(DateTime.Now - p.NgayLap).TotalDays;
                var khach = p.KhachHang != null ? p.KhachHang.TenCongTy : "khách hàng";
                var lyDo = "Phiếu " + p.SoPhieu + " (" + p.TongTien.ToString("N0") + "đ, container "
                         + p.SoCont + ") lập ngày " + p.NgayLap.ToString("dd/MM/yyyy")
                         + ", đã treo " + soNgay + " ngày > ngưỡng " + nguong + " ngày mà chưa thu được.";
                var noiDung = "Kính gửi " + khach + ", phiếu thu phí lưu bãi " + p.SoPhieu
                            + " cho container " + p.SoCont + " số tiền " + p.TongTien.ToString("N0")
                            + "đ đã quá hạn " + soNgay + " ngày. Kính đề nghị quý công ty thanh toán.";

                Ghi(db, nguoi, ch.KyNang, p.SoPhieu, lyDo,
                    "Soạn nội dung nhắc nợ gửi " + khach, noiDung,
                    ch.TuLam ? "Đã làm" : "Chờ duyệt", ch.TuLam, "PhieuLuuBai", p.MaPhieu);
                if (ch.TuLam) lam++; else cho++;
            }
        }

        // ============================================================
        // KY NANG 4 — CANH BAO THE XE
        // ============================================================

        private static void CanhBaoTheXe(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var nguong = Math.Max(1, ch.Nguong);
            var hanCuoi = DateTime.Today.AddDays(nguong);
            var giaTB = db.BieuPhiCongs.Where(b => b.KichHoat).Select(b => (decimal?)b.PhiLuot).Average() ?? 50000m;
            var nguongTien = giaTB * 3;

            var ds = db.TheXes.Where(t => t.KichHoat &&
                        ((t.LoaiThe == "Tháng" && t.HanDen != null && t.HanDen <= hanCuoi)
                      || (t.LoaiThe == "Trả trước" && t.SoDu < nguongTien)))
                       .Take(ch.ToiDaMoiLan).ToList();

            foreach (var t in ds)
            {
                if (DaCo(db, ch.KyNang, t.BienSo, 24 * 3)) continue;

                string lyDo, hanhDong;
                if (t.LoaiThe == "Tháng")
                {
                    var conLai = (t.HanDen.Value.Date - DateTime.Today).Days;
                    lyDo = "Thẻ tháng xe " + t.BienSo + " " + (conLai < 0
                         ? "ĐÃ HẾT HẠN " + (-conLai) + " ngày trước"
                         : "còn " + conLai + " ngày là hết hạn") + " (" + t.HanDen.Value.ToString("dd/MM/yyyy") + ").";
                    hanhDong = "Nhắc thu ngân gọi " + (t.TenChuXe ?? "chủ xe")
                             + (t.DienThoai != null ? " (" + t.DienThoai + ")" : "") + " gia hạn";
                }
                else
                {
                    lyDo = "Thẻ trả trước xe " + t.BienSo + " chỉ còn " + t.SoDu.ToString("N0")
                         + "đ, dưới ngưỡng " + nguongTien.ToString("N0") + "đ (≈3 lượt). Hết tiền là phải thu tiền mặt.";
                    hanhDong = "Nhắc thu ngân gọi " + (t.TenChuXe ?? "chủ xe") + " nạp thêm";
                }

                Ghi(db, nguoi, ch.KyNang, t.BienSo, lyDo, hanhDong, null,
                    ch.TuLam ? "Đã làm" : "Chờ duyệt", ch.TuLam, "TheXe", t.MaThe);
                if (ch.TuLam) lam++; else cho++;
            }
        }

        // ============================================================
        // KY NANG 5 — GIUC DUYET EOR
        // ============================================================

        private static void GiucDuyetEor(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var nguong = Math.Max(1, ch.Nguong);
            var moc = DateTime.Now.AddDays(-nguong);
            var ds = db.PhieuSuaChuas.Include(e => e.HangTau)
                       .Where(e => e.TrangThai == "Chờ duyệt" && e.NgayLap <= moc)
                       .OrderBy(e => e.NgayLap).Take(ch.ToiDaMoiLan).ToList();

            foreach (var e in ds)
            {
                if (DaCo(db, ch.KyNang, e.SoEOR, 24 * 5)) continue;
                var soNgay = (int)(DateTime.Now - e.NgayLap).TotalDays;
                var hang = e.HangTau != null ? e.HangTau.TenHangTau : "hãng tàu";
                var lyDo = "EOR " + e.SoEOR + " (container " + e.SoCont + ", "
                         + e.TongChiPhi.ToString("N0") + "đ) gửi " + hang + " từ "
                         + e.NgayLap.ToString("dd/MM/yyyy") + ", đã " + soNgay
                         + " ngày chưa được duyệt. Vỏ chưa sửa được, tiền chưa thu được.";

                Ghi(db, nguoi, ch.KyNang, e.SoEOR, lyDo,
                    "Nhắc gửi lại EOR cho " + hang, null,
                    ch.TuLam ? "Đã làm" : "Chờ duyệt", ch.TuLam, "PhieuSuaChua", e.MaEOR);
                if (ch.TuLam) lam++; else cho++;
            }
        }

        // ============================================================
        // KY NANG 6 — CANH BAO CONTAINER TON LAU
        // ============================================================

        private static void CanhBaoTonLau(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var nguong = Math.Max(1, ch.Nguong);
            var ds = LuuBaiService.BangKeDangChay(db)
                     .Where(d => d.Phi.TongNgay > nguong)
                     .OrderByDescending(d => d.Phi.TongNgay)
                     .Take(ch.ToiDaMoiLan).ToList();

            foreach (var d in ds)
            {
                if (DaCo(db, ch.KyNang, d.Cont.SoCont, 24 * 7)) continue;
                var lyDo = "Container " + d.Cont.SoCont + " nằm bãi " + d.Phi.TongNgay
                         + " ngày (> " + nguong + "), chiếm chỗ và đang treo "
                         + d.Phi.TongTien.ToString("N0") + "đ tiền lưu bãi"
                         + (d.TenKhach != null ? ". Khách: " + d.TenKhach : "") + ".";

                Ghi(db, nguoi, ch.KyNang, d.Cont.SoCont, lyDo,
                    "Đề nghị gọi khách lấy hàng ra / xác nhận kế hoạch", null,
                    ch.TuLam ? "Đã làm" : "Chờ duyệt", ch.TuLam, "Container", d.Cont.MaCont);
                if (ch.TuLam) lam++; else cho++;
            }
        }

        // ============================================================
        // KY NANG 7 — TAI XE HET HAN GPLX  (hanh dong THAT: mo su co)
        // ============================================================

        private static void ChanGplxHetHan(AppDb db, CauHinhAgent ch, string nguoi, ref int lam, ref int cho)
        {
            var homNay = DateTime.Today;
            var moc = DateTime.Now.AddDays(-7);
            var ds = db.TaiXes.Where(t => t.HanGPLX != null && t.HanGPLX < homNay
                                       && t.LanVaoCuoi != null && t.LanVaoCuoi >= moc)
                       .Take(ch.ToiDaMoiLan).ToList();

            foreach (var t in ds)
            {
                if (DaCo(db, ch.KyNang, t.SoGiayTo, 24 * 7)) continue;
                var quaHan = (homNay - t.HanGPLX.Value.Date).Days;
                var lyDo = "Tài xế " + (t.HoTen ?? t.SoGiayTo) + " (xe " + (t.BienSo ?? "?")
                         + ") có GPLX hết hạn từ " + t.HanGPLX.Value.ToString("dd/MM/yyyy")
                         + " — quá " + quaHan + " ngày, nhưng vẫn vào cổng lúc "
                         + t.LanVaoCuoi.Value.ToString("HH:mm dd/MM") + ". Đây là rủi ro pháp lý cho cảng.";

                if (!ch.TuLam)
                {
                    Ghi(db, nguoi, ch.KyNang, t.SoGiayTo, lyDo, "Đề nghị mở sự cố an ninh", null,
                        "Chờ duyệt", false, "TaiXe", t.MaTX);
                    cho++;
                    continue;
                }

                var sc = new SuCoAnNinh
                {
                    Loai = "Khac", MucDo = "Chu y",
                    ViTri = "Cổng — xe " + (t.BienSo ?? "?"),
                    MoTa = lyDo, TrangThai = "Mới", ThoiGian = DateTime.Now
                };
                BuildingBlocks.SaoChep.GhiHo.Them(db, sc);    // su co thuoc dich vu An ninh (microservices: gui lenh ghi ho)
                db.SaveChanges();

                Ghi(db, nguoi, ch.KyNang, t.SoGiayTo, lyDo, "Mở sự cố an ninh",
                    "Sự cố #" + sc.MaSC + " · mức Chú ý", "Đã làm", true, "SuCoAnNinh", sc.MaSC);
                lam++;
            }
        }

        // ============================================================
        // DUYET / HUY / HOAN TAC
        // ============================================================

        /// <summary>Nguoi duyet mot de xuat "Chờ duyệt" -> agent thuc thi that</summary>
        public static string Duyet(AppDb db, int maNK, string nguoi)
        {
            var nk = db.NhatKyAgents.Find(maNK);
            if (nk == null) return "Không tìm thấy việc.";
            if (nk.TrangThai != "Chờ duyệt") return "Việc này không ở trạng thái chờ duyệt.";

            var ch = db.CauHinhAgents.FirstOrDefault(c => c.KyNang == nk.KyNang);
            var toiDa = ch != null ? ch.ToiDaMoiLan : 20;
            string ketQua;

            switch (nk.KyNang)
            {
                case "ChotPhiLuuBai":
                    {
                        var cont = nk.MaThamChieu != null ? db.Containers.Find(nk.MaThamChieu.Value) : null;
                        if (cont == null) return "Không còn container này.";
                        var p = LuuBaiService.LapPhieu(db, cont, nguoi);
                        if (p == null) { nk.TrangThai = "Đã hủy"; nk.KetQua = "Không còn phát sinh phí."; db.SaveChanges(); return nk.KetQua; }
                        nk.BangThamChieu = "PhieuLuuBai"; nk.MaThamChieu = p.MaPhieu;
                        ketQua = "Đã lập phiếu " + p.SoPhieu + " — " + p.TongTien.ToString("N0") + "đ";
                        break;
                    }
                case "SinhCodeco":
                    {
                        int lam = 0, cho = 0;
                        if (ch != null)
                        {
                            var tam = new CauHinhAgent
                            {
                                KyNang = ch.KyNang, CheDo = "Tự làm",
                                Nguong = ch.Nguong, ToiDaMoiLan = toiDa
                            };
                            SinhCodeco(db, tam, nguoi, ref lam, ref cho);
                        }
                        ketQua = lam > 0 ? "Đã sinh " + lam + " bản tin CODECO" : "Không còn lượt nào cần báo cáo.";
                        break;
                    }
                default:
                    // Cac ky nang con lai la viec cua NGUOI (goi dien, nhac no) —
                    // duyet nghia la xac nhan da lam, khong co gi de he thong thuc thi
                    ketQua = "Đã xác nhận xử lý.";
                    break;
            }

            nk.TrangThai = "Đã làm";
            nk.KetQua = ketQua;
            nk.NguoiXuLy = nguoi;
            nk.LucXuLy = DateTime.Now;
            db.SaveChanges();
            return ketQua;
        }

        public static string Huy(AppDb db, int maNK, string nguoi)
        {
            var nk = db.NhatKyAgents.Find(maNK);
            if (nk == null) return "Không tìm thấy việc.";
            nk.TrangThai = "Đã hủy";
            nk.NguoiXuLy = nguoi;
            nk.LucXuLy = DateTime.Now;
            db.SaveChanges();
            return "Đã bỏ qua việc này.";
        }

        /// <summary>
        /// HOAN TAC thu agent da tao. Day la thu bat buoc phai co: giao viec
        /// cho may lam thi phai co duong lui khi no lam sai.
        /// </summary>
        public static string HoanTac(AppDb db, int maNK, string nguoi)
        {
            var nk = db.NhatKyAgents.Find(maNK);
            if (nk == null) return "Không tìm thấy việc.";
            if (nk.DaHoanTac) return "Việc này đã hoàn tác rồi.";
            if (nk.MaThamChieu == null || string.IsNullOrEmpty(nk.BangThamChieu))
                return "Việc này không tạo ra bản ghi nào để hoàn tác.";

            string kq;
            switch (nk.BangThamChieu)
            {
                case "PhieuLuuBai" when !BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("PhieuLuuBai"):
                    return "Phiếu lưu bãi thuộc dịch vụ Tài chính — hủy phiếu tại màn hình Thu phí lưu bãi.";
                case "BanTinEDI" when !BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("BanTinEDI"):
                    return "Bản tin EDI thuộc dịch vụ Tích hợp — xóa bản tin tại màn hình EDI.";
                case "SuCoAnNinh" when !BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("SuCoAnNinh"):
                    return "Sự cố thuộc dịch vụ An ninh — xử lý tại màn hình Phòng an ninh.";
                case "PhieuLuuBai":
                    {
                        var p = db.PhieuLuuBais.Find(nk.MaThamChieu.Value);
                        if (p == null) return "Phiếu đã bị xóa trước đó.";
                        if (p.TrangThai == "Đã thu") return "Phiếu đã thu tiền — không hoàn tác được. Lập phiếu điều chỉnh nếu cần.";
                        p.TrangThai = "Hủy";
                        p.GhiChu = "Hoàn tác việc agent tự làm, bởi " + nguoi;
                        kq = "Đã hủy phiếu " + p.SoPhieu + ".";
                        break;
                    }
                case "BanTinEDI":
                    {
                        var bt = db.BanTinEDIs.Find(nk.MaThamChieu.Value);
                        if (bt == null) return "Bản tin đã bị xóa trước đó.";
                        if (bt.TrangThai == "Đã gửi") return "Bản tin đã gửi cho hãng tàu — không hoàn tác được.";
                        // Tra cac luot ve trang thai CHUA bao cao, khong de ket vinh vien
                        var luot = db.XeRaVaos.Where(x => x.MaBT == bt.MaBT).ToList();
                        CodecoHelper.GanLuot(db, luot, null);
                        db.BanTinEDIs.Remove(bt);
                        kq = "Đã xóa bản tin " + bt.SoChungTu + ", trả " + luot.Count + " lượt về chưa báo cáo.";
                        break;
                    }
                case "SuCoAnNinh":
                    {
                        var sc = db.SuCoAnNinhs.Find(nk.MaThamChieu.Value);
                        if (sc == null) return "Sự cố đã bị xóa trước đó.";
                        db.SuCoAnNinhs.Remove(sc);
                        kq = "Đã xóa sự cố #" + nk.MaThamChieu.Value + ".";
                        break;
                    }
                default:
                    return "Loại bản ghi này chưa hỗ trợ hoàn tác.";
            }

            nk.DaHoanTac = true;
            nk.NguoiXuLy = nguoi;
            nk.LucXuLy = DateTime.Now;
            nk.KetQua = (nk.KetQua ?? "") + " · HOÀN TÁC: " + kq;
            db.SaveChanges();
            return kq;
        }
    }
}
