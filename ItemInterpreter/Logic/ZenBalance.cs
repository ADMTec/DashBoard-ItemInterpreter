using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemInterpreter.Logic
{
    public class ZenBalance
    {
        public DateTime Date { get; set; }
        public long TotalZen { get; set; }

        public static List<(string Date, long TotalZen)> CalcularZenDiario(string connectionString)
        {
            var resultados = new List<(string Date, long TotalZen)>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
            SELECT CONVERT(date, MI.ConnectTM) as Dia, SUM(CAST(C.Money AS BIGINT)) as TotalZen
            FROM [dbo].[MEMB_INFO] MI
            JOIN [dbo].[Character] C ON C.AccountID = MI.memb___id
            GROUP BY CONVERT(date, MI.ConnectTM)
            ORDER BY Dia ASC
        ", conn);

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var date = reader.GetDateTime(0).ToString("yyyy-MM-dd");
                    var total = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    resultados.Add((date, total));
                }
            }

            return resultados;
        }

    }
}
