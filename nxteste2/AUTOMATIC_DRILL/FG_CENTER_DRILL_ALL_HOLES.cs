using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;

public class FG_CENTER_DRILL_ALL_HOLES
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;

        if (workPart == null)
            return;

        // ── Buffer de log em memória: nada é escrito na janela Information
        // durante a execução. Só no final (bloco finally) a janela é aberta
        // e todo o conteúdo acumulado é despejado de uma vez. ──
        LogBuffer lw = new LogBuffer();

        NXOpen.UF.UFSession ufs = NXOpen.UF.UFSession.GetUFSession();

        try
        {
            NXOpen.CAM.NCGroup nCGroup1 =
                ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));

            NXOpen.CAM.NCGroup nCGroup2 =
                ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));

            NXOpen.CAM.Tool tool1 =
                ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CENTER_DRILL"));

            // Pasta que organiza todas as operações de furo de centro juntas
            NXOpen.CAM.NCGroup centerDrillsFolder = FindOrCreateProgramFolder(workPart, nCGroup1, "CENTER_DRILLS");

            List<NXOpen.CAM.FeatureGeometryGroup> featureGroups = GetFeatureGeometryGroups(workPart);

            lw.WriteLine("Quantidade de feature groups encontrados: " + featureGroups.Count);

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in featureGroups)
            {
                double diametro;
                string erro;
                bool temDiametro = TryMeasureDiameter(fg, ufs, out diametro, out erro);

                CreateSpotDrillingOperation(
                    workPart,
                    centerDrillsFolder,
                    nCGroup2,
                    tool1,
                    fg,
                    temDiametro ? diametro : (double?)null,
                    lw);
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
        // A janela Information não é mais aberta — o log fica só em memória
        // e é descartado ao final da execução (os scripts já estão validados).
    }

    // ── Buffer simples de log: acumula linhas em memória em vez de escrever
    // direto na ListingWindow (janela Information), evitando que ela suba
    // repetidamente durante a execução. Só é despejada no final (ver finally
    // do Run). Expõe WriteLine com a mesma assinatura da ListingWindow pra
    // não precisar mudar nenhuma chamada existente no resto do código. ──
    private class LogBuffer
    {
        public List<string> Lines = new List<string>();

        public void WriteLine(string text)
        {
            Lines.Add(text);
        }
    }

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

            if (fg.Name.StartsWith("FG_STEP") ||
                fg.Name.StartsWith("FG_HOLE"))
            {
                groups.Add(fg);
            }
        }

        return groups;
    }

    // Mede o diâmetro pegando a primeira face cilíndrica encontrada na
    // feature (a maior/entrada, já que é só pra nomear a operação de centro).
    private static bool TryMeasureDiameter(NXOpen.CAM.FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs, out double diameter, out string erro)
    {
        diameter = 0.0;
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

            foreach (NXOpen.Face face in faces)
            {
                if (face.SolidFaceType != NXOpen.Face.FaceType.Cylindrical)
                    continue;

                int faceType;
                double[] facePt = new double[3];
                double[] faceDir = new double[3];
                double[] bbox = new double[6];
                double faceRadius, faceRadData;
                int normDirection;

                ufs.Modl.AskFaceData(face.Tag, out faceType, facePt, faceDir, bbox, out faceRadius, out faceRadData, out normDirection);

                diameter = faceRadius * 2.0;
                return true;
            }

            erro = "nenhuma face cilíndrica encontrada";
            return false;
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

    private static void CreateSpotDrillingOperation(
        NXOpen.Part workPart,
        NXOpen.CAM.NCGroup nCGroup1,
        NXOpen.CAM.NCGroup nCGroup2,
        NXOpen.CAM.Tool tool1,
        NXOpen.CAM.FeatureGeometryGroup featureGeometryGroup1,
        double? diametro,
        LogBuffer lw)
    {
        lw.WriteLine("Criando operação para o grupo: " + featureGeometryGroup1.Name);

        // Nome da operação baseado no diâmetro medido (SPOT_DRILL_D_<diam>),
        // ou no nome do grupo se não conseguir medir o diâmetro.
        string operationName = diametro.HasValue
            ? "SPOT_DRILL_D_" + diametro.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            : "SPOT_DRILL_" + featureGeometryGroup1.Name;

        operationName = MakeUniqueOperationName(workPart, operationName);

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

        holeDrillingBuilder1.PredefinedDepth.Status = true;
        holeDrillingBuilder1.PredefinedDepth.Value = 2.0;

        holeDrillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 1500.0;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 10.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = holeDrillingBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling2 = ((NXOpen.CAM.HoleDrilling)nXObject1);
        objects1[0] = holeDrilling2;
        workPart.CAMSetup.GenerateToolPath(objects1);

        lw.WriteLine("Trajetória gerada para: " + featureGeometryGroup1.Name + " -> operação: " + operationName);

        holeDrillingBuilder1.Destroy();
    }

    // Gera um nome único de operação (evita "Input name already exists"
    // se dois furos tiverem o mesmo diâmetro).
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
