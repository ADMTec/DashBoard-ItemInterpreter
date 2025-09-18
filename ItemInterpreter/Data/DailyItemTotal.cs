using System;

namespace ItemInterpreter.Data
{
    public class DailyItemTotal
    {
        public DateTime Date { get; set; }
        public int Section { get; set; }
        public int Index { get; set; }
        public int TotalCount { get; set; }
    }
}
