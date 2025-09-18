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

        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Quantidade mínima desejada para manter o estoque saudável.
        /// </summary>
        public int? MinimumTarget { get; set; }

        /// <summary>
        /// Quantidade máxima aceitável antes de sinalizar excesso.
        /// </summary>
        public int? MaximumTarget { get; set; }

        /// <summary>
        /// Preço médio de aquisição do item para cálculos de custo.
        /// </summary>
        public decimal? PurchasePrice { get; set; }

        /// <summary>
        /// Preço médio de venda estimado para o item.
        /// </summary>
        public decimal? SalePrice { get; set; }
    }
}
