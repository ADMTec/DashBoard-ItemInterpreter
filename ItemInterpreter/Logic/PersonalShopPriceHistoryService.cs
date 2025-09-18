using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using ItemInterpreter.Data;
using ItemInterpreter.Loaders;
using Microsoft.Data.SqlClient;

namespace ItemInterpreter.Logic
{
    public class PersonalShopPriceHistoryService
    {
        private readonly string _connectionString;
        private readonly string _historyPath;
        private readonly int _maxEntriesPerItem;
        private readonly Dictionary<(int Section, int Index), string> _itemNames;

        public PersonalShopPriceHistoryService(
            string connectionString,
            string? historyPath = null,
            int maxEntriesPerItem = 365,
            IEnumerable<ItemDefinition>? itemDefinitions = null)
        {
            _connectionString = connectionString;
            _historyPath = historyPath ?? "personalshop_price_history.json";
            _maxEntriesPerItem = maxEntriesPerItem;

            var definitions = itemDefinitions?.ToList() ?? ItemXmlLoader.Load("IGC_ItemList.xml");
            _itemNames = definitions
                .GroupBy(d => (d.Section, d.Index))
                .ToDictionary(group => group.Key, group => group.First().Name);
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

        public List<PersonalShopSaleEntry> GetRecentSales(int take = 20, IEnumerable<(int Section, int Index)>? filter = null)
        {
            var tracked = filter?.Distinct().ToList() ?? new List<(int Section, int Index)>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;

            command.Parameters.Add(new SqlParameter("@Take", SqlDbType.Int) { Value = Math.Max(take, 1) });

            var filters = new List<string>();
            for (int i = 0; i < tracked.Count; i++)
            {
                var sectionParam = $"@section{i}";
                var indexParam = $"@index{i}";
                filters.Add($"(l.[Section] = {sectionParam} AND l.[IndexID] = {indexParam})");
                command.Parameters.AddWithValue(sectionParam, tracked[i].Section);
                command.Parameters.AddWithValue(indexParam, tracked[i].Index);
            }

            var filterClause = filters.Count > 0
                ? $"WHERE {string.Join(" OR ", filters)}"
                : string.Empty;

            command.CommandText = $@"
SELECT TOP (@Take)
    l.[ItemSerial],
    l.[Section],
    l.[IndexID],
    l.[PriceZen],
    l.[Buyer],
    l.[Seller],
    l.[Date],
    v.[Money] AS AlternativePrice,
    stats.AvgPrice AS WindowAverage,
    stats.SaleCount AS WindowSaleCount
FROM [dbo].[T_PSHOP_SALE_LOG] AS l
LEFT JOIN [dbo].[T_PSHOP_ITEMVALUE_INFO] AS v ON l.[ItemSerial] = v.[ItemSerial]
OUTER APPLY (
    SELECT
        AVG(CAST(CASE WHEN v2.[Money] > 0 THEN v2.[Money] ELSE l2.[PriceZen] END AS DECIMAL(18, 2))) AS AvgPrice,
        COUNT(*) AS SaleCount
    FROM [dbo].[T_PSHOP_SALE_LOG] AS l2
    LEFT JOIN [dbo].[T_PSHOP_ITEMVALUE_INFO] AS v2 ON l2.[ItemSerial] = v2.[ItemSerial]
    WHERE l2.[Section] = l.[Section]
      AND l2.[IndexID] = l.[IndexID]
      AND l2.[Date] >= DATEADD(DAY, -30, l.[Date])
) stats
{filterClause}
ORDER BY l.[Date] DESC;";

            var results = new List<PersonalShopSaleEntry>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var section = reader.GetInt32(1);
                var index = reader.GetInt32(2);

                results.Add(new PersonalShopSaleEntry
                {
                    ItemSerial = reader.GetInt64(0),
                    Section = section,
                    Index = index,
                    PriceZen = reader.GetInt64(3),
                    Buyer = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Seller = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Date = reader.GetDateTime(6),
                    AlternativePrice = reader.IsDBNull(7) ? null : Convert.ToInt64(reader.GetInt32(7)),
                    AveragePriceWindow = reader.IsDBNull(8) ? null : Convert.ToDouble(reader.GetDecimal(8)),
                    WindowSaleCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    ItemName = ResolveItemName(section, index)
                });
            }

            return results;
        }

        public List<HotItemSummary> GetHotItemSummaries(TimeSpan window, int take = 5, TimeSpan? comparisonWindow = null, IEnumerable<(int Section, int Index)>? filter = null)
        {
            var tracked = filter?.Distinct().ToList() ?? new List<(int Section, int Index)>();
            var filters = new List<string>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;

            command.Parameters.Add(new SqlParameter("@Take", SqlDbType.Int) { Value = Math.Max(take, 1) });

            var windowDays = Math.Max(1, (int)window.TotalDays);
            var comparison = comparisonWindow ?? window;
            var comparisonDays = Math.Max(1, (int)comparison.TotalDays);

            var startRecent = DateTime.Now.Date.AddDays(-windowDays);
            var startPrevious = startRecent.AddDays(-comparisonDays);

            command.Parameters.Add(new SqlParameter("@StartRecent", SqlDbType.DateTime2) { Value = startRecent });
            command.Parameters.Add(new SqlParameter("@StartPrevious", SqlDbType.DateTime2) { Value = startPrevious });

            for (int i = 0; i < tracked.Count; i++)
            {
                var sectionParam = $"@hotSection{i}";
                var indexParam = $"@hotIndex{i}";
                filters.Add($"(l.[Section] = {sectionParam} AND l.[IndexID] = {indexParam})");
                command.Parameters.AddWithValue(sectionParam, tracked[i].Section);
                command.Parameters.AddWithValue(indexParam, tracked[i].Index);
            }

            var filterClause = filters.Count > 0
                ? $"AND ({string.Join(" OR ", filters)})"
                : string.Empty;

            command.CommandText = $@"
WITH Recent AS (
    SELECT
        l.[Section],
        l.[IndexID],
        COUNT(*) AS SaleCount,
        AVG(CAST(CASE WHEN v.[Money] > 0 THEN v.[Money] ELSE l.[PriceZen] END AS DECIMAL(18, 2))) AS AvgPrice,
        STDEV(CAST(CASE WHEN v.[Money] > 0 THEN v.[Money] ELSE l.[PriceZen] END AS DECIMAL(18, 2))) AS PriceStdDev
    FROM [dbo].[T_PSHOP_SALE_LOG] AS l
    LEFT JOIN [dbo].[T_PSHOP_ITEMVALUE_INFO] AS v ON l.[ItemSerial] = v.[ItemSerial]
    WHERE l.[Date] >= @StartRecent {filterClause}
    GROUP BY l.[Section], l.[IndexID]
), Previous AS (
    SELECT
        l.[Section],
        l.[IndexID],
        AVG(CAST(CASE WHEN v.[Money] > 0 THEN v.[Money] ELSE l.[PriceZen] END AS DECIMAL(18, 2))) AS AvgPrice
    FROM [dbo].[T_PSHOP_SALE_LOG] AS l
    LEFT JOIN [dbo].[T_PSHOP_ITEMVALUE_INFO] AS v ON l.[ItemSerial] = v.[ItemSerial]
    WHERE l.[Date] >= @StartPrevious AND l.[Date] < @StartRecent {filterClause}
    GROUP BY l.[Section], l.[IndexID]
)
SELECT TOP (@Take)
    r.[Section],
    r.[IndexID],
    r.SaleCount,
    r.AvgPrice,
    r.PriceStdDev,
    p.AvgPrice AS PreviousAvgPrice
FROM Recent AS r
LEFT JOIN Previous AS p ON p.[Section] = r.[Section] AND p.[IndexID] = r.[IndexID]
ORDER BY r.SaleCount DESC, r.AvgPrice DESC;";

            var result = new List<HotItemSummary>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var section = reader.GetInt32(0);
                var index = reader.GetInt32(1);
                var avgPrice = reader.IsDBNull(3) ? 0d : Convert.ToDouble(reader.GetDecimal(3));
                var previousAverage = reader.IsDBNull(5) ? (double?)null : Convert.ToDouble(reader.GetDecimal(5));
                var stdDev = reader.IsDBNull(4) ? (double?)null : Convert.ToDouble(reader.GetDouble(4));

                double changePercent = 0d;
                if (previousAverage.HasValue && previousAverage.Value != 0)
                {
                    changePercent = (avgPrice - previousAverage.Value) / previousAverage.Value * 100d;
                }

                var isOutlier = stdDev.HasValue && avgPrice > 0 && stdDev.Value >= avgPrice * 0.5;

                result.Add(new HotItemSummary
                {
                    Section = section,
                    Index = index,
                    ItemName = ResolveItemName(section, index),
                    TotalSales = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    AveragePrice = avgPrice,
                    PreviousAveragePrice = previousAverage,
                    PriceStandardDeviation = stdDev,
                    PriceChangePercent = changePercent,
                    IsOutlier = isOutlier,
                    Window = TimeSpan.FromDays(windowDays)
                });
            }

            return result;
        }

        private string ResolveItemName(int section, int index)
        {
            return _itemNames.TryGetValue((section, index), out var name)
                ? name
                : $"ITEMGET({section},{index})";
        }
    }
}
