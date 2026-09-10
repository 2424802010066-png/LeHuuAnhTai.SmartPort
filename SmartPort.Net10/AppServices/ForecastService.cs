using System;
using System.Collections.Generic;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// AI DU BAO SAN LUONG: hoi quy tuyen tinh (Least Squares) tren chuoi
    /// san luong TEU theo thang de du bao cac thang tiep theo.
    /// y = a*x + b ; a,b tinh theo cong thuc binh phuong toi thieu.
    /// </summary>
    public static class ForecastService
    {
        public static ForecastResult Build(List<SanLuongThang> history, int horizon = 3)
        {
            var result = new ForecastResult
            {
                Points = new List<ForecastPoint>(),
                GrowthPercent = 0,
                NextMonthTEU = 0
            };
            if (history == null || history.Count < 3) return result;

            var ordered = history.OrderBy(h => h.Nam).ThenBy(h => h.Thang).ToList();

            // Hoi quy tuyen tinh: x = chi so thang (0..n-1), y = TEU
            int n = ordered.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                double x = i, y = ordered[i].TEU;
                sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x;
            }
            double denom = n * sumX2 - sumX * sumX;
            double slope = denom == 0 ? 0 : (n * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / n;
            result.Slope = Math.Round(slope, 1);

            // Diem lich su
            foreach (var h in ordered)
            {
                result.Points.Add(new ForecastPoint
                {
                    Label = "T" + h.Thang + "/" + (h.Nam % 100),
                    TEU = h.TEU,
                    IsForecast = false
                });
            }

            // Diem du bao
            int lastNam = ordered[n - 1].Nam, lastThang = ordered[n - 1].Thang;
            int lastTEU = ordered[n - 1].TEU;
            for (int k = 1; k <= horizon; k++)
            {
                lastThang++;
                if (lastThang > 12) { lastThang = 1; lastNam++; }
                int pred = (int)Math.Round(slope * (n - 1 + k) + intercept);
                if (pred < 0) pred = 0;
                var point = new ForecastPoint
                {
                    Label = "T" + lastThang + "/" + (lastNam % 100),
                    TEU = pred,
                    IsForecast = true
                };
                result.Points.Add(point);
                if (k == 1)
                {
                    result.NextMonthTEU = pred;
                    result.NextMonthLabel = "T" + lastThang + "/" + lastNam;
                    if (lastTEU > 0)
                        result.GrowthPercent = Math.Round((pred - lastTEU) * 100.0 / lastTEU, 1);
                }
            }
            return result;
        }
    }
}
