using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ItemInterpreter.Data;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;


namespace ItemInterpreter.UI.Charts
{
    public partial class ItemCountChart : Window
    {
        private readonly DispatcherTimer _graficoTimer = new DispatcherTimer();

        private List<ItemDefinition> _itemDefinitions = new();

        private enum Periodo
        {
            Ultimos7Dias,
            Ultimos30Dias,
            Ultimos12Meses,
            Ultimos5Anos
        }

        private enum Origem
        {
            Warehouse,
            Inventory,
            Ambos
        }

        private enum TipoGrafico
        {
            ItensPorPeriodo,
            ZenTotal
        }

        public PlotModel PlotModel { get; private set; } = new PlotModel();

        private Dictionary<string, LineSeries> _seriesPorItem = new();

        public ItemCountChart()
        {
            InitializeComponent();
            DataContext = this;

            PeriodoComboBox.ItemsSource = Enum.GetValues(typeof(Periodo));
            OrigemComboBox.ItemsSource = Enum.GetValues(typeof(Origem));
            PeriodoComboBox.SelectedIndex = 0;
            OrigemComboBox.SelectedIndex = 2;
            _graficoTimer.Interval = TimeSpan.FromSeconds(30); // ou menos, se desejar
            _graficoTimer.Tick += (s, e) =>
            {
                if (PeriodoComboBox.SelectedItem is Periodo periodo &&
                    OrigemComboBox.SelectedItem is Origem origem)
                {
                    LoadChart(periodo, origem);
                }
            };
            _graficoTimer.Start();
            _itemDefinitions = ItemXmlLoader.Load("IGC_ItemList.xml");
            CarregarItemSelector();
        }

        private void PeriodoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeriodoComboBox.SelectedIndex >= 0 && OrigemComboBox.SelectedIndex >= 0)
            {
                CarregarItemSelector(); // ✅ Recarrega o selector
                LoadChart((Periodo)PeriodoComboBox.SelectedItem, (Origem)OrigemComboBox.SelectedItem);
            }
        }

        private void OrigemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeriodoComboBox.SelectedIndex >= 0 && OrigemComboBox.SelectedIndex >= 0)
            {
                CarregarItemSelector(); // ✅ Recarrega o selector
                LoadChart((Periodo)PeriodoComboBox.SelectedItem, (Origem)OrigemComboBox.SelectedItem);
            }
        }

        
        private void CarregarItemSelector()
        {
            if (!File.Exists("tracked_history.json")) return;

            var historico = JsonSerializer.Deserialize<List<ItemTrackingLog>>(File.ReadAllText("tracked_history.json")) ?? new();
            var opcoes = historico
                .GroupBy(h => new { h.Section, h.Index })
                .Select(g => new ItemOption
                {
                    Section = g.Key.Section,
                    Index = g.Key.Index,
                    ItemName = g.FirstOrDefault()?.ItemName ?? $"ITEMGET({g.Key.Section},{g.Key.Index})"
                })
                .ToList();
        }
       

        private void LoadSingleItemChart(int section, int index)
        {
            var model = new PlotModel { Title = $"Rastreamento: Sec {section}, Idx {index}" };
            var historico = JsonSerializer.Deserialize<List<ItemTrackingLog>>(File.ReadAllText("tracked_history.json")) ?? new();
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Bottom, Title = "Data" };
            var valueAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Quantidade" };

            var registros = historico
                .Where(h => h.Section == section && h.Index == index)
                .OrderBy(h => h.Date)
                .ToList();

            var warehouseSeries = new LineSeries { Title = "Warehouse", MarkerType = MarkerType.Square };
            var inventorySeries = new LineSeries { Title = "Inventory", MarkerType = MarkerType.Circle };

            for (int i = 0; i < registros.Count; i++)
            {
                categoryAxis.Labels.Add(registros[i].Date.ToString("dd/MM"));
                warehouseSeries.Points.Add(new DataPoint(i, registros[i].Warehouse));
                inventorySeries.Points.Add(new DataPoint(i, registros[i].Inventory));
            }

            model.Series.Add(warehouseSeries);
            model.Series.Add(inventorySeries);
            model.Axes.Add(categoryAxis);
            model.Axes.Add(valueAxis);

            PlotModel = model;
            ItemChart.Model = PlotModel;
        }

        private void LoadChart(Periodo periodo, Origem origem)
        {
            var model = new PlotModel { Title = "Quantidade de Itens por Período" };
            model.IsLegendVisible = true;

            var categoryAxis = new CategoryAxis { Position = AxisPosition.Bottom, Title = "Data" };
            var valueAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Quantidade" };

            var dadosPorItem = ObterDadosReaisPorItem(periodo, origem);
            var cores = OxyPalettes.HueDistinct(dadosPorItem.Count).Colors;

            int corIndex = 0;

            _seriesPorItem.Clear();
            /*LegendaCheckList.ItemsSource = dadosPorItem.Keys.ToList();*/

            foreach (var (itemName, dados) in dadosPorItem)
            {
                var series = new LineSeries
                {
                    Title = itemName,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    MarkerStroke = OxyColors.Black,
                    StrokeThickness = 2,
                    Color = cores[corIndex % cores.Count]
                };

                for (int i = 0; i < dados.Count; i++)
                {
                    var (label, quantidade) = dados[i];
                    series.Points.Add(new DataPoint(i, quantidade));

                    if (categoryAxis.Labels.Count <= i)
                        categoryAxis.Labels.Add(label);
                }

                _seriesPorItem[itemName] = series;
                model.Series.Add(series);
                corIndex++;
            }


            model.Axes.Add(categoryAxis);
            model.Axes.Add(valueAxis);

            PlotModel = model;
            ItemChart.Model = PlotModel;
            // Reaplica visibilidade conforme checkboxes
            Legenda_Checked(null, null);

        }

        public class ItemTrackingLog
        {
            public string ItemName { get; set; } = string.Empty;
            public int Section { get; set; }
            public int Index { get; set; }
            public DateTime Date { get; set; }
            public int Warehouse { get; set; }
            public int Inventory { get; set; }
            public int ZenAmount { get; set; }
        }

        public class ItemOption
        {
            public int Section { get; set; }
            public int Index { get; set; }
            public string DisplayName => $"{ItemName} (Sec: {Section}, Idx: {Index})";
            public string ItemName { get; set; } = string.Empty;
        }

        private Dictionary<string, List<(string Label, int Quantidade)>> ObterDadosReaisPorItem(Periodo periodo, Origem origem)
        {
            var resultados = new Dictionary<string, List<(string Label, int Quantidade)>>();
            var trackedItems = JsonSerializer.Deserialize<List<TrackedItem>>(File.ReadAllText("tracked_items.json")) ?? new();

            var historicoPath = "tracked_history.json";
            if (!File.Exists(historicoPath))
                return resultados;

            var historico = JsonSerializer.Deserialize<List<ItemTrackingLog>>(File.ReadAllText(historicoPath)) ?? new();

            DateTime cutoffDate = periodo switch
            {
                Periodo.Ultimos7Dias => DateTime.Today.AddDays(-7),
                Periodo.Ultimos30Dias => DateTime.Today.AddDays(-30),
                Periodo.Ultimos12Meses => DateTime.Today.AddMonths(-12),
                Periodo.Ultimos5Anos => DateTime.Today.AddYears(-5),
                _ => DateTime.Today.AddDays(-7)
            };

            foreach (var item in trackedItems)
            {
                // Procurar nome do item a partir do XML
                var def = _itemDefinitions.FirstOrDefault(d => d.Section == item.Section && d.Index == item.Index);
                item.ItemName = def != null ? def.Name : $"ITEMGET({item.Section},{item.Index})";

                string key = $"{item.ItemName} (Sec:{item.Section}, Idx:{item.Index})";

                var registrosFiltrados = historico
                    .Where(h => h.Section == item.Section && h.Index == item.Index && h.Date >= cutoffDate)
                    .GroupBy(h => h.Date.ToString("yyyy-MM-dd"))
                    .Select(g => (g.Key, Warehouse: g.Sum(x => x.Warehouse), Inventory: g.Sum(x => x.Inventory)))
                    .OrderBy(x => x.Key);

                var pontos = new List<(string Label, int Quantidade)>();

                foreach (var r in registrosFiltrados)
                {
                    int quantidade = origem switch
                    {
                        Origem.Warehouse => r.Warehouse,
                        Origem.Inventory => r.Inventory,
                        Origem.Ambos => r.Warehouse + r.Inventory,
                        _ => 0
                    };

                    pontos.Add((r.Key, quantidade));
                }

                resultados[key] = pontos.Count > 0
                    ? pontos
                    : new List<(string Label, int Quantidade)> { ("Sem dados", 0) };

            }

            return resultados;
        }

        private void LoadZenChart(Periodo periodo)
        {
            var model = new PlotModel
            { Title = "Zen Total em Circulação por Período", 
                Background = OxyColors.Black,
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray
            };
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Bottom, Title = "Data" };
            var valueAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Zen em Circulação" };

            var historico = JsonSerializer.Deserialize<List<ItemTrackingLog>>(File.ReadAllText("tracked_history.json")) ?? new();

            DateTime cutoffDate = periodo switch
            {
                Periodo.Ultimos7Dias => DateTime.Today.AddDays(-7),
                Periodo.Ultimos30Dias => DateTime.Today.AddDays(-30),
                Periodo.Ultimos12Meses => DateTime.Today.AddMonths(-12),
                Periodo.Ultimos5Anos => DateTime.Today.AddYears(-5),
                _ => DateTime.Today.AddDays(-7)
            };

            var agrupado = historico
                .Where(h => h.Date >= cutoffDate)
                .GroupBy(h => h.Date.ToString("yyyy-MM-dd"))
                .Select(g => new { Data = g.Key, TotalZen = g.Sum(x => x.ZenAmount) })
                .OrderBy(g => g.Data)
                .ToList();

            var series = new LineSeries
            {
                Title = "Zen Total",
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerStroke = OxyColors.Black,
                StrokeThickness = 2,
                Color = OxyColors.Gold
            };

            for (int i = 0; i < agrupado.Count; i++)
            {
                series.Points.Add(new DataPoint(i, agrupado[i].TotalZen));
                categoryAxis.Labels.Add(agrupado[i].Data);
            }

            model.Series.Add(series);
            model.Axes.Add(categoryAxis);
            model.Axes.Add(valueAxis);

            PlotModel = model;
            ItemChart.Model = PlotModel;
        }
        private void Legenda_Checked(object sender, RoutedEventArgs e)
        {
            if (PlotModel == null || _seriesPorItem.Count == 0) return;

            var selecionados = new List<string>();

            /*foreach (var item in LegendaCheckList.Items)
            {
                var container = LegendaCheckList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    var checkbox = FindVisualChild<CheckBox>(container);
                    if (checkbox?.IsChecked == true)
                    {
                        selecionados.Add(item.ToString());
                    }
                }
            }*/

            PlotModel.Series.Clear();

            foreach (var nome in selecionados)
            {
                if (_seriesPorItem.TryGetValue(nome, out var serieOriginal))
                {
                    var novaSerie = new LineSeries
                    {
                        Title = serieOriginal.Title,
                        MarkerType = serieOriginal.MarkerType,
                        MarkerSize = serieOriginal.MarkerSize,
                        MarkerStroke = serieOriginal.MarkerStroke,
                        StrokeThickness = serieOriginal.StrokeThickness,
                        Color = serieOriginal.Color
                    };

                    foreach (var ponto in serieOriginal.Points)
                    {
                        novaSerie.Points.Add(new DataPoint(ponto.X, ponto.Y));
                    }

                    PlotModel.Series.Add(novaSerie);
                }

            }

            PlotModel.InvalidatePlot(true);
        }
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    return tChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        private void SelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            /*foreach (var item in LegendaCheckList.Items)
            {
                if (LegendaCheckList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                {
                    var checkbox = FindVisualChild<CheckBox>(container);
                    if (checkbox != null)
                        checkbox.IsChecked = true;
                }
            }*/
        }

        /*private void DesmarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in LegendaCheckList.Items)
            {
                if (LegendaCheckList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                {
                    var checkbox = FindVisualChild<CheckBox>(container);
                    if (checkbox != null)
                        checkbox.IsChecked = false;
                }
            }
        }*/


    }
}