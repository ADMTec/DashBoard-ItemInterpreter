using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using ItemInterpreter.Data;
using Microsoft.Data.SqlClient;

namespace ItemInterpreter.Logic
{
    public class PersonalShopPriceHistoryService
    {
        private readonly string _connectionString;
        private readonly string _historyPath;
        private readonly int _maxEntriesPerItem;

        public PersonalShopPriceHistoryService(string connectionString, string? historyPath = null, int maxEntriesPerItem = 365)
        {
            _connectionString = connectionString;
            _historyPath = historyPath ?? "personalshop_price_history.json";
            _maxEntriesPerItem = maxEntriesPerItem;
        }

        public List<PersonalShopAveragePriceEntry> ReadHistory()
        {
            if (!File.Exists(_historyPath))
            {
                return new List<PersonalShopAveragePriceEntry>();
            }

            try
            {
                var json = File.ReadAllText(_historyPath);
                return JsonSerializer.Deserialize<List<PersonalShopAveragePriceEntry>>(json) ?? new List<PersonalShopAveragePriceEntry>();
            }
            catch
            {
                return new List<PersonalShopAveragePriceEntry>();
            }
        }

        public List<PersonalShopAveragePriceEntry> RefreshHistory(IEnumerable<(int Section, int Index)> trackedItems, int daysToKeep = 120)
        {
            var trackedList = trackedItems?.Distinct().ToList() ?? new List<(int Section, int Index)>();
            if (trackedList.Count == 0)
            {
                SaveHistory(new List<PersonalShopAveragePriceEntry>());
                return new List<PersonalShopAveragePriceEntry>();
            }

            var cutoffDate = DateTime.Now.Date.AddDays(-Math.Abs(daysToKeep));

            try
            {
                var databaseEntries = QueryDatabase(cutoffDate, trackedList);
                var existing = ReadHistory()
                    .Where(entry => entry.Date.Date >= cutoffDate)
                    .Where(entry => trackedList.Contains((entry.Section, entry.Index)))
                    .ToDictionary(entry => (entry.Date.Date, entry.Section, entry.Index));

                foreach (var entry in databaseEntries)
                {
                    existing[(entry.Date.Date, entry.Section, entry.Index)] = entry;
                }

                var merged = existing
                    .Values
                    .GroupBy(entry => (entry.Section, entry.Index))
                    .SelectMany(group => group
                        .OrderByDescending(entry => entry.Date)
                        .Take(_maxEntriesPerItem))
                    .OrderBy(entry => entry.Date)
                    .ThenBy(entry => entry.Section)
                    .ThenBy(entry => entry.Index)
                    .ToList();

                SaveHistory(merged);
                return merged;
            }
            catch
            {
                return ReadHistory();
            }
        }

        private List<PersonalShopAveragePriceEntry> QueryDatabase(DateTime cutoffDate, List<(int Section, int Index)> trackedItems)
        {
            var results = new List<PersonalShopAveragePriceEntry>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;

            var filters = new List<string>();
            for (int i = 0; i < trackedItems.Count; i++)
            {
                var sectionParam = $"@section{i}";
                var indexParam = $"@index{i}";

                filters.Add($"(l.[Section] = {sectionParam} AND l.[IndexID] = {indexParam})");

                command.Parameters.AddWithValue(sectionParam, trackedItems[i].Section);
                command.Parameters.AddWithValue(indexParam, trackedItems[i].Index);
            }

            command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = cutoffDate });

            var filterClause = filters.Count > 0
                ? $"AND ({string.Join(" OR ", filters)})"
                : string.Empty;

            command.CommandText = $@"
SELECT
    CAST(l.[Date] AS DATE) AS SaleDate,
    l.[Section],
    l.[IndexID],
    AVG(CAST(CASE WHEN v.[Money] > 0 THEN v.[Money] ELSE l.[PriceZen] END AS DECIMAL(18, 2))) AS AveragePrice,
    COUNT(*) AS SaleCount
FROM [dbo].[T_PSHOP_SALE_LOG] AS l
LEFT JOIN [dbo].[T_PSHOP_ITEMVALUE_INFO] AS v ON l.[ItemSerial] = v.[ItemSerial]
WHERE l.[Date] >= @StartDate {filterClause}
GROUP BY CAST(l.[Date] AS DATE), l.[Section], l.[IndexID]
ORDER BY SaleDate;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entry = new PersonalShopAveragePriceEntry
                {
                    Date = reader.GetDateTime(0),
                    Section = reader.GetInt32(1),
                    Index = reader.GetInt32(2),
                    AveragePrice = reader.IsDBNull(3) ? 0d : Convert.ToDouble(reader.GetDecimal(3)),
                    SaleCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                };

                results.Add(entry);
            }

            return results;
        }

        private void SaveHistory(IEnumerable<PersonalShopAveragePriceEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
    }
}
