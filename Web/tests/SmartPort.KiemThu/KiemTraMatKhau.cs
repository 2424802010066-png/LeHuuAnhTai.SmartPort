using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartPort.Helpers;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU BAO MAT MAT KHAU (khong can CSDL): quy tac mat khau, ma xac nhan 6 so, bam HMAC, dau phien.
    /// Chay: dotnet run --project tests/SmartPort.KiemThu -- mat-khau
    /// </summary>
    public static class KiemTraMatKhau
    {
        private static readonly byte[] Khoa = Encoding.UTF8.GetBytes("khoa-kiem-thu-du-dai-32-ky-tu-tro-len!!");
        private static readonly byte[] KhoaKhac = Encoding.UTF8.GetBytes("mot-khoa-hoan-toan-khac-cung-du-32-ky-tu");

        public static void Chay(BoKiemTra bo)
        {
            QuyTac(bo);
            MaXacNhan(bo);
            DauPhien(bo);
        }

        private static bool CoLoi(List<string> loi, string tuKhoa) => loi.Any(l => l.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase));

        private static void QuyTac(BoKiemTra bo)
        {
            bo.Nhom("Mật khẩu: quy tắc mật khẩu mới");
            var l1 = BaoMatMatKhau.KiemTraChinhSach("123456");
            bo.Kiem("'123456' bị chặn: thiếu độ dài và thiếu chữ cái", CoLoi(l1, "ít nhất 8") && CoLoi(l1, "chữ cái"), string.Join(" | ", l1));
            bo.Kiem("'abcdefgh' bị chặn: không có chữ số", CoLoi(BaoMatMatKhau.KiemTraChinhSach("abcdefgh"), "chữ số"));
            bo.Kiem("'12345678' bị chặn: không có chữ cái + nằm trong danh sách phổ biến",
                CoLoi(BaoMatMatKhau.KiemTraChinhSach("12345678"), "chữ cái") && CoLoi(BaoMatMatKhau.KiemTraChinhSach("12345678"), "phổ biến"));
            bo.Kiem("'Password1' bị chặn: mật khẩu phổ biến (không phân biệt hoa thường)",
                CoLoi(BaoMatMatKhau.KiemTraChinhSach("Password1"), "phổ biến"));
            bo.Kiem("'a1a1a1a1' bị chặn: chỉ có 2 ký tự khác nhau", CoLoi(BaoMatMatKhau.KiemTraChinhSach("a1a1a1a1"), "phổ biến"));
            bo.Kiem("'Taile2026' bị chặn khi tên đăng nhập là 'taile' (chứa tên, không phân biệt hoa thường)",
                CoLoi(BaoMatMatKhau.KiemTraChinhSach("Taile2026", "taile"), "tên đăng nhập"));
            bo.Kiem("Chưa biết tên đăng nhập (nhập email) thì bỏ qua quy tắc chứa tên",
                BaoMatMatKhau.KiemTraChinhSach("Taile2026").Count == 0);
            bo.Kiem("Khoảng trắng ở đầu / cuối bị chặn", CoLoi(BaoMatMatKhau.KiemTraChinhSach(" Cang2026x"), "khoảng trắng"));
            bo.Kiem("51 ký tự bị chặn (tối đa 50)", CoLoi(BaoMatMatKhau.KiemTraChinhSach(new string('a', 45) + "123456"), "tối đa 50"));
            bo.Kiem("'Cang@2026xyz' hợp lệ", BaoMatMatKhau.KiemTraChinhSach("Cang@2026xyz", "dieudo").Count == 0);
            bo.Kiem("Chữ tiếng Việt có dấu tính là chữ cái: 'Cảng2026Đông' hợp lệ",
                BaoMatMatKhau.KiemTraChinhSach("Cảng2026Đông", "admin").Count == 0);
            bo.Kiem("Mật khẩu rỗng / null không làm hỏng hàm (báo lỗi độ dài)",
                CoLoi(BaoMatMatKhau.KiemTraChinhSach(null), "ít nhất 8") && CoLoi(BaoMatMatKhau.KiemTraChinhSach(""), "ít nhất 8"));
        }

        private static void MaXacNhan(BoKiemTra bo)
        {
            bo.Nhom("Mật khẩu: mã xác nhận 6 số + HMAC");
            var ds = Enumerable.Range(0, 300).Select(_ => BaoMatMatKhau.TaoMaXacNhan()).ToList();
            bo.Kiem("300 mã sinh ra đều đúng 6 chữ số", ds.All(BaoMatMatKhau.DungDinhDangMa));
            bo.Kiem("Mã ngẫu nhiên: 300 lần sinh có ≥ 290 mã khác nhau", ds.Distinct().Count() >= 290, ds.Distinct().Count() + " mã khác nhau");
            bo.Kiem("Định dạng: '12345', '1234567', '12a456', null đều sai",
                !BaoMatMatKhau.DungDinhDangMa("12345") && !BaoMatMatKhau.DungDinhDangMa("1234567")
                && !BaoMatMatKhau.DungDinhDangMa("12a456") && !BaoMatMatKhau.DungDinhDangMa(null));

            var h = BaoMatMatKhau.BamMaXacNhan(Khoa, 7, "042913");
            bo.Kiem("HMAC dạng hex 64 ký tự (SHA-256)", h.Length == 64 && h.All(Uri.IsHexDigit));
            bo.Kiem("Cùng khóa + tài khoản + mã → cùng HMAC (kiểm lại được)", h == BaoMatMatKhau.BamMaXacNhan(Khoa, 7, "042913"));
            bo.Kiem("Mã của tài khoản 7 không dùng được cho tài khoản 8", h != BaoMatMatKhau.BamMaXacNhan(Khoa, 8, "042913"));
            bo.Kiem("Đổi khóa server → HMAC khác (lộ CSDL không đoán ngược được mã)", h != BaoMatMatKhau.BamMaXacNhan(KhoaKhac, 7, "042913"));
            bo.Kiem("Không lưu mã gốc: HMAC không chứa chuỗi '042913'", !h.Contains("042913"));
            bo.Kiem("So sánh thời gian không đổi: bằng nhau (không phân biệt hoa thường) / khác / null",
                BaoMatMatKhau.BangNhau(h, h.ToLowerInvariant()) && !BaoMatMatKhau.BangNhau(h, BaoMatMatKhau.BamMaXacNhan(Khoa, 7, "042914"))
                && !BaoMatMatKhau.BangNhau(null, h));
        }

        private static void DauPhien(BoKiemTra bo)
        {
            bo.Nhom("Mật khẩu: dấu phiên (đăng xuất mọi thiết bị khi đổi mật khẩu)");
            var hashCu = Security.HashPasswordStrong("MatKhauCu2026");
            var hashMoi = Security.HashPasswordStrong("MatKhauMoi2026");
            var d1 = BaoMatMatKhau.DauPhien(Khoa, hashCu);
            bo.Kiem("Dấu phiên 16 ký tự hex", d1.Length == 16 && d1.All(Uri.IsHexDigit));
            bo.Kiem("Mật khẩu chưa đổi → dấu giữ nguyên (token cũ còn dùng được)", d1 == BaoMatMatKhau.DauPhien(Khoa, hashCu));
            bo.Kiem("Đổi mật khẩu → dấu đổi (token cũ bị từ chối)", d1 != BaoMatMatKhau.DauPhien(Khoa, hashMoi));
            bo.Kiem("Đặt lại CÙNG mật khẩu cũ vẫn đổi dấu (PBKDF2 có muối ngẫu nhiên mới)",
                d1 != BaoMatMatKhau.DauPhien(Khoa, Security.HashPasswordStrong("MatKhauCu2026")));
            bo.Kiem("Dấu phụ thuộc khóa bí mật của server", d1 != BaoMatMatKhau.DauPhien(KhoaKhac, hashCu));
            bo.Kiem("Hash mật khẩu mới vẫn kiểm đúng mật khẩu (PBKDF2)", Security.VerifyPassword("x", "MatKhauMoi2026", hashMoi)
                && !Security.VerifyPassword("x", "MatKhauCu2026", hashMoi));
        }
    }
}
