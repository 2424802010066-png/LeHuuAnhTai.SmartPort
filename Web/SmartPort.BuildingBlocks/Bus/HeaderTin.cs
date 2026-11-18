using System.Text;
using RabbitMQ.Client;

namespace SmartPort.BuildingBlocks.Bus
{
    /// <summary>Doc header tin RabbitMQ (gia tri co the la int / long / byte[] tuy cach gui)</summary>
    public static class HeaderTin
    {
        public static long DocSoLong(IReadOnlyBasicProperties tt, string ten)
        {
            if (tt?.Headers == null || !tt.Headers.TryGetValue(ten, out var v) || v == null) return 0;
            return v switch
            {
                int i => i,
                long l => l,
                short s => s,
                byte b => b,
                byte[] bs when long.TryParse(Encoding.UTF8.GetString(bs), out var n) => n,
                _ => long.TryParse(v.ToString(), out var m) ? m : 0
            };
        }

        public static string DocChuoi(IReadOnlyBasicProperties tt, string ten)
        {
            if (tt?.Headers == null || !tt.Headers.TryGetValue(ten, out var v) || v == null) return null;
            return v is byte[] b ? Encoding.UTF8.GetString(b) : v.ToString();
        }
    }
}
