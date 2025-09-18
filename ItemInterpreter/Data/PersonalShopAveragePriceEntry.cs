using System;

namespace ItemInterpreter.Data
{
    public class PersonalShopAveragePriceEntry
    {
        public DateTime Date { get; set; }
        public int Section { get; set; }
        public int Index { get; set; }
        public double AveragePrice { get; set; }
        public int SaleCount { get; set; }
    }
}
