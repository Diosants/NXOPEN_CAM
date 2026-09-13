// NX 2406
// LoadAndAlignCenteredVise_Auto.cs
//
// Versao para a morsa CENTERED/self-centering nova (botao na pagina "3+2
// Axis", chamada via LoadAndAlignCenteredVise_Auto.Run(null)). Carrega o
// componente (do disco, se necessario) e roda o alinhamento automatico, no
// mesmo padrao/arquitetura do LoadAndAlignVise_Auto.cs (que e a morsa
// ANTIGA, jaw_1/jaw_2 - nao mexi nela, continua intacta e e quem alinha a
// morsa antiga na pagina Setup).
//
// DIFERENCA DE TOPOLOGIA (corrigida em 29/08 - a primeira suposicao estava
// ERRADA): a morsa centered NAO tem os dois mordentes num unico componente.
// Olhando os filhos reais do MORSA-C-BASE-01.prt depois de adicionado, o
// nome certo do arquivo/componente e "CELIST_SLDPRT" (sem "X2" - aquele
// "X2" era so o jeito que a arvore do NX mostrava "2 ocorrencias", nao
// parte do nome) e ele aparece DUAS VEZES na arvore - 2 ocorrencias
// separadas do MESMO arquivo celist_sldprt.prt. Confirmado com o usuario
// (29/08): nao e um erro/duplicata acidental no CAD, e assim mesmo.
//
// Por isso o alinhamento agora trata TODAS as ocorrencias de
// "CELIST_SLDPRT" encontradas dentro do MORSA-C-BASE-01 (normalmente 2),
// aplicando o MESMO par de passos (Center22 de centralizacao + Distance de
// profundidade) em CADA uma independentemente, contra a MESMA peca de
// referencia. Isso funciona tanto se forem 2 mordentes fisicamente
// independentes quanto se as 2 ocorrencias juntas formam 1 mordente so -
// de qualquer jeito, cada ocorrencia fica centralizada em relacao a peca.
//
// SIMPLIFICACAO ASSUMIDA (avise se estiver errada depois que testar): pra
// cada ocorrencia, resolvo 2 dos 3 eixos de translacao automaticamente por
// geometria:
//   EIXO DE CENTRALIZACAO - detectado uma vez (na primeira ocorrencia
//     encontrada) via TryDetectBestCenteringPair, e reusado pras demais
//     ocorrencias (assumindo que todas compartilham o mesmo eixo de
//     abertura/fechamento da morsa) - Center22 contra o par simetrico
//     equivalente na peca.
//   EIXO DE PROFUNDIDADE (Z) - Distance, igual ao STEP 3 do sistema antigo:
//     testo os 2 eixos que sobraram depois da centralizacao e uso como
//     profundidade o que tiver o MAIOR par de faces opostas na peca
//     (fixtureBody) - assume que a maior face plana top/bottom da peca e
//     sempre a referencia de profundidade (valido pra chapas/placas de
//     molde, que e o caso de uso do projeto).
// O 3o eixo (o que sobra) fica SEM constraint contra a peca - assumi que,
// numa morsa self-centering, esse eixo e so a posicao fixa de montagem da
// morsa na mesa (onde ela ja fica parada, independente do tamanho da peca),
// nao algo que depende da peca. Se isso estiver errado (a morsa tambem
// precisa se alinhar nesse 3o eixo contra a peca), me fala o que a peca deve
// tocar nesse eixo que eu adiciono um 3o Distance/Touch, do mesmo jeito que
// o STEP 4 do sistema antigo (LoadAndAlignVise_Auto.cs).
//
// PENDENTE (29/08): o MORSA-C-BASE-01.prt tambem referencia um arquivo
// "ZAKLADNA 5 AXIS_SLDPRT" que NAO existe na pasta Resources (nem fonte nem
// bin\...\Debug) - por isso sempre aparece o aviso "1 sub-peca(s) nao
// carregada(s)" ao abrir o MORSA-C-BASE-01. Nao bloqueia o alinhamento (os
// mordentes CELIST_SLDPRT resolvem normalmente), mas esse componente vai
// aparecer faltando/quebrado na arvore ate voce localizar esse .prt e
// colocar em Resources.
//
// Depende da classe FaceFinderHelpers, que ja esta no projeto dentro de
// VISES_FIXTURES\ALIGN_VISE.cs (mesma classe reusada pelo LoadAndAlignVise_
// Auto.cs) - NAO cole essa classe aqui de novo, senao da erro de definicao
// duplicada/ambiguidade.
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.Positioning;

public class LoadAndAlignCenteredVise_Auto
{
    // =====================================================================
    // CONFIGURACAO - ajuste aqui, sem precisar mexer no meio do codigo
    // =====================================================================
    // --- Carregamento do componente ---
    // Nome do ARQUIVO .prt (sem extensao) - usado pra montar o caminho em
    // Resources\ e pra achar a peca ja carregada na sessao por nome de
    // arquivo. CORRIGIDO (28/08): o nome real do arquivo e "MORSA-C-BASE-01"
    // (nao "centered_vise_assmbly", que era so um nome provisorio que eu
    // tinha escolhido antes de voce confirmar o nome real). Coloque o .prt
    // em:
    // <pasta do projeto>\Resources\MORSA-C-BASE-01.prt
    // e marque "Copy to Output Directory" = "Copy if newer" nas
    // propriedades do arquivo no Visual Studio.
    private const string COMPONENT_FILE_NAME = "MORSA-C-BASE-01";
    private const string COMPONENT_RELATIVE_PATH = @"Resources\MORSA-C-BASE-01.prt";

    // Nome (ou prefixo) do(s) componente(s) FILHO(S) do MORSA-C-BASE-01 que
    // tem a geometria de verdade do mordente - CORRIGIDO (29/08): o nome
    // real e "CELIST_SLDPRT" (sem "X2" - isso era so a arvore do NX
    // mostrando "2 ocorrencias", nao parte do nome). Pode haver mais de uma
    // ocorrencia com esse nome (normalmente 2) - todas sao tratadas em
    // Run(), ver FindAllComponents.
    private const string ASSEMBLY_COMPONENT_NAME = "CELIST_SLDPRT";

    // Arquivos "irmaos" referenciados internamente pelo MORSA-C-BASE-01.prt
    // (confirmado pelo usuario 28/08) - o NX so resolve essas referencias
    // se eles JA ESTIVEREM carregados na sessao ANTES do MORSA-C-BASE-01.prt
    // ser aberto/adicionado. Estao na MESMA pasta (Resources\, relativo a
    // pasta do .dll) que o MORSA-C-BASE-01.prt.
    private static readonly string[] SIBLING_FILE_NAMES = new string[]
    {
        "01_sldprt",
        "02_R_sldprt",
        "03_L_sldprt",
        "04_sldprt",
        "celist_sldprt",
    };

    // --- Alinhamento ---
    private const double FACE_ANGLE_TOLERANCE_DEG = 5.0;

    // Deslocamento em Z (profundidade) da peca em relacao a face de
    // referencia da morsa - mesmo papel do Z_DEPTH_OFFSET_MM do
    // LoadAndAlignVise_Auto.cs. Ajuste aqui se o resultado sair na
    // profundidade errada (ou inverta trocando fixtureDepthNeg/Pos e
    // viseDepthNeg/Pos mais abaixo).
    private const string Z_DEPTH_OFFSET_MM = "0";

    /// <summary>
    /// Calcula o caminho completo do .prt a partir da pasta onde o
    /// assembly (.dll) atual esta rodando, combinada com o caminho
    /// relativo configurado acima.
    /// </summary>
    private static string GetComponentFullPath()
    {
        string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir, COMPONENT_RELATIVE_PATH);
    }

    /// <summary>
    /// Ponto de entrada para chamar a partir da aplicacao:
    /// LoadAndAlignCenteredVise_Auto.Run(null);
    /// </summary>
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        ListingWindow lw = theSession.ListingWindow;

        Session.UndoMarkId markId = theSession.SetUndoMark(Session.MarkVisibility.Invisible, "LoadAndAlignCenteredVise_Auto");

        // -----------------------------------------------------------
        // PARTE 1 - Garantir que a montagem da morsa (MORSA-C-BASE-01)
        // esta na montagem atual (carrega do disco se necessario, e nao
        // duplica se ja estiver la).
        // -----------------------------------------------------------
        Component viseAssembly = FindComponent(workPart.ComponentAssembly.RootComponent, COMPONENT_FILE_NAME);
        if (viseAssembly == null)
        {
            Component addedComponent;
            if (!AddViseComponent(theSession, workPart, lw, out addedComponent))
            {
                FlushUpdate(theSession, markId, lw); // erro ja reportado dentro de AddViseComponent
                return;
            }
            if (addedComponent == null)
            {
                LogMessage(lw, "ERRO: componente foi adicionado mas o objeto retornado nao e um Component valido.");
                FlushUpdate(theSession, markId, lw);
                return;
            }
            viseAssembly = addedComponent;
        }

        // -----------------------------------------------------------
        // PARTE 1b - Dentro da montagem da morsa (MORSA-C-BASE-01, que nao
        // tem Body proprio - e so um conjunto de componentes), localizar
        // TODAS as ocorrencias do mordente de verdade (CELIST_SLDPRT -
        // normalmente 2, ver header do arquivo).
        // -----------------------------------------------------------
        List<Component> jawComponents = new List<Component>();
        FindAllComponents(viseAssembly, ASSEMBLY_COMPONENT_NAME, jawComponents);
        if (jawComponents.Count == 0)
        {
            LogMessage(lw, "ERRO: nao encontrei nenhum componente contendo '" + ASSEMBLY_COMPONENT_NAME + "' dentro de '" + viseAssembly.Name + "'. Filhos diretos encontrados:");
            foreach (Component child in viseAssembly.GetChildren())
            {
                LogMessage(lw, "  - " + child.Name);
            }
            LogMessage(lw, "Confira o nome real do componente na arvore (Assembly Navigator) e ajuste a constante ASSEMBLY_COMPONENT_NAME se for diferente de '" + ASSEMBLY_COMPONENT_NAME + "'.");
            FlushUpdate(theSession, markId, lw);
            return;
        }
        LogMessage(lw, jawComponents.Count.ToString() + " ocorrencia(s) de '" + ASSEMBLY_COMPONENT_NAME + "' encontrada(s) - alinhando cada uma independentemente.");

        // -----------------------------------------------------------
        // PARTE 2 - Alinhamento automatico
        // -----------------------------------------------------------
        Body fixtureBody = FindFixtureBody(workPart, viseAssembly, lw);
        if (fixtureBody == null)
        {
            FlushUpdate(theSession, markId, lw); // erro ja reportado dentro de FindFixtureBody
            return;
        }
        List<FaceFinderHelpers.PlanarFaceInfo> fixtureFaces = FaceFinderHelpers.GetPlanarFaces(fixtureBody);

        // --- Eixo de centralizacao - detectado uma unica vez, na PRIMEIRA
        //     ocorrencia do mordente, e reusado pras demais (assume que
        //     todas as ocorrencias compartilham o mesmo eixo de
        //     abertura/fechamento da morsa). ---
        Body firstJawBody = FaceFinderHelpers.GetOccurrenceBody(jawComponents[0]);
        if (firstJawBody == null)
        {
            LogMessage(lw, "ERRO: nao consegui obter o corpo (Body) do componente '" + jawComponents[0].Name + "'.");
            FlushUpdate(theSession, markId, lw);
            return;
        }
        List<FaceFinderHelpers.PlanarFaceInfo> firstJawFaces = FaceFinderHelpers.GetPlanarFaces(firstJawBody);

        int centeringAxis;
        Face firstJawNeg, firstJawPos;
        bool centerAxisOk = FaceFinderHelpers.TryDetectBestCenteringPair(firstJawFaces, FACE_ANGLE_TOLERANCE_DEG, out centeringAxis, out firstJawNeg, out firstJawPos);
        if (!centerAxisOk)
        {
            LogMessage(lw, "ERRO: nao foi possivel detectar o eixo de centralizacao da morsa (usando '" + jawComponents[0].Name + "' como referencia).");
            FlushUpdate(theSession, markId, lw);
            return;
        }

        Face fixtureNeg, fixturePos;
        double fixtureMatchedArea;
        bool fixturePairOk = FaceFinderHelpers.TryFindSymmetricFacePair(fixtureFaces, centeringAxis, FACE_ANGLE_TOLERANCE_DEG, out fixtureNeg, out fixturePos, out fixtureMatchedArea);
        if (!fixturePairOk)
        {
            LogMessage(lw, "ERRO: nao foi possivel encontrar o par simetrico da peca no eixo de centralizacao detectado (eixo " + centeringAxis + ").");
            FlushUpdate(theSession, markId, lw);
            return;
        }

        // --- Eixo de profundidade (Z) - detectado uma unica vez a partir
        //     da FIXTURE (nao depende de qual ocorrencia do mordente):
        //     testa os 2 eixos que sobraram depois da centralizacao e usa
        //     como profundidade o que tiver o MAIOR par de faces opostas
        //     na peca. ---
        int depthAxis = -1;
        double bestFixtureArea = -1;
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == centeringAxis) continue;
            Face testNeg, testPos;
            double testArea;
            if (FaceFinderHelpers.TryFindOpposingFacePair(fixtureFaces, axis, FACE_ANGLE_TOLERANCE_DEG, out testNeg, out testPos, out testArea))
            {
                if (testArea > bestFixtureArea)
                {
                    bestFixtureArea = testArea;
                    depthAxis = axis;
                }
            }
        }
        if (depthAxis < 0)
        {
            LogMessage(lw, "AVISO: nao foi possivel detectar o eixo de profundidade automaticamente - alinhamento ficou so com a centralizacao. Confira a posicao em Z manualmente.");
        }
        Face fixtureDepthNeg = null, fixtureDepthPos = null;
        if (depthAxis >= 0)
        {
            double fixtureDepthArea;
            bool fixtureDepthOk = FaceFinderHelpers.TryFindOpposingFacePair(fixtureFaces, depthAxis, FACE_ANGLE_TOLERANCE_DEG, out fixtureDepthNeg, out fixtureDepthPos, out fixtureDepthArea);
            if (!fixtureDepthOk)
            {
                LogMessage(lw, "AVISO: nao foi possivel detectar as faces de profundidade da peca (eixo " + depthAxis + ") - alinhamento ficou so com a centralizacao.");
                depthAxis = -1;
            }
        }

        // --- Aplica Center22 (+ Distance de profundidade, se detectada)
        //     em CADA ocorrencia do mordente, independentemente. ---
        foreach (Component jaw in jawComponents)
        {
            AlignJawToFixture(workPart, jaw, centeringAxis, fixtureNeg, fixturePos, depthAxis, fixtureDepthNeg, lw);
        }

        // -----------------------------------------------------------
        // PARTE 3 - Esconde a exibicao grafica das constraints de
        // montagem, so depois dos passos do alinhamento terem terminado.
        // -----------------------------------------------------------
        ComponentPositioner assemblyPositioner = workPart.ComponentAssembly.Positioner;
        assemblyPositioner.DisplayConstraints = false;
        assemblyPositioner.DisplaySuppressedConstraints = false;
        workPart.ModelingViews.WorkView.Regenerate();

        // -----------------------------------------------------------
        // PARTE 4 - Forca a NX a processar e EXIBIR imediatamente tudo que
        // ficou pendente desde markId (mesmo motivo documentado no
        // LoadAndAlignVise_Auto.cs).
        // -----------------------------------------------------------
        FlushUpdate(theSession, markId, lw);
    }

    // =====================================================================
    // Log / Update helpers
    // =====================================================================
    /// <summary>
    /// Escreve na Listing Window abrindo-a sob demanda (lazy). Assim,
    /// numa execucao bem-sucedida (sem nenhum ERRO/Aviso) a janela de
    /// log nunca chega a aparecer para o usuario.
    /// </summary>
    private static void LogMessage(ListingWindow lw, string message)
    {
        if (!lw.IsOpen)
        {
            lw.Open();
        }
        lw.WriteLine(message);
    }

    /// <summary>
    /// Forca o Update Manager a processar imediatamente tudo que foi
    /// alterado desde 'markId' (equivalente ao que a propria NX faz ao
    /// final de qualquer comando gravado em journal). Isso garante que
    /// o resultado do alinhamento (posicao final da morsa) seja exibido
    /// na tela assim que Run() termina, sem depender de nenhum evento
    /// externo (como fechar a aplicacao) para "destravar" a atualizacao
    /// pendente. Mesmo padrao/motivo do LoadAndAlignVise_Auto.cs.
    /// </summary>
    private static void FlushUpdate(Session theSession, Session.UndoMarkId markId, ListingWindow lw)
    {
        try
        {
            int updateErrorCount = theSession.UpdateManager.DoUpdate(markId);
            if (updateErrorCount > 0)
            {
                LogMessage(lw, "Aviso: UpdateManager.DoUpdate retornou " + updateErrorCount.ToString() + " erro(s) ao finalizar a atualizacao do modelo.");
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: falha ao forcar UpdateManager.DoUpdate: " + ex.Message);
        }
    }

    // =====================================================================
    // Carregamento do componente
    // =====================================================================
    /// <summary>
    /// Localiza a peca da morsa (MORSA-C-BASE-01, a montagem PAI) ja
    /// carregada na sessao, ou carrega do disco (caminho calculado por
    /// GetComponentFullPath, apos garantir que os arquivos irmaos ja
    /// estao carregados) e adiciona na montagem atual. Retorna true em
    /// caso de sucesso, e devolve em addedComponent o Component do
    /// MORSA-C-BASE-01 recem-adicionado (direto do retorno do Commit()) -
    /// quem localiza as ocorrencias FILHAS de CELIST_SLDPRT (o corpo
    /// rigido de verdade) dentro dele e o Run() (via FindAllComponents).
    /// </summary>
    private static bool AddViseComponent(Session theSession, Part workPart, ListingWindow lw, out Component addedComponent)
    {
        addedComponent = null;
        Part sourcePart = FindLoadedPart(theSession, COMPONENT_FILE_NAME);
        if (sourcePart == null)
        {
            string componentFullPath = GetComponentFullPath();
            if (!File.Exists(componentFullPath))
            {
                LogMessage(lw, "ERRO: peca '" + COMPONENT_FILE_NAME + "' nao esta carregada na sessao " +
                    "e o arquivo nao foi encontrado em: " + componentFullPath);
                LogMessage(lw, "Confira se o .prt esta em '" + COMPONENT_RELATIVE_PATH + "' (relativo a pasta do .dll) e se 'Copy to Output Directory' esta marcado.");
                return false;
            }

            // Carrega os arquivos irmaos ANTES do MORSA-C-BASE-01.prt, pra o
            // NX conseguir resolver as referencias internas dele. So avisa
            // se algum falhar (nao e fatal), mas o resultado pode sair
            // incompleto se algum ficar de fora.
            string resourcesDir = Path.GetDirectoryName(componentFullPath);
            int siblingWarnings = 0;
            foreach (string siblingName in SIBLING_FILE_NAMES)
            {
                if (!LoadSiblingFile(theSession, lw, resourcesDir, siblingName))
                {
                    siblingWarnings++;
                }
            }
            if (siblingWarnings > 0)
            {
                LogMessage(lw, "Aviso: " + siblingWarnings.ToString() + " de " + SIBLING_FILE_NAMES.Length.ToString() + " arquivo(s) irmao(s) do " + COMPONENT_FILE_NAME + " nao carregaram - ele pode sair incompleto.");
            }

            PartLoadStatus loadStatus;
            try
            {
                sourcePart = theSession.Parts.Open(componentFullPath, out loadStatus);
            }
            catch (NXException ex)
            {
                LogMessage(lw, "ERRO ao carregar '" + componentFullPath + "': " + ex.Message);
                return false;
            }
            if (loadStatus != null)
            {
                if (loadStatus.NumberUnloadedParts > 0)
                {
                    LogMessage(lw, "Aviso: " + loadStatus.NumberUnloadedParts.ToString() + " sub-peca(s) nao carregada(s) ao abrir " + COMPONENT_FILE_NAME + ".");
                }
                loadStatus.Dispose();
            }
            if (sourcePart == null)
            {
                LogMessage(lw, "ERRO: falha ao carregar a peca '" + componentFullPath + "'.");
                return false;
            }
        }

        AddComponentBuilder addComponentBuilder = workPart.AssemblyManager.CreateAddComponentBuilder();
        try
        {
            addComponentBuilder.SetAllowMultipleAssemblyLocations(false);
            addComponentBuilder.SetComponentAnchor(null);
            addComponentBuilder.SetInitialLocationType(AddComponentBuilder.LocationType.WorkPartAbsolute);
            addComponentBuilder.SetCount(1);
            addComponentBuilder.Layer = -1;
            addComponentBuilder.SetUseReferenceSetAndApplyInitialLocation(false);
            addComponentBuilder.ReferenceSet = "Entire Part";
            // CORRIGIDO (29/08): NAO forcar ComponentName = ASSEMBLY_COMPONENT_NAME
            // aqui - isso estava errado desde o inicio. O que esta sendo
            // adicionado agora e o MORSA-C-BASE-01 (a montagem PAI); as
            // ocorrencias de "CELIST_SLDPRT" sao componentes FILHOS dela
            // (celist_sldprt.prt, aparece 2x), nao o nome do componente que
            // este Commit() cria. Deixo o NX nomear o componente pai
            // normalmente (fica "MORSA-C-BASE-01") e localizo os filhos
            // depois, em Run(), via FindAllComponents(viseAssembly, ...).
            addComponentBuilder.SetCamComponentType(AddComponentBuilder.CamComponentType.Target);
            BasePart[] partsToAdd = new BasePart[1];
            partsToAdd[0] = sourcePart;
            addComponentBuilder.SetPartsToAdd(partsToAdd);
            NXObject result = addComponentBuilder.Commit();
            ErrorList errors = addComponentBuilder.GetOperationFailures();
            errors.Dispose();
            if (result == null)
            {
                LogMessage(lw, "Falha ao adicionar o componente '" + COMPONENT_FILE_NAME + "' na montagem.");
                return false;
            }
            addedComponent = result as Component;
            return true;
        }
        finally
        {
            addComponentBuilder.ResetPartsToAdd();
            addComponentBuilder.Destroy();
        }
    }

    /// <summary>
    /// Localiza a peca (por nome de arquivo, sem extensao) ja carregada na
    /// sessao atual - usada tanto pelo MORSA-C-BASE-01 quanto pelos
    /// arquivos irmaos, pra nao carregar a mesma peca do disco duas vezes.
    /// </summary>
    private static Part FindLoadedPart(Session theSession, string partName)
    {
        foreach (Part p in theSession.Parts)
        {
            string leafName = Path.GetFileNameWithoutExtension(p.FullPath);
            if (string.Equals(leafName, partName, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        return null;
    }

    /// <summary>
    /// Carrega um arquivo irmao (referenciado internamente pelo
    /// MORSA-C-BASE-01.prt) na sessao do NX, se ainda nao estiver
    /// carregado. So carrega (Session.Parts.Open) - nao adiciona na
    /// montagem, quem faz isso e o proprio NX ao resolver as referencias
    /// internas do assembly pai quando ele e aberto/adicionado depois.
    /// Retorna true se ja estava (ou ficou) carregado com sucesso.
    /// </summary>
    private static bool LoadSiblingFile(Session theSession, ListingWindow lw, string resourcesDir, string siblingName)
    {
        Part already = FindLoadedPart(theSession, siblingName);
        if (already != null)
        {
            return true;
        }

        string siblingPath = Path.Combine(resourcesDir, siblingName + ".prt");
        if (!File.Exists(siblingPath))
        {
            LogMessage(lw, "Aviso: arquivo irmao '" + siblingName + ".prt' nao encontrado em " + siblingPath);
            return false;
        }

        PartLoadStatus loadStatus;
        try
        {
            Part loaded = theSession.Parts.Open(siblingPath, out loadStatus);
            if (loadStatus != null)
            {
                loadStatus.Dispose();
            }
            if (loaded == null)
            {
                LogMessage(lw, "Aviso: falha ao carregar arquivo irmao '" + siblingName + ".prt' (retornou nulo).");
                return false;
            }
            return true;
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: falha ao carregar arquivo irmao '" + siblingName + ".prt' - " + ex.Message);
            return false;
        }
    }

    // =====================================================================
    // Helpers de constraint / componente
    // =====================================================================
    /// <summary>
    /// Abre/fecha o bloco de constraints de montagem (BeginAssemblyConstraints
    /// / EndAssemblyConstraints) em volta de um Center22 ou Distance, e
    /// chama Solve() duas vezes (mesmo padrao do LoadAndAlignVise_Auto.cs -
    /// uma unica chamada nem sempre converge de primeira).
    /// </summary>
    private static void RunConstraintBlock(Part workPart, Action<ComponentPositioner, ComponentNetwork> buildConstraints)
    {
        ComponentPositioner positioner = workPart.ComponentAssembly.Positioner;
        Network network = positioner.EstablishNetwork();
        positioner.BeginAssemblyConstraints();
        ComponentNetwork componentNetwork = (ComponentNetwork)network;
        componentNetwork.NetworkArrangementsMode = ComponentNetwork.ArrangementsMode.Existing;
        componentNetwork.DisplayComponent = null;
        componentNetwork.MoveObjectsState = true;
        buildConstraints(positioner, componentNetwork);
        componentNetwork.Solve();
        componentNetwork.Solve();
        positioner.PrimaryArrangement = null;
        positioner.ClearNetwork();
        positioner.EndAssemblyConstraints();
    }

    /// <summary>
    /// Localiza o Body da placa fixa/referencia (a peca a ser fixada) - mesma
    /// logica do LoadAndAlignVise_Auto.cs: procura direto por geometria
    /// (bodies), sem depender de nome de feature/historico de modelagem.
    ///   1) Body(s) direto(s) no workPart - se houver mais de um, o mais
    ///      simples (menos faces) e escolhido como referencia.
    ///   2) Se nao houver nenhum body no workPart, procura um componente
    ///      IRMAO da morsa direto na raiz da montagem.
    /// </summary>
    private static Body FindFixtureBody(Part workPart, Component viseComponent, ListingWindow lw)
    {
        List<Body> topLevelBodies = new List<Body>();
        foreach (Body b in workPart.Bodies)
        {
            topLevelBodies.Add(b);
        }
        if (topLevelBodies.Count == 1)
        {
            return topLevelBodies[0];
        }
        else if (topLevelBodies.Count > 1)
        {
            Body simplest = null;
            int minFaceCount = int.MaxValue;
            foreach (Body b in topLevelBodies)
            {
                int count = b.GetFaces().Length;
                if (count < minFaceCount)
                {
                    minFaceCount = count;
                    simplest = b;
                }
            }
            LogMessage(lw, "Aviso: " + topLevelBodies.Count.ToString() + " bodies encontrados no workPart. " +
                "Usando automaticamente o corpo mais simples (menos faces = " + minFaceCount.ToString() + ") como placa fixa.");
            return simplest;
        }

        Component root = workPart.ComponentAssembly.RootComponent;
        if (root != null)
        {
            List<Component> siblingCandidates = new List<Component>();
            foreach (Component child in root.GetChildren())
            {
                bool isVise = viseComponent != null && child.Tag == viseComponent.Tag;
                if (!isVise)
                {
                    siblingCandidates.Add(child);
                }
            }
            if (siblingCandidates.Count == 1)
            {
                Body occBody = FaceFinderHelpers.GetOccurrenceBody(siblingCandidates[0]);
                if (occBody != null)
                {
                    return occBody;
                }
                LogMessage(lw, "ERRO: componente '" + siblingCandidates[0].Name + "' encontrado, mas nao foi possivel obter seu Body de ocorrencia.");
                return null;
            }
            else if (siblingCandidates.Count > 1)
            {
                LogMessage(lw, "ERRO: ha " + siblingCandidates.Count.ToString() + " componentes na raiz alem da morsa - nao da pra escolher automaticamente qual e a peca a ser fixada.");
                foreach (Component c in siblingCandidates)
                {
                    LogMessage(lw, "  - " + c.Name);
                }
                return null;
            }
        }
        LogMessage(lw, "ERRO: nenhum body no workPart nem componente de referencia encontrado na raiz da montagem.");
        return null;
    }

    /// <summary>
    /// Localiza o sistema de coordenadas MCS_MILL (ja existente na
    /// configuracao CAM) - disponivel para uso futuro caso precise de
    /// uma referencia absoluta independente de qualquer componente. Mesmo
    /// helper do LoadAndAlignVise_Auto.cs, nao chamado no Run() atual.
    /// </summary>
    private static NXObject FindMcsMillCsys(Part workPart, ListingWindow lw)
    {
        try
        {
            foreach (CartesianCoordinateSystem csys in workPart.CoordinateSystems)
            {
                if (string.Equals(csys.Name, "MCS_MILL", StringComparison.OrdinalIgnoreCase))
                {
                    return csys;
                }
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: erro ao varrer workPart.CoordinateSystems: " + ex.Message);
        }
        try
        {
            NXOpen.CAM.CAMObject camObj = workPart.CAMSetup.CAMGroupCollection.FindObject("MCS_MILL");
            if (camObj is NXObject)
            {
                return (NXObject)camObj;
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: erro ao procurar MCS_MILL no CAMSetup: " + ex.Message);
        }
        LogMessage(lw, "ERRO: MCS_MILL nao encontrado (nem como CoordinateSystem, nem como grupo CAM).");
        return null;
    }

    private static Component FindComponent(Component parent, string partialName)
    {
        if (parent == null)
        {
            return null;
        }
        foreach (Component child in parent.GetChildren())
        {
            if (child.Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
            Component found = FindComponent(child, partialName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Igual ao FindComponent, mas coleta TODAS as ocorrencias que baterem
    /// com partialName (nao para na primeira) - usada pra achar as 2
    /// ocorrencias de CELIST_SLDPRT dentro da montagem da morsa.
    /// </summary>
    private static void FindAllComponents(Component parent, string partialName, List<Component> results)
    {
        if (parent == null)
        {
            return;
        }
        foreach (Component child in parent.GetChildren())
        {
            if (child.Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                results.Add(child);
            }
            FindAllComponents(child, partialName, results);
        }
    }

    /// <summary>
    /// Aplica o alinhamento (Center22 de centralizacao + Distance de
    /// profundidade, se detectada) numa UNICA ocorrencia do mordente
    /// (jaw), contra a peca de referencia. Chamada uma vez pra cada
    /// ocorrencia de CELIST_SLDPRT encontrada em Run() - assim funciona
    /// independente de ser 1 ou 2 (ou mais) ocorrencias.
    /// </summary>
    private static void AlignJawToFixture(Part workPart, Component jaw, int centeringAxis, Face fixtureNeg, Face fixturePos, int depthAxis, Face fixtureDepthNeg, ListingWindow lw)
    {
        Body jawBody = FaceFinderHelpers.GetOccurrenceBody(jaw);
        if (jawBody == null)
        {
            LogMessage(lw, "ERRO: nao consegui obter o corpo (Body) do componente '" + jaw.Name + "' - pulando esse mordente.");
            return;
        }
        List<FaceFinderHelpers.PlanarFaceInfo> jawFaces = FaceFinderHelpers.GetPlanarFaces(jawBody);

        Face jawNeg, jawPos;
        double jawArea;
        bool jawPairOk = FaceFinderHelpers.TryFindSymmetricFacePair(jawFaces, centeringAxis, FACE_ANGLE_TOLERANCE_DEG, out jawNeg, out jawPos, out jawArea);
        if (!jawPairOk)
        {
            LogMessage(lw, "ERRO: nao encontrei o par simetrico de centralizacao no componente '" + jaw.Name + "' (eixo " + centeringAxis + ") - pulando esse mordente.");
            return;
        }

        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Center22;
            ConstraintReference r1 = cc.CreateConstraintReference(jaw, jawNeg, false, false, false);
            ConstraintReference r2 = cc.CreateConstraintReference(jaw, jawPos, false, false, false);
            ConstraintReference r3 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureNeg, false, false, false);
            r3.SetFixHint(true);
            ConstraintReference r4 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixturePos, false, false, false);
            r4.SetFixHint(true);
        });

        if (depthAxis < 0 || fixtureDepthNeg == null)
        {
            return;
        }

        Face jawDepthNeg, jawDepthPos;
        double jawDepthArea;
        bool jawDepthOk = FaceFinderHelpers.TryFindOpposingFacePair(jawFaces, depthAxis, FACE_ANGLE_TOLERANCE_DEG, out jawDepthNeg, out jawDepthPos, out jawDepthArea);
        if (!jawDepthOk)
        {
            LogMessage(lw, "AVISO: nao foi possivel detectar as faces de profundidade no componente '" + jaw.Name + "' (eixo " + depthAxis + ") - esse mordente ficou so com a centralizacao.");
            return;
        }

        // Lado Neg da peca / Pos do mordente (mesma convencao usada no
        // LoadAndAlignVise_Auto.cs) - se sair invertido, troca aqui.
        Face pieceFaceRef = fixtureDepthNeg;   // STATIONARY
        Face jawFaceRef = jawDepthPos;          // MOTION
        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Distance;
            // MOTION OBJECT primeiro (o que efetivamente se move)
            ConstraintReference r5 = cc.CreateConstraintReference(jaw, jawFaceRef, false, false, false);
            // STATIONARY OBJECT - fixo, a referencia nao se move
            ConstraintReference r6 = cc.CreateConstraintReference(workPart.ComponentAssembly, pieceFaceRef, false, false, false);
            r6.SetFixHint(true);
            cc.SetExpression(Z_DEPTH_OFFSET_MM);
        });
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
