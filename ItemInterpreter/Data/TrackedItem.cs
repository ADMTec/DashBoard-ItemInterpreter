using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemInterpreter.Data
{
    public class TrackedItem
    {
        public int Section { get; set; }
        public int Index { get; set; }

        // ✅ Adicione esta propriedade
        public string ItemName { get; set; } = string.Empty;
    }
}
