// PATHNC AUTOMATION - StrategyApplier.cs
//
// Aplica na Work Part atual a estrategia de uma peca similar do historico:
//   - cria um grupo de programa COPILOT_<peca_origem>
//   - para cada operacao do historico, na mesma ordem:
//       . resolve o template/subtipo NX a partir do OperationType salvo
//       . garante a ferramenta (reaproveita se existir no setup; senao cria
//         uma fresa generica com o mesmo nome e diametro)
//       . resolve o metodo (mesmo nome do historico, senao METHOD)
//       . cria a operacao com WORKPIECE como geometria
//       . aplica RPM e avanco do historico
//   - NAO seleciona geometria nem gera caminho: isso fica para o programador
//     ou para os wizards FBM. O resultado e um esqueleto pronto para editar.
//
// Tipos de operacao nao mapeados sao pulados e listados no resumo.

using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;

namespace PathNCAutomation.ShopDoc
{
    public class ApplyResult
    {
        public string ProgramGroupName;
        public List<string> OperacoesCriadas = new List<string>();
        public List<string> FerramentasCriadas = new List<string>();
        public List<string> Puladas = new List<string>();      // "OP_NAME (tipo X nao mapeado)"
        public List<string> Avisos = new List<string>();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Program group: " + ProgramGroupName);
            sb.AppendLine("Operations created: " + OperacoesCriadas.Count);
            foreach (string s in OperacoesCriadas) sb.AppendLine("   • " + s);
            if (FerramentasCriadas.Count > 0)
            {
                sb.AppendLine("Tools created (check diameter/flutes): " + FerramentasCriadas.Count);
                foreach (string s in FerramentasCriadas) sb.AppendLine("   • " + s);
            }
            if (Puladas.Count > 0)
            {
                sb.AppendLine("Skipped: " + Puladas.Count);
                foreach (string s in Puladas) sb.AppendLine("   • " + s);
            }
            if (Avisos.Count > 0)
            {
                sb.AppendLine("Warnings:");
                foreach (string s in Avisos) sb.AppendLine("   • " + s);
            }
            sb.AppendLine();
            sb.AppendLine("Next step: open each operation and select its geometry, then Generate.");
            return sb.ToString();
        }
    }

    public class StrategyApplier
    {
        // ------------------------------------------------------------
        // Mapa OperationType (nome da classe NXOpen.CAM salvo no historico)
        // -> (template, subtipo) usados em CreateWithUserName.
        // Ajuste/complete conforme os templates da sua instalacao.
        // ------------------------------------------------------------
        private static readonly Dictionary<string, Tuple<string, string>> TemplateMap =
            new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "CavityMilling",          Tuple.Create("mill_contour", "CAVITY_MILL") },
            { "VolumeBased25DMilling",  Tuple.Create("mill_contour", "ADAPTIVE_MILLING") },
            { "ZLevelMilling",          Tuple.Create("mill_contour", "ZLEVEL_PROFILE") },
            { "SurfaceContour",         Tuple.Create("mill_contour", "FIXED_CONTOUR") },
            { "PlanarMilling",          Tuple.Create("mill_planar",  "PLANAR_MILL") },
            { "FaceMilling",            Tuple.Create("mill_planar",  "FACE_MILLING") },
            { "PlanarProfile",          Tuple.Create("mill_planar",  "PLANAR_PROFILE") },
            { "FeatureMilling",         Tuple.Create("mill_planar",  "FLOOR_WALL") },
            { "HoleDrilling",           Tuple.Create("hole_making",  "DRILLING") },
            { "HoleMaking",             Tuple.Create("hole_making",  "DRILLING") },
            { "CylinderMilling",        Tuple.Create("hole_making",  "HOLE_MILLING") },
            { "ThreadMilling",          Tuple.Create("hole_making",  "THREAD_MILLING") },
            { "ChamferMilling",         Tuple.Create("hole_making",  "CHAMFER_MILLING") },
            { "Tapping",                Tuple.Create("hole_making",  "TAPPING") },
        };

        private const string WORKPIECE_NAME = "WORKPIECE";
        private const string DEFAULT_METHOD = "METHOD";

        public ApplyResult Apply(SimilarPart origem)
        {
            Session session = Session.GetSession();
            UFSession uf = UFSession.GetUFSession();
            Part workPart = session.Parts.Work;
            if (workPart == null || workPart.CAMSetup == null)
                throw new InvalidOperationException("Open a part with a CAM setup first.");

            CAMSetup setup = workPart.CAMSetup;
            ApplyResult result = new ApplyResult();

            Session.UndoMarkId mark = session.SetUndoMark(Session.MarkVisibility.Visible,
                "Copilot: apply strategy from " + origem.PartName);
            try
            {
                // ---------- grupos base ----------
                FeatureGeometry workpiece = FindGroup<FeatureGeometry>(setup, WORKPIECE_NAME);
                if (workpiece == null)
                    throw new InvalidOperationException("Geometry group '" + WORKPIECE_NAME + "' not found in this setup.");

                NCGroup programRoot = setup.GetRoot(CAMSetup.View.ProgramOrder);
                NCGroup toolRoot = setup.GetRoot(CAMSetup.View.MachineTool);

                string groupName = NomeUnicoGrupo(setup, "COPILOT_" + Sanitize(origem.PartName));
                NCGroup programGroup = setup.CAMGroupCollection.CreateProgram(
                    programRoot, "mill_planar", "PROGRAM",
                    NCGroupCollection.UseDefaultName.False, groupName);
                result.ProgramGroupName = groupName;

                // ---------- operacoes ----------
                foreach (ShopDocOperationModel h in origem.Operations.OrderBy(o => o.Seq))
                {
                    Tuple<string, string> tpl;
                    if (string.IsNullOrEmpty(h.OperationType) || !TemplateMap.TryGetValue(h.OperationType, out tpl))
                    {
                        result.Puladas.Add(h.OperationName + " (type '" + h.OperationType + "' not mapped)");
                        continue;
                    }

                    NXOpen.CAM.Tool tool = GarantirFerramenta(setup, uf, toolRoot, h, result);
                    if (tool == null)
                    {
                        result.Puladas.Add(h.OperationName + " (no tool)");
                        continue;
                    }

                    Method method = FindGroup<Method>(setup, h.Method) ?? FindGroup<Method>(setup, DEFAULT_METHOD);
                    if (method == null)
                    {
                        result.Puladas.Add(h.OperationName + " (method not found)");
                        continue;
                    }

                    string opName = NomeUnicoOperacao(setup, h.OperationName);
                    NXOpen.CAM.Operation op;
                    try
                    {
                        op = setup.CAMOperationCollection.CreateWithUserName(
                            programGroup, method, tool, workpiece,
                            tpl.Item1, tpl.Item2,
                            OperationCollection.UseDefaultName.False, opName, opName);
                    }
                    catch (NXException ex)
                    {
                        result.Puladas.Add(h.OperationName + " (" + tpl.Item1 + "/" + tpl.Item2 + ": " + ex.Message + ")");
                        continue;
                    }

                    AplicarParametros(uf, op, h, result);
                    result.OperacoesCriadas.Add(opName + "  [" + tool.Name + "]");
                }
            }
            catch
            {
                session.UndoToMark(mark, null);
                throw;
            }

            return result;
        }

        // ------------------------------------------------------------
        private static void AplicarParametros(UFSession uf, NXOpen.CAM.Operation op, ShopDocOperationModel h, ApplyResult r)
        {
            if (h.Rpm.HasValue && h.Rpm.Value > 0)
            {
                try { uf.Param.SetDoubleValue(op.Tag, UFConstants.UF_PARAM_SPINDLE_RPM, h.Rpm.Value); }
                catch (NXException ex) { r.Avisos.Add(op.Name + ": RPM not set (" + ex.Message + ")"); }
            }
            if (h.FeedCut.HasValue && h.FeedCut.Value > 0)
            {
                try { uf.Param.SetDoubleValue(op.Tag, UFConstants.UF_PARAM_FEED_CUT, h.FeedCut.Value); }
                catch (NXException ex) { r.Avisos.Add(op.Name + ": feed not set (" + ex.Message + ")"); }
            }
        }

        // Reaproveita a ferramenta pelo nome; se nao existir, cria uma fresa
        // generica (mill_planar/MILL) com o mesmo nome e diametro.
        private static NXOpen.CAM.Tool GarantirFerramenta(CAMSetup setup, UFSession uf, NCGroup toolRoot,
                                                          ShopDocOperationModel h, ApplyResult r)
        {
            if (string.IsNullOrEmpty(h.ToolName)) return null;

            NXOpen.CAM.Tool existente = setup.CAMGroupCollection.ToArray()
                .OfType<NXOpen.CAM.Tool>()
                .FirstOrDefault(t => string.Equals(t.Name, h.ToolName, StringComparison.OrdinalIgnoreCase));
            if (existente != null) return existente;

            try
            {
                NXOpen.CAM.Tool nova = (NXOpen.CAM.Tool)setup.CAMGroupCollection.CreateTool(
                    toolRoot, "mill_planar", "MILL",
                    NCGroupCollection.UseDefaultName.False, h.ToolName);

                if (h.ToolDiameter.HasValue && h.ToolDiameter.Value > 0)
                    uf.Param.SetDoubleValue(nova.Tag, UFConstants.UF_PARAM_TL_DIAMETER, h.ToolDiameter.Value);
                if (h.ToolNumber.HasValue)
                    uf.Param.SetIntValue(nova.Tag, UFConstants.UF_PARAM_TL_NUMBER, h.ToolNumber.Value);

                r.FerramentasCriadas.Add(h.ToolName +
                    (h.ToolDiameter.HasValue ? "  Ø" + h.ToolDiameter.Value.ToString("F1") : ""));
                return nova;
            }
            catch (NXException ex)
            {
                r.Avisos.Add("Tool '" + h.ToolName + "' could not be created: " + ex.Message);
                return null;
            }
        }

        private static T FindGroup<T>(CAMSetup setup, string name) where T : class
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (CAMObject obj in setup.CAMGroupCollection.ToArray())
            {
                NCGroup g = obj as NCGroup;
                if (g != null && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    T t = obj as T;
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static string NomeUnicoOperacao(CAMSetup setup, string baseName)
        {
            HashSet<string> nomes = new HashSet<string>(
                setup.CAMOperationCollection.ToArray().Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            return NomeUnico(nomes, Sanitize(baseName));
        }

        private static string NomeUnicoGrupo(CAMSetup setup, string baseName)
        {
            HashSet<string> nomes = new HashSet<string>(
                setup.CAMGroupCollection.ToArray().Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
            return NomeUnico(nomes, baseName);
        }

        private static string NomeUnico(HashSet<string> existentes, string baseName)
        {
            if (!existentes.Contains(baseName)) return baseName;
            for (int i = 1; i < 1000; i++)
            {
                string cand = baseName + "_" + i;
                if (!existentes.Contains(cand)) return cand;
            }
            return baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        // Nomes de objetos CAM: maiusculas, sem espacos/caracteres especiais
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "OP";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            string r = sb.ToString().Trim('_');
            return r.Length > 0 ? r : "OP";
        }
    }
}