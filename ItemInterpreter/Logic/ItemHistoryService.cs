using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ItemInterpreter.Data;

namespace ItemInterpreter.Logic
{
    public class ItemHistoryService
    {
        private readonly string _historyPath;
        private readonly int _maxEntriesPerItem;

        public ItemHistoryService(string? historyPath = null, int maxEntriesPerItem = 1000)
        {
            _historyPath = historyPath ?? "item_history.json";
            _maxEntriesPerItem = maxEntriesPerItem;
        }

        public List<ItemSnapshot> ReadHistory()
        {
            if (!File.Exists(_historyPath))
            {
                return new List<ItemSnapshot>();
            }

            try
            {
                var json = File.ReadAllText(_historyPath);
                return JsonSerializer.Deserialize<List<ItemSnapshot>>(json) ?? new List<ItemSnapshot>();
            }
            catch
            {
                return new List<ItemSnapshot>();
            }
        }

        public void AppendSnapshots(IEnumerable<ItemSnapshot> snapshots)
        {
            var existing = ReadHistory();
            existing.AddRange(snapshots);

            var trimmed = existing
                .GroupBy(s => (s.Section, s.Index))
                .SelectMany(g => g
                    .OrderByDescending(s => s.Timestamp)
                    .Take(_maxEntriesPerItem))
                .OrderBy(s => s.Timestamp)
                .ToList();

            var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
    }
}
