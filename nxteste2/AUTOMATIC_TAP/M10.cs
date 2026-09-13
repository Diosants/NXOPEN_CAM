using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;

public class M10
{
    // Controla o quanto o log mostra: false = só avisos de furo não
    // realizado; true = mostra tudo.
    private const bool VERBOSE = false;

    // ── Configuração da rosca M10 ──
    private const int COLOR_M10 = 6;          // cor amarela = M10
    private const double M10_NOMINAL_DIAM = 10.0;
    private const double MIN_ENGAGEMENT_FACTOR = 1.5; // profundidade = 1,5x o nominal

    // ── Parâmetros de corte pra furação (usado no escareador, mesmo cálculo
    // de broca): RPM = (Vc x 318) / diâmetro. Feed = RPM x avanço-por-rotação. ──
    private const double DRILL_CUTTING_SPEED_VC = 25.0;
    private const double DRILL_FEED_PER_REV = 0.1;

    // ── Parâmetros de corte pra ROSQUEAMENTO (macho) - fórmula diferente:
    // RPM = (Vc x 318) / diâmetro nominal (mesma estrutura, Vc mais baixo).
    // Feed = passo da rosca (mm) x RPM - o macho avança exatamente um passo
    // por rotação, sincronizado com o giro (não é avanço arbitrário). ──
    private const double TAP_CUTTING_SPEED_VC = 5; // m/min - típico pra aço, ajuste conforme material
    private const double TAP_PITCH_MM = 1.5;         // passo da rosca M10x1.5

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
    // Nomes das ferramentas já existentes na biblioteca.
    private const string COUNTERSINK_TOOL_NAME = "COUNTER_SINK";
    private const string TAP_TOOL_NAME = "TAP_M10X1.5";

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

        try
        {
            NXOpen.CAM.NCGroup nCGroup1 =
                (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");

            NXOpen.CAM.Method method1 =
                (NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD");

            // ── Pasta que organiza as operações de escareador + macho do M10 ──
            NXOpen.CAM.NCGroup tapM10Folder = FindOrCreateProgramFolder(workPart, nCGroup1, "TAPM10X1.5");

            NXOpen.CAM.Tool counterSinkTool = FindToolByName(workPart, COUNTERSINK_TOOL_NAME);
            NXOpen.CAM.Tool tapTool = FindToolByName(workPart, TAP_TOOL_NAME);

            if (counterSinkTool == null)
            {
                lw.WriteLine("ERRO: ferramenta '" + COUNTERSINK_TOOL_NAME + "' não encontrada na biblioteca. Abortando.");
                return;
            }

            if (tapTool == null)
            {
                lw.WriteLine("ERRO: ferramenta '" + TAP_TOOL_NAME + "' não encontrada na biblioteca. Abortando.");
                return;
            }

            List<NXOpen.CAM.FeatureGeometryGroup> gruposM10 = GetM10Groups(workPart, lw);
            lw.WriteLine("Quantidade de grupos M10 (cor amarela) encontrados: " + gruposM10.Count);

            double depth = M10_NOMINAL_DIAM * MIN_ENGAGEMENT_FACTOR;

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in gruposM10)
            {
                CreateCountersinkOperation(workPart, tapM10Folder, method1, counterSinkTool, fg, lw);
                CreateTappingOperation(workPart, tapM10Folder, method1, tapTool, fg, depth, lw);
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
    }

    private static void CreateCountersinkOperation(
        NXOpen.Part workPart, NXOpen.CAM.NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, NXOpen.CAM.FeatureGeometryGroup fg, NXOpen.ListingWindow lw)
    {
        string operationName = MakeUniqueOperationName(workPart, "CSINK_M10X1.5");

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

        // RPM/Feed calculados com a fórmula de broca (mesma do resto da
        // automação). Usa o diâmetro nominal M10 como referência, já que o
        // escareador trabalha nessa região do furo.
        double csinkRpm, csinkFeed;
        CalculateDrillCuttingParameters(M10_NOMINAL_DIAM, out csinkRpm, out csinkFeed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = csinkRpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = csinkFeed;

        // Escareador não precisa desabilitar gouge check (só o macho tinha
        // esse problema, por causa da geometria da rosca já usinada).

        NXObject committed = builder.Commit();

        NXOpen.CAM.CAMObject[] objects = new NXOpen.CAM.CAMObject[1];
        objects[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects);

        if (VERBOSE)
            lw.WriteLine("Escareador criado para: " + fg.Name);

        builder.Destroy();
    }

    private static void CreateTappingOperation(
        NXOpen.Part workPart, NXOpen.CAM.NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, NXOpen.CAM.FeatureGeometryGroup fg, double depth, NXOpen.ListingWindow lw)
    {
        string operationName = MakeUniqueOperationName(workPart, "TAP_M10X1.5_" + depth.ToString("0.###", CultureInfo.InvariantCulture) + "MM");

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

        // RPM/Feed calculados com a fórmula de ROSQUEAMENTO (não a de broca):
        // Feed = passo x RPM, já que o macho avança um passo por rotação.
        double tapRpm, tapFeed;
        CalculateTapCuttingParameters(M10_NOMINAL_DIAM, TAP_PITCH_MM, out tapRpm, out tapFeed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = tapRpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = tapFeed;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegions.Type =
            NXOpen.CAM.NcmTransfer.TransferTypes.LowestSafeZ;

        // Necessário pro macho: sem isso, a operação não gera trajetória
        // (acusa colisão por causa da rosca já usinada).
        DisableGougeCheck(builder);

        NXObject committed = builder.Commit();

        NXOpen.CAM.CAMObject[] objects = new NXOpen.CAM.CAMObject[1];
        objects[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects);

        if (VERBOSE)
            lw.WriteLine("Macho M10 (profundidade " + depth.ToString(CultureInfo.InvariantCulture) + "mm) criado para: " + fg.Name);

        builder.Destroy();
    }

    // Filtra os grupos FG_STEP* cuja cor (lida direto da face) é amarela (id 6 = M10).
    private static List<NXOpen.CAM.FeatureGeometryGroup> GetM10Groups(NXOpen.Part workPart, NXOpen.ListingWindow lw)
    {
        List<NXOpen.CAM.FeatureGeometryGroup> resultado = new List<NXOpen.CAM.FeatureGeometryGroup>();
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.FeatureGeometryGroup fg = obj as NXOpen.CAM.FeatureGeometryGroup;
            if (fg == null)
                continue;

            if (!fg.Name.StartsWith("FG_STEP"))
                continue;

            int colorId;
            string erro;
            bool ok = TryGetGroupColorId(fg, out colorId, out erro);

            if (!ok)
            {
                lw.WriteLine("AVISO: " + fg.Name + " -> não deu pra ler a cor: " + erro + ". Ignorado.");
                continue;
            }

            if (colorId == COLOR_M10)
                resultado.Add(fg);
        }

        return resultado;
    }

    private static bool TryGetGroupColorId(NXOpen.CAM.FeatureGeometryGroup fg, out int colorId, out string erro)
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

    private static NXOpen.CAM.Tool FindToolByName(NXOpen.Part workPart, string toolName)
    {
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.Tool t = obj as NXOpen.CAM.Tool;
            if (t != null && string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

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

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
