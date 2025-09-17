using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using ItemInterpreter.Data;

namespace ItemInterpreter.UI.Configurador
{
    public partial class ItemTrackerConfigGrouped : Window
    {
        private readonly List<ItemDefinition> _allItems;
        private readonly Dictionary<string, List<ItemDefinition>> _groupedItems;
        private readonly string _configPath = "tracked_items.json";

        private readonly ObservableCollection<TrackedItem> _trackedItems = new();

        public ItemTrackerConfigGrouped(List<ItemDefinition> allItems)
        {
            InitializeComponent();
            _allItems = allItems;

            var sectionNames = _allItems
                .GroupBy(i => i.Section)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.SectionName ?? $"Section {g.Key}");

            _groupedItems = _allItems
                .GroupBy(i => $"{i.Section:D2} - {sectionNames[i.Section]}")
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Index).ToList());

            TypeComboBox.ItemsSource = _groupedItems;
            TypeComboBox.SelectedIndex = 0;

            LoadPreviousSelections();
            TrackedItemsGrid.ItemsSource = _trackedItems;
        }

        private void LoadPreviousSelections()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var items = JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new();

                _trackedItems.Clear();

                foreach (var tracked in items)
                {
                    var definition = _allItems.FirstOrDefault(d => d.Section == tracked.Section && d.Index == tracked.Index);
                    tracked.ItemName = definition?.Name ?? tracked.ItemName;
                    _trackedItems.Add(tracked);
                }
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TypeComboBox.SelectedItem is KeyValuePair<string, List<ItemDefinition>> pair)
            {
                ItemListBox.ItemsSource = pair.Value;
                ItemListBox.SelectedItems.Clear();

                foreach (var item in pair.Value)
                {
                    if (_trackedItems.Any(s => s.Section == item.Section && s.Index == item.Index))
                        ItemListBox.SelectedItems.Add(item);
                }
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var json = JsonSerializer.Serialize(_trackedItems);
            File.WriteAllText(_configPath, json);

            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AdicionarSelecionados_Click(object sender, RoutedEventArgs e)
        {
            if (ItemListBox.SelectedItems.Count == 0)
                return;

            foreach (ItemDefinition item in ItemListBox.SelectedItems)
            {
                if (_trackedItems.Any(t => t.Section == item.Section && t.Index == item.Index))
                    continue;

                var tracked = new TrackedItem
                {
                    Section = item.Section,
                    Index = item.Index,
                    ItemName = item.Name
                };

                _trackedItems.Add(tracked);
            }
        }

        private void RemoverSelecionados_Click(object sender, RoutedEventArgs e)
        {
            if (TrackedItemsGrid.SelectedItems.Count == 0)
                return;

            var toRemove = TrackedItemsGrid.SelectedItems.Cast<TrackedItem>().ToList();

            foreach (var tracked in toRemove)
            {
                _trackedItems.Remove(tracked);
            }
        }

    }
}
