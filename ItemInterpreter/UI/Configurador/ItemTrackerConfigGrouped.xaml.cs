using System;
using System.Collections.Generic;
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

        private List<TrackedItem> _selectedItems = new();

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
        }

        private void LoadPreviousSelections()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _selectedItems = JsonSerializer.Deserialize<List<TrackedItem>>(json) ?? new();
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
                    if (_selectedItems.Any(s => s.Section == item.Section && s.Index == item.Index))
                        ItemListBox.SelectedItems.Add(item);
                }
            }
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            var selected = new List<TrackedItem>(_selectedItems);

            if (ItemListBox.ItemsSource is List<ItemDefinition> currentList)
            {
                foreach (ItemDefinition item in ItemListBox.SelectedItems)
                {
                    if (!selected.Any(s => s.Section == item.Section && s.Index == item.Index))
                    {
                        selected.Add(new TrackedItem { Section = item.Section, Index = item.Index });
                    }
                }
            }

            var json = JsonSerializer.Serialize(selected);
            File.WriteAllText(_configPath, json);

            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
