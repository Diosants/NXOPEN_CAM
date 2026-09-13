using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;
using NXOpen.CAM;
using Operation = NXOpen.CAM.Operation;

public class DRILL_AND_TAP_COMBINED
{
    // Controla o quanto o log mostra: false = só avisos/resumo; true = mostra
    // tudo (diâmetro/profundidade de cada furo, confirmação de operação
    // criada). Deixe true se precisar depurar de novo no futuro.
    private const bool VERBOSE = false;

    private const string DRILL_PREFIX = "HSS_DRILL_D";

    // ── IDs de cor reais confirmados pelo usuário ──
    private const int COLOR_YELLOW = 6;    // rosca M10
    private const int COLOR_BROWN = 125;   // counterbore
    private const int COLOR_RED = 186;     // rosca M8
    private const int COLOR_EMERALD = 108; // rosca M12
    private const int COLOR_PURPLE = 164;  // superfície (não usinar)
    private const int COLOR_BLUE = 211;    // furo H7
    private const int COLOR_ORANGE = 78;   // rosca M16
    private const int COLOR_CYAN = 31;     // counterbore maior

    // ── Tabela de roscas reconhecidas: cor -> dados da rosca. Adicione uma
    // linha nova aqui pra reconhecer mais roscas, sem precisar duplicar
    // classe/lógica nenhuma. ──
    private class ThreadInfo
    {
        public string Name;        // "M10"
        public double NominalDiam; // 10.0
        public double Pitch;       // 1.5 (passo, mm)
        public string TapToolName; // "TAP_M10X1.5" - nome exato na biblioteca
    }

    private static readonly Dictionary<int, ThreadInfo> ThreadByColorId = new Dictionary<int, ThreadInfo>
    {
        { COLOR_YELLOW,  new ThreadInfo { Name = "M10", NominalDiam = 10.0, Pitch = 1.5,  TapToolName = "TAP_M10X1.5" } },
        { COLOR_RED,     new ThreadInfo { Name = "M8",  NominalDiam = 8.0,  Pitch = 1.25, TapToolName = "TAP_M8X1.25" } },
        { COLOR_EMERALD, new ThreadInfo { Name = "M12", NominalDiam = 12.0, Pitch = 1.75, TapToolName = "TAP_M12X1.75" } },
        { COLOR_ORANGE,  new ThreadInfo { Name = "M16", NominalDiam = 16.0, Pitch = 2.0,  TapToolName = "TAP_M16X2.0" } },
    };

    // Ferramenta de escareador - genérica, a mesma pra qualquer rosca.
    private const string COUNTERSINK_TOOL_NAME = "COUNTER_SINK";

    // Profundidade mínima de engajamento de rosca: 1,5x o diâmetro nominal.
    private const double MIN_ENGAGEMENT_FACTOR = 1.5;

    private class HoleStep
    {
        public double Diameter;
        public double Depth;
        public bool IsThrough;
    }

    private class RawFaceData
    {
        public double Diameter;
        public double MinProjection;
        public double MaxProjection;
    }

    // Folga extra além da face de saída, aplicada só em furos PASSANTES (pra
    // broca quebrar limpo do outro lado). Ajuste esse valor conforme a
    // prática da sua oficina.
    private const double THROUGH_HOLE_BREAKOUT_ALLOWANCE = 5.0;

    // Tolerância pra considerar que o fundo do furo "encosta" na superfície
    // oposta do corpo sólido (ou seja, é passante).
    private const double THROUGH_HOLE_TOLERANCE = 0.5;

    // ── Parâmetros de corte pra furação (usado na broca e no escareador):
    // RPM = (Vc x 318) / diâmetro. Feed = RPM x avanço-por-rotação. ──
    private const double DRILL_CUTTING_SPEED_VC = 25.0;
    private const double DRILL_FEED_PER_REV = 0.1;

    // ── Parâmetros de corte pra ROSQUEAMENTO (macho): Feed = passo x RPM,
    // já que o macho avança exatamente um passo por rotação. ──
    private const double TAP_CUTTING_SPEED_VC = 8.0; // m/min - típico pra aço, ajuste conforme material

    private static void CalculateDrillCuttingParameters(double diameter, out double rpm, out double feed)
    {
        rpm = (DRILL_CUTTING_SPEED_VC * 318.0) / diameter;
        feed = rpm * DRILL_FEED_PER_REV;
    }

    private static void CalculateTapCuttingParameters(double nominalDiameter, double pitch, out double rpm, out double feed)
    {
        rpm = (TAP_CUTTING_SPEED_VC * 318.0) / nominalDiameter;
        feed = rpm * pitch;
    }

    // Desabilita checagem de colisão/gouge - necessário pro macho, senão a
    // operação não gera trajetória (acusa colisão por causa da geometria da
    // rosca já usinada por outras operações anteriores).
    private static void DisableGougeCheck(NXOpen.CAM.HoleDrillingBuilder builder)
    {
        builder.CollisionCheck = false;
        builder.GougeChecking = false;
        builder.NonCuttingBuilder.CollisionCheck = false;
    }

    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;

        if (workPart == null)
            return;

        NXOpen.ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        NXOpen.UF.UFSession ufs = NXOpen.UF.UFSession.GetUFSession();

        try
        {
            NCGroup ncProgramRoot = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");
            NCGroup nCGroup2 = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE");
            NXOpen.CAM.Method method1 = (NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD");

            // ── Pastas de furação (Program Order), por estratégia ──
            NCGroup thruFolder = FindOrCreateProgramFolder(workPart, ncProgramRoot, "THRU_DRILLS");
            NCGroup blindFolder = FindOrCreateProgramFolder(workPart, ncProgramRoot, "BLIND_DRILLS");
            NCGroup cboreFolder = FindOrCreateProgramFolder(workPart, ncProgramRoot, "CBORE_DRILLS");

            List<FeatureGeometryGroup> featureGroups = GetFeatureGeometryGroups(workPart);
            lw.WriteLine("Quantidade de feature groups encontrados: " + featureGroups.Count);

            // ── Deduplicação: quando o mesmo furo é reconhecido duas vezes,
            // mantém só o TAPERED (se houver) e pula o resto, comparando a
            // posição (X,Y) de cada grupo. Usado nas duas etapas abaixo. ──
            List<FeatureGeometryGroup> gruposParaPular = FindDuplicateGroups(featureGroups, lw);

            // ══════════════════════════════════════════════════════════════
            // ETAPA 1 — FURAÇÃO: fura TODOS os furos válidos, independente
            // da cor ser rosca reconhecida ou não.
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== ETAPA 1: FURAÇÃO ===");

            foreach (FeatureGeometryGroup fg in featureGroups)
            {
                if (gruposParaPular.Contains(fg))
                {
                    lw.WriteLine("Grupo " + fg.Name + " -> duplicado (mesma posição de outro grupo já processado). Ignorado.");
                    continue;
                }

                int colorId;
                string erroCor;
                bool hasColor = TryGetGroupColorId(fg, out colorId, out erroCor);

                if (!hasColor)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> " + erroCor + ". Grupo ignorado.");
                    continue;
                }

                if (colorId == COLOR_PURPLE)
                {
                    lw.WriteLine("Grupo " + fg.Name + " -> cor PURPLE (superfície). Não é furo. Ignorado.");
                    continue;
                }

                ProcessGeometryHole(workPart, thruFolder, blindFolder, cboreFolder, nCGroup2, fg, colorId, ufs, lw);
            }

            // ══════════════════════════════════════════════════════════════
            // ETAPA 2 — ESCAREADOR + MACHO: só roda se a cor do grupo bater
            // com uma rosca reconhecida na tabela. Se não bater, não cria
            // nem escareador nem macho pra esse furo.
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== ETAPA 2: ESCAREADOR + MACHO (roscas reconhecidas) ===");

            NXOpen.CAM.Tool counterSinkTool = FindToolByName(workPart, COUNTERSINK_TOOL_NAME);

            if (counterSinkTool == null)
            {
                lw.WriteLine("AVISO: ferramenta '" + COUNTERSINK_TOOL_NAME + "' não encontrada na biblioteca. Etapa 2 inteira pulada.");
            }
            else
            {
                Dictionary<string, NXOpen.CAM.Tool> tapToolCache = new Dictionary<string, NXOpen.CAM.Tool>();
                Dictionary<string, NXOpen.CAM.NCGroup> folderCache = new Dictionary<string, NXOpen.CAM.NCGroup>();

                int processados = 0;

                foreach (FeatureGeometryGroup fg in featureGroups)
                {
                    if (gruposParaPular.Contains(fg))
                        continue; // já avisado na etapa 1, não repete o log aqui

                    int colorId;
                    string erroCor;
                    bool hasColor = TryGetGroupColorId(fg, out colorId, out erroCor);

                    if (!hasColor)
                        continue; // já avisado na etapa 1

                    ThreadInfo threadInfo;
                    bool isRosca = ThreadByColorId.TryGetValue(colorId, out threadInfo);

                    if (!isRosca)
                        continue; // cor não é rosca reconhecida - não cria escareador nem macho, de propósito

                    NXOpen.CAM.Tool tapTool;
                    if (!tapToolCache.TryGetValue(threadInfo.Name, out tapTool))
                    {
                        tapTool = FindToolByName(workPart, threadInfo.TapToolName);
                        tapToolCache[threadInfo.Name] = tapTool;
                    }

                    if (tapTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta '" + threadInfo.TapToolName + "' (rosca " + threadInfo.Name + ") não encontrada na biblioteca. Grupo ignorado (nem escareador, nem macho).");
                        continue;
                    }

                    NXOpen.CAM.NCGroup folder;
                    if (!folderCache.TryGetValue(threadInfo.Name, out folder))
                    {
                        string folderName = "TAP" + threadInfo.Name + "X" + threadInfo.Pitch.ToString("0.###", CultureInfo.InvariantCulture);
                        folder = FindOrCreateProgramFolder(workPart, ncProgramRoot, folderName);
                        folderCache[threadInfo.Name] = folder;
                    }

                    double depth = threadInfo.NominalDiam * MIN_ENGAGEMENT_FACTOR;

                    CreateCountersinkOperation(workPart, folder, method1, counterSinkTool, fg, threadInfo, lw);
                    CreateTappingOperation(workPart, folder, method1, tapTool, fg, threadInfo, depth, lw);

                    processados++;
                }

                lw.WriteLine("Total de furos com rosca processados (escareador + macho): " + processados);
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ETAPA 1 — FURAÇÃO
    // ══════════════════════════════════════════════════════════════════════

    private static void ProcessGeometryHole(
        Part workPart, NCGroup thruFolder, NCGroup blindFolder, NCGroup cboreFolder, NCGroup nCGroup2,
        FeatureGeometryGroup fg, int colorId, NXOpen.UF.UFSession ufs, ListingWindow lw)
    {
        List<HoleStep> steps;
        string erroDetalhe;
        bool hasSteps = TryMeasureHoleSteps(fg, ufs, out steps, out erroDetalhe);

        if (!hasSteps)
        {
            // Sem face cilíndrica: se o nome tiver "TAPERED", tenta o fallback
            // via atributo DIAMETER_2. Senão, é mesmo um chanfro sem furo
            // associado - ignora.
            if (fg.Name.IndexOf("TAPERED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ProcessTaperedHole(workPart, blindFolder, nCGroup2, fg, ufs, lw);
                return;
            }

            lw.WriteLine("AVISO: " + fg.Name + " (cor id " + colorId + ") -> " + erroDetalhe + " -> provável chanfro, não furo. Ignorado.");
            return;
        }

        if (VERBOSE)
            lw.WriteLine("Grupo " + fg.Name + " -> cor id " + colorId + " (medição por geometria) -> " + steps.Count + " degrau(s).");

        foreach (HoleStep step in steps)
        {
            string toolName = DRILL_PREFIX + step.Diameter.ToString("0.###", CultureInfo.InvariantCulture);
            NXOpen.CAM.Tool tool = FindToolByName(workPart, toolName);

            if (tool == null)
            {
                lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta '" + toolName + "' não encontrada (diâmetro " + step.Diameter.ToString(CultureInfo.InvariantCulture) + "). Degrau ignorado.");
                continue;
            }

            if (VERBOSE)
                lw.WriteLine("  Diâmetro " + step.Diameter.ToString(CultureInfo.InvariantCulture)
                    + "  Profundidade " + step.Depth.ToString(CultureInfo.InvariantCulture)
                    + (step.IsThrough ? "  [PASSANTE, +" + THROUGH_HOLE_BREAKOUT_ALLOWANCE.ToString(CultureInfo.InvariantCulture) + "mm de folga já incluída]" : "  [CEGO]")
                    + " -> broca " + toolName);

            // Estratégia no nome, determinada pelo prefixo do grupo:
            // FG_STEP1HOLE* -> THROUGH, FG_STEP1POCKET* (inclui _THREAD) -> BLIND,
            // FG_STEP2HOLE* -> COUNTERBORE.
            string estrategia;
            NCGroup pastaEscolhida;
            if (fg.Name.StartsWith("FG_STEP2HOLE", StringComparison.OrdinalIgnoreCase))
            {
                estrategia = "CBORE";
                pastaEscolhida = cboreFolder;
            }
            else if (fg.Name.StartsWith("FG_STEP1POCKET", StringComparison.OrdinalIgnoreCase))
            {
                estrategia = "BLIND";
                pastaEscolhida = blindFolder;
            }
            else if (fg.Name.StartsWith("FG_STEP1HOLE", StringComparison.OrdinalIgnoreCase))
            {
                estrategia = "THRU";
                pastaEscolhida = thruFolder;
            }
            else
            {
                estrategia = step.IsThrough ? "THRU" : "BLIND";
                pastaEscolhida = step.IsThrough ? thruFolder : blindFolder;
            }

            string label = estrategia
                + "_D" + step.Diameter.ToString("0.###", CultureInfo.InvariantCulture)
                + "_" + step.Depth.ToString("0.###", CultureInfo.InvariantCulture) + "MM";
            CreateDrillingOperation(workPart, pastaEscolhida, nCGroup2, tool, fg, label, step.Depth, step.Diameter, lw);
        }
    }

    private static void CreateDrillingOperation(
        Part workPart, NCGroup nCGroup1, NCGroup nCGroup2,
        NXOpen.CAM.Tool tool1, FeatureGeometryGroup featureGeometryGroup1,
        string label, double? depth, double diameter, ListingWindow lw)
    {
        if (VERBOSE)
            lw.WriteLine("Criando operação para o grupo: " + featureGeometryGroup1.Name);

        string baseOperationName = "DRILL_" + label;
        string operationName = MakeUniqueOperationName(workPart, baseOperationName);

        Operation operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, nCGroup2, tool1, featureGeometryGroup1,
            "hole_making", "DRILLING",
            NXOpen.CAM.OperationCollection.UseDefaultName.False,
            operationName, operationName);

        NXOpen.CAM.HoleDrilling holeDrilling1 = (NXOpen.CAM.HoleDrilling)operation1;
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1 =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);

        NXOpen.CAM.FBM.FeatureGeometry featureGeometry1 = holeDrillingBuilder1.GetFeatureGeometry();
        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningFeatureGeometry1 =
            (NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry1;
        NXOpen.CAM.GeometrySetList geometrySetList1 = machiningFeatureGeometry1.GeometryList;

        holeDrillingBuilder1.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder1.CycleTable.CycleType = "Drill,Deep";
        holeDrillingBuilder1.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;
        holeDrillingBuilder1.CycleTable.AxialStepover.DistanceBuilder.Value = 1.0;

        if (depth.HasValue)
        {
            holeDrillingBuilder1.PredefinedDepth.Status = true;
            holeDrillingBuilder1.PredefinedDepth.Value = depth.Value;
        }

        double rpm, feed;
        CalculateDrillCuttingParameters(diameter, out rpm, out feed);

        if (VERBOSE)
            lw.WriteLine("  RPM calculado: " + rpm.ToString("0", CultureInfo.InvariantCulture)
                + "  Feed calculado: " + feed.ToString("0", CultureInfo.InvariantCulture));

        holeDrillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        holeDrillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = feed;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 10.0;

        NXObject committed = holeDrillingBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        objects1[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects1);

        if (VERBOSE)
            lw.WriteLine("Trajetória gerada para: " + featureGeometryGroup1.Name);

        holeDrillingBuilder1.Destroy();
    }

    // ── Medição por geometria (diâmetro + profundidade acumulada, com
    // auto-correção de eixo). ──
    private static bool TryMeasureHoleSteps(
        FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs,
        out List<HoleStep> steps, out string erroDetalhe)
    {
        steps = new List<HoleStep>();
        erroDetalhe = "";

        const double TOLERANCE = 0.01;

        NXOpen.CAM.CAMFeature[] camFeatures;
        try { camFeatures = fg.GetFeatures(); }
        catch (Exception ex) { erroDetalhe = "GetFeatures() falhou: " + ex.Message; return false; }

        if (camFeatures == null || camFeatures.Length == 0)
        {
            erroDetalhe = "sem CAMFeature";
            return false;
        }

        NXOpen.Face[] faces;
        try { faces = camFeatures[0].GetFaces(); }
        catch (Exception ex) { erroDetalhe = "GetFaces() falhou: " + ex.Message; return false; }

        if (faces == null || faces.Length == 0)
        {
            erroDetalhe = "GetFaces() retornou vazio";
            return false;
        }

        double[] axisDir = null;
        List<RawFaceData> rawFaces = new List<RawFaceData>();

        foreach (NXOpen.Face face in faces)
        {
            if (face.SolidFaceType != NXOpen.Face.FaceType.Cylindrical)
                continue;

            try
            {
                int faceType;
                double[] facePt = new double[3];
                double[] faceDir = new double[3];
                double[] bbox = new double[6];
                double faceRadius;
                double faceRadData;
                int normDirection;

                ufs.Modl.AskFaceData(face.Tag, out faceType, facePt, faceDir, bbox, out faceRadius, out faceRadData, out normDirection);

                if (axisDir == null)
                    axisDir = faceDir;

                double diameter = faceRadius * 2.0;

                double minProj, maxProj;
                ProjectBoxOntoAxis(bbox, axisDir, out minProj, out maxProj);

                bool jaExiste = false;
                foreach (RawFaceData rf in rawFaces)
                {
                    if (Math.Abs(rf.Diameter - diameter) < TOLERANCE)
                    {
                        jaExiste = true;
                        break;
                    }
                }

                if (!jaExiste)
                {
                    rawFaces.Add(new RawFaceData { Diameter = diameter, MinProjection = minProj, MaxProjection = maxProj });
                }
            }
            catch (Exception ex)
            {
                erroDetalhe = "AskFaceData falhou: " + ex.Message;
                return false;
            }
        }

        if (rawFaces.Count == 0)
        {
            erroDetalhe = "nenhuma face cilíndrica encontrada (provável furo cônico - precisa de outra estratégia)";
            return false;
        }

        double bodyMin = double.MaxValue;
        double bodyMax = double.MinValue;
        try
        {
            NXOpen.Body body = faces[0].GetBody();
            double[] bodyBbox = new double[6];
            ufs.Modl.AskBoundingBox(body.Tag, bodyBbox);
            ProjectBoxOntoAxis(bodyBbox, axisDir, out bodyMin, out bodyMax);
        }
        catch
        {
            // segue sem diagnosticar passante/cego (assume cego, mais seguro)
        }

        double topoMax = double.MinValue;
        double topoMin = double.MaxValue;
        foreach (RawFaceData rf in rawFaces)
        {
            if (rf.MaxProjection > topoMax) topoMax = rf.MaxProjection;
            if (rf.MinProjection < topoMin) topoMin = rf.MinProjection;
        }

        List<HoleStep> stepsOpcaoA = BuildSteps(rawFaces, topoMax, true, bodyMin, bodyMax);
        List<HoleStep> stepsOpcaoB = BuildSteps(rawFaces, topoMin, false, bodyMin, bodyMax);

        steps = RespeitaRegraDiametroMaiorMaisRaso(stepsOpcaoA) ? stepsOpcaoA : stepsOpcaoB;
        steps.Sort((a, b) => b.Diameter.CompareTo(a.Diameter));

        if (steps.Count > 0)
        {
            HoleStep deepest = steps[steps.Count - 1];
            if (deepest.IsThrough)
                deepest.Depth += THROUGH_HOLE_BREAKOUT_ALLOWANCE;
        }

        return true;
    }

    private static List<HoleStep> BuildSteps(List<RawFaceData> rawFaces, double topoComum, bool usarMaxComoTopo, double bodyMin, double bodyMax)
    {
        List<HoleStep> result = new List<HoleStep>();
        foreach (RawFaceData rf in rawFaces)
        {
            double bottomProjection = usarMaxComoTopo ? rf.MinProjection : rf.MaxProjection;
            double depth = usarMaxComoTopo ? (topoComum - rf.MinProjection) : (rf.MaxProjection - topoComum);

            double oppositeBodyBoundary = usarMaxComoTopo ? bodyMin : bodyMax;
            bool isThrough = Math.Abs(bottomProjection - oppositeBodyBoundary) < THROUGH_HOLE_TOLERANCE;

            result.Add(new HoleStep
            {
                Diameter = rf.Diameter,
                Depth = depth,
                IsThrough = isThrough
            });
        }
        return result;
    }

    private static bool RespeitaRegraDiametroMaiorMaisRaso(List<HoleStep> candidatos)
    {
        if (candidatos.Count < 2)
            return true;

        List<HoleStep> ordenado = new List<HoleStep>(candidatos);
        ordenado.Sort((a, b) => b.Diameter.CompareTo(a.Diameter));

        for (int i = 0; i < ordenado.Count - 1; i++)
        {
            if (ordenado[i].Depth > ordenado[i + 1].Depth + 0.001)
                return false;
        }

        return true;
    }

    private static void ProjectBoxOntoAxis(double[] bbox, double[] axisDir, out double min, out double max)
    {
        double[] xs = { bbox[0], bbox[3] };
        double[] ys = { bbox[1], bbox[4] };
        double[] zs = { bbox[2], bbox[5] };

        min = double.MaxValue;
        max = double.MinValue;

        foreach (double x in xs)
            foreach (double y in ys)
                foreach (double z in zs)
                {
                    double projection = (x * axisDir[0]) + (y * axisDir[1]) + (z * axisDir[2]);
                    if (projection < min) min = projection;
                    if (projection > max) max = projection;
                }
    }

    // ── Fallback pra features TAPERED (só chanfro, sem furo cilíndrico real):
    // lê DIAMETER_2 via atributo + tenta ray-trace local, com fallback pra
    // fórmula 1,5x nominal. ──
    private static readonly Dictionary<double, double> IsoTapDrillToNominal = new Dictionary<double, double>
    {
        { 2.5, 3 }, { 3.3, 4 }, { 4.2, 5 }, { 5.0, 6 },
        { 6.8, 8 }, { 8.5, 10 }, { 10.5, 12 }, { 12.0, 14 },
        { 14.0, 16 }, { 15.5, 18 }, { 17.5, 20 }, { 19.5, 22 },
        { 21.0, 24 }, { 24.0, 27 }, { 26.5, 30 }
    };

    private static double RecognizeNominalDiameter(double drillDiameter, out bool isApproximate)
    {
        double bestNominal = drillDiameter;
        double bestDist = double.MaxValue;

        foreach (KeyValuePair<double, double> kv in IsoTapDrillToNominal)
        {
            double dist = Math.Abs(kv.Key - drillDiameter);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestNominal = kv.Value;
            }
        }

        if (bestDist <= 1.0)
        {
            isApproximate = false;
            return bestNominal;
        }

        isApproximate = true;
        return drillDiameter;
    }

    private static void ProcessTaperedHole(
        Part workPart, NCGroup nCGroup1, NCGroup nCGroup2,
        FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs, ListingWindow lw)
    {
        double diameter2;
        string erro;
        bool ok = TryGetAttributeDouble(fg, "DIAMETER_2", out diameter2, out erro);

        if (!ok)
        {
            lw.WriteLine("AVISO: " + fg.Name + " -> não conseguiu ler DIAMETER_2: " + erro + ". Grupo ignorado.");
            return;
        }

        string toolName = DRILL_PREFIX + diameter2.ToString("0.###", CultureInfo.InvariantCulture);
        NXOpen.CAM.Tool tool = FindToolByName(workPart, toolName);

        if (tool == null)
        {
            lw.WriteLine("AVISO: ferramenta '" + toolName + "' (DIAMETER_2 = " + diameter2.ToString(CultureInfo.InvariantCulture) + ") não encontrada. Grupo " + fg.Name + " ignorado.");
            return;
        }

        double depth;
        bool isThrough;
        bool medidoPorRaio = TryMeasureLocalDepthByRayTrace(fg, ufs, out depth, out isThrough, lw);

        string origemProfundidade;
        if (medidoPorRaio)
        {
            if (isThrough)
                depth += THROUGH_HOLE_BREAKOUT_ALLOWANCE;
            origemProfundidade = "medida local (ray-trace)" + (isThrough ? " [PASSANTE, +" + THROUGH_HOLE_BREAKOUT_ALLOWANCE.ToString(CultureInfo.InvariantCulture) + "mm]" : " [CEGO]")
                + " *** VERIFIQUE MANUALMENTE ANTES DE USAR EM PRODUÇÃO ***";
        }
        else
        {
            bool isApproximate;
            double nominal = RecognizeNominalDiameter(diameter2, out isApproximate);
            depth = nominal * MIN_ENGAGEMENT_FACTOR;
            origemProfundidade = "fórmula " + MIN_ENGAGEMENT_FACTOR.ToString(CultureInfo.InvariantCulture) + "x nominal (fallback - ray-trace não deu certo)"
                + (isApproximate ? " [nominal aproximado]" : "");
        }

        if (VERBOSE)
            lw.WriteLine("Grupo " + fg.Name + " -> DIAMETER_2 = " + diameter2.ToString(CultureInfo.InvariantCulture)
                + " -> broca " + toolName
                + " -> profundidade: " + depth.ToString(CultureInfo.InvariantCulture) + " (" + origemProfundidade + ")");

        string label = "D" + diameter2.ToString("0.###", CultureInfo.InvariantCulture);
        CreateDrillingOperation(workPart, nCGroup1, nCGroup2, tool, fg, label, depth, diameter2, lw);
    }

    private static bool TryMeasureLocalDepthByRayTrace(FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs, out double depth, out bool isThrough, ListingWindow lw)
    {
        depth = 0.0;
        isThrough = false;

        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();
            if (camFeatures == null || camFeatures.Length == 0)
                return false;

            NXOpen.Face[] faces = camFeatures[0].GetFaces();
            if (faces == null || faces.Length == 0)
                return false;

            NXOpen.Face refFace = faces[0];

            int faceType;
            double[] facePt = new double[3];
            double[] faceDir = new double[3];
            double[] bbox = new double[6];
            double faceRadius, faceRadData;
            int normDirection;

            ufs.Modl.AskFaceData(refFace.Tag, out faceType, facePt, faceDir, bbox, out faceRadius, out faceRadData, out normDirection);

            NXOpen.Body body = refFace.GetBody();

            double[] identity = new double[16];
            ufs.Mtx4.Identity(identity);

            const double OFFSET = 5.0;

            double[] hitFirst, hitLast;
            bool ok = TryTraceRayBothDirections(ufs, body, facePt, faceDir, identity, OFFSET, out hitFirst, out hitLast, lw);

            if (!ok)
                return false;

            double dx = hitLast[0] - hitFirst[0];
            double dy = hitLast[1] - hitFirst[1];
            double dz = hitLast[2] - hitFirst[2];
            depth = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

            if (depth < faceRadius || depth > 500.0)
            {
                lw.WriteLine("    [ray-trace] profundidade " + depth.ToString(CultureInfo.InvariantCulture) + " reprovada na checagem de sanidade (raio da face: " + faceRadius.ToString(CultureInfo.InvariantCulture) + ")");
                return false;
            }

            isThrough = true;
            return true;
        }
        catch (Exception ex)
        {
            lw.WriteLine("    [ray-trace] exceção: " + ex.Message);
            return false;
        }
    }

    private static bool TryTraceRayBothDirections(
        NXOpen.UF.UFSession ufs, NXOpen.Body body,
        double[] facePt, double[] faceDir, double[] identity, double offset,
        out double[] hitFirst, out double[] hitLast, ListingWindow lw)
    {
        hitFirst = null;
        hitLast = null;

        double[][] direcoesTentativas = new double[][]
        {
            new double[] { faceDir[0], faceDir[1], faceDir[2] },
            new double[] { -faceDir[0], -faceDir[1], -faceDir[2] }
        };

        int tentativaNum = 0;
        foreach (double[] rayDir in direcoesTentativas)
        {
            tentativaNum++;
            try
            {
                double[] startPt = new double[3];
                startPt[0] = facePt[0] - (rayDir[0] * offset);
                startPt[1] = facePt[1] - (rayDir[1] * offset);
                startPt[2] = facePt[2] - (rayDir[2] * offset);

                int numResults;
                NXOpen.UF.UFModl.RayHitPointInfo[] hits;

                ufs.Modl.TraceARay(1, new NXOpen.Tag[] { body.Tag }, startPt, rayDir, identity, 1, out numResults, out hits);

                if (VERBOSE)
                    lw.WriteLine("    [ray-trace] tentativa " + tentativaNum + ": numResults = " + numResults);

                if (numResults >= 2)
                {
                    hitFirst = hits[0].hit_point;
                    hitLast = hits[numResults - 1].hit_point;
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (VERBOSE)
                    lw.WriteLine("    [ray-trace] tentativa " + tentativaNum + " lançou exceção: " + ex.Message);
            }
        }

        return false;
    }

    private static bool TryGetAttributeDouble(FeatureGeometryGroup fg, string attrName, out double value, out string erro)
    {
        value = 0.0;
        erro = "";

        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();

            if (camFeatures == null || camFeatures.Length == 0)
            {
                erro = "sem CAMFeature";
                return false;
            }

            NXOpen.CAM.CAMAttributeCollection attrs = camFeatures[0].Attributes;
            NXOpen.CAM.CAMAttribute attr;

            try { attr = attrs.FindObject(attrName); }
            catch (Exception ex) { erro = attrName + " não existe: " + ex.Message; return false; }

            if (attr == null)
            {
                erro = attrName + " retornou null";
                return false;
            }

            System.Reflection.MethodInfo getInt = attr.GetType().GetMethod("GetIntegerValue", Type.EmptyTypes);
            if (getInt != null)
            {
                try { getInt.Invoke(attr, null); }
                catch { /* esperado falhar - aquece o objeto antes da leitura real */ }
            }

            System.Reflection.MethodInfo getDouble = attr.GetType().GetMethod("GetDoubleValue", Type.EmptyTypes);
            if (getDouble == null)
            {
                erro = "GetDoubleValue não encontrado via reflection";
                return false;
            }

            object result = getDouble.Invoke(attr, null);
            value = (double)result;
            return true;
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            erro = (tie.InnerException != null) ? tie.InnerException.Message : tie.Message;
            return false;
        }
        catch (Exception ex)
        {
            erro = ex.Message;
            return false;
        }
    }

    // ── Deduplicação por posição ──
    private const double DUPLICATE_POSITION_TOLERANCE = 0.5;

    private static List<FeatureGeometryGroup> FindDuplicateGroups(List<FeatureGeometryGroup> featureGroups, ListingWindow lw)
    {
        List<FeatureGeometryGroup> paraPular = new List<FeatureGeometryGroup>();

        List<Tuple<FeatureGeometryGroup, double, double>> comPosicao = new List<Tuple<FeatureGeometryGroup, double, double>>();

        foreach (FeatureGeometryGroup fg in featureGroups)
        {
            double x, y;
            if (TryGetGroupPosition(fg, out x, out y))
                comPosicao.Add(new Tuple<FeatureGeometryGroup, double, double>(fg, x, y));
        }

        bool[] jaAgrupado = new bool[comPosicao.Count];

        for (int i = 0; i < comPosicao.Count; i++)
        {
            if (jaAgrupado[i])
                continue;

            List<int> cluster = new List<int> { i };

            for (int j = i + 1; j < comPosicao.Count; j++)
            {
                if (jaAgrupado[j])
                    continue;

                double dx = comPosicao[i].Item2 - comPosicao[j].Item2;
                double dy = comPosicao[i].Item3 - comPosicao[j].Item3;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));

                if (dist < DUPLICATE_POSITION_TOLERANCE)
                    cluster.Add(j);
            }

            if (cluster.Count > 1)
            {
                foreach (int idx in cluster)
                    jaAgrupado[idx] = true;

                int escolhidoIdx = cluster[0];
                foreach (int idx in cluster)
                {
                    if (comPosicao[idx].Item1.Name.IndexOf("TAPERED", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        escolhidoIdx = idx;
                        break;
                    }
                }

                List<string> nomesCluster = new List<string>();
                foreach (int idx in cluster)
                    nomesCluster.Add(comPosicao[idx].Item1.Name);

                lw.WriteLine("Posição duplicada detectada entre: " + string.Join(", ", nomesCluster.ToArray())
                    + " -> mantendo: " + comPosicao[escolhidoIdx].Item1.Name);

                foreach (int idx in cluster)
                {
                    if (idx != escolhidoIdx)
                        paraPular.Add(comPosicao[idx].Item1);
                }
            }
        }

        return paraPular;
    }

    private static bool TryGetGroupPosition(FeatureGeometryGroup fg, out double x, out double y)
    {
        x = 0;
        y = 0;

        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();

            if (camFeatures == null || camFeatures.Length == 0)
                return false;

            NXOpen.CartesianCoordinateSystem csys = camFeatures[0].CoordinateSystem;

            if (csys == null)
                return false;

            x = csys.Origin.X;
            y = csys.Origin.Y;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ETAPA 2 — ESCAREADOR + MACHO
    // ══════════════════════════════════════════════════════════════════════

    private static void CreateCountersinkOperation(
        Part workPart, NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, FeatureGeometryGroup fg, ThreadInfo threadInfo, ListingWindow lw)
    {
        string operationName = MakeUniqueOperationName(workPart, "CSINK_" + threadInfo.Name + "X" + threadInfo.Pitch.ToString("0.###", CultureInfo.InvariantCulture));

        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, method1, tool, fg,
            "hole_making", "COUNTERSINKING",
            NXOpen.CAM.OperationCollection.UseDefaultName.False,
            operationName, operationName);

        NXOpen.CAM.HoleDrilling holeDrilling = (NXOpen.CAM.HoleDrilling)operation;
        NXOpen.CAM.HoleDrillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);

        builder.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegions.Type =
            NXOpen.CAM.NcmTransfer.TransferTypes.LowestSafeZ;

        double rpm, feed;
        CalculateDrillCuttingParameters(threadInfo.NominalDiam, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        NXObject committed = builder.Commit();

        NXOpen.CAM.CAMObject[] objects = new NXOpen.CAM.CAMObject[1];
        objects[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects);

        if (VERBOSE)
            lw.WriteLine("Escareador (" + threadInfo.Name + ") criado para: " + fg.Name);

        builder.Destroy();
    }

    private static void CreateTappingOperation(
        Part workPart, NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, FeatureGeometryGroup fg, ThreadInfo threadInfo, double depth, ListingWindow lw)
    {
        string operationName = MakeUniqueOperationName(
            workPart,
            "TAP_" + threadInfo.Name + "X" + threadInfo.Pitch.ToString("0.###", CultureInfo.InvariantCulture)
            + "_" + depth.ToString("0.###", CultureInfo.InvariantCulture) + "MM");

        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, method1, tool, fg,
            "hole_making", "TAPPING",
            NXOpen.CAM.OperationCollection.UseDefaultName.False,
            operationName, operationName);

        NXOpen.CAM.HoleDrilling holeDrilling = (NXOpen.CAM.HoleDrilling)operation;
        NXOpen.CAM.HoleDrillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);

        builder.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        builder.PredefinedDepth.Status = true;
        builder.PredefinedDepth.Value = depth;

        double rpm, feed;
        CalculateTapCuttingParameters(threadInfo.NominalDiam, threadInfo.Pitch, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegions.Type =
            NXOpen.CAM.NcmTransfer.TransferTypes.LowestSafeZ;

        DisableGougeCheck(builder);

        NXObject committed = builder.Commit();

        NXOpen.CAM.CAMObject[] objects = new NXOpen.CAM.CAMObject[1];
        objects[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects);

        if (VERBOSE)
            lw.WriteLine("Macho " + threadInfo.Name + " (profundidade " + depth.ToString(CultureInfo.InvariantCulture) + "mm) criado para: " + fg.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS COMPARTILHADOS
    // ══════════════════════════════════════════════════════════════════════

    private static bool TryGetGroupColorId(FeatureGeometryGroup fg, out int colorId, out string erro)
    {
        colorId = 0;
        erro = "";

        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();

            if (camFeatures == null || camFeatures.Length == 0)
            {
                erro = "sem CAMFeature";
                return false;
            }

            NXOpen.Face[] faces = camFeatures[0].GetFaces();

            if (faces == null || faces.Length == 0)
            {
                erro = "sem faces";
                return false;
            }

            colorId = faces[0].Color;
            return true;
        }
        catch (Exception ex)
        {
            erro = ex.Message;
            return false;
        }
    }

    private static List<FeatureGeometryGroup> GetFeatureGeometryGroups(Part workPart)
    {
        List<FeatureGeometryGroup> groups = new List<FeatureGeometryGroup>();
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (CAMObject obj in objects)
        {
            FeatureGeometryGroup fg = obj as FeatureGeometryGroup;
            if (fg == null)
                continue;

            if (fg.Name.StartsWith("FG_STEP"))
                groups.Add(fg);
        }

        return groups;
    }

    private static NCGroup FindOrCreateProgramFolder(Part workPart, NCGroup parentGroup, string folderName)
    {
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (CAMObject obj in objects)
        {
            NCGroup existing = obj as NCGroup;
            if (existing != null && string.Equals(existing.Name, folderName, StringComparison.OrdinalIgnoreCase))
                return existing;
        }

        NCGroup novaPasta = workPart.CAMSetup.CAMGroupCollection.CreateProgramWithUserName(
            parentGroup, "hole_making", "PROGRAM",
            NXOpen.CAM.NCGroupCollection.UseDefaultName.False, folderName, "Program");

        NXOpen.CAM.ProgramOrderGroupBuilder programOrderGroupBuilder1 =
            workPart.CAMSetup.CAMGroupCollection.CreateProgramOrderGroupBuilder(novaPasta);
        programOrderGroupBuilder1.Commit();
        programOrderGroupBuilder1.Destroy();

        return novaPasta;
    }

    private static NXOpen.CAM.Tool FindToolByName(Part workPart, string toolName)
    {
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (CAMObject obj in objects)
        {
            NXOpen.CAM.Tool t = obj as NXOpen.CAM.Tool;
            if (t != null && string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static string MakeUniqueOperationName(Part workPart, string baseName)
    {
        if (!OperationNameExists(workPart, baseName))
            return baseName;

        int counter = 2;
        while (true)
        {
            string candidate = baseName + "_" + counter;
            if (!OperationNameExists(workPart, candidate))
                return candidate;
            counter++;
        }
    }

    private static bool OperationNameExists(Part workPart, string name)
    {
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            if (string.Equals(obj.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        Operation[] operations = workPart.CAMSetup.CAMOperationCollection.ToArray();
        foreach (Operation op in operations)
        {
            if (string.Equals(op.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
