using System;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// Doi duong dan "ao" luu trong CSDL / sinh ra tu nghiep vu sang dia chi giao dien React dung duoc.
    ///   "~/Uploads/gate/a.jpg"      -> "/Uploads/gate/a.jpg"   (API phuc vu file tinh trong wwwroot)
    ///   "~/Admin/Containers/HoSo/5" -> "/Admin/Containers/HoSo/5"
    /// Man hinh React GIU NGUYEN dia chi cua ban MVC cu (React Router so khop khong phan biet hoa thuong),
    /// nen link do nghiep vu sinh ra, link da luu trong CSDL, ma QR da in tren chung tu van mo dung man hinh.
    /// </summary>
    public static class DuongDan
    {
        public static string AnhTuApi(string duongDanAo)
        {
            if (string.IsNullOrEmpty(duongDanAo)) return null;
            if (duongDanAo.StartsWith("http", StringComparison.OrdinalIgnoreCase) || duongDanAo.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return duongDanAo;
            return duongDanAo.StartsWith("~/", StringComparison.Ordinal) ? duongDanAo.Substring(1) : duongDanAo;
        }

        public static string SangGiaoDien(string duongDan)
        {
            if (string.IsNullOrEmpty(duongDan) || duongDan.StartsWith("#", StringComparison.Ordinal)
                || duongDan.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return duongDan;
            var p = duongDan.StartsWith("~/", StringComparison.Ordinal) ? duongDan.Substring(1) : duongDan;
            return p.StartsWith("/", StringComparison.Ordinal) ? p : "/" + p;
        }
    }
}
