using System.Collections.Generic;
using System.Text.Json;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// Noi dung su kien "du-lieu.&lt;bang&gt;": mot dong vua duoc them / sua / xoa o dich vu so huu.
    /// Mang DAY DU gia tri dong (khong chi phan thay doi) -> ben nhan ghi de thang, khong can doc nguoc lai.
    /// Khoa va gia tri theo TEN THUOC TINH EF (ben nhan dung cung mo hinh EF de doi ra ten cot).
    /// </summary>
    public sealed class ThayDoiDuLieu
    {
        public const string TienTo = "du-lieu.";
        public const string Them = "them";
        public const string Sua = "sua";
        public const string Xoa = "xoa";

        public string Bang { get; set; }
        public string ThaoTac { get; set; }
        public Dictionary<string, JsonElement> Khoa { get; set; }
        public Dictionary<string, JsonElement> DuLieu { get; set; }

        public static string LoaiSuKien(string bang) => TienTo + bang.ToLowerInvariant();
    }
}
