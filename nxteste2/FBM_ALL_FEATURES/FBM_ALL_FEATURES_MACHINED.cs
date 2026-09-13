using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using NXOpen;
using NXOpen.CAM;
using Operation = NXOpen.CAM.Operation;

public class FBM_ALL_FEATURES_MACHINED
{
    // ── Limpa os caches em memória de materiais/ferramentas (ver
    // _materialTableCache, _roughToolTableCache, _finishToolTableCache mais
    // abaixo). Sem isso, se você editar um valor na tela "Manage Tool /
    // Material Database" DEPOIS de já ter rodado o FBM/AutoDrill uma vez
    // nesta mesma sessão do PATHNC AUTOMATION, a edição só valeria a partir
    // da PRÓXIMA vez que você fechar e abrir o programa (o cache antigo
    // continuaria valendo até lá). Chamado automaticamente pelo botão
    // "Save All Changes" da tela de gerenciamento (ToolDatabaseManagerForm),
    // então o valor editado já entra em vigor no próximo Run() dentro da
    // mesma sessão, sem precisar reiniciar o programa. ──
    public static void InvalidateDatabaseCaches()
    {
        _materialTableCache = null;
        _roughToolTableCache = null;
        _finishToolTableCache = null;
    }

    // Controla o quanto o log mostra: false = só avisos/resumo; true = mostra tudo.
    private const bool VERBOSE = false;

    // ── FG_STEP2HOLE nunca é uma figura de fresagem - é sempre furo
    // escareado/coluna de molde, e o diâmetro decide qual das duas fases de
    // furação usina: a PARTIR de MOLD_COLUMN_MIN_DIAMETER (>=) é uma coluna
    // de molde de verdade (furo em degrau pra bucha/pino guia) e é usinada
    // na FASE 5 (pré-furação + desbaste cilíndrico dos dois diâmetros +
    // acabamento + mandrilhamento de precisão). ABAIXO disso (<) é um
    // counterbore comum e é usinado na FASE 6 (hole_making/COUNTERBORING
    // com ferramenta CBORE_<diametro>MM). As duas fases rodam DENTRO deste
    // mesmo programa agora - antes a FASE 6 era um journal separado
    // (AUTOMATIC_COUNTERBORE.cs) que precisava ser rodado à parte no NX e
    // não estava sendo acionado; foi incorporado aqui pra não depender
    // disso. ──
    private const double MOLD_COLUMN_MIN_DIAMETER = 38.0;

    // ── Counterbore comum (FASE 6) - ferramenta é montada dinamicamente
    // pelo diâmetro medido (CBORE_<diametro>MM), não por uma tabela fixa
    // como o rough. ──
    private const string CBORE_TOOL_PREFIX = "CBORE_";
    private const string CBORE_TOOL_SUFFIX = "MM";
    private const double COUNTERBORE_FEED_PER_REV = 0.1;

    // ── Nomes candidatos pro atributo de raio de canto das figuras
    // retangulares (pocket, slot, furo retangular, superfície planar).
    // NENHUM confirmado ainda - ASSUNÇÃO. Se nenhum bater, o log vai listar
    // os atributos reais disponíveis no grupo pra você confirmar o nome
    // certo (mesmo processo que confirmou DIAMETER_1 pro STEP2HOLE). ──
    private static readonly string[] CornerRadiusAttrCandidates =
    {
        "CORNER_RADIUS",
        "CNR_RADIUS",
        "FILLET_RADIUS",
        "RADIUS_CORNER",
        "MIN_CORNER_RADIUS",
    };

    // ══════════════════════════════════════════════════════════════════════
    // TABELA DE MATERIAIS — velocidade de corte (Vc, m/min) por material
    // ══════════════════════════════════════════════════════════════════════
    //
    // Valores de PARTIDA pra fresa de metal duro (carbide), pesquisados em
    // tabelas de fabricantes/calculadoras de usinagem (Sandvik-style,
    // Kennametal-style, Hymson, 6G Tools, RobbJack - fontes secundárias, não
    // catálogo oficial). São faixas "seguras" pra começar - ajuste conforme
    // o resultado real na sua máquina/ferramenta:
    //   1020 (aço baixo carbono, recozido): rough 120-230 / finish 200-300
    //   1045 (aço médio carbono):           rough  90-150 / finish 130-220
    //   P20  (aço pré-temperado ~28-36HRC): rough  60-110 / finish  90-140
    //   Alumínio (6061/7075):               rough 150-400 / finish 300-600
    //   Bronze (fosforoso/alumínio):        rough  60-150 / finish 130-215
    //   Cobre (C11000):                     rough  80-180 / finish 150-300
    //   Nylon/poliamida:                    sem rough/finish bem definido,
    //     150-370 geral (limitado por avanço/calor, não por Vc)
    // O CutterVc (facemill/cutter com pastilha) usa um valor único pra
    // rough e finish, igual ao padrão que já existia no script (CUTTER_VC).
    // ── Pública (não mais private) porque o ciclo combinado
    // FBM_AUTODRILL_COMPLETE_CYCLE.cs também usa esse tipo agora - ver
    // AskMaterialShared() e o overload Run(args, material) abaixo, que
    // eliminam a pergunta duplicada de material nesse ciclo. ──
    public class MaterialCuttingData
    {
        public string Name;
        public string Code;
        public double EndmillRoughVc;
        public double EndmillFinishVc;
        public double CutterVc;
        public double DrillVc; // broca HSS - usado no counterbore (FASE 6, hole_making/COUNTERBORING)
    }

    // ── DrillVc: valores cruzados com a mesma fonte da tabela original do
    // AUTOMATIC_COUNTERBORE.cs (Machining Doctor, Redline Tools, AIMS
    // Industrial, Machinery's Handbook) pra broca HSS. Bronze não tinha
    // entrada equivalente lá (tinha H13 no lugar) - o valor de 28 aqui é
    // uma ASSUNÇÃO por interpolação entre cobre e aço baixo carbono, não
    // confirmada numa fonte direta. Ajuste se tiver um valor melhor. ──
    // ── Renomeada de "MaterialTable" pra "DefaultMaterialTable" - agora é
    // só o FALLBACK usado quando o banco (ToolDatabase.cs / SQL Server)
    // não estiver acessível. Continua com os MESMOS 7 materiais de sempre,
    // então se você nunca configurar o banco, nada muda. Ver
    // GetMaterialTable() logo abaixo, que é quem decide banco-ou-fallback
    // e é o que todo o resto do arquivo usa agora (em vez de referenciar
    // esta constante direto). ──
    private static readonly MaterialCuttingData[] DefaultMaterialTable =
    {
        new MaterialCuttingData { Code = "1020", Name = "Aço Carbono 1020", EndmillRoughVc = 140.0, EndmillFinishVc = 200.0, CutterVc = 170.0, DrillVc = 25.0 },
        new MaterialCuttingData { Code = "1045", Name = "Aço Carbono 1045", EndmillRoughVc = 100.0, EndmillFinishVc = 160.0, CutterVc = 130.0, DrillVc = 20.0 },
        new MaterialCuttingData { Code = "P20",  Name = "Aço P20 (pré-temperado)", EndmillRoughVc = 70.0,  EndmillFinishVc = 100.0, CutterVc = 90.0, DrillVc = 15.0 },
        new MaterialCuttingData { Code = "AL",   Name = "Alumínio", EndmillRoughVc = 250.0, EndmillFinishVc = 400.0, CutterVc = 350.0, DrillVc = 80.0 },
        new MaterialCuttingData { Code = "BRONZE", Name = "Bronze", EndmillRoughVc = 90.0,  EndmillFinishVc = 160.0, CutterVc = 120.0, DrillVc = 28.0 },
        new MaterialCuttingData { Code = "CU",   Name = "Cobre", EndmillRoughVc = 100.0, EndmillFinishVc = 180.0, CutterVc = 130.0, DrillVc = 30.0 },
        new MaterialCuttingData { Code = "NYLON", Name = "Nylon / Poliamida", EndmillRoughVc = 220.0, EndmillFinishVc = 280.0, CutterVc = 250.0, DrillVc = 60.0 },
    };

    // Cache em memória (por sessão do NX) - só consulta o banco uma vez
    // por execução do NX, não uma vez por chamada.
    private static MaterialCuttingData[] _materialTableCache;

    // ── Ponto único de leitura da tabela de materiais: tenta o banco
    // (ToolDatabase.LoadMaterials) e, se der QUALQUER problema (SQL Server
    // fora do ar, banco não configurado, etc.), cai pra DefaultMaterialTable
    // acima - registra um único aviso no log passado e nunca trava o
    // journal por causa do banco. ──
    private static MaterialCuttingData[] GetMaterialTable(Action<string> log)
    {
        if (_materialTableCache != null)
            return _materialTableCache;

        try
        {
            List<ToolDatabase.MaterialRow> rows = ToolDatabase.LoadMaterials(log);
            if (rows != null && rows.Count > 0)
            {
                List<MaterialCuttingData> lista = new List<MaterialCuttingData>();
                foreach (ToolDatabase.MaterialRow r in rows)
                    lista.Add(new MaterialCuttingData { Code = r.Code, Name = r.Name, EndmillRoughVc = r.EndmillRoughVc, EndmillFinishVc = r.EndmillFinishVc, CutterVc = r.CutterVc, DrillVc = r.DrillVc });
                _materialTableCache = lista.ToArray();
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

    // ── Pergunta o material da peça numa caixa de diálogo simples logo no
    // início da execução. Se o usuário cancelar/fechar, ou se a caixa não
    // puder ser aberta (ex.: sessão sem UI), cai pro primeiro material da
    // tabela (1020) com aviso no log - nunca trava o script por causa disso.
    // Só o Vc muda por material; avanço por rotação (feed) continua fixo
    // por enquanto (ver ENDMILL_ROUGH_FEED_PER_REV etc.) - se quiser que o
    // avanço também escale por material (ex.: nylon aceita avanço bem mais
    // alto), me avisa que eu estendo. ──

    // ── Paleta da caixa de diálogo (tema escuro/industrial, não é só
    // branco). Se quiser trocar as cores da marca, mexe só aqui. ──
    private static readonly Color DialogHeaderColor = Color.FromArgb(24, 45, 74);   // azul marinho escuro
    private static readonly Color DialogBodyColor = Color.FromArgb(245, 246, 248);  // cinza bem claro
    private static readonly Color DialogAccentColor = Color.FromArgb(0, 120, 215);  // azul de destaque (botão OK)
    private static readonly Color DialogTextColor = Color.FromArgb(45, 45, 48);     // cinza escuro pro texto

    private static MaterialCuttingData AskMaterial(LogBuffer lw)
    {
        MaterialCuttingData[] materialTable = GetMaterialTable(lw.WriteLine);
        using (Form form = new Form())
        {
            form.Text = "FBM - Material da peça";
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.TopMost = true;
            form.Font = new Font("Segoe UI", 9F);
            form.BackColor = DialogBodyColor;
            form.ClientSize = new Size(340, 172);

            Panel header = new Panel();
            header.BackColor = DialogHeaderColor;
            header.Location = new System.Drawing.Point(0, 0);
            header.Size = new Size(340, 48);
            form.Controls.Add(header);

            Label headerLabel = new Label();
            headerLabel.Text = "SELEÇÃO DE MATERIAL DA PEÇA";
            headerLabel.ForeColor = Color.White;
            headerLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            headerLabel.Location = new System.Drawing.Point(16, 13);
            headerLabel.AutoSize = true;
            header.Controls.Add(headerLabel);

            Label label = new Label();
            label.Text = "Material a ser usinado:";
            label.ForeColor = DialogTextColor;
            label.Location = new System.Drawing.Point(16, 62);
            label.AutoSize = true;
            form.Controls.Add(label);

            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Location = new System.Drawing.Point(16, 86);
            combo.Width = 308;
            foreach (MaterialCuttingData mat in materialTable)
                combo.Items.Add(mat.Name);
            combo.SelectedIndex = 0;
            form.Controls.Add(combo);

            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.Location = new System.Drawing.Point(150, 128);
            okButton.Size = new Size(84, 30);
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderSize = 0;
            okButton.BackColor = DialogAccentColor;
            okButton.ForeColor = Color.White;
            okButton.DialogResult = DialogResult.OK;
            form.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.Location = new System.Drawing.Point(240, 128);
            cancelButton.Size = new Size(84, 30);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 205);
            cancelButton.BackColor = Color.FromArgb(225, 226, 230);
            cancelButton.ForeColor = DialogTextColor;
            cancelButton.DialogResult = DialogResult.Cancel;
            form.Controls.Add(cancelButton);

            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;

            DialogResult result = form.ShowDialog();
            if (result == DialogResult.OK && combo.SelectedIndex >= 0)
            {
                MaterialCuttingData escolhido = materialTable[combo.SelectedIndex];
                lw.WriteLine("Material selecionado: " + escolhido.Name + " (Vc endmill rough/finish: "
                    + escolhido.EndmillRoughVc.ToString("0", CultureInfo.InvariantCulture) + "/"
                    + escolhido.EndmillFinishVc.ToString("0", CultureInfo.InvariantCulture)
                    + " m/min, Vc cutter: " + escolhido.CutterVc.ToString("0", CultureInfo.InvariantCulture) + " m/min).");
                return escolhido;
            }

            lw.WriteLine("AVISO: seleção de material cancelada/fechada - usando material padrão '" + materialTable[0].Name + "'.");
            return materialTable[0];
        }
    }

    // ── Ponto de entrada público pra outro script perguntar o material
    // usando esta MESMA tabela/diálogo, uma única vez, e depois passar o
    // resultado pra cá via Run(args, material) - em vez de cada script
    // (FBM e AutoDrill) abrir seu próprio diálogo de material no mesmo
    // ciclo. 'log' recebe cada linha do que normalmente iria pro
    // LogBuffer interno (pode ser null se quem chamar não quiser log). ──
    public static MaterialCuttingData AskMaterialShared(Action<string> log)
    {
        LogBuffer lw = new LogBuffer();
        MaterialCuttingData[] materialTable = GetMaterialTable(log);
        MaterialCuttingData material = materialTable[0];
        try
        {
            material = AskMaterial(lw);
        }
        catch (Exception ex)
        {
            lw.WriteLine("AVISO: não foi possível abrir a caixa de seleção de material (" + ex.Message
                + "). Usando material padrão '" + materialTable[0].Name + "'.");
        }

        if (log != null)
            foreach (string line in lw.Lines)
                log(line);

        return material;
    }

    // ══════════════════════════════════════════════════════════════════════
    // REGISTRO DE TIPOS DE FEATURE — fonte única pra reconhecimento (FASE 0),
    // classificação (ClassifyMillingGroup) e nome curto de operação
    // (ShortFeatureTag)
    // ══════════════════════════════════════════════════════════════════════
    //
    // ANTES desse registro, cada tipo de feature precisava ser adicionado em
    // 3 lugares separados (lista tiposParaReconhecer da FASE 0, if-chain do
    // ClassifyMillingGroup e mapa do ShortFeatureTag) - é fácil esquecer um
    // dos três (foi exatamente o que aconteceu com SLOT_PARTIAL_RECTANGULAR
    // e POCKET_OPEN, ver comentários históricos nos itens abaixo). Agora
    // existe UM único lugar (FeatureTypeRegistry, logo abaixo) com todas as
    // propriedades de cada tipo de feature. Pra adicionar um tipo novo:
    //
    //   1. Adicione um item novo em FeatureTypeRegistry (posição só importa
    //      em relação a outros prefixos que colidem por StartsWith - ver
    //      nota "ORDEM IMPORTA" abaixo).
    //   2. Se o tipo precisa de reconhecimento automático (FBM) nesta FASE
    //      0, preencha RecognitionTypeName; senão deixe null (ex.: quando o
    //      grupo já vem de outro processo, como FG_STEP2HOLE).
    //   3. Se a família (Family) ainda não existe, crie um novo valor em
    //      MillFamily e o pipeline de operações correspondente (dicionário
    //      + bloco de fase em Run(), à imagem dos que já existem) - ou, se
    //      quiser só reconhecer/agrupar por enquanto sem usinar nada ainda,
    //      use MillFamily.None (mesmo comportamento de hoje pros tipos só
    //      reconhecidos, ver a seção "reconhecidos mas sem estratégia" no
    //      fim da tabela).
    //
    // Cada família (MillFamily) tem um jeito diferente de criar a operação
    // NXOpen (subtype, padrão de corte, engajamento), mas as famílias de
    // fresagem usam a mesma tabela de seleção de ferramenta por medida
    // (SelectRoughTool/SelectFinishTool/SelectFacemillTool) em vez de
    // ferramenta fixa como nos scripts originais.
    //
    // IMPORTANTE: esse script NÃO cria nenhuma furação (spot drill, peck
    // drill, counterbore "simples", furo cônico etc). Grupos de furo
    // (FG_STEP1*, FG_HOLE* redondo, roscas) continuam não sendo processados
    // aqui.
    // ══════════════════════════════════════════════════════════════════════
    private enum MillFamily
    {
        None,
        GenericFloorWall,   // Obround, Slot, Superficie Planar (+ redonda), Step2Hole grande
        PocketRectangular,  // Pocketing com rampa de mergulho, floor facing, wall multi-stepover
        PocketOpen,         // Pocketing FollowPart/ZigZag, floor facing, wall multi-stepover
        HoleRectangular,    // Pocketing FollowPart/ZigZag (mesma estrategia do PocketOpen), so wall finish - sem floor finish (feature vazada). Também usada por WEDM_RECTANGULAR_STRAIGHT/WEDM_OBROUND_STRAIGHT/WEDM_FREE_SHAPED_STRAIGHT (ver FeatureTypeRegistry).
        PlanarFacing,       // Faceamento (facemill grande, ZigZag) - Rough + Finish
    }

    private class FeatureTypeSpec
    {
        // Nome exato a passar pro GroupFeatures.SetFeatureTypes() na FASE 0
        // (AFR). null = este tipo não é buscado pelo reconhecimento
        // automático deste script (ex.: FG_STEP2HOLE, que hoje é fornecido
        // por outro processo/journal).
        public string RecognitionTypeName;

        // Prefixo do nome do FeatureGeometryGroup (sempre com "FG_"),
        // usado por ClassifyMillingGroup/ShortFeatureTag via StartsWith.
        public string GroupNamePrefix;

        // Tag curta pro nome da operação (ShortFeatureTag). null quando o
        // grupo é só reconhecido/agrupado, mas nunca chega nas funções
        // Create*Operation (Family == None).
        public string ShortTag;

        // Atributo de tamanho a ler do CAMFeature ("WIDTH"/"RADIUS"...).
        // null quando o grupo ainda não tem estratégia de usinagem.
        public string SizeAttributeName;

        // Pipeline de operações a aplicar. MillFamily.None = reconhecido/
        // classificável, mas sem usinagem implementada ainda.
        public MillFamily Family;

        // Se true, tenta resolver o raio de canto da figura (atributo ou
        // geometria) pra restringir a seleção de ferramenta rough/finish.
        // Não se aplica a faceamento (facemill não entra em canto fechado).
        public bool NeedsCornerRadius = true;
    }

    // ── ORDEM IMPORTA: prefixos mais específicos precisam vir ANTES de
    // prefixos mais curtos que são um prefixo deles (ex.:
    // "FG_SURFACE_PLANAR_ROUND"/"..._RECTANGULAR" também começam com
    // "FG_SURFACE_PLANAR" - por isso os dois primeiros vêm antes do
    // genérico). A busca abaixo (FindFeatureTypeSpec) para no primeiro que
    // bater, na ordem desta lista. Os itens sem ShortTag/Family real (fim
    // da lista) são reconhecidos pela FASE 0 mas nunca tiveram uma
    // estratégia de usinagem definida - herdado do comportamento original,
    // não é bug introduzido aqui; a ordem deles entre si não importa porque
    // nenhum é um prefixo de outro tipo classificado acima. ──
    private static readonly FeatureTypeSpec[] FeatureTypeRegistry =
    {
        // NOVO — WEDM_RECTANGULAR_STRAIGHT. Colocado no início do registro
        // (a pedido) como o exemplo mais recente de como adicionar um tipo.
        // Apesar do nome (a geometria vem do reconhecimento de corte a fio),
        // por pedido do usuário essa feature é USINADA COMO SE FOSSE UM
        // FURO/POCKET RETANGULAR VAZADO - mesma ferramenta convencional
        // (fresa), mesma estratégia de desbaste + acabamento de parede da
        // família HoleRectangular, só que SEM acabamento de fundo (a
        // geometria é vazada, não tem fundo pra acabar) - exatamente o
        // pipeline que HoleRectangular já implementa hoje (FASE 1d + FASE
        // 3d, sem FASE "2d"). Por isso reaproveita Family =
        // MillFamily.HoleRectangular em vez de criar uma família nova -
        // zero código novo de usinagem, só a classificação. ASSUNÇÃO:
        // atributo de tamanho "WIDTH", pelo mesmo padrão de
        // POCKET_RECTANGULAR_STRAIGHT e HOLE_RECTANGULAR_STRAIGHT (nomes
        // "..._RECTANGULAR_STRAIGHT") - se estiver errado, o log lista os
        // atributos reais disponíveis.
        new FeatureTypeSpec { RecognitionTypeName = "WEDM_RECTANGULAR_STRAIGHT", GroupNamePrefix = "FG_WEDM_RECTANGULAR_STRAIGHT", ShortTag = "WEDM", SizeAttributeName = "WIDTH", Family = MillFamily.HoleRectangular },

        // NOVO — WEDM_OBROUND_STRAIGHT e WEDM_FREE_SHAPED_STRAIGHT. Mesmo
        // tratamento do WEDM_RECTANGULAR_STRAIGHT acima (usinados como se
        // fossem furo/pocket retangular vazado - reaproveita Family =
        // MillFamily.HoleRectangular, então entram automaticamente no
        // mesmo pipeline: FASE 1d (desbaste) + FASE 3d (acabamento de
        // parede/perfil), SEM acabamento de fundo, porque a geometria é
        // vazada. Também entram automaticamente na lista de reconhecimento
        // da FASE 0 (derivada deste registro) - nenhuma outra alteração no
        // arquivo é necessária.
        //   OBROUND: ASSUNÇÃO de atributo "RADIUS", pelo mesmo padrão de
        //     POCKET_OBROUND_STRAIGHT/SURFACE_PLANAR_ROUND (figuras
        //     "..._OBROUND..." já usam RADIUS neste registro).
        //   FREE_SHAPED: ASSUNÇÃO de atributo "WIDTH" - NÃO HÁ precedente
        //     confirmado neste arquivo (POCKET_FREE_SHAPED_STRAIGHT, mais
        //     abaixo, está na lista "reconhecido mas sem estratégia", sem
        //     atributo de tamanho nunca confirmado). Se o atributo real for
        //     outro, o log (LogAttributeNames) lista os atributos
        //     disponíveis no grupo pra confirmar o nome certo - me avisa
        //     que eu ajusto.
        new FeatureTypeSpec { RecognitionTypeName = "WEDM_OBROUND_STRAIGHT", GroupNamePrefix = "FG_WEDM_OBROUND_STRAIGHT", ShortTag = "WEDO", SizeAttributeName = "RADIUS", Family = MillFamily.HoleRectangular },
        new FeatureTypeSpec { RecognitionTypeName = "WEDM_FREE_SHAPED_STRAIGHT", GroupNamePrefix = "FG_WEDM_FREE_SHAPED_STRAIGHT", ShortTag = "WEDF", SizeAttributeName = "WIDTH", Family = MillFamily.HoleRectangular },

        // ── Famílias de fresagem já implementadas (comportamento idêntico
        // ao ClassifyMillingGroup/ShortFeatureTag originais) ──
        new FeatureTypeSpec { RecognitionTypeName = null, GroupNamePrefix = "FG_HOLE_RECTANGULAR_STRAIGHT", ShortTag = "HRET", SizeAttributeName = "WIDTH", Family = MillFamily.HoleRectangular },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_RECTANGULAR_STRAIGHT", GroupNamePrefix = "FG_POCKET_RECTANGULAR_STRAIGHT", ShortTag = "PKTR", SizeAttributeName = "WIDTH", Family = MillFamily.PocketRectangular },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_OBROUND_STRAIGHT", GroupNamePrefix = "FG_POCKET_OBROUND_STRAIGHT", ShortTag = "POBR", SizeAttributeName = "RADIUS", Family = MillFamily.GenericFloorWall },
        new FeatureTypeSpec { RecognitionTypeName = "SLOT_PARTIAL_RECTANGULAR", GroupNamePrefix = "FG_SLOT_PARTIAL_RECTANGULAR", ShortTag = "SLTP", SizeAttributeName = "WIDTH", Family = MillFamily.GenericFloorWall },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_OPEN", GroupNamePrefix = "FG_POCKET_OPEN", ShortTag = "POPN", SizeAttributeName = "WIDTH", Family = MillFamily.PocketOpen },
        new FeatureTypeSpec { RecognitionTypeName = "SURFACE_PLANAR_ROUND", GroupNamePrefix = "FG_SURFACE_PLANAR_ROUND", ShortTag = "SPLR", SizeAttributeName = "RADIUS", Family = MillFamily.GenericFloorWall }, // ASSUNÇÃO: mesmo atributo do Obround (RADIUS). Confirmar - se errado, o log lista os atributos reais.
        new FeatureTypeSpec { RecognitionTypeName = "SURFACE_PLANAR_RECTANGULAR", GroupNamePrefix = "FG_SURFACE_PLANAR_RECTANGULAR", ShortTag = "SPLX", SizeAttributeName = "WIDTH", Family = MillFamily.PlanarFacing, NeedsCornerRadius = false },
        new FeatureTypeSpec { RecognitionTypeName = "SURFACE_PLANAR", GroupNamePrefix = "FG_SURFACE_PLANAR", ShortTag = "SPLN", SizeAttributeName = "WIDTH", Family = MillFamily.GenericFloorWall },

        // ── Furação especial (fora do fluxo de fresagem regular - ver
        // tratamento dedicado de FG_STEP2HOLE em Run(), FASE 5/6). Mantido
        // no registro só pra ShortFeatureTag conseguir taguear o nome. ──
        new FeatureTypeSpec { RecognitionTypeName = null, GroupNamePrefix = "FG_STEP2HOLE", ShortTag = "STP2", SizeAttributeName = null, Family = MillFamily.None },

        // ── Reconhecidos pela FASE 0 (estavam em tiposParaReconhecer no
        // script original) mas SEM estratégia de usinagem definida - o
        // comportamento de hoje já era ignorar esses grupos (caem em
        // MillFamily.None => "continue" no loop de classificação). Preservado
        // aqui só pra manter a FASE 0 idêntica ao original nesse ponto;
        // qualquer um pode virar uma família de verdade seguindo os mesmos
        // 3 passos do comentário no topo desta seção. ──
        new FeatureTypeSpec { RecognitionTypeName = "SLOT_RECTANGULAR", GroupNamePrefix = "FG_SLOT_RECTANGULAR", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "SLOT_OBROUND", GroupNamePrefix = "FG_SLOT_OBROUND", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "SLOT_PARTIAL_OBROUND", GroupNamePrefix = "FG_SLOT_PARTIAL_OBROUND", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "SLOT_PARTIAL_ROUND", GroupNamePrefix = "FG_SLOT_PARTIAL_ROUND", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "BOSS_RECTANGULAR_STRAIGHT", GroupNamePrefix = "FG_BOSS_RECTANGULAR_STRAIGHT", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "BOSS_ROUND_STRAIGHT_THREAD", GroupNamePrefix = "FG_BOSS_ROUND_STRAIGHT_THREAD", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "BOSS_ROUND_STRAIGHT", GroupNamePrefix = "FG_BOSS_ROUND_STRAIGHT", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_ROUND_TAPERED", GroupNamePrefix = "FG_POCKET_ROUND_TAPERED", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_CLOSED", GroupNamePrefix = "FG_POCKET_CLOSED", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_OBROUND_CURVED_STRAIGHT", GroupNamePrefix = "FG_POCKET_OBROUND_CURVED_STRAIGHT", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
        new FeatureTypeSpec { RecognitionTypeName = "POCKET_FREE_SHAPED_STRAIGHT", GroupNamePrefix = "FG_POCKET_FREE_SHAPED_STRAIGHT", ShortTag = null, SizeAttributeName = null, Family = MillFamily.None },
    };

    // Procura, na ordem do registro, o primeiro FeatureTypeSpec cujo
    // GroupNamePrefix bate com o nome do grupo (StartsWith). Usado tanto
    // por ClassifyMillingGroup quanto por ShortFeatureTag - single source
    // of truth, evita os dois ficarem fora de sincronia (ver comentário no
    // topo desta seção).
    private static FeatureTypeSpec FindFeatureTypeSpec(string groupName)
    {
        foreach (FeatureTypeSpec spec in FeatureTypeRegistry)
        {
            if (spec.GroupNamePrefix != null && groupName.StartsWith(spec.GroupNamePrefix, StringComparison.OrdinalIgnoreCase))
                return spec;
        }
        return null;
    }

    // Classifica o grupo pelo NOME e devolve qual atributo usar pra medir o
    // tamanho e se a seleção de ferramenta deve considerar raio de canto.
    private static MillFamily ClassifyMillingGroup(string name, out string attrName, out bool needsCornerRadius)
    {
        FeatureTypeSpec spec = FindFeatureTypeSpec(name);
        if (spec == null)
        {
            attrName = null;
            needsCornerRadius = false;
            return MillFamily.None;
        }

        attrName = spec.SizeAttributeName;
        needsCornerRadius = spec.NeedsCornerRadius;
        return spec.Family;
    }

    public static void Run(string[] args)
    {
        Run(args, null);
    }

    // ── preSelectedMaterial: quando != null, PULA o diálogo de material
    // abaixo e usa direto o material recebido - usado pelo ciclo combinado
    // FBM_AUTODRILL_COMPLETE_CYCLE.cs, que agora pergunta o material UMA
    // VEZ só (reaproveitando AskMaterialShared, acima) antes de chamar FBM
    // + AutoDrill, em vez de cada um perguntar por conta própria. Chamado
    // sem esse parâmetro (ou com null, como o botão/comando do FBM sozinho
    // continua fazendo via Run(args) acima), o comportamento é EXATAMENTE
    // o de sempre: pergunta o material aqui mesmo. ──
    public static void Run(string[] args, MaterialCuttingData preSelectedMaterial)
    {
        Run(args, preSelectedMaterial, true);
    }

    // ── includeMoldColumnAndCounterbore: quando false, PULA a FASE 5
    // (coluna de molde) e a FASE 6 (counterbore comum) inteiras - ou
    // seja, NENHUMA operação em FG_STEP2HOLE é criada, só as figuras de
    // fresagem (pocket/slot/superfície/furo retangular etc., que este
    // script já isola desde sempre). Usado pelo FBM_MILLING_ONLY.cs, que
    // quer só usinar figura, zero furação. Chamado sem esse parâmetro
    // (via Run(args) ou Run(args, material) acima, default true), o
    // comportamento é o de sempre: processa FG_STEP2HOLE normalmente. ──
    public static void Run(string[] args, MaterialCuttingData preSelectedMaterial, bool includeMoldColumnAndCounterbore)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        if (workPart == null)
            return;

        LogBuffer lw = new LogBuffer();

        MaterialCuttingData[] materialTableForRun = GetMaterialTable(lw.WriteLine);
        MaterialCuttingData material = materialTableForRun[0];
        if (preSelectedMaterial != null)
        {
            material = preSelectedMaterial;
            lw.WriteLine("Material recebido do ciclo combinado (já escolhido uma vez, antes de rodar FBM + AutoDrill): " + material.Name + ".");
        }
        else
        {
            // Pergunta o material ANTES de qualquer outra coisa - se a caixa de
            // diálogo falhar por algum motivo (ex.: sessão sem UI), cai pro
            // material padrão em vez de travar o script inteiro.
            try
            {
                material = AskMaterial(lw);
            }
            catch (Exception ex)
            {
                lw.WriteLine("AVISO: não foi possível abrir a caixa de seleção de material (" + ex.Message
                    + "). Usando material padrão '" + materialTableForRun[0].Name + "'.");
            }
        }

        NXOpen.Session.UndoMarkId markId1 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, "FBM_ALL_FEATURES_MACHINED");

        try
        {
            NXOpen.UF.UFSession ufs = NXOpen.UF.UFSession.GetUFSession();

            // ══════════════════════════════════════════════════════════════
            // FASE 0 — RECONHECIMENTO E AGRUPAMENTO DE FEATURES (AFR)
            // ══════════════════════════════════════════════════════════════
            //
            // Integrado do journal CREATE_GROUP_FEATURES: roda o
            // reconhecimento automático de features e cria os
            // FeatureGeometryGroup ANTES de classificar/usinar - sem isso
            // não existe nenhum FG_* no CAMGroupCollection pra esse script
            // processar depois.
            //
            // STEP1POCKET, STEP1HOLE, STEP2HOLE e STEP1POCKET_THREAD foram
            // removidos da lista de tipos reconhecidos por pedido do
            // usuário - esse processo não usina furo/rosca (ver FASE 0 de
            // furação removida mais abaixo), então nem faz sentido
            // reconhecer/agrupar essas features aqui.
            //
            // CORREÇÃO em relação ao journal original: ele terminava com
            // FeaturesToGroupType = All, o que faz o NX agrupar TODOS os
            // tipos de feature reconhecidos e ignora a lista específica de
            // SetFeatureTypes (inclusive os 4 tipos removidos acima
            // continuariam sendo agrupados). Troquei pra SpecifyFeatures pra
            // a lista abaixo realmente ser respeitada - se isso não bater
            // com o comportamento esperado no seu NX, me avisa que eu ajusto.
            lw.WriteLine("=== FASE 0: RECONHECIMENTO E AGRUPAMENTO DE FEATURES ===");
            try
            {
                // Derivado do FeatureTypeRegistry (seção "REGISTRO DE TIPOS
                // DE FEATURE", mais abaixo nesta classe) - pega todo
                // RecognitionTypeName não-nulo, na ordem do registro. Isso
                // garante que essa lista nunca mais fica fora de sincronia
                // com a classificação/tag (ver comentário no registro sobre
                // o motivo dessa unificação - SLOT_PARTIAL_RECTANGULAR e
                // POCKET_OPEN já tiveram bug por causa disso). A ordem dos
                // itens aqui não importa pro NX (é só a lista de tipos a
                // procurar, não uma sequência).
                List<string> tiposParaReconhecerList = new List<string>();
                foreach (FeatureTypeSpec spec in FeatureTypeRegistry)
                {
                    if (spec.RecognitionTypeName != null)
                        tiposParaReconhecerList.Add(spec.RecognitionTypeName);
                }
                string[] tiposParaReconhecer = tiposParaReconhecerList.ToArray();

                NXOpen.Point3d origemEixoAcesso = new NXOpen.Point3d(0.0, 0.0, 0.0);
                NXOpen.Vector3d direcaoEixoAcesso = new NXOpen.Vector3d(0.0, 0.0, 1.0);
                NXOpen.Direction direcaoAcesso = workPart.Directions.CreateDirection(
                    origemEixoAcesso, direcaoEixoAcesso, NXOpen.SmartObject.UpdateOption.AfterModeling);
                NXOpen.Direction[] direcoesAcesso = { direcaoAcesso };

                // ── CORREÇÃO (bug relatado: "0 grupos" em tudo, mesmo com
                // pockets/slots/WEDM reais na peça): esta FASE 0 dizia no
                // comentário que "roda o reconhecimento automático de
                // features", mas o código só chamava GroupFeatures (que
                // AGRUPA CAMFeature que JÁ EXISTEM - não cria features
                // novas a partir da geometria). Sem um FeatureRecognitionBuilder
                // rodando antes, numa peça sem reconhecimento prévio (ex.:
                // sem ter rodado RECOGNIZE_FEATURES.cs manualmente antes),
                // não existe NENHUM CAMFeature dos tipos de fresagem pra
                // agrupar, e o resultado é sempre 0 - era exatamente esse o
                // sintoma. Adicionado aqui o passo de reconhecimento de
                // verdade (mesmo padrão do RECOGNIZE_FEATURES.cs), ANTES do
                // agrupamento, usando a MESMA lista de tipos (tiposParaReconhecer)
                // pras duas etapas. ──
                NXOpen.CAM.CAMObject nullCamObjectFase0 = null;
                NXOpen.CAM.FeatureRecognitionBuilder frbFase0 = workPart.CAMSetup.CreateFeatureRecognitionBuilder(nullCamObjectFase0);
                NXOpen.CAM.ManualFeatureBuilder mfbFase0 = frbFase0.CreateManualFeatureBuilder();
                frbFase0.AssignColor = false;
                frbFase0.AddCadFeatureAttributes = false;
                frbFase0.MapFeatures = false;
                frbFase0.RecognitionType = NXOpen.CAM.FeatureRecognitionBuilder.RecognitionEnum.Parametric;
                // Mesma correção já validada em RECOGNIZE_FEATURES.cs: ignora
                // o tipo herdado da modelagem, reclassifica pela geometria atual.
                frbFase0.UseFeatureNameAsType = false;
                frbFase0.IgnoreWarnings = false;
                frbFase0.SetMachiningAccessDirection(direcoesAcesso, 9.9999999999999995e-07);
                frbFase0.SetFeatureTypes(tiposParaReconhecer);
                frbFase0.GeometrySearchType = NXOpen.CAM.FeatureRecognitionBuilder.GeometrySearch.Workpiece;
                NXOpen.CAM.CAMFeature[] featuresReconhecidasFase0 = frbFase0.FindFeatures();
                frbFase0.Commit();
                frbFase0.Destroy();
                mfbFase0.Destroy();

                NXOpen.CAM.GroupFeatures groupFeatures1 = workPart.CAMSetup.CAMGroupCollection.CreateGroupFeatures();
                groupFeatures1.GeometryLocation = "WORKPIECE";
                groupFeatures1.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.SpecifyFeatures;
                groupFeatures1.SetFeatureTypes(tiposParaReconhecer);
                groupFeatures1.SetMachiningAccessDirections(direcoesAcesso, 9.9999999999999995e-07);

                groupFeatures1.CreateFeatureGroups();
                groupFeatures1.Commit();
                groupFeatures1.Destroy();

                lw.WriteLine("Reconhecimento de features concluído (" + tiposParaReconhecer.Length
                    + " tipos pesquisados, " + (featuresReconhecidasFase0 != null ? featuresReconhecidasFase0.Length : 0)
                    + " feature(s) reconhecida(s) nesta passada; STEP1POCKET/STEP1HOLE/STEP2HOLE/STEP1POCKET_THREAD excluídos da busca).");
            }
            catch (Exception ex)
            {
                lw.WriteLine("ERRO no reconhecimento/agrupamento de features: " + ex.Message);
                lw.WriteLine("AVISO: sem features reconhecidas, as fases seguintes provavelmente não vão encontrar nenhum grupo FG_* pra processar.");
            }

            NCGroup programGroup = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");
            Method method = (Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_ROUGH");
            NCGroup noneGroup = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE");

            Dictionary<string, Tool> toolCache = BuildToolCache(workPart);
            Dictionary<string, bool> usedNames = BuildUsedNamesCache(workPart);

            // ── Pastas de organização (Program Order) ──
            // Observação: nenhuma pasta de furação (spot drill, thru/blind
            // drill, counterbore) é criada aqui - o processo não usina furos.
            // Só 3 pastas pra TODAS as famílias (genérico, pocket, pocket
            // aberto, furo retangular, faceamento) - uma pasta por
            // feature group ficava confuso/poluído. Rough, floor finish e
            // faceamento rough vão pra ROUGH; floor finish + faceamento
            // finish vão pra FLOOR_FINISH (faceamento finish também acaba
            // numa superfície plana, mesma lógica de "fundo"); todo o resto
            // de acabamento de parede vai pra WALL_FINISH.
            NCGroup ftMillRoughFolder = FindOrCreateProgramFolder(workPart, programGroup, "FT_MILL_ROUGH", usedNames, lw);
            NCGroup ftFloorFinishFolder = FindOrCreateProgramFolder(workPart, programGroup, "FT_FLOOR_FINISH", usedNames, lw);
            NCGroup ftWallFinishFolder = FindOrCreateProgramFolder(workPart, programGroup, "FT_WALL_FINISH", usedNames, lw);

            // ══════════════════════════════════════════════════════════════
            // CLASSIFICAÇÃO DE TODOS OS GRUPOS DE FRESAGEM
            // ══════════════════════════════════════════════════════════════
            //
            // Grupos de furação (FG_STEP1HOLE, FG_STEP1POCKET, FG_HOLE*
            // redondo, roscas etc.) NÃO são classificados nem usinados por
            // este script - somente as "figuras" de fresagem abaixo
            // (obround, slot, pocket, superfície planar, furo retangular
            // fresado e faceamento). FG_STEP2HOLE nunca é figura de
            // fresagem - é desviado pra moldColumnGroups (FASE 5, coluna de
            // molde) quando DIAMETER_1 >= MOLD_COLUMN_MIN_DIAMETER, ou
            // ignorado aqui quando não (counterbore comum, cuidado pelo
            // AUTOMATIC_COUNTERBORE.cs separado).
            List<FeatureGeometryGroup> allGroups = GetAllFeatureGeometryGroups(workPart);

            Dictionary<FeatureGeometryGroup, double> widthByGroup = new Dictionary<FeatureGeometryGroup, double>();
            Dictionary<FeatureGeometryGroup, double> pocketWidthByGroup = new Dictionary<FeatureGeometryGroup, double>();
            Dictionary<FeatureGeometryGroup, double> pocketOpenWidthByGroup = new Dictionary<FeatureGeometryGroup, double>();
            Dictionary<FeatureGeometryGroup, double> holeRectWidthByGroup = new Dictionary<FeatureGeometryGroup, double>();
            Dictionary<FeatureGeometryGroup, double> facingWidthByGroup = new Dictionary<FeatureGeometryGroup, double>();

            // Raio de canto de cada figura (quando identificável) - usado
            // pra restringir a seleção de ferramenta de desbaste/acabamento
            // (rough e finish) de forma que ela realmente caiba no canto.
            Dictionary<FeatureGeometryGroup, double> cornerRadiusByGroup = new Dictionary<FeatureGeometryGroup, double>();

            List<FeatureGeometryGroup> moldColumnGroups = new List<FeatureGeometryGroup>();
            // FG_STEP2HOLE com DIAMETER_1 < MOLD_COLUMN_MIN_DIAMETER: não é
            // coluna de molde, é counterbore comum - processado na FASE 6
            // (COUNTERBORING), incorporada aqui mesmo (antes era um journal
            // separado, AUTOMATIC_COUNTERBORE.cs, que não rodava sozinho).
            Dictionary<FeatureGeometryGroup, double> counterboreGroups = new Dictionary<FeatureGeometryGroup, double>();
            int counterboreDiameterUnreadableCount = 0;

            foreach (FeatureGeometryGroup fg in allGroups)
            {
                if (fg.Name.StartsWith("FG_STEP2HOLE", StringComparison.OrdinalIgnoreCase))
                {
                    // FG_STEP2HOLE nunca é figura de fresagem. O diâmetro
                    // decide o destino: coluna de molde (FASE 5) se for
                    // grande, ou counterbore comum (FASE 6) se não for.
                    double diamCbore;
                    string erroCbore;
                    bool okCbore = TryGetAttributeDouble(fg, "DIAMETER_1", out diamCbore, out erroCbore);
                    if (!okCbore)
                    {
                        counterboreDiameterUnreadableCount++;
                        lw.WriteLine("AVISO: " + fg.Name + " -> não conseguiu ler DIAMETER_1: " + erroCbore + ". Grupo ignorado (nem coluna de molde, nem counterbore).");
                        continue;
                    }

                    if (diamCbore >= MOLD_COLUMN_MIN_DIAMETER)
                    {
                        moldColumnGroups.Add(fg);
                        if (VERBOSE)
                            lw.WriteLine(fg.Name + " -> coluna de molde (DIAMETER_1 = " + diamCbore.ToString("0.###", CultureInfo.InvariantCulture)
                                + "mm >= " + MOLD_COLUMN_MIN_DIAMETER.ToString("0.###", CultureInfo.InvariantCulture) + "mm). Vai pra FASE 5.");
                    }
                    else
                    {
                        counterboreGroups[fg] = diamCbore;
                        if (VERBOSE)
                            lw.WriteLine(fg.Name + " -> counterbore comum (DIAMETER_1 = " + diamCbore.ToString("0.###", CultureInfo.InvariantCulture)
                                + "mm < " + MOLD_COLUMN_MIN_DIAMETER.ToString("0.###", CultureInfo.InvariantCulture) + "mm). Vai pra FASE 6.");
                    }
                    continue;
                }

                string attrName;
                bool needsCornerRadius;
                MillFamily family = ClassifyMillingGroup(fg.Name, out attrName, out needsCornerRadius);
                if (family == MillFamily.None)
                    continue; // não é um grupo que esse script processa (inclui furação)

                double size;
                string erro;
                bool ok = TryGetAttributeDouble(fg, attrName, out size, out erro);
                if (!ok)
                {
                    lw.WriteLine("AVISO: " + fg.Name + " -> não conseguiu ler " + attrName + ": " + erro + ". Grupo ignorado.");
                    LogAttributeNames(fg, lw);
                    continue;
                }

                switch (family)
                {
                    case MillFamily.PocketRectangular:
                        pocketWidthByGroup[fg] = size;
                        break;
                    case MillFamily.PocketOpen:
                        pocketOpenWidthByGroup[fg] = size;
                        break;
                    case MillFamily.HoleRectangular:
                        holeRectWidthByGroup[fg] = size;
                        break;
                    case MillFamily.PlanarFacing:
                        facingWidthByGroup[fg] = size;
                        break;
                    default:
                        widthByGroup[fg] = size;
                        break;
                }

                if (needsCornerRadius)
                {
                    // Faceamento não tem passe de parede dedicado (facemill
                    // não precisa entrar em canto fechado) - não faz sentido
                    // restringir por raio de canto nesse caso (ver
                    // NeedsCornerRadius no FeatureTypeRegistry).
                    double? cornerRadius = ResolveCornerRadius(fg, attrName, size, ufs, lw);
                    if (cornerRadius.HasValue)
                        cornerRadiusByGroup[fg] = cornerRadius.Value;
                }
            }

            lw.WriteLine("Grupos classificados -> genérico(floor/wall): " + widthByGroup.Count
                + " | pocket retangular: " + pocketWidthByGroup.Count
                + " | pocket aberto: " + pocketOpenWidthByGroup.Count
                + " | furo retangular (inclui WEDM_RECTANGULAR_STRAIGHT/WEDM_OBROUND_STRAIGHT/WEDM_FREE_SHAPED_STRAIGHT, usinados igual): " + holeRectWidthByGroup.Count
                + " | faceamento: " + facingWidthByGroup.Count
                + " | coluna de molde (FASE 5): " + moldColumnGroups.Count
                + " | counterbore comum (FASE 6): " + counterboreGroups.Count
                + " | FG_STEP2HOLE com DIAMETER_1 ilegível (ignorado): " + counterboreDiameterUnreadableCount);
            lw.WriteLine("Raio de canto identificado em " + cornerRadiusByGroup.Count + " de "
                + (widthByGroup.Count + pocketWidthByGroup.Count + pocketOpenWidthByGroup.Count + holeRectWidthByGroup.Count)
                + " grupos passíveis de fresagem de parede/canto.");

            // ══════════════════════════════════════════════════════════════
            // FASE 4a — FACEAMENTO ROUGH (superfície planar retangular)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 4a: FACEAMENTO ROUGH ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in facingWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection facemillSel = SelectFacemillTool(width);
                    Tool facemillTool;
                    toolCache.TryGetValue(facemillSel.ToolName, out facemillTool);
                    if (facemillTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> facemill '" + facemillSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateFacingRoughOperation(workPart, ftMillRoughFolder, method, facemillTool, facemillSel, fg, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (faceamento rough): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 4b — FACEAMENTO FINISH (superfície planar retangular)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 4b: FACEAMENTO FINISH ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in facingWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection facemillSel = SelectFacemillTool(width);
                    Tool facemillTool;
                    toolCache.TryGetValue(facemillSel.ToolName, out facemillTool);
                    if (facemillTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> facemill '" + facemillSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateFacingFinishOperation(workPart, ftFloorFinishFolder, method, facemillTool, facemillSel, fg, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (faceamento finish): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 1 — DESBASTE (família genérica floor/wall)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 1: DESBASTE (genérico) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in widthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection roughSel = SelectRoughTool(width, toolCache, fg.Name, lw);
                    Tool roughTool;
                    toolCache.TryGetValue(roughSel.ToolName, out roughTool);
                    if (roughTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de desbaste '" + roughSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateRoughOperation(workPart, ftMillRoughFolder, method, roughTool, roughSel, fg, width, material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (desbaste): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 1b — DESBASTE (pocket retangular - POCKETING)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 1b: DESBASTE DE POCKETS (POCKETING) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection roughSel = SelectRoughTool(width, toolCache, fg.Name, lw);
                    Tool roughTool;
                    toolCache.TryGetValue(roughSel.ToolName, out roughTool);
                    if (roughTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de desbaste '" + roughSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketRoughOperation(workPart, ftMillRoughFolder, noneGroup, roughTool, roughSel, fg, width, material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (desbaste de pocket): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 1c — DESBASTE (pocket aberto - POCKETING FollowPart/ZigZag)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 1c: DESBASTE DE POCKET ABERTO ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketOpenWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection roughSel = SelectRoughTool(width, toolCache, fg.Name, lw);
                    Tool roughTool;
                    toolCache.TryGetValue(roughSel.ToolName, out roughTool);
                    if (roughTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de desbaste '" + roughSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateOpenStyleRoughOperation(workPart, ftMillRoughFolder, noneGroup, roughTool, roughSel, fg, width, "POR_", material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (desbaste de pocket aberto): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 1d — DESBASTE (furo retangular - mesma estratégia FollowPart)
            // ══════════════════════════════════════════════════════════════
            //
            // Também processa WEDM_RECTANGULAR_STRAIGHT, WEDM_OBROUND_STRAIGHT
            // e WEDM_FREE_SHAPED_STRAIGHT (classificados na família
            // HoleRectangular por pedido do usuário - ver
            // FeatureTypeRegistry): mesma estratégia de desbaste, com fresa
            // convencional.
            lw.WriteLine("");
            lw.WriteLine("=== FASE 1d: DESBASTE DE FURO RETANGULAR (inclui WEDM_RECTANGULAR_STRAIGHT/WEDM_OBROUND_STRAIGHT/WEDM_FREE_SHAPED_STRAIGHT) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in holeRectWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection roughSel = SelectRoughTool(width, toolCache, fg.Name, lw);
                    Tool roughTool;
                    toolCache.TryGetValue(roughSel.ToolName, out roughTool);
                    if (roughTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de desbaste '" + roughSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateOpenStyleRoughOperation(workPart, ftMillRoughFolder, noneGroup, roughTool, roughSel, fg, width, "HRR_", material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (desbaste de furo retangular): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 2 — ACABAMENTO DE FUNDO (genérico)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 2: ACABAMENTO DE FUNDO (genérico) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in widthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateFloorFinishOperation(workPart, ftFloorFinishFolder, method, finishTool, finishSel, fg, width, material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (acabamento de fundo): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 2b — ACABAMENTO DE FUNDO (pocket retangular - FLOOR_FACING)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 2b: ACABAMENTO DE FUNDO DE POCKETS (FLOOR_FACING) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketFloorFacingOperation(workPart, ftFloorFinishFolder, noneGroup, finishTool, finishSel, fg, width, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (floor facing de pocket): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 2c — ACABAMENTO DE FUNDO (pocket aberto - FLOOR_FACING)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 2c: ACABAMENTO DE FUNDO DE POCKET ABERTO ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketOpenWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketFloorFacingOperation(workPart, ftFloorFinishFolder, noneGroup, finishTool, finishSel, fg, width, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (floor facing de pocket aberto): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 3 — ACABAMENTO DE PAREDE (genérico)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 3: ACABAMENTO DE PAREDE (genérico) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in widthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreateWallFinishOperation(workPart, ftWallFinishFolder, method, finishTool, finishSel, fg, width, material, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (acabamento de parede): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 3b — ACABAMENTO DE PAREDE (pocket retangular - multi-stepover)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 3b: ACABAMENTO DE PAREDE DE POCKETS ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketWallFinishOperation(workPart, ftWallFinishFolder, noneGroup, finishTool, finishSel, fg, width, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (wall finish de pocket): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 3c — ACABAMENTO DE PAREDE (pocket aberto)
            // ══════════════════════════════════════════════════════════════
            lw.WriteLine("");
            lw.WriteLine("=== FASE 3c: ACABAMENTO DE PAREDE DE POCKET ABERTO ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in pocketOpenWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketWallFinishOperation(workPart, ftWallFinishFolder, noneGroup, finishTool, finishSel, fg, width, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (wall finish de pocket aberto): " + ex.Message);
                }
            }

            // ══════════════════════════════════════════════════════════════
            // FASE 3d — ACABAMENTO DE PAREDE (furo retangular)
            // ══════════════════════════════════════════════════════════════
            //
            // Também processa WEDM_RECTANGULAR_STRAIGHT, WEDM_OBROUND_STRAIGHT
            // e WEDM_FREE_SHAPED_STRAIGHT (ver FASE 1d) - sem acabamento de
            // fundo em nenhum dos casos (features vazadas).
            lw.WriteLine("");
            lw.WriteLine("=== FASE 3d: ACABAMENTO DE PAREDE DE FURO RETANGULAR (inclui WEDM_RECTANGULAR_STRAIGHT/WEDM_OBROUND_STRAIGHT/WEDM_FREE_SHAPED_STRAIGHT) ===");
            foreach (KeyValuePair<FeatureGeometryGroup, double> kv in holeRectWidthByGroup)
            {
                FeatureGeometryGroup fg = kv.Key;
                double width = kv.Value;
                try
                {
                    ToolSelection finishSel = SelectFinishTool(width, GetCornerRadius(cornerRadiusByGroup, fg), toolCache, fg.Name, lw);
                    Tool finishTool;
                    toolCache.TryGetValue(finishSel.ToolName, out finishTool);
                    if (finishTool == null)
                    {
                        lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta de acabamento '" + finishSel.ToolName + "' não encontrada. Pulado.");
                        continue;
                    }
                    CreatePocketWallFinishOperation(workPart, ftWallFinishFolder, noneGroup, finishTool, finishSel, fg, width, usedNames, lw);
                }
                catch (Exception ex)
                {
                    lw.WriteLine("ERRO em " + fg.Name + " (wall finish de furo retangular): " + ex.Message);
                }
            }

            // Nenhuma fase de furação "simples" é executada aqui (spot
            // drill, peck drill, furo cônico). FG_STEP1HOLE, FG_STEP1POCKET,
            // FG_HOLE* redondo e roscas não são usinados por este script -
            // apenas as figuras de fresagem acima e, agora, a coluna de
            // molde da FASE 5 abaixo.

            // ── includeMoldColumnAndCounterbore: quando false (FBM_MILLING_ONLY.cs),
            // as FASES 5 e 6 inteiras são puladas - zero operação em
            // FG_STEP2HOLE, só as figuras de fresagem processadas acima. ──
            if (!includeMoldColumnAndCounterbore)
            {
                lw.WriteLine("");
                lw.WriteLine("=== FASE 5/6 PULADAS (modo somente fresagem - includeMoldColumnAndCounterbore = false) ===");
                lw.WriteLine("FG_STEP2HOLE encontrados mas NÃO processados: " + moldColumnGroups.Count + " coluna(s) de molde + " + counterboreGroups.Count + " counterbore(s) comum(ns). Rode o FBM completo (ou o ciclo combinado) se quiser essas operações também.");
            }
            else
            {

                // ══════════════════════════════════════════════════════════════
                // FASE 5 — COLUNA DE MOLDE (FG_STEP2HOLE com DIAMETER_1 >= 38mm)
                // ══════════════════════════════════════════════════════════════
                //
                // Processo integrado dos 5 programas de referência do usuário
                // (USINAGEM_FIGURA_COLUNA_OP_01..05, orquestrados originalmente
                // pelo WIZARD_CBORE_MOLD): pré-furação profunda, desbaste
                // cilíndrico dos dois diâmetros do degrau (cabeça e haste),
                // acabamento do diâmetro maior (endmill) e mandrilhamento de
                // precisão do diâmetro menor (furo pra bucha/pino guia).
                //
                // Só roda pra FG_STEP2HOLE com DIAMETER_1 >= 38mm (coluna de
                // molde) - identificados durante a classificação acima e
                // acumulados em moldColumnGroups. Counterbore comum (< 38mm)
                // NÃO passa por aqui - é a FASE 6 (mais abaixo) que cuida desse
                // caso, complementar a esta.
                //
                // ASSUNÇÃO: ferramentas e parâmetros de corte são os mesmos dos
                // 5 programas de referência (HSS_DRILL_D25, CUTTER_D25_R.8,
                // ENDMILL_D10MM, UGT0333_007 / RPM e feed fixos) - não escalam
                // por material nem por diâmetro medido, igual ao processo
                // original. Se quiser que passem a escalar (ex.: pelo Vc do
                // material selecionado), me avisa que eu ajusto.
                lw.WriteLine("");
                lw.WriteLine("=== FASE 5: COLUNA DE MOLDE (FG_STEP2HOLE >= " + MOLD_COLUMN_MIN_DIAMETER.ToString("0.###", CultureInfo.InvariantCulture) + "mm) ===");
                NCGroup moldColumnFolder = FindOrCreateProgramFolder(workPart, programGroup, "FT_MOLD_COLUMN", usedNames, lw);
                int moldColumnProcessed = 0;
                foreach (FeatureGeometryGroup fg in moldColumnGroups)
                {
                    try
                    {
                        Tool drillTool, roughCutterTool, finishEndmillTool, boringTool;
                        toolCache.TryGetValue("HSS_DRILL_D25", out drillTool);
                        toolCache.TryGetValue("CUTTER_D25_R.8", out roughCutterTool);
                        toolCache.TryGetValue("ENDMILL_D10MM", out finishEndmillTool);
                        toolCache.TryGetValue("BORE_BAR_DIAM_40", out boringTool);
                        if (drillTool == null || roughCutterTool == null || finishEndmillTool == null || boringTool == null)
                        {
                            lw.WriteLine("AVISO: " + fg.Name + " -> uma ou mais ferramentas da coluna de molde não encontradas na biblioteca"
                                + " (HSS_DRILL_D25/CUTTER_D25_R.8/ENDMILL_D10MM/UGT0333_007). Grupo pulado.");
                            continue;
                        }

                        CreateMoldColumnDeepHoleOperation(workPart, moldColumnFolder, noneGroup, drillTool, fg, usedNames, lw);
                        CreateMoldColumnCylinderRoughOperation(workPart, moldColumnFolder, noneGroup, roughCutterTool, fg, "FACES_CYLINDER_2", "MCR2", usedNames, lw);
                        CreateMoldColumnCylinderRoughOperation(workPart, moldColumnFolder, noneGroup, roughCutterTool, fg, "FACES_CYLINDER_1", "MCR1", usedNames, lw);
                        CreateMoldColumnFinishOperation(workPart, moldColumnFolder, noneGroup, finishEndmillTool, fg, usedNames, lw);
                        CreateMoldColumnBoringOperation(workPart, moldColumnFolder, noneGroup, boringTool, fg, usedNames, lw);

                        moldColumnProcessed++;
                    }
                    catch (Exception ex)
                    {
                        lw.WriteLine("ERRO em " + fg.Name + " (coluna de molde): " + ex.Message);
                    }
                }
                lw.WriteLine("Colunas de molde processadas: " + moldColumnProcessed + " de " + moldColumnGroups.Count);

                // ══════════════════════════════════════════════════════════════
                // FASE 6 — COUNTERBORE COMUM (FG_STEP2HOLE com DIAMETER_1 < 38mm)
                // ══════════════════════════════════════════════════════════════
                //
                // Antes era o journal separado AUTOMATIC_COUNTERBORE.cs, que
                // precisava ser rodado à parte no NX e não estava sendo
                // acionado. Agora roda aqui mesmo, como a última fase de
                // usinagem, pros grupos que a classificação acima separou em
                // counterboreGroups (complemento da FASE 5: tudo que não é
                // coluna de molde). Ferramenta é montada pelo diâmetro medido
                // (CBORE_<diametro>MM) e RPM/feed vêm do DrillVc do material
                // escolhido no início.
                lw.WriteLine("");
                lw.WriteLine("=== FASE 6: COUNTERBORE COMUM (FG_STEP2HOLE < " + MOLD_COLUMN_MIN_DIAMETER.ToString("0.###", CultureInfo.InvariantCulture) + "mm) ===");
                Dictionary<string, NCGroup> counterboreFolderCache = new Dictionary<string, NCGroup>();
                int counterboreProcessed = 0;
                foreach (KeyValuePair<FeatureGeometryGroup, double> kv in counterboreGroups)
                {
                    FeatureGeometryGroup fg = kv.Key;
                    double diameter1 = kv.Value;
                    try
                    {
                        string diameterLabel = diameter1.ToString("0.###", CultureInfo.InvariantCulture);
                        string toolName = CBORE_TOOL_PREFIX + diameterLabel + CBORE_TOOL_SUFFIX;
                        Tool cbTool;
                        if (!toolCache.TryGetValue(toolName, out cbTool))
                        {
                            lw.WriteLine("AVISO: " + fg.Name + " -> ferramenta '" + toolName + "' (DIAMETER_1 = " + diameterLabel + ") não encontrada na biblioteca. Grupo ignorado.");
                            continue;
                        }

                        string folderName = "CBORE_GROUP_" + diameterLabel + "MM";
                        NCGroup cbFolder;
                        if (!counterboreFolderCache.TryGetValue(folderName, out cbFolder))
                        {
                            cbFolder = FindOrCreateProgramFolder(workPart, programGroup, folderName, usedNames, lw);
                            counterboreFolderCache[folderName] = cbFolder;
                        }

                        CreateCounterboreOperation(workPart, cbFolder, noneGroup, cbTool, fg, diameterLabel, diameter1, material, usedNames, lw);
                        counterboreProcessed++;
                    }
                    catch (Exception ex)
                    {
                        lw.WriteLine("ERRO em " + fg.Name + " (counterbore): " + ex.Message);
                    }
                }
                lw.WriteLine("Counterbores processados: " + counterboreProcessed + " de " + counterboreGroups.Count);

            } // fim do if (includeMoldColumnAndCounterbore) - FASE 5/6

            // ══════════════════════════════════════════════════════════════
            // REGENERAÇÃO FINAL — recalcula TODAS as trajetórias depois que
            // todas as fases (0 a 6) já criaram todas as operações
            // ══════════════════════════════════════════════════════════════
            //
            // Cada Create*Operation já chama GenerateToolPath logo depois de
            // criar a própria operação, mas isso calcula a trajetória com o
            // IPW (in-process workpiece) do estado da peça NAQUELE momento
            // da execução - que não é necessariamente a ordem final do
            // programa (ex.: a FASE 5/6 de furação roda DEPOIS das fases de
            // fresagem no código, mas pode entrar ANTES delas na ordem do
            // programa/setup). Esse passo final força o NX a recalcular
            // TODAS as trajetórias de uma vez, já com todas as operações
            // existentes e na ordem final - evita trajetória desatualizada
            // por causa da ordem de criação.
            lw.WriteLine("");
            lw.WriteLine("=== REGENERAÇÃO FINAL DE TODAS AS TRAJETÓRIAS ===");
            try
            {
                Operation[] todasOperacoes = workPart.CAMSetup.CAMOperationCollection.ToArray();
                CAMObject[] todosOsObjetos = new CAMObject[todasOperacoes.Length];
                for (int i = 0; i < todasOperacoes.Length; i++)
                    todosOsObjetos[i] = (CAMObject)todasOperacoes[i];
                workPart.CAMSetup.GenerateToolPath(todosOsObjetos);
                lw.WriteLine("Trajetórias regeneradas: " + todosOsObjetos.Length + " operação(ões).");
            }
            catch (Exception ex)
            {
                lw.WriteLine("AVISO: falha ao regenerar todas as trajetórias no final: " + ex.Message
                    + ". As trajetórias individuais criadas por cada fase continuam válidas - só a recalculada final que não rodou.");
            }
        }
        catch (Exception ex)
        {
            lw.WriteLine("ERRO:");
            lw.WriteLine(ex.ToString());
        }
        finally
        {
            ListingWindow realLw = theSession.ListingWindow;
            realLw.Open();
            foreach (string line in lw.Lines)
                realLw.WriteLine(line);
        }
    }

    private class LogBuffer
    {
        public List<string> Lines = new List<string>();
        public void WriteLine(string text) { Lines.Add(text); }
    }

    // ══════════════════════════════════════════════════════════════════════
    // PARÂMETROS DE CORTE - FRESAGEM
    // ══════════════════════════════════════════════════════════════════════
    // Vc (velocidade de corte) agora vem da MaterialCuttingData escolhida no
    // início da execução (ver tabela de materiais acima) - não é mais fixo.
    // Avanço por rotação continua fixo por enquanto (ver comentário na
    // tabela de materiais).
    private const double ENDMILL_ROUGH_FEED_PER_REV = 0.3;
    private const double ENDMILL_FINISH_FEED_PER_REV = 0.15;
    private const double CUTTER_ROUGH_FEED_PER_REV = 0.6;
    private const double CUTTER_FINISH_FEED_PER_REV = 0.3;

    private class ToolSelection
    {
        public string ToolName;
        public double Diameter;
        public bool IsCutter;
    }

    private static void CalculateMillingParameters(ToolSelection sel, bool isRough, MaterialCuttingData material, out double rpm, out double feed)
    {
        double vc = sel.IsCutter ? material.CutterVc : (isRough ? material.EndmillRoughVc : material.EndmillFinishVc);
        double feedPerRev = sel.IsCutter
            ? (isRough ? CUTTER_ROUGH_FEED_PER_REV : CUTTER_FINISH_FEED_PER_REV)
            : (isRough ? ENDMILL_ROUGH_FEED_PER_REV : ENDMILL_FINISH_FEED_PER_REV);

        rpm = (vc * 318.0) / sel.Diameter;
        feed = rpm * feedPerRev;
    }

    // ── Regra de desbaste: a fresa precisa ser MENOR que a largura/
    // comprimento da figura (senão não usina - não cabe/não entra), mas nem
    // pequena demais (senão fica ineficiente). Faixa: diâmetro entre 1/2 e
    // 3/5 da largura da figura.
    //
    // CORREÇÃO: o máximo era 3/4 (0.75), o que deixava o CUTTER_D40_R1
    // "caber" matematicamente em pockets de 60/62mm (0.75x60=45,
    // 0.75x62=46.5 - 40 entra nos dois) e ser escolhido como ideal. Na
    // prática o D40 é grande demais pra esses pockets - o usuário reportou
    // e pediu pra usar um cabeçote menor (D25) nesses casos. Reduzido pra
    // 0.6 (3/5): agora o D40 só vira ideal a partir de ~67mm de largura
    // (40 / 0.6), e pockets de 60/62mm caem pro D25 (maior ferramenta que
    // ainda cabe em 0.6x da largura). Se 67mm não for exatamente o corte
    // certo pra quando o D40 deve entrar, me passa o valor certo que eu
    // ajusto o fator. ──
    private const double ROUGH_WIDTH_MIN_FACTOR = 0.5;
    private const double ROUGH_WIDTH_MAX_FACTOR = 0.6;

    // ── Regra de acabamento: a fresa é escolhida DIRETO pelo raio de canto
    // (não pela largura) - diâmetro = 2x o raio de canto, arredondado pra
    // baixo pra ferramenta disponível mais próxima. Ex.: raio 8 -> D16;
    // raio 5 -> D10 no máximo. ──
    private const double CORNER_RADIUS_FP_EPSILON = 0.001; // só tolerância de ponto flutuante, não é folga mecânica

    // ── Renomeada de "RoughToolTable" pra "DefaultRoughToolTable" - agora é
    // só o FALLBACK usado quando o banco (ToolDatabase.cs / SQL Server) não
    // estiver acessível. Ver GetRoughToolTable() logo abaixo (mesmo padrão
    // de GetMaterialTable() lá em cima). ──
    private static readonly ToolSelection[] DefaultRoughToolTable =
    {
        new ToolSelection { ToolName = "ENDMILL_D5MM", Diameter = 5.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D8MM", Diameter = 8.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D12MM", Diameter = 12.0, IsCutter = false },
        new ToolSelection { ToolName = "CUTTER_D16_R.8", Diameter = 16.0, IsCutter = true },
        new ToolSelection { ToolName = "CUTTER_D25_R.8", Diameter = 25.0, IsCutter = true },
        new ToolSelection { ToolName = "CUTTER_D40_R1", Diameter = 40.0, IsCutter = true },
    };

    private static ToolSelection[] _roughToolTableCache;

    private static ToolSelection[] GetRoughToolTable(Action<string> log)
    {
        if (_roughToolTableCache != null)
            return _roughToolTableCache;

        try
        {
            List<ToolDatabase.ToolRow> rows = ToolDatabase.LoadRoughTools(log);
            if (rows != null && rows.Count > 0)
            {
                List<ToolSelection> lista = new List<ToolSelection>();
                foreach (ToolDatabase.ToolRow r in rows)
                    lista.Add(new ToolSelection { ToolName = r.ToolName, Diameter = r.Diameter, IsCutter = r.IsCutter });
                _roughToolTableCache = lista.ToArray();
                return _roughToolTableCache;
            }
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: falha lendo ferramentas de desbaste do banco (" + ex.Message + ") - usando tabela fixa interna.");
        }

        _roughToolTableCache = DefaultRoughToolTable;
        return _roughToolTableCache;
    }

    // Escolhe, dentro da tabela de desbaste, a MAIOR ferramenta cujo
    // diâmetro esteja entre 1/2 e 3/5 da largura (mais material removido
    // por passe, sem estourar a regra de "não cabe") E que EXISTA na
    // biblioteca de ferramentas da peça (toolCache) - antes essa checagem
    // de disponibilidade não existia aqui: a função devolvia a ferramenta
    // "ideal" pela tabela mesmo que ela não estivesse na biblioteca, e a
    // operação inteira era pulada mais adiante (upstream, no Run) com só um
    // aviso genérico de "ferramenta não encontrada" - por isso um pocket de
    // 65/66mm de largura (faixa ideal 32.5-49.5mm, só o D40 cai nela)
    // simplesmente não gerava operação nenhuma quando CUTTER_D40_R1 não
    // estava na biblioteca, em vez de cair pro D25 (que existe). Agora a
    // disponibilidade entra na conta em cada etapa da degradação:
    // 1. maior ferramenta ideal (1/2 a 3/5) que esteja na biblioteca;
    // 2. se nada ideal estiver disponível, maior ferramenta <= 3/5 que
    //    esteja na biblioteca (abaixo do ideal, mas ainda cabe);
    // 3. só como último recurso, a menor ferramenta disponível mesmo que
    //    ultrapasse 3/5 da largura, com aviso bem claro.
    private static ToolSelection SelectRoughTool(double width, Dictionary<string, Tool> toolCache, string context, LogBuffer lw)
    {
        ToolSelection[] roughToolTable = GetRoughToolTable(lw.WriteLine);
        double diametroMin = width * ROUGH_WIDTH_MIN_FACTOR;
        double diametroMax = width * ROUGH_WIDTH_MAX_FACTOR;

        // 1. Maior ferramenta dentro da faixa ideal E disponível na biblioteca.
        ToolSelection melhorTeorico = null;
        ToolSelection melhorDisponivel = null;
        foreach (ToolSelection candidata in roughToolTable)
        {
            if (candidata.Diameter >= diametroMin && candidata.Diameter <= diametroMax)
            {
                if (melhorTeorico == null || candidata.Diameter > melhorTeorico.Diameter)
                    melhorTeorico = candidata;
                if (toolCache.ContainsKey(candidata.ToolName) && (melhorDisponivel == null || candidata.Diameter > melhorDisponivel.Diameter))
                    melhorDisponivel = candidata;
            }
        }
        if (melhorDisponivel != null)
        {
            if (melhorTeorico != null && melhorTeorico.ToolName != melhorDisponivel.ToolName)
                lw.WriteLine("AVISO: " + context + " (desbaste) -> ferramenta ideal '" + melhorTeorico.ToolName
                    + "' (D" + melhorTeorico.Diameter.ToString("0.###", CultureInfo.InvariantCulture)
                    + "mm) não encontrada na biblioteca. Usando '" + melhorDisponivel.ToolName
                    + "' - ainda dentro da faixa de 1/2 a 3/5 da largura.");
            return melhorDisponivel;
        }

        // 2. Nada ideal disponível - maior ferramenta <= 3/5 da largura que
        // esteja na biblioteca (abaixo do ideal, mas ainda cabe).
        ToolSelection maiorDentroDoMaximo = null;
        foreach (ToolSelection candidata in roughToolTable)
        {
            if (candidata.Diameter <= diametroMax && toolCache.ContainsKey(candidata.ToolName))
            {
                if (maiorDentroDoMaximo == null || candidata.Diameter > maiorDentroDoMaximo.Diameter)
                    maiorDentroDoMaximo = candidata;
            }
        }
        if (maiorDentroDoMaximo != null)
        {
            lw.WriteLine("AVISO: " + context + " (desbaste) -> nenhuma ferramenta disponível na biblioteca entre "
                + diametroMin.ToString("0.###", CultureInfo.InvariantCulture) + "mm e "
                + diametroMax.ToString("0.###", CultureInfo.InvariantCulture) + "mm (largura "
                + width.ToString("0.###", CultureInfo.InvariantCulture) + "mm). Usando '" + maiorDentroDoMaximo.ToolName
                + "' - abaixo do ideal (1/2 da largura), mas ainda cabe dentro de 3/5 e está na biblioteca.");
            return maiorDentroDoMaximo;
        }

        // 3. Último recurso: menor ferramenta disponível na biblioteca,
        // mesmo ultrapassando 3/5 da largura.
        ToolSelection menorDisponivel = null;
        foreach (ToolSelection candidata in roughToolTable)
        {
            if (toolCache.ContainsKey(candidata.ToolName) && (menorDisponivel == null || candidata.Diameter < menorDisponivel.Diameter))
                menorDisponivel = candidata;
        }
        if (menorDisponivel != null)
        {
            lw.WriteLine("AVISO: " + context + " (desbaste) -> mesmo a menor ferramenta disponível na biblioteca ('" + menorDisponivel.ToolName + "', D"
                + menorDisponivel.Diameter.ToString("0.###", CultureInfo.InvariantCulture) + "mm) ultrapassa 3/5 da largura ("
                + width.ToString("0.###", CultureInfo.InvariantCulture) + "mm). Usando ela mesmo assim - pode não usinar corretamente, revisar manualmente.");
            return menorDisponivel;
        }

        // 4. Nenhuma ferramenta da tabela de desbaste (D5/D8/D12/D16/D25/D40)
        // está na biblioteca - devolve a menor da tabela mesmo assim (o
        // Run() upstream vai avisar "não encontrada" e pular a operação,
        // mas pelo menos o motivo fica claro aqui, não só um aviso genérico).
        ToolSelection menor = roughToolTable[0];
        foreach (ToolSelection candidata in roughToolTable)
        {
            if (candidata.Diameter < menor.Diameter)
                menor = candidata;
        }
        lw.WriteLine("AVISO: " + context + " (desbaste) -> NENHUMA ferramenta de desbaste da tabela (D5/D8/D12/D16/D25/D40) foi encontrada na biblioteca. Usando '"
            + menor.ToolName + "' como último recurso - a operação provavelmente será pulada mais adiante.");
        return menor;
    }

    // ── Renomeada de "FinishToolCandidatesAscending" pra
    // "DefaultFinishToolTable" - agora é só o FALLBACK usado quando o
    // banco não estiver acessível (mesmo padrão de DefaultRoughToolTable
    // acima). A antiga "FinishToolFallbackChain" (mesma lista, ordem
    // DECRESCENTE) foi removida como tabela separada - agora é derivada
    // na hora, em SelectFinishTool(), a partir desta mesma lista (ordenada
    // ao contrário) - assim as duas nunca podem divergir entre si (o que
    // já era um risco real: se alguém editasse uma tabela sem lembrar da
    // outra, elas ficariam com ferramentas diferentes). ──
    private static readonly ToolSelection[] DefaultFinishToolTable =
    {
        new ToolSelection { ToolName = "ENDMILL_D10MM", Diameter = 10.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D12MM", Diameter = 12.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D14MM", Diameter = 14.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D16MM", Diameter = 16.0, IsCutter = false },
        new ToolSelection { ToolName = "ENDMILL_D18MM", Diameter = 18.0, IsCutter = false },
    };

    private static ToolSelection[] _finishToolTableCache;

    private static ToolSelection[] GetFinishToolTable(Action<string> log)
    {
        if (_finishToolTableCache != null)
            return _finishToolTableCache;

        try
        {
            List<ToolDatabase.ToolRow> rows = ToolDatabase.LoadFinishTools(log);
            if (rows != null && rows.Count > 0)
            {
                List<ToolSelection> lista = new List<ToolSelection>();
                foreach (ToolDatabase.ToolRow r in rows)
                    lista.Add(new ToolSelection { ToolName = r.ToolName, Diameter = r.Diameter, IsCutter = r.IsCutter });
                _finishToolTableCache = lista.ToArray();
                return _finishToolTableCache;
            }
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: falha lendo ferramentas de acabamento do banco (" + ex.Message + ") - usando tabela fixa interna.");
        }

        _finishToolTableCache = DefaultFinishToolTable;
        return _finishToolTableCache;
    }

    // Acabamento: se o raio de canto da figura é conhecido, a ferramenta é
    // escolhida DIRETO por ele (diâmetro = até 2x o raio, ex.: raio 8 ->
    // D16, raio 5 -> D10 no máximo) - a largura não entra na conta. Se o
    // raio de canto não foi identificado pra esse grupo, cai pro critério
    // antigo por largura (fallback).
    private static ToolSelection SelectFinishTool(double width, double? cornerRadius, Dictionary<string, Tool> toolCache, string context, LogBuffer lw)
    {
        if (cornerRadius.HasValue)
            return SelectFinishToolByCornerRadius(cornerRadius.Value, toolCache, context, lw);

        if (width <= 30.0)
            return new ToolSelection { ToolName = "ENDMILL_D10MM", Diameter = 10.0, IsCutter = false };

        // Cadeia de fallback = tabela de acabamento em ordem DECRESCENTE de
        // diâmetro (maior primeiro) - derivada aqui da mesma tabela usada
        // por SelectFinishToolByCornerRadius, em vez de uma lista separada
        // (ver comentário em DefaultFinishToolTable acima).
        ToolSelection[] finishToolTable = GetFinishToolTable(lw.WriteLine);
        ToolSelection[] fallbackChain = (ToolSelection[])finishToolTable.Clone();
        Array.Sort(fallbackChain, (a, b) => b.Diameter.CompareTo(a.Diameter));

        foreach (ToolSelection candidate in fallbackChain)
        {
            if (toolCache.ContainsKey(candidate.ToolName))
            {
                if (candidate.ToolName != fallbackChain[0].ToolName)
                    lw.WriteLine("AVISO: " + context + " -> " + fallbackChain[0].ToolName
                        + " não encontrada, usando fallback '" + candidate.ToolName + "'.");
                return candidate;
            }
        }
        return fallbackChain[0];
    }

    private static ToolSelection SelectFinishToolByCornerRadius(double cornerRadius, Dictionary<string, Tool> toolCache, string context, LogBuffer lw)
    {
        ToolSelection[] finishToolTable = GetFinishToolTable(lw.WriteLine);
        double diametroIdeal = cornerRadius * 2.0;

        ToolSelection ideal = null;
        foreach (ToolSelection candidata in finishToolTable)
        {
            if (candidata.Diameter <= diametroIdeal + CORNER_RADIUS_FP_EPSILON)
            {
                if (ideal == null || candidata.Diameter > ideal.Diameter)
                    ideal = candidata;
            }
        }

        if (ideal == null)
        {
            ToolSelection menor = finishToolTable[0];
            lw.WriteLine("AVISO: " + context + " (acabamento) -> raio de canto de " + cornerRadius.ToString("0.###", CultureInfo.InvariantCulture)
                + "mm é menor que a metade da menor ferramenta de acabamento disponível ('" + menor.ToolName + "', D"
                + menor.Diameter.ToString("0.###", CultureInfo.InvariantCulture) + "mm). Usando ela mesmo assim - pode sobrar material no canto.");
            return menor;
        }

        if (toolCache.ContainsKey(ideal.ToolName))
            return ideal;

        // Ferramenta ideal (diâmetro = 2x o raio) não está na biblioteca -
        // procura a maior alternativa disponível que ainda caiba no raio.
        ToolSelection alternativaDisponivel = null;
        foreach (ToolSelection candidata in finishToolTable)
        {
            if (candidata.Diameter <= diametroIdeal + CORNER_RADIUS_FP_EPSILON && toolCache.ContainsKey(candidata.ToolName))
            {
                if (alternativaDisponivel == null || candidata.Diameter > alternativaDisponivel.Diameter)
                    alternativaDisponivel = candidata;
            }
        }

        if (alternativaDisponivel != null)
        {
            lw.WriteLine("AVISO: " + context + " (acabamento) -> ferramenta ideal '" + ideal.ToolName + "' (raio de canto "
                + cornerRadius.ToString("0.###", CultureInfo.InvariantCulture) + "mm -> D"
                + ideal.Diameter.ToString("0.###", CultureInfo.InvariantCulture) + ") não encontrada na biblioteca. Usando '"
                + alternativaDisponivel.ToolName + "'.");
            return alternativaDisponivel;
        }

        lw.WriteLine("AVISO: " + context + " (acabamento) -> ferramenta ideal '" + ideal.ToolName + "' pro raio de canto de "
            + cornerRadius.ToString("0.###", CultureInfo.InvariantCulture)
            + "mm não está na biblioteca e não há alternativa menor disponível. Mantendo '" + ideal.ToolName + "' - operação pode ser pulada na fase se ela realmente não existir.");
        return ideal;
    }

    private static double? GetCornerRadius(Dictionary<FeatureGeometryGroup, double> cornerRadiusByGroup, FeatureGeometryGroup fg)
    {
        double valor;
        if (cornerRadiusByGroup.TryGetValue(fg, out valor))
            return valor;
        return null;
    }

    // ── ASSUNÇÃO: tabela de facemill por largura. O script original só usava
    // FACEMILL_D64MM fixo - se você tiver outros diâmetros na biblioteca,
    // ajuste os nomes/limites aqui. ──
    private static ToolSelection SelectFacemillTool(double width)
    {
        if (width <= 40.0) return new ToolSelection { ToolName = "FACEMILL_D32MM", Diameter = 32.0, IsCutter = true };
        if (width <= 60.0) return new ToolSelection { ToolName = "FACEMILL_D50MM", Diameter = 50.0, IsCutter = true };
        return new ToolSelection { ToolName = "FACEMILL_D64MM", Diameter = 64.0, IsCutter = true };
    }

    // ══════════════════════════════════════════════════════════════════════
    // NOME DAS OPERAÇÕES — curto, mas identificável
    // ══════════════════════════════════════════════════════════════════════
    //
    // Padrão: "<ESTRATEGIA>_<FIGURA>_D<diametro da FRESA usada>". O diâmetro
    // é sempre o da ferramenta de corte selecionada pra aquela operação
    // (sel.Diameter), não o tamanho da figura - é isso que ajuda a
    // identificar rapidamente qual fresa rodou em cada operação.
    //
    // Código de <ESTRATEGIA> (por função Create*Operation):
    //   RG  = desbaste genérico          FF  = acabamento de fundo genérico
    //   WF  = acabamento de parede gen.  PKR = desbaste de pocket
    //   PKF = fundo de pocket            PKW = parede de pocket
    //   POR = desbaste de pocket aberto  HRR = desbaste de furo retangular
    //   FCR = faceamento desbaste        FCA = faceamento acabamento
    //
    // Código de <FIGURA> (ShortTag de cada item do FeatureTypeRegistry, ver
    // seção "REGISTRO DE TIPOS DE FEATURE" mais acima): HRET, PKTR, POBR,
    // SLTP, POPN, SPLR, SPLX, SPLN, STP2, WEDM (novo). O sufixo numérico
    // que o NX dá ao grupo (ex.: "_3") é preservado pra rastrear até o
    // grupo de origem.
    private static string ShortFeatureTag(string name)
    {
        FeatureTypeSpec spec = FindFeatureTypeSpec(name);
        if (spec == null || spec.ShortTag == null)
        {
            // Prefixo desconhecido, ou reconhecido só pra agrupar (sem
            // ShortTag definido ainda, ver fim do FeatureTypeRegistry) -
            // mantém o nome original como fallback pra não perder
            // rastreabilidade (mesmo comportamento de hoje pra prefixo
            // desconhecido).
            return name;
        }

        string sufixo = name.Substring(spec.GroupNamePrefix.Length).TrimStart('_');
        return string.IsNullOrEmpty(sufixo) ? spec.ShortTag : spec.ShortTag + "_" + sufixo;
    }

    // ══════════════════════════════════════════════════════════════════════
    // FAMÍLIA GENÉRICA — DESBASTE / ACABAMENTO DE FUNDO / ACABAMENTO DE PAREDE
    // (Obround, Slot, Pocket Aberto, Superfície Planar, Superfície Planar
    // Redonda)
    // ══════════════════════════════════════════════════════════════════════
    private static void CreateRoughOperation(
        Part workPart, NCGroup nCGroup1, Method method, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, MaterialCuttingData material, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "RG_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, method, tool, featureGroup,
            "mill_planar", "FLOOR_WALL",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.0;
        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        builder.BndStepover.PercentToolFlatBuilder.Value = 80.0;
        builder.CutParameters.WallStock.Value = 0.25;
        builder.CutParameters.IpwType = CutParametersIpwTypes.ThreeDimension;
        builder.CutParameters.ExtendFloorTo = CutParametersExtendFloorTypes.None;
        builder.DepthPerCut.Value = 1.0;

        double rpm, feed;
        CalculateMillingParameters(sel, true, material, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Desbaste criado para: " + featureGroup.Name + " (ferramenta " + tool.Name + ")");

        builder.Destroy();
    }

    private static void CreateFloorFinishOperation(
        Part workPart, NCGroup nCGroup1, Method method, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, MaterialCuttingData material, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "FF_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, method, tool, featureGroup,
            "mill_planar", "FLOOR_WALL",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutParameters.IpwType = CutParametersIpwTypes.Thickness;
        builder.CutParameters.ExtendFloorTo = CutParametersExtendFloorTypes.None;
        builder.CutParameters.BlankDistance.Value = 0.25;
        builder.WallBlankThickness.Value = 0.25;
        builder.BndStepover.PercentToolFlatBuilder.Value = 50.0;

        double rpm, feed;
        CalculateMillingParameters(sel, false, material, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Acabamento de fundo criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreateWallFinishOperation(
        Part workPart, NCGroup nCGroup1, Method method, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, MaterialCuttingData material, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "WF_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            nCGroup1, method, tool, featureGroup,
            "mill_planar", "WALL_PROFILING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutParameters.IpwType = CutParametersIpwTypes.ThreeDimension;
        builder.CutParameters.CutWallsOnly = true;

        double rpm, feed;
        CalculateMillingParameters(sel, false, material, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Acabamento de parede criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // FAMÍLIA POCKET RETANGULAR — POCKETING / FLOOR_FACING / WALL (multi-stepover)
    // ══════════════════════════════════════════════════════════════════════
    private static void CreatePocketRoughOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, MaterialCuttingData material, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "PKR_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "mill_planar", "POCKETING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutParameters.FloorStock.Value = 0.25;
        builder.CutParameters.WallStock.Value = 0.5;
        builder.DepthPerCut.Value = 1.0;

        double rpm, feed;
        CalculateMillingParameters(sel, true, material, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        builder.NonCuttingBuilder.EngageOpenAreaBuilder.EngRetType = NcmPlanarEngRetBuilder.EngRetTypes.Linear;
        builder.NonCuttingBuilder.EngageOpenAreaBuilder.RampAngle = 1.0;
        builder.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NcmPlanarEngRetBuilder.EngRetTypes.RampOnShape;
        builder.NonCuttingBuilder.EngageClosedAreaBuilder.HelicalRampAngleBuilder.Value = 1.0;
        builder.NonCuttingBuilder.EngageClosedAreaBuilder.HeightBuilder.Value = 1.0;
        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        builder.CutParameters.UseToolHolder = true;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Pocket rough criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreatePocketFloorFacingOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "PKF_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "mill_planar", "FLOOR_FACING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutPattern.CutPattern = CutPatternBuilder.Types.FollowPeriphery;
        builder.CutParameters.IpwType = CutParametersIpwTypes.Thickness;
        builder.CutParameters.ExtendFloorTo = CutParametersExtendFloorTypes.None;
        builder.CutParameters.BlankDistance.Value = 0.25;
        builder.BndStepover.PercentToolFlatBuilder.Value = 25.0;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 2000.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 1000.0;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Pocket floor facing criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreatePocketWallFinishOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "PKW_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "mill_planar", "WALL_PROFILING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.BndStepover.StepoverType = StepoverBuilder.StepoverTypes.Multiple;
        MultipleStepoverBuilder multipleStepover = builder.BndStepover.MultipleBuilder;
        multipleStepover.Modify(0, 1, 0.0, 0);
        multipleStepover.Modify(0, 1, 0.25, 0);

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1900.0;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        builder.CutParameters.UseToolHolder = true;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Pocket wall finish criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // FAMÍLIA FACEAMENTO — Superfície Planar Retangular (facemill, ZigZag)
    // ══════════════════════════════════════════════════════════════════════
    private static void CreateFacingRoughOperation(
        Part workPart, NCGroup programGroup, Method method, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "FCR_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, method, tool, featureGroup,
            "mill_planar", "FLOOR_WALL",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutPattern.CutPattern = CutPatternBuilder.Types.ZigZag;
        builder.CutParameters.IpwType = CutParametersIpwTypes.Thickness;
        builder.CutParameters.ExtendFloorTo = CutParametersExtendFloorTypes.None;
        builder.CutParameters.BlankDistance.Value = 2.0;
        builder.DepthPerCut.Value = 0.5;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1200.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 1000.0;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Faceamento rough criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreateFacingFinishOperation(
        Part workPart, NCGroup programGroup, Method method, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "FCA_" + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, method, tool, featureGroup,
            "mill_planar", "FLOOR_WALL",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutPattern.CutPattern = CutPatternBuilder.Types.ZigZag;
        builder.CutParameters.IpwType = CutParametersIpwTypes.Thickness;
        builder.CutParameters.ExtendFloorTo = CutParametersExtendFloorTypes.None;
        builder.CutParameters.BlankDistance.Value = 0.5;
        builder.DepthPerCut.Value = 0.5;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1200.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 500.0;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Faceamento finish criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ROUGH "ESTILO ABERTO" — compartilhado por Pocket Aberto e Furo
    // Retangular (mesma configuração de builder nos dois scripts originais:
    // FollowPart + ZigZag + engajamento linear com Length/Height/MinClearance)
    // ══════════════════════════════════════════════════════════════════════
    private static void CreateOpenStyleRoughOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool, ToolSelection sel,
        FeatureGeometryGroup featureGroup, double width, string operationPrefix, MaterialCuttingData material,
        Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, operationPrefix + ShortFeatureTag(featureGroup.Name) + "_D" + sel.Diameter.ToString("0.###", CultureInfo.InvariantCulture));
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "mill_planar", "POCKETING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        VolumeBased25DMillingOperation millingOp = (VolumeBased25DMillingOperation)operation;
        VolumeBased25DMillingOperationBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(millingOp);

        builder.CutParameters.FloorStock.Value = 0.25;
        builder.CutParameters.WallStock.Value = 0.25;
        builder.CutPattern.CutPattern = CutPatternBuilder.Types.FollowPart;
        builder.BndStepover.PercentToolFlatBuilder.Value = 50.0;
        builder.DepthPerCut.Value = 0.5;

        double rpm, feed;
        CalculateMillingParameters(sel, true, material, out rpm, out feed);
        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        builder.CutParameters.TraverseOpenPasses = CutParametersTraverseOpenPassesTypes.ZigZag;

        builder.NonCuttingBuilder.EngageOpenAreaBuilder.EngRetType = NcmPlanarEngRetBuilder.EngRetTypes.Linear;
        builder.NonCuttingBuilder.EngageOpenAreaBuilder.LengthBuilder.Value = 5.0;
        builder.NonCuttingBuilder.EngageOpenAreaBuilder.HeightBuilder.Value = 1.0;
        builder.NonCuttingBuilder.EngageOpenAreaBuilder.MinClearanceBuilder.Value = 2.0;

        builder.CutParameters.IpwType = CutParametersIpwTypes.ThreeDimension;

        builder.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NcmPlanarEngRetBuilder.EngRetTypes.SameAsEngage;
        builder.NonCuttingBuilder.RetractAreaBuilder.EngRetType = NcmPlanarEngRetBuilder.EngRetTypes.None;

        builder.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NcmTransfer.TransferTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
        builder.NonCuttingBuilder.TransferWithinLevelsType = NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
        builder.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        builder.CutParameters.UseToolHolder = true;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Rough (estilo aberto) criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // COLUNA DE MOLDE — FG_STEP2HOLE grande (DIAMETER_1 >= MOLD_COLUMN_MIN_DIAMETER)
    // ══════════════════════════════════════════════════════════════════════
    //
    // Porta direta dos 5 programas de referência do usuário
    // (USINAGEM_FIGURA_COLUNA_OP_01..05.cs) pra dentro deste script único.
    // Ferramentas e parâmetros de corte (RPM, feed, stock etc.) são
    // exatamente os mesmos dos originais - fixos, não escalam por material
    // nem por diâmetro medido.
    private static void CreateMoldColumnDeepHoleOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool,
        FeatureGeometryGroup featureGroup, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        // OP_01 de referência: pré-furação profunda (broca HSS D25) na
        // profundidade cheia da feature (UseModelDepth = true), sem stepover
        // axial - fura o furo em degrau inteiro antes do desbaste cilíndrico.
        string operationName = MakeUniqueOperationName(usedNames, "MCP_" + ShortFeatureTag(featureGroup.Name) + "_D25");
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "hole_making", "DEEP_HOLE_DRILLING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);

        builder.CycleTable.MotionOutput = Cycle.MotionOutputTypes.MachineCycle;
        builder.CycleTable.AxialStepover.StepoverType = StepoverBuilder.StepoverTypes.None;
        builder.ControlPointOffset = HoleDrillingBuilder.ControlPointOffsetType.Feature;
        builder.IntersectionStrategy = HoleDrillingBuilder.IntersectionStrategyType.Ipw;

        NXOpen.CAM.FBM.FeatureGeometry featureGeometry = builder.GetFeatureGeometry();
        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningGeometry = (NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry;
        machiningGeometry.UseModelDepth = true;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1000.0;

        builder.CycleTable.FirstCutMode = Cycle.CutMode.Percentage;
        builder.CycleTable.LastCutMode = Cycle.CutMode.Percentage;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Pré-furação (coluna de molde) criada para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreateMoldColumnCylinderRoughOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool,
        FeatureGeometryGroup featureGroup, string machiningArea, string opCode,
        Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        // OP_02/OP_03 de referência: desbaste cilíndrico (HOLE_MILLING,
        // CylinderMilling circular) com fresa CUTTER_D25_R.8, 0.5mm de
        // sobra na parede. Chamada 2x por grupo - uma vez por área
        // cilíndrica do degrau (FACES_CYLINDER_1 = diâmetro maior/cabeça,
        // FACES_CYLINDER_2 = diâmetro menor/haste) - só muda a área e o
        // código curto do nome (opCode: "MCR1"/"MCR2").
        string operationName = MakeUniqueOperationName(usedNames, opCode + "_" + ShortFeatureTag(featureGroup.Name) + "_D25");
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "hole_making", "HOLE_MILLING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        CylinderMilling cylinderMilling = (CylinderMilling)operation;
        CylinderMillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling);

        builder.CutPattern = CylinderMillingBuilder.CutPatternTypes.Circular;
        builder.CuttingParameters.PartStock.Value = 0.5;

        NXOpen.CAM.FBM.FeatureGeometry featureGeometry = builder.GetFeatureGeometry();
        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningGeometry = (NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry;
        machiningGeometry.UseModelDepth = false;
        try { machiningGeometry.SetMachiningArea(machiningArea); }
        catch { /* algumas features podem não possuir essa área */ }

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 2800.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 1800.0;

        builder.AxialStepover.DistanceBuilder.Value = 0.25;
        builder.CuttingParameters.TopOffset.Distance = 1.0;

        CylinderMillingCutParameters cutParameters = (CylinderMillingCutParameters)builder.CuttingParameters;
        cutParameters.BottomOffset.Distance = 2.0;

        builder.NonCuttingBuilder.Engage.EngRetType = NcmHoleMachiningEngRet.EngRetTypes.None;
        builder.NonCuttingBuilder.Retract.EngRetType = NcmHoleMachiningEngRet.EngRetTypes.None;
        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Desbaste cilíndrico (" + machiningArea + ") criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreateMoldColumnFinishOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool,
        FeatureGeometryGroup featureGroup, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        // OP_04 de referência: acabamento (HOLE_MILLING, CylinderMilling
        // circular) do diâmetro maior/cabeça com topo D10, stepover radial
        // multi-passe (aproxima em 0.1mm). Sem SetMachiningArea - aplica no
        // que sobrou pra acabar depois do desbaste dos dois diâmetros.
        string operationName = MakeUniqueOperationName(usedNames, "MCF_" + ShortFeatureTag(featureGroup.Name) + "_D10");
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "hole_making", "HOLE_MILLING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        CylinderMilling cylinderMilling = (CylinderMilling)operation;
        CylinderMillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling);

        builder.CutPattern = CylinderMillingBuilder.CutPatternTypes.Circular;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 2000.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 800.0;

        builder.AxialStepover.StepoverType = StepoverBuilder.StepoverTypes.Number;
        builder.RadialStepover.StepoverType = StepoverBuilder.StepoverTypes.Multiple;
        MultipleStepoverBuilder multipleStepover = builder.RadialStepover.MultipleBuilder;
        multipleStepover.Add(0, 3, 0.0, 0);
        multipleStepover.Modify(0, 3, 0.1, 0);

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Acabamento (coluna de molde) criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    private static void CreateMoldColumnBoringOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool,
        FeatureGeometryGroup featureGroup, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        // OP_05 de referência: mandrilhamento de precisão (DEEP_HOLE_DRILLING
        // com barra de mandrilar UGT0333_007) do diâmetro menor/haste
        // (FACES_CYLINDER_2) - avanço bem mais lento (feed 100) que a
        // pré-furação, pra dar o acabamento/tolerância fina do furo da bucha.
        string operationName = MakeUniqueOperationName(usedNames, "MCB_" + ShortFeatureTag(featureGroup.Name) + "BORE_BAR_DIAM_40");
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "hole_making", "DEEP_HOLE_DRILLING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);

        builder.CycleTable.MotionOutput = Cycle.MotionOutputTypes.MachineCycle;
        builder.CycleTable.AxialStepover.StepoverType = StepoverBuilder.StepoverTypes.None;

        NXOpen.CAM.FBM.FeatureGeometry featureGeometry = builder.GetFeatureGeometry();
        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningGeometry = (NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry;
        builder.ControlPointOffset = HoleDrillingBuilder.ControlPointOffsetType.Feature;
        builder.IntersectionStrategy = HoleDrillingBuilder.IntersectionStrategyType.Ipw;
        machiningGeometry.UseModelDepth = false;
        try { machiningGeometry.SetMachiningArea("FACES_CYLINDER_2"); }
        catch { /* algumas features podem não possuir essa área */ }

        builder.FeedsBuilder.SpindleRpmBuilder.Value = 1000.0;
        builder.FeedsBuilder.FeedCutBuilder.Value = 100.0;

        builder.CuttingParameters.TopOffset.Distance = 1.0;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Mandrilhamento (coluna de molde) criado para: " + featureGroup.Name);

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // COUNTERBORE COMUM — FG_STEP2HOLE pequeno (DIAMETER_1 < MOLD_COLUMN_MIN_DIAMETER)
    // ══════════════════════════════════════════════════════════════════════
    //
    // Porta direta do AUTOMATIC_COUNTERBORE.cs (hole_making/COUNTERBORING)
    // pra dentro deste script - RPM/feed calculados a partir do Vc de broca
    // (DrillVc) do material escolhido no início, igual ao original.
    private static void CreateCounterboreOperation(
        Part workPart, NCGroup programGroup, NCGroup methodGroup, Tool tool,
        FeatureGeometryGroup featureGroup, string diameterLabel, double diameter,
        MaterialCuttingData material, Dictionary<string, bool> usedNames, LogBuffer lw)
    {
        string operationName = MakeUniqueOperationName(usedNames, "CB_" + ShortFeatureTag(featureGroup.Name) + "_D" + diameterLabel);
        Operation operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
            programGroup, methodGroup, tool, featureGroup,
            "hole_making", "COUNTERBORING",
            OperationCollection.UseDefaultName.False,
            operationName, operationName);

        HoleDrilling holeDrilling = (HoleDrilling)operation;
        HoleDrillingBuilder builder =
            workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling);

        double rpm = (material.DrillVc * 318.0) / diameter;
        double feed = rpm * COUNTERBORE_FEED_PER_REV;

        builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
        builder.FeedsBuilder.FeedCutBuilder.Value = feed;

        builder.CuttingParameters.TopOffset.Distance = 2.0;

        builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
        builder.NonCuttingBuilder.TransferClearance.SafeDistance = 30.0;

        NXObject committedObject = builder.Commit();
        CAMObject[] operations = { (CAMObject)committedObject };
        workPart.CAMSetup.GenerateToolPath(operations);

        if (VERBOSE)
            lw.WriteLine("Counterbore criado para: " + featureGroup.Name + " (RPM=" + rpm.ToString("0", CultureInfo.InvariantCulture)
                + ", feed=" + feed.ToString("0", CultureInfo.InvariantCulture) + ")");

        builder.Destroy();
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS COMPARTILHADOS
    // ══════════════════════════════════════════════════════════════════════
    private static List<FeatureGeometryGroup> GetAllFeatureGeometryGroups(Part workPart)
    {
        List<FeatureGeometryGroup> groups = new List<FeatureGeometryGroup>();
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects)
        {
            FeatureGeometryGroup fg = obj as FeatureGeometryGroup;
            if (fg != null)
                groups.Add(fg);
        }
        return groups;
    }

    // ══════════════════════════════════════════════════════════════════════
    // RAIO DE CANTO DAS FIGURAS (pra seleção de ferramenta de rough/finish)
    // ══════════════════════════════════════════════════════════════════════
    //
    // Tenta primeiro por atributo (rápido, exato - mas o nome real do
    // atributo ainda não foi confirmado, ver CornerRadiusAttrCandidates).
    // Se não achar, tenta medir geometricamente: procura, entre as faces
    // cilíndricas da feature, a de menor raio cujo eixo seja
    // aproximadamente vertical (paralelo ao eixo Z / eixo da ferramenta) -
    // esse é o fillet vertical do canto do bolsão/furo retangular.
    // ASSUNÇÃO: eixo Z global = eixo da ferramenta (mesma premissa usada no
    // resto do script, nunca transforma por CSYS da feature).
    private static bool TryGetCornerRadiusAttribute(FeatureGeometryGroup fg, out double raio, out string erro)
    {
        foreach (string candidate in CornerRadiusAttrCandidates)
        {
            if (TryGetAttributeDouble(fg, candidate, out raio, out erro))
                return true;
        }
        raio = 0.0;
        erro = "nenhum atributo de raio de canto encontrado (tentei: " + string.Join(", ", CornerRadiusAttrCandidates) + ")";
        return false;
    }

    private static bool TryMeasureCornerRadiusGeometric(FeatureGeometryGroup fg, NXOpen.UF.UFSession ufs, out double raio, out string erro)
    {
        raio = 0.0;
        erro = "";
        double[] eixoVertical = { 0.0, 0.0, 1.0 };
        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();
            if (camFeatures == null || camFeatures.Length == 0) { erro = "sem CAMFeature"; return false; }

            NXOpen.Face[] faces = camFeatures[0].GetFaces();
            if (faces == null || faces.Length == 0) { erro = "sem faces"; return false; }

            double melhorRaio = double.MaxValue;
            bool achou = false;

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

                if (faceRadius <= 0.001)
                    continue;

                double dot = Math.Abs((faceDir[0] * eixoVertical[0]) + (faceDir[1] * eixoVertical[1]) + (faceDir[2] * eixoVertical[2]));
                if (dot < 0.9)
                    continue; // eixo não é vertical -> não é o fillet de canto (é furo, chanfro etc.)

                if (faceRadius < melhorRaio)
                {
                    melhorRaio = faceRadius;
                    achou = true;
                }
            }

            if (!achou)
            {
                erro = "nenhuma face cilíndrica vertical encontrada (canto vivo ou geometria não reconhecida)";
                return false;
            }

            raio = melhorRaio;
            return true;
        }
        catch (Exception ex)
        {
            erro = ex.Message;
            return false;
        }
    }

    // Combina atributo + geometria; se nenhum dos dois achar E o próprio
    // atributo de tamanho da figura já for um raio (ex.: obround/slot cujo
    // "size" é RADIUS), usa esse valor como raio de canto - porque nesses
    // casos o raio já É o raio de canto/extremidade da própria figura.
    private static double? ResolveCornerRadius(FeatureGeometryGroup fg, string sizeAttrName, double sizeValue, NXOpen.UF.UFSession ufs, LogBuffer lw)
    {
        double raio;
        string erroAttr;
        if (TryGetCornerRadiusAttribute(fg, out raio, out erroAttr))
            return raio;

        string erroGeo;
        if (TryMeasureCornerRadiusGeometric(fg, ufs, out raio, out erroGeo))
        {
            if (VERBOSE)
                lw.WriteLine(fg.Name + " -> raio de canto medido geometricamente: " + raio.ToString("0.###", CultureInfo.InvariantCulture) + "mm.");
            return raio;
        }

        if (string.Equals(sizeAttrName, "RADIUS", StringComparison.OrdinalIgnoreCase))
        {
            // Obround/slot/superfície redonda/step2hole grande: o próprio
            // valor de tamanho já é o raio de canto/extremidade da figura.
            return sizeValue;
        }

        lw.WriteLine("AVISO: " + fg.Name + " -> não foi possível determinar o raio de canto (atributo: " + erroAttr + " / geometria: " + erroGeo
            + "). Seleção de ferramenta vai ignorar o raio de canto pra esse grupo.");
        return null;
    }

    private static void LogAttributeNames(FeatureGeometryGroup fg, LogBuffer lw)
    {
        List<string> attrNames = ListAttributeNames(fg);
        lw.WriteLine("       Atributos disponiveis em " + fg.Name + ": "
            + (attrNames.Count > 0 ? string.Join(", ", attrNames) : "(nenhum encontrado / nao foi possivel listar)"));
    }

    private static List<string> ListAttributeNames(FeatureGeometryGroup fg)
    {
        List<string> names = new List<string>();
        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();
            if (camFeatures == null || camFeatures.Length == 0)
                return names;

            NXOpen.CAM.CAMAttribute[] attrs = camFeatures[0].Attributes.ToArray();
            foreach (NXOpen.CAM.CAMAttribute attr in attrs)
            {
                System.Reflection.PropertyInfo nameProp = attr.GetType().GetProperty("Name");
                object val = (nameProp != null) ? nameProp.GetValue(attr, null) : null;
                names.Add(val != null ? val.ToString() : "(sem nome)");
            }
        }
        catch (Exception ex)
        {
            names.Add("(erro ao listar: " + ex.Message + ")");
        }
        return names;
    }

    private static bool TryGetAttributeDouble(FeatureGeometryGroup fg, string attrName, out double value, out string erro)
    {
        value = 0.0;
        erro = "";
        try
        {
            NXOpen.CAM.CAMFeature[] camFeatures = fg.GetFeatures();
            if (camFeatures == null || camFeatures.Length == 0) { erro = "sem CAMFeature"; return false; }

            NXOpen.CAM.CAMAttributeCollection attrs = camFeatures[0].Attributes;
            NXOpen.CAM.CAMAttribute attr;
            try { attr = attrs.FindObject(attrName); }
            catch (Exception ex) { erro = attrName + " não existe: " + ex.Message; return false; }
            if (attr == null) { erro = attrName + " retornou null"; return false; }

            System.Reflection.MethodInfo getInt = attr.GetType().GetMethod("GetIntegerValue", Type.EmptyTypes);
            if (getInt != null)
            {
                try { getInt.Invoke(attr, null); }
                catch { /* esperado falhar - aquece o objeto antes da leitura real */ }
            }

            System.Reflection.MethodInfo getDouble = attr.GetType().GetMethod("GetDoubleValue", Type.EmptyTypes);
            if (getDouble == null) { erro = "GetDoubleValue não encontrado via reflection"; return false; }

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
                parentGroup, "mill_planar", "PROGRAM",
                NCGroupCollection.UseDefaultName.False, folderName, "Program");

            ProgramOrderGroupBuilder programOrderGroupBuilder1 =
                workPart.CAMSetup.CAMGroupCollection.CreateProgramOrderGroupBuilder(novaPasta);
            programOrderGroupBuilder1.Commit();
            programOrderGroupBuilder1.Destroy();

            usedNames[folderName] = true;
            return novaPasta;
        }
        catch (Exception ex)
        {
            lw.WriteLine("AVISO: falha ao criar a pasta '" + folderName + "': " + ex.Message + ". Operações desse grupo vão ficar soltas em " + parentGroup.Name + ".");
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
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
