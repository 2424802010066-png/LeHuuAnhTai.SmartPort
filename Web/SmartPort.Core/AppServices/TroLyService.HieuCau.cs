using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// TRO LY — PHAN HIEU CAU NOI TU NHIEN.
    ///
    /// Luat o hai tep kia bat duoc cau "dung mau". Nguoi that thi go "alo",
    /// "sao rồi", "mở sơ đồ bãi", "phí lưu bãi". Do bang 53 cau nhan vien depot
    /// hay go: 25 cau roi vao "tôi chưa hiểu". Tep nay gom ba lop do:
    ///   1. XA GIAO  — chao hoi, cam on, tam biet; chao xong bao luon viec dang cho
    ///   2. MAN HINH — "mở ...", "sơ đồ bãi", "xuất excel": dua dung nut mo man hinh
    ///   3. GOI Y    — van khong hieu thi dua 3 viec GAN NHAT trong danh muc de bam
    /// Khong lop nao GHI du lieu — ca ba chi tra loi hoac dua nut.
    /// </summary>
    public static partial class TroLyService
    {
        // ============================================================
        // XA GIAO — "alo", "chào", "cảm ơn", "sao rồi"
        // ============================================================
        //
        // Nguoi that mo khung chat bang "alo", khong bang mot lenh. Tra "toi chua
        // hieu" ngay cau dau tien la mat long tin ngay cau dau tien.
        //
        // KHONG dung Co() o day. Co() so khop CHUOI CON, ma "hi" nam trong "phi",
        // "chi", "thi", "khi" — "phi luu bai" se bi hieu thanh cau chao. O day xet
        // TUNG TU: cau chi la xa giao khi MOI tu trong cau deu la tu xa giao. Co mot
        // tu nghiep vu la roi xuong luat nghiep vu — "chào, công nợ hôm nay" van ra
        // bang ke cong no.

        /// <summary>Tu dem — di voi loai cau nao cung duoc: "ạ", "nhé", "bạn ơi", "trợ lý"...</summary>
        private static readonly HashSet<string> TuDem = new HashSet<string>(
            "a ah ban em anh chi sep oi nha nhe nhen hen ne nhi tro ly bot ad admin day".Split(' ').Select(Gon));

        private class KieuXaGiao
        {
            public string Loai;
            public HashSet<string> Chinh;   // phai co it nhat mot tu
            public HashSet<string> Kem;     // co cung duoc
            public KieuXaGiao(string loai, string chinh, string kem)
            {
                Loai = loai;
                Chinh = new HashSet<string>(chinh.Split(' ').Select(Gon));
                Kem = new HashSet<string>(kem.Split(' ').Select(Gon));
            }
        }

        /// <summary>
        /// Can tu CHINH de "có không" khong bi hieu thanh loi chao. Thu tu mang la
        /// thu tu xet: "chào nhé, tôi về đây" co ca "chào" lan "về" — la loi tam biet,
        /// nen tam biet dung truoc chao.
        /// </summary>
        private static readonly KieuXaGiao[] CacKieuXaGiao =
        {
            new KieuXaGiao("tambiet", "bye byebye tam biet gap nghi ve", "hen lai sau mai toi di chao nhe"),
            new KieuXaGiao("camon", "cam thanks thank tks thx", "on you roi qua lam nhieu voi xin vay ok oke tot"),
            new KieuXaGiao("khen", "tot hay gioi tuyet great nice cool good", "qua lam voi roi the vay"),
            new KieuXaGiao("ok", "ok oke okie okay oki okela u um uhm uh vang da duoc", "roi the vay"),
            new KieuXaGiao("chao", "alo lo hello helo hi hey chao e ey ai morning", "xin co khong good buoi sang trua chieu toi"),
            new KieuXaGiao("tinhhinh", "sao nao moi tinh nay", "the roi co gi khong hinh hom ra nhu vay"),
        };

        /// <summary>Loai cau xa giao, hoac null neu cau co noi dung nghiep vu.</summary>
        private static string NhanXaGiao(string tho, string t)
        {
            var sach = Regex.Replace(t, @"[^\p{L}\p{N}\s]", " ");
            sach = Regex.Replace(sach, @"\bbai bai\b", "byebye");   // "bái bai" — de rieng "bai" thi trung "bãi"
            var tu = sach.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(Gon).ToList();

            if (tu.Count == 0) return tho.Contains("?") ? "hoi" : "chao";   // chi co "👋" hoac "???"
            if (tu.Count > 8) return null;
            if (tu.All(TuDem.Contains)) return "chao";                        // "bạn ơi", "em ơi"

            foreach (var k in CacKieuXaGiao)
                if (tu.Any(k.Chinh.Contains)
                    && tu.All(x => k.Chinh.Contains(x) || k.Kem.Contains(x) || TuDem.Contains(x)))
                    return k.Loai;
            return null;
        }

        private static KetQua XaGiao(AppDb db, string tho, string t, string tenGoi)
        {
            var loai = NhanXaGiao(tho, t);
            if (loai == null) return null;

            var ten = MaHoa(string.IsNullOrWhiteSpace(tenGoi) ? "bạn" : tenGoi);
            switch (loai)
            {
                // Loi chao cung phai CO ICH: chao xong la bao luon viec dang cho,
                // de nguoi mo chat biet ngay hom nay can lam gi.
                case "chao": return BanDoViec(db, "Chào <b>" + ten + "</b>! ");
                case "tinhhinh": return ViecHomNay(db);
                case "hoi": return LamDuocGi();
                case "tambiet": return new KetQua { TraLoi = "Hẹn gặp lại <b>" + ten + "</b>!" };
                case "camon": return new KetQua { TraLoi = "Không có gì! Cần gì cứ gõ nhé." };
                case "khen": return new KetQua { TraLoi = "Cảm ơn <b>" + ten + "</b>! Cần gì cứ gõ nhé." };
                default: return new KetQua { TraLoi = "Ok. Cần gì cứ gõ nhé." };
            }
        }

        /// <summary>
        /// Ten de chao: "Lê Hữu Anh Tài" -> "Tài". Nguoi Viet goi nhau bang TEN
        /// (tu cuoi), khong bang ho. Tai khoan dat theo chuc danh ("Quản trị hệ thống")
        /// thi goi "bạn" — "Chào Quản trị hệ thống!" nghe nhu may doc.
        /// </summary>
        public static string TenGoi(string hoTen)
        {
            if (string.IsNullOrWhiteSpace(hoTen)) return "bạn";
            var tu = hoTen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tu.Length < 2 || !HoPhoBien.Contains(BoDau(tu[0]).ToLowerInvariant())) return "bạn";
            return tu[tu.Length - 1];
        }

        private static readonly HashSet<string> HoPhoBien = new HashSet<string>(
            ("nguyen tran le pham hoang huynh phan vu vo dang bui do ho ngo duong ly dinh truong lam mai "
           + "trinh doan luong cao ha ta to chau quach thai luu tang la kieu trieu vuong giang hua mac tong "
           + "diep nong lai tieu au").Split(' '));

        // ============================================================
        // TU / CUM TU
        // ============================================================

        /// <summary>Tach cau (da bo dau) thanh tu — bo dau cau va bieu tuong.</summary>
        private static List<string> TachTu(string daBoDau)
        {
            return Regex.Split(daBoDau ?? "", @"[^\p{L}\p{N}]+").Where(x => x.Length > 0).ToList();
        }

        /// <summary>
        /// Co CUM TU nguyen ven khong. Khac Co() o cho KHONG khop chuoi con:
        /// "khach no" (khách nợ) khong duoc khop "khach noi" (khách nói),
        /// "cont hong" (cont hỏng) khong duoc khop giua chu "khong".
        /// </summary>
        private static bool CoTu(string daBoDau, params string[] cacCum)
        {
            var s = " " + string.Join(" ", TachTu(daBoDau)) + " ";
            foreach (var c in cacCum) if (s.Contains(" " + c + " ")) return true;
            return false;
        }

        // ============================================================
        // MAN HINH — "mở sơ đồ bãi", "xuất excel", "hóa đơn"
        // ============================================================

        private class ManHinh
        {
            public string Ten, Url, Icon;
            public string[] TuKhoa;     // da bo dau, viet thuong
            public ManHinh(string ten, string url, string icon, string tuKhoa)
            {
                Ten = ten; Url = url; Icon = icon; TuKhoa = tuKhoa.Split('|');
            }
        }

        /// <summary>
        /// Tu khoa o day phai RIENG cua man hinh do. "tàu", "booking" khong dua vao:
        /// hoi "booking nào chờ duyệt" la muon so lieu, khong phai muon mo trang.
        /// </summary>
        private static readonly ManHinh[] CacManHinh =
        {
            new ManHinh("Tổng quan", "/Admin/Dashboard", "bi-speedometer2", "tong quan|dashboard|trang chu"),
            new ManHinh("Sơ đồ bãi", "/Admin/Containers/YardMap", "bi-grid-3x3-gap", "so do bai|ban do bai|yard map|yardmap"),
            new ManHinh("Danh sách container", "/Admin/Containers", "bi-box-seam", "danh sach container|quan ly container|danh sach cont"),
            new ManHinh("Container lạnh", "/Admin/Reefer", "bi-thermometer-snow", "giam sat reefer|giam sat cont lanh"),
            new ManHinh("Tồn rỗng theo hãng", "/Admin/Depot/TonRong", "bi-box2", "ton rong theo hang"),
            new ManHinh("EDI hãng tàu", "/Admin/Edi", "bi-arrow-left-right", "man hinh edi|trang edi"),
            new ManHinh("Sửa chữa vỏ · EOR", "/Admin/Depot/Eor", "bi-tools", "man hinh eor|trang eor|xuong sua chua"),
            new ManHinh("Điều độ chuyến tàu", "/Admin/VesselCalls", "bi-calendar2-week", "dieu do tau|dieu do chuyen tau"),
            new ManHinh("Gantt cầu bến", "/Admin/Berths/Gantt", "bi-bar-chart-steps", "gantt|ke hoach cau ben"),
            new ManHinh("Cổng thông minh", "/Admin/Gate", "bi-upc-scan", "cong thong minh|camera cong|doc bien so"),
            new ManHinh("Hồ sơ xe · tài xế", "/Admin/Gate/HoSoXe", "bi-truck", "ho so xe|ho so tai xe"),
            new ManHinh("Lệnh giao nhận", "/Admin/GatePass", "bi-qr-code-scan", "dang ky lenh|tao lenh giao nhan|man hinh lenh"),
            new ManHinh("Trạm cân · VGM", "/Admin/Gate/PhieuCan", "bi-speedometer2", "tram can|man hinh can"),
            new ManHinh("Kho chứng từ", "/Admin/Docs", "bi-archive", "kho chung tu|in phieu|phieu eir|in chung tu"),
            new ManHinh("Phòng an ninh", "/Admin/Security", "bi-building-lock", "phong an ninh|man hinh an ninh"),
            new ManHinh("Đơn đặt dịch vụ", "/Admin/Bookings", "bi-receipt-cutoff", "danh sach booking|don dat dich vu"),
            new ManHinh("Thu phí cổng", "/Admin/PhiCong", "bi-cash-coin", "quay thu phi|man hinh thu phi"),
            new ManHinh("Phí lưu bãi · công nợ", "/Admin/LuuBai", "bi-stopwatch", "bang ke luu bai|man hinh luu bai"),
            new ManHinh("Đối soát thanh toán", "/Admin/Payments", "bi-cash-stack", "hoa don|doi soat thanh toan"),
            new ManHinh("Dịch vụ & giá", "/Admin/Services", "bi-tags", "quan ly dich vu|sua gia dich vu|cap nhat gia"),
            new ManHinh("Khách hàng", "/Admin/Customers", "bi-buildings", "them khach hang|quan ly khach hang"),
            new ManHinh("Báo cáo thống kê", "/Admin/Reports", "bi-graph-up-arrow", "excel|xuat file|xuat bao cao"),
            new ManHinh("Hộp thư liên hệ", "/Admin/Contacts", "bi-chat-left-text", "hop thu|hop thu lien he"),
            new ManHinh("Nhật ký hoạt động", "/Admin/System/NhatKy", "bi-journal-text", "nhat ky hoat dong"),
            new ManHinh("Tài khoản", "/Admin/Accounts", "bi-people", "tai khoan|them nhan vien|quan ly nguoi dung"),
            new ManHinh("Agent tự động", "/Admin/Agent", "bi-robot", "bang dieu khien agent|man hinh agent"),
            new ManHinh("Tích hợp · API", "/Admin/TichHop", "bi-plug", "tich hop|api"),
        };

        private static readonly Regex LenhMo = new Regex(@"^(mo|vao|di toi|di den|chuyen toi|chuyen sang)\s+((man hinh|trang)\s+)?");

        /// <param name="lenhMo">
        /// true : chi xet cau BAT DAU bang "mở/vào..." — duoc khop ca theo TEN man hinh ("mở lưu bãi").
        /// false: xet moi cau nhung chi khop TU KHOA rieng ("sơ đồ bãi", "xuất excel").
        /// </param>
        private static KetQua MoManHinh(string t, bool lenhMo)
        {
            ManHinh tot = null;
            var diemTot = 0;

            string conLai = null;
            if (lenhMo)
            {
                var m = LenhMo.Match(t);
                if (!m.Success) return null;
                conLai = string.Join(" ", TachTu(t.Substring(m.Length)));
                if (conLai.Length < 2) return null;
            }

            foreach (var mh in CacManHinh)
            {
                int diem;
                if (lenhMo)
                {
                    var ten = string.Join(" ", TachTu(BoDau(mh.Ten).ToLowerInvariant()));
                    diem = CoTu(ten, conLai) ? 100 + conLai.Length
                         : mh.TuKhoa.Where(k => CoTu(conLai, k) || CoTu(k, conLai)).Select(k => k.Length).DefaultIfEmpty(0).Max();
                }
                else
                {
                    diem = mh.TuKhoa.Where(k => CoTu(t, k)).Select(k => k.Length).DefaultIfEmpty(0).Max();
                }
                if (diem > diemTot) { tot = mh; diemTot = diem; }
            }
            if (tot == null) return null;

            var kq = new KetQua { CongCu = "Mở màn hình", TraLoi = "Bấm để mở <b>" + tot.Ten + "</b>:" };
            kq.LienKets.Add(LK(tot.Ten, tot.Url, tot.Icon));
            return kq;
        }

        // ============================================================
        // GOI Y GAN NHAT — thay cho "tôi chưa hiểu" chung chung
        // ============================================================

        /// <summary>Tu khong mang nghia nghiep vu — bo ra truoc khi cham diem.</summary>
        private static readonly HashSet<string> TuRong = new HashSet<string>(
            ("cho toi ban co khong nao gi la cua va voi the hom nay xem di nha oi duoc lam can muon hay giup minh "
           + "em anh bao nhieu dau de ma thi mot cac nhung do roi chua da dang se bi tu ra vao len sao nhu qua "
           + "hien xin vui long").Split(' '));

        /// <summary>
        /// Doan viec GAN NHAT trong danh muc cho cau khong luat nao bat duoc.
        /// Cham theo tu trung: trung TEN viec 2 diem, trung CAU MAU 1 diem.
        /// Chi GOI Y de bam, KHONG tu chay — doan sai ma tu chay thi te hon khong lam gi.
        /// </summary>
        private static List<MoTaCongCu> GoiYGanNhat(string daBoDau, int toiDa)
        {
            var tuHoi = TachTu(daBoDau).Where(x => x.Length > 1 && !TuRong.Contains(x)).Distinct().ToList();
            if (tuHoi.Count == 0) return new List<MoTaCongCu>();

            return TatCaCongCu()
                .Select(m =>
                {
                    var ten = new HashSet<string>(TachTu(BoDau(m.Ten).ToLowerInvariant()));
                    var mau = new HashSet<string>(TachTu(BoDau(m.ViDu).ToLowerInvariant()));
                    return new { M = m, Diem = tuHoi.Sum(x => ten.Contains(x) ? 2 : mau.Contains(x) ? 1 : 0) };
                })
                .Where(x => x.Diem >= 2)
                .OrderByDescending(x => x.Diem).ThenBy(x => x.M.DungTien)
                .Take(toiDa).Select(x => x.M).ToList();
        }

        /// <summary>Mot viec trong danh muc thanh mot dong bam duoc — dung chung cho danh muc va goi y.</summary>
        private static string DongCongCu(MoTaCongCu m)
        {
            // Viec GHI co ma rieng ("lap phieu cho MSCU2277456") thi chi DIEN SAN cau mau:
            // gui thang la dang lam tren ho so mau, khong phai ho so nguoi dung can.
            return m.DungTien && Regex.IsMatch(m.ViDu, @"\d") ? DongDien(m.Ten, m.ViDu) : Dong(m.Ten, null, m.ViDu);
        }
    }
}
