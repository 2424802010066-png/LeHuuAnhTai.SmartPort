using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// SO HUU DU LIEU (database-per-service): moi bang chi co MOT dich vu duoc ghi. Dich vu khac can doc thi giu
    /// BAN SAO chi-doc, cap nhat qua su kien "du-lieu.&lt;bang&gt;" (event-carried state transfer).
    ///   SoHuu  : bang dich vu nay duoc ghi
    ///   Phat   : bang so huu ma dich vu khac dang sao chep -> moi thay doi phat su kien
    ///   BanSao : bang chi-doc nhan tu dich vu khac (buoc hang doi RabbitMQ "du-lieu.&lt;bang&gt;")
    ///   CucBo  : bang moi dich vu tu giu rieng (nhat ky, Outbox, inbox) — ghi tu do, khong sao chep
    /// Che do GOP (1 tien trinh, 1 CSDL — ban chay tren somee): Bat = false, khong kiem tra, khong phat.
    /// </summary>
    public static class SoHuuDuLieu
    {
        private static HashSet<string> soHuu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> phat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> banSao = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> cucBo = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SuKienOutbox", "SuKienDaXuLy" };
        private static Dictionary<string, string> chuSoHuu = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool Bat { get; private set; }
        public static string TenDichVu { get; private set; } = "gop";

        public static IReadOnlyCollection<string> BangSoHuu => soHuu;
        public static IReadOnlyCollection<string> BangPhat => phat;
        public static IReadOnlyCollection<string> BangBanSao => banSao;

        /// <summary>Nap tu tep dich-vu.json cho dich vu "tenDichVu" (che do tach)</summary>
        public static void Nap(DanhMucDichVu danhMuc, string tenDichVu)
        {
            var dv = danhMuc.Tim(tenDichVu) ?? throw new InvalidOperationException("Không có dịch vụ \"" + tenDichVu + "\" trong dich-vu.json.");
            TenDichVu = dv.Ten;
            soHuu = new HashSet<string>(dv.SoHuu ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            banSao = new HashSet<string>(dv.BanSao ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            // bang cuc bo = danh sach chung (moi dich vu) + danh sach RIENG cua dich vu nay (vd cong: LuotXeNgoaiTuyen)
            cucBo = new HashSet<string>(danhMuc.BangCucBoCua(dv), StringComparer.OrdinalIgnoreCase);
            chuSoHuu = danhMuc.DichVu.SelectMany(d => (d.SoHuu ?? new List<string>()).Select(b => (b, d.Ten)))
                                     .GroupBy(x => x.b, StringComparer.OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First().Ten, StringComparer.OrdinalIgnoreCase);
            // chi phat bang ma IT NHAT MOT dich vu khac giu ban sao -> khong ton tin nhan vo ich
            var duocSaoChep = new HashSet<string>(danhMuc.DichVu.Where(d => d.Ten != dv.Ten).SelectMany(d => d.BanSao ?? new List<string>()),
                                                  StringComparer.OrdinalIgnoreCase);
            phat = new HashSet<string>(soHuu.Where(duocSaoChep.Contains), StringComparer.OrdinalIgnoreCase);
            Bat = true;
        }

        /// <summary>Dung cho kiem thu: tat kiem tra (ve che do gop)</summary>
        public static void Tat()
        {
            Bat = false;
            TenDichVu = "gop";
        }

        public static bool DuocGhi(string bang) => !Bat || soHuu.Contains(bang) || cucBo.Contains(bang);

        public static bool CanPhat(string bang) => Bat && phat.Contains(bang);

        /// <summary>Bang dich vu nay chi giu BAN SAO (chi doc) — moi lenh ghi vao day deu bi chan</summary>
        public static bool LaBanSao(string bang) => Bat && bang != null && banSao.Contains(bang) && !soHuu.Contains(bang);

        public static string ChuCua(string bang) => chuSoHuu.TryGetValue(bang ?? "", out var t) ? t : "(chưa khai báo)";
    }

    /// <summary>Mo ta 1 dich vu trong dich-vu.json (dung chung cho dich vu, cong API gateway va script dung CSDL)</summary>
    public sealed class MoTaDichVu
    {
        public string Ten { get; set; }
        public string TenHienThi { get; set; }
        public int Cong { get; set; }
        public string CoSoDuLieu { get; set; }
        public List<string> DuongDan { get; set; }
        public List<string> SoHuu { get; set; }
        public List<string> BanSao { get; set; }
        public string MoTa { get; set; }
        /// <summary>"dotnet" (mac dinh) hoac "python" (dich vu AI chay rieng)</summary>
        public string NenTang { get; set; }

        /// <summary>Bang CUC BO chi dich vu nay co (khong sao chep, tao bang script Database/Add_&lt;Bang&gt;.sql), vd cong: LuotXeNgoaiTuyen</summary>
        public List<string> BangCucBoRieng { get; set; }

        /// <summary>Gateway: cat yeu cau toi dich vu nay sau bao nhieu giay khong co hoat dong (null = mac dinh cua gateway)</summary>
        public int? ThoiGianChoGiay { get; set; }
    }

    /// <summary>Muc "gateway" trong dich-vu.json: cong, thoi gian cho mac dinh, cac tuyen dac biet (SSE, tai video...)</summary>
    public sealed class CauHinhGateway
    {
        public int Cong { get; set; } = 5100;

        /// <summary>Thoi gian toi da mot yeu cau "dung im" (khong nhan / gui du lieu) truoc khi gateway cat va tra 504</summary>
        public int ThoiGianChoGiay { get; set; } = 30;

        public List<TuyenDacBiet> TuyenDacBiet { get; set; } = new List<TuyenDacBiet>();
    }

    /// <summary>Tuyen co thoi gian cho rieng (vd /api/su-kien SSE: 0 = khong cat; tai video AI: 600 giay)</summary>
    public sealed class TuyenDacBiet
    {
        public string Ten { get; set; }
        public string DichVu { get; set; }
        public string DuongDan { get; set; }
        /// <summary>true: chi dung DUNG duong dan (khong gom duong dan con)</summary>
        public bool KhopChinhXac { get; set; }
        public List<string> PhuongThuc { get; set; }
        /// <summary>0 = khong cat (luong SSE giu ket noi lau)</summary>
        public int ThoiGianChoGiay { get; set; }
        public string GhiChu { get; set; }
    }

    public sealed class DanhMucDichVu
    {
        /// <summary>Bang cuc bo MOI dich vu deu co, ke ca khi dich-vu.json khong ghi</summary>
        public static readonly string[] BangCucBoLuonCo = { "SuKienOutbox", "SuKienDaXuLy" };

        public List<MoTaDichVu> DichVu { get; set; } = new List<MoTaDichVu>();
        public List<string> BangCucBo { get; set; } = new List<string>();
        public string CoSoDuLieuNguon { get; set; }
        public CauHinhGateway Gateway { get; set; }

        public MoTaDichVu Tim(string ten) => DichVu.FirstOrDefault(d => string.Equals(d.Ten, ten, StringComparison.OrdinalIgnoreCase));

        /// <summary>Bang cuc bo cua MOT dich vu = danh sach chung + Outbox/Inbox + danh sach rieng cua dich vu do</summary>
        public IEnumerable<string> BangCucBoCua(MoTaDichVu dv) =>
            (BangCucBo ?? new List<string>()).Concat(BangCucBoLuonCo).Concat(dv?.BangCucBoRieng ?? new List<string>())
                                             .Distinct(StringComparer.OrdinalIgnoreCase);

        /// <summary>Moi bang cuc bo cua MOI dich vu (dung cho kiem thu "bang nao cung co chu")</summary>
        public IEnumerable<string> TatCaBangCucBo() =>
            (BangCucBo ?? new List<string>()).Concat(BangCucBoLuonCo)
                .Concat(DichVu.SelectMany(d => d.BangCucBoRieng ?? new List<string>()))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        public static DanhMucDichVu Doc(string duongDan)
        {
            return JsonSerializer.Deserialize<DanhMucDichVu>(System.IO.File.ReadAllText(duongDan),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
    }
}
