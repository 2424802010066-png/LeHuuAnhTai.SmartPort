using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPort.BuildingBlocks.SuKien
{
    /// <summary>
    /// SU KIEN TICH HOP (integration event): mot viec DA XAY RA trong mot module, phat cho
    /// cac module khac. Dat ten o thi qua khu (XeDaQuaCong, ContainerDaRoiBai...).
    /// Chi chua DU LIEU (khong chua doi tuong EF) de luu duoc thanh JSON trong Outbox.
    /// </summary>
    public abstract class SuKienTichHop
    {
        /// <summary>Ma duy nhat — dung de chong xu ly trung khi su kien duoc giao lai</summary>
        public Guid Ma { get; init; } = Guid.NewGuid();

        /// <summary>Thoi diem viec xay ra (gio may chu)</summary>
        public DateTime Luc { get; init; } = DateTime.Now;

        /// <summary>Khoa gom cac su kien cung mot nghiep vu, vd "xe:221"</summary>
        [JsonIgnore]
        public virtual string MaTuongQuan => null;
    }

    /// <summary>Ten loai su kien tren day (Outbox, luong realtime) va module phat ra no</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LoaiSuKienAttribute : Attribute
    {
        public LoaiSuKienAttribute(string ten, string module)
        {
            Ten = ten;
            Module = module;
        }

        /// <summary>vd "cong.xe-da-qua-cong" — tien to la module so huu su kien</summary>
        public string Ten { get; }

        /// <summary>vd "Cổng"</summary>
        public string Module { get; }
    }

    public static class ChuanJson
    {
        /// <summary>camelCase, giu nguyen tieng Viet (khong ma hoa \uXXXX) de doc duoc trong CSDL</summary>
        public static readonly JsonSerializerOptions TuyChon = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
