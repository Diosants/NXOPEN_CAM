using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.Json;

namespace PathNCAutomation.Copilot
{
    /// <summary>
    /// Persiste e consulta assinaturas de pecas no PathNCAutomationDB.
    /// Segue o mesmo padrao Repository usado no restante do PATHNC AUTOMATION.
    /// </summary>
    public class PartSignatureRepository
    {
        private readonly string _connectionString;

        public PartSignatureRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void SaveSignature(string partName, string partFamily, Dictionary<string, int> counts, int? featureExecutionLogId = null)
        {
            string json = JsonSerializer.Serialize(counts);
            int total = counts.Values.Sum();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO PartSignature (PartName, PartFamily, FeatureCountJson, TotalFeatureCount, FeatureExecutionLogId)
                    VALUES (@name, @family, @json, @total, @logId)", conn);

                cmd.Parameters.AddWithValue("@name", partName);
                cmd.Parameters.AddWithValue("@family", (object)partFamily ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@json", json);
                cmd.Parameters.AddWithValue("@total", total);
                cmd.Parameters.AddWithValue("@logId", (object)featureExecutionLogId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        public List<PartSignatureRecord> GetAllSignatures()
        {
            var results = new List<PartSignatureRecord>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT PartSignatureId, PartName, PartFamily, FeatureCountJson, TotalFeatureCount
                    FROM PartSignature", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new PartSignatureRecord
                        {
                            PartSignatureId = reader.GetInt32(0),
                            PartName = reader.GetString(1),
                            PartFamily = reader.IsDBNull(2) ? null : reader.GetString(2),
                            FeatureCounts = JsonSerializer.Deserialize<Dictionary<string, int>>(reader.GetString(3)),
                            TotalFeatureCount = reader.GetInt32(4)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Retorna os nomes de estrategias usadas para uma peca especifica,
        /// cruzando com o FeatureExecutionLog ja existente.
        /// Ajuste os nomes de coluna conforme o schema real do seu FeatureExecutionLog.
        /// </summary>
        public List<string> GetStrategiesUsedForPart(string partName)
        {
            var strategies = new List<string>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT DISTINCT StrategyName
                    FROM FeatureExecutionLog
                    WHERE PartName = @partName", conn);
                cmd.Parameters.AddWithValue("@partName", partName);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        strategies.Add(reader.GetString(0));
                    }
                }
            }

            return strategies;
        }
    }
}
