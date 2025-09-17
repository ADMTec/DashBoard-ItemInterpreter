using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using ItemInterpreter.UI.Charts;
using ItemInterpreter.UI.Configurador;
using ItemInterpreter.Logic;

namespace ItemInterpreter.UI.Dashboard
{
    public partial class DashboardWindow : Window
    {
        public class TrackedItemDisplay
        {
            public string Name { get; set; } = string.Empty;
            public int InventoryCount { get; set; }
            public int WarehouseCount { get; set; }
        }

        private readonly string _configPath = "tracked_items.json";
        private readonly string _connectionString = "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";

        private List<ItemDefinition> _itemDatabase = ItemXmlLoader.Load("IGC_ItemList.xml");
        private List<TrackedItem> _trackedItems = new();

        public DashboardWindow()
        {
            InitializeComponent();
            LoadTrackedItems();
            RefreshData();
        }

        private void LoadTrackedItems()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _trackedItems = JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new();
            }
        }

        private void RefreshData()
        {
            var dbReader = new DatabaseItemReader(_connectionString);
            var inventoryCounts = dbReader.ReadInventoryCounts();
            var warehouseCounts = dbReader.ReadWarehouseCounts();

            var displayList = _trackedItems.Select(pair =>
            {
                var item = _itemDatabase.FirstOrDefault(i => i.Section == pair.Section && i.Index == pair.Index);
                var key = (pair.Section, pair.Index);
                return new TrackedItemDisplay
                {
                    Name = item?.Name ?? $"ITEMGET({pair.Section},{pair.Index})",
                    InventoryCount = inventoryCounts.TryGetValue(key, out var inv) ? inv : 0,
                    WarehouseCount = warehouseCounts.TryGetValue(key, out var wh) ? wh : 0
                };
            }).ToList();


            ItemListView.ItemsSource = displayList;
        }

        private void Atualizar_Click(object sender, RoutedEventArgs e)
        {
            LoadTrackedItems();
            RefreshData();
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ItemTrackerConfigGrouped(_itemDatabase);
            bool? result = configWindow.ShowDialog();
            if (result == true)
            {
                LoadTrackedItems();
                RefreshData();
            }
        }

        private void RemoverItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemListView.SelectedItem is TrackedItemDisplay selectedDisplay)
            {
                // Converte o nome ITEMGET(x, y) ou procura pelo nome no banco de dados
                var match = _itemDatabase.FirstOrDefault(item => item.Name == selectedDisplay.Name);
                if (match != null)
                {
                    // Remove da lista de itens rastreados
                    _trackedItems = _trackedItems
                        .Where(t => !(t.Section == match.Section && t.Index == match.Index))
                        .ToList();

                    // Salva o arquivo atualizado
                    var json = JsonSerializer.Serialize(_trackedItems);
                    File.WriteAllText(_configPath, json);

                    // Atualiza visualmente
                    RefreshData();
                }
                else
                {
                    MessageBox.Show("Não foi possível identificar o item para remoção.");
                }
            }
        }

        private void AbrirGraficoZen_Click(object sender, RoutedEventArgs e)
        {
            new ZenChart().Show();
        }
    }
}