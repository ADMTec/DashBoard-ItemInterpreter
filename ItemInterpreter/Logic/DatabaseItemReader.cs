using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;

namespace ItemInterpreter.Logic
{
    public class DatabaseItemReader
    {
        private readonly string _connectionString;

        public DatabaseItemReader(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<(int Section, int Index)> DecodeItemsFromBytes(byte[] data)
        {
            var items = new List<(int, int)>();

            for (int i = 0; i + 31 < data.Length; i += 32)
            {
                byte index = data[i];
                byte typeGroup = (byte)(data[i + 9] >> 4);

                if (index != 0xFF && typeGroup != 0xFF)
                    items.Add((typeGroup, index));
            }

            return items;
        }

        public Dictionary<(int Section, int Index), int> ReadInventoryCounts()
        {
            var counts = new Dictionary<(int, int), int>();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand("SELECT Inventory FROM Character", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    byte[] raw = (byte[])reader["Inventory"];
                    foreach (var item in DecodeItemsFromBytes(raw))
                    {
                        if (!counts.ContainsKey(item))
                            counts[item] = 0;
                        counts[item]++;
                    }
                }
            }

            return counts;
        }

        public Dictionary<(int Section, int Index), int> ReadWarehouseCounts()
        {
            var counts = new Dictionary<(int, int), int>();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand("SELECT Items FROM warehouse", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    byte[] raw = (byte[])reader["Items"];
                    foreach (var item in DecodeItemsFromBytes(raw))
                    {
                        if (!counts.ContainsKey(item))
                            counts[item] = 0;
                        counts[item]++;
                    }
                }
            }

            return counts;
        }

        public long ReadTotalZenInventory()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var command = new SqlCommand("SELECT SUM(Money) FROM Inventory", connection);
            var result = command.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }

        public long ReadTotalZenWarehouse()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var command = new SqlCommand("SELECT SUM(Money) FROM Warehouse", connection);
            var result = command.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }

    }
}
