using System.Collections.Generic;
using NXOpen;

namespace PathNCAutomation.Copilot
{
    /// <summary>
    /// Extrai a assinatura (contagem de features por tipo/cor) da peca carregada.
    ///
    /// IMPORTANTE: nao duplique a logica de classificacao por cor aqui.
    /// Esta classe deve CHAMAR o classificador que ja existe em
    /// ClassifyMillingGroup / HoleDiameterColorizer, nao reimplementar.
    /// O metodo ClassifyFaceByColor abaixo e um placeholder — troque pela
    /// chamada real ao seu classificador existente.
    /// </summary>
    public class PartSignatureExtractor
    {
        public Dictionary<string, int> ExtractFeatureCounts(Part workPart)
        {
            var counts = new Dictionary<string, int>();

            foreach (Body body in workPart.Bodies)
            {
                foreach (Face face in body.GetFaces())
                {
                    string featureType = ClassifyFaceByColor(face);
                    if (featureType == null) continue;

                    if (!counts.ContainsKey(featureType))
                        counts[featureType] = 0;
                    counts[featureType]++;
                }
            }

            return counts;
        }

        /// <summary>
        /// PLACEHOLDER — substitua pela chamada ao seu classificador real
        /// (ex: MillingGroupClassifier.Classify(face) ou equivalente).
        /// Mantenha o mapeamento cor->feature centralizado em UM lugar so,
        /// tanto o Copilot quanto o AutoDrill/FBM devem consumir a mesma fonte.
        /// </summary>
        private string ClassifyFaceByColor(Face face)
        {
            // TODO: trocar por chamada real, ex:
            // return MillingGroupClassifier.Classify(face);
            return null;
        }
    }
}
