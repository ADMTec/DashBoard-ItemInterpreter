using System;

namespace ItemInterpreter.Data
{
    public class ItemSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int Section { get; set; }
        public int Index { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int InventoryCount { get; set; }
        public int WarehouseCount { get; set; }

        public int TotalCount => InventoryCount + WarehouseCount;
    }
}
