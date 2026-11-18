using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== AI CONTAINER LANH: CANH BAO SOM + PHAN LOAI SU CO =====================
    /// VAN DE THUC TE: bao dong theo "lan do cuoi lech ≥ 3°C" gay 2 loi:
    ///   (1) BAO DONG GIA khi mo cua kiem hang / xa da — nhiet tang vot roi tu ve, tho dien lanh chay oan;
    ///   (2) BAO DONG MUON khi may nen hong dan — nhiet tang tu tu 0,3–0,5°C/gio, luc cham nguong thi hang da hong.
    /// GIAI PHAP (thong ke ben vung, chiu duoc diem nhieu):
    ///   · Muc hien tai = TRUNG VI 3 lan do cuoi (mot lan vot don le khong keo lech).
    ///   · Xu huong = do doc THEIL–SEN 6 gio gan nhat (°C/gio) + ty le cap diem tang (tang deu hay dao dong).
    ///   · Vot tam thoi = diem lech ≥ nguong nhung hai diem ke ben binh thuong.
    ///   · Du bao gio cham nguong canh bao / nguy cap = (nguong − muc hien tai) / do doc.
    ///   · Mat tin hieu: lan do cuoi qua 90 phut; cam bien ket: 6 gio lien khong doi 0,05°C.
    /// So sanh voi cach cu tren 24 gio gan nhat: dem so bao dong gia duoc loc bo.
    /// </summary>
    public static class AiContLanhService
    {
        public const double NGUONG_CANH_BAO = 3.0;
        public const double NGUONG_NGUY_CAP = 6.0;
        private const double DOC_AM_DAN = 0.25;          // °C/gio
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Mot diem do: thoi diem + do lech so voi nhiet dat (°C, duong = am hon)</summary>
        public readonly struct DiemDo
        {
            public DiemDo(DateTime luc, double lech)
            {
                Luc = luc;
                Lech = lech;
            }

            public DateTime Luc { get; }
            public double Lech { get; }
        }

        public sealed class PhanTich
        {
            /// <summary>on-dinh | vot-tam-thoi | am-dan | lech-keo-dai | nguy-cap | mat-tin-hieu | cam-bien-ket | chua-du-lieu</summary>
            public string TrangThai { get; set; }
            public string TenTrangThai { get; set; }
            public int DiemRuiRo { get; set; }
            public double? MucHienTai { get; set; }
            public double? LanDoCuoi { get; set; }
            public double? DoDocMoiGio { get; set; }
            public double? TyLeTang { get; set; }
            public double? GioDenCanhBao { get; set; }
            public double? GioDenNguyCap { get; set; }
            public int PhutTuLanDoCuoi { get; set; }
            public int SoVotTamThoi24h { get; set; }
            public int BaoDongCachCu24h { get; set; }
            public int BaoDongAi24h { get; set; }
            public string KhuyenNghi { get; set; }
        }

        /// <summary>Phan tich chuoi do (tang dan theo thoi gian). Ham thuan — kiem thu duoc.</summary>
        public static PhanTich PhanTichChuoi(IReadOnlyList<DiemDo> chuoi, DateTime now)
        {
            var kq = new PhanTich();
            if (chuoi == null || chuoi.Count == 0)
            {
                kq.TrangThai = "chua-du-lieu";
                kq.TenTrangThai = "Chưa có số đo";
                kq.DiemRuiRo = 50;
                kq.KhuyenNghi = "Chưa nhận số đo nào — kiểm tra kết nối cảm biến hoặc đo tay.";
                return kq;
            }
            var ds = chuoi.OrderBy(d => d.Luc).ToList();
            var cuoi = ds[ds.Count - 1];
            kq.LanDoCuoi = Math.Round(cuoi.Lech, 1);
            kq.PhutTuLanDoCuoi = (int)Math.Max(0, (now - cuoi.Luc).TotalMinutes);
            kq.MucHienTai = Math.Round(ThuatToanAi.TrungVi(ds.Skip(Math.Max(0, ds.Count - 3)).Select(d => d.Lech)), 2);

            // 24h: dem vot tam thoi + bao dong theo cach cu / theo AI
            var tu24 = now.AddHours(-24);
            var ds24 = ds.Where(d => d.Luc >= tu24).ToList();
            for (var i = 0; i < ds24.Count; i++)
            {
                if (ds24[i].Lech >= NGUONG_CANH_BAO) kq.BaoDongCachCu24h++;
                var truocOn = i == 0 || ds24[i - 1].Lech < NGUONG_CANH_BAO;
                var sauOn = i == ds24.Count - 1 ? false : ds24[i + 1].Lech < NGUONG_CANH_BAO;
                if (ds24[i].Lech >= NGUONG_CANH_BAO && truocOn && sauOn) kq.SoVotTamThoi24h++;
                // AI chi bao khi trung vi 3 diem gan nhat (tinh den diem i) vuot nguong -> lech that su
                var cuaSo = ds24.Skip(Math.Max(0, i - 2)).Take(Math.Min(3, i + 1)).Select(d => d.Lech);
                if (ThuatToanAi.TrungVi(cuaSo) >= NGUONG_CANH_BAO) kq.BaoDongAi24h++;
            }

            // Xu huong 6 gio (Theil–Sen)
            var tu6 = cuoi.Luc.AddHours(-6);
            var ds6 = ds.Where(d => d.Luc >= tu6).ToList();
            var dungYen = false;
            if (ds6.Count >= 5)
            {
                var x = ds6.Select(d => (d.Luc - tu6).TotalHours).ToList();
                var y = ds6.Select(d => d.Lech).ToList();
                var (doc, _) = ThuatToanAi.TheilSen(x, y);
                kq.DoDocMoiGio = Math.Round(doc, 3);
                kq.TyLeTang = Math.Round(ThuatToanAi.TyLeCapTang(x, y), 2);
                dungYen = ThuatToanAi.MAD(y) < 0.025 && y.Max() - y.Min() < 0.05 && ds6.Count >= 8;
            }

            var muc = kq.MucHienTai.Value;
            if (kq.DoDocMoiGio.HasValue && kq.DoDocMoiGio > 0.02 && muc < NGUONG_CANH_BAO)
            {
                kq.GioDenCanhBao = Math.Round((NGUONG_CANH_BAO - muc) / kq.DoDocMoiGio.Value, 1);
                kq.GioDenNguyCap = Math.Round((NGUONG_NGUY_CAP - muc) / kq.DoDocMoiGio.Value, 1);
            }

            // Thu tu uu tien: mat tin hieu -> cam bien ket -> nguy cap -> lech keo dai -> am dan -> vot -> on dinh
            if (kq.PhutTuLanDoCuoi > 90)
            {
                kq.TrangThai = "mat-tin-hieu";
                kq.TenTrangThai = "Mất tín hiệu cảm biến";
                kq.DiemRuiRo = Math.Min(90, 60 + kq.PhutTuLanDoCuoi / 30);
                kq.KhuyenNghi = "Không nhận số đo " + (kq.PhutTuLanDoCuoi / 60.0).ToString("0.#", Vi)
                                + " giờ — kiểm tra nguồn điện ổ cắm / bộ phát IoT, đo tay ngay.";
            }
            else if (dungYen)
            {
                kq.TrangThai = "cam-bien-ket";
                kq.TenTrangThai = "Nghi cảm biến kẹt";
                kq.DiemRuiRo = 55;
                kq.KhuyenNghi = "Số đo không đổi suốt 6 giờ — cảm biến có thể treo; cử thợ đo tay đối chiếu.";
            }
            else if (muc >= NGUONG_NGUY_CAP)
            {
                kq.TrangThai = "nguy-cap";
                kq.TenTrangThai = "Nguy cấp — mất lạnh";
                kq.DiemRuiRo = 100;
                kq.KhuyenNghi = "Lệch ≥ " + NGUONG_NGUY_CAP + "°C kéo dài — kiểm tra máy nén / nguồn điện ngay, báo chủ hàng.";
            }
            else if (muc >= NGUONG_CANH_BAO)
            {
                kq.TrangThai = "lech-keo-dai";
                kq.TenTrangThai = "Lệch kéo dài";
                kq.DiemRuiRo = 80;
                kq.KhuyenNghi = "Nhiều lần đo liên tiếp lệch ≥ " + NGUONG_CANH_BAO + "°C (không phải mở cửa thoáng qua) — cử thợ điện lạnh.";
            }
            else if (kq.DoDocMoiGio >= DOC_AM_DAN && kq.TyLeTang >= 0.65)
            {
                kq.TrangThai = "am-dan";
                kq.TenTrangThai = "Đang ấm dần";
                var gio = kq.GioDenCanhBao ?? 99;
                kq.DiemRuiRo = gio <= 3 ? 75 : gio <= 8 ? 62 : 48;
                kq.KhuyenNghi = "Nhiệt tăng đều " + kq.DoDocMoiGio.Value.ToString("0.00", Vi) + "°C/giờ — dự báo chạm ngưỡng cảnh báo sau ~"
                                + gio.ToString("0.#", Vi) + " giờ. Kiểm tra máy nén, cửa gió, dàn ngưng TRƯỚC khi hàng hỏng.";
            }
            else if (cuoi.Lech >= NGUONG_CANH_BAO)
            {
                kq.TrangThai = "vot-tam-thoi";
                kq.TenTrangThai = "Vọt tạm thời";
                kq.DiemRuiRo = 25;
                kq.KhuyenNghi = "Lần đo cuối vọt lên nhưng mức nền vẫn ổn — nhiều khả năng mở cửa kiểm hàng/xả đá; theo dõi lần đo kế tiếp, chưa cần điều thợ.";
            }
            else
            {
                kq.TrangThai = "on-dinh";
                kq.TenTrangThai = "Ổn định";
                kq.DiemRuiRo = Math.Min(20, 5 + kq.SoVotTamThoi24h * 3);
                kq.KhuyenNghi = kq.SoVotTamThoi24h > 0
                    ? "Ổn định; có " + kq.SoVotTamThoi24h + " lần vọt tạm thời trong 24 giờ (mở cửa/xả đá) — đã lọc, không báo động."
                    : "Ổn định quanh nhiệt độ cài đặt.";
            }
            return kq;
        }

        // =====================================================================
        // DOC CSDL
        // =====================================================================

        public sealed class ContLanhAi
        {
            public int MaCont { get; set; }
            public string SoCont { get; set; }
            public string LoaiCont { get; set; }
            public string ViTriBai { get; set; }
            public string KhachHang { get; set; }
            public string HangHoa { get; set; }
            public double NhietDoCaiDat { get; set; }
            public PhanTich AI { get; set; }
            public List<DiemNhiet> Diem24Gio { get; set; } = new List<DiemNhiet>();
            public bool DienTap { get; set; }
        }

        public sealed class DiemNhiet
        {
            public DateTime Luc { get; set; }
            public double NhietDo { get; set; }
        }

        public sealed class KetQuaContLanhAi
        {
            public DateTime LucTinh { get; set; }
            public int SoCont { get; set; }
            public int SoCanXuLy { get; set; }
            public int SoAmDan { get; set; }
            public int BaoDongCachCu24h { get; set; }
            public int BaoDongAi24h { get; set; }
            public int BaoDongGiaLocBo { get; set; }
            public bool DienTap { get; set; }
            public string ThongDiep { get; set; }
            public List<ContLanhAi> Cont { get; set; } = new List<ContLanhAi>();
        }

        /// <summary>
        /// Phan tich moi container lanh dang trong bai. dienTap = true: mo phong may nen hong dan tren container
        /// dau tien (CHI TINH TOAN, khong ghi CSDL) de dien tap cho ca truc.
        /// </summary>
        public static KetQuaContLanhAi PhanTichTatCa(AppDb db, bool dienTap = false, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            var conts = db.Containers.AsNoTracking().Include(c => c.KhachHang)
                .Where(c => c.NhietDo != null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .OrderBy(c => c.ViTriBai).ThenBy(c => c.SoCont)
                .ToList();
            var ids = conts.Select(c => c.MaCont).ToList();
            var tu = now.AddHours(-30);
            var logs = ids.Count == 0
                ? new List<NhietDoLog>()
                : db.NhietDoLogs.AsNoTracking().Where(l => ids.Contains(l.MaCont) && l.ThoiGian >= tu && l.ThoiGian <= now)
                    .OrderBy(l => l.ThoiGian).ThenBy(l => l.MaLog).ToList();
            var theoCont = logs.GroupBy(l => l.MaCont).ToDictionary(g => g.Key, g => g.ToList());

            // Container khong co so do nao trong 30 gio: lay LAN DO CUOI truoc do (neu co) de phan biet
            // "mat tin hieu" (tung gui so do, nay im lang) voi "chua co du lieu" (chua gan cam bien)
            var imLang = ids.Where(id => !theoCont.ContainsKey(id)).ToList();
            if (imLang.Count > 0)
            {
                var lanCuoi = db.NhietDoLogs.AsNoTracking()
                    .Where(l => imLang.Contains(l.MaCont) && l.ThoiGian < tu)
                    .GroupBy(l => l.MaCont)
                    .Select(g => new { MaCont = g.Key, Luc = g.Max(l => l.ThoiGian) })
                    .ToList();
                foreach (var lc in lanCuoi)
                {
                    var log = db.NhietDoLogs.AsNoTracking().Where(l => l.MaCont == lc.MaCont && l.ThoiGian == lc.Luc)
                        .OrderByDescending(l => l.MaLog).FirstOrDefault();
                    if (log != null) theoCont[lc.MaCont] = new List<NhietDoLog> { log };
                }
            }

            var kq = new KetQuaContLanhAi { LucTinh = now, DienTap = dienTap };

            // Dien tap: chon container YEN ON nhat (muc lech 3 lan do cuoi thap nhat, du so do 6 gio) — con nhieu khoang
            // truoc nguong de ca truc thay AI bao SOM ("dang am dan") chu khong phai bao khi hang da lech
            int? maDienTap = null;
            if (dienTap)
                maDienTap = conts
                    .Select(c => new { c.MaCont, Dat = (double)c.NhietDo.Value, Ds = theoCont.TryGetValue(c.MaCont, out var l) ? l : null })
                    .Where(x => x.Ds != null && x.Ds.Count(l => l.ThoiGian >= now.AddHours(-6)) >= 8)
                    .OrderBy(x => ThuatToanAi.TrungVi(x.Ds.Skip(Math.Max(0, x.Ds.Count - 3)).Select(l => (double)l.NhietDo - x.Dat)))
                    .ThenBy(x => x.MaCont)
                    .Select(x => (int?)x.MaCont)
                    .FirstOrDefault();

            foreach (var c in conts)
            {
                var dat = (double)c.NhietDo.Value;
                theoCont.TryGetValue(c.MaCont, out var ds);
                var chuoi = (ds ?? new List<NhietDoLog>()).Select(l => new DiemDo(l.ThoiGian, (double)l.NhietDo - dat)).ToList();
                var laDienTap = c.MaCont == maDienTap;
                if (laDienTap)
                {
                    // May nen yeu dan: 6 gio cuoi nhiet tang deu (co nhieu nho) cong vao so do that. Do doc 0,3–0,4°C/gio,
                    // chon de muc cuoi con ~0,8°C duoi nguong canh bao -> dung thoi diem AI phai bao SOM
                    var moc = chuoi[chuoi.Count - 1].Luc.AddHours(-6);
                    var nen = ThuatToanAi.TrungVi(chuoi.Where(d => d.Luc <= moc).Select(d => d.Lech).DefaultIfEmpty(0));
                    var doc = Math.Clamp((NGUONG_CANH_BAO - 0.8 - nen) / 6, 0.3, 0.4);
                    chuoi = chuoi.Select((d, i) => d.Luc <= moc ? d
                        : new DiemDo(d.Luc, d.Lech + (d.Luc - moc).TotalHours * doc + ((i % 3) - 1) * 0.05)).ToList();
                }
                var ai = PhanTichChuoi(chuoi, now);
                kq.Cont.Add(new ContLanhAi
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    ViTriBai = c.ViTriBai,
                    KhachHang = c.KhachHang?.TenCongTy,
                    HangHoa = c.HangHoa,
                    NhietDoCaiDat = dat,
                    AI = ai,
                    DienTap = laDienTap,
                    Diem24Gio = chuoi.Where(d => d.Luc >= now.AddHours(-24))
                        .Select(d => new DiemNhiet { Luc = d.Luc, NhietDo = Math.Round(d.Lech + dat, 2) }).ToList()
                });
            }

            kq.Cont = kq.Cont.OrderByDescending(c => c.AI.DiemRuiRo).ThenBy(c => c.SoCont).ToList();
            kq.SoCont = kq.Cont.Count;
            kq.SoCanXuLy = kq.Cont.Count(c => c.AI.DiemRuiRo >= 60);
            kq.SoAmDan = kq.Cont.Count(c => c.AI.TrangThai == "am-dan");
            kq.BaoDongCachCu24h = kq.Cont.Sum(c => c.AI.BaoDongCachCu24h);
            kq.BaoDongAi24h = kq.Cont.Sum(c => c.AI.BaoDongAi24h);
            kq.BaoDongGiaLocBo = Math.Max(0, kq.BaoDongCachCu24h - kq.BaoDongAi24h);
            kq.ThongDiep = kq.SoCont == 0
                ? "Không có container lạnh nào trong bãi."
                : kq.SoCont + " container lạnh · " + kq.SoCanXuLy + " cần xử lý · trong 24 giờ cách báo động cũ phát "
                  + kq.BaoDongCachCu24h + " lần, AI chỉ giữ " + kq.BaoDongAi24h + " (lọc " + kq.BaoDongGiaLocBo + " báo động giả do mở cửa/xả đá)."
                  + (!dienTap ? ""
                     : maDienTap != null ? " ĐANG DIỄN TẬP: một container được mô phỏng máy nén yếu dần (không ghi dữ liệu)."
                     : " Chưa container nào đủ số đo 6 giờ để diễn tập.");
            return kq;
        }
    }
}
