using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ================= TRỢ LÝ NGHIỆP VỤ =================
    ///
    /// KHAC GI VOI HAI THU DA CO:
    ///   ChatbotService     : khach vang lai hoi -> TRA LOI (tra cuu cong khai)
    ///   AgentTuDongService : TU DANH THUC theo lich -> tu lam viec
    ///   TroLyService       : NHAN VIEN NHO -> lam viec ngay, ngay trong luc lam
    ///
    /// Nhan vien go mot cau tieng Viet binh thuong, tro ly chon dung CONG CU roi
    /// goi thang nghiep vu co san. Khong phai hoc menu, khong phai nho man hinh.
    ///
    /// BA MUC AN TOAN — ranh gioi quan trong nhat cua ca file nay:
    ///   · DOC   : tra cuu, tinh toan             -> lam ngay
    ///   · GHI   : doi trang thai, lap phieu, gui  -> HOI XAC NHAN, ghi nhat ky
    ///   · CAM   : xoa du lieu, doi phan quyen, doi bieu gia, doi cau hinh
    ///             -> tro ly KHONG CO cong cu nay, co xin cung khong lam.
    ///             Mot cau go voi khong duoc phep xoa lich su cua doanh nghiep.
    ///
    /// VI SAO NHAN DIEN Y DINH BANG LUAT CHU KHONG BANG MO HINH NGON NGU:
    /// tro ly nay dong vao tien that. Luat thi giai thich duoc "vi sao hieu cau
    /// nay thanh viec kia"; mo hinh ngon ngu thi khong. Va depot mat mang van chay.
    /// </summary>
    /// <remarks>
    /// Lop nay CHIA LAM BA TEP:
    ///   TroLyService.cs         — bo khung + nghiep vu loi (bai, tien, booking, EOR, agent)
    ///   TroLyService.MoRong.cs  — phan con lai cua he thong (tau/ben, cong, giay to,
    ///                             EDI, AI) va cac VIEC THEO LOAT.
    ///   TroLyService.HieuCau.cs — hieu cau noi tu nhien: chao hoi, "mở màn hình",
    ///                             goi y viec gan nhat khi khong luat nao bat duoc.
    /// Tach ra vi mot tep 2.500 dong thi khong ai doc noi, chu khong phai vi
    /// cac phan khac nhau ve ban chat — cung mot bo quy tac an toan.
    /// </remarks>
    public static partial class TroLyService
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        // ============================================================
        // KIEU DU LIEU
        // ============================================================

        public class KetQua
        {
            public string TraLoi { get; set; }
            public string CongCu { get; set; }
            public List<LienKet> LienKets { get; set; }
            public XacNhan CanXacNhan { get; set; }
            public KetQua() { LienKets = new List<LienKet>(); }
        }

        public class LienKet
        {
            public string Nhan { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
        }

        public class XacNhan
        {
            public string CongCu { get; set; }
            public string ThamSo { get; set; }
            public string MoTa { get; set; }
            public string CanhBao { get; set; }
        }

        /// <summary>Mot cong cu = mot viec tro ly biet lam. Danh sach nay hien o "tro ly lam duoc gi"</summary>
        public class MoTaCongCu
        {
            public string Nhom { get; set; }
            public string Ten { get; set; }
            public string ViDu { get; set; }
            public bool DungTien { get; set; }
        }

        /// <summary>Toan bo cong cu = loi + mo rong. Dung cho man hinh "lam duoc gi" va cho test dem so.</summary>
        public static MoTaCongCu[] TatCaCongCu()
        {
            var ds = new List<MoTaCongCu>(DanhMuc);
            ds.AddRange(DanhMucMoRong);
            return ds.ToArray();
        }

        // ============================================================
        // DANH MUC — de nguoi dung hoi "lam duoc gi"
        // ============================================================

        public static readonly MoTaCongCu[] DanhMuc = new[]
        {
            new MoTaCongCu { Nhom = "Tra cứu", Ten = "Tra cứu container", ViDu = "container MSCU2277456" },
            new MoTaCongCu { Nhom = "Tra cứu", Ten = "Tra cứu xe", ViDu = "xe 61C-12345" },
            new MoTaCongCu { Nhom = "Tra cứu", Ten = "Tra cứu khách hàng", ViDu = "khách hàng Hải An" },
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Container tồn lâu", ViDu = "cont nào tồn quá 30 ngày" },
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Tồn bãi theo hãng tàu", ViDu = "tồn bãi theo hãng tàu" },
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Container lạnh", ViDu = "cont lạnh nào bất thường" },
            new MoTaCongCu { Nhom = "Bãi & container", Ten = "Gợi ý hạ bãi", ViDu = "hạ bãi ở đâu cho MSCU2277456" },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Công nợ lưu bãi", ViDu = "công nợ hôm nay" },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Thanh toán chưa thu", ViDu = "ai chưa thanh toán" },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Doanh thu", ViDu = "doanh thu tháng này" },
            new MoTaCongCu { Nhom = "Tiền & công nợ", Ten = "Lập phiếu thu lưu bãi", ViDu = "lập phiếu cho MSCU2277456", DungTien = true },
            new MoTaCongCu { Nhom = "Khách & booking", Ten = "Booking chờ duyệt", ViDu = "booking nào chờ duyệt" },
            new MoTaCongCu { Nhom = "Khách & booking", Ten = "Duyệt / hủy booking", ViDu = "duyệt booking BK260005", DungTien = true },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Lệnh giao nhận", ViDu = "lệnh nào đang chạy" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Xe qua cổng", ViDu = "xe qua cổng hôm nay" },
            new MoTaCongCu { Nhom = "Cổng & xe", Ten = "Bằng lái sắp hết hạn", ViDu = "bằng lái nào sắp hết hạn" },
            new MoTaCongCu { Nhom = "Depot & sửa chữa", Ten = "EOR chờ duyệt", ViDu = "eor nào chờ duyệt" },
            new MoTaCongCu { Nhom = "Depot & sửa chữa", Ten = "Duyệt / từ chối EOR", ViDu = "duyệt eor EOR-DEMO-01", DungTien = true },
            new MoTaCongCu { Nhom = "An ninh", Ten = "Sự cố an ninh", ViDu = "sự cố nào đang mở" },
            new MoTaCongCu { Nhom = "An ninh", Ten = "Đóng sự cố an ninh", ViDu = "đóng sự cố 3", DungTien = true },
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Việc agent chờ duyệt", ViDu = "agent đang chờ duyệt gì" },
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Duyệt / hoàn tác việc agent", ViDu = "duyệt việc agent 12", DungTien = true },
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Sinh bản tin EDI", ViDu = "sinh codeco cho Maersk", DungTien = true },
            new MoTaCongCu { Nhom = "Agent & EDI", Ten = "Chạy agent rà soát", ViDu = "chạy agent", DungTien = true },
            new MoTaCongCu { Nhom = "Phân tích", Ten = "Dự báo sản lượng", ViDu = "dự báo tháng tới" },
        };

        // ============================================================
        // DIEU PHOI
        // ============================================================

        /// <param name="tenGoi">Ten de chao ("Tài") — lay tu <see cref="TenGoi"/>. Null thi goi "bạn".</param>
        public static KetQua Hoi(AppDb db, string cauHoi, string nguoiDung, string tenGoi = null)
        {
            if (string.IsNullOrWhiteSpace(cauHoi))
                return LamDuocGi();

            var tho = cauHoi.Trim();
            var t = BoDau(tho).ToLowerInvariant();

            // ---------- xa giao: "alo", "chào", "cảm ơn", "sao rồi" ----------
            // Xet dau tien: nguoi that mo khung chat bang "alo" chu khong bang lenh.
            var xaGiao = XaGiao(db, tho, t, tenGoi);
            if (xaGiao != null) return xaGiao;

            // ---------- tro ly lam duoc gi ----------
            // Nguoi that ho khong go dung mot cach: "lam duoc gi", "lam duoc NHUNG gi",
            // "biet lam gi", "co the lam gi"... Bat thieu mot bien the la nguoi dung
            // nhan ngay cau "toi chua hieu" ngay o cau hoi dau tien — mat het long tin.
            if (Co(t, "lam duoc gi", "lam duoc nhung gi", "lam nhung gi", "lam duoc viec gi",
                      "giup duoc gi", "giup dc gi", "biet lam gi", "biet gi", "co the lam gi",
                      "lam gi duoc", "lam dc gi", "huong dan", "help", "chuc nang", "menu",
                      "danh muc", "danh sach viec", "ban la ai", "ho tro gi"))
                return LamDuocGi();

            // ---------- CAM — noi ro, khong im lang ----------
            if (Co(t, "xoa ", "xoa het", "delete", "phan quyen", "doi mat khau", "doi gia", "sua bieu gia", "cau hinh he thong"))
                return TuChoi(t);

            // ---------- "mở ...": lenh mo man hinh ----------
            // Xet som: "mở danh sách booking" de xuong duoi se bi luat "booking" nuot.
            var mo = MoManHinh(t, true);
            if (mo != null) return mo;

            var soCont = TimSoContainer(tho);
            var bienSo = TimBienSo(tho);
            var soMa = TimMaHoSo(tho);          // BK-..., EOR-..., PLB-...
            var soNguyen = TimSoNguyen(t);

            // ---------- VIEC THEO LOAT — phai xet TRUOC lenh don le ----------
            // "lap phieu het" ma de sau "lap phieu" thi bi cau don le nuot mat,
            // roi tro ly hoi "lap phieu cho container nao?" — dung nghia den, sai y.
            var loat = ViecTheoLoat(db, tho, t, nguoiDung);
            if (loat != null) return loat;

            // ---------- GHI: cac lenh co dong tu hanh dong ro rang ----------
            if (Co(t, "lap phieu", "chot phi", "chot luu bai", "thu tien luu bai"))
                return ChuanBiLapPhieu(db, soCont);

            if (Co(t, "duyet booking", "duyet don", "chap nhan booking"))
                return ChuanBiBooking(db, soMa, soNguyen, "Đã duyệt");
            if (Co(t, "huy booking", "huy don"))
                return ChuanBiBooking(db, soMa, soNguyen, "Đã hủy");
            if (Co(t, "hoan thanh booking", "xong booking"))
                return ChuanBiBooking(db, soMa, soNguyen, "Hoàn thành");

            if (Co(t, "duyet eor", "duyet phieu sua"))
                return ChuanBiEor(db, soMa, soNguyen, "duyet");
            if (Co(t, "tu choi eor", "khong duyet eor"))
                return ChuanBiEor(db, soMa, soNguyen, "tuchoi");

            if (Co(t, "dong su co", "xu ly su co", "da xu ly su co"))
                return ChuanBiDongSuCo(db, soNguyen);

            if (Co(t, "duyet viec agent", "duyet viec", "dong y viec"))
                return ChuanBiViecAgent(db, soNguyen, "duyet");
            if (Co(t, "hoan tac viec", "hoan tac agent", "go viec"))
                return ChuanBiViecAgent(db, soNguyen, "hoantac");
            if (Co(t, "huy viec agent", "bo viec"))
                return ChuanBiViecAgent(db, soNguyen, "huy");

            if (Co(t, "codeco", "sinh edi", "gui edi", "tao edi", "xuat edi", "edi cho", "bao hang tau", "gui hang tau"))
                return ChuanBiSinhCodeco(db, tho);

            if (Co(t, "chay agent", "quet he thong", "ra soat"))
                return ChuanBiChayAgent();

            // ---------- MO RONG (som) ----------
            // Cac lenh GHI moi, cong voi vai cau DOC co tu khoa trung voi luat cu.
            // Vi du "phan khuc khach hang" co chua "khach hang" — de xuong duoi thi
            // bi luat tra cuu khach hang nuot mat. Cho nao de nham thi xet truoc.
            var som = MoRongSom(db, tho, t, soCont, soMa, soNguyen, bienSo);
            if (som != null) return som;

            // ---------- DOC: tra cuu theo ma ----------
            if (soCont != null) return TraCuuContainer(db, soCont);
            if (bienSo != null) return TraCuuXe(db, bienSo);
            if (soMa != null && soMa.StartsWith("BK")) return TraCuuBooking(db, soMa);
            if (soMa != null && soMa.StartsWith("EOR")) return TraCuuEor(db, soMa);

            // ---------- DOC: theo chu de ----------
            if (Co(t, "khach hang", "cong ty", "khach nao"))
                return TraCuuKhachHang(db, tho);
            if (Co(t, "cong no", "treo bao nhieu", "chua thu", "no luu bai", "tien luu bai"))
                return BangKeCongNo(db);
            if (Co(t, "ton lau", "ton qua", "nam qua", "luu qua", "ton bai lau"))
                return ContainerTonLau(db, TimSoNgay(t) ?? 30);
            if (Co(t, "theo hang tau", "hang tau nao", "moi hang tau"))
                return TonTheoHangTau(db);
            if (Co(t, "cont lanh", "container lanh", "reefer", "nhiet do"))
                return ContainerLanh(db);
            if (Co(t, "ton bai", "trong bai", "bao nhieu cont", "so luong cont"))
                return TonBai(db);
            if (Co(t, "booking", "don dat", "dat dich vu"))
                return BookingChoDuyet(db);
            if (Co(t, "chua thanh toan", "cho thanh toan", "ai chua tra", "thanh toan"))
                return ThanhToanChuaThu(db);
            if (Co(t, "doanh thu", "thu duoc bao nhieu", "tien ve"))
                return DoanhThu(db, t);
            if (Co(t, "su co", "an ninh", "canh bao"))
                return SuCoDangMo(db);
            if (Co(t, "eor", "phieu sua", "sua chua"))
                return EorChoDuyet(db);
            if (Co(t, "agent", "viec cho duyet", "cho duyet gi"))
                return ViecAgentChoDuyet(db);
            if (Co(t, "bang lai", "gplx", "het han"))
                return BangLaiSapHetHan(db);
            if (Co(t, "ha bai o dau", "vi tri ha", "goi y vi tri", "xep cont o dau"))
                return GoiYViTri(db, tho);
            if (Co(t, "lenh giao nhan", "so lenh", "epass", "lenh nao"))
                return LenhDangChay(db);
            if (Co(t, "xe qua cong", "luot xe", "bao nhieu xe", "xe vao", "xe ra"))
                return XeQuaCong(db);
            if (Co(t, "du bao", "thang toi", "san luong", "teu"))
                return DuBao(db);

            // ---------- MO RONG (muon): phan con lai cua he thong ----------
            var muon = MoRongMuon(db, tho, t, soCont, soMa, soNguyen, bienSo);
            if (muon != null) return muon;

            // ---------- nhac ten man hinh ("sơ đồ bãi", "xuất excel") ----------
            var manHinh = MoManHinh(t, false);
            if (manHinh != null) return manHinh;

            return TimChung(db, tho);
        }

        // ============================================================
        // THUC THI — chi chay sau khi nguoi bam dong y
        // ============================================================

        public static KetQua ThucHien(AppDb db, string congCu, string thamSo, string nguoiDung)
        {
            switch (congCu)
            {
                case "LapPhieuLuuBai": return LapPhieuThat(db, thamSo, nguoiDung);
                case "Booking": return BookingThat(db, thamSo, nguoiDung);
                case "Eor": return EorThat(db, thamSo, nguoiDung);
                case "DongSuCo": return DongSuCoThat(db, thamSo, nguoiDung);
                case "ViecAgent": return ViecAgentThat(db, thamSo, nguoiDung);
                case "SinhCodeco": return SinhCodecoThat(db, thamSo, nguoiDung);
                case "ChayAgent": return ChayAgentThat(db, nguoiDung);
                default:
                    // Cong cu cua phan mo rong nam o tep kia — chuyen tiep, dung tu choi voi.
                    var mr = ThucHienMoRong(db, congCu, thamSo, nguoiDung);
                    if (mr != null) return mr;
                    return new KetQua { TraLoi = "Không nhận ra việc cần làm: " + congCu };
            }
        }

        // ============================================================
        // DANH MUC / TU CHOI
        // ============================================================

        private static KetQua LamDuocGi()
        {
            var tatCa = TatCaCongCu();

            // Nhom theo thu tu xuat hien — de "Viec theo loat" (thu quan trong nhat)
            // nam dung cho no trong danh muc chu khong bi sap xep chu cai day xuong duoi.
            var thuTu = new List<string>();
            foreach (var m in tatCa) if (!thuTu.Contains(m.Nhom)) thuTu.Add(m.Nhom);

            // Moi nhom la mot khoi dong/mo, moi viec la mot dong BAM DUOC.
            // Ban truoc la ~70 dong chu liet ke kem cau mau: doc de hieu thi duoc,
            // nhung nguoi dang truc ca khong doc — ho can bam la chay.
            var s = new StringBuilder("Tôi làm được <b>" + tatCa.Length + " việc</b>. Chọn nhóm rồi bấm việc cần làm:");
            foreach (var nhom in thuTu)
            {
                var trong = tatCa.Where(m => m.Nhom == nhom).ToList();
                s.Append("<details class=\"tl-nhom\"><summary><span>").Append(nhom).Append("</span><b>")
                 .Append(trong.Count).Append("</b><i class=\"bi bi-chevron-down\"></i></summary><div class=\"tl-ds\">");
                foreach (var m in trong) s.Append(DongCongCu(m));
                s.Append("</div></details>");
            }
            s.Append("<div class=\"tl-ghichu\"><i class=\"bi bi-shield-lock\"></i> "
                   + "Không làm qua chat: xóa dữ liệu · phân quyền · mật khẩu · biểu giá.</div>");

            var kq = new KetQua { CongCu = "Danh mục", TraLoi = s.ToString() };
            kq.LienKets.Add(LK("Việc hôm nay", "#goi:việc hôm nay", "bi-list-check"));
            return kq;
        }

        private static KetQua TuChoi(string t)
        {
            var kq = new KetQua { CongCu = "Từ chối" };
            string ly;
            LienKet manHinh = null;   // tu choi thi chi luon cho lam dung — dung bo nguoi dung tu tim
            if (Co(t, "xoa", "delete")) ly = "xóa dữ liệu";
            else if (Co(t, "phan quyen", "mat khau")) { ly = "đổi tài khoản / phân quyền"; manHinh = LK("Mở Tài khoản", "/Admin/Accounts", "bi-people"); }
            else if (Co(t, "gia")) { ly = "đổi biểu giá"; manHinh = LK("Mở Dịch vụ & giá", "/Admin/Services", "bi-tags"); }
            else { ly = "đổi cấu hình hệ thống"; manHinh = LK("Mở Vận hành hệ thống", "/Admin/System", "bi-heart-pulse"); }

            // Cố ý không làm: một câu gõ vội không được xóa lịch sử hay đổi giá của doanh nghiệp.
            kq.TraLoi = "Việc <b>" + ly + "</b> không làm qua chat — vào đúng màn hình để có xác nhận và ghi vết.";
            if (Co(t, "xoa", "delete"))
                kq.TraLoi += "<div class=\"tl-ghichu\">Muốn bỏ một bản ghi thì <b>hủy</b> thay vì xóa — chứng từ phải còn dấu vết.</div>";
            if (manHinh != null) kq.LienKets.Add(manHinh);
            return kq;
        }

        // ============================================================
        // DOC — CONTAINER / XE / KHACH
        // ============================================================

        private static KetQua TraCuuContainer(AppDb db, string soCont)
        {
            var kq = new KetQua { CongCu = "Tra cứu container" };
            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Không tìm thấy container <b>" + soCont + "</b>."; return kq; }

            var s = new StringBuilder();
            s.Append("<b>").Append(c.SoCont).Append("</b> · ").Append(c.LoaiCont).Append(" · ").Append(c.TrangThai);
            if (!string.IsNullOrEmpty(c.ViTriBai)) s.Append("<br>Vị trí: <b>").Append(c.ViTriBai).Append("</b>");
            if (c.MaHangTau != null)
            {
                var ht = db.HangTaus.AsNoTracking().FirstOrDefault(h => h.MaHangTau == c.MaHangTau);
                if (ht != null) s.Append("<br>Hãng tàu: ").Append(ht.TenHangTau);
            }

            var phi = LuuBaiService.Tinh(db, c);
            if (phi != null)
            {
                s.Append("<br>Lưu bãi <b>").Append(phi.TongNgay).Append(" ngày</b>");
                if (phi.NgayTinhPhi > 0)
                    s.Append(" · tính phí ").Append(phi.NgayTinhPhi).Append(" ngày · <b>").Append(Tien(phi.TongTien)).Append("</b>");
                else s.Append(" · <i>còn trong hạn miễn phí</i>");
            }

            var dong = LienKetService.DongThoiGianContainer(db, c);
            if (dong != null && dong.Count > 0)
            {
                s.Append("<br><br><b>Gần đây nhất:</b>");
                foreach (var e in dong.OrderByDescending(x => x.Luc).Take(3))
                    s.Append("<br>· ").Append(e.Luc.ToString("dd/MM HH:mm")).Append(" — ").Append(e.TieuDe);
            }

            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở hồ sơ 360°", "/Admin/Containers/HoSo/" + c.MaCont, "bi-box-seam"));
            if (phi != null && phi.TongTien > 0)
                kq.LienKets.Add(LK("Lập phiếu thu", "#goi:lập phiếu cho " + c.SoCont, "bi-receipt"));
            return kq;
        }

        private static KetQua TraCuuXe(AppDb db, string bienSo)
        {
            var kq = new KetQua { CongCu = "Tra cứu xe" };
            var dong = LienKetService.DongThoiGianXe(db, bienSo);
            if (dong == null || dong.Count == 0)
            { kq.TraLoi = "Không có lượt nào của xe <b>" + bienSo + "</b> trong 90 ngày qua."; return kq; }

            var s = new StringBuilder("Xe <b>" + bienSo + "</b> — " + dong.Count + " lượt trong 90 ngày.<br><br><b>Gần đây:</b>");
            foreach (var e in dong.OrderByDescending(x => x.Luc).Take(5))
                s.Append("<br>· ").Append(e.Luc.ToString("dd/MM HH:mm")).Append(" — ").Append(e.TieuDe);
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Hồ sơ xe · tài xế", "/Admin/TaiXe?bienSo=" + Uri.EscapeDataString(bienSo), "bi-truck"));
            return kq;
        }

        private static KetQua TraCuuKhachHang(AppDb db, string cauHoi)
        {
            var kq = new KetQua { CongCu = "Tra cứu khách hàng" };
            var t = BoDau(cauHoi).ToLowerInvariant();
            var ds = db.KhachHangs.AsNoTracking().ToList()
                .Where(k => !string.IsNullOrEmpty(k.TenCongTy)
                         && t.Contains(BoDau(k.TenCongTy).ToLowerInvariant()))
                .ToList();

            if (ds.Count == 0)
            {
                // khong khop ten -> liet ke de nguoi dung chon
                var tatCa = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy).Take(8).ToList();
                var s0 = new StringBuilder("Tôi chưa khớp được tên. Khách hàng hiện có:");
                foreach (var k in tatCa) s0.Append("<br>· ").Append(k.TenCongTy);
                kq.TraLoi = s0.ToString();
                kq.LienKets.Add(LK("Danh sách khách hàng", "/Admin/Customers", "bi-people"));
                return kq;
            }

            var kh = ds[0];
            var soCont = db.Containers.AsNoTracking().Count(c => c.MaKH == kh.MaKH && c.TrangThai == "Trong bãi");
            var bk = db.DatDichVus.AsNoTracking().Count(b => b.MaKH == kh.MaKH && b.TrangThai != "Hoàn thành" && b.TrangThai != "Đã hủy");
            var no = LuuBaiService.BangKeDangChay(db, kh.MaKH)
                       .Where(d => d.Phi != null).Sum(d => d.Phi.TongTien);

            var s = new StringBuilder();
            s.Append("<b>").Append(kh.TenCongTy).Append("</b>");
            if (!string.IsNullOrEmpty(kh.NguoiLienHe)) s.Append("<br>Liên hệ: ").Append(kh.NguoiLienHe);
            if (!string.IsNullOrEmpty(kh.SoDienThoai)) s.Append(" · ").Append(kh.SoDienThoai);
            s.Append("<br>Container trong bãi: <b>").Append(soCont).Append("</b>");
            s.Append("<br>Booking đang mở: <b>").Append(bk).Append("</b>");
            s.Append("<br>Nợ lưu bãi đang treo: <b>").Append(Tien(no)).Append("</b>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Hồ sơ khách hàng", "/Admin/Customers/Details/" + kh.MaKH, "bi-person-vcard"));
            return kq;
        }

        // ============================================================
        // DOC — DONG TIEN
        // ============================================================

        private static KetQua BangKeCongNo(AppDb db)
        {
            var kq = new KetQua { CongCu = "Bảng kê công nợ lưu bãi" };
            var ds = LuuBaiService.BangKeDangChay(db);
            var coPhi = ds.Where(d => d.Phi != null && d.Phi.TongTien > 0).ToList();
            var chuaChot = coPhi.Where(d => !d.DaCoPhieu).ToList();

            var s = new StringBuilder();
            s.Append("Đang có <b>").Append(ds.Count).Append(" container</b> trong bãi.<br>")
             .Append("Tiền lưu bãi đang treo: <b>").Append(Tien(coPhi.Sum(d => d.Phi.TongTien))).Append("</b>");
            if (chuaChot.Count > 0)
                s.Append("<br>Trong đó <b>").Append(Tien(chuaChot.Sum(d => d.Phi.TongTien)))
                 .Append("</b> của ").Append(chuaChot.Count).Append(" container <b>chưa ai lập phiếu</b>.");
            else if (coPhi.Count > 0) s.Append("<br><i>Tất cả đã được lập phiếu.</i>");

            if (chuaChot.Count > 0)
            {
                s.Append("<br><br><b>Nợ nhiều nhất:</b>");
                foreach (var d in chuaChot.OrderByDescending(x => x.Phi.TongTien).Take(5))
                    s.Append("<br>· ").Append(d.Cont.SoCont).Append(" — ").Append(Tien(d.Phi.TongTien))
                     .Append(" (").Append(d.Phi.TongNgay).Append(" ngày)");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở bảng kê đầy đủ", "/Admin/LuuBai", "bi-cash-stack"));
            return kq;
        }

        private static KetQua ThanhToanChuaThu(AppDb db)
        {
            var kq = new KetQua { CongCu = "Thanh toán chưa thu" };
            var ds = db.ThanhToans.AsNoTracking()
                       .Where(t => t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn")
                       .OrderByDescending(t => t.SoTien).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có khoản nào đang chờ thanh toán. Sạch nợ."; return kq; }

            var maBk = ds.Select(t => t.MaBooking).Distinct().ToList();
            var bk = db.DatDichVus.AsNoTracking().Include(b => b.KhachHang)
                       .Where(b => maBk.Contains(b.MaBooking)).ToDictionary(b => b.MaBooking);

            var s = new StringBuilder();
            s.Append("<b>").Append(ds.Count).Append(" khoản</b> chưa thu — tổng <b>")
             .Append(Tien(ds.Sum(t => t.SoTien))).Append("</b>:");
            foreach (var t in ds.Take(6))
            {
                DatDichVu b; bk.TryGetValue(t.MaBooking, out b);
                s.Append("<br>· ").Append(Tien(t.SoTien)).Append(" — ")
                 .Append(b != null ? b.MaSoBooking : "#" + t.MaBooking)
                 .Append(b != null && b.KhachHang != null ? " · " + b.KhachHang.TenCongTy : "")
                 .Append(t.TrangThai == "Quá hạn" ? " <span class='tl-do'>quá hạn</span>" : "");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Đối soát thanh toán", "/Admin/Payments", "bi-credit-card"));
            return kq;
        }

        private static KetQua DoanhThu(AppDb db, string t)
        {
            var kq = new KetQua { CongCu = "Doanh thu" };
            var nay = DateTime.Today;
            var dau = new DateTime(nay.Year, nay.Month, 1);
            var nhan = "tháng " + nay.Month + "/" + nay.Year;
            if (Co(t, "thang truoc")) { dau = dau.AddMonths(-1); nhan = "tháng " + dau.Month + "/" + dau.Year; }
            else if (Co(t, "nam nay", "ca nam")) { dau = new DateTime(nay.Year, 1, 1); nhan = "năm " + nay.Year; }
            var cuoi = Co(t, "nam nay", "ca nam") ? dau.AddYears(1) : dau.AddMonths(1);

            var dt = DoanhThuKy(db, dau, cuoi);
            var s = new StringBuilder("Doanh thu <b>" + nhan + "</b>:");
            s.Append("<br>· Dịch vụ đã thanh toán: <b>").Append(Tien(dt.DichVu)).Append("</b> (").Append(dt.SoKhoan).Append(" khoản)");
            s.Append("<br>· Phí lưu bãi đã thu: <b>").Append(Tien(dt.LuuBai)).Append("</b> (").Append(dt.SoPhieu).Append(" phiếu)");
            s.Append("<br><b>Tổng: ").Append(Tien(dt.Tong)).Append("</b>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Báo cáo", "/Admin/Reports", "bi-graph-up"));
            return kq;
        }

        private class DoanhThuTrongKy
        {
            public decimal DichVu, LuuBai;
            public int SoKhoan, SoPhieu;
            public decimal Tong { get { return DichVu + LuuBai; } }
        }

        /// <summary>
        /// Doanh thu mot ky. Dung chung cho "doanh thu" va "báo cáo nhanh" — hai cho
        /// tu dem rieng thi som muon lech nhau, va nguoi xem se hoi so nao dung.
        /// </summary>
        private static DoanhThuTrongKy DoanhThuKy(AppDb db, DateTime dau, DateTime cuoi)
        {
            var tt = db.ThanhToans.AsNoTracking()
                       .Where(x => x.TrangThai == "Đã thanh toán" && x.NgayThanhToan != null
                                && x.NgayThanhToan >= dau && x.NgayThanhToan < cuoi)
                       .Select(x => x.SoTien).ToList();
            var plb = db.PhieuLuuBais.AsNoTracking()
                        .Where(p => p.TrangThai == "Đã thu" && p.NgayLap >= dau && p.NgayLap < cuoi)
                        .Select(p => p.TongTien).ToList();
            return new DoanhThuTrongKy { DichVu = tt.Sum(), SoKhoan = tt.Count, LuuBai = plb.Sum(), SoPhieu = plb.Count };
        }

        // ============================================================
        // DOC — BAI / HANG TAU / LANH
        // ============================================================

        private static KetQua ContainerTonLau(AppDb db, int nguong)
        {
            var kq = new KetQua { CongCu = "Container tồn lâu (≥ " + nguong + " ngày)" };
            var ds = LuuBaiService.BangKeDangChay(db)
                .Where(d => d.Phi != null && d.Phi.TongNgay >= nguong)
                .OrderByDescending(d => d.Phi.TongNgay).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có container nào nằm bãi quá <b>" + nguong + " ngày</b>. Bãi đang thoáng."; return kq; }

            var s = new StringBuilder();
            s.Append("Có <b>").Append(ds.Count).Append(" container</b> nằm bãi từ ").Append(nguong)
             .Append(" ngày trở lên — tổng <b>").Append(Tien(ds.Sum(d => d.Phi.TongTien))).Append("</b> tiền lưu bãi.<br>");
            foreach (var d in ds.Take(8))
                s.Append("<br>· <b>").Append(d.Cont.SoCont).Append("</b> — ").Append(d.Phi.TongNgay).Append(" ngày · ")
                 .Append(Tien(d.Phi.TongTien)).Append(d.DaCoPhieu ? " <i>(đã có phiếu)</i>" : "");
            if (ds.Count > 8) s.Append("<br>… và ").Append(ds.Count - 8).Append(" container nữa.");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở bảng kê", "/Admin/LuuBai", "bi-hourglass-split"));
            return kq;
        }

        private static KetQua TonBai(AppDb db)
        {
            var kq = new KetQua { CongCu = "Tồn bãi" };
            var q = db.Containers.AsNoTracking().Where(c => c.TrangThai == "Trong bãi");
            var tong = q.Count();
            var theoLoai = q.GroupBy(c => c.LoaiCont).Select(g => new { Loai = g.Key, So = g.Count() })
                            .OrderByDescending(x => x.So).Take(6).ToList();
            var s = new StringBuilder("Hiện có <b>" + tong + " container</b> trong bãi.");
            if (theoLoai.Count > 0)
            {
                s.Append("<br><br><b>Theo loại:</b>");
                foreach (var x in theoLoai) s.Append("<br>· ").Append(x.Loai ?? "(không rõ)").Append(" — ").Append(x.So);
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Sơ đồ bãi", "/Admin/Containers/YardMap", "bi-grid-3x3"));
            return kq;
        }

        private static KetQua TonTheoHangTau(AppDb db)
        {
            var kq = new KetQua { CongCu = "Tồn bãi theo hãng tàu" };
            var ds = db.Containers.AsNoTracking().Where(c => c.TrangThai == "Trong bãi")
                       .GroupBy(c => c.MaHangTau).Select(g => new { Ma = g.Key, So = g.Count() }).ToList();
            var ten = db.HangTaus.AsNoTracking().ToDictionary(h => h.MaHangTau, h => h.TenHangTau);

            var s = new StringBuilder("Tồn bãi theo hãng tàu:");
            foreach (var x in ds.OrderByDescending(x => x.So))
            {
                string tn;
                s.Append("<br>· ").Append(x.Ma != null && ten.TryGetValue(x.Ma.Value, out tn) ? tn : "(chưa gán hãng)")
                 .Append(" — <b>").Append(x.So).Append("</b>");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Tồn rỗng theo hãng", "/Admin/Depot/TonRong", "bi-building"));
            return kq;
        }

        private static KetQua ContainerLanh(AppDb db)
        {
            var kq = new KetQua { CongCu = "Container lạnh" };
            var lanh = db.Containers.AsNoTracking()
                         .Where(c => c.TrangThai == "Trong bãi" && (c.LoaiCont.Contains("RF") || c.NhietDo != null)).ToList();
            if (lanh.Count == 0) { kq.TraLoi = "Không có container lạnh nào trong bãi."; return kq; }

            var moc = DateTime.Now.AddHours(-6);
            var ma = lanh.Select(c => c.MaCont).ToList();
            var log = db.NhietDoLogs.AsNoTracking().Where(l => ma.Contains(l.MaCont) && l.ThoiGian >= moc).ToList();

            var s = new StringBuilder("<b>" + lanh.Count + " container lạnh</b> trong bãi.");
            var batThuong = 0;
            foreach (var c in lanh)
            {
                var gan = log.Where(l => l.MaCont == c.MaCont).OrderByDescending(l => l.ThoiGian).FirstOrDefault();
                if (gan == null)
                { s.Append("<br>· ").Append(c.SoCont).Append(" — <span class='tl-do'>không có số đo 6 giờ qua</span>"); batThuong++; continue; }
                var lech = c.NhietDo != null ? Math.Abs(gan.NhietDo - c.NhietDo.Value) : 0;
                if (lech > 2) { s.Append("<br>· ").Append(c.SoCont).Append(" — <span class='tl-do'>").Append(gan.NhietDo).Append("°C, lệch ").Append(lech.ToString("0.#")).Append("°C</span>"); batThuong++; }
                else s.Append("<br>· ").Append(c.SoCont).Append(" — ").Append(gan.NhietDo).Append("°C ổn");
            }
            if (batThuong == 0) s.Append("<br><br>Tất cả trong ngưỡng.");
            else s.Append("<br><br><b>").Append(batThuong).Append(" container cần xem ngay.</b>");
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Giám sát reefer", "/Admin/Reefer", "bi-snow"));
            return kq;
        }

        // ============================================================
        // DOC — BOOKING / EOR / SU CO / AGENT / TAI XE
        // ============================================================

        private static KetQua BookingChoDuyet(AppDb db)
        {
            var kq = new KetQua { CongCu = "Booking chờ duyệt" };
            var ds = db.DatDichVus.AsNoTracking().Include(b => b.KhachHang)
                       .Where(b => b.TrangThai == "Chờ duyệt").OrderBy(b => b.NgayTao).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có booking nào chờ duyệt."; return kq; }

            var s = new StringBuilder("<b>" + ds.Count + " booking</b> chờ duyệt:");
            foreach (var b in ds.Take(6))
            {
                var cho = (DateTime.Now - b.NgayTao).TotalHours;
                s.Append("<br>· <b>").Append(b.MaSoBooking).Append("</b> · ")
                 .Append(b.KhachHang != null ? b.KhachHang.TenCongTy : "").Append(" · ").Append(Tien(b.TongTien))
                 .Append(cho > 48 ? " <span class='tl-do'>chờ " + (int)(cho / 24) + " ngày</span>" : "");
                kq.LienKets.Add(LK("Duyệt " + b.MaSoBooking, "#goi:duyệt booking " + b.MaSoBooking, "bi-check2"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Danh sách booking", "/Admin/Bookings?trangThai=Chờ duyệt", "bi-journal-check"));
            return kq;
        }

        private static KetQua TraCuuBooking(AppDb db, string ma)
        {
            var kq = new KetQua { CongCu = "Tra cứu booking" };
            var b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaSoBooking == ma);
            if (b == null) { kq.TraLoi = "Không tìm thấy booking <b>" + ma + "</b>."; return kq; }
            kq.TraLoi = "<b>" + b.MaSoBooking + "</b> · " + b.TrangThai
                      + "<br>Khách: " + (b.KhachHang != null ? b.KhachHang.TenCongTy : "")
                      + "<br>Tổng tiền: <b>" + Tien(b.TongTien) + "</b>"
                      + "<br>Tạo lúc: " + b.NgayTao.ToString("dd/MM/yyyy HH:mm");
            kq.LienKets.Add(LK("Mở booking", "/Admin/Bookings/Details/" + b.MaBooking, "bi-journal"));
            if (b.TrangThai == "Chờ duyệt") kq.LienKets.Add(LK("Duyệt", "#goi:duyệt booking " + ma, "bi-check2"));
            return kq;
        }

        private static KetQua EorChoDuyet(AppDb db)
        {
            var kq = new KetQua { CongCu = "EOR chờ duyệt" };
            var ds = db.PhieuSuaChuas.AsNoTracking().Where(e => e.TrangThai == "Chờ duyệt").ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có phiếu sửa chữa nào chờ duyệt."; return kq; }
            var s = new StringBuilder("<b>" + ds.Count + " EOR</b> chờ hãng tàu duyệt:");
            foreach (var e in ds.Take(6))
            {
                s.Append("<br>· <b>").Append(e.SoEOR).Append("</b> · ").Append(e.SoCont).Append(" · ").Append(e.ViTriHuHong)
                 .Append(e.ChiPhiUocTinh != null ? " · " + Tien(e.ChiPhiUocTinh.Value) : "");
                kq.LienKets.Add(LK("Duyệt " + e.SoEOR, "#goi:duyệt eor " + e.SoEOR, "bi-check2"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Màn hình EOR", "/Admin/Depot/Eor", "bi-wrench"));
            return kq;
        }

        private static KetQua TraCuuEor(AppDb db, string ma)
        {
            var kq = new KetQua { CongCu = "Tra cứu EOR" };
            var e = db.PhieuSuaChuas.AsNoTracking().FirstOrDefault(x => x.SoEOR == ma);
            if (e == null) { kq.TraLoi = "Không tìm thấy EOR <b>" + ma + "</b>."; return kq; }
            kq.TraLoi = "<b>" + e.SoEOR + "</b> · " + e.TrangThai + "<br>Container: " + e.SoCont
                      + "<br>Hư hỏng: " + e.ViTriHuHong + " · " + e.MaHuHong
                      + (e.ChiPhiUocTinh != null ? "<br>Ước tính: <b>" + Tien(e.ChiPhiUocTinh.Value) + "</b>" : "");
            kq.LienKets.Add(LK("Bản in EOR", "/Admin/Depot/InEor/" + e.MaEOR, "bi-printer"));
            return kq;
        }

        private static KetQua SuCoDangMo(AppDb db)
        {
            var kq = new KetQua { CongCu = "Sự cố an ninh" };
            var ds = db.SuCoAnNinhs.AsNoTracking().Where(s => s.TrangThai == "Mới").OrderByDescending(s => s.ThoiGian).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có sự cố nào đang mở. Bãi yên."; return kq; }
            var s = new StringBuilder("<b>" + ds.Count + " sự cố</b> đang mở:");
            foreach (var x in ds.Take(6))
            {
                s.Append("<br>· #").Append(x.MaSC).Append(" · <b>").Append(x.Loai).Append("</b> · ").Append(x.MucDo)
                 .Append(" · ").Append(x.ViTri).Append(" · ").Append(x.ThoiGian.ToString("dd/MM HH:mm"));
                kq.LienKets.Add(LK("Đóng #" + x.MaSC, "#goi:đóng sự cố " + x.MaSC, "bi-shield-check"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Trạm an ninh", "/Admin/Security", "bi-shield-exclamation"));
            return kq;
        }

        private static KetQua ViecAgentChoDuyet(AppDb db)
        {
            var kq = new KetQua { CongCu = "Việc agent chờ duyệt" };
            var ds = db.NhatKyAgents.AsNoTracking().Where(n => n.TrangThai == "Chờ duyệt").OrderByDescending(n => n.MaNK).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Agent không có việc nào chờ bạn duyệt."; return kq; }
            var s = new StringBuilder("Agent đề nghị <b>" + ds.Count + " việc</b>, chờ bạn quyết:");
            foreach (var n in ds.Take(6))
            {
                s.Append("<br>· #").Append(n.MaNK).Append(" · <b>").Append(n.KyNang).Append("</b> · ").Append(n.DoiTuong)
                 .Append("<br>&nbsp;&nbsp;<i>").Append(n.LyDo).Append("</i>");
                kq.LienKets.Add(LK("Duyệt #" + n.MaNK, "#goi:duyệt việc agent " + n.MaNK, "bi-check2"));
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Bảng điều khiển Agent", "/Admin/Agent", "bi-robot"));
            return kq;
        }

        private static KetQua BangLaiSapHetHan(AppDb db)
        {
            var kq = new KetQua { CongCu = "Bằng lái sắp hết hạn" };
            var moc = DateTime.Today.AddDays(30);
            var ds = db.TaiXes.AsNoTracking().Where(t => t.HanGPLX != null && t.HanGPLX <= moc)
                       .OrderBy(t => t.HanGPLX).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không tài xế nào có bằng lái hết hạn trong 30 ngày tới."; return kq; }
            var s = new StringBuilder("<b>" + ds.Count + " tài xế</b> có bằng lái hết hạn trong 30 ngày:");
            foreach (var t in ds.Take(8))
            {
                var het = t.HanGPLX.Value < DateTime.Today;
                s.Append("<br>· ").Append(t.HoTen).Append(" · ").Append(t.SoGPLX).Append(" · ")
                 .Append(het ? "<span class='tl-do'>ĐÃ HẾT HẠN " : "hạn ")
                 .Append(t.HanGPLX.Value.ToString("dd/MM/yyyy")).Append(het ? "</span>" : "");
            }
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Hồ sơ tài xế", "/Admin/TaiXe", "bi-person-badge"));
            return kq;
        }

        private static KetQua LenhDangChay(AppDb db)
        {
            var kq = new KetQua { CongCu = "Lệnh giao nhận" };
            var ds = db.LenhGiaoNhans.AsNoTracking().Where(l => l.TrangThai != "Hoàn tất")
                       .OrderByDescending(l => l.MaLenh).Take(8).ToList();
            if (ds.Count == 0) { kq.TraLoi = "Không có lệnh nào đang chạy."; return kq; }
            var s = new StringBuilder("Có <b>" + ds.Count + " lệnh</b> chưa hoàn tất:");
            foreach (var l in ds) s.Append("<br>· <b>").Append(l.SoLenh).Append("</b> · ").Append(l.BienSoXe).Append(" · ").Append(l.TrangThai);
            kq.TraLoi = s.ToString();
            kq.LienKets.Add(LK("Mở lệnh giao nhận", "/Admin/GatePass", "bi-qr-code"));
            return kq;
        }

        private static KetQua XeQuaCong(AppDb db)
        {
            var kq = new KetQua { CongCu = "Lượt xe qua cổng" };
            var homNay = DateTime.Today; var mai = homNay.AddDays(1);
            var ds = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= homNay && x.ThoiGian < mai).ToList();
            var vao = ds.Count(x => x.HuongDi == "Vào");
            kq.TraLoi = "Hôm nay có <b>" + ds.Count + " lượt</b> qua cổng — vào <b>" + vao + "</b>, ra <b>" + (ds.Count - vao) + "</b>.";
            kq.LienKets.Add(LK("Nhật ký cổng", "/Admin/Gate/History", "bi-card-list"));
            return kq;
        }

        private static KetQua DuBao(AppDb db)
        {
            var kq = new KetQua { CongCu = "Dự báo sản lượng" };
            var ls = db.SanLuongThangs.AsNoTracking().OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            if (ls.Count < 3) { kq.TraLoi = "Chưa đủ dữ liệu lịch sử để dự báo (cần ít nhất 3 tháng)."; return kq; }
            var f = ForecastService.Build(ls);
            kq.TraLoi = "Dự báo <b>" + f.NextMonthLabel + "</b>: <b>" + f.NextMonthTEU.ToString("#,##0", Vi) + " TEU</b>"
                      + " (" + (f.GrowthPercent >= 0 ? "+" : "") + f.GrowthPercent.ToString("0.#") + "% so với tháng gần nhất)"
                      + "<br>Xu hướng: " + (f.Slope >= 0 ? "+" : "") + f.Slope.ToString("#,##0.#", Vi) + " TEU/tháng.";
            kq.LienKets.Add(LK("Biểu đồ dự báo", "/Admin/AiTools/ML", "bi-graph-up-arrow"));
            return kq;
        }

        private static KetQua GoiYViTri(AppDb db, string cauHoi)
        {
            var kq = new KetQua { CongCu = "Gợi ý vị trí hạ bãi" };
            var soCont = TimSoContainer(cauHoi);
            var c = soCont == null ? null : db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Cho tôi số container cụ thể, ví dụ <i>“hạ bãi ở đâu cho MSCU2277456”</i>."; return kq; }
            var g = YardService.Suggest(db, c.LoaiCont, c.TrangThai, c.TinhTrang, null);
            kq.TraLoi = g == null ? "Chưa gợi ý được vị trí cho <b>" + c.SoCont + "</b>."
                      : "Gợi ý hạ <b>" + c.SoCont + "</b> tại <b>" + g.ViTri + "</b>."
                        + (g.LyDo != null && g.LyDo.Count > 0 ? "<br><i>" + string.Join(" · ", g.LyDo) + "</i>" : "");
            kq.LienKets.Add(LK("Sơ đồ bãi", "/Admin/Containers/YardMap", "bi-grid-3x3"));
            return kq;
        }

        private static KetQua TimChung(AppDb db, string tuKhoa)
        {
            var kq = new KetQua();
            var ds = LienKetService.TimToanCuc(db, tuKhoa);
            if (ds == null || ds.Count == 0)
            {
                // Khong hieu thi DUA LUA CHON de bam, dung bat nguoi dung doc huong dan.
                // Cung khong gan CongCu: chua cong cu nao chay ma hien nhan
                // "Tìm toàn hệ thống" tren dau cau "chua hieu" la noi sai.
                var goiY = GoiYGanNhat(BoDau(tuKhoa).ToLowerInvariant(), 3);
                if (goiY.Count > 0)
                {
                    kq.TraLoi = "Có phải bạn cần:<div class=\"tl-ds\">" + string.Concat(goiY.Select(DongCongCu))
                              + Dong("Xem tôi làm được gì", null, "làm được gì") + "</div>";
                    return kq;
                }
                kq.TraLoi = "Tôi chưa rõ ý bạn. Bạn cần:"
                    + "<div class=\"tl-ds\">"
                    + Dong("Việc đang chờ hôm nay", null, "việc hôm nay")
                    + Dong("Công nợ lưu bãi", null, "công nợ hôm nay")
                    + Dong("Tồn bãi", null, "tồn bãi")
                    + Dong("Xem tôi làm được gì", null, "làm được gì")
                    + "</div><div class=\"tl-ghichu\">Hoặc gõ thẳng số container, biển số xe, mã booking.</div>";
                return kq;
            }

            kq.CongCu = "Tìm toàn hệ thống";
            var s = new StringBuilder("Tìm thấy <b>" + ds.Count + "</b> kết quả:<div class=\"tl-ds\">");
            foreach (var r in ds.Take(6))
            {
                // Du lieu tu CSDL (ten tau, ten khach...) — ma hoa truoc khi chen vao HTML.
                var nhan = "<span>" + MaHoa(r.TieuDe) + "</span><em>" + MaHoa(r.Nhom) + "</em>";
                s.Append(string.IsNullOrEmpty(r.Url)
                    ? "<div class=\"tl-dong tl-tinh\">" + nhan + "</div>"
                    : "<a class=\"tl-dong\" href=\"" + MaHoa(r.Url) + "\">" + nhan + "</a>");
            }
            kq.TraLoi = s.Append("</div>").ToString();
            return kq;
        }

        // ============================================================
        // GHI — CHUAN BI (hoi truoc)
        // ============================================================

        private static KetQua ChuanBiLapPhieu(AppDb db, string soCont)
        {
            var kq = new KetQua { CongCu = "Lập phiếu thu lưu bãi" };
            if (soCont == null) { kq.TraLoi = "Lập phiếu cho container nào? Ví dụ <i>“lập phiếu cho MSCU2277456”</i>."; return kq; }
            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Không tìm thấy container <b>" + soCont + "</b>."; return kq; }
            var phi = LuuBaiService.Tinh(db, c);
            if (phi == null || phi.TongTien <= 0)
            { kq.TraLoi = "Container <b>" + soCont + "</b> chưa phát sinh phí (còn trong " + LuuBaiService.NGAY_MIEN_PHI + " ngày miễn phí)."; return kq; }

            // Da chot roi thi NOI NGAY, dung hoi "lap phieu?". Truoc day tro ly van hoi,
            // bam Dong y thi LapPhieu chong trung tra lai phieu CU — va man hinh bao
            // "Đã lập phiếu", nguoi dung tuong vua lap them mot phieu.
            var daCo = LuuBaiService.PhieuConHieuLuc(db, c, phi.TuNgay);
            var conLai = daCo == null ? null : LuuBaiService.TinhPhanChuaChot(phi, LuuBaiService.PhieuCuaLuot(db, c, phi.TuNgay));
            if (daCo != null && conLai != null)
            {
                // Da chot giua ky nhung container con o bai them ngay -> de nghi PHIEU BO SUNG (truoc day bao "da co phieu" la het)
                kq.TraLoi = "Container <b>" + soCont + "</b> đã có phiếu <b>" + daCo.SoPhieu + "</b> (" + Tien(daCo.TongTien) + ") nhưng còn ở bãi thêm <b>"
                          + conLai.TongNgay + " ngày</b> sau lần chốt đó — phát sinh <b>" + Tien(conLai.TongTien) + "</b>.";
                kq.CanXacNhan = new XacNhan
                {
                    CongCu = "LapPhieuLuuBai", ThamSo = soCont,
                    MoTa = "Lập phiếu BỔ SUNG " + Tien(conLai.TongTien) + " cho " + soCont + "?",
                    CanhBao = "Phiếu bổ sung chỉ thu phần ngày chưa chốt — không thu lại số đã có trên phiếu " + daCo.SoPhieu + "."
                };
                return kq;
            }
            if (daCo != null)
            {
                kq.TraLoi = "Container <b>" + soCont + "</b> đã có phiếu <b>" + daCo.SoPhieu + "</b> · "
                          + daCo.TrangThai + " · <b>" + Tien(daCo.TongTien) + "</b>.";
                kq.LienKets.Add(LK("Mở phiếu " + daCo.SoPhieu, "/Admin/LuuBai/Phieu/" + daCo.MaPhieu, "bi-receipt"));
                if (daCo.TrangThai == "Chờ thu")
                    kq.LienKets.Add(LK("Ghi nhận đã thu", "#goi:đã thu phiếu " + daCo.SoPhieu, "bi-cash-coin"));
                return kq;
            }

            kq.TraLoi = "Container <b>" + soCont + "</b> đã nằm bãi <b>" + phi.TongNgay + " ngày</b>, tính phí <b>"
                      + phi.NgayTinhPhi + " ngày</b> — tổng <b>" + Tien(phi.TongTien) + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "LapPhieuLuuBai", ThamSo = soCont,
                MoTa = "Lập phiếu thu " + Tien(phi.TongTien) + " cho " + soCont + "?",
                CanhBao = "Phiếu sẽ được đánh số và sinh chứng từ. Hoàn tác được, nhưng số phiếu đã cấp thì không dùng lại."
            };
            return kq;
        }

        private static KetQua ChuanBiBooking(AppDb db, string ma, int? so, string trangThaiMoi)
        {
            var kq = new KetQua { CongCu = "Duyệt booking" };
            var b = TimBooking(db, ma, so);
            if (b == null) { kq.TraLoi = "Booking nào? Cho tôi mã, ví dụ <i>“duyệt booking BK260005”</i>."; kq.LienKets.Add(LK("Xem booking chờ duyệt", "#goi:booking nào chờ duyệt", "bi-list")); return kq; }
            if (b.TrangThai == trangThaiMoi) { kq.TraLoi = "Booking <b>" + b.MaSoBooking + "</b> đã ở trạng thái <b>" + trangThaiMoi + "</b> rồi."; return kq; }

            kq.TraLoi = "Booking <b>" + b.MaSoBooking + "</b> · " + (b.KhachHang != null ? b.KhachHang.TenCongTy : "")
                      + " · " + Tien(b.TongTien) + " · đang <b>" + b.TrangThai + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "Booking", ThamSo = b.MaBooking + "|" + trangThaiMoi,
                MoTa = "Chuyển " + b.MaSoBooking + " → " + trangThaiMoi + "?",
                CanhBao = trangThaiMoi == "Đã hủy"
                    ? "Hủy booking sẽ hủy luôn các khoản chờ thanh toán của nó — không để tiền ảo nằm trong hàng đợi đối soát."
                    : "Khách sẽ thấy trạng thái mới ngay trong tài khoản của họ."
            };
            return kq;
        }

        private static KetQua ChuanBiEor(AppDb db, string ma, int? so, string quyetDinh)
        {
            var kq = new KetQua { CongCu = "Duyệt EOR" };
            PhieuSuaChua e = null;
            if (ma != null) e = db.PhieuSuaChuas.AsNoTracking().FirstOrDefault(x => x.SoEOR == ma);
            if (e == null && so != null) e = db.PhieuSuaChuas.AsNoTracking().FirstOrDefault(x => x.MaEOR == so);
            if (e == null) { kq.TraLoi = "EOR nào? Ví dụ <i>“duyệt eor EOR-DEMO-01”</i>."; kq.LienKets.Add(LK("Xem EOR chờ duyệt", "#goi:eor nào chờ duyệt", "bi-list")); return kq; }
            if (e.TrangThai != "Chờ duyệt") { kq.TraLoi = "EOR <b>" + e.SoEOR + "</b> đã xử lý rồi (" + e.TrangThai + ")."; return kq; }

            var nhan = quyetDinh == "duyet" ? "Duyệt" : "Từ chối";
            kq.TraLoi = "EOR <b>" + e.SoEOR + "</b> · " + e.SoCont + " · " + e.ViTriHuHong
                      + (e.ChiPhiUocTinh != null ? " · ước tính <b>" + Tien(e.ChiPhiUocTinh.Value) + "</b>" : "");
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "Eor", ThamSo = e.MaEOR + "|" + quyetDinh,
                MoTa = nhan + " EOR " + e.SoEOR + "?",
                CanhBao = quyetDinh == "duyet" ? "Sau khi duyệt, xưởng được phép sửa và tính chi phí cho hãng tàu."
                                               : "Container sẽ không được sửa theo phiếu này."
            };
            return kq;
        }

        private static KetQua ChuanBiDongSuCo(AppDb db, int? so)
        {
            var kq = new KetQua { CongCu = "Đóng sự cố" };
            if (so == null) { kq.TraLoi = "Sự cố số mấy? Ví dụ <i>“đóng sự cố 3”</i>."; kq.LienKets.Add(LK("Xem sự cố đang mở", "#goi:sự cố nào đang mở", "bi-list")); return kq; }
            var sc = db.SuCoAnNinhs.AsNoTracking().FirstOrDefault(x => x.MaSC == so);
            if (sc == null) { kq.TraLoi = "Không tìm thấy sự cố <b>#" + so + "</b>."; return kq; }
            if (sc.TrangThai != "Mới") { kq.TraLoi = "Sự cố <b>#" + so + "</b> đã xử lý rồi."; return kq; }

            kq.TraLoi = "Sự cố <b>#" + sc.MaSC + "</b> · " + sc.Loai + " · " + sc.MucDo + " · " + sc.ViTri
                      + "<br><i>" + sc.MoTa + "</i>";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "DongSuCo", ThamSo = sc.MaSC.ToString(),
                MoTa = "Xác nhận đã xử lý sự cố #" + sc.MaSC + "?",
                CanhBao = "Tên bạn và thời điểm sẽ được ghi vào sự cố. Chỉ đóng khi đã thật sự xử lý tại hiện trường."
            };
            return kq;
        }

        private static KetQua ChuanBiViecAgent(AppDb db, int? so, string hanhDong)
        {
            var kq = new KetQua { CongCu = "Việc agent" };
            if (so == null) { kq.TraLoi = "Việc số mấy? Ví dụ <i>“duyệt việc agent 12”</i>."; kq.LienKets.Add(LK("Xem việc chờ duyệt", "#goi:agent đang chờ duyệt gì", "bi-list")); return kq; }
            var n = db.NhatKyAgents.AsNoTracking().FirstOrDefault(x => x.MaNK == so);
            if (n == null) { kq.TraLoi = "Không tìm thấy việc <b>#" + so + "</b>."; return kq; }

            var nhan = hanhDong == "duyet" ? "Duyệt" : hanhDong == "huy" ? "Hủy" : "Hoàn tác";
            kq.TraLoi = "Việc <b>#" + n.MaNK + "</b> · " + n.KyNang + " · " + n.DoiTuong + " · đang <b>" + n.TrangThai + "</b>"
                      + "<br><i>" + n.LyDo + "</i>";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "ViecAgent", ThamSo = n.MaNK + "|" + hanhDong,
                MoTa = nhan + " việc #" + n.MaNK + "?",
                CanhBao = hanhDong == "duyet" ? "Agent sẽ thực hiện thật: " + n.HanhDong
                        : hanhDong == "hoantac" ? "Bản ghi agent đã tạo sẽ chuyển sang trạng thái hủy (không xóa)."
                        : "Việc này sẽ không được làm."
            };
            return kq;
        }

        private static KetQua ChuanBiSinhCodeco(AppDb db, string cauHoi)
        {
            var kq = new KetQua { CongCu = "Sinh bản tin EDI" };
            var hangTaus = db.HangTaus.AsNoTracking().ToList();
            var t = BoDau(cauHoi).ToLowerInvariant();
            var hang = hangTaus.FirstOrDefault(h => !string.IsNullOrEmpty(h.TenHangTau) && t.Contains(BoDau(h.TenHangTau).ToLowerInvariant()));
            if (hang == null)
            {
                kq.TraLoi = "Sinh bản tin cho hãng tàu nào? Hiện có: <b>" + string.Join("</b>, <b>", hangTaus.Take(6).Select(h => h.TenHangTau)) + "</b>.";
                kq.LienKets.Add(LK("Mở màn hình EDI", "/Admin/Edi", "bi-broadcast"));
                return kq;
            }
            kq.TraLoi = "Sẽ sinh bản tin <b>CODECO</b> báo lượt vào/ra bãi cho hãng <b>" + hang.TenHangTau + "</b>.";
            kq.CanXacNhan = new XacNhan
            {
                CongCu = "SinhCodeco", ThamSo = hang.MaHangTau.ToString(),
                MoTa = "Sinh bản tin CODECO gửi " + hang.TenHangTau + "?",
                CanhBao = "Bản tin được tạo ở trạng thái chờ gửi. Kiểm tra nội dung trước khi gửi thật cho hãng tàu."
            };
            return kq;
        }

        private static KetQua ChuanBiChayAgent()
        {
            return new KetQua
            {
                CongCu = "Agent tự động",
                TraLoi = "Agent sẽ rà soát 7 kỹ năng: phí lưu bãi · bản tin EDI · công nợ quá hạn · thẻ xe · EOR tồn đọng · container tồn lâu · bằng lái hết hạn.",
                CanXacNhan = new XacNhan
                {
                    CongCu = "ChayAgent", ThamSo = "",
                    MoTa = "Chạy agent rà soát ngay bây giờ?",
                    CanhBao = "Kỹ năng nào đang ở chế độ “Tự làm” sẽ thực hiện thật. Mọi việc đều ghi nhật ký và hoàn tác được."
                }
            };
        }

        // ============================================================
        // GHI — THUC HIEN THAT
        // ============================================================

        private static KetQua LapPhieuThat(AppDb db, string soCont, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Lập phiếu thu lưu bãi" };
            var c = db.Containers.FirstOrDefault(x => x.SoCont == soCont);
            if (c == null) { kq.TraLoi = "Không tìm thấy container " + soCont + "."; return kq; }
            var batDau = DateTime.Now;
            var p = LuuBaiService.LapPhieu(db, c, nguoiDung);
            if (p == null) { kq.TraLoi = "Không lập được phiếu cho <b>" + soCont + "</b> — chưa phát sinh phí."; return kq; }
            db.SaveChanges();
            // LapPhieu tra PHIEU CU khi da chot (chong trung) — phai phan biet, khong thi
            // bao "Đã lập" trong khi khong lap gi (giua luc hoi va luc bam, nguoi khac vua chot).
            kq.TraLoi = p.NgayLap < batDau
                ? "Không lập thêm: <b>" + soCont + "</b> đã có phiếu <b>" + p.SoPhieu + "</b> · " + p.TrangThai + " · <b>" + Tien(p.TongTien) + "</b>."
                : "Đã lập phiếu <b>" + p.SoPhieu + "</b> — <b>" + Tien(p.TongTien) + "</b> cho <b>" + soCont + "</b>.";
            kq.LienKets.Add(LK("Mở phiếu " + p.SoPhieu, "/Admin/LuuBai/Phieu/" + p.MaPhieu, "bi-receipt"));
            return kq;
        }

        private static KetQua BookingThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Duyệt booking" };
            var p = thamSo.Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var b = db.DatDichVus.Find(id);
            if (b == null) { kq.TraLoi = "Không tìm thấy booking."; return kq; }

            var moi = p[1];
            b.TrangThai = moi;
            // Cung quy tac voi BookingsController.SetStatus: huy booking thi huy luon
            // khoan cho thanh toan, khong de "tien ma" trong hang doi doi soat.
            if (moi == "Đã hủy")
                foreach (var tt in db.ThanhToans.Where(t => t.MaBooking == id && (t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn")))
                { tt.TrangThai = "Đã hủy"; tt.GhiChu = "Tự hủy do booking bị hủy (trợ lý, " + nguoiDung + ")."; }
            db.SaveChanges();

            kq.TraLoi = "Đã chuyển booking <b>" + b.MaSoBooking + "</b> → <b>" + moi + "</b>.";
            kq.LienKets.Add(LK("Mở booking", "/Admin/Bookings/Details/" + b.MaBooking, "bi-journal"));
            return kq;
        }

        private static KetQua EorThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Duyệt EOR" };
            var p = thamSo.Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var e = db.PhieuSuaChuas.Find(id);
            if (e == null) { kq.TraLoi = "Không tìm thấy EOR."; return kq; }
            if (e.TrangThai != "Chờ duyệt") { kq.TraLoi = "EOR này đã xử lý rồi."; return kq; }
            // Cung quy tac voi DepotController.DuyetEor
            e.TrangThai = p[1] == "duyet" ? "Đã duyệt" : "Từ chối";
            e.NgayDuyet = DateTime.Now;
            db.SaveChanges();
            kq.TraLoi = "EOR <b>" + e.SoEOR + "</b> → <b>" + e.TrangThai + "</b>.";
            kq.LienKets.Add(LK("Bản in EOR", "/Admin/Depot/InEor/" + e.MaEOR, "bi-printer"));
            return kq;
        }

        private static KetQua DongSuCoThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Đóng sự cố" };
            int id; if (!int.TryParse(thamSo, out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            var sc = db.SuCoAnNinhs.Find(id);
            if (sc == null) { kq.TraLoi = "Không tìm thấy sự cố."; return kq; }
            if (sc.TrangThai != "Mới") { kq.TraLoi = "Sự cố đã xử lý rồi."; return kq; }
            // Cung quy tac voi SecurityController.Resolve
            sc.TrangThai = "Đã xử lý";
            sc.NguoiXuLy = nguoiDung;
            sc.ThoiGianXuLy = DateTime.Now;
            db.SaveChanges();
            kq.TraLoi = "Đã đóng sự cố <b>#" + id + "</b>. Người xử lý: " + nguoiDung + ".";
            kq.LienKets.Add(LK("Trạm an ninh", "/Admin/Security", "bi-shield-check"));
            return kq;
        }

        private static KetQua ViecAgentThat(AppDb db, string thamSo, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Việc agent" };
            var p = thamSo.Split('|');
            int id; if (p.Length != 2 || !int.TryParse(p[0], out id)) { kq.TraLoi = "Tham số không hợp lệ."; return kq; }
            // Goi thang AgentTuDongService de dung mot bo quy tac, mot cho ghi nhat ky
            string r = p[1] == "duyet" ? AgentTuDongService.Duyet(db, id, nguoiDung)
                     : p[1] == "huy" ? AgentTuDongService.Huy(db, id, nguoiDung)
                     : AgentTuDongService.HoanTac(db, id, nguoiDung);
            kq.TraLoi = r;
            kq.LienKets.Add(LK("Bảng điều khiển Agent", "/Admin/Agent", "bi-robot"));
            return kq;
        }

        /// <summary>
        /// Truoc day ham nay chi MO man hinh EDI cho nguoi tu bam — nguoi dung
        /// bam "dong y" xong lai phai lam tay them mot vong. Gio sinh that,
        /// dung nguyen cac buoc cua EdiController.SinhCodeco.
        /// </summary>
        private static KetQua SinhCodecoThat(AppDb db, string maHangTau, string nguoiDung)
        {
            return SinhEdiThatSu(db, maHangTau, nguoiDung);
        }

        private static KetQua ChayAgentThat(AppDb db, string nguoiDung)
        {
            var kq = new KetQua { CongCu = "Agent tự động" };
            var r = AgentTuDongService.ChayTatCa(db, nguoiDung);
            kq.TraLoi = "Agent chạy xong: <b>" + r.DaLam + "</b> việc đã làm, <b>" + r.ChoDuyet + "</b> việc chờ bạn duyệt"
                      + (r.Loi > 0 ? ", <b>" + r.Loi + "</b> kỹ năng lỗi" : "") + ".";
            if (r.ChiTiet != null && r.ChiTiet.Count > 0) kq.TraLoi += "<br><br>· " + string.Join("<br>· ", r.ChiTiet);
            kq.LienKets.Add(LK("Mở nhật ký Agent", "/Admin/Agent", "bi-robot"));
            return kq;
        }

        // ============================================================
        // TIEN ICH
        // ============================================================

        private static LienKet LK(string nhan, string url, string icon) { return new LienKet { Nhan = nhan, Url = url, Icon = icon }; }

        /// <summary>
        /// Ma hoa chu truoc khi chen vao HTML — noi dung, hoac thuoc tinh trong ngoac kep.
        /// Chi doi 5 ky tu co the pha HTML. Khong dung WebUtility.HtmlEncode vi no doi ca
        /// chu co dau Latin-1 ("bãi" -> "b&#227;i"): trinh duyet van hien dung, nhung HTML
        /// tra ve thanh kho doc, kho tim loi.
        /// </summary>
        private static string MaHoa(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        /// <summary>
        /// Mot DONG BAM DUOC trong cau tra loi — thay cho mot dong chu phai doc.
        /// Bam vao thi tro ly tu hoi `cau`, nguoi dung khong phai go lai.
        /// `so` null thi hien mui ten thay cho con so. `nhan` phai la chu tinh
        /// (khong lay tu CSDL) — du lieu CSDL thi ma hoa truoc roi moi dua vao.
        /// </summary>
        private static string Dong(string nhan, int? so, string cau, bool noiBat = false)
        {
            return DongGiaTri(nhan, so.HasValue ? so.Value.ToString("#,##0", Vi) : null, cau, noiBat);
        }

        /// <summary>Nhu Dong() nhung o ben phai la CHU ("113,4 tr", "3 cont"). Null thi hien mui ten.</summary>
        private static string DongGiaTri(string nhan, string giaTri, string cau, bool noiBat = false)
        {
            return "<button type=\"button\" class=\"tl-dong" + (noiBat ? " tl-noibat" : "") + "\""
                 + (noiBat ? " title=\"Trợ lý tự làm được\"" : "")
                 + " data-goi=\"" + MaHoa(cau) + "\"><span>" + nhan + "</span>"
                 + (giaTri != null ? "<b>" + MaHoa(giaTri) + "</b>" : "<i class=\"bi bi-chevron-right\"></i>")
                 + "</button>";
        }

        /// <summary>Dong CHI DE XEM, khong bam — bieu gia, danh sach viec sap lam.</summary>
        private static string DongTinh(string nhan, string giaTri)
        {
            return "<div class=\"tl-dong tl-tinh\"><span>" + nhan + "</span>"
                 + (giaTri != null ? "<b>" + MaHoa(giaTri) + "</b>" : "") + "</div>";
        }

        /// <summary>Tien gon cho o nho: 113.390.000 -> "113,4 tr". Duoi mot trieu thi ghi du.</summary>
        private static string TienGon(decimal v)
        {
            if (Math.Abs(v) >= 1000000000m) return (v / 1000000000m).ToString("#,##0.#", Vi) + " tỷ";
            if (Math.Abs(v) >= 1000000m) return (v / 1000000m).ToString("#,##0.#", Vi) + " tr";
            return Tien(v);
        }

        /// <summary>
        /// Dong DIEN SAN: bam vao thi cau mau duoc dien vao o nhap (phan ma duoc
        /// boi den san), CHUA gui. Nguoi dung go ma that de len roi Enter.
        /// </summary>
        private static string DongDien(string nhan, string cauMau)
        {
            return "<button type=\"button\" class=\"tl-dong\" title=\"Điền sẵn — sửa mã rồi Enter\" data-dien=\""
                 + MaHoa(cauMau) + "\"><span>" + nhan + "</span><i class=\"bi bi-pencil\"></i></button>";
        }

        /// <summary>Gop chu lap: "alooo" -> "alo", "hiii" -> "hi", "okkk" -> "ok" — go chat hay keo dai chu.</summary>
        private static string Gon(string tu)
        {
            if (string.IsNullOrEmpty(tu)) return "";
            var sb = new StringBuilder(tu.Length);
            foreach (var c in tu) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
            return sb.ToString();
        }

        private static DatDichVu TimBooking(AppDb db, string ma, int? so)
        {
            DatDichVu b = null;
            if (ma != null) b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaSoBooking == ma);
            if (b == null && so != null) b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaBooking == so);
            return b;
        }

        private static string BoDau(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var chuan = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in chuan)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        private static bool Co(string daBoDau, params string[] cacTu)
        {
            foreach (var k in cacTu) if (daBoDau.Contains(k)) return true;
            return false;
        }

        private static string TimSoContainer(string s)
        {
            var m = Regex.Match(s.ToUpperInvariant(), @"\b([A-Z]{4}\s?\d{7})\b");
            return m.Success ? m.Groups[1].Value.Replace(" ", "") : null;
        }

        private static string TimBienSo(string s)
        {
            var m = Regex.Match(s.ToUpperInvariant(), @"\b(\d{2}[A-Z]{1,2}[-\s]?\d{4,5}(?:\.\d{2})?)\b");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Ma ho so: BK260005 / EOR-DEMO-01 / PLB-2609-0001 / LGN-DEMO-001.
        /// Chi bam vao TIEN TO, khong ep dinh dang phan sau — vi moi loai ho so
        /// danh so mot kieu (co cai co gach, co cai khong, co cai chen chu).
        /// Ep dinh dang la tu lam minh mu truoc du lieu that (da bi mot lan).
        /// </summary>
        private static string TimMaHoSo(string s)
        {
            var m = Regex.Match(s.ToUpperInvariant(), @"\b((?:BK|EOR|PLB|LGN)[-A-Z0-9]*\d)\b");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>So nguyen dung mot minh (vd "dong su co 3", "duyet viec 12") — khong lay so trong ma cont/bien so</summary>
        private static int? TimSoNguyen(string daBoDau)
        {
            var m = Regex.Match(daBoDau, @"(?:^|\s)#?(\d{1,6})(?:\s|$)");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) ? (int?)n : null;
        }

        private static int? TimSoNgay(string daBoDau)
        {
            var m = Regex.Match(daBoDau, @"(\d+)\s*ngay");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) ? (int?)n : null;
        }

        private static string Tien(decimal v) { return v.ToString("#,##0", Vi) + " đ"; }
    }
}
