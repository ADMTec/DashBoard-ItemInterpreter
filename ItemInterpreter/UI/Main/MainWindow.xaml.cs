using System;
using System.Linq;
using System.Text;
using System.Windows;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using ItemInterpreter.Logic;
using ItemInterpreter.UI.Charts;
using ItemInterpreter.UI.Dashboard;

namespace ItemInterpreter.UI.Main
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _itemDatabase = ItemXmlLoader.Load("IGC_ItemList.xml"); // coloque o caminho correto do XML aqui
            _excellentOptions = ExcellentOptionsXmlLoader.Load("IGC_ExcellentOptions.xml");
            DailyItemTracker.RegistrarContagemDiaria();

            _zenWatcher = new ZenWatcher();
            _zenWatcher.Start(); // Inicia a escuta de mudanças de Zen

            new ItemMarketAgent(); // ativa coleta automática

        }

        private ZenWatcher _zenWatcher;

        private List<ExcellentOptionDefinition> _excellentOptions;

        private List<ItemDefinition> _itemDatabase;
        private void AbrirGraficoZen_Click(object sender, RoutedEventArgs e)
        {
            DailyItemTracker.RegistrarContagemDiaria(); // 🪙 Garante que o JSON será criado

            var graficoZen = new ZenChart();
            graficoZen.Show();
        }

        private void InterpretItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new InterpretarItemWindow();
            window.ShowDialog();
        }

        private void DashboardMenu_Click(object sender, RoutedEventArgs e)
        {
            var dash = new DashboardWindow();
            dash.Show();
        }

        private void AbrirGrafico_Click(object sender, RoutedEventArgs e)
        {
            var chart = new ItemCountChart();
            chart.Show();
        }
    }
}
