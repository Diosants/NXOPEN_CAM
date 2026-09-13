using System;
using System.Collections.Generic;
using System.Linq;

namespace PathNCAutomation.Copilot
{
    /// <summary>
    /// Calcula similaridade entre duas assinaturas de peca (Nivel 1: distancia
    /// euclidiana normalizada sobre o vetor de contagem de features por tipo).
    ///
    /// Score vai de 0 (nada parecido) a 1 (identico).
    /// Se depois de testar com dados reais o ranking parecer ruim, evoluir para
    /// pesos por tipo de feature ou normalizacao por TotalFeatureCount.
    /// </summary>
    public class SimilarityCalculator
    {
        public double CalculateSimilarity(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            var allKeys = a.Keys.Union(b.Keys);
            double sumSquaredDiff = 0;

            foreach (var key in allKeys)
            {
                int va = a.ContainsKey(key) ? a[key] : 0;
                int vb = b.ContainsKey(key) ? b[key] : 0;
                sumSquaredDiff += Math.Pow(va - vb, 2);
            }

            double distance = Math.Sqrt(sumSquaredDiff);
            return 1.0 / (1.0 + distance);
        }
    }
}
