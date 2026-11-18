using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartPort.Services
{
    /// <summary>
    /// ĐỌC GIẤY TỜ TÙY THÂN tại cổng.
    ///
    /// VI SAO KHONG DUNG OCR CHO CCCD:
    /// The CCCD gan chip cua Viet Nam co san MA QR o mat truoc, ben trong la
    /// chuoi 7 truong ngan cach bang dau |. Quet ma QR cho ket qua CHINH XAC
    /// TUYET DOI, khong phu thuoc anh mo/nghieng/nguoc sang nhu OCR. Nguoi
    /// moi lam hay lao ngay vao OCR roi vat va voi do chinh xac — trong khi
    /// du lieu da nam san trong QR.
    ///
    /// OCR (Tesseract) chi dung cho BANG LAI, vi bang lai khong co QR.
    ///
    /// PHAN TICH O DAY (may chu) chu khong tin ket qua client gui len: trinh
    /// duyet chi gui CHUOI THO doc duoc, may chu tu bo ra cac truong.
    /// </summary>
    public static class GiayToService
    {
        public class KetQuaCccd
        {
            public bool Ok { get; set; }
            public string Loi { get; set; }
            public string SoCccd { get; set; }
            public string SoCmndCu { get; set; }
            public string HoTen { get; set; }
            public DateTime? NgaySinh { get; set; }
            public string GioiTinh { get; set; }
            public string DiaChi { get; set; }
            public DateTime? NgayCap { get; set; }
            /// <summary>Diem nghi van can nguoi xem lai — KHONG chan luu</summary>
            public System.Collections.Generic.List<string> CanhBao { get; } = new System.Collections.Generic.List<string>();

            public int? Tuoi
            {
                get
                {
                    if (NgaySinh == null) return null;
                    var t = DateTime.Today.Year - NgaySinh.Value.Year;
                    if (NgaySinh.Value.Date > DateTime.Today.AddYears(-t)) t--;
                    return t;
                }
            }
        }

        /// <summary>
        /// Doc chuoi trong ma QR the CCCD gan chip.
        /// Dinh dang: SoCCCD|SoCMND|HoTen|NgaySinh|GioiTinh|DiaChi|NgayCap. Ngay theo kieu ddMMyyyy.
        /// Truong SoCMND co the rong voi nguoi chua tung co CMND 9 so — khong duoc coi do la loi.
        ///
        /// HAI MUC XU LY: sai CAU TRUC (so khong du 12 chu so, ten/ngay sinh khong hop le) thi TU CHOI vi day khong
        /// phai ma QR cua the; con DIEM NGHI VAN (so CCCD khong khop gioi tinh/nam sinh, ma tinh la, ngay cap hong)
        /// chi ghi vao CanhBao de nguoi truc cong xem lai, khong chan — mot quy tac sai khong duoc lam ket xe o cong.
        /// </summary>
        public static KetQuaCccd DocQrCccd(string chuoi)
        {
            var kq = new KetQuaCccd();
            var s = LamSach(chuoi);
            if (s.Length == 0)
            {
                kq.Loi = "Chưa đọc được nội dung mã QR.";
                return kq;
            }

            var p = s.Split('|').Select(x => x.Trim()).ToList();
            while (p.Count > 6 && p[p.Count - 1].Length == 0) p.RemoveAt(p.Count - 1); // "...|20082021||" cua mot so dau doc
            if (p.Count < 6)
            {
                kq.Loi = "Mã QR này không phải thẻ CCCD gắn chip (đọc được \""
                       + (s.Length > 40 ? s.Substring(0, 40) + "…" : s) + "\").";
                return kq;
            }

            kq.SoCccd = p[0];
            kq.SoCmndCu = p[1];
            kq.HoTen = ChuanTen(p[2]);
            kq.GioiTinh = ChuanGioiTinh(p[4], kq);

            // CCCD dung 12 chu so — sai la doc nham ma QR khac
            if (!Regex.IsMatch(kq.SoCccd, @"^\d{12}$"))
            {
                kq.Loi = "Số CCCD \"" + kq.SoCccd + "\" không hợp lệ (phải đủ 12 chữ số).";
                return kq;
            }
            if (string.IsNullOrWhiteSpace(kq.HoTen))
            {
                kq.Loi = "Mã QR thiếu họ tên.";
                return kq;
            }
            if (kq.HoTen.Length < 2 || !Regex.IsMatch(kq.HoTen, @"^[\p{L}\p{M}][\p{L}\p{M}\s'.\-]*$"))
            {
                kq.Loi = "Họ tên trong mã QR không hợp lệ — không phải thẻ CCCD.";
                return kq;
            }

            // Ngay sinh bat buoc. Rieng the chi ghi nam sinh (nguoi lon tuoi khong ro ngay) thi nhan va canh bao.
            int? namSinh = null;
            var mNam = Regex.Match(p[3], @"^(?:0000|00/00/)?(\d{4})$");
            if (p[3].Length == 0)
            {
                kq.Loi = "Mã QR thiếu ngày sinh — không phải thẻ CCCD hợp lệ.";
                return kq;
            }
            if (mNam.Success)
            {
                namSinh = int.Parse(mNam.Groups[1].Value);
                if (namSinh < 1900 || namSinh > DateTime.Today.Year)
                {
                    kq.Loi = "Năm sinh \"" + p[3] + "\" không hợp lệ.";
                    return kq;
                }
                kq.CanhBao.Add("Thẻ chỉ ghi năm sinh, không có ngày tháng.");
            }
            else
            {
                kq.NgaySinh = DocNgay(p[3]);
                if (kq.NgaySinh == null || kq.NgaySinh.Value.Date > DateTime.Today || kq.NgaySinh.Value.Year < 1900)
                {
                    kq.NgaySinh = null;
                    kq.Loi = "Ngày sinh \"" + p[3] + "\" không hợp lệ.";
                    return kq;
                }
                namSinh = kq.NgaySinh.Value.Year;
            }

            // Dia chi co the chua dau | -> ngay cap la truong ngay DAU TIEN tinh tu vi tri 6, phan giua la dia chi.
            // The doi moi co them truong sau ngay cap thi cac truong do bi bo qua.
            kq.DiaChi = p[5];
            if (p.Count >= 7)
            {
                var iNgay = -1;
                for (var i = 6; i < p.Count && iNgay < 0; i++)
                    if (Regex.IsMatch(p[i], @"^\d{8}$|^\d{2}/\d{2}/\d{4}$") && DocNgay(p[i]) != null) iNgay = i;
                if (iNgay >= 0)
                {
                    kq.DiaChi = string.Join(", ", p.Skip(5).Take(iNgay - 5).Where(x => x.Length > 0));
                    kq.NgayCap = DocNgay(p[iNgay]);
                    if (kq.NgayCap.Value.Date > DateTime.Today || (kq.NgaySinh != null && kq.NgayCap.Value.Date < kq.NgaySinh.Value.Date))
                    {
                        kq.NgayCap = null;
                        kq.CanhBao.Add("Ngày cấp trong mã QR không hợp lý (ở tương lai hoặc trước ngày sinh) — đã để trống.");
                    }
                }
                else kq.CanhBao.Add("Không đọc được ngày cấp trong mã QR — đã để trống.");
            }

            DoiChieuSoCccd(kq, namSinh);
            kq.Ok = true;
            return kq;
        }

        /// <summary>Bo ky tu dieu khien/dinh dang (BOM, zero-width, NUL, xuong dong) va chuan hoa Unicode ve NFC</summary>
        private static string LamSach(string chuoi)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return "";
            var sb = new StringBuilder(chuoi.Length);
            foreach (var c in chuoi)
            {
                var loai = CharUnicodeInfo.GetUnicodeCategory(c);
                if (loai == UnicodeCategory.Format) continue;
                sb.Append(loai == UnicodeCategory.Control ? ' ' : c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
        }

        private static string ChuanGioiTinh(string s, KetQuaCccd kq)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            switch (BoDau(s).Trim().ToUpperInvariant())
            {
                case "NAM": case "MALE": case "M": return "Nam";
                case "NU": case "FEMALE": case "F": return "Nữ";
            }
            kq.CanhBao.Add("Giới tính trong mã QR không nhận ra được — đã để trống.");
            return null;
        }

        // Ma tinh/thanh tren so CCCD (Thong tu 07/2016/TT-BCA). Chi dung de CANH BAO, khong tu choi.
        private static readonly System.Collections.Generic.HashSet<string> MaTinhCccd = new System.Collections.Generic.HashSet<string>(
            ("001 002 004 006 008 010 011 012 014 015 017 019 020 022 024 025 026 027 030 031 033 034 035 036 037 038 040 042 044 045 "
           + "046 048 049 051 052 054 056 058 060 062 064 066 067 068 070 072 074 075 077 079 080 082 083 084 086 087 089 091 092 093 094 095 096").Split(' '));

        /// <summary>
        /// 12 so CCCD: 3 so ma tinh, 1 so the ky + gioi tinh (0/1 = nam/nu the ky 20, 2/3 = the ky 21...), 2 so cuoi nam sinh, 6 so ngau nhien.
        /// Doi chieu voi gioi tinh, nam sinh trong chinh ma QR: lech nhau la dau hieu the gia hoac ma QR bi sua.
        /// </summary>
        private static void DoiChieuSoCccd(KetQuaCccd kq, int? namSinh)
        {
            var so = kq.SoCccd;
            if (!MaTinhCccd.Contains(so.Substring(0, 3)))
                kq.CanhBao.Add("Mã tỉnh \"" + so.Substring(0, 3) + "\" trong số CCCD không có trong danh mục — kiểm tra lại thẻ.");
            var c = so[3] - '0';
            var gtTheoSo = c % 2 == 0 ? "Nam" : "Nữ";
            if (kq.GioiTinh != null && kq.GioiTinh != gtTheoSo)
                kq.CanhBao.Add("Số CCCD ứng với giới tính " + gtTheoSo + " nhưng mã QR ghi " + kq.GioiTinh + " — kiểm tra lại thẻ.");
            var namTheoSo = 1900 + (c / 2) * 100 + int.Parse(so.Substring(4, 2));
            if (namSinh != null && namSinh != namTheoSo)
                kq.CanhBao.Add("Số CCCD ứng với năm sinh " + namTheoSo + " nhưng mã QR ghi " + namSinh + " — kiểm tra lại thẻ.");
        }

        private static DateTime? DocNgay(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            DateTime d;
            foreach (var dinhDang in new[] { "ddMMyyyy", "dd/MM/yyyy", "yyyy-MM-dd", "ddMMyy" })
                if (DateTime.TryParseExact(s, dinhDang, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return d;
            return null;
        }

        /// <summary>Ho ten trong QR viet HOA het — doi ve dang Chu Hoa Dau Tu cho de doc</summary>
        private static string ChuanTen(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return s;
            var tu = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tu.Select(t =>
                t.Length == 1 ? t.ToUpperInvariant()
                              : char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant()));
        }

        // ============================================================
        // BANG LAI (OCR)
        // ============================================================

        public class KetQuaGplx
        {
            public bool Ok { get; set; }
            public string Loi { get; set; }
            public string SoGplx { get; set; }
            public string Hang { get; set; }
            public DateTime? HanDen { get; set; }
            public bool DaHetHan { get { return HanDen != null && HanDen.Value.Date < DateTime.Today; } }
        }

        /// <summary>
        /// Boc thong tin bang lai tu VAN BAN THO ma OCR doc duoc.
        ///
        /// OCR bang lai khong bao gio sach — anh nghieng, bong den, chu in
        /// chong len hoa van. Nen o day KHONG co gang doc het moi thu, chi
        /// bat 3 thu quan trong nhat bang bieu thuc chinh quy, con lai de
        /// nguoi dung sua tay. Doan chac an van tot hon doan bua.
        ///
        /// Trinh duyet OCR bang lai bang du lieu TIENG VIET (vie) -> van ban CO DAU ("HẠNG", "ĐẾN");
        /// ban cu dung tieng Anh (eng) -> khong dau ("Hang", "den"). Moi bieu thuc chinh quy chay tren
        /// ban BO DAU nen dung cho ca hai (truoc day "Đ[EÊ]N" truot chu "ĐẾN" co dau sac).
        /// </summary>
        public static KetQuaGplx DocOcrGplx(string vanBan)
        {
            var kq = new KetQuaGplx();
            if (string.IsNullOrWhiteSpace(vanBan))
            {
                kq.Loi = "OCR không đọc được chữ nào — chụp lại rõ hơn, đủ sáng, để thẳng.";
                return kq;
            }

            var t = BoDau(vanBan).ToUpperInvariant();

            // OCR hay doc nham chu thanh so: O→0, I/L→1, B→8, S→5, Z→2, G→6.
            // CHI ap dung cho ban sao dung de BAT DAY SO — neu doi tren ca chuoi
            // thi "Hạng: B2" bien thanh "Hạng: 82", hong luon truong hang.
            var soHoa = new string(t.Select(c =>
                  c == 'O' ? '0'
                : c == 'I' || c == 'L' ? '1'
                : c == 'B' ? '8'
                : c == 'S' ? '5'
                : c == 'Z' ? '2'
                : c == 'G' ? '6' : c).ToArray());

            // So GPLX Viet Nam: 12 chu so (mau moi) hoac 10-12 so
            var mSo = Regex.Match(soHoa, @"\b(\d{12})\b");
            if (!mSo.Success) mSo = Regex.Match(soHoa, @"\b(\d{10,12})\b");
            if (mSo.Success) kq.SoGplx = mSo.Groups[1].Value;

            // Hang: mau cu (A1-A4, B1, B2, C, D, E, FB2, FC, FD, FE) va mau moi tu 2025 (A, B, C1, D1, D2, BE, C1E, CE, D1E, D2E, DE).
            // The in "Hạng/Class: C" -> sau HANG co the chen "/CLASS". Ma DAI dat TRUOC ma ngan (C1E truoc C1 truoc C).
            const string maHang = @"(FB2|FC|FD|FE|C1E|D1E|D2E|BE|CE|DE|A1|A2|A3|A4|B1|B2|C1|D1|D2|A|B|C|D|E|F)";
            const string nhanHang = @"\b(?:HANG|CLASS)\s*(?:/\s*(?:CLASS|HANG)\s*)?[:\-.]?\s*";
            var mHang = Regex.Match(t, nhanHang + maHang + @"\b");
            // Khong thay nhan "Hang" -> chi nhan ma DAI de nhan (1 chu cai don nhu "C" de trung chu thuong)
            if (!mHang.Success) mHang = Regex.Match(t, @"\b(FB2|FC|FD|FE|C1E|D1E|D2E|B2|B1)\b");
            if (mHang.Success) kq.Hang = mHang.Groups[1].Value;
            else
            {
                // OCR hay doc "B2" thanh "82": ngay sau nhan "Hang/Class" thi so 8 chinh la chu B
                var m8 = Regex.Match(t, nhanHang + @"8([12])\b");
                if (m8.Success) kq.Hang = "B" + m8.Groups[1].Value;
            }

            // Han: "Có giá trị đến/Expires: 20/08/2031" (bo dau: "CO GIA TRI DEN/EXPIRES: ...") hoac chi mot ngay dd/MM/yyyy.
            // Bang "Không thời hạn" (vd hang A1): KHONG doan ngay xa nhat (se lay nham ngay cap) — de trong cho nguoi dung xem.
            var khongThoiHan = Regex.IsMatch(t, @"KHONG\s*THOI\s*HAN|UNLIMITED");
            var mHan = Regex.Match(t, @"(?:\bDEN|EXPIRES?|VALID\s*UNTIL)\D{0,14}(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})");
            if (!mHan.Success && !khongThoiHan)
            {
                var ds = Regex.Matches(t, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\b");
                // Lay ngay XA NHAT — han su dung luon sau ngay cap
                DateTime? xa = null;
                foreach (Match m in ds)
                {
                    DateTime d;
                    if (DateTime.TryParseExact(m.Groups[1].Value.PadLeft(2, '0') + "/" +
                                               m.Groups[2].Value.PadLeft(2, '0') + "/" + m.Groups[3].Value,
                                               "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                        if (xa == null || d > xa) xa = d;
                }
                kq.HanDen = xa;
            }
            else if (mHan.Success)
            {
                DateTime d;
                if (DateTime.TryParseExact(mHan.Groups[1].Value.PadLeft(2, '0') + "/" +
                                           mHan.Groups[2].Value.PadLeft(2, '0') + "/" + mHan.Groups[3].Value,
                                           "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    kq.HanDen = d;
            }

            if (kq.SoGplx == null && kq.Hang == null && kq.HanDen == null)
            {
                kq.Loi = "Không bóc được thông tin nào từ ảnh — nhập tay hoặc chụp lại rõ hơn.";
                return kq;
            }
            kq.Ok = true;
            return kq;
        }

        /// <summary>Bo dau tieng Viet (Đ -> D) — so khop khong phu thuoc OCR doc co dau hay khong</summary>
        private static string BoDau(string s)
        {
            var chuan = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(chuan.Length);
            foreach (var c in chuan)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }
}
