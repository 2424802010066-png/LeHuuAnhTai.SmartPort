using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPort.Api.Controllers.ThuTien
{
    /// <summary>
    /// Tien ich dung chung cua khu THU TIEN (dung chung dich vu Tai chinh + Kinh doanh): doc so tien nguoi dung go, viet so tien kieu Viet Nam.
    /// </summary>
    public static class TienTe
    {
        private static readonly CultureInfo VN = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>"3.420.000đ" — co dinh dinh dang Viet Nam, khong phu thuoc ngon ngu cua may chu</summary>
        public static string Tien(decimal soTien) => soTien.ToString("N0", VN) + "đ";

        /// <summary>
        /// Doc so tien nguoi thu ngan go: "50.000", "50,000", "50000" deu hieu la 50000 (giong ban MVC:
        /// bo dau cham, dau phay). Them: bo khoang trang va chu "đ" o cuoi. Chuoi rong -> false.
        /// </summary>
        public static bool DocTien(string chuoi, out decimal soTien)
        {
            soTien = 0m;
            if (string.IsNullOrWhiteSpace(chuoi)) return false;
            var sb = new StringBuilder(chuoi.Length);
            foreach (var ch in chuoi)
            {
                if (ch == '.' || ch == ',' || char.IsWhiteSpace(ch) || ch == 'đ' || ch == 'Đ' || ch == '₫') continue;
                sb.Append(ch);
            }
            return sb.Length > 0 && decimal.TryParse(sb.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out soTien);
        }
    }

    /// <summary>
    /// O so tien nhan ca chuoi ("50.000") lan so JSON (50000). So JSON lam tron thanh dong
    /// (tien Viet khong co phan le) de khi doc lai khong bi hieu nham dau cham thap phan la dau nghin.
    /// </summary>
    public sealed class ChuoiHoacSoJson : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetDecimal(out var so)
                        ? Math.Round(so, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
                        : null;
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException("Số tiền phải là chuỗi hoặc số.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
