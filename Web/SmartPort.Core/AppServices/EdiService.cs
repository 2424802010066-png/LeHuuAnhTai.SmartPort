using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// EDI HANG TAU — UN/EDIFACT (CODECO / COPARN).
    ///
    /// VI SAO PHAI CO:
    /// Depot KHONG so huu vo container, no giu ho cho hang tau. Moi lan cont
    /// vao/ra bai hang tau bat buoc phai biet. Thuc te o Viet Nam phan lon
    /// depot nho van gui Excel qua email — nguoi go tay, sai so, den cuoi
    /// thang doi soat khong khop thi hang tau tru tien depot. Phan mem depot
    /// quoc te (ContainerHub, ContPark, DepotSystems) deu ban EDI nhu tinh
    /// nang loi; khong co la khong choi duoc o phan khuc nay.
    ///
    /// CODECO = bao cao vao/ra depot  (depot -> hang tau)
    /// COPARN = hang tau bao truoc     (hang tau -> depot) -> tu tao lenh
    ///
    /// CU PHAP EDIFACT (bo ky tu UNOA muc 2):
    ///   +  ngan cac phan tu trong mot doan
    ///   :  ngan thanh phan trong mot phan tu
    ///   '  ket thuc doan
    ///   ?  ky tu thoat (dung khi du lieu chua chinh 4 ky tu tren)
    /// </summary>
    public static class EdiService
    {
        // ============================================================
        // TIEN ICH CU PHAP
        // ============================================================

        /// <summary>
        /// Thoat ky tu dac biet. Bo qua buoc nay la ban tin hong ngay khi ten
        /// khach co dau ' hoac + — loi rat kho tim vi hang tau moi bao lai.
        /// </summary>
        public static string Thoat(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                if (c == '+' || c == ':' || c == '\'' || c == '?') sb.Append('?');
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Bo dau tieng Viet + chi giu ky tu bo UNOA cho phep (A-Z 0-9 va vai dau)</summary>
        public static string ChuanUNOA(string s, int toiDa = 0)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var chuan = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in chuan)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                var u = char.ToUpperInvariant(c);
                if (u == 'Đ') { sb.Append('D'); continue; }
                if ((u >= 'A' && u <= 'Z') || (u >= '0' && u <= '9') ||
                    u == ' ' || u == '.' || u == ',' || u == '-' || u == '(' || u == ')' || u == '/')
                    sb.Append(u);
            }
            var kq = sb.ToString().Trim();
            if (toiDa > 0 && kq.Length > toiDa) kq = kq.Substring(0, toiDa);
            return kq;
        }

        /// <summary>
        /// Ma kich thuoc/kieu ISO 6346 tu loai container noi bo.
        /// Hang tau doc ma nay chu khong doc "40HC" — sai ma la ban tin bi tu choi.
        /// </summary>
        public static string MaISO(string loaiCont)
        {
            var l = (loaiCont ?? "").ToUpperInvariant().Trim();
            bool la45 = l.StartsWith("45");
            bool la40 = l.StartsWith("40");
            // "RH" = reefer high-cube: cao 9'6" nhu HC, khong phai 8'6"
            bool laHC = l.Contains("HC") || l.Contains("RH");
            bool laRF = l.Contains("RF") || l.Contains("RH");
            bool laOT = l.Contains("OT");
            bool laFR = l.Contains("FR");
            bool laTK = l.Contains("TK") || l.Contains("TANK");

            string chieu = la45 ? "L" : (la40 ? (laHC ? "4" : "4") : "2");
            string cao = la45 ? "5" : (la40 ? (laHC ? "5" : "2") : "2");
            string kieu = laRF ? "R1" : laOT ? "U1" : laFR ? "P1" : laTK ? "T1" : "G1";
            // 20' thuong: 22G1 · 40' thuong: 42G1 · 40'HC: 45G1 · 40'RF: 45R1 · 45'HC: L5G1
            return chieu + cao + kieu;
        }

        private static string Ngay(DateTime d) { return d.ToString("yyyyMMddHHmm"); }
        private static string NgayUNB(DateTime d) { return d.ToString("yyMMdd") + ":" + d.ToString("HHmm"); }

        // ============================================================
        // SINH CODECO
        // ============================================================

        /// <summary>Mot dong bao cao vao/ra cho hang tau</summary>
        public class DongCodeco
        {
            public string SoCont { get; set; }
            public string LoaiCont { get; set; }
            public string TinhTrang { get; set; }     // Full / Empty / Rỗng
            public string HuongDi { get; set; }       // Vào / Ra
            public DateTime ThoiGian { get; set; }
            public string BienSoXe { get; set; }
            public string SoLenh { get; set; }
            public decimal? TrongLuongKg { get; set; }
            public string ViTriBai { get; set; }
        }

        public class KetQuaSinh
        {
            public string NoiDung { get; set; }
            public string SoThamChieu { get; set; }
            public string SoChungTu { get; set; }
            public int SoLuongCont { get; set; }
            public int SoDoan { get; set; }
        }

        /// <summary>
        /// Sinh ban tin CODECO D:95B bao cao cac luot container vao/ra depot.
        ///
        /// Diem de sai nhat: huong dich chuyen duoc ma hoa bang QUALIFIER CUA
        /// DTM chu khong phai bang mot truong rieng —
        ///   DTM+178 = thoi diem ĐẾN thuc te  (gate-in)
        ///   DTM+186 = thoi diem ĐI thuc te   (gate-out)
        /// </summary>
        public static KetQuaSinh SinhCodeco(string maDepot, string tenDepot, string maHangTau,
                                            List<DongCodeco> dong, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            var thamChieu = "SP" + now.ToString("yyMMddHHmmss");
            var chungTu = "CODECO" + now.ToString("yyMMddHHmm");
            var sb = new StringBuilder();
            var soDoan = 0;
            Action<string> ghi = s => { sb.Append(s).Append('\''); sb.Append('\n'); soDoan++; };

            var depot = ChuanUNOA(maDepot, 17);
            var hang = ChuanUNOA(maHangTau, 17);
            if (depot.Length == 0) depot = "SMARTPORT";
            if (hang.Length == 0) hang = "UNKNOWN";

            // --- Phong bi trao doi (khong tinh vao so doan cua UNT) ---
            sb.Append("UNB+UNOA:2+").Append(depot).Append(":ZZZ+").Append(hang).Append(":ZZZ+")
              .Append(NgayUNB(now)).Append('+').Append(thamChieu).Append("'\n");

            // --- Than ban tin ---
            ghi("UNH+1+CODECO:D:95B:UN");
            ghi("BGM+34+" + chungTu + "+9");                       // 34 = bao cao vao/ra container
            ghi("DTM+137:" + Ngay(now) + ":203");                  // 137 = ngay lap ban tin
            ghi("NAD+MS+" + depot + "::ZZZ");                      // ben gui
            ghi("NAD+MR+" + hang + "::ZZZ");                       // ben nhan
            ghi("NAD+CF+" + hang + "::ZZZ");                       // hang khai thac vo
            ghi("LOC+165+" + depot + ":139:ZZZ");                  // dia diem giao nhan

            foreach (var d in dong)
            {
                var vao = d.HuongDi == "Vào";
                var rong = (d.TinhTrang ?? "").IndexOf("ỗng", StringComparison.OrdinalIgnoreCase) >= 0
                        || string.Equals(d.TinhTrang, "Empty", StringComparison.OrdinalIgnoreCase);

                // EQD+CN+<so cont>+<ma ISO>:102:5++<trang thai>+<4 rong / 5 co hang>
                ghi("EQD+CN+" + Thoat(ChuanUNOA(d.SoCont, 17)) + "+" + MaISO(d.LoaiCont) + ":102:5++"
                    + (vao ? "3" : "2") + "+" + (rong ? "4" : "5"));

                if (!string.IsNullOrWhiteSpace(d.SoLenh))
                    ghi("RFF+BN:" + Thoat(ChuanUNOA(d.SoLenh, 35)));

                // Van tai duong bo (31) kem bien so xe
                if (!string.IsNullOrWhiteSpace(d.BienSoXe))
                    ghi("TDT+20+++31++" + Thoat(ChuanUNOA(d.BienSoXe, 17)));

                ghi("DTM+" + (vao ? "178" : "186") + ":" + Ngay(d.ThoiGian) + ":203");

                if (d.TrongLuongKg != null && d.TrongLuongKg > 0)
                    ghi("MEA+AAE+G+KGM:" + Math.Round(d.TrongLuongKg.Value).ToString("0", CultureInfo.InvariantCulture));

                if (!string.IsNullOrWhiteSpace(d.ViTriBai))
                    ghi("FTX+AAA+++" + Thoat(ChuanUNOA("VI TRI BAI " + d.ViTriBai, 70)));
            }

            ghi("CNT+16:" + dong.Count);
            var soDoanUNT = soDoan + 1;                            // UNT tu dem chinh no
            ghi("UNT+" + soDoanUNT + "+1");
            sb.Append("UNZ+1+").Append(thamChieu).Append("'\n");

            return new KetQuaSinh
            {
                NoiDung = sb.ToString(),
                SoThamChieu = thamChieu,
                SoChungTu = chungTu,
                SoLuongCont = dong.Count,
                SoDoan = soDoanUNT
            };
        }

        // ============================================================
        // SINH COARRI
        // ============================================================

        /// <summary>Mot container da do khoi tau hoac xep len tau</summary>
        public class DongCoarri
        {
            public string SoCont { get; set; }
            public string LoaiCont { get; set; }
            public string TinhTrang { get; set; }
            public decimal? TrongLuongTan { get; set; }
            public string ViTriBai { get; set; }
            public string SoBooking { get; set; }
        }

        /// <summary>
        /// Sinh COARRI D:95B — bao cao DO HANG / XEP HANG cua mot chuyen tau.
        ///
        /// Khac CODECO o cho: CODECO noi ve CONG (xe ra vao depot), con COARRI
        /// noi ve TAU (container roi tau hay len tau). Vi vay bat buoc phai co
        /// doan TDT mo ta con tau va so chuyen — thieu la hang tau khong biet
        /// bao cao thuoc chuyen nao.
        ///
        ///   BGM+45 = bao cao do/xep container
        ///   TDT+20 ... phuong thuc 1 = duong bien
        ///   LOC+9  = cang xep · LOC+11 = cang do
        /// </summary>
        public static KetQuaSinh SinhCoarri(string maDepot, string maHangTau, string tenTau, string soIMO,
                                            string maChuyen, string maCang, bool laDoHang,
                                            List<DongCoarri> dong, DateTime? thoiDiemTau = null, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            var thamChieu = "SP" + now.ToString("yyMMddHHmmss");
            var chungTu = "COARRI" + now.ToString("yyMMddHHmm");
            var sb = new StringBuilder();
            var soDoan = 0;
            Action<string> ghi = s => { sb.Append(s).Append('\'').Append('\n'); soDoan++; };

            var depot = ChuanUNOA(maDepot, 17); if (depot.Length == 0) depot = "SMARTPORT";
            var hang = ChuanUNOA(maHangTau, 17); if (hang.Length == 0) hang = "UNKNOWN";
            var cang = ChuanUNOA(maCang, 17); if (cang.Length == 0) cang = depot;

            sb.Append("UNB+UNOA:2+").Append(depot).Append(":ZZZ+").Append(hang).Append(":ZZZ+")
              .Append(NgayUNB(now)).Append('+').Append(thamChieu).Append("'\n");

            ghi("UNH+1+COARRI:D:95B:UN");
            ghi("BGM+45+" + chungTu + "+9");                    // 45 = bao cao do/xep container
            ghi("DTM+137:" + Ngay(now) + ":203");
            ghi("NAD+MS+" + depot + "::ZZZ");
            ghi("NAD+MR+" + hang + "::ZZZ");

            // Con tau + so chuyen — bat buoc voi COARRI
            var tau = Thoat(ChuanUNOA(tenTau, 35));
            var imo = ChuanUNOA(soIMO, 17);
            ghi("TDT+20+" + Thoat(ChuanUNOA(maChuyen, 17)) + "+1++" + hang + "+++"
                + (imo.Length > 0 ? imo : "UNKNOWN") + ":103:ZZZ:" + tau);

            // Do hang = tau la noi DEN (LOC+11 cang do) · Xep hang = LOC+9 cang xep
            ghi("LOC+" + (laDoHang ? "11" : "9") + "+" + cang + ":139:ZZZ");
            if (thoiDiemTau != null)
                ghi("DTM+" + (laDoHang ? "178" : "186") + ":" + Ngay(thoiDiemTau.Value) + ":203");

            foreach (var d in dong)
            {
                var rong = (d.TinhTrang ?? "").IndexOf("ỗng", StringComparison.OrdinalIgnoreCase) >= 0
                        || string.Equals(d.TinhTrang, "Empty", StringComparison.OrdinalIgnoreCase);

                ghi("EQD+CN+" + Thoat(ChuanUNOA(d.SoCont, 17)) + "+" + MaISO(d.LoaiCont) + ":102:5++"
                    + (laDoHang ? "3" : "2") + "+" + (rong ? "4" : "5"));

                if (!string.IsNullOrWhiteSpace(d.SoBooking))
                    ghi("RFF+BN:" + Thoat(ChuanUNOA(d.SoBooking, 35)));

                if (d.TrongLuongTan != null && d.TrongLuongTan > 0)
                    ghi("MEA+AAE+G+KGM:" + Math.Round(d.TrongLuongTan.Value * 1000m).ToString("0", CultureInfo.InvariantCulture));

                // 147 = o xep hang tren tau / vi tri bai
                if (!string.IsNullOrWhiteSpace(d.ViTriBai))
                    ghi("LOC+147+" + Thoat(ChuanUNOA(d.ViTriBai, 25)) + ":139:ZZZ");
            }

            ghi("CNT+16:" + dong.Count);
            var soDoanUNT = soDoan + 1;
            ghi("UNT+" + soDoanUNT + "+1");
            sb.Append("UNZ+1+").Append(thamChieu).Append("'\n");

            return new KetQuaSinh
            {
                NoiDung = sb.ToString(),
                SoThamChieu = thamChieu,
                SoChungTu = chungTu,
                SoLuongCont = dong.Count,
                SoDoan = soDoanUNT
            };
        }

        // ============================================================
        // DOC COPARN
        // ============================================================

        public class DongCoparn
        {
            public string SoCont { get; set; }
            public string MaISO { get; set; }
            public string LoaiContNoiBo { get; set; }
            public bool Rong { get; set; }
            public string SoBooking { get; set; }
            public DateTime? HenLuc { get; set; }
            public string BienSoXe { get; set; }
            public string Loi { get; set; }
        }

        public class KetQuaDoc
        {
            public bool Ok { get; set; }
            public string Loi { get; set; }
            public string LoaiBanTin { get; set; }
            public string BenGui { get; set; }
            public string BenNhan { get; set; }
            public string SoThamChieu { get; set; }
            public string SoChungTu { get; set; }
            public List<DongCoparn> Dong { get; set; }
            public KetQuaDoc() { Dong = new List<DongCoparn>(); }
        }

        /// <summary>
        /// Tach ban tin thanh cac doan, TON TRONG ky tu thoat '?'.
        /// Tach bang Split('\'') la sai ngay khi du lieu co "?'" — day la loi
        /// kinh dien cua nguoi moi lam EDI.
        /// </summary>
        public static List<string> TachDoan(string noiDung)
        {
            var ds = new List<string>();
            if (string.IsNullOrEmpty(noiDung)) return ds;
            var sb = new StringBuilder();
            var thoat = false;
            foreach (var c in noiDung)
            {
                if (thoat) { sb.Append(c); thoat = false; continue; }
                if (c == '?') { sb.Append(c); thoat = true; continue; }
                if (c == '\'')
                {
                    var d = sb.ToString().Trim(' ', '\r', '\n');
                    if (d.Length > 0) ds.Add(d);
                    sb.Clear();
                    continue;
                }
                if (c == '\r' || c == '\n') continue;
                sb.Append(c);
            }
            var cuoi = sb.ToString().Trim(' ', '\r', '\n');
            if (cuoi.Length > 0) ds.Add(cuoi);
            return ds;
        }

        /// <summary>Tach mot doan thanh cac phan tu, cung ton trong '?'</summary>
        public static List<string> TachPhanTu(string doan)
        {
            var ds = new List<string>();
            var sb = new StringBuilder();
            var thoat = false;
            foreach (var c in doan)
            {
                if (thoat) { sb.Append(c); thoat = false; continue; }
                if (c == '?') { thoat = true; continue; }     // bo dau ? khi GIAI ma
                if (c == '+') { ds.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(c);
            }
            ds.Add(sb.ToString());
            return ds;
        }

        private static string TP(List<string> pt, int i)
        {
            return i < pt.Count ? pt[i] : "";
        }

        private static string TPTP(List<string> pt, int i, int j)
        {
            var p = TP(pt, i);
            var c = p.Split(':');
            return j < c.Length ? c[j] : "";
        }

        /// <summary>Doi ma ISO 6346 nguoc lai thanh loai noi bo de tao lenh cho dung</summary>
        public static string LoaiTuMaISO(string maIso)
        {
            var m = (maIso ?? "").ToUpperInvariant().Trim();
            if (m.Length < 4) return "40HC";
            var chieu = m[0];
            var cao = m[1];
            var kieu = m.Substring(2, 2);
            var co = chieu == '2' ? "20" : (chieu == 'L' ? "45" : "40");
            if (kieu.StartsWith("R")) return co + "RF";
            if (kieu.StartsWith("U")) return co + "OT";
            if (kieu.StartsWith("P")) return co + "FR";
            if (kieu.StartsWith("T")) return co + "TK";
            var hc = (cao == '5' || cao == '9');
            return co + (hc && co != "20" ? "HC" : "GP");
        }

        /// <summary>
        /// Doc ban tin COPARN (hang tau bao truoc container se lay/tra).
        /// Bo qua doan la, chi lay nhung gi can de lap lenh giao nhan.
        /// </summary>
        public static KetQuaDoc DocCoparn(string noiDung)
        {
            var kq = new KetQuaDoc();
            var doans = TachDoan(noiDung);
            if (doans.Count == 0)
            {
                kq.Loi = "Nội dung rỗng hoặc không phải bản tin EDIFACT (không tìm thấy dấu kết đoạn ').";
                return kq;
            }

            DongCoparn hienTai = null;
            foreach (var d in doans)
            {
                var pt = TachPhanTu(d);
                var tag = TP(pt, 0);

                switch (tag)
                {
                    case "UNB":
                        kq.BenGui = TPTP(pt, 2, 0);
                        kq.BenNhan = TPTP(pt, 3, 0);
                        kq.SoThamChieu = TP(pt, 5);
                        break;

                    case "UNH":
                        kq.LoaiBanTin = TPTP(pt, 2, 0);
                        break;

                    case "BGM":
                        kq.SoChungTu = TPTP(pt, 2, 0);
                        if (string.IsNullOrEmpty(kq.SoChungTu)) kq.SoChungTu = TP(pt, 2);
                        break;

                    case "EQD":
                        // Moi EQD mo mot container moi
                        hienTai = new DongCoparn
                        {
                            SoCont = TP(pt, 2).Trim().ToUpperInvariant(),
                            MaISO = TPTP(pt, 3, 0),
                            Rong = TP(pt, 6) == "4"
                        };
                        hienTai.LoaiContNoiBo = LoaiTuMaISO(hienTai.MaISO);
                        kq.Dong.Add(hienTai);
                        break;

                    case "RFF":
                        if (hienTai != null)
                        {
                            var loai = TPTP(pt, 1, 0);
                            if (loai == "BN" || loai == "BM" || loai == "CT")
                                hienTai.SoBooking = TPTP(pt, 1, 1);
                        }
                        break;

                    case "DTM":
                        if (hienTai != null)
                        {
                            var q = TPTP(pt, 1, 0);
                            // 2 = ngay yeu cau giao · 178 = den thuc te · 132 = du kien den
                            if (q == "2" || q == "132" || q == "178" || q == "186")
                            {
                                var giaTri = TPTP(pt, 1, 1);
                                DateTime dt;
                                if (DateTime.TryParseExact(giaTri, "yyyyMMddHHmm", CultureInfo.InvariantCulture,
                                                           DateTimeStyles.None, out dt)) hienTai.HenLuc = dt;
                                else if (DateTime.TryParseExact(giaTri, "yyyyMMdd", CultureInfo.InvariantCulture,
                                                           DateTimeStyles.None, out dt)) hienTai.HenLuc = dt;
                            }
                        }
                        break;

                    case "TDT":
                        if (hienTai != null)
                        {
                            var xe = TP(pt, 6);
                            if (!string.IsNullOrWhiteSpace(xe)) hienTai.BienSoXe = xe.Trim().ToUpperInvariant();
                        }
                        break;
                }
            }

            if (kq.Dong.Count == 0)
            {
                kq.Loi = "Không tìm thấy đoạn EQD nào — bản tin không chứa container.";
                return kq;
            }

            // Kiem tra tung dong, KHONG loai bo — de nguoi dung thay ro dong nao hong
            foreach (var d in kq.Dong)
            {
                if (string.IsNullOrWhiteSpace(d.SoCont))
                    d.Loi = "Thiếu số container";
                else if (!System.Text.RegularExpressions.Regex.IsMatch(d.SoCont, @"^[A-Z]{4}\d{7}$"))
                    d.Loi = "Số container sai định dạng ISO 6346 (4 chữ + 7 số)";
            }

            kq.Ok = true;
            return kq;
        }
    }
}
