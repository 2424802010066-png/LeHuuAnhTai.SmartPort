using System;
using SmartPort.Helpers;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Che do van hanh cua he thong — doc tu Web.config appSettings.
    ///   CheDoTrinhDien = "true"  : ban TRINH DIEN (di thi/gioi thieu) — hien
    ///                              tai khoan trai nghiem nhanh, credit do an.
    ///   CheDoTrinhDien = "false" : ban VAN HANH THAT (khoi nghiep) — an het
    ///                              moi thu demo, chi con nghiep vu.
    /// </summary>
    public static class AppMode
    {
        private static bool? _trinhDien;

        public static bool TrinhDien
        {
            get
            {
                if (_trinhDien == null)
                {
                    var gt = CauHinh.AppSettings["CheDoTrinhDien"];
                    _trinhDien = string.Equals(gt, "true", StringComparison.OrdinalIgnoreCase);
                }
                return _trinhDien.Value;
            }
        }
    }
}
