using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// XUAT EXCEL (.xlsx that bang EPPlus):
    /// - Hoa don dich vu cang cho tung booking (khung hoa don ke toan VN,
    ///   tu dong doc so tien thanh chu)
    /// - Bao cao san luong & doanh thu theo nam
    /// </summary>
    public static class ExcelService
    {
        private static readonly Color Navy = ColorTranslator.FromHtml("#0A2F57");
        private static readonly Color Blue = ColorTranslator.FromHtml("#1173C4");
        private static readonly Color Orange = ColorTranslator.FromHtml("#E5691D");
        private static readonly Color LightBlue = ColorTranslator.FromHtml("#EAF2FB");

        /* ==================== HOA DON BOOKING ==================== */

        public static byte[] BuildInvoice(DatDichVu booking, KhachHang kh, List<DatDichVuChiTiet> items)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("HoaDon");
                ws.Cells.Style.Font.Name = "Calibri";
                ws.Cells.Style.Font.Size = 11;

                ws.Column(1).Width = 6;    // STT
                ws.Column(2).Width = 42;   // Dich vu
                ws.Column(3).Width = 12;   // DVT
                ws.Column(4).Width = 10;   // SL
                ws.Column(5).Width = 16;   // Don gia
                ws.Column(6).Width = 18;   // Thanh tien

                // ---- Header don vi ----
                ws.Cells["A1:F1"].Merge = true;
                ws.Cells["A1"].Value = "CẢNG QUỐC TẾ SMARTPORT";
                ws.Cells["A1"].Style.Font.Bold = true;
                ws.Cells["A1"].Style.Font.Size = 16;
                ws.Cells["A1"].Style.Font.Color.SetColor(Navy);

                ws.Cells["A2:F2"].Merge = true;
                ws.Cells["A2"].Value = "Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM  ·  Hotline: 0387 562 579  ·  MST: 0312 999 888";
                ws.Cells["A2"].Style.Font.Size = 10;
                ws.Cells["A2"].Style.Font.Color.SetColor(Color.Gray);

                // ---- Tieu de ----
                ws.Cells["A4:F4"].Merge = true;
                ws.Cells["A4"].Value = "HÓA ĐƠN DỊCH VỤ CẢNG";
                ws.Cells["A4"].Style.Font.Bold = true;
                ws.Cells["A4"].Style.Font.Size = 20;
                ws.Cells["A4"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells["A4"].Style.Font.Color.SetColor(Navy);

                ws.Cells["A5:F5"].Merge = true;
                ws.Cells["A5"].Value = "Số: " + booking.MaSoBooking + "   —   Ngày lập: " + DateTime.Now.ToString("dd/MM/yyyy");
                ws.Cells["A5"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells["A5"].Style.Font.Italic = true;

                // ---- Thong tin khach hang ----
                int r = 7;
                ws.Cells[r, 1].Value = "Khách hàng:";
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2, r, 6].Merge = true;
                ws.Cells[r, 2].Value = kh != null ? kh.TenCongTy : "";
                r++;
                ws.Cells[r, 1].Value = "Mã số thuế:";
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2, r, 3].Merge = true;
                ws.Cells[r, 2].Value = kh != null ? (kh.MaSoThue ?? "") : "";
                ws.Cells[r, 4].Value = "Điện thoại:";
                ws.Cells[r, 4].Style.Font.Bold = true;
                ws.Cells[r, 5, r, 6].Merge = true;
                ws.Cells[r, 5].Value = kh != null ? (kh.SoDienThoai ?? "") : "";
                r++;
                ws.Cells[r, 1].Value = "Địa chỉ:";
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2, r, 6].Merge = true;
                ws.Cells[r, 2].Value = kh != null ? (kh.DiaChi ?? "") : "";
                r++;
                ws.Cells[r, 1].Value = "Diễn giải:";
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2, r, 6].Merge = true;
                ws.Cells[r, 2].Value = "Thanh toán dịch vụ theo booking " + booking.MaSoBooking +
                    (booking.NgayCanThucHien.HasValue ? " — thực hiện ngày " + booking.NgayCanThucHien.Value.ToString("dd/MM/yyyy") : "");

                // ---- Bang dich vu ----
                r += 2;
                int headRow = r;
                string[] heads = { "STT", "Tên dịch vụ", "ĐVT", "Số lượng", "Đơn giá (₫)", "Thành tiền (₫)" };
                for (int c = 1; c <= 6; c++)
                {
                    var cell = ws.Cells[headRow, c];
                    cell.Value = heads[c - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Color.SetColor(Color.White);
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Navy);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                r++;
                int stt = 1;
                foreach (var it in items)
                {
                    ws.Cells[r, 1].Value = stt;
                    ws.Cells[r, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[r, 2].Value = it.DichVu != null ? it.DichVu.TenDV : ("Dịch vụ #" + it.MaDV);
                    ws.Cells[r, 3].Value = it.DichVu != null ? it.DichVu.DonViTinh : "";
                    ws.Cells[r, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[r, 4].Value = it.SoLuong;
                    ws.Cells[r, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[r, 5].Value = it.DonGia;
                    ws.Cells[r, 6].Value = it.ThanhTien;
                    if (stt % 2 == 0)
                    {
                        ws.Cells[r, 1, r, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws.Cells[r, 1, r, 6].Style.Fill.BackgroundColor.SetColor(LightBlue);
                    }
                    r++;
                    stt++;
                }

                // ---- Tong cong ----
                decimal cong = booking.TongTien;
                decimal vat = Math.Round(cong * 0.08m);
                decimal tong = cong + vat;

                ws.Cells[r, 1, r, 5].Merge = true;
                ws.Cells[r, 1].Value = "Cộng tiền dịch vụ:";
                ws.Cells[r, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 6].Value = cong;
                r++;
                ws.Cells[r, 1, r, 5].Merge = true;
                ws.Cells[r, 1].Value = "Thuế GTGT (8%):";
                ws.Cells[r, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 6].Value = vat;
                r++;
                ws.Cells[r, 1, r, 5].Merge = true;
                ws.Cells[r, 1].Value = "TỔNG THANH TOÁN:";
                ws.Cells[r, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 1].Style.Font.Size = 12;
                ws.Cells[r, 6].Value = tong;
                ws.Cells[r, 6].Style.Font.Bold = true;
                ws.Cells[r, 6].Style.Font.Size = 12;
                ws.Cells[r, 6].Style.Font.Color.SetColor(Orange);
                int totalRow = r;

                r++;
                ws.Cells[r, 1, r, 6].Merge = true;
                ws.Cells[r, 1].Value = "Bằng chữ: " + DocSoThanhChu(tong);
                ws.Cells[r, 1].Style.Font.Italic = true;

                // Border toan bang
                var table = ws.Cells[headRow, 1, totalRow, 6];
                table.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                table.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                ws.Cells[headRow + 1, 5, totalRow, 6].Style.Numberformat.Format = "#,##0";

                // ---- Chu ky ----
                r += 2;
                ws.Cells[r, 1, r, 2].Merge = true;
                ws.Cells[r, 1].Value = "NGƯỜI LẬP HÓA ĐƠN";
                ws.Cells[r, 3, r, 4].Merge = true;
                ws.Cells[r, 3].Value = "KẾ TOÁN TRƯỞNG";
                ws.Cells[r, 5, r, 6].Merge = true;
                ws.Cells[r, 5].Value = "KHÁCH HÀNG";
                ws.Cells[r, 1, r, 6].Style.Font.Bold = true;
                ws.Cells[r, 1, r, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                r++;
                ws.Cells[r, 1, r, 2].Merge = true;
                ws.Cells[r, 1].Value = "(Ký, họ tên)";
                ws.Cells[r, 3, r, 4].Merge = true;
                ws.Cells[r, 3].Value = "(Ký, họ tên)";
                ws.Cells[r, 5, r, 6].Merge = true;
                ws.Cells[r, 5].Value = "(Ký, họ tên)";
                ws.Cells[r, 1, r, 6].Style.Font.Italic = true;
                ws.Cells[r, 1, r, 6].Style.Font.Size = 9;
                ws.Cells[r, 1, r, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                return pkg.GetAsByteArray();
            }
        }

        /* ==================== BAO CAO NAM ==================== */

        public static byte[] BuildYearReport(int year, List<SanLuongThang> rows, List<SvcMonthRow> svcRows)
        {
            using (var pkg = new ExcelPackage())
            {
                // Sheet 1: San luong
                var ws = pkg.Workbook.Worksheets.Add("SanLuong" + year);
                ws.Cells.Style.Font.Name = "Calibri";
                ws.Column(1).Width = 14; ws.Column(2).Width = 18; ws.Column(3).Width = 22;

                ws.Cells["A1:C1"].Merge = true;
                ws.Cells["A1"].Value = "BÁO CÁO SẢN LƯỢNG & DOANH THU KHAI THÁC NĂM " + year + " — CẢNG SMARTPORT";
                ws.Cells["A1"].Style.Font.Bold = true;
                ws.Cells["A1"].Style.Font.Size = 13;
                ws.Cells["A1"].Style.Font.Color.SetColor(Navy);

                string[] heads = { "Tháng", "Sản lượng (TEU)", "Doanh thu (₫)" };
                for (int c = 1; c <= 3; c++)
                {
                    ws.Cells[3, c].Value = heads[c - 1];
                    ws.Cells[3, c].Style.Font.Bold = true;
                    ws.Cells[3, c].Style.Font.Color.SetColor(Color.White);
                    ws.Cells[3, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[3, c].Style.Fill.BackgroundColor.SetColor(Blue);
                }
                int r = 4;
                foreach (var row in rows)
                {
                    ws.Cells[r, 1].Value = "Tháng " + row.Thang;
                    ws.Cells[r, 2].Value = row.TEU;
                    ws.Cells[r, 3].Value = row.DoanhThu;
                    r++;
                }
                ws.Cells[r, 1].Value = "TỔNG CỘNG";
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 2].Value = rows.Sum(x => x.TEU);
                ws.Cells[r, 3].Value = rows.Sum(x => x.DoanhThu);
                ws.Cells[r, 1, r, 3].Style.Font.Bold = true;
                var t1 = ws.Cells[3, 1, r, 3];
                t1.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                t1.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                t1.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                t1.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                ws.Cells[4, 2, r, 3].Style.Numberformat.Format = "#,##0";

                // Sheet 2: Doanh thu dich vu ePort
                var ws2 = pkg.Workbook.Worksheets.Add("DoanhThuDichVu");
                ws2.Cells.Style.Font.Name = "Calibri";
                ws2.Column(1).Width = 14; ws2.Column(2).Width = 16; ws2.Column(3).Width = 22;
                ws2.Cells["A1:C1"].Merge = true;
                ws2.Cells["A1"].Value = "DOANH THU DỊCH VỤ ePORT (BOOKING HOÀN THÀNH) NĂM " + year;
                ws2.Cells["A1"].Style.Font.Bold = true;
                ws2.Cells["A1"].Style.Font.Color.SetColor(Navy);
                string[] heads2 = { "Tháng", "Số booking", "Doanh thu (₫)" };
                for (int c = 1; c <= 3; c++)
                {
                    ws2.Cells[3, c].Value = heads2[c - 1];
                    ws2.Cells[3, c].Style.Font.Bold = true;
                    ws2.Cells[3, c].Style.Font.Color.SetColor(Color.White);
                    ws2.Cells[3, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws2.Cells[3, c].Style.Fill.BackgroundColor.SetColor(Orange);
                }
                r = 4;
                foreach (var s in svcRows)
                {
                    ws2.Cells[r, 1].Value = "Tháng " + s.Thang;
                    ws2.Cells[r, 2].Value = s.SoBooking;
                    ws2.Cells[r, 3].Value = s.Tien;
                    r++;
                }
                ws2.Cells[r, 1].Value = "TỔNG";
                ws2.Cells[r, 2].Value = svcRows.Sum(x => x.SoBooking);
                ws2.Cells[r, 3].Value = svcRows.Sum(x => x.Tien);
                ws2.Cells[r, 1, r, 3].Style.Font.Bold = true;
                var t2 = ws2.Cells[3, 1, r, 3];
                t2.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                t2.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                t2.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                t2.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                ws2.Cells[4, 3, r, 3].Style.Numberformat.Format = "#,##0";

                return pkg.GetAsByteArray();
            }
        }

        /* ==================== DOC SO THANH CHU ==================== */

        private static readonly string[] ChuSo = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
        private static readonly string[] DonViNhom = { "", " nghìn", " triệu", " tỷ", " nghìn tỷ", " triệu tỷ" };

        /// <summary>Doc so tien VND thanh chu (VD: 1.590.000 → "Một triệu năm trăm chín mươi nghìn đồng chẵn")</summary>
        public static string DocSoThanhChu(decimal soTien)
        {
            long so = (long)Math.Round(soTien);
            if (so == 0) return "Không đồng";
            if (so < 0) return "Âm " + DocSoThanhChu(-so).ToLowerInvariant();

            // Tach thanh cac nhom 3 chu so
            var nhoms = new List<int>();
            while (so > 0)
            {
                nhoms.Add((int)(so % 1000));
                so /= 1000;
            }

            var parts = new List<string>();
            for (int i = nhoms.Count - 1; i >= 0; i--)
            {
                int n = nhoms[i];
                if (n == 0) continue;
                bool full = i < nhoms.Count - 1;   // nhom giua/cuoi doc day du "khong tram"
                parts.Add(Doc3ChuSo(n, full) + DonViNhom[i]);
            }

            var kq = string.Join(" ", parts).Trim() + " đồng chẵn";
            kq = char.ToUpperInvariant(kq[0]) + kq.Substring(1);
            return kq;
        }

        private static string Doc3ChuSo(int n, bool full)
        {
            int tram = n / 100, chuc = (n % 100) / 10, dv = n % 10;
            var s = "";

            if (tram > 0 || full) s += ChuSo[tram] + " trăm";

            if (chuc > 1)
            {
                s += " " + ChuSo[chuc] + " mươi";
                if (dv == 1) s += " mốt";
                else if (dv == 4) s += " tư";
                else if (dv == 5) s += " lăm";
                else if (dv > 0) s += " " + ChuSo[dv];
            }
            else if (chuc == 1)
            {
                s += " mười";
                if (dv == 5) s += " lăm";
                else if (dv > 0) s += " " + ChuSo[dv];
            }
            else if (dv > 0)
            {
                if (tram > 0 || full) s += " lẻ";
                s += " " + ChuSo[dv];
            }
            return s.Trim();
        }

        /// <summary>
        /// BAO CAO LUU LUONG CAMERA AI — 2 sheet:
        /// (1) Tong hop theo ngay + gio cao diem, (2) Nhat ky nhan dien chi tiet.
        /// Phuc vu doanh nghiep theo doi luu luong cong cang va bo tri nhan luc.
        /// </summary>
        public static byte[] BuildVisionReport(List<NhanDienAI> ds, int soNgay)
        {
            using (var pkg = new ExcelPackage())
            {
                // ---------- Sheet 1: Tong hop ----------
                var ws = pkg.Workbook.Worksheets.Add("Tổng hợp");
                ws.Cells[1, 1].Value = "BÁO CÁO LƯU LƯỢNG CỔNG CẢNG — CAMERA AI (YOLO26)";
                ws.Cells[1, 1, 1, 6].Merge = true;
                ws.Cells[1, 1].Style.Font.Bold = true;
                ws.Cells[1, 1].Style.Font.Size = 15;
                ws.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(10, 47, 87));
                ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[2, 1].Value = "Cảng Quốc tế SmartPort · Kỳ báo cáo: " + soNgay + " ngày gần nhất · Xuất lúc "
                                       + DateTime.Now.ToString("HH:mm dd/MM/yyyy");
                ws.Cells[2, 1, 2, 6].Merge = true;
                ws.Cells[2, 1].Style.Font.Italic = true;
                ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Chi so tong quan
                int r = 4;
                ws.Cells[r, 1].Value = "CHỈ SỐ TỔNG QUAN";
                ws.Cells[r, 1, r, 6].Merge = true;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[r, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(10, 47, 87));
                ws.Cells[r, 1].Style.Font.Color.SetColor(Color.White);
                r++;

                Action<string, object> dong = (nhan, giaTri) =>
                {
                    ws.Cells[r, 1].Value = nhan;
                    ws.Cells[r, 2].Value = giaTri;
                    ws.Cells[r, 2].Style.Font.Bold = true;
                    r++;
                };
                dong("Tổng lượt nhận diện", ds.Count);
                dong("Tổng xe tải/đầu kéo", ds.Sum(x => x.SoXeTai));
                dong("Tổng ô tô", ds.Sum(x => x.SoOto));
                dong("Tổng lượt người", ds.Sum(x => x.SoNguoi));
                dong("Số lần cảnh báo ÙN TẮC", ds.Count(x => x.MucCanhBao == "Ùn tắc"));
                dong("Số lần cảnh báo CHÚ Ý", ds.Count(x => x.MucCanhBao == "Chú ý"));
                dong("Thời gian xử lý TB (ms)", ds.Any() ? (int)ds.Average(x => x.ThoiGianXuLy ?? 0) : 0);
                dong("Độ tin cậy TB (%)", ds.Any(x => x.DoTinCayTB.HasValue)
                        ? Math.Round(ds.Where(x => x.DoTinCayTB.HasValue).Average(x => x.DoTinCayTB.Value), 1) : 0);

                // Theo ngay
                r++;
                ws.Cells[r, 1].Value = "LƯU LƯỢNG THEO NGÀY";
                ws.Cells[r, 1, r, 6].Merge = true;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[r, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(10, 47, 87));
                ws.Cells[r, 1].Style.Font.Color.SetColor(Color.White);
                r++;

                string[] head = { "Ngày", "Số lượt quét", "Xe tải/đầu kéo", "Ô tô", "Người", "Cảnh báo" };
                for (int i = 0; i < head.Length; i++)
                {
                    ws.Cells[r, i + 1].Value = head[i];
                    ws.Cells[r, i + 1].Style.Font.Bold = true;
                    ws.Cells[r, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[r, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(219, 229, 241));
                    ws.Cells[r, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
                r++;

                for (int i = soNgay - 1; i >= 0; i--)
                {
                    var d = DateTime.Today.AddDays(-i);
                    var trong = ds.Where(x => x.ThoiGian.Date == d).ToList();
                    ws.Cells[r, 1].Value = d.ToString("dd/MM/yyyy");
                    ws.Cells[r, 2].Value = trong.Count;
                    ws.Cells[r, 3].Value = trong.Sum(x => x.SoXeTai);
                    ws.Cells[r, 4].Value = trong.Sum(x => x.SoOto);
                    ws.Cells[r, 5].Value = trong.Sum(x => x.SoNguoi);
                    ws.Cells[r, 6].Value = trong.Count(x => x.MucCanhBao == "Ùn tắc" || x.MucCanhBao == "Chú ý");
                    for (int c = 1; c <= 6; c++) ws.Cells[r, c].Style.Border.BorderAround(ExcelBorderStyle.Hair);
                    r++;
                }

                // Gio cao diem
                r++;
                ws.Cells[r, 1].Value = "KHUNG GIỜ CAO ĐIỂM (theo số xe tải)";
                ws.Cells[r, 1, r, 6].Merge = true;
                ws.Cells[r, 1].Style.Font.Bold = true;
                ws.Cells[r, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[r, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 125, 45));
                ws.Cells[r, 1].Style.Font.Color.SetColor(Color.White);
                r++;
                ws.Cells[r, 1].Value = "Khung giờ"; ws.Cells[r, 2].Value = "Xe tải"; ws.Cells[r, 3].Value = "Lượt quét";
                for (int c = 1; c <= 3; c++)
                {
                    ws.Cells[r, c].Style.Font.Bold = true;
                    ws.Cells[r, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[r, c].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(219, 229, 241));
                }
                r++;
                var gioTop = Enumerable.Range(0, 24)
                    .Select(h => new { Gio = h, Xe = ds.Where(x => x.ThoiGian.Hour == h).Sum(x => x.SoXeTai), Luot = ds.Count(x => x.ThoiGian.Hour == h) })
                    .Where(x => x.Luot > 0).OrderByDescending(x => x.Xe).Take(8).ToList();
                foreach (var g in gioTop)
                {
                    ws.Cells[r, 1].Value = string.Format("{0:00}:00 – {1:00}:00", g.Gio, g.Gio + 1);
                    ws.Cells[r, 2].Value = g.Xe;
                    ws.Cells[r, 3].Value = g.Luot;
                    r++;
                }

                r++;
                ws.Cells[r, 1].Value = "Khuyến nghị: bố trí tăng cường nhân sự & mở thêm làn cổng trong các khung giờ cao điểm nêu trên.";
                ws.Cells[r, 1, r, 6].Merge = true;
                ws.Cells[r, 1].Style.Font.Italic = true;

                ws.Column(1).Width = 30;
                for (int c = 2; c <= 6; c++) ws.Column(c).Width = 16;

                // ---------- Sheet 2: Nhat ky chi tiet ----------
                var ws2 = pkg.Workbook.Worksheets.Add("Nhật ký nhận diện");
                string[] h2 = { "Mã", "Thời gian", "Vị trí camera", "Nguồn", "Mô hình", "Tổng ĐT", "Xe tải", "Ô tô", "Người", "Tàu", "Tin cậy %", "Xử lý (ms)", "Cảnh báo" };
                for (int i = 0; i < h2.Length; i++)
                {
                    ws2.Cells[1, i + 1].Value = h2[i];
                    ws2.Cells[1, i + 1].Style.Font.Bold = true;
                    ws2.Cells[1, i + 1].Style.Font.Color.SetColor(Color.White);
                    ws2.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws2.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(10, 47, 87));
                }
                int r2 = 2;
                foreach (var n in ds)
                {
                    ws2.Cells[r2, 1].Value = n.MaND;
                    ws2.Cells[r2, 2].Value = n.ThoiGian.ToString("dd/MM/yyyy HH:mm");
                    ws2.Cells[r2, 3].Value = n.ViTriCamera;
                    ws2.Cells[r2, 4].Value = n.Nguon;
                    ws2.Cells[r2, 5].Value = n.TenModel;
                    ws2.Cells[r2, 6].Value = n.TongDoiTuong;
                    ws2.Cells[r2, 7].Value = n.SoXeTai;
                    ws2.Cells[r2, 8].Value = n.SoOto;
                    ws2.Cells[r2, 9].Value = n.SoNguoi;
                    ws2.Cells[r2, 10].Value = n.SoTauThuyen;
                    ws2.Cells[r2, 11].Value = n.DoTinCayTB;
                    ws2.Cells[r2, 12].Value = n.ThoiGianXuLy;
                    ws2.Cells[r2, 13].Value = n.MucCanhBao;
                    if (n.MucCanhBao == "Ùn tắc")
                    {
                        ws2.Cells[r2, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws2.Cells[r2, 13].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(254, 226, 226));
                        ws2.Cells[r2, 13].Style.Font.Color.SetColor(Color.FromArgb(153, 27, 27));
                        ws2.Cells[r2, 13].Style.Font.Bold = true;
                    }
                    r2++;
                }
                ws2.Column(2).Width = 18; ws2.Column(3).Width = 18; ws2.Column(4).Width = 12; ws2.Column(5).Width = 12;
                ws2.Column(13).Width = 14;
                if (r2 > 2) ws2.Cells[1, 1, r2 - 1, h2.Length].AutoFilter = true;
                ws2.View.FreezePanes(2, 1);

                return pkg.GetAsByteArray();
            }
        }

        /// <summary>
        /// BAO CAO XE RA/VAO CONG (ALPR) — nhat ky bien so do AI doc duoc,
        /// kem thong ke vao/ra theo ngay. Phuc vu doi soat an ninh cong cang.
        /// </summary>
        public static byte[] BuildGateReport(List<XeRaVao> ds, int soNgay)
        {
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Nhật ký xe ra vào");
                ws.Cells[1, 1].Value = "NHẬT KÝ XE RA/VÀO CỔNG — HỆ THỐNG ALPR (YOLO26 + OCR)";
                ws.Cells[1, 1, 1, 8].Merge = true;
                ws.Cells[1, 1].Style.Font.Bold = true;
                ws.Cells[1, 1].Style.Font.Size = 14;
                ws.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(10, 47, 87));
                ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[2, 1].Value = "Cảng Quốc tế SmartPort · Kỳ: " + soNgay + " ngày · Tổng " + ds.Count
                                       + " lượt (Vào " + ds.Count(x => x.HuongDi == "Vào") + " / Ra " + ds.Count(x => x.HuongDi == "Ra")
                                       + ") · Xuất lúc " + DateTime.Now.ToString("HH:mm dd/MM/yyyy");
                ws.Cells[2, 1, 2, 8].Merge = true;
                ws.Cells[2, 1].Style.Font.Italic = true;
                ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                var h = new[] { "STT", "Thời gian", "Biển số", "Cổng", "Hướng", "Loại xe", "Tin cậy OCR (%)", "Nguồn" };
                for (int i = 0; i < h.Length; i++)
                {
                    var c = ws.Cells[4, i + 1];
                    c.Value = h[i];
                    c.Style.Font.Bold = true;
                    c.Style.Font.Color.SetColor(Color.White);
                    c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    c.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(10, 47, 87));
                }

                int r = 5, stt = 1;
                foreach (var x in ds)
                {
                    ws.Cells[r, 1].Value = stt++;
                    ws.Cells[r, 2].Value = x.ThoiGian.ToString("HH:mm:ss dd/MM/yyyy");
                    ws.Cells[r, 3].Value = x.BienSo;
                    ws.Cells[r, 3].Style.Font.Bold = true;
                    ws.Cells[r, 4].Value = x.Cong;
                    ws.Cells[r, 5].Value = x.HuongDi;
                    if (x.HuongDi == "Vào")
                        ws.Cells[r, 5].Style.Font.Color.SetColor(Color.FromArgb(21, 128, 61));
                    else
                        ws.Cells[r, 5].Style.Font.Color.SetColor(Color.FromArgb(194, 65, 12));
                    ws.Cells[r, 6].Value = x.LoaiXe;
                    ws.Cells[r, 7].Value = x.DoTinCay.HasValue ? (object)x.DoTinCay.Value : "";
                    ws.Cells[r, 8].Value = x.Nguon;
                    r++;
                }
                ws.Column(1).Width = 6; ws.Column(2).Width = 22; ws.Column(3).Width = 15;
                ws.Column(4).Width = 8; ws.Column(5).Width = 9; ws.Column(6).Width = 20;
                ws.Column(7).Width = 15; ws.Column(8).Width = 11;
                if (r > 5) ws.Cells[4, 1, r - 1, h.Length].AutoFilter = true;
                ws.View.FreezePanes(5, 1);

                return pkg.GetAsByteArray();
            }
        }
    }
}
