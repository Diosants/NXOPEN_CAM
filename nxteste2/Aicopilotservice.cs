
using System.Collections.Generic;
using System.Linq;

namespace PathNCAutomation.Copilot
{
    /// <summary>
    /// Orquestra o fluxo do Copilot: recebe a assinatura da peca atual,
    /// busca pecas similares no historico e sugere estrategias por frequencia de uso.
    /// </summary>
    public class AiCopilotService
    {
        private readonly PartSignatureRepository _signatureRepo;
        private readonly SimilarityCalculator _similarity;

        public AiCopilotService(PartSignatureRepository signatureRepo, SimilarityCalculator similarity)
        {
            _signatureRepo = signatureRepo;
            _similarity = similarity;
        }

        public List<CopilotSuggestion> GetSuggestions(Dictionary<string, int> currentPartSignature, int topN = 5)
        {
            var allSignatures = _signatureRepo.GetAllSignatures();

            var ranked = allSignatures
                .Select(s => new
                {
                    Signature = s,
                    Score = _similarity.CalculateSimilarity(currentPartSignature, s.FeatureCounts)
                })
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToList();

            var strategyFrequency = new Dictionary<string, int>();

            foreach (var item in ranked)
            {
                var strategiesUsed = _signatureRepo.GetStrategiesUsedForPart(item.Signature.PartName);
                foreach (var strat in strategiesUsed)
                {
                    if (!strategyFrequency.ContainsKey(strat))
                        strategyFrequency[strat] = 0;
                    strategyFrequency[strat]++;
                }
            }

            return strategyFrequency
                .Select(kv => new CopilotSuggestion
                {
                    StrategyName = kv.Key,
                    MatchCount = kv.Value,
                    TotalSimilarParts = ranked.Count
                })
                .OrderByDescending(s => s.MatchCount)
                .ToList();
        }
    }
}
