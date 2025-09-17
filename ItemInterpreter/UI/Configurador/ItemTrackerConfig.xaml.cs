using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using ItemInterpreter.Data;

namespace ItemInterpreter.UI.Configurador
{
    public partial class ItemTrackerConfig : Window
    {
        private List<ItemDefinition> _allItems;
        private string _configPath = "tracked_items.json";

        public List<(int Section, int Index)> SelectedItems { get; private set; } = new();

        public ItemTrackerConfig(List<ItemDefinition> allItems)
        {
            InitializeComponent();
            _allItems = allItems;
            LoadPreviousSelections();
        }

        private void LoadPreviousSelections()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var selected = JsonSerializer.Deserialize<List<(int Section, int Index)>>(json);
                SelectedItems = selected ?? new();
            }

            ItemListBox.ItemsSource = _allItems.OrderBy(i => i.Name).ToList();

            foreach (var item in _allItems)
            {
                if (SelectedItems.Any(s => s.Section == item.Section && s.Index == item.Index))
                    ItemListBox.SelectedItems.Add(item);
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            SelectedItems = ItemListBox.SelectedItems
                .Cast<ItemDefinition>()
                .Select(i => (i.Section, i.Index))
                .ToList();

            var json = JsonSerializer.Serialize(SelectedItems);
            File.WriteAllText(_configPath, json);

            this.DialogResult = true;
            this.Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}