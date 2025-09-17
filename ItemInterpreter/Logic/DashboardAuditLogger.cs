using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ItemInterpreter.Logic
{
    public class DashboardAuditLogger
    {
        private readonly string _auditPath;

        public DashboardAuditLogger(string? auditPath = null)
        {
            _auditPath = auditPath ?? "dashboard_audit.json";
        }

        public void AppendEntry(SyncAuditEntry entry)
        {
            var entries = ReadEntries();
            entries.Add(entry);

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_auditPath, json);
        }

        public List<SyncAuditEntry> ReadEntries()
        {
            if (!File.Exists(_auditPath))
            {
                return new List<SyncAuditEntry>();
            }

            try
            {
                var json = File.ReadAllText(_auditPath);
                return JsonSerializer.Deserialize<List<SyncAuditEntry>>(json) ?? new List<SyncAuditEntry>();
            }
            catch
            {
                return new List<SyncAuditEntry>();
            }
        }
    }

    public class SyncAuditEntry
    {
        public DateTime Timestamp { get; set; }
        public long TotalZen { get; set; }
        public List<SyncAuditItemDetail> Items { get; set; } = new();
        public List<string> Alerts { get; set; } = new();
    }

    public class SyncAuditItemDetail
    {
        public string ItemName { get; set; } = string.Empty;
        public int Section { get; set; }
        public int Index { get; set; }
        public int InventoryCount { get; set; }
        public int WarehouseCount { get; set; }
        public int TotalCount { get; set; }
    }
}
