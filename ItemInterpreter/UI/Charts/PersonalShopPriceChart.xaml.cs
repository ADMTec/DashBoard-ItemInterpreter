using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using ItemInterpreter.Logic;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ItemInterpreter.UI.Charts
{
    public partial class PersonalShopPriceChart : Window
    {
        private enum Periodo
        {
            Ultimos7Dias,
            Ultimos30Dias,
            Ultimos90Dias,
            Ultimos12Meses,
            Completo
        }

        private class ItemOption
        {
            public int Section { get; set; }
            public int Index { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        private readonly string _connectionString;
        private readonly List<ItemDefinition> _definitions = ItemXmlLoader.Load("IGC_ItemList.xml");
        private readonly List<TrackedItem> _trackedItems;
        private readonly PersonalShopPriceHistoryService _priceHistoryService;

        private List<ItemOption> _trackedItemOptions = new();
        private List<PersonalShopAveragePriceEntry> _historyCache = new();
        private bool _isInitialized;

        public PersonalShopPriceChart(IEnumerable<TrackedItem>? trackedItems = null, string? connectionString = null)
        {
            InitializeComponent();

            _connectionString = connectionString ?? "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";

            _priceHistoryService = new PersonalShopPriceHistoryService(_connectionString, itemDefinitions: _definitions);

            _trackedItems = trackedItems?.Select(CloneTrackedItem).ToList() ?? LoadTrackedItemsFromFile();

            Loaded += PersonalShopPriceChart_Loaded;
        }

        private static TrackedItem CloneTrackedItem(TrackedItem original)
        {
            return new TrackedItem
            {
                Section = original.Section,
                Index = original.Index,
                ItemName = original.ItemName,
                MinimumTarget = original.MinimumTarget,
                MaximumTarget = original.MaximumTarget,
                PurchasePrice = original.PurchasePrice,
                SalePrice = original.SalePrice
            };
        }

        private static List<TrackedItem> LoadTrackedItemsFromFile()
        {
            if (!File.Exists("tracked_items.json"))
            {
                return new List<TrackedItem>();
            }

            try
            {
                var json = File.ReadAllText("tracked_items.json");
                return JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new List<TrackedItem>();
            }
            catch
            {
                return new List<TrackedItem>();
            }
        }

        private void PersonalShopPriceChart_Loaded(object sender, RoutedEventArgs e)
        {
            PeriodoComboBox.ItemsSource = Enum.GetValues(typeof(Periodo));
            PeriodoComboBox.SelectedIndex = 1; // Últimos 30 dias

            LoadTrackedOptions();
            AtualizarHistorico();
            AtualizarGrafico();

            _isInitialized = true;
        }

        private void LoadTrackedOptions()
        {
            _trackedItemOptions = _trackedItems
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
                .OrderBy(option => option.DisplayName)
                .ToList();

            ItemSelector.ItemsSource = _trackedItemOptions;
            ItemSelector.DisplayMemberPath = nameof(ItemOption.DisplayName);
        }

        private void AtualizarHistorico()
        {
            var keys = _trackedItemOptions.Select(option => (option.Section, option.Index));
            _historyCache = _priceHistoryService.RefreshHistory(keys);
            LastRefreshText.Text = _historyCache.Count > 0
                ? $"Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}"
                : "Sem dados registrados";
        }

        private void AtualizarGrafico()
        {
            var model = new PlotModel
            {
                Title = "Preço médio diário por item",
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
                IntervalLength = 80,
                Title = "Data"
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Preço médio (Zen)",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0
            };

            model.Axes.Add(dateAxis);
            model.Axes.Add(valueAxis);

            if (_trackedItemOptions.Count == 0)
            {
                model.Subtitle = "Nenhum item rastreado configurado.";
                PriceChart.Model = model;
                return;
            }

            if (_historyCache.Count == 0)
            {
                model.Subtitle = "Nenhum registro de vendas encontrado para os itens selecionados.";
                PriceChart.Model = model;
                return;
            }

            var periodo = (Periodo)(PeriodoComboBox.SelectedItem ?? Periodo.Ultimos30Dias);
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
                var entries = _historyCache
                    .Where(entry => entry.Section == option.Section && entry.Index == option.Index && entry.Date >= cutoff)
                    .OrderBy(entry => entry.Date)
                    .ToList();

                if (entries.Count == 0)
                {
                    continue;
                }

                var series = new LineSeries
                {
                    Title = option.DisplayName,
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    Color = paletteColors[i % paletteColors.Count],
                    MarkerStroke = OxyColors.White,
                    ItemsSource = entries,
                    DataFieldX = nameof(PersonalShopAveragePriceEntry.Date),
                    DataFieldY = nameof(PersonalShopAveragePriceEntry.AveragePrice),
                    TrackerFormatString = "{0}\nData: {Date:dd/MM/yyyy}\nPreço médio: {AveragePrice:N0} Zen\nVendas: {SaleCount}"
                };

                model.Series.Add(series);
            }

            if (!model.Series.Any())
            {
                model.Subtitle = "Sem dados dentro do período selecionado.";
            }

            PriceChart.Model = model;
        }

        private static DateTime GetCutoffDate(Periodo periodo)
        {
            return periodo switch
            {
                Periodo.Ultimos7Dias => DateTime.Now.AddDays(-7),
                Periodo.Ultimos30Dias => DateTime.Now.AddDays(-30),
                Periodo.Ultimos90Dias => DateTime.Now.AddDays(-90),
                Periodo.Ultimos12Meses => DateTime.Now.AddMonths(-12),
                _ => DateTime.MinValue
            };
        }

        private List<ItemOption> GetSelectedItems()
        {
            return ItemSelector.SelectedItems.Cast<ItemOption>().ToList();
        }

        private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            AtualizarGrafico();
        }

        private void AtualizarDados_Click(object sender, RoutedEventArgs e)
        {
            AtualizarHistorico();
            AtualizarGrafico();
        }
    }
}
