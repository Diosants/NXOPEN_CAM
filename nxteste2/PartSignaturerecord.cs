using System.Collections.Generic;

namespace PathNCAutomation.Copilot
{
    /// <summary>
    /// Representa a assinatura de uma peca ja usinada: contagem de features por tipo,
    /// usada para calcular similaridade com a peca atualmente carregada no NX.
    /// </summary>
    public class PartSignatureRecord
    {
        public int PartSignatureId { get; set; }
        public string PartName { get; set; }
        public string PartFamily { get; set; }
        public Dictionary<string, int> FeatureCounts { get; set; }
        public int TotalFeatureCount { get; set; }
    }

    /// <summary>
    /// Uma sugestao do Copilot: uma estrategia e a frequencia com que ela
    /// apareceu nas pecas similares encontradas no historico.
    /// </summary>
    public class CopilotSuggestion
    {
        public string StrategyName { get; set; }
        public int MatchCount { get; set; }
        public int TotalSimilarParts { get; set; }

        public int MatchPercentage =>
            TotalSimilarParts == 0 ? 0 : (int)(100.0 * MatchCount / TotalSimilarParts);
    }
}
