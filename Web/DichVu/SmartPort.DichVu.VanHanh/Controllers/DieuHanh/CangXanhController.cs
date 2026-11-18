using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// CẢNG XANH · ESG (thay màn hình MVC Admin/Dashboard/CangXanh): lợi ích môi trường của số hóa tính từ dữ liệu THẬT,
    /// quy ra tiền tiết kiệm, CO₂, giấy, dầu — kèm hệ số để giải trình "tính thế nào".
    /// </summary>
    [Route("api/cang-xanh")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class CangXanhController : ApiGocController
    {
        // ---- He so quy doi (giu nguyen ban MVC) ----
        private const int TO_MOI_LENH = 4;                 // lenh dien tu thay bo EIR giay 4 lien
        private const int TO_MOI_CHUNG_TU = 2;             // moi chung tu so thay ~2 to giay
        private const int PHUT_MOI_CHECK_IN = 7;           // check-in truoc bot ~7' no may cho cong (Cat Lai 13' -> 6')
        private const double GRAM_CO2_MOI_TO = 4.7;        // 1 to giay ~ 4,7 g CO2
        private const double KG_CO2_MOI_PHUT_CHO = 0.093;  // 1 phut xe idle ~ 0,093 kg CO2
        private const double LIT_DAU_MOI_PHUT_CHO = 0.035; // 1 phut xe idle ~ 0,035 lit dau
        private const decimal GIA_MOT_TO = 500m;           // giay A4 + muc in + cong luu ho so ~500 d/to
        private const decimal GIA_DAU_LIT = 20000m;        // dau diesel ~20.000 d/lit (2026)

        private readonly AppDb db;

        public CangXanhController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tiền tiết kiệm, CO₂ cắt giảm, tờ giấy bớt in, dầu bớt đốt, tỷ lệ lượt xe không giấy và các hệ số quy đổi.</summary>
        [HttpGet]
        public ActionResult<CangXanhDto> Lay()
        {
            var soLenh = db.LenhGiaoNhans.Count();
            var soCT = db.ChungTus.Count();
            var soCheckIn = db.LenhGiaoNhans.Count(l => l.DaCheckIn);
            var soXe = db.XeRaVaos.Count();

            var toGiay = soLenh * TO_MOI_LENH + soCT * TO_MOI_CHUNG_TU;
            var phutIdle = soCheckIn * PHUT_MOI_CHECK_IN;
            var co2Giay = toGiay * GRAM_CO2_MOI_TO / 1000.0;                 // kg
            var co2Idle = phutIdle * KG_CO2_MOI_PHUT_CHO;                     // kg
            var litDau = Math.Round(phutIdle * LIT_DAU_MOI_PHUT_CHO, 1);

            // Doanh nghiep hoi "xanh thi minh duoc gi" truoc khi hoi "giam bao nhieu CO2"
            var tienGiay = toGiay * GIA_MOT_TO;
            var tienDau = (decimal)litDau * GIA_DAU_LIT;

            return new CangXanhDto
            {
                ThoiDiem = DateTime.Now,
                SoLenh = soLenh,
                SoChungTu = soCT,
                SoCheckIn = soCheckIn,
                SoXe = soXe,
                ToGiay = toGiay,
                PhutIdle = phutIdle,
                LitDau = litDau,
                Co2Kg = Math.Round(co2Giay + co2Idle, 1),
                Co2GiayKg = Math.Round(co2Giay, 1),
                Co2XeChoKg = Math.Round(co2Idle, 1),
                TienTietKiem = Math.Round((tienGiay + tienDau) / 1000) * 1000,
                TienGiay = tienGiay,
                TienDau = tienDau,
                TyLeDienTu = soXe > 0 ? Math.Min(100, (int)Math.Round(100.0 * soLenh / soXe)) : 0,
                HeSo = new HeSoCangXanhDto
                {
                    ToMoiLenh = TO_MOI_LENH,
                    ToMoiChungTu = TO_MOI_CHUNG_TU,
                    PhutMoiCheckIn = PHUT_MOI_CHECK_IN,
                    GramCo2MoiTo = GRAM_CO2_MOI_TO,
                    KgCo2MoiPhutCho = KG_CO2_MOI_PHUT_CHO,
                    LitDauMoiPhutCho = LIT_DAU_MOI_PHUT_CHO,
                    GiaMotTo = GIA_MOT_TO,
                    GiaDauLit = GIA_DAU_LIT
                }
            };
        }
    }

    public class CangXanhDto
    {
        public DateTime ThoiDiem { get; set; }
        public int SoLenh { get; set; }
        public int SoChungTu { get; set; }
        public int SoCheckIn { get; set; }
        public int SoXe { get; set; }
        /// <summary>To giay bot in = lenh x 4 + chung tu x 2</summary>
        public int ToGiay { get; set; }
        /// <summary>Phut xe bot no may cho cong = check-in x 7</summary>
        public int PhutIdle { get; set; }
        public double LitDau { get; set; }
        public double Co2Kg { get; set; }
        public double Co2GiayKg { get; set; }
        public double Co2XeChoKg { get; set; }
        /// <summary>Lam tron nghin dong</summary>
        public decimal TienTietKiem { get; set; }
        public decimal TienGiay { get; set; }
        public decimal TienDau { get; set; }
        /// <summary>% luot xe co lenh dien tu (khong giay), toi da 100</summary>
        public int TyLeDienTu { get; set; }
        public HeSoCangXanhDto HeSo { get; set; }
    }

    public class HeSoCangXanhDto
    {
        public int ToMoiLenh { get; set; }
        public int ToMoiChungTu { get; set; }
        public int PhutMoiCheckIn { get; set; }
        public double GramCo2MoiTo { get; set; }
        public double KgCo2MoiPhutCho { get; set; }
        public double LitDauMoiPhutCho { get; set; }
        public decimal GiaMotTo { get; set; }
        public decimal GiaDauLit { get; set; }
    }
}
