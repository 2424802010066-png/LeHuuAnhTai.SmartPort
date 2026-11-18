using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.Outbox
{
    /// <summary>Noi module "phat" su kien. Su kien chi thuc su di khi DbContext SaveChanges thanh cong.</summary>
    public interface IHopThuDi
    {
        void Them(SuKienTichHop suKien);
    }

    internal sealed class HopThuDi<TDb> : IHopThuDi where TDb : DbContext
    {
        private readonly TDb db;

        public HopThuDi(TDb db)
        {
            this.db = db;
        }

        public void Them(SuKienTichHop suKien)
        {
            db.Set<SuKienOutbox>().Add(HopThuDiTinh.TaoBanGhi(suKien));
        }
    }
}
