namespace SmartPort.Api.Dtos
{
    /// <summary>Ket qua mot thao tac GHI (them / sua / xoa / duyet...): thong bao cho nguoi dung + ma ban ghi.</summary>
    public class ThaoTacDto
    {
        public string ThongBao { get; set; }
        public int? Id { get; set; }
        /// <summary>Du lieu kem theo neu man hinh can (vd dong vua luu) — co the null</summary>
        public object DuLieu { get; set; }
    }

    /// <summary>Mot lua chon cho o chon (select): gia tri gui len + chu hien thi</summary>
    public class LuaChonDto
    {
        public string GiaTri { get; set; }
        public string Nhan { get; set; }

        public LuaChonDto() { }
        public LuaChonDto(object giaTri, string nhan)
        {
            GiaTri = giaTri == null ? null : giaTri.ToString();
            Nhan = nhan;
        }
    }
}
