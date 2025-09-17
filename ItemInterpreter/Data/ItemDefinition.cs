using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ItemInterpreter.Data
{
    public class ItemDefinition
    {
        public int Section { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Slot { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Type { get; set; }
        public bool Excellent { get; set; }
        public bool CanBeSold { get; set; }
        public bool CanBeStored { get; set; }
        public bool Repairable { get; set; }

        public Dictionary<string, int> Requirements { get; set; } = new();
    }
}
