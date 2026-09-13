// PATHNC AUTOMATION - QuoteService.cs
//
// Orcamento e estimativa de prazo a partir do historico (ShopDoc):
//
//   tempo usinagem = media ponderada (pelo score) do tempo das pecas
//                    similares, usando ActualTimeMin quando existe e
//                    TotalTimeMin * fatorCorrecao quando nao existe
//   fatorCorrecao  = mediana de (ActualTimeMin / TotalTimeMin) em todas as
//                    pecas que tem os dois (vw_TimeCalibration); 1.0 se
//                    ainda nao houver nenhuma
//   setup          = mediana de ActualSetupMin do historico, ou DefaultSetupMin
//   programacao    = n operacoes (da melhor vizinha) * ProgrammingMinPerOp
//   custo          = (usinagem+setup) * hora-maquina + programacao * hora-CAM
//   preco          = custo * (1 + margem)
//   prazo (dias)   = QueueDays + (usinagem*qtd + setup) / (60*MachineHoursPerDay)
//                    + programacao / (60*8)

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace PathNCAutomation.ShopDoc
{
    public class QuoteSettings
    {
        public string ProfileName = "DEFAULT";
        public string Currency = "BRL";
        public double MachineRatePerHour = 180;
        public double ProgrammingRatePerHour = 150;
        public double DefaultSetupMin = 45;
        public double ProgrammingMinPerOp = 12;
        public double MarginPercent = 25;
        public double MachineHoursPerDay = 8;
        public double QueueDays = 2;
    }

    public class QuoteEstimate
    {
        public string PartName;
        public int Quantity;
        public QuoteSettings Settings;

        public List<SimilarPart> Base = new List<SimilarPart>();
        public double CorrectionFactor;
        public int CalibrationSamples;      // quantas pecas tem tempo real

        public double EstMachiningMin;      // por peca
        public double EstSetupMin;          // por lote
        public double EstProgrammingMin;    // por lote
        public int OperationCount;

        public double MachineCost;          // lote
        public double ProgrammingCost;      // lote
        public double Cost { get { return MachineCost + ProgrammingCost; } }
        public double TotalPrice;           // lote com margem
        public double UnitPrice;
        public double LeadTimeDays;

        public string ConfidenceLabel
        {
            get
            {
                if (Base.Count == 0) return "no data";
                double best = Base[0].Score;
                if (CalibrationSamples >= 5 && best >= 0.8) return "high";
                if (CalibrationSamples >= 1 && best >= 0.6) return "medium";
                return "low";
            }
        }

        public string SimilarPartsLabel
        {
            get { return string.Join(", ", Base.Select(b => string.Format("{0} ({1:P0})", b.PartName, b.Score))); }
        }
    }

    public class QuoteRecord
    {
        public int QuoteId;
        public string PartName, CustomerName, Currency, SimilarParts, Notes, CreatedBy;
        public int Quantity;
        public double UnitPrice, TotalPrice, LeadTimeDays, EstMachiningMin, EstSetupMin,
                      EstProgrammingMin, MachineCost, ProgrammingCost, MarginPercent, CorrectionFactor;
        public DateTime CreatedAt;
    }

    public class QuoteService
    {
        private readonly string _cs;
        public QuoteService() : this(ShopDocRepository.ResolverConnectionString()) { }
        public QuoteService(string cs) { _cs = cs; }

        // ------------------------------------------------------------
        // Estimativa
        // ------------------------------------------------------------
        public QuoteEstimate Estimate(ShopDocModel atual, StrategySuggestion sug, int quantity, QuoteSettings s)
        {
            if (quantity < 1) quantity = 1;
            QuoteEstimate q = new QuoteEstimate
            {
                PartName = atual.PartName,
                Quantity = quantity,
                Settings = s,
                Base = sug.Vizinhos.ToList()
            };

            List<double> ratios = LoadCalibrationRatios();
            q.CalibrationSamples = ratios.Count;
            q.CorrectionFactor = ratios.Count > 0 ? Mediana(ratios) : 1.0;

            if (q.Base.Count == 0)
                throw new InvalidOperationException("No similar parts found — cannot estimate. Save more parts or lower the similarity threshold.");

            // tempo de usinagem por peca: real se houver, senao estimado corrigido
            Dictionary<int, double?> actuals = LoadActualTimes(q.Base.Select(b => b.ShopDocId));
            double somaPesos = 0, somaTempo = 0;
            foreach (SimilarPart b in q.Base)
            {
                double? real = actuals.ContainsKey(b.ShopDocId) ? actuals[b.ShopDocId] : null;
                double t = real.HasValue && real.Value > 0 ? real.Value : b.TotalTimeMin * q.CorrectionFactor;
                somaTempo += t * b.Score;
                somaPesos += b.Score;
            }
            q.EstMachiningMin = somaPesos > 0 ? somaTempo / somaPesos : 0;

            List<double> setups = LoadActualSetups();
            q.EstSetupMin = setups.Count > 0 ? Mediana(setups) : s.DefaultSetupMin;

            q.OperationCount = q.Base[0].Operations.Count;
            q.EstProgrammingMin = q.OperationCount * s.ProgrammingMinPerOp;

            // custos
            double machineMin = q.EstMachiningMin * quantity + q.EstSetupMin;
            q.MachineCost = machineMin / 60.0 * s.MachineRatePerHour;
            q.ProgrammingCost = q.EstProgrammingMin / 60.0 * s.ProgrammingRatePerHour;
            q.TotalPrice = q.Cost * (1.0 + s.MarginPercent / 100.0);
            q.UnitPrice = q.TotalPrice / quantity;

            // prazo
            double machineDays = machineMin / (60.0 * Math.Max(1.0, s.MachineHoursPerDay));
            double progDays = q.EstProgrammingMin / (60.0 * 8.0);
            q.LeadTimeDays = Math.Ceiling(s.QueueDays + progDays + machineDays);

            return q;
        }

        // ------------------------------------------------------------
        // Persistencia
        // ------------------------------------------------------------
        public int SaveQuote(QuoteEstimate q, ShopDocModel atual, string customer, string notes)
        {
            const string sql = @"
INSERT INTO dbo.Quote
 (PartName, CustomerName, Quantity, SizeX, SizeY, SizeZ, VolumeMm3, FaceCount,
  SimilarParts, CorrectionFactor, EstMachiningMin, EstSetupMin, EstProgrammingMin,
  Currency, MachineCost, ProgrammingCost, MarginPercent, TotalPrice, UnitPrice,
  LeadTimeDays, Notes, CreatedBy)
OUTPUT INSERTED.QuoteId
VALUES
 (@PartName, @Customer, @Qty, @SX, @SY, @SZ, @Vol, @Faces,
  @Similar, @Corr, @Mach, @Setup, @Prog,
  @Cur, @MachCost, @ProgCost, @Margin, @Total, @Unit,
  @Lead, @Notes, @By);";

            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@PartName", q.PartName);
                cmd.Parameters.AddWithValue("@Customer", (object)customer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Qty", q.Quantity);
                cmd.Parameters.AddWithValue("@SX", (object)atual.SizeX ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SY", (object)atual.SizeY ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SZ", (object)atual.SizeZ ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Vol", (object)atual.VolumeMm3 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Faces", (object)atual.FaceCount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Similar", q.SimilarPartsLabel);
                cmd.Parameters.AddWithValue("@Corr", q.CorrectionFactor);
                cmd.Parameters.AddWithValue("@Mach", q.EstMachiningMin);
                cmd.Parameters.AddWithValue("@Setup", q.EstSetupMin);
                cmd.Parameters.AddWithValue("@Prog", q.EstProgrammingMin);
                cmd.Parameters.AddWithValue("@Cur", q.Settings.Currency);
                cmd.Parameters.AddWithValue("@MachCost", q.MachineCost);
                cmd.Parameters.AddWithValue("@ProgCost", q.ProgrammingCost);
                cmd.Parameters.AddWithValue("@Margin", q.Settings.MarginPercent);
                cmd.Parameters.AddWithValue("@Total", q.TotalPrice);
                cmd.Parameters.AddWithValue("@Unit", q.UnitPrice);
                cmd.Parameters.AddWithValue("@Lead", q.LeadTimeDays);
                cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@By", Environment.UserName);
                cn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }

        // Registra o tempo real na ULTIMA revisao da peca
        public bool RegisterActualTime(string partName, double actualMin, double? setupMin, string note)
        {
            const string sql = @"
UPDATE dbo.ShopDoc SET ActualTimeMin = @t, ActualSetupMin = @s, ActualTimeNote = @n
WHERE ShopDocId = (SELECT TOP 1 ShopDocId FROM dbo.ShopDoc WHERE PartName = @p ORDER BY Revision DESC);";
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@t", actualMin);
                cmd.Parameters.AddWithValue("@s", (object)setupMin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", (object)note ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@p", partName);
                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // Lista orcamentos salvos (mais recente primeiro); filtro opcional por peca/cliente
        public List<QuoteRecord> ListQuotes(string filtro = null, int max = 500)
        {
            const string sql = @"
SELECT TOP (@max) QuoteId, PartName, CustomerName, Quantity, Currency, UnitPrice, TotalPrice, LeadTimeDays,
       EstMachiningMin, EstSetupMin, EstProgrammingMin, MachineCost, ProgrammingCost, MarginPercent,
       CorrectionFactor, SimilarParts, Notes, CreatedBy, CreatedAt
FROM dbo.Quote
WHERE (@f IS NULL OR PartName LIKE @f OR CustomerName LIKE @f)
ORDER BY CreatedAt DESC;";
            List<QuoteRecord> r = new List<QuoteRecord>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@max", max);
                cmd.Parameters.AddWithValue("@f", string.IsNullOrWhiteSpace(filtro) ? (object)DBNull.Value : "%" + filtro.Trim() + "%");
                cn.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        r.Add(new QuoteRecord
                        {
                            QuoteId = rd.GetInt32(0),
                            PartName = rd.GetString(1),
                            CustomerName = rd.IsDBNull(2) ? "" : rd.GetString(2),
                            Quantity = rd.GetInt32(3),
                            Currency = rd.GetString(4),
                            UnitPrice = rd.GetDouble(5),
                            TotalPrice = rd.GetDouble(6),
                            LeadTimeDays = rd.GetDouble(7),
                            EstMachiningMin = rd.GetDouble(8),
                            EstSetupMin = rd.GetDouble(9),
                            EstProgrammingMin = rd.GetDouble(10),
                            MachineCost = rd.GetDouble(11),
                            ProgrammingCost = rd.GetDouble(12),
                            MarginPercent = rd.GetDouble(13),
                            CorrectionFactor = rd.GetDouble(14),
                            SimilarParts = rd.IsDBNull(15) ? "" : rd.GetString(15),
                            Notes = rd.IsDBNull(16) ? "" : rd.GetString(16),
                            CreatedBy = rd.IsDBNull(17) ? "" : rd.GetString(17),
                            CreatedAt = rd.GetDateTime(18),
                        });
                    }
                }
            }
            return r;
        }

        public List<string> ListPartNames()
        {
            List<string> r = new List<string>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand("SELECT DISTINCT PartName FROM dbo.ShopDoc ORDER BY PartName", cn))
            {
                cn.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read()) r.Add(rd.GetString(0));
            }
            return r;
        }

        public QuoteSettings LoadSettings(string profile = "DEFAULT")
        {
            const string sql = @"SELECT Currency, MachineRatePerHour, ProgrammingRatePerHour, DefaultSetupMin,
ProgrammingMinPerOp, MarginPercent, MachineHoursPerDay, QueueDays FROM dbo.QuoteSettings WHERE ProfileName = @p";
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@p", profile);
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return new QuoteSettings();
                    return new QuoteSettings
                    {
                        ProfileName = profile,
                        Currency = r.GetString(0),
                        MachineRatePerHour = r.GetDouble(1),
                        ProgrammingRatePerHour = r.GetDouble(2),
                        DefaultSetupMin = r.GetDouble(3),
                        ProgrammingMinPerOp = r.GetDouble(4),
                        MarginPercent = r.GetDouble(5),
                        MachineHoursPerDay = r.GetDouble(6),
                        QueueDays = r.GetDouble(7),
                    };
                }
            }
        }

        public void SaveSettings(QuoteSettings s)
        {
            const string sql = @"
UPDATE dbo.QuoteSettings SET Currency=@c, MachineRatePerHour=@m, ProgrammingRatePerHour=@pr,
 DefaultSetupMin=@setup, ProgrammingMinPerOp=@pmo, MarginPercent=@mg, MachineHoursPerDay=@h,
 QueueDays=@q, UpdatedAt=SYSUTCDATETIME()
WHERE ProfileName=@p;
IF @@ROWCOUNT = 0
 INSERT INTO dbo.QuoteSettings (ProfileName, Currency, MachineRatePerHour, ProgrammingRatePerHour,
  DefaultSetupMin, ProgrammingMinPerOp, MarginPercent, MachineHoursPerDay, QueueDays)
 VALUES (@p, @c, @m, @pr, @setup, @pmo, @mg, @h, @q);";
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@p", s.ProfileName);
                cmd.Parameters.AddWithValue("@c", s.Currency);
                cmd.Parameters.AddWithValue("@m", s.MachineRatePerHour);
                cmd.Parameters.AddWithValue("@pr", s.ProgrammingRatePerHour);
                cmd.Parameters.AddWithValue("@setup", s.DefaultSetupMin);
                cmd.Parameters.AddWithValue("@pmo", s.ProgrammingMinPerOp);
                cmd.Parameters.AddWithValue("@mg", s.MarginPercent);
                cmd.Parameters.AddWithValue("@h", s.MachineHoursPerDay);
                cmd.Parameters.AddWithValue("@q", s.QueueDays);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------------
        private List<double> LoadCalibrationRatios()
        {
            List<double> r = new List<double>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand("SELECT Ratio FROM dbo.vw_TimeCalibration WHERE Ratio > 0", cn))
            {
                cn.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read()) if (!rd.IsDBNull(0)) r.Add(rd.GetDouble(0));
            }
            return r;
        }

        private List<double> LoadActualSetups()
        {
            List<double> r = new List<double>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand("SELECT ActualSetupMin FROM dbo.ShopDoc WHERE ActualSetupMin > 0", cn))
            {
                cn.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read()) r.Add(rd.GetDouble(0));
            }
            return r;
        }

        private Dictionary<int, double?> LoadActualTimes(IEnumerable<int> ids)
        {
            Dictionary<int, double?> d = new Dictionary<int, double?>();
            List<int> lista = ids.ToList();
            if (lista.Count == 0) return d;
            string inList = string.Join(",", lista);
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(
                "SELECT ShopDocId, ActualTimeMin FROM dbo.ShopDoc WHERE ShopDocId IN (" + inList + ")", cn))
            {
                cn.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read())
                        d[rd.GetInt32(0)] = rd.IsDBNull(1) ? (double?)null : rd.GetDouble(1);
            }
            return d;
        }

        private static double Mediana(List<double> v)
        {
            List<double> s = v.OrderBy(x => x).ToList();
            int m = s.Count / 2;
            return s.Count % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2.0;
        }
    }
}