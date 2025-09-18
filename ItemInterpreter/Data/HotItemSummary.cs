using System;

namespace ItemInterpreter.Data
{
    public class HotItemSummary
    {
        public int Section { get; set; }
        public int Index { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int TotalSales { get; set; }
        public double AveragePrice { get; set; }
        public double? PreviousAveragePrice { get; set; }
        public double? PriceStandardDeviation { get; set; }
        public double PriceChangePercent { get; set; }
        public bool IsOutlier { get; set; }
        public TimeSpan Window { get; set; }
    }
}
