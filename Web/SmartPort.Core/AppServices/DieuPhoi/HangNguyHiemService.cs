using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services.DieuPhoi
{
    /// <summary>
    /// HANG NGUY HIEM IMDG TRONG BAI. Bang cach ly lay nguyen van IMDG Code muc 7.2.4 (Amdt. 40-20).
    /// IMDG quy dinh cho tau bien; cu ly doi ra met trong bai la THAM SO NOI BO cua cang (CuLyCanM), khong phai so cua IMDG.
    /// O 'X' nghia la phai tra them Danh muc hang nguy hiem (DGL) cua tung ma UN, '*' xem 7.2.7.1 — he thong chi nhac, khong tu ket luan.
    /// </summary>
    public static class HangNguyHiemService
    {
        public static readonly string[] CacLop = { "1.1", "1.3", "1.4", "2.1", "2.2", "2.3", "3", "4.1", "4.2", "4.3", "5.1", "5.2", "6.1", "6.2", "7", "8", "9" };

        public static readonly Dictionary<string, string> TenLop = new Dictionary<string, string>
        {
            ["1.1"] = "Nổ 1.1, 1.2, 1.5", ["1.3"] = "Nổ 1.3, 1.6", ["1.4"] = "Nổ 1.4", ["2.1"] = "Khí dễ cháy", ["2.2"] = "Khí không cháy, không độc",
            ["2.3"] = "Khí độc", ["3"] = "Chất lỏng dễ cháy", ["4.1"] = "Chất rắn dễ cháy", ["4.2"] = "Chất dễ tự bốc cháy", ["4.3"] = "Gặp nước sinh khí cháy",
            ["5.1"] = "Chất oxy hóa", ["5.2"] = "Peroxit hữu cơ", ["6.1"] = "Chất độc", ["6.2"] = "Chất lây nhiễm", ["7"] = "Phóng xạ", ["8"] = "Chất ăn mòn", ["9"] = "Nguy hiểm khác",
        };

        // Thu tu cot va hang theo CacLop
        private static readonly string[][] Bang =
        {
            "* * * 4 2 2 4 4 4 4 4 4 2 4 2 4 X".Split(' '),
            "* * * 4 2 2 4 3 3 4 4 4 2 4 2 2 X".Split(' '),
            "* * * 2 1 1 2 2 2 2 2 2 X 4 2 2 X".Split(' '),
            "4 4 2 X X X 2 1 2 2 2 2 X 4 2 1 X".Split(' '),
            "2 2 1 X X X 1 X 1 X X 1 X 2 1 X X".Split(' '),
            "2 2 1 X X X 2 X 2 X X 2 X 2 1 X X".Split(' '),
            "4 4 2 2 1 2 X X 2 2 2 2 X 3 2 X X".Split(' '),
            "4 3 2 1 X X X X 1 X 1 2 X 3 2 1 X".Split(' '),
            "4 3 2 2 1 2 2 1 X 1 2 2 1 3 2 1 X".Split(' '),
            "4 4 2 2 X X 2 X 1 X 2 2 X 2 2 1 X".Split(' '),
            "4 4 2 2 X X 2 1 2 2 X 2 1 3 1 2 X".Split(' '),
            "4 4 2 2 1 2 2 2 2 2 2 X 1 3 2 2 X".Split(' '),
            "2 2 X X X X X X 1 X 1 1 X 1 X X X".Split(' '),
            "4 4 4 4 2 2 3 3 3 2 3 3 1 X 3 3 X".Split(' '),
            "2 2 2 2 1 1 2 2 2 2 1 2 X 3 X 2 X".Split(' '),
            "4 2 2 1 X X X 1 1 1 2 2 X 3 2 X X".Split(' '),
            "X X X X X X X X X X X X X X X X X".Split(' '),
        };

        private const double BuocBayM = 6.1;
        private const double BuocHangM = 2.44;
        private const double CachKhuM = 30;   // lan duong noi bo toi thieu giua hai khu

        private static readonly Dictionary<string, (string Ten, double CanM)> Muc = new Dictionary<string, (string, double)>
        {
            ["1"] = ("Cách xa (away from)", 3),
            ["2"] = ("Tách biệt (separated from)", 6.1),
            ["3"] = ("Tách biệt bằng cả khoang (by a complete compartment)", 12.2),
            ["4"] = ("Tách dọc qua một khoang (longitudinally)", 24.4),
        };

        public static string ChuanLop(string lop)
        {
            var s = (lop ?? "").Trim();
            if (s == "1.2" || s == "1.5") return "1.1";
            if (s == "1.6") return "1.3";
            return CacLop.Contains(s) ? s : null;
        }

        public static string MaCachLy(string a, string b)
        {
            var i = Array.IndexOf(CacLop, ChuanLop(a));
            var j = Array.IndexOf(CacLop, ChuanLop(b));
            return i < 0 || j < 0 ? null : Bang[i][j];
        }

        public static object MaTran() => new
        {
            Lop = CacLop.Select(l => new { Ma = l, Ten = TenLop[l] }),
            Bang,
            ChuGiai = Muc.Select(m => new { Ma = m.Key, m.Value.Ten, m.Value.CanM }),
            GhiChu = "X: tra Danh mục hàng nguy hiểm (DGL) theo số UN. *: xem IMDG 7.2.7.1. Bảng theo IMDG Code 7.2.4; cự ly đổi ra mét là tham số nội bộ của cảng.",
        };

        private static readonly Regex MauViTri = new Regex(@"^([A-Z])-(\d{2})-(\d{2})-(\d)$", RegexOptions.Compiled);

        public static bool ViTriHopLe(string vt) => vt != null && MauViTri.IsMatch(vt);

        /// <summary>Khoang cach giua 2 o bai (m). Cung chong = 0. Khac khu lay cu ly lan duong toi thieu.</summary>
        public static double? KhoangCach(string a, string b)
        {
            var ma = MauViTri.Match(a ?? "");
            var mb = MauViTri.Match(b ?? "");
            if (!ma.Success || !mb.Success) return null;
            if (ma.Groups[1].Value != mb.Groups[1].Value) return CachKhuM;
            var dBay = (int.Parse(ma.Groups[2].Value) - int.Parse(mb.Groups[2].Value)) * BuocBayM;
            var dHang = (int.Parse(ma.Groups[3].Value) - int.Parse(mb.Groups[3].Value)) * BuocHangM;
            return Math.Round(Math.Sqrt(dBay * dBay + dHang * dHang), 1);
        }

        private sealed class DongDg
        {
            public KhaiBaoNguyHiem K;
            public string ViTri;
        }

        private static List<DongDg> HangTrongBai(AppDb db)
        {
            var khai = db.KhaiBaoNguyHiems.AsNoTracking().Where(k => k.ConHieuLuc).ToList();
            var so = khai.Select(k => k.SoCont).ToList();
            var vt = db.Containers.AsNoTracking().Where(c => so.Contains(c.SoCont) && c.NgayRaBai == null).Select(c => new { c.SoCont, c.ViTriBai }).ToList();
            return khai.Select(k => new DongDg { K = k, ViTri = vt.FirstOrDefault(c => c.SoCont == k.SoCont)?.ViTriBai }).ToList();
        }

        private static object DanhGiaCap(string soA, string viTriA, string lopA, string soB, string viTriB, string lopB)
        {
            var ma = MaCachLy(lopA, lopB);
            var kc = KhoangCach(viTriA, viTriB);
            var coYeuCau = ma != null && Muc.ContainsKey(ma);
            var can = coYeuCau ? Muc[ma].CanM : 0;
            return new
            {
                SoA = soA, ViTriA = viTriA, LopA = lopA, SoB = soB, ViTriB = viTriB, LopB = lopB,
                MaCachLy = ma,
                TenCachLy = coYeuCau ? Muc[ma].Ten : (ma == "*" ? "Xem IMDG 7.2.7.1" : "Tra DGL theo số UN"),
                CanM = can,
                ThucTeM = kc,
                ViPham = coYeuCau && kc.HasValue && kc.Value < can,
                CanTraThem = !coYeuCau,
            };
        }

        /// <summary>Danh sach khai bao + moi cap can xem xet trong bai.</summary>
        public static object TongQuan(AppDb db)
        {
            var ds = HangTrongBai(db);
            var cap = new List<object>();
            for (var i = 0; i < ds.Count; i++)
                for (var j = i + 1; j < ds.Count; j++)
                    if (ViTriHopLe(ds[i].ViTri) && ViTriHopLe(ds[j].ViTri))
                        cap.Add(DanhGiaCap(ds[i].K.SoCont, ds[i].ViTri, ds[i].K.LopIMDG, ds[j].K.SoCont, ds[j].ViTri, ds[j].K.LopIMDG));
            return new
            {
                Khai = ds.Select(d => new { d.K.MaKhaiBao, d.K.SoCont, d.K.SoUN, d.K.TenHang, d.K.LopIMDG, TenLop = TenLop.GetValueOrDefault(ChuanLop(d.K.LopIMDG) ?? ""), d.K.NhomDongGoi, d.K.ONhiemBien, d.K.KhaiLuc, d.ViTri }),
                Cap = cap,
            };
        }

        public static (KhaiBaoNguyHiem Kb, string Loi) Khai(AppDb db, string soCont, string soUn, string tenHang, string lop, string nhom, bool oNhiem, string nguoi)
        {
            var so = (soCont ?? "").Trim().ToUpperInvariant();
            if (ChuanLop(lop) == null) return (null, "Lớp IMDG không hợp lệ.");
            if (string.IsNullOrWhiteSpace(tenHang)) return (null, "Nhập tên hàng theo vận đơn.");
            var un = (soUn ?? "").Trim();
            if (!Regex.IsMatch(un, @"^\d{4}$")) return (null, "Số UN gồm đúng 4 chữ số.");
            if (!db.Containers.Any(c => c.SoCont == so)) return (null, "Không có container " + so + " trong hệ thống.");
            if (db.KhaiBaoNguyHiems.Any(k => k.SoCont == so && k.ConHieuLuc)) return (null, "Container này đã có khai báo hàng nguy hiểm.");
            var kb = new KhaiBaoNguyHiem { SoCont = so, SoUN = un, TenHang = tenHang.Trim(), LopIMDG = lop.Trim(), NhomDongGoi = string.IsNullOrWhiteSpace(nhom) ? null : nhom.Trim(), ONhiemBien = oNhiem, NguoiKhai = nguoi, KhaiLuc = DateTime.Now, ConHieuLuc = true };
            db.KhaiBaoNguyHiems.Add(kb);
            db.SaveChanges();
            return (kb, null);
        }

        public static string Huy(AppDb db, int id)
        {
            var k = db.KhaiBaoNguyHiems.Find(id);
            if (k == null) return "Không tìm thấy khai báo.";
            k.ConHieuLuc = false;
            db.SaveChanges();
            return null;
        }

        /// <summary>Goi y o bai TRONG (nen chong trong) xa hang xung khac nhat. Khong de vao khu R: o co dien danh cho cont lanh.</summary>
        public static object GoiYViTri(AppDb db, string soCont)
        {
            var so = (soCont ?? "").Trim().ToUpperInvariant();
            var ds = HangTrongBai(db);
            var ban = ds.FirstOrDefault(d => d.K.SoCont == so);
            if (ban == null) return null;
            var conLai = ds.Where(d => d.K.SoCont != so && ViTriHopLe(d.ViTri)).ToList();
            var daDung = new HashSet<string>(db.Containers.AsNoTracking().Where(c => c.NgayRaBai == null && c.ViTriBai != null).Select(c => c.ViTriBai).ToList()
                .Where(ViTriHopLe).Select(v => v.Substring(0, v.Length - 2)));
            var ungVien = new List<(string ViTri, double DuM, int ViPham)>();
            foreach (var kv in YardService.BlockConfig.Where(b => b.Key != "R"))
                for (var b = 1; b <= kv.Value[0]; b++)
                    for (var r = 1; r <= kv.Value[1]; r++)
                    {
                        var goc = $"{kv.Key}-{b:00}-{r:00}";
                        if (daDung.Contains(goc)) continue;
                        var vt = goc + "-1";
                        var du = double.MaxValue;
                        var vp = 0;
                        foreach (var d in conLai)
                        {
                            var ma = MaCachLy(ban.K.LopIMDG, d.K.LopIMDG);
                            if (ma == null || !Muc.ContainsKey(ma)) continue;
                            var hieu = KhoangCach(vt, d.ViTri).Value - Muc[ma].CanM;
                            if (hieu < 0) vp++;
                            du = Math.Min(du, hieu);
                        }
                        ungVien.Add((vt, du == double.MaxValue ? 999 : Math.Round(du, 1), vp));
                    }
            return new
            {
                SoCont = so,
                ban.K.LopIMDG,
                ViTriHienTai = ban.ViTri,
                GoiY = ungVien.Where(u => u.ViPham == 0).OrderByDescending(u => u.DuM).ThenBy(u => u.ViTri).Take(5).Select(u => new { u.ViTri, DuAnToanM = u.DuM == 999 ? (double?)null : u.DuM }),
            };
        }
    }
}
