using System.Text.Json;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Threading;
using System.IO;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Windows;
using ItemInterpreter.Data; // Certifique-se de que essa linha exista


namespace ItemInterpreter.UI.Charts
{
    public partial class ZenChart : Window
    {
        private readonly DispatcherTimer _graficoTimer = new DispatcherTimer();

        private enum OrigemZen
        {
            Total,
            Warehouse,
            Inventory
        }

        public ZenChart()
        {
            InitializeComponent();
            OrigemComboBox.ItemsSource = new[] { "Total", "Warehouse", "Inventory" };
            OrigemComboBox.SelectedIndex = 0;
            _graficoTimer.Interval = TimeSpan.FromSeconds(30); // ou menos, se preferir
            _graficoTimer.Tick += (s, e) =>
            {
                if (OrigemComboBox.SelectedItem is string origem)
                    CarregarGrafico(origem);
            };
            _graficoTimer.Start();

        }

        private void OrigemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OrigemComboBox.SelectedItem is string origem)
            {
                CarregarGrafico(origem);
            }
        }

        private void CarregarGrafico(string origem)
        {
            var model = new PlotModel
            {
                Title = "Zen em Circulação por Dia",
                Background = OxyColors.Black,
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray
            };

            if (!File.Exists("zen_history.json"))
            {
                MessageBox.Show("Ainda não há dados de Zen registrados.");
                return;
            }

            var historico = JsonSerializer.Deserialize<List<ZenTrackingLog>>(File.ReadAllText("zen_history.json")) ?? new();

            var agrupado = historico
                .GroupBy(e => e.Date.Date)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    long valor = origem switch
                    {
                        "Total" => g.Sum(e => e.TotalZenInventory + e.TotalZenWarehouse),
                        "Warehouse" => g.Sum(e => e.TotalZenWarehouse),
                        "Inventory" => g.Sum(e => e.TotalZenInventory),
                        _ => 0
                    };

                    return new { Data = g.Key.ToString("yyyy-MM-dd"), Total = valor };
                })
                .ToList();

            var eixoX = new CategoryAxis { Position = AxisPosition.Bottom, Title = "Data" };
            var eixoY = new LinearAxis { Position = AxisPosition.Left, Title = "Zen" };

            var serie = new LineSeries { Title = origem.ToString(), MarkerType = MarkerType.Circle,
                StrokeThickness = 2,
                Color = OxyColors.LimeGreen, // ou outra cor forte
                MarkerFill = OxyColors.White,
            };

            for (int i = 0; i < agrupado.Count; i++)
            {
                eixoX.Labels.Add(agrupado[i].Data);
                serie.Points.Add(new DataPoint(i, agrupado[i].Total));
            }

            model.Series.Add(serie);
            model.Axes.Add(eixoX);
            model.Axes.Add(eixoY);

            ZenPlot.Model = model;
        }
    }

}
