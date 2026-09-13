using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using NXOpen;
using NXOpen.CAM;
using NXOpen.CAM.FBM;

// ══════════════════════════════════════════════════════════════════════
// AUTODRILL INDEPENDENTE - versão que usa GEOMETRIA PRÓPRIA em vez de
// depender das feature groups nativas do NX (FG_STEP*) pra criar as
// operações.
//
// POR QUE: hoje o pipeline (RECOGNIZE_FEATURES -> CREATE_GROUP_FEATURES
// -> FG_CENTER_DRILL_ALL_HOLES -> FG_PECK_DRILL_ALL_HOLES ->
// AUTOMATIC_COUNTERBORE -> ALL_THREADS_COUNTERSINK) cria as operações
// direto em cima dos grupos FG_STEP* que o NX gera automaticamente. O
// NOME desses grupos (FG_STEP1POCKET_THREAD_3, por exemplo) é só um
// artefato interno, incremental, sem significado nenhum pra gente - e
// isso já causou dois bugs seguidos hoje tentando adivinhar o tipo
// interno de feature (CAMFeature.Name) que o NX usa.
//
// Este arquivo faz diferente: ainda usa o FG_STEP* como PONTO DE PARTIDA
// (é a única forma de descobrir quais furos existem, sem reinventar o
// reconhecimento de feature do NX), mas na hora de criar as operações,
// em vez de usar o grupo FG_STEP* direto, CRIA uma geometria própria
// (HoleBossGeometry) com nome que você escolhe e entende (TAPM14X2.0,
// GEOM_HOLE_D8MM, M16_SOCKET_HEAD, GEOM_CBORE_D18MM) - exatamente como
// já fazíamos em AUTOMATIC_HOLE_BOSS_GEOM.cs / AUTOMATIC_TAP_AND_SOCKET.cs
// pra soquete, agora estendido pra TODOS os tipos de furo.
//
// GANHO EXTRA: como cada furo vira uma geometria própria por TAMANHO
// (não por grupo original do NX), furos do mesmo diâmetro/tipo que
// estavam espalhados em vários FG_STEP diferentes agora viram UMA SÓ
// geometria mesclada -> menos operações, mais fácil de auditar no
// Operation Navigator (você vê "TAPM14X2.0", não "FG_STEP1POCKET_THREAD_3").
//
// ESCOPO: cobre os 4 tipos de furo que sua peça tem, com prioridade de
// classificação (cada furo cai em só uma categoria):
//   1) ROSCA        - nome do grupo tem "THREAD" -> centro + furação +
//                      escareador (COUNTERSINK) + macho (TAP_<size>).
//   2) SOQUETE ALLEN - formato "Counter Bore" (STEP2HOLE/STEP1POCKET) cujo
//                      diâmetro bate com a tabela de cabeça de soquete
//                      (M4 a M30) -> centro + contra-furo simples, mesma
//                      fórmula do item 3 abaixo (ferramenta CBORE_xxMM mais
//                      próxima do diâmetro da cabeça). Trocado da sequência
//                      antiga de 4 operações (broca+fresa+chanfro) - mais
//                      simples e não depende mais de tabela de referência
//                      por tamanho.
//   3) CONTRA-FURO   - mesmo formato "Counter Bore", mas SEM bater com
//      GENÉRICO         soquete -> centro + contra-furo genérico (mesma
//                      fórmula do AUTOMATIC_COUNTERBORE.cs, ferramenta
//                      CBORE_xxMM mais próxima disponível).
//   4) FURO NORMAL   - formato "STEP1HOLE" (furo comum, sem degrau) ->
//                      centro + furação com a broca HSS mais próxima
//                      disponível na biblioteca.
//
// TABELA DE MATERIAL: agora com diálogo interativo (AskMaterial), igual ao
// AskMaterial() do seu AUTOFBM (FBM_ALL_FEATURES_MACHINED.cs) - o usuário
// escolhe o material logo no início da execução, e o Vc escolhido é usado
// como fonte ÚNICA pra TODAS as operações de furação/contra-furo/escareador
// (antes cada arquivo tinha seu próprio Vc fixo e havia inconsistência entre
// eles). Se o diálogo for cancelado ou falhar, cai pro material padrão
// (DefaultMaterial, "1045") com aviso no log.
//
// ORDEM DE EXECUÇÃO DAS OPERAÇÕES E DAS PASTAS (pedido explícito): 1) furos
// de centro (pasta única "CENTER_DRILLS"), 2) furos passantes/cegos (pasta
// única "DRILL_GROUPS" - furo normal E o piloto da rosca, todos juntos aqui;
// não classificado fica numa pasta separada, "FUROS_NAO_CLASSIFICADOS"),
// 3) counterbores (pasta única "COUNTERBORE_GROUP" - soquete allen +
// contra-furo genérico, todos juntos), 4) por último as roscas (uma pasta
// POR TAMANHO, "TAP<size>_PROG" - a exceção: tap continua com várias
// pastas, uma por tamanho, não uma só). A pasta TAP só é CRIADA na fase 4 -
// o piloto da rosca (fase 2) usa a pasta DRILL_GROUPS, não a pasta de tap,
// exatamente pra pasta de tap não aparecer antes das outras no Program
// Order. A
// geometria de cada rosca (não a pasta) é criada na fase 2 (piloto) e
// reaproveitada na fase 4 (acabamento) - sem duplicar furo.
// ══════════════════════════════════════════════════════════════════════
public class AUTODRILL_GEOMETRIA_PROPRIA
{
    // ── Limpa o cache em memória de materiais (_materialTableCache abaixo).
    // Mesmo motivo/uso de FBM_ALL_FEATURES_MACHINED.InvalidateDatabaseCaches() -
    // chamado pelo botão "Save All Changes" da tela de gerenciamento pra
    // que uma edição de material já valha no próximo Run(), sem precisar
    // reiniciar o PATHNC AUTOMATION. (Threads/Sockets não têm cache aqui -
    // já são recarregados do banco a cada Run(), não precisam disso.) ──
    public static void InvalidateDatabaseCaches()
    {
        _materialTableCache = null;
    }

    private const double MATCH_TOLERANCE = 0.15; // mm - tolerância pra casar diâmetro medido com uma tabela (rosca/soquete)
    private const double DRILL_FEED_PER_REV = 0.1; // mm/rot - furação/contra-furo (broca HSS/CBORE)
    private const double TAP_CUTTING_SPEED_VC = 8.0; // m/min - rosqueamento (macho) - físico diferente de furação, fica de fora da tabela de material
    private const double MIN_ENGAGEMENT_FACTOR = 1.5; // profundidade do macho = 1.5x o nominal
    private const string COUNTERSINK_TOOL_NAME = "COUNTERSINK"; // confirmado na sua biblioteca real
    private const string CENTER_DRILL_TOOL_NAME = "CENTER_DRILL"; // confirmado na sua biblioteca real - usado pra furo normal/contra-furo/rosca/soquete
    // Pastas únicas (pedido do usuário): TODOS os furos passantes/cegos
    // (furo normal + não classificado + piloto de rosca) numa pasta só, e
    // TODOS os contra-furos (soquete allen + genérico) em outra pasta só.
    // Centro já era uma pasta só ("CENTER_DRILLS"). Tap continua uma pasta
    // POR TAMANHO (TAP<size>X<pitch>_PROG) - é a exceção pedida.
    private const string DRILL_GROUPS_FOLDER = "DRILL_GROUPS";
    private const string COUNTERBORE_FOLDER = "COUNTERBORE_GROUP";

    // ══════════════════ MEDIÇÃO GEOMÉTRICA DE DEGRAUS (portado de FG_PECK_DRILL_ALL_HOLES.cs) ══════════════════
    // Motivo: um contra-furo (soquete ou genérico) é fisicamente um furo de
    // DOIS diâmetros - a boca larga/rasa (o degrau que a operação de
    // COUNTERBORING já faz) e um furo mais estreito por baixo (passante ou
    // cego) que segue até o fundo de verdade. O DIAMETER_1 do grupo só
    // descreve o diâmetro largo - o script criava a operação de contra-furo
    // e parava aí, sem nunca furar o furo estreito por baixo. Esse bloco
    // mede a geometria REAL (todas as faces cilíndricas do furo, com
    // diâmetro e profundidade de cada uma) pra descobrir se existe um
    // segundo diâmetro, e se ele é passante ou cego (comparando o fundo do
    // furo com a face oposta do sólido).
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
    // Um "balde" de furo estreito medido sob um contra-furo - agrupa CAMFeature
    // de vários grupos FG_STEP diferentes que deram o MESMO diâmetro estreito,
    // MESMA profundidade (arredondada) e MESMO passante/cego, pra virar UMA
    // operação de furação só, igual ao resto do arquivo já faz por tamanho.
    private class SubCounterboreStep
    {
        public double Diameter;
        public double Depth;
        public bool IsThrough;
        public List<CAMFeature> Feats = new List<CAMFeature>();
    }
    // Folga extra além da face de saída, aplicada só em furos PASSANTES (pra
    // broca quebrar limpo do outro lado).
    private const double THROUGH_HOLE_BREAKOUT_ALLOWANCE = 5.0;
    // Tolerância pra considerar que o fundo do furo "encosta" na superfície
    // oposta do corpo sólido (ou seja, é passante).
    private const double THROUGH_HOLE_TOLERANCE = 0.5;

    // ══════════════════ MATERIAL (RPM/feed) ══════════════════
    private class MaterialInfo
    {
        public string Name;
        public double DrillVc; // m/min, brocas HSS
    }
    // ── Renomeada de "MaterialTable" pra "DefaultMaterialTable" - agora é
    // só o FALLBACK usado quando o banco (ToolDatabase.cs / SQL Server) não
    // estiver acessível. Ver GetMaterialTable() logo abaixo. NOTA: esta
    // lista tinha H13 (não existe na tabela de materiais do FBM/banco -
    // FBM_ALL_FEATURES_MACHINED.cs tem Bronze no lugar) e nomes em inglês -
    // o banco usa a MESMA fonte de materiais do FBM (Materials, uma única
    // vez, sem duplicar), então quando o banco estiver configurado, a lista
    // aqui passa a ter os 7 materiais do FBM (sem H13). Se ainda quiser
    // H13 disponível pra furação, adicione pela tela "Manage Tool /
    // Material Database" - até lá, sem banco configurado, nada muda (H13
    // continua disponível via este fallback). ──
    private static readonly List<MaterialInfo> DefaultMaterialTable = new List<MaterialInfo>
    {
        new MaterialInfo { Name = "1020 (low carbon steel)",         DrillVc = 25 },
        new MaterialInfo { Name = "1045 (medium carbon steel)",      DrillVc = 20 },
        new MaterialInfo { Name = "P20 (mold steel, pre-hardened)",  DrillVc = 15 },
        new MaterialInfo { Name = "H13 (tool steel, hardened)",      DrillVc = 10 },
        new MaterialInfo { Name = "Aluminum",                        DrillVc = 80 },
        new MaterialInfo { Name = "Copper",                          DrillVc = 30 },
        new MaterialInfo { Name = "Nylon/Polymer",                   DrillVc = 60 },
    };

    private static List<MaterialInfo> _materialTableCache;

    private static List<MaterialInfo> GetMaterialTable(Action<string> log)
    {
        if (_materialTableCache != null)
            return _materialTableCache;

        try
        {
            List<ToolDatabase.MaterialRow> rows = ToolDatabase.LoadMaterials(log);
            if (rows != null && rows.Count > 0)
            {
                List<MaterialInfo> lista = new List<MaterialInfo>();
                foreach (ToolDatabase.MaterialRow r in rows)
                    lista.Add(new MaterialInfo { Name = r.Name, DrillVc = r.DrillVc });
                _materialTableCache = lista;
                return _materialTableCache;
            }
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: falha lendo materiais do banco (" + ex.Message + ") - usando tabela fixa interna.");
        }

        _materialTableCache = DefaultMaterialTable;
        return _materialTableCache;
    }

    // ══════════════════ DIÁLOGO DE MATERIAL (portado do AskMaterial() de FBM_ALL_FEATURES_MACHINED.cs - "AUTOFBM") ══════════════════
    // Mesmo padrão visual/comportamental do AUTOFBM: ComboBox travado em lista
    // (DropDownStyle = DropDownList) com os materiais da MaterialTable acima,
    // botões OK/Cancelar, painel de cabeçalho escuro. Aqui a tabela só tem
    // DrillVc (não precisamos de EndmillRoughVc/EndmillFinishVc/CutterVc -
    // esse arquivo só faz furação/contra-furo/rosca, não fresa). Se o usuário
    // cancelar ou o diálogo falhar por qualquer motivo (ex.: sessão sem
    // interface gráfica), cai pro material padrão com aviso no log - o script
    // NUNCA trava esperando o diálogo.
    private static MaterialInfo AskMaterial(Action<string> W)
    {
        List<MaterialInfo> materialTable = GetMaterialTable(W);
        MaterialInfo defaultMaterial = materialTable.Count > 1 ? materialTable[1] : materialTable[0];
        try
        {
            using (Form form = new Form())
            {
                Color headerColor = Color.FromArgb(24, 45, 74);
                Color bodyColor = Color.FromArgb(240, 240, 240);
                Color accentColor = Color.FromArgb(0, 120, 215);
                Color textColor = Color.White;

                form.Text = "AUTODRILL - Material Selection";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(380, 190);
                form.BackColor = bodyColor;

                Panel header = new Panel();
                header.Dock = DockStyle.Top;
                header.Height = 50;
                header.BackColor = headerColor;
                Label headerLabel = new Label();
                headerLabel.Text = "Select the part material";
                headerLabel.ForeColor = textColor;
                headerLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                headerLabel.AutoSize = false;
                headerLabel.Dock = DockStyle.Fill;
                headerLabel.TextAlign = ContentAlignment.MiddleLeft;
                headerLabel.Padding = new Padding(12, 0, 0, 0);
                header.Controls.Add(headerLabel);

                Label subLabel = new Label();
                subLabel.Text = "Sets the cutting speed (Vc) used in all drilling, counterboring and tapping operations:";
                subLabel.ForeColor = Color.FromArgb(60, 60, 60);
                subLabel.Font = new Font("Segoe UI", 8);
                subLabel.AutoSize = false;
                subLabel.Location = new System.Drawing.Point(20, 58);
                subLabel.Size = new Size(340, 30);

                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Location = new System.Drawing.Point(20, 92);
                combo.Width = 340;
                combo.Font = new Font("Segoe UI", 10);
                foreach (MaterialInfo m in materialTable)
                    combo.Items.Add(m.Name);
                combo.SelectedIndex = materialTable.IndexOf(defaultMaterial); // "1045" (ou equivalente do banco) pré-selecionado

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.BackColor = accentColor;
                okButton.ForeColor = textColor;
                okButton.FlatStyle = FlatStyle.Flat;
                okButton.Location = new System.Drawing.Point(184, 140);
                okButton.Width = 80;

                Button cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new System.Drawing.Point(280, 140);
                cancelButton.Width = 80;

                form.Controls.Add(subLabel);
                form.Controls.Add(combo);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.Controls.Add(header);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                DialogResult result = form.ShowDialog();
                if (result == DialogResult.OK && combo.SelectedIndex >= 0)
                {
                    MaterialInfo escolhido = materialTable[combo.SelectedIndex];
                    W("Material selected by the user: " + escolhido.Name);
                    return escolhido;
                }

                W("WARNING: material selection cancelled - using default material: " + defaultMaterial.Name);
                return defaultMaterial;
            }
        }
        catch (Exception ex)
        {
            W("WARNING: material dialog failed (" + ex.Message + ") - using default material: " + defaultMaterial.Name);
            return defaultMaterial;
        }
    }

    // ══════════════════ TABELA DE ROSCA (mesma de ALL_THREADS_COUNTERSINK.cs) ══════════════════
    private class ThreadInfo
    {
        public double DrillDiameter;
        public string Name;
        public double NominalDiam;
        public double Pitch;
        public string TapToolName;
    }

    // ── Renomeada de "BuildThreadTable()" pra "BuildDefaultThreadTable()" -
    // agora é só o FALLBACK usado quando o banco não estiver acessível. Ver
    // BuildThreadTable(log) logo abaixo (mesmo padrão de GetMaterialTable). ──
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
        string pitchStr = pitch.ToString("0.0##", CultureInfo.InvariantCulture); // sempre >= 1 casa decimal (bate com TAP_M16X2.0 real da biblioteca)
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

    // ══════════════════ TABELA DE SOQUETE ALLEN (mesma de AUTOMATIC_TAP_AND_SOCKET.cs) ══════════════════
    // Simplificado (pedido do usuário): antes cada tamanho precisava de uma
    // linha de referência completa (broca/fresa/chanfro/tool number/altura)
    // pra rodar a sequência de 4 operações (centro dedicado + furação +
    // fresamento circular do corpo + fresamento do chanfro). Agora o soquete
    // usa o MESMO padrão simples do AUTOMATIC_COUNTERBORE.cs (centro +
    // contra-furo com ferramenta CBORE_xxMM) - só precisa do diâmetro da
    // cabeça pra achar a ferramenta mais próxima. Isso também resolve o
    // problema de tamanhos "sem dados de referência" (M4/M5/M6/M8/M30) -
    // agora todos são usinados, desde que exista uma ferramenta CBORE
    // razoavelmente próxima na biblioteca.
    //
    // OBS: a tabela antiga tinha DUAS entradas "M8" (14.0mm e 15.0mm) com o
    // MESMO GroupName - ou seja, o mesmo bucket seria processado duas vezes
    // no loop (inofensivo antes, porque as duas eram "sem dados"; teria
    // virado operação duplicada agora que M8 passa a ser usinado de
    // verdade). Consolidei numa entrada só (14.0mm). Se o M8 real da sua
    // biblioteca de solda de cabeça mede 15mm em vez de 14mm, muda o valor
    // aqui - com tolerância de 0.15mm ele não bate perfeito nos dois.
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

    // ══════════════════ CATÁLOGO DE FERRAMENTAS POR DIÂMETRO (lido ao vivo da biblioteca) ══════════════════
    // Em vez de montar o nome exato esperado e falhar se não bater 100%
    // (o que já causou vários "ferramenta não encontrada" hoje - CBORE_11MM
    // não existe mas CBORE_11 existe), escaneia a biblioteca de verdade,
    // extrai o diâmetro de cada ferramenta pelo nome e pega a MAIS PRÓXIMA
    // do diâmetro que você precisa.
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

    // ══════════════════ RESUMO ILUSTRATIVO: contagens da última execução,
    // pra Form1/AutoDrillSummaryForm mostrarem um popup com furos e
    // operações processados (sem estimativa de tempo - contagem só). ══════════════════
    public class AutoDrillSummary
    {
        public string MaterialUsado;
        public int GruposVarridos;
        public int FeaturesVarridas;
        public int Ignorados;
        public int FurosRosca;
        public int FurosSoquete;
        public int FurosContraFuroGenerico;
        public int FurosNormais;
        public int FurosNaoClassificados;
        public int FurosEstreitosSobContraFuro;
        public int TamanhosRosca;
        public int TamanhosSoquete;
        public int TamanhosContraFuroGenerico;
        public int TamanhosNormais;
        public int TamanhosNaoClassificados;
        public int OperacoesCriadas;
        public int GruposSemDiametro;
        public int MedicaoDeStepsFalhou;

        public int TotalFurosProcessados
        {
            get
            {
                return FurosRosca + FurosSoquete + FurosContraFuroGenerico
                    + FurosNormais + FurosNaoClassificados;
            }
        }
    }

    // Contador estático de operações CAM criadas com sucesso nesta
    // execução - incrementado no fim de cada CreateXxxOperation (só quando
    // chega até lá sem exceção). Resetado no início de Run().
    private static int _operacoesCriadas;

    // Última execução, lida pelo Form1 depois de Run(null) retornar pra
    // montar o popup de resumo (AutoDrillSummaryForm). Null se ainda não
    // rodou nesta sessão do NX, ou se a execução caiu no catch antes de
    // chegar no fim.
    public static AutoDrillSummary LastRunSummary { get; private set; }

    /// <summary>
    /// Ponto de entrada pra chamar a partir da aplicacao:
    /// AUTODRILL_GEOMETRIA_PROPRIA.Run(null);
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

        try
        {
            MaterialInfo material = AskMaterial(W);
            W("Material selected: " + material.Name + " (drill Vc = " + material.DrillVc.ToString("0.#", CultureInfo.InvariantCulture) + " m/min) - used in all drilling/counterboring/tapping operations.");

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

            // ── buckets: cada categoria agrupa as CAMFeature por TAMANHO,
            // não por grupo FG_STEP original - furos do mesmo tamanho
            // espalhados em vários FG_STEP viram UMA geometria só. ──
            Dictionary<string, List<CAMFeature>> roscaBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> soqueteBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> cboreBuckets = new Dictionary<string, List<CAMFeature>>();
            Dictionary<string, List<CAMFeature>> normalBuckets = new Dictionary<string, List<CAMFeature>>();
            // furos com diâmetro legível mas cujo nome de grupo não bate com
            // NENHUM dos 3 prefixos conhecidos (FG_STEP2HOLE/FG_STEP1POCKET/
            // FG_STEP1HOLE) - antes eram só contados como "ignorados" e
            // ficavam sem NENHUMA operação. Agora ganham um tratamento
            // básico (centro + furação, igual furo normal) numa pasta
            // separada, pra nada ficar sem usinar silenciosamente. NÃO inclui
            // rosca sem tamanho batido nem soquete sem dados de referência -
            // esses continuam só logados, porque "furar normal" um furo que
            // deveria virar rosca ou soquete sairia com a geometria errada.
            Dictionary<string, List<CAMFeature>> outrosBuckets = new Dictionary<string, List<CAMFeature>>();

            // Furo estreito (passante ou cego) medido sob a boca larga de um
            // contra-furo (soquete ou genérico) - a operação de COUNTERBORING
            // sozinha só faz a boca larga/rasa; isso aqui garante que o resto
            // do furo, por baixo, também seja furado. Chave = passante/cego +
            // diâmetro + profundidade (arredondados), pra furos iguais de
            // grupos FG_STEP diferentes virarem UMA operação só.
            Dictionary<string, SubCounterboreStep> subCborePassanteBuckets = new Dictionary<string, SubCounterboreStep>();
            int subCboreMedicaoFalhou = 0;

            int totalGrupos = 0, totalFeatures = 0, ignorados = 0;
            Dictionary<string, int> roscaNaoReconhecida = new Dictionary<string, int>();
            Dictionary<string, int> semDiametro = new Dictionary<string, int>();

            List<NXOpen.CAM.FeatureGeometryGroup> stepGroups = GetAllStepGroups(workPart);
            W("FG_STEP* groups found (starting point, not used directly in the operations): " + stepGroups.Count);

            foreach (NXOpen.CAM.FeatureGeometryGroup fg in stepGroups)
            {
                CAMFeature[] feats = fg.GetFeatures();
                if (feats == null || feats.Length == 0)
                    continue;
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
                bool counterboreShaped = !isThread &&
                    (fg.Name.StartsWith("FG_STEP2HOLE", StringComparison.OrdinalIgnoreCase)
                     || fg.Name.StartsWith("FG_STEP1POCKET", StringComparison.OrdinalIgnoreCase));
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
                    // ── Furo estreito sob a boca do contra-furo: mede a
                    // geometria real do grupo (todas as faces cilíndricas) pra
                    // achar o segundo diâmetro (se existir) e furá-lo também -
                    // a operação de COUNTERBORING (mais abaixo) só cuida da
                    // boca larga/rasa. ──
                    List<HoleStep> medidos;
                    string erroMedicao;
                    bool medicaoOk = TryMeasureHoleSteps(fg, ufs, out medidos, out erroMedicao);
                    if (medicaoOk && medidos.Count >= 2)
                    {
                        // medidos já vem ordenado por diâmetro decrescente
                        // (TryMeasureHoleSteps.Sort) - o último é o mais estreito.
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
                    // medicaoOk && medidos.Count == 1 -> furo de diâmetro único
                    // só (a classificação por FG_STEP pegou errado ou é mesmo
                    // só a boca rasa sem furo por baixo) - nada extra a fazer.

                    SoqueteRef s = FindClosestSoquete(soqueteTable, diameter1);
                    if (s != null)
                    {
                        // Não precisa mais de "dados de referência" pra processar -
                        // o soquete agora usa o mesmo contra-furo simples (centro +
                        // ferramenta CBORE_xxMM) do contra-furo genérico, então
                        // qualquer tamanho de cabeça reconhecido já pode ser usinado.
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
            // Rosca, SOQUETE, contra-furo genérico, furo normal e não-classificado usam
            // a MESMA ferramenta de centro - em vez de uma operação de centro
            // por categoria (redundante, a mesma broca sendo "remontada"
            // várias vezes em pastas diferentes), agora é UMA operação só,
            // numa pasta só ("CENTER_DRILLS", mesmo nome que FG_CENTER_DRILL_
            // ALL_HOLES.cs já usava), cobrindo todos os furos de uma vez.
            // Soquete allen agora ENTRA aqui também - como o soquete deixou
            // de usar a sequência de 4 operações (que tinha seu próprio
            // centro com ferramenta dedicada "CENTERDRILL"), ele passa a usar
            // a mesma broca de centro "em branco" de todo mundo, igual ao
            // contra-furo genérico já fazia.
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

            // ══════════════════ ORDEM DE EXECUÇÃO E DE PASTAS (pedido explícito do usuário): ══════════════════
            // 1) centro, 2) furos passantes/cegos (uma pasta só, DRILL_GROUPS),
            // 3) counterbore (uma pasta só, COUNTERBORE_GROUP), 4) tap - por
            // último (uma pasta POR TAMANHO, essa é a exceção). Como o
            // piloto da rosca TAMBÉM é uma furação reta (broca HSS), ele sai
            // do bloco de rosca e entra na FASE 1, usando a MESMA pasta
            // DRILL_GROUPS que o furo normal usa - NÃO cria pasta de tap
            // aqui. Só a geometria da rosca é guardada (em roscaGeoms) pra
            // reaproveitar depois, na FASE 3 (acabamento) - a pasta
            // TAP<size>_PROG só é criada LÁ, garantindo que ela apareça por
            // último no Program Order (era exatamente esse o problema antes).

            // ══════════════════ FASE 1: FURAÇÕES POR DIÂMETRO (piloto de rosca + furo normal + não classificado) ══════════════════

            // --- ROSCA (piloto): geometria própria + furação com a broca HSS mais próxima ---
            // O piloto entra na MESMA pasta única (DRILL_GROUPS) que o furo
            // normal usa - NÃO cria pasta TAP nenhuma aqui. A pasta
            // TAP<size>X<pitch>_PROG só é criada na FASE 3 (acabamento, por
            // último), exatamente como pedido: centro -> furos -> counterbore -> tap.
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

            // --- FURO NORMAL: geometria própria + furação (centro já feito na passada unificada) ---
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

            // --- FURO ESTREITO SOB CONTRA-FURO: passante/cego medido por baixo da boca larga do counterbore (soquete + genérico) ---
            // Mesma pasta DRILL_GROUPS que furo normal/piloto (centro já foi
            // feito na passada unificada, junto com o resto do contra-furo).
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

                    // Profundidade MEDIDA na geometria real (já inclui a folga
                    // de quebra se for passante) - sobrescreve a profundidade
                    // "natural" da feature, que é da boca inteira, não só
                    // dessa fatia estreita.
                    CreateGenericDrillOperation(workPart, folder, drillMethod, drillTool, geom, actualDiam, material.DrillVc, "DRILL_" + geomName, bucket.Depth, W);

                    subCboreOk++;
                    W((bucket.IsThrough ? "THROUGH HOLE" : "BLIND HOLE") + " under counterbore " + chave + ": " + bucket.Feats.Count + " hole(s) -> geometry '" + geomName + "' + drilling (measured depth: " + bucket.Depth.ToString("0.##", CultureInfo.InvariantCulture) + "mm) created.");
                }
                catch (Exception exSubCbore)
                {
                    W("ERROR processing hole under counterbore " + chave + " (continuing to the next): " + exSubCbore.Message);
                }
            }

            // --- NÃO CLASSIFICADO: mesmo tratamento de furo normal, pasta separada (centro já feito na passada unificada) ---
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

            // ══════════════════ FASE 2: COUNTERBORES (soquete allen + contra-furo genérico) ══════════════════

            // --- SOQUETE ALLEN: geometria própria + contra-furo simples (centro já feito na passada unificada) ---
            // Trocado (pedido do usuário): em vez da sequência de 4 operações
            // (centro dedicado + furação com remoção de cavaco + fresamento
            // circular do corpo + fresamento do chanfro), usa exatamente o
            // padrão do AUTOMATIC_COUNTERBORE.cs - centro (já feito) + UMA
            // operação de COUNTERBORING com a ferramenta CBORE_xxMM mais
            // próxima do diâmetro da cabeça. Mais simples, mais robusto (não
            // depende mais de tool number/altura cadastrados à mão por
            // tamanho) e usina os tamanhos que antes ficavam sem operação.
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

            // ══════════════════ CONTRA-FURO GENÉRICO: geometria própria + contra-furo (centro já feito na passada unificada) ══════════════════
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
            // Reaproveita a geometria criada na FASE 1 (furação do piloto),
            // mas a pasta TAP<size>X<pitch>_PROG só é criada AGORA - é
            // exatamente essa a ordem pedida: centro -> furos por diâmetro ->
            // counterbore -> tap. Assim a pasta de tap só aparece por último
            // no Program Order, e não lá no início (que era o problema).
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
                        continue; // já foi logado na FASE 1 (piloto)

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

            W("TOTAL: " + roscaPilotoOk + " thread pilot size(s), " + roscaOk + " thread finishing size(s) (countersink+tap), " + soqueteOk + " socket size(s), " + cboreOk + " generic counterbore diameter(s), " + normalOk + " normal hole diameter(s), " + subCboreOk + " narrow hole(s) under counterbore, " + outrosOk + " unclassified diameter(s) processed.");

            // ── Monta o resumo pro popup ilustrativo (AutoDrillSummaryForm).
            // Contagem de FUROS (não de "tamanhos"): soma o Count de cada
            // lista de CAMFeature dentro dos buckets - é o número real de
            // furos, não quantos diâmetros/tamanhos distintos existem. ──
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

            LastRunSummary = new AutoDrillSummary
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
            W("ERROR:");
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

    // ══════════════════ GEOMETRIA PRÓPRIA MESCLADA (genérica pras 4 categorias) ══════════════════
    // Mesmo padrão validado em AUTOMATIC_HOLE_BOSS_GEOM.cs / AUTOMATIC_TAP_AND_SOCKET.cs:
    // 1 AddFeatureSet + 1 CreateFeature POR FURO, 1 Commit só no final.
    private static NXOpen.CAM.FeatureGeometry EnsureMergedGeometry(Part workPart, NXOpen.CAM.FeatureGeometry workpieceGeom, Dictionary<string, bool> usedNames, string geomName, List<CAMFeature> feats, Action<string> W)
    {
        // CORREÇÃO IMPORTANTE: antes, se já existia uma geometria com esse
        // nome (de uma execução anterior do script no mesmo teste), a
        // função só devolvia a geometria VELHA sem tocar nela - os
        // CAMFeature desta execução (que podem ser diferentes, se o
        // reconhecimento de feature/RECOGNIZE_FEATURES foi rodado de novo
        // entre um teste e outro) nunca eram adicionados a lugar nenhum.
        // A operação era criada normalmente (nome novo, sem erro no log),
        // mas gerando trajetória em cima do conjunto de furos CONGELADO da
        // primeira execução - por isso o CENTRO/contra-furo/etc pareciam
        // "não fazer" furos que na verdade só não estavam mais na geometria
        // usada. Agora SEMPRE cria uma geometria nova (com sufixo _2, _3...
        // se o nome já existir) com os furos ATUAIS - a antiga fica intacta
        // no Operation Navigator (sem apagar nada, apagar objeto de CAM por
        // script não está validado neste arquivo). Se for rodar isso várias
        // vezes de teste, limpe as geometrias/operações antigas manualmente
        // antes da rodada final de produção.
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

    // ══════════════════ OPERAÇÕES: CENTRO (blanket, furo normal/contra-furo/rosca) ══════════════════
    // Mesma fórmula validada em FG_CENTER_DRILL_ALL_HOLES.cs.
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

    // ══════════════════ OPERAÇÕES: FURAÇÃO genérica (furo normal / piloto de rosca / furo sob o counterbore) ══════════════════
    private static void CreateGenericDrillOperation(Part workPart, NCGroup folder, Method drillMethod, Tool tool, NXOpen.CAM.FeatureGeometry geom, double diameter, double vc, string opNameBase, double? depthOverride, Action<string> W)
    {
        string operationName = MakeUniqueOperationName(workPart, opNameBase);
        NXOpen.CAM.Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            folder, drillMethod, tool, geom, "hole_making", "DRILLING",
            OperationCollection.UseDefaultName.False, operationName, operationName);
        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);
        // depthOverride: usado só quando a profundidade foi MEDIDA na geometria
        // real (caso do furo sob o counterbore, que é só uma FATIA de uma
        // feature maior - o depth "natural" da feature inteira não serve).
        // Nos outros casos (furo normal / piloto de rosca) fica null e a
        // profundidade continua vindo do reconhecimento automático da feature,
        // como sempre foi.
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

    // ══════════════════ OPERAÇÕES: CONTRA-FURO genérico (fórmula de AUTOMATIC_COUNTERBORE.cs) ══════════════════
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

    // ══════════════════ OPERAÇÕES: ROSCA - escareador + macho (fórmula de ALL_THREADS_COUNTERSINK.cs) ══════════════════
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

    // ══════════════════ MEDIÇÃO DE DEGRAUS: mede diâmetro+profundidade de cada
    // face cilíndrica do furo (portado quase igual de FG_PECK_DRILL_ALL_HOLES.cs,
    // já validado lá) ══════════════════
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
            // Se não conseguir medir o corpo, segue sem diagnosticar passante/cego
            // (assume cego - mais seguro não adicionar folga sem certeza).
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

    // ══════════════════ HELPERS COMPARTILHADOS ══════════════════

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
