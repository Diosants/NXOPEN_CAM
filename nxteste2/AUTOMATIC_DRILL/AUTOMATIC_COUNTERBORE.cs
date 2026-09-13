using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;
using NXOpen.CAM;
using Operation = NXOpen.CAM.Operation;
public class AUTOMATIC_COUNTERBORE
{
    // Controla o quanto o log mostra: false = só avisos/resumo; true = mostra tudo.
    private const bool VERBOSE = true; // <<< LIGADO PRA DEBUG
    private const string CBORE_TOOL_PREFIX = "CBORE_";
    private const string CBORE_TOOL_SUFFIX = "MM";
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
    // ══════════════════════════════════════════════════════════════════════
    // TABELA DE MATERIAIS — Vc (m/min) por tipo de ferramenta.
    // Fontes cruzadas: Machining Doctor, Redline Tools, AIMS Industrial,
    // Machinery's Handbook. Valores são ponto de partida - ajuste conforme
    // sua experiência real de chão de fábrica, revestimento de ferramenta,
    // rigidez da máquina, etc.
    // ══════════════════════════════════════════════════════════════════════
    private class MaterialInfo
    {
        public string Name;
        public double DrillVc;         // brocas HSS
        public double EndmillRoughVc;  // fresa topo - desbaste (carboneto)
        public double EndmillFinishVc; // fresa topo - acabamento (carboneto)
        public double CutterVc;        // cabeçote/facemill (carboneto)
    }
    private static readonly List<MaterialInfo> MaterialTable = new List<MaterialInfo>
    {
        new MaterialInfo { Name = "1020 (aço baixo carbono)",  DrillVc = 25, EndmillRoughVc = 80, EndmillFinishVc = 100, CutterVc = 220 },
        new MaterialInfo { Name = "1045 (aço médio carbono)",  DrillVc = 20, EndmillRoughVc = 70, EndmillFinishVc = 90,  CutterVc = 200 },
        new MaterialInfo { Name = "P20 (aço molde, pré-temperado)", DrillVc = 15, EndmillRoughVc = 55, EndmillFinishVc = 75, CutterVc = 150 },
        new MaterialInfo { Name = "H13 (aço ferramenta, temperado)", DrillVc = 10, EndmillRoughVc = 45, EndmillFinishVc = 65, CutterVc = 130 },
        new MaterialInfo { Name = "Aluminio",  DrillVc = 80, EndmillRoughVc = 250, EndmillFinishVc = 350, CutterVc = 500 },
        new MaterialInfo { Name = "Cobre",     DrillVc = 30, EndmillRoughVc = 120, EndmillFinishVc = 160, CutterVc = 250 },
        new MaterialInfo { Name = "Nylon/Polimero", DrillVc = 60, EndmillRoughVc = 200, EndmillFinishVc = 300, CutterVc = 400 },
    };
    private static readonly MaterialInfo DefaultMaterial = MaterialTable[1]; // "1045 (aço médio carbono)"
    public static event Action<int, int> OnProgress;
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        if (workPart == null)
            return;
        LogBuffer lw = new LogBuffer();
        try
        {
            NCGroup nCGroup1 = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");
            NCGroup nCGroup2 = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE");
            MaterialInfo material = DefaultMaterial;
            lw.WriteLine("Material usado (padrão, sem prompt): " + material.Name);
            const double FEED_PER_REV = 0.1;
            Dictionary<string, Tool> toolCache = BuildToolCache(workPart);
            Dictionary<string, bool> usedNames = BuildUsedNamesCache(workPart);
            Dictionary<string, NCGroup> folderCache = new Dictionary<string, NCGroup>();

            // ── DEBUG: lista TODAS as ferramentas encontradas no cache ──
            lw.WriteLine("=== DEBUG: ferramentas no toolCache (" + toolCache.Count + ") ===");
            foreach (var kv in toolCache)
                lw.WriteLine("   tool na biblioteca: '" + kv.Key + "'  (Length=" + kv.Key.Length + ")");
            lw.WriteLine("=== FIM DEBUG ferramentas ===");

            List<FeatureGeometryGroup> groups = GetStep2HoleGroups(workPart);
            lw.WriteLine("Quantidade de grupos encontrados (FG_STEP2HOLE / FG_STEP1POCKET): " + groups.Count);
            int processados = 0;
            int totalGroups = groups.Count;
            int indiceAtual = 0;
            foreach (FeatureGeometryGroup fg in groups)
            {
                indiceAtual++;
                if (OnProgress != null)
                    OnProgress(indiceAtual, totalGroups);
                double diameter1;
                string erro;
                if (!TryGetAttributeDouble(fg, "DIAMETER_1", out diameter1, out erro))
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> não conseguiu ler DIAMETER_1: " + erro + ". Grupo ignorado.");
                    continue;
                }
                // ── DEBUG: valor bruto com mais casas decimais ──
                lw.WriteLine("DEBUG: " + fg.Name + " -> DIAMETER_1 bruto = " + diameter1.ToString("0.000000", CultureInfo.InvariantCulture));

                string diameterLabel = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                string toolName = CBORE_TOOL_PREFIX + diameterLabel + CBORE_TOOL_SUFFIX;
                lw.WriteLine("DEBUG: " + fg.Name + " -> toolName procurado = '" + toolName + "'  (Length=" + toolName.Length + ")");
                Tool tool;
                if (!toolCache.TryGetValue(toolName, out tool))
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta '" + toolName + "' (DIAMETER_1 = " + diameterLabel + ") não encontrada na biblioteca. Grupo ignorado.");
                    // ── DEBUG: lista candidatas parecidas pra comparar caractere a caractere ──
                    foreach (var kv in toolCache)
                    {
                        if (kv.Key.IndexOf(diameterLabel, StringComparison.OrdinalIgnoreCase) >= 0
                            || kv.Key.IndexOf("48", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            lw.WriteLine("   candidata parecida na biblioteca: '" + kv.Key + "'  (Length=" + kv.Key.Length + ")");
                        }
                    }
                    continue;
                }
                string folderName = "CBORE_GROUP_" + diameterLabel + "MM";
                NCGroup folder;
                if (!folderCache.TryGetValue(folderName, out folder))
                {
                    folder = FindOrCreateProgramFolder(workPart, nCGroup1, folderName, usedNames, lw);
                    folderCache[folderName] = folder;
                }
                if (VERBOSE)
                    lw.WriteLine("Grupo " + fg.Name + " -> DIAMETER_1 = " + diameterLabel + " -> ferramenta " + toolName + " -> pasta " + folderName);
                CreateCounterboreOperation(workPart, folder, nCGroup2, tool, fg, diameterLabel, diameter1, material.DrillVc, FEED_PER_REV, usedNames, lw);
                processados++;
            }
            lw.WriteLine("Total de counterbores processados: " + processados);
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
        finally
        {
            // ── DEBUG: dump do log pra janela Information, só nesta versão de diagnóstico ──
            NXOpen.ListingWindow listingWindow = theSession.ListingWindow;
            listingWindow.Open();
            foreach (string line in lw.Lines)
                listingWindow.WriteLine(line);
        }
    }
    private static void CreateCounterboreOperation(
        Part workPart, NCGroup nCGroup1, NCGroup nCGroup2,
        Tool tool1, FeatureGeometryGroup featureGeometryGroup1,
        string diameterLabel, double diameter, double vc, double feedPerRev,
        Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string baseName = "CBORE_D" + diameterLabel;
        string operationName = MakeUniqueOperationName(usedNames, baseName);
        Operation operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, nCGroup2, tool1, featureGeometryGroup1,
            "hole_making", "COUNTERBORING",
            NXOpen.CAM.OperationCollection.UseDefaultName.False,
            operationName, operationName);
        NXOpen.CAM.HoleDrilling holeDrilling1 = (NXOpen.CAM.HoleDrilling)operation1;
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1 =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);
        double rpm = (vc * 318.0) / diameter;
        double feed = rpm * feedPerRev;
        if (VERBOSE)
            lw.WriteLine("  RPM calculado: " + rpm.ToString("0", CultureInfo.InvariantCulture)
                + "  Feed calculado: " + feed.ToString("0", CultureInfo.InvariantCulture));
        holeDrillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        holeDrillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = feed;
        holeDrillingBuilder1.CuttingParameters.TopOffset.Distance = 2.0;
        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 30.0;
        NXObject committed = holeDrillingBuilder1.Commit();
        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        objects1[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects1);
        if (VERBOSE)
            lw.WriteLine("Counterbore criado para: " + featureGeometryGroup1.Name);
        holeDrillingBuilder1.Destroy();
    }
    // Prefixos de FeatureGeometryGroup que devem ser tratados como candidatos
    // a counterbore. FG_STEP2HOLE cobre furo escalonado "normal"; FG_STEP1POCKET
    // apareceu como o nome real usado pra alguns counterbores (ex.: o de 48mm)
    // e por isso também precisa entrar aqui - sem isso o grupo nem chega a ser
    // avaliado (não aparece nem como AVISO no log, porque é descartado antes
    // de qualquer leitura de DIAMETER_1 ou busca de ferramenta).
    private static readonly string[] VALID_GROUP_PREFIXES = { "FG_STEP2HOLE", "FG_STEP1POCKET" };
    private static List<FeatureGeometryGroup> GetStep2HoleGroups(Part workPart)
    {
        List<FeatureGeometryGroup> groups = new List<FeatureGeometryGroup>();
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            FeatureGeometryGroup fg = obj as FeatureGeometryGroup;
            if (fg == null)
                continue;
            foreach (string prefix in VALID_GROUP_PREFIXES)
            {
                if (fg.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    groups.Add(fg);
                    break;
                }
            }
        }
        return groups;
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
    private static Dictionary<string, Tool> BuildToolCache(Part workPart)
    {
        Dictionary<string, Tool> cache = new Dictionary<string, Tool>(StringComparer.OrdinalIgnoreCase);
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            Tool t = obj as Tool;
            if (t != null && !cache.ContainsKey(t.Name))
                cache[t.Name] = t;
        }
        return cache;
    }
    private static Dictionary<string, bool> BuildUsedNamesCache(Part workPart)
    {
        Dictionary<string, bool> names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
            names[obj.Name] = true;
        Operation[] operations = workPart.CAMSetup.CAMOperationCollection.ToArray();
        foreach (Operation op in operations)
            names[op.Name] = true;
        return names;
    }
    private static NCGroup FindOrCreateProgramFolder(Part workPart, NCGroup parentGroup, string folderName, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        bool jaExiste;
        if (usedNames.TryGetValue(folderName, out jaExiste))
        {
            CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
            foreach (CAMObject obj in objects)
            {
                NCGroup existing = obj as NCGroup;
                if (existing != null && string.Equals(existing.Name, folderName, StringComparison.OrdinalIgnoreCase))
                    return existing;
            }
        }
        try
        {
            NCGroup novaPasta = workPart.CAMSetup.CAMGroupCollection.CreateProgramWithUserName(
                parentGroup, "hole_making", "PROGRAM",
                NXOpen.CAM.NCGroupCollection.UseDefaultName.False, folderName, "Program");
            NXOpen.CAM.ProgramOrderGroupBuilder programOrderGroupBuilder1 =
                workPart.CAMSetup.CAMGroupCollection.CreateProgramOrderGroupBuilder(novaPasta);
            programOrderGroupBuilder1.Commit();
            programOrderGroupBuilder1.Destroy();
            usedNames[folderName] = true;
            return novaPasta;
        }
        catch (Exception ex)
        {
            lw.WriteLine("AVISO: falha ao criar a pasta '" + folderName + "': " + ex.Message + ". Operações desse diâmetro vão ficar soltas em " + parentGroup.Name + ".");
            return parentGroup;
        }
    }
    private static string MakeUniqueOperationName(Dictionary<string, bool> usedNames, string baseName)
    {
        if (!usedNames.ContainsKey(baseName))
        {
            usedNames[baseName] = true;
            return baseName;
        }
        int counter = 2;
        while (true)
        {
            string candidate = baseName + "_" + counter;
            if (!usedNames.ContainsKey(candidate))
            {
                usedNames[candidate] = true;
                return candidate;
            }
            counter++;
        }
    }
    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
