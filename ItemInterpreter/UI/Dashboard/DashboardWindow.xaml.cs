using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using ItemInterpreter.Logic;
using ItemInterpreter.UI.Charts;
using ItemInterpreter.UI.Configurador;
using Microsoft.Win32;

namespace ItemInterpreter.UI.Dashboard
{
    public partial class DashboardWindow : Window
    {
        public class TrackedItemDisplay
        {
            public string Name { get; set; } = string.Empty;
            public int InventoryCount { get; set; }
            public int WarehouseCount { get; set; }
            public int Section { get; set; }
            public int Index { get; set; }
            public int? MinimumTarget { get; set; }
            public int? MaximumTarget { get; set; }
            public decimal? PurchasePrice { get; set; }
            public decimal? SalePrice { get; set; }
            public InventoryStatus Status { get; set; }
            public Brush StatusBrush { get; set; } = Brushes.Gray;
            public string StatusMessage { get; set; } = string.Empty;

            public int TotalCount => InventoryCount + WarehouseCount;
            public string MinimumTargetDisplay => MinimumTarget?.ToString() ?? "—";
            public string MaximumTargetDisplay => MaximumTarget?.ToString() ?? "—";

            public string MarginDisplay
            {
                get
                {
                    if (PurchasePrice.HasValue && SalePrice.HasValue && PurchasePrice.Value > 0)
                    {
                        var margin = (SalePrice.Value - PurchasePrice.Value) / PurchasePrice.Value * 100m;
                        return $"{margin:F1}%";
                    }

                    return "—";
                }
            }

            public string TotalCostDisplay => PurchasePrice.HasValue ? FormatCurrency(PurchasePrice.Value * TotalCount) : "—";
            public string PotentialRevenueDisplay => SalePrice.HasValue ? FormatCurrency(SalePrice.Value * TotalCount) : "—";

            private static string FormatCurrency(decimal value)
            {
                return value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
            }
        }

        public enum InventoryStatus
        {
            Healthy,
            BelowMinimum,
            AboveMaximum
        }

        private readonly string _configPath = "tracked_items.json";
        private readonly string _connectionString = "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";

        private readonly ItemHistoryService _historyService = new();
        private readonly DashboardAuditLogger _auditLogger = new();
        private readonly DispatcherTimer _autoRefreshTimer = new();
        private readonly ObservableCollection<string> _alerts = new();
        private readonly HashSet<string> _alertRegistry = new();

        private List<ItemDefinition> _itemDatabase = ItemXmlLoader.Load("IGC_ItemList.xml");
        private List<TrackedItem> _trackedItems = new();

        public DashboardWindow()
        {
            InitializeComponent();
            AlertList.ItemsSource = _alerts;
            AutoRefreshInterval.SelectedIndex = 2; // 5 minutos
            AutoRefreshCheckBox.IsChecked = true;

            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;

            LoadTrackedItems();
            RefreshData();
            ConfigureAutoRefreshTimer();
        }

        private void LoadTrackedItems()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _trackedItems = JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new();

                foreach (var item in _trackedItems)
                {
                    var definition = _itemDatabase.FirstOrDefault(d => d.Section == item.Section && d.Index == item.Index);
                    if (definition != null)
                    {
                        item.ItemName = definition.Name;
                    }
                }
            }
        }

        private void RefreshData()
        {
            var now = DateTime.Now;
            var dbReader = new DatabaseItemReader(_connectionString);
            var inventoryCounts = dbReader.ReadInventoryCounts();
            var warehouseCounts = dbReader.ReadWarehouseCounts();
            var totalZen = dbReader.ReadTotalZenInventory() + dbReader.ReadTotalZenWarehouse();

            var displayList = new List<TrackedItemDisplay>();
            var newAlerts = new List<string>();

            decimal totalCost = 0m;
            decimal totalPotentialRevenue = 0m;
            int totalStock = 0;

            foreach (var pair in _trackedItems)
            {
                var item = _itemDatabase.FirstOrDefault(i => i.Section == pair.Section && i.Index == pair.Index);
                var key = (pair.Section, pair.Index);
                var inventory = inventoryCounts.TryGetValue(key, out var inv) ? inv : 0;
                var warehouse = warehouseCounts.TryGetValue(key, out var wh) ? wh : 0;
                var total = inventory + warehouse;

                var status = InventoryStatus.Healthy;
                string statusMessage = "Estoque equilibrado";
                Brush statusBrush = new SolidColorBrush(Color.FromRgb(102, 187, 106));

                if (pair.MinimumTarget.HasValue && total < pair.MinimumTarget.Value)
                {
                    status = InventoryStatus.BelowMinimum;
                    statusMessage = "Abaixo da meta";
                    statusBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                }
                else if (pair.MaximumTarget.HasValue && total > pair.MaximumTarget.Value)
                {
                    status = InventoryStatus.AboveMaximum;
                    statusMessage = "Acima do limite";
                    statusBrush = new SolidColorBrush(Color.FromRgb(255, 183, 77));
                }

                var display = new TrackedItemDisplay
                {
                    Name = item?.Name ?? pair.ItemName ?? $"ITEMGET({pair.Section},{pair.Index})",
                    InventoryCount = inventory,
                    WarehouseCount = warehouse,
                    Section = pair.Section,
                    Index = pair.Index,
                    MinimumTarget = pair.MinimumTarget,
                    MaximumTarget = pair.MaximumTarget,
                    PurchasePrice = pair.PurchasePrice,
                    SalePrice = pair.SalePrice,
                    Status = status,
                    StatusMessage = statusMessage,
                    StatusBrush = statusBrush
                };

                displayList.Add(display);

                if (pair.PurchasePrice.HasValue)
                {
                    totalCost += pair.PurchasePrice.Value * display.TotalCount;
                }

                if (pair.SalePrice.HasValue)
                {
                    totalPotentialRevenue += pair.SalePrice.Value * display.TotalCount;
                }

                totalStock += display.TotalCount;

                if (status != InventoryStatus.Healthy)
                {
                    string alertMessage = $"{display.Name}: {statusMessage} (total {display.TotalCount})";
                    newAlerts.Add(alertMessage);
                }
            }

            ItemListView.ItemsSource = displayList;

            LastUpdatedText.Text = $"Última atualização: {now:dd/MM/yyyy HH:mm:ss}";
            TotalTrackedText.Text = displayList.Count.ToString();
            TotalInventoryText.Text = totalStock.ToString();
            TotalValueText.Text = totalCost > 0 ? totalCost.ToString("C", CultureInfo.GetCultureInfo("pt-BR")) : "—";
            if (totalPotentialRevenue > 0 && totalCost > 0)
            {
                var ganho = totalPotentialRevenue - totalCost;
                var percentual = totalCost > 0 ? ganho / totalCost * 100m : 0;
                TotalValueText.ToolTip = $"Receita potencial: {totalPotentialRevenue.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))} (Δ {ganho.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}, {percentual:F1}%)";
            }
            else
            {
                TotalValueText.ToolTip = null;
            }
            TotalZenText.Text = totalZen.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));

            AppendSnapshots(displayList, now);
            RegisterAlerts(newAlerts);
            LogSync(displayList, totalZen, newAlerts, now);
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
                _trackedItems = _trackedItems
                    .Where(t => !(t.Section == selectedDisplay.Section && t.Index == selectedDisplay.Index))
                    .ToList();

                var json = JsonSerializer.Serialize(_trackedItems);
                File.WriteAllText(_configPath, json);

                RefreshData();
            }
        }

        private void AbrirGraficoZen_Click(object sender, RoutedEventArgs e)
        {
            new ZenChart().Show();
        }

        private void AbrirGraficoItens_Click(object sender, RoutedEventArgs e)
        {
            new ItemCountChart().Show();
        }

        private void ExportarCsv_Click(object sender, RoutedEventArgs e)
        {
            if (ItemListView.ItemsSource is not IEnumerable<TrackedItemDisplay> items)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "Arquivo CSV (*.csv)|*.csv",
                FileName = $"dashboard_itens_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var builder = new StringBuilder();
            builder.AppendLine("Item;Sec;Idx;Inventario;Armazem;Total;MetaMin;MetaMax;PrecoCompra;PrecoVenda;CustoTotal;ReceitaPotencial;Margem");

            foreach (var item in items)
            {
                builder.AppendLine(string.Join(';', new[]
                {
                    EscapeCsv(item.Name),
                    item.Section.ToString(),
                    item.Index.ToString(),
                    item.InventoryCount.ToString(),
                    item.WarehouseCount.ToString(),
                    item.TotalCount.ToString(),
                    item.MinimumTarget?.ToString() ?? string.Empty,
                    item.MaximumTarget?.ToString() ?? string.Empty,
                    item.PurchasePrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    item.SalePrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    ExtractNumeric(item.TotalCostDisplay),
                    ExtractNumeric(item.PotentialRevenueDisplay),
                    item.MarginDisplay
                }));
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            MessageBox.Show("Relatório exportado com sucesso!", "Exportação", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(';'))
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }

            return value;
        }

        private static string ExtractNumeric(string formattedValue)
        {
            if (string.IsNullOrWhiteSpace(formattedValue) || formattedValue == "—")
                return string.Empty;

            var digits = formattedValue.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray();
            return new string(digits);
        }

        private void AppendSnapshots(IEnumerable<TrackedItemDisplay> items, DateTime timestamp)
        {
            var snapshots = items.Select(i => new ItemSnapshot
            {
                Timestamp = timestamp,
                Section = i.Section,
                Index = i.Index,
                ItemName = i.Name,
                InventoryCount = i.InventoryCount,
                WarehouseCount = i.WarehouseCount
            }).ToList();

            if (snapshots.Count == 0)
                return;

            _historyService.AppendSnapshots(snapshots);
        }

        private void RegisterAlerts(IEnumerable<string> alerts)
        {
            foreach (var alert in alerts)
            {
                if (_alertRegistry.Add(alert))
                {
                    _alerts.Add($"[{DateTime.Now:HH:mm}] {alert}");
                }
            }
        }

        private void LogSync(IEnumerable<TrackedItemDisplay> items, long totalZen, IEnumerable<string> alerts, DateTime timestamp)
        {
            var entry = new SyncAuditEntry
            {
                Timestamp = timestamp,
                TotalZen = totalZen,
                Items = items.Select(i => new SyncAuditItemDetail
                {
                    ItemName = i.Name,
                    Section = i.Section,
                    Index = i.Index,
                    InventoryCount = i.InventoryCount,
                    WarehouseCount = i.WarehouseCount,
                    TotalCount = i.TotalCount
                }).ToList(),
                Alerts = alerts.ToList()
            };

            _auditLogger.AppendEntry(entry);
        }

        private void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            LoadTrackedItems();
            RefreshData();
        }

        private void AutoRefreshCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ConfigureAutoRefreshTimer();
        }

        private void AutoRefreshCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer.Stop();
        }

        private void AutoRefreshInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfigureAutoRefreshTimer();
        }

        private void ConfigureAutoRefreshTimer()
        {
            if (AutoRefreshCheckBox.IsChecked != true)
                return;

            if (AutoRefreshInterval.SelectedItem is ComboBoxItem combo && combo.Tag is string tag && TimeSpan.TryParse(tag, out var interval))
            {
                _autoRefreshTimer.Stop();
                _autoRefreshTimer.Interval = interval;
                _autoRefreshTimer.Start();
            }
        }

        private void LimparAlertas_Click(object sender, RoutedEventArgs e)
        {
            _alerts.Clear();
            _alertRegistry.Clear();
        }
    }
}
