using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;

public class FIND_ALL_HOLES_CENTER_DRILL
{
    // Controla o quanto o log mostra: false = só avisos de furo não
    // realizado; true = mostra tudo (confirmação de cada operação criada).
    private const bool VERBOSE = false;

    // Profundidade fixa de furo de centro (spot drilling) - não usa a
    // profundidade real do furo final, já que centro é sempre raso.
    private const double CENTER_DRILL_DEPTH = 2.0;

    private const double CENTER_DRILL_RPM = 1500.0;

    // Tolerância (mm) pra considerar que dois furos estão na mesma posição.
    private const double DUPLICATE_POSITION_TOLERANCE = 0.5;

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
            NXOpen.CAM.NCGroup ncProgramRoot =
                ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));

            NXOpen.CAM.NCGroup nCGroup2 =
                ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));

            // ── Pasta (Program Order) que vai organizar todas as operações
            // de furo de centro juntas, em vez de espalhadas soltas. ──
            NXOpen.CAM.NCGroup centerDrillsFolder = FindOrCreateProgramFolder(workPart, ncProgramRoot, "CENTER_DRILLS");

            // ── Ferramenta e estratégia fixas: sempre CENTER_DRILL, sempre
            // SPOT_DRILLING - é a mesma ferramenta pra todos os furos de centro. ──
            NXOpen.CAM.Tool centerDrillTool =
                (NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CENTER_DRILL");

            List<NXOpen.CAM.FeatureGeometryGroup> featureGroups = GetFeatureGeometryGroups(workPart);
            lw.WriteLine("Quantidade de feature groups encontrados: " + featureGroups.Count);

            // ── Deduplicação por posição (mesma lógica do script de furação) ──
            List<NXOpen.CAM.FeatureGeometryGroup> gruposParaPular = FindDuplicateGroups(featureGroups, lw);

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in featureGroups)
            {
                if (gruposParaPular.Contains(fg))
                {
                    lw.WriteLine("Grupo " + fg.Name + " -> duplicado (mesma posição de outro grupo já processado). Ignorado.");
                    continue;
                }

                List<HoleStep> steps;
                string erroDetalhe;
                bool hasSteps = TryMeasureHoleSteps(fg, ufs, out steps, out erroDetalhe);

                if (!hasSteps)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> " + erroDetalhe + ". Grupo ignorado.");
                    continue;
                }

                // Furo de centro só precisa ser feito UMA vez por furo, no
                // ponto de entrada - usa só o degrau de maior diâmetro (o
                // primeiro, já que a lista vem ordenada do maior pro menor).
                HoleStep stepEntrada = steps[0];

                string estrategia;
                if (fg.Name.StartsWith("FG_STEP2HOLE", StringComparison.OrdinalIgnoreCase))
                    estrategia = "CBORE";
                else if (fg.Name.StartsWith("FG_STEP1POCKET", StringComparison.OrdinalIgnoreCase))
                    estrategia = "BLIND";
                else if (fg.Name.StartsWith("FG_STEP1HOLE", StringComparison.OrdinalIgnoreCase))
                    estrategia = "THRU";
                else
                    estrategia = stepEntrada.IsThrough ? "THRU" : "BLIND";

                string label = "CENTER_" + estrategia + "_D" + stepEntrada.Diameter.ToString("0.###", CultureInfo.InvariantCulture);

                CreateSpotDrillingOperation(
                    workPart,
                    centerDrillsFolder,
                    nCGroup2,
                    centerDrillTool,
                    fg,
                    label,
                    lw);
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
    }

    private static void CreateSpotDrillingOperation(
        NXOpen.Part workPart,
        NXOpen.CAM.NCGroup nCGroup1,
        NXOpen.CAM.NCGroup nCGroup2,
        NXOpen.CAM.Tool tool1,
        NXOpen.CAM.FeatureGeometryGroup featureGeometryGroup1,
        string label,
        NXOpen.ListingWindow lw)
    {
        if (VERBOSE)
            lw.WriteLine("Criando operação para o grupo: " + featureGeometryGroup1.Name);

        string baseOperationName = "DRILL_" + label;
        string operationName = MakeUniqueOperationName(workPart, baseOperationName);

        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1,
            nCGroup2,
            tool1,
            featureGeometryGroup1,
            "hole_making",
            "SPOT_DRILLING",
            NXOpen.CAM.OperationCollection.UseDefaultName.False,
            operationName,
            operationName);

        NXOpen.CAM.HoleDrilling holeDrilling1 = ((NXOpen.CAM.HoleDrilling)operation1);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1;
        holeDrillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);

        NXOpen.CAM.FBM.FeatureGeometry featureGeometry1;
        featureGeometry1 = holeDrillingBuilder1.GetFeatureGeometry();

        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningFeatureGeometry1 =
            ((NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry1);

        NXOpen.CAM.GeometrySetList geometrySetList1;
        geometrySetList1 = machiningFeatureGeometry1.GeometryList;

        // ----------------------------------------------
        holeDrillingBuilder1.PredefinedDepth.Status = true;
        holeDrillingBuilder1.PredefinedDepth.Value = CENTER_DRILL_DEPTH;

        holeDrillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = CENTER_DRILL_RPM;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 10.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = holeDrillingBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling2 = ((NXOpen.CAM.HoleDrilling)nXObject1);
        objects1[0] = holeDrilling2;
        workPart.CAMSetup.GenerateToolPath(objects1);

        if (VERBOSE)
            lw.WriteLine("Trajetória gerada para: " + featureGeometryGroup1.Name);

        holeDrillingBuilder1.Destroy();
    }

    // ── Medição por geometria (diâmetro + profundidade acumulada, com
    // auto-correção de eixo) - mesma lógica validada no script de furação. ──
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

    private const double THROUGH_HOLE_TOLERANCE = 0.5;

    private static bool TryMeasureHoleSteps(
        NXOpen.CAM.FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs,
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

    // ── Filtro de grupos: só FG_STEP* (mesma lógica do script de furação) ──
    private static List<NXOpen.CAM.FeatureGeometryGroup> GetFeatureGeometryGroups(NXOpen.Part workPart)
    {
        List<NXOpen.CAM.FeatureGeometryGroup> groups =
            new List<NXOpen.CAM.FeatureGeometryGroup>();

        NXOpen.CAM.CAMObject[] objects =
            workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.FeatureGeometryGroup fg =
                obj as NXOpen.CAM.FeatureGeometryGroup;

            if (fg == null)
                continue;

            if (fg.Name.StartsWith("FG_STEP"))
                groups.Add(fg);
        }

        return groups;
    }

    // ── Deduplicação por posição (mesma lógica do script de furação) ──
    private static List<NXOpen.CAM.FeatureGeometryGroup> FindDuplicateGroups(
        List<NXOpen.CAM.FeatureGeometryGroup> featureGroups, NXOpen.ListingWindow lw)
    {
        List<NXOpen.CAM.FeatureGeometryGroup> paraPular = new List<NXOpen.CAM.FeatureGeometryGroup>();

        List<Tuple<NXOpen.CAM.FeatureGeometryGroup, double, double>> comPosicao =
            new List<Tuple<NXOpen.CAM.FeatureGeometryGroup, double, double>>();

        foreach (NXOpen.CAM.FeatureGeometryGroup fg in featureGroups)
        {
            double x, y;
            if (TryGetGroupPosition(fg, out x, out y))
                comPosicao.Add(new Tuple<NXOpen.CAM.FeatureGeometryGroup, double, double>(fg, x, y));
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

    private static bool TryGetGroupPosition(NXOpen.CAM.FeatureGeometryGroup fg, out double x, out double y)
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

    // ── Nome único de operação (mesma lógica do script de furação) ──
    private static string MakeUniqueOperationName(NXOpen.Part workPart, string baseName)
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

    private static bool OperationNameExists(NXOpen.Part workPart, string name)
    {
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            if (string.Equals(obj.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        NXOpen.CAM.Operation[] operations = workPart.CAMSetup.CAMOperationCollection.ToArray();
        foreach (NXOpen.CAM.Operation op in operations)
        {
            if (string.Equals(op.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // Busca uma pasta (Program Order group) pelo nome sem usar FindObject
    // (que lança exceção se não achar). Se não existir, cria uma nova.
    private static NXOpen.CAM.NCGroup FindOrCreateProgramFolder(NXOpen.Part workPart, NXOpen.CAM.NCGroup parentGroup, string folderName)
    {
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.NCGroup existing = obj as NXOpen.CAM.NCGroup;
            if (existing != null && string.Equals(existing.Name, folderName, StringComparison.OrdinalIgnoreCase))
                return existing;
        }

        NXOpen.CAM.NCGroup novaPasta = workPart.CAMSetup.CAMGroupCollection.CreateProgramWithUserName(
            parentGroup, "hole_making", "PROGRAM",
            NXOpen.CAM.NCGroupCollection.UseDefaultName.False, folderName, "Program");

        NXOpen.CAM.ProgramOrderGroupBuilder programOrderGroupBuilder1 =
            workPart.CAMSetup.CAMGroupCollection.CreateProgramOrderGroupBuilder(novaPasta);
        programOrderGroupBuilder1.Commit();
        programOrderGroupBuilder1.Destroy();

        return novaPasta;
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
