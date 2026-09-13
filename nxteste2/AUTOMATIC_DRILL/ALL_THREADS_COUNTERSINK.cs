using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;

public class ALL_THREADS_COUNTERSINK
{
    // Controla o quanto o log mostra quando ALGO DA ERRADO (o log so e
    // exibido nesse caso - ver logica no finally do Run). false = so
    // avisos de furo nao realizado; true = mostra tudo, incluindo sucesso.
    private const bool VERBOSE = false;

    // ── Buffer simples de log: acumula linhas em memória em vez de escrever
    // direto na ListingWindow (janela Information). So e exibido se algo
    // der errado (ver logica no finally do Run) - se tudo ocorrer bem,
    // nenhuma mensagem aparece. ──
    private class LogBuffer
    {
        public List<string> Lines = new List<string>();

        public void WriteLine(string text)
        {
            Lines.Add(text);
        }
    }

    // ── Tabela de roscas reconhecidas: diâmetro de furo (contra-furo) ->
    // dados da rosca. ATUALIZADO: antes essa identificação era feita por
    // COR da face (só reconhecia 4 tamanhos: M8/M10/M12/M16, e quebrava se
    // alguém mudasse a cor da geometria por engano). Agora usa o mesmo
    // critério robusto do resto do pipeline (AUTOMATIC_TAP_AND_SOCKET.cs /
    // AUTOMATIC_HOLE_BOSS_GEOM.cs): tipo de feature (Through Hole / Blind
    // Hole) + diâmetro medido de verdade (atributo DIAMETER_1), com
    // tolerância de casamento. Cobre 12 tamanhos (M4-M24) em vez de só 4. ──
    private class ThreadInfo
    {
        public double DrillDiameter; // diâmetro do furo de broca (contra-furo) pra casar com DIAMETER_1
        public string Name;          // "M10"
        public double NominalDiam;   // 10.0
        public double Pitch;         // 1.5 (passo, mm)
        public string TapToolName;   // "TAP_M10X1.5" - nome exato na biblioteca
    }

    private const double MATCH_TOLERANCE = 0.15; // mm - tolerância de casamento do diâmetro medido contra a tabela

    // Tabela construída em BuildThreadTable() - ver comentário lá embaixo
    // sobre a convenção de nome de ferramenta (formato "0.0##" no passo).

    private const double MIN_ENGAGEMENT_FACTOR = 1.5; // profundidade = 1,5x o nominal

    // Ferramenta de escareador - genérica, a mesma pra qualquer rosca.
    private const string COUNTERSINK_TOOL_NAME = "COUNTERSINK";

    // ── Parâmetros de corte pra furação (escareador): RPM = (Vc x 318) / diâmetro. ──
    private const double DRILL_CUTTING_SPEED_VC = 25.0;
    private const double DRILL_FEED_PER_REV = 0.1;

    // ── Parâmetros de corte pra ROSQUEAMENTO (macho): Feed = passo x RPM. ──
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

    // ══════════════════ TABELA DE ROSCAS (por tipo + diâmetro, não mais por cor) ══════════════════
    //
    // AVISO SOBRE NOMES DE FERRAMENTA: só 4 tamanhos estavam confirmados no
    // arquivo original (via dicionário de cor): M8, M10, M12 e M16 - com os
    // nomes literais "TAP_M8X1.25", "TAP_M10X1.5", "TAP_M12X1.75" e
    // "TAP_M16X2.0". Reparei que esses 4 nomes seguem uma convenção
    // consistente: o passo é sempre escrito com PELO MENOS uma casa decimal
    // (é por isso que M16 é "X2.0" e não "X2"). Apliquei essa MESMA
    // convenção (formato "0.0##") pros outros 8 tamanhos (M4, M5, M6, M14,
    // M18, M20, M22, M24), que não têm arquivo de referência conhecido. Se
    // a ferramenta não existir na biblioteca com esse nome exato, o furo é
    // só logado e pulado (não trava o resto do script) - ver AVISO no log.
    //
    // Isso é diferente da pasta AUTOMATIC_TAP_AND_SOCKET.cs (a nova, feita
    // do zero), que usa formato "0.###" (sem casa decimal forçada) pro nome
    // de pasta/ferramenta - lá o único tamanho confirmado por arquivo real
    // era M10 ("TAP_M10X1.5", que também bate nos dois formatos). Mantive
    // AQUI a convenção deste arquivo (COUNTERSINK, não COUNTER_SINK; passo
    // sempre com decimal) porque é o que já está validado na produção de
    // vocês pra esse pipeline específico - não troquei pela convenção do
    // outro arquivo.
    private static List<ThreadInfo> BuildThreadTable()
    {
        List<ThreadInfo> list = new List<ThreadInfo>();
        AddThread(list, 3.3, 4.0, 0.7, "M4");
        AddThread(list, 4.2, 5.0, 0.8, "M5");
        AddThread(list, 5.0, 6.0, 1.0, "M6");
        AddThread(list, 6.8, 8.0, 1.25, "M8");
        AddThread(list, 8.5, 10.0, 1.5, "M10");
        AddThread(list, 10.2, 12.0, 1.75, "M12");
        AddThread(list, 12.0, 14.0, 2.0, "M14");
        AddThread(list, 14.0, 16.0, 2.0, "M16");
        AddThread(list, 15.5, 18.0, 2.5, "M18");
        AddThread(list, 17.5, 20.0, 2.5, "M20");
        AddThread(list, 19.5, 22.0, 2.5, "M22");
        AddThread(list, 21.0, 24.0, 3.0, "M24");
        return list;
    }

    private static void AddThread(List<ThreadInfo> list, double drillDiam, double nominal, double pitch, string sizeLabel)
    {
        string pitchStr = pitch.ToString("0.0##", CultureInfo.InvariantCulture); // sempre >= 1 casa decimal, ex: "2.0", "1.5", "1.25"
        list.Add(new ThreadInfo
        {
            DrillDiameter = drillDiam,
            Name = sizeLabel,
            NominalDiam = nominal,
            Pitch = pitch,
            TapToolName = "TAP_" + sizeLabel + "X" + pitchStr
        });
    }

    private static ThreadInfo FindClosestThread(List<ThreadInfo> table, double diameter)
    {
        ThreadInfo best = null;
        double bestDiff = double.MaxValue;
        foreach (ThreadInfo t in table)
        {
            double diff = Math.Abs(t.DrillDiameter - diameter);
            if (diff <= MATCH_TOLERANCE && diff < bestDiff)
            {
                bestDiff = diff;
                best = t;
            }
        }
        return best;
    }

    /// <summary>
    /// Ponto de entrada pra chamar a partir da aplicacao:
    /// ALL_THREADS_COUNTERSINK.Run(null);
    /// </summary>
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;

        if (workPart == null)
            return;

        LogBuffer lw = new LogBuffer();
        bool hadError = false;

        try
        {
            NXOpen.CAM.NCGroup nCGroup1 =
                (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");

            NXOpen.CAM.Method method1 =
                (NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD");

            NXOpen.CAM.Tool counterSinkTool = FindToolByName(workPart, COUNTERSINK_TOOL_NAME);

            if (counterSinkTool == null)
            {
                lw.WriteLine("ERRO: ferramenta '" + COUNTERSINK_TOOL_NAME + "' não encontrada na biblioteca. Abortando.");
                hadError = true;
                return;
            }

            List<ThreadInfo> threadTable = BuildThreadTable();
            List<NXOpen.CAM.FeatureGeometryGroup> allGroups = GetStepGroups(workPart);

            if (VERBOSE)
                lw.WriteLine("Quantidade de feature groups encontrados: " + allGroups.Count);

            // Cache de ferramenta de macho e pasta por tipo de rosca, pra não
            // buscar/criar de novo a cada furo do mesmo tipo.
            Dictionary<string, NXOpen.CAM.Tool> tapToolCache = new Dictionary<string, NXOpen.CAM.Tool>();
            Dictionary<string, NXOpen.CAM.NCGroup> folderCache = new Dictionary<string, NXOpen.CAM.NCGroup>();

            int processados = 0;

            // diagnóstico: diâmetros de furo (Through/Blind Hole) que não
            // bateram com NENHUM tamanho de rosca conhecido - responde
            // direto "por que essa rosca não foi reconhecida" (fora de
            // tolerância de 0.15mm, ou é um passo/tamanho novo).
            Dictionary<string, int> diametrosNaoReconhecidos = new Dictionary<string, int>();

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in allGroups)
            {
                NXOpen.CAM.CAMFeature[] feats = fg.GetFeatures();
                if (feats == null || feats.Length == 0)
                    continue;

                string tipo = StripTrailingNumber(feats[0].Name ?? "");

                double diameter1;
                string erroDiam;
                bool temDiametro = TryGetAttributeDouble(fg, "DIAMETER_1", out diameter1, out erroDiam);

                if (!temDiametro)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> não deu pra ler DIAMETER_1: " + erroDiam + ". Ignorado.");
                    continue;
                }

                ThreadInfo threadInfo = FindClosestThread(threadTable, diameter1);

                if (threadInfo == null)
                {
                    // não bateu com nenhum tamanho de rosca - não é
                    // necessariamente problema (pode ser furo normal), só
                    // registra pro diagnóstico agregado.
                    string diamKey = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                    if (!diametrosNaoReconhecidos.ContainsKey(diamKey))
                        diametrosNaoReconhecidos[diamKey] = 0;
                    diametrosNaoReconhecidos[diamKey]++;
                    continue;
                }

                // O diâmetro bate com uma rosca da tabela, mas só cria
                // operação se o TIPO da feature também for compatível com
                // furo roscado. "Pocket Thread" é o tipo real usado pelo NX
                // pras roscas reconhecidas como STEP1POCKET_THREAD (ver
                // featureTypes1 em CREATE_GROUP_FEATURES.cs) - confirmado
                // batendo exatamente com os diâmetros de furo de macho
                // (8.5/10.3/12.1/14/15.5/17.5mm) num teste real. "Through
                // Hole"/"Blind Hole" ficam como fallback pro caso de a peça
                // ter rosca reconhecida por esse outro tipo também. Isso
                // evita criar macho num furo que só COINCIDE em diâmetro com
                // uma rosca mas é, por exemplo, um contra-furo counter-bore
                // comum (ex: os dois furos de 14mm no log de teste - um era
                // FG_STEP2HOLE/contra-furo normal, outro era
                // FG_STEP1POCKET_THREAD/rosca de verdade).
                bool isTipoRoscaAceito =
                    string.Equals(tipo, "Pocket Thread", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipo, "Through Hole", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipo, "Blind Hole", StringComparison.OrdinalIgnoreCase);

                if (!isTipoRoscaAceito)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> diâmetro " + diameter1.ToString("0.###", CultureInfo.InvariantCulture)
                        + "mm bate com rosca " + threadInfo.Name + ", mas o tipo de feature é '" + tipo
                        + "' (não é Pocket Thread/Through Hole/Blind Hole) - PULADO por segurança pra não fazer macho num furo que não é rosca de verdade."
                        + " Se ESSE furo for realmente uma rosca, me avise esse nome de tipo exato pra eu adicionar na lista aceita.");
                    continue;
                }

                // ── Ferramenta de macho (cacheada por tipo de rosca) ──
                NXOpen.CAM.Tool tapTool;
                if (!tapToolCache.TryGetValue(threadInfo.Name, out tapTool))
                {
                    tapTool = FindToolByName(workPart, threadInfo.TapToolName);
                    tapToolCache[threadInfo.Name] = tapTool;
                }

                if (tapTool == null)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta '" + threadInfo.TapToolName + "' (rosca " + threadInfo.Name + ") não encontrada na biblioteca. Grupo ignorado.");
                    continue;
                }

                // ── Pasta de organização por tipo de rosca (cacheada) ──
                NXOpen.CAM.NCGroup folder;
                if (!folderCache.TryGetValue(threadInfo.Name, out folder))
                {
                    string folderName = "TAP" + threadInfo.Name + "X" + threadInfo.Pitch.ToString("0.0##", CultureInfo.InvariantCulture);
                    folder = FindOrCreateProgramFolder(workPart, nCGroup1, folderName);
                    folderCache[threadInfo.Name] = folder;
                }

                double depth = threadInfo.NominalDiam * MIN_ENGAGEMENT_FACTOR;

                CreateCountersinkOperation(workPart, folder, method1, counterSinkTool, fg, threadInfo, lw);
                CreateTappingOperation(workPart, folder, method1, tapTool, fg, threadInfo, depth, lw);

                processados++;
            }

            if (VERBOSE)
                lw.WriteLine("Total de furos com rosca processados: " + processados);

            if (diametrosNaoReconhecidos.Count > 0)
            {
                lw.WriteLine("--- Diâmetros de furo (Through/Blind Hole) que NÃO bateram com nenhum tamanho de rosca conhecido (tolerância de 0.15mm): ---");
                List<KeyValuePair<string, int>> ordenado = new List<KeyValuePair<string, int>>(diametrosNaoReconhecidos);
                ordenado.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (KeyValuePair<string, int> kv in ordenado)
                    lw.WriteLine("   D" + kv.Key + "mm -> " + kv.Value + " furo(s)");
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
            hadError = true;
        }
        finally
        {
            // So exibe a Listing Window se algo deu errado (aviso ou
            // excecao). Se tudo ocorreu bem, nenhuma mensagem aparece.
            if (hadError || lw.Lines.Exists(l => l.StartsWith("AVISO") || l.StartsWith("ERRO") || l.StartsWith("---")))
            {
                theSession.ListingWindow.Open();
                foreach (string line in lw.Lines)
                {
                    theSession.ListingWindow.WriteLine(line);
                }
            }
        }
    }

    private static void CreateCountersinkOperation(
        NXOpen.Part workPart, NXOpen.CAM.NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, NXOpen.CAM.FeatureGeometryGroup fg, ThreadInfo threadInfo, LogBuffer lw)
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
        NXOpen.Part workPart, NXOpen.CAM.NCGroup nCGroup1, NXOpen.CAM.Method method1,
        NXOpen.CAM.Tool tool, NXOpen.CAM.FeatureGeometryGroup fg, ThreadInfo threadInfo, double depth, LogBuffer lw)
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

        // Necessário pro macho: sem isso, a operação não gera trajetória
        // (acusa colisão por causa da rosca já usinada).
        DisableGougeCheck(builder);

        NXObject committed = builder.Commit();

        NXOpen.CAM.CAMObject[] objects = new NXOpen.CAM.CAMObject[1];
        objects[0] = (NXOpen.CAM.HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objects);

        if (VERBOSE)
            lw.WriteLine("Macho " + threadInfo.Name + " (profundidade " + depth.ToString(CultureInfo.InvariantCulture) + "mm) criado para: " + fg.Name);

        builder.Destroy();
    }

    private static List<NXOpen.CAM.FeatureGeometryGroup> GetStepGroups(NXOpen.Part workPart)
    {
        List<NXOpen.CAM.FeatureGeometryGroup> resultado = new List<NXOpen.CAM.FeatureGeometryGroup>();
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.FeatureGeometryGroup fg = obj as NXOpen.CAM.FeatureGeometryGroup;
            if (fg == null)
                continue;

            if (fg.Name.StartsWith("FG_STEP"))
                resultado.Add(fg);
        }

        return resultado;
    }

    // Remove os dígitos/pontos finais do nome da feature (ex: "Through Hole1" -> "Through Hole"),
    // igual ao padrão usado em AUTOMATIC_HOLE_BOSS_GEOM.cs / AUTOMATIC_TAP_AND_SOCKET.cs.
    private static string StripTrailingNumber(string nome)
    {
        int i = nome.Length;
        while (i > 0 && (char.IsDigit(nome[i - 1]) || nome[i - 1] == '.'))
            i--;
        return nome.Substring(0, i).TrimEnd();
    }

    // Lê um atributo numérico (ex: DIAMETER_1) da primeira CAMFeature do
    // grupo via reflection - mesmo padrão validado em
    // AUTOMATIC_TAP_AND_SOCKET.cs / AUTOMATIC_HOLE_BOSS_GEOM.cs.
    private static bool TryGetAttributeDouble(NXOpen.CAM.FeatureGeometryGroup fg, string attrName, out double value, out string erro)
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
                catch { }
            }
            System.Reflection.MethodInfo getDouble = attr.GetType().GetMethod("GetDoubleValue", Type.EmptyTypes);
            if (getDouble == null)
            {
                erro = "GetDoubleValue não encontrado via reflection";
                return false;
            }
            value = (double)getDouble.Invoke(attr, null);
            return true;
        }
        catch (Exception ex)
        {
            erro = ex.Message;
            return false;
        }
    }

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
