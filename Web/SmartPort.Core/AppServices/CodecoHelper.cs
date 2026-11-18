using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Contracts.TichHop;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// LAP BAN TIN CODECO tu cac luot xe qua cong chua bao cao — dung chung cho man hinh EDI, agent tu dong, tro ly va
    /// lenh "YeuCauSinhCodeco" tu dich vu khac (truoc day 3 noi chep cung mot doan code).
    /// Microservices: ban tin EDI thuoc dich vu Tich hop, luot xe thuoc dich vu Cong -> danh dau "da bao cao" (MaBT)
    /// bang su kien LuotXeDaVaoBanTinEdi thay vi ghi thang vao bang cua Cong.
    /// </summary>
    public static class CodecoHelper
    {
        public static BanTinEDI Lap(AppDb db, HangTau hang, List<XeRaVao> luot, string maDepot, string nguoiTao, string ghiChu)
        {
            var soConts = luot.Select(x => x.SoCont).Distinct().ToList();
            var conts = db.Containers.AsNoTracking().Where(c => soConts.Contains(c.SoCont))
                .ToList()
                .GroupBy(c => c.SoCont, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var maLenhs = luot.Where(x => x.MaLenh != null).Select(x => x.MaLenh.Value).Distinct().ToList();
            var lenhs = db.LenhGiaoNhans.AsNoTracking().Where(l => maLenhs.Contains(l.MaLenh)).ToDictionary(l => l.MaLenh, l => l.SoLenh);
            var can = db.PhieuCans.AsNoTracking().Where(p => soConts.Contains(p.SoCont) && p.CanLan2 != null).ToList();

            var dong = luot.Select(x =>
            {
                conts.TryGetValue(x.SoCont, out var c);
                var pc = can.Where(p => string.Equals(p.SoCont, x.SoCont, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(p => p.Luc1).FirstOrDefault();
                return new EdiService.DongCodeco
                {
                    SoCont = x.SoCont,
                    LoaiCont = c != null ? c.LoaiCont : "40HC",
                    TinhTrang = c != null ? c.TinhTrang : "Full",
                    HuongDi = x.HuongDi,
                    ThoiGian = x.ThoiGian,
                    BienSoXe = x.BienSo,
                    SoLenh = x.MaLenh != null && lenhs.ContainsKey(x.MaLenh.Value) ? lenhs[x.MaLenh.Value] : null,
                    TrongLuongKg = pc != null ? pc.KhoiLuongHang : null,
                    ViTriBai = c != null ? c.ViTriBai : null
                };
            }).ToList();

            var maHang = !string.IsNullOrWhiteSpace(hang.MaSCAC) ? hang.MaSCAC : hang.TenHangTau;
            var kq = EdiService.SinhCodeco(maDepot, "SMARTPORT DEPOT", maHang, dong);
            var bt = new BanTinEDI
            {
                LoaiBT = "CODECO",
                Huong = "Gửi",
                MaHangTau = hang.MaHangTau,
                SoThamChieu = kq.SoThamChieu,
                SoChungTu = kq.SoChungTu,
                TuNgay = luot.Min(x => x.ThoiGian),
                DenNgay = luot.Max(x => x.ThoiGian),
                SoLuongCont = kq.SoLuongCont,
                NoiDung = kq.NoiDung,
                TrangThai = "Chờ gửi",
                NgayTao = DateTime.Now,
                NguoiTao = nguoiTao != null && nguoiTao.Length > 50 ? nguoiTao.Substring(0, 50) : nguoiTao,
                GhiChu = (ghiChu ?? (kq.SoDoan + " đoạn EDIFACT · gom " + luot.Count + " lượt qua cổng"))
            };

            // Luu ban tin va danh dau luot trong CUNG giao dich: khong bao gio co ban tin da luu ma cac luot cua no
            // van "chua bao cao" (se bi gom lai lan sau -> bao trung)
            var gdRieng = db.Database.CurrentTransaction == null ? db.Database.BeginTransaction() : null;
            try
            {
                db.BanTinEDIs.Add(bt);
                db.SaveChanges();
                GanLuot(db, luot, bt.MaBT, bt.SoChungTu);
                db.SaveChanges();
                gdRieng?.Commit();
            }
            finally
            {
                gdRieng?.Dispose();
            }

            ChungTuHelper.Ghi(db, "Khac", bt.SoChungTu, null, null, null,
                "Bản tin CODECO gửi " + hang.TenHangTau + " — " + kq.SoLuongCont + " container",
                "/Admin/Edi/Xem/" + bt.MaBT, nguoiTao);
            return bt;
        }

        /// <summary>Danh dau cac luot thuoc ban tin (maBT null = go ra khi huy ban tin). Khong goi SaveChanges.</summary>
        public static void GanLuot(AppDb db, IEnumerable<XeRaVao> luot, int? maBT, string soChungTu = null)
        {
            var ds = luot.ToList();
            if (ds.Count == 0) return;
            if (SoHuuDuLieu.DuocGhi("XeRaVao"))
            {
                foreach (var x in ds) x.MaBT = maBT;
                return;
            }
            HopThuDiTinh.Them(db, new LuotXeDaVaoBanTinEdi { MaBT = maBT, SoChungTu = soChungTu, DanhSachMaXV = ds.Select(x => x.MaXV).ToArray() });
        }

        /// <summary>
        /// Agent / tro ly (dich vu Van hanh) muon sinh CODECO: duoc ghi ban tin thi lap ngay (ban gop), khong thi gui lenh
        /// cho dich vu Tich hop. Tra ban tin vua lap, hoac null neu da gui lenh (Tich hop se lap trong vai giay).
        /// </summary>
        public static BanTinEDI LapHoacYeuCau(AppDb db, HangTau hang, List<XeRaVao> luot, string maDepot, string nguoiTao, string ghiChu)
        {
            if (SoHuuDuLieu.DuocGhi("BanTinEDI")) return Lap(db, hang, luot, maDepot, nguoiTao, ghiChu);
            HopThuDiTinh.Them(db, new YeuCauSinhCodeco
            {
                MaHangTau = hang.MaHangTau,
                DanhSachMaXV = luot.Select(x => x.MaXV).ToArray(),
                NguoiYeuCau = nguoiTao,
                GhiChu = ghiChu
            });
            db.SaveChanges();
            return null;
        }
    }
}
