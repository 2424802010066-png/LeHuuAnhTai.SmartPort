using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Modules.ThongBao
{
    /// <summary>Mot su kien da "dich" san cho giao dien: noi dung goc + id, loai, module, cau mo ta</summary>
    public sealed class BanTinSuKien
    {
        private BanTinSuKien(long id, string loai, string module, string json)
        {
            Id = id;
            Loai = loai;
            Module = module;
            Json = json;
        }

        /// <summary>Id trong Outbox — tang dan, dung lam "id:" cua Server-Sent Events</summary>
        public long Id { get; }
        public string Loai { get; }
        public string Module { get; }

        /// <summary>JSON gui cho giao dien (camelCase)</summary>
        public string Json { get; }

        public static BanTinSuKien Tu(BanGhiSuKien bg)
        {
            JsonObject doiTuong;
            try
            {
                doiTuong = JsonNode.Parse(bg.NoiDung) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                doiTuong = new JsonObject();
            }
            doiTuong["id"] = bg.Id;
            doiTuong["loai"] = bg.Loai;
            doiTuong["module"] = bg.Module;
            doiTuong["moTa"] = DinhDangSuKien.MoTa(bg.Loai, doiTuong);
            return new BanTinSuKien(bg.Id, bg.Loai, bg.Module, doiTuong.ToJsonString(ChuanJson.TuyChon));
        }
    }

    /// <summary>Dich su kien thanh cau tieng Viet ngan cho bang tin / man hinh cong</summary>
    public static class DinhDangSuKien
    {
        private static readonly CultureInfo vi = new CultureInfo("vi-VN");

        public static string MoTa(string loai, JsonObject d)
        {
            string S(string k) => d[k] is JsonValue v && v.TryGetValue(out string s) ? s : null;
            decimal N(string k) => d[k] is JsonValue v && v.TryGetValue(out decimal n) ? n : 0m;
            bool B(string k) => d[k] is JsonValue v && v.TryGetValue(out bool b) && b;
            string Tien(decimal x) => x.ToString("N0", vi) + "đ";
            // Gio trong JSON su kien (DateTime, gio may chu) -> "HH:mm dd/MM"; doc khong duoc thi giu nguyen chuoi
            string Gio(string k)
            {
                var s = S(k);
                return s != null && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
                    ? t.ToString("HH:mm dd/MM", vi) : s;
            }

            switch (loai)
            {
                case "cong.xe-da-qua-cong":
                    return "Xe " + S("bienSo") + (S("huongDi") == "Ra" ? " ra" : " vào") + " cổng " + S("cong")
                           + (S("dock") != null ? " · dock " + S("dock") : "")
                           + (S("soLenh") != null ? " · lệnh " + S("soLenh") : "")
                           + (S("soCont") != null ? " · cont " + S("soCont") : "")
                           + (S("nguon") == "OFFLINE" ? " · nhập bù khi mất mạng"
                              : B("laGhiTre") ? " · ghi trễ khi mất mạng (lúc " + Gio("luc") + ")" : "")
                           + (N("tienMatTaiCong") > 0
                               ? " · đã thu tại cổng " + Tien(N("tienMatTaiCong")) + (S("soPhieuThu") != null ? " (phiếu " + S("soPhieuThu") + ")" : "")
                               : "");
                case "cong.luot-xe-can-doi-soat":
                    // Bang tin "Cần đối soát": luot ghi tre vi pham tuan thu / xung dot — xe da qua that, can nguoi xu ly
                    return "⚠ Cần đối soát: xe " + S("bienSo") + (S("huongDi") == "Ra" ? " ra" : " vào") + " cổng"
                           + (S("cong") != null ? " " + S("cong") : "") + " lúc " + Gio("lucXayRa")
                           + (N("maXV") > 0 ? " (lượt #" + N("maXV").ToString("0", vi) + ")" : "")
                           + " — " + (S("lyDo") ?? "lượt ghi trễ khi mất mạng cần kiểm tra");
                case "bai.container-da-vao-bai":
                    return "Container " + S("soCont") + " vào bãi — bắt đầu tính lưu bãi";
                case "bai.container-da-roi-bai":
                    return "Container " + S("soCont") + " rời bãi sau " + N("soNgayLuu").ToString("N0", vi) + " ngày (" + S("trangThai") + ")";
                case "bai.container-khong-doi":
                    return S("lyDo") ?? ("Container " + S("soCont") + " không đổi trạng thái");
                case "tai-chinh.phi-cong-da-xu-ly":
                    return B("coThu")
                        ? "Phí cổng " + Tien(N("soTien")) + " (" + S("hinhThuc") + ")" + (B("canhBao") ? " — " + S("thongBao") : "")
                        : "Không thu phí cổng — " + S("thongBao");
                case "tai-chinh.phi-luu-bai-da-chot":
                    if (S("soPhieu") == null) return S("ghiChu") ?? "Không phát sinh phí lưu bãi";
                    return (B("phieuDaCoTruoc") ? "Phí lưu bãi đã có phiếu " : "Lập phiếu ") + S("soPhieu") + " · "
                           + Tien(N("tongTien")) + " · " + N("tongNgay").ToString("N0", vi) + " ngày";
                case "an-ninh.su-co-da-bao":
                    return (S("mucDo") == "Nguy hiểm" ? "⚠ NGUY HIỂM: " : "Chú ý: ") + (S("moTa") ?? S("loaiSuCo"))
                           + " — " + S("viTri") + " (sự cố #" + N("maSC").ToString("0", vi) + ")";
                default:
                    return loai;
            }
        }
    }
}
