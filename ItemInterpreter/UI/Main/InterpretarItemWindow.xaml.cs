using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ItemInterpreter.UI.Main
{
    /// <summary>
    /// Lógica interna para InterpretarItemWindow.xaml
    /// </summary>
    public partial class InterpretarItemWindow : Window
    {
        public InterpretarItemWindow()
        {
            InitializeComponent();
            _itemDatabase = ItemXmlLoader.Load("IGC_ItemList.xml");
            _excellentOptions = ExcellentOptionsXmlLoader.Load("IGC_ExcellentOptions.xml");
        }


        private List<ItemDefinition> _itemDatabase;
        private List<ExcellentOptionDefinition> _excellentOptions;

        private void InterpretItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] itemBytes = HexInputTextBox.Text
                    .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => Convert.ToByte(h, 16))
                    .ToArray();

                if (itemBytes.Length != 32)
                {
                    ResultTextBlock.Text = "O item precisa ter exatamente 32 bytes.";
                    return;
                }

                var sb = new StringBuilder();

                // Informações básicas
                byte itemIndex = itemBytes[0];
                byte packedOptions = itemBytes[1];
                byte durability = itemBytes[2];
                byte typeGroup = (byte)(itemBytes[9] >> 4); // Byte 9 = tipo * 16
                int itemId = (typeGroup * 512) + itemIndex;

                // Correto: nível usa apenas 4 bits
                int level = (packedOptions >> 3) & 0x0F;

                // Option flags
                bool hasSkill = (packedOptions & 0x80) != 0;
                bool hasLuck = (packedOptions & 0x04) != 0;

                // Option adicional
                int optionRaw = itemBytes[1] & 0x03; // +0, +4, +8, +12
                int optionValue = optionRaw * 4;

                // Bit 6 do byte 07 → flag de +16 adicional
                bool hasPlus16 = (itemBytes[7] & 0x40) != 0;
                if (hasPlus16)
                    optionValue += 16;
                byte excellentFlags = (byte)(itemBytes[7] & 0x3F);


                // Serial completo (bytes 03–06 e 16–19)
                uint serialHigh = (uint)((itemBytes[3] << 24) | (itemBytes[4] << 16) | (itemBytes[5] << 8) | itemBytes[6]);
                uint serialLow = (uint)((itemBytes[16] << 24) | (itemBytes[17] << 16) | (itemBytes[18] << 8) | itemBytes[19]);
                ulong fullSerial = ((ulong)serialHigh << 32) | serialLow;

                // 🔎 Busca o item no XML
                var itemDef = _itemDatabase.FirstOrDefault(i => i.Section == typeGroup && i.Index == itemIndex);

                // Exibição
                sb.AppendLine($"📦 Tipo (Group): {typeGroup}");
                sb.AppendLine($"🔢 Índice (Index): {itemIndex}");
                sb.AppendLine($"🆔 ID MuOnline: ITEMGET({typeGroup}, {itemIndex}) = {itemId}");
                sb.AppendLine($"🔧 Level: {level}");
                sb.AppendLine($"🛡️ Durabilidade: {durability}");
                sb.AppendLine($"✨ Skill: {(hasSkill ? "Sim" : "Não")}");
                sb.AppendLine($"🍀 Luck: {(hasLuck ? "Sim" : "Não")}");
                sb.AppendLine($"🔸 Option (+): +{optionValue}");

                sb.AppendLine($"🔐 Serial: {serialHigh:X8}-{serialLow:X8} (decimal: {fullSerial})");

                sb.AppendLine("🧩 Sockets:");
                for (int i = 11; i <= 15; i++)
                {
                    sb.AppendLine($"  Slot {i - 10}: {(itemBytes[i] == 0xFF ? "Vazio" : itemBytes[i].ToString("X2"))}");
                }
                // 📛 Mostra dados do XML, se encontrar
                if (itemDef != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"📛 Nome do Item: {itemDef.Name}");
                    sb.AppendLine($"📐 Tamanho: {itemDef.Width}x{itemDef.Height}");
                    sb.AppendLine($"🎒 Slot: {itemDef.Slot}");
                    sb.AppendLine($"🔧 Reparável: {(itemDef.Repairable ? "Sim" : "Não")}");
                    sb.AppendLine($"🏪 Armazenável: {(itemDef.CanBeStored ? "Sim" : "Não")}");
                    sb.AppendLine($"🛒 Vendável: {(itemDef.CanBeSold ? "Sim" : "Não")}");
                    sb.AppendLine($"📈 Requisitos:");
                    sb.AppendLine($" - Level: {itemDef.Requirements["Level"]}");
                    sb.AppendLine($" - Força: {itemDef.Requirements["Strength"]}");
                    sb.AppendLine($" - Agilidade: {itemDef.Requirements["Dexterity"]}");
                    sb.AppendLine($" - Energia: {itemDef.Requirements["Energy"]}");
                    sb.AppendLine($" - Vitalidade: {itemDef.Requirements["Vitality"]}");
                    sb.AppendLine($" - Comando: {itemDef.Requirements["Command"]}");
                }
                else
                {
                    sb.AppendLine("❌ Item não encontrado no XML.");
                }

                // 🧪 Exibição das opções Excellent
                if (excellentFlags > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("💎 Opções Excellent:");

                    for (int i = 0; i < 6; i++)
                    {
                        if ((excellentFlags & (1 << i)) != 0)
                        {
                            var option = _excellentOptions.FirstOrDefault(o =>
                                o.ItemType == typeGroup &&
                                o.Index == itemIndex &&
                                o.Kind == i);

                            if (option != null)
                                sb.AppendLine($" - {option.Name}");
                            else
                                sb.AppendLine($" - Opção {i + 1} (não definida no XML)");
                        }
                    }
                }


                ResultTextBlock.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                ResultTextBlock.Text = $"❌ Erro ao interpretar: {ex.Message}";
            }


        }
    }
}
