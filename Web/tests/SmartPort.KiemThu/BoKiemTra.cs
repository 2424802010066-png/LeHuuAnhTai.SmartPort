using System;

namespace SmartPort.KiemThu
{
    public sealed class BoKiemTra
    {
        private int dat;
        private int loi;

        public void Nhom(string ten)
        {
            Console.WriteLine();
            Console.WriteLine("== " + ten + " ==");
        }

        public void Kiem(string ten, bool dieuKien, string chiTiet = null)
        {
            if (dieuKien)
            {
                dat++;
                Console.WriteLine("  OK  " + ten);
            }
            else
            {
                loi++;
                Console.WriteLine("  LOI " + ten + (chiTiet != null ? "  -> " + chiTiet : ""));
            }
        }

        public void Ghi(string noiDung) => Console.WriteLine("       " + noiDung);

        public int InKetQua()
        {
            Console.WriteLine();
            Console.WriteLine(loi == 0 ? "KET QUA: DAT " + dat + "/" + dat : "KET QUA: " + loi + " LOI / " + (dat + loi));
            return loi == 0 ? 0 : 1;
        }
    }
}
