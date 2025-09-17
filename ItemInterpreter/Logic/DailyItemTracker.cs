// DailyItemTracker.cs - com suporte ao rastreamento de Zen

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using Microsoft.Data.SqlClient;

namespace ItemInterpreter.Logic
{
    public static class DailyItemTracker
    {
        private static readonly string ConnectionString = "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";
        private static readonly string ConfigPath = "tracked_items.json";
        private static readonly string HistoryPath = "item_history.json";
        private static readonly string ZenHistoryPath = "zen_history.json";

        public static void RegistrarContagemDiaria()
        {
            if (!File.Exists(ConfigPath))
                return;

            var trackedItems = JsonSerializer.Deserialize<List<TrackedItem>>(File.ReadAllText(ConfigPath)) ?? new();
            var historico = File.Exists(HistoryPath)
                ? JsonSerializer.Deserialize<List<ItemSnapshot>>(File.ReadAllText(HistoryPath)) ?? new()
                : new List<ItemSnapshot>();

            var hoje = DateTime.Today;

            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            // 🔹 Gravar histórico de itens apenas se ainda não existir para o dia
            if (!historico.Any(h => h.Timestamp.Date == hoje))
            {
                foreach (var item in trackedItems)
                {
                    int warehouseCount = ContarItem(conn, "Warehouse", "Items", item.Section, item.Index);
                    int inventoryCount = ContarItem(conn, "Character", "Inventory", item.Section, item.Index);

                    historico.Add(new ItemSnapshot
                    {
                        Section = item.Section,
                        Index = item.Index,
                        Timestamp = hoje,
                        WarehouseCount = warehouseCount,
                        InventoryCount = inventoryCount,
                        ItemName = ObterNomeDoItem(item.Section, item.Index)
                    });
                }

                File.WriteAllText(HistoryPath, JsonSerializer.Serialize(historico, new JsonSerializerOptions { WriteIndented = true }));
            }

            // 🔹 Sempre gravar Zen (independente do histórico de itens)
            var historicoZen = File.Exists(ZenHistoryPath)
                ? JsonSerializer.Deserialize<List<ZenTrackingLog>>(File.ReadAllText(ZenHistoryPath)) ?? new()
                : new List<ZenTrackingLog>();

            if (!historicoZen.Any(z => z.Date == hoje)) // evita duplicação de Zen também
            {
                long totalZenWarehouse = ObterZen(conn, "Warehouse");
                long totalZenInventory = ObterZen(conn, "Character");

                historicoZen.Add(new ZenTrackingLog
                {
                    Date = hoje,
                    TotalZenWarehouse = totalZenWarehouse,
                    TotalZenInventory = totalZenInventory
                });

                File.WriteAllText(ZenHistoryPath, JsonSerializer.Serialize(historicoZen, new JsonSerializerOptions { WriteIndented = true }));
            }
        }


        private static int ContarItem(SqlConnection conn, string tabela, string campo, int section, int index)
        {
            int total = 0;
            using var cmd = new SqlCommand($"SELECT {campo} FROM {tabela}", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader[campo] is byte[] blob && blob.Length % 32 == 0)
                {
                    for (int i = 0; i < blob.Length; i += 32)
                    {
                        byte itemIndex = blob[i];
                        byte group = (byte)(blob[i + 9] >> 4);
                        if (itemIndex == index && group == section)
                            total++;
                    }
                }
            }
            return total;
        }

        private static string ObterNomeDoItem(int section, int index)
        {
            try
            {
                var allItems = ItemXmlLoader.Load("IGC_ItemList.xml");
                return allItems.FirstOrDefault(i => i.Section == section && i.Index == index)?.Name ?? $"ITEMGET({section},{index})";
            }
            catch
            {
                return $"ITEMGET({section},{index})";
            }
        }

        private static long ObterZen(SqlConnection conn, string tabela)
        {
            using var cmd = new SqlCommand($"SELECT SUM(CAST(Money AS BIGINT)) FROM {tabela}", conn);
            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }

    }
}
