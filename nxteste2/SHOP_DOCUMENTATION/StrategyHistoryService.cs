// PATHNC AUTOMATION - StrategyHistoryService.cs
//
// Busca, no historico de folhas salvas (ShopDoc), as pecas mais parecidas
// com a Work Part atual e devolve a sequencia real de operacoes usada em
// cada uma, mais um agregado (sequencia mais comum e parametros medianos
// por ferramenta). E a base do modulo Copilot: recomendacao por memoria
// da ferramentaria, nao por simulacao.
//
// Similaridade: distancia normalizada entre assinaturas geometricas
// (dimensoes ordenadas, volume, numero de faces). Score 0..1, 1 = identica.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using NXOpen;
using NXOpen.UF;

namespace PathNCAutomation.ShopDoc
{
    public class SimilarPart
    {
        public int ShopDocId;
        public string PartName;
        public int Revision;
        public double Score;            // 0..1
        public double SizeX, SizeY, SizeZ, VolumeMm3;
        public int FaceCount;
        public double TotalTimeMin;
        public DateTime CreatedAt;
        public byte[] Thumbnail;
        public List<ShopDocOperationModel> Operations = new List<ShopDocOperationModel>();
    }

    public class ToolParamSuggestion
    {
        public string ToolName;
        public double? ToolDiameter;
        public double? RpmMediana;
        public double? FeedMediana;
        public int Ocorrencias;
    }

    public class StrategySuggestion
    {
        public List<SimilarPart> Vizinhos = new List<SimilarPart>();
        public List<string> SequenciaMaisComum = new List<string>();   // tipos de operacao em ordem
        public List<ToolParamSuggestion> Parametros = new List<ToolParamSuggestion>();
        public double TempoEstimadoMin;                                   // media ponderada pelo score
    }

    public class StrategyHistoryService
    {
        private readonly string _cs;

        public StrategyHistoryService() : this(ShopDocRepository.ResolverConnectionString()) { }
        public StrategyHistoryService(string connectionString) { _cs = connectionString; }

        // Assinatura da Work Part atual (sem salvar nada)
        public static ShopDocModel AssinaturaAtual()
        {
            Session session = Session.GetSession();
            Part workPart = session.Parts.Work;
            if (workPart == null)
                throw new InvalidOperationException("Nenhuma peca aberta.");

            ShopDocModel doc = new ShopDocModel { PartName = workPart.Leaf };
            ShopDocCollector.ColetarAssinatura(UFSession.GetUFSession(), workPart, doc);
            if (doc.SizeX == null)
                throw new InvalidOperationException("A peca nao tem corpo solido para gerar a assinatura.");
            doc.Thumbnail = ShopDocCollector.CapturarThumbnail(workPart);
            return doc;
        }

        // ------------------------------------------------------------
        // Busca principal
        // ------------------------------------------------------------
        public StrategySuggestion Sugerir(ShopDocModel atual, int top = 3, double scoreMinimo = 0.5)
        {
            List<SimilarPart> candidatos = CarregarCandidatos(atual.PartName);
            foreach (SimilarPart c in candidatos)
                c.Score = CalcularScore(atual, c);

            List<SimilarPart> vizinhos = candidatos
                .Where(c => c.Score >= scoreMinimo)
                .OrderByDescending(c => c.Score)
                .Take(top)
                .ToList();

            foreach (SimilarPart v in vizinhos)
                v.Operations = CarregarOperacoes(v.ShopDocId);

            StrategySuggestion s = new StrategySuggestion { Vizinhos = vizinhos };
            if (vizinhos.Count == 0) return s;

            s.SequenciaMaisComum = SequenciaMaisComum(vizinhos);
            s.Parametros = ParametrosPorFerramenta(vizinhos);

            double somaPesos = vizinhos.Sum(v => v.Score);
            s.TempoEstimadoMin = vizinhos.Sum(v => v.TotalTimeMin * v.Score) / somaPesos;
            return s;
        }

        // ------------------------------------------------------------
        // Score: 1 - distancia normalizada. Compara dimensoes ORDENADAS
        // (independe de como a peca foi orientada), volume e faces.
        // ------------------------------------------------------------
        public static double CalcularScore(ShopDocModel a, SimilarPart b)
        {
            double[] da = Ordenado(a.SizeX.Value, a.SizeY.Value, a.SizeZ.Value);
            double[] db = Ordenado(b.SizeX, b.SizeY, b.SizeZ);

            double dDim = 0;
            for (int i = 0; i < 3; i++) dDim += Rel(da[i], db[i]);
            dDim /= 3.0;

            double dVol = Rel(Math.Pow(a.VolumeMm3 ?? 0, 1.0 / 3), Math.Pow(b.VolumeMm3, 1.0 / 3));
            double dFaces = Rel(a.FaceCount ?? 0, b.FaceCount);

            // pesos: dimensoes mandam, volume e complexidade ajustam
            double dist = 0.5 * dDim + 0.3 * dVol + 0.2 * dFaces;
            return Math.Max(0.0, 1.0 - dist);
        }

        private static double[] Ordenado(double x, double y, double z)
        {
            double[] v = { x, y, z };
            Array.Sort(v);
            return v;
        }

        // diferenca relativa simetrica, 0 = iguais, 1 = totalmente diferentes
        private static double Rel(double a, double b)
        {
            double m = Math.Max(Math.Abs(a), Math.Abs(b));
            if (m < 1e-9) return 0.0;
            return Math.Min(1.0, Math.Abs(a - b) / m);
        }

        // ------------------------------------------------------------
        // Agregados
        // ------------------------------------------------------------
        private static List<string> SequenciaMaisComum(List<SimilarPart> vizinhos)
        {
            // Usa a sequencia de tipos do vizinho de maior score como base;
            // e a recomendacao mais defensavel com poucos exemplos.
            SimilarPart melhor = vizinhos.OrderByDescending(v => v.Score).First();
            return melhor.Operations.OrderBy(o => o.Seq).Select(o => o.OperationType).ToList();
        }

        private static List<ToolParamSuggestion> ParametrosPorFerramenta(List<SimilarPart> vizinhos)
        {
            return vizinhos
                .SelectMany(v => v.Operations)
                .Where(o => !string.IsNullOrEmpty(o.ToolName))
                .GroupBy(o => o.ToolName)
                .Select(g => new ToolParamSuggestion
                {
                    ToolName = g.Key,
                    ToolDiameter = g.Select(o => o.ToolDiameter).FirstOrDefault(d => d.HasValue),
                    RpmMediana = Mediana(g.Where(o => o.Rpm.HasValue && o.Rpm > 0).Select(o => o.Rpm.Value)),
                    FeedMediana = Mediana(g.Where(o => o.FeedCut.HasValue && o.FeedCut > 0).Select(o => o.FeedCut.Value)),
                    Ocorrencias = g.Count()
                })
                .OrderByDescending(p => p.Ocorrencias)
                .ToList();
        }

        private static double? Mediana(IEnumerable<double> valores)
        {
            List<double> v = valores.OrderBy(x => x).ToList();
            if (v.Count == 0) return null;
            int mid = v.Count / 2;
            return v.Count % 2 == 1 ? v[mid] : (v[mid - 1] + v[mid]) / 2.0;
        }

        // ------------------------------------------------------------
        // Acesso a dados
        // ------------------------------------------------------------
        private List<SimilarPart> CarregarCandidatos(string partNameAtual)
        {
            const string sql = @"
SELECT ShopDocId, PartName, Revision, SizeX, SizeY, SizeZ, VolumeMm3,
       ISNULL(FaceCount, 0), ISNULL(TotalTimeMin, 0), CreatedAt, Thumbnail
FROM dbo.vw_ShopDocLatest
WHERE PartName <> @atual;";

            List<SimilarPart> lista = new List<SimilarPart>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@atual", partNameAtual ?? "");
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new SimilarPart
                        {
                            ShopDocId = r.GetInt32(0),
                            PartName = r.GetString(1),
                            Revision = r.GetInt32(2),
                            SizeX = r.GetDouble(3),
                            SizeY = r.GetDouble(4),
                            SizeZ = r.GetDouble(5),
                            VolumeMm3 = r.IsDBNull(6) ? 0 : r.GetDouble(6),
                            FaceCount = r.GetInt32(7),
                            TotalTimeMin = r.GetDouble(8),
                            CreatedAt = r.GetDateTime(9),
                            Thumbnail = r.IsDBNull(10) ? null : (byte[])r[10]
                        });
                    }
                }
            }
            return lista;
        }

        private List<ShopDocOperationModel> CarregarOperacoes(int shopDocId)
        {
            const string sql = @"
SELECT Seq, ProgramGroup, OperationName, OperationType, ToolName, ToolNumber,
       ToolDiameter, Rpm, FeedCut, Geometry, Method, ISNULL(TimeMin, 0)
FROM dbo.ShopDocOperation
WHERE ShopDocId = @id
ORDER BY Seq;";

            List<ShopDocOperationModel> ops = new List<ShopDocOperationModel>();
            using (SqlConnection cn = new SqlConnection(_cs))
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", shopDocId);
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ops.Add(new ShopDocOperationModel
                        {
                            Seq = r.GetInt32(0),
                            ProgramGroup = r.IsDBNull(1) ? null : r.GetString(1),
                            OperationName = r.GetString(2),
                            OperationType = r.IsDBNull(3) ? null : r.GetString(3),
                            ToolName = r.IsDBNull(4) ? null : r.GetString(4),
                            ToolNumber = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                            ToolDiameter = r.IsDBNull(6) ? (double?)null : r.GetDouble(6),
                            Rpm = r.IsDBNull(7) ? (double?)null : r.GetDouble(7),
                            FeedCut = r.IsDBNull(8) ? (double?)null : r.GetDouble(8),
                            Geometry = r.IsDBNull(9) ? null : r.GetString(9),
                            Method = r.IsDBNull(10) ? null : r.GetString(10),
                            TimeMin = r.GetDouble(11)
                        });
                    }
                }
            }
            return ops;
        }
    }
}