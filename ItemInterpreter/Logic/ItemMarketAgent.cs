using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;

namespace ItemInterpreter.Logic
{
    public class ItemMarketAgent
    {
        private readonly System.Timers.Timer _timer;
        private readonly string _connectionString = "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";

        public ItemMarketAgent()
        {
            _timer = new System.Timers.Timer(3600000); // a cada 1 hora
            _timer.Elapsed += (s, e) => RegistrarDados();
            _timer.Start();
        }

        public void RegistrarDados()
        {
            var reader = new DatabaseItemReader(_connectionString);
            var warehouse = reader.ReadWarehouseCounts();
            var inventory = reader.ReadInventoryCounts();
            var totalZenWarehouse = reader.ReadTotalZenWarehouse();
            var totalZenInventory = reader.ReadTotalZenInventory();

            var dataZen = new ZenTrackingLog
            {
                Date = DateTime.Now,
                TotalZenInventory = totalZenInventory,
                TotalZenWarehouse = totalZenWarehouse
            };

            var tracked = JsonSerializer.Deserialize<List<TrackedItem>>(File.ReadAllText("tracked_items.json")) ?? new();
            var historico = new List<ItemTrackingLog>();

            foreach (var item in tracked)
            {
                historico.Add(new ItemTrackingLog
                {
                    ItemName = $"ITEMGET({item.Section},{item.Index})",
                    Section = item.Section,
                    Index = item.Index,
                    Date = DateTime.Now,
                    Warehouse = warehouse.GetValueOrDefault((item.Section, item.Index)),
                    Inventory = inventory.GetValueOrDefault((item.Section, item.Index)),
                });
            }

            Salvar("zen_history.json", dataZen);
            Salvar("tracked_history.json", historico);
        }

        private void Salvar<T>(string path, T entrada)
        {
            List<T> dados = File.Exists(path)
                ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? new()
                : new();

            dados.Add(entrada);
            File.WriteAllText(path, JsonSerializer.Serialize(dados));
        }
    }
}
