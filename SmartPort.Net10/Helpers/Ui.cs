using System;
using System.Globalization;

namespace SmartPort.Helpers
{
    /// <summary>Ham tien ich dinh dang hien thi dung chung cho Views</summary>
    public static class Ui
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public static string Vnd(decimal? v)
        {
            if (!v.HasValue) return "—";
            return v.Value.ToString("#,##0", Vi) + " ₫";
        }

        public static string Num(decimal? v)
        {
            if (!v.HasValue) return "—";
            return v.Value.ToString("#,##0.#", Vi);
        }

        public static string Num(int? v)
        {
            if (!v.HasValue) return "—";
            return v.Value.ToString("#,##0", Vi);
        }

        public static string D(DateTime? d)
        {
            return d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "—";
        }

        public static string Dt(DateTime? d)
        {
            return d.HasValue ? d.Value.ToString("dd/MM/yyyy HH:mm") : "—";
        }

        /// <summary>Map trang thai -> css class badge</summary>
        public static string Badge(string status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            switch (s)
            {
                case "chờ duyệt":
                case "chờ xuất tàu":
                case "bảo trì":
                    return "st-warn";
                case "đã duyệt":
                case "dự kiến":
                case "trong bãi":
                    return "st-info";
                case "đang thực hiện":
                case "đang làm hàng":
                case "đã cập bến":
                    return "st-run";
                case "hoàn thành":
                case "đã rời bến":
                case "đã xuất tàu":
                case "đã giao khách":
                case "hoạt động":
                    return "st-ok";
                case "đã hủy":
                case "hủy":
                    return "st-off";
                default:
                    return "st-info";
            }
        }
    }
}
