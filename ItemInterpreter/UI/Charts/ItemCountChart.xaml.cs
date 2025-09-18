using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using ItemInterpreter.Logic;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ItemInterpreter.UI.Charts
{
    public partial class ItemCountChart : Window
    {
        private enum Periodo
        {
            Ultimos7Dias,
            Ultimos30Dias,
            Ultimos12Meses,
            Completo
        }

        private enum Origem
        {
            Inventario,
            Armazem,
            Ambos
        }

        private class ItemOption
        {
            public int Section { get; set; }
            public int Index { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        private readonly ItemHistoryService _historyService = new();
        private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMinutes(1) };
        private readonly List<ItemDefinition> _definitions = ItemXmlLoader.Load("IGC_ItemList.xml");
        private List<ItemOption> _trackedItemOptions = new();
        private bool _isInitialized;

        public ItemCountChart()
        {
            InitializeComponent();
            Loaded += ItemCountChart_Loaded;
            _refreshTimer.Tick += RefreshTimer_Tick;
            Closed += (s, e) => _refreshTimer.Stop();
        }

        private void ItemCountChart_Loaded(object sender, RoutedEventArgs e)
        {
            PeriodoComboBox.ItemsSource = Enum.GetValues(typeof(Periodo));
            OrigemComboBox.ItemsSource = Enum.GetValues(typeof(Origem));
            PeriodoComboBox.SelectedIndex = 0;
            OrigemComboBox.SelectedIndex = 2;

            LoadTrackedItems();
            UpdateChart();

            _isInitialized = true;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            LoadTrackedItems();
            UpdateChart();
        }

        private void LoadTrackedItems()
        {
            try
            {
                if (!File.Exists("tracked_items.json"))
                {
                    _trackedItemOptions = new List<ItemOption>();
                }
                else
                {
                    var json = File.ReadAllText("tracked_items.json");
                    var trackedItems = JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new List<TrackedItem>();

                    _trackedItemOptions = trackedItems
                        .Select(item =>
                        {
                            var definition = _definitions.FirstOrDefault(d => d.Section == item.Section && d.Index == item.Index);
                            var name = definition?.Name ?? item.ItemName ?? $"ITEMGET({item.Section},{item.Index})";
                            return new ItemOption
                            {
                                Section = item.Section,
                                Index = item.Index,
                                DisplayName = $"{name} (Sec {item.Section}, Idx {item.Index})"
                            };
                        })
                        .OrderBy(o => o.DisplayName)
                        .ToList();
                }

                ItemSelector.ItemsSource = _trackedItemOptions;
                ItemSelector.DisplayMemberPath = nameof(ItemOption.DisplayName);
            }
            catch
            {
                _trackedItemOptions = new List<ItemOption>();
            }
        }

        private void Filtro_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            UpdateChart();
        }

        private void UpdateChart()
        {
            var model = new PlotModel
            {
                Title = "Histórico de estoque",
                Background = OxyColors.Black,
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray
            };

            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd/MM",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.Dot,
                IntervalLength = 80
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Quantidade",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.Dot
            };

            model.Axes.Add(dateAxis);
            model.Axes.Add(valueAxis);

            var history = _historyService.ReadHistory();
            if (history.Count == 0)
            {
                ItemChart.Model = model;
                return;
            }

            var periodo = (Periodo)(PeriodoComboBox.SelectedItem ?? Periodo.Ultimos7Dias);
            var origem = (Origem)(OrigemComboBox.SelectedItem ?? Origem.Ambos);
            var cutoff = GetCutoffDate(periodo);
            var selectedItems = GetSelectedItems();

            if (!selectedItems.Any())
            {
                selectedItems = _trackedItemOptions;
            }

            var palette = OxyPalettes.HueDistinct(selectedItems.Count);
            var paletteColors = palette.Colors;

            for (int i = 0; i < selectedItems.Count; i++)
            {
                var option = selectedItems[i];
                var points = history
                    .Where(h => h.Section == option.Section && h.Index == option.Index && h.Timestamp >= cutoff)
                    .OrderBy(h => h.Timestamp)
                    .ToList();

                if (points.Count == 0)
                    continue;

                var series = new LineSeries
                {
                    Title = option.DisplayName,
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    Color = paletteColors[i % paletteColors.Count],
                    MarkerStroke = OxyColors.White
                };

                foreach (var snapshot in points)
                {
                    double value = origem switch
                    {
                        Origem.Inventario => snapshot.InventoryCount,
                        Origem.Armazem => snapshot.WarehouseCount,
                        _ => snapshot.InventoryCount + snapshot.WarehouseCount
                    };

                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(snapshot.Timestamp), value));
                }

                model.Series.Add(series);
            }

            if (!model.Series.Any())
            {
                model.Series.Add(new LineSeries
                {
                    Title = "Sem dados",
                    Points = { new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), 0) }
                });
            }

            ItemChart.Model = model;
        }

        private static DateTime GetCutoffDate(Periodo periodo)
        {
            return periodo switch
            {
                Periodo.Ultimos7Dias => DateTime.Now.AddDays(-7),
                Periodo.Ultimos30Dias => DateTime.Now.AddDays(-30),
                Periodo.Ultimos12Meses => DateTime.Now.AddMonths(-12),
                _ => DateTime.MinValue
            };
        }

        private List<ItemOption> GetSelectedItems()
        {
            return ItemSelector.SelectedItems.Cast<ItemOption>().ToList();
        }
    }
}
