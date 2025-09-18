using System;

namespace ItemInterpreter.Data
{
    public class PersonalShopSaleEntry
    {
        public long ItemSerial { get; set; }
        public int Section { get; set; }
        public int Index { get; set; }
        public long PriceZen { get; set; }
        public long? AlternativePrice { get; set; }
        public string Buyer { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double? AveragePriceWindow { get; set; }
        public int WindowSaleCount { get; set; }
        public string ItemName { get; set; } = string.Empty;
    }
}
