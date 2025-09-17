using System;
using System.Timers;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using ItemInterpreter.Data;

namespace ItemInterpreter.Logic
{
    public class ZenWatcher
    {
        private readonly System.Timers.Timer _timer;
        private readonly string _connectionString = "Data Source=localhost;Initial Catalog=MuOnline;Integrated Security=True;TrustServerCertificate=True;";
        private long _ultimoValorTotal = -1;
        private const string ZenHistoryPath = "zen_history.json";

        public ZenWatcher()
        {
            _timer = new System.Timers.Timer(30000); // 30 segundos                                                     // Verifica a cada 30 segundos (pode ajustar)
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                long totalWarehouse = ObterZen(conn, "Warehouse");
                long totalInventory = ObterZen(conn, "Character");

                long totalAtual = totalWarehouse + totalInventory;

                if (totalAtual != _ultimoValorTotal)
                {
                    _ultimoValorTotal = totalAtual;

                    var historicoZen = File.Exists(ZenHistoryPath)
                        ? JsonSerializer.Deserialize<List<ZenTrackingLog>>(File.ReadAllText(ZenHistoryPath)) ?? new()
                        : new List<ZenTrackingLog>();

                    historicoZen.Add(new ZenTrackingLog
                    {
                        Date = DateTime.Now,
                        TotalZenWarehouse = totalWarehouse,
                        TotalZenInventory = totalInventory
                    });

                    File.WriteAllText(ZenHistoryPath, JsonSerializer.Serialize(historicoZen, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                // Log opcional
                File.AppendAllText("zen_error.log", $"[{DateTime.Now}] Erro ao verificar Zen: {ex.Message}\n");
            }
        }

        private long ObterZen(SqlConnection conn, string tabela)
        {
            using var cmd = new SqlCommand($"SELECT CAST(SUM(CAST(Money AS BIGINT)) AS BIGINT) FROM {tabela}", conn);
            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToInt64(result) : 0;
        }
    }
}
