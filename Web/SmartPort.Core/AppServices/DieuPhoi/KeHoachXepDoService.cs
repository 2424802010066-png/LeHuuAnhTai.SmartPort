using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services.DieuPhoi
{
    /// <summary>
    /// KE HOACH XEP DO TAU. Hai bai toan:
    ///  1) Xep cont xuat vao o tren tau: cont nang xuong tang thap + gan tam tau; cont lanh len tang tren (co o cam dien).
    ///  2) Chia cong viec cho cac cau bo: moi cau nhan mot DOAN BAY LIEN TIEP (cau chay chung ray, khong vuot nhau) sao cho
    ///     cau nang viec nhat la nhe nhat — bai toan chia day lien tiep, giai toi uu bang quy hoach dong.
    /// Hinh hoc tau suy tu suc chua TEU va la MO HINH DON GIAN HOA, chua thay the phan mem tinh on dinh cua tau.
    /// </summary>
    public static class KeHoachXepDoService
    {
        private const double MoveMoiGioMoiCau = 28;
        private const double GioChuanBi = 1;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static object DanhSachChuyen(AppDb db)
        {
            var ds = db.ChuyenTaus.AsNoTracking().Where(c => c.TrangThai != "Đã rời bến" && c.TrangThai != "Hủy").OrderBy(c => c.ETA)
                .Select(c => new { c.MaChuyen, c.MaChuyenHieu, TenTau = c.Tau.TenTau, TenBen = c.Ben.TenBen, c.ETA, c.ETD, c.TrangThai, c.ContNhap, c.ContXuat }).ToList();
            var daLap = db.KeHoachXepDos.AsNoTracking().Select(k => k.MaChuyen).Distinct().ToList();
            return ds.Select(c => new { c.MaChuyen, c.MaChuyenHieu, c.TenTau, c.TenBen, c.ETA, c.ETD, c.TrangThai, c.ContNhap, c.ContXuat, DaLap = daLap.Contains(c.MaChuyen) });
        }

        /// <summary>Chia moves[] thanh k doan lien tiep, cuc tieu hoa doan lon nhat. Tra ve chi so bat dau moi doan.</summary>
        public static List<int> ChiaDoan(int[] moves, int k)
        {
            var n = moves.Length;
            k = Math.Max(1, Math.Min(k, n));
            var cong = new long[n + 1];
            for (var i = 0; i < n; i++) cong[i + 1] = cong[i] + moves[i];
            var f = new long[n + 1, k + 1];
            var vet = new int[n + 1, k + 1];
            for (var i = 0; i <= n; i++) for (var j = 0; j <= k; j++) f[i, j] = long.MaxValue;
            f[0, 0] = 0;
            for (var j = 1; j <= k; j++)
                for (var i = j; i <= n; i++)
                    for (var t = j - 1; t < i; t++)
                    {
                        if (f[t, j - 1] == long.MaxValue) continue;
                        var v = Math.Max(f[t, j - 1], cong[i] - cong[t]);
                        if (v < f[i, j]) { f[i, j] = v; vet[i, j] = t; }
                    }
            var dau = new List<int>();
            for (int i = n, j = k; j > 0; j--)
            {
                i = vet[i, j];
                dau.Insert(0, i);
            }
            return dau;
        }

        public static (object KetQua, string Loi) Lap(AppDb db, int maChuyen, string nguoi)
        {
            var ch = db.ChuyenTaus.AsNoTracking().Where(c => c.MaChuyen == maChuyen)
                .Select(c => new { c.MaChuyen, c.MaChuyenHieu, c.ETA, c.ETD, c.ATA, c.TrangThai, c.ContNhap, c.ContXuat, c.Tau.TenTau, c.Tau.SucChuaTEU, SoCau = (int?)c.Ben.SoCau, c.Ben.TenBen }).FirstOrDefault();
            if (ch == null) return (null, "Không tìm thấy chuyến tàu.");
            if (ch.TrangThai == "Đã rời bến" || ch.TrangThai == "Hủy") return (null, "Chuyến đã " + ch.TrangThai.ToLowerInvariant() + ", không lập kế hoạch được.");

            var teu = ch.SucChuaTEU ?? 2000;
            var soBay = Math.Max(6, Math.Min(24, teu / 200));
            var soHang = Math.Max(7, Math.Min(19, teu / 500 + 7));
            if (soHang % 2 == 0) soHang++;
            const int soTang = 6;

            var canhBao = new List<string>();
            var xuat = db.Containers.AsNoTracking().Where(c => c.MaChuyenXuat == maChuyen && c.NgayRaBai == null)
                .Select(c => new { c.SoCont, c.LoaiCont, c.TrongLuong, c.NhietDo }).ToList();

            // Thu tu o trong: tang thap truoc, roi gan giua hang, roi gan giua tau
            var giuaHang = (soHang + 1) / 2.0;
            var giuaBay = (soBay + 1) / 2.0;
            var o = (from t in Enumerable.Range(1, soTang)
                     from h in Enumerable.Range(1, soHang)
                     from b in Enumerable.Range(1, soBay)
                     orderby t, Math.Abs(h - giuaHang), Math.Abs(b - giuaBay)
                     select new { Bay = b, Hang = h, Tang = t }).ToList();
            var oLanh = o.Where(x => x.Tang >= soTang - 1).ToList();
            var dung = new HashSet<(int, int, int)>();
            var xep = new List<object>();
            double mDoc = 0, mNgang = 0, tongW = 0, tangW = 0;

            foreach (var c in xuat.OrderByDescending(c => c.TrongLuong ?? 0))
            {
                var laLanh = (c.LoaiCont ?? "").Contains("RF");
                var chon = (laLanh ? oLanh : o).FirstOrDefault(x => !dung.Contains((x.Bay, x.Hang, x.Tang)));
                if (chon == null) { canhBao.Add("Không còn ô cho container " + c.SoCont + "."); continue; }
                dung.Add((chon.Bay, chon.Hang, chon.Tang));
                var w = (double)(c.TrongLuong ?? 0);
                mDoc += w * (chon.Bay - giuaBay);
                mNgang += w * (chon.Hang - giuaHang);
                tongW += w;
                tangW += w * chon.Tang;
                xep.Add(new { c.SoCont, c.LoaiCont, TrongLuong = w, Lanh = laLanh, chon.Bay, chon.Hang, chon.Tang });
            }

            var tongMove = ch.ContNhap + ch.ContXuat;
            if (tongMove == 0) canhBao.Add("Chuyến chưa khai báo số container nhập/xuất.");
            var soCau = ch.SoCau ?? 0;
            if (soCau <= 0)
            {
                soCau = 2;
                canhBao.Add("Chuyến chưa có bến: tạm tính với 2 cẩu bờ.");
            }

            // Viec tap trung o giua tau (khoang rong nhat)
            var trongSo = Enumerable.Range(0, soBay).Select(i => Math.Sin(Math.PI * (i + 0.5) / soBay) + 0.3).ToArray();
            var tongTs = trongSo.Sum();
            var moves = trongSo.Select(w => (int)Math.Round(tongMove * w / tongTs)).ToArray();
            var lech = tongMove - moves.Sum();
            moves[soBay / 2] = Math.Max(0, moves[soBay / 2] + lech);

            var dau = ChiaDoan(moves, soCau);
            var batDau = ch.ATA ?? ch.ETA;
            if (batDau < DateTime.Now && ch.ATA == null) batDau = DateTime.Now;
            var cau = new List<object>();
            double gioMax = 0;
            for (var i = 0; i < dau.Count; i++)
            {
                var tu = dau[i];
                var den = (i + 1 < dau.Count ? dau[i + 1] : soBay) - 1;
                var mv = moves.Skip(tu).Take(den - tu + 1).Sum();
                var gio = Math.Round(mv / MoveMoiGioMoiCau, 1);
                gioMax = Math.Max(gioMax, gio);
                cau.Add(new { Cau = "QC" + (i + 1), TuBay = tu + 1, DenBay = den + 1, Move = mv, Gio = gio });
            }
            var tongGio = Math.Round(gioMax + GioChuanBi, 1);
            var xongLuc = batDau.AddHours(tongGio);
            var kip = xongLuc <= ch.ETD;
            if (!kip) canhBao.Add($"Dự kiến xong lúc {xongLuc:HH:mm dd/MM}, trễ hơn ETD {ch.ETD:HH:mm dd/MM}: cần thêm cẩu hoặc dời ETD.");

            var kq = new
            {
                ch.MaChuyen, ch.MaChuyenHieu, ch.TenTau, ch.TenBen,
                HinhHoc = new { SoBay = soBay, SoHang = soHang, SoTang = soTang },
                Xep = xep,
                CanDoi = new
                {
                    TongTan = Math.Round(tongW, 1),
                    LechDocBay = tongW > 0 ? Math.Round(mDoc / tongW, 2) : 0,
                    LechNgangHang = tongW > 0 ? Math.Round(mNgang / tongW, 2) : 0,
                    TangTrongTam = tongW > 0 ? Math.Round(tangW / tongW, 2) : 0,
                },
                MoveTheoBay = moves,
                Cau = cau,
                TongMove = tongMove,
                MoveMoiGioMoiCau,
                GioDuKien = tongGio,
                BatDau = batDau,
                XongLuc = xongLuc,
                ch.ETD,
                KipEtd = kip,
                CanhBao = canhBao,
                LapLuc = DateTime.Now,
                NguoiLap = nguoi,
            };
            db.KeHoachXepDos.Add(new KeHoachXepDo { MaChuyen = maChuyen, LapLuc = DateTime.Now, NguoiLap = nguoi, SoMove = tongMove, GioDuKien = tongGio, NoiDungJson = JsonSerializer.Serialize(kq, Json) });
            db.SaveChanges();
            return (kq, null);
        }

        /// <summary>Ban ke hoach moi nhat da luu (chuoi JSON nguyen ban), null neu chua lap.</summary>
        public static string BanMoiNhat(AppDb db, int maChuyen) =>
            db.KeHoachXepDos.AsNoTracking().Where(k => k.MaChuyen == maChuyen).OrderByDescending(k => k.MaKeHoach).Select(k => k.NoiDungJson).FirstOrDefault();
    }
}
