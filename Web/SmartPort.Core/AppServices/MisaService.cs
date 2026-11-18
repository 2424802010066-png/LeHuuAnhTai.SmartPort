using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ================ TÍCH HỢP KẾ TOÁN MISA ================
    /// Xuất chứng từ bán hàng dịch vụ theo ĐÚNG BỐ CỤC bảng nhập liệu
    /// Excel của MISA (AMIS Kế toán / MISA SME) — kế toán chỉ cần vào
    /// "Nghiệp vụ → Nhập chứng từ từ Excel" và chọn file này.
    ///
    /// Ghi chú kiến trúc: MISA có OpenAPI (graphapi.misa.vn) nhưng yêu cầu
    /// doanh nghiệp đăng ký AppID/khóa bí mật với MISA. Trong phạm vi đồ án,
    /// tích hợp qua CHUẨN TRAO ĐỔI FILE — cách nhiều hệ thống thực tế
    /// vẫn dùng song song với API.
    /// </summary>
    public static class MisaService
    {
        /// <summary>
        /// Bảng kê chứng từ BÁN HÀNG DỊCH VỤ từ các giao dịch ĐÃ THANH TOÁN.
        /// Định khoản chuẩn: Nợ TK 131 (phải thu KH) / Có TK 5113 (doanh thu dịch vụ).
        /// </summary>
        public static byte[] BuildSalesVoucherExcel(AppDb db, DateTime tuNgay, DateTime denNgay)
        {
            var den = denNgay.Date.AddDays(1);
            // HIEU NANG: Include booking + khach hang ngay tu dau — 1 truy van
            // thay cho 2N+1 lan Find trong vong lap xuat chung tu
            var ds = db.ThanhToans
                .Include(t => t.DatDichVu.KhachHang)
                .Where(t => t.TrangThai == "Đã thanh toán"
                            && t.NgayThanhToan >= tuNgay && t.NgayThanhToan < den)
                .OrderBy(t => t.NgayThanhToan)
                .ToList();

            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Ban hang dich vu");

                // ---------- Tieu de ----------
                ws.Cells[1, 1].Value = "BẢNG KÊ CHỨNG TỪ BÁN HÀNG DỊCH VỤ — NHẬP VÀO MISA";
                ws.Cells[1, 1, 1, 12].Merge = true;
                ws.Cells[1, 1].Style.Font.Bold = true;
                ws.Cells[1, 1].Style.Font.Size = 14;
                ws.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(10, 47, 87));
                ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[2, 1].Value = "Cảng Quốc tế SmartPort · Kỳ " + tuNgay.ToString("dd/MM/yyyy") + " – " + denNgay.ToString("dd/MM/yyyy")
                                       + " · " + ds.Count + " chứng từ · Định khoản: Nợ 131 / Có 5113";
                ws.Cells[2, 1, 2, 12].Merge = true;
                ws.Cells[2, 1].Style.Font.Italic = true;
                ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // ---------- Dong tieu de cot theo mau nhap MISA ----------
                var h = new[]
                {
                    "Ngày hạch toán (*)", "Ngày chứng từ (*)", "Số chứng từ (*)",
                    "Diễn giải", "Mã khách hàng", "Tên khách hàng", "Mã số thuế", "Địa chỉ",
                    "TK Nợ (*)", "TK Có (*)", "Số tiền", "Hình thức thanh toán"
                };
                for (int i = 0; i < h.Length; i++)
                {
                    var c = ws.Cells[4, i + 1];
                    c.Value = h[i];
                    c.Style.Font.Bold = true;
                    c.Style.Font.Color.SetColor(Color.White);
                    c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    c.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(16, 121, 63));   // xanh MISA
                    c.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // ---------- Du lieu ----------
                int r = 5;
                decimal tong = 0;
                foreach (var t in ds)
                {
                    var bk = t.DatDichVu;
                    var kh = bk != null ? bk.KhachHang : null;
                    var ngay = t.NgayThanhToan ?? t.NgayTao;

                    ws.Cells[r, 1].Value = ngay.ToString("dd/MM/yyyy");
                    ws.Cells[r, 2].Value = ngay.ToString("dd/MM/yyyy");
                    ws.Cells[r, 3].Value = "BH" + ngay.ToString("yyMM") + "-" + t.MaTT.ToString("D4");
                    ws.Cells[r, 4].Value = "Thu tiền dịch vụ cảng theo booking " + (bk != null ? bk.MaSoBooking : ("#" + t.MaBooking))
                                           + " (" + t.TenPhuongThuc + ", mã CK " + t.NoiDungCK + ")";
                    ws.Cells[r, 5].Value = kh != null ? "KH" + kh.MaKH.ToString("D4") : "";
                    ws.Cells[r, 6].Value = kh != null ? kh.TenCongTy : "Khách lẻ";
                    ws.Cells[r, 7].Value = kh != null ? kh.MaSoThue : "";
                    ws.Cells[r, 8].Value = kh != null ? kh.DiaChi : "";
                    ws.Cells[r, 9].Value = "131";
                    ws.Cells[r, 10].Value = "5113";
                    ws.Cells[r, 11].Value = t.SoTien;
                    ws.Cells[r, 11].Style.Numberformat.Format = "#,##0";
                    ws.Cells[r, 12].Value = t.TenPhuongThuc;
                    tong += t.SoTien;
                    r++;
                }

                // ---------- Dong tong ----------
                ws.Cells[r, 4].Value = "TỔNG CỘNG";
                ws.Cells[r, 4].Style.Font.Bold = true;
                ws.Cells[r, 11].Value = tong;
                ws.Cells[r, 11].Style.Numberformat.Format = "#,##0";
                ws.Cells[r, 11].Style.Font.Bold = true;
                ws.Cells[r, 11].Style.Font.Color.SetColor(Color.FromArgb(16, 121, 63));

                // ---------- Trinh bay ----------
                ws.Column(1).Width = 15; ws.Column(2).Width = 15; ws.Column(3).Width = 15;
                ws.Column(4).Width = 52; ws.Column(5).Width = 13; ws.Column(6).Width = 30;
                ws.Column(7).Width = 14; ws.Column(8).Width = 30; ws.Column(9).Width = 9;
                ws.Column(10).Width = 9; ws.Column(11).Width = 15; ws.Column(12).Width = 20;
                if (r > 5) ws.Cells[4, 1, r - 1, h.Length].AutoFilter = true;
                ws.View.FreezePanes(5, 1);

                // ---------- Sheet huong dan ----------
                var hd = pkg.Workbook.Worksheets.Add("Hướng dẫn nhập MISA");
                hd.Cells[1, 1].Value = "CÁCH NHẬP FILE NÀY VÀO MISA";
                hd.Cells[1, 1].Style.Font.Bold = true; hd.Cells[1, 1].Style.Font.Size = 14;
                var steps = new[]
                {
                    "1. Mở MISA AMIS Kế toán (hoặc MISA SME).",
                    "2. Vào menu: Nghiệp vụ → Nhập chứng từ từ Excel (Import).",
                    "3. Chọn loại chứng từ: Bán hàng / Thu tiền khách hàng.",
                    "4. Chọn file này, ánh xạ cột theo dòng tiêu đề (các cột (*) là bắt buộc).",
                    "5. Kiểm tra dữ liệu xem trước rồi bấm Thực hiện — chứng từ sẽ được hạch toán Nợ 131 / Có 5113.",
                    "",
                    "Lưu ý: nếu doanh nghiệp có đăng ký MISA OpenAPI (graphapi.misa.vn),",
                    "có thể nâng cấp lên đồng bộ tự động qua API bằng AppID được MISA cấp."
                };
                for (int i = 0; i < steps.Length; i++) hd.Cells[3 + i, 1].Value = steps[i];
                hd.Column(1).Width = 100;

                return pkg.GetAsByteArray();
            }
        }
    }
}
