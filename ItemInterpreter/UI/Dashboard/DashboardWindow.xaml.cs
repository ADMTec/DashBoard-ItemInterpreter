using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
            public double? AverageDailyChange { get; set; }
            public double? DaysToMinimum { get; set; }
            public double? DaysToMaximum { get; set; }
            public string ProjectionMessage { get; set; } = "Histórico insuficiente";
            public Brush ProjectionBrush { get; set; } = Brushes.Gray;

            public int TotalCount => InventoryCount + WarehouseCount;
            public string MinimumTargetDisplay => MinimumTarget?.ToString() ?? "—";
            public string MaximumTargetDisplay => MaximumTarget?.ToString() ?? "—";

            public string AverageDailyChangeDisplay => AverageDailyChange.HasValue
                ? $"{AverageDailyChange.Value:+0.##;-0.##;0}/dia"
                : "—";

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

        private class RecentSaleDisplay
        {
            public string DateDisplay { get; set; } = string.Empty;
            public string ItemName { get; set; } = string.Empty;
            public string PriceDisplay { get; set; } = string.Empty;
            public Brush PriceBrush { get; set; } = Brushes.White;
            public string TradeDisplay { get; set; } = string.Empty;
            public string Tooltip { get; set; } = string.Empty;
        }

        private class HotItemDisplay
        {
            public string ItemName { get; set; } = string.Empty;
            public int TotalSales { get; set; }
            public string AveragePriceDisplay { get; set; } = string.Empty;
            public string ChangeDisplay { get; set; } = string.Empty;
            public Brush ChangeBrush { get; set; } = Brushes.White;
            public string OutlierDisplay { get; set; } = string.Empty;
            public string Tooltip { get; set; } = string.Empty;
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
        private readonly ObservableCollection<RecentSaleDisplay> _recentSales = new();
        private readonly ObservableCollection<HotItemDisplay> _topVolume = new();
        private readonly ObservableCollection<HotItemDisplay> _priceVariations = new();
        private readonly HashSet<string> _alertRegistry = new();

        private List<ItemDefinition> _itemDatabase = ItemXmlLoader.Load("IGC_ItemList.xml");
        private List<TrackedItem> _trackedItems = new();
        private readonly PersonalShopPriceHistoryService _personalShopService;
        private readonly NotificationSettingsService _notificationSettingsService = new();
        private NotificationSettings _notificationSettings;
        private INotificationDispatcher? _notificationDispatcher;
        private readonly string _notificationLogPath = "notification_failures.log";

        public DashboardWindow()
        {
            InitializeComponent();
            AlertList.ItemsSource = _alerts;
            RecentSalesListView.ItemsSource = _recentSales;
            TopVolumeListView.ItemsSource = _topVolume;
            PriceVariationListView.ItemsSource = _priceVariations;
            AutoRefreshInterval.SelectedIndex = 2; // 5 minutos
            AutoRefreshCheckBox.IsChecked = true;

            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;

            _personalShopService = new PersonalShopPriceHistoryService(_connectionString, itemDefinitions: _itemDatabase);
            _notificationSettings = _notificationSettingsService.Load();
            UpdateNotificationDispatcher();

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
            var trackedKeys = new List<(int Section, int Index)>();

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
                trackedKeys.Add((pair.Section, pair.Index));

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

            UpdateItemProjections(displayList, trackedKeys);
            UpdateRecentSales(trackedKeys);
            UpdateHotItemRankings(trackedKeys);

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

        private void AbrirGraficoPersonalShop_Click(object sender, RoutedEventArgs e)
        {
            LoadTrackedItems();
            new PersonalShopPriceChart(_trackedItems, _connectionString).Show();
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
                    var formatted = $"[{DateTime.Now:HH:mm}] {alert}";
                    _alerts.Add(formatted);
                    DispatchAlertAsync(formatted);
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

        private void UpdateItemProjections(List<TrackedItemDisplay> displays, IEnumerable<(int Section, int Index)> trackedKeys)
        {
            if (displays.Count == 0)
            {
                return;
            }

            var history = _historyService.GetDailyTotals(trackedKeys, 60);

            foreach (var display in displays)
            {
                if (!history.TryGetValue((display.Section, display.Index), out var totals) || totals.Count < 2)
                {
                    display.AverageDailyChange = null;
                    display.DaysToMinimum = null;
                    display.DaysToMaximum = null;
                    display.ProjectionMessage = "Histórico insuficiente";
                    display.ProjectionBrush = Brushes.Gray;
                    continue;
                }

                var averageChange = _historyService.CalculateAverageDailyChange(totals, 7);
                display.AverageDailyChange = averageChange;

                var (message, brush, daysToMin, daysToMax) = CalculateProjection(display, averageChange);
                display.ProjectionMessage = message;
                display.ProjectionBrush = brush;
                display.DaysToMinimum = daysToMin;
                display.DaysToMaximum = daysToMax;
            }
        }

        private (string Message, Brush Brush, double? DaysToMinimum, double? DaysToMaximum) CalculateProjection(TrackedItemDisplay display, double? averageChange)
        {
            var neutralBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            if (!averageChange.HasValue || Math.Abs(averageChange.Value) < 0.01)
            {
                return ("Tendência estável", neutralBrush, null, null);
            }

            if (display.MinimumTarget.HasValue && display.TotalCount <= display.MinimumTarget.Value)
            {
                return ("⚠️ Abaixo da meta mínima", new SolidColorBrush(Color.FromRgb(244, 67, 54)), 0, null);
            }

            if (display.MaximumTarget.HasValue && display.TotalCount >= display.MaximumTarget.Value)
            {
                return ("⬆️ Acima da meta máxima", new SolidColorBrush(Color.FromRgb(255, 183, 77)), null, 0);
            }

            if (averageChange.Value < 0)
            {
                if (display.MinimumTarget.HasValue)
                {
                    var distance = display.TotalCount - display.MinimumTarget.Value;
                    if (distance > 0)
                    {
                        var days = distance / Math.Abs(averageChange.Value);
                        var brush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                        var message = days <= 1
                            ? "⚠️ Ruptura iminente"
                            : $"⚠️ {Math.Ceiling(days)}d até meta mínima";
                        return (message, brush, days, null);
                    }
                }

                return ("Consumo em queda", new SolidColorBrush(Color.FromRgb(229, 115, 115)), null, null);
            }

            if (averageChange.Value > 0)
            {
                if (display.MaximumTarget.HasValue)
                {
                    var distance = display.MaximumTarget.Value - display.TotalCount;
                    if (distance > 0)
                    {
                        var days = distance / averageChange.Value;
                        var brush = new SolidColorBrush(Color.FromRgb(255, 183, 77));
                        var message = days <= 1
                            ? "⬆️ Ultrapassa meta amanhã"
                            : $"⬆️ {Math.Ceiling(days)}d até meta máxima";
                        return (message, brush, null, days);
                    }
                }

                return ("Estoque crescendo", new SolidColorBrush(Color.FromRgb(129, 199, 132)), null, null);
            }

            return ("Tendência estável", neutralBrush, null, null);
        }

        private void UpdateRecentSales(List<(int Section, int Index)> trackedKeys)
        {
            _recentSales.Clear();

            List<PersonalShopSaleEntry> sales;
            try
            {
                sales = _personalShopService.GetRecentSales(20, trackedKeys);
            }
            catch
            {
                return;
            }

            var culture = CultureInfo.GetCultureInfo("pt-BR");

            foreach (var sale in sales)
            {
                var price = sale.AlternativePrice.HasValue && sale.AlternativePrice.Value > 0
                    ? sale.AlternativePrice.Value
                    : sale.PriceZen;

                double? diffPercent = null;
                if (sale.AveragePriceWindow.HasValue && sale.AveragePriceWindow.Value > 0)
                {
                    diffPercent = (price - sale.AveragePriceWindow.Value) / sale.AveragePriceWindow.Value * 100d;
                }

                var brush = GetPriceBrush(diffPercent);
                var tooltip = sale.AveragePriceWindow.HasValue
                    ? $"Média 30d: {sale.AveragePriceWindow.Value.ToString("N0", culture)} | Δ {(diffPercent ?? 0):F1}%"
                    : "Sem histórico recente";

                _recentSales.Add(new RecentSaleDisplay
                {
                    DateDisplay = sale.Date.ToString("dd/MM HH:mm"),
                    ItemName = sale.ItemName,
                    PriceDisplay = price.ToString("N0", culture),
                    PriceBrush = brush,
                    TradeDisplay = string.IsNullOrWhiteSpace(sale.Buyer) && string.IsNullOrWhiteSpace(sale.Seller)
                        ? "—"
                        : $"{sale.Seller} → {sale.Buyer}",
                    Tooltip = tooltip
                });
            }
        }

        private void UpdateHotItemRankings(List<(int Section, int Index)> trackedKeys)
        {
            _topVolume.Clear();
            _priceVariations.Clear();

            List<HotItemSummary> summaries;
            try
            {
                summaries = _personalShopService.GetHotItemSummaries(TimeSpan.FromDays(7), 10, TimeSpan.FromDays(7), trackedKeys);
            }
            catch
            {
                return;
            }

            var culture = CultureInfo.GetCultureInfo("pt-BR");

            foreach (var summary in summaries.OrderByDescending(s => s.TotalSales).Take(5))
            {
                var display = BuildHotItemDisplay(summary, culture);
                _topVolume.Add(display);
            }

            foreach (var summary in summaries
                .OrderByDescending(s => Math.Abs(s.PriceChangePercent))
                .ThenByDescending(s => s.TotalSales)
                .Take(5))
            {
                var display = BuildHotItemDisplay(summary, culture);
                _priceVariations.Add(display);
            }
        }

        private HotItemDisplay BuildHotItemDisplay(HotItemSummary summary, CultureInfo culture)
        {
            var changeBrush = summary.PriceChangePercent switch
            {
                >= 15 => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                >= 5 => new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                <= -15 => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
                <= -5 => new SolidColorBrush(Color.FromRgb(129, 199, 132)),
                _ => Brushes.White
            };

            var tooltip = summary.PreviousAveragePrice.HasValue
                ? $"Média atual: {summary.AveragePrice.ToString("N0", culture)} | Média anterior: {summary.PreviousAveragePrice.Value.ToString("N0", culture)}"
                : $"Média atual: {summary.AveragePrice.ToString("N0", culture)}";

            if (summary.PriceStandardDeviation.HasValue)
            {
                tooltip += $" | Desvio padrão: {summary.PriceStandardDeviation.Value:F0}";
            }

            return new HotItemDisplay
            {
                ItemName = summary.ItemName,
                TotalSales = summary.TotalSales,
                AveragePriceDisplay = summary.AveragePrice.ToString("N0", culture),
                ChangeDisplay = summary.PriceChangePercent == 0
                    ? "0%"
                    : $"{summary.PriceChangePercent:+0.0;-0.0}%",
                ChangeBrush = changeBrush,
                OutlierDisplay = summary.IsOutlier ? "Sim" : "Não",
                Tooltip = tooltip
            };
        }

        private Brush GetPriceBrush(double? diffPercent)
        {
            if (!diffPercent.HasValue)
            {
                return Brushes.White;
            }

            if (diffPercent.Value >= 15)
            {
                return new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }

            if (diffPercent.Value >= 5)
            {
                return new SolidColorBrush(Color.FromRgb(255, 183, 77));
            }

            if (diffPercent.Value <= -15)
            {
                return new SolidColorBrush(Color.FromRgb(102, 187, 106));
            }

            if (diffPercent.Value <= -5)
            {
                return new SolidColorBrush(Color.FromRgb(129, 199, 132));
            }

            return Brushes.White;
        }

        private void DispatchAlertAsync(string message)
        {
            if (_notificationDispatcher == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationDispatcher.DispatchAsync(message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogNotificationFailure(ex, message);
                }
            });
        }

        private void LogNotificationFailure(Exception exception, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] Falha ao enviar alerta: {message} | Erro: {exception.Message}{Environment.NewLine}";
                File.AppendAllText(_notificationLogPath, line);
            }
            catch
            {
                // Evita loop de erros
            }
        }

        private void ConfigurarNotificacoes_Click(object sender, RoutedEventArgs e)
        {
            var window = new NotificationSettingsWindow(_notificationSettingsService)
            {
                Owner = this
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                _notificationSettings = window.GetUpdatedSettings();
                UpdateNotificationDispatcher();
            }
        }

        private void UpdateNotificationDispatcher()
        {
            (_notificationDispatcher as IDisposable)?.Dispose();

            if (_notificationSettings.WebhookEnabled && !string.IsNullOrWhiteSpace(_notificationSettings.WebhookUrl))
            {
                _notificationDispatcher = new WebhookNotificationDispatcher(_notificationSettings.WebhookUrl!);
            }
            else
            {
                _notificationDispatcher = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            (_notificationDispatcher as IDisposable)?.Dispose();
        }
    }
}
