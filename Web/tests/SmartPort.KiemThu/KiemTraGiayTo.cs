using System;
using SmartPort.Services;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU BOC BANG LAI TU VAN BAN OCR (khong can CSDL): van ban OCR tieng Viet (co dau) va tieng Anh (khong dau),
    /// nhan "Hạng/Class", ma hang moi 2025, "Không thời hạn", loi OCR doc B thanh 8. Du lieu GIA.
    /// Chay: dotnet run --project tests/SmartPort.KiemThu -- giay-to
    /// </summary>
    public static class KiemTraGiayTo
    {
        public static void Chay(BoKiemTra bo)
        {
            bo.Nhom("Giấy tờ: bóc số / hạng / hạn bằng lái từ văn bản OCR");

            // OCR du lieu tieng Viet (vie): co dau, nhan song ngu tren the PET
            var vie = GiayToService.DocOcrGplx(
                "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nGIẤY PHÉP LÁI XE / DRIVER'S LICENSE\nSố/No: 790123456789\n" +
                "Họ tên/Full name: NGUYỄN VĂN AN\nNgày sinh/Date of Birth: 15/04/1995\nHạng/Class: C\n" +
                "Có giá trị đến/Expires: 20/08/2031\nNgày trúng tuyển/Beginning date: 20/08/2021");
            bo.Kiem("OCR tiếng Việt có dấu: đọc đủ số 790123456789", vie.Ok && vie.SoGplx == "790123456789", vie.SoGplx);
            bo.Kiem("'Hạng/Class: C' -> hạng C (trước đây trượt vì vướng '/Class')", vie.Hang == "C", vie.Hang);
            bo.Kiem("'Có giá trị đến/Expires' có dấu sắc (ĐẾN) -> hạn 20/08/2031", vie.HanDen == new DateTime(2031, 8, 20), vie.HanDen?.ToString("dd/MM/yyyy"));

            // OCR du lieu tieng Anh (eng) nhu cu: mat dau
            var eng = GiayToService.DocOcrGplx("GIAY PHEP LAI XE\nSo/No: 790123456789\nHang/Class: B2\nCo gia tri den/Expires: 01/01/2030");
            bo.Kiem("OCR tiếng Anh không dấu vẫn đọc được: B2, hạn 01/01/2030",
                eng.Ok && eng.Hang == "B2" && eng.HanDen == new DateTime(2030, 1, 1), eng.Hang + " " + eng.HanDen?.ToString("dd/MM/yyyy"));

            // Mau cu khong co nhan tieng Anh
            var cu = GiayToService.DocOcrGplx("Số: 790123456789\nHạng: FC\nCó giá trị đến: 15/06/2029");
            bo.Kiem("Mẫu cũ 'Hạng: FC' / 'Có giá trị đến: 15/06/2029'", cu.Hang == "FC" && cu.HanDen == new DateTime(2029, 6, 15));

            bo.Kiem("Hạng mới 2025 'C1E' (không cắt thành C1 hay C)", GiayToService.DocOcrGplx("Hạng/Class: C1E\nSố/No: 790123456789").Hang == "C1E");
            bo.Kiem("Chỉ có nhãn tiếng Anh 'Class: D' -> D", GiayToService.DocOcrGplx("Class: D\nNo: 790123456789").Hang == "D");
            bo.Kiem("OCR đọc nhầm 'B2' thành '82' ngay sau nhãn hạng -> B2", GiayToService.DocOcrGplx("Hạng/Class: 82\nSố/No: 790123456789").Hang == "B2");
            bo.Kiem("Hạn viết dấu chấm 'Expires 20.08.2031'", GiayToService.DocOcrGplx("No: 790123456789\nExpires 20.08.2031").HanDen == new DateTime(2031, 8, 20));

            // Bang khong thoi han: khong duoc lay nham ngay trung tuyen / ngay sinh lam han
            var vo = GiayToService.DocOcrGplx("Số/No: 790123456789\nHạng/Class: A1\nNgày sinh: 15/04/1995\nCó giá trị đến/Expires: Không thời hạn\nNgày trúng tuyển: 20/08/2021");
            bo.Kiem("'Không thời hạn' -> để trống hạn (không lấy nhầm 20/08/2021)", vo.Ok && vo.Hang == "A1" && vo.HanDen == null, vo.HanDen?.ToString("dd/MM/yyyy"));

            // Van ban khong co gi / rong
            bo.Kiem("Văn bản rỗng -> báo lỗi", !GiayToService.DocOcrGplx("  ").Ok);
            bo.Kiem("Văn bản không có số / hạng / ngày -> báo lỗi, không đoán bừa", !GiayToService.DocOcrGplx("xin chao cang Cat Lai").Ok);

            ChayCccd(bo);
        }

        /// <summary>Boc truong tu chuoi QR CCCD: du lieu GIA, so CCCD gia. Chay cung nhom giay-to.</summary>
        public static void ChayCccd(BoKiemTra bo)
        {
            bo.Nhom("Giấy tờ: bóc trường từ chuỗi QR CCCD (dữ liệu giả)");
            const string SO = "079095001234";
            const string DC = "12/3 Trần Hưng Đạo, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh";

            var d = GiayToService.DocQrCccd($"{SO}|123456789|NGUYỄN VĂN AN|15041995|Nam|{DC}|20082021");
            bo.Kiem("7 trường chuẩn: đủ số, CMND, tên, ngày sinh, giới tính, địa chỉ, ngày cấp",
                d.Ok && d.SoCccd == SO && d.SoCmndCu == "123456789" && d.HoTen == "Nguyễn Văn An" && d.NgaySinh == new DateTime(1995, 4, 15) && d.GioiTinh == "Nam" && d.DiaChi == DC && d.NgayCap == new DateTime(2021, 8, 20), d.Loi);

            var k = GiayToService.DocQrCccd($"{SO}||Trần Thị Bé|15041995|Nữ|{DC}|20082021");
            bo.Kiem("Thiếu CMND cũ (trường rỗng) vẫn hợp lệ", k.Ok && k.SoCmndCu == "" && k.HoTen == "Trần Thị Bé" && k.GioiTinh == "Nữ", k.Loi);

            var o = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam|{DC}");
            bo.Kiem("6 trường (không có ngày cấp) vẫn đọc được", o.Ok && o.NgayCap == null && o.DiaChi == DC, o.Loi);

            bo.Kiem("Thiếu trường (5 trường) -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam").Ok);
            var dd = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam|Thôn 1|Xã A|20082021");
            bo.Kiem("Địa chỉ chứa dấu | -> ghép lại, ngày cấp lấy đúng", dd.Ok && dd.DiaChi == "Thôn 1, Xã A" && dd.NgayCap == new DateTime(2021, 8, 20), dd.DiaChi + " / " + dd.NgayCap);
            var du = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam|{DC}|20082021|Trường mới|");
            bo.Kiem("Có thêm trường sau ngày cấp -> vẫn lấy đúng địa chỉ + ngày cấp", du.Ok && du.DiaChi == DC && du.NgayCap == new DateTime(2021, 8, 20), du.DiaChi);

            foreach (var sai in new[] { "07909500123", "0790950012345", "07909500123A", "" })
                bo.Kiem($"Số CCCD '{sai}' sai định dạng -> từ chối", !GiayToService.DocQrCccd($"{sai}||Lê Văn Cường|15041995|Nam|{DC}|20082021").Ok);
            foreach (var kqr in new[] { "https://smartport.vn/lenh/123", "00020101021138570010A000000727", "{\"so\":1}", "   ", null })
                bo.Kiem($"Chuỗi không phải CCCD ({kqr ?? "null"}) -> từ chối", !GiayToService.DocQrCccd(kqr).Ok);
            bo.Kiem("12 số + đủ dấu | nhưng tên/ngày rác -> từ chối, không coi là thẻ", !GiayToService.DocQrCccd("123456789012|a|b|c|d|e").Ok);
            bo.Kiem("Tên chứa số -> từ chối", !GiayToService.DocQrCccd($"{SO}||Nguyen123|15041995|Nam|{DC}|20082021").Ok);

            var nhuan = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|29022004|Nam|{DC}|20082021");
            bo.Kiem("29/02 năm nhuận hợp lệ", nhuan.Ok && nhuan.NgaySinh == new DateTime(2004, 2, 29), nhuan.Loi);
            bo.Kiem("29/02 năm không nhuận -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|29022003|Nam|{DC}|20082021").Ok);
            bo.Kiem("Ngày sinh 32/13 -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|32132000|Nam|{DC}|20082021").Ok);
            bo.Kiem("Ngày sinh ở tương lai -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|01012099|Nam|{DC}|20082021").Ok);
            bo.Kiem("Thiếu ngày sinh -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường||Nam|{DC}|20082021").Ok);
            var cap = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam|{DC}|32132021");
            bo.Kiem("Ngày cấp hỏng không làm mất cả thẻ: vẫn đọc, ngày cấp để trống", cap.Ok && cap.NgayCap == null, cap.Loi);

            var nfd = GiayToService.DocQrCccd($"{SO}||{"NGUYỄN THỊ ÁNH".Normalize(System.Text.NormalizationForm.FormD)}|15041995|{"Nữ".Normalize(System.Text.NormalizationForm.FormD)}|{DC.Normalize(System.Text.NormalizationForm.FormD)}|20082021");
            bo.Kiem("Tiếng Việt NFD -> chuẩn về NFC (tên, giới tính, địa chỉ)", nfd.Ok && nfd.HoTen == "Nguyễn Thị Ánh" && nfd.GioiTinh == "Nữ" && nfd.DiaChi == DC, nfd.HoTen);
            var hoa = GiayToService.DocQrCccd($"{SO}||ĐẶNG THỊ Ả|15041995|NỮ|{DC}|20082021");
            bo.Kiem("Tên VIẾT HOA -> Chữ Hoa Đầu Từ; 'NỮ' -> 'Nữ'", hoa.Ok && hoa.HoTen == "Đặng Thị Ả" && hoa.GioiTinh == "Nữ", hoa.HoTen + "/" + hoa.GioiTinh);
            var bom = GiayToService.DocQrCccd($"\uFEFF{SO}||Lê Văn Cường|15041995|Nam|{DC}|20082021\r\n\0");
            bo.Kiem("Ký tự BOM / xuống dòng / NUL ở đầu cuối chuỗi", bom.Ok && bom.SoCccd == SO && bom.NgayCap == new DateTime(2021, 8, 20), bom.Loi);
            var kc = GiayToService.DocQrCccd($" {SO} | | Lê  Văn   Cường | 15041995 | Nam | {DC} | 20082021 ");
            bo.Kiem("Khoảng trắng thừa quanh trường", kc.Ok && kc.HoTen == "Lê Văn Cường" && kc.NgayCap == new DateTime(2021, 8, 20), kc.Loi);
            bo.Kiem("Thẻ khớp hết -> không cảnh báo", d.CanhBao.Count == 0, string.Join(" ", d.CanhBao));
            var gt = GiayToService.DocQrCccd($"{SO}||Lê Thị Cường|15041995|Nữ|{DC}|20082021");
            bo.Kiem("Số CCCD ứng nam mà QR ghi nữ -> vẫn đọc nhưng cảnh báo", gt.Ok && gt.CanhBao.Count == 1 && gt.CanhBao[0].Contains("giới tính"), string.Join(" ", gt.CanhBao));
            var nam = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041988|Nam|{DC}|20082021");
            bo.Kiem("Số CCCD ứng 1995 mà QR ghi 1988 -> cảnh báo", nam.Ok && nam.CanhBao.Count == 1 && nam.CanhBao[0].Contains("năm sinh"), string.Join(" ", nam.CanhBao));
            var tinh = GiayToService.DocQrCccd($"999095001234||Lê Văn Cường|15041995|Nam|{DC}|20082021");
            bo.Kiem("Mã tỉnh 999 không có trong danh mục -> cảnh báo, không chặn", tinh.Ok && tinh.CanhBao.Count == 1 && tinh.CanhBao[0].Contains("Mã tỉnh"), string.Join(" ", tinh.CanhBao));
            var the21 = GiayToService.DocQrCccd($"079206001234||Lê Văn Em|15042006|Nam|{DC}|20082024");
            bo.Kiem("Sinh thế kỷ 21 (chữ số thứ 4 = 2) -> không cảnh báo", the21.Ok && the21.CanhBao.Count == 0, string.Join(" ", the21.CanhBao));
            var chiNam = GiayToService.DocQrCccd($"079050001234||Lê Văn Lão|1950|Nam|{DC}|20082021");
            bo.Kiem("Thẻ chỉ ghi năm sinh -> nhận, cảnh báo, không đoán ngày", chiNam.Ok && chiNam.NgaySinh == null && chiNam.CanhBao.Count == 1, chiNam.Loi);
            var capTL = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|Nam|{DC}|20082099");
            bo.Kiem("Ngày cấp ở tương lai -> để trống + cảnh báo", capTL.Ok && capTL.NgayCap == null && capTL.CanhBao.Count == 1, string.Join(" ", capTL.CanhBao));
            var gtLa = GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|15041995|XYZ|{DC}|20082021");
            bo.Kiem("Giới tính lạ -> để trống + cảnh báo", gtLa.Ok && gtLa.GioiTinh == null && gtLa.CanhBao.Count == 1, string.Join(" ", gtLa.CanhBao));
            bo.Kiem("Ngày sinh trước 1900 -> từ chối", !GiayToService.DocQrCccd($"{SO}||Lê Văn Cường|01011800|Nam|{DC}|20082021").Ok);
            bo.Kiem("Tên chứa thẻ HTML -> từ chối", !GiayToService.DocQrCccd($"{SO}||<script>alert(1)</script>|15041995|Nam|{DC}|20082021").Ok);
            var ngang = GiayToService.DocQrCccd($"{SO}||Y-Bih O'Neil|15041995|Nam|{DC}|20082021");
            bo.Kiem("Tên dân tộc có dấu gạch / nháy vẫn hợp lệ", ngang.Ok, ngang.Loi);
            bo.Kiem("Tuổi tính đúng (năm sinh 1995)", d.Tuoi == DateTime.Today.Year - 1995 - (new DateTime(DateTime.Today.Year, 4, 15) > DateTime.Today ? 1 : 0), d.Tuoi?.ToString());
        }
    }
}
