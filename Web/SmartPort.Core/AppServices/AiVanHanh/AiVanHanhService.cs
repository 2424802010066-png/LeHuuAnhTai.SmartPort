using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// TRUNG TAM AI VAN HANH: gom ket qua 4 mo-dun (bai, cong, container lanh, chong gian lan) thanh
    /// mot bang dieu hanh + day canh bao AI vao "Canh bao van hanh" cua trang Tong quan.
    /// </summary>
    public static class AiVanHanhService
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public sealed class TheAi
        {
            public string Ma { get; set; }
            public string Ten { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
            public string ThuatToan { get; set; }
            /// <summary>ok | warn | danger</summary>
            public string Muc { get; set; }
            public string SoLon { get; set; }
            public string NhanSoLon { get; set; }
            public List<string> Dong { get; set; } = new List<string>();
            public string Loi { get; set; }
        }

        public sealed class KetQuaTongQuanAi
        {
            public DateTime LucTinh { get; set; }
            public List<TheAi> The { get; set; } = new List<TheAi>();
        }

        public static KetQuaTongQuanAi TongQuan(AppDb db)
        {
            var kq = new KetQuaTongQuanAi { LucTinh = DateTime.Now };

            // ---- Bai ----
            var bai = new TheAi { Ma = "bai", Ten = "Dự báo rời bãi · giảm đảo chuyển", Url = "/admin/ai/bai", Icon = "bi-boxes",
                                  ThuatToan = "k-NN ↔ trung vị, tự chọn bằng kiểm định chéo · đảo trước tham lam" };
            try
            {
                var db1 = AiBaiService.DuBaoRoiBai(db);
                var dao = AiBaiService.KeHoachDaoChuyen(db, duBao: db1);
                bai.SoLon = db1.SoRoiTrong72h.ToString(Vi);
                bai.NhanSoLon = "container dự kiến rời bãi trong 72 giờ";
                bai.Dong.Add(db1.SoContTrongBai + " container trong bãi · " + db1.SoQuaDuKien + " quá thời gian dự kiến");
                bai.Dong.Add("Doanh thu lưu bãi dự kiến khi rời: " + db1.TongPhiDuKien.ToString("N0", Vi) + " đ");
                bai.Dong.Add("Đảo chuyển: " + dao.DaoChuyenTruoc + " → " + dao.DaoChuyenSau + " lần nếu làm " + dao.NuocDi.Count + " nước đảo trước");
                bai.Dong.Add("Mô hình đang dùng: " + db1.MoHinhDangDung
                             + (db1.MaeMoHinh.HasValue
                                 ? " (kiểm định chéo: k-NN sai " + db1.MaeMoHinh.Value.ToString("0.0", Vi) + " · thống kê sai "
                                   + db1.MaeCoSo.Value.ToString("0.0", Vi) + " ngày)"
                                 : " (chưa đủ dữ liệu kiểm định)"));
                bai.Muc = db1.SoQuaDuKien > 0 || dao.DaoChuyenTruoc > dao.DaoChuyenSau ? "warn" : "ok";
            }
            catch (Exception ex)
            {
                bai.Muc = "danger";
                bai.Loi = ex.Message;
            }
            kq.The.Add(bai);

            // ---- Cong ----
            var cong = new TheAi { Ma = "cong", Ten = "Dự báo ùn tắc cổng", Url = "/admin/ai/cong", Icon = "bi-stoplights",
                                   ThuatToan = "Mô hình thứ × giờ (Bayes) + lệnh hẹn · hàng đợi M/M/c" };
            try
            {
                var c1 = AiCongService.DuBao(db, soNgay: 2);
                var h24 = c1.Gio.Take(24).ToList();
                var dinh = h24.OrderByDescending(o => o.SoXeDuKien).FirstOrDefault();
                cong.SoLon = h24.Sum(o => o.SoXeDuKien).ToString("0", Vi);
                cong.NhanSoLon = "lượt xe dự kiến trong 24 giờ tới";
                if (dinh != null)
                    cong.Dong.Add("Cao điểm " + dinh.Luc.ToString("HH:00 dd/MM", Vi) + ": ~" + dinh.SoXeDuKien.ToString("0.#", Vi)
                                  + " xe/giờ → mở " + dinh.SoLanCan + " làn, chờ ~" + dinh.PhutChoDuKien.ToString("0.#", Vi) + " phút");
                var vang = c1.KhungGioNenHen.FirstOrDefault();
                if (vang != null) cong.Dong.Add("Khung nên hẹn tài xế: " + vang.Tu.ToString("HH:mm", Vi) + "–" + vang.Den.ToString("HH:mm dd/MM", Vi));
                if (c1.QuayVongTrungViPhut.HasValue)
                    cong.Dong.Add("Thời gian quay vòng xe (trung vị): " + c1.QuayVongTrungViPhut.Value.ToString("0", Vi) + " phút");
                cong.Muc = h24.Any(o => o.MucDo == "Ùn tắc") ? "danger" : h24.Any(o => o.SoLanCan >= 2) ? "warn" : "ok";
            }
            catch (Exception ex)
            {
                cong.Muc = "danger";
                cong.Loi = ex.Message;
            }
            kq.The.Add(cong);

            // ---- Container lanh ----
            var lanh = new TheAi { Ma = "cont-lanh", Ten = "Cảnh báo sớm container lạnh", Url = "/admin/ai/cont-lanh", Icon = "bi-thermometer-snow",
                                   ThuatToan = "Hồi quy Theil–Sen + trung vị trượt (thống kê bền vững)" };
            try
            {
                var l1 = AiContLanhService.PhanTichTatCa(db);
                lanh.SoLon = l1.SoCanXuLy.ToString(Vi);
                lanh.NhanSoLon = "container lạnh cần xử lý";
                lanh.Dong.Add(l1.SoCont + " container lạnh đang giám sát · " + l1.SoAmDan + " đang ấm dần");
                lanh.Dong.Add("24 giờ: cách cũ " + l1.BaoDongCachCu24h + " báo động, AI giữ " + l1.BaoDongAi24h + " (lọc " + l1.BaoDongGiaLocBo + " báo động giả)");
                lanh.Muc = l1.Cont.Any(c => c.AI.DiemRuiRo >= 80) ? "danger" : l1.SoCanXuLy > 0 ? "warn" : "ok";
            }
            catch (Exception ex)
            {
                lanh.Muc = "danger";
                lanh.Loi = ex.Message;
            }
            kq.The.Add(lanh);

            // ---- Chong gian lan ----
            var gl = new TheAi { Ma = "gian-lan", Ten = "Phát hiện lượt xe bất thường", Url = "/admin/ai/gian-lan", Icon = "bi-incognito",
                                 ThuatToan = "Isolation Forest (học không giám sát)" };
            try
            {
                var g1 = AiGianLanService.PhatHien(db);
                gl.SoLon = g1.SoNghiVan.ToString(Vi);
                gl.NhanSoLon = "lượt xe cần hậu kiểm (" + g1.SoNgay + " ngày)";
                gl.Dong.Add(g1.SoLuotPhanTich + " lượt đã phân tích · " + g1.SoRaKhongVao + " lượt ra không có lượt vào");
                var dau = g1.Luot.FirstOrDefault(l => l.NghiVan);
                if (dau != null)
                    gl.Dong.Add("Cao nhất: " + dau.BienSo + " " + dau.ThoiGian.ToString("dd/MM HH:mm", Vi) + " — " + dau.LyDo.FirstOrDefault());
                gl.Muc = g1.SoNghiVan > 0 ? "warn" : "ok";
            }
            catch (Exception ex)
            {
                gl.Muc = "danger";
                gl.Loi = ex.Message;
            }
            kq.The.Add(gl);
            return kq;
        }

        /// <summary>
        /// Canh bao AI cho trang Tong quan — CHI nhung viec chac chan can lam (khong day "mat tin hieu" cua
        /// cam bien mo phong de tranh bao dong gia khi chua gan thiet bi that).
        /// </summary>
        public static List<SmartAlert> CanhBao(AppDb db)
        {
            var ds = new List<SmartAlert>();
            try
            {
                var l1 = AiContLanhService.PhanTichTatCa(db);
                var nong = l1.Cont.Where(c => c.AI.TrangThai == "nguy-cap" || c.AI.TrangThai == "lech-keo-dai" || c.AI.TrangThai == "am-dan").ToList();
                foreach (var c in nong.Take(3))
                    ds.Add(new SmartAlert
                    {
                        Muc = c.AI.DiemRuiRo >= 80 ? "danger" : "warn",
                        Icon = "bi-thermometer-high",
                        Msg = "AI container lạnh: " + c.SoCont + " — " + c.AI.TenTrangThai.ToLower(Vi)
                              + (c.AI.GioDenCanhBao.HasValue ? ", dự báo chạm ngưỡng sau ~" + c.AI.GioDenCanhBao.Value.ToString("0.#", Vi) + " giờ" : "")
                              + ".",
                        Url = "/admin/ai/cont-lanh"
                    });
            }
            catch
            {
                /* canh bao AI khong duoc lam hong trang tong quan */
            }

            try
            {
                var c1 = AiCongService.DuBao(db, soNgay: 1);
                var dinh = c1.Gio.Take(12).Where(o => o.SoLanCan >= 2).OrderByDescending(o => o.SoXeDuKien).FirstOrDefault();
                if (dinh != null)
                    ds.Add(new SmartAlert
                    {
                        Muc = dinh.MucDo == "Ùn tắc" ? "danger" : "warn",
                        Icon = "bi-stoplights",
                        Msg = "AI dự báo cổng đông lúc " + dinh.Luc.ToString("HH:00", Vi) + " (~" + dinh.SoXeDuKien.ToString("0.#", Vi)
                              + " xe/giờ) — nên mở " + dinh.SoLanCan + " làn.",
                        Url = "/admin/ai/cong"
                    });
            }
            catch
            {
            }

            try
            {
                var dao = AiBaiService.KeHoachDaoChuyen(db);
                var giam = dao.DaoChuyenTruoc - dao.DaoChuyenSau;
                if (giam >= 2)
                    ds.Add(new SmartAlert
                    {
                        Muc = "info",
                        Icon = "bi-boxes",
                        Msg = "AI bãi: làm " + dao.NuocDi.Count + " nước đảo trước lúc bãi rảnh sẽ bớt " + giam + " lần đảo khi khách lấy hàng.",
                        Url = "/admin/ai/bai"
                    });
            }
            catch
            {
            }

            try
            {
                // Hoc tren cung cua so 90 ngay nhu trang AI (it hon thi chua du luot de hoc phan bo), dem luot 7 ngay gan nhat
                var g1 = AiGianLanService.PhatHien(db, soHien: 100);
                var moc = DateTime.Now.AddDays(-7);
                var gan = g1.Luot.Count(l => l.NghiVan && l.ThoiGian >= moc);
                if (gan > 0)
                    ds.Add(new SmartAlert
                    {
                        Muc = "warn",
                        Icon = "bi-incognito",
                        Msg = "AI chống gian lận: " + gan + " lượt xe bất thường trong 7 ngày qua cần hậu kiểm.",
                        Url = "/admin/ai/gian-lan"
                    });
            }
            catch
            {
            }
            return ds;
        }
    }
}
