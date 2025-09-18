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

        public Dictionary<(int Section, int Index), List<DailyItemTotal>> GetDailyTotals(IEnumerable<(int Section, int Index)> items, int days = 60)
        {
            var history = ReadHistory();
            var filter = items?.ToHashSet() ?? new HashSet<(int Section, int Index)>();
            var hasFilter = filter.Count > 0;
            var cutoff = DateTime.Now.Date.AddDays(-Math.Abs(days));

            return history
                .Where(snapshot => snapshot.Timestamp.Date >= cutoff)
                .Where(snapshot => !hasFilter || filter.Contains((snapshot.Section, snapshot.Index)))
                .GroupBy(snapshot => (snapshot.Section, snapshot.Index))
                .ToDictionary(group => group.Key, group => group
                    .GroupBy(snapshot => snapshot.Timestamp.Date)
                    .Select(dayGroup => dayGroup
                        .OrderByDescending(s => s.Timestamp)
                        .First())
                    .OrderBy(s => s.Timestamp)
                    .Select(s => new DailyItemTotal
                    {
                        Date = s.Timestamp.Date,
                        Section = s.Section,
                        Index = s.Index,
                        TotalCount = s.TotalCount
                    })
                    .ToList());
        }

        public double? CalculateAverageDailyChange(IEnumerable<DailyItemTotal> totals, int window = 7)
        {
            if (totals == null)
            {
                return null;
            }

            var ordered = totals
                .OrderByDescending(total => total.Date)
                .Take(Math.Max(window + 1, 2))
                .OrderBy(total => total.Date)
                .ToList();

            if (ordered.Count < 2)
            {
                return null;
            }

            var deltas = new List<double>();
            for (int i = 1; i < ordered.Count; i++)
            {
                var current = ordered[i].TotalCount;
                var previous = ordered[i - 1].TotalCount;
                deltas.Add(current - previous);
            }

            return deltas.Count > 0 ? deltas.Average() : null;
        }

    }
}
