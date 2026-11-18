using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// TRUC LIEN KET DU LIEU (Single Timeline).
    ///
    /// VAN DE CUA PHAN MEM CANG/DEPOT NOI CHUNG: mo-dun nao cung ghi du lieu
    /// cua rieng no — cong ghi luot xe, tram can ghi phieu can, dien lanh ghi
    /// nhiet do, xuong ghi EOR — nen khi khach goi hoi "cai cont cua toi dang
    /// o dau, sao tinh tien nhu vay" thi nhan vien phai mo 6 man hinh roi
    /// ghep tay. Bao cao Logistics VN 2024 goi day la "thieu co che lien ket";
    /// phan mem depot quoc te goi la "gate and yard must share ONE timeline".
    ///
    /// LOP NAY LA CAU TRA LOI: chi mot noi duy nhat biet "moi thu lien quan
    /// den X", dung chung cho Ho so container, Ho so xe va o tim kiem toan cuc.
    /// Moi man hinh khac chi viec goi vao day — them nguon du lieu moi thi
    /// SUA MOT CHO, ca he thong sang theo.
    /// </summary>
    public static class LienKetService
    {
        /// <summary>Mot dau moc tren dong thoi gian, du de ve ra giao dien</summary>
        public class SuKien
        {
            public DateTime Luc { get; set; }
            public string Nhom { get; set; }      // Cổng / Lệnh / Cân / Sửa chữa / Lạnh / Chứng từ / Bãi
            public string TieuDe { get; set; }
            public string MoTa { get; set; }
            public string Url { get; set; }       // bam vao nhay toi ban ghi goc (null = khong co trang rieng)
            public string Icon { get; set; }      // ten icon bootstrap
            public string Mau { get; set; }       // ma mau hex cho cham tren truc
        }

        /// <summary>Mot dong ket qua cua o tim kiem toan cuc</summary>
        public class KetQuaTim
        {
            public string Nhom { get; set; }
            public string TieuDe { get; set; }
            public string MoTa { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
            public string Mau { get; set; }
        }

        // ============================================================
        // DONG THOI GIAN CUA MOT CONTAINER
        // ============================================================

        /// <summary>
        /// Gom TAT CA su kien lien quan den mot container tu 7 bang khac nhau
        /// va xep theo thoi gian giam dan. Day la thu ma truoc kia nhan vien
        /// phai mo 6 man hinh moi ghep duoc.
        /// </summary>
        public static List<SuKien> DongThoiGianContainer(AppDb db, Container c)
        {
            var ds = new List<SuKien>();
            if (c == null) return ds;
            var soCont = (c.SoCont ?? "").Trim().ToUpperInvariant();
            if (soCont.Length == 0) return ds;

            // 0. Chuyen tau nhap/xuat — chi co khi nguoi goi da Include san
            //    (EF Core khong lazy-load, nav null nghia la khong nap chu khong phai khong co)
            if (c.ChuyenNhap != null)
                ds.Add(new SuKien
                {
                    Luc = c.ChuyenNhap.ATA ?? c.ChuyenNhap.ETA, Nhom = "Tàu", Icon = "bi-water", Mau = "#0d9488",
                    TieuDe = "Tàu " + (c.ChuyenNhap.Tau != null ? c.ChuyenNhap.Tau.TenTau : "?") + " cập cảng",
                    MoTa = "Chuyến " + c.ChuyenNhap.MaChuyenHieu + " · dỡ container khỏi tàu"
                         + (c.ChuyenNhap.ATA == null ? " (kế hoạch)" : ""),
                    Url = "/Admin/VesselCalls"
                });
            if (c.ChuyenXuat != null)
                ds.Add(new SuKien
                {
                    Luc = c.ChuyenXuat.ATD ?? c.ChuyenXuat.ETD, Nhom = "Tàu", Icon = "bi-water", Mau = "#0369a1",
                    TieuDe = "Xuất lên tàu " + (c.ChuyenXuat.Tau != null ? c.ChuyenXuat.Tau.TenTau : "?"),
                    MoTa = "Chuyến " + c.ChuyenXuat.MaChuyenHieu + (c.ChuyenXuat.ATD == null ? " (kế hoạch)" : ""),
                    Url = "/Admin/VesselCalls"
                });

            // 1. Vong doi bai — lay tu chinh ban ghi container
            if (c.NgayVaoBai != null)
                ds.Add(new SuKien
                {
                    Luc = c.NgayVaoBai.Value, Nhom = "Bãi", Icon = "bi-box-arrow-in-down", Mau = "#0891b2",
                    TieuDe = "Hạ bãi", MoTa = "Vị trí " + (c.ViTriBai ?? "chưa gán") + " · bắt đầu tính lưu bãi"
                });
            if (c.NgayRaBai != null)
                ds.Add(new SuKien
                {
                    Luc = c.NgayRaBai.Value, Nhom = "Bãi", Icon = "bi-box-arrow-up", Mau = "#15803d",
                    TieuDe = "Rời bãi", MoTa = c.TrangThai
                });

            // 2. Luot xe qua cong cho chinh container nay
            foreach (var x in db.XeRaVaos.Where(x => x.SoCont == soCont)
                                         .OrderByDescending(x => x.ThoiGian).Take(40).ToList())
                ds.Add(new SuKien
                {
                    Luc = x.ThoiGian, Nhom = "Cổng", Icon = x.HuongDi == "Vào" ? "bi-box-arrow-in-right" : "bi-box-arrow-right",
                    Mau = x.HuongDi == "Vào" ? "#1c5fa8" : "#b45309",
                    TieuDe = "Xe " + x.BienSo + " " + (x.HuongDi == "Vào" ? "vào" : "ra") + " cổng " + x.Cong,
                    MoTa = (x.LoaiXe ?? "") + (x.DoTinCay != null ? " · OCR " + x.DoTinCay + "%" : "")
                         + (x.Nguon == "OFFLINE" ? " · nhập bù offline" : x.Nguon == "NGOAI-TUYEN" ? " · ghi trễ khi mất mạng" : ""),
                    Url = "/Admin/Gate/HoSoXe?bienSo=" + Uri.EscapeDataString(x.BienSo)
                });

            // 3. Lenh giao nhan
            foreach (var l in db.LenhGiaoNhans.Where(l => l.SoCont == soCont)
                                              .OrderByDescending(l => l.NgayTao).Take(20).ToList())
            {
                ds.Add(new SuKien
                {
                    Luc = l.NgayTao, Nhom = "Lệnh", Icon = "bi-qr-code", Mau = "#b45309",
                    TieuDe = "Lệnh " + l.SoLenh + " — " + l.TenLoaiLenh,
                    MoTa = "Xe " + l.BienSoXe + " · hẹn " + l.HenTuLuc.ToString("HH:mm dd/MM") + " · " + l.TrangThai,
                    Url = "/Admin/GatePass/In/" + l.MaLenh
                });
                if (l.HoanTatLuc != null)
                    ds.Add(new SuKien
                    {
                        Luc = l.HoanTatLuc.Value, Nhom = "Lệnh", Icon = "bi-check2-circle", Mau = "#15803d",
                        TieuDe = "Hoàn tất lệnh " + l.SoLenh, MoTa = "Xe " + l.BienSoXe + " đã rời cổng"
                    });
            }

            // 4. Phieu can (VGM)
            foreach (var p in db.PhieuCans.Where(p => p.SoCont == soCont)
                                          .OrderByDescending(p => p.Luc1).Take(20).ToList())
                ds.Add(new SuKien
                {
                    Luc = p.Luc2 ?? p.Luc1, Nhom = "Cân", Icon = "bi-speedometer2", Mau = "#7c3aed",
                    TieuDe = "Phiếu cân " + p.SoPhieu,
                    MoTa = p.KhoiLuongHang != null
                        ? "Khối lượng hàng " + p.KhoiLuongHang.Value.ToString("N0") + " kg (VGM)"
                        : "Mới cân lần 1: " + p.CanLan1.ToString("N0") + " kg — chờ cân lần 2",
                    Url = "/Admin/Gate/PhieuCan"
                });

            // 5. Phieu sua chua vo (EOR / M&R)
            foreach (var e in db.PhieuSuaChuas.Where(e => e.SoCont == soCont)
                                              .OrderByDescending(e => e.NgayLap).Take(20).ToList())
            {
                ds.Add(new SuKien
                {
                    Luc = e.NgayLap, Nhom = "Sửa chữa", Icon = "bi-tools", Mau = "#dc2626",
                    TieuDe = "EOR " + e.SoEOR + " — " + e.ViTriHuHong,
                    MoTa = "Mã " + e.MaHuHong + " · đề nghị " + e.TongChiPhi.ToString("N0") + "đ · " + e.TrangThai,
                    Url = "/Admin/Depot/InEor/" + e.MaEOR
                });
                if (e.NgayDuyet != null)
                    ds.Add(new SuKien
                    {
                        Luc = e.NgayDuyet.Value, Nhom = "Sửa chữa",
                        Icon = e.TrangThai == "Từ chối" ? "bi-x-circle" : "bi-patch-check",
                        Mau = e.TrangThai == "Từ chối" ? "#dc2626" : "#15803d",
                        TieuDe = "Hãng tàu " + (e.TrangThai == "Từ chối" ? "từ chối" : "duyệt") + " EOR " + e.SoEOR,
                        MoTa = e.HangTau != null ? e.HangTau.TenHangTau : ""
                    });
            }

            // 6. Canh bao nhiet do — CHI lay lan lech nguong, khong do het log vao
            //    (mot cont lanh sinh hang tram dong/ngay, do het thi troi mat cac su kien khac)
            if ((c.LoaiCont ?? "").ToUpperInvariant().Contains("RF") || c.NhietDo != null)
            {
                var dat = c.NhietDo ?? -18m;
                foreach (var t in db.NhietDoLogs.Where(t => t.MaCont == c.MaCont)
                                                .OrderByDescending(t => t.ThoiGian).Take(300).ToList()
                                                .Where(t => Math.Abs(t.NhietDo - dat) > 2m).Take(8))
                    ds.Add(new SuKien
                    {
                        Luc = t.ThoiGian, Nhom = "Lạnh", Icon = "bi-thermometer-high", Mau = "#e11d48",
                        TieuDe = "Lệch nhiệt: đo " + t.NhietDo + "°C (đặt " + dat + "°C)",
                        MoTa = "Nguồn " + (t.Nguon ?? "IoT"), Url = "/Admin/Reefer"
                    });
            }

            // 7. Chung tu da phat hanh
            foreach (var ct in db.ChungTus.Where(ct => ct.SoCont == soCont)
                                          .OrderByDescending(ct => ct.NgayLap).Take(20).ToList())
                ds.Add(new SuKien
                {
                    Luc = ct.NgayLap, Nhom = "Chứng từ", Icon = "bi-file-earmark-text", Mau = "#0d9488",
                    TieuDe = ct.TenLoai + " " + ct.SoChungTu,
                    MoTa = (ct.MoTa ?? "") + " · lập bởi " + (ct.NguoiLap ?? "hệ thống"),
                    Url = ct.LienKet
                });

            return ds.OrderByDescending(s => s.Luc).ToList();
        }

        // ============================================================
        // DONG THOI GIAN CUA MOT XE (theo bien so)
        // ============================================================

        /// <summary>
        /// Lich su cua mot dau keo: da vao ra bao nhieu luot, cho nhung cont
        /// nao, can bao nhieu lan. Bao ve/dieu do go bien so la ra het —
        /// truoc day khong co man hinh nao lam duoc viec nay.
        /// </summary>
        public static List<SuKien> DongThoiGianXe(AppDb db, string bienSo, int soNgay = 90)
        {
            var ds = new List<SuKien>();
            bienSo = (bienSo ?? "").Trim().ToUpperInvariant();
            if (bienSo.Length == 0) return ds;
            var tu = DateTime.Now.AddDays(-Math.Abs(soNgay));

            foreach (var x in db.XeRaVaos.Where(x => x.BienSo == bienSo && x.ThoiGian >= tu)
                                         .OrderByDescending(x => x.ThoiGian).Take(80).ToList())
                ds.Add(new SuKien
                {
                    Luc = x.ThoiGian, Nhom = "Cổng", Icon = x.HuongDi == "Vào" ? "bi-box-arrow-in-right" : "bi-box-arrow-right",
                    Mau = x.HuongDi == "Vào" ? "#1c5fa8" : "#b45309",
                    TieuDe = (x.HuongDi == "Vào" ? "Vào" : "Ra") + " cổng " + x.Cong
                           + (string.IsNullOrEmpty(x.SoCont) ? "" : " · cont " + x.SoCont),
                    MoTa = (x.LoaiXe ?? "") + (x.ViTriDo != null ? " · dock " + x.ViTriDo : "")
                         + (x.Nguon == "OFFLINE" ? " · nhập bù offline" : x.Nguon == "NGOAI-TUYEN" ? " · ghi trễ khi mất mạng" : ""),
                    Url = string.IsNullOrEmpty(x.SoCont) ? null : "/Admin/Containers/TraCuu?so=" + Uri.EscapeDataString(x.SoCont)
                });

            foreach (var l in db.LenhGiaoNhans.Where(l => l.BienSoXe == bienSo && l.NgayTao >= tu)
                                              .OrderByDescending(l => l.NgayTao).Take(40).ToList())
                ds.Add(new SuKien
                {
                    Luc = l.NgayTao, Nhom = "Lệnh", Icon = "bi-qr-code", Mau = "#b45309",
                    TieuDe = "Lệnh " + l.SoLenh + " — " + l.TenLoaiLenh,
                    MoTa = (l.SoCont ?? "chưa gán cont") + " · " + l.TrangThai
                         + " · tài xế " + (l.TenTaiXe ?? "?"),
                    Url = string.IsNullOrEmpty(l.SoCont) ? null : "/Admin/Containers/TraCuu?so=" + Uri.EscapeDataString(l.SoCont)
                });

            foreach (var p in db.PhieuCans.Where(p => p.BienSoXe == bienSo && p.Luc1 >= tu)
                                          .OrderByDescending(p => p.Luc1).Take(40).ToList())
                ds.Add(new SuKien
                {
                    Luc = p.Luc2 ?? p.Luc1, Nhom = "Cân", Icon = "bi-speedometer2", Mau = "#7c3aed",
                    TieuDe = "Phiếu cân " + p.SoPhieu,
                    MoTa = p.KhoiLuongHang != null ? p.KhoiLuongHang.Value.ToString("N0") + " kg hàng"
                                                   : "chờ cân lần 2",
                    Url = "/Admin/Gate/PhieuCan"
                });

            return ds.OrderByDescending(s => s.Luc).ToList();
        }

        // ============================================================
        // TIM KIEM TOAN CUC
        // ============================================================

        /// <summary>
        /// Go MOT o duy nhat, ra moi thu: so container, bien so xe, so lenh,
        /// ten khach hang, so EOR, so chung tu, ten tau. Thay cho viec phai
        /// nho "cai nay nam o man hinh nao".
        /// </summary>
        public static List<KetQuaTim> TimToanCuc(AppDb db, string tuKhoa, int gioiHan = 12)
        {
            var kq = new List<KetQuaTim>();
            var q = (tuKhoa ?? "").Trim();
            if (q.Length < 2) return kq;
            var Q = q.ToUpperInvariant();

            // 1. Container — uu tien cao nhat, la thuc the trung tam
            foreach (var c in db.Containers.Include(c => c.KhachHang)
                        .Where(c => c.SoCont.Contains(Q))
                        .OrderByDescending(c => c.MaCont).Take(5).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "Container", Icon = "bi-box-seam", Mau = "#1c5fa8",
                    TieuDe = c.SoCont,
                    MoTa = c.LoaiCont + " · " + (c.TinhTrang ?? "") + " · " + (c.TrangThai ?? "")
                         + (c.KhachHang != null ? " · " + c.KhachHang.TenCongTy : "")
                         + (c.ViTriBai != null ? " · vị trí " + c.ViTriBai : ""),
                    Url = "/Admin/Containers/HoSo/" + c.MaCont
                });

            // 2. Bien so xe — gom nhom, lay luot gan nhat lam dai dien
            foreach (var g in db.XeRaVaos.Where(x => x.BienSo.Contains(Q))
                        .GroupBy(x => x.BienSo)
                        .Select(g => new { BienSo = g.Key, Lan = g.Max(x => x.ThoiGian), So = g.Count() })
                        .OrderByDescending(g => g.Lan).Take(4).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "Xe", Icon = "bi-truck", Mau = "#0891b2",
                    TieuDe = g.BienSo,
                    MoTa = g.So + " lượt qua cổng · gần nhất " + g.Lan.ToString("HH:mm dd/MM/yyyy"),
                    Url = "/Admin/Gate/HoSoXe?bienSo=" + Uri.EscapeDataString(g.BienSo)
                });

            // 3. Lenh giao nhan
            foreach (var l in db.LenhGiaoNhans
                        .Where(l => l.SoLenh.Contains(Q) || l.BienSoXe.Contains(Q) || l.MaPin == Q)
                        .OrderByDescending(l => l.MaLenh).Take(4).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "Lệnh", Icon = "bi-qr-code", Mau = "#b45309",
                    TieuDe = l.SoLenh + " · " + l.BienSoXe,
                    MoTa = l.TenLoaiLenh + " · " + (l.SoCont ?? "chưa gán cont") + " · " + l.TrangThai,
                    Url = "/Admin/GatePass"
                });

            // 4. Khach hang
            foreach (var k in db.KhachHangs
                        .Where(k => k.TenCongTy.Contains(q) || k.MaSoThue.Contains(q))
                        .OrderBy(k => k.TenCongTy).Take(4).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "Khách hàng", Icon = "bi-buildings", Mau = "#7c3aed",
                    TieuDe = k.TenCongTy,
                    MoTa = "MST " + (k.MaSoThue ?? "—") + " · " + (k.SoDienThoai ?? ""),
                    Url = "/Admin/Customers/Form/" + k.MaKH
                });

            // 5. Phieu sua chua vo
            foreach (var e in db.PhieuSuaChuas
                        .Where(e => e.SoEOR.Contains(Q) || e.SoCont.Contains(Q))
                        .OrderByDescending(e => e.MaEOR).Take(3).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "EOR", Icon = "bi-tools", Mau = "#dc2626",
                    TieuDe = e.SoEOR + " · " + e.SoCont,
                    MoTa = e.ViTriHuHong + " · " + e.TongChiPhi.ToString("N0") + "đ · " + e.TrangThai,
                    Url = "/Admin/Depot/InEor/" + e.MaEOR
                });

            // 6. Tau
            foreach (var t in db.Taus.Where(t => t.TenTau.Contains(q) || t.SoIMO.Contains(q))
                        .OrderBy(t => t.TenTau).Take(3).ToList())
                kq.Add(new KetQuaTim
                {
                    Nhom = "Tàu", Icon = "bi-water", Mau = "#0d9488",
                    TieuDe = t.TenTau, MoTa = "IMO " + (t.SoIMO ?? "—"),
                    Url = "/Admin/Vessels"
                });

            return kq.Take(gioiHan).ToList();
        }
    }
}
