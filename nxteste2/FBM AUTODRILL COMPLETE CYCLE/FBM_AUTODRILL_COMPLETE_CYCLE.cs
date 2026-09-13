using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using NXOpen;
using NXOpen.CAM;
using NXOpen.CAM.FBM;

// ══════════════════════════════════════════════════════════════════════
// CICLO COMPLETO: FBM (fresagem) + AUTODRILL (furação/rosca/contra-furo),
// numa única classe/botão, incluindo o reconhecimento e a criação das
// figuras que os dois processos precisam - sem depender de rodar
// RECOGNIZE_FEATURES/CREATE_GROUP_FEATURES manualmente antes.
//
// COMO FUNCIONA (ordem dentro de Run()):
//   FASE 0a - Reconhecimento + agrupamento, mas SÓ dos 4 tipos de furo
//             (STEP1POCKET, STEP1HOLE, STEP2HOLE, STEP1POCKET_THREAD) -
//             os únicos tipos que FBM_ALL_FEATURES_MACHINED NÃO reconhece
//             sozinho (ele exclui esses 4 de propósito, porque não usina
//             furo). Sem essa fase, os grupos FG_STEP* nunca existiriam e
//             a parte de furação deste arquivo não teria nada pra
//             processar.
//   FASE 0b - FBM_ALL_FEATURES_MACHINED.Run(null) - roda o script de
//             fresagem inteiro (que já tem sua PRÓPRIA fase de
//             reconhecimento interna, pra pocket/slot/superfície
//             planar/WEDM - incluindo os WEDM_OBROUND_STRAIGHT e
//             WEDM_FREE_SHAPED_STRAIGHT recém adicionados). Esse script
//             TAMBÉM já cuida de TODO o FG_STEP2HOLE sozinho (coluna de
//             molde >=38mm com a sequência de 5 operações, E counterbore
//             comum <38mm) - por isso a fase de furação abaixo (FASE 1+)
//             NUNCA reprocessa FG_STEP2HOLE, ver decisão abaixo.
//   FASE 1+  - A furação propriamente dita (rosca, soquete allen,
//             contra-furo genérico, furo normal) - lógica idêntica à do
//             AUTODRILL_GEOMETRIA_PROPRIA.cs original, com UMA mudança:
//             grupos FG_STEP2HOLE são pulados aqui (contados e logados,
//             não processados) porque a FASE 0b já cuidou deles.
//
// DECISÃO IMPORTANTE (confirmada com o usuário): FBM_ALL_FEATURES_MACHINED
// e AUTODRILL_GEOMETRIA_PROPRIA processam os MESMOS grupos FG_STEP2HOLE,
// cada um com sua própria lógica (FBM: coluna de molde + counterbore
// comum; AutoDrill: soquete allen + contra-furo genérico) - rodar os
// dois script separadamente sobre o MESMO FG_STEP2HOLE geraria operações
// de furação DUPLICADAS na mesma peça. Resolvido escolhendo o FBM como
// dono EXCLUSIVO de FG_STEP2HOLE neste ciclo combinado (ele tem o
// tratamento mais completo, incluindo a coluna de molde que o AutoDrill
// não tem) - a parte de furação deste arquivo cuida só de ROSCA
// (qualquer grupo com "THREAD" no nome, inclusive FG_STEP1POCKET_THREAD),
// SOQUETE ALLEN / CONTRA-FURO GENÉRICO (só a partir de FG_STEP1POCKET,
// nunca FG_STEP2HOLE) e FURO NORMAL (FG_STEP1HOLE).
//
// POR QUE UMA FASE 0a SEPARADA (em vez de chamar RECOGNIZE_FEATURES.cs e
// CREATE_GROUP_FEATURES.cs originais): esses dois journals reconhecem e
// agrupam TANTO os 4 tipos de furo QUANTO os mesmos tipos de figura de
// fresagem que o FBM_ALL_FEATURES_MACHINED já reconhece sozinho
// internamente (POCKET_RECTANGULAR_STRAIGHT, SLOT_PARTIAL_RECTANGULAR,
// SURFACE_PLANAR*, WEDM_RECTANGULAR_STRAIGHT...). Se essa FASE 0a
// chamasse os journals originais (que agrupam TUDO) e depois o FBM
// rodasse sua própria fase de reconhecimento (que agrupa as MESMAS
// figuras de fresagem de novo), cada pocket/slot/superfície acabaria
// GRUPADO DUAS VEZES (uma vez por cada passada de reconhecimento) e o FBM
// acabaria usinando cada figura duas vezes também - o mesmíssimo
// problema de duplicidade que a decisão do FG_STEP2HOLE acima evita, só
// que pro lado da fresagem. Por isso esta FASE 0a é uma versão ENXUTA,
// escrita direto aqui, que só pede pro NX reconhecer/agrupar os 4 tipos
// de furo (SetFeatureTypes com só esses 4 nomes) - nunca toca nos tipos
// de fresagem, que ficam 100% por conta da fase de reconhecimento interna
// do FBM_ALL_FEATURES_MACHINED. Também corrige, de propósito, o mesmo bug
// que o FBM já corrigiu no seu próprio comentário: usa
// FeaturesToGroupTypes.SpecifyFeatures (não .All), pra garantir que só os
// 4 tipos pedidos sejam agrupados, nada além disso.
//
// GERADO COMO CLASSE SEPARADA (pedido do usuário) - não altera nenhum dos
// arquivos originais (RECOGNIZE_FEATURES.cs, CREATE_GROUP_FEATURES.cs,
// FBM_ALL_FEATURES_MACHINED.cs, AUTODRILL_GEOMETRIA_PROPRIA.cs seguem
// funcionando exatamente como antes, standalone, se você continuar
// usando os botões separados). Esta classe é só um NOVO ponto de entrada
// que orquestra o ciclo completo. Falta: criar o arquivo numa pasta do
// projeto (ex.: FBM_AUTODRILL_COMPLETE_CYCLE\) e adicionar o
// <Compile Include=".../FBM_AUTODRILL_COMPLETE_CYCLE.cs" /> no
// nxteste2.csproj (mesmo passo que faltou pro AUTODRILL_GEOMETRIA_PROPRIA
// no começo desta conversa) - sem isso o Visual Studio não compila o
// arquivo. Depois, um botão/comando de chat chamando
// FBM_AUTODRILL_COMPLETE_CYCLE.Run(null) já roda o ciclo inteiro.
// ══════════════════════════════════════════════════════════════════════
public class FBM_AUTODRILL_COMPLETE_CYCLE
{
    private const double MATCH_TOLERANCE = 0.15; // mm - tolerância pra casar diâmetro medido com uma tabela (rosca/soquete)
    private const double DRILL_FEED_PER_REV = 0.1; // mm/rot - furação/contra-furo (broca HSS/CBORE)
    private const double TAP_CUTTING_SPEED_VC = 8.0; // m/min - rosqueamento (macho) - físico diferente de furação, fica de fora da tabela de material
    private const double MIN_ENGAGEMENT_FACTOR = 1.5; // profundidade do macho = 1.5x o nominal
    private const string COUNTERSINK_TOOL_NAME = "COUNTERSINK"; // confirmado na sua biblioteca real
    private const string CENTER_DRILL_TOOL_NAME = "CENTER_DRILL"; // confirmado na sua biblioteca real - usado pra furo normal/contra-furo/rosca/soquete
    // Pastas únicas (mesmo padrão do AUTODRILL_GEOMETRIA_PROPRIA original):
    // TODOS os furos passantes/cegos (furo normal + piloto de rosca) numa
    // pasta só, e TODOS os contra-furos (soquete allen + genérico, só a
    // partir de FG_STEP1POCKET aqui) em outra pasta só.
    private const string DRILL_GROUPS_FOLDER = "DRILL_GROUPS";
    private const string COUNTERBORE_FOLDER = "COUNTERBORE_GROUP";

    // ══════════════════ MEDIÇÃO GEOMÉTRICA DE DEGRAUS (portado de AUTODRILL_GEOMETRIA_PROPRIA.cs / FG_PECK_DRILL_ALL_HOLES.cs) ══════════════════
    // Mesmo motivo do arquivo original: um contra-furo (soquete ou
    // genérico) é fisicamente um furo de DOIS diâmetros - a boca larga
    // (COUNTERBORING) e um furo mais estreito por baixo, que essa medição
    // detecta e fura à parte. Só se aplica aqui pra FG_STEP1POCKET (o
    // FG_STEP2HOLE, que também podia cair nessa medição no arquivo
    // original, agora é 100% do FBM - ver decisão no cabeçalho).
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
    private class SubCounterboreStep
    {
        public double Diameter;
        public double Depth;
        public bool IsThrough;
        public List<CAMFeature> Feats = new List<CAMFeature>();
    }
    private const double THROUGH_HOLE_BREAKOUT_ALLOWANCE = 5.0;
    private const double THROUGH_HOLE_TOLERANCE = 0.5;

    // ══════════════════ MATERIAL (RPM/feed) ══════════════════
    // ANTES: este arquivo tinha seu PRÓPRIO diálogo de material aqui
    // (MaterialInfo/MaterialTable/AskMaterial, só com DrillVc), separado do
    // diálogo do FBM_ALL_FEATURES_MACHINED (que também pergunta Vc de
    // endmill/cutter) - o usuário via os DOIS diálogos, um atrás do outro,
    // no mesmo ciclo combinado.
    // CORRIGIDO: removido o diálogo próprio. Agora o material é perguntado
    // UMA VEZ SÓ, em Run() (logo no início, antes da FASE 0a), reaproveitando
    // a tabela e o diálogo do próprio FBM_ALL_FEATURES_MACHINED via o novo
    // método público FBM_ALL_FEATURES_MACHINED.AskMaterialShared(W) - e o
    // MESMO material escolhido é aplicado às duas fases: passado direto pra
    // FASE 0b via FBM_ALL_FEATURES_MACHINED.Run(null, material) (que agora
    // aceita um material pré-selecionado e pula seu próprio diálogo quando
    // recebe um), e usado na FASE 1+ (furação) através de material.DrillVc,
    // igual antes. O tipo usado em todo este arquivo passa a ser
    // FBM_ALL_FEATURES_MACHINED.MaterialCuttingData (que já tem Name e
    // DrillVc, os dois campos que este arquivo usava) em vez do antigo
    // MaterialInfo local, que foi removido.

    // ══════════════════ TABELA DE ROSCA ══════════════════
    private class ThreadInfo
    {
        public double DrillDiameter;
        public string Name;
        public double NominalDiam;
        public double Pitch;
        public string TapToolName;
    }

    // ── Renomeada de "BuildThreadTable()" pra "BuildDefaultThreadTable()" -
    // agora é só o FALLBACK usado quando o banco (ToolDatabase.cs / SQL
    // Server) não estiver acessível. Ver BuildThreadTable(log) logo abaixo -
    // mesma tabela "Threads" do banco usada por AUTODRILL_GEOMETRIA_PROPRIA.cs
    // (fonte única, não duplicada por arquivo). ──
    private static List<ThreadInfo> BuildDefaultThreadTable()
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

    private static List<ThreadInfo> BuildThreadTable(Action<string> log)
    {
        try
        {
            List<ToolDatabase.ThreadRow> rows = ToolDatabase.LoadThreads(log);
            if (rows != null && rows.Count > 0)
            {
                List<ThreadInfo> lista = new List<ThreadInfo>();
                foreach (ToolDatabase.ThreadRow r in rows)
                    lista.Add(new ThreadInfo { DrillDiameter = r.DrillDiameter, Name = r.SizeLabel, NominalDiam = r.NominalDiam, Pitch = r.Pitch, TapToolName = r.TapToolName });
                return lista;
            }
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: falha lendo tabela de rosca do banco (" + ex.Message + ") - usando tabela fixa interna.");
        }
        return BuildDefaultThreadTable();
    }

    private static void AddThread(List<ThreadInfo> list, double drillDiam, double nominal, double pitch, string sizeLabel)
    {
        string pitchStr = pitch.ToString("0.0##", CultureInfo.InvariantCulture);
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
            if (diff <= MATCH_TOLERANCE && diff < bestDiff) { bestDiff = diff; best = t; }
        }
        return best;
    }

    // ══════════════════ TABELA DE SOQUETE ALLEN ══════════════════
    private class SoqueteRef
    {
        public double HeadDiameter;
        public string SizeLabel;
        public string GroupName;
    }

    // ── Renomeada de "BuildSoqueteTable()" pra "BuildDefaultSoqueteTable()" -
    // agora é só o FALLBACK usado quando o banco não estiver acessível. ──
    private static List<SoqueteRef> BuildDefaultSoqueteTable()
    {
        List<SoqueteRef> list = new List<SoqueteRef>();
        AddSoquete(list, 8.0, "M4");
        AddSoquete(list, 10.0, "M5");
        AddSoquete(list, 11.0, "M6");
        AddSoquete(list, 14.0, "M8");
        AddSoquete(list, 18.0, "M10");
        AddSoquete(list, 20.0, "M12");
        AddSoquete(list, 26.0, "M16");
        AddSoquete(list, 33.0, "M20");
        AddSoquete(list, 40.0, "M24");
        AddSoquete(list, 50.0, "M30");
        return list;
    }

    private static List<SoqueteRef> BuildSoqueteTable(Action<string> log)
    {
        try
        {
            List<ToolDatabase.SocketRow> rows = ToolDatabase.LoadSockets(log);
            if (rows != null && rows.Count > 0)
            {
                List<SoqueteRef> lista = new List<SoqueteRef>();
                foreach (ToolDatabase.SocketRow r in rows)
                    lista.Add(new SoqueteRef { HeadDiameter = r.HeadDiameter, SizeLabel = r.SizeLabel, GroupName = r.GroupName });
                return lista;
            }
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: falha lendo tabela de soquete do banco (" + ex.Message + ") - usando tabela fixa interna.");
        }
        return BuildDefaultSoqueteTable();
    }

    private static void AddSoquete(List<SoqueteRef> list, double headDiam, string sizeLabel)
    {
        list.Add(new SoqueteRef { HeadDiameter = headDiam, SizeLabel = sizeLabel, GroupName = sizeLabel + "_SOCKET_HEAD" });
    }

    private static SoqueteRef FindClosestSoquete(List<SoqueteRef> table, double diameter)
    {
        SoqueteRef best = null;
        double bestDiff = double.MaxValue;
        foreach (SoqueteRef s in table)
        {
            double diff = Math.Abs(s.HeadDiameter - diameter);
            if (diff <= MATCH_TOLERANCE && diff < bestDiff) { bestDiff = diff; best = s; }
        }
        return best;
    }

    // ══════════════════ CATÁLOGO DE FERRAMENTAS POR DIÂMETRO ══════════════════
    private class DrillToolEntry { public double Diameter; public Tool ToolRef; }

    private static List<DrillToolEntry> BuildHssDrillCatalog(Part workPart)
    {
        List<DrillToolEntry> list = new List<DrillToolEntry>();
        const string prefix = "HSS_DRILL_D";
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            Tool t = obj as Tool;
            if (t == null || !t.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            double diam;
            if (double.TryParse(t.Name.Substring(prefix.Length), NumberStyles.Any, CultureInfo.InvariantCulture, out diam))
                list.Add(new DrillToolEntry { Diameter = diam, ToolRef = t });
        }
        return list;
    }

    private static List<DrillToolEntry> BuildCboreCatalog(Part workPart)
    {
        List<DrillToolEntry> list = new List<DrillToolEntry>();
        const string prefix = "CBORE_";
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            Tool t = obj as Tool;
            if (t == null || !t.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string rest = t.Name.Substring(prefix.Length);
            if (rest.EndsWith("MM", StringComparison.OrdinalIgnoreCase))
                rest = rest.Substring(0, rest.Length - 2);
            double diam;
            if (double.TryParse(rest, NumberStyles.Any, CultureInfo.InvariantCulture, out diam))
                list.Add(new DrillToolEntry { Diameter = diam, ToolRef = t });
        }
        return list;
    }

    private static Tool FindNearestTool(List<DrillToolEntry> catalog, double target, out double actualDiameter, out double delta)
    {
        Tool best = null;
        double bestDiff = double.MaxValue;
        actualDiameter = 0.0;
        foreach (DrillToolEntry e in catalog)
        {
            double diff = Math.Abs(e.Diameter - target);
            if (diff < bestDiff) { bestDiff = diff; best = e.ToolRef; actualDiameter = e.Diameter; }
        }
        delta = bestDiff;
        return best;
    }

    // Reaproveita o TIPO de resumo já definido em AUTODRILL_GEOMETRIA_PROPRIA
    // (public, mesma pasta/projeto) em vez de duplicar a classe - assim, se
    // você quiser reaproveitar o popup AutoDrillSummaryForm pra esse ciclo
    // combinado também, ele já é compatível sem nenhuma alteração.
    public static AUTODRILL_GEOMETRIA_PROPRIA.AutoDrillSummary LastRunSummary { get; private set; }

    private static int _operacoesCriadas;

    /// <summary>
    /// Ponto de entrada do ciclo completo: reconhece/agrupa os 4 tipos de
    /// furo, roda o FBM inteiro (fresagem + coluna de molde + counterbore
    /// comum), e por último a furação/rosca/contra-furo (soquete e
    /// genérico só a partir de FG_STEP1POCKET - FG_STEP2HOLE já foi
    /// resolvido pelo FBM, não é reprocessado aqui).
    /// FBM_AUTODRILL_COMPLETE_CYCLE.Run(null);
    /// </summary>
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        if (workPart == null)
            return;
        NXOpen.UF.UFSession ufs = NXOpen.UF.UFSession.GetUFSession();

        List<string> log = new List<string>();
        Action<string> W = s => log.Add(s);
        _operacoesCriadas = 0;

        // ══════════════════ MATERIAL: pergunta ÚNICA, aplicada ao FBM (fresagem) E ao AutoDrill (furação) ══════════════════
        // ANTES: dois diálogos de material no mesmo ciclo (um aqui, outro
        // dentro do FBM_ALL_FEATURES_MACHINED.Run()). CORRIGIDO: pergunta
        // uma vez só, aqui, ANTES de qualquer fase rodar, reaproveitando a
        // tabela/diálogo do próprio FBM (que já cobre Vc de endmill/cutter
        // E de broca - DrillVc). O mesmo objeto 'material' é passado direto
        // pra FASE 0b (FBM) logo abaixo, e reaproveitado na FASE 1+
        // (furação) via material.DrillVc.
        W("=== MATERIAL: single prompt, shared by FBM (milling) and AutoDrill (drilling) ===");
        FBM_ALL_FEATURES_MACHINED.MaterialCuttingData material = FBM_ALL_FEATURES_MACHINED.AskMaterialShared(W);
        W("Material selected: " + material.Name + " (drill Vc = " + material.DrillVc.ToString("0.#", CultureInfo.InvariantCulture) + " m/min) - used in FBM's milling/mold-column/counterbore AND in all drilling/counterboring/tapping operations below.");

        // ══════════════════ FASE 0a: RECONHECIMENTO + AGRUPAMENTO - SÓ OS 4 TIPOS DE FURO ══════════════════
        // Ver cabeçalho do arquivo pra explicação completa do motivo de ser
        // uma versão enxuta em vez de chamar RECOGNIZE_FEATURES.cs /
        // CREATE_GROUP_FEATURES.cs originais (que também reconhecem tipos
        // de fresagem, duplicando o que o FBM já faz sozinho).
        W("=== PHASE 0a: RECOGNITION + GROUPING (hole types only: STEP1POCKET, STEP1HOLE, STEP2HOLE, STEP1POCKET_THREAD) ===");
        try
        {
            string[] holeFeatureTypes = { "STEP1POCKET", "STEP1HOLE", "STEP2HOLE", "STEP1POCKET_THREAD" };

            NXOpen.CAM.CAMObject nullCamObject = null;
            NXOpen.CAM.FeatureRecognitionBuilder frb = workPart.CAMSetup.CreateFeatureRecognitionBuilder(nullCamObject);
            NXOpen.CAM.ManualFeatureBuilder mfb = frb.CreateManualFeatureBuilder();
            frb.AssignColor = false;
            frb.AddCadFeatureAttributes = false;
            frb.MapFeatures = false;

            NXOpen.Point3d origemRec = new NXOpen.Point3d(0.0, 0.0, 0.0);
            NXOpen.Vector3d direcaoRec = new NXOpen.Vector3d(0.0, 0.0, 1.0);
            NXOpen.Direction eixoAcessoRec = workPart.Directions.CreateDirection(
                origemRec, direcaoRec, NXOpen.SmartObject.UpdateOption.AfterModeling);

            frb.RecognitionType = NXOpen.CAM.FeatureRecognitionBuilder.RecognitionEnum.Parametric;
            // Mesma correção já validada em RECOGNIZE_FEATURES.cs: ignora o
            // tipo herdado da modelagem, reclassifica pela geometria atual.
            frb.UseFeatureNameAsType = false;
            frb.IgnoreWarnings = false;
            frb.SetMachiningAccessDirection(new NXOpen.Direction[] { eixoAcessoRec }, 9.9999999999999995e-07);
            frb.SetFeatureTypes(holeFeatureTypes);
            frb.GeometrySearchType = NXOpen.CAM.FeatureRecognitionBuilder.GeometrySearch.Workpiece;

            NXOpen.CAM.CAMFeature[] foundFeatures = frb.FindFeatures();
            frb.Commit();
            frb.Destroy();
            mfb.Destroy();

            NXOpen.CAM.GroupFeatures gf = workPart.CAMSetup.CAMGroupCollection.CreateGroupFeatures();
            gf.GeometryLocation = "WORKPIECE";
            // SpecifyFeatures (não .All) - só agrupa os 4 tipos de furo
            // pedidos, nada de fresagem entra aqui.
            gf.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.SpecifyFeatures;
            gf.SetFeatureTypes(holeFeatureTypes);
            gf.SetMachiningAccessDirections(new NXOpen.Direction[] { eixoAcessoRec }, 9.9999999999999995e-07);
            gf.CreateFeatureGroups();
            gf.Commit();
            gf.Destroy();

            W("Hole recognition/grouping done (" + (foundFeatures != null ? foundFeatures.Length : 0) + " feature(s) found across the 4 hole types).");
        }
        catch (Exception exRec)
        {
            W("ERROR in hole recognition/grouping (phase 0a): " + exRec.Message);
            W("WARNING: without FG_STEP* groups, neither FBM's mold-column/counterbore phases nor the drilling phase below will find anything to process.");
        }

        // ══════════════════ FASE 0b: FBM COMPLETO (fresagem + coluna de molde + counterbore comum) ══════════════════
        W("");
        W("=== PHASE 0b: FBM_ALL_FEATURES_MACHINED (milling + mold column + generic counterbore - owns ALL FG_STEP2HOLE) ===");
        try
        {
            FBM_ALL_FEATURES_MACHINED.Run(null, material);
            W("FBM_ALL_FEATURES_MACHINED finished (used the material selected above - its own material dialog was skipped). See its own log lines above/below (it writes directly to this same Listing Window).");
        }
        catch (Exception exFbm)
        {
            W("ERROR running FBM_ALL_FEATURES_MACHINED: " + exFbm.Message);
            W("WARNING: milling and FG_STEP2HOLE (mold column / generic counterbore) were NOT processed. The drilling phase below still runs (thread/normal hole/FG_STEP1POCKET), but FG_STEP2HOLE stays untouched this run.");
        }

        // ══════════════════ FASE 1+: FURAÇÃO (rosca, soquete/genérico via FG_STEP1POCKET, furo normal) ══════════════════
        W("");
        W("=== PHASE 1+: DRILLING (thread, socket/generic counterbore from FG_STEP1POCKET only, normal hole) ===");
        try
        {
            // Material já foi perguntado UMA VEZ, no início de Run() (ver
            // bloco "MATERIAL" acima) - reaproveitado aqui, sem novo diálogo.

            List<ThreadInfo> threadTable = BuildThreadTable(W);
            List<SoqueteRef> soqueteTable = BuildSoqueteTable(W);
            List<DrillToolEntry> hssCatalog = BuildHssDrillCatalog(workPart);
            List<DrillToolEntry> cboreCatalog = BuildCboreCatalog(workPart);

            NXOpen.CAM.NCGroup ncProgram = (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");
            NXOpen.CAM.NCGroup nCGroupNone = (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE");
            NXOpen.CAM.Method drillMethod = (NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD");
            NXOpen.CAM.FeatureGeometry workpieceGeom = (NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE");

            Tool counterSinkTool = FindToolByName(workPart, COUNTERSINK_TOOL_NAME);
            Tool centerDrillToolBlanket = FindToolByName(workPart, CENTER_DRILL_TOOL_NAME);
            if (counterSinkTool == null)
                W("WARNING: tool '" + COUNTERSINK_TOOL_NAME + "' not found - no thread will be countersunk/tapped.");
            if (centerDrillToolBlanket == null)
                W("WARNING: tool '" + CENTER_DRILL_TOOL_NAME + "' not found - normal hole/counterbore/thread/socket will have no center drill (goes straight to drilling).");

            Dictionary<string, bool> usedNames = BuildUsedNamesCache(workPart);

            Dictionary<string, List<CAMFeature>> roscaBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> soqueteBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> cboreBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> normalBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> outrosBuckets = new Dictionary<string, List<CAMFeature>>();

            Dictionary<string, SubCounterboreStep> subCborePassanteBuckets = new Dictionary<string, SubCounterboreStep>();
            int subCboreMedicaoFalhou = 0;

            int totalGrupos = 0, totalFeatures = 0, ignorados = 0;
            int step2HoleSkipped = 0, step2HoleFeaturesSkipped = 0;
            Dictionary<string, int> roscaNaoReconhecida = new Dictionary<string, int>();
            Dictionary<string, int> semDiametro = new Dictionary<string, int>();

            List<NXOpen.CAM.FeatureGeometryGroup> stepGroups = GetAllStepGroups(workPart);
            W("FG_STEP* groups found (starting point, not used directly in the operations): " + stepGroups.Count);

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in stepGroups)
            {
                CAMFeature[] feats = fg.GetFeatures();
                if (feats == null || feats.Length == 0)
                    continue;

                // ── FG_STEP2HOLE é 100% do FBM neste ciclo combinado (ver
                // decisão no cabeçalho do arquivo) - pulado aqui antes de
                // contar/classificar, pra não duplicar a operação que o
                // FBM (FASE 0b, acima) já criou pra esse mesmo furo. ──
                if (fg.Name.StartsWith("FG_STEP2HOLE", StringComparison.OrdinalIgnoreCase))
                {
                    step2HoleSkipped++;
                    step2HoleFeaturesSkipped += feats.Length;
                    continue;
                }

                totalGrupos++;
                totalFeatures += feats.Length;

                double diameter1;
                string erroDiam;
                bool temDiametro = TryGetAttributeDouble(fg, "DIAMETER_1", out diameter1, out erroDiam);
                if (!temDiametro)
                {
                    ignorados += feats.Length;
                    string key = fg.Name;
                    if (!semDiametro.ContainsKey(key)) semDiametro[key] = 0;
                    semDiametro[key] += feats.Length;
                    continue;
                }

                bool isThread = fg.Name.IndexOf("THREAD", StringComparison.OrdinalIgnoreCase) >= 0;
                // SEM FG_STEP2HOLE aqui (única mudança em relação ao
                // AUTODRILL_GEOMETRIA_PROPRIA original) - só FG_STEP1POCKET
                // conta como "formato counterbore" neste ciclo combinado.
                bool counterboreShaped = !isThread &&
                    fg.Name.StartsWith("FG_STEP1POCKET", StringComparison.OrdinalIgnoreCase);
                bool plainHole = !isThread && !counterboreShaped
                    && fg.Name.StartsWith("FG_STEP1HOLE", StringComparison.OrdinalIgnoreCase);

                if (isThread)
                {
                    ThreadInfo t = FindClosestThread(threadTable, diameter1);
                    if (t == null)
                    {
                        ignorados += feats.Length;
                        string diamKey = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                        if (!roscaNaoReconhecida.ContainsKey(diamKey)) roscaNaoReconhecida[diamKey] = 0;
                        roscaNaoReconhecida[diamKey] += feats.Length;
                        continue;
                    }
                    AddToBucket(roscaBuckets, t.Name, feats);
                }
                else if (counterboreShaped)
                {
                    List<HoleStep> medidos;
                    string erroMedicao;
                    bool medicaoOk = TryMeasureHoleSteps(fg, ufs, out medidos, out erroMedicao);
                    if (medicaoOk && medidos.Count >= 2)
                    {
                        HoleStep estreito = medidos[medidos.Count - 1];
                        string chave = (estreito.IsThrough ? "THRU_" : "BLIND_")
                            + "D" + estreito.Diameter.ToString("0.##", CultureInfo.InvariantCulture)
                            + "_P" + estreito.Depth.ToString("0.#", CultureInfo.InvariantCulture);
                        SubCounterboreStep bucket;
                        if (!subCborePassanteBuckets.TryGetValue(chave, out bucket))
                        {
                            bucket = new SubCounterboreStep { Diameter = estreito.Diameter, Depth = estreito.Depth, IsThrough = estreito.IsThrough };
                            subCborePassanteBuckets[chave] = bucket;
                        }
                        bucket.Feats.AddRange(feats);
                    }
                    else if (!medicaoOk)
                    {
                        subCboreMedicaoFalhou += feats.Length;
                        W("WARNING: " + fg.Name + " -> couldn't measure the hole steps (" + erroMedicao + ") - only the wide mouth (counterbore) will be machined, check manually whether there's a narrow hole underneath.");
                    }

                    SoqueteRef s = FindClosestSoquete(soqueteTable, diameter1);
                    if (s != null)
                    {
                        AddToBucket(soqueteBuckets, s.SizeLabel, feats);
                    }
                    else
                    {
                        string diamKey = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                        AddToBucket(cboreBuckets, diamKey, feats);
                    }
                }
                else if (plainHole)
                {
                    string diamKey = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                    AddToBucket(normalBuckets, diamKey, feats);
                }
                else
                {
                    string diamKey = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                    AddToBucket(outrosBuckets, diamKey, feats);
                }
            }

            W("FG_STEP2HOLE skipped (owned by FBM in this combined cycle): " + step2HoleSkipped + " group(s) / " + step2HoleFeaturesSkipped + " feature(s).");
            W("Groups scanned: " + totalGrupos + " | CAMFeatures scanned: " + totalFeatures + " | Ignored: " + ignorados);
            W("Sizes: THREAD=" + roscaBuckets.Count + " SOCKET=" + soqueteBuckets.Count + " GENERIC COUNTERBORE=" + cboreBuckets.Count + " NORMAL=" + normalBuckets.Count + " UNCLASSIFIED=" + outrosBuckets.Count);

            if (roscaNaoReconhecida.Count > 0)
            {
                W("--- Thread diameters (name contains THREAD) that didn't match any table size: ---");
                foreach (KeyValuePair<string, int> kv in roscaNaoReconhecida)
                    W("   D" + kv.Key + "mm -> " + kv.Value + " hole(s)");
            }
            if (semDiametro.Count > 0)
                W("WARNING: " + semDiametro.Count + " group(s) without a readable DIAMETER_1 - ignored.");
            if (subCboreMedicaoFalhou > 0)
                W("WARNING: " + subCboreMedicaoFalhou + " counterbore hole(s) without a successful step measurement - check whether they need a narrow hole underneath, done by hand.");
            W("Narrow hole under counterbore (through/blind): " + subCborePassanteBuckets.Count + " diameter/depth combination(s) found.");

            // ══════════════════ CENTRO: UMA passada só, TODOS os furos que usam a broca CENTER_DRILL ══════════════════
            if (centerDrillToolBlanket != null)
            {
                try
                {
                    List<CAMFeature> todosParaCentro = new List<CAMFeature>();
                    foreach (List<CAMFeature> l in roscaBuckets.Values) todosParaCentro.AddRange(l);
                    foreach (List<CAMFeature> l in soqueteBuckets.Values) todosParaCentro.AddRange(l);
                    foreach (List<CAMFeature> l in cboreBuckets.Values) todosParaCentro.AddRange(l);
                    foreach (List<CAMFeature> l in normalBuckets.Values) todosParaCentro.AddRange(l);
                    foreach (List<CAMFeature> l in outrosBuckets.Values) todosParaCentro.AddRange(l);

                    if (todosParaCentro.Count > 0)
                    {
                        NXOpen.CAM.FeatureGeometry geomCentro = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, "GEOM_CENTER_DRILL_ALL", todosParaCentro, W);
                        if (geomCentro != null)
                        {
                            NXOpen.CAM.NCGroup folderCentro = FindOrCreateProgramFolder(workPart, ncProgram, "CENTER_DRILLS");
                            CreateCenterDrillOperation(workPart, folderCentro, nCGroupNone, centerDrillToolBlanket, geomCentro, "CENTER_DRILL_ALL", W);
                            W("CENTER: " + todosParaCentro.Count + " hole(s) -> 1 operation created in 'CENTER_DRILLS'.");
                        }
                    }
                }
                catch (Exception exCentro)
                {
                    W("ERROR in the unified center-drill pass (holes remain without a center drill, but the rest of the script continues): " + exCentro.Message);
                }
            }

            // ══════════════════ FASE 1: FURAÇÕES POR DIÂMETRO (piloto de rosca + furo normal + não classificado) ══════════════════
            Dictionary<string, NXOpen.CAM.FeatureGeometry> roscaGeoms = new Dictionary<string, NXOpen.CAM.FeatureGeometry>();
            int roscaPilotoOk = 0;
            foreach (ThreadInfo t in threadTable)
            {
                List<CAMFeature> feats;
                if (!roscaBuckets.TryGetValue(t.Name, out feats))
                    continue;
                if (counterSinkTool == null)
                    continue;

                try
                {
                    Tool tapTool = FindToolByName(workPart, t.TapToolName);
                    if (tapTool == null)
                    {
                        W("SKIPPED (thread " + t.Name + "): tap tool '" + t.TapToolName + "' not found - " + feats.Count + " hole(s) without an operation (pilot, countersink and tap skipped).");
                        continue;
                    }

                    string pitchStr = t.Pitch.ToString("0.0##", CultureInfo.InvariantCulture);
                    string geomName = "TAP" + t.Name + "X" + pitchStr;

                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, geomName, feats, W);
                    if (geom == null)
                        continue;

                    roscaGeoms[t.Name] = geom;

                    double actualDiam; double delta;
                    Tool pilotDrillTool = FindNearestTool(hssCatalog, t.DrillDiameter, out actualDiam, out delta);
                    if (pilotDrillTool != null)
                    {
                        if (delta > 0.3)
                            W("WARNING: nearest drill for the " + t.Name + " pilot (" + t.DrillDiameter + "mm) is " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm off (using D" + actualDiam + "mm).");
                        NXOpen.CAM.NCGroup diamFolder = FindOrCreateProgramFolder(workPart, ncProgram, DRILL_GROUPS_FOLDER);
                        CreateGenericDrillOperation(workPart, diamFolder, drillMethod, pilotDrillTool, geom, actualDiam, material.DrillVc, "DRILL_" + geomName, null, W);
                    }
                    else
                    {
                        W("WARNING: no HSS drill available for the " + t.Name + " pilot (" + t.DrillDiameter + "mm) - pilot drilling skipped, continuing to countersink/tap.");
                    }

                    roscaPilotoOk++;
                    W("THREAD " + t.Name + " (pilot): " + feats.Count + " hole(s) -> geometry '" + geomName + "' created, pilot drilled in the diameter folder (countersink/tap are done in the thread finishing phase, at the end, in their own folder).");
                }
                catch (Exception exRoscaPiloto)
                {
                    W("ERROR processing thread pilot " + t.Name + " (continuing to the next size): " + exRoscaPiloto.Message);
                }
            }

            // --- FURO NORMAL ---
            int normalOk = 0;
            foreach (KeyValuePair<string, List<CAMFeature>> kv in normalBuckets)
            {
                string diamLabel = kv.Key;
                List<CAMFeature> feats = kv.Value;

                try
                {
                    double diamValue;
                    double.TryParse(diamLabel, NumberStyles.Any, CultureInfo.InvariantCulture, out diamValue);

                    string geomName = "GEOM_HOLE_D" + diamLabel + "MM";

                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, geomName, feats, W);
                    if (geom == null)
                        continue;

                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, DRILL_GROUPS_FOLDER);

                    double actualDiam; double delta;
                    Tool drillTool = FindNearestTool(hssCatalog, diamValue, out actualDiam, out delta);
                    if (drillTool == null)
                    {
                        W("SKIPPED (normal hole D" + diamLabel + "mm): no HSS drill available in the library - " + feats.Count + " hole(s) not drilled.");
                        continue;
                    }
                    if (delta > 0.3)
                        W("WARNING: normal hole of " + diamLabel + "mm using nearest available drill (D" + actualDiam + "mm, difference " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm).");

                    CreateGenericDrillOperation(workPart, folder, drillMethod, drillTool, geom, actualDiam, material.DrillVc, "DRILL_" + geomName, null, W);

                    normalOk++;
                    W("NORMAL HOLE D" + diamLabel + "mm: " + feats.Count + " hole(s) -> geometry '" + geomName + "' + drilling created.");
                }
                catch (Exception exNormal)
                {
                    W("ERROR processing normal hole D" + diamLabel + "mm (continuing to the next diameter): " + exNormal.Message);
                }
            }

            // --- FURO ESTREITO SOB CONTRA-FURO (FG_STEP1POCKET) ---
            int subCboreOk = 0;
            foreach (KeyValuePair<string, SubCounterboreStep> kv in subCborePassanteBuckets)
            {
                string chave = kv.Key;
                SubCounterboreStep bucket = kv.Value;

                try
                {
                    string geomName = "GEOM_SUBCBORE_" + chave;

                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, geomName, bucket.Feats, W);
                    if (geom == null)
                        continue;

                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, DRILL_GROUPS_FOLDER);

                    double actualDiam; double delta;
                    Tool drillTool = FindNearestTool(hssCatalog, bucket.Diameter, out actualDiam, out delta);
                    if (drillTool == null)
                    {
                        W("SKIPPED (hole under counterbore " + chave + "): no HSS drill available in the library - " + bucket.Feats.Count + " hole(s) missing the narrow part.");
                        continue;
                    }
                    if (delta > 0.3)
                        W("WARNING: hole under counterbore " + chave + " using nearest available drill (D" + actualDiam + "mm, difference " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm).");

                    CreateGenericDrillOperation(workPart, folder, drillMethod, drillTool, geom, actualDiam, material.DrillVc, "DRILL_" + geomName, bucket.Depth, W);

                    subCboreOk++;
                    W((bucket.IsThrough ? "THROUGH HOLE" : "BLIND HOLE") + " under counterbore " + chave + ": " + bucket.Feats.Count + " hole(s) -> geometry '" + geomName + "' + drilling (measured depth: " + bucket.Depth.ToString("0.##", CultureInfo.InvariantCulture) + "mm) created.");
                }
                catch (Exception exSubCbore)
                {
                    W("ERROR processing hole under counterbore " + chave + " (continuing to the next): " + exSubCbore.Message);
                }
            }

            // --- NÃO CLASSIFICADO ---
            int outrosOk = 0;
            foreach (KeyValuePair<string, List<CAMFeature>> kv in outrosBuckets)
            {
                string diamLabel = kv.Key;
                List<CAMFeature> feats = kv.Value;

                try
                {
                    double diamValue;
                    double.TryParse(diamLabel, NumberStyles.Any, CultureInfo.InvariantCulture, out diamValue);

                    string geomName = "GEOM_OUTROS_D" + diamLabel + "MM";
                    string folderName = "FUROS_NAO_CLASSIFICADOS";

                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, geomName, feats, W);
                    if (geom == null)
                        continue;

                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, folderName);

                    double actualDiam; double delta;
                    Tool drillTool = FindNearestTool(hssCatalog, diamValue, out actualDiam, out delta);
                    if (drillTool == null)
                    {
                        W("SKIPPED (unclassified D" + diamLabel + "mm): no HSS drill available in the library - " + feats.Count + " hole(s) not drilled.");
                        continue;
                    }
                    if (delta > 0.3)
                        W("WARNING: unclassified hole of " + diamLabel + "mm using nearest available drill (D" + actualDiam + "mm, difference " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm).");

                    CreateGenericDrillOperation(workPart, folder, drillMethod, drillTool, geom, actualDiam, material.DrillVc, "DRILL_" + geomName, null, W);

                    outrosOk++;
                    W("UNCLASSIFIED D" + diamLabel + "mm: " + feats.Count + " hole(s) -> geometry '" + geomName + "' + drilling created in 'FUROS_NAO_CLASSIFICADOS' (treated as a normal hole - verify this is correct).");
                }
                catch (Exception exOutros)
                {
                    W("ERROR processing unclassified hole D" + diamLabel + "mm (continuing to the next diameter): " + exOutros.Message);
                }
            }

            // ══════════════════ FASE 2: COUNTERBORES a partir de FG_STEP1POCKET (soquete allen + contra-furo genérico) ══════════════════
            int soqueteOk = 0;
            foreach (SoqueteRef s in soqueteTable)
            {
                List<CAMFeature> feats;
                if (!soqueteBuckets.TryGetValue(s.SizeLabel, out feats))
                    continue;

                try
                {
                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, s.GroupName, feats, W);
                    if (geom == null)
                        continue;

                    string folderName = COUNTERBORE_FOLDER;
                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, folderName);

                    double actualDiam; double delta;
                    Tool cboreTool = FindNearestTool(cboreCatalog, s.HeadDiameter, out actualDiam, out delta);
                    if (cboreTool == null)
                    {
                        W("SKIPPED (socket " + s.SizeLabel + "): no CBORE tool available in the library - " + feats.Count + " hole(s) without a counterbore.");
                        continue;
                    }
                    if (delta > 0.3)
                        W("WARNING: socket " + s.SizeLabel + " (head D" + s.HeadDiameter.ToString("0.###", CultureInfo.InvariantCulture) + "mm) using nearest available CBORE tool (D" + actualDiam + "mm, difference " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm).");

                    string diamLabelForOp = actualDiam.ToString("0.###", CultureInfo.InvariantCulture);
                    CreateCounterboreOperation(workPart, folder, nCGroupNone, cboreTool, geom, diamLabelForOp, actualDiam, material.DrillVc, W);

                    soqueteOk++;
                    W("SOCKET " + s.SizeLabel + ": " + feats.Count + " hole(s) -> geometry '" + s.GroupName + "' + counterbore created in '" + folderName + "'.");
                }
                catch (Exception exSoquete)
                {
                    W("ERROR processing socket " + s.SizeLabel + " (continuing to the next size): " + exSoquete.Message);
                }
            }

            int cboreOk = 0;
            foreach (KeyValuePair<string, List<CAMFeature>> kv in cboreBuckets)
            {
                string diamLabel = kv.Key;
                List<CAMFeature> feats = kv.Value;

                try
                {
                    double diamValue;
                    double.TryParse(diamLabel, NumberStyles.Any, CultureInfo.InvariantCulture, out diamValue);

                    string geomName = "GEOM_CBORE_D" + diamLabel + "MM";
                    string folderName = COUNTERBORE_FOLDER;

                    NXOpen.CAM.FeatureGeometry geom = EnsureMergedGeometry(workPart, workpieceGeom, usedNames, geomName, feats, W);
                    if (geom == null)
                        continue;

                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, folderName);

                    double actualDiam; double delta;
                    Tool cboreTool = FindNearestTool(cboreCatalog, diamValue, out actualDiam, out delta);
                    if (cboreTool == null)
                    {
                        W("SKIPPED (counterbore D" + diamLabel + "mm): no CBORE tool available in the library - " + feats.Count + " hole(s) without a counterbore.");
                        continue;
                    }
                    if (delta > 0.3)
                        W("WARNING: counterbore of " + diamLabel + "mm using nearest available tool (D" + actualDiam + "mm, difference " + delta.ToString("0.##", CultureInfo.InvariantCulture) + "mm).");

                    CreateCounterboreOperation(workPart, folder, nCGroupNone, cboreTool, geom, diamLabel, actualDiam, material.DrillVc, W);

                    cboreOk++;
                    W("COUNTERBORE D" + diamLabel + "mm: " + feats.Count + " hole(s) -> geometry '" + geomName + "' + counterbore created.");
                }
                catch (Exception exCbore)
                {
                    W("ERROR processing counterbore D" + diamLabel + "mm (continuing to the next diameter): " + exCbore.Message);
                }
            }

            // ══════════════════ FASE 3: ROSCAS - acabamento (escareador + macho, por último) ══════════════════
            int roscaOk = 0;
            foreach (ThreadInfo t in threadTable)
            {
                NXOpen.CAM.FeatureGeometry geom;
                if (!roscaGeoms.TryGetValue(t.Name, out geom))
                    continue;

                try
                {
                    Tool tapTool = FindToolByName(workPart, t.TapToolName);
                    if (tapTool == null)
                        continue;

                    string pitchStr = t.Pitch.ToString("0.0##", CultureInfo.InvariantCulture);
                    string folderName = "TAP" + t.Name + "X" + pitchStr + "_PROG";
                    NXOpen.CAM.NCGroup folder = FindOrCreateProgramFolder(workPart, ncProgram, folderName);

                    double depth = t.NominalDiam * MIN_ENGAGEMENT_FACTOR;
                    CreateCountersinkOperation(workPart, folder, drillMethod, counterSinkTool, geom, t, material.DrillVc, W);
                    CreateTappingOperation(workPart, folder, drillMethod, tapTool, geom, t, depth, W);

                    roscaOk++;
                    List<CAMFeature> feats;
                    int qtd = roscaBuckets.TryGetValue(t.Name, out feats) ? feats.Count : 0;
                    W("THREAD " + t.Name + " (finish): " + qtd + " hole(s) -> countersink + tap created in '" + folderName + "'.");
                }
                catch (Exception exRosca)
                {
                    W("ERROR processing thread finishing " + t.Name + " (continuing to the next size): " + exRosca.Message);
                }
            }

            W("DRILLING TOTAL: " + roscaPilotoOk + " thread pilot size(s), " + roscaOk + " thread finishing size(s) (countersink+tap), " + soqueteOk + " socket size(s), " + cboreOk + " generic counterbore diameter(s), " + normalOk + " normal hole diameter(s), " + subCboreOk + " narrow hole(s) under counterbore, " + outrosOk + " unclassified diameter(s) processed. (FG_STEP2HOLE handled separately by FBM, phase 0b above.)");

            int furosRosca = 0;
            foreach (List<CAMFeature> l in roscaBuckets.Values) furosRosca += l.Count;
            int furosSoquete = 0;
            foreach (List<CAMFeature> l in soqueteBuckets.Values) furosSoquete += l.Count;
            int furosCboreGenerico = 0;
            foreach (List<CAMFeature> l in cboreBuckets.Values) furosCboreGenerico += l.Count;
            int furosNormais = 0;
            foreach (List<CAMFeature> l in normalBuckets.Values) furosNormais += l.Count;
            int furosNaoClassificados = 0;
            foreach (List<CAMFeature> l in outrosBuckets.Values) furosNaoClassificados += l.Count;
            int furosEstreitosSobContraFuro = 0;
            foreach (SubCounterboreStep sc in subCborePassanteBuckets.Values) furosEstreitosSobContraFuro += sc.Feats.Count;

            LastRunSummary = new AUTODRILL_GEOMETRIA_PROPRIA.AutoDrillSummary
            {
                MaterialUsado = material.Name,
                GruposVarridos = totalGrupos,
                FeaturesVarridas = totalFeatures,
                Ignorados = ignorados,
                FurosRosca = furosRosca,
                FurosSoquete = furosSoquete,
                FurosContraFuroGenerico = furosCboreGenerico,
                FurosNormais = furosNormais,
                FurosNaoClassificados = furosNaoClassificados,
                FurosEstreitosSobContraFuro = furosEstreitosSobContraFuro,
                TamanhosRosca = roscaBuckets.Count,
                TamanhosSoquete = soqueteBuckets.Count,
                TamanhosContraFuroGenerico = cboreBuckets.Count,
                TamanhosNormais = normalBuckets.Count,
                TamanhosNaoClassificados = outrosBuckets.Count,
                OperacoesCriadas = _operacoesCriadas,
                GruposSemDiametro = semDiametro.Count,
                MedicaoDeStepsFalhou = subCboreMedicaoFalhou,
            };
        }
        catch (Exception ex)
        {
            W("ERROR (drilling phase):");
            W(ex.ToString());
        }
        finally
        {
            theSession.ListingWindow.Open();
            foreach (string line in log)
                theSession.ListingWindow.WriteLine(line);
        }
    }

    private static void AddToBucket(Dictionary<string, List<CAMFeature>> buckets, string key, CAMFeature[] feats)
    {
        List<CAMFeature> lista;
        if (!buckets.TryGetValue(key, out lista))
        {
            lista = new List<CAMFeature>();
            buckets[key] = lista;
        }
        foreach (CAMFeature f in feats)
            lista.Add(f);
    }

    private static List<NXOpen.CAM.FeatureGeometryGroup> GetAllStepGroups(NXOpen.Part workPart)
    {
        List<NXOpen.CAM.FeatureGeometryGroup> resultado = new List<NXOpen.CAM.FeatureGeometryGroup>();
        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.FeatureGeometryGroup fg = obj as NXOpen.CAM.FeatureGeometryGroup;
            if (fg != null && fg.Name.StartsWith("FG_STEP", StringComparison.OrdinalIgnoreCase))
                resultado.Add(fg);
        }
        return resultado;
    }

    private static NXOpen.CAM.FeatureGeometry EnsureMergedGeometry(Part workPart, NXOpen.CAM.FeatureGeometry workpieceGeom, Dictionary<string, bool> usedNames, string geomName, List<CAMFeature> feats, Action<string> W)
    {
        string realName = geomName;
        if (usedNames.ContainsKey(realName))
        {
            int counter = 2;
            while (usedNames.ContainsKey(geomName + "_" + counter))
                counter++;
            realName = geomName + "_" + counter;
            W("WARNING: geometry '" + geomName + "' already existed (previous run) - creating '" + realName + "' with the holes from THIS run, so no new hole is left out.");
        }

        NCGroup novoGrupo = workPart.CAMSetup.CAMGroupCollection.CreateGeometryWithUserName(
            workpieceGeom, "hole_making", "HOLE_BOSS_GEOM",
            NCGroupCollection.UseDefaultName.False, realName, "Hole Boss Geom");
        NXOpen.CAM.FeatureGeometry novoGrupoGeom = (NXOpen.CAM.FeatureGeometry)novoGrupo;
        HoleBossGeometry holeBossGeometry = workPart.CAMSetup.CAMGroupCollection.CreateHoleBossGeometryBuilder(novoGrupoGeom);

        CAMFeature nullFeature = null;
        foreach (CAMFeature feat in feats)
        {
            FeatureSet featureSet = holeBossGeometry.FeatureGeometry.AddFeatureSet(nullFeature, "NXHOLE");
            NXObject[] entities = new NXObject[1];
            entities[0] = feat;
            featureSet.CreateFeature(entities);
        }

        holeBossGeometry.Commit();
        holeBossGeometry.Destroy();
        usedNames[realName] = true;
        return novoGrupoGeom;
    }

    private static void CreateCenterDrillOperation(Part workPart, NCGroup folder, NCGroup nCGroupNone, Tool centerTool, NXOpen.CAM.FeatureGeometry geom, string opNameBase, Action<string> W)
    {
        string operationName = MakeUniqueOperationName(workPart, opNameBase);
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, nCGroupNone, centerTool, geom, "hole_making", "SPOT_DRILLING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        builder.PredefinedDepth.Status = true;
        builder.PredefinedDepth.Value = 2.0;
        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1500.0;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 10.0;
        NXObject committed = builder.Commit();
        CAMObject[] objs = new CAMObject[1];
        objs[0] = (HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objs);
        builder.Destroy();
        _operacoesCriadas++;
    }

    private static void CreateGenericDrillOperation(Part workPart, NCGroup folder, Method drillMethod, Tool tool, NXOpen.CAM.FeatureGeometry geom, double diameter, double vc, string opNameBase, double? depthOverride, Action<string> W)
    {
        string operationName = MakeUniqueOperationName(workPart, opNameBase);
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, drillMethod, tool, geom, "hole_making", "DRILLING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        if (depthOverride.HasValue)
        {
            builder.PredefinedDepth.Status = true;
            builder.PredefinedDepth.Value = depthOverride.Value;
        }
        double rpm = (vc * 318.0) / diameter;
        double feed = rpm * DRILL_FEED_PER_REV;
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 30.0;
        NXObject committed = builder.Commit();
        CAMObject[] objs = new CAMObject[1];
        objs[0] = (HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objs);
        builder.Destroy();
        _operacoesCriadas++;
    }

    private static void CreateCounterboreOperation(Part workPart, NCGroup folder, NCGroup nCGroupNone, Tool tool, NXOpen.CAM.FeatureGeometry geom, string diameterLabel, double diameter, double vc, Action<string> W)
    {
        string operationName = MakeUniqueOperationName(workPart, "CBORE_D" + diameterLabel);
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, nCGroupNone, tool, geom, "hole_making", "COUNTERBORING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        double rpm = (vc * 318.0) / diameter;
        double feed = rpm * DRILL_FEED_PER_REV;
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;
        builder.CuttingParameters.TopOffset.Distance = 2.0;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 30.0;
        NXObject committed = builder.Commit();
        CAMObject[] objs = new CAMObject[1];
        objs[0] = (HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objs);
        builder.Destroy();
        _operacoesCriadas++;
    }

    private static void CreateCountersinkOperation(Part workPart, NCGroup folder, Method method, Tool tool, NXOpen.CAM.FeatureGeometry geom, ThreadInfo t, double vc, Action<string> W)
    {
        string pitchStr = t.Pitch.ToString("0.0##", CultureInfo.InvariantCulture);
        string operationName = MakeUniqueOperationName(workPart, "CSINK_" + t.Name + "X" + pitchStr);
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, method, tool, geom, "hole_making", "COUNTERSINKING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        builder.CycleTable.AxialStepover.StepoverType = StepoverBuilder.StepoverTypes.None;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegions.Type = NcmTransfer.TransferTypes.LowestSafeZ;
        double rpm = (vc * 318.0) / t.NominalDiam;
        double feed = rpm * DRILL_FEED_PER_REV;
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;
        NXObject committed = builder.Commit();
        CAMObject[] objs = new CAMObject[1];
        objs[0] = (HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objs);
        builder.Destroy();
        _operacoesCriadas++;
    }

    private static void CreateTappingOperation(Part workPart, NCGroup folder, Method method, Tool tool, NXOpen.CAM.FeatureGeometry geom, ThreadInfo t, double depth, Action<string> W)
    {
        string pitchStr = t.Pitch.ToString("0.0##", CultureInfo.InvariantCulture);
        string operationName = MakeUniqueOperationName(workPart, "TAP_" + t.Name + "X" + pitchStr + "_" + depth.ToString("0.###", CultureInfo.InvariantCulture) + "MM");
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, method, tool, geom, "hole_making", "TAPPING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        builder.CycleTable.AxialStepover.StepoverType = StepoverBuilder.StepoverTypes.None;
        builder.PredefinedDepth.Status = true;
        builder.PredefinedDepth.Value = depth;
        double rpm = (TAP_CUTTING_SPEED_VC * 318.0) / t.NominalDiam;
        double feed = rpm * t.Pitch;
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegions.Type = NcmTransfer.TransferTypes.LowestSafeZ;
        builder.CollisionCheck = false;
        builder.GougeChecking = false;
        builder.NonCuttingBuilder.CollisionCheck = false;
        NXObject committed = builder.Commit();
        CAMObject[] objs = new CAMObject[1];
        objs[0] = (HoleDrilling)committed;
        workPart.CAMSetup.GenerateToolPath(objs);
        builder.Destroy();
        _operacoesCriadas++;
    }

    private static bool TryMeasureHoleSteps(
        NXOpen.CAM.FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs,
        out List<HoleStep> steps, out string erroDetalhe)
    {
        steps = new List<HoleStep>();
        erroDetalhe = "";
        const double TOLERANCE = 0.01;
        CAMFeature[] camFeatures;
        try { camFeatures = fg.GetFeatures(); }
        catch (Exception ex) { erroDetalhe = "GetFeatures() failed: " + ex.Message; return false; }
        if (camFeatures == null || camFeatures.Length == 0)
        {
            erroDetalhe = "no CAMFeature";
            return false;
        }
        NXOpen.Face[] faces;
        try { faces = camFeatures[0].GetFaces(); }
        catch (Exception ex) { erroDetalhe = "GetFaces() failed: " + ex.Message; return false; }
        if (faces == null || faces.Length == 0)
        {
            erroDetalhe = "GetFaces() returned empty";
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
                erroDetalhe = "AskFaceData failed: " + ex.Message;
                return false;
            }
        }
        if (rawFaces.Count == 0)
        {
            erroDetalhe = "no cylindrical face found (likely a conical hole - needs a different strategy)";
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
            // Se não conseguir medir o corpo, segue sem diagnosticar passante/cego.
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

    private static bool TryGetAttributeDouble(NXOpen.CAM.FeatureGeometryGroup fg, string attrName, out double value, out string erro)
    {
        value = 0.0;
        erro = "";
        try
        {
            CAMFeature[] camFeatures = fg.GetFeatures();
            if (camFeatures == null || camFeatures.Length == 0)
            {
                erro = "no CAMFeature";
                return false;
            }
            CAMAttributeCollection attrs = camFeatures[0].Attributes;
            CAMAttribute attr;
            try { attr = attrs.FindObject(attrName); }
            catch (Exception ex) { erro = attrName + " does not exist: " + ex.Message; return false; }
            if (attr == null)
            {
                erro = attrName + " returned null";
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
                erro = "GetDoubleValue not found via reflection";
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

    private static NXOpen.CAM.Tool FindToolByName(Part workPart, string toolName)
    {
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            Tool t = obj as Tool;
            if (t != null && string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private static Dictionary<string, bool> BuildUsedNamesCache(Part workPart)
    {
        Dictionary<string, bool> names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
            names[obj.Name] = true;
        return names;
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
            parentGroup, "hole_making", "PROGRAM", NCGroupCollection.UseDefaultName.False, folderName, "Program");

        ProgramOrderGroupBuilder programOrderGroupBuilder = workPart.CAMSetup.CAMGroupCollection.CreateProgramOrderGroupBuilder(novaPasta);
        programOrderGroupBuilder.Commit();
        programOrderGroupBuilder.Destroy();

        return novaPasta;
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
